using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace UseBedrolls
{
	[HarmonyPatch(typeof(CaravanEnterMapUtility), "Enter", new Type[] { typeof(Caravan), typeof(Map), typeof(Func < Pawn, IntVec3 > ), typeof(CaravanDropInventoryMode), typeof(bool)})]
	static class CaravanBedrollSharer
	{
		//public static void Enter(Caravan caravan, Map map, Func<Pawn, IntVec3> spawnCellGetter, CaravanDropInventoryMode dropInventoryMode = CaravanDropInventoryMode.DoNotDrop, bool draftColonists = false)
		public static void Prefix(Caravan caravan)
		{
			Pawn colonist = caravan.PawnsListForReading.FirstOrDefault(p => p.IsFreeColonist);
			if (colonist == null) return;	//I don't know about this, caravans need at least one pawn, right?

			Log.Message($"Prefxing enter");
			List<Pawn> caravanPawns = new List<Pawn>(caravan.PawnsListForReading);
			Log.Message($"pawns are {caravanPawns.ToStringSafeEnumerable()}");
			List<Pawn> needPawns = caravanPawns.FindAll(p => p.IsFreeColonist && p.CountBedsFor(colonist) == 0 && p.inventory != null);
			Log.Message($"needPawns are {needPawns.ToStringSafeEnumerable()}");

			Predicate<Pawn> surplusFinder = delegate (Pawn p) {
				int count = p.CountBedsFor(colonist);
				return count > 1 || (p.RaceProps.Animal && count > 0);
			};
			List<Pawn> surplusPawns = caravanPawns.FindAll(surplusFinder);
			surplusPawns.Sort((p1, p2) => p1.CountBedsFor(colonist).CompareTo(p2.CountBedsFor(colonist)));
			Log.Message($"surplusPawns are {surplusPawns.ToStringSafeEnumerable()}");

			foreach (Pawn toPawn in needPawns)
			{
				Log.Message($"Finding bed for {toPawn}");
				if (surplusPawns.Count == 0) return;
				Pawn fromPawn = surplusPawns.First();
				Log.Message($"Getting bed from {fromPawn}");
				Thing bed = fromPawn.inventory.innerContainer
					.First(t => InterceptRest.UseableBed(t, colonist));
				Log.Message($"Bed is {bed}");

				fromPawn.inventory.innerContainer.TryTransferToContainer(bed, toPawn.inventory.innerContainer);
				if (!surplusFinder(fromPawn)) surplusPawns.RemoveAt(0);
			}
		}
	}
}
