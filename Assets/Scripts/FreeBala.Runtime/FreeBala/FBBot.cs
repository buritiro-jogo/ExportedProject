using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
public class FBBot : MonoBehaviour
{
	[NonSerialized]
	public FBCharacter ch;

	private FBWeaponUser wu;

	private CharacterController cc;

	public string ai = "patrol";

	public string role = "tactical";

	public float laneOffset;

	public float gunMul = 1f;

	[NonSerialized]
	public FBCharacter target;

	[NonSerialized]
	public Vector3? lastSeen;

	[NonSerialized]
	public Vector3? goalP;

	[NonSerialized]
	public Vector3? coverPos;

	[NonSerialized]
	public float goalT;

	[NonSerialized]
	public float pauseT;

	private Vector3 home;

	private Vector3 vel;

	// fisica 31/10: velocidade que as pernas e o LocoRate enxergam: a pedida, limitada ao deslocamento real do CharacterController
	private Vector3 legVel;

	// fisica 31/10: parte da velocidade pedida que o CC nao andou (pedida - real), filtrada (~80 ms). Sem o filtro, nos quadros de
	// colisao parcial (bots se rocando na saida da base, quina, degrau) o ritmo das pernas/LocoRate pulava de um quadro para o outro.
	// Filtra so o deficit (nao a velocidade toda): sem colisao o deficit e 0 e legVel = vel, sem atraso na arrancada/parada.
	private Vector3 legDef;

	// fisica 31/10: velocidade pedida (sem o y): o FBPropBody empurra objetos com ela, porque ch.velocity agora e o deslocamento real (~0 quando o objeto trava o bot)
	public Vector3 WishVel => new Vector3(vel.x, 0f, vel.z);

	private float thinkT;

	private float shotT;

	private float seeT = 9f;

	private float strafeT;

	private float strafe;

	private float coverT;

	private float peekT;

	private float peekT2;

	private float peekDur;

	private float fleeT;

	private float calmT;

	private float drawT;

	private float jumpCd;

	private float stuckWin;

	private float medT;

	private float airT;

	private float aimYawRad;

	private float pitch;

	private float healEndT;

	private int burst;

	private int stuckN;

	private float botSlide;

	private float botStep = -1f;

	private FBMotor.Ground botGround;

	private bool sees;

	private bool peeking;

	private bool holster;

	private bool onGround = true;

	private bool rescuing;

	private bool safeOk;

	private Vector3 lastPos;

	private FBCharacter reviveTgt;

	private float hitT = -99f;

	private float safeT;

	private readonly Dictionary<FBCharacter, float> aware = new Dictionary<FBCharacter, float>();

	private float lookT;

	private float acquireT;

	private float lastThinkDt = 0.17f;

	private float searchT;

	private float aggr = 0.6f;

	private float prefDist = 22f;

	private int seenMask = 2;

	private FBCharacter acquired;

	private List<Vector3> path;

	private Vector3? pathGoal;

	private float navT;

	private Vector3? escOff;

	private Vector3 strafeDir;

	private float strafeAmp;

	private float strafeW;

	private float dT;

	// presos 01/11: aplica_presos.py v3. Vigia de progresso e ilha do NavMesh (ver Vigia). Campos próprios: o stuckN de
	// antes continua igual para obstáculo curto.
	private float vigUlt = -99f;

	private float vigT;

	private float vigDur;

	private float vigQuer;

	private bool vigTemDest;

	private Vector3 vigUltDest;

	private Vector3 vigPos;

	private Vector3 vigP0;

	private bool vigAcomp;

	private Vector3 vigDest;

	private float vigBase;

	private float vigBloq;

	private float vigTotal;

	private int vigNivel;

	private float vigEstritoAte = -99f;

	// presos v4: saída a pé de bot travado (ver SaidaLivre): ponto livre a 1,4-2,2 m e até quando ele manda no movimento
	private Vector3? saidaP;

	private float saidaAte = -99f;

	private int saidaGiro;

	private Vector3? realocAlvo;

	private Vector3 realocDe;

	private int realocTipo;

	// revisão 2: realocação pedida pelo stuckN 10 (tipo 3) vale até este tempo, também fora da patrulha
	private float realocAte;

	// revisão 2: o destino deste quadro veio do goalP da patrulha (o desvio de lado do stuckN 4 só serve aí)
	private bool destDoGoal;

	// revisão 2: o cache destTem/destK/destSub/destFora foi preenchido pela regra da parede da arena (não pela ilha)
	private bool destLimite;

	private string realocPorque;

	private float ilhaT;

	private int ilhaN;

	private Vector3 ilhaOkP;

	private float ilhaOkT = -99f;

	private bool destTem;

	private Vector3 destK;

	private bool destFora;

	private Vector3 destSub;

	private float destT = -99f;

	// presos 01/11: destino novo ainda sem classificar neste quadro (célula nova sem vaga ou limite de 1/s): não vale a
	// reta dos 12 m, vai pelo Steer
	private bool destPendente;

	// presos 01/11: FindCover vetado até este tempo (uma cobertura fora da ilha principal foi cancelada; ver DestinoSeguro)
	private float coverVetoT = -99f;

	// presos 01/11: quadro em que a vaga de consulta pesada ao NavMesh já foi usada (ver PodePesado)
	private static int pesadoQuadro = -1;

	private static UnityEngine.AI.NavMeshPath caminho;

	// presos 01/11: quantos bots foram realocados (teleporte) e por quê: [0] ilha do NavMesh, [1] vigia de progresso
	// (preso fora de ilha), [2] teleporte antigo do stuckN 10, [3] realocação escondida pedida pelo stuckN 10 sem ponto livre
	// ao lado (revisão v4). O FBTesteExe zera no começo de cada rodada.
	public static int Realocados;

	public static readonly int[] RealocadosPor = new int[4];

	// presos 01/11: prevenção com âncora: [0] destino de patrulha/busca fora da ilha principal desviado para o ponto da ilha
	// principal mais perto, [1] cobertura fora da ilha principal cancelada. O FBTesteExe zera no começo de cada rodada.
	// presos v4: [2] saída a pé no stuckN 4 fora da patrulha (combate, cobertura, fuga), [3] saída a pé no stuckN 10 no
	// lugar do teleporte antigo que só mexia 0,1 a 0,7 m (bot em cima do NavMesh, travado em mureta, banco ou outro bot).
	public static readonly int[] Evitados = new int[4];

	public static int ReviveStarts;

	public static int ReviveUnsafeBlocks;

	public static int ReviveHot;

	public static readonly int[] ReviveWhy = new int[5];

	public float AimYawRad => aimYawRad;

	public float AimPitch => pitch;

	private float YawRad
	{
		get
		{
			return ch.Yaw * ((float)Math.PI / 180f);
		}
		set
		{
			ch.Yaw = value * 57.29578f;
		}
	}

	private Vector3 Fwd => new Vector3(Mathf.Sin(YawRad), 0f, Mathf.Cos(YawRad));

	private float S => ch.ScaleK;

	// Bug do deslize (Ruan, 31/10/2026): bot mirando e andando escorregava os pés.
	// 1) Com "remapSpeedToAI" as pernas do bot animavam com a metade da velocidade real e o
	//    FB_LocoSpeed só acelera a animação até 1,45x, então o pé no chão andava para trás.
	//    Agora as pernas usam a velocidade real ("aiLegsRealSpeed" no fb_data, padrão 1).
	// 2) Bot mirando andava até 4,8 m/s; a animação de correr mirando aguenta ~3,9 m/s.
	//    Agora mirando ele anda no máximo como o jogador mirando (speedAiming) e agachado no
	//    máximo "aiCrouchMaxSpeed" ("aiAimSpeedLikePlayer" no fb_data, padrão 1).
	// PassoTeste/MiraTeste: -1 segue o fb_data; 0/1 forçam (o FBTesteExe mede antes e depois).
	public static int PassoTeste = -1;

	public static int MiraTeste = -1;

	private static bool PassoReal
	{
		get
		{
			if (PassoTeste >= 0)
			{
				return PassoTeste == 1;
			}
			return Json.F(GameData.Section("puppet"), "aiLegsRealSpeed", 1f) > 0.5f;
		}
	}

	private static bool LimiteMira
	{
		get
		{
			if (MiraTeste >= 0)
			{
				return MiraTeste == 1;
			}
			return Json.F(GameData.Section("puppet"), "aiAimSpeedLikePlayer", 1f) > 0.5f;
		}
	}

	private void Awake()
	{
		ch = GetComponent<FBCharacter>();
		wu = GetComponent<FBWeaponUser>();
		cc = GetComponent<CharacterController>();
		ch.isBot = true;
	}

	public void Setup(string aiMode, string r, float lane, Vector3 p)
	{
		ai = aiMode;
		role = r;
		laneOffset = lane;
		home = p;
		lastPos = p;
		thinkT = FBUtil.Rnd(0.05f, 0.35f);
		shotT = FBUtil.Rnd(0.2f, 0.6f);
		jumpCd = FBUtil.Rnd(3.5f, 7f);
		pauseT = FBUtil.Rnd(0f, 1.5f);
		if (role == "striker")
		{
			shotT = FBUtil.Rnd(0.12f, 0.28f);
		}
		else if (role == "baianogmbarril")
		{
			shotT = FBUtil.Rnd(0.2f, 0.4f);
		}
		else if (role == "halemph")
		{
			shotT = FBUtil.Rnd(0.3f, 0.55f);
			burst = 3;
		}
		else if (role == "halemph2")
		{
			shotT = FBUtil.Rnd(0.25f, 0.45f);
		}
		aimYawRad = ch.Yaw * ((float)Math.PI / 180f);
		(float, float) tuple;
		switch (role)
		{
		case "rusher":
			tuple = (1f, 10f);
			break;
		case "striker":
			tuple = (0.9f, 14f);
			break;
		case "flanker_left":
		case "flanker_right":
			tuple = (0.75f, 18f);
			break;
		case "baianogmbarril":
			tuple = (0.8f, 15f);
			break;
		case "halemph":
			tuple = (0.4f, 34f);
			break;
		case "halemph2":
			tuple = (0.5f, 28f);
			break;
		default:
			tuple = (0.6f, 22f);
			break;
		}
		(aggr, prefDist) = tuple;
		aware.Clear();
		acquired = null;
	}

	public static string Diag()
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		float num = 0f;
		float num2 = 1E+09f;
		int num3 = 0;
		int num4 = 0;
		foreach (FBCharacter bot in FBMatch.Bots)
		{
			if (!bot || !bot.alive)
			{
				continue;
			}
			FBBot component = bot.GetComponent<FBBot>();
			if (!component)
			{
				continue;
			}
			num4++;
			dictionary[component.ai] = ((!dictionary.TryGetValue(component.ai, out var value)) ? 1 : (value + 1));
			foreach (KeyValuePair<FBCharacter, float> item in component.aware)
			{
				num = Mathf.Max(num, item.Value);
			}
			if (component.sees)
			{
				num3++;
			}
			foreach (FBCharacter item2 in FBMatch.Actors())
			{
				if ((bool)item2 && item2.alive && item2 != bot && FBMatch.Hostile(item2, bot))
				{
					num2 = Mathf.Min(num2, (item2.Pos - bot.Pos).magnitude);
				}
			}
		}
		return string.Format("{0} bots · {1} · vendo {2} · consciência máx {3:0.00} · inimigo mais perto {4:0} m", num4, string.Join(", ", dictionary.Select((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value)), num3, num, (num2 < 100000000f) ? num2 : 0f);
	}

	public void SetDummy(Vector3 dir, float amp, float w)
	{
		ai = "dummy";
		strafeDir = dir;
		strafeAmp = amp;
		strafeW = w;
	}

	public void ResetRound()
	{
		ai = "squad";
		target = null;
		lastSeen = null;
		goalP = null;
		holster = false;
		calmT = 0f;
		ch.SetWeaponVisible(v: true);
		ch.StowCurrent(on: false);
		vel = Vector3.zero;
		// fisica 31/10: rodada nova comeca em pe. Ninguem zerava o agachado (EndKnock so roda para derrubado/getUp/sys e Revive nao mexe):
		// bot que terminou ou morreu agachado na cobertura comecava a compra e a rodada seguinte agachado, preso em aiCrouchMaxSpeed
		ch.crouch = false;
		ch.sprint = false;
		legVel = Vector3.zero;
		legDef = Vector3.zero;
		path = null;
		aware.Clear();
		acquired = null;
	}

	public void OnKnocked()
	{
		ai = "knocked";
		target = null;
		coverPos = null;
		ch.combat = false;
	}

	public void OnRevived()
	{
		ch.combat = wu.Cur != null;
		ai = "patrol";
		goalP = null;
		thinkT = 0f;
	}

	public void Engage(FBCharacter src)
	{
		if (!(ai == "dummy"))
		{
			ai = "combat";
			target = src;
			lastSeen = src.Pos;
		}
	}

