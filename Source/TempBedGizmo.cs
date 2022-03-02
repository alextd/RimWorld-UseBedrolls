using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using HarmonyLib;
using UnityEngine;

namespace UseBedrolls
{
	public class BedOwnerGizmo : Gizmo
	{
		public Pawn owner;
		public string label;
		public Action cancelAction;

		public BedOwnerGizmo(Pawn o, string tag, Action a = null) : base()
		{
			label = tag.Translate(o.LabelShortCap);
			owner = o;
			order = -50f;
			cancelAction = a;
		}

		public override float GetWidth(float maxWidth)
		{
			return 140f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
			GUI.color = Color.white;
			GUI.DrawTexture(rect, Command.BGTex);

			Widgets.ThingIcon(rect, owner);

			//Label
			float num = Text.CalcHeight(label, rect.width);
			Rect rectLabel = new Rect(rect.x, rect.yMax - num + 12f, rect.width, num);
			GUI.color = Color.white;
			GUI.DrawTexture(rectLabel, TexUI.GrayTextBG);
			Text.Anchor = TextAnchor.UpperCenter;
			Widgets.Label(rectLabel, label);
			Text.Anchor = TextAnchor.UpperLeft;

			if (Widgets.ButtonInvisible(rect)) //&& Event.current.button == 1 // Eh, right click only? Any mouse button fine.
				return new GizmoResult(GizmoState.OpenedFloatMenu, Event.current);

			return new GizmoResult(GizmoState.Clear);
		}

		public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions =>
			new FloatMenuOption[] { new FloatMenuOption("CancelButton".Translate(), cancelAction) };
	}



	[HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
	class TravelerBedGizmo
	{
		//public override IEnumerable<Gizmo> GetGizmos()
		public static void Postfix(ref IEnumerable<Gizmo> __result, Building_Bed __instance)
		{
			var placedBeds = __instance.Map.GetComponent<PlacedBedsMapComponent>().placedBeds;

			Pawn owner = placedBeds.FirstOrDefault(kvp => kvp.Value == __instance).Key;
			if (owner != null)
			{
				List<Gizmo> result = __result.ToList();
				result.Add(new BedOwnerGizmo(owner, "TD.CarriedBy", () =>
				{
					__instance.Map.GetComponent<PlacedBedsMapComponent>().placedBeds.Remove(owner);
				}));
				__result = result;
			}
		}
	}
}
