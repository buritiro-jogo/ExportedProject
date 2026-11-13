// BV City rodada 11 (segunda parte do FBBVCityPlanta): prefabs dos setores, ajustes da cena e fotos de conferencia.
//
// O que muda na cena Map_bv_city (tudo por ligar/desligar objeto, mover raiz ou trocar malha por copia recortada;
// nenhum arquivo dos mapas originais e alterado; copia da cena em game/_backup/bvcity_r11_20261111):
//   1. BVC_Planta: raiz com o FBBVCitySetores (carrega os setores da planta por distancia).
//   2. centro_civico desce 1,53 m: o chao dele estava 1,4 a 2,0 m ACIMA da Praca das Aguas na costura (MEDIDO).
//   3. Paisagem do parceiro (pano de fundo de 7 km) e o recheio generico em volta da Praca das Aguas saem de cena:
//      a planta georreferenciada entra no lugar.
//   4. PA_Chao (uma malha so, 1.554 x 624 m) e recortado na zona mantida; os ladrilhos de chao do centro_civico que
//      a costura da rodada 7 tinha desligado inteiros voltam recortados em x >= 530.
//   5. marcos: Catedral e Palacio da Cultura vao para a posicao corrigida (a aerea estava ~7 m a oeste); todos sobem
//      para o chao; o Tribunal de Justica, que a costura tinha desligado por engano, volta; o marco "Palacio Helio
//      Campos" da rodada 2 e DESLIGADO (estava sobre o lote do Palacio da Cultura, com planta de outro predio);
//      a versao antiga de cada predio, fundida nas quadras do parceiro, e retirada debaixo do marco.
//   6. NavMesh do corredor assado de novo.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Setor = FreeBala.FBBVCitySetores.Setor;

namespace FreeBala.EditorTools
{
    public static partial class FBBVCityPlanta
    {
        const string GLB_DIR = "Assets/BURITIRO_Mapas/bv_city/planta/glb";
        const string MAT_DIR = "Assets/BURITIRO_Mapas/bv_city/planta/mats";
        const string REC_DIR = "Assets/BURITIRO_Mapas/bv_city/planta/recortes";
        const string TEX_DIR = "Assets/BURITIRO_Mapas/bv_city/planta/tex";
        const string RES_DIR = "Assets/FreeBala/Resources/BVCity/Setores";
        const float DESCE_CC = 1.53f, COSTURA = 530f;
        // r15: zona mantida da Praca das Aguas em METROS REAIS (era -180..530 x +-59 no espaco ampliado 1,2x do mapa).
        const float PA_X0 = -100, PA_X1 = 530, PA_Y0 = -49.2f, PA_Y1 = 49.2f;
        const float ESC_PA = 1f / 1.2f;                                    // o mapa praca_aguas veio do jogo web ampliado 1,2x
        static readonly StringBuilder LOG = new StringBuilder();
        static void Diz(string s) { Debug.Log("[BVPLANTA] " + s); LOG.AppendLine(s); }

        [Serializable] class MarcoJ { public string id; public bool ativo; public float x, y, h, giro; public float[] casco; public float[] casco2; public float[] casco3; public float[] chao_fora; }
        [Serializable] class MarcosJ { public float desce_cc_m; public MarcoJ[] marcos; }
        [Serializable] class ArvJ { public float x, y, h, escala, giro, alvo, larg; public string modelo, grupo, origem; }
        [Serializable] class ArvsJ { public ArvJ[] arvores; }

        /// <summary>r26: arvores do lado sul. Clona, nas posicoes de 06_unity/arvores_sul.json (corredor), arvores que ja existem no mapa do
        /// Centro Civico (I_CC_*): o mapa original nao e alterado, os clones ficam na raiz BVC_Arvores_Sul.</summary>
        static void PlantaArvoresSul()
        {
            string p = Saida("arvores_sul.json"); if (!File.Exists(p)) { Diz("arvores do lado sul: sem arvores_sul.json"); return; }
            var aj = JsonUtility.FromJson<ArvsJ>(File.ReadAllText(p)); if (aj == null || aj.arvores == null) return;
            var cena = SceneManager.GetActiveScene();
            var velho = Raiz(cena, "BVC_Arvores_Sul"); if (velho) UnityEngine.Object.DestroyImmediate(velho);
            var modelos = new Dictionary<string, GameObject>();
            foreach (var r in cena.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name; if (!n.StartsWith("I_CC_") || n.Contains("_LOD")) continue;
                    int k = n.LastIndexOf('_'); if (k < 6) continue; string tipo = n.Substring(0, k);
                    if (!modelos.ContainsKey(tipo) && t.GetComponentInChildren<MeshRenderer>(true) != null) modelos[tipo] = t.gameObject;
                }
            var raiz = new GameObject("BVC_Arvores_Sul"); int posto = 0, semCol = 0; var altM = new Dictionary<string, float>(); var falta = new HashSet<string>(); var dims = new Dictionary<string, string>();
            foreach (var a in aj.arvores)
            {
                if (a.modelo == null || !modelos.TryGetValue(a.modelo, out var mo)) { falta.Add(a.modelo ?? "(sem modelo)"); continue; }
                var g = UnityEngine.Object.Instantiate(mo, raiz.transform); g.name = "BVC_Arv_" + a.grupo + "_" + posto.ToString("000"); g.SetActive(true);
                g.transform.SetPositionAndRotation(Mundo(a.x, a.y, a.h), Quaternion.Euler(0, a.giro, 0));
                // r26b: 'alvo' = altura desejada em metros (a escala sai da altura do modelo); 'larg' = fator horizontal (copa baixa e larga)
                if (!altM.TryGetValue(a.modelo, out float hm))
                {
                    float y0 = 1e9f, y1 = -1e9f; foreach (var re in mo.GetComponentsInChildren<MeshRenderer>(true)) { y0 = Mathf.Min(y0, re.bounds.min.y); y1 = Mathf.Max(y1, re.bounds.max.y); }
                    hm = Mathf.Max(0.1f, y1 - Mathf.Max(y0, mo.transform.position.y)); altM[a.modelo] = hm;
                }
                float es = a.alvo > 0 ? a.alvo / hm : (a.escala > 0 ? a.escala : 1f), lg2 = a.larg > 0 ? a.larg : 1f; var ls = mo.transform.lossyScale;
                g.transform.localScale = new Vector3(ls.x * es * lg2, ls.y * es, ls.z * es * lg2); posto++;
                // r26b: o modelo so tem o nivel de perto (some com menos de 35 % da tela; de longe o mapa do parceiro mostra a malha juntada da quadra,
                // que o clone nao tem): o clone fica visivel ate 2 % da tela (~250 m). Tronco com colisor, para nao ser atravessado.
                var lg = g.GetComponent<LODGroup>();
                if (lg) { var lods = lg.GetLODs(); if (lods.Length > 0) { lods[lods.Length - 1].screenRelativeTransitionHeight = 0.02f; lg.SetLODs(lods); } }
                if (a.alvo >= 2.5f && g.GetComponentInChildren<Collider>(true) == null) { var cc2 = g.AddComponent<CapsuleCollider>(); cc2.direction = 1; cc2.radius = 0.3f; cc2.height = 3.4f; cc2.center = new Vector3(0f, 1.7f, 0f); semCol++; }
                if (!dims.ContainsKey(a.modelo))
                {
                    Bounds? b = null;
                    foreach (var re in mo.GetComponentsInChildren<MeshRenderer>(true)) { if (b == null) b = re.bounds; else { var bb = b.Value; bb.Encapsulate(re.bounds); b = bb; } }
                    if (b != null) dims[a.modelo] = string.Format(IC, "{0} {1:0.0} x {2:0.0} m, altura {3:0.0} m, base {4:0.00} m do pivo, {5} filhos", a.modelo, b.Value.size.x, b.Value.size.z,
                                                                  b.Value.size.y, b.Value.min.y - mo.transform.position.y, mo.transform.childCount);
                }
            }
            Diz(string.Format(IC, "arvores do lado sul (r26): {0} postas de {1} ({4} ganharam colisor de tronco); modelos: {2}{3}", posto, aj.arvores.Length, string.Join(" | ", dims.Values),
                              falta.Count > 0 ? "; SEM MODELO: " + string.Join(", ", falta) : "", semCol));
            var ds = modelos.Keys.Where(k => k.Contains("Oiti") || k.Contains("Mangueira") || k.Contains("Nim") || k.Contains("Caimbe") || k.Contains("Ipe") || k.Contains("Arbusto") || k.Contains("Ixora")).OrderBy(k => k);
            Diz("   modelos de vegetacao disponiveis no mapa do Centro Civico: " + string.Join(", ", ds));
        }

        public static void BatchImporta() => Sai(() =>
        {
            LOG.Clear();
            try
            {
                bool semPrefab = Environment.GetCommandLineArgs().Contains("-fbsemprefab") && Directory.Exists(RES_DIR);
                var lista = semPrefab ? ListaDosPrefabs() : Importa();
                if (semPrefab) Diz($"prefabs dos setores: reaproveitados os {lista.Count} ja gravados");
                Cena(lista); Fotos();
            }
            finally { File.WriteAllText(Saida("importa_resumo.txt"), LOG.ToString()); }
        });

        public static void BatchFotos() => Sai(() =>
        {
            LOG.Clear();
            try { EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single); Fotos(); }
            finally { File.WriteAllText(Saida("fotos_resumo.txt"), LOG.ToString()); }
        });

        /// <summary>r20: perfil do chao por raio (todas as camadas) ao longo de y, em alguns x do corredor: que objeto/material ha em cada faixa
        /// entre a pista e o muro (guia, grama, ciclovia, calcada). So leitura; grava 06_unity/perfil_calcada.txt.</summary>
        public static void BatchPerfil() => Sai(() =>
        {
            LOG.Clear();
            try
            {
                EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single); Physics.SyncTransforms();
                foreach (float x in new[] { -70f, -25f, 30f, 50f, 95f, 112f, 125f, 150f, 230f, 280f })
                {
                    Diz(string.Format(IC, "== x = {0:0}", x)); string ant = null; float y0 = 0f, hAnt = 0f;
                    for (float y = -34f; y >= -52.01f; y -= 0.1f)
                    {
                        string id = "(nada)"; float h = 0f;
                        if (Physics.Raycast(Mundo(x, y, 6f), Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            var mr = hit.collider.GetComponent<MeshRenderer>(); h = hit.point.y;
                            string mat = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "-";
                            if (mr != null && mr.sharedMaterials.Length > 1 && hit.collider is MeshCollider mc && mc.sharedMesh != null && mc.sharedMesh.subMeshCount == mr.sharedMaterials.Length)
                            {
                                int tri = hit.triangleIndex * 3;
                                for (int s = 0; s < mc.sharedMesh.subMeshCount; s++)
                                {
                                    var sd = mc.sharedMesh.GetSubMesh(s);
                                    if (tri >= sd.indexStart && tri < sd.indexStart + sd.indexCount) { mat = mr.sharedMaterials[s] != null ? mr.sharedMaterials[s].name : "-"; break; }
                                }
                            }
                            id = hit.collider.gameObject.name + " | " + mat;
                        }
                        string chave = id + " | h " + (Mathf.Round(h * 10f) / 10f).ToString("0.0", IC);
                        if (chave != ant)
                        {
                            if (ant != null) Diz(string.Format(IC, "   y {0:0.0} .. {1:0.0}: {2}", y0, y + 0.1f, ant));
                            ant = chave; y0 = y; hAnt = h;
                        }
                    }
                    if (ant != null) Diz(string.Format(IC, "   y {0:0.0} .. -52.0: {1}", y0, ant));
                }
            }
            finally { File.WriteAllText(Saida("perfil_calcada.txt"), LOG.ToString()); }
        });