	public void OnHit(FBCharacter src)
	{
		if (!ch.alive || ch.team > 1 || ai == "dummy" || ai == "victim" || !src)
		{
			return;
		}
		float magnitude = (src.Pos - ch.Pos).magnitude;
		Vector3 vector = UnityEngine.Random.insideUnitSphere.Flat() * magnitude * 0.08f;
		target = src;
		lastSeen = src.Pos + vector;
		seeT = 0f;
		FBBotBrain.TeamIntel(ch.team, src.Pos + vector);
		medT = 0f;
		hitT = Time.time;
		AbortRevive();
		aware.TryGetValue(src, out var value);
		aware[src] = Mathf.Max(value, 0.65f);
		if (ai != "cover" && ai != "flee")
		{
			if (ai != "combat")
			{
				shotT = Mathf.Min(shotT, FBUtil.Rnd(0.3f, 0.6f) * FBCombat.DiffReact);
			}
			ai = "combat";
		}
		Turn(Mathf.Atan2(src.Pos.x - ch.Pos.x, src.Pos.z - ch.Pos.z), 0.7f);
		if (FBMatch.FFA)
		{
			return;
		}
		foreach (FBBot item in (from m in FBMatch.Bots
			where (bool)m && m != ch && m.alive && !m.knocked && m.team == ch.team && (m.Pos - ch.Pos).magnitude < 35f
			select m.GetComponent<FBBot>() into mb
			where (bool)mb && mb.ai != "combat" && mb.ai != "cover" && mb.ai != "guard" && !mb.reviveTgt
			orderby (mb.ch.Pos - ch.Pos).sqrMagnitude
			select mb).Take(2))
		{
			item.goalP = item.FlankPoint(src.Pos);
			item.goalT = 12f;
			item.pauseT = 0f;
			item.lastSeen = src.Pos;
		}
	}

	private void Turn(float yawRad, float k)
	{
		float num = ch.Yaw * ((float)Math.PI / 180f);
		num += FBUtil.WrapA(yawRad - num) * k;
		ch.Yaw = num * 57.29578f;
	}

	private Vector3? FindCover(Vector3 threat)
	{
		Vector3? result = null;
		float num = -1f;
		float[] array = new float[3] { 4f, 6f, 8f };
		foreach (float num2 in array)
		{
			for (int j = 0; j < 360; j += 45)
			{
				float f = (float)j * ((float)Math.PI / 180f);
				float x = ch.Pos.x + Mathf.Sin(f) * num2;
				float z = ch.Pos.z + Mathf.Cos(f) * num2;
				float? num3 = FBWorld.GroundY(x, ch.Pos.y + 1f, z, 0.3f, 3f);
				if (!num3.HasValue)
				{
					continue;
				}
				float valueOrDefault = num3.GetValueOrDefault();
				// presos v4 (revisão): atrás da parede invisível da arena não é cobertura (a parede conta como sólida)
				if (!FBNav.DentroLimite(new Vector3(x, valueOrDefault, z), 1.5f))
				{
					continue;
				}
				Vector3 vector = new Vector3(x, valueOrDefault + 1f, z);
				Vector3 normalized = (threat - vector).normalized;
				if (FBWorld.RayHit(vector, normalized, (threat - vector).magnitude - 0.3f, 0.15f))
				{
					float num4 = (10f - (vector - ch.Pos).magnitude) * 0.4f + (vector - threat).magnitude * 0.15f;
					if (num4 > num)
					{
						num = num4;
						result = new Vector3(x, valueOrDefault, z);
					}
				}
			}
		}
		return result;
	}

	public Vector3 FlankPoint(Vector3 p)
	{
		if (Mathf.Abs(laneOffset) < 2f)
		{
			return p;
		}
		Vector3 vector = (p - ch.Pos).Flat();
		float magnitude = vector.magnitude;
		if (magnitude < 6f)
		{
			return p;
		}
		vector /= magnitude;
		if (!FBNav.Find(p + new Vector3(vector.z, 0f, 0f - vector.x) * (0f - laneOffset) * Mathf.Min(1f, magnitude / 40f), 8f, out var at))
		{
			return p;
		}
		// presos v4 (revisão): o NavMesh a até 8 m pode ser o de fora da parede da arena
		if (!FBNav.DentroLimite(at, 1.5f))
		{
			return p;
		}
		return at;
	}

	private Vector3 PickPatrolGoal()
	{
		Vector3? vector = FBMatch.I?.mode?.Goal(ch);
		if (vector.HasValue)
		{
			Vector3 valueOrDefault = vector.GetValueOrDefault();
			return FlankPoint(valueOrDefault);
		}
		Vector3? vector2 = FBBotBrain.FreshIntel(ch.team);
		if (vector2.HasValue)
		{
			Vector3 valueOrDefault2 = vector2.GetValueOrDefault();
			if ((valueOrDefault2 - ch.Pos).magnitude < 120f)
			{
				return FlankPoint(valueOrDefault2);
			}
		}
		if (ch.team < 2 && UnityEngine.Random.value < 0.55f && (bool)FBMatch.I)
		{
			List<Vector3> list = FBMatch.I.TeamSpawns(1 - ch.team);
			if (list.Count > 0)
			{
				Vector3 vector3 = Vector3.Lerp(list.Aggregate(Vector3.zero, (Vector3 a, Vector3 b) => a + b) / list.Count, ch.Pos, FBUtil.Rnd(0.15f, 0.55f));
				vector2 = FBNav.RandomPoint(vector3, FBUtil.Rnd(14f, 30f));
				if (vector2.HasValue)
				{
					Vector3 valueOrDefault3 = vector2.GetValueOrDefault();
					return FlankPoint(valueOrDefault3);
				}
				return FlankPoint(vector3);
			}
		}
		vector2 = FBNav.RandomPoint(ch.Pos, FBUtil.Rnd(30f, 85f));
		if (vector2.HasValue)
		{
			return vector2.GetValueOrDefault();
		}
		vector2 = FBNav.RandomPoint();
		if (vector2.HasValue)
		{
			return vector2.GetValueOrDefault();
		}
		List<Vector3> list2 = FBMatch.I?.Map?.waypoints;
		if (list2 != null && list2.Count > 0)
		{
			List<Vector3> list3 = list2.Where((Vector3 w) => (w - ch.Pos).Flat().magnitude > 18f).ToList();
			return ((list3.Count > 0) ? FBUtil.Pick(list3) : FBUtil.Pick(list2)) + new Vector3(FBUtil.Rnd(-2f, 2f), 0f, FBUtil.Rnd(-2f, 2f));
		}
		return home + new Vector3(FBUtil.Rnd(-25f, 25f), 0f, FBUtil.Rnd(-25f, 25f));
	}

	private Vector3? Steer(Vector3? desired, float dt)
	{
		if (desired.HasValue)
		{
			Vector3 valueOrDefault = desired.GetValueOrDefault();
			navT -= dt;
			if (path == null || navT <= 0f || !pathGoal.HasValue || (pathGoal.Value - valueOrDefault).magnitude > 3f)
			{
				navT = 2f + UnityEngine.Random.value;
				pathGoal = valueOrDefault;
				path = FBNav.Path(ch.Pos, valueOrDefault) ?? new List<Vector3> { valueOrDefault };
			}
			while (path.Count > 1 && (path[0] - ch.Pos).Flat().magnitude < 1.3f)
			{
				path.RemoveAt(0);
			}
			// presos 01/11: no modo estrito (bot que travou há pouco, ver Estrito) não corta canto pelo ClearLine
			if (!Estrito && path.Count > 1 && FBWorld.ClearLine(ch.Pos, path[1]))
			{
				path.RemoveAt(0);
			}
			return (path.Count > 0) ? path[0] : valueOrDefault;
		}
		return null;
	}

	// presos 01/11: estados em que o bot anda para um destino dele (patrulha/esquadrão/caça e busca). Combate, cobertura
	// (com o espiar), fuga, guarda e escolta têm regra e tempo próprios e não contam como preso.
	private bool Vigiado
	{
		get
		{
			switch (ai)
			{
			case "combat":
			case "cover":
			case "flee":
			case "guard":
			case "escort":
			case "dummy":
			case "victim":
			case "knocked":
				return false;
			default:
				return true;
			}
		}
	}

	// presos 01/11: modo estrito, por 8 s depois de cada segundo travado: segue os cantos do caminho do NavMesh, sem
	// cortar canto pelo ClearLine e sem a reta dos 12 m. O ClearLine são 2 raios finos (0,6 e 1,5 m do pé) que passam
	// entre os postes e por baixo do corrimão do deck da R6 e pela face de trás do alambrado; a cápsula de 1,8 m não passa.
	private bool Estrito
	{
		get
		{
			if (Time.time < vigEstritoAte)
			{
				return Vigiado;
			}
			return false;
		}
	}

	private static UnityEngine.AI.NavMeshPath Caminho => caminho ?? (caminho = new UnityEngine.AI.NavMeshPath());

	// presos 01/11: no máximo 1 consulta pesada ao NavMesh por quadro somando todos os bots: a busca da âncora para célula
	// ainda não vista (com o ponto numa ilha ela varre a ilha principal até acabar os nós) e a escalada do vigia (dezenas de
	// SamplePosition e alguns CalculatePath). Quem não pega a vaga espera 1 quadro (ilha, destino) ou 1 tique (escalada).
	private static bool PodePesado()
	{
		int frameCount = Time.frameCount;
		if (frameCount == pesadoQuadro)
		{
			return false;
		}
		pesadoQuadro = frameCount;
		return true;
	}

	// presos 01/11: vigia de progresso. Roda todo quadro no fim do Update normal (compra/freeze, caído, remédio,
	// levantando, dummy e vítima retornam antes e não chegam aqui; um buraco de mais de 2 s zera tudo).
	// - "Vale" = patrulha/esquadrão/caça ou busca, no chão, sem inimigo à vista e sem reanimar (indo até o caído ou
	//   ajoelhado). Só nesses estados o vigia escala e teleporta.
	// - A cada 1 s: se o bot quis andar (vale, velocidade > 1, com destino) em 70% do tique e andou menos de 0,4 m, soma
	//   1 s "travado"; querendo andar, soma 1 s "sem progresso". Progresso de verdade zera tudo: sair 5 m de onde o
	//   episódio começou ou chegar 3 m mais perto do destino que persegue. O desvio de 2,5 m do stuckN 4 não é progresso,
	//   então não zera mais a escalada (antes o desvio "chegava", vinha destino novo com pausa, a pausa zerava o stuckN e
	//   os passos 6 e 10 nunca rodavam).
	// - 4 s travado (ou 15 s sem progresso): em ilha do NavMesh realoca para a saída; senão destino novo que o NavMesh
	//   alcança. 8 s travado (ou 30 s sem progresso): realoca (ilha -> saída; fora do NavMesh -> NavMesh mais perto;
	//   preso com caminho completo -> 3,5 m à frente no caminho; senão ponto do NavMesh livre perto).
	// - Com âncora (10x10), a cada 2 s: fora da ilha principal duas vezes seguidas (cache de células do FBNav, conferido
	//   de perto) -> realoca para o ponto da ilha principal mais perto.
	// - Realocar = teleporte só se a câmera não vê o bot nem o ponto (Escondido); vendo, tenta a cada 1 s. A pendência de
	//   bot travado é descartada se ele entra em combate, cobertura, fuga ou reanimação; a saída de ilha espera.
	// Custo: sem consulta ao NavMesh por quadro. Por bot, 1 SamplePosition e 1 CalculatePath curto a cada 2 s e por
	// destino novo; a busca cara da âncora só para célula nova, com no máximo 1 por quadro somando todos os bots.
	private void Vigia(float dt, float quer, Vector3? dest)
	{
		float time = Time.time;
		if (time - vigUlt > 2f)
		{
			VigiaZera();
			realocAlvo = null;
			ilhaN = 0;
			vigT = time + UnityEngine.Random.Range(0.6f, 1.4f);
			ilhaT = time + UnityEngine.Random.Range(1f, 3f);
		}
		vigUlt = time;
		if (ch.isPlayer || ch.special || ch.vehiclePose || !cc || !cc.enabled || FBMatch.I.freeze)
		{
			return;
		}
		// presos 01/11: reanimando = indo até o caído (o destino é ele) ou ajoelhado ao lado dele neste quadro (rescuing)
		bool flag = rescuing || (dest.HasValue && (bool)reviveTgt && (reviveTgt.Pos - dest.Value).sqrMagnitude < 0.04f);
		bool flag2 = Vigiado && !flag && onGround && !sees;
		vigDur += dt;
		if (flag2 && quer > 1f && dest.HasValue)
		{
			vigQuer += dt;
			vigTemDest = true;
			vigUltDest = dest.Value;
		}
		if (FBNav.HasAnchor && time >= ilhaT)
		{
			ilhaT = time + 2f;
			Ilha(flag2, time);
		}
		if (!(time < vigT))
		{
			vigT = time + 1f;
			Tique(time, flag2);
		}
	}

	private void VigiaZera()
	{
		Vector3 pos = ch.Pos;
		vigP0 = pos;
		vigPos = pos;
		vigDur = 0f;
		vigQuer = 0f;
		vigTemDest = false;
		vigAcomp = false;
		vigBloq = 0f;
		vigTotal = 0f;
		vigNivel = 0;
	}

