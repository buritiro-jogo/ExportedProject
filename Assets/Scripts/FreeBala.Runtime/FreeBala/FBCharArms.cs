using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
[DefaultExecutionOrder(300)]
public class FBCharArms : MonoBehaviour
{
	private class Finger
	{
		public string name;

		public Transform[] c;

		public Transform[] f;
	}

	private class Arm
	{
		public Transform root;

		public Transform cArm;

		public Transform cFore;

		public Transform cHand;

		public Transform fArm;

		public Transform fFore;

		public Transform fHand;

		public List<Finger> fingers = new List<Finger>();

		public bool ok;

		public bool ownHand;

		public Finger knuckle;

		public Dictionary<Transform, (Vector3 p, Quaternion q)> rest = new Dictionary<Transform, (Vector3, Quaternion)>();

		public float la;

		public float lb;

		public float scale;

		public Vector3 armRest;

		public Vector3 sRest;

		public Vector3 handDirRest;

		public Vector3 baseScale;

		public Vector3 n0;

		public Vector3 lastN;

		public float twistArm;

		public float twistFore;

		public float pron;
	}

	private static readonly (string c, string f)[] FINGERS = new(string, string)[5]
	{
		("Thumb", "thumb"),
		("Index", "index"),
		("Middle", "middle"),
		("Ring", "ring"),
		("Little", "pinky")
	};

	private FBFpRig rig;

	private readonly List<Arm> arms = new List<Arm>();

	private readonly List<Renderer> generic = new List<Renderer>();

	public bool ok;

	private Color? handTint;

	private readonly List<(Material m, Color c)> tinted = new List<(Material, Color)>();

	public static bool Legacy;

