using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
public static class FBNav
{
	private static Vector3[] centers;

	private static float[] areas;

	private static float totalArea;

	private static List<Vector3> sample;

	private static Vector3? anchor;

	private static readonly Dictionary<long, bool> reach = new Dictionary<long, bool>();

	private static readonly NavMeshPath rpath = new NavMeshPath();

	private static readonly NavMeshPath path = new NavMeshPath();

	// presos 01/11: por célula do cache do Inside: o ponto que decidiu a célula (o primeiro consultado nela) e, se a âncora
	// não chega nele, o fim do caminho parcial (o ponto da ilha principal mais perto dele). O Inside já paga essa busca, a
	// mais cara (com o ponto numa ilha ela varre a ilha principal até acabar os nós); guardar evita que o FBBot a refaça.
	private static readonly Dictionary<long, Vector3> ponto = new Dictionary<long, Vector3>();

	private static readonly Dictionary<long, Vector3> fim = new Dictionary<long, Vector3>();

	private static readonly Vector3[] cantos = new Vector3[256];

	// presos v4 (01/11): limite da arena 10x10 (o quadrado das paredes invisíveis do FBModo10x10). O NavMesh das arenas foi
	// assado sem as paredes (elas nascem no modo) e continuava do lado de fora: na PT (C1, rodada 10) dois bots ficaram uns
	// 90 s presos em (-299,7; 16,3), na parede oeste, com caminho e desvio (stuckN 4) passando por x < -300. Com limite:
	// Inside() dá falso fora do quadrado (RandomPoint e Points não sorteiam lá), Path() puxa o destino para dentro e corta o
	// caminho onde ele sai do quadrado. O FBModo10x10 também recorta o NavMesh nas paredes (NavMeshObstacle) e chama
	// Recarregar() quando o recorte entra. Sem limite (outros modos e mapas sem "arena") nada muda.
	private static bool temLimite;

	private static Vector3 limC;

	private static float limHalf;

	public static bool TemLimite => temLimite;

	public static void SetLimite(Vector3 c, float half)
	{
		temLimite = half > 0f;
		limC = c;
		limHalf = half;
	}

	// p dentro do quadrado com folga "margem" das paredes (sem limite: sempre)
	public static bool DentroLimite(Vector3 p, float margem)
	{
		if (!temLimite)
		{
			return true;
		}
		float h = limHalf - margem;
		return Mathf.Abs(p.x - limC.x) <= h && Mathf.Abs(p.z - limC.z) <= h;
	}

	// p puxado para dentro do quadrado, a "margem" das paredes (sem limite: o próprio p)
	public static Vector3 PraDentro(Vector3 p, float margem)
	{
		if (!temLimite)
		{
			return p;
		}
		float h = Mathf.Max(0f, limHalf - margem);
		p.x = Mathf.Clamp(p.x, limC.x - h, limC.x + h);
		p.z = Mathf.Clamp(p.z, limC.z - h, limC.z + h);
		return p;
	}

	// presos v4: o NavMesh mudou (recorte das paredes da arena): refaz o sorteio de triângulos e os caches de ilha, com a
	// mesma âncora (achada de novo no NavMesh novo)
	public static void Recarregar()
	{
		centers = null;
		sample = null;
		Vector3? a = anchor;
		reach.Clear();
		ponto.Clear();
		fim.Clear();
		if (a.HasValue)
		{
			SetAnchor(a.Value);
		}
		// revisão: refaz a triangulação já (fase de compra, bots parados), não no primeiro quadro da rodada valendo
		Build();
	}

	public static bool Ready
	{
		get
		{
			Build();
			return centers.Length != 0;
		}
	}

	public static void Reset()
	{
		centers = null;
		sample = null;
		anchor = null;
		reach.Clear();
		// presos 01/11: o cache de células sai junto com o reach (Reset)
		ponto.Clear();
		fim.Clear();
		// presos v4: limite da arena é por partida
		temLimite = false;
	}

	public static void SetAnchor(Vector3 p)
	{
		reach.Clear();
		// presos 01/11: âncora nova, cache de células novo
		ponto.Clear();
		fim.Clear();
		// fis: Points() guarda 300 pontos sorteados; os de antes da âncora não passaram pelo filtro de ilha
		sample = null;
		anchor = (NavMesh.SamplePosition(p, out var hit, 6f, -1) ? new Vector3?(hit.position) : null);
	}

	public static bool HasAnchor => anchor.HasValue;

