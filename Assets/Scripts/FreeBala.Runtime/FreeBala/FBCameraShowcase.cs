using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FreeBala
{
public class FBCameraShowcase : MonoBehaviour
{
	private string dir;

	private int shots;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbcam") >= 0)
		{
			GameObject obj = new GameObject("FB_CamVitrine");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<FBCameraShowcase>();
		}
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		while (!FBMatch.I || !FBMatch.Player || !FBMatch.Player.alive || !FBPlayer.I || !(FBMatch.I.state == "play"))
		{
			yield return null;
		}
		yield return Wait(6f);
		FBCharacter me = FBMatch.Player;
		FBPlayer pl = FBPlayer.I;
		FBMatch.God = true;
		if (pl.FirstPerson)
		{
			pl.ForceThirdPerson();
		}
		FBCamLab.Install();
		Debug.Log("[FreeBala][cam] vitrine 3ª pessoa começando");
		Vector3 p0 = me.Pos;
		float yaw = TargetYaw(p0);
		(string cls, string[] pref)[] cLASSES = FBCamLab.CLASSES;
		for (int i = 0; i < cLASSES.Length; i++)
		{
			(string, string[]) tuple = cLASSES[i];
			string cls = tuple.Item1;
			string[] item = tuple.Item2;
			WeaponDef w = FBCamLab.Pick(cls, item);
			if (w == null)
			{
				Debug.Log("[FreeBala][cam] sem arma da classe " + cls);
				continue;
			}
			me.GetComponent<FBWeaponUser>().Give(w.id);
			yield return Wait(1.2f);
			yield return Pose(me, p0, yaw);
			yield return Snap(cls, "parado", 1f);
			FBRecoil.TestNoSpread = true;
			yield return ShotAndMark(pl, cls, "quadril");
			FBRecoil.TestNoSpread = false;
			FBCamLab.aim = true;
			yield return Snap(cls, "mira", 1f);
			FBRecoil.TestNoSpread = true;
			yield return ShotAndMark(pl, cls, "mira_tiro");
			FBRecoil.TestNoSpread = false;
			if (w.auto)
			{
				FBCamLab.fire = true;
				yield return Wait(0.35f);
				yield return Snap(cls, "rajada", 0f);
				FBCamLab.fire = false;
			}
			FBCamLab.aim = false;
			yield return Wait(0.8f);
			yield return Pose(me, p0, yaw);
			FBCamLab.move = new Vector2(0f, 1f);
			FBCamLab.ToggleRun();
			yield return Snap(cls, "correndo", 0.9f);
			FBCamLab.move = Vector2.zero;
			FBCamLab.ToggleRun();
			yield return Wait(0.6f);
			(string, Vector2, bool, bool, bool)[] array = new(string, Vector2, bool, bool, bool)[5]
			{
				("corre_mira", new Vector2(0f, 1f), true, true, false),
				("corre_tiro", new Vector2(0f, 1f), true, false, true),
				("mira_diagonal", new Vector2(0.71f, 0.71f), false, true, false),
				("mira_costas", new Vector2(0f, -1f), false, true, false),
				("andando", new Vector2(0f, 1f), false, false, false)
			};
			for (int j = 0; j < array.Length; j++)
			{
				(string, Vector2, bool, bool, bool) tuple2 = array[j];
				string nm = tuple2.Item1;
				Vector2 mv = tuple2.Item2;
				bool run = tuple2.Item3;
				bool am = tuple2.Item4;
				bool fr = tuple2.Item5;
				yield return Pose(me, p0, yaw);
				string tag = nm;
				yield return FBCamLab.Move(me, pl.cam, mv, run, am, fr, 1.6f, delegate(float dr, float dd)
				{
					Debug.Log($"[FreeBala][cam] {cls} {tag}: quadril até {dr * 100f:0} cm do personagem · distância câmera→quadril variou {dd * 100f:0} cm · sprint {me.sprint}");
				}, (float _) => Snap(cls, tag, 0f), 1.1f);
				yield return Wait(0.5f);
			}
			yield return Pose(me, p0, yaw);
			FBCamLab.ToggleCrouch();
			yield return Snap(cls, "agachado", 1f);
			FBCamLab.aim = true;
			yield return Snap(cls, "agachado_mira", 0.8f);
			FBCamLab.aim = false;
			FBCamLab.ToggleCrouch();
			yield return Wait(0.6f);
			FBCamLab.leanL = true;
			yield return Snap(cls, "inclina_q", 1f);
			FBCamLab.aim = true;
			yield return Snap(cls, "inclina_q_mira", 0.8f);
			FBCamLab.leanL = false;
			FBCamLab.aim = false;
			FBCamLab.leanR = true;
			yield return Snap(cls, "inclina_e", 1f);
			FBCamLab.aim = true;
			yield return Snap(cls, "inclina_e_mira", 0.8f);
			FBCamLab.leanR = false;
			FBCamLab.aim = false;
			yield return Wait(0.5f);
			Transform arena = new GameObject("ArenaCamera").transform;
			Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
			Vector3 rt = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
			GameObject wall = FBCamLab.Box(arena, "ParedeCostas", me.Pos - fw * 0.75f + Vector3.up * 2f, new Vector3(6f, 4f, 0.4f), alt: true);
			wall.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
			yield return Pose(me, p0, yaw);
			yield return Snap(cls, "parede", 1f);
			FBCamLab.aim = true;
			yield return Snap(cls, "parede_mira", 0.8f);
			FBCamLab.aim = false;
			Debug.Log($"[FreeBala][cam] {cls} parede: câmera {Vector3.Dot(pl.cam.transform.position - wall.transform.position, fw) - 0.2f:0.00} m à frente da parede (≥ 0 = não atravessou)");
			UnityEngine.Object.Destroy(wall);
			yield return null;
			FBCar car = FBVehicles.Spawn("S10_2017", me.Pos - fw * 2.6f + rt * 0.8f, yaw + 90f);
			if ((bool)car)
			{
				yield return Wait(0.3f);
				yield return Pose(me, p0, yaw);
				yield return Snap(cls, "carro", 1f);
				FBCamLab.aim = true;
				yield return Snap(cls, "carro_mira", 0.8f);
				FBCamLab.aim = false;
				bool flag = Physics.CheckSphere(pl.cam.transform.position, 0.05f, 2048, QueryTriggerInteraction.Ignore);
				Debug.Log($"[FreeBala][cam] {cls} carro: câmera dentro do veículo {flag}");
				FBVehicles.list.Remove(car);
				UnityEngine.Object.Destroy(car.gameObject);
				yield return null;
			}
			UnityEngine.Object.Destroy(arena.gameObject);
		}
		FBCamLab.Remove();
		Debug.Log($"[FreeBala][cam] vitrine fim: {shots} prints");
	}

	private IEnumerator Pose(FBCharacter me, Vector3 p, float yaw)
	{
		FBCamLab.Teleport(me, p, yaw);
		FBPlayer.I.camPitch = 2f;
		yield return Wait(0.5f);
	}

	private IEnumerator ShotAndMark(FBPlayer pl, string cls, string name)
	{
		yield return Wait(0.6f);
		FBCamLab.CrosshairHit(pl.cam, out var ch0);
		int j = pl.ShotCount;
		FBCamLab.Shoot();
		for (int i = 0; i < 4; i++)
		{
			if (pl.ShotCount != j)
			{
				break;
			}
			yield return null;
		}
		if (pl.ShotCount == j)
		{
			Debug.Log("[FreeBala][cam] " + cls + " " + name + ": o tiro não saiu");
			yield break;
		}
		float num = Vector3.Distance(pl.cam.transform.position, pl.LastShotHit);
		GameObject m1 = FBCamLab.Mark(pl.LastShotHit, num);
		GameObject m2 = FBCamLab.Mark(pl.LastShotAim, num * 0.6f, Color.green);
		Debug.Log($"[FreeBala][cam] {cls} {name}: distância {num:0.0} m · impacto × ponto mirado {Vector3.Distance(pl.LastShotHit, pl.LastShotAim) * 100f:0.0} cm · impacto × retículo (antes do tiro) {(ch0.collider ? (Vector3.Distance(pl.LastShotHit, ch0.point) * 100f) : (-1f)):0.0} cm · mira bloqueada {pl.AimBlocked}");
		yield return Wait(0.25f);
		yield return Snap(cls, name, 0f);
		UnityEngine.Object.Destroy(m1);
		UnityEngine.Object.Destroy(m2);
	}

	private IEnumerator Snap(string cls, string name, float wait)
	{
		if (wait > 0f)
		{
			yield return Wait(wait);
		}
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "cam_" + cls + "_" + name + ".png"));
		shots++;
		yield return null;
		yield return null;
	}

	private static IEnumerator Wait(float s)
	{
		for (float t = 0f; t < s; t += Time.deltaTime)
		{
			yield return null;
		}
	}

	private static float TargetYaw(Vector3 p)
	{
		float result = 0f;
		float num = -1E+09f;
		Vector3 o = p + Vector3.up * 1.5f;
		for (int i = 0; i < 36; i++)
		{
			float num2 = i * 10;
			RaycastHit hit;
			float num3 = (FBWorld.RayHit(o, Quaternion.Euler(0f, num2, 0f) * Vector3.forward, 80f, out hit) ? (0f - Mathf.Abs(hit.distance - 20f)) : (-100f));
			if (num3 > num)
			{
				num = num3;
				result = num2;
			}
		}
		return result;
	}
}
}
