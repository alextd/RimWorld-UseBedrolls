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
            foreach (CodeInstruction i in instructions)
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

            Thing spare_bed = pawn.inventory.innerContainer.FirstOrDefault(tmini => tmini.GetInnerIfMinified() is Building_Bed b && b.def.building.bed_humanlike);
            if (spare_bed == null)
            {
                spare_bed = nearest_bed_laying_around(pawn);
                if (spare_bed == null)
                {
                    Pawn nearby_pawn = who_can_share_with(pawn);
                    if ((nearby_pawn != null))
                    {
                        spare_bed = nearby_pawn.inventory.innerContainer.FirstOrDefault(tmini => tmini.GetInnerIfMinified() is Building_Bed b && b.def.building.bed_humanlike);
                        nearby_pawn.inventory.innerContainer.TryDrop(spare_bed, ThingPlaceMode.Near, out spare_bed);
                        Log.Message(nearby_pawn.Name + " dropped bed at " + spare_bed.Position);
                    }
                }
            };
            MinifiedThing invBed = (MinifiedThing)spare_bed;
            if (invBed == null)
                return fallbackJob;
            Log.Message(pawn + " found " + invBed);

            Map map = pawn.Map;
            Building_Bed bed = (Building_Bed)invBed.GetInnerIfMinified();

            Func<IntVec3, Rot4, bool> cellValidatorDir = delegate (IntVec3 c, Rot4 direction)
            {
                if (RegionAndRoomQuery.RoomAtFast(c, map).isPrisonCell != pawn.IsPrisoner)
                    return false;

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

        private static Pawn who_can_share_with(Pawn sleepy_pawn)
        {
            List<Pawn> pawns = sleepy_pawn.Map.mapPawns.SpawnedPawnsInFaction(sleepy_pawn.Faction).ListFullCopy();
            Log.Message("pawns are " + pawns.ToStringSafeEnumerable());
            TraverseParms traverseParams = TraverseParms.For(sleepy_pawn, Danger.Deadly, TraverseMode.ByPawn, false);
            Predicate<Pawn> surplusFinder = delegate (Pawn p)
            {
                int count = p.CountBeds();
                Log.Message(p.Name + " has " + count + " beds");
                if (((p.RaceProps.Animal || p.ownership.OwnedBed != null) && count > 0)
                    || (count > 1))
                {
                    Log.Message(p.Name + " has can spare some");
                    if (sleepy_pawn.Map.reachability.CanReach(sleepy_pawn.Position, p, PathEndMode.ClosestTouch, traverseParams))
                    {
                        Log.Message(sleepy_pawn.Name + " can reach " + p.Name);
                        return true;
                    }
                }
                return false;
            };
            List<Pawn> surplusPawns = pawns.FindAll(surplusFinder);
            if (surplusPawns.NullOrEmpty())
                return null;
            Pawn generous_pawn = surplusPawns.MinBy(p => DistanceTo(p, sleepy_pawn));
            Log.Message("surplusPawns are " + generous_pawn);
            return generous_pawn;
        }

        private static Thing nearest_bed_laying_around(Pawn sleepy_pawn)
        {
            Predicate<Thing> validator = delegate (Thing t)
             {
                 return t.GetInnerIfMinified() is Building_Bed b && b.def.building.bed_humanlike;
             };
            List<Thing> groundBeds = sleepy_pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing).FindAll(t => validator(t));
            if (groundBeds.NullOrEmpty())
                return null;
            return groundBeds.MinBy(t => DistanceTo(t, sleepy_pawn));
        }

        private static int DistanceTo(Thing t1, Thing t2)
        {
            return (t1.Position - t2.Position).LengthManhattan;
        }
    }
}
