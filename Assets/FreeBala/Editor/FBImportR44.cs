// Importação da rodada 44 (28/10/2026) — motos do MX Grau. Sem refazer mapas/personagens:
//  • assets/vehicles/mx/<id>.glb -> Resources/FBImported/Vehicles/mx_<id>.gltf (hierarquia original: chassi, guidão, suspensões, rodas e os
//    nós vazios de física) — tools/mxgrau/export_bikes.py
//  • assets/vehicles/mx.json -> Resources/FBMx/mx_bikes.json (física/som/poses do piloto por moto)
//  • assets/audio/mx_<id>_<papel>.ogg (66 sons, 6 por moto) -> Resources/Audio/sfx — tools/mxgrau/export_audio.py
//  • assets/anims_mx.json (clipes humanoides do PlayerMX) -> MX_<nome> — tools/mxgrau/export_anims.py
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR44.MotosBatch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR44
    {
        public static void MotosBatch()
        {
            int code = 0;
            try { Motos(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r44] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar rodada 44 (motos MX Grau)", priority = 8)]
        public static void Motos()
        {
            var src = FBImport.Src; var sw = System.Diagnostics.Stopwatch.StartNew();
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            // ---- dados
            Directory.CreateDirectory("Assets/FreeBala/Resources/FBMx");
            File.Copy(Path.Combine(src, "vehicles", "mx.json"), "Assets/FreeBala/Resources/FBMx/mx_bikes.json", true);
            // ---- sons
            int ns = 0; foreach (var f in Directory.GetFiles(Path.Combine(src, "audio"), "mx_*.ogg")) { File.Copy(f, "Assets/FreeBala/Resources/Audio/sfx/" + Path.GetFileName(f), true); ns++; }
            // ---- modelos
            var ids = Directory.GetFiles(Path.Combine(src, "vehicles", "mx"), "*.glb").Select(Path.GetFileNameWithoutExtension).ToList();
            foreach (var id in ids) FBImport.Copy(Path.Combine(src, "vehicles", "mx", id + ".glb"), $"{FBImport.Imp}/Vehicles/mx_{id}.glb");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var id in ids) AssetDatabase.ImportAsset($"{FBImport.Imp}/Vehicles/mx_{id}.gltf", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (var id in ids)
            {
                var g = AssetDatabase.LoadAssetAtPath<GameObject>($"{FBImport.Imp}/Vehicles/mx_{id}.gltf");
                if (!g) { Debug.LogError($"[FreeBala][r44] mx_{id} não importou"); continue; }
                var need = new[] { "FPhysicsWheel", "RPhysicsWheel", "FrontSuspension", "RearSuspension", "gripL", "gripR" };
                var names = g.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToHashSet();
                int tris = g.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh).Sum(m => m.sharedMesh.triangles.Length / 3);
                Debug.Log($"[FreeBala][r44] mx_{id}: {names.Count} nós · {tris} triângulos · faltando: {string.Join(",", need.Where(n => !names.Contains(n)))}");
            }
            // ---- clipes do piloto (humanoides)
            var fa = Path.Combine(src, "anims_mx.json"); int nc = 0;
            if (File.Exists(fa)) foreach (var e in Json.L(Json.Parse(File.ReadAllText(fa)), "clips") ?? new System.Collections.Generic.List<object>())
                {
                    var name = Json.S(e, "name"); var d = Json.Floats(Json.L(e, "d")); if (d.Length < 102) continue;
                    FBAnimImport.MakeClip("MX_" + name, Json.F(e, "fps", 30), d, .001f, Json.B(e, "loop"), true); nc++;
                }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            // ---- registro
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            reg.vehicleIds = Directory.GetFiles($"{FBImport.Imp}/Vehicles", "*.gltf").Where(f => AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
            reg.clips = Directory.GetFiles(FBAnimImport.ClipDir, "*.anim").Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace('\\', '/'))).Where(c => c).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala][r44] motos MX: {ids.Count} modelos · {ns} sons · {nc} clipes MX_* · registro {reg.vehicleIds.Count} veículos / {reg.clips.Count} clipes · {sw.Elapsed.TotalSeconds:0} s");
        }

        // ---- personagens do 171 (malha + esqueleto completo do manequim ALS convertidos por tools/x171/chars171.py): x171_*.glb + .avatar.json
        // Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR44.CharsBatch (antes: node tools/unity/export_data.mjs)
        public static void CharsBatch()
        {
            int code = 0;
            try { Chars(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r44] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar rodada 44 (personagens do 171)", priority = 9)]
        public static void Chars()
        {
            var src = FBImport.Src; var sw = System.Diagnostics.Stopwatch.StartNew(); var Imp = FBImport.Imp;
            File.Copy(Path.Combine(Application.dataPath, "StreamingAssets/fb_data.json"), "Assets/FreeBala/Resources/fb_data.json", true);
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            var rs = Directory.GetFiles(Path.Combine(src, "chars"), "x171_*.glb").Where(f => File.Exists(Path.ChangeExtension(f, ".avatar.json"))).ToList();
            foreach (var f in rs) FBImport.Copy(f, $"{Imp}/Chars/{Path.GetFileName(f)}");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset"); int nc = 0; var bad = new System.Collections.Generic.List<string>();
            foreach (var f in rs)
            {
                var id = Path.GetFileNameWithoutExtension(f); var g = $"{Imp}/Chars/{id}.gltf";
                AssetDatabase.ImportAsset(g, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var go = FBCharImport.Import(id, g, Path.ChangeExtension(f, ".avatar.json"), Path.Combine(src, "chars", id + ".sheet.json"), reg.mainController);
                if (go) nc++; else bad.Add(id);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            reg.charIds = Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab").Where(x => AssetDatabase.LoadAssetAtPath<GameObject>(x.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala][r44] personagens do 171: {nc}/{rs.Count} · falharam: {string.Join(",", bad)} · registro {reg.charIds.Count} · {sw.Elapsed.TotalSeconds:0} s");
        }
    }
}
