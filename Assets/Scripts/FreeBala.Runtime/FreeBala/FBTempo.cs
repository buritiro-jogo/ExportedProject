using System.Collections.Generic;

namespace FreeBala
{
// v1 (thread de física): mede o tempo de cada script (Update, LateUpdate, FixedUpdate) sem o Profiler,
// para achar o que pesa na CPU principal no 10x10. Fica DESLIGADO no jogo (On = false): cada chamada
// custa só um teste de bool. O teste do .exe (FBTesteExe) liga, lê e zera por fase.
public static class FBTempo
{
	public static bool On;

	private static readonly object trava = new object();

	private static readonly List<string> nomes = new List<string>();

	private static long[] ticks = new long[256];

	private static int[] chamadas = new int[256];

	public static int Id(string nome)
	{
		lock (trava)
		{
			int i = nomes.IndexOf(nome);
			if (i >= 0)
			{
				return i;
			}
			nomes.Add(nome);
			if (nomes.Count > ticks.Length)
			{
				System.Array.Resize(ref ticks, ticks.Length * 2);
				System.Array.Resize(ref chamadas, chamadas.Length * 2);
			}
			return nomes.Count - 1;
		}
	}

	public static long T0()
	{
		return System.Diagnostics.Stopwatch.GetTimestamp();
	}

	public static void Soma(int id, long t0)
	{
		ticks[id] += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
		chamadas[id]++;
	}

	public static void Zera()
	{
		lock (trava)
		{
			System.Array.Clear(ticks, 0, ticks.Length);
			System.Array.Clear(chamadas, 0, chamadas.Length);
		}
	}

	// (nome, milissegundos somados, chamadas) de tudo que rodou desde o último Zera
	public static List<(string nome, double ms, int n)> Lista()
	{
		List<(string, double, int)> l = new List<(string, double, int)>();
		lock (trava)
		{
			double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
			for (int i = 0; i < nomes.Count; i++)
			{
				if (chamadas[i] > 0)
				{
					l.Add((nomes[i], ticks[i] * f, chamadas[i]));
				}
			}
		}
		return l;
	}
}
}
