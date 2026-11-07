// Importação da rodada 56 (05/11/2026) — SÓ os personagens que voltaram do Mixamo entram no projeto.
// Nada de mapa, arma, veículo ou clipe é tocado: a Vila Jardim r54z já importada (postes, luzes, árvores, ruas, LOD3,
// grafites) seria refeita do zero por um "Importar tudo", e é justamente o que não pode acontecer aqui.
// O que faz: copia fb_data.json -> Resources, copia os .glb de assets/chars dos ids pedidos, monta avatar + prefab
// (FBCharImport), refaz a lista charIds do FBRegistry e roda a auditoria do elenco (FBElenco.Audit).
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR56.CharsBatch -fbchars r_may,r_narc,...
//       (sem -fbchars: todos os ids do skins.json que ainda não têm prefab)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR56
    {
        public static void CharsBatch()
        {
            int code = 0;
            try { Chars(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r56] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        static List<string> Pedidos()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbchars");
            if (i >= 0 && i + 1 < a.Length) return a[i + 1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            GameData.Load();
            return GameData.Skins.Select(s => s.id).Where(id => !File.Exists($"{FBCharImport.PrefabDir}/{id}.prefab")).ToList();
        }

        [MenuItem("FreeBala/Importar rodada 56 (personagens do Mixamo)", priority = 11)]
        public static void Chars()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew(); var src = FBImport.Src; var Imp = FBImport.Imp;
            File.Copy(Path.Combine(Application.dataPath, "StreamingAssets/fb_data.json"), "Assets/FreeBala/Resources/fb_data.json", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;      // glTFast 6.14: corrida no SortAndNormalizeBoneWeightsJob

            var ids = Pedidos(); var falta = new List<string>();
            foreach (var id in ids.ToList())
            {
                var glb = Path.Combine(src, "chars", id + ".glb");
                if (!File.Exists(glb) || !File.Exists(Path.ChangeExtension(glb, ".avatar.json"))) { falta.Add(id); ids.Remove(id); continue; }
                FBImport.Copy(glb, $"{Imp}/Chars/{id}.glb");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            int ok = 0; var ruim = new List<string>();
            foreach (var id in ids)
            {
                var g = $"{Imp}/Chars/{id}.gltf";
                AssetDatabase.ImportAsset(g, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var go = FBCharImport.Import(id, g, Path.Combine(src, "chars", id + ".avatar.json"), Path.Combine(src, "chars", id + ".sheet.json"), reg.mainController);
                if (go) ok++; else ruim.Add(id);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            reg.charIds = Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Where(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();

            GameData.Load(); var bad = FBElenco.Audit();
            Debug.Log($"[FreeBala][r56] personagens {ok}/{ids.Count}{(ruim.Count > 0 ? " · falharam: " + string.Join(",", ruim) : "")}" +
                      $"{(falta.Count > 0 ? " · sem GLB: " + string.Join(",", falta) : "")} · registro {reg.charIds.Count} · skins {GameData.Skins.Count}" +
                      $" · {FBElenco.Summary()} · problemas {bad.Count}{(bad.Count > 0 ? ": " + string.Join(" | ", bad) : "")} · {sw.Elapsed.TotalSeconds:0} s");
            if (ruim.Count > 0 || bad.Count > 0) throw new System.Exception("importação incompleta (ver acima)");
        }
    }
}
