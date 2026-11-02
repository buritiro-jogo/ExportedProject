using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBWeaponModel
{
	public class Choice
	{
		public int scope = -1;

		public int muzzle;

		public int magazine;

		public int laser = -1;

		public int grip = -1;

		public int skin = -1;
	}

	public struct Mods
	{
		public float recV;

		public float recH;

		public float spreadHip;

		public float spreadAds;

		public float move;

		public bool laser;

		public bool flash;

		public bool bipod;
	}

	private static readonly Dictionary<string, Choice> chosen = new Dictionary<string, Choice>();

	public static Choice Default(WeaponDef d)
	{
		List<object> list = Json.L(Json.O(Json.O(GameData.WeaponsRaw, d.id), "att"), "muzzles");
		int muzzle = -1;
		if (list != null)
		{
			for (int i = 0; i < list.Count; i++)
			{
				if (Json.S(list[i], "node", "").IndexOf("Default", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					muzzle = i;
					break;
				}
			}
		}
		return new Choice
		{
			scope = -1,
			muzzle = muzzle,
			magazine = 0,
			laser = -1,
			grip = -1
		};
	}

	public static Choice Of(WeaponDef d)
	{
		if (!chosen.TryGetValue(d.id, out var value))
		{
			return Default(d);
		}
		return value;
	}

	public static void Set(WeaponDef d, Choice c)
	{
		chosen[d.id] = c;
	}

	public static Dictionary<string, object> Att(WeaponDef d, string kind, int i)
	{
		List<object> list = Json.L(Json.O(Json.O(GameData.WeaponsRaw, d.id), "att"), kind);
		if (list == null || i < 0 || i >= list.Count)
		{
			return null;
		}
		return list[i] as Dictionary<string, object>;
	}

	public static Dictionary<string, object> Scope(WeaponDef d)
	{
		return Att(d, "scopes", Of(d).scope);
	}

	public static Mods ModsOf(WeaponDef d, Choice c = null, bool braced = false)
	{
		Mods mods = default(Mods);
		mods.recV = 1f;
		mods.recH = 1f;
		mods.spreadHip = 1f;
		mods.spreadAds = 1f;
		mods.move = 1f;
		Mods result = mods;
		if (d == null)
		{
			return result;
		}
		if (c == null)
		{
			c = Of(d);
		}
		string text = Json.S(Att(d, "grips", c.grip), "node") ?? "";
		if (text.Contains("Bipod"))
		{
			result.bipod = true;
			result.move = 0.95f;
			if (braced)
			{
				result.recV = 0.55f;
				result.recH = 0.6f;
				result.spreadAds = 0.7f;
			}
			else
			{
				result.recV = 0.95f;
			}
		}
		else if (text.Contains("Angled"))
		{
			result.recH = 0.78f;
			result.recV = 0.94f;
		}
		else if (text.Length > 0)
		{
			result.recV = 0.82f;
			result.recH = 0.9f;
			result.spreadHip = 0.95f;
		}
		string text2 = Json.S(Att(d, "lasers", c.laser), "node") ?? "";
		if (text2.Length > 0 && text2 != "None")
		{
			result.laser = true;
			result.flash = text2.Contains("Flash") || text2.Contains("Tactical");
			result.spreadAds *= (result.flash ? 0.93f : 0.9f);
		}
		string input = Json.S(Att(d, "muzzles", c.muzzle), "node") ?? "";
		if (Regex.IsMatch(input, "Silencer|Supress", RegexOptions.IgnoreCase))
		{
			result.recV *= 0.95f;
		}
		else if (Regex.IsMatch(input, "Brake|Comp|Freio", RegexOptions.IgnoreCase))
		{
			result.recV *= 0.9f;
			result.recH *= 0.88f;
		}
		return result;
	}

	public static GameObject Make(WeaponDef d, Transform parent = null, Choice c = null)
	{
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Weapon(d.id) : null);
		if (!gameObject)
		{
			return null;
		}
		if (c == null)
		{
			c = Of(d);
		}
		GameObject obj = (parent ? UnityEngine.Object.Instantiate(gameObject, parent) : UnityEngine.Object.Instantiate(gameObject));
		obj.name = "Arma_" + d.id;
		Apply(obj, d, c);
		return obj;
	}

	public static void Apply(GameObject go, WeaponDef d, Choice c)
	{
		Dictionary<string, Transform> byName = new Dictionary<string, Transform>();
		Transform[] componentsInChildren = go.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (!byName.ContainsKey(transform.name))
			{
				byName[transform.name] = transform;
			}
		}
		List<object> list = Json.L(Json.O(GameData.Root, "weaponOff"), d.id);
		if (list != null)
		{
			foreach (object item in list)
			{
				Transform transform2 = Get(item as string);
				if ((bool)transform2)
				{
					transform2.gameObject.SetActive(value: false);
				}
			}
		}
		Dictionary<string, object> att = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att");
		Pick("scopes", c.scope);
		Pick("muzzles", c.muzzle);
		Pick("magazines", c.magazine);
		Pick("lasers", c.laser);
		Pick("grips", c.grip);
		Transform transform3 = Get(Json.S(att, "scopeDefault"));
		if ((bool)transform3)
		{
			transform3.gameObject.SetActive(c.scope < 0 && Json.B(att, "scopeDefaultShow"));
		}
		componentsInChildren = go.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform4 in componentsInChildren)
		{
			if (Regex.IsMatch(transform4.name, "Beam", RegexOptions.IgnoreCase) && (bool)transform4.GetComponent<Renderer>())
			{
				transform4.gameObject.SetActive(value: false);
			}
		}
		Transform transform5 = ((c.scope >= 0) ? Get(Json.S(Json.L(att, "scopes")?[c.scope], "node")) : null);
		if (!transform5)
		{
			return;
		}
		Renderer[] componentsInChildren2 = transform5.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren2)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			bool flag = false;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if ((bool)sharedMaterials[j] && Regex.IsMatch(renderer.name + " " + sharedMaterials[j].name, "lens|glass|vidro", RegexOptions.IgnoreCase))
				{
					Material material = new Material(sharedMaterials[j]);
					if (material.HasProperty("_BaseColor"))
					{
						Color color = material.GetColor("_BaseColor");
						color.a = 0.2f;
						material.SetColor("_BaseColor", color);
					}
					material.SetFloat("_Surface", 1f);
					material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
					material.SetOverrideTag("RenderType", "Transparent");
					if (material.renderQueue < 3000)
					{
						material.renderQueue = 3000;
					}
					material.SetFloat("_ZWrite", 0f);
					material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
					material.renderQueue = 3000;
					sharedMaterials[j] = material;
					flag = true;
				}
			}
			if (flag)
			{
				renderer.sharedMaterials = sharedMaterials;
			}
		}
		Transform Get(string n)
		{
			if (n == null || !byName.TryGetValue(n, out var value))
			{
				return null;
			}
			return value;
		}
		void Pick(string kind, int idx)
		{
			List<object> list2 = Json.L(att, kind);
			if (list2 != null)
			{
				for (int k = 0; k < list2.Count; k++)
				{
					Transform transform6 = Get(Json.S(list2[k], "node"));
					if ((bool)transform6)
					{
						transform6.gameObject.SetActive(k == idx);
					}
				}
			}
		}
	}

	public static float Zoom(Dictionary<string, object> sc)
	{
		float num = Json.F(sc, "fieldOfViewMultiplierAim", 0.9f);
		if (!(num >= 0.845f))
		{
			if (!(num >= 0.745f))
			{
				if (!(num >= 0.645f))
				{
					return (num >= 0.595f) ? 2 : ((num >= 0.495f) ? 4 : 6);
				}
				return 1.5f;
			}
			return 1.3f;
		}
		return 1.12f;
	}
}
}
