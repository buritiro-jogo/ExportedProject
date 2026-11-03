// Reimporta UM mapa do jogo web (GLB -> glTF + texturas) e refaz só a cena dele (colisão, NavMesh, luz, spawns), sem mexer em
// personagens, armas nem nos outros mapas. Opcional: prints da cena pelo editor (com gráficos) em assets/maps/<id>/renders/unity.
// Lote: Unity -batchmode -projectPath FreeBala -executeMethod FreeBala.EditorTools.FBImportMap.Batch -fbmap praca_pt
//       [-fbviews "nome:ex,ey,eh,tx,ty,th;..."]   (metros reais do mapa web: x leste, y norte, h altura; escala 1,2 como no build)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBImportMap
    {
        static string Arg(string k) { var a = Environment.GetCommandLineArgs(); int i = Array.IndexOf(a, k); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }

        public static void Batch()
        {
            int code = 0; var id = Arg("-fbmap");
            try { Run(id); if (Arg("-fbviews") != null) Shots(id, Arg("-fbviews")); }
            catch (Exception e) { Debug.LogError("[FreeBala] importar mapa " + id + ": " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Run(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("faltou -fbmap <id>");
            var src = Path.Combine(FBImport.Src, "maps", id, "map.glb"); if (!File.Exists(src)) throw new FileNotFoundException(src);
            var dst = $"{FBImport.Imp}/Maps/{id}.glb"; var tex = $"{FBImport.Imp}/Maps/{id}_tex";
            if (AssetDatabase.IsValidFolder(tex)) AssetDatabase.DeleteAsset(tex);                 // texturas antigas (os nomes são por índice)
            // o desempacotador pula quando o .glb não mudou (carimbo em Library/FBUnpack) — com a pasta de texturas apagada o glTF ficava
            // sem imagens e a importação falhava; força desempacotar de novo
            var stamp = Path.Combine("Library/FBUnpack", FBImport.G(dst).Replace('/', '_').Replace(Path.DirectorySeparatorChar, '_') + ".src"); if (File.Exists(stamp)) File.Delete(stamp);
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobDebuggerEnabled = false;
            typeof(FBImport).GetMethod("Copy", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { src, dst });
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(FBImport.G(dst), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(FBImport.G(dst))) throw new Exception("GLB não importou: " + FBImport.G(dst));
            FBImport.OnlyMap = id; var ssr = FBProjectSetup.SuspendSSR();                         // câmeras do editor (sonda/ícones) sem normais: SSR do URP 17.6 lança NullReference
            try { typeof(FBImport).GetMethod("Maps", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); }
            finally { FBImport.OnlyMap = null; FBProjectSetup.ResumeSSR(ssr); }
            AssetDatabase.SaveAssets();
            Debug.Log($"[FreeBala] mapa {id} reimportado");
        }

        static Vector3 Real(float x, float y, float h) => Coords.FromWeb(x * 1.2f, h * 1.2f, -y * 1.2f);

        static void Shots(string id, string views)
        {
            EditorSceneManager.OpenScene($"{FBImport.SceneDir}/Map_{id}.unity");
            var go = new GameObject("CamPrint"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 62; cam.nearClipPlane = .1f; cam.farClipPlane = 3000;
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            var tx = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var outDir = Path.Combine(FBImport.GameRoot, "assets", "maps", id, "renders", "unity"); Directory.CreateDirectory(outDir);
            foreach (var v in views.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = v.Split(':'); var n = kv[1].Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                cam.transform.position = Real(n[0], n[1], n[2]); cam.transform.LookAt(Real(n[3], n[4], n[5]));
                for (int i = 0; i < 3; i++) cam.Render();                                         // aquece sombras/reflexo
                RenderTexture.active = rt; tx.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tx.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, kv[0] + ".jpg"), tx.EncodeToJPG(88));
                Debug.Log("[FreeBala] print " + kv[0]);
            }
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(go); rt.Release();
        }
    }
}
