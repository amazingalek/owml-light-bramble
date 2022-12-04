﻿using OWML.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace OWML.LightBramble
{
	public static class Patches
	{
		public static void SetupPatches()
		{
			Common.IHarmonyHelper hmy = LightBramble.inst.ModHelper.HarmonyHelper;
			hmy.AddPrefix<AnglerfishController>(nameof(AnglerfishController.OnSectorOccupantsUpdated), typeof(AnglerPatch), nameof(AnglerPatch.SectorUpdated));
			hmy.AddPostfix<AnglerfishController>(nameof(AnglerfishController.Awake), typeof(AnglerPatch), nameof(AnglerPatch.AwakePostfix));
			hmy.AddPrefix<AnglerfishController>(nameof(AnglerfishController.OnDestroy), typeof(AnglerPatch), nameof(AnglerPatch.OnDestroyPrefix));
			hmy.AddPostfix<FogOverrideVolume>(nameof(FogOverrideVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogOverrideVolumePostfix));
			hmy.AddPostfix<FogWarpVolume>(nameof(FogWarpVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogWarpVolumePostfix));
			hmy.AddPostfix<PlanetaryFogController>(nameof(PlanetaryFogController.Awake), typeof(FogPatches), nameof(FogPatches.PlanetaryFogPostfix));
			hmy.AddPostfix<FogLight>(nameof(FogLight.Start), typeof(FogPatches), nameof(FogPatches.FogLightPostfix));
			hmy.AddPostfix<GlobalMusicController>(nameof(GlobalMusicController.Start), typeof(GlobalMusicControllerPatch), nameof(GlobalMusicControllerPatch.GlobalMusicControllerPostfix));
			hmy.AddPrefix<AnglerfishAudioController>(nameof(AnglerfishAudioController.UpdateLoopingAudio), typeof(AnglerfishAudioControllerPatch), nameof(AnglerfishAudioControllerPatch.UpdateLoopingAudioPatch));
			hmy.AddPostfix<FogLightManager>(nameof(FogLightManager.Awake), typeof(FogPatches), nameof(FogPatches.FogLightManagerAwakePostfix));
		}
	}

	public class AnglerPatch
	{
		static public void SectorUpdated(AnglerfishController __instance, ref bool __runOriginal)
		{
			__runOriginal = false;

			if (!LightBramble.inst._disableFish && !__instance.gameObject.activeSelf && __instance.GetSector().ContainsAnyOccupants(DynamicOccupant.Player | DynamicOccupant.Probe | DynamicOccupant.Ship))
			{
				__instance.gameObject.SetActive(true);
				__instance.GetAttachedOWRigidbody().Unsuspend(true);
				__instance.RaiseEvent("OnAnglerUnsuspended", __instance.GetAnglerState());
			}
			else if (__instance.gameObject.activeSelf && !__instance.GetSector().ContainsAnyOccupants(DynamicOccupant.Player | DynamicOccupant.Probe | DynamicOccupant.Ship))
			{
				__instance.GetAttachedOWRigidbody().Suspend();
				__instance.gameObject.SetActive(false);
				__instance.RaiseEvent("OnAnglerSuspended", __instance.GetAnglerState());
			}
		}
		static public void AwakePostfix(AnglerfishController __instance)
		{
			LightBramble.inst.anglerfishList.Add(__instance);
			__instance.OnAnglerSuspended += (anglerState) => LightBramble.inst.DebugLog("angler suspended event called");
		}
		static public void OnDestroyPrefix(AnglerfishController __instance)
		{
			LightBramble.inst.anglerfishList.Remove(__instance);
		}
	}

	public class FogPatches
	{
		static public void FogLightManagerAwakePostfix(FogLightManager __instance)
		{
			LightBramble.inst.fogLightCanvas = __instance.GetValue<Canvas>("_canvas");
		}

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
			List<FogLight> linkedFogLights = __instance.GetValue<List<FogLight>>("_linkedFogLights");
			List<FogLight.LightData> linkedLightData = __instance.GetValue<List<FogLight.LightData>>("_linkedLightData");
			for (int i = 0; i < linkedFogLights.Count; i++)
			{
				if (linkedFogLights[i].gameObject.name == "Lure_FogLight")
				{
					LightBramble.inst.lureLightDataList.Add(linkedLightData[i]);
					LightBramble.inst.DebugLog("adding lure foglight, maxAlpha is " + linkedLightData[i].maxAlpha);
				}
			}
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
			LightBramble.inst.globalMusicController = __instance;
			LightBramble.inst._brambleSource = __instance.GetValue<OWAudioSource>("_darkBrambleSource");
			LightBramble.inst.ModHelper.HarmonyHelper.EmptyMethod<GlobalMusicController>(nameof(GlobalMusicController.UpdateBrambleMusic));
		}
	}

	public class AnglerfishAudioControllerPatch
	{
		public static void UpdateLoopingAudioPatch(AnglerfishAudioController __instance, ref bool __runOriginal, AnglerfishController.AnglerState anglerState)
		{
			__runOriginal = false;
			LightBramble.inst.DebugLog(anglerState.ToString());

			OWAudioSource _loopSource = __instance.GetValue<OWAudioSource>("_loopSource");
			//LightBramble.inst.DebugLog("audioManager is " + (audioManager?.ToString() ?? "null"));
			if (Locator.GetAudioManager() is AudioManager audioManager && audioManager != null)
			{
				switch (anglerState)
				{
					case AnglerfishController.AnglerState.Lurking:
						_loopSource.AssignAudioLibraryClip(global::AudioType.DBAnglerfishLurking_LP);
						_loopSource.FadeIn(0.5f, true, false, 1f);
						return;
					case AnglerfishController.AnglerState.Chasing:
						_loopSource.AssignAudioLibraryClip(global::AudioType.DBAnglerfishChasing_LP);
						_loopSource.FadeIn(0.5f, true, false, 1f);
						return;
				}
				_loopSource.FadeOut(0.5f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
			}
		}
	}
}