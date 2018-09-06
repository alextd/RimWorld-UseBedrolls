using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;

namespace UseBedrolls
{
	class JobGiver_TakeBedBack : ThinkNode_JobGiver
	{

		public override float GetPriority(Pawn pawn)
		{
			JobGiver_GetRest rester = pawn.thinker.GetMainTreeThinkNode<JobGiver_GetRest>();
			return (8f - (rester?.GetPriority(pawn) ?? 0f));
		}

		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!pawn.IsColonistPlayerControlled) return null;

			Log.Message(pawn + " taking back bed?");

			var placedBeds = pawn.Map.GetComponent<PlacedBedsMapComponent>().placedBeds;
			if (placedBeds.TryGetValue(pawn, out Building_Bed bed))
			{
				Log.Message(pawn + " has bed " + bed);
				if (pawn.CanReserve(bed))
					return new Job(JobDefOf.TakeBedroll, bed);
			}
			Log.Message(pawn + " has no bed");

			return null;
		}
	}
}
