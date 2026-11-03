// Rodada 39 (26/10/2026): Boeing 787 do Battle Royale (assets/br/boeing_787.glb do web) entra como acessório (Resources/FBImported/Acc)
// — o BR e o avião que passa no céu (FBSkyPlanes) usavam peças primitivas provisórias. Fica em Acc para não entrar nas listas de veículos.
// Uso: Unity -batchmode -executeMethod FreeBala.EditorTools.FBImportR39.Batch
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportR39
    {
        public static void Batch()
        {
            int code = 0;
            try
            {
                // tools/unity/fix_boeing.py: transformações aplicadas, nariz +Z, 68 m
                var src = Path.Combine(FBImport.Src, "br", "boeing_787_unity.glb"); var dst = $"{FBImport.Imp}/Acc/boeing787.glb";
                if (!File.Exists(src)) { Debug.LogError("[FreeBala][r39] falta " + src); code = 1; return; }
                FBImport.Copy(src, dst); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(FBImport.G(dst));
                if (!go) { Debug.LogError("[FreeBala][r39] GLB não importou: " + FBImport.G(dst)); code = 1; return; }
                var reg = AssetDatabase.LoadAssetAtPath<FBRegistry>("Assets/FreeBala/Resources/FBRegistry.asset");
                reg.accIds = Directory.GetFiles($"{FBImport.Imp}/Acc", "*.gltf").Where(f => AssetDatabase.LoadAssetAtPath<GameObject>(f.Replace('\\', '/'))).Select(Path.GetFileNameWithoutExtension).ToList();
                EditorUtility.SetDirty(reg); AssetDatabase.SaveAssets();
                var b = new Bounds(); bool any = false; foreach (var r in go.GetComponentsInChildren<MeshFilter>(true)) if (r.sharedMesh) { var bb = r.sharedMesh.bounds; if (!any) { b = bb; any = true; } else b.Encapsulate(bb); }
                Debug.Log($"[FreeBala][r39] boeing787 importado · acessórios {string.Join(",", reg.accIds)} · malha {b.size}");
            }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            finally { EditorApplication.Exit(code); }
        }
    }
}
