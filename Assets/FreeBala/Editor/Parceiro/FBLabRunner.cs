// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/FBLabRunner.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// Ferramenta de teste (pode apagar depois). Roda no Editor os testes automáticos que o próprio jogo já tem
// (FBCarroLab, FBMotoLab, FBArmaLab, FBDriveLab), que no jogo compilado só ligam por linha de comando.
// Uso: criar Buritiro_Unity\_diag\lab_pedido.txt com uma linha, por exemplo "motolab vila_jardim:classico mx_xj6".
// O Editor entra no Play, liga o teste, grava o relatório em _diag\lab_log.txt e sai do Play quando o teste termina.
// "build" ou "build il2cpp" gera um .exe de teste em Buritiro_Unity\Builds\teste_<data>\BURITIRO.exe (fora do Play).
// "metodo Classe.Metodo" chama um método estático sem parâmetros do Editor (ex.: "metodo BuritiroVilaJardim.Tudo").
// "refresh" faz AssetDatabase.Refresh. Cada compilação de scripts grava o resultado em _diag\lab_compilacao.txt.
// v5: "build" recusa quando uma cena aberta tem mudanças não salvas (antes o Unity abria a janela "Scene(s) Have Been
// Modified" e o build ficava parado esperando um clique). "salvar" grava as cenas abertas com mudanças e "descartar"
// reabre do disco a cena ativa, jogando fora o que não foi salvo. Os dois anotam no lab_log quais cenas eram.
// [unificação 02/11] a pasta de pedidos continua sendo <Assets>/../../../_diag, como na máquina do parceiro (lá dava em
// Buritiro_Unity\_diag). No nosso projeto (game\unity\FreeBala\Assets) isso dá em favela\game\_diag; o "build" grava em
// favela\game\Builds\teste_<data>\. Sem lab_pedido.txt nessa pasta o runner não faz nada (só anota compilações em lab_compilacao.txt).
// FBLabFisica e FBVigiaSom aqui ficam no namespace FreeBala (Runtime/Teste); no parceiro eram do Assembly-CSharp sem namespace.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
static class FBLabRunner
{
	const string chave = "fb_lab_pedido";
	const string chaveFim = "fb_lab_fim";
	const string chaveInicio = "fb_lab_inicio";
	static readonly string pasta = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag"));
	static readonly object trava = new object();
	static double proximo;
	// cópia do pedido para o Log: o Unity também manda mensagens de outras threads (ao carregar cena),
	// e SessionState só pode ser lido na thread principal (era o erro "GetString can only be called from the main thread")
	static volatile bool ativo;

	static FBLabRunner()
	{
		EditorApplication.update += Tick;
		Application.logMessageReceivedThreaded += Log;
		EditorApplication.playModeStateChanged += Mudou;
		UnityEditor.Compilation.CompilationPipeline.assemblyCompilationFinished += Compilou;
		UnityEditor.Compilation.CompilationPipeline.compilationFinished += TerminouCompilar;
	}

	static int errosCompilacao;

	static void Compilou(string dll, UnityEditor.Compilation.CompilerMessage[] msgs)
	{
		try
		{
			foreach (var m in msgs)
			{
				if (m.type != UnityEditor.Compilation.CompilerMessageType.Error) continue;
				errosCompilacao++;
				if (errosCompilacao <= 40) File.AppendAllText(Arquivo("lab_compilacao.txt"), $"[{DateTime.Now:HH:mm:ss}] ERRO {Path.GetFileName(dll)}: {m.message}\n");
			}
		}
		catch { }
	}

	static void TerminouCompilar(object _)
	{
		try { File.AppendAllText(Arquivo("lab_compilacao.txt"), $"[{DateTime.Now:HH:mm:ss}] compilação terminou · erros {errosCompilacao}\n"); } catch { }
		errosCompilacao = 0;
	}

