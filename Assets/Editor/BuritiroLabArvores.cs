// BURITIRO · teste das árvores do pacote 2 de recursos gráficos (thread de gráficos). v2: só chão aberto (sem telhado em cima) e câmeras fora das paredes.
// Lab: "metodo BuritiroLabArvores.Testar"
// Abre a Vila Jardim, planta as árvores de Assets/BURITIRO/Recursos2/Prefabs só no chão perto do meio do mapa,
// tira fotos pela câmera do editor e reabre a cena sem salvar (o mapa não muda).
// Relatório e fotos em Buritiro_Unity\_diag\arvores_teste\.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuritiroLabArvores
{
    const string Cena = "Assets/FreeBala/Scenes/Map_vila_jardim.unity";
    const string PastaPrefab = "Assets/BURITIRO/Recursos2/Prefabs";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static string Base => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
    static string Dir => Path.Combine(Base, "_diag", "arvores_teste");
    static StringBuilder sb;

    static void L(string s) { sb.AppendLine(s); Debug.Log("[ARV] " + s); }
    static string F(float v) => v.ToString("0.00", CI);
    static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

    public static void Testar()
    {
        sb = new StringBuilder();
        Directory.CreateDirectory(Dir);
        string antes = EditorSceneManager.GetActiveScene().path;
        L("teste árvores " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  cena antes: " + antes);
        try { Rodar(); }
        catch (Exception e) { L("ERRO: " + e); }
        finally
        {
            File.WriteAllText(Path.Combine(Dir, "relatorio.txt"), sb.ToString(), new UTF8Encoding(false));
            // reabre sem salvar: as árvores de teste somem e o mapa fica como estava
            string volta = string.IsNullOrEmpty(antes) ? Cena : antes;
            EditorSceneManager.OpenScene(volta, OpenSceneMode.Single);
            Debug.Log("[ARV] cena reaberta sem salvar: " + volta);
        }
    }

    static void Rodar()
    {
        // 1) prefabs
        var prefabs = Directory.Exists(PastaPrefab)
            ? Directory.GetFiles(PastaPrefab, "*.prefab").Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'))).Where(g => g != null).ToArray()
            : new GameObject[0];
        L("prefabs: " + prefabs.Length);
        if (prefabs.Length == 0) { L("nenhum prefab: rode antes BuritiroImportarRecursos.Importar"); return; }
        foreach (var p in prefabs) Descrever(p);

        // 2) cena
        EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        Physics.SyncTransforms();
        Vector3 cA = new Vector3(79, 0, -31), cB = new Vector3(-112, 0, -55);
        var centro = (cA + cB) * 0.5f;
        float chao = Topo(cA, out _).y;
        L("centro " + V(centro) + "  chão de referência y=" + F(chao));
        // v2: "aberto" = a primeira coisa que o raio de cima acerta é chão (não telhado), no ponto e num anel de 12 e 22 m
        Vector3 melhor = Vector3.zero; int melhorN = -1; float melhorD = 1e9f;
        for (int gx = -10; gx <= 10; gx++)
            for (int gz = -10; gz <= 10; gz++)
            {
                var c = centro + new Vector3(gx * 15f, 0, gz * 15f);
                if (!ChaoAberto(c, chao, out var pc)) continue;
                int n = 0;
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8f;
                    if (ChaoAberto(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 12f, chao, out _)) n++;
                    if (ChaoAberto(c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 22f, chao, out _)) n++;
                }
                float d = (c - centro).magnitude;
                if (n > melhorN || (n == melhorN && d < melhorD)) { melhorN = n; melhorD = d; melhor = pc; }
            }
        L("ponto escolhido " + V(melhor) + "  aberto " + melhorN + "/32");

        // 3) plantar só em chão aberto (sem telhado em cima), 5 m de distância mínima
        var pai = new GameObject("BURITIRO_ArvoresTeste");
        var rnd = new System.Random(7);
        var postas = new List<Vector3>();
        for (int t = 0; t < 600 && postas.Count < 14; t++)
        {
            double a = rnd.NextDouble() * Math.PI * 2, d = Math.Sqrt(rnd.NextDouble()) * 22.0;
            var q = melhor + new Vector3((float)(Math.Cos(a) * d), 0, (float)(Math.Sin(a) * d));
            if (!ChaoAberto(q, chao, out var pq)) continue;
            if (Physics.CheckSphere(pq + Vector3.up * 1.2f, 1.0f, ~0, QueryTriggerInteraction.Ignore)) continue; // parede, poste, banco
            if (postas.Any(o => (o - pq).sqrMagnitude < 25f)) continue;
            var pf = prefabs[postas.Count % prefabs.Length];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, pai.transform);
            go.transform.position = pq;
            go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            go.transform.localScale = Vector3.one * (0.85f + (float)rnd.NextDouble() * 0.3f);
            postas.Add(pq);
        }
        Physics.SyncTransforms();
        L("árvores plantadas: " + postas.Count);
        if (postas.Count == 0) return;
        var meio = postas.Aggregate(Vector3.zero, (s, v) => s + v) / postas.Count;
        var alvo = pai.transform.Cast<Transform>().OrderBy(t => (t.position - meio).sqrMagnitude).First();
        var b = Limites(alvo.gameObject);
        L("árvore da foto de perto: " + alvo.name + " em " + V(alvo.position) + " altura " + F(b.size.y) + " m");

        // 4) fotos: cada câmera procura, girando de 30 em 30 graus, um lugar fora de parede e com visão livre do alvo
        Vector3 olho = Vector3.up * 1.7f;
        var alvoMeio = meio + Vector3.up * 3f;
        var vistas = new List<(string, Vector3, Vector3)>();
        void Vista(string nome, Vector3 foco, float dist, float altura, Transform ignorar)
        {
            Vector3 melhorP = foco + new Vector3(0, altura, -dist); float melhorLivre = -1f;
            for (int k = 0; k < 12; k++)
            {
                float a = k * 30f * Mathf.Deg2Rad;
                var p = new Vector3(foco.x + Mathf.Sin(a) * dist, 0, foco.z + Mathf.Cos(a) * dist);
                float yChao = Topo(p, out bool ok).y; if (!ok) continue;
                p.y = Mathf.Max(yChao, chao) + altura;
                if (Physics.CheckSphere(p, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var dir = foco - p;
                float livre = Physics.Raycast(p, dir.normalized, out var h, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    ? (ignorar != null && h.transform.IsChildOf(pai.transform) ? dir.magnitude : h.distance) : dir.magnitude;
                if (livre > melhorLivre) { melhorLivre = livre; melhorP = p; }
                if (livre >= dir.magnitude - 0.5f) break;
            }
            L("vista " + nome + ": visão livre " + F(melhorLivre) + " de " + F(dist) + " m");
            vistas.Add((nome, melhorP, foco));
        }
        Vista("1_aerea", meio + Vector3.up * 2f, 40f, 30f, pai.transform);
        Vista("2_jogador", alvoMeio, 24f, 1.7f, pai.transform);
        Vista("3_perto", b.center, Mathf.Max(8f, b.size.y * 1.1f), 1.7f, pai.transform);
        vistas.Add(("4_debaixo_copa", alvo.position + new Vector3(1.5f, 1.7f, 1.0f), alvo.position + Vector3.up * (b.size.y * 0.8f) + new Vector3(-2f, 0, -1f)));
        Vista("5_longe_80m", alvoMeio, 80f, 6f, pai.transform);
        Vista("6_longe_160m", alvoMeio, 160f, 12f, pai.transform);
        Fotos(vistas);
    }

    // primeira coisa que um raio vindo de cima acerta
    static Vector3 Topo(Vector3 p, out bool ok)
    {
        ok = Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out var h, 600f, ~0, QueryTriggerInteraction.Ignore);
        return ok ? h.point : new Vector3(p.x, 0f, p.z);
    }

    // chão aberto: o raio de cima acerta direto o chão (normal para cima, perto da altura do chão), sem telhado/laje/copa em cima
    static bool ChaoAberto(Vector3 p, float chao, out Vector3 ponto)
    {
        ponto = p;
        if (!Physics.Raycast(new Vector3(p.x, 300f, p.z), Vector3.down, out var h, 600f, ~0, QueryTriggerInteraction.Ignore)) return false;
        ponto = h.point;
        return h.normal.y > 0.85f && Mathf.Abs(h.point.y - chao) < 1.0f;
    }

    static Bounds Limites(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static void Descrever(GameObject p)
    {
        L("--- " + p.name);
        var lg = p.GetComponent<LODGroup>();
        if (lg == null) L("  sem LODGroup");
        else
        {
            var lods = lg.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                int tris = 0; var mats = new HashSet<string>();
                foreach (var r in lods[i].renderers)
                {
                    if (r == null) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf && mf.sharedMesh) tris += (int)(mf.sharedMesh.triangles.Length / 3);
                    foreach (var m in r.sharedMaterials) if (m) mats.Add(m.name + "[" + (m.shader ? m.shader.name : "sem shader") + "]");
                }
                L("  LOD" + i + " corte " + F(lods[i].screenRelativeTransitionHeight) + "  renderers " + lods[i].renderers.Length + "  tris " + tris + "  mats " + string.Join(", ", mats));
            }
        }
        var b = Limites(p);
        L("  tamanho " + V(b.size) + "  colisores " + p.GetComponentsInChildren<Collider>().Length);
        foreach (var r in p.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
            {
                if (!m) { L("  material vazio em " + r.name); continue; }
                var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                if (t == null) L("  " + r.name + ": " + m.name + " SEM textura base");
            }
    }

    static void Fotos(List<(string, Vector3, Vector3)> vistas)
    {
        var go = new GameObject("ARV_FotoCam"); go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>(); cam.fieldOfView = 60; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500f;
        var ucd = go.AddComponent<UniversalAdditionalCameraData>(); ucd.renderPostProcessing = true; ucd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1;
        cam.targetTexture = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        float lodAntes = QualitySettings.lodBias;
        try
        {
            QualitySettings.lodBias = Mathf.Max(lodAntes, 1f);
            foreach (var (nome, de, para) in vistas)
            {
                go.transform.position = de; go.transform.rotation = Quaternion.LookRotation(para - de, Vector3.up);
                cam.Render(); // a 1ª imagem logo depois de abrir a cena sai sem parte do mapa no editor
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(Dir, nome + ".jpg"), tex.EncodeToJPG(90));
                L("foto " + nome + " de " + V(de));
            }
        }
        finally
        {
            QualitySettings.lodBias = lodAntes;
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
