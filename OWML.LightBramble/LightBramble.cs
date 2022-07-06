using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections;
using System;
using System.Threading.Tasks;

namespace OWML.LightBramble
{
	public class LightBramble : ModBehaviour
	{
		public static IModAssets modAssets;
		public static AudioSource _dekuSource;
		public static OWAudioSource _brambleSource;
		public static OWAudioSource dekuAudioSource;

		public static List<FogLightData> fogLightDataList = new List<FogLightData>();
		public static Dictionary<FogWarpVolume, Color> fogWarpVolumeDict = new Dictionary<FogWarpVolume, Color>();
		public static Dictionary<PlanetaryFogController, Color> planetaryFogControllerDict = new Dictionary<PlanetaryFogController, Color>();
		public static Dictionary<FogOverrideVolume, Color> fogOverrideVolumeDict = new Dictionary<FogOverrideVolume, Color>();

		//for some reason, the anglerfish have a tendency to re-activate in the wrong positions and rotations. this fixes that.
		public static Dictionary<AnglerfishController, Transform> anglerFishDict = new Dictionary<AnglerfishController, Transform>();

		public static bool isInSolarSystem { get; private set; } = false;   //updated on every scene load
		//private bool isInBramble = false;   //updated by a global event called by the game

		//Config toggles, automatically modified from Configure when the user toggles them in the OWML menu
		private bool _swapMusic = true;
		public static bool _disableFish = true;
		public static bool _disableFog = true;

		//called when fish detect a new occupant in their sector
		public static Action ToggleFishAction;

		MethodInfo anglerChangeState;

		public static IModConsole modConsole;

		public static LightBramble instance;

		private void Awake()
		{
			ToggleFishAction = () => { };

			//setup singleton
			if (instance == null)
				instance = this;
			else
				Destroy(this.gameObject);
		}

