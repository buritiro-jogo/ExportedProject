using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBTour : MonoBehaviour
{
	private string run;

	private ProfilerRecorder dc;

	private ProfilerRecorder bt;

	private ProfilerRecorder sp;

	private ProfilerRecorder tr;

	private static readonly (string n, float x, float y, float h)[] PTS = new(string, float, float, float)[34]
	{
		("IpeAmarelo", 50.1f, 150.9f, 100f),
		("Samauma", 52.4f, 136f, -78f),
		("Andiroba", 1.3f, 54f, -78f),
		("Cedro0", 222.7f, 79.7f, -73f),
		("Cedro1", 233.1f, 81.5f, 99f),
		("Inga0", 152.3f, -103.2f, -78f),
		("Inga1", 151.3f, -178.1f, 93f),
		("Araca", -74.5f, -124.9f, -80f),
		("Jatoba", 109.6f, -201.9f, -77f),
		("Pupunha0", 51.9f, -321.6f, -81f),
		("Pupunha1", 55.2f, -378.2f, -73f),
		("Pupunha2", 46.9f, -479.8f, 103f),
		("Urucum0", -131.3f, -339f, -82f),
		("Urucum1", -120.3f, -380.9f, -81f),
		("Urucum2", -129f, -442f, -84f),
		("Acai", -11.4f, -505.5f, -82f),
		("Buriti0", -27.9f, -647.9f, -85f),
		("Buriti1", -69.5f, -743.8f, 15f),
		("Buriti2", -56.7f, -787.9f, 16f),
		("Bat_Norte", -80.3f, -18f, 304f),
		("Bat_Norte2", -44f, -11.6f, 270f),
		("Bat_Sul", -44.8f, -127.2f, 90f),
		("Bat_Leste", 28.2f, -59.5f, 188f),
		("Int_Escritorio", -93.97f, -60.8f, -133f),
		("Int_Reserva", -90.13f, -49.62f, 68f),
		("Int_Oficina", -58.71f, -49.12f, 108f),
		("Int_Alojamento", -32.68f, -41.06f, 78f),
		("Avenida_N", 163.3f, 188.7f, 253f),
		("Avenida_Andiroba", 107.4f, 53.5f, 253f),
		("Avenida_Colegio", 54.6f, -93.9f, 253f),
		("Avenida_Jatoba", 0.7f, -243.9f, 253f),
		("Avenida_Pupunha", -52.9f, -396.4f, 253f),
		("Avenida_Acai", -106.5f, -545.2f, 253f),
		("Avenida_S", -46.4f, -633.6f, 160f)
	};

	private static readonly Dictionary<Mesh, long> triCache = new Dictionary<Mesh, long>();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbtour");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			GameObject obj = new GameObject("FB_Tour");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<FBTour>().run = commandLineArgs[num + 1];
		}
	}

	private static long Rec(ProfilerRecorder r)
	{
		if (!r.Valid)
		{
			return -1L;
		}
		return r.LastValue;
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][tour] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Start()
	{
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "tour");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		string[] array = run.Split(':');
		string text = array[0];
		string mode = ((array.Length > 1) ? array[1] : "role");
		GameData.Load();
		FBSession.Load();
		string keepMap = FBSession.map;
		string keepMode = FBSession.mode;
		FBSession.map = text;
		FBSession.mode = mode;
		FBSession.online = false;
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + text);
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		yield return Wait(6f);
		if (mode == "role")
		{
			FBGarage.AutoPick();
			yield return Wait(2f);
		}
		FBMatch.God = true;
		FBPlayer i2 = FBPlayer.I;
		if ((bool)i2)
		{
			i2.enabled = false;
		}
		Camera cam = Camera.main;
		dc = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
		bt = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
		sp = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
		tr = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
		int num = 0;
		Renderer[] array2 = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
		foreach (Renderer renderer in array2)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			foreach (Material material in sharedMaterials)
			{
				if ((bool)material && (!material.shader || !material.shader.isSupported || material.shader.name.Contains("Error")) && num++ < 30)
				{
					Log(string.Format("MATERIAL QUEBRADO: {0} ({1}) mat {2} shader {3} em {4}", renderer.name, renderer.transform.root.name, material.name, material.shader ? material.shader.name : "nulo", renderer.bounds.center));
				}
			}
		}
		Log($"materiais quebrados: {num}");
		int ok = 0;
		(string n, float x, float y, float h)[] pTS = PTS;
		for (int l = 0; l < pTS.Length; l++)
		{
			(string n, float x, float y, float h) p = pTS[l];
			Vector3 flat = new Vector3(-1.2f * p.x, 0f, -1.2f * p.y);
			float num2 = (p.n.StartsWith("Int_") ? 2.6f : 80f);
			RaycastHit hitInfo;
			float gy = (Physics.Raycast(flat + Vector3.up * num2, Vector3.down, out hitInfo, 160f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) ? hitInfo.point.y : (0f / 0f));
			if (float.IsNaN(gy))
			{
				Log($"{p.n}: SEM CHÃO em {flat}");
				continue;
			}
			float f = p.h * ((float)Math.PI / 180f);
			Vector3 dirv = new Vector3(0f - Mathf.Cos(f), 0f, 0f - Mathf.Sin(f)).normalized;
			NavMeshHit hit;
			bool nav = NavMesh.SamplePosition(new Vector3(flat.x, gy, flat.z), out hit, 3f, -1);
			for (int v = 0; v < 2; v++)
			{
				Vector3 position = ((v == 0) ? new Vector3(flat.x, gy + 1.6f, flat.z) : (new Vector3(flat.x, gy + 2.7f, flat.z) - dirv * 3.6f));
				cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Quaternion.AngleAxis((v == 0) ? 3 : 9, Vector3.Cross(dirv, Vector3.up)) * dirv));
				FBDistanceCull.Refresh();
				yield return Wait(1.1f);
				float fps = 0f;
				int i = 0;
				for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
				{
					fps += 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
					i++;
					yield return null;
				}
				ScreenCapture.CaptureScreenshot(Path.Combine(dir, p.n + "_" + ((v == 0) ? "fp" : "tps") + ".png"));
				yield return null;
				yield return null;
				Log(string.Format("{0} {1}: chão {2:0.00} · navmesh {3} · FPS {4:0} · draw calls {5} · batches {6} · setpass {7} · tris {8}k · corte dist. {9}/{10} · carros escondidos {11}", p.n, (v == 0) ? "fp" : "tps", gy, nav ? "sim" : "NÃO", fps / (float)Mathf.Max(1, i), Rec(dc), Rec(bt), Rec(sp), Rec(tr) / 1000, FBDistanceCull.Hidden, FBDistanceCull.Count, FBCarCull.Hidden));
				if (v == 0)
				{
					Log(p.n + " quem desenha: " + Breakdown());
				}
				if (v == 0)
				{
					Log(p.n + " carros: " + Cars(cam));
				}
				if (v == 0 && (p.n == "Avenida_S" || p.n == "Andiroba" || p.n == "Jatoba"))
				{
					IEnumerator e = Experiments();
					while (e.MoveNext())
					{
						yield return e.Current;
					}
				}
			}
			ok++;
		}
		Log($"fim · {ok}/{PTS.Length} pontos");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		Application.Quit();
	}

	private static string Cars(Camera main)
	{
		string arg = string.Join(",", Camera.allCameras.Select((Camera c) => c.name + (c.enabled ? "" : "(off)")));
		List<(FBCar c, float d, int vis, int all)> list = (from c in UnityEngine.Object.FindObjectsByType<FBCar>(FindObjectsSortMode.None)
			select (c: c, d: Vector3.Distance(c.transform.position, main.transform.position), vis: c.GetComponentsInChildren<Renderer>().Count((Renderer r) => r.isVisible), all: c.GetComponentsInChildren<Renderer>().Length) into e
			orderby e.vis descending
			select e).ToList();
		return $"{list.Count} carros, câmeras [{arg}] · " + string.Join(" | ", from e in list.Take(8)
			select $"{e.c.id} {e.d:0}m vis {e.vis}/{e.all}");
	}

	private IEnumerator Experiments()
	{
		Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
		Renderer[] mato = Sel("VJ11_Mato");
		Renderer[] arv = Sel("VJ11_Arvore");
		UniversalRenderPipelineAsset urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		float sd = (urp ? urp.shadowDistance : 0f);
		List<string> res = new List<string>();
		List<(string, ProfilerRecorder)> recs = new(string, ProfilerCategory, string)[8]
		{
			("main", ProfilerCategory.Internal, "Main Thread"),
			("render", ProfilerCategory.Internal, "Render Thread"),
			("update", ProfilerCategory.Scripts, "BehaviourUpdate"),
			("late", ProfilerCategory.Scripts, "LateBehaviourUpdate"),
			("fixed", ProfilerCategory.Scripts, "FixedBehaviourUpdate"),
			("fisica", ProfilerCategory.Physics, "Physics.Simulate"),
			("cull", ProfilerCategory.Render, "CullScriptable"),
			("anim", ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationEnd")
		}.Select(((string, ProfilerCategory, string) x) => (x.Item1, ProfilerRecorder.StartNew(x.Item2, x.Item3, 30))).ToList();
		ProfilerRecorder bu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate", 60);
		try
		{
			ProfilerRecorder lu = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "LateBehaviourUpdate", 60);
			double Avg(int frames)
			{
				List<ProfilerRecorderSample> list3 = new List<ProfilerRecorderSample>();
				List<ProfilerRecorderSample> list4 = new List<ProfilerRecorderSample>();
				bu.CopyTo(list3);
				lu.CopyTo(list4);
				return list3.Skip(Mathf.Max(0, list3.Count - frames)).DefaultIfEmpty().Average((ProfilerRecorderSample x) => x.Value) * 1E-06 + list4.Skip(Mathf.Max(0, list4.Count - frames)).DefaultIfEmpty().Average((ProfilerRecorderSample x) => x.Value) * 1E-06;
			}
			try
			{
				for (int j = 0; j < 30; j++)
				{
					yield return null;
				}
				double base0 = Avg(25);
				List<(string, int, double)> gains = new List<(string, int, double)>();
				List<IGrouping<Type, MonoBehaviour>> list2 = (from m in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
					where (bool)m && m.enabled && m != this && !(m is FBDistanceCull)
					group m by m.GetType()).ToList();
				foreach (IGrouping<Type, MonoBehaviour> item in list2)
				{
					Type t = item.Key;
					if (t.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null && t.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null)
					{
						continue;
					}
					List<MonoBehaviour> list = item.ToList();
					foreach (MonoBehaviour item2 in list)
					{
						if ((bool)item2)
						{
							item2.enabled = false;
						}
					}
					for (int j = 0; j < 22; j++)
					{
						yield return null;
					}
					double num = Avg(18);
					foreach (MonoBehaviour item3 in list)
					{
						if ((bool)item3)
						{
							item3.enabled = true;
						}
					}
					gains.Add((t.Name, list.Count, base0 - num));
					for (int j = 0; j < 6; j++)
					{
						yield return null;
					}
				}
				Log($"scripts (update+late {base0:0.0} ms) — o que cai ao desligar: " + string.Join(" · ", from x in gains.OrderByDescending(((string, int, double) x) => x.Item3).Take(10)
					select $"{x.Item1}×{x.Item2} {x.Item3:0.00}"));
			}
			finally
			{
				((IDisposable)lu).Dispose();
			}
		}
		finally
		{
			((IDisposable)bu).Dispose();
		}
		Log("cpu (ms/quadro): " + string.Join(" · ", recs.Select(((string, ProfilerRecorder) r) => $"{r.Item1} {((r.Item2.Valid && r.Item2.Count > 0) ? AvgMs(r.Item2) : (-1.0)):0.0}")));
		foreach (var item4 in recs)
		{
			item4.Item2.Dispose();
		}
		GameObject gameObject = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault((GameObject g) => g.name.StartsWith("Map_"));
		if ((bool)gameObject)
		{
			Log("componentes no mapa: " + string.Join(", ", from g in (from c in gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true)
					group c by c.GetType().Name into g
					orderby g.Count() descending
					select g).Take(8)
				select $"{g.Key} {g.Count()}") + $" · colisores {gameObject.GetComponentsInChildren<Collider>(includeInactive: true).Length} · renderers {gameObject.GetComponentsInChildren<Renderer>(includeInactive: true).Length} · luzes {UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length}");
		}
		IEnumerator e5 = Meas("tudo");
		while (e5.MoveNext())
		{
			yield return e5.Current;
		}
		Set(mato, off: true);
		e5 = Meas($"sem mato ({mato.Length})");
		while (e5.MoveNext())
		{
			yield return e5.Current;
		}
		Set(mato, off: false);
		Set(arv, off: true);
		e5 = Meas($"sem árvores ({arv.Length})");
		while (e5.MoveNext())
		{
			yield return e5.Current;
		}
		Set(arv, off: false);
		if ((bool)urp)
		{
			urp.shadowDistance = 0f;
			e5 = Meas("sem sombra");
			while (e5.MoveNext())
			{
				yield return e5.Current;
			}
			urp.shadowDistance = sd;
		}
		Set(mato, off: true);
		Set(arv, off: true);
		if ((bool)urp)
		{
			urp.shadowDistance = 0f;
		}
		e5 = Meas("sem vegetação e sem sombra");
		while (e5.MoveNext())
		{
			yield return e5.Current;
		}
		Set(mato, off: false);
		Set(arv, off: false);
		if ((bool)urp)
		{
			urp.shadowDistance = sd;
		}
		FBDistanceCull.Refresh();
		Log("experimento: " + string.Join(" | ", res));
		IEnumerator Meas(string tag)
		{
			for (int i2 = 0; i2 < 20; i2++)
			{
				yield return null;
			}
			float f = 0f;
			int n2 = 0;
			for (float t2 = 0f; t2 < 1.5f; t2 += Time.unscaledDeltaTime)
			{
				f += Time.unscaledDeltaTime;
				n2++;
				yield return null;
			}
			res.Add($"{tag} {(float)n2 / Mathf.Max(0.001f, f):0} fps ({Rec(dc)} dc)");
		}
		Renderer[] Sel(string k)
		{
			return all.Where((Renderer r) => (bool)r && (bool)r.transform.parent && (r.name.Contains(k) || r.transform.parent.name.Contains(k))).ToArray();
		}
		static void Set(Renderer[] rs, bool off)
		{
			foreach (Renderer renderer in rs)
			{
				if ((bool)renderer)
				{
					renderer.forceRenderingOff = off;
				}
			}
		}
	}

	private static double AvgMs(ProfilerRecorder r)
	{
		List<ProfilerRecorderSample> list = new List<ProfilerRecorderSample>(r.Capacity);
		r.CopyTo(list);
		if (list.Count != 0)
		{
			return list.Average((ProfilerRecorderSample x) => x.Value) * 1E-06;
		}
		return 0.0;
	}

	private static string Breakdown()
	{
		Dictionary<string, (int n, long t)> dictionary = new Dictionary<string, (int, long)>();
		Renderer[] array = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
		foreach (Renderer renderer in array)
		{
			if (!renderer.isVisible || renderer.forceRenderingOff)
			{
				continue;
			}
			Mesh mesh = ((renderer is SkinnedMeshRenderer skinnedMeshRenderer) ? skinnedMeshRenderer.sharedMesh : (renderer.GetComponent<MeshFilter>() ? renderer.GetComponent<MeshFilter>().sharedMesh : null));
			int num = renderer.sharedMaterials.Length;
			long value = 0L;
			if ((bool)mesh && renderer.isPartOfStaticBatch && renderer is MeshRenderer { subMeshStartIndex: var j } meshRenderer)
			{
				for (; j < Mathf.Min(mesh.subMeshCount, meshRenderer.subMeshStartIndex + num); j++)
				{
					value += mesh.GetIndexCount(j) / 3;
				}
			}
			else if ((bool)mesh && !triCache.TryGetValue(mesh, out value))
			{
				for (int k = 0; k < mesh.subMeshCount; k++)
				{
					value += mesh.GetIndexCount(k) / 3;
				}
				triCache[mesh] = value;
			}
			string text = renderer.transform.root.name;
			if (text.StartsWith("Map_"))
			{
				Transform parent = renderer.transform;
				while ((bool)parent.parent && (bool)parent.parent.parent)
				{
					parent = parent.parent;
				}
				text = "mapa/" + Regex.Replace(parent.name, "[\\d._]+$", "");
			}
			else
			{
				text = Regex.Replace(text, "[\\d._ ()]+$", "");
			}
			dictionary.TryGetValue(text, out var value2);
			dictionary[text] = (value2.Item1 + Mathf.Max(1, num), value2.Item2 + value);
		}
		return "por tris: " + string.Join(" | ", from e in dictionary.OrderByDescending((KeyValuePair<string, (int n, long t)> e) => e.Value.t).Take(8)
			select $"{e.Key} {e.Value.n}dc {e.Value.t / 1000}k") + " ## por draw calls: " + string.Join(" | ", from e in dictionary.OrderByDescending((KeyValuePair<string, (int n, long t)> e) => e.Value.n).Take(10)
			select $"{e.Key} {e.Value.n}dc {e.Value.t / 1000}k");
	}
}
}
