// Rodada 53 (03/11/2026): os passos do pipeline41/42 só da Vila Jardim, para rodar com o Unity ABERTO pelo laboratório do
// parceiro (_diag/lab_pedido.txt → "metodo FreeBala.EditorTools.FBR53Lab.Luzes"). Os *.Batch originais terminam com
// EditorApplication.Exit (fechariam o editor de quem está usando); aqui é a mesma lógica sem sair, e no fim a cena que
// estava aberta volta. Luzes/Lod3 salvam a cena da VJ; Ruas/Grafites só gravam arquivos (scan e Resources/Graffiti).
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBR53Lab
    {
        const string Id = "vila_jardim";
        const string LabOut = "F:/FreeBala_builds/WindowsLab53/FreeBala.exe";
        const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Static;
        static string Cena => $"{FBImport.SceneDir}/Map_{Id}.unity";

        static void NaCena(string tag, System.Func<UnityEngine.SceneManagement.Scene, string> f, bool salvar)
        {
            var antes = EditorSceneManager.GetActiveScene().path;
            try
            {
                var sc = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
                var rep = f(sc);
                if (salvar) { EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets(); }
                Debug.Log($"[FreeBala][{tag}] {Id}: {rep}");
            }
            finally { if (!string.IsNullOrEmpty(antes) && antes != Cena && File.Exists(antes)) EditorSceneManager.OpenScene(antes, OpenSceneMode.Single); }
        }

        public static void Luzes() => NaCena("lamp", FBLampPrep.Prep, true);

        public static void Ruas() => NaCena("ruas", sc => { FBRoadScan.Scan(Id); return "varredura em " + FBRoadScan.OutDir; }, false);

        public static void Lod3()
        {
            var t = typeof(FBLod3);
            t.GetMethod("Recover", Priv).Invoke(null, null); Directory.CreateDirectory("Assets/FreeBala/Optimized/Lod3");
            NaCena("lod3", sc => (string)t.GetMethod("Scene", Priv).Invoke(null, new object[] { sc }), true);
            Debug.Log("[FreeBala][lod3] veículos: " + (string)t.GetMethod("Veiculos", Priv).Invoke(null, null));
            AssetDatabase.SaveAssets();
            int F(string n) => (int)t.GetField(n, Priv).GetValue(null);
            Debug.Log($"[FreeBala][lod3] fim · malhas geradas {F("made")} · reaproveitadas {F("reused")} · puladas {F("skipped")} · falharam {F("failed")}");
        }

        public static void Grafites()
        {
            GameData.Load(); Directory.CreateDirectory("Assets/FreeBala/Resources/Graffiti");
            NaCena("grafites", sc =>
            {
                Physics.SyncTransforms();
                // igual ao FBGraffitiBake.Batch: a navmesh do mapa só entra pelo OnEnable do FBNavData (não roda no editor)
                var nd = Object.FindFirstObjectByType<FBNavData>(); var nh = nd && nd.data ? UnityEngine.AI.NavMesh.AddNavMeshData(nd.data) : default; FBNav.Reset();
                var sw = System.Diagnostics.Stopwatch.StartNew(); FBGraffiti.BakeToAssets = true;
                int n; try { n = FBGraffiti.Place(Id); } finally { FBGraffiti.BakeToAssets = false; FBGraffiti.Clear(); if (nh.valid) UnityEngine.AI.NavMesh.RemoveNavMeshData(nh); FBNav.Reset(); }
                return $"{n} artes assadas em {sw.ElapsedMilliseconds} ms" + (nd ? "" : " (cena sem FBNavData)");
            }, false);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        // = FBBuild.WindowsLabBatch (Development em Mono) com a saída da r53, sem fechar o editor
        public static void BuildLab()
        {
            var t = typeof(FBBuild); var mono = t.GetField("mono", Priv); bool ok;
            FBProneClips.Ensure();
            try
            {
                mono.SetValue(null, true);
                ok = (bool)t.GetMethod("Run", Priv).Invoke(null, new object[] { BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, LabOut, BuildOptions.Development });
            }
            finally { mono.SetValue(null, false); PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP); }
            Debug.Log($"[FreeBala][build] r53 lab: {(ok ? "Build Finished" : "FALHOU")} → {LabOut}");
        }
    }
}
