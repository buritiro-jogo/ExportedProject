// Navegação (porte do nav.js/NAVM + grafo de pontos do main.js): a NavMesh do Unity (construída no importador, a partir da colisão do
// mapa) faz o papel da navmesh original — caminho (path), ponto aleatório (randomPoint perto de um ponto), achar o polígono mais perto.
// Pontos fixos da navmesh (centros dos triângulos) para pontos táticos/zonas/grafites com a mesma escolha a cada carga.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public static class FBNav
    {
        static Vector3[] centers; static float[] areas; static float totalArea; static List<Vector3> sample;
        public static void Reset()
        {
            centers = null; sample = null; anchor = null; reach.Clear();
            // [parceiro] presos 01/11: o cache de células sai junto com o reach (Reset)
            ponto.Clear(); fim.Clear();
            // [parceiro] presos v4: limite da arena é por partida
            temLimite = false;
        }
        // ÁREA JOGÁVEL (pedido 20/10: "NPCs sendo gerados pra fora do muro de limite"): a NavMesh também cobre o terreno do lado de fora do
        // muro de perímetro; com uma âncora (onde o jogador nasce) só vale ponto ALCANÇÁVEL a pé dela — caminho completo na NavMesh.
        // Resultado guardado por célula de 4 m (a mesma pergunta não recalcula o caminho).
        static Vector3? anchor; static readonly Dictionary<long, bool> reach = new(); static readonly NavMeshPath rpath = new();
        public static void SetAnchor(Vector3 p)
        {
            reach.Clear();
            // [parceiro] presos 01/11: âncora nova, cache de células novo
            ponto.Clear(); fim.Clear();
            // [parceiro] fis: Points() guarda 300 pontos sorteados; os de antes da âncora não passaram pelo filtro de ilha
            sample = null;
            anchor = NavMesh.SamplePosition(p, out var h, 6, NavMesh.AllAreas) ? h.position : (Vector3?)null;
        }
        public static bool HasAnchor => anchor.HasValue;
        // [parceiro] presos 01/11: ponto da âncora (null sem âncora), para o FBBot ver em que ilha do NavMesh o bot está
        public static Vector3? Anchor => anchor;

        // [parceiro] presos 01/11: por célula do cache do Inside: o ponto que decidiu a célula (o primeiro consultado nela) e, se a âncora
        // não chega nele, o fim do caminho parcial (o ponto da ilha principal mais perto dele). O Inside já paga essa busca, a
        // mais cara (com o ponto numa ilha ela varre a ilha principal até acabar os nós); guardar evita que o FBBot a refaça.
        static readonly Dictionary<long, Vector3> ponto = new(), fim = new();
        static readonly Vector3[] cantos = new Vector3[256];

        // [parceiro] presos v4 (01/11): limite da arena 10x10 (o quadrado das paredes invisíveis do FBModo10x10). O NavMesh das arenas foi
        // assado sem as paredes (elas nascem no modo) e continuava do lado de fora: na PT (C1, rodada 10) dois bots ficaram uns
        // 90 s presos em (-299,7; 16,3), na parede oeste, com caminho e desvio (stuckN 4) passando por x < -300. Com limite:
        // Inside() dá falso fora do quadrado (RandomPoint e Points não sorteiam lá), Path() puxa o destino para dentro e corta o
        // caminho onde ele sai do quadrado. O FBModo10x10 também recorta o NavMesh nas paredes (NavMeshObstacle) e chama
        // Recarregar() quando o recorte entra. Sem limite (outros modos e mapas sem "arena") nada muda.
        static bool temLimite; static Vector3 limC; static float limHalf;
        public static bool TemLimite => temLimite;
        public static void SetLimite(Vector3 c, float half) { temLimite = half > 0f; limC = c; limHalf = half; }
        // [parceiro] p dentro do quadrado com folga "margem" das paredes (sem limite: sempre)
        public static bool DentroLimite(Vector3 p, float margem)
        {
            if (!temLimite) return true;
            float h = limHalf - margem; return Mathf.Abs(p.x - limC.x) <= h && Mathf.Abs(p.z - limC.z) <= h;
        }
        // [parceiro] p puxado para dentro do quadrado, a "margem" das paredes (sem limite: o próprio p)
        public static Vector3 PraDentro(Vector3 p, float margem)
        {
            if (!temLimite) return p;
            float h = Mathf.Max(0f, limHalf - margem);
            p.x = Mathf.Clamp(p.x, limC.x - h, limC.x + h); p.z = Mathf.Clamp(p.z, limC.z - h, limC.z + h); return p;
        }
        // [parceiro] presos v4: o NavMesh mudou (recorte das paredes da arena): refaz o sorteio de triângulos e os caches de ilha, com a
        // mesma âncora (achada de novo no NavMesh novo)
        public static void Recarregar()
        {
            centers = null; sample = null; var a = anchor;
            reach.Clear(); ponto.Clear(); fim.Clear();
            if (a.HasValue) SetAnchor(a.Value);
            // [parceiro] revisão: refaz a triangulação já (fase de compra, bots parados), não no primeiro quadro da rodada valendo
            Build();
        }
        // ponto fora da malha (assento de banco/cadeira: o próprio móvel recorta a NavMesh): mede do ponto andável mais perto
        public static bool InsideNear(Vector3 p, float r) => anchor == null || (NavMesh.SamplePosition(p, out var h, r, NavMesh.AllAreas) && Inside(h.position));
        public static bool Inside(Vector3 p)
        {
            // [parceiro] presos v4: fora do quadrado da arena não é área de jogo (sem cache: não gasta busca)
            if (temLimite && !DentroLimite(p, 0f)) return false;
            if (anchor is not Vector3 a) return true;
            // [parceiro] presos 01/11: a chave da célula foi para Chave() (a Celula usa a mesma)
            long key = Chave(p);
            if (reach.TryGetValue(key, out var ok)) return ok;
            bool achou = NavMesh.CalculatePath(a, p, NavMesh.AllAreas, rpath);
            ok = achou && rpath.status == NavMeshPathStatus.PathComplete; reach[key] = ok;
            // [parceiro] presos 01/11: guarda quem decidiu a célula e, no parcial, o fim (buffer fixo; o array inteiro só se o caminho
            // tiver mais cantos que o buffer)
            ponto[key] = p;
            if (achou && rpath.status == NavMeshPathStatus.PathPartial)
            {
                int nc = rpath.GetCornersNonAlloc(cantos);
                if (nc >= cantos.Length) { var cs = rpath.corners; if (cs.Length != 0) fim[key] = cs[cs.Length - 1]; }
                else if (nc > 0) fim[key] = cantos[nc - 1];
            }
            return ok;
        }
        // [parceiro] presos 01/11: célula de 4x4x6 m do cache do Inside
        static long Chave(Vector3 p) => ((long)Mathf.FloorToInt(p.x / 4) << 32) ^ (uint)Mathf.FloorToInt(p.z / 4) ^ ((long)Mathf.FloorToInt(p.y / 6) << 52);
        // [parceiro] presos 01/11: p pelo cache do Inside, sem busca nova da âncora quando a célula já foi vista. hp = p no NavMesh;
        // refP = ponto que decidiu a célula; at = fim do caminho parcial da âncora até refP (ponto da ilha principal mais perto).
        // -1 = sem âncora ou sem NavMesh a r de p; 0 = célula dentro da ilha principal; 1 = célula fora, com fim guardado;
        // 2 = célula fora sem fim (caminho inválido); 3 = célula ainda não vista e soCache (nada calculado). A célula vale para
        // o ponto que a decidiu e pode pegar os dois lados de uma cerca: quem usa confere p de perto (FBBot.Principal).
        public static int Celula(Vector3 p, float r, bool soCache, out Vector3 hp, out Vector3 refP, out Vector3 at)
        {
            hp = p; refP = p; at = p;
            if (!anchor.HasValue || !NavMesh.SamplePosition(p, out var hit, r, NavMesh.AllAreas)) return -1;
            hp = hit.position; long key = Chave(hp);
            if (soCache && !reach.ContainsKey(key)) return 3;
            bool dentro = Inside(hp);
            if (!ponto.TryGetValue(key, out refP)) refP = hp;
            if (dentro) return 0;
            if (!fim.TryGetValue(key, out at)) { at = hp; return 2; }
            return 1;
        }
        static void Build()
        {
            if (centers != null) return; var t = NavMesh.CalculateTriangulation(); int n = t.indices.Length / 3;
            centers = new Vector3[n]; areas = new float[n]; totalArea = 0;
            for (int i = 0; i < n; i++)
            {
                var a = t.vertices[t.indices[i * 3]]; var b = t.vertices[t.indices[i * 3 + 1]]; var c = t.vertices[t.indices[i * 3 + 2]];
                centers[i] = (a + b + c) / 3; float ar = Vector3.Cross(b - a, c - a).magnitude * .5f; totalArea += ar; areas[i] = totalArea;
            }
        }
        public static bool Ready { get { Build(); return centers.Length > 0; } }
        // ponto aleatório na navmesh (ponderado pela área) — ou perto de 'near' dentro de 'r'
        public static Vector3? RandomPoint(Vector3? near = null, float r = 0)
        {
            Build(); if (centers.Length == 0) return null;
            if (near is Vector3 c && r > 0)
            {
                for (int k = 0; k < 16; k++) { var q = c + new Vector3(Random.Range(-r, r), 0, Random.Range(-r, r)); if (NavMesh.SamplePosition(q, out var h, Mathf.Max(4, r * .3f), NavMesh.AllAreas) && Inside(h.position)) return h.position; }
                return null;
            }
            for (int k = 0; k < 24; k++)
            {
                float x = Random.value * totalArea; int lo = 0, hi = areas.Length - 1; while (lo < hi) { int m = (lo + hi) / 2; if (areas[m] < x) lo = m + 1; else hi = m; }
                if (Inside(centers[lo])) return centers[lo];
            }
            return anchor;
        }
        public static List<Vector3> Points() { if (sample != null) return sample; sample = new List<Vector3>(); for (int i = 0; i < 300; i++) if (RandomPoint() is Vector3 p) sample.Add(p); return sample; }
        // pontos fixos: centros dos triângulos (no máx. ~600), mesma escolha em toda carga
        public static List<Vector3> Fixed() { Build(); var l = new List<Vector3>(); int k = Mathf.Max(1, centers.Length / 600); for (int i = 0; i < centers.Length; i += k) l.Add(centers[i]); return l; }
        public static bool Find(Vector3 p, float r, out Vector3 at) { if (NavMesh.SamplePosition(p, out var h, r, NavMesh.AllAreas)) { at = h.position; return true; } at = p; return false; }
        static readonly NavMeshPath path = new();
        public static List<Vector3> Path(Vector3 from, Vector3 to)
        {
            // [parceiro] presos v4: destino fora do quadrado da arena vem para dentro (1,5 m da parede)
            if (temLimite && !DentroLimite(to, 1f)) to = PraDentro(to, 1.5f);
            if (!Find(from, 3, out var a) || !Find(to, 8, out var b)) return null;
            if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) || path.status == NavMeshPathStatus.PathInvalid) return null;
            var l = new List<Vector3>(path.corners); if (l.Count > 0) l.RemoveAt(0); if (l.Count == 0) l.Add(to);
            // [parceiro] presos v4: caminho que sai do quadrado da arena (NavMesh de fora, antes do recorte entrar) acaba onde sai
            if (temLimite) Corta(l, a);
            return l;
        }
        // [parceiro] presos v4: corta o caminho no primeiro canto fora do quadrado (folga 0,2 m): esse canto vira o último ponto do trecho
        // que ainda está a 1 m da parede, e o resto sai. Começo já fora (não acontece: a parede segura): fica só o ponto de
        // dentro mais perto.
        static void Corta(List<Vector3> l, Vector3 from)
        {
            var a = from;
            for (int i = 0; i < l.Count; i++)
            {
                if (!DentroLimite(l[i], 0.2f))
                {
                    if (!DentroLimite(a, 1f)) { l.Clear(); l.Add(PraDentro(a, 1.5f)); return; }
                    var b = l[i]; float lo = 0f, hi = 1f;
                    for (int k = 0; k < 12; k++) { float m = (lo + hi) * 0.5f; if (DentroLimite(Vector3.Lerp(a, b, m), 1f)) lo = m; else hi = m; }
                    l[i] = Vector3.Lerp(a, b, lo); l.RemoveRange(i + 1, l.Count - i - 1); return;
                }
                a = l[i];
            }
        }
    }
}
