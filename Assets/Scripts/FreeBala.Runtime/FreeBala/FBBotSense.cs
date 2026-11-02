using System;
using UnityEngine;

namespace FreeBala
{
public static class FBBotSense
{
	private static readonly Vector3[] pts = new Vector3[3];

	private static int D => Mathf.Clamp(FBSession.diff, 0, 2);

	public static float AimBaseDeg => (new float[3] { 2.3f, 1.45f, 1f })[D];

	public static float ReactK => (new float[3] { 1.35f, 1f, 0.78f })[D];

	public static float RangeK => (new float[3] { 0.85f, 1f, 1.12f })[D];

	public static float HeadAim => (new float[3] { 0.02f, 0.06f, 0.12f })[D];

	public static float SightMax
	{
		get
		{
			float nightK = FBWeather.NightK;
			return Mathf.Lerp(120f, 65f, Mathf.InverseLerp(0.4f, 0.95f, nightK));
		}
	}

	public static float EngageRange(WeaponDef w)
	{
		int num = ((w != null) ? (w.cls switch
		{
			"pistola" => 32, 
			"submetralhadora" => 42, 
			"escopeta" => 20, 
			"sniper" => 130, 
			"explosivo" => 45, 
			_ => 68, 
		}) : 25);
		return (float)num * RangeK;
	}

	public static Vector3 Eye(FBCharacter ch)
	{
		if ((bool)ch.Head)
		{
			Vector3 position = ch.Head.position;
			return Vector3.Lerp(new Vector3(ch.Pos.x, position.y + 0.04f * ch.ScaleK, ch.Pos.z), position, 0.35f);
		}
		return ch.Pos + Vector3.up * (ch.crouch ? 1.15f : 1.62f) * ch.ScaleK;
	}

	public static void TargetPoints(FBCharacter t, Vector3[] p)
	{
		float scaleK = t.ScaleK;
		if ((bool)t.Head)
		{
			p[0] = t.Head.position;
		}
		else
		{
			p[0] = t.Pos + Vector3.up * (t.prone ? 0.45f : (t.crouch ? 1.25f : 1.65f)) * scaleK;
		}
		p[1] = (t.Chest ? t.Chest.position : (t.Pos + Vector3.up * (t.prone ? 0.35f : (t.crouch ? 0.9f : 1.3f)) * scaleK));
		p[2] = (t.Hips ? t.Hips.position : (t.Pos + Vector3.up * (t.prone ? 0.25f : (t.crouch ? 0.6f : 0.95f)) * scaleK));
	}

	public static float Visibility(FBCharacter self, float yawRad, bool alert, FBCharacter t, out float dist, out int seenPts, out int mask)
	{
		dist = 9999f;
		seenPts = 0;
		mask = 0;
		if (!t || !t.alive)
		{
			return 0f;
		}
		Vector3 vector = Eye(self);
		TargetPoints(t, pts);
		Vector3 vector2 = pts[1];
		dist = (vector2 - vector).magnitude;
		if (dist > SightMax)
		{
			return 0f;
		}
		Vector3 lhs = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
		Vector3 normalized = (vector2 - vector).Flat().normalized;
		float num = Vector3.Dot(lhs, normalized);
		if (dist > 5f && num < (alert ? (-0.17f) : 0.5f))
		{
			return 0f;
		}
		for (int i = 0; i < 3; i++)
		{
			Vector3 vector3 = pts[i] - vector;
			float magnitude = vector3.magnitude;
			if (magnitude < 0.05f || !FBWorld.RayHit(vector, vector3 / magnitude, magnitude - 0.3f, 0.04f))
			{
				seenPts++;
				mask |= 1 << i;
			}
		}
		if (seenPts == 0)
		{
			return 0f;
		}
		float num2 = (float)seenPts / 3f;
		num2 *= (t.prone ? 0.4f : (t.crouch ? 0.7f : 1f));
		if (t.velocity.Flat().magnitude > 1f)
		{
			num2 *= 1.35f;
		}
		bool flag = t.lastShot < 1.2f;
		if (flag)
		{
			num2 *= 2.2f;
		}
		float nightK = FBWeather.NightK;
		if (nightK > 0.5f && !flag && dist > 12f)
		{
			num2 *= Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(0.5f, 0.95f, nightK));
		}
		num2 *= Mathf.Clamp01(1.25f - dist / SightMax);
		if (dist > 5f && num < 0.5f)
		{
			num2 *= 0.55f;
		}
		return Mathf.Clamp01(num2);
	}

	public static float NoticeTime(float dist)
	{
		return Mathf.Lerp(0.28f, 2.1f, Mathf.Clamp01(dist / 80f)) * ReactK;
	}

	public static float AimErrorDeg(FBCharacter self, FBCharacter t, float sinceAcquire, bool selfMoving)
	{
		float num = AimBaseDeg * (1f + 2f * Mathf.Exp((0f - sinceAcquire) / 0.5f));
		if (selfMoving)
		{
			num *= 1.35f;
		}
		if (self.crouch)
		{
			num *= 0.82f;
		}
		Vector3 vector = (t.Pos - self.Pos).Flat();
		float num2 = Mathf.Max(4f, vector.magnitude);
		return num + (t.velocity.Flat() - Vector3.Project(t.velocity.Flat(), vector.normalized)).magnitude / num2 * 57.29578f * 0.3f;
	}

	public static Vector3 Scatter(Vector3 dir, float sigmaDeg)
	{
		Vector3 normalized = Vector3.Cross(dir, (Mathf.Abs(dir.y) > 0.95f) ? Vector3.right : Vector3.up).normalized;
		Vector3 vector = Vector3.Cross(normalized, dir);
		float num = Gauss();
		float num2 = Gauss();
		float num3 = sigmaDeg * ((float)Math.PI / 180f);
		return (dir + normalized * Mathf.Tan(num * num3) + vector * Mathf.Tan(num2 * num3 * 0.8f)).normalized;
	}

	private static float Gauss()
	{
		float f = Mathf.Max(1E-06f, UnityEngine.Random.value);
		float value = UnityEngine.Random.value;
		return Mathf.Clamp(Mathf.Sqrt(-2f * Mathf.Log(f)) * Mathf.Cos((float)Math.PI * 2f * value), -2.6f, 2.6f);
	}
}
}
