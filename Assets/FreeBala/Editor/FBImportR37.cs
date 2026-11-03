// Importação só da rodada 37 (21/10/2026), sem refazer mapas/personagens: animações e controllers (Complexo como base, clipes de sistema/
// emote/NPC a 20 qps, reanimar do Sigma), helicóptero RR (assets/vehicles/ie/heli.glb) e o registro. Os controllers mantêm o GUID
// (prefabs continuam apontando para eles). Luminárias apagáveis: FBLampPrep (abre as cenas dos mapas).
// Antes: node tools/unity/export_data.mjs. Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportR37.Batch
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR37
    {
        public static void Batch()
        {
            int code = 0;
            try { Run(); } catch (System.Exception e) { Debug.LogError("[FreeBala][r37] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/Importar rodada 37 (animações, heli)", priority = 7)]
        public static void Run()
        {
            var src = FBImport.Src; var sw = System.Diagnostics.Stopwatch.StartNew();
            File.Copy(Path.Combine(Application.dataPath, "StreamingAssets/fb_data.json"), "Assets/FreeBala/Resources/fb_data.json", true);
            AssetDatabase.ImportAsset("Assets/FreeBala/Resources/fb_data.json", ImportAssetOptions.ForceSynchronousImport); GameData.Load();
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            // ---- helicóptero RR (substitui o Bell UH-1N do IE; mesmo id "heli" e mesmos nós Helices/Helices-02)
            var heli = Path.Combine(src, "vehicles", "ie", "heli.glb");
            if (File.Exists(heli)) { FBImport.Copy(heli, $"{FBImport.Imp}/Vehicles/heli.glb"); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); AssetDatabase.ImportAsset($"{FBImport.Imp}/Vehicles/heli.gltf", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport); }
            var hg = AssetDatabase.LoadAssetAtPath<GameObject>($"{FBImport.Imp}/Vehicles/heli.gltf");
            if (hg) { var nodes = hg.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList(); var b = new Bounds(); bool f = true; foreach (var r in hg.GetComponentsInChildren<Renderer>(true)) { if (f) { b = r.bounds; f = false; } else b.Encapsulate(r.bounds); } Debug.Log($"[FreeBala][r37] heli: nós {string.Join(",", nodes)} · caixa {b.size}"); }
            else Debug.LogError("[FreeBala][r37] heli não importou");
            // ---- clipes + controllers
            var clips = FBAnimImport.ImportClips(Path.Combine(src, "anims.json"));
            var ctrls = Json.Parse(File.ReadAllText(Path.Combine(src, "controllers.json"))) as Dictionary<string, object>;
            AnimatorController main = null, ped = null;
            foreach (var kv in ctrls)
            {
                var c = FBAnimImport.ImportController(kv.Key, kv.Value as Dictionary<string, object>, clips);
                if (kv.Key.StartsWith("EnemyAnimator_AAA")) main = c; else if (kv.Key == "PedestrianAnimator") ped = c;
            }
            AssetDatabase.SaveAssets();
            // ---- registro
            var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
            reg.clips = Directory.GetFiles(FBAnimImport.ClipDir, "*.anim").Select(p => AssetDatabase.LoadAssetAtPath<AnimationClip>(p.Replace('\\', '/'))).Where(c => c).ToList();
            if (main) reg.mainController = main; if (ped) reg.pedestrianController = ped;
            reg.vehicleIds = Directory.GetFiles($"{FBImport.Imp}/Vehicles", "*.gltf").Where(f => AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
            EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
            // conferência: prefabs de personagem com o controller certo
            int ok = 0, fix = 0;
            foreach (var p in Directory.GetFiles(FBCharImport.PrefabDir, "*.prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/')); var an = go ? go.GetComponent<Animator>() : null; if (!an) continue;
                if (an.runtimeAnimatorController == main || an.runtimeAnimatorController == ped) { ok++; continue; }
                if (!an.runtimeAnimatorController && main) { an.runtimeAnimatorController = main; EditorUtility.SetDirty(go); PrefabUtility.SavePrefabAsset(go); fix++; }
            }
            AssetDatabase.SaveAssets();
            string[] need = { FBAnimProfile.ReviveStart, FBAnimProfile.ReviveLoop, FBAnimProfile.ReviveEnd, "HumanoidIdle", "Standard Walk", "Running", "HumanoidRun", "IE_Unarmed_Crouch_Idle" };
            Debug.Log($"[FreeBala][r37] import em {sw.Elapsed.TotalSeconds:0} s · {reg.clips.Count} clipes · controllers {ctrls.Count} · prefabs ok {ok}, corrigidos {fix} · faltando: {string.Join(",", need.Where(n => !reg.clips.Any(c => c && c.name == n)))}");
        }
    }
}
