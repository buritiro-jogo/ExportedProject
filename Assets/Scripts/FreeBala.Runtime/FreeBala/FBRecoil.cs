using System;
using UnityEngine;

namespace FreeBala
{
public class FBRecoil
{
	public struct State
	{
		public bool aiming;

		public bool crouch;

		public bool moving;

		public bool air;

		public float zoom;

		public float grip;

		public float recV;

		public float recH;

		public float spreadHip;

		public float spreadAds;
	}

	private const float KICK = 0.75f;

	private const float BLOOM = 0.85f;

	public float p;

	public float y;

	public float qp;

	public float qy;

	public float n;

	public float idle = 9f;

	public float delay = 0.12f;

	public float rec = 6f;

	public float bloom;

	public float bloomDecay = 2.2f;

	public float lastKick;

	public static bool TestNoSpread;

	public void Reset()
	{
		p = (y = (qp = (qy = 0f)));
		n = 0f;
		idle = 9f;
		bloom = 0f;
	}

	private static float Hash(int seed, int k)
	{
		float num = Mathf.Sin((float)seed * 12.9898f + (float)k * 78.233f) * 43758.547f;
		return (num - Mathf.Floor(num)) * 2f - 1f;
	}

	private static int Seed(string id)
	{
		int num = 7;
		foreach (char c in id)
		{
			num = (num * 31 + c) % 9973;
		}
		return num;
	}

	private static RecoilClass C(WeaponDef w)
	{
		if (!GameData.Recoil.TryGetValue(w.cls, out var value))
		{
			if (!GameData.Recoil.TryGetValue("fuzil", out var value2))
			{
				return new RecoilClass
				{
					v = 0.58f,
					h = 0.42f,
					ramp = 8f,
					spread = 1.25f,
					ads = 0.26f,
					bloom = 0.1f,
					bloomMax = 3f,
					rec = 6.5f
				};
			}
			return value2;
		}
		return value;
	}

	public float Spread(WeaponDef w, State st)
	{
		if (TestNoSpread)
		{
			return 0f;
		}
		RecoilClass recoilClass = C(w);
		float num = recoilClass.spread * (w.spread / 0.25f);
		if (st.aiming)
		{
			num *= recoilClass.ads * Json.F(FBWeaponModel.Scope(w), "multiplierSpread", 0.1f) / 0.1f;
		}
		num *= ((!st.aiming) ? ((st.spreadHip > 0f) ? st.spreadHip : 1f) : ((st.spreadAds > 0f) ? st.spreadAds : 1f));
		if (st.crouch)
		{
			num *= 0.7f;
		}
		num += ((!st.moving) ? 0f : (st.aiming ? 0.18f : 0.75f));
		if (st.air)
		{
			num += 2.5f;
		}
		return num + bloom * (st.aiming ? 0.6f : 1f) * (st.crouch ? 0.8f : 1f);
	}

	public float Shot(WeaponDef w, State st)
	{
		RecoilClass recoilClass = C(w);
		int num = (int)n++;
		int seed = Seed(w.id);
		idle = 0f;
		delay = Mathf.Clamp(w.Interval * 1.6f, 0.1f, 0.45f);
		rec = recoilClass.rec;
		float b = w.Damage(0f, head: false);
		float num2 = Mathf.Clamp(Mathf.Pow(Mathf.Max(1f, b) / 25f, 0.3f), 0.85f, 1.4f);
		float num3 = (w.auto ? Mathf.Clamp(Mathf.Sqrt(600f / Mathf.Max(1f, w.rpm)), 0.8f, 1.5f) : 1f);
		float num4 = 1f;
		if (st.aiming)
		{
			num4 *= 0.72f;
		}
		if (st.crouch)
		{
			num4 *= 0.8f;
		}
		if (st.moving)
		{
			num4 *= 1.15f;
		}
		if (st.air)
		{
			num4 *= 1.5f;
		}
		num4 /= Mathf.Sqrt(Mathf.Max(1f, st.zoom));
		num4 *= ((st.grip > 0f) ? st.grip : 1f);
		if (w.IsSniper && w.auto)
		{
			num4 *= 0.5f;
		}
		float num5 = w.recoilScale * 0.75f;
		float num6 = recoilClass.v * num5 * num2 * num3 * (0.8f + 0.45f * Mathf.Min(1f, (float)num / Mathf.Max(1f, recoilClass.ramp))) * num4 * (1f + (UnityEngine.Random.value - 0.5f) * 0.1f) * ((st.recV > 0f) ? st.recV : 1f);
		float num7 = 0.5f + 0.4f * Mathf.Abs(Hash(seed, 1));
		float num8 = Hash(seed, 2) * (float)Math.PI;
		float num9 = ((Hash(seed, 3) > 0f) ? 1 : (-1));
		float num10 = recoilClass.h * num5 * num4 * (num9 * 0.32f * Mathf.Min(1f, (float)num / 6f) + 0.65f * Mathf.Sin((float)num * num7 + num8) + 0.35f * Hash(seed, 10 + num) + (UnityEngine.Random.value - 0.5f) * 0.25f) * ((st.recH > 0f) ? st.recH : 1f);
		qp += num6;
		qy += num10;
		bloom = Mathf.Min(recoilClass.bloomMax, bloom + recoilClass.bloom * 0.85f * (st.aiming ? 0.7f : 1f));
		lastKick = num6;
		return num6;
	}

	public Vector2 Step(float dt)
	{
		float num = 1f - Mathf.Exp((0f - dt) * 45f);
		float num2 = qp * num;
		float num3 = qy * num;
		qp -= num2;
		qy -= num3;
		p += num2;
		y += num3;
		idle += dt;
		if (idle > delay)
		{
			float num4 = 1f - Mathf.Exp((0f - dt) * rec);
			float b = 1.1459156f * dt;
			float num5 = p * num4 + Mathf.Sign(p) * Mathf.Min(Mathf.Abs(p), b);
			float num6 = y * num4 + Mathf.Sign(y) * Mathf.Min(Mathf.Abs(y), b);
			p -= num5;
			y -= num6;
			num2 -= num5;
			num3 -= num6;
			n = Mathf.Max(0f, n - dt * 9f);
			bloom = Mathf.Max(0f, bloom - dt * bloomDecay);
		}
		return new Vector2(num2, num3);
	}

	public void Look(float dyaw, float dpitch)
	{
		if (dpitch < 0f && p > 0f)
		{
			p = Mathf.Max(0f, p + dpitch);
		}
		if (y != 0f && dyaw * y < 0f)
		{
			y = ((Mathf.Abs(dyaw) >= Mathf.Abs(y)) ? 0f : (y + dyaw));
		}
	}
}
}
