// Hitboxes nos ossos do Humanoid (porte do Actor.hitTest): cabeça = esfera de 0,17 m um pouco acima do osso; tronco = cápsula
// quadril→pescoço (0,28); pernas = cápsula do quadril aos pés (0,22, "membro"); braços = cápsulas (0,075, "membro") — a parte decide a
// capa do Free Fire (cabeça / corpo / membro). Camada "Hitbox": só os tiros a enxergam (sem colisão física, são gatilhos).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    // vidro de janela quebrável (setupGlass/breakGlass do world.js). A malha de vidro do mapa junta TODAS as janelas do prédio: quebrar
    // escondia o prédio inteiro. Agora só o PAINEL atingido sai — os triângulos ligados ao triângulo do impacto (componente conexo por
    // vértice na mesma posição) são removidos da malha e do colisor; o resto continua inteiro e quebrável. Malha sem leitura: some tudo.
    public class FBGlass : MonoBehaviour
    {
        public bool broken; Mesh own; MeshCollider mc; MeshFilter mf;
        public void Break(Vector3 p, Vector3 dir, bool fromNet = false)
        {
            if (broken) return; dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            if (!mf) { mf = GetComponent<MeshFilter>(); mc = GetComponent<MeshCollider>(); }
            // Prédios e estruturas com múltiplos submateriais nunca devem quebrar/sumir inteiros como vidro
            if (mf && mf.sharedMesh && mf.sharedMesh.subMeshCount > 1) return;
            if (mf && mc && mf.sharedMesh && mf.sharedMesh.isReadable && mf.sharedMesh.subMeshCount == 1)
            {
                if (!RemovePane(p, dir)) return;
            }
            else
            {
                if (mf && mf.sharedMesh && (mf.sharedMesh.subMeshCount > 1 || mf.sharedMesh.triangles.Length > 12)) return;
                broken = true; foreach (var r in GetComponents<Renderer>()) r.enabled = false; foreach (var c in GetComponents<Collider>()) c.enabled = false;
            }
            FBImpactFx.Glass(p, -dir, dir);   // cacos e pó de vidro (pool do FBImpactFx)
            FBAudio.PlayAt("glass_break", p, .9f, 6);
            if (!fromNet) FBNet.I?.SendGlass(this, p);
        }
        bool RemovePane(Vector3 p, Vector3 dir)
        {
            RaycastHit h = default; bool hit = false;   // direção da bala; pela rede (sem direção) tenta os eixos
            foreach (var d in new[] { dir, -dir, Vector3.right, Vector3.left, Vector3.forward, Vector3.back }) if (mc.Raycast(new Ray(p - d * .3f, d), out h, .8f)) { hit = true; break; }
            if (!hit) return false;
            if (!own) { own = Instantiate(mf.sharedMesh); own.name = mf.sharedMesh.name + "_q"; mf.sharedMesh = own; }
            var V = own.vertices; var T = own.triangles; int nt = T.Length / 3; if (h.triangleIndex < 0 || h.triangleIndex >= nt) return false;
            // posição → id (vértices duplicados nas quinas contam como o mesmo)
            var pid = new Dictionary<Vector3Int, int>(); var vid = new int[V.Length];
            for (int i = 0; i < V.Length; i++) { var k = Vector3Int.RoundToInt(V[i] * 500); if (!pid.TryGetValue(k, out var id)) pid[k] = id = pid.Count; vid[i] = id; }
            var byV = new Dictionary<int, List<int>>(); for (int t = 0; t < nt; t++) for (int c = 0; c < 3; c++) { int v = vid[T[t * 3 + c]]; if (!byV.TryGetValue(v, out var l)) byV[v] = l = new List<int>(); l.Add(t); }
            var gone = new HashSet<int> { h.triangleIndex }; var q = new Queue<int>(); q.Enqueue(h.triangleIndex);
            while (q.Count > 0 && gone.Count < 400) { int t = q.Dequeue(); for (int c = 0; c < 3; c++) foreach (var o in byV[vid[T[t * 3 + c]]]) if (gone.Add(o)) q.Enqueue(o); }
            var keep = new List<int>(T.Length); for (int t = 0; t < nt; t++) if (!gone.Contains(t)) { keep.Add(T[t * 3]); keep.Add(T[t * 3 + 1]); keep.Add(T[t * 3 + 2]); }
            // último painel: desliga ANTES — malha sem triângulos no colisor dava erro do PhysX no console ("must have at least one
            // non-degenerate triangle", orla_OR_Restaurante_8_vidro_q no autoteste da rodada 37)
            if (keep.Count == 0) { broken = true; GetComponent<Renderer>().enabled = false; mc.enabled = false; mc.sharedMesh = null; return true; }
            own.subMeshCount = 1; own.SetTriangles(keep, 0); mc.sharedMesh = null; mc.sharedMesh = own;
            return true;
        }
    }
}
