// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/BuritiroPlantarArvores.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// BURITIRO · planta as árvores do pacote 2 (Recursos2) nos mapas. Pedido da thread de gráficos (31/10/2026), feito pela
// thread de física. Regras: só em grama, canteiro e praça; nunca em rua ou calçada estreita; pelo menos 3 m da borda da
// rua, 6 m entre as árvores e 6 m de spawns e objetivos; no máximo uns 40 por mapa.
//
// Lab (uma linha em _diag\lab_pedido.txt):
//   metodo BuritiroPlantarArvores.LevantarVila  /  LevantarPT   só relatório e mapa dos tipos de chão; não muda nada
//   metodo BuritiroPlantarArvores.PlantarVila   /  PlantarPT    planta, salva a cena (cópia antes em _backup) e tira fotos
//   metodo BuritiroPlantarArvores.TirarVila     /  TirarPT      tira o grupo BURITIRO_Arvores e salva a cena
// Saída em Buritiro_Unity\_diag\plantar\<mapa>\ (levantamento.txt, classes.png, relatorio.txt, arvores.csv, fotos).
//
// v2 (revisão de 31/10): o chão é classificado primeiro pelo material do triângulo acertado (MeshCollider + MeshRenderer
// no mesmo objeto) e só depois pelo nome do objeto (nunca pelo nome da raiz Map_*). Nos mapas do montador os colisores
// COL_x_y_<superfície> não têm renderer: aí o rótulo vem da malha visual de Map_<id>/Chao, com colisores temporários numa
// camada livre que somem antes de salvar. Ordem certa: montar o mapa, juntar o trecho no fb_data e só então plantar
// (remontar o mapa apaga as árvores; a semente fixa repete o mesmo resultado). As palavras de cada mapa podem ser trocadas sem recompilar em
// _diag\plantar_regras.txt, linhas "<mapa>.permitir=<regex>", "<mapa>.negar=<regex>", "<mapa>.max=<n>" ou "*.negar=...".
// A regra "negar" ganha da "permitir".
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FreeBala;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class BuritiroPlantarArvores
{
    const string PastaPrefab = "Assets/BURITIRO/Recursos2/Prefabs";
    const string NomeGrupo = "BURITIRO_Arvores";
    const float Passo = 2f;              // grade do levantamento (m)
    const float DistArvores = 6f;        // entre árvores (mínimo)
    const float DistPontos = 6f;         // de spawns, objetivos, bases, carros parados
    const float DistRota = 4f;           // das rotas do 10x10 e dos waypoints
    const float DistRua = 3f;            // da borda da rua
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    const string PermitirPadrao = @"gram|grass|lawn|canteiro|lavrado|planter";
    const string NegarPadrao = @"rua|asfalt|asphalt|road|street|estrada|calcad|calçad|sidewalk|pavement|passeio|ciclovia|cycle|faixa|meio.?fio|curb|sarjeta|guia|sinaliza|estacion|parking|quadra|court|borracha|terra_batida|dirt_road|telhad|roof|laje|agua|água|water|rio_|_rio|lago|igarap|ponte|bridge|trilho|escada|stair";
    static readonly Regex ArvoreMapa = new Regex("arvore|árvore|tree|palm|coqueiro|buriti|mangueira|caimbe|oiti|ipe_|nim_|cajueiro|jambeiro|castanh|flamboy|acacia|sibipiruna|selvinha|arbusto|bush", RegexOptions.IgnoreCase);

    static string Base => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
    // colisores temporários da malha visual do chão (mapas do montador) e a máscara que os deixa fora das outras consultas
    static int camadaVis = -1;
    static int Mascara = ~0;
    static readonly List<GameObject> chaoVis = new List<GameObject>();
    static readonly Dictionary<Collider, MeshRenderer> visRend = new Dictionary<Collider, MeshRenderer>();
    static StringBuilder sb;
    static string dir;
    static void L(string s) { sb.AppendLine(s); Debug.Log("[PLANTAR] " + s); }
    static string F(float v) => v.ToString("0.0", CI);
    static string V(Vector3 v) => "(" + F(v.x) + ", " + F(v.y) + ", " + F(v.z) + ")";

    public static void LevantarVila() => Rodar("vila_jardim", Modo.Levantar);
    public static void LevantarPT() => Rodar("praca_pt", Modo.Levantar);
    public static void PlantarVila() => Rodar("vila_jardim", Modo.Plantar);
    public static void PlantarPT() => Rodar("praca_pt", Modo.Plantar);
    public static void TirarVila() => Rodar("vila_jardim", Modo.Tirar);
    public static void TirarPT() => Rodar("praca_pt", Modo.Tirar);

    enum Modo { Levantar, Plantar, Tirar }
    enum Classe { Permitido, Negado, Outro, Inclinado, Nada }

    // ------------------------------------------------------------------
    static void Rodar(string id, Modo modo)
    {
        sb = new StringBuilder();
        dir = Path.Combine(Base, "_diag", "plantar", id);
        Directory.CreateDirectory(dir);
        string cena = "Assets/FreeBala/Scenes/Map_" + id + ".unity";
        string antes = EditorSceneManager.GetActiveScene().path;
        L($"{modo} {id} · {DateTime.Now:yyyy-MM-dd HH:mm:ss} · cena antes: {antes}");
        bool salvou = false;
        NavMeshDataInstance nav = default;
        try
        {
            if (!File.Exists(cena)) { L("cena não existe: " + cena); return; }
            var sc = EditorSceneManager.OpenScene(cena, OpenSceneMode.Single);
            Physics.SyncTransforms();
            if (modo == Modo.Tirar)
            {
                int n = TiraGrupo(sc);
                L($"grupos {NomeGrupo} tirados: {n}");
                if (n > 0) { Backup(cena); salvou = EditorSceneManager.SaveScene(sc); L("cena salva: " + salvou); }
                return;
            }
            var lvl = Nivel(id);
            if (lvl == null) { L("sem maps." + id + ".level no fb_data.json"); return; }
            var regras = Regras(id);
            L("permitir: " + regras.permitir + "\nnegar: " + regras.negar + "\nmáximo: " + regras.max);
            var area = Area(sc, lvl);
            L($"área: centro {V(area.center)} · lado {F(area.size.x)} x {F(area.size.z)} m");
            // NavMesh do mapa (no editor o FBNavData não liga sozinho)
            var nd = Object.FindObjectsByType<FBNavData>(FindObjectsInactive.Include).FirstOrDefault(n => n.data);
            if (nd) { nav = NavMesh.AddNavMeshData(nd.data); L("NavMesh do mapa carregado"); }
            else L("sem FBNavData na cena: o teste de chão andável fica desligado");
            bool temNav = nav.valid;

            cacheRot.Clear();
            CriaChaoVisual(sc, id);
            var lev = Levantamento(area, regras);
            if (modo == Modo.Levantar) { TiraChaoVisual(); Fotos(area, new List<Vector3>(), lev, false); return; }

            // planta (tira as de antes, para poder rodar de novo)
            int velhas = TiraGrupo(sc);
            Physics.SyncTransforms();
            var postas = Plantar(sc, area, lvl, regras, temNav);
            TiraChaoVisual();
            if (postas.Count > 0)
            {
                Backup(cena);
                salvou = EditorSceneManager.SaveScene(sc);
                L("cena salva: " + salvou + (velhas > 0 ? $" (troquei {velhas} grupo(s) {NomeGrupo} de antes)" : ""));
                Fotos(area, postas.Select(p => p.pos).ToList(), lev, true);
            }
            else
                L(velhas > 0
                    ? $"nenhuma árvore nova: cena NÃO salva, o grupo {NomeGrupo} de antes continua no arquivo (para tirar use TirarVila/TirarPT); sem fotos"
                    : "nenhuma árvore nova: cena não mudou; sem fotos");
        }
        catch (Exception e) { L("ERRO: " + e); }
        finally
        {
            TiraChaoVisual();
            if (nav.valid) nav.Remove();
            File.WriteAllText(Path.Combine(dir, modo == Modo.Levantar ? "levantamento.txt" : "relatorio.txt"), sb.ToString(), new UTF8Encoding(false));
            if (!salvou)
            {   // não salvou: reabre a cena de antes sem salvar nada
                string volta = string.IsNullOrEmpty(antes) ? cena : antes;
                EditorSceneManager.OpenScene(volta, OpenSceneMode.Single);
            }
        }
    }

    // ------------------------------------------------------------------ dados do fb_data
    static Dictionary<string, object> Nivel(string id)
    {
        string p = Path.Combine(Application.streamingAssetsPath, "fb_data.json");
        if (!File.Exists(p)) return null;
        var raiz = Json.Parse(File.ReadAllText(p));
        var mapa = Json.O(Json.O(raiz, "maps"), id);
        return Json.O(mapa, "level") ?? mapa;
    }

    // fb_data usa x invertido (Coords.ToWeb)
    static bool Ponto(object o, out Vector3 v)
    {
        v = default;
        if (!(o is List<object> l) || l.Count < 2 || !(l[0] is double) || !(l[1] is double)) return false;
        v = l.Count >= 3 && l[2] is double ? new Vector3(-Json.Fv(l[0]), Json.Fv(l[1]), Json.Fv(l[2])) : new Vector3(-Json.Fv(l[0]), 0f, Json.Fv(l[1]));
        return true;
    }

    // todos os pontos [x,y,z] dentro de uma chave (listas de pontos, listas de listas e objetos com "p")
    static List<Vector3> Pontos(object o)
    {
        var r = new List<Vector3>();
        void Junta(object x, int prof)
        {
            if (x == null || prof > 4) return;
            if (Ponto(x, out var v)) { r.Add(v); return; }
            if (x is List<object> l) foreach (var y in l) Junta(y, prof + 1);
            else if (x is Dictionary<string, object> d && d.TryGetValue("p", out var p)) Junta(p, prof + 1);
        }
        Junta(o, 0);
        return r;
    }

    static List<List<Vector3>> Linhas(object o)
    {
        var r = new List<List<Vector3>>();
        if (o is List<object> l)
            foreach (var x in l)
            {
                var pts = Pontos(x);
                if (pts.Count >= 2) r.Add(pts);
            }
        return r;
    }

    static Bounds Area(Scene sc, Dictionary<string, object> lvl)
    {
        var ar = Json.O(lvl, "arena");
        if (ar != null)
        {   // arena 10x10: o quadrado menos 15 m (as paredes ficam na borda)
            float half = Json.F(ar, "half", 300f) - 15f;
            var c = new Vector3(-Json.F(ar, "cx"), 0f, Json.F(ar, "cy"));
            return new Bounds(c, new Vector3(2f * half, 400f, 2f * half));
        }
        // sem arena: quadrado em volta das duas bases (lado = 1,5 x a distância entre elas)
        var a = Pontos(Json.Get(lvl, "spawnA"));
        var b = Pontos(Json.Get(lvl, "spawnB"));
        if (a.Count == 0 || b.Count == 0) return new Bounds(Vector3.zero, new Vector3(300f, 400f, 300f));
        Vector3 ca = a.Aggregate(Vector3.zero, (s, v) => s + v) / a.Count, cb = b.Aggregate(Vector3.zero, (s, v) => s + v) / b.Count;
        float lado = Mathf.Clamp(1.5f * new Vector2(ca.x - cb.x, ca.z - cb.z).magnitude, 120f, 500f);
        var m = (ca + cb) * 0.5f; m.y = 0f;
        return new Bounds(m, new Vector3(lado, 400f, lado));
    }

    // ------------------------------------------------------------------ regras de chão
    class Regra { public Regex rxP, rxN; public string permitir, negar; public int max = 40; }

    static Regra Regras(string id)
    {
        var r = new Regra { permitir = PermitirPadrao, negar = NegarPadrao };
        string f = Path.Combine(Base, "_diag", "plantar_regras.txt");
        if (File.Exists(f))
            foreach (var linha in File.ReadAllLines(f, Encoding.UTF8))
            {
                var t = linha.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                int i = t.IndexOf('=');
                if (i < 0) continue;
                string k = t.Substring(0, i).Trim(), v = t.Substring(i + 1).Trim();
                if (k == id + ".permitir" || k == "*.permitir") r.permitir = v;
                else if (k == id + ".negar" || k == "*.negar") r.negar = v;
                else if ((k == id + ".max" || k == "*.max") && int.TryParse(v, out int mx)) r.max = Mathf.Clamp(mx, 0, 80);
            }
        r.rxP = new Regex(r.permitir, RegexOptions.IgnoreCase);
        r.rxN = new Regex(r.negar, RegexOptions.IgnoreCase);
        return r;
    }

    class Rot { public string mat, nome, desc; }
    static readonly Dictionary<(Collider, int), Rot> cacheRot = new Dictionary<(Collider, int), Rot>();

    // material do triângulo acertado (submesh pelo triangleIndex); null se não há renderer
    static string MaterialDoHit(RaycastHit h, MeshRenderer mr, out int sub)
    {
        sub = -1;
        var mc = h.collider as MeshCollider;
        if (mr && mc && mc.sharedMesh && h.triangleIndex >= 0 && mc.sharedMesh.subMeshCount > 1)
        {
            int idx = h.triangleIndex * 3;
            for (int i = 0; i < mc.sharedMesh.subMeshCount; i++)
            {
                var d = mc.sharedMesh.GetSubMesh(i);
                if (idx >= d.indexStart && idx < d.indexStart + d.indexCount) { sub = i; break; }
            }
        }
        if (!mr) return null;
        var ms = mr.sharedMaterials;
        if (ms.Length == 0) return null;
        var m = ms[Mathf.Clamp(sub < 0 ? 0 : sub, 0, ms.Length - 1)];
        return m ? m.name : null;
    }

    // nome do objeto e do pai, mas nunca a raiz do mapa (Map_vila_jardim casava com "jardim")
    static string NomeObj(Transform t)
    {
        var pai = t.parent;
        bool usaPai = pai && pai.parent && !pai.name.StartsWith("Map_");
        return t.name + (usaPai ? " < " + pai.name : "");
    }

    static Rot Rotulo(RaycastHit h)
    {
        var col = h.collider;
        var mr = col.GetComponent<MeshRenderer>();
        string mat = MaterialDoHit(h, mr, out int sub);
        if (cacheRot.TryGetValue((col, sub), out var r0) && mr) return r0;
        var r = new Rot { mat = mat, nome = NomeObj(col.transform) };
        if (!mr && camadaVis >= 0)
        {   // colisor sem renderer (montador): rótulo da malha visual do chão no mesmo ponto
            var o = h.point + Vector3.up * 2f;
            if (Physics.Raycast(o, Vector3.down, out var hv, 4f, 1 << camadaVis, QueryTriggerInteraction.Collide)
                && Mathf.Abs(hv.point.y - h.point.y) < 0.35f && visRend.TryGetValue(hv.collider, out var vr) && vr)
            {
                r.mat = MaterialDoHit(hv, vr, out _);
                r.nome = NomeObj(vr.transform) + " (visual; colisor " + col.name + ")";
            }
        }
        r.desc = (r.mat != null ? "mat " + r.mat + " · " : "") + r.nome;
        if (mr) cacheRot[(col, sub)] = r;
        return r;
    }

    // primeiro o material; o nome do objeto só decide quando o material não diz nada
    static Classe Classifica(Rot rot, Regra r)
    {
        if (rot.mat != null)
        {
            if (r.rxN.IsMatch(rot.mat)) return Classe.Negado;
            if (r.rxP.IsMatch(rot.mat)) return Classe.Permitido;
        }
        if (r.rxN.IsMatch(rot.nome)) return Classe.Negado;
        if (r.rxP.IsMatch(rot.nome)) return Classe.Permitido;
        return Classe.Outro;
    }

    // malha visual do chão como colisores temporários (HideAndDontSave, camada livre, nunca salvos)
    static void CriaChaoVisual(Scene sc, string id)
    {
        TiraChaoVisual();
        var raiz = sc.GetRootGameObjects().FirstOrDefault(g => g.name == "Map_" + id);
        var chao = raiz ? raiz.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Chao") : null;
        if (!chao) return;
        camadaVis = Enumerable.Range(8, 24).Reverse().Where(i => string.IsNullOrEmpty(LayerMask.LayerToName(i))).DefaultIfEmpty(-1).First();
        if (camadaVis < 0) { L("sem camada livre para a malha visual do chão: rótulo fica pelo nome do colisor"); return; }
        foreach (var mr in chao.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) continue;
            var g = EditorUtility.CreateGameObjectWithHideFlags("PLANTAR_ChaoVis", HideFlags.HideAndDontSave);
            g.transform.SetPositionAndRotation(mr.transform.position, mr.transform.rotation);
            g.transform.localScale = mr.transform.lossyScale;
            g.layer = camadaVis;
            var mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            chaoVis.Add(g); visRend[mc] = mr;
        }
        Mascara = ~(1 << camadaVis);
        Physics.SyncTransforms();
        L($"malha visual do chão: {chaoVis.Count} peças de {NomeObj(chao)} na camada temporária {camadaVis}");
    }

    static void TiraChaoVisual()
    {
        foreach (var g in chaoVis) if (g) Object.DestroyImmediate(g);
        chaoVis.Clear(); visRend.Clear();
        camadaVis = -1; Mascara = ~0;
    }

    // primeira coisa que um raio de cima acerta
    static bool Topo(float x, float z, float yRef, out RaycastHit h) =>
        Physics.Raycast(new Vector3(x, yRef + 150f, z), Vector3.down, out h, 400f, Mascara, QueryTriggerInteraction.Ignore);

    // ------------------------------------------------------------------ levantamento
    class Lev
    {
        public int nx, nz; public Bounds area; public Classe[,] cl; public float[,] y;
        public Dictionary<string, (int n, Vector3 ex, Classe c)> rot = new Dictionary<string, (int, Vector3, Classe)>();
    }

    static Lev Levantamento(Bounds area, Regra r)
    {
        var lv = new Lev { area = area };
        lv.nx = Mathf.CeilToInt(area.size.x / Passo); lv.nz = Mathf.CeilToInt(area.size.z / Passo);
        lv.cl = new Classe[lv.nx, lv.nz]; lv.y = new float[lv.nx, lv.nz];
        int[] cont = new int[5];
        for (int i = 0; i < lv.nx; i++)
            for (int j = 0; j < lv.nz; j++)
            {
                float x = area.min.x + (i + 0.5f) * Passo, z = area.min.z + (j + 0.5f) * Passo;
                Classe c;
                if (!Topo(x, z, area.center.y, out var h)) { c = Classe.Nada; lv.y[i, j] = float.NaN; }
                else
                {
                    lv.y[i, j] = h.point.y;
                    var rot = Rotulo(h);
                    c = h.normal.y < 0.85f ? Classe.Inclinado : Classifica(rot, r);
                    var k = h.normal.y < 0.85f ? "(inclinado) " + rot.desc : rot.desc;
                    lv.rot[k] = lv.rot.TryGetValue(k, out var t) ? (t.n + 1, t.ex, t.c) : (1, h.point, c);
                }
                lv.cl[i, j] = c; cont[(int)c]++;
            }
        float m2 = Passo * Passo;
        L($"levantamento em grade de {Passo:0} m: {lv.nx * lv.nz} pontos · permitido {cont[0] * m2:0} m² · negado {cont[1] * m2:0} m² · outro {cont[2] * m2:0} m² · inclinado {cont[3] * m2:0} m² · nada {cont[4] * m2:0} m²");
        L("tipos de chão (área, classe, exemplo):");
        foreach (var kv in lv.rot.OrderByDescending(k => k.Value.n).Take(80))
            L($"  {kv.Value.n * m2,8:0} m²  {kv.Value.c,-9}  {kv.Key}  ex {V(kv.Value.ex)}");
        // mapa das classes (verde permitido, vermelho negado, cinza outro, marrom inclinado, preto nada)
        try
        {
            const int px = 3;
            var tex = new Texture2D(lv.nx * px, lv.nz * px, TextureFormat.RGB24, false);
            var cores = new[] { new Color(0.2f, 0.75f, 0.25f), new Color(0.85f, 0.2f, 0.2f), new Color(0.55f, 0.55f, 0.55f), new Color(0.45f, 0.3f, 0.15f), Color.black };
            for (int i = 0; i < lv.nx; i++)
                for (int j = 0; j < lv.nz; j++)
                    for (int a = 0; a < px; a++)
                        for (int b = 0; b < px; b++)
                            tex.SetPixel(i * px + a, j * px + b, cores[(int)lv.cl[i, j]]);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "classes.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            L($"classes.png: {px} px por ponto de {Passo:0} m, x para a direita, z para cima, canto de baixo à esquerda em ({F(area.min.x)}, {F(area.min.z)})");
        }
        catch (Exception e) { L("classes.png falhou: " + e.Message); }
        return lv;
    }

    // ------------------------------------------------------------------ plantar
    class Posta { public Vector3 pos; public string prefab; public float raio; public string chao; }

    static List<Posta> Plantar(Scene sc, Bounds area, Dictionary<string, object> lvl, Regra r, bool temNav)
    {
        var postas = new List<Posta>();
        var prefabs = Directory.Exists(PastaPrefab)
            ? Directory.GetFiles(PastaPrefab, "*.prefab").Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'))).Where(g => g).OrderBy(g => g.name).ToArray()
            : new GameObject[0];
        if (prefabs.Length == 0) { L("sem prefabs em " + PastaPrefab + ": rode BuritiroImportarRecursos.Importar antes"); return postas; }
        // tamanho de cada árvore (raio da copa e altura) pelo LODGroup do prefab
        var info = prefabs.Select(p =>
        {
            var rs = p.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(p.transform.position, Vector3.one * 6f);
            foreach (var x in rs) b.Encapsulate(x.bounds);
            float raio = 0.5f * Mathf.Max(b.size.x, b.size.z), alt = b.size.y;
            float peso = raio > 7f ? 0.2f : 0.4f;   // a copa muito larga (ipê) entra menos
            return (p, raio, alt, peso);
        }).ToArray();
        foreach (var i in info) L($"prefab {i.p.name}: copa {F(2 * i.raio)} m, altura {F(i.alt)} m" + (i.raio < 1f || i.alt < 1f ? " · medida inválida, fica de fora" : ""));
        info = info.Where(i => i.raio >= 1f && i.alt >= 1f).ToArray();
        if (info.Length == 0) { L("nenhum prefab com medida válida"); return postas; }

        // pontos protegidos
        var protegidos = new List<Vector3>();
        foreach (var k in new[] { "spawnA", "spawnB", "points", "spawnPolice", "basePairs", "parked", "caveirao", "bots" })
            protegidos.AddRange(Pontos(Json.Get(lvl, k)));
        var rotas = Linhas(Json.Get(lvl, "lanes"));
        var way = Pontos(Json.Get(lvl, "waypoints"));
        // água (polígonos [[x,z]..., [y,0]], x invertido)
        var aguas = new List<List<Vector2>>();
        if (Json.L(lvl, "water") is List<object> wl)
            foreach (var w in wl)
                if (w is List<object> poly && poly.Count >= 4)
                    aguas.Add(poly.Take(poly.Count - 1).OfType<List<object>>().Where(q => q.Count >= 2).Select(q => new Vector2(-Json.Fv(q[0]), Json.Fv(q[1]))).ToList());
        L($"protegidos {protegidos.Count} pontos · rotas {rotas.Count} · waypoints {way.Count} · água {aguas.Count} polígonos");
        // árvores que o mapa já tem (pelo nome do objeto)
        var existentes = new List<Vector2>();
        int nRendArv = 0, nJuntadas = 0;
        foreach (var root in sc.GetRootGameObjects())
            foreach (var rr in root.GetComponentsInChildren<Renderer>(true))
            {
                if (rr.transform.IsChildOf(root.transform) && rr.GetComponentsInParent<Transform>(true).Any(t => t.name == NomeGrupo)) continue;
                if (!(ArvoreMapa.IsMatch(rr.gameObject.name) || (rr.transform.parent && ArvoreMapa.IsMatch(rr.transform.parent.name)))) continue;
                nRendArv++;
                var bb = rr.bounds;
                if (Mathf.Max(bb.size.x, bb.size.z) <= 8f) { existentes.Add(new Vector2(bb.center.x, bb.center.z)); continue; }
                // árvores juntadas por célula: troncos = vértices até 1,2 m acima da base, agrupados numa grade de 2 m
                nJuntadas++;
                var mf = rr.GetComponent<MeshFilter>();
                var malha = mf ? mf.sharedMesh : null;
                bool ok = false;
                if (malha)
                    try
                    {
                        var vs = malha.vertices; var mat4 = rr.transform.localToWorldMatrix;
                        var celulas = new HashSet<(int, int)>();
                        foreach (var v0 in vs)
                        {
                            var w = mat4.MultiplyPoint3x4(v0);
                            if (w.y > bb.min.y + 1.2f) continue;
                            celulas.Add((Mathf.FloorToInt(w.x / 2f), Mathf.FloorToInt(w.z / 2f)));
                        }
                        foreach (var c in celulas) existentes.Add(new Vector2(c.Item1 * 2f + 1f, c.Item2 * 2f + 1f));
                        ok = celulas.Count > 0;
                    }
                    catch (Exception) { }
                if (!ok) existentes.Add(new Vector2(bb.center.x, bb.center.z));
            }
        L($"árvores que o mapa já tem (pelo nome): {nRendArv} renderers, {nJuntadas} juntados por célula · {existentes.Count} pontos de tronco");

        // grupo
        var mapaRaiz = sc.GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Map_"));
        var grupo = new GameObject(NomeGrupo);
        SceneManager.MoveGameObjectToScene(grupo, sc);
        if (mapaRaiz) grupo.transform.SetParent(mapaRaiz.transform, false);

        var rnd = new System.Random(31);
        var rej = new Dictionary<string, int>();
        void R(string k) => rej[k] = rej.TryGetValue(k, out int n) ? n + 1 : 1;
        var csv = new StringBuilder("n;prefab;x;y;z;escala;yaw;chao\n");
        float pesoTotal = info.Sum(i => i.peso);
        int tentativas = 0;
        for (; tentativas < 30000 && postas.Count < r.max; tentativas++)
        {
            float x = area.min.x + (float)rnd.NextDouble() * area.size.x, z = area.min.z + (float)rnd.NextDouble() * area.size.z;
            if (!Topo(x, z, area.center.y, out var h)) { R("sem chão"); continue; }
            if (h.normal.y < 0.92f) { R("chão inclinado, parede ou telhado"); continue; }
            var rot = Rotulo(h);
            var cls = Classifica(rot, r);
            if (cls != Classe.Permitido) { R(cls == Classe.Negado ? "chão negado (rua, calçada...)" : "chão fora da lista"); continue; }
            var p = h.point;
            if (temNav && (!NavMesh.SamplePosition(p, out var nh, 1.0f, NavMesh.AllAreas) || Mathf.Abs(nh.position.y - p.y) > 0.6f)) { R("fora do chão andável (telhado?)"); continue; }
            var p2 = new Vector2(p.x, p.z);
            if (aguas.Any(a => Dentro(a, p2))) { R("na água"); continue; }
            if (protegidos.Any(q => new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude < DistPontos * DistPontos)) { R("perto de spawn/objetivo"); continue; }
            if (way.Any(q => new Vector2(q.x - p.x, q.z - p.z).sqrMagnitude < DistRota * DistRota)) { R("perto de waypoint"); continue; }
            if (rotas.Any(l => DistLinha(l, p2) < DistRota)) { R("perto de rota do 10x10"); continue; }
            // borda: anel de 1,5 m e de 3 m com o mesmo tipo de chão permitido, sem degrau nem rua
            bool borda = false; string motivo = null;
            for (int k = 0; k < 16 && !borda; k++)
            {
                float a = k * Mathf.PI / 8f;
                foreach (float d in new[] { 1.5f, DistRua, DistRua + 0.3f })
                {
                    float qx = p.x + Mathf.Cos(a) * d, qz = p.z + Mathf.Sin(a) * d;
                    if (!Topo(qx, qz, p.y, out var hq)) { borda = true; motivo = "borda sem chão"; break; }
                    if (Mathf.Abs(hq.point.y - p.y) > 0.6f || hq.normal.y < 0.85f) { borda = true; motivo = "degrau, muro ou parede a menos de 3 m"; break; }
                    if (Classifica(Rotulo(hq), r) == Classe.Negado) { borda = true; motivo = "rua ou calçada a menos de 3 m"; break; }
                }
            }
            if (borda) { R(motivo); continue; }
            // escolhe a árvore (sorteio com peso); se a copa não cabe, tenta as menores
            double s = rnd.NextDouble() * pesoTotal; int esc = 0;
            for (int i = 0; i < info.Length; i++) { s -= info[i].peso; if (s <= 0) { esc = i; break; } }
            var ordem = new[] { esc }.Concat(Enumerable.Range(0, info.Length).Where(i => i != esc).OrderBy(i => info[i].raio)).ToArray();
            float escala = 0.85f + (float)rnd.NextDouble() * 0.25f;
            int pega = -1; string porque = null;
            foreach (int i in ordem)
            {
                float raio = info[i].raio * escala, alt = info[i].alt * escala;
                if (postas.Any(o => new Vector2(o.pos.x - p.x, o.pos.z - p.z).magnitude < Mathf.Max(DistArvores, 0.5f * (o.raio + raio)))) { porque = "perto de outra árvore nova"; continue; }
                if (existentes.Any(e => (e - p2).magnitude < Mathf.Max(DistArvores, raio * 0.7f))) { porque = "perto de árvore do mapa"; continue; }
                // tronco: nada de 0,5 m a 3 m de altura num raio de 0,8 m (banco, poste, muro, lixeira). O colisor do chão
                // não é ignorado de propósito: no mapa do montador o chão e as paredes podem estar na mesma malha.
                if (Physics.CheckCapsule(p + Vector3.up * 1.3f, p + Vector3.up * 3.0f, 0.8f, Mascara, QueryTriggerInteraction.Ignore)) { porque = "objeto no lugar do tronco"; continue; }
                // copa: nada (prédio, poste, fio, toldo) dentro de 75% do raio, na altura da copa (a esfera fica pelo menos
                // 2,5 m acima do chão)
                float cy = Mathf.Max(0.75f * raio + 2.5f, 0.62f * alt);
                if (Physics.CheckSphere(p + Vector3.up * cy, 0.75f * raio, Mascara, QueryTriggerInteraction.Ignore)) { porque = "copa bate em prédio ou poste"; continue; }
                pega = i; break;
            }
            if (pega < 0) { R(porque ?? "sem árvore que caiba"); continue; }

            var pf = info[pega].p;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, grupo.transform);
            float yaw = (float)rnd.NextDouble() * 360f;
            go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * escala;
            int camada = h.collider.gameObject.layer;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camada;
            // o NavMesh foi assado sem a árvore: o tronco abre um buraco nele, para os bots desviarem
            var cap = go.GetComponent<CapsuleCollider>();
            var obs = go.AddComponent<NavMeshObstacle>();
            obs.shape = NavMeshObstacleShape.Capsule;
            obs.center = cap ? cap.center : new Vector3(0f, 1.5f, 0f);
            obs.radius = (cap ? cap.radius : 0.35f) + 0.15f;
            obs.height = cap ? cap.height : 3f;
            obs.carving = true;
            obs.carveOnlyStationary = true;
            Physics.SyncTransforms();
            postas.Add(new Posta { pos = p, prefab = pf.name, raio = info[pega].raio * escala, chao = rot.desc });
            csv.AppendLine($"{postas.Count};{pf.name};{F(p.x)};{F(p.y)};{F(p.z)};{escala.ToString("0.00", CI)};{yaw:0};{rot.desc.Replace(';', ',')}");
        }
        if (postas.Count > 0) File.WriteAllText(Path.Combine(dir, "arvores.csv"), csv.ToString(), new UTF8Encoding(false));
        L($"plantadas {postas.Count} de no máximo {r.max} em {tentativas} tentativas");
        foreach (var g in postas.GroupBy(o => o.prefab)) L($"  {g.Key}: {g.Count()}");
        foreach (var g in postas.GroupBy(o => o.chao).OrderByDescending(g => g.Count())) L($"  chão {g.Key}: {g.Count()}");
        L("recusas:");
        foreach (var kv in rej.OrderByDescending(k => k.Value)) L($"  {kv.Value,6}  {kv.Key}");
        if (postas.Count == 0) Object.DestroyImmediate(grupo);
        return postas;
    }

    static bool Dentro(List<Vector2> poly, Vector2 p)
    {
        bool d = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) d = !d;
        return d;
    }

    static float DistLinha(List<Vector3> l, Vector2 p)
    {
        float m = float.MaxValue;
        for (int i = 0; i + 1 < l.Count; i++)
        {
            Vector2 a = new Vector2(l[i].x, l[i].z), b = new Vector2(l[i + 1].x, l[i + 1].z), ab = b - a;
            float t = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            m = Mathf.Min(m, (a + t * ab - p).magnitude);
        }
        return m;
    }

    static int TiraGrupo(Scene sc)
    {
        int n = 0;
        foreach (var root in sc.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t && t.name == NomeGrupo).ToList())
                if (t) { Object.DestroyImmediate(t.gameObject); n++; }
        return n;
    }

    static void Backup(string cena)
    {
        string bk = Path.Combine(Base, "_backup", "plantar_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(bk);
        string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", cena));
        File.Copy(full, Path.Combine(bk, Path.GetFileName(cena)), true);
        L("cópia da cena em " + bk);
    }

    // ------------------------------------------------------------------ fotos
    static void Fotos(Bounds area, List<Vector3> postas, Lev lev, bool comArvores)
    {
        var go = EditorUtility.CreateGameObjectWithHideFlags("PLANTAR_FotoCam", HideFlags.HideAndDontSave, typeof(Camera), typeof(UniversalAdditionalCameraData));
        var cam = go.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 2000f;
        var ucd = go.GetComponent<UniversalAdditionalCameraData>();
        ucd.renderPostProcessing = true; ucd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        float lodAntes = QualitySettings.lodBias;
        int maxLodAntes = QualitySettings.maximumLODLevel;
        try
        {
            QualitySettings.lodBias = Mathf.Max(lodAntes, 1f);
            // de cima, ortográfica, do tamanho da área (para comparar com classes.png). Árvores novas no LOD0 só nesta foto
            // (na ortográfica elas cairiam no impostor de cartões em pé e quase não apareceriam)
            var lgs = Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude)
                .Where(g => g.GetComponentsInParent<Transform>(true).Any(t => t.name == NomeGrupo)).ToArray();
            float topo = area.center.y + 250f;
            cam.orthographic = true;
            cam.orthographicSize = 0.5f * area.size.z;
            try
            {
                QualitySettings.maximumLODLevel = 0;
                foreach (var g in lgs) g.ForceLOD(0);
                Foto(cam, "de_cima", new Vector3(area.center.x, topo, area.center.z), Quaternion.Euler(90f, 0f, 0f), 1600, Mathf.RoundToInt(1600f * area.size.z / area.size.x));
            }
            finally
            {
                foreach (var g in lgs) if (g) g.ForceLOD(-1);
                QualitySettings.maximumLODLevel = maxLodAntes;
            }
            L($"foto de_cima: ortográfica, {F(area.size.x)} m de largura, mesmo enquadramento do classes.png");
            if (comArvores && postas.Count > 0) MarcaArvores(lev, postas);
            cam.orthographic = false; cam.fieldOfView = 60f;
            if (!comArvores || postas.Count == 0) return;
            // três vistas na altura do jogador, cada uma olhando para uma árvore nova com visão livre
            var escolhidas = postas.OrderBy(p => (p - area.center).sqrMagnitude).Take(1)
                .Concat(postas.OrderByDescending(p => (p - area.center).sqrMagnitude).Take(1))
                .Concat(postas.Skip(postas.Count / 2).Take(1)).Distinct().ToList();
            int n = 0;
            foreach (var alvo in escolhidas)
            {
                n++;
                var foco = alvo + Vector3.up * 3.5f;
                Vector3 melhor = foco + new Vector3(0f, -1.8f, -16f); float melhorLivre = -1f;
                for (int k = 0; k < 12; k++)
                {
                    float a = k * 30f * Mathf.Deg2Rad;
                    var q = new Vector3(alvo.x + Mathf.Sin(a) * 16f, 0f, alvo.z + Mathf.Cos(a) * 16f);
                    if (!Topo(q.x, q.z, alvo.y, out var hq) || hq.normal.y < 0.85f) continue;
                    q.y = hq.point.y + 1.7f;
                    if (Physics.CheckSphere(q, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var d = foco - q;
                    float livre = Physics.Raycast(q, d.normalized, out var hh, d.magnitude, ~0, QueryTriggerInteraction.Ignore) ? hh.distance : d.magnitude;
                    // a própria árvore pode tampar o foco: conta como livre se o acerto está perto dela
                    if (livre < d.magnitude && new Vector2(hh.point.x - alvo.x, hh.point.z - alvo.z).magnitude < 3f) livre = d.magnitude;
                    if (livre > melhorLivre) { melhorLivre = livre; melhor = q; }
                    if (livre >= d.magnitude - 0.5f) break;
                }
                Foto(cam, "vista_" + n, melhor, Quaternion.LookRotation(foco - melhor, Vector3.up), 1920, 1080);
                L($"foto vista_{n}: de {V(melhor)} para a árvore em {V(alvo)} · visão livre {F(melhorLivre)} de 16 m");
            }
        }
        catch (Exception e) { L("fotos falharam: " + e.Message); }
        finally
        {
            QualitySettings.lodBias = lodAntes;
            Object.DestroyImmediate(go);
        }
    }

    // cópia do classes.png com um ponto branco (borda preta) em cada árvore nova
    static void MarcaArvores(Lev lev, List<Vector3> postas)
    {
        try
        {
            const int px = 3;
            var cores = new[] { new Color(0.2f, 0.75f, 0.25f), new Color(0.85f, 0.2f, 0.2f), new Color(0.55f, 0.55f, 0.55f), new Color(0.45f, 0.3f, 0.15f), Color.black };
            int w = lev.nx * px, hgt = lev.nz * px;
            var tex = new Texture2D(w, hgt, TextureFormat.RGB24, false);
            for (int i = 0; i < lev.nx; i++)
                for (int j = 0; j < lev.nz; j++)
                    for (int a = 0; a < px; a++)
                        for (int b = 0; b < px; b++)
                            tex.SetPixel(i * px + a, j * px + b, cores[(int)lev.cl[i, j]]);
            foreach (var p in postas)
            {
                int cx = Mathf.RoundToInt((p.x - lev.area.min.x) / Passo * px), cz = Mathf.RoundToInt((p.z - lev.area.min.z) / Passo * px);
                for (int a = -5; a <= 5; a++)
                    for (int b = -5; b <= 5; b++)
                    {
                        int d2 = a * a + b * b;
                        if (d2 > 25) continue;
                        int x = cx + a, y = cz + b;
                        if (x < 0 || y < 0 || x >= w || y >= hgt) continue;
                        tex.SetPixel(x, y, d2 > 12 ? Color.black : Color.white);
                    }
            }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "classes_arvores.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            L("classes_arvores.png: o mesmo mapa das classes com um ponto branco em cada árvore nova");
        }
        catch (Exception e) { L("classes_arvores.png falhou: " + e.Message); }
    }

    static void Foto(Camera cam, string nome, Vector3 pos, Quaternion rot, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        try
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.aspect = (float)w / h;
            cam.targetTexture = rt;
            // a 1ª imagem logo depois de abrir a cena no editor sai sem parte do mapa (chão e paredes): desenha duas vezes
            cam.Render();
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, nome + ".jpg"), tex.EncodeToJPG(90));
        }
        finally
        {
            cam.targetTexture = null;
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
    }
}
