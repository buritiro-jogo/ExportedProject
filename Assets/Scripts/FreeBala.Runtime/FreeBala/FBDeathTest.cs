using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBDeathTest : MonoBehaviour
{
	private class Case
	{
		public string id;

		public Vector3 shotDir;

		public bool head;

		public bool crouch;

		public bool runFwd;

		public bool wall;

		public string why;
	}

	private string run;

	private string dir;

	private Camera cam;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbmorte");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_MorteTeste");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<FBDeathTest>().run = ((num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-")) ? commandLineArgs[num + 1] : "vila_jardim:classico");
			int num2 = Array.IndexOf(commandLineArgs, "-fbflexsign");
			if (num2 >= 0 && num2 + 1 < commandLineArgs.Length)
			{
				FBRagdoll.FlexForce = (FBRagdoll.FlexSign = int.Parse(commandLineArgs[num2 + 1]));
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][morte-teste] " + s);
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
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		yield return Wait(3f);
		string[] array = run.Split(':');
		GameData.Load();
		FBSession.Load();
		string keepMap = FBSession.map;
		string keepMode = FBSession.mode;
		FBSession.map = array[0];
		FBSession.mode = ((array.Length > 1) ? array[1] : "classico");
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + array[0]);
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		yield return Wait(5f);
		FBMatch.God = true;
		FBCharacter P = FBMatch.Player;
		foreach (FBCharacter item in FBMatch.Bots.ToList())
		{
			if ((bool)item)
			{
				item.gameObject.SetActive(value: false);
			}
		}
		GameObject gameObject = new GameObject("MorteCam");
		cam = gameObject.AddComponent<Camera>();
		cam.depth = 100f;
		cam.cullingMask = (Camera.main ? Camera.main.cullingMask : (-1));
		cam.nearClipPlane = 0.05f;
		Log(string.Format("mapa {0} · biblioteca {1} quedas · FlexSign {2}", run, FBRegistry.I.clips.Count((AnimationClip c) => (bool)c && c.name.StartsWith("X171_death")), FBRagdoll.FlexSign));
		List<Case> list = new List<Case>();
		for (int j = 0; j < 8; j++)
		{
			float num = j * 45;
			list.Add(new Case
			{
				id = $"pe_{num:000}",
				shotDir = Quaternion.Euler(0f, num, 0f) * Vector3.forward
			});
		}
		list.Add(new Case
		{
			id = "agachado_frente",
			shotDir = Vector3.back,
			crouch = true
		});
		list.Add(new Case
		{
			id = "agachado_costas",
			shotDir = Vector3.forward,
			crouch = true
		});
		list.Add(new Case
		{
			id = "correndo_costas",
			shotDir = Vector3.forward,
			runFwd = true
		});
		list.Add(new Case
		{
			id = "correndo_frente",
			shotDir = Vector3.back,
			runFwd = true
		});
		list.Add(new Case
		{
			id = "cabeca_frente",
			shotDir = Vector3.back,
			head = true
		});
		list.Add(new Case
		{
			id = "cabeca_lado",
			shotDir = Vector3.right,
			head = true
		});
		list.Add(new Case
		{
			id = "explosao",
			shotDir = Vector3.left,
			why = "explosion"
		});
		list.Add(new Case
		{
			id = "parede_atras",
			shotDir = Vector3.back,
			wall = true
		});
		list.Add(new Case
		{
			id = "parede_frente",
			shotDir = Vector3.forward,
			wall = true
		});
		string[] skins = new string[6] { "r_tutu", "r_elaine", "r_giga", "r_mikele", "r_pm_silva", "r_vivi" };
		int bad = 0;
		int i = 0;
		int ci = 0;
		Dictionary<string, int> picks = new Dictionary<string, int>();
		foreach (Case c2 in list)
		{
			string skin = skins[ci % skins.Length];
			UnityEngine.Random.InitState(4200 + ci++);
			if (!Place(P ? P.Pos : Vector3.zero, c2.wall, c2.shotDir, out var spot, out var yaw))
			{
				Log(c2.id + ": sem lugar");
				continue;
			}
			FBCharacter v = FBMatch.I.Spawn(skin, null, 1, spot, player: false, bot: false, civ: true);
			MonoBehaviour[] components = v.GetComponents<MonoBehaviour>();
			foreach (MonoBehaviour monoBehaviour in components)
			{
				if (monoBehaviour is FBCivilian || monoBehaviour is FBRoleNpc || monoBehaviour is FBBot)
				{
					monoBehaviour.enabled = false;
				}
			}
			FBMatch.Teleport(v, spot, yaw);
			v.SetUnarmedLocomotion(on: true, force: true);
			yield return Wait(0.6f);
			if (c2.crouch)
			{
				v.crouch = true;
				yield return Wait(1.2f);
			}
			if (c2.runFwd)
			{
				for (float t2 = 0f; t2 < 1.4f; t2 += Time.deltaTime)
				{
					v.sprint = true;
					v.velocity = v.transform.forward * 6.5f;
					CharacterController component = v.GetComponent<CharacterController>();
					if ((bool)component && component.enabled)
					{
						component.Move(v.velocity * Time.deltaTime + Vector3.down * 0.2f);
					}
					else
					{
						v.transform.position += v.velocity * Time.deltaTime;
					}
					yield return null;
				}
			}
			Vector3 vector = Quaternion.Euler(0f, yaw, 0f) * c2.shotDir;
			vector.y = 0f;
			vector.Normalize();
			Vector3 point = ((c2.head && (bool)v.Head) ? v.Head.position : (v.Chest ? v.Chest.position : (v.Pos + Vector3.up * 1.2f)));
			FBHitInfo fBHitInfo = default(FBHitInfo);
			fBHitInfo.valid = c2.why == null;
			fBHitInfo.point = point;
			fBHitInfo.dir = vector;
			fBHitInfo.normal = -vector;
			fBHitInfo.part = (c2.head ? "head" : "body");
			fBHitInfo.dist = 12f;
			FBHitInfo hit = fBHitInfo;
			Look(v.Pos + Quaternion.Euler(0f, 90f, 0f) * vector * 4.2f + Vector3.up * 1.8f, v.Pos + Vector3.up * 0.6f, 60f);
			FBCombat.DamageActor(v, 999f, c2.head, null, c2.why, hit);
			string pick = v.deathClip ?? "(sem clipe)";
			string why = FBDeath.LastWhy;
			picks[pick] = ((!picks.TryGetValue(pick, out var value)) ? 1 : (value + 1));
			FBRagdoll rd = v.GetComponent<FBRagdoll>();
			float worst = 999f;
			string worstJ = "-";
			float t3 = Time.time;
			bool shotA = false;
			for (float t2 = 0f; t2 < 13f; t2 += Time.deltaTime)
			{
				if ((bool)rd && rd.Done)
				{
					break;
				}
				if (!shotA && t2 > 0.7f)
				{
					Snap("morte_" + c2.id + "_a.png");
					shotA = true;
				}
				yield return new WaitForEndOfFrame();
				if (!rd || !rd.Simulating)
				{
					continue;
				}
				foreach (var item2 in rd.Hinges())
				{
					if (item2.flex < worst)
					{
						worst = item2.flex;
						(worstJ, _, _) = item2;
					}
				}
			}
			yield return Wait(0.3f);
			float a0min = 999f;
			string a0j = "-";
			if ((bool)rd)
			{
				foreach (var item3 in rd.HingesAtStart())
				{
					if (item3.a0 < a0min)
					{
						a0min = item3.a0;
						(a0j, _) = item3;
					}
				}
			}
			float num3;
			if ((bool)v.Hips)
			{
				float? num2 = FBWorld.GroundY(v.Hips.position.x, v.Hips.position.y + 0.4f, v.Hips.position.z, 0.3f, 2f);
				if (num2.HasValue)
				{
					float valueOrDefault = num2.GetValueOrDefault();
					num3 = valueOrDefault;
					goto IL_0c17;
				}
			}
			num3 = v.Pos.y;
			goto IL_0c17;
			IL_0c17:
			float num4 = num3;
			float hipH = (v.Hips ? (v.Hips.position.y - num4) : 0f);
			Snap("morte_" + c2.id + "_b.png");
			yield return null;
			bool flag = (rd == null || rd.Done) && worst > -12f && hipH > -0.05f && hipH < 0.45f;
			if (!flag)
			{
				bad++;
			}
			i++;
			Log(string.Format("{0} [{1}]: {2} · {3} · assentou {4} em {5:0.0} s · quadril {6:0.00} m do chão · pior dobra {7} · no início da física {8} {9}", c2.id, skin, pick, why, ((bool)rd && rd.Done) ? $"{rd.SimTime:0.0} s de física" : "NÃO", Time.time - t3, hipH, (worst > 900f) ? "-" : $"{worst:0}° ({worstJ})", (a0min > 900f) ? "-" : $"{a0min:0}° ({a0j})", flag ? "ok" : "<< PROBLEMA"));
			yield return Wait(0.5f);
			UnityEngine.Object.Destroy(v.gameObject);
			FBMatch.All.Remove(v);
			FBMatch.Civs.Remove(v);
		}
		Log($"fim · {i} mortes · {bad} com problema · clipes distintos {picks.Count} · ragdolls montados {FBRagdoll.Built} assentados {FBRagdoll.Settled} · trava anatômica {FBRagdoll.AnatomyFixes} correções");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		yield return Wait(1f);
		Application.Quit();
	}

	private bool Place(Vector3 near, bool wall, Vector3 shotLocal, out Vector3 spot, out float yaw)
	{
		spot = near;
		yaw = 0f;
		for (int i = 0; i < 400; i++)
		{
			Vector3 vector = FBNav.RandomPoint(near, 25f) ?? (near + UnityEngine.Random.insideUnitSphere.Flat() * 20f);
			if ((vector - near).Flat().magnitude < 5f)
			{
				continue;
			}
			float? num = FBWorld.GroundY(vector.x, vector.y + 1f, vector.z, 1f, 3f);
			if (!num.HasValue)
			{
				continue;
			}
			float valueOrDefault = num.GetValueOrDefault();
			vector.y = valueOrDefault;
			if (!wall)
			{
				if (!Physics.CheckSphere(vector + Vector3.up * 1.1f, 3.2f, FBWorld.SolidMask & -257, QueryTriggerInteraction.Ignore))
				{
					bool flag = true;
					for (int j = 0; j < 8 && flag; j++)
					{
						flag = !Physics.Raycast(vector + Vector3.up * 0.9f, Quaternion.Euler(0f, j * 45, 0f) * Vector3.forward, 4f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
					}
					if (flag)
					{
						yaw = UnityEngine.Random.Range(0f, 360f);
						spot = vector;
						return true;
					}
				}
				continue;
			}
			for (int k = 0; k < 8; k++)
			{
				Vector3 vector2 = Quaternion.Euler(0f, k * 45, 0f) * Vector3.forward;
				if (!Physics.Raycast(vector + Vector3.up * 1f, vector2, out var hitInfo, 2.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(hitInfo.normal.y) > 0.3f)
				{
					continue;
				}
				Vector3 vector3 = hitInfo.point - vector2 * 0.55f;
				vector3.y = vector.y;
				num = FBWorld.GroundY(vector3.x, vector3.y + 1f, vector3.z, 1f, 2f);
				if (num.HasValue)
				{
					float valueOrDefault2 = num.GetValueOrDefault();
					if (!(Mathf.Abs(valueOrDefault2 - vector.y) > 0.2f))
					{
						Vector3 normalized = shotLocal.Flat().normalized;
						float num2 = Mathf.Atan2(vector2.x, vector2.z) * 57.29578f - Mathf.Atan2(normalized.x, normalized.z) * 57.29578f;
						yaw = num2;
						spot = vector3;
						return true;
					}
				}
			}
		}
		return false;
	}

	private void Look(Vector3 from, Vector3 at, float fov)
	{
		cam.transform.position = from;
		cam.transform.rotation = Quaternion.LookRotation(at - from);
		cam.fieldOfView = fov;
	}

	private void Snap(string f)
	{
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, f));
	}
}
}
