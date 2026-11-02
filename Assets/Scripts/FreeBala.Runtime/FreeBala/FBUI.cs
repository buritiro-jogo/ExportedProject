using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBUI
{
	public static readonly Color Gold = new Color(0.96f, 0.67f, 0.09f);

	public static readonly Color Red = new Color(0.93f, 0.23f, 0.2f);

	public static readonly Color Dark = new Color(0.04f, 0.05f, 0.06f, 0.92f);

	public static readonly Color Mut = new Color(0.62f, 0.66f, 0.7f);

	private static Font font;

	private static Font title;

	private static Sprite circle;

	private static Sprite hit;

	private static Sprite arrow;

	private static Sprite white;

	public static Font Font
	{
		get
		{
			if (!font)
			{
				Font result = Resources.Load<Font>("Fonts/LiberationSans") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
				font = result;
				return result;
			}
			return font;
		}
	}

	public static Font Title
	{
		get
		{
			if (!title)
			{
				Font result = Resources.Load<Font>("Fonts/RussoOne-Regular") ?? Font;
				title = result;
				return result;
			}
			return title;
		}
	}

	public static Sprite CircleSprite
	{
		get
		{
			if (!circle)
			{
				return circle = Make(64, (float x, float y) => (1f - Mathf.Sqrt(x * x + y * y)) * 32f);
			}
			return circle;
		}
	}

	public static Sprite WhiteSprite
	{
		get
		{
			if (!white)
			{
				return white = Make(4, (float x, float y) => 1f);
			}
			return white;
		}
	}

	public static Sprite HitSprite
	{
		get
		{
			if (!hit)
			{
				return hit = Make(64, delegate(float x, float y)
				{
					float num = Mathf.Abs(x);
					float num2 = Mathf.Abs(y);
					return ((num < 0.1f && num2 > 0.35f) || (num2 < 0.1f && num > 0.35f)) ? 1 : 0;
				});
			}
			return hit;
		}
	}

	public static Sprite ArrowSprite
	{
		get
		{
			if (!arrow)
			{
				return arrow = Make(64, delegate(float x, float y)
				{
					float num = (y + 1f) / 2f;
					return (Mathf.Abs(x) < (1f - num) * 0.9f && y > -0.8f) ? 1 : 0;
				});
			}
			return arrow;
		}
	}

	private static Sprite Make(int n, Func<float, float, float> a)
	{
		Texture2D texture2D = new Texture2D(n, n, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		Color[] array = new Color[n * n];
		for (int i = 0; i < n; i++)
		{
			for (int j = 0; j < n; j++)
			{
				array[i * n + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(a(((float)j + 0.5f) / (float)n * 2f - 1f, ((float)i + 0.5f) / (float)n * 2f - 1f)));
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, n, n), Vector2.one * 0.5f);
	}

	public static Canvas Canvas(string name, int order)
	{
		GameObject gameObject = new GameObject(name);
		Canvas canvas = gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = order;
		CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1600f, 900f);
		canvasScaler.matchWidthOrHeight = 0.5f;
		gameObject.AddComponent<GraphicRaycaster>();
		if (!UnityEngine.Object.FindAnyObjectByType<EventSystem>())
		{
			GameObject gameObject2 = new GameObject("EventSystem");
			gameObject2.AddComponent<EventSystem>();
			gameObject2.AddComponent<InputSystemUIInputModule>();
		}
		return canvas;
	}

	public static RectTransform Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
	{
		RectTransform obj = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
		obj.SetParent(parent, worldPositionStays: false);
		obj.anchorMin = aMin;
		obj.anchorMax = aMax;
		obj.pivot = new Vector2(0.5f, 0.5f);
		obj.anchoredPosition = pos;
		obj.sizeDelta = size;
		return obj;
	}

	public static Image Panel(Transform parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color c)
	{
		Image image = Rect(parent, name, aMin, aMax, pos, size).gameObject.AddComponent<Image>();
		image.color = c;
		return image;
	}

	public static Text Label(Transform parent, string text, int size, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 box, TextAnchor al = TextAnchor.MiddleCenter, bool title = false, Color? col = null)
	{
		Text text2 = Rect(parent, "txt", aMin, aMax, pos, box).gameObject.AddComponent<Text>();
		text2.text = text;
		text2.font = (title ? Title : Font);
		text2.fontSize = size;
		text2.alignment = al;
		text2.color = col ?? Color.white;
		text2.raycastTarget = false;
		text2.horizontalOverflow = HorizontalWrapMode.Wrap;
		text2.verticalOverflow = VerticalWrapMode.Overflow;
		return text2;
	}

	public static Button Button(Transform parent, string text, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Action onClick, bool primary = false)
	{
		Image image = Panel(parent, "btn_" + text, aMin, aMax, pos, size, primary ? Gold : new Color(1f, 1f, 1f, 0.08f));
		Button button = image.gameObject.AddComponent<Button>();
		button.onClick.AddListener(delegate
		{
			onClick?.Invoke();
		});
		ColorBlock colors = button.colors;
		colors.highlightedColor = (primary ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 1f, 1f, 0.18f));
		colors.pressedColor = Gold;
		button.colors = colors;
		Label(image.transform, text, primary ? 26 : 18, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true, primary ? new Color(0.07f, 0.07f, 0.07f) : Color.white);
		return button;
	}
}
}
