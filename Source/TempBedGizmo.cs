using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using Harmony;
using UnityEngine;

namespace UseBedrolls
{
	public class BedOwnerGizmo : Gizmo
	{
		public Pawn owner;

		public BedOwnerGizmo(Pawn o) : base()
		{
			owner = o;
			order = -50f;
		}

		public override float GetWidth(float maxWidth)
		{
			return 140f;
		}

		public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth)
		{
			Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
			GUI.color = Color.white;
			GUI.DrawTexture(rect, Command.BGTex);

			Widgets.ThingIcon(rect, owner);

			//Label
			string label = "TD.CarriedBy".Translate(new string[] {owner.LabelShortCap});
			float num = Text.CalcHeight(label, rect.width);
			Rect rectLabel = new Rect(rect.x, rect.yMax - num + 12f, rect.width, num);
			GUI.color = Color.white;
			GUI.DrawTexture(rectLabel, TexUI.GrayTextBG);
			Text.Anchor = TextAnchor.UpperCenter;
			Widgets.Label(rectLabel, label);
			Text.Anchor = TextAnchor.UpperLeft;

			return new GizmoResult(GizmoState.Clear);
		}
	}


	[HarmonyPatch(typeof(Building_Bed), "GetGizmos")]
	class TempBedGizmo
	{
		//public override IEnumerable<Gizmo> GetGizmos()
		public static void Postfix(ref IEnumerable<Gizmo> __result, Building_Bed __instance)
		{
			var placedBeds = __instance.Map.GetComponent<PlacedBedsMapComponent>().placedBeds;

			Pawn owner = placedBeds.FirstOrDefault(kvp => kvp.Value == __instance).Key;
			if (owner != null)
			{
				List<Gizmo> result = __result.ToList();
				result.Add(new BedOwnerGizmo(owner));
				__result = result;
			}
		}
	}
}
