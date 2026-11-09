// MAPAS PBR POR PIXEL nos materiais URP/Lit dos mapas (rodada 64). O FBMapLook dá a cada material um brilho/metal CONSTANTE pela regra do
// nome; quando existe Art/Gfx/PBR/<mapa>/<material>_ms.png (tools/r64/pbr_vj_r64.py: R metal, G oclusão, A smoothness, tirados da própria
// textura base — mesmo ladrilho), o material passa a usar o mapa: _MetallicGlossMap (R/A) + _OcclusionMap (G) da MESMA textura.
// Keywords ligadas aqui no Editor (o build leva as variantes; nada muda em tempo de jogo — regra do engasgo de shader).
//   · chamado pelo FBMapLook.Apply a cada importação do mapa (sobrevive à reinstalação);
//   · em lote, sem reimportar o mapa: FreeBala.EditorTools.FBPbrMaps.Batch -fbmap vila_jardim (aplica nos materiais já convertidos em
//     Art/MapMats/<mapa>_materiais.mat).
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBPbrMaps
    {
        const string Root = "Assets/FreeBala/Art/Gfx/PBR";
        static readonly int MetalMap = Shader.PropertyToID("_MetallicGlossMap"), OccMap = Shader.PropertyToID("_OcclusionMap");

        // textura linear (não sRGB), alfa como dado, ladrilhando, até 512 px
        static Texture2D Tex(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter; if (!ti) return null;
            bool ch = ti.sRGBTexture || ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Repeat || ti.maxTextureSize != 512 || ti.alphaSource != TextureImporterAlphaSource.FromInput || !ti.mipmapEnabled;
            if (ch)
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false;
                ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.maxTextureSize = 512; ti.textureCompression = TextureImporterCompression.Compressed; ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static bool Assign(Material o, string mapId)
        {
            if (!o || !o.HasProperty(MetalMap) || !o.HasProperty(OccMap)) return false;
            var p = $"{Root}/{mapId}/{o.name}_ms.png"; if (!File.Exists(p)) return false;
            var t = Tex(p); if (!t) return false;
            o.SetTexture(MetalMap, t); o.SetTexture(OccMap, t); o.SetFloat("_OcclusionStrength", 1f);
            o.SetFloat("_Smoothness", 1f);                                  // o A do mapa já é a smoothness absoluta (média = a regra do nome)
            o.SetFloat("_SmoothnessTextureChannel", 0); o.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            o.EnableKeyword("_METALLICSPECGLOSSMAP"); o.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(o); return true;
        }

        public static void Batch()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbmap"); string id = i >= 0 && i + 1 < a.Length ? a[i + 1] : "vila_jardim";
            int n = Run(id); Debug.Log($"[FreeBala][pbr] {id}: {n} materiais com mapa PBR"); EditorApplication.Exit(n > 0 ? 0 : 1);
        }
        [MenuItem("FreeBala/Mapas/Aplicar mapas PBR (Vila Jardim)")] static void Menu() => Run("vila_jardim");
        public static int Run(string id)
        {
            AssetDatabase.Refresh();
            var path = $"Assets/FreeBala/Art/MapMats/{id}_materiais.mat";
            var mats = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToList();
            int n = 0; foreach (var m in mats) if (Assign(m, id)) n++;
            AssetDatabase.SaveAssets();
            var faltam = Directory.Exists($"{Root}/{id}") ? Directory.GetFiles($"{Root}/{id}", "*_ms.png").Select(f => Path.GetFileName(f).Replace("_ms.png", "")).Where(nm => mats.All(m => m.name != nm)).ToList() : new System.Collections.Generic.List<string>();
            if (faltam.Count > 0) Debug.Log($"[FreeBala][pbr] mapas sem material convertido em {path}: {string.Join(", ", faltam)}");
            return n;
        }
    }
}
