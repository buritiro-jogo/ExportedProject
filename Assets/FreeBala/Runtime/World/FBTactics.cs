// Visual/HUD dos sistemas recuperados dos APKs (porte do tactics.js + updateTactics/updateKillstreak do main.js; regras em FBMechanics):
//   brilho da luneta (IE bl_SniperGlint: sniper inimigo cuja mira passa a até 12 m de você reflete a luz da lente; parede no meio apaga),
//   proteção de spawn (r45: aura justa ao corpo no lugar da bolha azul; a sua só em 3ª pessoa, em 1ª pessoa barra + brilho na borda da tela), tela de caído com a barra de sangramento e de reanimação
//   (IE bl_KnockDownUI) + véu vermelho, marcadores sobre aliados caídos, chip da sequência de abates e painel do atirador (CF KillStreakHUD).
// Materiais criados uma vez (pool fixo), nada é criado no meio da partida.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBTactics : MonoBehaviour
    {
        public static FBTactics I; const int N_GLINT = 6, N_SHIELD = 10;
        readonly List<Transform> glints = new(), rings = new(); Material auraMat, ringMat; bool shieldNew; MaterialPropertyBlock mpb;
        class Aura { public readonly List<(SkinnedMeshRenderer src, SkinnedMeshRenderer dst)> r = new(); }
        readonly Dictionary<FBCharacter, Aura> auras = new(); Image protBar; RawImage protEdge; float protTotal = 1, protSeen;
        class ShieldSt { public float start, until, broke = -1; }
        readonly Dictionary<FBCharacter, ShieldSt> shSt = new();
        static readonly int idAlpha = Shader.PropertyToID("_Alpha"), idIntro = Shader.PropertyToID("_Intro"), idEnd = Shader.PropertyToID("_End"), idColor = Shader.PropertyToID("_Color"), idFeet = Shader.PropertyToID("_Feet"), idHeight = Shader.PropertyToID("_Height");
        GameObject knock, veil, streak, gun, prot; Image knockBar, reviveBar, gunTime, gunArmor, gunExit; Text knockSub, reviveTxt, streakN, streakPips, gunTitle, gunTm, gunTl, gunAl, protT; GameObject reviveBox; readonly List<Text> marks = new();
        public static void Spawn() { if (I) return; var g = new GameObject("FB_Tactics"); I = g.AddComponent<FBTactics>(); I.Build(); }
        static Texture2D GlintTex()
        {
            int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float dx = (x - 63.5f) / 64, dy = (y - 63.5f) / 64, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = r < .12f ? Mathf.Lerp(1, .95f, r / .12f) : r < .35f ? Mathf.Lerp(.95f, .35f, (r - .12f) / .23f) : Mathf.Lerp(.35f, 0, Mathf.Clamp01((r - .35f) / .65f));
                    float ray = Mathf.Max(Mathf.Clamp01(1 - (dx * dx / .94f + dy * dy / .0025f)), Mathf.Clamp01(1 - (dx * dx / .0025f + dy * dy / .39f))) * .9f;   // raios em cruz (reflexo da lente)
                    t.SetPixel(x, y, new Color(1, Mathf.Lerp(.86f, .98f, 1 - r), Mathf.Lerp(.6f, .88f, 1 - r), Mathf.Clamp01(a + ray)));
                }
            t.Apply(); return t;
        }
        void Build()
        {
            FBLaser.Warm();                                                                       // r45: feixes/lanternas criados no carregamento
            var gm = FBShaders.Transparent(new Material(FBShaders.ParticlesUnlit), true); var gt = GlintTex(); gm.SetTexture("_BaseMap", gt); gm.mainTexture = gt; gm.SetFloat("_Cull", 0);
            for (int i = 0; i < N_GLINT; i++) { var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "brilho_luneta"; q.transform.SetParent(transform); q.GetComponent<Renderer>().sharedMaterial = gm; q.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; q.SetActive(false); glints.Add(q.transform); }
            // proteção de spawn (r45): AURA no corpo (Resources/FX/FBSpawnAura.shader, segunda malha skinned com o mesmo esqueleto) + anel discreto
            // no chão (modo anel do FBSpawnShield). Sem suporte ao shader: só o HUD (a bolha translúcida antiga saiu a pedido)
            var auSh = Resources.Load<Shader>("FX/FBSpawnAura"); var rgSh = Resources.Load<Shader>("FX/FBSpawnShield"); shieldNew = auSh && auSh.isSupported;
            if (shieldNew) { auraMat = new Material(auSh) { name = "aura_protecao" }; if (rgSh && rgSh.isSupported) { ringMat = new Material(rgSh) { name = "anel_protecao" }; ringMat.SetFloat("_Mode", 1); } }
            for (int i = 0; i < N_SHIELD && ringMat; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "anel_protecao"; q.transform.SetParent(transform); var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = ringMat; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; qr.receiveShadows = false; q.SetActive(false); rings.Add(q.transform);
            }
            // ---- HUD
            var cv = FBUI.Canvas("Tatico", 29); cv.transform.SetParent(transform);
            veil = new GameObject("veu", typeof(RectTransform), typeof(RawImage)); veil.transform.SetParent(cv.transform, false); var vr = (RectTransform)veil.transform; vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one; vr.offsetMin = vr.offsetMax = Vector2.zero;
            var vt = new Texture2D(64, 64); for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float r = new Vector2((x - 31.5f) / 32, (y - 31.5f) / 32).magnitude; vt.SetPixel(x, y, new Color(.43f, 0, 0, Mathf.Clamp01((r - .55f) / .6f) * .55f)); } vt.Apply(); var vi = veil.GetComponent<RawImage>(); vi.texture = vt; vi.raycastTarget = false; veil.SetActive(false);
            knock = FBUI.Rect(cv.transform, "caido", new Vector2(.5f, .22f), new Vector2(.5f, .22f), Vector2.zero, new Vector2(420, 120)).gameObject;
            FBUI.Label(knock.transform, "VOCÊ FOI DERRUBADO", 22, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(420, 30), TextAnchor.MiddleCenter, true, new Color(1, .29f, .23f));
            knockBar = Bar(knock.transform, new Vector2(0, -38), 420, new Color(1, .29f, .23f));
            knockSub = FBUI.Label(knock.transform, "", 12, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -56), new Vector2(420, 18), TextAnchor.MiddleCenter, false, new Color(.93f, .91f, .86f, .85f));
            reviveBox = FBUI.Rect(knock.transform, "rv", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -86), new Vector2(420, 40)).gameObject;
            reviveTxt = FBUI.Label(reviveBox.transform, "SENDO REANIMADO", 12, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(420, 18), TextAnchor.MiddleCenter);
            reviveBar = Bar(reviveBox.transform, new Vector2(0, -24), 420, new Color(.93f, .91f, .86f)); knock.SetActive(false);
            streak = FBUI.Rect(cv.transform, "sequencia", new Vector2(0, 0), new Vector2(0, 0), new Vector2(112, 165), new Vector2(190, 60)).gameObject;
            FBUI.Label(streak.transform, "SEQUÊNCIA DE ABATES", 11, new Vector2(0, 1), new Vector2(0, 1), new Vector2(95, -8), new Vector2(190, 16), TextAnchor.MiddleLeft, false, new Color(1, 1, 1, .8f));
            streakN = FBUI.Label(streak.transform, "", 16, new Vector2(0, 1), new Vector2(0, 1), new Vector2(95, -28), new Vector2(190, 22), TextAnchor.MiddleLeft, true);
            streakPips = FBUI.Label(streak.transform, "", 12, new Vector2(0, 1), new Vector2(0, 1), new Vector2(95, -48), new Vector2(190, 16), TextAnchor.MiddleLeft, false); streak.AddComponent<Button>().onClick.AddListener(() => FBKillstreak2.Call()); streak.SetActive(false);
            gun = FBUI.Rect(cv.transform, "atirador", new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -150), new Vector2(380, 130)).gameObject;
            gunTitle = FBUI.Label(gun.transform, "", 11, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(380, 16), TextAnchor.MiddleCenter, false, new Color(1, 1, 1, .8f));
            gunTm = FBUI.Label(gun.transform, "", 28, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -32), new Vector2(380, 34), TextAnchor.MiddleCenter, true);
            gunTl = FBUI.Label(gun.transform, "", 10, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -56), new Vector2(380, 14), TextAnchor.MiddleRight, false); FBUI.Label(gun.transform, "TEMPO", 10, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -56), new Vector2(380, 14), TextAnchor.MiddleLeft, false);
            gunTime = Bar(gun.transform, new Vector2(0, -68), 380, new Color(.93f, .91f, .86f));
            gunAl = FBUI.Label(gun.transform, "", 10, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -82), new Vector2(380, 14), TextAnchor.MiddleRight, false); FBUI.Label(gun.transform, "BLINDAGEM", 10, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -82), new Vector2(380, 14), TextAnchor.MiddleLeft, false);
            gunArmor = Bar(gun.transform, new Vector2(0, -94), 380, new Color(.93f, .91f, .86f));
            FBUI.Label(gun.transform, "SEGURE G PARA SAIR", 11, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -110), new Vector2(380, 16), TextAnchor.MiddleCenter, false, new Color(1, 1, 1, .8f));
            gunExit = Bar(gun.transform, new Vector2(0, -124), 120, new Color(.93f, .91f, .86f)); gun.SetActive(false);
            protT = FBUI.Label(cv.transform, "", 11, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -34), new Vector2(360, 18), TextAnchor.MiddleCenter, false, new Color(.56f, .83f, 1)); prot = protT.gameObject; prot.SetActive(false);
            protBar = Bar(prot.transform, new Vector2(0, -20), 140, new Color(.56f, .83f, 1, .9f)); protBar.transform.parent.GetComponent<Image>().color = new Color(1, 1, 1, .1f);
            // 1ª pessoa: brilho fino ciano nas bordas da tela enquanto protegido (a aura do próprio corpo não aparece nessa câmera)
            var eg = new GameObject("protecao_borda", typeof(RectTransform), typeof(RawImage)); eg.transform.SetParent(cv.transform, false); var er = (RectTransform)eg.transform; er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one; er.offsetMin = er.offsetMax = Vector2.zero;
            var et = new Texture2D(64, 64); for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float ex = Mathf.Min(x, 63 - x) / 63f, ey = Mathf.Min(y, 63 - y) / 63f, e = Mathf.Min(ex, ey); et.SetPixel(x, y, new Color(.45f, .85f, 1, Mathf.Pow(Mathf.Clamp01(1 - e / .09f), 2) * .5f)); } et.Apply();
            protEdge = eg.GetComponent<RawImage>(); protEdge.texture = et; protEdge.raycastTarget = false; eg.SetActive(false);
            for (int i = 0; i < 6; i++) { var t = FBUI.Label(cv.transform, "", 11, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(220, 36), TextAnchor.LowerCenter, true, new Color(1, .29f, .23f)); t.gameObject.SetActive(false); marks.Add(t); }
        }
        static Image Bar(Transform p, Vector2 pos, float w, Color c)
        {
            var bg = FBUI.Panel(p, "barra", new Vector2(.5f, 1), new Vector2(.5f, 1), pos, new Vector2(w, 6), new Color(1, 1, 1, .14f));
            var f = FBUI.Panel(bg.transform, "fill", Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero, c); f.rectTransform.offsetMin = f.rectTransform.offsetMax = Vector2.zero; return f;
        }
        static void Fill(Image i, float k) => i.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(k), 1);
        // aura: entra materializando dos pés à cabeça (0,45 s), some em pontos nos últimos 0,6 s; se a proteção cai antes (atirou) apaga em 0,35 s.
        // Aliados/você em ciano, inimigos em vermelho. Na tela de carregamento a aura fica invisível no jogador para o shader compilar ali.
        Aura AuraOf(FBCharacter a)
        {
            var srcs = a.GetComponentsInChildren<SkinnedMeshRenderer>(false).Where(x => x && x.enabled && x.sharedMesh && !x.name.StartsWith("aura_")).ToList();
            if (auras.TryGetValue(a, out var au) && au.r.All(p => p.src && p.dst) && au.r.Count == srcs.Count) return au;
            if (au != null) foreach (var (_, d) in au.r) if (d) Destroy(d.gameObject);
            au = new Aura();
            foreach (var src in srcs)
            {
                var g = new GameObject("aura_" + src.name); g.layer = src.gameObject.layer; g.transform.SetParent(src.transform.parent, false);
                g.transform.localPosition = src.transform.localPosition; g.transform.localRotation = src.transform.localRotation; g.transform.localScale = src.transform.localScale;
                var d = g.AddComponent<SkinnedMeshRenderer>(); d.sharedMesh = src.sharedMesh; d.bones = src.bones; d.rootBone = src.rootBone; d.localBounds = src.localBounds; d.quality = src.quality;
                var mats = new Material[src.sharedMesh.subMeshCount]; for (int i = 0; i < mats.Length; i++) mats[i] = auraMat; d.sharedMaterials = mats;
                d.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; d.receiveShadows = false; d.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; d.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                d.skinnedMotionVectors = false; d.enabled = false; au.r.Add((src, d));
            }
            auras[a] = au; return au;
        }
        public int AuraCount(FBCharacter a) => a && auras.TryGetValue(a, out var au) ? au.r.Count(p => p.dst && p.dst.enabled) : 0;   // laboratório
        void ShowAura(FBCharacter a, float alpha, float intro, float end, Color col)
        {
            var au = AuraOf(a); mpb.Clear(); mpb.SetFloat(idAlpha, alpha); mpb.SetFloat(idIntro, intro); mpb.SetFloat(idEnd, end); mpb.SetColor(idColor, col);
            mpb.SetFloat(idFeet, a.Pos.y); mpb.SetFloat(idHeight, 1.8f * a.ScaleK);
            foreach (var (src, d) in au.r)
            {
                if (!src || !d) continue; bool on = src.enabled && src.gameObject.activeInHierarchy && src.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                if (d.sharedMesh != src.sharedMesh) d.sharedMesh = src.sharedMesh;                     // LOD trocou a malha
                if (d.enabled != on) d.enabled = on; if (on) d.SetPropertyBlock(mpb);
            }
        }
        void HideAura(FBCharacter a) { if (a && auras.TryGetValue(a, out var au)) foreach (var (_, d) in au.r) if (d && d.enabled) d.enabled = false; }
        void Shields(FBCharacter P, float now)
        {
            int si = 0; bool tps = !(FBPlayer.I && FBPlayer.I.FirstPerson);
            if (!shieldNew) return;
            mpb ??= new MaterialPropertyBlock();
            var shown = new HashSet<FBCharacter>();
            if (FBLoading.Busy && P) { ShowAura(P, .002f, 1, 0, Color.black); shown.Add(P); }
            var ally = new Color(.3f, .85f, 1.7f) * .7f; var foe = new Color(1.7f, .32f, .2f) * .7f;
            int n = 0;
            foreach (var a in FBMatch.All)
            {
                if (!a) continue; shSt.TryGetValue(a, out var st); bool on = a.alive && a.protUntil > now;
                if (on && (st == null || st.broke >= 0 || now >= st.until)) shSt[a] = st = new ShieldSt { start = now, until = a.protUntil };
                else if (on) st.until = a.protUntil;
                else if (st != null && st.broke < 0) { if (a.alive && now < st.until - .05f) st.broke = now; else { shSt.Remove(a); continue; } }
                if (st == null) continue;
                if (st.broke >= 0 && now - st.broke > .35f) { shSt.Remove(a); continue; }
                if (n >= N_SHIELD || (a == P && !tps)) continue; n++;
                float intro = Mathf.Clamp01((now - st.start) / .45f), end = st.broke >= 0 ? Mathf.Clamp01((now - st.broke) / .35f) : Mathf.Clamp01(1 - (st.until - now) / .6f);
                float x = intro - 1, pop = 1 + 2.2f * x * x * x + 1.2f * x * x;
                bool friend = a == P || (P && !FBMatch.FFA && a.team == P.team); var col = friend ? ally : foe;
                float alpha = (st.broke >= 0 ? 1.6f - end : 1) * (.85f + .15f * Mathf.Sin(now * 3.2f));
                ShowAura(a, alpha, intro, end, col); shown.Add(a);
                if (si < rings.Count)
                {   // anel no chão, menor e mais apagado que o da bolha antiga
                    var r = rings[si++]; r.gameObject.SetActive(true); float k = a.ScaleK;
                    r.position = a.Pos + Vector3.up * .03f * k; r.rotation = Quaternion.Euler(90, now * 12, 0); r.localScale = Vector3.one * 1.45f * k * Mathf.Lerp(.6f, 1, Mathf.LerpUnclamped(.82f, 1, pop));
                    mpb.Clear(); mpb.SetFloat(idAlpha, alpha * .45f); mpb.SetFloat(idIntro, 1); mpb.SetFloat(idEnd, end); mpb.SetColor(idColor, col); r.GetComponent<Renderer>().SetPropertyBlock(mpb);
                }
            }
            foreach (var k in auras.Keys.ToList()) { if (!k) { auras.Remove(k); continue; } if (!shown.Contains(k)) HideAura(k); }
            for (int i = si; i < rings.Count; i++) rings[i].gameObject.SetActive(false);
            if (shSt.Count > 48) foreach (var k in new List<FBCharacter>(shSt.Keys)) if (!k) shSt.Remove(k);
        }
        static float PointRayDist(Vector3 o, Vector3 d, Vector3 p) { var v = p - o; float t = Mathf.Max(0, Vector3.Dot(v, d)); return (v - d * t).magnitude; }

        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBTactics.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            var cam = UnityEngine.Camera.main; var P = FBMatch.Player; if (!cam || !FBMatch.I) return; float now = Time.time; var cp = cam.transform.position;
            // ---- brilho da luneta
            int gi = 0;
            if (P && P.alive) foreach (var b in FBMatch.Bots)
                {
                    if (gi >= N_GLINT) break; if (!b || !b.alive || b.knocked || b.weapon == null || b.weapon.cls != "sniper" || !FBMatch.Hostile(b, P)) continue;
                    var head = b.an ? b.an.GetBoneTransform(HumanBodyBones.Head) : null; var hp = head ? head.position : b.Pos + Vector3.up * 1.7f * b.ScaleK; float dist = Vector3.Distance(hp, cp); if (dist > FBMechanics.GlintMaxDist) continue;
                    var bot = b.GetComponent<FBBot>(); float yw = bot ? bot.AimYawRad : b.Yaw * Mathf.Deg2Rad, pt = bot ? bot.AimPitch : 0; float cpp = Mathf.Cos(pt); var gd = new Vector3(Mathf.Sin(yw) * cpp, Mathf.Sin(pt), Mathf.Cos(yw) * cpp);
                    if (PointRayDist(hp, gd, cp) > FBMechanics.GlintArea) continue;
                    float sc = FBMechanics.GlintScaleFor(dist, cam.fieldOfView, FBPlayer.I ? FBPlayer.I.FovBase : 70); if (sc <= 0) continue;
                    var to = (cp - hp).normalized; if (FBWorld.RayHit(hp + to * .4f, to, dist - .8f)) continue;   // parede no meio: sem brilho
                    var g = glints[gi]; g.gameObject.SetActive(true); g.position = hp + gd * .3f; g.rotation = Quaternion.LookRotation(g.position - cp); float flick = .85f + Mathf.Sin(now * 25 + gi) * .15f; g.localScale = Vector3.one * Mathf.Max(.05f, sc * .45f) * flick; gi++;
                }
            for (int i = gi; i < N_GLINT; i++) glints[i].gameObject.SetActive(false);
            // ---- bolhas de proteção (a sua só em 3ª pessoa)
            Shields(P, now);
            prot.SetActive(P && P.alive && P.protUntil > now);
            if (prot.activeSelf)
            {
                float left = P.protUntil - now; if (P.protUntil != protSeen) { protSeen = P.protUntil; protTotal = Mathf.Max(.5f, left); }
                protT.text = "PROTEÇÃO DE SPAWN · " + left.ToString("0.0") + " s"; Fill(protBar, left / protTotal);
            }
            bool fpv = FBPlayer.I && FBPlayer.I.FirstPerson; protEdge.gameObject.SetActive(prot.activeSelf && fpv);
            if (protEdge.gameObject.activeSelf) protEdge.color = new Color(1, 1, 1, Mathf.Clamp01((P.protUntil - now) / .6f) * (.75f + .25f * Mathf.Sin(now * 3.2f)));
            // ---- caído
            bool kn = P && P.knocked; knock.SetActive(kn); veil.SetActive(kn);
            if (kn)
            {
                Fill(knockBar, P.hp / 100f); bool rv = P.revivedBy != null; reviveBox.SetActive(rv); if (rv) { Fill(reviveBar, P.reviveP); reviveTxt.text = "SENDO REANIMADO" + (P.revivedBy is FBCharacter rb ? " POR " + rb.Name.ToUpperInvariant() : ""); }
                bool alone = !FBMatch.TeamOf(P).Any(o => o && o.alive && !o.knocked); knockSub.text = alone ? "SEM ALIADOS DE PÉ" : "SANGRANDO · AGUENTE ATÉ UM ALIADO TE LEVANTAR";
            }
            // ---- aliados caídos
            int mi = 0;
            if (P && FBMatch.KnockOn) foreach (var o in FBMatch.TeamOf(P))
                {
                    if (mi >= marks.Count) break; if (!o.alive || !o.knocked) continue; var sp = cam.WorldToScreenPoint(o.Pos + Vector3.up * .9f * o.ScaleK); if (sp.z < 0 || sp.x < -50 || sp.x > Screen.width + 50 || sp.y < -50 || sp.y > Screen.height + 50) continue;
                    var m = marks[mi++]; m.gameObject.SetActive(true); var cvs = m.canvas; float sf = cvs ? cvs.scaleFactor : 1; m.rectTransform.anchoredPosition = new Vector2(sp.x / sf, sp.y / sf + 16);
                    m.text = "✚\n" + o.Name + " · " + Mathf.RoundToInt(Vector3.Distance(o.Pos, P.Pos)) + " m"; m.color = o.revivedBy != null ? new Color(.93f, .91f, .86f) : new Color(1, .29f, .23f);
                }
            for (int i = mi; i < marks.Count; i++) marks[i].gameObject.SetActive(false);
            // ---- sequência de abates / atirador
            var G = FBKillstreak2.gunner; var ks = FBMatch.I.ks; bool on = FBKillstreak2.On && P;
            bool show = on && G == null && ks != null && (ks.kills > 0 || ks.reward != null); streak.SetActive(show);
            if (show)
            {
                bool ready = ks.Ready(now); var nx = ks.Next(); streakN.color = ready ? FBUI.Gold : Color.white;
                streakN.text = ready ? $"[G] {FBKillstreak.STREAK[ks.reward].name} PRONTO" : nx != null ? $"{ks.kills} / {nx} · {(ks.reward != null ? FBKillstreak.STREAK["heli"].name : FBKillstreak.STREAK["caveirao"].name)}" : $"{(ks.reward != null ? FBKillstreak.STREAK[ks.reward].name : "")} PRONTO";
                int n = Mathf.Min(15, nx ?? Mathf.Max(1, ks.kills)); streakPips.text = new string('■', Mathf.Min(ks.kills, n)) + "<color=#ffffff40>" + new string('■', Mathf.Max(0, n - ks.kills)) + "</color>"; streakPips.supportRichText = true;
            }
            gun.SetActive(G != null);
            if (G != null)
            {
                gunTitle.text = "ATIRADOR · " + FBKillstreak.STREAK[G.kind].name; float left = G.total - G.t; gunTm.text = Mathf.Max(0, Mathf.CeilToInt(left)) + " s"; gunTl.text = Mathf.Max(0, left).ToString("0") + " s";
                Fill(gunTime, left / Mathf.Max(.01f, G.total)); gunAl.text = Mathf.RoundToInt(G.hp * 100) + "%"; Fill(gunArmor, G.hp); gunArmor.color = G.hp < .35f ? new Color(1, .29f, .23f) : new Color(.93f, .91f, .86f); Fill(gunExit, Mathf.Min(1, G.exitHold / FBKillstreak.HoldToExit));
            }
        }
    }
}
