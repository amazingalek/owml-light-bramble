using OWML.Utils;
using UnityEngine;
using System.Reflection;
using HarmonyLib;

namespace LightBramble
{
	public static class Patches
	{
		public static void ApplyPatches()
		{
			Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
			OWML.Common.IHarmonyHelper hmy = LightBramble.inst.ModHelper.HarmonyHelper; //for some reason the patch below doesn't work with Harmony attributes
			hmy.AddPrefix<AnglerfishController>(nameof(AnglerfishController.OnDestroy), typeof(AnglerPatches), nameof(AnglerPatches.OnDestroyPrefix));
		}
	}

	[HarmonyPatch]
	public static class AnglerPatches
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(AnglerfishController), nameof(AnglerfishController.OnSectorOccupantsUpdated))]
		public static void OnSectorOccupantsUpdated(AnglerfishController __instance, ref bool __runOriginal)
		{
			__runOriginal = false;

			LightBramble.inst.ToggleFogLights(!(LightBramble.inst._disableFish));
			//delay disabling to give time for UpdateFogLight to trigger
			LightBramble.inst.ModHelper.Events.Unity.FireInNUpdates(() =>
								LightBramble.inst.ToggleFishes(LightBramble.inst._disableFish), 2);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(AnglerfishController), nameof(AnglerfishController.Awake))]
		public static void AwakePostfix(AnglerfishController __instance)
		{
			LightBramble.inst.collections.anglerfishList.Add(__instance);
		}

		//[HarmonyPrefix]
		//[HarmonyPatch(typeof(AnglerfishController), nameof(AnglerfishController.OnDestroy))]
		public static void OnDestroyPrefix(AnglerfishController __instance)
		{
			LightBramble.inst.collections.anglerfishList.Remove(__instance);
		}
	}

	[HarmonyPatch]
	public static class FogPatches
	{
		[HarmonyPostfix]
		[HarmonyPatch(typeof(FogWarpVolume), nameof(FogWarpVolume.Awake))]
		public static void FogWarpVolumePostfix(FogWarpVolume __instance)
		{
			LightBramble.inst.collections.fogWarpVolumeDict.Add(__instance, __instance.GetValue<Color>("_fogColor"));
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(PlanetaryFogController), nameof(PlanetaryFogController.Awake))]
		public static void PlanetaryFogPostfix(PlanetaryFogController __instance)
		{
			LightBramble.inst.collections.planetaryFogControllerDict.Add(__instance, __instance.fogTint);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(FogOverrideVolume), nameof(FogOverrideVolume.Awake))]
		public static void FogOverrideVolumePostfix(FogOverrideVolume __instance)
		{
			LightBramble.inst.collections.fogOverrideVolumeDict.Add(__instance, __instance.tint);
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(FogLight), nameof(FogLight.Start))]
		public static void FogLightStartPostfix(FogLight __instance)
		{
			LightBramble.inst.collections.fogLights.Add(__instance);
		}
	}

	[HarmonyPatch]
	public static class GlobalMusicControllerPatch
	{
		[HarmonyPostfix]
		[HarmonyPatch(typeof(GlobalMusicController), nameof(GlobalMusicController.Start))]
		static public void GlobalMusicControllerStartPostfix(GlobalMusicController __instance)
		{
			LightBramble.inst.musicManager = new MusicManager(__instance);
		}
	}

	[HarmonyPatch]
	public static class AnglerfishAudioControllerPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch(typeof(AnglerfishAudioController), nameof(AnglerfishAudioController.UpdateLoopingAudio))]
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