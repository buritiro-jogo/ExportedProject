using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBLoot
{
	public class L
	{
		public string id;

		public GameObject obj;

		public Transform ring;

		public Vector3 pos;

		public int ammo;

		public int reserve;

		public float t;

		public string armor;
	}

	public static readonly List<L> loots = new List<L>();

	public static L near;

	private static Material ringMat;

	public static void Clear()
	{
		foreach (L loot in loots)
		{
			if ((bool)loot.obj)
			{
				Object.Destroy(loot.obj);
			}
		}
		loots.Clear();
		near = null;
	}

	public static void DropWeapon(string id, Vector3 pos, int ammo = -1, int reserve = -1)
	{
		if (!GameData.Weapons.TryGetValue(id, out var value) || !FBRegistry.I.Weapon(id))
		{
			return;
		}
		GameObject gameObject = new GameObject("Saque_" + id);
		GameObject gameObject2 = FBWeaponModel.Make(value, gameObject.transform);
		Collider[] componentsInChildren = gameObject2.GetComponentsInChildren<Collider>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Object.Destroy(componentsInChildren[i]);
		}
		gameObject2.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
		gameObject2.transform.localScale = Vector3.one * 0.6175f * 2.2f / 2.2f;
		float num = FBWorld.GroundY(pos.x, pos.y + 1f, pos.z, 0.3f) ?? pos.y;
		gameObject.transform.position = new Vector3(pos.x, num, pos.z);
		gameObject.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
		Bounds bounds = new Bounds(gameObject.transform.position, Vector3.zero);
		Renderer[] componentsInChildren2 = gameObject2.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren2)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		gameObject2.transform.position += Vector3.up * (num + 0.03f - bounds.min.y);
		if ((object)ringMat == null)
		{
			ringMat = RingMat();
		}
		GameObject gameObject3 = FBZone.Ring(32, 0.42f, 0.5f, ringMat);
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		gameObject3.transform.localPosition = Vector3.up * 0.03f;
		loots.Add(new L
		{
			id = id,
			obj = gameObject,
			ring = gameObject3.transform,
			pos = gameObject.transform.position,
			ammo = ((ammo < 0) ? value.mag : ammo),
			reserve = ((reserve < 0) ? (value.mag * 2) : reserve)
		});
		int num2 = (((bool)FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.br) ? 700 : 24);
		while (loots.Count > num2)
		{
			if ((bool)loots[0].obj)
			{
				Object.Destroy(loots[0].obj);
			}
			loots.RemoveAt(0);
		}
	}

	public static void DropArmor(string key, Vector3 pos)
	{
		FBBrCore.Armor armor = FBBrCore.NewArmor(key);
		if (armor != null)
		{
			GameObject gameObject = new GameObject("Saque_" + key);
			GameObject gameObject2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
			Object.Destroy(gameObject2.GetComponent<Collider>());
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject2.transform.localScale = ((armor.kind == "vest") ? new Vector3(0.5f, 0.12f, 0.4f) : new Vector3(0.3f, 0.2f, 0.3f));
			gameObject2.transform.localPosition = Vector3.up * 0.08f;
			Material material = new Material(FBShaders.Lit);
			material.SetColor("_BaseColor", (armor.lv == 3) ? new Color(0.55f, 0.3f, 0.7f) : ((armor.lv == 2) ? new Color(0.2f, 0.45f, 0.8f) : new Color(0.45f, 0.45f, 0.4f)));
			gameObject2.GetComponent<Renderer>().sharedMaterial = material;
			float y = FBWorld.GroundY(pos.x, pos.y + 1f, pos.z, 0.3f) ?? pos.y;
			gameObject.transform.position = new Vector3(pos.x, y, pos.z);
			if ((object)ringMat == null)
			{
				ringMat = RingMat();
			}
			GameObject gameObject3 = FBZone.Ring(32, 0.42f, 0.5f, ringMat);
			gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject3.transform.localPosition = Vector3.up * 0.03f;
			loots.Add(new L
			{
				armor = key,
				obj = gameObject,
				ring = gameObject3.transform,
				pos = gameObject.transform.position
			});
		}
	}

	public static void DropRandomAtt(WeaponDef w, Vector3 pos)
	{
	}

	private static Material RingMat()
	{
		Material material = new Material(FBShaders.Unlit);
		material.SetColor("_BaseColor", new Color(1f, 0.77f, 0f, 0.7f));
		material.SetFloat("_Surface", 1f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		if (material.renderQueue < 3000)
		{
			material.renderQueue = 3000;
		}
		material.SetFloat("_Cull", 0f);
		material.SetFloat("_ZWrite", 0f);
		material.SetInt("_SrcBlend", 5);
		material.SetInt("_DstBlend", 10);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.renderQueue = 3050;
		return material;
	}

	public static void Update(float dt, FBCharacter player)
	{
		near = null;
		float num = 2.3f;
		bool flag = (bool)FBMatch.I && FBMatch.I.mode != null && FBMatch.I.mode.br;
		for (int num2 = loots.Count - 1; num2 >= 0; num2--)
		{
			L l = loots[num2];
			l.t += dt;
			if (!l.obj)
			{
				loots.RemoveAt(num2);
			}
			else if (!flag && l.t > 120f)
			{
				Object.Destroy(l.obj);
				loots.RemoveAt(num2);
			}
			else
			{
				float magnitude = (l.pos - player.Pos).Flat().magnitude;
				if (magnitude < num && Mathf.Abs(l.pos.y - player.Pos.y) < 2f)
				{
					num = magnitude;
					near = l;
				}
				l.ring.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(l.t * 4f));
			}
		}
		if (near == null || !player.alive)
		{
			FBHUD.I?.Prompt(null, null);
		}
		else if (near.armor != null)
		{
			FBBrCore.Armor armor = FBBrCore.NewArmor(near.armor);
			FBHUD.I?.Prompt("X", "PEGAR " + ArmorLabel(near.armor), $"EQUIPAMENTO · REDUZ O DANO {Mathf.RoundToInt(armor.red * 100f)}%");
		}
		else
		{
			FBHUD.I?.Prompt("X", "PEGAR " + GameData.Weapons[near.id].name.ToUpper(), $"{near.ammo} / {near.reserve}");
		}
	}

	public static string ArmorLabel(string key)
	{
		FBBrCore.Armor armor = FBBrCore.NewArmor(key);
		if (armor != null)
		{
			return string.Format("{0} NV.{1}", (armor.kind == "vest") ? "COLETE" : "CAPACETE", armor.lv);
		}
		return key;
	}

	public static void Pickup(FBCharacter p)
	{
		L l = near;
		if (l == null)
		{
			return;
		}
		FBWeaponUser component = p.GetComponent<FBWeaponUser>();
		if (component.switching > 0f)
		{
			return;
		}
		if (l.armor != null)
		{
			FBBrCore.Armor armor = FBBrCore.NewArmor(l.armor);
			FBBrCore.Armor armor2 = ((armor.kind == "vest") ? p.vest : p.helmetFF);
			if (armor2 != null && armor2.dur > 0f && armor2.lv > armor.lv)
			{
				FBHUD.I?.Center("VOCÊ JÁ TEM " + ArmorLabel(armor2.key) + " MELHOR", 1.4f);
				FBAudio.Synth("deny");
				return;
			}
			Object.Destroy(l.obj);
			loots.Remove(l);
			near = null;
			if (armor.kind == "vest")
			{
				p.vest = armor;
			}
			else
			{
				p.helmetFF = armor;
			}
			FBAudio.Play2D("ie_crouch", 0.6f);
			FBAudio.Play2D("click", 0.6f);
			if (armor2 != null && armor2.dur > 0f)
			{
				DropArmor(armor2.key, p.Pos);
			}
			FBHUD.I?.Toast("EQUIPADO", ArmorLabel(armor.key), $"REDUZ {Mathf.RoundToInt(armor.red * 100f)}% · DURABILIDADE {armor.max}", new Color(0.93f, 0.91f, 0.86f));
			return;
		}
		WeaponDef weaponDef = GameData.Weapons[l.id];
		int num = Mathf.Clamp(weaponDef.slot, 0, 2);
		WeaponDef weaponDef2 = component.slots[num];
		Object.Destroy(l.obj);
		loots.Remove(l);
		near = null;
		if (weaponDef2 != null)
		{
			DropWeapon(weaponDef2.id, p.Pos, component.ammo[num], component.reserve[num]);
		}
		component.Give(l.id);
		component.ammo[num] = l.ammo;
		component.reserve[num] = l.reserve;
		component.switching = 0.25f;
		FBHUD.I?.Toast("ARMA COLETADA", weaponDef.name.ToUpper(), string.Format("{0} / {1} · {2}", l.ammo, l.reserve, (new string[3] { "FUZIL", "SMG", "PISTOLA" })[num]), new Color(0.96f, 0.67f, 0.09f));
	}
}
}
