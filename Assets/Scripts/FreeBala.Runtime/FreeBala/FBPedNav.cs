using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
public static class FBPedNav
{
	private const float ROAD_COST = 6f;

	private const float CELL = 4f;

	private static Vector3[] V;

	private static int[] I;

	private static Vector3[] C;

	private static float[] W;

	private static int[][] nb;

	private static int[][] nbEdge;

	private static Dictionary<long, List<int>> grid;

	private static float gx0;

	private static float gz0;

	private static string built;

	private static readonly Dictionary<int, float> g = new Dictionary<int, float>();

	private static readonly Dictionary<int, int> from = new Dictionary<int, int>();

	public static bool Ready
	{
		get
		{
			if (C != null)
			{
				return C.Length != 0;
			}
			return false;
		}
	}

	public static void Build(string key)
	{
		if (built == key && Ready)
		{
			return;
		}
		built = key;
		NavMeshTriangulation navMeshTriangulation = NavMesh.CalculateTriangulation();
		V = navMeshTriangulation.vertices;
		I = navMeshTriangulation.indices;
		int num = I.Length / 3;
		C = new Vector3[num];
		W = new float[num];
		nb = new int[num][];
		grid = new Dictionary<long, List<int>>();
		if (num == 0)
		{
			return;
		}
		gx0 = 3.4028235E+38f;
		gz0 = 3.4028235E+38f;
		Vector3[] v = V;
		for (int i = 0; i < v.Length; i++)
		{
			Vector3 vector = v[i];
			gx0 = Mathf.Min(gx0, vector.x);
			gz0 = Mathf.Min(gz0, vector.z);
		}
		Dictionary<(long, long), (int, int)> dictionary = new Dictionary<(long, long), (int, int)>();
		for (int j = 0; j < num; j++)
		{
			Vector3 vector2 = V[I[j * 3]];
			Vector3 vector3 = V[I[j * 3 + 1]];
			Vector3 vector4 = V[I[j * 3 + 2]];
			C[j] = (vector2 + vector3 + vector4) / 3f;
			nb[j] = new int[3] { -1, -1, -1 };
			W[j] = ((FBRoads.Ready && FBRoads.IsRoad(C[j])) ? 6f : 1f);
			for (int k = 0; k < 3; k++)
			{
				Vector3 p2 = V[I[j * 3 + k]];
				Vector3 p3 = V[I[j * 3 + (k + 1) % 3]];
				long num2 = K(p2);
				long num3 = K(p3);
				(long, long) key2 = ((num2 < num3) ? (num2, num3) : (num3, num2));
				if (dictionary.TryGetValue(key2, out var value))
				{
					nb[j][k] = value.Item1;
					nb[value.Item1][value.Item2] = j;
				}
				else
				{
					dictionary[key2] = (j, k);
				}
			}
			float num4 = Mathf.Min(vector2.x, Mathf.Min(vector3.x, vector4.x));
			float num5 = Mathf.Max(vector2.x, Mathf.Max(vector3.x, vector4.x));
			float num6 = Mathf.Min(vector2.z, Mathf.Min(vector3.z, vector4.z));
			float num7 = Mathf.Max(vector2.z, Mathf.Max(vector3.z, vector4.z));
			for (int l = Mathf.FloorToInt((num4 - gx0) / 4f); l <= Mathf.FloorToInt((num5 - gx0) / 4f); l++)
			{
				for (int m = Mathf.FloorToInt((num6 - gz0) / 4f); m <= Mathf.FloorToInt((num7 - gz0) / 4f); m++)
				{
					long key3 = ((long)l << 32) ^ (uint)m;
					if (!grid.TryGetValue(key3, out var value2))
					{
						value2 = (grid[key3] = new List<int>());
					}
					value2.Add(j);
				}
			}
		}
		static long K(Vector3 p)
		{
			return ((long)Mathf.RoundToInt(p.x * 50f) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 20f) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 50f) * 83492791L);
		}
	}

	private static float Cross(Vector3 a, Vector3 b)
	{
		return a.x * b.z - a.z * b.x;
	}

	private static bool InTri(int t, Vector3 p)
	{
		Vector3 vector = V[I[t * 3]];
		Vector3 vector2 = V[I[t * 3 + 1]];
		Vector3 vector3 = V[I[t * 3 + 2]];
		float num = Cross(vector2 - vector, p - vector);
		float num2 = Cross(vector3 - vector2, p - vector2);
		float num3 = Cross(vector - vector3, p - vector3);
		bool flag = num < 0f || num2 < 0f || num3 < 0f;
		bool flag2 = num > 0f || num2 > 0f || num3 > 0f;
		return !(flag && flag2);
	}

	public static int Locate(Vector3 p)
	{
		if (!Ready)
		{
			return -1;
		}
		int num = Mathf.FloorToInt((p.x - gx0) / 4f);
		int num2 = Mathf.FloorToInt((p.z - gz0) / 4f);
		int num3 = -1;
		float num4 = 3.4028235E+38f;
		for (int i = 0; i <= 2; i++)
		{
			if (num3 >= 0)
			{
				break;
			}
			for (int j = -i; j <= i; j++)
			{
				for (int k = -i; k <= i; k++)
				{
					if (!grid.TryGetValue(((long)(num + j) << 32) ^ (uint)(num2 + k), out var value))
					{
						continue;
					}
					foreach (int item in value)
					{
						float num5 = Mathf.Abs(C[item].y - p.y);
						if (num5 > 2.5f)
						{
							continue;
						}
						if (InTri(item, p))
						{
							if (num5 < num4)
							{
								num4 = num5;
								num3 = item;
							}
						}
						else if (num3 < 0)
						{
							float num6 = (C[item] - p).sqrMagnitude + 10000f;
							if (num6 < num4)
							{
								num4 = num6;
								num3 = item;
							}
						}
					}
				}
			}
		}
		return num3;
	}

	public static List<Vector3> Path(Vector3 a, Vector3 b, int maxNodes = 9000)
	{
		int num = Locate(a);
		int num2 = Locate(b);
		if (num < 0 || num2 < 0)
		{
			return null;
		}
		if (num == num2)
		{
			return new List<Vector3> { b };
		}
		g.Clear();
		from.Clear();
		SortedSet<(float, int)> sortedSet = new SortedSet<(float, int)>();
		g[num] = 0f;
		sortedSet.Add((H(num, num2), num));
		int num3 = 0;
		while (sortedSet.Count > 0 && num3++ < maxNodes)
		{
			(float, int) min = sortedSet.Min;
			sortedSet.Remove(min);
			int item = min.Item2;
			if (item == num2)
			{
				break;
			}
			for (int i = 0; i < 3; i++)
			{
				int num4 = nb[item][i];
				if (num4 < 0)
				{
					continue;
				}
				float num5 = g[item] + Vector3.Distance(C[item], C[num4]) * (W[item] + W[num4]) * 0.5f;
				if (g.TryGetValue(num4, out var value))
				{
					if (value <= num5)
					{
						continue;
					}
					sortedSet.Remove((value + H(num4, num2), num4));
				}
				g[num4] = num5;
				from[num4] = item;
				sortedSet.Add((num5 + H(num4, num2), num4));
			}
		}
		if (!from.ContainsKey(num2))
		{
			return null;
		}
		List<int> list = new List<int> { num2 };
		while (list[list.Count - 1] != num)
		{
			list.Add(from[list[list.Count - 1]]);
		}
		list.Reverse();
		return Funnel(a, b, list);
	}

	private static float H(int a, int b)
	{
		return Vector3.Distance(C[a], C[b]);
	}

	private static List<Vector3> Funnel(Vector3 start, Vector3 end, List<int> tris)
	{
		List<Vector3> list = new List<Vector3> { start };
		List<Vector3> list2 = new List<Vector3> { start };
		for (int i = 0; i + 1 < tris.Count; i++)
		{
			int num = tris[i];
			int num2 = tris[i + 1];
			int num3 = Array.IndexOf(nb[num], num2);
			if (num3 >= 0)
			{
				Vector3 vector = V[I[num * 3 + num3]];
				Vector3 item = V[I[num * 3 + (num3 + 1) % 3]];
				if (Cross(C[num2] - C[num], vector - C[num]) > 0f)
				{
					list.Add(vector);
					list2.Add(item);
				}
				else
				{
					list.Add(item);
					list2.Add(vector);
				}
			}
		}
		list.Add(end);
		list2.Add(end);
		List<Vector3> list3 = new List<Vector3>();
		Vector3 vector2 = start;
		Vector3 vector3 = list[0];
		Vector3 vector4 = list2[0];
		int num4 = 0;
		int num5 = 0;
		for (int j = 1; j < list.Count; j++)
		{
			Vector3 vector5 = list[j];
			Vector3 vector6 = list2[j];
			if (Cross(vector4 - vector2, vector6 - vector2) >= 0f)
			{
				if (!((vector2 - vector4).sqrMagnitude < 1E-08f) && !(Cross(vector3 - vector2, vector6 - vector2) < 0f))
				{
					list3.Add(vector3);
					vector2 = vector3;
					int num6 = num4;
					vector3 = (vector4 = vector2);
					num4 = (num5 = num6);
					j = num6;
					continue;
				}
				vector4 = vector6;
				num5 = j;
			}
			if (Cross(vector3 - vector2, vector5 - vector2) <= 0f)
			{
				if ((vector2 - vector3).sqrMagnitude < 1E-08f || Cross(vector4 - vector2, vector5 - vector2) > 0f)
				{
					vector3 = vector5;
					num4 = j;
					continue;
				}
				list3.Add(vector4);
				vector2 = vector4;
				int num7 = num5;
				vector3 = (vector4 = vector2);
				num4 = (num5 = num7);
				j = num7;
			}
		}
		if (list3.Count != 0)
		{
			if (!((list3[list3.Count - 1] - end).sqrMagnitude > 0.0001f))
			{
				goto IL_0276;
			}
		}
		list3.Add(end);
		goto IL_0276;
		IL_0276:
		return list3;
	}
}
}
