using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public static class FBRoads
{
	public static readonly Regex RoadRx = new Regex("asphalt|asfalto|road|tarmac", RegexOptions.IgnoreCase);

	private static readonly Regex CoverRx = new Regex("^(PA_Conteiner|VJ_Cob_conteiner)|_Cob_conteiner", RegexOptions.IgnoreCase);

	public static readonly List<FBSeg> segs = new List<FBSeg>();

	public static readonly List<FBSpot> spots = new List<FBSpot>();

	public static readonly Dictionary<int, Vector3> junctions = new Dictionary<int, Vector3>();

	private static byte[] grid;

	private static float gx0;

	private static float gz0;

	private static float gcell = 1f;

	private static int gnx;

	private static int gnz;

	public static string loaded;

	public static bool Ready => segs.Count > 0;

	public static bool IsCoverContainer(Transform t)
	{
		if (!t || !CoverRx.IsMatch(t.name))
		{
			return false;
		}
		Transform parent = t.parent;
		while ((bool)parent)
		{
			if (CoverRx.IsMatch(parent.name))
			{
				return false;
			}
			parent = parent.parent;
		}
		return true;
	}

	public static bool Load(string mapId)
	{
		if (loaded == mapId && Ready)
		{
			return true;
		}
		segs.Clear();
		spots.Clear();
		junctions.Clear();
		grid = null;
		loaded = mapId;
		TextAsset textAsset = Resources.Load<TextAsset>("FBRoads/" + mapId);
		if (!textAsset)
		{
			Debug.Log("[FreeBala][ruas] " + mapId + ": sem malha de faixas");
			return false;
		}
		if (!(Json.Parse(textAsset.text) is Dictionary<string, object> o))
		{
			return false;
		}
		gx0 = Json.F(o, "gx0");
		gz0 = Json.F(o, "gz0");
		gcell = Json.F(o, "cell", 1f);
		gnx = Json.I(o, "gnx");
		gnz = Json.I(o, "gnz");
		try
		{
			grid = Convert.FromBase64String(Json.S(o, "road") ?? "");
		}
		catch
		{
			grid = null;
		}
		foreach (object item in Json.L(o, "segs") ?? new List<object>())
		{
			Dictionary<string, object> o2 = item as Dictionary<string, object>;
			FBSeg fBSeg = new FBSeg
			{
				id = Json.I(o2, "id"),
				kind = Json.I(o2, "k")
			};
			fBSeg.p = Pts(Json.L(o2, "p"));
			fBSeg.next = Ints(Json.L(o2, "n"));
			if (fBSeg.kind == 0)
			{
				fBSeg.w = Json.F(o2, "w", 6f);
				fBSeg.nl = Json.I(o2, "nl", 1);
				fBSeg.li = Json.I(o2, "li");
				fBSeg.one = Json.I(o2, "one") != 0;
				fBSeg.ring = Json.I(o2, "ring") != 0;
				fBSeg.left = Json.I(o2, "lf", -1);
				fBSeg.right = Json.I(o2, "rt", -1);
				fBSeg.opp = Json.I(o2, "op", -1);
			}
			else
			{
				string text = Json.S(o2, "t");
				fBSeg.turn = (string.IsNullOrEmpty(text) ? 'S' : text[0]);
				fBSeg.junction = Json.I(o2, "j", -1);
				fBSeg.conf = Ints(Json.L(o2, "c"));
			}
			Measure(fBSeg);
			segs.Add(fBSeg);
		}
		foreach (FBSeg seg in segs)
		{
			int[] next = seg.next;
			foreach (int num in next)
			{
				if (num >= 0 && num < segs.Count)
				{
					segs[num].prev.Add(seg.id);
				}
			}
		}
		foreach (FBSeg seg2 in segs)
		{
			Speeds(seg2);
		}
		foreach (object item2 in Json.L(o, "junctions") ?? new List<object>())
		{
			Dictionary<string, object> o3 = item2 as Dictionary<string, object>;
			List<object> list = Json.L(o3, "c");
			if (list != null)
			{
				junctions[Json.I(o3, "id")] = new Vector3(Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]));
			}
		}
		foreach (object item3 in Json.L(o, "spots") ?? new List<object>())
		{
			Dictionary<string, object> o4 = item3 as Dictionary<string, object>;
			List<object> list2 = Json.L(o4, "p");
			FBSpot fBSpot = new FBSpot
			{
				p = new Vector3(Json.Fv(list2[0]), Json.Fv(list2[1]), Json.Fv(list2[2])),
				yaw = Json.F(o4, "yaw"),
				kind = Json.S(o4, "kind"),
				seg = Json.I(o4, "seg"),
				s = Json.F(o4, "s"),
				sin = Json.F(o4, "sin"),
				sout = Json.F(o4, "sout"),
				inp = Pts(Json.L(o4, "inp")),
				outp = Pts(Json.L(o4, "outp")),
				rev = Pts(Json.L(o4, "rev"))
			};
			if (fBSpot.seg >= 0 && fBSpot.seg < segs.Count && fBSpot.inp.Length > 1 && fBSpot.outp.Length > 1)
			{
				spots.Add(fBSpot);
			}
		}
		Debug.Log($"[FreeBala][ruas] {mapId}: {segs.Count} segmentos, {spots.Count} vagas, {junctions.Count} cruzamentos");
		return Ready;
	}

	private static Vector3[] Pts(List<object> l)
	{
		if (l == null)
		{
			return new Vector3[0];
		}
		Vector3[] array = new Vector3[l.Count / 3];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = new Vector3(Json.Fv(l[i * 3]), Json.Fv(l[i * 3 + 1]), Json.Fv(l[i * 3 + 2]));
		}
		return array;
	}

	private static int[] Ints(List<object> l)
	{
		if (l == null)
		{
			return new int[0];
		}
		int[] array = new int[l.Count];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (int)Json.Fv(l[i]);
		}
		return array;
	}

	private static void Measure(FBSeg S)
	{
		S.s = new float[S.p.Length];
		float num = 0f;
		Bounds box = new Bounds((S.p.Length != 0) ? S.p[0] : Vector3.zero, Vector3.zero);
		for (int i = 0; i < S.p.Length; i++)
		{
			if (i > 0)
			{
				num += Vector3.Distance(S.p[i - 1], S.p[i]);
			}
			S.s[i] = num;
			box.Encapsulate(S.p[i]);
		}
		S.len = num;
		box.Expand(new Vector3(4f, 4f, 4f));
		S.box = box;
	}

	private static void Speeds(FBSeg S)
	{
		float num = ((S.kind == 0) ? S.w : 7f);
		S.vmax = ((S.kind != 0) ? 9f : ((S.one && num >= 9f) ? 14f : ((num >= 7f) ? 11.5f : 8.5f)));
		S.vcurv = new float[S.p.Length];
		for (int i = 0; i < S.p.Length; i++)
		{
			int num2 = Mathf.Max(0, i - 2);
			int num3 = Mathf.Min(S.p.Length - 1, i + 2);
			float num4 = 10000f;
			if (num3 - num2 >= 2)
			{
				Vector3 vector = S.p[num2];
				Vector3 vector2 = S.p[(num2 + num3) / 2];
				Vector3 vector3 = S.p[num3];
				float num5 = Vector3.Distance(vector, vector2);
				float num6 = Vector3.Distance(vector2, vector3);
				float num7 = Vector3.Distance(vector3, vector);
				float magnitude = Vector3.Cross(vector2 - vector, vector3 - vector).magnitude;
				if (magnitude > 0.0001f)
				{
					num4 = num5 * num6 * num7 / (2f * magnitude);
				}
			}
			S.vcurv[i] = Mathf.Clamp(Mathf.Sqrt(2.6f * num4), 3.2f, 30f);
		}
	}

	public static bool IsRoad(Vector3 p)
	{
		if (grid == null)
		{
			return false;
		}
		int num = Mathf.FloorToInt((p.x - gx0) / gcell);
		int num2 = Mathf.FloorToInt((p.z - gz0) / gcell);
		if (num < 0 || num2 < 0 || num >= gnx || num2 >= gnz)
		{
			return false;
		}
		int num3 = num2 * gnx + num;
		return (grid[num3 >> 3] & (1 << (num3 & 7))) != 0;
	}

	public static Vector3 At(FBSeg S, float d, out Vector3 tan)
	{
		Vector3[] p = S.p;
		if (p.Length == 1)
		{
			tan = Vector3.forward;
			return p[0];
		}
		if (d <= 0f)
		{
			tan = (p[1] - p[0]).normalized;
			return p[0] + tan * d;
		}
		if (d >= S.len)
		{
			tan = (p[p.Length - 1] - p[p.Length - 2]).normalized;
			return p[p.Length - 1] + tan * (d - S.len);
		}
		int num = 0;
		int num2 = p.Length - 1;
		while (num2 - num > 1)
		{
			int num3 = num + num2 >> 1;
			if (S.s[num3] <= d)
			{
				num = num3;
			}
			else
			{
				num2 = num3;
			}
		}
		float t = (d - S.s[num]) / Mathf.Max(1E-05f, S.s[num2] - S.s[num]);
		tan = (p[num2] - p[num]).normalized;
		return Vector3.LerpUnclamped(p[num], p[num2], t);
	}

	public static float VCurve(FBSeg S, float d)
	{
		if (S.vcurv == null || S.vcurv.Length == 0)
		{
			return S.vmax;
		}
		if (d <= 0f)
		{
			return S.vcurv[0];
		}
		if (d >= S.len)
		{
			float[] vcurv = S.vcurv;
			return vcurv[vcurv.Length - 1];
		}
		int num = 0;
		int num2 = S.p.Length - 1;
		while (num2 - num > 1)
		{
			int num3 = num + num2 >> 1;
			if (S.s[num3] <= d)
			{
				num = num3;
			}
			else
			{
				num2 = num3;
			}
		}
		return Mathf.Min(S.vcurv[num], S.vcurv[num2]);
	}

	public static float Project(FBSeg S, Vector3 q, out float lat, out float dist)
	{
		float num = 3.4028235E+38f;
		float result = 0f;
		float num2 = 0f;
		Vector3[] p = S.p;
		for (int i = 0; i + 1 < p.Length; i++)
		{
			Vector3 vector = p[i];
			Vector3 vector2 = p[i + 1] - vector;
			vector2.y = 0f;
			float sqrMagnitude = vector2.sqrMagnitude;
			if (!(sqrMagnitude < 1E-06f))
			{
				Vector3 lhs = q - vector;
				lhs.y = 0f;
				float num3 = Mathf.Clamp01(Vector3.Dot(lhs, vector2) / sqrMagnitude);
				Vector3 vector3 = vector + vector2 * num3;
				Vector3 vector4 = q - vector3;
				vector4.y = 0f;
				float sqrMagnitude2 = vector4.sqrMagnitude;
				if (sqrMagnitude2 < num)
				{
					num = sqrMagnitude2;
					result = S.s[i] + Mathf.Sqrt(sqrMagnitude) * num3;
					Vector3 normalized = vector2.normalized;
					num2 = vector4.x * normalized.z - vector4.z * normalized.x;
				}
			}
		}
		lat = num2;
		dist = Mathf.Sqrt(num);
		return result;
	}

	public static FBSeg NearestLane(Vector3 p, float maxD, out float s, out float lat, Vector3? dir = null)
	{
		FBSeg result = null;
		float num = maxD;
		s = 0f;
		lat = 0f;
		foreach (FBSeg seg in segs)
		{
			if (seg.kind != 0)
			{
				continue;
			}
			Bounds box = seg.box;
			if (p.x < box.min.x - maxD || p.x > box.max.x + maxD || p.z < box.min.z - maxD || p.z > box.max.z + maxD)
			{
				continue;
			}
			float lat2;
			float dist;
			float num2 = Project(seg, p, out lat2, out dist);
			if (dist >= num)
			{
				continue;
			}
			if (dir.HasValue)
			{
				Vector3 valueOrDefault = dir.GetValueOrDefault();
				At(seg, num2, out var tan);
				if (Vector3.Dot(tan, valueOrDefault.normalized) < 0.5f)
				{
					continue;
				}
			}
			num = dist;
			result = seg;
			s = num2;
			lat = lat2;
		}
		return result;
	}

	public static List<int> Route(int from, int to, int maxNodes = 20000)
	{
		if (from < 0 || to < 0 || from >= segs.Count || to >= segs.Count)
		{
			return null;
		}
		Dictionary<int, float> dictionary = new Dictionary<int, float> { [from] = 0f };
		Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
		SortedSet<(float, int)> sortedSet = new SortedSet<(float, int)> { (0f, from) };
		Vector3 goal = ((segs[to].p.Length != 0) ? segs[to].p[0] : Vector3.zero);
		int num = 0;
		while (sortedSet.Count > 0 && num++ < maxNodes)
		{
			(float, int) min = sortedSet.Min;
			sortedSet.Remove(min);
			int item = min.Item2;
			if (item == to)
			{
				break;
			}
			int[] next = segs[item].next;
			foreach (int num2 in next)
			{
				FBSeg fBSeg = segs[num2];
				float num3 = fBSeg.len / Mathf.Max(3f, fBSeg.vmax) + ((fBSeg.kind != 1) ? 0f : ((fBSeg.turn == 'L') ? 2.2f : ((fBSeg.turn == 'R') ? 0.9f : ((fBSeg.turn == 'U') ? 6f : 0.3f))));
				float num4 = dictionary[item] + num3;
				if (!dictionary.TryGetValue(num2, out var value) || !(value <= num4))
				{
					if (dictionary.ContainsKey(num2))
					{
						sortedSet.Remove((value + H(num2, goal), num2));
					}
					dictionary[num2] = num4;
					dictionary2[num2] = item;
					sortedSet.Add((num4 + H(num2, goal), num2));
				}
			}
		}
		if (!dictionary2.ContainsKey(to) && from != to)
		{
			return null;
		}
		List<int> list = new List<int> { to };
		while (list[list.Count - 1] != from)
		{
			list.Add(dictionary2[list[list.Count - 1]]);
		}
		list.Reverse();
		return list;
	}

	private static float H(int id, Vector3 goal)
	{
		FBSeg fBSeg = segs[id];
		Vector3 vector;
		if (fBSeg.p.Length == 0)
		{
			vector = Vector3.zero;
		}
		else
		{
			Vector3[] p = fBSeg.p;
			vector = p[p.Length - 1];
		}
		Vector3 vector2 = vector;
		return new Vector2(vector2.x - goal.x, vector2.z - goal.z).magnitude / 14f;
	}
}
}
