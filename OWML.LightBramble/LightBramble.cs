using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using UnityEngine;

namespace OWML.LightBramble
{
	public class LightBramble : ModBehaviour
	{
		private AudioSource _dekuSource;
		private OWAudioSource _brambleSource;
		private bool _isInSolarSystem;

		private bool _swapMusic = true;
		private bool _disableFish = true;
		private bool _disableFog = true;

		private void Start()
		{
			ModHelper.Logger.Log($"Start of {nameof(LightBramble)}");

			ModHelper.Events.Subscribe<AnglerfishController>(Events.AfterEnable);
			ModHelper.Events.Subscribe<FogLight>(Events.AfterStart);
			ModHelper.Events.Subscribe<FogWarpVolume>(Events.AfterAwake);
			ModHelper.Events.Subscribe<PlanetaryFogController>(Events.AfterEnable);
			ModHelper.Events.Subscribe<FogOverrideVolume>(Events.AfterAwake);
			ModHelper.Events.Subscribe<GlobalMusicController>(Events.AfterStart);
			ModHelper.Events.Event += OnEvent;

			_dekuSource = gameObject.AddComponent<AudioSource>();
			_dekuSource.clip = ModHelper.Assets.GetAudio("deku-tree.mp3");

			LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
		}

		public override void Configure(IModConfig config)
		{
			_swapMusic = config.GetSettingsValue<bool>("swapMusic");
			_disableFish = config.GetSettingsValue<bool>("disableFish");
			_disableFog = config.GetSettingsValue<bool>("disableFog");
		}

		private void OnCompleteSceneLoad(OWScene oldScene, OWScene newScene)
		{
			_isInSolarSystem = newScene == OWScene.SolarSystem;
		}

		private void OnEvent(MonoBehaviour behaviour, Events ev)
		{
			switch (behaviour)
			{
				case AnglerfishController anglerfishController
					when ev == Events.AfterEnable && _disableFish:
					ModHelper.Logger.Log("Deactivating anglerfish");
					anglerfishController.gameObject.SetActive(false);
					break;
				case FogLight fogLight
					when ev == Events.AfterStart && _disableFish:
					ModHelper.Logger.Log("Clearing _tint in FogOverrideVolume");
					ModHelper.HarmonyHelper.EmptyMethod<FogLight>("UpdateFogLight");
					break;
				case FogWarpVolume fogWarpVolume
					when ev == Events.AfterAwake && _disableFog:
					ModHelper.Logger.Log("Clearing _fogColor in FogWarpVolume");
					fogWarpVolume.SetValue("_fogColor", Color.clear);
					break;
				case PlanetaryFogController planetaryFogController
					when ev == Events.AfterEnable && _disableFog:
					ModHelper.Logger.Log("Clearing _fogTint in PlanetaryFogController");
					planetaryFogController.SetValue("_fogTint", Color.clear);
					break;
				case FogOverrideVolume fogOverrideVolume
					when ev == Events.AfterAwake && _disableFog:
					ModHelper.Logger.Log("Clearing _tint in FogOverrideVolume");
					fogOverrideVolume.SetValue("_tint", Color.clear);
					break;
				case GlobalMusicController globalMusicController
					when ev == Events.AfterStart:
					ModHelper.Logger.Log("Swapping _darkBrambleSource in GlobalMusicController");
					_brambleSource = behaviour.GetValue<OWAudioSource>("_darkBrambleSource");
					ModHelper.HarmonyHelper.EmptyMethod<GlobalMusicController>("UpdateBrambleMusic");
					break;
			}
		}

		private void Update()
		{
			var isInBramble = _isInSolarSystem &&
							  Locator.GetPlayerSectorDetector().InBrambleDimension() &&
							  !Locator.GetPlayerSectorDetector().InVesselDimension() &&
							  PlayerState.AtFlightConsole() &&
							  !PlayerState.IsHullBreached();
			if (isInBramble)
			{
				if (_swapMusic)
				{
					PlayDekuMusic();
					StopBrambleMusic();
				}
				else
				{
					PlayBrambleMusic();
					StopDekuMusic();
				}
			}
			else
			{
				StopDekuMusic();
				StopBrambleMusic();
			}
		}

		private void PlayDekuMusic()
		{
			if (_dekuSource != null && !_dekuSource.isPlaying)
			{
				_dekuSource.Play();
			}
		}

		private void PlayBrambleMusic()
		{
			if (_brambleSource != null && !_brambleSource.isPlaying)
			{
				_brambleSource.Play();
			}
		}

		private void StopDekuMusic()
		{
			if (_dekuSource != null && _dekuSource.isPlaying)
			{
				_dekuSource.Stop();
			}
		}

		private void StopBrambleMusic()
		{
			if (_brambleSource != null && _brambleSource.isPlaying)
			{
				_brambleSource.Stop();
			}
		}
	}
}