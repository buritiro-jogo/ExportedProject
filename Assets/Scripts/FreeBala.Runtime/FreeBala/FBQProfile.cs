using System;

namespace FreeBala
{
[Serializable]
public class FBQProfile
{
	public FBQLevel level;

	public FBPlat plat;

	public bool mobile;

	public bool shadows;

	public float shadowDistance;

	public int shadowRes;

	public int shadowCascades;

	public bool softShadows;

	public float smallCasterSize;

	public bool postFX;

	public bool bloom;

	public bool ssao;

	public bool volumetricClouds;

	public bool realtimeReflections;

	public bool heightFog;

	public bool detailMaps;

	public int maxPixelLights;

	public float vegetationDensity;

	public float ambientParticles;

	public int maxVoices;

	public bool decals;

	public int maxDecals;

	public int maxParticles;

	public float particleScale;

	public float rainDensity;

	public float impactChance;

	public bool hdOnlyLocal;

	public float footIKDistance;

	public int footIKInterval;

	public float animCullDistance;

	public int skinWeights;

	public float lodBias;

	public int maxLodLevel;

	public int mipLimit;

	public int targetFps;

	public int texBudgetMB;

	public float renderScale;

	public float minRenderScale;

	public float drawDistance;

	public bool dynamicResolution;

	public int maxTraffic;

	public int maxNpcs;

	public float vegDrawDistance;

	public float vegLodDistance;

	public int vegMaxInstances;

	public bool vegShadows;

	public bool vegCrossFade;

	public bool vegGpuCulling;
}
}
