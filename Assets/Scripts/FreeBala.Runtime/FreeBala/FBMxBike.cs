using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public class FBMxBike : MonoBehaviour
{
	private class MxSound
	{
		private readonly FBMxBike m;

		private AudioSource low;

		private AudioSource high;

		private AudioSource idle;

		private AudioSource off;

		private AudioSource rpmup;

		private AudioSource rev;

		private AudioClip cOff;

		private AudioClip cUp;

		public bool On;

		public float OffMix;

		public float HighMix;

		private float lowVol;

		private float highVol;

		private float idleVol;

		private float offtolow;

		private float rpmupToHigh;

		private float loopstorevving;

		private readonly float mUp;

		private readonly float mOff;

		private readonly float mLow;

		private readonly float mIdle;

		private readonly float pUp;

		private readonly float pDown;

		private readonly float tHigh;

		private readonly float tOff;

		private readonly AnimationCurve fadeIN;

		private readonly AnimationCurve fadeOUT;

		private const float VOL = 0.8f;

		public MxSound(FBMxBike m)
		{
			this.m = m;
			Dictionary<string, object> o = Json.O(m.M, "sound");
			mUp = Json.F(o, "rpmupVolumeMultiplier", 1f);
			mOff = Json.F(o, "offVolumeMultiplier", 1f);
			mLow = Json.F(o, "lowVolumeMultiplier", 1f);
			mIdle = Json.F(o, "idleVolumeMultiplier", 1f);
			pUp = Json.F(o, "low_pitch_up", 1f);
			pDown = Json.F(o, "low_pitch_down", 1f);
			tHigh = Json.F(o, "rpmhightransitiontime", 1f);
			tOff = Json.F(o, "offtolowtransitiontime", 1f);
			fadeIN = Curve(Json.Get(o, "fadeIN"));
			fadeOUT = Curve(Json.Get(o, "fadeOUT"));
			if (fadeIN.length == 0)
			{
				fadeIN = AnimationCurve.Linear(0f, 0f, 1f, 1f);
			}
			if (fadeOUT.length == 0)
			{
				fadeOUT = AnimationCurve.Linear(0f, 1f, 1f, 0f);
			}
		}

		private AudioSource Src(string role)
		{
			AudioClip clip = FBAudio.Clip("mx_" + m.vid + "_" + role);
			GameObject gameObject = new GameObject("som_" + role);
			gameObject.transform.SetParent(m.transform, worldPositionStays: false);
			gameObject.transform.localPosition = new Vector3(0f, 0.5f, -0.3f);
			AudioSource audioSource = gameObject.AddComponent<AudioSource>();
			audioSource.clip = clip;
			audioSource.loop = true;
			audioSource.playOnAwake = false;
			audioSource.spatialBlend = 1f;
			audioSource.dopplerLevel = 0f;
			audioSource.volume = 0f;
			audioSource.priority = 40;
			audioSource.rolloffMode = AudioRolloffMode.Custom;
			audioSource.maxDistance = 400f;
			audioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.Rolloff(6f, 1.05f));
			audioSource.outputAudioMixerGroup = FBAudio.Group(voice: false);
			return audioSource;
		}

		public void Start()
		{
			if (On)
			{
				return;
			}
			On = true;
			if (!low)
			{
				low = Src("low");
				high = Src("high");
				idle = Src("idle");
				off = Src("off");
				rpmup = Src("rpmup");
				rev = Src("revving");
				cOff = off.clip;
				cUp = rpmup.clip;
			}
			AudioSource[] array = new AudioSource[6] { low, high, idle, off, rpmup, rev };
			foreach (AudioSource audioSource in array)
			{
				if ((bool)audioSource && (bool)audioSource.clip)
				{
					audioSource.volume = 0f;
					audioSource.Play();
				}
			}
			lowVol = 1f;
			highVol = 0f;
			offtolow = (cOff ? cOff.length : 1f);
			rpmupToHigh = 0f;
			loopstorevving = 0f;
		}

		public void Stop()
		{
			On = false;
			AudioSource[] array = new AudioSource[6] { low, high, idle, off, rpmup, rev };
			foreach (AudioSource audioSource in array)
			{
				if ((bool)audioSource)
				{
					audioSource.Stop();
				}
			}
		}

		public void Update(float dt)
		{
			if (!On || !low)
			{
				return;
			}
			if (m.in_revving > 0.5f)
			{
				loopstorevving = Mathf.Clamp01(loopstorevving + dt * 4f);
				rev.volume = loopstorevving * 0.8f;
				float volume = (1f - loopstorevving) * 0.8f;
				rpmup.volume = volume;
				low.volume = volume;
				high.volume = volume;
				idle.volume = volume;
				off.volume = volume;
				return;
			}
			loopstorevving = Mathf.Clamp01(loopstorevving - dt * 4f);
			rev.volume = loopstorevving * 0.8f;
			if (loopstorevving <= 0f)
			{
				rev.time = 0f;
			}
			float rpmN = m.rpmN;
			idleVol = Mathf.Clamp01((m.bikeSpeed - 5f) / -5f);
			float num = (cOff ? cOff.length : 1f);
			float num2 = (cUp ? cUp.length : 1f);
			if (m.in_acceleration > 0.05f)
			{
				highVol = Mathf.Clamp01(highVol + dt * 16f);
				lowVol = Mathf.Clamp01(lowVol - dt * 16f);
				offtolow = num;
				rpmupToHigh = Mathf.Clamp(rpmupToHigh + dt * tHigh, 0f, num2);
				off.time = 0f;
			}
			else
			{
				rpmup.time = 0f;
				highVol = Mathf.Clamp01(highVol - dt * 16f);
				lowVol = Mathf.Clamp01(lowVol + dt * 16f);
				offtolow = Mathf.Clamp(offtolow - dt * tOff, 0f, num);
				rpmupToHigh = 0f;
			}
			float time = Mathf.Clamp01(rpmupToHigh / num2);
			float num3 = Mathf.Clamp01((rpmN - 0.6f) / 0.35f);
			float num4 = num3 * 0.8f + 0.2f;
			high.volume = highVol * num4 * fadeIN.Evaluate(time) * 0.8f;
			high.pitch = num3 * 0.2f + 0.8f;
			rpmup.volume = num4 * highVol * fadeOUT.Evaluate(time) * mUp * 0.8f;
			rpmup.pitch = high.pitch;
			float time2 = Mathf.Clamp01(offtolow / num);
			low.volume = lowVol * fadeOUT.Evaluate(time2) * (1f - idleVol) * mLow * 0.8f;
			low.pitch = pDown + Mathf.Clamp01(rpmN) * (pUp - pDown);
			idle.volume = mIdle * idleVol * 0.8f;
			off.volume = lowVol * fadeIN.Evaluate(time2) * (1f - idleVol) * mOff * 0.8f;
			off.pitch = ((rpmN >= 0f) ? (Mathf.Min(rpmN, 1f) * 0.2f + 1f) : 1f);
			if (SomPiso)
			{
				float num5 = low.volume + off.volume;
				float num6 = (1f - idleVol) * lowVol * 0.38f * 0.8f;
				if (num5 < num6)
				{
					if (num5 > 0.001f)
					{
						float num7 = Mathf.Min(num6 / num5, 8f);
						low.volume *= num7;
						off.volume *= num7;
					}
					else
					{
						low.volume = num6;
					}
				}
				idle.volume = Mathf.Max(mIdle, 0.45f) * idleVol * 0.8f;
			}
			OffMix = low.volume + off.volume + idle.volume;
			HighMix = high.volume + rpmup.volume;
		}

		public void Blip()
		{
			if ((bool)rev && (bool)rev.clip)
			{
				loopstorevving = 1f;
				rev.time = 0f;
			}
		}
	}

	public enum St
	{
		Idle,
		Reversing,
		Riding,
		RidingStanding,
		Wheelie,
		WheelieStanding,
		TrickA,
		TrickB
	}

	public struct Pose
	{
		public Vector3 hips;

		public Vector3 lhand;

		public Vector3 rhand;

		public Vector3 lhh;

		public Vector3 rhh;

		public Vector3 lfoot;

		public Vector3 rfoot;

		public Vector3 lfh;

		public Vector3 rfh;

		public float pitch;
	}

	private static Dictionary<string, object> db;

	public static readonly string[] IDS = new string[11]
	{
		"pop110", "titan160", "cg150", "moby", "xre300", "xt660", "gs310", "xj6", "mt09", "at1100",
		"gs1250"
	};

	public const float S = 1.2f;

	public const float APK_DT = 0.012f;

	public const float APK_G = 10f;

	private static int activeCount;

	private static float savedDt = -1f;

	public FBCar car;

	public string vid;

	private Dictionary<string, object> M;

	private Dictionary<string, object> P;

	private Transform t;

	private Transform tfw;

	private Transform trw;

	private Transform tchassis;

	private Transform fwmesh;

	private Transform rwmesh;

	private Transform handlemesh;

	private Transform pivothandle;

	public Transform gripL;

	public Transform gripR;

	private Rigidbody rb;

	private Rigidbody fwrb;

	private Rigidbody rwrb;

	private ConfigurableJoint fwjoint;

	private readonly List<Rigidbody> bodies = new List<Rigidbody>();

	private readonly List<Collider> cols = new List<Collider>();

	private readonly List<(Transform tr, Vector3 p, Quaternion r)> rest = new List<(Transform, Vector3, Quaternion)>();

	private float maxspeed;

	private float torque;

	private float reverseSpeed;

	private float brakePower;

	private float wbrakePower;

	private float balanceForce;

	private float wheeliePowerB;

	private float wheelieDrag;

	private float scrapeAngle;

	private float fallAngle;

	private float lookAngle;

	private Vector2 fwgrip;

	private Vector2 rwgrip;

	private Vector2 fwgrip_def;

	private Vector2 rwgrip_def;

	private AnimationCurve steerAngle;

	private AnimationCurve leanCurve;

	private AnimationCurve acelcv;

	private int gearcount;

	private float[] gears;

	private float fwheelRadius;

	private float rwheelRadius;

	private float distWheelHandle;

	private float weight = 120f;

	private float steervel;

	private Vector3 handleLocPos;

	public float in_steering;

	public float in_acceleration;

	public float in_revving;

	public float smtThrottle;

	public float speedKmh;

	public float rpmN;

	public float bikeSpeed;

	public float gearRatio;

	public float leanAngle;

	public float wheelieAngle;

	public float wheelieDir = 1f;

	public float lookDir = 1f;

	public float targetLeanWheelie;

	public float leanWheelieAngle;

	public float wheelieMod45;

	public float wheelieMod90;

	public float wheelieMod4590;

	public float wheelieMod3060;

	public float speedMod10;

	public float speedMod40;

	public float pickUpSpeed;

	public float smtHandleRot;

	public float wheeliePowerA;

	public bool in_wheelie;

	public bool onWheelie;

	public bool isReversing;

	public bool crashed;

	public bool active;

	public bool changeGear = true;

	public int currentGear;

	public int currGear;

	public int prevGear;

	public int visualGear;

	public float vmag;

	private Vector3 lastVelocity;

	private float gearWaitT;

	public float crashT;

	public bool rabeta;

	public bool standing;

	public bool trickA;

	public bool trickB;

	public static bool GrauAssist = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbgrauapk") < 0;

	public static bool AntiHop = true;

	public static bool SomPiso = true;

	private float prevWA;

	public float hopVy;

	public float spinF;

	public float spinR;

	public float spinExp;

	private float angF;

	private float angR;

	private float remT = -9f;

	private Quaternion fwRest;

	private Quaternion rwRest;

	private Quaternion rwRestC;

	private Quaternion lastF;

	private Quaternion lastR;

	private bool restOk;

	public static float SpinCapDeg = 16f;

	private static readonly Dictionary<string, PhysicsMaterial> mats = new Dictionary<string, PhysicsMaterial>();

	private static float camAng;

	private static float camAngV;

	private static float orbit;

	private static float orbitV;

	private static float orbitT;

	private static float sway;

	private static float lastCamYaw = 0f / 0f;

	private static FBMxBike camBike;

	private static readonly Color[] TINTAS = new Color[10]
	{
		new Color(0.62f, 0.04f, 0.04f),
		new Color(0.03f, 0.03f, 0.035f),
		new Color(0.86f, 0.86f, 0.84f),
		new Color(0.05f, 0.14f, 0.5f),
		new Color(0.55f, 0.56f, 0.58f),
		new Color(0.85f, 0.62f, 0.04f),
		new Color(0.16f, 0.17f, 0.18f),
		new Color(0.45f, 0.02f, 0.02f),
		new Color(0.08f, 0.3f, 0.12f),
		new Color(0.9f, 0.32f, 0.03f)
	};

	private static readonly float[] PESO = new float[10] { 3.2f, 3f, 2.4f, 1.6f, 1.6f, 0.7f, 1.4f, 1f, 0.5f, 0.4f };

	public int Painted;

	public Color PaintColor;

	private MxSound sound;

	private Vector3 remPrev;

	private bool remInit;

	private Pose[] poses;

	private Pose cur;

	private bool curInit;

	public St state;

	private float stateT;

	private Vector3 pmPos;

	private Vector3 pmVel;

	private bool pmInit;

	private Vector3 pmTgt;

	private Vector3 pmTgtVel;

	public static int Recovered;

	public static Dictionary<string, object> DB
	{
		get
		{
			if (db == null)
			{
				TextAsset textAsset = Resources.Load<TextAsset>("FBMx/mx_bikes");
				db = (textAsset ? ((Json.Parse(textAsset.text) as Dictionary<string, object>) ?? new Dictionary<string, object>()) : new Dictionary<string, object>());
			}
			return db;
		}
	}

	public string WheelDiag => $"rodas: visível D {spinF:0} T {spinR:0} °/s (teto {SpinCapDeg:0}°/quadro) × real {spinExp:0} °/s · traseira filha da física {(bool)rwmesh && (bool)trw && rwmesh.IsChildOf(trw)}";

	public float Rpm => rpmN;

	public string GearLabel
	{
		get
		{
			if (!(bikeSpeed < -1f))
			{
				return (visualGear + 1).ToString();
			}
			return "R";
		}
	}

	public float MaxLeanDeg
	{
		get
		{
			if (leanCurve == null)
			{
				return 45f;
			}
			return Mathf.Abs(leanCurve.Evaluate(1f));
		}
	}

	public float Wheelbase
	{
		get
		{
			if (!tfw || !trw)
			{
				return 1.6800001f;
			}
			return Vector3.Distance(tfw.position, trw.position);
		}
	}

	public string SoundDiag
	{
		get
		{
			if (sound != null)
			{
				return $"som: sem acelerar {sound.OffMix:0.00} · acelerando {sound.HighMix:0.00}";
			}
			return "-";
		}
	}

	public Pose RidingPose
	{
		get
		{
			if (poses == null)
			{
				return PoseOf(2);
			}
			return poses[2];
		}
	}

	public Vector3 CurHipsW
	{
		get
		{
			Transform obj = base.transform;
			Vector3 hips;
			if (!curInit)
			{
				Pose obj2 = ((poses != null) ? poses[0] : PoseOf(0));
				hips = obj2.hips;
			}
			else
			{
				hips = cur.hips;
			}
			return obj.TransformPoint(hips);
		}
	}

	public event Action<string> OnCrash;

	public static bool Has(string vid)
	{
		if (DB.ContainsKey(vid) && (bool)FBRegistry.I)
		{
			return FBRegistry.I.Vehicle("mx_" + vid);
		}
		return false;
	}

	private static Transform Find(Transform r, string n)
	{
		if (string.IsNullOrEmpty(n))
		{
			return null;
		}
		Transform[] componentsInChildren = r.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform in componentsInChildren)
		{
			if (transform.name == n)
			{
				return transform;
			}
		}
		return null;
	}

	private static Vector3 V3(object o)
	{
		if (o is List<object> { Count: >=3 } list)
		{
			return new Vector3(Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]));
		}
		if (!(o is Dictionary<string, object> o2))
		{
			return Vector3.zero;
		}
		return new Vector3(Json.F(o2, "x"), Json.F(o2, "y"), Json.F(o2, "z"));
	}

	private static Quaternion Q4(object o)
	{
		if (o is Dictionary<string, object> o2)
		{
			return new Quaternion(Json.F(o2, "x"), Json.F(o2, "y"), Json.F(o2, "z"), Json.F(o2, "w", 1f));
		}
		if (!(o is List<object> { Count: >=4 } list))
		{
			return Quaternion.identity;
		}
		return new Quaternion(Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]), Json.Fv(list[3]));
	}

	private static Vector2 V2(object o)
	{
		if (!(o is Dictionary<string, object> o2))
		{
			return Vector2.zero;
		}
		return new Vector2(Json.F(o2, "x"), Json.F(o2, "y"));
	}

	public static AnimationCurve Curve(object o)
	{
		List<object> list = Json.L(o, "m_Curve");
		AnimationCurve animationCurve = new AnimationCurve();
		if (list == null)
		{
			return animationCurve;
		}
		foreach (object item in list)
		{
			animationCurve.AddKey(new Keyframe(Json.F(item, "time"), Json.F(item, "value"), Json.F(item, "inSlope"), Json.F(item, "outSlope")));
		}
		return animationCurve;
	}

	public static FBMxBike Attach(FBCar car, string vid, Transform model)
	{
		FBMxBike fBMxBike = car.gameObject.AddComponent<FBMxBike>();
		fBMxBike.car = car;
		fBMxBike.vid = vid;
		fBMxBike.M = Json.O(DB, vid);
		fBMxBike.P = Json.O(fBMxBike.M, "params");
		fBMxBike.Build(model);
		return fBMxBike;
	}

	private void Build(Transform model)
	{
		if ((bool)FBRegistry.I)
		{
			MeshFilter[] componentsInChildren = model.GetComponentsInChildren<MeshFilter>(includeInactive: true);
			foreach (MeshFilter meshFilter in componentsInChildren)
			{
				meshFilter.sharedMesh = FBRegistry.I.LodOf(meshFilter.sharedMesh);
			}
		}
		t = base.transform;
		Dictionary<string, object> o = Json.O(M, "roles");
		tfw = Find(model, Json.S(o, "tfw"));
		trw = Find(model, Json.S(o, "trw"));
		tchassis = Find(model, Json.S(o, "tchassis")) ?? t;
		fwmesh = Find(model, Json.S(o, "fwmesh"));
		rwmesh = Find(model, Json.S(o, "rwmesh"));
		handlemesh = Find(model, Json.S(o, "handlemesh"));
		pivothandle = Find(model, Json.S(o, "pivothandle"));
		gripL = Find(model, "gripL");
		gripR = Find(model, "gripR");
		maxspeed = Json.F(P, "maxspeed", 120f);
		weight = Json.F(P, "weight", 120f);
		torque = Json.F(P, "torque", 10f);
		reverseSpeed = Json.F(P, "reverseSpeed", 1f);
		brakePower = Json.F(P, "brakePower", 20f);
		wbrakePower = Json.F(P, "wbrakePower", 25f);
		balanceForce = Json.F(P, "balanceForce", 5f);
		wheeliePowerB = Json.F(P, "wheeliePowerB", 40f);
		wheelieDrag = Json.F(P, "wheelieDrag", 8f);
		scrapeAngle = Json.F(P, "scrapeAngle", 80f);
		fallAngle = Json.F(P, "fallAngle", 96f);
		lookAngle = Json.F(P, "lookAngle", 30f);
		fwgrip = (fwgrip_def = V2(Json.Get(P, "fwgrip")));
		rwgrip = (rwgrip_def = V2(Json.Get(P, "rwgrip")));
		steerAngle = Curve(Json.Get(P, "steerAngle"));
		leanCurve = Curve(Json.Get(P, "leanCurve"));
		acelcv = Curve(Json.Get(P, "acelcv"));
		gearcount = Mathf.Max(1, Json.I(P, "gearcount", 5));
		gears = new float[gearcount];
		for (int j = 0; j < gearcount; j++)
		{
			gears[j] = Mathf.Floor((float)(j + 1) / (float)gearcount * maxspeed);
		}
		string text = Json.S(M, "apk");
		Dictionary<string, Rigidbody> dictionary = new Dictionary<string, Rigidbody>();
		foreach (object item in Json.L(M, "physics") ?? new List<object>())
		{
			if (!Json.B(item, "active", def: true))
			{
				continue;
			}
			string text2 = Json.S(item, "name");
			Transform transform = ((text2 == text) ? t : Find(model, text2));
			if (!transform)
			{
				continue;
			}
			if (transform != t)
			{
				rest.Add((transform, transform.localPosition, transform.localRotation));
			}
			foreach (object item2 in Json.L(item, "comps") ?? new List<object>())
			{
				switch (Json.S(item2, "type"))
				{
				case "Rigidbody":
				{
					Rigidbody rigidbody = transform.GetComponent<Rigidbody>().OrNull() ?? transform.gameObject.AddComponent<Rigidbody>();
					rigidbody.mass = Json.F(item2, "Mass", 1f);
					rigidbody.linearDamping = Json.F(item2, "Drag");
					rigidbody.angularDamping = Json.F(item2, "AngularDrag", 0.05f);
					rigidbody.useGravity = Json.B(item2, "UseGravity", def: true);
					rigidbody.isKinematic = true;
					rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
					rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
					dictionary[text2] = rigidbody;
					bodies.Add(rigidbody);
					break;
				}
				case "SphereCollider":
				{
					SphereCollider sphereCollider = transform.gameObject.AddComponent<SphereCollider>();
					sphereCollider.radius = Json.F(item2, "Radius", 0.3f);
					sphereCollider.center = V3(Json.Get(item2, "Center"));
					Mat(sphereCollider, Json.O(item2, "mat"));
					cols.Add(sphereCollider);
					break;
				}
				case "CapsuleCollider":
				{
					CapsuleCollider capsuleCollider = transform.gameObject.AddComponent<CapsuleCollider>();
					capsuleCollider.radius = Json.F(item2, "Radius", 0.2f);
					capsuleCollider.height = Json.F(item2, "Height", 1f);
					capsuleCollider.direction = Json.I(item2, "Direction", 1);
					capsuleCollider.center = V3(Json.Get(item2, "Center"));
					capsuleCollider.isTrigger = Json.B(item2, "IsTrigger");
					Mat(capsuleCollider, Json.O(item2, "mat"));
					cols.Add(capsuleCollider);
					break;
				}
				case "BoxCollider":
				{
					BoxCollider boxCollider = transform.gameObject.AddComponent<BoxCollider>();
					boxCollider.size = V3(Json.Get(item2, "Size"));
					boxCollider.center = V3(Json.Get(item2, "Center"));
					Mat(boxCollider, Json.O(item2, "mat"));
					cols.Add(boxCollider);
					break;
				}
				}
			}
		}
		rb = (dictionary.TryGetValue(text, out var value) ? value : t.GetComponent<Rigidbody>());
		fwrb = (tfw ? tfw.GetComponent<Rigidbody>() : null);
		rwrb = (trw ? trw.GetComponent<Rigidbody>() : null);
		foreach (object item3 in Json.L(M, "physics") ?? new List<object>())
		{
			if (!Json.B(item3, "active", def: true))
			{
				continue;
			}
			string text3 = Json.S(item3, "name");
			Transform transform2 = ((text3 == text) ? t : Find(model, text3));
			if (!transform2)
			{
				continue;
			}
			foreach (object item4 in Json.L(item3, "comps") ?? new List<object>())
			{
				if (Json.S(item4, "type") == "ConfigurableJoint")
				{
					Joint(transform2, item4, dictionary);
				}
			}
		}
		fwjoint = (tfw ? tfw.GetComponent<ConfigurableJoint>() : null);
		if ((bool)rb)
		{
			rb.maxAngularVelocity = 10f;
			rb.solverVelocityIterations = 10;
		}
		Rigidbody[] array = new Rigidbody[2] { fwrb, rwrb };
		foreach (Rigidbody rigidbody2 in array)
		{
			if ((bool)rigidbody2)
			{
				rigidbody2.maxAngularVelocity = 300f;
				rigidbody2.solverIterations = 20;
			}
		}
		ConfigurableJoint[] componentsInChildren2 = GetComponentsInChildren<ConfigurableJoint>(includeInactive: true);
		foreach (ConfigurableJoint configurableJoint in componentsInChildren2)
		{
			if ((bool)configurableJoint.connectedBody && configurableJoint.connectedBody != rb)
			{
				configurableJoint.connectedBody.solverIterations = 20;
			}
		}
		Collider[] components = t.GetComponents<Collider>();
		foreach (Collider collider in components)
		{
			Transform[] array2 = new Transform[2] { tfw, trw };
			foreach (Transform transform3 in array2)
			{
				if ((bool)transform3)
				{
					Collider[] components2 = transform3.GetComponents<Collider>();
					foreach (Collider collider2 in components2)
					{
						Physics.IgnoreCollision(collider, collider2);
					}
				}
			}
		}
		foreach (Collider col in cols)
		{
			col.contactOffset = 0.0012f;
		}
		foreach (Rigidbody body in bodies)
		{
			body.maxDepenetrationVelocity = 1.8000001f;
		}
		if ((bool)fwmesh && (bool)rwmesh)
		{
			fwRest = (lastF = fwmesh.localRotation);
			rwRest = rwmesh.localRotation;
			rwRestC = (lastR = Quaternion.Inverse(t.rotation) * rwmesh.rotation);
			restOk = true;
		}
		fwheelRadius = (((bool)tfw && (bool)tfw.GetComponent<SphereCollider>()) ? tfw.GetComponent<SphereCollider>().radius : 0.3f);
		rwheelRadius = (((bool)trw && (bool)trw.GetComponent<SphereCollider>()) ? trw.GetComponent<SphereCollider>().radius : 0.3f);
		if ((bool)handlemesh)
		{
			Transform transform4 = new GameObject("handle_pivot").transform;
			transform4.SetParent(handlemesh.parent, worldPositionStays: false);
			transform4.SetPositionAndRotation(handlemesh.position, handlemesh.rotation);
			handlemesh.SetParent(transform4, worldPositionStays: true);
			handlemesh.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			transform4.gameObject.layer = handlemesh.gameObject.layer;
		}
		if ((bool)pivothandle)
		{
			handleLocPos = pivothandle.localPosition;
		}
		if ((bool)handlemesh && (bool)tfw)
		{
			distWheelHandle = Vector3.Distance(handlemesh.position, tfw.position);
		}
		foreach (Collider col2 in cols)
		{
			car.cols.Add(col2);
		}
		sound = new MxSound(this);
	}

	private static void Mat(Collider c, Dictionary<string, object> m)
	{
		if (m != null)
		{
			string key = Json.S(m, "name") + Json.F(m, "dyn");
			if (!mats.TryGetValue(key, out var value))
			{
				value = new PhysicsMaterial(Json.S(m, "name"))
				{
					dynamicFriction = Json.F(m, "dyn", 0.6f),
					staticFriction = Json.F(m, "sta", 0.6f),
					bounciness = Json.F(m, "bounce"),
					frictionCombine = (PhysicsMaterialCombine)Json.I(m, "fcomb"),
					bounceCombine = (PhysicsMaterialCombine)Json.I(m, "bcomb")
				};
				mats[key] = value;
			}
			c.sharedMaterial = value;
		}
	}

	private static JointDrive Drive(object o, float springK = 1f)
	{
		JointDrive result = default(JointDrive);
		result.positionSpring = Json.F(o, "positionSpring") * springK;
		result.positionDamper = Json.F(o, "positionDamper");
		result.maximumForce = Mathf.Min(3.4E+38f, Json.F(o, "maximumForce", 3.4E+38f));
		return result;
	}

	private static SoftJointLimit Lim(object o, float k = 1f)
	{
		SoftJointLimit result = default(SoftJointLimit);
		result.limit = Json.F(o, "limit") * k;
		result.bounciness = Json.F(o, "bounciness");
		result.contactDistance = Json.F(o, "contactDistance") * k;
		return result;
	}

	private void Joint(Transform tr, object c, Dictionary<string, Rigidbody> nodeRb)
	{
		ConfigurableJoint configurableJoint = tr.gameObject.AddComponent<ConfigurableJoint>();
		string text = Json.S(c, "ConnectedBody");
		if (text != null && nodeRb.TryGetValue(text, out var value))
		{
			configurableJoint.connectedBody = value;
		}
		Vector3 vector = V3(Json.Get(c, "Axis"));
		configurableJoint.axis = ((vector.sqrMagnitude > 1E-06f) ? vector : Vector3.right);
		Vector3 secondaryAxis = V3(Json.Get(c, "SecondaryAxis"));
		if (secondaryAxis.sqrMagnitude > 1E-06f)
		{
			configurableJoint.secondaryAxis = secondaryAxis;
		}
		configurableJoint.anchor = V3(Json.Get(c, "Anchor"));
		configurableJoint.autoConfigureConnectedAnchor = Json.B(c, "AutoConfigureConnectedAnchor", def: true);
		if (!configurableJoint.autoConfigureConnectedAnchor)
		{
			configurableJoint.connectedAnchor = V3(Json.Get(c, "ConnectedAnchor"));
		}
		configurableJoint.xMotion = (ConfigurableJointMotion)Json.I(c, "XMotion");
		configurableJoint.yMotion = (ConfigurableJointMotion)Json.I(c, "YMotion");
		configurableJoint.zMotion = (ConfigurableJointMotion)Json.I(c, "ZMotion");
		configurableJoint.angularXMotion = (ConfigurableJointMotion)Json.I(c, "AngularXMotion");
		configurableJoint.angularYMotion = (ConfigurableJointMotion)Json.I(c, "AngularYMotion");
		configurableJoint.angularZMotion = (ConfigurableJointMotion)Json.I(c, "AngularZMotion");
		configurableJoint.linearLimit = Lim(Json.Get(c, "LinearLimit"), 1.2f);
		object o = Json.Get(c, "LinearLimitSpring");
		configurableJoint.linearLimitSpring = new SoftJointLimitSpring
		{
			spring = Json.F(o, "spring"),
			damper = Json.F(o, "damper")
		};
		configurableJoint.lowAngularXLimit = Lim(Json.Get(c, "LowAngularXLimit"));
		configurableJoint.highAngularXLimit = Lim(Json.Get(c, "HighAngularXLimit"));
		configurableJoint.angularYLimit = Lim(Json.Get(c, "AngularYLimit"));
		configurableJoint.angularZLimit = Lim(Json.Get(c, "AngularZLimit"));
		configurableJoint.xDrive = Drive(Json.Get(c, "XDrive"));
		configurableJoint.yDrive = Drive(Json.Get(c, "YDrive"));
		configurableJoint.zDrive = Drive(Json.Get(c, "ZDrive"));
		configurableJoint.angularXDrive = Drive(Json.Get(c, "AngularXDrive"));
		configurableJoint.angularYZDrive = Drive(Json.Get(c, "AngularYZDrive"));
		configurableJoint.slerpDrive = Drive(Json.Get(c, "SlerpDrive"));
		configurableJoint.rotationDriveMode = (RotationDriveMode)Json.I(c, "RotationDriveMode");
		configurableJoint.targetPosition = V3(Json.Get(c, "TargetPosition")) * 1.2f;
		configurableJoint.enablePreprocessing = Json.B(c, "EnablePreprocessing", def: true);
		configurableJoint.massScale = Json.F(c, "MassScale", 1f);
		configurableJoint.connectedMassScale = Json.F(c, "ConnectedMassScale", 1f);
		configurableJoint.configuredInWorldSpace = Json.B(c, "ConfiguredInWorldSpace");
		configurableJoint.swapBodies = Json.B(c, "SwapBodies");
		configurableJoint.enableCollision = Json.B(c, "EnableCollision");
	}

	public void Camera(Camera cam, float dt, float camPitch)
	{
		if (camBike != this)
		{
			camBike = this;
			camAng = base.transform.eulerAngles.y;
			camAngV = 0f;
			orbit = 0f;
			orbitV = 0f;
			lastCamYaw = 0f / 0f;
		}
		FBPlayer i = FBPlayer.I;
		float target = (i ? i.camYaw : camAng);
		if (!float.IsNaN(lastCamYaw) && Mathf.Abs(Mathf.DeltaAngle(lastCamYaw, target)) > 0.01f)
		{
			orbit += Mathf.DeltaAngle(lastCamYaw, target);
			orbitT = 0f;
		}
		else if ((orbitT += dt) > 0.5f)
		{
			orbit = Mathf.SmoothDampAngle(orbit, 0f, ref orbitV, 0.5f, 1f / 0f, dt);
		}
		Vector3 vector = Vector3.ProjectOnPlane(base.transform.forward, Vector3.up);
		if (crashed && (bool)rb && rb.linearVelocity.sqrMagnitude > 0.25f)
		{
			vector = rb.linearVelocity.Flat();
		}
		if (vector.sqrMagnitude < 0.0001f)
		{
			vector = Vector3.forward;
		}
		float y = Quaternion.LookRotation(vector.normalized).eulerAngles.y;
		float num = Mathf.Clamp01((Mathf.Abs(bikeSpeed) - 30f) / 50f);
		num = num * num * (3f - 2f * num);
		float smoothTime = num * 0.25f + (1f - num) * 0.6f;
		camAng = Mathf.SmoothDampAngle(camAng, y, ref camAngV, smoothTime, 1f / 0f, dt);
		float num2 = camAng + orbit;
		if ((bool)i)
		{
			i.camYaw = num2;
		}
		lastCamYaw = num2;
		Vector3 vector2 = base.transform.TransformPoint(Vector3.up * 1.2f);
		Vector3 vector3 = new Vector3(vector2.x, rwrb ? rwrb.position.y : (base.transform.position.y + 0.36f), vector2.z) + vector.normalized * -0.24000001f;
		Vector3 vector4 = Quaternion.Euler(0f, num2, 0f) * Vector3.forward;
		float num3 = Mathf.Clamp(FBDrive.CamZoom / 0.82f, 0.6f, 1.6f);
		Vector3 vector5 = vector3 + Vector3.up * 1.4f * 1.2f - vector4 * 2.55f * 1.2f * num3;
		Vector3 vector6 = vector3 + Vector3.up * 0.9f * 1.2f;
		sway = Mathf.Lerp(sway, in_steering, Mathf.Clamp01(dt * 4f));
		Vector3 vector7 = vector5 - vector6;
		if (FBWorld.RayHit(vector6, vector7.normalized, vector7.magnitude + 0.2f, out var hit))
		{
			vector5 = vector6 + vector7.normalized * Mathf.Max(0.6f, hit.distance - 0.25f);
		}
		cam.transform.position = vector5;
		cam.transform.LookAt(vector6);
		cam.transform.position += cam.transform.right * sway * (1f - wheelieMod45) * speedMod40 * 0.25f * 1.2f;
		float num4 = Mathf.Clamp01((Mathf.Abs(bikeSpeed) - 10f) / 190f);
		float num5 = num4 * 8f + 2f;
		float num6 = num4 * 0.049f + 0.001f;
		cam.transform.Rotate((Mathf.PerlinNoise(Time.time * num5, 0f) * 2f - 1f) * num6 * 2f, (Mathf.PerlinNoise(0f, Time.time * num5) * 2f - 1f) * num6 * 2f, 0f, Space.Self);
		cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 62f + Mathf.Min(10f, Mathf.Abs(bikeSpeed) * 0.06f), 1f - Mathf.Exp((0f - dt) * 4f));
		cam.nearClipPlane = 0.08f;
	}

	public void Paint(uint seed)
	{
		Dictionary<string, object> o = Json.O(M, "paint");
		List<object> list = Json.L(o, "parts");
		if (list == null || list.Count == 0)
		{
			return;
		}
		Func<float> rnd = FBVehicles.Seeded(seed);
		List<object> list2 = Json.L(o, "p");
		List<object> list3 = Json.L(o, "def");
		Color[] array = new Color[3];
		for (int i = 0; i < 3; i++)
		{
			List<object> list4 = ((list3 != null && i < list3.Count) ? (list3[i] as List<object>) : null);
			array[i] = ((list4 != null && list4.Count >= 3) ? new Color(Json.Fv(list4[0]), Json.Fv(list4[1]), Json.Fv(list4[2])) : new Color(0.33f, 0.33f, 0.33f));
		}
		array[0] = PickC();
		bool flag = array[0].grayscale < 0.3f;
		bool flag2 = default(bool);
		int num;
		if (list2 != null && list2.Count > 1)
		{
			object obj = list2[1];
			if (obj is bool)
			{
				flag2 = (bool)obj;
				num = 1;
			}
			else
			{
				num = 0;
			}
		}
		else
		{
			num = 0;
		}
		if (((uint)num & (flag2 ? 1u : 0u)) != 0)
		{
			array[1] = ((!(rnd() < 0.6f)) ? PickC() : (flag ? new Color(0.85f, 0.85f, 0.84f) : new Color(0.03f, 0.03f, 0.035f)));
		}
		bool flag3 = default(bool);
		int num2;
		if (list2 != null && list2.Count > 2)
		{
			object obj = list2[2];
			if (obj is bool)
			{
				flag3 = (bool)obj;
				num2 = 1;
			}
			else
			{
				num2 = 0;
			}
		}
		else
		{
			num2 = 0;
		}
		if (((uint)num2 & (flag3 ? 1u : 0u)) != 0)
		{
			array[2] = ((rnd() < 0.7f) ? new Color(0.05f, 0.05f, 0.055f) : array[1]);
		}
		Texture2D texture2D = new Texture2D(1, 3, TextureFormat.RGBA32, mipChain: false)
		{
			filterMode = FilterMode.Point,
			wrapMode = TextureWrapMode.Clamp,
			name = "mx_paint"
		};
		texture2D.SetPixels(new Color[3]
		{
			array[0],
			array[1],
			array[2]
		});
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		HashSet<string> hashSet = new HashSet<string>();
		foreach (object item2 in list)
		{
			foreach (object item3 in Json.L(item2, "mats") ?? new List<object>())
			{
				if (item3 is string item)
				{
					hashSet.Add(item);
				}
			}
		}
		int num3 = 0;
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			bool flag4 = false;
			for (int k = 0; k < sharedMaterials.Length; k++)
			{
				Material material = sharedMaterials[k];
				if (!material || !hashSet.Contains(material.name.Replace(" (Instance)", "")))
				{
					continue;
				}
				Material material2 = new Material(material)
				{
					name = material.name
				};
				string[] array2 = new string[3] { "_BaseMap", "baseColorTexture", "_MainTex" };
				foreach (string text in array2)
				{
					if (material2.HasProperty(text))
					{
						material2.SetTexture(text, texture2D);
					}
				}
				array2 = new string[3] { "_BaseColor", "baseColorFactor", "_Color" };
				foreach (string text2 in array2)
				{
					if (material2.HasProperty(text2))
					{
						material2.SetColor(text2, Color.white);
					}
				}
				sharedMaterials[k] = material2;
				flag4 = true;
				num3++;
			}
			if (flag4)
			{
				renderer.sharedMaterials = sharedMaterials;
			}
		}
		Painted = num3;
		PaintColor = array[0];
		Color PickC()
		{
			float num4 = PESO.Sum();
			float num5 = rnd() * num4;
			for (int m = 0; m < TINTAS.Length; m++)
			{
				num5 -= PESO[m];
				if (num5 <= 0f)
				{
					return TINTAS[m];
				}
			}
			return TINTAS[0];
		}
	}

	public void SetActive(bool on)
	{
		if (on == active)
		{
			return;
		}
		active = on;
		if (on)
		{
			crashed = false;
			crashT = 0f;
			smtThrottle = (in_acceleration = (in_steering = 0f));
			wheelieDir = 1f;
			onWheelie = (in_wheelie = false);
			pmInit = false;
			steervel = 0f;
			changeGear = true;
			currentGear = (currGear = (prevGear = 0));
			foreach (Rigidbody body in bodies)
			{
				body.isKinematic = false;
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
				body.WakeUp();
			}
			if (activeCount++ == 0)
			{
				savedDt = Time.fixedDeltaTime;
				Time.fixedDeltaTime = 0.012f;
			}
			sound.Start();
			return;
		}
		foreach (Rigidbody body2 in bodies)
		{
			if (!body2.isKinematic)
			{
				body2.linearVelocity = Vector3.zero;
				body2.angularVelocity = Vector3.zero;
			}
			body2.isKinematic = true;
		}
		foreach (var (transform, localPosition, localRotation) in rest)
		{
			if ((bool)transform)
			{
				transform.SetLocalPositionAndRotation(localPosition, localRotation);
			}
		}
		if ((bool)pivothandle)
		{
			pivothandle.localPosition = handleLocPos;
		}
		if ((bool)handlemesh)
		{
			handlemesh.localRotation = Quaternion.identity;
		}
		if (--activeCount <= 0)
		{
			activeCount = 0;
			if (savedDt > 0f)
			{
				Time.fixedDeltaTime = savedDt;
			}
		}
		sound.Stop();
	}

	private void OnDestroy()
	{
		if (active)
		{
			active = false;
			if (--activeCount <= 0)
			{
				activeCount = 0;
				if (savedDt > 0f)
				{
					Time.fixedDeltaTime = savedDt;
				}
			}
		}
		sound?.Stop();
	}

	public void Input(float steer, float thr, bool wheelie, bool revving, bool stand, bool tA, bool tB, float dt)
	{
		float smoothTime = 0.3f + 0.15f * Mathf.Clamp01((weight - 60f) / 190f);
		in_steering = Mathf.Clamp(Mathf.SmoothDamp(in_steering, Mathf.Clamp(steer, -1f, 1f), ref steervel, smoothTime, 1f / 0f, dt), -1f, 1f);
		in_revving = (revving ? 1 : 0);
		in_wheelie = wheelie;
		standing = stand;
		trickA = tA && onWheelie;
		trickB = tB && onWheelie;
		smtThrottle = Mathf.MoveTowards(smtThrottle, Mathf.Clamp(thr, -1f, 1f), dt * 4f);
		in_acceleration = smtThrottle;
	}

	private static readonly int __fbt_FixedUpdate = FBTempo.Id("FBMxBike.FixedUpdate");

	private void FixedUpdate()
	{
		if (!FBTempo.On)
		{
			FixedUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		FixedUpdate__fbt();
		FBTempo.Soma(__fbt_FixedUpdate, t0);
	}

	private void FixedUpdate__fbt()
	{
		if (!active || !rb || rb.isKinematic)
		{
			return;
		}
		float fixedDeltaTime = Time.fixedDeltaTime;
		foreach (Rigidbody body in bodies)
		{
			if ((bool)body && !body.isKinematic && body.useGravity)
			{
				body.AddForce(Vector3.down * (12f + Physics.gravity.y), ForceMode.Acceleration);
			}
		}
		if (crashed)
		{
			crashT += fixedDeltaTime;
			if (crashT > 0.4f)
			{
				foreach (Rigidbody body2 in bodies)
				{
					if ((bool)body2 && !body2.isKinematic)
					{
						Vector3 linearVelocity = body2.linearVelocity;
						float num = Mathf.Clamp01(1f - fixedDeltaTime * 1.6f);
						body2.linearVelocity = new Vector3(linearVelocity.x * num, linearVelocity.y, linearVelocity.z * num);
						body2.angularVelocity *= Mathf.Clamp01(1f - fixedDeltaTime * 1.2f);
					}
				}
			}
			if (crashT > 4f && rb.linearVelocity.sqrMagnitude < 0.09f && !car.driver)
			{
				(Vector3, Quaternion) tuple = (t.position, t.rotation);
				SetActive(on: false);
				t.SetPositionAndRotation(tuple.Item1, tuple.Item2);
			}
			return;
		}
		if (onWheelie)
		{
			in_wheelie = true;
		}
		float num2 = ((in_acceleration <= 0f) ? Mathf.Min(0f - in_acceleration, 1f) : 0f);
		wheelieDir += ((float)((!in_wheelie) ? 1 : (-1)) - wheelieDir) * Mathf.Clamp01(fixedDeltaTime * 2f);
		if ((bool)pivothandle && (bool)handlemesh && (bool)tfw)
		{
			float num3 = Vector3.Distance(handlemesh.position, tfw.position);
			pivothandle.localPosition = new Vector3(handleLocPos.x, (distWheelHandle - num3) / 1.2f, handleLocPos.z);
		}
		float num4 = (rwrb ? trw.InverseTransformDirection(rwrb.angularVelocity).x : 0f);
		Vector3 normalized = Vector3.Cross(Vector3.ProjectOnPlane(t.right, Vector3.up).normalized, Vector3.up).normalized;
		Vector3 linearVelocity2 = rb.linearVelocity;
		speedKmh = (bikeSpeed = Vector3.Dot(linearVelocity2, normalized) * 3.6f / 1.2f);
		speedMod10 = Mathf.Clamp01(speedKmh / 10f);
		speedMod40 = Mathf.Clamp01(speedKmh / 40f);
		float num5 = 1f - Mathf.Clamp01((bikeSpeed - 10f) / 30f);
		fwgrip = fwgrip_def * (1f + 0.25f * num5);
		rwgrip = rwgrip_def * (1f + 0.25f * num5);
		visualGear = 0;
		for (int i = 0; i < gears.Length; i++)
		{
			if (bikeSpeed > gears[i] - 2f)
			{
				visualGear = ((i + 1 < gears.Length) ? (i + 1) : (gears.Length - 1));
			}
		}
		float num6 = gears[visualGear];
		float target = ((num6 == 0f || bikeSpeed / num6 < 0f) ? 1f : Mathf.Clamp(bikeSpeed / num6, 0.1f, 1f));
		rpmN = Mathf.Clamp01(Mathf.MoveTowards(rpmN, target, fixedDeltaTime * 4f));
		Vector3 normalized2 = Vector3.ProjectOnPlane(tchassis.right, Vector3.up).normalized;
		Vector3 normalized3 = Vector3.Cross(normalized2, Vector3.up).normalized;
		vmag = linearVelocity2.magnitude / 1.2f;
		if ((bool)tfw && normalized3.sqrMagnitude > 1E-06f)
		{
			tfw.rotation = Quaternion.AngleAxis(in_steering * steerAngle.Evaluate(vmag), Vector3.up) * Quaternion.LookRotation(normalized3, Vector3.up);
		}
		if ((bool)fwjoint)
		{
			fwjoint.axis = Vector3.right;
		}
		rb.centerOfMass = Vector3.zero;
		float num7 = ((maxspeed != 0f && bikeSpeed / maxspeed >= 0f) ? Mathf.Min(bikeSpeed / maxspeed, 1f) : 0f);
		currGear = Mathf.Min(Mathf.FloorToInt(num7 * (float)gearcount), gearcount);
		if (currGear != prevGear)
		{
			changeGear = false;
			currentGear = 0;
			gearWaitT = 0.2f;
		}
		if (gearWaitT > 0f)
		{
			gearWaitT -= fixedDeltaTime;
			if (gearWaitT <= 0f)
			{
				changeGear = true;
			}
		}
		prevGear = currGear;
		if (changeGear)
		{
			currentGear = currGear;
		}
		gearRatio = Mathf.Clamp01((vmag + (float)gearcount) / (float)gearcount - (float)currentGear);
		float num8 = ((bikeSpeed < maxspeed) ? ((1f - num7) * 0.4f + 0.6f) : 0f);
		if ((bool)rwrb && Mathf.Abs(rwrb.angularVelocity.x) > 500f)
		{
			num8 = 0f;
		}
		if (in_acceleration > 0f && (bool)rwrb)
		{
			float num9 = ((currentGear >= 0) ? Mathf.Min(currentGear, 1) : 0);
			rwrb.AddTorque(t.right * (in_acceleration * torque * num9 * num8) * 1.44f);
			rb.AddForce(tchassis.forward * acelcv.Evaluate(gearRatio) * num8 * 1.2f);
		}
		isReversing = num4 < 0f;
		if (vmag < reverseSpeed && in_acceleration < 0f)
		{
			rb.AddForce(-t.forward * acelcv.Evaluate(in_acceleration) * 0.5f * 1.2f);
		}
		Vector3 vector = ((linearVelocity2.sqrMagnitude > 1E-10f) ? linearVelocity2.normalized : Vector3.zero);
		// fisica 31/10: raycasts das rodas movidos para ca (antes ficavam mais abaixo) para o freio saber se ha roda no chao
		int layerMask = -11777;
		bool flag = (bool)tfw && Physics.Raycast(tfw.position, Vector3.down, fwheelRadius * 1.2f * 1.4f, layerMask, QueryTriggerInteraction.Ignore);
		bool flag2 = (bool)trw && Physics.Raycast(trw.position, Vector3.down, rwheelRadius * 1.2f * 1.4f, layerMask, QueryTriggerInteraction.Ignore);
		// fisica 31/10: freio (S) e freio-motor so agem com alguma roda no chao. No ar eles freavam a velocidade 3D
		// (inclusive a queda) e a moto ficava quase parada no ar segurando S.
		bool noChao = flag || flag2;
		// Correção: sem acelerar, a moto também freia quando está indo para trás (antes rodava de ré sozinha sem parar)
		// e o freio não passa do zero (não fica invertendo o sentido a cada passo).
		if (noChao && in_acceleration == 0f && !in_wheelie)
		{
			rb.AddForce(-Vector3.ClampMagnitude(vector * gearRatio * brakePower * 0.2f * fixedDeltaTime * 1.2f, linearVelocity2.magnitude), ForceMode.VelocityChange);
		}
		if (noChao && in_acceleration < 0f && !isReversing)
		{
			// fisica 31/10: freio limitado a |v| (nao passa do zero)
			rb.AddForce(-Vector3.ClampMagnitude(vector * num2 * brakePower * fixedDeltaTime * 1.2f, linearVelocity2.magnitude), ForceMode.VelocityChange);
		}
		pickUpSpeed = Mathf.Clamp01((bikeSpeed - 2f) * 0.125f);
		float num10 = (onWheelie ? 1.2f : 1f);
		smtHandleRot = Mathf.MoveTowardsAngle(smtHandleRot, 0f - lookDir, fixedDeltaTime * 4f);
		if ((bool)handlemesh)
		{
			handlemesh.localRotation = Quaternion.Euler(0f, in_steering * wheelieDir * steerAngle.Evaluate(vmag) * num10, 0f);
		}
		float num11 = Vector3.SignedAngle(tchassis.forward, normalized3, normalized2);
		rabeta = false;
		if (num11 > 50f)
		{
			rabeta = num11 > scrapeAngle && num11 < fallAngle;
			if (num11 >= fallAngle)
			{
				Crash("loop");
			}
			if (bikeSpeed < 1f)
			{
				Crash("parado");
			}
		}
		if (num11 > 10f)
		{
			rb.linearVelocity *= 0.999f;
		}
		prevWA = wheelieAngle;
		wheelieAngle = num11;
		wheelieMod45 = Mathf.Clamp01(num11 / 45f);
		wheelieMod90 = Mathf.Clamp01(num11 / 90f);
		wheelieMod4590 = Mathf.Clamp01((num11 - 45f) / 45f);
		wheelieMod3060 = Mathf.Clamp01((num11 - 30f) / 30f);
		float num12 = Mathf.Clamp01((num11 - 90f) / 10f);
		if (num12 > 0f && linearVelocity2.sqrMagnitude > 0.01f)
		{
			Vector3 normalized4 = Vector3.ProjectOnPlane(linearVelocity2, Vector3.up).normalized;
			rb.AddTorque(Vector3.up * 50f * Vector3.SignedAngle(normalized3, normalized4, Vector3.up) * num12 * 1.2f * 1.2f);
		}
		float num13 = Mathf.Clamp01(bikeSpeed / 30f) * (0f - leanCurve.Evaluate(in_steering));
		leanAngle = num13 + Mathf.Clamp01(wheelieMod45) * (num13 * 0.5f - num13);
		if (flag && (bool)fwrb)
		{
			float num14 = fwrb.transform.InverseTransformDirection(rb.GetPointVelocity(fwrb.position)).x / 1.2f;
			fwrb.AddRelativeForce(Vector3.right * ((num14 >= -10f) ? (-40f * Mathf.Min(num14, 10f)) : 400f) * 1.2f);
		}
		if (flag2 && (bool)rwrb)
		{
			float num15 = rwrb.transform.InverseTransformDirection(rb.GetPointVelocity(rwrb.position)).x / 1.2f;
			rb.AddForceAtPosition(rwrb.transform.right * ((num15 >= -10f) ? (-80f * Mathf.Min(num15, 10f)) : 800f) * 1.2f, rwrb.position);
			if (num12 < 0.9f && (bool)fwrb)
			{
				Vector3 direction = tfw.InverseTransformDirection(fwrb.linearVelocity);
				direction.x *= Mathf.Clamp01(1f / (fwgrip.x + fwgrip.y));
				fwrb.linearVelocity = tfw.TransformDirection(direction);
				rb.linearVelocity = Quaternion.AngleAxis(in_steering * 0.1f, Vector3.up) * rb.linearVelocity;
			}
			Vector3 direction2 = trw.InverseTransformDirection(rwrb.linearVelocity);
			direction2.x *= Mathf.Clamp01(1f / (rwgrip.x + rwgrip.y));
			rwrb.linearVelocity = trw.TransformDirection(direction2);
			if (Mathf.Abs(in_steering) < 0.05f)
			{
				Vector3 direction3 = t.InverseTransformDirection(rb.angularVelocity);
				direction3.y *= 0.1f;
				rb.angularVelocity = t.TransformDirection(direction3);
				Vector3 direction4 = t.InverseTransformDirection(rb.linearVelocity);
				direction4.x *= 0.1f;
				rb.linearVelocity = t.TransformDirection(direction4);
			}
		}
		else
		{
			Vector3 direction5 = t.InverseTransformDirection(rb.angularVelocity);
			direction5.y *= 0.1f;
			direction5.z *= 0.1f;
			rb.angularVelocity = t.TransformDirection(direction5);
			Vector3 direction6 = t.InverseTransformDirection(rb.linearVelocity);
			direction6.x *= 0.1f;
			rb.linearVelocity = t.TransformDirection(direction6);
		}
		if (AntiHop && flag && flag2 && !onWheelie && !in_wheelie && rb.linearVelocity.y > 1.08f)
		{
			float num16 = 1.08f;
			hopVy = Mathf.Max(hopVy, rb.linearVelocity.y / 1.2f);
			foreach (Rigidbody body3 in bodies)
			{
				if ((bool)body3 && !body3.isKinematic && body3.linearVelocity.y > num16)
				{
					Vector3 linearVelocity3 = body3.linearVelocity;
					linearVelocity3.y = Mathf.Lerp(linearVelocity3.y, num16, 0.6f);
					body3.linearVelocity = linearVelocity3;
				}
			}
		}
		if (onWheelie)
		{
			rb.AddTorque(-t.right * wheelieMod3060 * balanceForce, ForceMode.Acceleration);
		}
		if ((bool)fwrb)
		{
			lastVelocity = fwrb.linearVelocity;
		}
		bool flag3 = false;
		if (in_wheelie)
		{
			if (in_acceleration < 0f)
			{
				rb.AddTorque(t.right * num2 * Mathf.Clamp01(bikeSpeed - 1f) * wbrakePower * wheelieMod4590, ForceMode.Acceleration);
			}
			if (in_acceleration > 0f && bikeSpeed > 5f)
			{
				float num17 = (1f - ((maxspeed != 0f) ? Mathf.Clamp01(speedKmh / maxspeed) : 0f)) * 0.5f + 0.5f;
				rb.angularDamping = wheelieDrag;
				wheeliePowerA = in_acceleration * wheeliePowerB * num17;
				if ((bool)tfw && Physics.Raycast(tfw.position, Vector3.down, fwheelRadius * 1.2f * 1.2f, layerMask, QueryTriggerInteraction.Ignore))
				{
					wheeliePowerA *= 2f;
				}
				if (GrauAssist && num11 > 4f)
				{
					float num18 = 24f + 14f * in_acceleration;
					float b = (num11 - prevWA) / Mathf.Max(fixedDeltaTime, 0.0001f);
					float num19 = num11 + Mathf.Max(0f, b) * 0.25f;
					wheeliePowerA *= Mathf.Clamp01(0.55f + (num18 - num19) / 18f);
					if (num19 > num18 + 4f)
					{
						rb.AddTorque(t.right * Mathf.Min(40f, (num19 - num18 - 4f) * 1.6f), ForceMode.Acceleration);
					}
				}
				rb.AddTorque(-t.right * wheeliePowerA, ForceMode.Acceleration);
				rb.AddTorque(Vector3.up * in_steering * steerAngle.Evaluate(vmag), ForceMode.Acceleration);
				rb.linearVelocity = Quaternion.AngleAxis(in_steering * 0.1f, Vector3.up) * rb.linearVelocity;
				flag3 = true;
			}
		}
		if (!flag3)
		{
			rb.angularDamping = 0.2f;
		}
		Vector3 forward = t.forward;
		Vector3 from = Vector3.ProjectOnPlane(Vector3.up, forward);
		Vector3 to = Vector3.ProjectOnPlane(t.up, forward);
		if (from.sqrMagnitude > 1E-06f && to.sqrMagnitude > 1E-06f && (bool)rwrb)
		{
			float num20 = Vector3.SignedAngle(from, to, forward);
			t.RotateAround(rwrb.position, forward, 0f - (num20 - leanAngle));
		}
		float num21 = Vector3.SignedAngle(t.forward, Vector3.up, t.right);
		if (num21 > -95f && in_wheelie)
		{
			onWheelie = true;
		}
		if (num21 < -90f)
		{
			onWheelie = false;
			in_wheelie = false;
		}
		if (Mathf.Abs(in_steering) > 0.25f)
		{
			lookDir = Mathf.Sign(in_steering);
		}
		targetLeanWheelie = Mathf.MoveTowards(targetLeanWheelie, lookDir, fixedDeltaTime * 4f);
		leanWheelieAngle = (0f - targetLeanWheelie) * lookAngle * wheelieMod4590;
	}

	private void OnCollisionEnter(Collision c)
	{
		if (!active || crashed)
		{
			return;
		}
		float num = ((wheelieAngle > 60f) ? 800 : 8000);
		if (c.impulse.sqrMagnitude / 1.44f > num)
		{
			if (in_wheelie)
			{
				Crash("batida");
			}
			else if (speedKmh > 50f)
			{
				Crash("batida");
			}
		}
	}

	public void Crash(string why)
	{
		if (crashed || !active)
		{
			return;
		}
		crashed = true;
		crashT = 0f;
		in_acceleration = (smtThrottle = 0f);
		foreach (Rigidbody body in bodies)
		{
			if ((bool)body)
			{
				body.angularDamping = 0.2f;
			}
		}
		this.OnCrash?.Invoke(why);
	}

	// fisica 31/10: piloto saiu a forca (morreu) com a moto andando ou no ar: a moto fica na fisica e cai/para sozinha,
	// como no caminho de queda, mas sem disparar OnCrash (MxCrash -> Exit de novo, recursivo)
	public void Abandon()
	{
		if (crashed || !active)
		{
			return;
		}
		crashed = true;
		crashT = 0f;
		in_acceleration = (smtThrottle = 0f);
		foreach (Rigidbody body in bodies)
		{
			if ((bool)body)
			{
				body.angularDamping = 0.2f;
			}
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBMxBike.Update");

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
		if (active && (bool)fwmesh && (bool)trw)
		{
			fwmesh.localRotation = trw.localRotation;
		}
		if (active)
		{
			sound?.Update(Time.deltaTime);
		}
	}

	private void SpinVisual(float dt)
	{
		if (restOk && !(dt <= 0f) && (bool)fwmesh && (bool)rwmesh)
		{
			float num = ((active || Time.time - remT < 0.3f) ? (bikeSpeed / 3.6f * 1.2f) : 0f);
			spinExp = Mathf.Abs(num) / Mathf.Max(0.05f, rwheelRadius * 1.2f) * 57.29578f;
			float spinCapDeg = SpinCapDeg;
			float num2 = Mathf.Clamp(num / Mathf.Max(0.05f, fwheelRadius * 1.2f) * 57.29578f * dt, 0f - spinCapDeg, spinCapDeg);
			float num3 = Mathf.Clamp(num / Mathf.Max(0.05f, rwheelRadius * 1.2f) * 57.29578f * dt, 0f - spinCapDeg, spinCapDeg);
			if (active && (bool)rwrb && Mathf.Abs(bikeSpeed) < 8f && in_acceleration > 0.5f)
			{
				num3 = Mathf.Clamp(Mathf.Abs(trw.InverseTransformDirection(rwrb.angularVelocity).x) * 57.29578f * dt, 0f - spinCapDeg, spinCapDeg);
			}
			angF = (angF + num2) % 360f;
			angR = (angR + num3) % 360f;
			fwmesh.localRotation = fwRest * Quaternion.Euler(angF, 0f, 0f);
			rwmesh.rotation = t.rotation * rwRestC * Quaternion.Euler(angR, 0f, 0f);
			Quaternion b = Quaternion.Inverse(t.rotation) * rwmesh.rotation;
			float b2 = Quaternion.Angle(lastF, fwmesh.localRotation) / dt;
			float b3 = Quaternion.Angle(lastR, b) / dt;
			lastF = fwmesh.localRotation;
			lastR = b;
			spinF = Mathf.Lerp(spinF, b2, Mathf.Clamp01(dt * 6f));
			spinR = Mathf.Lerp(spinR, b3, Mathf.Clamp01(dt * 6f));
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBMxBike.LateUpdate");

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
		SpinVisual(Time.deltaTime);
		if (active)
		{
			car.v = bikeSpeed * 1.2f / 3.6f;
			car.lean = leanAngle * ((float)Math.PI / 180f);
			car.steerN = in_steering;
			car.wheelie = Mathf.Max(0f, wheelieAngle) * ((float)Math.PI / 180f);
			car.rpm = rpmN;
			car.gear = visualGear + 1;
			car.yaw = (0f - t.eulerAngles.y) * ((float)Math.PI / 180f);
			car.pitch = 0f;
			car.roll = 0f;
		}
	}

	public float SteerDeg(float vmag)
	{
		if (steerAngle == null)
		{
			return 30f;
		}
		return steerAngle.Evaluate(vmag);
	}

	public void RemoteState(float v, float steer, float thr, float wheelieRad, float dt)
	{
		bikeSpeed = (speedKmh = v * 3.6f / 1.2f);
		in_steering = Mathf.Clamp(steer, -1f, 1f);
		in_acceleration = thr;
		wheelieAngle = wheelieRad * 57.29578f;
		onWheelie = wheelieAngle > 8f;
		isReversing = bikeSpeed < -0.3f;
		visualGear = 0;
		for (int i = 0; i < gears.Length; i++)
		{
			if (Mathf.Abs(bikeSpeed) > gears[i] - 2f)
			{
				visualGear = Mathf.Min(i + 1, gears.Length - 1);
			}
		}
		float num = Mathf.Max(1f, gears[visualGear]);
		rpmN = Mathf.MoveTowards(rpmN, Mathf.Clamp(Mathf.Abs(bikeSpeed) / num, 0.1f, 1f), dt * 4f);
		if ((bool)handlemesh)
		{
			handlemesh.localRotation = Quaternion.Euler(0f, in_steering * steerAngle.Evaluate(Mathf.Abs(v) / 1.2f), 0f);
		}
		Vector3 position = t.position;
		if (!remInit)
		{
			remPrev = position;
			remInit = true;
		}
		_ = Vector3.Dot(position - remPrev, t.forward) / 1.2f;
		remPrev = position;
		remT = Time.time;
		if (!sound.On)
		{
			sound.Start();
		}
		sound.Update(dt);
	}

	public void RemoteStop()
	{
		remInit = false;
		if (!active)
		{
			sound.Stop();
		}
	}

	public void Launch(Vector3 vel)
	{
		if (!active)
		{
			return;
		}
		foreach (Rigidbody body in bodies)
		{
			if ((bool)body && !body.isKinematic)
			{
				body.linearVelocity = vel;
			}
		}
		float num = Vector3.Dot(vel, t.forward);
		if ((bool)rwrb && !rwrb.isKinematic)
		{
			rwrb.angularVelocity = t.right * (num / Mathf.Max(0.05f, rwheelRadius * 1.2f));
		}
		if ((bool)fwrb && !fwrb.isKinematic)
		{
			fwrb.angularVelocity = t.right * (num / Mathf.Max(0.05f, fwheelRadius * 1.2f));
		}
	}

	public void StartBlip()
	{
		sound?.Blip();
	}

	private Pose PoseOf(int i)
	{
		Dictionary<string, object> r = Json.O(M, "rig");
		Pose result = default(Pose);
		result.hips = G("hips");
		result.lhand = G("lhand");
		result.rhand = G("rhand");
		result.lhh = G("lhandhint");
		result.rhh = G("rhandhint");
		result.lfoot = G("lfoot");
		result.rfoot = G("rfoot");
		result.lfh = G("lfoothint");
		result.rfh = G("rfoothint");
		result.pitch = pitchOf(i) - pitchOf(2);
		return result;
		Vector3 G(string k)
		{
			List<object> list2 = Json.L(r, k);
			if (list2 == null || i >= list2.Count)
			{
				return Vector3.zero;
			}
			return V3(list2[i]);
		}
		float pitchOf(int j)
		{
			List<object> list = Json.L(r, "hipsRot");
			if (list == null || j >= list.Count)
			{
				return 0f;
			}
			Quaternion quaternion = Q4(list[j]);
			if (!(quaternion.eulerAngles.x > 180f))
			{
				return quaternion.eulerAngles.x;
			}
			return quaternion.eulerAngles.x - 360f;
		}
	}

	public void Recover()
	{
		if (crashed || !(Vector3.Angle(t.up, Vector3.up) < 25f))
		{
			float y = t.eulerAngles.y;
			Vector3 position = t.position;
			SetActive(on: false);
			crashed = false;
			crashT = 0f;
			if (Physics.Raycast(position + Vector3.up * 2f * 1.2f, Vector3.down, out var hitInfo, 7.2000003f, -11777, QueryTriggerInteraction.Ignore))
			{
				position.y = hitInfo.point.y;
			}
			t.SetPositionAndRotation(position + Vector3.up * 0.35f * 1.2f, Quaternion.Euler(0f, y, 0f));
			Physics.SyncTransforms();
			curInit = false;
			Recovered++;
		}
	}

	private static Pose Lerp(Pose a, Pose b, float k)
	{
		Pose result = default(Pose);
		result.hips = Vector3.Lerp(a.hips, b.hips, k);
		result.lhand = Vector3.Lerp(a.lhand, b.lhand, k);
		result.rhand = Vector3.Lerp(a.rhand, b.rhand, k);
		result.lhh = Vector3.Lerp(a.lhh, b.lhh, k);
		result.rhh = Vector3.Lerp(a.rhh, b.rhh, k);
		result.lfoot = Vector3.Lerp(a.lfoot, b.lfoot, k);
		result.rfoot = Vector3.Lerp(a.rfoot, b.rfoot, k);
		result.lfh = Vector3.Lerp(a.lfh, b.lfh, k);
		result.rfh = Vector3.Lerp(a.rfh, b.rfh, k);
		result.pitch = Mathf.Lerp(a.pitch, b.pitch, k);
		return result;
	}

	public string RiderClip(float dt)
	{
		if (poses == null)
		{
			poses = Enumerable.Range(0, 8).Select(PoseOf).ToArray();
		}
		St st = (trickA ? St.TrickA : (trickB ? St.TrickB : ((!onWheelie) ? ((Mathf.Abs(bikeSpeed) < 2f && in_acceleration <= 0.05f) ? ((isReversing && bikeSpeed < -0.3f) ? St.Reversing : St.Idle) : (standing ? St.RidingStanding : St.Riding)) : (standing ? St.WheelieStanding : St.Wheelie))));
		if (st != state)
		{
			state = st;
			stateT = 0f;
		}
		stateT += dt;
		Pose b = poses[(int)state];
		float num = ((state == St.TrickA || state == St.TrickB) ? 0.4f : 0.25f);
		if (!curInit)
		{
			cur = b;
			curInit = true;
		}
		else
		{
			cur = Lerp(cur, b, Mathf.Clamp01(dt / num * 3f));
		}
		return state switch
		{
			St.Idle => "MX_Idle", 
			St.Reversing => "MX_Reverse", 
			St.TrickA => (stateT < 0.83f) ? "MX_BancoJoelho" : "MX_BancoJoelho", 
			St.TrickB => "MX_ChaoMao", 
			_ => (!(Mathf.Abs(in_steering) > 0.35f) || !(bikeSpeed > 8f)) ? "MX_Ride" : ((in_steering > 0f) ? "MX_LeanR" : "MX_LeanL"), 
		};
	}

	public FBDrive.PoseTargets PoseRider(FBCharacter a, float w)
	{
		FBDrive.PoseTargets result = default(FBDrive.PoseTargets);
		Animator an = a.an;
		if (!an || !an.isHuman)
		{
			return result;
		}
		Transform boneTransform = an.GetBoneTransform(HumanBodyBones.Hips);
		if (!boneTransform)
		{
			return result;
		}
		if (!curInit)
		{
			RiderClip(0f);
		}
		Transform transform = base.transform;
		Pose pose = cur;
		Vector3 vector = transform.TransformPoint(pose.hips) + transform.up * 0.25f * 1.2f;
		float num = Mathf.Max(0.001f, Time.deltaTime);
		// Correção: a mola do quadril freava a velocidade absoluta (a da moto andando), então o quadril ficava sempre
		// atrasado, passava do limite e voltava de uma vez: o piloto tremia para frente e para trás entre ~6 e 45 km/h.
		// Agora a mola é um deslocamento em relação à moto, empurrado só pela aceleração (inércia ao acelerar e frear).
		if (!pmInit)
		{
			pmPos = Vector3.zero;
			pmVel = Vector3.zero;
			pmTgt = vector;
			pmTgtVel = Vector3.zero;
			pmInit = true;
		}
		Vector3 vector9 = (vector - pmTgt) / num;
		Vector3 vector10 = Vector3.ClampMagnitude((vector9 - pmTgtVel) / num, 40f);
		if ((vector - pmTgt).sqrMagnitude > 1f)
		{
			vector9 = pmTgtVel;
			vector10 = Vector3.zero;
		}
		pmTgt = vector;
		pmTgtVel = vector9;
		Vector3 vector11 = transform.InverseTransformVector(vector10);
		Vector3 vector4 = new Vector3(-250f * pmPos.x - 20f * pmVel.x, -250f * pmPos.y - 20f * pmVel.y, -120f * pmPos.z - 25f * pmVel.z) - vector11;
		pmVel += vector4 * num;
		pmPos += pmVel * num;
		if (pmPos.sqrMagnitude > 0.0625f)
		{
			pmPos = Vector3.zero;
			pmVel = Vector3.zero;
		}
		Vector3 vector5 = pmPos;
		vector5 = new Vector3(Mathf.Clamp(vector5.x, -0.06f, 0.06f), Mathf.Clamp(vector5.y, -0.04f, 0.04f), Mathf.Clamp(vector5.z, -0.08f, 0.08f)) * w;
		a.transform.rotation = transform.rotation;
		a.transform.position += transform.TransformPoint(pose.hips + new Vector3(vector5.x, vector5.y, vector5.z) / 1.2f) - boneTransform.position;
		float num2 = pose.pitch * w + Mathf.Clamp(wheelieAngle, 0f, 60f) * -0.25f * w;
		Vector3 right = transform.right;
		Vector3 forward = transform.forward;
		(HumanBodyBones, float)[] array = new(HumanBodyBones, float)[3]
		{
			(HumanBodyBones.Spine, 0.35f),
			(HumanBodyBones.Chest, 0.35f),
			(HumanBodyBones.UpperChest, 0.3f)
		};
		for (int i = 0; i < array.Length; i++)
		{
			(HumanBodyBones, float) tuple = array[i];
			HumanBodyBones item = tuple.Item1;
			float item2 = tuple.Item2;
			Transform boneTransform2 = an.GetBoneTransform(item);
			if ((bool)boneTransform2)
			{
				boneTransform2.rotation = Quaternion.AngleAxis(num2 * item2, right) * Quaternion.AngleAxis((0f - leanWheelieAngle) * 0.3f * item2 * w, transform.up) * boneTransform2.rotation;
			}
		}
		Transform boneTransform3 = an.GetBoneTransform(HumanBodyBones.Head);
		if ((bool)boneTransform3)
		{
			boneTransform3.rotation = Quaternion.AngleAxis((0f - num2) * 0.6f, right) * Quaternion.AngleAxis(leanAngle * 0.35f * w, forward) * boneTransform3.rotation;
		}
		if ((bool)gripL && (bool)gripR)
		{
			Vector3 vector6 = gripL.TransformPoint(pose.lhand);
			Vector3 vector7 = gripR.TransformPoint(pose.rhand);
			Transform transform2 = B(HumanBodyBones.LeftUpperArm);
			Transform transform3 = B(HumanBodyBones.LeftLowerArm);
			Transform transform4 = B(HumanBodyBones.LeftHand);
			Transform transform5 = B(HumanBodyBones.RightUpperArm);
			Transform transform6 = B(HumanBodyBones.RightLowerArm);
			Transform transform7 = B(HumanBodyBones.RightHand);
			if (state != St.TrickB)
			{
				FBDrive.Reach(a, car, a.GetComponent<FBDriverPose>(), transform2, transform3, transform4, transform5, transform6, transform7, vector6, vector7, w);
			}
			FBIK.TwoBonePole(transform2, transform3, transform4, vector6, transform.TransformPoint(pose.lhh), w);
			FBIK.TwoBonePole(transform5, transform6, transform7, vector7, transform.TransformPoint(pose.rhh), w);
			result.hands = true;
			result.hL = vector6;
			result.hR = vector7;
		}
		(Transform, Transform, Transform, Vector3, Vector3)[] array2 = new(Transform, Transform, Transform, Vector3, Vector3)[2]
		{
			(B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot), pose.lfoot, pose.lfh),
			(B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot), pose.rfoot, pose.rfh)
		};
		for (int j = 0; j < 2; j++)
		{
			var (upper, fore, transform8, position, position2) = array2[j];
			if ((bool)transform8)
			{
				Vector3 vector8 = transform.TransformPoint(position);
				if (state == St.Idle && Mathf.Abs(bikeSpeed) < 1f && j == 0 && Physics.Raycast(transform.TransformPoint(new Vector3(position.x - 0.12f, position.y, position.z + 0.1f)) + Vector3.up * 0.6f, Vector3.down, out var hitInfo, 2.5f, FBWorld.SolidMask & -2049, QueryTriggerInteraction.Ignore))
				{
					vector8 = hitInfo.point + Vector3.up * 0.09f;
				}
				Quaternion rotation = transform8.rotation;
				FBIK.TwoBonePole(upper, fore, transform8, vector8, transform.TransformPoint(position2), w);
				transform8.rotation = rotation;
				if (j == 0)
				{
					result.fL = vector8;
				}
				else
				{
					result.fR = vector8;
				}
				result.feet = true;
			}
		}
		return result;
		Transform B(HumanBodyBones b)
		{
			return an.GetBoneTransform(b);
		}
	}
}
}
