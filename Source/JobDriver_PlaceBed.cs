using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using RimWorld;
using HarmonyLib;

namespace UseBedrolls
{
	[DefOf]
	public static class JobDefOf
	{
		public static JobDef PlaceBedroll;
		public static JobDef TakeBedroll;
	}

	public static class ClaimTheGoddamnBed
	{
		public static void ClaimTheGoddamnBedOkay(this Pawn pawn, Building_Bed newBed)
		{
			if (pawn.IsSlaveOfColony)
				newBed.ForOwnerType = BedOwnerType.Slave;

			pawn.ownership?.ClaimBedIfNonMedical(newBed);
		}
	}

	public class JobDriver_PlaceBedroll : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}
		protected override IEnumerable<Toil> MakeNewToils()
		{
			//Clean up any spare blueprints
			AddFinishAction(delegate
			{
				Blueprint blueprint = TargetB.Thing as Blueprint;
				if (!blueprint?.Destroyed ?? false)
				{ 
					Log.Message($"Cleaning up {blueprint}");
					blueprint.Destroy(DestroyMode.Vanish);
				}
			});

			job.count = 1;
			Thing bedroll = pawn.CurJob.targetA.Thing;

			if (bedroll.Spawned)
			{
				Log.Message($"{pawn} needs to pick up {bedroll}");
				yield return Toils_Reserve.Reserve(TargetIndex.A);
				yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
				yield return Toils_Haul.TakeToInventory(TargetIndex.A, 1);
			}
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

				pawn.ClaimTheGoddamnBedOkay(bed);

				Job restJob = new Job(RimWorld.JobDefOf.LayDown, TargetB);
				pawn.jobs.StartJob(restJob, JobCondition.Succeeded);
			};
			yield return restInBed;
		}
	}

	public class JobDriver_TakeBedroll : JobDriver_Uninstall
	{
		public override void Notify_Starting()
		{
			base.Notify_Starting();
			if(pawn.Map.designationManager.DesignationOn(Target)?.def != DesignationDefOf.Uninstall)
				pawn.Map.designationManager.AddDesignation(new Designation(Target, DesignationDefOf.Uninstall));
		}

		protected override void FinishedRemoving()
		{
			Thing minifiedThing = Building.Uninstall();
			pawn.Map.GetComponent<PlacedBedsMapComponent>().placedBeds.Remove(pawn);
			pawn.inventory.innerContainer.TryAdd(minifiedThing.SplitOff(1));

			if(HomeBedComp.Get(pawn, out Building_Bed bed))
			{
				pawn.ClaimTheGoddamnBedOkay(bed);
			}
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