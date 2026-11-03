// CABEÇA DE LUMINÁRIA apagável (rodada 37 — "atirei na luminária: a luz apaga, mas a base continua branca e brilhando").
// Causa: o tiro/batida desligava o spot e o brilho (sprite) da luminária, mas a cabeça usa um material emissivo COMPARTILHADO por todos
// os postes do mapa, que o FBWeather acende à noite (LampGlow) — e várias cabeças moram na mesma malha. Agora:
//  · FBLampPrep (editor, junto do FBPolePrep) separa a submalha de lâmpada de cada luminária num objeto próprio com este componente;
//  · em jogo FBStreetLights acende/apaga a emissão SÓ daquela cabeça por MaterialPropertyBlock (o material compartilhado continua
//    controlado pelo FBWeather): apagada = sem emissão e vidro escurecido; no curto pisca junto com a luz.
//  · mapa ainda não preparado: cai para o renderizador inteiro quando ele contém só aquela luminária (poste partido pelo FBPolePrep).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    [DisallowMultipleComponent]
    public class FBLampHead : MonoBehaviour
    {
        public static readonly List<FBLampHead> All = new();
        [System.NonSerialized] public int lamp = -1;                                    // índice em FBStreetLights.Lamps (ligado no Build)
        Renderer r; static MaterialPropertyBlock mpb; static readonly int idE = Shader.PropertyToID("_EmissionColor"), idB = Shader.PropertyToID("_BaseColor");
        void OnEnable() { if (!All.Contains(this)) All.Add(this); r = GetComponent<Renderer>(); }
        void OnDisable() { All.Remove(this); }
        public Vector3 Center => r ? r.bounds.center : transform.position;

        // on = segue o material compartilhado (aceso à noite); off = apagada (sem emissão, vidro escuro)
        public void SetLit(bool on) => Apply(r, on, -1);
        public static void Apply(Renderer rd, bool on, int onlySub)
        {
            if (!rd) return; mpb ??= new MaterialPropertyBlock(); var mats = rd.sharedMaterials;
            for (int s = 0; s < mats.Length; s++)
            {
                if (onlySub >= 0 && s != onlySub) continue; var m = mats[s]; if (!m) continue;
                if (on) { rd.SetPropertyBlock(null, s); continue; }
                mpb.Clear(); if (m.HasProperty(idE)) mpb.SetColor(idE, Color.black);
                if (m.HasProperty(idB)) { var b = m.GetColor(idB); mpb.SetColor(idB, new Color(b.r * .22f, b.g * .22f, b.b * .24f, b.a)); }
                rd.SetPropertyBlock(mpb, s);
            }
        }

        // liga cada cabeça à luminária mais perto (FBStreetLights.Build)
        public static void Bind(IReadOnlyList<FBStreetLights.Lamp> lamps)
        {
            foreach (var h in All)
            {
                if (!h) continue; h.lamp = -1; float bd = 2.2f * 2.2f; var c = h.Center;
                for (int i = 0; i < lamps.Count; i++) { float d = (lamps[i].p - c).sqrMagnitude; if (d < bd) { bd = d; h.lamp = i; } }
            }
        }
        // acende/apaga a(s) cabeça(s) da luminária i; sem cabeça preparada: renderizador do mapa que só tem esta luminária
        static readonly Dictionary<int, List<(Renderer r, int sub)>> fallback = new();
        public static void SetLamp(int i, bool on, Vector3 p, IReadOnlyList<FBStreetLights.Lamp> lamps)
        {
            bool any = false; foreach (var h in All) if (h && h.lamp == i) { h.SetLit(on); any = true; }
            if (any) return;
            if (!fallback.TryGetValue(i, out var L)) fallback[i] = L = FindFallback(p, lamps);
            foreach (var (rd, sub) in L) Apply(rd, on, sub);
        }
        public static void ResetScene() { fallback.Clear(); }
        static readonly System.Text.RegularExpressions.Regex HEAD = new(@"(^|_)(lamp|lampada|luminaria)(_on|_sodio|_sodium)?$|street_?light", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        static List<(Renderer, int)> FindFallback(Vector3 p, IReadOnlyList<FBStreetLights.Lamp> lamps)
        {
            var res = new List<(Renderer, int)>();
            foreach (var c in Physics.OverlapSphere(p, 1.2f, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore))
            {
                // [parceiro 31/10] '??' não enxerga o "null falso" do Unity (Renderer ausente no Editor) e pulava a malha do pai: testa com o operador do Unity
                var rd = c.GetComponent<Renderer>(); if (!rd) rd = c.GetComponentInParent<Renderer>(); if (!rd) continue; var b = rd.bounds; int inside = 0;
                foreach (var l in lamps) if (b.SqrDistance(l.p) < 1.2f * 1.2f) inside++;
                if (inside > 1) continue;                                                      // malha com várias luminárias: não apaga as outras
                var mats = rd.sharedMaterials; for (int s = 0; s < mats.Length; s++) if (mats[s] && HEAD.IsMatch(mats[s].name.Replace(" (Instance)", ""))) res.Add((rd, s));
            }
            if (res.Count == 0)
                foreach (var rd in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    if (rd.gameObject.layer != FBWorld.LMap || rd.bounds.SqrDistance(p) > 1.2f * 1.2f) continue; int inside = 0;
                    foreach (var l in lamps) if (rd.bounds.SqrDistance(l.p) < 1.2f * 1.2f) inside++; if (inside > 1) continue;
                    var mats = rd.sharedMaterials; for (int s = 0; s < mats.Length; s++) if (mats[s] && HEAD.IsMatch(mats[s].name.Replace(" (Instance)", ""))) res.Add((rd, s));
                }
            return res;
        }
    }
}
