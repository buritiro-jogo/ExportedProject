// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/BuritiroImportarRecursos.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// BURITIRO: importa as texturas PBR e as árvores com LOD de BURITIRO_RECURSOS_GRAFICOS_2.
// Menu: BURITIRO > Importar árvores e texturas PBR   |   BURITIRO > Plantar árvores de teste
//       BURITIRO > Aplicar pisos emborrachados PBR (cena aberta)
// Batch: Unity.exe -batchmode -projectPath <proj> -executeMethod BuritiroImportarRecursos.Importar -quit
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class BuritiroImportarRecursos
{
    const string Raiz = "Assets/BURITIRO/Recursos2";
    const string PastaMat = Raiz + "/Materiais";
    const string PastaPrefab = Raiz + "/Prefabs";
    const float Corte = 0.45f;

    [MenuItem("BURITIRO/Importar árvores e texturas PBR")]
    public static void Importar()
    {
        CriarPasta(PastaMat);
        CriarPasta(PastaPrefab);
        int nTex = Texturas();
        int nArv = Arvores();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BURITIRO] Importação concluída: {nTex} materiais PBR e {nArv} árvores (prefabs em {PastaPrefab}).");
    }

    // ---------- Texturas PBR ----------
    static int Texturas()
    {
        string dir = Raiz + "/Texturas";
        if (!Directory.Exists(dir)) { Debug.LogWarning("[BURITIRO] Pasta não encontrada: " + dir); return 0; }
        int n = 0;
        foreach (var pasta in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
        {
            var arqs = Directory.GetFiles(pasta).Select(p => p.Replace('\\', '/')).Where(p => !p.EndsWith(".meta")).ToArray();
            string alb = arqs.FirstOrDefault(p => p.Contains("_albedo"));
            if (alb == null) continue;
            string nor = arqs.FirstOrDefault(p => p.Contains("_normal"));
            string ms = arqs.FirstOrDefault(p => p.Contains("_URP_metal_smooth"));
            string ao = arqs.FirstOrDefault(p => p.Contains("_ao_"));
            ConfigTextura(alb, true, false);
            if (nor != null) ConfigTextura(nor, false, true);
            if (ms != null) ConfigTextura(ms, false, false);
            if (ao != null) ConfigTextura(ao, false, false);

            string nome = Path.GetFileName(pasta);
            var mat = MaterialLit(PastaMat + "/PBR_" + nome + ".mat");
            mat.SetTexture("_BaseMap", Carregar<Texture2D>(alb));
            if (nor != null) { mat.SetTexture("_BumpMap", Carregar<Texture2D>(nor)); mat.EnableKeyword("_NORMALMAP"); }
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", Carregar<Texture2D>(ms));
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else mat.SetFloat("_Smoothness", 0.25f);
            if (ao != null) { mat.SetTexture("_OcclusionMap", Carregar<Texture2D>(ao)); mat.SetFloat("_OcclusionStrength", 1f); mat.EnableKeyword("_OCCLUSIONMAP"); }
            EditorUtility.SetDirty(mat);
            n++;
        }
        return n;
    }

    // ---------- Pisos emborrachados (troca de material no mapa montado) ----------
    // O GLB da Praça do PT (glTFast) traz o piso do parquinho inclusivo como MT_piso_borracha_colorido: só baseColorTexture
    // com manchas chapadas (vermelho, azul, amarelo, verde) e grade de 1 m, sem normal e sem mapa de rugosidade; e o piso do
    // parquinho antigo como MT_piso_emborrachado_vermelho, só cor (baseColorFactor), sem textura nenhuma.
    // Aqui cada um vira URP/Lit: placas de 0,5 m nas cores das fotos + granulado de EPDM + juntas no normal map, bem fosco.
    // Regra = regex no nome do material do GLB. O UV do GLB não muda (os tilings abaixo seguem o UV que o pipeline gera).
    // Fora de Recursos2 de propósito: desfazer_recursos2.ps1 apaga Recursos2 e o piso do mapa ficaria magenta.
    const string RaizPisos = "Assets/BURITIRO/Pisos";
    const string PastaPiso = RaizPisos + "/Texturas/piso_emborrachado";
    const string PastaMatPiso = RaizPisos + "/Materiais";
    class RegraPiso
    {
        public string rx, nome;             // regex no nome do material do glTF; nome do .mat novo em PastaMat
        public string baseMap, normalMap;   // relativos a PastaPiso; null = usa o granulado
        public Color cor = Color.white;     // tinta que multiplica o _BaseMap (granulado claro tem média 0,78)
        public float tilingBase = 1f;       // repetições do _BaseMap por unidade de UV do GLB
        public float tilingDetalhe = 0f;    // 0 = sem detalhe; senão repetições do granulado (Detail Inputs) por unidade de UV
    }
    static readonly RegraPiso[] PISOS =
    {
        // parquinho inclusivo (PT_Parquinho_Inclusivo_Piso, uv_box 20 m): placas no _BaseMap 1x, granulado a cada 0,5 m (40x)
        new RegraPiso { rx = @"^MT_piso_borracha_colorido\b", nome = "PBR_piso_borracha_parquinho",
                        baseMap = "piso_borracha_parquinho/TX_piso_borracha_parquinho_cor_2k.png",
                        normalMap = "piso_borracha_parquinho/TX_piso_borracha_parquinho_normal_2k.png",
                        tilingBase = 1f, tilingDetalhe = 40f },
        // parquinho antigo (PT_Parquinho_Antigo_Piso, UV 1 = 4 m): granulado tingido de vermelho, 1 m por repetição (4x)
        new RegraPiso { rx = @"^MT_piso_emborrachado_vermelho\b", nome = "PBR_piso_borracha_vermelho",
                        cor = new Color(0.75f, 0.29f, 0.25f), tilingBase = 4f },
    };

    [MenuItem("BURITIRO/Aplicar pisos emborrachados PBR (cena aberta)")]
    public static void AplicarPisosMenu()
    {
        int n = AplicarPisos(null);
        Debug.Log($"[BURITIRO] Pisos emborrachados: {n} submalhas trocadas para URP/Lit. Salve a cena.");
    }

    // raiz == null: todas as cenas abertas. Devolve quantas submalhas trocou. Pode rodar de novo (não troca o que já é PBR_).
    public static int AplicarPisos(GameObject raiz)
    {
        CriarPasta(PastaMatPiso);
        var rxs = PISOS.Select(p => new Regex(p.rx, RegexOptions.IgnoreCase)).ToArray();
        var feitos = new Dictionary<RegraPiso, Material>();
        var rends = new List<MeshRenderer>();
        if (raiz != null) rends.AddRange(raiz.GetComponentsInChildren<MeshRenderer>(true));
        else
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var cena = SceneManager.GetSceneAt(s);
                if (!cena.isLoaded) continue;
                foreach (var go in cena.GetRootGameObjects()) rends.AddRange(go.GetComponentsInChildren<MeshRenderer>(true));
            }
        int n = 0;
        foreach (var r in rends)
        {
            var ms = r.sharedMaterials;
            bool mudou = false;
            for (int i = 0; i < ms.Length; i++)
            {
                if (ms[i] == null) continue;
                for (int k = 0; k < PISOS.Length; k++)
                {
                    if (!rxs[k].IsMatch(ms[i].name)) continue;
                    if (!feitos.TryGetValue(PISOS[k], out var m)) { m = MaterialPiso(PISOS[k]); feitos[PISOS[k]] = m; }
                    if (m != null && ms[i] != m) { ms[i] = m; mudou = true; n++; }
                    break;
                }
            }
            if (!mudou) continue;
            Undo.RecordObject(r, "Pisos emborrachados PBR");
            r.sharedMaterials = ms;
            EditorSceneManager.MarkSceneDirty(r.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
        return n;
    }

    static Material MaterialPiso(RegraPiso p)
    {
        string g = PastaPiso + "/piso_borracha_granulado/piso_borracha_granulado_";
        string albG = g + "albedo_2k.jpg", detG = g + "detalhe_2k.png", norG = g + "normal_gl_2k.jpg",
               msG = g + "URP_metal_smooth_2k.png", aoG = g + "ao_2k.jpg";
        string alb = p.baseMap != null ? PastaPiso + "/" + p.baseMap : albG;
        string nor = p.normalMap != null ? PastaPiso + "/" + p.normalMap : norG;
        foreach (var arq in new[] { alb, nor, albG, norG, detG, msG, aoG })
            if (!File.Exists(arq)) { Debug.LogWarning("[BURITIRO] Falta " + arq + " (rode aplicar_recursos2.ps1, que copia 06_PISOS para Assets/BURITIRO/Pisos/Texturas)"); return null; }
        ConfigTextura(alb, true, false); ConfigTextura(nor, false, true); ConfigTextura(norG, false, true);
        AnisoPiso(alb); AnisoPiso(nor);

        var mat = MaterialLit(PastaMatPiso + "/" + p.nome + ".mat");
        mat.SetTexture("_BaseMap", Carregar<Texture2D>(alb));
        mat.SetTextureScale("_BaseMap", Vector2.one * p.tilingBase);
        mat.SetColor("_BaseColor", p.cor);
        mat.SetTexture("_BumpMap", Carregar<Texture2D>(nor));
        mat.SetFloat("_BumpScale", 0.6f);
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_Metallic", 0f);
        if (p.baseMap == null)
        {   // granulado direto: rugosidade e AO do próprio conjunto
            ConfigTextura(msG, false, false); ConfigTextura(aoG, false, false);
            mat.SetTexture("_MetallicGlossMap", Carregar<Texture2D>(msG));
            mat.SetFloat("_SmoothnessTextureChannel", 0f);
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetTexture("_OcclusionMap", Carregar<Texture2D>(aoG));
            mat.SetFloat("_OcclusionStrength", 0.6f);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }
        else mat.SetFloat("_Smoothness", 0.1f);   // borracha fosca (o GLB tinha roughness 0,9)
        if (p.tilingDetalhe > 0f)
        {   // granulado por cima das placas: Detail Inputs do URP/Lit (albedo x2; cinza 0,5 = neutro, por isso sRGB desligado)
            ConfigTextura(detG, false, false);
            mat.SetTexture("_DetailAlbedoMap", Carregar<Texture2D>(detG));
            mat.SetTextureScale("_DetailAlbedoMap", Vector2.one * p.tilingDetalhe);   // o URP usa o _ST do albedo para os dois
            mat.SetFloat("_DetailAlbedoMapScale", 1f);
            mat.SetTexture("_DetailNormalMap", Carregar<Texture2D>(norG));
            mat.SetFloat("_DetailNormalMapScale", 0.5f);
            mat.EnableKeyword("_DETAIL_MULX2");
        }
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void AnisoPiso(string caminho)
    {   // chão visto rasante: sem anisotrópico as juntas de 0,5 m viram borrão a poucos metros
        var ti = (TextureImporter)AssetImporter.GetAtPath(caminho);
        if (ti == null || ti.anisoLevel >= 8) return;
        ti.anisoLevel = 8;
        ti.SaveAndReimport();
    }

    // ---------- Árvores ----------
    static int Arvores()
    {
        string dir = Raiz + "/Arvores";
        if (!Directory.Exists(dir)) { Debug.LogWarning("[BURITIRO] Pasta não encontrada: " + dir); return 0; }
        int n = 0;
        foreach (var pasta0 in Directory.GetDirectories(dir))
        {
            string pasta = pasta0.Replace('\\', '/');
            string nome = Path.GetFileName(pasta);
            string fbx = Directory.GetFiles(pasta, "*.fbx").Select(p => p.Replace('\\', '/')).FirstOrDefault();
            if (fbx == null) continue;
            string texDir = pasta + "/Texturas";
            string atlas = Directory.GetFiles(pasta, "*_folhas_atlas.png").Select(p => p.Replace('\\', '/')).FirstOrDefault();
            string impostor = Directory.GetFiles(pasta, "*_impostor.png").Select(p => p.Replace('\\', '/')).FirstOrDefault();

            var remap = new Dictionary<string, Material>();
            if (atlas != null)
            {
                ConfigTexturaCartao(atlas);
                remap["arvore_" + nome + "_folhas"] = MaterialCartao(PastaMat + "/Arvore_" + nome + "_folhas.mat", Carregar<Texture2D>(atlas));
            }
            if (impostor != null)
            {
                ConfigTexturaCartao(impostor);
                remap["arvore_" + nome + "_impostor"] = MaterialCartao(PastaMat + "/Arvore_" + nome + "_impostor.mat", Carregar<Texture2D>(impostor));
            }
            // materiais.txt: <material do FBX>;<textura base>;<normal>
            string lista = pasta + "/materiais.txt";
            if (File.Exists(lista))
            {
                foreach (var linha in File.ReadAllLines(lista))
                {
                    var c = linha.Trim().Split(';');
                    if (c.Length < 3) continue;
                    string b = texDir + "/" + c[1], nr = texDir + "/" + c[2];
                    var mat = MaterialLit(PastaMat + "/Arvore_" + c[0] + ".mat");
                    if (File.Exists(b)) { ConfigTextura(b, true, false); mat.SetTexture("_BaseMap", Carregar<Texture2D>(b)); }
                    if (File.Exists(nr)) { ConfigTextura(nr, false, true); mat.SetTexture("_BumpMap", Carregar<Texture2D>(nr)); mat.EnableKeyword("_NORMALMAP"); }
                    mat.SetFloat("_Smoothness", 0.15f);
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                    remap[c[0]] = mat;
                }
            }
            AssetDatabase.SaveAssets();

            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.addCollider = false;
            mi.generateSecondaryUV = true;
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in remap)
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            mi.SaveAndReimport();

            var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            inst.name = "Arvore_" + nome;
            MontarLOD(inst);
            var rends = inst.GetComponentsInChildren<Renderer>();
            // garante os materiais certos mesmo se o remap não pegar algum nome
            foreach (var r in rends)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string mn = mats[i].name.Replace(" (Instance)", "");
                    if (remap.TryGetValue(mn, out var m)) mats[i] = m;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            // colisor só no tronco (cápsula fina), para jogador e balas não atravessarem
            var col = inst.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = nome.Contains("jacaranda") ? 0.45f : 0.3f;
            col.height = nome.Contains("jacaranda") ? 5f : 3f;
            col.center = new Vector3(0, col.height * 0.5f, 0);
            GameObjectUtility.SetStaticEditorFlags(inst, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

            string caminho = PastaPrefab + "/Arvore_" + nome + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(inst, caminho);
            Object.DestroyImmediate(inst);
            n++;
        }
        return n;
    }

    static void MontarLOD(GameObject raiz)
    {
        var lg = raiz.GetComponent<LODGroup>();
        if (lg == null) lg = raiz.AddComponent<LODGroup>();
        float[] corte = { 0.35f, 0.10f, 0.012f };
        var lods = new List<LOD>();
        for (int i = 0; i < 3; i++)
        {
            var rs = raiz.GetComponentsInChildren<Renderer>(true).Where(r => r.name.EndsWith("_LOD" + i)).ToArray();
            if (rs.Length == 0) continue;
            if (i == 2) foreach (var r in rs) r.shadowCastingMode = ShadowCastingMode.Off;
            lods.Add(new LOD(corte[i], rs));
        }
        if (lods.Count > 0) { lg.SetLODs(lods.ToArray()); lg.fadeMode = LODFadeMode.None; lg.RecalculateBounds(); }
    }

    // ---------- Plantar árvores de teste ----------
    [MenuItem("BURITIRO/Plantar árvores de teste")]
    public static void PlantarTeste()
    {
        Vector3 centro = Selection.activeTransform != null ? Selection.activeTransform.position
                       : (SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero);
        Plantar(centro, 40f, 24);
    }

    // Planta 'qtd' árvores sorteadas num raio, apoiadas no chão (raycast), sob o objeto BURITIRO_ArvoresTeste.
    public static void Plantar(Vector3 centro, float raio, int qtd)
    {
        var prefabs = Directory.Exists(PastaPrefab)
            ? Directory.GetFiles(PastaPrefab, "*.prefab").Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p.Replace('\\', '/'))).Where(g => g != null).ToArray()
            : new GameObject[0];
        if (prefabs.Length == 0) { Debug.LogWarning("[BURITIRO] Rode primeiro: BURITIRO > Importar árvores e texturas PBR"); return; }
        var pai = GameObject.Find("BURITIRO_ArvoresTeste") ?? new GameObject("BURITIRO_ArvoresTeste");
        var rnd = new System.Random(7);
        int feitas = 0;
        for (int t = 0; t < qtd * 6 && feitas < qtd; t++)
        {
            double a = rnd.NextDouble() * Mathf.PI * 2, d = System.Math.Sqrt(rnd.NextDouble()) * raio;
            var p = centro + new Vector3((float)(System.Math.Cos(a) * d), 200f, (float)(System.Math.Sin(a) * d));
            if (!Physics.Raycast(p, Vector3.down, out var hit, 400f) || hit.normal.y < 0.85f) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rnd.Next(prefabs.Length)], pai.transform);
            go.transform.position = hit.point;
            go.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
            go.transform.localScale = Vector3.one * (0.85f + (float)rnd.NextDouble() * 0.3f);
            feitas++;
        }
        Undo.RegisterCreatedObjectUndo(pai, "Plantar árvores");
        Debug.Log($"[BURITIRO] {feitas} árvores plantadas em volta de {centro}.");
    }

    // ---------- utilidades ----------
    static Material MaterialLit(string caminho)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, caminho);
        }
        return mat;
    }

    static Material MaterialCartao(string caminho, Texture2D tex)
    {
        var mat = MaterialLit(caminho);
        mat.SetTexture("_BaseMap", tex);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", Corte);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.SetFloat("_Smoothness", 0.1f);
        // folhas não recebem sombra das próprias folhas: sem isso a parte de baixo da copa fica quase preta
        mat.SetFloat("_ReceiveShadows", 0f);
        // sem auto-sombra a copa fica clara e amarelada demais; escurece e puxa pro verde
        mat.SetColor("_BaseColor", new Color(0.78f, 0.86f, 0.74f, 1f));
        mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
        mat.SetFloat("_SpecularHighlights", 0f);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        mat.SetFloat("_EnvironmentReflections", 0f);
        mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.doubleSidedGI = true;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void ConfigTextura(string caminho, bool srgb, bool normal)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(caminho);
        if (ti == null) return;
        bool muda = ti.sRGBTexture != srgb || (normal && ti.textureType != TextureImporterType.NormalMap);
        if (normal) ti.textureType = TextureImporterType.NormalMap;
        ti.sRGBTexture = srgb;
        ti.maxTextureSize = 2048;
        if (muda) ti.SaveAndReimport();
    }

    static void ConfigTexturaCartao(string caminho)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(caminho);
        if (ti == null) return;
        ti.alphaIsTransparency = true;
        ti.mipMapsPreserveCoverage = true;
        ti.alphaTestReferenceValue = Corte;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.sRGBTexture = true;
        ti.SaveAndReimport();
    }

    static T Carregar<T>(string caminho) where T : Object => AssetDatabase.LoadAssetAtPath<T>(caminho);

    static void CriarPasta(string caminho)
    {
        if (AssetDatabase.IsValidFolder(caminho)) return;
        string pai = Path.GetDirectoryName(caminho).Replace('\\', '/');
        CriarPasta(pai);
        AssetDatabase.CreateFolder(pai, Path.GetFileName(caminho));
    }
}
