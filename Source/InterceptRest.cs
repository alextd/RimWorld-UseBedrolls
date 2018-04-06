using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using Verse;
using Verse.AI;
using RimWorld;
using Harmony;

namespace UseBedrolls
{
	[HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
	static class InterceptRest
	{
		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo FindBedrollJobInfo = AccessTools.Method(typeof(InterceptRest), nameof(InterceptRest.FindBedrollJob));

			bool foundNew = false;
			foreach(CodeInstruction i in instructions)
			{
				//ldsfld       class Verse.JobDef RimWorld.JobDefOf::LayDown
				yield return i;
				if (i.opcode == OpCodes.Newobj)
				{
					if (!foundNew) foundNew = true;
					else
					{
						yield return new CodeInstruction(OpCodes.Ldarg_1);
						yield return new CodeInstruction(OpCodes.Call, FindBedrollJobInfo);
					}
				}
			}
		}

		static Job FindBedrollJob(Job fallbackJob, Pawn pawn)
		{	
			if (!pawn.IsColonistPlayerControlled) return fallbackJob;
			Log.Message(pawn + " looking for inventory beds");

			MinifiedThing invBed = (MinifiedThing)pawn.inventory.innerContainer.FirstOrDefault(tmini => tmini.GetInnerIfMinified() is Building_Bed bed && bed.def.building.bed_humanlike);
			if (invBed == null)	return fallbackJob;
			Log.Message(pawn + " found " + invBed);

			Map map = pawn.Map;

			Predicate<IntVec3> cellValidator = c =>
				c.Standable(map) && !c.IsForbidden(pawn) && !c.GetTerrain(map).avoidWander
				&& GenConstruct.CanPlaceBlueprintAt(invBed.GetInnerIfMinified().def, c, Rot4.South, map).Accepted;

			Predicate<IntVec3> goodCellValidator = c =>
				cellValidator(c) && !RegionAndRoomQuery.RoomAt(c, map).PsychologicallyOutdoors;

			IntVec3 placePosition = IntVec3.Invalid;
			IntVec3 root = pawn.Position;
			TraverseParms trav = TraverseParms.For(pawn);
			if (!CellFinder.TryFindRandomReachableCellNear(root, map, 4, trav, goodCellValidator, null, out placePosition))
				if (!CellFinder.TryFindRandomReachableCellNear(root, map, 12, trav, goodCellValidator, null, out placePosition))
					if (!CellFinder.TryFindRandomReachableCellNear(root, map, 4, trav, cellValidator, null, out placePosition))
						CellFinder.TryFindRandomReachableCellNear(root, map, 12, trav, cellValidator, null, out placePosition);

			if (placePosition.IsValid)
			{
				Blueprint_Install blueprint = GenConstruct.PlaceBlueprintForInstall(invBed, placePosition, map, Rot4.South, pawn.Faction);

				Log.Message(pawn + " placing " + blueprint + " at " + placePosition);

				return new Job(JobDefOf.PlaceBedroll, invBed, blueprint)
				{
					haulMode = HaulMode.ToContainer
				};  //One assumes they will immediately use it.
			}
			Log.Message(pawn + " couldn't find place for " + invBed);

			return fallbackJob;
		}
	}
}
