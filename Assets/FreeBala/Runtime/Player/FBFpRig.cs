// Primeira pessoa (porte 1:1 do fp.js) — réplica do personagem do jogo original (Infima "Low Poly Shooter Pack"): AnimatorController
// AC_LPSP_PCH com camadas override/aditivas (locomoção em 4 direções + mira, corrida, tiro, ferrolho, recarga normal/vazia/cíclica,
// inspeção, sacar/guardar, granada, faca, transições de mira/corrida/agachar, sprint tático), animator da arma (AC_LPSP_WEP, como DIFERENÇA
// sobre o repouso do modelo), IK dos braços até os efetuadores (CharacterKinematics) e motions procedurais com as springs e curvas extraídas
// (recuo por tiro da rajada, pulo/queda/pouso, inclinar, offsets do item por estado, offset da luneta) + alinhamento da mira no ADS.
// Os clipes são Generic (trilhas por osso) — tocados por este sampler próprio, exatamente como no web, sobre o rig fp_arms.glb.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public class FBFpRig : MonoBehaviour
    {
        // ---------------------------------------------------------------- dados
        static Dictionary<string, object> J; static readonly Dictionary<string, Dictionary<string, object>> raw = new();
        // [parceiro] v18 (thread de física): o wanims.json tem ~12 MB. Num exe de teste o TextAsset.text deu OutOfMemoryException dentro
        // do FBPlayer.Awake, o J ficou pela metade e o jogador ficou parado, com a câmera embaixo do mapa. Agora o J só é
        // guardado quando os dois arquivos carregam; na falta de memória tenta de novo depois de um GC e, se falhar, o J fica
        // nulo (a próxima criação tenta outra vez) e o jogo segue sem os braços de primeira pessoa.
        static bool LoadJ()
        {
            if (J != null) return true;
            var j = new Dictionary<string, object>();
            foreach (var n in new[] { "FP/wanims", "FP/fp_anims" })
            {
                Dictionary<string, object> d = null;
                for (int tentativa = 0; tentativa < 2; tentativa++)
                {
                    try { var ta = Resources.Load<TextAsset>(n); d = ta ? Json.Parse(ta.text) as Dictionary<string, object> : null; break; }
                    catch (System.OutOfMemoryException)
                    {
                        Debug.LogWarning("[FreeBala][fp] faltou memória lendo " + n + (tentativa == 0 ? ": tentando de novo depois do GC" : ": seguindo sem os braços de primeira pessoa"));
                        System.GC.Collect(); System.GC.WaitForPendingFinalizers();
                        if (tentativa == 1) return false;
                    }
                }
                if (d == null) continue;
                foreach (var kv in d) j[kv.Key] = kv.Value;
            }
            J = j; return true;
        }
        static string San(string n) { var sb = new System.Text.StringBuilder(); foreach (var c in n) { if (c == ' ') sb.Append('_'); else if ("[].:/".IndexOf(c) < 0) sb.Append(c); } return sb.ToString(); }
        static float EvalCurve(List<object> k, float t)
        {
            if (k == null || k.Count == 0) return 0; float K(int i, int j) => Json.Fv((k[i] as List<object>)[j]);
            if (t <= K(0, 0)) return K(0, 1); int L = k.Count - 1; if (t >= K(L, 0)) return K(L, 1);
            int i = 0; while (i < L - 1 && t > K(i + 1, 0)) i++;
            float t0 = K(i, 0), v0 = K(i, 1), o0 = K(i, 3), t1 = K(i + 1, 0), v1 = K(i + 1, 1), i1 = K(i + 1, 2), dt = t1 - t0;
            if (!float.IsFinite(o0) || !float.IsFinite(i1) || Mathf.Abs(o0) > 1e29f || Mathf.Abs(i1) > 1e29f) return v0;
            float s = (t - t0) / dt, s2 = s * s, s3 = s2 * s; return (2 * s3 - 3 * s2 + 1) * v0 + (s3 - 2 * s2 + s) * dt * o0 + (-2 * s3 + 3 * s2) * v1 + (s3 - s2) * dt * i1;
        }
        class Spring
        {
            public float damping = 20, stiffness = 200, mass = 1, speed = 1; public Vector3 x, v;
            public void Set(Dictionary<string, object> s) { if (s == null) return; damping = Json.F(s, "damping", 20); stiffness = Json.F(s, "stiffness", 200); mass = Json.F(s, "mass", 1); speed = Json.F(s, "speed", 1); }
            public Vector3 Update(Vector3 target, float dt) { float h = dt * speed; while (h > 1e-6f) { float st = Mathf.Min(h, 1 / 240f); h -= st; var a = ((x - target) * -stiffness - v * damping) / mass; v += a * st; x += v * st; } return x; }
        }
        class CurveMotion
        {
            Dictionary<string, object> ac; public readonly Spring sl = new(), sr = new(); public Vector3 tl, tr;
            public void Set(Dictionary<string, object> a) { ac = a; sl.Set(Json.O(a, "locationSpring")); sr.Set(Json.O(a, "rotationSpring")); }
            public void Target(float t) { if (ac == null) { tl = tr = Vector3.zero; return; } var lc = Json.L(ac, "locationCurves"); var rc = Json.L(ac, "rotationCurves"); float lm = Json.F(ac, "locationMultiplier", 1), rm = Json.F(ac, "rotationMultiplier", 1); tl = new Vector3(EvalCurve(lc?[0] as List<object>, t), EvalCurve(lc?[1] as List<object>, t), EvalCurve(lc?[2] as List<object>, t)) * lm; tr = new Vector3(EvalCurve(rc?[0] as List<object>, t), EvalCurve(rc?[1] as List<object>, t), EvalCurve(rc?[2] as List<object>, t)) * rm; }
            public void Zero() { tl = tr = Vector3.zero; }
            public (Vector3, Vector3) Update(float dt) => (sl.Update(tl, dt), sr.Update(tr, dt));
        }
        class Pose { public Vector3[] P; public Quaternion[] Q; public Vector3[] S; public Pose(int n) { P = new Vector3[n]; Q = new Quaternion[n]; S = new Vector3[n]; } public void CopyFrom(Pose o) { System.Array.Copy(o.P, P, P.Length); System.Array.Copy(o.Q, Q, Q.Length); System.Array.Copy(o.S, S, S.Length); } }
        class GClip
        {
            public string name, refName; public float len, fps; public bool loop; public readonly List<(int bi, char k, float[] d, int w)> tracks = new();
            public GClip(string n, Dictionary<string, object> c, System.Func<string, int> index)
            {
                name = n; len = Json.F(c, "len"); fps = Json.F(c, "fps", 30); loop = Json.B(c, "loop"); refName = Json.S(c, "ref");
                foreach (var to in Json.L(c, "t") ?? new List<object>())
                {
                    int bi = index(Json.S(to, "p")); if (bi < 0) continue; char k = (Json.S(to, "k") ?? "p")[0]; var d = Json.Floats(Json.L(to, "d")); int w = k == 'q' ? 4 : 3;
                    // three/glTF -> Unity (glTFast espelha X): posição (-x,y,z); rotação (x,-y,-z,w)
                    if (k == 'p') for (int i = 0; i < d.Length; i += 3) d[i] = -d[i]; else if (k == 'q') for (int i = 0; i < d.Length; i += 4) { d[i + 1] = -d[i + 1]; d[i + 2] = -d[i + 2]; }
                    tracks.Add((bi, k, d, w));
                }
            }
            public void Sample(float t, Pose pose)
            {
                t = loop ? ((t % len) + len) % len : Mathf.Clamp(t, 0, len); float f = t * fps;
                foreach (var (B, k, d, w) in tracks)
                {
                    int nf = d.Length / w; float fi = Mathf.Min(f, nf - 1); int i0 = (int)fi, i1 = Mathf.Min(i0 + 1, nf - 1); float a = fi - i0; int o0 = i0 * w, o1 = i1 * w;
                    if (k == 'q') { var q0 = new Quaternion(d[o0], d[o0 + 1], d[o0 + 2], d[o0 + 3]); var q1 = new Quaternion(d[o1], d[o1 + 1], d[o1 + 2], d[o1 + 3]); pose.Q[B] = Quaternion.Slerp(q0, q1, a); }
                    else { var v = Vector3.Lerp(new Vector3(d[o0], d[o0 + 1], d[o0 + 2]), new Vector3(d[o1], d[o1 + 1], d[o1 + 2]), a); if (k == 'p') pose.P[B] = v; else pose.S[B] = v; }
                }
            }
        }
        class Entry { public string s; public float t, w = 1, r, speed = 1; public bool end, bolted; }
        class Layer
        {
            public List<Entry> list; public Layer(string s) { list = new List<Entry> { new Entry { s = s } }; }
            public Entry cur => list[^1];
            public void Go(string s, float dur, float t0 = 0) { if (dur <= 0) { list = new List<Entry> { new Entry { s = s, t = t0 } }; return; } foreach (var e in list) e.r = -1 / dur; list.Add(new Entry { s = s, t = t0, w = 0, r = 1 / dur }); }
            public void Step(float dt) { foreach (var e in list) e.w = Mathf.Clamp01(e.w + e.r * dt); list.RemoveAll(e => !(e.w > 0 || e.r > 0)); if (list.Count == 0) list.Add(new Entry { s = "none" }); }
        }
        static readonly Dictionary<string, string> PCH = new()
        {
            ["idlePose"] = "A_FP_PCH_AR_01_Idle_Pose", ["aimPose"] = "A_FP_PCH_AR_01_Aim_Pose", ["idle"] = "A_FP_PCH_Idle", ["aimIdle"] = "A_FP_PCH_Aim_Idle", ["walkF"] = "A_FP_PCH_Walk_F_Additive", ["walkB"] = "A_FP_PCH_Walk_B_Additive",
            ["walkL"] = "A_FP_PCH_Walk_L_Additive", ["walkR"] = "A_FP_PCH_Walk_R_Additive", ["aimWalkF"] = "A_FP_PCH_Aim_Walk_F_Additive", ["walkPose"] = "A_FP_PCH_Walk_F_Pose_Additive", ["run"] = "A_FP_PCH_Run_02_Additive",
            ["fire"] = "A_FP_PCH_AR_Fire", ["aimFire"] = "A_FP_PCH_AR_Aim_Fire", ["fireEmpty"] = "A_FP_PCH_Fire_Empty", ["bolt"] = "A_FP_PCH_Sniper_01_Reload_Bolt", ["boltAim"] = "A_FP_PCH_Sniper_01_Reload_Bolt_Aim",
            ["reload"] = "A_FP_PCH_AR_01_Reload", ["aimReload"] = "A_FP_PCH_AR_01_Aim_Reload", ["reloadEmpty"] = "A_FP_PCH_AR_01_Reload_Empty", ["aimReloadEmpty"] = "A_FP_PCH_AR_01_Aim_Reload_Empty",
            ["rOpen"] = "A_FP_PCH_Sniper_01_Reload_Open", ["rInsert"] = "A_FP_PCH_Sniper_01_Reload_Insert_Additive", ["rClose"] = "A_FP_PCH_Sniper_01_Reload_Close", ["inspect"] = "A_FP_PCH_AR_01_Inspect",
            ["holster"] = "A_FP_PCH_AR_01_Holster", ["unholster"] = "A_FP_PCH_AR_01_Unholster", ["grenadeAdd"] = "A_FP_PCH_Grenade_Throw_Additive", ["grenade"] = "A_FP_PCH_Grenade_Throw", ["knife"] = "A_FP_PCH_Knife_Attack_01",
            ["aimIn"] = "A_FP_PCH_Aim_In_Transition", ["aimOut"] = "A_FP_PCH_Aim_Out_Transition", ["tactical"] = "A_FP_PCH_AG14W_Sprint_Tactical_F_Loop_Aligned",
        };
        static readonly Dictionary<string, string> WEP = new() { ["fire"] = "A_WEP_AR_01_Fire", ["reload"] = "A_FP_WEP_AR_01_Reload", ["reloadEmpty"] = "A_FP_WEP_AR_01_Reload_Empty", ["bolt"] = "A_FP_WEP_Sniper_01_Reload_Bolt", ["boltAim"] = "A_FP_WEP_Sniper_01_Reload_Bolt_Aim", ["rOpen"] = "A_FP_WEP_Sniper_01_Reload_Open", ["rInsert"] = "A_FP_WEP_Sniper_01_Reload_Insert", ["rClose"] = "A_FP_WEP_Sniper_01_Reload_Close" };

        // ---------------------------------------------------------------- rig
        public Transform pitchNode, motionNode, camNode, inv; Transform[] A; Dictionary<string, int> Amap; Pose rest, pose, tmp, tmp2, dT; int[] bonesA;
        readonly Dictionary<string, GClip> clips = new(); Dictionary<string, object> over = new(); readonly Dictionary<string, Pose> refs = new();
        float aiming, h, v, movement, turning, leanFwd, leanP, rateF = 1, rateB = .9f, rateS = .9f, playRate = 1; bool aimSt, runSt, crouchSt;
        readonly Layer loco = new("loco"), overlay = new("none"), actions = new("none"), holsterL = new("unholster"), armL = new("none"), armR = new("none"), wAim = new("none"), wRun = new("none"), wCrouch = new("none"), tac = new("none");
        GameObject weapon; WeaponDef def; string wid; Transform[] W; Dictionary<string, int> Wmap; Pose wrest, wpose; readonly Dictionary<string, GClip> wclips = new(); int[] bonesW; (GClip c, float t)? weaponAnim; readonly Dictionary<GClip, Pose> wref = new();
        Dictionary<string, object> IA = new(); Transform[] armLb, armRb; Transform effL, effR, knifeMesh; Transform sight, sightSock, pipNode; Dictionary<string, object> scope; Transform muzzleT; Camera pipCam; RenderTexture pipRT; bool ironSight;
        GameObject fpFlash; float fpFlashTimer; static Material fpFlashMat; static Mesh fpFlashMesh;
        readonly CurveMotion recoil = new(), jump = new(), land = new(), fall = new(), leanItem = new(), leanCam = new(); int shots; float sinceShot = 9, jumpT = 9, landT = 9, airT; bool wasAir;
        readonly Spring offL = new(), offR = new(), scopeL = new() { damping = 25, stiffness = 300 }; Vector3 motionBase; Quaternion motionBaseQ; float camH; public Vector3 camLeanLoc, camLeanRot; public Quaternion recoilQ = Quaternion.identity;
        public bool ok; public readonly List<string> events = new();
        static Dictionary<string, object> D => GameData.FP;

        public static FBFpRig Create(Transform parent)
        {
            // [parceiro] v18: sem memória para o wanims o LoadJ devolve false (J nulo) e seguimos sem os braços de 1ª pessoa
            bool jOk = LoadJ(); var prefab = FBRegistry.I ? FBRegistry.I.fpArms : null; if (!jOk || !prefab || J == null || J.Count == 0) return null;
            var root = new GameObject("FP_Rig"); root.transform.SetParent(parent, false); var r = root.AddComponent<FBFpRig>(); r.Build(Instantiate(prefab, root.transform)); return r.ok ? r : null;
        }
        void Build(GameObject arms)
        {
            pitchNode = arms.transform; float H = Json.F(Json.O(D, "controller"), "m_Height", 1.8f); pitchNode.localPosition = new Vector3(0, H, 0); camH = H;
            (A, Amap) = Index(arms.transform);
            Transform Get(string n) => Amap.TryGetValue(San(n), out var i) ? A[i] : null;
            motionNode = Get("Character Root Motion"); camNode = Get("Camera"); inv = Get("P_LPSP_Inventory"); knifeMesh = Get("SM_Knife_01"); var radio = Get("RadioSet");
            if (knifeMesh) knifeMesh.gameObject.SetActive(false); if (radio) radio.gameObject.SetActive(false);
            if (!motionNode || !camNode || !inv) { Debug.LogWarning("[FreeBala] rig FP incompleto"); return; }
            foreach (var rd in arms.GetComponentsInChildren<Renderer>(true)) { rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rd.gameObject.layer = FBWorld.LFirstPerson; }
            int n = A.Length; rest = new Pose(n); pose = new Pose(n); tmp = new Pose(n); tmp2 = new Pose(n); dT = new Pose(n); Read(A, rest);
            bonesA = J.Keys.Where(k => k.StartsWith("A_FP_PCH")).SelectMany(k => Clip(k)?.tracks.Select(t => t.bi) ?? Enumerable.Empty<int>()).Distinct().ToArray();
            armLb = new[] { Get("upperarm_l"), Get("lowerarm_l"), Get("hand_l") }; armRb = new[] { Get("upperarm_r"), Get("lowerarm_r"), Get("hand_r") }; effL = Get("Effector Hand Left"); effR = Get("Effector Hand Right");
            var F = Json.O(Json.O(D, "itemFeel"), "standing"); recoil.Set(Json.O(Json.O(D, "weaponRecoil"), "standingState")); jump.Set(Json.O(F, "jumpingCurves")); land.Set(Json.O(F, "landingCurves")); fall.Set(Json.O(F, "fallingCurves"));
            leanItem.Set(Json.O(Json.O(D, "leaning"), "itemStanding")); leanCam.Set(Json.O(Json.O(D, "leaning"), "cameraStanding"));
            motionBase = motionNode.localPosition; motionBaseQ = motionNode.localRotation; ok = true;
        }
        static (Transform[], Dictionary<string, int>) Index(Transform root) { var l = new List<Transform>(); var m = new Dictionary<string, int>(); foreach (var t in root.GetComponentsInChildren<Transform>(true)) { if (t == root) continue; var k = San(t.name); if (!m.ContainsKey(k)) { m[k] = l.Count; l.Add(t); } } return (l.ToArray(), m); }
        static void Read(Transform[] I, Pose p) { for (int i = 0; i < I.Length; i++) { p.P[i] = I[i].localPosition; p.Q[i] = I[i].localRotation; p.S[i] = I[i].localScale; } }
        static void Write(Transform[] I, Pose p, int[] bones) { foreach (var i in bones) { var t = I[i]; t.localPosition = p.P[i]; t.localRotation = p.Q[i]; t.localScale = p.S[i]; } }
        GClip Clip(string n) { if (n == null) return null; if (!clips.TryGetValue(n, out var c)) { if (!(J.TryGetValue(n, out var j) && j is Dictionary<string, object> d)) return null; clips[n] = c = new GClip(n, d, p => Amap.TryGetValue(p, out var i) ? i : -1); } return c; }
        GClip C(string key) { var b = PCH[key]; var o = Json.S(over, b); return (o != null ? Clip(o) : null) ?? Clip(b); }
        Pose Ref(string key)
        {
            var b = PCH[key]; var k = b + "|" + wid; if (refs.TryGetValue(k, out var r)) return r;
            var c = C(key); var rb = J.TryGetValue(b, out var jo) ? Json.S(jo, "ref") : null; bool overridden = Json.S(over, b) != null && Clip(Json.S(over, b)) != null; r = new Pose(A.Length); r.CopyFrom(rest);
            if (rb == "__end") c?.Sample(c.len, r);
            else if (rb != null) { var rk = PCH.FirstOrDefault(x => x.Value == rb).Key; var rc = overridden && rk != null ? C(rk) : Clip(rb) ?? (rk != null ? C(rk) : null); rc?.Sample(0, r); }
            else c?.Sample(0, r);
            return refs[k] = r;
        }

        static Mesh FpFlashMesh()
        {
            if (fpFlashMesh) return fpFlashMesh;
            var m = new Mesh { name = "fp_flash_mesh" };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int k = v.Count;
                v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3, k, k + 2, k + 1, k, k + 3, k + 2 });
            }
            // Front star facing camera
            Quad(new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0));
            // Cross quads along Z barrel
            Quad(new Vector3(-.35f, 0, 0), new Vector3(.35f, 0, 0), new Vector3(.35f, 0, .85f), new Vector3(-.35f, 0, .85f));
            Quad(new Vector3(0, -.35f, 0), new Vector3(0, .35f, 0), new Vector3(0, .35f, .85f), new Vector3(0, -.35f, .85f));
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return fpFlashMesh = m;
        }

        static Material FpFlashMat()
        {
            if (fpFlashMat) return fpFlashMat;
            var mat = new Material(FBShaders.Unlit ?? Shader.Find("Universal Render Pipeline/Unlit")) { name = "fp_flash_mat" };
            var tex = FBFx.FlashTex();
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(1f, 0.92f, 0.72f, 1f));
            mat.SetFloat("_Surface", 1);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3200;
            mat.SetFloat("_Blend", 2);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0);
            mat.SetFloat("_Cull", 0);
            return fpFlashMat = mat;
        }

        void SetupFlash()
        {
            if (fpFlash) Destroy(fpFlash);
            var parent = muzzleT ? muzzleT : inv;
            fpFlash = new GameObject("FP_MuzzleFlash");
            fpFlash.transform.SetParent(parent, false);
            fpFlash.transform.localPosition = Vector3.zero;
            fpFlash.transform.localRotation = Quaternion.identity;
            fpFlash.layer = FBWorld.LFirstPerson;
            var mf = fpFlash.AddComponent<MeshFilter>();
            mf.sharedMesh = FpFlashMesh();
            var mr = fpFlash.AddComponent<MeshRenderer>();
            mr.sharedMaterial = FpFlashMat();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            fpFlash.SetActive(false);
        }

        // ---- troca de arma
        public void SetWeapon(WeaponDef d)
        {
            if (weapon) Destroy(weapon); def = d; ironSight = false; sightSock = pipNode = null; wid = d.id; refs.Clear(); weaponAnim = null; wref.Clear();
            weapon = FBWeaponModel.Make(d, inv); weapon.transform.localPosition = Vector3.zero; weapon.transform.localRotation = Quaternion.identity; weapon.transform.localScale = Vector3.one;
            foreach (var r in weapon.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.gameObject.layer = FBWorld.LFirstPerson; }
            var raw = Json.O(GameData.WeaponsRaw, d.id); over = Json.O(Json.O(raw, "pch"), "map") ?? new Dictionary<string, object>();
            (W, Wmap) = Index(weapon.transform); wrest = new Pose(W.Length); wpose = new Pose(W.Length); Read(W, wrest);
            wclips.Clear(); var wmap = Json.O(Json.O(raw, "wep"), "map");
            foreach (var kv in WEP) { var n = Json.S(wmap, kv.Value) ?? kv.Value; if (J.TryGetValue(n, out var jo) && jo is Dictionary<string, object> dd) wclips[kv.Key] = new GClip(n, dd, p => Wmap.TryGetValue(p, out var i) ? i : -1); }
            bonesW = wclips.Values.SelectMany(c => c.tracks.Select(t => t.bi)).Distinct().ToArray();
            var ia = Json.O(raw, "itemAnim"); IA = new Dictionary<string, object> { ["itemOffsets"] = Json.O(ia, "itemOffsets") ?? Json.O(D, "itemOffsets"), ["leaning"] = Json.O(ia, "leaningData") ?? Json.O(D, "leaning"), ["weaponRecoil"] = Json.O(ia, "weaponRecoilData") ?? Json.O(D, "weaponRecoil") };
            scope = FBWeaponModel.Scope(d); sight = null; muzzleT = null;
            var att = Json.O(raw, "att"); var c2 = FBWeaponModel.Of(d);
            bool OnW(Transform t) { for (; t && t != weapon.transform; t = t.parent) if (!t.gameObject.activeSelf) return false; return true; }
            // se houver acessório de cano ativo (silenciador, freio), pega o soquete ou ponta dele
            if (c2.muzzle >= 0)
            {
                var mzn = Json.S(Json.L(att, "muzzles")?[c2.muzzle], "node");
                if (!string.IsNullOrEmpty(mzn))
                {
                    foreach (var t in weapon.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == mzn && OnW(t))
                        {
                            foreach (var st in t.GetComponentsInChildren<Transform>(true))
                                if (st.name.StartsWith("SOCKET_Muzzle")) { muzzleT = st; break; }
                            if (muzzleT == null) muzzleT = t;
                            break;
                        }
                    }
                }
            }
            if (muzzleT == null)
            {
                foreach (var t in weapon.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("SOCKET_Muzzle") && OnW(t)) { muzzleT = t; break; }
            }
            if (c2.scope >= 0) { var sn = Json.S(Json.L(att, "scopes")?[c2.scope], "node"); foreach (var t in weapon.GetComponentsInChildren<Transform>(true)) if (t.name == sn) sight = t; }
            else if ((Json.S(Json.O(raw, "pch"), "name") ?? "").Contains("Sniper")) { var sd = Json.S(att, "scopeDefault"); foreach (var t in weapon.GetComponentsInChildren<Transform>(true)) if (t.name == sd && OnW(t)) sight = t; }
            else { var sd0 = Json.S(att, "scopeDefault"); foreach (var t in weapon.GetComponentsInChildren<Transform>(true)) if (t.name == sd0 && OnW(t)) { sight = t; ironSight = true; } }   // r45: mira de ferro também alinha
            SightSetup();
            SetupFlash();
            actions.Go("none", 0); overlay.Go("none", 0); Unholster();
            var me = GetComponentInParent<FBCharacter>() ?? FBMatch.Player; if (me) FBLaser.Bind(me, weapon, d, true);
        }
        public Transform Muzzle => muzzleT ? muzzleT : inv;
        bool Pistol => def != null && def.cls == "pistola";
        // ---- eventos
        public void Fire(bool empty, bool automatic)
        {
            if (empty) { overlay.Go("fireEmpty", 0, 0); return; }
            overlay.Go("fire", 0, 0);
            weaponAnim = wclips.TryGetValue("fire", out var c) ? (c, 0) : null;
            shots = automatic ? Mathf.Min(shots + 1, 3) : 1;
            sinceShot = 0;
            if (fpFlash)
            {
                bool silenced = def != null && FBWeaponSound.Silenced(def);
                float sz = def != null && def.IsSniper ? .24f : def != null && def.cls == "escopeta" ? .22f : def != null && def.IsPistol ? .12f : .16f;
                if (silenced) sz *= .42f;
                fpFlash.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0, 360f));
                fpFlash.transform.localScale = Vector3.one * sz;
                fpFlash.SetActive(true);
                fpFlashTimer = silenced ? .035f : .048f;
            }
        }
        public float Reload(bool empty) { actions.Go(empty ? "reloadEmpty" : "reload", 0, 0); if (wclips.TryGetValue(empty ? "reloadEmpty" : "reload", out var c)) weaponAnim = (c, 0); return C(empty ? "reloadEmpty" : "reload")?.len ?? 2; }
        int cycLeft; System.Action onInsert;
        public void ReloadCycled(int count, System.Action ins) { cycLeft = count; onInsert = ins; actions.Go("rOpen", 0, 0); WAnim("rOpen"); }
        public void StopCycled() { cycLeft = 0; }
        void WAnim(string k) { if (wclips.TryGetValue(k, out var c)) weaponAnim = (c, 0); }
        public void Inspect() { if (actions.cur.s == "none") actions.Go("inspect", 0, 0); }
        public void Unholster() => holsterL.Go("unholster", 0, 0);
        public float Holster() { holsterL.Go("holster", 0, 0); return C("holster")?.len ?? .5f; }
        public bool Busy => actions.cur.s != "none" || armL.cur.s != "none";
        public string ActionState => actions.cur.s;

        // ---- poses
        static void LerpPose(Pose dst, Pose src, float w, int[] bones) { foreach (var b in bones) { dst.P[b] = Vector3.Lerp(dst.P[b], src.P[b], w); dst.S[b] = Vector3.Lerp(dst.S[b], src.S[b], w); dst.Q[b] = Quaternion.Slerp(dst.Q[b], src.Q[b], w); } }
        static void MakeDelta(Pose o, Pose p, Pose r, int[] bones) { foreach (var b in bones) { o.P[b] = p.P[b] - r.P[b]; o.Q[b] = Quaternion.Inverse(r.Q[b]) * p.Q[b]; } }
        static void ZeroDelta(Pose d, int[] bones) { foreach (var b in bones) { d.P[b] = Vector3.zero; d.Q[b] = Quaternion.identity; } }
        static void AccDelta(Pose acc, Pose d, float w, int[] bones) { if (w <= 0) return; foreach (var b in bones) { acc.P[b] += d.P[b] * w; acc.Q[b] = acc.Q[b] * Quaternion.Slerp(Quaternion.identity, d.Q[b], w); } }
        static void ApplyDelta(Pose p, Pose d, float w, int[] bones) { if (w <= 0) return; foreach (var b in bones) { p.P[b] += d.P[b] * w; p.Q[b] = p.Q[b] * Quaternion.Slerp(Quaternion.identity, d.Q[b], w); } }
        void AddClip(string key, float t, float w) { if (w < .001f) return; var c = C(key); if (c == null) return; tmp.CopyFrom(rest); c.Sample(t, tmp); MakeDelta(tmp2, tmp, Ref(key), bonesA); AccDelta(dT, tmp2, w, bonesA); }
        float Len(string k) => C(k)?.len ?? 1;
        Pose StateDelta(Entry e, float dt)
        {
            ZeroDelta(dT, bonesA); string s = e.s;
            float Once(string key, float speed = 1) { e.t += dt * speed; if (e.t >= Len(key)) e.end = true; return e.t; }
            if (s == "loco")
            {
                // pistola: a pose-base é a de mira (ver Tick), então o balanço de locomoção também é o da mira — o aditivo de repouso do LPSP,
                // somado à pose de mira, entortava o revólver 38/RC425 e soltava a mão esquerda
                float wA = Pistol ? 1 : aiming, wN = 1 - wA, mag = Mathf.Min(1, new Vector2(h, v).magnitude); var dir = new Dictionary<string, float> { ["F"] = Mathf.Max(0, v), ["B"] = Mathf.Max(0, -v), ["L"] = Mathf.Max(0, -h), ["R"] = Mathf.Max(0, h) };
                float ds = dir.Values.Sum(); if (ds <= 0) ds = 1; foreach (var k in dir.Keys.ToList()) dir[k] = dir[k] / ds * mag;
                float cW = 1 - mag, turnW = Mathf.Min(1, turning); var walk = new Dictionary<string, string> { ["F"] = "walkF", ["B"] = "walkB", ["L"] = "walkL", ["R"] = "walkR" }; var rate = new Dictionary<string, float> { ["F"] = rateF, ["B"] = rateB, ["L"] = rateS, ["R"] = rateS };
                float dur = wN * cW * ((1 - turnW) * Len("idle") + turnW * Len("walkF")); foreach (var k in dir.Keys) dur += wN * dir[k] * Len(walk[k]) / rate[k];
                dur += wA * ((1 - movement) * Len("aimIdle") + movement * Len("aimWalkF")); e.t += dt * playRate / Mathf.Max(dur, .1f); float u = e.t % 1;
                AddClip("idle", u * Len("idle"), wN * cW * (1 - turnW)); AddClip("walkF", u * Len("walkF"), wN * cW * turnW);
                foreach (var k in dir.Keys) AddClip(walk[k], u * Len(walk[k]), wN * dir[k]);
                AddClip("aimIdle", u * Len("aimIdle"), wA * (1 - movement)); AddClip("aimWalkF", u * Len("aimWalkF"), wA * movement);
            }
            else if (s == "running") { e.t += dt * playRate; AddClip("run", e.t, 1); }
            else if (s == "fire") { float t = Once("fire"); AddClip("fire", t, 1 - aiming); AddClip("aimFire", t, aiming); }
            else if (s == "fireEmpty") AddClip("fireEmpty", Once("fireEmpty"), 1);
            else if (s == "bolt") { float t = Once("bolt"); AddClip("bolt", t, 1 - aiming); AddClip("boltAim", t, aiming); }
            else if (s == "reload" || s == "reloadEmpty") { string a = s == "reload" ? "reload" : "reloadEmpty", b2 = s == "reload" ? "aimReload" : "aimReloadEmpty"; float t = Once(a); AddClip(a, t, 1 - aiming); AddClip(b2, t, aiming); }
            else if (s == "rOpen") AddClip("rOpen", Once("rOpen"), 1);
            else if (s == "rInsA" || s == "rInsB") AddClip("rInsert", Once("rInsert", 1.15f), 1);
            else if (s == "rClose") AddClip("rClose", Once("rClose"), 1);
            else if (s == "inspect") AddClip("inspect", Once("inspect"), 1 - aiming);
            else if (s == "unholster") { e.t += dt; AddClip("unholster", e.t, 1); }
            else if (s == "holster") { e.t += dt; AddClip("holster", e.t, 1); }
            else if (s == "grenadeAdd") AddClip("grenadeAdd", Once("grenadeAdd", 1.1f), 1);
            else if (s == "aimIn" || s == "aimOut") { e.t += dt * e.speed; AddClip(s, e.t, 1); }
            return dT;
        }
        void EvalLayer(Layer L, float dt, float w = 1) { L.Step(dt); foreach (var e in L.list) { if (e.s == "none") continue; var d = StateDelta(e, dt); ApplyDelta(pose, d, e.w * w, bonesA); } }
        void EvalOverride(Layer L, float dt, System.Func<string, string> clipOf, float speed = 1)
        {
            L.Step(dt); foreach (var e in L.list) { if (e.s == "none") continue; var c = C(clipOf(e.s)); if (c == null) continue; e.t += dt * speed; if (!c.loop && e.t >= c.len) e.end = true; tmp.CopyFrom(pose); c.Sample(e.t, tmp); LerpPose(pose, tmp, e.w, bonesA); }
        }

        public struct St { public bool aiming, moving, running, crouching, jumped, inAir; public float axisX, axisY, lookX, lean, pitch, height, movementRate; }
        public void Tick(float dt, St st)
        {
            if (!ok || dt <= 0) return;
            if (fpFlashTimer > 0) { fpFlashTimer -= dt; if (fpFlashTimer <= 0 && fpFlash && fpFlash.activeSelf) fpFlash.SetActive(false); }
            var ch = Json.O(D, "character"); var mv = Json.O(D, "movement"); var stats = Json.O(Json.O(GameData.WeaponsRaw, wid ?? ""), "stats");
            float Damp(float cur, float tgt, float time) => cur + (tgt - cur) * (1 - Mathf.Exp(-dt / Mathf.Max(time, 1e-3f) * 2.3f));
            aiming = Damp(aiming, st.aiming ? 1 : 0, Json.F(ch, "dampTimeAiming", .08f)); h = Damp(h, st.axisX, Json.F(ch, "dampTimeLocomotion", .25f)); v = Damp(v, st.axisY, Json.F(ch, "dampTimeLocomotion", .25f));
            movement = Damp(movement, Mathf.Min(1, Mathf.Abs(st.axisX) + Mathf.Abs(st.axisY)), Json.F(ch, "dampTimeLocomotion", .25f)); turning = Damp(turning, Mathf.Min(1, Mathf.Abs(st.lookX) * .08f), Json.F(ch, "dampTimeTurning", .4f));
            leanFwd = Damp(leanFwd, Mathf.Max(0, st.axisY) * (st.moving ? 1 : 0), Json.F(ch, "dampTimeLocomotion", .25f)); leanP = Damp(leanP, st.lean, .12f);
            rateF = Json.F(mv, "walkingMultiplierForward", 1); rateB = Json.F(mv, "walkingMultiplierBackwards", .9f); rateS = Json.F(mv, "walkingMultiplierSideways", .9f);
            playRate = (st.aiming ? Json.F(ch, "aimingSpeedMultiplier", .9f) : 1) * Mathf.Clamp(st.movementRate <= 0 ? 1 : st.movementRate, .4f, 1.18f);
            // transições
            bool tactical = st.running && Json.B(stats, "useTacticalSprintAnimation");
            if (st.running && !tactical && loco.cur.s != "running") loco.Go("running", .5f, loco.cur.t); if ((!st.running || tactical) && loco.cur.s == "running") loco.Go("loco", .2f);
            if (tactical && tac.cur.s == "none") tac.Go("tac", .251f); if (!tactical && tac.cur.s != "none") tac.Go("none", .12f);
            if (st.aiming != aimSt) { wAim.Go(st.aiming ? "aimIn" : "aimOut", .3f); aimSt = st.aiming; }
            if (st.running != runSt) { wRun.Go(st.running ? "aimIn" : "aimOut", st.running ? .35f : .2f); wRun.cur.speed = st.running ? .6f : .8f; runSt = st.running; }
            if (st.crouching != crouchSt) { wCrouch.Go(st.crouching ? "aimIn" : "aimOut", .35f); crouchSt = st.crouching; }
            { var c = overlay.cur; if (c.s == "fire" && Json.B(stats, "boltAction") && c.t >= Len("fire") * .6f && !c.bolted) { c.bolted = true; overlay.Go("bolt", .2f); WAnim(st.aiming ? "boltAim" : "bolt"); events.Add("bolt"); } else if (c.end) overlay.Go("none", 0); }
            {
                var c = actions.cur;
                if (c.end)
                {
                    if (c.s == "rOpen" || c.s == "rInsA" || c.s == "rInsB") { if (c.s != "rOpen") { cycLeft--; onInsert?.Invoke(); } if (cycLeft > 0) { actions.Go(c.s == "rInsA" ? "rInsB" : "rInsA", 0, 0); WAnim("rInsert"); } else { actions.Go("rClose", 0, 0); WAnim("rClose"); } }
                    else actions.Go("none", 0);
                }
            }
            if (armR.cur.end) armR.Go("none", 0); if (armL.cur.end) { armL.Go("none", .1f); if (knifeMesh) knifeMesh.gameObject.SetActive(false); }
            // pose base (override): Idle_Pose <-> Aim_Pose
            pose.CopyFrom(rest);
            // pistola (rodada 38, usuário: "na primeira pessoa, a postura e o posicionamento da pistola são diferentes e inconsistentes"): cada
            // conjunto do LPSP tem a sua Idle_Pose (o revólver 38/RC425 deitado de lado, as Handgun_01/03/04 com o cano para cima) e só a
            // mira ficava igual. Repouso de pistola = a pose de MIRA (arma à frente, as duas mãos nela), abaixada e inclinada nos Motions —
            // a mesma postura para todas as pistolas e coerente com o low ready da 3ª pessoa (misturar 60 % deixava a mão esquerda solta no ar)
            float baseAim = Pistol ? 1 : aiming;
            tmp.CopyFrom(rest); C("idlePose")?.Sample(0, tmp); LerpPose(pose, tmp, 1 - baseAim, bonesA);
            tmp.CopyFrom(rest); C("aimPose")?.Sample(0, tmp); LerpPose(pose, tmp, baseAim, bonesA);
            EvalLayer(loco, dt);
            { var c = C("walkPose"); if (c != null) { tmp.CopyFrom(rest); c.Sample(0, tmp); MakeDelta(dT, tmp, Ref("walkPose"), bonesA); ApplyDelta(pose, dT, leanFwd * (1 - aiming), bonesA); } }
            EvalLayer(overlay, dt); EvalLayer(actions, dt); EvalLayer(holsterL, dt); EvalLayer(armR, dt);
            EvalOverride(armL, dt, s => s == "grenade" ? "grenade" : "knife", 1.1f);
            if (Pistol) wAim.Step(dt); else EvalLayer(wAim, dt);   // pistola: a pose-base já é a de mira; o "aimOut" do LPSP ficava preso no último quadro e entortava o revólver parado
            EvalLayer(wRun, dt); EvalLayer(wCrouch, dt); EvalOverride(tac, dt, _ => "tactical");
            Write(A, pose, bonesA);
            // animator da arma: diferença sobre o repouso do modelo atual
            if (weapon && W != null)
            {
                wpose.CopyFrom(wrest);
                if (weaponAnim is (GClip c, float t))
                {
                    t += dt; if (!wref.TryGetValue(c, out var r)) { r = new Pose(W.Length); r.CopyFrom(wrest); c.Sample(0, r); wref[c] = r; }
                    c.Sample(t, wpose); foreach (var i in bonesW) { wpose.P[i] = wrest.P[i] + wpose.P[i] - r.P[i]; wpose.S[i] = Vector3.Scale(wrest.S[i], new Vector3(r.S[i].x != 0 ? wpose.S[i].x / r.S[i].x : 1, r.S[i].y != 0 ? wpose.S[i].y / r.S[i].y : 1, r.S[i].z != 0 ? wpose.S[i].z / r.S[i].z : 1)); wpose.Q[i] = wrest.Q[i] * (Quaternion.Inverse(r.Q[i]) * wpose.Q[i]); }
                    weaponAnim = t > c.len ? null : (c, t);
                }
                Write(W, wpose, bonesW);
            }
            camH += (st.height - camH) * (1 - Mathf.Exp(-dt * 12));
            pitchNode.localPosition = new Vector3(0, camH, 0); pitchNode.localRotation = Quaternion.Euler(-st.pitch, 0, 0);
            Motions(dt, st);
            AlignSight(dt);
            if (armL.cur.s == "none" || armL.list.Count > 1) IK(armLb, effL); IK(armRb, effR);
        }
        void Motions(float dt, St st)
        {
            var F = Json.O(Json.O(D, "itemFeel"), st.crouching ? "crouching" : st.aiming ? "aiming" : st.running ? "running" : "standing");
            sinceShot += dt; if (sinceShot > .25f) shots = 0;
            var WR = Json.O(IA, "weaponRecoil"); float mul = st.aiming ? Json.F(WR, "aimingStateMultiplier", 1) : Json.F(WR, "standingStateMultiplier", 1);
            recoil.Set(Json.O(WR, st.aiming ? "aimingState" : "standingState"));
            if (shots > 0) { recoil.Target(shots); recoil.tl *= mul; recoil.tr *= mul; } else recoil.Zero();
            var (rl, rr) = recoil.Update(dt);
            if (st.jumped) jumpT = 0; if (st.inAir) airT += dt; else { if (wasAir && airT > .15f) landT = 0; airT = 0; } wasAir = st.inAir;
            jump.Set(Json.O(F, "jumpingCurves")); land.Set(Json.O(F, "landingCurves")); fall.Set(Json.O(F, "fallingCurves")); jumpT += dt; landT += dt;
            if (jumpT < 1) jump.Target(jumpT); else jump.Zero(); if (landT < 1) land.Target(landT); else land.Zero(); if (st.inAir && airT > .1f) fall.Target(airT); else fall.Zero();
            var (jl, jr) = jump.Update(dt); var (ll, lr) = land.Update(dt); var (fl, fr) = fall.Update(dt);
            var LD = Json.O(IA, "leaning"); leanItem.Set(Json.O(LD, st.aiming ? "itemAiming" : "itemStanding")); leanCam.Set(Json.O(LD, st.aiming ? "cameraAiming" : "cameraStanding"));
            leanItem.Target(leanP); leanCam.Target(leanP); var (il, ir) = leanItem.Update(dt); var (cl, cr) = leanCam.Update(dt); camLeanLoc = cl; camLeanRot = cr;
            var IO = Json.O(IA, "itemOffsets"); string key = st.crouching ? "crouching" : st.aiming ? "aiming" : st.running ? "running" : "standing"; var off = Json.O(F, "offset");
            Vector3 V(Dictionary<string, object> o) => o == null ? Vector3.zero : new Vector3(Json.F(o, "x"), Json.F(o, "y"), Json.F(o, "z"));
            var oL = offL.Update(V(Json.O(IO, key + "Location")) + V(Json.O(off, "offsetLocation")), dt); var oR = offR.Update(V(Json.O(IO, key + "Rotation")) + V(Json.O(off, "offsetRotation")), dt);
            var sc = Json.O(scope, "aimLoc"); var sL = scopeL.Update(V(sc) * aiming, dt);
            var loc = rl + jl + ll + fl + oL + il + sL; var rot = jr + lr + fr + oR + ir;
            if (Pistol) { loc += new Vector3(.025f, -.075f, -.03f) * (1 - aiming); rot += new Vector3(-14f, -4f, 0) * (1 - aiming); }   // x negativo = cano para BAIXO neste nó (medido na vitrine: +14° levantava o cano)   // pistola em repouso: baixa e com o cano para a frente/baixo
            motionNode.localPosition = motionBase + loc;
            var other = Quaternion.Euler(rot.x, rot.y, rot.z); recoilQ = Quaternion.Euler(rr.x, rr.y, rr.z);
            motionNode.localRotation = motionBaseQ * other * recoilQ;
        }
        // ADS com mira acoplada/luneta: mede o centro óptico da mira no espaço da câmera e desloca a arma para cair no eixo da visão
        Vector3 sightCorr, gunLast;
        void AlignSight(float dt)
        {
            var gn = inv.parent; if (!gn) return; if (gn.localPosition == gunLast) gn.localPosition -= sightCorr;
            Vector3 tgt = Vector3.zero;
            if (sight && sight.gameObject.activeInHierarchy && aiming > .001f)
            {
                Bounds b = default, lens = default; bool hb = false, hl = false;
                foreach (var r in sight.GetComponentsInChildren<Renderer>())
                {
                    var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; var mb = mf.sharedMesh.bounds; var corners = new List<Vector3>();
                    for (int i = 0; i < 8; i++) corners.Add(camNode.InverseTransformPoint(r.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)))));
                    var bb = new Bounds(corners[0], Vector3.zero); foreach (var c in corners) bb.Encapsulate(c);
                    if (!hb) { b = bb; hb = true; } else b.Encapsulate(bb);
                    var m = r.sharedMaterial; bool alpha = m && (m.renderQueue >= 2450 || m.IsKeywordEnabled("_ALPHATEST_ON") || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"));
                    if (alpha && bb.size.z < .15f * Mathf.Max(bb.size.x, bb.size.y)) { if (!hl) { lens = bb; hl = true; } else lens.Encapsulate(bb); }
                }
                if (hb)
                {
                    // r45: o eixo óptico de cada mira do LPSP é o SOCKET_Scope_XX dela (3,2–4,8 cm acima do trilho); mira de ferro: o topo da massa.
                    // Antes: "62 % da altura da caixa" (as lentes do pacote não têm material com nome) — ponto vermelho 25 px fora, luneta 1,3x 130 px
                    var B = hl ? lens : b; float cz = B.center.z;
                    // r51: luneta da tabela = alinha o CENTRO DA LENTE (mesmo eixo do soquete; a Barret não tem soquete — antes 62 % da caixa)
                    var sa = HasLens ? camNode.InverseTransformPoint(LensCenter) : sightSock ? camNode.InverseTransformPoint(sightSock.position) : ironSight ? new Vector3(B.center.x, B.max.y - B.size.y * .08f, cz) : new Vector3(B.center.x, hl ? B.center.y : B.min.y + B.size.y * .62f, cz);
                    cz = sa.z; var sb = new Vector3(0, 0, cz);
                    var wa = gn.parent.InverseTransformPoint(camNode.TransformPoint(sa)); var wb = gn.parent.InverseTransformPoint(camNode.TransformPoint(sb)); tgt = (wb - wa) * aiming;
                }
            }
            sightCorr = Vector3.Lerp(sightCorr, tgt, 1 - Mathf.Exp(-dt * 8)); gn.localPosition += sightCorr; gunLast = gn.localPosition;
        }
        // ---- soquete óptico da mira + lente com câmera (PiP). r45 trocava o material da lente "Render Camera" pela imagem, mas só abaixo de
        // 2x (≥ 2x virava a luneta preta 2D do HUD). r51 (FBOptic): disco próprio no plano da lente de cada luneta da tabela (2x/4x/6x/1,3x e
        // a luneta da Barret), a lente opaca do modelo some e o retículo do modelo fica na frente; sombra da ocular por cima.
        public Vector3? SightWorld => HasLens ? LensCenter : sightSock ? sightSock.position : (Vector3?)null;   // laboratório: o ponto que o AlignSight põe no eixo
        public float Aiming => aiming; public int PipRes => pipRT ? pipRT.width : 0; public bool PipLive => pipCam && pipCam.enabled;   // r51: teste no exe
        MeshRenderer lensMr; FBOptic.Lens lens; GameObject pipDisc, vigDisc, retDisc, impDisc; MaterialPropertyBlock vigMpb; Material pipMat;
        public Vector2? ImpactMarkPx { get; private set; } public Camera PipCam => pipCam;                                     // r52: onde o X da lente ficou na tela (teste no exe)
        public const float ImpactMinZoom = 2.5f;                                                // r52: X de impacto nas lunetas de 3x para cima
        public bool HasLens => lensMr && lens != null;
        public Vector3 LensCenter => lensMr.transform.TransformPoint(0, lens.y, lens.z);
        public float LensRadius => lens.r * Mathf.Abs(lensMr.transform.lossyScale.x);
        public float PipFov { get; private set; } public float LensScreenR { get; private set; } public Vector2 LensScreenOff { get; private set; }   // laboratório
        void SightSetup()
        {
            lensMr = null; lens = null; pipDisc = vigDisc = retDisc = impDisc = null;            // os discos são filhos da arma (morrem com ela)
            if (!sight) return;
            foreach (var t in sight.GetComponentsInChildren<Transform>(true)) { if (!sightSock && t.name.StartsWith("SOCKET_Scope")) sightSock = t; if (!pipNode && t.name == "Render Camera") pipNode = t; }
            if (!FBOptic.On) return;
            foreach (var r in sight.GetComponentsInChildren<MeshRenderer>(true)) { var l = FBOptic.Find(r.name); if (l != null) { lensMr = r; lens = l; break; } }
            if (!lensMr) return;
            if (lens.hide != null) { var ms = lensMr.sharedMaterials; foreach (var k in lens.hide) if (k < ms.Length) ms[k] = FBOptic.Hidden(); lensMr.sharedMaterials = ms; }
            GameObject Disc(string n, Material m)
            {
                var g = new GameObject(n); g.transform.SetParent(weapon.transform, false); g.layer = FBWorld.LFirstPerson;
                g.AddComponent<MeshFilter>().sharedMesh = FBOptic.Disc(); var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false; g.SetActive(false); return g;
            }
            if (!pipMat) { pipMat = new Material(FBShaders.Unlit) { name = "lente_imagem" }; pipMat.SetColor("_BaseColor", Color.white); if (pipMat.HasProperty("_Cull")) pipMat.SetFloat("_Cull", 0); }
            pipDisc = Disc("lente_imagem", pipMat);
            vigDisc = Disc("lente_sombra", FBOptic.Overlay(FBOptic.Vignette(), 3002)); vigMpb ??= new MaterialPropertyBlock();
            if (lens.reticle) retDisc = Disc("lente_reticulo", FBOptic.Overlay(FBOptic.SniperReticle(), 3001));
            impDisc = Disc("lente_impacto", FBOptic.Overlay(FBOptic.ImpactX(), 3003));
            if (FBOptic.PipOn) { PipRT(128); PipRT(256); }                                       // tamanhos da transição prontos ao equipar, não no quadro da mira
        }
        // r51l: RT da lente por tamanho, criada uma vez e nunca liberada. Antes a lente começava em 128² (câmera ainda longe do olho) e, no quadro
        // em que a mira fechava, passava a 256² com Release + Destroy da RT que a câmera da lente e o material ainda usavam — o driver espera a
        // GPU largar a textura: 17 a 333 ms nesse quadro (traço do exe: Scope_07 137 ms, Barret 333 ms na troca 128 → 256)
        static readonly Dictionary<int, RenderTexture> pipPool = new();
        static RenderTexture PipRT(int n)
        {
            if (!pipPool.TryGetValue(n, out var rt) || !rt) { rt = new RenderTexture(n, n, 24, FBQuality.Mobile ? RenderTextureFormat.Default : RenderTextureFormat.DefaultHDR) { name = "luneta_pip_" + n, antiAliasing = 1 }; rt.Create(); pipPool[n] = rt; }
            return rt;
        }
        // chamado pelo FBPlayer no FIM do LateUpdate (câmera já no lugar, com tremor/coice): posiciona câmera da lente e discos no mesmo quadro
        public void PipUpdate(Camera main, Camera vm, float baseFov, float zoom, bool on, Vector3? impact = null)
        {
            on &= HasLens && main && vm && lensMr.gameObject.activeInHierarchy && aiming > .5f; ImpactMarkPx = null;
            Vector3 c = default; float d = 0;
            if (on) { c = LensCenter; d = Vector3.Dot(c - main.transform.position, main.transform.forward); on = d > .005f; }
            if (!on)
            {
                if (pipCam && pipCam.enabled) pipCam.enabled = false;
                if (pipDisc && pipDisc.activeSelf) { pipDisc.SetActive(false); vigDisc.SetActive(false); if (retDisc) retDisc.SetActive(false); }
                if (impDisc && impDisc.activeSelf) impDisc.SetActive(false);
                return;
            }
            var ct = main.transform; float rW = LensRadius, tanL = rW / d, tanV = Mathf.Tan(vm.fieldOfView * .5f * Mathf.Deg2Rad);
            float pEdge = tanL / tanV;                                                             // raio da lente na tela (fração da meia-altura)
            float tanPhi = pEdge * Mathf.Tan(baseFov * .5f * Mathf.Deg2Rad) / Mathf.Max(1.01f, zoom);
            PipFov = 2 * Mathf.Atan(tanPhi) * Mathf.Rad2Deg; LensScreenR = pEdge;
            var rel = c - ct.position; LensScreenOff = new Vector2(Vector3.Dot(rel, ct.right), Vector3.Dot(rel, ct.up)) / d / tanV;
            bool pip = FBOptic.PipOn;
            if (pip)
            {
                // resolução = tamanho da lente na tela (celular: até 384 px, PC: até 1024) — a câmera só desenha o cone estreito da ampliação
                int want = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(pEdge * Screen.height * .9f)), 128, FBQuality.Mobile ? 384 : 1024);
                if (!pipRT || pipRT.width != want)
                {
                    pipRT = PipRT(want);
                    pipMat.SetTexture("_BaseMap", pipRT); pipMat.mainTexture = pipRT; if (pipCam) pipCam.targetTexture = pipRT;
                }
                if (!pipCam)
                {
                    var g = new GameObject("luneta_camera"); DontDestroyOnLoad(g); pipCam = g.AddComponent<Camera>(); pipCam.CopyFrom(main); pipCam.targetTexture = pipRT;
                    // r51e (fotos do exe: imagem da lente esbranquiçada, sem contraste): a lente fazia o pós-processamento (tonemap + gradação) na RT e o
                    // disco passava DE NOVO pelo pós da câmera principal — duas gradações. Agora a lente renderiza a cena crua (HDR no PC) e o pós da
                    // câmera principal gradua o disco junto com o mundo em volta: mesma cor dentro e fora da lente
                    var ud = g.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); ud.renderPostProcessing = false; ud.renderShadows = !FBQuality.Mobile;
                    ud.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None; pipCam.allowMSAA = false; pipCam.allowHDR = !FBQuality.Mobile;
                }
                if (pipMat.GetTexture("_BaseMap") != pipRT) { pipMat.SetTexture("_BaseMap", pipRT); pipMat.mainTexture = pipRT; pipMat.SetColor("_BaseColor", Color.white); }
                pipCam.depth = main.depth - 1; pipCam.clearFlags = main.clearFlags; pipCam.backgroundColor = main.backgroundColor;
                pipCam.cullingMask = main.cullingMask & ~(1 << FBWorld.LFirstPerson); pipCam.nearClipPlane = .3f; pipCam.farClipPlane = main.farClipPlane;
                pipCam.transform.SetPositionAndRotation(ct.position, ct.rotation); pipCam.fieldOfView = PipFov; pipCam.aspect = 1; if (!pipCam.enabled) pipCam.enabled = true;
            }
            else { if (pipCam && pipCam.enabled) pipCam.enabled = false; pipMat.SetTexture("_BaseMap", null); pipMat.SetColor("_BaseColor", new Color(.05f, .07f, .08f)); }
            // discos virados para o olho no centro da lente; a sombra da ocular vai 2 mm à frente (para o olho), o retículo próprio 1 mm
            float s = Mathf.Abs(lensMr.transform.lossyScale.x);
            void Put(GameObject g, float toEye) { if (!g) return; if (!g.activeSelf) g.SetActive(true); g.transform.SetPositionAndRotation(c - ct.forward * toEye * s, ct.rotation); g.transform.localScale = Vector3.one; var ls = g.transform.lossyScale; g.transform.localScale = new Vector3(rW / Mathf.Max(1e-6f, ls.x), rW / Mathf.Max(1e-6f, ls.y), 1 / Mathf.Max(1e-6f, ls.z)); }
            Put(pipDisc, 0); Put(retDisc, .001f); Put(vigDisc, .002f);
            // olho fora do eixo (balanço/coice): a parte limpa da ocular vai para onde o eixo do olho cruza a lente
            var off = Vector2.ClampMagnitude(-LensScreenOff / Mathf.Max(1e-4f, pEdge) * .5f * 1.5f, .35f);
            vigMpb.SetVector("_BaseMap_ST", new Vector4(1, 1, -off.x, -off.y)); vigDisc.GetComponent<MeshRenderer>().SetPropertyBlock(vigMpb);
            // r52 X DE IMPACTO na lente (3x+): o ponto onde o tiro para (FBPlayer.PredictImpact) projetado pela CÂMERA DA LENTE e levado para o
            // disco pelo mesmo mapeamento da imagem (viewport (u,v) → ponto (2u−1, 2v−1) do disco): fica em cima do que a lente mostra ali,
            // anda junto com o balanço/coice da luneta. Cano sem linha = o X vai para o obstáculo (fora da lente: preso na borda)
            if (impDisc)
            {
                bool show = pip && pipCam && impact.HasValue && zoom >= ImpactMinZoom; Vector2 p = default;
                if (show) { var vp = pipCam.WorldToViewportPoint(impact.Value); show = vp.z > 0; p = new Vector2(vp.x * 2 - 1, vp.y * 2 - 1); if (p.magnitude > .9f) p *= .9f / p.magnitude; }
                if (show != impDisc.activeSelf) impDisc.SetActive(show);
                if (show)
                {
                    float px = Mathf.Max(11, Screen.height * .02f), wr = px * .5f / Mathf.Max(1, pEdge * Screen.height * .5f) * rW;   // ~2 % da altura da tela
                    impDisc.transform.SetPositionAndRotation(c + ct.right * p.x * rW + ct.up * p.y * rW - ct.forward * .0025f * s, ct.rotation); impDisc.transform.localScale = Vector3.one;
                    var ls = impDisc.transform.lossyScale; impDisc.transform.localScale = new Vector3(wr / Mathf.Max(1e-6f, ls.x), wr / Mathf.Max(1e-6f, ls.y), 1 / Mathf.Max(1e-6f, ls.z));
                    var sp = vm.WorldToScreenPoint(impDisc.transform.position); ImpactMarkPx = new Vector2(sp.x, sp.y);
                }
            }
        }
        // r51: o rig fica desligado em 3ª pessoa e liga ao entrar na mira óptica — sem tocar o "sacar" que estava parado no quadro 0
        public void Settle() { holsterL.Go("none", 0); actions.Go("none", 0); overlay.Go("none", 0); weaponAnim = null; }
        void OnDisable() { if (pipCam) pipCam.enabled = false; if (fpFlash) fpFlash.SetActive(false); }                               // veículo/cura/morte desligam o rig: a câmera da lente para junto
        void OnDestroy() { if (pipCam) Destroy(pipCam.gameObject); }                           // RTs da lente ficam no pipPool (compartilhadas)
        static void IK(Transform[] arm, Transform eff)
        {
            if (arm == null || !eff || !arm[0] || !arm[1] || !arm[2]) return; var tgt = eff.position;
            for (int it = 0; it < 6; it++) foreach (var bone in new[] { arm[1], arm[0] })
                {
                    var a = (arm[2].position - bone.position).normalized; var b = (tgt - bone.position).normalized; if (Vector3.Dot(a, b) > .99999f) continue;
                    bone.rotation = Quaternion.FromToRotation(a, b) * bone.rotation;
                }
            arm[2].rotation = eff.rotation;
        }
        // câmera: nó Camera do rig sem o coice da arma (só a arma coiceia na tela)
        public void CameraPose(out Vector3 pos, out Quaternion rot)
        {
            pos = camNode.position; rot = camNode.rotation;
            var M = motionNode; var Wq = M.parent.rotation * M.localRotation * Quaternion.Inverse(recoilQ); var G = Wq * Quaternion.Inverse(recoilQ) * Quaternion.Inverse(Wq); var p0 = M.position;
            pos = G * (pos - p0) + p0; rot = G * rot;
        }
    }
}
