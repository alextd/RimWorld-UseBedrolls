using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using RimWorld;
using HarmonyLib;

namespace UseBedrolls
{
	public class HomeBedComp : GameComponent
	{
		public Dictionary<Pawn, Building_Bed> homeBeds = new Dictionary<Pawn, Building_Bed>();
		public Dictionary<Building_Bed, Pawn> bedOwners = new Dictionary<Building_Bed, Pawn>();

		public HomeBedComp(Game game) : base() { }

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref homeBeds, "homeBeds", LookMode.Reference, LookMode.Reference);
			homeBeds.RemoveAll(kvp => kvp.Key == null || kvp.Value == null);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				foreach (var kvp in homeBeds)
					bedOwners[kvp.Value] = kvp.Key;
			}
		}

		public static void Set(Pawn pawn, Building_Bed bed)
		{
			var comp = Current.Game.GetComponent<HomeBedComp>();
			comp.homeBeds[pawn] = bed;
			comp.bedOwners[bed] = pawn;
		}

		public static bool Get(Pawn pawn, out Building_Bed bed)
		{
			var comp = Current.Game.GetComponent<HomeBedComp>();
			return comp.homeBeds.TryGetValue(pawn, out bed);
		}

		public static bool Get(Building_Bed bed, out Pawn pawn)
		{
			var comp = Current.Game.GetComponent<HomeBedComp>();
			return comp.bedOwners.TryGetValue(bed, out pawn);
		}

		public static void Remove(Pawn pawn)
		{
			var comp = Current.Game.GetComponent<HomeBedComp>();
			if (comp.homeBeds.TryGetValue(pawn, out Building_Bed bed))
				comp.bedOwners.Remove(bed);
			comp.homeBeds.Remove(pawn);
		}

		public static void Remove(Building_Bed bed)
		{
			var comp = Current.Game.GetComponent<HomeBedComp>();
			if (comp.bedOwners.TryGetValue(bed, out Pawn pawn))
				comp.homeBeds.Remove(pawn);
			comp.bedOwners.Remove(bed);
		}
	}

	[HarmonyPatch(typeof(Building_Bed), "DeSpawn")]
	public static class BedDeSpawnRemove
	{
		public static void Prefix(Building_Bed __instance)
		{
			Log.Message($"Removing {__instance}");
			HomeBedComp.Remove(__instance);
		}
	}

	[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
	public static class ExitMapSaver
	{
		public static void Prefix(Pawn __instance)
		{
			if (!__instance.IsColonistPlayerControlled) return;

			if (__instance.ownership?.OwnedBed is Building_Bed bed)
			{
				if (bed.Map?.IsPlayerHome ?? false)
				{
					HomeBedComp.Set(__instance, bed);

					Log.Message($"Saving Home bed {bed} for {__instance}");
				}

				if(Mod.settings.unassignOnExit)
				{
					__instance.ownership.UnclaimBed();
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

			if (map?.IsPlayerHome ?? false)
			{
				if (HomeBedComp.Get(__instance, out Building_Bed homeBed) &&
					homeBed?.Map == map && !homeBed.ForPrisoners &&
					(Mod.settings.reclaimAggresively || RestUtility.IsValidBedFor(homeBed, __instance, __instance, false, true)))
				{
					Log.Message($"Re-claming Home bed {homeBed} for {__instance}");
					__instance.ownership?.ClaimBedIfNonMedical(homeBed);
				}
				Log.Message($"Removing Home beds for {__instance}");
				HomeBedComp.Remove(__instance);
			}
		}
	}


	[HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
	class TempBedGizmo
	{
		//public override IEnumerable<Gizmo> GetGizmos()
		public static void Postfix(ref IEnumerable<Gizmo> __result, Building_Bed __instance)
		{
			if(HomeBedComp.Get(__instance, out Pawn traveler))
			if (traveler != null)
			{
				List<Gizmo> result = __result.ToList();
				result.Add(new BedOwnerGizmo(traveler, "TD.TravelerOwned", () =>
				{
					HomeBedComp.Remove(traveler);
					traveler.ownership?.UnclaimBed();
				}));
				__result = result;
			}
		}
	}
}
