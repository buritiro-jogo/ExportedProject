using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBShellSim : MonoBehaviour
{
	private class S
	{
		public Transform t;

		public MeshRenderer r;

		public Vector3 v;

		public Vector3 w;

		public float life;

		public float len;

		public bool rest;

		public MaterialPropertyBlock mpb;
	}

	private static FBShellSim I;

	private S[] pool;

	private int next;

	private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

	private static FBShellSim Ensure(int n)
	{
		if ((bool)I)
		{
			return I;
		}
		GameObject gameObject = new GameObject("FB_Cartuchos");
		I = gameObject.AddComponent<FBShellSim>();
		I.pool = new S[n];
		Mesh sharedMesh = FBFxLib.Shell();
		Material sharedMaterial = FBFxLib.Mat("Lit_Clip", null, Color.white, "CartuchoSim");
		for (int i = 0; i < n; i++)
		{
			GameObject gameObject2 = new GameObject("c" + i);
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			gameObject2.AddComponent<MeshFilter>().sharedMesh = sharedMesh;
			MeshRenderer meshRenderer = gameObject2.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = sharedMaterial;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			gameObject2.SetActive(value: false);
			I.pool[i] = new S
			{
				t = gameObject2.transform,
				r = meshRenderer,
				mpb = new MaterialPropertyBlock()
			};
		}
		return I;
	}

	public static void Spawn(Vector3 p, Vector3 v, Quaternion rot, Vector3 size, Color col, int poolSize)
	{
		FBShellSim fBShellSim = Ensure(poolSize);
		S s = fBShellSim.pool[fBShellSim.next];
		fBShellSim.next = (fBShellSim.next + 1) % fBShellSim.pool.Length;
		s.t.gameObject.SetActive(value: true);
		s.t.SetPositionAndRotation(p, rot);
		s.t.localScale = size;
		s.len = size.y;
		s.v = v;
		s.w = new Vector3(Random.Range(-12f, 12f), Random.Range(-20f, 20f), Random.Range(-12f, 12f));
		s.life = 4.5f;
		s.rest = false;
		s.mpb.SetColor(ColorId, col);
		s.r.SetPropertyBlock(s.mpb);
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBShellSim.Update");

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
		float num = Mathf.Min(Time.deltaTime, 0.05f);
		S[] array = pool;
		foreach (S s in array)
		{
			if (!s.t.gameObject.activeSelf)
			{
				continue;
			}
			s.life -= num;
			if (s.life <= 0f)
			{
				s.t.gameObject.SetActive(value: false);
			}
			else
			{
				if (s.rest)
				{
					continue;
				}
				s.v += Physics.gravity * num;
				Vector3 vector = s.t.position;
				Vector3 vector2 = s.v * num;
				if (Physics.Raycast(vector, vector2.normalized, out var hitInfo, vector2.magnitude + 0.01f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
				{
					vector = hitInfo.point + hitInfo.normal * 0.006f;
					Vector3 vector3 = Vector3.Dot(s.v, hitInfo.normal) * hitInfo.normal;
					Vector3 vector4 = s.v - vector3;
					s.v = vector4 * 0.55f - vector3 * 0.3f;
					s.w *= 0.45f;
					if (hitInfo.normal.y > 0.6f && s.v.magnitude < 0.45f)
					{
						Vector3 vector5 = Vector3.ProjectOnPlane(s.t.up, hitInfo.normal);
						if (vector5.sqrMagnitude < 0.0001f)
						{
							vector5 = Vector3.ProjectOnPlane(Random.onUnitSphere, hitInfo.normal);
						}
						s.t.SetPositionAndRotation(hitInfo.point + hitInfo.normal * 0.005f, Quaternion.LookRotation(vector5.normalized, hitInfo.normal) * Quaternion.Euler(90f, 0f, 0f));
						s.rest = true;
						continue;
					}
				}
				else
				{
					vector += vector2;
				}
				s.t.position = vector;
				s.t.rotation = Quaternion.Euler(s.w * 57.29578f * num) * s.t.rotation;
			}
		}
	}
}
}
