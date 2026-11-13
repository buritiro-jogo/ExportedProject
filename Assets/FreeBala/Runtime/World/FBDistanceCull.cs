// CORTE POR DISTÂNCIA dos detalhes do mapa (rodada 40). Medido no passeio da Vila Jardim inteira (-fbtour): 7–29 mil draw calls e 10–21 mi
// de triângulos por quadro — o mapa todo era desenhado de qualquer distância (câmera até 1 400 m; os LODGroups de corte do FBOptimize foram
// desligados porque o glTF assa as malhas em coordenadas do mundo e o centro do LODGroup ficava errado). Aqui: só os renderers PEQUENOS e
// MÉDIOS do mapa (splits, mato, coberturas, carros parados, árvores, decalques) apagam longe; prédios, ruas e terreno ficam sempre.
// Só forceRenderingOff (colisão continua; não briga com vidro/destrutível que desligam renderer.enabled). Histerese de 10 % (sem piscar).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public class FBDistanceCull : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }
        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (!s.IsValid() || !s.name.StartsWith("Map_")) return;
            foreach (var root in s.GetRootGameObjects())
                if (root.name.StartsWith("Map_") && !root.GetComponent<FBDistanceCull>()) root.AddComponent<FBDistanceCull>();
        }
        public static float SmallDist => FBQuality.Mobile ? 90 : 180;     // raio <= 2,5 m
        public static float MidDist => FBQuality.Mobile ? 180 : 360;      // raio <= 6 m
        // postes e fios: o FBPolePrep corta a rede em ~600 pedaços (luminárias acesas) e cada um vale 2–3 draw calls; perto do Andiroba eram
        // 1 500–1 800 desenhos de quase nada (passeio r40). Fio de 30 m não é "pequeno", por isso a regra pelo nome.
        public static float PoleDist => FBQuality.Mobile ? 110 : 180;
        static bool IsPole(Transform t) { for (; t; t = t.parent) { var n = t.name; if (n.Contains("Postes") || n.Contains("Fios") || n.StartsWith("VJ_Poste") || n.Contains("poste_br")) return true; } return false; }
        // (r41) Vila Jardim inteira ACESSÍVEL: cada bloco do OSM tem o modelo completo "<bloco>_PERTO" (portas, escada, apartamentos,
        // colide) e a casca leve "<bloco>_LONGE_nocol" (sem colisão). Os dois ficam num grupo só, com UM estado: perto = completo, longe =
        // casca (nunca os dois juntos: as paredes coincidem e cintilariam). Interiores ("_interior") só perto; mato juntado (VJ11_Mato) a 150 m.
        public static float NearDist => FBQuality.Mobile ? 90 : 120;
        public static float InteriorDist => FBQuality.Mobile ? 40 : 60;
        public static float MatoDist => FBQuality.Mobile ? 90 : 150;
        class Item { public Renderer[] on, off; public Bounds b; public float d2; public bool state = true; }
        Item[] items; float t; int i0;
        public static int Count { get; private set; }
        public static int Hidden { get; private set; }
        static string Tag(Transform t, out string kind)
        {
            for (; t; t = t.parent)
            {
                var n = t.name; int k;
                if ((k = n.IndexOf("_PERTO", System.StringComparison.Ordinal)) > 0) { kind = "perto"; return n.Substring(0, k); }
                if ((k = n.IndexOf("_LONGE", System.StringComparison.Ordinal)) > 0) { kind = "longe"; return n.Substring(0, k); }
                if (n.EndsWith("_interior")) { kind = "interior"; return n; }
                if (n.StartsWith("VJ11_Mato")) { kind = "mato"; return n; }
            }
            kind = null; return null;
        }
        void Start()
        {
            var list = new List<Item>(); var groups = new Dictionary<string, (List<Renderer> on, List<Renderer> off, string kind)>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer || r.GetComponentInParent<LODGroup>()) continue;
                var tag = Tag(r.transform, out var kind);
                if (tag != null)
                {
                    if (!groups.TryGetValue(tag, out var g)) groups[tag] = g = (new List<Renderer>(), new List<Renderer>(), kind == "longe" ? "perto" : kind);
                    (kind == "longe" ? g.off : g.on).Add(r); continue;
                }
                float rad = r.bounds.extents.magnitude; bool pole = IsPole(r.transform);
                if (pole && r.sharedMaterials.Any(m => m && m.IsKeywordEnabled("_EMISSION"))) continue;   // cabeça de luminária: o brilho de longe à noite fica
                if (rad > 6 && !pole) continue;
                float d = pole ? PoleDist : rad <= 2.5f ? SmallDist : MidDist;
                list.Add(new Item { on = new[] { r }, off = new Renderer[0], b = r.bounds, d2 = d * d });
            }
            foreach (var g in groups.Values)
            {
                var all = g.on.Concat(g.off).ToList(); var b = all[0].bounds; foreach (var r in all) b.Encapsulate(r.bounds);
                float d = g.kind == "interior" ? InteriorDist : g.kind == "mato" ? MatoDist : NearDist;
                var it = new Item { on = g.on.ToArray(), off = g.off.ToArray(), b = b, d2 = d * d };
                foreach (var r in it.off) r.forceRenderingOff = true;   // começa "perto"; o primeiro Update acerta
                list.Add(it);
            }
            items = list.ToArray(); Count = items.Length;
        }
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update do corte por distância
        static readonly int __fbt_Update = FBTempo.Id("FBDistanceCull.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (items == null || items.Length == 0) return;
            var cam = Camera.main; if (!cam) return; var p = cam.transform.position;
            // espalha o trabalho: 1/6 da lista por quadro (a lista toda a cada ~0,1–0,3 s)
            int n = Mathf.Max(1, items.Length / 6), hid = 0;
            for (int k = 0; k < n; k++)
            {
                var it = items[(i0 + k) % items.Length];
                float d2 = it.b.SqrDistance(p); bool want = it.state ? d2 < it.d2 * 1.21f : d2 < it.d2;   // distância até a borda (fio comprido não some com a câmera embaixo dele); histerese de 10 %
                if (want != it.state) Apply(it, want);
            }
            i0 = (i0 + n) % items.Length;
            if ((t += Time.unscaledDeltaTime) > 1) { t = 0; foreach (var it in items) if (!it.state) hid++; Hidden = hid; }
        }
        static void Apply(Item it, bool near)
        {
            it.state = near;
            foreach (var r in it.on) if (r) r.forceRenderingOff = !near;
            foreach (var r in it.off) if (r) r.forceRenderingOff = near;
        }
        // a câmera pulou (teleporte, autoteste, respawn): acerta tudo de uma vez em vez de esperar as ~6 fatias
        public static void Refresh() { foreach (var c in FindObjectsByType<FBDistanceCull>(FindObjectsSortMode.None)) c.RefreshAll(); }
        void RefreshAll()
        {
            var cam = Camera.main; if (!cam || items == null) return; var p = cam.transform.position;
            foreach (var it in items) { bool want = it.b.SqrDistance(p) < it.d2; if (want != it.state) Apply(it, want); }
        }
        void OnDestroy() { if (items != null) foreach (var it in items) { foreach (var r in it.on) if (r) r.forceRenderingOff = false; foreach (var r in it.off) if (r) r.forceRenderingOff = false; } }
    }

    // CARROS (rodada 40): o passeio (-fbtour) achou os 37 carros do rolê desenhados por inteiro e com sombra a 300–400 m (o LODGroup só corta
    // abaixo de 0,4–0,6 % da tela). Como o carLOD() do web: sombra só perto, carro inteiro some longe; o do jogador nunca. forceRenderingOff +
    // shadowCastingMode guardado (não briga com quem liga/desliga renderer.enabled: luzes, eventos de rua, oficina).
    public class FBCarCull : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { if (I) return; var g = new GameObject("FBCarCull"); DontDestroyOnLoad(g); I = g.AddComponent<FBCarCull>(); }
        static FBCarCull I;
        public static float HideDist => FBQuality.Mobile ? 150 : 260;
        public static float ShadowDist => FBQuality.Mobile ? 25 : FBVisual.C.carShadowDist > 0 ? FBVisual.C.carShadowDist : 45;   // r65: 90 m no PC (a sombra do carro nascia a 45 m, na frente do jogador)
        class St { public Renderer[] rs; public UnityEngine.Rendering.ShadowCastingMode[] sh; public float t; public bool hid, noSh; public FBCharacter drv; public bool drvHid; }
        readonly Dictionary<FBCar, St> st = new();
        public static int Hidden { get; private set; }
        float tick;
        // [parceiro 01/11] medidor de tempo por sistema (FBTempo): com FBTempo.On soma o custo do Update do corte de carros
        static readonly int __fbt_Update = FBTempo.Id("FBCarCull.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if ((tick += Time.unscaledDeltaTime) < .2f) return; tick = 0;
            /* r45e: sem Camera.main (câmera do jogador desligada: testes, cutscene) mede pelo jogador — antes parava de atualizar e a moto
               escondida longe continuava invisível do lado do jogador (racha com os pilotos "montados no ar") */
            var cam = Camera.main; var me0 = FBMatch.Player; if (!cam && !me0) return; var p = cam ? cam.transform.position : me0.Pos; var me = FBDrive.Cur; int hid = 0;
            foreach (var car in FBVehicles.list)
            {
                if (!car) continue;
                if (!st.TryGetValue(car, out var s) || Time.time - s.t > 3) { if (s != null) { Apply(s, false, false); HideChar(s.drv, false); } s = Snap(car); st[car] = s; }   // peças novas (lentes, dano)
                float d = Vector3.Distance(car.transform.position, p); if (me0) d = Mathf.Min(d, Vector3.Distance(car.transform.position, me0.Pos)); bool mine = car == me;
                bool hide = !mine && (s.hid ? d > HideDist * .9f : d > HideDist), noSh = !mine && (s.noSh ? d > ShadowDist * .9f : d > ShadowDist);
                if (hide != s.hid || noSh != s.noSh) Apply(s, hide, noSh);
                // r45e: o piloto/motorista some junto (antes a moto sumia a 260 m e o piloto ficava "montado no ar")
                var dv = car.driver && !car.driver.isPlayer ? car.driver : null;
                if (dv != s.drv || (dv && s.drvHid != s.hid)) { if (s.drv && s.drv != dv) HideChar(s.drv, false); s.drv = dv; s.drvHid = s.hid; if (dv) HideChar(dv, s.hid); }
                if (s.hid) hid++;
            }
            Hidden = hid;
            if (st.Count > FBVehicles.list.Count + 8) foreach (var k in new List<FBCar>(st.Keys)) if (!k) st.Remove(k);
        }
        static void HideChar(FBCharacter c, bool off) { if (!c) return; foreach (var r in c.GetComponentsInChildren<Renderer>(true)) if (r && !(r is ParticleSystemRenderer)) r.forceRenderingOff = off; }
        static St Snap(FBCar car)
        {
            var rs = car.GetComponentsInChildren<Renderer>(true); var sh = new UnityEngine.Rendering.ShadowCastingMode[rs.Length];
            for (int i = 0; i < rs.Length; i++) sh[i] = rs[i].shadowCastingMode;
            return new St { rs = rs, sh = sh, t = Time.time };
        }
        static void Apply(St s, bool hide, bool noSh)
        {
            s.hid = hide; s.noSh = noSh;
            for (int i = 0; i < s.rs.Length; i++)
            {
                var r = s.rs[i]; if (!r || r is ParticleSystemRenderer) continue; r.forceRenderingOff = hide;
                r.shadowCastingMode = noSh && s.sh[i] == UnityEngine.Rendering.ShadowCastingMode.On ? UnityEngine.Rendering.ShadowCastingMode.Off : s.sh[i];
            }
        }
    }
}
