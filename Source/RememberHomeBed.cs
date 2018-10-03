using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using RimWorld;
using Harmony;

namespace UseBedrolls
{
	public class HomeBedComp : GameComponent
	{
		public Dictionary<Pawn, Building_Bed> homeBeds = new Dictionary<Pawn, Building_Bed>();

		public HomeBedComp(Game game) : base() { }

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref homeBeds, "homeBeds", LookMode.Reference, LookMode.Reference);
		}

		public static Dictionary<Pawn, Building_Bed> Get()
		{
			return Current.Game.GetComponent<HomeBedComp>().homeBeds;
		}
	}

	//ought to be exitmap but despawn is called before it
	[HarmonyPatch(typeof(Pawn), "DeSpawn")]
	public static class ExitMapSaver
	{
		public static void Prefix(Pawn __instance)
		{
			if (__instance.ownership?.OwnedBed is Building_Bed bed)
			{
				Pawn owner = (Pawn)AccessTools.Field(typeof(Pawn_Ownership), "pawn").GetValue(__instance.ownership);
				if (owner.Map.IsPlayerHome)
				{
					HomeBedComp.Get()[owner] = bed;

					Log.Message($"Saving Home bed {bed} for {owner}");
				}
			}
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
	public static class SpawnClaimHomeBed
	{
		//public override void SpawnSetup(Map map, bool respawningAfterLoad)
		public static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
		{
			if (respawningAfterLoad) return;

			if (map.IsPlayerHome)
			{
				if (HomeBedComp.Get().TryGetValue(__instance, out Building_Bed homeBed) &&
					homeBed.Map == map && 
					RestUtility.IsValidBedFor(homeBed, __instance, __instance, false, true))
				{
					Log.Message($"Re-claming Home bed {homeBed} for {__instance}");
					__instance.ownership.ClaimBedIfNonMedical(homeBed);
				}
				Log.Message($"Removing Home beds for {__instance}");
				HomeBedComp.Get().Remove(__instance);
			}
		}
	}
}
