using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBKit
{
	public static readonly Color Bg = new Color(0.035f, 0.04f, 0.05f, 0.94f);

	public static readonly Color Card = new Color(1f, 1f, 1f, 0.045f);

	public static readonly Color CardHi = new Color(1f, 1f, 1f, 0.09f);

	public static readonly Color Line = new Color(1f, 1f, 1f, 0.1f);

	public static readonly Color Txt = new Color(0.93f, 0.92f, 0.88f);

	public static readonly Color Dim = new Color(0.93f, 0.92f, 0.88f, 0.55f);

	public static readonly Color Gold = new Color(1f, 0.74f, 0.12f);

	public static readonly Color GoldDark = new Color(0.16f, 0.1f, 0f);

	public const float MaxW = 1240f;

	private static Texture2D grad;

	public static RectTransform Body(Transform page, float top = 64f, float bottom = 0f)
	{
		RectTransform rectTransform = FBUI.Rect(page, "corpo", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
		rectTransform.offsetMin = new Vector2(-620f, bottom);
		rectTransform.offsetMax = new Vector2(620f, 0f - top);
		return rectTransform;
	}

	public static RectTransform At(Transform p, string n, float x, float y, float w, float h)
	{
		RectTransform rectTransform = FBUI.Rect(p, n, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(w, h));
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.anchoredPosition = new Vector2(x, 0f - y);
		return rectTransform;
	}

	public static Image Box(Transform p, string n, float x, float y, float w, float h, Color? c = null, bool border = true)
	{
		Image image = At(p, n, x, y, w, h).gameObject.AddComponent<Image>();
		image.color = c ?? Card;
		image.raycastTarget = false;
		if (border)
		{
			Outline outline = image.gameObject.AddComponent<Outline>();
			outline.effectColor = Line;
			outline.effectDistance = new Vector2(1f, -1f);
		}
		return image;
	}

	public static Text T(Transform p, string s, int size, float x, float y, float w, float h, TextAnchor al = TextAnchor.MiddleLeft, bool title = false, Color? c = null)
	{
		Text text = FBUI.Label(p, s, size, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(w, h), al, title, c ?? Txt);
		RectTransform rectTransform = text.rectTransform;
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.anchoredPosition = new Vector2(x, 0f - y);
		return text;
	}

	public static void Section(Transform p, string s, float x, float y, float w)
	{
		T(p, s, 13, x + 12f, y, w, 20f, TextAnchor.MiddleLeft, title: true, Dim);
		Image image = At(p, "acento", x, y + 3f, 3f, 14f).gameObject.AddComponent<Image>();
		image.color = Gold;
		image.raycastTarget = false;
	}

	public static Button Tile(Transform p, string n, float x, float y, float w, float h, bool sel, Action click)
	{
		Image im = Box(p, n, x, y, w, h, sel ? new Color(1f, 0.74f, 0.12f, 0.14f) : Card);
		im.raycastTarget = true;
		if (sel)
		{
			im.GetComponent<Outline>().effectColor = new Color(1f, 0.74f, 0.12f, 0.9f);
			Image image = At(im.transform, "sel", 0f, h - 3f, w, 3f).gameObject.AddComponent<Image>();
			image.color = Gold;
			image.raycastTarget = false;
		}
		Button button = im.gameObject.AddComponent<Button>();
		button.targetGraphic = im;
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener(delegate
		{
			FBAudio.Synth("hover");
			click?.Invoke();
		});
		Color baseC = im.color;
		EventTrigger tr = im.gameObject.AddComponent<EventTrigger>();
		E(EventTriggerType.PointerEnter, delegate
		{
			im.color = (sel ? baseC : CardHi);
		});
		E(EventTriggerType.PointerExit, delegate
		{
			im.color = baseC;
		});
		return button;
		void E(EventTriggerType t, Action a)
		{
			EventTrigger.Entry entry = new EventTrigger.Entry
			{
				eventID = t
			};
			entry.callback.AddListener(delegate
			{
				a();
			});
			tr.triggers.Add(entry);
		}
	}

	public static Button TextTile(Transform p, string s, float x, float y, float w, float h, bool sel, Action click, int size = 15)
	{
		Button button = Tile(p, "b_" + s, x, y, w, h, sel, click);
		T(button.transform, s, size, 0f, 0f, w, h, TextAnchor.MiddleCenter, title: true, sel ? Color.white : Txt);
		return button;
	}

	public static Button Primary(Transform p, string s, float x, float y, float w, float h, Action click)
	{
		Image image = Box(p, "b_" + s, x, y, w, h, Gold, border: false);
		image.raycastTarget = true;
		Button button = image.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.highlightedColor = new Color(1f, 0.93f, 0.75f);
		colors.pressedColor = new Color(0.85f, 0.75f, 0.55f);
		button.colors = colors;
		button.onClick.AddListener(delegate
		{
			FBAudio.Synth("hover");
			click?.Invoke();
		});
		T(image.transform, s, 18, 0f, 0f, w, h, TextAnchor.MiddleCenter, title: true, GoldDark);
		return button;
	}

	public static RawImage Cover(Transform p, Texture tex, float x, float y, float w, float h)
	{
		RawImage rawImage = At(p, "img", x, y, w, h).gameObject.AddComponent<RawImage>();
		rawImage.raycastTarget = false;
		rawImage.texture = tex;
		if ((bool)tex)
		{
			float num = (float)tex.width / (float)tex.height;
			float num2 = w / h;
			rawImage.uvRect = ((num > num2) ? new Rect((1f - num2 / num) / 2f, 0f, num2 / num, 1f) : new Rect(0f, (1f - num / num2) / 2f, 1f, num / num2));
		}
		else
		{
			rawImage.color = new Color(0.12f, 0.13f, 0.15f);
		}
		return rawImage;
	}

	public static void Shade(Transform p, float x, float y, float w, float h)
	{
		Texture2D texture = (grad ? grad : (grad = Grad()));
		RawImage rawImage = At(p, "sombra", x, y, w, h).gameObject.AddComponent<RawImage>();
		rawImage.texture = texture;
		rawImage.raycastTarget = false;
	}

	private static Texture2D Grad()
	{
		Texture2D texture2D = new Texture2D(1, 64, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		for (int i = 0; i < 64; i++)
		{
			texture2D.SetPixel(0, i, new Color(0f, 0f, 0f, Mathf.Pow(1f - (float)i / 63f, 1.6f) * 0.85f));
		}
		texture2D.Apply();
		return texture2D;
	}

	public static RectTransform Scroll(Transform p, float x, float y, float w, float h, float spacing = 6f)
	{
		Image image = Box(p, "lista", x, y, w, h, new Color(0f, 0f, 0f, 0.25f));
		image.raycastTarget = true;
		image.gameObject.AddComponent<RectMask2D>();
		GameObject gameObject = new GameObject("c", typeof(RectTransform));
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		rectTransform.SetParent(image.transform, worldPositionStays: false);
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(1f, 1f);
		rectTransform.pivot = new Vector2(0.5f, 1f);
		rectTransform.sizeDelta = Vector2.zero;
		VerticalLayoutGroup verticalLayoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.spacing = spacing;
		verticalLayoutGroup.padding = new RectOffset(8, 8, 8, 8);
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		ScrollRect scrollRect = image.gameObject.AddComponent<ScrollRect>();
		scrollRect.content = rectTransform;
		scrollRect.horizontal = false;
		scrollRect.scrollSensitivity = 30f;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		return rectTransform;
	}

	public static Button Row(Transform list, string title, string sub, bool sel, Action click, float h = 40f)
	{
		GameObject gameObject = new GameObject("linha", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
		gameObject.transform.SetParent(list, worldPositionStays: false);
		gameObject.GetComponent<LayoutElement>().preferredHeight = h;
		Image im = gameObject.GetComponent<Image>();
		Color baseC = (sel ? new Color(1f, 0.74f, 0.12f, 0.16f) : new Color(1f, 1f, 1f, 0.035f));
		im.color = baseC;
		if (sel)
		{
			FBUI.Panel(gameObject.transform, "acento", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1.5f, 0f), new Vector2(3f, 0f), Gold).raycastTarget = false;
		}
		Text text = FBUI.Label(gameObject.transform, title, 15, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleLeft, title: true, sel ? Color.white : Txt);
		text.rectTransform.offsetMin = new Vector2(16f, 0f);
		text.rectTransform.offsetMax = new Vector2(-110f, 0f);
		if (!string.IsNullOrEmpty(sub))
		{
			Text text2 = FBUI.Label(gameObject.transform, sub, 11, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleRight, title: true, Dim);
			text2.rectTransform.offsetMin = new Vector2(0f, 0f);
			text2.rectTransform.offsetMax = new Vector2(-14f, 0f);
		}
		Button button = gameObject.AddComponent<Button>();
		button.targetGraphic = im;
		button.transition = Selectable.Transition.None;
		button.onClick.AddListener(delegate
		{
			FBAudio.Synth("hover");
			click?.Invoke();
		});
		EventTrigger eventTrigger = gameObject.AddComponent<EventTrigger>();
		EventTrigger.Entry entry = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerEnter
		};
		entry.callback.AddListener(delegate
		{
			if (!sel)
			{
				im.color = CardHi;
			}
		});
		eventTrigger.triggers.Add(entry);
		EventTrigger.Entry entry2 = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerExit
		};
		entry2.callback.AddListener(delegate
		{
			im.color = baseC;
		});
		eventTrigger.triggers.Add(entry2);
		return button;
	}
}
}
