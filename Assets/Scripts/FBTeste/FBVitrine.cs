// FBVitrine v3b: fotos de vitrine (investidor / Instagram). Só liga com "-fbvitrine" na linha de comando; sem isso não faz nada.
// Uso: BURITIRO.exe -fbvitrine [mapas separados por vírgula | todos] [conjuntos separados por vírgula: dia,por,noite,jogo] [png] [ss1]
// Lista de fotos em <pasta do .exe>\vitrine_fotos.txt, uma por linha (# = comentário):
//   <mapa> <conjunto> <nome> alvo=obj:<parte do nome>|x,y,z [n=índice] [perto=x,z] [vista=graus] [sol=esq|dir|frente|costas|lado]
//        [dist=m] [alt=m] [fov=graus] [olhar=m] [incl=graus] [cam=x,y,z] [jogador=1 (personagem de costas na frente da câmera)]
//   "volume <conjunto> chave=valor ..." muda o volume de foto (exp bloom limiar espalha temp tinta vinheta sat contraste aces)
//   "volume <mapa>.<conjunto> chave=valor ..." vale só naquele mapa (ex.: "volume orla.noite exp=2.4"); exp= numa linha de foto
//        vale só para aquela foto (só em por/noite, que têm volume de foto)
//   "sol <graus>" muda a altura do sol no pôr do sol (padrão 10)
// "dia" = jogo como ele é (sol fixo a 38°, sem volume de foto). "por" = anoitecer do próprio jogo adiantado até o sol ficar
// a ~10°. "noite" = noite do próprio jogo (postes, luzes de destaque, janelas e piso molhado). Os dois últimos ganham um
// volume só de foto (ACES, bloom, branco quente, vinheta). "jogo" = 10x10 de verdade com HUD (sem a lista ESQUADRÃO).
// Fotos em <pasta do .exe>\vitrine\<conjunto>\<mapa>_<nome>.jpg (3840x2160, renderizadas em 7680x4320 e reduzidas).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FreeBala;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[DefaultExecutionOrder(32000)]
public class FBVitrine : MonoBehaviour
{
	static readonly string[] TodosMapas = { "praca_pt", "orla", "centro_civico", "praca_aguas", "vila_jardim" };
	static readonly CultureInfo IC = CultureInfo.InvariantCulture;
	static readonly FieldInfo fHudSquad = typeof(FBHUD).GetField("squadRT", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly FieldInfo fHudCanvas = typeof(FBHUD).GetField("canvas", BindingFlags.NonPublic | BindingFlags.Instance);
	static readonly FieldInfo fShopGo = typeof(FBShop).GetField("go", BindingFlags.NonPublic | BindingFlags.Static);

	string[] mapas = TodosMapas;
	HashSet<string> conjuntos = new HashSet<string> { "dia", "por", "noite", "jogo" };
	bool salvarPng;
	int SS = 2;
	const int W = 3840, H = 2160;
	string dir, dirExe;
	readonly StringBuilder rel = new StringBuilder();
	int erros, fotosOk;
	readonly List<string> errosTxt = new List<string>();
	readonly List<string> salvas = new List<string>();

	class Foto { public string mapa, conj, nome; public Dictionary<string, string> kv = new Dictionary<string, string>(); public string linha; }
	readonly List<Foto> fotos = new List<Foto>();
	readonly Dictionary<string, Dictionary<string, float>> vols = new Dictionary<string, Dictionary<string, float>>
	{
		["por"] = new Dictionary<string, float> { ["aces"] = 1, ["exp"] = 0.2f, ["bloom"] = 1.2f, ["limiar"] = 0.95f, ["espalha"] = 0.7f, ["temp"] = 14, ["tinta"] = 4, ["vinheta"] = 0.28f, ["sat"] = 12, ["contraste"] = 14 },
		["noite"] = new Dictionary<string, float> { ["aces"] = 1, ["exp"] = 0.55f, ["bloom"] = 1.2f, ["limiar"] = 0.85f, ["espalha"] = 0.72f, ["temp"] = 6, ["tinta"] = 2, ["vinheta"] = 0.3f, ["sat"] = 8, ["contraste"] = 12 },
	};
	float solPor = 10f;

	// pose da câmera do jogo durante a foto (aplicada no fim do LateUpdate e antes de cada render)
	bool poseOn;
	bool carregou;
	float? expFoto; // exp= da linha da foto (sobrepõe o do volume)
	Vector3 posePos;
	Quaternion poseRot;
	float poseFov = 52f;
	// HUD: some só com a lista ESQUADRÃO
	bool hudVitrine;
	FBHUD hudTratado;
	float lodVitrine = -1f;
	Volume volFoto;
	string volConj;
	readonly List<(Renderer r, bool on)> jogadorEscondido = new List<(Renderer, bool)>();

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void Boot()
	{
		var a = Environment.GetCommandLineArgs();
		int i = Array.IndexOf(a, "-fbvitrine");
		if (i < 0) return;
		var go = new GameObject("FB_Vitrine");
		DontDestroyOnLoad(go);
		var t = go.AddComponent<FBVitrine>();
		if (i + 1 < a.Length && !a[i + 1].StartsWith("-") && a[i + 1] != "todos") t.mapas = a[i + 1].Split(',').Where(s => s.Length > 0).ToArray();
		if (i + 2 < a.Length && !a[i + 2].StartsWith("-")) t.conjuntos = new HashSet<string>(a[i + 2].Split(',').Where(s => s.Length > 0));
		for (int k = i + 3; k < a.Length && !a[k].StartsWith("-"); k++)
		{
			if (a[k] == "png") t.salvarPng = true;
			if (a[k] == "ss1") t.SS = 1;
		}
	}

	float ultimoLog;
	bool terminou;
	void Log(string s)
	{
		ultimoLog = Time.realtimeSinceStartup;
		Debug.Log("[FreeBala][vitrine] " + s);
		rel.AppendLine(DateTime.Now.ToString("HH:mm:ss") + "  " + s);
		try { File.WriteAllText(Path.Combine(dir, "relatorio.txt"), rel.ToString()); } catch { }
	}

	IEnumerator Wait(float s)
	{
		for (float w = 0f; w < s; w += Time.unscaledDeltaTime) yield return null;
	}

	void OnEnable() { RenderPipelineManager.beginContextRendering += AntesRender; }
	void OnDisable() { RenderPipelineManager.beginContextRendering -= AntesRender; }
	void AntesRender(ScriptableRenderContext c, List<Camera> cams) { AplicaPose(); }

	FBCharacter miraEm;
	void Update()
	{
		var me = FBMatch.Player;
		if (poseOn && miraEm && me) me.aimPoint = miraEm.Head ? miraEm.Head.position : miraEm.Pos + Vector3.up * 1.4f;
	}

	void LateUpdate()
	{
		// cão de guarda: exceção dentro de qualquer corrotina aninhada para o Start inteiro e o .exe ficaria aberto para sempre
		if (!terminou && dir != null && Time.realtimeSinceStartup - ultimoLog > 600f)
		{
			terminou = true;
			Log($"CÃO DE GUARDA: 10 min sem progresso, saindo · erros {erros} · {string.Join(" | ", errosTxt)}");
			Application.Quit(2);
		}
		if (volConj != null && volFoto && volFoto.weight > 0f) VolumeFoto(volConj);
		AplicaPose();
		if (!hudVitrine) return;
		var h = FBHUD.I;
		if (h && h != hudTratado)
		{
			if (fHudSquad?.GetValue(h) is RectTransform sq && sq) { sq.localScale = Vector3.zero; hudTratado = h; Log("HUD: lista ESQUADRÃO escondida"); }
		}
	}

	void AplicaPose()
	{
		if (!poseOn) return;
		var c = Camera.main;
		if (!c) return;
		c.transform.SetPositionAndRotation(posePos, poseRot);
		c.fieldOfView = poseFov;
		c.aspect = W / (float)H; // a grama/vegetação recortam no Update com o frustum da Camera.main

	}

	IEnumerator Start()
	{
		dirExe = Path.GetDirectoryName(Application.dataPath);
		dir = Path.Combine(dirExe, "vitrine");
		Directory.CreateDirectory(dir);
		Application.logMessageReceived += (c, st, ty) =>
		{
			if (ty != LogType.Exception && ty != LogType.Error) return;
			erros++;
			string l = c.Split('\n')[0];
			if (errosTxt.Count < 10 && !errosTxt.Contains(l)) errosTxt.Add(l);
		};
		Log($"início · mapas {string.Join(",", mapas)} · conjuntos {string.Join(",", conjuntos)} · tela {Screen.width}x{Screen.height} · {SystemInfo.graphicsDeviceName} {SystemInfo.graphicsMemorySize} MB · supersampling {SS}x · build de desenvolvimento {Debug.isDebugBuild}");
		LerLista();
		yield return Wait(4f);
		GameData.Load();
		FBSession.Load();
		var keep = (FBSession.map, FBSession.mode, FBSession.view, FBSession.team);
		var perfil0 = (FBSession.money, FBSession.xp, FBSession.passXp);
		int escolha0 = FBQuality.Choice;
		DesligarSobreposicoes();
		ForcarUltra(2.4f);
		foreach (var mapa in mapas)
		{
			var wx0 = FBSession.WeatherFor(mapa);
			var minhas = fotos.Where(f => f.mapa == mapa && conjuntos.Contains(f.conj) && f.conj != "jogo").ToList();
			if (minhas.Count > 0)
			{
				FBSession.SetWeather(mapa, "dia", "limpo");
				yield return Carregar(mapa, "classico");
				if (carregou && FBMatch.I && FBMatch.Player)
				{
					yield return Cenas(mapa, minhas);
				}
				else Log($"{mapa}: a partida não montou, fotos pulam");
			}
			if (conjuntos.Contains("jogo")) yield return Jogo(mapa);
			FBSession.SetWeather(mapa, wx0.time, wx0.weather);
		}
		poseOn = false;
		hudVitrine = false;
		FBPlayer.TestInput = null;
		FBMatch.God = false;
		DesfazerUltra(escolha0);
		(FBSession.map, FBSession.mode, FBSession.view, FBSession.team) = keep;
		(FBSession.money, FBSession.xp, FBSession.passXp) = perfil0; // EndMatch de cada 10x10 soma dinheiro, XP e XP do passe
		FBSession.Save();
		Log($"fim · {fotosOk} fotos salvas · erros/exceções no log: {erros}" + (errosTxt.Count > 0 ? " · " + string.Join(" | ", errosTxt) : ""));
		try { File.WriteAllLines(Path.Combine(dir, "fotos_salvas.txt"), salvas); } catch { }
		terminou = true;
		yield return Wait(0.5f);
		Application.Quit(0);
	}

	// ---------- lista de fotos ----------
	void LerLista()
	{
		string arq = Path.Combine(dirExe, "vitrine_fotos.txt");
		if (!File.Exists(arq)) { Log($"sem {arq}: só o conjunto jogo roda"); return; }
		foreach (var bruta in File.ReadAllLines(arq))
		{
			string l = bruta.Trim();
			int c = l.IndexOf('#');
			if (c >= 0) l = l.Substring(0, c).Trim();
			if (l.Length == 0) continue;
			var p = l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (p[0] == "volume" && p.Length >= 2)
			{
				if (!vols.TryGetValue(p[1], out var v)) vols[p[1]] = v = new Dictionary<string, float>();
				foreach (var kv in p.Skip(2)) { var s = kv.Split('='); if (s.Length == 2 && float.TryParse(s[1], NumberStyles.Float, IC, out float f)) v[s[0]] = f; }
				continue;
			}
			if (p[0] == "sol" && p.Length >= 2 && float.TryParse(p[1], NumberStyles.Float, IC, out float g)) { solPor = g; continue; }
			if (p.Length < 4) { Log("linha ignorada: " + l); continue; }
			var foto = new Foto { mapa = p[0], conj = p[1], nome = p[2], linha = l };
			foreach (var kv in p.Skip(3)) { int e = kv.IndexOf('='); if (e > 0) foto.kv[kv.Substring(0, e)] = kv.Substring(e + 1); }
			fotos.Add(foto);
		}
		Log($"lista: {fotos.Count} fotos · sol do pôr do sol a {solPor:0.#}°");
	}

	static float F(Dictionary<string, string> kv, string k, float padrao) => kv.TryGetValue(k, out var s) && float.TryParse(s, NumberStyles.Float, IC, out float f) ? f : padrao;
	static bool V3(string s, out Vector3 v)
	{
		v = default;
		var p = s.Split(',');
		if (p.Length == 2 && float.TryParse(p[0], NumberStyles.Float, IC, out float x2) && float.TryParse(p[1], NumberStyles.Float, IC, out float z2)) { v = new Vector3(x2, float.NaN, z2); return true; }
		if (p.Length != 3) return false;
		if (!float.TryParse(p[0], NumberStyles.Float, IC, out float x) || !float.TryParse(p[1], NumberStyles.Float, IC, out float y) || !float.TryParse(p[2], NumberStyles.Float, IC, out float z)) return false;
		v = new Vector3(x, y, z);
		return true;
	}

	// ---------- carregar ----------
	IEnumerator Carregar(string mapa, string modo)
	{
		poseOn = false;
		carregou = false;
		FBSession.map = mapa;
		FBSession.mode = modo;
		FBSession.online = false;
		FBSession.view = "tps";
		FBSession.Save();
		Log($"carregando Map_{mapa} no modo {modo} (clima {FBSession.WeatherFor(mapa)})");
		var op = SceneManager.LoadSceneAsync("Map_" + mapa);
		if (op == null) { Log($"FALHA: cena Map_{mapa} não existe"); yield break; }
		while (!op.isDone) yield return null;
		for (float w = 0f; w < 30f && !(FBMatch.I && FBMatch.Player && FBPlayer.I && FBPlayer.I.wu); w += Time.unscaledDeltaTime) yield return null;
		for (float w = 0f; w < 20f && FBLoading.Busy; w += Time.unscaledDeltaTime) yield return null;
		SoltarLod();
		DesligarSobreposicoes();
		yield return Wait(2f);
		carregou = FBMatch.I && FBMatch.Player && SceneManager.GetActiveScene().name == "Map_" + mapa;
		if (!carregou) Log($"FALHA: Map_{mapa} não ficou ativa (cena ativa {SceneManager.GetActiveScene().name})");
	}

	// ---------- qualidade máxima ----------
	void ForcarUltra(float lodFoto)
	{
		try
		{
			FBQuality.Apply(FBQLevel.Ultra);
			var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
			if (urp) urp.renderScale = 1f;
		}
		catch (Exception e) { Log("Ultra falhou: " + e.Message); }
		lodVitrine = lodFoto;
		SoltarLod();
		FBQuality.Changed -= SoltarLod; FBQuality.Changed += SoltarLod;
		SceneManager.sceneLoaded -= SoltarLodCena; SceneManager.sceneLoaded += SoltarLodCena;
		Log($"qualidade {FBQuality.Level} · lodBias {QualitySettings.lodBias:0.0} · sombra {FBQuality.ShadowDistance:0} m");
	}
	void SoltarLodCena(Scene s, LoadSceneMode m) => SoltarLod();
	void SoltarLod()
	{
		if (lodVitrine <= 0f) return;
		QualitySettings.lodBias = lodVitrine;
		QualitySettings.meshLodThreshold = Mathf.Clamp(1.2f / Mathf.Max(0.3f, lodVitrine), 0.45f, 2.2f);
	}
	void DesfazerUltra(int escolha0)
	{
		FBQuality.Changed -= SoltarLod;
		SceneManager.sceneLoaded -= SoltarLodCena;
		lodVitrine = -1f;
		try { FBQuality.Apply(escolha0 < 0 ? FBQuality.Detected : (FBQLevel)escolha0); } catch { }
	}

	void DesligarSobreposicoes()
	{
		Debug.developerConsoleEnabled = false;
		try
		{
			var wm = typeof(GraphicsSettings).Assembly.GetType("UnityEngine.Rendering.Watermark")?.GetProperty("showDeveloperWatermark", BindingFlags.Public | BindingFlags.Static);
			wm?.SetValue(null, false);
		}
		catch { }
		var guias = GameObject.Find("GuiasReels");
		if (guias) guias.SetActive(false);
	}

	// ---------- clima e volume de foto ----------
	static float ElevacaoSol(float t)
	{
		float num = t / 420f;
		float n = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((num - 0.08f) / 0.84f));
		return Mathf.Lerp(13f, -14f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(n / 0.62f)));
	}
	static float TempoSol(float graus)
	{
		for (float t = 0f; t <= 420f; t += 0.25f) if (ElevacaoSol(t) <= graus) return t;
		return 0f;
	}
	static Light Sol() => RenderSettings.sun ? RenderSettings.sun : Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);

	void Clima(string conj)
	{
		if (conj == "noite") FBWeather.ForceNight(true);
		else if (conj == "por") { FBWeather.ForceNight(false); FBWeather.Dusk = true; FBWeather.SkipDusk(TempoSol(solPor)); }
		else FBWeather.ForceNight(false);
	}

	void VolumeFoto(string conj)
	{
		if (!volFoto)
		{
			var go = new GameObject("FB_VitrineVolume");
			volFoto = go.AddComponent<Volume>();
			volFoto.isGlobal = true;
			volFoto.priority = 100f;
			var pr = ScriptableObject.CreateInstance<VolumeProfile>();
			pr.name = "FB_VitrineVolume";
			volFoto.profile = pr;
			pr.Add<Tonemapping>(false);
			pr.Add<ColorAdjustments>(false);
			pr.Add<Bloom>(false);
			pr.Add<WhiteBalance>(false);
			pr.Add<Vignette>(false);
		}
		if (!vols.TryGetValue(conj, out var v)) { volFoto.weight = 0f; return; }
		var p = volFoto.profile;
		float G(string k, float d) => v.TryGetValue(k, out float f) ? f : d;
		if (p.TryGet(out Tonemapping tm)) { tm.mode.overrideState = true; tm.mode.value = G("aces", 1) > 0.5f ? TonemappingMode.ACES : TonemappingMode.Neutral; }
		if (p.TryGet(out ColorAdjustments ca))
		{
			ca.postExposure.overrideState = true; ca.postExposure.value = expFoto ?? G("exp", 0f);
			ca.contrast.overrideState = true; ca.contrast.value = G("contraste", 10f);
			ca.saturation.overrideState = true; ca.saturation.value = G("sat", 8f);
			ca.colorFilter.overrideState = false; // NightGrade liga isto no nosso perfil; o filtro (azul à noite) fica o do jogo
		}
		if (p.TryGet(out Bloom bl))
		{
			bl.intensity.overrideState = true; bl.intensity.value = G("bloom", 1.2f);
			bl.threshold.overrideState = true; bl.threshold.value = G("limiar", 0.95f);
			bl.scatter.overrideState = true; bl.scatter.value = G("espalha", 0.7f);
			bl.highQualityFiltering.overrideState = true; bl.highQualityFiltering.value = true;
			bl.maxIterations.overrideState = true;
			bl.maxIterations.value = Mathf.Clamp(6 + Mathf.RoundToInt(Mathf.Log(W * SS / (float)Mathf.Max(1, Screen.width), 2f)), 2, 8);
		}
		if (p.TryGet(out WhiteBalance wb))
		{
			wb.temperature.overrideState = true; wb.temperature.value = G("temp", 10f);
			wb.tint.overrideState = true; wb.tint.value = G("tinta", 0f);
		}
		if (p.TryGet(out Vignette vg))
		{
			vg.intensity.overrideState = true; vg.intensity.value = G("vinheta", 0.25f);
			vg.smoothness.overrideState = true; vg.smoothness.value = 0.42f;
		}
		volFoto.weight = 1f;
	}

	// ---------- fotos de cenário ----------
	void EsconderJogador(bool esconder)
	{
		if (esconder)
		{
			jogadorEscondido.Clear();
			var me = FBMatch.Player;
			if (me) foreach (var r in me.GetComponentsInChildren<Renderer>(true)) { jogadorEscondido.Add((r, r.enabled)); r.enabled = false; }
		}
		else
		{
			foreach (var (r, on) in jogadorEscondido) if (r) r.enabled = on;
			jogadorEscondido.Clear();
		}
	}

	IEnumerator Cenas(string mapa, List<Foto> minhas)
	{
		FBMatch.God = true;
		foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
		FBPlayer.TestInput = inp => new FBInputState();
		EsconderJogador(true);
		yield return Wait(1f);
		foreach (var conj in new[] { "dia", "por", "noite" })
		{
			var lista = minhas.Where(f => f.conj == conj).ToList();
			if (lista.Count == 0) continue;
			Clima(conj);
			// "volume <mapa>.<conj> ..." sobrepõe só as chaves que trouxer; o resto vem do volume do conjunto
			string chave = mapa + "." + conj;
			if (vols.TryGetValue(chave, out var vm) && vols.TryGetValue(conj, out var vb))
				foreach (var kv in vb) if (!vm.ContainsKey(kv.Key)) vm[kv.Key] = kv.Value;
			volConj = vols.ContainsKey(chave) ? chave : conj;
			VolumeFoto(volConj);
			if (FBBirds.I) FBBirds.I.gameObject.SetActive(conj != "noite");
			yield return Wait(conj == "noite" ? 3f : 1.5f);
			var sol = Sol();
			Log($"{mapa} {conj}: sol a {(sol ? AlturaSol(sol) : 0f):0.0}° de altura, rumo {(sol ? RumoSol(sol) : 0f):0}° · noite {FBWeather.NightK:0.00} · volume de foto {(volFoto ? volFoto.weight : 0f):0} ({volConj})");
			Directory.CreateDirectory(Path.Combine(dir, conj));
			foreach (var f in lista)
			{
				if (conj == "por") FBWeather.SkipDusk(TempoSol(solPor));
				yield return Cena(mapa, f);
			}
		}
		poseOn = false;
		volConj = null;
		if (Camera.main) Camera.main.ResetAspect();
		if (volFoto) volFoto.weight = 0f;
		if (FBBirds.I) FBBirds.I.gameObject.SetActive(true);
		FBWeather.ForceNight(false);
		EsconderJogador(false);
	}

	// personagem do jogador de costas, 2,4 m à frente da câmera e um pouco à direita, olhando para o alvo, de fuzil
	bool PorJogador(Vector3 C, Vector3 T, float yaw, float alt)
	{
		var me = FBMatch.Player;
		var P = FBPlayer.I;
		if (!me || !P) return false;
		Vector3 fw = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
		Vector3 pos = C + fw * 2.4f + dir * 0.75f;
		float? g = Chao(new Vector3(pos.x, C.y + 2f, pos.z), alt + 6f);
		if (g == null) { Log("  jogador: sem chão na frente da câmera"); return false; }
		pos.y = g.Value + 0.05f;
		EsconderJogador(false);
		var cc = me.GetComponent<CharacterController>();
		if (cc) cc.enabled = false;
		me.transform.position = pos;
		float y = Mathf.Atan2(T.x - pos.x, T.z - pos.z) * Mathf.Rad2Deg;
		me.Yaw = y;
		if (cc) cc.enabled = true;
		try { P.OnRespawn(y); } catch { }
		try
		{
			int s = Array.FindIndex(P.wu.slots, x => x != null && x.id == "ak");
			if (s < 0) { P.wu.Give("ak"); s = Array.FindIndex(P.wu.slots, x => x != null && x.id == "ak"); }
			if (s >= 0) P.wu.Equip(s, instant: true);
		}
		catch { }
		return true;
	}

	static float AlturaSol(Light s) => Mathf.Asin(Mathf.Clamp(-s.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
	static float RumoSol(Light s) { var f = -s.transform.forward; return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg; }

	// acha o objeto pelo nome (o mais alto da hierarquia que bate) e junta os renderizadores dele
	bool Alvo(Foto f, out Bounds b, out Transform raiz, out string desc)
	{
		b = default; raiz = null; desc = "";
		if (!f.kv.TryGetValue("alvo", out var a)) { desc = "sem alvo="; return false; }
		if (a.StartsWith("obj:"))
		{
			string parte = a.Substring(4).ToLowerInvariant();
			var achados = new List<Transform>();
			for (int s = 0; s < SceneManager.sceneCount; s++)
			{
				var cena = SceneManager.GetSceneAt(s);
				if (!cena.isLoaded) continue;
				foreach (var go in cena.GetRootGameObjects())
					foreach (var t in go.GetComponentsInChildren<Transform>(false))
						if (t.name.ToLowerInvariant().Contains(parte) && !AncestralBate(t, parte)) achados.Add(t);
			}
			achados = achados.Where(t => t.GetComponentsInChildren<Renderer>(false).Any(r => r.enabled && !(r is ParticleSystemRenderer))).ToList();
			if (achados.Count == 0) { desc = $"nenhum objeto com \"{parte}\" no nome"; return false; }
			if (f.kv.TryGetValue("perto", out var pp) && V3(pp, out var perto)) achados = achados.OrderBy(t => new Vector2(t.position.x - perto.x, t.position.z - perto.z).sqrMagnitude).ToList();
			int n = Mathf.Clamp((int)F(f.kv, "n", 0), 0, achados.Count - 1);
			raiz = achados[n];
			bool tem = false;
			foreach (var r in raiz.GetComponentsInChildren<Renderer>(false))
			{
				if (!r.enabled || r is ParticleSystemRenderer) continue;
				if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
			}
			desc = $"{raiz.name} ({achados.Count} com esse nome, usei o {n}) centro ({b.center.x:0.0}, {b.center.y:0.0}, {b.center.z:0.0}) tamanho ({b.size.x:0.0}, {b.size.y:0.0}, {b.size.z:0.0})";
			return tem;
		}
		if (V3(a, out var p))
		{
			if (float.IsNaN(p.y)) p.y = Chao(new Vector3(p.x, 200f, p.z), 400f) ?? 0f;
			b = new Bounds(p + Vector3.up * 1f, new Vector3(2f, 2f, 2f));
			desc = $"ponto ({p.x:0.0}, {p.y:0.0}, {p.z:0.0})";
			return true;
		}
		desc = "alvo inválido: " + a;
		return false;
	}

	static bool AncestralBate(Transform t, string parte)
	{
		for (var p = t.parent; p; p = p.parent) if (p.name.ToLowerInvariant().Contains(parte)) return true;
		return false;
	}

	static float? Chao(Vector3 de, float alcance)
	{
		var hits = Physics.RaycastAll(de, Vector3.down, alcance, ~0, QueryTriggerInteraction.Ignore);
		float? melhor = null;
		foreach (var h in hits)
		{
			if (h.collider.GetComponentInParent<FBCharacter>() || h.collider.GetComponentInParent<FBCar>()) continue;
			if (melhor == null || h.point.y > melhor.Value) melhor = h.point.y;
		}
		return melhor;
	}

	bool Visivel(Vector3 c, Vector3 t, Transform raiz)
	{
		Vector3 d = t - c;
		float dist = d.magnitude;
		if (dist < 0.1f) return true;
		foreach (var h in Physics.RaycastAll(c, d / dist, dist - 0.4f, ~0, QueryTriggerInteraction.Ignore))
		{
			if (raiz && h.collider.transform.IsChildOf(raiz)) continue;
			if (h.collider.GetComponentInParent<FBCharacter>()) continue;
			return false;
		}
		return true;
	}

	IEnumerator Cena(string mapa, Foto f)
	{
		if (!Alvo(f, out var b, out var raiz, out var desc)) { Log($"  {f.conj}/{f.nome}: PULADA, {desc}"); yield break; }
		float fov = F(f.kv, "fov", 52f);
		float alt = F(f.kv, "alt", 0.9f);
		float incl = F(f.kv, "incl", 5f);
		float olhar = F(f.kv, "olhar", Mathf.Min(b.size.y * 0.5f, 5f));
		Vector3 T = new Vector3(b.center.x, b.min.y + olhar, b.center.z);
		var sol = Sol();
		float rumoSol = sol ? RumoSol(sol) : 145f;
		float V0;
		if (f.kv.ContainsKey("vista")) V0 = F(f.kv, "vista", 0f);
		else
		{
			string modo = f.kv.TryGetValue("sol", out var s) ? s : (f.conj == "dia" ? "costas" : "esq");
			if (f.conj == "noite" && !f.kv.ContainsKey("sol")) rumoSol = 145f; // a lua fica em outro lugar: usa o mesmo enquadramento do pôr do sol
			float meiaH = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * W / (float)H) * Mathf.Rad2Deg;
			float offSol = Mathf.Min(33f, meiaH * 0.75f);
			switch (modo)
			{
				case "dir": V0 = rumoSol - offSol; break;
				case "frente": V0 = rumoSol + 12f; break;
				case "costas": V0 = rumoSol + 180f + 35f; break;
				case "lado": V0 = rumoSol + 90f; break;
				default: V0 = rumoSol + offSol; break;
			}
		}
		float S = Mathf.Max(b.extents.x, b.extents.z, b.extents.y * 0.6f);
		float D0 = f.kv.ContainsKey("dist") ? F(f.kv, "dist", 10f) : Mathf.Clamp(S * 2.4f + 2.5f, 4f, 70f);
		Vector3 C = default;
		bool achou = false;
		string tentativa = "";
		if (f.kv.TryGetValue("cam", out var cs) && V3(cs, out var cp))
		{
			if (float.IsNaN(cp.y)) cp.y = (Chao(new Vector3(cp.x, b.min.y + 3f, cp.z), 10f) ?? b.min.y) + alt;
			C = cp; achou = true; tentativa = "cam= fixa";
		}
		else
		{
			foreach (float dYaw in new[] { 0f, 12f, -12f, 25f, -25f, 40f, -40f, 60f, -60f, 90f, -90f })
			{
				foreach (float fD in new[] { 1f, 0.8f, 0.62f, 1.25f, 0.45f })
				{
					float V = (V0 + dYaw) * Mathf.Deg2Rad;
					var fw = new Vector3(Mathf.Sin(V), 0f, Mathf.Cos(V));
					var c = new Vector3(T.x, 0f, T.z) - fw * (D0 * fD);
					bool bf0 = Physics.queriesHitBackfaces;
					Physics.queriesHitBackfaces = true;
					try
					{
						float? g = Chao(new Vector3(c.x, b.min.y + 3f, c.z), 8f);
						if (g == null) continue;
						if (g.Value > b.min.y + 1.5f) continue;
						if (Physics.Raycast(new Vector3(c.x, g.Value + 0.3f, c.z), Vector3.up, 4f, ~0, QueryTriggerInteraction.Ignore)) continue; // marquise, quiosque ou teto baixo
						c.y = g.Value + alt;
						if (Physics.CheckSphere(c, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
						if (!Visivel(c, T, raiz) && !Visivel(c, T + Vector3.up * b.extents.y * 0.6f, raiz)) continue;
					}
					finally { Physics.queriesHitBackfaces = bf0; }
					C = c; achou = true; tentativa = $"giro {dYaw:+0;-0;0}°, distância {D0 * fD:0.0} m";
					break;
				}
				if (achou) break;
			}
		}
		if (!achou) { Log($"  {f.conj}/{f.nome}: PULADA, sem lugar livre para a câmera em volta de {desc}"); yield break; }
		Vector3 d = T - C;
		float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
		float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg - incl;
		bool comJogador = F(f.kv, "jogador", 0f) > 0.5f && PorJogador(C, T, yaw, alt);
		posePos = C;
		poseRot = Quaternion.Euler(pitch, yaw, 0f);
		poseFov = fov;
		poseOn = true;
		expFoto = f.kv.ContainsKey("exp") ? F(f.kv, "exp", 0f) : (float?)null;
		if (volConj != null && volFoto && volFoto.weight > 0f) VolumeFoto(volConj);
		yield return Wait(f.conj == "noite" ? 1.2f : 0.6f);
		string nome = $"{mapa}_{f.nome}";
		yield return Foto3D(Path.Combine(dir, f.conj), nome);
		if (comJogador) EsconderJogador(true);
		if (expFoto.HasValue) Log($"  {f.conj}/{nome}: exposição da foto {expFoto.Value:0.##}");
		expFoto = null;
		Log($"  {f.conj}/{nome}: {desc} · câmera ({C.x:0.0}, {C.y:0.0}, {C.z:0.0}) {tentativa} · rumo {yaw:0}° inclinação {pitch:0}° fov {fov:0}");
	}

	// ---------- render 4K (câmera do jogo, com pós-processamento) ----------
	Action PrepararFoto()
	{
		var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		float lod0 = QualitySettings.lodBias, mesh0 = QualitySettings.meshLodThreshold;
		int maxLod0 = QualitySettings.maximumLODLevel, mip0 = QualitySettings.globalTextureMipmapLimit;
		bool all0 = Texture.streamingTextureForceLoadAll;
		float sd0 = urp ? urp.shadowDistance : 0f, rs0 = urp ? urp.renderScale : 1f;
		int res0 = urp ? urp.mainLightShadowmapResolution : 0, casc0 = urp ? urp.shadowCascadeCount : 0;
		var up0 = urp ? urp.upscalingFilter : UpscalingFilterSelection.Auto;
		QualitySettings.lodBias = Mathf.Max(lod0, 4f);
		QualitySettings.meshLodThreshold = 0.45f;
		QualitySettings.maximumLODLevel = 0;
		QualitySettings.globalTextureMipmapLimit = 0;
		Texture.streamingTextureForceLoadAll = true;
		if (urp)
		{
			urp.shadowDistance = Mathf.Max(sd0, 160f); urp.mainLightShadowmapResolution = 4096; urp.shadowCascadeCount = 4;
			urp.renderScale = 1f;
			if (up0 == UpscalingFilterSelection.STP) urp.upscalingFilter = UpscalingFilterSelection.Auto;
		}
		return () =>
		{
			QualitySettings.lodBias = lod0; QualitySettings.meshLodThreshold = mesh0;
			QualitySettings.maximumLODLevel = maxLod0; QualitySettings.globalTextureMipmapLimit = mip0;
			Texture.streamingTextureForceLoadAll = all0;
			if (urp) { urp.shadowDistance = sd0; urp.mainLightShadowmapResolution = res0; urp.shadowCascadeCount = casc0; urp.renderScale = rs0; urp.upscalingFilter = up0; }
		};
	}

	IEnumerator EsperarTexturas(float max = 10f)
	{
		for (float t = 0f; t < max; t += Time.unscaledDeltaTime)
		{
			if (t > Mathf.Max(0.4f, LODGroup.crossFadeAnimationDuration + 0.3f) && Texture.streamingTexturePendingLoadCount == 0 && Texture.streamingTextureLoadingCount == 0) yield break;
			yield return null;
		}
	}

	Texture2D Render3D(Camera cam, int w, int h, int ss)
	{
		var lista = new List<RenderTexture>();
		var big = new RenderTexture(new RenderTextureDescriptor(w * ss, h * ss, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 }) { name = "vitrine_ssaa", filterMode = FilterMode.Bilinear };
		lista.Add(big); if (!big.Create()) throw new Exception($"RT {w * ss}x{h * ss} não foi criada (VRAM?)");
		try
		{
			var req = new RenderPipeline.StandardRequest { destination = big };
			var alvo0 = cam.targetTexture;
			cam.aspect = w / (float)h;
			try
			{
				if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
				else { cam.targetTexture = big; cam.Render(); }
			}
			finally { cam.targetTexture = alvo0; cam.ResetAspect(); }
			var src = big;
			while (src.width > w)
			{
				var dst = new RenderTexture(new RenderTextureDescriptor(src.width / 2, src.height / 2, RenderTextureFormat.ARGB32, 0) { sRGB = true, msaaSamples = 1 }) { filterMode = FilterMode.Bilinear };
				lista.Add(dst); if (!dst.Create()) throw new Exception("RT de redução não foi criada");
				Graphics.Blit(src, dst);
				src = dst;
			}
			var prev = RenderTexture.active;
			RenderTexture.active = src;
			var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
			tex.Apply(false);
			RenderTexture.active = prev;
			return tex;
		}
		finally { foreach (var r in lista) { r.Release(); Object.Destroy(r); } }
	}

	void Salvar(Texture2D tex, string pasta, string nome)
	{
		Directory.CreateDirectory(pasta);
		string jpg = Path.Combine(pasta, nome + ".jpg");
		File.WriteAllBytes(jpg, tex.EncodeToJPG(94));
		if (salvarPng) File.WriteAllBytes(Path.Combine(pasta, nome + ".png"), tex.EncodeToPNG());
		salvas.Add(jpg);
		fotosOk++;
	}

	IEnumerator Foto3D(string pasta, string nome)
	{
		var cam = Camera.main;
		if (!cam) { Log("foto: sem Camera.main"); yield break; }
		var desfaz = PrepararFoto();
		Texture2D tex = null;
		try
		{
			yield return EsperarTexturas();
			yield return null; // meio do quadro, depois dos Update (grama/vegetação já enfileiradas), como o FBScreenshot.DoReel
			AplicaPose();
			try { tex = Render3D(cam, W, H, SS); }
			catch (Exception e)
			{
				Log($"foto {nome}: render falhou ({e.Message}), tentando sem supersampling");
				try { tex = Render3D(cam, W, H, 1); } catch (Exception e2) { Log($"foto {nome}: PULADA ({e2.Message})"); }
			}
			if (tex) Salvar(tex, pasta, nome);
		}
		finally { if (tex) Object.Destroy(tex); desfaz(); }
	}

	// ---------- tela do jogo com HUD ----------
	IEnumerator TelaHud(string nome)
	{
		var pasta = Path.Combine(dir, "jogo");
		Directory.CreateDirectory(pasta);
		if (Screen.width < 3840) yield return FotoComHud(pasta, nome + "_4k"); // primeiro a 4K (espera texturas), depois a tela: 2 quadros de diferença
		yield return new WaitForEndOfFrame();
		var tex = ScreenCapture.CaptureScreenshotAsTexture();
		if (tex)
		{
			Salvar(tex, pasta, nome + (tex.width >= 3840 ? "" : $"_{tex.width}x{tex.height}"));
			Object.Destroy(tex);
		}
	}

	const int CamadaFoto = 31;
	static void Camada(Transform raiz, List<(GameObject go, int layer)> guardadas)
	{
		foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
			if (t.gameObject.layer != CamadaFoto) { guardadas.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = CamadaFoto; }
	}

	// 3D em 4K de verdade + o HUD redesenhado em 4K por cima (o Canvas passa um quadro para "Screen Space - Camera")
	IEnumerator FotoComHud(string pasta, string nome)
	{
		var cam = Camera.main;
		var hud = FBHUD.I;
		var canvas = hud ? fHudCanvas?.GetValue(hud) as Canvas : null;
		var scaler = canvas ? canvas.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
		if (!cam || !canvas || !scaler) { Log("fotohud: sem câmera ou HUD"); yield break; }
		var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		var desfaz = PrepararFoto();
		Texture2D fundo = null, tex = null;
		RenderTexture rtHud = null;
		Camera uiCam = null;
		GameObject raw = null;
		var camadas = new List<(GameObject go, int layer)>();
		var feats = new List<(ScriptableRendererFeature f, bool on)>();
		var outros = new List<(Canvas c, RenderMode m, Camera wc, float pd, UnityEngine.UI.CanvasScaler s, UnityEngine.UI.CanvasScaler.ScaleMode sm, float sf)>();
		bool ok = false;
		try
		{
			yield return EsperarTexturas();
			yield return null;
			try { fundo = Render3D(cam, W, H, SS); }
			catch (Exception e) { Log($"fotohud {nome}: 3D falhou ({e.Message}), tentando sem supersampling"); fundo = Render3D(cam, W, H, 1); }
			fundo.filterMode = FilterMode.Point;
			float k = canvas.scaleFactor * W / Mathf.Max(1f, Screen.width);
			uiCam = new GameObject("FB_VitrineCamHud").AddComponent<Camera>();
			uiCam.transform.position = new Vector3(0f, -1500f, 0f);
			uiCam.clearFlags = CameraClearFlags.SolidColor; uiCam.backgroundColor = Color.black;
			uiCam.cullingMask = 1 << CamadaFoto; uiCam.allowHDR = false; uiCam.allowMSAA = false;
			uiCam.nearClipPlane = 0.1f; uiCam.farClipPlane = 10f; uiCam.depth = -100f;
			var ud = uiCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
			ud.renderPostProcessing = false; ud.antialiasing = AntialiasingMode.None; ud.renderShadows = false;
			ud.requiresDepthOption = CameraOverrideOption.Off; ud.requiresColorOption = CameraOverrideOption.Off;
			rtHud = new RenderTexture(new RenderTextureDescriptor(W, H, RenderTextureFormat.ARGB32, 24) { sRGB = true, msaaSamples = 1 });
			rtHud.Create();
			uiCam.targetTexture = rtHud;
			raw = new GameObject("FB_VitrineFundo", typeof(RectTransform), typeof(UnityEngine.UI.RawImage));
			raw.transform.SetParent(canvas.transform, false); raw.transform.SetAsFirstSibling();
			var rr = (RectTransform)raw.transform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
			var ri = raw.GetComponent<UnityEngine.UI.RawImage>(); ri.texture = fundo; ri.raycastTarget = false;
			foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
			{
				if (!cv || !cv.isRootCanvas || !cv.enabled || !cv.gameObject.activeInHierarchy || cv.renderMode != RenderMode.ScreenSpaceOverlay || cv.name == "GuiasReels") continue;
				var sc = cv.GetComponent<UnityEngine.UI.CanvasScaler>();
				float kc = cv.scaleFactor * W / Mathf.Max(1f, Screen.width);
				outros.Add((cv, cv.renderMode, cv.worldCamera, cv.planeDistance, sc, sc ? sc.uiScaleMode : default, sc ? sc.scaleFactor : 1f));
				cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = uiCam; cv.planeDistance = 1f;
				if (sc) { sc.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize; sc.scaleFactor = kc; }
				cv.scaleFactor = kc;
				Camada(cv.transform, camadas);
			}
			if (urp && typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(urp) is ScriptableRendererData[] rds)
				foreach (var rd in rds) if (rd) foreach (var f in rd.rendererFeatures) if (f) { feats.Add((f, f.isActive)); f.SetActive(false); }
			Canvas.ForceUpdateCanvases();
			yield return null;
			foreach (var o in outros) if (o.c) Camada(o.c.transform, camadas);
			yield return new WaitForEndOfFrame();
			var prev = RenderTexture.active; RenderTexture.active = rtHud;
			tex = new Texture2D(W, H, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(false);
			RenderTexture.active = prev;
			Salvar(tex, pasta, nome);
			ok = true;
			Log($"  fotohud {nome}: {W}x{H} · escala {k:0.00} · tamanho lógico {((RectTransform)canvas.transform).rect.size}");
		}
		finally
		{
			foreach (var (f, on) in feats) if (f) f.SetActive(on);
			foreach (var o in outros)
			{
				if (o.c) { o.c.renderMode = o.m; o.c.worldCamera = o.wc; o.c.planeDistance = o.pd; }
				if (o.s) { o.s.uiScaleMode = o.sm; o.s.scaleFactor = o.sf; }
			}
			foreach (var (go, l) in camadas) if (go) go.layer = l;
			if (raw) Object.Destroy(raw);
			if (uiCam) Object.Destroy(uiCam.gameObject);
			if (rtHud) { rtHud.Release(); Object.Destroy(rtHud); }
			if (fundo) Object.Destroy(fundo);
			if (tex) Object.Destroy(tex);
			desfaz();
			if (!ok) Log($"  fotohud {nome}: falhou");
		}
	}

	// ---------- 10x10 com HUD ----------
	IEnumerator Jogo(string mapa)
	{
		FBSession.SetWeather(mapa, "dia", "limpo");
		FBSession.team = 0;
		hudVitrine = true;
		hudTratado = null;
		int w0 = Screen.width, h0 = Screen.height;
		var m0 = Screen.fullScreenMode;
		if (Screen.width < 3840)
		{
			Screen.SetResolution(3840, 2160, FullScreenMode.Windowed);
			yield return Wait(1.5f);
			Log($"jogo: pedi janela 3840x2160, ficou {Screen.width}x{Screen.height}");
		}
		yield return Carregar(mapa, "10x10");
		var g = FBMatch.I;
		var me = FBMatch.Player;
		var P = FBPlayer.I;
		bool TelaMudou() => Screen.width != w0 || Screen.height != h0 || Screen.fullScreenMode != m0;
		if (!carregou || !g || g.mode == null || !me || !P)
		{
			Log($"{mapa} jogo: a partida não montou");
			hudVitrine = false;
			if (TelaMudou()) { Screen.SetResolution(w0, h0, m0); yield return Wait(1f); }
			yield break;
		}
		var tipo = g.mode.GetType();
		var fWins = tipo.GetField("wins", BindingFlags.NonPublic | BindingFlags.Instance);
		var fRound = tipo.GetField("round", BindingFlags.NonPublic | BindingFlags.Instance);
		var fPhase = tipo.GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance);
		var fState = typeof(FBMatch).GetField("state", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
		if (fPhase == null || fWins == null) { Log($"{mapa} jogo: o modo carregado é {tipo.Name}, não o 10x10 (sem phase/wins)"); hudVitrine = false; if (TelaMudou()) { Screen.SetResolution(w0, h0, m0); yield return Wait(1f); } yield break; }
		int win = 7;
		try { var fWin = tipo.GetField("WIN", BindingFlags.Public | BindingFlags.Static); if (fWin != null) win = Convert.ToInt32(fWin.GetValue(null)); } catch { }
		string Fase() { try { return fPhase?.GetValue(g.mode) as string ?? "?"; } catch { return "?"; } }
		string Estado() { try { return fState?.GetValue(g) as string ?? "?"; } catch { return "?"; } }
		int[] Wins() => fWins?.GetValue(g.mode) as int[] ?? new int[2];
		FBMatch.God = true;
		var entrada = new FBInputState();
		FBPlayer.TestInput = inp => entrada;
		void DarFuzil()
		{
			try
			{
				if (P.wu.slots.Any(x => x != null && x.id == "ak")) { P.wu.Equip(Array.FindIndex(P.wu.slots, x => x != null && x.id == "ak"), instant: true); return; }
				P.wu.Give("ak");
				int s = Array.FindIndex(P.wu.slots, x => x != null && x.id == "ak");
				if (s >= 0) P.wu.Equip(s, instant: true);
			}
			catch (Exception e) { Log("jogo: fuzil falhou: " + e.Message); }
		}
		// fase de compra: loja aberta
		yield return Wait(2.5f);
		if (Fase() == "buy")
		{
			try { FBShop.Show(true); } catch (Exception e) { Log("loja: " + e.Message); }
			yield return Wait(1.5f);
			yield return TelaHud($"{mapa}_loja");
			try { FBShop.Show(false); } catch { }
		}
		DarFuzil();
		int rodada = 1, fotosCombate = 0;
		float proxCombate = 0f;
		bool encenado = false;
		float limite = 0f;
		var hp = new Dictionary<FBCharacter, float>();
		string faseAnt = Fase();
		while (FBMatch.I == g && limite < 900f)
		{
			yield return null;
			limite += Time.unscaledDeltaTime;
			string f = Fase();
			if (f != faseAnt)
			{
				// fim de rodada ANTES de contar a rodada nova (live -> buy direto acontece quando a pós-rodada de 5 s
				// passou inteira dentro de uma foto de combate)
				if (faseAnt == "live" && f != "live")
				{
					yield return Wait(0.6f);
					var w = Wins();
					Log($"{mapa} jogo: rodada {rodada} acabou · placar {w[0]} × {w[1]} · fase agora {Fase()}");
					if (rodada <= 2 && Fase() == "post") yield return TelaHud($"{mapa}_fim_rodada{rodada}");
				}
				if (f == "buy")
				{
					rodada++;
					DarFuzil();
					// placar montado no começo da 3ª rodada do MODO (não do contador daqui), com a rodada já certa (12)
					int rm = fRound != null ? Convert.ToInt32(fRound.GetValue(g.mode)) : rodada;
					if (rm == 3 && !encenado && fWins != null)
					{
						encenado = true;
						var w = Wins();
						w[0] = win - 1; w[1] = win - 2;
						try { fRound?.SetValue(g.mode, 2 * win - 2); } catch { }
						Log($"{mapa} jogo: PLACAR MONTADO para a foto da vitória: {w[0]} × {w[1]} (rodada {fRound?.GetValue(g.mode)})");
					}
				}
				faseAnt = f;
			}
			// depois do placar montado, a primeira pós-rodada fecha a partida com vitória do time do jogador
			if (encenado && f == "post")
			{
				var w = Wins();
				if (w[0] < win) { w[0] = win; w[1] = Mathf.Min(w[1], win - 2); Log($"{mapa} jogo: vitória garantida {w[0]} × {w[1]}"); }
			}
			if (Estado() == "end")
			{
				yield return Wait(7.5f);
				var w = Wins();
				yield return TelaHud($"{mapa}_fim_partida");
				Log($"{mapa} jogo: fim da partida {w[0]} × {w[1]}");
				break;
			}
			if (f != "live") { hp.Clear(); continue; }
			// primeiro acerto da rodada: o jogador vai para trás de um aliado que está atirando e mira junto
			FBCharacter alvo = null;
			foreach (var ch in FBMatch.All)
			{
				if (!ch || ch.team > 1 || ch.isPlayer) continue;
				if (hp.TryGetValue(ch, out float h0v) && ch.hp < h0v - 0.01f && ch.alive && ch.team != me.team && !alvo) alvo = ch;
				hp[ch] = ch.hp;
			}
			if (alvo && fotosCombate < rodada * 2 && fotosCombate < 4 && limite >= proxCombate)
			{
				var aliado = FBMatch.All.Where(c => c && c.alive && !c.isPlayer && c.team == me.team).OrderBy(c => (c.Pos - alvo.Pos).sqrMagnitude).FirstOrDefault();
				if (aliado && (aliado.Pos - alvo.Pos).magnitude < 90f)
				{
					fotosCombate++;
					proxCombate = limite + 20f;
					Vector3 d = alvo.Pos - aliado.Pos; d.y = 0f;
					float dist = d.magnitude;
					d = dist > 0.1f ? d / dist : Vector3.forward;
					Vector3 lado = Vector3.Cross(Vector3.up, d);
					Vector3 pos = aliado.Pos - d * 2.6f + lado * 1.4f;
					float? gch = Chao(pos + Vector3.up * 3f, 8f);
					if (gch != null) pos.y = gch.Value + 0.05f;
					var cc = me.GetComponent<CharacterController>();
					if (cc) cc.enabled = false;
					me.transform.position = pos;
					float yaw = Mathf.Atan2(alvo.Pos.x - pos.x, alvo.Pos.z - pos.z) * Mathf.Rad2Deg;
					me.Yaw = yaw;
					if (cc) cc.enabled = true;
					try { P.OnRespawn(yaw); } catch { }
					DarFuzil();
					entrada = new FBInputState { aim = true, fire = true };
					yield return Wait(0.8f);
					entrada = new FBInputState { aim = true };
					yield return Wait(0.4f);
					try { FBHUD.I?.Center("", 0f); } catch { }
					yield return TelaHud($"{mapa}_combate{fotosCombate}");
					// a mesma luta vista de lado, sem HUD, em 4K
					Vector3 luta = (aliado.Pos + alvo.Pos) * 0.5f;
					Vector3 cpos = default; bool okLado = false;
					foreach (float sg in new[] { 1f, -1f })
					{
						foreach (float k in new[] { 1f, 0.7f, 1.4f, 0.5f })
						{
							Vector3 c = luta + lado * (sg * k * Mathf.Clamp(dist * 0.45f + 5f, 7f, 30f));
							float? gc = Chao(new Vector3(c.x, luta.y + 3f, c.z), 8f);
							if (gc == null || gc.Value > luta.y + 1.5f) continue;
							c.y = gc.Value + 1.6f;
							if (Physics.CheckSphere(c, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
							if (!Visivel(c, aliado.Pos + Vector3.up * 1.2f, null) || !Visivel(c, alvo.Pos + Vector3.up * 1.2f, null)) continue;
							cpos = c; okLado = true; break;
						}
						if (okLado) break;
					}
					if (!okLado) { Log($"{mapa} jogo: combate {fotosCombate} de lado pulado, sem lugar com visão"); entrada = new FBInputState(); hp.Clear(); continue; }
					var dd = luta + Vector3.up * 1.2f - cpos;
					posePos = cpos;
					poseRot = Quaternion.LookRotation(dd);
					poseFov = 50f;
					miraEm = alvo;
					poseOn = true;
					yield return Wait(0.2f);
					yield return Foto3D(Path.Combine(dir, "jogo"), $"{mapa}_combate{fotosCombate}_de_lado");
					poseOn = false;
					miraEm = null;
					if (Camera.main) Camera.main.ResetAspect();
					entrada = new FBInputState();
					Log($"{mapa} jogo: combate {fotosCombate} na rodada {rodada}: {aliado.name} contra {alvo.name} a {dist:0} m");
					hp.Clear();
				}
			}
		}
		if (Estado() != "end") Log($"{mapa} jogo: saiu sem fim de partida (limite {limite:0} s, partida {(FBMatch.I == g ? "a mesma" : "trocou")}, fase {Fase()}, rodada {rodada})");
		hudVitrine = false;
		FBPlayer.TestInput = null;
		FBMatch.God = false;
		if (TelaMudou()) { Screen.SetResolution(w0, h0, m0); yield return Wait(1f); Log($"jogo: tela devolvida, {Screen.width}x{Screen.height} {Screen.fullScreenMode}"); }
	}
}
