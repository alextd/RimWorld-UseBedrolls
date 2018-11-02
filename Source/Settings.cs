using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace UseBedrolls
{
	class Settings : ModSettings
	{
		public bool distanceCheck = false;
		public float distance = 100f;

		public static Settings Get()
		{
			return LoadedModManager.GetMod<UseBedrolls.Mod>().GetSettings<Settings>();
		}

		public void DoWindowContents(Rect wrect)
		{
			var options = new Listing_Standard();
			options.Begin(wrect);

			options.CheckboxLabeled("Use bedroll if too far from assigned bed", ref distanceCheck);
			if (distanceCheck)
			{
				options.Label("Use inventory bedroll if assigned bed is at least this far away:" + $" {distance:0.}");
				distance = options.Slider(distance, 0, 300);
			}

			options.End();
		}
		
		public override void ExposeData()
		{
			Scribe_Values.Look(ref distanceCheck, "distanceCheck", true);
			Scribe_Values.Look(ref distance, "distance", 100f);
		}
	}
}