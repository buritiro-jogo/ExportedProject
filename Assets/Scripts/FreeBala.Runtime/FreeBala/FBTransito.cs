using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FreeBala
{
public static class FBTransito
{
	public class Car
	{
		public int id;

		public FBCar car;

		public FBCharacter actor;

		public FBCharacter walker;

		public string mode = "drive";

		public float life;

		public float modeT;

		public readonly List<int> route = new List<int>();

		public int ri;

		public float s;

		public float v;

		public float acc;

		public float lat;

		public float latT;

		public float cruise = 11f;

		public float T = 1.4f;

		public float amax = 2.2f;

		public float aggr = 0.5f;

		public float halfL = 2.3f;

		public float halfW = 0.95f;

		public float yaw;

		public float pitch;

		public float roll;

		public float steer;

		public float vPrev;

		public float aAcc;

		public float sensT;

		public float gapObs = 1E+09f;

		public float vObs;

		public float obsStill;

		public float stuck;

		public float honkT;

		public float waitT;

		public float shockT;

		public float passUntil;

		public object obsWho;

		public int overSide;

		public FBSpot spot;

		public bool wantPark;

		public int blink;

		public bool brake;

		public bool reverse;

		public bool hazard;

		public Vector3[] mp;

		public float[] ms;

		public float mlen;

		public float mpos;

		public int mstage;

		public bool mrev;

		public Vector3 pos;

		public Vector3 fwd = Vector3.forward;

		public int segNow = -1;

		public float eta;

		public int nextConn = -1;

		public FBCarSound.Gearbox gb;

		public bool audio;

		public int overSeg = -1;

		public float squealT;

		public float whA;

		public float whT;

		public float whHold;

		public float whPeak;

		public int whSt;

		public bool phys;

		public bool physIgn;

		public float prevA;

		public bool patrol;

		public bool sigHeld;

		public float callT;

		public float callLeft;

		public readonly List<(Vector3[] p, bool rev)> legs = new List<(Vector3[], bool)>();

		public float kWait;

		public string tag;

		public float vCap = 99f;

		public float maxLife = 1E+09f;

		public bool lure;

		public bool leave;

		public Action<Car> onDrop;

		public Vector3? fleeFrom;

		public object lastWho;

		public float lastGap;

		public FBCharacter garupa;

		public bool garupaDown;

		public float yieldT;

		public float pushT;

		public bool yielded;

		public string leadWho;

		public static int Pushes;

		public static int Yields;

		public Car chase;

		public Car fugitive;

		public bool runLights;

		public float chaseT;

		public float closeT;

		public bool caught;
	}

	private static readonly List<Car> cars = new List<Car>();

	private static int serial;

	private static float enterCd = 25f;

	private static float spawnCd;

	private static int target = 10;

	private static List<Car>[] occ = new List<Car>[0];

	private static readonly Dictionary<int, HashSet<Car>> res = new Dictionary<int, HashSet<Car>>();

	private static readonly string[] EXCL = new string[14]
	{
		"S10_2017", "RAM3500", "F250", "Lambo", "McLaren", "Porsche", "Camaro", "Mustang", "AudioS8", "Bmw",
		"Macan", "Evoque", "RangerRover", "Viatura"
	};

	public static int OutsDone;

	public static int InsDone;

	private static List<string> carIds;

	public static int SpotsMade;

	public static float GarupaShare = 0.4f;

	public static int GarupaSpawned;

	public static int GarupaDesceu;

	public static int GarupaSemSkin;

	public static float MotoShare = 0.18f;

	private static float patrolCd = 30f;

	public static int PatrolCalls;

	public static int ChaseStarts;

	public static int ChaseCaught;

	public static int ChaseEnds;

	public static float ChaseGapSum;

	public static float ChaseGapN;

	public static float ChaseVmax;

	public static string ChaseWhy;

	private static float chaseCd = 90f;

	public static int ChaseKTurns;

	private static readonly Dictionary<int, bool> dead = new Dictionary<int, bool>();

	public static int SigStops;

	public static int PropStops;

	public static int PropPushes;

	public static int SirenPasses;

	private static readonly List<(Car c, int conn, float eta, int pri)> reqs = new List<(Car, int, float, int)>();

	private static readonly Vector3[] SP = new Vector3[10];

	private static readonly Vector3[] ST = new Vector3[10];

	private static readonly float[] SD = new float[10];

	private static readonly Vector2[] bxA = new Vector2[4];

	private static readonly Vector2[] bxB = new Vector2[4];

	public static int MotoPhysOn;

	public static int MotoPhysUps;

	public static int MotoCrashes;

	public static int MotoPhysWh;

	public static float MotoPhysLagMax;

	public static float MotoPhysLatMax;

	public static float MotoPhysFrames;

	public static float MotoPhysWhMax;

	public static int Vanished;

	public static int VanishWaits;

	public static bool Running
	{
		get
		{
			if (FBRoads.Ready)
			{
				return occ.Length == FBRoads.segs.Count;
			}
			return false;
		}
	}

	public static IReadOnlyList<Car> List => cars;

	private static Vector3 PlayerPos
	{
		get
		{
			if (!FBMatch.Player)
			{
				return Vector3.zero;
			}
			return FBMatch.Player.Pos;
		}
	}

	private static Vector3 CamPos
	{
		get
		{
			if (!Camera.main)
			{
				return PlayerPos;
			}
			return Camera.main.transform.position;
		}
	}

	public static Car ChaseCop => cars.FirstOrDefault((Car c) => c.fugitive != null && c.mode != "gone");

	public static int MotoPhysMax
	{
		get
		{
			if (!FBQuality.Mobile)
			{
				return 2;
			}
			return 1;
		}
	}

	public static void TestParking()
	{
		foreach (Car car in cars)
		{
			if (car.tag == null && !car.patrol && (!car.car || !car.car.mx) && car.mode == "drive")
			{
				car.wantPark = true;
				car.life = 99f;
			}
		}
		enterCd = 0f;
	}

	private static IEnumerator After(float t, Action a)
	{
		if (t > 0f)
		{
			yield return new WaitForSeconds(t);
		}
		a();
	}

	private static FBSeg Seg(int i)
	{
		return FBRoads.segs[i];
	}

	public static void BuildRoads()
	{
		FBRoads.Load(FBSession.map);
	}

	public static bool OnRoad(Vector3 p, FBCar skip = null)
	{
		return FBRoads.IsRoad(p);
	}

	public static Vector3? SidewalkPoint(Vector3? near, float r)
	{
		for (int i = 0; i < 10; i++)
		{
			Vector3? result = ((r > 0f && near.HasValue) ? FBNav.RandomPoint(near, r) : FBNav.RandomPoint());
			if (!result.HasValue)
			{
				return null;
			}
			if (!FBRoads.Ready || !FBRoads.IsRoad(result.Value))
			{
				return result;
			}
			if (i == 9)
			{
				return result;
			}
		}
		return null;
	}

	public static bool Danger(Vector3 p, float r, float t)
	{
		foreach (Car car in cars)
		{
			if (!car.car || car.mode == "walk" || car.mode == "gone")
			{
				continue;
			}
			Vector3 lhs = p - car.pos;
			lhs.y = 0f;
			float magnitude = lhs.magnitude;
			if (!(magnitude > 60f))
			{
				if (magnitude < r + car.halfL)
				{
					return true;
				}
				float num = Vector3.Dot(lhs, car.fwd);
				float num2 = Mathf.Abs(lhs.x * car.fwd.z - lhs.z * car.fwd.x);
				if (num > 0f && num2 < r + car.halfW && num - car.halfL < Mathf.Max(0f, car.v) * t + 2f)
				{
					return true;
				}
			}
		}
		return false;
	}

	public static void Clear()
	{
		foreach (Car car in cars)
		{
			StopAudio(car);
			if ((bool)car.garupa && !car.garupaDown)
			{
				UnityEngine.Object.Destroy(car.garupa.gameObject);
			}
			if ((bool)car.actor)
			{
				FBDrive.Release(car.actor);
				if (car.mode != "gone")
				{
					UnityEngine.Object.Destroy(car.actor.gameObject);
				}
			}
			if (car.spot != null && car.spot.res == car)
			{
				car.spot.res = null;
			}
		}
		cars.Clear();
		res.Clear();
		FBSemaforo.Clear();
	}

	public static void Start(int n)
	{
		Clear();
		if (!FBRoads.Ready)
		{
			Debug.Log("[FreeBala][trânsito] mapa sem malha de faixas — sem trânsito");
			return;
		}
		occ = new List<Car>[FBRoads.segs.Count];
		for (int i = 0; i < occ.Length; i++)
		{
			occ[i] = new List<Car>();
		}
		SpotsFromParked();
		foreach (FBSpot spot in FBRoads.spots)
		{
			spot.car = null;
			spot.res = null;
			foreach (FBCar item in FBVehicles.list)
			{
				if ((bool)item && Vector3.Distance(item.transform.position, spot.p) < 2.2f)
				{
					spot.car = item;
					break;
				}
			}
		}
		FBSemaforo.Build();
		target = (FBQuality.Mobile ? Mathf.Min(n, 6) : Mathf.Max(n, 11));
		float num = FBRoads.segs.Where((FBSeg s) => s.kind == 0).Sum((FBSeg s) => s.len) / 1000f;
		target = Mathf.Clamp(Mathf.RoundToInt(num * 2.6f), 4, target);
		for (int j = 0; j < target * 3; j++)
		{
			if (cars.Count((Car c) => c.mode != "gone") >= target)
			{
				break;
			}
			SpawnDriving(anywhere: true);
		}
		Debug.Log($"[FreeBala][trânsito] {cars.Count} carros em {num:0.0} km de faixas, {FBRoads.spots.Count} vagas");
	}

	public static void StartSpecialOnly()
	{
		Clear();
		if (!FBRoads.Ready)
		{
			return;
		}
		occ = new List<Car>[FBRoads.segs.Count];
		for (int i = 0; i < occ.Length; i++)
		{
			occ[i] = new List<Car>();
		}
		target = 0;
		FBSemaforo.Build();
		SpotsFromParked();
		foreach (FBSpot spot in FBRoads.spots)
		{
			spot.car = null;
			spot.res = null;
			foreach (FBCar item in FBVehicles.list)
			{
				if ((bool)item && Vector3.Distance(item.transform.position, spot.p) < 2.2f)
				{
					spot.car = item;
					break;
				}
			}
		}
	}

	public static Car SpawnSpecial(FBCar car, FBCharacter driver, string tag, float vCap, int seg = -1, float s = 0f)
	{
		if (!car || !driver || !Running)
		{
			return null;
		}
		if (seg < 0 && !LanePoint(out seg, out s, anywhere: false))
		{
			return null;
		}
		Vector3 tan;
		Vector3 vector = FBRoads.At(Seg(seg), s, out tan);
		Vector3 fwd = ((tan.Flat().sqrMagnitude > 1E-06f) ? tan.Flat().normalized : Vector3.forward);
		if (car.syncId == null)
		{
			int num = ++serial;
			car.syncId = "npc:" + tag + ":" + num;
		}
		car.aiLights = true;
		Car car2 = New(car);
		car2.actor = driver;
		car2.tag = tag;
		car2.vCap = vCap;
		car2.wantPark = false;
		car2.route.Add(seg);
		car2.s = s;
		car2.v = Mathf.Min(vCap, 3f);
		Extend(car2);
		car2.pos = vector;
		car2.fwd = fwd;
		car2.yaw = Mathf.Atan2(0f - fwd.x, fwd.z);
		car.transform.SetPositionAndRotation(vector, FBDrive.CarRot(0f, car2.yaw, 0f));
		car.yaw = car2.yaw;
		cars.Add(car2);
		Seat(car2);
		Pose(car2, 0f);
		return car2;
	}

	public static Car LeaveCurb(FBCar car, FBCharacter driver, string tag, float vCap)
	{
		if (!car || !driver || !Running)
		{
			return null;
		}
		Vector3 position = car.transform.position;
		Vector3 normalized = car.transform.forward.Flat().normalized;
		float s;
		float lat;
		FBSeg fBSeg = FBRoads.NearestLane(position, 9f, out s, out lat, normalized);
		if (fBSeg == null)
		{
			return null;
		}
		float num = s + 9f;
		if (num > fBSeg.len - 1.5f)
		{
			if (fBSeg.len - s < 4f)
			{
				return null;
			}
			num = fBSeg.len - 1.5f;
		}
		Vector3 tan;
		Vector3 b = FBRoads.At(fBSeg, num, out tan);
		Vector3[] outp = Bez(position, normalized, b, tan.Flat().normalized);
		FBSpot spot = new FBSpot
		{
			p = position,
			yaw = (0f - car.yaw) * 57.29578f,
			kind = "guia",
			seg = fBSeg.id,
			s = s,
			sin = s,
			sout = num,
			inp = new Vector3[0],
			outp = outp,
			rev = null
		};
		if (car.syncId == null)
		{
			int num2 = ++serial;
			car.syncId = "npc:" + tag + ":" + num2;
		}
		car.aiLights = true;
		Car car2 = New(car);
		car2.actor = driver;
		car2.tag = tag;
		car2.vCap = vCap;
		car2.wantPark = false;
		car2.spot = spot;
		car2.mode = "out_wait";
		car2.modeT = 1.2f + UnityEngine.Random.value;
		car2.yaw = car.yaw;
		car2.pos = position;
		car2.fwd = normalized;
		cars.Add(car2);
		Seat(car2);
		return car2;
	}

	public static bool FarLanePoint(out int seg, out float s)
	{
		return LanePoint(out seg, out s, anywhere: false);
	}

	public static void Leave(Car c)
	{
		if (c != null && !c.leave)
		{
			c.lure = false;
			c.leave = true;
			c.maxLife = c.life;
			int num = c.ri + 2;
			if (c.route.Count > num)
			{
				c.route.RemoveRange(num, c.route.Count - num);
			}
		}
	}

	public static bool Visible(Vector3 p)
	{
		return InView(p);
	}

	public static void Flee(Car c, Vector3 from)
	{
		if (c == null || c.mode == "gone")
		{
			return;
		}
		bool num = !c.fleeFrom.HasValue;
		c.fleeFrom = from;
		c.lure = false;
		c.leave = true;
		c.maxLife = c.life;
		c.vCap = 99f;
		c.cruise = 1.3f;
		c.amax = 3.6f;
		c.T = 0.95f;
		c.aggr = 1f;
		c.wantPark = false;
		if (num)
		{
			c.shockT = Mathf.Max(c.shockT, 0.4f + UnityEngine.Random.value * 0.3f);
			if ((bool)c.car)
			{
				c.car.Horn();
			}
		}
		int num2 = c.ri + 1;
		if (c.route.Count > num2)
		{
			c.route.RemoveRange(num2, c.route.Count - num2);
		}
	}

	public static void Forget(Car c)
	{
		if (c != null)
		{
			StopAudio(c);
			ReleaseRes(c);
			if (c.spot != null && c.spot.res == c)
			{
				c.spot.res = null;
			}
			c.onDrop = null;
			c.mode = "gone";
			cars.Remove(c);
		}
	}

	private static bool LanePoint(out int seg, out float s, bool anywhere)
	{
		seg = -1;
		s = 0f;
		Vector3 camPos = CamPos;
		List<FBSeg> list = FBRoads.segs.Where((FBSeg x) => x.kind == 0 && x.len > 18f && !x.ring).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		Camera main = Camera.main;
		Plane[] array = (main ? GeometryUtility.CalculateFrustumPlanes(main) : null);
		for (int i = 0; i < 60; i++)
		{
			FBSeg fBSeg = list[UnityEngine.Random.Range(0, list.Count)];
			float num = UnityEngine.Random.Range(6f, fBSeg.len - 6f);
			Vector3 tan;
			Vector3 vector = FBRoads.At(fBSeg, num, out tan);
			float num2 = Vector3.Distance(vector, camPos);
			if ((!anywhere && (num2 < 70f || num2 > 230f)) || (anywhere && num2 < 35f) || (!anywhere && array != null && num2 < 160f && GeometryUtility.TestPlanesAABB(array, new Bounds(vector + Vector3.up, new Vector3(5f, 3f, 5f)))))
			{
				continue;
			}
			bool flag = true;
			foreach (Car car in cars)
			{
				if ((bool)car.car && car.mode != "gone" && Vector3.Distance(car.pos, vector) < 22f)
				{
					flag = false;
					break;
				}
			}
			foreach (FBCar item in FBVehicles.list)
			{
				if ((bool)item && Vector3.Distance(item.transform.position, vector) < 9f)
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				seg = fBSeg.id;
				s = num;
				return true;
			}
		}
		return false;
	}

	private static void SpotsFromParked()
	{
		if (FBRoads.spots.Count > 0 || !FBRoads.Ready)
		{
			return;
		}
		foreach (FBCar item2 in FBVehicles.list)
		{
			if ((bool)item2 && !item2.driver && !(item2.kind == "moto") && !item2.ie)
			{
				Vector3 position = item2.transform.position;
				Vector3 normalized = item2.transform.forward.Flat().normalized;
				float s;
				float lat;
				FBSeg fBSeg = FBRoads.NearestLane(position, 9f, out s, out lat, normalized);
				if (fBSeg != null && !(s < 14f) && !(s > fBSeg.len - 14f))
				{
					float num = s - 10f;
					float num2 = s + 9f;
					Vector3 tan;
					Vector3 a = FBRoads.At(fBSeg, num, out tan);
					Vector3 tan2;
					Vector3 b = FBRoads.At(fBSeg, num2, out tan2);
					FBSpot item = new FBSpot
					{
						p = position,
						yaw = (0f - item2.yaw) * 57.29578f,
						kind = "guia",
						seg = fBSeg.id,
						s = s,
						sin = num,
						sout = num2,
						inp = Bez(a, tan.Flat().normalized, position, normalized),
						outp = Bez(position, normalized, b, tan2.Flat().normalized),
						rev = null
					};
					FBRoads.spots.Add(item);
					SpotsMade++;
				}
			}
		}
		if (SpotsMade == 0)
		{
			foreach (FBSeg item3 in FBRoads.segs.Where((FBSeg x) => x.kind == 0 && !x.ring && x.len > 60f && x.w >= 5f))
			{
				for (float num3 = 25f; num3 < item3.len - 25f; num3 += 80f)
				{
					Vector3 tan3;
					Vector3 vector = FBRoads.At(item3, num3, out tan3);
					tan3 = tan3.Flat().normalized;
					Vector3 vector2 = new Vector3(tan3.z, 0f, 0f - tan3.x);
					Vector3 vector3 = vector + vector2 * (item3.w / 2f - 0.95f);
					if (!Physics.CheckBox(vector3 + Vector3.up * 1f, new Vector3(1f, 0.6f, 2.4f), Quaternion.LookRotation(tan3), 2304, QueryTriggerInteraction.Ignore))
					{
						float? num4 = FBWorld.GroundY(vector3.x, vector3.y + 1.5f, vector3.z, 0.3f, 3f);
						if (num4.HasValue)
						{
							float valueOrDefault = num4.GetValueOrDefault();
							vector3.y = valueOrDefault;
							float num5 = num3 - 11f;
							float num6 = num3 + 9f;
							Vector3 tan4;
							Vector3 a2 = FBRoads.At(item3, num5, out tan4);
							Vector3 tan5;
							Vector3 b2 = FBRoads.At(item3, num6, out tan5);
							FBRoads.spots.Add(new FBSpot
							{
								p = vector3,
								yaw = (0f - Mathf.Atan2(0f - tan3.x, tan3.z)) * 57.29578f,
								kind = "rua",
								seg = item3.id,
								s = num3,
								sin = num5,
								sout = num6,
								inp = Bez(a2, tan4.Flat().normalized, vector3, tan3),
								outp = Bez(vector3, tan3, b2, tan5.Flat().normalized),
								rev = null
							});
							SpotsMade++;
						}
					}
				}
			}
		}
		Debug.Log($"[FreeBala][trânsito] vagas geradas: {SpotsMade}");
	}

	private static Car SpawnDriving(bool anywhere)
	{
		if (carIds == null)
		{
			carIds = GameData.Vehicles.Keys.Where((string k) => !Json.B(GameData.Vehicles[k], "viatura") && (bool)FBRegistry.I.Vehicle(k) && !EXCL.Any((string x) => k.Contains(x))).ToList();
		}
		if (carIds.Count == 0 || !LanePoint(out var seg, out var s, anywhere))
		{
			return null;
		}
		Vector3 tan;
		Vector3 vector = FBRoads.At(Seg(seg), s, out tan);
		float y = Quaternion.LookRotation((tan.Flat().sqrMagnitude > 1E-06f) ? tan.Flat() : Vector3.forward).eulerAngles.y;
		List<string> list = FBMxBike.IDS.Where(FBMxBike.Has).ToList();
		bool flag = list.Count > 0 && UnityEngine.Random.value < MotoShare && cars.Count((Car o) => (bool)o.car && (bool)o.car.mx) < 4;
		FBCar fBCar = (flag ? FBVehicles.Spawn("mx_" + FBUtil.Pick(list), vector, y) : FBVehicles.Make(FBUtil.Pick(carIds), vector, y, civil: true, null, UnityEngine.Random.value < 0.6f));
		if (!fBCar)
		{
			return null;
		}
		FBCharacter fBCharacter = Actor(vector);
		if (!fBCharacter)
		{
			FBVehicles.list.Remove(fBCar);
			UnityEngine.Object.Destroy(fBCar.gameObject);
			return null;
		}
		fBCar.syncId = "npc:car:" + ++serial;
		fBCar.aiLights = true;
		Car car = New(fBCar);
		car.actor = fBCharacter;
		car.route.Add(seg);
		car.s = s;
		car.v = UnityEngine.Random.Range(4f, 9f);
		Extend(car);
		car.pos = vector;
		car.fwd = tan.Flat().normalized;
		car.yaw = Mathf.Atan2(0f - car.fwd.x, car.fwd.z);
		cars.Add(car);
		Seat(car);
		Pose(car, 0f);
		if (flag && UnityEngine.Random.value < GarupaShare)
		{
			AddGarupa(car, vector);
		}
		return car;
	}

	public static int TestMotos(int n)
	{
		float motoShare = MotoShare;
		MotoShare = 1f;
		int num = 0;
		for (int i = 0; i < n * 4; i++)
		{
			if (num >= n)
			{
				break;
			}
			Car car = SpawnDriving(anywhere: false);
			if (car != null && (bool)car.car && (bool)car.car.mx)
			{
				num++;
			}
		}
		MotoShare = motoShare;
		return num;
	}

	public static bool ForceGarupa(Car c)
	{
		if (c == null || (bool)c.garupa || !c.car || !c.car.mx)
		{
			return false;
		}
		AddGarupa(c, c.pos);
		return c.garupa;
	}

	public static string Chain(Car c)
	{
		StringBuilder stringBuilder = new StringBuilder();
		HashSet<Car> hashSet = new HashSet<Car>();
		for (int i = 0; i < 4; i++)
		{
			if (c == null)
			{
				break;
			}
			if (!hashSet.Add(c))
			{
				break;
			}
			stringBuilder.Append(string.Format("{0}{1} {2} {3:0} km/h preso {4:0} s espera {5:0} s{6}{7} obsParado {8:0.0} frente: ", (i > 0) ? " <- " : "", c.car?.id, c.mode, c.v * 3.6f, c.stuck, c.waitT, c.hazard ? " alerta" : "", (c.overSide != 0) ? " ultrapassando" : "", c.obsStill));
			Car car = null;
			object lastWho = c.lastWho;
			if (!(lastWho is Car car2))
			{
				if (!(lastWho is FBCar fBCar))
				{
					if (!(lastWho is FBCharacter fBCharacter))
					{
						if (!(lastWho is string arg))
						{
							if (lastWho == null)
							{
								stringBuilder.Append("livre");
							}
							else
							{
								stringBuilder.Append($"{c.lastWho.GetType().Name} a {c.lastGap:0} u");
							}
						}
						else
						{
							stringBuilder.Append($"{arg} a {c.lastGap:0} u");
						}
					}
					else
					{
						stringBuilder.Append(string.Format("{0} a {1:0} u", fBCharacter.isPlayer ? "JOGADOR" : ("pedestre " + fBCharacter.skinId), c.lastGap));
					}
				}
				else
				{
					stringBuilder.Append(string.Format("veículo {0} {1} a {2:0} u", fBCar.id, (!fBCar.driver) ? "parado" : (fBCar.driver.isPlayer ? "do JOGADOR" : "com motorista"), c.lastGap));
				}
			}
			else
			{
				stringBuilder.Append($"trânsito a {c.lastGap:0} u");
				car = car2;
			}
			c = car;
		}
		return stringBuilder.ToString();
	}

	public static void GarupaDesce(FBCharacter g)
	{
		foreach (Car car in cars)
		{
			if (car.garupa == g)
			{
				car.garupa = null;
				car.garupaDown = false;
			}
		}
		FBRoleNpc component = g.GetComponent<FBRoleNpc>();
		if ((bool)component)
		{
			component.enabled = true;
			component.st = "walk";
			component.goalP = null;
		}
		if (!FBRole.npcs.Contains(g))
		{
			FBRole.npcs.Add(g);
		}
		GarupaDesceu++;
	}

	private static void AddGarupa(Car c, Vector3 p)
	{
		List<string> source = (from id in FBNpcAnims.Roster(FBSession.skin)
			where GameData.Skins.Exists((SkinDef s) => s.id == id && s.sex == "f")
			select id).ToList();
		List<string> list = source.Where((string id) => !FBMatch.All.Any((FBCharacter a) => (bool)a && !a.isPlayer && a.skinId == id)).ToList();
		if (list.Count == 0)
		{
			list = source.Where((string id) => !FBMatch.All.Any((FBCharacter a) => (bool)a && !a.isPlayer && a.skinId == id && (a.Pos - p).sqrMagnitude < 22500f)).ToList();
		}
		if (list.Count == 0 || FBGarupa.Clip == null)
		{
			GarupaSemSkin++;
			return;
		}
		FBCharacter fBCharacter = FBRole.SpawnNpc(FBUtil.Pick(list), p, "tg" + serial);
		if ((bool)fBCharacter)
		{
			FBRole.npcs.Remove(fBCharacter);
			FBRoleNpc component = fBCharacter.GetComponent<FBRoleNpc>();
			if ((bool)component)
			{
				component.enabled = false;
			}
			if ((bool)FBGarupa.Sit(fBCharacter, c.car, owned: true))
			{
				c.garupa = fBCharacter;
				GarupaSpawned++;
			}
			else
			{
				UnityEngine.Object.Destroy(fBCharacter.gameObject);
			}
		}
	}

	public static bool StartChase(bool force)
	{
		ChaseWhy = null;
		if (!Running)
		{
			ChaseWhy = "sem trânsito";
			return false;
		}
		if (ChaseCop != null)
		{
			ChaseWhy = "já tem perseguição";
			return false;
		}
		Vector3 pp = PlayerPos;
		Car f = (from c in cars.Where((Car c) => c.mode == "drive" && (bool)c.car && (bool)c.actor && !c.patrol && c.tag == null && c.chase == null && !c.leave && c.spot == null && !c.phys && c.ri < c.route.Count && Seg(c.route[c.ri]).kind == 0 && !DeadLane(Seg(c.route[c.ri])) && c.v > 3f && c.stuck < 1f).Where(delegate(Car c)
			{
				float magnitude = (c.pos - pp).Flat().magnitude;
				return magnitude > (float)(force ? 15 : 40) && magnitude < (float)(force ? 260 : 170);
			})
			orderby Mathf.Abs((c.pos - pp).Flat().magnitude - 90f)
			select c).FirstOrDefault();
		if (f == null)
		{
			ChaseWhy = "nenhum carro/moto bom para fugir";
			return false;
		}
		Car car = (from c in cars
			where c.patrol && c.fugitive == null && c.mode == "drive" && (bool)c.car && c.callLeft <= 0f && c.stuck < 1f && c.obsStill < 1f
			orderby (c.pos - f.pos).sqrMagnitude
			select c).FirstOrDefault((Car c) => (c.pos - f.pos).magnitude < 220f && (c.pos - f.pos).magnitude > 22f && !NeedsU(c, f));
		if (car == null)
		{
			int num = f.route[f.ri];
			float num2 = f.s - 28f;
			if (num2 < 4f && f.ri > 0)
			{
				int num3 = f.route[f.ri - 1];
				num2 = Seg(num3).len + num2;
				num = num3;
			}
			if (num2 < 4f)
			{
				ChaseWhy = "sem espaço atrás do fugitivo";
				return false;
			}
			if (Seg(num).kind == 1 && Seg(num).turn == 'U')
			{
				ChaseWhy = "fugitivo acabou de fazer meia-volta";
				return false;
			}
			Vector3 tan;
			Vector3 vector = FBRoads.At(Seg(num), num2, out tan);
			if (!force && FBStreetEvents.Visible(vector) && (vector - pp).magnitude < 90f)
			{
				ChaseWhy = "nasceria na vista do jogador";
				return false;
			}
			car = SpawnPatrol(num, num2);
			if (car == null)
			{
				ChaseWhy = "viatura não nasceu";
				return false;
			}
			car.v = Mathf.Max(6f, f.v);
		}
		car.fugitive = f;
		f.chase = car;
		car.chaseT = (f.chaseT = 0f);
		car.closeT = 0f;
		f.caught = false;
		car.runLights = (f.runLights = true);
		f.cruise = 1.6f;
		f.amax = 4.2f;
		f.T = 0.7f;
		f.aggr = 1f;
		f.vCap = 99f;
		f.wantPark = false;
		f.fleeFrom = car.pos;
		f.maxLife = 1E+09f;
		car.cruise = 1.62f;
		car.amax = 4.4f;
		car.T = 0.55f;
		car.wantPark = false;
		car.callLeft = 0f;
		ReplanFlee(f);
		for (int i = f.ri; i < f.route.Count; i++)
		{
			if (Seg(f.route[i]).kind == 1 && Seg(f.route[i]).turn == 'U')
			{
				EndChase(car, null, quiet: true);
				ChaseWhy = "fugitivo só sairia com meia-volta";
				return false;
			}
		}
		int num4 = car.route[car.ri];
		int num5 = f.route[f.ri];
		if (num4 != num5 && !f.route.Skip(f.ri).Contains(num4))
		{
			List<int> list = FBRoads.Route(num4, num5);
			if (list == null)
			{
				f.chase = null;
				car.fugitive = null;
				ChaseWhy = "sem caminho até o fugitivo";
				return false;
			}
			car.route.RemoveRange(car.ri + 1, car.route.Count - car.ri - 1);
			for (int j = 1; j < list.Count; j++)
			{
				car.route.Add(list[j]);
			}
		}
		else
		{
			car.route.RemoveRange(car.ri + 1, car.route.Count - car.ri - 1);
		}
		ChaseExtend(car);
		FBSiren.Set(car.car, 2);
		FBSiren.SetLights(car.car, on: true);
		ChaseStarts++;
		Debug.Log(string.Format("[FreeBala][trânsito] PERSEGUIÇÃO: viatura {0} atrás de {1} ({2}) a {3:0} m do jogador · distância viatura {4:0} m", car.car.id, f.car.id, (f.car.kind == "moto") ? "moto" : "carro", (f.pos - pp).Flat().magnitude, (car.pos - f.pos).magnitude));
		return true;
	}

	private static bool ChaseExtend(Car c)
	{
		Car fugitive = c.fugitive;
		if (fugitive == null || fugitive.mode == "gone" || fugitive.route.Count == 0)
		{
			return false;
		}
		List<int> route = c.route;
		int num = route[route.Count - 1];
		int num2 = fugitive.route.LastIndexOf(num);
		if (num2 < 0)
		{
			List<int> list = FBRoads.Route(num, fugitive.route[Mathf.Min(fugitive.ri, fugitive.route.Count - 1)]);
			if (list == null)
			{
				return false;
			}
			for (int i = 1; i < list.Count; i++)
			{
				c.route.Add(list[i]);
			}
			List<int> route2 = fugitive.route;
			List<int> route3 = c.route;
			num2 = route2.LastIndexOf(route3[route3.Count - 1]);
			if (num2 < 0)
			{
				return true;
			}
		}
		for (int j = num2 + 1; j < fugitive.route.Count; j++)
		{
			c.route.Add(fugitive.route[j]);
		}
		if (c.ri > 6)
		{
			int num3 = c.ri - 2;
			c.route.RemoveRange(0, num3);
			c.ri -= num3;
		}
		return true;
	}

	private static bool NeedsU(Car cop, Car f)
	{
		if (cop.route.Count == 0 || f.route.Count == 0 || cop.ri >= cop.route.Count)
		{
			return true;
		}
		int num = cop.route[cop.ri];
		int num2 = f.route[Mathf.Min(f.ri, f.route.Count - 1)];
		if (Seg(num).kind == 1 && Seg(num).turn == 'U')
		{
			return true;
		}
		if (num == num2)
		{
			return cop.s > f.s;
		}
		if (f.route.Skip(f.ri).Contains(num))
		{
			return true;
		}
		List<int> list = FBRoads.Route(num, num2);
		if (list == null)
		{
			return true;
		}
		foreach (int item in list)
		{
			if (Seg(item).kind == 1 && Seg(item).turn == 'U')
			{
				return true;
			}
		}
		return false;
	}

	private static void ReplanFlee(Car f)
	{
		if (f.ri < f.route.Count)
		{
			int num = f.ri + 1;
			float num2 = Seg(f.route[f.ri]).len - f.s;
			if (num < f.route.Count && num2 < 10f && (Seg(f.route[num]).kind != 1 || Seg(f.route[num]).turn != 'U'))
			{
				num++;
			}
			for (int i = num; i < f.route.Count; i++)
			{
				ReleaseRes(f, f.route[i]);
			}
			if (num < f.route.Count)
			{
				f.route.RemoveRange(num, f.route.Count - num);
			}
			Extend(f);
		}
	}

	private static void ChaseTick(float dt)
	{
		Car chaseCop = ChaseCop;
		if (chaseCop == null)
		{
			return;
		}
		Car fugitive = chaseCop.fugitive;
		chaseCop.chaseT += dt;
		if (fugitive != null)
		{
			fugitive.chaseT += dt;
		}
		string text = ((fugitive == null || fugitive.mode == "gone" || !fugitive.car) ? "fugitivo sumiu" : ((fugitive.mode == "moto_crash") ? "fugitivo caiu da moto" : ((chaseCop.mode != "drive") ? "viatura saiu da rota" : ((chaseCop.chaseT > (fugitive.caught ? 1E+09f : 75f)) ? "tempo" : (((fugitive.pos - PlayerPos).Flat().magnitude > 280f && (chaseCop.pos - PlayerPos).Flat().magnitude > 280f) ? "longe do jogador" : null)))));
		if (text == null && fugitive.caught && (fugitive.modeT -= dt) <= 0f)
		{
			text = "pegou: liberado";
		}
		if (text != null)
		{
			EndChase(chaseCop, text);
			return;
		}
		float magnitude = (chaseCop.pos - fugitive.pos).magnitude;
		ChaseGapSum += magnitude;
		ChaseGapN += 1f;
		ChaseVmax = Mathf.Max(ChaseVmax, fugitive.v);
		fugitive.fleeFrom = chaseCop.pos;
		List<int> route = chaseCop.route;
		int num = route[route.Count - 1];
		List<int> route2 = fugitive.route;
		if (num != route2[route2.Count - 1])
		{
			ChaseExtend(chaseCop);
		}
		if (!fugitive.caught)
		{
			if (magnitude < 12f && fugitive.v < 6f)
			{
				chaseCop.closeT += dt;
			}
			else
			{
				chaseCop.closeT = Mathf.Max(0f, chaseCop.closeT - dt * 0.5f);
			}
			if (chaseCop.closeT > 7f)
			{
				fugitive.caught = true;
				fugitive.vCap = 0f;
				fugitive.hazard = true;
				fugitive.modeT = 8f;
				chaseCop.vCap = 0f;
				ChaseCaught++;
				FBSiren.Set(chaseCop.car, 0);
				FBSiren.SetLights(chaseCop.car, on: true);
				Debug.Log($"[FreeBala][trânsito] PERSEGUIÇÃO: pegou {fugitive.car.id} depois de {chaseCop.chaseT:0} s");
			}
		}
		else
		{
			fugitive.v = Mathf.MoveTowards(fugitive.v, 0f, dt * 6f);
			fugitive.hazard = true;
		}
	}

	private static void EndChase(Car cop, string why, bool quiet = false)
	{
		Car fugitive = cop.fugitive;
		cop.fugitive = null;
		cop.runLights = false;
		cop.vCap = 99f;
		cop.cruise = 0.92f;
		cop.T = 1.6f;
		cop.amax = 2.2f;
		cop.callT = UnityEngine.Random.Range(60f, 150f);
		if ((bool)cop.car)
		{
			FBSiren.Set(cop.car, 0);
			FBSiren.SetLights(cop.car, on: false);
		}
		if (fugitive != null)
		{
			fugitive.chase = null;
			fugitive.runLights = false;
			fugitive.caught = false;
			fugitive.vCap = 99f;
			fugitive.hazard = false;
			fugitive.fleeFrom = null;
			fugitive.cruise = 1.05f;
			fugitive.amax = 2.6f;
			fugitive.T = 1.2f;
			fugitive.leave = true;
		}
		if (!quiet)
		{
			ChaseEnds++;
			chaseCd = UnityEngine.Random.Range(160f, 320f);
			ChaseWhy = why;
			Debug.Log($"[FreeBala][trânsito] PERSEGUIÇÃO terminou: {why} · {cop.chaseT:0} s");
		}
	}

	private static Car SpawnPatrol(int seg, float s)
	{
		return SpawnPatrolAt(seg, s);
	}

	private static Car SpawnPatrol()
	{
		if (!LanePoint(out var seg, out var s, anywhere: false))
		{
			return null;
		}
		return SpawnPatrolAt(seg, s);
	}

	private static Car SpawnPatrolAt(int seg, float s)
	{
		string text = FBVehicles.PickViatura(() => UnityEngine.Random.value);
		if (text == null)
		{
			return null;
		}
		Vector3 tan;
		Vector3 vector = FBRoads.At(Seg(seg), s, out tan);
		FBCar fBCar = FBVehicles.Make(text, vector, Quaternion.LookRotation((tan.Flat().sqrMagnitude > 1E-06f) ? tan.Flat() : Vector3.forward).eulerAngles.y, civil: false, () => UnityEngine.Random.value);
		if (!fBCar)
		{
			return null;
		}
		string text2 = FBStreetEvents.FreeIds(FBNpcAnims.Police).FirstOrDefault();
		FBCharacter fBCharacter = null;
		if (text2 != null)
		{
			fBCharacter = FBRole.SpawnNpc(text2, vector, "tp" + serial);
			FBRole.npcs.Remove(fBCharacter);
			FBRoleNpc component = fBCharacter.GetComponent<FBRoleNpc>();
			if ((bool)component)
			{
				component.enabled = false;
			}
		}
		if ((object)fBCharacter == null)
		{
			fBCharacter = Actor(vector);
		}
		if (!fBCharacter)
		{
			FBVehicles.list.Remove(fBCar);
			UnityEngine.Object.Destroy(fBCar.gameObject);
			return null;
		}
		fBCar.syncId = "npc:pm:" + ++serial;
		fBCar.aiLights = true;
		Car car = New(fBCar);
		car.actor = fBCharacter;
		car.patrol = true;
		car.wantPark = false;
		car.cruise = 0.92f;
		car.T = 1.6f;
		car.callT = UnityEngine.Random.Range(40f, 110f);
		car.route.Add(seg);
		car.s = s;
		car.v = 5f;
		Extend(car);
		car.pos = vector;
		car.fwd = tan.Flat().normalized;
		car.yaw = Mathf.Atan2(0f - car.fwd.x, car.fwd.z);
		cars.Add(car);
		Seat(car);
		Pose(car, 0f);
		return car;
	}

	private static void PatrolTick(Car c, float dt)
	{
		if (c.fugitive != null)
		{
			return;
		}
		if (c.callLeft > 0f)
		{
			c.callLeft -= dt;
			if (c.callLeft <= 0f)
			{
				FBSiren.Set(c.car, 0);
				FBSiren.SetLights(c.car, on: false);
				c.cruise = 0.92f;
				c.T = 1.6f;
				c.amax = 2.2f;
				c.callT = UnityEngine.Random.Range(60f, 150f);
			}
		}
		else if (!((c.callT -= dt) > 0f) && !(c.mode != "drive"))
		{
			c.callLeft = UnityEngine.Random.Range(25f, 45f);
			c.cruise = 1.25f;
			c.T = 1f;
			c.amax = 3.2f;
			PatrolCalls++;
			FBSiren.Set(c.car, (UnityEngine.Random.value < 0.5f) ? 1 : 2, 0.95f);
		}
	}

	private static Car New(FBCar car)
	{
		Car car2 = new Car
		{
			id = ++serial,
			car = car,
			aggr = UnityEngine.Random.value
		};
		car2.cruise = Mathf.Lerp(0.86f, 1.08f, car2.aggr);
		car2.T = Mathf.Lerp(1.65f, 1.15f, car2.aggr);
		car2.amax = Mathf.Lerp(1.7f, 2.8f, car2.aggr);
		car2.wantPark = UnityEngine.Random.value < 0.4f;
		Bounds lbox = car.lbox;
		car2.halfL = Mathf.Max(1.6f, lbox.extents.z * car.scale);
		car2.halfW = Mathf.Max(0.75f, lbox.extents.x * car.scale);
		if ((bool)car.mx)
		{
			car2.halfL = 1.15f;
			car2.halfW = 0.5f;
			car2.cruise = Mathf.Lerp(0.95f, 1.18f, car2.aggr);
			car2.amax = Mathf.Lerp(2.4f, 3.6f, car2.aggr);
			car2.T = Mathf.Lerp(1.3f, 0.9f, car2.aggr);
			car2.wantPark = false;
		}
		return car2;
	}

	private static FBCharacter Actor(Vector3 p)
	{
		List<string> list = (from id in FBNpcAnims.Roster(FBSession.skin)
			where !FBMatch.All.Any((FBCharacter a) => (bool)a && !a.isPlayer && a.skinId == id)
			select id).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		FBCharacter fBCharacter = FBRole.SpawnNpc(FBUtil.Pick(list), p, "t" + serial);
		FBRole.npcs.Remove(fBCharacter);
		FBRoleNpc component = fBCharacter.GetComponent<FBRoleNpc>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		return fBCharacter;
	}

	private static void Seat(Car c)
	{
		if ((bool)c.actor && (bool)c.car)
		{
			FBDrive.ApplyRemote(c.actor, StateOf(c), 0f, npc: true);
			FBCarSound.StopEngine("veh_remote_" + c.car.syncId + "_e_");
		}
	}

	private static List<object> StateOf(Car c)
	{
		float[] array = Coords.ToWeb(c.car.transform.position);
		return new List<object>
		{
			c.car.syncId,
			(double)array[0],
			(double)array[1],
			(double)array[2],
			(double)c.yaw,
			(double)c.pitch,
			(double)c.roll,
			(double)c.steer,
			(double)c.car.spin,
			(double)c.v,
			0.0,
			0.0
		};
	}

	private static void Extend(Car c)
	{
		if (c.fugitive != null && ChaseExtend(c))
		{
			return;
		}
		float num = 0f - c.s;
		for (int i = c.ri; i < c.route.Count; i++)
		{
			num += Seg(c.route[i]).len;
		}
		int num2 = 0;
		int num3;
		for (; num < 180f; num += Seg(num3).len)
		{
			if (num2++ >= 40)
			{
				break;
			}
			List<int> route = c.route;
			FBSeg fBSeg = Seg(route[route.Count - 1]);
			if (fBSeg.next.Length == 0)
			{
				break;
			}
			num3 = Choose(fBSeg, c);
			c.route.Add(num3);
		}
		if (c.ri > 6)
		{
			int num4 = c.ri - 2;
			c.route.RemoveRange(0, num4);
			c.ri -= num4;
		}
	}

	private static int Choose(FBSeg L, Car c = null)
	{
		if (L.next.Length == 1)
		{
			return L.next[0];
		}
		Vector3? vector = null;
		if (c != null && c.lure && (bool)FBMatch.Player)
		{
			Vector3 vector2 = (PlayerPos - c.pos).Flat();
			if (vector2.magnitude > 35f)
			{
				vector = vector2.normalized;
			}
		}
		if (c != null && c.leave && (bool)FBMatch.Player)
		{
			Vector3 vector3 = (c.pos - PlayerPos).Flat();
			if (vector3.sqrMagnitude > 1f)
			{
				vector = vector3.normalized;
			}
		}
		if (c != null)
		{
			Vector3? fleeFrom = c.fleeFrom;
			if (fleeFrom.HasValue)
			{
				Vector3 valueOrDefault = fleeFrom.GetValueOrDefault();
				Vector3 vector4 = (c.pos - valueOrDefault).Flat();
				if (vector4.sqrMagnitude > 1f)
				{
					vector = vector4.normalized;
				}
			}
		}
		float num = 0f;
		float[] array = new float[L.next.Length];
		for (int i = 0; i < array.Length; i++)
		{
			FBSeg fBSeg = Seg(L.next[i]);
			char c2 = ((fBSeg.kind == 1) ? fBSeg.turn : 'S');
			float num2 = c2 switch
			{
				'L' => 0.2f, 
				'R' => 0.25f, 
				'S' => 0.55f, 
				_ => 0.02f, 
			};
			bool flag = c != null && (c.runLights || c.chase != null || c.fugitive != null);
			if (flag && c2 == 'U')
			{
				num2 *= 0.01f;
			}
			int i2 = ((fBSeg.kind == 1 && fBSeg.next.Length != 0) ? fBSeg.next[0] : fBSeg.id);
			if (Seg(i2).kind == 0 && DeadLane(Seg(i2)))
			{
				num2 *= (flag ? 0.005f : 0.15f);
			}
			if (vector.HasValue)
			{
				Vector3 valueOrDefault2 = vector.GetValueOrDefault();
				FBSeg fBSeg2 = Seg(i2);
				Vector3 vector5;
				if (fBSeg2.p.Length == 0)
				{
					Vector3[] p = fBSeg.p;
					vector5 = p[p.Length - 1];
				}
				else
				{
					Vector3[] p2 = fBSeg2.p;
					vector5 = p2[p2.Length - 1];
				}
				Vector3 vector6 = (vector5 - fBSeg.p[0]).Flat();
				if (vector6.sqrMagnitude > 1f)
				{
					num2 *= Mathf.Lerp(0.35f, 2.6f, (Vector3.Dot(vector6.normalized, valueOrDefault2) + 1f) / 2f);
				}
			}
			array[i] = num2;
			num += num2;
		}
		float num3 = UnityEngine.Random.value * num;
		for (int j = 0; j < array.Length; j++)
		{
			num3 -= array[j];
			if (num3 <= 0f)
			{
				return L.next[j];
			}
		}
		int[] next = L.next;
		return next[next.Length - 1];
	}

	private static bool DeadLane(FBSeg S)
	{
		if (dead.TryGetValue(S.id, out var value))
		{
			return value;
		}
		value = S.next.Length != 0 && S.next.All((int n) => Seg(n).kind == 1 && Seg(n).turn == 'U');
		dead[S.id] = value;
		return value;
	}

	public static void Arrive(FBCharacter ch)
	{
		Car car2 = cars.Find((Car x) => x.walker == ch);
		FBRoleNpc component = ch.GetComponent<FBRoleNpc>();
		if (car2 == null)
		{
			if ((bool)component)
			{
				component.st = "walk";
			}
			return;
		}
		FBCar car = car2.car;
		if (!car || (bool)car.driver || car.remoteDriver >= 0)
		{
			car2.mode = "gone";
			if ((bool)component)
			{
				component.st = "walk";
			}
			return;
		}
		FBRole.npcs.Remove(ch);
		if ((bool)component)
		{
			component.enabled = false;
		}
		car2.actor = ch;
		car2.walker = null;
		car.aiLights = true;
		FBCar fBCar = car;
		if (fBCar.syncId == null)
		{
			fBCar.syncId = "npc:car:" + car2.id;
		}
		car2.yaw = car.yaw;
		car2.pos = car.transform.position;
		car2.fwd = car.transform.forward;
		Seat(car2);
		float num = 0f;
		if (car.kind == "moto")
		{
			FBDrive.StartMount(ch, car);
			FBDriverPose component2 = ch.GetComponent<FBDriverPose>();
			if ((bool)component2 && component2.trans == "mount")
			{
				num = component2.transLen + 0.4f;
				InsDone++;
			}
		}
		else if (FBDrive.StartCarIn(ch, car))
		{
			num = ch.GetComponent<FBDriverPose>().transLen + 0.4f;
			InsDone++;
		}
		else
		{
			FBAudio.PlayAt("veh_door_open", car.transform.position, 0.4f);
		}
		car.StartCoroutine(After(num, delegate
		{
			if ((bool)car)
			{
				FBAudio.PlayAt("veh_start", car.transform.position, 0.35f, 5f);
			}
		}));
		if (car2.spot == null)
		{
			car2.mode = "gone";
			return;
		}
		car2.mode = "out_wait";
		car2.modeT = num + 1.2f + UnityEngine.Random.value;
	}

	public static void Update(float dt)
	{
		if (!FBRoads.Ready || occ.Length != FBRoads.segs.Count)
		{
			return;
		}
		FBSemaforo.Tick(dt);
		List<Car>[] array = occ;
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Clear();
		}
		foreach (Car car in cars)
		{
			if ((bool)car.car && (car.mode == "drive" || car.mode == "kturn") && car.ri < car.route.Count)
			{
				occ[car.route[car.ri]].Add(car);
			}
		}
		array = occ;
		foreach (List<Car> list in array)
		{
			if (list.Count > 1)
			{
				list.Sort((Car a, Car b) => a.s.CompareTo(b.s));
			}
		}
		Requests();
		PedestrianTakesCar(dt);
		Vector3 cam = CamPos;
		int num = 0;
		MotoPhysOn = 0;
		foreach (Car car2 in cars)
		{
			if (car2.phys)
			{
				MotoPhysOn++;
			}
		}
		foreach (Car item in cars.OrderBy((Car c) => (!c.car) ? 1E+12f : (c.pos - cam).sqrMagnitude))
		{
			item.life += dt;
			if (item.mode == "gone")
			{
				continue;
			}
			if (item.mode == "walk")
			{
				FBRoleNpc fBRoleNpc = (item.walker ? item.walker.GetComponent<FBRoleNpc>() : null);
				if (!item.walker || !item.walker.alive || fBRoleNpc == null || fBRoleNpc.st != "gocar" || item.life > 50f)
				{
					if ((bool)fBRoleNpc && fBRoleNpc.st == "gocar")
					{
						fBRoleNpc.st = "walk";
					}
					if (item.spot != null && item.spot.res == item)
					{
						item.spot.res = null;
					}
					item.mode = "gone";
				}
				continue;
			}
			if (!item.car || !item.actor || ((bool)item.car.driver && item.car.driver != item.actor))
			{
				Drop(item);
				continue;
			}
			switch (item.mode)
			{
			case "drive":
				Drive(item, dt);
				break;
			case "kturn":
				KTurn(item, dt);
				break;
			case "park_in":
				ParkIn(item, dt);
				break;
			case "parked":
				Parked(item, dt);
				break;
			case "exiting":
				if ((item.modeT -= dt) <= 0f)
				{
					OutDone(item);
				}
				break;
			case "moto_crash":
				MotoCrashTick(item, dt);
				break;
			case "wait_vanish":
				item.v = 0f;
				item.brake = true;
				item.hazard = true;
				if (CanVanish(item))
				{
					Respawn(item);
				}
				break;
			case "out_wait":
				OutWait(item, dt);
				break;
			case "out_rev":
			case "out_fwd":
				ParkOut(item, dt);
				break;
			}
			if (!(item.mode == "gone") && (bool)item.car)
			{
				bool flag = (item.pos - cam).sqrMagnitude < 2500f && num < 3;
				if (flag)
				{
					num++;
				}
				Pose(item, dt, flag);
				Recycle(item);
			}
		}
		cars.RemoveAll((Car c) => c.mode == "gone");
		if ((spawnCd -= dt) <= 0f && cars.Count((Car c) => c.mode != "walk" && c.tag == null) < target)
		{
			spawnCd = 4f;
			SpawnDriving(anywhere: false);
		}
		if (target > 0 && (chaseCd -= dt) <= 0f)
		{
			chaseCd = 45f;
			if (!FBRacha.Active)
			{
				StartChase(force: false);
			}
		}
		if (target > 0 && (patrolCd -= dt) <= 0f)
		{
			patrolCd = 20f;
			if (cars.Count((Car c) => c.patrol) < (FBQuality.Mobile ? 1 : 2))
			{
				SpawnPatrol();
			}
		}
		foreach (Car car3 in cars)
		{
			if (car3.patrol && (bool)car3.car)
			{
				PatrolTick(car3, dt);
			}
		}
		ChaseTick(dt);
	}

	private static void Drop(Car c)
	{
		if (c.onDrop != null)
		{
			Action<Car> onDrop = c.onDrop;
			c.onDrop = null;
			StopAudio(c);
			ReleaseRes(c);
			c.mode = "gone";
			onDrop(c);
			return;
		}
		if (c.phys && (bool)c.car && !c.car.driver)
		{
			PhysOff(c);
		}
		c.phys = false;
		StopAudio(c);
		if ((bool)c.actor)
		{
			FBDrive.Release(c.actor);
			UnityEngine.Object.Destroy(c.actor.gameObject);
		}
		if ((bool)c.garupa)
		{
			UnityEngine.Object.Destroy(c.garupa.gameObject);
		}
		ReleaseRes(c);
		if (c.spot != null && c.spot.res == c)
		{
			c.spot.res = null;
		}
		c.mode = "gone";
	}

	private static void Drive(Car c, float dt)
	{
		Extend(c);
		FBSeg fBSeg = Seg(c.route[c.ri]);
		if (fBSeg.kind == 1 && fBSeg.turn == 'U')
		{
			StartKTurn(c);
			return;
		}
		float num = Mathf.Min(fBSeg.vmax, 14f) * c.cruise;
		float num2 = 0f - c.s;
		float num3 = 12f + c.v * c.v / 4.5f;
		for (int i = c.ri; i < c.route.Count; i++)
		{
			if (!(num2 < num3))
			{
				break;
			}
			FBSeg fBSeg2 = Seg(c.route[i]);
			for (int j = 0; j < fBSeg2.p.Length; j += 2)
			{
				float num4 = num2 + fBSeg2.s[j];
				if (!(num4 < 0f) && !(num4 > num3))
				{
					float num5 = Mathf.Sqrt(fBSeg2.vcurv[j] * fBSeg2.vcurv[j] + 4.8f * num4);
					if (num5 < num)
					{
						num = num5;
					}
				}
			}
			float num6 = fBSeg2.vmax * c.cruise;
			if (num2 > 0f)
			{
				num6 = Mathf.Sqrt(num6 * num6 + 4.8f * num2);
			}
			num = Mathf.Min(num, num6);
			num2 += fBSeg2.len;
		}
		num = Mathf.Min(num, c.vCap);
		if (c.shockT > 0f)
		{
			c.shockT -= dt;
			num = 0f;
		}
		float num7 = 1E+09f;
		float num8 = 0f;
		num2 = 0f - c.s;
		object obj = null;
		for (int k = c.ri; k < c.route.Count; k++)
		{
			if (!(num2 < 80f))
			{
				break;
			}
			int num9 = c.route[k];
			foreach (Car item in occ[num9])
			{
				if (item == c || (c.overSide != 0 && num9 == c.overSeg && Mathf.Abs(c.lat - item.lat) > 1.9f))
				{
					continue;
				}
				float s = item.s;
				if (k != c.ri || !(s <= c.s))
				{
					float num10 = num2 + s - c.halfL - item.halfL;
					if (num10 < num7)
					{
						num7 = num10;
						num8 = Mathf.Max(0f, item.v);
						obj = item;
					}
				}
			}
			num2 += Seg(num9).len;
		}
		float num11 = 0f - c.s;
		for (int l = c.ri; l < c.route.Count; l++)
		{
			num11 += Seg(c.route[l]).len;
		}
		List<int> route = c.route;
		if (Seg(route[route.Count - 1]).next.Length == 0)
		{
			float num12 = num11 - c.halfL - 1f;
			if (num12 < num7)
			{
				num7 = num12;
				num8 = 0f;
				obj = "fim";
			}
		}
		c.nextConn = -1;
		num2 = 0f - c.s;
		c.blink = 0;
		for (int m = c.ri; m < c.route.Count; m++)
		{
			if (!(num2 < 60f))
			{
				break;
			}
			FBSeg fBSeg3 = Seg(c.route[m]);
			if (fBSeg3.kind != 1)
			{
				num2 += fBSeg3.len;
				continue;
			}
			if (m == c.ri)
			{
				num2 += fBSeg3.len;
				continue;
			}
			c.nextConn = fBSeg3.id;
			c.eta = num2 / Mathf.Max(2.5f, c.v);
			if (num2 < 42f && (fBSeg3.turn == 'L' || fBSeg3.turn == 'R'))
			{
				c.blink = ((fBSeg3.turn != 'L') ? 1 : (-1));
			}
			HashSet<Car> value;
			bool flag = res.TryGetValue(fBSeg3.id, out value) && value.Contains(c);
			if (c.v > 2f)
			{
				c.sigHeld = false;
			}
			int num13 = ((!flag && m > 0 && !c.runLights) ? FBSemaforo.StateOf(c.route[m - 1]) : 0);
			if (num13 >= 2)
			{
				float num14 = num2 - 1.2f - c.halfL;
				float num15 = c.v * c.v / 6.4f + 1.5f;
				if (num13 == 3 || num14 > num15)
				{
					if (num14 < num7)
					{
						num7 = num14;
						num8 = 0f;
						obj = "semaforo";
					}
					c.waitT = 0f;
					if (!c.sigHeld && c.v < 0.5f)
					{
						c.sigHeld = true;
						SigStops++;
					}
					break;
				}
			}
			if (flag)
			{
				break;
			}
			float num16 = c.v * c.v / 6.4f + 5f;
			if (CanEnter(c, fBSeg3, m) || c.runLights)
			{
				if (num2 < num16 + 4f)
				{
					Reserve(c, fBSeg3.id);
				}
				break;
			}
			float num17 = num2 - 1.2f - c.halfL;
			if (num17 < num7)
			{
				num7 = num17;
				num8 = 0f;
				obj = "linha";
			}
			c.waitT += dt;
			break;
		}
		if ((c.sensT -= dt) <= 0f)
		{
			c.sensT = 0.1f + UnityEngine.Random.value * 0.05f;
			Sense(c);
		}
		if (c.gapObs < num7)
		{
			num7 = c.gapObs;
			num8 = c.vObs;
			obj = c.obsWho;
		}
		c.lastWho = obj;
		c.lastGap = num7;
		if (c.runLights)
		{
			leadWho_(c, obj, num7);
		}
		object obj2 = obj as Car;
		if (obj2 == null)
		{
			if (c.runLights)
			{
				FBCar ywf = obj as FBCar;
				if ((object)ywf != null)
				{
					obj2 = cars.Find((Car x) => x.car == ywf);
					goto IL_064c;
				}
			}
			obj2 = null;
		}
		goto IL_064c;
		IL_064c:
		Car car = (Car)obj2;
		if (c.runLights && car != null && car != c.fugitive && car != c.chase && car.mode == "drive" && num7 < 35f)
		{
			if (car.yieldT <= 0f)
			{
				Car.Yields++;
			}
			car.yieldT = 3f;
		}
		if (c.yieldT > 0f)
		{
			c.yieldT -= dt;
			num = 0f;
			if (c.overSide == 0)
			{
				c.latT = 1.1f;
			}
			c.yielded = true;
		}
		else if (c.yielded)
		{
			c.yielded = false;
			if (c.overSide == 0)
			{
				c.latT = 0f;
			}
		}
		Overtake(c, dt, num7, num8, obj);
		if (c.runLights)
		{
			FBCar pc = obj as FBCar;
			if ((object)pc != null && !cars.Exists((Car x) => x.car == pc) && c.v < 0.6f && num7 < 4f)
			{
				if ((c.pushT += dt) > 1.5f)
				{
					c.pushT = 0f;
					Vector3 normalized = Vector3.Cross(Vector3.up, c.fwd).normalized;
					float num18 = ((Vector3.Dot(pc.transform.position - c.pos, normalized) >= 0f) ? 1 : (-1));
					FBCarDamage.Shove(pc, normalized * num18 * 4.5f + c.fwd * 2f, num18 * 25f);
					FBAudio.PlayAt("reb_carimpact3", pc.transform.position, 0.45f, 5f);
					Car.Pushes++;
				}
				goto IL_0871;
			}
		}
		c.pushT = 0f;
		goto IL_0871;
		IL_0871:
		if (obj is FBPropBody fBPropBody && (bool)fBPropBody.rb && !fBPropBody.rb.isKinematic && c.v < 0.6f && num7 < 3f && c.overSide == 0 && c.obsStill > (c.runLights ? 0.6f : 2.5f))
		{
			Vector3 normalized2 = c.fwd.Flat().normalized;
			Vector3 vector = new Vector3(normalized2.z, 0f, 0f - normalized2.x);
			if (Vector3.Dot(fBPropBody.transform.position - c.pos, vector) < 0f)
			{
				vector = -vector;
			}
			fBPropBody.rb.WakeUp();
			fBPropBody.rb.AddForce((vector * 3.2f + normalized2 * 1.6f + Vector3.up * 0.9f) * fBPropBody.rb.mass, ForceMode.Impulse);
			c.obsStill = 0f;
			PropPushes++;
		}
		float num19 = 2.2f + Mathf.Max(0f, c.v * c.T + c.v * (c.v - num8) / (2f * Mathf.Sqrt(c.amax * 3.2f)));
		float num20 = c.amax * (1f - Mathf.Pow(Mathf.Clamp01(c.v / Mathf.Max(0.5f, num)), 4f));
		if (num7 < 100000000f)
		{
			num20 -= c.amax * (num19 / Mathf.Max(0.25f, num7)) * (num19 / Mathf.Max(0.25f, num7));
		}
		if (num < c.v)
		{
			num20 = Mathf.Min(num20, (num - c.v) * 1.6f);
		}
		num20 = (c.acc = Mathf.Clamp(num20, -9f, c.amax));
		c.v = Mathf.Max(0f, c.v + num20 * dt);
		if (num7 < 0.15f)
		{
			c.v = 0f;
		}
		c.brake = num20 < -1f || (c.v < 0.3f && (num7 < 6f || num < 0.5f));
		c.hazard = c.stuck > 14f || c.shockT > 0f;
		if (c.v < 0.3f)
		{
			c.stuck += dt;
			if ((obj is Car || obj is FBCharacter || obj is FBCar) && (c.honkT += dt) > 2.4f + 2f * (1f - c.aggr))
			{
				c.honkT = -3f - UnityEngine.Random.value * 4f;
				c.car.Horn();
			}
		}
		else
		{
			c.stuck = Mathf.Max(0f, c.stuck - dt * 3f);
			c.honkT = 0f;
			c.waitT = 0f;
		}
		if (c.stuck > 45f)
		{
			Respawn(c);
			return;
		}
		c.s += c.v * dt;
		while (c.ri < c.route.Count - 1 && c.s > Seg(c.route[c.ri]).len)
		{
			c.s -= Seg(c.route[c.ri]).len;
			FBSeg fBSeg4 = Seg(c.route[c.ri]);
			if (fBSeg4.kind == 1)
			{
				ReleaseRes(c, fBSeg4.id);
			}
			c.ri++;
			Extend(c);
		}
		c.lat = Mathf.MoveTowards(c.lat, c.latT, dt * (1.2f + c.v * 0.06f));
		if (c.wantPark && c.spot == null && c.life > 35f && UnityEngine.Random.value < dt * ((c.life > 90f) ? 2f : 0.2f))
		{
			PickSpot(c);
		}
		if (c.spot != null && c.route[c.ri] == c.spot.seg && c.s >= c.spot.sin - 0.5f && Mathf.Abs(c.lat) < 0.3f)
		{
			BeginParkIn(c);
		}
		else if (c.spot != null)
		{
			c.blink = ((c.route[c.ri] != c.spot.seg || !(c.spot.sin - c.s < 30f)) ? c.blink : ((SideOf(c.spot) > 0f) ? 1 : (-1)));
		}
	}

	private static float SideOf(FBSpot sp)
	{
		FBSeg s = Seg(sp.seg);
		FBRoads.At(s, sp.s, out var tan);
		Vector3 tan2;
		Vector3 vector = FBRoads.At(s, sp.s, out tan2);
		Vector3 vector2 = sp.p - vector;
		return vector2.x * tan.z - vector2.z * tan.x;
	}

	private static int Pri(char t)
	{
		return t switch
		{
			'R' => 2, 
			'S' => 3, 
			_ => (t == 'L') ? 1 : 0, 
		};
	}

	private static void Requests()
	{
		reqs.Clear();
		foreach (Car car in cars)
		{
			if (car.mode == "drive" && car.nextConn >= 0 && car.eta < 12f)
			{
				reqs.Add((car, car.nextConn, car.eta, Pri(Seg(car.nextConn).turn)));
			}
		}
	}

	private static bool Conflict(FBSeg A, int b)
	{
		int[] conf = A.conf;
		for (int i = 0; i < conf.Length; i++)
		{
			if (conf[i] == b)
			{
				return true;
			}
		}
		return false;
	}

	private static bool CanEnter(Car c, FBSeg C, int k)
	{
		int[] conf = C.conf;
		foreach (int num in conf)
		{
			if (res.TryGetValue(num, out var value) && value.Count > 0 && !value.Contains(c))
			{
				return Deadlock(c);
			}
			if (occ[num].Count > 0)
			{
				return Deadlock(c);
			}
		}
		if (k + 1 < c.route.Count)
		{
			int num2 = c.route[k + 1];
			List<Car> list = occ[num2];
			if (list.Count > 0 && list[0].s < c.halfL * 2f + 3.5f && list[0].v < 2.5f)
			{
				return false;
			}
		}
		float eta = c.eta;
		int num3 = Pri(C.turn);
		foreach (var (car, b, num4, num5) in reqs)
		{
			if (car != c && Conflict(C, b) && (num4 < eta - 0.6f || (Mathf.Abs(num4 - eta) <= 0.6f && (num5 > num3 || (num5 == num3 && car.id < c.id)))))
			{
				return c.waitT > 9f && car.v < 0.2f && car.waitT > 4f && car.id > c.id;
			}
		}
		return true;
	}

	private static bool Deadlock(Car c)
	{
		if (c.waitT > 14f && c.v < 0.2f)
		{
			return UnityEngine.Random.value < 0.02f;
		}
		return false;
	}

	private static void Reserve(Car c, int conn)
	{
		if (!res.TryGetValue(conn, out var value))
		{
			value = (res[conn] = new HashSet<Car>());
		}
		value.Add(c);
	}

	private static void ReleaseRes(Car c, int conn = -1)
	{
		if (conn >= 0)
		{
			if (res.TryGetValue(conn, out var value))
			{
				value.Remove(c);
			}
			return;
		}
		foreach (HashSet<Car> value2 in res.Values)
		{
			value2.Remove(c);
		}
	}

	private static int Corridor(Car c, float look)
	{
		int num = 0;
		float[] obj = new float[10] { 0.5f, 2f, 4f, 6.5f, 9.5f, 13f, 17f, 22f, 28f, 35f };
		bool flag = c.mp != null && c.mp.Length > 1 && (c.mode == "park_in" || c.mode == "out_fwd" || c.mode == "out_rev");
		float[] array = obj;
		foreach (float num2 in array)
		{
			if (num2 > look)
			{
				break;
			}
			float num3 = c.lat;
			Vector3 vector;
			Vector3 tan;
			if (flag)
			{
				vector = PathAt(c, c.mpos + c.halfL + num2, out tan);
				num3 = 0f;
			}
			else
			{
				vector = Ahead(c, c.halfL + num2, out tan);
			}
			Vector3 vector2 = new Vector3(tan.z, 0f, 0f - tan.x);
			SP[num] = vector + vector2 * num3;
			ST[num] = tan;
			SD[num] = num2;
			num++;
		}
		return num;
	}

	private static void Sense(Car c)
	{
		float look = Mathf.Clamp(6f + c.v * 2.4f, 10f, 35f);
		int num = Corridor(c, look);
		c.gapObs = 1E+09f;
		c.vObs = 0f;
		c.obsWho = null;
		if (num == 0)
		{
			return;
		}
		foreach (FBCharacter item in FBMatch.All)
		{
			if (!item || !item.alive || item.vehiclePose || item == c.actor)
			{
				continue;
			}
			Vector3 pos = item.Pos;
			if ((pos - c.pos).sqrMagnitude > 2025f)
			{
				continue;
			}
			Vector3 q = pos + item.velocity.Flat() * 0.8f;
			for (int i = 0; i < num; i++)
			{
				float w = c.halfW + 0.65f;
				if (PtHit(SP[i], ST[i], pos, w, 1.4f) || PtHit(SP[i], ST[i], q, w, 1.4f))
				{
					Hit(Mathf.Max(0f, SD[i] - 1.2f), 0f, item);
					break;
				}
			}
		}
		foreach (FBPropBody item2 in FBFisica.All)
		{
			if (!item2 || !item2.rb || item2.rb.mass < 1.5f)
			{
				continue;
			}
			Vector3 position = item2.transform.position;
			if ((position - c.pos).sqrMagnitude > 1600f)
			{
				continue;
			}
			for (int j = 0; j < num; j++)
			{
				if (PtHit(SP[j], ST[j], position, c.halfW + 0.45f, 1f))
				{
					Hit(Mathf.Max(0f, SD[j] - 0.8f), 0f, item2);
					PropStops++;
					break;
				}
			}
		}
		foreach (FBCar car in FBVehicles.list)
		{
			if (!car || car == c.car)
			{
				continue;
			}
			Vector3 position2 = car.transform.position;
			if ((position2 - c.pos).sqrMagnitude > 2500f)
			{
				continue;
			}
			Car car2 = cars.Find((Car x) => x.car == car);
			if (car2 != null && car2.mode == "drive" && Mathf.Abs(car2.lat) < 0.4f && occ.Length != 0)
			{
				bool flag = false;
				for (int k = c.ri; k < c.route.Count && k < c.ri + 6; k++)
				{
					if (c.route[k] == car2.route[car2.ri])
					{
						flag = true;
						break;
					}
				}
				if (flag)
				{
					continue;
				}
			}
			Transform transform = car.transform;
			Bounds lbox = car.lbox;
			Vector3 vector = lbox.extents * car.scale;
			Vector3 vector2 = lbox.center * car.scale;
			for (int l = 0; l < num; l++)
			{
				Vector3 vector3 = transform.InverseTransformPoint(SP[l]) * car.scale;
				vector3 -= vector2;
				Vector3 vector4 = Quaternion.Inverse(transform.rotation) * ST[l];
				float num2 = vector.x + c.halfW * Mathf.Abs(vector4.z) + c.halfL * 0.15f * Mathf.Abs(vector4.x) + 0.3f;
				float num3 = vector.z + c.halfW * Mathf.Abs(vector4.x) + 0.35f;
				if (Mathf.Abs(vector3.x) <= num2 && Mathf.Abs(vector3.z) <= num3)
				{
					float b = ((car2 != null) ? (car2.v * Vector3.Dot(car2.fwd, ST[l])) : (car.driver ? Mathf.Max(0f, car.v * Vector3.Dot(transform.forward, ST[l])) : 0f));
					Hit(Mathf.Max(0f, SD[l] - 0.6f), Mathf.Max(0f, b), car);
					break;
				}
			}
			if ((bool)car.driver && car.driver.isPlayer && Mathf.Abs(car.v) > 3f && (position2 - c.pos).magnitude < c.halfL + 2.6f && c.shockT <= 0f)
			{
				c.shockT = 2.5f + UnityEngine.Random.value * 2f;
				c.car.Horn();
			}
		}
		void Hit(float d, float v, object who)
		{
			if (d < c.gapObs)
			{
				c.gapObs = d;
				c.vObs = v;
				c.obsWho = who;
			}
		}
	}

	private static bool PtHit(Vector3 a, Vector3 t, Vector3 q, float w, float half)
	{
		Vector3 vector = q - a;
		vector.y = 0f;
		float f = vector.x * t.x + vector.z * t.z;
		float num = Mathf.Abs(vector.x * t.z - vector.z * t.x);
		if (Mathf.Abs(f) <= half)
		{
			return num <= w;
		}
		return false;
	}

	private static Vector3 Ahead(Car c, float dist, out Vector3 tan)
	{
		if (c.route.Count == 0)
		{
			tan = c.fwd.Flat().normalized;
			if (tan.sqrMagnitude < 0.5f)
			{
				tan = Vector3.forward;
			}
			return c.pos + tan * dist;
		}
		float num = c.s + dist;
		int i;
		for (i = Mathf.Clamp(c.ri, 0, c.route.Count - 1); i < c.route.Count - 1 && num > Seg(c.route[i]).len; i++)
		{
			num -= Seg(c.route[i]).len;
		}
		Vector3 result = FBRoads.At(Seg(c.route[i]), num, out tan);
		tan.y = 0f;
		tan.Normalize();
		return result;
	}

	private static void leadWho_(Car c, object who, float gap)
	{
		object leadWho;
		if (who != null)
		{
			object obj;
			if (!(who is Car))
			{
				if (!(who is string text))
				{
					FBCar fc = who as FBCar;
					obj = (((object)fc != null) ? (fc.id + " (" + (cars.Exists((Car x) => x.car == fc) ? "trânsito" : (fc.driver ? "com motorista" : "parado")) + ")") : who.GetType().Name);
				}
				else
				{
					obj = text;
				}
			}
			else
			{
				obj = "carro";
			}
			leadWho = (string)obj + ((gap < 100000000f) ? $" a {gap:0} u" : "");
		}
		else
		{
			leadWho = "livre";
		}
		c.leadWho = (string)leadWho;
	}

	private static void Overtake(Car c, float dt, float gap, float vLead, object who)
	{
		FBSeg fBSeg = Seg(c.route[c.ri]);
		bool flag = (who is FBCar || who is FBPropBody || (who is Car car && (car.mode != "drive" || car.hazard || car.stuck > 6f || car.yieldT > 0f))) && vLead < 0.6f && gap < 18f;
		c.obsStill = (flag ? (c.obsStill + dt) : 0f);
		if (c.overSide != 0)
		{
			if (c.s > c.passUntil || fBSeg.id != c.overSeg)
			{
				c.latT = 0f;
				c.overSide = 0;
			}
			return;
		}
		float dist;
		float lat;
		if (c.runLights && fBSeg.kind == 0 && gap < 6f && c.v < 7f)
		{
			object obj = who as Car;
			if (obj == null)
			{
				FBCar wf = who as FBCar;
				obj = (((object)wf != null) ? cars.Find((Car x) => x.car == wf) : null);
			}
			Car car2 = (Car)obj;
			if (car2 != null && car2 != c.fugitive && car2 != c.chase && (car2.yielded || car2.yieldT > 0f))
			{
				if (fBSeg.opp >= 0 && !fBSeg.one)
				{
					FBSeg fBSeg2 = Seg(fBSeg.opp);
					foreach (Car car3 in cars)
					{
						if (car3 != c && !(car3.mode != "drive") && car3.ri < car3.route.Count && car3.route[car3.ri] == fBSeg2.id)
						{
							float num = FBRoads.Project(fBSeg, car3.pos, out dist, out lat);
							if (num > c.s - 3f && num < c.s + 25f)
							{
								return;
							}
						}
					}
				}
				c.overSide = -1;
				c.overSeg = fBSeg.id;
				c.latT = Mathf.Max(-2.6f, car2.lat - (car2.halfW + c.halfW + 0.45f));
				c.passUntil = c.s + gap + 2f * (car2.halfL + c.halfL) + 2f;
				c.obsStill = 0f;
				SirenPasses++;
				return;
			}
		}
		if (c.obsStill < (c.runLights ? 0.5f : 2.5f) || fBSeg.kind != 0 || c.v > (float)(c.runLights ? 7 : 4))
		{
			return;
		}
		if (fBSeg.left >= 0 && fBSeg.one)
		{
			FBSeg fBSeg3 = Seg(fBSeg.left);
			float dist2;
			float num2 = FBRoads.Project(fBSeg3, c.pos, out lat, out dist2);
			if (dist2 > 6f)
			{
				return;
			}
			foreach (Car item in occ[fBSeg3.id])
			{
				if (item.s > num2 - 12f && item.s < num2 + 14f)
				{
					return;
				}
			}
			Vector3 tan;
			Vector3 vector = FBRoads.At(fBSeg3, num2, out tan);
			float lat2 = Vector3.Dot(rhs: new Vector3(tan.z, 0f, 0f - tan.x), lhs: c.pos - vector);
			ReleaseRes(c);
			c.route.Clear();
			c.route.Add(fBSeg3.id);
			c.ri = 0;
			c.s = num2;
			c.lat = lat2;
			c.latT = 0f;
			Extend(c);
			c.obsStill = 0f;
			c.blink = -1;
		}
		else
		{
			if (fBSeg.opp < 0 || fBSeg.one)
			{
				return;
			}
			FBSeg fBSeg4 = Seg(fBSeg.opp);
			foreach (Car car4 in cars)
			{
				if (car4 != c && !(car4.mode != "drive") && car4.ri < car4.route.Count && car4.route[car4.ri] == fBSeg4.id)
				{
					float num3 = FBRoads.Project(fBSeg, car4.pos, out lat, out dist);
					if (num3 > c.s - 5f && num3 < c.s + 75f)
					{
						return;
					}
				}
			}
			float latT = 0f - 2f * Mathf.Clamp(fBSeg.w / 4f, 1.3f, 2.7f) - 0.2f;
			if (!(c.s + gap + 11f > fBSeg.len - 4f))
			{
				c.overSide = -1;
				c.overSeg = fBSeg.id;
				c.latT = latT;
				c.passUntil = c.s + gap + 11f;
				c.obsStill = 0f;
			}
		}
	}

	private static void PickSpot(Car c)
	{
		int num = c.route[c.ri];
		FBSpot fBSpot = null;
		List<int> collection = null;
		float num2 = 1E+09f;
		foreach (FBSpot spot in FBRoads.spots)
		{
			if ((bool)spot.car || spot.res != null || spot.inp.Length < 2)
			{
				continue;
			}
			float num3 = Vector3.Distance(spot.p, c.pos);
			if (num3 > 220f || num3 > num2)
			{
				continue;
			}
			bool flag = false;
			foreach (FBCar item in FBVehicles.list)
			{
				if ((bool)item && Vector3.Distance(item.transform.position, spot.p) < 2.4f)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				List<int> list = ((num == spot.seg && c.s < spot.sin - 6f) ? new List<int> { num } : FBRoads.Route(num, spot.seg));
				if (list != null)
				{
					num2 = num3;
					fBSpot = spot;
					collection = list;
				}
			}
		}
		if (fBSpot == null)
		{
			c.wantPark = UnityEngine.Random.value < 0.5f;
			return;
		}
		ReleaseRes(c);
		fBSpot.res = c;
		c.spot = fBSpot;
		c.route.Clear();
		c.route.AddRange(collection);
		c.ri = 0;
	}

	private static void BeginParkIn(Car c)
	{
		FBSpot spot = c.spot;
		ReleaseRes(c);
		c.mode = "park_in";
		SetPath(c, spot.inp, rev: false);
		c.blink = ((SideOf(spot) > 0f) ? 1 : (-1));
		c.latT = (c.lat = 0f);
	}

	private static void SetPath(Car c, Vector3[] p, bool rev)
	{
		c.mp = p;
		c.ms = new float[p.Length];
		float num = 0f;
		for (int i = 0; i < p.Length; i++)
		{
			if (i > 0)
			{
				num += Vector3.Distance(p[i - 1], p[i]);
			}
			c.ms[i] = num;
		}
		c.mlen = num;
		c.mpos = 0f;
		c.mrev = rev;
	}

	private static Vector3 PathAt(Car c, float d, out Vector3 tan)
	{
		Vector3[] mp = c.mp;
		if (mp.Length < 2)
		{
			tan = c.fwd;
			if (mp.Length == 0)
			{
				return c.pos;
			}
			return mp[0];
		}
		d = Mathf.Clamp(d, 0f, c.mlen);
		int i;
		for (i = 1; i < mp.Length - 1 && c.ms[i] < d; i++)
		{
		}
		float t = (d - c.ms[i - 1]) / Mathf.Max(0.0001f, c.ms[i] - c.ms[i - 1]);
		tan = (mp[i] - mp[i - 1]).Flat().normalized;
		return Vector3.Lerp(mp[i - 1], mp[i], t);
	}

	private static bool FollowPath(Car c, float dt, float vmax, bool stopAtEnd)
	{
		float num = c.mlen - c.mpos;
		float num2 = (stopAtEnd ? Mathf.Min(vmax, Mathf.Sqrt(3.2f * Mathf.Max(0f, num)) + 0.25f) : vmax);
		c.v = Mathf.MoveTowards(c.v, num2, dt * 2.2f);
		c.mpos += c.v * dt;
		c.acc = 0f;
		c.brake = c.v < num2 - 0.3f || num < 1.5f;
		Vector3 tan;
		Vector3 pos = PathAt(c, c.mpos, out tan);
		c.pos = pos;
		c.fwd = (c.mrev ? (-tan) : tan);
		if (c.fwd.sqrMagnitude < 1E-06f)
		{
			c.fwd = c.car.transform.forward;
		}
		c.reverse = c.mrev;
		c.yaw = Mathf.Atan2(0f - c.fwd.x, c.fwd.z);
		return c.mpos >= c.mlen - 0.05f;
	}

	private static void ParkIn(Car c, float dt)
	{
		Sense(c);
		if (c.gapObs < 1.2f)
		{
			c.v = 0f;
			c.brake = true;
		}
		else if (FollowPath(c, dt, 2.6f, stopAtEnd: true))
		{
			FBSpot spot = c.spot;
			c.pos = spot.p;
			c.yaw = (0f - spot.yaw) * ((float)Math.PI / 180f);
			c.fwd = FBDrive.Fwd(c.yaw);
			c.v = 0f;
			c.mode = "parked";
			c.modeT = 1f + UnityEngine.Random.value;
			spot.car = c.car;
			spot.res = null;
			c.blink = 0;
			c.reverse = false;
		}
	}

	private static void Parked(Car c, float dt)
	{
		c.brake = false;
		c.blink = 0;
		if ((c.modeT -= dt) > 0f)
		{
			return;
		}
		FBCar car = c.car;
		FBCharacter actor = c.actor;
		FBDriverPose fBDriverPose = (actor ? actor.GetComponent<FBDriverPose>() : null);
		if (c.mode == "parked" && (bool)fBDriverPose && ((car.kind == "moto") ? FBDrive.StartDismount(actor, car) : FBDrive.StartCarOut(actor, car)))
		{
			StopAudio(c);
			FBAudio.PlayAt("veh_stop", car.transform.position, 0.35f, 5f);
			c.mode = "exiting";
			c.modeT = fBDriverPose.transLen + 2f;
			fBDriverPose.onOut = delegate
			{
				OutDone(c);
			};
		}
		else
		{
			OutDone(c);
		}
	}

	private static void OutDone(Car c)
	{
		if (c.mode == "gone")
		{
			return;
		}
		FBCar car = c.car;
		FBCharacter actor = c.actor;
		if (!car || !actor)
		{
			c.mode = "gone";
			return;
		}
		StopAudio(c);
		Vector3 at = car.L2W(car.rigOut ?? new Vector3(-1.6f, 0f, 0f));
		float yaw = actor.Yaw;
		bool flag = c.mode == "exiting" && FBDrive.OutPoint(actor, car, out at, out yaw);
		FBDrive.Release(actor);
		if (!flag)
		{
			float? num = FBWorld.GroundY(at.x, at.y + 2f, at.z, 0.5f);
			if (num.HasValue)
			{
				float valueOrDefault = num.GetValueOrDefault();
				at.y = valueOrDefault;
			}
		}
		CharacterController component = actor.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		actor.transform.position = at;
		if ((bool)component)
		{
			component.enabled = true;
		}
		actor.transform.rotation = Quaternion.Euler(0f, flag ? yaw : actor.Yaw, 0f);
		if (flag)
		{
			actor.Yaw = yaw;
		}
		if (flag && FBDoors.HasPart(car, 1))
		{
			FBDoors.SetMask(car, FBDoors.Mask(car) & -2, sound: true);
		}
		FBRoleNpc component2 = actor.GetComponent<FBRoleNpc>();
		if ((bool)component2)
		{
			component2.enabled = true;
			component2.st = "walk";
			component2.goalP = null;
		}
		FBRole.npcs.Add(actor);
		actor.SetWeaponVisible(v: false);
		if (!flag)
		{
			FBAudio.PlayAt("veh_door_close", car.transform.position, 0.4f);
		}
		car.aiLights = false;
		car.v = 0f;
		OutsDone++;
		c.actor = null;
		c.mode = "gone";
	}

	private static void OutWait(Car c, float dt)
	{
		c.brake = true;
		c.blink = -SideOfSign(c.spot);
		if (!((c.modeT -= dt) > 0f))
		{
			FBSpot spot = c.spot;
			if (spot.rev != null && spot.rev.Length > 1)
			{
				SetPath(c, spot.rev, rev: true);
				c.mode = "out_rev";
			}
			else
			{
				SetPath(c, spot.outp, rev: false);
				c.mode = "out_fwd";
			}
			if (spot.car == c.car)
			{
				spot.car = null;
			}
		}
	}

	private static int SideOfSign(FBSpot sp)
	{
		if (sp == null || !(SideOf(sp) > 0f))
		{
			return -1;
		}
		return 1;
	}

	private static void ParkOut(Car c, float dt)
	{
		FBSpot spot = c.spot;
		if (c.mode == "out_rev")
		{
			if (FollowPath(c, dt, 1.5f, stopAtEnd: true))
			{
				SetPath(c, spot.outp, rev: false);
				c.mode = "out_fwd";
				c.v = 0f;
			}
			return;
		}
		if (c.mpos < 0.5f)
		{
			bool flag = true;
			Seg(spot.seg);
			foreach (Car car in cars)
			{
				if (car != c && car.mode == "drive" && car.ri < car.route.Count && car.route[car.ri] == spot.seg && car.s > spot.sout - 32f && car.s < spot.sout + 6f)
				{
					flag = false;
					break;
				}
			}
			Sense(c);
			if (c.gapObs < 3f)
			{
				flag = false;
			}
			if (!flag)
			{
				c.v = 0f;
				c.brake = true;
				c.blink = ((SideOfSign(spot) <= 0) ? 1 : (-1));
				return;
			}
		}
		c.blink = ((SideOfSign(spot) <= 0) ? 1 : (-1));
		if (FollowPath(c, dt, 4f, stopAtEnd: false))
		{
			c.mode = "drive";
			c.route.Clear();
			c.route.Add(spot.seg);
			c.ri = 0;
			c.s = spot.sout;
			c.lat = (c.latT = 0f);
			c.reverse = false;
			c.spot = null;
			spot.res = null;
			c.life = 0f;
			c.wantPark = UnityEngine.Random.value < 0.25f;
			Extend(c);
		}
	}

	private static void PedestrianTakesCar(float dt)
	{
		if (target <= 0 || (enterCd -= dt) > 0f || cars.Count((Car t) => t.mode == "drive" && t.tag == null) >= target + 2)
		{
			return;
		}
		enterCd = 25f + UnityEngine.Random.value * 35f;
		Vector3 pl = PlayerPos;
		List<FBSpot> list = FBRoads.spots.Where(delegate(FBSpot sp)
		{
			if ((bool)sp.car && sp.res == null && !sp.car.ie && sp.car != FBRole.roleCar && !sp.car.driver && sp.car.remoteDriver < 0)
			{
				FBParedao.Wall wall = FBParedao.State(sp.car);
				if ((wall == null || !wall.on) && !(sp.car.syncId ?? "").StartsWith("role:"))
				{
					return !cars.Any((Car t) => t.car == sp.car);
				}
			}
			return false;
		}).Where(delegate(FBSpot sp)
		{
			float num = Vector3.Distance(sp.p, pl);
			return num > 25f && num < 160f;
		}).ToList();
		if (list.Count != 0)
		{
			FBSpot fBSpot = FBUtil.Pick(list);
			FBCar car = fBSpot.car;
			Vector3 door = car.L2W(car.rigOut ?? new Vector3(-1.6f, 0f, 0f));
			FBCharacter fBCharacter = (from o in FBRole.npcs
				where (bool)o && o.alive && o.GetComponent<FBRoleNpc>()?.st == "walk" && (o.Pos - door).magnitude < 60f
				orderby (o.Pos - door).sqrMagnitude
				select o).FirstOrDefault();
			if ((bool)fBCharacter)
			{
				FBRoleNpc component = fBCharacter.GetComponent<FBRoleNpc>();
				component.st = "gocar";
				component.goalP = door;
				component.stT = 45f;
				Car car2 = New(car);
				car2.walker = fBCharacter;
				car2.mode = "walk";
				car2.spot = fBSpot;
				fBSpot.res = car2;
				car2.yaw = car.yaw;
				car2.pos = car.transform.position;
				car2.fwd = car.transform.forward;
				cars.Add(car2);
			}
		}
	}

	private static void StartKTurn(Car c)
	{
		if (c.runLights)
		{
			ChaseKTurns++;
		}
		if (c.ri + 1 >= c.route.Count)
		{
			Extend(c);
		}
		FBSeg fBSeg = Seg(c.route[Mathf.Min(c.route.Count - 1, c.ri + 1)]);
		Vector3 tan;
		Vector3 qq = FBRoads.At(fBSeg, Mathf.Min(6f, fBSeg.len), out tan);
		Vector3 normalized = tan.Flat().normalized;
		ReleaseRes(c);
		c.v = Mathf.Min(c.v, 2f);
		c.kWait = 0f;
		if (!PlanTurn(c, c.pos, c.fwd.Flat().normalized, qq, normalized))
		{
			Respawn(c);
			return;
		}
		c.mode = "kturn";
		c.mstage = 0;
		SetPath(c, c.legs[0].p, c.legs[0].rev);
	}

	private static bool PlanTurn(Car c, Vector3 p, Vector3 f, Vector3 qq, Vector3 ft)
	{
		c.legs.Clear();
		float r = Mathf.Max(4.6f, c.car ? (c.car.wheelbase * 1.85f) : 4.8f);
		bool flag = false;
		int num = 0;
		for (int i = 0; i < 8; i++)
		{
			if (num >= 2)
			{
				break;
			}
			float num2 = 0f - Vector3.SignedAngle(f, ft, Vector3.up);
			if (num2 < -5f)
			{
				num2 += 360f;
			}
			if (!flag && num2 < 85f)
			{
				Vector3[] array = Bez(p, f, qq, ft);
				if (LegFits(c, array, f, 0f, bez: true))
				{
					c.legs.Add((array, false));
					return true;
				}
			}
			Vector3[] array2 = null;
			float deg = 0f;
			for (float num3 = 8f; num3 <= Mathf.Clamp(num2, 8f, 100f); num3 += 6f)
			{
				Vector3[] array3 = Arc(p, f, r, num3, !flag);
				if (!LegFits(c, array3, f, num3, bez: false))
				{
					break;
				}
				array2 = array3;
				deg = num3;
			}
			if (array2 == null)
			{
				num++;
				flag = !flag;
				continue;
			}
			num = 0;
			c.legs.Add((array2, flag));
			Vector3[] array4 = array2;
			p = array4[array4.Length - 1];
			f = Rot(f, deg);
			flag = !flag;
		}
		return false;
	}

	private static bool LegFits(Car c, Vector3[] P, Vector3 f0, float deg, bool bez)
	{
		for (int i = 1; i < P.Length; i++)
		{
			Vector3 f = (bez ? (P[i] - P[i - 1]) : Rot(f0, deg * (float)i / (float)(P.Length - 1)));
			if (!Fits(c, P[i], f))
			{
				return false;
			}
		}
		return true;
	}

	private static bool Fits(Car c, Vector3 p, Vector3 f)
	{
		f.y = 0f;
		if (f.sqrMagnitude < 1E-06f)
		{
			return true;
		}
		f.Normalize();
		Vector3 vector = new Vector3(f.z, 0f, 0f - f.x);
		float num = Mathf.Max(0.8f, c.halfL - 0.3f);
		float num2 = Mathf.Max(0.4f, c.halfW - 0.25f);
		for (int i = 0; i < 6; i++)
		{
			float num3 = ((i < 2) ? num : ((i < 4) ? (0f - num) : 0f));
			float num4 = (((i & 1) == 0) ? num2 : (0f - num2));
			if (!FBRoads.IsRoad(p + f * num3 + vector * num4))
			{
				return false;
			}
		}
		foreach (FBCar item in FBVehicles.list)
		{
			if ((bool)item && !(item == c.car) && !((item.transform.position - p).sqrMagnitude > 196f) && BoxHit(p, f, c.halfL + 0.3f, c.halfW + 0.25f, item))
			{
				return false;
			}
		}
		return true;
	}

	private static void FillBox(Vector2[] q, Vector3 ctr, Vector3 fw, float l, float w)
	{
		Vector3 vector = new Vector3(fw.z, 0f, 0f - fw.x);
		Vector2 vector2 = new Vector2(ctr.x, ctr.z);
		Vector2 vector3 = new Vector2(fw.x, fw.z) * l;
		Vector2 vector4 = new Vector2(vector.x, vector.z) * w;
		q[0] = vector2 + vector3 + vector4;
		q[1] = vector2 + vector3 - vector4;
		q[2] = vector2 - vector3 - vector4;
		q[3] = vector2 - vector3 + vector4;
	}

	public static bool BoxHit(Vector3 p, Vector3 f, float hl, float hw, FBCar o)
	{
		FillBox(bxA, p, f, hl, hw);
		Bounds lbox = o.lbox;
		Vector3 vector = lbox.extents * o.scale;
		Vector3 forward = o.transform.forward;
		forward.y = 0f;
		forward = ((forward.sqrMagnitude > 1E-06f) ? forward.normalized : Vector3.forward);
		FillBox(bxB, o.transform.TransformPoint(lbox.center), forward, vector.z, vector.x);
		for (int i = 0; i < 2; i++)
		{
			Vector2[] array = ((i == 0) ? bxA : bxB);
			for (int j = 0; j < 4; j++)
			{
				Vector2 vector2 = array[(j + 1) % 4] - array[j];
				Vector2 rhs = new Vector2(0f - vector2.y, vector2.x);
				float num = 1E+09f;
				float num2 = -1E+09f;
				float num3 = 1E+09f;
				float num4 = -1E+09f;
				Vector2[] array2 = bxA;
				for (int k = 0; k < array2.Length; k++)
				{
					float b = Vector2.Dot(array2[k], rhs);
					num = Mathf.Min(num, b);
					num2 = Mathf.Max(num2, b);
				}
				array2 = bxB;
				for (int k = 0; k < array2.Length; k++)
				{
					float b2 = Vector2.Dot(array2[k], rhs);
					num3 = Mathf.Min(num3, b2);
					num4 = Mathf.Max(num4, b2);
				}
				if (num2 < num3 || num4 < num)
				{
					return false;
				}
			}
		}
		return true;
	}

	private static Vector3 Rot(Vector3 f, float deg)
	{
		return Quaternion.Euler(0f, 0f - deg, 0f) * f;
	}

	private static Vector3[] Arc(Vector3 p, Vector3 f, float R, float deg, bool centerLeft)
	{
		Vector3 vector = new Vector3(0f - f.z, 0f, f.x);
		Vector3 vector2 = (centerLeft ? (p + vector * R) : (p - vector * R));
		Vector3[] array = new Vector3[11];
		for (int i = 0; i <= 10; i++)
		{
			array[i] = vector2 + Quaternion.Euler(0f, (0f - deg) * (float)i / 10f, 0f) * (p - vector2);
			array[i].y = p.y;
		}
		return array;
	}

	private static Vector3[] Bez(Vector3 a, Vector3 da, Vector3 b, Vector3 db)
	{
		float num = Vector3.Distance(a, b) * 0.4f;
		Vector3[] array = new Vector3[14];
		for (int i = 0; i < array.Length; i++)
		{
			float num2 = (float)i / ((float)array.Length - 1f);
			float num3 = 1f - num2;
			array[i] = num3 * num3 * num3 * a + 3f * num3 * num3 * num2 * (a + da * num) + 3f * num3 * num2 * num2 * (b - db * num) + num2 * num2 * num2 * b;
		}
		return array;
	}

	private static void KTurn(Car c, float dt)
	{
		c.blink = 2;
		if (c.legs.Count == 0 || c.mstage >= c.legs.Count)
		{
			Replan(c);
			return;
		}
		if (KBlocked(c))
		{
			c.v = 0f;
			c.brake = true;
			if ((c.kWait += dt) > 6f)
			{
				c.kWait = 0f;
				Replan(c);
			}
			return;
		}
		c.kWait = 0f;
		if (FollowPath(c, dt, c.legs[c.mstage].rev ? 1.4f : 2f, stopAtEnd: true))
		{
			c.mstage++;
			c.v = 0f;
			if (c.mstage < c.legs.Count)
			{
				SetPath(c, c.legs[c.mstage].p, c.legs[c.mstage].rev);
				return;
			}
			int num = Mathf.Min(c.route.Count - 1, c.ri + 1);
			FBSeg fBSeg = Seg(c.route[num]);
			c.ri = num;
			c.s = Mathf.Min(6f, fBSeg.len);
			c.mode = "drive";
			c.reverse = false;
			c.blink = 0;
			c.lat = (c.latT = 0f);
			Extend(c);
		}
	}

	private static bool KBlocked(Car c)
	{
		float num = c.mpos + 1.5f;
		if (num > c.mlen + 0.5f)
		{
			return false;
		}
		Vector3 tan;
		Vector3 vector = PathAt(c, num, out tan);
		Vector3 vector2 = (c.mrev ? (-tan) : tan);
		foreach (FBCar item in FBVehicles.list)
		{
			if ((bool)item && !(item == c.car) && !((item.transform.position - vector).sqrMagnitude > 144f) && BoxHit(vector, vector2, c.halfL + 0.15f, c.halfW + 0.1f, item))
			{
				return true;
			}
		}
		foreach (FBCharacter item2 in FBMatch.All)
		{
			if ((bool)item2 && item2.alive && !item2.vehiclePose && !(item2 == c.actor))
			{
				Vector3 pos = item2.Pos;
				if (!((pos - vector).sqrMagnitude > 9f) && PtHit(vector, vector2, pos, c.halfW + 0.5f, c.halfL + 0.5f))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void Replan(Car c)
	{
		int index = Mathf.Min(c.route.Count - 1, c.ri + 1);
		FBSeg fBSeg = Seg(c.route[index]);
		Vector3 tan;
		Vector3 qq = FBRoads.At(fBSeg, Mathf.Min(6f, fBSeg.len), out tan);
		if (!PlanTurn(c, c.pos, c.fwd.Flat().normalized, qq, tan.Flat().normalized))
		{
			Respawn(c);
			return;
		}
		c.mstage = 0;
		SetPath(c, c.legs[0].p, c.legs[0].rev);
	}

	private static void Pose(Car c, float dt, bool audio = false)
	{
		FBCar car = c.car;
		if (c.mode == "drive")
		{
			Vector3 tan;
			Vector3 vector = FBRoads.At(Seg(c.route[c.ri]), c.s, out tan);
			Vector3 vector2 = (Ahead(c, 1.6f, out var _) - vector).Flat();
			vector2 = ((vector2.sqrMagnitude > 0.0001f) ? vector2.normalized : tan.Flat().normalized);
			Vector3 vector3 = new Vector3(vector2.z, 0f, 0f - vector2.x);
			vector += vector3 * c.lat;
			if (Mathf.Abs(c.lat - c.latT) > 0.02f)
			{
				vector2 = (vector2 + vector3 * Mathf.Clamp((c.latT - c.lat) * 0.25f, -0.3f, 0.3f)).normalized;
			}
			c.pos = vector;
			c.fwd = vector2;
			c.yaw = Mathf.Atan2(0f - vector2.x, vector2.z);
			c.reverse = false;
			Ahead(c, 4.5f, out var tan3);
			float num = Vector3.SignedAngle(tan.Flat(), tan3.Flat(), Vector3.up) * ((float)Math.PI / 180f) / 4.5f;
			c.steer = Mathf.Lerp(c.steer, Mathf.Clamp(0f - Mathf.Atan(car.wheelbase * num), -0.6f, 0.6f), Mathf.Min(1f, dt * 6f));
		}
		else if (c.mode == "kturn" || c.mode == "park_in" || c.mode == "out_rev" || c.mode == "out_fwd")
		{
			PathAt(c, c.mpos + (c.mrev ? (-1.5f) : 1.5f), out var tan4);
			float num2 = Vector3.SignedAngle(c.fwd, c.mrev ? (-tan4) : tan4, Vector3.up) * ((float)Math.PI / 180f) / 1.5f;
			c.steer = Mathf.Lerp(c.steer, Mathf.Clamp((float)(c.mrev ? 1 : (-1)) * Mathf.Atan(car.wheelbase * num2), -0.6f, 0.6f), Mathf.Min(1f, dt * 6f));
		}
		Vector3 pos = c.pos;
		float magnitude = (pos - CamPos).magnitude;
		if ((bool)car.mx && MotoPhys(c, car, magnitude, dt))
		{
			car.lBrake = c.brake;
			car.lRev = false;
			car.lBlink = (c.hazard ? 2 : c.blink);
			car.aiLights = true;
			ActorVis(c, car, magnitude);
			return;
		}
		if (magnitude < 70f)
		{
			car.SetColliders(on: false);
			float? num3 = FBWorld.GroundY(pos.x, pos.y + 1.2f, pos.z, 0.4f, 3f);
			if (num3.HasValue)
			{
				float valueOrDefault = num3.GetValueOrDefault();
				pos.y = Mathf.Lerp(car.transform.position.y, valueOrDefault, Mathf.Min(1f, dt * 12f));
			}
			car.SetColliders(on: true);
			if (!car.mx)
			{
				BodyDyn(c, dt);
			}
		}
		else
		{
			c.pitch = Mathf.Lerp(c.pitch, 0f, dt);
			c.roll = Mathf.Lerp(c.roll, 0f, dt);
		}
		c.pos = pos;
		float num4 = (c.reverse ? (0f - c.v) : c.v);
		if ((bool)car.mx)
		{
			MotoPose(c, car, ref pos, num4, dt);
		}
		else
		{
			car.transform.SetPositionAndRotation(pos, FBDrive.CarRot(c.pitch, c.yaw, c.roll));
		}
		car.yaw = c.yaw;
		car.pitch = c.pitch;
		car.v = num4;
		car.spin += num4 * dt;
		car.steerN = Mathf.Clamp(c.steer / 0.55f, -1f, 1f);
		FBDrive.PoseWheels(car, c.steer);
		car.lBrake = c.brake;
		car.lRev = c.reverse && c.v > 0.05f;
		car.lBlink = (c.hazard ? 2 : c.blink);
		car.aiLights = true;
		ActorVis(c, car, magnitude);
		string key = "veh_npc_" + c.id + "_";
		if (audio && !car.mx)
		{
			FBCar fBCar = car;
			string profile = fBCar.engineProf ?? (fBCar.engineProf = FBCarSound.ProfileFor(car.id, car));
			if (c.gb == null)
			{
				c.gb = new FBCarSound.Gearbox(FBDrive.TopV(car));
			}
			FBCarSound.EngineAudio(key, profile, c.gb.Step(Mathf.Max(dt, 0.001f), num4, (c.acc > 0.2f) ? 0.75f : 0.25f).rpm, (c.gb.shiftT > 0f) ? 0.15f : ((c.acc > 0.2f) ? 0.7f : ((c.acc < -0.5f) ? 0.1f : 0.3f)), pos, 0.3f, 5f);
			c.audio = true;
			if (c.acc < -5.5f && c.v > 7f && Time.time > c.squealT)
			{
				c.squealT = Time.time + 4f;
				FBAudio.PlayAt("veh_brake_squeal", pos, Mathf.Clamp(c.v / 25f, 0.3f, 0.7f), 7f, 0.06f);
			}
		}
		else if (c.audio)
		{
			StopAudio(c, siren: false);
		}
	}

	private static void StopAudio(Car c, bool siren = true)
	{
		if (c.audio)
		{
			FBCarSound.StopEngine("veh_npc_" + c.id + "_");
			c.audio = false;
		}
		if ((bool)c.car && (bool)c.car.mx)
		{
			c.car.mx.RemoteStop();
		}
		if (siren && c.patrol && (bool)c.car)
		{
			FBSiren.Stop(c.car);
		}
	}

	private static void MotoPose(Car c, FBCar car, ref Vector3 pos, float sv, float dt)
	{
		FBMxBike mx = car.mx;
		float num = 0f;
		if (c.mode == "drive" && c.route.Count > 0)
		{
			Ahead(c, 5f, out var tan);
			FBRoads.At(Seg(c.route[Mathf.Min(c.ri, c.route.Count - 1)]), c.s, out var tan2);
			num = Vector3.SignedAngle(tan2.Flat(), tan.Flat(), Vector3.up) * ((float)Math.PI / 180f) / 5f;
		}
		float value = Mathf.Atan(sv * sv * num / 11.76f) * 57.29578f;
		value = Mathf.Clamp(value, 0f - mx.MaxLeanDeg, mx.MaxLeanDeg);
		c.roll = Mathf.Lerp(c.roll, (0f - value) * ((float)Math.PI / 180f), Mathf.Min(1f, dt * 4f));
		bool flag = Mathf.Abs(num) < 0.006f && c.mode == "drive" && c.gapObs > 50f && sv > 7f && sv < 18f;
		if (c.whSt == 0 && c.aggr > 0.7f && flag && UnityEngine.Random.value < dt * 0.12f)
		{
			c.whSt = 1;
			c.whPeak = UnityEngine.Random.Range(22f, 40f) * ((float)Math.PI / 180f);
			c.whHold = UnityEngine.Random.Range(1.5f, 4f);
			c.whT = 0f;
		}
		if (c.whSt > 0)
		{
			c.whT += dt;
			if (!flag && c.whSt < 3)
			{
				c.whSt = 3;
				c.whT = 0f;
			}
			if (c.whSt == 1)
			{
				c.whA = Mathf.SmoothStep(0f, c.whPeak, c.whT / 0.7f);
				if (c.whT >= 0.7f)
				{
					c.whSt = 2;
					c.whT = 0f;
				}
			}
			else if (c.whSt == 2)
			{
				c.whA = c.whPeak + Mathf.Sin(c.whT * 2.3f) * 0.035f;
				if (c.whT >= c.whHold)
				{
					c.whSt = 3;
					c.whT = 0f;
				}
			}
			else
			{
				c.whA = Mathf.MoveTowards(c.whA, 0f, dt * c.whPeak / 0.6f);
				if (c.whA <= 0f)
				{
					c.whSt = 0;
					c.whA = 0f;
				}
			}
		}
		float wheelbase = mx.Wheelbase;
		Quaternion quaternion = FBDrive.CarRot(0f, c.yaw, c.roll);
		Quaternion quaternion2 = FBDrive.CarRot(0f - c.whA, c.yaw, c.roll);
		Vector3 vector = new Vector3(0f, 0f, (0f - wheelbase) / 2f);
		Vector3 vector2 = quaternion * vector - quaternion2 * vector;
		car.transform.SetPositionAndRotation(pos + vector2, quaternion2);
		float steer = Mathf.Clamp(Mathf.Atan(wheelbase * num) * 57.29578f / Mathf.Max(5f, mx.SteerDeg(Mathf.Abs(sv) / 1.2f)), -1f, 1f);
		if ((pos - CamPos).sqrMagnitude < 6400f)
		{
			mx.RemoteState(sv, steer, (c.acc > 0.3f || c.whSt > 0) ? 1f : ((c.acc < -0.5f) ? (-1f) : 0.2f), c.whA, dt);
		}
		else
		{
			mx.RemoteStop();
		}
	}

	private static void ActorVis(Car c, FBCar car, float camD)
	{
		bool flag = camD > (((bool)car.mx || car.kind == "moto") ? FBCarCull.HideDist : 110f);
		if ((bool)c.actor && c.actor.gameObject.activeSelf == flag)
		{
			c.actor.gameObject.SetActive(!flag);
		}
		if ((bool)c.garupa && !c.garupaDown && c.garupa.gameObject.activeSelf == flag)
		{
			c.garupa.gameObject.SetActive(!flag);
		}
	}

	private static bool MotoPhys(Car c, FBCar car, float camD, float dt)
	{
		if (c.mode == "moto_crash")
		{
			return true;
		}
		FBMxBike mx = car.mx;
		if (c.phys)
		{
			if (mx.crashed)
			{
				MotoCrash(c);
				return true;
			}
			if (camD > 85f || (c.mode != "drive" && c.mode != "wait_vanish"))
			{
				PhysOff(c);
				return false;
			}
		}
		else
		{
			if (c.mode != "drive" || camD > 55f || MotoPhysOn >= MotoPhysMax || c.whSt != 0 || Mathf.Abs(c.roll) > (float)Math.PI / 45f)
			{
				return false;
			}
			car.transform.SetPositionAndRotation(c.pos + Vector3.up * 0.05f, FBDrive.CarRot(0f, c.yaw, 0f));
			mx.RemoteStop();
			mx.SetActive(on: true);
			mx.Launch(c.fwd * c.v);
			c.phys = true;
			MotoPhysOn++;
			MotoPhysUps++;
			c.prevA = 0f;
			c.whA = 0f;
			c.roll = 0f;
			if (!c.physIgn && (bool)c.actor)
			{
				c.physIgn = true;
				Collider[] componentsInChildren = c.actor.GetComponentsInChildren<Collider>();
				foreach (Collider collider in componentsInChildren)
				{
					foreach (Collider col in car.cols)
					{
						if ((bool)collider && (bool)col)
						{
							Physics.IgnoreCollision(collider, col);
						}
					}
				}
			}
		}
		MotoPhysFrames += dt;
		Transform transform = car.transform;
		Vector3 position = transform.position;
		float num = mx.speedKmh / 3.6f * 1.2f;
		Vector3 vector = c.pos - position;
		vector.y = 0f;
		float num2 = Vector3.Dot(vector, c.fwd);
		float magnitude = (vector - c.fwd * num2).magnitude;
		MotoPhysLagMax = Mathf.Max(MotoPhysLagMax, Mathf.Abs(num2));
		MotoPhysLatMax = Mathf.Max(MotoPhysLatMax, magnitude);
		float num3 = Mathf.Clamp(num * 0.9f, 6f, 16f);
		Vector3 tan;
		Vector3 vector2 = Ahead(c, Mathf.Max(2.5f, num3 - num2), out tan);
		vector2 += new Vector3(tan.z, 0f, 0f - tan.x) * c.lat;
		float num4 = Mathf.Clamp(Vector3.SignedAngle(transform.forward.Flat(), (vector2 - position).Flat(), Vector3.up) / 22f, -1f, 1f);
		float thr = Mathf.Clamp((Mathf.Max(0f, c.v + Mathf.Clamp(num2 * 0.8f, -4f, 6f)) - num) * 0.35f, -1f, 1f);
		if (c.v < 0.2f && num < 1.2f)
		{
			thr = ((num > 0.3f) ? (-1) : 0);
		}
		if (num2 > 3f)
		{
			c.v = Mathf.Min(c.v, num + 0.6f);
		}
		float f = 0f;
		if (c.route.Count > 0)
		{
			Ahead(c, 5f, out var tan2);
			FBRoads.At(Seg(c.route[Mathf.Min(c.ri, c.route.Count - 1)]), c.s, out var tan3);
			f = Vector3.SignedAngle(tan3.Flat(), tan2.Flat(), Vector3.up) * ((float)Math.PI / 180f) / 5f;
		}
		bool flag = Mathf.Abs(f) < 0.006f && c.mode == "drive" && c.gapObs > 50f && magnitude < 1.5f;
		if (c.whSt == 0 && c.aggr > 0.7f && flag && mx.speedKmh > 35f && mx.speedKmh < 70f && UnityEngine.Random.value < dt * 0.12f)
		{
			c.whSt = 1;
			c.whPeak = UnityEngine.Random.Range(22f, 38f) * ((float)Math.PI / 180f);
			c.whHold = UnityEngine.Random.Range(1.5f, 4f);
			c.whT = 0f;
			MotoPhysWh++;
		}
		bool wheelie = false;
		float wheelieAngle = mx.wheelieAngle;
		float b = (wheelieAngle - c.prevA) / Mathf.Max(dt, 0.001f);
		c.prevA = wheelieAngle;
		float num5 = wheelieAngle + Mathf.Max(0f, b) * 0.3f;
		if (mx.onWheelie)
		{
			MotoPhysWhMax = Mathf.Max(MotoPhysWhMax, wheelieAngle);
		}
		if (c.whSt > 0)
		{
			c.whT += dt;
			if ((!flag || c.whT > 0.7f + c.whHold || mx.speedKmh > 80f) && c.whSt < 3)
			{
				c.whSt = 3;
			}
			if (c.whSt < 3)
			{
				float num6 = c.whPeak * 57.29578f;
				wheelie = true;
				thr = ((num5 > num6 + 10f) ? (-1f) : ((num5 > num6) ? 0f : ((num5 > num6 - 12f) ? 0.35f : 1f)));
				num4 *= 0.35f;
			}
			else if (!mx.onWheelie)
			{
				c.whSt = 0;
			}
		}
		mx.Input(num4, thr, wheelie, revving: false, stand: false, tA: false, tB: false, dt);
		c.roll = 0f;
		c.whA = 0f;
		return true;
	}

	private static void PhysOff(Car c)
	{
		c.phys = false;
		if ((bool)c.car && (bool)c.car.mx)
		{
			c.car.mx.SetActive(on: false);
			c.car.mx.RemoteStop();
		}
	}

	private static void MotoCrash(Car c)
	{
		MotoCrashes++;
		c.phys = false;
		FBCharacter actor = c.actor;
		FBCar car = c.car;
		ReleaseRes(c);
		c.mode = "moto_crash";
		c.modeT = 0f;
		Debug.Log($"[FreeBala][trânsito] moto {car.id} caiu ({car.mx.wheelieAngle:0}° de grau a {car.mx.speedKmh:0} km/h)");
		if (!actor)
		{
			return;
		}
		FBDrive.Release(actor);
		Vector3 vector = car.transform.position - car.transform.right * 1.1f;
		float? num = FBWorld.GroundY(vector.x, vector.y + 2f, vector.z, 0.3f);
		if (num.HasValue)
		{
			float valueOrDefault = num.GetValueOrDefault();
			vector.y = valueOrDefault;
		}
		if (!actor.gameObject.activeSelf)
		{
			actor.gameObject.SetActive(value: true);
		}
		string why = (((bool)car.mx && car.mx.wheelieAngle > 45f) ? "loop" : "batida");
		if ((bool)c.garupa)
		{
			FBCharacter garupa = c.garupa;
			FBGarupa component = garupa.GetComponent<FBGarupa>();
			if ((bool)component)
			{
				component.Leave();
			}
			c.garupaDown = true;
			string text = FBFall.DirFor(car, why);
			string dir = ((text == "Left") ? "Right" : ((text == "Right") ? "Left" : text));
			FBFall.Begin(garupa, FBFall.FallPoint(car, dir), car.transform.eulerAngles.y, car.transform.forward * car.v * 0.8f, dir);
		}
		FBFall.FromBike(actor, car, why);
	}

	private static void MotoCrashTick(Car c, float dt)
	{
		c.modeT += dt;
		c.v = 0f;
		if (c.modeT < 3.5f || !c.actor || FBFall.Busy(c.actor))
		{
			return;
		}
		FBCharacter actor = c.actor;
		c.actor = null;
		actor.StopDance(force: true);
		FBRoleNpc component = actor.GetComponent<FBRoleNpc>();
		if ((bool)component)
		{
			component.enabled = true;
			component.st = "walk";
			component.goalP = null;
		}
		FBRole.npcs.Add(actor);
		actor.SetWeaponVisible(v: false);
		if ((bool)c.garupa && c.garupaDown && !FBFall.Busy(c.garupa))
		{
			FBCharacter garupa = c.garupa;
			c.garupa = null;
			FBRoleNpc component2 = garupa.GetComponent<FBRoleNpc>();
			if ((bool)component2)
			{
				component2.enabled = true;
				component2.st = "walk";
				component2.goalP = null;
			}
			FBRole.npcs.Add(garupa);
		}
		c.mode = "gone";
	}

	private static void BodyDyn(Car c, float dt)
	{
		FBCar car = c.car;
		Bounds lbox = car.lbox;
		float num = Mathf.Max(1.1f, lbox.size.z * car.scale * 0.36f);
		float num2 = Mathf.Max(0.6f, lbox.size.x * car.scale * 0.38f);
		Vector3 vector = FBDrive.Fwd(c.yaw);
		Vector3 vector2 = FBDrive.Left(c.yaw);
		Vector3 p = c.pos;
		car.SetColliders(on: false);
		float? num3 = G(p + vector * num);
		float? num4 = G(p - vector * num);
		float? num5 = G(p + vector2 * num2);
		float? num6 = G(p - vector2 * num2);
		car.SetColliders(on: true);
		float acc = c.acc;
		float num7 = c.steer * c.v / Mathf.Max(1.5f, car.wheelbase);
		float num8 = ((num3.HasValue && num4.HasValue) ? Mathf.Atan2(num4.Value - num3.Value, 2f * num) : 0f);
		float num9 = ((num5.HasValue && num6.HasValue) ? Mathf.Atan2(num5.Value - num6.Value, 2f * num2) : 0f);
		// fisica 31/10: pitch positivo = nariz desce (Euler X): freando (acc<0) mergulha ate 0,045 rad, acelerando agacha a traseira ate 0,03 rad
		float num10 = num8 + Mathf.Clamp((0f - acc) * 0.008f, -0.03f, 0.045f);
		// fisica 31/10: CarRot nega o roll (c.roll>0 baixa o lado direito); curva a esquerda (steer>0) rola para fora, como o FBCarPhys.Body
		float num11 = num9 + Mathf.Clamp(c.v * num7 * 0.01f, -0.05f, 0.05f);
		float num12 = Mathf.Min(1f, dt * 6f);
		c.pitch += (num10 - c.pitch) * num12;
		c.roll += (num11 - c.roll) * num12;
		float? G(Vector3 q)
		{
			return FBWorld.GroundY(q.x, p.y + 1.2f, q.z, 0.3f, 3f);
		}
	}

	private static void Recycle(Car c)
	{
		if (!(c.mode != "drive"))
		{
			float magnitude = (c.pos - CamPos).magnitude;
			if (c.tag != null && c.life > c.maxLife && magnitude > 120f && !InView(c.pos))
			{
				Drop(c);
			}
			else if (magnitude > 290f && !InView(c.pos))
			{
				Respawn(c);
			}
		}
	}

	private static bool InView(Vector3 p)
	{
		Camera main = Camera.main;
		if ((bool)main)
		{
			return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(main), new Bounds(p + Vector3.up, new Vector3(5f, 3f, 5f)));
		}
		return false;
	}

	private static bool CanVanish(Car c)
	{
		if ((c.pos - CamPos).magnitude > 45f && !InView(c.pos))
		{
			return (c.pos - PlayerPos).magnitude > 35f;
		}
		return false;
	}

	private static void Respawn(Car c)
	{
		if (c.fugitive != null)
		{
			EndChase(c, "viatura reciclada");
		}
		else if (c.chase != null)
		{
			EndChase(c.chase, "fugitivo reciclado");
		}
		if (!CanVanish(c))
		{
			if (c.mode != "wait_vanish")
			{
				c.mode = "wait_vanish";
				VanishWaits++;
			}
			c.v = 0f;
			c.brake = true;
			c.hazard = true;
			ReleaseRes(c);
			return;
		}
		Vanished++;
		if (!LanePoint(out var seg, out var s, anywhere: false))
		{
			Drop(c);
			return;
		}
		if (c.phys)
		{
			PhysOff(c);
		}
		ReleaseRes(c);
		if (c.spot != null && c.spot.res == c)
		{
			c.spot.res = null;
		}
		c.spot = null;
		c.mode = "drive";
		c.reverse = false;
		c.legs.Clear();
		c.blink = 0;
		c.route.Clear();
		c.route.Add(seg);
		c.ri = 0;
		c.s = s;
		c.v = 6f;
		c.lat = (c.latT = 0f);
		c.stuck = 0f;
		c.overSide = 0;
		c.waitT = 0f;
		c.shockT = 0f;
		Extend(c);
		Vector3 tan;
		Vector3 position = (c.pos = FBRoads.At(Seg(seg), s, out tan));
		c.fwd = tan.Flat().normalized;
		c.yaw = Mathf.Atan2(0f - c.fwd.x, c.fwd.z);
		c.car.transform.position = position;
	}
}
}
