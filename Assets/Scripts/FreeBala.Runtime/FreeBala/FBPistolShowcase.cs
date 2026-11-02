using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public class FBPistolShowcase : MonoBehaviour
{
	private string dir;

	private Camera extra;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbpistola") >= 0)
		{
			GameObject obj = new GameObject("FB_VitrinePistolas");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<FBPistolShowcase>();
		}
	}

	private static IEnumerator Wait(float s)
	{
		for (float t = 0f; t < s; t += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Snap(string name)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
		yield return null;
		yield return null;
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
		FBCamLab.Install();
		FBWeaponUser wu = me.GetComponent<FBWeaponUser>();
		Vector3 p0 = me.Pos;
		float yaw = me.Yaw;
		List<string> ids = (from w in GameData.Weapons.Values
			where w.cls == "pistola" && (bool)FBRegistry.I.Weapon(w.id)
			select w.id).ToList();
		Debug.Log("[FreeBala][pistola] vitrine: " + string.Join(", ", ids));
		string text = (from w in GameData.Weapons.Values
			where w.cls == "fuzil" && (bool)FBRegistry.I.Weapon(w.id)
			select w.id).FirstOrDefault();
		if (text != null)
		{
			wu.Strip();
			wu.Give(text);
			yield return Wait(1.4f);
			pl.ForceFirstPerson();
			FBCamLab.Teleport(me, p0, yaw);
			pl.camPitch = 0f;
			yield return Wait(3f);
			yield return Snap("fp_fuzil_parado");
			FBCamLab.leanL = true;
			yield return Wait(0.9f);
			yield return Snap("fp_fuzil_inclina_q");
			FBCamLab.leanL = false;
			yield return Wait(0.6f);
			FBCamLab.leanR = true;
			yield return Wait(0.9f);
			yield return Snap("fp_fuzil_inclina_e");
			FBCamLab.aim = true;
			yield return Wait(0.8f);
			yield return Snap("fp_fuzil_inclina_e_mira");
			FBCamLab.leanR = false;
			FBCamLab.aim = false;
			yield return Wait(0.6f);
		}
		foreach (string id in ids)
		{
			wu.Strip();
			wu.Give(id);
			yield return Wait(1.4f);
			pl.ForceFirstPerson();
			FBCamLab.Teleport(me, p0, yaw);
			pl.camPitch = 0f;
			yield return Wait(3f);
			yield return Snap("pist_" + id + "_fp_parado");
			if (id == ids[0])
			{
				FBCamLab.leanL = true;
				yield return Wait(0.9f);
				yield return Snap("pist_" + id + "_fp_inclina_q");
				FBCamLab.leanL = false;
				yield return Wait(0.6f);
			}
			FBCamLab.aim = true;
			yield return Wait(0.9f);
			yield return Snap("pist_" + id + "_fp_mira");
			FBCamLab.aim = false;
			yield return Wait(0.5f);
			FBCamLab.move = new Vector2(0f, 1f);
			yield return Wait(0.8f);
			yield return Snap("pist_" + id + "_fp_andando");
			FBCamLab.move = Vector2.zero;
			yield return Wait(0.4f);
			pl.ForceThirdPerson();
			FBCamLab.Teleport(me, p0, yaw);
			yield return Wait(1.2f);
			if (!extra)
			{
				extra = new GameObject("CamPistola").AddComponent<Camera>();
				extra.depth = 50f;
				extra.fieldOfView = 32f;
				extra.nearClipPlane = 0.05f;
			}
			extra.cullingMask = (Camera.main ? (Camera.main.cullingMask & -16385) : (-1));
			extra.enabled = true;
			(string, bool, Vector2)[] array = new(string, bool, Vector2)[3]
			{
				("parado", false, Vector2.zero),
				("mira", true, Vector2.zero),
				("andando", false, new Vector2(0f, 1f))
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (st, aim, move) = array[i];
				FBCamLab.Teleport(me, p0, yaw);
				FBCamLab.aim = aim;
				FBCamLab.move = move;
				yield return Wait((move.sqrMagnitude > 0f) ? 0.7f : 1.1f);
				(string, float)[] array2 = new(string, float)[3]
				{
					("frente", 40f),
					("lado", -90f),
					("costas", 160f)
				};
				for (int j = 0; j < array2.Length; j++)
				{
					(string, float) tuple2 = array2[j];
					string item = tuple2.Item1;
					float item2 = tuple2.Item2;
					Vector3 vector = (me.Chest ? me.Chest.position : (me.Pos + Vector3.up * 1.3f * me.ScaleK));
					extra.transform.position = vector + Quaternion.Euler(0f, me.Yaw + item2, 0f) * Vector3.forward * 1.9f * me.ScaleK + Vector3.up * 0.1f * me.ScaleK;
					extra.transform.LookAt(vector - Vector3.up * 0.05f * me.ScaleK);
					yield return Snap("pist_" + id + "_tps_" + item + "_" + st);
				}
				FBRig rig = me.rig;
				Transform leftHand = me.LeftHand;
				if (!me.weaponGO)
				{
					_ = Vector3.zero;
				}
				else
				{
					_ = me.weaponGO.transform.position;
				}
				Debug.Log($"[FreeBala][pistola] {id} 3ª pessoa {st}: mão esquerda a {(((bool)leftHand && (bool)rig && rig.LeftTargetValid) ? ((leftHand.position - rig.LeftTarget).magnitude * 100f) : (-1f)):0.0} cm do alvo · mãos separadas {(((bool)leftHand && (bool)me.RightHand) ? ((leftHand.position - me.RightHand.position).magnitude * 100f) : (-1f)):0.0} cm · erro do cano {(me.hasAim ? Vector3.Angle(me.BarrelDir(), me.aimPoint - me.MuzzlePos) : (-1f)):0.0}°");
			}
			FBCamLab.aim = false;
			FBCamLab.move = Vector2.zero;
			extra.enabled = false;
			yield return Wait(0.3f);
		}
		FBCamLab.Remove();
		if ((bool)extra)
		{
			UnityEngine.Object.Destroy(extra.gameObject);
		}
		Debug.Log("[FreeBala][pistola] vitrine fim");
	}
}
}
