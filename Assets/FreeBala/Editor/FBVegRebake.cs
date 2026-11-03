// Refaz a vegetação (instâncias + tapete de grama) de um mapa já importado e otimizado, sem reimportar o glTF:
//   Unity -batchmode -executeMethod FreeBala.EditorTools.FBMapLook.VegRebakeBatch -fbmap praca_aguas
// A cena otimizada usa cópias Mesh LOD nos MeshFilters (o MeshCollider ficou com a malha original): durante o bake o filtro volta para a
// malha do colisor (o índice de triângulo do raio bate com os submeshes certos) e depois é restaurado.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static partial class FBMapLook
    {
        public static void VegRebakeBatch()
        {
            var args = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(args, "-fbmap"); string id = i >= 0 && i + 1 < args.Length ? args[i + 1] : "praca_aguas";
            int code = 0;
            try
            {
                var sc = EditorSceneManager.OpenScene($"{FBImport.SceneDir}/Map_{id}.unity");
                var map = sc.GetRootGameObjects().FirstOrDefault(g => g.name == "Map_" + id) ?? sc.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Map_"));
                if (!map) throw new System.Exception("raiz Map_ não achada");
                foreach (var g in sc.GetRootGameObjects()) if (g.name == "Vegetacao") Object.DestroyImmediate(g);
                var swap = new List<(MeshFilter mf, Mesh m)>();
                foreach (var mf in map.GetComponentsInChildren<MeshFilter>(true)) { var mc = mf.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh && mf.sharedMesh != mc.sharedMesh) { swap.Add((mf, mf.sharedMesh)); mf.sharedMesh = mc.sharedMesh; } }
                var mats = map.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToList();
                try { VegBake(map, id, mats); } finally { foreach (var (mf, m) in swap) if (mf) mf.sharedMesh = m; }
                EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets();
                Debug.Log($"[FreeBala] vegetação refeita: {id} ({swap.Count} malhas LOD trocadas durante o bake)");
            }
            catch (System.Exception e) { Debug.LogError("[FreeBala] VegRebake falhou: " + e); code = 1; }
            EditorApplication.Exit(code);
        }
    }
}
