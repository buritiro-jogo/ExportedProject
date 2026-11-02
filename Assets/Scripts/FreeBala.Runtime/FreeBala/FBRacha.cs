using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBRacha
{
	private class R
	{
		public FBCar car;

		public FBCharacter rider;

		public float lat;

		public float best;

		public float prevA;

		public bool show;

		public bool crashed;

		public bool done;

		public float wT;
	}

	private static readonly List<R> rs = new List<R>();

	private static FBSeg seg;

	private static float s0;

	private static float t;

	private static float life;

	private static string st;

	public static string Why;

	public static int Brakes;

	public static int Crashes;

	public static int Races;

	public static int Finishes;

	public static bool Active => rs.Count > 0;

	public static string State => st;

	public static float Progress
	{
		get
		{
			if (rs.Count != 0)
			{
				return rs.Max((R r) => Along(r) - s0);
			}
			return 0f;
		}
	}

	public static float MaxWheelie
	{
		get
		{
			if (rs.Count != 0)
			{
				return rs.Max((R r) => r.best);
			}
			return 0f;
		}
	}

	private static float Dist
	{
		get
		{
			if (seg == null)
			{
				return 200f;
			}
			return Mathf.Min(220f, seg.len - s0 - 25f);
		}
	}

	public static float Cooldown()
	{
		return Random.Range(200f, 380f);
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][racha] " + s);
	}

	private static float Along(R r)
	{
		if (!r.car)
		{
			return 0f;
		}
		float lat;
		float dist;
		return FBRoads.Project(seg, r.car.transform.position, out lat, out dist);
	}

	public static bool Start()
	{
		Why = null;
		if (Active)
		{
			Why = "já ativo";
			return false;
		}
		if (!FBRoads.Ready)
		{
			Why = "sem malha de ruas";
			return false;
		}
		List<string> list = (from i in FBMxBike.IDS.Where(FBMxBike.Has)
			where i != "at1100" && i != "gs1250" && i != "moby"
			select i).ToList();
		if (list.Count == 0)
		{
			Why = "sem motos MX";
			return false;
		}
		Vector3 playerPos = FBStreetEvents.PlayerPos;
		float forceNear = FBStreetEvents.ForceNear;
		List<FBSeg> list2 = FBRoads.segs.Where((FBSeg S) => S.kind == 0 && S.len > 150f && !S.ring && Turn(S) < 8f).ToList();
		foreach (FBSeg item in list2.OrderBy((FBSeg _) => Random.value))
		{
			float d = 12f;
			Vector3 tan;
			Vector3 p = FBRoads.At(item, d, out tan);
			float magnitude = (p - playerPos).Flat().magnitude;
			if ((forceNear <= 0f && (magnitude < 40f || magnitude > 180f)) || FBTransito.List.Any((FBTransito.Car c) => (bool)c.car && (c.pos - p).magnitude < 25f))
			{
				continue;
			}
			seg = item;
			s0 = d;
			Build(list, p, tan.Flat().normalized);
			if (rs.Count < 2)
			{
				End(now: true);
				continue;
			}
			st = "alinhando";
			t = 0f;
			life = 0f;
			Log(string.Format("racha montado a {0:0} m · faixa {1} ({2:0} m) · {3}", magnitude, item.id, item.len, string.Join(" × ", rs.Select((R r) => r.car.id))));
			return true;
		}
		Why = $"nenhuma reta boa ({list2.Count} retas longas no mapa)";
		return false;
		static float Turn(FBSeg S)
		{
			FBRoads.At(S, 5f, out var tan2);
			FBRoads.At(S, S.len - 5f, out var tan3);
			return Vector3.Angle(tan2.Flat(), tan3.Flat());
		}
	}

	private static void Build(List<string> ids, Vector3 p, Vector3 f)
	{
		Vector3 vector = Vector3.Cross(Vector3.up, f);
		float num = Mathf.Clamp(seg.w / 2f - 0.9f, 0.9f, 1.6f);
		List<string> list = FBStreetEvents.FreeIds(FBNpcAnims.ByCls("motoboy")).Concat(FBStreetEvents.FreeIds(FBNpcAnims.Roster(FBSession.skin))).Distinct()
			.ToList();
		for (int i = 0; i < 2; i++)
		{
			float num2 = ((i == 0) ? (0f - num) : num);
			Vector3 vector2 = p + vector * num2;
			float? num3 = FBWorld.GroundY(vector2.x, vector2.y + 2f, vector2.z, 0.3f);
			if (num3.HasValue)
			{
				float valueOrDefault = num3.GetValueOrDefault();
				vector2.y = valueOrDefault;
			}
			FBCar fBCar = FBVehicles.Spawn("mx_" + ids[Random.Range(0, ids.Count)], vector2 + Vector3.up * 0.25f, Quaternion.LookRotation(f).eulerAngles.y);
			if ((bool)fBCar && (bool)fBCar.mx)
			{
				FBCharacter fBCharacter = FBStreetEvents.Spawn((list.Count > i) ? list[i] : FBStreetEvents.FreeCivilian(allowRepeat: true), vector2 - vector * 1.2f, 0f, "racha" + i);
				if (!fBCharacter)
				{
					Kill(fBCar);
					continue;
				}
				Mount(fBCharacter, fBCar);
				rs.Add(new R
				{
					car = fBCar,
					rider = fBCharacter,
					lat = num2,
					show = (i == 0)
				});
			}
		}
	}

	private static void Mount(FBCharacter a, FBCar car)
	{
		a.StopDance(force: true);
		CharacterController component = a.GetComponent<CharacterController>();
		if ((bool)component)
		{
			component.enabled = false;
		}
		a.vehiclePose = true;
		a.sys = FBDrive.SitClip(car);
		a.aiming = (a.shooting = (a.sprint = (a.crouch = false)));
		a.combat = false;
		a.SetWeaponVisible(v: false);
		a.velocity = Vector3.zero;
		car.driver = a;
		FBVehicles.SetDriving(a, car);
		a.gameObject.GetOrAdd<FBDriverPose>().Set(car);
		Collider[] componentsInChildren = a.GetComponentsInChildren<Collider>();
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
		car.mx.SetActive(on: false);
	}

	private static void Kill(FBCar car)
	{
		if ((bool)car)
		{
			if ((bool)car.mx)
			{
				car.mx.SetActive(on: false);
			}
			FBVehicles.list.Remove(car);
			Object.Destroy(car.gameObject);
		}
	}

	public static void Tick(float dt)
	{
		if (!Active)
		{
			return;
		}
		life += dt;
		t += dt;
		foreach (R r in rs)
		{
			if (!r.car || !r.rider)
			{
				continue;
			}
			FBMxBike mx = r.car.mx;
			if (!mx)
			{
				continue;
			}
			if (mx.crashed && !r.crashed)
			{
				r.crashed = true;
				Crashes++;
				Log($"{r.car.id} caiu ({mx.wheelieAngle:0}° de grau a {mx.speedKmh:0} km/h)");
				FBCharacter rider = r.rider;
				FBDrive.Release(rider);
				FBFall.FromBike(rider, r.car, (mx.wheelieAngle > 45f) ? "loop" : "batida");
			}
			if (r.crashed)
			{
				continue;
			}
			float thr = 0f;
			float num = Steer(r);
			bool flag = false;
			bool revving = false;
			switch (st)
			{
			case "alinhando":
				mx.RemoteState(0f, 0f, (t > 0.6f) ? (0.35f + 0.35f * Mathf.PerlinNoise(t * 3f, r.lat)) : 0f, 0f, dt);
				if (t > 0.6f && Random.value < dt * 2.5f)
				{
					mx.StartBlip();
				}
				continue;
			case "largada":
			{
				thr = 1f;
				float wheelieAngle = mx.wheelieAngle;
				float b = (wheelieAngle - r.prevA) / Mathf.Max(dt, 0.001f);
				r.prevA = wheelieAngle;
				float num2 = wheelieAngle + Mathf.Max(0f, b) * 0.3f;
				if (r.show && ((mx.speedKmh > 35f && mx.speedKmh < 80f) || mx.onWheelie) && Along(r) - s0 < Dist - 30f)
				{
					flag = mx.speedKmh < 80f;
					thr = ((!flag) ? 0f : ((num2 > 40f) ? (-1f) : ((num2 > 30f) ? 0f : ((num2 > 18f) ? 0.35f : 1f))));
					num *= 0.35f;
				}
				r.best = Mathf.Max(r.best, mx.onWheelie ? wheelieAngle : 0f);
				if (Along(r) - s0 > Dist)
				{
					r.done = true;
				}
				if (r.done)
				{
					thr = ((mx.speedKmh > 3f) ? (-1) : 0);
					flag = false;
				}
				break;
			}
			case "volta":
				thr = ((mx.speedKmh < 35f) ? 0.55f : 0f);
				break;
			}
			if (st == "largada" && !r.done)
			{
				Transform transform = r.car.transform;
				float maxDistance = Mathf.Max(8f, mx.speedKmh / 3.6f * 1.2f * 1.2f);
				RaycastHit[] array = Physics.SphereCastAll(transform.position + Vector3.up * 0.9f, 0.55f, transform.forward.Flat().normalized, maxDistance, FBWorld.SolidMask | 0x800 | 0x400, QueryTriggerInteraction.Ignore);
				for (int i = 0; i < array.Length; i++)
				{
					RaycastHit raycastHit = array[i];
					if (!(raycastHit.collider.GetComponentInParent<FBCar>() == r.car) && !(raycastHit.collider.GetComponentInParent<FBCharacter>() == r.rider) && !(raycastHit.normal.y > 0.6f))
					{
						thr = -1f;
						flag = false;
						Brakes++;
						break;
					}
				}
			}
			mx.Input(num, thr, flag, revving, stand: false, tA: false, tB: false, dt);
		}
		if (st == "alinhando" && t > 3.2f)
		{
			st = "largada";
			t = 0f;
			foreach (R r2 in rs)
			{
				if ((bool)r2.car)
				{
					r2.car.mx.RemoteStop();
					r2.car.mx.SetActive(on: true);
					r2.car.Horn();
				}
			}
			Log("largada");
		}
		else if (st == "largada" && (rs.All((R r) => r.done || r.crashed) || t > 30f))
		{
			st = "volta";
			t = 0f;
			Races++;
			Finishes += rs.Count((R r) => r.done && !r.crashed);
			Log($"chegada · grau máx {MaxWheelie:0}° · percorrido {Progress:0} m · quedas {rs.Count((R r) => r.crashed)}");
		}
		else if (st == "volta" && t > 4f && (!rs.Any((R r) => (bool)r.car && FBStreetEvents.Visible(r.car.transform.position)) || t > 60f))
		{
			End(now: false);
		}
		if (life > 120f)
		{
			End(now: true);
		}
	}

	private static float Steer(R r)
	{
		Transform transform = r.car.transform;
		float lat;
		float dist;
		float num = FBRoads.Project(seg, transform.position, out lat, out dist);
		float num2 = Mathf.Clamp(r.car.mx.speedKmh * 0.18f, 8f, 18f);
		Vector3 tan;
		Vector3 vector = FBRoads.At(seg, num + num2, out tan);
		Vector3 vector2 = Vector3.Cross(Vector3.up, tan.Flat().normalized);
		vector += vector2 * r.lat;
		return Mathf.Clamp(Vector3.SignedAngle(transform.forward.Flat(), (vector - transform.position).Flat(), Vector3.up) / 22f, -1f, 1f);
	}

	public static void End(bool now)
	{
		foreach (R r in rs)
		{
			if ((bool)r.rider)
			{
				if ((bool)FBVehicles.CarOf(r.rider))
				{
					FBDrive.Release(r.rider);
				}
				FBStreetEvents.Despawn(r.rider);
			}
			Kill(r.car);
		}
		rs.Clear();
		st = null;
	}
}
}
