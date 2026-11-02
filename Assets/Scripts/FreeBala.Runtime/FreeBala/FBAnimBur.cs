using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
// Pegada do fuzil com as animações grátis BUR_ (Kevin Iglesias, pasta Assets/BURITIRO_Animacoes).
// O script de Editor BuritiroEditor.BuritiroAnimBUR.Aplicar cria no controlador dos personagens a camada
// "BUR Tronco" logo depois de "Upper Body", com peso 0 e máscara só de tronco, cabeça e braços (sem pernas,
// sem raiz, sem IK dos pés). Nela ficam os estados Vazio, Fuzil_Segurar, Fuzil_Mirar, Espingarda_Segurar e
// Espingarda_Mirar, que tocam os clipes BUR_ direto (sem AnimatorOverrideController).
// Este componente só sobe o peso dessa camada e escolhe o estado; as pernas continuam com os clipes do jogo
// (andar, correr, agachar, sprint), e o FBRig continua corrigindo mão, arma e coluna no LateUpdate do FBCharacter.
// DESLIGADO POR PADRÃO: sem a chave nada é criado e o jogo fica igual. Liga com:
//   linha de comando: -fbburanim (liga) · -fbburanim 0 (desliga) · -fbburanim semsprint (liga, menos no sprint)
//                     -fbburanim agachado (liga, também agachado)
//   fb_data.json (raiz): "burAnim": { "ligado": true, "sprint": true, "agachado": false, "classes": "fuzil;submetralhadora;escopeta;sniper" }
//   código/teste: FBAnimBur.Ligar(true)
// A linha de comando manda mais que o fb_data.
[DefaultExecutionOrder(900)]
public class FBAnimBur : MonoBehaviour
{
	public const string Camada = "BUR Tronco";

	// chave geral (o componente lê todo quadro; desligar faz o peso descer até 0)
	public static bool Ligado;

	// true: a pose BUR continua no sprint; false: no sprint volta o clipe do jogo
	public static bool Sprint = true;

	// false: agachado volta o clipe do jogo (com a máscara "com coluna" a camada pode mexer na altura do quadril;
	// ligar só depois de ver o teste agachado)
	public static bool Agachado;

	// classes de arma (WeaponDef.cls) que usam a camada; pistola e explosivo ficam de fora (a pistola é toda por IK no FBRig)
	public static string Classes = "fuzil;submetralhadora;escopeta;sniper";

	// velocidade de subida e descida do peso (por segundo) e tempo da troca segurar <-> mirar (s)
	public static float Sobe = 6f;
	public static float Desce = 12f;
	public static float Troca = 0.15f;

	// log do jogador a cada troca de estado (prova no log do .exe que a camada estava ligada em cada fase do teste);
	// v2: só liga com a chave -fbburanim na linha de comando (no jogo normal não escreve nada)
	public static bool LogJogador;

	// contadores para o log do teste
	public static int DbgComCamada;
	public static int DbgSemCamada;

	private static readonly int hVazio = Animator.StringToHash("Vazio");
	private static readonly int hFuzilSeg = Animator.StringToHash("Fuzil_Segurar");
	private static readonly int hFuzilMir = Animator.StringToHash("Fuzil_Mirar");
	private static readonly int hEspSeg = Animator.StringToHash("Espingarda_Segurar");
	private static readonly int hEspMir = Animator.StringToHash("Espingarda_Mirar");

	private static bool configLida;
	private static Varredor varredor;
	private static HashSet<string> classesSet;
	private static string classesDe;

	private FBCharacter ch;
	private FBWeaponUser wu;
	private Animator an;
	private int layer = -1;
	private int estado;
	private int alvoLog = -1;
	private float w;

	public float Peso => w;

