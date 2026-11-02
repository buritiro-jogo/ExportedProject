using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBFisica
{
	public struct Mat
	{
		public float dyn;

		public float sta;

		public float bounce;

		public string snd;
	}

	private class DebrisRunner : MonoBehaviour
	{
		private float chk;

		private static readonly int __fbt_Update = FBTempo.Id("FBFisica.DebrisRunner.Update");

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
			if ((chk -= Time.deltaTime) > 0f)
			{
				return;
			}
			chk = 1f;
			Camera main = Camera.main;
			for (int num = debris.Count - 1; num >= 0; num--)
			{
				var (gameObject, num2) = debris[num];
				if (!gameObject)
				{
					debris.RemoveAt(num);
				}
				else if (!(Time.time < num2))
				{
					bool flag = false;
					if ((bool)main)
					{
						Vector3 vector = main.WorldToViewportPoint(gameObject.transform.position);
						flag = vector.z > 0f && vector.z < 35f && vector.x > -0.1f && vector.x < 1.1f && vector.y > -0.1f && vector.y < 1.1f;
					}
					if (!flag || Time.time > num2 + 90f)
					{
						Object.Destroy(gameObject);
						debris.RemoveAt(num);
					}
				}
			}
		}
	}

	public static readonly Dictionary<string, Mat> MATS = new Dictionary<string, Mat>
	{
		["plastico"] = new Mat
		{
			dyn = 0.45f,
			sta = 0.55f,
			bounce = 0.25f,
			snd = "plastico"
		},
		["metal"] = new Mat
		{
			dyn = 0.35f,
			sta = 0.45f,
			bounce = 0.12f,
			snd = "metal"
		},
		["metal_pesado"] = new Mat
		{
			dyn = 0.45f,
			sta = 0.6f,
			bounce = 0.05f,
			snd = "metal_pesado"
		},
		["lata"] = new Mat
		{
			dyn = 0.3f,
			sta = 0.4f,
			bounce = 0.3f,
			snd = "lata"
		},
		["madeira"] = new Mat
		{
			dyn = 0.5f,
			sta = 0.6f,
			bounce = 0.15f,
			snd = "madeira"
		},
		["concreto"] = new Mat
		{
			dyn = 0.7f,
			sta = 0.85f,
			bounce = 0.02f,
			snd = "concreto"
		},
		["papelao"] = new Mat
		{
			dyn = 0.6f,
			sta = 0.7f,
			bounce = 0.05f,
			snd = "papelao"
		},
		["vidro"] = new Mat
		{
			dyn = 0.3f,
			sta = 0.4f,
			bounce = 0.1f,
			snd = "vidro"
		},
		["grade"] = new Mat
		{
			dyn = 0.4f,
			sta = 0.5f,
			bounce = 0.1f,
			snd = "grade"
		},
		["sacola"] = new Mat
		{
			dyn = 0.8f,
			sta = 0.9f,
			bounce = 0f,
			snd = "sacola"
		},
		["ceramica"] = new Mat
		{
			dyn = 0.5f,
			sta = 0.6f,
			bounce = 0.08f,
			snd = "ceramica"
		}
	};

	private static Dictionary<string, List<string>> snd;

	private static float budgetT;

	private static int budget;

	public static readonly List<FBPropBody> All = new List<FBPropBody>();

	public static int Impacts;

	private static readonly Dictionary<string, PhysicsMaterial> pms = new Dictionary<string, PhysicsMaterial>();

	private static readonly (string pre, float vMin, float kg, string mat)[] FIXOS = new(string, float, float, string)[6]
	{
		("x171_Placas", 4.5f, 18f, "metal"),
		("x171_LixeiraPoste", 4f, 14f, "metal"),
		("x171_SM_Caixadoscorreios", 6f, 40f, "metal_pesado"),
		("x171_SM_Orelhao", 7f, 60f, "metal_pesado"),
		("x171_PontoOnibus", 10f, 180f, "metal_pesado"),
		("x171_SM_PontoOnibus", 10f, 180f, "metal_pesado")
	};

	public static int Knocked;

	private static readonly List<(GameObject g, float t)> debris = new List<(GameObject, float)>();

	private static DebrisRunner dr;

	public static int DebrisCount => debris.Count;

	private static void Load()
	{
		if (snd != null)
		{
			return;
		}
		snd = new Dictionary<string, List<string>>();
		TextAsset textAsset = Resources.Load<TextAsset>("Audio/171/fisica/fisica");
		if (!textAsset)
		{
			return;
		}
		foreach (KeyValuePair<string, object> item in Json.O(Json.Parse(textAsset.text), "cats") ?? new Dictionary<string, object>())
		{
			snd[item.Key] = (from o in (item.Value as List<object>) ?? new List<object>()
				select o as string into x
				where x != null
				select x).ToList();
		}
	}

	public static bool Sound(string cat, Vector3 p, float vol, float refD = 6f)
	{
		Load();
		if (Time.time > budgetT)
		{
			budgetT = Time.time + 1f;
			budget = 24;
		}
		if (budget-- <= 0)
		{
			return false;
		}
		if (!snd.TryGetValue(cat ?? "", out var value) || value.Count == 0)
		{
			return false;
		}
		AudioClip audioClip = Resources.Load<AudioClip>("Audio/171/fisica/" + value[Random.Range(0, value.Count)]);
		if (!audioClip)
		{
			return false;
		}
		return FBAudio.PlayAt(audioClip, p, Mathf.Clamp01(vol), refD);
	}

	public static FBPropBody Make(GameObject g, string mat, float kg)
	{
		if (!g)
		{
			return null;
		}
		Rigidbody rigidbody = g.GetComponent<Rigidbody>().OrNull() ?? g.AddComponent<Rigidbody>();
		rigidbody.mass = Mathf.Max(0.05f, kg);
		rigidbody.isKinematic = false;
		rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
		rigidbody.linearDamping = 0.05f;
		rigidbody.angularDamping = 0.25f;
		rigidbody.collisionDetectionMode = ((kg < 2f) ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete);
		rigidbody.sleepThreshold = 0.02f;
		if (!g.GetComponentInChildren<Collider>())
		{
			Renderer[] componentsInChildren = g.GetComponentsInChildren<Renderer>();
			if (componentsInChildren.Length != 0)
			{
				Bounds bounds = componentsInChildren[0].bounds;
				Renderer[] array = componentsInChildren;
				foreach (Renderer renderer in array)
				{
					bounds.Encapsulate(renderer.bounds);
				}
				BoxCollider boxCollider = g.AddComponent<BoxCollider>();
				boxCollider.center = g.transform.InverseTransformPoint(bounds.center);
				boxCollider.size = Vector3.Max(g.transform.InverseTransformVector(bounds.size).Abs3(), Vector3.one * 0.02f);
			}
		}
		MATS.TryGetValue(mat, out var value);
		if (value.snd == null)
		{
			value = MATS["plastico"];
		}
		if (!pms.TryGetValue(mat, out var value2))
		{
			Dictionary<string, PhysicsMaterial> dictionary = pms;
			PhysicsMaterial obj = new PhysicsMaterial(mat)
			{
				dynamicFriction = value.dyn,
				staticFriction = value.sta,
				bounciness = value.bounce,
				bounceCombine = PhysicsMaterialCombine.Average,
				frictionCombine = PhysicsMaterialCombine.Average
			};
			value2 = obj;
			dictionary[mat] = obj;
		}
		Collider[] componentsInChildren2 = g.GetComponentsInChildren<Collider>();
		foreach (Collider obj2 in componentsInChildren2)
		{
			obj2.sharedMaterial = value2;
			if (obj2 is MeshCollider meshCollider)
			{
				meshCollider.convex = true;
			}
		}
		FBPropBody obj3 = g.GetComponent<FBPropBody>().OrNull() ?? g.AddComponent<FBPropBody>();
		obj3.mat = mat;
		obj3.snd = value.snd;
		obj3.rb = rigidbody;
		rigidbody.Sleep();
		return obj3;
	}

	public static void Watch(GameObject g, string mat, float kg)
	{
		if ((bool)g)
		{
			Rigidbody component = g.GetComponent<Rigidbody>();
			if (!component)
			{
				Make(g, mat, kg);
				return;
			}
			FBPropBody obj = g.GetComponent<FBPropBody>().OrNull() ?? g.AddComponent<FBPropBody>();
			obj.mat = mat;
			obj.snd = ((MATS.TryGetValue(mat, out var value) && value.snd != null) ? value.snd : mat);
			obj.rb = component;
		}
	}

	public static void Shot(Collider c, Vector3 p, Vector3 dir)
	{
		FBPropBody fBPropBody = (c ? c.GetComponentInParent<FBPropBody>() : null);
		if ((bool)fBPropBody && (bool)fBPropBody.rb && !fBPropBody.rb.isKinematic)
		{
			fBPropBody.rb.WakeUp();
			fBPropBody.rb.AddForceAtPosition(dir.normalized * Mathf.Clamp(fBPropBody.rb.mass * 3.5f, 2f, 40f), p, ForceMode.Impulse);
			Sound(fBPropBody.snd, p, 0.6f);
		}
	}

	public static Vector3 Abs3(this Vector3 v)
	{
		return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
	}

	public static float Knock(Collider c, Vector3 dir, float v, Vector3 at)
	{
		if (!c || (bool)c.attachedRigidbody)
		{
			return -1f;
		}
		Transform transform = c.transform;
		GameObject gameObject = null;
		(string, float, float, string) tuple = default((string, float, float, string));
		Transform transform2 = transform;
		while ((bool)transform2 && gameObject == null)
		{
			(string, float, float, string)[] fIXOS = FIXOS;
			for (int i = 0; i < fIXOS.Length; i++)
			{
				(string, float, float, string) tuple2 = fIXOS[i];
				if (transform2.name.StartsWith(tuple2.Item1))
				{
					gameObject = transform2.gameObject;
					tuple = tuple2;
					break;
				}
			}
			transform2 = transform2.parent;
		}
		if (!gameObject || v < tuple.Item2)
		{
			return -1f;
		}
		Transform[] componentsInChildren = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.layer = 0;
		}
		FBPropBody fBPropBody = Make(gameObject, tuple.Item4, tuple.Item3);
		fBPropBody.rb.WakeUp();
		fBPropBody.rb.linearVelocity = dir.normalized * v * Mathf.Clamp(1.4f - tuple.Item3 / 150f, 0.3f, 1.2f) + Vector3.up * 1.2f;
		fBPropBody.rb.AddForceAtPosition(dir.normalized * tuple.Item3 * v * 0.3f, gameObject.transform.position + Vector3.up * 1.6f, ForceMode.Impulse);
		Sound((tuple.Item4 == "metal") ? "metal" : "metal_pesado", at, 0.9f, 8f);
		FBFx.Sparks(at, -dir);
		Debris(gameObject, 60f);
		Knocked++;
		return Mathf.Clamp(tuple.Item3 / 400f, 0.05f, 0.45f);
	}

	public static void Debris(GameObject g, float life)
	{
		if (!g)
		{
			return;
		}
		debris.Add((g, Time.time + life));
		if (!dr)
		{
			GameObject gameObject = new GameObject("FB_Destrocos");
			Object.DontDestroyOnLoad(gameObject);
			dr = gameObject.AddComponent<DebrisRunner>();
		}
		while (debris.Count > 60)
		{
			if ((bool)debris[0].g)
			{
				Object.Destroy(debris[0].g);
			}
			debris.RemoveAt(0);
		}
	}
}
}
