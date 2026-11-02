using System.Linq;
using UnityEngine;

namespace FreeBala
{
public static class FBAcai
{
	private static readonly string[] MODELS = new string[3] { "SaveiroG4", "SaveiroQuadrada", "SaveiroCross" };

	private const uint WHITE = 15987695u;

	private static FBTransito.Car tc;

	private static FBCar car;

	private static FBCharacter driver;

	private static AudioSource src;

	private static float fadeOut = -1f;

	private static float life;

	private static float visit;

	private static float leaveT = -1f;

	private static float carHp0;

	private static float drvHp0;

	private static float noiseSeen;

	private static bool passed;

	private const float VOL = 0.85f;

	public static bool Mute;

	public static bool Fleeing { get; private set; }

	public static bool Active => car;

	public static bool Leaving => leaveT >= 0f;

	public static FBCar Car => car;

	public static AudioSource Source => src;

	public static FBTransito.Car Traffic => tc;

	public static FBParedao.Wall Wall { get; private set; }

	public static float Cooldown()
	{
		return Random.Range(240f, 460f);
	}

	public static bool Start()
	{
		if (!FBTransito.Running || !FBRegistry.I)
		{
			return false;
		}
		string text = MODELS.FirstOrDefault((string m) => (bool)FBRegistry.I.Vehicle(m) && GameData.Vehicles.ContainsKey(m));
		if (text == null)
		{
			return false;
		}
		int seg;
		float s;
		if (FBStreetEvents.ForceNear > 0f)
		{
			if (!NearLane(FBStreetEvents.ForceNear, out seg, out s))
			{
				return false;
			}
		}
		else if (!FBTransito.FarLanePoint(out seg, out s))
		{
			return false;
		}
		Vector3 tan;
		Vector3 vector = FBRoads.At(FBRoads.segs[seg], s, out tan);
		car = FBVehicles.Make(text, vector, Mathf.Atan2(tan.x, tan.z) * 57.29578f, civil: true, () => 0.5f, true, 15987695u, false);
		if (!car)
		{
			return false;
		}
		car.name = "Carro_Acai";
		string text2 = FBStreetEvents.FreeCivilian(allowRepeat: true);
		driver = ((text2 != null) ? FBStreetEvents.Spawn(text2, vector, 0f, "acai") : null);
		if (!driver)
		{
			Kill();
			return false;
		}
		tc = FBTransito.SpawnSpecial(car, driver, "acai", Random.Range(4.2f, 5.4f), seg, s);
		if (tc == null)
		{
			Kill();
			return false;
		}
		tc.lure = true;
		visit = Random.Range(150f, 240f);
		passed = false;
		leaveT = -1f;
		tc.aggr = 0.1f;
		tc.T = 1.9f;
		tc.onDrop = delegate
		{
			End(now: false);
		};
		Fleeing = false;
		carHp0 = FBCarDamage.Hp(car);
		drvHp0 = driver.hp;
		noiseSeen = FBBotBrain.lastNoiseT;
		Speaker();
		life = 0f;
		fadeOut = -1f;
		Debug.Log($"[FreeBala][ruas-eventos] carro do açaí ({text}) entrou a {(vector - FBStreetEvents.PlayerPos).Flat().magnitude:0} m do jogador (visita de até {visit:0} s)");
		return true;
	}

	private static bool NearLane(float d, out int seg, out float s)
	{
		seg = -1;
		s = 0f;
		Vector3 playerPos = FBStreetEvents.PlayerPos;
		float num = 1E+09f;
		foreach (FBSeg seg2 in FBRoads.segs)
		{
			if (seg2.kind != 0 || seg2.len < 20f || seg2.ring)
			{
				continue;
			}
			for (float num2 = 6f; num2 < seg2.len - 6f; num2 += 4f)
			{
				Vector3 tan;
				Vector3 q = FBRoads.At(seg2, num2, out tan);
				tan = (q - playerPos).Flat();
				float num3 = Mathf.Abs(tan.magnitude - d);
				if (num3 < num && FBVehicles.list.All((FBCar c) => !c || (c.transform.position - q).sqrMagnitude > 64f))
				{
					num = num3;
					seg = seg2.id;
					s = num2;
				}
			}
		}
		return seg >= 0;
	}

