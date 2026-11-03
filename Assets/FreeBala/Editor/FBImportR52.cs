// Importação da rodada 52 (01/11/2026) — elenco por categoria, sem os NPCs do 171. Sem reimportar malha nenhuma:
//  • fb_data.json novo (skins com "cats"/"func", sem x171_*; tools/unity/export_data.mjs) -> Resources/fb_data.json
//  • registro de personagens reconstruído da pasta de prefabs (os 93 x171_* — prefab, glTF, texturas, avatar — foram movidos para
//    unity/retirados_r52/x171_chars; nenhum GUID deles era usado fora dos próprios arquivos). Clipes X171_*, objetos Resources/X171 e
//    Audio/171 ficam: são animações/objetos/vozes usados pelos NOSSOS personagens e eventos, não NPCs.
//  • auditoria do elenco (FBElenco.Audit) no log
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR52.ElencoBatch (antes: node tools/unity/export_data.mjs)
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR52
    {
        public static void ElencoBatch()
        {
            int code = 0;
            try { Elenco(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r52] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar rodada 52 (elenco por categoria)", priority = 10)]
        public static void Elenco()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            File.Copy(Path.Combine(Application.dataPath, "StreamingAssets/fb_data.json"), "Assets/FreeBala/Resources/fb_data.json", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            var antes = reg.charIds.ToList();
            reg.charIds = Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Where(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            var sairam = antes.Except(reg.charIds).ToList(); var x171 = reg.charIds.Count(i => i.StartsWith("x171_"));
            GameData.Load(); var bad = FBElenco.Audit();
            Debug.Log($"[FreeBala][r52] registro {antes.Count} -> {reg.charIds.Count} personagens (saíram {sairam.Count}, {sairam.Count(i => i.StartsWith("x171_"))} do 171; x171 restantes {x171})" +
                      $" · skins {GameData.Skins.Count} · {FBElenco.Summary()} · problemas {bad.Count}{(bad.Count > 0 ? ": " + string.Join(" | ", bad) : "")} · {sw.Elapsed.TotalSeconds:0} s");
            if (x171 > 0 || bad.Count > 0) throw new System.Exception("elenco inconsistente (ver acima)");
        }
    }
}
