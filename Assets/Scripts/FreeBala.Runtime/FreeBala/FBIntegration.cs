using UnityEngine;

namespace FreeBala
{
public static class FBIntegration
{
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Boot()
	{
		FBGfx.FlagsOverride = delegate(FBGfxTier t)
		{
			FBGfxFlags result = FBGfx.Defaults(t);
			result.ssao &= FBQuality.SSAO;
			result.volClouds &= FBQuality.VolumetricClouds;
			result.heightFog &= FBQuality.PostFX && FBQuality.HeightFog;
			result.detailMaps &= FBQuality.DetailMaps;
			result.volumetric &= FBQuality.PostFX;
			result.ssr &= FBQuality.RealtimeReflections;
			float ambientParticles = FBQuality.AmbientParticles;
			result.lampInsects = Mathf.RoundToInt((float)result.lampInsects * ambientParticles);
			result.dustMotes = Mathf.RoundToInt((float)result.dustMotes * ambientParticles);
			result.smokePlumes = Mathf.RoundToInt((float)result.smokePlumes * ambientParticles);
			return result;
		};
		FBQuality.Changed -= Sync;
		FBQuality.Changed += Sync;
		Sync();
	}

	private static void Sync()
	{
		FBGfx.Refresh();
		FBFxQuality.Apply(Mathf.Clamp((int)(FBQuality.Level - 1), 0, 2));
		FBFootIK.Q.FarDist = Mathf.Max(8f, FBQuality.FootIKDistance);
		FBFootIK.Q.NearDist = Mathf.Min(FBFootIK.Q.FarDist * 0.5f, 18f);
		if (FBVoice.D != null)
		{
			FBVoice.D.maxVoices = FBQuality.MaxVoices;
		}
	}
}
}
