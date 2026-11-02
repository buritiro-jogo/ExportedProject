using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBAutoTest : MonoBehaviour
{
	private readonly List<(string map, string mode)> jobs = new List<(string, string)>();

	private float secs = 15f;

	private bool show;

	private int[] levels = new int[1] { -1 };

	private string[] sims = new string[1] { "" };

	private string[] skins = new string[1] { "" };

	private int errors;

	private int vegAvg;

	private int vegPeak;

	private static readonly Dictionary<string, (int n, int lo, int hi, double sum, double g)> Loco = new Dictionary<string, (int, int, int, double, double)>();

	private static readonly List<AnimatorClipInfo> ci = new List<AnimatorClipInfo>();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbauto");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			GameObject obj = new GameObject("FB_AutoTeste");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBAutoTest fBAutoTest = obj.AddComponent<FBAutoTest>();
			string[] array = commandLineArgs[num + 1].Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(':');
				fBAutoTest.jobs.Add((array2[0], (array2.Length > 1) ? array2[1] : "tatico"));
			}
			fBAutoTest.show = Array.IndexOf(commandLineArgs, "-fbshow") >= 0;
			int num2 = Array.IndexOf(commandLineArgs, "-fbskin");
			if (num2 >= 0 && num2 + 1 < commandLineArgs.Length)
			{
				fBAutoTest.skins = commandLineArgs[num2 + 1].Split(',');
			}
			int num3 = Array.IndexOf(commandLineArgs, "-fbsim");
			if (num3 >= 0 && num3 + 1 < commandLineArgs.Length)
			{
				fBAutoTest.sims = commandLineArgs[num3 + 1].Split(',');
			}
			int num4 = Array.IndexOf(commandLineArgs, "-fbq");
			if (num4 >= 0 && num4 + 1 < commandLineArgs.Length)
			{
				fBAutoTest.levels = ((!(commandLineArgs[num4 + 1] == "all")) ? ((!int.TryParse(commandLineArgs[num4 + 1], out var result)) ? fBAutoTest.levels : new int[1] { Mathf.Clamp(result, 0, 3) }) : new int[4] { 0, 1, 2, 3 });
			}
			if (num + 2 < commandLineArgs.Length && float.TryParse(commandLineArgs[num + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				fBAutoTest.secs = result2;
			}
		}
	}

	private IEnumerator Start()
	{
		string skin0 = ((skins.Length > 1 || skins[0] != "") ? PlayerPrefs.GetString("fb_skin", "r_tutu") : null);
		Application.logMessageReceived += delegate(string c, string st, LogType ty)
		{
			if (ty == LogType.Exception || ty == LogType.Error)
			{
				errors++;
			}
		};
		string shots = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(shots);
		yield return new WaitForSeconds(4f);
		ScreenCapture.CaptureScreenshot(Path.Combine(shots, "menu.png"));
		yield return null;
		yield return null;
		Debug.Log("[FreeBala][auto] rig do menu: " + Rig(GameObject.Find("Previa")));
		int keepQ = FBQuality.Choice;
		foreach (var job in jobs)
		{
			string map = job.map;
			string mode = job.mode;
			string[] array = skins;
			foreach (string sk in array)
			{
				string[] array2 = sims;
				foreach (string sim in array2)
				{
					int[] array3 = levels;
					foreach (int num in array3)
					{
						if (sim != "")
						{
							FBQuality.SimulatePlatform((Enum.TryParse<FBPlat>(sim, ignoreCase: true, out var result) && result != 0) ? new FBPlat?(result) : null);
						}
						if (num >= 0)
						{
							FBQuality.Set(num);
						}
						string qn = (FBQuality.Simulated ? (FBQuality.Device.plat.ToString() + "_") : "") + FBQuality.Names[(int)FBQuality.Level];
						GameData.Load();
						FBSession.Load();
						FBSession.map = map;
						FBSession.mode = mode;
						FBSession.online = false;
						if (sk != "")
						{
							FBSession.skin = sk;
							FBSession.look = "";
							FBSession.pickRandom = false;
						}
						if (show)
						{
							FBSession.view = "tps";
						}
						FBSession.Save();
						errors = 0;
						Debug.Log("[FreeBala][auto] carregando " + map + "/" + mode + " · qualidade " + qn);
						AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + map);
						if (op == null)
						{
							Debug.Log("[FreeBala][auto] FALHA: cena Map_" + map + " não existe");
							continue;
						}
						while (!op.isDone)
						{
							yield return null;
						}
						for (float w3 = 0f; w3 < 4f; w3 += Time.unscaledDeltaTime)
						{
							yield return null;
						}
						List<(string n, ProfilerRecorder r)> rec = new string[7] { "Main Thread", "Render Thread", "Batches Count", "SetPass Calls Count", "Triangles Count", "Shadow Casters Count", "Visible Skinned Meshes Count" }.Select((string n) => (n: n, r: ProfilerRecorder.StartNew(n.Contains("Thread") ? ProfilerCategory.Internal : ProfilerCategory.Render, n, 300))).ToList();
						float t = 0f;
						int frames = 0;
						float worst = 0f;
						List<float> ft = new List<float>(4096);
						long vegSum = 0L;
						int vegMax = 0;
						Loco.Clear();
						while (t < secs)
						{
							yield return null;
							t += Time.unscaledDeltaTime;
							frames++;
							LocoSample();
							worst = Mathf.Max(worst, Time.unscaledDeltaTime);
							ft.Add(Time.unscaledDeltaTime * 1000f);
							vegSum += FBQuality.VegVisibleInstances;
							vegMax = Mathf.Max(vegMax, FBQuality.VegVisibleInstances);
						}
						vegAvg = (int)((frames > 0) ? (vegSum / frames) : 0);
						vegPeak = vegMax;
						Dictionary<string, double> avg = new Dictionary<string, double>();
						string stats = string.Join(" · ", rec.Select(delegate((string n, ProfilerRecorder r) x)
						{
							double num4 = 0.0;
							int count = x.r.Count;
							for (int l = 0; l < count; l++)
							{
								num4 += (double)x.r.GetSample(l).Value;
							}
							num4 /= (double)Math.Max(1, count);
							x.r.Dispose();
							avg[x.n] = num4;
							return (!x.n.Contains("Thread")) ? $"{x.n} {num4:0}" : $"{x.n} {num4 / 1000000.0:0.0} ms";
						}));
						Bench(shots, map, mode, qn, ft, (float)frames / Mathf.Max(0.01f, t), avg);
						if ((bool)FBPlayer.I && (bool)FBMatch.Player)
						{
							float w3 = FBMatch.Player.Pos.y;
							float top = w3;
							FBPlayer.I.TestJump();
							for (float w = 0f; w < 1.2f; w += Time.deltaTime)
							{
								yield return null;
								top = Mathf.Max(top, FBMatch.Player.Pos.y);
							}
							Debug.Log($"[FreeBala][auto] {map}/{mode} pulo: subiu {top - w3:0.00} m");
						}
						Debug.Log("[FreeBala][auto] " + map + "/" + mode + " rig do jogador: " + Rig(FBMatch.Player ? FBMatch.Player.gameObject : null) + " · chão " + (FBMatch.Player ? FBMatch.Player.grounded.ToString() : "-"));
						Debug.Log("[FreeBala][auto] " + map + "/" + mode + " desempenho: " + stats);
						LocoReport(map + "/" + mode);
						Debug.Log($"[FreeBala][auto] {map}/{mode} bots: {FBBot.Diag()} · reanimações iniciadas {FBBot.ReviveStarts} · com inimigo a < 22 m do caído {FBBot.ReviveHot} · adiadas por perigo {FBBot.ReviveUnsafeBlocks} (checagens: tiro recebido {FBBot.ReviveWhy[0]}, trocando tiro {FBBot.ReviveWhy[1]}, disparo perto {FBBot.ReviveWhy[2]}, inimigo < 22 m {FBBot.ReviveWhy[3]}, na mira {FBBot.ReviveWhy[4]})" + ((FBMatch.I?.mode is FBModes.Tatico tatico) ? (" · " + tatico.OrdersDiag()) : ""));
						FBBot.ReviveUnsafeBlocks = 0;
						FBBot.ReviveHot = 0;
						FBBot.ReviveStarts = 0;
						Array.Clear(FBBot.ReviveWhy, 0, 5);
						Debug.Log($"[FreeBala][auto] custo corpo: medição {FBCharacter.MeasureMs:0.0} ms em {FBCharacter.MeasureN}x · afastamento da arma {FBRig.ClearMs / (double)Math.Max(1, frames):0.00} ms/quadro ({FBRig.ClearN} chamadas)");
						FBCharacter.MeasureMs = 0.0;
						FBCharacter.MeasureN = 0;
						FBRig.ClearMs = 0.0;
						FBRig.ClearN = 0L;
						ScreenCapture.CaptureScreenshot(Path.Combine(shots, (levels[0] >= 0) ? (map + "_" + mode + "_" + qn + ".png") : (map + "_" + mode + ".png")));
						yield return null;
						yield return null;
						FBCharacter player = FBMatch.Player;
						int num2 = 0;
						int num3 = 0;
						foreach (FBCharacter bot in FBMatch.Bots)
						{
							if ((bool)bot)
							{
								num2++;
								if (bot.alive)
								{
									num3++;
								}
							}
						}
						Debug.Log(string.Format("[FreeBala][auto] {0}/{1}: partida {2} · jogador {3} · bots {4}/{5} · {6:0} FPS (pior quadro {7:0} ms) · erros {8}", map, mode, FBMatch.I ? "ok" : "NÃO montou", player ? player.Pos.ToString() : "nenhum", num3, num2, (float)frames / Mathf.Max(0.01f, t), worst * 1000f, errors));
						if (show)
						{
							yield return Showcase(shots, map + "_" + mode + ((sk != "") ? ("_" + sk) : ""));
						}
					}
				}
			}
		}
		if (sims[0] != "")
		{
			FBQuality.SimulatePlatform(null);
		}
		if (levels[0] >= 0)
		{
			FBQuality.Set(keepQ);
		}
		if (skin0 != null)
		{
			PlayerPrefs.SetString("fb_skin", skin0);
			PlayerPrefs.Save();
		}
		Debug.Log("[FreeBala][auto] fim");
		Application.Quit(0);
	}

	private static void LocoSample()
	{
		foreach (FBCharacter item in FBMatch.All)
		{
			if (!item || !item.alive || item.UnarmedLocomotion || !item.grounded || item.vehiclePose || item.sys != null || !item.an)
			{
				continue;
			}
			float magnitude = new Vector2(item.velocity.x, item.velocity.z).magnitude;
			if (magnitude < 0.6f)
			{
				continue;
			}
			item.an.GetCurrentAnimatorClipInfo(0, ci);
			string text = "-";
			float num = 0f;
			foreach (AnimatorClipInfo item2 in ci)
			{
				if (item2.weight > num && (bool)item2.clip)
				{
					num = item2.weight;
					text = item2.clip.name;
				}
			}
			string key = (item.isPlayer ? "jogador " : "bot ") + (item.CombatStance ? "combate " : "patrulha ") + text;
			Loco.TryGetValue(key, out (int, int, int, double, double) value);
			Loco[key] = (value.Item1 + 1, value.Item2 + ((item.LocoRate <= 0.705f) ? 1 : 0), value.Item3 + ((item.LocoRate >= 1.445f) ? 1 : 0), value.Item4 + (double)item.LocoRate, value.Item5 + (double)magnitude);
		}
	}

	private static void LocoReport(string tag)
	{
		foreach (KeyValuePair<string, (int, int, int, double, double)> item in Loco.OrderByDescending((KeyValuePair<string, (int n, int lo, int hi, double sum, double g)> k) => k.Value.n).Take(14))
		{
			(int, int, int, double, double) value = item.Value;
			Debug.Log($"[FreeBala][auto] {tag} ritmo armado · {item.Key}: {value.Item1} amostras · ritmo médio {value.Item4 / (double)value.Item1:0.00} · no piso 0,7: {100.0 * (double)value.Item2 / (double)value.Item1:0}% · no teto 1,45: {100.0 * (double)value.Item3 / (double)value.Item1:0}% · v média {value.Item5 / (double)value.Item1:0.0} m/s");
		}
	}

	private void Bench(string dir, string map, string mode, string q, List<float> ft, float fps, Dictionary<string, double> avg)
	{
		try
		{
			if (ft.Count != 0)
			{
				ft.Sort();
				Camera main = Camera.main;
				CultureInfo invariantCulture = CultureInfo.InvariantCulture;
				string text = Path.Combine(dir, "bench.csv");
				if (File.Exists(text) && File.ReadLines(text).FirstOrDefault() != "data;plataforma;gpu;mapa;modo;nivel;fps;p50_ms;p95_ms;p99_ms;pior_ms;memoria_mb;main_ms;render_ms;triangulos;shadow_casters;batches;setpass;skinned;casters_cortados;occlusion;erros;veg_visiveis_media;veg_visiveis_pico;veg_total;veg_teto;perfil")
				{
					File.Move(text, Path.Combine(dir, $"bench_antigo_{DateTime.Now:yyyyMMdd_HHmmss}.csv"));
				}
				bool flag = !File.Exists(text);
				object[] obj = new object[27]
				{
					DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
					Application.platform,
					SystemInfo.graphicsDeviceName,
					map,
					mode,
					q,
					fps.ToString("0", invariantCulture),
					P(0.5f).ToString("0.0", invariantCulture),
					P(0.95f).ToString("0.0", invariantCulture),
					P(0.99f).ToString("0.0", invariantCulture),
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null
				};
				List<float> list = ft;
				obj[10] = list[list.Count - 1].ToString("0", invariantCulture);
				obj[11] = Profiler.GetTotalAllocatedMemoryLong() / 1048576;
				obj[12] = (A("Main Thread") / 1000000.0).ToString("0.0", invariantCulture);
				obj[13] = (A("Render Thread") / 1000000.0).ToString("0.0", invariantCulture);
				obj[14] = A("Triangles Count").ToString("0", invariantCulture);
				obj[15] = A("Shadow Casters Count").ToString("0", invariantCulture);
				obj[16] = A("Batches Count").ToString("0", invariantCulture);
				obj[17] = A("SetPass Calls Count").ToString("0", invariantCulture);
				obj[18] = A("Visible Skinned Meshes Count").ToString("0", invariantCulture);
				obj[19] = FBQuality.LastCulledCasters;
				obj[20] = (((bool)main && main.useOcclusionCulling) ? 1 : 0);
				obj[21] = errors;
				obj[22] = vegAvg;
				obj[23] = vegPeak;
				obj[24] = FBQuality.VegTotalInstances;
				obj[25] = FBQuality.VegMaxInstances;
				obj[26] = FBQuality.Device.plat;
				string text2 = string.Join(";", obj);
				File.AppendAllText(text, (flag ? "data;plataforma;gpu;mapa;modo;nivel;fps;p50_ms;p95_ms;p99_ms;pior_ms;memoria_mb;main_ms;render_ms;triangulos;shadow_casters;batches;setpass;skinned;casters_cortados;occlusion;erros;veg_visiveis_media;veg_visiveis_pico;veg_total;veg_teto;perfil\n" : "") + text2 + "\n");
				Debug.Log("[FreeBala][auto][bench] " + text2);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[FreeBala][auto] bench.csv: " + ex.Message);
		}
		double A(string k)
		{
			if (!avg.TryGetValue(k, out var value))
			{
				return 0.0;
			}
			return value;
		}
		float P(float x)
		{
			return ft[Mathf.Clamp((int)(x * (float)ft.Count), 0, ft.Count - 1)];
		}
	}

	private IEnumerator Showcase(string dir, string tag)
	{
		FBCharacter me = FBMatch.Player;
		if (!me)
		{
			yield break;
		}
		GameObject cg = new GameObject("AutoCam");
		Camera cam = cg.AddComponent<Camera>();
		cam.depth = 100f;
		if ((bool)Camera.main)
		{
			cam.cullingMask = Camera.main.cullingMask;
		}
		cam.fieldOfView = 38f;
		cam.nearClipPlane = 0.05f;
		yield return Shot(me.transform, new Vector3(1f, 0.1f, 0.25f), 3.2f, 1f, "jogador_lado");
		yield return Shot(me.transform, new Vector3(-0.5f, 0.15f, -1f), 3.2f, 1f, "jogador_costas");
		yield return Shot(me.transform, new Vector3(0.4f, 0.1f, 1f), 3.2f, 1f, "jogador_frente");
		FBWeaponUser wu = (FBPlayer.I ? FBPlayer.I.wu : null);
		if ((bool)wu)
		{
			wu.Give(FBSession.primary, FBSession.smg, FBSession.secondary);
			wu.Equip(2, instant: true);
			for (float t3 = 0f; t3 < 1.2f; t3 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			yield return Shot(me.transform, new Vector3(-0.5f, 0.15f, -1f), 2.6f, 1.15f, "coldre_costas");
			yield return Shot(me.transform, new Vector3(1f, 0.1f, 0.25f), 2.6f, 1.15f, "coldre_lado");
			yield return Shot(me.transform, new Vector3(0.4f, 0.1f, 1f), 2.6f, 1.15f, "coldre_frente");
			wu.Equip(0, instant: true);
		}
		Animator an = me.an;
		Transform transform = (an ? an.GetBoneTransform(HumanBodyBones.Head) : null);
		foreach (Transform item in from x in me.GetComponentsInChildren<Transform>(includeInactive: true)
			where x.name.StartsWith("Coldre_") || x.name.StartsWith("Arma_")
			select x)
		{
			Renderer[] componentsInChildren = item.GetComponentsInChildren<Renderer>();
			if (componentsInChildren.Length == 0)
			{
				continue;
			}
			Matrix4x4 worldToLocalMatrix = item.worldToLocalMatrix;
			Bounds bounds = default(Bounds);
			bool flag = true;
			Renderer[] array = componentsInChildren;
			for (int j = 0; j < array.Length; j++)
			{
				Bounds bounds2 = array[j].bounds;
				for (int k = 0; k < 8; k++)
				{
					Vector3 vector = worldToLocalMatrix.MultiplyPoint3x4(new Vector3(((k & 1) == 0) ? bounds2.min.x : bounds2.max.x, ((k & 2) == 0) ? bounds2.min.y : bounds2.max.y, ((k & 4) == 0) ? bounds2.min.z : bounds2.max.z));
					if (flag)
					{
						bounds = new Bounds(vector, Vector3.zero);
						flag = false;
					}
					else
					{
						bounds.Encapsulate(vector);
					}
				}
			}
			Vector3 vector2 = Vector3.Scale(bounds.size, item.lossyScale);
			Debug.Log(string.Format("[FreeBala][auto] vitrine {0}: {1} ativo {2} · escala {3:0.000} · caixa local {4:0.00}×{5:0.00}×{6:0.00} m · cabeça {7:0.00} m · peças: {8}", tag, item.name, item.gameObject.activeInHierarchy, item.lossyScale.x, vector2.x, vector2.y, vector2.z, transform ? (transform.position.y - me.transform.position.y) : 0f, string.Join(",", componentsInChildren.Select((Renderer r) => r.name).Distinct().Take(14))));
		}
		foreach (FBCar item2 in FBVehicles.list.Where((FBCar c) => (bool)c && Vector3.Distance(c.transform.position, me.Pos) < 30f).Take(4))
		{
			float num = 1E+09f;
			foreach (FBWheel wheel in item2.wheels)
			{
				if ((bool)wheel.obj)
				{
					Renderer[] array = wheel.obj.GetComponentsInChildren<Renderer>();
					foreach (Renderer renderer in array)
					{
						num = Mathf.Min(num, renderer.bounds.min.y);
					}
				}
			}
			Vector3 position = item2.transform.position;
			float num2 = 0f / 0f;
			foreach (RaycastHit item3 in from h in Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 10f, FBWorld.SolidMask)
				orderby h.distance
				select h)
			{
				if (!item3.transform.IsChildOf(item2.transform))
				{
					num2 = item3.point.y;
					break;
				}
			}
			Debug.Log($"[FreeBala][auto] vitrine {tag}: carro {item2.id} · pneu mais baixo {num - num2:0.00} m acima do chão · origem {position.y - num2:0.00} m");
		}
		FBCharacter fBCharacter = FBMatch.Bots.FirstOrDefault((FBCharacter b) => (bool)b && b.alive);
		if ((bool)fBCharacter)
		{
			FBCharacter component = fBCharacter.GetComponent<FBCharacter>();
			if ((bool)component)
			{
				component.transform.position = me.Pos + me.transform.right * 2.2f;
				yield return Shot(component.transform, new Vector3(1f, 0.1f, 0.3f), 3.2f, 1f, "bot_lado");
			}
		}
		string[] array2 = new string[4] { "S10_2017", "xj6", "ViaturaPMAzul", "ViaturaBM" };
		foreach (string vid in array2)
		{
			Vector3 p = me.Pos + me.transform.forward * 6f;
			FBCar car = FBVehicles.Spawn(vid, p, me.transform.eulerAngles.y);
			if (!car)
			{
				Debug.Log("[FreeBala][auto] vitrine: veículo " + vid + " não nasceu");
				continue;
			}
			yield return null;
			bool ok = FBDrive.Enter(me, car);
			for (float t3 = 0f; t3 < 1.5f; t3 += Time.deltaTime)
			{
				yield return null;
			}
			Debug.Log($"[FreeBala][auto] vitrine {tag}: entrou em {vid} {ok}");
			yield return Shot(car.transform, new Vector3(-1f, 0.25f, 0.15f), 4.2f, 0.9f, vid + "_lado");
			if (car.kind != "moto")
			{
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				Vector3 vector3 = car.L2W(car.seat);
				cam.transform.position = car.L2W(car.seat + new Vector3(1.05f, 0.45f, 0.35f));
				cam.transform.LookAt(vector3 + car.transform.up * 0.35f);
				cam.fieldOfView = 60f;
				yield return null;
				ScreenCapture.CaptureScreenshot(Path.Combine(dir, "show_" + tag + "_" + vid + "_cabine.png"));
				yield return null;
				yield return null;
				cam.fieldOfView = 38f;
			}
			yield return Shot(car.transform, new Vector3(-0.6f, 0.5f, 1f), 4.6f, 0.9f, vid + "_frente");
			FBDrive.Exit(me, force: true);
			for (float t3 = 0f; t3 < 0.6f; t3 += Time.deltaTime)
			{
				yield return null;
			}
			FBVehicles.list.Remove(car);
			UnityEngine.Object.Destroy(car.gameObject);
			yield return null;
		}
		UnityEngine.Object.Destroy(cg);
		IEnumerator Shot(Transform tgt, Vector3 localDir, float dist, float h, string name)
		{
			for (int m = 0; m < 3; m++)
			{
				yield return null;
			}
			Vector3 vector4 = tgt.position + Vector3.up * h;
			cam.transform.position = vector4 + tgt.rotation * localDir.normalized * dist;
			cam.transform.LookAt(vector4);
			yield return null;
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, "show_" + tag + "_" + name + ".png"));
			yield return null;
			yield return null;
		}
	}

	private static string Rig(GameObject g)
	{
		if (!g)
		{
			return "nenhum";
		}
		Animator an = g.GetComponentInChildren<Animator>();
		if (!an || !an.isHuman)
		{
			return "sem humanoide";
		}
		AnimatorClipInfo[] currentAnimatorClipInfo = an.GetCurrentAnimatorClipInfo(0);
		RigBuilder component = g.GetComponent<RigBuilder>();
		return string.Format("quadril {0} · pé {1} · cabeça {2} · clipe {3} · culling {4} · rigBuilder {5} · escala {6:0.00} · humanScale {7:0.00}", B(HumanBodyBones.Hips), B(HumanBodyBones.LeftFoot), B(HumanBodyBones.Head), (currentAnimatorClipInfo.Length != 0) ? currentAnimatorClipInfo[0].clip.name : "-", an.cullingMode, component ? component.enabled.ToString() : "-", g.transform.lossyScale.y, an.humanScale);
		string B(HumanBodyBones b)
		{
			Transform boneTransform = an.GetBoneTransform(b);
			if (!boneTransform)
			{
				return "-";
			}
			return (boneTransform.position.y - g.transform.position.y).ToString("0.00");
		}
	}
}
}
