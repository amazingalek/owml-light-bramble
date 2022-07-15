#define DEBUG

using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;

namespace OWML.LightBramble
{
	public class LightBramble : ModBehaviour
	{
		public AudioSource _dekuSource;
		public OWAudioSource _brambleSource;
		public OWAudioSource dekuAudioSource;

		public List<AnglerfishController> anglerfishList = new List<AnglerfishController>();
		public List<FogLightData> fogLightDataList = new List<FogLightData>();
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

			ModHelper.HarmonyHelper.AddPostfix<AnglerfishController>(nameof(AnglerfishController.OnSectorOccupantsUpdated), typeof(AnglerPatch), nameof(AnglerPatch.SectorUpdated));
			ModHelper.HarmonyHelper.AddPostfix<AnglerfishController>(nameof(AnglerfishController.Awake), typeof(AnglerPatch), nameof(AnglerPatch.AwakePostfix));
			ModHelper.HarmonyHelper.AddPostfix<AnglerfishController>(nameof(AnglerfishController.OnDestroy), typeof(AnglerPatch), nameof(AnglerPatch.OnDestroyPostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogOverrideVolume>(nameof(FogOverrideVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogOverrideVolumePostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogWarpVolume>(nameof(FogWarpVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogWarpVolumePostfix));
			ModHelper.HarmonyHelper.AddPostfix<PlanetaryFogController>(nameof(PlanetaryFogController.Awake), typeof(FogPatches), nameof(FogPatches.PlanetaryFogPostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogLight>(nameof(FogLight.Awake), typeof(FogPatches), nameof(FogPatches.FogLightPostfix));
			ModHelper.HarmonyHelper.AddPostfix<GlobalMusicController>(nameof(GlobalMusicController.Start), typeof(GlobalMusicControllerPatch), nameof(GlobalMusicControllerPatch.GlobalMusicControllerPostfix));

			GlobalMessenger.AddListener("PlayerEnterBrambleDimension", PlayerEnterBramble);
			GlobalMessenger.AddListener("PlayerExitBrambleDimension", PlayerExitBramble);

			LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
			//LoadManager.OnStartSceneLoad += OnStartSceneLoad;

			//get handle to ChangeState so that we can set Anglerfish to idle before disabling
			Type anglerType = typeof(AnglerfishController);
			anglerChangeState = anglerType.GetMethod(nameof(AnglerfishController.ChangeState), BindingFlags.NonPublic | BindingFlags.Instance);

#if DEBUG
			GlobalMessenger.AddListener("SuitUp", new Callback(OnSuitUp));
#endif
		}

		private void PrintPlayerPosition()
		{
			if (!isInSolarSystem)
				return;

			ModHelper.Console.WriteLine("Ship rb position: " + Locator.GetShipBody().GetAttachedOWRigidbody().GetPosition());

			var bramblePb = Locator.GetAstroObject(AstroObject.Name.DarkBramble).GetPrimaryBody();
			ModHelper.Console.WriteLine("bramblepb = " + bramblePb.GetAstroObjectName());
			ModHelper.Console.WriteLine("Bramble position pb: " + bramblePb.GetAttachedOWRigidbody().GetPosition());
			ModHelper.Console.WriteLine("Bramble position: " + Locator.GetAstroObject(AstroObject.Name.DarkBramble).GetAttachedOWRigidbody().GetPosition());

		}

		public override void Configure(IModConfig config)
		{
			_swapMusic = config.GetSettingsValue<bool>("swapMusic");
			_disableFish = config.GetSettingsValue<bool>("disableFish");
			_disableFog = config.GetSettingsValue<bool>("disableFog");
			CheckToggleables();
		}

		private void PlayerEnterBramble()
		{
			isInBramble = true;
			CheckToggleables();
		}

		private void PlayerExitBramble()
		{
			isInBramble = false;
			EnableFog();
		}

		private void OnCompleteSceneLoad(OWScene oldScene, OWScene newScene)
		{
			isInSolarSystem = (newScene == OWScene.SolarSystem);
			if (isInSolarSystem)
			{
				AstroObject darkbramble = Locator.GetAstroObject(AstroObject.Name.DarkBramble);
				if (darkbramble == null)
				{
					ModHelper.Console.WriteLine("darkBramble null, wtf>>!", MessageType.Error);
					return;
				}

				ModHelper.Console.WriteLine("before pfc = " + planetaryFogControllerDict.Count);

				//filter to fog that is inside Dark Bramble
				//planetaryFogControllerDict = planetaryFogControllerDict.Where(kvp => darkbramble.IsInsideSphere(kvp.Key.transform.position, kvp.Key.fogRadius + 3000)).ToDictionary(i => i.Key, i => i.Value);
				//planetaryFogControllerDict = planetaryFogControllerDict.Where(kvp => kvp.Key.IsInsideSphere(darkbramble.GetPrimaryBody().GetAttachedOWRigidbody().GetPosition(), 3000)).ToDictionary(i => i.Key, i => i.Value);

				ModHelper.Console.WriteLine("after pfc = " + planetaryFogControllerDict.Count);


				ModHelper.Console.WriteLine("before foglight= " + fogLightDataList.Count);

				fogLightDataList.RemoveAll(fogLightData => !(fogLightData.fogLight.IsInBrambleSector()));

				ModHelper.Console.WriteLine("after foglight = " + fogLightDataList.Count);


				ModHelper.Console.WriteLine("before fogoverridevolume= " + fogOverrideVolumeDict.Count);
				#region my failed attempts to filter FogOverrideVolume
				//fogOverrideVolumeDict = fogOverrideVolumeDict.Where(kvp => kvp.Key.IsInBrambleSector()).ToDictionary(i => i.Key, i => i.Value);
				//fogOverrideVolumeDict = fogOverrideVolumeDict.Where(kvp => kvp.Key.IsInsideSphere(darkbramble.gameObject.transform.position, 60000)).ToDictionary(i => i.Key, i => i.Value);
				//fogOverrideVolumeDict = fogOverrideVolumeDict.Where(kvp => darkbramble.IsInsideSphere(kvp.Key.transform.position, kvp.Key.radius + 3000)).ToDictionary(i => i.Key, i => i.Value);

				//var filteredFogOVDict = new Dictionary<FogOverrideVolume, Color>();
				//foreach (Collider collider in darkbramble.GetComponentsInChildren<Collider>())
				//{
				//	ModHelper.Console.WriteLine("dark bramble collider = " + collider);
				//	foreach (KeyValuePair<FogOverrideVolume, Color> kvp in fogOverrideVolumeDict)
				//	{
				//		if (!filteredFogOVDict.ContainsKey(kvp.Key) && collider.bounds.Contains(kvp.Key.transform.position))
				//		{
				//			filteredFogOVDict.Add(kvp.Key, kvp.Value);
				//		}
				//	}
				//}
				//fogOverrideVolumeDict = filteredFogOVDict;

				//fogOverrideVolumeDict =
				//(from collider in darkbramble.GetComponentsInChildren<Collider>()
				// from kvp in fogOverrideVolumeDict
				// where collider.bounds.Contains(kvp.Key.transform.position)
				// select kvp).ToDictionary(i => i.Key, i => i.Value);
				#endregion
				ModHelper.Console.WriteLine("after fogoverridevolume= " + fogOverrideVolumeDict.Count);


				ModHelper.Console.WriteLine("before fogWarpVolumeDict= " + fogWarpVolumeDict.Count);

				fogWarpVolumeDict = fogWarpVolumeDict.Where(kvp => kvp.Key.IsInBrambleSector()).ToDictionary(i => i.Key, i => i.Value);

				ModHelper.Console.WriteLine("after fogWarpVolumeDict= " + fogWarpVolumeDict.Count);

				CheckToggleables();

#if DEBUG
				Invoke(nameof(TPToShip), 3);
				InvokeRepeating(nameof(PrintPlayerPosition), 4, 2);
#endif
			}
			else
			{
				isInBramble = false;
			}
		}

		private void TPToShip()
		{
			var shipBody = Locator.GetShipBody();
			Locator.GetPlayerBody().GetAttachedOWRigidbody().WarpToPositionRotation(shipBody.GetPosition(), shipBody.GetRotation());
		}

		private void OnSuitUp()
		{
			WarpShip(AstroObject.Name.DarkBramble, offset: new Vector3(1000, 0, 0));
		}

		/// <summary>
		/// Warps to an AstroObject at a specified offset and matches the ship's velocity to that of the AstroObject
		/// </summary>
		private void WarpShip(AstroObject.Name astroObjectName, Vector3 offset)
		{
			var astroObject = Locator.GetAstroObject(astroObjectName);
			Locator.GetShipBody().WarpToPositionRotation(astroObject.transform.position + offset, astroObject.transform.rotation);
			Locator.GetShipBody().SetVelocity(astroObject.GetPrimaryBody().GetOWRigidbody().GetVelocity());
		}

		private void CheckToggleables()
		{
			if (isInSolarSystem && isInBramble)
			{
				if (_swapMusic)
				{
					//PlayDekuMusic();
				}
				else
				{
					//PlayBrambleMusic();
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
			ModHelper.Console.WriteLine("Disabling multiple fish? : " + disabled.ToString());
			foreach (AnglerfishController anglerfishController in anglerfishList)
			{
				ToggleFish(anglerfishController, disabled);
			}
		}

		public void ToggleFish(AnglerfishController anglerfishController, bool disabled)
		{
			if (!isInSolarSystem)
				return;

			if (disabled)
			{
				//set anglerfish state to lurking so that the angler is not still following player when re-enabled
				anglerChangeState.Invoke(anglerfishController, new object[] { AnglerfishController.AnglerState.Lurking });
				//anglerfishController.GetAttachedOWRigidbody().SetVelocity(Locator.GetAstroObject(AstroObject.Name.DarkBramble).GetPrimaryBody().GetAttachedOWRigidbody().GetVelocity());
				anglerfishController.GetAttachedOWRigidbody().Suspend();
				anglerfishController.gameObject.SetActive(false);
			}
			else
			{
				anglerfishController.gameObject.SetActive(true);
				anglerfishController.GetAttachedOWRigidbody().Unsuspend();
			}
			ModHelper.Console.WriteLine("toggled a fish");
		}

		private void EnableFog()
		{
			ModHelper.Console.WriteLine("Enabling Fog");
			foreach (FogLightData fogLightData in fogLightDataList)
			{
				fogLightData.alpha = fogLightData.originalAlpha;

				//ModHelper.Console.WriteLine($"Enabling FogLight");
			}
			foreach (KeyValuePair<FogWarpVolume, Color> kvp in fogWarpVolumeDict)
			{
				kvp.Key.SetValue("_fogColor", kvp.Value);

				var fogSector = kvp.Key.GetValue<Sector>("_sector");
				if (fogSector == null) { }
				//else { ModHelper.Console.WriteLine($"FogWarpVolume is in {fogSector.ToString()}"); }
			}
			foreach (KeyValuePair<PlanetaryFogController, Color> kvp in planetaryFogControllerDict)
			{
				kvp.Key.fogTint = kvp.Value;

				//ModHelper.Console.WriteLine($"PlanetaryFogController named {kvp.Key.gameObject.name}");
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in fogOverrideVolumeDict)
			{
				kvp.Key.tint = kvp.Value;

				var fogSector = kvp.Key.GetValue<Sector>("_sector");
				if (fogSector == null) { }
				//else { ModHelper.Console.WriteLine($"FogOverrideVolume is in {fogSector.ToString()}"); }
			}
		}

		private void DisableFog()
		{
			ModHelper.Console.WriteLine("Disabling Fog");
			foreach (FogLightData fogLightData in fogLightDataList)
			{
				fogLightData.alpha = 0f;
			}
			foreach (KeyValuePair<FogWarpVolume, Color> kvp in fogWarpVolumeDict)
			{
				kvp.Key.SetValue("_fogColor", Color.clear);
			}
			foreach (KeyValuePair<PlanetaryFogController, Color> kvp in planetaryFogControllerDict)
			{
				kvp.Key.fogTint = Color.clear;
				ModHelper.Console.WriteLine("disabling fog pfogcontroller tint = " + kvp.Key.fogTint);
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in fogOverrideVolumeDict)
			{
				kvp.Key.tint = Color.clear;
			}
		}
	}
}