using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public class FBKillFeed : MonoBehaviour
{
	private class Row
	{
		public RectTransform rt;

		public CanvasGroup cg;

		public float t;

		public float w;
	}

	private readonly List<Row> rows = new List<Row>();

	private RectTransform box;

	private static FBKillFeed I;

	private static readonly Color Ally = new Color(0.4f, 0.68f, 1f);

	private static readonly Color Enemy = new Color(1f, 0.4f, 0.36f);

	private static readonly Color Me = new Color(1f, 0.78f, 0.25f);

	private const float Life = 5f;

	private const float H = 30f;

	private const float Gap = 4f;

	private static Texture2D skull;

	public static FBKillFeed Attach(Transform canvasRoot)
	{
		GameObject obj = new GameObject("FeedAbates", typeof(RectTransform));
		obj.transform.SetParent(canvasRoot, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)obj.transform;
		Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(1f, 1f));
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = new Vector2(1f, 1f);
		rectTransform.anchoredPosition = new Vector2(-18f, -180f);
		rectTransform.sizeDelta = new Vector2(520f, 200f);
		I = obj.AddComponent<FBKillFeed>();
		I.box = rectTransform;
		return I;
	}

	private static Color ColorOf(FBCharacter a)
	{
		if ((bool)a)
		{
			if (!a.isPlayer)
			{
				if (!FBMatch.FFA)
				{
					if (a.team != FBSession.team)
					{
						return Enemy;
					}
					return Ally;
				}
				return Color.white;
			}
			return Me;
		}
		return Color.white;
	}

	private Text Txt(Transform p, string s, Color c, int size = 17)
	{
		Text component = new GameObject("t", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
		component.transform.SetParent(p, worldPositionStays: false);
		component.font = FBUI.Title;
		component.fontSize = size;
		component.color = c;
		component.text = s;
		component.raycastTarget = false;
		component.alignment = TextAnchor.MiddleCenter;
		component.horizontalOverflow = HorizontalWrapMode.Overflow;
		component.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
		component.gameObject.AddComponent<LayoutElement>();
		return component;
	}

	private Row NewRow(bool me)
	{
		GameObject gameObject = new GameObject("linha", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
		gameObject.transform.SetParent(box, worldPositionStays: false);
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(1f, 1f));
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = new Vector2(1f, 1f);
		Image component = gameObject.GetComponent<Image>();
		component.color = (me ? new Color(0.18f, 0.13f, 0.03f, 0.78f) : new Color(0.03f, 0.04f, 0.05f, 0.66f));
		component.raycastTarget = false;
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.padding = new RectOffset(12, 12, 2, 2);
		horizontalLayoutGroup.spacing = 8f;
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.childForceExpandWidth = false;
		gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		if (me)
		{
			GameObject obj = new GameObject("fio", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
			obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
			obj.GetComponent<Image>().color = Me;
			obj.GetComponent<LayoutElement>().ignoreLayout = true;
			RectTransform obj2 = (RectTransform)obj.transform;
			obj2.anchorMin = new Vector2(1f, 0f);
			obj2.anchorMax = new Vector2(1f, 1f);
			obj2.pivot = new Vector2(1f, 0.5f);
			obj2.sizeDelta = new Vector2(3f, 0f);
			obj2.anchoredPosition = Vector2.zero;
		}
		Row row = new Row
		{
			rt = rectTransform,
			cg = gameObject.GetComponent<CanvasGroup>(),
			t = 0f
		};
		rows.Insert(0, row);
		while (rows.Count > 5)
		{
			List<Row> list = rows;
			Object.Destroy(list[list.Count - 1].rt.gameObject);
			rows.RemoveAt(rows.Count - 1);
		}
		return row;
	}

	public static void Kill(FBCharacter src, FBCharacter victim, string why, bool head, string weaponId)
	{
		if ((bool)I && (bool)victim)
		{
			bool me = ((bool)src && src.isPlayer) || victim.isPlayer;
			Row row = I.NewRow(me);
			if ((bool)src && src != victim)
			{
				I.Txt(row.rt, src.Name.ToUpperInvariant(), ColorOf(src));
			}
			Texture2D texture2D = ((why == null && !string.IsNullOrEmpty(weaponId)) ? FBIcons.Weapon(weaponId, 128, 56) : null);
			if ((bool)texture2D)
			{
				GameObject obj = new GameObject("arma", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
				obj.transform.SetParent(row.rt, worldPositionStays: false);
				obj.GetComponent<RawImage>().texture = texture2D;
				obj.GetComponent<RawImage>().raycastTarget = false;
				LayoutElement component = obj.GetComponent<LayoutElement>();
				component.preferredWidth = 64f;
				component.preferredHeight = 28f;
			}
			else
			{
				I.Txt(row.rt, why switch
				{
					"bleed" => "SANGROU", 
					"car" => "ATROPELADO", 
					"fall" => "QUEDA", 
					"zone" => "☁ ZONA", 
					_ => "✖", 
				}, new Color(1f, 1f, 1f, 0.7f), 13);
			}
			GameObject obj2 = new GameObject("caveira", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
			obj2.transform.SetParent(row.rt, worldPositionStays: false);
			RawImage component2 = obj2.GetComponent<RawImage>();
			component2.texture = Skull();
			component2.color = (head ? new Color(1f, 0.28f, 0.22f) : new Color(1f, 1f, 1f, 0.9f));
			component2.raycastTarget = false;
			LayoutElement component3 = obj2.GetComponent<LayoutElement>();
			component3.preferredWidth = 22f;
			component3.preferredHeight = 22f;
			if (head)
			{
				I.Txt(row.rt, "CABEÇA", new Color(1f, 0.3f, 0.25f), 12);
			}
			I.Txt(row.rt, victim.Name.ToUpperInvariant(), ColorOf(victim));
		}
	}

	public static void Message(string text, bool me)
	{
		if ((bool)I)
		{
			Row row = I.NewRow(me);
			I.Txt(row.rt, text, me ? Me : new Color(1f, 1f, 1f, 0.85f), 15);
		}
	}

	private static Texture2D Skull()
	{
		if ((bool)skull)
		{
			return skull;
		}
		skull = new Texture2D(48, 48, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Bilinear
		};
		for (int i = 0; i < 48; i++)
		{
			for (int j = 0; j < 48; j++)
			{
				float num = ((float)j + 0.5f) / 48f * 2f - 1f;
				float num2 = ((float)i + 0.5f) / 48f * 2f - 1f;
				bool num3 = num * num / 0.62f + (num2 - 0.18f) * (num2 - 0.18f) / 0.5f < 1f;
				bool flag = Mathf.Abs(num) < 0.42f && num2 > -0.78f && num2 < -0.2f;
				bool flag2 = (num + 0.3f) * (num + 0.3f) + (num2 - 0.08f) * (num2 - 0.08f) < 0.045f || (num - 0.3f) * (num - 0.3f) + (num2 - 0.08f) * (num2 - 0.08f) < 0.045f;
				bool flag3 = Mathf.Abs(num) < 0.09f - (num2 + 0.3f) * 0.2f && num2 > -0.38f && num2 < -0.16f;
				bool flag4 = num2 < -0.5f && num2 > -0.74f && Mathf.Abs(num) < 0.38f && Mathf.Repeat((num + 0.38f) / 0.19f, 1f) < 0.16f;
				float a = (((num3 || flag) && !flag2 && !flag3 && !flag4) ? 1 : 0);
				skull.SetPixel(j, i, new Color(1f, 1f, 1f, a));
			}
		}
		skull.Apply();
		return skull;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBKillFeed.Update");

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
		float num = 0f;
		float deltaTime = Time.deltaTime;
		for (int num2 = rows.Count - 1; num2 >= 0; num2--)
		{
			Row row = rows[num2];
			row.t += deltaTime;
			if (row.t > 5f)
			{
				Object.Destroy(row.rt.gameObject);
				rows.RemoveAt(num2);
			}
		}
		for (int i = 0; i < rows.Count; i++)
		{
			Row row2 = rows[i];
			float num3 = Mathf.Clamp01(row2.t / 0.18f);
			float b = 1f - Mathf.Clamp01((row2.t - 4.4f) / 0.6f);
			row2.cg.alpha = Mathf.Min(num3, b);
			Vector2 vector = new Vector2((1f - Mathf.SmoothStep(0f, 1f, num3)) * 80f, 0f - num);
			row2.rt.anchoredPosition = Vector2.Lerp(row2.rt.anchoredPosition, vector, 1f - Mathf.Exp((0f - deltaTime) * 18f));
			if (row2.t < deltaTime * 1.5f)
			{
				row2.rt.anchoredPosition = vector;
			}
			num += 34f;
			row2.rt.sizeDelta = new Vector2(row2.rt.sizeDelta.x, 30f);
		}
	}
}
}
