using UnityEngine;

namespace FreeBala
{
public static class FBShotFx
{
	private static ParticleSystem psSmoke;

	private static ParticleSystem psShell;

	private static bool ready;

	public static void Warm()
	{
		if (!ready)
		{
			ready = true;
			FBFxQuality.Ensure();
			bool flag = FBFxQuality.PoolDecals > 100;
			psSmoke = FBFxLib.PS("DisparoFumaca", FBFxLib.Mat("Particles_Alpha", FBFxLib.Tex("smoke"), Color.white, "DisparoFumaca"), flag ? 120 : 50, new FBFxLib.Opt
			{
				gravity = -0.03f,
				drag = 0.5f,
				tilesX = 2,
				tilesY = 2,
				spin = true,
				size = FBFxLib.Curve(0f, 0.4f, 1f, 2.2f),
				color = FBFxLib.Fade(0.05f, 0.35f)
			});
			if (FBFxQuality.PoolDecals > 0)
			{
				psShell = FBFxLib.PS("Cartuchos", FBFxLib.Mat("Particles_Alpha", null, Color.white, "Cartucho"), flag ? 48 : 20, new FBFxLib.Opt
				{
					gravity = 1.1f,
					mode = ParticleSystemRenderMode.Mesh,
					mesh = FBFxLib.Shell(),
					size3D = true,
					collide = true,
					bounce = 0.35f,
					dampen = 0.45f,
					color = FBFxLib.Fade(0f, 0.85f)
				});
			}
		}
	}

	public static void Smoke(Vector3 p, Vector3 fwd, float size, string cls, bool silenced)
	{
		Warm();
		float num = FBFxQuality.Lod(p);
		if (!(num <= 0f))
		{
			float num2 = cls switch
			{
				"pistola" => 0.7f, 
				"sniper" => 1.8f, 
				"escopeta" => 2.2f, 
				_ => 1f, 
			};
			if (silenced)
			{
				num2 *= 0.5f;
			}
			int num3 = FBFxQuality.Count(1.4f * num2, num);
			for (int i = 0; i < num3; i++)
			{
				FBFxLib.Emit(psSmoke, p + fwd * Random.Range(0.02f, 0.1f), fwd * Random.Range(0.3f, 1.1f) * num2 + Vector3.up * Random.Range(0.1f, 0.35f), Random.Range(0.05f, 0.12f) * Mathf.Max(0.4f, size * 2f) * Mathf.Sqrt(num2), Random.Range(0.5f, 1.2f), new Color(0.75f, 0.74f, 0.72f, Random.Range(0.12f, 0.26f)), Random.Range(0f, 360f));
			}
		}
	}

	public static void Shell(Vector3 port, Vector3 fwd, Vector3 right, string cls)
	{
		if (!FBFxQuality.Shells || cls == "explosivo")
		{
			return;
		}
		Camera cam = FBFxQuality.Cam;
		if ((bool)cam && !((cam.transform.position - port).sqrMagnitude > 324f) && FBFxQuality.Take(1) != 0)
		{
			bool num = cls == "escopeta";
			float num2 = (num ? 0.019f : ((cls == "pistola") ? 0.0095f : 0.0105f));
			float y = (num ? 0.065f : ((cls == "pistola") ? 0.019f : ((cls == "sniper") ? 0.07f : 0.045f)));
			Vector3 vector = Vector3.Cross(fwd, right).normalized;
			if (vector.y < 0f)
			{
				vector = -vector;
			}
			Vector3 v = right * Random.Range(1.4f, 2.6f) + vector * Random.Range(0.9f, 1.8f) - fwd * Random.Range(0.1f, 0.5f);
			Color col = (num ? new Color(0.62f, 0.1f, 0.08f) : new Color(0.8f, 0.62f, 0.26f));
			Quaternion rot = Quaternion.LookRotation(fwd, vector) * Quaternion.Euler(90f, 0f, 0f);
			FBShellSim.Spawn(port, v, rot, new Vector3(num2, y, num2), col, (FBFxQuality.PoolDecals > 100) ? 48 : 20);
		}
	}
}
}
