using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public static class FBGfx
{
	public static Func<FBGfxTier> TierOverride;

	public static Func<FBGfxTier, FBGfxFlags> FlagsOverride;

	private static bool hooked;

	public static FBGfxTier Tier { get; private set; } = FBGfxTier.PCAlto;


	public static FBGfxFlags F { get; private set; } = Defaults(FBGfxTier.PCAlto);


	public static event Action Changed;

	public static FBGfxFlags Defaults(FBGfxTier t)
	{
		switch (t)
		{
		case FBGfxTier.Mobile:
		{
			FBGfxFlags result = default(FBGfxFlags);
			result.skyLite = true;
			result.wind = true;
			result.lampInsects = 2;
			result.dustMotes = 0;
			result.smokePlumes = 1;
			result.camAA = AntialiasingMode.None;
			result.grass = true;
			result.grassNear = 8f;
			result.grassFar = 17f;
			result.grassNearSpacing = 0.36f;
			result.grassFarSpacing = 0.7f;
			result.grassDensity = 0.85f;
			return result;
		}
		case FBGfxTier.Web:
		{
			FBGfxFlags result = default(FBGfxFlags);
			result.skyLite = true;
			result.wind = true;
			result.cloudSprites = true;
			result.lampInsects = 3;
			result.dustMotes = 40;
			result.smokePlumes = 1;
			result.camAA = AntialiasingMode.FastApproximateAntialiasing;
			result.grass = true;
			result.grassNear = 8f;
			result.grassFar = 18f;
			result.grassNearSpacing = 0.36f;
			result.grassFarSpacing = 0.7f;
			result.grassDensity = 0.85f;
			return result;
		}
		case FBGfxTier.PCMedio:
		{
			FBGfxFlags result = default(FBGfxFlags);
			result.ssao = true;
			result.heightFog = true;
			result.detailMaps = true;
			result.wind = true;
			result.cloudSprites = true;
			result.volumetric = true;
			result.volSteps = 14;
			result.volScale = 0.4f;
			result.lampInsects = 6;
			result.dustMotes = 90;
			result.smokePlumes = 3;
			result.camAA = AntialiasingMode.None;
			result.grass = true;
			result.grassNear = 11f;
			result.grassFar = 26f;
			result.grassNearSpacing = 0.3f;
			result.grassFarSpacing = 0.6f;
			result.grassDensity = 1f;
			return result;
		}
		default:
		{
			FBGfxFlags result = default(FBGfxFlags);
			result.ssao = true;
			result.heightFog = true;
			result.volClouds = false;
			result.detailMaps = true;
			result.wind = true;
			result.volumetric = true;
			result.volSteps = 26;
			result.volScale = 0.5f;
			result.ssr = true;
			result.lampInsects = 8;
			result.dustMotes = 140;
			result.smokePlumes = 4;
			result.camAA = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
			result.grass = true;
			result.grassNear = 14f;
			result.grassFar = 34f;
			result.grassNearSpacing = 0.27f;
			result.grassFarSpacing = 0.55f;
			result.grassDensity = 1f;
			return result;
		}
		}
	}

	private static FBGfxTier Detect()
	{
		if (TierOverride != null)
		{
			return TierOverride();
		}
		if (Application.platform == RuntimePlatform.WebGLPlayer)
		{
			return FBGfxTier.Web;
		}
		int qualityLevel = QualitySettings.GetQualityLevel();
		string[] names = QualitySettings.names;
		string text = ((qualityLevel >= 0 && qualityLevel < names.Length) ? names[qualityLevel].ToLowerInvariant() : "");
		if (text.Contains("web"))
		{
			return FBGfxTier.Web;
		}
		if (text.Contains("mobile") || text.Contains("baixo"))
		{
			return FBGfxTier.Mobile;
		}
		if (text.Contains("médio") || text.Contains("medio"))
		{
			return FBGfxTier.PCMedio;
		}
		if (text.Contains("alto"))
		{
			return FBGfxTier.PCAlto;
		}
		if (!Application.isMobilePlatform)
		{
			return FBGfxTier.PCAlto;
		}
		return FBGfxTier.Mobile;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (!hooked)
		{
			hooked = true;
			QualitySettings.activeQualityLevelChanged += delegate
			{
				Refresh();
			};
			SceneManager.sceneLoaded += delegate
			{
				Refresh();
			};
			RenderPipelineManager.beginCameraRendering += OnCam;
		}
		SetWind(new Vector2(0.8f, 0.6f), 1f);
		Refresh();
	}

	public static void Refresh()
	{
		Tier = Detect();
		FBGfxFlags fBGfxFlags2 = (F = ((FlagsOverride != null) ? FlagsOverride(Tier) : Defaults(Tier)));
		if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset a)
		{
			foreach (ScriptableRendererData item in Renderers(a))
			{
				foreach (ScriptableRendererFeature rendererFeature in item.rendererFeatures)
				{
					if ((bool)rendererFeature)
					{
						if (rendererFeature is ScreenSpaceAmbientOcclusion)
						{
							rendererFeature.SetActive(fBGfxFlags2.ssao);
						}
						else if (rendererFeature is FBHeightFogFeature)
						{
							rendererFeature.SetActive(fBGfxFlags2.heightFog);
						}
						else if (rendererFeature is FBVolumetricFeature || rendererFeature is VolumetricFogRendererFeature)
						{
							// Correção: o shader Hidden/VolumetricFog veio vazio na reconstrução e deixa a câmera toda preta.
							// A névoa volumétrica (VolumetricFogRendererFeature) fica desligada até o shader original voltar.
							rendererFeature.SetActive(fBGfxFlags2.volumetric);
						}
						else if (rendererFeature is ScreenSpaceReflectionRendererFeature)
						{
							rendererFeature.SetActive(fBGfxFlags2.ssr);
						}
					}
				}
			}
		}
		FBVolumetric.Active = fBGfxFlags2.volumetric;
		FBVolumetric.Steps = Mathf.Max(8, fBGfxFlags2.volSteps);
		FBVolumetric.ResolutionScale = ((fBGfxFlags2.volScale > 0f) ? fBGfxFlags2.volScale : 0.5f);
		FBGfx.Changed?.Invoke();
	}

	private static IEnumerable<ScriptableRendererData> Renderers(UniversalRenderPipelineAsset a)
	{
		if (!(typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(a) is ScriptableRendererData[] array))
		{
			yield break;
		}
		ScriptableRendererData[] array2 = array;
		foreach (ScriptableRendererData scriptableRendererData in array2)
		{
			if ((bool)scriptableRendererData)
			{
				yield return scriptableRendererData;
			}
		}
	}

	private static void OnCam(ScriptableRenderContext ctx, Camera cam)
	{
		if (!cam || cam.cameraType != CameraType.Game || cam != Camera.main)
		{
			return;
		}
		UniversalAdditionalCameraData universalAdditionalCameraData = cam.GetUniversalAdditionalCameraData();
		if ((bool)universalAdditionalCameraData && universalAdditionalCameraData.renderType == CameraRenderType.Base)
		{
			if (universalAdditionalCameraData.antialiasing != F.camAA)
			{
				universalAdditionalCameraData.antialiasing = F.camAA;
				universalAdditionalCameraData.antialiasingQuality = AntialiasingQuality.High;
			}
			if (!universalAdditionalCameraData.renderPostProcessing)
			{
				universalAdditionalCameraData.renderPostProcessing = true;
			}
		}
	}

	public static void SetWind(Vector2 dir, float strength, float groundY = 0f)
	{
		Shader.SetGlobalFloat("_FBWindGround", groundY);
		dir = ((dir.sqrMagnitude > 0.0001f) ? dir.normalized : Vector2.right);
		float num = (F.wind ? strength : 0f);
		Shader.SetGlobalVector("_FBWind", new Vector4(dir.x, dir.y, num, (num > 0f) ? Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(strength - 0.5f)) : 0f));
	}
}
}
