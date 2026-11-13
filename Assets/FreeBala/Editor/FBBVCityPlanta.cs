// BV City rodada 11: traz a PLANTA r10 (tools/bv_city/03_planta) para o Unity, por setor de 400 m.
//
//   corredor (x, y, h) -> Unity (-x, h, +y)      1 unidade = 1 metro (MEDIDO na rodada 7)
//
// A planta e FUNDACAO: chao (vias, calcadas, quadras, relevo, agua) e volumes PROVISORIOS de edificacao
// (BVC_PROV_*, material M_PROV). Volume provisorio nunca e predio final.
//
// Lote:
//   -executeMethod FreeBala.EditorTools.FBBVCityPlanta.BatchCobertura   mascara do que a cena atual ja cobre
//   -executeMethod FreeBala.EditorTools.FBBVCityPlanta.BatchImporta     prefabs dos setores + cena + conferencia
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala.EditorTools
{
    public static partial class FBBVCityPlanta
    {
        public const string CENA = "Assets/FreeBala/Scenes/Map_bv_city.unity";
        const int LAYER_MAPA = 8;
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        public static string Saida(string nome)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../tools/bv_city/06_unity"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, nome);
        }

        public static Vector3 Mundo(float x, float y, float h) => new Vector3(-x, h, y);

        static void Sai(Action f)
        {
            int code = 0;
            try { f(); } catch (Exception e) { Debug.LogError("[BVPLANTA] " + e); code = 1; }
            EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ cobertura da cena atual
        // regiao em coordenadas do corredor, 1 m por pixel
        const float RX0 = -1000, RX1 = 2000, RY0 = -1200, RY1 = 1200;

        public static void BatchCobertura() => Sai(Cobertura);

        /// <summary>O que a cena Map_bv_city ja desenha, visto de cima: (1) so a fatia do chao (h de -5 a +1,5 m),
        /// (2) tudo. E a altura do colisor mais baixo e mais alto numa grade de 2 m. Serve para recortar a planta
        /// onde os mapas prontos ja tem chao.</summary>
        static void Cobertura()
        {
            EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);
            var sb = new StringBuilder();
            sb.AppendLine("# raizes da cena: nome | ativo | renderizadores | colisores | caixa (corredor x0 x1 y0 y1 h0 h1)");
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var rs = go.GetComponentsInChildren<Renderer>(false);
                var cs = go.GetComponentsInChildren<Collider>(false);
                string caixa = "-";
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    caixa = string.Format(IC, "{0:0.0} {1:0.0} {2:0.0} {3:0.0} {4:0.0} {5:0.0}",
                                          -b.max.x, -b.min.x, b.min.z, b.max.z, b.min.y, b.max.y);
                }
                sb.AppendLine($"{go.name} | {go.activeSelf} | {rs.Length} | {cs.Length} | {caixa}");
                if (go.name.StartsWith("BVC_CCV_"))
                    sb.AppendLine(string.Format(IC, "   marco pos corredor ({0:0.00}, {1:0.00}) h {2:0.00} giroY {3:0.00}",
                                                -go.transform.position.x, go.transform.position.z, go.transform.position.y,
                                                go.transform.eulerAngles.y));
            }
            sb.AppendLine($"# nevoa {RenderSettings.fog} modo {RenderSettings.fogMode} fim {RenderSettings.fogEndDistance.ToString(IC)}");
            File.WriteAllText(Saida("cena_atual_raizes.txt"), sb.ToString());

            int W = Mathf.RoundToInt(RX1 - RX0), H = Mathf.RoundToInt(RY1 - RY0);
            float cx = (RX0 + RX1) / 2, cy = (RY0 + RY1) / 2;
            Foto(Mundo(cx, cy, 1.5f), W, H, 0.01f, 6.5f, Saida("cobertura_chao.png"), new Color(1, 0, 1));
            Foto(Mundo(cx, cy, 600f), W, H, 1f, 700f, Saida("cobertura_tudo.png"), new Color(1, 0, 1));
            File.WriteAllText(Saida("cobertura.json"),
                "{\n \"o_que_e\": \"vista de cima da cena Map_bv_city, 1 m por pixel; magenta puro = nada desenhado\",\n" +
                string.Format(IC, " \"corredor_x\": [{0}, {1}],\n \"corredor_y\": [{2}, {3}],\n", RX0, RX1, RY0, RY1) +
                " \"pixel\": \"coluna i (da esquerda) -> x = corredor_x[1] - (i + 0.5) ; linha j (de CIMA) -> y = corredor_y[1] - (j + 0.5)\",\n" +
                " \"cobertura_chao\": \"so superficies com h entre -5,0 e +1,5 m\",\n \"cobertura_tudo\": \"tudo, de 600 m de altura\"\n}\n");

            // alturas dos colisores da camada do mapa, grade de 2 m
            Physics.SyncTransforms();
            int nx = (int)((RX1 - RX0) / 2), ny = (int)((RY1 - RY0) / 2);
            var baixo = new float[nx * ny]; var alto = new float[nx * ny];
            var hits = new RaycastHit[64];
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = RX0 + (i + 0.5f) * 2, y = RY0 + (j + 0.5f) * 2;
                    int n = Physics.RaycastNonAlloc(Mundo(x, y, 400f), Vector3.down, hits, 450f, 1 << LAYER_MAPA,
                                                    QueryTriggerInteraction.Ignore);
                    float lo = float.NaN, hi = float.NaN;
                    for (int k = 0; k < n; k++)
                    {
                        float h = hits[k].point.y;
                        if (float.IsNaN(lo) || h < lo) lo = h;
                        if (float.IsNaN(hi) || h > hi) hi = h;
                    }
                    baixo[j * nx + i] = lo; alto[j * nx + i] = hi;
                }
            var bytes = new byte[nx * ny * 8];
            Buffer.BlockCopy(baixo, 0, bytes, 0, nx * ny * 4);
            Buffer.BlockCopy(alto, 0, bytes, nx * ny * 4, nx * ny * 4);
            File.WriteAllBytes(Saida("colisor_alturas.f32"), bytes);
            File.WriteAllText(Saida("colisor_alturas.json"),
                string.Format(IC, "{{\n \"nx\": {0}, \"ny\": {1}, \"passo_m\": 2,\n \"x\": \"corredor x = {2} + (i + 0.5) * 2\",\n" +
                                  " \"y\": \"corredor y = {3} + (j + 0.5) * 2\",\n \"ordem\": \"float32, primeiro o colisor MAIS BAIXO [j*nx+i], depois o MAIS ALTO; NaN = sem colisor\"\n}}\n",
                              nx, ny, RX0, RY0));
            Debug.Log("[BVPLANTA] cobertura gravada em " + Saida(""));
        }

        /// <summary>Foto ortografica de cima, 1 pixel = 1 metro. Camera olhando para baixo: direita = +X do Unity
        /// (= -x do corredor), cima da imagem = +Z do Unity (= +y do corredor).</summary>
        public static void Foto(Vector3 pos, int W, int H, float near, float far, string saida, Color fundo, float mPorPx = 1f)
        {
            bool fog0 = RenderSettings.fog; RenderSettings.fog = false;
            var go = new GameObject("CamBVPlanta");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = H * mPorPx / 2f; cam.aspect = (float)W / H;
            cam.nearClipPlane = near; cam.farClipPlane = far;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = fundo; cam.allowHDR = false; cam.allowMSAA = false;
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(90, 0, 0);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            for (int i = 0; i < 2; i++) cam.Render();
            var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
            RenderTexture.active = rt; tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(saida, tx.EncodeToPNG());
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(go); rt.Release(); UnityEngine.Object.DestroyImmediate(tx);
            RenderSettings.fog = fog0;
            Debug.Log("[BVPLANTA] foto -> " + saida);
        }
    }
}
