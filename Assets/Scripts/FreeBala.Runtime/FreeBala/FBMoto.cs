using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBMoto
{
	public class Wheelie
	{
		public float a;

		public float w;

		public float thr;

		public float fall;

		public float dist;

		public float best;

		public float last;

		public float peak;

		public float t;

		public float trick;

		public bool key;
	}

	public class Engine
	{
		public int gear = 1;

		public float shift;

		public float rpm = 0.2f;
	}

	public struct Ev
	{
		public string kind;

		public float power;

		public float dist;

		public float time;

		public float peak;
	}

	public const float BALANCE = 0.6981317f;

	public const float FAIL = 1.0821041f;

	public const float LEAN_MAX = (float)Math.PI / 4f;

	public const float LEAN_SPEED = 8f;

	public const float STEER_SPEED = 20f;

	public const float G = 3.4f;

	public const float UP = 3.3f;

	public const float DOWN = 5f;

	public const float DRAG = 4.4f;

	public const float SOFT = 2.2f;

	public const float POP = 0.9f;

	public const float MIN_SPEED = 1.5f;

	public const float FULL_SPEED = 5.5f;

	public const float THR_RATE = 4f;

	public const float SCORE_MIN = 0.29670596f;

	public const float LOW_MIN = 0.08726646f;

	public const float SIDE_LEAN = 0.43633232f;

	public const float STEER_IN_WHEELIE = 0.55f;

	public const float FALL_TIME = 1.6f;

	public static readonly float[] STEER_DEG = new float[2] { 45f, 18f };

	public static readonly float[] TRICK = new float[2] { 25f, 30f };

	private static readonly float[][] CURVE = new float[3][]
	{
		new float[2] { 0f, 300f },
		new float[2] { 0.17f, 449f },
		new float[2] { 1f, 350f }
	};

	private static readonly float[] RATIO = new float[5] { 1.55f, 1.2f, 1f, 0.86f, 0.74f };

	public static Wheelie NewWheelie()
	{
		return new Wheelie();
	}

	public static Engine NewEngine()
	{
		return new Engine();
	}

	public static float SteerAngle(float vReal)
	{
		float num = Mathf.Clamp01(Mathf.Abs(vReal) / 20f);
		return (STEER_DEG[0] + (STEER_DEG[1] - STEER_DEG[0]) * num) * ((float)Math.PI / 180f);
	}

	public static float LeanTarget(float steerN, float vReal, bool inWheelie = false)
	{
		return Mathf.Clamp(Mathf.Clamp(steerN, -1f, 1f) * ((float)Math.PI / 4f) * Mathf.Clamp01(Mathf.Abs(vReal) / 8f), 0f - (inWheelie ? 0.43633232f : ((float)Math.PI / 4f)), inWheelie ? 0.43633232f : ((float)Math.PI / 4f));
	}

	public static List<Ev> StepWheelie(Wheelie S, bool key, float thr, float brake, float vReal, float dt)
	{
		List<Ev> list = new List<Ev>();
		float num = Mathf.Abs(vReal);
		if (S.fall > 0f)
		{
			S.fall -= dt;
			S.a += (0f - S.a) * Mathf.Min(1f, dt * 3f);
			S.w = 0f;
			S.thr = 0f;
			S.key = false;
			S.trick = 0f;
			if (S.fall <= 0f)
			{
				S.fall = 0f;
				S.a = 0f;
			}
			return list;
		}
		float num2 = Mathf.Clamp01((num - 1.5f) / 4f);
		S.thr += (Mathf.Clamp01(thr) - S.thr) * Mathf.Min(1f, dt * 4f);
		bool flag = key && !S.key;
		S.key = key;
		if (S.a < 0.02f && flag && thr > 0.5f && num2 > 0.3f)
		{
			S.w += 0.9f;
			list.Add(new Ev
			{
				kind = "start"
			});
		}
		float num3 = (key ? (3.3f * S.thr * num2) : 0f);
		float num4 = 5f * Mathf.Clamp01(brake);
		float num5 = ((S.a > 0.001f || S.w > 0f) ? (-3.4f * Mathf.Sin(0.6981317f - S.a)) : 0f);
		S.w += (num3 - num4 + num5 - 4.4f * S.w - (key ? 0f : (2.2f * S.w))) * dt;
		float a = S.a;
		S.a += S.w * dt;
		if (S.a <= 0f)
		{
			if (S.w < -0.8f && a > 0.12f)
			{
				list.Add(new Ev
				{
					kind = "land",
					power = 0f - S.w
				});
			}
			S.a = 0f;
			S.w = 0f;
		}
		if (S.a > S.peak)
		{
			S.peak = S.a;
		}
		if (S.a > 1.0821041f)
		{
			list.Add(new Ev
			{
				kind = "loop",
				dist = S.dist
			});
			S.fall = 1.6f;
			S.w = 0f;
			S.dist = 0f;
			S.peak = 0f;
			S.t = 0f;
			return list;
		}
		if (S.a > 0.29670596f)
		{
			S.dist += num * dt;
			S.t += dt;
		}
		else if (S.a < 0.08726646f && (S.dist > 0f || S.t > 0f))
		{
			if (S.dist > 4f)
			{
				S.last = S.dist;
				S.best = Mathf.Max(S.best, S.dist);
				list.Add(new Ev
				{
					kind = "end",
					dist = S.dist,
					time = S.t,
					peak = S.peak
				});
			}
			S.dist = 0f;
			S.t = 0f;
			S.peak = 0f;
		}
		float num6 = S.a * 57.29578f;
		S.trick = ((num6 >= TRICK[0]) ? Mathf.Clamp01((num6 - TRICK[0]) / 15f) : 0f);
		return list;
	}

	public static float TorqueAt(float t)
	{
		t = Mathf.Clamp01(t);
		for (int i = 1; i < CURVE.Length; i++)
		{
			if (t <= CURVE[i][0])
			{
				float[] array = CURVE[i - 1];
				float[] array2 = CURVE[i];
				float num = (t - array[0]) / (array2[0] - array[0]);
				return (array[1] + (array2[1] - array[1]) * num) / 449f;
			}
		}
		float[][] cURVE = CURVE;
		return cURVE[cURVE.Length - 1][1] / 449f;
	}

	public static float EngineStep(Engine E, float v, float thr, float dt, float vmax, float acc)
	{
		float num = Mathf.Clamp01(v / vmax);
		float num2 = 0.2f;
		E.rpm = Mathf.Clamp01((num - (float)(E.gear - 1) * num2) / num2) * 0.8f + 0.2f + thr * 0.05f;
		if (E.shift > 0f)
		{
			E.shift -= dt;
			E.rpm = Mathf.Max(0.2f, E.rpm - 0.25f);
		}
		else if (E.gear < 5 && E.rpm > 0.93f)
		{
			E.gear++;
			E.shift = 0.16f;
		}
		else if (E.gear > 1 && E.rpm < 0.32f && (num - (float)(E.gear - 1) * num2) / num2 < 0.12f)
		{
			E.gear--;
		}
		if (!(E.shift > 0f))
		{
			return acc * TorqueAt(num) * RATIO[E.gear - 1] * thr * (1f - num * 0.35f);
		}
		return 0f;
	}

	public static float YawRate(float vWorld, float steer, float lean, float wheelbase)
	{
		float num = Mathf.Abs(vWorld) / 1.2f;
		float num2 = vWorld / wheelbase * Mathf.Tan(steer);
		float num3 = 11.76f;
		float num4 = (0f - Mathf.Sign(vWorld)) * num3 * Mathf.Tan(Mathf.Clamp(lean, -0.78f, 0.78f)) / Mathf.Max(1f, Mathf.Abs(vWorld));
		float num5 = Mathf.Clamp01((num - 2.5f) / 5f);
		return num2 * (1f - num5) + num4 * num5;
	}
}
}
