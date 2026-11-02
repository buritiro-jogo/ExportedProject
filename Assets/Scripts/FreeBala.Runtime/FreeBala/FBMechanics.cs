using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBMechanics
{
	public static class KNOCK
	{
		public const float moveSpeed = 1.5f;

		public const float bleedDamage = 4f;

		public const float bleedTime = 3f;

		public const float invuln = 0.8f;

		public const float reviveHealth = 50f;

		public const float reviveTime = 5f;

		public const float detectDistance = 5f;

		public const float detectAngle = 40f;
	}

	private static readonly Dictionary<string, float> KNOCK_MODE = new Dictionary<string, float>
	{
		["squad"] = 10f,
		["pvl"] = 5f,
		["tatico"] = 5f
	};

	public const float FallSafe = 4.5f;

	public const float FallDeath = 15f;

	public const float FallMinAir = 0.4f;

	public const float ProtMin = 5f;

	public const float ProtMax = 8f;

	public const bool ProtBreakOnFire = true;

	public const float VoidY = -70f;

	public const float Coyote = 0.12f;

	public const float JumpBuffer = 0.12f;

	public const float GlintMaxDist = 900f;

	public const float GlintMinDist = 5f;

	public const float GlintBaseDist = 10f;

	public const float GlintScale = 0.52f;

	public const float GlintArea = 12f;

	public const float GlintMaxScale = 3f;

	public static bool KnockMode(string id)
	{
		if (id != null)
		{
			return KNOCK_MODE.ContainsKey(id);
		}
		return false;
	}

	public static float ReviveTime(string id)
	{
		if (id == null || !KNOCK_MODE.TryGetValue(id, out var value))
		{
			return 5f;
		}
		return value;
	}

	public static bool CanKnock(IKnockable a, string modeId, bool hasReviver)
	{
		if (!KnockMode(modeId) || a == null || a.Knocked || a.Civ)
		{
			return false;
		}
		return hasReviver;
	}

	public static void KnockDown(IKnockable a, float now)
	{
		a.Knocked = true;
		a.Hp = ((a.MaxHp > 0f) ? a.MaxHp : 100f);
		a.KnockInvT = now + 0.8f;
		a.BleedAcc = 0f;
		a.RevivedBy = null;
		a.ReviveP = 0f;
	}

	public static float BleedStep(IKnockable a, float dt)
	{
		if (!a.Knocked)
		{
			return 0f;
		}
		a.BleedAcc += dt;
		float num = 0f;
		while (a.BleedAcc >= 2.9999f)
		{
			a.BleedAcc -= 3f;
			num += 4f;
		}
		return num;
	}

	public static bool KnockInvulnerable(IKnockable a, float now)
	{
		if (a.Knocked)
		{
			return now < a.KnockInvT;
		}
		return false;
	}

	public static bool CanReviveTarget(float dx, float dz, float dist, float lookYaw)
	{
		if (dist > 5f)
		{
			return false;
		}
		if (dist < 0.9f)
		{
			return true;
		}
		float f = Mathf.Atan2(dx, dz) - lookYaw;
		f = Mathf.Atan2(Mathf.Sin(f), Mathf.Cos(f));
		return Mathf.Abs(f) * 57.29578f <= 40f;
	}

	public static bool ReviveStep(IKnockable a, float dt, string modeId)
	{
		a.ReviveP = Mathf.Min(1f, a.ReviveP + dt / ReviveTime(modeId));
		return a.ReviveP >= 1f;
	}

	public static void ReviveDone(IKnockable a)
	{
		a.Knocked = false;
		a.Hp = 50f;
		a.ReviveP = 0f;
		a.BleedAcc = 0f;
		a.RevivedBy = null;
	}

	public static float FallDamage(float dist, float airTime)
	{
		if (!(airTime > 0.4f) || !(dist > 4.5f))
		{
			return 0f;
		}
		return Mathf.Floor(dist / 15f * 100f);
	}

	public static float LandVolume(float dist)
	{
		return Mathf.Clamp01(dist / 15f);
	}

	public static float RollProtection(Func<float> rand = null)
	{
		return 5f + (rand ?? ((Func<float>)(() => UnityEngine.Random.value)))() * 3f;
	}

	public static bool CanCoyoteJump(float offGroundT, bool jumpedSinceGround)
	{
		if (!jumpedSinceGround)
		{
			return offGroundT <= 0.12f;
		}
		return false;
	}

	public static bool BufferedJump(float pressedT)
	{
		return pressedT <= 0.12f;
	}

	public static float GlintScaleFor(float dist, float fov, float defFov)
	{
		if (dist <= 5f || dist > 900f)
		{
			return 0f;
		}
		return Mathf.Min(fov / defFov * (dist / 10f) * 0.52f, 3f);
	}

	public static float RayPointDist(Vector3 o, Vector3 d, Vector3 p)
	{
		Vector3 vector = p - o;
		float num = Vector3.Dot(vector, d);
		if (num < 0f)
		{
			return 1f / 0f;
		}
		return (vector - d * num).magnitude;
	}
}
}
