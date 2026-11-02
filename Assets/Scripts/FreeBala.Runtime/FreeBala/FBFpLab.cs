using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBFpLab : MonoBehaviour
{
	private string[] skins;

	private string dir;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbfplab");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_FpLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBFpLab fBFpLab = obj.AddComponent<FBFpLab>();
			string text = ((num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-")) ? commandLineArgs[num + 1] : "padrao");
			fBFpLab.skins = ((!(text == "padrao")) ? text.Split(',') : new string[8] { "r_tutu", "r_elaine", "r_giga", "r_pm_silva", "r_vivi", "r_miguel", "r_aka_junin", "r_mikele" });
			FBCharArms.Legacy = Array.IndexOf(commandLineArgs, "-fbfpold") >= 0;
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][fplab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.deltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "fplab");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		GameData.Load();
		FBSession.Load();
		(string map, string mode, string skin, string view) keep = (map: FBSession.map, mode: FBSession.mode, skin: FBSession.skin, view: FBSession.view);
		List<string> ids = new List<string>();
		string[] array = new string[5] { "fuzil", "submetralhadora", "pistola", "escopeta", "sniper" };
		foreach (string cls in array)
		{
			string text = (from x in GameData.Weapons.Values
				where x.cls == cls && (bool)FBRegistry.I.Weapon(x.id)
				select x.id).FirstOrDefault();
			if (text != null)
			{
				ids.Add(text);
			}
		}
		Log("armas " + string.Join(",", ids) + " · modo " + (FBCharArms.Legacy ? "ANTIGO (torção pela mão)" : "novo"));
		string[] array2 = skins;
		foreach (string sk in array2)
		{
			FBSession.map = "vila_jardim";
			FBSession.mode = "classico";
			FBSession.skin = sk;
			FBSession.online = false;
			FBSession.view = "fp";
			FBSession.Save();
			AsyncOperation op = SceneManager.LoadSceneAsync("Map_vila_jardim");
			while (op != null && !op.isDone)
			{
				yield return null;
			}
			for (float w = 0f; w < 25f; w += Time.deltaTime)
			{
				if ((bool)FBPlayer.I && (bool)FBPlayer.I.wu)
				{
					break;
				}
				yield return null;
			}
			FBPlayer P = FBPlayer.I;
			if (!P || !P.wu)
			{
				Log(sk + ": sem jogador");
				continue;
			}
			yield return Wait(2f);
			FBMatch.God = true;
			foreach (FBCharacter item in FBMatch.Bots.ToList())
			{
				if ((bool)item)
				{
					item.gameObject.SetActive(value: false);
				}
			}
			bool aim = false;
			bool run = false;
			bool rel = false;
			bool tv = !P.firstPerson;
			FBPlayer.TestInput = delegate(FBInputState inp)
			{
				inp.toggleView = tv;
				tv = false;
				inp.aim = aim;
				inp.aimPressed = false;
				inp.fire = (inp.firePressed = false);
				inp.move = (run ? new Vector2(0f, 1f) : Vector2.zero);
				inp.sprintHeld = run;
				inp.look = Vector2.zero;
				inp.reload = rel;
				rel = false;
				return inp;
			};
			yield return Wait(0.5f);
			Camera side = new GameObject("FpSide").AddComponent<Camera>();
			side.depth = 200f;
			side.cullingMask = 16384;
			side.clearFlags = CameraClearFlags.Color;
			side.backgroundColor = new Color(0.35f, 0.38f, 0.42f);
			side.nearClipPlane = 0.01f;
			side.fieldOfView = 40f;
			side.enabled = false;
			foreach (string id in ids)
			{
				P.wu.Strip();
				P.wu.Give(id);
				int num = Array.FindIndex(P.wu.slots, (WeaponDef x) => x != null && x.id == id);
				P.wu.Equip((num >= 0) ? num : 0, instant: true);
				yield return Wait(1.3f);
				string[] array3 = new string[4] { "parado", "mirando", "recarga", "correndo" };
				foreach (string st in array3)
				{
					aim = st == "mirando";
					run = st == "correndo";
					if (st == "recarga")
					{
						rel = true;
					}
					yield return Wait((st == "recarga") ? 0.9f : 1.1f);
					FBCharArms fBCharArms = P.GetComponentInChildren<FBCharArms>(includeInactive: true) ?? UnityEngine.Object.FindAnyObjectByType<FBCharArms>();
					string text2 = (fBCharArms ? fBCharArms.TwistReport() : "sem braços do personagem");
					Log(sk + " " + id + " " + st + ": " + text2);
					yield return Snap(sk + "_" + id + "_" + st + "_cam.png", null);
					Transform transform = (P.cam ? P.cam.transform : Camera.main.transform);
					side.transform.position = transform.position + transform.right * 0.95f + transform.forward * 0.25f + transform.up * 0.05f;
					side.transform.LookAt(transform.position + transform.forward * 0.35f - transform.up * 0.12f);
					yield return Snap(sk + "_" + id + "_" + st + "_lado.png", side);
					run = false;
					aim = false;
				}
			}
			UnityEngine.Object.Destroy(side.gameObject);
			FBPlayer.TestInput = null;
		}
		(FBSession.map, FBSession.mode, FBSession.skin, FBSession.view) = keep;
		FBSession.Save();
		Log("fim");
		yield return Wait(1f);
		Application.Quit();
	}

	private IEnumerator Snap(string f, Camera only)
	{
		if ((bool)only)
		{
			only.enabled = true;
		}
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, f));
		yield return null;
		yield return null;
		if ((bool)only)
		{
			only.enabled = false;
		}
	}
}
}
