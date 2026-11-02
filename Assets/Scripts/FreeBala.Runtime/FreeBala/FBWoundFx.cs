using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public static class FBWoundFx
{
	public class Wound
	{
		public GameObject go;

		public Transform tr;

		public MeshRenderer mr;

		public FBCharacter owner;

		public Transform bone;

		public int group;

		public int seq;

		public bool on;

		public bool streak;

		public Renderer skin;
	}

	private class MeshSamples
	{
		public int[] bone;

		public Vector3[] lp;

		public Vector3[] ln;
	}

	private class CharInfo
	{
		public FBCharacter ch;

		public SkinnedMeshRenderer[] smr;

		public MeshSamples[] ms;

		public Dictionary<Transform, int> group;

		public bool wasAlive = true;

		public Vector3 lastPos;

		public readonly List<Wound> wounds = new List<Wound>();

		public float dripT;
	}

	public const int GHead = 1;

	public const int GBody = 2;

	public const int GArmL = 4;

	public const int GArmR = 8;

	public const int GLegL = 16;

	public const int GLegR = 32;

	private static readonly Dictionary<Mesh, MeshSamples> meshCache = new Dictionary<Mesh, MeshSamples>();

	private static readonly Dictionary<FBCharacter, CharInfo> chars = new Dictionary<FBCharacter, CharInfo>();

	private static Wound[] pool;

	private static Material mat;

	private static Material matStreak;

	private static Mesh quad;

	private static MaterialPropertyBlock mpb;

	private static int seq;

	private static bool ready;

	private static readonly int idST = Shader.PropertyToID("_BaseMap_ST");

	private static readonly int idBaseColor = Shader.PropertyToID("_BaseColor");

	private static readonly List<FBCharacter> dead = new List<FBCharacter>();

	public static int Capacity
	{
		get
		{
			Wound[] array = pool;
			if (array == null)
			{
				return 0;
			}
			return array.Length;
		}
	}

	public static int ActiveCount
	{
		get
		{
			int num = 0;
			if (pool != null)
			{
				Wound[] array = pool;
				for (int i = 0; i < array.Length; i++)
				{
					if (array[i].on)
					{
						num++;
					}
				}
			}
			return num;
		}
	}

	public static List<Wound> WoundsOf(FBCharacter ch)
	{
		if (!ch || !chars.TryGetValue(ch, out var value))
		{
			return new List<Wound>();
		}
		return value.wounds;
	}

	public static void Warm()
	{
		if (!ready)
		{
			ready = true;
			FBFxQuality.Ensure();
			quad = FBFxLib.Quad("FerimentoQuad");
			mpb = new MaterialPropertyBlock();
			mat = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("wounds"), Color.white, "Ferimento");
			if (mat.HasProperty("_Smoothness"))
			{
				mat.SetFloat("_Smoothness", 0.55f);
			}
			matStreak = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("blood_splats"), new Color(1f, 1f, 1f, 0.85f), "FerimentoEscorrido");
			if (matStreak.HasProperty("_Smoothness"))
			{
				matStreak.SetFloat("_Smoothness", 0.5f);
			}
			pool = new Wound[FBFxQuality.PoolWounds];
			for (int i = 0; i < pool.Length; i++)
			{
				pool[i] = Make(i);
			}
		}
	}

	private static Wound Make(int i)
	{
		Wound wound = new Wound();
		GameObject gameObject = new GameObject("Ferimento" + i);
		gameObject.transform.SetParent(FBFxLib.Root, worldPositionStays: false);
		wound.go = gameObject;
		wound.tr = gameObject.transform;
		gameObject.AddComponent<MeshFilter>().sharedMesh = quad;
		wound.mr = gameObject.AddComponent<MeshRenderer>();
		wound.mr.sharedMaterial = mat;
		wound.mr.shadowCastingMode = ShadowCastingMode.Off;
		wound.mr.lightProbeUsage = LightProbeUsage.BlendProbes;
		wound.mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
		wound.mr.enabled = false;
		gameObject.SetActive(value: false);
		return wound;
	}

	private static int GroupOf(HumanBodyBones b)
	{
		switch (b)
		{
		case HumanBodyBones.Head:
		case HumanBodyBones.LeftEye:
		case HumanBodyBones.RightEye:
		case HumanBodyBones.Jaw:
			return 1;
		case HumanBodyBones.Neck:
			return 3;
		case HumanBodyBones.Hips:
		case HumanBodyBones.Spine:
		case HumanBodyBones.Chest:
		case HumanBodyBones.UpperChest:
			return 2;
		case HumanBodyBones.LeftShoulder:
			return 6;
		case HumanBodyBones.RightShoulder:
			return 10;
		case HumanBodyBones.LeftUpperLeg:
		case HumanBodyBones.LeftLowerLeg:
		case HumanBodyBones.LeftFoot:
		case HumanBodyBones.LeftToes:
			return 16;
		case HumanBodyBones.RightUpperLeg:
		case HumanBodyBones.RightLowerLeg:
		case HumanBodyBones.RightFoot:
		case HumanBodyBones.RightToes:
			return 32;
		default:
		{
			string text = b.ToString();
			if (text.StartsWith("Left"))
			{
				if (!text.Contains("Leg") && !text.Contains("Foot") && !text.Contains("Toe"))
				{
					return 4;
				}
				return 16;
			}
			if (text.StartsWith("Right"))
			{
				if (!text.Contains("Leg") && !text.Contains("Foot") && !text.Contains("Toe"))
				{
					return 8;
				}
				return 32;
			}
			return 2;
		}
		}
	}

	public static string GroupName(int g)
	{
		if ((g & 1) == 0)
		{
			if ((g & 0xC) == 0)
			{
				if ((g & 0x30) == 0)
				{
					return "body";
				}
				return "leg";
			}
			return "arm";
		}
		return "head";
	}

	private static CharInfo Info(FBCharacter ch)
	{
		SkinnedMeshRenderer[] componentsInChildren = ch.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false);
		if (chars.TryGetValue(ch, out var value) && Same(value.smr, componentsInChildren))
		{
			return value;
		}
		if (value == null)
		{
			value = new CharInfo
			{
				ch = ch,
				lastPos = ch.transform.position,
				wasAlive = ch.alive
			};
			chars[ch] = value;
		}
		value.smr = componentsInChildren;
		value.ms = new MeshSamples[componentsInChildren.Length];
		value.group = new Dictionary<Transform, int>();
		Animator component = ch.GetComponent<Animator>();
		if ((bool)component && component.isHuman)
		{
			for (int i = 0; i < 55; i++)
			{
				Transform boneTransform = component.GetBoneTransform((HumanBodyBones)i);
				if ((bool)boneTransform)
				{
					value.group[boneTransform] = GroupOf((HumanBodyBones)i);
				}
			}
		}
		for (int j = 0; j < componentsInChildren.Length; j++)
		{
			value.ms[j] = Samples(componentsInChildren[j].sharedMesh);
		}
		return value;
	}

	private static bool Same(SkinnedMeshRenderer[] a, SkinnedMeshRenderer[] b)
	{
		if (a == null || a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}

	private static int GroupOfBone(CharInfo ci, Transform t)
	{
		Transform transform = t;
		while ((bool)transform && transform != ci.ch.transform)
		{
			if (ci.group.TryGetValue(transform, out var value))
			{
				if (transform != t)
				{
					ci.group[t] = value;
				}
				return value;
			}
			transform = transform.parent;
		}
		return 2;
	}

	private static MeshSamples Samples(Mesh m)
	{
		if (!m)
		{
			return null;
		}
		if (meshCache.TryGetValue(m, out var value))
		{
			return value;
		}
		value = null;
		if (m.isReadable && m.bindposeCount > 0)
		{
			Vector3[] vertices = m.vertices;
			Vector3[] normals = m.normals;
			BoneWeight[] boneWeights = m.boneWeights;
			Matrix4x4[] bindposes = m.bindposes;
			if (boneWeights != null && boneWeights.Length == vertices.Length)
			{
				int num = Mathf.Max(1, vertices.Length / 1500);
				int num2 = (vertices.Length + num - 1) / num;
				value = new MeshSamples
				{
					bone = new int[num2],
					lp = new Vector3[num2],
					ln = new Vector3[num2]
				};
				int num3 = 0;
				int num4 = 0;
				while (num4 < vertices.Length && num3 < num2)
				{
					int num5 = boneWeights[num4].boneIndex0;
					if (num5 < 0 || num5 >= bindposes.Length)
					{
						num5 = 0;
					}
					value.bone[num3] = num5;
					value.lp[num3] = bindposes[num5].MultiplyPoint3x4(vertices[num4]);
					value.ln[num3] = ((normals != null && normals.Length == vertices.Length) ? bindposes[num5].MultiplyVector(normals[num4]).normalized : Vector3.zero);
					num4 += num;
					num3++;
				}
			}
		}
		meshCache[m] = value;
		return value;
	}

	private static FBHitbox NearestHitbox(FBCharacter ch, Vector3 p, string part, out float best)
	{
		FBHitbox fBHitbox = null;
		best = 1E+09f;
		FBHitbox[] componentsInChildren = ch.GetComponentsInChildren<FBHitbox>(includeInactive: true);
		for (int i = 0; i < 2; i++)
		{
			if ((bool)fBHitbox)
			{
				break;
			}
			FBHitbox[] array = componentsInChildren;
			foreach (FBHitbox fBHitbox2 in array)
			{
				if (i != 0 || PartMatch(fBHitbox2, part))
				{
					float num = 1E+09f;
					Transform transform = fBHitbox2.transform;
					CapsuleCollider component2;
					if (fBHitbox2.TryGetComponent<SphereCollider>(out var component))
					{
						num = Vector3.Distance(p, transform.TransformPoint(component.center)) - component.radius * transform.lossyScale.x;
					}
					else if (fBHitbox2.TryGetComponent<CapsuleCollider>(out component2))
					{
						Seg(transform, component2, out var a, out var b, out var r);
						num = Vector3.Distance(p, Closest(a, b, p)) - r;
					}
					if (num < best)
					{
						best = num;
						fBHitbox = fBHitbox2;
					}
				}
			}
		}
		return fBHitbox;
	}

	private static bool PartMatch(FBHitbox h, string part)
	{
		if (part != null && (part == null || part.Length != 0))
		{
			if (!(part == "head"))
			{
				if (part == "body")
				{
					return !h.head && h.part == "body";
				}
				return !h.head && h.part != "body";
			}
			return h.head;
		}
		return true;
	}

	private static void Seg(Transform tr, CapsuleCollider cc, out Vector3 a, out Vector3 b, out float r)
	{
		Vector3 vector = ((cc.direction == 0) ? Vector3.right : ((cc.direction == 1) ? Vector3.up : Vector3.forward));
		float num = Mathf.Max(0f, cc.height * 0.5f - cc.radius);
		a = tr.TransformPoint(cc.center - vector * num);
		b = tr.TransformPoint(cc.center + vector * num);
		r = cc.radius * tr.lossyScale.x;
	}

	private static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
	{
		Vector3 vector = b - a;
		float num = ((vector.sqrMagnitude < 1E-08f) ? 0f : Mathf.Clamp01(Vector3.Dot(p - a, vector) / vector.sqrMagnitude));
		return a + vector * num;
	}

	public static Wound Attach(FBCharacter ch, Vector3 p, Vector3 dir, float size, bool streak = false, string part = null)
	{
		if (!ch || !FBFx.BloodOn)
		{
			return null;
		}
		if (!ready)
		{
			Warm();
		}
		if (ch.isPlayer && (bool)FBPlayer.I && FBPlayer.I.firstPerson)
		{
			return null;
		}
		CharInfo charInfo = Info(ch);
		float best;
		FBHitbox fBHitbox = NearestHitbox(ch, p, part, out best);
		if (!fBHitbox)
		{
			return null;
		}
		int num = GroupOfBone(charInfo, fBHitbox.transform.parent ? fBHitbox.transform.parent : fBHitbox.transform);
		if (fBHitbox.head)
		{
			num = 1;
		}
		dir = ((dir.sqrMagnitude > 1E-06f) ? dir.normalized : (-ch.transform.forward));
		Transform transform = null;
		Vector3 vector = default(Vector3);
		Vector3 vector2 = default(Vector3);
		float num2 = 0.22f * Mathf.Max(0.5f, ch.transform.lossyScale.y / 2.2f);
		Renderer renderer = null;
		for (int i = 0; i < charInfo.smr.Length; i++)
		{
			SkinnedMeshRenderer skinnedMeshRenderer = charInfo.smr[i];
			MeshSamples meshSamples = charInfo.ms[i];
			if (!skinnedMeshRenderer || meshSamples == null || !skinnedMeshRenderer.enabled)
			{
				continue;
			}
			Transform[] bones = skinnedMeshRenderer.bones;
			if (bones == null)
			{
				continue;
			}
			for (int j = 0; j < meshSamples.bone.Length; j++)
			{
				int num3 = meshSamples.bone[j];
				if (num3 >= bones.Length)
				{
					continue;
				}
				Transform transform2 = bones[num3];
				if ((bool)transform2 && (GroupOfBone(charInfo, transform2) & num) != 0)
				{
					float num4 = (transform2.TransformPoint(meshSamples.lp[j]) - p).magnitude;
					if (meshSamples.ln[j] != Vector3.zero && Vector3.Dot(transform2.TransformDirection(meshSamples.ln[j]), dir) > 0.15f)
					{
						num4 += 0.06f;
					}
					if (num4 < num2)
					{
						num2 = num4;
						transform = transform2;
						vector = meshSamples.lp[j];
						vector2 = meshSamples.ln[j];
						renderer = skinnedMeshRenderer;
					}
				}
			}
		}
		if (!transform)
		{
			transform = (fBHitbox.transform.parent ? fBHitbox.transform.parent : fBHitbox.transform);
			Vector3 vector3;
			if (!fBHitbox.TryGetComponent<CapsuleCollider>(out var component))
			{
				vector3 = ((!fBHitbox.TryGetComponent<SphereCollider>(out var component2)) ? transform.position : fBHitbox.transform.TransformPoint(component2.center));
			}
			else
			{
				Seg(fBHitbox.transform, component, out var a, out var b, out best);
				vector3 = Closest(a, b, p);
			}
			Vector3 vector4 = p - vector3;
			Vector3 position = vector3 + vector4 * 0.72f;
			vector = transform.InverseTransformPoint(position);
			vector2 = transform.InverseTransformDirection((vector4.sqrMagnitude > 1E-06f) ? vector4.normalized : (-dir));
			renderer = ((charInfo.smr.Length != 0) ? charInfo.smr[0] : null);
		}
		if (vector2 == Vector3.zero)
		{
			vector2 = transform.InverseTransformDirection(-dir);
		}
		List<Wound> wounds = charInfo.wounds;
		while (wounds.Count >= FBFxQuality.WoundsPerChar * (streak ? 1 : 1) && wounds.Count > 0)
		{
			Free(wounds[0]);
		}
		Wound wound = TakeSlot();
		wound.on = true;
		wound.owner = ch;
		wound.bone = transform;
		wound.group = num;
		wound.seq = ++seq;
		wound.streak = streak;
		wound.skin = renderer;
		wounds.Add(wound);
		wound.go.SetActive(value: true);
		wound.tr.SetParent(transform, worldPositionStays: false);
		wound.go.layer = (renderer ? renderer.gameObject.layer : ch.gameObject.layer);
		float num5 = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
		float num6 = 0.0035f / num5;
		Vector3 normalized = vector2.normalized;
		Vector3 vector5 = transform.InverseTransformDirection(Vector3.up);
		if (streak)
		{
			vector5 = Vector3.ProjectOnPlane(vector5, normalized);
		}
		Quaternion quaternion = Quaternion.LookRotation(-normalized, (streak && vector5.sqrMagnitude > 0.0001f) ? vector5.normalized : (Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), normalized) * Perp(normalized)));
		wound.tr.localPosition = vector + normalized * num6 + (streak ? (quaternion * Vector3.down * (size * 0.35f / num5)) : Vector3.zero);
		wound.tr.localRotation = quaternion;
		wound.tr.localScale = new Vector3(size, size * (streak ? 1.6f : 1f), size) / num5;
		wound.mr.sharedMaterial = (streak ? matStreak : mat);
		wound.mr.sortingOrder = wound.seq % 30000;
		int num7 = (streak ? (10 + UnityEngine.Random.Range(0, 2)) : UnityEngine.Random.Range(0, 3));
		if (!streak && size > 0.055f && UnityEngine.Random.value < 0.5f)
		{
			num7 = 3;
		}
		int num8 = (streak ? 4 : 2);
		int num9 = num8;
		mpb.Clear();
		mpb.SetVector(idST, new Vector4(1f / (float)num8, 1f / (float)num9, (float)(num7 % num8) / (float)num8, 1f - ((float)(num7 / num8) + 1f) / (float)num9));
		mpb.SetColor(idBaseColor, streak ? new Color(0.85f, 0.8f, 0.8f, 0.9f) : Color.white);
		wound.mr.SetPropertyBlock(mpb);
		wound.mr.enabled = true;
		return wound;
	}

	private static Vector3 Perp(Vector3 n)
	{
		return Vector3.Cross(n, (Mathf.Abs(n.y) > 0.95f) ? Vector3.forward : Vector3.up).normalized;
	}

	private static Wound TakeSlot()
	{
		int num = 0;
		Wound wound = null;
		Wound[] array = pool;
		foreach (Wound wound2 in array)
		{
			if (wound2.on)
			{
				num++;
			}
			else if (wound == null)
			{
				wound = wound2;
			}
		}
		if (wound == null || num >= FBFxQuality.MaxWounds)
		{
			wound = null;
			array = pool;
			foreach (Wound wound3 in array)
			{
				if (wound3.on && (wound == null || wound3.seq < wound.seq))
				{
					wound = wound3;
				}
			}
			Free(wound);
		}
		int num2 = Array.IndexOf(pool, wound);
		if (!wound.go)
		{
			wound = (pool[num2] = Make(num2));
		}
		return wound;
	}

	private static void Free(Wound w)
	{
		if (w == null)
		{
			return;
		}
		w.on = false;
		if ((bool)w.owner && chars.TryGetValue(w.owner, out var value))
		{
			value.wounds.Remove(w);
		}
		else
		{
			foreach (CharInfo value2 in chars.Values)
			{
				value2.wounds.Remove(w);
			}
		}
		w.owner = null;
		w.bone = null;
		if ((bool)w.go)
		{
			w.mr.enabled = false;
			w.tr.SetParent(FBFxLib.Root, worldPositionStays: false);
			w.go.SetActive(value: false);
		}
	}

	public static void ClearCharacter(FBCharacter ch)
	{
		if ((bool)ch && chars.TryGetValue(ch, out var value))
		{
			for (int num = value.wounds.Count - 1; num >= 0; num--)
			{
				Free(value.wounds[num]);
			}
		}
	}

	public static void Clear()
	{
		if (pool != null)
		{
			Wound[] array = pool;
			foreach (Wound wound in array)
			{
				if (wound.on)
				{
					Free(wound);
				}
			}
		}
		chars.Clear();
	}

	public static void Tick(float dt)
	{
		if (!ready)
		{
			return;
		}
		dead.Clear();
		foreach (KeyValuePair<FBCharacter, CharInfo> @char in chars)
		{
			FBCharacter key = @char.Key;
			CharInfo value = @char.Value;
			if (!key)
			{
				dead.Add(key);
				continue;
			}
			Vector3 position = key.transform.position;
			bool num = (key.alive && !value.wasAlive) || (position - value.lastPos).sqrMagnitude > 64f;
			value.wasAlive = key.alive;
			value.lastPos = position;
			if (num && value.wounds.Count > 0)
			{
				ClearCharacter(key);
				continue;
			}
			for (int num2 = value.wounds.Count - 1; num2 >= 0; num2--)
			{
				Wound wound = value.wounds[num2];
				if (!wound.go || !wound.bone)
				{
					wound.on = false;
					value.wounds.RemoveAt(num2);
				}
				else
				{
					bool flag = !wound.skin || (wound.skin.enabled && wound.skin.gameObject.activeInHierarchy && wound.skin.shadowCastingMode != ShadowCastingMode.ShadowsOnly);
					if (wound.mr.enabled != flag)
					{
						wound.mr.enabled = flag;
					}
				}
			}
			if (value.wounds.Count > 0 && FBFxQuality.Drips && (key.alive || key.knocked))
			{
				Drip(key, value, dt);
			}
		}
		foreach (FBCharacter item in dead)
		{
			if (chars.TryGetValue(item, out var value2))
			{
				foreach (Wound wound2 in value2.wounds)
				{
					wound2.on = false;
					wound2.owner = null;
				}
			}
			chars.Remove(item);
		}
	}

	private static void Drip(FBCharacter ch, CharInfo ci, float dt)
	{
		if ((ci.dripT -= dt) > 0f)
		{
			return;
		}
		float t = Mathf.Clamp01(1f - ch.hp / 100f);
		ci.dripT = Mathf.Lerp(3.2f, 0.9f, t) * UnityEngine.Random.Range(0.7f, 1.3f);
		if (ch.knocked)
		{
			ci.dripT *= 0.6f;
		}
		if (ch.hp > 75f && !ch.knocked)
		{
			return;
		}
		Wound wound = ci.wounds[ci.wounds.Count - 1];
		if (!wound.tr)
		{
			return;
		}
		Vector3 p = wound.tr.position + UnityEngine.Random.insideUnitSphere * 0.05f;
		float? num = FBWorld.GroundY(p.x, p.y, p.z, 0.2f, 2.5f);
		if (num.HasValue)
		{
			float valueOrDefault = num.GetValueOrDefault();
			if (FBFxQuality.Lod(p) > 0f)
			{
				FBDecalFx.Place(FBDecalFx.Atlas.Blood, 14 + UnityEngine.Random.Range(0, 2), new Vector3(p.x, valueOrDefault, p.z), Vector3.up, UnityEngine.Random.Range(0.12f, 0.22f), null, 1f, null, FBFxQuality.DecalLife * 0.6f);
			}
		}
	}
}
}
