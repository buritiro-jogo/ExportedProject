using System;
using UnityEngine;

namespace FreeBala
{
public class FBAcReverb : MonoBehaviour
{
	public FBAcTap tap;

	public volatile float send;

	public volatile float delay = 0.14f;

	public volatile float cutoff = 6000f;

	public volatile float rt60 = 1.7f;

	private static readonly int[] LEN = new int[8] { 1427, 1601, 1867, 2053, 2251, 2399, 2617, 2797 };

	private readonly float[][] buf = new float[8][];

	private readonly int[] pos = new int[8];

	private readonly float[] damp = new float[8];

	private float[] gfb = new float[8];

	private int sr;

	private double D0 = -1.0;

	private float s0;

	private float c0 = 6000f;

	private float lpIn;

	private float rtNow = -1f;

	private float scale = 1f;

	private void Awake()
	{
		sr = AudioSettings.outputSampleRate;
		scale = (float)sr / 48000f;
		for (int i = 0; i < 8; i++)
		{
			buf[i] = new float[Mathf.Max(64, Mathf.RoundToInt((float)LEN[i] * scale))];
		}
	}

	public static FBAcReverb Make(Transform parent, FBAcTap tap)
	{
		GameObject obj = new GameObject("paredao_reverb");
		obj.transform.SetParent(parent, worldPositionStays: false);
		AudioSource audioSource = obj.AddComponent<AudioSource>();
		audioSource.clip = FBAcTap.Silence;
		audioSource.loop = true;
		audioSource.playOnAwake = false;
		audioSource.spatialBlend = 0f;
		audioSource.priority = 3;
		audioSource.volume = 1f;
		audioSource.outputAudioMixerGroup = FBAudio.Group(voice: false);
		FBAcReverb fBAcReverb = obj.AddComponent<FBAcReverb>();
		fBAcReverb.tap = tap;
		audioSource.Play();
		return fBAcReverb;
	}

	private void OnAudioFilterRead(float[] data, int ch)
	{
		int num = data.Length / Math.Max(1, ch);
		FBAcTap fBAcTap = tap;
		if (!fBAcTap)
		{
			Array.Clear(data, 0, data.Length);
			return;
		}
		if (rtNow != rt60)
		{
			rtNow = rt60;
			for (int i = 0; i < 8; i++)
			{
				gfb[i] = Mathf.Pow(10f, -3f * (float)buf[i].Length / (Mathf.Max(0.2f, rtNow) * (float)sr));
			}
		}
		long num2 = (long)Math.Round(AudioSettings.dspTime * (double)sr);
		double num3 = Math.Max(256f, delay * (float)sr);
		if (D0 < 0.0 || Math.Abs(num3 - D0) > 0.25 * (double)sr)
		{
			D0 = num3;
		}
		double num4 = D0 + Math.Max(-0.08 * (double)num, Math.Min(0.08 * (double)num, num3 - D0));
		float num5 = send;
		float num6 = Mathf.Lerp(c0, Mathf.Clamp(cutoff, 200f, 12000f), 0.3f);
		float num7 = 1f - Mathf.Exp((float)Math.PI * -2f * num6 / (float)sr);
		float num8 = 1f - Mathf.Exp((float)Math.PI * -7200f / (float)sr);
		Span<float> span = stackalloc float[8];
		fBAcTap.Bounds(out var b, out var e);
		for (int j = 0; j < num; j++)
		{
			float num9 = ((float)j + 1f) / (float)num;
			float num10 = fBAcTap.Read((double)(num2 + j) - (D0 + (num4 - D0) * (double)num9), 2, b, e) * (s0 + (num5 - s0) * num9);
			lpIn += (num10 - lpIn) * num7;
			num10 = lpIn;
			for (int k = 0; k < 8; k++)
			{
				float[] array = buf[k];
				span[k] = array[pos[k]];
			}
			for (int num11 = 1; num11 < 8; num11 <<= 1)
			{
				for (int l = 0; l < 8; l += num11 << 1)
				{
					for (int m = l; m < l + num11; m++)
					{
						float num12 = span[m];
						float num13 = span[m + num11];
						span[m] = num12 + num13;
						span[m + num11] = num12 - num13;
					}
				}
			}
			for (int n = 0; n < 8; n++)
			{
				float num14 = span[n] * 0.35355f * gfb[n];
				damp[n] += (num14 - damp[n]) * num8;
				float[] array2 = buf[n];
				array2[pos[n]] = damp[n] + num10 * (((n & 1) == 0) ? 0.5f : (-0.5f));
				if (++pos[n] >= array2.Length)
				{
					pos[n] = 0;
				}
			}
			float num15 = (span[0] - span[2] + span[4] - span[6]) * 0.25f;
			float num16 = (span[1] - span[3] + span[5] - span[7]) * 0.25f;
			int num17 = j * ch;
			data[num17] = num15;
			if (ch > 1)
			{
				data[num17 + 1] = num16;
			}
			for (int num18 = 2; num18 < ch; num18++)
			{
				data[num17 + num18] = 0f;
			}
		}
		D0 = num4;
		s0 = num5;
		c0 = num6;
	}
}
}