	// presos 01/11: ponto da âncora (null sem âncora), para o FBBot ver em que ilha do NavMesh o bot está
	public static Vector3? Anchor => anchor;

	public static bool InsideNear(Vector3 p, float r)
	{
		if (anchor.HasValue)
		{
			if (NavMesh.SamplePosition(p, out var hit, r, -1))
			{
				return Inside(hit.position);
			}
			return false;
		}
		return true;
	}

	public static bool Inside(Vector3 p)
	{
		// presos v4: fora do quadrado da arena não é área de jogo (sem cache: não gasta busca)
		if (temLimite && !DentroLimite(p, 0f))
		{
			return false;
		}
		Vector3? vector = anchor;
		if (vector.HasValue)
		{
			Vector3 valueOrDefault = vector.GetValueOrDefault();
			// presos 01/11: a chave da célula foi para Chave() (a Celula usa a mesma)
			long key = Chave(p);
			if (reach.TryGetValue(key, out var value))
			{
				return value;
			}
			bool flag = NavMesh.CalculatePath(valueOrDefault, p, -1, rpath);
			value = flag && rpath.status == NavMeshPathStatus.PathComplete;
			reach[key] = value;
			// presos 01/11: guarda quem decidiu a célula e, no parcial, o fim (buffer fixo; o array inteiro só se o caminho
			// tiver mais cantos que o buffer)
			ponto[key] = p;
			if (flag && rpath.status == NavMeshPathStatus.PathPartial)
			{
				int cornersNonAlloc = rpath.GetCornersNonAlloc(cantos);
				if (cornersNonAlloc >= cantos.Length)
				{
					Vector3[] corners = rpath.corners;
					if (corners.Length != 0)
					{
						fim[key] = corners[corners.Length - 1];
					}
				}
				else if (cornersNonAlloc > 0)
				{
					fim[key] = cantos[cornersNonAlloc - 1];
				}
			}
			return value;
		}
		return true;
	}

	// presos 01/11: célula de 4x4x6 m do cache do Inside
	private static long Chave(Vector3 p)
	{
		return ((long)Mathf.FloorToInt(p.x / 4f) << 32) ^ (uint)Mathf.FloorToInt(p.z / 4f) ^ ((long)Mathf.FloorToInt(p.y / 6f) << 52);
	}

	// presos 01/11: p pelo cache do Inside, sem busca nova da âncora quando a célula já foi vista. hp = p no NavMesh;
	// refP = ponto que decidiu a célula; at = fim do caminho parcial da âncora até refP (ponto da ilha principal mais perto).
	// -1 = sem âncora ou sem NavMesh a r de p; 0 = célula dentro da ilha principal; 1 = célula fora, com fim guardado;
	// 2 = célula fora sem fim (caminho inválido); 3 = célula ainda não vista e soCache (nada calculado). A célula vale para
	// o ponto que a decidiu e pode pegar os dois lados de uma cerca: quem usa confere p de perto (FBBot.Principal).
	public static int Celula(Vector3 p, float r, bool soCache, out Vector3 hp, out Vector3 refP, out Vector3 at)
	{
		hp = p;
		refP = p;
		at = p;
		if (!anchor.HasValue || !NavMesh.SamplePosition(p, out var hit, r, -1))
		{
			return -1;
		}
		hp = hit.position;
		long key = Chave(hp);
		if (soCache && !reach.ContainsKey(key))
		{
			return 3;
		}
		bool dentro = Inside(hp);
		if (!ponto.TryGetValue(key, out refP))
		{
			refP = hp;
		}
		if (dentro)
		{
			return 0;
		}
		if (!fim.TryGetValue(key, out at))
		{
			at = hp;
			return 2;
		}
		return 1;
	}

	private static void Build()
	{
		if (centers == null)
		{
			NavMeshTriangulation navMeshTriangulation = NavMesh.CalculateTriangulation();
			int num = navMeshTriangulation.indices.Length / 3;
			centers = new Vector3[num];
			areas = new float[num];
			totalArea = 0f;
			for (int i = 0; i < num; i++)
			{
				Vector3 vector = navMeshTriangulation.vertices[navMeshTriangulation.indices[i * 3]];
				Vector3 vector2 = navMeshTriangulation.vertices[navMeshTriangulation.indices[i * 3 + 1]];
				Vector3 vector3 = navMeshTriangulation.vertices[navMeshTriangulation.indices[i * 3 + 2]];
				centers[i] = (vector + vector2 + vector3) / 3f;
				float num2 = Vector3.Cross(vector2 - vector, vector3 - vector).magnitude * 0.5f;
				totalArea += num2;
				areas[i] = totalArea;
			}
		}
	}

