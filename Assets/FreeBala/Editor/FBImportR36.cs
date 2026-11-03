// Importação só da rodada 36 (21/10/2026), sem refazer o import inteiro: troca TODOS os jogáveis pelos personagens regionais r_*
// (assets/chars/r_*, auto-rig de tools/personagens_novos). Os jogáveis antigos (c_* dos APKs/retex e ie_* do Invasão Elite) saem do projeto
// (os arquivos do jogo web foram para assets/chars_retirados — um import completo não os traz de volta). NPCs do rolê (n2_*) ficam.
// Antes: node tools/unity/export_data.mjs. Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR36.Batch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR36
    {
        const string Imp = FBImport.Imp;
        public static void Batch()
        {
            int code = 0;
            try { Run(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r36] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar rodada 36 (personagens regionais)", priority = 6)]
        public static void Run()
        {
            var src = FBImport.Src;
            File.Copy(Path.Combine(Application.dataPath, "StreamingAssets/fb_data.json"), "Assets/FreeBala/Resources/fb_data.json", true);
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            // ---- jogáveis antigos saem
            int nd = 0;
            foreach (var pre in new[] { "c_", "ie_" })
            {
                foreach (var f in Directory.GetFiles(FBCharImport.PrefabDir, pre + "*.prefab")) if (AssetDatabase.DeleteAsset(f.Replace('\\', '/'))) nd++;
                foreach (var f in Directory.GetFiles(FBCharImport.AvatarDir, pre + "*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
                foreach (var f in Directory.GetFiles($"{Imp}/Chars", pre + "*.gltf")) { var b = Path.ChangeExtension(f, null); AssetDatabase.DeleteAsset(f.Replace('\\', '/')); AssetDatabase.DeleteAsset((b + ".bin").Replace('\\', '/')); AssetDatabase.DeleteAsset((b + FBGlbUnpack.TexSuffix).Replace('\\', '/')); }
            }
            // ---- regionais entram
            var rs = Directory.GetFiles(Path.Combine(src, "chars"), "r_*.glb").Where(f => File.Exists(Path.ChangeExtension(f, ".avatar.json"))).ToList();
            // regional que saiu do jogo (TOROZIN, 21/10): o prefab antigo sai junto, senão continua na tela de personagem pelo registro
            var keepR = new System.Collections.Generic.HashSet<string>(rs.Select(Path.GetFileNameWithoutExtension));
            foreach (var f in Directory.GetFiles(FBCharImport.PrefabDir, "r_*.prefab"))
            {
                var id = Path.GetFileNameWithoutExtension(f); if (keepR.Contains(id)) continue;
                AssetDatabase.DeleteAsset(f.Replace('\\', '/')); AssetDatabase.DeleteAsset($"{FBCharImport.AvatarDir}/{id}.asset");
                var b = $"{Imp}/Chars/{id}"; AssetDatabase.DeleteAsset(b + ".gltf"); AssetDatabase.DeleteAsset(b + ".bin"); AssetDatabase.DeleteAsset(b + FBGlbUnpack.TexSuffix); nd++;
            }
            foreach (var f in rs) FBImport.Copy(f, $"{Imp}/Chars/{Path.GetFileName(f)}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            int nc = 0;
            foreach (var f in rs)
            {
                var id = Path.GetFileNameWithoutExtension(f); var g = $"{Imp}/Chars/{id}.gltf";
                AssetDatabase.ImportAsset(g, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (FBCharImport.Import(id, g, Path.ChangeExtension(f, ".avatar.json"), Path.Combine(src, "chars", id + ".sheet.json"), reg.mainController)) nc++;
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // ---- registro
            reg.charIds = Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Where(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala][r36] jogáveis antigos removidos {nd}; regionais {nc}/{rs.Count}; registro {reg.charIds.Count} personagens: {string.Join(",", reg.charIds)}");
        }
    }
}
