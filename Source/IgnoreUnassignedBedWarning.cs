using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld;

namespace UseBedrolls
{
	[HarmonyPatch(typeof(Building_Bed), "RemoveAllOwners")]
	class IgnoreUnassignedBedWarning
	{
		//private void RemoveAllOwners(bool destroyed = false)
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			//public static void Message(string text, LookTargets lookTargets, MessageTypeDef def, bool historical = true)
			MethodInfo MessageInfo = AccessTools.Method(typeof(Messages), nameof(Messages.Message),
				new Type[] {typeof(string), typeof(LookTargets), typeof(MessageTypeDef), typeof(bool)});

			MethodInfo MaybeMessageInfo = AccessTools.Method(typeof(IgnoreUnassignedBedWarning), nameof(MaybeMessage));

			foreach (var inst in instructions)
			{
				if(inst.Calls(MessageInfo))
				{
					yield return new CodeInstruction(OpCodes.Ldarg_0);//Building_Bed this
					yield return new CodeInstruction(OpCodes.Call, MaybeMessageInfo);

				}
				else
					yield return inst;
			}
		}

		//public static void Message(string text, LookTargets lookTargets, MessageTypeDef def, bool historical = true)
		public static void MaybeMessage(string text, LookTargets lookTargets, MessageTypeDef def, bool historical, Building_Bed bed)
		{
			//Beds on hostile maps being picked up is triggering message that you lost a bed. That's silly. Let's not.
			//This is assuming the bed is lost due to this mod? Even if not, messaging a lost bed on an enemy map shouldn't happen
			if (bed.Map?.ParentFaction != Faction.OfPlayer) return;

			Messages.Message(text, lookTargets, def, historical);
		}

	}
}
