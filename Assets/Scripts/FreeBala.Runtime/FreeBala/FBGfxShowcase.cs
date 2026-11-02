using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBGfxShowcase : MonoBehaviour
{
	private enum Pose
	{
		Fachada,
		Arvore,
		Sol,
		Lua,
		Poste,
		Rua,
		GramaCanteiro,
		GramaPraca,
		GramaBorda,
		GramaCima,
		GramaPerto,
		Container,
		Fixa,
		Passaros,
		Urubu
	}

	private enum Nivel
	{
		PCAlto,
		Mobile,
		Web,
		PCMedio
	}

	private struct Shot
	{
		public string n;

		public string map;

		public string time;

		public string weather;

		public float duskAt;

		public Pose pose;

		public Nivel nivel;

		public float delay;

		public Vector3 eye;

		public Vector3 tgt;

		public bool plane;
	}

	private static readonly Shot[] Shots = new Shot[60]
	{
		new Shot
		{
			n = "1",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fachada
		},
		new Shot
		{
			n = "2a",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.Arvore
		},
		new Shot
		{
			n = "2b",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.Arvore,
			delay = 1f
		},
		new Shot
		{
			n = "3",
			map = "praca_aguas",
			time = "anoitecer",
			weather = "limpo",
			pose = Pose.Sol,
			duskAt = 40f
		},
		new Shot
		{
			n = "4",
			map = "praca_aguas",
			time = "anoitecer",
			weather = "limpo",
			pose = Pose.Sol,
			duskAt = 240f
		},
		new Shot
		{
			n = "5",
			map = "praca_pt",
			time = "noite",
			weather = "limpo",
			pose = Pose.Lua
		},
		new Shot
		{
			n = "6",
			map = "vila_jardim",
			time = "noite",
			weather = "limpo",
			pose = Pose.Poste
		},
		new Shot
		{
			n = "7",
			map = "orla",
			time = "dia",
			weather = "chuva",
			pose = Pose.Rua
		},
		new Shot
		{
			n = "8m",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fachada,
			nivel = Nivel.Mobile
		},
		new Shot
		{
			n = "8w",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fachada,
			nivel = Nivel.Web
		},
		new Shot
		{
			n = "g1",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaCanteiro
		},
		new Shot
		{
			n = "g2",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca
		},
		new Shot
		{
			n = "g3",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaBorda
		},
		new Shot
		{
			n = "g4",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaCima
		},
		new Shot
		{
			n = "g2m",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca,
			nivel = Nivel.PCMedio
		},
		new Shot
		{
			n = "g2b",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca,
			nivel = Nivel.Mobile
		},
		new Shot
		{
			n = "g5",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaCanteiro
		},
		new Shot
		{
			n = "g6",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca
		},
		new Shot
		{
			n = "g7",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaBorda
		},
		new Shot
		{
			n = "g8",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaCima
		},
		new Shot
		{
			n = "g6m",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca,
			nivel = Nivel.PCMedio
		},
		new Shot
		{
			n = "g6b",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca,
			nivel = Nivel.Mobile
		},
		new Shot
		{
			n = "g9",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPerto
		},
		new Shot
		{
			n = "g10",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPerto
		},
		new Shot
		{
			n = "g11",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Container
		},
		new Shot
		{
			n = "g12",
			map = "vila_jardim",
			time = "dia",
			weather = "limpo",
			pose = Pose.Container
		},
		new Shot
		{
			n = "n1",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Poste
		},
		new Shot
		{
			n = "n2",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.GramaPraca
		},
		new Shot
		{
			n = "n3",
			map = "vila_jardim",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fachada
		},
		new Shot
		{
			n = "p1",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-4f, -23f, 1.8f),
			tgt = new Vector3(30f, -3f, 1.6f)
		},
		new Shot
		{
			n = "p2",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(62f, 1f, 1.9f),
			tgt = new Vector3(46f, 1f, 2.5f)
		},
		new Shot
		{
			n = "p3",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(58f, 9f, 3.2f),
			tgt = new Vector3(22f, 0f, 1.5f)
		},
		new Shot
		{
			n = "p4",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(212f, -14f, 1.7f),
			tgt = new Vector3(240f, 2f, 1.6f)
		},
		new Shot
		{
			n = "p5",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(165f, 30f, 3f),
			tgt = new Vector3(131f, 0f, 7f)
		},
		new Shot
		{
			n = "p6",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-30f, 31f, 1.4f),
			tgt = new Vector3(10f, 8f, 3f)
		},
		new Shot
		{
			n = "p7",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-70f, 0f, 42f),
			tgt = new Vector3(30f, 0f, 0f)
		},
		new Shot
		{
			n = "p8",
			map = "praca_aguas",
			time = "noite",
			weather = "chuva",
			pose = Pose.Fixa,
			eye = new Vector3(70f, 31f, 1.7f),
			tgt = new Vector3(130f, 29f, 1.5f)
		},
		new Shot
		{
			n = "p9",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(100f, 0f, 38f),
			tgt = new Vector3(150f, 0f, 0f)
		},
		new Shot
		{
			n = "p10",
			map = "praca_aguas",
			time = "anoitecer",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-30f, 31f, 1.4f),
			tgt = new Vector3(10f, 8f, 3f),
			duskAt = 200f
		},
		new Shot
		{
			n = "p11",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-6f, -21.5f, 2.4f),
			tgt = new Vector3(2f, -22f, 0.2f)
		},
		new Shot
		{
			n = "p12",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(40f, 0f, 1.7f),
			tgt = new Vector3(75f, 15f, 50f),
			plane = true
		},
		new Shot
		{
			n = "p13",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(30f, 12f, 2.2f),
			tgt = new Vector3(18f, 2f, 0.8f)
		},
		new Shot
		{
			n = "p1m",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-4f, -23f, 1.8f),
			tgt = new Vector3(30f, -3f, 1.6f),
			nivel = Nivel.Mobile
		},
		new Shot
		{
			n = "p2m",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(62f, 1f, 1.9f),
			tgt = new Vector3(46f, 1f, 2.5f),
			nivel = Nivel.Mobile
		},
		new Shot
		{
			n = "e1",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(72f, -37f, 1.7f),
			tgt = new Vector3(100f, -34f, 1.6f)
		},
		new Shot
		{
			n = "e2",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(98f, -37f, 1.7f),
			tgt = new Vector3(82f, -40f, 5.2f)
		},
		new Shot
		{
			n = "e3",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(76f, -39f, 1.7f),
			tgt = new Vector3(59.8f, -45.5f, 2.6f)
		},
		new Shot
		{
			n = "e4",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-24.2f, -9.6f, 1.45f),
			tgt = new Vector3(-24f, -12.7f, 0.75f)
		},
		new Shot
		{
			n = "e5",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(592f, 40f, 3.6f),
			tgt = new Vector3(598f, 15f, 3f)
		},
		new Shot
		{
			n = "e6",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(222f, -15f, 1.6f),
			tgt = new Vector3(232f, -13f, 4.2f)
		},
		new Shot
		{
			n = "e7",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca
		},
		new Shot
		{
			n = "e8",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPerto
		},
		new Shot
		{
			n = "e9",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaCanteiro
		},
		new Shot
		{
			n = "e10",
			map = "praca_aguas",
			time = "dia",
			weather = "chuva",
			pose = Pose.Fixa,
			eye = new Vector3(-35f, 8f, 1.7f),
			tgt = new Vector3(-20f, 4f, 0f)
		},
		new Shot
		{
			n = "e11",
			map = "praca_aguas",
			time = "noite",
			weather = "chuva",
			pose = Pose.Fixa,
			eye = new Vector3(70f, 31f, 1.7f),
			tgt = new Vector3(100f, 30f, 0.2f)
		},
		new Shot
		{
			n = "e12",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Passaros
		},
		new Shot
		{
			n = "e13",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.Urubu
		},
		new Shot
		{
			n = "e14",
			map = "praca_aguas",
			time = "noite",
			weather = "limpo",
			pose = Pose.Fixa,
			eye = new Vector3(-20f, -30f, 1.7f),
			tgt = new Vector3(10f, -24f, 6f)
		},
		new Shot
		{
			n = "e7b",
			map = "praca_aguas",
			time = "dia",
			weather = "limpo",
			pose = Pose.GramaPraca,
			nivel = Nivel.Mobile
		},
		new Shot
		{
			n = "e11b",
			map = "praca_aguas",
			time = "noite",
			weather = "chuva",
			pose = Pose.Fixa,
			eye = new Vector3(70f, 31f, 1.7f),
			tgt = new Vector3(100f, 30f, 0.2f),
			nivel = Nivel.Mobile
		}
	};

	private float secs = 3f;

	private HashSet<string> only;

	private const int MapMask = 256;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbgfx");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_VitrineGrafica");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBGfxShowcase fBGfxShowcase = obj.AddComponent<FBGfxShowcase>();
			if (num + 1 < commandLineArgs.Length && float.TryParse(commandLineArgs[num + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				fBGfxShowcase.secs = Mathf.Max(0.5f, result);
			}
			if (num + 2 < commandLineArgs.Length && !commandLineArgs[num + 2].StartsWith("-"))
			{
				fBGfxShowcase.only = new HashSet<string>(commandLineArgs[num + 2].Split(','));
			}
		}
	}

	private IEnumerator Start()
	{
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		List<string> csv = new List<string> { "print;mapa;hora;clima;nivel;fps_medio;pior_quadro_ms;veg_visiveis;veg_total;arquivo" };
		yield return new WaitForSeconds(3f);
		GameData.Load();
		FBSession.Load();
		Dictionary<string, (string time, string weather)> wx0 = Shots.Select((Shot s) => s.map).Distinct().ToDictionary((string m) => m, FBSession.WeatherFor);
		string map0 = FBSession.map;
		string mode0 = FBSession.mode;
		int q0 = FBQuality.Choice;
		List<Shot> list = Shots.Where((Shot s) => only == null || only.Contains(s.n) || only.Contains(s.n.TrimEnd('a', 'b', 'm', 'w')) || (only.Contains("g") && s.n.StartsWith("g")) || (only.Contains("p") && s.n.StartsWith("p")) || (only.Contains("e") && s.n.StartsWith("e"))).ToList();
		string loaded = null;
		Dictionary<string, (Vector3 p, Quaternion r)> poses = new Dictionary<string, (Vector3, Quaternion)>();
		foreach (Shot s2 in list)
		{
			FBGfx.TierOverride = ((s2.nivel == Nivel.Web) ? ((Func<FBGfxTier>)(() => FBGfxTier.Web)) : null);
			FBQuality.Set((s2.nivel == Nivel.PCAlto) ? 3 : ((s2.nivel == Nivel.PCMedio) ? 2 : 0));
			FBGfx.Refresh();
			string key = $"{s2.map}|{s2.time}|{s2.weather}|{s2.nivel}";
			if (key != loaded)
			{
				FBSession.SetWeather(s2.map, s2.time, s2.weather);
				FBSession.map = s2.map;
				FBSession.mode = "treino";
				FBSession.online = false;
				FBSession.Save();
				Debug.Log($"[FreeBala][gfx] print {s2.n}: carregando {s2.map} ({s2.time}/{s2.weather}, {s2.nivel})");
				AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + s2.map);
				if (op == null)
				{
					Debug.Log("[FreeBala][gfx] FALHA: cena Map_" + s2.map + " não existe");
					continue;
				}
				while (!op.isDone)
				{
					yield return null;
				}
				loaded = key;
				for (float w = 0f; w < 5f; w += Time.unscaledDeltaTime)
				{
					yield return null;
				}
			}
			FBMatch.God = true;
			if (s2.duskAt > 0f)
			{
				FBWeather.SkipDusk(s2.duskAt);
				yield return null;
			}
			if (s2.delay > 0f)
			{
				for (float w = 0f; w < s2.delay; w += Time.unscaledDeltaTime)
				{
					yield return null;
				}
			}
			Camera main = Camera.main;
			GameObject cg = new GameObject("CamVitrine");
			Camera cam = cg.AddComponent<Camera>();
			cam.fieldOfView = 60f;
			cam.nearClipPlane = 0.05f;
			cam.farClipPlane = 1400f;
			cam.cullingMask = (main ? main.cullingMask : (-16385));
			UniversalAdditionalCameraData universalAdditionalCameraData = cg.AddComponent<UniversalAdditionalCameraData>();
			universalAdditionalCameraData.renderPostProcessing = true;
			universalAdditionalCameraData.antialiasing = FBGfx.F.camAA;
			universalAdditionalCameraData.antialiasingQuality = AntialiasingQuality.High;
			if ((bool)main)
			{
				main.enabled = false;
				main.tag = "Untagged";
			}
			cg.tag = "MainCamera";
			RenderTexture rt = (cam.targetTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32)
			{
				antiAliasing = 1
			});
			List<Renderer> hidden = (FBMatch.Player ? (from r in FBMatch.Player.GetComponentsInChildren<Renderer>()
				where r.enabled
				select r).ToList() : new List<Renderer>());
			foreach (Renderer item in hidden)
			{
				item.enabled = false;
			}
			(Vector3 p, Quaternion r) pose;
			if (s2.pose == Pose.Fixa)
			{
				pose = Fixed(s2.map, s2.eye, s2.tgt);
			}
			else
			{
				string map = s2.map;
				Pose pose2 = s2.pose;
				if (!poses.TryGetValue(map + pose2, out pose))
				{
					string map2 = s2.map;
					pose2 = s2.pose;
					string key2 = map2 + pose2;
					(Vector3, Quaternion) value;
					pose = (value = Place(s2.pose));
					poses[key2] = value;
				}
			}
			cg.transform.SetPositionAndRotation(pose.p, pose.r);
			for (int j = 0; j < 10; j++)
			{
				yield return null;
			}
			if (s2.plane)
			{
				FBSkyPlanes.Flyover(pose.p, pose.p.y, true);
				for (float w = 0f; w < 28f; w += Time.unscaledDeltaTime)
				{
					yield return null;
				}
				Vector3? vector = FBSkyPlanes.TrailMid();
				if (vector.HasValue)
				{
					cg.transform.rotation = Quaternion.LookRotation((vector.Value - cg.transform.position).normalized);
				}
				for (int j = 0; j < 5; j++)
				{
					yield return null;
				}
			}
			float t = 0f;
			float worst = 0f;
			int frames = 0;
			while (t < secs)
			{
				yield return null;
				t += Time.unscaledDeltaTime;
				frames++;
				worst = Mathf.Max(worst, Time.unscaledDeltaTime);
			}
			yield return new WaitForEndOfFrame();
			string text = "gfx_" + s2.n + ".png";
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
			texture2D.Apply();
			RenderTexture.active = active;
			File.WriteAllBytes(Path.Combine(dir, text), texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
			float num = (float)frames / Mathf.Max(0.01f, t);
			csv.Add(string.Join(";", s2.n, s2.map, s2.time + ((s2.duskAt > 0f) ? $"+{s2.duskAt:0}s" : ""), s2.weather, s2.nivel, num.ToString("0.0", CultureInfo.InvariantCulture), (worst * 1000f).ToString("0.0", CultureInfo.InvariantCulture), FBVegetation.Visible, FBVegetation.Total, text));
			Debug.Log("[FreeBala][gfx][luz] " + FBWeather.Diag());
			Debug.Log($"[FreeBala][gfx] print {s2.n} {s2.pose} em {pose.p} · {num:0} FPS (pior {worst * 1000f:0} ms) · nível {FBGfx.Tier} · vegetação {FBVegetation.Visible}/{FBVegetation.Total}");
			foreach (Renderer item2 in hidden)
			{
				if ((bool)item2)
				{
					item2.enabled = true;
				}
			}
			cam.targetTexture = null;
			rt.Release();
			UnityEngine.Object.Destroy(rt);
			if ((bool)main)
			{
				main.tag = "MainCamera";
				main.enabled = true;
			}
			UnityEngine.Object.Destroy(cg);
			yield return null;
			File.WriteAllLines(Path.Combine(dir, "gfx.csv"), csv);
			pose = default((Vector3, Quaternion));
		}
		foreach (KeyValuePair<string, (string, string)> item3 in wx0)
		{
			FBSession.SetWeather(item3.Key, item3.Value.Item1, item3.Value.Item2);
		}
		FBSession.map = map0;
		FBSession.mode = mode0;
		FBSession.Save();
		FBGfx.TierOverride = null;
		FBQuality.Set(q0);
		Debug.Log("[FreeBala][gfx] fim");
		Application.Quit(0);
	}

	private static Vector3 Origin()
	{
		return (FBMatch.Player ? FBMatch.Player.Pos : Vector3.zero) + Vector3.up * 1.7f;
	}

	private static bool Ray(Vector3 o, Vector3 d, float far, out RaycastHit h)
	{
		return Physics.Raycast(o, d, out h, far, 256, QueryTriggerInteraction.Ignore);
	}

	private static Vector3 Ground(Vector3 p)
	{
		if (!Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hitInfo, 120f, 256, QueryTriggerInteraction.Ignore))
		{
			return p;
		}
		return hitInfo.point;
	}

	private static bool IsLawn(RaycastHit h)
	{
		Renderer component = h.collider.GetComponent<Renderer>();
		return Regex.IsMatch(((bool)component && (bool)component.sharedMaterial) ? component.sharedMaterial.name : "", "grass|grama|lawn|gramado|canteiro|jardim", RegexOptions.IgnoreCase);
	}

	private static Vector3 Yaw(float deg)
	{
		return new Vector3(Mathf.Sin(deg * ((float)Math.PI / 180f)), 0f, Mathf.Cos(deg * ((float)Math.PI / 180f)));
	}

	private static Quaternion Look(Vector3 flat, float pitch)
	{
		return Quaternion.LookRotation(Quaternion.AngleAxis(0f - pitch, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized);
	}

	private static Light Sun()
	{
		if (!RenderSettings.sun)
		{
			return UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault((Light l) => l.type == LightType.Directional);
		}
		return RenderSettings.sun;
	}

	private static (Vector3 p, Quaternion r) Fixed(string map, Vector3 eye, Vector3 tgt)
	{
		GameObject root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault((GameObject g) => g.name == "Map_" + map);
		Vector3 vector = W(eye);
		Vector3 vector2 = W(tgt);
		return (p: vector, r: Quaternion.LookRotation((vector2 - vector).normalized));
		Vector3 W(Vector3 v)
		{
			Vector3 vector3 = new Vector3(-1.2f * v.x, 1.2f * v.z, 1.2f * v.y);
			if (!root)
			{
				return vector3;
			}
			return root.transform.TransformPoint(vector3);
		}
	}

	private static (Vector3 p, Quaternion r) Place(Pose pose)
	{
		Vector3 o = Origin();
		switch (pose)
		{
		case Pose.Fachada:
		{
			float num8 = 1E+09f;
			Vector3 flat4 = Vector3.forward;
			for (int num9 = 0; num9 < 360; num9 += 15)
			{
				Vector3 vector14 = Yaw(num9);
				if (Ray(o, vector14, 60f, out var h4) && Mathf.Abs(h4.normal.y) < 0.3f && h4.distance > 9f)
				{
					float num10 = Mathf.Abs(h4.distance - 18f);
					if (num10 < num8)
					{
						num8 = num10;
						flat4 = vector14;
					}
				}
			}
			return (p: o, r: Look(flat4, 9f));
		}
		case Pose.Arvore:
		{
			Transform transform = (from t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
				where t.name.StartsWith("VJ_Arvore") || t.name.Contains("Arvore")
				orderby (t.position - o).sqrMagnitude
				select t).FirstOrDefault();
			if (!transform)
			{
				return (p: o, r: Look(Vector3.forward, 10f));
			}
			Vector3 vector22 = o - transform.position;
			vector22.y = 0f;
			if (vector22.sqrMagnitude < 0.01f)
			{
				vector22 = Vector3.back;
			}
			Vector3 vector23 = Ground(transform.position + vector22.normalized * 7f) + Vector3.up * 1.7f;
			Vector3 vector24 = transform.position + Vector3.up * 3.5f;
			return (p: vector23, r: Quaternion.LookRotation(vector24 - vector23));
		}
		case Pose.Sol:
		case Pose.Lua:
		{
			Light light = Sun();
			Vector3 vector6 = (light ? (-light.transform.forward) : Vector3.forward);
			Vector3 flat2 = new Vector3(vector6.x, 0f, vector6.z);
			if (flat2.sqrMagnitude < 0.0001f)
			{
				flat2 = Vector3.forward;
			}
			float pitch = ((pose == Pose.Sol) ? 10f : Mathf.Clamp(Mathf.Asin(Mathf.Clamp(vector6.y, -1f, 1f)) * 57.29578f, 20f, 50f));
			Quaternion quaternion = Look(flat2, pitch);
			Vector3 d = quaternion * Vector3.forward;
			for (float num3 = 0f; num3 <= 48f; num3 += 8f)
			{
				for (int j = 0; j < 360; j += ((num3 == 0f) ? 360 : 45))
				{
					Vector3 vector7 = Ground(o + Yaw(j) * num3) + Vector3.up * 1.7f;
					if (!Ray(vector7, d, 90f, out var h2) && !Ray(vector7, Vector3.up, 2.5f, out h2))
					{
						return (p: vector7, r: quaternion);
					}
				}
			}
			return (p: o, r: quaternion);
		}
		case Pose.Poste:
		{
			IReadOnlyList<Vector3> lamps = FBWeather.Lamps;
			if (lamps == null || lamps.Count == 0)
			{
				return (p: o, r: Look(Vector3.forward, 5f));
			}
			Vector3 vector2 = lamps.OrderBy((Vector3 l) => (l - o).sqrMagnitude).First();
			Vector3 vector3 = o - vector2;
			vector3.y = 0f;
			if (vector3.sqrMagnitude < 0.01f)
			{
				vector3 = Vector3.back;
			}
			Vector3 vector4 = Ground(vector2 + vector3.normalized * 4.5f) + Vector3.up * 1.7f;
			Vector3 vector5 = vector2 + Vector3.down * 1.4f;
			return (p: vector4, r: Quaternion.LookRotation(vector5 - vector4));
		}
		case Pose.GramaPerto:
		{
			FBVegData fBVegData2 = (FBVegetation.I ? FBVegetation.I.data : null);
			List<Vector3> list2 = (fBVegData2 ? fBVegData2.openSpots.Concat(fBVegData2.edgeSpots).ToList() : null);
			if (list2 == null || list2.Count == 0)
			{
				return (p: o, r: Look(Vector3.forward, -20f));
			}
			Vector3 vector19 = list2.OrderBy((Vector3 q) => (q - o).sqrMagnitude).First();
			Vector3 vector20 = Yaw(20f);
			Vector3 vector21 = vector19 - vector20 * 0.9f + Vector3.up * 0.35f;
			return (p: vector21, r: Quaternion.LookRotation(vector19 + Vector3.up * 0.12f + vector20 * 0.3f - vector21));
		}
		case Pose.Container:
		{
			MeshRenderer meshRenderer = (from x in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
				where x.sharedMaterials.Any((Material m) => (bool)m && Regex.IsMatch(m.name, "Ft_Generic|Ft_Var|container|conteiner|galpao_metal|chapa", RegexOptions.IgnoreCase))
				orderby (x.bounds.center - o).sqrMagnitude
				select x).FirstOrDefault();
			if (!meshRenderer)
			{
				return (p: o, r: Look(Vector3.forward, 0f));
			}
			Vector3 center = meshRenderer.bounds.center;
			Vector3 vector17 = o - center;
			vector17.y = 0f;
			vector17 = ((vector17.sqrMagnitude > 0.01f) ? vector17.normalized : Vector3.back);
			vector17 = Quaternion.Euler(0f, 35f, 0f) * vector17;
			Vector3 vector18 = Ground(center + vector17 * (meshRenderer.bounds.extents.magnitude + 5f)) + Vector3.up * 1.7f;
			return (p: vector18, r: Quaternion.LookRotation(center - vector18));
		}
		case Pose.Passaros:
		{
			Vector3? vector25 = (FBBirds.I ? FBBirds.I.GroundFlock(o) : null);
			if (!vector25.HasValue)
			{
				return (p: o, r: Look(Vector3.forward, -5f));
			}
			Vector3 vector26 = o - vector25.Value;
			vector26.y = 0f;
			vector26 = ((vector26.sqrMagnitude > 0.01f) ? vector26.normalized : Vector3.back);
			Vector3 vector27 = Ground(vector25.Value + vector26 * 6.5f) + Vector3.up * 1.1f;
			return (p: vector27, r: Quaternion.LookRotation(vector25.Value + Vector3.up * 0.2f - vector27));
		}
		case Pose.Urubu:
		{
			Vector3? vector15 = (FBBirds.I ? FBBirds.I.VulturePos() : null);
			if (!vector15.HasValue)
			{
				return (p: o, r: Look(Vector3.forward, 40f));
			}
			Vector3 vector16 = vector15.Value + new Vector3(-9f, 2.5f, -9f);
			return (p: vector16, r: Quaternion.LookRotation(vector15.Value - vector16));
		}
		case Pose.GramaCanteiro:
		case Pose.GramaPraca:
		case Pose.GramaBorda:
		case Pose.GramaCima:
		{
			FBVegData fBVegData = (FBVegetation.I ? FBVegetation.I.data : null);
			bool flag = pose == Pose.GramaCanteiro || pose == Pose.GramaBorda;
			List<Vector3> list = ((!fBVegData) ? null : (flag ? fBVegData.edgeSpots : fBVegData.openSpots));
			if (list == null || list.Count == 0)
			{
				return (p: o, r: Look(Vector3.forward, -10f));
			}
			Vector3 vector8 = list.OrderBy((Vector3 q) => (q - o).sqrMagnitude).First();
			switch (pose)
			{
			case Pose.GramaCima:
			{
				Vector3 vector13 = Yaw(35f);
				return (p: vector8 + Vector3.up * 14f - vector13 * 8f, r: Look(vector13, -60f));
			}
			case Pose.GramaPraca:
			{
				float num6 = -1f;
				Vector3 flat3 = Vector3.forward;
				for (int n = 0; n < 360; n += 20)
				{
					Vector3 vector12 = Yaw(n);
					RaycastHit h3;
					float num7 = (Ray(vector8 + Vector3.up * 1.7f, vector12, 200f, out h3) ? h3.distance : 200f);
					if (num7 > num6)
					{
						num6 = num7;
						flat3 = vector12;
					}
				}
				return (p: vector8 + Vector3.up * 1.7f, r: Look(flat3, -12f));
			}
			default:
			{
				Vector3 vector9 = Vector3.forward;
				for (int k = 0; k < 360; k += 45)
				{
					Vector3 vector10 = Yaw(k);
					if (Physics.Raycast(vector8 + vector10 * 1.2f + Vector3.up * 5f, Vector3.down, out var hitInfo, 10f, 256, QueryTriggerInteraction.Ignore) && !IsLawn(hitInfo))
					{
						vector9 = vector10;
						break;
					}
				}
				float num4 = ((pose == Pose.GramaCanteiro) ? 4.5f : 2.2f);
				float num5 = ((pose == Pose.GramaCanteiro) ? 1.7f : 0.9f);
				Vector3 vector11 = Ground(vector8 + vector9 * num4) + Vector3.up * num5;
				return (p: vector11, r: Quaternion.LookRotation(vector8 + Vector3.up * 0.25f - vector11));
			}
			}
		}
		default:
		{
			float num = -1f;
			Vector3 flat = Vector3.forward;
			for (int i = 0; i < 360; i += 10)
			{
				Vector3 vector = Yaw(i);
				RaycastHit h;
				float num2 = (Ray(o, vector, 300f, out h) ? h.distance : 300f);
				if (num2 > num)
				{
					num = num2;
					flat = vector;
				}
			}
			return (p: o, r: Look(flat, -3f));
		}
		}
	}
}
}