	private static void Speaker()
	{
		AudioClip audioClip = FBAudio.Clip("acai_carro");
		if (!audioClip)
		{
			Debug.LogWarning("[FreeBala][ruas-eventos] sem o áudio acai_carro");
			return;
		}
		Wall = FBParedao.PlayLocal(car, audioClip, 0f, Random.value * audioClip.length * 0.8f);
		src = Wall?.src;
	}

	public static void Tick(float dt)
	{
		if (!car)
		{
			if (tc != null || (bool)driver)
			{
				End(now: true);
			}
			return;
		}
		life += dt;
		if (Wall != null)
		{
			float f = ((fadeOut >= 0f) ? Mathf.Clamp01(1f - fadeOut / 1.6f) : Mathf.Clamp01(life / 2.5f));
			Wall.vol = (Mute ? 0f : (0.85f * Mathf.Sqrt(f)));
			if ((bool)Wall.src)
			{
				src = Wall.src;
			}
			if (fadeOut >= 0f && (fadeOut += dt) > 1.6f)
			{
				Kill();
				return;
			}
		}
		if (tc != null && fadeOut < 0f && (bool)driver && driver.alive)
		{
			Vector3? vector = null;
			Vector3 position = car.transform.position;
			if (FBCarDamage.Hp(car) < carHp0 - 0.01f)
			{
				vector = FBBotBrain.lastNoiseP ?? (position - car.transform.forward * 10f);
			}
			else if (driver.hp < drvHp0 - 0.01f)
			{
				vector = FBBotBrain.lastNoiseP.GetValueOrDefault(position);
			}
			else if (FBBotBrain.lastNoiseT > noiseSeen)
			{
				Vector3? lastNoiseP = FBBotBrain.lastNoiseP;
				if (lastNoiseP.HasValue)
				{
					Vector3 valueOrDefault = lastNoiseP.GetValueOrDefault();
					if ((valueOrDefault - position).Flat().magnitude < 30f)
					{
						vector = valueOrDefault;
					}
				}
			}
			noiseSeen = FBBotBrain.lastNoiseT;
			carHp0 = FBCarDamage.Hp(car);
			drvHp0 = driver.hp;
			if (vector.HasValue)
			{
				Vector3 valueOrDefault2 = vector.GetValueOrDefault();
				if (!Fleeing)
				{
					Debug.Log($"[FreeBala][ruas-eventos] carro do açaí FUGINDO (tiro a {(valueOrDefault2 - position).Flat().magnitude:0} m)");
				}
				Fleeing = true;
				leaveT = Mathf.Max(leaveT, 0f);
				FBTransito.Flee(tc, valueOrDefault2);
			}
		}
		else if (tc != null && (bool)driver && !driver.alive && tc.vCap > 0f)
		{
			tc.vCap = 0f;
		}
		if (tc != null && fadeOut < 0f)
		{
			float magnitude = (car.transform.position - FBStreetEvents.PlayerPos).Flat().magnitude;
			if (leaveT < 0f)
			{
				if (magnitude < 45f)
				{
					passed = true;
				}
				if ((passed && magnitude > 75f) || life > visit)
				{
					leaveT = 0f;
					FBTransito.Leave(tc);
					Debug.Log(string.Format("[FreeBala][ruas-eventos] carro do açaí indo embora ({0}, {1:0} s)", passed ? "já passou pelo jogador" : "fim da visita", life));
				}
			}
			else if ((leaveT += dt) > 120f && (!FBTransito.Visible(car.transform.position) || leaveT > 180f))
			{
				End(now: false);
			}
		}
		if (tc != null && tc.mode == "gone" && fadeOut < 0f)
		{
			End(now: false);
		}
	}

	public static void End(bool now)
	{
		if (!car && tc == null && !driver)
		{
			return;
		}
		if (!now && Wall != null && fadeOut < 0f)
		{
			fadeOut = 0f;
			if (tc != null)
			{
				tc.onDrop = null;
				FBTransito.Forget(tc);
				tc = null;
			}
		}
		else
		{
			Kill();
		}
	}

	private static void Kill()
	{
		if (tc != null)
		{
			tc.onDrop = null;
			FBTransito.Forget(tc);
			tc = null;
		}
		if ((bool)driver)
		{
			FBStreetEvents.Despawn(driver);
		}
		driver = null;
		if ((bool)car)
		{
			FBParedao.StopLocal(car);
			FBVehicles.list.Remove(car);
			Object.Destroy(car.gameObject);
		}
		car = null;
		src = null;
		Wall = null;
		fadeOut = -1f;
	}
}
}
