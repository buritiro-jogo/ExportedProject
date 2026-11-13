// RODADA 65 — CAPAS DOS MAPAS (menu, cartões, tela de carregamento). As capas cinematográficas novas são capturadas NO JOGO (FBLuzTeste -fbluzdrone,
// super-amostragem 4K) e ficam em StreamingAssets/MapShots: <mapa>.jpg (3840×2160, só tela cheia: carregamento e cartão grande) e
// <mapa>_1080.jpg (miniaturas). Ler do StreamingAssets deixa trocar a capa sem gerar build. Sem o arquivo (ou no celular/web, onde o
// StreamingAssets não é um arquivo comum) cai nas capas antigas do Resources/MapShots.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FreeBala
{
    public static class FBMapShot
    {
        static readonly Dictionary<string, Texture2D> small = new(); static string bigId; static Texture2D bigTex;
        static bool Files => Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer;
        static Texture2D Load(string file, bool mips)
        {
            if (!Files) return null; var p = Path.Combine(Application.streamingAssetsPath, "MapShots", file); if (!File.Exists(p)) return null;
            try
            {
                var t = new Texture2D(2, 2, TextureFormat.RGBA32, mips) { name = "capa_" + file, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
                if (!t.LoadImage(File.ReadAllBytes(p), true)) { Object.Destroy(t); return null; }
                return t;
            }
            catch (System.Exception e) { Debug.LogWarning($"[FreeBala][capa] {p}: {e.Message}"); return null; }
        }
        // miniaturas e cartões (1920×1080 com mipmaps: reduz sem serrilhar)
        public static Texture2D Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (small.TryGetValue(id, out var t) && t) return t;
            t = Load(id + "_1080.jpg", true) ?? Resources.Load<Texture2D>("MapShots/" + id); small[id] = t; return t;
        }
        // tela cheia (carregamento): a 4K, uma por vez na memória
        public static Texture2D Big(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (bigId == id && bigTex) return bigTex;
            var t = Load(id + ".jpg", false); if (!t) return Get(id);
            if (bigTex) Object.Destroy(bigTex); bigTex = t; bigId = id; return t;
        }
    }
}
