using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBAmbientScenery : MonoBehaviour
{
	private class Rotor
	{
		public List<(Transform n, Quaternion q0)> nodes = new List<(Transform, Quaternion)>();

		public Vector3 axis;

		public float a;

		public Renderer disc;

		public Material mat;
	}

	public static FBAmbientScenery I;

	private GameObject heli;

	private GameObject cav;

	private Rotor main;

	private Rotor tail;

	private float rpm = 1f;

	private float angle;

	private float r;

	private float alt;

	private float R0 = 120f;

	private float A0 = 48f;

	private float t;

	private float fxT = 15f;

	private Vector3 center;

	private Light search;

	private Light red;

	private Light blue;

	private bool crash;

	private float crashVy;

	private float strobe;

	private Bounds heliBox;

	private float cavDeadUntil;

	public bool HeliUp
	{
		get
		{
			if ((bool)heli)
			{
				return !crash;
			}
			return false;
		}
	}

	public bool CavUp
	{
		get
		{
			if ((bool)cav)
			{
				return Time.time > cavDeadUntil;
			}
			return false;
		}
	}

	public Transform Heli
	{
		get
		{
			if (!heli)
			{
				return null;
			}
			return heli.transform;
		}
	}

	public Transform Cav
	{
		get
		{
			if (!cav)
			{
				return null;
			}
			return cav.transform;
		}
	}

	public static void Spawn(FBMatch G)
	{
		I = new GameObject("FB_CenarioVivo").AddComponent<FBAmbientScenery>();
		I.Build(G);
	}

	private void Build(FBMatch G)
	{
		MapDef map = G.Map;
		List<Vector3> list = map.spawnA.Concat(map.spawnB).Concat(map.points).ToList();
		center = ((list.Count > 0) ? (list.Aggregate(Vector3.zero, (Vector3 a, Vector3 b) => a + b) / list.Count) : Vector3.zero);
		center.y = 0f;
		R0 = 100 + Mathf.Min(45, list.Count * 2);
		r = R0;
		alt = A0;
		angle = UnityEngine.Random.value * (float)Math.PI * 2f;
		SpawnHeli();
		if (G.mapId != "estande")
		{
			SpawnCaveirao(G);
		}
	}

	private void SpawnHeli()
	{
		GameObject gameObject = FBRegistry.I.Vehicle("heli");
		if (!gameObject)
		{
			return;
		}
		crash = false;
		crashVy = 0f;
		heli = UnityEngine.Object.Instantiate(gameObject);
		heli.name = "Helicoptero";
		Collider[] componentsInChildren = heli.GetComponentsInChildren<Collider>();
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			UnityEngine.Object.Destroy(componentsInChildren[i]);
		}
		heliBox = new Bounds(heli.transform.position, Vector3.zero);
		bool flag = false;
		Renderer[] componentsInChildren2 = heli.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren2)
		{
			bool flag2 = false;
			Transform parent = renderer.transform;
			while ((bool)parent && parent != heli.transform)
			{
				if (parent.name.StartsWith("Helices") || parent.name.StartsWith("Rotor"))
				{
					flag2 = true;
					break;
				}
				parent = parent.parent;
			}
			if (!flag2)
			{
				if (!flag)
				{
					heliBox = renderer.bounds;
					flag = true;
				}
				else
				{
					heliBox.Encapsulate(renderer.bounds);
				}
			}
		}
		heliBox.center -= heli.transform.position;
		main = MakeRotor(new string[2] { "Helices", "Rotor" }, mainR: true);
		tail = MakeRotor(new string[2] { "Helices-02", "Rotor-02" }, mainR: false);
		GameObject gameObject2 = new GameObject("Farol");
		gameObject2.transform.SetParent(heli.transform, worldPositionStays: false);
		gameObject2.transform.localPosition = new Vector3(0f, 1.2f, 3.5f);
		search = gameObject2.AddComponent<Light>();
		search.type = LightType.Spot;
		search.range = 140f;
		search.spotAngle = 45f;
		search.intensity = 30f;
		search.color = new Color(1f, 0.93f, 0.87f);
		search.shadows = LightShadows.None;
	}

	private Rotor MakeRotor(string[] names, bool mainR)
	{
		List<Transform> list = (from t in heli.GetComponentsInChildren<Transform>(includeInactive: true)
			where names.Contains(t.name)
			select t).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		Rotor rotor = new Rotor
		{
			axis = (mainR ? Vector3.up : Vector3.right)
		};
		foreach (Transform item in list)
		{
			rotor.nodes.Add((item, item.localRotation));
		}
		Transform transform = list[0];
		Bounds bounds = new Bounds(transform.position, Vector3.zero);
		Renderer[] componentsInChildren = transform.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		float num = (mainR ? (Mathf.Max(bounds.size.x, bounds.size.z) / 2f) : (Mathf.Max(bounds.size.y, bounds.size.z) / 2f));
		GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
		UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
		gameObject.name = "DiscoBorrao";
		gameObject.transform.SetParent(transform.parent, worldPositionStays: true);
		gameObject.transform.position = bounds.center;
		gameObject.transform.rotation = transform.parent.rotation * (mainR ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f));
		gameObject.transform.localScale = Vector3.one;
		Vector3 lossyScale = gameObject.transform.lossyScale;
		gameObject.transform.localScale = new Vector3(num * 2f / lossyScale.x, 0.002f / lossyScale.y, num * 2f / lossyScale.z);
		rotor.mat = new Material(FBShaders.Unlit);
		rotor.mat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f, 0f));
		rotor.mat.SetFloat("_Surface", 1f);
		rotor.mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		rotor.mat.SetOverrideTag("RenderType", "Transparent");
		if (rotor.mat.renderQueue < 3000)
		{
			rotor.mat.renderQueue = 3000;
		}
		rotor.mat.SetInt("_SrcBlend", 5);
		rotor.mat.SetInt("_DstBlend", 10);
		rotor.mat.SetFloat("_ZWrite", 0f);
		rotor.mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		rotor.mat.renderQueue = 3050;
		rotor.disc = gameObject.GetComponent<Renderer>();
		rotor.disc.sharedMaterial = rotor.mat;
		rotor.disc.shadowCastingMode = ShadowCastingMode.Off;
		return rotor;
	}

	private void Spin(Rotor X, float w, float dt)
	{
		if (X == null)
		{
			return;
		}
		X.a = (X.a + w * rpm * dt) % ((float)Math.PI * 2f);
		foreach (var (transform, quaternion) in X.nodes)
		{
			if ((bool)transform)
			{
				transform.localRotation = Quaternion.AngleAxis(X.a * 57.29578f, X.axis) * quaternion;
			}
		}
		float num = Mathf.Clamp01((rpm - 0.3f) / 0.6f) * 0.55f;
		X.mat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.15f, num));
		X.disc.enabled = num > 0.02f;
	}

	private void SpawnCaveirao(FBMatch G)
	{
		bool flag = G.mapId == "vila_jardim" && (bool)FBRegistry.I.Vehicle("caveirao_bope");
		GameObject gameObject = (flag ? FBRegistry.I.Vehicle("caveirao_bope") : FBRegistry.I.Vehicle("caveirao"));
		if ((bool)gameObject)
		{
			cav = UnityEngine.Object.Instantiate(gameObject);
			cav.name = "Caveirao";
			Collider[] componentsInChildren = cav.GetComponentsInChildren<Collider>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				UnityEngine.Object.Destroy(componentsInChildren[i]);
			}
			float num = 0f;
			Dictionary<string, object> dictionary = Json.O(G.Level, "caveirao");
			Vector3 position;
			if (flag && dictionary != null)
			{
				position = Coords.FromWeb(Json.L(dictionary, "p"));
				num = Coords.YawFromWeb(Json.F(dictionary, "yaw"));
				cav.transform.localScale = Vector3.one * 1.2f;
			}
			else if (G.Map.spawnA.Count > 0)
			{
				position = G.Map.spawnA[0] + new Vector3(6.5f, 0f, -4.5f);
				num = -60f;
			}
			else
			{
				position = ((G.Map.waypoints.Count > 0) ? G.Map.waypoints[0] : Vector3.zero);
			}
			float? num2 = FBWorld.GroundY(position.x, position.y + 2f, position.z, 0.3f, 5f);
			if (num2.HasValue)
			{
				float valueOrDefault = num2.GetValueOrDefault();
				position.y = valueOrDefault;
			}
			cav.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, num + (float)(flag ? (-90) : 180), 0f));
			Bounds bounds = new Bounds(cav.transform.position, Vector3.zero);
			Renderer[] componentsInChildren2 = cav.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in componentsInChildren2)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			BoxCollider boxCollider = cav.AddComponent<BoxCollider>();
			boxCollider.center = cav.transform.InverseTransformPoint(bounds.center);
			Vector3 vector = cav.transform.InverseTransformVector(bounds.size);
			boxCollider.size = new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z)) * 0.96f;
			cav.layer = 11;
			float y = bounds.max.y - cav.transform.position.y;
			red = Giro(new Vector3(0.45f, y, 0f), new Color(1f, 0.07f, 0f));
			blue = Giro(new Vector3(-0.45f, y, 0f), new Color(0.07f, 0.4f, 1f));
		}
	}

	private Light Giro(Vector3 local, Color c)
	{
		GameObject obj = new GameObject("Giroflex");
		obj.transform.SetParent(cav.transform, worldPositionStays: false);
		obj.transform.position = cav.transform.position + cav.transform.rotation * local;
		Light light = obj.AddComponent<Light>();
		light.type = LightType.Point;
		light.range = 24f;
		light.color = c;
		light.shadows = LightShadows.None;
		return light;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBAmbientScenery.Update");

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
		float deltaTime = Time.deltaTime;
		t += deltaTime;
		if ((bool)cav)
		{
			strobe += deltaTime * 12f;
			bool flag = Mathf.Sin(strobe) > 0f;
			if ((bool)red)
			{
				red.intensity = (flag ? 3.5f : 0.1f);
			}
			if ((bool)blue)
			{
				blue.intensity = (flag ? 0.1f : 3.5f);
			}
		}
		if (!heli)
		{
			return;
		}
		Spin(main, 34f, deltaTime);
		Spin(tail, 52f, deltaTime);
		if (crash)
		{
			rpm += (0.55f - rpm) * (1f - Mathf.Exp((0f - deltaTime) * 2f));
			crashVy = Mathf.Min(24f, crashVy + 11f * deltaTime);
			heli.transform.position += Vector3.down * crashVy * deltaTime;
			heli.transform.Rotate(0f, 200f * deltaTime, 0f, Space.World);
			FBHeliSound fBHeliSound = FBHeliSound.Attach(heli, 1f);
			if ((bool)fBHeliSound)
			{
				fBHeliSound.rpm = rpm;
			}
			if (!FBWorld.GroundY(heli.transform.position.x, heli.transform.position.y + 1f, heli.transform.position.z, 0.3f).HasValue && !(heli.transform.position.y < -20f))
			{
				return;
			}
			FBKillstreak2.Boom(heli.transform.position);
			if (FBKillstreak2.gunner != null && FBKillstreak2.gunner.kind == "heli")
			{
				FBKillstreak2.GunnerDie();
			}
			FBAudio.StopLoop("ambient_heli");
			UnityEngine.Object.Destroy(heli);
			heli = null;
			FBMatch.I?.Invoke2(45f, delegate
			{
				if ((bool)this && !heli)
				{
					SpawnHeli();
				}
			});
		}
		else
		{
			rpm += (1f - rpm) * (1f - Mathf.Exp((0f - deltaTime) * 2f));
			bool flag2 = FBKillstreak2.gunner != null && FBKillstreak2.gunner.kind == "heli";
			float num = 1f - Mathf.Exp((0f - deltaTime) * 0.6f);
			r += ((flag2 ? 46f : R0) - r) * num;
			alt += ((flag2 ? 30f : A0) - alt) * num;
			angle += (flag2 ? (14f / r) : 0.16f) * deltaTime;
			float y = alt + Mathf.Sin(t * 0.4f) * (flag2 ? 1.2f : 3.2f);
			heli.transform.position = new Vector3(center.x + Mathf.Cos(angle) * r, y, center.z + Mathf.Sin(angle) * r);
			Vector3 forward = new Vector3(0f - Mathf.Sin(angle), 0f, Mathf.Cos(angle));
			heli.transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(3f, 0f, 10f);
			FBHeliSound fBHeliSound2 = FBHeliSound.Attach(heli, 0.9f);
			if ((bool)fBHeliSound2)
			{
				fBHeliSound2.rpm = rpm;
			}
			if ((fxT -= deltaTime) <= 0f)
			{
				fxT = UnityEngine.Random.Range(18f, 30f);
				FBAudio.PlayAt("ie_heli_fx", heli.transform.position, 0.35f, 60f, 0.08f, 0f, 0.4f);
			}
			if ((bool)search)
			{
				search.transform.rotation = Quaternion.LookRotation(center + new Vector3(Mathf.Sin(t * 0.5f) * 16f, 2f, Mathf.Cos(t * 0.5f) * 16f) - search.transform.position);
			}
		}
	}

	public void ShootDown()
	{
		crash = true;
	}

	public void KillCav()
	{
		cavDeadUntil = Time.time + FBKillstreak.STREAK["caveirao"].run;
	}

	public Vector3 GunnerSeat(string kind)
	{
		if (kind == "heli" && (bool)heli)
		{
			return heli.transform.TransformPoint(heliBox.min.x + 0.55f, heliBox.min.y + 0.45f, heliBox.max.z - 2.6f);
		}
		if ((bool)cav)
		{
			BoxCollider component = cav.GetComponent<BoxCollider>();
			return cav.transform.TransformPoint(component.center + new Vector3(0f, component.size.y / 2f - 0.2f, 0.3f));
		}
		return Vector3.zero;
	}

	private void OnDestroy()
	{
		FBAudio.StopLoop("ambient_heli");
		if ((bool)heli)
		{
			UnityEngine.Object.Destroy(heli);
		}
		if ((bool)cav)
		{
			UnityEngine.Object.Destroy(cav);
		}
	}
}
}
