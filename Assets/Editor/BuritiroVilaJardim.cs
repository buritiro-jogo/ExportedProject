// BURITIRO · Vila Jardim · auditoria, correção de bugs e polimento visual
// Thread "Polir mapa Vila Jardim com realismo" (31/10/2026). Versão 2:
// - não desliga mais as "cópias" *_interior/*_LONGE_nocol (eram LOD de longe: sumiam paredes)
// - não mexe mais na sonda de reflexo (realtime quebrava o SSR e pesava a GPU)
// - material novo com nome único por material antigo (dois VJ_concrete/VJ_white_paint viravam um só e trocavam a textura das paredes)
// - v2.2: móveis internos dos galpões e objetos pequenos pesados ganham LOD que os esconde de longe (menu 6)
// - grama mantém a cor verde original, só ganha relevo; detalhe das paredes mais leve; névoa termina nos mesmos 448 m
// Menu: BURITIRO > Vila Jardim. Toda ação de mudança faz backup da cena antes.
// Relatórios em Buritiro_Unity\_diag\vila_jardim_auditoria\ e fotos em Buritiro_Unity\_diag\vila_jardim_fotos\.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BuritiroVilaJardim
{
    const string Cena = "Assets/FreeBala/Scenes/Map_vila_jardim.unity";
    const string Raiz = "Map_vila_jardim";
    const string Pasta = "Assets/BuritiroVJ";
    const string PastaTex = "Assets/BuritiroVJ/Texturas";
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    static string Base => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
    static string Diag => Path.Combine(Base, "_diag", "vila_jardim_auditoria");
    static string Fotos => Path.Combine(Base, "_diag", "vila_jardim_fotos");
    static string Backup => Path.Combine(Base, "_backup", "vila_jardim");

    // nomes que não devem ganhar colisão nem ser "assentados" no chão
    static readonly Regex NaoSolido = new Regex("Mato|capim|copa|folha|leaves|palm_leaves|vidro|glass|Fios|Luminaria|LuminariaCabeca|ArCond|nocol|LONGE|interior|planta|Sinalizacao|Ruas|Chao|Terra_|Becos|Patio|decal|sombra", RegexOptions.IgnoreCase);
    static readonly Regex ObjetoDeChao = new Regex("Arvore|Troncos|sacos|Cor_|banco|mesas|churrasqueira|lixo|moto|carro|Caixa|conteiner|Postes|poste|Cob_|bicicleta|lixeira|vaso|tambor|pallet|entulho", RegexOptions.IgnoreCase);

    // ------------------------------------------------------------------ menus
    [MenuItem("BURITIRO/Vila Jardim/1 Auditar (só relatório)")]
    public static void MenuAuditar() { Abrir(); Auditar("manual"); }

    [MenuItem("BURITIRO/Vila Jardim/2 Corrigir bugs")]
    public static void MenuCorrigir() { Abrir(); FazerBackup("antes_corrigir"); Corrigir(); Salvar(); }

    [MenuItem("BURITIRO/Vila Jardim/3 Polir visual")]
    public static void MenuPolir() { Abrir(); FazerBackup("antes_polir"); Polir(); Salvar(); }

    [MenuItem("BURITIRO/Vila Jardim/4 Fotos do Editor (antes)")]
    public static void MenuFotosAntes() { Abrir(); TirarFotos("antes"); }

    [MenuItem("BURITIRO/Vila Jardim/5 Fotos do Editor (depois)")]
    public static void MenuFotosDepois() { Abrir(); TirarFotos("depois"); }

    [MenuItem("BURITIRO/Vila Jardim/Tudo (fotos antes, auditar, corrigir, polir, auditar, fotos depois)")]
    public static void Tudo()
    {
        Abrir();
        FazerBackup("antes_tudo");
        TirarFotos("antes");
        Auditar("antes");
        Corrigir();
        Polir();
        Otimizar();
        Salvar();
        Auditar("depois");
        TirarFotos("depois");
        Log("TUDO PRONTO");
    }

    [MenuItem("BURITIRO/Vila Jardim/6 Otimizar desempenho (some móveis e objetos pesados de longe)")]
    public static void MenuOtimizar() { Abrir(); FazerBackup("antes_otimizar"); Otimizar(); Salvar(); }

    [MenuItem("BURITIRO/Vila Jardim/Desfazer (volta o último backup)")]
    public static void Desfazer() { Restaurar(false); }

    [MenuItem("BURITIRO/Vila Jardim/Voltar ao original (primeiro backup, antes de qualquer versão)")]
    public static void VoltarOriginal() { Restaurar(true); }

    static void Restaurar(bool primeiro)
    {
        if (!Directory.Exists(Backup)) { Log("sem backup"); return; }
        var lista = Directory.GetFiles(Backup, "Map_vila_jardim_*.unity").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToList();
        if (lista.Count == 0) { Log("sem backup"); return; }
        var esc = primeiro ? lista.First() : lista.Last();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        File.Copy(esc, Path.Combine(Path.GetDirectoryName(Application.dataPath), Cena), true);
        AssetDatabase.ImportAsset(Cena, ImportAssetOptions.ForceUpdate);
        // fb_data do mesmo momento do backup
        var m = Regex.Match(Path.GetFileName(esc), @"Map_vila_jardim_(\d{8}_\d{6})_");
        if (m.Success)
        {
            string st = m.Groups[1].Value;
            foreach (var fb in FbDataCaminhos())
            {
                var src = Path.Combine(Backup, "fb_data_" + (fb.Contains("StreamingAssets") ? "streaming" : "resources") + "_" + st + ".json");
                if (!File.Exists(src) || !File.Exists(fb)) continue;
                // só o bloco maps.vila_jardim volta; o resto do arquivo atual (outras arenas) fica intacto
                string velho = File.ReadAllText(src), atual = File.ReadAllText(fb);
                string bVelho = BlocoVJ(velho, out _, out _);
                string bAtual = BlocoVJ(atual, out int ini, out int fim);
                if (bVelho == null || bAtual == null) { Log("fb_data: bloco vila_jardim não achado em " + fb + ", não mexido"); continue; }
                if (bVelho == bAtual) { Log("fb_data: vila_jardim já igual ao backup em " + fb); continue; }
                File.Copy(fb, fb + ".antes_restaurar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak", true);
                File.WriteAllText(fb, atual.Substring(0, ini) + bVelho + atual.Substring(fim + 1), new UTF8Encoding(false));
                Log("fb_data: só o bloco vila_jardim restaurado em " + fb);
            }
        }
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        Log("cena restaurada de " + esc);
    }

    // ------------------------------------------------------------------ util
    static StringBuilder logSb = new StringBuilder();
    static void Log(string s)
    {
        string l = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
        Debug.Log("[VJ] " + l);
        logSb.AppendLine(l);
        try { Directory.CreateDirectory(Diag); File.AppendAllText(Path.Combine(Diag, "log_editor.txt"), l + Environment.NewLine); } catch { }
    }

    static void Abrir()
    {
        var s = EditorSceneManager.GetActiveScene();
        if (s.path != Cena) EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        Physics.SyncTransforms();
    }

    static void Salvar()
    {
        var s = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
        AssetDatabase.SaveAssets();
        Log("cena salva");
    }

    static void FazerBackup(string tag)
    {
        Directory.CreateDirectory(Backup);
        string st = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string proj = Path.GetDirectoryName(Application.dataPath);
        File.Copy(Path.Combine(proj, Cena), Path.Combine(Backup, "Map_vila_jardim_" + st + "_" + tag + ".unity"), true);
        foreach (var fb in FbDataCaminhos())
            if (File.Exists(fb)) File.Copy(fb, Path.Combine(Backup, "fb_data_" + (fb.Contains("StreamingAssets") ? "streaming" : "resources") + "_" + st + ".json"), true);
        Log("backup feito em " + Backup + " (" + tag + ")");
    }

    static IEnumerable<string> FbDataCaminhos()
    {
        yield return Path.Combine(Application.dataPath, "StreamingAssets", "fb_data.json");
        yield return Path.Combine(Application.dataPath, "Resources", "fb_data.json");
    }

    static GameObject RaizMapa() { return GameObject.Find(Raiz); }

    static string V(Vector3 v) { return string.Format(CI, "({0:0.0}, {1:0.0}, {2:0.0})", v.x, v.y, v.z); }

    static Bounds? BoundsDe(GameObject g)
    {
        Bounds? b = null;
        foreach (var r in g.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled) continue;
            if (b == null) b = r.bounds; else { var x = b.Value; x.Encapsulate(r.bounds); b = x; }
        }
        return b;
    }

    static bool ChaoAbaixo(Vector3 de, float alcance, Transform ignorar, out RaycastHit hitOut)
    {
        hitOut = default(RaycastHit);
        var hits = Physics.RaycastAll(de, Vector3.down, alcance, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue; bool ok = false;
        foreach (var h in hits)
        {
            if (ignorar != null && h.collider.transform.IsChildOf(ignorar)) continue;
            if (h.distance < best) { best = h.distance; hitOut = h; ok = true; }
        }
        return ok;
    }

    // ------------------------------------------------------------------ pontos do fb_data
    class Ponto { public string lista; public int i; public Vector3 web; public Vector3 unity; public string texto; }

    static string BlocoVJ(string json, out int ini, out int fim)
    {
        ini = json.IndexOf("\"vila_jardim\"", StringComparison.Ordinal); fim = -1;
        if (ini < 0) return null;
        int a = json.IndexOf('{', ini); int prof = 0;
        for (int k = a; k < json.Length; k++)
        {
            if (json[k] == '{') prof++;
            else if (json[k] == '}') { prof--; if (prof == 0) { fim = k; break; } }
        }
        return fim > 0 ? json.Substring(ini, fim - ini + 1) : null;
    }

    static List<Ponto> LerPontos(string bloco)
    {
        var res = new List<Ponto>();
        // usa só a parte "level" (o jogo lê de lá); se não houver, usa o bloco inteiro
        int lv = bloco.IndexOf("\"level\"", StringComparison.Ordinal);
        string b = lv >= 0 ? bloco.Substring(lv) : bloco;
        foreach (var nome in new[] { "spawnA", "spawnB", "spawnPolice", "bots", "points", "waypoints", "alleys" })
        {
            var m = Regex.Match(b, "\"" + nome + "\"\\s*:\\s*\\[(.*?)\\]\\s*\\]", RegexOptions.Singleline);
            if (!m.Success) continue;
            int i = 0;
            foreach (Match t in Regex.Matches(m.Groups[1].Value + "]", "\\[\\s*(-?[\\d.eE+-]+)\\s*,\\s*(-?[\\d.eE+-]+)\\s*,\\s*(-?[\\d.eE+-]+)\\s*\\]"))
            {
                var w = new Vector3(float.Parse(t.Groups[1].Value, CI), float.Parse(t.Groups[2].Value, CI), float.Parse(t.Groups[3].Value, CI));
                res.Add(new Ponto { lista = nome, i = i++, web = w, unity = new Vector3(-w.x, w.y, w.z), texto = t.Value });
            }
        }
        return res;
    }

    // personagem: cápsula 0,3 m de raio, 1,8 m
    static bool CapsulaLivre(Vector3 pe)
    {
        return !Physics.CheckCapsule(pe + Vector3.up * 0.45f, pe + Vector3.up * 1.55f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
    }

    static bool AcharLugarLivre(Vector3 p, float raioMax, out Vector3 livre)
    {
        livre = p;
        for (float r = 0.5f; r <= raioMax; r += 0.5f)
            for (int a = 0; a < 16; a++)
            {
                float ang = a * Mathf.PI * 2f / 16f;
                var q = p + new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
                RaycastHit h;
                if (!ChaoAbaixo(q + Vector3.up * 3f, 8f, null, out h)) continue;
                var pe = h.point + Vector3.up * 0.05f;
                if (CapsulaLivre(pe)) { livre = pe; return true; }
            }
        return false;
    }

    // ------------------------------------------------------------------ AUDITORIA
    public static void Auditar(string tag)
    {
        var sb = new StringBuilder();
        var root = RaizMapa();
        if (!root) { Log("raiz " + Raiz + " não achada"); return; }
        Physics.SyncTransforms();
        sb.AppendLine("AUDITORIA VILA JARDIM · " + tag + " · " + DateTime.Now);

        var mrs = root.GetComponentsInChildren<MeshRenderer>(true);
        int lote = 0;
        foreach (var mr in mrs)
        {
            var so = new SerializedObject(mr);
            var p = so.FindProperty("m_StaticBatchInfo.subMeshCount");
            if (p != null && p.intValue > 0) lote++;
        }
        sb.AppendLine("renderers: " + mrs.Length + " · sobras de lote estático do AssetRipper: " + lote);

        // shaders quebrados (roxo)
        var mats = new HashSet<Material>();
        foreach (var mr in mrs) foreach (var m in mr.sharedMaterials) if (m) mats.Add(m);
        int roxo = 0, nulos = 0;
        foreach (var mr in mrs) foreach (var m in mr.sharedMaterials) if (!m) nulos++;
        foreach (var m in mats)
            if (!m.shader || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported) { roxo++; sb.AppendLine("  ROXO: " + m.name + " shader " + (m.shader ? m.shader.name : "null")); }
        sb.AppendLine("materiais: " + mats.Count + " · com shader quebrado (roxo): " + roxo + " · espaços de material vazios: " + nulos);

        // duplicados exatos
        var dup = Duplicados(mrs);
        sb.AppendLine("cópias exatas sobrepostas (Z-fighting e desenho em dobro): " + dup.Count);
        foreach (var d in dup.Take(30)) sb.AppendLine("  DUP: " + Caminho(d.transform));

        // coplanares
        var cop = Coplanares(mrs);
        sb.AppendLine("pares de pisos coplanares com material diferente (Z-fighting): " + cop.Count);
        foreach (var c in cop.Take(30)) sb.AppendLine("  COPLANAR: " + Caminho(c.Key.transform) + " x " + Caminho(c.Value.transform));

        // flutuando / sem chão
        int flut = 0, semChao = 0;
        foreach (Transform t in root.transform)
        {
            if (!ObjetoDeChao.IsMatch(t.name) || NaoSolido.IsMatch(t.name)) continue;
            var b = BoundsDe(t.gameObject); if (b == null) continue;
            if (b.Value.size.y > 25f || b.Value.size.x > 40f || b.Value.size.z > 40f) continue;
            RaycastHit h;
            var de = new Vector3(b.Value.center.x, b.Value.min.y + 0.4f, b.Value.center.z);
            if (!ChaoAbaixo(de, 60f, t, out h)) { semChao++; sb.AppendLine("  SEM CHÃO: " + t.name + " " + V(b.Value.center)); continue; }
            float gap = b.Value.min.y - h.point.y;
            if (gap > 0.15f && gap < 6f) { flut++; sb.AppendLine(string.Format(CI, "  FLUTUANDO {0:0.00} m: {1} {2}", gap, t.name, V(b.Value.center))); }
        }
        sb.AppendLine("objetos de chão flutuando: " + flut + " · sem chão embaixo: " + semChao);

        // atravessáveis (visual sem colisão)
        var atrav = Atravessaveis(root);
        sb.AppendLine("objetos sólidos sem colisão (dá para atravessar): " + atrav.Count);
        foreach (var a in atrav.Take(60)) sb.AppendLine("  SEM COLISÃO: " + Caminho(a.transform) + " " + V(a.bounds.center) + " " + V(a.bounds.size));

        // pontos do fb_data
        string fb = FbDataCaminhos().FirstOrDefault(File.Exists);
        Vector3 cA = Vector3.zero, cB = Vector3.zero;
        if (fb != null)
        {
            int i0, i1; var bloco = BlocoVJ(File.ReadAllText(fb), out i0, out i1);
            var pts = bloco != null ? LerPontos(bloco) : new List<Ponto>();
            int ruins = 0, buraco = 0;
            foreach (var p in pts)
            {
                RaycastHit h;
                if (!ChaoAbaixo(p.unity + Vector3.up * 2.5f, 12f, null, out h)) { buraco++; sb.AppendLine("  PONTO SEM CHÃO: " + p.lista + "[" + p.i + "] " + V(p.unity)); continue; }
                if (!CapsulaLivre(h.point + Vector3.up * 0.05f)) { ruins++; sb.AppendLine("  PONTO PRESO EM OBJETO: " + p.lista + "[" + p.i + "] " + V(p.unity) + " encosta em " + QuemEncosta(h.point)); }
            }
            sb.AppendLine("pontos do fb_data (spawns, bots, waypoints, becos): " + pts.Count + " · presos dentro de objeto: " + ruins + " · sem chão: " + buraco);
            var sa = pts.Where(p => p.lista == "spawnA").Select(p => p.unity).ToList();
            var sbb = pts.Where(p => p.lista == "spawnB").Select(p => p.unity).ToList();
            if (sa.Count > 0 && sbb.Count > 0)
            {
                cA = Media(sa); cB = Media(sbb);
                sb.AppendLine(string.Format(CI, "base A {0} · base B {1} · distância {2:0} m · linha de visão direta: {3}", V(cA), V(cB), Vector3.Distance(cA, cB), Visao(cA, cB) ? "SIM (ruim)" : "não"));
                sb.AppendLine(string.Format(CI, "cobertura num raio de 25 m · base A: {0} · base B: {1}", Cobertura(cA, cB), Cobertura(cB, cA)));
            }
        }

        // buracos na colisão do chão, numa grade de 2 m dentro da área de jogo
        Vector3 centro = (cA + cB) * 0.5f; float raio = Mathf.Max(160f, Vector3.Distance(cA, cB) * 0.5f + 90f);
        int amostras = 0, furos = 0; var celulas = new Dictionary<Vector2Int, int>();
        for (float x = centro.x - raio; x <= centro.x + raio; x += 2f)
            for (float z = centro.z - raio; z <= centro.z + raio; z += 2f)
            {
                amostras++;
                if (Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore)) continue;
                furos++; var c = new Vector2Int(Mathf.FloorToInt(x / 10f), Mathf.FloorToInt(z / 10f));
                celulas[c] = celulas.TryGetValue(c, out var n) ? n + 1 : 1;
            }
        sb.AppendLine(string.Format(CI, "chão: {0} amostras de 2 m em {1:0} m de raio · sem nenhuma colisão embaixo (cai do mapa): {2}", amostras, raio, furos));
        foreach (var c in celulas.OrderByDescending(k => k.Value).Take(25)) sb.AppendLine(string.Format(CI, "  BURACO perto de ({0}, {1}): {2} amostras", c.Key.x * 10 + 5, c.Key.y * 10 + 5, c.Value));

        // pesados
        var pesados = new Dictionary<Mesh, int>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) pesados[mf.sharedMesh] = pesados.TryGetValue(mf.sharedMesh, out var n2) ? n2 + 1 : 1;
        sb.AppendLine("malhas mais pesadas (triângulos x cópias):");
        foreach (var kv in pesados.Select(k => new { k.Key, n = k.Value, t = Tris(k.Key) }).OrderByDescending(k => (long)k.n * k.t).Take(15))
            sb.AppendLine(string.Format(CI, "  {0} · {1} tri x {2} = {3}", kv.Key.name, kv.t, kv.n, (long)kv.t * kv.n));

        Directory.CreateDirectory(Diag);
        string arq = Path.Combine(Diag, "relatorio_editor_" + tag + ".txt");
        File.WriteAllText(arq, sb.ToString());
        Log("auditoria (" + tag + ") gravada em " + arq);
    }

    static int Tris(Mesh m) { long t = 0; for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i); return (int)(t / 3); }
    static Vector3 Media(List<Vector3> l) { var s = Vector3.zero; foreach (var v in l) s += v; return s / Mathf.Max(1, l.Count); }
    static string Caminho(Transform t) { var s = t.name; while (t.parent) { t = t.parent; s = t.name + "/" + s; } return s; }

    static string QuemEncosta(Vector3 pe)
    {
        var cs = Physics.OverlapCapsule(pe + Vector3.up * 0.45f, pe + Vector3.up * 1.55f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
        return string.Join(", ", cs.Take(3).Select(c => c.name));
    }

    static bool Visao(Vector3 a, Vector3 b)
    {
        var o = a + Vector3.up * 1.6f; var d = b + Vector3.up * 1.6f;
        return !Physics.Linecast(o, d, ~0, QueryTriggerInteraction.Ignore);
    }

    // conta peças que cobrem um homem agachado entre a base e o inimigo
    static int Cobertura(Vector3 base_, Vector3 inimigo)
    {
        var dir = (inimigo - base_); dir.y = 0; dir.Normalize();
        int n = 0;
        var cs = Physics.OverlapSphere(base_, 25f, ~0, QueryTriggerInteraction.Ignore);
        var vistos = new HashSet<Collider>();
        foreach (var c in cs)
        {
            if (vistos.Contains(c)) continue; vistos.Add(c);
            var b = c.bounds;
            if (b.max.y - Mathf.Max(b.min.y, base_.y) < 0.9f) continue; // baixo demais
            if (b.size.x > 120f || b.size.z > 120f) continue; // chão/terreno
            var to = b.center - base_; to.y = 0;
            if (to.magnitude < 1.5f) continue;
            if (Vector3.Dot(to.normalized, dir) < 0.2f) continue; // atrás da base
            n++;
        }
        return n;
    }

    static List<MeshRenderer> Duplicados(MeshRenderer[] mrs)
    {
        var res = new List<MeshRenderer>();
        var vistos = new Dictionary<string, MeshRenderer>();
        foreach (var mr in mrs)
        {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var so = new SerializedObject(mr);
            var sp = so.FindProperty("m_StaticBatchInfo.subMeshCount");
            var fp = so.FindProperty("m_StaticBatchInfo.firstSubMesh");
            var m = mr.transform.localToWorldMatrix;
            var sbk = new StringBuilder();
            sbk.Append(mf.sharedMesh.GetInstanceIDSafe()).Append('|').Append(sp != null ? sp.intValue : 0).Append('|').Append(fp != null ? fp.intValue : 0).Append('|');
            for (int i = 0; i < 12; i++) sbk.Append(Mathf.Round(m[i] * 50f) / 50f).Append(',');
            foreach (var mat in mr.sharedMaterials) sbk.Append(mat ? mat.name : "-").Append(';');
            string k = sbk.ToString();
            if (vistos.ContainsKey(k)) res.Add(mr); else vistos[k] = mr;
        }
        return res;
    }

    static int GetInstanceIDSafe(this UnityEngine.Object o) { return o ? o.GetHashCode() : 0; }

    static List<KeyValuePair<MeshRenderer, MeshRenderer>> Coplanares(MeshRenderer[] mrs)
    {
        var planos = mrs.Where(r => r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.y < 0.06f && r.bounds.size.x * r.bounds.size.z > 0.5f && r.bounds.size.x < 400f && r.bounds.size.z < 400f).ToArray();
        var res = new List<KeyValuePair<MeshRenderer, MeshRenderer>>();
        for (int i = 0; i < planos.Length; i++)
            for (int j = i + 1; j < planos.Length; j++)
            {
                var a = planos[i]; var b = planos[j];
                if (Mathf.Abs(a.bounds.center.y - b.bounds.center.y) > 0.004f) continue;
                var ba = a.bounds; var bb = b.bounds;
                if (ba.max.x < bb.min.x || bb.max.x < ba.min.x || ba.max.z < bb.min.z || bb.max.z < ba.min.z) continue;
                if (a.sharedMaterial == b.sharedMaterial) continue;
                res.Add(new KeyValuePair<MeshRenderer, MeshRenderer>(a, b));
            }
        return res;
    }

    static List<Renderer> Atravessaveis(GameObject root)
    {
        var res = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!r.enabled) continue;
            if (NaoSolido.IsMatch(Caminho(r.transform))) continue;
            var b = r.bounds;
            if (b.size.y < 1.2f || b.size.x * b.size.z < 1f) continue;
            if (b.size.x > 80f || b.size.z > 80f || b.size.y > 40f) continue;
            var cs = Physics.OverlapBox(b.center, b.extents * 0.9f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (cs.Length == 0) res.Add(r);
        }
        return res;
    }

    // ------------------------------------------------------------------ CORREÇÕES
    public static void Corrigir()
    {
        var root = RaizMapa(); if (!root) { Log("raiz não achada"); return; }
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets", "BuritiroVJ");
        if (!AssetDatabase.IsValidFolder(Pasta + "/Malhas")) AssetDatabase.CreateFolder(Pasta, "Malhas");

        int n1 = DesfazerLoteEstatico(root);
        Physics.SyncTransforms();
        var mrs = root.GetComponentsInChildren<MeshRenderer>(true);

        // cópias exatas
        // v2: só relata. As "cópias" são *_interior e *_LONGE_nocol, que o jogo usa como LOD/longe; desligar fazia paredes sumirem.
        var dup = Duplicados(mrs);
        Log("cópias exatas encontradas (não mexidas): " + dup.Count);

        // shaders roxos
        int rx = ConsertarRoxos(mrs);
        Log("materiais roxos consertados: " + rx);

        // Z-fighting em pisos coplanares: sobe 1,2 cm o menor
        int cp = 0; var subidos = new HashSet<Transform>();
        foreach (var par in Coplanares(mrs))
        {
            var a = par.Key; var b = par.Value;
            var menor = (a.bounds.size.x * a.bounds.size.z) < (b.bounds.size.x * b.bounds.size.z) ? a : b;
            if (subidos.Contains(menor.transform)) continue;
            subidos.Add(menor.transform);
            menor.transform.position += Vector3.up * 0.012f; cp++;
        }
        Log("pisos coplanares separados: " + cp);
        Physics.SyncTransforms();

        // objetos de chão flutuando: assenta no chão
        int fl = 0;
        foreach (Transform t in root.transform)
        {
            if (!ObjetoDeChao.IsMatch(t.name) || NaoSolido.IsMatch(t.name)) continue;
            var b = BoundsDe(t.gameObject); if (b == null) continue;
            if (b.Value.size.y > 25f || b.Value.size.x > 40f || b.Value.size.z > 40f) continue;
            RaycastHit h;
            if (!ChaoAbaixo(new Vector3(b.Value.center.x, b.Value.min.y + 0.4f, b.Value.center.z), 60f, t, out h)) continue;
            float gap = b.Value.min.y - h.point.y;
            if (gap > 0.15f && gap < 6f) { t.position -= Vector3.up * (gap - 0.02f); fl++; }
        }
        Log("objetos flutuando assentados no chão: " + fl);
        Physics.SyncTransforms();

        // sólidos sem colisão: ganha MeshCollider
        int col = 0;
        foreach (var r in Atravessaveis(root))
        {
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            if (Tris(mf.sharedMesh) > 60000) continue;
            var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; col++;
        }
        Log("colisões adicionadas em objetos atravessáveis: " + col);
        Physics.SyncTransforms();

        // buracos no chão: chão de segurança invisível 25 cm abaixo
        ChaoDeSeguranca(root);

        // pontos do fb_data presos: move para o lugar livre mais perto (até 3 m)
        CorrigirPontos();

        // cobertura da base mais aberta
        Physics.SyncTransforms();
        IgualarCobertura(root);

        // sombra: objetos pequenos não projetam sombra (desempenho)
        int sh = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var s = r.bounds.size;
            if (r.shadowCastingMode == ShadowCastingMode.On && Mathf.Max(s.x, Mathf.Max(s.y, s.z)) < 0.7f) { r.shadowCastingMode = ShadowCastingMode.Off; sh++; }
        }
        Log("objetos pequenos sem sombra: " + sh);

        // v2: a sonda de reflexo fica como está (realtime quebrava o SSR do Render Graph e pesava a GPU)
        Log("correções feitas (lote estático desfeito em " + n1 + " objetos)");
    }

    // O AssetRipper deixou 1.231 objetos apontando para "Combined Mesh (root: scene) N" com só um pedaço deles valendo.
    // Isso quebra caixa de limite (culling e sombra nunca descartam), impede mover objeto e duplica desenho.
    // Aqui cada objeto ganha a própria malha, com os vértices de volta no espaço local.
    static int DesfazerLoteEstatico(GameObject root)
    {
        int n = 0, falhas = 0;
        var cache = new Dictionary<string, Mesh>();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var so = new SerializedObject(mr);
                var spCount = so.FindProperty("m_StaticBatchInfo.subMeshCount");
                var spFirst = so.FindProperty("m_StaticBatchInfo.firstSubMesh");
                if (spCount == null || spCount.intValue == 0) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh) continue;
                var src = mf.sharedMesh;
                int first = spFirst.intValue, cnt = spCount.intValue;
                if (first + cnt > src.subMeshCount) { falhas++; continue; }
                try
                {
                    var nova = Extrair(src, first, cnt, mr.transform);
                    nova.name = mr.name + "_vj" + n;
                    string path = Pasta + "/Malhas/" + Sanitizar(nova.name) + ".asset";
                    AssetDatabase.CreateAsset(nova, AssetDatabase.GenerateUniqueAssetPath(path));
                    foreach (var mc in mr.GetComponents<MeshCollider>()) if (mc.sharedMesh == src) mc.sharedMesh = nova;
                    mf.sharedMesh = nova;
                    spCount.intValue = 0; spFirst.intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    n++;
                }
                catch (Exception e) { falhas++; if (falhas < 10) Log("falha ao desfazer lote em " + mr.name + ": " + e.Message); }
            }
        }
        finally { AssetDatabase.StopAssetEditing(); }
        foreach (var lg in root.GetComponentsInChildren<LODGroup>(true)) lg.RecalculateBounds();
        Log("lote estático desfeito: " + n + " objetos · falhas: " + falhas);
        return n;
    }

    static string Sanitizar(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace(':', '_'); }

    static Mesh Extrair(Mesh src, int first, int cnt, Transform t)
    {
        var v = src.vertices; var nr = src.normals; var tg = src.tangents; var cl = src.colors;
        var uvs = new List<Vector2>[4];
        for (int c = 0; c < 4; c++) { uvs[c] = new List<Vector2>(); src.GetUVs(c, uvs[c]); }
        var mapa = new Dictionary<int, int>();
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nt = new List<Vector4>(); var nc = new List<Color>();
        var nuv = new List<Vector2>[4]; for (int c = 0; c < 4; c++) nuv[c] = new List<Vector2>();
        var subs = new List<int[]>();
        var w2l = t.worldToLocalMatrix; var l2wT = t.localToWorldMatrix.transpose;
        bool espelho = t.localToWorldMatrix.determinant < 0;
        for (int s = first; s < first + cnt; s++)
        {
            var idx = src.GetIndices(s);
            var ni = new int[idx.Length];
            for (int k = 0; k < idx.Length; k++)
            {
                int o = idx[k];
                if (!mapa.TryGetValue(o, out int q))
                {
                    q = nv.Count; mapa[o] = q;
                    nv.Add(w2l.MultiplyPoint3x4(v[o]));
                    if (nr.Length == v.Length) nn.Add(l2wT.MultiplyVector(nr[o]).normalized);
                    if (tg.Length == v.Length) { var d = w2l.MultiplyVector(new Vector3(tg[o].x, tg[o].y, tg[o].z)).normalized; nt.Add(new Vector4(d.x, d.y, d.z, espelho ? -tg[o].w : tg[o].w)); }
                    if (cl.Length == v.Length) nc.Add(cl[o]);
                    for (int c = 0; c < 4; c++) if (uvs[c].Count == v.Length) nuv[c].Add(uvs[c][o]);
                }
                ni[k] = q;
            }
            subs.Add(ni);
        }
        var m = new Mesh();
        m.indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        m.SetVertices(nv);
        if (nn.Count == nv.Count) m.SetNormals(nn);
        if (nt.Count == nv.Count) m.SetTangents(nt);
        if (nc.Count == nv.Count) m.SetColors(nc);
        for (int c = 0; c < 4; c++) if (nuv[c].Count == nv.Count) m.SetUVs(c, nuv[c]);
        m.subMeshCount = subs.Count;
        for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s, false);
        m.RecalculateBounds();
        if (nn.Count != nv.Count) m.RecalculateNormals();
        return m;
    }

    static int ConsertarRoxos(MeshRenderer[] mrs)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var feitos = new Dictionary<Material, Material>(); int n = 0;
        foreach (var mr in mrs)
        {
            var ms = mr.sharedMaterials; bool mudou = false;
            for (int i = 0; i < ms.Length; i++)
            {
                var m = ms[i]; if (!m) continue;
                if (m.shader && m.shader.name != "Hidden/InternalErrorShader" && m.shader.isSupported) continue;
                if (!feitos.TryGetValue(m, out var novo))
                {
                    novo = new Material(lit) { name = m.name + "_vjlit" };
                    var tex = PegarTex(m, "baseColorTexture", "_BaseMap", "_MainTex");
                    if (tex) novo.SetTexture("_BaseMap", tex);
                    novo.SetColor("_BaseColor", PegarCor(m, "baseColorFactor", "_BaseColor", "_Color", Color.white));
                    AssetDatabase.CreateAsset(novo, AssetDatabase.GenerateUniqueAssetPath(Pasta + "/" + Sanitizar(novo.name) + ".mat"));
                    feitos[m] = novo; n++;
                }
                ms[i] = novo; mudou = true;
            }
            if (mudou) mr.sharedMaterials = ms;
        }
        return n;
    }

    static Texture PegarTex(Material m, params string[] nomes) { foreach (var p in nomes) if (m.HasProperty(p) && m.GetTexture(p)) return m.GetTexture(p); return null; }
    static Color PegarCor(Material m, string a, string b, string c, Color def) { foreach (var p in new[] { a, b, c }) if (m.HasProperty(p)) return m.GetColor(p); return def; }

    static void ChaoDeSeguranca(GameObject root)
    {
        var velho = root.transform.Find("VJ2_ChaoSeguranca"); if (velho) UnityEngine.Object.DestroyImmediate(velho.gameObject);
        string fb = FbDataCaminhos().FirstOrDefault(File.Exists); if (fb == null) return;
        int a, b; var pts = LerPontos(BlocoVJ(File.ReadAllText(fb), out a, out b) ?? "");
        if (pts.Count == 0) return;
        var c = Media(pts.Select(p => p.unity).ToList());
        int furos = 0;
        for (float x = c.x - 260; x <= c.x + 260; x += 4f)
            for (float z = c.z - 260; z <= c.z + 260; z += 4f)
                if (!Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore)) furos++;
        if (furos == 0) { Log("chão sem buracos: chão de segurança não foi preciso"); return; }
        var g = new GameObject("VJ2_ChaoSeguranca"); g.layer = 8; g.transform.SetParent(root.transform, false);
        g.transform.position = new Vector3(c.x, -0.35f, c.z);
        var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(620f, 0.2f, 620f);
        Log("chão de segurança invisível criado (havia " + furos + " furos em grade de 4 m)");
    }

    static void CorrigirPontos()
    {
        int movidos = 0, semJeito = 0;
        foreach (var fb in FbDataCaminhos())
        {
            if (!File.Exists(fb)) continue;
            string json = File.ReadAllText(fb);
            int i0, i1; var bloco = BlocoVJ(json, out i0, out i1); if (bloco == null) continue;
            var novoBloco = bloco;
            var trocas = new Dictionary<string, string>();
            foreach (var p in LerPontos(bloco))
            {
                RaycastHit h;
                if (!ChaoAbaixo(p.unity + Vector3.up * 2.5f, 12f, null, out h)) continue;
                var pe = h.point + Vector3.up * 0.05f;
                if (CapsulaLivre(pe)) continue;
                if (AcharLugarLivre(pe, 3f, out var livre))
                {
                    string novo = string.Format(CI, "[{0:0.##}, {1:0.##}, {2:0.##}]", -livre.x, Mathf.Max(p.web.y, livre.y), livre.z);
                    trocas[p.texto] = novo;
                }
                else semJeito++;
            }
            foreach (var kv in trocas) { novoBloco = novoBloco.Replace(kv.Key, kv.Value); }
            if (trocas.Count > 0)
            {
                File.WriteAllText(fb, json.Substring(0, i0) + novoBloco + json.Substring(i1 + 1));
                movidos = Math.Max(movidos, trocas.Count);
                Log("fb_data " + (fb.Contains("StreamingAssets") ? "StreamingAssets" : "Resources") + ": " + trocas.Count + " pontos tirados de dentro de objetos");
            }
        }
        Log("pontos corrigidos: " + movidos + " · sem lugar livre a 3 m: " + semJeito);
    }

    static void IgualarCobertura(GameObject root)
    {
        foreach (Transform t in root.transform.Cast<Transform>().ToList()) if (t.name.StartsWith("VJ2_Cobertura")) UnityEngine.Object.DestroyImmediate(t.gameObject);
        Physics.SyncTransforms();
        string fb = FbDataCaminhos().FirstOrDefault(File.Exists); if (fb == null) return;
        int a, b; var pts = LerPontos(BlocoVJ(File.ReadAllText(fb), out a, out b) ?? "");
        var sa = pts.Where(p => p.lista == "spawnA").Select(p => p.unity).ToList();
        var sb2 = pts.Where(p => p.lista == "spawnB").Select(p => p.unity).ToList();
        if (sa.Count == 0 || sb2.Count == 0) return;
        Vector3 cA = Media(sa), cB = Media(sb2);
        int covA = Cobertura(cA, cB), covB = Cobertura(cB, cA);
        Log("cobertura antes · base A " + covA + " · base B " + covB);
        if (Mathf.Abs(covA - covB) < 3) return;
        var fraca = covA < covB ? cA : cB; var forte = covA < covB ? cB : cA; string nome = covA < covB ? "A" : "B";
        var waypts = pts.Where(p => p.lista == "waypoints" || p.lista == "bots" || p.lista == "alleys").Select(p => p.unity).ToList();

        // moldes: as próprias peças de cobertura do mapa
        var moldes = new List<GameObject>();
        foreach (Transform t in root.transform)
            if (Regex.IsMatch(t.name, "^VJ_Cor_(jersey|sacos)") || Regex.IsMatch(t.name, "^VJ_Cob_conteiner")) moldes.Add(t.gameObject);
        if (moldes.Count == 0) { Log("sem moldes de cobertura (VJ_Cor_jersey / VJ_Cor_sacos)"); return; }
        var jersey = moldes.FirstOrDefault(m => m.name.Contains("jersey")) ?? moldes[0];
        var sacos = moldes.FirstOrDefault(m => m.name.Contains("sacos")) ?? jersey;

        var dir = forte - fraca; dir.y = 0; dir.Normalize();
        var lado = Vector3.Cross(Vector3.up, dir);
        // flancos: deixa livre o corredor do meio (±25°) por onde os bots saem
        var alvos = new[] { (-60f, 9f, sacos), (-40f, 12f, jersey), (40f, 12f, jersey), (60f, 9f, sacos), (-75f, 15f, jersey), (75f, 15f, jersey), (-35f, 18f, sacos), (35f, 18f, sacos) };
        int postos = 0; int precisa = Mathf.Abs(covA - covB);
        foreach (var (ang, dist, molde) in alvos)
        {
            if (postos >= precisa) break;
            var d = Quaternion.AngleAxis(ang, Vector3.up) * dir;
            var p = fraca + d * dist;
            RaycastHit h;
            if (!ChaoAbaixo(p + Vector3.up * 3f, 8f, null, out h)) continue;
            if (waypts.Any(w => (new Vector2(w.x - p.x, w.z - p.z)).magnitude < 2.2f)) continue;
            var bm = BoundsDe(molde); if (bm == null) continue;
            var mfm = molde.GetComponentInChildren<MeshFilter>();
            bool longoX = mfm == null || mfm.sharedMesh == null || mfm.sharedMesh.bounds.size.x >= mfm.sharedMesh.bounds.size.z;
            var rot = Quaternion.LookRotation(longoX ? dir : lado, Vector3.up);
            float meiaAlt = bm.Value.extents.y; float meiaLarg = Mathf.Max(bm.Value.extents.x, bm.Value.extents.z);
            var meio = new Vector3(p.x, h.point.y + meiaAlt + 0.05f, p.z);
            if (Physics.CheckBox(meio, new Vector3(meiaLarg + 0.4f, meiaAlt * 0.9f, 1.0f), Quaternion.LookRotation(dir), ~0, QueryTriggerInteraction.Ignore)) continue;
            var c = UnityEngine.Object.Instantiate(molde, root.transform);
            c.name = "VJ2_Cobertura_" + nome + "_" + postos;
            c.transform.rotation = rot;
            c.transform.position = p;
            Physics.SyncTransforms();
            var bc2 = BoundsDe(c);
            if (bc2 != null) c.transform.position += new Vector3(p.x - bc2.Value.center.x, h.point.y - bc2.Value.min.y, p.z - bc2.Value.center.z);
            Physics.SyncTransforms(); postos++;
        }
        Log("cobertura posta na base " + nome + ": " + postos + " peças (as rotas dos bots e o meio ficaram livres) · depois: base A " + Cobertura(cA, cB) + " · base B " + Cobertura(cB, cA));
    }


    // ------------------------------------------------------------------ DESEMPENHO (v2.2)
    // A base do time 0 olhando para a base 1 dava 30 a 50 ms de GPU: os galpões têm móveis internos de 12 a 76 mil
    // triângulos cada (caixa plástica 75.920) que são desenhados e entram na sombra mesmo a 150 m, onde ninguém vê.
    // Aqui cada móvel interno e cada objeto pequeno pesado ganha um LODGroup próprio que o esconde de longe,
    // e móvel interno deixa de fazer sombra (fica sob telhado). Paredes, telhados, *_LONGE e coberturas de jogo não mudam.
    static readonly Regex Interior = new Regex("_moveis_interior|_interior$|_interior/", RegexOptions.IgnoreCase);
    static readonly Regex NuncaEsconder = new Regex("LONGE|Cor_|Cob_|sacos|jersey|conteiner|Parede|Muro|Telhado|Roof|Wall|Chao|Piso|Ruas", RegexOptions.IgnoreCase);

    public static void Otimizar()
    {
        var root = RaizMapa(); if (!root) return;
        var jaEmLod = new HashSet<Renderer>();
        foreach (var lg in root.GetComponentsInChildren<LODGroup>(true))
            foreach (var l in lg.GetLODs()) foreach (var r in l.renderers) if (r) jaEmLod.Add(r);
        // distância em que some (m), já contando que o jogo usa lodBias perto de 1; FOV vertical ~60°
        const float distInterior = 35f, distPequeno = 80f;
        float k = 2f * Mathf.Tan(30f * Mathf.Deg2Rad);
        int nInt = 0, nPeq = 0, nSombra = 0; long trisInt = 0, trisPeq = 0;
        foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (jaEmLod.Contains(mr)) continue;
            if (mr.GetComponent<LODGroup>()) continue;
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            string cam = Caminho(mr.transform);
            if (NuncaEsconder.IsMatch(mr.name)) continue;
            int tris = Tris(mf.sharedMesh);
            var tam = mf.sharedMesh.bounds.size; tam = Vector3.Scale(tam, mr.transform.lossyScale);
            float lado = Mathf.Max(Mathf.Abs(tam.x), Mathf.Max(Mathf.Abs(tam.y), Mathf.Abs(tam.z)));
            bool interior = Interior.IsMatch(cam);
            bool pequenoPesado = !interior && lado < 4f && tris > 3000;
            if (!interior && !pequenoPesado) continue;
            if (interior && lado > 12f) continue; // peça grande dentro de "interior" (piso, parede interna): fica
            float dist = interior ? distInterior : distPequeno;
            var lg = mr.gameObject.AddComponent<LODGroup>();
            float altura = Mathf.Clamp(lado / (dist * k), 0.002f, 0.9f);
            // 2 níveis (o 2º vazio): o FBQuality desliga LODGroup com 1 nível só nos mapas
            lg.SetLODs(new[] { new LOD(altura, new Renderer[] { mr }), new LOD(altura * 0.5f, new Renderer[0]) });
            lg.fadeMode = LODFadeMode.None;
            lg.RecalculateBounds();
            if (interior) { nInt++; trisInt += tris; if (mr.shadowCastingMode != ShadowCastingMode.Off) { mr.shadowCastingMode = ShadowCastingMode.Off; nSombra++; } }
            else { nPeq++; trisPeq += tris; }
        }
        Log(string.Format(CI, "desempenho: {0} móveis internos somem além de {1} m ({2:N0} triângulos) · {3} objetos pequenos pesados somem além de {4} m ({5:N0} triângulos) · {6} móveis internos sem sombra",
            nInt, distInterior, trisInt, nPeq, distPequeno, trisPeq, nSombra));
    }

    // ------------------------------------------------------------------ POLIMENTO
    public static void Polir()
    {
        var root = RaizMapa(); if (!root) return;
        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets", "BuritiroVJ");
        if (!AssetDatabase.IsValidFolder(Pasta + "/Materiais")) AssetDatabase.CreateFolder(Pasta, "Materiais");
        PrepararTexturas();
        var mrs = root.GetComponentsInChildren<MeshRenderer>(true);
        var dens = DensidadeUV(mrs);

        // pisos: troca completa por PBR 2K, mantendo a cor média do original
        int p1 = 0;
        p1 += TrocarPiso(mrs, dens, "^VJ_dirt$|^VJ_dirt_yard$|^VJ_dirt_road$", "terra", 5.5f, 0.15f); // v2.1: repetição maior (manchas repetidas de cima)
        p1 += TrocarPiso(mrs, dens, "^VJ_asphalt$|^VJ_road_worn$", "asfalto", 5.0f, 0.25f);
        p1 += TrocarPiso(mrs, dens, "^VJ_sidewalk$|^VJ_concrete_slabs$|^VJ_concrete_floor$", "calcada", 2.5f, 0.2f);
        Log("pisos trocados por PBR 2K: " + p1 + " materiais");
        // v2: grama mantém a textura e o verde do mapa; só ganha relevo e manchas leves que quebram a repetição
        int pg = Detalhar(mrs, dens, "^VJ_grass$|^VJ_grass_dry$", "reboco_sujo", 3.0f, 0.08f, 0.45f, 0.6f);
        Log("grama com relevo (cor original): " + pg + " materiais");

        // paredes e pinturas: mantém a cor/textura, ganha detalhe de reboco (relevo e sujeira) por metro
        int p2 = 0;
        p2 += Detalhar(mrs, dens, "^VJ_wall|^VJ_white_paint|^VJ_yellow_paint|^VJ_band_|^VJ_acc_|^VJ_tile_wall|^VJ_int_wall|^VJ_blockwall|^VJ_wall_cream", "reboco", 1.4f, 0.18f, 0.5f, 0.45f);
        p2 += Detalhar(mrs, dens, "^VJ_concrete|^VJ_pole|^VJ_sill", "concreto", 1.2f, 0.22f, 0.6f, 0.5f);
        p2 += Detalhar(mrs, dens, "^VJ_roof", "telha", 2.0f, 0.25f, 0.6f, 0.6f);
        Log("materiais com detalhe de reboco/concreto/telha: " + p2);

        Iluminacao();
        Log("polimento feito");
    }

    static readonly string[] Conjuntos = { "terra", "grama", "asfalto", "calcada" };
    static readonly string[] Detalhes = { "reboco", "reboco_sujo", "concreto", "telha" };

    static void PrepararTexturas()
    {
        AssetDatabase.Refresh();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PastaTex }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(p) as TextureImporter; if (imp == null) continue;
            string n = Path.GetFileNameWithoutExtension(p);
            bool normal = n.EndsWith("_normal");
            bool dado = n.EndsWith("_metal_liso") || n.EndsWith("_ao");
            bool mudou = false;
            if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; mudou = true; }
            if (dado && imp.sRGBTexture) { imp.sRGBTexture = false; mudou = true; }
            if (n.StartsWith("VJ2_det_") && !normal && imp.sRGBTexture) { imp.sRGBTexture = false; mudou = true; }
            if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; mudou = true; }
            if (imp.wrapMode != TextureWrapMode.Repeat) { imp.wrapMode = TextureWrapMode.Repeat; mudou = true; }
            if (imp.anisoLevel < 8) { imp.anisoLevel = 8; mudou = true; }
            if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; mudou = true; }
            if (mudou) imp.SaveAndReimport();
        }
    }

    static Texture2D Tex(string nome)
    {
        foreach (var ext in new[] { ".jpg", ".png" })
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(PastaTex + "/" + nome + ext);
            if (t) return t;
        }
        Log("textura não achada: " + nome);
        return null;
    }

    // metros por unidade de UV de cada material (mediana dos triângulos)
    static Dictionary<Material, float> DensidadeUV(MeshRenderer[] mrs)
    {
        var amostras = new Dictionary<Material, List<float>>();
        foreach (var mr in mrs)
        {
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var m = mf.sharedMesh; var ms = mr.sharedMaterials;
            Vector3[] v; Vector2[] uv;
            try { v = m.vertices; uv = m.uv; } catch { continue; }
            if (uv == null || uv.Length != v.Length) continue;
            var l2w = mr.transform.localToWorldMatrix;
            for (int s = 0; s < Mathf.Min(m.subMeshCount, ms.Length); s++)
            {
                var mat = ms[s]; if (!mat) continue;
                if (!amostras.TryGetValue(mat, out var lst)) { lst = new List<float>(); amostras[mat] = lst; }
                if (lst.Count > 400) continue;
                var idx = m.GetTriangles(s);
                int passo = Mathf.Max(3, (idx.Length / 3 / 40) * 3);
                for (int k = 0; k + 2 < idx.Length; k += passo)
                {
                    Vector3 a = l2w.MultiplyPoint3x4(v[idx[k]]), b = l2w.MultiplyPoint3x4(v[idx[k + 1]]), c = l2w.MultiplyPoint3x4(v[idx[k + 2]]);
                    float aw = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    Vector2 ua = uv[idx[k]], ub = uv[idx[k + 1]], uc = uv[idx[k + 2]];
                    float au = Mathf.Abs((ub.x - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (ub.y - ua.y)) * 0.5f;
                    if (aw < 1e-4f || au < 1e-9f) continue;
                    lst.Add(Mathf.Sqrt(aw / au));
                }
            }
        }
        var res = new Dictionary<Material, float>();
        foreach (var kv in amostras) if (kv.Value.Count > 0) { var o = kv.Value.OrderBy(x => x).ToList(); res[kv.Key] = o[o.Count / 2]; }
        return res;
    }

    static Color MediaTextura(Texture t)
    {
        if (!t) return Color.white;
        var p = AssetDatabase.GetAssetPath(t);
        if (string.IsNullOrEmpty(p) || !File.Exists(p)) return Color.white;
        var tmp = new Texture2D(2, 2);
        try
        {
            if (!tmp.LoadImage(File.ReadAllBytes(p))) return Color.white;
            var px = tmp.GetPixels32(); double r = 0, g = 0, b = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 97) { r += px[i].r; g += px[i].g; b += px[i].b; n++; }
            return new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255), 1);
        }
        finally { UnityEngine.Object.DestroyImmediate(tmp); }
    }

    // nome único por material antigo: há dois VJ_concrete, dois VJ_white_paint etc. com o mesmo nome
    static string NomeNovo(Material velho)
    {
        var g = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(velho));
        string suf = string.IsNullOrEmpty(g) ? ((uint)velho.GetHashCode()).ToString("x8") : g.Substring(0, 8);
        return "VJ3_" + velho.name + "_" + suf;
    }

    static Material NovoLit(string nome)
    {
        var path = Pasta + "/Materiais/" + nome + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static int TrocarMaterial(MeshRenderer[] mrs, Material velho, Material novo)
    {
        int n = 0;
        foreach (var mr in mrs)
        {
            var ms = mr.sharedMaterials; bool mudou = false;
            for (int i = 0; i < ms.Length; i++) if (ms[i] == velho) { ms[i] = novo; mudou = true; n++; }
            if (mudou) mr.sharedMaterials = ms;
        }
        return n;
    }

    static IEnumerable<Material> MateriaisPorNome(MeshRenderer[] mrs, string padrao)
    {
        var re = new Regex(padrao, RegexOptions.IgnoreCase);
        return mrs.SelectMany(r => r.sharedMaterials).Where(m => m && re.IsMatch(m.name) && !m.name.StartsWith("VJ2_") && !m.name.StartsWith("VJ3_")).Distinct().ToList();
    }

    static int TrocarPiso(MeshRenderer[] mrs, Dictionary<Material, float> dens, string padrao, string conj, float metrosPorRepeticao, float lisura)
    {
        int n = 0;
        foreach (var velho in MateriaisPorNome(mrs, padrao))
        {
            var cor = Tex("VJ2_" + conj + "_cor"); if (!cor) return 0;
            var m = NovoLit(NomeNovo(velho));
            m.SetTexture("_BaseMap", cor);
            // tinta: puxa a cor média do PBR para a do original (70%), mantendo a direção de arte do mapa
            var orig = MediaTextura(PegarTex(velho, "baseColorTexture", "_BaseMap", "_MainTex"));
            var fat = PegarCor(velho, "baseColorFactor", "_BaseColor", "_Color", Color.white);
            orig = new Color(orig.r * fat.r, orig.g * fat.g, orig.b * fat.b, 1);
            var nova = MediaTextura(cor);
            var tinta = new Color(Mathf.Clamp(orig.r / Mathf.Max(0.05f, nova.r), 0.55f, 1.7f), Mathf.Clamp(orig.g / Mathf.Max(0.05f, nova.g), 0.55f, 1.7f), Mathf.Clamp(orig.b / Mathf.Max(0.05f, nova.b), 0.55f, 1.7f), 1);
            m.SetColor("_BaseColor", Color.Lerp(Color.white, tinta, 0.7f));
            var nrm = Tex("VJ2_" + conj + "_normal"); if (nrm) { m.SetTexture("_BumpMap", nrm); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            var met = Tex("VJ2_" + conj + "_metal_liso");
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", lisura);
            if (met) { m.SetTexture("_MetallicGlossMap", met); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 0.8f); }
            var ao = Tex("VJ2_" + conj + "_ao"); if (ao) { m.SetTexture("_OcclusionMap", ao); m.SetFloat("_OcclusionStrength", 0.8f); m.EnableKeyword("_OCCLUSIONMAP"); }
            float mpu = dens.TryGetValue(velho, out var d) ? d : metrosPorRepeticao;
            float rep = Mathf.Clamp(mpu / metrosPorRepeticao, 0.05f, 400f);
            m.SetTextureScale("_BaseMap", new Vector2(rep, rep));
            // quebra a repetição com o detalhe do mesmo conjunto 3,7x maior
            var det = Tex("VJ2_det_" + (conj == "grama" || conj == "terra" ? "reboco_sujo" : "concreto"));
            if (det) { m.SetTexture("_DetailAlbedoMap", det); float macro = conj == "terra" ? 6.3f : 3.7f; m.SetFloat("_DetailAlbedoMapScale", conj == "terra" ? 0.85f : 0.6f); m.SetTextureScale("_DetailAlbedoMap", new Vector2(rep / macro, rep / macro)); m.EnableKeyword("_DETAIL_MULX2"); m.EnableKeyword("_DETAIL_SCALED"); }
            EditorUtility.SetDirty(m);
            int k = TrocarMaterial(mrs, velho, m);
            Log(string.Format(CI, "piso {0} -> {1} · {2:0.00} m por UV · repetição {3:0.0} · {4} usos", velho.name, m.name, mpu, rep, k));
            n++;
        }
        return n;
    }

    static int Detalhar(MeshRenderer[] mrs, Dictionary<Material, float> dens, string padrao, string det, float metrosPorRepeticao, float lisura, float forcaCor, float forcaRelevo)
    {
        int n = 0;
        var da = Tex("VJ2_det_" + det); var dn = Tex("VJ2_det_" + det + "_normal");
        if (!da || !dn) return 0;
        foreach (var velho in MateriaisPorNome(mrs, padrao))
        {
            var m = NovoLit(NomeNovo(velho));
            var tex = PegarTex(velho, "baseColorTexture", "_BaseMap", "_MainTex");
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", PegarCor(velho, "baseColorFactor", "_BaseColor", "_Color", Color.white));
            var nrm = PegarTex(velho, "normalTexture", "_BumpMap");
            if (nrm) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
            if (velho.HasProperty("_BaseMap")) { m.SetTextureScale("_BaseMap", velho.GetTextureScale("_BaseMap")); m.SetTextureOffset("_BaseMap", velho.GetTextureOffset("_BaseMap")); }
            else if (velho.HasProperty("baseColorTexture")) { m.SetTextureScale("_BaseMap", velho.GetTextureScale("baseColorTexture")); m.SetTextureOffset("_BaseMap", velho.GetTextureOffset("baseColorTexture")); }
            m.SetFloat("_Metallic", 0f); m.SetFloat("_Smoothness", lisura);
            float mpu = dens.TryGetValue(velho, out var d) ? d : 1f;
            float rep = Mathf.Clamp(mpu / metrosPorRepeticao, 0.05f, 400f);
            m.SetTexture("_DetailAlbedoMap", da); m.SetTexture("_DetailNormalMap", dn);
            // URP só respeita a força do detalhe com _DETAIL_SCALED; sem ele o detalhe vai com força total
            m.SetFloat("_DetailAlbedoMapScale", forcaCor); m.SetFloat("_DetailNormalMapScale", forcaRelevo);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(rep, rep));
            m.EnableKeyword("_DETAIL_MULX2");
            if (forcaCor != 1f) m.EnableKeyword("_DETAIL_SCALED"); else m.DisableKeyword("_DETAIL_SCALED");
            // lisura vem da rugosidade do glTF quando existe
            if (velho.HasProperty("roughnessFactor")) m.SetFloat("_Smoothness", Mathf.Clamp(1f - velho.GetFloat("roughnessFactor"), 0.02f, 0.5f));
            // transparência e recorte do original
            if (velho.HasProperty("alphaCutoff") && velho.GetFloat("alphaCutoff") > 0f) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", velho.GetFloat("alphaCutoff")); m.EnableKeyword("_ALPHATEST_ON"); }
            m.SetFloat("_Cull", velho.HasProperty("_Cull") ? velho.GetFloat("_Cull") : 2f);
            m.renderQueue = velho.renderQueue < 2450 ? -1 : velho.renderQueue;
            EditorUtility.SetDirty(m);
            int k = TrocarMaterial(mrs, velho, m);
            Log(string.Format(CI, "detalhe {0} em {1} · {2:0.00} m por UV · repetição {3:0.0} · {4} usos", det, velho.name, mpu, rep, k));
            n++;
        }
        return n;
    }

    // sol de Boa Vista: quente e alto; céu claro; névoa mais longe e menos azul; luz que volta do chão vermelho
    static void Iluminacao()
    {
        var sol = RenderSettings.sun ? RenderSettings.sun : UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sol)
        {
            sol.color = new Color(1f, 0.945f, 0.86f);
            sol.intensity = 1.5f;
            var e = sol.transform.eulerAngles;
            float alt = e.x > 180 ? e.x - 360 : e.x;
            if (alt < 38f || alt > 62f) sol.transform.rotation = Quaternion.Euler(50f, e.y, 0f);
            sol.shadows = LightShadows.Soft;
            sol.shadowNormalBias = 0.3f;
            EditorUtility.SetDirty(sol);
            Log("sol: cor quente, intensidade 1,5, altura " + V(sol.transform.eulerAngles));
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.70f, 0.79f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.66f, 0.64f, 0.60f);
        RenderSettings.ambientGroundColor = new Color(0.46f, 0.36f, 0.29f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.74f, 0.79f, 0.85f);
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 448f; // v2: mesmo fim do original (o jogo pode cortar desenho pela névoa)

        // volume próprio do mapa: balanço de branco levemente quente e sombras com um toque frio
        var vg = GameObject.Find("VJ2_Visual") ?? new GameObject("VJ2_Visual");
        var vol = vg.GetComponent<Volume>();
        if (vol == null) vol = vg.AddComponent<Volume>();
        vol.isGlobal = true; vol.priority = 60f; vol.weight = 1f;
        var path = Pasta + "/VJ2_Visual.asset";
        var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (!prof) { prof = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(prof, path); }
        if (!prof.TryGet<WhiteBalance>(out var wb)) { wb = prof.Add<WhiteBalance>(true); AssetDatabase.AddObjectToAsset(wb, prof); }
        wb.temperature.Override(3f); wb.tint.Override(1f); // v2.1: menos amarelo na névoa vista de cima
        if (!prof.TryGet<ShadowsMidtonesHighlights>(out var smh)) { smh = prof.Add<ShadowsMidtonesHighlights>(true); AssetDatabase.AddObjectToAsset(smh, prof); }
        smh.shadows.Override(new Vector4(0.97f, 0.99f, 1.04f, 0f));
        smh.highlights.Override(new Vector4(1.03f, 1.0f, 0.96f, 0f));
        EditorUtility.SetDirty(prof);
        vol.sharedProfile = prof;
        Log("ambiente trilight, névoa 90-448 m, volume VJ2_Visual (balanço de branco +3)");
    }

    // ------------------------------------------------------------------ FOTOS (Editor)
    public static void TirarFotos(string tag)
    {
        string fb = FbDataCaminhos().FirstOrDefault(File.Exists);
        var pts = fb != null ? LerPontos(BlocoVJ(File.ReadAllText(fb), out _, out _) ?? "") : new List<Ponto>();
        var sa = pts.Where(p => p.lista == "spawnA").Select(p => p.unity).ToList();
        var sb2 = pts.Where(p => p.lista == "spawnB").Select(p => p.unity).ToList();
        Vector3 cA = sa.Count > 0 ? Media(sa) : new Vector3(79, 0, -31);
        Vector3 cB = sb2.Count > 0 ? Media(sb2) : new Vector3(-112, 0, -55);
        var meio = (cA + cB) * 0.5f;
        var vistas = new List<(string, Vector3, Vector3)>
        {
            ("1_base_A_olhando_inimigo", cA + Vector3.up * 1.7f, cB + Vector3.up * 1.5f),
            ("2_base_B_olhando_inimigo", cB + Vector3.up * 1.7f, cA + Vector3.up * 1.5f),
            ("3_meio_rua", meio + Vector3.up * 1.7f + Vector3.forward * 4f, meio + Vector3.forward * 60f + Vector3.up * 2f),
            ("4_meio_casas", meio + Vector3.up * 1.7f + Vector3.right * 6f, meio + Vector3.right * 60f + Vector3.up * 4f),
            ("5_aerea_base_A", cA + new Vector3(-15, 35, -25), cA),
            ("6_aerea_mapa", meio + new Vector3(0, 140, -160), meio),
            ("7_parede_perto", cA + Vector3.up * 1.6f, cA + (cB - cA).normalized * 4f + Vector3.up * 1.4f),
        };
        string dir = Path.Combine(Fotos, tag); Directory.CreateDirectory(dir);
        var go = new GameObject("VJ_FotoCam"); go.hideFlags = HideFlags.HideAndDontSave;
        var cam = go.AddComponent<Camera>(); cam.fieldOfView = 70; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500f;
        var ucd = go.AddComponent<UniversalAdditionalCameraData>(); ucd.renderPostProcessing = true; ucd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1;
        cam.targetTexture = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            foreach (var (nome, de, para) in vistas)
            {
                go.transform.position = de; go.transform.rotation = Quaternion.LookRotation(para - de, Vector3.up);
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, nome + ".png"), tex.EncodeToPNG());
            }
        }
        finally
        {
            cam.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(go);
        }
        Log("fotos do Editor (" + tag + ") em " + dir);
    }
}
