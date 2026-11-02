using System;
using UnityEngine;

namespace FreeBala
{
public static class FBCarPhys
{
	public static bool On = Array.IndexOf(Environment.GetCommandLineArgs(), "-fbfisicaantiga") < 0;

	private const float G = 11.76f;

	private const float STEER_MAX = 0.9599311f;

	private const float A_PEAK = 0.12217305f;

	private const float TB = 21.690546f;

	private const float TC = 1.3f;

	private const float SELF_ALIGN = 0.8f;

	public static readonly Vector3 Pivot = new Vector3(0f, 0.25f, 0f);

	public static string Last;

	public static float SteerLimit(float kmh)
	{
		return Mathf.Lerp(1f, 0.01f, Mathf.Clamp01(kmh / 300f));
	}

	public static float SteerSpeed(float kmh)
	{
		return Mathf.Lerp(1.35f, 0f, Mathf.Clamp01(kmh / 400f));
	}

	private static float Tire(float a)
	{
		return Mathf.Sin(1.3f * Mathf.Atan(21.690546f * a));
	}

	private static float Sq(float x)
	{
		return x * x;
	}

	public static float SteerIn(FBCar car, float st, float kmh, float dt)
	{
		float stIn = car.stIn;
		float target = st;
		float num;
		if (stIn == 0f || !(st * stIn < 0f))
		{
			num = ((!(Mathf.Abs(st) < Mathf.Abs(stIn))) ? (Mathf.Max(0.9f, SteerSpeed(kmh) * 1.6f) * (FBInput.Touch ? 1.3f : 1f)) : 4f);
		}
		else
		{
			target = 0f;
			num = 4f;
		}
		return Mathf.MoveTowards(stIn, target, num * dt);
	}

	public static void Lateral(FBCar car, FBDrive.K Kd, bool hb, float ax, float dt)
	{
		float num = Mathf.Max(1.2f, car.wheelbase);
		float num2 = num / 2f;
		float num3 = Mathf.Abs(car.v) * 3.6f / 1.2f;
		float num4 = 0.9599311f * SteerLimit(num3);
		if (car.air)
		{
			car.yaw += car.yr * dt;
			car.yr *= Mathf.Exp((0f - dt) * 0.6f);
			car.steer = car.stIn * num4;
			car.lim = num4;
			car.slipF = (car.slipR = 0f);
			return;
		}
		car.hbG = Mathf.Clamp(car.hbG + (hb ? (-6f) : 2.2f) * dt, 0.32f, 1f);
		float num5 = 1.08f * Kd.grip;
		float num6 = num5 * 1.12f * car.hbG;
		float num7 = Mathf.Max(Mathf.Abs(car.v), 0.1f);
		float num8 = Mathf.Min(num4, Mathf.Atan(num * num5 * 11.76f / (num7 * num7)) + 0.13439035f);
		string text = car.tracao ?? "";
		bool flag = text == "FWD" || text == "4x4";
		bool flag2 = text != "FWD";
		float num9 = ((ax > 0f) ? (Mathf.Clamp01(ax / (num5 * 11.76f)) * 0.8f) : 0f);
		float num10 = ((ax < 0f) ? Mathf.Min(0.75f, (0f - ax) / (num5 * 11.76f) * 0.5f) : 0f);
		float num11 = Mathf.Sqrt(1f - Sq(Mathf.Max(flag ? num9 : 0f, num10)));
		float num12 = Mathf.Sqrt(1f - Sq(Mathf.Max(flag2 ? num9 : 0f, num10 * 0.6f))) * (car.burn ? 0.5f : 1f);
		float num13 = Mathf.Clamp(0.5f - 0.3f * Mathf.Clamp(ax, -11.76f, 9.408f) / (num * 11.76f), 0.3f, 0.8f);
		int num14 = Mathf.Clamp(Mathf.CeilToInt(dt / 0.004f), 1, 12);
		float num15 = dt / (float)num14;
		float i = ((car.v > 0f) ? Mathf.Clamp01((car.v - 2.5f) / 4.5f) : 0f);
		float a2 = 0f;
		float a3 = 0f;
		float num16 = 0f;
		float num17 = 0f;
		float num18 = (hb ? 1.4f : 0.34906584f);
		for (int j = 0; j < num14; j++)
		{
			float v = car.v;
			float num19 = Mathf.Max(v, 3f);
			num17 = Mathf.Atan2(car.vl - num2 * car.yr, Mathf.Max(Mathf.Abs(v), 1f));
			num16 = Mathf.Clamp(car.stIn * num8 + i * 0.8f * num17, -0.9599311f, 0.9599311f);
			float num20 = num16 - Mathf.Atan2(car.vl + num2 * car.yr, num19);
			float num21 = 0f - Mathf.Atan2(car.vl - num2 * car.yr, num19);
			float num22 = num5 * num13 * 11.76f * num11 * Tire(num20);
			float num23 = num6 * (1f - num13) * 11.76f * num12 * Tire(num21);
			float num24 = Mathf.Cos(num16);
			float num25 = num22 * num24 + num23 - num19 * car.yr;
			float num26 = (num22 * num24 - num23) / num2;
			float num27 = (0f - num22) * Mathf.Sin(num16) + car.vl * car.yr;
			float num28 = Mathf.Abs(num17);
			if (num28 > num18)
			{
				num26 += Mathf.Sign(num17) * (num28 - num18) * 14f;
			}
			float num29 = v / num * Mathf.Tan(num16);
			float a4 = num29 * num2;
			car.vl = Mathf.Lerp(a4, car.vl + num25 * num15, i);
			car.yr = Mathf.Clamp(Mathf.Lerp(num29, car.yr + num26 * num15, i), -4f, 4f);
			car.v += num27 * num15 * i;
			car.yaw += car.yr * num15;
			a2 = Mathf.Max(a2, Mathf.Abs(num20));
			a3 = Mathf.Max(a3, Mathf.Abs(num21));
		}
		car.slipF = Sl(a2);
		car.slipR = Sl(a3);
		car.steer = num16;
		car.lim = num8;
		car.beta = num17 * i;
		Last = $"{num3:0} km/h · volante {car.stIn:+0.00;-0.00} · roda {num16 * 57.29578f:+0.0;-0.0}° (lim {num8 * 57.29578f:0.0}°) · β {num17 * 57.29578f:+0.0;-0.0}° · giro {car.yr * 57.29578f:0}°/s · lat {car.v * car.yr / 11.76f:0.00} g · deriva F/T {car.slipF:0.00}/{car.slipR:0.00}";
		float Sl(float a)
		{
			return i * Mathf.Clamp01((a - 0.09773844f) / 0.30543262f);
		}
	}

