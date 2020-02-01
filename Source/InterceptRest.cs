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
		//protected override Job TryGiveJob(Pawn pawn)
		public static void Postfix(ref Job __result, Pawn pawn)
		{
			if (__result == null) return;

			if (!pawn.IsColonistPlayerControlled) return;

			Map map = pawn.Map;
			if (!Settings.Get().alsoColonies && map.IsPlayerHome)
			{
				if(!Settings.Get().alsoColoniesKnown)
				{
					Settings.Get().alsoColoniesKnown = true;
					Settings.Get().Write();
					Find.LetterStack.ReceiveLetter("TD.UseBedrollsUpdated".Translate(),
						TranslatorFormattedStringExtensions.Translate("TD.UpdateNewsColonyMaps", pawn),
						LetterDefOf.NeutralEvent, pawn);
					// I don't think this really needs to be hugslibs update news or anything. alsoColoniesKnown defaults
				}
				return;
			}

			if (__result.targetA.Thing is Building_Bed ownedBed)
			{
				if(!Settings.Get().distanceCheck || (ownedBed.Position).DistanceTo(pawn.Position) < Settings.Get().distance)
					return;//Have a bed that close enough, no need to get from inventory
			}

			MinifiedThing invBed = (MinifiedThing)FindMinifiedBed(pawn);
			if (invBed == null)	return ;
			Log.Message($"{pawn} found {invBed}");
			Building_Bed bed = (Building_Bed)invBed.GetInnerIfMinified();

			Func<IntVec3, Rot4, bool> cellValidatorDir = delegate (IntVec3 c, Rot4 direction)
			{
				if (RegionAndRoomQuery.RoomAt(c,map).isPrisonCell != pawn.IsPrisoner)
					return false;

				if (!GenConstruct.CanPlaceBlueprintAt(invBed.GetInnerIfMinified().def, c, direction, map).Accepted)
					return false;

				//Support ReplaceStuff allowing blueprints over beds
				if (EdificeBlocking(invBed.GetInnerIfMinified().def, c, direction, map))
					return false;

				if (!GenConstruct.CanPlaceBlueprintAt(invBed.GetInnerIfMinified().def, c, direction, map).Accepted)
					return false;

				//Each cell of bed:
				for (CellRect.CellRectIterator iterator = GenAdj.OccupiedRect(c, direction, bed.def.size).GetIterator();
						!iterator.Done(); iterator.MoveNext())
				{
					if (map.zoneManager.ZoneAt(iterator.Current) != null)
						return false;
					foreach (Thing t in iterator.Current.GetThingList(map))
						if (!(t is Pawn) && GenConstruct.BlocksConstruction(bed, t))
							return false;
				}

				return true;
			};

			IntVec3 root = invBed.PositionHeld;

			// North/East would be redundant, except for cells on edge ; oh well, too much code to handle that
			Predicate<IntVec3> cellValidator = delegate (IntVec3 c)
			{
				if (!cellValidatorDir(c, Rot4.South) && !cellValidatorDir(c, Rot4.West))
					return false;
				using (PawnPath path = map.pathFinder.FindPath(root, c, pawn))
				{
					return path.TotalCost < 500;
				}
			};
			
			Predicate<IntVec3> goodCellValidator = c =>
				!RegionAndRoomQuery.RoomAt(c, map).PsychologicallyOutdoors && cellValidator(c);

			IntVec3 placePosition = IntVec3.Invalid;
			TraverseParms trav = TraverseParms.For(pawn);
			if (!CellFinder.TryFindRandomReachableCellNear(root, map, 4, trav, goodCellValidator, null, out placePosition))
				if (!CellFinder.TryFindRandomReachableCellNear(root, map, 12, trav, goodCellValidator, null, out placePosition))
					if (!CellFinder.TryFindRandomReachableCellNear(root, map, 4, trav, cellValidator, null, out placePosition))
						CellFinder.TryFindRandomReachableCellNear(root, map, 12, trav, cellValidator, null, out placePosition);

			if (placePosition.IsValid)
			{
				Rot4 dir = cellValidatorDir(placePosition, Rot4.South) ? Rot4.South : Rot4.West;
				Blueprint_Install blueprint = GenConstruct.PlaceBlueprintForInstall(invBed, placePosition, map, dir, pawn.Faction);

				Log.Message($"{pawn} placing {blueprint} at {placePosition}");

				__result = new Job(JobDefOf.PlaceBedroll, invBed, blueprint)
				{
					haulMode = HaulMode.ToContainer
				};
			}
		}

		public static bool EdificeBlocking(BuildableDef entDef, IntVec3 center, Rot4 rot, Map map)
		{
			foreach(IntVec3 pos in GenAdj.OccupiedRect(center, rot, entDef.Size))
				foreach (Thing t in pos.GetThingList(map))
					if (t.def.IsEdifice())
						return true;
			return false;
		}

		public static Thing FindMinifiedBed(Pawn pawn)
		{
			Log.Message($"{pawn} looking for inventory beds");
			//inventory bed
			if (InventoryBed(pawn) is Thing invBed)
				return invBed;

			Log.Message($"{pawn} looking for ground beds");
			//minified bed laying around
			if (GroundMinifedBed(pawn) is Thing groundBed)
				return groundBed;

			Log.Message($"{pawn} looking for shared beds");
			//bed on another pawn? last chance.
			return SharedInventoryBed(pawn);
		}

		public static bool UseableBed(Thing miniThing, Pawn pawn)
		{
			return miniThing.GetInnerIfMinified() is Building_Bed b && 
				!b.def.building.bed_defaultMedical &&
				RestUtility.CanUseBedEver(pawn, b.def) &&
				!CaravanReserved(pawn, miniThing) &&
				InstallBlueprintUtility.ExistingBlueprintFor(miniThing) == null;
		}

		public static int CountBedsFor(this Pawn pawn, Pawn sleeper)
		{
			return pawn?.inventory?.innerContainer?
				.Where(t => UseableBed(t, sleeper)).Count() ?? 0;
		}

		public static bool CaravanReserved(Pawn pawn, Thing bed)
		{
			return pawn?.Map?.lordManager?.lords.Any(l => l.LordJob is LordJob_FormAndSendCaravan c && (c.transferables?.Any(t => t.things?.Any(thing => thing == bed) ?? false) ?? false)) ?? false;
		}

		public static Thing InventoryBed(Pawn pawn, Pawn sleeper = null)
		{
			return pawn.inventory.innerContainer.FirstOrDefault(tmini => UseableBed(tmini, sleeper ?? pawn));
		}

		public static Thing GroundMinifedBed(Pawn sleepy_pawn)
		{
			Predicate<Thing> validator = delegate (Thing t)
			{
				return UseableBed(t, sleepy_pawn) && sleepy_pawn.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.None);
			};
			List<Thing> groundBeds = sleepy_pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing).FindAll(t => validator(t));
			if (groundBeds.NullOrEmpty())
				return null;
			return groundBeds.MinBy(t => DistanceTo(t, sleepy_pawn));
		}

		public static Thing SharedInventoryBed(Pawn pawn)
		{
			Thing spareBed = null;
			Pawn pawnWithSpareBed = PawnWithSpareBed(pawn);
			if ((pawnWithSpareBed != null))
			{
				spareBed = InventoryBed(pawnWithSpareBed, pawn);
				Log.Message($"{pawnWithSpareBed} dropping bed {spareBed}");
				//dropping here is fine since this isn't a commanded job, shouldn't get multiple calls to TryGiveJob
				pawnWithSpareBed.inventory.innerContainer.TryDrop(spareBed, ThingPlaceMode.Near, out spareBed);
			}
			return spareBed;
		}
		public static Pawn PawnWithSpareBed(Pawn sleepyPawn)
		{
			TraverseParms traverseParams = TraverseParms.For(sleepyPawn, Danger.Deadly, TraverseMode.ByPawn, false);
			Predicate<Pawn> surplusFinder = delegate (Pawn p) {
				int count = p.CountBedsFor(sleepyPawn);
				Log.Message($"{p} has {count} beds");
				if (count > 1 || (count > 0 && SingleInvBedIsSpare(p, sleepyPawn)))
				{
					Log.Message($"{p} has can spare some");
					if (sleepyPawn.Map.reachability.CanReach(sleepyPawn.Position, p, PathEndMode.ClosestTouch, traverseParams))
					{
						Log.Message($"{sleepyPawn} can reach {p}");
						return true;
					}
				}
				return false;
			};
			List<Pawn> surplusPawns = sleepyPawn.Map.mapPawns.SpawnedPawnsInFaction(sleepyPawn.Faction).FindAll(surplusFinder);
			if (surplusPawns.NullOrEmpty())
				return null;

			Log.Message($"surplusPawns are {surplusPawns.ToStringSafeEnumerable()}");
			Pawn generousPawn = surplusPawns.MinBy(p => DistanceTo(p,sleepyPawn));
			Log.Message($"generousPawn is {generousPawn}");
			return generousPawn;
		}

		public static bool SingleInvBedIsSpare(Pawn p, Pawn sleepyPawn)
		{
			return p.RaceProps.Animal || p.ownership?.OwnedBed != null || LovePartnerRelationUtility.LovePartnerRelationExists(sleepyPawn, p);
		}

		public static int DistanceTo(Thing t1, Thing t2)
		{
			return (t1.Position - t2.Position).LengthManhattan;
		}
	}
}
