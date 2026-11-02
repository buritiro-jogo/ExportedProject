using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace FreeBala
{
public class FBScreenshot : MonoBehaviour
{
	private static FBScreenshot instance;

	private bool hudVisible = true;

	private GameObject guides;

	public static string ScreenshotDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Screenshots"));

	public static string ReelDir
	{
		get
		{
			if (!Application.isMobilePlatform)
			{
				return Path.Combine(ScreenshotDir, "Reels");
			}
			return Path.Combine(Application.persistentDataPath, "Reels");
		}
	}

	public static string LastReel { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Init()
	{
		if (!instance)
		{
			GameObject obj = new GameObject("[BURITIRO] ScreenshotSystem");
			instance = obj.AddComponent<FBScreenshot>();
			UnityEngine.Object.DontDestroyOnLoad(obj);
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBScreenshot.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		Keyboard current = Keyboard.current;
		if (current != null)
		{
			if (current.f11Key != null && current.f11Key.wasPressedThisFrame)
			{
				ToggleHUD();
			}
			if (current.f10Key != null && current.f10Key.wasPressedThisFrame)
			{
				CaptureReel(current.shiftKey.isPressed ? 1080 : 2160);
			}
			if (current.f9Key != null && current.f9Key.wasPressedThisFrame)
			{
				ToggleGuides();
			}
			bool num = current.f12Key != null && current.f12Key.wasPressedThisFrame;
			bool flag = current.printScreenKey != null && current.printScreenKey.wasPressedThisFrame;
			if (num || flag)
			{
				bool isPressed = current.shiftKey.isPressed;
				bool isPressed2 = current.ctrlKey.isPressed;
				int superSize = (isPressed ? 4 : 2);
				bool hideUI = isPressed2;
				Capture(superSize, hideUI);
			}
		}
	}

	public void ToggleHUD()
	{
		hudVisible = !hudVisible;
		Canvas[] array = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
		foreach (Canvas canvas in array)
		{
			if ((bool)canvas)
			{
				canvas.enabled = hudVisible;
			}
		}
		Debug.Log("[BURITIRO] Interface (HUD): " + (hudVisible ? "Visível" : "Oculta (Modo Foto)"));
	}

	public static void Capture(int superSize = 2, bool hideUI = false)
	{
		if ((bool)instance)
		{
			instance.StartCoroutine(instance.DoCapture(superSize, hideUI));
			return;
		}
		Directory.CreateDirectory(ScreenshotDir);
		string text = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
		string text2 = ((superSize >= 4) ? "8K" : ((superSize == 2) ? "4K" : "FullHD"));
		string text3 = Path.Combine(ScreenshotDir, "Buritiro_" + text2 + "_" + text + ".png");
		ScreenCapture.CaptureScreenshot(text3, superSize);
		Debug.Log("[BURITIRO] \ud83d\udcf8 Screenshot salva: " + text3);
	}

	public static void CaptureReel(int width = 2160)
	{
		if ((bool)instance)
		{
			instance.StartCoroutine(instance.DoReel(width));
		}
	}

	private IEnumerator DoReel(int width)
	{
		Camera cam = Camera.main;
		if (!cam)
		{
			yield break;
		}
		int H = width * 16 / 9;
		int ss = Mathf.Min(2, Mathf.Max(1, SystemInfo.maxTextureSize / H));
		UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		float lod = QualitySettings.lodBias;
		float sd = (urp ? urp.shadowDistance : 0f);
		int maxLod = QualitySettings.maximumLODLevel;
		int mip = QualitySettings.globalTextureMipmapLimit;
		int sres = (urp ? urp.mainLightShadowmapResolution : 0);
		int casc = (urp ? urp.shadowCascadeCount : 0);
		QualitySettings.lodBias = Mathf.Max(lod, 4f);
		QualitySettings.maximumLODLevel = 0;
		QualitySettings.globalTextureMipmapLimit = 0;
		if ((bool)urp)
		{
			urp.shadowDistance = Mathf.Max(sd, 160f);
			urp.mainLightShadowmapResolution = 4096;
			urp.shadowCascadeCount = 4;
		}
		yield return null;
		yield return null;
		yield return null;
		RenderTexture renderTexture = null;
		RenderTexture renderTexture2 = null;
		Texture2D texture2D = null;
		try
		{
			renderTexture = new RenderTexture(width * ss, H * ss, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
			{
				antiAliasing = 1,
				name = "reel_ssaa"
			};
			renderTexture2 = new RenderTexture(width, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
			{
				name = "reel"
			};
			renderTexture.Create();
			renderTexture2.Create();
			RenderPipeline.StandardRequest data = new RenderPipeline.StandardRequest
			{
				destination = renderTexture
			};
			RenderTexture targetTexture = cam.targetTexture;
			cam.aspect = (float)width / (float)H;
			try
			{
				if (RenderPipeline.SupportsRenderRequest(cam, data))
				{
					RenderPipeline.SubmitRenderRequest(cam, data);
				}
				else
				{
					cam.targetTexture = renderTexture;
					cam.Render();
				}
			}
			finally
			{
				cam.targetTexture = targetTexture;
				cam.ResetAspect();
			}
			renderTexture.filterMode = FilterMode.Bilinear;
			Graphics.Blit(renderTexture, renderTexture2);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = renderTexture2;
			texture2D = new Texture2D(width, H, TextureFormat.RGB24, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, width, H), 0, 0);
			texture2D.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			Directory.CreateDirectory(ReelDir);
			string text = Path.Combine(ReelDir, $"Buritiro_Reels_{width}x{H}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
			File.WriteAllBytes(text, texture2D.EncodeToPNG());
			LastReel = text;
			Debug.Log($"[BURITIRO] \ud83d\udcf8 Reels 9:16 {width}×{H} (SSAA {ss}×) salvo em: {text}");
			FBHUD.I?.Center($"FOTO REELS 9:16 SALVA · {width}×{H}", 2.2f);
		}
		finally
		{
			if ((bool)renderTexture)
			{
				renderTexture.Release();
				UnityEngine.Object.Destroy(renderTexture);
			}
			if ((bool)renderTexture2)
			{
				renderTexture2.Release();
				UnityEngine.Object.Destroy(renderTexture2);
			}
			if ((bool)texture2D)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
			QualitySettings.lodBias = lod;
			QualitySettings.maximumLODLevel = maxLod;
			QualitySettings.globalTextureMipmapLimit = mip;
			if ((bool)urp)
			{
				urp.shadowDistance = sd;
				urp.mainLightShadowmapResolution = sres;
				urp.shadowCascadeCount = casc;
			}
		}
	}

	public void ToggleGuides()
	{
		if ((bool)guides)
		{
			guides.SetActive(!guides.activeSelf);
			return;
		}
		Canvas canvas = FBUI.Canvas("GuiasReels", 900);
		guides = canvas.gameObject;
		UnityEngine.Object.DontDestroyOnLoad(guides);
		GameObject obj = new GameObject("quadro_9x16", typeof(RectTransform), typeof(AspectRatioFitter));
		obj.transform.SetParent(canvas.transform, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)obj.transform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		Vector2 offsetMin = (rectTransform.offsetMax = Vector2.zero);
		rectTransform.offsetMin = offsetMin;
		AspectRatioFitter component = obj.GetComponent<AspectRatioFitter>();
		component.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
		component.aspectRatio = 0.5625f;
		Color c = new Color(0f, 0f, 0f, 0.62f);
		Color c2 = new Color(1f, 1f, 1f, 0.5f);
		(Vector2, Vector2)[] array = new(Vector2, Vector2)[2]
		{
			(new Vector2(-10f, 0f), new Vector2(0f, 1f)),
			(new Vector2(1f, 0f), new Vector2(11f, 1f))
		};
		for (int i = 0; i < array.Length; i++)
		{
			(Vector2, Vector2) tuple = array[i];
			Vector2 item = tuple.Item1;
			Vector2 item2 = tuple.Item2;
			Image image = FBUI.Panel(rectTransform, "fora", item, item2, Vector2.zero, Vector2.zero, c);
			RectTransform rectTransform2 = image.rectTransform;
			offsetMin = (image.rectTransform.offsetMax = Vector2.zero);
			rectTransform2.offsetMin = offsetMin;
			image.raycastTarget = false;
		}
		for (int j = 1; j <= 2; j++)
		{
			FBUI.Panel(rectTransform, "terco_v", new Vector2((float)j / 3f, 0f), new Vector2((float)j / 3f, 1f), Vector2.zero, new Vector2(1.5f, 0f), c2).raycastTarget = false;
			FBUI.Panel(rectTransform, "terco_h", new Vector2(0f, (float)j / 3f), new Vector2(1f, (float)j / 3f), Vector2.zero, new Vector2(0f, 1.5f), c2).raycastTarget = false;
		}
		FBUI.Label(rectTransform, "REELS 9:16 · F10 FOTO 4K · SHIFT+F10 FULL HD · F9 GUIAS", 13, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(560f, 22f), TextAnchor.MiddleCenter, title: true, new Color(1f, 1f, 1f, 0.85f)).raycastTarget = false;
	}

	private IEnumerator DoCapture(int superSize, bool hideUI)
	{
		Canvas[] canvases = null;
		bool[] prevStates = null;
		if (hideUI)
		{
			canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
			prevStates = new bool[canvases.Length];
			for (int i = 0; i < canvases.Length; i++)
			{
				if ((bool)canvases[i])
				{
					prevStates[i] = canvases[i].enabled;
					canvases[i].enabled = false;
				}
			}
		}
		yield return new WaitForEndOfFrame();
		Directory.CreateDirectory(ScreenshotDir);
		string text = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
		string resName = ((superSize >= 4) ? "8K" : ((superSize == 2) ? "4K" : "FullHD"));
		string file = Path.Combine(ScreenshotDir, "Buritiro_" + resName + "_" + text + ".png");
		ScreenCapture.CaptureScreenshot(file, superSize);
		if (hideUI && canvases != null)
		{
			yield return null;
			for (int j = 0; j < canvases.Length; j++)
			{
				if ((bool)canvases[j])
				{
					canvases[j].enabled = prevStates[j];
				}
			}
		}
		Debug.Log("[BURITIRO] \ud83d\udcf8 Screenshot (" + resName + ") salva em: " + file);
	}
}
}
