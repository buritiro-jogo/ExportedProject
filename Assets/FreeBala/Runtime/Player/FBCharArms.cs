// BRAÇOS DA 1ª PESSOA COM A MALHA DO PRÓPRIO PERSONAGEM (porte do fparms.js) — antes eram sempre as mangas/mãos genéricas do rig LPSP.
// Para cada lado: uma cópia do personagem mostrando só os triângulos do braço (braço, antebraço, mão e dedos). A cada quadro os ossos dela
// seguem as juntas do rig da 1ª pessoa: ombro e pulso presos às juntas do rig, cotovelo por IK de dois ossos com os comprimentos do
// personagem (escala igualada ao braço do rig), giro pela linha dos nós dos dedos (indicador -> mínimo), dedos apontando para as juntas
// dos dedos do rig. Sem ossos de dedos (rig só com o indicador): o braço vai até o pulso e a mão genérica do rig continua.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    [DefaultExecutionOrder(300)]
    public class FBCharArms : MonoBehaviour
    {
        class Finger { public string name; public Transform[] c, f; }
        class Arm
        {
            public Transform root; public Transform cArm, cFore, cHand, fArm, fFore, fHand; public List<Finger> fingers = new(); public bool ok, ownHand; public Finger knuckle;
            public Dictionary<Transform, (Vector3 p, Quaternion q)> rest = new(); public float la, lb, scale; public Vector3 armRest, sRest, handDirRest, baseScale;
            public Vector3 n0, lastN; public float twistArm, twistFore, pron;   // dobradiça do cotovelo no repouso / último quadro; diagnóstico (FBFpLab)
        }
        static readonly (string c, string f)[] FINGERS = { ("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"), ("Ring", "ring"), ("Little", "pinky") };
        FBFpRig rig; readonly List<Arm> arms = new(); readonly List<Renderer> generic = new(); public bool ok;

        public static FBCharArms Attach(FBFpRig rig, FBCharacter ch)
        {
            if (!rig || !ch) return null; var prefab = FBRegistry.I ? FBRegistry.I.Char(ch.skinId) : null; if (!prefab) return null;
            var old = rig.GetComponent<FBCharArms>(); if (old) { old.Dispose(); Destroy(old); }
            var a = rig.gameObject.AddComponent<FBCharArms>(); a.Build(rig, prefab); if (!a.ok) { a.Dispose(); Destroy(a); return null; }
            return a;
        }
        static Transform FpBone(FBFpRig rig, string n) { foreach (var t in rig.pitchNode.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        void Build(FBFpRig r, GameObject prefab)
        {
            rig = r;
            foreach (var side in new[] { "L", "R" })
            {
                var A = new Arm(); string C = side == "L" ? "Left" : "Right", f = side == "L" ? "_l" : "_r";
                var go = Instantiate(prefab); go.name = "fp_char_arm_" + side; A.root = go.transform; A.baseScale = go.transform.localScale;
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) if (!(mb is FBCharArms)) mb.enabled = false;
                var an = go.GetComponentInChildren<Animator>(); if (!an || !an.isHuman) { Destroy(go); continue; }
                Transform HB(string n) => System.Enum.TryParse<HumanBodyBones>(n, out var hb) ? an.GetBoneTransform(hb) : null;
                A.cArm = HB(C + "UpperArm"); A.cFore = HB(C + "LowerArm"); A.cHand = HB(C + "Hand");
                A.fArm = FpBone(rig, "upperarm" + f); A.fFore = FpBone(rig, "lowerarm" + f); A.fHand = FpBone(rig, "hand" + f);
                A.ok = A.cArm && A.cFore && A.cHand && A.fArm && A.fFore && A.fHand;
                if (!A.ok) { Destroy(go); continue; }
                foreach (var (cn, fn) in FINGERS)
                {
                    var cj = new[] { HB(C + cn + "Proximal"), HB(C + cn + "Intermediate"), HB(C + cn + "Distal"), null }; var fj = new[] { 1, 2, 3 }.Select(i => FpBone(rig, $"{fn}_0{i}{f}")).ToArray();
                    if (cj.Take(3).All(x => x) && fj.All(x => x)) A.fingers.Add(new Finger { name = cn, c = cj, f = fj });
                }
                an.enabled = false;
                // rodada 39 (retorno: "as mãos estão muito grandes, não se parece com o rig original do APK"): as malhas de IA (r_*) têm a mão
                // 20–70% maior que a do braço do LPSP em relação ao braço (mão/braço 0,41–0,55 contra 0,33; medido nos GLB) e várias com os dedos
                // fundidos (todo o dedo pesado no indicador) — ampliada até o tamanho do braço da 1ª pessoa virava um bloco sem dedos.
                // A mão agora é SEMPRE a do rig original (dedos e pegada feitos para estes clipes), tingida com o tom da mão do personagem;
                // braço e antebraço (mangas, tatuagens) continuam do personagem.
                A.ownHand = false; A.knuckle = A.fingers.Find(x => x.name == "Middle") ?? A.fingers.Find(x => x.name == "Index");
                if (handTint == null) handTint = HandColor(go, A.cHand);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var o in new[] { A.cArm, A.cFore, A.cHand }.Concat(A.fingers.SelectMany(x => x.c.Where(y => y)))) A.rest[o] = (o.position, o.rotation);
                Vector3 RP(Transform o) => A.rest[o].p;
                A.la = (RP(A.cFore) - RP(A.cArm)).magnitude; A.lb = (RP(A.cHand) - RP(A.cFore)).magnitude; A.armRest = RP(A.cArm);
                var ix = A.fingers.Find(x => x.name == "Index"); var pk = A.fingers.Find(x => x.name == "Little") ?? A.fingers.Find(x => x.name == "Ring");
                A.sRest = ix != null && pk != null ? RP(ix.c[0]) - RP(pk.c[0]) : Vector3.forward;
                A.handDirRest = (A.knuckle != null ? RP(A.knuckle.c[0]) : RP(A.cHand) - RP(A.cFore) + RP(A.cHand)) - RP(A.cHand);
                // eixo da dobradiça do cotovelo no repouso: o antebraço dobra para a FRENTE do corpo (+Z do personagem no repouso, raiz na identidade)
                A.n0 = Vector3.Cross(RP(A.cFore) - RP(A.cArm), Vector3.forward).normalized; if (A.n0.sqrMagnitude < .5f) A.n0 = Vector3.Cross(RP(A.cFore) - RP(A.cArm), Vector3.up).normalized;
                // só os triângulos do braço deste lado
                var keep = new HashSet<Transform>(A.ownHand ? new[] { A.cArm, A.cFore, A.cHand }.Concat(A.fingers.SelectMany(x => x.c.Where(y => y))) : new[] { A.cArm, A.cFore });
                // ossos-filho sem ser osso humano (pontas dos dedos, ossos de apoio) também contam como da mão
                if (A.ownHand) foreach (var t in A.cHand.GetComponentsInChildren<Transform>(true)) keep.Add(t);
                float lim = A.ownHand ? 1.5f : 1f; bool any = false;
                foreach (var rd in go.GetComponentsInChildren<Renderer>(true))
                {
                    rd.enabled = false; var smr = rd as SkinnedMeshRenderer; if (!smr || !smr.sharedMesh || !smr.sharedMesh.isReadable) continue;
                    var m = smr.sharedMesh; var bw = m.boneWeights; var bones = smr.bones; if (bw.Length != m.vertexCount) continue;
                    var w = new float[bw.Length];
                    for (int v = 0; v < bw.Length; v++) { var b = bw[v]; float s = 0; if (In(bones, b.boneIndex0, keep)) s += b.weight0; if (In(bones, b.boneIndex1, keep)) s += b.weight1; if (In(bones, b.boneIndex2, keep)) s += b.weight2; if (In(bones, b.boneIndex3, keep)) s += b.weight3; w[v] = s; }
                    var nm = Instantiate(m); nm.name = m.name + "_braco" + side; bool has = false;
                    for (int sm = 0; sm < m.subMeshCount; sm++)
                    {
                        var tri = m.GetTriangles(sm); var outT = new List<int>(tri.Length / 4);
                        for (int t = 0; t + 2 < tri.Length; t += 3) if (w[tri[t]] + w[tri[t + 1]] + w[tri[t + 2]] > lim) { outT.Add(tri[t]); outT.Add(tri[t + 1]); outT.Add(tri[t + 2]); }
                        nm.SetTriangles(outT, sm); has |= outT.Count > 0;
                    }
                    if (!has) { Destroy(nm); continue; }
                    smr.sharedMesh = nm; smr.enabled = true; smr.updateWhenOffscreen = true; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false; any = true;
                }
                if (!any) { Destroy(go); A.ok = false; continue; }
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBWorld.LFirstPerson;
                go.transform.SetParent(rig.transform, true); arms.Add(A);
            }
            ok = arms.Count == 2;
            if (!ok) return;
            // esconde as mangas genéricas do rig; as mãos genéricas só ficam quando o personagem não tem ossos dos dedos
            bool keepHands = arms.Any(a => !a.ownHand);
            foreach (var rd in rig.pitchNode.GetComponentsInChildren<Renderer>(true))
            {
                var nm = rd.name + " " + (rd.transform.parent ? rd.transform.parent.name : "");
                if (System.Text.RegularExpressions.Regex.IsMatch(nm, "Sleeves|Hands", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (keepHands && System.Text.RegularExpressions.Regex.IsMatch(nm, "Hands", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) { Tint(rd); continue; }
                    generic.Add(rd); rd.enabled = false;
                }
            }
        }
        // ---- tom da mão: mediana da textura do personagem nos vértices da mão × cor do material, dividida pela mediana da mão do LPSP
        Color? handTint; readonly List<(Material m, Color c)> tinted = new();
        static Color? Median(Material mat, Mesh mesh, System.Func<int, bool> keep)
        {
            if (!mat || !mesh || !mesh.isReadable) return null; var tex = mat.mainTexture; if (!tex) foreach (var n in mat.GetTexturePropertyNames()) if (System.Text.RegularExpressions.Regex.IsMatch(n, "base|main|albedo|diffuse", System.Text.RegularExpressions.RegexOptions.IgnoreCase) && mat.GetTexture(n)) { tex = mat.GetTexture(n); break; }
            if (!tex) return null; var uv = mesh.uv; if (uv == null || uv.Length != mesh.vertexCount) return null;
            const int N = 256; var rt = RenderTexture.GetTemporary(N, N, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); var prev = RenderTexture.active;
            Graphics.Blit(tex, rt); RenderTexture.active = rt; var t2 = new Texture2D(N, N, TextureFormat.RGBA32, false); t2.ReadPixels(new Rect(0, 0, N, N), 0, 0); t2.Apply(false); RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = t2.GetPixels32(); Destroy(t2); var r = new List<float>(); var g = new List<float>(); var b = new List<float>();
            for (int i = 0; i < uv.Length; i++) { if (!keep(i)) continue; float u = uv[i].x - Mathf.Floor(uv[i].x), v = uv[i].y - Mathf.Floor(uv[i].y); var c = px[Mathf.Clamp((int)(v * N), 0, N - 1) * N + Mathf.Clamp((int)(u * N), 0, N - 1)]; r.Add(c.r); g.Add(c.g); b.Add(c.b); }
            if (r.Count < 8) return null; r.Sort(); g.Sort(); b.Sort(); int k = r.Count / 2; Color mc = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.HasProperty("_Color") ? mat.color : Color.white;
            return new Color(r[k] / 255f * mc.r, g[k] / 255f * mc.g, b[k] / 255f * mc.b);
        }
        static Color? HandColor(GameObject go, Transform hand)
        {
            var hb = new HashSet<Transform>(hand.GetComponentsInChildren<Transform>(true));
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = smr.sharedMesh; if (!m || !m.isReadable) continue; var bones = smr.bones; var bw = m.boneWeights; if (bw.Length != m.vertexCount) continue;
                bool Keep(int i) { var w = bw[i]; int bi = w.boneIndex0; float x = w.weight0; if (w.weight1 > x) { bi = w.boneIndex1; x = w.weight1; } if (w.weight2 > x) { bi = w.boneIndex2; x = w.weight2; } if (w.weight3 > x) bi = w.boneIndex3; return bi >= 0 && bi < bones.Length && bones[bi] && hb.Contains(bones[bi]); }
                // submalha com mais vértices da mão
                int best = -1, bestN = 0; for (int sm = 0; sm < m.subMeshCount && sm < smr.sharedMaterials.Length; sm++) { var set = new HashSet<int>(m.GetTriangles(sm)); int n = 0; foreach (int i in set) if (Keep(i)) n++; if (n > bestN) { bestN = n; best = sm; } }
                if (best < 0) continue; var tri = new HashSet<int>(m.GetTriangles(best));
                var c = Median(smr.sharedMaterials[best], m, i => tri.Contains(i) && Keep(i)); if (c != null) return c;
            }
            return null;
        }
        void Tint(Renderer rd)
        {
            if (handTint == null) return; var smr = rd as SkinnedMeshRenderer; var mesh = smr ? smr.sharedMesh : rd.GetComponent<MeshFilter>()?.sharedMesh;
            var mat = rd.material; var baseC = Median(mat, mesh, _ => true); if (baseC == null) return; var bc = baseC.Value; var hc = handTint.Value;
            Color orig = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            var k = new Color(Mathf.Clamp(hc.r / Mathf.Max(.02f, bc.r), .15f, 1.6f), Mathf.Clamp(hc.g / Mathf.Max(.02f, bc.g), .15f, 1.6f), Mathf.Clamp(hc.b / Mathf.Max(.02f, bc.b), .15f, 1.6f), 1);
            var nc = new Color(orig.r * k.r, orig.g * k.g, orig.b * k.b, orig.a); if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", nc); if (mat.HasProperty("_Color")) mat.SetColor("_Color", nc); tinted.Add((mat, orig));
            Debug.Log($"[FreeBala][fp] mão do rig tingida: personagem {hc} · rig {bc} · fator {k}");
        }
        static bool In(Transform[] bones, int i, HashSet<Transform> keep) => i >= 0 && i < bones.Length && bones[i] && keep.Contains(bones[i]);
        public void Dispose() { foreach (var a in arms) if (a.root) Destroy(a.root.gameObject); arms.Clear(); foreach (var r in generic) if (r) r.enabled = true; generic.Clear(); foreach (var (m, c) in tinted) if (m) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c); } tinted.Clear(); }
        void OnDestroy() => Dispose();

        static Quaternion Basis(Vector3 dir, Vector3 sec)
        {
            var x = dir.normalized; var z = sec - x * Vector3.Dot(sec, x); if (z.sqrMagnitude < 1e-10f) z = Vector3.up - x * x.y; z.Normalize(); var y = Vector3.Cross(z, x);
            return Quaternion.LookRotation(z, y);
        }
        static Quaternion FrameRot(Vector3 dRest, Vector3 sRest, Vector3 dNow, Vector3 sNow) => Basis(dNow, sNow) * Quaternion.Inverse(Basis(dRest, sRest));
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do LateUpdate dos braços em 1ª pessoa
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBCharArms.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!ok || !rig) return; bool vis = rig.pitchNode && rig.pitchNode.gameObject.activeInHierarchy;
            foreach (var a in arms) { a.root.gameObject.SetActive(vis); if (vis) Step(a); }
        }
        // ROLAGEM DO BRAÇO (rodada 42, usuário: "ao segurar determinadas armas, os braços ficam completamente deformados, com cotovelos,
        // pulsos e ombros em posições absurdas ... corrija a origem"). A versão antiga orientava braço E antebraço pela linha dos nós dos
        // dedos (indicador -> mínimo) da MÃO do rig: cada giro do punho na arma (palma para cima no guarda-mão do fuzil, de lado na pistola,
        // na bomba da escopeta) girava o braço inteiro desde o ombro — "papel de bala" no ombro e no cotovelo, cotovelo apontando para fora.
        // Agora é a anatomia: braço e antebraço giram pela DOBRADIÇA do cotovelo (normal do plano braço×antebraço, a mesma nos dois ossos —
        // nada torce no cotovelo); a pronação do punho fica metade no antebraço (rádio/ulna) e o resto na mão, que segue o rig por inteiro.
        // -fbfpold liga a versão antiga (comparação no FBFpLab).
        public static bool Legacy;
        public string TwistReport() => string.Join(" · ", arms.Select((a, i) => $"{(i == 0 ? "esq" : "dir")}: braço {a.twistArm:0}° antebraço {a.twistFore:0}° da dobradiça (pronação do punho {a.pron:0}°)"));
        static void Step(Arm A)
        {
            Vector3 S = A.fArm.position, E = A.fFore.position, W = A.fHand.position;
            if (A.scale <= 0) A.scale = ((S - E).magnitude + (E - W).magnitude) / (A.la + A.lb);
            float s = A.scale, a = A.la * s, b = A.lb * s;
            var fx = A.fingers.Find(x => x.name == "Index"); var fp = A.fingers.Find(x => x.name == "Little") ?? A.fingers.Find(x => x.name == "Ring");
            var sNow = fx != null && fp != null ? fx.f[0].position - fp.f[0].position : Vector3.forward;
            var K = A.knuckle != null ? A.knuckle.f[0].position : W + (W - E);
            var D = W - S; float d = Mathf.Min(D.magnitude, a + b - 1e-4f); D.Normalize();
            var pole = E - S; pole -= D * Vector3.Dot(pole, D); if (pole.sqrMagnitude < 1e-8f) pole = Vector3.Cross(sNow, D); pole.Normalize();
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1);
            var El = S + D * (a * cosA) + pole * (a * Mathf.Sqrt(1 - cosA * cosA)); var Wl = S + D * d;
            A.root.localScale = A.baseScale * s; A.root.rotation = Quaternion.identity; A.root.position = S - A.armRest * s;
            Vector3 RP(Transform o) => A.rest[o].p;
            void Put(Transform bone, Vector3 dRest, Vector3 dNow) => bone.rotation = FrameRot(dRest, A.sRest, dNow, sNow) * A.rest[bone].q;
            var restU = RP(A.cFore) - RP(A.cArm); var restF = RP(A.cHand) - RP(A.cFore); Vector3 dU = El - S, dF = Wl - El;
            // dobradiça agora: plano do cotovelo calculado; reto (sem plano) -> o do rig; reto também -> a do quadro anterior
            var n = Vector3.Cross(dU, dF); if (n.sqrMagnitude < 1e-6f * dU.sqrMagnitude * dF.sqrMagnitude) n = Vector3.Cross(E - S, W - E);
            if (n.sqrMagnitude < 1e-8f) n = A.lastN.sqrMagnitude > .5f ? A.lastN : Vector3.Cross(D, sNow);
            n.Normalize(); A.lastN = n;
            var qU = FrameRot(restU, A.n0, dU, n) * A.rest[A.cArm].q;                 // braço: só a dobradiça (ombro não torce com o punho)
            var qF0 = FrameRot(restF, A.n0, dF, n);                                   // antebraço neutro (alinhado ao cotovelo)
            var qH = FrameRot(A.handDirRest, A.sRest, K - W, sNow);                   // mão: segue o rig por inteiro
            // pronação = quanto a mão gira em torno do antebraço além do neutro; metade vai para o antebraço
            var axis = dF.normalized; var sPred = qF0 * A.sRest; var a1 = Vector3.ProjectOnPlane(sPred, axis); var a2 = Vector3.ProjectOnPlane(qH * A.sRest, axis);
            float pron = a1.sqrMagnitude > 1e-8f && a2.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(a1, a2, axis) : 0;
            var qF = Quaternion.AngleAxis(Mathf.Clamp(pron * .5f, -90, 90), axis) * qF0 * A.rest[A.cFore].q;
            // diagnóstico: quanto a versão antiga torcia cada osso em relação a esta
            var qUold = FrameRot(restU, A.sRest, dU, sNow) * A.rest[A.cArm].q; var qFold = FrameRot(restF, A.sRest, dF, sNow) * A.rest[A.cFore].q;
            A.twistArm = Quaternion.Angle(qUold, qU); A.twistFore = Quaternion.Angle(qFold, qF); A.pron = pron;
            if (Legacy) { Put(A.cArm, restU, dU); Put(A.cFore, restF, dF); }
            else { A.cArm.rotation = qU; A.cFore.rotation = qF; }
            Put(A.cHand, A.handDirRest, K - W);
            foreach (var F in A.fingers)
            {
                var j = F.f.Select(o => o.position).ToList(); j.Add(j[2] + (j[2] - j[1]) * .85f);
                for (int k = 0; k < 3; k++)
                {
                    var cb = F.c[k]; var cn = F.c[k + 1]; if (!cb) continue;
                    var dRest = cn ? RP(cn) - RP(cb) : RP(cb) - RP(F.c[k - 1]);
                    Put(cb, dRest, j[k + 1] - j[k]);
                }
            }
        }
    }
}
