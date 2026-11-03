// Assa o posicionamento dos GRAFITES de cada mapa (rodada 43, carregamento): o FBGraffiti.Place custava 5,6 s em toda partida (raios da
// navmesh + grade de 20 cm por parede). Roda o mesmo cálculo aqui, com a cena aberta no editor, e grava Resources/Graffiti/cache_<mapa>.txt
// (primeira linha = assinatura do mapa; o jogo só usa se bater). Rodar depois de reimportar o mapa (pipeline42: passo "grafites").
//   Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBGraffitiBake.Batch [-fbmap vila_jardim,praca_pt]
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBGraffitiBake
    {
        public static void Batch()
        {
            int code = 0;
            try
            {
                var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbmap");
                var maps = (i >= 0 && i + 1 < a.Length ? a[i + 1] : "vila_jardim,praca_pt,praca_aguas,orla").Split(',');
                GameData.Load(); Directory.CreateDirectory("Assets/FreeBala/Resources/Graffiti");
                foreach (var id in maps)
                {
                    var path = $"{FBImport.SceneDir}/Map_{id}.unity"; if (!File.Exists(path)) { Debug.LogWarning("[FreeBala][grafites] sem cena " + path); continue; }
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single); Physics.SyncTransforms();
                    // a navmesh do mapa só entra no jogo pelo OnEnable do FBNavData (não roda no editor): registra à mão, como o FBRoadScan
                    // (sem isso FBNav.Fixed() vinha vazio e o cálculo saía com 0 artes — r43g)
                    var nd = Object.FindFirstObjectByType<FBNavData>(); var nh = nd && nd.data ? UnityEngine.AI.NavMesh.AddNavMeshData(nd.data) : default; FBNav.Reset();
                    var sw = System.Diagnostics.Stopwatch.StartNew(); FBGraffiti.BakeToAssets = true;
                    int n; try { n = FBGraffiti.Place(id); } finally { FBGraffiti.BakeToAssets = false; FBGraffiti.Clear(); if (nh.valid) UnityEngine.AI.NavMesh.RemoveNavMeshData(nh); FBNav.Reset(); }
                    if (!nd) Debug.LogWarning($"[FreeBala][grafites] {id}: cena sem FBNavData (sem navmesh)");
                    Debug.Log($"[FreeBala][grafites] {id}: {n} artes assadas em {sw.ElapsedMilliseconds} ms");
                }
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala][grafites] " + e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
