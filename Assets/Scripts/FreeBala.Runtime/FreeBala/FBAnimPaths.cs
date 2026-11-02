using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBAnimPaths
{
	private class P
	{
		public Vector3[] t;

		public Vector3 hd;

		public float hl;

		public float dy;
	}

	private static Dictionary<string, P> map;

	private static float N(object o)
	{
		if (!(o is double num))
		{
			return (o is long num2) ? num2 : ((o is int num3) ? num3 : 0);
		}
		return (float)num;
	}

	private static void Load()
	{
		map = new Dictionary<string, P>();
		TextAsset textAsset = Resources.Load<TextAsset>("FBAnimPaths");
		if (!textAsset || !(Json.Parse(textAsset.text) is Dictionary<string, object> dictionary))
		{
			return;
		}
		foreach (KeyValuePair<string, object> item in dictionary)
		{
			List<object> list = Json.L(item.Value, "t");
			if (list != null && list.Count >= 2)
			{
				Vector3[] array = new Vector3[list.Count];
				for (int i = 0; i < list.Count; i++)
				{
					List<object> list2 = list[i] as List<object>;
					array[i] = ((list2 != null && list2.Count >= 3) ? new Vector3(N(list2[0]), N(list2[1]), N(list2[2])) : Vector3.zero);
				}
				Vector3 vector = array[array.Length - 1] - array[0];
				vector.y = 0f;
				Dictionary<string, P> dictionary2 = map;
				string key = item.Key;
				P obj = new P
				{
					t = array,
					hd = ((vector.sqrMagnitude > 1E-06f) ? vector.normalized : Vector3.forward),
					hl = Mathf.Max(0.05f, vector.magnitude)
				};
				obj.dy = array[array.Length - 1].y - array[0].y;
				dictionary2[key] = obj;
			}
		}
	}

	public static bool Offset(string clip, float k, out Vector3 d)
	{
		d = Vector3.zero;
		if (map == null)
		{
			Load();
		}
		if (clip == null || !map.TryGetValue(clip, out var value))
		{
			return false;
		}
		float num = Mathf.Clamp01(k) * (float)(value.t.Length - 1);
		int num2 = Mathf.Min((int)num, value.t.Length - 2);
		d = Vector3.Lerp(value.t[num2], value.t[num2 + 1], num - (float)num2) - value.t[0];
		return true;
	}

	public static bool Has(string clip)
	{
		if (map == null)
		{
			Load();
		}
		if (clip != null)
		{
			return map.ContainsKey(clip);
		}
		return false;
	}

	public static bool Eval(string clip, float k, out float wh, out float wv, out float lat, out float bump)
	{
		wh = (wv = Mathf.Clamp01(k));
		lat = (bump = 0f);
		if (map == null)
		{
			Load();
		}
		if (clip == null || !map.TryGetValue(clip, out var value))
		{
			return false;
		}
		float num = Mathf.Clamp01(k) * (float)(value.t.Length - 1);
		int num2 = Mathf.Min((int)num, value.t.Length - 2);
		Vector3 vector = Vector3.Lerp(value.t[num2], value.t[num2 + 1], num - (float)num2) - value.t[0];
		Vector3 vector2 = new Vector3(vector.x, 0f, vector.z);
		float num3 = Vector3.Dot(vector2, value.hd);
		wh = num3 / value.hl;
		lat = Vector3.Dot(vector2 - value.hd * num3, Vector3.Cross(Vector3.up, value.hd));
		if (Mathf.Abs(value.dy) > 0.04f)
		{
			wv = vector.y / value.dy;
			bump = 0f;
		}
		else
		{
			wv = wh;
			bump = vector.y - value.dy * wh;
		}
		return true;
	}
}
}
