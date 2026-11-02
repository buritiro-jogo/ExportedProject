using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBNpcLab : MonoBehaviour
{
	private string map = "vila_jardim";

	private string dir;

	private Camera cam;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbnpclab");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_NpcLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBNpcLab fBNpcLab = obj.AddComponent<FBNpcLab>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				fBNpcLab.map = commandLineArgs[num + 1];
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][npclab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Shot(Vector3 pos, Vector3 look, string n, float fov = 50f)
	{
		cam.transform.position = pos;
		cam.transform.LookAt(look);
		cam.fieldOfView = fov;
		for (int i = 0; i < 3; i++)
		{
			yield return null;
		}
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "npc_" + n + ".png"));
		yield return null;
	}

	private IEnumerator Side(FBCharacter c, string n, float dist = 3.2f)
	{
		if ((bool)c)
		{
			Vector3 vector = Quaternion.Euler(0f, c.Yaw, 0f) * Vector3.forward;
			Vector3 vector2 = Vector3.Cross(Vector3.up, vector);
			yield return Shot(c.Pos + vector2 * dist + vector * 0.8f + Vector3.up * 1.3f, c.Pos + Vector3.up * 0.95f, n);
		}
	}

	private FBRoleNpc R(FBCharacter c)
	{
		if (!c)
		{
			return null;
		}
		return c.GetComponent<FBRoleNpc>();
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		GameData.Load();
		FBSession.Load();
		string keepMap = FBSession.map;
		string keepMode = FBSession.mode;
		FBSession.map = map;
		FBSession.mode = "role";
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + map);
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		yield return Wait(8f);
		FBMatch.God = true;
		FBCharacter me = FBMatch.Player;
		if (!me)
		{
			Log("sem jogador");
			yield break;
		}
		cam = new GameObject("NpcCam").AddComponent<Camera>();
		cam.depth = 100f;
		cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
		cam.nearClipPlane = 0.05f;
		Log($"rotina ligada {FBNpcRotina.On} · falas prontas {FBNpcVoz.Ready} · NPCs {FBRole.npcs.Count((FBCharacter n) => n)}");
		List<FBCharacter> withAct = FBRole.npcs.Where((FBCharacter n) => (bool)n && FBNpcRotina.ActOf(n) != null).ToList();
		Log($"com atividade: {withAct.Count} — " + string.Join(", ", from n in withAct
			group n by FBNpcRotina.ActOf(n).id into g
			select g.Key + " " + g.Count()));
		int p2 = FBNpcRotina.Pauses;
		int c0 = FBNpcRotina.Convs;
		int s0 = FBNpcVoz.Said;
		int r0 = FBNpcRotina.Reacts;
		Dictionary<string, int> seenSt = new Dictionary<string, int>();
		for (float t3 = 0f; t3 < 60f; t3 += 1f)
		{
			foreach (FBCharacter npc in FBRole.npcs)
			{
				FBRoleNpc fBRoleNpc = R(npc);
				if ((bool)fBRoleNpc)
				{
					seenSt[fBRoleNpc.st] = ((!seenSt.TryGetValue(fBRoleNpc.st, out var value)) ? 1 : (value + 1));
				}
			}
			yield return Wait(1f);
		}
		Log($"60 s: pausas {FBNpcRotina.Pauses - p2} · rodas de conversa {FBNpcRotina.Convs - c0} · falas {FBNpcVoz.Said - s0} (última {FBNpcVoz.LastLine}) · reações {FBNpcRotina.Reacts - r0}");
		Log("estados (NPC×s): " + string.Join(", ", from kv in seenSt
			orderby kv.Value descending
			select kv.Key + " " + kv.Value));
		foreach (IGrouping<string, FBCharacter> g2 in (from n in withAct
			where R(n)?.st == "walk"
			group n by FBNpcRotina.ActOf(n).id).Take(4))
		{
			FBCharacter j = g2.First();
			yield return Side(j, "andar_" + g2.Key);
			Log($"foto andar_{g2.Key}: {j.skinId} ({j.npcSet?.sex}) clipe {j.npcSet?.walk} vel {j.walkSpeed:0.00}");
		}
		FBCharacter c = FBRole.npcs.FirstOrDefault((FBCharacter n) => (bool)n && FBNpcRotina.ActOf(n) == null && R(n)?.st == "walk" && n.npcSet?.sex == "m");
		yield return Side(c, "andar_normal_m");
		FBCharacter pa = FBRole.npcs.FirstOrDefault((FBCharacter n) => (bool)n && R(n)?.st == "pause");
		if ((bool)pa)
		{
			yield return Side(pa, "pausa", 2.6f);
			Log("foto pausa: " + pa.skinId + " clipe " + pa.dance?.clip);
		}
		else
		{
			Log("nenhum NPC em pausa agora");
		}
		FBCharacter fBCharacter = FBRole.npcs.FirstOrDefault((FBCharacter n) => (bool)n && R(n)?.st == "talk" && R(n).talkC.HasValue);
		if ((bool)fBCharacter)
		{
			Vector3 C = R(fBCharacter).talkC.Value;
			yield return Shot(C + new Vector3(3.2f, 1.5f, 2.4f), C + Vector3.up * 1f, "conversa");
			Log("foto conversa: " + string.Join(", ", from n in FBRole.npcs.Where(delegate(FBCharacter n)
				{
					if ((bool)n)
					{
						FBRoleNpc fBRoleNpc2 = R(n);
						if ((object)fBRoleNpc2 == null)
						{
							return false;
						}
						return fBRoleNpc2.talkC == C;
					}
					return false;
				})
				select n.skinId + ":" + n.dance?.clip));
		}
		List<FBCharacter> A = (from n in FBRole.npcs
			where (bool)n && n.alive && (bool)R(n) && R(n).Free
			orderby (n.Pos - me.Pos).sqrMagnitude
			select n).ToList();
		if (A.Count >= 2)
		{
			int cv0 = FBNpcRotina.Convs;
			_ = FBNpcVoz.Said;
			R(A[0]).TalkWith(R(A[1]));
			List<string> lines = new List<string>();
			string last = null;
			for (float t3 = 0f; t3 < 14f; t3 += Time.deltaTime)
			{
				if (FBNpcVoz.LastLine != last)
				{
					last = FBNpcVoz.LastLine;
					if (last != null && last.StartsWith("conv_"))
					{
						lines.Add(last);
					}
				}
				yield return null;
			}
			Vector3 vector = (A[0].Pos + A[1].Pos) / 2f;
			yield return Shot(vector + new Vector3(2.6f, 1.5f, 2f), vector + Vector3.up, "conversa_forcada");
			Log(string.Format("conversa forçada: {0} ({1}) × {2} ({3}) · rodas +{4} · falas: {5} · clipes {6}/{7}", A[0].skinId, FBNpcVoz.VozOf(A[0]), A[1].skinId, FBNpcVoz.VozOf(A[1]), FBNpcRotina.Convs - cv0, string.Join(" → ", lines), A[0].dance?.clip, A[1].dance?.clip));
		}
		CharacterController cc = me.GetComponent<CharacterController>();
		FBCharacter b = Pick();
		if ((bool)b)
		{
			Vector3 vector2 = Quaternion.Euler(0f, b.Yaw, 0f) * Vector3.forward;
			Put(b.Pos + vector2 * 1.6f);
			me.Yaw = Quaternion.LookRotation(-vector2).eulerAngles.y;
			FBPlayer.TestInput = delegate(FBInputState inp)
			{
				if ((bool)FBPlayer.I && (bool)b)
				{
					FBPlayer.I.camYaw = Quaternion.LookRotation((b.Pos - me.Pos).Flat()).eulerAngles.y;
				}
				inp.move = new Vector2(0f, 1f);
				return inp;
			};
			for (float t3 = 0f; t3 < 1.2f; t3 += Time.deltaTime)
			{
				if (!(R(b)?.st != "react"))
				{
					break;
				}
				yield return null;
			}
			FBPlayer.TestInput = null;
			yield return Wait(0.35f);
			yield return Side(b, "esbarrao", 2.8f);
			Log("esbarrão: " + b.skinId + " estado " + R(b).st + " clipe " + b.dance?.clip + " · fala " + FBNpcVoz.LastLine);
			yield return Wait(2f);
		}
		FBCharacter i = Pick();
		if ((bool)i)
		{
			Put(i.Pos + Quaternion.Euler(0f, i.Yaw, 0f) * Vector3.forward * 6f);
			me.GetComponent<FBWeaponUser>()?.Give("38");
			yield return Wait(0.5f);
			FBPlayer.TestInput = delegate(FBInputState inp)
			{
				if ((bool)FBPlayer.I && (bool)i)
				{
					Camera main = Camera.main;
					Vector3 v = i.Pos + Vector3.up * 1.2f * i.ScaleK - (main ? main.transform.position : (me.Pos + Vector3.up * 1.6f));
					FBPlayer.I.camYaw = Quaternion.LookRotation(v.Flat()).eulerAngles.y;
					FBPlayer.I.camPitch = (0f - Mathf.Atan2(v.y, v.Flat().magnitude)) * 57.29578f;
				}
				inp.aim = true;
				return inp;
			};
			for (float t3 = 0f; t3 < 2.5f; t3 += Time.deltaTime)
			{
				if (!(R(i)?.st != "scared"))
				{
					break;
				}
				yield return null;
			}
			yield return Side(i, "mira", 3f);
			Log($"mira: {i.skinId} estado {R(i).st} mãos para cima {i.surrender} · jogador mirando {me.aiming} armado {!me.Unarmed} · fala {FBNpcVoz.LastLine}");
			FBPlayer.TestInput = null;
			yield return Wait(4f);
			Log("depois da mira: estado " + R(i).st);
		}
		List<FBCharacter> near = FBRole.npcs.Where((FBCharacter n) => (bool)n && n.alive && (n.Pos - me.Pos).magnitude < 30f).ToList();
		FBBotBrain.Noise(me.Pos);
		yield return Wait(1.5f);
		Log(string.Format("tiro: {0} de {1} correndo · fala {2}", near.Count((FBCharacter n) => R(n)?.st == "flee"), near.Count, FBNpcVoz.LastLine));
		FBCharacter fBCharacter2 = near.FirstOrDefault((FBCharacter n) => R(n)?.st == "flee");
		if ((bool)fBCharacter2)
		{
			yield return Side(fBCharacter2, "fuga", 4f);
		}
		yield return Wait(8f);
		FBCharacter e = Pick(80f);
		if ((bool)e)
		{
			Vector3 p3 = e.Pos + Quaternion.Euler(0f, e.Yaw, 0f) * Vector3.forward * 12f;
			FBNpcRotina.Event("acidente", p3, 34f);
			yield return Wait(1.2f);
			Log(string.Format("batida: olhando {0} · fala {1}", FBRole.npcs.Count((FBCharacter n) => R(n)?.st == "look"), FBNpcVoz.LastLine));
			yield return Side(e, "batida_olhando", 3f);
		}
		Log($"total: falas {FBNpcVoz.Said} · reações {FBNpcRotina.Reacts} · pausas {FBNpcRotina.Pauses}");
		Log("fim");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		yield return Wait(1f);
		Application.Quit();
		FBCharacter Pick(float maxD = 60f)
		{
			return (from n in FBRole.npcs
				where (bool)n && n.alive && (bool)R(n) && R(n).Free && (n.Pos - me.Pos).magnitude < maxD
				orderby (n.Pos - me.Pos).sqrMagnitude
				select n).FirstOrDefault();
		}
		void Put(Vector3 p)
		{
			if ((bool)cc)
			{
				cc.enabled = false;
			}
			me.transform.position = p;
			if ((bool)cc)
			{
				cc.enabled = true;
			}
		}
	}
}
}
