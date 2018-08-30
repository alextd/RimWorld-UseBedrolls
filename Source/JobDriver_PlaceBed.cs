using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;
using Harmony;

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
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}
		protected override IEnumerable<Toil> MakeNewToils()
		{
			job.count = 1;
			yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
			yield return Toils_Haul.CarryHauledThingToContainer();
			yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(TargetIndex.B, TargetIndex.C);
			//yield return Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.C);//1.0 game destroys minified thing, but its job still does Deposit?
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
		/* Just use basegame uninstall. If this should change, it should go in building properties (1.0 update)
     *<building>
     *	<uninstallWork>5</uninstallWork>
     *</building>
		protected override float TotalNeededWork
		{
			get
			{
				return 5;
			}
		}
		*/

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
	
	[HarmonyPatch(typeof(GenConstruct), "BlocksConstruction")]
	static class PawnBlockConstruction
	{
		static bool Prefix(ref bool __result, Thing t, Thing constructible)
		{
			if (t is Pawn && 
				(constructible is Building_Bed || 
				(constructible is Blueprint_Install b && b.MiniToInstallOrBuildingToReinstall.GetInnerIfMinified() is Building_Bed)))
			{
				__result = false;
				return false;
			}
			return true;
		}
	}
}