// [mobile 09/11] Versão leve do Android. O primeiro APK deu 12 GB (7,2 GB de textura, 4,7 GB de malha) e o Gradle não fechou o pacote.
// Três cortes, todos medidos no relatório do build (FBBuildAudit, _diag\build_tamanho.txt):
//  1) Mapas: só os de Mapas (padrão praca_aguas e vila_jardim, os que vieram do jogo de navegador). FB_ANDROID_MAPS=id1,id2 troca.
//  2) Objetos de rua do 171 (Resources/X171/**.glb): o glTFast importa as imagens EMBUTIDAS num .glb sem compressão (1,07 GB em 8
//     arquivos). DesempacotarX171 troca cada .glb por .gltf + .bin + imagens soltas (FBGlbUnpack), que passam pelo TextureImporter
//     (FBGltfTexturePost: BC no PC, ASTC no celular). São carregados por Resources.Load pelo nome, sem extensão: nada muda no jogo.
//  3) Capim da Vila Jardim (VJ11_MatoCel_*, ~640 MB de malha): sai da cena SÓ no build Android. As árvores ficam.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala.EditorTools
{
    public static class FBAndroidLeve
    {
        public static HashSet<string> Mapas
        {
            get
            {
                var e = System.Environment.GetEnvironmentVariable("FB_ANDROID_MAPS");
                var l = string.IsNullOrWhiteSpace(e) ? new[] { "praca_aguas", "vila_jardim" } : e.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                return new HashSet<string>(l);
            }
        }

        const string X171 = "Assets/FreeBala/Resources/X171";

        [MenuItem("FreeBala/Otimizar/Desempacotar texturas dos objetos de rua (X171)")]
        public static void DesempacotarX171()
        {
            if (!Directory.Exists(X171)) return;
            var glbs = Directory.GetFiles(X171, "*.glb", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).ToArray();
            if (glbs.Length == 0) return;
            int n = 0;
            foreach (var glb in glbs)
            {
                var gltf = Path.ChangeExtension(glb, ".gltf");
                try
                {
                    FBGlbUnpack.Unpack(glb, gltf);
                    if (!File.Exists(gltf)) { Debug.LogWarning("[FreeBala][x171] não desempacotou " + glb); continue; }
                    AssetDatabase.DeleteAsset(glb); n++;
                }
                catch (System.Exception ex) { Debug.LogWarning("[FreeBala][x171] " + glb + ": " + ex.Message); }
            }
            AssetDatabase.Refresh();
            Debug.Log($"[FreeBala][x171] {n} de {glbs.Length} .glb desempacotados (texturas agora comprimidas por plataforma)");
        }
    }

    // capim da Vila Jardim fora do APK: roda em cada cena durante o build do player (report != null) e só no Android
    public class FBAndroidLeveCena : IProcessSceneWithReport
    {
        public int callbackOrder => 50;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null || report.summary.platform != BuildTarget.Android) return;
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(x => x && x.name.StartsWith("VJ11_MatoCel")).ToArray())
                    if (t) { Object.DestroyImmediate(t.gameObject); n++; }
            if (n > 0) Debug.Log($"[FreeBala][build] Android leve: {n} blocos de capim fora da cena {scene.name}");
            // árvores da Vila Jardim (VJ11_ArvoreCel_*: LODGroup com L0 no próprio objeto e L1/L2 nos filhos): o celular fica só com o
            // nível mais leve (L2). L0 + L1 somavam ~890 MB de malha no pacote; com eles o APK passava do limite de 4 GB do Gradle.
            int a = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
                {
                    if (!lg || !lg.name.StartsWith("VJ11_ArvoreCel")) continue;
                    var lods = lg.GetLODs(); if (lods.Length < 2) continue;
                    var fica = lods[lods.Length - 1]; var manter = new HashSet<Renderer>(fica.renderers.Where(r => r));
                    if (manter.Count == 0) continue;
                    for (int i = 0; i < lods.Length - 1; i++)
                        foreach (var r in lods[i].renderers)
                        {
                            if (!r || manter.Contains(r)) continue;
                            if (r.gameObject == lg.gameObject) { var mf = r.GetComponent<MeshFilter>(); Object.DestroyImmediate(r); if (mf) Object.DestroyImmediate(mf); }
                            else Object.DestroyImmediate(r.gameObject);
                        }
                    lg.SetLODs(new[] { fica }); lg.RecalculateBounds(); a++;
                }
            if (a > 0) Debug.Log($"[FreeBala][build] Android leve: {a} grupos de árvores só com o nível mais leve na cena {scene.name}");
        }
    }
}
