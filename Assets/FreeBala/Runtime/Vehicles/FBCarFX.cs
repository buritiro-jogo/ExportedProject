// EFEITOS DE PNEU (porte do carfx.js) — como o RCC do Rebaixados Elite Brasil (RCC_Skidmarks + partícula WheelSlip): marcas no chão com
// a textura "Skidmarks" (sulcos no alfa) e fumaça com a folha "SmokeSprite" (6×5 quadros). Marcas = faixa contínua por roda num buffer
// circular de 1600 segmentos (as mais antigas somem aos poucos); fumaça = um ParticleSystem com pool fixo (nada de material novo na partida).
// Asfalto/concreto: marca preta + fumaça branca. Terra/areia/lama: marca marrom + poeira.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBCarFX
    {
        const int MAX_SEG = 1600, MAX_P = 360; const float SEG_MIN = .22f, MARK_W = .26f, TEX_LEN = 1.6f;
        static readonly Dictionary<int, Color> SURF_COL = new() { [0] = new(.012f, .012f, .012f, .97f), [4] = new(.02f, .02f, .02f, .75f), [1] = new(.24f, .17f, .1f, .5f), [2] = new(.33f, .27f, .17f, .42f), [3] = new(.2f, .14f, .08f, .55f) };
        static readonly Dictionary<int, Color> SMOKE_COL = new() { [0] = new(.92f, .92f, .92f), [4] = new(.92f, .92f, .92f), [1] = new(.62f, .5f, .36f), [2] = new(.78f, .7f, .52f), [3] = new(.5f, .4f, .3f) };
        class Track { public bool has; public Vector3 last, e0, e1; public float v, emitAcc, gy; public int surf, chk; }
        static GameObject root; static Mesh mesh; static Vector3[] P; static Vector2[] U; static Color[] C; static int segN; static float fadeT; static bool dirty;
        static ParticleSystem smoke; static readonly Dictionary<object, Track> tracks = new();
        static Texture2D smokeOld, smokeLight; static Material smokeMat; public static int LastSurf = -1;
        public static void SmokeTex(bool light) { var t = light ? smokeLight : smokeOld; if (smokeMat && t) { smokeMat.SetTexture("_BaseMap", t); smokeMat.mainTexture = t; } }   // r45o: teste antes × depois

        static void Init()
        {
            if (root) return;
            root = new GameObject("FB_CarFX"); Object.DontDestroyOnLoad(root);
            mesh = new Mesh { name = "skidmarks" }; mesh.MarkDynamic(); P = new Vector3[MAX_SEG * 4]; U = new Vector2[MAX_SEG * 4]; C = new Color[MAX_SEG * 4]; var idx = new int[MAX_SEG * 6];
            for (int i = 0; i < MAX_SEG; i++) { int v = i * 4; idx[i * 6] = v; idx[i * 6 + 1] = v + 2; idx[i * 6 + 2] = v + 1; idx[i * 6 + 3] = v + 2; idx[i * 6 + 4] = v + 3; idx[i * 6 + 5] = v + 1; }
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; mesh.vertices = P; mesh.uv = U; mesh.colors = C; mesh.triangles = idx; mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2e5f);
            var mg = new GameObject("marcas"); mg.transform.SetParent(root.transform, false); mg.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = mg.AddComponent<MeshRenderer>();
            var mt = Resources.Load<Texture2D>("CarFX/skidmarks"); if (mt) mt.wrapMode = TextureWrapMode.Repeat;
            var mm = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit)); if (mt) { mm.SetTexture("_BaseMap", mt); mm.mainTexture = mt; } mm.SetFloat("_Cull", 0); mm.renderQueue = 2990;   // 'polygonOffset': desenha antes das demais transparências, sem escrever profundidade
            mr.sharedMaterial = mm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            // fumaça
            var sg = new GameObject("fumaca"); sg.transform.SetParent(root.transform, false); smoke = sg.AddComponent<ParticleSystem>(); smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = smoke.main; main.maxParticles = MAX_P; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = false; main.loop = false; main.startSpeed = 0; main.gravityModifier = -.025f;   // sobe devagar (+0,25 m/s²)
            var em = smoke.emission; em.enabled = false; var sh = smoke.shape; sh.enabled = false;
            var lim = smoke.limitVelocityOverLifetime; lim.enabled = true; lim.drag = 1.6f; lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false;
            var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .18f), new Keyframe(.25f, .6f), new Keyframe(1, 1)));
            var col = smoke.colorOverLifetime; col.enabled = true; var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.5f, .5f), new GradientAlphaKey(0, 1) }); col.color = gr;
            var ts = smoke.textureSheetAnimation; ts.enabled = true; ts.numTilesX = 6; ts.numTilesY = 5; ts.animation = ParticleSystemAnimationType.WholeSheet; ts.startFrame = new ParticleSystem.MinMaxCurve(0, 29); ts.cycleCount = 2;
            // r45o: textura clareada (CarFX/smoke_light, mesmo alfa): a original é cinza-médio e, com o shader unlit, a nuvem branca do asfalto
            //  saía como mancha suja atrás do carro ("fumaça do escapamento feia")
            var pr = sg.GetComponent<ParticleSystemRenderer>(); smokeOld = Resources.Load<Texture2D>("CarFX/smoke"); smokeLight = Resources.Load<Texture2D>("CarFX/smoke_light"); var stx = smokeLight ? smokeLight : smokeOld;
            var sm = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit)); if (stx) { sm.SetTexture("_BaseMap", stx); sm.mainTexture = stx; } smokeMat = sm;
            pr.sharedMaterial = sm; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.sortingFudge = -2;
            smoke.Play();
        }
        public static void Reset()
        {
            Init(); tracks.Clear(); smoke.Clear(); for (int i = 0; i < C.Length; i++) C[i] = default; segN = 0; dirty = true;
        }
        static void WriteSeg(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float v0, float v1, Color c)
        {
            int i = segN % MAX_SEG * 4; P[i] = a0; P[i + 1] = a1; P[i + 2] = b0; P[i + 3] = b1;
            U[i] = new Vector2(0, v0); U[i + 1] = new Vector2(1, v0); U[i + 2] = new Vector2(0, v1); U[i + 3] = new Vector2(1, v1);
            for (int k = 0; k < 4; k++) C[i + k] = c; segN++; dirty = true;
        }
        static void Emit(Vector3 p, Vector3 vel, Color col, float a, float s0, float s1, float life)
        {
            var ep = new ParticleSystem.EmitParams { position = p, velocity = vel, startLifetime = life, startSize = s1 * 1.0f, startColor = new Color(Mathf.Lerp(col.r, 1, .6f) * col.r, Mathf.Lerp(col.g, 1, .6f) * col.g, Mathf.Lerp(col.b, 1, .6f) * col.b, a), applyShapeToPosition = false };
            smoke.Emit(ep, 1);
        }
        // uma roda: center = centro (mundo), r = raio; slip 0..1; vel = velocidade do carro; side = eixo lateral do carro (mundo); burn = burnout
        public static void Wheel(object key, Vector3 center, float r, float slip, Vector3 vel, Vector3 side, float dt, bool burn = false, bool noSmoke = false)
        {
            Init();
            if (!tracks.TryGetValue(key, out var t)) tracks[key] = t = new Track();
            if (slip < .06f) { t.has = false; return; }
            if (!t.has || (t.chk = (t.chk + 1) % 2) == 0)
            {
                if (!FBWorld.RayHit(center, Vector3.down, r + .7f, out var h)) { t.has = false; return; }
                t.surf = FBWorld.SurfaceAt(h); t.gy = h.point.y;
            }
            if (!SURF_COL.ContainsKey(t.surf)) { t.has = false; return; }
            var cp = new Vector3(center.x, t.gy + .025f, center.z); float hw = MARK_W / 2;
            if (!t.has) { t.has = true; t.last = cp; t.e0 = cp + side * hw; t.e1 = cp - side * hw; t.v = 0; }
            else
            {
                float d = Vector3.Distance(cp, t.last);
                if (d >= SEG_MIN)
                {
                    var dir = (cp - t.last).normalized; var sd = Vector3.Cross(Vector3.up, dir).normalized; if (Vector3.Dot(sd, side) < 0) sd = -sd;
                    Vector3 n0 = cp + sd * hw, n1 = cp - sd * hw; var b = SURF_COL[t.surf]; float v1 = t.v + d / TEX_LEN;
                    WriteSeg(t.e0, t.e1, n0, n1, t.v, v1, new Color(b.r, b.g, b.b, b.a * Mathf.Min(1, .35f + slip)));
                    t.e0 = n0; t.e1 = n1; t.v = v1 % 64; t.last = cp;
                }
            }
            if (noSmoke) return;
            LastSurf = t.surf; bool asphalt = t.surf == 0 || t.surf == 4; float rate = (burn ? 34 : asphalt ? 16 : 14) * slip;   /* r45d: menos e mais leve (estava "feia": nuvem opaca grande) */
            t.emitAcc += rate * dt;
            while (t.emitAcc >= 1)
            {
                t.emitAcc -= 1;
                var sp = new Vector3((Random.value - .5f) * 1.6f, .5f + Random.value * .9f, (Random.value - .5f) * 1.6f) + vel * .35f;
                var p = cp + new Vector3((Random.value - .5f) * .3f, .12f + Random.value * .15f, (Random.value - .5f) * .3f);
                Emit(p, sp, SMOKE_COL.TryGetValue(t.surf, out var sc) ? sc : SMOKE_COL[0], (asphalt ? .55f : .5f) * Mathf.Min(1, .4f + slip), .55f + Random.value * .3f, (burn ? 3.2f : 2.3f) + Random.value * .8f, (burn ? 2.4f : 1.6f) + Random.value * .8f);
            }
        }
        public static void Puff(Vector3 p, Vector3 vel, Color col, float a, float s0, float s1, float life) { Init(); Emit(p, vel, col, a, s0, s1, life); }
        public static void Update(float dt)
        {
            if (!root) return;
            if ((fadeT += dt) > .5f && segN > MAX_SEG * .75f)
            {
                fadeT = 0; int oldest = segN - MAX_SEG; float span = MAX_SEG * .25f;
                for (int j = Mathf.Max(0, oldest); j < oldest + span; j++) { int i = j % MAX_SEG * 4; float f = Mathf.Max(0, (j - oldest) / span); for (int k = 0; k < 4; k++) C[i + k].a = Mathf.Min(C[i + k].a, .97f * f); }
                dirty = true;
            }
            if (dirty) { dirty = false; mesh.vertices = P; mesh.uv = U; mesh.colors = C; }
        }
    }
}
