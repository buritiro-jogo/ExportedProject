// POSTES QUE ENTORTAM / CAEM (pedido do usuário 20/10: "postes devem entortar, quebrar ou cair quando um veículo colidir; permanecer na
// cena; a luz deve apresentar falhas/curto e desligar; as luzes dos postes também destrutíveis por disparos").
//  · FBPole (preparado no editor pelo FBPolePrep: poste individual fora do static batching, grupos de postes partidos em um objeto por
//    poste): batida de carro de 3,5 a 7,5 m/s ENTORTA (inclina 4–24° no sentido do carro, acumula); acima disso QUEBRA no pé e CAI como uma
//    haste girando na base (aceleração da gravidade sobre a haste), parando no chão ou no que estiver no caminho — e fica lá (colisor
//    desligado: não trava o trânsito). O carro passa com parte da velocidade em vez de parar seco.
//  · a luminária daquele poste entra em curto (pisca irregular, faísca) e apaga (FBStreetLights.Kill); tiro na luminária apaga só ela
//    (estilhaço + faísca), mesmo sem colisor na cabeça (raio da bala passando a 30 cm da lâmpada).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBPole : MonoBehaviour
    {
        public Vector3 basePos; public float height = 6;                         // mundo, medido no editor (pé do poste)
        [System.NonSerialized] public float tilt; [System.NonSerialized] public bool falling, down;
        public static readonly List<FBPole> All = new();
        Vector3 axis; float w; Quaternion rot0; Vector3 pos0; bool init; Collider[] cols; readonly List<int> lamps = new(); Vector3 lastTop;
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void Init()
        {
            if (init) return; init = true; rot0 = transform.rotation; pos0 = transform.position; cols = GetComponentsInChildren<Collider>(true);
            var b = new Bounds(basePos + Vector3.up * height * .5f, new Vector3(1, height, 1)); foreach (var r in GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds); b.Expand(.8f);
            var L = FBStreetLights.Lamps; for (int i = 0; i < L.Count; i++) if (b.Contains(L[i].p)) lamps.Add(i);
        }
        Vector3 Top => basePos + Quaternion.AngleAxis(tilt, axis == Vector3.zero ? Vector3.right : axis) * Vector3.up * height;
        public void KillLights(float flicker, bool hideGlow) { Init(); foreach (var i in lamps) FBStreetLights.Kill(i, flicker * Random.Range(.7f, 1.3f), hideGlow); }
        // batida de carro: dir = sentido do carro, imp = velocidade contra o poste (m/s). true = cedeu (o carro segue)
        public bool Hit(Vector3 dir, float imp, Vector3 at)
        {
            Init(); FBStaticMerge.Break(this); if (down || falling) return true; dir.y = 0; if (dir.sqrMagnitude < 1e-4f) return false; dir.Normalize();
            if (axis == Vector3.zero || tilt < 1) axis = Vector3.Cross(Vector3.up, dir).normalized;
            FBFx.Sparks(at, -dir); FBAudio.PlayAt("reb_carimpact" + (imp > 9 ? 4 : 3), at, Mathf.Min(1, .5f + imp / 20), 8);
            if (imp > 7.5f || tilt > 22)
            {
                falling = true; w = Mathf.Max(w, imp * 2.5f); KillLights(.6f, true);
                foreach (var c in cols) if (c) c.enabled = false; lastTop = Top; return true;
            }
            tilt = Mathf.Min(tilt + Mathf.Lerp(4, 18, Mathf.InverseLerp(3.5f, 7.5f, imp)), 24); Apply(); KillLights(1.2f, false);
            return false;
        }
        void Apply()
        {
            var q = Quaternion.AngleAxis(tilt, axis); transform.SetPositionAndRotation(basePos + q * (pos0 - basePos), q * rot0);
        }
        // [parceiro 01/11] cronômetro por script (FBTempo, ligado só pelo teste do .exe); desligado custa só um teste de bool
        static readonly int __fbt_Update = FBTempo.Id("FBPole.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!falling) return; float dt = Time.deltaTime;
            w += 1.5f * 9.81f / Mathf.Max(2, height) * Mathf.Sin(Mathf.Max(tilt, 4) * Mathf.Deg2Rad) * Mathf.Rad2Deg * dt;   // haste girando no pé
            float nt = Mathf.Min(89, tilt + w * dt); var prev = Top; tilt = nt; var top = Top; bool stop = tilt >= 89;
            // bateu em algo no caminho (muro, carro, árvore): para ali, encostado
            var seg = top - prev; float len = seg.magnitude;
            if (len > 1e-3f && Physics.SphereCast(prev, .15f, seg / len, out var h, len, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && !h.transform.IsChildOf(transform)) { stop = true; tilt -= w * dt * .5f; }
            Apply();
            if (stop)
            {
                falling = false; down = true; var p = Top; FBAudio.PlayAt("reb_carimpact2", p, .9f, 10); FBFx.Puff(p, new Color(.55f, .52f, .48f), 1.1f, 1.4f, .6f);
                FBFx.Puff(Vector3.Lerp(basePos, p, .5f), new Color(.55f, .52f, .48f), .9f, 1.2f, .5f);
            }
        }
    }

    public static class FBStreetPoleFx
    {
        // tiro que acertou algo: luminária perto do ponto apaga
        public static void Shot(Collider c, Vector3 p, Vector3 dir)
        {
            int i = FBStreetLights.Nearest(p, .8f); if (i >= 0) FBStreetLights.Kill(i, Random.Range(.12f, .35f), false, true);
        }
        // bala passando perto da lâmpada (cabeça sem colisor)
        public static void Ray(Vector3 o, Vector3 dir, float dist)
        {
            var L = FBStreetLights.Lamps; if (L.Count == 0) return; float best = .3f; int bi = -1;
            for (int i = 0; i < L.Count; i++)
            {
                if (FBStreetLights.IsDead(i)) continue; var v = L[i].p - o; float t = Vector3.Dot(v, dir); if (t < 0 || t > dist) continue;
                float d = (v - dir * t).magnitude; if (d < best) { best = d; bi = i; }
            }
            if (bi >= 0) FBStreetLights.Kill(bi, Random.Range(.12f, .35f), false, true);
        }
        // batida de carro num colisor: é poste? (FBDrive)
        public static FBPole PoleOf(Collider c) => c ? c.GetComponentInParent<FBPole>() : null;
    }
}
