#define DEBUG

using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Threading.Tasks;

namespace OWML.LightBramble
{
	public class LightBramble : ModBehaviour
	{
		public GameObject musicManager;
		public GlobalMusicController globalMusicController;
		public AudioSource _dekuSource;
		public OWAudioSource _brambleSource;
		public OWAudioSource dekuOWAudioSource;

		public List<FogLight.LightData> lureLightDataList = new List<FogLight.LightData>();
		public List<AnglerfishController> anglerfishList = new List<AnglerfishController>();
		public Dictionary<FogWarpVolume, Color> fogWarpVolumeDict = new Dictionary<FogWarpVolume, Color>();
		public Dictionary<PlanetaryFogController, Color> planetaryFogControllerDict = new Dictionary<PlanetaryFogController, Color>();
		public Dictionary<FogOverrideVolume, Color> fogOverrideVolumeDict = new Dictionary<FogOverrideVolume, Color>();

		public bool isInSolarSystem { get; private set; } = false;   //updated on every scene load
		private bool isInBramble = false;   //updated by a global event called by the game

		//Config toggles, automatically modified from Configure when the user toggles them in the OWML menu
		private bool _swapMusic = true;
		public bool _disableFish = true;
		public bool _disableFog = true;
		 
		MethodInfo anglerChangeState;

		public static LightBramble inst;

		private void Awake()
		{
			//setup singleton
			if (inst == null)
				inst = this;
			else
				Destroy(this.gameObject);
		}

