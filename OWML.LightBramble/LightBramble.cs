#define DEBUG

using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace LightBramble
{
	public class LightBramble : ModBehaviour
	{
		public static LightBramble inst;

		public MusicManager musicManager;
		public Canvas fogLightCanvas;
		public FogLightManager fogLightManager;

		public class CollectionHolder
		{
			public List<AnglerfishController> anglerfishList = new List<AnglerfishController>();
			public Dictionary<FogWarpVolume, Color> fogWarpVolumeDict = new Dictionary<FogWarpVolume, Color>();
			public Dictionary<PlanetaryFogController, Color> planetaryFogControllerDict = new Dictionary<PlanetaryFogController, Color>();
			public Dictionary<FogOverrideVolume, Color> fogOverrideVolumeDict = new Dictionary<FogOverrideVolume, Color>();
		}
		//used to easily dereference all collections instead of a bunch of OnDestroy patches to remove from them
		public CollectionHolder collections = new CollectionHolder();

		private bool isInSolarSystem = false;   //updated on scene load
		private bool isInBramble = false;   //updated by a global event called by the game

		//Config toggles, modified by Configure when the user exits the OWML mod toggle page
		public bool _swapMusic => currentConfig.swapMusic;
		public bool _disableFish => currentConfig.disableFish;
		public bool _disableFog => currentConfig.disableFog;

		private MethodInfo anglerChangeState;

		//QSB-compatibility
		public Action<BrambleConfig> ConfigChanged;

		public struct BrambleConfig
		{
			public bool swapMusic;
			public bool disableFish;
			public bool disableFog;

			public BrambleConfig(bool __swapMusic = true, bool __disableFish = true, bool __disableFog = true)
			{
				swapMusic = __swapMusic;
				disableFish = __disableFish;
				disableFog = __disableFog;
			}
		}
		public BrambleConfig currentConfig = new BrambleConfig();

		private void Awake()
		{
			if (inst == null)
				inst = this;
			else
				Destroy(this);
		}

		private void Start()
		{
			Patches.SetupPatches();

			GlobalMessenger.AddListener("PlayerEnterBrambleDimension", PlayerEnterBramble);
			GlobalMessenger.AddListener("PlayerExitBrambleDimension", PlayerExitBramble);
			GlobalMessenger.AddListener("WakeUp", OnWakeUp);
			LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
			LoadManager.OnStartSceneLoad += OnStartSceneLoad;

			//get handle to ChangeState so that we can set Anglerfish to idle before disabling
			Type anglerType = typeof(AnglerfishController);
			anglerChangeState = anglerType.GetMethod(nameof(AnglerfishController.ChangeState), BindingFlags.NonPublic | BindingFlags.Instance);
		}

		public override void Configure(IModConfig config)
		{
			DebugLog("Configure called");
			currentConfig.swapMusic = config.GetSettingsValue<bool>("swapMusic");
			currentConfig.disableFish = config.GetSettingsValue<bool>("disableFish");
			currentConfig.disableFog = config.GetSettingsValue<bool>("disableFog");
			ConfigChanged?.Invoke(currentConfig);
			CheckToggleables();
		}

		//clear collections and let the gc get the old lists/dicts
		private void OnStartSceneLoad(OWScene oldScene, OWScene newScene) => collections = new CollectionHolder();

		private void OnCompleteSceneLoad(OWScene oldScene, OWScene newScene)
		{
			isInSolarSystem = (newScene == OWScene.SolarSystem);
			if (!isInSolarSystem)   //this is necessary because the game does not call PlayerExitBrambleDimension when exiting to menu
				PlayerExitBramble();
		}

		private void PlayerEnterBramble()
		{
			isInBramble = true;

			if (_disableFog)
				DisableFog();
			if (_swapMusic)
				musicManager.SwapMusic(BrambleMusic.Deku, 1f, 0f);
			else
				musicManager.SwapMusic(BrambleMusic.Spooky, 1f, 0f);
		}

		private void PlayerExitBramble()
		{
			isInBramble = false;
			EnableFog();
		}

		private void OnWakeUp()
		{
			CheckToggleables();
#if DEBUG
			//warp ship to Bramble, then player to ship
			var shipBody = Locator.GetShipBody();
			System.Threading.Tasks.Task.Delay(1000).ContinueWith(t => WarpShip(AstroObject.Name.DarkBramble, offset: new Vector3(1000, 0, 0)));
			System.Threading.Tasks.Task.Delay(2800).ContinueWith(t => {
				Locator.GetPlayerBody().GetAttachedOWRigidbody().WarpToPositionRotation(shipBody.GetPosition(), shipBody.GetRotation());
				Locator.GetPlayerBody().SetVelocity(shipBody.GetVelocity());
			});
#endif
		}

		internal void CheckToggleables()
		{
			//need to toggle this regardless of whether we're in bramble, because the lights are visible from outside the planet
			ModHelper.Events.Unity.FireInNUpdates(() => ToggleFishes(_disableFish), 2);

			if (!isInSolarSystem || !isInBramble)
				return;

			if (_swapMusic)
				musicManager?.SwapMusic(BrambleMusic.Deku);
			else
				musicManager?.SwapMusic(BrambleMusic.Spooky);
	
			if (_disableFog)
				DisableFog();
			else
				EnableFog();
		}

		public void ToggleFishes(bool disabled)
		{
			DebugLog("Toggling fish");
			foreach (AnglerfishController anglerfishController in collections.anglerfishList)
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

				DisableAnglerfish(anglerfishController);
			}
			else if (!disabled && !anglerfishController.gameObject.activeSelf)
			{
				EnableAnglerfish(anglerfishController);
			}
		}

		public void EnableAnglerfish(AnglerfishController anglerfishController)
		{
			anglerfishController.gameObject.SetActive(true);
			anglerfishController.GetAttachedOWRigidbody()?.Unsuspend();
			anglerfishController.RaiseEvent("OnAnglerUnsuspended", anglerfishController.GetAnglerState());
		}

		public void DisableAnglerfish(AnglerfishController anglerfishController)
		{
			anglerfishController.GetAttachedOWRigidbody()?.Suspend();
			anglerfishController.gameObject.SetActive(false);
			anglerfishController.RaiseEvent("OnAnglerSuspended", anglerfishController.GetAnglerState());
		}

		private void EnableFog()
		{
			DebugLog("Enabling Fog");
			foreach (KeyValuePair<FogWarpVolume, Color> kvp in collections.fogWarpVolumeDict)
			{
				kvp.Key.SetValue("_fogColor", kvp.Value);
			}
			foreach (KeyValuePair<PlanetaryFogController, Color> kvp in collections.planetaryFogControllerDict)
			{
				kvp.Key.fogTint = kvp.Value;
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in collections.fogOverrideVolumeDict)
			{
				kvp.Key.tint = kvp.Value;
			}
		}

		private void DisableFog()
		{
			DebugLog("Disabling Fog");
			foreach (KeyValuePair<FogWarpVolume, Color> kvp in collections.fogWarpVolumeDict)
			{
				kvp.Key.SetValue("_fogColor", Color.clear);
				DebugLog("disabling fogWarpVolume");
			}
			foreach (KeyValuePair<PlanetaryFogController, Color> kvp in collections.planetaryFogControllerDict)
			{
				kvp.Key.fogTint = Color.clear;
				DebugLog("disabling planetaryfogcontroller");
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in collections.fogOverrideVolumeDict)
			{
				kvp.Key.tint = Color.clear;
				DebugLog("disabling fogoverridevolume");
			}
		}

		public void DebugLog(string str)
		{
#if DEBUG
			ModHelper.Console.WriteLine(str);
#endif
		}

		public void DebugLog(string str, MessageType messageType)
		{
#if DEBUG
			ModHelper.Console.WriteLine(str, messageType);
#endif
		}

#if DEBUG
		/// <summary>
		/// Warps to an AstroObject at a specified offset and matches the ship's velocity to that of the AstroObject
		/// </summary>
		private void WarpShip(AstroObject.Name astroObjectName, Vector3 offset)
		{
			var astroObject = Locator.GetAstroObject(astroObjectName);
			Locator.GetShipBody().WarpToPositionRotation(astroObject.transform.position + offset, astroObject.transform.rotation);
			Locator.GetShipBody().SetVelocity(astroObject.GetOWRigidbody().GetVelocity());
		}
#endif
	}
}