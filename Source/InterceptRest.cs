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

			MinifiedThing invBed = (MinifiedThing)pawn.inventory.innerContainer.FirstOrDefault(tmini => tmini.GetInnerIfMinified() is Building_Bed b && b.def.building.bed_humanlike);
			if (invBed == null)	return fallbackJob;
			Log.Message(pawn + " found " + invBed);

			Map map = pawn.Map;
			Building_Bed bed = (Building_Bed)invBed.GetInnerIfMinified();

			Func<IntVec3, Rot4, bool> cellValidatorDir = delegate (IntVec3 c, Rot4 direction)
			{
				if (!GenConstruct.CanPlaceBlueprintAt(invBed.GetInnerIfMinified().def, c, direction, map).Accepted)
					return false;

				for (CellRect.CellRectIterator iterator = GenAdj.OccupiedRect(c, direction, bed.def.size).GetIterator();
						!iterator.Done(); iterator.MoveNext())
					foreach (Thing t in iterator.Current.GetThingList(map))
						if (!(t is Pawn) && GenConstruct.BlocksConstruction(bed, t))
							return false;

				return true;
			};

			// North/East would be redundant, except for cells on edge ; oh well, too much code to handle that
			Predicate<IntVec3> cellValidator = c => cellValidatorDir(c, Rot4.South) || cellValidatorDir(c, Rot4.West);
			
			Predicate<IntVec3> goodCellValidator = c =>
				!RegionAndRoomQuery.RoomAt(c, map).PsychologicallyOutdoors && cellValidator(c);

			IntVec3 placePosition = IntVec3.Invalid;
			IntVec3 root = pawn.Position;
			TraverseParms trav = TraverseParms.For(pawn);
			if (!CellFinder.TryFindRandomReachableCellNear(root, map, 4, trav, goodCellValidator, null, out placePosition))
				if (!CellFinder.TryFindRandomReachableCellNear(root, map, 12, trav, goodCellValidator, null, out placePosition))
					if (!CellFinder.TryFindRandomReachableCellNear(root, map, 4, trav, cellValidator, null, out placePosition))
						CellFinder.TryFindRandomReachableCellNear(root, map, 12, trav, cellValidator, null, out placePosition);

			if (placePosition.IsValid)
			{
				Rot4 dir = cellValidatorDir(placePosition, Rot4.South) ? Rot4.South : Rot4.West;
				Blueprint_Install blueprint = GenConstruct.PlaceBlueprintForInstall(invBed, placePosition, map, dir, pawn.Faction);

				Log.Message(pawn + " placing " + blueprint + " at " + placePosition);

				return new Job(JobDefOf.PlaceBedroll, invBed, blueprint)
				{
					haulMode = HaulMode.ToContainer
				};
			}
			Log.Message(pawn + " couldn't find place for " + invBed);

			return fallbackJob;
		}
	}
}