	static void Metodo(string nome)
	{
		var log = Arquivo("lab_log.txt");
		File.AppendAllText(log, $"\n===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} metodo {nome} =====\n");
		File.WriteAllText(Arquivo("lab_status.txt"), "rodando metodo " + nome);
		try
		{
			int i = nome.LastIndexOf('.');
			if (i <= 0) throw new ArgumentException("use Classe.Metodo");
			string tn = nome.Substring(0, i), mn = nome.Substring(i + 1);
			var tipo = AppDomain.CurrentDomain.GetAssemblies().Select(a => { try { return a.GetType(tn); } catch { return null; } }).FirstOrDefault(x => x != null);
			if (tipo == null) throw new Exception("classe não encontrada: " + tn);
			var mi = tipo.GetMethod(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
			if (mi == null) throw new Exception("método estático sem parâmetros não encontrado: " + mn);
			var t0 = DateTime.Now;
			mi.Invoke(null, null);
			File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss}] metodo {nome} terminou em {(DateTime.Now - t0).TotalSeconds:0} s\n");
		}
		catch (Exception e)
		{
			File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss}] ERRO no metodo {nome}: {(e is TargetInvocationException && e.InnerException != null ? e.InnerException : e)}\n");
		}
		finally
		{
			File.WriteAllText(Arquivo("lab_status.txt"), "livre " + DateTime.Now.ToString("HH:mm:ss"));
		}
	}

	static string Arquivo(string n) => Path.Combine(pasta, n);

	static void Log(string msg, string stack, LogType t)
	{
		if (!ativo) return;
		if (t == LogType.Log && !msg.StartsWith("[FreeBala]")) return;
		lock (trava)
			File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] {(t == LogType.Log ? "" : t + ": ")}{msg}\n{(t == LogType.Log ? "" : stack)}");
		if (t == LogType.Log && msg.Contains("lab] fim")) SessionState.SetBool(chaveFim, true);
	}

	static void Tick()
	{
		if (EditorApplication.timeSinceStartup < proximo) return;
		proximo = EditorApplication.timeSinceStartup + 1;
		var pedido = SessionState.GetString(chave, "");
		ativo = pedido != "";
		if (EditorApplication.isPlayingOrWillChangePlaymode)
		{
			if (pedido == "" || !EditorApplication.isPlaying) return;
			var passou = EditorApplication.timeSinceStartup - SessionState.GetFloat(chaveInicio, 0);
			if (SessionState.GetBool(chaveFim, false) || passou > 1200)
			{
				File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] === saindo do Play ({(passou > 1200 ? "tempo esgotado" : "teste terminou")}) ===\n");
				EditorApplication.ExitPlaymode();
			}
			return;
		}
		if (pedido != "")
		{
			// saiu do Play: encerra o pedido
			SessionState.EraseString(chave);
			File.WriteAllText(Arquivo("lab_status.txt"), "livre " + DateTime.Now.ToString("HH:mm:ss"));
			return;
		}
		if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Arquivo("lab_pedido.txt"))) return;
		pedido = File.ReadAllText(Arquivo("lab_pedido.txt")).Trim();
		File.Delete(Arquivo("lab_pedido.txt"));
		if (pedido == "") return;
		if ((pedido.StartsWith("build") || pedido.StartsWith("metodo ")) && EditorUtility.scriptCompilationFailed)
		{
			File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] recusado '{pedido}': scripts com erro de compilação (ver lab_compilacao.txt)\n");
			File.WriteAllText(Arquivo("lab_status.txt"), "recusado: scripts com erro de compilação");
			return;
		}
		if (pedido == "salvar" || pedido == "descartar")
		{
			// v5b: nunca abre janela modal. Cena sem arquivo (Untitled) não é salva nem "descartada" por aqui:
			// "salvar" salva só as cenas que já têm arquivo; "descartar" reabre do disco as cenas com arquivo e
			// troca uma Untitled ativa pela cena do menu.
			var abertas = Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).ToList();
			var sujas = abertas.Where(x => x.isDirty).ToList();
			string Nome(UnityEngine.SceneManagement.Scene x) => string.IsNullOrEmpty(x.path) ? "(sem arquivo)" : x.path;
			bool ok = true;
			string obs = "";
			try
			{
				if (pedido == "salvar")
				{
					foreach (var x in sujas)
					{
						if (string.IsNullOrEmpty(x.path)) { ok = false; obs += " · cena sem arquivo não salva"; continue; }
						if (!EditorSceneManager.SaveScene(x)) { ok = false; obs += $" · {x.path} não salvou"; }
					}
				}
				else if (sujas.Count > 0)
				{
					var ativa = EditorSceneManager.GetActiveScene();
					string abrir = !string.IsNullOrEmpty(ativa.path) ? ativa.path : "Assets/FreeBala/Scenes/Menu.unity";
					var outras = abertas.Where(x => !string.IsNullOrEmpty(x.path) && x.path != abrir).Select(x => x.path).ToList();
					EditorSceneManager.OpenScene(abrir, OpenSceneMode.Single);
					foreach (var o in outras) EditorSceneManager.OpenScene(o, OpenSceneMode.Additive);
					obs = $" · reaberta {abrir}" + (outras.Count > 0 ? $" + {outras.Count} junto" : "");
				}
			}
			catch (Exception e) { ok = false; obs += " · erro: " + e.Message; }
			bool aindaSuja = Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).Any(x => x.isDirty);
			if (aindaSuja) ok = false;
			File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] {pedido}: cenas com mudanças antes: {(sujas.Count == 0 ? "nenhuma" : string.Join(", ", sujas.Select(Nome)))}{obs} · {(ok ? "feito" : "FALHOU")}{(aindaSuja ? " (ainda há cena com mudanças)" : "")}\n");
			File.WriteAllText(Arquivo("lab_status.txt"), ok ? "livre " + DateTime.Now.ToString("HH:mm:ss") : $"recusado: {pedido} não terminou ({obs.Trim(' ', '·')})");
			return;
		}
		if (pedido.StartsWith("build"))
		{
			for (int k = 0; k < EditorSceneManager.sceneCount; k++)
			{
				var sc = EditorSceneManager.GetSceneAt(k);
				if (sc.isDirty)
				{
					File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] recusado '{pedido}': a cena {sc.path} tem mudanças não salvas (peça \"salvar\" ou \"descartar\")\n");
					File.WriteAllText(Arquivo("lab_status.txt"), "recusado: a cena aberta tem mudanças não salvas");
					return;
				}
			}
			Construir(pedido.Split(' ', StringSplitOptions.RemoveEmptyEntries));
			return;
		}
		if (pedido.StartsWith("metodo "))
		{
			for (int k = 0; k < EditorSceneManager.sceneCount; k++)
			{
				var sc = EditorSceneManager.GetSceneAt(k);
				if (sc.isDirty)
				{
					File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] recusado '{pedido}': a cena {sc.path} tem mudanças não salvas\n");
					File.WriteAllText(Arquivo("lab_status.txt"), "recusado: a cena aberta tem mudanças não salvas");
					return;
				}
			}
			Metodo(pedido.Substring(7).Trim());
			return;
		}
		if (pedido == "refresh")
		{
			File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] refresh pedido\n");
			AssetDatabase.Refresh();
			File.WriteAllText(Arquivo("lab_status.txt"), "livre " + DateTime.Now.ToString("HH:mm:ss"));
			return;
		}
		var menu = "Assets/FreeBala/Scenes/Menu.unity";
		if (EditorSceneManager.GetActiveScene().path != menu)
		{
			if (Enumerable.Range(0, EditorSceneManager.sceneCount).Any(k => EditorSceneManager.GetSceneAt(k).isDirty))
			{
				File.WriteAllText(Arquivo("lab_status.txt"), "recusado: a cena aberta tem mudanças não salvas");
				return;
			}
			EditorSceneManager.OpenScene(menu);
		}
		SessionState.SetString(chave, pedido);
		ativo = true;
		SessionState.SetBool(chaveFim, false);
		SessionState.SetFloat(chaveInicio, (float)EditorApplication.timeSinceStartup);
		File.AppendAllText(Arquivo("lab_log.txt"), $"\n===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} pedido: {pedido} =====\n");
		File.WriteAllText(Arquivo("lab_status.txt"), "rodando " + pedido);
		EditorApplication.EnterPlaymode();
	}

	static void Mudou(PlayModeStateChange s)
	{
		if (s != PlayModeStateChange.EnteredPlayMode) return;
		var pedido = SessionState.GetString(chave, "");
		ativo = pedido != "";
		if (pedido == "") return;
		SessionState.SetFloat(chaveInicio, (float)EditorApplication.timeSinceStartup);
		var p = pedido.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var nomes = new System.Collections.Generic.Dictionary<string, string>
		{
			{ "carrolab", "FreeBala.FBCarroLab" }, { "motolab", "FreeBala.FBMotoLab" },
			{ "armalab", "FreeBala.FBArmaLab" }, { "drivelab", "FreeBala.FBDriveLab" }, { "fisicalab", "FreeBala.FBLabFisica" }, // [unificação 02/11] namespace FreeBala no nosso fonte
		};
		if (!nomes.TryGetValue(p[0], out var nomeTipo)) { Erro("teste desconhecido: " + p[0]); return; }
		var tipo = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(nomeTipo)).FirstOrDefault(x => x != null);
		if (tipo == null) { Erro("classe não encontrada: " + nomeTipo); return; }
		// o teste continua mesmo quando a janela do Unity perde o foco (só durante o teste, não muda o projeto)
		Application.runInBackground = true;
		var go = new GameObject("FB_" + p[0]);
		UnityEngine.Object.DontDestroyOnLoad(go);
		// os campos são preenchidos antes do Start, igual ao Boot() do jogo faz com a linha de comando
		var c = go.AddComponent(tipo);
		go.AddComponent<FreeBala.FBVigiaSom>(); // [unificação 02/11] namespace FreeBala no nosso fonte (Runtime/Teste)
		var campos = p[0] == "armalab" ? new[] { "wid" } : p[0] == "motolab" ? new[] { "run", "moto" } : p[0] == "fisicalab" ? new[] { "parte", "lista" } : new[] { "run" };
		for (int i = 0; i < campos.Length && i + 1 < p.Length; i++)
			tipo.GetField(campos[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(c, p[i + 1]);
		File.AppendAllText(Arquivo("lab_log.txt"), $"[{DateTime.Now:HH:mm:ss}] {nomeTipo} ligado com {string.Join(" ", p.Skip(1))}\n");
	}

	static void Construir(string[] p)
	{
		bool il2cpp = p.Contains("il2cpp");
		var nome = "teste_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + (il2cpp ? "_il2cpp" : "_mono");
		var saida = Path.GetFullPath(Path.Combine(pasta, "../Builds", nome, "BURITIRO.exe"));
		var log = Arquivo("lab_log.txt");
		File.WriteAllText(Arquivo("lab_status.txt"), "compilando " + nome);
		File.AppendAllText(log, $"\n===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} build {nome} =====\n");
		var alvo = UnityEditor.Build.NamedBuildTarget.Standalone;
		var backendAntes = PlayerSettings.GetScriptingBackend(alvo);
		var produtoAntes = PlayerSettings.productName;
		try
		{
			if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
				EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
			PlayerSettings.SetScriptingBackend(alvo, il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
			// nome separado: o .exe de teste guarda configurações e saves longe do jogo original
			PlayerSettings.productName = produtoAntes + " TESTE";
			var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
			{
				scenes = EditorBuildSettings.scenes.Where(c => c.enabled).Select(c => c.path).ToArray(),
				locationPathName = saida,
				target = BuildTarget.StandaloneWindows64,
				options = BuildOptions.None,
			});
			var res = r.summary;
			File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss}] build {res.result} · {res.totalSize / 1048576f:0} MB · {res.totalTime:hh\\:mm\\:ss} · erros {res.totalErrors} · avisos {res.totalWarnings} · {saida}\n");
			foreach (var msg in r.steps.SelectMany(st => st.messages).Where(x => x.type == LogType.Error || x.type == LogType.Exception).Take(40))
				File.AppendAllText(log, "  ERRO: " + msg.content + "\n");
		}
		catch (Exception e)
		{
			File.AppendAllText(log, "ERRO no build: " + e + "\n");
		}
		finally
		{
			PlayerSettings.SetScriptingBackend(alvo, backendAntes);
			PlayerSettings.productName = produtoAntes;
			AssetDatabase.SaveAssets();
			File.WriteAllText(Arquivo("lab_status.txt"), "livre " + DateTime.Now.ToString("HH:mm:ss"));
		}
	}

	static void Erro(string m)
	{
		File.AppendAllText(Arquivo("lab_log.txt"), "ERRO: " + m + "\n");
		SessionState.SetBool(chaveFim, true);
	}
}
