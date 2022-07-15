﻿using OWML.Utils;
using UnityEngine;

namespace OWML.LightBramble
{
	public class AnglerPatch
	{
		static public void SectorUpdated(AnglerfishController __instance)
		{
			LightBramble.inst.ToggleFish(__instance, LightBramble.inst._disableFish);
		}
		static public void AwakePostfix(AnglerfishController __instance)
		{
			LightBramble.inst.anglerfishList.Add(__instance);
		}
		static public void OnDestroyPostfix(AnglerfishController __instance)
		{
			LightBramble.inst.anglerfishList.Remove(__instance);
		}
	}

	public class FogPatches
	{
		static public void FogWarpVolumePostfix(FogWarpVolume __instance)
		{
			LightBramble.inst.fogWarpVolumeDict.Add(__instance, __instance.GetValue<Color>("_fogColor"));
		}

		static public void PlanetaryFogPostfix(PlanetaryFogController __instance)
		{
			LightBramble.inst.planetaryFogControllerDict.Add(__instance, __instance.fogTint);
		}

		static public void FogLightPostfix(FogLight __instance)
		{
			FogLightData fogLightData = new FogLightData(__instance);
			LightBramble.inst.fogLightDataList.Add(fogLightData);
		}

		static public void FogOverrideVolumePostfix(FogOverrideVolume __instance)
		{
			LightBramble.inst.fogOverrideVolumeDict.Add(__instance, __instance.tint);
		}
	}

	public class GlobalMusicControllerPatch
	{
		static public void GlobalMusicControllerPostfix(GlobalMusicController __instance)
		{
			LightBramble.inst._brambleSource = __instance.GetValue<OWAudioSource>("_darkBrambleSource");

			LightBramble.inst._dekuSource = __instance.gameObject.AddComponent<AudioSource>();
			LightBramble.inst._dekuSource.clip = LightBramble.inst.ModHelper.Assets.GetAudio("deku-tree.mp3");
			LightBramble.inst.dekuAudioSource = __instance.gameObject.AddComponent<OWAudioSource>();
			LightBramble.inst.dekuAudioSource.time = 0;
			LightBramble.inst.dekuAudioSource.SetValue("_audioSource", LightBramble.inst._dekuSource);

			LightBramble.inst._brambleSource.Stop();
			__instance.SetValue("_darkBrambleSource", LightBramble.inst.dekuAudioSource);
		}
	}
}