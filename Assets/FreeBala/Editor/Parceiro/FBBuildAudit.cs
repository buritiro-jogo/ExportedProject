// Auditoria de tamanho do último build (09/11): lê Library/LastBuild.buildreport e grava em ..\..\_diag\build_tamanho.txt
// o peso por tipo, por pasta e os maiores assets. Serve também quando o build falha no fim (ex.: Gradle), porque o
// relatório do Unity é gravado antes. Em lote: Unity -batchmode -executeMethod FBBuildAudit.Batch
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class FBBuildAudit
{
	const string Tmp = "Assets/_FBBuildAuditTmp.buildreport";

	[MenuItem("FreeBala/Build/Auditar tamanho do último build")]
	public static void Rodar()
	{
		var src = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/LastBuild.buildreport"));
		var saida = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag/build_tamanho.txt"));
		if (!File.Exists(src)) { Debug.LogWarning("[FreeBala][auditoria] não há Library/LastBuild.buildreport"); return; }
		try
		{
			File.Copy(src, Tmp, true);
			AssetDatabase.ImportAsset(Tmp, ImportAssetOptions.ForceUpdate);
			var r = AssetDatabase.LoadAssetAtPath<BuildReport>(Tmp);
			if (r == null) { File.WriteAllText(saida, "não consegui abrir o relatório do build"); return; }
			var itens = r.packedAssets.SelectMany(p => p.contents).ToList();
			double MB(long b) => b / 1048576.0;
			long total = itens.Sum(c => (long)c.packedSize);
			var sb = new StringBuilder();
			sb.AppendLine($"build: {r.summary.platform} · {r.summary.result} · total do relatório {MB((long)r.summary.totalSize):0} MB · conteúdo empacotado {MB(total):0} MB · {itens.Count} itens");
			sb.AppendLine("\n== por tipo ==");
			foreach (var g in itens.GroupBy(c => c.type != null ? c.type.Name : "?").Select(g => (k: g.Key, s: g.Sum(c => (long)c.packedSize), n: g.Count())).OrderByDescending(x => x.s).Take(15))
				sb.AppendLine($"{MB(g.s),9:0.0} MB  {g.n,7} itens  {g.k}");
			foreach (int nivel in new[] { 2, 4 })
			{
				sb.AppendLine($"\n== por pasta (nível {nivel}) ==");
				foreach (var g in itens.GroupBy(c => Pasta(c.sourceAssetPath, nivel)).Select(g => (k: g.Key, s: g.Sum(c => (long)c.packedSize))).OrderByDescending(x => x.s).Take(nivel == 2 ? 20 : 40))
					sb.AppendLine($"{MB(g.s),9:0.0} MB  {g.k}");
			}
			sb.AppendLine("\n== malhas por pasta (nível 5) ==");
			foreach (var g in itens.Where(c => c.type == typeof(Mesh)).GroupBy(c => Pasta(c.sourceAssetPath, 5)).Select(g => (k: g.Key, s: g.Sum(c => (long)c.packedSize), n: g.Count())).OrderByDescending(x => x.s).Take(30))
				sb.AppendLine($"{MB(g.s),9:0.0} MB  {g.n,6} malhas  {g.k}");
			sb.AppendLine("\n== texturas por pasta (nível 5) ==");
			foreach (var g in itens.Where(c => c.type == typeof(Texture2D)).GroupBy(c => Pasta(c.sourceAssetPath, 5)).Select(g => (k: g.Key, s: g.Sum(c => (long)c.packedSize), n: g.Count())).OrderByDescending(x => x.s).Take(30))
				sb.AppendLine($"{MB(g.s),9:0.0} MB  {g.n,6} texturas  {g.k}");
			sb.AppendLine("\n== 60 maiores malhas ==");
			foreach (var g in itens.Where(c => c.type == typeof(Mesh)).OrderByDescending(c => (long)c.packedSize).Take(60))
			{
				Mesh m = null;
				foreach (var o in AssetDatabase.LoadAllAssetsAtPath(g.sourceAssetPath))
					if (o is Mesh mm && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out _, out long id) && id == g.id) { m = mm; break; }
				sb.AppendLine($"{MB((long)g.packedSize),9:0.0} MB  {(m ? $"{m.name} · {m.vertexCount} vértices · legível {(m.isReadable ? "SIM" : "não")} · índice {m.indexFormat}" : "?")}  {g.sourceAssetPath}");
			}
			sb.AppendLine("\n== 80 maiores assets ==");
			foreach (var g in itens.GroupBy(c => c.sourceAssetPath).Select(g => (k: g.Key, s: g.Sum(c => (long)c.packedSize), t: string.Join(",", g.Select(c => c.type != null ? c.type.Name : "?").Distinct().Take(3)))).OrderByDescending(x => x.s).Take(80))
				sb.AppendLine($"{MB(g.s),9:0.0} MB  {g.k}  [{g.t}]");
			sb.AppendLine("\n== texturas: as 60 maiores ==");
			foreach (var g in itens.Where(c => c.type == typeof(Texture2D)).GroupBy(c => c.sourceAssetPath).Select(g => (k: g.Key, s: g.Sum(c => (long)c.packedSize))).OrderByDescending(x => x.s).Take(60))
			{
				var t = AssetDatabase.LoadAssetAtPath<Texture2D>(g.k);
				sb.AppendLine($"{MB(g.s),9:0.0} MB  {(t ? $"{t.width}x{t.height} {t.format}" : "?"),-28} {g.k}");
			}
			File.WriteAllText(saida, sb.ToString());
			Debug.Log("[FreeBala][auditoria] gravado em " + saida);
		}
		finally { AssetDatabase.DeleteAsset(Tmp); }
	}

	static string Pasta(string p, int nivel)
	{
		if (string.IsNullOrEmpty(p)) return "(embutido)";
		var partes = p.Split('/');
		return string.Join("/", partes.Take(Mathf.Min(partes.Length - 1, nivel)));
	}

	public static void Batch() { try { Rodar(); EditorApplication.Exit(0); } catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); } }
}
