using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{

// Modo 10x10 por rodadas: cada time nasce junto no seu lado, fase de compra no
// começo da rodada, dinheiro por abate (FBMatch.OnKill já paga 250 + 100 no
// headshot quando o modo tem econ) e por rodada vencida ou perdida.
// Sem zona: os bots avançam em três faixas (esquerda, meio, direita) e, no fim
// da rodada, caçam os últimos inimigos vivos para a rodada não travar.
public class FBModo10x10 : FBMode
{
	public const int TIME = 10;

	public const int WIN = 7;

	private const float ROUND_T = 135f;

	private const float BUY_T = 20f;

	private const float POST_T = 5f;

	private const int PAGA_VITORIA = 2500;

	private const int PAGA_DERROTA = 1400;

	private const int PAGA_SEQUENCIA = 500;

	// v6: teto da sequência de derrotas (1400 + 500 x 4 = 3400).
	private const int SEQ_MAX = 5;

	private const float HUNT_T = 80f;

	private readonly int[] wins = new int[2];

	private readonly int[] lossStreak = new int[2];

	private readonly List<int> results = new List<int>();

	private readonly Vector3[] baseC = new Vector3[2];

	private int round;

	private string phase = "pre";

	private float pt;

	private float rt = ROUND_T;

	private float liveT;

	private int lastSec;

	public override int Round => round;

	public FBModo10x10(FBMatch g)
		: base(g)
	{
		name = "10X10";
		respawn = false;
		rounds = true;
		econ = true;
	}

	public override void Setup()
	{
		CarregarRotas();
		MontarArena();
		Ancorar();
		CarregarPares();
		NomesUnicos();
		G.SpawnSquad(FBSession.team, TIME - 1, "squad");
		G.SpawnSquad(1 - FBSession.team, TIME, "squad");
		FBEconomy.Reset();
		StartRound();
	}

	// FBMatch separa só 6 personagens por time e repete depois disso, então em
	// 10x10 apareciam nomes iguais no mesmo time. Aqui cada time recebe até 10
	// personagens diferentes, sem repetir entre os times enquanto der.
	// Rotas do mapa (fb_data: maps.<id>.lanes), cada uma uma lista de pontos de
	// base a base. Ponto [x, y, z] no formato do fb_data (Coords.FromWeb) ou
	// [x, z] (altura achada no chão). Sem lanes o modo usa a linha reta com desvio.
	private readonly List<List<Vector3>> rotas = new List<List<Vector3>>();

	private void CarregarRotas()
	{
		rotas.Clear();
		List<object> lanes = Json.L(G.Level, "lanes") ?? Json.L(MapaRaiz(), "lanes");
		if (lanes == null)
		{
			return;
		}
		foreach (object lo in lanes)
		{
			List<object> pts = lo as List<object>;
			if (pts == null)
			{
				continue;
			}
			List<Vector3> rota = new List<Vector3>();
			foreach (object po in pts)
			{
				List<object> c = po as List<object>;
				if (c == null || c.Count < 2)
				{
					continue;
				}
				if (c.Count >= 3)
				{
					rota.Add(Coords.FromWeb(c));
				}
				else
				{
					float x = -Json.Fv(c[0]);
					float z = Json.Fv(c[1]);
					float? y = FBWorld.GroundY(x, 200f, z, 0f, 400f);
					rota.Add(new Vector3(x, y ?? 0f, z));
				}
			}
			if (rota.Count >= 2)
			{
				rotas.Add(rota);
			}
		}
	}

	// O bloco do mapa no fb_data (maps.<id>). "lanes" e "arena" podem estar nele ou dentro de "level".
	private Dictionary<string, object> MapaRaiz()
	{
		string id = G.Map?.id ?? FBSession.map;
		return Json.O(Json.O(GameData.Root, "maps"), id);
	}

	// Paredes invisíveis na borda da arena (fb_data: "arena": {"cx", "cy", "half"}, no formato do fb_data,
	// x invertido como no Coords.FromWeb). Seguram jogador, bots, carros e tiros. Sem "arena", nada muda.
	private GameObject arenaGo;

	private Vector3 arenaC;

	private float arenaHalf;

	private void MontarArena()
	{
		Dictionary<string, object> a = Json.O(G.Level, "arena") ?? Json.O(MapaRaiz(), "arena");
		if (a == null)
		{
			return;
		}
		float half = Json.F(a, "half", 300f);
		if (half < 20f)
		{
			return;
		}
		Vector3 c = new Vector3(-Json.F(a, "cx"), 0f, Json.F(a, "cy"));
		arenaC = c;
		arenaHalf = half;
		float chao = FBWorld.GroundY(c.x, 300f, c.z, 0f, 700f) ?? 0f;
		const float esp = 4f;
		const float alto = 200f;
		float meio = chao + 60f;
		// presos v4 (revisão): pontos de conferência do recorte no meio de cada parede (9 por parede), cada um na altura do
		// próprio chão (antes era a altura do centro da arena, e na Orla o centro é o fundo da lagoa). Lidos antes das paredes
		// existirem: depois o raio de cima bateria nelas.
		List<Vector3> conferir = new List<Vector3>();
		{
			float dw = half + esp * 0.5f;
			for (int lado = 0; lado < 4; lado++)
			{
				for (int k = -4; k <= 4; k++)
				{
					float t = k / 5f * half;
					Vector3 p = (lado == 0) ? new Vector3(c.x + t, chao, c.z + dw) : ((lado == 1) ? new Vector3(c.x + t, chao, c.z - dw) : ((lado == 2) ? new Vector3(c.x + dw, chao, c.z + t) : new Vector3(c.x - dw, chao, c.z + t)));
					// revisão 2: desce pelas camadas (copa, telhado, carro) até achar chão com NavMesh; por último a altura do centro
					bool achou = false;
					float topo = chao + 80f;
					for (int camada = 0; camada < 5 && !achou; camada++)
					{
						float? gy = FBWorld.GroundY(p.x, topo, p.z, 0f, topo - (chao - 80f));
						if (!gy.HasValue)
						{
							break;
						}
						if (NavMesh.SamplePosition(new Vector3(p.x, gy.Value, p.z), out var hit, 1.5f, -1))
						{
							conferir.Add(hit.position);
							achou = true;
						}
						topo = gy.Value - 0.05f;
					}
					if (!achou && NavMesh.SamplePosition(p, out var hit2, 1.5f, -1))
					{
						conferir.Add(hit2.position);
					}
				}
			}
		}
		arenaGo = new GameObject("FB_ArenaParedes") { layer = FBWorld.LMap };
		arenaGo.transform.position = new Vector3(c.x, 0f, c.z);
		void Parede(Vector3 centro, Vector3 tam)
		{
			BoxCollider bc = arenaGo.AddComponent<BoxCollider>();
			bc.center = centro;
			bc.size = tam;
			// presos v4 (física, 01/11): o NavMesh foi assado sem estas paredes e passava por fora delas (PT, C1 R10: dois
			// bots 90 s presos em (-299,7; 16,3) com caminho por x < -300). Cada parede recorta o NavMesh (carving), um
			// objeto filho por parede (um NavMeshObstacle por objeto). Parado desde o começo: recorta já no próximo quadro.
			GameObject rec = new GameObject("recorte_navmesh") { layer = FBWorld.LMap };
			rec.transform.SetParent(arenaGo.transform, false);
			rec.transform.localPosition = centro;
			NavMeshObstacle ob = rec.AddComponent<NavMeshObstacle>();
			ob.shape = NavMeshObstacleShape.Box;
			ob.center = Vector3.zero;
			ob.size = tam;
			ob.carveOnlyStationary = true;
			ob.carvingTimeToStationary = 0f;
			ob.carving = true;
		}
		Parede(new Vector3(0f, meio, half + esp * 0.5f), new Vector3(2f * half + 2f * esp, alto, esp));
		Parede(new Vector3(0f, meio, -half - esp * 0.5f), new Vector3(2f * half + 2f * esp, alto, esp));
		Parede(new Vector3(half + esp * 0.5f, meio, 0f), new Vector3(esp, alto, 2f * half + 2f * esp));
		Parede(new Vector3(-half - esp * 0.5f, meio, 0f), new Vector3(esp, alto, 2f * half + 2f * esp));
		Debug.Log($"[BURITIRO] arena 10x10: quadrado de {2f * half:0} m com centro em ({c.x:0}, {c.z:0}), paredes invisíveis ligadas");
		// presos v4: o FBNav não sorteia, não valida e não traça caminho fora do quadrado, e limpa os caches quando o recorte
		// do NavMesh entrar (os pares de base da rodada 1 e as bases oficiais são conferidos antes disso, com o corte do Path)
		FBNav.SetLimite(c, half);
		if ((bool)G)
		{
			G.StartCoroutine(EsperaRecorte(conferir));
		}
	}

	// presos v4: espera o recorte do NavMesh nas paredes e recarrega o FBNav. pts = pontos no meio das paredes que tinham
	// NavMesh antes (MontarArena). Espera sempre 2 quadros (o Unity aplica o recorte no fim do quadro; antes, sem ponto
	// para conferir, recarregava no mesmo quadro, sem recorte) e depois até nenhum ponto ter NavMesh a 0,6 m, ou 6 s.
	private IEnumerator EsperaRecorte(List<Vector3> pts)
	{
		int n0 = pts.Count;
		int q0 = Time.frameCount;
		float t0 = Time.realtimeSinceStartup;
		yield return null;
		yield return null;
		pts.RemoveAll((Vector3 p) => !NavMesh.SamplePosition(p, out _, 0.6f, -1));
		while (pts.Count > 0 && Time.realtimeSinceStartup - t0 < 6f)
		{
			yield return null;
			pts.RemoveAll((Vector3 p) => !NavMesh.SamplePosition(p, out _, 0.6f, -1));
		}
		FBNav.Recarregar();
		string estado = (n0 == 0) ? "sem pontos de parede para conferir (esperei 2 quadros)" : ((pts.Count == 0) ? "feito" : "INCOMPLETO");
		Debug.Log($"[BURITIRO] arena 10x10: recorte do NavMesh nas paredes {estado} em {Time.frameCount - q0} quadros ({Time.realtimeSinceStartup - t0:0.00} s) · {n0} pontos de parede com NavMesh antes, {pts.Count} ainda com NavMesh · caches do FBNav recarregados");
	}

	// Comprimento já andado (projeção) de p na rota r.
	private static float Andado(List<Vector3> r, Vector3 p)
	{
		float melhorD = float.MaxValue, melhorS = 0f, s = 0f;
		for (int i = 1; i < r.Count; i++)
		{
			Vector3 a = r[i - 1].Flat(), b = r[i].Flat();
			Vector3 ab = b - a;
			float len = ab.magnitude;
			float t = (len > 0.01f) ? Mathf.Clamp01(Vector3.Dot(p.Flat() - a, ab) / (len * len)) : 0f;
			float d = (a + ab * t - p.Flat()).sqrMagnitude;
			if (d < melhorD)
			{
				melhorD = d;
				melhorS = s + len * t;
			}
			s += len;
		}
		return melhorS;
	}

	private static float Comprimento(List<Vector3> r)
	{
		float total = 0f;
		for (int i = 1; i < r.Count; i++)
		{
			total += (r[i] - r[i - 1]).Flat().magnitude;
		}
		return total;
	}

	// Ponto a uma fração 0..1 do comprimento da rota, saindo da base "de".
	private static Vector3 NaRota(List<Vector3> rota, Vector3 de, float frac)
	{
		List<Vector3> r = rota;
		if ((rota[0] - de).Flat().sqrMagnitude > (rota[rota.Count - 1] - de).Flat().sqrMagnitude)
		{
			r = new List<Vector3>(rota);
			r.Reverse();
		}
		float total = 0f;
		for (int i = 1; i < r.Count; i++)
		{
			total += (r[i] - r[i - 1]).Flat().magnitude;
		}
		float alvo = total * Mathf.Clamp01(frac);
		for (int i = 1; i < r.Count; i++)
		{
			float seg = (r[i] - r[i - 1]).Flat().magnitude;
			if (alvo <= seg || i == r.Count - 1)
			{
				return Vector3.Lerp(r[i - 1], r[i], (seg > 0.01f) ? Mathf.Clamp01(alvo / seg) : 1f);
			}
			alvo -= seg;
		}
		return r[r.Count - 1];
	}

	private void NomesUnicos()
	{
		List<string> todos = (from k in GameData.Skins
			where !k.role && k.id != FBSession.skin && FBRegistry.I.HasChar(k.id)
			select k.id).Distinct().OrderBy((string _) => Random.value).ToList();
		if (todos.Count == 0)
		{
			return;
		}
		int meu = FBSession.team;
		int deles = 1 - meu;
		// Mantém quem já estava em cada time (no modo pvl o lado importa), completa com os livres.
		List<string> livres = todos.Where((string id) => !G.typePool[0].Contains(id) && !G.typePool[1].Contains(id)).ToList();
		foreach (int t in new int[2] { meu, deles })
		{
			List<string> pool = G.typePool[t].Where((string id) => id != FBSession.skin).Distinct().ToList();
			int precisa = (t == meu) ? (TIME - 1) : TIME;
			while (pool.Count < precisa && livres.Count > 0)
			{
				pool.Add(livres[0]);
				livres.RemoveAt(0);
			}
			if (pool.Count == 0)
			{
				pool.Add(todos[0]);
			}
			G.typePool[t] = pool;
		}
	}

	// Primeiro tempo: cada time na sua base. A partir da rodada SWAP+1 os lados
	// trocam, como no intervalo. Assim nenhum time fica a partida inteira na base pior.
	private const int SWAP = 6;

	private bool Trocado => round > SWAP;

	// ---- Bases que mudam a cada rodada (pedido do Ruan: aproveitar o mapa todo) ----
	// A rodada 1 usa as bases do mapa. Da 2 em diante, cada rodada sorteia um par de
	// bases novo: de "basePairs" no fb_data, se o mapa tiver, ou sorteado no NavMesh,
	// longe das bases das últimas rodadas, a uma distância boa uma da outra, sem uma
	// enxergar a outra e com espaço para os 10 nascerem. O lado de cada time no par
	// é sorteado. Se nada servir, fica a base do mapa (e a troca de lado normal).
	private readonly List<(Vector3 a, Vector3 b, int n)> pares = new List<(Vector3, Vector3, int)>();

	private readonly List<Vector3> usadas = new List<Vector3>();

	// v7 (thread de física): bases oficiais do mapa (rodada 1). Em mapa sem "arena" (vila_jardim,
	// praca_aguas) o sorteio fica perto delas e a uma distância parecida com a oficial. No v6 o sorteio
	// usava a extensão do NavMesh inteiro e as bases ficavam a 1,3-1,7 km (contato só depois de 100 s).
	private Vector3 baseOf0, baseOf1;
	private bool temOficial;

	private float largura = 25f;

	private float meioRodada = 0.55f;

	private static Vector3 Ponto(List<object> c)
	{
		if (c.Count >= 3)
		{
			return Coords.FromWeb(c);
		}
		float x = -Json.Fv(c[0]);
		float z = Json.Fv(c[1]);
		return new Vector3(x, FBWorld.GroundY(x, 200f, z, 0f, 400f) ?? 0f, z);
	}

	// fb_data: "basePairs": [ [ [x,y,z], [x,y,z] ], ... ] (formato do fb_data, como as lanes).
	private void CarregarPares()
	{
		pares.Clear();
		List<object> l = Json.L(G.Level, "basePairs") ?? Json.L(MapaRaiz(), "basePairs");
		if (l == null)
		{
			return;
		}
		foreach (object o in l)
		{
			List<object> par = o as List<object>;
			if (par != null && par.Count >= 2 && par[0] is List<object> pa && par[1] is List<object> pb && pa.Count >= 2 && pb.Count >= 2)
			{
				pares.Add((Ponto(pa), Ponto(pb), pares.Count + 1));
			}
		}
		for (int i = pares.Count - 1; i > 0; i--)
		{
			int j = Random.Range(0, i + 1);
			(Vector3, Vector3, int) t = pares[i];
			pares[i] = pares[j];
			pares[j] = t;
		}
	}

	// v11: o FBNav só filtra ilhas do NavMesh (Inside/InsideNear) quando tem âncora, e o
	// SetAnchor só rodava no modo role. Sem âncora, a validação de bases da v8/v9 passava tudo
	// e um time travou numa quadra cercada (PT, partida g2c, R3).
	private bool ancorado;

	private void Ancorar()
	{
		if (ancorado)
		{
			return;
		}
		Vector3? p = null;
		FBMatch.Line l0 = (G.teamLine != null && G.teamLine.Length > 0) ? G.teamLine[0] : null;
		if (l0 != null && l0.spots.Count > 0)
		{
			p = l0.spots[0];
		}
		else if (arenaHalf > 0f)
		{
			p = arenaC;
		}
		if (!p.HasValue)
		{
			return;
		}
		FBNav.SetAnchor(p.Value);
		ancorado = true;
		Debug.Log($"[BURITIRO] 10x10 âncora do NavMesh em ({p.Value.x:0}, {p.Value.y:0.0}, {p.Value.z:0})");
	}

	private bool BasesDaRodada()
	{
		Ancorar();
		FBMatch.Line l0 = G.teamLine[0];
		FBMatch.Line l1 = G.teamLine[1];
		if (round <= 1)
		{
			if (l0 != null && l0.spots.Count > 0 && l1 != null && l1.spots.Count > 0)
			{
				// v9: lado da rodada 1 sorteado. Na Praça do PT o par oficial favorece a
				// base do bosque, e o time 0 sempre começava no lago (time 1 ganhou 10 de 12).
				if (Random.value < 0.5f)
				{
					G.teamLine[0] = l1;
					G.teamLine[1] = l0;
					l0 = G.teamLine[0];
					l1 = G.teamLine[1];
				}
				usadas.Add(l0.spots[0]);
				usadas.Add(l1.spots[0]);
				baseOf0 = l0.spots[0];
				baseOf1 = l1.spots[0];
				temOficial = true;
				LogBases("bases do mapa", baseOf0, baseOf1);
			}
			return false;
		}
		Vector3 a;
		Vector3 b;
		string origem;
		// v8: o par do fb_data só vale se as duas bases estão no NavMesh principal
		// (ligadas ao resto do mapa) e cabem a grade do time. Na Orla (exe C, R3)
		// um time ficou a 8 m de onde nasceu a rodada toda: suspeita de base numa
		// ilha do NavMesh (praia/beira do rio) sem caminho para o meio do mapa.
		bool achou = false;
		a = Vector3.zero;
		b = Vector3.zero;
		origem = "";
		for (int tent = 0; tent < pares.Count && !achou; tent++)
		{
			int n;
			Vector3 pa, pb;
			(pa, pb, n) = pares[proxPar % pares.Count];
			proxPar++;
			string porque = ParRuim(pa, pb);
			if (porque == null && pares.Count >= 3 && temOficial && MesmoPar(pa, pb, baseOf0, baseOf1))
			{
				porque = "é o par oficial, já usado na rodada 1";
			}
			if (porque != null)
			{
				Debug.Log($"[BURITIRO] 10x10 rodada {round}: par {n} de {pares.Count} do fb_data descartado ({porque})");
				continue;
			}
			a = pa;
			b = pb;
			origem = $"par {n} de {pares.Count} do fb_data";
			achou = true;
		}
		if (!achou)
		{
			if (SortearPar(out a, out b))
			{
				origem = "par sorteado no NavMesh";
			}
			else
			{
				Debug.Log($"[BURITIRO] 10x10 rodada {round}: nenhum par novo serviu, ficam as bases da rodada anterior");
				return false;
			}
		}
		bool virou = Random.value < 0.5f;
		if (virou)
		{
			Vector3 t = a;
			a = b;
			b = t;
		}
		G.teamLine[0] = Linha(a, b);
		G.teamLine[1] = Linha(b, a);
		LogBases(origem + (virou ? ", lados trocados" : ""), a, b);
		usadas.Add(a);
		usadas.Add(b);
		if (usadas.Count > 6)
		{
			usadas.RemoveRange(0, usadas.Count - 6);
		}
		return true;
	}

	private int proxPar;

	// Comprimento a pé de a até b, ou -1 se o NavMesh não chega lá (caminho parcial ou vazio).
	// v13: mede nos dois sentidos e só vale se os dois chegam. O FBNav.Path procura a
	// origem num raio de 3 m e o destino num de 8 m, então ida e volta podem dar resultados
	// diferentes. Na PT o par 5 passava no ParRuim (a -> b) e o log (b -> a, lados trocados)
	// dizia "sem caminho completo"; dois bots ficaram 90 s presos na borda da arena.
	private static float CaminhoAPe(Vector3 a, Vector3 b)
	{
		float ida = CaminhoAPe1(a, b);
		if (ida < 0f)
		{
			return -1f;
		}
		float volta = CaminhoAPe1(b, a);
		if (volta < 0f)
		{
			return -1f;
		}
		return Mathf.Max(ida, volta);
	}

	private static float CaminhoAPe1(Vector3 a, Vector3 b)
	{
		List<Vector3> cam = FBNav.Path(a, b);
		if (cam == null || cam.Count < 2)
		{
			return -1f;
		}
		if ((cam[cam.Count - 1] - b).Flat().magnitude > 4f)
		{
			return -1f;
		}
		float c = (cam[0] - a).Flat().magnitude;
		for (int k = 1; k < cam.Count; k++)
		{
			c += (cam[k] - cam[k - 1]).Flat().magnitude;
		}
		if (c < (a - b).Flat().magnitude * 0.9f)
		{
			return -1f;
		}
		return c;
	}

	private static bool MesmoPar(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
	{
		return ((a - c).Flat().magnitude < 30f && (b - d).Flat().magnitude < 30f) || ((a - d).Flat().magnitude < 30f && (b - c).Flat().magnitude < 30f);
	}

	private string ParRuim(Vector3 pa, Vector3 pb)
	{
		if (!FBNav.InsideNear(pa, 4f))
		{
			return "base A fora do NavMesh principal";
		}
		if (!FBNav.InsideNear(pb, 4f))
		{
			return "base B fora do NavMesh principal";
		}
		if (Grade(pa, 0f).Count < 8)
		{
			return "base A sem espaço para a grade";
		}
		if (Grade(pb, 0f).Count < 8)
		{
			return "base B sem espaço para a grade";
		}
		if (CaminhoAPe(pa, pb) < 0f)
		{
			return "sem caminho a pé entre as bases";
		}
		return null;
	}

	// v7c: uma linha por rodada com o par usado e onde cada time nasce (no v7b só o sorteio escrevia;
	// com basePairs não dava para saber qual par travou uma rodada).
	private void LogBases(string origem, Vector3 b0, Vector3 b1)
	{
		// v12: usa CaminhoAPe (soma o trecho da base até o 1º canto e recusa caminho parcial).
		float c = CaminhoAPe(b0, b1);
		string pe = (c < 0f) ? "sem caminho completo no NavMesh" : $"a pé {c:0} m";
		Debug.Log($"[BURITIRO] 10x10 rodada {round} bases: {origem} · time0 em ({b0.x:0}, {b0.y:0.0}, {b0.z:0}) · time1 em ({b1.x:0}, {b1.y:0.0}, {b1.z:0}) · reta {(b0 - b1).Flat().magnitude:0} m · {pe}");
	}

	private static FBMatch.Line Linha(Vector3 a, Vector3 olharPara)
	{
		FBMatch.Line l = new FBMatch.Line
		{
			yaw = Quaternion.LookRotation((olharPara - a).Flat()).eulerAngles.y
		};
		l.spots.Add(a);
		return l;
	}

	private bool DentroArena(Vector3 p, float margem)
	{
		if (arenaHalf <= 0f)
		{
			return true;
		}
		return Mathf.Abs(p.x - arenaC.x) < arenaHalf - margem && Mathf.Abs(p.z - arenaC.z) < arenaHalf - margem;
	}

	private bool SortearPar(out Vector3 a, out Vector3 b)
	{
		a = (b = Vector3.zero);
		List<Vector3> pts = FBNav.Points().Where((Vector3 p) => DentroArena(p, 30f)).ToList();
		// v7: sem arena, só pontos perto da área de jogo das bases oficiais
		float dOf = temOficial ? (baseOf0 - baseOf1).Flat().magnitude : 0f;
		Vector3 centroOf = (baseOf0 + baseOf1) * 0.5f;
		float raioJogo = Mathf.Max(200f, dOf * 1.1f);
		bool perto = arenaHalf <= 0f && temOficial && dOf > 40f;
		if (perto)
		{
			pts = pts.Where((Vector3 p) => (p - centroOf).Flat().magnitude <= raioJogo).ToList();
		}
		// v9: FBNav.Points são só 300 pontos do mapa inteiro; na vila_jardim (3 x 3,6 km)
		// sobravam ~10 dentro do raio e o sorteio falhava calado em todas as rodadas.
		// Agora completa com amostras locais (área da arena ou raio em volta das bases oficiais).
		int nGlobal = pts.Count;
		if (arenaHalf > 0f || perto)
		{
			Vector3 cLocal = (arenaHalf > 0f) ? arenaC : centroOf;
			float rLocal = (arenaHalf > 0f) ? (arenaHalf - 30f) : raioJogo;
			for (int i = 0; i < 100; i++)
			{
				Vector3? q = FBNav.RandomPoint(cLocal, rLocal);
				if (q.HasValue && DentroArena(q.Value, 30f) && (!perto || (q.Value - centroOf).Flat().magnitude <= raioJogo))
				{
					pts.Add(q.Value);
				}
			}
		}
		if (pts.Count < 20)
		{
			Debug.Log($"[BURITIRO] 10x10 rodada {round}: sorteio sem pontos suficientes ({nGlobal} do mapa + {pts.Count - nGlobal} locais)");
			return false;
		}
		// Nada de nascer em laje ou telhado: só pontos perto da altura do chão mais comum.
		List<float> ys = pts.Select((Vector3 p) => p.y).OrderBy((float y) => y).ToList();
		float chao = ys[ys.Count / 2];
		pts = pts.Where((Vector3 p) => Mathf.Abs(p.y - chao) < 4f).ToList();
		if (pts.Count < 20)
		{
			Debug.Log($"[BURITIRO] 10x10 rodada {round}: sorteio sem pontos no nível do chão ({pts.Count})");
			return false;
		}
		int fDist = 0, fVisao = 0, fGrade = 0, fCaminho = 0, fLonge = 0;
		float ext = 0f;
		for (int i = 0; i < pts.Count; i += 2)
		{
			for (int j = i + 1; j < pts.Count; j += 2)
			{
				ext = Mathf.Max(ext, (pts[i] - pts[j]).Flat().magnitude);
			}
		}
		float dMin = (arenaHalf > 0f) ? Mathf.Min(300f, ext * 0.55f) : (ext * 0.5f);
		float dMax = (arenaHalf > 0f) ? 450f : (ext * 0.95f);
		if (arenaHalf <= 0f && temOficial && dOf > 40f)
		{   // v7: distância parecida com a das bases oficiais (vila_jardim 191 m -> 150 a 250 m)
			dMin = Mathf.Clamp(dOf * 0.8f, 120f, 300f);
			dMax = Mathf.Clamp(dOf * 1.3f, dMin + 40f, 400f);
		}
		float caminhoMax = Mathf.Max(dMax * 1.6f, 250f);
		int testes = 0;
		foreach (Vector3 pa in pts.OrderBy((Vector3 _) => Random.value))
		{
			if (testes > 250)
			{
				break;
			}
			if (usadas.Any((Vector3 u) => (u - pa).Flat().magnitude < 50f))
			{
				continue;
			}
			testes++;
			if (Grade(pa, 0f).Count < 8)
			{
				fGrade++;
				continue;
			}
			List<Vector3> cands = pts.Where((Vector3 q) => { float d = (q - pa).Flat().magnitude; return d >= dMin && d <= dMax; }).OrderBy((Vector3 _) => Random.value).Take(12).ToList();
			if (cands.Count == 0)
			{
				fDist++;
			}
			foreach (Vector3 pb in cands)
			{
				testes++;
				if (usadas.Any((Vector3 u) => (u - pb).Flat().magnitude < 50f))
				{
					continue;
				}
				Vector3 o = pa + Vector3.up * 1.6f;
				Vector3 d2 = pb + Vector3.up * 1.6f - o;
				if (!FBWorld.RayHit(o, d2.normalized, d2.magnitude))
				{
					fVisao++;
					continue;
				}
				if (Grade(pb, 0f).Count < 8)
				{
					fGrade++;
					continue;
				}
				// v10: FBNav.Path aceita caminho parcial e devolve 1 ponto quando não chega
				// (exe F: "a pé 3 m" com bases a 172 m). Agora só vale caminho que chega no pb.
				float compr = CaminhoAPe(pa, pb);
				if (compr < 0f)
				{
					fCaminho++;
					continue;
				}
				if (compr > caminhoMax)
				{   // v7: caminho a pé muito maior que a reta (volta pelo bairro todo)
					fLonge++;
					continue;
				}
				a = pa;
				b = pb;
				Debug.Log($"[BURITIRO] 10x10 rodada {round}: bases novas a {(pa - pb).Flat().magnitude:0} m (a pé {compr:0} m; faixa {dMin:0}-{dMax:0} m)");
				return true;
			}
		}
		Debug.Log($"[BURITIRO] 10x10 rodada {round}: sorteio falhou com {pts.Count} pontos, faixa {dMin:0}-{dMax:0} m · descartes: sem par na faixa {fDist}, com visão {fVisao}, sem grade {fGrade}, sem caminho {fCaminho}, caminho longo {fLonge}");
		return false;
	}

	private void StartRound()
	{
		round++;
		phase = "buy";
		pt = BUY_T;
		rt = ROUND_T;
		liveT = 0f;
		mortesRodada = 0;
		diagT = 0f;
		G.ResetRound();
		largura = FBUtil.Rnd(15f, 40f);
		meioRodada = FBUtil.Rnd(0.42f, 0.62f);
		bool variou = BasesDaRodada();
		if (Trocado && !variou)
		{
			FBMatch.Line tmp = G.teamLine[0];
			G.teamLine[0] = G.teamLine[1];
			G.teamLine[1] = tmp;
		}
		if (round == SWAP + 1 && !variou)
		{
			FBHUD.I?.Toast("INTERVALO", "TROCA DE LADO", $"{wins[FBSession.team]} × {wins[1 - FBSession.team]}", GOLD);
		}
		Espalhar();
		G.freeze = true;
		FBHUD.I?.MatchToast($"RODADA {round} · FASE DE COMPRA", GOLD, 3f);
		lastSec = 0;
		FBAudio.Synth((wins[0] == WIN - 1 || wins[1] == WIN - 1) ? "lastround" : "round");
		AbrirCompra();
	}

	// Igual a FBEconomy.BuyPhase(true), mas sem FBEconomy.BotBuy. O BotBuy sorteia
	// 3 armas por time no começo da partida (lista do AZUL e lista do VERMELHO são
	// diferentes); com $1000 um time às vezes acha SMG barata e o outro só pistola,
	// e esse time ganhava todas as rodadas. Aqui os dois times compram pela mesma regra.
	// v12: teto de dinheiro (o FBEconomy deixava chegar a $9999 e o time que ganhava
	// embolava tudo: R6 com mediana $9999 contra $3550).
	private const int TETO = 8000;

	private void AbrirCompra()
	{
		foreach (FBCharacter a in FBMatch.Actors().ToList())
		{
			if (FBEconomy.money.ContainsKey(a) && FBEconomy.money[a] > TETO)
			{
				FBEconomy.money[a] = TETO;
			}
		}
		FBEconomy.buying = true;
		FBHUD.I?.BuyHint(on: true);
		List<FBCharacter> bots = FBMatch.Bots.ToList();
		string[] modo = new string[2];
		for (int t = 0; t < 2; t++)
		{
			modo[t] = DecidirCompra(t, bots);
		}
		Debug.Log($"[BURITIRO] 10x10 rodada {round} compra: time0={modo[0]} time1={modo[1]}");
		foreach (FBCharacter b in bots)
		{
			if ((bool)b && b.alive && b.team < 2)
			{
				Comprar(b, modo[b.team], lossStreak[b.team] >= 1);
			}
		}
		if ((bool)FBMatch.Player && FBMatch.Player.alive)
		{
			FBShop.Show(on: true);
		}
		FBEconomy.Hud();
	}

	private static int Nivel(WeaponDef w)
	{
		if (w == null)
		{
			return -1;
		}
		switch (w.cls)
		{
		case "fuzil":
			return 2;
		case "submetralhadora":
		case "escopeta":
			return 1;
		default:
			return 0;
		}
	}

	// Mesma regra para os 20 bots, pela quantidade de dinheiro:
	// fuzil + colete quando dá, senão submetralhadora + colete, senão só colete.
	// Escopeta e sniper ficam de fora (os bots usam mal em mapa grande).
	// v6: o time decide junto, como nos jogos de rodada.
	// COMPLETA: a mediana do time paga fuzil + colete.
	// FORCADA: não paga, mas o time já perdeu 2 seguidas, ou o inimigo está a
	//          1 rodada de fechar, ou é a rodada 1 (todo mundo com $1000) ou a 2 (v14: quem
	//          perdeu a pistola compra SMG em vez de ECO).
	// ECO: guarda o dinheiro (só pistola) para comprar fuzil na rodada seguinte.
	// Antes cada bot gastava tudo em SMG toda rodada, morria, perdia a arma e
	// nunca juntava para o fuzil: quem ganhava a rodada 1 ganhava quase todas.
	private string DecidirCompra(int t, List<FBCharacter> bots)
	{
		List<int> din = new List<int>();
		foreach (FBCharacter b in bots)
		{
			if ((bool)b && b.alive && b.team == t && FBEconomy.money.ContainsKey(b))
			{
				din.Add(FBEconomy.money[b]);
			}
		}
		if (din.Count == 0)
		{
			return "COMPLETA";
		}
		din.Sort();
		int mediana = din[din.Count / 2];
		int precoFuzil = PrecoMinimo("fuzil");
		int precisa = ((precoFuzil > 0) ? precoFuzil : 2200) + 650;
		string m;
		if (mediana >= precisa)
		{
			m = "COMPLETA";
		}
		// v14 (02/11): o perdedor da rodada 1 faz compra FORCADA na rodada 2 (SMG/colete) em vez de ECO; antes quem ganhava
		// a rodada 1 ganhou a rodada 2 em 11 de 11 partidas (fuzil contra pistola).
		else if (round <= 2 || lossStreak[t] >= 2 || wins[1 - t] >= WIN - 1)
		{
			m = "FORCADA";
		}
		else
		{
			m = "ECO";
		}
		Debug.Log($"[BURITIRO] 10x10 rodada {round} time{t}: mediana ${mediana}, fuzil+colete ${precisa}, derrotas seguidas {lossStreak[t]} -> {m}");
		return m;
	}

	private static int PrecoMinimo(string cls)
	{
		int min = 0;
		foreach (string id in GameData.Weapons.Keys)
		{
			if (GameData.Weapons[id].cls == cls && (bool)FBRegistry.I.Weapon(id))
			{
				int p = FBEconomy.PriceOf(id);
				if (p > 0 && (min == 0 || p < min))
				{
					min = p;
				}
			}
		}
		return min;
	}

	private static void Comprar(FBCharacter b, string modo, bool garantido)
	{
		if (!FBEconomy.money.ContainsKey(b))
		{
			return;
		}
		FBWeaponUser wu = b.GetComponent<FBWeaponUser>();
		if (modo == "ECO")
		{
			// Fica com a arma que sobreviveu da rodada anterior, ou pistola.
			if (wu.Cur == null && GameData.Weapons.ContainsKey(FBEconomy.BOT_PISTOL))
			{
				FBEconomy.ArmBot(b, FBEconomy.BOT_PISTOL);
			}
			// v7b: colete na eco se, mesmo perdendo de novo (+1900), ainda paga
			// fuzil + colete na rodada seguinte. Pistola com colete aguenta mais
			// contra 10 fuzis e quase nunca atrasa a compra.
			int fz = PrecoMinimo("fuzil");
			int precisa = ((fz > 0) ? fz : 2200) + 650;
			if (b.armor < 60f && FBEconomy.money[b] - 650 + PAGA_DERROTA + PAGA_SEQUENCIA >= precisa)
			{
				FBEconomy.money[b] -= 650;
				b.armor = 100f;
			}
			return;
		}
		int atual = Nivel(wu.Cur);
		int din = FBEconomy.money[b];
		int colete = (b.armor < 60f) ? 650 : 0;
		string arma = null;
		if (atual < 2)
		{
			arma = Escolher("fuzil", din - colete);
		}
		if (arma == null && atual < 1)
		{
			arma = Escolher("submetralhadora", din - colete);
		}
		if (arma != null)
		{
			FBEconomy.money[b] -= FBEconomy.PriceOf(arma);
			FBEconomy.ArmBot(b, arma);
		}
		if (wu.Cur == null && GameData.Weapons.ContainsKey(FBEconomy.BOT_PISTOL))
		{
			FBEconomy.ArmBot(b, FBEconomy.BOT_PISTOL);
		}
		if (b.armor < 60f && FBEconomy.money[b] >= 650)
		{
			FBEconomy.money[b] -= 650;
			b.armor = 100f;
		}
		// v12: com derrota na sequência a próxima rodada já paga +1900 ou mais, então o
		// capacete sai com qualquer sobra de $450 (antes exigia sobrar $1950).
		if (!b.helmet && FBEconomy.money[b] >= (garantido ? 450 : 450 + 1500))
		{
			FBEconomy.money[b] -= 450;
			b.helmet = true;
		}
	}

	private static string Escolher(string cls, int ate)
	{
		List<string> ok = GameData.Weapons.Keys.Where((string id) => GameData.Weapons[id].cls == cls && (bool)FBRegistry.I.Weapon(id) && FBEconomy.PriceOf(id) <= ate).ToList();
		if (ok.Count == 0)
		{
			return null;
		}
		return FBUtil.Pick(ok);
	}

	// FBMatch só monta 8 lugares por time e repete os mesmos depois disso.
	// Aqui o time inteiro é reposicionado numa grade de 2 fileiras x 5 atrás
	// do primeiro lugar da base, olhando para o inimigo. O jogador fica onde
	// FBMatch.ResetRound o colocou (lugar 0).
	private void Espalhar()
	{
		for (int t = 0; t < 2; t++)
		{
			FBMatch.Line line = G.teamLine[t];
			if (line == null || line.spots.Count == 0)
			{
				continue;
			}
			Vector3 b = line.spots[0];
			baseC[t] = b;
			List<Vector3> grade = Grade(b, line.yaw);
			float olhar = Olhar(b, line.yaw);
			if (t == FBSession.team && (bool)FBMatch.Player && !FBMatch.Player.isRemote)
			{
				FBMatch.Teleport(FBMatch.Player, b, olhar);
				FBPlayer.I?.OnRespawn(olhar);
			}
			int i = 0;
			// fisica 31/10: lugares ja ocupados. Se a grade tiver menos casas que o time, quem sobra
			// nao pode cair em cima do jogador (que esta em b) nem de outro bot.
			List<Vector3> usados = new List<Vector3>();
			if (t == FBSession.team)
			{
				usados.Add(b);
			}
			foreach (FBCharacter a in FBMatch.Actors().Where((FBCharacter x) => x.team == t && !x.isRemote && !x.isPlayer).ToList())
			{
				if (t == FBSession.team && i == 0)
				{
					i++;
				}
				Vector3 p = (i < grade.Count) ? grade[i] : Sobra(b, usados, line.spots[i % line.spots.Count]);
				usados.Add(p);
				FBMatch.Teleport(a, p, olhar);
				i++;
			}
		}
	}

	// Direção de nascer: a mais livre entre 32 testadas, puxando para o lado do
	// inimigo. Antes era a linha reta até a base inimiga, que às vezes dava numa parede.
	private static float Olhar(Vector3 b, float yawInimigo)
	{
		Vector3 o = b + Vector3.up * 1.5f;
		float melhor = yawInimigo * Mathf.Deg2Rad;
		float nota = -1e9f;
		for (int i = 0; i < 32; i++)
		{
			float a = (float)i / 32f * Mathf.PI * 2f;
			Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
			RaycastHit hit;
			float livre = FBWorld.RayHit(o, d, 30f, out hit) ? hit.distance : 30f;
			float n = Mathf.Min(livre, 20f) + Mathf.Cos(Mathf.DeltaAngle(a * Mathf.Rad2Deg, yawInimigo) * Mathf.Deg2Rad) * 8f;
			if (n > nota)
			{
				nota = n;
				melhor = a;
			}
		}
		return melhor * Mathf.Rad2Deg;
	}

	// fisica 31/10: lugar livre no NavMesh perto da base, a vista dela, no mesmo nivel e a mais de
	// 1,2 m de todo mundo do time. Sem achar, fica o lugar antigo (reserva).
	private static Vector3 Sobra(Vector3 b, List<Vector3> usados, Vector3 reserva)
	{
		for (int k = 0; k < 24; k++)
		{
			Vector3? q = FBNav.RandomPoint(b, 6f);
			if (!q.HasValue)
			{
				continue;
			}
			Vector3 v = q.Value;
			float? gy = FBWorld.GroundY(v.x, v.y + 1.5f, v.z, 0.3f, 3f);
			if (gy.HasValue)
			{
				v.y = gy.Value;
			}
			if (Mathf.Abs(v.y - b.y) < 1.8f && FBWorld.ClearLine(b, v) && usados.All((Vector3 u) => (u - v).Flat().sqrMagnitude > 1.44f))
			{
				return v;
			}
		}
		return reserva;
	}

	private static List<Vector3> Grade(Vector3 b, float yaw)
	{
		Quaternion q = Quaternion.Euler(0f, yaw, 0f);
		Vector3 right = q * Vector3.right;
		Vector3 fwd = q * Vector3.forward;
		Vector3 o = b + Vector3.up * 1.2f;
		List<Vector3> list = new List<Vector3>();
		float[] xs = new float[5] { 0f, 2f, -2f, 4f, -4f };
		float[] ys = new float[3] { 0f, -2.2f, -4.4f };
		foreach (float y in ys)
		{
			foreach (float x in xs)
			{
				Vector3 d = right * x + fwd * y;
				float m = d.magnitude;
				if (m > 0.01f && FBWorld.RayHit(o, d / m, m + 0.45f))
				{
					continue;
				}
				float? gy = FBWorld.GroundY(b.x + d.x, b.y + 1.5f, b.z + d.z, 0.3f, 3f);
				if (gy.HasValue && Mathf.Abs(gy.Value - b.y) <= 1.8f)
				{
					list.Add(new Vector3(b.x + d.x, gy.Value, b.z + d.z));
				}
			}
		}
		return list;
	}

	private void EndRound(int winner, string why)
	{
		if (phase != "live")
		{
			return;
		}
		phase = "post";
		pt = POST_T;
		results.Add(winner);
		wins[winner]++;
		Pagar(winner);
		int team = FBSession.team;
		bool flag = winner == team;
		FBHUD.I?.Toast(flag ? "RODADA VENCIDA" : "RODADA PERDIDA", $"{wins[team]} × {wins[1 - team]}", why, flag ? GREEN : RED);
		FBHUD.I?.MatchToast(flag ? "VITÓRIA NA RODADA" : "DERROTA NA RODADA", flag ? GREEN : RED, 4f);
		FBAudio.Synth(flag ? "win" : "lose");
	}

	// Vencedor: +2500. Perdedor: +1400 e mais 500 por derrota seguida (até 3400),
	// para o time que está perdendo conseguir se armar de novo.
	private void Pagar(int winner)
	{
		for (int i = 0; i < 2; i++)
		{
			lossStreak[i] = (i == winner) ? 0 : Mathf.Min(SEQ_MAX, lossStreak[i] + 1);
		}
		foreach (FBCharacter a in FBMatch.Actors().ToList())
		{
			if (a.team > 1)
			{
				continue;
			}
			if (a.team == winner)
			{
				FBEconomy.Give(a, PAGA_VITORIA, "VITÓRIA");
			}
			else
			{
				FBEconomy.Give(a, PAGA_DERROTA + PAGA_SEQUENCIA * (lossStreak[a.team] - 1), "RODADA");
			}
		}
	}

	private int mortesRodada;
	private float diagT;

	public override void OnKill(FBCharacter v, FBCharacter k, bool head)
	{
		mortesRodada++;
		if (phase != "live")
		{
			return;
		}
		for (int i = 0; i < 2; i++)
		{
			if (!Alive(i).Any())
			{
				EndRound(1 - i, "TIME ELIMINADO");
				return;
			}
		}
	}

	// Para onde o bot anda quando não está vendo ninguém.
	public override Vector3? Goal(FBCharacter b)
	{
		if (phase != "live" || b.team > 1)
		{
			return null;
		}
		int en = 1 - b.team;
		List<FBCharacter> vivos = Alive(en).ToList();
		if (vivos.Count == 0)
		{
			return null;
		}
		// Fim de rodada: vai atrás do inimigo vivo mais perto.
		// v10: se ninguém morreu até 50 s, começa a caçada mais cedo (exe F, R2: 135 s sem
		// nenhum bot ver inimigo).
		// v14 (02/11, pedido da thread do 10x10): na rodada de pistola todo mundo caça o inimigo mais perto a partir de 35 s
		// (antes 11 de 11 partidas tiveram a rodada 1 decidida no tempo, com contato só aos ~24-34 s).
		if (liveT > HUNT_T || vivos.Count <= 3 || (liveT > 50f && mortesRodada == 0) || (round <= 1 && liveT > 35f))
		{
			FBCharacter alvo = vivos.OrderBy((FBCharacter a) => (a.Pos - b.Pos).sqrMagnitude).First();
			return alvo.Pos + new Vector3(FBUtil.Rnd(-8f, 8f), 0f, FBUtil.Rnd(-8f, 8f));
		}
		// Tem informação recente do time por perto: deixa o FBBot usar ela.
		Vector3? intel = FBBotBrain.FreshIntel(b.team);
		if (intel.HasValue && (intel.Value - b.Pos).magnitude < 80f)
		{
			return null;
		}
		Vector3 mine = baseC[b.team];
		Vector3 theirs = baseC[en];
		float avanco = Mathf.Lerp(0.3f, meioRodada, Mathf.Clamp01(liveT / 45f));
		int sorteio = (b.GetHashCode() & 0x7fffffff) + round * 7;
		// Rotas do mapa só valem quando as pontas batem com as bases desta rodada.
		List<List<Vector3>> boas = rotas.Where((List<Vector3> r) => Liga(r, mine, theirs)).ToList();
		if (boas.Count > 0)
		{
			// Anda pela rota em trechos de até 35 m, para seguir as curvas dela em vez de
			// cortar caminho pelo NavMesh direto até o ponto final.
			List<Vector3> rota = boas[sorteio % boas.Count];
			if ((rota[0] - mine).Flat().sqrMagnitude > (rota[rota.Count - 1] - mine).Flat().sqrMagnitude)
			{
				rota = new List<Vector3>(rota);
				rota.Reverse();
			}
			float total = Comprimento(rota);
			float fim = total * avanco;
			float ja = Andado(rota, b.Pos);
			float frac = (total > 1f) ? (Mathf.Min(fim, ja + 35f) / total) : avanco;
			if (ja > fim + 10f)
			{
				frac = avanco;
			}
			Vector3 q = NaRota(rota, mine, frac) + new Vector3(FBUtil.Rnd(-4f, 4f), 0f, FBUtil.Rnd(-4f, 4f));
			return FBNav.RandomPoint(q, 6f) ?? FBNav.RandomPoint(q, 25f) ?? mine;
		}
		Vector3 eixo = (theirs - mine).Flat();
		float dist = eixo.magnitude;
		if (dist < 1f)
		{
			return null;
		}
		Vector3 lado = Vector3.Cross(Vector3.up, eixo / dist);
		int faixa = sorteio % 3 - 1;
		// Os dois times andam até o meio do mapa (não até a base do outro), pra
		// briga acontecer em terreno neutro; quem ataca a base inimiga em campo
		// aberto perdia sempre. A caçada do fim da rodada continua igual.
		Vector3 p = Vector3.Lerp(mine, theirs, avanco) + lado * faixa * Mathf.Min(largura, dist * 0.3f);
		// 1 em cada 4 bots faz volta larga pelo lado, para a rodada usar mais do mapa.
		if (sorteio % 4 == 0)
		{
			p += lado * ((faixa == 0) ? 1f : faixa) * largura * 0.8f;
			return FBNav.RandomPoint(p, 18f) ?? FBNav.RandomPoint(p, 35f) ?? mine;
		}
		return FBNav.RandomPoint(p, 8f) ?? FBNav.RandomPoint(p, 25f) ?? mine;
	}

	private static bool Liga(List<Vector3> r, Vector3 x, Vector3 y)
	{
		Vector3 i = r[0];
		Vector3 f = r[r.Count - 1];
		return ((i - x).Flat().magnitude < 60f && (f - y).Flat().magnitude < 60f) || ((i - y).Flat().magnitude < 60f && (f - x).Flat().magnitude < 60f);
	}

	public override void Update(float dt)
	{
		if (phase == "buy")
		{
			pt -= dt;
			FBEconomy.ShopTime(pt);
			FBHUD.I?.SetBox((pt > 3.5f) ? $"FASE DE COMPRA · {Mathf.CeilToInt(pt)}s · B = LOJA" : $"COMEÇA EM {Mathf.CeilToInt(pt)}", on: true);
			int num = Mathf.CeilToInt(pt);
			if (num != lastSec && pt > 0f)
			{
				lastSec = num;
				if (num <= 10 && num > 5)
				{
					FBAudio.Synth("tick");
				}
				if (num == 5)
				{
					FBAudio.Synth("count5");
				}
				if (num <= 3 && num >= 1)
				{
					FBAudio.Synth("beep", num);
					FBHUD.I?.Countdown(num.ToString());
				}
				if (num == 3)
				{
					FBAudio.Synth("riser", 3);
				}
			}
			if (pt <= 0f)
			{
				phase = "live";
				FBEconomy.BuyPhase(on: false);
				G.freeze = false;
				FBHUD.I?.SetBox("", on: false);
				FBHUD.I?.MatchToast("VALENDO!", GREEN, 1.5f);
				FBAudio.Synth("go");
				FBHUD.I?.Countdown("VALENDO!");
				G.OnLive();
			}
		}
		else if (phase == "post")
		{
			pt -= dt;
			if (pt <= 0f)
			{
				int team = FBSession.team;
				if (wins[0] >= WIN || wins[1] >= WIN)
				{
					G.EndMatch(wins[team] >= WIN, "10X10", wins[0], wins[1]);
					return;
				}
				StartRound();
			}
		}
		else if (phase == "live")
		{
			rt -= dt;
			liveT += dt;
			if (liveT >= 20f && diagT < 20f || liveT >= 60f && diagT < 60f || liveT >= 100f && diagT < 100f)
			{
				diagT = liveT;
				Diagnostico();
			}
			if (rt <= 0f)
			{
				// Tempo esgotado: ganha quem tem mais gente viva; empate vai para a vida somada.
				int n0 = Alive(0).Count();
				int n1 = Alive(1).Count();
				int winner = (n0 != n1) ? ((n0 > n1) ? 0 : 1) : ((H(0) >= H(1)) ? 0 : 1);
				EndRound(winner, "TEMPO ESGOTADO");
			}
		}
		float H(int t)
		{
			return Alive(t).Sum((FBCharacter a) => Mathf.Max(0f, a.hp));
		}
	}

	// v10: a cada 20/60/100 s, onde 3 bots de cada time estão, para onde o modo manda
	// e a distância até a base inimiga e ao inimigo mais perto.
	private void Diagnostico()
	{
		for (int t = 0; t < 2; t++)
		{
			List<FBCharacter> meus = Alive(t).Where((FBCharacter a) => !a.isPlayer).Take(3).ToList();
			List<FBCharacter> deles = Alive(1 - t).ToList();
			foreach (FBCharacter b in meus)
			{
				Vector3? g = Goal(b);
				float dIni = deles.Count > 0 ? deles.Min((FBCharacter e) => (e.Pos - b.Pos).Flat().magnitude) : -1f;
				FBBot bot = b.GetComponent<FBBot>();
				Debug.Log($"[BURITIRO] 10x10 diag rodada {round} {liveT:0}s time{t} {b.name}: em ({b.Pos.x:0}, {b.Pos.z:0}) · objetivo {(g.HasValue ? $"({g.Value.x:0}, {g.Value.z:0})" : "nenhum")} · base inimiga a {(baseC[1 - t] - b.Pos).Flat().magnitude:0} m · inimigo mais perto a {dIni:0} m · bot {(bot ? "ok" : "sem FBBot")}");
			}
		}
	}

	public override void Hud()
	{
		int ini = Mathf.Max(0, results.Count - 7);
		Color[] pips = (from i in Enumerable.Range(0, 7)
			select (ini + i < results.Count) ? ((results[ini + i] != 0) ? RED : BLUE) : DIM).ToArray();
		FBHUD.I?.SetMatch($"10X10 · RODADA {round} · PRIMEIRO A {WIN}", wins[0].ToString(), (phase == "buy") ? $"{Mathf.CeilToInt(pt)}" : Mmss(rt), wins[1].ToString(), A(0), A(1), pips);
		float A(int t)
		{
			return (float)Alive(t).Count() / (float)TIME;
		}
	}

	public override void Dispose()
	{
		FBEconomy.BuyPhase(on: false);
		if ((bool)arenaGo)
		{
			Object.Destroy(arenaGo);
		}
	}
}
}
