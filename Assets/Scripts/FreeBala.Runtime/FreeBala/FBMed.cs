using System;
using UnityEngine;

namespace FreeBala
{
public static class FBMed
{
	public const float Use = 3f;

	public const float Heal = 75f;

	public const float Hot = 2.5f;

	public const float PassiveCap = 40f;

	public const float MoveMul = 0.6f;

	public const int Max = 4;

	public const int Start = 2;

	public const int Price = 300;

	public static int kits;

	public static bool inf;

	public static bool spent;

	public static string state;

	public static float t;

	public static float hotLeft;

	public const float Wind = 0.5f;

	public static bool Busy => state == "use";

	public static bool Healing
	{
		get
		{
			if (Busy)
			{
				return t > 0.5f;
			}
			return false;
		}
	}

	public static float Progress
	{
		get
		{
			if (!(state == "use"))
			{
				return 0f;
			}
			return Mathf.Min(1f, t / 3f);
		}
	}

	public static void ResetPlayer(int n, bool infinite)
	{
		state = null;
		t = 0f;
		hotLeft = 0f;
		kits = n;
		inf = infinite;
	}

	public static bool Add(int n = 1)
	{
		if (kits >= 4)
		{
			return false;
		}
		kits = Mathf.Min(4, kits + n);
		return true;
	}

	public static string Why(FBCharacter p)
	{
		if (!p || !p.alive)
		{
			return "morto";
		}
		if (state != null)
		{
			return "em uso";
		}
		if (!inf && kits <= 0)
		{
			return "SEM KIT MÉDICO";
		}
		if (p.hp >= p.maxHp)
		{
			return "VIDA CHEIA";
		}
		return null;
	}

	public static void Begin()
	{
		state = "use";
		t = 0f;
		spent = false;
		hotLeft = 0f;
	}

	public static bool Cancel()
	{
		if (state != "use")
		{
			return false;
		}
		state = null;
		t = 0f;
		hotLeft = 0f;
		return true;
	}

	public static string Update(float dt, Action<float> heal)
	{
		string result = null;
		if (state == "use")
		{
			float a = t;
			t += dt;
			if (!spent && t >= 0.5f)
			{
				spent = true;
				if (!inf)
				{
					kits--;
				}
			}
			if (t > 0.5f)
			{
				heal(75f * (Mathf.Min(t, 3f) - Mathf.Max(a, 0.5f)) / 2.5f);
			}
			if (t >= 3f)
			{
				state = null;
				t = 0f;
				result = "done";
			}
		}
		return result;
	}
}
}
