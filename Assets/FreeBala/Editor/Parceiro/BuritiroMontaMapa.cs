// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/BuritiroMontaMapa.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// BuritiroMontaMapa.cs - monta as cenas Map_praca_pt e Map_orla a partir dos GLB preparados.
// Colocar em Assets/Editor/ (no nosso projeto: Assets/FreeBala/Editor/Parceiro, assembly FreeBala.Editor). Menu: BURITIRO > Mapas.
// Entrada (por mapa): Assets/BURITIRO_Mapas/<id>/<id>_unity.glb e <id>_dados.json (gerados por prepara_unity.py).
// Saida: Assets/FreeBala/Scenes/Map_<id>.unity, NavMesh_<id>.asset e <id>_fb_data_trecho.json.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BuritiroEditor
{
    public static class BuritiroMontaMapa
    {
        const string PASTA = "Assets/BURITIRO_Mapas";
        const string CENAS = "Assets/FreeBala/Scenes";
        const string MODELO = "Assets/FreeBala/Scenes/Map_praca_aguas.unity";
        const int LAYER_MAPA = 8;                       // FBWorld.LMap
        static readonly float[] LOD_CELULA = { 0.30f, 0.12f, 0.035f };   // altura relativa na tela
        static readonly float[] LOD_GRANDE = { 0.20f, 0.02f };
        static readonly float[] LOD_INSTANCIA = { 0.08f, 0.03f, 0.01f };  // postes e bancos repetidos
        static readonly float[] LOD_ARVORE = { 0.35f, 0.17f, 0.008f };     // arvore: LOD1 a ~30 m, impostor (LOD2) a ~60 m
        static readonly System.Text.RegularExpressions.Regex ARVORE = new System.Text.RegularExpressions.Regex("Arvore|Palmeira|Coqueiro|Buriti|Selvinha|Caimbe|Ipe_|Mangueira|Oiti|Nim_|Arbusto|Cajueiro|Jambeiro|Castanh|Flamboy|Acacia|Sibipiruna", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        // nome e neblina iguais aos que o fb_data.json ja tem para esses ids
        static readonly Dictionary<string, (string nome, string fog)> META = new Dictionary<string, (string, string)>
        {
            { "centro_civico", ("Centro Cívico", "{\"color\":\"#b4c6d4\",\"near\":100,\"far\":450}") },
            { "praca_pt", ("Praça do PT", "{\"color\":\"#b4c6d4\",\"near\":80,\"far\":360}") },
            { "orla", ("Orla do Rio Branco", "{\"color\":\"#b4c6d4\",\"near\":120,\"far\":520}") },
            { "morro_sacola", ("Morro da Sacola", "{\"color\":\"#b4c6d4\",\"near\":90,\"far\":440}") },
        };

        // v3b (thread de física): sem janelas, para rodar pelo lab runner ("metodo BuritiroMontaMapa.MontaPTAuto").
        // Com SemDialogo, toda pergunta responde o botão "ok" (Substituir) e as mensagens vão para o log em _diag\monta_mapa.txt.
        public static bool SemDialogo;
        static bool Dlg(string titulo, string msg, string ok, string cancelar = null)
        {
            if (!SemDialogo) return cancelar == null ? EditorUtility.DisplayDialog(titulo, msg, ok) : EditorUtility.DisplayDialog(titulo, msg, ok, cancelar);
            try { File.AppendAllText(Path.Combine(Directory.GetParent(Application.dataPath).Parent.Parent.FullName, "_diag", "monta_mapa.txt"), DateTime.Now.ToString("HH:mm:ss") + "  " + msg.Replace("\n", " | ") + "\r\n"); } catch { }
            Debug.Log("[BURITIRO] " + msg);
            return true;
        }
        static void MontaAuto(string id)
        {
            SemDialogo = true;
            try { Monta(id); }
            finally { SemDialogo = false; }
        }
        public static void MontaPTAuto() => MontaAuto("praca_pt");
        public static void MontaOrlaAuto() => MontaAuto("orla");
        public static void MontaCentroCivicoAuto() => MontaAuto("centro_civico");
        // Morro da Sacola (tools/morro_sacola): monta e já grava o trecho no fb_data (StreamingAssets + cópia em Resources)
        public static void MontaMorroSacolaAuto() { MontaAuto("morro_sacola"); AplicaTrechoAuto("morro_sacola"); }

        // lote: aplica o trecho exportado pelo Monta sem perguntar e deixa Resources/fb_data.json igual ao do StreamingAssets
        public static void AplicaTrechoAuto(string id)
        {
            string trecho = $"{PASTA}/{id}/{id}_fb_data_trecho.json";
            if (!File.Exists(trecho)) { Debug.LogError("[BURITIRO] sem " + trecho + " (a montagem falhou?)"); return; }
            AplicaNoFbData(id, File.ReadAllText(trecho));
            string res = "Assets/FreeBala/Resources/fb_data.json";
            if (File.Exists(res)) { File.Copy(Path.Combine(Application.streamingAssetsPath, "fb_data.json"), res, true); AssetDatabase.ImportAsset(res); }
        }

        [MenuItem("BURITIRO/Mapas/Montar Map_praca_pt (Praça do PT)")]
        static void MontaPT() => Monta("praca_pt");

        [MenuItem("BURITIRO/Mapas/Montar Map_orla (Mirante e Orla)")]
        static void MontaOrla() => Monta("orla");

        [MenuItem("BURITIRO/Mapas/Montar Map_centro_civico (Centro Cívico e Garimpeiro)")]
        public static void MontaCentroCivico() => Monta("centro_civico");

        [MenuItem("BURITIRO/Mapas/Montar Map_morro_sacola (Morro da Sacola)")]
        static void MontaMorroSacola() => Monta("morro_sacola");

        [MenuItem("BURITIRO/Mapas/Refazer NavMesh da cena aberta")]
        static void RefazNav()
        {
            var raiz = RaizMapa();
            if (!raiz) { Dlg("BURITIRO", "Abra uma cena Map_* primeiro.", "OK"); return; }
            string id = raiz.name.Substring(4);
            BakeNavMesh(id, raiz);
            EditorSceneManager.MarkSceneDirty(raiz.scene);
        }

        [MenuItem("BURITIRO/Mapas/Exportar pontos da cena aberta para o fb_data")]
        static void ExportaPontosMenu()
        {
            var raiz = RaizMapa();
            if (!raiz) { Dlg("BURITIRO", "Abra uma cena Map_* primeiro.", "OK"); return; }
            string id = raiz.name.Substring(4);
            string trecho = ExportaTrecho(id, LerDados(id));
            if (Dlg("BURITIRO", "Trecho gravado em\n" + trecho +
                "\n\nAplicar agora no StreamingAssets/fb_data.json? (faz cópia .bak antes)", "Aplicar", "Só o trecho"))
                AplicaNoFbData(id, File.ReadAllText(trecho));
        }

        // ------------------------------------------------------------------
        public static void Monta(string id)
        {
            string glb = $"{PASTA}/{id}/{id}_unity.glb";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(glb);
            if (!asset) { Dlg("BURITIRO", "Não achei " + glb + "\nCopie a pasta " + id + " para " + PASTA + " e espere o glTFast importar.", "OK"); return; }
            var dados = LerDados(id);
            if (dados == null) return;
            if (!SemDialogo && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string cenaPath = $"{CENAS}/Map_{id}.unity";
            if (File.Exists(cenaPath) && !Dlg("BURITIRO", cenaPath + " já existe. Substituir?", "Substituir", "Cancelar")) return;

            try
            {
                EditorUtility.DisplayProgressBar("BURITIRO", "Criando cena", 0.05f);
                var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CopiaDoModelo(cena, id);

                EditorUtility.DisplayProgressBar("BURITIRO", "Instanciando o mapa", 0.15f);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, cena);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var mapa = Acha(inst.transform, "Mapa_" + id) ?? inst.transform;
                mapa.SetParent(null, true);
                if (mapa.gameObject != inst) UnityEngine.Object.DestroyImmediate(inst);
                var raiz = mapa.gameObject;
                raiz.name = "Map_" + id;

                var conv = Conversor.De(raiz.transform);
                var reflexo = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Reflexo");
                if (reflexo && reflexo.TryGetComponent<ReflectionProbe>(out var rp))
                {   // caixa do reflexo cobrindo o miolo do mapa novo
                    reflexo.transform.position = conv.Ponto(new[] { dados.centro[0], dados.centro[1], 6f });
                    rp.size = new Vector3(2 * dados.raio, 110f, 2 * dados.raio);
                }

                EditorUtility.DisplayProgressBar("BURITIRO", "LODs por célula", 0.3f);
                int nLod = dados.lodDist != null && dados.lodDist.Length >= 3 ? MontaLODsHLOD(raiz.transform, dados.lodDist, dados) : MontaLODs(raiz.transform);

                EditorUtility.DisplayProgressBar("BURITIRO", "Colisores", 0.45f);
                int nCol = MontaColisores(raiz.transform);

                foreach (var t in raiz.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LAYER_MAPA;
                foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                    r.lightProbeUsage = LightProbeUsage.Off;
                }
                foreach (var a in Filhos(raiz.transform, "Agua")) foreach (var r in a.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var a in Filhos(raiz.transform, "Paisagem")) foreach (var r in a.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                // v6: chao (piso, calcada, grama) nao projeta sombra em arena; recebe normalmente. Na Orla sao ~470 casters a menos.
                if (dados.lodDist != null && dados.lodDist.Length >= 3) foreach (var a in Filhos(raiz.transform, "Chao")) foreach (var r in a.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                foreach (var a in Filhos(raiz.transform, "Horizonte")) foreach (var r in a.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                // v7 (arenas): objeto pequeno (banco, lixeira, frade, arbusto; caixa < 2,5 m) e fiacao nao projetam sombra.
                // Na Orla eram a maior parte dos ~1.600 casters. Continuam recebendo sombra.
                if (dados.lodDist != null && dados.lodDist.Length >= 3)
                    foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var b = r.bounds.size; float tam = Mathf.Max(b.x, Mathf.Max(b.y, b.z));
                        if ((r.name.StartsWith("I_") && tam < 2.5f) || r.name.Contains("Fiacao")) r.shadowCastingMode = ShadowCastingMode.Off;
                    }

                EditorUtility.DisplayProgressBar("BURITIRO", "Pontos de spawn e vagas", 0.55f);
                Physics.SyncTransforms();
                MontaPontos(raiz.transform, conv, dados);
                var refs = Acha(raiz.transform, "REF");
                if (refs) UnityEngine.Object.DestroyImmediate(refs.gameObject);

                EditorUtility.DisplayProgressBar("BURITIRO", "NavMesh (pode demorar alguns minutos)", 0.65f);
                BakeNavMesh(id, raiz, conv, dados);

                EditorUtility.DisplayProgressBar("BURITIRO", "Gravando", 0.95f);
                Directory.CreateDirectory(CENAS);
                EditorSceneManager.SaveScene(cena, cenaPath);
                AdicionaNoBuild(cenaPath);
                string trecho = ExportaTrecho(id, dados);
                Debug.Log($"[BURITIRO] {cenaPath} montada: {nLod} grupos de LOD, {nCol} colisores. Trecho do fb_data em {trecho}");
                EditorUtility.ClearProgressBar();
                Dlg("BURITIRO", $"Map_{id} montada.\n{nLod} grupos de LOD, {nCol} colisores.\n\nTrecho do fb_data: {trecho}\nPara o mapa aparecer no menu, falta liberar o id em GameData.AllowedMaps e no filtro do FBMenu (veja o LEIA-ME).", "OK");
            }
            catch (Exception e) { Debug.LogException(e); Dlg("BURITIRO", "Erro: " + e.Message, "OK"); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        // Jogo (FBMatch), Sol, Reflexo e o céu vêm da cena Map_praca_aguas
        static void CopiaDoModelo(Scene cena, string id)
        {
            if (!File.Exists(MODELO)) { Debug.LogWarning("[BURITIRO] modelo " + MODELO + " não existe; crie Jogo/Sol na mão."); return; }
            var modelo = EditorSceneManager.OpenScene(MODELO, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(modelo);
            var sky = RenderSettings.skybox; var amb = RenderSettings.ambientMode; var ambI = RenderSettings.ambientIntensity;
            var aSky = RenderSettings.ambientSkyColor; var aEq = RenderSettings.ambientEquatorColor; var aGr = RenderSettings.ambientGroundColor;
            var fog = RenderSettings.fog; var fogC = RenderSettings.fogColor; var fogM = RenderSettings.fogMode;
            var fogS = RenderSettings.fogStartDistance; var fogE = RenderSettings.fogEndDistance; var fogD = RenderSettings.fogDensity;
            var refl = RenderSettings.defaultReflectionMode; var reflI = RenderSettings.reflectionIntensity;
            Light sol = null;
            foreach (var go in modelo.GetRootGameObjects())
            {
                if (go.name != "Jogo" && go.name != "Sol" && go.name != "Reflexo") continue;
                var c = UnityEngine.Object.Instantiate(go); c.name = go.name;
                if (go.name == "Sol") sol = c.GetComponent<Light>();
                SceneManager.MoveGameObjectToScene(c, cena);
                if (go.name == "Jogo")
                    foreach (var mb in c.GetComponents<MonoBehaviour>())
                    {
                        var so = new SerializedObject(mb); var p = so.FindProperty("mapId");
                        if (p != null) { p.stringValue = id; so.ApplyModifiedPropertiesWithoutUndo(); }
                    }
            }
            SceneManager.SetActiveScene(cena);
            EditorSceneManager.CloseScene(modelo, true);
            if (sol) RenderSettings.sun = sol;   // [unificação 02/11] o modelo tem o sol registrado; sem isso m_Sun ficava vazio (FBSkyPlanes usa)
            RenderSettings.skybox = sky; RenderSettings.ambientMode = amb; RenderSettings.ambientIntensity = ambI;
            RenderSettings.ambientSkyColor = aSky; RenderSettings.ambientEquatorColor = aEq; RenderSettings.ambientGroundColor = aGr;
            RenderSettings.fog = fog; RenderSettings.fogColor = fogC; RenderSettings.fogMode = fogM;
            RenderSettings.fogStartDistance = fogS; RenderSettings.fogEndDistance = fogE; RenderSettings.fogDensity = fogD;
            RenderSettings.defaultReflectionMode = refl; RenderSettings.reflectionIntensity = reflI;
        }

        // ------------------------------------------------------------------
        static int MontaLODs(Transform raiz)
        {
            int n = 0;
            foreach (var grupo in new[] { "Celulas", "Grandes" })
            {
                var g = Acha(raiz, grupo); if (!g) continue;
                var limites = grupo == "Celulas" ? LOD_CELULA : LOD_GRANDE;
                foreach (Transform c in g)
                {
                    // a celula leva so as malhas juntadas; as instancias (C_x_y_Inst/I_*) ganham LODGroup proprio
                    var rs = c.GetComponentsInChildren<Renderer>(true).Where(r => !r.name.StartsWith("I_")).ToArray();
                    if (PoeLOD(c.gameObject, rs, limites)) n++;
                    foreach (Transform sub in c)
                    {
                        if (!sub.name.EndsWith("_Inst")) continue;
                        foreach (Transform inst in sub)
                            if (PoeLOD(inst.gameObject, inst.GetComponentsInChildren<Renderer>(true), ARVORE.IsMatch(inst.name) ? LOD_ARVORE : LOD_INSTANCIA)) n++;
                    }
                }
            }
            return n;
        }

        // v4 (HLOD): um LODGroup so por celula. LOD0 = malha juntada + instancias (postes, arvores...) inteiras;
        // LOD1 e LOD2 = tudo da celula juntado numa malha (instancias inclusive), sem sombra no LOD2;
        // depois de dist[2] metros a celula some (corte por distancia fixa, a neblina ja escondeu).
        // Os limites saem da distancia: altura relativa = tamanho / (2 * d * tan(fov/2)), fov 60.
        const float FOV = 60f;
        // As distancias valem com QualitySettings.lodBias = 1. O Ultra do jogo (FBQuality) usa 2,4: ai o Unity estica tudo
        // 2,4x (LOD0 da celula de 96 m ate ~220 m, corte em ~1,2 km) e o ganho de draw calls some. Nas arenas o
        // lodBias tem que ficar em 1 (pedido a thread de fisica). Um limite nao passa de 1, entao nao da para compensar aqui.
        const float BIAS_JOGO = 1f;
        static float Rel(float tamanho, float d) => Mathf.Clamp(BIAS_JOGO * tamanho / (2f * d * Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad)), 0.0005f, 0.99f);
        static bool PoeLODDist(GameObject go, Renderer[] rs, float[] dist, int niveis)
        {
            var grupos = new List<Renderer[]>();
            for (int i = 0; i < niveis; i++) grupos.Add(rs.Where(r => r.name.EndsWith("_LOD" + i)).ToArray());
            if (grupos.All(g => g.Length == 0)) return false;
            var lg = go.GetComponent<LODGroup>(); if (!lg) lg = go.AddComponent<LODGroup>();
            lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            // primeiro so para medir o tamanho do grupo
            lg.SetLODs(new[] { new LOD(0.01f, rs) }); lg.RecalculateBounds();
            float tam = Mathf.Max(lg.size, 1f);
            var lods = new List<LOD>(); float ult = 1f;
            for (int i = 0; i < niveis; i++)
            {
                if (grupos[i].Length == 0) continue;
                // ate onde este nivel fica: o fim do proximo nivel que existe, ou o corte
                int fim = i; while (fim + 1 < niveis && grupos[fim + 1].Length == 0) fim++;
                float d = dist[Mathf.Min(fim, dist.Length - 1)];   // dist[niveis-1] = corte
                float rel = Mathf.Min(Rel(tam, d), ult * 0.95f); ult = rel;
                lods.Add(new LOD(rel, grupos[i]));
                if (i == 2) foreach (var r in grupos[i]) r.shadowCastingMode = ShadowCastingMode.Off;
            }
            lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
            return true;
        }

        static int MontaLODsHLOD(Transform raiz, float[] dist, Dados dados)
        {
            int n = 0;
            var g = Acha(raiz, "Celulas");
            if (g) foreach (Transform c in g)
                    if (PoeLODDist(c.gameObject, c.GetComponentsInChildren<Renderer>(true), dist, 3)) n++;
            g = Acha(raiz, "Grandes");
            // v7: com lod_dist_grandes os grandes (muros por quadra) ganham LOD2 sem sombra; sem ele, 2 niveis como na v4
            var dg = dados.lodDistGrandes != null && dados.lodDistGrandes.Length >= 3 ? dados.lodDistGrandes : new[] { dist[1], dist[2] * 1.5f };
            if (g) foreach (Transform c in g)
                    if (PoeLODDist(c.gameObject, c.GetComponentsInChildren<Renderer>(true), dg, dg.Length)) n++;
            // v7: chao em ladrilhos de 96 m (Chao_x_y com _LOD0/_LOD1); ladrilho antigo sem sufixo continua sem LOD
            g = Acha(raiz, "Chao");
            if (g && dados.lodDistChao != null && dados.lodDistChao.Length >= 2) foreach (Transform c in g)
                    if (PoeLODDist(c.gameObject, c.GetComponentsInChildren<Renderer>(true), dados.lodDistChao, 2)) n++;
            // horizonte: um nivel so, cortado logo depois da neblina
            g = Acha(raiz, "Horizonte");
            if (g) foreach (Transform h in g)
                {
                    var rs = h.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                    var lg = h.GetComponent<LODGroup>(); if (!lg) lg = h.gameObject.AddComponent<LODGroup>();
                    lg.SetLODs(new[] { new LOD(0.01f, rs) }); lg.RecalculateBounds();
                    lg.SetLODs(new[] { new LOD(Rel(Mathf.Max(lg.size, 1f), dist[2] * 1.6f), rs) }); n++;
                }
            return n;
        }

        static bool PoeLOD(GameObject go, Renderer[] rs, float[] limites)
        {
            var lods = new List<LOD>();
            for (int i = 0; i < limites.Length; i++)
            {
                var ri = rs.Where(r => r.name.EndsWith("_LOD" + i)).ToArray();
                if (ri.Length > 0) lods.Add(new LOD(limites[i], ri));
            }
            if (lods.Count == 0) return false;
            var lg = go.GetComponent<LODGroup>(); if (!lg) lg = go.AddComponent<LODGroup>(); // "??" não serve: o Unity devolve um nulo falso
            lg.fadeMode = LODFadeMode.CrossFade; lg.animateCrossFading = true;
            lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
            return true;
        }

        static int MontaColisores(Transform raiz)
        {
            var g = Acha(raiz, "Colisao"); if (!g) return 0;
            int n = 0;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh; var go = mf.gameObject;
                var mr = go.GetComponent<MeshRenderer>(); if (mr) UnityEngine.Object.DestroyImmediate(mr);
                UnityEngine.Object.DestroyImmediate(mf);
                if (!mesh) continue;
                var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.EnableMeshCleaning | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.UseFastMidphase;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------
        static void MontaPontos(Transform raiz, Conversor conv, Dados d)
        {
            var velho = Acha(raiz, "Pontos"); if (velho) UnityEngine.Object.DestroyImmediate(velho.gameObject);
            var pontos = new GameObject("Pontos").transform;
            void Lista(string k, List<float[]> l, int icone)
            {
                if (l == null) return;
                for (int i = 0; i < l.Count; i++)
                {
                    var t = new GameObject($"{k}_{i}").transform; t.SetParent(pontos, false);
                    t.position = Assenta(conv.Ponto(l[i]));
                    Icone(t.gameObject, icone);
                }
            }
            Lista("spawnA", d.spawnA, 1); Lista("spawnB", d.spawnB, 6); Lista("spawnPolice", d.spawnPolice, 3);
            Lista("points", d.points, 4); Lista("bots", d.bots, 5); Lista("waypoints", d.waypoints, 0);
            for (int r = 0; r < d.lanes.Count; r++) Lista("lane_" + r, d.lanes[r], 7);           // rotas do 10x10
            for (int r = 0; r < d.basePairs.Count; r++) Lista("basePair_" + r, d.basePairs[r], 1); // pares de bases por rodada
            if (d.arena != null)
            {   // centro da arena (arena_0) e cantos das paredes (wall_0..): o export le daqui, sem precisar dos marcadores REF
                var t = new GameObject("arena_0").transform; t.SetParent(pontos, false); t.position = conv.Ponto(new[] { d.arena[0], d.arena[1], 0f });
                for (int i = 0; i < d.walls.Count; i++)
                {
                    var w = new GameObject("wall_" + i).transform; w.SetParent(pontos, false); w.position = conv.Ponto(new[] { d.walls[i][0], d.walls[i][1], 0f });
                }
            }
            for (int i = 0; i < d.parked.Count; i++)
            {
                var v = d.parked[i];
                var t = new GameObject($"parked_{i}_{v.kind}").transform; t.SetParent(pontos, false);
                t.position = Assenta(conv.Ponto(v.p));
                var f = conv.Direcao(v.dir[0], v.dir[1]); f.y = 0;
                t.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
                Icone(t.gameObject, v.kind == "viatura" ? 3 : 2);
            }
            var agua = new GameObject("Agua_Poligonos").transform; agua.SetParent(pontos, false);
            foreach (var w in d.water)
            {
                var t = new GameObject("agua_" + w.nome).transform; t.SetParent(agua, false);
                t.position = conv.Ponto(new[] { w.poly[0][0], w.poly[0][1], w.z });
                foreach (var p in w.poly)
                {
                    var c = new GameObject("v").transform; c.SetParent(t, true);
                    c.position = conv.Ponto(new[] { p[0], p[1], w.z });
                }
            }
        }

        // poe o ponto no chao/colisor mais proximo logo abaixo (ate 3 m acima e 6 m abaixo)
        static Vector3 Assenta(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var h, 9f, 1 << LAYER_MAPA, QueryTriggerInteraction.Ignore))
                return h.point + Vector3.up * 0.1f;
            return p + Vector3.up * 0.1f;
        }

        static void Icone(GameObject go, int cor)
        {
            var nomes = new[] { "sv_label_0", "sv_label_1", "sv_label_2", "sv_label_3", "sv_label_4", "sv_label_5", "sv_label_6", "sv_label_7" };
            var ic = EditorGUIUtility.IconContent(nomes[cor]);
            if (ic != null && ic.image) EditorGUIUtility.SetIconForObject(go, (Texture2D)ic.image);
        }

        // ------------------------------------------------------------------
        static void BakeNavMesh(string id, GameObject raiz, Conversor conv = null, Dados d = null)
        {
            var fontes = new List<NavMeshBuildSource>();
            var b = new Bounds(raiz.transform.position, Vector3.one);
            foreach (var mc in raiz.GetComponentsInChildren<MeshCollider>(true)) b.Encapsulate(mc.bounds);
            if (conv != null && d != null)
            {   // limita ao miolo jogavel + 200 m (o entorno de 12 km nao precisa de NavMesh); arena: so o quadrado
                var c = conv.Ponto(new[] { d.centro[0], d.centro[1], 0f });
                float r = d.arena != null ? d.arena[2] + 15f : d.raio + 200f;
                var mini = new Bounds(new Vector3(c.x, b.center.y, c.z), new Vector3(2 * r, b.size.y + 20f, 2 * r));
                b = mini;
            }
            else
            {
                b.size = new Vector3(Mathf.Min(b.size.x, 1600f), b.size.y + 20f, Mathf.Min(b.size.z, 1600f));
            }
            NavMeshBuilder.CollectSources(b, 1 << LAYER_MAPA, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), fontes);
            var cfg = NavMesh.GetSettingsByID(0);
            var data = NavMeshBuilder.BuildNavMeshData(cfg, fontes, b, Vector3.zero, Quaternion.identity);
            if (data == null) { Debug.LogError("[BURITIRO] NavMesh falhou"); return; }
            string path = $"{PASTA}/{id}/NavMesh_{id}.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
            var go = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "NavMesh") ?? new GameObject("NavMesh");
            var tipo = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FreeBala.FBNavData")).FirstOrDefault(t => t != null);
            if (tipo == null) { Debug.LogWarning("[BURITIRO] FreeBala.FBNavData não encontrado; NavMesh salvo em " + path); return; }
            var comp = go.GetComponent(tipo); if (!comp) comp = go.AddComponent(tipo);
            var so = new SerializedObject(comp); so.FindProperty("data").objectReferenceValue = data; so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[BURITIRO] NavMesh {id}: {fontes.Count} colisores, área {b.size.x:0}x{b.size.z:0} m -> {path}");
        }

        // ------------------------------------------------------------------
        // fb_data usa coordenadas "web": x invertido (Coords.ToWeb) e yaw em radianos invertido
        static string Web(Vector3 v) => "[" + F(-v.x) + "," + F(v.y) + "," + F(v.z) + "]";
        static string F(float f) => Math.Round(f, 2).ToString(IC);

        static string ExportaTrecho(string id, Dados d)
        {
            var cena = SceneManager.GetActiveScene();
            var pontos = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Pontos");
            if (!pontos) throw new Exception("cena sem o objeto Pontos");
            List<Transform> Pega(string k) => pontos.transform.Cast<Transform>().Where(t => t.name.StartsWith(k + "_")).OrderBy(t => int.Parse(t.name.Split('_')[1])).ToList();
            string Lst(string k) => "[" + string.Join(",", Pega(k).Select(t => Web(t.position))) + "]";
            var parked = pontos.transform.Cast<Transform>().Where(t => t.name.StartsWith("parked_")).Select(t =>
            {
                float yawUnity = t.eulerAngles.y;
                string kind = t.name.Split('_').Last();
                return "{\"p\":" + Web(t.position) + ",\"yaw\":" + Math.Round(-yawUnity * Mathf.Deg2Rad, 3).ToString(IC) + ",\"kind\":\"" + kind + "\"}";
            });
            var aguaRaiz = pontos.transform.Find("Agua_Poligonos");
            var aguas = new List<string>();
            if (aguaRaiz)
                foreach (Transform w in aguaRaiz)
                {
                    var vs = w.Cast<Transform>().Select(v => v.position).ToList();
                    if (vs.Count < 3) continue;
                    aguas.Add("[" + string.Join(",", vs.Select(v => "[" + F(-v.x) + "," + F(v.z) + "]")) + ",[" + F(vs[0].y) + ",0]]");
                }
            (string nome, string fog) meta = META.TryGetValue(id, out var m) ? m : (id, "{\"color\":\"#b4c6d4\",\"near\":100,\"far\":450}");
            string comuns = $"\"spawnA\":{Lst("spawnA")},\"spawnB\":{Lst("spawnB")},\"bots\":{Lst("bots")},\"points\":{Lst("points")},\"waypoints\":{Lst("waypoints")}";
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(id).Append("\",\"name\":\"").Append(meta.nome).Append("\",\"disabled\":false,");
            sb.Append(comuns).Append(",\"fog\":").Append(meta.fog).Append(",\"level\":{");
            sb.Append("\"id\":\"").Append(id).Append("\",").Append(comuns);
            sb.Append(",\"spawnPolice\":").Append(Lst("spawnPolice")).Append(",\"policeSpawn\":\"B\"");
            sb.Append(",\"alleys\":[],\"shadows\":true,\"fog\":").Append(meta.fog).Append(",\"cell\":64");
            sb.Append(",\"water\":[").Append(string.Join(",", aguas)).Append("]");
            sb.Append(",\"parked\":[").Append(string.Join(",", parked)).Append("]");
            if (d.arena != null)
            {   // arena 10x10: quadrado, paredes, rotas e pares de bases (formato do fb_data, x invertido)
                var c = Pega("arena")[0].position;
                sb.Append(",\"arena\":{\"cx\":").Append(F(-c.x)).Append(",\"cy\":").Append(F(c.z)).Append(",\"half\":").Append(F(d.arena[2])).Append("}");
                sb.Append(",\"walls\":[").Append(string.Join(",", Pega("wall").Select(w => "[" + F(-w.position.x) + "," + F(w.position.z) + "]"))).Append("]");
                List<List<Transform>> Grupos(string k) => pontos.transform.Cast<Transform>().Where(t => t.name.StartsWith(k + "_"))
                    .GroupBy(t => int.Parse(t.name.Split('_')[1])).OrderBy(g => g.Key)
                    .Select(g => g.OrderBy(t => int.Parse(t.name.Split('_')[2])).ToList()).ToList();
                sb.Append(",\"lanes\":[").Append(string.Join(",", Grupos("lane").Select(g => "[" + string.Join(",", g.Select(t => Web(t.position))) + "]"))).Append("]");
                sb.Append(",\"basePairs\":[").Append(string.Join(",", Grupos("basePair").Select(g => "[" + string.Join(",", g.Select(t => Web(t.position))) + "]"))).Append("]");
            }
            sb.Append("}}");
            string path = $"{PASTA}/{id}/{id}_fb_data_trecho.json";
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            return path;
        }

        // troca maps.<id> dentro do fb_data.json (com copia .bak)
        static void AplicaNoFbData(string id, string trecho)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "fb_data.json");
            string txt = File.ReadAllText(path);
            int maps = AchaChave(txt, "maps", 0);
            if (maps < 0) { Debug.LogError("[BURITIRO] fb_data.json sem \"maps\""); return; }
            int abre = txt.IndexOf('{', maps);
            int fechaMaps = Casa(txt, abre);
            int k = AchaChave(txt, id, abre + 1, fechaMaps);
            string novo;
            if (k >= 0)
            {
                int ini = txt.IndexOf('{', k); int fim = Casa(txt, ini);
                novo = txt.Substring(0, ini) + trecho + txt.Substring(fim + 1);
            }
            else novo = txt.Substring(0, abre + 1) + "\"" + id + "\":" + trecho + "," + txt.Substring(abre + 1);
            File.Copy(path, path + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak", true);
            File.WriteAllText(path, novo, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log("[BURITIRO] fb_data.json atualizado (maps." + id + "); cópia .bak ao lado.");
        }

        // posicao do ':' depois de "chave" no nivel de objeto (ignora textos)
        static int AchaChave(string s, string chave, int de, int ate = -1)
        {
            if (ate < 0) ate = s.Length;
            string alvo = "\"" + chave + "\"";
            int nivel = 0; bool str = false;
            for (int i = de; i < ate; i++)
            {
                char c = s[i];
                if (str) { if (c == '\\') i++; else if (c == '"') str = false; continue; }
                if (c == '"')
                {
                    if (nivel == (de == 0 ? 1 : 0) && string.CompareOrdinal(s, i, alvo, 0, alvo.Length) == 0)
                    {
                        int j = i + alvo.Length; while (j < ate && char.IsWhiteSpace(s[j])) j++;
                        if (j < ate && s[j] == ':') return j;
                    }
                    str = true; continue;
                }
                if (c == '{' || c == '[') nivel++; else if (c == '}' || c == ']') nivel--;
            }
            return -1;
        }

        static int Casa(string s, int abre)
        {
            int nivel = 0; bool str = false;
            for (int i = abre; i < s.Length; i++)
            {
                char c = s[i];
                if (str) { if (c == '\\') i++; else if (c == '"') str = false; continue; }
                if (c == '"') str = true;
                else if (c == '{' || c == '[') nivel++;
                else if ((c == '}' || c == ']') && --nivel == 0) return i;
            }
            return -1;
        }

        // ------------------------------------------------------------------
        static void AdicionaNoBuild(string cena)
        {
            var l = EditorBuildSettings.scenes.ToList();
            if (l.Any(s => s.path == cena)) return;
            l.Add(new EditorBuildSettingsScene(cena, true));
            EditorBuildSettings.scenes = l.ToArray();
        }

        static GameObject RaizMapa() => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Map_"));

        static Transform Acha(Transform t, string nome)
        {
            if (t.name == nome) return t;
            foreach (Transform c in t) { var r = Acha(c, nome); if (r) return r; }
            return null;
        }
        static IEnumerable<Transform> Filhos(Transform t, string nome) { var a = Acha(t, nome); if (a) yield return a; }

        // ------------------------------------------------------------------
        // Blender (x leste, y norte, z cima) -> Unity, medido pelos marcadores REF_* que vieram no GLB.
        // Assim nao importa como o glTFast converte os eixos.
        class Conversor
        {
            Vector3 o, e, n, u;
            public static Conversor De(Transform raiz)
            {
                var r = Acha(raiz, "REF");
                if (!r) throw new Exception("GLB sem marcadores REF (gere de novo com prepara_unity.py)");
                Vector3 P(string k) => Acha(r, k).position;
                var c = new Conversor { o = P("REF_ORIGEM") };
                c.e = (P("REF_LESTE") - c.o) / 100f; c.n = (P("REF_NORTE") - c.o) / 100f; c.u = (P("REF_CIMA") - c.o) / 100f;
                Debug.Log($"[BURITIRO] eixos: leste={c.e} norte={c.n} cima={c.u}");
                return c;
            }
            public Vector3 Ponto(float[] p) => o + e * p[0] + n * p[1] + u * (p.Length > 2 ? p[2] : 0f);
            public Vector3 Direcao(float x, float y) => e * x + n * y;
        }

        // ------------------------------------------------------------------
        [Serializable] class Vaga { public float[] p; public float[] dir; public string kind; }
        [Serializable] class Agua { public string nome; public float z; public List<float[]> poly = new List<float[]>(); }
        class Dados
        {
            public float[] centro = { 0, 0 }; public float raio = 330;
            public List<float[]> spawnA, spawnB, spawnPolice, points, bots, waypoints;
            public List<Vaga> parked = new List<Vaga>(); public List<Agua> water = new List<Agua>();
            public float[] arena;                                  // cx, cy, meio lado (Blender) ou null fora do 10x10
            public List<float[]> walls = new List<float[]>();
            public List<List<float[]>> lanes = new List<List<float[]>>(), basePairs = new List<List<float[]>>();
            public float[] lodDist;                                // v4: distancias (m) LOD0->1, LOD1->2 e corte; null = limites antigos
            public float[] lodDistGrandes, lodDistChao;            // v7: grandes com 3 niveis; chao em ladrilhos com 2 niveis (null = como antes)
        }

        static Dados LerDados(string id)
        {
            string path = $"{PASTA}/{id}/{id}_dados.json";
            if (!File.Exists(path)) { Dlg("BURITIRO", "Não achei " + path, "OK"); return null; }
            var j = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(path));
            float Fl(object o) => Convert.ToSingle(o, IC);
            float[] Arr(object o) => ((List<object>)o).Select(Fl).ToArray();
            List<float[]> L(string k) => j.TryGetValue(k, out var v) ? ((List<object>)v).Select(Arr).ToList() : new List<float[]>();
            var d = new Dados
            {
                centro = Arr(j["centro"]), raio = Fl(j["raio"]),
                spawnA = L("spawnA"), spawnB = L("spawnB"), spawnPolice = L("spawnPolice"),
                points = L("points"), bots = L("bots"), waypoints = L("waypoints"),
            };
            if (j.TryGetValue("arena", out var ar) && ar is Dictionary<string, object> ad)
                d.arena = new[] { Fl(ad["cx"]), Fl(ad["cy"]), Fl(ad["half"]) };
            if (j.TryGetValue("walls", out var wl)) d.walls = ((List<object>)wl).Select(Arr).ToList();
            List<List<float[]>> LL(string k) => j.TryGetValue(k, out var v) ? ((List<object>)v).Select(x => ((List<object>)x).Select(Arr).ToList()).ToList() : new List<List<float[]>>();
            d.lanes = LL("lanes"); d.basePairs = LL("basePairs");
            if (j.TryGetValue("lod_dist", out var ld)) d.lodDist = Arr(ld);
            if (j.TryGetValue("lod_dist_grandes", out var lg)) d.lodDistGrandes = Arr(lg);
            if (j.TryGetValue("lod_dist_chao", out var lc)) d.lodDistChao = Arr(lc);
            foreach (Dictionary<string, object> v in (List<object>)j["parked"])
                d.parked.Add(new Vaga { p = Arr(v["p"]), dir = Arr(v["dir"]), kind = (string)v["kind"] });
            foreach (Dictionary<string, object> w in (List<object>)j["water"])
                d.water.Add(new Agua { nome = (string)w["nome"], z = Fl(w["z"]), poly = ((List<object>)w["poly"]).Select(Arr).ToList() });
            return d;
        }

        // leitor JSON minimo (objetos, listas, numeros, textos, true/false/null)
        static class MiniJson
        {
            public static object Parse(string s) { int i = 0; return Val(s, ref i); }
            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            static object Val(string s, ref int i)
            {
                Ws(s, ref i);
                char c = s[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>(); i++;
                    while (true)
                    {
                        Ws(s, ref i); if (s[i] == '}') { i++; return d; }
                        string k = Str(s, ref i); Ws(s, ref i); i++; // ':'
                        d[k] = Val(s, ref i); Ws(s, ref i);
                        if (s[i] == ',') i++;
                    }
                }
                if (c == '[')
                {
                    var l = new List<object>(); i++;
                    while (true)
                    {
                        Ws(s, ref i); if (s[i] == ']') { i++; return l; }
                        l.Add(Val(s, ref i)); Ws(s, ref i);
                        if (s[i] == ',') i++;
                    }
                }
                if (c == '"') return Str(s, ref i);
                if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
                if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
                if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
                int a = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                return double.Parse(s.Substring(a, i - a), IC);
            }
            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder(); i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\')
                    {
                        i++; char e = s[i];
                        if (e == 'u') { sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16)); i += 4; }
                        else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                    }
                    else sb.Append(s[i]);
                    i++;
                }
                i++; return sb.ToString();
            }
        }
    }
}