	public static FBCharArms Attach(FBFpRig rig, FBCharacter ch)
	{
		if (!rig || !ch)
		{
			return null;
		}
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Char(ch.skinId) : null);
		if (!gameObject)
		{
			return null;
		}
		FBCharArms component = rig.GetComponent<FBCharArms>();
		if ((bool)component)
		{
			component.Dispose();
			UnityEngine.Object.Destroy(component);
		}
		FBCharArms fBCharArms = rig.gameObject.AddComponent<FBCharArms>();
		fBCharArms.Build(rig, gameObject);
		if (!fBCharArms.ok)
		{
			fBCharArms.Dispose();
			UnityEngine.Object.Destroy(fBCharArms);
			return null;
		}
		return fBCharArms;
	}

	private static Transform FpBone(FBFpRig rig, string n)
	{
		Transform[] componentsInChildren = rig.pitchNode.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform.name == n)
			{
				return transform;
			}
		}
		return null;
	}

	private void Build(FBFpRig r, GameObject prefab)
	{
		rig = r;
		string[] array = new string[2] { "L", "R" };
		Renderer[] componentsInChildren3;
		foreach (string text in array)
		{
			Arm A = new Arm();
			string text2 = ((text == "L") ? "Left" : "Right");
			string f = ((text == "L") ? "_l" : "_r");
			GameObject gameObject = UnityEngine.Object.Instantiate(prefab);
			gameObject.name = "fp_char_arm_" + text;
			A.root = gameObject.transform;
			A.baseScale = gameObject.transform.localScale;
			MonoBehaviour[] componentsInChildren = gameObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
			foreach (MonoBehaviour monoBehaviour in componentsInChildren)
			{
				if (!(monoBehaviour is FBCharArms))
				{
					monoBehaviour.enabled = false;
				}
			}
			Animator an = gameObject.GetComponentInChildren<Animator>();
			if (!an || !an.isHuman)
			{
				UnityEngine.Object.Destroy(gameObject);
				continue;
			}
			A.cArm = HB(text2 + "UpperArm");
			A.cFore = HB(text2 + "LowerArm");
			A.cHand = HB(text2 + "Hand");
			A.fArm = FpBone(rig, "upperarm" + f);
			A.fFore = FpBone(rig, "lowerarm" + f);
			A.fHand = FpBone(rig, "hand" + f);
			A.ok = (bool)A.cArm && (bool)A.cFore && (bool)A.cHand && (bool)A.fArm && (bool)A.fFore && (bool)A.fHand;
			if (!A.ok)
			{
				UnityEngine.Object.Destroy(gameObject);
				continue;
			}
			(string, string)[] fINGERS = FINGERS;
			for (int k = 0; k < fINGERS.Length; k++)
			{
				(string, string) tuple = fINGERS[k];
				string item = tuple.Item1;
				string fn = tuple.Item2;
				Transform[] array2 = new Transform[4]
				{
					HB(text2 + item + "Proximal"),
					HB(text2 + item + "Intermediate"),
					HB(text2 + item + "Distal"),
					null
				};
				Transform[] array3 = new int[3] { 1, 2, 3 }.Select((int i) => FpBone(rig, $"{fn}_0{i}{f}")).ToArray();
				if (array2.Take(3).All((Transform x) => x) && array3.All((Transform x) => x))
				{
					A.fingers.Add(new Finger
					{
						name = item,
						c = array2,
						f = array3
					});
				}
			}
			an.enabled = false;
			A.ownHand = false;
			A.knuckle = A.fingers.Find((Finger x) => x.name == "Middle") ?? A.fingers.Find((Finger x) => x.name == "Index");
			if (!handTint.HasValue)
			{
				handTint = HandColor(gameObject, A.cHand);
			}
			gameObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			foreach (Transform item3 in new Transform[3] { A.cArm, A.cFore, A.cHand }.Concat(A.fingers.SelectMany((Finger x) => x.c.Where((Transform y) => y))))
			{
				A.rest[item3] = (item3.position, item3.rotation);
			}
			A.la = (RP(A.cFore) - RP(A.cArm)).magnitude;
			A.lb = (RP(A.cHand) - RP(A.cFore)).magnitude;
			A.armRest = RP(A.cArm);
			Finger finger = A.fingers.Find((Finger x) => x.name == "Index");
			Finger finger2 = A.fingers.Find((Finger x) => x.name == "Little") ?? A.fingers.Find((Finger x) => x.name == "Ring");
			A.sRest = ((finger != null && finger2 != null) ? (RP(finger.c[0]) - RP(finger2.c[0])) : Vector3.forward);
			A.handDirRest = ((A.knuckle != null) ? RP(A.knuckle.c[0]) : (RP(A.cHand) - RP(A.cFore) + RP(A.cHand))) - RP(A.cHand);
			A.n0 = Vector3.Cross(RP(A.cFore) - RP(A.cArm), Vector3.forward).normalized;
			if (A.n0.sqrMagnitude < 0.5f)
			{
				A.n0 = Vector3.Cross(RP(A.cFore) - RP(A.cArm), Vector3.up).normalized;
			}
			IEnumerable<Transform> collection;
			if (!A.ownHand)
			{
				IEnumerable<Transform> enumerable = new Transform[2] { A.cArm, A.cFore };
				collection = enumerable;
			}
			else
			{
				collection = new Transform[3] { A.cArm, A.cFore, A.cHand }.Concat(A.fingers.SelectMany((Finger x) => x.c.Where((Transform y) => y)));
			}
			HashSet<Transform> hashSet = new HashSet<Transform>(collection);
			Transform[] componentsInChildren2;
			if (A.ownHand)
			{
				componentsInChildren2 = A.cHand.GetComponentsInChildren<Transform>(includeInactive: true);
				foreach (Transform item2 in componentsInChildren2)
				{
					hashSet.Add(item2);
				}
			}
			float num = (A.ownHand ? 1.5f : 1f);
			bool flag = false;
			componentsInChildren3 = gameObject.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer obj in componentsInChildren3)
			{
				obj.enabled = false;
				SkinnedMeshRenderer skinnedMeshRenderer = obj as SkinnedMeshRenderer;
				if (!skinnedMeshRenderer || !skinnedMeshRenderer.sharedMesh || !skinnedMeshRenderer.sharedMesh.isReadable)
				{
					continue;
				}
				Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
				BoneWeight[] boneWeights = sharedMesh.boneWeights;
				Transform[] bones = skinnedMeshRenderer.bones;
				if (boneWeights.Length != sharedMesh.vertexCount)
				{
					continue;
				}
				float[] array4 = new float[boneWeights.Length];
				for (int l = 0; l < boneWeights.Length; l++)
				{
					BoneWeight boneWeight = boneWeights[l];
					float num2 = 0f;
					if (In(bones, boneWeight.boneIndex0, hashSet))
					{
						num2 += boneWeight.weight0;
					}
					if (In(bones, boneWeight.boneIndex1, hashSet))
					{
						num2 += boneWeight.weight1;
					}
					if (In(bones, boneWeight.boneIndex2, hashSet))
					{
						num2 += boneWeight.weight2;
					}
					if (In(bones, boneWeight.boneIndex3, hashSet))
					{
						num2 += boneWeight.weight3;
					}
					array4[l] = num2;
				}
				Mesh mesh = UnityEngine.Object.Instantiate(sharedMesh);
				mesh.name = sharedMesh.name + "_braco" + text;
				bool flag2 = false;
				for (int m = 0; m < sharedMesh.subMeshCount; m++)
				{
					int[] triangles = sharedMesh.GetTriangles(m);
					List<int> list = new List<int>(triangles.Length / 4);
					for (int num3 = 0; num3 + 2 < triangles.Length; num3 += 3)
					{
						if (array4[triangles[num3]] + array4[triangles[num3 + 1]] + array4[triangles[num3 + 2]] > num)
						{
							list.Add(triangles[num3]);
							list.Add(triangles[num3 + 1]);
							list.Add(triangles[num3 + 2]);
						}
					}
					mesh.SetTriangles(list, m);
					flag2 |= list.Count > 0;
				}
				if (!flag2)
				{
					UnityEngine.Object.Destroy(mesh);
					continue;
				}
				skinnedMeshRenderer.sharedMesh = mesh;
				skinnedMeshRenderer.enabled = true;
				skinnedMeshRenderer.updateWhenOffscreen = true;
				skinnedMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
				skinnedMeshRenderer.receiveShadows = false;
				flag = true;
			}
			if (!flag)
			{
				UnityEngine.Object.Destroy(gameObject);
				A.ok = false;
				continue;
			}
			componentsInChildren2 = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
			for (int k = 0; k < componentsInChildren2.Length; k++)
			{
				componentsInChildren2[k].gameObject.layer = 14;
			}
			gameObject.transform.SetParent(rig.transform, worldPositionStays: true);
			arms.Add(A);
			Transform HB(string n)
			{
				if (!Enum.TryParse<HumanBodyBones>(n, out var result))
				{
					return null;
				}
				return an.GetBoneTransform(result);
			}
			Vector3 RP(Transform o)
			{
				return A.rest[o].p;
			}
		}
		ok = arms.Count == 2;
		if (!ok)
		{
			return;
		}
		bool flag3 = arms.Any((Arm a) => !a.ownHand);
		componentsInChildren3 = rig.pitchNode.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren3)
		{
			string input = renderer.name + " " + (renderer.transform.parent ? renderer.transform.parent.name : "");
			if (Regex.IsMatch(input, "Sleeves|Hands", RegexOptions.IgnoreCase))
			{
				if (flag3 && Regex.IsMatch(input, "Hands", RegexOptions.IgnoreCase))
				{
					Tint(renderer);
					continue;
				}
				generic.Add(renderer);
				renderer.enabled = false;
			}
		}
	}

	private static Color? Median(Material mat, Mesh mesh, Func<int, bool> keep)
	{
		if (!mat || !mesh || !mesh.isReadable)
		{
			return null;
		}
		Texture texture = mat.mainTexture;
		if (!texture)
		{
			string[] texturePropertyNames = mat.GetTexturePropertyNames();
			foreach (string input in texturePropertyNames)
			{
				if (Regex.IsMatch(input, "base|main|albedo|diffuse", RegexOptions.IgnoreCase) && (bool)mat.GetTexture(input))
				{
					texture = mat.GetTexture(input);
					break;
				}
			}
		}
		if (!texture)
		{
			return null;
		}
		Vector2[] uv = mesh.uv;
		if (uv == null || uv.Length != mesh.vertexCount)
		{
			return null;
		}
		RenderTexture temporary = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
		RenderTexture active = RenderTexture.active;
		Graphics.Blit(texture, temporary);
		RenderTexture.active = temporary;
		Texture2D texture2D = new Texture2D(256, 256, TextureFormat.RGBA32, mipChain: false);
		texture2D.ReadPixels(new Rect(0f, 0f, 256f, 256f), 0, 0);
		texture2D.Apply(updateMipmaps: false);
		RenderTexture.active = active;
		RenderTexture.ReleaseTemporary(temporary);
		Color32[] pixels = texture2D.GetPixels32();
		UnityEngine.Object.Destroy(texture2D);
		List<float> list = new List<float>();
		List<float> list2 = new List<float>();
		List<float> list3 = new List<float>();
		for (int j = 0; j < uv.Length; j++)
		{
			if (keep(j))
			{
				float num = uv[j].x - Mathf.Floor(uv[j].x);
				float num2 = uv[j].y - Mathf.Floor(uv[j].y);
				Color32 color = pixels[Mathf.Clamp((int)(num2 * 256f), 0, 255) * 256 + Mathf.Clamp((int)(num * 256f), 0, 255)];
				list.Add((int)color.r);
				list2.Add((int)color.g);
				list3.Add((int)color.b);
			}
		}
		if (list.Count < 8)
		{
			return null;
		}
		list.Sort();
		list2.Sort();
		list3.Sort();
		int index = list.Count / 2;
		Color color2 = (mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.color : Color.white));
		return new Color(list[index] / 255f * color2.r, list2[index] / 255f * color2.g, list3[index] / 255f * color2.b);
	}

	private static Color? HandColor(GameObject go, Transform hand)
	{
		HashSet<Transform> hb = new HashSet<Transform>(hand.GetComponentsInChildren<Transform>(includeInactive: true));
		SkinnedMeshRenderer[] componentsInChildren = go.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
		foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
		{
			Mesh sharedMesh = skinnedMeshRenderer.sharedMesh;
			if (!sharedMesh || !sharedMesh.isReadable)
			{
				continue;
			}
			Transform[] bones = skinnedMeshRenderer.bones;
			BoneWeight[] bw = sharedMesh.boneWeights;
			if (bw.Length != sharedMesh.vertexCount)
			{
				continue;
			}
			int num = -1;
			int num2 = 0;
			for (int k = 0; k < sharedMesh.subMeshCount && k < skinnedMeshRenderer.sharedMaterials.Length; k++)
			{
				HashSet<int> hashSet = new HashSet<int>(sharedMesh.GetTriangles(k));
				int num3 = 0;
				foreach (int item in hashSet)
				{
					if (Keep(item))
					{
						num3++;
					}
				}
				if (num3 > num2)
				{
					num2 = num3;
					num = k;
				}
			}
			if (num >= 0)
			{
				HashSet<int> tri = new HashSet<int>(sharedMesh.GetTriangles(num));
				Color? result = Median(skinnedMeshRenderer.sharedMaterials[num], sharedMesh, (int i) => tri.Contains(i) && Keep(i));
				if (result.HasValue)
				{
					return result;
				}
			}
			bool Keep(int i)
			{
				BoneWeight boneWeight = bw[i];
				int num4 = boneWeight.boneIndex0;
				float num5 = boneWeight.weight0;
				if (boneWeight.weight1 > num5)
				{
					num4 = boneWeight.boneIndex1;
					num5 = boneWeight.weight1;
				}
				if (boneWeight.weight2 > num5)
				{
					num4 = boneWeight.boneIndex2;
					num5 = boneWeight.weight2;
				}
				if (boneWeight.weight3 > num5)
				{
					num4 = boneWeight.boneIndex3;
				}
				if (num4 >= 0 && num4 < bones.Length && (bool)bones[num4])
				{
					return hb.Contains(bones[num4]);
				}
				return false;
			}
		}
		return null;
	}

	private void Tint(Renderer rd)
	{
		if (!handTint.HasValue)
		{
			return;
		}
		SkinnedMeshRenderer skinnedMeshRenderer = rd as SkinnedMeshRenderer;
		Mesh mesh = (skinnedMeshRenderer ? skinnedMeshRenderer.sharedMesh : rd.GetComponent<MeshFilter>()?.sharedMesh);
		Material material = rd.material;
		Color? color = Median(material, mesh, (int _) => true);
		if (color.HasValue)
		{
			Color value = color.Value;
			Color value2 = handTint.Value;
			Color item = (material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color);
			Color color2 = new Color(Mathf.Clamp(value2.r / Mathf.Max(0.02f, value.r), 0.15f, 1.6f), Mathf.Clamp(value2.g / Mathf.Max(0.02f, value.g), 0.15f, 1.6f), Mathf.Clamp(value2.b / Mathf.Max(0.02f, value.b), 0.15f, 1.6f), 1f);
			Color value3 = new Color(item.r * color2.r, item.g * color2.g, item.b * color2.b, item.a);
			if (material.HasProperty("_BaseColor"))
			{
				material.SetColor("_BaseColor", value3);
			}
			if (material.HasProperty("_Color"))
			{
				material.SetColor("_Color", value3);
			}
			tinted.Add((material, item));
			Debug.Log($"[FreeBala][fp] mão do rig tingida: personagem {value2} · rig {value} · fator {color2}");
		}
	}

	private static bool In(Transform[] bones, int i, HashSet<Transform> keep)
	{
		if (i >= 0 && i < bones.Length && (bool)bones[i])
		{
			return keep.Contains(bones[i]);
		}
		return false;
	}

	public void Dispose()
	{
		foreach (Arm arm in arms)
		{
			if ((bool)arm.root)
			{
				UnityEngine.Object.Destroy(arm.root.gameObject);
			}
		}
		arms.Clear();
		foreach (Renderer item in generic)
		{
			if ((bool)item)
			{
				item.enabled = true;
			}
		}
		generic.Clear();
		foreach (var (material, value) in tinted)
		{
			if ((bool)material)
			{
				if (material.HasProperty("_BaseColor"))
				{
					material.SetColor("_BaseColor", value);
				}
				if (material.HasProperty("_Color"))
				{
					material.SetColor("_Color", value);
				}
			}
		}
		tinted.Clear();
	}

	private void OnDestroy()
	{
		Dispose();
	}

	private static Quaternion Basis(Vector3 dir, Vector3 sec)
	{
		Vector3 normalized = dir.normalized;
		Vector3 vector = sec - normalized * Vector3.Dot(sec, normalized);
		if (vector.sqrMagnitude < 1E-10f)
		{
			vector = Vector3.up - normalized * normalized.y;
		}
		vector.Normalize();
		Vector3 upwards = Vector3.Cross(vector, normalized);
		return Quaternion.LookRotation(vector, upwards);
	}

	private static Quaternion FrameRot(Vector3 dRest, Vector3 sRest, Vector3 dNow, Vector3 sNow)
	{
		return Basis(dNow, sNow) * Quaternion.Inverse(Basis(dRest, sRest));
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBCharArms.LateUpdate");

	private void LateUpdate()
	{
		if (!FBTempo.On)
		{
			LateUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		LateUpdate__fbt();
		FBTempo.Soma(__fbt_LateUpdate, t0);
	}

	private void LateUpdate__fbt()
	{
		if (!ok || !rig)
		{
			return;
		}
		bool flag = (bool)rig.pitchNode && rig.pitchNode.gameObject.activeInHierarchy;
		foreach (Arm arm in arms)
		{
			arm.root.gameObject.SetActive(flag);
			if (flag)
			{
				Step(arm);
			}
		}
	}

	public string TwistReport()
	{
		return string.Join(" · ", arms.Select((Arm a, int i) => string.Format("{0}: braço {1:0}° antebraço {2:0}° da dobradiça (pronação do punho {3:0}°)", (i == 0) ? "esq" : "dir", a.twistArm, a.twistFore, a.pron)));
	}

	private static void Step(Arm A)
	{
		Vector3 position = A.fArm.position;
		Vector3 position2 = A.fFore.position;
		Vector3 position3 = A.fHand.position;
		if (A.scale <= 0f)
		{
			A.scale = ((position - position2).magnitude + (position2 - position3).magnitude) / (A.la + A.lb);
		}
		float scale = A.scale;
		float num = A.la * scale;
		float num2 = A.lb * scale;
		Finger finger = A.fingers.Find((Finger x) => x.name == "Index");
		Finger finger2 = A.fingers.Find((Finger x) => x.name == "Little") ?? A.fingers.Find((Finger x) => x.name == "Ring");
		Vector3 sNow = ((finger != null && finger2 != null) ? (finger.f[0].position - finger2.f[0].position) : Vector3.forward);
		Vector3 vector = ((A.knuckle != null) ? A.knuckle.f[0].position : (position3 + (position3 - position2)));
		Vector3 vector2 = position3 - position;
		float num3 = Mathf.Min(vector2.magnitude, num + num2 - 0.0001f);
		vector2.Normalize();
		Vector3 vector3 = position2 - position;
		vector3 -= vector2 * Vector3.Dot(vector3, vector2);
		if (vector3.sqrMagnitude < 1E-08f)
		{
			vector3 = Vector3.Cross(sNow, vector2);
		}
		vector3.Normalize();
		float num4 = Mathf.Clamp((num * num + num3 * num3 - num2 * num2) / (2f * num * num3), -1f, 1f);
		Vector3 vector4 = position + vector2 * (num * num4) + vector3 * (num * Mathf.Sqrt(1f - num4 * num4));
		Vector3 vector5 = position + vector2 * num3;
		A.root.localScale = A.baseScale * scale;
		A.root.rotation = Quaternion.identity;
		A.root.position = position - A.armRest * scale;
		Vector3 dRest2 = RP(A.cFore) - RP(A.cArm);
		Vector3 dRest3 = RP(A.cHand) - RP(A.cFore);
		Vector3 vector6 = vector4 - position;
		Vector3 vector7 = vector5 - vector4;
		Vector3 vector8 = Vector3.Cross(vector6, vector7);
		if (vector8.sqrMagnitude < 1E-06f * vector6.sqrMagnitude * vector7.sqrMagnitude)
		{
			vector8 = Vector3.Cross(position2 - position, position3 - position2);
		}
		if (vector8.sqrMagnitude < 1E-08f)
		{
			vector8 = ((A.lastN.sqrMagnitude > 0.5f) ? A.lastN : Vector3.Cross(vector2, sNow));
		}
		vector8.Normalize();
		A.lastN = vector8;
		Quaternion quaternion = FrameRot(dRest2, A.n0, vector6, vector8) * A.rest[A.cArm].q;
		Quaternion quaternion2 = FrameRot(dRest3, A.n0, vector7, vector8);
		Quaternion quaternion3 = FrameRot(A.handDirRest, A.sRest, vector - position3, sNow);
		Vector3 normalized = vector7.normalized;
		Vector3 from = Vector3.ProjectOnPlane(quaternion2 * A.sRest, normalized);
		Vector3 to = Vector3.ProjectOnPlane(quaternion3 * A.sRest, normalized);
		float num5 = ((from.sqrMagnitude > 1E-08f && to.sqrMagnitude > 1E-08f) ? Vector3.SignedAngle(from, to, normalized) : 0f);
		Quaternion quaternion4 = Quaternion.AngleAxis(Mathf.Clamp(num5 * 0.5f, -90f, 90f), normalized) * quaternion2 * A.rest[A.cFore].q;
		Quaternion a = FrameRot(dRest2, A.sRest, vector6, sNow) * A.rest[A.cArm].q;
		Quaternion a2 = FrameRot(dRest3, A.sRest, vector7, sNow) * A.rest[A.cFore].q;
		A.twistArm = Quaternion.Angle(a, quaternion);
		A.twistFore = Quaternion.Angle(a2, quaternion4);
		A.pron = num5;
		if (Legacy)
		{
			Put(A.cArm, dRest2, vector6);
			Put(A.cFore, dRest3, vector7);
		}
		else
		{
			A.cArm.rotation = quaternion;
			A.cFore.rotation = quaternion4;
		}
		Put(A.cHand, A.handDirRest, vector - position3);
		foreach (Finger finger3 in A.fingers)
		{
			List<Vector3> list = finger3.f.Select((Transform o) => o.position).ToList();
			list.Add(list[2] + (list[2] - list[1]) * 0.85f);
			for (int i = 0; i < 3; i++)
			{
				Transform transform = finger3.c[i];
				Transform transform2 = finger3.c[i + 1];
				if ((bool)transform)
				{
					Vector3 dRest4 = (transform2 ? (RP(transform2) - RP(transform)) : (RP(transform) - RP(finger3.c[i - 1])));
					Put(transform, dRest4, list[i + 1] - list[i]);
				}
			}
		}
		void Put(Transform bone, Vector3 dRest, Vector3 dNow)
		{
			bone.rotation = FrameRot(dRest, A.sRest, dNow, sNow) * A.rest[bone].q;
		}
		Vector3 RP(Transform o)
		{
			return A.rest[o].p;
		}
	}
}
}
