using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBWindowLights
{
	private struct Win
	{
		public Vector3 c;

		public Vector3 n;

		public float w;

		public float h;

		public bool lit;

		public float cct;
	}

	private struct Room
	{
		public bool tested;

		public bool ok;

		public Vector3 lp;

		public float depth;
	}

	private class Slot
	{
		public Light spot;

		public Light fill;

		public int win = -1;

		public float wgt;

		public float target;
	}

	private static readonly List<Win> wins = new List<Win>();

	private static readonly List<Slot> slots = new List<Slot>();

	private static GameObject root;

	private static Material mat;

	private static float scanT;

	private static readonly List<int> litIdx = new List<int>();

	private static Room[] rooms = new Room[0];

	private static int maxShadow;

	public static int RealLights { get; private set; }

	public static void Build()
	{
		wins.Clear();
		litIdx.Clear();
		slots.Clear();
		root = null;
		RealLights = 0;
		FBWindowData[] array = UnityEngine.Object.FindObjectsByType<FBWindowData>(FindObjectsSortMode.None);
		foreach (FBWindowData fBWindowData in array)
		{
			for (int j = 0; j < fBWindowData.center.Length; j++)
			{
				Vector3 vector = fBWindowData.center[j];
				uint num = Hash(vector);
				wins.Add(new Win
				{
					c = vector,
					n = fBWindowData.normal[j],
					w = fBWindowData.size[j].x,
					h = fBWindowData.size[j].y,
					lit = (num % 100 < 70),
					cct = Tone(num)
				});
			}
		}
		FBGlass[] array2;
		if (wins.Count == 0)
		{
			array2 = UnityEngine.Object.FindObjectsByType<FBGlass>(FindObjectsSortMode.None);
			foreach (FBGlass fBGlass in array2)
			{
				MeshFilter component = fBGlass.GetComponent<MeshFilter>();
				Mesh mesh = (component ? component.sharedMesh : null);
				if (!mesh || !mesh.isReadable)
				{
					continue;
				}
				Vector3[] vertices = mesh.vertices;
				int[] triangles = mesh.triangles;
				Matrix4x4 localToWorldMatrix = component.transform.localToWorldMatrix;
				Vector3 center = fBGlass.GetComponent<Renderer>().bounds.center;
				Dictionary<Vector3Int, int> dictionary = new Dictionary<Vector3Int, int>();
				int[] array3 = new int[vertices.Length];
				Vector3[] array4 = new Vector3[vertices.Length];
				for (int k = 0; k < vertices.Length; k++)
				{
					array4[k] = localToWorldMatrix.MultiplyPoint3x4(vertices[k]);
					Vector3Int key = Vector3Int.RoundToInt(array4[k] * 200f);
					if (!dictionary.TryGetValue(key, out var value))
					{
						value = (dictionary[key] = dictionary.Count);
					}
					array3[k] = value;
				}
				int[] par = Enumerable.Range(0, dictionary.Count).ToArray();
				for (int l = 0; l < triangles.Length; l += 3)
				{
					int num2 = F(array3[triangles[l]]);
					int num3 = F(array3[triangles[l + 1]]);
					int x2 = F(array3[triangles[l + 2]]);
					par[num3] = num2;
					par[F(x2)] = num2;
				}
				Dictionary<int, List<Vector3>> dictionary2 = new Dictionary<int, List<Vector3>>();
				for (int m = 0; m < vertices.Length; m++)
				{
					int key2 = F(array3[m]);
					if (!dictionary2.TryGetValue(key2, out var value2))
					{
						value2 = (dictionary2[key2] = new List<Vector3>());
					}
					value2.Add(array4[m]);
				}
				foreach (List<Vector3> value3 in dictionary2.Values)
				{
					Bounds bounds = new Bounds(value3[0], Vector3.zero);
					foreach (Vector3 item in value3)
					{
						bounds.Encapsulate(item);
					}
					float x3 = bounds.size.x;
					float z = bounds.size.z;
					float y = bounds.size.y;
					if (y < 0.35f || y > 3.5f)
					{
						continue;
					}
					Vector3 vector2 = ((x3 < z) ? Vector3.right : Vector3.forward);
					float num4 = Mathf.Max(x3, z);
					if (!(num4 < 0.3f) && !(num4 > 4f))
					{
						if (Vector3.Dot(bounds.center - center, vector2) < 0f)
						{
							vector2 = -vector2;
						}
						uint num5 = Hash(bounds.center);
						wins.Add(new Win
						{
							c = bounds.center,
							n = vector2,
							w = num4,
							h = y,
							lit = (num5 % 100 < 70),
							cct = Tone(num5)
						});
					}
				}
				int F(int x)
				{
					while (par[x] != x)
					{
						x = (par[x] = par[par[x]]);
					}
					return x;
				}
			}
		}
		rooms = new Room[wins.Count];
		if (wins.Count == 0)
		{
			return;
		}
		array2 = UnityEngine.Object.FindObjectsByType<FBGlass>(FindObjectsSortMode.None);
		for (int i = 0; i < array2.Length; i++)
		{
			Renderer[] components = array2[i].GetComponents<Renderer>();
			for (int n = 0; n < components.Length; n++)
			{
				components[n].shadowCastingMode = ShadowCastingMode.Off;
			}
		}
		List<Vector3> list2 = new List<Vector3>();
		List<Vector2> list3 = new List<Vector2>();
		List<Color> list4 = new List<Color>();
		List<int> list5 = new List<int>();
		System.Random random = new System.Random(5);
		for (int num6 = 0; num6 < wins.Count; num6++)
		{
			Win win = wins[num6];
			if (win.lit)
			{
				litIdx.Add(num6);
				Vector3 normalized = Vector3.Cross(Vector3.up, win.n).normalized;
				Vector3 vector3 = win.c - win.n * 0.12f;
				float num7 = win.w * 0.5f * 0.96f;
				float num8 = win.h * 0.5f * 0.96f;
				int count2 = list2.Count;
				list2.Add(vector3 - normalized * num7 - Vector3.up * num8);
				list2.Add(vector3 + normalized * num7 - Vector3.up * num8);
				list2.Add(vector3 - normalized * num7 + Vector3.up * num8);
				list2.Add(vector3 + normalized * num7 + Vector3.up * num8);
				float num9 = (float)random.NextDouble() * 0.5f;
				list3.Add(new Vector2(num9, 0f));
				list3.Add(new Vector2(num9 + 0.5f, 0f));
				list3.Add(new Vector2(num9, 1f));
				list3.Add(new Vector2(num9 + 0.5f, 1f));
				float num10 = 0.55f + (float)random.NextDouble() * 0.7f;
				Color color = ((win.cct > 5000f) ? new Color(0.75f, 0.85f, 1f) : ((win.cct > 3200f) ? new Color(1f, 0.9f, 0.74f) : Color.Lerp(new Color(1f, 0.72f, 0.42f), new Color(1f, 0.86f, 0.66f), (float)random.NextDouble())));
				for (int num11 = 0; num11 < 4; num11++)
				{
					list4.Add(color * num10);
				}
				list5.AddRange(new int[6]
				{
					count2,
					count2 + 2,
					count2 + 1,
					count2 + 1,
					count2 + 2,
					count2 + 3
				});
			}
		}
		Mesh mesh2 = new Mesh
		{
			name = "janelas_acesas",
			indexFormat = IndexFormat.UInt32
		};
		mesh2.SetVertices(list2);
		mesh2.SetUVs(0, list3);
		mesh2.SetColors(list4);
		mesh2.SetTriangles(list5, 0);
		mesh2.RecalculateBounds();
		root = new GameObject("FB_JanelasAcesas");
		root.AddComponent<MeshFilter>().sharedMesh = mesh2;
		MeshRenderer meshRenderer = root.AddComponent<MeshRenderer>();
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		meshRenderer.sharedMaterial = mat ?? (mat = MakeMat());
		root.SetActive(value: false);
		bool mobile = FBQuality.Mobile;
		FBQLevel level = FBQuality.Level;
		int num12 = ((!mobile) ? (level switch
		{
			FBQLevel.Medio => 5, 
			FBQLevel.Alto => 8, 
			FBQLevel.Ultra => 12, 
			_ => 2, 
		}) : 0);
		maxShadow = ((!mobile) ? (level switch
		{
			FBQLevel.Medio => 2, 
			FBQLevel.Alto => 4, 
			FBQLevel.Ultra => 6, 
			_ => 0, 
		}) : 0);
		for (int num13 = 0; num13 < num12; num13++)
		{
			Slot slot = new Slot();
			slot.spot = new GameObject("LampadaJanela" + num13).AddComponent<Light>();
			slot.spot.type = LightType.Spot;
			slot.spot.range = 16f;
			slot.spot.useColorTemperature = true;
			slot.spot.color = Color.white;
			slot.spot.shadows = LightShadows.None;
			slot.spot.shadowStrength = 0.95f;
			slot.spot.shadowBias = 0.05f;
			slot.spot.shadowNormalBias = 0.4f;
			slot.spot.shadowNearPlane = 0.15f;
			FBLightUtil.MediumShadows(slot.spot);
			slot.spot.enabled = false;
			slot.fill = new GameObject("LampadaComodo" + num13).AddComponent<Light>();
			slot.fill.type = LightType.Point;
			slot.fill.useColorTemperature = true;
			slot.fill.color = Color.white;
			slot.fill.shadows = LightShadows.None;
			slot.fill.enabled = false;
			slots.Add(slot);
			FBVolumetric.Sources.Add(slot.spot);
		}
		Debug.Log($"[FreeBala] janelas: {wins.Count} · acesas {litIdx.Count} · lâmpadas de cômodo {num12} ({maxShadow} com sombra)");
	}

	private static uint Hash(Vector3 c0)
	{
		return (uint)((Mathf.RoundToInt(c0.x * 7f) * 73856093) ^ (Mathf.RoundToInt(c0.y * 7f) * 19349663) ^ (Mathf.RoundToInt(c0.z * 7f) * 83492791));
	}

	private static float Tone(uint hs)
	{
		int num;
		switch (hs / 100 % 100)
		{
		default:
			num = 2700;
			break;
		case 12u:
		case 13u:
		case 14u:
		case 15u:
		case 16u:
		case 17u:
		case 18u:
		case 19u:
		case 20u:
		case 21u:
		case 22u:
		case 23u:
		case 24u:
		case 25u:
		case 26u:
		case 27u:
		case 28u:
		case 29u:
		case 30u:
		case 31u:
		case 32u:
		case 33u:
		case 34u:
		case 35u:
		case 36u:
		case 37u:
		case 38u:
		case 39u:
			num = 3600;
			break;
		case 0u:
		case 1u:
		case 2u:
		case 3u:
		case 4u:
		case 5u:
		case 6u:
		case 7u:
		case 8u:
		case 9u:
		case 10u:
		case 11u:
			num = 6200;
			break;
		}
		return num;
	}

	private static Material MakeMat()
	{
		Texture2D texture2D = new Texture2D(128, 64, TextureFormat.RGBA32, mipChain: true)
		{
			wrapMode = TextureWrapMode.Repeat
		};
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 128; j++)
			{
				float num = (float)j / 128f;
				float num2 = (float)i / 63f;
				float num3 = 0.78f + 0.22f * Mathf.Sin(num * (float)Math.PI * 18f) * Mathf.Sin(num * (float)Math.PI * 3.3f + 0.7f);
				float num4 = Mathf.SmoothStep(0f, 0.18f, Mathf.Min(num % 0.5f, 0.5f - num % 0.5f) * 2f) * Mathf.SmoothStep(0f, 0.15f, Mathf.Min(num2, 1f - num2));
				float num5 = Mathf.Lerp(0.7f, 1.05f, num2);
				float num6 = num3 * (0.35f + 0.65f * num4) * num5;
				texture2D.SetPixel(j, i, new Color(num6, num6, num6, 1f));
			}
		}
		texture2D.Apply();
		Material material = new Material(FBShaders.ParticlesUnlit);
		material.name = "janela_interior";
		material.SetTexture("_BaseMap", texture2D);
		material.mainTexture = texture2D;
		material.SetColor("_BaseColor", Color.black);
		material.SetFloat("_Cull", 0f);
		Material material2 = FBShaders.Transparent(material, additive: true);
		material2.renderQueue = 3010;
		return material2;
	}

	private static Room RoomOf(int i)
	{
		if (rooms[i].tested)
		{
			return rooms[i];
		}
		Win win = wins[i];
		Room room = default(Room);
		room.tested = true;
		Room room2 = room;
		int layerMask = 256;
		Vector3 n = win.n;
		n.y = 0f;
		if (n.sqrMagnitude < 0.1f)
		{
			rooms[i] = room2;
			return room2;
		}
		n.Normalize();
		Vector3 vector = win.c - n * 0.25f;
		RaycastHit hitInfo;
		float num = (Physics.Raycast(vector, -n, out hitInfo, 7f, layerMask, QueryTriggerInteraction.Ignore) ? hitInfo.distance : 7f);
		if (num >= 0.9f && !Physics.CheckSphere(vector, 0.12f, layerMask, QueryTriggerInteraction.Ignore))
		{
			Vector3 vector2 = vector - n * Mathf.Clamp(num * 0.5f, 0.5f, 2.4f);
			RaycastHit hitInfo2;
			float value = (Physics.Raycast(vector2, Vector3.up, out hitInfo2, 2.6f, layerMask, QueryTriggerInteraction.Ignore) ? (hitInfo2.distance - 0.28f) : (win.h * 0.5f + 0.35f));
			vector2 += Vector3.up * Mathf.Clamp(value, 0.25f, 1.9f);
			room2.ok = true;
			room2.lp = vector2;
			room2.depth = num;
		}
		rooms[i] = room2;
		return room2;
	}

	public static void Tick(Camera cam, float night, float dt)
	{
		if (!root || !cam)
		{
			return;
		}
		float num = Mathf.InverseLerp(0.35f, 0.9f, night);
		root.SetActive(num > 0.01f);
		if ((bool)mat)
		{
			mat.SetColor("_BaseColor", new Color(1f, 0.74f, 0.44f) * (2.6f * num));
		}
		RealLights = 0;
		if (slots.Count == 0)
		{
			return;
		}
		Vector3 cp = cam.transform.position;
		Vector3 forward = cam.transform.forward;
		if (num <= 0.01f)
		{
			foreach (Slot slot3 in slots)
			{
				slot3.target = 0f;
				slot3.wgt = 0f;
				slot3.win = -1;
				Light spot = slot3.spot;
				bool enabled = (slot3.fill.enabled = false);
				spot.enabled = enabled;
			}
			return;
		}
		if ((scanT -= dt) <= 0f)
		{
			scanT = 0.35f;
			HashSet<int> hashSet = new HashSet<int>(from s in slots
				where s.win >= 0 && s.target > 0f
				select s.win);
			List<(float, int)> list = new List<(float, int)>();
			foreach (int item3 in litIdx)
			{
				Vector3 vector = wins[item3].c - cp;
				float sqrMagnitude = vector.sqrMagnitude;
				if (!(sqrMagnitude > 2025f))
				{
					float num2 = Mathf.Sqrt(sqrMagnitude);
					if (!(Vector3.Dot(wins[item3].n, -vector) < -0.05f * num2) || !(num2 > 6f))
					{
						float item = num2 * ((Vector3.Dot(vector, forward) < 0f) ? 1.5f : 1f) * (hashSet.Contains(item3) ? 0.78f : 1f);
						list.Add((item, item3));
					}
				}
			}
			list.Sort(((float sc, int i) a, (float sc, int i) b) => a.sc.CompareTo(b.sc));
			List<int> list2 = new List<int>();
			foreach (var item4 in list)
			{
				int item2 = item4.Item2;
				if (list2.Count >= slots.Count)
				{
					break;
				}
				if (RoomOf(item2).ok)
				{
					list2.Add(item2);
				}
			}
			HashSet<int> hashSet2 = new HashSet<int>(list2);
			foreach (Slot slot4 in slots)
			{
				if (slot4.win >= 0 && !hashSet2.Contains(slot4.win))
				{
					slot4.target = 0f;
				}
			}
			foreach (int i in list2)
			{
				Slot slot = slots.FirstOrDefault((Slot s) => s.win == i);
				if (slot != null)
				{
					slot.target = 1f;
					continue;
				}
				Slot slot2 = slots.FirstOrDefault((Slot s) => s.win < 0) ?? (from s in slots
					where s.target == 0f
					orderby s.wgt
					select s).FirstOrDefault();
				if (slot2 == null)
				{
					break;
				}
				Assign(slot2, i);
			}
		}
		HashSet<Slot> hashSet3 = (from s in slots
			where s.win >= 0 && s.wgt > 0.9f
			orderby (wins[s.win].c - cp).sqrMagnitude
			select s).Take(maxShadow).ToHashSet();
		foreach (Slot slot5 in slots)
		{
			if (slot5.win < 0)
			{
				continue;
			}
			slot5.wgt = Mathf.MoveTowards(slot5.wgt, slot5.target, dt * 2f);
			bool enabled;
			if (slot5.wgt <= 0f && slot5.target <= 0f)
			{
				slot5.win = -1;
				Light spot2 = slot5.spot;
				enabled = (slot5.fill.enabled = false);
				spot2.enabled = enabled;
				continue;
			}
			Win win = wins[slot5.win];
			float f = Mathf.Clamp(win.w * win.h, 0.6f, 3f);
			slot5.spot.intensity = 9f * num * slot5.wgt * Mathf.Sqrt(f);
			slot5.fill.intensity = 2.4f * num * slot5.wgt;
			Light spot3 = slot5.spot;
			enabled = (slot5.fill.enabled = true);
			spot3.enabled = enabled;
			RealLights++;
			LightShadows lightShadows = (hashSet3.Contains(slot5) ? LightShadows.Soft : LightShadows.None);
			if (slot5.spot.shadows != lightShadows)
			{
				slot5.spot.shadows = lightShadows;
			}
		}
	}

	private static void Assign(Slot s, int i)
	{
		Win win = wins[i];
		Room room = rooms[i];
		s.win = i;
		s.target = 1f;
		s.wgt = 0f;
		Vector3 n = win.n;
		n.y = 0f;
		n.Normalize();
		Vector3 normalized = (win.c + n * 3f + Vector3.down * Mathf.Clamp(win.c.y - room.lp.y + 2.2f, 1.2f, 3.5f) - room.lp).normalized;
		float b = Vector3.Distance(room.lp, win.c);
		float num = 2f * Mathf.Atan(Mathf.Max(win.w, win.h) * 0.5f / Mathf.Max(0.3f, b)) * 57.29578f;
		s.spot.transform.SetPositionAndRotation(room.lp, Quaternion.LookRotation(normalized));
		s.spot.spotAngle = Mathf.Clamp(num * 1.5f + 25f, 70f, 135f);
		s.spot.innerSpotAngle = s.spot.spotAngle * 0.5f;
		s.spot.colorTemperature = win.cct;
		s.fill.colorTemperature = win.cct;
		s.fill.transform.position = room.lp - n * 0.2f;
		s.fill.range = Mathf.Clamp(room.depth * 0.85f, 2.2f, 4.2f);
	}
}
}
