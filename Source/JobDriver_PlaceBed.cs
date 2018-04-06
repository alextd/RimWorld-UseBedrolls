using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;

namespace UseBedrolls
{
	[DefOf]
	public static class JobDefOf
	{
		public static JobDef PlaceBedroll;
		public static JobDef TakeBedroll;
	}

	public class JobDriver_PlaceBedroll : JobDriver
	{
		public override bool TryMakePreToilReservations()
		{
			return true;
		}
		protected override IEnumerable<Toil> MakeNewToils()
		{
			job.count = 1;
			yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
			yield return Toils_Haul.CarryHauledThingToContainer();
			yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(TargetIndex.B, TargetIndex.C);
			yield return Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.C);
			Toil restInBed = new Toil();
			restInBed.initAction = delegate
			{
				Pawn pawn = restInBed.actor;
				Job curJob = pawn.jobs.curJob;
				Building_Bed bed = curJob.targetB.Thing as Building_Bed;

				pawn.Map.GetComponent<PlacedBedsMapComponent>().placedBeds[pawn] = bed;

				pawn.ownership.ClaimBedIfNonMedical(bed);

				Job restJob = new Job(RimWorld.JobDefOf.LayDown, TargetB);
				pawn.jobs.StartJob(restJob, JobCondition.Succeeded);
			};
			yield return restInBed;
		}
	}

	public class JobDriver_TakeBedroll : JobDriver_Uninstall
	{
		protected override int TotalNeededWork
		{
			get
			{
				return 5;
			}
		}

		public override void Notify_Starting()
		{
			base.Notify_Starting();
			if(pawn.Map.designationManager.DesignationOn(Target)?.def != DesignationDefOf.Uninstall)
				pawn.Map.designationManager.AddDesignation(new Designation(Target, DesignationDefOf.Uninstall));
		}

		protected override void FinishedRemoving()
		{
			pawn.Map.GetComponent<PlacedBedsMapComponent>().placedBeds.Remove(pawn);
			Thing minifiedThing = Building.Uninstall();
			pawn.inventory.innerContainer.TryAdd(minifiedThing.SplitOff(1));
		}
	}
}