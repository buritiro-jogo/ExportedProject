// [unificação 02/11] roteiro único (batch) que remonta, dentro do NOSSO projeto, as cenas que o parceiro montou no ExportedProject
// (as cenas dele vieram do AssetRipper, com GUIDs que não batem com os nossos assets, então não dá para copiar: refazemos com as
// mesmas ferramentas dele, na mesma ordem do RESUMO_PROJETO_BURITIRO.md).
// Uso: Unity.exe -batchmode -projectPath <FreeBala> -executeMethod FBUnificacao.Rodar -quit -logFile <arquivo>
//   (ou só uma parte: FBUnificacao.SoMapas / SoVila / SoAnim / SoBuild)
// Log em <game>\_diag\unificacao.txt. Backups das ferramentas do parceiro em <game>\_backup\ (fora do repositório).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FBUnificacao
{
    const string CENAS = "Assets/FreeBala/Scenes";
    static string Log => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "_diag", "unificacao.txt"));

    static void L(string s)
    {
        Debug.Log("[unificacao] " + s);
        try { Directory.CreateDirectory(Path.GetDirectoryName(Log)); File.AppendAllText(Log, DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n"); } catch { }
    }

    static int falhas;
    static void Etapa(string nome, Action a)
    {
        var t0 = DateTime.Now; L(">> " + nome);
        try { a(); L("ok " + nome + " (" + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s)"); }
        catch (Exception e) { falhas++; L("FALHOU " + nome + ": " + (e.InnerException ?? e)); }
    }

    public static void Rodar() { falhas = 0; Mapas(); Vila(); Anim(); Build(); Fim(); }
    // 2ª passada de 02/11 (mapas já montados; VJ voltada ao original): árvores da VJ, sol das cenas novas, camada BUR e build
    public static void Segunda() { falhas = 0; Vila(); Sol(); Anim(); Build(); Fim(); }
    public static void SoAnim() { falhas = 0; Anim(); Fim(); }

    // camada "BUR Tronco" (peso 0; quem liga é o FBAnimBur com -fbburanim ou burAnim.ligado no fb_data), igual à do parceiro (variante com coluna)
    static void Anim() => Etapa("camada BUR Tronco no controlador", BuritiroEditor.BuritiroAnimBUR.Aplicar);
    public static void SoMapas() { falhas = 0; Mapas(); Build(); Fim(); }
    public static void SoVila() { falhas = 0; Vila(); Fim(); }
    public static void SoBuild() { falhas = 0; Build(); Fim(); }

    // Praça do PT: montar apaga árvores e esculturas, então sempre montar -> plantar -> esculturas (que refazem o NavMesh)
    static void Mapas()
    {
        Etapa("montar Map_praca_pt", BuritiroEditor.BuritiroMontaMapa.MontaPTAuto);
        Etapa("plantar árvores na PT", BuritiroPlantarArvores.PlantarPT);
        Etapa("esculturas Tripo na PT (+ pisos PBR + NavMesh)", BuritiroEsculturasPT.PorSemTirar);
        Etapa("montar Map_orla", BuritiroEditor.BuritiroMontaMapa.MontaOrlaAuto);
        Etapa("montar Map_centro_civico", BuritiroEditor.BuritiroMontaMapa.MontaCentroCivicoAuto);
        foreach (var id in new[] { "praca_pt", "orla", "centro_civico" })
            L($"cena Map_{id}: " + (File.Exists($"{CENAS}/Map_{id}.unity") ? "existe" : "NÃO EXISTE"));
    }

    // Vila Jardim: só as 40 árvores do parceiro. O polimento v2.2b (BuritiroVilaJardim.Tudo) NÃO entra automático: o "Corrigir" foi
    // feito para a VJ extraída pelo AssetRipper (Combined Mesh, shaders roxos) e move objetos ("assenta no chão"), e o "Polir" escureceu
    // a vista 4 em ~23% e tirou saturação do dia (VJ2_Visual por cima do FB_Iluminacao) na passada de 02/11. Aplicar só pelo menu
    // BURITIRO > Vila Jardim, com fotos antes/depois aprovadas (a cena da passada polida ficou em <game>\_backup\unificacao_vj_polida).
    static void Vila()
    {
        Etapa("plantar árvores na Vila Jardim", BuritiroPlantarArvores.PlantarVila);
    }

    // sol registrado (RenderSettings.sun) nas cenas montadas antes da correção do BuritiroMontaMapa.CopiaDoModelo
    public static void SoSol() { falhas = 0; Sol(); Fim(); }
    static void Sol()
    {
        foreach (var id in new[] { "praca_pt", "orla", "centro_civico" })
            Etapa("sol registrado em Map_" + id, () =>
            {
                var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"{CENAS}/Map_{id}.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
                var sol = sc.GetRootGameObjects().Where(g => g.name == "Sol").Select(g => g.GetComponent<Light>()).FirstOrDefault(l => l);
                if (!sol) throw new Exception("sem objeto Sol com Light na raiz");
                // o getter devolve a luz direcional mesmo com m_Sun vazio no arquivo: grava sempre
                RenderSettings.sun = sol;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc)) throw new Exception("não salvou");
            });
    }

    // Lista do build igual à do parceiro, mais o nosso estande (desligado). As cenas *_antiga ficam só no projeto.
    static void Build()
    {
        Etapa("cenas no Build Settings", () =>
        {
            var lista = new List<EditorBuildSettingsScene>();
            void Add(string nome, bool on)
            {
                string p = $"{CENAS}/{nome}.unity";
                if (!File.Exists(p)) { L("  sem a cena " + p); return; }
                lista.Add(new EditorBuildSettingsScene(p, on));
            }
            Add("Menu", true);
            Add("Map_praca_aguas", true);
            Add("Map_vila_jardim", true);
            Add("Map_praca_pt", true);
            Add("Map_orla", true);
            Add("Map_centro_civico", true);
            Add("Map_estande", false);
            EditorBuildSettings.scenes = lista.ToArray();
            foreach (var s in lista) L($"  {(s.enabled ? "on " : "off")} {s.path} {s.guid}");
        });
    }

    static void Fim()
    {
        AssetDatabase.SaveAssets();
        L(falhas == 0 ? "FIM sem falhas" : "FIM com " + falhas + " falha(s)");
        if (Application.isBatchMode) EditorApplication.Exit(falhas == 0 ? 0 : 1);
    }
}
