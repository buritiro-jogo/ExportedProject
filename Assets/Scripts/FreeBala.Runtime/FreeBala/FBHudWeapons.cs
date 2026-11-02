using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public class FBHudWeapons : MonoBehaviour
{
	private class Small
	{
		public RectTransform rt;

		public RawImage icon;

		public Text n;

		public Text key;

		public Image bg;

		public string id;
	}

	private RectTransform big;

	private RectTransform row;

	private RectTransform life;

	private RawImage bigIcon;

	private Text ammo;

	private Text maxT;

	private Text perk;

	private Image magBar;

	private Image magBg;

	private Small[] smalls;

	private string bigId;

	private Image hpFill;

	private Image hpLag;

	private Image arFill;

	private Image arBg;

	private Text hpT;

	private Text arT;

	private Text kitT;

	private float lag = 1f;

	private static readonly Color Panel = new Color(0f, 0f, 0f, 0.55f);

	private static readonly Color Panel2 = new Color(0f, 0f, 0f, 0.42f);

	private static readonly Color W = new Color(1f, 1f, 1f, 0.95f);

	private static readonly Color M = new Color(1f, 1f, 1f, 0.6f);

	private static Texture2D fist;

	public static FBHudWeapons Attach(Transform canvasRoot)
	{
		GameObject obj = new GameObject("ArmasHUD", typeof(RectTransform));
		obj.transform.SetParent(canvasRoot, worldPositionStays: false);
		RectTransform obj2 = (RectTransform)obj.transform;
		obj2.anchorMin = Vector2.zero;
		obj2.anchorMax = Vector2.one;
		Vector2 offsetMin = (obj2.offsetMax = Vector2.zero);
		obj2.offsetMin = offsetMin;
		return obj.AddComponent<FBHudWeapons>();
	}

	private static RectTransform R(Transform p, string n, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size)
	{
		RectTransform rectTransform = FBUI.Rect(p, n, a, a, pos, size);
		rectTransform.pivot = pv;
		rectTransform.anchoredPosition = pos;
		return rectTransform;
	}

	private static Text T(Transform p, string s, int sz, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size, TextAnchor al, Color c)
	{
		Text text = R(p, "t", a, pv, pos, size).gameObject.AddComponent<Text>();
		text.font = FBUI.Title;
		text.fontSize = sz;
		text.alignment = al;
		text.color = c;
		text.text = s;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.supportRichText = true;
		Shadow shadow = text.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
		shadow.effectDistance = new Vector2(1.2f, -1.2f);
		return text;
	}

	private static Image I(Transform p, string n, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size, Color c)
	{
		Image image = R(p, n, a, pv, pos, size).gameObject.AddComponent<Image>();
		image.color = c;
		image.raycastTarget = false;
		return image;
	}

	private static Image Fill(Transform p, Color c)
	{
		Image image = FBUI.Panel(p, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, c);
		image.sprite = FBUI.WhiteSprite;
		image.type = Image.Type.Filled;
		image.fillMethod = Image.FillMethod.Horizontal;
		image.raycastTarget = false;
		return image;
	}

	private static Image Grad(Transform p, Vector2 a, Vector2 pv, Vector2 pos, Vector2 size, float a0, float a1)
	{
		Texture2D texture2D = new Texture2D(64, 1)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			texture2D.SetPixel(i, 0, new Color(1f, 1f, 1f, Mathf.Lerp(a0, a1, (float)i / 63f)));
		}
		texture2D.Apply();
		Image image = I(p, "painel", a, pv, pos, size, Color.white);
		image.sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 64f, 1f), Vector2.one * 0.5f);
		return image;
	}

	private void Awake()
	{
		Vector2 vector = new Vector2(1f, 1f);
		big = R(base.transform, "armaAtual", vector, vector, new Vector2(-18f, -18f), new Vector2(264f, 82f));
		Image image = Grad(big, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0.22f, 0.55f);
		image.rectTransform.anchorMax = Vector2.one;
		image.color = new Color(0.55f, 0.62f, 0.7f, 1f);
		ammo = T(big, "", 32, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -6f), new Vector2(70f, 40f), TextAnchor.UpperLeft, W);
		maxT = T(big, "", 15, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -20f), new Vector2(60f, 22f), TextAnchor.UpperLeft, M);
		RectTransform rectTransform = R(big, "silhueta", vector, vector, new Vector2(-8f, -6f), new Vector2(140f, 50f));
		bigIcon = rectTransform.gameObject.AddComponent<RawImage>();
		bigIcon.raycastTarget = false;
		perk = T(big, "", 13, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 12f), new Vector2(244f, 18f), TextAnchor.LowerLeft, W);
		magBg = I(big, "pente", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 5f), new Vector2(244f, 3f), new Color(1f, 1f, 1f, 0.2f));
		magBar = Fill(magBg.transform, W);
		row = R(base.transform, "reservas", vector, vector, new Vector2(-18f, -106f), new Vector2(264f, 58f));
		smalls = new Small[4];
		for (int i = 0; i < 4; i++)
		{
			Small small = new Small();
			small.rt = R(row, "slot" + i, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(62f, 58f));
			small.bg = Grad(small.rt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 0.5f, 0.38f);
			small.bg.rectTransform.anchorMax = Vector2.one;
			small.bg.color = new Color(0.35f, 0.4f, 0.46f, 1f);
			RectTransform rectTransform2 = R(small.rt, "ic", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(54f, 28f));
			small.icon = rectTransform2.gameObject.AddComponent<RawImage>();
			small.icon.raycastTarget = false;
			small.n = T(small.rt, "∞", 14, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(60f, 18f), TextAnchor.LowerCenter, W);
			small.key = T(small.rt, "", 9, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(3f, -2f), new Vector2(12f, 12f), TextAnchor.UpperLeft, M);
			smalls[i] = small;
		}
		life = R(base.transform, "vida", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(380f, 50f));
		arT = T(life, "", 14, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(200f, 18f), TextAnchor.UpperLeft, new Color(0.62f, 0.8f, 1f));
		arBg = I(life, "colete", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -18f), new Vector2(380f, 5f), new Color(1f, 1f, 1f, 0.15f));
		arFill = Fill(arBg.transform, new Color(0.55f, 0.78f, 1f));
		hpT = T(life, "", 14, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 10f), new Vector2(200f, 18f), TextAnchor.LowerLeft, W);
		kitT = T(life, "", 14, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 10f), new Vector2(200f, 18f), TextAnchor.LowerRight, W);
		Image image2 = I(life, "hp", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(380f, 7f), new Color(1f, 1f, 1f, 0.15f));
		hpLag = Fill(image2.transform, new Color(1f, 1f, 1f, 0.35f));
		hpFill = Fill(image2.transform, W);
	}

	private static Texture2D Fist()
	{
		if ((bool)fist)
		{
			return fist;
		}
		fist = Resources.Load<Texture2D>("UI/hud_soco");
		if ((bool)fist)
		{
			return fist;
		}
		fist = new Texture2D(64, 36, TextureFormat.RGBA32, mipChain: false);
		for (int i = 0; i < 36; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = (float)(j - 32) / 13f;
				float num2 = (float)(i - 17) / 11f;
				float num3 = num * num + num2 * num2;
				bool flag = i > 22 && i < 30 && Mathf.Abs(j - 32) < 14 && (j - 18) % 7 < 6;
				fist.SetPixel(j, i, new Color(1f, 1f, 1f, (num3 < 1f || flag) ? 0.95f : 0f));
			}
		}
		fist.Apply();
		return fist;
	}

	private static string Perk(WeaponDef d)
	{
		FBWeaponModel.Choice choice = FBWeaponModel.Of(d);
		Dictionary<string, object> o = Json.O(Json.O(GameData.WeaponsRaw, d.id), "att");
		if (choice.scope >= 0 && choice.scope != Json.I(o, "scope", -1))
		{
			return N("scopes", choice.scope) ?? "Mira";
		}
		if (choice.magazine > 0)
		{
			return N("magazines", choice.magazine) ?? "Pente Estendido";
		}
		if (choice.muzzle > 0)
		{
			return N("muzzles", choice.muzzle) ?? "Silenciador";
		}
		if (!d.auto)
		{
			return "Semiautomática";
		}
		return "Automática";
		string N(string kind, int i)
		{
			return Json.S(FBWeaponModel.Att(d, kind, i), "name");
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBHudWeapons.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		FBPlayer i = FBPlayer.I;
		FBCharacter fBCharacter = (i ? i.ch : null);
		bool flag = (bool)fBCharacter && fBCharacter.alive && !i.Driving && (!FBMatch.I || FBMatch.I.mode == null || !FBMatch.I.mode.role);
		big.gameObject.SetActive(flag);
		row.gameObject.SetActive(flag);
		life.gameObject.SetActive((bool)fBCharacter && fBCharacter.alive && (!FBMatch.I || FBMatch.I.mode == null || !FBMatch.I.mode.role));
		if (!fBCharacter)
		{
			return;
		}
		float num = Mathf.Max(0f, fBCharacter.hp);
		float num2 = Mathf.Max(1f, fBCharacter.maxHp);
		float num3 = Mathf.Clamp01(num / num2);
		hpFill.fillAmount = num3;
		lag = Mathf.Max(num3, Mathf.MoveTowards(lag, num3, Time.deltaTime * 0.5f));
		hpLag.fillAmount = lag;
		hpFill.color = (fBCharacter.knocked ? new Color(1f, 0.5f, 0.25f) : ((num3 < 0.3f) ? new Color(1f, 0.35f, 0.3f) : W));
		hpT.text = $"HP {Mathf.CeilToInt(num)}/{Mathf.RoundToInt(num2)}";
		kitT.text = (FBMed.Busy ? "<color=#9be7b1>CURANDO</color>  " : "") + "KIT ✚ " + (FBMed.inf ? "∞" : FBMed.kits.ToString()) + "  <color=#ffffff99>[H]</color>";
		float num4 = ((fBCharacter.vest != null) ? fBCharacter.vest.dur : fBCharacter.armor);
		float num5 = ((fBCharacter.vest != null) ? fBCharacter.vest.max : 100f);
		bool flag2 = num4 > 0f;
		arBg.gameObject.SetActive(flag2);
		arT.gameObject.SetActive(flag2);
		if (flag2)
		{
			arFill.fillAmount = Mathf.Clamp01(num4 / Mathf.Max(1f, num5));
			arT.text = $"COLETE {Mathf.CeilToInt(num4)}/{Mathf.RoundToInt(num5)}";
		}
		if (!flag)
		{
			return;
		}
		FBWeaponUser wu = i.wu;
		WeaponDef cur = wu.Cur;
		big.gameObject.SetActive(value: true);
		magBg.gameObject.SetActive(cur != null);
		if (cur == null)
		{
			if (bigId != "_soco")
			{
				bigId = "_soco";
				bigIcon.texture = Fist();
				perk.text = "SOCO · CORPO A CORPO";
			}
			ammo.text = "∞";
			ammo.color = W;
			maxT.text = "";
		}
		else
		{
			if (bigId != cur.id)
			{
				bigId = cur.id;
				bigIcon.texture = FBIcons.Weapon(cur.id, 280, 100);
				perk.text = Perk(cur);
			}
			int num6 = wu.Ammo;
			ammo.text = ((wu.reloadT > 0f) ? "··" : num6.ToString());
			ammo.color = ((num6 <= Mathf.Max(1, cur.mag / 5)) ? new Color(1f, 0.4f, 0.35f) : W);
			maxT.text = "/" + cur.mag;
			maxT.rectTransform.anchoredPosition = new Vector2(10f + ammo.preferredWidth + 3f, -20f);
			magBar.fillAmount = ((wu.reloadT > 0f) ? (1f - Mathf.Clamp01(wu.reloadT / Mathf.Max(0.1f, 2.2f))) : Mathf.Clamp01((float)num6 / (float)Mathf.Max(1, cur.mag)));
		}
		int num7 = 0;
		for (int j = 0; j < 3; j++)
		{
			WeaponDef weaponDef = wu.slots[j];
			if (weaponDef != null && j != wu.cur)
			{
				Small small = smalls[num7++];
				small.rt.gameObject.SetActive(value: true);
				if (small.id != weaponDef.id)
				{
					small.id = weaponDef.id;
					small.icon.texture = FBIcons.Weapon(weaponDef.id, 160, 72);
				}
				small.n.text = ((wu.reserve[j] > 999) ? "∞" : (wu.ammo[j] + wu.reserve[j]).ToString());
				small.key.text = (j + 1).ToString();
			}
		}
		if (wu.cur != 3)
		{
			Small small2 = smalls[num7++];
			small2.rt.gameObject.SetActive(value: true);
			if (small2.id != "_maos")
			{
				small2.id = "_maos";
				small2.icon.texture = Fist();
			}
			small2.n.text = "∞";
			small2.key.text = "4";
		}
		for (int k = num7; k < smalls.Length; k++)
		{
			smalls[k].rt.gameObject.SetActive(value: false);
		}
		for (int l = 0; l < num7; l++)
		{
			smalls[l].rt.anchoredPosition = new Vector2(-(num7 - 1 - l) * 67, 0f);
		}
	}
}
}
