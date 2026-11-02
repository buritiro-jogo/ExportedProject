// Diagnóstico temporário (pode apagar depois). Durante o Play grava em Buritiro_Unity\_diag:
//  - console.txt: todos os erros e avisos com o caminho completo (stack trace)
//  - diag.txt: câmeras, canvases, volumes, céu e shaders com problema da cena atual
//  - teste_*.png + teste.txt: na partida, desenha a câmera principal numa imagem, uma vez normal e
//    outra vez desligando cada efeito, para provar qual efeito deixa a tela preta ou rosa.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
static class FBDiagnostico
{
	static double next, mapaDesde = -1, proximoTeste;
	static int testes;
	static string pasta;
	static readonly object trava = new object();

	static FBDiagnostico()
	{
		pasta = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../_diag"));
		Directory.CreateDirectory(pasta);
		EditorApplication.update += Tick;
		Application.logMessageReceivedThreaded += Log;
		EditorApplication.playModeStateChanged += s =>
		{
			if (s == PlayModeStateChange.EnteredPlayMode)
			{
				testes = 0;
				mapaDesde = -1;
				File.AppendAllText(System.IO.Path.Combine(pasta, "console.txt"), $"\n===== PLAY {DateTime.Now:HH:mm:ss} =====\n");
			}
		};
	}

	static void Log(string msg, string stack, LogType t)
	{
		if (t == LogType.Log) return;
		lock (trava)
			File.AppendAllText(System.IO.Path.Combine(pasta, "console.txt"), $"[{DateTime.Now:HH:mm:ss}] {t}: {msg}\n{stack}\n");
	}

	static void Tick()
	{
		if (!EditorApplication.isPlaying) return;
		var cena = SceneManager.GetActiveScene().name;
		var agora = EditorApplication.timeSinceStartup;
		if (cena.StartsWith("Map_") && Time.timeScale > 0 && Camera.main)
		{
			if (mapaDesde < 0) { mapaDesde = agora; proximoTeste = agora + 12; }
			if (testes < 3 && agora >= proximoTeste)
			{
				testes++;
				proximoTeste = agora + 45;
				try { Teste(); }
				catch (Exception e) { File.AppendAllText(System.IO.Path.Combine(pasta, "teste.txt"), "ERRO no teste: " + e + "\n"); }
			}
		}
		else mapaDesde = -1;
		if (agora < next) return;
		next = agora + 10;
		File.WriteAllText(System.IO.Path.Combine(pasta, "diag.txt"), Estado());
	}

