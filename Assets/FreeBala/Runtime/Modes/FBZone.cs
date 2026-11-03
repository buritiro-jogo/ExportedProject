// Zona segura (ZoneCore do modes.js, compartilhada pelo 4V4 ZONA e pelo BATTLE ROYALE): espera → fecha → espera..., próximo centro num
// beco (level.alleys) ou ponto andável dentro da zona atual, dano 1×/s fora dela, parede baixa em degradê (ciano parada, âmbar fechando),
// anel fino no chão marcando a próxima zona, seta no HUD apontando para dentro. + marcadores (anel + feixe) e adereços dos modos.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBZone
    {
        public class Z { public Vector3 c; public float r, t, dmg; public int ph; public string st = "wait"; public (Vector3 c, float r)? from, to; }
        readonly FBMatch G; readonly FBBrCore.Phase[] PH; public Z zone; float tick; GameObject wall, ring; Material wallMat, ringMat;
        // r47: dano PROGRESSIVO pelo tempo fora (antes: o mesmo valor por segundo a qualquer momento) e aviso com o nome da área
        public string warn = "FORA DA ZONA"; readonly Dictionary<FBCharacter, float> outT = new();
        public static float OutsideDamage(float baseDmg, float secs) => Mathf.Min(baseDmg * (1 + secs * .5f) + secs * 1.5f, 40);
        public float OutsideSecs(FBCharacter a) => a && outT.TryGetValue(a, out var s) ? s : 0;
        public FBZone(FBMatch g, FBBrCore.Phase[] ph) { G = g; PH = ph; }
        public static List<Vector3> AlleyPoints(FBMatch G) { var l = Json.L(G.Level, "alleys"); return l == null ? new List<Vector3>() : l.Select(o => Coords.FromWeb(o as List<object>)).ToList(); }
        public void Fx()
        {
            if (wall) return;
            var tex = new Texture2D(64, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 256; y++) for (int x = 0; x < 64; x++)
                {
                    float v = y / 255f; float a = v < .04f ? Mathf.Lerp(.95f, .55f, v / .04f) : v < .18f ? Mathf.Lerp(.55f, .22f, (v - .04f) / .14f) : v < .55f ? Mathf.Lerp(.22f, .06f, (v - .18f) / .37f) : Mathf.Lerp(.06f, 0, (v - .55f) / .45f);
                    if (x % 8 == 0) a += .05f; tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            wallMat = Additive(tex, new Color(.27f, .82f, 1, .75f)); wallMat.mainTextureScale = new Vector2(48, 1);
            wall = Cylinder(128, 36, wallMat); ringMat = Additive(Texture2D.whiteTexture, new Color(1, 1, 1, .55f)); ring = Ring(160, .993f, 1, ringMat);
        }
        static Material Additive(Texture t, Color c)
        {
            var m = new Material(FBShaders.Unlit ?? Shader.Find("Unlit/Transparent")) { mainTexture = t }; m.SetTexture("_BaseMap", t); m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); if (m.renderQueue < 3000) m.renderQueue = 3000; m.SetFloat("_Blend", 2); m.SetFloat("_Cull", 0); m.SetFloat("_ZWrite", 0); m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3100; return m;
        }
        public static GameObject Cylinder(int seg, float h, Material m)
        {
            var g = new GameObject("ZonaParede"); var mesh = new Mesh(); var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; v.Add(new Vector3(Mathf.Cos(a), -h / 2, Mathf.Sin(a))); v.Add(new Vector3(Mathf.Cos(a), h / 2, Mathf.Sin(a))); uv.Add(new Vector2(i / (float)seg, 0)); uv.Add(new Vector2(i / (float)seg, 1)); if (i < seg) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); } }
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds(); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e4f);
            g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
        }
        public static GameObject Ring(int seg, float r0, float r1, Material m)
        {
            var g = new GameObject("Anel"); var mesh = new Mesh(); var v = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; v.Add(new Vector3(Mathf.Cos(a) * r0, 0, Mathf.Sin(a) * r0)); v.Add(new Vector3(Mathf.Cos(a) * r1, 0, Mathf.Sin(a) * r1)); if (i < seg) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); } }
            mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds(); g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g;
        }
        public Z Start(Vector3 c, float r) { zone = new Z { c = c, r = r, t = PH[0].wait }; tick = 0; return zone; }
        public void Plan()
        {
            if (zone.ph >= PH.Length) { zone.to = null; return; }
            var P = PH[zone.ph]; float r2 = Mathf.Max(4, zone.r * P.k), lim = (zone.r - r2) * .85f;
            float XZ(Vector3 p) => (p - zone.c).Flat().magnitude;
            var al = AlleyPoints(G).Where(p => XZ(p) <= lim).ToList(); var cand = al.Count > 0 ? al : FBNav.Points().Where(p => XZ(p) <= lim).ToList();
            if (cand.Count > 0) zone.to = (FBUtil.Pick(cand), r2);
            else { float a = Random.value * Mathf.PI * 2, off = lim * Mathf.Sqrt(Random.value); zone.to = (zone.c + new Vector3(Mathf.Cos(a) * off, 0, Mathf.Sin(a) * off), r2); }
        }
        public void Anim(float dt) { if (wallMat) wallMat.mainTextureOffset += new Vector2(dt * .004f, 0); }
        public void Step(float dt)
        {
            zone.t -= dt;
            if (zone.st == "wait" && zone.t <= 0 && zone.to != null) { zone.st = "shrink"; zone.from = (zone.c, zone.r); zone.t = PH[zone.ph].shrink; FBHUD.I?.MatchToast("A ZONA ESTÁ FECHANDO!", new Color(.35f, .65f, 1), 2.5f); FBAudio.Synth("zone"); }
            else if (zone.st == "shrink")
            {
                var P = PH[zone.ph]; float k = 1 - Mathf.Max(0, zone.t) / P.shrink; var f = zone.from.Value; var to = zone.to.Value;
                zone.c = Vector3.Lerp(f.c, to.c, k); zone.r = f.r + (to.r - f.r) * k; zone.dmg = P.dmg;
                if (zone.t <= 0) { zone.ph++; zone.st = "wait"; zone.t = zone.ph < PH.Length ? PH[zone.ph].wait : 999; Plan(); }
            }
            tick -= dt;
            if (tick <= 0)
            {
                tick = 1; float dmg = Mathf.Max(1, zone.dmg);
                foreach (var a in FBMatch.Actors().ToList())
                {
                    if (a.alive && (a.Pos - zone.c).Flat().magnitude > zone.r) { float s = OutsideSecs(a) + 1; outT[a] = s; FBCombat.DamageActor(a, OutsideDamage(dmg, s), false, null, "zone"); if (a.isPlayer) FBHUD.I?.Damaged(.16f + Mathf.Min(.3f, s * .03f)); }   // som do gás: banco próprio do FBVoice (tosse/respiração), sem o baque 2D a cada tick
                    else outT.Remove(a);
                }
            }
        }
        public void Visual()
        {
            var p = FBMatch.Player; bool outside = p && p.alive && zone != null && (p.Pos - zone.c).Flat().magnitude > zone.r;
            if (outside) { var d = zone.c - p.Pos; float rel = FBUtil.WrapA(Mathf.Atan2(d.x, d.z) - FBPlayer.CamYawRad); FBHUD.I?.ZoneWarning(true, rel, OutsideDamage(Mathf.Max(1, zone.dmg), OutsideSecs(p) + 1), Mathf.Max(0, d.Flat().magnitude - zone.r), warn); }
            else FBHUD.I?.ZoneWarning(false, 0, 0, 0);
            if (wall && zone != null)
            {
                bool shr = zone.st == "shrink"; wallMat.SetColor("_BaseColor", shr ? new Color(1, .6f, .24f, .75f) : new Color(.27f, .82f, 1, (FBBattleRoyale.PlayerHigh ? .22f : .75f)));
                ringMat.SetColor("_BaseColor", shr ? new Color(1, .76f, .48f, .55f) : new Color(1, 1, 1, .55f));
                wall.transform.position = new Vector3(zone.c.x, zone.c.y + 16, zone.c.z); wall.transform.localScale = new Vector3(zone.r, 1, zone.r);
                ring.SetActive(zone.to != null); if (zone.to is (Vector3 tc, float tr)) { ring.transform.position = new Vector3(tc.x, zone.c.y + .4f, tc.z); ring.transform.localScale = new Vector3(tr, 1, tr); }
            }
        }
        public string Label() => zone == null ? "" : zone.st == "shrink" ? $"ZONA FECHANDO {Mathf.CeilToInt(Mathf.Max(0, zone.t))}s" : zone.to != null ? $"ZONA EM {Mathf.CeilToInt(Mathf.Max(0, zone.t))}s" : "ZONA FINAL";
        public void Dispose() { if (wall) Object.Destroy(wall); if (ring) Object.Destroy(ring); zone = null; FBHUD.I?.ZoneWarning(false, 0, 0, 0); }
    }

    // anel no chão + feixe de luz (pontos do Tático, pedestais/entregas do Farinha × Buriti)
    public class FBMarker : MonoBehaviour
    {
        Material rm, bm; Transform ringT;
        public static FBMarker Make(Vector3 p, Color c, float r, float beamH)
        {
            var g = new GameObject("Marcador"); g.transform.position = p; var mk = g.AddComponent<FBMarker>();
            mk.rm = Mat(new Color(c.r, c.g, c.b, .55f), false); var ring = FBZone.Ring(48, r - .35f, r, mk.rm); ring.transform.SetParent(g.transform, false); ring.transform.localPosition = Vector3.up * .15f; mk.ringT = ring.transform;
            mk.bm = Mat(new Color(c.r, c.g, c.b, .28f), true); var beam = FBZone.Cylinder(8, beamH, mk.bm); beam.transform.SetParent(g.transform, false); beam.transform.localPosition = Vector3.up * beamH / 2; beam.transform.localScale = new Vector3(.25f, 1, .25f);
            return mk;
        }
        static Material Mat(Color c, bool add)
        {
            var m = new Material(FBShaders.Unlit); m.SetColor("_BaseColor", c); m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); if (m.renderQueue < 3000) m.renderQueue = 3000; m.SetFloat("_Cull", 0); m.SetFloat("_ZWrite", 0);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)(add ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha)); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3050; return m;
        }
        public void Spin(float a) { if (ringT) ringT.Rotate(0, a * Mathf.Rad2Deg, 0); }
        public void SetColor(Color c) { rm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, .55f)); bm.SetColor("_BaseColor", new Color(c.r, c.g, c.b, .28f)); if (label) label.color = new Color(Mathf.Lerp(c.r, 1, .35f), Mathf.Lerp(c.g, 1, .35f), Mathf.Lerp(c.b, 1, .35f)); }
        // letra do ponto (Tático: A–E) flutuando sobre o círculo, sempre virada para a câmera; cresce com a distância (legível de longe)
        TextMesh label;
        public void Label(string s)
        {
            var g = new GameObject("Letra"); g.transform.SetParent(transform, false); g.transform.localPosition = Vector3.up * 4.2f;
            label = g.AddComponent<TextMesh>(); label.text = s; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.fontSize = 96; label.characterSize = .06f; label.fontStyle = FontStyle.Bold;
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if (f) { label.font = f; var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = new Material(f.material); mr.sharedMaterial.renderQueue = 3100; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
        }
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBMarker.LateUpdate");   // [parceiro 01/11] medidor FBTempo
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!label) return; var cam = Camera.main; if (!cam) return; var t = label.transform; var d = t.position - cam.transform.position;
            t.rotation = Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d : cam.transform.forward, Vector3.up); t.localScale = Vector3.one * Mathf.Clamp(d.magnitude / 18f, 1, 6);
        }
    }

    // adereços feitos em código (farinhaSaco / buritiCacho do modes.js)
    public static class FBProps
    {
        static Material Lit(Color c) { var m = new Material(FBShaders.Lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .15f); return m; }
        static GameObject Prim(PrimitiveType t, Transform p, Vector3 pos, Vector3 sc, Material m) { var g = GameObject.CreatePrimitive(t); Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = sc; g.GetComponent<Renderer>().sharedMaterial = m; return g; }
        static void Glow(Transform p, Color c) { var m = new Material(FBShaders.Unlit); m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, .35f)); m.SetFloat("_Surface", 1); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); if (m.renderQueue < 3000) m.renderQueue = 3000; m.SetInt("_SrcBlend", 5); m.SetInt("_DstBlend", 1); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3050; var b = FBZone.Cylinder(8, 40, m); b.transform.SetParent(p, false); b.transform.localPosition = Vector3.up * 20.9f; b.transform.localScale = new Vector3(.12f, 1, .12f); }
        public static GameObject FarinhaSaco()
        {
            var g = new GameObject("Farinha"); var juta = Lit(new Color(.9f, .84f, .66f)); var po = Lit(new Color(.96f, .94f, .85f));
            Prim(PrimitiveType.Sphere, g.transform, Vector3.up * .44f, new Vector3(.65f, .85f, .49f), juta); Prim(PrimitiveType.Cylinder, g.transform, Vector3.up * .9f, new Vector3(.2f, .1f, .2f), juta);
            Prim(PrimitiveType.Cylinder, g.transform, Vector3.up * .86f, new Vector3(.22f, .02f, .22f), Lit(new Color(.48f, .29f, .13f))); Prim(PrimitiveType.Sphere, g.transform, Vector3.up * .02f, new Vector3(.92f, .2f, .92f), po);
            var lb = Prim(PrimitiveType.Quad, g.transform, new Vector3(0, .5f, .25f), new Vector3(.4f, .2f, 1), Lit(new Color(.96f, .94f, .85f))); lb.transform.localRotation = Quaternion.Euler(0, 180, 0);
            Glow(g.transform, new Color(.95f, .87f, .63f)); g.transform.localScale = Vector3.one * 1.3f; return g;
        }
        public static GameObject BuritiCacho()
        {
            var g = new GameObject("Buriti"); var palha = Lit(new Color(.66f, .45f, .23f));
            Prim(PrimitiveType.Cylinder, g.transform, Vector3.up * .15f, new Vector3(.66f, .15f, .66f), palha);
            var mats = new[] { new Color(.56f, .18f, .08f), new Color(.72f, .27f, .1f), new Color(.85f, .4f, .12f), new Color(.44f, .14f, .07f) }.Select(Lit).ToArray(); int i = 0;
            for (int ring = 0; ring < 3; ring++) for (int k = 0; k < 7 - ring * 2; k++, i++) { float a = k / (float)(7 - ring * 2) * Mathf.PI * 2 + ring, rr = .22f - ring * .07f; Prim(PrimitiveType.Sphere, g.transform, new Vector3(Mathf.Cos(a) * rr, .38f + ring * .13f, Mathf.Sin(a) * rr), new Vector3(.2f, .24f, .2f), mats[i % 4]); }
            Prim(PrimitiveType.Cylinder, g.transform, Vector3.up * .72f, new Vector3(.06f, .25f, .06f), Lit(new Color(.35f, .24f, .11f)));
            Glow(g.transform, new Color(.85f, .31f, .11f)); g.transform.localScale = Vector3.one * 1.4f; return g;
        }
    }

    // alvo de papel do estande (level.targets): tomba ao ser acertado e levanta de novo; alvo móvel no trilho
    public class FBRangeTarget : MonoBehaviour
    {
        const float W = .78f, H = 1.3f, BASE = .85f; Transform pivot; bool up = true; float rot, downT, ph; Vector2? moving; public float upAt, dist; public System.Action<FBRangeTarget, bool> onHit;
        static Texture2D tex;
        static Texture2D Tex()
        {
            if (tex) return tex; tex = new Texture2D(256, 420); var bg = new Color(.85f, .81f, .71f); var fg = new Color(.11f, .13f, .16f); var line = new Color(.91f, .89f, .81f);
            for (int y = 0; y < 420; y++) for (int x = 0; x < 256; x++)
                {
                    int Y = 419 - y; bool head = Mathf.Pow((x - 128) / 42f, 2) + Mathf.Pow((Y - 70) / 52f, 2) <= 1; bool torso = Y >= 118 && Y <= 420 && x >= Mathf.Lerp(108, 16, Mathf.Clamp01((Y - 118) / 52f)) && x <= Mathf.Lerp(148, 240, Mathf.Clamp01((Y - 118) / 52f));
                    var c = head || torso ? fg : bg;
                    foreach (var (cy, rx, ry) in new[] { (260f, 46f, 60f), (260f, 80f, 100f), (260f, 108f, 140f), (70f, 20f, 26f) }) { float e = Mathf.Pow((x - 128) / rx, 2) + Mathf.Pow((Y - cy) / ry, 2); if (Mathf.Abs(e - 1) < .03f) c = line; }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply(); return tex;
        }
        public static FBRangeTarget Make(Dictionary<string, object> T)
        {
            var p = Coords.FromWeb(Json.L(T, "p")); var g = new GameObject("Alvo"); g.transform.position = p; var tg = g.AddComponent<FBRangeTarget>(); tg.dist = Json.F(T, "d"); tg.upAt = Time.time; tg.ph = Random.value * 6;
            var mv = Json.L(T, "moving"); if (mv != null && mv.Count == 2) tg.moving = new Vector2(-Json.Fv(mv[0]), -Json.Fv(mv[1]));
            var ferro = new Material(FBShaders.Lit); ferro.SetColor("_BaseColor", new Color(.17f, .18f, .19f));
            foreach (var sx in new[] { -1, 1 }) { var leg = GameObject.CreatePrimitive(PrimitiveType.Cube); leg.transform.SetParent(g.transform, false); leg.transform.localPosition = new Vector3(sx * W * .42f, BASE / 2, -.06f); leg.transform.localScale = new Vector3(.05f, BASE, .05f); leg.GetComponent<Renderer>().sharedMaterial = ferro; }
            tg.pivot = new GameObject("Pivo").transform; tg.pivot.SetParent(g.transform, false); tg.pivot.localPosition = Vector3.up * BASE;
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(board.GetComponent<BoxCollider>()); board.transform.SetParent(tg.pivot, false); board.transform.localPosition = Vector3.up * H / 2; board.transform.localScale = new Vector3(W, H, .03f);
            var bm = new Material(FBShaders.Lit); bm.SetTexture("_BaseMap", Tex()); board.GetComponent<Renderer>().sharedMaterial = bm;
            var col = board.AddComponent<BoxCollider>(); col.isTrigger = true; board.layer = FBWorld.LHitbox; var hb = board.AddComponent<FBTarget>(); hb.headY = .24f;   // y local do cubo: topo = +0,5 → cabeça acima de 74 %
            hb.onHit = (t, h) => tg.Hit(h);
            return tg;
        }
        void Hit(bool head) { if (!up) return; up = false; downT = Random.Range(1.6f, 2.8f); onHit?.Invoke(this, head); }
        static readonly int __fbt_Update = FBTempo.Id("FBRangeTarget.Update");   // [parceiro 01/11] medidor FBTempo
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            float dt = Time.deltaTime;
            if (moving is Vector2 m) { ph += dt * .9f; var p = transform.position; p.x = (m.x + m.y) / 2 + (m.y - m.x) / 2 * Mathf.Sin(ph); transform.position = p; }
            if (!up) { downT -= dt; if (downT <= 0) { up = true; upAt = Time.time; } }
            float goal = up ? 0 : -90 * .96f; rot += (goal - rot) * Mathf.Min(1, dt * (up ? 6 : 14)); pivot.localRotation = Quaternion.Euler(rot, 0, 0);
        }
    }
}
