using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBUtil
{
	public static T GetOrAdd<T>(this GameObject g) where T : Component
	{
		T component = g.GetComponent<T>();
		if (!component)
		{
			return g.AddComponent<T>();
		}
		return component;
	}

	public static T GetOrAdd<T>(this Component c) where T : Component
	{
		return c.gameObject.GetOrAdd<T>();
	}

	public static float WrapA(float a)
	{
		return Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
	}

	public static float Rnd(float a, float b)
	{
		return a + Random.value * (b - a);
	}

	public static T Pick<T>(IList<T> l)
	{
		if (l.Count != 0)
		{
			return l[Random.Range(0, l.Count)];
		}
		return default(T);
	}

	public static float YawOf(Vector3 d)
	{
		return Mathf.Atan2(d.x, d.z);
	}

	public static Vector3 Flat(this Vector3 v)
	{
		return new Vector3(v.x, 0f, v.z);
	}
}
}
