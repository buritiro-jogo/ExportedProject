// OBJETOS SOLTOS COM FÍSICA + ATROPELAMENTO (rodada 44 — pedido 28/10: "comportamentos físicos apropriados para cones, mesas, cadeiras,
// placas, objetos menores ... que possam cair, tombar, rolar ou ser deslocados. Reações a colisões, sons de impacto e propriedades físicas
// compatíveis com o peso e o material ... Atropelamentos: física, rigs, animações e efeitos sonoros").
// O 171 usa corpos simulados nos props de evento (cones, caixotes, cadeiras de plástico, garrafas — BP_Cooler/BP_GarrafaCerveja/BP_Banquinho)
// e um banco FMOD "Fisica" com um som por material (Plastico/Metal/Madeira/Concreto/Papelao/Grade/Lata/Sacola/Cerâmica/Vidro) — os sons
// originais estão em Resources/Audio/171/fisica (tools/re/fisica171.py). Aqui:
//  • FBFisica.Make(objeto, material, kg): Rigidbody com a massa real, colisor de caixa (ou o que já tiver), atrito/quique pelo material,
//    dorme parado (custo zero); veículo bate -> é empurrado pela física (veículos são cinemáticos); personagem esbarra -> empurrão leve;
//    tiro -> impulso no ponto; impacto -> som do material com volume pela energia (sem metralhar: 0,12 s por objeto, 24 sons/s no total).
//  • FBAtropelo.Hit: ragdoll com o impulso do carro (velocidade, direção, altura do para-choque), som de atropelamento + corpo caindo do 171,
//    voz de dor; quem sobrevive levanta (clipe de levantar do 171 / FF, conforme caiu de costas ou de bruços) e foge xingando.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBFisica
    {
        public struct Mat { public float dyn, sta, bounce; public string snd; }
        public static readonly Dictionary<string, Mat> MATS = new()
        {
            ["plastico"] = new Mat { dyn = .45f, sta = .55f, bounce = .25f, snd = "plastico" }, ["metal"] = new Mat { dyn = .35f, sta = .45f, bounce = .12f, snd = "metal" },
            ["metal_pesado"] = new Mat { dyn = .45f, sta = .6f, bounce = .05f, snd = "metal_pesado" }, ["lata"] = new Mat { dyn = .3f, sta = .4f, bounce = .3f, snd = "lata" },
            ["madeira"] = new Mat { dyn = .5f, sta = .6f, bounce = .15f, snd = "madeira" }, ["concreto"] = new Mat { dyn = .7f, sta = .85f, bounce = .02f, snd = "concreto" },
            ["papelao"] = new Mat { dyn = .6f, sta = .7f, bounce = .05f, snd = "papelao" }, ["vidro"] = new Mat { dyn = .3f, sta = .4f, bounce = .1f, snd = "vidro" },
            ["grade"] = new Mat { dyn = .4f, sta = .5f, bounce = .1f, snd = "grade" }, ["sacola"] = new Mat { dyn = .8f, sta = .9f, bounce = 0, snd = "sacola" },
            ["ceramica"] = new Mat { dyn = .5f, sta = .6f, bounce = .08f, snd = "ceramica" },
        };
        static Dictionary<string, List<string>> snd; static float budgetT; static int budget;
        public static readonly List<FBPropBody> All = new(); public static int Impacts;
        static void Load()
        {
            if (snd != null) return; snd = new(); var ta = Resources.Load<TextAsset>("Audio/171/fisica/fisica"); if (!ta) return;
            foreach (var kv in Json.O(Json.Parse(ta.text), "cats") ?? new()) snd[kv.Key] = (kv.Value as List<object> ?? new()).Select(o => o as string).Where(x => x != null).ToList();
        }
        public static bool Sound(string cat, Vector3 p, float vol, float refD = 6)
        {
            Load(); if (Time.time > budgetT) { budgetT = Time.time + 1; budget = 24; } if (budget-- <= 0) return false;
            if (!snd.TryGetValue(cat ?? "", out var l) || l.Count == 0) return false; var clip = Resources.Load<AudioClip>("Audio/171/fisica/" + l[Random.Range(0, l.Count)]); if (!clip) return false;
            var s = FBAudio.PlayAt(clip, p, Mathf.Clamp01(vol), refD, .08f); return s;
        }
        static readonly Dictionary<string, PhysicsMaterial> pms = new();
        /// <summary>Torna um objeto do cenário/evento um corpo físico (cai, tomba, rola, é empurrado) com som do material.</summary>
        public static FBPropBody Make(GameObject g, string mat, float kg)
        {
            if (!g) return null; var rb = g.GetComponent<Rigidbody>(); if (!rb) rb = g.AddComponent<Rigidbody>(); rb.mass = Mathf.Max(.05f, kg); rb.isKinematic = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = .05f; rb.angularDamping = .25f; rb.collisionDetectionMode = kg < 2 ? CollisionDetectionMode.ContinuousSpeculative : CollisionDetectionMode.Discrete; rb.sleepThreshold = .02f;
            if (!g.GetComponentInChildren<Collider>())
            {
                var rs = g.GetComponentsInChildren<Renderer>(); if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); var bc = g.AddComponent<BoxCollider>(); bc.center = g.transform.InverseTransformPoint(b.center); bc.size = Vector3.Max(g.transform.InverseTransformVector(b.size).Abs3(), Vector3.one * .02f); }
            }
            MATS.TryGetValue(mat, out var M); if (M.snd == null) M = MATS["plastico"];
            if (!pms.TryGetValue(mat, out var pm)) pms[mat] = pm = new PhysicsMaterial(mat) { dynamicFriction = M.dyn, staticFriction = M.sta, bounciness = M.bounce, bounceCombine = PhysicsMaterialCombine.Average, frictionCombine = PhysicsMaterialCombine.Average };
            foreach (var c in g.GetComponentsInChildren<Collider>()) { c.sharedMaterial = pm; if (c is MeshCollider mc) mc.convex = true; }
            var pb = g.GetComponent<FBPropBody>(); if (!pb) pb = g.AddComponent<FBPropBody>(); pb.mat = mat; pb.snd = M.snd; pb.rb = rb; rb.Sleep(); return pb;
        }
        /// <summary>Só o som de impacto num corpo que já tem física (escada que cai, destroços).</summary>
        public static void Watch(GameObject g, string mat, float kg) { if (!g) return; var rb = g.GetComponent<Rigidbody>(); if (!rb) { Make(g, mat, kg); return; }
            // [parceiro 31/10] '??' não enxerga o "null falso" do Unity (componente ausente no Editor): testa com o operador do Unity
            var pb = g.GetComponent<FBPropBody>(); if (!pb) pb = g.AddComponent<FBPropBody>(); pb.mat = mat; pb.snd = MATS.TryGetValue(mat, out var M) && M.snd != null ? M.snd : mat; pb.rb = rb; }
        /// <summary>Tiro (FBCombat): impulso no ponto atingido.</summary>
        public static void Shot(Collider c, Vector3 p, Vector3 dir)
        {
            var pb = c ? c.GetComponentInParent<FBPropBody>() : null; if (!pb || !pb.rb || pb.rb.isKinematic) return;
            pb.rb.WakeUp(); pb.rb.AddForceAtPosition(dir.normalized * Mathf.Clamp(pb.rb.mass * 3.5f, 2, 40), p, ForceMode.Impulse); Sound(pb.snd, p, .6f, 6);
        }
        public static Vector3 Abs3(this Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        // r45 (pedido: "placas, postes, lixeiras ... destruição baseada em massa e material"): peça FIXA do 171 atingida por veículo cede
        // quando a velocidade passa do limite dela — vira corpo físico com o impulso da batida (tomba, rola), som de metal do 171 e some
        // fora da vista (Debris). Abaixo do limite continua segurando o carro. Devolve a fração da velocidade que o carro perde.
        static readonly (string pre, float vMin, float kg, string mat)[] FIXOS =
        {
            ("x171_Placas", 4.5f, 18, "metal"), ("x171_LixeiraPoste", 4f, 14, "metal"), ("x171_SM_Caixadoscorreios", 6, 40, "metal_pesado"),
            ("x171_SM_Orelhao", 7, 60, "metal_pesado"), ("x171_PontoOnibus", 10, 180, "metal_pesado"), ("x171_SM_PontoOnibus", 10, 180, "metal_pesado"),
        };
        public static int Knocked;
        public static float Knock(Collider c, Vector3 dir, float v, Vector3 at)
        {
            if (!c || c.attachedRigidbody) return -1; var t = c.transform; GameObject go = null; (string pre, float vMin, float kg, string mat) f = default;
            for (var x = t; x && go == null; x = x.parent) foreach (var F in FIXOS) if (x.name.StartsWith(F.pre)) { go = x.gameObject; f = F; break; }
            if (!go || v < f.vMin) return -1;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 0;
            var pb = Make(go, f.mat, f.kg); pb.rb.WakeUp(); pb.rb.linearVelocity = dir.normalized * v * Mathf.Clamp(1.4f - f.kg / 150, .3f, 1.2f) + Vector3.up * 1.2f;
            pb.rb.AddForceAtPosition(dir.normalized * f.kg * v * .3f, go.transform.position + Vector3.up * 1.6f, ForceMode.Impulse);   // empurrão no alto: tomba
            Sound(f.mat == "metal" ? "metal" : "metal_pesado", at, .9f, 8); FBFx.Sparks(at, -dir); Debris(go, 60); Knocked++;
            return Mathf.Clamp(f.kg / 400f, .05f, .45f);
        }
        // r45: destroços/fragmentos (peça de carro, pedaços de placa/cerca/lixeira) somem depois de 'life' s, só quando ninguém está vendo
        // (fora da tela ou a mais de 35 m) — e no máximo 60 ao mesmo tempo (o mais velho sai primeiro)
        static readonly List<(GameObject g, float t)> debris = new(); static DebrisRunner dr;
        public static int DebrisCount => debris.Count;
        public static void Debris(GameObject g, float life)
        {
            if (!g) return; debris.Add((g, Time.time + life)); if (!dr) { var o = new GameObject("FB_Destrocos"); Object.DontDestroyOnLoad(o); dr = o.AddComponent<DebrisRunner>(); }
            while (debris.Count > 60) { if (debris[0].g) Object.Destroy(debris[0].g); debris.RemoveAt(0); }
        }
        class DebrisRunner : MonoBehaviour
        {
            float chk;
            // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update do coletor de destroços
            static readonly int __fbt_Update = FBTempo.Id("FBFisica.DebrisRunner.Update");
            void Update()
            {
                if (!FBTempo.On) { Update__fbt(); return; }
                long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
            }
            void Update__fbt()
            {
                if ((chk -= Time.deltaTime) > 0) return; chk = 1; var cam = Camera.main;
                for (int i = debris.Count - 1; i >= 0; i--)
                {
                    var (g, t) = debris[i]; if (!g) { debris.RemoveAt(i); continue; } if (Time.time < t) continue;
                    bool seen = false; if (cam) { var vp = cam.WorldToViewportPoint(g.transform.position); seen = vp.z > 0 && vp.z < 35 && vp.x > -.1f && vp.x < 1.1f && vp.y > -.1f && vp.y < 1.1f; }
                    if (!seen || Time.time > t + 90) { Object.Destroy(g); debris.RemoveAt(i); }
                }
            }
        }
    }

    public class FBPropBody : MonoBehaviour
    {
        public string mat, snd; public Rigidbody rb; float lastT; static readonly Collider[] near = new Collider[8];
        void OnEnable() { FBFisica.All.Add(this); }
        void OnDisable() { FBFisica.All.Remove(this); }
        void OnCollisionEnter(Collision c)
        {
            if (!rb || Time.time - lastT < .12f) return; float e = c.impulse.magnitude / Mathf.Max(.2f, rb.mass); if (e < .45f) return;   // Δv do impacto (m/s)
            lastT = Time.time; FBFisica.Impacts++; var p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
            FBFisica.Sound(snd, p, Mathf.Clamp01(.15f + e * .12f) * Mathf.Clamp(rb.mass / 4, .5f, 1.2f), 4 + Mathf.Min(6, rb.mass * .5f));
        }
        // personagem (CharacterController não empurra corpos): esbarrão empurra pela velocidade dele
        float chkT;
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do FixedUpdate de todos os corpos físicos
        static readonly int __fbt_FixedUpdate = FBTempo.Id("FBPropBody.FixedUpdate");
        void FixedUpdate()
        {
            if (!FBTempo.On) { FixedUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); FixedUpdate__fbt(); FBTempo.Soma(__fbt_FixedUpdate, t0);
        }
        void FixedUpdate__fbt()
        {
            if (!rb || rb.isKinematic || (chkT -= Time.fixedDeltaTime) > 0) return; chkT = .1f; var b = transform.position;
            int n = Physics.OverlapSphereNonAlloc(b, .9f, near, 1 << FBWorld.LChar, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var ch = near[i].GetComponentInParent<FBCharacter>(); if (!ch || ch.vehiclePose) continue; var v = ch.velocity.Flat();
                // [parceiro] fisica 31/10: bot publica em ch.velocity o deslocamento real (~0 quando o objeto o trava); empurra com a velocidade pedida
                var bot = ch.GetComponent<FBBot>(); if (bot && bot.enabled) { var wish = bot.WishVel; if (wish.sqrMagnitude > v.sqrMagnitude) v = wish; }
                if (v.sqrMagnitude < .5f) continue;
                var d = (b - ch.Pos).Flat(); if (Vector3.Dot(d, v) <= 0 || d.magnitude > .75f) continue;
                rb.WakeUp(); rb.AddForceAtPosition(v.normalized * Mathf.Min(v.magnitude, 6) * Mathf.Min(rb.mass, 12) * .35f, b + Vector3.up * .35f, ForceMode.Impulse);
            }
        }
    }

    public static class FBAtropelo
    {
        public static int Hits, GotUp; public static string Last;
        /// <summary>Carro/moto pegou 'a' a 'v' (u/s) na direção 'f'. Ragdoll com o impulso; dano fora do rolê; levanta se viver.</summary>
        public static void Hit(FBCharacter a, FBCar car, float v, Vector3 f, bool damage)
        {
            if (!a || !a.alive) return; float sp = Mathf.Abs(v); Hits++; Last = $"{a.skinId} a {sp * 3.6f / 1.2f:0} km/h";
            FBFisica.Sound("atropelamento", a.Pos + Vector3.up * .8f, Mathf.Clamp01(.4f + sp / 20), 9); FBFisica.Sound("corpo", a.Pos, .8f, 7);
            FBAudio.PlayAt("ie_bodyhit", a.Pos, .8f, 5); FBNpcVoz.Say(a, "atropelado", 1, 6, 4, 0);
            bool kill = damage && sp * 6.5f >= a.hp;
            if (damage) FBCombat.DamageActor(a, sp * 6.5f, false, car ? car.driver : null, "car");
            if (!a.alive || kill) return;                                                                   // morreu: o FBDeath cuida (ragdoll direto por carro)
            if (sp < 3.2f || a.isPlayer) { a.velocity += f * sp * .45f + Vector3.up * 2.2f; a.grounded = false; return; }   // devagar: só empurra (e o jogador nunca vira ragdoll)
            var imp = (f * sp * 1.1f + Vector3.up * (1.5f + sp * .25f)) * 1.0f;
            // r46: ragdoll VIVO (FBFall.Knock). Antes: Go() num personagem vivo -> o FBRagdoll desmontava no quadro seguinte e o FBKnockdown
            // escolhia o levantar pelo eixo local do osso do quadril (depende do rig) e tocava o FF_SYS_KO_GetUp "de costas" — medido: ele começa de bruços
            if (!FBFall.Knock(a, imp, a.Pos + Vector3.up * .55f * a.ScaleK + f * -.2f, Mathf.Lerp(1.2f, 2.5f, Mathf.Clamp01(sp / 15)), car)) a.velocity += f * sp * .6f + Vector3.up * 3;
        }
    }

}
