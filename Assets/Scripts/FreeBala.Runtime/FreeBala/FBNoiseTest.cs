using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBNoiseTest : MonoBehaviour
{
	private string map = "vila_jardim";

	private string mode = "tatico";

	private string dir;

	private int level = -1;

	private readonly StringBuilder csv = new StringBuilder();

	private static UniversalRenderPipelineAsset URP => GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbnoise");
		if (num < 0)
		{
			return;
		}
		GameObject obj = new GameObject("FB_Ruido");
		UnityEngine.Object.DontDestroyOnLoad(obj);
		FBNoiseTest fBNoiseTest = obj.AddComponent<FBNoiseTest>();
		if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
		{
			string[] array = commandLineArgs[num + 1].Split(':');
			fBNoiseTest.map = array[0];
			if (array.Length > 1)
			{
				fBNoiseTest.mode = array[1];
			}
		}
		int num2 = Array.IndexOf(commandLineArgs, "-fbq");
		if (num2 >= 0 && num2 + 1 < commandLineArgs.Length && int.TryParse(commandLineArgs[num2 + 1], out var result))
		{
			fBNoiseTest.level = result;
		}
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		csv.AppendLine("vista;fator;ruido_espacial;ruido_temporal;tela;render_px;render_scale;msaa;hdr;ssao;cam_aa;nivel;asset_urp");
		yield return new WaitForSeconds(3f);
		if (level >= 0)
		{
			FBQuality.Set(level);
		}
		GameData.Load();
		FBSession.Load();
		FBSession.map = map;
		FBSession.mode = mode;
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + map);
		if (op == null)
		{
			Debug.Log("[FreeBala][ruido] cena não existe: " + map);
			Application.Quit(1);
			yield break;
		}
		while (!op.isDone)
		{
			yield return null;
		}
		FBMatch.God = true;
		for (float w = 0f; w < 6f; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		Time.timeScale = 0f;
		Camera main = Camera.main;
		FBCamPose pose = base.gameObject.AddComponent<FBCamPose>();
		Pose? item = null;
		if ((bool)main && Physics.Raycast(main.transform.position, main.transform.forward, out var hitInfo, 60f, FBWorld.SolidMask))
		{
			item = new Pose(hitInfo.point + hitInfo.normal * 1f, Quaternion.LookRotation(-hitInfo.normal, Vector3.up));
		}
		List<(string, Pose?)> list = new List<(string, Pose?)> { ("jogo", null) };
		if (item.HasValue)
		{
			list.Add(("perto", item));
		}
		foreach (var item3 in list)
		{
			string vn = item3.Item1;
			Pose? item2 = item3.Item2;
			pose.pose = item2;
			foreach (var f in Factors())
			{
				f.on();
				yield return Shot(vn, f.name);
				f.off();
			}
		}
		pose.pose = null;
		yield return Resolutions();
		File.WriteAllText(Path.Combine(dir, "noise.csv"), csv.ToString());
		Time.timeScale = 1f;
		FBMatch.God = false;
		Debug.Log("[FreeBala][ruido] fim → " + dir);
		Application.Quit(0);
	}

	private static (string name, Action on, Action off) Gfx(string name, Func<FBGfxFlags, FBGfxFlags> mod)
	{
		Func<FBGfxTier, FBGfxFlags> keep = null;
		return (name: name, on: delegate
		{
			keep = FBGfx.FlagsOverride;
			Func<FBGfxTier, FBGfxFlags> i = keep;
			FBGfx.FlagsOverride = (FBGfxTier t) => mod((i != null) ? i(t) : FBGfx.Defaults(t));
			FBGfx.Refresh();
		}, off: delegate
		{
			FBGfx.FlagsOverride = keep;
			FBGfx.Refresh();
		});
	}

	private static (string name, Action on, Action off) Prop<T>(string name, Func<T> get, Action<T> set, T value)
	{
		T old = default(T);
		return (name: name, on: delegate
		{
			old = get();
			set(value);
		}, off: delegate
		{
			set(old);
		});
	}

	private static (string name, Action on, Action off) Post<T>(string name) where T : VolumeComponent
	{
		List<VolumeComponent> comps = new List<VolumeComponent>();
		return (name: name, on: delegate
		{
			comps.Clear();
			foreach (VolumeProfile item in Profiles())
			{
				if (item.TryGet<T>(out var component) && component.active)
				{
					component.active = false;
					comps.Add(component);
				}
			}
		}, off: delegate
		{
			foreach (VolumeComponent item2 in comps)
			{
				item2.active = true;
			}
		});
	}

	private static IEnumerable<VolumeProfile> Profiles()
	{
		UniversalRenderPipelineAsset uRP = URP;
		if ((bool)uRP && (bool)uRP.volumeProfile)
		{
			yield return uRP.volumeProfile;
		}
		Volume[] array = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
		foreach (Volume volume in array)
		{
			if ((bool)volume && volume.isGlobal && (bool)volume.sharedProfile)
			{
				yield return volume.sharedProfile;
			}
		}
	}

	private IEnumerable<(string name, Action on, Action off)> Factors()
	{
		UniversalRenderPipelineAsset u = URP;
		Light sun = RenderSettings.sun;
		yield return (name: "base", on: delegate
		{
		}, off: delegate
		{
		});
		yield return Gfx("sem_ssao", delegate(FBGfxFlags f)
		{
			f.ssao = false;
			return f;
		});
		if ((bool)u)
		{
			yield return Prop("sem_msaa", () => u.msaaSampleCount, delegate(int v)
			{
				u.msaaSampleCount = v;
			}, 1);
		}
		yield return Gfx("aa_nenhum", delegate(FBGfxFlags f)
		{
			f.camAA = AntialiasingMode.None;
			return f;
		});
		yield return Gfx("aa_fxaa", delegate(FBGfxFlags f)
		{
			f.camAA = AntialiasingMode.FastApproximateAntialiasing;
			return f;
		});
		yield return Gfx("aa_smaa", delegate(FBGfxFlags f)
		{
			f.camAA = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
			return f;
		});
		(string name, Action on, Action off)[] allPost = new(string, Action, Action)[7]
		{
			Post<Bloom>("x"),
			Post<Vignette>("x"),
			Post<Tonemapping>("x"),
			Post<ColorAdjustments>("x"),
			Post<WhiteBalance>("x"),
			Post<FilmGrain>("x"),
			Post<ChromaticAberration>("x")
		};
		yield return (name: "sem_pos", on: delegate
		{
			(string, Action, Action)[] array2 = allPost;
			for (int j = 0; j < array2.Length; j++)
			{
				array2[j].Item2();
			}
		}, off: delegate
		{
			(string, Action, Action)[] array = allPost;
			for (int i = 0; i < array.Length; i++)
			{
				array[i].Item3();
			}
		});
		yield return Post<Bloom>("sem_bloom");
		yield return Post<Vignette>("sem_vinheta");
		yield return Post<Tonemapping>("sem_tonemap");
		yield return Post<FilmGrain>("sem_grao");
		yield return Post<ChromaticAberration>("sem_aberracao");
		if ((bool)u)
		{
			yield return Prop("sem_sombra", () => u.shadowDistance, delegate(float v)
			{
				u.shadowDistance = v;
			}, 0f);
		}
		if ((bool)sun)
		{
			yield return Prop("sombra_dura", () => sun.shadows, delegate(LightShadows v)
			{
				sun.shadows = v;
			}, LightShadows.Hard);
		}
		yield return Gfx("sem_nevoa_altura", delegate(FBGfxFlags f)
		{
			f.heightFog = false;
			return f;
		});
		yield return Gfx("sem_nuvens_vol", delegate(FBGfxFlags f)
		{
			f.volClouds = false;
			return f;
		});
		if ((bool)u)
		{
			yield return Prop("escala_100", () => u.renderScale, delegate(float v)
			{
				u.renderScale = v;
			}, 1f);
			yield return Prop("escala_085", () => u.renderScale, delegate(float v)
			{
				u.renderScale = v;
			}, 0.85f);
			yield return Prop("escala_070", () => u.renderScale, delegate(float v)
			{
				u.renderScale = v;
			}, 0.7f);
		}
		yield return Prop("sem_mip_streaming", () => QualitySettings.streamingMipmapsActive, delegate(bool v)
		{
			QualitySettings.streamingMipmapsActive = v;
		}, value: false);
		yield return Prop("mip_meia", () => QualitySettings.globalTextureMipmapLimit, delegate(int v)
		{
			QualitySettings.globalTextureMipmapLimit = v;
		}, 1);
		yield return Prop("aniso_desligado", () => QualitySettings.anisotropicFiltering, delegate(AnisotropicFiltering v)
		{
			QualitySettings.anisotropicFiltering = v;
		}, AnisotropicFiltering.Disable);
		if ((bool)u)
		{
			yield return Prop("sem_hdr", () => u.supportsHDR, delegate(bool v)
			{
				u.supportsHDR = v;
			}, value: false);
		}
		yield return Prop("sem_crossfade", () => QualitySettings.enableLODCrossFade, delegate(bool v)
		{
			QualitySettings.enableLODCrossFade = v;
		}, value: false);
	}

	private IEnumerator Shot(string view, string factor, string extra = null)
	{
		for (int i = 0; i < 6; i++)
		{
			yield return null;
		}
		yield return new WaitForEndOfFrame();
		Texture2D a = ScreenCapture.CaptureScreenshotAsTexture();
		yield return null;
		yield return new WaitForEndOfFrame();
		Texture2D texture2D = ScreenCapture.CaptureScreenshotAsTexture();
		int w = a.width;
		int height = a.height;
		int num = Mathf.Min(320, w);
		int num2 = Mathf.Min(180, height);
		int num3 = (w - num) / 2;
		int num4 = (height - num2) / 2;
		Color32[] pixels = a.GetPixels32();
		Color32[] pixels2 = texture2D.GetPixels32();
		double num5 = 0.0;
		double num6 = 0.0;
		int num7 = 0;
		for (int j = num4 + 1; j < num4 + num2 - 1; j++)
		{
			for (int k = num3 + 1; k < num3 + num - 1; k++)
			{
				num5 += (double)Mathf.Abs(4f * L(pixels, k, j) - L(pixels, k - 1, j) - L(pixels, k + 1, j) - L(pixels, k, j - 1) - L(pixels, k, j + 1));
				num7++;
			}
		}
		for (int l = 0; l < height; l += 2)
		{
			for (int m = 0; m < w; m += 2)
			{
				num6 += (double)Mathf.Abs(L(pixels, m, l) - L(pixels2, m, l));
			}
		}
		num6 /= (double)(w / 2 * (height / 2));
		num5 /= (double)Math.Max(1, num7);
		string text = "noise_" + view + "_" + factor;
		File.WriteAllBytes(Path.Combine(dir, text + ".png"), a.EncodeToPNG());
		Texture2D texture2D2 = new Texture2D(num * 4, num2 * 4, TextureFormat.RGB24, mipChain: false);
		Color32[] array = new Color32[num * 4 * num2 * 4];
		for (int n = 0; n < num2 * 4; n++)
		{
			for (int num8 = 0; num8 < num * 4; num8++)
			{
				array[n * num * 4 + num8] = pixels[(num4 + n / 4) * w + num3 + num8 / 4];
			}
		}
		texture2D2.SetPixels32(array);
		texture2D2.Apply();
		File.WriteAllBytes(Path.Combine(dir, text + "_x4.png"), texture2D2.EncodeToPNG());
		UnityEngine.Object.Destroy(a);
		UnityEngine.Object.Destroy(texture2D);
		UnityEngine.Object.Destroy(texture2D2);
		Camera main = Camera.main;
		UniversalRenderPipelineAsset uRP = URP;
		UniversalAdditionalCameraData universalAdditionalCameraData = (main ? main.GetUniversalAdditionalCameraData() : null);
		float num9 = (uRP ? uRP.renderScale : 1f);
		string text2 = (main ? $"{Mathf.RoundToInt((float)main.pixelWidth * num9)}x{Mathf.RoundToInt((float)main.pixelHeight * num9)}" : "-");
		string text3 = string.Join(";", view, factor, num5.ToString("0.00", CultureInfo.InvariantCulture), num6.ToString("0.000", CultureInfo.InvariantCulture), $"{Screen.width}x{Screen.height}", text2, num9.ToString("0.00", CultureInfo.InvariantCulture), uRP ? uRP.msaaSampleCount : 0, ((bool)uRP && uRP.supportsHDR) ? 1 : 0, FBGfx.F.ssao ? 1 : 0, universalAdditionalCameraData ? universalAdditionalCameraData.antialiasing.ToString() : "-", FBQuality.Names[(int)FBQuality.Level], uRP ? uRP.name : "-") + ((extra != null) ? (";" + extra) : "");
		csv.AppendLine(text3);
		Debug.Log("[FreeBala][ruido] " + text3);
		float L(Color32[] p, int x, int y)
		{
			Color32 color = p[y * w + x];
			return 0.2126f * (float)(int)color.r + 0.7152f * (float)(int)color.g + 0.0722f * (float)(int)color.b;
		}
	}

	private IEnumerator Resolutions()
	{
		int w2 = Screen.width;
		int h2 = Screen.height;
		FullScreenMode fs = Screen.fullScreenMode;
		(int, int, string)[] array = new(int, int, string)[5]
		{
			(1280, 720, "720p"),
			(1920, 1080, "1080p"),
			(2560, 1440, "1440p"),
			(2560, 1080, "21x9"),
			(1080, 1920, "retrato")
		};
		for (int j = 0; j < array.Length; j++)
		{
			var (w, h, tag) = array[j];
			Screen.SetResolution(w, h, FullScreenMode.Windowed);
			for (int i = 0; i < 30; i++)
			{
				yield return null;
			}
			float num = (from c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
				where c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay
				select c.scaleFactor).DefaultIfEmpty(1f).Max();
			yield return Shot("res_" + tag, $"{w}x{h}", $"pedido {w}x{h} · obtido {Screen.width}x{Screen.height} · escala UI {num:0.00}");
		}
		Screen.SetResolution(w2, h2, fs);
		for (int j = 0; j < 10; j++)
		{
			yield return null;
		}
	}
}
}
