// LABORATÓRIO DE ANIMAÇÃO (rodada 37): FreeBala.exe -fbanimlab [skins|all] [conjuntos|all] [-fblabraw]
// Fileira com TODOS os jogáveis (+ 1 NPC masculino e 1 feminino) numa cena vazia com a pista do foot IK; cada conjunto de arma
// (desarmado, fuzil, pistola, smg, escopeta, sniper) passa pelos estados de jogo (parado, andar, lateral, ré, sprint, agachar, mirar,
// atirar, recarregar, trocar arma, curar, reanimar, caído) com os MESMOS componentes da partida (FBCharacter + FBRig + FBFootIK +
// FBWeaponUser), dirigidos como o jogador/bot dirigem (velocidade remapeada, flags de combate).
// Saída em <build>/autoteste/lab/:
//   <conjunto>_<estado>_<vista>.jpg — folha com um quadro por personagem (ordem em lab_indice.txt); vistas f = frente 3/4, e = lado
//     esquerdo (mão de apoio), c = costas (armas guardadas);
//   lab_metricas.csv — por personagem e estado: erro de mira do cano, mão esquerda fora do alvo do IK, estalo de osso (°/s), cotovelo
//     dobrado ao extremo, joelho invertido, estiramento da malha (aresta deformada em relação ao bind) e o osso onde aconteceu;
//   terreno_<pele>_<conjunto>.jpg + lab_terreno.txt — caminhantes nas rotas do foot IK (meio-fio, escada, rampas, pedras).
// -fblabraw: desliga a pós-animação do FBRig (só a pose do Animator) para comparar.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBAnimLab : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var a = System.Environment.GetCommandLineArgs(); int i = System.Array.IndexOf(a, "-fbanimlab"); if (i < 0) return;
            var g = new GameObject("FB_LabAnimacao"); DontDestroyOnLoad(g); var lab = g.AddComponent<FBAnimLab>();
            string Arg(int k) => i + k < a.Length && !a[i + k].StartsWith("-") ? a[i + k] : null;
            lab.skinArg = Arg(1) ?? "all"; lab.setArg = Arg(2) ?? "all"; lab.raw = System.Array.IndexOf(a, "-fblabraw") >= 0;
        }
        string skinArg = "all", setArg = "all"; bool raw;

        // ---- estados (o que o jogador/bot faria naquele instante)
        public class St
        {
            public string id; public float speed; public Vector2 dir; public bool combat = true, hold, aim, fire, crouch, crouchFast, sprint, relaxed, reload, swap, heal, healCrouch, revive, knocked, treated;
            public float shotAt = .92f, len = 2.6f; public string views = "fe";
        }
        static St S(string id) => new St { id = id };
        static List<St> States(string set)
        {
            bool bare = set == "desarmado", full = set == "desarmado" || set == "fuzil" || set == "pistola";
            float walk = bare ? FBAnimProfile.UnarmedWalkSpeed : set == "pistola" ? 4.4f : 4.2f, walkHot = set == "pistola" ? 3.7f : 3.4f, run = 7.4f;   // = FBPlayer.WalkSp (fora de ação × em ação)
            var L = new List<St>();
            if (bare)
            {
                L.Add(new St { id = "parado", combat = false, relaxed = true, views = "fec" });
                L.Add(new St { id = "andar", combat = false, relaxed = true, speed = walk, dir = Vector2.up });
                L.Add(new St { id = "lateral_dir", combat = false, relaxed = true, speed = walk, dir = Vector2.right });
                L.Add(new St { id = "lateral_esq", combat = false, relaxed = true, speed = walk, dir = Vector2.left });
                L.Add(new St { id = "re", combat = false, relaxed = true, speed = walk, dir = Vector2.down });
                L.Add(new St { id = "sprint", combat = false, relaxed = true, speed = run, dir = Vector2.up, sprint = true, views = "fec" });
                L.Add(new St { id = "agachado", combat = false, relaxed = true, crouch = true });
                L.Add(new St { id = "agachado_andando", combat = false, relaxed = true, crouch = true, speed = 3, dir = Vector2.up });
                L.Add(new St { id = "agachado_rapido", combat = false, relaxed = true, crouch = true, crouchFast = true, speed = FBAnimProfile.CrouchFastSpeed, dir = Vector2.up });
                L.Add(new St { id = "agachado_lateral", combat = false, relaxed = true, crouch = true, speed = 3, dir = Vector2.right });
                L.Add(new St { id = "cura", combat = false, heal = true, views = "fec" });
                L.Add(new St { id = "reanimar", combat = false, revive = true, views = "fec" });
                L.Add(new St { id = "caido", combat = false, knocked = true });
                L.Add(new St { id = "sendo_reanimado", combat = false, knocked = true, treated = true, views = "fec" });
                L.Add(new St { id = "caido_andando", combat = false, knocked = true, speed = 1.2f, dir = Vector2.up });
                return L;
            }
            L.Add(new St { id = "patrulha_parado", views = "fec" });
            if (full) L.Add(new St { id = "relaxado_parado", relaxed = true, len = 4.6f });   // > 3 s parado: arma no descanso (FBRig, Resting)
            L.Add(new St { id = "combate_parado", hold = true });
            L.Add(new St { id = "mirando", aim = true });
            L.Add(new St { id = "atirando", aim = true, fire = true });
            if (full)
            {
                L.Add(new St { id = "andar", speed = walk, dir = Vector2.up, views = "fec" });
                L.Add(new St { id = "andar_combate", hold = true, speed = walkHot, dir = Vector2.up });
                L.Add(new St { id = "mirando_andando", aim = true, speed = 3, dir = Vector2.up });
                L.Add(new St { id = "lateral_dir", hold = true, speed = walkHot, dir = Vector2.right });
                L.Add(new St { id = "lateral_esq", hold = true, speed = walkHot, dir = Vector2.left });
                L.Add(new St { id = "re", hold = true, speed = walkHot, dir = Vector2.down });
                L.Add(new St { id = "diagonal", hold = true, speed = walkHot, dir = new Vector2(.707f, .707f) });
            }
            else L.Add(new St { id = "andar_combate", hold = true, speed = walkHot, dir = Vector2.up });
            L.Add(new St { id = "sprint", speed = run, dir = Vector2.up, sprint = true, views = "fec" });
            if (full) { L.Add(new St { id = "agachado", crouch = true, views = "fec" }); L.Add(new St { id = "agachado_andando", crouch = true, hold = true, speed = 3, dir = Vector2.up }); L.Add(new St { id = "agachado_rapido", crouch = true, crouchFast = true, speed = FBAnimProfile.CrouchFastSpeed, dir = Vector2.up }); }
            L.Add(new St { id = "agachado_mirando", crouch = true, aim = true });
            L.Add(new St { id = "recarga", hold = true, reload = true, shotAt = .5f });
            if (full)
            {
                L.Add(new St { id = "troca_arma", swap = true, shotAt = .2f, len = 2.2f, views = "fec" });
                L.Add(new St { id = "cura", combat = false, heal = true, views = "fec" });
                L.Add(new St { id = "cura_agachado", combat = false, heal = true, healCrouch = true, crouch = true });
                L.Add(new St { id = "reanimar", combat = false, revive = true, views = "fec" });
                L.Add(new St { id = "caido", combat = false, knocked = true });
                L.Add(new St { id = "sendo_reanimado", combat = false, knocked = true, treated = true, views = "fec" });
            }
            return L;
        }

        // ---- ator do laboratório (no lugar: só alimenta o Animator; a fileira não anda)
        [DefaultExecutionOrder(-200)]
        public class Actor : MonoBehaviour
        {
            public St st; public float t, fireT; public FBCharacter ch; public FBWeaponUser wu; public bool swapped, reloaded;
            void Awake() { ch = GetComponent<FBCharacter>(); wu = GetComponent<FBWeaponUser>(); }
            // [parceiro] v1 (thread de física): tempo de CPU do script medido pelo FBTempo (desligado = só um teste de bool;
            // o FBTesteExe liga, lê e zera por fase). O corpo original fica em Update__fbt.
            static readonly int __fbt_Update = FBTempo.Id("FBAnimLab.Actor.Update");
            void Update()
            {
                if (!FBTempo.On) { Update__fbt(); return; }
                long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
            }
            void Update__fbt()
            {
                if (st == null || !ch) return; float dt = Time.deltaTime; t += dt; var w = wu ? wu.Cur : null;
                ch.combat = st.combat && w != null && !st.knocked; ch.relaxed = st.relaxed; ch.crouch = st.crouch || st.knocked; ch.CrouchFast = st.crouchFast; ch.prone = st.knocked;
                ch.aiming = st.aim && w != null; ch.combatHold = st.hold ? 1 : 0;
                ch.sprint = st.sprint;
                // tiro: mesmo ritmo da arma (semiautomática no máximo 4/s, como o dedo)
                if (st.fire && w != null) { fireT -= dt; if (fireT <= 0) { fireT = w.auto ? w.Interval : Mathf.Max(.25f, w.Interval); ch.lastShot = 0; try { FBFx.Muzzle(ch.MuzzlePos, .5f, w.cls); } catch { } } }
                ch.shooting = st.fire && ch.lastShot < .15f;
                if (st.reload && !reloaded && wu && w != null) { reloaded = true; wu.ammo[wu.cur] = 0; wu.reserve[wu.cur] = w.mag * 4; wu.Reload(); }
                if (st.swap && !swapped && wu) { swapped = true; int to = wu.cur == 2 ? 0 : 2; if (wu.slots[to] != null) wu.SwitchTo(to); }
                // cura / reanimar / caído: o mesmo que o FBPlayer/FBBot fazem
                if (st.heal) ch.sys = st.healCrouch ? Has("FF_SYS_KO_Revive") ?? Has("FF_SYS_Heal_Loop") : t < .35f ? Has("FF_SYS_Heal_Start") : Has("FF_SYS_Heal_Loop");
                if (st.revive) ch.sys = ch.RescuePose(true, dt);                          // reanimar = Sigma, mesmo caminho do FBPlayer/FBBot
                if (st.treated) { ch.revivedBy = this; ch.reviveTick = Time.time; }                 // alguém atendendo (par do IE)
                if (st.knocked) { ch.knocked = true; ch.koAt = Time.time - 2; ch.sys = FBCombat.KoPose(ch, st.speed > .1f); }
                if (st.heal || st.revive || st.knocked) ch.SetWeaponVisible(false);
                ch.StowCurrent((st.heal || st.revive) && w != null);                         // curando/reanimando: arma no coldre
                // velocidade: mundo = direção local × velocidade; Animator = velocidade remapeada (remapSpeed dos bots/jogador)
                var lv = new Vector3(st.dir.x, 0, st.dir.y) * st.speed * ch.ScaleK; ch.velocity = transform.rotation * lv; ch.grounded = true;
                float s = st.speed, sp = Remap(s); ch.animVel = s > .05f ? new Vector2(st.dir.x, st.dir.y).normalized * sp : Vector2.zero;
                ch.hasAim = true; ch.aimPoint = transform.position + transform.forward * 30 + Vector3.up * 1.35f * ch.ScaleK;
            }
            static string Has(string c) => FBRegistry.I && FBRegistry.I.Clip(c) ? c : null;
            static float Remap(float s) { var P = GameData.Section("puppet"); float pw = Json.F(P, "playerWalkSpeed", 4), pr = Json.F(P, "playerRunSpeed", 6.8f), aw = Json.F(P, "aiWalkAnimSpeed", 2), ar = Json.F(P, "aiRunAnimSpeed", 5); return s <= pw ? s / pw * aw : aw + (s - pw) / (pr - pw) * (ar - aw); }
            public void Leave()
            {
                if (st == null) return;
                if (st.revive) { ch.RescuePose(false, 0); ch.RescuePose(false, 99); ch.sys = null; }
                ch.StowCurrent(false);
                if (st.knocked) { ch.knocked = false; ch.prone = false; ch.sys = null; ch.revivedBy = null; }
                if (st.heal) ch.sys = null;
                ch.SetWeaponVisible(!ch.UnarmedLocomotion); ch.lastShot = 9; ch.combatHold = 0; ch.shooting = ch.aiming = false;
                st = null; t = 0; swapped = reloaded = false;
            }
        }

        // ---- métricas por personagem/estado
        class M
        {
            public readonly List<float> aim = new(), lh = new(), pop = new(), rate = new(); public readonly Dictionary<string, int> clips = new(); public float elbowMin = 999, kneeRev; public int n;
            public float stretchMax, stretchFrac, squashMin = 9, popMax, holsterCm, gapCm; public string worstBone = "-", popBone = "-", holsterWhat = "-", gapWhat = "-";
            // r60: o estiramento "cru" é amplificado por arestas MINÚSCULAS do bind (o filtro era 1e-5 m = 0,01 mm; uma aresta de 0,1 mm que
            // vira 1 cm dá 100× sem nada aparecer na tela). stretchFirme ignora as arestas abaixo de 20 % da mediana do bind — é o número
            // que corresponde ao que se vê. worstLen0/worstLen1 = a aresta culpada no bind e deformada, em mm, para saber qual dos dois é.
            public float stretchFirme, worstLen0, worstLen1; public string firmeBone = "-";
            public float aboveCm; public string aboveWhat = "-";   // r60: arma guardada passando do alto da cabeça
            public readonly Dictionary<Transform, Quaternion> prev = new();
        }
        static readonly List<AnimatorClipInfo> ciL = new();
        static readonly HumanBodyBones[] POP = { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };

        void Sample(FBCharacter c, M m, float dt, bool settle)
        {
            var an = c.an; if (!an || !an.isHuman) return; var inv = Quaternion.Inverse(c.transform.rotation);
            foreach (var hb in POP)
            {
                var b = an.GetBoneTransform(hb); if (!b) continue; var q = inv * b.rotation;
                if (m.prev.TryGetValue(b, out var p) && settle && dt > 0) { float w = Quaternion.Angle(p, q) / dt; m.pop.Add(w); if (w > m.popMax) { m.popMax = w; m.popBone = hb.ToString(); } }
                m.prev[b] = q;
            }
            if (!settle) return; m.n++;
            if (c.velocity.sqrMagnitude > .3f) { m.rate.Add(c.LocoRate); an.GetCurrentAnimatorClipInfo(0, ciL); string bc = "-"; float bw = 0; foreach (var x in ciL) if (x.weight > bw && x.clip) { bw = x.weight; bc = x.clip.name; } m.clips[bc] = m.clips.TryGetValue(bc, out var nc) ? nc + 1 : 1; }
            if (c.weaponGO && c.weaponGO.activeInHierarchy && c.CombatStance && c.rig && c.rig.AimW > .9f)
            { var d = c.aimPoint - c.MuzzlePos; if (d.sqrMagnitude > 1) m.aim.Add(Vector3.Angle(c.BarrelDir(), d)); }
            if (c.rig && c.rig.LeftTargetValid && c.rig.IkW > .9f && c.LeftHand) m.lh.Add((c.LeftHand.position - c.rig.LeftTarget).magnitude);
            foreach (var right in new[] { false, true })
            {
                Transform U(HumanBodyBones r, HumanBodyBones l) => an.GetBoneTransform(right ? r : l);
                var sh = U(HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperArm); var el = U(HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerArm); var wr = U(HumanBodyBones.RightHand, HumanBodyBones.LeftHand);
                if (sh && el && wr) m.elbowMin = Mathf.Min(m.elbowMin, Vector3.Angle(sh.position - el.position, wr.position - el.position));
                var hp = U(HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftUpperLeg); var kn = U(HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftLowerLeg); var an2 = U(HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot);
                if (hp && kn && an2)
                {
                    float ang = Vector3.Angle(hp.position - kn.position, an2.position - kn.position);
                    // joelho invertido = dobrado para trás da frente do corpo por mais de 3,5 cm (com 1,2 cm o parado e a lateral desarmados,
                    // pés abertos, acusavam 70–150 quadros sem nada errado na imagem; pela direção do osso do dedo acusava quase tudo)
                    if (ang < 160 && Vector3.Dot(kn.position - (hp.position + an2.position) * .5f, c.transform.forward) < -.035f * c.ScaleK) m.kneeRev++;
                }
            }
        }

        // estiramento da malha: arestas do corpo deformado ÷ as do bind (normalizado pela mediana — escala some); maior e fração > 1,6×
        static readonly Dictionary<Mesh, (int[] a, int[] b, float[] len, BoneWeight[] bw, float med0)> edgeCache = new();
        static void Stretch(FBCharacter c, M m)
        {
            var tmp = new Mesh();
            foreach (var r in c.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r.enabled || !r.sharedMesh || !r.sharedMesh.isReadable) continue; var t = r.transform; bool weapon = false;
                for (var q = t; q && q != c.transform; q = q.parent) if (q.name.StartsWith("Coldre_") || (c.weaponGO && q == c.weaponGO.transform)) { weapon = true; break; }
                if (weapon) continue;
                var sm = r.sharedMesh;
                if (!edgeCache.TryGetValue(sm, out var E))
                {
                    var V0 = sm.vertices; var T = sm.triangles; var seen = new HashSet<long>(); var la = new List<int>(); var lb = new List<int>(); var ll = new List<float>();
                    for (int k = 0; k < T.Length; k += 3) for (int e = 0; e < 3; e++)
                        {
                            int x = T[k + e], y = T[k + (e + 1) % 3]; if (x > y) (x, y) = (y, x); long key = ((long)x << 32) | (uint)y; if (!seen.Add(key)) continue;
                            float l0 = (V0[x] - V0[y]).magnitude; if (l0 < 1e-5f) continue; la.Add(x); lb.Add(y); ll.Add(l0);
                        }
                    var ls = ll.ToArray(); var lsOrd = (float[])ls.Clone(); System.Array.Sort(lsOrd);
                    E = (la.ToArray(), lb.ToArray(), ls, sm.boneWeights, lsOrd.Length > 0 ? lsOrd[lsOrd.Length / 2] : 0f); edgeCache[sm] = E;
                }
                try { r.BakeMesh(tmp, true); } catch { continue; }
                var V = tmp.vertices; if (V.Length != sm.vertexCount) continue; int n = E.a.Length; if (n == 0) continue;
                var ratio = new float[n]; for (int k = 0; k < n; k++) ratio[k] = (V[E.a[k]] - V[E.b[k]]).magnitude / E.len[k];
                var sorted = (float[])ratio.Clone(); System.Array.Sort(sorted); float med = Mathf.Max(1e-6f, sorted[n / 2]);
                int over = 0, worst = -1, worstF = -1; float mx = 0, mn = 9, mxF = 0; float minLen = E.med0 * .2f;   // r60: aresta curta demais no bind = ruído
                for (int k = 0; k < n; k++)
                {
                    float s = ratio[k] / med; if (s > 1.6f) over++; if (s > mx) { mx = s; worst = k; } if (s < mn) mn = s;
                    if (E.len[k] >= minLen && s > mxF) { mxF = s; worstF = k; }
                }
                if (mx > m.stretchMax && worst >= 0) { m.worstLen0 = E.len[worst] * 1000; m.worstLen1 = (V[E.a[worst]] - V[E.b[worst]]).magnitude * 1000; }
                m.stretchMax = Mathf.Max(m.stretchMax, mx); m.stretchFrac = Mathf.Max(m.stretchFrac, over / (float)n); m.squashMin = Mathf.Min(m.squashMin, mn);
                if (mxF > m.stretchFirme) { m.stretchFirme = mxF; if (worstF >= 0 && E.bw != null && E.bw.Length > E.a[worstF]) { int bf = E.bw[E.a[worstF]].boneIndex0; var bb = r.bones; if (bf >= 0 && bf < bb.Length && bb[bf]) m.firmeBone = bb[bf].name; } }
                if (worst >= 0 && E.bw != null && E.bw.Length > E.a[worst]) { int bi = E.bw[E.a[worst]].boneIndex0; var bs = r.bones; if (bi >= 0 && bi < bs.Length && bs[bi]) m.worstBone = bs[bi].name; }
            }
            Destroy(tmp);
        }
        static void Holster(FBCharacter c, M m) { var hp = HolsterPenetration(c); if (hp.cm > m.holsterCm) { m.holsterCm = hp.cm; m.holsterWhat = hp.what; } if (hp.gap > m.gapCm) { m.gapCm = hp.gap; m.gapWhat = hp.gapWhat; } if (hp.above > m.aboveCm) { m.aboveCm = hp.above; m.aboveWhat = hp.aboveWhat; } }
        static float P(List<float> l, float q) { if (l.Count == 0) return 0; var c = new List<float>(l); c.Sort(); return c[Mathf.Clamp(Mathf.RoundToInt(q * (c.Count - 1)), 0, c.Count - 1)]; }

        /// <summary>Rodada 38: quanto cada arma GUARDADA entra no corpo, medido na pose atual contra os triângulos da malha (não o perfil do
        /// jogo): raio pela direção do encaixe (costas: de trás para a frente; bandoleira: da frente para trás; coxa: de fora para dentro);
        /// ponto da arma entre a 1ª e a última superfície do corpo = dentro, penetração = distância da superfície de entrada. Braços/mãos
        /// fora (seguram a arma da mão e balançam). Retorna o pior (cm), a arma e o encaixe.</summary>
        public static (float cm, string what, float gap, string gapWhat, float above, string aboveWhat) HolsterPenetration(FBCharacter c)
        {
            var hs = c.HolsterObjects.Where(o => o && o.activeInHierarchy).ToList(); if (hs.Count == 0 || !c.Chest || !c.an) return (0, "-", 0, "-", 0, "-");
            c.TorsoAxes(out var up, out var fw, out var right); c.ThighAxes(out _, out var tside, out _);   // eixos do PRÓPRIO tronco/coxa (rodada 38)
            var arms = new HashSet<Transform>(); var ua = new[] { c.an.GetBoneTransform(HumanBodyBones.LeftUpperArm), c.an.GetBoneTransform(HumanBodyBones.RightUpperArm) }; float shR = .13f * c.ScaleK;
            foreach (var hb in new[] { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm }) { var t0 = c.an.GetBoneTransform(hb); if (t0) foreach (var t in t0.GetComponentsInChildren<Transform>(true)) arms.Add(t); }
            var uaSet = new HashSet<Transform>(); foreach (var t0 in ua) if (t0) foreach (var t in t0.GetComponentsInChildren<Transform>(true)) if (!arms.Contains(t)) uaSet.Add(t);
            bool Under(Transform t, GameObject g) { for (; t; t = t.parent) if (t.gameObject == g) return true; return false; }
            bool IsGun(Transform t) { if (c.weaponGO && Under(t, c.weaponGO)) return true; foreach (var h in hs) if (Under(t, h)) return true; for (; t && t != c.transform; t = t.parent) if (t.name.StartsWith("Coldre_")) return true; return false; }
            // corpo: triângulos de mundo (malhas com pele + acessórios rígidos presos nos ossos, ex.: colete)
            var tris = new List<Vector3>(); var tmp = new Mesh();
            foreach (var r in c.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || IsGun(r.transform) || r is ParticleSystemRenderer) continue;
                Vector3[] V; int[] T; bool[] armV = null;
                if (r is SkinnedMeshRenderer sr) { if (!sr.sharedMesh || !sr.sharedMesh.isReadable) continue; sr.BakeMesh(tmp, true); var M = Matrix4x4.TRS(sr.transform.position, sr.transform.rotation, Vector3.one); V = tmp.vertices; for (int i = 0; i < V.Length; i++) V[i] = M.MultiplyPoint3x4(V[i]); T = sr.sharedMesh.triangles;
                    var bw = sr.sharedMesh.boneWeights; var bones = sr.bones; if (bw.Length == V.Length) { armV = new bool[V.Length]; for (int i = 0; i < bw.Length; i++) { int bi = bw[i].boneIndex0; var bt = bi >= 0 && bi < bones.Length ? bones[bi] : null; armV[i] = bt && (arms.Contains(bt) || (uaSet.Contains(bt) && ua.All(u => !u || (V[i] - u.position).magnitude > shR))); } } }   // antebraço/mão fora; do braço só o ombro conta
                else if (r is MeshRenderer && r.GetComponent<MeshFilter>() is MeshFilter mf && mf.sharedMesh && mf.sharedMesh.isReadable) { var M = r.transform.localToWorldMatrix; V = mf.sharedMesh.vertices; for (int i = 0; i < V.Length; i++) V[i] = M.MultiplyPoint3x4(V[i]); T = mf.sharedMesh.triangles; if (arms.Contains(r.transform) || (r.transform.parent && arms.Contains(r.transform.parent))) continue; }
                else continue;
                for (int k = 0; k + 2 < T.Length; k += 3) { tris.Add(V[T[k]]); tris.Add(V[T[k + 1]]); tris.Add(V[T[k + 2]]); }
            }
            Object.Destroy(tmp); if (tris.Count == 0) return (0, "sem malha legível", 0, "-", 0, "-");
            float worst = 0; string what = "-"; float gapW = 0; string gapWhat = "-";
            // r60: quanto a arma guardada passa ACIMA do alto da cabeça. Nas folhas de costas o fuzil nas costas sobe muito, e o "afastamento"
            // antigo não distingue isso de uma arma só comprida: aqui é uma medida direta e fácil de julgar (0 = encostado no alto da cabeça).
            float topBody = float.NegativeInfinity; foreach (var t in tris) if (t.y > topBody) topBody = t.y;
            float aboveW = 0; string aboveWhat = "-";
            foreach (var h in hs)
            {
                var id = h.name.Replace("Coldre_", ""); string slot = GameData.Weapons.TryGetValue(id, out var wd) ? FBCharacter.HolsterSlot(wd.cls) : "back";
                var dir = slot == "back" ? fw : slot == "sling" ? -fw : -tside; var au = Vector3.Cross(dir, Mathf.Abs(Vector3.Dot(dir, up)) < .9f ? up : fw).normalized; var av = Vector3.Cross(dir, au);
                var pts = new List<Vector3>();
                foreach (var mf in h.GetComponentsInChildren<MeshFilter>()) { if (!mf.sharedMesh || !mf.sharedMesh.isReadable || !mf.GetComponent<Renderer>() || !mf.GetComponent<Renderer>().enabled) continue; var V = mf.sharedMesh.vertices; var M = mf.transform.localToWorldMatrix; int step = Mathf.Max(1, V.Length / 600); for (int i = 0; i < V.Length; i += step) pts.Add(M.MultiplyPoint3x4(V[i])); }
                if (pts.Count == 0) continue;
                float topGun = float.NegativeInfinity; foreach (var q in pts) if (q.y > topGun) topGun = q.y;
                float above = (topGun - topBody) * 100 / Mathf.Max(.01f, c.ScaleK);
                if (above > aboveW) { aboveW = above; aboveWhat = h.name.Replace("Coldre_", "") + "@" + slot; }
                // grade dos triângulos projetados (2 cm) só em volta da arma
                float cell = .02f * c.ScaleK; float u0 = pts.Min(p => Vector3.Dot(p, au)) - cell, u1 = pts.Max(p => Vector3.Dot(p, au)) + cell, v0 = pts.Min(p => Vector3.Dot(p, av)) - cell, v1 = pts.Max(p => Vector3.Dot(p, av)) + cell;
                int nu = Mathf.Clamp(Mathf.CeilToInt((u1 - u0) / cell), 1, 400), nv = Mathf.Clamp(Mathf.CeilToInt((v1 - v0) / cell), 1, 400); var grid = new List<int>[nu * nv];
                for (int k = 0; k < tris.Count; k += 3)
                {
                    float a0 = Vector3.Dot(tris[k], au), a1 = Vector3.Dot(tris[k + 1], au), a2 = Vector3.Dot(tris[k + 2], au), b0 = Vector3.Dot(tris[k], av), b1 = Vector3.Dot(tris[k + 1], av), b2 = Vector3.Dot(tris[k + 2], av);
                    int iu0 = Mathf.FloorToInt((Mathf.Min(a0, Mathf.Min(a1, a2)) - u0) / cell), iu1 = Mathf.FloorToInt((Mathf.Max(a0, Mathf.Max(a1, a2)) - u0) / cell), iv0 = Mathf.FloorToInt((Mathf.Min(b0, Mathf.Min(b1, b2)) - v0) / cell), iv1 = Mathf.FloorToInt((Mathf.Max(b0, Mathf.Max(b1, b2)) - v0) / cell);
                    if (iu1 < 0 || iv1 < 0 || iu0 >= nu || iv0 >= nv) continue;
                    for (int x = Mathf.Max(0, iu0); x <= Mathf.Min(nu - 1, iu1); x++) for (int y = Mathf.Max(0, iv0); y <= Mathf.Min(nv - 1, iv1); y++) (grid[x * nv + y] ??= new List<int>()).Add(k);
                }
                float pen = 0, gap = 1e9f;
                foreach (var p in pts)
                {
                    int x = Mathf.FloorToInt((Vector3.Dot(p, au) - u0) / cell), y = Mathf.FloorToInt((Vector3.Dot(p, av) - v0) / cell); if (x < 0 || y < 0 || x >= nu || y >= nv || grid[x * nv + y] == null) continue;
                    var o = p - dir * 2f; float first = 1e9f, last = -1e9f;
                    foreach (int k in grid[x * nv + y]) if (RayTri(o, dir, tris[k], tris[k + 1], tris[k + 2], out float t)) { first = Mathf.Min(first, t); last = Mathf.Max(last, t); }
                    if (first < 2f && last > 2f) pen = Mathf.Max(pen, 2f - first);
                    else if (first > 2f && first < 3f) gap = Mathf.Min(gap, first - 2f);                       // afastamento: arma fora, até a 1ª superfície
                }
                if (pen > worst) { worst = pen; what = id + "@" + slot; }
                if (pen <= 0 && gap < 1e8f && gap > gapW) { gapW = gap; gapWhat = id + "@" + slot; }
            }
            return (worst * 100 / Mathf.Max(.01f, c.ScaleK), what, gapW * 100 / Mathf.Max(.01f, c.ScaleK), gapWhat, aboveW, aboveWhat);
        }
        static bool RayTri(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
        {
            t = 0; var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(d, e2); float det = Vector3.Dot(e1, p); if (Mathf.Abs(det) < 1e-9f) return false;
            float inv = 1 / det; var s = o - a; float u = Vector3.Dot(s, p) * inv; if (u < 0 || u > 1) return false; var q = Vector3.Cross(s, e1); float v = Vector3.Dot(d, q) * inv; if (v < 0 || u + v > 1) return false;
            t = Vector3.Dot(e2, q) * inv; return t > 0;
        }

        // ---- cena
        readonly List<FBCharacter> chars = new(); readonly List<Actor> actors = new(); Camera cam; RenderTexture tile; Texture2D sheet;
        const int TW = 256, TH = 320, COLS = 8; string dir;
        FBCharacter Spawn(string skin, Vector3 pos)
        {
            var pf = FBRegistry.I.Char(skin); if (!pf) return null;
            var go = Instantiate(pf, pos, Quaternion.identity); go.name = "Lab_" + skin;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = FBWorld.LChar;
            var ch = go.GetOrAdd<FBCharacter>(); ch.skinId = skin; ch.isBot = true; ch.team = 1;
            FBWardrobe.ApplyTo(go, null, false);
            var cc = go.AddComponent<CharacterController>(); float s = 1f / go.transform.lossyScale.y; float H = Json.F(GameData.Section("controller"), "m_Height", 1.8f);
            cc.height = H * s; cc.radius = .32f * s; cc.center = new Vector3(0, H * .5f * s, 0); cc.enabled = false;          // no lugar: sem física
            go.AddComponent<FBWeaponUser>(); var an = go.GetComponent<Animator>(); if (an) an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var a = go.AddComponent<Actor>(); actors.Add(a); if (raw && ch.rig) ch.rig.bypass = true;
            return ch;
        }
        void Show(FBCharacter only) { foreach (var c in chars) if (c) foreach (var r in c.GetComponentsInChildren<Renderer>(true)) r.forceRenderingOff = only && c != only; }
        void Capture(string name, string views)
        {
            foreach (char v in views)
            {
                int rows = Mathf.CeilToInt(chars.Count / (float)COLS); if (!sheet || sheet.height != rows * TH) sheet = new Texture2D(COLS * TW, rows * TH, TextureFormat.RGB24, false);
                for (int i = 0; i < chars.Count; i++)
                {
                    var c = chars[i]; if (!c) continue; Show(c); float S = c.ScaleK;
                    float yaw = v == 'f' ? 35 : v == 'e' ? -90 : 150; var ctr = c.transform.position + Vector3.up * (c.crouch || c.prone ? .7f : .95f) * S;
                    cam.transform.position = ctr + Quaternion.Euler(0, c.transform.eulerAngles.y + yaw, 0) * Vector3.forward * 3.1f * S + Vector3.up * .15f * S; cam.transform.LookAt(ctr);
                    cam.targetTexture = tile; cam.Render();
                    RenderTexture.active = tile; sheet.ReadPixels(new Rect(0, 0, TW, TH), (i % COLS) * TW, (rows - 1 - i / COLS) * TH); RenderTexture.active = null;
                }
                Show(null); sheet.Apply(false);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"{name}_{v}.jpg"), sheet.EncodeToJPG(85));
            }
            cam.targetTexture = null;
        }

        IEnumerator Start()
        {
            dir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "autoteste", "lab"); System.IO.Directory.CreateDirectory(dir);
            for (float w = 0; w < 2.5f; w += Time.unscaledDeltaTime) yield return null;                          // menu montou (registro, dados)
            GameData.Load(); FBQuality.Set(3); FBFootIK.Q.IgnoreVisibility = true; FBFootIK.Q.Lock = false;          // fileira no lugar: sem trava de pé
            var old = SceneManager.GetActiveScene(); var sc = SceneManager.CreateScene("AnimLab"); SceneManager.SetActiveScene(sc);
            var un = SceneManager.UnloadSceneAsync(old); while (un != null && !un.isDone) yield return null;
            FBFootIKTestBed.BuildCourse(); Physics.SyncTransforms();
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.62f, .66f, .74f); RenderSettings.ambientEquatorColor = new Color(.48f, .48f, .48f); RenderSettings.ambientGroundColor = new Color(.26f, .24f, .22f);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            var key = new GameObject("Sol").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.35f; key.shadows = LightShadows.Soft; key.transform.rotation = Quaternion.Euler(42, -35, 0);
            var fill = new GameObject("Preenchimento").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .45f; fill.shadows = LightShadows.None; fill.transform.rotation = Quaternion.Euler(25, 150, 0);
            cam = new GameObject("LabCam").AddComponent<Camera>(); cam.tag = "MainCamera"; cam.fieldOfView = 38; cam.nearClipPlane = .05f; cam.farClipPlane = 200; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.56f, .62f, .68f);
            cam.transform.position = new Vector3(0, 6, -26); cam.transform.LookAt(new Vector3(0, 1, -12));
            tile = new RenderTexture(TW, TH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };

            // personagens: todos os jogáveis (r_*) + um NPC de cada sexo; ou a lista pedida
            var reg = FBRegistry.I; var ids = new List<string>();
            if (skinArg == "all") { ids.AddRange(GameData.Skins.Where(s => s.id.StartsWith("r_") && reg.HasChar(s.id)).Select(s => s.id)); foreach (var sx in new[] { "m", "f" }) { var n = GameData.Skins.FirstOrDefault(s => s.id.StartsWith("n2_") && s.sex == sx && reg.HasChar(s.id)); if (n != null) ids.Add(n.id); } }
            else ids.AddRange(skinArg.Split(',').Where(reg.HasChar));
            for (int i = 0; i < ids.Count; i++) { var c = Spawn(ids[i], new Vector3(-30 + (i % 16) * 4, 0, -14 + (i / 16) * 5)); if (c) chars.Add(c); }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "lab_indice.txt"), $"colunas {COLS}, quadro {TW}x{TH}\n" + string.Join("\n", chars.Select((c, k) => $"{k + 1:00} (linha {k / COLS + 1}, coluna {k % COLS + 1}): {c.skinId} · {c.Name}")));
            Debug.Log($"[FreeBala][lab] {chars.Count} personagens · pós-animação {(raw ? "DESLIGADA" : "ligada")}");
            // aquecimento: no build Mono a 1ª folha saía vazia (texturas/shaders das 32 malhas ainda subindo) — renderiza a fileira na
            // textura do quadro até estabilizar
            cam.targetTexture = tile; for (float w = 0; w < 4.5f; w += Time.unscaledDeltaTime) { cam.Render(); yield return null; } cam.targetTexture = null;


            // armas por classe (a primeira de cada)
            string First(string cls, string pref) => pref != null && GameData.Weapons.TryGetValue(pref, out var d) && d.cls == cls && reg.Weapon(pref) ? pref : GameData.Weapons.Values.Where(x => x.cls == cls && reg.Weapon(x.id)).Select(x => x.id).FirstOrDefault();
            string rifle = First("fuzil", FBSession.primary) ?? First("fuzil", "ak"), smg = First("submetralhadora", FBSession.smg), pistol = First("pistola", FBSession.secondary), sg = First("escopeta", null), sn = First("sniper", null);
            var sets = new List<(string id, string[] give, int slot)> { ("desarmado", new[] { rifle, smg, pistol }, 3), ("fuzil", new[] { rifle, smg, pistol }, 0), ("pistola", new[] { rifle, smg, pistol }, 2), ("smg", new[] { rifle, smg, pistol }, 1), ("escopeta", new[] { sg, smg, pistol }, 0), ("sniper", new[] { sn, smg, pistol }, 0) };
            if (setArg != "all") { var want = new HashSet<string>(setArg.Split(',')); sets = sets.Where(s => want.Contains(s.id)).ToList(); }
            var csv = new System.Text.StringBuilder("conjunto;estado;personagem;erro_mira_p95_graus;mao_esq_p95_cm;estalo_p99_graus_s;estalo_max;cotovelo_min_graus;joelho_invertido_quadros;estiramento_max;estiramento_frac;compressao_min;osso_pior;osso_estalo;coldre_dentro_cm;coldre_pior;coldre_folga_cm;coldre_folga_pior;ritmo_passo_med;clipe_base;estiramento_firme;osso_firme;aresta_bind_mm;aresta_deformada_mm;arma_acima_cabeca_cm;arma_acima_qual\n");
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            bool first = true;
            foreach (var (setId, give, slot) in sets)
            {
                foreach (var a in actors) { a.Leave(); if (give == null) a.wu.Strip(); else { a.wu.Strip(); a.wu.Give(give.Where(x => x != null).ToArray()); a.wu.Equip(slot >= FBWeaponUser.HANDS ? FBWeaponUser.HANDS : a.wu.slots[slot] != null ? slot : 0, true); } }
                for (float w = 0; w < .8f; w += Time.deltaTime) yield return null;
                var aa = System.Environment.GetCommandLineArgs(); int ei = System.Array.IndexOf(aa, "-fblabestados"); var only = ei >= 0 && ei + 1 < aa.Length ? new HashSet<string>(aa[ei + 1].Split(',')) : null;   // r42: só estes estados
                foreach (var st in States(setId).Where(x => only == null || only.Contains(x.id)))
                {
                    // o 1º estado da sessão saía vazio nas vistas de frente/lado (personagens fora do lugar no 1º ciclo): roda uma vez sem medir
                    if (first) { first = false; foreach (var a in actors) { a.Leave(); a.st = st; a.t = 0; } for (float w0 = 0; w0 < st.len; w0 += Time.deltaTime) yield return null; foreach (var a in actors) a.Leave(); yield return null; }
                    var ms = chars.ToDictionary(c => c, c => new M());
                    foreach (var a in actors) { a.Leave(); a.st = st; a.t = 0; }
                    float t = 0; bool shot = false; int si = 0; float[] SF = { .3f, .5f, .7f, .9f };
                    while (t < st.len)
                    {
                        yield return null; float dt = Time.deltaTime; t += dt; bool settle = t > .6f;
                        foreach (var c in chars) if (c) Sample(c, ms[c], dt, settle);
                        // arma guardada x corpo em 4 momentos do estado (não só no quadro do print): dentro e afastada
                        if (settle && si < SF.Length && t >= st.len * SF[si])
                        {
                            si++; yield return new WaitForEndOfFrame();
                            foreach (var c in chars) if (c) Holster(c, ms[c]);
                        }
                        if (!shot && t >= st.len * st.shotAt) { shot = true; yield return new WaitForEndOfFrame(); foreach (var c in chars) if (c) { Stretch(c, ms[c]); Holster(c, ms[c]); } Capture($"{setId}_{st.id}", st.views); }
                    }
                    float aimW = 0, lhW = 0, popW = 0, strW = 0, firW = 0, holW = 0, gapW = 0, rLo = 9, rHi = 0; string wc = "-", fc = "-", hw = "-", rc = "-", gw = "-"; float wl0 = 0, wl1 = 0, abvW = 0; string ac = "-";
                    foreach (var c in chars)
                    {
                        var m = ms[c]; float a95 = P(m.aim, .95f), l95 = P(m.lh, .95f) * 100, p99 = P(m.pop, .99f);
                        csv.AppendLine(string.Join(";", setId, st.id, c.skinId, a95.ToString("0.0", ic), l95.ToString("0.0", ic), p99.ToString("0", ic), P(m.pop, 1).ToString("0", ic), m.elbowMin.ToString("0", ic), m.kneeRev.ToString("0", ic), m.stretchMax.ToString("0.00", ic), (m.stretchFrac * 100).ToString("0.00", ic), m.squashMin.ToString("0.00", ic), m.worstBone, m.popBone, m.holsterCm.ToString("0.0", ic), m.holsterWhat, m.gapCm.ToString("0.0", ic), m.gapWhat, P(m.rate, .5f).ToString("0.00", ic), m.clips.Count > 0 ? m.clips.OrderByDescending(k => k.Value).First().Key : "-", m.stretchFirme.ToString("0.00", ic), m.firmeBone, m.worstLen0.ToString("0.000", ic), m.worstLen1.ToString("0.000", ic), m.aboveCm.ToString("0.0", ic), m.aboveWhat));
                        aimW = Mathf.Max(aimW, a95); lhW = Mathf.Max(lhW, l95); popW = Mathf.Max(popW, p99); if (m.stretchMax > strW) { strW = m.stretchMax; wc = c.skinId + "/" + m.worstBone; wl0 = m.worstLen0; wl1 = m.worstLen1; } if (m.stretchFirme > firW) { firW = m.stretchFirme; fc = c.skinId + "/" + m.firmeBone; } if (m.aboveCm > abvW) { abvW = m.aboveCm; ac = c.skinId + "/" + m.aboveWhat; } if (m.holsterCm > holW) { holW = m.holsterCm; hw = c.skinId + "/" + m.holsterWhat; } if (m.gapCm > gapW) { gapW = m.gapCm; gw = c.skinId + "/" + m.gapWhat; } if (m.rate.Count > 0) { float rm = P(m.rate, .5f); if (rm < rLo) { rLo = rm; rc = m.clips.OrderByDescending(k => k.Value).First().Key; } rHi = Mathf.Max(rHi, rm); }
                    }
                    Debug.Log($"[FreeBala][lab] {setId}/{st.id}: pior erro de mira {aimW:0.0}° · pior mão esq. {lhW:0.0} cm · pior estalo p99 {popW:0}°/s · pior estiramento {strW:0.00}× ({wc}, aresta {wl0:0.00}→{wl1:0.00} mm) · FIRME {firW:0.00}× ({fc}) · arma guardada dentro do corpo {holW:0.0} cm ({hw}) · maior afastamento {gapW:0.0} cm ({gw}) · acima da cabeça {abvW:0.0} cm ({ac}){(rHi > 0 ? $" · ritmo do passo {rLo:0.00}–{rHi:0.00} (mais lento: {rc})" : "")}");
                    Debug.Log($"[FreeBala][lab] {setId}/{st.id} rig (modo {FBRig.Mode}): ergueu {FBRig.LiftN} quadros (média {(FBRig.LiftN > 0 ? FBRig.LiftSum / FBRig.LiftN : 0):0.0}°) · centralizou {FBRig.CenterN} quadros · diag {FBRig.DbgN} q: cru {FBRig.DbgRaw / Mathf.Max(1, FBRig.DbgN):0.0}° punho {FBRig.DbgWrist / Mathf.Max(1, FBRig.DbgN):0.0}° corpo {FBRig.DbgClear / Mathf.Max(1, FBRig.DbgN):0.0}° fim {FBRig.DbgLeft / Mathf.Max(1, FBRig.DbgN):0.0}° · low ready {FBRig.DbgLower / Mathf.Max(1, FBRig.DbgN):0.0}° · empurrão {100 * FBRig.DbgPush / Mathf.Max(1, FBRig.DbgN):0.0} cm · punho no limite {FBRig.DbgClamp} · longe {FBRig.DbgFar}"); FBRig.LiftN = FBRig.CenterN = 0; FBRig.LiftSum = 0; FBRig.DbgN = FBRig.DbgClamp = FBRig.DbgFar = 0; FBRig.DbgRaw = FBRig.DbgWrist = FBRig.DbgClear = FBRig.DbgLeft = FBRig.DbgLower = FBRig.DbgPush = 0;
                }
                foreach (var a in actors) a.Leave();
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "lab_metricas.csv"), csv.ToString());

            // ---- terreno: caminhantes nas rotas do foot IK (masculino, feminino, corpo largo) desarmados e de fuzil
            foreach (var c in chars) if (c) c.gameObject.SetActive(false);
            FBFootIK.Q.Lock = true;
            var picks = new[] { FBSession.DefaultSkin, GameData.Skins.FirstOrDefault(s => s.id.StartsWith("r_") && s.sex == "f")?.id, "r_aka_junin" }.Where(x => x != null && reg.HasChar(x)).Distinct().ToList();
            var tlog = new System.Text.StringBuilder();
            foreach (var skin in picks) foreach (var armed in new[] { false, true })
                {
                    var walkers = new List<FBCharacter>(); var meters = new Dictionary<FBCharacter, FBFootIKMeter>();
                    foreach (var r in FBFootIKTestBed.Routes)
                    {
                        var w = FBFootIKTestBed.SpawnWalker(skin, r); if (!w) continue; walkers.Add(w); meters[w] = new FBFootIKMeter();
                        if (armed) { var wu = w.gameObject.AddComponent<FBWeaponUser>(); wu.Give(rifle); w.combat = true; }
                    }
                    for (float t = 0; t < 5; t += Time.deltaTime) { yield return null; foreach (var w in walkers) if (w) meters[w].Sample(w, Time.deltaTime); }
                    yield return new WaitForEndOfFrame();
                    chars.Clear(); chars.AddRange(walkers); Capture($"terreno_{skin}_{(armed ? "fuzil" : "desarmado")}", "e");
                    for (int k = 0; k < walkers.Count; k++) tlog.AppendLine($"{skin} {(armed ? "fuzil" : "desarmado")} {FBFootIKTestBed.Routes[k].slug}: {meters[walkers[k]].Report()}");
                    foreach (var w in walkers) if (w) Destroy(w.gameObject); chars.Clear(); yield return null;
                }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "lab_terreno.txt"), tlog.ToString());
            Debug.Log("[FreeBala][lab] fim · " + dir);
            yield return null; Application.Quit(0);
        }
    }

}