		private void Start()
		{			
			modAssets = ModHelper.Assets;
			modConsole = ModHelper.Console;

			ModHelper.Console.WriteLine($"Start of {nameof(LightBramble)}");

			ModHelper.HarmonyHelper.AddPostfix<AnglerfishController>(nameof(AnglerfishController.OnSectorOccupantsUpdated), typeof(AnglerPatch), nameof(AnglerPatch.SectorUpdated));
			ModHelper.HarmonyHelper.AddPostfix<AnglerfishController>(nameof(AnglerfishController.Awake), typeof(AnglerPatch), nameof(AnglerPatch.AwakePostfix));
			ModHelper.HarmonyHelper.AddPostfix<AnglerfishController>(nameof(AnglerfishController.OnDestroy), typeof(AnglerPatch), nameof(AnglerPatch.OnDestroyPostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogOverrideVolume>(nameof(FogOverrideVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogOverrideVolumePostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogWarpVolume>(nameof(FogWarpVolume.Awake), typeof(FogPatches), nameof(FogPatches.FogWarpVolumePostfix));
			ModHelper.HarmonyHelper.AddPostfix<PlanetaryFogController>(nameof(PlanetaryFogController.Awake), typeof(FogPatches), nameof(FogPatches.PlanetaryFogPostfix));
			ModHelper.HarmonyHelper.AddPostfix<FogLight>(nameof(FogLight.Awake), typeof(FogPatches), nameof(FogPatches.FogLightPostfix));
			ModHelper.HarmonyHelper.AddPostfix<GlobalMusicController>(nameof(GlobalMusicController.Start), typeof(GlobalMusicControllerPatch), nameof(GlobalMusicControllerPatch.GlobalMusicControllerPostfix));

			ModHelper.Events.Subscribe<FogLight>(Events.AfterStart);
			ModHelper.Events.Subscribe<FogWarpVolume>(Events.AfterAwake);
			ModHelper.Events.Subscribe<PlanetaryFogController>(Events.AfterEnable);
			ModHelper.Events.Subscribe<FogOverrideVolume>(Events.AfterAwake);
			ModHelper.Events.Subscribe<GlobalMusicController>(Events.AfterStart);
			//ModHelper.Events.Event += OnEvent;

			//GlobalMessenger.AddListener("PlayerEnterBrambleDimension", PlayerEnterBramble);
			//GlobalMessenger.AddListener("PlayerExitBrambleDimension", PlayerExitBramble);

			//ToggleFishAction = () => ToggleFish(_disableFish);

			LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
			LoadManager.OnStartSceneLoad += OnStartSceneLoad;

			Type anglerType = typeof(AnglerfishController);
			anglerChangeState = anglerType.GetMethod(nameof(AnglerfishController.ChangeState), BindingFlags.NonPublic | BindingFlags.Instance);

			GlobalMessenger.AddListener("SuitUp", new Callback(OnSuitUp));

		}

		public override void Configure(IModConfig config)
		{
			_swapMusic = config.GetSettingsValue<bool>("swapMusic");
			_disableFish = config.GetSettingsValue<bool>("disableFish");
			_disableFog = config.GetSettingsValue<bool>("disableFog");
			CheckToggleables();
		}

		//private void PlayerEnterBramble()
		//{
		//	isInBramble = true;
		//	CheckToggleables();
		//}

		//private void PlayerExitBramble()
		//{
		//	isInBramble = false;
		//	EnableFog();
		//}

		private void CopyTransformValues(Transform source, Transform destination)
		{
			ModHelper.Console.WriteLine("Source before = " + source.position);
			ModHelper.Console.WriteLine("Destination before = " + destination.position);
			destination.position = source.position;
			destination.rotation = source.rotation;
			ModHelper.Console.WriteLine("Destination after = " + destination.position);
		}

		private void OnCompleteSceneLoad(OWScene oldScene, OWScene newScene)
		{
			isInSolarSystem = (newScene == OWScene.SolarSystem);
			if (isInSolarSystem)
			{  
				//ToggleFishAction = () => ToggleFish(_disableFish);

				//var sectorList = FindObjectsOfType<Sector>();
				//foreach (Sector sector in sectorList)
				//{
				//	if (sector.IsBrambleDimension())
				//	{
				//		List<FogLight> fogLights = sector.GetFogLights();
				//		foreach (FogLight light in fogLights)
				//		{
				//			ModHelper.Console.WriteLine("Fog light found in bramble");
				//			light.GetValue<FogLight.LightData>("_primaryLightData").alpha = 0f;
				//		}
				//	}
				//}

				AstroObject darkbramble = Locator.GetAstroObject(AstroObject.Name.DarkBramble);
				if (darkbramble == null)
				{
					ModHelper.Console.WriteLine("darkBramble null, wtf>>!");
					return;
				}


				//filter fog to only store the fog inside Dark Bramble
				var filteredPlanetaryFogDict = new Dictionary<PlanetaryFogController, Color>();
				foreach (KeyValuePair<PlanetaryFogController, Color> kvp in planetaryFogControllerDict)
				{
					if (darkbramble.IsInsideSphere(kvp.Key.transform.position, (kvp.Key.fogRadius)))
					{
						filteredPlanetaryFogDict.Add(kvp.Key, kvp.Value);
					}
				}
				ModHelper.Console.WriteLine("before = " + planetaryFogControllerDict.Count);
				//planetaryFogControllerDict = filteredPlanetaryFogDict;
				ModHelper.Console.WriteLine("after = " + planetaryFogControllerDict.Count);

				//fogLightDataList.RemoveAll(fogLightData => darkBrambleMesh.bounds.Contains(fogLightData.fogLight.transform.position));

				ModHelper.Console.WriteLine("before foglight= " + fogLightDataList.Count);


				fogLightDataList.RemoveAll(fogLightData => !(fogLightData.fogLight.IsInBrambleSector()));

				//ModHelper.Console.WriteLine(anglerFishDict.First().Key.transform.parent.ToString());

				ModHelper.Console.WriteLine("after foglight = " + fogLightDataList.Count);

				ModHelper.Console.WriteLine("before fogoverridevolume= " + fogOverrideVolumeDict.Count);

				//fogOverrideVolumeDict = fogOverrideVolumeDict.Where(kvp => kvp.Key.IsInBrambleSector()).ToDictionary(i => i.Key, i => i.Value);
				
				var filteredFogOVDict = new Dictionary<FogOverrideVolume, Color>();
				foreach (Collider collider in darkbramble.GetComponentsInChildren<Collider>())
				{
					ModHelper.Console.WriteLine("dark bramble collider = " + collider);
					foreach (KeyValuePair<FogOverrideVolume, Color> kvp in fogOverrideVolumeDict)
					{
						if (!filteredFogOVDict.ContainsKey(kvp.Key) && collider.bounds.Contains(kvp.Key.transform.position))
						{
							filteredFogOVDict.Add(kvp.Key, kvp.Value);
						}
					}
				}
				fogOverrideVolumeDict = filteredFogOVDict;

				//fogOverrideVolumeDict =
				//(from collider in darkbramble.GetComponentsInChildren<Collider>()
				// from kvp in fogOverrideVolumeDict
				// where collider.bounds.Contains(kvp.Key.transform.position)
				// select kvp).ToDictionary(i => i.Key, i => i.Value);


				//fogOverrideVolumeDict = fogOverrideVolumeDict.Where(kvp => kvp.Key.IsInsideSphere(darkbramble.gameObject.transform.position, 60000)).ToDictionary(i => i.Key, i => i.Value);
				//fogOverrideVolumeDict = new Dictionary<FogOverrideVolume, Color>();
				//fogOverrideVolumeDict = fogOverrideVolumeDict.Where(kvp => darkbramble.IsInsideSphere(kvp.Key.transform.position, kvp.Key.radius + 3000)).ToDictionary(i => i.Key, i => i.Value);
				ModHelper.Console.WriteLine("after fogoverridevolume= " + fogOverrideVolumeDict.Count);

				ModHelper.Console.WriteLine("before fogWarpVolumeDict= " + fogWarpVolumeDict.Count);

				//NOTES:
				//I think sector checking is unreliable on this or fogoverride volume. the planetary fog filter also fails to capture bramble

				fogWarpVolumeDict = fogWarpVolumeDict.Where(kvp => kvp.Key.IsInBrambleSector()).ToDictionary(i => i.Key, i => i.Value);
				//fogWarpVolumeDict = fogWarpVolumeDict.Where(kvp => darkbramble.IsInsideSphere(kvp.Key.transform.position, kvp.Key.)).ToDictionary(i => i.Key, i => i.Value);

				ModHelper.Console.WriteLine("after fogWarpVolumeDict= " + fogWarpVolumeDict.Count);

				//ModHelper.Console.WriteLine("dark bramble child = " + darkbramble.transform.GetChild(2));


				CheckToggleables();
			}
		}

		private void OnSuitUp()
		{
			WarpPlayer(AstroObject.Name.DarkBramble);
		}

		private void WarpPlayer(AstroObject.Name astroObjectName)
		{
			var astroObject = Locator.GetAstroObject(astroObjectName);
			Locator.GetShipBody().WarpToPositionRotation(astroObject.transform.position, astroObject.transform.rotation);
		}

		private void OnStartSceneLoad(OWScene oldScene, OWScene newScene)
		{
			if (newScene != OWScene.SolarSystem)
			{
				//ToggleFishAction = () => { };   //trying to toggle fish from outside solar system scene causes null-ref
			}
		}

		//returns objects that are in the BrambleDimension or VesselDimension sectors
		private IEnumerable<T> FilterObjectsInBrambleSector<T>(IEnumerable<T> enumerable)
		{
			List<T> newList = new List<T>();
			foreach (T obj in enumerable)
			{
				if (obj.IsInBrambleSector())
					newList.Add(obj);
			}
			return newList;
		}

		private void CheckToggleables()
		{
			if (isInSolarSystem)
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
				ToggleFish(_disableFish);
			}
		}

		private void ToggleFish(bool disabled)
		{
			if (!isInSolarSystem)
				return;

			ModHelper.Console.WriteLine("Disabling multiple fish? : " + disabled.ToString());
			foreach (AnglerfishController anglerfishController in anglerFishDict.Keys.ToList())
			{
				ToggleAFish(anglerfishController, disabled);
			}
		}

		public void ToggleAFish(AnglerfishController anglerfishController, bool disabled)
		{
			if (!isInSolarSystem)
				return;

			if (disabled)
			{
				//set anglerfish state to lurking so that the angler is not still following player when re-enabled
				anglerChangeState.Invoke(anglerfishController, new object[] { AnglerfishController.AnglerState.Lurking });
				//CopyTransformValues(anglerfishController.transform, anglerFishDict[anglerfishController]);
				//anglerFishDict[anglerfishController] = anglerfishController.transform;
				anglerfishController.GetAttachedOWRigidbody().Suspend();
				anglerfishController.gameObject.SetActive(false);
			}
			else
			{
				//CopyTransformValues(anglerFishDict[anglerfishController], anglerfishController.transform);
				anglerfishController.gameObject.SetActive(true);
				anglerfishController.GetAttachedOWRigidbody().Unsuspend();
				//Transform dictTransform = anglerFishDict[anglerfishController];
				//anglerfishController.GetAttachedOWRigidbody().Unsuspend();
				//anglerfishController.GetAttachedOWRigidbody().MoveToPosition(dictTransform.position);
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
				//kvp.Key.SetValue("_fogTint", Color.clear);
			}
			foreach (KeyValuePair<FogOverrideVolume, Color> kvp in fogOverrideVolumeDict)
			{
				kvp.Key.tint = Color.clear;
				//kvp.Key.SetValue("_tint", Color.clear);
			}
		}
	}

	class AnglerPatch
	{
		static public void SectorUpdated(AnglerfishController __instance)
		{
			LightBramble.instance.ToggleAFish(__instance, LightBramble._disableFish);
			//LightBramble.ToggleFishAction();
			//__instance.gameObject.SetActive(LightBramble._disableFish);
			//__instance.GetValue<OWRigidbody>("_brambleBody").gameObject.SetActive(LightBramble._disableFish);
		}
		static public void AwakePostfix(AnglerfishController __instance)
		{
			LightBramble.anglerFishDict.Add(__instance, __instance.transform);
		}
		static public void OnDestroyPostfix(AnglerfishController __instance)
		{
			LightBramble.anglerFishDict.Remove(__instance);
		}
	}

	class FogPatches
	{
		static public void FogWarpVolumePostfix(FogWarpVolume __instance)
		{
			LightBramble.fogWarpVolumeDict.Add(__instance, __instance.GetValue<Color>("_fogColor"));

		}
		static public void PlanetaryFogPostfix(PlanetaryFogController __instance)
		{
			LightBramble.planetaryFogControllerDict.Add(__instance, __instance.fogTint);
			//__instance.transform.position.WhenEncompassesAstroObjectAsync
			//(
			//	__instance.fogRadius,
			//	AstroObject.Name.DarkBramble,
			//	() => AddPlanetaryFog(__instance)
			//);
		}

		static public void FogLightPostfix(FogLight __instance)
		{
			//__instance.transform.position.WhenEncompassesAstroObjectAsync
			//(
			//	__instance.GetMaxVisibleDistance(),
			//	AstroObject.Name.DarkBramble,
			//	() => AddFogLight(__instance)
			//);
			FogLightData fogLightData = new FogLightData(__instance);
			LightBramble.fogLightDataList.Add(fogLightData);
			LightBramble.modConsole.WriteLine("foglightpostfix triggered");
		}

		static public void FogOverrideVolumePostfix(FogOverrideVolume __instance)
		{
			//LightBramble.fogOverrideVolumeDict.Add(__instance, __instance.GetValue<Color>("_tint"));
			LightBramble.fogOverrideVolumeDict.Add(__instance, __instance.tint);
		}
	}

	class GlobalMusicControllerPatch
	{
		static public void GlobalMusicControllerPostfix(GlobalMusicController __instance)
		{
			LightBramble._brambleSource = __instance.GetValue<OWAudioSource>("_darkBrambleSource");
			LightBramble._brambleSource.Stop();

			LightBramble._dekuSource = __instance.gameObject.AddComponent<AudioSource>();
			LightBramble._dekuSource.clip = LightBramble.modAssets.GetAudio("deku-tree.mp3");
			LightBramble.dekuAudioSource = __instance.gameObject.AddComponent<OWAudioSource>();
			LightBramble.dekuAudioSource.time = 0;

			LightBramble.dekuAudioSource.SetValue("_audioSource", LightBramble._dekuSource);
			__instance.SetValue("_darkBrambleSource", LightBramble.dekuAudioSource);
		}
	}

	static class Extensions
	{
		public static bool IsContainedIn<T>(this T source, IEnumerable<T> list)
		{
			if (source == null) throw new ArgumentNullException("IsContainedIn: source null");
			return list.Contains(source);
		}

		public static bool IsContainedIn<T>(this T source, params T[] list)
		{
			if (source == null) throw new ArgumentNullException("IsContainedIn: source null");
			return list.Contains(source);
		}

		public static bool IsInsideSphere(this AstroObject astroObject, Vector3 sphereOrigin, float sphereRadius)
		{
			Collider[] colliders = Physics.OverlapSphere(sphereOrigin, sphereRadius);
			if (colliders != null && colliders.Length > 0 && astroObject != null)
			{
				foreach (Collider collider in colliders)
				{
					if (collider.transform.position == astroObject.transform.position)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static bool IsInsideSphere(this MonoBehaviour objectToTest, Vector3 sphereOrigin, float sphereRadius)
		{
			Collider[] colliders = Physics.OverlapSphere(sphereOrigin, sphereRadius);
			if (colliders != null && colliders.Length > 0 && objectToTest != null)
			{
				foreach (Collider collider in colliders)
				{
					if (collider.transform.position == objectToTest.transform.position)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static bool IsInsideSphere(this Vector3 objectToTest, Vector3 sphereOrigin, float sphereRadius)
		{
			Collider[] colliders = Physics.OverlapSphere(sphereOrigin, sphereRadius);
			if (colliders != null && colliders.Length > 0 && objectToTest != null)
			{
				foreach (Collider collider in colliders)
				{
					if (collider.transform.position == objectToTest)
					{
						return true;
					}
				}
			}
			return false;
		}

		//waits until an Astro Object loads, then executes a function if the sphere encompasses the object
		public static async void WhenEncompassesAstroObjectAsync(this Vector3 sphereOrigin, float sphereRadius, AstroObject.Name astroObjectName, Action actionWhenDone)
		{
			//wait until Astro Object (Dark Bramble in our case) is loaded
			await WaitWhile(() => Locator.GetAstroObject(astroObjectName) == null, 15);

			AstroObject astroObject = Locator.GetAstroObject(astroObjectName);
			Collider[] colliders = Physics.OverlapSphere(sphereOrigin, sphereRadius);
			if (colliders != null && colliders.Length > 0 && astroObject != null)
			{
				foreach (Collider collider in colliders)
				{
					if (collider.transform.position == astroObject.transform.position)
					{
						actionWhenDone?.Invoke();
					}
				}
			}
		}

		public static async Task WaitWhile(Func<bool> condition, int frequency = 25, int timeout = -1)
		{
			var waitTask = Task.Run(async () =>
			{
				while (condition()) await Task.Delay(frequency);
			});

			if (waitTask != await Task.WhenAny(waitTask, Task.Delay(timeout)))
				throw new TimeoutException();
		}
		
		public static bool IsInBrambleSector<MonoBehaviour>(this MonoBehaviour obj)
		{
			//IEnumerable<string> brambleSectors = new List<string>{"Sector_AnglerNestDimension", "Sector_HubDimension",
			//	"Sector_ExitOnlyDimension", "Sector_EscapePodDimension", "Sector_EscapePodBody",
			//	"Sector_SmallNestDimension", "Sector_ClusterDimension", "Sector_PioneerDimension" };

			//brambleSectors = brambleSectors.Select(sectorName => sectorName + " (Sector)");

			LightBramble.modConsole.WriteLine("sector is ");

			var sector = obj.GetValue<Sector>("_sector");

			if (sector != null)
			{
				//if (sector.ToString().IsContainedIn(brambleSectors))
				//{
				//	return true;
				//}
				//else
				LightBramble.modConsole.WriteLine(sector.GetName());
				if (sector.GetName().IsContainedIn(Sector.Name.BrambleDimension, Sector.Name.VesselDimension, Sector.Name.DarkBramble))
				{
					//modHelper.Console.WriteLine(sector.GetName().ToString());
					return true;
				}
				//else
				//modHelper.Console.WriteLine(sector.GetName() + " not in bramble...");
			}
			else
			{
				LightBramble.modConsole.WriteLine("Sector null");
			}
			return false;
		}
	}

	/// <summary>
	/// Bundles all the important FogLight data together so it can fit neatly in a list
	/// </summary>
	public class FogLightData
	{
		public readonly FogLight fogLight;
		public readonly FogLight.LightData lightData;
		public readonly float originalAlpha;
		public float alpha
		{
			get { return lightData.alpha; }
			set { lightData.alpha = value; }
		}

		public FogLightData(FogLight fogLight)
		{
			this.fogLight = fogLight;
			lightData = fogLight.GetValue<FogLight.LightData>("_primaryLightData");
			originalAlpha = lightData.alpha;
		}
	}
}