// Passos por piso para TODOS os personagens (jogador, bots, remotos, civis/NPCs) — porte do footsteps()/ieStep() do main.js, agora
// no CONTATO REAL DO PÉ: lê os ossos LeftFoot/RightFoot do Animator de cada personagem por fora (sem mexer no FBCharacter) e o
// FBFootContact dispara quando o pé desce até o chão; sem ossos ou com o Animator parado (culling) cai para passadas por distância.
// • Piso: água (FBWorld.WaterAt) → vidro quebrado no chão (perto de FBGlass quebrado) → colisor abaixo do pé (FBSurface.kind +
//   nome do colisor/material do submesh) → FBStepSurfaces (concreto, asfalto, terra, grama, areia, lama, metal, madeira, água, vidro).
// • Volume pela velocidade (agachado / andando / correndo) e por piso; altura variada (correndo um pouco mais aguda); variação sem repetir.
// • Reprodução: pool próprio (12 fontes 3D, 8 no celular) com rolloff "inverse" (ref 3 m), corte a 40 m; cheio → rouba a mais distante.
//   O jogador local ouve os próprios passos em 2D (como no web). Pouso de pulo/queda: pisada dupla mais forte no piso + baque.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBFootsteps : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (I) return; var g = new GameObject("FB_Passos"); DontDestroyOnLoad(g); I = g.AddComponent<FBFootsteps>(); }
        public static FBFootsteps I { get; private set; }
        public static float Volume = 1;          // mestre dos passos (opção futura no menu)
        public const float MaxDist = 40;

        // diagnóstico (-fbaudio): personagem, piso, clipe, volume, velocidade, modo ("osso"/"passada"/"pouso"), distância
        public struct Trace { public FBCharacter ch; public FBStepSurf surf; public string clip, mode; public float vol, speed, dist; public int foot; }
        public static System.Action<Trace> trace;

        class St
        {
            public Transform lf, rf; public bool hasBones; public FBFootContact c = new(); public Vector3 prev; public float speed, airT; public bool wasGround = true, init;
            public readonly int[] last = new int[16];
        }
        readonly Dictionary<FBCharacter, St> st = new();
        readonly List<AudioSource> pool = new(); AudioSource own;
        readonly Dictionary<string, int> counts = new();
        static readonly List<FBGlass> broken = new(); float glassScan;

        void Awake()
        {
            int n = Application.isMobilePlatform ? 8 : 12;
            for (int i = 0; i < n; i++)
            {
                var s = new GameObject("Passo" + i).AddComponent<AudioSource>(); s.transform.SetParent(transform); s.playOnAwake = false; s.spatialBlend = 1;
                s.rolloffMode = AudioRolloffMode.Custom; s.maxDistance = 400; s.dopplerLevel = 0; s.priority = 160; s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.Rolloff(3, 1.3f));
                s.outputAudioMixerGroup = FBAudio.Group(false); pool.Add(s);
            }
            own = gameObject.AddComponent<AudioSource>(); own.playOnAwake = false; own.spatialBlend = 0; own.priority = 100; own.outputAudioMixerGroup = FBAudio.Group(false);
        }

        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBFootsteps.LateUpdate");
        void LateUpdate()   // depois da animação (e do foot IK) do quadro
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!FBMatch.I) { if (st.Count > 0) st.Clear(); return; }
            float dt = Time.deltaTime; if (dt <= 0) return;
            var lis = Listener();
            if ((glassScan -= dt) <= 0) { glassScan = 2; broken.Clear(); foreach (var g in FindObjectsByType<FBGlass>(FindObjectsSortMode.None)) if (g.broken) broken.Add(g); }
            foreach (var ch in FBMatch.All)
            {
                if (!ch) continue;
                if (!st.TryGetValue(ch, out var s)) st[ch] = s = Make(ch);
                var pos = ch.transform.position;
                if (!s.init) { s.prev = pos; s.init = true; continue; }
                var dv = pos - s.prev; dv.y = 0; s.prev = pos;
                float inst = dv.magnitude / dt; if (inst > 25) inst = 0;                     // teleporte/respawn não é passo
                s.speed = Mathf.Lerp(s.speed, inst, 1 - Mathf.Exp(-dt / .12f));
                bool active = ch.alive && ch.sys == null && !InVehicle(ch);
                float dist = Vector3.Distance(lis, pos);
                if (!active || dist > MaxDist) { s.c.Reset(); s.airT = 0; s.wasGround = true; continue; }
                bool ground = ch.grounded;
                // pouso: pulo/queda de verdade (≥ 0,35 s no ar)
                if (!ground) s.airT += dt;
                else if (!s.wasGround && s.airT > .35f) Land(ch, s, dist, s.airT);
                if (ground) s.airT = 0; s.wasGround = ground;
                float k = 1f / Mathf.Max(.01f, ch.ScaleK);
                float hl = s.hasBones && s.lf ? (s.lf.position.y - pos.y) * k : 0, hr = s.hasBones && s.rf ? (s.rf.position.y - pos.y) * k : 0;
                bool run = ch.sprint || s.speed > 3.6f;
                int foot = s.c.Update(dt, s.hasBones && s.lf && s.rf, hl, hr, s.speed, ground, run);
                if (foot >= 0) Step(ch, s, foot, dist, run, s.c.Fallback ? "passada" : "osso");
            }
            if (Time.frameCount % 300 == 0) { var dead = new List<FBCharacter>(); foreach (var kv in st) if (!kv.Key) dead.Add(kv.Key); foreach (var d in dead) st.Remove(d); }
        }

        static St Make(FBCharacter ch)
        {
            var s = new St(); var an = ch.an ? ch.an : ch.GetComponent<Animator>();
            if (an && an.isHuman) { s.lf = an.GetBoneTransform(HumanBodyBones.LeftFoot); s.rf = an.GetBoneTransform(HumanBodyBones.RightFoot); s.hasBones = s.lf && s.rf; }
            for (int i = 0; i < s.last.Length; i++) s.last[i] = -1; return s;
        }
        // no carro/moto (pedido 26/10: "o barulho dos passos está saindo junto com o som do carro"): o motorista NÃO é filho do carro
        // (o FBDrive só posiciona o corpo), então o teste do pai falhava e a velocidade do carro virava passada
        static bool InVehicle(FBCharacter ch) { if (ch.vehiclePose || FBVehicles.CarOf(ch)) return true; var p = ch.transform.parent; return p && p.GetComponentInParent<FBCar>(); }
        static Vector3 Listener() { var l = FindAnyObjectByType<AudioListener>(); return l ? l.transform.position : Camera.main ? Camera.main.transform.position : Vector3.zero; }

        // ---------------- piso sob o pé
        public static FBStepSurf SurfaceAt(Vector3 p)
        {
            if (FBWorld.WaterAt(p.x, p.z) is float wy && p.y < wy + .15f) return FBStepSurf.Agua;
            foreach (var g in broken)
                if (g) { var b = g.transform.position; var d = new Vector2(b.x - p.x, b.z - p.z); if (d.sqrMagnitude < 2.2f * 2.2f && p.y < b.y + .3f && p.y > b.y - 4) return FBStepSurf.Vidro; }
            if (!Physics.Raycast(p + Vector3.up * .4f, Vector3.down, out var h, 1.2f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) return FBStepSurf.Concreto;
            return Classify(h);
        }
        static readonly Dictionary<(Collider, int), FBStepSurf> cache = new();
        public static FBStepSurf Classify(RaycastHit h)
        {
            var c = h.collider; if (!c) return FBStepSurf.Concreto;
            var mc = c as MeshCollider; var mesh = mc ? mc.sharedMesh : null; int sub = 0;
            if (mesh && mesh.subMeshCount > 1 && h.triangleIndex >= 0) { int ix = h.triangleIndex * 3; for (int i = 0; i < mesh.subMeshCount; i++) { var sm = mesh.GetSubMesh(i); if (ix >= sm.indexStart && ix < sm.indexStart + sm.indexCount) { sub = i; break; } } }
            if (cache.TryGetValue((c, sub), out var s)) return s;
            var fs = c.GetComponent<FBSurface>(); var r = c.GetComponent<Renderer>(); var mats = r ? r.sharedMaterials : null;
            string mat = mats != null && sub < mats.Length && mats[sub] ? mats[sub].name : "";
            s = FBStepSurfaces.FromName(c.name + " " + mat, fs ? fs.kind : 0); cache[(c, sub)] = s; return s;
        }

        // ---------------- reprodução
        int Count(FBStepSurf s)
        {
            var b = FBStepSurfaces.ClipBase(s); if (counts.TryGetValue(b, out var n)) return n;
            n = 0; while (n < 16 && FBAudio.Has(b + n)) n++; counts[b] = n; return n;
        }
        AudioClip Pick(St s, FBStepSurf surf, out string name)
        {
            var sf = surf; int n = Count(sf); if (n == 0) { sf = FBStepSurf.Concreto; n = Count(sf); }
            name = null; if (n == 0) return null;
            int i = (int)sf, v = Random.Range(0, n); if (n > 1 && v == s.last[i]) v = (v + 1 + Random.Range(0, n - 1)) % n; s.last[i] = v;
            name = FBStepSurfaces.ClipBase(sf) + v; return FBAudio.Clip(name);
        }
        void Step(FBCharacter ch, St s, int foot, float dist, bool run, string mode, float boost = 1)
        {
            var ft = foot == 0 ? s.lf : s.rf; var p = ft && s.hasBones ? ft.position : ch.transform.position;
            var surf = SurfaceAt(new Vector3(p.x, ch.transform.position.y, p.z)); var clip = Pick(s, surf, out var nm); if (!clip) return;
            bool me = ch.isPlayer && !ch.isRemote, crouch = ch.crouch || ch.prone;
            // volumes do web (pisadas IE): jogador 2D 0,07/0,13/0,20 · outros 3D 0,12/0,25/0,36 · civis ×0,8; banco normalizado → ×1,6
            float v = me ? (crouch ? .07f : run ? .2f : .13f) : (crouch ? .12f : run ? .36f : .25f);
            v *= 1.6f * FBStepSurfaces.Gain(surf) * (ch.civ ? .8f : 1) * Mathf.Lerp(.85f, 1.15f, Mathf.Clamp01((s.speed - 1) / 4)) * boost;
            float pitch = Random.Range(.92f, 1.07f) * (run ? 1.05f : 1) * (crouch ? .96f : 1);
            float vol = Mathf.Clamp01(v * Volume * FBAudio.SfxVol);
            AudioSource src;
            if (me) src = own;
            else
            {
                src = null; float far = -1; AudioSource farS = null; var lis = Listener();
                foreach (var a in pool) { if (!a.isPlaying) { src = a; break; } float d = Vector3.Distance(a.transform.position, lis); if (d > far) { far = d; farS = a; } }
                if (!src) { if (far <= dist) return; src = farS; src.Stop(); }   // pool cheio: só rouba de um passo mais distante
                src.transform.position = p + Vector3.up * .05f;
            }
            src.pitch = pitch; if (me) { src.volume = 1; src.PlayOneShot(clip, vol); } else { src.clip = clip; src.volume = vol; src.Play(); }   // 2D próprio: passos se sobrepõem
            if (run && !crouch && Random.value < .45f)
            {   // correndo: farfalhar de roupa/equipamento junto da pisada (Steps/roupa*, 171) — peso no movimento sem virar barulho
                var rc = FBAudio.Clip("Steps/roupa" + Random.Range(0, 5)); if (rc) src.PlayOneShot(rc, me ? vol * .55f : .8f);   // escala × src.volume (próprio = 1; 3D = vol do passo)
            }
            trace?.Invoke(new Trace { ch = ch, surf = surf, clip = nm, mode = mode, vol = vol, speed = s.speed, dist = me ? 0 : dist, foot = foot });
        }
        // pouso fora do ciclo normal (pulo de janela): os dois pés no piso de verdade
        public static void LandNow(FBCharacter ch, float air)
        {
            Boot(); if (!ch || !I) return; if (!I.st.TryGetValue(ch, out var s)) return; float dist = Camera.main ? Vector3.Distance(Camera.main.transform.position, ch.transform.position) : 0;
            I.Land(ch, s, dist, Mathf.Max(.36f, air)); s.wasGround = true; s.airT = 0;
        }
        void Land(FBCharacter ch, St s, float dist, float air)
        {
            float b = Mathf.Lerp(1.3f, 2.2f, Mathf.Clamp01((air - .35f) / 1.2f));
            Step(ch, s, 0, dist, false, "pouso", b); Step(ch, s, 1, dist, false, "pouso", b * .8f);
            if (!(ch.isPlayer && !ch.isRemote)) FBAudio.PlayAt("ie_land", ch.transform.position, Mathf.Min(.7f, .25f + air * .3f), 4);   // o jogador já toca ie_land no FBPlayer
        }
    }
}