		private void Start()
		{
			ModHelper.Console.WriteLine($"Start of {nameof(LightBramble)}");

			ModHelper.HarmonyHelper.AddPrefix<AnglerfishController>(nameof(AnglerfishController.OnSectorOccupantsUpdated), typeof(AnglerPatch), nameof(AnglerPatch.SectorUpdated));
			ModHelper.HarmonyHelper.AddPostfix<AnglerfishController>(nameof(AnglerfishController.Awake), typeof(AnglerPatch), nameof(AnglerPatch.AwakePostfix));
			ModHelper.HarmonyHelper.AddPrefix<AnglerfishController>(nameof(AnglerfishController.OnDestroy), typeof(AnglerPatch), nameof(AnglerPatch.OnDestroyPrefix));
			ModHelper.HarmonyHelper.AddPostfix<FogOverrideVolume>(nameof(FogOverrideVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogOverrideVolumePostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogWarpVolume>(nameof(FogWarpVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogWarpVolumePostfix));
			ModHelper.HarmonyHelper.AddPostfix<PlanetaryFogController>(nameof(PlanetaryFogController.Awake), typeof(FogPatches), nameof(FogPatches.PlanetaryFogPostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogLight>(nameof(FogLight.Start), typeof(FogPatches), nameof(FogPatches.FogLightPostfix));
			ModHelper.HarmonyHelper.AddPostfix<GlobalMusicController>(nameof(GlobalMusicController.Start), typeof(GlobalMusicControllerPatch), nameof(GlobalMusicControllerPatch.GlobalMusicControllerPostfix));
			ModHelper.HarmonyHelper.AddPrefix<AnglerfishAudioController>(nameof(AnglerfishAudioController.UpdateLoopingAudio), typeof(AnglerfishAudioControllerPatch), nameof(AnglerfishAudioControllerPatch.UpdateLoopingAudioPatch));

			GlobalMessenger.AddListener("PlayerEnterBrambleDimension", PlayerEnterBramble);
			GlobalMessenger.AddListener("PlayerExitBrambleDimension", PlayerExitBramble);
			GlobalMessenger.AddListener("WakeUp", OnWakeUp);
			LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;

			//get handle to ChangeState so that we can set Anglerfish to idle before disabling
			Type anglerType = typeof(AnglerfishController);
			anglerChangeState = anglerType.GetMethod(nameof(AnglerfishController.ChangeState), BindingFlags.NonPublic | BindingFlags.Instance);
		}

		public override void Configure(IModConfig config)
		{
			_swapMusic = config.GetSettingsValue<bool>("swapMusic");
			_disableFish = config.GetSettingsValue<bool>("disableFish");
			_disableFog = config.GetSettingsValue<bool>("disableFog");
			CheckToggleables();
		}

		private void OnCompleteSceneLoad(OWScene oldScene, OWScene newScene)
		{
			isInSolarSystem = (newScene == OWScene.SolarSystem);
			//this handles exiting to the menu from bramble
			if (!isInSolarSystem)
			{
				isInBramble = false;
				if (_brambleSource != null && _brambleSource.isPlaying)
					_brambleSource.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
				else if (dekuOWAudioSource != null && dekuOWAudioSource.isPlaying)
					dekuOWAudioSource.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
			}
		}

		private void PlayerEnterBramble()
		{
			isInBramble = true;
			if (_disableFog)
			{
				DisableFog();
			}
			if (_swapMusic)
			{
				if (_brambleSource != null && _brambleSource.isPlaying)
					_brambleSource.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
				if (dekuOWAudioSource != null && !dekuOWAudioSource.isPlaying)
					dekuOWAudioSource.FadeIn(1f, false, false, 1f);
			}
		}

		private void PlayerExitBramble()
		{
			DebugLog("Player exited bramble");
			isInBramble = false;
			EnableFog();
			//fade out of whichever source is playing
			if (_brambleSource != null && _brambleSource.isPlaying)
				_brambleSource.FadeOut(3f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
			else if (dekuOWAudioSource != null && dekuOWAudioSource.isPlaying)
				dekuOWAudioSource.FadeOut(3f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
		}

		private void OnWakeUp()
		{
			SetupAudio();
			ToggleFishFogLights(_disableFish);
#if DEBUG
			//warp player to ship, then ship to Bramble
			var shipBody = Locator.GetShipBody();
			Task.Delay(200).ContinueWith(t => Locator.GetPlayerBody().GetAttachedOWRigidbody().WarpToPositionRotation(shipBody.GetPosition(), shipBody.GetRotation()));
			Task.Delay(2000).ContinueWith(t => WarpShip(AstroObject.Name.DarkBramble, offset: new Vector3(1000, 0, 0)));
#endif
		}

		/// <summary>
		/// Warps to an AstroObject at a specified offset and matches the ship's velocity to that of the AstroObject
		/// </summary>
		private void WarpShip(AstroObject.Name astroObjectName, Vector3 offset)
		{
			var astroObject = Locator.GetAstroObject(astroObjectName);
			Locator.GetShipBody().WarpToPositionRotation(astroObject.transform.position + offset, astroObject.transform.rotation);
			Locator.GetShipBody().SetVelocity(astroObject.GetOWRigidbody().GetVelocity());
		}

		private void SetupAudio()
		{
			//load audio, add it to an AudioSource component, then set up an OWAudioSource on musicManager
			DebugLog("SetupAudio called");
			if (musicManager != null)
				Destroy(musicManager);
			musicManager = new GameObject("LightBramble_MusicManager");
			musicManager.transform.SetParent(this.gameObject.transform);
			musicManager.SetActive(false);
			_dekuSource = musicManager.AddComponent<AudioSource>();
			_dekuSource.clip = ModHelper.Assets.GetAudio("deku-tree.mp3");

			dekuOWAudioSource = musicManager.AddComponent<OWAudioSource>();
			dekuOWAudioSource.SetValue("_audioSource", _dekuSource);
			dekuOWAudioSource.SetTrack(OWAudioMixer.TrackName.Music);
			musicManager.SetActive(true);

			//I don't know why this delay is necessary, but it is
			Task.Delay(500).ContinueWith(t => dekuOWAudioSource.Stop());
		}

		private void CheckToggleables()
		{
			ToggleFishFogLights(_disableFish);

			if (isInSolarSystem && isInBramble)
			{
				DebugLog("in solar system and bramble checktoggleables");
				if (_swapMusic)
				{
					if (_brambleSource != null && _brambleSource.isPlaying)
						_brambleSource.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
					if (dekuOWAudioSource != null && !dekuOWAudioSource.isPlaying)
						dekuOWAudioSource.FadeIn(1f, false, false, 1f);
				}
				else
				{
					if (dekuOWAudioSource != null && dekuOWAudioSource.isPlaying)
						dekuOWAudioSource.FadeOut(1f, OWAudioSource.FadeOutCompleteAction.STOP, 0f);
					if (_brambleSource != null && !_brambleSource.isPlaying)
						_brambleSource.FadeIn(1f, false, false, 1f);
				}

				if (_disableFog)
				{
					DisableFog();
				}
				else if (!_disableFog)
				{
					EnableFog();
				}

				ToggleFishes(_disableFish);
			}
		}

		private void ToggleFishes(bool disabled)
		{
			DebugLog("Disabling multiple fish? : " + disabled.ToString());
			foreach (AnglerfishController anglerfishController in anglerfishList)
			{
				ToggleFish(anglerfishController, disabled);
			}
		}

		public void ToggleFish(AnglerfishController anglerfishController, bool disabled)
		{
			if (!isInSolarSystem || anglerfishController == null || !(anglerfishController.GetSector().ContainsAnyOccupants(DynamicOccupant.Player | DynamicOccupant.Probe | DynamicOccupant.Ship)))
				return;

			if (disabled && anglerfishController.gameObject.activeSelf)
			{
				//set anglerfish state to lurking so that the angler is not still following player when re-enabled
				anglerChangeState?.Invoke(anglerfishController, new object[] { AnglerfishController.AnglerState.Lurking });

				anglerfishController.GetAttachedOWRigidbody()?.Suspend();
				anglerfishController.gameObject.SetActive(false);
				anglerfishController.RaiseEvent("OnAnglerSuspended", anglerfishController.GetAnglerState());
			}
			else if (!disabled && !anglerfishController.gameObject.activeSelf)
			{
				anglerfishController.gameObject.SetActive(true);
				anglerfishController.GetAttachedOWRigidbody()?.Unsuspend();
				anglerfishController.RaiseEvent("OnAnglerUnsuspended", anglerfishController.GetAnglerState());
			}
			DebugLog("toggled a fish");
		}

		private void ToggleFishFogLights(bool disabled)
		{
			//TODO: turn lureLightData into a dict and use it to set maxAlpha to its original value
			float newAlpha = disabled ? 0f : 0.5f;
			foreach (FogLight.LightData lightData in lureLightDataList)
			{
				lightData.maxAlpha = newAlpha;
			}
		}

		private void EnableFog()
		{
			DebugLog("Enabling Fog");
			foreach (KeyValuePair<FogWarpVolume, Color> kvp in fogWarpVolumeDict)
			{
				kvp.Key.SetValue("_fogColor", kvp.Value);
			}
			foreach (KeyValuePair<PlanetaryFogController, Color> kvp in planetaryFogControllerDict)
			{
				kvp.Key.fogTint = kvp.Value;
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in fogOverrideVolumeDict)
			{
				kvp.Key.tint = kvp.Value;
			}
		}

		private void DisableFog()
		{
			DebugLog("Disabling Fog");
			foreach (KeyValuePair<FogWarpVolume, Color> kvp in fogWarpVolumeDict)
			{
				kvp.Key.SetValue("_fogColor", Color.clear);
			}
			foreach (KeyValuePair<PlanetaryFogController, Color> kvp in planetaryFogControllerDict)
			{
				kvp.Key.fogTint = Color.clear;
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in fogOverrideVolumeDict)
			{
				kvp.Key.tint = Color.clear;
			}
		}

		public void DebugLog(string str)
		{
#if DEBUG
			ModHelper.Console.WriteLine(str);
#endif
		}

		private void DebugLog(string str, MessageType messageType)
		{
#if DEBUG
			ModHelper.Console.WriteLine(str, messageType);
#endif
		}
	}
}