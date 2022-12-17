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

		internal MusicManager musicManager;

		public class CollectionHolder
		{
			public List<AnglerfishController> anglerfishList = new List<AnglerfishController>();
			public Dictionary<FogWarpVolume, Color> fogWarpVolumeDict = new Dictionary<FogWarpVolume, Color>();
			public Dictionary<PlanetaryFogController, Color> planetaryFogControllerDict = new Dictionary<PlanetaryFogController, Color>();
			public Dictionary<FogOverrideVolume, Color> fogOverrideVolumeDict = new Dictionary<FogOverrideVolume, Color>();
		}
		//used to easily dereference all collections instead of a bunch of OnDestroy patches to remove from them
		internal CollectionHolder collections = new CollectionHolder();

		private bool isInSolarSystem = false;   //updated on scene load
		private bool isInBramble = false;   //updated by a global event called by the game

		//Config toggles, modified by Configure when the user exits the OWML mod toggle page
		public bool _swapMusic => CurrentConfig.swapMusic;
		public bool _disableFish => CurrentConfig.disableFish;
		public bool _disableFog => CurrentConfig.disableFog;

		private MethodInfo anglerChangeState;

		//QSB-compatibility
		public event Action<BrambleConfig> ConfigChanged;

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
		public BrambleConfig CurrentConfig { get; private set; } = new BrambleConfig();

		private void Awake()
		{
			if (inst == null)
				inst = this;
			else
				Destroy(this);
		}

		private void Start()
		{
			Patches.ApplyPatches();

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
			SetConfig(new BrambleConfig {
				swapMusic = config?.GetSettingsValue<bool>("swapMusic") ?? true,
				disableFish = config?.GetSettingsValue<bool>("disableFish") ?? true,
				disableFog = config?.GetSettingsValue<bool>("disableFog") ?? true
			});
		}

		public void SetConfig(BrambleConfig newConfig)
		{
			CurrentConfig = newConfig;
			CheckToggleables();
			ConfigChanged?.Invoke(CurrentConfig);
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
				musicManager.SwapMusic(BrambleMusic.Original, 1f, 0f);
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
			if (!isInSolarSystem || !isInBramble)
				return;
			
			ModHelper.Events.Unity.FireInNUpdates(() => ToggleFishes(_disableFish), 2);

			if (_swapMusic)
				musicManager?.SwapMusic(BrambleMusic.Deku);
			else
				musicManager?.SwapMusic(BrambleMusic.Original);
	
			if (_disableFog)
				DisableFog();
			else
				EnableFog();
		}

		internal void ToggleFishes(bool shouldDisable)
		{
			DebugLog("Toggling fish");
			foreach (AnglerfishController anglerfishController in collections.anglerfishList)
			{
				ToggleFish(anglerfishController, shouldDisable);
			}
		}

		internal void ToggleFish(AnglerfishController anglerfishController, bool shouldDisable)
		{
			if (!isInSolarSystem || anglerfishController == null || !(anglerfishController.GetSector().ContainsAnyOccupants(DynamicOccupant.Player | DynamicOccupant.Probe | DynamicOccupant.Ship)))
				return;

			if (shouldDisable && anglerfishController.gameObject.activeSelf)
				DisableAnglerfish(anglerfishController);
			else if (!shouldDisable && !anglerfishController.gameObject.activeSelf)
				EnableAnglerfish(anglerfishController);
		}

		internal void EnableAnglerfish(AnglerfishController anglerfishController)
		{
			anglerfishController.gameObject.SetActive(true);
			anglerfishController.GetAttachedOWRigidbody()?.Unsuspend();
			anglerfishController.RaiseEvent("OnAnglerUnsuspended", anglerfishController.GetAnglerState());
		}

		internal void DisableAnglerfish(AnglerfishController anglerfishController)
		{
			//set anglerfish state to lurking so that the angler is not still following player when re-enabled
			anglerChangeState?.Invoke(anglerfishController, new object[] { AnglerfishController.AnglerState.Lurking });

			anglerfishController.GetAttachedOWRigidbody()?.Suspend();
			anglerfishController.gameObject.SetActive(false);
			anglerfishController.RaiseEvent("OnAnglerSuspended", anglerfishController.GetAnglerState());
		}

		internal void EnableFog()
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

		internal void DisableFog()
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

		internal void DebugLog(string str)
		{
#if DEBUG
			ModHelper.Console.WriteLine(str);
#endif
		}

		internal void DebugLog(string str, MessageType messageType)
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