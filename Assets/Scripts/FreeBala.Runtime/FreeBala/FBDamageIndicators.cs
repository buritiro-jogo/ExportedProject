using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public class FBDamageIndicators : MonoBehaviour
{
	private class Ind
	{
		public FBCharacter src;

		public Vector3 at;

		public float t;

		public float k;

		public Image arc;

		public Image tip;

		public RectTransform rt;
	}

	private static FBDamageIndicators inst;

	private readonly List<Ind> list = new List<Ind>();

	private RectTransform root;

	private const float Life = 2.2f;

	private static Sprite arcS;

	private static Sprite tipS;

	public static FBDamageIndicators I
	{
		get
		{
			if (!inst)
			{
				return null;
			}
			return inst;
		}
	}

	private static Sprite Arc
	{
		get
		{
			if (!arcS)
			{
				return arcS = Gen(256, delegate(float x, float y)
				{
					float num = x - 0.5f;
					float num2 = y - 0.5f;
					float num3 = Mathf.Sqrt(num * num + num2 * num2) * 2f;
					float num4 = Mathf.Abs(Mathf.Atan2(num, num2)) * 57.29578f;
					float num5 = Mathf.Clamp01(1f - Mathf.Abs(num3 - 0.93f) / 0.07f);
					float num6 = Mathf.Clamp01((23f - num4) / 6f);
					return num5 * num6 * (0.55f + 0.45f * Mathf.Clamp01((num3 - 0.86f) / 0.14f));
				});
			}
			return arcS;
		}
	}

	private static Sprite Tip
	{
		get
		{
			if (!tipS)
			{
				return tipS = Gen(64, delegate(float x, float y)
				{
					float f = (x - 0.5f) * 2f;
					return (y > 0.1f && y < 0.9f && Mathf.Abs(f) < (0.9f - y) * 0.9f) ? 1 : 0;
				});
			}
			return tipS;
		}
	}

	private static Sprite Gen(int n, Func<float, float, float> a)
	{
		Texture2D texture2D = new Texture2D(n, n, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp
		};
		Color[] array = new Color[n * n];
		for (int i = 0; i < n; i++)
		{
			for (int j = 0; j < n; j++)
			{
				array[i * n + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(a(((float)j + 0.5f) / (float)n, ((float)i + 0.5f) / (float)n)));
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, n, n), Vector2.one * 0.5f);
	}

	public static void Ensure(Transform hudRoot)
	{
		if (!I)
		{
			GameObject obj = new GameObject("DirecaoDano", typeof(RectTransform));
			obj.transform.SetParent(hudRoot, worldPositionStays: false);
			RectTransform rectTransform = (RectTransform)obj.transform;
			Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(0.5f, 0.5f));
			rectTransform.anchorMin = anchorMin;
			rectTransform.sizeDelta = Vector2.zero;
			inst = obj.AddComponent<FBDamageIndicators>();
			inst.root = rectTransform;
		}
	}

	public static void Hit(FBCharacter src, Vector3 from, float dmg)
	{
		if (!I || !FBMatch.Player)
		{
			return;
		}
		Ind ind = null;
		foreach (Ind item in I.list)
		{
			if ((bool)src && item.src == src)
			{
				ind = item;
				break;
			}
		}
		if (ind == null)
		{
			if (I.list.Count >= 6)
			{
				Ind ind2 = I.list[0];
				foreach (Ind item2 in I.list)
				{
					if (item2.t < ind2.t)
					{
						ind2 = item2;
					}
				}
				ind = ind2;
			}
			else
			{
				ind = I.Make();
				I.list.Add(ind);
			}
			ind.src = src;
			ind.k = 0f;
		}
		ind.at = from;
		ind.t = 2.2f;
		ind.k = Mathf.Clamp01(ind.k * 0.6f + 0.45f + dmg / 60f);
	}

	private Ind Make()
	{
		GameObject gameObject = new GameObject("arco", typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(root, worldPositionStays: false);
		Image component = gameObject.GetComponent<Image>();
		component.sprite = Arc;
		component.raycastTarget = false;
		RectTransform rectTransform = (RectTransform)gameObject.transform;
		rectTransform.sizeDelta = Vector2.one * 300f;
		GameObject obj = new GameObject("seta", typeof(RectTransform), typeof(Image));
		obj.transform.SetParent(gameObject.transform, worldPositionStays: false);
		Image component2 = obj.GetComponent<Image>();
		component2.sprite = Tip;
		component2.raycastTarget = false;
		RectTransform obj2 = (RectTransform)obj.transform;
		obj2.sizeDelta = new Vector2(18f, 16f);
		obj2.anchoredPosition = new Vector2(0f, 163f);
		return new Ind
		{
			arc = component,
			tip = component2,
			rt = rectTransform
		};
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBDamageIndicators.Update");

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
		FBCharacter player = FBMatch.Player;
		FBPlayer i = FBPlayer.I;
		float deltaTime = Time.deltaTime;
		float num = (i ? i.ViewYaw : (Camera.main ? Camera.main.transform.eulerAngles.y : 0f));
		float num2 = (float)Screen.height / 1080f;
		for (int num3 = list.Count - 1; num3 >= 0; num3--)
		{
			Ind ind = list[num3];
			ind.t -= deltaTime;
			if (ind.t <= 0f || !player || !player.alive)
			{
				Image arc = ind.arc;
				Color color = (ind.tip.color = Color.clear);
				arc.color = color;
				if (ind.t <= 0f)
				{
					UnityEngine.Object.Destroy(ind.rt.gameObject);
					list.RemoveAt(num3);
				}
			}
			else
			{
				if ((bool)ind.src && ind.src.alive)
				{
					ind.at = ind.src.Pos;
				}
				Vector3 v = ind.at - player.Pos;
				float magnitude = v.Flat().magnitude;
				float num4 = Mathf.Atan2(v.x, v.z) * 57.29578f - num;
				ind.rt.localRotation = Quaternion.Euler(0f, 0f, 0f - num4);
				float num5 = Mathf.InverseLerp(15f, 70f, magnitude);
				ind.rt.sizeDelta = Vector2.one * 300f * num2;
				ind.rt.localScale = new Vector3(1f + num5 * 0.55f, 1f, 1f);
				float a = Mathf.Clamp01(ind.t / 0.6f) * (0.45f + 0.55f * ind.k);
				Color color2 = new Color(1f, 0.16f + 0.2f * (1f - ind.k), 0.1f, a);
				ind.arc.color = color2;
				bool flag = Mathf.Abs(Mathf.DeltaAngle(num4, 0f)) > 100f;
				ind.tip.color = (flag ? color2 : Color.clear);
				ind.tip.rectTransform.anchoredPosition = new Vector2(0f, 163f * num2);
				ind.tip.rectTransform.sizeDelta = new Vector2(18f, 16f) * num2;
			}
		}
	}

	private void OnDestroy()
	{
		if (inst == this)
		{
			inst = null;
		}
	}
}
}
