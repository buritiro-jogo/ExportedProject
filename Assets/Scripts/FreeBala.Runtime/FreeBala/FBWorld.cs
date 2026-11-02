using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBWorld
{
	public class Water
	{
		public List<Vector2> poly = new List<Vector2>();

		public float y;

		public Rect box;
	}

	public const int LMap = 8;

	public const int LHitbox = 9;

	public const int LChar = 10;

	public const int LVehicle = 11;

	public const int LGlass = 12;

	public const int LTrigger = 13;

	public const int LFirstPerson = 14;

	public static readonly string[] SURF = new string[6] { "concrete", "dirt", "sand", "mud", "metal", "wood" };

	private static readonly (Regex rx, int k)[] SURF_RX = new(Regex, int)[5]
	{
		(new Regex("mud|lama|barro|brejo", RegexOptions.IgnoreCase), 3),
		(new Regex("sand|areia|praia", RegexOptions.IgnoreCase), 2),
		(new Regex("dirt|terra|soil|grass|grama|lawn|mato|jardim|canteiro|campo|trilha", RegexOptions.IgnoreCase), 1),
		(new Regex("metal|ferro|steel|chapa|zinco|lata|grade", RegexOptions.IgnoreCase), 4),
		(new Regex("wood|madeira|tabua|deck", RegexOptions.IgnoreCase), 5)
	};

	public static readonly List<Water> WaterList = new List<Water>();

	public static int SolidMask => 2305;

	public static int ShotMask => SolidMask | 0x200 | 0x1000;

	public static int SurfOf(string name)
	{
		if (name != null)
		{
			(Regex, int)[] sURF_RX = SURF_RX;
			for (int i = 0; i < sURF_RX.Length; i++)
			{
				var (regex, result) = sURF_RX[i];
				if (regex.IsMatch(name))
				{
					return result;
				}
			}
		}
		return 0;
	}

	public static bool RayHit(Vector3 o, Vector3 dir, float far, out RaycastHit hit, float radius = 0f, float near = 0f)
	{
		if (near > 0f)
		{
			o += dir * near;
			far -= near;
		}
		if (far <= 0f)
		{
			hit = default(RaycastHit);
			return false;
		}
		if (!(radius > 0f))
		{
			return Physics.Raycast(o, dir, out hit, far, SolidMask, QueryTriggerInteraction.Ignore);
		}
		return Physics.SphereCast(o, radius, dir, out hit, far, SolidMask, QueryTriggerInteraction.Ignore);
	}

	public static bool RayHit(Vector3 o, Vector3 dir, float far, float radius = 0f)
	{
		RaycastHit hit;
		return RayHit(o, dir, far, out hit, radius);
	}

	public static float? GroundY(float x, float y, float z, float up = 1f, float far = 4f)
	{
		if (!Physics.Raycast(new Vector3(x, y + up, z), Vector3.down, out var hitInfo, up + far, SolidMask, QueryTriggerInteraction.Ignore))
		{
			return null;
		}
		return hitInfo.point.y;
	}

	public static bool ClearLine(Vector3 a, Vector3 b)
	{
		float[] array = new float[2] { 0.6f, 1.5f };
		foreach (float num in array)
		{
			Vector3 vector = new Vector3(a.x, a.y + num, a.z);
			Vector3 vector2 = new Vector3(b.x, b.y + num, b.z) - vector;
			float magnitude = vector2.magnitude;
			if (!(magnitude < 0.01f) && RayHit(vector, vector2 / magnitude, magnitude))
			{
				return false;
			}
		}
		return true;
	}

	public static int SurfaceAt(RaycastHit h)
	{
		if (!h.collider)
		{
			return 0;
		}
		FBSurface component = h.collider.GetComponent<FBSurface>();
		if ((bool)component)
		{
			return component.kind;
		}
		Renderer component2 = h.collider.GetComponent<Renderer>();
		return SurfOf(h.collider.name + " " + (((bool)component2 && (bool)component2.sharedMaterial) ? component2.sharedMaterial.name : ""));
	}

	public static int SurfaceBelow(Vector3 p)
	{
		if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hitInfo, 2f, SolidMask, QueryTriggerInteraction.Ignore))
		{
			return 0;
		}
		return SurfaceAt(hitInfo);
	}

	public static void SetWater(Dictionary<string, object> level)
	{
		WaterList.Clear();
		List<object> list = Json.L(level, "water");
		if (list == null)
		{
			return;
		}
		foreach (object item2 in list)
		{
			if (item2 is List<object> { Count: >=4 } list2)
			{
				Water water = new Water
				{
					y = Json.Fv((list2[list2.Count - 1] as List<object>)[0])
				};
				float num = 1E+09f;
				float num2 = 1E+09f;
				float num3 = -1E+09f;
				float num4 = -1E+09f;
				for (int i = 0; i < list2.Count - 1; i++)
				{
					List<object> list3 = list2[i] as List<object>;
					Vector2 item = new Vector2(0f - Json.Fv(list3[0]), Json.Fv(list3[1]));
					water.poly.Add(item);
					num = Mathf.Min(num, item.x);
					num3 = Mathf.Max(num3, item.x);
					num2 = Mathf.Min(num2, item.y);
					num4 = Mathf.Max(num4, item.y);
				}
				water.box = Rect.MinMaxRect(num, num2, num3, num4);
				WaterList.Add(water);
			}
		}
	}

	private static bool InPoly(List<Vector2> poly, float x, float z)
	{
		bool flag = false;
		int num = 0;
		int index = poly.Count - 1;
		while (num < poly.Count)
		{
			Vector2 vector = poly[num];
			Vector2 vector2 = poly[index];
			if (vector.y > z != vector2.y > z && x < (vector2.x - vector.x) * (z - vector.y) / (vector2.y - vector.y) + vector.x)
			{
				flag = !flag;
			}
			index = num++;
		}
		return flag;
	}

	public static float? WaterAt(float x, float z)
	{
		foreach (Water water in WaterList)
		{
			if (water.box.Contains(new Vector2(x, z)) && InPoly(water.poly, x, z))
			{
				return water.y;
			}
		}
		return null;
	}

	public static bool WaterHit(Vector3 o, Vector3 dir, float far, out Vector3 p)
	{
		p = default(Vector3);
		if (WaterList.Count == 0 || Mathf.Abs(dir.y) < 0.0001f)
		{
			return false;
		}
		foreach (Water water in WaterList)
		{
			float num = (water.y - o.y) / dir.y;
			if (num <= 0f || num > far)
			{
				continue;
			}
			Vector3 vector = o + dir * num;
			float? num2 = WaterAt(vector.x, vector.z);
			if (num2.HasValue)
			{
				float valueOrDefault = num2.GetValueOrDefault();
				if (Mathf.Abs(valueOrDefault - water.y) < 0.01f)
				{
					p = vector;
					return true;
				}
			}
		}
		return false;
	}
}
}
