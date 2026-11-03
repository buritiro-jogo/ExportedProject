// Ferimentos de bala presos no corpo: o ferimento vira FILHO do osso que deforma a pele no ponto atingido e acompanha a animação.
// Como achar a pele (a hitbox é uma cápsula maior que o corpo — usar o ponto dela deixava mancha flutuando):
//   1. a hitbox mais próxima do ponto define a região (cabeça, tronco, braço E/D, perna E/D);
//   2. amostras da malha de cada SkinnedMeshRenderer, calculadas UMA vez por malha no espaço do osso dominante pela bind pose
//      (bindposes[b] × vértice) — vale para qualquer pose sem BakeMesh;
//   3. entre as amostras da região, a mais próxima do impacto (preferindo as viradas para o atirador) dá osso, ponto e normal da pele.
// Malha não legível (isReadable = false): reserva — ponto puxado para dentro da cápsula (fica rente/escondido, nunca flutuando).
// Limite por personagem (FBFxQuality.WoundsPerChar) e global (pool fixo); tudo some ao renascer (vivo de novo / teleporte / Revive).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
    public static class FBWoundFx
    {
        public const int GHead = 1, GBody = 2, GArmL = 4, GArmR = 8, GLegL = 16, GLegR = 32;

        public class Wound { public GameObject go; public Transform tr; public MeshRenderer mr; public FBCharacter owner; public Transform bone; public int group; public int seq; public bool on, streak; public Renderer skin; }
        class MeshSamples { public int[] bone; public Vector3[] lp, ln; }
        class CharInfo
        {
            public FBCharacter ch; public SkinnedMeshRenderer[] smr; public MeshSamples[] ms; public Dictionary<Transform, int> group; public bool wasAlive = true; public Vector3 lastPos;
            public readonly List<Wound> wounds = new(); public float dripT;
        }
        static readonly Dictionary<Mesh, MeshSamples> meshCache = new();
        static readonly Dictionary<FBCharacter, CharInfo> chars = new();
        static Wound[] pool; static Material mat, matStreak; static Mesh quad; static MaterialPropertyBlock mpb; static int seq; static bool ready;
        static readonly int idST = Shader.PropertyToID("_BaseMap_ST"), idBaseColor = Shader.PropertyToID("_BaseColor");

        public static int Capacity => pool?.Length ?? 0;
        public static int ActiveCount { get { int n = 0; if (pool != null) foreach (var w in pool) if (w.on) n++; return n; } }
        public static List<Wound> WoundsOf(FBCharacter ch) => ch && chars.TryGetValue(ch, out var ci) ? ci.wounds : new List<Wound>();

        public static void Warm()
        {
            if (ready) return; ready = true; FBFxQuality.Ensure();
            quad = FBFxLib.Quad("FerimentoQuad"); mpb = new MaterialPropertyBlock();
            mat = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("wounds"), Color.white, "Ferimento"); if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .55f);
            matStreak = FBFxLib.Mat("Lit_Alpha", FBFxLib.Tex("blood_splats"), new Color(1, 1, 1, .85f), "FerimentoEscorrido"); if (matStreak.HasProperty("_Smoothness")) matStreak.SetFloat("_Smoothness", .5f);
            pool = new Wound[FBFxQuality.PoolWounds]; for (int i = 0; i < pool.Length; i++) pool[i] = Make(i);
        }
        static Wound Make(int i)
        {
            var w = new Wound(); var g = new GameObject("Ferimento" + i); g.transform.SetParent(FBFxLib.Root, false); w.go = g; w.tr = g.transform;
            g.AddComponent<MeshFilter>().sharedMesh = quad; w.mr = g.AddComponent<MeshRenderer>(); w.mr.sharedMaterial = mat; w.mr.shadowCastingMode = ShadowCastingMode.Off;
            w.mr.lightProbeUsage = LightProbeUsage.BlendProbes; w.mr.reflectionProbeUsage = ReflectionProbeUsage.Off; w.mr.enabled = false; g.SetActive(false); return w;
        }

        // ---------------- região pelo esqueleto humano
        static int GroupOf(HumanBodyBones b)
        {
            switch (b)
            {
                case HumanBodyBones.Head: case HumanBodyBones.Jaw: case HumanBodyBones.LeftEye: case HumanBodyBones.RightEye: return GHead;
                case HumanBodyBones.Neck: return GHead | GBody;
                case HumanBodyBones.Hips: case HumanBodyBones.Spine: case HumanBodyBones.Chest: case HumanBodyBones.UpperChest: return GBody;
                case HumanBodyBones.LeftShoulder: return GBody | GArmL;
                case HumanBodyBones.RightShoulder: return GBody | GArmR;
                case HumanBodyBones.LeftUpperLeg: case HumanBodyBones.LeftLowerLeg: case HumanBodyBones.LeftFoot: case HumanBodyBones.LeftToes: return GLegL;
                case HumanBodyBones.RightUpperLeg: case HumanBodyBones.RightLowerLeg: case HumanBodyBones.RightFoot: case HumanBodyBones.RightToes: return GLegR;
            }
            var s = b.ToString();
            if (s.StartsWith("Left")) return s.Contains("Leg") || s.Contains("Foot") || s.Contains("Toe") ? GLegL : GArmL;     // braço, mão, dedos
            if (s.StartsWith("Right")) return s.Contains("Leg") || s.Contains("Foot") || s.Contains("Toe") ? GLegR : GArmR;
            return GBody;
        }
        public static string GroupName(int g) => (g & GHead) != 0 ? "head" : (g & (GArmL | GArmR)) != 0 ? "arm" : (g & (GLegL | GLegR)) != 0 ? "leg" : "body";

        static CharInfo Info(FBCharacter ch)
        {
            var smr = ch.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            if (chars.TryGetValue(ch, out var ci) && Same(ci.smr, smr)) return ci;
            if (ci == null) { ci = new CharInfo { ch = ch, lastPos = ch.transform.position, wasAlive = ch.alive }; chars[ch] = ci; }
            ci.smr = smr; ci.ms = new MeshSamples[smr.Length]; ci.group = new Dictionary<Transform, int>();
            var an = ch.GetComponent<Animator>();
            if (an && an.isHuman) for (int i = 0; i < (int)HumanBodyBones.LastBone; i++) { var t = an.GetBoneTransform((HumanBodyBones)i); if (t) ci.group[t] = GroupOf((HumanBodyBones)i); }
            for (int i = 0; i < smr.Length; i++) ci.ms[i] = Samples(smr[i].sharedMesh);
            return ci;
        }
        static bool Same(SkinnedMeshRenderer[] a, SkinnedMeshRenderer[] b) { if (a == null || a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        // região de um osso qualquer (torção, ajudante, osso de roupa): a do ancestral humano mais próximo
        static int GroupOfBone(CharInfo ci, Transform t)
        {
            for (var p = t; p && p != ci.ch.transform; p = p.parent) if (ci.group.TryGetValue(p, out int g)) { if (p != t) ci.group[t] = g; return g; }
            return GBody;
        }

        // amostras da pele no espaço do osso dominante (até ~1500 por malha; cache por malha compartilhada entre personagens)
        static MeshSamples Samples(Mesh m)
        {
            if (!m) return null; if (meshCache.TryGetValue(m, out var s)) return s;
            s = null;
            if (m.isReadable && m.bindposeCount > 0)
            {
                var v = m.vertices; var nr = m.normals; var bw = m.boneWeights; var bp = m.bindposes;
                if (bw != null && bw.Length == v.Length)
                {
                    int stride = Mathf.Max(1, v.Length / 1500), n = (v.Length + stride - 1) / stride;
                    s = new MeshSamples { bone = new int[n], lp = new Vector3[n], ln = new Vector3[n] }; int k = 0;
                    for (int i = 0; i < v.Length && k < n; i += stride, k++)
                    {
                        int b = bw[i].boneIndex0; if (b < 0 || b >= bp.Length) b = 0;
                        s.bone[k] = b; s.lp[k] = bp[b].MultiplyPoint3x4(v[i]); s.ln[k] = nr != null && nr.Length == v.Length ? bp[b].MultiplyVector(nr[i]).normalized : Vector3.zero;
                    }
                }
            }
            meshCache[m] = s; return s;
        }

        // hitbox do acerto: só as da região informada pelo dano (head/body/limb) e, entre elas, a de menor distância COM SINAL
        // (negativa = ponto dentro). As hitboxes se sobrepõem — a cápsula do tronco (Spine→Neck, raio 0,28) envolve a frente da
        // cabeça —, então "a mais próxima" com distância cortada em 0 empatava e o tronco vencia um tiro na cabeça.
        // Geometria própria: funciona com o colisor desligado do morto.
        static FBHitbox NearestHitbox(FBCharacter ch, Vector3 p, string part, out float best)
        {
            FBHitbox hb = null; best = 1e9f; var all = ch.GetComponentsInChildren<FBHitbox>(true);
            for (int pass = 0; pass < 2 && !hb; pass++)                  // 2ª passada: região sem hitbox correspondente → qualquer uma
                foreach (var h in all)
                {
                    if (pass == 0 && !PartMatch(h, part)) continue;
                    float d = 1e9f; var tr = h.transform;
                    if (h.TryGetComponent<SphereCollider>(out var sc)) d = Vector3.Distance(p, tr.TransformPoint(sc.center)) - sc.radius * tr.lossyScale.x;
                    else if (h.TryGetComponent<CapsuleCollider>(out var cc)) { Seg(tr, cc, out var a, out var b, out float r); d = Vector3.Distance(p, Closest(a, b, p)) - r; }
                    if (d < best) { best = d; hb = h; }
                }
            return hb;
        }
        static bool PartMatch(FBHitbox h, string part) => part switch
        {
            null or "" => true,
            "head" => h.head,
            "body" or "chest" or "stomach" => !h.head && FBHitbox.Torso(h.part),
            _ => !h.head && !FBHitbox.Torso(h.part),                       // "limb" / "arm" / "leg"
        };
        static void Seg(Transform tr, CapsuleCollider cc, out Vector3 a, out Vector3 b, out float r)
        {
            var ax = cc.direction == 0 ? Vector3.right : cc.direction == 1 ? Vector3.up : Vector3.forward; float half = Mathf.Max(0, cc.height * .5f - cc.radius);
            a = tr.TransformPoint(cc.center - ax * half); b = tr.TransformPoint(cc.center + ax * half); r = cc.radius * tr.lossyScale.x;
        }
        static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p) { var ab = b - a; float t = ab.sqrMagnitude < 1e-8f ? 0 : Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude); return a + ab * t; }

        /// <summary>Prende um ferimento no corpo. p = ponto do acerto, dir = direção do projétil. size em metros. Devolve o ferimento (ou null).</summary>
        /// <param name="part">região do acerto (FBDamage: "head" | "body" | "limb"); null = decide só pelo ponto</param>
        public static Wound Attach(FBCharacter ch, Vector3 p, Vector3 dir, float size, bool streak = false, string part = null)
        {
            if (!ch || !FBFx.BloodOn) return null; if (!ready) Warm();
            if (ch.isPlayer && FBPlayer.I && FBPlayer.I.firstPerson) return null;              // o próprio corpo não aparece em 1ª pessoa
            var ci = Info(ch); var hb = NearestHitbox(ch, p, part, out _); if (!hb) return null;
            int grp = GroupOfBone(ci, hb.transform.parent ? hb.transform.parent : hb.transform);
            if (hb.head) grp = GHead;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : -ch.transform.forward;

            // amostra de pele mais próxima na região
            Transform bone = null; Vector3 lp = default, ln = default; float bestS = .22f * Mathf.Max(.5f, ch.transform.lossyScale.y / FBConst.ModelScale); Renderer skin = null;
            for (int i = 0; i < ci.smr.Length; i++)
            {
                var r = ci.smr[i]; var s = ci.ms[i]; if (!r || s == null || !r.enabled) continue; var bones = r.bones; if (bones == null) continue;
                for (int k = 0; k < s.bone.Length; k++)
                {
                    int bi = s.bone[k]; if (bi >= bones.Length) continue; var bt = bones[bi]; if (!bt) continue;
                    if ((GroupOfBone(ci, bt) & grp) == 0) continue;
                    var wp = bt.TransformPoint(s.lp[k]); float sc = (wp - p).magnitude;
                    if (s.ln[k] != Vector3.zero && Vector3.Dot(bt.TransformDirection(s.ln[k]), dir) > .15f) sc += .06f;     // pele virada para o outro lado
                    if (sc < bestS) { bestS = sc; bone = bt; lp = s.lp[k]; ln = s.ln[k]; skin = r; }
                }
            }
            if (!bone)
            {   // reserva: dentro da cápsula na direção do impacto (sem malha legível)
                bone = hb.transform.parent ? hb.transform.parent : hb.transform; Vector3 c;
                if (hb.TryGetComponent<CapsuleCollider>(out var cc)) { Seg(hb.transform, cc, out var a, out var b, out _); c = Closest(a, b, p); }
                else if (hb.TryGetComponent<SphereCollider>(out var sp)) c = hb.transform.TransformPoint(sp.center); else c = bone.position;
                var off = p - c; var wp = c + off * .72f; lp = bone.InverseTransformPoint(wp); ln = bone.InverseTransformDirection(off.sqrMagnitude > 1e-6f ? off.normalized : -dir);
                skin = ci.smr.Length > 0 ? ci.smr[0] : null;
            }
            if (ln == Vector3.zero) ln = bone.InverseTransformDirection(-dir);

            // limite por personagem: recicla o mais antigo dele
            var mine = ci.wounds; while (mine.Count >= FBFxQuality.WoundsPerChar * (streak ? 1 : 1) && mine.Count > 0) { Free(mine[0]); }
            var w = TakeSlot(); w.on = true; w.owner = ch; w.bone = bone; w.group = grp; w.seq = ++seq; w.streak = streak; w.skin = skin; mine.Add(w);
            w.go.SetActive(true); w.tr.SetParent(bone, false); w.go.layer = skin ? skin.gameObject.layer : ch.gameObject.layer;
            float ks = Mathf.Max(1e-4f, Mathf.Abs(bone.lossyScale.x)), lift = .0035f / ks; var lnN = ln.normalized;
            var upL = bone.InverseTransformDirection(Vector3.up); if (streak) upL = Vector3.ProjectOnPlane(upL, lnN);
            var rot = Quaternion.LookRotation(-lnN, streak && upL.sqrMagnitude > 1e-4f ? upL.normalized : Quaternion.AngleAxis(Random.Range(0, 360f), lnN) * Perp(lnN));
            w.tr.localPosition = lp + lnN * lift + (streak ? rot * Vector3.down * (size * .35f / ks) : Vector3.zero); w.tr.localRotation = rot;
            w.tr.localScale = new Vector3(size, size * (streak ? 1.6f : 1), size) / ks;
            w.mr.sharedMaterial = streak ? matStreak : mat; w.mr.sortingOrder = w.seq % 30000;
            int cell = streak ? FBDecalFx.CellDrip + Random.Range(0, 2) : Random.Range(0, 3);       // 0–2 entrada; 3 = saída (bem maior)
            if (!streak && size > .055f && Random.value < .5f) cell = 3;
            int cols = streak ? 4 : 2, rows = cols; mpb.Clear(); mpb.SetVector(idST, new Vector4(1f / cols, 1f / rows, (cell % cols) / (float)cols, 1 - (cell / cols + 1f) / rows));
            mpb.SetColor(idBaseColor, streak ? new Color(.85f, .8f, .8f, .9f) : Color.white); w.mr.SetPropertyBlock(mpb); w.mr.enabled = true;
            return w;
        }
        static Vector3 Perp(Vector3 n) => Vector3.Cross(n, Mathf.Abs(n.y) > .95f ? Vector3.forward : Vector3.up).normalized;

        static Wound TakeSlot()
        {
            int used = 0; Wound free = null; foreach (var w in pool) { if (w.on) used++; else if (free == null) free = w; }
            if (free == null || used >= FBFxQuality.MaxWounds) { free = null; foreach (var w in pool) if (w.on && (free == null || w.seq < free.seq)) free = w; Free(free); }
            int i = System.Array.IndexOf(pool, free); if (!free.go) pool[i] = free = Make(i);    // personagem destruído levou o objeto junto: recria
            return free;
        }
        static void Free(Wound w)
        {
            if (w == null) return; w.on = false;
            if (w.owner && chars.TryGetValue(w.owner, out var ci)) ci.wounds.Remove(w); else foreach (var c in chars.Values) c.wounds.Remove(w);
            w.owner = null; w.bone = null; if (!w.go) return;
            w.mr.enabled = false; w.tr.SetParent(FBFxLib.Root, false); w.go.SetActive(false);
        }
        public static void ClearCharacter(FBCharacter ch)
        {
            if (!ch || !chars.TryGetValue(ch, out var ci)) return;
            for (int i = ci.wounds.Count - 1; i >= 0; i--) Free(ci.wounds[i]);
        }
        public static void Clear() { if (pool != null) foreach (var w in pool) if (w.on) Free(w); chars.Clear(); }

        // por quadro: renascer limpa, personagem destruído devolve as vagas, 1ª pessoa/pele escondida esconde, gotas sob o ferido
        static readonly List<FBCharacter> dead = new();
        public static void Tick(float dt)
        {
            if (!ready) return; dead.Clear();
            foreach (var kv in chars)
            {
                var ch = kv.Key; var ci = kv.Value;
                if (!ch) { dead.Add(ch); continue; }
                var pos = ch.transform.position;
                bool respawn = (ch.alive && !ci.wasAlive) || (pos - ci.lastPos).sqrMagnitude > 64;    // voltou a viver ou teleportou (> 8 m num quadro)
                ci.wasAlive = ch.alive; ci.lastPos = pos;
                if (respawn && ci.wounds.Count > 0) { ClearCharacter(ch); continue; }
                for (int i = ci.wounds.Count - 1; i >= 0; i--)
                {
                    var w = ci.wounds[i]; if (!w.go || !w.bone) { w.on = false; ci.wounds.RemoveAt(i); continue; }
                    bool show = w.skin ? w.skin.enabled && w.skin.gameObject.activeInHierarchy && w.skin.shadowCastingMode != ShadowCastingMode.ShadowsOnly : true;
                    if (w.mr.enabled != show) w.mr.enabled = show;
                }
                if (ci.wounds.Count > 0 && FBFxQuality.Drips && (ch.alive || ch.knocked)) Drip(ch, ci, dt);
            }
            foreach (var c in dead) { if (chars.TryGetValue(c, out var ci)) { foreach (var w in ci.wounds) { w.on = false; w.owner = null; } } chars.Remove(c); }
        }
        // gotas no chão sob o ferido: mais frequentes com pouca vida ou caído
        static void Drip(FBCharacter ch, CharInfo ci, float dt)
        {
            if ((ci.dripT -= dt) > 0) return;
            float hpK = Mathf.Clamp01(1 - ch.hp / 100f); ci.dripT = Mathf.Lerp(3.2f, .9f, hpK) * Random.Range(.7f, 1.3f); if (ch.knocked) ci.dripT *= .6f;
            if (ch.hp > 75 && !ch.knocked) return;
            var w = ci.wounds[ci.wounds.Count - 1]; if (!w.tr) return; var p = w.tr.position + Random.insideUnitSphere * .05f;
            if (FBWorld.GroundY(p.x, p.y, p.z, .2f, 2.5f) is float gy && FBFxQuality.Lod(p) > 0)
                FBDecalFx.Place(FBDecalFx.Atlas.Blood, FBDecalFx.CellDrops + Random.Range(0, 2), new Vector3(p.x, gy, p.z), Vector3.up, Random.Range(.12f, .22f), null, 1, null, FBFxQuality.DecalLife * .6f);
        }
    }
}