	public static Vector3? RandomPoint(Vector3? near = null, float r = 0f)
	{
		Build();
		if (centers.Length == 0)
		{
			return null;
		}
		if (near.HasValue)
		{
			Vector3 valueOrDefault = near.GetValueOrDefault();
			if (r > 0f)
			{
				for (int i = 0; i < 16; i++)
				{
					if (NavMesh.SamplePosition(valueOrDefault + new Vector3(Random.Range(0f - r, r), 0f, Random.Range(0f - r, r)), out var hit, Mathf.Max(4f, r * 0.3f), -1) && Inside(hit.position))
					{
						return hit.position;
					}
				}
				return null;
			}
		}
		for (int j = 0; j < 24; j++)
		{
			float num = Random.value * totalArea;
			int num2 = 0;
			int num3 = areas.Length - 1;
			while (num2 < num3)
			{
				int num4 = (num2 + num3) / 2;
				if (areas[num4] < num)
				{
					num2 = num4 + 1;
				}
				else
				{
					num3 = num4;
				}
			}
			if (Inside(centers[num2]))
			{
				return centers[num2];
			}
		}
		return anchor;
	}

	public static List<Vector3> Points()
	{
		if (sample != null)
		{
			return sample;
		}
		sample = new List<Vector3>();
		for (int i = 0; i < 300; i++)
		{
			Vector3? vector = RandomPoint();
			if (vector.HasValue)
			{
				Vector3 valueOrDefault = vector.GetValueOrDefault();
				sample.Add(valueOrDefault);
			}
		}
		return sample;
	}

	public static List<Vector3> Fixed()
	{
		Build();
		List<Vector3> list = new List<Vector3>();
		int num = Mathf.Max(1, centers.Length / 600);
		for (int i = 0; i < centers.Length; i += num)
		{
			list.Add(centers[i]);
		}
		return list;
	}

	public static bool Find(Vector3 p, float r, out Vector3 at)
	{
		if (NavMesh.SamplePosition(p, out var hit, r, -1))
		{
			at = hit.position;
			return true;
		}
		at = p;
		return false;
	}

	public static List<Vector3> Path(Vector3 from, Vector3 to)
	{
		// presos v4: destino fora do quadrado da arena vem para dentro (1,5 m da parede)
		if (temLimite && !DentroLimite(to, 1f))
		{
			to = PraDentro(to, 1.5f);
		}
		if (!Find(from, 3f, out var at) || !Find(to, 8f, out var at2))
		{
			return null;
		}
		if (!NavMesh.CalculatePath(at, at2, -1, path) || path.status == NavMeshPathStatus.PathInvalid)
		{
			return null;
		}
		List<Vector3> list = new List<Vector3>(path.corners);
		if (list.Count > 0)
		{
			list.RemoveAt(0);
		}
		if (list.Count == 0)
		{
			list.Add(to);
		}
		// presos v4: caminho que sai do quadrado da arena (NavMesh de fora, antes do recorte entrar) acaba onde sai
		if (temLimite)
		{
			Corta(list, at);
		}
		return list;
	}

	// presos v4: corta o caminho no primeiro canto fora do quadrado (folga 0,2 m): esse canto vira o último ponto do trecho
	// que ainda está a 1 m da parede, e o resto sai. Começo já fora (não acontece: a parede segura): fica só o ponto de
	// dentro mais perto.
	private static void Corta(List<Vector3> l, Vector3 from)
	{
		Vector3 a = from;
		for (int i = 0; i < l.Count; i++)
		{
			if (!DentroLimite(l[i], 0.2f))
			{
				if (!DentroLimite(a, 1f))
				{
					l.Clear();
					l.Add(PraDentro(a, 1.5f));
					return;
				}
				Vector3 b = l[i];
				float lo = 0f;
				float hi = 1f;
				for (int k = 0; k < 12; k++)
				{
					float m = (lo + hi) * 0.5f;
					if (DentroLimite(Vector3.Lerp(a, b, m), 1f))
					{
						lo = m;
					}
					else
					{
						hi = m;
					}
				}
				l[i] = Vector3.Lerp(a, b, lo);
				l.RemoveRange(i + 1, l.Count - i - 1);
				return;
			}
			a = l[i];
		}
	}
}
}
