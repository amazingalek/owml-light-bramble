using OWML.Utils;

namespace OWML.LightBramble
{
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
