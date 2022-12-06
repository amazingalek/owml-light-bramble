using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace LightBramble
{
	public class LightBramble : ModBehaviour
	{
		public static LightBramble inst;

		public MusicManager musicManager;

		public class CollectionHolder
		{
			public List<FogLight> lureLights = new List<FogLight>();
			public List<AnglerfishController> anglerfishList = new List<AnglerfishController>();
			public Dictionary<FogWarpVolume, Color> fogWarpVolumeDict = new Dictionary<FogWarpVolume, Color>();
			public Dictionary<PlanetaryFogController, Color> planetaryFogControllerDict = new Dictionary<PlanetaryFogController, Color>();
			public Dictionary<FogOverrideVolume, Color> fogOverrideVolumeDict = new Dictionary<FogOverrideVolume, Color>();
		}
		//can destroy the instance instead of a bunch of OnDestroy patches to remove from the lists
		public CollectionHolder collections = new CollectionHolder();

		private bool isInSolarSystem = false;   //updated on every scene load
		private bool isInBramble = false;   //updated by a global event called by the game

		//Config toggles, automatically modified from Configure when the user toggles them in the OWML menu
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
			//setup singleton
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

			//get handle to ChangeState so that we can set Anglerfish to idle before disabling
			Type anglerType = typeof(AnglerfishController);
			anglerChangeState = anglerType.GetMethod(nameof(AnglerfishController.ChangeState), BindingFlags.NonPublic | BindingFlags.Instance);
		}

		public override void Configure(IModConfig config)
		{
			currentConfig.swapMusic = config.GetSettingsValue<bool>("swapMusic");
			currentConfig.disableFish = config.GetSettingsValue<bool>("disableFish");
			currentConfig.disableFog = config.GetSettingsValue<bool>("disableFog");
			ConfigChanged?.Invoke(currentConfig);
			CheckToggleables();
		}

		private void OnCompleteSceneLoad(OWScene oldScene, OWScene newScene)
		{
			isInSolarSystem = (newScene == OWScene.SolarSystem);
			//this handles exiting to the menu from bramble
			if (!isInSolarSystem)
			{
				isInBramble = false;
				collections = new CollectionHolder();	//clear out the old data (gc will get it)
			}
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
			ToggleFishFogLights(_disableFish);
#if DEBUG
			//warp player to ship, then ship to Bramble.	WARNING: do not move while warping
			var shipBody = Locator.GetShipBody();
			Task.Delay(3000).ContinueWith(t => WarpShip(AstroObject.Name.DarkBramble, offset: new Vector3(1000, 0, 0)));
			Task.Delay(4800).ContinueWith(t => {
				Locator.GetPlayerBody().GetAttachedOWRigidbody().WarpToPositionRotation(shipBody.GetPosition(), shipBody.GetRotation());
				Locator.GetPlayerBody().SetVelocity(shipBody.GetVelocity());
			});
#endif
		}

		private void CheckToggleables()
		{
			ToggleFishFogLights(_disableFish);
			ToggleFishes(_disableFish);

			if (_swapMusic)
				musicManager?.SwapMusic(BrambleMusic.Deku);
			else
				musicManager?.SwapMusic(BrambleMusic.Spooky);

			if (isInSolarSystem && isInBramble)
			{
				if (_disableFog)
					DisableFog();
				else
					EnableFog();
			}
		}

		private void ToggleFishes(bool disabled)
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

		private void ToggleFishFogLights(bool disabled)
		{
			float newDistance = disabled ? 0f : float.PositiveInfinity;
			foreach (FogLight fogLight in collections.lureLights)
			{
				fogLight.SetValue("_maxVisibleDistance", newDistance);
				//foreach (FogLight linkedFogLight in fogLight.GetValue<List<FogLight>>("_linkedFogLights"))
				//	linkedFogLight.SetValue("_maxVisibleDistance", newDistance);
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
				//if (kvp.Key.GetValue<Renderer>("_fogImpostor").bounds.center 
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
			}
			foreach (KeyValuePair<PlanetaryFogController, Color> kvp in collections.planetaryFogControllerDict)
			{
				kvp.Key.fogTint = Color.clear;
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in collections.fogOverrideVolumeDict)
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

		public void DebugLog(string str, MessageType messageType)
		{
#if DEBUG
			ModHelper.Console.WriteLine(str, messageType);
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
	}
}