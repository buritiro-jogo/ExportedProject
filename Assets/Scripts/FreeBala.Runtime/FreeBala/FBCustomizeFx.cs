using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBCustomizeFx : MonoBehaviour
{
	public enum Aspect
	{
		Portrait,
		Narrow,
		Wide,
		Ultra
	}

	private class Tw
	{
		public UnityEngine.Object key;

		public float t;

		public float dur;

		public float delay;

		public Action<float> step;

		public Action done;

		public bool back;
	}

	private static FBCustomizeFx inst;

	private readonly Dictionary<(UnityEngine.Object, int), Tw> runs = new Dictionary<(UnityEngine.Object, int), Tw>();

	private readonly List<(UnityEngine.Object, int)> dead = new List<(UnityEngine.Object, int)>();

	public static Aspect Current => Classify(Screen.width, Screen.height);

	private static FBCustomizeFx I
	{
		get
		{
			if (!inst)
			{
				inst = new GameObject("FB_CustomizeFx").AddComponent<FBCustomizeFx>();
			}
			return inst;
		}
	}

	public static Aspect Classify(float w, float h)
	{
		float num = w / Mathf.Max(1f, h);
		if (!(num < 1f))
		{
			if (!(num < 1.55f))
			{
				if (!(num < 2.05f))
				{
					return Aspect.Ultra;
				}
				return Aspect.Wide;
			}
			return Aspect.Narrow;
		}
		return Aspect.Portrait;
	}

	private static float OutCubic(float x)
	{
		return 1f - Mathf.Pow(1f - x, 3f);
	}

	private static float OutBack(float x)
	{
		return 1f + 2.70158f * Mathf.Pow(x - 1f, 3f) + 1.70158f * Mathf.Pow(x - 1f, 2f);
	}

	private static void Run(UnityEngine.Object key, int ch, float dur, float delay, Action<float> step, Action done = null, bool back = false)
	{
		if ((bool)key)
		{
			step(0f);
			I.runs[(key, ch)] = new Tw
			{
				key = key,
				dur = Mathf.Max(0.01f, dur),
				delay = delay,
				step = step,
				done = done,
				back = back
			};
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBCustomizeFx.Update");

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
		float num = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
		dead.Clear();
		foreach (KeyValuePair<(UnityEngine.Object, int), Tw> run in runs)
		{
			Tw value = run.Value;
			if (!value.key)
			{
				dead.Add(run.Key);
				continue;
			}
			if (value.delay > 0f)
			{
				value.delay -= num;
				continue;
			}
			value.t += num;
			float num2 = Mathf.Clamp01(value.t / value.dur);
			value.step(value.back ? OutBack(num2) : OutCubic(num2));
			if (num2 >= 1f)
			{
				dead.Add(run.Key);
			}
		}
		foreach (var item in dead)
		{
			if (runs.TryGetValue(item, out var value2))
			{
				runs.Remove(item);
				value2.done?.Invoke();
			}
		}
	}

	public static void Finish()
	{
		if (!inst)
		{
			return;
		}
		foreach (Tw value in inst.runs.Values)
		{
			if ((bool)value.key)
			{
				value.step(1f);
				value.done?.Invoke();
			}
		}
		inst.runs.Clear();
	}

	public static CanvasGroup Group(Component c)
	{
		CanvasGroup component = c.GetComponent<CanvasGroup>();
		if (!component)
		{
			return c.gameObject.AddComponent<CanvasGroup>();
		}
		return component;
	}

	public static void Fade(Component c, float from, float to, float dur = 0.22f, float delay = 0f, Action done = null)
	{
		if (!c)
		{
			return;
		}
		CanvasGroup g = Group(c);
		Run(g, 0, dur, delay, delegate(float k)
		{
			if ((bool)g)
			{
				g.alpha = Mathf.LerpUnclamped(from, to, k);
			}
		}, done);
	}

	public static void SlideIn(RectTransform r, Vector2 offset, float dur = 0.32f, float delay = 0f)
	{
		if (!r)
		{
			return;
		}
		Vector2 end = r.anchoredPosition;
		CanvasGroup g = Group(r);
		Run(r, 1, dur, delay, delegate(float k)
		{
			if ((bool)r)
			{
				r.anchoredPosition = Vector2.LerpUnclamped(end + offset, end, k);
				g.alpha = Mathf.Clamp01(k * 1.4f);
			}
		});
	}

	public static void SlideOut(RectTransform r, Vector2 offset, float dur = 0.2f, Action done = null)
	{
		if (!r)
		{
			return;
		}
		Vector2 start = r.anchoredPosition;
		CanvasGroup g = Group(r);
		Run(r, 1, dur, 0f, delegate(float k)
		{
			if ((bool)r)
			{
				r.anchoredPosition = Vector2.LerpUnclamped(start, start + offset, k);
				g.alpha = 1f - k;
			}
		}, done);
	}

	public static void Pop(Transform t, float from = 0.9f, float dur = 0.28f, float delay = 0f, Vector3? to = null)
	{
		if (!t)
		{
			return;
		}
		Vector3 end = to ?? Vector3.one;
		Run(t, 2, dur, delay, delegate(float k)
		{
			if ((bool)t)
			{
				t.localScale = Vector3.LerpUnclamped(end * from, end, k);
			}
		}, null, back: true);
	}

	public static void ScaleTo(Transform t, Vector3 to, float dur = 0.25f)
	{
		if (!t)
		{
			return;
		}
		Vector3 from = t.localScale;
		Run(t, 2, dur, 0f, delegate(float k)
		{
			if ((bool)t)
			{
				t.localScale = Vector3.LerpUnclamped(from, to, k);
			}
		});
	}
}
}
