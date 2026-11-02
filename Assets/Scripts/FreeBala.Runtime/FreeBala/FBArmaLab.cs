using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBArmaLab : MonoBehaviour
{
	private string wid;

	private string dir;

	private bool aim;

	private bool fire;

	private bool crouchT;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbarmalab");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_ArmaLab");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBArmaLab fBArmaLab = obj.AddComponent<FBArmaLab>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				fBArmaLab.wid = commandLineArgs[num + 1];
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][armalab] " + s);
	}

	private IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.deltaTime)
		{
			yield return null;
		}
	}

	private static Vector3? TopOf(Transform t)
	{
		Renderer[] componentsInChildren = t.GetComponentsInChildren<Renderer>();
		if (componentsInChildren.Length == 0)
		{
			return null;
		}
		Bounds bounds = componentsInChildren[0].bounds;
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		return new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.08f, bounds.center.z);
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "arma");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		GameData.Load();
		FBSession.Load();
		(string map, string mode, string view) keep = (map: FBSession.map, mode: FBSession.mode, view: FBSession.view);
		FBSession.map = "vila_jardim";
		FBSession.mode = "classico";
		FBSession.online = false;
		FBSession.view = "fp";
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_vila_jardim");
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		for (float w3 = 0f; w3 < 25f; w3 += Time.deltaTime)
		{
			if ((bool)FBPlayer.I && (bool)FBPlayer.I.wu)
			{
				break;
			}
			yield return null;
		}
		FBPlayer P = FBPlayer.I;
		if (!P)
		{
			Log("sem jogador");
			yield break;
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
		bool tv = !P.firstPerson;
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.toggleView = tv;
			tv = false;
			inp.aim = aim;
			inp.aimPressed = false;
			inp.fire = fire;
			inp.firePressed = fire;
			inp.move = Vector2.zero;
			inp.look = Vector2.zero;
			inp.crouchToggle = crouchT;
			crouchT = false;
			inp.reload = false;
			inp.slot = -1;
			inp.cycle = 0;
			return inp;
		};
		WeaponDef value;
		WeaponDef d = ((wid != null && GameData.Weapons.TryGetValue(wid, out value)) ? value : GameData.Weapons.Values.FirstOrDefault(delegate(WeaponDef x)
		{
			if (x.cls == "fuzil" && x.auto && (bool)FBRegistry.I.Weapon(x.id))
			{
				List<object> list3 = Json.L(Json.O(Json.O(GameData.WeaponsRaw, x.id), "att"), "scopes");
				if (list3 == null)
				{
					return false;
				}
				return list3.Count >= 6;
			}
			return false;
		}));
		if (d == null)
		{
			Log("sem arma");
			yield break;
		}
		Dictionary<string, object> att = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att");
		List<object> scopes = Json.L(att, "scopes") ?? new List<object>();
		Log(string.Format("arma {0} ({1}) · {2} miras · {3} empunhaduras · {4} lasers", d.id, d.name, scopes.Count, Json.L(att, "grips")?.Count ?? 0, Json.L(att, "lasers")?.Count ?? 0));
		Camera cam = (P.cam ? P.cam : Camera.main);
		Vector2 C;
		for (int si2 = -1; si2 < scopes.Count; si2++)
		{
			FBWeaponModel.Choice choice = FBWeaponModel.Default(d);
			choice.scope = si2;
			yield return Equip(choice);
			aim = true;
			yield return Wait(1.3f);
			string text = ((si2 < 0) ? "ferro" : Json.S(scopes[si2], "node"));
			FBFpRig fBFpRig = P.GetComponentInChildren<FBFpRig>(includeInactive: true) ?? UnityEngine.Object.FindAnyObjectByType<FBFpRig>();
			Transform transform = null;
			if ((bool)fBFpRig)
			{
				Transform[] componentsInChildren = fBFpRig.GetComponentsInChildren<Transform>(includeInactive: true);
				foreach (Transform transform2 in componentsInChildren)
				{
					if (transform2.name == text && transform2.gameObject.activeInHierarchy)
					{
						transform = transform2;
						break;
					}
				}
			}
			string text2 = "sem mira medida";
			if ((bool)transform)
			{
				C = new Vector2((float)Screen.width / 2f, (float)Screen.height / 2f);
				Vector3? w4 = null;
				Vector3? w5 = null;
				Renderer[] componentsInChildren2 = transform.GetComponentsInChildren<Renderer>();
				foreach (Renderer renderer in componentsInChildren2)
				{
					Material sharedMaterial = renderer.sharedMaterial;
					bool num = Regex.IsMatch((renderer.name + " " + (sharedMaterial ? sharedMaterial.name : "")).ToLowerInvariant(), "reticle|retic|dot|ponto|red|holo|mark|cross") || ((bool)sharedMaterial && sharedMaterial.IsKeywordEnabled("_EMISSION"));
					bool flag = (bool)sharedMaterial && (sharedMaterial.renderQueue >= 2450 || sharedMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
					if (num && !w4.HasValue)
					{
						w4 = renderer.bounds.center;
					}
					else if (flag && !w5.HasValue)
					{
						w5 = renderer.bounds.center;
					}
				}
				text2 = "eixo óptico (SOCKET_Scope) " + Px(fBFpRig ? fBFpRig.SightWorld : null) + " · retículo " + Px(w4) + " · lente " + Px(w5) + " · topo da peça " + Px(TopOf(transform));
			}
			Log($"mira {text}: ADS {text2} · zoom {FBScopes.Zoom(d):0.0}x · luneta do HUD {FBScopes.Scoped}");
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{d.id}_{si2}_{text}.png"));
			yield return null;
			yield return null;
			aim = false;
			yield return Wait(0.4f);
		}
		List<object> lasers = Json.L(att, "lasers") ?? new List<object>();
		for (int si2 = 0; si2 < lasers.Count; si2++)
		{
			FBWeaponModel.Choice choice2 = FBWeaponModel.Default(d);
			choice2.laser = si2;
			yield return Equip(choice2);
			string ln2 = Json.S(lasers[si2], "node");
			aim = false;
			yield return Wait(0.6f);
			int b0 = FBLaser.LitBeams;
			int l0 = FBLaser.LitLamps;
			aim = true;
			yield return Wait(1.2f);
			int b2 = FBLaser.LitBeams;
			int l2 = FBLaser.LitLamps;
			ScreenCapture.CaptureScreenshot(Path.Combine(dir, d.id + "_laser_" + ln2 + ".png"));
			yield return null;
			yield return null;
			aim = false;
			yield return Wait(0.6f);
			int litBeams = FBLaser.LitBeams;
			Log($"laser {ln2}: sem mirar feixes {b0} lanternas {l0} · mirando feixes {b2} lanternas {l2} · soltou a mira feixes {litBeams}");
		}
		FBRecoil.TestNoSpread = true;
		List<object> list = Json.L(att, "grips") ?? new List<object>();
		List<(string, int, bool)> list2 = new List<(string, int, bool)> { ("nenhuma", -1, false) };
		for (int j = 0; j < list.Count; j++)
		{
			string text3 = Json.S(list[j], "node");
			list2.Add((text3, j, false));
			if (text3.Contains("Bipod"))
			{
				list2.Add((text3 + " agachado", j, true));
			}
		}
		float baseRise = 0f;
		foreach (var item2 in list2)
		{
			string ln2 = item2.Item1;
			int si2 = item2.Item2;
			bool cr = item2.Item3;
			FBWeaponModel.Choice c2 = FBWeaponModel.Default(d);
			c2.grip = si2;
			yield return Equip(c2);
			if (cr)
			{
				crouchT = true;
				yield return Wait(0.6f);
			}
			aim = true;
			yield return Wait(1f);
			float w3 = P.camPitch;
			float y0 = P.camYaw;
			int l2 = P.wu.Ammo;
			fire = true;
			float maxRise2 = 0f;
			float maxSide = 0f;
			for (float t = 0f; t < 5f; t += Time.deltaTime)
			{
				if (l2 - P.wu.Ammo >= 15)
				{
					break;
				}
				maxRise2 = Mathf.Max(maxRise2, Mathf.Abs(Mathf.DeltaAngle(w3, P.camPitch)));
				maxSide = Mathf.Max(maxSide, Mathf.Abs(Mathf.DeltaAngle(y0, P.camYaw)));
				yield return null;
			}
			fire = false;
			int b2 = l2 - P.wu.Ammo;
			yield return Wait(0.2f);
			maxRise2 = Mathf.Max(maxRise2, Mathf.Abs(Mathf.DeltaAngle(w3, P.camPitch)));
			if (si2 < 0)
			{
				baseRise = maxRise2;
			}
			FBWeaponModel.Mods mods = FBWeaponModel.ModsOf(d, c2, cr);
			Log($"empunhadura {ln2}: {b2} tiros · subida máx {maxRise2:0.00}° ({((baseRise > 0f) ? (maxRise2 / baseRise * 100f) : 100f):0}% da sem nada) · lateral máx {maxSide:0.00}° · esperado recuo V×{mods.recV:0.00} H×{mods.recH:0.00}");
			aim = false;
			if (cr)
			{
				crouchT = true;
				yield return Wait(0.6f);
			}
			yield return Wait(0.8f);
		}
		FBRecoil.TestNoSpread = false;
		FBPlayer.TestInput = null;
		FBWeaponModel.Set(d, FBWeaponModel.Default(d));
		(FBSession.map, FBSession.mode, FBSession.view) = keep;
		FBSession.Save();
		Log("fim");
		yield return Wait(1f);
		Application.Quit();
		IEnumerator Equip(FBWeaponModel.Choice c)
		{
			FBWeaponModel.Set(d, c);
			P.wu.Strip();
			P.wu.Give(d.id);
			int num2 = Array.FindIndex(P.wu.slots, (WeaponDef x) => x != null && x.id == d.id);
			P.wu.Equip((num2 >= 0) ? num2 : 0, instant: true);
			yield return Wait(1.4f);
		}
		string Px(Vector3? w)
		{
			if (!w.HasValue)
			{
				return "-";
			}
			Vector3 vector = cam.WorldToScreenPoint(w.Value);
			return $"{Vector2.Distance(new Vector2(vector.x, vector.y), C):0.0} px ({vector.x - C.x:+0;-0},{vector.y - C.y:+0;-0})";
		}
	}
}
}