	// ---------- liga/desliga e configuração ----------

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		LerConfig();
		if (Ligado)
		{
			CriarVarredor();
		}
	}

	public static void Ligar(bool on)
	{
		configLida = true;
		Ligado = on;
		if (on)
		{
			CriarVarredor();
		}
	}

	private static void LerConfig()
	{
		if (configLida)
		{
			return;
		}
		configLida = true;
		try
		{
			GameData.Load();
			Dictionary<string, object> c = Json.O(GameData.Root, "burAnim");
			if (c != null)
			{
				Ligado = Json.B(c, "ligado", Ligado);
				Sprint = Json.B(c, "sprint", Sprint);
				Agachado = Json.B(c, "agachado", Agachado);
				Classes = Json.S(c, "classes", Classes);
				Sobe = Json.F(c, "sobe", Sobe);
				Desce = Json.F(c, "desce", Desce);
				Troca = Json.F(c, "troca", Troca);
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[FreeBala][BUR] não li burAnim do fb_data: " + e.Message);
		}
		string[] a = Environment.GetCommandLineArgs();
		int i = Array.IndexOf(a, "-fbburanim");
		if (i >= 0)
		{
			string op = (i + 1 < a.Length && !a[i + 1].StartsWith("-")) ? a[i + 1].ToLowerInvariant() : "";
			Ligado = op != "0" && op != "nao" && op != "off";
			LogJogador = true;
			if (op == "semsprint")
			{
				Sprint = false;
			}
			else if (op == "agachado")
			{
				Agachado = true;
			}
		}
		Debug.Log($"[FreeBala][BUR] animações BUR na pegada: {(Ligado ? "LIGADO" : "desligado")} · sprint {Sprint} · agachado {Agachado} · classes {Classes}");
	}

	private static void CriarVarredor()
	{
		if ((bool)varredor)
		{
			return;
		}
		GameObject go = new GameObject("FB_AnimBur");
		go.hideFlags = HideFlags.HideInHierarchy;
		DontDestroyOnLoad(go);
		varredor = go.AddComponent<Varredor>();
	}

	// põe o componente nos personagens que têm a camada (a cada 0,5 s; personagens sem a camada, como pedestres
	// com outro controlador, ficam marcados e não são olhados de novo)
	private class Varredor : MonoBehaviour
	{
		private float prox;
		private readonly HashSet<FBCharacter> vistos = new HashSet<FBCharacter>();

		private void Update()
		{
			if (!Ligado || Time.unscaledTime < prox)
			{
				return;
			}
			prox = Time.unscaledTime + 0.5f;
			vistos.RemoveWhere((FBCharacter c) => !c);
			FBCharacter[] todos = FindObjectsByType<FBCharacter>(FindObjectsSortMode.None);
			foreach (FBCharacter c in todos)
			{
				if (!c || vistos.Contains(c))
				{
					continue;
				}
				Animator a = c.an ? c.an : c.GetComponent<Animator>();
				if (!a || !a.runtimeAnimatorController)
				{
					continue;
				}
				vistos.Add(c);
				int idx = a.GetLayerIndex(Camada);
				if (idx < 0)
				{
					DbgSemCamada++;
					continue;
				}
				if (!c.TryGetComponent<FBAnimBur>(out _))
				{
					c.gameObject.AddComponent<FBAnimBur>();
					DbgComCamada++;
					Debug.Log($"[FreeBala][BUR] {(c.isPlayer ? "jogador" : (c.isBot ? "bot" : "personagem"))} {c.displayName ?? c.name}: camada '{Camada}' no índice {idx} de {a.layerCount} · controlador {a.runtimeAnimatorController.name}");
				}
			}
		}
	}

	// ---------- por personagem ----------

	private void Start()
	{
		ch = GetComponent<FBCharacter>();
		wu = GetComponent<FBWeaponUser>();
		an = ch ? ch.an : GetComponent<Animator>();
		layer = an ? an.GetLayerIndex(Camada) : (-1);
	}

	private static bool ClasseOk(string cls)
	{
		if (cls == null)
		{
			return false;
		}
		if (classesSet == null || classesDe != Classes)
		{
			classesDe = Classes;
			classesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string s in (Classes ?? "").Split(';', ',', ' '))
			{
				if (s.Length > 0)
				{
					classesSet.Add(s.Trim());
				}
			}
		}
		return classesSet.Contains(cls);
	}

	// estado que a camada deve tocar agora (0 = nenhum: o peso desce)
	private int Alvo()
	{
		if (!Ligado || !ch || !ch.alive || ch.weapon == null || !ch.weaponGO || !ch.weaponGO.activeSelf)
		{
			return 0;
		}
		if (ch.UnarmedLocomotion || !ClasseOk(ch.weapon.cls))
		{
			return 0;
		}
		// reação a tiro, recarga, saque/guardar, granada e inspeção ficam com as camadas do jogo
		if (ch.HandsBusy || ch.IsDrawing || ch.reloading)
		{
			return 0;
		}
		// o estado "Reload Anim" só aparece no fim da transição; o tempo da recarga e da troca começa na hora
		if ((bool)wu && (wu.reloadT > 0f || wu.switching > 0f))
		{
			return 0;
		}
		// poses de corpo inteiro (vault, cura, dança, sentado, veículo, caído, finalização)
		if (ch.Special || ch.vehiclePose || ch.knocked || ch.prone || ch.seated || ch.sys != null || ch.dance != null || ch.DanW > 0.05f)
		{
			return 0;
		}
		if (ch.sprint && !Sprint)
		{
			return 0;
		}
		if (ch.crouch && !Agachado)
		{
			return 0;
		}
		bool esp = ch.weapon.cls == "escopeta" || ch.weapon.cls == "sniper";
		// depois do tiro (Hot, 3 s) fica mirando, menos correndo de sprint
		bool mira = ch.aiming || ch.shooting || (ch.Hot && !ch.sprint);
		if (esp)
		{
			return mira ? hEspMir : hEspSeg;
		}
		return mira ? hFuzilMir : hFuzilSeg;
	}

	private void Update()
	{
		if (!an || !an.isActiveAndEnabled || !an.runtimeAnimatorController)
		{
			// v2: com o Animator desligado (ragdoll, morte) o Rebind volta a camada ao estado padrão; ao voltar,
			// começa do zero (peso 0 e Play de novo), senão o peso subiria com a camada no estado vazio
			w = 0f;
			estado = 0;
			return;
		}
		if (layer < 0 || layer >= an.layerCount)
		{
			layer = an.GetLayerIndex(Camada);
			if (layer < 0)
			{
				return;
			}
		}
		float dt = Time.deltaTime;
		int alvo = Alvo();
		if (LogJogador && (bool)ch && ch.isPlayer && alvo != alvoLog)
		{
			alvoLog = alvo;
			Debug.Log($"[FreeBala][BUR] jogador: estado {Nome(alvo)} · arma {(ch.weapon != null ? ch.weapon.id + "/" + ch.weapon.cls : "nenhuma")} · peso agora {w:0.00} · mirando {ch.aiming} · sprint {ch.sprint} · agachado {ch.crouch}");
		}
		// v2: confere o estado de verdade da camada (Rebind, Play de outro script ou troca de controlador podem ter mudado)
		if (alvo != 0 && alvo == estado)
		{
			AnimatorStateInfo atual = an.GetCurrentAnimatorStateInfo(layer);
			if (atual.shortNameHash != alvo && (!an.IsInTransition(layer) || an.GetNextAnimatorStateInfo(layer).shortNameHash != alvo))
			{
				estado = -1;
			}
		}
		if (alvo != 0 && alvo != estado)
		{
			// com peso quase zero troca na hora (não mistura com o estado vazio); com peso, mistura curta
			if (w < 0.02f)
			{
				an.Play(alvo, layer, 0f);
			}
			else
			{
				an.CrossFadeInFixedTime(alvo, Troca, layer);
			}
			estado = alvo;
		}
		w = Mathf.MoveTowards(w, (alvo != 0) ? 1f : 0f, dt * ((alvo != 0) ? Sobe : Desce));
		if (w <= 0f && estado != 0)
		{
			an.Play(hVazio, layer, 0f);
			estado = 0;
		}
		an.SetLayerWeight(layer, w);
	}

	private static string Nome(int h)
	{
		if (h == hFuzilSeg) return "Fuzil_Segurar";
		if (h == hFuzilMir) return "Fuzil_Mirar";
		if (h == hEspSeg) return "Espingarda_Segurar";
		if (h == hEspMir) return "Espingarda_Mirar";
		return h == 0 ? "nenhum (peso desce)" : "Vazio";
	}

	private void OnDisable()
	{
		w = 0f;
		estado = 0;
		if ((bool)an && layer >= 0 && layer < an.layerCount)
		{
			an.SetLayerWeight(layer, 0f);
		}
	}
}
}
