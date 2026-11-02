// Teste automático do .exe (pode apagar depois). Só liga com "-fbtesteexe" na linha de comando; sem isso não faz nada.
// Parte 1 "arma": Vila Jardim no clássico, sem bots, anda/corre/sprint com cada fuzil e mede se a arma entra no corpo.
// Parte 2 "10x10": Menu > 10X10 > mapa, conta os 20 personagens na fase de compra, abre a loja, deixa a partida
// rodar sozinha e fotografa o placar no fim de cada rodada, com FPS de cada fase.
// Fotos e relatorio.txt ficam em <pasta do .exe>\teste_exe\.
// Parte 3 "perf": 10x10 com o jogador parado; no meio do combate alterna 5 s do jeito atual e 5 s com um ajuste de
// desempenho (sombra 80 m/3 cascatas, STP a 77%, lodBias 1,8, câmera até 700 m, FootIK até 25 m, sem sombra nos
// objetos pequenos, e tudo junto), 3 vezes cada, e mede a diferença.
// Uso: BURITIRO.exe -fbtesteexe [arma|10x10|tudo|perf|medir|deslize] [mapa] [rodadas] [time1] [camalta] [semculling] [9x9]
// "medir" (v12c): também mede a GPU olhando de cada base para a outra.
// "videoarma" [mapa] [arma] [sembur]: vídeo bruto de antes/depois da pegada (quadros JPG a 30 por segundo, vertical 1080x1920 e câmera do jogo).
// "deslize": mede se o pé apoiado no chão escorrega nos bots (antes/depois da correção) e grava vídeos de lado.
// "time1" põe o jogador (parado e sem arma) no time 1, para ver se a vantagem é do lado do mapa ou do time do jogador.
// "camalta": a câmera do jogo fica parada no alto do meio do mapa (olhando para baixo) em vez de atrás do jogador.
// "semculling": todos os Animator animam mesmo fora da tela, FootIK ignora a tela e a correção da arma no peito desliga.
// "9x9": no começo de cada rodada morre um bot do time contrário ao do jogador (o jogador parado não conta como um a mais).
// "piloto" (v18, 01/11): um bot (FBPiloto) joga com o NOSSO jogador pela entrada normal (WASD, mouse, tiro, mira, recarga,
// troca de arma, B da loja e cliques nos botões da loja) e anota preso, sem arma, compra que falhou, morte/queda e câmera
// na parede, com fotos da tela do jogador. Com piloto o 9x9 e a câmera no alto ficam desligados (fica 10 x 10).
// No 10x10 também mede o desempenho (CPU principal, render, GPU, tempo de cada parte do quadro) e grava desempenho.csv.
// v13: o 10x10 vai até um time chegar a WIN vitórias (lido do FBModo10x10, padrão 7) e fotografa a tela final; em cada
// rodada: foto da compra sem a loja, foto de cima das duas bases, foto do combate e do primeiro tiroteio (de trás de
// quem atira), como a rodada acabou (eliminação ou tempo), armas por classe no começo e as linhas "bases:" do modo.
// v13b: separa as rodadas que só foram ao tempo porque o jogador parado (sem arma) ficou vivo depois dos bots do time.
// v14: a cada 15 s de combate, lista os bots que andaram menos de 6 m sem estar vendo inimigo (posição, destino, estado do
// caminho no NavMesh e se a base do time chega até eles) e fotografa de cima o primeiro de cada rodada.
// v15 (presos 01/11): em cada rodada e na soma final, quantos bots o FBBot realocou (FBBot.Realocados, zerado no começo
// de cada rodada, e FBBot.RealocadosPor: ilha do NavMesh, vigia de progresso, teleporte antigo do stuckN 10) e as linhas
// "[BURITIRO] presos: ..." do log (bot, de onde, para onde, por quê). O contador é lido por reflexão: sem o conserto dos
// presos no FreeBala o teste compila igual e escreve "sem contador".
// v16 (presos 01/11): também a prevenção do conserto (FBBot.Evitados: destinos de patrulha/busca fora da ilha principal
// do NavMesh desviados para o ponto da ilha principal mais perto e coberturas fora dela canceladas), por rodada e na
// soma final, também por reflexão ("sem contador" com FreeBala sem a v2 do conserto).
// v17 (presos v4, 01/11): a prevenção mostra também as saídas a pé (FBBot.Evitados[2] stuckN 4 fora da patrulha e [3]
// stuckN 10 em cima do NavMesh, no lugar do teleporte de 0,1-0,7 m) e as linhas "[BURITIRO] arena 10x10" do recorte do
// NavMesh. O "medir" faz, depois das 2 medições de base, um A/B de GPU em cada base com a partida congelada e o jogador
// imortal: para cada grupo do mapa (os 5 maiores Map_*, os de água e os pássaros) mede 3 s com ele e 3 s sem ele (1 s para
// assentar antes de cada) e religa. Também escreve a água do mapa como o jogo carregou (malha, altura real dos vértices,
// lote estático, material, shader) e tira foto das 2 maiores águas. Conta à parte a realocação escondida pedida pelo
// stuckN 10 (FBBot.RealocadosPor[3]).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FreeBala;
using Unity.Profiling;
using UnityEngine.AI;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class FBTesteExe : MonoBehaviour
{
	string parte = "tudo";
	string mapa = "vila_jardim";
	int rodadas = 3;
	int timeJog = 0;
	bool camAltaOpt, semCulling, opt9x9, optPiloto;
	string dir;
	Camera fotoCam;
	FBInputState? entrada;
	bool trocarVista;
	readonly StringBuilder rel = new StringBuilder();
	int erros;
	readonly List<string> errosTxt = new List<string>();
	readonly List<string> linhasModo = new List<string>(); // v13: linhas "[BURITIRO] 10x10 rodada N ..." do modo
	readonly List<string> linhasPresos = new List<string>(); // v15: linhas "[BURITIRO] presos: ..." do FBBot
	int presosPerdidos; // v15: linhas de presos que não couberam na lista

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void Boot()
	{
		var a = Environment.GetCommandLineArgs();
		int i = Array.IndexOf(a, "-fbtesteexe");
		if (i < 0) return;
		var go = new GameObject("FB_TesteExe");
		DontDestroyOnLoad(go);
		var t = go.AddComponent<FBTesteExe>();
		if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) t.parte = a[i + 1];
		if (i + 2 < a.Length && !a[i + 2].StartsWith("-")) t.mapa = a[i + 2];
		if (i + 3 < a.Length && int.TryParse(a[i + 3], out int r)) t.rodadas = Mathf.Max(1, r);
		if (t.parte == "videoarma" && i + 3 < a.Length && !a[i + 3].StartsWith("-") && !int.TryParse(a[i + 3], out _)) t.armaVideo = a[i + 3];
		for (int k = i + 4; k < a.Length && !a[k].StartsWith("-"); k++)
		{
			if (a[k] == "time1") t.timeJog = 1;
			if (a[k] == "camalta") t.camAltaOpt = true;
			if (a[k] == "semculling") t.semCulling = true;
			if (a[k] == "9x9") t.opt9x9 = true;
			if (a[k] == "piloto") t.optPiloto = true;
			if (a[k] == "sembur") t.videoSemBur = true;
		}
	}

	void Log(string s)
	{
		Debug.Log("[FreeBala][testeexe] " + s);
		rel.AppendLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
		try { File.WriteAllText(Path.Combine(dir, "relatorio.txt"), rel.ToString()); } catch { }
	}

	IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime) yield return null;
	}

	// foto da tela do jogo (câmera do jogo + HUD)
	IEnumerator Tela(string nome)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, nome + ".png"));
		yield return null;
		yield return null;
	}

	IEnumerator Start()
	{
		dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "teste_exe");
		Directory.CreateDirectory(dir);
		Application.logMessageReceived += (c, st, ty) =>
		{
			if (ty == LogType.Log && c != null && (c.StartsWith("[BURITIRO] 10x10 rodada") || c.StartsWith("[BURITIRO] arena 10x10")) && linhasModo.Count < 600) { linhasModo.Add(c.Split('\n')[0]); return; }
			if (ty == LogType.Log && c != null && c.StartsWith("[BURITIRO] presos:")) { if (linhasPresos.Count < 400) linhasPresos.Add(c.Split('\n')[0]); else presosPerdidos++; return; }
			if (ty != LogType.Exception && ty != LogType.Error) return;
			erros++;
			string l = c.Split('\n')[0];
			if (errosTxt.Count < 8 && !errosTxt.Contains(l)) errosTxt.Add(l);
		};
		Log($"início · parte {parte} · mapa {mapa} · rodadas {rodadas} · jogador no time {timeJog}{(camAltaOpt ? " · câmera no alto" : "")}{(semCulling ? " · sem culling" : "")}{(opt9x9 ? " · 9x9" : "")}{(optPiloto ? " · piloto (bot jogando com o nosso jogador)" : "")} · tela {Screen.width}x{Screen.height} · {SystemInfo.graphicsDeviceName} · {SystemInfo.processorType} ({SystemInfo.processorCount} núcleos)");
		yield return Wait(4f);
		yield return Tela("0_menu");
		GameData.Load();
		FBSession.Load();
		var keep = (FBSession.map, FBSession.mode, FBSession.view, FBSession.team);
		fotoCam = new GameObject("FB_FotoCamExe").AddComponent<Camera>();
		DontDestroyOnLoad(fotoCam.gameObject);
		fotoCam.enabled = false;
		fotoCam.nearClipPlane = 0.05f;
		fotoCam.fieldOfView = 50f;
		if (parte == "arma" || parte == "tudo") yield return Arma();
		if (parte == "10x10" || parte == "tudo") yield return Modo10x10();
		if (parte == "perf") yield return PerfAjustes();
		if (parte == "medir") yield return Medir();
		if (parte == "deslize") yield return Deslize();
		if (parte == "videoarma") yield return VideoArma();
		FBPlayer.TestInput = null;
		FBMatch.God = false;
		(FBSession.map, FBSession.mode, FBSession.view, FBSession.team) = keep;
		FBSession.Save();
		Log($"fim · erros/exceções no log: {erros}" + (errosTxt.Count > 0 ? " · " + string.Join(" | ", errosTxt) : ""));
		yield return Wait(0.5f);
		Application.Quit(0);
	}

	IEnumerator Carregar(string modo)
	{
		FBSession.map = mapa;
		FBSession.mode = modo;
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		Log($"carregando Map_{mapa} no modo {modo}");
		var op = SceneManager.LoadSceneAsync("Map_" + mapa);
		if (op == null) { Log($"FALHA: cena Map_{mapa} não existe"); yield break; }
		while (!op.isDone) yield return null;
		for (float w = 0f; w < 30f && !(FBMatch.I && FBMatch.Player && FBPlayer.I && FBPlayer.I.wu); w += Time.unscaledDeltaTime) yield return null;
		if (Camera.main) fotoCam.cullingMask = Camera.main.cullingMask;
	}

	// ---------- parte 1: arma na mão andando, correndo e no sprint, no .exe ----------
	IEnumerator Arma()
	{
		yield return Carregar("classico");
		var me = FBMatch.Player;
		var P = FBPlayer.I;
		if (!me || !P) { Log("arma: sem jogador"); yield break; }
		yield return Wait(3f);
		EstadoGrafico("arma, depois de carregar");
		FBMatch.God = true;
		foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
		FBPlayer.TestInput = inp =>
		{
			var e = entrada ?? default;
			e.toggleView = trocarVista;
			trocarVista = false;
			return e;
		};
		if (FBVehicles.CarOf(me)) FBDrive.Exit(me, force: true);
		if (P.firstPerson) { trocarVista = true; yield return Wait(0.5f); }
		var an = me.an;
		var peito = an ? an.GetBoneTransform(HumanBodyBones.Chest) : me.transform;
		var cc = me.GetComponent<CharacterController>();
		// vira o boneco para o lado mais livre (na primeira versão ele corria contra a parede do spawn)
		Vector3 p0 = me.Pos;
		float yaw0 = me.Yaw, livre = 0f;
		for (int k = 0; k < 24; k++)
		{
			float a = k * 15f;
			var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
			var o = p0 + Vector3.up * 1.2f + d * 0.6f;
			float dist = 0f;
			for (int sx = -1; sx <= 1; sx++)
			{
				var o2 = o + Vector3.Cross(Vector3.up, d) * (0.5f * sx);
				float h = Physics.Raycast(o2, d, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 30f;
				dist = sx == -1 ? h : Mathf.Min(dist, h);
			}
			if (dist > livre) { livre = dist; yaw0 = a; }
		}
		Log($"arma: virado para {yaw0:0}°, {livre:0} m livres à frente");
		var fases = new (string nome, float dur, FBInputState inp)[]
		{
			("parado", 1.5f, new FBInputState()),
			("andando", 1.6f, new FBInputState { move = new Vector2(0f, 0.5f) }),
			("correndo", 1.6f, new FBInputState { move = new Vector2(0f, 1f) }),
			("sprint", 1.6f, new FBInputState { move = new Vector2(0f, 1f), sprintHeld = true }),
			("mirando", 1.6f, new FBInputState { aim = true }),
			("mirando_andando", 1.6f, new FBInputState { aim = true, move = new Vector2(0.5f, 0.3f) }),
			("parou", 1.2f, new FBInputState()),
			// v10: tiro do quadril (sem mirar): o cano tem que ficar na linha do tiro, não na patrulha
			("atirando", 1.2f, new FBInputState { fire = true, firePressed = true }),
			// depois do tiro, correndo de sprint até o combate esfriar (3 s): a patrulha entra com o braço esticado
			("sprint_pos_tiro", 4.0f, new FBInputState { move = new Vector2(0f, 1f), sprintHeld = true }),
		};
		var fimQuadro = new WaitForEndOfFrame();
		int totDentro = 0, totQuadros = 0;
		var bArm = an ? an.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
		var bFore = an ? an.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
		var bMao = an ? an.GetBoneTransform(HumanBodyBones.RightHand) : null;
		var lArmB = an ? an.GetBoneTransform(HumanBodyBones.LeftUpperArm) : null;
		var lForeB = an ? an.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
		var lMaoB = an ? an.GetBoneTransform(HumanBodyBones.LeftHand) : null;
		float ombro0 = FBRig.OmbroLivre;
		var pegada0 = (FBRig.CabecaFuzil, FBRig.CabecaMira, FBRig.Patrulha, FBRig.Desvirar);
		// v11 (pegada do fuzil v17): "antes" = tudo desligado, como na v15; "jogo" = os valores que o jogo usa agora
		// (FBRig v17: cabeça olha a mira, patrulha só correndo e fora da escopeta)
		var modosArma = new (string nome, float cab, float cabMira, float pat, float desv)[]
		{
			("antes", 0f, 0f, 0f, 0f),
			("jogo", pegada0.Item1, pegada0.Item2, pegada0.Item3, pegada0.Item4),
		};
		foreach (var id in new[] { "ak", "m4", "remington_870", "p90", "barret_50", "rpg_7" })
		{
			if (!GameData.Weapons.ContainsKey(id) || !FBRegistry.I.Weapon(id)) { Log($"arma {id}: sem modelo"); continue; }
			foreach (var modo in modosArma)
			{
				FBRig.OmbroLivre = 0f;
				FBRig.CabecaFuzil = modo.cab;
				FBRig.CabecaMira = modo.cabMira;
				FBRig.Patrulha = modo.pat;
				FBRig.Desvirar = modo.desv;
				entrada = new FBInputState();
				if (cc) cc.enabled = false;
				me.transform.position = p0;
				me.Yaw = yaw0;
				if (cc) cc.enabled = true;
				P.OnRespawn(yaw0);
				P.wu.Strip();
				P.wu.Give(id);
				int s = Array.FindIndex(P.wu.slots, x => x != null && x.id == id);
				P.wu.Equip(s >= 0 ? s : 0, instant: true);
				// o tiro da variante anterior não pode deixar esta em combate (Hot dura 3 s)
				me.lastShot = 9f; me.shooting = false; me.combatHold = 0f;
				yield return Wait(0.8f);
				var linha = new StringBuilder();
				float giroAnt = float.NaN;
				int giroQuadro = -1;
				// giro do cotovelo direito em volta da linha ombro-mão
				bool Giro(out float g)
				{
					g = 0f;
					if (!bArm || !bFore || !bMao) return false;
					Vector3 eixo = bMao.position - bArm.position;
					if (eixo.sqrMagnitude < 1e-6f) return false;
					Vector3 cot = Vector3.ProjectOnPlane(bFore.position - bArm.position, eixo);
					Vector3 refd = Vector3.ProjectOnPlane(Vector3.down, eixo);
					if (cot.sqrMagnitude < 1e-8f || refd.sqrMagnitude < 1e-8f) return false;
					g = Vector3.SignedAngle(refd, cot, eixo);
					return true;
				}
				foreach (var f in fases)
				{
					if ((f.nome == "atirando" || f.nome == "sprint_pos_tiro") && id == "rpg_7") continue;
					// um quadro ainda com a entrada antiga para engolir o engasgo da foto; o salto da troca de entrada
					// fica entre dois quadros seguidos
					if (giroQuadro >= 0)
					{
						yield return fimQuadro;
						if (Giro(out var g0)) { giroAnt = g0; giroQuadro = Time.frameCount; }
					}
					entrada = f.inp;
					int dentro = 0, dentroSem = 0, quadros = 0;
					float pior = 0f, piorSem = 0f, somaCor = 0f, somaPitch = 0f, somaCotD = 0f, somaCotE = 0f, somaDist = 0f;
					float somaTronco = 0f, somaCabeca = 0f, somaDesvio = 0f, saltoCot = 0f, dtSalto = 0f;
					if (me.rig) me.rig.DbgPatrulhaMov = 0f;
					for (float t = 0f; t < f.dur; t += Time.deltaTime)
					{
						yield return fimQuadro;
						var w = me.weaponGO;
						if (!w || !w.activeInHierarchy) continue;
						float fundo = Fundo(me, w, false);
						float fundoSem = Fundo(me, w, true);
						quadros++;
						if (fundo > 0.02f * me.ScaleK) dentro++;
						if (fundoSem > 0.02f * me.ScaleK) dentroSem++;
						pior = Mathf.Max(pior, fundo);
						piorSem = Mathf.Max(piorSem, fundoSem);
						Vector3 bar = me.BarrelDir();
						Vector3 coronha = Coronha(me, w);
						Vector3 bolso = BolsoOmbro(me, bArm, me.Chest);
						somaCor += (coronha - bolso).magnitude / Mathf.Max(0.01f, me.ScaleK);
						somaPitch += Mathf.Asin(Mathf.Clamp(bar.y, -1f, 1f)) * Mathf.Rad2Deg;
						if (bArm && bFore && bMao) somaCotD += Vector3.Angle(bArm.position - bFore.position, bMao.position - bFore.position);
						if (lArmB && lForeB && lMaoB) somaCotE += Vector3.Angle(lArmB.position - lForeB.position, lMaoB.position - lForeB.position);
						if (bMao && peito) somaDist += Vector3.Dot(bMao.position - peito.position, me.transform.forward) / Mathf.Max(0.01f, me.ScaleK);
						// tronco: ângulo (de cima) entre a frente do peito e o cano; 0 = peito de frente para o cano
						me.TorsoAxes(out _, out var tfw, out _);
						Vector3 tH = Vector3.ProjectOnPlane(tfw, Vector3.up), bH = Vector3.ProjectOnPlane(bar, Vector3.up);
						if (tH.sqrMagnitude > 1e-6f && bH.sqrMagnitude > 1e-6f) somaTronco += Mathf.Abs(Vector3.SignedAngle(tH, bH, Vector3.up));
						// cabeça: inclinação do olhar (negativo = olhando para baixo)
						if (me.Head && me.HeadAxes(out var hfL, out _))
						{
							Vector3 hf = me.Head.TransformDirection(hfL).normalized;
							somaCabeca += Mathf.Asin(Mathf.Clamp(hf.y, -1f, 1f)) * Mathf.Rad2Deg;
							// desvio (de cima) entre o olhar e o cano: mirando tem que ficar perto de 0
							Vector3 hH = Vector3.ProjectOnPlane(hf, Vector3.up);
							if (hH.sqrMagnitude > 1e-6f && bH.sqrMagnitude > 1e-6f) somaDesvio += Mathf.Abs(Vector3.SignedAngle(hH, bH, Vector3.up));
						}
						// o maior salto do giro do cotovelo entre dois quadros seguidos mostra estalo
						if (Giro(out var giro))
						{
							if (!float.IsNaN(giroAnt) && Time.frameCount == giroQuadro + 1)
							{
								float sj = Mathf.Abs(Mathf.DeltaAngle(giroAnt, giro));
								if (sj > saltoCot) { saltoCot = sj; dtSalto = Time.deltaTime; }
							}
							giroAnt = giro;
							giroQuadro = Time.frameCount;
						}
					}
					totDentro += dentroSem;
					totQuadros += quadros;
					if (f.nome != "parou")
					{
						Foto(me, $"arma_{id}_{f.nome}_{modo.nome}_lado", peito, "lado");
						if ((id == "ak" || id == "m4") && (f.nome == "correndo" || f.nome == "parado" || f.nome == "mirando" || f.nome == "andando")) yield return Tela($"arma_{id}_{f.nome}_{modo.nome}_jogo");
					}
					float q = Mathf.Max(1, quadros);
					linha.Append($" | {f.nome}: coronha a {somaCor / q * 100f:0} cm do ombro · cano {somaPitch / q:0}° · cotovelo dir {somaCotD / q:0}° esq {somaCotE / q:0}° · mão {somaDist / q * 100f:0} cm à frente do peito · no peito sem contar o ombro {dentroSem}/{quadros} (fundo {piorSem * 100f:0} cm), contando {dentro} ({pior * 100f:0} cm) · tronco {somaTronco / q:0}° do cano · olhar {somaCabeca / q:0}° · olhar fora do cano {somaDesvio / q:0}° · maior salto do cotovelo {saltoCot:0}° (quadro de {dtSalto * 1000f:0} ms) · mão puxada até {(me.rig ? me.rig.DbgPatrulhaMov * 100f : 0f):0} cm");
				}
				Log($"arma {id} [{modo.nome}]{linha}");
			}
		}
		FBRig.OmbroLivre = ombro0;
		(FBRig.CabecaFuzil, FBRig.CabecaMira, FBRig.Patrulha, FBRig.Desvirar) = pegada0;
		entrada = new FBInputState();
		Log($"arma: total {totDentro} de {totQuadros} quadros com a arma mais de 2 cm dentro do peito/cabeça (sem contar a coronha no ombro)");
		EstadoGrafico("arma, no fim");
		FBPlayer.TestInput = null;
		FBMatch.God = false;
	}

	// ---------- parte "videoarma": vídeo bruto de antes/depois da pegada, para o Instagram ----------
	// Mesmo lugar, mesma arma, mesma câmera e a mesma entrada nas duas variantes. Durante a gravação o tempo do jogo anda
	// exatamente 1/30 s por quadro (Time.captureFramerate), então o vídeo sai liso mesmo com a gravação lenta.
	// Pastas em teste_exe\: video_<variante>_<fase>_perto\00000.jpg (1080x1920, personagem inteiro de frente em 3/4, pelo
	// lado da arma) e video_<variante>_<fase>_jogo\ (1920x1080, a câmera do jogo, sem HUD), mais video_quadros.csv.
	// "antes" = pegada antiga (chaves do FBRig desligadas, sem as animações BUR); "depois" = como o jogo está agora, com as
	// animações BUR ligadas se este build tiver o FBAnimBur.
	string armaVideo = "ak";
	bool videoSemBur;
	const int FpsVideo = 30;

	static bool BurAnim(bool on, out string msg)
	{
		var tp = typeof(FBRig).Assembly.GetType("FreeBala.FBAnimBur");
		var m = tp != null ? tp.GetMethod("Ligar", BindingFlags.Public | BindingFlags.Static) : null;
		if (m == null) { msg = "sem FBAnimBur neste build"; return false; }
		try { m.Invoke(null, new object[] { on }); msg = on ? "BUR ligado" : "BUR desligado"; return true; }
		catch (Exception e) { msg = "FBAnimBur.Ligar falhou: " + (e.InnerException ?? e).Message; return false; }
	}

	static float PesoBur(FBCharacter me)
	{
		var tp = typeof(FBRig).Assembly.GetType("FreeBala.FBAnimBur");
		if (tp == null || !me) return -1f;
		var c = me.GetComponent(tp);
		var p = tp.GetProperty("Peso");
		return c && p != null ? (float)p.GetValue(c) : -1f;
	}

	IEnumerator VideoArma()
	{
		yield return Carregar("classico");
		var me = FBMatch.Player;
		var P = FBPlayer.I;
		if (!me || !P) { Log("videoarma: sem jogador"); yield break; }
		string id = armaVideo;
		if (!GameData.Weapons.ContainsKey(id) || !FBRegistry.I.Weapon(id)) { Log($"videoarma: arma {id} sem modelo, usando ak"); id = "ak"; }
		yield return Wait(3f);
		FBMatch.God = true;
		foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
		FBPlayer.TestInput = inp =>
		{
			var e = entrada ?? default;
			e.toggleView = trocarVista;
			trocarVista = false;
			return e;
		};
		if (FBVehicles.CarOf(me)) FBDrive.Exit(me, force: true);
		if (P.firstPerson) { trocarVista = true; yield return Wait(0.5f); }
		var cc = me.GetComponent<CharacterController>();
		// lado mais livre (o sprint anda uns 20 m)
		Vector3 p0 = me.Pos;
		float yaw0 = me.Yaw, livre = 0f;
		for (int k = 0; k < 24; k++)
		{
			float a = k * 15f;
			var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
			var o = p0 + Vector3.up * 1.2f + d * 0.6f;
			float dist = 0f;
			for (int sx = -1; sx <= 1; sx++)
			{
				var o2 = o + Vector3.Cross(Vector3.up, d) * (0.5f * sx);
				float h = Physics.Raycast(o2, d, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 40f;
				dist = sx == -1 ? h : Mathf.Min(dist, h);
			}
			if (dist > livre) { livre = dist; yaw0 = a; }
		}
		Log($"videoarma: arma {id} · virado para {yaw0:0}°, {livre:0} m livres à frente{(livre < 22f ? " (POUCO: o sprint pode bater)" : "")}");
		var fases = new (string nome, float dur, float pre, FBInputState inp)[]
		{
			("parado", 3.0f, 0f, new FBInputState()),
			("andando", 3.0f, 0.4f, new FBInputState { move = new Vector2(0f, 0.5f) }),
			("correndo", 3.0f, 0.4f, new FBInputState { move = new Vector2(0f, 1f) }),
			("sprint", 2.5f, 0.4f, new FBInputState { move = new Vector2(0f, 1f), sprintHeld = true }),
			("mirando", 3.0f, 0.3f, new FBInputState { aim = true }),
			("mirando_andando", 3.0f, 0.4f, new FBInputState { aim = true, move = new Vector2(0.5f, 0.3f) }),
			("atirando", 2.5f, 0f, new FBInputState { aim = true, fire = true, firePressed = true }),
		};
		var pegada0 = (FBRig.CabecaFuzil, FBRig.CabecaMira, FBRig.Patrulha, FBRig.Desvirar);
		var variantes = new (string nome, bool novo)[] { ("antes", false), ("depois", true) };
		// câmera do vídeo: com pós-processamento e anti-serrilhado, igual à do jogo
		var camJogo = Camera.main;
		var urpV = fotoCam.GetComponent<UniversalAdditionalCameraData>() ?? fotoCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
		urpV.renderPostProcessing = true;
		urpV.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
		urpV.antialiasingQuality = AntialiasingQuality.High;
		var camJ = new GameObject("FB_VideoCamJogo").AddComponent<Camera>();
		camJ.enabled = false;
		var urpJ = camJ.gameObject.AddComponent<UniversalAdditionalCameraData>();
		urpJ.renderPostProcessing = true;
		urpJ.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
		urpJ.antialiasingQuality = AntialiasingQuality.High;
		var rtP = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
		var rtJ = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
		var texP = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
		var texJ = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
		float lod0 = QualitySettings.lodBias;
		float fov0 = fotoCam.fieldOfView, near0 = fotoCam.nearClipPlane;
		var csv = new StringBuilder("variante;fase;quadro;tempo_s;velocidade_m_s;mirando;atirando;peso_bur;cotovelo_dir\n");
		var bArm = me.an ? me.an.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
		var bFore = me.an ? me.an.GetBoneTransform(HumanBodyBones.RightLowerArm) : null;
		var bMao = me.an ? me.an.GetBoneTransform(HumanBodyBones.RightHand) : null;
		var bCab = me.an ? me.an.GetBoneTransform(HumanBodyBones.Head) : null;
		int totalQ = 0;
		try
		{
			QualitySettings.lodBias = Mathf.Max(lod0, 4f);
			fotoCam.fieldOfView = 30f;
			fotoCam.nearClipPlane = 0.05f;
			foreach (var v in variantes)
			{
				if (v.novo) (FBRig.CabecaFuzil, FBRig.CabecaMira, FBRig.Patrulha, FBRig.Desvirar) = pegada0;
				else { FBRig.CabecaFuzil = 0f; FBRig.CabecaMira = 0f; FBRig.Patrulha = 0f; FBRig.Desvirar = 0f; }
				BurAnim(v.novo && !videoSemBur, out string msgBur);
				Log($"videoarma [{v.nome}]: cabeça {FBRig.CabecaFuzil:0.##}/{FBRig.CabecaMira:0.##} · patrulha {FBRig.Patrulha:0.##} · desvirar {FBRig.Desvirar:0.##} · {msgBur}");
				foreach (var f in fases)
				{
					entrada = new FBInputState();
					Time.captureFramerate = 0;
					if (cc) cc.enabled = false;
					me.transform.position = p0;
					me.Yaw = yaw0;
					if (cc) cc.enabled = true;
					P.OnRespawn(yaw0);
					P.wu.Strip();
					P.wu.Give(id);
					int s = Array.FindIndex(P.wu.slots, x => x != null && x.id == id);
					P.wu.Equip(s >= 0 ? s : 0, instant: true);
					me.lastShot = 9f; me.shooting = false; me.combatHold = 0f;
					yield return Wait(1.0f);
					// altura do boneco (cabeça até o chão) para enquadrar o corpo inteiro na vertical
					float H = bCab ? Mathf.Max(0.5f, bCab.position.y - me.transform.position.y + 0.15f * me.ScaleK) : 1.8f * me.ScaleK;
					Time.captureFramerate = FpsVideo;
					entrada = f.inp;
					int pre = Mathf.RoundToInt(f.pre * FpsVideo), n = Mathf.RoundToInt(f.dur * FpsVideo);
					string pasta = $"video_{v.nome}_{f.nome}";
					string dP = Path.Combine(dir, pasta + "_perto"), dJ = Path.Combine(dir, pasta + "_jogo");
					Directory.CreateDirectory(dP);
					Directory.CreateDirectory(dJ);
					Vector3 c0 = me.transform.position;
					var rotV = Quaternion.Euler(0f, yaw0, 0f);
					Vector3 fw = rotV * Vector3.forward, rt = rotV * Vector3.right;
					// 3/4 de frente, pelo lado direito (o lado da arma); distância para o corpo ocupar uns 75% da altura
					float D = 0.675f * H / Mathf.Tan(15f * Mathf.Deg2Rad);
					for (int q = -pre; q < n; q++)
					{
						yield return new WaitForEndOfFrame();
						Vector3 c = me.transform.position;
						if (q < 0) { c0 = c; continue; }
						Vector3 alvo = new Vector3(c.x, me.transform.position.y + H * 0.5f, c.z);
						fotoCam.transform.position = alvo + (fw * 0.72f + rt * 0.69f).normalized * D + Vector3.up * H * 0.08f;
						fotoCam.transform.LookAt(alvo);
						RenderPara(fotoCam, rtP, texP, Path.Combine(dP, q.ToString("00000") + ".jpg"));
						var cm = Camera.main;
						if (cm)
						{
							camJ.CopyFrom(cm);
							camJ.enabled = false;
							camJ.targetTexture = null;
							camJ.transform.SetPositionAndRotation(cm.transform.position, cm.transform.rotation);
							camJ.aspect = 1920f / 1080f;
							RenderPara(camJ, rtJ, texJ, Path.Combine(dJ, q.ToString("00000") + ".jpg"));
						}
						float vel = new Vector2(c.x - c0.x, c.z - c0.z).magnitude * FpsVideo;
						c0 = c;
						float cot = bArm && bFore && bMao ? Vector3.Angle(bArm.position - bFore.position, bMao.position - bFore.position) : -1f;
						csv.AppendLine($"{v.nome};{f.nome};{q};{q / (float)FpsVideo:0.000};{vel:0.00};{(me.aiming ? 1 : 0)};{(me.shooting ? 1 : 0)};{PesoBur(me):0.00};{cot:0}");
						totalQ++;
					}
					Time.captureFramerate = 0;
					entrada = new FBInputState();
					Log($"videoarma [{v.nome}] {f.nome}: {n} quadros ({f.dur:0.0} s a {FpsVideo} por segundo) em {pasta}_perto e _jogo · andou {(me.transform.position - p0).magnitude:0.0} m");
				}
			}
		}
		finally
		{
			Time.captureFramerate = 0;
			QualitySettings.lodBias = lod0;
			fotoCam.fieldOfView = fov0;
			fotoCam.nearClipPlane = near0;
			(FBRig.CabecaFuzil, FBRig.CabecaMira, FBRig.Patrulha, FBRig.Desvirar) = pegada0;
			BurAnim(false, out _);
			rtP.Release(); Object.Destroy(rtP);
			rtJ.Release(); Object.Destroy(rtJ);
			Object.Destroy(texP); Object.Destroy(texJ);
			if (camJ) Object.Destroy(camJ.gameObject);
			try { File.WriteAllText(Path.Combine(dir, "video_quadros.csv"), csv.ToString()); } catch { }
		}
		Log($"videoarma: {totalQ} quadros gravados (cada um em 2 vistas)");
		entrada = new FBInputState();
		FBPlayer.TestInput = null;
		FBMatch.God = false;
	}

	static void RenderPara(Camera cam, RenderTexture rt, Texture2D tex, string arquivo)
	{
		var req = new RenderPipeline.StandardRequest { destination = rt };
		if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
		else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
		var prev = RenderTexture.active;
		RenderTexture.active = rt;
		tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		tex.Apply();
		RenderTexture.active = prev;
		File.WriteAllBytes(arquivo, tex.EncodeToJPG(92));
	}

	// v12: retrato do estado gráfico (para achar o que fica ligado depois da parte "arma" e deixa a GPU 3x mais lenta no
	// "tudo"): câmeras, URP, qualidade, efeitos, volumes, render textures vivas, luzes com sombra e partículas.
	void EstadoGrafico(string quando)
	{
		try
		{
			var sb = new StringBuilder();
			var cams = Camera.allCameras;
			sb.Append($"câmeras ligadas {cams.Length}: ");
			sb.Append(string.Join(", ", cams.Select(c =>
			{
				var u = c.GetComponent<UniversalAdditionalCameraData>();
				return $"{c.name}(prof {c.depth:0}{(c.targetTexture ? $", RT {c.targetTexture.width}x{c.targetTexture.height}" : "")}{(u ? $", pós {(u.renderPostProcessing ? 1 : 0)}, AA {u.antialiasing}, pilha {(u.cameraStack != null ? u.cameraStack.Count : 0)}, tipo {u.renderType}" : "")})";
			})));
			var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
			if (urp)
			{
				sb.Append($" · URP {urp.name}: escala {urp.renderScale:0.00}, filtro {urp.upscalingFilter}, MSAA {urp.msaaSampleCount}, HDR {(urp.supportsHDR ? 1 : 0)}, sombra {urp.shadowDistance:0} m/{urp.shadowCascadeCount} cascatas/{urp.mainLightShadowmapResolution}, luzes extras sombra {(urp.supportsAdditionalLightShadows ? 1 : 0)}");
				var fl = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
				if (fl?.GetValue(urp) is ScriptableRendererData[] rds)
					foreach (var rd in rds.Where(r => r))
						sb.Append($" · efeitos de {rd.name}: " + string.Join(", ", rd.rendererFeatures.Where(f => f).Select(f => f.name + (f.isActive ? "" : "(off)"))));
			}
			sb.Append($" · qualidade {QualitySettings.names[QualitySettings.GetQualityLevel()]} lodBias {QualitySettings.lodBias:0.0} maxLOD {QualitySettings.maximumLODLevel} mip {QualitySettings.globalTextureMipmapLimit} · DynRes {FBDynRes.Scale:0.00} · tela {Screen.width}x{Screen.height} {Screen.fullScreenMode} · alvo {Application.targetFrameRate} FPS · timeScale {Time.timeScale:0.00}");
			var fF = FBGfx.F;
			sb.Append($" · FBGfx {FBGfx.Tier}: " + string.Join(" ", typeof(FBGfxFlags).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => $"{f.Name}={f.GetValue(fF)}")));
			var vols = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Where(v => v && v.isActiveAndEnabled && v.weight > 0f).ToList();
			sb.Append($" · volumes ativos {vols.Count}: " + string.Join("; ", vols.Select(v => $"{v.name}{(v.isGlobal ? " global" : "")} p{v.priority:0} w{v.weight:0.00} [{(v.sharedProfile ? string.Join(",", v.sharedProfile.components.Where(c => c && c.active).Select(c => c.GetType().Name)) : "")}]")));
			var rts = Resources.FindObjectsOfTypeAll<RenderTexture>().Where(r => r && r.IsCreated()).ToList();
			double mb = rts.Sum(r => (double)r.width * r.height * Mathf.Max(1, r.volumeDepth) * Mathf.Max(1, r.antiAliasing) * 4.0) / (1024 * 1024);
			sb.Append($" · render textures vivas {rts.Count} (~{mb:0} MB): " + string.Join(", ", rts.OrderByDescending(r => (long)r.width * r.height * Mathf.Max(1, r.volumeDepth)).Take(8).Select(r => $"{r.name} {r.width}x{r.height}{(r.volumeDepth > 1 ? "x" + r.volumeDepth : "")}")));
			var luzes = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l && l.isActiveAndEnabled).ToList();
			sb.Append($" · luzes {luzes.Count} ({luzes.Count(l => l.shadows != LightShadows.None)} com sombra): " + string.Join(", ", luzes.Where(l => l.shadows != LightShadows.None).Take(8).Select(l => $"{l.name} {l.type}")));
			var ps = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Where(p => p && p.isPlaying).ToList();
			sb.Append($" · partículas tocando {ps.Count} ({ps.Sum(p => p.particleCount)} vivas): " + string.Join(", ", ps.OrderByDescending(p => p.particleCount).Take(6).Select(p => $"{p.name} {p.particleCount}")));
			var probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Count(p => p && p.isActiveAndEnabled && p.mode == UnityEngine.Rendering.ReflectionProbeMode.Realtime);
			sb.Append($" · reflection probes em tempo real {probes} · objetos que não somem na troca de cena: " + string.Join(", ", DontDestroyRaizes().Take(30)));
			Log($"estado gráfico ({quando}): {sb}");
		}
		catch (Exception e) { Log($"estado gráfico ({quando}) falhou: {e.GetType().Name} {e.Message}"); }
	}

	IEnumerable<string> DontDestroyRaizes()
	{
		var s = gameObject.scene;
		if (!s.IsValid()) yield break;
		foreach (var r in s.GetRootGameObjects()) if (r && r.activeSelf) yield return r.name;
	}

	// ponta de trás da arma (coronha): o ponto da malha mais para trás ao longo do cano
	static Vector3 Coronha(FBCharacter me, GameObject w)
	{
		CarregarPontos(me, w);
		Vector3 bar = me.BarrelDir();
		var t = w.transform;
		Vector3 melhor = t.position;
		float menor = float.MaxValue;
		for (int i = 0; i < pontos.Length; i++)
		{
			Vector3 p = t.TransformPoint(pontos[i]);
			float d = Vector3.Dot(p, bar);
			if (d < menor) { menor = d; melhor = p; }
		}
		return melhor;
	}

	// mesmo "bolso do ombro" do FBRig: um pouco para dentro e para baixo da junta do ombro direito
	static Vector3 BolsoOmbro(FBCharacter me, Transform ombro, Transform peito)
	{
		if (!ombro) return me.transform.position + Vector3.up * 1.4f * me.ScaleK;
		Vector3 pc = peito ? peito.position : ombro.position;
		return ombro.position + (pc - ombro.position) * 0.3f + Vector3.down * 0.04f * me.ScaleK;
	}

	// pontos da malha da arma conferidos contra o corpo medido da malha do personagem (FBCharacter.BodyDepth)
	static string pontosId;
	static Vector3[] pontos;
	static float Fundo(FBCharacter me, GameObject w, bool semOmbro)
	{
		CarregarPontos(me, w);
		float fundo = 0f;
		var t = w.transform;
		var an = me.an;
		var ombro = an ? an.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
		var mao = an ? an.GetBoneTransform(HumanBodyBones.RightHand) : null;
		bool pula = semOmbro && ombro && mao;
		Vector3 bolso = pula ? BolsoOmbro(me, ombro, me.Chest) : Vector3.zero;
		Vector3 bar = me.BarrelDir();
		float r = 0.13f * me.ScaleK;
		for (int i = 0; i < pontos.Length; i++)
		{
			Vector3 p = t.TransformPoint(pontos[i]);
			if (pula && Vector3.Dot(p - mao.position, bar) <= 0f && (p - bolso).sqrMagnitude < r * r) continue;
			float d = me.BodyDepth(p, false, out _, out _);
			if (d > fundo) fundo = d;
		}
		return fundo;
	}

	static void CarregarPontos(FBCharacter me, GameObject w)
	{
		string id = me.weapon != null ? me.weapon.id : "";
		if (pontos == null || pontosId != id)
		{
			pontosId = id;
			var lista = new List<Vector3>();
			var w2l = w.transform.worldToLocalMatrix;
			foreach (var r in w.GetComponentsInChildren<Renderer>())
			{
				if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "laser|beam|flash", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
				Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : (r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null);
				if (!m) continue;
				var mat = w2l * r.transform.localToWorldMatrix;
				if (m.isReadable)
				{
					var v = m.vertices;
					int passo = Mathf.Max(1, v.Length / 600);
					for (int i = 0; i < v.Length; i += passo) lista.Add(mat.MultiplyPoint3x4(v[i]));
				}
				else
				{
					var b = m.bounds;
					for (int i = 0; i < 125; i++)
						lista.Add(mat.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(i % 5 / 2f - 1f, i / 5 % 5 / 2f - 1f, i / 25 / 2f - 1f))));
				}
			}
			pontos = lista.ToArray();
		}
	}

	// ---------- parte 2: modo 10x10 ----------
	// por time, em cada rodada: tiros, acertos no inimigo, dano, quanto tempo cada bot levou para ver alguém e quanto
	// tempo o time ficou na tela (para ver se a câmera muda o resultado)
	sealed class Placar
	{
		public int tiros, acertos, mortes, quadros, naTela;
		public float dano;
		public readonly List<float> viu = new List<float>();
		public int bots;
	}

	sealed class Seguido
	{
		public FBWeaponUser wu;
		public FBBot bot;
		public Renderer[] rends;
		public string arma;
		public int balas;
		public float hp;
		public bool vivo;
		public bool viu;
	}

	static readonly FieldInfo fSees = typeof(FBBot).GetField("sees", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly FieldInfo fBotPath = typeof(FBBot).GetField("path", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly FieldInfo fBotStuckN = typeof(FBBot).GetField("stuckN", BindingFlags.NonPublic | BindingFlags.Instance);
	// v15: contador de bots realocados do conserto dos presos (FBBot.Realocados / FBBot.RealocadosPor), por reflexão
	static readonly FieldInfo fRealoc = typeof(FBBot).GetField("Realocados", BindingFlags.Public | BindingFlags.Static);
	static readonly FieldInfo fRealocPor = typeof(FBBot).GetField("RealocadosPor", BindingFlags.Public | BindingFlags.Static);
	static int Realocados() { try { return fRealoc != null ? (int)fRealoc.GetValue(null) : -1; } catch { return -1; } }
	static int[] RealocadosPor() { try { return fRealocPor?.GetValue(null) as int[]; } catch { return null; } }
	// v16: prevenção do conserto dos presos (FBBot.Evitados), por reflexão
	static readonly FieldInfo fEvit = typeof(FBBot).GetField("Evitados", BindingFlags.Public | BindingFlags.Static);
	static int[] Evitados() { try { return fEvit?.GetValue(null) as int[]; } catch { return null; } }
	static string TextoEvitados(int[] e)
	{
		if (e == null || e.Length < 2) return "sem contador (FreeBala sem a v2 do conserto dos presos)";
		string t = $"{e[0]} destinos fora da ilha principal desviados, {e[1]} coberturas fora da ilha principal canceladas";
		// v17: saídas a pé do conserto v4
		if (e.Length >= 4) t += $", {e[2]} saídas a pé no stuckN 4 fora da patrulha, {e[3]} saídas a pé no stuckN 10";
		return t;
	}
	static void ZeraRealocados()
	{
		try
		{
			fRealoc?.SetValue(null, 0);
			var p = RealocadosPor();
			if (p != null) Array.Clear(p, 0, p.Length);
			var e = Evitados(); // v16
			if (e != null) Array.Clear(e, 0, e.Length);
		}
		catch { }
	}
	static string TextoRealocados(int n, int[] por)
	{
		if (n < 0) return "sem contador (FreeBala sem o conserto dos presos)";
		string s = n.ToString();
		if (por != null && por.Length >= 3) s += $" (ilha do NavMesh {por[0]}, vigia de progresso {por[1]}, teleporte antigo do stuckN 10 {por[2]}" + (por.Length >= 4 ? $", realocação escondida do stuckN 10 {por[3]})" : ")");
		return s;
	}
	// v15: escreve as linhas "[BURITIRO] presos:" guardadas desde a última vez (até 15) e esvazia a lista
	void LinhasPresos()
	{
		var l = linhasPresos.ToList();
		linhasPresos.Clear();
		int perdidas = presosPerdidos;
		presosPerdidos = 0;
		foreach (var x in l.Take(15)) Log("      " + x.Substring("[BURITIRO] ".Length));
		if (l.Count > 15 || perdidas > 0) Log($"      ... e mais {l.Count - Mathf.Min(15, l.Count) + perdidas} linhas de presos");
	}
	static readonly NavMeshPath navDiag = new NavMeshPath();

	// v14: estado do caminho no NavMesh entre dois pontos (para achar bot preso em ilha do NavMesh).
	static string NavEstado(Vector3 de, Vector3 para)
	{
		if (!NavMesh.SamplePosition(de, out var h0, 3f, -1)) return "origem fora do NavMesh (3 m)";
		if (!NavMesh.SamplePosition(para, out var h1, 8f, -1)) return "destino fora do NavMesh (8 m)";
		if (!NavMesh.CalculatePath(h0.position, h1.position, -1, navDiag)) return "sem caminho";
		var c = navDiag.corners;
		if (navDiag.status == NavMeshPathStatus.PathComplete) return $"completo ({c.Length} cantos)";
		float fim = c.Length > 0 ? Vector3.Distance(c[c.Length - 1], h1.position) : -1f;
		return (navDiag.status == NavMeshPathStatus.PathPartial ? "PARCIAL" : "INVÁLIDO") + $" (acaba a {fim:0} m do ponto)";
	}
	readonly Plane[] planos = new Plane[6];

	IEnumerator Modo10x10()
	{
		FBSession.team = timeJog;
		yield return Carregar("10x10");
		var g = FBMatch.I;
		if (!g || g.mode == null) { Log("10x10: a partida não montou"); yield break; }
		Log($"10x10: modo carregado = {g.mode.GetType().Name} ({g.mode.name})");
		EstadoGrafico("10x10, compra");
		var tipo = g.mode.GetType();
		var fWins = tipo.GetField("wins", BindingFlags.NonPublic | BindingFlags.Instance);
		var fPhase = tipo.GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
		int[] Wins() => fWins?.GetValue(g.mode) as int[] ?? new int[2];
		int win = 7;
		try { var fWin = tipo.GetField("WIN", BindingFlags.Public | BindingFlags.Static); if (fWin != null) win = Convert.ToInt32(fWin.GetValue(null)); } catch { }
		var fState = typeof(FBMatch).GetField("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		string Estado() { try { return fState?.GetValue(g) as string ?? "?"; } catch { return "?"; } }
		void LinhasModo() { if (linhasModo.Count == 0) return; var l = linhasModo.ToList(); linhasModo.Clear(); foreach (var x in l) Log("   modo: " + x); }
		Log($"10x10: a partida termina quando um time chega a {win} vitórias (WIN do modo)" + (rodadas < 2 * win - 1 ? $"; este teste para antes, com {rodadas} rodadas" : ""));
		string Fase() => fPhase?.GetValue(g.mode) as string ?? "?";
		// v18: piloto = bot que joga com o nosso jogador (FBPiloto), ligado antes da primeira compra
		FBPiloto piloto = null;
		if (optPiloto)
		{
			if (opt9x9) { opt9x9 = false; Log("piloto: a opção 9x9 ficou desligada (o jogador joga, então é 10 x 10)"); }
			if (camAltaOpt) { camAltaOpt = false; Log("piloto: a câmera no alto ficou desligada (as fotos são da tela do jogador)"); }
			piloto = gameObject.AddComponent<FBPiloto>();
			piloto.Log = Log;
			piloto.Fase = Fase;
			piloto.NavEstado = NavEstado;
			piloto.Liga();
			Log($"piloto ligado: o jogador compra na loja (cliques e tecla B), segue o destino do modo como os bots do time e joga com WASD/mouse · mira assistida \"{FBSession.aimAssist}\" · segurar para correr {(FBSession.holdRun ? "sim" : "não")}");
		}
		for (float w = 0f; w < 10f && Fase() != "buy"; w += Time.unscaledDeltaTime) yield return null;
		yield return Wait(1.5f);

		// fase de compra: quantos personagens, de que time, dinheiro
		var todos = FBMatch.All.Where(c => c).ToList();
		var me = FBMatch.Player;
		Log($"10x10 fase de compra ({Fase()}): {todos.Count} personagens · time 0: {todos.Count(c => c.team == 0)} · time 1: {todos.Count(c => c.team == 1)} · vivos {todos.Count(c => c.alive)} · jogador no time {(me ? me.team : -1)} · dinheiro do jogador ${(me ? FBEconomy.Money(me) : 0)} · partida congelada {g.freeze}");
		var modos = todos.Where(c => c.an).GroupBy(c => c.an.cullingMode).Select(x => $"{x.Key} x{x.Count()}");
		Log($"10x10: culling dos Animator dos personagens: {string.Join(", ", modos)} · correção da arma no peito {(FBRig.TorsoClear ? "ligada" : "desligada")} até {FBRig.TorsoDist:0} m");
		bool lojaSozinha = FBShop.Open;
		Log($"10x10: loja aberta sozinha na fase de compra: {(lojaSozinha ? "sim" : "não")}");
		if (lojaSozinha) yield return Tela("10x10_2_loja_aberta");
		if (!piloto) FBShop.Show(false); // v18: com piloto a loja é dele, o teste não abre nem fecha
		yield return Wait(0.4f);
		yield return Tela("10x10_1_fase_de_compra");
		if (!lojaSozinha && !piloto)
		{
			FBShop.Show(true);
			yield return Wait(0.6f);
			yield return Tela("10x10_2_loja_aberta");
			FBShop.Show(false);
		}
		if (Camera.main) fotoCam.cullingMask = Camera.main.cullingMask;
		var centros = new Vector3[2];
		for (int t = 0; t < 2; t++)
		{
			var meu = todos.Where(c => c.team == t).ToList();
			var deles = todos.Where(c => c.team != t).ToList();
			if (meu.Count == 0) continue;
			Vector3 c0 = meu.Aggregate(Vector3.zero, (a, c) => a + c.Pos) / meu.Count;
			Vector3 c1 = deles.Count > 0 ? deles.Aggregate(Vector3.zero, (a, c) => a + c.Pos) / deles.Count : c0 + Vector3.forward;
			centros[t] = c0;
			Vector3 fr = c1 - c0; fr.y = 0f; fr = fr.sqrMagnitude > 0.01f ? fr.normalized : Vector3.forward;
			float raio = meu.Max(c => Vector3.Distance(c.Pos, c0));
			// de cima, por trás do time, acima dos prédios
			fotoCam.transform.position = c0 - fr * (9f + raio) + Vector3.up * (30f + raio);
			fotoCam.transform.LookAt(c0 + Vector3.up * 0.8f);
			Render($"10x10_{3 + t}_time{t}_na_base", 1280, 720);
			Log($"10x10 time {t}: {meu.Count} na base, espalhados num raio de {raio:0} m · distância até a base inimiga {Vector3.Distance(c0, c1):0} m");
		}

		// custo da correção da arma no peito com 20 personagens (bots parados na compra, mesma vista)
		yield return Medir("correção em todos os 20 (sem limite de distância nem de tela)", true, 0f);
		yield return Medir("correção só em quem está a menos de 30 m e aparece na tela", true, 30f);
		yield return Medir("correção desligada", false, 30f);
		FBRig.TorsoClear = true;
		FBRig.TorsoDist = 30f;
		if (semCulling) SemCulling(todos, true);
		if (camAltaOpt) CamAlta(centros[0], centros[1]);

		// partida rodando sozinha: placar, FPS e desempenho por rodada
		var perf = new Desempenho();
		yield return null;
		perf.Sincronizar();
		FBTempo.On = true; // v11: tempo de cada script (FBTempo), zerado junto com o perf
		FBTempo.Zera();
		Log("desempenho: " + perf.Estado());
		StreamWriter csv = null;
		try
		{
			csv = new StreamWriter(Path.Combine(dir, "desempenho.csv"), false, new UTF8Encoding(false)) { AutoFlush = true };
			csv.WriteLine("segundo;rodada;fase;fps;quadro_ms;cpu_principal_ms;render_thread_ms;gpu_ms;" + string.Join(";", perf.NomesCsv()) + ";correcao_peito_ms;vivos_time0;vivos_time1;na_tela_time0;na_tela_time1");
		}
		catch (Exception e) { Log("desempenho.csv não abriu: " + e.Message); }
		float limite = 60f + rodadas * 175f;
		int rodada = g.mode.Round;
		string fase = Fase();
		int[] w0 = (int[])Wins().Clone();
		float total = 0f, seg = 0f;
		bool fotoPlacar = false, fotoCombate = false, fotoTiroteio = false, fimPartida = false;
		FBCharacter alvoTiro = null;
		int pulaQuadros = 0;
		var baseIni = new Vector3[2];
		var fins = new List<string>();
		int porElim = 0, porTempo = 0, porSoJogador = 0;
		var armasRodada = new Dictionary<int, string>();
		var inicio = new Dictionary<FBCharacter, Vector3>();
		var ultimaPos = new Dictionary<FBCharacter, Vector3>(); // v14
		bool fotoParado = false;
		var mortes = new List<(int time, float t, float dist)>();
		var seg_ = new Dictionary<FBCharacter, Seguido>();
		var placar = new[] { new Placar(), new Placar() };
		var totalTimes = new[] { new Placar(), new Placar() };
		int realocTotal = 0; // v15: bots realocados somando as rodadas
		// v17: do tamanho dos contadores do FreeBala deste build (build antigo não ganha contador que não tem)
		var realocTotalPor = new int[RealocadosPor()?.Length ?? 3];
		var evitTotal = new int[Evitados()?.Length ?? 2]; // v16: prevenção do conserto dos presos somando as rodadas
		float tLive = 0f;
		float proxParado = 15f; // v11: a cada 15 s de combate, quantos bots de cada time ainda estão perto de onde nasceram
		double torsoSeg = 0.0;
		int quadrosSeg = 0;
		if (fase == "live") Comecar();
		Seguido Seg(FBCharacter c)
		{
			if (!seg_.TryGetValue(c, out var s))
			{
				s = new Seguido { wu = c.GetComponent<FBWeaponUser>(), bot = c.GetComponent<FBBot>(), rends = c.GetComponentsInChildren<SkinnedMeshRenderer>(true), hp = c.hp, vivo = c.alive };
				s.arma = s.wu ? s.wu.Cur?.id : null;
				s.balas = s.wu ? s.wu.Ammo : 0;
				seg_[c] = s;
			}
			return s;
		}
		void Comecar()
		{
			inicio.Clear(); mortes.Clear(); tLive = 0f; proxParado = 15f; ultimaPos.Clear(); fotoParado = false;
			fotoCombate = false; fotoTiroteio = false; alvoTiro = null;
			placar[0] = new Placar(); placar[1] = new Placar();
			seg_.Clear();
			// v15: contador de realocados do FBBot e linhas de presos começam do zero em cada rodada
			ZeraRealocados(); linhasPresos.Clear(); presosPerdidos = 0;
			foreach (var ch in FBMatch.All.Where(x => x && x.alive && x.team <= 1)) { inicio[ch] = ch.Pos; Seg(ch); if (!ch.isPlayer) placar[ch.team].bots++; }
			for (int t = 0; t < 2; t++)
			{
				var armas = FBMatch.All.Where(c => c && c.alive && c.team == t).Select(c => (c.weapon?.id ?? "sem arma") + (c.isPlayer ? (piloto ? " (jogador piloto)" : " (jogador parado)") : ""))
					.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} x{x.Count()}");
				Log($"   armas do time {t} no começo da rodada {g.mode.Round}: {string.Join(", ", armas)}");
				var vt = FBMatch.All.Where(c => c && c.alive && c.team == t).ToList();
				baseIni[t] = vt.Count > 0 ? vt.Aggregate(Vector3.zero, (a, c) => a + c.Pos) / vt.Count : Vector3.zero;
			}
			{
				string Classes(int t) => string.Join(", ", FBMatch.All.Where(c => c && c.alive && c.team == t && !c.isPlayer).Select(c => c.weapon?.cls ?? "sem arma")
					.GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} x{x.Count()}"));
				armasRodada[g.mode.Round] = $"time 0: {Classes(0)} | time 1: {Classes(1)}";
				Log($"   classes das armas dos bots na rodada {g.mode.Round}: {armasRodada[g.mode.Round]}");
			}
			if (semCulling) SemCulling(FBMatch.All.Where(x => x).ToList(), false);
			if (opt9x9 && me)
			{
				var alvo = FBMatch.All.Where(c => c && c.alive && !c.isPlayer && c.team <= 1 && c.team != me.team).OrderBy(c => (c.name.GetHashCode() ^ g.mode.Round * 7919) & 0x7fffffff).FirstOrDefault();
				if (alvo)
				{
					try { alvo.Die(null); } catch (Exception e) { Log("9x9: Die falhou: " + e.Message); }
					if (alvo.alive) try { alvo.Damage(9999f, null, false); } catch (Exception e) { Log("9x9: Damage falhou: " + e.Message); }
					inicio.Remove(alvo);
					placar[alvo.team].bots--;
					var sa = Seg(alvo);
					sa.hp = alvo.hp; sa.vivo = alvo.alive;
					Log($"   9x9: tirei 1 bot do time {alvo.team} ({(alvo.alive ? "NÃO morreu" : "morreu")})");
				}
			}
		}
		string Mortes(int t)
		{
			var m = mortes.Where(x => x.time == t).ToList();
			if (m.Count == 0) return $"time {t}: ninguém morreu";
			var ts = m.Select(x => x.t).OrderBy(x => x).ToList();
			return $"time {t}: {m.Count} mortes · primeira com {ts[0]:0} s · metade até {ts[(ts.Count - 1) / 2]:0} s · andaram em média {m.Average(x => x.dist):0} m do lugar onde nasceram";
		}
		string Combate(int t, Placar[] p)
		{
			var a = p[t];
			var b = p[1 - t];
			var v = a.viu.OrderBy(x => x).ToList();
			string viu = v.Count == 0 ? "nenhum bot viu inimigo" : $"{v.Count} de {a.bots} bots viram inimigo, o primeiro com {v[0]:0.0} s, metade até {v[(v.Count - 1) / 2]:0.0} s";
			return $"time {t}: {a.tiros} tiros · {b.acertos} acertos no inimigo ({(a.tiros > 0 ? 100f * b.acertos / a.tiros : 0f):0}% dos tiros) · {b.dano:0} de dano causado · {viu} · na tela {(a.quadros > 0 ? 100f * a.naTela / a.quadros : 0f):0}% do tempo";
		}
		FBRig.TorsoMs = 0.0; FBRig.TorsoCalls = 0;
		int quadros = 0; float tempo = 0f, pior = 0f;
		var ft = new List<float>(20000);
		while (total < limite && FBMatch.I == g)
		{
			yield return null;
			float dt = Time.unscaledDeltaTime;
			total += dt; tempo += dt; quadros++; seg += dt; quadrosSeg++;
			perf.Quadro(dt);
			if (camAltaOpt) CamAltaManter();
			LinhasModo();
			if (piloto && piloto.FotoPedida != null)
			{
				// v18: foto da tela do jogador pedida pelo piloto (mirando, preso, câmera na parede, 20 s de rodada)
				string nf = piloto.FotoPedida;
				piloto.FotoPedida = null;
				yield return Tela(nf);
				pulaQuadros = 2;
				perf.Sincronizar();
			}
			int[] vivosT = new int[2], telaT = new int[2];
			var camTela = Camera.main;
			if (camTela) GeometryUtility.CalculateFrustumPlanes(camTela, planos);
			foreach (var ch in FBMatch.All)
			{
				if (!ch || ch.team > 1) continue;
				var s = Seg(ch);
				// na tela = dentro do campo de visão da câmera do jogo (isVisible também conta quem só faz sombra)
				bool tela = false;
				if (camTela && !ch.isPlayer) foreach (var r in s.rends) if (r && r.enabled && GeometryUtility.TestPlanesAABB(planos, r.bounds)) { tela = true; break; }
				if (ch.alive) { vivosT[ch.team]++; if (tela) telaT[ch.team]++; }
				if (fase != "live") { s.hp = ch.hp; s.vivo = ch.alive; s.balas = s.wu ? s.wu.Ammo : 0; s.arma = s.wu ? s.wu.Cur?.id : null; continue; }
				var p = placar[ch.team];
				if (ch.alive && !ch.isPlayer) { p.quadros++; if (tela) p.naTela++; }
				if (s.wu)
				{
					string arma = s.wu.Cur?.id;
					int balas = s.wu.Ammo;
					if (arma == s.arma && balas < s.balas) p.tiros += s.balas - balas;
					s.arma = arma; s.balas = balas;
				}
				// acerto = quadro em que a vida caiu (sem contar o sangramento de quem está caído)
				if (ch.hp < s.hp - 0.01f && !ch.knocked) { p.acertos++; p.dano += s.hp - Mathf.Max(0f, ch.hp); if (!fotoTiroteio && !alvoTiro && ch.alive && !ch.isPlayer) alvoTiro = ch; }
				s.hp = ch.hp;
				if (s.vivo && !ch.alive) p.mortes++;
				s.vivo = ch.alive;
				// "sees" fica com o valor da rodada anterior até o bot pensar de novo (0,12-0,22 s), então só conta depois de 0,4 s
				if (!s.viu && tLive > 0.4f && s.bot && fSees != null && ch.alive && (bool)fSees.GetValue(s.bot)) { s.viu = true; p.viu.Add(tLive); }
			}
			if (fase == "live")
			{
				tLive += dt;
				foreach (var ch in inicio.Keys.ToList())
					if (!ch || !ch.alive) { mortes.Add((ch ? ch.team : -1, tLive, ch ? Vector3.Distance(ch.Pos, inicio[ch]) : 0f)); inicio.Remove(ch); }
				if (tLive >= proxParado)
				{
					proxParado += 15f;
					string Parados(int t)
					{
						var l = inicio.Where(kv => kv.Key && kv.Key.alive && !kv.Key.isPlayer && kv.Key.team == t).Select(kv => (kv.Key.Pos - kv.Value).Flat().magnitude).OrderBy(x => x).ToList();
						return l.Count == 0 ? $"time {t}: nenhum bot vivo" : $"time {t}: {l.Count(x => x < 20f)} de {l.Count} bots vivos a menos de 20 m de onde nasceram (mediana {l[l.Count / 2]:0} m, mais longe {l[l.Count - 1]:0} m)";
					}
					Log($"   10x10 rodada {rodada} com {tLive:0} s: {Parados(0)} | {Parados(1)}");
					// v14: quem andou menos de 6 m nesses 15 s sem estar vendo inimigo
					int nPar = 0;
					foreach (var c in FBMatch.All.ToList())
					{
						if (!c || !c.alive || c.isPlayer || c.team > 1) continue;
						bool tinha = ultimaPos.TryGetValue(c, out var ant);
						ultimaPos[c] = c.Pos;
						if (!tinha) continue;
						float andou = (c.Pos - ant).Flat().magnitude;
						if (andou >= 6f) continue;
						var sb = Seg(c).bot;
						if (sb && fSees != null && (bool)fSees.GetValue(sb)) continue;
						nPar++;
						if (nPar > 8) continue;
						try
						{
							Vector3? gp = sb ? sb.goalP : null;
							var pl = sb && fBotPath != null ? fBotPath.GetValue(sb) as List<Vector3> : null;
							int stN = sb && fBotStuckN != null ? (int)fBotStuckN.GetValue(sb) : -1;
							string alvo = gp.HasValue ? $"destino ({gp.Value.x:0}, {gp.Value.y:0.0}, {gp.Value.z:0}) a {(gp.Value - c.Pos).Flat().magnitude:0} m, caminho {NavEstado(c.Pos, gp.Value)}" : "sem destino";
							Log($"      parado: {c.name} (time {c.team}) em ({c.Pos.x:0.0}, {c.Pos.y:0.00}, {c.Pos.z:0.0}) andou {andou:0.0} m em 15 s · ai {(sb ? sb.ai : "?")} · {alvo} · cantos no caminho do bot {(pl != null ? pl.Count : -1)} · stuckN {stN} · da base do time até ele: {NavEstado(baseIni[c.team], c.Pos)}");
							if (!fotoParado)
							{
								fotoParado = true;
								fotoCam.transform.position = c.Pos + new Vector3(0f, 32f, -12f);
								fotoCam.transform.LookAt(c.Pos);
								Render($"10x10_rodada{rodada}_parado_de_cima", 1280, 720);
								fotoCam.transform.position = c.Pos + new Vector3(0f, 6f, -10f);
								fotoCam.transform.LookAt(c.Pos + Vector3.up);
								Render($"10x10_rodada{rodada}_parado_perto", 1280, 720);
							}
						}
						catch (Exception e) { Log($"      parado: {c.name} erro no diagnóstico: {e.Message}"); }
					}
					if (nPar > 8) Log($"      ... e mais {nPar - 8} bots parados");
				}
			}
			if (pulaQuadros > 0) pulaQuadros--;
			else { pior = Mathf.Max(pior, dt); ft.Add(dt * 1000f); }
			if (seg >= 1f)
			{
				double torso = FBRig.TorsoMs - torsoSeg;
				torsoSeg = FBRig.TorsoMs;
				try { csv?.WriteLine(FormattableString.Invariant($"{total:0.0};{rodada};{fase};{quadrosSeg / seg:0.0};") + perf.LinhaCsv() + FormattableString.Invariant($";{torso / Mathf.Max(1, quadrosSeg):0.000};{vivosT[0]};{vivosT[1]};{telaT[0]};{telaT[1]}")); } catch { }
				seg = 0f; quadrosSeg = 0;
			}
			string f = Fase();
			if (f == "live" && fase == "live" && !fotoTiroteio && alvoTiro)
			{
				fotoTiroteio = true;
				var alvo = alvoTiro;
				alvoTiro = null;
				var atirador = FBMatch.All.Where(c => c && c.alive && c.team <= 1 && c.team != alvo.team).OrderBy(c => (c.Pos - alvo.Pos).sqrMagnitude).FirstOrDefault();
				if (atirador)
				{
					Vector3 a0 = atirador.Pos, a1 = alvo.Pos;
					Vector3 d = a1 - a0; d.y = 0f;
					float dist = d.magnitude;
					d = dist > 0.1f ? d / dist : Vector3.forward;
					Vector3 dir = Vector3.Cross(Vector3.up, d);
					Vector3 meio = (baseIni[0] + baseIni[1]) * 0.5f, luta = (a0 + a1) * 0.5f;
					float entreBases = (baseIni[0] - baseIni[1]).Flat().magnitude;
					Log($"10x10 rodada {rodada} primeiro tiroteio com {tLive:0.0} s: {atirador.name} (time {atirador.team}, {atirador.weapon?.id ?? "sem arma"}) em ({a0.x:0}, {a0.z:0}) contra {alvo.name} (time {alvo.team}) a {dist:0} m · luta a {(luta - meio).Flat().magnitude:0} m do meio entre as bases (bases a {entreBases:0} m uma da outra) · a {(luta - baseIni[0]).Flat().magnitude:0} m da base 0 e {(luta - baseIni[1]).Flat().magnitude:0} m da base 1");
					fotoCam.transform.position = a0 - d * 3.2f + dir * 0.9f + Vector3.up * 2.1f;
					fotoCam.transform.LookAt(a1 + Vector3.up * 1.1f);
					Render($"10x10_rodada{rodada}_tiroteio", 1280, 720);
					float lado = Mathf.Clamp(dist * 0.6f + 8f, 10f, 60f);
					fotoCam.transform.position = luta + dir * lado + Vector3.up * Mathf.Clamp(dist * 0.35f + 6f, 8f, 40f);
					fotoCam.transform.LookAt(luta + Vector3.up * 1f);
					Render($"10x10_rodada{rodada}_tiroteio_de_lado", 1280, 720);
					pulaQuadros = 2;
					perf.Sincronizar();
				}
			}
			if (f == "live" && !fotoCombate && tempo > 35f)
			{
				fotoCombate = true;
				var vv = FBMatch.All.Where(c => c && c.alive).ToList();
				Log($"10x10 rodada {rodada} com 35 s de combate: vivos time 0: {vv.Count(c => c.team == 0)}, time 1: {vv.Count(c => c.team == 1)}");
				yield return Tela($"10x10_rodada{rodada}_combate");
				if (rodada == 1) EstadoGrafico("10x10, rodada 1 com 35 s de combate");
				pulaQuadros = 2;
				perf.Sincronizar();
			}
			if (f != fase)
			{
				var wn = Wins();
				var vivos = FBMatch.All.Where(c => c && c.alive).ToList();
				ft.Sort();
				float p95 = ft.Count > 0 ? ft[Mathf.Min(ft.Count - 1, (int)(ft.Count * 0.95f))] : 0f;
				Log($"10x10 rodada {rodada} · fase {fase} → {f} · {tempo:0} s · FPS médio {quadros / Mathf.Max(0.01f, tempo):0} (95% dos quadros abaixo de {p95:0} ms, pior {pior * 1000f:0} ms) · placar {wn[0]} x {wn[1]} · vivos time 0: {vivos.Count(c => c.team == 0)}, time 1: {vivos.Count(c => c.team == 1)}" + (me ? $" · dinheiro do jogador ${FBEconomy.Money(me)} · jogador {(me.alive ? "vivo" : "morto")}" : ""));
				Log($"   correção da arma no peito nessa fase: {FBRig.TorsoMs / Mathf.Max(1, quadros):0.00} ms por quadro ({FBRig.TorsoCalls / Mathf.Max(1, quadros)} personagens por quadro)");
				Log("   desempenho: " + perf.Resumo());
				{
					var lt = FBTempo.Lista();
					int qd = Mathf.Max(1, quadros);
					double somaMs = lt.Sum(x => x.ms);
					Log($"   scripts nessa fase: {somaMs / qd:0.00} ms por quadro somando {lt.Count} tipos · os mais pesados (ms por quadro, chamadas por quadro): " +
						string.Join(" · ", lt.OrderByDescending(x => x.ms).Take(14).Select(x => $"{x.nome} {x.ms / qd:0.000} ({(float)x.n / qd:0.#})")));
				}
				if (fase == "live" && f == "post")
				{
					int v0 = vivos.Count(c => c.team == 0), v1 = vivos.Count(c => c.team == 1);
					int b0 = vivos.Count(c => c.team == 0 && !c.isPlayer), b1 = vivos.Count(c => c.team == 1 && !c.isPlayer);
					bool elim = v0 == 0 || v1 == 0;
					// v13b: o jogador parado e sem arma segura a rodada até o tempo quando os bots do time dele já morreram
					bool soJogador = !elim && (b0 == 0 || b1 == 0);
					if (elim) porElim++; else if (soJogador) porSoJogador++; else porTempo++;
					string como = elim ? "eliminação" : soJogador ? (piloto ? "tempo, com só o jogador (piloto) vivo do time dele" : "tempo, mas só o jogador parado estava vivo (bots do time eliminados)") : "tempo";
					int venc = wn[0] > w0[0] ? 0 : wn[1] > w0[1] ? 1 : -1;
					string fim = $"rodada {rodada}: venceu o time {venc} por {como} com {tLive:0} s de combate · vivos {v0} x {v1} (bots {b0} x {b1}) · placar {wn[0]} x {wn[1]}" + (armasRodada.TryGetValue(rodada, out var ar) ? $" · armas no começo: {ar}" : "");
					fins.Add(fim);
					Log("   fim da " + fim);
					w0 = (int[])wn.Clone();
				}
				if (fase == "live")
				{
					Log($"   mortes na rodada {rodada}: {Mortes(0)} | {Mortes(1)}");
					Log($"   combate na rodada {rodada}: {Combate(0, placar)} | {Combate(1, placar)}");
					{
						// v15: bots realocados (teleportados) pelo conserto dos presos nesta rodada, com as linhas do log
						int nr = Realocados();
						var pr = RealocadosPor();
						Log($"   bots realocados (presos) na rodada {rodada}: {TextoRealocados(nr, pr)}");
						// v16: prevenção nesta rodada (destinos desviados, coberturas canceladas)
						var evR = Evitados();
						Log($"   prevenção (presos) na rodada {rodada}: {TextoEvitados(evR)}");
						if (evR != null) for (int k = 0; k < Mathf.Min(evitTotal.Length, evR.Length); k++) evitTotal[k] += evR[k];
						if (nr > 0)
						{
							realocTotal += nr;
							if (pr != null) for (int k = 0; k < Mathf.Min(realocTotalPor.Length, pr.Length); k++) realocTotalPor[k] += pr[k];
						}
						LinhasPresos();
					}
					for (int t = 0; t < 2; t++)
					{
						totalTimes[t].tiros += placar[t].tiros; totalTimes[t].acertos += placar[t].acertos; totalTimes[t].dano += placar[t].dano;
						totalTimes[t].mortes += placar[t].mortes; totalTimes[t].quadros += placar[t].quadros; totalTimes[t].naTela += placar[t].naTela;
						totalTimes[t].bots += placar[t].bots; totalTimes[t].viu.AddRange(placar[t].viu);
					}
				}
				if (f == "live") Comecar();
				FBRig.TorsoMs = 0.0; FBRig.TorsoCalls = 0; torsoSeg = 0.0;
				quadros = 0; tempo = 0f; pior = 0f; ft.Clear();
				perf.Zerar();
				if (f == "post")
				{
					yield return Wait(1.2f);
					yield return Tela($"10x10_placar_rodada{rodada}");
					if (Mathf.Max(wn[0], wn[1]) >= win)
					{
						// o modo chama EndMatch quando a fase post acaba (5 s) e o cartão final aparece 1,8-4,2 s depois
						for (float w = 0f; w < 20f && Estado() != "end"; w += Time.unscaledDeltaTime) yield return null;
						Log($"10x10: um time chegou a {win} vitórias · estado da partida \"{Estado()}\"");
						yield return Wait(6f);
						LinhasModo();
						yield return Tela("10x10_fim_partida");
						fimPartida = true; fotoPlacar = true; break;
					}
					if (wn[0] + wn[1] >= rodadas) { fotoPlacar = true; break; }
				}
				if (f == "buy" && g.mode.Round != rodada)
				{
					rodada = g.mode.Round;
					bool lojaAberta = FBShop.Open; // v18: 1º quadro da compra, antes de o piloto mexer
					yield return Wait(1f);
					if (!piloto) FBShop.Show(false);
					yield return Wait(0.4f);
					yield return Tela($"10x10_compra_rodada{rodada}");
					LinhasModo();
					{
						var tds = FBMatch.All.Where(c => c && c.team <= 1).ToList();
						var cb = new Vector3[2];
						for (int t = 0; t < 2; t++) { var m = tds.Where(c => c.team == t).ToList(); cb[t] = m.Count > 0 ? m.Aggregate(Vector3.zero, (a, c) => a + c.Pos) / m.Count : Vector3.zero; }
						for (int t = 0; t < 2; t++)
						{
							var m = tds.Where(c => c.team == t).ToList();
							if (m.Count == 0) continue;
							Vector3 fr = cb[1 - t] - cb[t]; fr.y = 0f; fr = fr.sqrMagnitude > 0.01f ? fr.normalized : Vector3.forward;
							float raio = m.Max(c => Vector3.Distance(c.Pos, cb[t]));
							fotoCam.transform.position = cb[t] - fr * (9f + raio) + Vector3.up * (30f + raio);
							fotoCam.transform.LookAt(cb[t] + Vector3.up * 0.8f);
							Render($"10x10_rodada{rodada}_base_time{t}", 1280, 720);
						}
						Log($"10x10 rodada {rodada} compra: base do time 0 em ({cb[0].x:0}, {cb[0].z:0}), do time 1 em ({cb[1].x:0}, {cb[1].z:0}), a {(cb[0] - cb[1]).Flat().magnitude:0} m · loja {(lojaAberta ? (piloto ? "abriu sozinha (o piloto compra e fecha)" : "estava aberta (fechada para a foto)") : "NÃO abriu sozinha")}");
					}
				}
				Log("   censo (" + f + "): " + Censo());
				// o que passou durante as fotos e esperas não entra na conta da fase seguinte
				perf.Sincronizar();
				FBTempo.Zera();
				FBRig.TorsoMs = 0.0; FBRig.TorsoCalls = 0; torsoSeg = 0.0;
				fase = f;
			}
		}
		var wf = Wins();
		Log($"10x10 resultado: {(fotoPlacar ? "terminou" : "parou no limite de tempo em")} {wf[0] + wf[1]} rodadas · placar {wf[0]} x {wf[1]} · tempo total {total:0} s");
		Log($"10x10 combate somando as rodadas: {Combate(0, totalTimes)} | {Combate(1, totalTimes)}");
		{
			// v15: bots realocados somando as rodadas; se o teste parou no meio de uma rodada, entra o que já tinha nela
			int nf = realocTotal;
			var pf = (int[])realocTotalPor.Clone();
			int nr = Realocados();
			if (fase == "live" && nr > 0)
			{
				nf += nr;
				var pr = RealocadosPor();
				if (pr != null) for (int k = 0; k < Mathf.Min(pf.Length, pr.Length); k++) pf[k] += pr[k];
			}
			Log($"10x10 bots realocados (presos) somando as rodadas: {TextoRealocados(nr < 0 ? -1 : nf, pf)}");
			// v16: prevenção somando as rodadas (com a rodada parcial, se o teste parou no meio)
			var evF = (int[])evitTotal.Clone();
			var evP = Evitados();
			if (fase == "live" && evP != null) for (int k = 0; k < Mathf.Min(evF.Length, evP.Length); k++) evF[k] += evP[k];
			Log($"10x10 prevenção (presos) somando as rodadas: {TextoEvitados(evP == null ? null : evF)}");
			if (fase == "live") LinhasPresos();
		}
		LinhasModo();
		Log($"10x10 como as rodadas acabaram: {porElim} por eliminação, {porTempo} por tempo, {porSoJogador} por tempo com só o jogador parado vivo · partida {(fimPartida ? $"terminou com um time em {win}" : "não chegou ao fim")}");
		foreach (var x in fins) Log("   " + x);
		if (piloto)
		{
			piloto.Resumo();
			piloto.Desliga();
			Destroy(piloto);
		}
		perf.Dispose();
		FBTempo.On = false;
		try { csv?.Dispose(); } catch { }
		if (camAltaOpt) CamAltaDesfaz();
		if (semCulling) SetFootIkVisibilidade(false);
	}

	// ---------- parte 4 "medir": tamanho, triângulos, bases e cobertura do mapa (regras das arenas 10x10) ----------
	IEnumerator Medir()
	{
		FBSession.team = 0;
		yield return Carregar("10x10");
		var g = FBMatch.I;
		if (!g) { Log("medir: a partida não montou"); yield break; }
		yield return Wait(3f);
		DiagAgua();
		// 1) o mapa: caixa, renderizadores, materiais e triângulos (LOD0 e tudo), sem as malhas juntadas em tempo de jogo
		var caixa = new Bounds();
		bool temCaixa = false;
		int nRend = 0;
		long trisTudo = 0, trisLod0 = 0;
		var mats = new HashSet<Material>();
		var porGrupo = new Dictionary<string, long>();
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			var sc = SceneManager.GetSceneAt(i);
			if (!sc.isLoaded) continue;
			foreach (var raiz in sc.GetRootGameObjects())
			{
				if (!raiz.name.StartsWith("Map_")) continue;
				var foraLod0 = new HashSet<Renderer>();
				foreach (var lg in raiz.GetComponentsInChildren<LODGroup>(true))
				{
					var lods = lg.GetLODs();
					for (int l = 1; l < lods.Length; l++) foreach (var r in lods[l].renderers) if (r) foraLod0.Add(r);
				}
				foreach (var mr in raiz.GetComponentsInChildren<MeshRenderer>(true))
				{
					if (mr.name.StartsWith("juncao_")) continue;
					var mf = mr.GetComponent<MeshFilter>();
					var m = mf ? mf.sharedMesh : null;
					if (!m) continue;
					long t = 0;
					for (int sm = 0; sm < m.subMeshCount; sm++) t += m.GetIndexCount(sm) / 3;
					nRend++;
					trisTudo += t;
					if (!foraLod0.Contains(mr))
					{
						trisLod0 += t;
						var tr = mr.transform;
						while (tr.parent && tr.parent != raiz.transform) tr = tr.parent;
						string k = tr == raiz.transform ? raiz.name : tr.name;
						porGrupo.TryGetValue(k, out long n);
						porGrupo[k] = n + t;
					}
					foreach (var mt in mr.sharedMaterials) if (mt) mats.Add(mt);
					if (mr.gameObject.layer == FBWorld.LMap || mr.gameObject.layer == 0)
					{
						if (!temCaixa) { caixa = mr.bounds; temCaixa = true; } else caixa.Encapsulate(mr.bounds);
					}
				}
			}
		}
		Log($"medir {mapa}: caixa do mapa {caixa.size.x:0} x {caixa.size.z:0} m (altura {caixa.size.y:0} m), centro ({caixa.center.x:0}, {caixa.center.z:0}) · {nRend} renderizadores · {mats.Count} materiais · triângulos LOD0 {trisLod0:N0} · com todos os LODs {trisTudo:N0}");
		Log("medir: grupos com mais triângulos LOD0: " + string.Join("; ", porGrupo.OrderByDescending(x => x.Value).Take(10).Select(x => $"{x.Key} {x.Value:N0}")));
		// 2) bases: onde cada time nasce no 10x10, distância e linha de visão
		var bases = new Vector3[2];
		for (int t = 0; t < 2; t++)
		{
			var line = g.teamLine[t];
			bases[t] = line != null && line.spots.Count > 0 ? line.spots[0] : Vector3.zero;
			var web = Coords.ToWeb(bases[t]);
			Log($"medir: base do time {t} em ({bases[t].x:0}, {bases[t].y:0}, {bases[t].z:0}) · no fb_data [{web[0]:0}, {web[1]:0}, {web[2]:0}] · {line?.spots.Count ?? 0} lugares");
		}
		float dist = (bases[1] - bases[0]).Flat().magnitude;
		Vector3 o0 = bases[0] + Vector3.up * 1.6f, o1 = bases[1] + Vector3.up * 1.6f;
		bool visao = !FBWorld.RayHit(o0, (o1 - o0).normalized, (o1 - o0).magnitude - 0.5f);
		var path = new UnityEngine.AI.NavMeshPath();
		float andando = -1f;
		if (UnityEngine.AI.NavMesh.SamplePosition(bases[0], out var h0, 6f, -1) && UnityEngine.AI.NavMesh.SamplePosition(bases[1], out var h1, 6f, -1)
			&& UnityEngine.AI.NavMesh.CalculatePath(h0.position, h1.position, -1, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
		{
			andando = 0f;
			for (int i = 1; i < path.corners.Length; i++) andando += (path.corners[i] - path.corners[i - 1]).magnitude;
		}
		Log($"medir: bases a {dist:0} m em linha reta · {(andando > 0 ? $"{andando:0} m andando pelo NavMesh" : "sem caminho no NavMesh")} · uma base {(visao ? "ENXERGA" : "não enxerga")} a outra");
		if (andando > 0) Log("medir: caminho do NavMesh (fb_data [x, z]): " + string.Join(" ", path.corners.Select(c => { var w = Coords.ToWeb(c); return $"[{w[0]:0},{w[2]:0}]"; })));
		// 3) cobertura em volta de cada base: 64 raios a 1,2 m do chão, até 60 m
		for (int t = 0; t < 2; t++)
		{
			Vector3 o = bases[t] + Vector3.up * 1.2f;
			int ate10 = 0, ate20 = 0;
			float soma = 0f;
			for (int i = 0; i < 64; i++)
			{
				float a = i / 64f * Mathf.PI * 2f;
				var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
				float livre = FBWorld.RayHit(o, d, 60f, out var hit) ? hit.distance : 60f;
				soma += livre;
				if (livre <= 10f) ate10++;
				if (livre <= 20f) ate20++;
			}
			int nCol = Physics.OverlapSphere(bases[t], 25f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore).Length;
			Log($"medir: cobertura da base {t}: {ate10 * 100 / 64}% das direções bloqueadas até 10 m, {ate20 * 100 / 64}% até 20 m, distância livre média {soma / 64f:0} m, {nCol} colisores num raio de 25 m");
		}
		// 4) fotos de cima: o mapa inteiro e cada base (câmera ortográfica, norte para cima = +z)
		fotoCam.orthographic = true;
		fotoCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		float lado = Mathf.Max(caixa.size.x, caixa.size.z) * 0.5f + 10f;
		fotoCam.orthographicSize = lado;
		fotoCam.farClipPlane = 2000f;
		fotoCam.transform.position = new Vector3(caixa.center.x, caixa.max.y + 200f, caixa.center.z);
		Render($"medir_{mapa}_de_cima", 1600, 1600);
		Log($"medir: foto medir_{mapa}_de_cima.png: 1600 px = {2f * lado:0} m, centro ({caixa.center.x:0}, {caixa.center.z:0}), x para a direita, z para cima");
		for (int t = 0; t < 2; t++)
		{
			fotoCam.orthographicSize = 60f;
			fotoCam.transform.position = new Vector3(bases[t].x, bases[t].y + 150f, bases[t].z);
			Render($"medir_{mapa}_base{t}", 1200, 1200);
		}
		fotoCam.orthographic = false;
		yield return Wait(0.5f);
		// 5) v12c: GPU olhando de cada base para a outra, no mesmo .exe e na mesma hora.
		// Separa "lugar caro de desenhar" de "estado que sobrou de outra parte do teste" (exe F: 30 ms numa base, 11 ms na outra).
		var me = FBMatch.Player;
		var P = FBPlayer.I;
		if (me && P)
		{
			var cc = me.GetComponent<CharacterController>();
			var perf = new Desempenho();
			try
			{
				for (int t = 0; t < 2; t++)
				{
					Vector3 alvo = bases[1 - t] - bases[t];
					alvo.y = 0f;
					float yaw = Quaternion.LookRotation(alvo.sqrMagnitude > 1f ? alvo : Vector3.forward).eulerAngles.y;
					if (cc) cc.enabled = false;
					me.transform.position = bases[t];
					me.Yaw = yaw;
					if (cc) cc.enabled = true;
					P.OnRespawn(yaw);
					if (FBShop.Open) { FBShop.Show(false); Log("medir: loja estava aberta, fechei antes de medir"); }
					yield return Wait(2f);
					perf.Sincronizar();
					perf.Zerar();
					for (float s = 0f; s < 4f; s += Time.unscaledDeltaTime) { yield return null; perf.Quadro(Time.unscaledDeltaTime); }
					Log($"medir: GPU olhando da base {t} ({bases[t].x:0}, {bases[t].z:0}) para a base {1 - t}: " + perf.Resumo());
					Log($"medir: censo na base {t}: " + Censo());
					yield return Tela($"medir_{mapa}_gpu_base{t}");
				}
				// v17: A/B na mesma vista de cada base, um grupo de renderizadores desligado por vez. Depois das 2 medições de
				// base (que continuam na fase de compra, como na v12c), com o jogador imortal e a partida congelada o tempo todo
				// (senão a rodada começava no meio e os bots entravam na conta)
				bool god0 = FBMatch.God;
				bool frz0 = g.freeze;
				var fPh = g.mode != null ? g.mode.GetType().GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance) : null;
				FBMatch.God = true;
				try
				{
					for (int t = 0; t < 2; t++)
					{
						Vector3 alvo = bases[1 - t] - bases[t];
						alvo.y = 0f;
						float yaw = Quaternion.LookRotation(alvo.sqrMagnitude > 1f ? alvo : Vector3.forward).eulerAngles.y;
						if (cc) cc.enabled = false;
						me.transform.position = bases[t];
						me.Yaw = yaw;
						if (cc) cc.enabled = true;
						P.OnRespawn(yaw);
						if (FBShop.Open) FBShop.Show(false);
						g.freeze = true;
						for (float s2 = 0f; s2 < 2f; s2 += Time.unscaledDeltaTime) { g.freeze = true; yield return null; }
						yield return ABGrupos($"base {t}", perf, g);
					}
				}
				finally
				{
					FBMatch.God = god0;
					string fase = fPh?.GetValue(g.mode) as string;
					g.freeze = fase == "live" ? false : frz0;
				}
			}
			finally { perf.Dispose(); }
		}
		else Log("medir: sem jogador para medir a GPU nas bases");
	}

	static long Tris(Mesh m)
	{
		if (!m) return 0;
		long t = 0;
		for (int k = 0; k < m.subMeshCount; k++) t += m.GetIndexCount(k) / 3;
		return t;
	}

	// v17: água do mapa como o jogo carregou (malha, altura real dos vértices, material e shader), para achar a água em cacos da Orla
	void DiagAgua()
	{
		try
		{
			int n = 0, nFora = 0;
			Log("medir: água: materiais que o jogo trocou por água: " + (FBWater.Trocados.Count == 0 ? "nenhum" : string.Join("; ", FBWater.Trocados.Select(t => t.Key + " -> " + t.Value))));
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				var sc = SceneManager.GetSceneAt(i);
				if (!sc.isLoaded) continue;
				foreach (var raiz in sc.GetRootGameObjects())
				{
					if (!raiz.name.StartsWith("Map_")) continue;
					foreach (var mr in raiz.GetComponentsInChildren<MeshRenderer>(true))
					{
						string cam = mr.name;
						for (var t = mr.transform.parent; t && t != raiz.transform; t = t.parent) cam = t.name + "/" + cam;
						// olha todos os materiais do renderizador (o chão junta vários; antes só via o primeiro)
						Material mat = null;
						foreach (var mt in mr.sharedMaterials) if (mt && (mt.name.StartsWith("FBWater_") || mt.name.IndexOf("agua", StringComparison.OrdinalIgnoreCase) >= 0 || mt.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0)) { mat = mt; break; }
						if (!mat && (cam.Contains("Agua") || cam.Contains("Água"))) mat = mr.sharedMaterial;
						string mn = mat ? mat.name : "";
						if (!mat) continue;
						if (!cam.Contains("Agua") && !cam.Contains("Água")) nFora++;
						if (++n > 24) continue;
						var mf = mr.GetComponent<MeshFilter>();
						var m = mf ? mf.sharedMesh : null;
						string vy = "malha não legível";
						if (m && m.isReadable)
						{
							var vs = m.vertices;
							float a = float.MaxValue, b = float.MinValue;
							var l2w = mr.transform.localToWorldMatrix;
							foreach (var v in vs) { float y = l2w.MultiplyPoint3x4(v).y; if (y < a) a = y; if (y > b) b = y; }
							vy = vs.Length > 0 ? $"vértices y {a:0.00} a {b:0.00} ({b - a:0.00} m)" : "sem vértices";
						}
						var rb = mr.bounds;
						Log($"medir: água {raiz.name}/{cam} · ligado {mr.enabled && mr.gameObject.activeInHierarchy} · malha {(m ? m.name : "-")} v {(m ? m.vertexCount : 0)} tri {Tris(m)} · {vy} · caixa y {rb.min.y:0.00} a {rb.max.y:0.00} · lote estático {mr.isPartOfStaticBatch} · material {mn} shader {(mat && mat.shader ? mat.shader.name : "-")}");
					}
				}
			}
			Log($"medir: água: {n} renderizadores com água no mapa ({nFora} fora do grupo Agua)" + (n > 24 ? " (mostrei 24)" : ""));
			// foto das 2 maiores águas, de 18 m de altura olhando para o meio da caixa
			var grandes = new List<MeshRenderer>();
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				var sc = SceneManager.GetSceneAt(i);
				if (!sc.isLoaded) continue;
				foreach (var raiz in sc.GetRootGameObjects())
					if (raiz.name.StartsWith("Map_"))
						foreach (var mr in raiz.GetComponentsInChildren<MeshRenderer>(false))
						{
							var mat = mr.sharedMaterial;
							string mn = mat ? mat.name : "";
							if (mr.enabled && (mr.transform.parent && mr.transform.parent.name.Contains("Agua") || mn.IndexOf("agua", StringComparison.OrdinalIgnoreCase) >= 0)) grandes.Add(mr);
						}
			}
			int f = 0;
			foreach (var mr in grandes.OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).Take(2))
			{
				var b = mr.bounds;
				var alvo = new Vector3(b.center.x, b.max.y, b.center.z);
				fotoCam.transform.position = alvo + new Vector3(0f, 18f, -22f);
				fotoCam.transform.LookAt(alvo);
				Render($"medir_{mapa}_agua{++f}_{mr.name}", 1280, 720);
				Log($"medir: foto da água {f}: {mr.name} olhando ({alvo.x:0}, {alvo.y:0.0}, {alvo.z:0}) de 18 m de altura");
			}
		}
		catch (Exception e) { Log($"medir: água falhou: {e.GetType().Name} {e.Message}"); }
	}

	// v17: renderizadores ligados por grupo (raiz/filho, como no censo)
	Dictionary<string, List<Renderer>> GruposRender()
	{
		var g = new Dictionary<string, List<Renderer>>();
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			var sc = SceneManager.GetSceneAt(i);
			if (!sc.IsValid() || !sc.isLoaded) continue;
			foreach (var raiz in sc.GetRootGameObjects())
			{
				foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
				{
					if (!r.enabled || r.forceRenderingOff) continue;
					string k = raiz.name;
					var t = r.transform;
					while (t.parent && t.parent != raiz.transform) t = t.parent;
					if (t != raiz.transform) k += "/" + t.name;
					if (!g.TryGetValue(k, out var l)) g[k] = l = new List<Renderer>();
					l.Add(r);
				}
			}
		}
		return g;
	}

	// v17: A/B de GPU na vista atual, em pares: com o grupo ligado (3 s) e sem ele (3 s), 1 s para assentar antes de cada
	// medida, um grupo por vez, religando no fim. Só grupos do mapa (Map_*: os 5 maiores e os de água) e os pássaros: o
	// jogo não liga/desliga nada neles (vidro quebrado, decalques e faróis ficam fora). A partida fica congelada.
	IEnumerator ABGrupos(string onde, Desempenho perf, FBMatch g)
	{
		var grupos = GruposRender();
		bool doMapa(string k) => k.StartsWith("Map_");
		bool agua(string k) => k.Contains("Agua") || k.Contains("Água") || k.Contains("Water");
		var nomes = grupos.Where(x => doMapa(x.Key) && !x.Key.Contains("juncao")).OrderByDescending(x => x.Value.Count).Take(5).Select(x => x.Key).ToList();
		foreach (var k in grupos.Keys)
			if (!nomes.Contains(k) && ((doMapa(k) && agua(k)) || k.Contains("Passaros"))) nomes.Add(k);
		IEnumerator Mede(float seg)
		{
			for (float s = 0f; s < 1f; s += Time.unscaledDeltaTime) { if (g) g.freeze = true; yield return null; }
			perf.Sincronizar();
			perf.Zerar();
			for (float s = 0f; s < seg; s += Time.unscaledDeltaTime) { if (g) g.freeze = true; yield return null; perf.Quadro(Time.unscaledDeltaTime); }
		}
		foreach (var k in nomes)
		{
			var rs = grupos[k].Where(r => r && r.enabled).ToList();
			yield return Mede(3f);
			string com = perf.Resumo();
			foreach (var r in rs) r.enabled = false;
			yield return Mede(3f);
			string sem = perf.Resumo();
			foreach (var r in rs) if (r) r.enabled = true;
			Log($"medir A/B {onde}: {k} ({rs.Count} renderizadores) · com: {com}");
			Log($"medir A/B {onde}: {k} ({rs.Count} renderizadores) · sem: {sem}");
		}
	}

	// ---------- censo dos renderizadores: o que está ligado, por grupo do mapa ----------
	// Serve para achar o que liga de uma vez (na A1 as chamadas de desenho pularam de 11 mil para 28 mil
	// no começo da rodada 4 e ficaram assim). Compara com o censo anterior e mostra os grupos que mais mudaram.
	Dictionary<string, int> censoAnt;
	string Censo()
	{
		var atual = new Dictionary<string, int>();
		int ligados = 0, cortados = 0, comSombra = 0;
		var cenas = new List<Scene>();
		for (int i = 0; i < SceneManager.sceneCount; i++) cenas.Add(SceneManager.GetSceneAt(i));
		if (gameObject.scene.IsValid() && !cenas.Contains(gameObject.scene)) cenas.Add(gameObject.scene);
		foreach (var sc in cenas)
		{
			if (!sc.IsValid() || !sc.isLoaded) continue;
			foreach (var raiz in sc.GetRootGameObjects())
			{
				foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
				{
					if (!r.enabled) continue;
					string k = raiz.name;
					var t = r.transform;
					while (t.parent && t.parent != raiz.transform) t = t.parent;
					if (t != raiz.transform) k += "/" + t.name;
					if (r.forceRenderingOff) { cortados++; k += " (cortado)"; }
					else { ligados++; if (r.shadowCastingMode != ShadowCastingMode.Off) comSombra++; }
					atual.TryGetValue(k, out int n);
					atual[k] = n + 1;
				}
			}
		}
		var sb = new StringBuilder($"{ligados} renderizadores ligados ({comSombra} com sombra) e {cortados} cortados por distância");
		if (censoAnt != null)
		{
			var mud = atual.Keys.Union(censoAnt.Keys)
				.Select(k => (k, d: (atual.TryGetValue(k, out int a) ? a : 0) - (censoAnt.TryGetValue(k, out int b) ? b : 0)))
				.Where(x => Mathf.Abs(x.d) >= 50).OrderByDescending(x => Mathf.Abs(x.d)).Take(8).ToList();
			sb.Append(mud.Count == 0 ? " · nada mudou mais de 50" : " · mudou: " + string.Join("; ", mud.Select(x => $"{x.k} {(x.d > 0 ? "+" : "")}{x.d}")));
		}
		else
		{
			sb.Append(" · maiores grupos: " + string.Join("; ", atual.OrderByDescending(x => x.Value).Take(8).Select(x => $"{x.Key} {x.Value}")));
		}
		censoAnt = atual;
		return sb.ToString();
	}

	// ---------- parte 3: ajustes de desempenho medidos no meio do combate ----------
	static readonly string[] NomesNum = { "quadro ms", "CPU principal ms", "render thread ms", "GPU ms", "SetPass", "draw calls", "triângulos", "objetos com sombra", "scripts Update ms", "scripts LateUpdate ms", "render principal ms" };

	IEnumerator PerfAjustes()
	{
		FBSession.team = 0;
		yield return Carregar("10x10");
		var g = FBMatch.I;
		if (!g || g.mode == null) { Log("perf: a partida não montou"); yield break; }
		var fPhase = g.mode.GetType().GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
		string Fase() => fPhase?.GetValue(g.mode) as string ?? "?";
		var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		if (!urp) { Log("perf: sem URP asset"); yield break; }
		var cam0 = Camera.main;
		var P = FBQuality.P;
		// valores de agora (o "jeito atual")
		float sd0 = urp.shadowDistance, rs0 = urp.renderScale, lod0 = QualitySettings.lodBias, far0 = cam0 ? cam0.farClipPlane : 1400f, pe0 = P.footIKDistance;
		int sc0 = urp.shadowCascadeCount;
		var up0 = urp.upscalingFilter;
		Log($"perf: qualidade {FBQuality.Level} · sombra {sd0:0} m com {sc0} cascatas · escala {rs0:0.00} ({up0}) · lodBias {lod0:0.0} · câmera até {far0:0} m · FootIK até {pe0:0} m · sombra só em objeto com meia diagonal acima de {P.smallCasterSize:0.0} m");
		var desfazSombra = new List<(Renderer, ShadowCastingMode)>();
		bool avisouPoda = false;
		void Aplicar(string c)
		{
			bool tudo = c == "tudo";
			urp.shadowDistance = (tudo || c == "sombra80") ? Mathf.Min(sd0, 80f) : sd0;
			urp.shadowCascadeCount = (tudo || c == "sombra80") ? Mathf.Min(sc0, 3) : sc0;
			urp.upscalingFilter = (tudo || c == "stp") ? UpscalingFilterSelection.STP : up0;
			urp.renderScale = (tudo || c == "stp") ? 0.77f : rs0;
			QualitySettings.lodBias = (tudo || c == "lod18") ? Mathf.Min(lod0, 1.8f) : lod0;
			var cam = Camera.main;
			if (cam) cam.farClipPlane = (tudo || c == "longe700") ? Mathf.Min(far0, 700f) : far0;
			P.footIKDistance = (tudo || c == "pe25") ? Mathf.Min(pe0, 25f) : pe0;
			bool peq = tudo || c == "sombrapeq";
			if (peq && desfazSombra.Count == 0)
			{
				int nPodou = 0;
				for (int i = 0; i < SceneManager.sceneCount; i++) nPodou += FBQuality.PruneCasters(SceneManager.GetSceneAt(i), 0.6f, desfazSombra);
				if (!avisouPoda) { avisouPoda = true; Log($"perf: sombrapeq tirou a sombra de {nPodou} objetos menores que ~1 m"); }
			}
			if (!peq && desfazSombra.Count > 0)
			{
				foreach (var (r, m) in desfazSombra) if (r) r.shadowCastingMode = m;
				desfazSombra.Clear();
			}
		}
		for (float w = 0f; w < 90f && Fase() != "live"; w += Time.unscaledDeltaTime) yield return null;
		yield return Wait(8f);
		var perf = new Desempenho();
		yield return null;
		perf.Sincronizar();
		Log("perf: " + perf.Estado());
		Log("perf: censo " + Censo());
		perf.Sincronizar();
		var ajustes = new[] { "sombra80", "stp", "lod18", "longe700", "pe25", "sombrapeq", "tudo" };
		var bases = ajustes.ToDictionary(a => a, a => new List<double[]>());
		var comAjuste = ajustes.ToDictionary(a => a, a => new List<double[]>());
		float limite = 900f, total = 0f;
		double[] ultimo = null;
		// uma janela de 5 s com a configuração c; "ultimo" fica null se o combate não estava rolando a janela toda
		IEnumerator MedirJanela(string c)
		{
			ultimo = null;
			Aplicar(c);
			yield return Wait(1f);
			if (Fase() != "live") yield break;
			perf.Sincronizar();
			perf.Zerar();
			for (float t = 0f; t < 5f; t += Time.unscaledDeltaTime) { yield return null; perf.Quadro(Time.unscaledDeltaTime); }
			if (Fase() == "live") ultimo = perf.Numeros();
		}
		for (int rep = 0; rep < 3 && total < limite; rep++)
		{
			foreach (var a in ajustes)
			{
				if (!FBMatch.I || total >= limite) break;
				float t0 = Time.realtimeSinceStartup;
				while (Fase() != "live" && Time.realtimeSinceStartup - t0 < 60f) yield return null;
				// alterna a ordem a cada repetição para o fim da rodada (menos bots vivos) não pesar só de um lado
				double[] nb = null, na = null;
				if (rep % 2 == 0) { yield return MedirJanela("base"); nb = ultimo; yield return MedirJanela(a); na = ultimo; }
				else { yield return MedirJanela(a); na = ultimo; yield return MedirJanela("base"); nb = ultimo; }
				if (nb != null && na != null) { bases[a].Add(nb); comAjuste[a].Add(na); }
				total += Time.realtimeSinceStartup - t0;
				if (rep == 0 && (a == "stp" || a == "tudo"))
				{
					Aplicar(a);
					yield return Wait(0.5f);
					yield return Tela($"perf_{a}");
					Aplicar("base");
					yield return Wait(0.5f);
					yield return Tela($"perf_{a}_base");
				}
			}
		}
		Aplicar("base");
		Log("perf: censo no fim " + Censo());
		perf.Dispose();
		double[] Media(List<double[]> l) => l.Count == 0 ? null : Enumerable.Range(0, NomesNum.Length).Select(i => l.Average(x => x[i])).ToArray();
		foreach (var a in ajustes)
		{
			var mb = Media(bases[a]);
			var ma = Media(comAjuste[a]);
			if (mb == null || ma == null) { Log($"perf {a}: sem janelas no combate"); continue; }
			var sb = new StringBuilder($"perf {a} ({comAjuste[a].Count} janelas de 5 s): ");
			for (int i = 0; i < NomesNum.Length; i++)
			{
				string f = i >= 4 && i <= 7 ? "0" : "0.00";
				sb.Append($"{NomesNum[i]} {mb[i].ToString(f)} → {ma[i].ToString(f)}{(i < NomesNum.Length - 1 ? " · " : "")}");
			}
			Log(sb.ToString());
		}
	}

	// ---------- parte "deslize": o pé que está no chão escorrega quando o bot mira e anda? ----------
	// A cada quadro, em todos os bots vivos, mede a velocidade do pé apoiado (o mais baixo, que não está
	// subindo nem descendo). Pé bem animado fica parado no chão (perto de 0 m/s); escorregando, ele anda
	// junto com o corpo. Alterna janelas de 20 s: "antes" (como era), "pernas" (pernas na velocidade real)
	// e "pernas+mira" (mais o limite de velocidade mirando), e grava um vídeo de lado de um bot mirando.
	static readonly MethodInfo mRootSpeed = typeof(FBCharacter).GetMethod("BaseRootSpeed", BindingFlags.NonPublic | BindingFlags.Instance);

	class PesBot { public Transform l, r; public Vector3 pl, pr, corpo; public int baixo = -1; public bool ok; }

	class Balde { public int n, acima, sat; public double desl, corpo, falta; public readonly Dictionary<string, int> clipes = new Dictionary<string, int>(); }

	static readonly (string nome, int passo, int mira)[] ModosDeslize = { ("antes", 0, 0), ("pernas", 1, 0), ("pernas+mira", 1, 1) };

	static readonly string[] DirsDeslize = { "frente", "diagonal frente", "lado", "diagonal trás", "trás" };

	IEnumerator Deslize()
	{
		FBSession.team = 0;
		yield return Carregar("10x10");
		var g = FBMatch.I;
		if (!g || g.mode == null) { Log("deslize: a partida não montou"); yield break; }
		var fPhase = g.mode.GetType().GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
		string Fase() => fPhase?.GetValue(g.mode) as string ?? "?";
		if (mRootSpeed == null) Log("deslize: AVISO não achei FBCharacter.BaseRootSpeed (a coluna 'falta de passo' fica 0)");
		var amostra = FBMatch.All.FirstOrDefault(c => c && !c.isPlayer && c.an && c.an.runtimeAnimatorController);
		if (amostra)
		{
			var aoc = amostra.an.runtimeAnimatorController as AnimatorOverrideController;
			string baseNome = aoc && aoc.runtimeAnimatorController ? aoc.runtimeAnimatorController.name : amostra.an.runtimeAnimatorController.name;
			Log($"deslize: controlador {baseNome} · ScaleK {amostra.ScaleK:0.00} · humanScale {amostra.an.humanScale:0.00} · escala {amostra.transform.lossyScale.y:0.00} · humanoide {amostra.an.isHuman}");
			var nomes = new[] { "Rifle_WalkFwdLoop", "Rifle_StrafeRightLoop", "Rifle_StrafeLeftLoop", "Rifle_WalkBwdLoop", "Walk Forward Right", "Walk Backward Right", "Rifle_RunFwdLoop", "Rifle_RunBwdLoop", "Run Forward Right", "Run Forward Left", "RunRtLoop", "RunLtLoop", "RunStrafeRight135Loop", "RunStrafeLeft135Loop", "Walk Crouching Forward", "Crouch Walk Strafe Right", "Crouch Walk Strafe Left", "Walk Crouching Backward 1" };
			var sb = new StringBuilder("deslize: clipes (base → tocando, velocidade do clipe em m/s): ");
			foreach (var n in nomes)
			{
				AnimationClip clip = null;
				if (aoc) { try { clip = aoc[n]; } catch { } }
				sb.Append($"{n} → {amostra.OverrideOf(n)} {(clip ? FBCharacter.RootSpeedOf(clip).ToString("0.00") : "?")} · ");
			}
			Log(sb.ToString());
		}
		for (float w = 0f; w < 90f && Fase() != "live"; w += Time.unscaledDeltaTime) yield return null;
		yield return Wait(3f);
		SemCulling(FBMatch.All.Where(x => x).ToList(), true);
		var pes = new Dictionary<FBCharacter, PesBot>();
		var baldes = new Dictionary<string, Balde>();
		var clipInfo = new List<AnimatorClipInfo>();
		void Somar(string k, float desl, float sCorpo, float falta, bool sat, string clipe)
		{
			if (!baldes.TryGetValue(k, out var b)) baldes[k] = b = new Balde();
			b.n++; b.desl += desl; b.corpo += sCorpo; b.falta += falta;
			if (desl > 0.8f) b.acima++;
			if (sat) b.sat++;
			b.clipes.TryGetValue(clipe, out int q);
			b.clipes[clipe] = q + 1;
		}
		void Quadro(string modo, float dt)
		{
			bool dtOk = dt > 0f && dt < 0.1f;
			foreach (var c in FBMatch.All)
			{
				if (!c || c.isPlayer || c.team > 1 || !c.an) continue;
				if (!pes.TryGetValue(c, out var p))
				{
					p = new PesBot();
					if (c.an.isHuman) { p.l = c.an.GetBoneTransform(HumanBodyBones.LeftFoot); p.r = c.an.GetBoneTransform(HumanBodyBones.RightFoot); }
					pes[c] = p;
				}
				if (!p.l || !p.r) continue;
				if (!c.alive) { p.ok = false; p.baixo = -1; continue; }
				Vector3 pl = p.l.position, pr = p.r.position, corpo = c.transform.position;
				int baixo = pl.y <= pr.y ? 0 : 1;
				if (dtOk && p.ok && c.grounded && !c.knocked && c.sys == null && !c.special && c.an.enabled && baixo == p.baixo)
				{
					Vector3 vCorpo = (corpo - p.corpo) / dt;
					vCorpo.y = 0f;
					float sCorpo = vCorpo.magnitude;
					Vector3 atual = baixo == 0 ? pl : pr, antes = baixo == 0 ? p.pl : p.pr;
					float vy = Mathf.Abs(atual.y - antes.y) / dt;
					if (sCorpo > 1f && sCorpo < 12f && vy < 0.35f)
					{
						Vector3 vPe = (atual - antes) / dt;
						vPe.y = 0f;
						float desl = vPe.magnitude;
						Vector3 loc = Quaternion.Inverse(Quaternion.Euler(0f, c.Yaw, 0f)) * vCorpo;
						float ang = Mathf.Abs(Mathf.Atan2(loc.x, loc.z) * Mathf.Rad2Deg);
						string dir = ang < 25f ? "frente" : ang < 65f ? "diagonal frente" : ang < 115f ? "lado" : ang < 155f ? "diagonal trás" : "trás";
						string faixa = sCorpo < 2f ? "1-2" : sCorpo < 3f ? "2-3" : sCorpo < 4f ? "3-4" : "4+";
						float rs = 0f;
						if (mRootSpeed != null) { try { rs = (float)mRootSpeed.Invoke(c, null) * c.an.humanScale * c.transform.lossyScale.y; } catch { } }
						float falta = rs > 0.01f ? Mathf.Max(0f, sCorpo - rs * c.LocoRate) : 0f;
						string clipe = "?";
						float wmax = 0f;
						c.an.GetCurrentAnimatorClipInfo(0, clipInfo);
						foreach (var ci in clipInfo) if (ci.clip && ci.weight > wmax) { wmax = ci.weight; clipe = ci.clip.name; }
						string mira = c.aiming ? "mirando" : "sem mirar";
						string post = c.crouch ? "agachado" : "em pé";
						bool sat = c.LocoRate >= 1.445f;
						Somar($"{modo}|*|*|*|*", desl, sCorpo, falta, sat, clipe);
						Somar($"{modo}|{mira}|{post}|*|*", desl, sCorpo, falta, sat, clipe);
						Somar($"{modo}|{mira}|{post}|{dir}|*", desl, sCorpo, falta, sat, clipe);
						Somar($"{modo}|{mira}|{post}|{dir}|{faixa}", desl, sCorpo, falta, sat, clipe);
					}
				}
				p.baixo = baixo; p.pl = pl; p.pr = pr; p.corpo = corpo; p.ok = true;
			}
		}
		float limite = 720f, total = 0f;
		var videoFeito = new HashSet<string>();
		for (int rep = 0; rep < rodadas && total < limite && FBMatch.I; rep++)
		{
			for (int k = 0; k < ModosDeslize.Length && total < limite && FBMatch.I; k++)
			{
				var m = ModosDeslize[(k + rep) % ModosDeslize.Length];
				FBBot.PassoTeste = m.passo;
				FBBot.MiraTeste = m.mira;
				float t0 = Time.realtimeSinceStartup;
				while (FBMatch.I && Fase() != "live" && Time.realtimeSinceStartup - t0 < 60f) yield return null;
				if (!FBMatch.I || Fase() != "live") { total += Time.realtimeSinceStartup - t0; continue; }
				yield return Wait(1.5f);
				foreach (var p in pes.Values) p.ok = false;
				int quadros = 0;
				for (float t = 0f; t < 20f && FBMatch.I && Fase() == "live"; t += Time.deltaTime) { yield return new WaitForEndOfFrame(); Quadro(m.nome, Time.deltaTime); quadros++; }
				Log($"deslize: janela {m.nome} (repetição {rep + 1}) · {quadros} quadros · rodada {g.mode.Round}");
				if (!videoFeito.Contains(m.nome) && FBMatch.I && Fase() == "live")
				{
					videoFeito.Add(m.nome);
					yield return Video(m.nome);
				}
				total += Time.realtimeSinceStartup - t0;
			}
		}
		FBBot.PassoTeste = -1;
		FBBot.MiraTeste = -1;
		string Linha(string key)
		{
			if (!baldes.TryGetValue(key, out var b) || b.n == 0) return "sem amostras";
			string top = string.Join(", ", b.clipes.OrderByDescending(x => x.Value).Take(2).Select(x => $"{x.Key} {100.0 * x.Value / b.n:0}%"));
			return $"{b.n} amostras · pé no chão escorrega {b.desl / b.n:0.00} m/s · {100.0 * b.acima / b.n:0}% dos quadros acima de 0,8 m/s · corpo {b.corpo / b.n:0.0} m/s · animação no limite (1,45x) {100.0 * b.sat / b.n:0}% · falta de passo {b.falta / b.n:0.00} m/s · clipe {top}";
		}
		foreach (var m in ModosDeslize) Log($"deslize {m.nome} · todos andando: {Linha($"{m.nome}|*|*|*|*")}");
		foreach (var mira in new[] { "mirando", "sem mirar" })
			foreach (var post in new[] { "em pé", "agachado" })
				foreach (var m in ModosDeslize) Log($"deslize {m.nome} · {mira} {post}: {Linha($"{m.nome}|{mira}|{post}|*|*")}");
		foreach (var post in new[] { "em pé", "agachado" })
			foreach (var dirx in DirsDeslize)
				foreach (var m in ModosDeslize) Log($"deslize {m.nome} · mirando {post} · {dirx}: {Linha($"{m.nome}|mirando|{post}|{dirx}|*")}");
		try
		{
			var csv = new StringBuilder("modo;mira;postura;direcao;faixa_m_s;amostras;pe_escorrega_m_s;pct_acima_0_8;corpo_m_s;pct_animacao_no_limite;falta_de_passo_m_s;clipe_principal\n");
			foreach (var kv in baldes.OrderBy(x => x.Key))
			{
				var b = kv.Value;
				var top = b.clipes.OrderByDescending(x => x.Value).FirstOrDefault();
				csv.AppendLine(kv.Key.Replace('|', ';') + $";{b.n};{b.desl / b.n:0.000};{100.0 * b.acima / b.n:0.0};{b.corpo / b.n:0.00};{100.0 * b.sat / b.n:0.0};{b.falta / b.n:0.000};{top.Key}");
			}
			File.WriteAllText(Path.Combine(dir, "deslize.csv"), csv.ToString(), new UTF8Encoding(false));
		}
		catch (Exception e) { Log("deslize.csv não gravou: " + e.Message); }
	}

	// vídeo curto de lado (câmera parada) de um bot mirando e andando; quadros em JPG + quadros.csv
	IEnumerator Video(string modo)
	{
		// v9: só filma bot que tem um lugar de câmera com visão livre (sem parede entre a câmera e o corpo/pés)
		FBCharacter alvo = null;
		Vector3 camPos = Vector3.zero;
		for (float w = 0f; w < 15f && !alvo; w += Time.unscaledDeltaTime)
		{
			var cands = FBMatch.All.Where(c => c && !c.isPlayer && c.alive && c.aiming && !c.crouch && c.grounded && new Vector2(c.velocity.x, c.velocity.z).magnitude > 2f)
				.OrderByDescending(c => Mathf.Abs((Quaternion.Inverse(Quaternion.Euler(0f, c.Yaw, 0f)) * c.velocity).x)).Take(8).ToList();
			foreach (var c in cands)
				if (LugarCamera(c, out camPos)) { alvo = c; break; }
			if (!alvo) yield return null;
		}
		if (!alvo) { Log($"deslize {modo}: nenhum bot mirando e andando com visão livre para filmar"); yield break; }
		string pasta = Path.Combine(dir, "video_" + modo.Replace("+", "_"));
		Directory.CreateDirectory(pasta);
		Vector3 olhar = alvo.transform.position + Vector3.up * 0.55f;
		float fov0 = fotoCam.fieldOfView;
		fotoCam.fieldOfView = 40f;
		fotoCam.transform.position = camPos;
		fotoCam.transform.LookAt(olhar);
		int tapados = 0, trocas = 0;
		var lF = alvo.an && alvo.an.isHuman ? alvo.an.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
		var rF = alvo.an && alvo.an.isHuman ? alvo.an.GetBoneTransform(HumanBodyBones.RightFoot) : null;
		Vector3 pl0 = lF ? lF.position : Vector3.zero, pr0 = rF ? rF.position : Vector3.zero, c0 = alvo.transform.position;
		var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
		var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
		var linhas = new StringBuilder("quadro;tempo_s;corpo_m_s;pe_apoiado_m_s;mirando;locorate;visao_livre\n");
		bool livreQ = true;
		float t = 0f;
		int q = 0;
		try
		{
			for (; q < 90 && alvo && alvo.alive; q++)
			{
				yield return new WaitForEndOfFrame();
				float dt = Mathf.Max(0.0001f, Time.deltaTime);
				t += dt;
				Vector3 alvoP = alvo.transform.position + Vector3.up * 0.55f;
				bool livre = VisaoLivre(fotoCam.transform.position, alvo.transform.position + Vector3.up * 1.1f) && VisaoLivre(fotoCam.transform.position, alvo.transform.position + Vector3.up * 0.15f);
				if (!livre || (fotoCam.transform.position - alvoP).magnitude > 9f)
				{
					if (LugarCamera(alvo, out var np)) { fotoCam.transform.position = np; olhar = alvoP; trocas++; livre = true; }
				}
				if (!livre) tapados++;
				if ((alvoP - olhar).magnitude > 2.5f) olhar = Vector3.Lerp(olhar, alvoP, 0.15f);
				fotoCam.transform.LookAt(olhar);
				var req = new RenderPipeline.StandardRequest { destination = rt };
				if (RenderPipeline.SupportsRenderRequest(fotoCam, req)) RenderPipeline.SubmitRenderRequest(fotoCam, req);
				else { fotoCam.targetTexture = rt; fotoCam.Render(); fotoCam.targetTexture = null; }
				var prev = RenderTexture.active;
				RenderTexture.active = rt;
				tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
				tex.Apply();
				RenderTexture.active = prev;
				File.WriteAllBytes(Path.Combine(pasta, q.ToString("000") + ".jpg"), tex.EncodeToJPG(88));
				livreQ = livre;
				Vector3 c1 = alvo.transform.position;
				float corpo = new Vector2(c1.x - c0.x, c1.z - c0.z).magnitude / dt;
				float pe = 0f;
				if (lF && rF)
				{
					Vector3 pl = lF.position, pr = rF.position;
					Vector3 d = pl.y <= pr.y ? pl - pl0 : pr - pr0;
					pe = new Vector2(d.x, d.z).magnitude / dt;
					pl0 = pl; pr0 = pr;
				}
				c0 = c1;
				linhas.AppendLine($"{q};{t:0.000};{corpo:0.00};{pe:0.00};{(alvo.aiming ? 1 : 0)};{alvo.LocoRate:0.00};{(livreQ ? 1 : 0)}");
			}
		}
		finally
		{
			rt.Release();
			Object.Destroy(rt);
			Object.Destroy(tex);
			fotoCam.fieldOfView = fov0;
		}
		File.WriteAllText(Path.Combine(pasta, "quadros.csv"), linhas.ToString());
		Log($"deslize {modo}: vídeo de {q} quadros ({t:0.0} s de jogo) do bot {(alvo ? alvo.name : "?")} em {pasta} · quadros com parede na frente {tapados} · trocas de câmera {trocas}");
	}

	// v9: linha livre entre dois pontos, ignorando os próprios personagens e gatilhos
	static bool VisaoLivre(Vector3 de, Vector3 para)
	{
		Vector3 d = para - de;
		float dist = d.magnitude;
		if (dist < 0.3f) return true;
		var hits = Physics.RaycastAll(de, d / dist, dist - 0.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
		foreach (var h in hits)
		{
			if (!h.collider) continue;
			if (h.collider.GetComponentInParent<FBCharacter>()) continue;
			return false;
		}
		return true;
	}

	// v9: procura um lugar de câmera de lado (ou meio de lado) com visão livre do peito e dos pés do bot
	static bool LugarCamera(FBCharacter alvo, out Vector3 pos)
	{
		pos = Vector3.zero;
		if (!alvo) return false;
		Vector3 v = alvo.velocity;
		v.y = 0f;
		Vector3 frente = v.sqrMagnitude > 0.01f ? v.normalized : alvo.transform.forward;
		Vector3 b = alvo.transform.position;
		Vector3 peito = b + Vector3.up * 1.1f, pes = b + Vector3.up * 0.15f;
		float[] angs = { 90f, -90f, 60f, -60f, 120f, -120f };
		float[] dists = { 6.5f, 5f, 4f, 3.2f };
		float[] alts = { 1.45f, 2.1f, 1.0f };
		foreach (float dd in dists)
			foreach (float a in angs)
				foreach (float h in alts)
				{
					Vector3 dirc = Quaternion.Euler(0f, a, 0f) * frente;
					Vector3 c = b + dirc * dd + Vector3.up * h;
					bool dentro = false;
					foreach (var col in Physics.OverlapSphere(c, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
						if (col && !col.GetComponentInParent<FBCharacter>()) { dentro = true; break; }
					if (dentro) continue;
					if (!VisaoLivre(c, peito) || !VisaoLivre(c, pes)) continue;
					pos = c;
					return true;
				}
		return false;
	}

	// ---------- opção "semculling": nada depende de estar na tela ----------
	void SemCulling(List<FBCharacter> lista, bool avisa)
	{
		int n = 0;
		foreach (var c in lista)
		{
			if (!c) continue;
			foreach (var an in c.GetComponentsInChildren<Animator>(true))
			{
				if (an.cullingMode != AnimatorCullingMode.AlwaysAnimate) { an.cullingMode = AnimatorCullingMode.AlwaysAnimate; n++; }
			}
		}
		FBRig.TorsoClear = false;
		bool ik = SetFootIkVisibilidade(true);
		if (avisa) Log($"semculling: {n} Animator passaram para AlwaysAnimate · correção da arma no peito desligada · FootIK ignorando a tela: {(ik ? "sim" : "NÃO ACHEI")}");
	}

	static bool SetFootIkVisibilidade(bool ignora)
	{
		var q = typeof(FBFootIK).GetNestedType("Q", BindingFlags.Public | BindingFlags.NonPublic);
		var f = q?.GetField("IgnoreVisibility", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		if (f == null) return false;
		f.SetValue(null, ignora);
		return true;
	}

	// ---------- opção "camalta": câmera parada no alto do meio do mapa ----------
	Camera camAlta, camOrig;
	void CamAlta(Vector3 b0, Vector3 b1)
	{
		Vector3 centro = (b0 + b1) * 0.5f;
		Vector3 eixo = b1 - b0; eixo.y = 0f;
		float distBases = eixo.magnitude;
		camOrig = Camera.main;
		var go = new GameObject("FB_CamAltaTeste");
		go.tag = "MainCamera";
		camAlta = go.AddComponent<Camera>();
		var urp = go.AddComponent<UniversalAdditionalCameraData>();
		urp.renderPostProcessing = true;
		if (camOrig)
		{
			camAlta.cullingMask = camOrig.cullingMask;
			camAlta.farClipPlane = camOrig.farClipPlane;
			camOrig.enabled = false;
		}
		camAlta.fieldOfView = 60f;
		camAlta.nearClipPlane = 0.3f;
		// a linha entre as bases fica na largura da tela, e a altura é a que cabe as duas bases com 15 m de folga
		float aspecto = Mathf.Max(1f, (float)Screen.width / Mathf.Max(1, Screen.height));
		float altura = Mathf.Clamp((distBases * 0.5f + 15f) / (Mathf.Tan(30f * Mathf.Deg2Rad) * aspecto), 40f, 220f);
		float yaw = (eixo.sqrMagnitude > 0.01f ? Mathf.Atan2(eixo.x, eixo.z) * Mathf.Rad2Deg : 90f) - 90f;
		go.transform.SetPositionAndRotation(centro + Vector3.up * (altura + centro.y), Quaternion.Euler(90f, yaw, 0f));
		Vector3 v0 = camAlta.WorldToViewportPoint(b0), v1 = camAlta.WorldToViewportPoint(b1);
		bool cabem = v0.x > 0f && v0.x < 1f && v0.y > 0f && v0.y < 1f && v1.x > 0f && v1.x < 1f && v1.y > 0f && v1.y < 1f;
		Log($"camalta: câmera do jogo desligada, câmera parada a {altura:0} m acima do meio do mapa ({centro.x:0}, {centro.z:0}), girada {yaw:0}° · as duas bases na tela: {(cabem ? "sim" : "NÃO")} · Camera.main agora é {(Camera.main ? Camera.main.name : "nenhuma")}");
	}

	void CamAltaManter()
	{
		if (camOrig && camOrig.enabled) camOrig.enabled = false;
		if (camAlta && !camAlta.enabled) camAlta.enabled = true;
	}

	void CamAltaDesfaz()
	{
		if (camAlta) Destroy(camAlta.gameObject);
		if (camOrig) camOrig.enabled = true;
	}

	// ---------- desempenho sem o Profiler (funciona no .exe normal) ----------
	// Põe marcas de tempo no PlayerLoop em volta das partes do quadro (scripts, Animator, física, render...),
	// lê CPU principal / render thread / GPU do FrameTimingManager e os contadores de render (batches, SetPass, triângulos).
	sealed class Desempenho : IDisposable
	{
		static readonly (string tipo, string nome, string csv)[] Partes =
		{
			("Initialization", "começo do quadro", "inicio_ms"),
			("EarlyUpdate", "EarlyUpdate", "earlyupdate_ms"),
			("FixedUpdate", "FixedUpdate inteiro", "fixedupdate_ms"),
			("PhysicsFixedUpdate", "física", "fisica_ms"),
			("ScriptRunBehaviourFixedUpdate", "scripts no FixedUpdate", "scripts_fixed_ms"),
			("PreUpdate", "PreUpdate", "preupdate_ms"),
			("Update", "Update inteiro", "update_ms"),
			("ScriptRunBehaviourUpdate", "scripts no Update (bots, jogador, partida)", "scripts_update_ms"),
			("ScriptRunDelayedDynamicFrameRate", "corrotinas", "corrotinas_ms"),
			("PreLateUpdate", "PreLateUpdate inteiro", "prelateupdate_ms"),
			("DirectorUpdateAnimationBegin", "Animator (começo)", "animator_comeco_ms"),
			("DirectorUpdateAnimationEnd", "Animator (fim)", "animator_fim_ms"),
			("ScriptRunBehaviourLateUpdate", "scripts no LateUpdate (rig, FootIK, câmera)", "scripts_late_ms"),
			("PostLateUpdate", "PostLateUpdate inteiro", "postlateupdate_ms"),
			("UpdateAllSkinnedMeshes", "malhas com osso", "skinning_ms"),
			("PlayerUpdateCanvases", "interface", "interface_ms"),
			("FinishFrameRendering", "render na thread principal", "render_principal_ms"),
		};
		static readonly (ProfilerCategory cat, string stat, string nome, string csv)[] Contadores =
		{
			(ProfilerCategory.Render, "Batches Count", "batches", "batches"),
			(ProfilerCategory.Render, "SetPass Calls Count", "SetPass", "setpass"),
			(ProfilerCategory.Render, "Draw Calls Count", "draw calls", "draw_calls"),
			(ProfilerCategory.Render, "Triangles Count", "triângulos", "triangulos"),
			(ProfilerCategory.Render, "Shadow Casters Count", "objetos fazendo sombra", "sombras"),
			(ProfilerCategory.Render, "Visible Skinned Meshes Count", "malhas com osso visíveis", "malhas_osso_visiveis"),
			(ProfilerCategory.Memory, "GC Allocated In Frame", "lixo de memória por quadro (bytes)", "gc_bytes"),
		};

		static long[] soma = new long[Partes.Length], ini = new long[Partes.Length];
		readonly long[] visto = new long[Partes.Length];
		readonly bool[] achou = new bool[Partes.Length];
		readonly PlayerLoopSystem original;
		readonly bool loopOk;
		readonly string loopErro;
		readonly ProfilerRecorder[] rec = new ProfilerRecorder[Contadores.Length];
		readonly FrameTiming[] ftm = new FrameTiming[1];
		static readonly double msPorTick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

		sealed class Janela
		{
			public int n, nT;
			public double quadro, cpu, render, gpu;
			public readonly double[] fase = new double[Partes.Length];
			public readonly double[] cont = new double[Contadores.Length];
			public readonly int[] contN = new int[Contadores.Length];
			public readonly List<float> qs = new List<float>(4000);
		}
		Janela fase = new Janela(), seg = new Janela();

		public Desempenho()
		{
			try
			{
				original = PlayerLoop.GetCurrentPlayerLoop();
				var raiz = original;
				var topo = (PlayerLoopSystem[])raiz.subSystemList.Clone();
				for (int i = 0; i < topo.Length; i++)
				{
					var t = topo[i];
					var subs = new List<PlayerLoopSystem>();
					if (t.subSystemList != null)
					{
						foreach (var s in t.subSystemList)
						{
							int id = Indice(s.type);
							if (id >= 0) { subs.Add(Marca(id, false)); subs.Add(s); subs.Add(Marca(id, true)); achou[id] = true; }
							else subs.Add(s);
						}
					}
					int idT = Indice(t.type);
					if (idT >= 0) { subs.Insert(0, Marca(idT, false)); subs.Add(Marca(idT, true)); achou[idT] = true; }
					t.subSystemList = subs.ToArray();
					topo[i] = t;
				}
				raiz.subSystemList = topo;
				PlayerLoop.SetPlayerLoop(raiz);
				loopOk = true;
			}
			catch (Exception e) { loopErro = e.Message; }
			for (int i = 0; i < Contadores.Length; i++)
			{
				try { rec[i] = ProfilerRecorder.StartNew(Contadores[i].cat, Contadores[i].stat); } catch { }
			}
		}

		static int Indice(Type ty)
		{
			if (ty == null) return -1;
			for (int i = 0; i < Partes.Length; i++) if (Partes[i].tipo == ty.Name) return i;
			return -1;
		}

		static PlayerLoopSystem Marca(int id, bool fim)
		{
			PlayerLoopSystem.UpdateFunction f;
			if (fim) f = () => soma[id] += System.Diagnostics.Stopwatch.GetTimestamp() - ini[id];
			else f = () => ini[id] = System.Diagnostics.Stopwatch.GetTimestamp();
			return new PlayerLoopSystem { type = typeof(Desempenho), updateDelegate = f };
		}

		public string Estado()
		{
			var falta = Partes.Where((p, i) => !achou[i]).Select(p => p.tipo).ToList();
			var semCont = Contadores.Where((c, i) => !rec[i].Valid).Select(c => c.stat).ToList();
			return $"marcas no PlayerLoop {(loopOk ? "ok" : "FALHOU: " + loopErro)}" + (falta.Count > 0 ? " (não achei: " + string.Join(", ", falta) + ")" : "")
				+ $" · FrameTimingManager {(FrameTimingManager.IsFeatureEnabled() ? "ligado" : "DESLIGADO (sem CPU/GPU separados)")}"
				+ (semCont.Count > 0 ? " · contadores sem leitura: " + string.Join(", ", semCont) : " · contadores de render ok");
		}

		public IEnumerable<string> NomesCsv() => Contadores.Select(c => c.csv).Concat(Partes.Select(p => p.csv));

		public void Quadro(float dt)
		{
			FrameTimingManager.CaptureFrameTimings();
			bool temT = FrameTimingManager.GetLatestTimings(1, ftm) > 0;
			foreach (var j in new[] { fase, seg })
			{
				j.n++;
				j.quadro += dt * 1000.0;
				j.qs.Add(dt * 1000f);
				if (temT) { j.nT++; j.cpu += ftm[0].cpuMainThreadFrameTime; j.render += ftm[0].cpuRenderThreadFrameTime; j.gpu += ftm[0].gpuFrameTime; }
				for (int i = 0; i < rec.Length; i++) if (rec[i].Valid) { j.cont[i] += rec[i].LastValue; j.contN[i]++; }
			}
			for (int i = 0; i < Partes.Length; i++)
			{
				long d = soma[i] - visto[i];
				visto[i] = soma[i];
				double ms = d * msPorTick;
				fase.fase[i] += ms;
				seg.fase[i] += ms;
			}
		}

		public void Zerar() => fase = new Janela();

		// quadro, CPU principal, render thread, GPU, SetPass, draw calls, triângulos, objetos fazendo sombra,
		// scripts no Update, scripts no LateUpdate, render na thread principal (médias da janela atual)
		public double[] Numeros()
		{
			var j = fase;
			int n = Mathf.Max(1, j.n), nT = Mathf.Max(1, j.nT);
			double C(int i) => j.contN[i] > 0 ? j.cont[i] / j.contN[i] : 0;
			return new[] { j.quadro / n, j.cpu / nT, j.render / nT, j.gpu / nT, C(1), C(2), C(3), C(4), j.fase[7] / n, j.fase[12] / n, j.fase[16] / n };
		}

		// descarta o tempo das partes que passou sem Quadro() (esperas e fotos do teste)
		public void Sincronizar()
		{
			for (int i = 0; i < Partes.Length; i++) visto[i] = soma[i];
		}

		public string LinhaCsv()
		{
			var j = seg;
			seg = new Janela();
			int n = Mathf.Max(1, j.n), nT = Mathf.Max(1, j.nT);
			var sb = new StringBuilder();
			sb.Append(FormattableString.Invariant($"{j.quadro / n:0.00};{j.cpu / nT:0.00};{j.render / nT:0.00};{j.gpu / nT:0.00}"));
			for (int i = 0; i < Contadores.Length; i++) sb.Append(FormattableString.Invariant($";{(j.contN[i] > 0 ? j.cont[i] / j.contN[i] : -1):0}"));
			for (int i = 0; i < Partes.Length; i++) sb.Append(FormattableString.Invariant($";{j.fase[i] / n:0.000}"));
			return sb.ToString();
		}

		public string Resumo()
		{
			var j = fase;
			if (j.n == 0) return "sem quadros";
			int n = j.n;
			j.qs.Sort();
			float p95 = j.qs[Mathf.Min(j.qs.Count - 1, (int)(j.qs.Count * 0.95f))];
			var sb = new StringBuilder();
			sb.Append($"quadro {j.quadro / n:0.0} ms (95% abaixo de {p95:0.0})");
			if (j.nT > 0) sb.Append($" · CPU principal {j.cpu / j.nT:0.0} ms · render thread {j.render / j.nT:0.0} ms · GPU {j.gpu / j.nT:0.0} ms");
			else sb.Append(" · CPU/GPU separados: sem leitura");
			for (int i = 0; i < Contadores.Length; i++) if (j.contN[i] > 0) sb.Append(j.cont[i] > 0 ? $" · {Contadores[i].nome} {j.cont[i] / j.contN[i]:0}" : $" · {Contadores[i].nome} sem leitura");
			sb.Append(" || partes do quadro (ms por quadro):");
			for (int i = 0; i < Partes.Length; i++) if (achou[i]) sb.Append($" {Partes[i].nome} {j.fase[i] / n:0.00};");
			return sb.ToString();
		}

		public void Dispose()
		{
			if (loopOk) try { PlayerLoop.SetPlayerLoop(original); } catch { }
			for (int i = 0; i < rec.Length; i++) try { rec[i].Dispose(); } catch { }
		}
	}

	IEnumerator Medir(string nome, bool liga, float dist)
	{
		FBRig.TorsoClear = liga;
		FBRig.TorsoDist = dist;
		yield return Wait(0.5f);
		FBRig.TorsoMs = 0.0;
		FBRig.TorsoCalls = 0;
		int q = 0;
		float t = 0f;
		while (t < 3f) { yield return null; t += Time.unscaledDeltaTime; q++; }
		Log($"10x10 desempenho na compra · {nome}: {q / Mathf.Max(0.01f, t):0} FPS · correção da arma {FBRig.TorsoMs / Mathf.Max(1, q):0.00} ms por quadro ({FBRig.TorsoCalls / Mathf.Max(1, q)} personagens por quadro)");
	}

	void Render(string nome, int w, int h)
	{
		var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
		try
		{
			var req = new RenderPipeline.StandardRequest { destination = rt };
			if (RenderPipeline.SupportsRenderRequest(fotoCam, req)) RenderPipeline.SubmitRenderRequest(fotoCam, req);
			else { fotoCam.targetTexture = rt; fotoCam.Render(); fotoCam.targetTexture = null; }
			var prev = RenderTexture.active;
			RenderTexture.active = rt;
			var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
			tex.Apply();
			RenderTexture.active = prev;
			File.WriteAllBytes(Path.Combine(dir, nome + ".png"), tex.EncodeToPNG());
			Object.Destroy(tex);
		}
		finally { rt.Release(); Object.Destroy(rt); }
	}

	void Foto(FBCharacter me, string nome, Transform foco, string vista)
	{
		var t = me.transform;
		if (vista == "lado") fotoCam.transform.position = t.position + t.right * 2.2f + t.forward * 0.6f + Vector3.up * 1.45f;
		else fotoCam.transform.position = t.position + t.forward * 2.0f - t.right * 1.2f + Vector3.up * 1.6f;
		fotoCam.transform.LookAt(foco ? foco.position : t.position + Vector3.up);
		Render(nome, 640, 480);
	}
}
