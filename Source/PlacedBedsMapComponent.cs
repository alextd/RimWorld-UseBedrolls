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
			var placedBeds = __instance.Map.GetComponent<PlacedBedsMapComponent>().placedBeds;
			foreach (KeyValuePair<Pawn, Building_Bed> kvp in placedBeds)
				if (kvp.Value == __instance)
				{
					placedBeds.Remove(kvp.Key);
					return;
				}
		}
	}
}
