using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBRenderAudit : MonoBehaviour
{
	private struct Sample
	{
		public string name;

		public Vector3 pos;

		public Quaternion rot;

		public float ocDiff;

		public float lodDiff;

		public Texture2D a;

		public Texture2D c;
	}

	private string[] runs;

	private int points = 16;

	private string dir;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbrender");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			GameObject obj = new GameObject("FB_RenderAudit");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBRenderAudit fBRenderAudit = obj.AddComponent<FBRenderAudit>();
			fBRenderAudit.runs = commandLineArgs[num + 1].Split(',');
			if (num + 2 < commandLineArgs.Length && int.TryParse(commandLineArgs[num + 2], out var result))
			{
				fBRenderAudit.points = result;
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][render] " + s);
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
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		string[] array = runs;
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(':');
			string map = array2[0];
			string mode = ((array2.Length > 1) ? array2[1] : "classico");
			GameData.Load();
			FBSession.Load();
			string keepMap = FBSession.map;
			string keepMode = FBSession.mode;
			FBSession.map = map;
			FBSession.mode = mode;
			FBSession.online = false;
			FBSession.view = "tps";
			FBSession.Save();
			AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + map);
			if (op == null)
			{
				Log(map + ": cena não existe");
				continue;
			}
			while (!op.isDone)
			{
				yield return null;
			}
			yield return Wait(5f);
			FBMatch.God = true;
			yield return Shots(map);
			yield return Audit(map);
			FBMatch.God = false;
			FBSession.map = keepMap;
			FBSession.mode = keepMode;
			FBSession.Save();
		}
		Log("fim");
		Application.Quit();
	}

	private IEnumerator Shots(string map)
	{
		FBCharacter me = FBMatch.Player;
		List<FBCharacter> list = FBMatch.Bots.Where((FBCharacter b) => (bool)b && b.alive && b.GetComponent<FBWeaponUser>()?.Cur != null).ToList();
		if (!me || list.Count == 0)
		{
			Log(map + ": tiros — sem jogador/bot armado");
			yield break;
		}
		foreach (FBCharacter bot2 in FBMatch.Bots)
		{
			if ((bool)bot2)
			{
				FBBot component = bot2.GetComponent<FBBot>();
				if ((bool)component)
				{
					component.enabled = false;
				}
			}
		}
		FBCharacter bot = list[0];
		FBWeaponUser bw = bot.GetComponent<FBWeaponUser>();
		FBWeaponUser mw = me.GetComponent<FBWeaponUser>();
		float[] buf = new float[1024];
		float own = 0f;
		if ((bool)mw && mw.Cur != null)
		{
			Camera cam = Camera.main;
			mw.RefillAll(3);
			yield return Measure(delegate
			{
				mw.Fire(cam.transform.position, cam.transform.forward, default(FBRecoil.State), out var _);
			}, delegate(float v)
			{
				own = v;
			});
		}
		List<string> line = new List<string>();
		float[] array = new float[4] { 10f, 25f, 50f, 100f };
		foreach (float d in array)
		{
			Vector3 normalized = me.transform.forward.Flat().normalized;
			Vector3 vector = me.Pos + normalized * d;
			Vector3 valueOrDefault = FBNav.RandomPoint(vector, 6f).GetValueOrDefault(vector);
			FBMatch.Teleport(bot, valueOrDefault, Quaternion.LookRotation(me.Pos - valueOrDefault).eulerAngles.y);
			yield return Wait(0.3f);
			bw.RefillAll(3);
			float lvl = 0f;
			yield return Measure(delegate
			{
				bw.Fire(bot.Pos + Vector3.up * 1.5f, (me.Pos + Vector3.up - (bot.Pos + Vector3.up * 1.5f)).normalized, default(FBRecoil.State), out var _, -99f);
			}, delegate(float v)
			{
				lvl = v;
			});
			line.Add($"{d:0} m {lvl:0.000} ({((own > 0f) ? (lvl / own * 100f) : 0f):0}% do próprio)");
		}
		Log($"{map}: tiro do jogador ({mw?.Cur?.id}) {own:0.000} · bot ({bw.Cur.id}) → " + string.Join(" · ", line));
		int num = GameData.Weapons.Values.Select((WeaponDef w) => FBWeaponSound.Fire(w, silenced: false)).Distinct().Count();
		Log($"{map}: armas com disparo próprio {GameData.Weapons.Values.Count((WeaponDef w) => FBWeaponSound.FireCount(w) > 0)}/{GameData.Weapons.Count} · sorteio de 1 disparo por arma deu {num} sons diferentes · sacar {FBWeaponSound.Draw(bw.Cur)} · longe {FBWeaponSound.Far(bw.Cur)}");
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		bool god = FBMatch.God;
		FBMatch.God = false;
		me.hp = me.maxHp;
		Vector3 vector2 = me.Pos + me.transform.right * 12f;
		FBMatch.Teleport(bot, FBNav.RandomPoint(vector2, 4f).GetValueOrDefault(vector2), 0f);
		yield return Wait(0.3f);
		FBCombat.DamageActor(me, me.maxHp * 0.35f, head: false, bot);
		yield return null;
		yield return null;
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "render_" + map + "_dano.png"));
		yield return Wait(0.4f);
		me.hp = me.maxHp * 0.2f;
		yield return Wait(0.8f);
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "render_" + map + "_vidacritica.png"));
		yield return null;
		me.hp = me.maxHp;
		FBCombat.DamageActor(bot, bot.maxHp * 3f, head: true, me);
		yield return null;
		yield return null;
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "render_" + map + "_abate.png"));
		yield return Wait(0.5f);
		FBMatch.God = god;
		Log(map + ": prints de dano/vida crítica/abate gravados");
		foreach (FBCharacter bot3 in FBMatch.Bots)
		{
			if ((bool)bot3)
			{
				FBBot component2 = bot3.GetComponent<FBBot>();
				if ((bool)component2)
				{
					component2.enabled = true;
				}
			}
		}
		IEnumerator Measure(Action fire, Action<float> done)
		{
			float bg = 0f;
			float floor2;
			for (int k = 0; k < 12; k++)
			{
				bg += Peak(0f, 0f, out floor2);
				yield return null;
			}
			bg /= 12f;
			fire();
			float pk = 0f;
			for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
			{
				pk = Mathf.Max(pk, Peak(0f, 0f, out floor2));
				yield return null;
			}
			done(Mathf.Max(0f, pk - bg));
			yield return Wait(0.6f);
		}
		float Peak(float secs, float t0, out float floor)
		{
			floor = 0f;
			float num2 = 0f;
			AudioListener.GetOutputData(buf, 0);
			float[] array2 = buf;
			foreach (float num3 in array2)
			{
				num2 += num3 * num3;
			}
			AudioListener.GetOutputData(buf, 1);
			array2 = buf;
			foreach (float num4 in array2)
			{
				num2 += num4 * num4;
			}
			return Mathf.Sqrt(num2 / (float)(2 * buf.Length));
		}
	}

	private void SkinnedBounds(string map)
	{
		int num = 0;
		int num2 = 0;
		List<string> list = new List<string>();
		Mesh mesh = new Mesh();
		SkinnedMeshRenderer[] array = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in array)
		{
			if (!skinnedMeshRenderer || !skinnedMeshRenderer.enabled || !skinnedMeshRenderer.gameObject.activeInHierarchy || !skinnedMeshRenderer.sharedMesh)
			{
				continue;
			}
			num++;
			skinnedMeshRenderer.BakeMesh(mesh, useScale: true);
			Vector3[] vertices = mesh.vertices;
			if (vertices.Length == 0)
			{
				continue;
			}
			Matrix4x4 localToWorldMatrix = skinnedMeshRenderer.transform.localToWorldMatrix;
			Bounds bounds = skinnedMeshRenderer.bounds;
			int num3 = 0;
			float num4 = 0f;
			for (int j = 0; j < vertices.Length; j += 7)
			{
				Vector3 point = localToWorldMatrix.MultiplyPoint3x4(vertices[j]);
				if (!bounds.Contains(point))
				{
					num3++;
					num4 = Mathf.Max(num4, Mathf.Sqrt(bounds.SqrDistance(point)));
				}
			}
			if (num4 > 0.15f)
			{
				num2++;
				if (list.Count < 8)
				{
					list.Add($"{skinnedMeshRenderer.transform.root.name}/{skinnedMeshRenderer.name} {num4:0.00} m fora ({num3} pts) upd={skinnedMeshRenderer.updateWhenOffscreen}");
				}
			}
		}
		UnityEngine.Object.Destroy(mesh);
		Log($"{map}: malhas animadas {num} · com pose fora da caixa (>15 cm) {num2}" + ((list.Count > 0) ? (" · " + string.Join(" | ", list)) : ""));
	}

	private IEnumerator Audit(string map)
	{
		Camera cam = Camera.main;
		if (!cam)
		{
			Log(map + ": sem câmera");
			yield break;
		}
		FBPlayer pl = FBPlayer.I;
		if ((bool)pl)
		{
			pl.enabled = false;
		}
		int maskKeep = cam.cullingMask;
		int maskScene = maskKeep & -1025 & -16385;
		int maskAll = maskKeep & -16385;
		SkinnedBounds(map);
		bool ocKeep = cam.useOcclusionCulling;
		float lodKeep = QualitySettings.lodBias;
		int maxLodKeep = QualitySettings.maximumLODLevel;
		List<Vector3> list = FBNav.Points();
		if (list == null || list.Count == 0)
		{
			Log(map + ": sem pontos de navegação");
			yield break;
		}
		List<Vector3> pick = new List<Vector3> { list[0] };
		while (pick.Count < Mathf.Min(points, list.Count))
		{
			pick.Add(list.OrderByDescending((Vector3 p) => pick.Min((Vector3 q) => (p - q).sqrMagnitude)).First());
		}
		RenderTexture rt = new RenderTexture(320, 180, 24, RenderTextureFormat.ARGB32)
		{
			name = "audit"
		};
		rt.Create();
		List<Sample> res = new List<Sample>();
		float ocSum = 0f;
		float lodSum = 0f;
		int n0 = 0;
		for (int pi = 0; pi < pick.Count; pi++)
		{
			Vector3 g = pick[pi];
			for (int v = 0; v < 2; v++)
			{
				for (int i = 0; i < 6; i++)
				{
					Vector3 vector = Quaternion.Euler(0f, i * 60 + pi * 17, 0f) * Vector3.forward;
					Vector3 eye;
					if (v == 0)
					{
						eye = g + Vector3.up * 1.6f;
					}
					else
					{
						eye = g + Vector3.up * 2.4f - vector * 3.2f;
						Vector3 vector2 = g + Vector3.up * 1.7f;
						if (Physics.Linecast(vector2, eye, out var hitInfo, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
						{
							eye = hitInfo.point + (vector2 - eye).normalized * 0.3f;
						}
					}
					Quaternion rot = Quaternion.LookRotation(Quaternion.AngleAxis((v == 0) ? 2 : 8, Vector3.Cross(vector, Vector3.up)) * vector);
					cam.transform.SetPositionAndRotation(eye, rot);
					QualitySettings.lodBias = lodKeep;
					QualitySettings.maximumLODLevel = maxLodKeep;
					yield return Wait(0.65f);
					cam.cullingMask = maskAll;
					cam.useOcclusionCulling = true;
					Texture2D A = Grab();
					cam.useOcclusionCulling = false;
					Texture2D B = Grab();
					cam.cullingMask = maskScene;
					Texture2D B2 = Grab();
					QualitySettings.lodBias = 50f;
					QualitySettings.maximumLODLevel = 0;
					yield return Wait(0.65f);
					Texture2D texture2D = Grab();
					Sample sample = default(Sample);
					sample.name = string.Format("{0} p{1} d{2}", (v == 0) ? "fp" : "tps", pi, i);
					sample.pos = eye;
					sample.rot = rot;
					sample.ocDiff = Diff(A, B);
					sample.lodDiff = Diff(B2, texture2D);
					sample.a = A;
					sample.c = texture2D;
					Sample item = sample;
					ocSum += item.ocDiff;
					lodSum += item.lodDiff;
					n0++;
					UnityEngine.Object.Destroy(B);
					UnityEngine.Object.Destroy(B2);
					res.Add(item);
					List<Sample> list2 = res.OrderByDescending((Sample x) => Mathf.Max(x.ocDiff, x.lodDiff)).Take(6).ToList();
					foreach (Sample item2 in res)
					{
						if (!list2.Contains(item2))
						{
							if ((bool)item2.a)
							{
								UnityEngine.Object.Destroy(item2.a);
							}
							if ((bool)item2.c)
							{
								UnityEngine.Object.Destroy(item2.c);
							}
						}
					}
					res = list2;
				}
			}
		}
		Log($"{map}: {n0} vistas · oclusão corta (A×B) média {ocSum / (float)Mathf.Max(1, n0):0.00}% · LOD/corte (B×C) média {lodSum / (float)Mathf.Max(1, n0):0.00}% · bias {lodKeep:0.00} maxLOD {maxLodKeep} · oclusão na câmera {ocKeep}");
		int num = 0;
		foreach (Sample item3 in res)
		{
			Log($"{map}: pior {num}: {item3.name} em {item3.pos} · oclusão {item3.ocDiff:0.0}% · LOD {item3.lodDiff:0.0}%");
			Texture2D texture2D2 = new Texture2D(item3.a.width * 2 + 4, item3.a.height, TextureFormat.RGB24, mipChain: false);
			Color32[] pixels = Enumerable.Repeat(new Color32(255, 0, 255, 255), texture2D2.width * texture2D2.height).ToArray();
			texture2D2.SetPixels32(pixels);
			texture2D2.SetPixels(0, 0, item3.a.width, item3.a.height, item3.a.GetPixels());
			texture2D2.SetPixels(item3.a.width + 4, 0, item3.c.width, item3.c.height, item3.c.GetPixels());
			texture2D2.Apply();
			File.WriteAllBytes(Path.Combine(dir, $"render_{map}_{num++}.png"), texture2D2.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D2);
		}
		cam.useOcclusionCulling = ocKeep;
		cam.cullingMask = maskKeep;
		QualitySettings.lodBias = lodKeep;
		QualitySettings.maximumLODLevel = maxLodKeep;
		rt.Release();
		if ((bool)pl)
		{
			pl.enabled = true;
		}
		static float Diff(Texture2D x, Texture2D y)
		{
			Color32[] pixels2 = x.GetPixels32();
			Color32[] pixels3 = y.GetPixels32();
			int num2 = 0;
			for (int j = 0; j < pixels2.Length; j++)
			{
				if (Mathf.Abs(pixels2[j].r - pixels3[j].r) + Mathf.Abs(pixels2[j].g - pixels3[j].g) + Mathf.Abs(pixels2[j].b - pixels3[j].b) > 60)
				{
					num2++;
				}
			}
			return 100f * (float)num2 / (float)pixels2.Length;
		}
		Texture2D Grab()
		{
			RenderTexture targetTexture = cam.targetTexture;
			cam.targetTexture = rt;
			cam.Render();
			cam.targetTexture = targetTexture;
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			Texture2D texture2D3 = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, mipChain: false);
			texture2D3.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
			texture2D3.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			return texture2D3;
		}
	}
}
}
