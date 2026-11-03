// Base comum dos efeitos de combate: texturas de Resources/FX (com reserva procedural se faltarem), materiais clonados dos
// materiais-modelo de Resources/FBMats (o build só leva as variantes de shader usadas por algum material salvo — material criado do
// zero com shader trocado em runtime saía opaco no executável), construtor de ParticleSystem com pool fixo (maxParticles) e Emit.
// Regras anti-travada: tudo é criado no Warm do carregamento; na partida só se emite partícula / reposiciona objetos já existentes.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public static class FBFxLib
    {
        public static Transform Root;
        public static readonly List<ParticleSystem> Systems = new();

        // ---- texturas
        static readonly Dictionary<string, Texture2D> tex = new();
        public static Texture2D Tex(string name)
        {
            if (tex.TryGetValue(name, out var t) && t) return t;
            t = Resources.Load<Texture2D>("FX/" + name) ?? Fallback(name); tex[name] = t; return t;
        }
        // reserva (asset ausente): círculo suave branco — o efeito continua funcionando, só mais simples
        static Texture2D Fallback(string name)
        {
            const int n = 32; var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name + "_reserva", wrapMode = TextureWrapMode.Clamp }; var px = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float dx = (x + .5f) / n * 2 - 1, dy = (y + .5f) / n * 2 - 1; px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy))); }
            t.SetPixels(px); t.Apply(true, true); return t;
        }

        // ---- materiais a partir dos modelos (FBMats/Particles_Alpha, Particles_Add, Lit_Alpha, Unlit_Alpha)
        public static Material Mat(string template, Texture tx, Color tint, string name)
        {
            var tpl = Resources.Load<Material>("FBMats/" + template);
            Material m;
            if (tpl) m = new Material(tpl);
            else
            {   // projeto sem os modelos (worktree sem import): mesmo caminho do FBShaders.Transparent
                bool add = template.EndsWith("_Add"); var sh = template.StartsWith("Lit") ? FBShaders.Lit : template.StartsWith("Unlit") ? FBShaders.Unlit : FBShaders.ParticlesUnlit;
                m = FBShaders.Transparent(new Material(sh), add);
            }
            m.name = name; if (tx) { m.mainTexture = tx; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tx); }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); m.color = tint;
            return m;
        }

        // ---- ParticleSystem com pool fixo: emissão desligada (só Emit manual), espaço do mundo, sem sombra
        public class Opt
        {
            public float gravity, drag;              // drag: amortecimento (LimitVelocity dampen) — fumaça/poeira desaceleram
            public ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard;
            public float stretch;                    // Stretch: comprimento pela velocidade
            public AnimationCurve size;              // tamanho pela vida (multiplicador)
            public Gradient color;                   // cor/alfa pela vida
            public int tilesX = 1, tilesY = 1, row = -1;   // atlas: row >= 0 = só essa linha; quadro sorteado por partícula
            public bool spin;                        // rotação aleatória ao longo da vida
            public Mesh mesh; public bool size3D;
            public bool collide; public float bounce = .3f, dampen = .4f;
            public int sortFudge;                    // ordem entre sistemas transparentes (maior = desenha antes)
        }
        public static AnimationCurve Curve(params float[] kv) { var c = new AnimationCurve(); for (int i = 0; i + 1 < kv.Length; i += 2) c.AddKey(kv[i], kv[i + 1]); return c; }
        public static Gradient Fade(float inT = 0, float outT = 1, Color? end = null)
        {
            var g = new Gradient(); var e = end ?? Color.white;
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(e, 1) },
                inT > 0 ? new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, inT), new GradientAlphaKey(1, Mathf.Max(inT, outT)), new GradientAlphaKey(0, 1) }
                        : new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, outT), new GradientAlphaKey(0, 1) });
            return g;
        }

        public static ParticleSystem PS(string n, Material m, int max, Opt o = null)
        {
            o ??= new Opt();
            var g = new GameObject(n); g.transform.SetParent(Root, false); var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.maxParticles = Mathf.Max(1, max); main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = o.gravity;
            main.startSpeed = 0; main.loop = true; main.duration = 1; main.scalingMode = ParticleSystemScalingMode.Shape; main.startSize3D = o.size3D; main.startRotation3D = o.mesh != null;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            if (o.color != null) { var col = ps.colorOverLifetime; col.enabled = true; col.color = o.color; }
            if (o.size != null) { var so = ps.sizeOverLifetime; so.enabled = true; so.size = new ParticleSystem.MinMaxCurve(1, o.size); }
            if (o.drag > 0) { var lv = ps.limitVelocityOverLifetime; lv.enabled = true; lv.limit = 0; lv.dampen = o.drag; lv.drag = 0; }
            if (o.spin) { var rl = ps.rotationOverLifetime; rl.enabled = true; rl.z = new ParticleSystem.MinMaxCurve(-3f, 3f); }
            if (o.tilesX * o.tilesY > 1)
            {
                var ts = ps.textureSheetAnimation; ts.enabled = true; ts.mode = ParticleSystemAnimationMode.Grid; ts.numTilesX = o.tilesX; ts.numTilesY = o.tilesY;
                if (o.row >= 0) { ts.animation = ParticleSystemAnimationType.SingleRow; ts.rowMode = ParticleSystemAnimationRowMode.Custom; ts.rowIndex = o.row; }
                else ts.animation = ParticleSystemAnimationType.WholeSheet;
                ts.frameOverTime = new ParticleSystem.MinMaxCurve(0, .999f); ts.cycleCount = 1;            // quadro sorteado e fixo na vida
            }
            if (o.collide)
            {
                var c = ps.collision; c.enabled = true; c.type = ParticleSystemCollisionType.World; c.mode = ParticleSystemCollisionMode.Collision3D;
                c.collidesWith = FBWorld.SolidMask; c.bounce = o.bounce; c.dampen = o.dampen; c.lifetimeLoss = 0; c.quality = ParticleSystemCollisionQuality.Medium;
                c.maxCollisionShapes = 32; c.enableDynamicColliders = false; c.radiusScale = .5f;
            }
            var r = g.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = m; r.renderMode = o.mode; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off; r.sortingFudge = o.sortFudge; r.minParticleSize = 0; r.maxParticleSize = 2;
            if (o.mode == ParticleSystemRenderMode.Stretch) { r.velocityScale = o.stretch > 0 ? o.stretch : .03f; r.lengthScale = 1.2f; }
            if (o.mode == ParticleSystemRenderMode.Mesh)
            {   // partícula de malha exige Read/Write: malha ilegível vira billboard (aviso uma vez) em vez de erro em toda partida
                if (o.mesh && o.mesh.isReadable) { r.mesh = o.mesh; r.alignment = ParticleSystemRenderSpace.World; }
                else { Debug.LogWarning($"[FreeBala] {n}: malha de partícula sem Read/Write ({(o.mesh ? o.mesh.name : "nula")}) — usando billboard"); r.renderMode = ParticleSystemRenderMode.Billboard; }
            }
            if (o.mode == ParticleSystemRenderMode.HorizontalBillboard) r.alignment = ParticleSystemRenderSpace.World;
            ps.Play(); Systems.Add(ps); return ps;
        }

        // ---- emissão de 1 partícula (a contagem/orçamento é decidida por quem chama)
        static ParticleSystem.EmitParams ep;
        public static void Emit(ParticleSystem ps, Vector3 p, Vector3 v, float size, float life, Color c, float rot = 0, float spin = 0)
        {
            if (!ps) return;
            ep = default; ep.position = p; ep.velocity = v; ep.startSize = size; ep.startLifetime = life; ep.startColor = c; ep.rotation = rot; ep.angularVelocity = spin; ep.applyShapeToPosition = false;
            ps.Emit(ep, 1);
        }
        public static void Emit3D(ParticleSystem ps, Vector3 p, Vector3 v, Vector3 size, float life, Color c, Vector3 rot, Vector3 spin)
        {
            if (!ps) return;
            ep = default; ep.position = p; ep.velocity = v; ep.startSize3D = size; ep.startLifetime = life; ep.startColor = c; ep.rotation3D = rot; ep.angularVelocity3D = spin; ep.applyShapeToPosition = false;
            ps.Emit(ep, 1);
        }

        // ---- utilidades
        public static Vector3 Cone(Vector3 dir, float spread)
        {
            var d = dir + Random.insideUnitSphere * spread; return d.sqrMagnitude > 1e-6f ? d.normalized : dir;
        }
        public static Color Jit(Color c, float v, float a = 1) { float k = Random.Range(1 - v, 1 + v); return new Color(c.r * k, c.g * k, c.b * k, c.a * a); }
        public static void Tangents(Vector3 n, out Vector3 t, out Vector3 b)
        {
            t = Vector3.Cross(n, Mathf.Abs(n.y) > .95f ? Vector3.forward : Vector3.up).normalized; b = Vector3.Cross(t, n);
        }
        public static Mesh Quad(string name)
        {
            // quadrado 1×1 no plano XY, frente para -Z (LookRotation(-normal) deixa a face para fora da superfície)
            var q = new Mesh { name = name };
            q.vertices = new[] { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f) };
            q.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one }; q.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            q.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back }; q.tangents = new[] { new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1) };
            q.RecalculateBounds(); q.UploadMeshData(true); return q;
        }
        // cartucho: cilindro de 8 lados, eixo Y, 1 m de altura × 1 m de diâmetro (startSize3D escala para o calibre)
        public static Mesh Shell()
        {
            const int S = 8; var v = new List<Vector3>(); var tri = new List<int>(); var nr = new List<Vector3>();
            for (int i = 0; i <= S; i++) { float a = i * Mathf.PI * 2 / S; var d = new Vector3(Mathf.Cos(a) * .5f, 0, Mathf.Sin(a) * .5f); v.Add(d + Vector3.down * .5f); v.Add(d + Vector3.up * .5f); nr.Add(d.normalized); nr.Add(d.normalized); }
            for (int i = 0; i < S; i++) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            int c0 = v.Count; v.Add(Vector3.down * .5f); nr.Add(Vector3.down); int c1 = v.Count; v.Add(Vector3.up * .5f); nr.Add(Vector3.up);
            for (int i = 0; i < S; i++) { int k = i * 2; tri.AddRange(new[] { c0, k, k + 2, c1, k + 3, k + 1 }); }
            // sem UploadMeshData(true): o ParticleSystem em modo Mesh lê os vértices na CPU (malha precisa continuar legível)
            var m = new Mesh { name = "Cartucho" }; m.SetVertices(v); m.SetNormals(nr); m.SetTriangles(tri, 0); m.RecalculateBounds(); return m;
        }
    }
}