	private void Tique(float time, bool vale)
	{
		Vector3 pos = ch.Pos;
		float num = vigDur;
		bool flag = num > 0.1f && vigQuer >= 0.7f * num;
		float magnitude = (pos - vigPos).Flat().magnitude;
		bool flag2 = (pos - vigP0).Flat().magnitude >= 5f;
		bool flag3 = vigTemDest;
		float num2 = 0f;
		if (flag3)
		{
			num2 = (vigUltDest - pos).Flat().magnitude;
			if (!vigAcomp || (vigUltDest - vigDest).Flat().magnitude > 3f)
			{
				vigAcomp = true;
				vigDest = vigUltDest;
				vigBase = num2;
			}
			else if (num2 <= vigBase - 3f)
			{
				flag2 = true;
			}
		}
		vigPos = pos;
		vigDur = 0f;
		vigQuer = 0f;
		vigTemDest = false;
		if (realocAlvo.HasValue && (pos - realocDe).Flat().magnitude > 3f)
		{
			// presos 01/11: saiu de onde o ponto foi calculado; recalcula mais tarde
			realocAlvo = null;
			vigNivel = Mathf.Min(vigNivel, 1);
			vigBloq = Mathf.Min(vigBloq, 4f);
			vigTotal = Mathf.Min(vigTotal, 15f);
		}
		// revisão 2: a realocação do stuckN 10 (tipo 3) expira em 10 s
		if (realocAlvo.HasValue && realocTipo == 3 && time > realocAte)
		{
			realocAlvo = null;
		}
		if (realocAlvo.HasValue && realocTipo != 0 && realocTipo != 3 && !vale)
		{
			// presos 01/11: em combate, cobertura, fuga, vendo inimigo ou reanimando, a realocação de bot travado (tipo 1) é
			// descartada (o travado era da patrulha); a saída de ilha (tipo 0) espera ele voltar a patrulhar ou buscar
			realocAlvo = null;
			vigNivel = Mathf.Min(vigNivel, 1);
			vigBloq = Mathf.Min(vigBloq, 4f);
		}
		if (flag2)
		{
			vigP0 = pos;
			vigBase = num2;
			vigAcomp = vigAcomp && flag3;
			vigBloq = 0f;
			vigTotal = 0f;
			vigNivel = 0;
			if (realocTipo == 1 || realocTipo == 3)
			{
				realocAlvo = null;
			}
			// presos 01/11: andar não tira ninguém de ilha: a saída pendente continua sendo tentada (só se vale)
			if (vale)
			{
				TentaRealocar();
			}
			return;
		}
		if (flag)
		{
			vigTotal += num;
			if (magnitude < 0.4f)
			{
				vigBloq += num;
				vigEstritoAte = time + 8f;
			}
		}
		if (!vale)
		{
			// presos 01/11: escalada e teleporte só em patrulha/esquadrão/busca, no chão, sem inimigo à vista e sem reanimar
			// revisão 2: menos a realocação pedida pelo stuckN 10 (combate travado), tentada enquanto a câmera não vê
			if (realocAlvo.HasValue && realocTipo == 3)
			{
				TentaRealocar();
			}
			return;
		}
		if (realocAlvo.HasValue)
		{
			TentaRealocar();
		}
		else if (vigNivel == 0 && (vigBloq >= 4f || vigTotal >= 15f) && PodePesado())
		{
			vigNivel = 1;
			vigEstritoAte = time + 12f;
			if (IlhaSaida(out var saida))
			{
				Pendente(saida, 0, "ilha do NavMesh (vigia, travado)");
				TentaRealocar();
			}
			else
			{
				NovoDestino();
			}
		}
		else if (vigNivel == 1 && (vigBloq >= 8f || vigTotal >= 30f) && PodePesado())
		{
			vigNivel = 2;
			vigEstritoAte = time + 12f;
			if (AlvoRealocar(out var alvo, out var tipo, out var porque))
			{
				Pendente(alvo, tipo, porque);
				TentaRealocar();
			}
			else
			{
				// presos 01/11: sem ponto para realocar: destino novo e tenta de novo depois de mais 4 s travado
				NovoDestino();
				vigNivel = 1;
				vigBloq = 4f;
				vigTotal = Mathf.Min(vigTotal, 15f);
			}
		}
	}

	// presos 01/11: com âncora, a cada 2 s. Pelo cache de células do FBNav (conferido de perto, ver Principal) o bot está
	// fora da ilha principal duas vezes seguidas -> realoca para o ponto da ilha principal mais perto (até 25 m). Célula
	// nova, ou célula que pega a cerca com o bot do outro lado, precisa da busca da âncora: pega a vaga do quadro
	// (PodePesado) ou tenta no quadro seguinte.
	private void Ilha(bool vale, float time)
	{
		if (!vale || realocAlvo.HasValue || (time < ilhaOkT && (ch.Pos - ilhaOkP).Flat().magnitude < 3f))
		{
			return;
		}
		int num = Principal(ch.Pos, 2f, out var at, fresco: false, soCache: true);
		if (num == -2)
		{
			if (!PodePesado())
			{
				ilhaT = time + 0.05f;
				return;
			}
			num = Principal(ch.Pos, 2f, out at, fresco: false);
		}
		if (num != 0)
		{
			ilhaN = 0;
			return;
		}
		if (++ilhaN < 2)
		{
			return;
		}
		ilhaN = 0;
		if ((at - ch.Pos).magnitude <= 25f)
		{
			Pendente(at, 0, "ilha do NavMesh");
			TentaRealocar();
			return;
		}
		// presos 01/11: saída longe demais: não testa de novo aqui por 10 s
		ilhaOkP = ch.Pos;
		ilhaOkT = time + 10f;
	}

	// presos 01/11: com âncora, destino guardado (goalP da patrulha, lastSeen da busca/combate sem ver, coverPos) fora da
	// ilha principal vira o ponto da ilha principal mais perto dele. Nunca devolve destino novo cru: célula já vista pelo
	// FBNav.Inside classifica na hora (1 SamplePosition e 1 CalculatePath curto); célula nova precisa da busca da âncora,
	// no máximo 1 por segundo por bot (cobertura não espera) e 1 por quadro somando os bots. Até lá, perto (8 m) do último
	// destino "fora" usa a mesma saída (o Alert troca o goalP a cada tiro); senão vai pelo Steer, sem a reta dos 12 m.
	// Chegando a 2 m do ponto dá o destino por alcançado (patrulha sorteia outro, busca termina). Cobertura fora da ilha
	// principal é cancelada sempre que aparece (era a armadilha) e o FindCover fica vetado por 4 s.
	private Vector3 DestinoSeguro(Vector3 d)
	{
		if (!FBNav.HasAnchor || ch.isPlayer)
		{
			return d;
		}
		int num = 0;
		if (ai == "cover")
		{
			if (coverPos.HasValue && (coverPos.Value - d).sqrMagnitude < 0.01f)
			{
				num = 3;
			}
		}
		else if (ai == "search" || (ai == "combat" && !sees))
		{
			if (lastSeen.HasValue && (lastSeen.Value - d).sqrMagnitude < 0.01f)
			{
				num = 2;
			}
		}
		else if (Vigiado && goalP.HasValue && (goalP.Value - d).sqrMagnitude < 0.01f)
		{
			num = 1;
		}
		if (num == 0)
		{
			return d;
		}
		// presos v4 (revisão): destino fora do quadrado da arena é "fora" já (o FBNav.Inside recusa sem guardar célula, então a
		// Celula dava 2 e o destino passava cru; o bot parava a 1,5 m da parede sem nunca "chegar"): o ponto de troca é o
		// NavMesh perto do ponto puxado para dentro, e vale a regra de baixo (cobertura cancelada, patrulha/busca chegam a 2 m)
		if (FBNav.TemLimite && !FBNav.DentroLimite(d, 1f))
		{
			if (!destTem || !destLimite || (d - destK).sqrMagnitude > 1f)
			{
				Vector3 dentro = FBNav.PraDentro(d, 1.5f);
				if (UnityEngine.AI.NavMesh.SamplePosition(dentro, out var hitDentro, 3f, -1) && FBNav.DentroLimite(hitDentro.position, 1f))
				{
					dentro = hitDentro.position;
				}
				// revisão 2: o ponto junto da parede pode cair num quintal/quadra fechado (ilha): vai para a saída da ilha
				// principal mais perto (só com a célula já vista ou com a vaga pesada do quadro)
				if (Principal(dentro, 3f, out var atDentro, fresco: false, soCache: !PodePesado()) == 0 && (atDentro - dentro).Flat().magnitude <= 30f && FBNav.DentroLimite(atDentro, 1f))
				{
					dentro = atDentro;
				}
				destTem = true;
				destLimite = true;
				destK = d;
				destSub = dentro;
				destFora = true;
				if (num != 3)
				{
					Evitados[0]++;
				}
			}
		}
		else if (!destTem || destLimite || (d - destK).sqrMagnitude > 1f)
		{
			int num2 = Principal(d, 3f, out var at, fresco: false, soCache: true);
			if (num2 == -2)
			{
				float time = Time.time;
				if ((time < destT && num != 3) || !PodePesado())
				{
					// presos 01/11: cobertura só espera a vaga do quadro (pendente 1 quadro), não reaproveita
					if (num == 3 || !destTem || !destFora || destLimite || (d - destK).Flat().magnitude >= 8f)
					{
						destPendente = true;
						return d;
					}
					// presos 01/11: reaproveita destSub do último destino "fora" (destK fica o mesmo)
					num2 = -3;
				}
				else
				{
					destT = time + UnityEngine.Random.Range(0.8f, 1.25f);
					num2 = Principal(d, 3f, out at, fresco: false);
				}
			}
			if (num2 != -3)
			{
				destTem = true;
				destLimite = false;
				destK = d;
				destSub = at;
				destFora = num2 == 0 && (at - d).Flat().magnitude <= 30f;
				if (destFora && num != 3)
				{
					Evitados[0]++;
				}
			}
		}
		if (!destFora)
		{
			return d;
		}
		if (num == 3)
		{
			// presos 01/11: cobertura fora da ilha principal: cancela sempre (também com o ponto já classificado, porque o
			// FindCover devolve o mesmo ponto a cada recarga) e veta o FindCover por 4 s. Antes o bot ficava de pé sem
			// atirar rente à cerca até o coverT acabar, ou alternava cobertura e combate a cada 0,2 s.
			coverT = 0f;
			coverVetoT = Time.time + 4f;
			Evitados[1]++;
			return destSub;
		}
		if ((destSub - ch.Pos).Flat().magnitude < 2f)
		{
			if (num == 1)
			{
				goalT = 0f;
			}
			else if (num == 2 && ai == "search")
			{
				searchT = 99f;
			}
		}
		return destSub;
	}

	// presos 01/11: o desvio de lado do stuckN 4 sorteia o lado. Se o lado sorteado bate na borda do NavMesh em 2,5 m
	// e o outro não, vai pelo outro. Os dois livres ou os dois bloqueados: igual a antes. 1 SamplePosition e até
	// 2 NavMesh.Raycast por desvio.
	private float LadoLivre(float lado)
	{
		if (!UnityEngine.AI.NavMesh.SamplePosition(ch.Pos, out var hit, 1f, -1))
		{
			return lado;
		}
		// presos v4: 3,2 m (era 2,5 m, igual ao raio de chegada da patrulha: o desvio "chegava" na hora) e lado que sai do
		// quadrado da arena conta como bloqueado
		Vector3 vector = new Vector3(Mathf.Cos(YawRad), 0f, 0f - Mathf.Sin(YawRad)) * DesvioLado;
		if (!UnityEngine.AI.NavMesh.Raycast(hit.position, hit.position + vector * lado, out _, -1) && FBNav.DentroLimite(hit.position + vector * lado, 0.6f))
		{
			return lado;
		}
		if (!UnityEngine.AI.NavMesh.Raycast(hit.position, hit.position - vector * lado, out _, -1) && FBNav.DentroLimite(hit.position - vector * lado, 0.6f))
		{
			return 0f - lado;
		}
		return lado;
	}

	// presos v4: tamanho do desvio de lado do stuckN 4 (passa do raio de chegada de 2,5 m da patrulha)
	private const float DesvioLado = 3.2f;

