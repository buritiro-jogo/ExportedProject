// VISUAL DOS MAPAS na importação (Agente 5 — chamado pelo FBImport.Maps em cada cena de mapa):
//  · MATERIAIS: os materiais glTF do mapa (shader graph do glTFast) viram URP/Lit — mesma textura, mesma cor, mesmo nome (o jogo procura
//    materiais pelo nome: superfícies, grafites, postes, vidro), mais barato (SRP Batcher) e com os recursos do Lit: normal map e AO do glTF
//    quando existem, smoothness/metálico COERENTES POR TIPO (asfalto, concreto, reboco, metal, vidro, água, cerâmica, madeira, plástico…),
//    DETAIL MAPS procedurais (reboco/concreto/asfalto: manchas grandes + grão fino + relevo) que quebram a cor chapada das fachadas, ruas e
//    calçadas sem trocar a cor real (média neutra ×2) e com o ladrilhamento calculado pelos metros por UV de cada material.
//    Vegetação com alpha (folhas, palmeiras, plantas) e troncos vão para o FreeBala/Foliage (vento no vertex shader, duas faces).
//    Ficam como estão: materiais com textura metallicRoughness/rotação de UV/extensões (clearcoat, transmissão) e os de veículo (car*).
//    Os materiais convertidos são salvos num asset por mapa (Art/MapMats/<id>_materiais.mat, os demais como sub-assets) — assim o
//    build leva só as variantes usadas.
//  · REFLEXO: uma Reflection Probe assada (céu de dia + prédios) cobrindo o mapa; o FBWeather apaga à noite.
//  · Luz/ambiente: ambiente por gradiente (Trilight; o FBWeather anima dia/noite) — decisão: sem light probes assadas (mapas grandes e
//    horário dinâmico; GI assada não acompanharia o dia/noite e custaria minutos por mapa).
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala.EditorTools
{
    public static partial class FBMapLook
    {
        const string MatDir = "Assets/FreeBala/Art/MapMats", GfxDir = "Assets/FreeBala/Art/Gfx";

        // ---- tipos de superfície: (regex no nome, smoothness, metálico, detail, força do relevo do detail)
        enum Det { None, Reboco, Concreto, Asfalto }
        static readonly (Regex rx, float sm, float met, Det det, float dn)[] Kinds =
        {
            (R("concrete_pole"), .16f, 0, Det.Concreto, .5f),
            (R("grime|sujeira|desgaste"), .06f, 0, Det.None, 0),                                  // manchas/escorridos (VJ_grime_win): antes caíam na regra de vidro ("_win") → retângulo espelhado sob cada janela
            (R("glass|vidro|_win$|window|janela"), .92f, 0, Det.None, 0),
            (R("water|_rio$|agua|lagoa|brejo|lake"), .88f, 0, Det.None, 0),
            // brilho no padrão do web (sem mapa de ambiente lá: metal/pintura parecem foscos). Só cromado, vidro e água ficam altos.
            (R("ft_generic|ft_var|container|conteiner|contêiner"), .28f, .1f, Det.None, 0),          // contêiner = chapa PINTADA
            (R("rust|ferrug"), .15f, .2f, Det.None, 0),
            (R("chrome|cromo|inox|espelho|mirror"), .6f, .85f, Det.None, 0),
            (R("steel|acero|alum|galv|zinco|metal|chapa|corrugated"), .32f, .45f, Det.None, 0),
            (R("iron|ferro|grade|grill|rail|corrimao|portao|pole|poste|wire|fio|lamp"), .28f, .1f, Det.None, 0),
            (R("asphalt|asfalto|road"), .14f, 0, Det.Asfalto, .6f),
            (R("sidewalk|calcada|paver|pav_|piso|curb|meiofio|concrete_floor|slab|laje|ciclovia"), .2f, 0, Det.Concreto, .7f),
            (R("tile|azulejo|ceramica|granite|marmore"), .45f, 0, Det.None, 0),
            (R("brick|tijolo|bloco|block"), .12f, 0, Det.Concreto, .35f),
            (R("concrete|concreto|conc$|conc_|pillar|marquise|ceiling|stone|pedra"), .16f, 0, Det.Concreto, .65f),
            (R("wall|parede|reboco|muro|paint|pintura|band_|acc_"), .24f, 0, Det.Reboco, .5f),
            (R("roof|telha|fibro|telhado"), .2f, 0, Det.Concreto, .3f),
            (R("wood|madeira|deck|door|porta|tapume"), .22f, 0, Det.None, 0),
            (R("plastic|plastico|cone|tank|caixa|lixeira|barrel"), .35f, 0, Det.None, 0),
            (R("fabric|lona|sandbag|saco|cardboard|paper|dirt|terra|sand|areia|grass|grama|lawn|mud"), .04f, 0, Det.None, 0),
            (R("sign|placa|let_|mural|menu|banner|ad_"), .3f, 0, Det.None, 0),
        };
        static readonly Regex Veg = R("folha|leaf|leaves|petal|plant|flower|washingtonia|palm|canhhoa|la_mat|grass|tuft|bush|arbust");
        static readonly Regex VegAlways = R("folha|leaf|leaves|washingtonia|palm");
        static readonly Regex Trunk = R("tronco|trunk|bark");
        static Regex R(string p) => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void Apply(GameObject map, string id)
        {
            Directory.CreateDirectory(MatDir); Directory.CreateDirectory(GfxDir);
            var det = new Dictionary<Det, (Texture2D a, Texture2D n)> { [Det.Reboco] = DetailTex("reboco"), [Det.Concreto] = DetailTex("concreto"), [Det.Asfalto] = DetailTex("asfalto") };
            var path = $"{MatDir}/{id}_materiais.mat"; AssetDatabase.DeleteAsset(path);
            Material container = null; var conv = new Dictionary<Material, Material>(); int nConv = 0, nKeep = 0, nVeg = 0;
            var rends = map.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer).ToList();
            foreach (var r in rends)
            {
                var ms = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < ms.Length; i++)
                {
                    var m = ms[i]; if (!m) continue;
                    if (!conv.TryGetValue(m, out var nm))
                    {
                        nm = Convert(m, rends, det, out bool veg);
                        conv[m] = nm;
                        if (nm != m)
                        {
                            nConv++; if (veg) nVeg++;
                            if (!container) { container = new Material(FBShaders.Lit) { name = id + "_materiais" }; AssetDatabase.CreateAsset(container, path); }   // asset principal só agrupa (o nome dele vira o do arquivo)
                            AssetDatabase.AddObjectToAsset(nm, container);
                        }
                        else nKeep++;
                    }
                    if (nm != m) { ms[i] = nm; ch = true; }
                }
                if (ch) r.sharedMaterials = ms;
            }
            if (container) { EditorUtility.SetDirty(container); AssetDatabase.SaveAssets(); }
            VegBake(map, id, conv.Values.Distinct().ToList());                                 // vegetação instanciada (FBVegBake.cs)
            ReflectionProbe(map, id);
            Debug.Log($"[FreeBala] visual do mapa {id}: {nConv} materiais convertidos para URP/Lit ({nVeg} vegetação com vento), {nKeep} mantidos");
        }

        // ---- glTF (glTFast) -> URP/Lit
        static Material Convert(Material m, List<Renderer> rends, Dictionary<Det, (Texture2D a, Texture2D n)> det, out bool veg)
        {
            veg = false; var sn = m.shader ? m.shader.name : "";
            if (!sn.EndsWith("glTF-pbrMetallicRoughness") || m.name.StartsWith("car")) return m;
            if (m.HasProperty("metallicRoughnessTexture") && m.GetTexture("metallicRoughnessTexture")) return m;
            if (m.HasProperty("baseColorTexture_Rotation") && ((Vector4)m.GetVector("baseColorTexture_Rotation")).sqrMagnitude > 1e-8f) return m;
            string n = m.name; bool transp = m.renderQueue >= 3000 || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
            bool clip = !transp && (m.IsKeywordEnabled("_ALPHATEST_ON") || m.renderQueue >= 2450);
            veg = clip && Veg.IsMatch(n) || VegAlways.IsMatch(n) && !transp; bool trunk = !transp && !clip && Trunk.IsMatch(n);
            var o = new Material(veg || trunk ? FBShaders.Foliage : FBShaders.Lit) { name = n };
            // cor e texturas (a transformação de UV do glTF já está no _ST)
            var bt = m.HasProperty("baseColorTexture") ? m.GetTexture("baseColorTexture") : null;
            if (bt) { o.SetTexture("_BaseMap", bt); o.SetTextureScale("_BaseMap", m.GetTextureScale("baseColorTexture")); o.SetTextureOffset("_BaseMap", m.GetTextureOffset("baseColorTexture")); }
            o.SetColor("_BaseColor", m.HasProperty("baseColorFactor") ? m.GetColor("baseColorFactor") : Color.white);
            var nt = m.HasProperty("normalTexture") ? m.GetTexture("normalTexture") : null;
            if (nt) { o.SetTexture("_BumpMap", nt); o.SetFloat("_BumpScale", m.HasProperty("normalTexture_scale") ? m.GetFloat("normalTexture_scale") : 1); o.EnableKeyword("_NORMALMAP"); }
            var ot = m.HasProperty("occlusionTexture") ? m.GetTexture("occlusionTexture") : null;   // AO do glTF é no R; o Lit lê o G (texturas cinza: iguais)
            if (ot) { o.SetTexture("_OcclusionMap", ot); o.SetFloat("_OcclusionStrength", m.HasProperty("occlusionTexture_strength") ? m.GetFloat("occlusionTexture_strength") : 1); o.EnableKeyword("_OCCLUSIONMAP"); }
            var et = m.HasProperty("emissiveTexture") ? m.GetTexture("emissiveTexture") : null; var ec = m.HasProperty("emissiveFactor") ? m.GetColor("emissiveFactor") : Color.black;
            if (et || ec.maxColorComponent > .002f)
            {
                if (et) o.SetTexture("_EmissionMap", et); o.SetColor("_EmissionColor", et && ec.maxColorComponent <= .002f ? Color.white : ec);
                o.EnableKeyword("_EMISSION"); o.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            // tipo de superfície
            float rough = m.HasProperty("roughnessFactor") ? m.GetFloat("roughnessFactor") : .9f, metal = m.HasProperty("metallicFactor") ? m.GetFloat("metallicFactor") : 0;
            float sm = Mathf.Clamp(1 - rough, .05f, .3f), met = Mathf.Min(metal, .2f); Det dk = Det.None; float dn = 0;   // sem regra: fosco (como o web)
            if (veg) { sm = .16f; met = 0; }
            else if (trunk) { sm = .08f; met = 0; }
            else foreach (var k in Kinds) if (k.rx.IsMatch(n)) { sm = k.sm; met = k.met; dk = k.det; dn = k.dn; break; }   // a regra manda (o metallicFactor do glTF vinha 1 em muita pintura)
            o.SetFloat("_Smoothness", sm); o.SetFloat("_Metallic", met); o.SetFloat("_WorkflowMode", 1);
            // superfície/alfa/faces
            float cull = m.HasProperty("_Cull") ? m.GetFloat("_Cull") : m.HasProperty("_CullMode") ? m.GetFloat("_CullMode") : 2;
            o.SetFloat("_Cull", veg ? 0 : cull);
            if (transp) FBShaders.TransparentInPlace(o);
            else if (clip || veg)
            {
                o.SetFloat("_AlphaClip", 1); o.EnableKeyword("_ALPHATEST_ON"); o.SetFloat("_Cutoff", Mathf.Max(.5f, m.HasProperty("alphaCutoff") ? m.GetFloat("alphaCutoff") : .5f)); o.SetFloat("_AlphaToMask", 1); FixAlphaTex(bt);   // sem halo nas folhas
                o.SetOverrideTag("RenderType", "TransparentCutout"); o.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else { o.SetOverrideTag("RenderType", "Opaque"); o.renderQueue = (int)RenderQueue.Geometry; }
            // detail map (fachadas, ruas, calçadas, muros), ladrilho de ~3 m (asfalto 4 m) no mundo
            if (dk != Det.None && !transp)
            {
                float mpu = MetersPerUV(m, rends, dk == Det.Asfalto ? 8 : dk == Det.Reboco ? 4 : 3.2f), tile = dk == Det.Asfalto ? 4f : 3f;
                var (da, dnm) = det[dk]; var st = o.GetTextureScale("_BaseMap"); float t = mpu / tile / Mathf.Max(.01f, Mathf.Abs(st.x));
                o.SetTexture("_DetailAlbedoMap", da); o.SetTexture("_DetailNormalMap", dnm); o.SetTextureScale("_DetailAlbedoMap", new Vector2(t, t));
                o.SetFloat("_DetailAlbedoMapScale", 1); o.SetFloat("_DetailNormalMapScale", dn); o.EnableKeyword("_DETAIL_MULX2");
            }
            return o;
        }

        // metros de mundo por unidade de UV do material (mediana de arestas de triângulo; os mapas usam UV em escala de mundo)
        static float MetersPerUV(Material m, List<Renderer> rends, float fallback)
        {
            var ratios = new List<float>();
            try
            {
                foreach (var r in rends)
                {
                    if (ratios.Count > 400) break; var ms = r.sharedMaterials; var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                    for (int s = 0; s < ms.Length && s < mf.sharedMesh.subMeshCount; s++)
                    {
                        if (ms[s] != m) continue; var mesh = mf.sharedMesh; var v = mesh.vertices; var uv = mesh.uv; if (uv == null || uv.Length != v.Length) continue;
                        var tri = mesh.GetTriangles(s); var sc = r.transform.lossyScale; float k = (Mathf.Abs(sc.x) + Mathf.Abs(sc.y) + Mathf.Abs(sc.z)) / 3;
                        for (int i = 0; i + 2 < tri.Length && i < 1800; i += 3)
                        {
                            float dp = (v[tri[i + 1]] - v[tri[i]]).magnitude * k, du = (uv[tri[i + 1]] - uv[tri[i]]).magnitude;
                            if (du > 1e-4f && dp > .05f) ratios.Add(dp / du);
                        }
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[FreeBala] metros por UV de " + m.name + ": " + e.Message); }
            if (ratios.Count < 3) return fallback; ratios.Sort(); return Mathf.Clamp(ratios[ratios.Count / 2], .3f, 40);
        }

        // ---- detail maps procedurais (tileáveis, 256²): albedo cinza com média ~0,5 (neutro no ×2 do URP) + normal do mesmo relevo
        static (Texture2D a, Texture2D n) DetailTex(string kind)
        {
            string pa = $"{GfxDir}/det_{kind}.png", pn = $"{GfxDir}/det_{kind}_n.png";
            if (!File.Exists(pa) || !File.Exists(pn))
            {
                const int S = 256; var h = new float[S * S]; var alb = new float[S * S]; var rnd = new System.Random(kind.GetHashCode());
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                    {
                        float u = x / (float)S, v = y / (float)S;
                        float blot = Fbm(u, v, 2, 3) - .5f, mid = Fbm(u, v, 8, 3) - .5f, fine = Fbm(u, v, 64, 2) - .5f, grain = Fbm(u, v, 128, 1) - .5f;
                        float a, ht;
                        if (kind == "asfalto")
                        {
                            float spec = Hash(x, y, 7) > .93f ? .5f : 0;                               // brita clara
                            a = .5f + blot * .12f + mid * .06f + grain * .2f + spec * .25f; ht = grain * .8f + fine * .5f + spec;
                        }
                        else if (kind == "concreto")
                        {
                            float pore = Hash(x, y, 3) > .965f ? -.6f : 0;                             // poros
                            a = .5f + blot * .14f + mid * .06f + fine * .07f + pore * .12f; ht = fine * .7f + grain * .3f + pore;
                        }
                        else                                                                              // reboco: manchas + escorrido vertical
                        {
                            float streak = Fbm(u, v, 1, 2, 16) - .5f;
                            a = .5f + blot * .14f + mid * .05f + fine * .045f + Mathf.Min(0, streak) * .12f; ht = fine * .8f + grain * .5f + mid * .3f;
                        }
                        alb[y * S + x] = Mathf.Clamp01(a); h[y * S + x] = ht;
                    }
                // centraliza a média em 0,5 (não muda a cor do material)
                float mean = alb.Average(); for (int i = 0; i < alb.Length; i++) alb[i] = Mathf.Clamp01(alb[i] - mean + .5f);
                var ta = new Texture2D(S, S, TextureFormat.RGB24, false); var tn = new Texture2D(S, S, TextureFormat.RGB24, false);
                for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                    {
                        float g = alb[y * S + x]; ta.SetPixel(x, y, new Color(g, g, g));
                        float dx = h[y * S + (x + 1) % S] - h[y * S + (x + S - 1) % S], dy = h[((y + 1) % S) * S + x] - h[((y + S - 1) % S) * S + x];
                        var nv = new Vector3(-dx * 2.2f, -dy * 2.2f, 1).normalized; tn.SetPixel(x, y, new Color(nv.x * .5f + .5f, nv.y * .5f + .5f, nv.z * .5f + .5f));
                    }
                File.WriteAllBytes(pa, ta.EncodeToPNG()); File.WriteAllBytes(pn, tn.EncodeToPNG()); Object.DestroyImmediate(ta); Object.DestroyImmediate(tn);
                AssetDatabase.ImportAsset(pa, ImportAssetOptions.ForceSynchronousImport); AssetDatabase.ImportAsset(pn, ImportAssetOptions.ForceSynchronousImport);
            }
            foreach (var (p, normal) in new[] { (pa, false), (pn, true) })
            {
                var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (!ti) continue;
                bool dirty = ti.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) || ti.wrapMode != TextureWrapMode.Repeat;
                if (dirty) { ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.anisoLevel = 4; ti.sRGBTexture = !normal; ti.SaveAndReimport(); }
            }
            return (AssetDatabase.LoadAssetAtPath<Texture2D>(pa), AssetDatabase.LoadAssetAtPath<Texture2D>(pn));
        }
        static float Hash(int x, int y, int s) { uint h = (uint)(x * 374761393 + y * 668265263 + s * 2246822519u); h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xffffff) / 16777215f; }
        // fbm de valor PERIÓDICO em [0,1)²: per = células por ladrilho; perY opcional (esticado = escorrido vertical)
        static float Fbm(float u, float v, int per, int oct, int perY = 0)
        {
            float s = 0, a = .5f, n = 0; int px = per, py = perY > 0 ? perY : per;
            for (int o = 0; o < oct; o++) { s += a * PN(u * px, v * py, px, py, o); n += a; a *= .5f; px *= 2; py *= 2; }
            return s / n;
        }
        static float PN(float x, float y, int px, int py, int seed)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y); float fx = x - ix, fy = y - iy; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float L(int a, int b) => Hash(((ix + a) % px + px) % px, ((iy + b) % py + py) % py, seed * 31 + px);
            return Mathf.Lerp(Mathf.Lerp(L(0, 0), L(1, 0), fx), Mathf.Lerp(L(0, 1), L(1, 1), fx), fy);
        }

        // ---- reflection probe assada cobrindo o mapa (céu de dia + prédios); o FBWeather escurece à noite/chuva
        static void ReflectionProbe(GameObject map, string id)
        {
            var rs = map.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var skyMat = SkyMaterial(); if (skyMat) { RenderSettings.skybox = skyMat; RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox; }
            var go = new GameObject("Reflexo"); var p = go.AddComponent<ReflectionProbe>();
            float ground = b.min.y; foreach (var r in rs) if (r.bounds.size.x > 40 && r.bounds.size.z > 40) { ground = r.bounds.max.y; break; }
            go.transform.position = new Vector3(b.center.x, Mathf.Min(ground + 6, b.max.y), b.center.z);
            p.mode = ReflectionProbeMode.Baked; p.size = new Vector3(Mathf.Min(b.size.x, 900) + 20, Mathf.Max(60, b.size.y + 20), Mathf.Min(b.size.z, 900) + 20);
            p.intensity = .7f; p.boxProjection = false; p.resolution = 128;   // reflexo contido (o web não tem mapa de ambiente)
            p.hdr = true; p.importance = 1; p.blendDistance = 2; p.farClipPlane = 900; p.cullingMask = 1 << FBProjectSetup.LMap;
            var path = $"{FBImport.SceneDir}/Map_{id}_Reflexo.exr";
            try { if (!Lightmapping.BakeReflectionProbe(p, path)) Debug.LogWarning("[FreeBala] reflexo do mapa " + id + " não assou"); else p.bakedTexture = AssetDatabase.LoadAssetAtPath<Texture>(path); }
            catch (System.Exception e) { Debug.LogWarning("[FreeBala] reflexo do mapa " + id + ": " + e.Message); Object.DestroyImmediate(go); }
        }
        // céu salvo como asset (para o editor/assar o reflexo; no jogo o FBLivingSky cria o seu)
        public static Material SkyMaterial()
        {
            var sh = FBShaders.Sky; if (!sh) return null; var path = $"{GfxDir}/ceu_editor.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh) { name = "ceu_editor" }; AssetDatabase.CreateAsset(m, path); }
            var s = Quaternion.Euler(48, -35, 0) * Vector3.back; m.SetVector("_SunDir", new Vector4(-s.x, s.y, s.z, 0)); m.SetFloat("_Night", 0); m.SetFloat("_Cover", .45f);
            EditorUtility.SetDirty(m); return m;
        }
    }
}