	static string Estado()
	{
		var sb = new StringBuilder();
		sb.AppendLine($"hora {DateTime.Now:HH:mm:ss} cena {SceneManager.GetActiveScene().name} timeScale {Time.timeScale} tela {Screen.width}x{Screen.height}");
		sb.AppendLine($"pipeline {(GraphicsSettings.currentRenderPipeline ? GraphicsSettings.currentRenderPipeline.name : "nenhum")} qualidade {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
		var sky = RenderSettings.skybox;
		sb.AppendLine($"ceu={(sky ? sky.name + " / " + Sh(sky.shader) : "-")} ambiente={RenderSettings.ambientMode} sol={(RenderSettings.sun ? RenderSettings.sun.name + " int=" + RenderSettings.sun.intensity : "-")} nevoa={RenderSettings.fog}");
		sb.AppendLine("== CAMERAS");
		foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
		{
			var d = c.GetComponent<UniversalAdditionalCameraData>();
			sb.AppendLine($"{Path(c.transform)} ativo={c.isActiveAndEnabled} depth={c.depth} mask={c.cullingMask} clear={c.clearFlags} fundo={c.backgroundColor} rt={(c.targetTexture ? c.targetTexture.name : "-")} rect={c.rect} near={c.nearClipPlane} far={c.farClipPlane} pos={c.transform.position} display={c.targetDisplay}"
				+ (d ? $" tipo={d.renderType} pos-proc={d.renderPostProcessing} pilha={(d.renderType == CameraRenderType.Base ? d.cameraStack?.Count : 0)} renderer={Campo(d, "m_RendererIndex")} volumeMask={d.volumeLayerMask.value}" : " (sem dados URP)"));
		}
		sb.AppendLine("== CANVAS");
		foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(x => x.isRootCanvas))
			sb.AppendLine($"{Path(cv.transform)} ligado={cv.enabled} modo={cv.renderMode} ordem={cv.sortingOrder} cam={(cv.worldCamera ? cv.worldCamera.name : "-")} alpha={(cv.GetComponent<CanvasGroup>() ? cv.GetComponent<CanvasGroup>().alpha : 1)}");
		sb.AppendLine("== IMAGENS GRANDES E ESCURAS");
		foreach (var g in Object.FindObjectsByType<Graphic>(FindObjectsInactive.Exclude))
		{
			var r = g.rectTransform.rect;
			var col = g.color;
			if (!g.isActiveAndEnabled || !g.canvas || !g.canvas.enabled || col.a < 0.5f || r.width * g.transform.lossyScale.x < Screen.width * 0.7f) continue;
			if (col.r + col.g + col.b > 0.6f && !(g is RawImage)) continue;
			sb.AppendLine($"{Path(g.transform)} tipo={g.GetType().Name} cor={col} tam={r.size} mat={(g.material ? g.material.name + "/" + Sh(g.material.shader) : "-")}");
		}
		sb.AppendLine("== VOLUMES");
		foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude))
		{
			sb.AppendLine($"{Path(v.transform)} global={v.isGlobal} peso={v.weight} prioridade={v.priority} camada={v.gameObject.layer} perfil={(v.sharedProfile ? v.sharedProfile.name : "-")}");
			if (!v.sharedProfile) continue;
			foreach (var comp in v.sharedProfile.components.Where(x => x.active))
				sb.AppendLine("   " + comp.GetType().Name + ": " + string.Join(", ", comp.parameters.Select((p, i) => (p, i)).Where(x => x.p.overrideState).Select(x => Nome(comp, x.i) + "=" + Valor(x.p))));
		}
		sb.AppendLine("== RENDERER FEATURES");
		foreach (var rd in Dados())
			sb.AppendLine($"{rd.name}: " + string.Join(", ", rd.rendererFeatures.Where(f => f).Select(f => f.name + (f.isActive ? "(ligado)" : "(desligado)"))));
		sb.AppendLine("== SHADERS DOS OBJETOS NA CENA (quantos materiais)");
		var cont = new Dictionary<string, int>();
		foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
			foreach (var m in r.sharedMaterials)
			{
				var k = m ? Sh(m.shader) : "(material vazio)";
				cont[k] = cont.TryGetValue(k, out var n) ? n + 1 : 1;
			}
		foreach (var kv in cont.OrderByDescending(x => x.Value)) sb.AppendLine($"   {kv.Value,6}  {kv.Key}");
		return sb.ToString();
	}

	static void Teste()
	{
		var cam = Camera.main;
		var d = cam.GetComponent<UniversalAdditionalCameraData>();
		var sb = new StringBuilder($"\n=== teste {testes} {DateTime.Now:HH:mm:ss} cena {SceneManager.GetActiveScene().name} camera {Path(cam.transform)} pos={cam.transform.position} rot={cam.transform.eulerAngles}\n");
		var pre = $"teste{testes}_";
		ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(pasta, pre + "00_tela_do_jogo.png"));
		sb.AppendLine(Foto(cam, pre + "01_normal"));
		if (d)
		{
			var pp = d.renderPostProcessing;
			d.renderPostProcessing = false;
			sb.AppendLine(Foto(cam, pre + "02_sem_posprocessamento"));
			d.renderPostProcessing = pp;
		}
		var feats = Dados().SelectMany(x => x.rendererFeatures).Where(f => f && f.isActive).Distinct().ToList();
		foreach (var f in feats)
		{
			f.SetActive(false);
			try { sb.AppendLine(Foto(cam, pre + "03_sem_" + f.name)); }
			finally { f.SetActive(true); }
		}
		foreach (var f in feats) f.SetActive(false);
		bool pp2 = d && d.renderPostProcessing;
		if (d) d.renderPostProcessing = false;
		try { sb.AppendLine(Foto(cam, pre + "04_sem_nada")); }
		finally
		{
			foreach (var f in feats) f.SetActive(true);
			if (d) d.renderPostProcessing = pp2;
		}
		var cf = cam.clearFlags; var bg = cam.backgroundColor;
		cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
		try { sb.AppendLine(Foto(cam, pre + "05_fundo_verde_sem_ceu")); }
		finally { cam.clearFlags = cf; cam.backgroundColor = bg; }
		File.AppendAllText(System.IO.Path.Combine(pasta, "teste.txt"), sb.ToString());
	}

	static string Foto(Camera cam, string nome)
	{
		var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
		var velho = cam.targetTexture;
		try
		{
			var req = new RenderPipeline.StandardRequest { destination = rt };
			if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
			else { cam.targetTexture = rt; cam.Render(); }
			var a = RenderTexture.active;
			RenderTexture.active = rt;
			var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
			tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			tex.Apply();
			RenderTexture.active = a;
			File.WriteAllBytes(System.IO.Path.Combine(pasta, nome + ".png"), tex.EncodeToPNG());
			var px = tex.GetPixels32();
			double soma = 0; int rosa = 0, preto = 0;
			foreach (var p in px)
			{
				soma += (p.r + p.g + p.b) / 3.0;
				if (p.r > 200 && p.b > 200 && p.g < 60) rosa++;
				if (p.r < 8 && p.g < 8 && p.b < 8) preto++;
			}
			Object.DestroyImmediate(tex);
			return $"{nome}: brilho medio {soma / px.Length:0} de 255, preto {100.0 * preto / px.Length:0}%, rosa {100.0 * rosa / px.Length:0}%";
		}
		catch (Exception e) { return nome + ": ERRO " + e.Message; }
		finally { cam.targetTexture = velho; rt.Release(); Object.DestroyImmediate(rt); }
	}

	static IEnumerable<ScriptableRendererData> Dados()
	{
		var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		if (!asset) yield break;
		var lista = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(asset) as ScriptableRendererData[];
		if (lista == null) yield break;
		foreach (var x in lista) if (x) yield return x;
	}

	static string Sh(Shader s) => !s ? "(sem shader)" : s.name + (s.isSupported ? "" : " [NAO SUPORTADO]");
	static object Campo(object o, string n) => o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(o);
	static string Nome(VolumeComponent c, int i)
	{
		var campos = c.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => typeof(VolumeParameter).IsAssignableFrom(f.FieldType)).ToList();
		return i < campos.Count ? campos[i].Name : "p" + i;
	}
	static string Valor(VolumeParameter p)
	{
		var prop = p.GetType().GetProperty("value");
		var v = prop?.GetValue(p);
		return v is Object uo ? (uo ? uo.name : "null") : v?.ToString() ?? "null";
	}
	static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
}
