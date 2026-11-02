using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBBloodFx
{
	public struct Profile
	{
		public float amount;

		public float speed;

		public float mist;

		public float wound;

		public float reach;

		public float splat;

		public Profile(float a, float s, float m, float w, float r, float sp)
		{
			amount = a;
			speed = s;
			mist = m;
			wound = w;
			reach = r;
			splat = sp;
		}
	}

	public static bool BodyWounds = false;

	private static readonly Color cDrop = new Color(0.36f, 0.015f, 0.015f, 0.95f);

	private static readonly Color cFine = new Color(0.42f, 0.03f, 0.025f, 0.85f);

	private static readonly Color cMist = new Color(0.4f, 0.04f, 0.035f, 0.38f);

	private static ParticleSystem psDrop;

	private static ParticleSystem psFine;

	private static ParticleSystem psMist;

	private static bool ready;

	private static readonly Dictionary<FBCharacter, (int frame, int n)> burst = new Dictionary<FBCharacter, (int, int)>();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void Hook()
	{
		FBDamageEvents.OnDamage -= On;
		FBDamageEvents.OnDamage += On;
	}

	public static Profile Region(string part)
	{
		switch (part)
		{
		case "head":
			return new Profile(1.5f, 1.15f, 1.7f, 1.05f, 1.25f, 1.15f);
		case "arm":
		case "leg":
		case "limb":
			return new Profile(0.6f, 0.9f, 0.6f, 0.8f, 0.7f, 0.7f);
		default:
			return new Profile(1f, 1f, 1f, 1f, 1f, 1f);
		}
	}

	public static Profile Weapon(string cls)
	{
		return cls switch
		{
			"pistola" => new Profile(0.8f, 0.85f, 0.85f, 0.85f, 0.8f, 0.85f), 
			"submetralhadora" => new Profile(0.75f, 0.9f, 0.8f, 0.8f, 0.85f, 0.8f), 
			"escopeta" => new Profile(0.4f, 0.75f, 0.55f, 0.7f, 0.6f, 0.6f), 
			"sniper" => new Profile(1.9f, 1.6f, 1.8f, 1.25f, 1.9f, 1.4f), 
			"explosivo" => new Profile(1.2f, 0.7f, 1.4f, 1f, 0.8f, 1.1f), 
			_ => new Profile(1f, 1f, 1f, 1f, 1f, 1f), 
		};
	}

	public static string ClassOf(string weaponId)
	{
		if (weaponId == null || !GameData.Weapons.TryGetValue(weaponId, out var value))
		{
			return "fuzil";
		}
		return value.cls;
	}

	public static void Warm()
	{
		if (!ready)
		{
			ready = true;
			FBFxQuality.Ensure();
			bool flag = FBFxQuality.PoolDecals > 100;
			Material m = FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("droplet"), Color.white, "SangueGota");
			psDrop = FBFxLib.PS("SangueGotas", m, flag ? 500 : 180, new FBFxLib.Opt
			{
				gravity = 1f,
				mode = ParticleSystemRenderMode.Stretch,
				stretch = 0.022f,
				color = FBFxLib.Fade(0f, 0.75f, new Color(0.7f, 0.6f, 0.6f))
			});
			psFine = FBFxLib.PS("SangueBorrifo", m, flag ? 400 : 140, new FBFxLib.Opt
			{
				gravity = 0.55f,
				drag = 0.18f,
				mode = ParticleSystemRenderMode.Stretch,
				stretch = 0.03f,
				color = FBFxLib.Fade(0f, 0.5f)
			});
			psMist = FBFxLib.PS("SangueNevoa", FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("mist"), Color.white, "SangueNevoa"), flag ? 80 : 30, new FBFxLib.Opt
			{
				gravity = 0.04f,
				drag = 0.35f,
				size = FBFxLib.Curve(0f, 0.55f, 0.35f, 1.15f, 1f, 1.6f),
				color = FBFxLib.Fade(0.08f, 0.25f),
				spin = true
			});
		}
	}

	private static void On(FBDamage d)
	{
		if (!d.victim || !FBFx.BloodOn)
		{
			return;
		}
		switch (d.type)
		{
		case FBDamageType.Zone:
		case FBDamageType.Fall:
			return;
		case FBDamageType.Bleed:
			BleedDrip(d.victim);
			return;
		}
		FBFx.Warm();
		FBCharacter victim = d.victim;
		Vector3 point = d.hit.point;
		Vector3 vector = ((d.hit.dir.sqrMagnitude > 0.01f) ? d.hit.dir.normalized : (-victim.transform.forward));
		string text = (d.head ? "head" : (d.hit.part ?? "body"));
		Profile profile = Region(text);
		Profile profile2 = ((d.type == FBDamageType.Explosion) ? Weapon("explosivo") : Weapon(ClassOf(d.hit.weaponId)));
		float num = Mathf.Clamp((d.applied > 0f) ? (d.applied / 28f) : (d.amount / 28f), 0.45f, 1.6f);
		(int, int) value;
		int num2 = ((burst.TryGetValue(victim, out value) && value.Item1 == Time.frameCount) ? (value.Item2 + 1) : 0);
		burst[victim] = (Time.frameCount, num2);
		float num3 = ((num2 == 0) ? 1f : (1f / (1f + (float)num2 * 0.8f)));
		if (d.type == FBDamageType.Explosion || (!d.hit.valid && d.type != 0))
		{
			Vector3 vector2 = (victim.Chest ? victim.Chest.position : (victim.Pos + Vector3.up));
			for (int i = 0; i < 3; i++)
			{
				Spray(vector2 + Random.insideUnitSphere * 0.2f, FBFxLib.Cone((d.type == FBDamageType.Explosion) ? ((vector2 - (d.source ? d.source.Pos : (vector2 - vector))).normalized + Vector3.up * 0.4f) : vector, 0.9f), 1.1f * num3, 1f, 1.2f);
			}
			SceneSplat(vector2, Vector3.down * 0.5f + vector * 0.5f, 1f, 1.2f);
			return;
		}
		float amount = profile.amount * profile2.amount * num * num3;
		Spray(point, vector, amount, profile.speed * profile2.speed, profile.mist * profile2.mist * ((num2 == 0) ? 1f : 0.5f));
		float num4 = Random.Range(0.034f, 0.05f) * profile.wound * profile2.wound * (float)((text == "head") ? 1 : 1);
		if (BodyWounds)
		{
			FBWoundFx.Attach(victim, point, vector, num4, streak: false, text);
		}
		if (BodyWounds && num2 == 0 && (text == "head" || d.applied > 30f || ClassOf(d.hit.weaponId) == "sniper") && Random.value < 0.6f)
		{
			FBWoundFx.Attach(victim, point, vector, num4 * 2.4f, streak: true, text);
		}
		if (num2 <= 1)
		{
			SceneSplat(point, vector, profile.reach * profile2.reach, profile.splat * profile2.splat * Mathf.Lerp(0.8f, 1.2f, (num - 0.45f) / 1.15f));
		}
		if (num2 == 0)
		{
			FBAudio.PlayAt("thud", point, 0.6f);
		}
	}

	public static void Spray(Vector3 p, Vector3 dir, float amount = 1f, float speed = 1f, float mist = 1f)
	{
		if (!FBFx.BloodOn)
		{
			return;
		}
		Warm();
		float num = FBFxQuality.Lod(p);
		if (!(num <= 0f))
		{
			dir = ((dir.sqrMagnitude > 1E-06f) ? dir.normalized : Vector3.forward);
			int num2 = FBFxQuality.Count(9f * amount, num);
			int num3 = FBFxQuality.Count(14f * amount, num);
			int num4 = FBFxQuality.Count(3f * Mathf.Min(1.5f, amount), num);
			int num5 = FBFxQuality.Count(Mathf.Clamp(1.6f * mist, 1f, 4f), Mathf.Max(num, 0.5f));
			Vector3 vector = p + dir * 0.03f;
			for (int i = 0; i < num2; i++)
			{
				FBFxLib.Emit(psDrop, vector + Random.insideUnitSphere * 0.02f, FBFxLib.Cone(dir, 0.32f) * Random.Range(2.2f, 5.5f) * speed + Vector3.up * Random.Range(0f, 0.7f), Random.Range(0.006f, 0.02f), Random.Range(0.28f, 0.62f), FBFxLib.Jit(cDrop, 0.25f));
			}
			for (int j = 0; j < num3; j++)
			{
				FBFxLib.Emit(psFine, vector, FBFxLib.Cone(dir, 0.5f) * Random.Range(3.5f, 8.5f) * speed, Random.Range(0.003f, 0.008f), Random.Range(0.12f, 0.3f), FBFxLib.Jit(cFine, 0.2f));
			}
			for (int k = 0; k < num4; k++)
			{
				FBFxLib.Emit(psDrop, p - dir * 0.02f, FBFxLib.Cone(-dir, 0.6f) * Random.Range(0.8f, 2.2f) + Vector3.up * 0.4f, Random.Range(0.005f, 0.012f), Random.Range(0.2f, 0.4f), FBFxLib.Jit(cDrop, 0.2f));
			}
			for (int l = 0; l < num5; l++)
			{
				FBFxLib.Emit(psMist, p + dir * Random.Range(0.04f, 0.18f) * speed, dir * Random.Range(0.4f, 1.3f) * speed + Random.insideUnitSphere * 0.25f, Random.Range(0.12f, 0.26f) * Mathf.Sqrt(mist), Random.Range(0.18f, 0.38f), FBFxLib.Jit(cMist, 0.15f, Random.Range(0.7f, 1.2f)), Random.Range(0f, 360f));
			}
		}
	}

	public static void SceneSplat(Vector3 p, Vector3 dir, float reach = 1f, float size = 1f)
	{
		if (!FBFx.BloodOn)
		{
			return;
		}
		dir = ((dir.sqrMagnitude > 1E-06f) ? dir.normalized : Vector3.down);
		float num = 2.6f * reach;
		if (FBWorld.RayHit(p + dir * 0.35f, dir, num, out var hit, 0.03f))
		{
			Vector3 normal = hit.normal;
			float num2 = Mathf.Abs(Vector3.Dot(normal, dir));
			float num3 = 1f - Mathf.Clamp01(hit.distance / num) * 0.45f;
			float num4 = Random.Range(0.22f, 0.42f) * size * num3;
			Vector3 vector = Vector3.ProjectOnPlane(dir, normal);
			bool flag = Mathf.Abs(normal.y) < 0.5f;
			if (num2 < 0.55f && vector.sqrMagnitude > 0.0001f)
			{
				FBDecalFx.Place(FBDecalFx.Atlas.Blood, 6 + Random.Range(0, 4), hit.point, normal, num4 * 0.8f, Vector3.Cross(vector.normalized, normal), Mathf.Clamp(1f / Mathf.Max(num2, 0.35f), 1.3f, 2.4f), Mover(hit.collider));
			}
			else if (flag && num4 > 0.26f && Random.value < 0.45f)
			{
				FBDecalFx.Place(FBDecalFx.Atlas.Blood, 10 + Random.Range(0, 2), hit.point, normal, num4 * 1.25f, Vector3.up, 1f, Mover(hit.collider));
			}
			else
			{
				FBDecalFx.Place(FBDecalFx.Atlas.Blood, Random.Range(0, 6), hit.point, normal, num4, null, 1f, Mover(hit.collider));
			}
		}
		if (Random.value < 0.65f)
		{
			Vector3 vector2 = p + new Vector3(dir.x, 0f, dir.z) * Random.Range(0.35f, 1.1f) * reach;
			float? num5 = FBWorld.GroundY(vector2.x, p.y, vector2.z, 0.2f, 2.6f);
			if (num5.HasValue)
			{
				float valueOrDefault = num5.GetValueOrDefault();
				FBDecalFx.Place(FBDecalFx.Atlas.Blood, (Random.value < 0.5f) ? (14 + Random.Range(0, 2)) : Random.Range(0, 6), new Vector3(vector2.x, valueOrDefault, vector2.z), Vector3.up, Random.Range(0.16f, 0.32f) * size);
			}
		}
	}

	public static Transform Mover(Collider c)
	{
		if (!c || (!c.attachedRigidbody && c.gameObject.layer != 11))
		{
			return null;
		}
		return c.transform;
	}

	private static void BleedDrip(FBCharacter v)
	{
		Vector3 p = (v.Chest ? v.Chest.position : (v.Pos + Vector3.up)) + Random.insideUnitSphere * 0.1f;
		if (FBFxQuality.Lod(p) > 0f)
		{
			float? num = FBWorld.GroundY(p.x, p.y, p.z, 0.3f, 2.5f);
			if (num.HasValue)
			{
				float valueOrDefault = num.GetValueOrDefault();
				FBDecalFx.Place(FBDecalFx.Atlas.Blood, 14 + Random.Range(0, 2), new Vector3(p.x, valueOrDefault, p.z), Vector3.up, Random.Range(0.14f, 0.26f), null, 1f, null, FBFxQuality.DecalLife * 0.6f);
			}
		}
	}

	public static void Pool(Vector3 p)
	{
		if (FBFx.BloodOn)
		{
			FBDecalFx.Pool(p + Vector3.up * 0.01f, Random.Range(1.1f, 1.7f), Random.Range(4f, 6.5f));
		}
	}
}
}
