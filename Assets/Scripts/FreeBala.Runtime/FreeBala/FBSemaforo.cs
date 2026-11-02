using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBSemaforo
{
	private class Ctl
	{
		public int j;

		public readonly List<int>[] grp = new List<int>[2]
		{
			new List<int>(),
			new List<int>()
		};

		public float t;

		public int cur;

		public int phase;
	}

	private class Head
	{
		public Ctl c;

		public int g;

		public GameObject go;

		public readonly List<(MeshRenderer r, int col)> lamps = new List<(MeshRenderer, int)>();
	}

	public const float GREEN = 8f;

	public const float YELLOW = 3f;

	public const float ALLRED = 1.5f;

	private static readonly Dictionary<int, (Ctl c, int g)> byLane = new Dictionary<int, (Ctl, int)>();

	private static readonly List<Ctl> ctls = new List<Ctl>();

	private static readonly List<Head> heads = new List<Head>();

	private static Material mR;

	private static Material mY;

	private static Material mG;

	private static Mesh disc;

	public static int MaxJunctions = 6;

	private static readonly (Vector3 p, int col, float r)[] LENS = new(Vector3, int, float)[6]
	{
		(new Vector3(0.16f, 3.27f, 0.03f), 3, 0.12f),
		(new Vector3(0.16f, 3f, 0.03f), 2, 0.12f),
		(new Vector3(0.16f, 2.73f, 0.03f), 1, 0.12f),
		(new Vector3(0.15f, 5.69f, 5.19f), 3, 0.15f),
		(new Vector3(0.15f, 5.35f, 5.19f), 2, 0.15f),
		(new Vector3(0.15f, 5f, 5.19f), 1, 0.15f)
	};

	private static Transform model;

	private static int flipX = -1;

	private static int flipZ = 1;

	private static bool modelTried;

	public static int Junctions => ctls.Count;

	public static int Heads => heads.Count;

	public static int StateOf(int lane)
	{
		if (!byLane.TryGetValue(lane, out (Ctl, int) value))
		{
			return 0;
		}
		var (ctl, _) = value;
		if (ctl.phase == 2 || ctl.cur != value.Item2)
		{
			return 3;
		}
		if (ctl.phase != 0)
		{
			return 2;
		}
		return 1;
	}

	public static float LeftOf(int lane)
	{
		if (!byLane.TryGetValue(lane, out (Ctl, int) value))
		{
			return 0f;
		}
		var (ctl, _) = value;
		if (ctl.cur == value.Item2 && ctl.phase != 2)
		{
			if (ctl.phase != 0)
			{
				return 3f - ctl.t;
			}
			return 8f - ctl.t;
		}
		return 0f;
	}

	public static void Clear()
	{
		foreach (Head head in heads)
		{
			if ((bool)head.go)
			{
				UnityEngine.Object.Destroy(head.go);
			}
		}
		heads.Clear();
		ctls.Clear();
		byLane.Clear();
	}

	public static void Build()
	{
		Clear();
		if (!FBRoads.Ready)
		{
			return;
		}
		Dictionary<int, HashSet<int>> dictionary = new Dictionary<int, HashSet<int>>();
		foreach (FBSeg seg in FBRoads.segs)
		{
			if (seg.kind != 1 || seg.junction < 0)
			{
				continue;
			}
			foreach (int item in seg.prev)
			{
				if (item >= 0 && item < FBRoads.segs.Count && FBRoads.segs[item].kind == 0)
				{
					HashSet<int> hashSet3;
					if (!dictionary.TryGetValue(seg.junction, out var value))
					{
						HashSet<int> hashSet2 = (dictionary[seg.junction] = new HashSet<int>());
						hashSet3 = hashSet2;
					}
					else
					{
						hashSet3 = value;
					}
					hashSet3.Add(item);
				}
			}
		}
		List<(int j, Vector3 c)> picked = new List<(int, Vector3)>();
		int[] array = new int[2] { 4, 3 };
		foreach (int minIn in array)
		{
			foreach (KeyValuePair<int, HashSet<int>> kv in from k in dictionary
				where k.Value.Count >= minIn
				orderby k.Value.Sum((int id) => FBRoads.segs[id].len) descending
				select k)
			{
				if (picked.Count >= MaxJunctions || picked.Any(((int j, Vector3 c) q) => q.j == kv.Key))
				{
					continue;
				}
				Vector3 ctr = Vector3.zero;
				foreach (int item2 in kv.Value)
				{
					ctr += End(item2);
				}
				ctr /= (float)kv.Value.Count;
				if (!picked.Any(((int j, Vector3 c) q) => (q.c - ctr).Flat().magnitude < 230f))
				{
					picked.Add((kv.Key, ctr));
				}
			}
		}
		foreach (KeyValuePair<int, HashSet<int>> item3 in from k in dictionary
			where picked.Any(((int j, Vector3 c) q) => q.j == k.Key)
			orderby k.Key
			select k)
		{
			List<int> list = item3.Value.ToList();
			if (list.Count < 3)
			{
				continue;
			}
			Vector3 from = H(list[0]);
			Ctl ctl = new Ctl
			{
				j = item3.Key
			};
			foreach (int item4 in list)
			{
				float num = Vector3.Angle(from, H(item4));
				ctl.grp[(!(num < 45f) && !(num > 135f)) ? 1 : 0].Add(item4);
			}
			if (ctl.grp[1].Count == 0)
			{
				continue;
			}
			uint num2 = (uint)(item3.Key * 2654435761u);
			ctl.cur = (int)(num2 & 1);
			ctl.t = (float)((num2 >> 8) % 800) / 100f;
			ctls.Add(ctl);
			for (int j = 0; j < 2; j++)
			{
				foreach (int item5 in ctl.grp[j])
				{
					byLane[item5] = (ctl, j);
				}
			}
		}
		Visuals();
		Debug.Log($"[FreeBala][semáforo] {ctls.Count} cruzamentos semaforizados · {byLane.Count} faixas · {heads.Count} postes (171)");
		static Vector3 End(int id)
		{
			FBSeg fBSeg2 = FBRoads.segs[id];
			Vector3 tan2;
			return FBRoads.At(fBSeg2, fBSeg2.len, out tan2);
		}
		static Vector3 H(int id)
		{
			FBSeg fBSeg = FBRoads.segs[id];
			FBRoads.At(fBSeg, fBSeg.len, out var tan);
			tan.y = 0f;
			return tan.normalized;
		}
	}

	public static void Tick(float dt)
	{
		foreach (Ctl ctl in ctls)
		{
			ctl.t += dt;
			if (ctl.phase == 0 && ctl.t >= 8f)
			{
				ctl.phase = 1;
				ctl.t = 0f;
			}
			else if (ctl.phase == 1 && ctl.t >= 3f)
			{
				ctl.phase = 2;
				ctl.t = 0f;
			}
			else if (ctl.phase == 2 && ctl.t >= 1.5f)
			{
				ctl.phase = 0;
				ctl.t = 0f;
				ctl.cur = 1 - ctl.cur;
			}
		}
		foreach (Head head in heads)
		{
			if (!head.go)
			{
				continue;
			}
			int num = ((head.c.phase == 2 || head.c.cur != head.g) ? 3 : ((head.c.phase == 0) ? 1 : 2));
			foreach (var (meshRenderer, num2) in head.lamps)
			{
				if ((bool)meshRenderer)
				{
					bool flag = num2 == num;
					if (meshRenderer.enabled != flag)
					{
						meshRenderer.enabled = flag;
					}
				}
			}
		}
	}

	private static Transform Model()
	{
		if (modelTried)
		{
			return model;
		}
		modelTried = true;
		GameObject gameObject = Resources.Load<GameObject>("X171/Urbano/Placas");
		if (!gameObject)
		{
			return null;
		}
		foreach (Transform item3 in gameObject.transform)
		{
			if (item3.name.StartsWith("Semaforo"))
			{
				model = item3;
				break;
			}
		}
		if (!model)
		{
			return null;
		}
		MeshFilter componentInChildren = model.GetComponentInChildren<MeshFilter>();
		Vector3[] array = (((bool)componentInChildren && (bool)componentInChildren.sharedMesh && componentInChildren.sharedMesh.isReadable) ? componentInChildren.sharedMesh.vertices : null);
		if (array != null)
		{
			Matrix4x4 matrix4x = model.worldToLocalMatrix * componentInChildren.transform.localToWorldMatrix;
			float num = 1E+09f;
			(int, int)[] array2 = new(int, int)[4]
			{
				(-1, 1),
				(1, -1),
				(1, 1),
				(-1, -1)
			};
			for (int i = 0; i < array2.Length; i++)
			{
				(int, int) tuple = array2[i];
				int item = tuple.Item1;
				int item2 = tuple.Item2;
				float num2 = 0f;
				(Vector3, int, float)[] lENS = LENS;
				for (int j = 0; j < lENS.Length; j++)
				{
					(Vector3, int, float) tuple2 = lENS[j];
					Vector3 vector = new Vector3(tuple2.Item1.x * (float)item, tuple2.Item1.y, tuple2.Item1.z * (float)item2);
					float num3 = 1E+09f;
					Vector3[] array3 = array;
					foreach (Vector3 point in array3)
					{
						num3 = Mathf.Min(num3, (matrix4x.MultiplyPoint3x4(point) - vector).sqrMagnitude);
					}
					num2 += num3;
				}
				if (num2 < num)
				{
					num = num2;
					flipX = item;
					flipZ = item2;
				}
			}
			Debug.Log($"[FreeBala][semáforo] modelo {model.name}: eixos do glTF x{flipX} z{flipZ} (erro {Mathf.Sqrt(num / (float)LENS.Length):0.000} m)");
		}
		return model;
	}

	private static void Mats()
	{
		if ((bool)mR)
		{
			return;
		}
		mR = M(new Color(4.2f, 0.25f, 0.12f));
		mY = M(new Color(3.6f, 2f, 0.15f));
		mG = M(new Color(0.2f, 3.4f, 0.9f));
		disc = new Mesh
		{
			name = "lente_semaforo"
		};
		List<Vector3> list = new List<Vector3> { Vector3.zero };
		List<int> list2 = new List<int>();
		for (int i = 0; i <= 16; i++)
		{
			float f = (float)i / 16f * (float)Math.PI * 2f;
			list.Add(new Vector3(0f, Mathf.Sin(f), Mathf.Cos(f)));
			if (i > 0)
			{
				list2.Add(0);
				list2.Add(i);
				list2.Add(i + 1);
			}
		}
		disc.SetVertices(list);
		disc.SetTriangles(list2, 0);
		disc.RecalculateBounds();
		static Material M(Color c)
		{
			Material material = new Material(FBShaders.Unlit);
			material.SetColor("_BaseColor", c);
			material.color = c;
			material.SetFloat("_Cull", 0f);
			return material;
		}
	}

	private static void Visuals()
	{
		Transform transform = Model();
		if (!transform)
		{
			return;
		}
		Mats();
		int layerMask = 256;
		foreach (KeyValuePair<int, (Ctl, int)> item in byLane)
		{
			FBSeg fBSeg = FBRoads.segs[item.Key];
			Vector3 tan;
			Vector3 vector = FBRoads.At(fBSeg, fBSeg.len, out tan);
			tan.y = 0f;
			tan.Normalize();
			Vector3 vector2 = Vector3.Cross(Vector3.up, tan);
			Vector3? vector3 = null;
			for (float num = fBSeg.w / 2f + 0.9f; num < fBSeg.w / 2f + 4f; num += 0.5f)
			{
				Vector3 vector4 = vector - tan * 1.2f + vector2 * num;
				if (FBRoads.IsRoad(vector4))
				{
					continue;
				}
				float? num2 = FBWorld.GroundY(vector4.x, vector4.y + 3f, vector4.z, 0.3f, 6f);
				if (num2.HasValue)
				{
					float valueOrDefault = num2.GetValueOrDefault();
					vector4.y = valueOrDefault;
					if (!Physics.CheckCapsule(vector4 + Vector3.up * 0.4f, vector4 + Vector3.up * 2.4f, 0.3f, layerMask, QueryTriggerInteraction.Ignore))
					{
						vector3 = vector4;
						break;
					}
				}
			}
			if (!vector3.HasValue)
			{
				continue;
			}
			Vector3 rhs = -tan;
			Vector3 vector5 = -vector2;
			Vector3 vector6 = new Vector3(flipX, 0f, 0f);
			new Vector3(0f, 0f, flipZ);
			Quaternion quaternion = Quaternion.LookRotation(vector5 * flipZ, Vector3.up);
			if (Vector3.Dot(quaternion * vector6, rhs) < 0f)
			{
				quaternion = Quaternion.LookRotation(-vector5 * flipZ, Vector3.up);
				vector3 = vector - tan * 1.2f - vector2 * (fBSeg.w / 2f + 1.2f);
				float? num2 = FBWorld.GroundY(vector3.Value.x, vector3.Value.y + 3f, vector3.Value.z, 0.3f, 6f);
				if (num2.HasValue)
				{
					float valueOrDefault2 = num2.GetValueOrDefault();
					vector3 = new Vector3(vector3.Value.x, valueOrDefault2, vector3.Value.z);
				}
			}
			GameObject gameObject = UnityEngine.Object.Instantiate(transform.gameObject);
			gameObject.name = "semaforo_" + item.Key;
			gameObject.transform.SetPositionAndRotation(vector3.Value, quaternion);
			gameObject.transform.localScale = transform.lossyScale;
			Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].gameObject.layer = 8;
			}
			BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
			boxCollider.center = new Vector3(0f, 1.6f, 0f);
			boxCollider.size = new Vector3(0.22f, 3.2f, 0.22f);
			Head head = new Head
			{
				c = item.Value.Item1,
				g = item.Value.Item2,
				go = gameObject
			};
			(Vector3, int, float)[] lENS = LENS;
			for (int i = 0; i < lENS.Length; i++)
			{
				(Vector3, int, float) tuple = lENS[i];
				GameObject gameObject2 = new GameObject("lente");
				gameObject2.layer = 8;
				gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
				gameObject2.transform.localPosition = new Vector3((tuple.Item1.x + 0.012f) * (float)flipX, tuple.Item1.y, tuple.Item1.z * (float)flipZ);
				gameObject2.transform.localRotation = ((flipX < 0) ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
				gameObject2.transform.localScale = Vector3.one * tuple.Item3;
				gameObject2.AddComponent<MeshFilter>().sharedMesh = disc;
				MeshRenderer meshRenderer = gameObject2.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = ((tuple.Item2 == 3) ? mR : ((tuple.Item2 == 2) ? mY : mG));
				meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				meshRenderer.receiveShadows = false;
				meshRenderer.enabled = false;
				head.lamps.Add((meshRenderer, tuple.Item2));
			}
			heads.Add(head);
		}
	}
}
}
