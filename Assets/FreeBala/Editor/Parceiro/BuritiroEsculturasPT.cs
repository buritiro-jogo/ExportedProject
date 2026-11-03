// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/BuritiroEsculturasPT.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// BURITIRO · Praça do PT: troca as esculturas feitas à mão do mapa v4 (onça na rocha, 2 filhotes, jacaré, papagaio e lagarto)
// pelas 6 do Tripo 3D (thread do Instagram, 31/10).
//
// Lab:
//   metodo BuritiroEsculturasPT.Trocar        tira as antigas e põe as novas; salva a cena; relatório e fotos antes/depois
//   metodo BuritiroEsculturasPT.Reposicionar  só refaz as novas, lendo _diag\esculturas_pt_ajuste.txt (giro, escala, deslocamento)
//   metodo BuritiroEsculturasPT.Desfazer      volta a cena do último backup (_backup\esculturas_pt_*)
// Saída: _diag\esculturas_pt\relatorio.txt e fotos antes_<id>.jpg / depois_<id>.jpg
//
// As antigas estão fundidas nas malhas das células (C_x_y_LOD0/1/2) e na colisão (COL_x_y_*). Elas saem assim:
//  - malha visual: tira os triângulos cujo centro cai numa caixa de escultura E cujo material é de escultura
//    (e, só na rocha da onça, também os arbustos que estavam em cima dela);
//    (o triângulo só sai se os 3 vértices também estão perto da mesma caixa);
//  - colisão: tira os triângulos com os 3 vértices dentro de uma caixa de escultura (o chão, COL_CH_*, não é mexido;
//    pelo centro sairiam 2 triângulos de 77 m² do cascalho em volta da rocha, PT_Onca_Cascalho, que fica);
//  - instâncias inteiras do LOD0 (I_*) em cima da rocha da onça (os 3 arbustos): desligadas. Os limites delas viram
//    caixas extras (folhas do arbusto que saem até 0,3 m da caixa da rocha ficavam no LOD1/LOD2 e na colisão).
// Depois: troca os pisos emborrachados por PBR (BuritiroImportarRecursos.AplicarPisos, se existir), salva a cena e
// refaz o NavMesh no mesmo arquivo (NavMesh_praca_pt.asset, com cópia em _backup), para os bots contornarem as novas.
// No mapa v5 (sem as esculturas antigas) a parte de tirar não acha nada e só põe as novas.
// As malhas novas são cópias gravadas em Assets/BURITIRO_Mapas/praca_pt/derivados_esculturas (o GLB não muda).
// Remontar o mapa com o BuritiroMontaMapa desfaz tudo isto: rode Trocar de novo depois.
//
// As caixas são as das esculturas antigas no praca_do_pt_v4.glb, convertidas para o Unity (x do glTF negado),
// conferidas contra a foto vista_1 do plantio (o papagaio e o lagarto caem onde aparecem).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class BuritiroEsculturasPT
{
    const string Cena = "Assets/FreeBala/Scenes/Map_praca_pt.unity";
    const string PastaEsc = "Assets/BURITIRO_Mapas/praca_pt/esculturas";
    const string PastaMalhas = "Assets/BURITIRO_Mapas/praca_pt/derivados_esculturas";
    const string Dados = "Assets/BURITIRO_Mapas/praca_pt/praca_pt_dados.json";
    const string NomeGrupo = "BURITIRO_Esculturas";
    const int LAYER_MAPA = 8;
    const float FOV = 60f;
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;
    static string Base => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
    static string Dir => Path.Combine(Base, "_diag", "esculturas_pt");
    static string ArqAjuste => Path.Combine(Base, "_diag", "esculturas_pt_ajuste.txt");
    static readonly StringBuilder sb = new StringBuilder();
    static void L(string s) { sb.AppendLine(s); }
    static string V(Vector3 v) => string.Format(IC, "({0:0.00}, {1:0.00}, {2:0.00})", v.x, v.y, v.z);

    class Caixa
    {
        public string grupo, nome; public Vector3 c, ax, az, h;
        public Caixa(string g, string n, Vector3 c, Vector3 ax, Vector3 az, Vector3 h) { grupo = g; nome = n; this.c = c; this.ax = ax; this.az = az; this.h = h; }
        // margem de 20 cm para os lados e para cima; para baixo vai até o chão (y 0), para pegar laterais de base
        public bool Dentro(Vector3 p)
        {
            var d = p - c;
            if (Mathf.Abs(Vector3.Dot(d, ax)) > h.x + 0.2f) return false;
            if (Mathf.Abs(Vector3.Dot(d, az)) > h.z + 0.2f) return false;
            return p.y <= c.y + h.y + 0.2f && p.y >= Mathf.Min(c.y - h.y - 0.2f, 0f);
        }
        // vértice até 50 cm além da margem: um triângulo grande do piso com o centro na caixa não sai inteiro
        // (no COL_3_0_concreto, 2 triângulos de 77 m² do piso em y 0,2 têm o centro na caixa da rocha)
        public bool Perto(Vector3 p)
        {
            var d = p - c;
            if (Mathf.Abs(Vector3.Dot(d, ax)) > h.x + 0.7f) return false;
            if (Mathf.Abs(Vector3.Dot(d, az)) > h.z + 0.7f) return false;
            return p.y <= c.y + h.y + 0.7f && p.y >= Mathf.Min(c.y - h.y - 0.7f, 0f) - 0.5f;
        }
    }

    static readonly Caixa[] Caixas =
    {
        new Caixa("jacare", "PT_Escultura_Jacare", new Vector3(-39.278f, 1.095f, 12.747f), new Vector3(-0.9994f, 0f, 0.0344f), new Vector3(0.0344f, 0f, 0.9994f), new Vector3(2.733f, 0.885f, 0.99f)),
        new Caixa("jacare", "PT_Escultura_Jacare_Base", new Vector3(-39.332f, 0.26f, 12.761f), new Vector3(-0.9994f, 0f, 0.0344f), new Vector3(0.0344f, 0f, 0.9994f), new Vector3(1.382f, 0.01f, 1.304f)),
        new Caixa("lagarto", "PT_Escultura_Lagarto", new Vector3(-28.101f, 1.211f, -40.08f), new Vector3(0.216f, 0f, 0.9764f), new Vector3(0.9764f, 0f, -0.216f), new Vector3(1.158f, 1.039f, 0.645f)),
        new Caixa("lagarto", "PT_Escultura_Lagarto_Base", new Vector3(-27.855f, 0.26f, -39.682f), new Vector3(0.216f, 0f, 0.9764f), new Vector3(0.9764f, 0f, -0.216f), new Vector3(1.261f, 0.01f, 1.285f)),
        new Caixa("filhote0", "PT_Escultura_Onca_Filhote_0", new Vector3(-137.989f, 1.469f, -5.73f), new Vector3(-0.7229f, 0f, 0.691f), new Vector3(0.691f, 0f, 0.7229f), new Vector3(1.603f, 1.013f, 0.493f)),
        new Caixa("filhote1", "PT_Escultura_Onca_Filhote_1", new Vector3(-139.119f, 1.469f, 2.833f), new Vector3(-0.008f, 0f, 1.0f), new Vector3(1.0f, 0f, 0.008f), new Vector3(1.604f, 1.013f, 0.491f)),
        new Caixa("onca", "PT_Escultura_Onca_Pintada", new Vector3(-144.424f, 3.066f, -1.958f), new Vector3(-0.3031f, 0f, 0.953f), new Vector3(0.953f, 0f, 0.3031f), new Vector3(3.771f, 1.875f, 1.123f)),
        new Caixa("papagaio", "PT_Escultura_Papagaio", new Vector3(-10.864f, 1.195f, -23.821f), new Vector3(0.8515f, 0f, -0.5243f), new Vector3(-0.5243f, 0f, -0.8515f), new Vector3(0.881f, 0.986f, 0.511f)),
        new Caixa("papagaio", "PT_Escultura_Papagaio_Base", new Vector3(-10.776f, 0.26f, -24.002f), new Vector3(0.8515f, 0f, -0.5243f), new Vector3(-0.5243f, 0f, -0.8515f), new Vector3(1.209f, 0.01f, 1.317f)),
        new Caixa("filhote0", "PT_Onca_Base_Filhote_0", new Vector3(-137.869f, 0.49f, -5.721f), new Vector3(-0.7229f, 0f, 0.691f), new Vector3(0.691f, 0f, 0.7229f), new Vector3(1.788f, 0.01f, 1.676f)),
        new Caixa("filhote1", "PT_Onca_Base_Filhote_1", new Vector3(-139.264f, 0.49f, 2.764f), new Vector3(-0.008f, 0f, 1.0f), new Vector3(1.0f, 0f, 0.008f), new Vector3(1.733f, 0.01f, 1.628f)),
        new Caixa("onca", "PT_Onca_Rocha", new Vector3(-144.292f, 1.121f, -1.95f), new Vector3(-0.3031f, 0f, 0.953f), new Vector3(0.953f, 0f, 0.3031f), new Vector3(4.186f, 1.464f, 2.734f)),
    };

    // materiais das esculturas antigas (corpo, olhos, dentes, crista, bases e rocha)
    static readonly string[] MatsEsc =
    {
        "MT_onca_pelagem", "MT_rocha_escultura", "MT_jacare_escultura", "MT_fibra_verde_papagaio", "MT_fibra_verde_agua",
        "MT_base_verde_escultura", "MT_fibra_preta", "MT_fibra_branca", "MT_fibra_vermelha", "MT_fibra_azul_claro", "MT_fibra_amarela",
    };
    // só dentro da rocha da onça: os 3 arbustos que estavam plantados em cima dela (juntados no LOD1/LOD2 da célula)
    static readonly string[] MatsSoRocha = { "A4_casca_arvore", "A4_folhas_folhas_arbusto" };

    class Esc
    {
        public string id, arquivo; public Vector3 pos; public float yaw, dist;
        public Esc(string id, string arq, float x, float z, float yaw, float dist) { this.id = id; arquivo = arq; pos = new Vector3(x, 0f, z); this.yaw = yaw; this.dist = dist; }
    }
    // yaw = para onde a cabeça da escultura antiga apontava, supondo que o modelo novo olha para +Z (corrigir no arquivo de ajuste)
    static readonly Esc[] Escs =
    {
        new Esc("onca", "onca_adulta", -144.29f, -1.95f, -17.7f, 12f),
        new Esc("filhote0", "filhote_em_pe", -137.97f, -5.74f, -46.3f, 6f),
        new Esc("filhote1", "filhote_deitado", -139.12f, 2.81f, -0.5f, 6f),
        new Esc("jacare", "jacare", -39.32f, 12.84f, -88.1f, 7f),
        new Esc("papagaio", "papagaio", -10.72f, -23.91f, 121.6f, 6f),
        new Esc("lagarto", "lagarto", -27.83f, -39.67f, 12.5f, 6f),
    };

    // ------------------------------------------------------------------ entradas do lab
    public static void Trocar() => Rodar(true);
    public static void Reposicionar() => Rodar(false);
    // v3b: para mapa remontado que já vem sem as esculturas antigas (arenas v6): põe as novas sem TiraAntigas.
    // O TiraAntigas cortaria a colisão do cascalho dentro das caixas antigas (na v6 não há mais rocha nem esculturas ali).
    public static void PorSemTirar() => Rodar(false, true);

    public static void Desfazer()
    {
        sb.Clear(); Directory.CreateDirectory(Dir);
        L("desfazer " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        try
        {
            var pasta = Directory.Exists(Path.Combine(Base, "_backup"))
                ? Directory.GetDirectories(Path.Combine(Base, "_backup"), "esculturas_pt_*").OrderBy(d => d).LastOrDefault() : null;
            var arq = pasta != null ? Path.Combine(pasta, "Map_praca_pt.unity") : null;
            if (arq == null || !File.Exists(arq)) { L("ERRO: não achei backup em _backup\\esculturas_pt_*"); return; }
            var destino = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Cena));
            if (CenaSujaAberta()) return;
            // só desfaz se a cena atual tem as novas: depois de remontar o mapa o backup é de uma montagem velha
            if (!File.Exists(destino) || !File.ReadAllText(destino).Contains(NomeGrupo))
            { L("ERRO: " + Cena + " não tem " + NomeGrupo + " (Trocar não está aplicado nesta cena): nada a desfazer, backup não usado"); return; }
            // aberta em qualquer modo (não só a ativa): copiar por cima de uma cena carregada abre o diálogo de "mudou fora"
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(Cena).isLoaded)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            File.Copy(arq, destino, true);
            AssetDatabase.ImportAsset(Cena, ImportAssetOptions.ForceUpdate);
            L("cena voltou de " + arq + " (as malhas em " + PastaMalhas + " ficam no projeto, sem uso)");
        }
        catch (Exception e) { L("ERRO " + e); }
        finally { Grava(); }
    }

    // ------------------------------------------------------------------
    static void Rodar(bool tirarAntigas, bool semGrupoOk = false)
    {
        sb.Clear(); Directory.CreateDirectory(Dir);
        string antes = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        L((tirarAntigas ? "trocar" : semGrupoOk ? "pôr sem tirar as antigas (mapa remontado)" : "reposicionar") + " esculturas da Praça do PT · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        bool salvou = false, abriu = false;
        try
        {
            if (!File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", Cena)))) { L("ERRO: não existe " + Cena); return; }
            if (CenaSujaAberta()) return;
            // os GLBs novos antes de mexer na cena: sem eles, não tira nada
            var modelos = new Dictionary<string, GameObject[]>();
            foreach (var e in Escs)
            {
                var m = CarregaModelo(e.arquivo);
                if (m == null) { L($"ERRO: não achei {PastaEsc}/{e.arquivo}_LODs.glb nem {e.arquivo}_LOD0/1/2.glb (importados pelo glTFast)"); return; }
                modelos[e.id] = m;
            }
            var cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single); abriu = true;
            var raiz = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Map_praca_pt");
            if (!raiz) { L("ERRO: a cena não tem a raiz Map_praca_pt"); return; }
            Physics.SyncTransforms();
            var aj = LeAjustes();

            var grupoVelho = raiz.transform.Find(NomeGrupo);
            // Reposicionar sem Trocar (ou depois de remontar o mapa) poria as novas em cima das antigas, sem backup
            if (!tirarAntigas && !grupoVelho && !semGrupoOk) { L("ERRO: a cena não tem " + NomeGrupo + " (Trocar não rodou nesta cena ou o mapa foi remontado): rode Trocar"); return; }

            if (tirarAntigas || semGrupoOk)
            {
                Fotos("antes");
                // backup do arquivo da cena antes de qualquer mudança; se Trocar já rodou nesta cena, o backup de antes da
                // troca continua sendo o último (senão o Desfazer voltaria para a cena já trocada)
                if (grupoVelho) L("a cena já tem " + NomeGrupo + " (Trocar já rodou): sem backup novo, o último continua sendo o de antes da troca");
                else
                {
                    var pastaBk = Path.Combine(Base, "_backup", "esculturas_pt_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    Directory.CreateDirectory(pastaBk);
                    File.Copy(Path.GetFullPath(Path.Combine(Application.dataPath, "..", Cena)), Path.Combine(pastaBk, "Map_praca_pt.unity"), true);
                    L("backup da cena em " + pastaBk);
                }
            }

            // o grupo velho sai antes do corte das malhas, para as esculturas novas nunca entrarem nele
            if (grupoVelho) { Object.DestroyImmediate(grupoVelho.gameObject); L("tirei o grupo " + NomeGrupo + " de antes"); }
            if (tirarAntigas && !TiraAntigas(raiz)) { L("nada foi salvo"); return; }
            Physics.SyncTransforms();
            var grupo = new GameObject(NomeGrupo); grupo.transform.SetParent(raiz.transform, false); grupo.layer = LAYER_MAPA;
            float corte = LeCorte();
            int postas = 0;
            foreach (var e in Escs) if (PoeNova(e, modelos[e.id], grupo.transform, aj, corte)) postas++;
            L($"esculturas novas postas: {postas} de {Escs.Length}");
            if (postas == 0) { L("nada foi salvo"); return; }

            AplicaPisos(raiz);
            EditorSceneManager.MarkSceneDirty(cena);
            salvou = EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            L("cena salva: " + salvou);
            Physics.SyncTransforms();
            if (salvou) RefazNavMesh(raiz);
            Fotos("depois");
        }
        catch (Exception ex) { L("ERRO " + ex); }
        finally
        {
            // sem salvar, a cena em memória pode estar pela metade (malhas trocadas, grupo apagado). Se ela ficaria aberta
            // (era a cena de antes, ou não havia cena salva), volta a do disco: o lab recusa 'metodo' com cena suja e um Ctrl+S gravaria a metade.
            if (abriu && !salvou && (string.IsNullOrEmpty(antes) || antes == Cena))
            {
                try { EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single); L("cena reaberta do disco (nada pela metade ficou aberto)"); }
                catch (Exception e2) { L("ERRO ao reabrir a cena do disco: " + e2.Message); }
            }
            Grava();
            // só se a cena do mapa foi aberta aqui: num retorno antes do OpenScene, reabrir 'antes' recarregaria a cena atual do disco
            try { if (abriu && !string.IsNullOrEmpty(antes) && antes != Cena) EditorSceneManager.OpenScene(antes, OpenSceneMode.Single); } catch { }
        }
    }

    // pisos emborrachados PBR do pacote de recursos gráficos 2 (BuritiroImportarRecursos.AplicarPisos), se ele estiver no projeto
    static void AplicaPisos(GameObject raiz)
    {
        var tipo = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuritiroImportarRecursos")).FirstOrDefault(t => t != null);
        var met = tipo?.GetMethod("AplicarPisos", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(GameObject) }, null);
        if (met == null) { L("AVISO: BuritiroImportarRecursos.AplicarPisos não existe no projeto: pisos emborrachados não trocados"); return; }
        try { L("pisos emborrachados PBR: " + met.Invoke(null, new object[] { raiz }) + " submalhas trocadas (0 = já eram PBR ou faltam as texturas; veja o Console)"); }
        catch (Exception e) { L("AVISO: AplicarPisos falhou: " + (e.InnerException ?? e).Message); }
    }

    // refaz o NavMesh no mesmo asset (mesmo GUID: o FBNavData da cena continua apontando para ele), com a mesma área da
    // montagem (sourceBounds) e as mesmas regras do BuritiroMontaMapa: colisores físicos do layer do mapa.
    // Assim os bots contornam as esculturas novas e não batem nas antigas que saíram. Cópia do arquivo velho em _backup.
    static void RefazNavMesh(GameObject raiz)
    {
        try
        {
            NavMeshData data = null;
            var go = raiz.scene.GetRootGameObjects().FirstOrDefault(g => g.name == "NavMesh");
            if (go)
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (!comp) continue;
                    var prop = new SerializedObject(comp).FindProperty("data");
                    if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue is NavMeshData nd) { data = nd; break; }
                }
            if (!data) { L("AVISO: a cena não tem o objeto NavMesh com FBNavData.data: NavMesh não refeito"); return; }
            var path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) { L("AVISO: o NavMesh da cena não é um arquivo do projeto: não refeito"); return; }
            var b = data.sourceBounds;
            if (b.size.x < 10f || b.size.z < 10f) { L("AVISO: área do NavMesh velho desconhecida (" + V(b.size) + "): não refeito; use BURITIRO/Mapas/Refazer NavMesh"); return; }
            var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            var bk = Path.Combine(Base, "_backup", "esculturas_pt_navmesh_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(bk);
            File.Copy(full, Path.Combine(bk, Path.GetFileName(path)), true);
            long antesB = new FileInfo(full).Length;
            var fontes = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(b, 1 << LAYER_MAPA, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fontes);
            var t0 = DateTime.Now;
            if (!NavMeshBuilder.UpdateNavMeshData(data, NavMesh.GetSettingsByID(0), fontes, b))
            { L("AVISO: UpdateNavMeshData falhou; o NavMesh velho continua (cópia em " + bk + ")"); return; }
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            long depoisB = new FileInfo(full).Length;
            L(string.Format(IC, "NavMesh refeito em {0}: {1} colisores, área {2:0}x{3:0} m, {4:0} s, arquivo {5} -> {6} bytes (cópia do velho em {7})",
                path, fontes.Count, b.size.x, b.size.z, (DateTime.Now - t0).TotalSeconds, antesB, depoisB, bk));
        }
        catch (Exception e) { L("AVISO: refazer o NavMesh falhou: " + e.Message); }
    }

    // o OpenScene(Single) e o NewScene descartam mudanças não salvas sem perguntar
    static bool CenaSujaAberta()
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.isDirty)
            {
                L("ERRO: a cena " + (string.IsNullOrEmpty(s.path) ? s.name : s.path) + " tem mudanças não salvas; salve ou descarte antes");
                return true;
            }
        }
        return false;
    }

    static void Grava() => File.WriteAllText(Path.Combine(Dir, "relatorio.txt"), sb.ToString(), new UTF8Encoding(false));

    // ------------------------------------------------------------------ tirar as antigas
    static bool TiraAntigas(GameObject raiz)
    {
        if (!AssetDatabase.IsValidFolder(PastaMalhas))
        {
            var pai = Path.GetDirectoryName(PastaMalhas).Replace('\\', '/');
            AssetDatabase.CreateFolder(pai, Path.GetFileName(PastaMalhas));
        }
        var rocha = Caixas.Where(c => c.nome == "PT_Onca_Rocha").ToArray();
        var porGrupo = new Dictionary<string, int>();
        int malhas = 0, tirTotal = 0;

        // 0) instâncias inteiras (LOD0) em cima da rocha: os 3 arbustos. Os limites da malha deles (vale inativo também)
        //    viram caixas extras, para as folhas que passam da caixa da rocha saírem do LOD1/LOD2 e da colisão.
        var arbustos = new List<Transform>();
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("I_")) continue;
            if (t.parent && t.parent.name.StartsWith("I_")) continue; // só o objeto de cima da instância
            if (t.GetComponentInParent<LODGroup>(true) == null) continue;
            if (!rocha.Any(c => c.Dentro(t.position))) continue;
            arbustos.Add(t);
        }
        var cxArb = arbustos.Select(CaixaDe).Where(c => c != null).ToArray();
        foreach (var c in cxArb) L($"  caixa extra {c.nome}: centro {V(c.c)} meia-medida {V(c.h)}");
        var todas = Caixas.Concat(cxArb).ToArray();
        var rochaMais = rocha.Concat(cxArb).ToArray();

        // 1) malhas visuais das células (todas as malhas com renderer, menos o chão e a água, que não têm material de escultura)
        foreach (var mr in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.GetComponentInParent<LODGroup>(true) == null && !mr.name.Contains("_LOD")) continue;
            if (arbustos.Any(a => mr.transform.IsChildOf(a))) continue; // o arbusto inteiro é desligado no passo 3
            var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            if (!Toca(mr.bounds, todas)) continue;
            var mats = mr.sharedMaterials;
            var novo = SemEsculturas(mf.sharedMesh, mr.transform, s =>
            {
                var nm = s < mats.Length && mats[s] ? NomeMat(mats[s].name) : "";
                if (MatsEsc.Any(m => nm.StartsWith(m, StringComparison.OrdinalIgnoreCase))) return todas;
                if (MatsSoRocha.Any(m => nm.StartsWith(m, StringComparison.OrdinalIgnoreCase))) return rochaMais;
                return null;
            }, porGrupo, out int tir, false);
            if (novo == null) continue;
            mf.sharedMesh = Grava(novo, mr.name);
            malhas++; tirTotal += tir;
            L($"  visual {Caminho(mr.transform)}: tirei {tir} triângulos");
        }

        // 2) colisão (menos o chão): triângulo com os 3 vértices dentro de uma caixa (sem material para separar, o centro
        //    não basta: o cascalho em volta da rocha tem triângulos de 77 m² com o centro dentro da caixa da rocha)
        int col = 0;
        foreach (var mc in raiz.GetComponentsInChildren<MeshCollider>(true))
        {
            if (!mc.sharedMesh || mc.name.StartsWith("COL_CH_")) continue;
            if (!Toca(mc.bounds, todas)) continue;
            var novo = SemEsculturas(mc.sharedMesh, mc.transform, s => todas, porGrupo, out int tir, true);
            if (novo == null) continue;
            mc.sharedMesh = Grava(novo, mc.name);
            col++;
            L($"  colisão {Caminho(mc.transform)}: tirei {tir} triângulos");
        }

        // 3) instâncias inteiras (LOD0) em cima da rocha: os arbustos (achados no passo 0)
        int inst = 0;
        foreach (var t in arbustos)
        {
            if (!t.gameObject.activeSelf) continue;
            t.gameObject.SetActive(false); inst++;
            L($"  instância desligada: {Caminho(t)} em {V(t.position)}");
        }

        L($"malhas visuais trocadas: {malhas} · malhas de colisão trocadas: {col} · instâncias desligadas: {inst} · triângulos visuais tirados: {tirTotal}");
        foreach (var kv in porGrupo.OrderBy(k => k.Key)) L($"  triângulos tirados de {kv.Key}: {kv.Value}");
        var faltando = Escs.Select(e => e.id).Where(id => !porGrupo.ContainsKey(id)).ToList();
        if (faltando.Count > 0) L("AVISO: não achei triângulos de: " + string.Join(", ", faltando) + " (já tinham saído ou os nomes dos materiais mudaram)");
        if (malhas == 0 && col == 0 && inst == 0)
        {
            L("AVISO: nenhuma malha tinha escultura antiga (Trocar já rodou?). Sigo só pondo as novas.");
        }
        return true;
    }

    // pré-filtro pelos limites: cobre a caixa inteira, inclusive para baixo até min(c.y - h.y - 0,2; 0) como Caixa.Dentro
    static bool Toca(Bounds b, IEnumerable<Caixa> caixas)
    {
        foreach (var c in caixas)
        {
            float r = Mathf.Sqrt(c.h.x * c.h.x + c.h.z * c.h.z) + 0.3f;
            float y0 = Mathf.Min(c.c.y - c.h.y - 0.2f, 0f) - 0.1f, y1 = c.c.y + c.h.y + 0.3f;
            var cb = new Bounds(); cb.SetMinMax(new Vector3(c.c.x - r, y0, c.c.z - r), new Vector3(c.c.x + r, y1, c.c.z + r));
            if (b.Intersects(cb)) return true;
        }
        return false;
    }

    // caixa alinhada aos eixos com os limites (no mundo) das malhas de uma instância; funciona com o objeto inativo
    static Caixa CaixaDe(Transform t)
    {
        bool tem = false; var b = new Bounds();
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            var mb = mf.sharedMesh.bounds; var m = mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!tem) { b = new Bounds(p, Vector3.zero); tem = true; } else b.Encapsulate(p);
            }
        }
        return tem ? new Caixa("onca", t.name, b.center, Vector3.right, Vector3.forward, b.extents) : null;
    }

    static string NomeMat(string n) => n.Replace(" (Instance)", "").Trim();

    // cópia da malha sem os triângulos que caem nas caixas do submesh (em coordenadas do mundo): pelo centro do triângulo
    // com os 3 vértices perto da mesma caixa, ou, com porVertice, só se os 3 vértices estão dentro da mesma caixa;
    // null se nada saiu
    static Mesh SemEsculturas(Mesh m, Transform t, Func<int, Caixa[]> caixasDoSub, Dictionary<string, int> porGrupo, out int tirados, bool porVertice)
    {
        tirados = 0;
        var v = m.vertices;
        var l2w = t.localToWorldMatrix;
        var vw = new Vector3[v.Length];
        for (int i = 0; i < v.Length; i++) vw[i] = l2w.MultiplyPoint3x4(v[i]);
        var novos = new List<int[]>(); var mudou = new bool[m.subMeshCount];
        for (int s = 0; s < m.subMeshCount; s++)
        {
            var cx = caixasDoSub(s);
            if (cx == null || m.GetTopology(s) != MeshTopology.Triangles) { novos.Add(null); continue; }
            var tri = m.GetTriangles(s);
            var fica = new List<int>(tri.Length);
            for (int i = 0; i + 2 < tri.Length; i += 3)
            {
                Vector3 a = vw[tri[i]], b = vw[tri[i + 1]], d = vw[tri[i + 2]], c = (a + b + d) / 3f;
                Caixa dentro = null;
                foreach (var k in cx)
                    if (porVertice ? k.Dentro(a) && k.Dentro(b) && k.Dentro(d) : k.Dentro(c) && k.Perto(a) && k.Perto(b) && k.Perto(d)) { dentro = k; break; }
                if (dentro != null)
                {
                    tirados++; mudou[s] = true;
                    porGrupo.TryGetValue(dentro.grupo, out int n); porGrupo[dentro.grupo] = n + 1;
                    continue;
                }
                fica.Add(tri[i]); fica.Add(tri[i + 1]); fica.Add(tri[i + 2]);
            }
            novos.Add(fica.ToArray());
        }
        if (tirados == 0) return null;
        var novo = Object.Instantiate(m);
        novo.name = m.name + "_semEsc";
        if (novo.vertexCount > 65535) novo.indexFormat = IndexFormat.UInt32;
        for (int s = 0; s < m.subMeshCount; s++)
            if (mudou[s]) novo.SetTriangles(novos[s], s, false, 0);
        novo.RecalculateBounds();
        return novo;
    }

    static Mesh Grava(Mesh novo, string nomeObj)
    {
        var nome = Regex.Replace(nomeObj, @"[^A-Za-z0-9_\-]", "_") + "_semEsc";
        var path = $"{PastaMalhas}/{nome}.asset";
        int k = 1;
        while (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) path = $"{PastaMalhas}/{nome}_{k++}.asset";
        AssetDatabase.CreateAsset(novo, path);
        return novo;
    }

    // ------------------------------------------------------------------ pôr as novas
    static GameObject[] CarregaModelo(string arq)
    {
        var junto = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaEsc}/{arq}_LODs.glb");
        if (junto) return new[] { junto };
        var partes = Enumerable.Range(0, 3).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaEsc}/{arq}_LOD{i}.glb")).ToArray();
        if (partes[0] == null) return null;
        // fica com 3 posições (null onde falta o arquivo): o índice é o nível; compactar faria o LOD2 virar LOD1
        return partes;
    }

    static readonly Regex RxLod = new Regex(@"LOD[ _\-]?(\d)", RegexOptions.IgnoreCase);

    static bool PoeNova(Esc e, GameObject[] modelo, Transform grupo, Dictionary<string, float[]> aj, float corte)
    {
        aj.TryGetValue(e.id, out var a); // giro, escala, dx, dy, dz
        float giro = a != null ? a[0] : 0f, esc = a != null ? a[1] : 1f;
        var desl = a != null ? new Vector3(a[2], a[3], a[4]) : Vector3.zero;

        var raiz = new GameObject("ESC_" + e.id); raiz.layer = LAYER_MAPA;
        raiz.transform.SetParent(grupo, false);
        var niveis = new List<Renderer>[3] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
        bool separado = modelo.Length > 1;
        Transform topo = null;
        for (int p = 0; p < modelo.Length; p++)
        {
            if (!modelo[p]) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo[p]);
            if (!go) go = Object.Instantiate(modelo[p]);
            if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(raiz.transform, false);
            if (!topo) topo = go.transform;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                int nivel = separado ? p : NivelPeloNome(r.transform, go.transform);
                if (nivel >= 0 && nivel < 3) niveis[nivel].Add(r);
            }
        }
        // _LODs.glb sem nome de LOD: um nível por nó de cima do GLB, ordenados pelos triângulos (o mais pesado é o LOD0).
        // Agrupar pelo nó (e não por renderer) porque o glTFast põe filhos "<malha>_1" quando uma malha tem layouts diferentes.
        var todos = raiz.GetComponentsInChildren<Renderer>(true);
        if (niveis[0].Count == 0)
        {
            foreach (var n in niveis) n.Clear();
            var grupos = PorNoDeCima(todos, separado || !topo ? raiz.transform : topo).OrderByDescending(g => g.Sum(Tris)).ToList();
            if (grupos.Count <= 3) for (int i = 0; i < grupos.Count; i++) niveis[i].AddRange(grupos[i]);
            else { niveis[0].AddRange(todos); L($"  {e.id}: AVISO {grupos.Count} nós no GLB, não sei separar os LODs: tudo no LOD0 (confira a foto)"); }
            L($"  {e.id}: LOD pelo número de triângulos ({grupos.Count} nós, {todos.Length} malhas)");
        }
        var semNivel = todos.Where(r => !niveis.Any(n => n.Contains(r))).ToList();
        if (semNivel.Count > 0) L($"  {e.id}: AVISO {semNivel.Count} malha(s) sem LOD no nome, fora do LODGroup (aparecem sempre): " + string.Join(", ", semNivel.Select(r => r.name)));

        // posição: chão (só colisores COL_CH_) no ponto da escultura antiga
        var p0 = new Vector3(e.pos.x + desl.x, 0f, e.pos.z + desl.z);
        float chao = Chao(p0.x, p0.z);
        raiz.transform.SetPositionAndRotation(new Vector3(p0.x, chao + desl.y, p0.z), Quaternion.Euler(0f, e.yaw + giro, 0f));
        raiz.transform.localScale = Vector3.one * esc;

        foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LAYER_MAPA;
        foreach (var r in todos)
        {
            r.lightProbeUsage = LightProbeUsage.Off;
            r.shadowCastingMode = niveis[2].Contains(r) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }

        // LOD por distância, igual às células da arena (lodBias 1): LOD0 até 40 m, LOD1 até 100 m, LOD2 até o corte da célula
        var lg = raiz.AddComponent<LODGroup>();
        lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
        lg.SetLODs(new[] { new LOD(0.01f, todos) }); lg.RecalculateBounds();
        // lg.size é no espaço local do LODGroup; o Unity compara a altura na tela com size * escala do mundo,
        // então com escala != 1 as distâncias (e o corte) iam junto com a escala
        var ls = raiz.transform.lossyScale;
        float tam = Mathf.Max(lg.size * Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z)), 0.5f);
        var dists = new[] { 40f, 100f, corte };
        var lods = new List<LOD>(); float ult = 1f;
        for (int i = 0; i < 3; i++)
        {
            if (niveis[i].Count == 0) continue;
            int fim = i; while (fim + 1 < 3 && niveis[fim + 1].Count == 0) fim++;
            float rel = Mathf.Min(Rel(tam, dists[fim]), ult * 0.95f); ult = rel;
            lods.Add(new LOD(rel, niveis[i].ToArray()));
        }
        lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();

        // colisão: caixas no espaço da escultura, a partir do LOD0 (onça: rocha embaixo e corpo em cima)
        var vs = VerticesLocais(niveis[0].Count > 0 ? niveis[0] : todos.ToList(), raiz.transform);
        int nCaixas = 0;
        if (vs.Count > 0)
        {
            float y0 = vs.Min(p => p.y), y1 = vs.Max(p => p.y);
            if (e.id == "onca")
            {
                float meio = y0 + (y1 - y0) * 0.45f;
                nCaixas += PoeCaixa(raiz.transform, vs.Where(p => p.y <= meio).ToList(), 0.95f, e.id + "_rocha");
                nCaixas += PoeCaixa(raiz.transform, vs.Where(p => p.y >= meio).ToList(), 0.85f, e.id + "_corpo");
            }
            else nCaixas += PoeCaixa(raiz.transform, vs, 0.85f, e.id);
            L($"  {e.id}: {e.arquivo} em {V(raiz.transform.position)} giro {e.yaw + giro:0.0}° escala {esc:0.00} · chão {chao:0.00} · altura {(y1 - y0) * esc:0.00} m · " +
              $"LODs {string.Join("/", niveis.Select(n => n.Sum(Tris)))} triângulos · limites {string.Join("/", lods.Select(l => l.screenRelativeTransitionHeight.ToString("0.0000", IC)))} · tamanho {tam:0.0} · {nCaixas} caixa(s) de colisão");
        }
        else L($"  {e.id}: AVISO sem vértices no LOD0, sem colisão");
        return true;
    }

    static int NivelPeloNome(Transform t, Transform topo)
    {
        for (var x = t; x != null; x = x.parent)
        {
            var m = RxLod.Match(x.name);
            if (m.Success) return int.Parse(m.Groups[1].Value);
            if (x == topo) break;
        }
        return -1;
    }

    // renderers agrupados pelo nó logo abaixo da "cena" do GLB (desce pela corrente de filho único sem malha:
    // raiz do arquivo -> "Scene" do glTFast -> nó vazio do exportador)
    static List<List<Renderer>> PorNoDeCima(Renderer[] rs, Transform topo)
    {
        var cena = topo;
        while (cena.childCount == 1 && !cena.GetComponent<Renderer>()) cena = cena.GetChild(0);
        var d = new Dictionary<Transform, List<Renderer>>(); var ordem = new List<Transform>();
        foreach (var r in rs)
        {
            var x = r.transform;
            while (x != cena && x.parent != null && x.parent != cena) x = x.parent;
            if (!d.TryGetValue(x, out var l)) { d[x] = l = new List<Renderer>(); ordem.Add(x); }
            l.Add(r);
        }
        return ordem.Select(t => d[t]).ToList();
    }

    static int Tris(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh) { long n = 0; for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) n += mf.sharedMesh.GetIndexCount(s); return (int)(n / 3); }
        if (r is SkinnedMeshRenderer sk && sk.sharedMesh) { long n = 0; for (int s = 0; s < sk.sharedMesh.subMeshCount; s++) n += sk.sharedMesh.GetIndexCount(s); return (int)(n / 3); }
        return 0;
    }

    static float Rel(float tamanho, float d) => Mathf.Clamp(tamanho / (2f * d * Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad)), 0.0005f, 0.99f);

    static List<Vector3> VerticesLocais(List<Renderer> rs, Transform raiz)
    {
        var w2l = raiz.worldToLocalMatrix; var lista = new List<Vector3>();
        foreach (var r in rs)
        {
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var m = r.transform.localToWorldMatrix;
            var v = mf.sharedMesh.vertices;
            int passo = Mathf.Max(1, v.Length / 20000);
            for (int i = 0; i < v.Length; i += passo) lista.Add(w2l.MultiplyPoint3x4(m.MultiplyPoint3x4(v[i])));
        }
        return lista;
    }

    static int PoeCaixa(Transform raiz, List<Vector3> vs, float encolhe, string nome)
    {
        if (vs.Count < 4) return 0;
        var b = new Bounds(vs[0], Vector3.zero); foreach (var p in vs) b.Encapsulate(p);
        var go = new GameObject("COL_ESC_" + nome + "_concreto"); go.layer = LAYER_MAPA;
        go.transform.SetParent(raiz, false);
        var bc = go.AddComponent<BoxCollider>();
        bc.center = b.center; bc.size = new Vector3(b.size.x * encolhe, b.size.y, b.size.z * encolhe);
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
        return 1;
    }

    static float Chao(float x, float z)
    {
        var hits = Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore);
        float melhor = float.NegativeInfinity;
        foreach (var h in hits)
            if (h.collider && h.collider.name.StartsWith("COL_CH_") && h.point.y > melhor) melhor = h.point.y;
        if (float.IsNegativeInfinity(melhor)) return 0.15f;
        // v3: uma camada fina por cima do chão (cascalho, piso) até 12 cm também conta. A onça e os filhotes ficam no
        // cascalho vermelho, que é 3 a 5 cm mais alto que o COL_CH_ (sem isso eles ficavam com a base enterrada).
        float topo = melhor;
        foreach (var h in hits)
            if (h.collider && h.collider.name.StartsWith("COL_") && !h.collider.name.StartsWith("COL_CH_") && h.point.y > topo && h.point.y <= melhor + 0.12f) topo = h.point.y;
        return topo;
    }

    static float LeCorte()
    {
        try
        {
            var p = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Dados));
            if (File.Exists(p))
            {
                var m = Regex.Match(File.ReadAllText(p), "\"lod_dist\"\\s*:\\s*\\[\\s*([0-9.]+)\\s*,\\s*([0-9.]+)\\s*,\\s*([0-9.]+)");
                if (m.Success) return float.Parse(m.Groups[3].Value, IC);
            }
        }
        catch { }
        return 380f;
    }

    // _diag\esculturas_pt_ajuste.txt: uma linha por escultura, ex.: "onca giro=90 escala=1 dx=0 dy=0 dz=0" (# comenta)
    static Dictionary<string, float[]> LeAjustes()
    {
        var d = new Dictionary<string, float[]>();
        if (!File.Exists(ArqAjuste)) return d;
        foreach (var linha0 in File.ReadAllLines(ArqAjuste))
        {
            var linha = linha0.Split('#')[0].Trim(); if (linha.Length == 0) continue;
            var partes = linha.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var v = new float[] { 0f, 1f, 0f, 0f, 0f };
            foreach (var kv in partes.Skip(1))
            {
                var p = kv.Split('='); if (p.Length != 2 || !float.TryParse(p[1].Replace(',', '.'), NumberStyles.Float, IC, out var f)) continue;
                switch (p[0].ToLowerInvariant()) { case "giro": v[0] = f; break; case "escala": v[1] = f; break; case "dx": v[2] = f; break; case "dy": v[3] = f; break; case "dz": v[4] = f; break; }
            }
            d[partes[0]] = v;
            L("ajuste " + linha);
        }
        return d;
    }

    // ------------------------------------------------------------------ fotos (renderiza 2 vezes: a 1ª depois do OpenScene perde o chão)
    static void Fotos(string quando)
    {
        var go = EditorUtility.CreateGameObjectWithHideFlags("ESC_FotoCam", HideFlags.HideAndDontSave, typeof(Camera), typeof(UniversalAdditionalCameraData));
        var cam = go.GetComponent<Camera>(); cam.fieldOfView = 50f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 1500f;
        var ucd = go.GetComponent<UniversalAdditionalCameraData>(); ucd.renderPostProcessing = true;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        cam.targetTexture = rt;
        float bias = QualitySettings.lodBias; QualitySettings.lodBias = 1f;
        try
        {
            foreach (var e in Escs)
            {
                float ang = e.yaw * Mathf.Deg2Rad;
                var frente = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                float chao = Chao(e.pos.x, e.pos.z);
                var alvo = new Vector3(e.pos.x, chao + (e.id == "onca" ? 2.5f : 1.0f), e.pos.z);
                // de frente para a cabeça, um pouco de lado
                var dir = (Quaternion.Euler(0f, 25f, 0f) * frente).normalized;
                var pos = new Vector3(e.pos.x, 0f, e.pos.z) + dir * e.dist;
                pos.y = Chao(pos.x, pos.z) + 1.7f;
                go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(alvo - pos, Vector3.up));
                for (int k = 0; k < 2; k++) cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(Dir, $"{quando}_{e.id}.jpg"), tex.EncodeToJPG(88));
            }
            L($"fotos {quando}: {Escs.Length}");
        }
        catch (Exception ex) { L("fotos falharam: " + ex.Message); }
        finally
        {
            QualitySettings.lodBias = bias;
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
    }

    static string Caminho(Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }
}
