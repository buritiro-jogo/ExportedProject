using UnityEngine;

namespace FreeBala
{
public static class FBFxQuality
{
	public static float ParticleMul = 1f;

	public static int MaxDecals = 120;

	public static float DecalLife = 90f;

	public static int MaxWounds = 64;

	public static int WoundsPerChar = 6;

	public static bool Shells = true;

	public static bool UseProjector = true;

	public static bool Drips = true;

	public static int FrameBudget = 420;

	public static float FarCull = 90f;

	private static Camera cam;

	private static float camT;

	private static int budgetFrame = -1;

	private static int budgetLeft;

	public static int Level { get; private set; } = -1;


	public static int PoolDecals
	{
		get
		{
			if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
			{
				return 160;
			}
			return 64;
		}
	}

	public static int PoolWounds
	{
		get
		{
			if (!Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
			{
				return 80;
			}
			return 32;
		}
	}

	public static Camera Cam
	{
		get
		{
			if (!cam || !cam.isActiveAndEnabled || Time.unscaledTime > camT)
			{
				camT = Time.unscaledTime + 1f;
				FBPlayer i = FBPlayer.I;
				cam = (((bool)i && (bool)i.cam && i.cam.isActiveAndEnabled) ? i.cam : Camera.main);
			}
			return cam;
		}
	}

	public static int AutoLevel()
	{
		if (Application.platform == RuntimePlatform.WebGLPlayer)
		{
			if (SystemInfo.systemMemorySize <= 0 || SystemInfo.systemMemorySize >= 3000)
			{
				return 1;
			}
			return 0;
		}
		if (Application.isMobilePlatform)
		{
			if (SystemInfo.systemMemorySize <= 0 || SystemInfo.systemMemorySize >= 3500)
			{
				return 1;
			}
			return 0;
		}
		return 2;
	}

	public static void Ensure()
	{
		if (Level < 0)
		{
			Apply(AutoLevel());
		}
	}

	public static void Apply(int level)
	{
		Level = Mathf.Clamp(level, 0, 2);
		switch (Level)
		{
		case 0:
			ParticleMul = 0.45f;
			MaxDecals = 36;
			DecalLife = 35f;
			MaxWounds = 16;
			WoundsPerChar = 3;
			Shells = false;
			UseProjector = false;
			Drips = false;
			FrameBudget = 120;
			FarCull = 45f;
			break;
		case 1:
			ParticleMul = 0.7f;
			MaxDecals = 64;
			DecalLife = 60f;
			MaxWounds = 32;
			WoundsPerChar = 4;
			Shells = true;
			UseProjector = false;
			Drips = true;
			FrameBudget = 220;
			FarCull = 65f;
			break;
		default:
			ParticleMul = 1f;
			MaxDecals = 160;
			DecalLife = 120f;
			MaxWounds = 80;
			WoundsPerChar = 6;
			Shells = true;
			UseProjector = true;
			Drips = true;
			FrameBudget = 420;
			FarCull = 90f;
			break;
		}
		MaxDecals = Mathf.Min(MaxDecals, PoolDecals);
		MaxWounds = Mathf.Min(MaxWounds, PoolWounds);
		FBWater.ApplyQuality(Level);
	}

	public static float Lod(Vector3 p)
	{
		Camera camera = Cam;
		if (!camera)
		{
			return 1f;
		}
		Vector3 lhs = p - camera.transform.position;
		float magnitude = lhs.magnitude;
		if (magnitude > FarCull)
		{
			return 0f;
		}
		if (magnitude > 6f && Vector3.Dot(lhs, camera.transform.forward) < -0.2f * magnitude)
		{
			return 0f;
		}
		float num = ((Level >= 2) ? 22 : 14);
		if (!(magnitude <= num))
		{
			return Mathf.Lerp(1f, 0.3f, (magnitude - num) / Mathf.Max(1f, FarCull - num));
		}
		return 1f;
	}

	public static int Take(int want)
	{
		if (budgetFrame != Time.frameCount)
		{
			budgetFrame = Time.frameCount;
			budgetLeft = FrameBudget;
		}
		int num = Mathf.Min(want, budgetLeft);
		budgetLeft -= num;
		return Mathf.Max(0, num);
	}

	public static int Count(float baseCount, float lod)
	{
		float num = Mathf.Max(0f, baseCount * ParticleMul * lod);
		int num2 = (int)num;
		if (Random.value < num - (float)num2)
		{
			num2++;
		}
		return Take(num2);
	}
}
}
