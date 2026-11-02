using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBR37Test : MonoBehaviour
{
	private string map = "vila_jardim";

	private string mode = "tatico";

	private float secs = 60f;

	private string dir;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbr37");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_R37Teste");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBR37Test fBR37Test = obj.AddComponent<FBR37Test>();
			string[] array = ((num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-")) ? commandLineArgs[num + 1].Split(':') : new string[1] { "vila_jardim" });
			fBR37Test.map = array[0];
			fBR37Test.mode = ((array.Length > 1) ? array[1] : "tatico");
			if (num + 2 < commandLineArgs.Length && float.TryParse(commandLineArgs[num + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				fBR37Test.secs = result;
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][r37t] " + s);
	}

	private IEnumerator Shot(string name)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
		yield return null;
		yield return null;
	}

	private IEnumerator MotoTest(FBCharacter me, string tag)
	{
		FBCar car = FBVehicles.Spawn("xj6", me.Pos + me.transform.forward * 4f, me.transform.eulerAngles.y);
		if (!car)
		{
			Log("moto: xj6 não nasceu");
			yield break;
		}
		yield return null;
		bool ok = FBDrive.Enter(me, car);
		float t = 0f;
		Animator an = me.an;
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.move = ((t > 2.5f && t < 5.5f) ? new Vector2(0f, 1f) : Vector2.zero);
			inp.look = ((t > 7f && t < 9.5f) ? new Vector2(4f, 0.3f) : Vector2.zero);
			inp.fire = (inp.firePressed = (inp.aim = (inp.aimPressed = (inp.freeLook = false))));
			return inp;
		};
		(float, string)[] shots = new(float, string)[4]
		{
			(2f, "parado"),
			(5f, "andando"),
			(7f, "parou"),
			(10f, "camera_girada")
		};
		int si = 0;
		float handMax = 0f;
		float headMax = 0f;
		for (; t < 10.5f; t += Time.deltaTime)
		{
			yield return null;
			FBDriverPose component = me.GetComponent<FBDriverPose>();
			FBDrive.PoseTargets poseTargets = (component ? component.Last : default(FBDrive.PoseTargets));
			if (poseTargets.hands && (bool)an)
			{
				float magnitude = (an.GetBoneTransform(HumanBodyBones.LeftHand).position - poseTargets.hL).magnitude;
				float magnitude2 = (an.GetBoneTransform(HumanBodyBones.RightHand).position - poseTargets.hR).magnitude;
				if (t > 1f)
				{
					handMax = Mathf.Max(handMax, Mathf.Max(magnitude, magnitude2));
				}
			}
			Transform transform = (an ? an.GetBoneTransform(HumanBodyBones.Head) : null);
			if ((bool)transform && t > 1f)
			{
				headMax = Mathf.Max(headMax, Vector3.Angle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.ProjectOnPlane(car.transform.forward, Vector3.up)));
			}
			if (si < shots.Length && t >= shots[si].Item1)
			{
				yield return Shot("moto_" + tag + "_" + shots[si].Item2);
				si++;
			}
		}
		int layerIndex = an.GetLayerIndex("Upper Body");
		int layerIndex2 = an.GetLayerIndex("Emote Full Body");
		Log($"moto ({tag}): entrou {ok} · pose {me.sys} · camada corpo inteiro {((layerIndex2 >= 0) ? an.GetLayerWeight(layerIndex2) : (-1f)):0.00} · camada braços {((layerIndex >= 0) ? an.GetLayerWeight(layerIndex) : (-1f)):0.00} · InCombat {me.CombatStance} · mãos longe do guidão até {handMax * 100f:0} cm · cabeça virada até {headMax:0}° da frente da moto");
		FBPlayer.TestInput = null;
		FBDrive.Exit(me, force: true);
		for (float w = 0f; w < 0.8f; w += Time.deltaTime)
		{
			yield return null;
		}
		FBVehicles.list.Remove(car);
		UnityEngine.Object.Destroy(car.gameObject);
		yield return null;
	}

	private IEnumerator MotoFromFp(FBCharacter me)
	{
		FBPlayer P = FBPlayer.I;
		if (!P)
		{
			yield break;
		}
		bool tv = !P.FirstPerson;
		float t = 0f;
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.toggleView = tv;
			tv = false;
			inp.move = ((t < 1.5f) ? new Vector2(0f, 1f) : Vector2.zero);
			inp.look = ((t > 4.5f && t < 5.7f) ? new Vector2(5f, 0f) : Vector2.zero);
			inp.fire = (inp.aim = (inp.freeLook = (inp.interact = false)));
			return inp;
		};
		for (; t < 2f; t += Time.deltaTime)
		{
			yield return null;
		}
		Log($"moto da 1ª pessoa: em 1ª pessoa {P.FirstPerson}");
		FBCar car = FBVehicles.Spawn("start", me.Pos + me.transform.forward * 3f, me.transform.eulerAngles.y);
		if (!car)
		{
			car = FBVehicles.Spawn("xj6", me.Pos + me.transform.forward * 3f, me.transform.eulerAngles.y);
		}
		if ((bool)car)
		{
			CharacterController component = me.GetComponent<CharacterController>();
			if ((bool)component)
			{
				component.enabled = false;
			}
			me.transform.position = car.transform.position - car.transform.right * 1.2f;
			if ((bool)component)
			{
				component.enabled = true;
			}
		}
		if (!car)
		{
			FBPlayer.TestInput = null;
			yield break;
		}
		yield return null;
		bool ok = FBDrive.Enter(me, car);
		for (; t < 4f; t += Time.deltaTime)
		{
			yield return null;
		}
		yield return Shot("moto_da_1p_atras");
		GameObject go = new GameObject("CamMoto1p");
		Camera camera = go.AddComponent<Camera>();
		camera.depth = 50f;
		camera.fieldOfView = 40f;
		Vector3 vector = car.transform.position + Vector3.up * 1.1f;
		camera.transform.position = vector - car.transform.right * 3.6f + Vector3.up * 0.3f;
		camera.transform.LookAt(vector);
		yield return Shot("moto_da_1p_lado");
		UnityEngine.Object.Destroy(go);
		for (; t < 6.2f; t += Time.deltaTime)
		{
			yield return null;
		}
		yield return Shot("moto_da_1p_frente");
		int num = 0;
		int num2 = 0;
		Renderer[] componentsInChildren = me.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			num2++;
			if (renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !renderer.forceRenderingOff)
			{
				num++;
			}
		}
		FBDriverPose component2 = me.GetComponent<FBDriverPose>();
		FBDrive.PoseTargets poseTargets = (component2 ? component2.Last : default(FBDrive.PoseTargets));
		Animator an = me.an;
		float num3 = 0f;
		if (poseTargets.hands && (bool)an)
		{
			num3 = Mathf.Max((an.GetBoneTransform(HumanBodyBones.LeftHand).position - poseTargets.hL).magnitude, (an.GetBoneTransform(HumanBodyBones.RightHand).position - poseTargets.hR).magnitude);
		}
		Transform transform = (an ? an.GetBoneTransform(HumanBodyBones.RightUpperArm) : null);
		Transform transform2 = (an ? an.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null);
		int num4 = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count((Renderer r) => (bool)r && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && r.gameObject.layer == 14);
		Log(string.Format("moto da 1ª pessoa ({0}): objetos de 1ª pessoa ainda visíveis no mundo {1} · entrou {2} · pose {3} · camada corpo {4:0.00} · renderizadores do corpo visíveis {5}/{6} · mãos longe do guidão {7:0} cm · escala braço d/e {8:0.00}/{9:0.00}", car.id, num4, ok, me.sys, an.GetLayerWeight(an.GetLayerIndex("Emote Full Body")), num, num2, num3 * 100f, transform ? transform.lossyScale.x : 0f, transform2 ? transform2.lossyScale.x : 0f));
		FBPlayer.TestInput = null;
		FBDrive.Exit(me, force: true);
		for (float w = 0f; w < 0.8f; w += Time.deltaTime)
		{
			yield return null;
		}
		if (P.FirstPerson)
		{
			P.ForceThirdPerson();
		}
		FBVehicles.list.Remove(car);
		UnityEngine.Object.Destroy(car.gameObject);
		yield return null;
	}

	private IEnumerator MotoParked(FBCharacter me)
	{
		List<FBCar> list = (from c in FBVehicles.list
			where (bool)c && c.kind == "moto" && !c.driver
			orderby (c.transform.position - me.Pos).sqrMagnitude
			select c).Take(3).ToList();
		Log(string.Format("motos estacionadas no mapa: {0}", FBVehicles.list.Count((FBCar c) => (bool)c && c.kind == "moto")));
		int j = 0;
		foreach (FBCar car in list)
		{
			j++;
			Vector3 position = car.transform.position - car.transform.right * 2.2f;
			CharacterController component = me.GetComponent<CharacterController>();
			if ((bool)component)
			{
				component.enabled = false;
			}
			me.transform.position = position;
			if ((bool)component)
			{
				component.enabled = true;
			}
			float t = 0f;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			if (!FBDrive.Enter(me, car))
			{
				Log("moto do mapa " + car.id + ": não entrou");
				continue;
			}
			FBPlayer.TestInput = delegate(FBInputState inp)
			{
				inp.move = Vector2.zero;
				inp.interact = false;
				inp.look = ((t > 3f && t < 4.2f) ? new Vector2(5f, -0.6f) : Vector2.zero);
				inp.fire = (inp.aim = (inp.freeLook = false));
				return inp;
			};
			for (; t < 5f; t += Time.deltaTime)
			{
				yield return null;
				if (t > 2.4f && t < 2.4f + Time.deltaTime * 1.5f)
				{
					yield return Shot($"moto_mapa{j}_{car.id}_atras");
				}
			}
			yield return Shot($"moto_mapa{j}_{car.id}_frente");
			Animator an = me.an;
			FBDrive.PoseTargets poseTargets = me.GetComponent<FBDriverPose>()?.Last ?? default(FBDrive.PoseTargets);
			float num = 0f;
			if (poseTargets.hands && (bool)an)
			{
				num = Mathf.Max((an.GetBoneTransform(HumanBodyBones.LeftHand).position - poseTargets.hL).magnitude, (an.GetBoneTransform(HumanBodyBones.RightHand).position - poseTargets.hR).magnitude);
			}
			Log($"moto do mapa {car.id}: dirigindo {FBVehicles.Driving(me)} · pose {me.sys} · mãos longe do guidão {num * 100f:0} cm · grau {car.wheelie:0.00} · manobra {car.trick:0.00}");
			FBPlayer.TestInput = null;
			if (FBVehicles.Driving(me))
			{
				FBDrive.Exit(me, force: true);
			}
			for (float w = 0f; w < 0.8f; w += Time.deltaTime)
			{
				yield return null;
			}
		}
	}

	private IEnumerator MotoSkins(FBCharacter me)
	{
		if (!FBMatch.I || !FBRegistry.I)
		{
			yield break;
		}
		GameObject go = new GameObject("CamMoto");
		Camera cam = go.AddComponent<Camera>();
		cam.depth = 50f;
		cam.fieldOfView = 40f;
		Vector3 basePos = me.Pos + me.transform.right * 8f;
		float yaw = me.transform.eulerAngles.y;
		List<string> worst = new List<string>();
		List<string> list = (from x in GameData.Skins
			where x.id.StartsWith("r_") && FBRegistry.I.HasChar(x.id)
			select x.id).ToList();
		if (list.Count > 1)
		{
			list.Add(list[0]);
			list.RemoveAt(0);
		}
		foreach (string sk in list)
		{
			FBCar car = FBVehicles.Spawn("xj6", basePos, yaw);
			if (!car)
			{
				Log("moto: xj6 não nasceu");
				break;
			}
			FBCharacter a = FBMatch.I.Spawn(sk, "", 0, basePos + Vector3.up * 0.2f, player: false);
			if (!a)
			{
				FBVehicles.list.Remove(car);
				UnityEngine.Object.Destroy(car.gameObject);
				continue;
			}
			FBBot[] components = a.GetComponents<FBBot>();
			for (int i = 0; i < components.Length; i++)
			{
				UnityEngine.Object.Destroy(components[i]);
			}
			yield return null;
			FBDrive.SeatTest(a, car);
			float hm = 0f;
			Animator an = a.an;
			for (float w = 0f; w < 1.4f; w += Time.deltaTime)
			{
				yield return null;
				FBDrive.PoseTargets poseTargets = a.GetComponent<FBDriverPose>()?.Last ?? default(FBDrive.PoseTargets);
				if (w > 0.8f && poseTargets.hands && (bool)an)
				{
					hm = Mathf.Max(hm, Mathf.Max((an.GetBoneTransform(HumanBodyBones.LeftHand).position - poseTargets.hL).magnitude, (an.GetBoneTransform(HumanBodyBones.RightHand).position - poseTargets.hR).magnitude));
				}
			}
			Vector3 c = car.transform.position + Vector3.up * 1.1f;
			cam.transform.position = c - car.transform.right * 3.6f + Vector3.up * 0.3f;
			cam.transform.LookAt(c);
			yield return Shot("motoskin_" + sk + "_lado");
			cam.transform.position = c + car.transform.forward * 3.4f - car.transform.right * 1.2f + Vector3.up * 0.2f;
			cam.transform.LookAt(c);
			yield return Shot("motoskin_" + sk + "_frente");
			Log($"moto {sk}: mãos longe do guidão até {hm * 100f:0} cm");
			if (hm > 0.05f)
			{
				worst.Add($"{sk} {hm * 100f:0} cm");
			}
			a.gameObject.SetActive(value: false);
			UnityEngine.Object.Destroy(a.gameObject);
			FBVehicles.list.Remove(car);
			UnityEngine.Object.Destroy(car.gameObject);
			yield return null;
		}
		UnityEngine.Object.Destroy(go);
		Log("moto (todas as skins): " + ((worst.Count == 0) ? "todas com as mãos no guidão" : ("mãos fora: " + string.Join(", ", worst))));
	}

	private IEnumerator FpAds(FBCharacter me)
	{
		FBPlayer P = FBPlayer.I;
		if (!P || !P.wu || !FBRegistry.I)
		{
			yield break;
		}
		List<string> ids = new List<string>();
		string[] array = new string[4] { "fuzil", "submetralhadora", "pistola", "escopeta" };
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
		ids.AddRange(from x in GameData.Weapons.Values
			where x.cls == "sniper" && (bool)FBRegistry.I.Weapon(x.id)
			select x.id);
		bool tv = true;
		bool aimOn = false;
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.toggleView = tv;
			tv = false;
			inp.aim = aimOn;
			inp.aimPressed = false;
			inp.fire = (inp.firePressed = false);
			inp.move = Vector2.zero;
			inp.look = Vector2.zero;
			inp.freeLook = false;
			return inp;
		};
		for (int k = 0; k < 6; k++)
		{
			yield return null;
		}
		foreach (string id in ids)
		{
			P.wu.Strip();
			P.wu.Give(id);
			int num = Array.FindIndex(P.wu.slots, (WeaponDef x) => x != null && x.id == id);
			P.wu.Equip((num >= 0) ? num : 0, instant: true);
			aimOn = false;
			for (float w2 = 0f; w2 < 1.2f; w2 += Time.deltaTime)
			{
				yield return null;
			}
			aimOn = true;
			for (float w2 = 0f; w2 < 1.4f; w2 += Time.deltaTime)
			{
				yield return null;
			}
			FBFpRig componentInChildren = P.GetComponentInChildren<FBFpRig>(includeInactive: true);
			int num2 = 0;
			int num3 = 0;
			if ((bool)componentInChildren)
			{
				Renderer[] componentsInChildren = componentInChildren.GetComponentsInChildren<Renderer>(includeInactive: true);
				foreach (Renderer renderer in componentsInChildren)
				{
					num3++;
					if (renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy)
					{
						num2++;
					}
				}
			}
			Log(string.Format("1ª pessoa mirando {0}: luneta {1} · rig de 1ª pessoa {2} · renderizadores visíveis {3}/{4}", id, FBScopes.Scoped, (!componentInChildren) ? "nenhum" : (componentInChildren.gameObject.activeInHierarchy ? "ativo" : "inativo"), num2, num3));
			yield return Shot("fp_mira_" + id);
		}
		aimOn = false;
		tv = true;
		for (int k = 0; k < 6; k++)
		{
			yield return null;
		}
		FBPlayer.TestInput = null;
		P.wu.Strip();
		P.wu.Give(FBSession.primary, FBSession.smg, FBSession.secondary);
		P.wu.Equip(0, instant: true);
		for (int k = 0; k < 10; k++)
		{
			yield return null;
		}
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste", "r37");
		Directory.CreateDirectory(dir);
		float w;
		for (w = 0f; w < 4f; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		GameData.Load();
		FBSession.Load();
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
		for (w = 0f; w < 5f; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		FBPlayer P = FBPlayer.I;
		FBCharacter me = FBMatch.Player;
		if (!P || !me)
		{
			Log("FALHA: partida sem jogador");
			Application.Quit(1);
			yield break;
		}
		FBMatch.God = true;
		w = P.camYaw;
		float t = 0f;
		List<float> heads = new List<float>();
		float camMax = 0f;
		FBPlayer.TestInput = delegate(FBInputState inp)
		{
			inp.move = new Vector2(0f, 1f);
			inp.freeLook = t > 0.6f && t < 2.6f;
			inp.freeLookPressed = false;
			inp.look = ((t > 0.7f && t < 2.4f) ? new Vector2(2.2f, 0.25f) : Vector2.zero);
			inp.fire = (inp.firePressed = (inp.aim = (inp.aimPressed = false)));
			return inp;
		};
		bool shot2 = false;
		for (; t < 2.6f; t += Time.deltaTime)
		{
			yield return null;
			Vector3 vector = me.velocity.Flat();
			if (vector.magnitude > 0.5f && t > 0.8f)
			{
				heads.Add(Mathf.Atan2(vector.x, vector.z) * 57.29578f);
			}
			camMax = Mathf.Max(camMax, Mathf.Abs(Mathf.DeltaAngle(P.ViewYaw, P.camYaw)));
			if (!shot2 && t > 1.4f)
			{
				shot2 = true;
				yield return Shot("camera_livre_olhando");
			}
		}
		float hMin = ((heads.Count > 0) ? heads.Min() : 0f);
		float hMax3 = ((heads.Count > 0) ? heads.Max() : 0f);
		float back2;
		for (back2 = 0f; back2 < 3f; back2 += Time.deltaTime)
		{
			if (!(Mathf.Abs(Mathf.DeltaAngle(P.ViewYaw, P.camYaw)) > 2f))
			{
				break;
			}
			yield return null;
			t += Time.deltaTime;
		}
		for (; t < 4.2f; t += Time.deltaTime)
		{
			yield return null;
		}
		FBPlayer.TestInput = null;
		yield return Shot("camera_livre_voltou");
		Log($"câmera livre: câmera girou até {camMax:0}° do corpo · rumo do movimento variou {Mathf.DeltaAngle(hMin, hMax3):0.0}° · yaw do corpo {Mathf.DeltaAngle(w, P.camYaw):0.0}° · voltou em {back2:0.00} s");
		yield return MotoTest(me, "armado");
		if ((bool)FBPlayer.I && (bool)FBPlayer.I.wu)
		{
			FBPlayer.I.wu.Strip();
			me.SetUnarmedLocomotion(on: true);
			for (int k = 0; k < 10; k++)
			{
				yield return null;
			}
		}
		yield return MotoTest(me, "desarmado");
		yield return MotoFromFp(me);
		yield return MotoParked(me);
		yield return MotoSkins(me);
		if ((bool)FBPlayer.I && (bool)FBPlayer.I.wu)
		{
			FBPlayer.I.wu.Give(FBSession.primary, FBSession.smg, FBSession.secondary);
			FBPlayer.I.wu.Equip(0, instant: true);
			for (int k = 0; k < 10; k++)
			{
				yield return null;
			}
		}
		yield return FpAds(me);
		FBWeather.ForceNight(on: true);
		for (hMax3 = 0f; hMax3 < 2.5f; hMax3 += Time.deltaTime)
		{
			yield return null;
		}
		_ = Camera.main;
		int li = -1;
		back2 = 1E+09f;
		IReadOnlyList<FBStreetLights.Lamp> lamps = FBStreetLights.Lamps;
		HashSet<int> hashSet = new HashSet<int>(from h in FBLampHead.All
			where (bool)h && h.lamp >= 0
			select h.lamp);
		Log($"luminárias: {lamps.Count} no mapa · {hashSet.Count} com cabeça apagável · {FBLampHead.All.Count((FBLampHead h) => (bool)h && h.lamp < 0)} cabeças sem luminária ligada");
		for (int m = 0; m < lamps.Count; m++)
		{
			if (hashSet.Contains(m))
			{
				float magnitude = (lamps[m].p - me.Pos).magnitude;
				if (magnitude < back2 && magnitude > 4f)
				{
					back2 = magnitude;
					li = m;
				}
			}
		}
		if (li < 0)
		{
			for (int n = 0; n < lamps.Count; n++)
			{
				float magnitude2 = (lamps[n].p - me.Pos).magnitude;
				if (magnitude2 < back2)
				{
					back2 = magnitude2;
					li = n;
				}
			}
		}
		if (li >= 0)
		{
			Vector3 lp = lamps[li].p;
			GameObject go = new GameObject("CamLuz");
			Camera camera = go.AddComponent<Camera>();
			camera.depth = 50f;
			camera.fieldOfView = 45f;
			camera.transform.position = lp + (me.Pos - lp).Flat().normalized * 7f + Vector3.down * 2.5f;
			camera.transform.LookAt(lp);
			for (int k = 0; k < 20; k++)
			{
				yield return null;
			}
			yield return Shot("luminaria_antes");
			FBStreetPoleFx.Ray(lp + Vector3.down * 3f, Vector3.up, 6f);
			for (hMax3 = 0f; hMax3 < 1.5f; hMax3 += Time.deltaTime)
			{
				yield return null;
			}
			yield return Shot("luminaria_depois");
			List<FBLampHead> list = FBLampHead.All.Where((FBLampHead h) => (bool)h && h.lamp == li).ToList();
			string text = "sem cabeça separada (bloco no renderizador do poste)";
			if (list.Count > 0)
			{
				MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
				list[0].GetComponent<Renderer>().GetPropertyBlock(materialPropertyBlock, 0);
				text = string.Format("{0} cabeça(s), emissão no bloco {1}", list.Count, materialPropertyBlock.GetColor("_EmissionColor"));
			}
			Log($"luminária {li} a {back2:0} m: apagada {FBStreetLights.IsDead(li)} · {text}");
			UnityEngine.Object.Destroy(go);
		}
		else
		{
			Log("luminária: nenhuma no mapa");
		}
		FBWeather.ForceNight(on: false);
		FBCombatStats.On = true;
		FBCombatStats.botShots.Clear();
		FBCombatStats.playerHits.Clear();
		shot2 = false;
		bool front = false;
		back2 = 0f;
		FBMatch.God = false;
		me.maxHp = Mathf.Max(me.maxHp, 100000f);
		me.hp = me.maxHp;
		for (hMax3 = 0f; hMax3 < secs; hMax3 += Time.deltaTime)
		{
			yield return null;
			me.hp = me.maxHp;
			float num;
			back2 = (num = back2 - Time.deltaTime);
			if (num <= 0f)
			{
				back2 = 10f;
				Log($"t={hMax3:0} s · {FBBot.Diag()} · tiros de bots {FBCombatStats.botShots.Count}");
			}
			if (!front && hMax3 > secs * 0.5f)
			{
				FBCharacter fBCharacter = FBMatch.Bots.FirstOrDefault(delegate(FBCharacter b)
				{
					if ((bool)b && b.alive && !FBMatch.Hostile(b, me))
					{
						FBBot component2 = b.GetComponent<FBBot>();
						if ((object)component2 != null)
						{
							if (!(component2.ai == "combat"))
							{
								return component2.ai == "cover";
							}
							return true;
						}
					}
					return false;
				});
				if ((bool)fBCharacter)
				{
					front = true;
					CharacterController component = me.GetComponent<CharacterController>();
					if ((bool)component)
					{
						component.enabled = false;
					}
					me.transform.position = fBCharacter.Pos + fBCharacter.transform.right * 1.6f;
					if ((bool)component)
					{
						component.enabled = true;
					}
					Log($"t={hMax3:0} s · jogador levado para perto de {fBCharacter.Name} (em combate)");
				}
			}
			if (!shot2 && FBCombatStats.playerHits.Count > 0)
			{
				shot2 = true;
				for (int k = 0; k < 3; k++)
				{
					yield return null;
				}
				yield return Shot("indicador_dano");
			}
		}
		FBCombatStats.On = false;
		List<(float d, bool hit)> S = FBCombatStats.botShots;
		Log($"bots: {S.Count} tiros · mais longe {((S.Count > 0) ? S.Max(((float d, bool hit) s) => s.d) : 0f):0} m · " + string.Join(" · ", Band(0f, 15f), Band(15f, 35f), Band(35f, 60f), Band(60f, 100f), Band(100f, 999f)));
		List<(float d, string who, bool head)> playerHits = FBCombatStats.playerHits;
		Log(string.Format("jogador atingido {0}× · distâncias {1} · mais longe {2:0} m", playerHits.Count, string.Join(",", from h in playerHits.Take(30)
			select h.d.ToString("0")), (playerHits.Count > 0) ? playerHits.Max<(float, string, bool)>(((float d, string who, bool head) h) => h.d) : 0f));
		Log("fim");
		yield return null;
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbauto") < 0)
		{
			Application.Quit(0);
		}
		string Band(float a, float b)
		{
			List<(float, bool)> list2 = S.Where(((float d, bool hit) s) => s.d >= a && s.d < b).ToList();
			return $"{a:0}–{b:0} m: {list2.Count} tiros, {list2.Count<(float, bool)>(((float d, bool hit) s) => s.hit)} acertos ({((list2.Count > 0) ? (100f * (float)list2.Count<(float, bool)>(((float d, bool hit) s) => s.hit) / (float)list2.Count) : 0f):0}%)";
		}
	}
}
}
