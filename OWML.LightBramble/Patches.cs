﻿using OWML.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace OWML.LightBramble
{
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
					//linkedLightData[i].maxAlpha = 0f;
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