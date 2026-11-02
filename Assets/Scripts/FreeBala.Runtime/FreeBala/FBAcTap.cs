using System;
using System.Threading;
using UnityEngine;

namespace FreeBala
{
public class FBAcTap : MonoBehaviour
{
	public const int N = 131072;

	public const int MASK = 131071;

	public readonly float[] L = new float[131072];

	public readonly float[] R = new float[131072];

	public volatile float drive;

	public volatile float bass;

	public volatile float level;

	public long end = -1L;

	public long begin = 9223372036854775807L;

	private int sr;

	private double b0;

	private double b1;

	private double b2;

	private double a1;

	private double a2;

	private readonly double[] z1 = new double[2];

	private readonly double[] z2 = new double[2];

	private readonly double[] lp = new double[2];

	private static AudioClip sil;

	public static AudioClip Silence
	{
		get
		{
			if (!sil)
			{
				int outputSampleRate = AudioSettings.outputSampleRate;
				sil = AudioClip.Create("fb_silencio", outputSampleRate / 2, 1, outputSampleRate, stream: false);
				sil.SetData(new float[outputSampleRate / 2], 0);
			}
			return sil;
		}
	}

	private void Awake()
	{
		sr = AudioSettings.outputSampleRate;
		Shelf(90.0, 5.5);
	}

	private void Shelf(double f, double dB)
	{
		double num = Math.Pow(10.0, dB / 40.0);
		double num2 = Math.PI * 2.0 * f / (double)sr;
		double num3 = Math.Cos(num2);
		double num4 = Math.Sin(num2) / 2.0 * Math.Sqrt(2.0);
		double num5 = 2.0 * Math.Sqrt(num) * num4;
		double num6 = num + 1.0 + (num - 1.0) * num3 + num5;
		b0 = num * (num + 1.0 - (num - 1.0) * num3 + num5) / num6;
		b1 = 2.0 * num * (num - 1.0 - (num + 1.0) * num3) / num6;
		b2 = num * (num + 1.0 - (num - 1.0) * num3 - num5) / num6;
		a1 = -2.0 * (num - 1.0 + (num + 1.0) * num3) / num6;
		a2 = (num + 1.0 + (num - 1.0) * num3 - num5) / num6;
	}

	private void OnAudioFilterRead(float[] data, int ch)
	{
		int num = data.Length / Math.Max(1, ch);
		long num2 = (long)Math.Round(AudioSettings.dspTime * (double)sr);
		long num3 = Interlocked.Read(ref end);
		if (num3 < 0 || num2 > num3 + 2 || num2 < num3 - num)
		{
			begin = num2;
		}
		float num4 = drive * 36f;
		double num5 = 0.0;
		double num6 = 0.0;
		double num7 = 1.0 - Math.Exp(Math.PI * -220.0 / (double)sr);
		for (int i = 0; i < num; i++)
		{
			int num8 = i * ch;
			double num9 = data[num8];
			double num10 = ((ch > 1) ? ((double)data[num8 + 1]) : num9);
			for (int j = 0; j < 2; j++)
			{
				double num11 = ((j == 0) ? num9 : num10);
				double num12 = b0 * num11 + z1[j];
				z1[j] = b1 * num11 - a1 * num12 + z2[j];
				z2[j] = b2 * num11 - a2 * num12;
				if (num4 > 0.4f)
				{
					num12 = (double)(1f + num4) * num12 / (1.0 + (double)num4 * Math.Abs(num12));
				}
				lp[j] += (num12 - lp[j]) * num7;
				num5 += lp[j] * lp[j];
				num6 += num12 * num12;
				if (j == 0)
				{
					L[(num2 + i) & 0x1FFFF] = (float)num12;
				}
				else
				{
					R[(num2 + i) & 0x1FFFF] = (float)num12;
				}
			}
			for (int k = 0; k < ch; k++)
			{
				data[num8 + k] = 0f;
			}
		}
		Interlocked.Exchange(ref end, num2 + num);
		bass = Mathf.Clamp01((float)Math.Sqrt(num5 / (double)(2 * Math.Max(1, num))) * 3.2f);
		level = Mathf.Clamp01((float)Math.Sqrt(num6 / (double)(2 * Math.Max(1, num))) * 2.6f);
	}

	public void Bounds(out long b, out long e)
	{
		e = Interlocked.Read(ref end);
		b = begin;
	}

	public float Read(double pos, int chan)
	{
		Bounds(out var b, out var e);
		return Read(pos, chan, b, e);
	}

	public float Read(double pos, int chan, long begin, long e)
	{
		long num = (long)Math.Floor(pos);
		if (num < begin || num + 1 >= e || num <= e - 131072 + 2048)
		{
			return 0f;
		}
		float num2 = (float)(pos - (double)num);
		int num3 = (int)(num & 0x1FFFF);
		int num4 = (int)((num + 1) & 0x1FFFF);
		float num5 = L[num3] + (L[num4] - L[num3]) * num2;
		float num6 = R[num3] + (R[num4] - R[num3]) * num2;
		return chan switch
		{
			1 => num6, 
			0 => num5, 
			_ => (num5 + num6) * 0.5f, 
		};
	}
}
}
