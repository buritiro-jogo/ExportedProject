// ÁREA DE COMBATE (rodada 47, pedido 30/10: "cada partida deve utilizar apenas uma região específica do mapa, claramente delimitada
// visualmente ... dano progressivo fora da área ... bots devem reconhecer os limites ... spawns posicionados corretamente dentro da área
// ... tamanho equilibrado conforme o modo ... sem afetar os demais modos"). Só CLÁSSICO e 4X4, fora do online.
// Escolha: até 48 centros candidatos (becos do nível + centros fixos da navmesh); para cada um, a fração ANDÁVEL de uma grade de 67 pontos
// em 4 anéis dentro do raio (NavMesh.SamplePosition a 2,5 m, na altura do centro) e um par de bases opostas a ~0,68 r (navmesh, mesma altura,
// caminho entre elas); sorteio entre os 25 % melhores (cada partida numa região diferente). O 4X4 fecha a zona DENTRO da área; o Clássico
// mantém a área fixa (parede da zona parada). Dano fora = progressivo pelo tempo fora (FBZone.OutsideDamage).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public class FBArena
    {
        public Vector3 c, a, b; public float r, walk; public string why;
        public static readonly Dictionary<string, float> RADIUS = new() { ["classico"] = 62, ["squad"] = 55 };   // r47 (antes)
        // r51 (pedido 31/10: "as zonas estão muito pequenas, limitando a movimentação... aumentar consideravelmente, equilibrado com o
        // tamanho e a estrutura de cada mapa"): raio = fração do tamanho ANDÁVEL do mapa (raio que contém 80 % dos pontos da navmesh
        // alcançáveis do nascimento), entre um piso (2× o antigo) e um teto; sem região boa nesse raio, tenta menor até o antigo.
        public static readonly Dictionary<string, (float k, float min, float max)> SIZE = new() { ["classico"] = (.62f, 110, 160), ["squad"] = (.55f, 100, 145) };
        public static bool Bigger = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbzonaantiga") < 0;
        public static float MapRadius;
        public static float RadiusFor(string mode)
        {
            var pb = FBWorld.GetPlayableBounds(FBMatch.I ? FBMatch.I.mapId : FBSession.map);
            float rMax = Mathf.Max(28f, Mathf.Min(pb.extents.x, pb.extents.z) * 0.88f);
            if (!Bigger || !SIZE.TryGetValue(mode, out var z)) return Mathf.Min(RADIUS[mode], rMax);
            var pts = FBNav.Points(); if (pts.Count < 20) return Mathf.Min(z.min, rMax);
            var c = Vector3.zero; foreach (var p in pts) c += p; c /= pts.Count;
            var d = pts.Select(p => (p - c).Flat().magnitude).OrderBy(x => x).ToList(); MapRadius = d[Mathf.Clamp((int)(d.Count * .8f), 0, d.Count - 1)];
            float computed = Mathf.Clamp(MapRadius * z.k, z.min, z.max);
            return Mathf.Min(computed, rMax);
        }
        public static FBArena PickFor(FBMatch G, string mode)
        {
            float r = RadiusFor(mode), r0 = RADIUS[mode];
            for (; ; r = Mathf.Max(r0, r * .88f)) { var a = Pick(G, r); if (a != null || r <= r0) { Debug.Log($"[FreeBala][arena] {mode}: mapa {MapRadius:0} m · raio {r:0} m (antes {r0:0})"); return a; } }
        }
        public static bool For(string mode) => !FBSession.online && RADIUS.ContainsKey(mode);
        public static FBArena Last;

        public static FBArena Pick(FBMatch G, float r)
        {
            var pb = FBWorld.GetPlayableBounds(G ? G.mapId : FBSession.map);
            var cands = new List<Vector3>(FBZone.AlleyPoints(G)); var fx = FBNav.Fixed(); for (int i = 0; i < fx.Count; i += Mathf.Max(1, fx.Count / 40)) cands.Add(fx[i]);
            cands = cands.Where(p => pb.Contains(p) && FBNav.Inside(p)).OrderBy(_ => Random.value).Take(48).ToList();
            if (cands.Count == 0) cands.Add(pb.center);
            var scored = new List<(FBArena ar, float s)>();
            foreach (var c0 in cands)
            {
                if (!NavMesh.SamplePosition(c0, out var hc, 4, NavMesh.AllAreas)) continue; var c = hc.position;
                if (!pb.Contains(c) || !FBNav.Inside(c)) continue;
                int hit = 0, tot = 0;
                for (int ring = 0; ring <= 3; ring++)
                {
                    int n = ring == 0 ? 1 : ring * 6 * 2 - (ring == 3 ? 6 : 0); float rr = r * ring / 3.2f;
                    for (int k = 0; k < n; k++) { float ang = k / (float)n * Mathf.PI * 2; var q = c + new Vector3(Mathf.Cos(ang) * rr, 0, Mathf.Sin(ang) * rr); tot++; if (NavMesh.SamplePosition(q, out var h, 2.5f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - c.y) < 4 && pb.Contains(h.position) && FBNav.Inside(h.position)) hit++; }
                }
                float walk = hit / (float)Mathf.Max(1, tot); if (walk < .35f) continue;
                var ar = new FBArena { c = c, r = r, walk = walk }; if (!ar.Bases()) continue;
                scored.Add((ar, walk + (ar.a - ar.b).Flat().magnitude / (2 * r) * .25f));
            }
            if (scored.Count == 0) return null;
            var top = scored.OrderByDescending(s => s.s).Take(Mathf.Max(1, scored.Count / 4)).ToList(); var pick = top[Random.Range(0, top.Count)].ar;
            pick.why = $"{scored.Count}/{cands.Count} candidatos · andável {pick.walk:P0} · bases a {(pick.a - pick.b).Flat().magnitude:0} m"; Last = pick;
            Debug.Log($"[FreeBala][arena] centro {pick.c} raio {r:0} m · {pick.why}");
            return pick;
        }
        // bases opostas dentro da área: 12 direções, a ~0,65 r do centro, na navmesh, na altura do centro, ligadas por caminho
        bool Bases()
        {
            float best = -1; float a0 = Random.value * Mathf.PI * 2;
            for (int k = 0; k < 12; k++)
            {
                float ang = a0 + k / 12f * Mathf.PI; var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                if (!Spot(c + d * r * .65f, out var pa) || !Spot(c - d * r * .65f, out var pb)) continue;
                float sep = (pa - pb).Flat().magnitude; if (sep < r * .85f || sep <= best) continue;
                var path = new NavMeshPath(); if (!NavMesh.CalculatePath(pa, pb, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                best = sep; a = pa; b = pb;
            }
            return best > 0;
        }
        bool Spot(Vector3 q, out Vector3 p)
        {
            p = q;
            if (!FBMatch.ValidateSpawnSpot(q, out p, this)) return false;
            return Mathf.Abs(p.y - c.y) < 4 && (p - c).Flat().magnitude <= r - 5f;
        }
        public bool Inside(Vector3 p, float margin = 0) => (p - c).Flat().magnitude <= r - margin;
        // destino de bot mantido dentro da área: fora (com folga) vira um ponto andável a 60 % do raio na mesma direção
        public Vector3 Keep(Vector3 p, float margin = 6)
        {
            if (Inside(p, margin)) return p; var d = (p - c).Flat(); d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
            if (NavMesh.SamplePosition(c + d * r * .6f, out var h, 10, NavMesh.AllAreas) && Inside(h.position, margin)) return h.position;
            return FBNav.RandomPoint(c, r * .6f) is Vector3 q && Inside(q, margin) ? q : c;
        }
        // renascimento do Clássico: ponto andável dentro da área o mais longe possível dos inimigos vivos (e perto da própria base) com cobertura
        public Vector3 SafeSpot(int team)
        {
            var enemies = FBMatch.Actors().Where(x => x && x.alive && x.team != team && x.team < 2).Select(x => x.Pos).ToList(); var home = Home(team);
            Vector3 best = home; float bs = -1e9f;
            for (int i = 0; i < 32; i++)
            {
                var q0 = i < 4 ? home + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f)) : (FBNav.RandomPoint(c, r * .8f) ?? home);
                if (!FBMatch.ValidateSpawnSpot(q0, out var q, this)) continue;
                if (!Inside(q, 5f) || Mathf.Abs(q.y - c.y) > 4) continue;
                float near = enemies.Count == 0 ? 60 : enemies.Min(e => (e - q).Flat().magnitude);
                if (near < 16f) continue;
                bool exposed = enemies.Any(e => (e - q).Flat().magnitude < 45f && FBWorld.ClearLine(q + Vector3.up * 1.5f, e + Vector3.up * 1.5f));
                float coverScore = exposed ? -70f : 30f;
                float s = Mathf.Min(near, 45) * 2 - (q - home).Flat().magnitude * .2f + coverScore;
                if (s > bs) { bs = s; best = q; }
            }
            if (!FBMatch.ValidateSpawnSpot(best, out var finalSpot, this))
            {
                FBMatch.ValidateSpawnSpot(home, out finalSpot, this);
            }
            return finalSpot;
        }
        public Vector3 Home(int team) => team == FBSession.team ? a : b;
    }
}
