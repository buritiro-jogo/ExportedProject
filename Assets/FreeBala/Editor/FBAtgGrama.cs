// GRAMA DO ADVANCED TERRAIN GRASS NA VILA JARDIM — um comando só, para rodar com o Unity aberto.
//   · menu: FreeBala ▸ Vegetação ▸ Grama ATG na Vila Jardim
//   · laboratório (terminal): escrever "metodo FBAtgGrama.VilaJardim" em _diag\lab_pedido.txt
//   · linha de comando: Unity -batchmode -executeMethod FreeBala.EditorTools.FBAtgGrama.VilaJardimBatch
// O que faz: desempacota assets/vegetation/atg_vj.glb (gerado por tools/unity/atg_prep.py) para FBImported/Vegetation e refaz a vegetação
// do mapa (FBMapLook.VegRebake), que grava Art/Vegetation/vila_jardim_veg.asset com os tipos do ATG + a máscara de chão do FBGrassATG.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FreeBala.EditorTools
{
    public static class FBAtgGrama
    {
        const string Glb = "atg_vj.glb";

        [MenuItem("FreeBala/Vegetação/Grama ATG na Vila Jardim")]
        public static void VilaJardim()
        {
            if (!Importar()) return;
            if (FBMapLook.VegRebake("vila_jardim")) Debug.Log("[FreeBala] grama do ATG aplicada na Vila Jardim");
        }

        public static void VilaJardimBatch()
        {
            bool ok = Importar() && FBMapLook.VegRebake("vila_jardim");
            if (ok) Transparentes();                                     // a cena ficou aberta: aproveita e procura a lâmina azul do print vj1
            EditorApplication.Exit(ok ? 0 : 1);
        }

        // LÂMINAS TRANSPARENTES GRANDES: no print gfx_vj1 (06/11) metade da tela está coberta por um bloco azul translúcido. Lista os
        // renderers de material transparente com caixa grande, para achar o vidro/água fora de escala. Uso: metodo FBAtgGrama.Transparentes
        [MenuItem("FreeBala/Vegetação/Lâminas transparentes grandes")]
        public static void Transparentes()
        {
            var achados = new List<(float vol, string txt)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var b = r.bounds; float vol = b.size.x * b.size.y * b.size.z;
                if (vol < 300 || b.size.magnitude < 12) continue;
                var m = r.sharedMaterial; if (!m) continue;
                bool transp = m.renderQueue >= 2450 || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > .5f);
                if (!transp) continue;
                achados.Add((vol, $"[diag] {r.name} · material {m.name} (fila {m.renderQueue}) · centro {b.center} · tamanho {b.size} · volume {vol:0} m³ · caminho {Caminho(r.transform)}"));
            }
            Debug.Log($"[diag] lâminas transparentes grandes: {achados.Count}");
            foreach (var a in achados.OrderByDescending(a => a.vol).Take(25)) Debug.Log(a.txt);
        }

        static string Caminho(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

        // DIAGNÓSTICO da grama de tempo de jogo: refaz, fora do jogo, a mesma conta do FBGrassATG em volta de alguns pontos do mapa e
        // compara a altura da máscara com a altura real do chão (raio na cena aberta). Uso: metodo FBAtgGrama.Diagnostico
        public static void DiagnosticoBatch()
        {
            EditorSceneManager.OpenScene($"{FBImport.SceneDir}/Map_vila_jardim.unity");       // o raio precisa dos colisores do mapa
            Diagnostico(); EditorApplication.Exit(0);
        }

        [MenuItem("FreeBala/Vegetação/Diagnóstico da grama ATG")]
        public static void Diagnostico()
        {
            var data = AssetDatabase.LoadAssetAtPath<FBVegData>("Assets/FreeBala/Art/Vegetation/vila_jardim_veg.asset");
            if (!data) { Debug.LogError("[FreeBala] asset da vegetação não achado"); return; }
            Debug.Log($"[diag] tipos={data.kinds?.Length} carpetKinds={(data.carpetKinds == null ? "null" : string.Join(",", data.carpetKinds))} densidade={data.carpetDensity} " +
                      $"máscara={(data.grassMask ? $"{data.grassMask.width}x{data.grassMask.height} {data.grassMask.format} legível={data.grassMask.isReadable}" : "NULA")} " +
                      $"altura={(data.grassHeight ? $"{data.grassHeight.width}x{data.grassHeight.height} {data.grassHeight.format}" : "NULA")} rect={data.grassRect} y0={data.grassY0}");
            foreach (var k in data.kinds ?? new FBVegData.Kind[0])
                Debug.Log($"[diag] tipo {k.name}: malha={(k.mesh ? k.mesh.name + " " + k.mesh.triangles.Length / 3 + " tri" : "NULA")} material={(k.mat ? k.mat.name : "NULO")}");
            if (!data.grassMask) return;

            var raw = data.grassMask.GetRawTextureData<byte>(); int mw = data.grassMask.width, mh = data.grassMask.height;
            var hist = new int[5];
            for (int i = 0; i < raw.Length; i++) hist[Mathf.Min(4, raw[i] / 52)]++;
            Debug.Log($"[diag] densidade da máscara: 0–20%={hist[0]} · 20–40%={hist[1]} · 40–60%={hist[2]} · 60–80%={hist[3]} · 80–100%={hist[4]} (de {raw.Length})");

            var pontos = new List<Vector3>(data.openSpots); pontos.AddRange(data.edgeSpots);
            if (pontos.Count == 0) { Debug.LogWarning("[diag] o asset não tem pontos de referência"); return; }
            Physics.SyncTransforms();
            var hraw = data.grassHeight ? data.grassHeight.GetRawTextureData<ushort>() : default;
            int hw = data.grassHeight ? data.grassHeight.width : 0, hh = data.grassHeight ? data.grassHeight.height : 0;
            var r4 = data.grassRect;
            float Dens(float x, float z)
            {
                float u = (x - r4.x) / r4.z, v = (z - r4.y) / r4.w;
                if (u < 0 || v < 0 || u >= 1 || v >= 1) return -1;
                return raw[Mathf.Min(mh - 1, (int)(v * mh)) * mw + Mathf.Min(mw - 1, (int)(u * mw))] / 255f;
            }
            float Alt(float x, float z)
            {
                if (hw == 0) return data.grassY0;
                int px = Mathf.Clamp((int)((x - r4.x) / r4.z * hw), 0, hw - 1), pz = Mathf.Clamp((int)((z - r4.y) / r4.w * hh), 0, hh - 1);
                return data.grassY0 + Mathf.HalfToFloat(hraw[pz * hw + px]);
            }
            foreach (var p in pontos.Take(6))
            {
                int n = 0; float somaErro = 0; int comChao = 0;
                for (int i = -12; i <= 12; i++) for (int j = -12; j <= 12; j++)      // 25x25 pontos de 0,5 m = 12 m em volta
                    {
                        float x = p.x + i * .5f, z = p.z + j * .5f; float d = Dens(x, z); if (d <= .02f) continue;
                        n++;
                        if (Physics.Raycast(new Vector3(x, p.y + 30, z), Vector3.down, out var h, 120, 1 << FBProjectSetup.LMap, QueryTriggerInteraction.Ignore))
                        { somaErro += Mathf.Abs(h.point.y - Alt(x, z)); comChao++; }
                    }
                Debug.Log($"[diag] ponto {p}: {n} de 625 células com grama · erro médio da altura {(comChao > 0 ? (somaErro / comChao).ToString("0.00") : "?")} m ({comChao} medidas)");
            }
        }

        // copia/desempacota o GLB da grama para Assets/FreeBala/Resources/FBImported/Vegetation (o mesmo que o FBImport faz na importação)
        static bool Importar()
        {
            var src = Path.Combine(FBImport.Src, "vegetation", Glb);
            if (!File.Exists(src)) { Debug.LogError($"[FreeBala] não achei {src} — rode antes: py -3.14 tools/unity/atg_prep.py"); return false; }
            var dst = $"{FBImport.Imp}/Vegetation/{Glb}";
            FBImport.Copy(src, dst);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var gltf = FBImport.G(dst);
            if (!File.Exists(gltf)) { Debug.LogError("[FreeBala] o glTF da grama não apareceu em " + gltf); return false; }
            return true;
        }
    }
}
