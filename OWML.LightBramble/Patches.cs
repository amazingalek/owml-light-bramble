using OWML.Utils;
using UnityEngine;

namespace LightBramble
{
	public static class Patches
	{
		public static void SetupPatches()
		{
			OWML.Common.IHarmonyHelper hmy = LightBramble.inst.ModHelper.HarmonyHelper;
			hmy.AddPrefix<AnglerfishController>(nameof(AnglerfishController.OnSectorOccupantsUpdated), typeof(AnglerPatches), nameof(AnglerPatches.SectorUpdated));
			hmy.AddPostfix<AnglerfishController>(nameof(AnglerfishController.Awake), typeof(AnglerPatches), nameof(AnglerPatches.AwakePostfix));
			hmy.AddPrefix<AnglerfishController>(nameof(AnglerfishController.OnDestroy), typeof(AnglerPatches), nameof(AnglerPatches.OnDestroyPrefix));
			hmy.AddPostfix<FogOverrideVolume>(nameof(FogOverrideVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogOverrideVolumePostfix));
			hmy.AddPostfix<FogWarpVolume>(nameof(FogWarpVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogWarpVolumePostfix));
			hmy.AddPostfix<PlanetaryFogController>(nameof(PlanetaryFogController.Awake), typeof(FogPatches), nameof(FogPatches.PlanetaryFogPostfix));
			hmy.AddPostfix<GlobalMusicController>(nameof(GlobalMusicController.Start), typeof(GlobalMusicControllerPatch), nameof(GlobalMusicControllerPatch.GlobalMusicControllerStartPostfix));
			hmy.AddPrefix<AnglerfishAudioController>(nameof(AnglerfishAudioController.UpdateLoopingAudio), typeof(AnglerfishAudioControllerPatch), nameof(AnglerfishAudioControllerPatch.UpdateLoopingAudioPatch));
			hmy.AddPostfix<FogLightManager>(nameof(FogLightManager.Awake), typeof(FogPatches), nameof(FogPatches.FogLightManagerAwakePostfix));
		}
	}

	public class AnglerPatches
	{
		static public void SectorUpdated(AnglerfishController __instance, ref bool __runOriginal)
		{
			__runOriginal = false;

			if (!LightBramble.inst._disableFish && !__instance.gameObject.activeSelf && __instance.GetSector().ContainsAnyOccupants(DynamicOccupant.Player | DynamicOccupant.Probe | DynamicOccupant.Ship))
			{
				LightBramble.inst.EnableAnglerfish(__instance);
			}
			else if (__instance.gameObject.activeSelf && !__instance.GetSector().ContainsAnyOccupants(DynamicOccupant.Player | DynamicOccupant.Probe | DynamicOccupant.Ship))
			{
				LightBramble.inst.DisableAnglerfish(__instance);
			}
		}

		static public void AwakePostfix(AnglerfishController __instance)
		{
			LightBramble.inst.collections.anglerfishList.Add(__instance);
		}
		static public void OnDestroyPrefix(AnglerfishController __instance)
		{
			LightBramble.inst.collections.anglerfishList.Remove(__instance);
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
			LightBramble.inst.collections.fogWarpVolumeDict.Add(__instance, __instance.GetValue<Color>("_fogColor"));
		}

		static public void PlanetaryFogPostfix(PlanetaryFogController __instance)
		{
			LightBramble.inst.collections.planetaryFogControllerDict.Add(__instance, __instance.fogTint);
		}

		static public void FogOverrideVolumePostfix(FogOverrideVolume __instance)
		{
			LightBramble.inst.collections.fogOverrideVolumeDict.Add(__instance, __instance.tint);
		}
	}

	public class GlobalMusicControllerPatch
	{
		static public void GlobalMusicControllerStartPostfix(GlobalMusicController __instance)
		{
			LightBramble.inst.musicManager = new MusicManager(__instance);
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