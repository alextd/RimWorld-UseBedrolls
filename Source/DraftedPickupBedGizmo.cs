using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;
using Harmony;
using UnityEngine;

namespace UseBedrolls
{
	[HarmonyPatch(typeof(Pawn), "GetGizmos")]
	class PickBackUpBedGizmo
	{
		//public override IEnumerable<Gizmo> GetGizmos()
		public static void Postfix(ref IEnumerable<Gizmo> __result, Pawn __instance)
		{
			if (!__instance.Drafted) return;

			var placedBeds = __instance.Map.GetComponent<PlacedBedsMapComponent>().placedBeds;

			if (placedBeds.TryGetValue(__instance, out Building_Bed placedBed))
			{
				List<Gizmo> result = __result.ToList();
				result.Add(new Command_Action()
				{
					action = delegate () {
						Job pickupJob = new Job(JobDefOf.TakeBedroll, placedBed);
						__instance.jobs.StartJob(pickupJob);
					},
					defaultLabel = "TD.PickBackUpBed".Translate(),
					icon = ContentFinder<Texture2D>.Get("UI/Designators/Uninstall"),
					order = -50f
				});
				__result = result;
			}
		}
	}
}