        /// <summary>r25: altura do chao por raio numa lista de pontos do corredor (06_unity/pontos_alturas.txt, um "x y" por linha). So leitura;
        /// grava 06_unity/alturas_pontos.txt com o primeiro objeto atingido de cima e o mais baixo (o chao, quando ha copa ou cobertura).</summary>
        public static void BatchAlturas() => Sai(() =>
        {
            LOG.Clear();
            try
            {
                EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single); Physics.SyncTransforms();
                foreach (var lin in File.ReadAllLines(Saida("pontos_alturas.txt")))
                {
                    var p = lin.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 2 || !float.TryParse(p[0], System.Globalization.NumberStyles.Float, IC, out float x) || !float.TryParse(p[1], System.Globalization.NumberStyles.Float, IC, out float y)) continue;
                    var hs = Physics.RaycastAll(Mundo(x, y, 40f), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
                    if (hs.Length == 0) { Diz(string.Format(IC, "{0:0.0} {1:0.0} nada", x, y)); continue; }
                    System.Array.Sort(hs, (a, b) => a.distance.CompareTo(b.distance));
                    var c = hs[0]; var f = hs[hs.Length - 1];
                    Diz(string.Format(IC, "{0:0.0} {1:0.0} cima {2:0.00} {3} | baixo {4:0.00} {5} | n {6}", x, y, c.point.y, c.collider.gameObject.name, f.point.y, f.collider.gameObject.name, hs.Length));
                }
            }
            finally { File.WriteAllText(Saida("alturas_pontos.txt"), LOG.ToString()); }
        });

        /// <summary>r21: lista os renderizadores ativos que tem alguma parte ACIMA de 4,5 m numa caixa do corredor (so leitura): serve para
        /// descobrir de que objeto sao as pecas soltas no ar. Grava 06_unity/lista_altos.txt.</summary>
        public static void BatchListaAltos() => Sai(() =>
        {
            LOG.Clear();
            try
            {
                EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);
                foreach (var cx in new[] { new Vector4(-100f, -55f, 47f, 85f), new Vector4(135f, 162f, 47f, 85f), new Vector4(-100f, 330f, -47f, 47f), new Vector4(535f, 625f, 44f, 110f), new Vector4(360f, 480f, 40f, 110f) })
                {
                    Diz(string.Format(IC, "== caixa x {0:0}..{1:0}, y {2:0}..{3:0}", cx.x, cx.y, cx.z, cx.w));
                    Vector3 a = Mundo(cx.x, cx.z, 0f), b = Mundo(cx.y, cx.w, 0f);
                    float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
                    foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                        var bd = r.bounds; if (bd.max.y < 4.5f || bd.max.x < x0 || bd.min.x > x1 || bd.max.z < z0 || bd.min.z > z1) continue;
                        if (r.gameObject.name.StartsWith("BVC_F") || r.gameObject.name.StartsWith("BVC_TCE") || r.gameObject.name.StartsWith("BVC_OI")) continue;
                        var mf = r.GetComponent<MeshFilter>(); int tri = mf != null && mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0;
                        string pai = r.transform.parent != null ? r.transform.parent.name : "-";
                        Diz(string.Format(IC, "   {0} | pai {1} | mat {2} | tri {3} | h {4:0.0}..{5:0.0} | caixa mundo x {6:0}..{7:0} z {8:0}..{9:0}", r.gameObject.name, pai,
                                          r.sharedMaterial != null ? r.sharedMaterial.name : "-", tri, bd.min.y, bd.max.y, bd.min.x, bd.max.x, bd.min.z, bd.max.z));
                    }
                }
            }
            finally { File.WriteAllText(Saida("lista_altos.txt"), LOG.ToString()); }
        });

        /// <summary>r31: lista TUDO o que a cena tem dentro de uma caixa do corredor (06_unity/caixa_lista.txt: "x0 x1 y0 y1"), malha por malha:
        /// caminho, material, triangulos com o centro na caixa, faixa de altura e de planta desses triangulos. So leitura. Grava lista_caixa.txt.</summary>
        public static void BatchListaCaixa() => Sai(() =>
        {
            LOG.Clear();
            try
            {
                EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);
                var q = File.ReadAllText(Saida("caixa_lista.txt")).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                float cx0 = float.Parse(q[0], IC), cx1 = float.Parse(q[1], IC), cy0 = float.Parse(q[2], IC), cy1 = float.Parse(q[3], IC);
                Vector3 a = Mundo(cx0, cy0, 0f), b = Mundo(cx1, cy1, 0f);
                float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
                Diz(string.Format(IC, "caixa corredor x {0}..{1}, y {2}..{3}", cx0, cx1, cy0, cy1));

                void Lista(Transform t, Mesh m, string tipo, string mats, Bounds wb)
                {
                    if (m == null || wb.max.x < x0 || wb.min.x > x1 || wb.max.z < z0 || wb.min.z > z1) return;
                    string cam = t.name; var pt = t.parent; int nv = 0; while (pt != null && nv < 4) { cam = pt.name + "/" + cam; pt = pt.parent; nv++; }
                    if (!m.isReadable) { Diz(string.Format(IC, "{0} {1} | malha nao legivel | mats {2}", tipo, cam, mats)); return; }
                    var v = m.vertices; var l2w = t.localToWorldMatrix; int dentro = 0, tot = 0; float h0 = 1e9f, h1 = -1e9f, ex0 = 1e9f, ex1 = -1e9f, ey0 = 1e9f, ey1 = -1e9f, lmax = 0f; int piso = 0;
                    for (int s2 = 0; s2 < m.subMeshCount; s2++)
                    {
                        var tr = m.GetTriangles(s2); tot += tr.Length / 3;
                        for (int i = 0; i < tr.Length; i += 3)
                        {
                            var pa = l2w.MultiplyPoint3x4(v[tr[i]]); var pb = l2w.MultiplyPoint3x4(v[tr[i + 1]]); var pc = l2w.MultiplyPoint3x4(v[tr[i + 2]]); var c = (pa + pb + pc) / 3f;
                            if (c.x < x0 || c.x > x1 || c.z < z0 || c.z > z1) continue;
                            dentro++; h0 = Mathf.Min(h0, Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y))); h1 = Mathf.Max(h1, Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y)));
                            ex0 = Mathf.Min(ex0, -Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))); ex1 = Mathf.Max(ex1, -Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)));
                            ey0 = Mathf.Min(ey0, Mathf.Min(pa.z, Mathf.Min(pb.z, pc.z))); ey1 = Mathf.Max(ey1, Mathf.Max(pa.z, Mathf.Max(pb.z, pc.z)));
                            lmax = Mathf.Max(lmax, Mathf.Max((pa - pb).magnitude, Mathf.Max((pb - pc).magnitude, (pc - pa).magnitude)));
                            if (Mathf.Abs(Vector3.Cross(pb - pa, pc - pa).normalized.y) > 0.7f) piso++;
                        }
                    }
                    if (dentro == 0) return;
                    Diz(string.Format(IC, "{0} {1} | mats {2} | tri {3} de {4} na caixa ({5} deitados) | h {6:0.00}..{7:0.00} | x {8:0.0}..{9:0.0} y {10:0.0}..{11:0.0} | maior aresta {12:0.0} m", tipo, cam, mats, dentro, tot, piso, h0, h1, ex0, ex1, ey0, ey1, lmax));
                }
                foreach (var mf in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                {
                    if (!mf.gameObject.activeInHierarchy || mf.sharedMesh == null) continue; var r = mf.GetComponent<Renderer>();
                    Lista(mf.transform, mf.sharedMesh, r != null && r.enabled ? "REND" : "malha", r != null ? string.Join(",", r.sharedMaterials.Where(mm => mm != null).Select(mm => mm.name).Take(4)) : "-", CaixaMundo(mf));
                }
                foreach (var mc in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                {
                    if (!mc.gameObject.activeInHierarchy || mc.sharedMesh == null) continue; var mf = mc.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh == mc.sharedMesh) continue;
                    Lista(mc.transform, mc.sharedMesh, "COL ", "-", mc.bounds);
                }
                int no = 0;
                foreach (var k in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                {
                    if (k is MeshCollider || !k.gameObject.activeInHierarchy) continue; var c = k.bounds.center; if (c.x < x0 || c.x > x1 || c.z < z0 || c.z > z1) continue;
                    if (no++ < 40) Diz(string.Format(IC, "COLISOR {0} {1}/{2} | centro x {3:0.0} y {4:0.0} h {5:0.0} | tam {6:0.0} x {7:0.0} x {8:0.0}", k.GetType().Name, k.transform.parent != null ? k.transform.parent.name : "-", k.name, -c.x, c.z, c.y, k.bounds.size.x, k.bounds.size.y, k.bounds.size.z));
                }
                Diz("colisores simples na caixa: " + no);
            }
            finally { File.WriteAllText(Saida("lista_caixa.txt"), LOG.ToString()); }
        });

        /// <summary>r22: lista o que o mapa da praca tem dentro da faixa norte entre a pista e o muro (corredor y -48,4..-40,4, x -100..333),
        /// no mapa ORIGINAL do parceiro (so leitura). Grava 06_unity/lista_faixa.txt.</summary>
        public static void BatchListaFaixa() => Sai(() =>
        {
            LOG.Clear();
            try
            {
                EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);      // a cena do BV City: o mapa da praca ja esta na escala e na posicao daqui
                Vector3 a = Mundo(-100f, -48.4f, 0f), b = Mundo(333f, -40.4f, 0f);
                float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
                var fam = new SortedDictionary<string, List<string>>();
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var bd = r.bounds; if (bd.max.x < x0 || bd.min.x > x1 || bd.max.z < z0 || bd.min.z > z1) continue;
                    bool grande = bd.size.x > 60f || bd.size.z > 60f;
                    var t = r.transform; while (t.parent != null && t.parent.parent != null) t = t.parent;      // filho direto da raiz
                    string nome = System.Text.RegularExpressions.Regex.Replace(t.name, @"[_\.]?p?\d+$", "");
                    string chave = (grande ? "GRANDE " : "pequeno ") + nome;
                    if (!fam.TryGetValue(chave, out var l)) fam[chave] = l = new List<string>();
                    l.Add(string.Format(IC, "{0:0.0}", bd.center.z));
                }
                foreach (var kv in fam)
                {
                    var ys = kv.Value.Select(s => float.Parse(s, IC)).OrderBy(v => v).ToList();
                    Diz(string.Format(IC, "{0} x{1} | centro y de {2:0.0} a {3:0.0} (mediana {4:0.0})", kv.Key, ys.Count, ys[0], ys[ys.Count - 1], ys[ys.Count / 2]));
                }
                int nc = 0; var cy = new List<float>();
                foreach (var k in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                {
                    if (k.GetComponent<Renderer>() || !k.gameObject.activeInHierarchy) continue;
                    var c = k.bounds.center; if (c.x < x0 || c.x > x1 || c.z < z0 || c.z > z1) continue; nc++; cy.Add(c.z);
                }
                cy.Sort(); Diz(string.Format(IC, "colisores soltos com o centro na faixa: {0}{1}", nc, nc > 0 ? string.Format(IC, " | y de {0:0.0} a {1:0.0}", cy[0], cy[cy.Count - 1]) : ""));
            }
            finally { File.WriteAllText(Saida("lista_faixa.txt"), LOG.ToString()); }
        });

        /// <summary>Lista dos setores a partir dos prefabs ja gravados (o quadrado de 400 m sai do nome).</summary>
        static List<Setor> ListaDosPrefabs()
        {
            var lista = new List<Setor>(); var rx = new Regex(@"X(-?\d+)_Y(-?\d+)$");
            foreach (var arq in Directory.GetFiles(RES_DIR, "*.prefab").OrderBy(x => x))
            {
                string sid = Path.GetFileNameWithoutExtension(arq); var mm = rx.Match(sid); if (!mm.Success) continue;
                float x0 = int.Parse(mm.Groups[1].Value) * 400f, y0 = int.Parse(mm.Groups[2].Value) * 400f;
                lista.Add(new Setor { id = sid, min = new Vector2(-(x0 + 400f), y0), max = new Vector2(-x0, y0 + 400f) });
            }
            return lista;
        }

        /// <summary>Teste do carregamento por setor EM PLAY, numa cena so com a planta (nao e o jogo): o alvo percorre uma
        /// rota de ~11 km a 80 m/s. Quem encerra o Editor e o proprio teste (FBBVCityStreamTeste).</summary>
        public static void BatchStream()
        {
            try
            {
                var cena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var planta = new GameObject("BVC_Planta");
                var st = planta.AddComponent<FreeBala.FBBVCitySetores>(); st.setores = ListaDosPrefabs().ToArray();
                var alvo = new GameObject("Alvo");
                var te = alvo.AddComponent<FreeBala.FBBVCityStreamTeste>(); te.st = st; te.saida = Saida("stream_teste.txt");
                if (File.Exists(te.saida)) File.Delete(te.saida);
                Directory.CreateDirectory(REC_DIR);
                EditorSceneManager.SaveScene(cena, "Assets/BURITIRO_Mapas/bv_city/planta/Teste_carregamento_setores.unity");
                Debug.Log($"[BVPLANTA] teste de carregamento: {st.setores.Length} setores; entrando em Play");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogError("[BVPLANTA] " + e); EditorApplication.Exit(1); }
        }

        /// <summary>Teste do carregador por setor SEM Play (o Play em lote nao avancou os quadros): o mesmo Passo() que o
        /// jogo chama, com carga na hora, ao longo de uma rota de ~11 km a 80 m/s em passos de 0,25 s. Mede cargas,
        /// descargas, o que faltou, o tempo de cada carga e a memoria com tudo carregado e depois de descarregar.
        /// NAO mede: carga assincrona dentro do jogo nem engasgo de quadro.</summary>
        public static void BatchStreamEdit() => Sai(() =>
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var planta = new GameObject("BVC_Planta") { hideFlags = HideFlags.DontSave };
            var st = planta.AddComponent<FreeBala.FBBVCitySetores>(); st.setores = ListaDosPrefabs().ToArray();
            var c = new[] { new Vector2(0, 0), new Vector2(2500, 0), new Vector2(2500, 1500), new Vector2(-1500, 1500), new Vector2(-1500, -1200), new Vector2(0, 0) };
            float vel = 80f, dt = st.intervalo; var sb = new StringBuilder(); var sw = new System.Diagnostics.Stopwatch();
            long mem0 = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            int naPartida = st.CarregaAgora(new Vector3(-c[0].x, 2f, c[0].y), st.raioPartida);      // como o Update faz no primeiro quadro
            sb.AppendLine($"# na partida: {naPartida} setores carregados na hora (raio {st.raioPartida} m)");
            var faltaDesde = new Dictionary<int, float>(); float t = 0, percorrido = 0, piorAtraso = 0, piorCarga = 0, somaCarga = 0; int maxVivos = 0, maxFalt = 0, maxSobra = 0, nCarga = 0, passos = 0;
            long memMax = mem0;
            for (int s = 1; s < c.Length; s++)
            {
                var a = new Vector3(-c[s - 1].x, 2f, c[s - 1].y); var b = new Vector3(-c[s].x, 2f, c[s].y); float L = (b - a).magnitude;
                for (float u = 0; u < L; u += vel * dt)
                {
                    var p = Vector3.Lerp(a, b, u / L); int antes = st.Cargas;
                    sw.Restart(); st.Passo(p); sw.Stop(); passos++; t += dt; percorrido += vel * dt;
                    if (st.Cargas > antes) { float ms = (float)sw.Elapsed.TotalMilliseconds; piorCarga = Mathf.Max(piorCarga, ms); somaCarga += ms; nCarga++; }
                    int falt = 0, sobra = 0;
                    for (int i = 0; i < st.setores.Length; i++)
                    {
                        float d = FreeBala.FBBVCitySetores.Dist(st.setores[i], p); bool vivo = st.Vivo(i);
                        if (d < st.raioEntra - 150f && !vivo) { falt++; if (!faltaDesde.ContainsKey(i)) faltaDesde[i] = t; piorAtraso = Mathf.Max(piorAtraso, t - faltaDesde[i]); }
                        else faltaDesde.Remove(i);
                        if (d > st.raioSai + 1f && vivo) sobra++;
                    }
                    maxVivos = Mathf.Max(maxVivos, st.Vivos); maxFalt = Mathf.Max(maxFalt, falt); maxSobra = Mathf.Max(maxSobra, sobra);
                    if (passos % 8 == 0)
                    {
                        long mem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(); memMax = Math.Max(memMax, mem);
                        sb.AppendLine(string.Format(IC, "{0:0.0} {1:0} {2:0} {3} {4} {5} {6:0}", t, -p.x, p.z, st.Vivos, falt, sobra, mem / 1048576f));
                    }
                }
            }
            int vivosFim = st.Vivos;
            st.Passo(new Vector3(1e6f, 0, 1e6f));                       // longe de tudo: descarrega o que sobrou
            EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect();
            long memFim = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            var r = new StringBuilder();
            r.AppendLine("TESTE NO EDITOR, SEM PLAY: o mesmo Passo() do jogo, com carga na hora. Nao mede carga assincrona nem engasgo de quadro.");
            r.AppendLine(string.Format(IC, "rota: {0:0} m a {1:0} m/s, passos de {2:0.00} s ({3} passos)", percorrido, vel, dt, passos));
            r.AppendLine($"setores: {st.setores.Length}; entra a {st.raioEntra} m, sai a {st.raioSai} m");
            r.AppendLine($"cargas {st.Cargas}; descargas {st.Descargas}; maximo de setores vivos ao mesmo tempo {maxVivos}; vivos ao fim da rota {vivosFim}; depois de afastar {st.Vivos}");
            r.AppendLine(string.Format(IC, "setor a menos de {0:0} m ainda nao carregado: maximo {1} ao mesmo tempo; pior espera {2:0.00} s (tempo simulado)", st.raioEntra - 150f, maxFalt, piorAtraso));
            r.AppendLine($"setor vivo alem do raio de saida: maximo {maxSobra}");
            r.AppendLine(string.Format(IC, "tempo de uma carga (Resources.Load + Instantiate, com colisores): media {0:0.0} ms, pior {1:0.0} ms, em {2} cargas", nCarga > 0 ? somaCarga / nCarga : 0, piorCarga, nCarga));
            r.AppendLine(string.Format(IC, "memoria alocada pelo Unity: inicio {0:0} MB; maximo na rota {1:0} MB; depois de descarregar tudo {2:0} MB", mem0 / 1048576f, memMax / 1048576f, memFim / 1048576f));
            r.AppendLine("# t(s) x_corredor y_corredor vivos faltando sobrando memoria_MB");
            r.Append(sb);
            File.WriteAllText(Saida("stream_teste_editor.txt"), r.ToString());
            UnityEngine.Object.DestroyImmediate(planta);
        });

        // ------------------------------------------------------------------ materiais e prefabs
        static Dictionary<string, Material> Mats()
        {
            Directory.CreateDirectory(MAT_DIR); AssetDatabase.Refresh();
            var d = new Dictionary<string, Material>();
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var tex = new Dictionary<string, (string tex, float ladrilho, string fator)>(); int nTex = 0;
            if (File.Exists(Saida("materiais_tex.txt")))
                foreach (var ln in File.ReadAllLines(Saida("materiais_tex.txt")))
                {
                    var q = ln.Split(' '); if (q.Length < 4) continue;
                    tex[q[0]] = (q[1], float.Parse(q[2], IC), q[3]);
                }
            AssetDatabase.Refresh();
            foreach (var ln in File.ReadAllLines(Saida("materiais.txt")))
            {
                var c = ln.Split(' '); if (c.Length < 3) continue;
                string nome = c[0];
                var k = FreeBala.FBWater.Classify(nome);
                if (k != FreeBala.FBWater.Kind.None)
                {
                    var t = AssetDatabase.LoadAssetAtPath<Material>("Assets/FreeBala/Resources/Water/" + FreeBala.FBWater.MaterialName(k) + ".mat");
                    if (t) { d[nome] = t; continue; }
                }
                string p = $"{MAT_DIR}/{nome}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) { m = new Material(sh) { name = nome }; AssetDatabase.CreateAsset(m, p); }
                ColorUtility.TryParseHtmlString("#" + c[1], out var cor);
                m.SetColor("_BaseColor", cor); m.SetFloat("_Smoothness", 1f - float.Parse(c[2], IC)); m.SetFloat("_Metallic", 0f);
                // r15: textura PROPRIA (gerada por script, 04_texturas) no chao da planta. O UV da planta esta em metros: a escala
                // da textura e 1 / (metros por ladrilho). Material sem linha na tabela (volume provisorio, lazer) fica chapado.
                if (tex.TryGetValue(nome, out var tx))
                {
                    var img = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TEX_DIR}/{tx.tex}.jpg");
                    if (img)
                    {
                        ColorUtility.TryParseHtmlString("#" + tx.fator, out var fat);
                        m.SetTexture("_BaseMap", img); m.SetTextureScale("_BaseMap", new Vector2(1f / tx.ladrilho, 1f / tx.ladrilho));
                        m.SetColor("_BaseColor", fat); nTex++;
                    }
                    else Diz("textura da planta nao encontrada: " + tx.tex);
                }
                else m.SetTexture("_BaseMap", null);
                EditorUtility.SetDirty(m); d[nome] = m;
            }
            AssetDatabase.SaveAssets();
            Diz($"materiais da planta: {d.Count}, com textura propria {nTex}");
            return d;
        }

        static List<Setor> Importa()
        {
            AssetDatabase.Refresh();
            var mats = Mats();
            Directory.CreateDirectory(RES_DIR); AssetDatabase.Refresh();
            var lista = new List<Setor>(); float errRef = 0; int tri = 0, nref = 0, semMat = 0, malhas = 0, colis = 0, falhou = 0;
            var rx = new Regex(@"X(-?\d+)_Y(-?\d+)$");
            foreach (var arq in Directory.GetFiles(GLB_DIR, "*.glb").OrderBy(x => x))
            {
                string p = arq.Replace('\\', '/'); string sid = Path.GetFileNameWithoutExtension(p);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var mm = rx.Match(sid);
                if (!asset || !mm.Success) { Diz("GLB nao importado: " + p); falhou++; continue; }
                float x0 = int.Parse(mm.Groups[1].Value) * 400f, y0 = int.Parse(mm.Groups[2].Value) * 400f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("REF_")) continue;
                    bool a = t.name.EndsWith("_A");
                    var esp = Mundo(a ? x0 : x0 + 400f, a ? y0 : y0 + 400f, 0f);
                    errRef = Mathf.Max(errRef, (t.position - esp).magnitude); nref++;
                }
                var raiz = new GameObject(sid);
                int triS = 0; string pre = "BVC_" + sid + "_";
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    var o = mf.gameObject; var r = o.GetComponent<MeshRenderer>();
                    string mn = o.name.StartsWith(pre) ? "M_" + o.name.Substring(pre.Length) : null;
                    if (mn != null && mats.TryGetValue(mn, out var mat)) r.sharedMaterial = mat; else semMat++;
                    bool agua = mn != null && (mn.StartsWith("M_AGUA") || mn == "M_PISCINA");
                    bool prov = mn != null && mn.StartsWith("M_PROV");
                    r.shadowCastingMode = prov ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    r.receiveShadows = !agua;
                    if (!agua) { var mc = o.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; colis++; }
                    o.layer = LAYER_MAPA;
                    o.transform.SetParent(raiz.transform, true);
                    triS += (int)(mf.sharedMesh.GetIndexCount(0) / 3); malhas++;
                }
                UnityEngine.Object.DestroyImmediate(go);
                PrefabUtility.SaveAsPrefabAsset(raiz, $"{RES_DIR}/{sid}.prefab");
                UnityEngine.Object.DestroyImmediate(raiz);
                lista.Add(new Setor { id = sid, min = new Vector2(-(x0 + 400f), y0), max = new Vector2(-x0, y0 + 400f), triangulos = triS });
                tri += triS;
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Diz($"prefabs dos setores: {lista.Count} (GLB sem importar: {falhou}); malhas {malhas}; colisores {colis}; triangulos {tri}; " +
                $"malhas sem material da tabela: {semMat}");
            Diz(string.Format(IC, "conferencia de posicao: {0} vazios REF; maior erro contra (-x, h, +y) = {1:0.0000} m", nref, errRef));
            return lista;
        }

        // ------------------------------------------------------------------ cena
        static GameObject Raiz(Scene c, string nome) => c.GetRootGameObjects().FirstOrDefault(g => g.name == nome);
        static float CorX(Vector3 p) => -p.x;
        static Bounds CaixaMundo(MeshFilter mf)
        {
            var b = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
            var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return r;
        }

        // ------------------------------------------------------------------ r15: Praca das Aguas em escala real
        // r15d: BOCAS DE RUA (06_unity/bocas_pa.txt, gravado pelo ancora.py): recortes da zona onde uma rua real cruza a borda da
        // praca. Cada uma em coordenadas do Unity: (Xmin, Xmax, Zmin, Zmax) = (-x1, -x0, y0, y1).
        static readonly List<Vector4> BOCAS = new List<Vector4>();
        static void LeBocas()
        {
            BOCAS.Clear();
            if (!File.Exists(Saida("bocas_pa.txt"))) { Diz("bocas_pa.txt nao encontrado: zona sem bocas de rua"); return; }
            foreach (var ln in File.ReadAllLines(Saida("bocas_pa.txt")))
            {
                var c = ln.Split(' '); if (c.Length < 4) continue;
                float x0 = float.Parse(c[0], IC), x1 = float.Parse(c[1], IC), y0 = float.Parse(c[2], IC), y1 = float.Parse(c[3], IC);
                BOCAS.Add(new Vector4(-x1, -x0, y0, y1));
            }
            Diz($"bocas de rua na borda da praca: {BOCAS.Count}");
        }
        static bool NaBoca(Vector3 w) { foreach (var b in BOCAS) if (w.x >= b.x && w.x <= b.y && w.z >= b.z && w.z <= b.w) return true; return false; }
        static bool TocaBoca(Bounds q) { foreach (var b in BOCAS) if (q.max.x > b.x && q.min.x < b.y && q.max.z > b.z && q.min.z < b.w) return true; return false; }
        static bool NaZonaPA(Vector3 w) { float x = -w.x; return x >= PA_X0 && x <= PA_X1 && w.z >= PA_Y0 && w.z <= PA_Y1 && !NaBoca(w); }

        /// <summary>O mapa praca_aguas foi gerado em metros e AMPLIADO 1,2x na exportacao do jogo web (personagem de 2,1 m).
        /// No Unity o personagem tem 1,8 m e a planta e o centro_civico estao em 1:1: aqui a praca volta ao tamanho real,
        /// reduzida em torno da origem (que e a origem do corredor). So na cena do BV City; Map_praca_aguas.unity nao muda.
        /// Junto: raizes soltas da cena-base, luminarias e janelas medidas (posicao de mundo), dados da vegetacao (copia),
        /// e voltam as pecas que a costura da rodada 7 desligou em x 530..760 DO ESPACO AMPLIADO (x real 442..633).</summary>
        static void EscalaPA(Scene cena, GameObject pa, GameObject cc)
        {
            float k = ESC_PA;
            if (pa.transform.localScale.x < 0.95f) throw new Exception("Map_praca_aguas ja esta reduzido: restaure a cena do backup antes de importar");
            foreach (var nome in new[] { "Map_praca_aguas", "Pontos", "FB_PropsTrocados", "PARADA DE ONIBUS RR", "Reflexo" })
            {
                var g = Raiz(cena, nome); if (!g) { Diz("escala PA: raiz ausente " + nome); continue; }
                var t = g.transform; var p0 = t.position; var s0 = t.localScale;
                t.position = p0 * k; if (nome != "Reflexo") t.localScale = s0 * k;
                Diz(string.Format(IC, "escala PA: {0} pos ({1:0.00}, {2:0.00}, {3:0.00}) -> ({4:0.00}, {5:0.00}, {6:0.00}), escala {7:0.000} -> {8:0.000}",
                    nome, p0.x, p0.y, p0.z, t.position.x, t.position.y, t.position.z, s0.x, t.localScale.x));
            }
            Physics.SyncTransforms();

            // pecas que a costura da r7 desligou no espaco ampliado. As que JA nascem desligadas na cena da praca (o original de
            // um objeto trocado a mao, por exemplo) continuam desligadas: a lista sai da propria Map_praca_aguas.unity.
            var jaDesl = new HashSet<string>();
            string Caminho(Transform q) { var sb = new StringBuilder(q.name); for (var u = q.parent; u; u = u.parent) sb.Insert(0, u.name + "/"); return sb.ToString(); }
            var cenaPA = EditorSceneManager.OpenScene("Assets/FreeBala/Scenes/Map_praca_aguas.unity", OpenSceneMode.Additive);
            foreach (var g in cenaPA.GetRootGameObjects())
                foreach (var t in g.GetComponentsInChildren<Transform>(true))
                    if (!t.gameObject.activeSelf) jaDesl.Add(Caminho(t));
            EditorSceneManager.CloseScene(cenaPA, true);
            SceneManager.SetActiveScene(cena);
            Diz($"escala PA: {jaDesl.Count} objetos ja nascem desligados em Map_praca_aguas.unity: {string.Join(", ", jaDesl.Take(12))}");
            int rel = 0;
            foreach (var nome in new[] { "Map_praca_aguas", "FB_PropsTrocados" })
            {
                var g = Raiz(cena, nome); if (!g) continue;
                foreach (var t in g.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject.activeSelf || t == g.transform || jaDesl.Contains(Caminho(t))) continue;
                    Vector3 c; var mf = t.GetComponent<MeshFilter>(); var col = t.GetComponent<Collider>();
                    if (mf && mf.sharedMesh) c = CaixaMundo(mf).center;
                    else if (col is MeshCollider mc && mc.sharedMesh) c = t.TransformPoint(mc.sharedMesh.bounds.center);
                    else if (col is BoxCollider bc) c = t.TransformPoint(bc.center);
                    else if (col is CapsuleCollider kc) c = t.TransformPoint(kc.center);      // r15b: tronco de arvore e poste (FBCol_tree / FBCol_pole)
                    else if (col is SphereCollider sc) c = t.TransformPoint(sc.center);
                    else continue;
                    float xa = -c.x / k;                                   // x no espaco ampliado, onde a r7 cortou
                    if (xa > 530f && xa < 760f) { t.gameObject.SetActive(true); rel++; }
                }
            }
            Diz($"escala PA: {rel} pecas da praca religadas (a costura da r7 cortou em x = 530 do espaco ampliado = 442 m reais)");

            // luminarias e janelas medidas na importacao: posicoes de mundo
            foreach (var raiz in cena.GetRootGameObjects())
            {
                if (raiz == cc) continue;
                // r15b (verificacao): o FBPole guarda o PE do poste e a altura em posicao de mundo (FBPolePrep): reduzem junto,
                // senao a batida de carro gira o poste em volta do pe antigo.
                int nPole = 0;
                foreach (var pl in raiz.GetComponentsInChildren<FreeBala.FBPole>(true)) { pl.basePos *= k; pl.height *= k; EditorUtility.SetDirty(pl); nPole++; }
                if (nPole > 0) Diz($"escala PA: {nPole} postes (FBPole) de {raiz.name}: pe e altura reduzidos");
                foreach (var d in raiz.GetComponentsInChildren<FreeBala.FBLampData>(true))
                {
                    var p = new List<Vector3>(); var so = new List<bool>(); int n0 = d.pos != null ? d.pos.Length : 0;
                    for (int i = 0; i < n0; i++)
                    {
                        var w = d.pos[i] * k; if (!NaZonaPA(w)) continue;
                        p.Add(w); so.Add(d.sodium != null && i < d.sodium.Length && d.sodium[i]);
                    }
                    d.pos = p.ToArray(); d.sodium = so.ToArray(); EditorUtility.SetDirty(d);
                    Diz($"escala PA: luminarias de {d.gameObject.name}: {n0} -> {p.Count} (as de fora da zona saem)");
                }
                foreach (var d in raiz.GetComponentsInChildren<FreeBala.FBWindowData>(true))
                {
                    var c = new List<Vector3>(); var nn = new List<Vector3>(); var sz = new List<Vector2>(); int n0 = d.center != null ? d.center.Length : 0;
                    for (int i = 0; i < n0; i++)
                    {
                        var w = d.center[i] * k; if (!NaZonaPA(w)) continue;
                        c.Add(w); nn.Add(i < d.normal.Length ? d.normal[i] : Vector3.forward); sz.Add(i < d.size.Length ? d.size[i] * k : Vector2.one);
                    }
                    d.center = c.ToArray(); d.normal = nn.ToArray(); d.size = sz.ToArray(); EditorUtility.SetDirty(d);
                    Diz($"escala PA: janelas de {d.gameObject.name}: {n0} -> {c.Count}");
                }
            }

            // vegetacao instanciada: os dados sao um asset em posicao de mundo, compartilhado com a cena da praca -> copia reduzida
            var vg = Raiz(cena, "Vegetacao"); var veg = vg ? vg.GetComponent<FreeBala.FBVegetation>() : null;
            if (veg && veg.data)
            {
                var o = veg.data; var nd = UnityEngine.Object.Instantiate(o); nd.name = "bv_city_pa_veg";
                var S = Matrix4x4.Scale(new Vector3(k, k, k));
                nd.cells = new List<FreeBala.FBVegData.Cell>(); int tot = 0;
                foreach (var c in o.cells)
                {
                    // r15b (verificacao): o filtro e por INSTANCIA (a celula tem ate 14 m de raio; pelo centro saiam 35 % dos tufos
                    // de dentro da zona e ficavam 8 de fora). A ordem das instancias fica (a densidade usa um prefixo do array).
                    if (c.m == null) continue;
                    var lm = new List<Matrix4x4>(); var soma = Vector3.zero;
                    foreach (var m0 in c.m)
                    {
                        var m1 = S * m0; var pos = (Vector3)m1.GetColumn(3);
                        if (!NaZonaPA(pos)) continue;
                        lm.Add(m1); soma += pos;
                    }
                    if (lm.Count == 0) continue;
                    var ce = soma / lm.Count; float rr = 0; foreach (var m1 in lm) rr = Mathf.Max(rr, Vector3.Distance(ce, (Vector3)m1.GetColumn(3)));
                    nd.cells.Add(new FreeBala.FBVegData.Cell { center = ce, radius = rr + 1.5f, kind = c.kind, m = lm.ToArray() }); tot += lm.Count;
                }
                nd.total = tot;
                nd.openSpots = o.openSpots.Select(q => q * k).Where(NaZonaPA).ToList();
                nd.edgeSpots = o.edgeSpots.Select(q => q * k).Where(NaZonaPA).ToList();
                nd.grassRect = o.grassRect * k; nd.grassY0 = o.grassY0 * k; nd.grassMask = null; nd.grassHeight = null;
                AssetDatabase.CreateAsset(nd, REC_DIR + "/PA_veg_r15.asset");
                var gr = nd.grassRect; int zer = 0;
                if (o.grassMask)
                {
                    var t = UnityEngine.Object.Instantiate(o.grassMask); t.name = "bv_city_pa_grama_mascara"; int w = t.width, h = t.height;
                    bool Fora(int i) { float x = gr.x + (i % w + 0.5f) / w * gr.z, z = gr.y + (i / w + 0.5f) / h * gr.w; return !NaZonaPA(new Vector3(x, 0, z)); }
                    if (t.format == TextureFormat.R8)
                    {
                        var raw = t.GetRawTextureData<byte>();
                        for (int i = 0; i < raw.Length; i++) if (raw[i] != 0 && Fora(i)) { raw[i] = 0; zer++; }
                    }
                    else
                    {
                        var px = t.GetPixels32();
                        for (int i = 0; i < px.Length; i++) if (px[i].a != 0 && Fora(i)) { px[i].a = 0; zer++; }
                        t.SetPixels32(px);
                    }
                    t.Apply(false, false); AssetDatabase.AddObjectToAsset(t, nd); nd.grassMask = t;
                }
                if (o.grassHeight)
                {
                    var t = UnityEngine.Object.Instantiate(o.grassHeight); t.name = "bv_city_pa_grama_altura";
                    if (t.format == TextureFormat.RHalf)
                    {
                        var raw = t.GetRawTextureData<ushort>();
                        for (int i = 0; i < raw.Length; i++) raw[i] = Mathf.FloatToHalf(Mathf.HalfToFloat(raw[i]) * k);
                    }
                    else Diz("escala PA: altura da grama em formato " + t.format + " - nao reduzida");
                    t.Apply(false, false); AssetDatabase.AddObjectToAsset(t, nd); nd.grassHeight = t;
                }
                EditorUtility.SetDirty(nd); AssetDatabase.SaveAssets();
                veg.data = nd; EditorUtility.SetDirty(veg);
                Diz($"escala PA: vegetacao {o.cells.Count} celulas / {o.total} instancias -> {nd.cells.Count} / {tot} na zona; mascara da grama: {zer} pontos fora da zona zerados");
            }
            else Diz("escala PA: sem Vegetacao/FBVegData na cena");
        }

        static void Cena(List<Setor> lista)
        {
            var cena = EditorSceneManager.OpenScene(CENA, OpenSceneMode.Single);
            Directory.CreateDirectory(REC_DIR); AssetDatabase.Refresh();

            // 1. raiz da planta
            var velho = Raiz(cena, "BVC_Planta"); if (velho) UnityEngine.Object.DestroyImmediate(velho);
            var planta = new GameObject("BVC_Planta");
            var st = planta.AddComponent<FreeBala.FBBVCitySetores>();
            st.setores = lista.ToArray();
            Diz($"BVC_Planta: {lista.Count} setores; entra a {st.raioEntra} m, sai a {st.raioSai} m");

            foreach (var g in cena.GetRootGameObjects())
                Diz($"raiz {g.name}: componentes [{string.Join(", ", g.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name))}] filhos {g.transform.childCount}");

            // 2. centro_civico: desce e perde o pano de fundo
            var cc = Raiz(cena, "BVC_centro_civico");
            if (!cc) throw new Exception("cena sem BVC_centro_civico");
            var p0 = cc.transform.position;
            cc.transform.position = new Vector3(p0.x, -DESCE_CC, p0.z);
            Diz(string.Format(IC, "centro_civico: altura da raiz {0:0.00} -> {1:0.00} m", p0.y, cc.transform.position.y));
            foreach (var nome in new[] { "Paisagem", "Horizonte" })
            {
                var t = cc.transform.Find(nome);
                if (t) { int n = t.GetComponentsInChildren<Renderer>(true).Length; t.gameObject.SetActive(false); Diz($"centro_civico/{nome} desligado ({n} renderizadores)"); }
                else Diz($"centro_civico/{nome} nao encontrado");
            }

            // 3. Praca das Aguas: recheio generico fora da zona mantida
            var pa = Raiz(cena, "Map_praca_aguas");
            if (!pa) throw new Exception("cena sem Map_praca_aguas");
            LeBocas();
            EscalaPA(cena, pa, cc);
            var fam = new SortedDictionary<string, int>(); var famCorte = new SortedDictionary<string, int>(); int nCol = 0, nRec = 0, nMuro = 0;
            void Conta(SortedDictionary<string, int> d, string n) { string f = Regex.Replace(n, @"[_.]?\d+$", ""); d[f] = d.TryGetValue(f, out var q) ? q + 1 : 1; }
            foreach (var r in pa.GetComponentsInChildren<Renderer>(false))
            {
                string n = r.gameObject.name;
                if (n == "PA_Chao") continue;
                // pano de fundo e lotes GERADOS (fachada generica atras do muro): saem inteiros
                // r15d: o MURO de perimetro da arena tambem sai (no real nao ha muro: ha fachada, e as ruas transversais passam)
                bool generico = n.StartsWith("PA_Cidade_Entorno") || n.StartsWith("PA_Entorno") || n.StartsWith("PA_Arvore_Fundo") ||
                                n.StartsWith("PA_Lotes_") || n.StartsWith("PA_Predios_Muros") || n.StartsWith("PA_Muro_Perimetro");
                var b = r.bounds; float cx = -b.center.x;
                bool foraX = cx < PA_X0 || cx > PA_X1, foraY = b.min.z > PA_Y1 || b.max.z < PA_Y0;
                if (generico || foraX || foraY) { r.gameObject.SetActive(false); Conta(fam, n); continue; }
                if (b.min.z >= PA_Y0 - 0.3f && b.max.z <= PA_Y1 + 0.3f && -b.max.x >= PA_X0 - 0.3f && -b.min.x <= PA_X1 + 0.3f) continue;   // inteiro dentro da zona
                var mf = r.GetComponent<MeshFilter>();
                bool grande = b.size.x > 60f || b.size.z > 60f;                              // so malha juntada grande e recortada
                Mesh nm = grande && mf && mf.sharedMesh ? RecortaRet(mf.sharedMesh, mf.transform.localToWorldMatrix, -PA_X1, -PA_X0, PA_Y0, PA_Y1, out _, out _) : null;
                if (nm == null) { if (b.center.z < PA_Y0 || b.center.z > PA_Y1) { r.gameObject.SetActive(false); Conta(fam, n); } continue; }
                if (nm.vertexCount == 0) { r.gameObject.SetActive(false); Conta(fam, n); continue; }       // r15b: recorte vazio (tapume fora do muro): o objeto sai
                Grava(nm, "PA_" + n + "_" + (nRec++));
                var mc = r.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nm;
                mf.sharedMesh = nm; Conta(famCorte, n);
            }
            foreach (var k in pa.GetComponentsInChildren<Collider>(false))
            {
                if (k.GetComponent<Renderer>()) continue;
                var b = k.bounds; float cx = -b.center.x;
                if (cx < PA_X0 || cx > PA_X1 || b.center.z < PA_Y0 || b.center.z > PA_Y1) { k.gameObject.SetActive(false); nCol++; continue; }
                // r15d: colisor do muro = caixa comprida e fina deitada ao longo da avenida, perto da borda norte ou sul
                if (b.size.x > 150f && b.size.z < 3f && Mathf.Abs(b.center.z) > 40f)
                {
                    k.gameObject.SetActive(false); nMuro++;
                    Diz(string.Format(IC, "   colisor do muro desligado: {0}, {1:0} x {2:0.0} m em y {3:0.0}", k.gameObject.name, b.size.x, b.size.z, b.center.z));
                }
            }
            Diz($"muro de perimetro: {nMuro} colisores soltos desligados (a malha PA_Muro_Perimetro sai na lista acima)");
            // r16: os DECALQUES de desgaste (escorrido, sujeira) do muro e dos predios genericos que sairam ficavam flutuando no ar.
            // O muro ficava em y = -48,5 e +47,5: os decalques so ficam de -47,6 a +46,6 (o que esta dentro da linha do muro).
            foreach (var r in pa.GetComponentsInChildren<Renderer>(false))
            {
                // r21: PA_Vida_Detalhes (janelas, ar-condicionado e manchas que ficavam sobre o muro e os predios genericos que sairam)
                // tambem ficava solto no ar na linha do muro, na frente das fachadas novas dos dois lados: mesmo recorte.
                if (!r.gameObject.name.StartsWith("PA_Desgaste_Decalques") && !r.gameObject.name.StartsWith("PA_Vida_Detalhes")) continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var nm = RecortaRet(mf.sharedMesh, mf.transform.localToWorldMatrix, -PA_X1, -PA_X0, -47.6f, 46.6f, out int a, out int d);
                if (nm == null) continue;
                if (nm.vertexCount == 0) { r.gameObject.SetActive(false); Diz("decalques de desgaste: nada dentro da linha do muro; objeto desligado"); continue; }
                // r21: restos ALTOS junto a borda (manchas escorridas e janelas pintadas dos predios genericos que sairam): ficavam soltos no ar
                // na frente das fachadas novas. Sai o triangulo com o centro acima de 3 m e a mais de 40 m do eixo da praca.
                {
                    var l2w = mf.transform.localToWorldMatrix; var vv = nm.vertices; int fora = 0;
                    for (int s = 0; s < nm.subMeshCount; s++)
                    {
                        var tr = nm.GetTriangles(s); var fica = new List<int>(tr.Length);
                        for (int i = 0; i < tr.Length; i += 3)
                        {
                            var c = l2w.MultiplyPoint3x4((vv[tr[i]] + vv[tr[i + 1]] + vv[tr[i + 2]]) / 3f);
                            if (c.y > 3.0f && Mathf.Abs(c.z) > 40f) { fora++; continue; }
                            fica.Add(tr[i]); fica.Add(tr[i + 1]); fica.Add(tr[i + 2]);
                        }
                        nm.SetTriangles(fica, s);
                    }
                    if (fora > 0) Diz($"   {r.gameObject.name}: {fora} triangulos altos junto a borda retirados (restos dos predios genericos do mapa da praca)");
                }
                Grava(nm, "PAdecal_" + r.gameObject.name + "_" + (nRec++)); mf.sharedMesh = nm;
                Diz($"decalques de desgaste recortados para dentro da linha do muro: {a} -> {d} triangulos");
            }
            // r15d: BOCAS DE RUA. Malha grande (calcada, piso, sinalizacao, chao) perde o pedaco de dentro de cada boca; objeto pequeno
            // e colisor solto com o centro dentro da boca saem (ficariam no meio da rua).
            if (BOCAS.Count > 0)
            {
                int nCorte = 0, nSai = 0, nColB = 0, triFora = 0;
                foreach (var r in pa.GetComponentsInChildren<Renderer>(false))
                {
                    var b = r.bounds; if (!TocaBoca(b)) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    bool grande = b.size.x > 60f || b.size.z > 60f || r.gameObject.name == "PA_Chao";
                    if (!grande) { if (NaBoca(b.center)) { r.gameObject.SetActive(false); nSai++; } continue; }
                    if (!mf || !mf.sharedMesh) continue;
                    var nm = RecortaFora(mf.sharedMesh, mf.transform.localToWorldMatrix, BOCAS, out int a, out int d);
                    if (nm == null || a == d) continue;
                    Grava(nm, "PAboca_" + r.gameObject.name + "_" + (nRec++));
                    var mc = r.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nm;
                    mf.sharedMesh = nm; nCorte++; triFora += a - d;
                }
                foreach (var k in pa.GetComponentsInChildren<Collider>(false))
                {
                    if (k.GetComponent<Renderer>()) continue;
                    if (NaBoca(k.bounds.center)) { k.gameObject.SetActive(false); nColB++; }
                }
                Diz($"bocas de rua: {nCorte} malhas grandes recortadas (saldo de triangulos {-triFora}), {nSai} objetos e {nColB} colisores soltos tirados do meio da rua");
            }
            Diz("Praca das Aguas, desligado fora da zona mantida: " + string.Join(", ", fam.Select(kv => kv.Key + " x" + kv.Value)) + $"; colisores soltos {nCol}");
            Diz(string.Format(IC, "Praca das Aguas, recortado na zona (x {0}..{1}, |y| <= {2}): ", PA_X0, PA_X1, PA_Y1) + string.Join(", ", famCorte.Select(kv => kv.Key + " x" + kv.Value)));
            // objetos trocados a mao (FB_PropsTrocados) e a parada de onibus: fora da zona, saem
            foreach (var nomeR in new[] { "FB_PropsTrocados", "PARADA DE ONIBUS RR" })
            {
                var g = Raiz(cena, nomeR); if (!g) continue; int fora = 0;
                foreach (var r in g.GetComponentsInChildren<Renderer>(false))
                    if (!NaZonaPA(r.bounds.center)) { r.gameObject.SetActive(false); fora++; }
                foreach (var k in g.GetComponentsInChildren<Collider>(false))
                    if (!NaZonaPA(k.bounds.center)) { k.gameObject.SetActive(false); fora++; }
                Diz($"{nomeR}: {fora} pecas fora da zona desligadas");
            }

            // 4. chao: PA_Chao recortado na zona; ladrilhos do centro_civico recortados em x >= 530
            var chaoPA = pa.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.gameObject.name == "PA_Chao");
            if (chaoPA && chaoPA.sharedMesh)
            {
                var novo = RecortaRet(chaoPA.sharedMesh, chaoPA.transform.localToWorldMatrix, -PA_X1, -PA_X0, PA_Y0, PA_Y1, out int a, out int d);
                if (novo != null)
                {
                    Grava(novo, "PA_Chao_recortado");
                    var mc = chaoPA.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = novo;
                    chaoPA.sharedMesh = novo;
                    Diz($"PA_Chao recortado na zona mantida: {a} -> {d} triangulos" + (mc ? " (colisor de malha tambem)" : ""));
                }
            }
            else Diz("PA_Chao nao encontrado");
            // r22: FAIXA NORTE NA LARGURA REAL (autorizado pelo dono em 12/11/2026). No mapa da praca a guia da pista fica a 4,1 m do muro;
            // no real sao ~7,5 m (canteiro, ciclovia rebaixada, calcada). O chao da praca sai da faixa y -48,36..-40,45 entre as bocas de rua
            // e o marco BVC_PDA_FAIXA_NORTE poe o chao novo. O que ficava no antigo canteiro (postes com a fiacao, arvores, capim, placas,
            // motos encostadas na guia) vai 3,15 m para o lado da pista, para o canteiro novo. O que fica junto ao muro nao se mexe.
            {
                var faixa = new List<Vector4>(); const float FY0 = -48.36f, FY1 = -40.08f, BY0 = -45.5f, BY1 = -42.8f, DZ = 3.15f;
                foreach (var bl in new[] { new Vector2(-100f, -46.9f), new Vector2(-37.3f, 8.1f), new Vector2(18.8f, 64.2f), new Vector2(71.8f, 185.8f), new Vector2(194.0f, 333.2f) })
                    faixa.Add(new Vector4(-bl.y, -bl.x, FY0, FY1));
                // r22b: nas bocas de rua o chao da praca tambem sai (sobrava a ponta da calcada antiga barrando a ciclovia); o marco poe asfalto e a travessia
                var corte = new List<Vector4>(faixa);
                foreach (var bl in new[] { new Vector2(-46.9f, -37.3f), new Vector2(8.1f, 18.8f), new Vector2(64.2f, 71.8f), new Vector2(185.8f, 194.0f) })
                    corte.Add(new Vector4(-bl.y, -bl.x, FY0, FY1));
                System.Func<Vector3, bool> naBanda = w =>
                {
                    if (w.z < BY0 || w.z > BY1) return false;
                    foreach (var f in faixa) if (w.x >= f.x && w.x <= f.y) return true;
                    return false;
                };
                int nCorteF = 0, triF = 0, nMov = 0, nVert = 0, nColF = 0, nArv = 0; var movidos = new HashSet<Transform>();
                foreach (var r in pa.GetComponentsInChildren<Renderer>(false))
                {
                    var b = r.bounds; bool toca = false;
                    foreach (var f in corte) if (b.max.x > f.x && b.min.x < f.y && b.max.z > f.z && b.min.z < f.w) { toca = true; break; }
                    if (!toca) continue;
                    string nm0 = r.gameObject.name; var mf = r.GetComponent<MeshFilter>();
                    bool chao = nm0.StartsWith("PA_Chao") || nm0.StartsWith("PA_Sinalizacao") || nm0.StartsWith("PA_Desgaste_Decalques") || nm0.StartsWith("PA_Acessibilidade");
                    bool grande = b.size.x > 60f || b.size.z > 60f;
                    if (chao)
                    {
                        if (!mf || !mf.sharedMesh) continue;
                        var nm = RecortaFora(mf.sharedMesh, mf.transform.localToWorldMatrix, corte, out int a, out int d);
                        if (nm == null || a == d) continue;
                        Grava(nm, "PAfaixa_" + nm0 + "_" + (nRec++));
                        var mc = r.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nm;
                        mf.sharedMesh = nm; nCorteF++; triF += a - d;
                    }
                    else if (grande)
                    {
                        if (!mf || !mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                        var l2w = mf.transform.localToWorldMatrix; var w2l = l2w.inverse; var vs = mf.sharedMesh.vertices; int n0 = 0;
                        for (int i = 0; i < vs.Length; i++)
                        {
                            var w = l2w.MultiplyPoint3x4(vs[i]); if (!naBanda(w)) continue;
                            w.z += DZ; vs[i] = w2l.MultiplyPoint3x4(w); n0++;
                        }
                        if (n0 == 0) continue;
                        var nm = UnityEngine.Object.Instantiate(mf.sharedMesh); nm.name = mf.sharedMesh.name + "_faixa"; nm.vertices = vs; nm.RecalculateBounds();
                        Grava(nm, "PAfaixa_" + nm0 + "_" + (nRec++));
                        var mc = r.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nm;
                        mf.sharedMesh = nm; nVert += n0;
                    }
                    else
                    {
                        var t = r.transform; while (t.parent != null && t.parent != pa.transform) t = t.parent;
                        if (movidos.Contains(t) || !naBanda(b.center)) continue;
                        // r23: no video nao ha nenhuma arvore no canteiro norte; o dono mandou tirar as arvores de rua do mapa da praca que ficavam ali
                        if (t.name.StartsWith("PA_Arvore_Rua")) { t.gameObject.SetActive(false); movidos.Add(t); nArv++; continue; }
                        t.position += new Vector3(0f, 0f, DZ); movidos.Add(t); nMov++;
                    }
                }
                foreach (var k in pa.GetComponentsInChildren<Collider>(false))
                {
                    if (k.GetComponent<Renderer>()) continue;
                    var kb = k.bounds; if (kb.size.x > 1.5f || kb.size.z > 1.5f) continue;      // conteiner, cacamba: fica onde esta
                    var t = k.transform; while (t.parent != null && t.parent != pa.transform) t = t.parent;
                    if (movidos.Contains(t) || !naBanda(kb.center)) continue;
                    t.position += new Vector3(0f, 0f, DZ); movidos.Add(t); nColF++;
                }
                Diz(string.Format(IC, "faixa norte (r22): {0} malhas de chao recortadas ({1} triangulos a menos); {2} objetos e {3} colisores soltos levados {4:0.00} m para o canteiro novo; {5} vertices de malhas juntadas (placas, mobiliario) deslocados; {6} arvores de rua desligadas (r23)",
                                  nCorteF, triF, nMov, nColF, DZ, nVert, nArv));
            }
            var chaoCC = cc.transform.Find("Chao");
            if (chaoCC)
            {
                int rel = 0, cortados = 0, desl = 0;
                foreach (var mf in chaoCC.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh) continue;
                    var b = CaixaMundo(mf); float xmin = -b.max.x, xmax = -b.min.x;
                    if (xmax <= COSTURA) { if (mf.gameObject.activeSelf) { mf.gameObject.SetActive(false); desl++; } continue; }
                    if (xmin >= COSTURA) { if (!mf.gameObject.activeSelf) { mf.gameObject.SetActive(true); rel++; } continue; }
                    var novo = RecortaRet(mf.sharedMesh, mf.transform.localToWorldMatrix, -1e6f, -COSTURA, -1e6f, 1e6f, out int a, out int d);
                    if (novo == null) continue;
                    Grava(novo, "CC_" + mf.gameObject.name + "_x530");
                    var mc = mf.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = novo;
                    mf.sharedMesh = novo; cortados++;
                    if (!mf.gameObject.activeSelf) { mf.gameObject.SetActive(true); rel++; }
                }
                Diz($"chao do centro_civico na costura: {cortados} ladrilhos recortados em x >= {COSTURA}, {rel} religados, {desl} desligados");
            }

            // 4b. colisores de PISO do centro_civico que a costura da rodada 7 desligou inteiros e que passam de x = 530:
            //     sem eles o chao religado acima fica sem colisao. Voltam, recortados em x >= 530. Parede e predio nao.
            var colCC = cc.transform.Find("Colisao");
            if (colCC)
            {
                int relc = 0, cortc = 0;
                foreach (var mc in colCC.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (mc.gameObject.activeSelf || !mc.sharedMesh) continue;
                    var m = mc.sharedMesh; var l2w = mc.transform.localToWorldMatrix; var bb = m.bounds;
                    var wb = new Bounds(l2w.MultiplyPoint3x4(bb.center), Vector3.zero);
                    for (int i = 0; i < 8; i++)
                        wb.Encapsulate(l2w.MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                    float xmin = -wb.max.x, xmax = -wb.min.x;
                    if (xmax <= COSTURA + 0.5f || wb.size.y > 1.2f) continue;
                    if (xmin < COSTURA)
                    {
                        var novo = RecortaRet(m, l2w, -1e6f, -COSTURA, -1e6f, 1e6f, out _, out _);
                        if (novo == null) continue;
                        Grava(novo, "CCcol_" + mc.gameObject.name + "_x530_" + cortc); mc.sharedMesh = novo; cortc++;
                    }
                    mc.gameObject.SetActive(true); relc++;
                }
                Diz($"colisores de piso do centro_civico na costura: {relc} religados ({cortc} recortados em x >= {COSTURA})");
            }

            // 5. marcos
            var mj = JsonUtility.FromJson<MarcosJ>(File.ReadAllText(Saida("marcos_cena.json")));
            foreach (var m in mj.marcos)
            {
                var go = Raiz(cena, m.id);
                if (!go)
                {
                    // r15: marco NOVO (nao existia na cena-base da rodada 7): nasce do GLB, se ele existir
                    if (!m.ativo || !AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/BURITIRO_Mapas/bv_city/marcos/{m.id}.glb")) { Diz("marco fora da cena e sem GLB: " + m.id); continue; }
                    go = new GameObject(m.id); go.transform.position = Mundo(m.x, m.y, m.h);
                    Diz("marco novo, criado a partir do GLB: " + m.id);
                }
                if (!m.ativo)
                {
                    go.SetActive(false);
                    Diz(m.id + " DESLIGADO: o modelo nao corresponde ao predio (planta de outro lote) e estava no lugar errado. Refazer pelo contorno certo.");
                    continue;
                }
                var antes = go.transform.position; float giro0 = go.transform.eulerAngles.y;
                // r12: o marco e instanciado de novo a partir do GLB (o modelo pode ter sido regerado: pecas com outro nome
                // nao existiriam na copia desempacotada que a cena guardava desde a rodada 7)
                var glb = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/BURITIRO_Mapas/bv_city/marcos/{m.id}.glb");
                if (glb)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                    go = (GameObject)PrefabUtility.InstantiatePrefab(glb, cena);
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    go.name = m.id;
                }
                else Diz("   GLB do marco nao encontrado; ficou a copia que a cena tinha: " + m.id);
                go.transform.SetPositionAndRotation(Mundo(m.x, m.y, m.h), Quaternion.Euler(0, m.giro, 0));
                int lig = 0, col = 0, deg = 0;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    if (!t.gameObject.activeSelf) { t.gameObject.SetActive(true); lig++; }
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                {
                    mf.gameObject.layer = LAYER_MAPA;
                    if (mf.sharedMesh && !mf.GetComponent<Collider>())
                    {
                        var bs = mf.sharedMesh.bounds.size;      // r25c: peca sem volume (duas dimensoes nulas) nao tem como virar colisor
                        if ((bs.x < 1e-3f ? 1 : 0) + (bs.y < 1e-3f ? 1 : 0) + (bs.z < 1e-3f ? 1 : 0) >= 2) { deg++; continue; }
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; col++;
                    }
                }
                Diz(string.Format(IC, "{0}: pivo corredor ({1:0.00}, {2:0.00}, h {3:0.00}) giro {4:0.0} -> ({5:0.00}, {6:0.00}, h {7:0.00}) giro {8:0.0}; objetos religados {9}; colisores novos {10}; pecas degeneradas (sem colisor) {11}",
                                  m.id, -antes.x, antes.z, antes.y, giro0, m.x, m.y, m.h, m.giro, lig, col, deg));
                try { Demole(cc, go, m.casco); } catch (Exception e) { Diz("   demolicao falhou: " + e.Message); }
                // r15e: segundo contorno do mesmo marco (peca solta do conjunto, por exemplo o ponto de taxi do terminal)
                if (m.casco2 != null && m.casco2.Length >= 6) { try { Demole(cc, go, m.casco2); } catch (Exception e) { Diz("   demolicao (2) falhou: " + e.Message); } }
                if (m.casco3 != null && m.casco3.Length >= 6) { try { Demole(cc, go, m.casco3); } catch (Exception e) { Diz("   demolicao (3) falhou: " + e.Message); } }
                // r31: 'chao_fora' = retangulos do corredor (x0, x1, y0, y1, ...) onde o marco traz o chao MEDIDO: ali sai do mapa do parceiro tudo, inclusive o chao
                if (m.chao_fora != null && m.chao_fora.Length >= 4) { try { CortaChaoParceiro(cc, m.id, m.chao_fora); } catch (Exception e) { Diz("   corte do chao do parceiro falhou: " + e.Message); } }
            }

            // r26: arvores do lado sul, antes do NavMesh (o tronco entra como obstaculo)
            try { PlantaArvoresSul(); } catch (Exception e) { Diz("arvores do lado sul falharam: " + e.Message); }
            Physics.SyncTransforms();
            // 6. NavMesh do corredor
            try
            {
                var mi = typeof(FBMontaBVCity).GetMethod("AssaNavMesh", BindingFlags.NonPublic | BindingFlags.Static);
                mi.Invoke(null, null); Diz("NavMesh do corredor assado de novo (ver linhas [BVCITY] no log do Unity)");
            }
            catch (Exception e) { Diz("NavMesh falhou: " + (e.InnerException ?? e).Message); }
            try
            {
                var data = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>("Assets/BURITIRO_Mapas/bv_city/NavMesh_bv_city.asset");
                var hnd = UnityEngine.AI.NavMesh.AddNavMeshData(data);
                int ok = 0, n = 0; var fora = new List<string>();
                foreach (var ln in File.ReadAllLines(Saida("fbdata_pontos.txt")))
                {
                    var c = ln.Split(' '); if (c.Length < 4) continue;
                    var pos = new Vector3(float.Parse(c[1], IC), float.Parse(c[2], IC), float.Parse(c[3], IC)); n++;
                    if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var nh, 1.5f, UnityEngine.AI.NavMesh.AllAreas) && Mathf.Abs(nh.position.y - pos.y) < 1.0f) ok++;
                    else if (fora.Count < 15) fora.Add(c[0]);
                }
                if (hnd.valid) UnityEngine.AI.NavMesh.RemoveNavMeshData(hnd);
                Diz($"pontos do fb_data (bv_city) a menos de 1,5 m do NavMesh novo e a menos de 1 m de altura dele: {ok} de {n}" + (fora.Count > 0 ? "; fora: " + string.Join(", ", fora) : ""));
            }
            catch (Exception e) { Diz("conferencia dos pontos do fb_data falhou: " + e.Message); }

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Diz("cena gravada: " + CENA);
        }

        static void Grava(Mesh m, string nome)
        {
            string p = $"{REC_DIR}/{Regex.Replace(nome, @"[^A-Za-z0-9_\-]", "_")}.asset";
            if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(m, p);
        }

        // ------------------------------------------------------------------ recorte de malha por retangulo (XZ do mundo)
        struct V { public Vector3 p, n; public Vector2 uv, uv2; public Color c; }

        static List<V> Corta(List<V> pol, int eixo, float lim, bool maior)
        {
            var o = new List<V>(pol.Count + 2);
            for (int i = 0; i < pol.Count; i++)
            {
                var a = pol[i]; var b = pol[(i + 1) % pol.Count];
                float da = (eixo == 0 ? a.p.x : a.p.z) - lim, db = (eixo == 0 ? b.p.x : b.p.z) - lim;
                bool ina = maior ? da >= 0 : da <= 0, inb = maior ? db >= 0 : db <= 0;
                if (ina) o.Add(a);
                if (ina != inb)
                {
                    float t = da / (da - db);
                    o.Add(new V { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t), uv2 = Vector2.Lerp(a.uv2, b.uv2, t), c = Color.Lerp(a.c, b.c, t) });
                }
            }
            return o;
        }

        static Mesh RecortaRet(Mesh m, Matrix4x4 l2w, float X0, float X1, float Z0, float Z1, out int antes, out int depois)
        {
            antes = depois = 0;
            if (!m.isReadable) { Diz($"   malha {m.name} nao e legivel; recorte pulado"); return null; }
            var v = m.vertices; var n = m.normals; var uv = m.uv; var uv2 = m.uv2; var cor = m.colors;
            bool temN = n.Length == v.Length, temUV = uv.Length == v.Length, temUV2 = uv2.Length == v.Length, temC = cor.Length == v.Length;
            var w2l = l2w.inverse;
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var nc = new List<Color>(); var subs = new List<List<int>>();
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var tr = m.GetTriangles(s); var saida = new List<int>(); antes += tr.Length / 3;
                for (int i = 0; i < tr.Length; i += 3)
                {
                    var pol = new List<V>(3);
                    for (int k = 0; k < 3; k++)
                    {
                        int a = tr[i + k];
                        pol.Add(new V { p = l2w.MultiplyPoint3x4(v[a]), n = temN ? n[a] : Vector3.up, uv = temUV ? uv[a] : Vector2.zero, uv2 = temUV2 ? uv2[a] : Vector2.zero, c = temC ? cor[a] : Color.white });
                    }
                    pol = Corta(pol, 0, X0, true); if (pol.Count >= 3) pol = Corta(pol, 0, X1, false);
                    if (pol.Count >= 3) pol = Corta(pol, 2, Z0, true); if (pol.Count >= 3) pol = Corta(pol, 2, Z1, false);
                    if (pol.Count < 3) continue;
                    int b0 = nv.Count;
                    foreach (var q in pol) { nv.Add(w2l.MultiplyPoint3x4(q.p)); nn.Add(q.n); nuv.Add(q.uv); nuv2.Add(q.uv2); nc.Add(q.c); }
                    for (int k = 1; k + 1 < pol.Count; k++) { saida.Add(b0); saida.Add(b0 + k); saida.Add(b0 + k + 1); depois++; }
                }
                subs.Add(saida);
            }
            var r = new Mesh { name = m.name + "_recortado", indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            r.SetVertices(nv); if (temN) r.SetNormals(nn); if (temUV) r.SetUVs(0, nuv); if (temUV2) r.SetUVs(1, nuv2); if (temC) r.SetColors(nc);
            r.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) r.SetTriangles(subs[s], s);
            r.RecalculateBounds(); if (!temN) r.RecalculateNormals();
            if (temN && temUV && m.tangents.Length == v.Length) r.RecalculateTangents();
            return r;
        }

        /// <summary>r15d: tira da malha o que cai DENTRO de qualquer um dos retangulos (Xmin, Xmax, Zmin, Zmax do mundo), em toda altura.
        /// O triangulo e partido em ate quatro pedacos por retangulo (oeste, leste, norte, sul). Guarda posicao, normal, uv, uv2 e cor.</summary>
        static Mesh RecortaFora(Mesh m, Matrix4x4 l2w, List<Vector4> bur, out int antes, out int depois)
        {
            antes = depois = 0;
            if (!m.isReadable) { Diz($"   malha {m.name} nao e legivel; recorte de boca pulado"); return null; }
            var v = m.vertices; var n = m.normals; var uv = m.uv; var uv2 = m.uv2; var cor = m.colors;
            bool temN = n.Length == v.Length, temUV = uv.Length == v.Length, temUV2 = uv2.Length == v.Length, temC = cor.Length == v.Length;
            var w2l = l2w.inverse; var w = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) w[i] = l2w.MultiplyPoint3x4(v[i]);
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var nc = new List<Color>(); var subs = new List<List<int>>();
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var tr = m.GetTriangles(s); var saida = new List<int>(); antes += tr.Length / 3;
                for (int i = 0; i < tr.Length; i += 3)
                {
                    var pol = new List<V>(3); float xa = float.MaxValue, xb = float.MinValue, za = float.MaxValue, zb = float.MinValue;
                    for (int k = 0; k < 3; k++)
                    {
                        int a = tr[i + k]; var p = w[a];
                        xa = Mathf.Min(xa, p.x); xb = Mathf.Max(xb, p.x); za = Mathf.Min(za, p.z); zb = Mathf.Max(zb, p.z);
                        pol.Add(new V { p = p, n = temN ? n[a] : Vector3.up, uv = temUV ? uv[a] : Vector2.zero, uv2 = temUV2 ? uv2[a] : Vector2.zero, c = temC ? cor[a] : Color.white });
                    }
                    var pecas = new List<List<V>> { pol };
                    foreach (var b in bur)
                    {
                        if (xb <= b.x || xa >= b.y || zb <= b.z || za >= b.w) continue;          // o triangulo nem encosta nesta boca
                        var prox = new List<List<V>>();
                        foreach (var q in pecas)
                        {
                            var oeste = Corta(q, 0, b.x, false); if (oeste.Count >= 3) prox.Add(oeste);
                            var resto = Corta(q, 0, b.x, true); if (resto.Count < 3) continue;
                            var leste = Corta(resto, 0, b.y, true); if (leste.Count >= 3) prox.Add(leste);
                            var meio = Corta(resto, 0, b.y, false); if (meio.Count < 3) continue;
                            var norte = Corta(meio, 2, b.z, false); if (norte.Count >= 3) prox.Add(norte);
                            var sul = Corta(meio, 2, b.w, true); if (sul.Count >= 3) prox.Add(sul);
                        }
                        pecas = prox; if (pecas.Count == 0) break;
                    }
                    foreach (var q in pecas)
                    {
                        int b0 = nv.Count;
                        foreach (var e in q) { nv.Add(w2l.MultiplyPoint3x4(e.p)); nn.Add(e.n); nuv.Add(e.uv); nuv2.Add(e.uv2); nc.Add(e.c); }
                        for (int k = 1; k + 1 < q.Count; k++)
                        {
                            if (Vector3.Cross(q[k].p - q[0].p, q[k + 1].p - q[0].p).sqrMagnitude < 1e-12f) continue;     // lasca sem area
                            saida.Add(b0); saida.Add(b0 + k); saida.Add(b0 + k + 1); depois++;
                        }
                    }
                }
                subs.Add(saida);
            }
            var r = new Mesh { name = m.name + "_boca", indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            r.SetVertices(nv); if (temN) r.SetNormals(nn); if (temUV) r.SetUVs(0, nuv); if (temUV2) r.SetUVs(1, nuv2); if (temC) r.SetColors(nc);
            r.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) r.SetTriangles(subs[s], s);
            r.RecalculateBounds(); if (!temN) r.RecalculateNormals();
            if (temN && temUV && m.tangents.Length == v.Length) r.RecalculateTangents();
            return r;
        }

        /// <summary>r31: tira do mapa do parceiro (so na cena do BV City; o mapa original nao e alterado) TUDO o que cai dentro dos retangulos
        /// dados no corredor (x0, x1, y0, y1, ...): chao, guia, pintura, colisor e objeto, em qualquer altura, com o triangulo partido na borda.
        /// Serve para o marco que traz o chao medido de uma rua (o Demole nao mexe em piso).</summary>
        static void CortaChaoParceiro(GameObject cc, string id, float[] r)
        {
            var bur = new List<Vector4>();
            for (int i = 0; i + 3 < r.Length; i += 4)
                bur.Add(new Vector4(Mathf.Min(-r[i], -r[i + 1]), Mathf.Max(-r[i], -r[i + 1]), Mathf.Min(r[i + 2], r[i + 3]), Mathf.Max(r[i + 2], r[i + 3])));
            int nm = 0, nc = 0, antes = 0, depois = 0, vazios = 0, k = 0; var feitos = new Dictionary<Mesh, Mesh>();
            foreach (var t in cc.GetComponentsInChildren<Transform>(true))
            {
                var mf = t.GetComponent<MeshFilter>(); var mc = t.GetComponent<MeshCollider>();
                foreach (bool colisor in new[] { false, true })
                {
                    Mesh me = colisor ? (mc ? mc.sharedMesh : null) : (mf ? mf.sharedMesh : null); if (!me) continue;
                    if (colisor && mf && mf.sharedMesh == me) continue;                 // mesma malha do desenho: ja tratada (e reatribuida) na volta anterior
                    var l2w = t.localToWorldMatrix; var bb = me.bounds; var wb = new Bounds(l2w.MultiplyPoint3x4(bb.center), Vector3.zero);
                    for (int i = 0; i < 8; i++) wb.Encapsulate(l2w.MultiplyPoint3x4(bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                    bool toca = false; foreach (var b in bur) if (!(wb.max.x <= b.x || wb.min.x >= b.y || wb.max.z <= b.z || wb.min.z >= b.w)) toca = true;
                    if (!toca) continue;
                    if (!feitos.TryGetValue(me, out var nv))
                    {
                        nv = RecortaFora(me, l2w, bur, out int a, out int d); if (nv == null) continue;
                        antes += a; depois += d; Grava(nv, "fora_" + id + "_" + t.name + "_" + (k++)); feitos[me] = nv;
                    }
                    if (colisor) { mc.sharedMesh = nv; nc++; }
                    else { mf.sharedMesh = nv; nm++; if (mc && mc.sharedMesh == me) { mc.sharedMesh = nv; nc++; } }
                    if (nv.triangles.Length == 0) vazios++;
                }
            }
            Diz($"   chao do parceiro retirado debaixo de {id}: {bur.Count} retangulo(s); {nm} malhas e {nc} colisores recortados ({antes} -> {depois} triangulos); {vazios} ficaram vazios");
        }

        // ------------------------------------------------------------------ versao antiga do predio debaixo do marco
        static List<Vector2> Casco(List<Vector2> pts)
        {
            var p = pts.Distinct().OrderBy(a => a.x).ThenBy(a => a.y).ToList();
            if (p.Count < 3) return p;
            float Cr(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            var h = new List<Vector2>();
            foreach (var q in p) { while (h.Count >= 2 && Cr(h[h.Count - 2], h[h.Count - 1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
            int t = h.Count + 1;
            for (int i = p.Count - 2; i >= 0; i--) { var q = p[i]; while (h.Count >= t && Cr(h[h.Count - 2], h[h.Count - 1], q) <= 0) h.RemoveAt(h.Count - 1); h.Add(q); }
            h.RemoveAt(h.Count - 1);
            return h;                                   // anti-horario
        }

        /// <summary>dentro do poligono (qualquer forma) ou a menos de 'margem' da borda dele</summary>
        static bool NoPoligono(List<Vector2> pol, Vector2 q, float margem)
        {
            bool dentro = false; float dmin = float.MaxValue;
            for (int i = 0, j = pol.Count - 1; i < pol.Count; j = i++)
            {
                var a = pol[i]; var b = pol[j];
                if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) dentro = !dentro;
                var e = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, e) / Mathf.Max(e.sqrMagnitude, 1e-9f));
                dmin = Mathf.Min(dmin, (q - (a + e * t)).magnitude);
            }
            return dentro || dmin <= margem;
        }

        static bool NoCasco(List<Vector2> h, Vector2 q, float margem)
        {
            for (int i = 0; i < h.Count; i++)
            {
                var a = h[i]; var b = h[(i + 1) % h.Count]; var e = b - a; float L = e.magnitude; if (L < 1e-6f) continue;
                float d = (e.x * (q.y - a.y) - e.y * (q.x - a.x)) / L;      // distancia com sinal: positiva = dentro
                if (d < -margem) return false;
            }
            return true;
        }

        /// <summary>Tira do mapa do parceiro o que esta DEBAIXO do marco: objeto inteiro quando cabe no casco do predio,
        /// senao so os triangulos cujo centro cai nele. O chao (grupo Chao) nao e tocado.</summary>
        static int nSem;

        /// <summary>Copia da malha sem o predio antigo. Triangulo de PISO (baixo e deitado) fica sempre. Dos demais sai:
        /// (a) o que tem o centro dentro do contorno; (b) o COMPONENTE LIGADO inteiro (triangulos que partilham vertice)
        /// quando 30 % ou mais dele esta dentro - assim o predio antigo sai inteiro mesmo sendo maior que o real.</summary>
        static Mesh TiraPredio(Mesh m, Matrix4x4 l2w, float baseY, Func<Vector2, bool> dentro, out int tirou)
        {
            var v = m.vertices; int nv = v.Length; tirou = 0;
            var pai = new int[nv]; for (int i = 0; i < nv; i++) pai[i] = i;
            int Acha(int i) { while (pai[i] != i) { pai[i] = pai[pai[i]]; i = pai[i]; } return i; }
            void Une(int i, int j) { i = Acha(i); j = Acha(j); if (i != j) pai[i] = j; }
            var porPos = new Dictionary<Vector3Int, int>(nv);
            for (int i = 0; i < nv; i++)
            {
                var k = Vector3Int.RoundToInt(v[i] * 1000f);
                if (porPos.TryGetValue(k, out int j)) Une(i, j); else porPos[k] = i;
            }
            var subs = new List<int[]>(); var chao = new List<bool[]>(); var den = new List<bool[]>();
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var tr = m.GetTriangles(s); int nt = tr.Length / 3; var ch = new bool[nt]; var de = new bool[nt];
                for (int q = 0; q < nt; q++)
                {
                    int i = q * 3; Une(tr[i], tr[i + 1]); Une(tr[i], tr[i + 2]);
                    var pa = l2w.MultiplyPoint3x4(v[tr[i]]); var pb = l2w.MultiplyPoint3x4(v[tr[i + 1]]); var pc = l2w.MultiplyPoint3x4(v[tr[i + 2]]);
                    var c = (pa + pb + pc) / 3f; var nrm = Vector3.Cross(pb - pa, pc - pa).normalized;
                    ch[q] = c.y - baseY < 0.6f && Mathf.Abs(nrm.y) > 0.7f;
                    de[q] = !ch[q] && dentro(new Vector2(c.x, c.z));
                }
                subs.Add(tr); chao.Add(ch); den.Add(de);
            }
            var tot = new Dictionary<int, int>(); var nd = new Dictionary<int, int>();
            for (int s = 0; s < subs.Count; s++)
                for (int q = 0; q < chao[s].Length; q++)
                {
                    if (chao[s][q]) continue;
                    int c = Acha(subs[s][q * 3]);
                    tot[c] = tot.TryGetValue(c, out int a) ? a + 1 : 1;
                    if (den[s][q]) nd[c] = nd.TryGetValue(c, out int b) ? b + 1 : 1;
                }
            var novo = UnityEngine.Object.Instantiate(m); novo.name = m.name + "_semMarco";
            for (int s = 0; s < subs.Count; s++)
            {
                var tr = subs[s]; var fica = new List<int>(tr.Length);
                for (int q = 0; q < chao[s].Length; q++)
                {
                    bool sai = false;
                    if (!chao[s][q])
                    {
                        int c = Acha(tr[q * 3]);
                        sai = den[s][q] || (nd.TryGetValue(c, out int d) && d >= 0.3f * tot[c]);
                    }
                    if (sai) { tirou++; continue; }
                    fica.Add(tr[q * 3]); fica.Add(tr[q * 3 + 1]); fica.Add(tr[q * 3 + 2]);
                }
                novo.SetTriangles(fica, s);
            }
            return novo;
        }
        static void Demole(GameObject cc, GameObject marco, float[] contorno)
        {
            float baseY = marco.transform.position.y;
            var pts = new List<Vector2>();
            if (contorno != null) for (int i = 0; i + 1 < contorno.Length; i += 2) pts.Add(new Vector2(-contorno[i], contorno[i + 1]));   // contorno do cadastro (corredor -> Unity)
            foreach (var r in marco.GetComponentsInChildren<Renderer>(false))
            {
                var b = r.bounds; if (b.max.y - baseY < 2.5f) continue;       // adro, degrau, canteiro: nao contam como predio
                pts.Add(new Vector2(b.min.x, b.min.z)); pts.Add(new Vector2(b.max.x, b.min.z));
                pts.Add(new Vector2(b.max.x, b.max.z)); pts.Add(new Vector2(b.min.x, b.max.z));
            }
            var h = Casco(pts);
            if (h.Count < 3) { Diz("   sem casco para " + marco.name); return; }
            const float MARGEM = 3f;
            bool usaContorno = contorno != null && contorno.Length >= 6;
            if (usaContorno) { h = new List<Vector2>(); for (int i = 0; i + 1 < contorno.Length; i += 2) h.Add(new Vector2(-contorno[i], contorno[i + 1])); }
            bool Dentro(Vector2 q) => usaContorno ? NoPoligono(h, q, MARGEM) : NoCasco(h, q, MARGEM);
            var cx = new Bounds(new Vector3(h[0].x, 0, h[0].y), Vector3.zero);
            foreach (var q in h) cx.Encapsulate(new Vector3(q.x, 0, q.y)); cx.Expand(new Vector3(2 * MARGEM, 1e4f, 2 * MARGEM));
            int inteiros = 0, cirurgia = 0, triFora = 0, cols = 0, triCol = 0;
            var nomes = new List<string>();
            foreach (var grupo in new[] { "Celulas", "Grandes", "Colisao" })
            {
                var g = cc.transform.Find(grupo); if (!g) continue;
                foreach (var t in g.GetComponentsInChildren<Transform>(false))
                {
                    var mf = t.GetComponent<MeshFilter>(); var mc = t.GetComponent<MeshCollider>();
                    Mesh m = mf ? mf.sharedMesh : (mc ? mc.sharedMesh : null);
                    if (!m) continue;
                    var wb = mf ? CaixaMundo(mf) : mc.bounds;
                    if (!wb.Intersects(cx)) continue;
                    bool todo = Dentro(new Vector2(wb.min.x, wb.min.z)) && Dentro(new Vector2(wb.max.x, wb.min.z)) &&
                                Dentro(new Vector2(wb.max.x, wb.max.z)) && Dentro(new Vector2(wb.min.x, wb.max.z));
                    if (todo && wb.max.y - baseY > 0.8f) { t.gameObject.SetActive(false); inteiros++; if (nomes.Count < 6) nomes.Add(t.name); continue; }
                    if (!m.isReadable) { Diz($"   {t.name}: malha nao legivel, ficou como estava"); continue; }
                    var novo = TiraPredio(m, t.localToWorldMatrix, baseY, Dentro, out int tirou);
                    if (tirou == 0) { UnityEngine.Object.DestroyImmediate(novo); continue; }
                    Grava(novo, "sem_" + marco.name + "_" + t.name + "_" + (nSem++));
                    if (mf) { mf.sharedMesh = novo; cirurgia++; triFora += tirou; }
                    if (mc) { mc.sharedMesh = novo; cols++; triCol += tirou; }
                }
            }
            Diz($"   debaixo de {marco.name}: {inteiros} objetos do parceiro desligados inteiros ({string.Join(", ", nomes)}), " +
                $"{cirurgia} malhas com {triFora} triangulos retirados, {cols} colisores com {triCol} triangulos retirados");
        }

        // ------------------------------------------------------------------ fotos
        static void Persp(Vector3 pos, Vector3 alvo, float fov, string nome, int W = 1600, int H = 900)
        {
            bool fog0 = RenderSettings.fog; RenderSettings.fog = false;
            var go = new GameObject("CamBVPersp");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov; cam.nearClipPlane = 0.3f; cam.farClipPlane = 6000f; cam.allowHDR = false; cam.allowMSAA = false;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.62f, .74f, .86f);
            go.transform.position = pos; go.transform.LookAt(alvo);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.aspect = (float)W / H;
            for (int i = 0; i < 2; i++) cam.Render();
            var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
            RenderTexture.active = rt; tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
            File.WriteAllBytes(Saida(nome + ".png"), tx.EncodeToPNG());
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(go); rt.Release(); UnityEngine.Object.DestroyImmediate(tx);
            RenderSettings.fog = fog0;
        }

        static void Fotos()
        {
            var cena = SceneManager.GetActiveScene();
            var planta = Raiz(cena, "BVC_Planta");
            var st = planta ? planta.GetComponent<FreeBala.FBBVCitySetores>() : null;
            if (!st) { Diz("fotos: cena sem BVC_Planta"); return; }
            // todos os setores, so para as fotos (nao sao gravados na cena)
            var tmp = new GameObject("BVC_Planta_FOTOS") { hideFlags = HideFlags.DontSave };
            float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f, z1 = -1e9f; int n = 0;
            foreach (var s in st.setores)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{RES_DIR}/{s.id}.prefab");
                if (!pf) continue;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, tmp.transform); g.hideFlags = HideFlags.DontSave; n++;
                x0 = Mathf.Min(x0, s.min.x); x1 = Mathf.Max(x1, s.max.x); z0 = Mathf.Min(z0, s.min.y); z1 = Mathf.Max(z1, s.max.y);
            }
            Diz(string.Format(IC, "fotos: {0} setores instanciados; planta ocupa Unity X {1:0}..{2:0}, Z {3:0}..{4:0} (corredor x {5:0}..{6:0}, y {3:0}..{4:0})",
                              n, x0, x1, z0, z1, -x1, -x0));
            Physics.SyncTransforms();

            // de cima: perimetro inteiro a 2 m por pixel, corredor a 0,5 m por pixel
            int W = Mathf.RoundToInt((x1 - x0) / 2f), H = Mathf.RoundToInt((z1 - z0) / 2f);
            Foto(new Vector3((x0 + x1) / 2, 700f, (z0 + z1) / 2), W, H, 1f, 900f, Saida("r11_perimetro.png"), new Color(.07f, .08f, .10f), 2f);
            File.WriteAllText(Saida("r11_perimetro.json"), string.Format(IC,
                "{{\n \"m_por_pixel\": 2,\n \"corredor_x\": [{0}, {1}],\n \"corredor_y\": [{2}, {3}],\n \"pixel\": \"coluna i -> x = corredor_x[1] - (i + 0.5) * 2 ; linha j (de cima) -> y = corredor_y[1] - (j + 0.5) * 2\"\n}}\n",
                -x1, -x0, z0, z1));
            Foto(Mundo(500f, -25f, 600f), 3200, 1900, 1f, 800f, Saida("r11_corredor.png"), new Color(.07f, .08f, .10f), 0.5f);
            File.WriteAllText(Saida("r11_corredor.json"),
                "{\n \"m_por_pixel\": 0.5,\n \"corredor_x\": [-300, 1300],\n \"corredor_y\": [-500, 450],\n \"pixel\": \"coluna i -> x = 1300 - (i + 0.5) * 0.5 ; linha j (de cima) -> y = 450 - (j + 0.5) * 0.5\"\n}\n");

            // chao sem buraco: raio de cima para baixo na camada do mapa, grade de 4 m na janela do corredor
            {
                int nx = 400, ny = 238, semChao = 0, n2 = 0; var mapa = new Texture2D(nx, ny, TextureFormat.RGB24, false);
                for (int j = 0; j < ny; j++)
                    for (int i = 0; i < nx; i++)
                    {
                        float x = -300f + (i + 0.5f) * 4f, y = -500f + (j + 0.5f) * 4f; n2++;
                        bool ok = Physics.Raycast(Mundo(x, y, 300f), Vector3.down, out var hit, 400f, 1 << LAYER_MAPA, QueryTriggerInteraction.Ignore);
                        if (!ok) semChao++;
                        float g = ok ? Mathf.Clamp01((hit.point.y + 3f) / 20f) : 0f;
                        mapa.SetPixel(nx - 1 - i, j, ok ? new Color(g, g, g) : Color.red);
                    }
                mapa.Apply(); File.WriteAllBytes(Saida("r11_chao_colisor.png"), mapa.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(mapa);
                Diz(string.Format(IC, "chao (colisor) na janela x -300..1300, y -500..450, grade de 4 m: {0} pontos, {1} sem nada embaixo ({2:0.00} %); mapa em r11_chao_colisor.png (vermelho = sem colisor)",
                                  n2, semChao, 100f * semChao / n2));
            }

            // r21: diagnostico das pecas soltas no ar atras da frente sul (com os setores carregados): o que ha acima de 4,5 m nessa caixa
            {
                Vector3 a = Mundo(-140f, 40f, 0f), b = Mundo(-60f, 95f, 0f);
                float bx0 = Mathf.Min(a.x, b.x), bx1 = Mathf.Max(a.x, b.x), bz0 = Mathf.Min(a.z, b.z), bz1 = Mathf.Max(a.z, b.z);
                foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var bd = r.bounds; if (bd.max.y < 4.5f || bd.max.x < bx0 || bd.min.x > bx1 || bd.max.z < bz0 || bd.min.z > bz1) continue;
                    if (bd.size.x > 150f && bd.size.z > 150f) continue;
                    var mats = string.Join(",", System.Linq.Enumerable.Select(r.sharedMaterials, m => m != null ? m.name : "-"));
                    Diz(string.Format(IC, "diag r21: {0} | pai {1} | mats {2} | h {3:0.0}..{4:0.0} | mundo x {5:0}..{6:0} z {7:0}..{8:0}", r.gameObject.name,
                                      r.transform.parent != null ? r.transform.parent.name : "-", mats, bd.min.y, bd.max.y, bd.min.x, bd.max.x, bd.min.z, bd.max.z));
                }
            }
            // obliquas
            Persp(Mundo(440f, -22f, 9f), Mundo(640f, -35f, 2f), 60f, "r11_costura_pa_cc");
            Persp(Mundo(200f, -430f, 110f), Mundo(360f, -140f, 0f), 55f, "r11_planta_borda_pa");
            Persp(Mundo(1750f, -520f, 330f), Mundo(850f, -30f, 0f), 50f, "r11_centro_geral");
            Persp(Mundo(1400f, 240f, 80f), Mundo(1760f, 250f, -18f), 55f, "r11_orla");
            Persp(Mundo(1360f, 190f, 45f), Mundo(1150f, 20f, 0f), 55f, "r11_borda_cc_sudeste");
            Persp(Mundo(-900f, -600f, 2.2f), Mundo(-800f, -560f, 2f), 65f, "r11_rua_planta");
            // r15: Praca das Aguas em escala real e restaurantes da Praca Velia Coutinho, dos pontos consultados no Street View
            Persp(Mundo(-112f, -16f, 2.5f), Mundo(-140f, -14.5f, 5f), 50f, "r15_rest_norte_frente");
            Persp(Mundo(-112f, 13f, 2.5f), Mundo(-140f, 14.7f, 5f), 50f, "r15_rest_sul_frente");
            Persp(Mundo(-166f, -40f, 2.5f), Mundo(-152f, -14f, 3.6f), 60f, "r15_rest_passarela");
            Persp(Mundo(-100f, 2f, 22f), Mundo(-142f, 0f, 3f), 55f, "r15_rest_conjunto");
            Persp(Mundo(-200f, 42f, 2.5f), Mundo(-193f, 12f, 3.2f), 50f, "r15_rest_predio_oeste");
            Persp(Mundo(-190f, 60f, 30f), Mundo(-170f, 5f, 2f), 55f, "r15_rest_de_cima_sul");
            Persp(Mundo(250f, -35f, 1.7f), Mundo(255f, 0f, 4f), 75f, "r15_pavilhoes_da_avenida");
            Persp(Mundo(92f, -34f, 1.7f), Mundo(128f, 2f, 9f), 65f, "r15_portal_da_avenida");
            Persp(Mundo(-60f, -70f, 55f), Mundo(-40f, 0f, 0f), 60f, "r15_oeste_de_cima");
            Persp(Mundo(-110f, -78f, 1.7f), Mundo(-112f, -20f, 1.4f), 62f, "r15_tex_av_terencio");
            // r15d: sem o muro. Da avenida olhando para dentro de cada rua transversal, e a frente dos lotes
            Persp(Mundo(337f, -36f, 1.7f), Mundo(337f, -80f, 1.6f), 68f, "r15d_boca_alfredo_cruz");
            Persp(Mundo(68f, 36f, 1.7f), Mundo(68.5f, 80f, 1.6f), 68f, "r15d_boca_cerejo_cruz");
            Persp(Mundo(190f, 36f, 1.7f), Mundo(190.5f, 80f, 1.6f), 68f, "r15d_boca_ajuricaba");
            Persp(Mundo(13f, -36f, 1.7f), Mundo(13.5f, -80f, 1.6f), 68f, "r15d_boca_pedrinho_filho");
            Persp(Mundo(100f, -30f, 1.7f), Mundo(150f, -50f, 3f), 70f, "r15d_frente_norte");
            Persp(Mundo(120f, -95f, 40f), Mundo(130f, -40f, 0f), 60f, "r15d_borda_norte_de_cima");
            Persp(Mundo(250f, 100f, 40f), Mundo(255f, 42f, 0f), 60f, "r15d_borda_sul_de_cima");
            // r15e: Mini Terminal, dos pontos consultados no Street View
            Persp(Mundo(595f, -40f, 1.7f), Mundo(597f, -6f, 3.2f), 58f, "r15e_term_norte");
            Persp(Mundo(595f, 34f, 1.7f), Mundo(597f, 2f, 3.2f), 58f, "r15e_term_sul");
            Persp(Mundo(640f, -5f, 1.7f), Mundo(612f, -5f, 3.4f), 58f, "r15e_term_anel");
            Persp(Mundo(548f, 30f, 1.7f), Mundo(590f, 2f, 3.0f), 58f, "r15e_term_sudoeste");
            Persp(Mundo(594f, 38f, 70f), Mundo(594f, -4f, 0f), 50f, "r15e_term_de_cima");
            // r16: dos mesmos pontos do NOSSO video (carro na pista norte, olhando para o norte)
            Persp(Mundo(-70f, -27.5f, 1.3f), Mundo(-70f, -57.5f, 3.9f), 40f, "r16_video_xm70");
            Persp(Mundo(-9f, -27.5f, 1.3f), Mundo(-9f, -57.5f, 3.9f), 40f, "r16_video_xm9");
            Persp(Mundo(28f, -27.5f, 1.3f), Mundo(28f, -57.5f, 3.9f), 40f, "r16_video_x28");
            Persp(Mundo(50f, -27.5f, 1.3f), Mundo(50f, -57.5f, 3.9f), 40f, "r16_video_x50");
            Persp(Mundo(94f, -27.5f, 1.3f), Mundo(94f, -57.5f, 3.9f), 40f, "r16_video_x94");
            Persp(Mundo(134f, -27.5f, 1.3f), Mundo(134f, -57.5f, 3.9f), 40f, "r16_video_x134");
            Persp(Mundo(262f, -27.5f, 1.3f), Mundo(262f, -57.5f, 3.9f), 40f, "r16_video_x262");
            Persp(Mundo(60f, 10f, 45f), Mundo(70f, -50f, 2f), 55f, "r16_frente_norte_de_cima");
            Persp(Mundo(112f, -24.5f, 1.3f), Mundo(112f, -60f, 5.6f), 40f, "r17_tce_x112");
            Persp(Mundo(97f, -24.5f, 1.3f), Mundo(97f, -60f, 5.6f), 40f, "r17_tce_x97");
            Persp(Mundo(83f, -24.5f, 1.3f), Mundo(83f, -60f, 5.6f), 40f, "r17_tce_x83");
            Persp(Mundo(94f, 0f, 38f), Mundo(94f, -58f, 4f), 50f, "r17_tce_de_cima");
            // r17: predio da Oi e torre de concreto, das posicoes do carro no video (pista norte)
            Persp(Mundo(522f, -40f, 1.3f), Mundo(522f, -66f, 6f), 40f, "r17_oi_x522");
            Persp(Mundo(547f, -40f, 1.3f), Mundo(548f, -70f, 9f), 40f, "r17_oi_x547");
            Persp(Mundo(500f, -40f, 1.3f), Mundo(512f, -66f, 5f), 40f, "r17_oi_x500");
            Persp(Mundo(500f, 20f, 1.6f), Mundo(548f, -100f, 30f), 60f, "r17_oi_torre_de_longe");
            Persp(Mundo(530f, -10f, 55f), Mundo(532f, -95f, 4f), 55f, "r17_oi_de_cima");
            // r18: obra em esqueleto, ESCON (pilone e marquise curva) e casas, das posicoes do carro no video
            Persp(Mundo(4f, -27.5f, 1.3f), Mundo(4f, -57.5f, 3.9f), 40f, "r18_obra_x4");
            Persp(Mundo(17f, -30f, 1.3f), Mundo(5f, -56f, 4f), 40f, "r18_obra_esquina");
            // (da calcada do canteiro, a frente dos conteineres do jogo que ficam na pista)
            Persp(Mundo(148f, -38f, 1.5f), Mundo(141.5f, -52f, 3.2f), 62f, "r18_escon_x147");
            Persp(Mundo(134f, -37f, 1.5f), Mundo(137.5f, -52f, 3.0f), 62f, "r18_escon_x134");
            Persp(Mundo(36f, -38.5f, 1.5f), Mundo(36f, -55f, 2.6f), 62f, "r18_casas_x36");
            Persp(Mundo(104f, -24.5f, 1.3f), Mundo(106f, -60f, 5.6f), 40f, "r18_tce_x104");
            Persp(Mundo(24f, -29f, 1.3f), Mundo(8f, -60f, 3.5f), 40f, "r18_obra_lateral");
            // r19: alturas reconferidas (muro e faixa azul entre o TCE e o predio branco) e predio antigo do portico com o tapume recuado
            Persp(Mundo(109f, -37f, 1.5f), Mundo(112f, -52f, 2.4f), 62f, "r19_muro_tce_x109");
            Persp(Mundo(122f, -37f, 1.5f), Mundo(121f, -56f, 3.0f), 62f, "r19_branco_x122");
            Persp(Mundo(96f, -36f, 1.5f), Mundo(100f, -54f, 3.4f), 62f, "r19_tce_entrada_x96");
            Persp(Mundo(279f, -38f, 1.5f), Mundo(279f, -66f, 3.2f), 62f, "r19_portico_x279");
            Persp(Mundo(236f, -38f, 1.5f), Mundo(244f, -62f, 2.6f), 62f, "r19_ala_oeste_x236");
            Persp(Mundo(-24f, -38f, 1.5f), Mundo(-24f, -54f, 2.0f), 62f, "r19_corretora_xm24");
            // r20: posicoes em planta recalibradas (predio branco 116-124, ESCON 130-152) e ciclovia
            Persp(Mundo(121f, -36f, 1.5f), Mundo(119f, -56f, 3.0f), 62f, "r20_branco_x120");
            Persp(Mundo(133f, -36f, 1.5f), Mundo(131f, -54f, 3.0f), 62f, "r20_escon_x132");
            Persp(Mundo(112f, -36f, 1.5f), Mundo(111f, -52f, 2.2f), 62f, "r20_muro_x112");
            Persp(Mundo(60f, -41.5f, 1.6f), Mundo(20f, -44.5f, 0.2f), 60f, "r20_calcada_x60");
            Persp(Mundo(125f, -30f, 14f), Mundo(125f, -47f, 0f), 50f, "r20_calcada_de_cima_x125");
            // r21: frente SUL (cadastro + consulta ao Street View), vista da pista sul
            foreach (var cx in new[] { -82f, -62f, -40f, -12f, 18f, 45f, 88f, 104f, 120f, 148f, 171f, 208f, 228f, 262f, 290f, 316f })
                Persp(Mundo(cx, 36f, 1.5f), Mundo(cx, 62f, 3.0f), 62f, "r21_sul_x" + (cx < 0 ? "m" : "") + Mathf.Abs(Mathf.RoundToInt(cx)));
            Persp(Mundo(120f, 5f, 60f), Mundo(120f, 62f, 0f), 60f, "r21_sul_de_cima");
            // r24: pontas da frente sul (oeste da Av. Terencio Lima; leste da R. Cecilia Brasil ate o predio cinza de brises)
            foreach (var cx in new[] { -140f, 355f, 410f, 452f, 472f, 528f, 578f })
                Persp(Mundo(cx, 34f, 1.5f), Mundo(cx, 64f, 3.5f), 62f, "r24_sul_x" + (cx < 0 ? "m" : "") + Mathf.Abs(Mathf.RoundToInt(cx)));
            // r25: frente sul a oeste da Av. Terencio Lima ate o Cine Super K: camera onde ficou a da consulta (pista sul, 2,1 m, 8 graus para cima)
            foreach (var c in new[] { new Vector2(-215f, 39.8f), new Vector2(-300f, 40.3f), new Vector2(-390f, 41f), new Vector2(-570f, 42f), new Vector2(-750f, 43f), new Vector2(-930f, 44.6f),
                                      new Vector2(-990f, 45f), new Vector2(-1007f, 45.4f), new Vector2(-1048f, 45.8f), new Vector2(-1115f, 46.5f), new Vector2(-1168f, 47f), new Vector2(-1222f, 48f), new Vector2(-1250f, 47.5f) })
                Persp(Mundo(c.x, c.y, 2.1f), Mundo(c.x, c.y + 20f, 4.9f), 60f, "r25_so_xm" + Mathf.Abs(Mathf.RoundToInt(c.x)));
            Persp(Mundo(410f, -15f, 75f), Mundo(410f, 85f, 0f), 60f, "r25_escola_de_cima");
            Persp(Mundo(-700f, 20f, 45f), Mundo(-700f, 60f, 0f), 60f, "r26_bec_de_cima_xm700");
            // r29: Av. Jaime Brasil (continuacao da avenida a leste do Centro Civico): do canteiro central, olhando para o norte (n) e para o sul (s)
            foreach (var cx in new[] { 1120f, 1160f, 1200f, 1240f, 1280f, 1320f, 1360f, 1400f, 1440f })
            {
                Persp(Mundo(cx, -13f, 2.2f), Mundo(cx, -33f, 5.0f), 60f, "r29_jb_n_x" + Mathf.RoundToInt(cx));
                Persp(Mundo(cx, -23f, 2.2f), Mundo(cx, -3f, 5.0f), 60f, "r29_jb_s_x" + Mathf.RoundToInt(cx));
            }
            Persp(Mundo(1280f, -18f, 90f), Mundo(1280f, -17.9f, 0f), 60f, "r29_jb_de_cima");
            // r29b: cameras dadas por arquivo (06_unity/cams_extra.txt: nome px py pz ax ay az fov W H, no referencial do corredor), para repetir o ponto de vista de cada captura de consulta
            if (File.Exists(Saida("cams_extra.txt")))
                foreach (var lin in File.ReadAllLines(Saida("cams_extra.txt")))
                {
                    var q = lin.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); if (q.Length < 10 || q[0].StartsWith("#")) continue;
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    Persp(Mundo(float.Parse(q[1], ci), float.Parse(q[2], ci), float.Parse(q[3], ci)), Mundo(float.Parse(q[4], ci), float.Parse(q[5], ci), float.Parse(q[6], ci)), float.Parse(q[7], ci), q[0], int.Parse(q[8]), int.Parse(q[9]));
                }
            // r28: frente NORTE da avenida a oeste da praca: camera na pista norte onde ficou a da consulta, olhando para o norte
            foreach (var c in new[] { new Vector2(-150f, -37.5f), new Vector2(-170f, -37f), new Vector2(-235f, -36.5f), new Vector2(-300f, -34.6f), new Vector2(-375f, -35.4f), new Vector2(-440f, -33.7f),
                                      new Vector2(-630f, -33.6f), new Vector2(-770f, -32.9f), new Vector2(-860f, -32.2f), new Vector2(-920f, -31.5f), new Vector2(-1025f, -30f), new Vector2(-1100f, -29.5f),
                                      new Vector2(-1145f, -28.6f), new Vector2(-1190f, -27.7f) })
                Persp(Mundo(c.x, c.y, 2.2f), Mundo(c.x, c.y - 20f, 5.0f), 60f, "r28_no_xm" + Mathf.Abs(Mathf.RoundToInt(c.x)));
            Persp(Mundo(-1025f, -10f, 6f), Mundo(-1025f, -47f, 9f), 70f, "r28_no_sede_aberta");
            Persp(Mundo(-1190f, -8f, 5f), Mundo(-1187f, -51f, 7f), 70f, "r28_no_funasa_aberta");
            Persp(Mundo(-630f, -15f, 5f), Mundo(-631f, -62f, 7f), 70f, "r28_no_ginasio_aberta");
            Persp(Mundo(-316f, -5f, 40f), Mundo(-316f, -60f, 0f), 60f, "r28_no_camara_de_cima");
            // r27: telhados inclinados nos volumes provisorios do mapa inteiro: vistas obliquas de quadras residenciais e uma ao nivel da rua
            Persp(Mundo(-1040f, 55f, 45f), Mundo(-1080f, 130f, 0f), 60f, "r27_telhados_a");
            Persp(Mundo(150f, -70f, 55f), Mundo(150f, -160f, 0f), 60f, "r27_telhados_b");
            Persp(Mundo(250f, 70f, 55f), Mundo(250f, 160f, 0f), 60f, "r27_telhados_c");
            Persp(Mundo(-600f, -260f, 60f), Mundo(-600f, -360f, 0f), 60f, "r27_telhados_d");
            Persp(Mundo(1500f, -300f, 70f), Mundo(1500f, -420f, 0f), 60f, "r27_telhados_e");
            Persp(Mundo(-1500f, 600f, 70f), Mundo(-1500f, 720f, 0f), 60f, "r27_telhados_f");
            Persp(Mundo(200f, 150f, 1.7f), Mundo(260f, 152f, 3.0f), 60f, "r27_rua_c");
            Persp(Mundo(-1100f, 150f, 1.7f), Mundo(-1040f, 152f, 3.0f), 60f, "r27_rua_a");
            Persp(Mundo(-860f, 47f, 1.7f), Mundo(-800f, 52.5f, 2.5f), 60f, "r26_bec_ao_longo_xm860");
            Persp(Mundo(-250f, 41f, 2.1f), Mundo(-250f, 61f, 4.9f), 60f, "r26_so_xm250");
            Persp(Mundo(-480f, 41.6f, 2.1f), Mundo(-480f, 61.6f, 4.9f), 60f, "r26_so_xm480");
            Persp(Mundo(-840f, 43.9f, 2.1f), Mundo(-840f, 63.9f, 4.9f), 60f, "r26_so_xm840");
            Persp(Mundo(572f, -10f, 65f), Mundo(572f, 72f, 0f), 60f, "r25_cinza_de_cima");
            Persp(Mundo(-562f, 40f, 2.1f), Mundo(-563f, 57f, 1.2f), 60f, "r25_so_monumento");
            Persp(Mundo(-145f, 38.6f, 2.1f), Mundo(-145f, 58.6f, 4.9f), 60f, "r25_sul_xm145");
            Persp(Mundo(481f, 32f, 2.1f), Mundo(476f, 46f, 1.5f), 60f, "r25_sul_lanchonete");
            Persp(Mundo(-681f, 43f, 2.1f), Mundo(-681f, 63f, 4.9f), 60f, "r25_so_xm681");
            Persp(Mundo(-1222f, 20f, 40f), Mundo(-1222f, 70f, 0f), 60f, "r25_so_cine_de_cima");
            Persp(Mundo(-1010f, 15f, 45f), Mundo(-1010f, 72f, 0f), 60f, "r25_so_pm_de_cima");
            Persp(Mundo(-562f, 20f, 30f), Mundo(-562f, 60f, 0f), 60f, "r25_so_portaria_de_cima");
            // r22: calcada, ciclovia e canteiro do lado norte na largura real
            Persp(Mundo(96f, -36f, 2.2f), Mundo(112f, -45f, 0f), 60f, "r22_faixa_x104");
            Persp(Mundo(-30f, -36f, 2.2f), Mundo(-12f, -45f, 0f), 60f, "r22_faixa_xm20");
            Persp(Mundo(250f, -36f, 2.2f), Mundo(270f, -45f, 0f), 60f, "r22_faixa_x260");
            Persp(Mundo(30f, -30f, 14f), Mundo(30f, -47f, 0f), 50f, "r22_faixa_de_cima_x30");
            Persp(Mundo(13f, -29f, 12f), Mundo(13f, -45f, 0f), 55f, "r22_boca_x13");
            Persp(Mundo(222f, -34f, 4f), Mundo(236f, -44f, 0f), 60f, "r22_plataforma_x235");
            Persp(Mundo(-175f, -110f, 45f), Mundo(-150f, -15f, 0f), 58f, "r15_tex_planta_de_cima");
            Persp(Mundo(-128f, 30f, 1.7f), Mundo(-146f, 16f, 3.5f), 62f, "r15_tex_rest_sul_perto");
            foreach (var id in new[] { "BVC_CCV_ASSEMBLEIA", "BVC_CCV_TJ", "BVC_CCV_CATEDRAL", "BVC_CCV_CULTURA", "BVC_CCV_PALACIO" })
            {
                var go = Raiz(cena, id); if (!go || !go.activeSelf) continue;
                var rs = go.GetComponentsInChildren<Renderer>(false); if (rs.Length == 0) { Diz("fotos: marco sem renderizador ativo: " + id); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var f = go.transform.forward; float d = Mathf.Max(b.extents.x, b.extents.z) * 1.9f + 25f;
                Persp(b.center + f * d + Vector3.up * (b.extents.y + 6f), b.center, 55f, "r11_marco_" + id.Substring(8).ToLowerInvariant() + "_frente");
                Persp(b.center + new Vector3(0, d * 1.3f, 0) - f * 0.01f, b.center, 55f, "r11_marco_" + id.Substring(8).ToLowerInvariant() + "_cima");
                // r15: de perto, na altura de quem anda, para ver material e textura
                float dp = Mathf.Max(b.extents.x, b.extents.z) * 0.55f + 14f;
                Persp(new Vector3(b.center.x, b.min.y + 3.2f, b.center.z) + f * dp + go.transform.right * (dp * 0.35f), new Vector3(b.center.x, b.min.y + b.size.y * 0.42f, b.center.z), 62f, "r15_perto_" + id.Substring(8).ToLowerInvariant());
                Diz(string.Format(IC, "marco {0}: caixa corredor x {1:0.0}..{2:0.0}, y {3:0.0}..{4:0.0}, h {5:0.00}..{6:0.00}; renderizadores ativos {7}",
                                  id, -b.max.x, -b.min.x, b.min.z, b.max.z, b.min.y, b.max.y, rs.Length));
            }
            UnityEngine.Object.DestroyImmediate(tmp);
        }
    }
}
