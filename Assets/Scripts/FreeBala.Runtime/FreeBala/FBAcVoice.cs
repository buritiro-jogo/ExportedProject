using System;
using UnityEngine;

namespace FreeBala
{
public class FBAcVoice : MonoBehaviour
{
	public FBAcTap tap;

	public int chan = 2;

	public volatile float gain;

	public volatile float cutoff = 20000f;

	public volatile float delay = 0.12f;

	public volatile float pan;

	public volatile float behind;

	public volatile float width = 0.78f;

	private float g0;

	private float c0 = 20000f;

	private float p0;

	private float bh0;

	private double D0 = -1.0;

	private int sr;

	private double y1;

	private double y2;

	private double x1;

	private double x2;

	private float farL;

	private readonly float[] itd = new float[128];

	private int iw;

	private float fadeIn = 1f;

	private void Awake()
	{
		sr = AudioSettings.outputSampleRate;
	}

	public static FBAcVoice Make(Transform parent, FBAcTap tap, int chan, string name)
	{
		GameObject obj = new GameObject(name);
		obj.transform.SetParent(parent, worldPositionStays: false);
		AudioSource audioSource = obj.AddComponent<AudioSource>();
		audioSource.clip = FBAcTap.Silence;
		audioSource.loop = true;
		audioSource.playOnAwake = false;
		audioSource.spatialBlend = 0f;
		audioSource.priority = 2;
		audioSource.volume = 1f;
		audioSource.dopplerLevel = 0f;
		audioSource.outputAudioMixerGroup = FBAudio.Group(voice: false);
		FBAcVoice fBAcVoice = obj.AddComponent<FBAcVoice>();
		fBAcVoice.tap = tap;
		fBAcVoice.chan = chan;
		audioSource.Play();
		return fBAcVoice;
	}

	private void OnAudioFilterRead(float[] data, int ch)
	{
		int num = data.Length / Math.Max(1, ch);
		FBAcTap fBAcTap = tap;
		if (!fBAcTap || ch < 1)
		{
			Array.Clear(data, 0, data.Length);
			return;
		}
		long num2 = (long)Math.Round(AudioSettings.dspTime * (double)sr);
		double num3 = Math.Max(256f, delay * (float)sr);
		if (D0 < 0.0 || Math.Abs(num3 - D0) > 0.25 * (double)sr)
		{
			D0 = num3;
			fadeIn = 0f;
		}
		double num4 = D0 + Math.Max(-0.08 * (double)num, Math.Min(0.08 * (double)num, num3 - D0));
		float num5 = gain;
		float num6 = Mathf.Clamp(pan, -1f, 1f);
		float num7 = Mathf.Clamp01(behind);
		float num8 = Mathf.Lerp(c0, Mathf.Clamp(cutoff * (1f - 0.45f * num7), 120f, Mathf.Min(20000f, (float)sr * 0.45f)), 0.35f);
		double num9 = Math.PI * 2.0 * (double)num8 / (double)sr;
		double num10 = Math.Cos(num9);
		double num11 = Math.Sin(num9) / 1.4142;
		double num12 = 1.0 + num11;
		double num13 = (1.0 - num10) / 2.0 / num12;
		double num14 = (1.0 - num10) / num12;
		double num15 = -2.0 * num10 / num12;
		double num16 = (1.0 - num11) / num12;
		float num17 = width;
		double num18 = 1.0 - Math.Exp(Math.PI * -2.0 * (double)Mathf.Lerp(18000f, 2600f, Mathf.Abs(num6)) / (double)sr);
		fBAcTap.Bounds(out var b, out var e);
		for (int i = 0; i < num; i++)
		{
			float num19 = ((float)i + 1f) / (float)num;
			double num20 = D0 + (num4 - D0) * (double)num19;
			float num21 = fBAcTap.Read((double)(num2 + i) - num20, chan, b, e) * (g0 + (num5 - g0) * num19);
			if (fadeIn < 1f)
			{
				num21 *= fadeIn;
				fadeIn = Mathf.Min(1f, fadeIn + 0.00390625f);
			}
			double num22 = num13 * (double)num21 + num14 * x1 + num13 * x2 - num15 * y1 - num16 * y2;
			x2 = x1;
			x1 = num21;
			y2 = y1;
			y1 = num22;
			float num23 = (float)num22;
			float num24 = p0 + (num6 - p0) * num19;
			float num25 = Mathf.Sqrt(0.5f * (1f + num17 * num24));
			float num26 = Mathf.Sqrt(0.5f * (1f - num17 * num24));
			itd[iw & 0x7F] = num23;
			float num27 = Mathf.Abs(num24) * 0.00066f * (float)sr;
			int num28 = (int)num27;
			float num29 = num27 - (float)num28;
			float num30 = itd[(iw - num28) & 0x7F] * (1f - num29) + itd[(iw - num28 - 1) & 0x7F] * num29;
			iw++;
			farL += (float)((double)(num30 - farL) * num18);
			float num31 = num23;
			float num32 = farL;
			float num33 = ((num24 >= 0f) ? (num32 * num26) : (num31 * num26));
			float num34 = ((num24 >= 0f) ? (num31 * num25) : (num32 * num25));
			if (num33 > 0.8f || num33 < -0.8f)
			{
				num33 = Mathf.Sign(num33) * (0.8f + 0.2f * (float)Math.Tanh((Mathf.Abs(num33) - 0.8f) * 5f));
			}
			if (num34 > 0.8f || num34 < -0.8f)
			{
				num34 = Mathf.Sign(num34) * (0.8f + 0.2f * (float)Math.Tanh((Mathf.Abs(num34) - 0.8f) * 5f));
			}
			int num35 = i * ch;
			data[num35] = num33;
			if (ch > 1)
			{
				data[num35 + 1] = num34;
			}
			for (int j = 2; j < ch; j++)
			{
				data[num35 + j] = 0f;
			}
		}
		D0 = num4;
		g0 = num5;
		c0 = num8;
		p0 = num6;
		bh0 = num7;
	}
}
}
