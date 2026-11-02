using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBScreensTest : MonoBehaviour
{
	private string map = "vila_jardim";

	private string mode = "tatico";

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbtelas");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_TesteTelas");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBScreensTest fBScreensTest = obj.AddComponent<FBScreensTest>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				string[] array = commandLineArgs[num + 1].Split(':');
				fBScreensTest.map = array[0];
				fBScreensTest.mode = ((array.Length > 1) ? array[1] : "tatico");
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][telas] " + s);
	}

	private IEnumerator Shot(string dir, string name)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, name));
		yield return null;
		yield return null;
		Log("print " + name);
	}

	private IEnumerator Start()
	{
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		yield return new WaitForSeconds(3f);
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
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		for (float w2 = 0f; w2 < 8f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		FBCharacter P = FBMatch.Player;
		FBCharacter killer = (from b in FBMatch.Bots
			where (bool)b && b.alive && b.team != P.team
			orderby (b.Pos - P.Pos).sqrMagnitude
			select b).FirstOrDefault();
		Log("jogador " + (P ? P.Name : "-") + " · quem vai abater: " + (killer ? killer.Name : "ninguém"));
		FBMatch.Stats.kills = 7;
		FBMatch.Stats.deaths = 2;
		FBMatch.Stats.heads = 3;
		FBMatch.Stats.shots = 140;
		FBMatch.Stats.hits = 61;
		for (int i = 0; i < 6; i++)
		{
			if (!P.alive)
			{
				break;
			}
			FBCombat.DamageActor(P, 999f, i == 0, killer, "gun");
			yield return new WaitForSeconds(0.3f);
		}
		Log($"jogador vivo {P.alive} · estado {FBMatch.I.state}");
		for (float w2 = 0f; w2 < 4.2f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Shot(dir, "tela_abate.png");
		for (float w2 = 0f; w2 < 1.5f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Shot(dir, "tela_abate_2.png");
		FBMatch.I.Respawn();
		Camera cam2 = new GameObject("CamBolha").AddComponent<Camera>();
		cam2.depth = 50f;
		cam2.fieldOfView = 40f;
		cam2.nearClipPlane = 0.05f;
		cam2.enabled = false;
		for (float w2 = 0f; w2 < 0.18f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Both("bolha_1_entrada");
		for (float w2 = 0f; w2 < 0.9f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Both("bolha_2_meio");
		Log($"proteção até {P.protUntil - Time.time:0.00} s");
		while (P.protUntil - Time.time > 0.3f)
		{
			yield return null;
		}
		yield return Both("bolha_3_fim");
		Log($"renasceu: vivo {P.alive} · estado {FBMatch.I.state} · tela aberta {FBHUD.CardOpen}");
		FBWeaponUser wu = P.GetComponent<FBWeaponUser>();
		int rs = -1;
		for (int k = 0; k < 3; k++)
		{
			if (wu.slots[k] != null && wu.slots[k].cls == "fuzil")
			{
				rs = k;
			}
		}
		if (rs >= 0)
		{
			wu.Equip(3, instant: true);
			for (float w2 = 0f; w2 < 1.2f; w2 += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			string rid = wu.slots[rs].id;
			wu.SwitchTo(rs);
			int i = 0;
			int frames = 0;
			for (float w2 = 0f; w2 < 1.6f; w2 += Time.unscaledDeltaTime)
			{
				yield return null;
				frames++;
				bool num = (bool)P.weaponGO && P.weaponGO.activeInHierarchy && P.weapon != null && P.weapon.id == rid;
				bool flag = P.HolsterObjects.Any((GameObject o) => (bool)o && o.activeInHierarchy && o.name == "Coldre_" + rid);
				if (num && flag)
				{
					i++;
				}
				if (frames % 12 == 0)
				{
					Vector3 vector = P.Pos + Vector3.up * 1.2f * P.ScaleK;
					cam2.transform.position = vector + Quaternion.Euler(0f, P.Yaw + 150f, 0f) * Vector3.forward * 2.6f * P.ScaleK;
					cam2.transform.LookAt(vector);
					cam2.enabled = true;
					yield return Shot(dir, $"saque_{frames:000}.png");
					cam2.enabled = false;
				}
			}
			Log($"saque do fuzil: {i} de {frames} quadros com DUAS armas (mão + costas)");
		}
		UnityEngine.Object.Destroy(cam2.gameObject);
		FBMatch.I.EndMatch(win: true, "CONQUISTA", 250, 187);
		for (float w2 = 0f; w2 < 8f; w2 += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		yield return Shot(dir, "tela_fim_vitoria.png");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		Log("fim");
		Application.Quit(0);
		IEnumerator Both(string n)
		{
			yield return Shot(dir, n + "_jogo.png");
			Vector3 vector2 = P.Pos + Vector3.up * 1.1f * P.ScaleK;
			cam2.transform.position = vector2 + Quaternion.Euler(0f, P.Yaw + 25f, 0f) * Vector3.forward * 3.4f * P.ScaleK + Vector3.up * 0.5f * P.ScaleK;
			cam2.transform.LookAt(vector2);
			cam2.enabled = true;
			yield return Shot(dir, n + "_perto.png");
			cam2.enabled = false;
		}
	}
}
}