	// presos v4: ponto livre para o bot travado sair andando, sem teleporte. No NavMesh, ligado ao pé do bot em reta pelo
	// NavMesh (NavMesh.Raycast sem borda no meio), dentro do quadrado da arena, com a cápsula livre (Livre: parede, objeto e
	// outro personagem) e sem nada sólido na reta a 0,4 m e a 1,2 m do chão. Tenta primeiro para trás do que ele vinha
	// tentando andar, depois os lados e as diagonais, a 2,2 m e depois a 1,4 m; a cada vez começa por um lado diferente.
	// No pior caso 16 SamplePosition, 16 NavMesh.Raycast, 16 Livre e 32 raios, só no stuckN 4 (fora da patrulha) e 10.
	private bool SaidaLivre(out Vector3 p)
	{
		p = ch.Pos;
		if (!UnityEngine.AI.NavMesh.SamplePosition(ch.Pos, out var hit, 1f, -1))
		{
			return false;
		}
		Vector3 position = hit.position;
		Vector3 vector = vel.Flat();
		float num = ((vector.sqrMagnitude > 0.01f) ? Mathf.Atan2(vector.x, vector.z) : YawRad);
		float num2 = ((saidaGiro++ % 2 == 0) ? 1f : (-1f));
		float[] array = new float[8] { 180f, 90f, -90f, 135f, -135f, 45f, -45f, 0f };
		float s = S;
		for (int i = 0; i < 2; i++)
		{
			float num3 = ((i == 0) ? 2.2f : 1.4f);
			for (int j = 0; j < array.Length; j++)
			{
				float f = num + array[j] * num2 * ((float)Math.PI / 180f);
				if (!UnityEngine.AI.NavMesh.SamplePosition(position + new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f)) * num3, out var hit2, 0.5f, -1))
				{
					continue;
				}
				Vector3 position2 = hit2.position;
				Vector3 vector2 = (position2 - ch.Pos).Flat();
				float magnitude = vector2.magnitude;
				if (!(magnitude < 0.8f) && Mathf.Abs(position2.y - position.y) <= 1.2f && !UnityEngine.AI.NavMesh.Raycast(position, position2, out _, -1) && FBNav.DentroLimite(position2, 1.2f) && Livre(position2))
				{
					vector2 /= magnitude;
					if (!FBWorld.RayHit(ch.Pos + Vector3.up * 0.4f * s, vector2, magnitude, 0.05f) && !FBWorld.RayHit(ch.Pos + Vector3.up * 1.2f * s, vector2, magnitude, 0.05f))
					{
						p = position2;
						return true;
					}
				}
			}
		}
		return false;
	}

	// presos v4: manda o bot andar até p por até 1,6 s (ver o bloco da saída antes do DestinoSeguro), conta e escreve no log
	private void Sai(Vector3 p, int tipo, string porque)
	{
		saidaP = p;
		saidaAte = Time.time + 1.6f;
		path = null;
		navT = 0f;
		Evitados[Mathf.Clamp(tipo, 0, Evitados.Length - 1)]++;
		Debug.Log(string.Format("[BURITIRO] presos: {0} saída a pé por {1}: de ({2:0.0}, {3:0.00}, {4:0.0}) para ({5:0.0}, {6:0.00}, {7:0.0}), {8:0.0} m · ai {9} · sem teleporte", ch.name, porque, ch.Pos.x, ch.Pos.y, ch.Pos.z, p.x, p.y, p.z, Vector3.Distance(ch.Pos, p), ai));
	}

	// presos 01/11: 1 = p está na mesma parte do NavMesh que "de" (at = p no NavMesh); 0 = não está (at = ponto da parte
	// de "de" mais perto de p: fim do caminho parcial de "de" até p); -1 = sem NavMesh perto de um dos dois.
	private static int MesmaParte(Vector3 de, Vector3 p, float r, out Vector3 at)
	{
		at = p;
		if (!UnityEngine.AI.NavMesh.SamplePosition(de, out var hit, 3f, -1) || !UnityEngine.AI.NavMesh.SamplePosition(p, out var hit2, r, -1))
		{
			return -1;
		}
		at = hit2.position;
		UnityEngine.AI.NavMeshPath navMeshPath = Caminho;
		if (!UnityEngine.AI.NavMesh.CalculatePath(hit.position, hit2.position, -1, navMeshPath))
		{
			return -1;
		}
		if (navMeshPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
		{
			return 1;
		}
		if (navMeshPath.status != UnityEngine.AI.NavMeshPathStatus.PathPartial)
		{
			return -1;
		}
		Vector3[] corners = navMeshPath.corners;
		if (corners.Length == 0)
		{
			return -1;
		}
		at = corners[corners.Length - 1];
		return 0;
	}

	// presos 01/11: p na ilha principal (a da âncora)? 1 = sim (at = p no NavMesh); 0 = não (at = ponto da ilha principal
	// mais perto de p); -1 = sem âncora ou sem NavMesh perto; -2 = só com soCache: precisa da busca da âncora (cara).
	// fresco = MesmaParte da âncora até p, sem cache. Sem fresco usa a célula do FBNav (Celula) e confere p de perto,
	// porque a célula de 4x4x6 m vale para o ponto que a decidiu e pode pegar os dois lados da cerca: célula dentro ->
	// caminho de p até esse ponto (curto); célula fora -> caminho de p até o fim guardado (de dentro de uma ilha a busca só
	// anda pela ilha, pequena). Em todo "não" o caminho de p até o ponto achado também tem que dar parcial: se chega lá, o
	// parcial da âncora era falta de nós da busca num caminho longo, não ilha.
	private static int Principal(Vector3 p, float r, out Vector3 at, bool fresco = true, bool soCache = false)
	{
		at = p;
		Vector3? anchor = FBNav.Anchor;
		if (!anchor.HasValue)
		{
			return -1;
		}
		if (!fresco)
		{
			int num = FBNav.Celula(p, r, soCache, out var hp, out var refP, out var fimP);
			if (num == 3)
			{
				return -2;
			}
			if (num == -1 || num == 2)
			{
				at = hp;
				return -1;
			}
			UnityEngine.AI.NavMeshPath navMeshPath = Caminho;
			if (num == 1)
			{
				if (UnityEngine.AI.NavMesh.CalculatePath(hp, fimP, -1, navMeshPath) && navMeshPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
				{
					at = hp;
					return 1;
				}
				at = fimP;
				return 0;
			}
			if (UnityEngine.AI.NavMesh.CalculatePath(hp, refP, -1, navMeshPath) && navMeshPath.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
			{
				at = hp;
				return 1;
			}
			// presos 01/11: célula dentro, mas p não chega no ponto que a decidiu: célula que pega a cerca, p do outro lado
			if (soCache)
			{
				return -2;
			}
		}
		int num2 = MesmaParte(anchor.Value, p, r, out at);
		if (num2 != 0)
		{
			return num2;
		}
		if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, r, -1) && UnityEngine.AI.NavMesh.CalculatePath(hit.position, at, -1, Caminho) && Caminho.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
		{
			at = hit.position;
			return 1;
		}
		return 0;
	}

	// presos 01/11: caminho completo do NavMesh de a até b (1 CalculatePath)
	private static bool Alcanca(Vector3 a, Vector3 b)
	{
		if (UnityEngine.AI.NavMesh.SamplePosition(a, out var hit, 2f, -1) && UnityEngine.AI.NavMesh.SamplePosition(b, out var hit2, 4f, -1) && UnityEngine.AI.NavMesh.CalculatePath(hit.position, hit2.position, -1, Caminho))
		{
			return Caminho.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
		}
		return false;
	}

	// presos 01/11: bot em ilha do NavMesh? saida = ponto da parte principal mais perto dele (até 25 m). Com âncora:
	// Principal sem cache (pega também célula rotulada "dentro" por engano). Sem âncora (outros modos): ilha = não chega
	// nem em casa (onde nasceu) nem num ponto sorteado do NavMesh.
	private bool IlhaSaida(out Vector3 saida)
	{
		Vector3 pos = ch.Pos;
		saida = pos;
		if (FBNav.HasAnchor)
		{
			if (Principal(pos, 2f, out saida) == 0)
			{
				return (saida - pos).magnitude <= 25f;
			}
			return false;
		}
		Vector3? vector = FBNav.RandomPoint();
		if (!vector.HasValue)
		{
			return false;
		}
		if (MesmaParte(home, pos, 2f, out var at) != 0 || MesmaParte(vector.Value, pos, 2f, out var at2) != 0)
		{
			return false;
		}
		saida = (((at2 - pos).sqrMagnitude < (at - pos).sqrMagnitude) ? at2 : at);
		if ((saida - pos).magnitude <= 25f)
		{
			return !Alcanca(pos, saida);
		}
		return false;
	}

	// presos 01/11: destino novo que o NavMesh alcança daqui (o da patrulha/modo ou um ponto sorteado a 15-35 m). Na
	// busca, encerra a busca (olha em volta e volta à patrulha).
	private void NovoDestino()
	{
		path = null;
		navT = 0f;
		pauseT = 0f;
		if (ai == "search")
		{
			searchT = 99f;
			return;
		}
		Vector3 vector = PickPatrolGoal();
		if (!Alcanca(ch.Pos, vector))
		{
			Vector3? vector2 = FBNav.RandomPoint(ch.Pos, FBUtil.Rnd(15f, 35f));
			if (vector2.HasValue && Alcanca(ch.Pos, vector2.Value))
			{
				vector = vector2.Value;
			}
		}
		goalP = vector;
		goalT = FBUtil.Rnd(8f, 16f);
	}

	// presos 01/11: ponto a d metros do começo, andando pelos cantos do caminho
	private static Vector3 AoLongo(Vector3[] c, float d)
	{
		for (int i = 1; i < c.Length; i++)
		{
			float num = Vector3.Distance(c[i - 1], c[i]);
			if (num >= d)
			{
				return Vector3.Lerp(c[i - 1], c[i], d / Mathf.Max(num, 0.001f));
			}
			d -= num;
		}
		return c[c.Length - 1];
	}

	// presos 01/11: para onde realocar um bot travado há 8 s
	private bool AlvoRealocar(out Vector3 alvo, out int tipo, out string porque)
	{
		Vector3 pos = ch.Pos;
		tipo = 0;
		porque = "ilha do NavMesh (vigia, travado)";
		if (IlhaSaida(out alvo))
		{
			return true;
		}
		tipo = 1;
		if (!UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 0.8f, -1))
		{
			porque = "fora do NavMesh (NavMesh mais perto, até 4 m)";
			if (FBNav.Find(pos, 4f, out alvo) && (alvo - pos).magnitude >= 0.4f)
			{
				if (FBNav.HasAnchor)
				{
					return Principal(alvo, 1f, out var _) != 0;
				}
				return true;
			}
			return false;
		}
		if (vigAcomp && UnityEngine.AI.NavMesh.SamplePosition(vigDest, out var hit2, 4f, -1) && UnityEngine.AI.NavMesh.CalculatePath(hit.position, hit2.position, -1, Caminho) && Caminho.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
		{
			Vector3[] corners = Caminho.corners;
			if (corners.Length > 1)
			{
				alvo = AoLongo(corners, 3.5f);
				if ((alvo - pos).Flat().magnitude >= 1.5f)
				{
					porque = "travado com caminho completo (3,5 m à frente no caminho do NavMesh)";
					return true;
				}
			}
		}
		Vector3? vector = FBNav.RandomPoint(pos, 4f);
		if (vector.HasValue && (vector.Value - pos).Flat().magnitude >= 1.5f && Alcanca(pos, vector.Value))
		{
			alvo = vector.Value;
			porque = "travado (ponto do NavMesh a até 4 m que o caminho alcança)";
			return true;
		}
		alvo = pos;
		porque = null;
		return false;
	}

	private void Pendente(Vector3 alvo, int tipo, string porque)
	{
		realocAte = Time.time + 10f;
		realocAlvo = alvo;
		realocDe = ch.Pos;
		realocTipo = tipo;
		realocPorque = porque;
	}

	// presos 01/11: como a câmera vê o ponto p (1,2 m acima do pé). aparente = distância com o zoom: com luneta o FOV da
	// câmera cai (4x, 6x) e 60 m parecem 15 m; referência = FOV normal do jogador (FBPlayer.FovBase), sem ampliar com FOV
	// mais aberto. Devolve se p está na tela (na frente da câmera, com 15% de folga nas bordas).
	private static bool Vista(Camera cam, Vector3 p, out float aparente)
	{
		Vector3 position = cam.transform.position;
		float num = (FBPlayer.I ? FBPlayer.I.FovBase : 60f);
		float num2 = Mathf.Tan(cam.fieldOfView * 0.5f * ((float)Math.PI / 180f)) / Mathf.Tan(Mathf.Clamp(num, 20f, 150f) * 0.5f * ((float)Math.PI / 180f));
		aparente = (p - position).magnitude * Mathf.Min(1f, num2);
		Vector3 vector = cam.WorldToViewportPoint(p + Vector3.up * 1.2f);
		if (vector.z > 0f && vector.x >= -0.15f && vector.x <= 1.15f && vector.y >= -0.15f)
		{
			return vector.y <= 1.15f;
		}
		return false;
	}

	// presos 01/11: a câmera não vê o salto de p: sem câmera, p fora da tela, peito E cabeça de p atrás de algo sólido
	// (poste ou tronco fino no meio não basta), ou p longe com salto pequeno para a distância. salto = quanto o bot anda
	// no teleporte. O teleporte antigo do stuckN 10 continua com a regra dele (bloco do stuckN sem mudança).
	private static bool Escondido(Vector3 p, float salto)
	{
		Camera main = Camera.main;
		if (!main)
		{
			return true;
		}
		// presos 01/11 (v3): longe (40 m aparentes ou mais, com o zoom) só esconde salto até 6,25% da distância (2,5 m a
		// 40 m, como o stuckN 10). Antes os 40 m bastavam para qualquer salto: a saída de ilha do meio da quadra (6 a
		// 25 m) e os 3,5 m do vigia piscavam no meio da tela. Salto maior espera p sair da tela ou ficar atrás de algo.
		if (!Vista(main, p, out var aparente) || (aparente >= 40f && salto <= aparente * 0.0625f))
		{
			return true;
		}
		return Atras(main.transform.position, p);
	}

	// presos 01/11: peito (1,0 m) e cabeça (1,7 m) de p atrás de algo sólido vistos de o (a câmera)
	private static bool Atras(Vector3 o, Vector3 p)
	{
		float magnitude = (p - o).magnitude;
		if (FBWorld.RayHit(o, (p + Vector3.up * 1f - o).normalized, magnitude - 0.5f))
		{
			return FBWorld.RayHit(o, (p + Vector3.up * 1.7f - o).normalized, magnitude - 0.5f);
		}
		return false;
	}

	// presos 01/11: cabe a cápsula do bot no ponto (de 0,32 m a 1,8 m acima do chão)? Personagens ficam na camada 10,
	// fora da SolidMask: o CheckCapsule não vê nenhum (nem o próprio bot) e eles são conferidos à parte.
	private bool Livre(Vector3 p)
	{
		float s = S;
		if (Physics.CheckCapsule(p + Vector3.up * 0.62f * s, p + Vector3.up * 1.5f * s, 0.3f * s, FBWorld.SolidMask, QueryTriggerInteraction.Ignore))
		{
			return false;
		}
		// presos 01/11 (v3): ponto ocupado por personagem: outro bot, civil ou o jogador com o pé a menos de 0,8 m (os
		// dois raios de 0,32 m, com folga) e a menos de 1,8 m de altura. Sem isto os bots presos na mesma célula recebiam
		// o mesmo ponto de saída (FBNav.Celula) e o segundo caía dentro do primeiro. Morto e caído contam: o
		// CharacterController deles continua ligado no lugar (FBCharacter.Die não desliga). 1 volta em FBMatch.All.
		List<FBCharacter> all = FBMatch.All;
		float num = 0.8f * s;
		for (int i = 0; i < all.Count; i++)
		{
			FBCharacter fBCharacter = all[i];
			if ((bool)fBCharacter && fBCharacter != ch && (fBCharacter.Pos - p).Flat().sqrMagnitude < num * num && Mathf.Abs(fBCharacter.Pos.y - p.y) < 1.8f * s)
			{
				return false;
			}
		}
		return true;
	}

	// presos 01/11: o ponto ou, se ele está apertado (parede a menos de 0,3 m, carro, objeto, personagem), um ponto do
	// NavMesh a 0,45 m dele em que a cápsula cabe. No pior caso 17 Livre, 16 SamplePosition e 8 NavMesh.Raycast, só na
	// hora de realocar.
	private Vector3? Folga(Vector3 p)
	{
		if (Livre(p))
		{
			return p;
		}
		for (int i = 0; i < 8; i++)
		{
			float f = (float)i * ((float)Math.PI / 4f);
			if (UnityEngine.AI.NavMesh.SamplePosition(p + new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f)) * 0.45f, out var hit, 0.3f, -1) && Livre(hit.position))
			{
				return hit.position;
			}
		}
		// presos 01/11 (v3): personagem parado no ponto (corpo, caído, outro bot) pega os 8 vizinhos a 0,45 m. A 0,9 m ele
		// não pega mais; sem este anel a saída de ilha esperava o corpo sair, isto é, até o fim da rodada. Só vale vizinho
		// que o NavMesh liga ao ponto em reta (NavMesh.Raycast sem bater na borda): a 0,9 m o SamplePosition poderia pegar
		// o NavMesh do outro lado da cerca, de volta na ilha.
		for (int j = 0; j < 8; j++)
		{
			float f2 = (float)j * ((float)Math.PI / 4f);
			if (UnityEngine.AI.NavMesh.SamplePosition(p + new Vector3(Mathf.Sin(f2), 0f, Mathf.Cos(f2)) * 0.9f, out var hit2, 0.3f, -1) && !UnityEngine.AI.NavMesh.Raycast(p, hit2.position, out _, -1) && Livre(hit2.position))
			{
				return hit2.position;
			}
		}
		return null;
	}

	private void TentaRealocar()
	{
		if (!realocAlvo.HasValue)
		{
			return;
		}
		Vector3 value = realocAlvo.Value;
		// presos 01/11 (v3): o tamanho do salto entra no Escondido (longe só esconde salto pequeno)
		float salto = (value - ch.Pos).magnitude;
		if (!Escondido(ch.Pos, salto) || !Escondido(value, salto))
		{
			return;
		}
		Vector3? vector = Folga(value);
		if (!vector.HasValue)
		{
			// presos 01/11: ponto ocupado. Saída de ilha espera (tenta a cada 1 s); o resto recalcula mais tarde.
			if (realocTipo != 0)
			{
				realocAlvo = null;
				vigNivel = Mathf.Min(vigNivel, 1);
				vigBloq = Mathf.Min(vigBloq, 4f);
			}
			return;
		}
		Vector3 value2 = vector.Value;
		if (value2 != value)
		{
			// presos 01/11 (v3): a Folga tirou o ponto do lugar (até 0,9 m): confere de novo com o ponto e o salto finais;
			// vendo, espera como acima (tenta a cada 1 s)
			salto = (value2 - ch.Pos).magnitude;
			if (!Escondido(ch.Pos, salto) || !Escondido(value2, salto))
			{
				return;
			}
		}
		Realocar(value2, realocPorque, realocTipo);
	}

	private void Realocar(Vector3 at, string porque, int tipo)
	{
		Vector3 pos = ch.Pos;
		float? num = FBWorld.GroundY(at.x, at.y, at.z, 0.6f, 0.6f);
		if (num.HasValue)
		{
			at.y = num.Value;
		}
		FBMatch.Teleport(ch, at, ch.Yaw);
		vel = Vector3.zero;
		// revisão: a saída a pé era do lugar antigo
		saidaP = null;
		saidaAte = -99f;
		path = null;
		navT = 0f;
		stuckN = 0;
		stuckWin = 0f;
		lastPos = ch.Pos;
		realocAlvo = null;
		Contar(tipo, porque, pos, at);
		VigiaZera();
		ilhaN = 0;
		destTem = false;
		// presos 01/11: o destino novo que vem depois da realocação é classificado já, sem esperar o limite de 1/s
		destT = -99f;
		if (Vigiado)
		{
			goalP = PickPatrolGoal();
			goalT = FBUtil.Rnd(10f, 20f);
			pauseT = 0f;
		}
	}

	// presos 01/11: conta a realocação (Realocados/RealocadosPor) e escreve "[BURITIRO] presos: ..." no log, com o que a
	// câmera via (distância aparente e se estava na tela) do bot e do ponto
	private void Contar(int tipo, string porque, Vector3 de, Vector3 para)
	{
		Realocados++;
		RealocadosPor[Mathf.Clamp(tipo, 0, RealocadosPor.Length - 1)]++;
		Debug.Log(string.Format("[BURITIRO] presos: {0} realocado por {1}: de ({2:0.0}, {3:0.00}, {4:0.0}) para ({5:0.0}, {6:0.00}, {7:0.0}), {8:0.0} m · ai {9} · câmera: {10}", ch.name, porque, de.x, de.y, de.z, para.x, para.y, para.z, Vector3.Distance(de, para), ai, NaCamera(de, para)));
	}

	private static string NaCamera(Vector3 de, Vector3 para)
	{
		Camera main = Camera.main;
		if (!main)
		{
			return "sem câmera";
		}
		float aparente;
		bool flag = Vista(main, de, out aparente);
		float aparente2;
		bool flag2 = Vista(main, para, out aparente2);
		// presos 01/11 (v3): ponto na tela encoberto (peito e cabeça atrás de algo sólido) sai como "na tela atrás de algo"
		Vector3 position = main.transform.position;
		return string.Format("bot a {0:0} m aparentes {1}, ponto a {2:0} m aparentes {3}", aparente, flag ? (Atras(position, de) ? "na tela atrás de algo" : "na tela") : "fora da tela", aparente2, flag2 ? (Atras(position, para) ? "na tela atrás de algo" : "na tela") : "fora da tela");
	}

	private float RemapSpeed(float s)
	{
		Dictionary<string, object> o = GameData.Section("puppet");
		float num = Json.F(o, "playerWalkSpeed", 4f);
		float num2 = Json.F(o, "playerRunSpeed", 6.8f);
		float num3 = Json.F(o, "aiWalkAnimSpeed", 2f);
		float num4 = Json.F(o, "aiRunAnimSpeed", 5f);
		if (!(s <= num))
		{
			return num3 + (s - num) / (num2 - num) * (num4 - num3);
		}
		return s / num * num3;
	}

	private Vector3 AimTarget(FBCharacter t)
	{
		Vector3[] array = new Vector3[3];
		FBBotSense.TargetPoints(t, array);
		int num = ((((uint)seenMask & 2u) != 0) ? 1 : (((seenMask & 1) == 0) ? 2 : 0));
		if (num == 1 && Time.time - acquireT > 0.8f && ((uint)seenMask & (true ? 1u : 0u)) != 0 && UnityEngine.Random.value < FBBotSense.HeadAim)
		{
			num = 0;
		}
		float num2 = (ch.MuzzlePos - array[num]).magnitude / 280f;
		float num3 = FBCombat.DiffAcc * 0.85f;
		return array[num] + t.velocity.Flat() * num2 * num3;
	}

	private void FireSmart(FBCharacter t)
	{
		ch.protUntil = 0f;
		Vector3 muzzlePos = ch.MuzzlePos;
		Vector3 vector = AimTarget(t);
		Vector3 normalized = (vector - muzzlePos).normalized;
		FBCombatStats.BotShot((t.Pos - ch.Pos).magnitude);
		Vector3 hitPoint = vel.Flat();
		bool flag = hitPoint.magnitude > 1f;
		Vector3 dir = FBBotSense.Scatter(normalized, FBBotSense.AimErrorDeg(ch, t, Time.time - acquireT, flag));
		wu.Fire(muzzlePos, dir, new FBRecoil.State
		{
			aiming = true,
			crouch = ch.crouch,
			moving = flag,
			zoom = 1f,
			grip = 1f
		}, out hitPoint);
		ch.lastShot = 0f;
		ch.aimPoint = vector;
		FBBotBrain.TeamIntel(ch.team, t.Pos);
	}

	private void FireAtCadence(FBCharacter t, int bMin, int bMax, float pMin, float pMax)
	{
		WeaponDef cur = wu.Cur;
		if (cur != null)
		{
			bool auto = cur.auto;
			if (auto && burst <= 0)
			{
				burst = UnityEngine.Random.Range(bMin, bMax);
			}
			else if (!auto)
			{
				burst = 0;
			}
			FireSmart(t);
			if (auto)
			{
				burst = Mathf.Max(0, burst - 1);
				shotT = ((burst > 0) ? Mathf.Clamp(cur.Interval, 0.08f, 0.35f) : (FBUtil.Rnd(pMin, pMax) * FBCombat.DiffReact));
			}
			else
			{
				shotT = FBUtil.Rnd(Mathf.Max(0.55f, pMin), Mathf.Max(0.9f, pMax)) * Mathf.Max(0.8f, FBCombat.DiffReact);
			}
		}
	}

	private (Vector3? desired, float speed, Vector3? face)? Revive(float dt)
	{
		if (!FBMatch.KnockOn || ch.knocked || ch.team == 2)
		{
			return null;
		}
		float now = Time.time;
		FBCharacter fBCharacter = reviveTgt;
		if (!fBCharacter || !fBCharacter.alive || !fBCharacter.knocked || Busy(fBCharacter))
		{
			fBCharacter = null;
			float num = 40f;
			foreach (FBCharacter item in FBMatch.TeamOf(ch))
			{
				if (item.alive && item.knocked && !Busy(item))
				{
					float magnitude = (item.Pos - ch.Pos).magnitude;
					if (magnitude < num)
					{
						num = magnitude;
						fBCharacter = item;
					}
				}
			}
			reviveTgt = fBCharacter;
		}
		if (!fBCharacter)
		{
			return null;
		}
		float magnitude2 = (fBCharacter.Pos - ch.Pos).magnitude;
		if ((safeT -= dt) <= 0f)
		{
			safeT = 0.3f;
			safeOk = SafeToRevive(fBCharacter);
		}
		if (!safeOk)
		{
			if (fBCharacter.revivedBy == ch)
			{
				AbortRevive();
			}
			if (magnitude2 < 8f)
			{
				ReviveUnsafeBlocks++;
			}
			return null;
		}
		if (magnitude2 > 1.4f)
		{
			return (fBCharacter.Pos, (magnitude2 > 6f) ? 5.6f : 3f, fBCharacter.Pos);
		}
		if (fBCharacter.revivedBy != ch)
		{
			ReviveStarts++;
			float num2 = 1E+09f;
			foreach (FBCharacter item2 in FBMatch.Actors())
			{
				if ((bool)item2 && item2.alive && !item2.knocked && FBMatch.Hostile(item2, ch))
				{
					num2 = Mathf.Min(num2, (item2.Pos - fBCharacter.Pos).magnitude);
				}
			}
			if (num2 < 22f)
			{
				ReviveHot++;
			}
		}
		ch.crouch = false;
		fBCharacter.revivedBy = ch;
		fBCharacter.reviveTick = now;
		rescuing = true;
		if (FBMechanics.ReviveStep(fBCharacter, dt, FBSession.mode))
		{
			FBCombat.CompleteRevive(fBCharacter, ch);
			reviveTgt = null;
			rescuing = false;
		}
		return (null, 0f, fBCharacter.Pos);
		bool Busy(FBCharacter o)
		{
			if (o.revivedBy != null && o.revivedBy != ch)
			{
				return now - o.reviveTick < 0.3f;
			}
			return false;
		}
	}

	private bool SafeToRevive(FBCharacter kt)
	{
		float time = Time.time;
		if (time - hitT < 5f || ai == "flee")
		{
			ReviveWhy[0]++;
			return false;
		}
		if ((ai == "combat" || ai == "cover" || sees) && (bool)target && target.alive && (target.Pos - ch.Pos).magnitude < 45f)
		{
			ReviveWhy[1]++;
			return false;
		}
		Vector3? lastNoiseP = FBBotBrain.lastNoiseP;
		if (lastNoiseP.HasValue)
		{
			Vector3 np = lastNoiseP.GetValueOrDefault();
			if (time - FBBotBrain.lastNoiseT < 2.5f && ((np - ch.Pos).magnitude < 25f || (np - kt.Pos).magnitude < 25f) && !FBMatch.TeamOf(ch).Any((FBCharacter o) => (bool)o && o.alive && (o.Pos - np).magnitude < 1.5f && o.lastShot < 2.7f))
			{
				ReviveWhy[2]++;
				return false;
			}
		}
		foreach (FBCharacter item in FBMatch.Actors())
		{
			if ((bool)item && item.alive && !item.knocked && FBMatch.Hostile(item, ch))
			{
				float num = Mathf.Min((item.Pos - kt.Pos).magnitude, (item.Pos - ch.Pos).magnitude);
				if (num < 22f)
				{
					ReviveWhy[3]++;
					return false;
				}
				if (num < 40f && (FBWorld.ClearLine(item.Pos + Vector3.up * 1.5f * item.ScaleK, kt.Pos + Vector3.up * 0.5f) || FBWorld.ClearLine(item.Pos + Vector3.up * 1.5f * item.ScaleK, ch.Pos + Vector3.up * 1.2f * ch.ScaleK)))
				{
					ReviveWhy[4]++;
					return false;
				}
			}
		}
		return true;
	}

	private void AbortRevive()
	{
		if ((bool)reviveTgt && reviveTgt.revivedBy == ch)
		{
			reviveTgt.revivedBy = null;
		}
		rescuing = false;
	}

	private void BotMed(float dt)
	{
		if (medT > 0f)
		{
			float num = 3f - medT;
			medT -= dt;
			float num2 = 3f - Mathf.Max(0f, medT);
			ch.shooting = (ch.aiming = false);
			if (num < 0.5f && num2 >= 0.5f)
			{
				ch.kits--;
			}
			if (num2 > 0.5f)
			{
				ch.hp = Mathf.Min(ch.maxHp, ch.hp + 75f * (num2 - Mathf.Max(num, 0.5f)) / 2.5f);
			}
			if (medT <= 0f)
			{
				healEndT = 0.35f;
			}
		}
		if (healEndT > 0f)
		{
			healEndT -= dt;
		}
		string text = ((!(medT > 0f)) ? ((healEndT > 0f) ? "FF_SYS_Heal_End" : null) : ((3f - medT < 0.35f) ? "FF_SYS_Heal_Start" : "FF_SYS_Heal_Loop"));
		if (text != null && !FBRegistry.I.Clip(text))
		{
			text = null;
		}
		string text2 = ch.RescuePose(rescuing && text == null, dt);
		rescuing = false;
		if (text == null)
		{
			text = text2;
		}
		if (text != null || (ch.sys != null && (ch.sys.StartsWith("FF_SYS_Heal") || ch.sys.StartsWith("FF_SYS_Revive") || ch.sys.StartsWith("IE_Revive"))))
		{
			ch.sys = text;
		}
		bool flag = medT > 0f || healEndT > 0f || ch.Rescuing;
		ch.SetWeaponVisible(!flag && !holster && !ch.UnarmedLocomotion && ch.dance == null && !ch.vehiclePose);
		ch.StowCurrent(holster || (flag && wu.Cur != null));
		if (!(medT > 0f) && ch.team < 2 && ch.hp < 45f && ch.kits > 0 && seeT > 2.5f && ai != "dummy" && ai != "victim" && UnityEngine.Random.value < dt * 0.8f)
		{
			medT = 3f;
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBBot.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		float deltaTime = Time.deltaTime;
		destDoGoal = false;
		if (deltaTime <= 0f || !FBMatch.I)
		{
			return;
		}
		if (!ch.alive)
		{
			vel.x = (vel.z = 0f);
			Move(deltaTime);
			return;
		}
		if (ch.tagT > 0f)
		{
			ch.tagT -= deltaTime;
		}
		if (FBBattleRoyale.Active(ch) && FBBattleRoyale.StepBot(ch, deltaTime))
		{
			return;
		}
		if (ch.knocked)
		{
			KnockedStep(deltaTime);
			return;
		}
		if (FBMatch.I.mode != null && FBMatch.I.mode.br && wu.Cur == null && ch.team != 2 && FBBattleRoyale.BotLoot(ch, this, deltaTime))
		{
			return;
		}
		if (ch.getUpT > 0f)
		{
			ch.getUpT -= deltaTime;
			ch.sys = FBCombat.GetUpClip;
			vel.x = (vel.z = 0f);
			Move(deltaTime);
			if (ch.getUpT <= 0f)
			{
				ch.sys = null;
			}
			return;
		}
		BotMed(deltaTime);
		if (ai == "victim" || ch.special)
		{
			vel = new Vector3(0f, vel.y, 0f);
			ch.shooting = false;
			Move(deltaTime);
			return;
		}
		if (FBMatch.I.freeze)
		{
			vel.x = (vel.z = 0f);
			ch.shooting = false;
			// fisica 31/10: no freeze (fase de compra) zera sprint/mira da rodada anterior; senao o bot corria parado e mirava no alvo velho
			ch.sprint = false;
			ch.aiming = false;
			ch.hasAim = false;
			Move(deltaTime);
			// fisica 31/10: Anim() depois do Move zera ch.animVel (vel horizontal 0); antes ficava o animVel velho e o pe patinava
			Anim();
			return;
		}
		if (medT > 0f)
		{
			vel.x = (vel.z = 0f);
			Move(deltaTime);
			Anim();
			return;
		}
		if (ai == "dummy")
		{
			ch.shooting = false;
			ch.combat = true;
			ch.aiming = false;
			dT += deltaTime;
			float num = ((strafeAmp > 0f) ? (Mathf.Cos(dT * strafeW) * strafeAmp * strafeW) : 0f);
			vel.x = strafeDir.x * num;
			vel.z = strafeDir.z * num;
			if ((bool)FBMatch.Player)
			{
				YawRad = Mathf.Atan2(FBMatch.Player.Pos.x - ch.Pos.x, FBMatch.Player.Pos.z - ch.Pos.z);
			}
			aimYawRad = YawRad;
			Move(deltaTime);
			Anim();
			return;
		}
		thinkT -= deltaTime;
		shotT -= deltaTime;
		wu.recoil.Step(deltaTime);
		Vector3 value2;
		if (thinkT <= 0f)
		{
			float num2 = lastThinkDt;
			thinkT = (lastThinkDt = FBUtil.Rnd(0.12f, 0.22f));
			FBCharacter fBCharacter = null;
			float num3 = 1E+09f;
			int num4 = 0;
			bool flag = ai == "combat" || ai == "cover" || ai == "search" || ai == "flee";
			foreach (FBCharacter item in FBMatch.Actors())
			{
				if (!item.alive || !FBMatch.Hostile(item, ch) || item.special || FBBattleRoyale.InPlane(item))
				{
					aware.Remove(item);
					continue;
				}
				float dist;
				int seenPts;
				int mask;
				float num5 = FBBotSense.Visibility(ch, YawRad, flag, item, out dist, out seenPts, out mask);
				aware.TryGetValue(item, out var value);
				value = ((num5 > 0f) ? Mathf.Min(1.4f, value + num2 * num5 / FBBotSense.NoticeTime(dist) * (flag ? 1.7f : 1f)) : Mathf.Max(0f, value - num2 * 0.3f));
				aware[item] = value;
				float num6 = dist + (float)(item.knocked ? 30 : 0) + (float)((item.protUntil > Time.time) ? 20 : 0);
				if (value >= 1f && num5 > 0f && num6 < num3)
				{
					num3 = num6;
					fBCharacter = item;
					num4 = mask;
				}
			}
			sees = fBCharacter;
			if ((bool)fBCharacter)
			{
				seenMask = num4;
				if (fBCharacter != acquired)
				{
					acquired = fBCharacter;
					acquireT = Time.time;
				}
			}
			if ((bool)fBCharacter)
			{
				if (ai != "combat" && ai != "cover" && ai != "flee")
				{
					shotT = FBUtil.Rnd(0.14f, 0.32f) * FBCombat.DiffReact;
					burst = 0;
					float num7 = role switch
					{
						"tactical" => 0.6f, 
						"halemph" => 0.55f, 
						"halemph2" => 0.5f, 
						"flanker_left" => 0.35f, 
						"flanker_right" => 0.35f, 
						"baianogmbarril" => 0.3f, 
						_ => 0.15f, 
					};
					// presos 01/11: sem cobertura por 4 s depois de uma cancelada fora da ilha principal (ver DestinoSeguro)
					if (num3 > 12f && UnityEngine.Random.value < num7 && Time.time >= coverVetoT)
					{
						Vector3? vector = FindCover(fBCharacter.Pos);
						if (vector.HasValue)
						{
							Vector3 valueOrDefault = vector.GetValueOrDefault();
							coverPos = valueOrDefault;
							ai = "cover";
							coverT = FBUtil.Rnd(3.5f, 6.5f);
							peekT = FBUtil.Rnd(0.3f, 0.9f);
							peeking = false;
						}
					}
				}
				FBBotBrain.TeamIntel(ch.team, fBCharacter.Pos);
				if (ai != "cover" && ai != "flee")
				{
					ai = "combat";
				}
				target = fBCharacter;
				lastSeen = fBCharacter.Pos;
				seeT = 0f;
			}
			else
			{
				seeT += 0.2f;
			}
			if ((ai == "combat" || ai == "cover") && !sees && seeT > 1.4f && lastSeen.HasValue)
			{
				ai = "search";
				searchT = 0f;
				ch.crouch = false;
			}
			if ((ai == "combat" || ai == "cover") && !sees && seeT > 3.5f)
			{
				ai = "patrol";
				target = null;
				Vector3? vector = lastSeen;
				if (vector.HasValue)
				{
					Vector3 valueOrDefault2 = vector.GetValueOrDefault();
					if ((valueOrDefault2 - ch.Pos).magnitude > 3f)
					{
						value2 = FlankPoint(valueOrDefault2);
						goto IL_08b3;
					}
				}
				value2 = PickPatrolGoal();
				goto IL_08b3;
			}
			goto IL_08ea;
		}
		goto IL_0a9c;
		IL_18bd:
		bool flag2 = sees || ai == "combat" || ai == "cover" || ai == "flee" || ai == "search" || (bool)reviveTgt;
		calmT = (flag2 ? 0f : (calmT + deltaTime));
		bool flag3 = wu.Cur != null;
		if (flag3 && (FBMatch.I.mode == null || !FBMatch.I.mode.br))
		{
			if (!holster && calmT > 2.5f && !(medT > 0f) && ch.dance == null)
			{
				holster = true;
				float num8 = ch.HolsterAnim();
				ch.SetUnarmedLocomotion(on: true);
				ch.StowCurrent(on: true);
				if (num8 <= 0f)
				{
					ch.SetWeaponVisible(v: false);
				}
			}
			else if (holster && flag2)
			{
				holster = false;
				ch.SetUnarmedLocomotion(on: false);
				ch.StowCurrent(on: false);
				if (!(medT > 0f))
				{
					ch.SetWeaponVisible(v: true);
					ch.DrawAnim();
				}
				shotT = Mathf.Max(shotT, 0.3f);
				drawT = 0.3f;
				FBAudio.PlayAt(FBWeaponSound.Draw(ch.weapon) ?? "unholster", ch.Pos + Vector3.up, 0.5f, 5f);
			}
			if (holster && !ch.UnarmedLocomotion)
			{
				ch.SetUnarmedLocomotion(on: true);
			}
		}
		else if (holster)
		{
			holster = false;
			ch.SetUnarmedLocomotion(!flag3);
		}
		if (drawT > 0f)
		{
			drawT -= deltaTime;
		}
		float num9;
		if (num9 > 0f)
		{
			num9 *= (holster ? 1.2f : ((wu.Cur != null) ? wu.Cur.move : 1.15f));
		}
		float magnitude = vel.Flat().magnitude;
		ch.combat = flag3 && !holster;
		ch.shooting = (ai == "combat" || ai == "cover") && ch.lastShot < Json.F(GameData.Section("puppet"), "shootingFlagDuration", 0.15f);
		ch.aiming = (ai == "combat" || ai == "cover") && sees;
		if (num9 > 0f && LimiteMira && !holster)
		{
			if (ch.crouch)
			{
				num9 = Mathf.Min(num9, Json.F(GameData.Section("puppet"), "aiCrouchMaxSpeed", 1.1f) * S);
			}
			else if (ch.aiming)
			{
				num9 = Mathf.Min(num9, GameData.Move("speedAiming", 3f) * S);
			}
		}
		// fisica 31/10: sprint pelo que o corpo anda (legVel do Move anterior), nao pela velocidade pedida: bot travado em poste/carro/outro
		// bot com vel > 3,6 ficava com IsRunning=true e Speed~0 (corria parado). Sem colisao legVel == vel e nada muda.
		ch.sprint = legVel.magnitude > 3.6f && !ch.crouch && !ch.shooting && !ch.aiming && ai != "combat";
		if (FBMatch.I.mode != null && FBMatch.I.mode.econ && !flag3 && ch.alive && onGround && magnitude < 0.18f && !ch.shooting)
		{
			ch.PlayWaitPose("FF_Emote_supercar_Male_Entrance_Idle01");
		}
		else
		{
			ch.StopWaitPose();
		}
		ch.relaxed = false;
		float num10 = 0f;
		float num11 = 0f;
		Vector3? vector2;
		// presos 01/11: presoDest = para onde o bot quer ir (antes do Steer), para o vigia de progresso. Com âncora, destino
		// de patrulha/busca/cobertura fora da ilha principal do NavMesh vira o ponto da ilha principal mais perto dele
		// (DestinoSeguro): a reta até ele (regra dos 12 m) e o fim do caminho parcial (FBNav.Path devolve o destino cru)
		// levavam o bot para dentro da quadra cercada, cujo alambrado é painel de um lado só (de fora o ClearLine não vê).
		// No modo estrito (bot que travou há pouco) e com destino ainda sem classificar sempre vai pelo Steer.
		// presos v4: saída a pé de bot travado (stuckN 4 fora da patrulha, stuckN 10): por até 1,6 s ele anda até o ponto livre,
		// em qualquer estado (no combate continua mirando e atirando no alvo); chegando a 0,5 m, acaba
		if (saidaP.HasValue)
		{
			Vector3 value9 = saidaP.Value;
			if (Time.time < saidaAte && (value9 - ch.Pos).Flat().magnitude > 0.5f && !ch.vehiclePose)
			{
				vector2 = value9;
				num9 = Mathf.Max(num9, 2.6f);
				// agachado o corpo não passa de ~1,1 m/s e o pé escorregaria
				ch.crouch = false;
			}
			else
			{
				saidaP = null;
			}
		}
		Vector3? presoDest = vector2;
		destPendente = false;
		if (vector2.HasValue && num9 > 0f)
		{
			vector2 = DestinoSeguro(vector2.GetValueOrDefault());
		}
		// presos v4: destino fora do quadrado da arena (parede invisível do 10x10) vem para dentro, a 1,5 m da parede; antes o
		// bot empurrava a parede (o strafe do combate e o desvio do stuckN 4 não olham o quadrado)
		if (vector2.HasValue && FBNav.TemLimite && !FBNav.DentroLimite(vector2.Value, 1f))
		{
			vector2 = FBNav.PraDentro(vector2.Value, 1.5f);
		}
		if (vector2.HasValue)
		{
			Vector3 valueOrDefault3 = vector2.GetValueOrDefault();
			if (Estrito || destPendente || !FBWorld.ClearLine(ch.Pos, valueOrDefault3) || (ch.Pos - valueOrDefault3).magnitude > 12f)
			{
				vector2 = Steer(vector2, deltaTime);
			}
		}
		Vector3? vector4;
		if (vector2.HasValue)
		{
			Vector3 valueOrDefault4 = vector2.GetValueOrDefault();
			Vector3 vector3 = (valueOrDefault4 - ch.Pos).Flat();
			float magnitude2 = vector3.magnitude;
			if (magnitude2 > 0.25f)
			{
				vector3 /= magnitude2;
				if (ch.crouch && num9 > 0f && LimiteMira && !holster)
				{
					// v15: agachado de lado o clipe só chega a ~0,82 m/s (0,567 x 1,45); de frente/trás chega a ~1,67
					Vector3 frente = Quaternion.Euler(0f, ch.Yaw, 0f) * Vector3.forward;
					if (vector4.HasValue)
					{
						Vector3 olha = (vector4.GetValueOrDefault() - ch.Pos).Flat();
						if (olha.sqrMagnitude > 0.01f) frente = olha.normalized;
					}
					float lado = Mathf.Abs(Vector3.Cross(frente, vector3).y);
					float limLado = Mathf.Lerp(Json.F(GameData.Section("puppet"), "aiCrouchMaxSpeed", 1.1f), Json.F(GameData.Section("puppet"), "aiCrouchSideMaxSpeed", 0.8f), lado) * S;
					if (num9 > limLado) num9 = limLado;
				}
				num10 = vector3.x * num9;
				num11 = vector3.z * num9;
				Vector3 valueOrDefault5 = vector4.GetValueOrDefault();
				if (!vector4.HasValue)
				{
					valueOrDefault5 = valueOrDefault4;
					vector4 = valueOrDefault5;
				}
			}
		}
		float num12 = (ch.sprint ? 9f : ((ai == "combat") ? 6.5f : 5f));
		// fisica 31/10: agachado freia rapido ate o limite do agachado (antes o corpo levava ~0,3 s no combate e ~0,6 s chegando na cobertura
		// para cair de 2,8-3,9 m/s para ~0,8 m/s com as pernas ja no clipe agachado, e o pe escorregava)
		// (puppet.aiCrouchBrake no fb_data, padrao 20 m/s2; 12-14 se o strafe de media distancia ficar seco)
		if (ch.crouch && LimiteMira && !holster && magnitude > Json.F(GameData.Section("puppet"), "aiCrouchMaxSpeed", 1.1f) * S)
		{
			num12 = Mathf.Max(num12, Json.F(GameData.Section("puppet"), "aiCrouchBrake", 20f));
		}
		float num13 = num10 - vel.x;
		float num14 = num11 - vel.z;
		float num15 = Mathf.Sqrt(num13 * num13 + num14 * num14);
		float num16 = num12 * deltaTime;
		if (num15 > num16)
		{
			vel.x += num13 / num15 * num16;
			vel.z += num14 / num15 * num16;
		}
		else
		{
			vel.x = num10;
			vel.z = num11;
		}
		jumpCd -= deltaTime;
		if (onGround && jumpCd <= 0f && num9 > 1.2f && magnitude > 1f)
		{
			Vector3 normalized = vel.Flat().normalized;
			if (FBWorld.RayHit(ch.Pos + Vector3.up * 0.35f * S, normalized, 0.85f * S, 0.05f) && !FBWorld.RayHit(ch.Pos + Vector3.up * 1.1f * S, normalized, 0.85f * S, 0.05f))
			{
				vel.y = Mathf.Sqrt(47.5f);
				onGround = false;
				airT = 0.08f;
				jumpCd = FBUtil.Rnd(3.5f, 6.5f);
			}
		}
		// fisica 31/10: fora de combate/cobertura e andando, o corpo segue a velocidade (bloco abaixo); girar aqui para o
		// destino final (vector4) brigava com isso e deixava o corpo ~20 graus de lado nas curvas do caminho
		if (vector4.HasValue && (ai == "combat" || ai == "cover" || magnitude <= 0.6f))
		{
			Vector3 valueOrDefault6 = vector4.GetValueOrDefault();
			float num17 = Mathf.Atan2(valueOrDefault6.x - ch.Pos.x, valueOrDefault6.z - ch.Pos.z);
			float num18 = ((ai == "combat" || ai == "cover") ? 8f : 4.5f) * deltaTime;
			YawRad += Mathf.Clamp(FBUtil.WrapA(num17 - YawRad), 0f - num18, num18);
		}
		if (ai != "combat" && ai != "cover" && magnitude > 0.6f)
		{
			YawRad += FBUtil.WrapA(Mathf.Atan2(vel.x, vel.z) - YawRad) * (1f - Mathf.Exp((0f - deltaTime) * 12f));
		}
		if ((ai == "combat" || ai == "cover") && (bool)target)
		{
			FBCharacter fBCharacter2 = target;
			float num19 = (ch.Pos - fBCharacter2.Pos).magnitude / 280f;
			float diffAcc = FBCombat.DiffAcc;
			float num20 = fBCharacter2.Pos.x + fBCharacter2.velocity.x * num19 * diffAcc;
			float num21 = Mathf.Atan2(x: fBCharacter2.Pos.z + fBCharacter2.velocity.z * num19 * diffAcc - ch.Pos.z, y: num20 - ch.Pos.x);
			float num22 = (8f + diffAcc * 5f) * deltaTime;
			aimYawRad += Mathf.Clamp(FBUtil.WrapA(num21 - aimYawRad), 0f - num22, num22);
			float num23 = ch.Pos.y + 1.7f * S;
			float num24 = fBCharacter2.Pos.y + (fBCharacter2.prone ? 0.45f : (fBCharacter2.crouch ? 1f : 1.4f)) * fBCharacter2.ScaleK;
			pitch = Mathf.Lerp(pitch, Mathf.Clamp(Mathf.Atan2(num24 - num23, (ch.Pos - fBCharacter2.Pos).magnitude), -0.65f, 0.65f), 1f - Mathf.Exp((0f - deltaTime) * 9f));
			ch.hasAim = true;
			ch.aimPoint = fBCharacter2.Pos + Vector3.up * (fBCharacter2.crouch ? 1f : 1.35f) * fBCharacter2.ScaleK;
		}
		else
		{
			aimYawRad = YawRad;
			pitch = Mathf.Lerp(pitch, 0f, 1f - Mathf.Exp((0f - deltaTime) * 4f));
			ch.hasAim = false;
		}
		stuckWin += deltaTime;
		if (stuckWin >= 0.6f)
		{
			float magnitude3 = (ch.Pos - lastPos).Flat().magnitude;
			if (num9 > 1f && vector2.HasValue && magnitude3 < 0.35f && !ch.special)
			{
				stuckN++;
				if (stuckN == 2)
				{
					path = null;
					navT = 0f;
				}
				else if (stuckN == 4)
				{
					// presos v4: fora da patrulha (combate, cobertura, fuga) o goalP não move o bot (o destino vem do strafe, da
					// cobertura ou da fuga): sai andando para um ponto livre
					// revisão: busca (lastSeen), reanimar (vai até o caído) e patrulha urgente (goalP trocado todo quadro) também
					// não andam pelo goalP; o desvio de lado só serve quando o destino deste quadro é o próprio goalP
					// revisão 2: pela origem do destino (busca com lastSeen igual ao goalP do alerta enganava a comparação de posição)
					bool peloGoal = Vigiado && destDoGoal && presoDest.HasValue && goalP.HasValue && (presoDest.Value - goalP.Value).sqrMagnitude < 0.01f;
					if (!peloGoal)
					{
						if (SaidaLivre(out var saida2))
						{
							Sai(saida2, 2, Vigiado ? "stuckN 4 (destino que não é o da patrulha)" : "stuckN 4 fora da patrulha");
						}
					}
					else
					{
						float num25 = ((UnityEngine.Random.value < 0.5f) ? 1 : (-1));
						// presos 01/11: lado sorteado batendo na borda do NavMesh (cerca, parede, quadra) e o outro livre -> vai pelo outro
						num25 = LadoLivre(num25);
						goalP = ch.Pos + new Vector3(Mathf.Cos(YawRad), 0f, 0f - Mathf.Sin(YawRad)) * DesvioLado * num25;
						goalT = 1.5f;
						path = null;
					}
				}
				else if (stuckN == 6)
				{
					path = null;
					goalP = PickPatrolGoal();
					goalT = FBUtil.Rnd(8f, 16f);
				}
				else if (stuckN >= 10)
				{
					// presos v4: o teleporte antigo só serve para bot fora do NavMesh (ponto do NavMesh a 0,75 m ou mais). Em cima
					// do NavMesh ele mexia 0,1 a 0,7 m e o bot travava de novo no mesmo lugar (C1 01/11: 29 vezes no Centro, bots
					// em combate; PT, borda da arena). Nesse caso sai andando para um ponto livre; sem ponto livre, usa a
					// realocação do vigia (teleporte só escondido da câmera).
					Vector3 presoDe = ch.Pos;
					bool flag9 = FBNav.Find(ch.Pos, 2.5f, out var at);
					bool espera = false;
					bool feito = false;
					bool longe = flag9 && (at - presoDe).Flat().magnitude >= 0.75f;
					if (longe)
					{
						// revisão: a regra antiga (câmera a 40 m ou 1 raio no peito) deixava o salto aparecer na luneta; agora é a
						// mesma do vigia (com zoom e tamanho do salto). Vendo, segue para a saída a pé / realocação abaixo.
						float salto0 = (at - presoDe).magnitude;
						if (Escondido(presoDe, salto0) && Escondido(at, salto0))
						{
							// presos 01/11: mesmo teleporte de antes; só conta e registra no log
							FBMatch.Teleport(ch, at, ch.Yaw);
							vel = Vector3.zero;
							Contar(2, "stuckN 10 (teleporte antigo para o NavMesh a até 2,5 m)", presoDe, at);
							// presos v4: a realocação pendente do vigia e a saída a pé eram do lugar antigo
							realocAlvo = null;
							saidaP = null;
							saidaAte = -99f;
							VigiaZera();
							feito = true;
						}
					}
					if (!feito && SaidaLivre(out var saida3))
					{
						Sai(saida3, 3, longe ? "stuckN 10 (salto para o NavMesh à vista da câmera)" : "stuckN 10 (em cima do NavMesh)");
						feito = true;
					}
					if (!feito && !realocAlvo.HasValue)
					{
						// revisão: a busca da realocação é pesada (caminho da âncora); 1 por quadro somando os bots, como no vigia.
						// Sem vaga, tenta de novo na próxima janela de 0,6 s.
						if (!PodePesado())
						{
							espera = true;
						}
						else if (AlvoRealocar(out var alvo2, out var tipo2, out var porque2))
						{
							// tipo 3 = realocação pedida pelo stuckN 10 (conta à parte; a saída de ilha continua tipo 0)
							Pendente(alvo2, (tipo2 == 1) ? 3 : tipo2, porque2 + " (stuckN 10 sem ponto livre ao lado)");
							TentaRealocar();
						}
					}
					if (espera)
					{
						stuckN = 9;
					}
					else
					{
						stuckN = 0;
						path = null;
						goalP = PickPatrolGoal();
					}
				}
			}
			else
			{
				stuckN = 0;
			}
			stuckWin = 0f;
			lastPos = ch.Pos;
		}
		// presos 01/11: vigia de progresso e ilha do NavMesh (ver Vigia); o stuckN acima continua como era
		Vigia(deltaTime, num9, presoDest);
		Move(deltaTime);
		Anim();
		if (ch.Pos.y < -120f)
		{
			ch.alive = false;
			UnityEngine.Object.Destroy(base.gameObject, 0.1f);
		}
		return;
		IL_0a9c:
		if (ai == "cover")
		{
			coverT -= deltaTime;
			peekT -= deltaTime;
			if (coverT <= 0f)
			{
				ai = (((bool)target && target.alive) ? "combat" : "patrol");
				ch.crouch = false;
			}
		}
		if (ai == "flee")
		{
			fleeT -= deltaTime;
			if (fleeT <= 0f)
			{
				float num26 = (target ? (ch.Pos - target.Pos).magnitude : 999f);
				fleeT = ((num26 > 24f || ch.hp > 50f) ? 0f : FBUtil.Rnd(2f, 4f));
				if (fleeT <= 0f)
				{
					ai = (((bool)target && target.alive) ? "combat" : "patrol");
				}
			}
		}
		vector2 = null;
		vector4 = null;
		num9 = 0f;
		bool flag4 = FBMatch.I.mode != null && FBMatch.I.mode.Urgent(ch);
		if (ai != "flee")
		{
			(Vector3?, float, Vector3?)? tuple = Revive(deltaTime);
			if (tuple.HasValue)
			{
				vector2 = tuple.Value.Item1;
				num9 = tuple.Value.Item2;
				vector4 = tuple.Value.Item3;
				ch.shooting = false;
				goto IL_18bd;
			}
		}
		if (ai == "guard")
		{
			if (!goalP.HasValue || (ch.Pos - goalP.Value).magnitude < 1.5f || strafeT <= 0f)
			{
				goalP = home + new Vector3(FBUtil.Rnd(-5f, 5f), 0f, FBUtil.Rnd(-5f, 5f));
				strafeT = FBUtil.Rnd(3f, 6f);
				pauseT = FBUtil.Rnd(1f, 2.5f);
			}
			strafeT -= deltaTime;
			if (pauseT > 0f)
			{
				pauseT -= deltaTime;
				vector4 = goalP;
			}
			else
			{
				vector2 = goalP;
				num9 = 2.4f;
				vector4 = goalP;
			}
		}
		else if (ai == "escort")
		{
			Vector3 valueOrDefault5 = escOff.GetValueOrDefault();
			if (!escOff.HasValue)
			{
				valueOrDefault5 = new Vector3(FBUtil.Rnd(-3f, 3f), 0f, FBUtil.Rnd(-3f, 3f)).normalized * FBUtil.Rnd(4f, 7f);
				escOff = valueOrDefault5;
			}
			FBCharacter player = FBMatch.Player;
			Vector3 vector5 = (((bool)player && player.alive) ? (player.Pos + escOff.Value) : home);
			float magnitude4 = (ch.Pos - vector5).magnitude;
			if (magnitude4 > 2.5f)
			{
				vector2 = vector5;
				num9 = ((magnitude4 > 12f) ? 5.2f : 4.2f);
				vector4 = vector5;
			}
			else if ((bool)player)
			{
				vector4 = player.Pos;
			}
		}
		else
		{
			if (ai == "cover")
			{
				Vector3? vector = coverPos;
				if (vector.HasValue)
				{
					Vector3 valueOrDefault7 = vector.GetValueOrDefault();
					if (!((ch.Pos - valueOrDefault7).magnitude < 2f))
					{
						vector2 = valueOrDefault7;
						num9 = 5f;
						vector4 = valueOrDefault7;
					}
					else
					{
						ch.crouch = true;
						vector4 = (target ? target.Pos : lastSeen.GetValueOrDefault(valueOrDefault7));
						if (peekT <= 0f && sees && (bool)target)
						{
							peekT = FBUtil.Rnd(2f, 3.8f);
							peeking = true;
							peekDur = FBUtil.Rnd(1f, 1.6f);
							peekT2 = 0f;
						}
						if (peeking)
						{
							peekT2 += deltaTime;
							ch.crouch = peekT2 > peekDur * 0.65f;
							if (peekT2 > peekDur)
							{
								peeking = false;
								ch.crouch = true;
							}
							if (!ch.crouch && sees && shotT <= 0f && (bool)target)
							{
								FireAtCadence(target, 3, 6, 0.4f, 0.9f);
							}
						}
						strafeT -= deltaTime;
						if (strafeT <= 0f)
						{
							strafeT = FBUtil.Rnd(1f, 2.5f);
							strafe = ((!(UnityEngine.Random.value < 0.5f)) ? 1 : (-1));
						}
						Vector3 normalized2 = ((target ? target.Pos : ch.Pos) - ch.Pos).Flat().normalized;
						Vector3 vector6 = new Vector3(normalized2.z, 0f, 0f - normalized2.x);
						vector2 = ch.Pos + vector6 * strafe * 1.5f;
						num9 = 1.6f;
					}
					goto IL_18bd;
				}
			}
			if (ai == "flee" && (bool)target)
			{
				ch.crouch = false;
				Vector3 normalized3 = (ch.Pos - target.Pos).Flat().normalized;
				vector2 = ch.Pos + normalized3 * 18f;
				num9 = 5.4f;
				vector4 = vector2;
			}
			else if (ai == "search")
			{
				searchT += deltaTime;
				Vector3 vector7 = lastSeen ?? ch.Pos;
				if (!((vector7 - ch.Pos).Flat().magnitude <= 2.2f) && !(searchT > 10f))
				{
					vector2 = vector7;
					num9 = 3.2f;
					vector4 = vector7;
					lookT = 0f;
				}
				else
				{
					lookT += deltaTime;
					float f = YawRad + Mathf.Sin(lookT * 1.4f) * 1.6f;
					vector4 = ch.Pos + new Vector3(Mathf.Sin(f), 0f, Mathf.Cos(f)) * 5f;
					if (lookT > 2.6f)
					{
						ai = "patrol";
						target = null;
						lastSeen = null;
						goalP = PickPatrolGoal();
						goalT = FBUtil.Rnd(10f, 20f);
					}
				}
			}
			else if (ai == "combat" && (bool)target)
			{
				FBCharacter fBCharacter3 = target;
				if (!fBCharacter3.alive)
				{
					ai = "patrol";
					target = null;
					lastSeen = null;
					goalP = PickPatrolGoal();
					goalT = FBUtil.Rnd(10f, 20f);
					ch.crouch = false;
				}
				else
				{
					float magnitude5 = (ch.Pos - fBCharacter3.Pos).magnitude;
					vector4 = fBCharacter3.Pos;
					strafeT -= deltaTime;
					float num27 = FBBotSense.EngageRange(wu.Cur);
					float num28 = Mathf.Min(prefDist, num27 * 0.8f);
					if (strafeT <= 0f)
					{
						strafeT = FBUtil.Rnd(0.4f, 1.1f);
						strafe = (new int[5] { -1, 1, -1, 1, 0 })[UnityEngine.Random.Range(0, 5)];
						ch.crouch = magnitude5 > 12f && UnityEngine.Random.value < 0.35f * (1.3f - aggr);
					}
					Vector3 normalized4 = (fBCharacter3.Pos - ch.Pos).Flat().normalized;
					Vector3 vector8 = new Vector3(normalized4.z, 0f, 0f - normalized4.x);
					Vector3 zero = Vector3.zero;
					if (magnitude5 > num27 * 1.05f)
					{
						zero += normalized4 * 1.8f;
						num9 = 4.8f;
						ch.crouch = false;
					}
					else if (magnitude5 > num28 * 1.3f)
					{
						zero += normalized4 * (0.6f + aggr * 0.6f) + vector8 * strafe * 0.8f;
						num9 = (ch.crouch ? 1.8f : 3.8f);
					}
					else if (magnitude5 < num28 * 0.5f)
					{
						zero += vector8 * strafe * 1.3f;
						zero -= normalized4 * (0.9f - aggr * 0.5f);
						num9 = (ch.crouch ? 1.6f : 3f);
					}
					else
					{
						zero += vector8 * strafe * 1.2f;
						if (UnityEngine.Random.value < 0.25f * aggr)
						{
							zero += normalized4 * 0.4f;
						}
						num9 = (ch.crouch ? 1.8f : 3.6f);
					}
					if (zero.sqrMagnitude > 0.01f)
					{
						vector2 = ch.Pos + zero.normalized * 4f;
					}
					if (!sees)
					{
						Vector3? vector = lastSeen;
						if (vector.HasValue)
						{
							Vector3 valueOrDefault8 = vector.GetValueOrDefault();
							vector2 = valueOrDefault8;
							num9 = 4.5f;
						}
					}
					if (sees && shotT <= 0f && magnitude5 <= num27 * 1.15f)
					{
						if (magnitude5 > 45f)
						{
							FireAtCadence(fBCharacter3, 2, 4, 0.5f, 1.1f);
						}
						else if (magnitude5 > 18f)
						{
							FireAtCadence(fBCharacter3, 3, 6, 0.32f, 0.85f);
						}
						else
						{
							FireAtCadence(fBCharacter3, 4, 8, 0.18f, 0.5f);
						}
					}
				}
			}
			else
			{
				if (!goalP.HasValue || goalT <= 0f || (ch.Pos - goalP.Value).magnitude < 2.5f || flag4)
				{
					goalP = ((!flag4) ? PickPatrolGoal() : (FBMatch.I.mode.UrgentGoal(ch) ?? FBMatch.I.mode.Zone?.c ?? ch.Pos));
					goalT = FBUtil.Rnd(10f, 20f);
					pauseT = (flag4 ? 0f : FBUtil.Rnd(0.6f, 2f));
				}
				goalT -= deltaTime;
				if (pauseT > 0f)
				{
					pauseT -= deltaTime;
					num9 = 0f;
					vector2 = null;
				}
				else
				{
					vector2 = goalP;
					num9 = (((goalP.HasValue ? (ch.Pos - goalP.Value).magnitude : 0f) > 6f) ? 6.2f : 4.2f);
					vector4 = vector2;
					// revisão 2: patrulha andando pelo goalP (urgente troca o goalP todo quadro: não conta)
					destDoGoal = !flag4;
				}
			}
		}
		goto IL_18bd;
		IL_08ea:
		if ((bool)target && target.alive && (ai == "combat" || ai == "cover"))
		{
			float magnitude6 = (ch.Pos - target.Pos).magnitude;
			if ((ch.hp < 45f || (wu.reloadT > 0f && magnitude6 < 26f)) && ai != "flee" && ai != "cover")
			{
				// presos 01/11: cobertura vetada por 4 s conta como "sem cobertura" (vida < 25 -> foge, como antes)
				Vector3? vector = ((Time.time >= coverVetoT) ? FindCover(target.Pos) : null);
				if (vector.HasValue)
				{
					Vector3 valueOrDefault9 = vector.GetValueOrDefault();
					coverPos = valueOrDefault9;
					ai = "cover";
					// v15: sai do combate em pé; o agachado que sobrou do combate fazia o bot ir para a cobertura a 1,1 m/s
					ch.crouch = false;
					coverT = FBUtil.Rnd(2.5f, 4.5f);
					peekT = 0f;
					peeking = false;
				}
				else if (ch.hp < 25f)
				{
					ai = "flee";
					fleeT = FBUtil.Rnd(3f, 5f);
				}
			}
			if (ch.hp < 18f && ai != "flee")
			{
				ai = "flee";
				fleeT = FBUtil.Rnd(3f, 5f);
				ch.crouch = false;
			}
		}
		goto IL_0a9c;
		IL_08b3:
		goalP = value2;
		lastSeen = null;
		goalT = FBUtil.Rnd(10f, 20f);
		ch.crouch = false;
		goto IL_08ea;
	}

	private void Anim()
	{
		// fisica 31/10: pernas usam legVel (deslocamento real limitado a vel), nao a velocidade pedida; bot travado nao corre parado
		float magnitude = legVel.magnitude;
		float num = (PassoReal ? (magnitude / Mathf.Max(0.01f, S)) : RemapSpeed(magnitude / Mathf.Max(0.01f, S)));
		Vector3 vector = Quaternion.Inverse(Quaternion.Euler(0f, ch.Yaw, 0f)) * legVel;
		float magnitude2 = vector.magnitude;
		ch.animVel = ((magnitude2 > 0.001f) ? new Vector2(vector.x / magnitude2 * num, vector.z / magnitude2 * num) : Vector2.zero);
		ch.lean = 0f;
	}

	private void KnockedStep(float dt)
	{
		FBCombat.KnockTick(ch, dt);
		if (ch.alive && ch.knocked)
		{
			FBCharacter fBCharacter = null;
			float num = 1E+09f;
			foreach (FBCharacter item in FBMatch.TeamOf(ch))
			{
				if (item.alive && !item.knocked)
				{
					float magnitude = (item.Pos - ch.Pos).magnitude;
					if (magnitude < num)
					{
						num = magnitude;
						fBCharacter = item;
					}
				}
			}
			float num2 = 0f;
			float num3 = 0f;
			if ((bool)fBCharacter && num > 2.5f && ch.revivedBy == null)
			{
				Vector3? vector = fBCharacter.Pos;
				if (!FBWorld.ClearLine(ch.Pos, fBCharacter.Pos) || num > 12f)
				{
					vector = Steer(vector, dt) ?? vector;
				}
				Vector3 vector2 = (vector.Value - ch.Pos).Flat();
				float magnitude2 = vector2.magnitude;
				if (magnitude2 > 0.2f)
				{
					num2 = vector2.x / magnitude2 * 1.5f;
					num3 = vector2.z / magnitude2 * 1.5f;
					YawRad += FBUtil.WrapA(Mathf.Atan2(num2, num3) - YawRad) * Mathf.Min(1f, dt * 4f);
				}
			}
			if (ch.revivedBy is FBCharacter fBCharacter2 && (bool)fBCharacter2 && Time.time - ch.reviveTick < 0.35f)
			{
				float num4 = fBCharacter2.Yaw * ((float)Math.PI / 180f) + (float)Math.PI / 2f;
				YawRad += FBUtil.WrapA(num4 - YawRad) * Mathf.Min(1f, dt * 3f);
			}
			vel.x = num2;
			vel.z = num3;
			aimYawRad = YawRad;
			ch.animVel = Vector2.zero;
			ch.sys = FBCombat.KoPose(ch, new Vector2(num2, num3).magnitude > 0.3f);
			ch.prone = true;
			ch.shooting = false;
		}
		Move(dt);
	}

	private void Move(float dt)
	{
		if (!cc || !cc.enabled)
		{
			legVel = vel.Flat(); // fisica 31/10: sem CC, pernas seguem a velocidade pedida
			legDef = Vector3.zero;
			ch.velocity = vel;
			return;
		}
		vel.y += (float)(onGround ? (-2) : (-19)) * dt;
		if (botStep < 0f)
		{
			botStep = cc.stepOffset;
		}
		Vector3 p0 = base.transform.position;
		FBMotor.Move(cc, vel * dt, onGround, vel.y > 0f, botStep, 0.45f * ch.ScaleK, ref botSlide, out onGround, out botGround);
		// fisica 31/10: legVel = velocidade pedida limitada ao deslocamento real do CC (bot travado em outro/poste/carro nao anima corrida parado)
		Vector3 pedida = vel.Flat();
		Vector3 bruta = pedida;
		if (dt > 1E-05f)
		{
			Vector3 real = (base.transform.position - p0) / dt;
			real.y = 0f;
			if (real.sqrMagnitude < bruta.sqrMagnitude)
			{
				bruta = real;
			}
			// deficit filtrado (ver legDef); legVel nunca passa da pedida nem aponta contra ela (bot travado freando nao anda de re)
			legDef = Vector3.Lerp(legDef, pedida - bruta, 1f - Mathf.Exp((0f - dt) / 0.08f));
		}
		legVel = Vector3.ClampMagnitude(pedida - legDef, pedida.magnitude);
		if (Vector3.Dot(legVel, pedida) < 0f)
		{
			legVel = Vector3.zero;
		}
		if (onGround && vel.y < 0f)
		{
			vel.y = 0f;
		}
		airT = (onGround ? 0f : (airT + dt));
		ch.velocity = new Vector3(legVel.x, vel.y, legVel.z); // fisica 31/10: publica o deslocamento real (limitado a vel)
		ch.grounded = onGround;
	}
}
}
