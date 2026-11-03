// Rodada 47 — passos de editor (rodar com -batchmode -executeMethod FreeBala.EditorTools.FBR47.Prep antes do build):
//  1. cena Map_vila_jardim: nó VJ11_Becos (primitivas artificiais) inativo + EditorOnly → não vai para o build (FBMapRetire);
//  2. emotes do Free Fire: os 199 clipes FF_* que não são de sistema (FF_SYS_*) nem do REB (FF_REB_*) saem de Anim/Clips para
//     Anim/Retirados_FF (GUID mantido) e do registro (FBRegistry.clips) → não entram no build nem voltam numa reimportação.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBR47
    {
        const string Retired = "Assets/FreeBala/Anim/Retirados_FF";
        public static void Prep()
        {
            int code = 0;
            try { RetireMapNodes(); PruneFFEmotes(); }
            catch (System.Exception e) { Debug.LogError("[FreeBala][r47] falhou: " + e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
        [MenuItem("FreeBala/r47/Retirar objetos artificiais")]
        public static void RetireMapNodes()
        {
            foreach (var path in Directory.GetFiles("Assets/FreeBala/Scenes", "Map_*.unity"))
            {
                var sc = EditorSceneManager.OpenScene(path.Replace('\\', '/'), OpenSceneMode.Single); int n = 0;
                foreach (var root in sc.GetRootGameObjects())
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        if (System.Array.IndexOf(FBMapRetire.NODES, t.name) >= 0) { t.gameObject.SetActive(false); t.gameObject.tag = "EditorOnly"; n++; }
                if (n > 0) { EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); }
                Debug.Log($"[FreeBala][r47] {Path.GetFileName(path)}: {n} nó(s) retirado(s)");
            }
        }
        [MenuItem("FreeBala/r47/Tirar emotes do Free Fire")]
        public static void PruneFFEmotes()
        {
            if (!AssetDatabase.IsValidFolder(Retired)) AssetDatabase.CreateFolder("Assets/FreeBala/Anim", "Retirados_FF");
            int moved = 0;
            foreach (var f in Directory.GetFiles(FBAnimImport.ClipDir, "*.anim"))
            {
                var name = Path.GetFileNameWithoutExtension(f); if (!FBEmotes.IsFF(name)) continue;
                var err = AssetDatabase.MoveAsset(f.Replace('\\', '/'), $"{Retired}/{Path.GetFileName(f)}"); if (string.IsNullOrEmpty(err)) moved++; else Debug.LogWarning($"[FreeBala][r47] {name}: {err}");
            }
            int before = 0, after = 0;
            foreach (var g in AssetDatabase.FindAssets("t:FBRegistry"))
            {
                var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>(AssetDatabase.GUIDToAssetPath(g)); if (!reg) continue; before += reg.clips.Count;
                reg.clips = reg.clips.Where(c => c && !FBEmotes.IsFF(c.name) && AssetDatabase.GetAssetPath(c).StartsWith(FBAnimImport.ClipDir)).ToList(); after += reg.clips.Count; EditorUtility.SetDirty(reg);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala][r47] emotes FF: {moved} clipes movidos para {Retired}; registro {before} -> {after} clipes");
        }
    }
}
