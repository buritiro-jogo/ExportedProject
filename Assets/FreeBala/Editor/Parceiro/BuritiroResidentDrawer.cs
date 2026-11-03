// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/BuritiroResidentDrawer.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// BURITIRO: liga/desliga o GPU Resident Drawer do Unity 6 para o teste de 120 FPS (sugestão da thread dos mapas).
// Liga só nos URP com renderizador Forward+ (PC e PC Médio). O GPU Resident Drawer desenha malhas repetidas com o mesmo material (árvores, postes, bancos das Celulas) como instâncias,
// sem o custo de um draw call por renderizador na thread principal. Precisa de Forward+, SRP Batcher e das variantes BRG no build.
// Pedidos do lab: "metodo BuritiroResidentDrawer.Estado", "metodo BuritiroResidentDrawer.Liga", "metodo BuritiroResidentDrawer.Desliga".
// Liga guarda os valores de antes em _diag\resident_drawer_antes.txt e copia os .asset para _backup\resident_drawer_<data>; Desliga volta esses valores.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuritiroResidentDrawer
{
	static string Diag => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag"));
	static string Raiz => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
	static string Projeto => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

	static void Log(StringBuilder sb, string s) { sb.AppendLine(s); Debug.Log("[BURITIRO] resident drawer: " + s); }

	static List<UniversalRenderPipelineAsset> Assets()
	{
		var l = new List<UniversalRenderPipelineAsset>();
		void Add(RenderPipelineAsset a) { if (a is UniversalRenderPipelineAsset u && !l.Contains(u)) l.Add(u); }
		Add(GraphicsSettings.defaultRenderPipeline);
		for (int i = 0; i < QualitySettings.names.Length; i++) Add(QualitySettings.GetRenderPipelineAssetAt(i));
		return l;
	}

	static List<UniversalRendererData> Renderers(UniversalRenderPipelineAsset a)
	{
		var l = new List<UniversalRendererData>();
		foreach (var d in a.rendererDataList) if (d is UniversalRendererData u && !l.Contains(u)) l.Add(u);
		return l;
	}

	static SerializedProperty Brg(out SerializedObject so)
	{
		so = null;
		var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
		if (!gs) return null;
		so = new SerializedObject(gs);
		return so.FindProperty("m_BrgStripping");
	}

	static Type BrgEnum()
	{
		foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
		{
			Type[] ts;
			try { ts = asm.GetTypes(); } catch { continue; }
			foreach (var t in ts) if (t.IsEnum && t.Name == "BatchRendererGroupStrippingMode") return t;
		}
		return null;
	}

	static string Texto()
	{
		var sb = new StringBuilder();
		var brg = Brg(out _);
		var et = BrgEnum();
		string brgTxt = brg == null ? "campo m_BrgStripping não achado" : (et != null && Enum.IsDefined(et, brg.intValue) ? Enum.GetName(et, brg.intValue) : brg.intValue.ToString());
		sb.AppendLine($"variantes BatchRendererGroup: {brgTxt}");
		sb.AppendLine($"static batching do Player: {GetStaticBatching()}");
		for (int i = 0; i < QualitySettings.names.Length; i++)
		{
			var a = QualitySettings.GetRenderPipelineAssetAt(i);
			sb.AppendLine($"qualidade {i} {QualitySettings.names[i]}: {(a ? a.name : "(usa o padrão)")}");
		}
		foreach (var a in Assets())
		{
			sb.AppendLine($"URP {a.name} ({AssetDatabase.GetAssetPath(a)}): GPU Resident Drawer {a.gpuResidentDrawerMode} · oclusão na GPU {a.gpuResidentDrawerEnableOcclusionCullingInCameras} · SRP Batcher {a.useSRPBatcher}");
			foreach (var r in Renderers(a)) sb.AppendLine($"   renderizador {r.name} ({AssetDatabase.GetAssetPath(r)}): {r.renderingMode}");
		}
		return sb.ToString();
	}

	static string GetStaticBatching()
	{
		try
		{
			var m = typeof(PlayerSettings).GetMethod("GetStaticBatchingForPlatform", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
			if (m != null) return m.Invoke(null, new object[] { EditorUserBuildSettings.activeBuildTarget }).ToString();
		}
		catch { }
		return "?";
	}

	public static void Estado()
	{
		var t = Texto();
		Directory.CreateDirectory(Diag);
		File.WriteAllText(Path.Combine(Diag, "resident_drawer.txt"), DateTime.Now.ToString("HH:mm:ss") + " estado\n" + t, new UTF8Encoding(true));
		Debug.Log("[BURITIRO] resident drawer estado:\n" + t);
	}

	public static void Liga()
	{
		var sb = new StringBuilder();
		Directory.CreateDirectory(Diag);
		string antes = Path.Combine(Diag, "resident_drawer_antes.txt");
		// backup dos arquivos antes de mexer
		string bk = Path.Combine(Raiz, "_backup", "resident_drawer_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
		Directory.CreateDirectory(bk);
		var arquivos = new List<string> { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset" };
		foreach (var a in Assets()) { arquivos.Add(AssetDatabase.GetAssetPath(a)); foreach (var r in Renderers(a)) arquivos.Add(AssetDatabase.GetAssetPath(r)); }
		foreach (var f in arquivos.Distinct())
		{
			var src = Path.Combine(Projeto, f);
			if (File.Exists(src)) File.Copy(src, Path.Combine(bk, f.Replace('/', '_')), true);
		}
		Log(sb, "backup em " + bk);
		// valores de antes (só grava se ainda não existe: Liga duas vezes não perde o original)
		if (!File.Exists(antes))
		{
			var l = new List<string>();
			var brg0 = Brg(out _);
			l.Add("brg|" + (brg0 != null ? brg0.intValue.ToString() : ""));
			foreach (var a in Assets())
			{
				l.Add($"urp|{AssetDatabase.GetAssetPath(a)}|{(int)a.gpuResidentDrawerMode}|{a.useSRPBatcher}");
				foreach (var r in Renderers(a)) l.Add($"rend|{AssetDatabase.GetAssetPath(r)}|{(int)r.renderingMode}");
			}
			File.WriteAllLines(antes, l, new UTF8Encoding(true));
		}
		foreach (var a in Assets())
		{
			// só liga onde o renderizador já é Forward+ (PC e PC Médio); Mobile e Web ficam como estão
			var rs = Renderers(a);
			if (rs.Count == 0 || rs.Any(r => r.renderingMode != RenderingMode.ForwardPlus))
			{
				Log(sb, $"{a.name}: pulado (renderizador não é Forward+)");
				continue;
			}
			if (!a.useSRPBatcher) { a.useSRPBatcher = true; Log(sb, $"{a.name}: SRP Batcher ligado"); }
			a.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing;
			EditorUtility.SetDirty(a);
			Log(sb, $"{a.name}: GPU Resident Drawer = InstancedDrawing");
		}
		var brg = Brg(out var so);
		var et = BrgEnum();
		if (brg != null && et != null)
		{
			brg.intValue = Convert.ToInt32(Enum.Parse(et, "KeepAll"));
			so.ApplyModifiedPropertiesWithoutUndo();
			Log(sb, "variantes BatchRendererGroup = KeepAll");
		}
		else Log(sb, "AVISO: não achei o campo das variantes BatchRendererGroup (" + (brg == null ? "m_BrgStripping" : "enum") + ")");
		AssetDatabase.SaveAssets();
		var t = Texto();
		File.WriteAllText(Path.Combine(Diag, "resident_drawer.txt"), DateTime.Now.ToString("HH:mm:ss") + " liga\n" + sb + "\n" + t, new UTF8Encoding(true));
		Debug.Log("[BURITIRO] resident drawer ligado:\n" + t);
	}

	public static void Desliga()
	{
		var sb = new StringBuilder();
		string antes = Path.Combine(Diag, "resident_drawer_antes.txt");
		if (!File.Exists(antes)) { Debug.LogError("[BURITIRO] resident drawer: sem " + antes); return; }
		foreach (var linha in File.ReadAllLines(antes))
		{
			var c = linha.Split('|');
			if (c[0] == "brg" && c.Length > 1 && c[1] != "")
			{
				var brg = Brg(out var so);
				if (brg != null) { brg.intValue = int.Parse(c[1]); so.ApplyModifiedPropertiesWithoutUndo(); Log(sb, "variantes BatchRendererGroup voltaram para " + c[1]); }
			}
			else if (c[0] == "urp" && c.Length > 3)
			{
				var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(c[1]);
				if (a) { a.gpuResidentDrawerMode = (GPUResidentDrawerMode)int.Parse(c[2]); a.useSRPBatcher = bool.Parse(c[3]); EditorUtility.SetDirty(a); Log(sb, $"{a.name}: voltou"); }
			}
			else if (c[0] == "rend" && c.Length > 2)
			{
				var r = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(c[1]);
				if (r) { r.renderingMode = (RenderingMode)int.Parse(c[2]); EditorUtility.SetDirty(r); Log(sb, $"{r.name}: voltou para {r.renderingMode}"); }
			}
		}
		AssetDatabase.SaveAssets();
		File.Delete(antes);
		var t = Texto();
		File.WriteAllText(Path.Combine(Diag, "resident_drawer.txt"), DateTime.Now.ToString("HH:mm:ss") + " desliga\n" + sb + "\n" + t, new UTF8Encoding(true));
		Debug.Log("[BURITIRO] resident drawer desligado:\n" + t);
	}
}
