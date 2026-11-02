using UnityEngine;

namespace FreeBala
{
public class FBFootContact
{
	public float walkStride = 0.78f;

	public float runStride = 1.55f;

	public float minSpeed = 0.55f;

	private const float RELAX = 0.8f;

	private const float MINAMP = 0.025f;

	private const float ARM = 0.55f;

	private const float HIT = 0.22f;

	private const float MINGAP = 0.2f;

	private readonly float[] lo = new float[2]
	{
		0f / 0f,
		0f / 0f
	};

	private readonly float[] hi = new float[2];

	private readonly float[] mean = new float[2]
	{
		0f / 0f,
		0f / 0f
	};

	private readonly float[] lastT = new float[2] { -9f, -9f };

	private readonly bool[] armed = new bool[2];

	private float t;

	private float lastMove = -9f;

	private float strideAcc;

	private int lastFoot = 1;

	public bool Fallback { get; private set; }

	public void Reset()
	{
		lo[0] = (lo[1] = (mean[0] = (mean[1] = 0f / 0f)));
		armed[0] = (armed[1] = false);
		strideAcc = 0f;
	}

	public int Update(float dt, bool bones, float hl, float hr, float speed, bool grounded, bool run)
	{
		t += dt;
		if (!grounded)
		{
			armed[0] = (armed[1] = false);
			strideAcc = Mathf.Min(strideAcc, 0.6f);
			return -1;
		}
		int num = -1;
		bool flag = false;
		if (bones)
		{
			float num2 = Mathf.Clamp01(dt / 0.8f);
			float num3 = Mathf.Clamp01(dt / 0.5f);
			for (int i = 0; i < 2; i++)
			{
				float num4 = ((i == 0) ? hl : hr);
				if (float.IsNaN(mean[i]))
				{
					mean[i] = num4;
				}
				mean[i] += (num4 - mean[i]) * num3;
				float num5 = num4 - mean[i];
				if (float.IsNaN(lo[i]))
				{
					lo[i] = (hi[i] = num5);
					continue;
				}
				lo[i] = ((num5 < lo[i]) ? num5 : (lo[i] + (num5 - lo[i]) * num2));
				hi[i] = ((num5 > hi[i]) ? num5 : (hi[i] + (num5 - hi[i]) * num2));
				float num6 = hi[i] - lo[i];
				if (num6 < 0.025f)
				{
					armed[i] = false;
					continue;
				}
				flag = true;
				if (num5 > lo[i] + num6 * 0.55f)
				{
					armed[i] = true;
				}
				else if (armed[i] && num5 < lo[i] + num6 * 0.22f && t - lastT[i] >= 0.2f && speed >= minSpeed * 0.6f)
				{
					armed[i] = false;
					lastT[i] = t;
					if (num < 0 || i != lastFoot)
					{
						num = i;
					}
				}
			}
			if (flag)
			{
				lastMove = t;
			}
			if (num >= 0)
			{
				lastFoot = num;
				Fallback = false;
				strideAcc = 0f;
				return num;
			}
			if (t - lastMove < 0.6f)
			{
				strideAcc = 0f;
				return -1;
			}
		}
		if (speed < minSpeed)
		{
			strideAcc = Mathf.Min(strideAcc, 0.6f);
			return -1;
		}
		Fallback = true;
		strideAcc += speed * dt / (run ? runStride : walkStride);
		if (strideAcc >= 1f)
		{
			strideAcc -= 1f;
			lastFoot = 1 - lastFoot;
			return lastFoot;
		}
		return -1;
	}
}
}
