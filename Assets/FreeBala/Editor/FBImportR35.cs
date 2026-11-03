// Importação só da rodada 35 (20/10/2026), sem refazer o import inteiro:
//  · peças da oficina do REB por carro (assets/vehicles/parts/*.glb -> FBImported/Parts), atlas das bandeiras, ícones da oficina, os 37 roncos;
//  · pacote NOVO de NPCs (assets/chars/n2_*) + clipes NPC2_* (assets/npc2_anims.json); os NPCs antigos (n_* e pedestres p_*) saem do projeto
//    (os arquivos do jogo web foram para assets/chars_retirados — um import completo não os traz de volta).
// Antes: node tools/unity/export_data.mjs. Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR35.Batch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR35
    {
        const string Imp = FBImport.Imp;
        public static void Batch()
        {
            int code = 0;
            try { Run(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r35] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar rodada 35 (oficina REB + NPCs novos)", priority = 5)]
        public static void Run()
        {
            var src = FBImport.Src;
            File.Copy(Path.Combine(Application.dataPath, "StreamingAssets/fb_data.json"), "Assets/FreeBala/Resources/fb_data.json", true);
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            // ---- veículos
            var parts = Path.Combine(src, "vehicles", "parts"); int np = 0;
            if (Directory.Exists(parts)) foreach (var f in Directory.GetFiles(parts, "*.glb")) { FBImport.Copy(f, $"{Imp}/Parts/{Path.GetFileName(f)}"); np++; }
            foreach (var f in Directory.GetFiles(Path.Combine(src, "vehicles", "fx"), "*.png")) FBImport.Copy(f, $"Assets/FreeBala/Resources/CarFX/{Path.GetFileName(f)}");
            var ofi = Path.Combine(src, "oficina", "icons"); int ni = 0;
            if (Directory.Exists(ofi)) foreach (var f in Directory.GetFiles(ofi, "*.png")) if (Path.GetFileName(f).StartsWith("Oficina_") || Path.GetFileName(f).StartsWith("Editor_Som_")) { FBImport.Copy(f, $"Assets/FreeBala/Resources/UI/Oficina/{Path.GetFileName(f)}"); ni++; }
            int na = 0; foreach (var f in Directory.GetFiles(Path.Combine(src, "audio"), "reb_*.ogg")) { FBImport.Copy(f, $"Assets/FreeBala/Resources/Audio/sfx/{Path.GetFileName(f)}"); na++; }
            // ---- NPCs: tira os antigos, traz os novos
            int nd = 0;
            foreach (var pre in new[] { "n_", "p_" })
            {
                foreach (var f in Directory.GetFiles(FBCharImport.PrefabDir, pre + "*.prefab")) if (AssetDatabase.DeleteAsset(f.Replace('\\', '/'))) nd++;
                foreach (var f in Directory.GetFiles(FBCharImport.AvatarDir, pre + "*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
                foreach (var f in Directory.GetFiles($"{Imp}/Chars", pre + "*.gltf")) { var b = Path.ChangeExtension(f, null); AssetDatabase.DeleteAsset(f.Replace('\\', '/')); AssetDatabase.DeleteAsset((b + ".bin").Replace('\\', '/')); AssetDatabase.DeleteAsset((b + FBGlbUnpack.TexSuffix).Replace('\\', '/')); }
            }
            var n2 = Directory.GetFiles(Path.Combine(src, "chars"), "n2_*.glb").Where(f => File.Exists(Path.ChangeExtension(f, ".avatar.json"))).ToList();
            foreach (var f in n2) FBImport.Copy(f, $"{Imp}/Chars/{Path.GetFileName(f)}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            int nc = 0;
            foreach (var f in n2)
            {
                var id = Path.GetFileNameWithoutExtension(f); var g = $"{Imp}/Chars/{id}.gltf";
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(g)) AssetDatabase.ImportAsset(g, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (FBCharImport.Import(id, g, Path.ChangeExtension(f, ".avatar.json"), Path.Combine(src, "chars", id + ".sheet.json"), reg.mainController)) nc++;
            }
            // ---- clipes NPC2_*
            int nk = 0; var cj = Path.Combine(src, "npc2_anims.json");
            if (File.Exists(cj))
                foreach (var e in Json.L(Json.Parse(File.ReadAllText(cj)), "clips"))
                {
                    var d = Json.Floats(Json.L(e, "d")); if (d.Length < 102) continue;
                    FBAnimImport.MakeClip(Json.S(e, "id"), Json.F(e, "fps", 30), d, .001f, Json.Get(e, "loop") is bool lb ? lb : true); nk++;
                }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // ---- registro
            System.Collections.Generic.List<string> Ids(string dir, string pat) => Directory.Exists(dir) ? Directory.GetFiles(dir, pat).Where(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList() : new System.Collections.Generic.List<string>();
            reg.charIds = Ids(FBCharImport.PrefabDir, "*.prefab"); reg.partIds = Ids($"{Imp}/Parts", "*.gltf");
            reg.clips = Directory.GetFiles(FBAnimImport.ClipDir, "*.anim").Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace('\\', '/'))).Where(c => c).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala][r35] peças {np} ({reg.partIds.Count} no registro), ícones {ni}, sons {na}; NPCs antigos removidos {nd}, novos {nc}/{n2.Count}; clipes NPC2 {nk}; registro {reg.charIds.Count} personagens, {reg.clips.Count} clipes");
        }
    }
}
