using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using HarmonyLib;
using RimWorld;

namespace UseBedrolls
{
	public class PlacedBedsMapComponent : MapComponent
	{
		public Dictionary<Pawn, Building_Bed> placedBeds = new Dictionary<Pawn, Building_Bed>();

		public PlacedBedsMapComponent(Map map) : base(map) { }

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref placedBeds, "placedBeds", LookMode.Reference, LookMode.Reference);
		}

		public static Pawn PlacedBedOwner(Map map, Building_Bed bed)
		{
			var placedBeds = map.GetComponent<PlacedBedsMapComponent>().placedBeds;
			return placedBeds.FirstOrDefault(kvp => kvp.Value == bed).Key;
		}
	}

	[HarmonyPatch(typeof(Building_Bed), "RemoveAllOwners")]
	public static class RemoveAllOwners_Patch
	{
		public static void Postfix(Building_Bed __instance)
		{
			Map map = __instance.Map;
			if (map == null) return;	//If it's not spawned, there should be no owners.
			// If it got despawned, owners were already removed.
			// If it hasn't spawned yet, there should be no opportunity to set owners.
			// This can be hit when the Replace Stuff mod transfers settings from a medical bed to a new bed built over it
			// The new bed is not yet spawned, but gets the medical setting from the old bed,
			// and setting medical to true removes all owners
			// That process could probably be made better by saving settings between spawned/not spawned but it hasn't been a problem elswhere.

			var placedBeds = map.GetComponent<PlacedBedsMapComponent>().placedBeds;
			foreach (KeyValuePair<Pawn, Building_Bed> kvp in placedBeds)
				if (kvp.Value == __instance)
				{
					placedBeds.Remove(kvp.Key);
					return;
				}
		}
	}


	[HarmonyPatch(typeof(Building_Bed), nameof(Building_Bed.DeSpawn))]
	public static class DontMessageUnassigned
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			//else if (InstallBlueprintUtility.ExistingBlueprintFor((Thing) this) == null)
			//IL_0012: ldarg.0      // this
			//IL_0013: call         class RimWorld.Blueprint_Install RimWorld.InstallBlueprintUtility::ExistingBlueprintFor(class Verse.Thing)
			//IL_0018: brtrue.s IL_0099
			MethodInfo ExistingInfo = AccessTools.Method(typeof(InstallBlueprintUtility), nameof(InstallBlueprintUtility.ExistingBlueprintFor));

			foreach (var inst in instructions)
			{
				yield return inst;
				if(inst.Calls(ExistingInfo))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);//Building_Bed
					yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DontMessageUnassigned), nameof(ShouldSkipMessage))); // ShouldSkipMessage(Blueprint_Install, Building_Bed)
				}
			}
		}

		public static bool ShouldSkipMessage(Blueprint_Install bp, Building_Bed bed)
		{
			return bp != null || //reinstall blueprint exists? No need to message
				PlacedBedsMapComponent.PlacedBedOwner(bed.Map, bed) != null; // This was a placed bed? No need to message
		}
	}
}
