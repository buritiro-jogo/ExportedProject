using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
public static class FBLightUtil
{
	public static void MediumShadows(Light l)
	{
		if ((bool)l)
		{
			UniversalAdditionalLightData universalAdditionalLightData = l.GetUniversalAdditionalLightData();
			if ((bool)universalAdditionalLightData)
			{
				universalAdditionalLightData.additionalLightsShadowResolutionTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
			}
		}
	}
}
}
