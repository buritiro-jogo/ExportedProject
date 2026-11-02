using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public class FBFxShowcase : MonoBehaviour
{
	private string dir;

	private Camera cam;

	private int shots;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (Array.IndexOf(Environment.GetCommandLineArgs(), "-fbfx") >= 0)
		{
			GameObject obj = new GameObject("FB_FxVitrine");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			obj.AddComponent<FBFxShowcase>();
		}
	}

	private IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		while (!FBMatch.I || !FBMatch.Player || !FBMatch.Player.alive)
		{
			yield return null;
		}
		for (float w = 0f; w < 6f; w += Time.unscaledDeltaTime)
		{
			yield return null;
		}
		Debug.Log("[FreeBala][fx] vitrine começando · projetor URP " + FBDecalFx.Projector + " · nível FX " + FBFxQuality.Level);
		List<Canvas> canv = (from c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
			where c.enabled
			select c).ToList();
		foreach (Canvas item in canv)
		{
			item.enabled = false;
		}
		GameObject cg = new GameObject("FxCam");
		cam = cg.AddComponent<Camera>();
		cam.depth = 100f;
		cam.fieldOfView = 50f;
		cam.nearClipPlane = 0.03f;
		if ((bool)Camera.main)
		{
			cam.cullingMask = Camera.main.cullingMask;
		}
		yield return Run();
		foreach (Canvas item2 in canv)
		{
			if ((bool)item2)
			{
				item2.enabled = true;
			}
		}
		UnityEngine.Object.Destroy(cg);
		Debug.Log($"[FreeBala][fx] vitrine fim: {shots} prints · decals {FBDecalFx.ActiveCount}/{FBDecalFx.Capacity} · ferimentos {FBWoundFx.ActiveCount}/{FBWoundFx.Capacity}");
	}

	private IEnumerator Snap(string name)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, "fx_" + name + ".png"));
		shots++;
		yield return null;
		yield return null;
	}

	private void Look(Vector3 from, Vector3 at)
	{
		cam.transform.position = from;
		cam.transform.LookAt(at);
	}

	private static IEnumerator Wait(float s)
	{
		for (float t = 0f; t < s; t += Time.deltaTime)
		{
			yield return null;
		}
	}

	private static bool FindWall(out Vector3 wp, out Vector3 n)
	{
		List<Vector3> list = new List<Vector3> { FBMatch.Player.Pos };
		if (FBMatch.I.Map != null)
		{
			list.AddRange(FBMatch.I.Map.points.Take(12));
		}
		foreach (Vector3 item in list)
		{
			for (int i = 0; i < 24; i++)
			{
				float f = (float)i * (float)Math.PI * 2f / 24f;
				if (!Physics.Raycast(direction: new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f)), origin: item + Vector3.up * 1.3f, hitInfo: out var hitInfo, maxDistance: 6f, layerMask: FBWorld.SolidMask, queryTriggerInteraction: QueryTriggerInteraction.Ignore) || hitInfo.distance < 2.2f || Mathf.Abs(hitInfo.normal.y) > 0.25f)
				{
					continue;
				}
				Vector3 normalized = new Vector3(hitInfo.normal.x, 0f, hitInfo.normal.z).normalized;
				Vector3 vector = hitInfo.point + normalized * 1.3f;
				float? num = FBWorld.GroundY(vector.x, vector.y, vector.z, 0.5f, 2.5f);
				if (num.HasValue)
				{
					float valueOrDefault = num.GetValueOrDefault();
					if (Mathf.Abs(valueOrDefault - item.y) < 1.2f && !FBWorld.RayHit(vector + Vector3.up * 1.3f, normalized, 7f))
					{
						wp = new Vector3(hitInfo.point.x, valueOrDefault + 1.3f, hitInfo.point.z);
						n = normalized;
						return true;
					}
				}
			}
		}
		wp = default(Vector3);
		n = default(Vector3);
		return false;
	}

	private static string Wpn(string cls)
	{
		return GameData.Weapons.Values.FirstOrDefault((WeaponDef w) => w.cls == cls)?.id;
	}

	private static Vector3 Hit(FBCharacter v, string hbName, string cls, Vector3 dir, float dmg = 30f)
	{
		FBHitbox fBHitbox = v.GetComponentsInChildren<FBHitbox>(includeInactive: true).FirstOrDefault((FBHitbox h) => h.name == hbName);
		if (!fBHitbox)
		{
			return v.Pos;
		}
		Collider component = fBHitbox.GetComponent<Collider>();
		Vector3 vector = component.ClosestPoint(component.bounds.center - dir * 2f);
		FBDamage d = default(FBDamage);
		d.victim = v;
		d.source = FBMatch.Player;
		d.type = FBDamageType.Bullet;
		d.amount = dmg;
		d.applied = dmg;
		d.hpBefore = v.hp;
		d.hpAfter = v.hp;
		d.head = fBHitbox.head;
		d.hit = new FBHitInfo
		{
			valid = true,
			point = vector,
			normal = -dir,
			dir = dir,
			part = (fBHitbox.head ? "head" : fBHitbox.part),
			weaponId = Wpn(cls),
			dist = 10f
		};
		FBDamageEvents.Raise(in d);
		return vector;
	}

	private IEnumerator Run()
	{
		if (!FindWall(out var wall, out var n))
		{
			Debug.Log("[FreeBala][fx] FALHA: nenhuma parede livre perto do jogador/pontos do mapa");
			yield break;
		}
		Vector3 t = Vector3.Cross(Vector3.up, n).normalized;
		float gy = wall.y - 1.3f;
		Vector3 spot = new Vector3(wall.x, gy, wall.z) + n * 1.3f;
		List<string> charIds = FBRegistry.I.charIds;
		FBCharacter v = FBMatch.I.Spawn(charIds[Mathf.Min(1, charIds.Count - 1)], null, 1, spot, player: false);
		FBMatch.Teleport(v, spot, Quaternion.LookRotation(n).eulerAngles.y);
		v.combat = false;
		for (int l = 0; l < 20; l++)
		{
			yield return null;
		}
		Vector3 chest = (v.Chest ? v.Chest.position : (spot + Vector3.up * 1.3f));
		Vector3 dirIn = -n;
		Look(chest + t * 2.4f + n * 0.6f + Vector3.up * 0.15f, (chest + wall) * 0.5f);
		Hit(v, "HB_Spine", "fuzil", dirIn, 34f);
		yield return Wait(0.05f);
		yield return Snap("1_tronco_jato");
		yield return Wait(1f);
		yield return Snap("1_tronco_parede");
		Vector3 head = (v.Head ? v.Head.position : (chest + Vector3.up * 0.5f));
		Look(head + t * 1.6f + n * 0.7f, head - n * 0.3f);
		Hit(v, "HB_Cabeca", "sniper", dirIn, 95f);
		yield return Wait(0.06f);
		yield return Snap("2_sniper_cabeca");
		yield return Wait(1.2f);
		Look(head + n * 1.4f + t * 0.5f, head);
		yield return Snap("2_sniper_ferimento");
		Hit(v, "HB_LeftUpperArm", "pistola", dirIn, 20f);
		Hit(v, "HB_RightUpperLeg", "submetralhadora", dirIn, 20f);
		yield return Wait(0.6f);
		Look(chest + n * 1.5f + Vector3.down * 0.35f, chest + Vector3.down * 0.45f);
		yield return Snap("3_membros_a");
		yield return Wait(1.3f);
		yield return Snap("3_membros_b");
		Vector3 w4 = wall + t * 2.2f;
		for (int num = 0; num < 5; num++)
		{
			FBBloodFx.SceneSplat(w4 + n * 0.7f + t * ((float)num * 0.35f - 0.7f) + Vector3.up * ((float)(num % 2) * 0.25f), (t * 0.85f - n * 0.5f).normalized, 1.2f, 1.2f);
		}
		for (int num2 = 0; num2 < 2; num2++)
		{
			FBBloodFx.SceneSplat(w4 + n * 0.9f + t * (0.9f + (float)num2 * 0.4f), (-n + Vector3.down * 0.1f).normalized, 1.3f, 1.4f);
		}
		yield return Wait(0.3f);
		Look(w4 + n * 2.4f + Vector3.up * 0.2f, w4);
		yield return Snap("4_parede_rasante");
		Vector3 w5 = wall - t * 2.4f;
		string[] source = new string[5] { "fx_concreto", "fx_metal_chapa", "fx_madeira", "fx_terra", "fx_vidro" };
		List<GameObject> pieces = source.Select(delegate(string m, int i)
		{
			GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
			obj.name = m;
			obj.layer = 8;
			obj.transform.position = w5 + n * 0.45f + t * ((float)(i - 2) * 0.6f) + Vector3.down * 0.3f;
			obj.transform.rotation = Quaternion.LookRotation(n);
			obj.transform.localScale = new Vector3(0.5f, 0.9f, 0.4f);
			return obj;
		}).ToList();
		yield return null;
		Physics.SyncTransforms();
		Look(w5 + n * 2.6f + Vector3.up * 0.25f, w5 + n * 0.4f + Vector3.down * 0.3f);
		for (int l = 0; l < 3; l++)
		{
			foreach (GameObject item in pieces)
			{
				Collider component = item.GetComponent<Collider>();
				FBImpactFx.Surface(item.transform.position + n * 0.2f + item.transform.right * UnityEngine.Random.Range(-0.15f, 0.15f) + Vector3.up * UnityEngine.Random.Range(-0.3f, 0.3f), n, component, -1, -n);
			}
			yield return Wait((l == 2) ? 0.07f : 0.2f);
		}
		yield return Snap("5_materiais_particulas");
		yield return Wait(1.4f);
		yield return Snap("5_materiais_buracos");
		Vector3 wpos = new Vector3(w5.x, gy, w5.z) + n * 1.8f;
		FBFx.Splash(wpos + Vector3.up * 0.02f);
		yield return Wait(0.12f);
		Look(wpos + n * 2f + Vector3.up * 1.1f, wpos);
		yield return Snap("5_agua");
		FBCombat.DamageActor(v, 999f, head: false, FBMatch.Player);
		yield return Wait(7f);
		Vector3 vector = (v.Hips ? v.Hips.position : spot);
		Look(vector + n * 2.2f + t * 1.2f + Vector3.up * 1.8f, vector);
		yield return Snap("6_poca");
		Vector3 ep = new Vector3(spot.x, gy, spot.z) + n * 5f - t * 3f;
		Vector3 from = ep + n * 9f + Vector3.up * 2.5f;
		FBFx.Explosion(ep + Vector3.up * 0.3f, 4f);
		Look(from, ep + Vector3.up * 1.5f);
		yield return Wait(0.12f);
		yield return Snap("7_explosao_fogo");
		yield return Wait(1.6f);
		yield return Snap("7_explosao_fumaca");
		yield return Wait(3f);
		Look(ep + n * 3f + Vector3.up * 3f, ep);
		yield return Snap("7_queimado");
		FBCharacter pl = FBMatch.Player;
		Vector3 muzzlePos = pl.MuzzlePos;
		Look(muzzlePos + pl.transform.right * 1.3f + pl.transform.forward * 0.4f + Vector3.up * 0.15f, muzzlePos - pl.transform.forward * 0.3f);
		for (int l = 0; l < 6; l++)
		{
			FBFx.Muzzle(pl.MuzzlePos, 0.55f, "fuzil");
			yield return Wait(0.09f);
			if (l == 3)
			{
				yield return Snap("8_cartuchos");
			}
		}
		yield return Wait(0.5f);
		yield return Snap("8_fumaca_cano");
		Vector3? vector2 = FindEdge(wall, n, t);
		if (vector2.HasValue)
		{
			Vector3 e = vector2.GetValueOrDefault();
			FBBloodFx.SceneSplat(e + n * 0.8f, -n, 1.2f, 1.6f);
			FBDecalFx.Place(FBDecalFx.Atlas.Blood, 0, e, n, 0.6f);
			yield return Wait(0.2f);
			Look(e + n * 1.3f + t * 0.8f, e);
			yield return Snap("9_quina");
		}
		else
		{
			Debug.Log("[FreeBala][fx] sem quina perto da parede para o print 9");
		}
	}

	private static Vector3? FindEdge(Vector3 wall, Vector3 n, Vector3 t)
	{
		float[] array = new float[2] { 1f, -1f };
		foreach (float num in array)
		{
			for (float num2 = 0.2f; num2 < 4f; num2 += 0.1f)
			{
				if (!Physics.Raycast(wall + t * num * num2 + n * 0.3f, -n, 0.6f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
				{
					return wall + t * num * (num2 - 0.15f);
				}
			}
		}
		return null;
	}
}
}
