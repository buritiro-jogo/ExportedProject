// Decals do cenário (sangue, buracos de bala, queimado de explosão) com pool fixo e reciclagem dos mais antigos.
// Dois caminhos, escolhidos UMA vez no Warm:
//  • PC: URP Decal Projector (material-modelo Resources/FX/DecalTemplate = "Shader Graphs/Decal") — projeta na geometria de verdade
//    (dobra quina, segue degrau/meio-fio). Só liga se o renderer ativo tiver o DecalRendererFeature (pedido ao agente de gráficos).
//  • celular/WebGL (ou PC sem o feature): quadrado orientado pela normal, afastado alguns mm, com teste dos 4 cantos na superfície
//    (encolhe ou desiste em vez de ficar flutuando para fora de quina/beirada) e ordem de desenho crescente (o mais novo por cima).
// Atlas: blood_splats (4×4: 0–5 respingo redondo, 6–9 direcional, 10–11 escorrido, 12–13 poça, 14–15 gotas), bullet_holes (4×2:
// concreto, concreto, metal, madeira, vidro, queimado, terra, reboco). Sangue escurece com o tempo (coagula) e perde o brilho molhado.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
    public static class FBDecalFx
    {
        public enum Atlas { Blood, Holes }
        public const int CellRound = 0, CellDir = 6, CellDrip = 10, CellPool = 12, CellDrops = 14;          // blood_splats
        public const int HoleConcrete = 0, HoleMetal = 2, HoleWood = 3, HoleGlass = 4, Scorch = 5, HoleDirt = 6, HolePlaster = 7;  // bullet_holes

        class Slot
        {
            public GameObject go; public MeshRenderer mr; public DecalProjector proj; public Transform tr;
            public bool on; public Atlas atlas; public float born, life, fade = 1, size, grow0, grow1, growT, growDur;
            public Vector2 aspect = Vector2.one; public int seq; public bool blood; public Vector4 st;
        }
        static Slot[] slots; static int seq; static bool ready;
        public static bool Projector { get; private set; }
        static Mesh quad; static Material matBlood, matHoles, projBloodFresh, projBloodDry, projHoles;
        static MaterialPropertyBlock mpb;
        static readonly int idBaseColor = Shader.PropertyToID("_BaseColor"), idSmooth = Shader.PropertyToID("_Smoothness"), idST = Shader.PropertyToID("_BaseMap_ST");

        public static int Capacity => slots?.Length ?? 0;
        public static int ActiveCount { get { int n = 0; if (slots != null) foreach (var s in slots) if (s.on) n++; return n; } }
        public static int SlotObjects { get { int n = 0; if (slots != null) foreach (var s in slots) if (s.go) n++; return n; } }

        public static void Warm()
        {
            if (ready) return; ready = true; FBFxQuality.Ensure();
            mpb = new MaterialPropertyBlock(); quad = FBFxLib.Quad("DecalQuad");
            matBlood = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("blood_splats"), Color.white, "DecalSangue"); Wet(matBlood, .55f);
            matHoles = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("bullet_holes"), Color.white, "DecalBuraco"); Wet(matHoles, .15f);
            Projector = FBFxQuality.UseProjector && ProjectorSupported();
            if (Projector)
            {
                var tpl = Resources.Load<Material>("FX/DecalTemplate");
                projBloodFresh = new Material(tpl) { name = "ProjSangue" }; projBloodFresh.SetTexture("Base_Map", FBFxLib.Tex("blood_splats"));
                projBloodDry = new Material(tpl) { name = "ProjSangueSeco" }; projBloodDry.SetTexture("Base_Map", FBFxLib.Tex("blood_splats_dry"));
                projHoles = new Material(tpl) { name = "ProjBuraco" }; projHoles.SetTexture("Base_Map", FBFxLib.Tex("bullet_holes"));
            }
            slots = new Slot[FBFxQuality.PoolDecals];
            for (int i = 0; i < slots.Length; i++) slots[i] = Make(i);
        }
        static void Wet(Material m, float smooth) { if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0); }

        // o renderer do URP em uso tem o DecalRendererFeature ativo? (sem ele o projetor não desenha nada)
        static bool ProjectorSupported()
        {
            try
            {
                var tpl = Resources.Load<Material>("FX/DecalTemplate"); if (!tpl || !tpl.shader || !tpl.shader.isSupported) return false;
                var a = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset; if (!a) return false;
                foreach (var rd in a.rendererDataList) if (rd) foreach (var f in rd.rendererFeatures) if (f is DecalRendererFeature && f.isActive) return true;
            }
            catch (System.Exception e) { Debug.LogWarning("[FreeBala] decal URP indisponível: " + e.Message); }
            return false;
        }

        static Slot Make(int i)
        {
            var s = new Slot(); var g = new GameObject("Decal" + i); g.transform.SetParent(FBFxLib.Root, false); s.go = g; s.tr = g.transform;
            if (Projector)
            {
                s.proj = g.AddComponent<DecalProjector>(); s.proj.material = projBloodFresh; s.proj.drawDistance = 60; s.proj.fadeScale = .9f;
                s.proj.startAngleFade = 70; s.proj.endAngleFade = 88; s.proj.scaleMode = DecalScaleMode.ScaleInvariant; s.proj.enabled = false;
            }
            else
            {
                g.AddComponent<MeshFilter>().sharedMesh = quad; s.mr = g.AddComponent<MeshRenderer>(); s.mr.sharedMaterial = matBlood;
                s.mr.shadowCastingMode = ShadowCastingMode.Off; s.mr.receiveShadows = true; s.mr.lightProbeUsage = LightProbeUsage.Off; s.mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                s.mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion; s.mr.enabled = false;
            }
            g.SetActive(false); return s;
        }

        // vaga: livre → senão a mais antiga (reciclagem). Respeita o limite do nível de qualidade (MaxDecals ≤ capacidade do pool).
        static Slot Take()
        {
            Slot best = null; int used = 0;
            foreach (var s in slots) { if (s.on) used++; else if (best == null) best = s; }
            if (best == null || used >= FBFxQuality.MaxDecals) { best = null; foreach (var s in slots) if (s.on && (best == null || s.seq < best.seq)) best = s; }
            int idx = System.Array.IndexOf(slots, best);
            if (!best.go) slots[idx] = best = Make(idx);                // pai (veículo/porta) destruído levou o objeto junto: recria a vaga
            return best;
        }

        /// <summary>Coloca um decal. p/n = ponto e normal da superfície; up = direção do "alto" da arte no plano (escorrido para baixo,
        /// direcional para +U); size em metros; aspect alonga em U (respingo rasante). parent: colisor móvel (veículo) — o decal acompanha.</summary>
        public static Transform Place(Atlas atlas, int cell, Vector3 p, Vector3 n, float size, Vector3? up = null, float aspect = 1, Transform parent = null, float life = -1, bool checkEdges = true)
        {
            if (parent && parent.GetComponentInParent<FBCharacter>()) return null;   // nunca grudar decal em personagem
            if (!ready) Warm(); if (n.sqrMagnitude < 1e-6f) return null; n.Normalize();
            if (atlas == Atlas.Blood && !FBFx.BloodOn) return null;
            FBFxLib.Tangents(n, out var t0, out _);
            var u = up ?? Quaternion.AngleAxis(Random.Range(0, 360f), n) * t0; u = Vector3.ProjectOnPlane(u, n); if (u.sqrMagnitude < 1e-6f) u = t0; u.Normalize();
            float w = size * aspect, h = size;
            if (!Projector && checkEdges)
            {   // quadrado: os 4 cantos precisam estar na mesma superfície (senão encolhe; na segunda falha desiste)
                var right = Vector3.Cross(n, u);
                for (int k = 0; ; k++)
                {
                    if (CornersOn(p, n, right, u, w * .42f, h * .42f)) break;
                    if (k == 1) return null; w *= .55f; h *= .55f;
                }
            }
            var s = Take(); s.on = true; s.atlas = atlas; s.blood = atlas == Atlas.Blood; s.born = Time.time; s.life = life > 0 ? life : FBFxQuality.DecalLife * Random.Range(.85f, 1.15f);
            s.fade = 1; s.size = h; s.aspect = new Vector2(w / h, 1); s.growDur = 0; s.seq = ++seq;
            int cols = atlas == Atlas.Blood ? 4 : 4, rows = atlas == Atlas.Blood ? 4 : 2; int cx = cell % cols, cy = cell / cols;
            s.st = new Vector4(1f / cols, 1f / rows, cx / (float)cols, 1 - (cy + 1f) / rows);     // linha 0 do atlas = topo da imagem
            s.go.SetActive(true);
            if (parent) s.tr.SetParent(parent, true); else if (s.tr.parent != FBFxLib.Root) s.tr.SetParent(FBFxLib.Root, true);
            var rot = Quaternion.LookRotation(-n, u);
            if (Projector)
            {
                s.tr.SetPositionAndRotation(p + n * .05f, rot);
                s.proj.material = s.blood ? projBloodFresh : projHoles; s.proj.uvScale = new Vector2(s.st.x, s.st.y); s.proj.uvBias = new Vector2(s.st.z, s.st.w);
                s.proj.size = new Vector3(w, h, .2f); s.proj.pivot = new Vector3(0, 0, .05f); s.proj.fadeFactor = 1; s.proj.enabled = true;
            }
            else
            {
                float lift = .004f + (s.seq % 16) * .0004f;                // afasta da parede; decals sobrepostos não brigam no z
                s.tr.SetPositionAndRotation(p + n * lift, rot); SetScale(s, w, h); s.mr.sharedMaterial = s.blood ? matBlood : matHoles;
                s.mr.sortingOrder = s.seq % 30000; s.mr.enabled = true; Props(s, 0);
            }
            return s.tr;
        }
        static void SetScale(Slot s, float w, float h)
        {
            var ls = s.tr.parent ? s.tr.parent.lossyScale : Vector3.one; float k = Mathf.Max(1e-4f, Mathf.Abs(ls.x));
            s.tr.localScale = new Vector3(w / k, h / k, 1 / k);
        }
        static bool CornersOn(Vector3 p, Vector3 n, Vector3 r, Vector3 u, float hw, float hh)
        {
            for (int i = 0; i < 4; i++)
            {
                var c = p + r * (i % 2 == 0 ? -hw : hw) + u * (i < 2 ? -hh : hh);
                if (!Physics.Raycast(c + n * .06f, -n, out var hit, .14f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return false;
                if (Vector3.Dot(hit.normal, n) < .9f || Mathf.Abs(Vector3.Dot(hit.point - p, n)) > .025f) return false;
            }
            return true;
        }

        /// <summary>poça que cresce (corpo caído): começa pequena e espalha devagar.</summary>
        public static Transform Pool(Vector3 p, float to, float seconds = 5)
        {
            var t = Place(Atlas.Blood, CellPool + Random.Range(0, 2), p, Vector3.up, to, null, Random.Range(.9f, 1.15f), null, FBFxQuality.DecalLife * 1.5f, false);
            if (!t) return null; var s = Find(t); s.grow0 = to * .15f; s.grow1 = to; s.growT = 0; s.growDur = seconds; Size(s, s.grow0); return t;
        }
        static Slot Find(Transform t) { foreach (var s in slots) if (s.tr == t) return s; return null; }
        static void Size(Slot s, float h)
        {
            float w = h * s.aspect.x; s.size = h;
            if (Projector) s.proj.size = new Vector3(w, h, s.proj.size.z); else SetScale(s, w, h);
        }

        // coagulação: vermelho vivo e molhado → vinho-marrom fosco em ~40 s (quad); o projetor troca para o atlas seco
        static void Props(Slot s, float age)
        {
            float a = s.fade;
            if (s.blood)
            {
                float dry = Mathf.Clamp01(age / 40f); var c = Color.Lerp(Color.white, new Color(.55f, .42f, .38f), dry * dry * (3 - 2 * dry)); c.a = a;
                mpb.SetColor(idBaseColor, c); mpb.SetFloat(idSmooth, Mathf.Lerp(.62f, .18f, dry));
            }
            else { mpb.SetColor(idBaseColor, new Color(1, 1, 1, a)); mpb.SetFloat(idSmooth, .12f); }
            mpb.SetVector(idST, s.st); s.mr.SetPropertyBlock(mpb);
        }

        static float propT;
        public static void Tick(float dt)
        {
            if (!ready) return; bool props = (propT -= dt) <= 0; if (props) propT = .5f;
            float now = Time.time;
            foreach (var s in slots)
            {
                if (!s.on) continue;
                if (!s.go) { s.on = false; continue; }                                   // pai destruído
                if (s.growDur > 0) { s.growT += dt; float k = Mathf.Clamp01(s.growT / s.growDur); Size(s, Mathf.Lerp(s.grow0, s.grow1, 1 - (1 - k) * (1 - k))); if (k >= 1) s.growDur = 0; }
                float age = now - s.born;
                if (age > s.life) { s.fade -= dt / 2.5f; if (s.fade <= 0) { Free(s); continue; } props = true; }
                if (Projector) { s.proj.fadeFactor = s.fade; if (s.blood && age > 30 && s.proj.material != projBloodDry) s.proj.material = projBloodDry; }
                else if (props || s.growDur > 0) Props(s, age);
            }
        }
        static void Free(Slot s)
        {
            s.on = false; if (!s.go) return;
            if (s.proj) s.proj.enabled = false; if (s.mr) s.mr.enabled = false;
            if (s.tr.parent != FBFxLib.Root) s.tr.SetParent(FBFxLib.Root, false); s.go.SetActive(false);
        }
        public static void Clear() { if (slots != null) foreach (var s in slots) if (s.on) Free(s); }

        // para testes: normal e ancoragem do decal colocado
        public static Vector3 FacingOf(Transform t) => t ? -t.forward : Vector3.zero;
    }
}
