using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public static class FBQuality
{
	private class Snapshot
	{
		private readonly UniversalRenderPipelineAsset a;

		private readonly float rs;

		private readonly float sd;

		private readonly int sc;

		private readonly int res;

		private readonly int lights;

		public Snapshot(UniversalRenderPipelineAsset a)
		{
			this.a = a;
			rs = a.renderScale;
			sd = a.shadowDistance;
			sc = a.shadowCascadeCount;
			res = a.mainLightShadowmapResolution;
			lights = a.maxAdditionalLightsCount;
		}

		public void Restore()
		{
			if ((bool)a)
			{
				a.renderScale = rs;
				a.shadowDistance = sd;
				a.shadowCascadeCount = sc;
				a.mainLightShadowmapResolution = res;
				a.maxAdditionalLightsCount = lights;
			}
		}
	}

	public static readonly string[] Names = new string[4] { "BAIXO", "MÉDIO", "ALTO", "ULTRA" };

	public static bool FootIK = true;

	public static int VegVisibleInstances;

	public static int VegTotalInstances;

	private static bool booted;

	private static bool applying;

	private static Snapshot snap;

	private static readonly List<(Renderer r, ShadowCastingMode m)> casters = new List<(Renderer, ShadowCastingMode)>();

	private static readonly Dictionary<LODGroup, float> lodOrig = new Dictionary<LODGroup, float>();

	public static FBQProfile P { get; private set; } = Build(FBQLevel.Alto, new FBDeviceInfo
	{
		plat = FBPlat.PC
	});


	public static FBQLevel Level => P.level;

	public static bool Auto { get; private set; } = true;


	public static FBQLevel Detected { get; private set; } = FBQLevel.Alto;


	public static FBDeviceInfo Device { get; private set; }

	public static bool Shadows => P.shadows;

	public static float ShadowDistance => P.shadowDistance;

	public static float SmallCasterSize => P.smallCasterSize;

	public static bool PostFX => P.postFX;

	public static bool Bloom => P.bloom;

	public static bool SSAO => P.ssao;

	public static bool VolumetricClouds => P.volumetricClouds;

	public static bool RealtimeReflections => P.realtimeReflections;

	public static bool HeightFog => P.heightFog;

	public static bool DetailMaps => P.detailMaps;

	public static float AmbientParticles => P.ambientParticles;

	public static int MaxVoices => P.maxVoices;

	public static int MaxPixelLights => P.maxPixelLights;

	public static float VegetationDensity => P.vegetationDensity;

	public static bool Decals => P.decals;

	public static int MaxDecals => P.maxDecals;

	public static int MaxParticles => P.maxParticles;

	public static float ParticleScale => P.particleScale;

	public static float ImpactChance => P.impactChance;

	public static bool HdOnlyLocal => P.hdOnlyLocal;

	public static float RainDensity => P.rainDensity;

	public static float FootIKDistance => P.footIKDistance;

	public static int FootIKInterval => P.footIKInterval;

	public static float AnimCullDistance => P.animCullDistance;

	public static float LodBias => P.lodBias;

	public static float RenderScale => P.renderScale;

	public static float DrawDistance => P.drawDistance;

	public static int MaxTraffic => P.maxTraffic;

	public static int MaxNpcs => P.maxNpcs;

	public static bool Mobile => P.mobile;

	public static float VegDrawDistance => P.vegDrawDistance;

	public static float VegLodDistance => P.vegLodDistance;

	public static int VegMaxInstances => P.vegMaxInstances;

	public static bool VegShadows => P.vegShadows;

	public static bool VegCrossFade => P.vegCrossFade;

	public static bool VegGpuCulling => P.vegGpuCulling;

	public static int Choice
	{
		get
		{
			if (!Auto)
			{
				return (int)Level;
			}
			return -1;
		}
	}

	public static bool Simulated { get; private set; }

	public static int LastCulledCasters { get; private set; }

	public static float LodCutK
	{
		get
		{
			if (!P.mobile)
			{
				if (P.level < FBQLevel.Alto)
				{
					if (P.level != FBQLevel.Medio)
					{
						return 0.9f;
					}
					return 0.7f;
				}
				return 0.55f;
			}
			return 1f;
		}
	}

	public static event Action Changed;

	public static bool FootIKFor(Vector3 pos, bool isPlayer, int id)
	{
		if (!FootIK)
		{
			return false;
		}
		if (isPlayer)
		{
			return true;
		}
		Camera main = Camera.main;
		if ((bool)main && (main.transform.position - pos).sqrMagnitude > P.footIKDistance * P.footIKDistance)
		{
			return false;
		}
		if (P.footIKInterval > 1)
		{
			return (Time.frameCount + (id & 0xFF)) % P.footIKInterval == 0;
		}
		return true;
	}

	private static bool Has(string s, params string[] k)
	{
		foreach (string value in k)
		{
			if (s.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}

	public static FBQLevel Detect(FBDeviceInfo d)
	{
		string s = d.gpu ?? "";
		switch (d.plat)
		{
		case FBPlat.Android:
		{
			bool flag3 = Has(s, "Mali-T", "Mali-G3", "Mali-G5", "Mali-400", "Mali-450", "PowerVR", "Adreno (TM) 3", "Adreno (TM) 4", "Adreno (TM) 5");
			bool flag4 = Has(s, "Adreno (TM) 7", "Adreno (TM) 8", "Adreno (TM) 66", "Adreno (TM) 69", "Immortalis", "Mali-G71", "Mali-G72", "Mali-G76", "Mali-G77", "Mali-G78", "Mali-G710", "Mali-G715", "Mali-G720", "Mali-G610", "Mali-G615", "Xclipse");
			if (flag3 || d.memMB < 3500 || d.cores < 6 || d.shaderLevel < 35)
			{
				return FBQLevel.Baixo;
			}
			if (flag4 && d.memMB >= 7000 && d.cores >= 8)
			{
				return FBQLevel.Alto;
			}
			return FBQLevel.Medio;
		}
		case FBPlat.iOS:
			if (d.memMB < 5500)
			{
				if (d.memMB < 2900)
				{
					return FBQLevel.Baixo;
				}
				return FBQLevel.Medio;
			}
			return FBQLevel.Alto;
		case FBPlat.WebGL:
			if (d.mobileBrowser)
			{
				return FBQLevel.Baixo;
			}
			if (Has(s, "RTX", "RX 6", "RX 7", "RX 9", "Apple M", "Arc A7"))
			{
				return FBQLevel.Alto;
			}
			if (Has(s, "Intel", "SwiftShader", "llvmpipe", "Basic Render", "Mali", "Adreno"))
			{
				return FBQLevel.Baixo;
			}
			return FBQLevel.Medio;
		default:
		{
			bool flag = Has(s, "Basic Render", "SwiftShader", "llvmpipe", "Microsoft Basic");
			bool flag2 = Has(s, "Intel(R) HD", "Intel(R) UHD", "Intel HD", "Intel UHD", "Iris", "Radeon(TM) Graphics", "Radeon Graphics", "Radeon(TM) Vega", "Vega 8", "Vega 6", "Vega 3", "Radeon 6", "Radeon 7", "Radeon 8", "Radeon(TM) 6", "Radeon(TM) 7", "Radeon(TM) 8");
			if (flag || d.memMB < 4000)
			{
				return FBQLevel.Baixo;
			}
			if (flag2)
			{
				if (d.memMB < 12000 || !Has(s, "Xe Graphics", "Iris Xe", "Iris(R) Xe", "680M", "780M", "880M", "890M", "Arc"))
				{
					return FBQLevel.Baixo;
				}
				return FBQLevel.Medio;
			}
			if (d.gpuMB >= 7500 && d.memMB >= 15000 && d.cores >= 8)
			{
				return FBQLevel.Ultra;
			}
			if (d.gpuMB >= 3500 && d.memMB >= 7500)
			{
				return FBQLevel.Alto;
			}
			if (d.gpuMB < 1800)
			{
				return FBQLevel.Baixo;
			}
			return FBQLevel.Medio;
		}
		}
	}

	public static FBQProfile Build(FBQLevel L, FBDeviceInfo d)
	{
		int i = (int)L;
		bool mobile = d.Mobile;
		bool flag = d.plat == FBPlat.WebGL;
		FBQProfile fBQProfile = new FBQProfile
		{
			level = L,
			plat = d.plat,
			mobile = mobile
		};
		fBQProfile.shadows = i >= 1 || !mobile;
		fBQProfile.shadowDistance = (mobile ? V<float>(0f, 25f, 40f, 55f) : V<float>(32f, 60f, 85f, 120f));
		fBQProfile.shadowRes = (mobile ? V<int>(512, 1024, 1024, 2048) : V<int>(1024, 2048, 2048, 4096));
		fBQProfile.shadowCascades = ((mobile || flag) ? 1 : V<int>(1, 2, 3, 4));
		fBQProfile.softShadows = i >= 2;
		fBQProfile.smallCasterSize = V<float>(2.5f, 1.4f, 0.7f, 0.3f);
		fBQProfile.postFX = i >= 1 || !mobile;
		fBQProfile.bloom = i >= ((!mobile) ? 1 : 2);
		fBQProfile.ssao = !mobile && !flag && i >= 2;
		fBQProfile.volumetricClouds = (mobile ? (i >= 3) : (i >= 2));
		fBQProfile.realtimeReflections = !mobile && i >= 3;
		fBQProfile.maxPixelLights = (mobile ? V<int>(1, 2, 4, 4) : V<int>(2, 4, 8, 8));
		fBQProfile.vegetationDensity = (mobile ? V<float>(0.15f, 0.35f, 0.6f, 0.8f) : (flag ? V<float>(0.25f, 0.45f, 0.7f, 0.85f) : V<float>(0.3f, 0.6f, 0.85f, 1f)));
		fBQProfile.vegDrawDistance = (mobile ? V<float>(25f, 40f, 60f, 80f) : (flag ? V<float>(30f, 50f, 75f, 100f) : V<float>(40f, 70f, 110f, 160f)));
		fBQProfile.vegLodDistance = (mobile ? V<float>(8f, 14f, 22f, 30f) : V<float>(14f, 22f, 35f, 55f));
		fBQProfile.vegMaxInstances = (mobile ? V<int>(2000, 5000, 10000, 16000) : (flag ? V<int>(4000, 8000, 16000, 26000) : V<int>(8000, 20000, 45000, 90000)));
		fBQProfile.vegShadows = !mobile && !flag && i >= 2;
		fBQProfile.vegCrossFade = !mobile && i >= 2;
		fBQProfile.vegGpuCulling = !flag && i >= 1 && SystemInfo.supportsComputeShaders;
		fBQProfile.heightFog = ((mobile || flag) ? (i >= 2) : (i >= 1));
		fBQProfile.detailMaps = (mobile ? (i >= 2) : (i >= 1));
		fBQProfile.ambientParticles = (mobile ? V<float>(0.25f, 0.5f, 0.8f, 1f) : V<float>(0.4f, 0.7f, 1f, 1.3f));
		fBQProfile.maxVoices = (mobile ? V<int>(3, 4, 5, 6) : V<int>(4, 6, 8, 10));
		fBQProfile.decals = i >= 1;
		fBQProfile.maxDecals = V<int>(0, 32, 96, 256);
		fBQProfile.maxParticles = (mobile ? V<int>(120, 300, 600, 1000) : V<int>(200, 500, 1200, 2500));
		fBQProfile.particleScale = V<float>(0.4f, 0.7f, 1f, 1f);
		fBQProfile.impactChance = V<float>(0.3f, 0.65f, 1f, 1f);
		fBQProfile.hdOnlyLocal = i <= 2;
		fBQProfile.rainDensity = V<float>(0.3f, 0.6f, 1f, 1f);
		fBQProfile.footIKDistance = V<float>(8f, 18f, 35f, 60f);
		fBQProfile.footIKInterval = V<int>(3, 2, 1, 1);
		fBQProfile.animCullDistance = V<float>(35f, 60f, 110f, 200f);
		fBQProfile.skinWeights = (mobile ? V<int>(1, 2, 2, 4) : V<int>(2, 2, 4, 4));
		fBQProfile.lodBias = (mobile ? V<float>(0.6f, 0.8f, 1.2f, 1.8f) : V<float>(0.8f, 1.2f, 1.8f, 2.4f));
		fBQProfile.maxLodLevel = ((i == 0 && mobile) ? 1 : 0);
		fBQProfile.mipLimit = (mobile ? V<int>(1, 1, 0, 0) : V<int>(1, 0, 0, 0));
		fBQProfile.texBudgetMB = ((mobile || flag) ? V<int>(256, 384, 512, 768) : Mathf.Clamp(Mathf.Max(V<int>(768, 1536, 2560, 3584), d.gpuMB * 6 / 10), 512, Mathf.Max(768, d.gpuMB * 8 / 10)));
		fBQProfile.renderScale = ((d.plat == FBPlat.Android) ? V<float>(0.6f, 0.75f, 0.85f, 1f) : ((d.plat == FBPlat.iOS) ? V<float>(0.65f, 0.8f, 0.9f, 1f) : (flag ? V<float>(0.7f, 0.85f, 1f, 1f) : V<float>(0.85f, 1f, 1f, 1f))));
		fBQProfile.minRenderScale = Mathf.Max(0.5f, fBQProfile.renderScale - 0.2f);
		fBQProfile.dynamicResolution = mobile || flag;
		fBQProfile.targetFps = (mobile ? V<int>(30, 45, 60, 60) : (-1));
		fBQProfile.drawDistance = V<float>(250f, 400f, 700f, 1200f);
		fBQProfile.maxTraffic = V<int>(3, 6, 12, 18);
		fBQProfile.maxNpcs = V<int>(4, 10, 18, 28);
		return fBQProfile;
		T V<T>(T b, T m, T a, T u)
		{
			if (i != 0)
			{
				if (i != 1)
				{
					if (i != 2)
					{
						return u;
					}
					return a;
				}
				return m;
			}
			return b;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Boot()
	{
		if (booted)
		{
			return;
		}
		booted = true;
		Device = FBDeviceInfo.Current();
		Detected = Detect(Device);
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbplat");
		if (num >= 0 && num + 1 < commandLineArgs.Length && Enum.TryParse<FBPlat>(commandLineArgs[num + 1], ignoreCase: true, out var result))
		{
			FBDeviceInfo device = Device;
			device.plat = result;
			Device = device;
			Detected = Detect(Device);
			Simulated = true;
		}
		int num2 = PlayerPrefs.GetInt("fb_qlevel", -1);
		if (num2 < 0)
		{
			int @int = PlayerPrefs.GetInt("fb_quality", -1);
			if (@int >= 0)
			{
				num2 = Mathf.Clamp(@int, 0, 2);
			}
		}
		Auto = num2 < 0;
		Apply(Auto ? Detected : ((FBQLevel)Mathf.Clamp(num2, 0, 3)));
		SceneManager.sceneLoaded += delegate(Scene s, LoadSceneMode m)
		{
			OnScene(s);
		};
		QualitySettings.activeQualityLevelChanged += delegate
		{
			if (!applying)
			{
				Apply(P.level);
			}
		};
		Debug.Log(string.Format("[FreeBala][qualidade] {0} · GPU {1} {2} MB · RAM {3} MB · {4} núcleos · detectado {5} → usando {6}{7}", Device.plat, Device.gpu, Device.gpuMB, Device.memMB, Device.cores, Names[(int)Detected], Names[(int)Level], Auto ? " (auto)" : ""));
	}

	public static void Set(int level)
	{
		PlayerPrefs.SetInt("fb_qlevel", level);
		PlayerPrefs.Save();
		Auto = level < 0;
		Apply(Auto ? Detected : ((FBQLevel)Mathf.Clamp(level, 0, 3)));
	}

	public static void SimulatePlatform(FBPlat? plat)
	{
		if (!booted)
		{
			Boot();
		}
		FBDeviceInfo device = FBDeviceInfo.Current();
		if (plat.HasValue)
		{
			device.plat = plat.Value;
		}
		Device = device;
		Simulated = plat.HasValue;
		Detected = Detect(Device);
		Apply(Auto ? Detected : P.level);
	}

	public static void Apply(FBQLevel L)
	{
		if (!booted)
		{
			booted = true;
			Device = FBDeviceInfo.Current();
			Detected = Detect(Device);
		}
		P = Build(L, Device);
		applying = true;
		try
		{
			int num = QualitySettings.names.Length;
			int num2 = Array.IndexOf(QualitySettings.names, "Web");
			int a = ((Device.plat == FBPlat.WebGL && num2 >= 0) ? num2 : ((!P.mobile) ? Mathf.Clamp((int)(L - 1), 0, 2) : 0));
			if (num > 0 && QualitySettings.GetQualityLevel() != Mathf.Min(a, num - 1))
			{
				QualitySettings.SetQualityLevel(Mathf.Min(a, num - 1), applyExpensiveChanges: true);
			}
			QualitySettings.vSyncCount = 0;
			QualitySettings.lodBias = P.lodBias;
			QualitySettings.maximumLODLevel = P.maxLodLevel;
			QualitySettings.meshLodThreshold = Mathf.Clamp(1.2f / Mathf.Max(0.3f, P.lodBias), 0.45f, 2.2f);
			QualitySettings.skinWeights = P.skinWeights switch
			{
				1 => SkinWeights.OneBone, 
				2 => SkinWeights.TwoBones, 
				_ => SkinWeights.FourBones, 
			};
			QualitySettings.globalTextureMipmapLimit = P.mipLimit;
			QualitySettings.anisotropicFiltering = L switch
			{
				FBQLevel.Medio => AnisotropicFiltering.Enable, 
				FBQLevel.Baixo => AnisotropicFiltering.Disable, 
				_ => AnisotropicFiltering.ForceEnable, 
			};
			QualitySettings.realtimeReflectionProbes = P.realtimeReflections;
			QualitySettings.particleRaycastBudget = (P.mobile ? 64 : 256);
			QualitySettings.asyncUploadTimeSlice = (P.mobile ? 4 : 8);
			QualitySettings.asyncUploadBufferSize = (P.mobile ? 32 : 64);
			QualitySettings.asyncUploadPersistentBuffer = !P.mobile;
			Application.targetFrameRate = P.targetFps;
			QualitySettings.streamingMipmapsActive = true;
			QualitySettings.streamingMipmapsAddAllCameras = true;
			QualitySettings.streamingMipmapsMemoryBudget = P.texBudgetMB;
			QualitySettings.streamingMipmapsRenderersPerFrame = (P.mobile ? 512 : 2048);
			QualitySettings.streamingMipmapsMaxLevelReduction = (P.mobile ? 3 : 2);
			QualitySettings.streamingMipmapsMaxFileIORequests = (P.mobile ? 256 : 1024);
			if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset universalRenderPipelineAsset)
			{
				if (snap == null)
				{
					snap = new Snapshot(universalRenderPipelineAsset);
				}
				universalRenderPipelineAsset.renderScale = P.renderScale;
				universalRenderPipelineAsset.shadowDistance = (P.shadows ? P.shadowDistance : 0f);
				universalRenderPipelineAsset.shadowCascadeCount = P.shadowCascades;
				universalRenderPipelineAsset.mainLightShadowmapResolution = P.shadowRes;
				universalRenderPipelineAsset.maxAdditionalLightsCount = P.maxPixelLights;
				universalRenderPipelineAsset.cascadeBorder = 0.3f;
			}
			FBDynRes.Setup();
		}
		finally
		{
			applying = false;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (activeScene.isLoaded)
		{
			OnScene(activeScene);
		}
		FBQuality.Changed?.Invoke();
	}

	private static void OnScene(Scene s)
	{
		Camera main = Camera.main;
		if ((bool)main && main.TryGetComponent<UniversalAdditionalCameraData>(out var component))
		{
			component.renderShadows = P.shadows;
		}
		if ((bool)main)
		{
			main.useOcclusionCulling = false;
		}
		foreach (var (renderer, shadowCastingMode) in casters)
		{
			if ((bool)renderer)
			{
				renderer.shadowCastingMode = shadowCastingMode;
			}
		}
		casters.Clear();
		LastCulledCasters = PruneCasters(s, P.smallCasterSize, casters);
		SmoothLods(s);
		AjustaLodArena();
	}

	// v18 (thread de física, arenas v4): as células das arenas 10x10 têm um LODGroup por célula com distâncias
	// calculadas para lodBias 1 (LOD0 até 90 m, LOD1 até 200 m, corte logo depois da neblina). Com o bias do Alto e do
	// Ultra (1,8 e 2,4) o Unity estica tudo: a célula fica no LOD0 até ~220 m, o corte vai a ~1,2 km e o ganho de
	// draw calls some. Nas cenas de arena (objeto raiz "Pontos" com o filho "arena_0", que o BuritiroMontaMapa v4 cria)
	// o bias fica em no máximo 1; nos outros mapas continua o do nível de qualidade. v18c: a cena Map_vila_jardim também.
	public static bool ArenaLod { get; private set; }

	// arena: 1 no Alto/Ultra e nunca abaixo de 0,85 (com 0,6 do Baixo do celular as células sumiam ainda dentro da neblina)
	public static float LodBiasAtual => ArenaLod ? Mathf.Clamp(P.lodBias, 0.85f, 1f) : P.lodBias;

	private static void AjustaLodArena()
	{
		bool arena = false;
		for (int i = 0; i < SceneManager.sceneCount && !arena; i++)
		{
			Scene sc = SceneManager.GetSceneAt(i);
			if (!sc.isLoaded)
			{
				continue;
			}
			GameObject[] roots = sc.GetRootGameObjects();
			for (int j = 0; j < roots.Length && !arena; j++)
			{
				if (roots[j].name == "Pontos" && roots[j].transform.Find("arena_0") != null)
				{
					arena = true;
				}
				// v18c (thread da Vila Jardim): a Vila também fica com o bias em no máximo 1. Os móveis de dentro das
				// casas ganharam um LODGroup de 2 níveis (o segundo vazio) e com 2,4 eles continuavam desenhados longe.
				if (roots[j].name == "Map_vila_jardim")
				{
					arena = true;
				}
			}
			if (sc.name == "Map_vila_jardim")
			{
				arena = true;
			}
		}
		if (arena != ArenaLod)
		{
			Debug.Log(arena ? $"[FreeBala][qualidade] arena 10x10 ou Vila Jardim: lodBias {Mathf.Clamp(P.lodBias, 0.85f, 1f):0.0#} (o nível usa {P.lodBias:0.0#})" : $"[FreeBala][qualidade] fora da arena: lodBias {P.lodBias:0.0#}");
		}
		ArenaLod = arena;
		float bias = LodBiasAtual;
		QualitySettings.lodBias = bias;
		QualitySettings.meshLodThreshold = Mathf.Clamp(1.2f / Mathf.Max(0.3f, bias), 0.45f, 2.2f);
	}

	public static void SmoothLods(Scene s)
	{
		if (!s.IsValid() || !s.isLoaded)
		{
			return;
		}
		float lodCutK = LodCutK;
		LODGroup.crossFadeAnimationDuration = 0.5f;
		bool flag = s.name.Contains("praca_aguas");
		GameObject[] rootGameObjects = s.GetRootGameObjects();
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			LODGroup[] componentsInChildren = rootGameObjects[i].GetComponentsInChildren<LODGroup>(includeInactive: true);
			foreach (LODGroup lODGroup in componentsInChildren)
			{
				if ((bool)lODGroup && lODGroup.lodCount <= 1 && (lODGroup.transform.root.name.StartsWith("Map_") || lODGroup.gameObject.layer == 8))
				{
					lODGroup.enabled = false;
				}
				else
				{
					Smooth(lODGroup, flag ? Mathf.Min(lodCutK * 0.4f, 0.002f) : lodCutK);
				}
			}
		}
	}

	public static void Smooth(LODGroup lg, float k = -1f)
	{
		if (!lg)
		{
			return;
		}
		if (lg.lodCount <= 1 && (lg.transform.root.name.StartsWith("Map_") || lg.gameObject.layer == 8))
		{
			lg.enabled = false;
			return;
		}
		if (k < 0f)
		{
			k = LodCutK;
		}
		if (lg.fadeMode == LODFadeMode.None)
		{
			lg.fadeMode = LODFadeMode.CrossFade;
			lg.animateCrossFading = true;
		}
		if (lg.lodCount == 1)
		{
			LOD[] lODs = lg.GetLODs();
			if (!lodOrig.TryGetValue(lg, out var value))
			{
				value = (lodOrig[lg] = lODs[0].screenRelativeTransitionHeight);
			}
			float num = ((k <= 0.005f) ? Mathf.Min(value, k) : (value * k));
			if (Mathf.Abs(lODs[0].screenRelativeTransitionHeight - num) > 1E-06f)
			{
				lODs[0].screenRelativeTransitionHeight = num;
				lg.SetLODs(lODs);
			}
		}
	}

	public static int PruneCasters(Scene s, float minHalfDiag, List<(Renderer, ShadowCastingMode)> undo = null)
	{
		int num = 0;
		if (!s.IsValid() || !s.isLoaded)
		{
			return 0;
		}
		GameObject[] rootGameObjects = s.GetRootGameObjects();
		for (int i = 0; i < rootGameObjects.Length; i++)
		{
			MeshRenderer[] componentsInChildren = rootGameObjects[i].GetComponentsInChildren<MeshRenderer>(includeInactive: true);
			foreach (MeshRenderer meshRenderer in componentsInChildren)
			{
				if (meshRenderer.shadowCastingMode == ShadowCastingMode.On && !meshRenderer.GetComponentInParent<FBCharacter>() && !meshRenderer.GetComponentInParent<FBCar>() && !(meshRenderer.bounds.extents.magnitude >= minHalfDiag))
				{
					undo?.Add((meshRenderer, meshRenderer.shadowCastingMode));
					meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
					num++;
				}
			}
		}
		return num;
	}
}
}