	public static void OnHit(FBCar car, float k = 0.4f)
	{
		car.vl *= k;
		car.yr *= k;
	}

	public static void Body(FBCar car, float ax, float ay, float dt)
	{
		if ((bool)car.shell)
		{
			float num = (car.heavy ? 1.5f : 1f);
			float t = Mathf.Clamp((0f - ax) * 0.0022f * num, -0.06f, 0.06f);
			float t2 = Mathf.Clamp((0f - ay) * 0.0048f * num, -0.08f, 0.08f);
			Spring(ref car.bPit, ref car.bPitV, t, 8.5f, 0.45f, dt);
			Spring(ref car.bRol, ref car.bRolV, t2, 7.5f, 0.4f, dt);
			Spring(ref car.bHv, ref car.bHvV, 0f, 10f, 0.35f, dt);
			car.bHv = Mathf.Clamp(car.bHv, -0.1f, 0.06f);
			car.bodyQ = Quaternion.Euler(car.bPit * 57.29578f, 0f, car.bRol * 57.29578f);
			car.bodyHv = car.bHv;
			car.bodyOn = true;
			Transform transform = car.transform;
			Transform parent = car.shell.parent;
			if ((bool)parent)
			{
				Vector3 vector = transform.InverseTransformPoint(parent.TransformPoint(car.shellP0));
				Quaternion quaternion = Quaternion.Inverse(transform.rotation) * parent.rotation * car.shellR0;
				car.shell.SetPositionAndRotation(transform.TransformPoint(Pivot + car.bodyQ * (vector - Pivot) + Vector3.up * car.bHv), transform.rotation * car.bodyQ * quaternion);
			}
		}
	}

	private static void Spring(ref float x, ref float v, float t, float w, float z, float dt)
	{
		int num = ((!(dt > 0.02f)) ? 1 : 3);
		float num2 = dt / (float)num;
		for (int i = 0; i < num; i++)
		{
			v += (w * w * (t - x) - 2f * z * w * v) * num2;
			x += v * num2;
		}
	}

	public static void Reset(FBCar car)
	{
		if ((bool)car)
		{
			car.vl = (car.yr = (car.slipF = (car.slipR = (car.beta = 0f))));
			car.hbG = 1f;
			car.bPit = (car.bPitV = (car.bRol = (car.bRolV = (car.bHv = (car.bHvV = 0f)))));
			car.bodyQ = Quaternion.identity;
			car.bodyHv = 0f;
			if (car.bodyOn && (bool)car.shell)
			{
				car.shell.localPosition = car.shellP0;
				car.shell.localRotation = car.shellR0;
			}
			car.bodyOn = false;
		}
	}
}
}
