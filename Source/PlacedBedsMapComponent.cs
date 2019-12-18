using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Harmony;
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
}
