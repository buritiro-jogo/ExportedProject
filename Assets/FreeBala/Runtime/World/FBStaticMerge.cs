// JUNÇÃO POR CÉLULA de peças pequenas e repetidas (rodada 43, otimização). O tour da r42c ("quem desenha") mostrou que as maiores
// fontes de chamadas de desenho na Vila Jardim eram peças minúsculas desenhadas uma por uma: fios/postes (VJ_Postes_Fios_pNNN, 741–1.582
// chamadas para só 5–9 mil triângulos) e aparelhos de ar-condicionado (VJ_ArCond, 470–580 chamadas). Aqui, ao carregar o mapa, as peças de
// cada célula de 48 m viram UMA malha por material; as originais ficam invisíveis (forceRenderingOff — o FBDistanceCull pode continuar
// ligando/desligando o 'enabled' delas sem efeito) e mantêm a colisão.
// Postes continuam podendo entortar/cair (FBPole): na primeira batida, a célula daquele poste volta às peças separadas (Break).
// -fbsemjuncao desliga (comparação no tour).
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FreeBala
{
    public static class FBStaticMerge
    {
        static readonly Regex GROUP = new(@"(_Postes_.*_p\d+$|^VJ_ArCond)", RegexOptions.IgnoreCase);
        const float CELL = 48;
        class Cell { public readonly List<Renderer> src = new(); public readonly List<GameObject> merged = new(); public bool broken; }
        static readonly Dictionary<FBPole, Cell> poleCell = new();
        public static int Sources, Merged, Unreadable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsemjuncao") >= 0) return;
            SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }
        static void OnScene(Scene s, LoadSceneMode m)
        {
            if (m == LoadSceneMode.Single) poleCell.Clear();
            foreach (var root in s.GetRootGameObjects()) if (root.name.StartsWith("Map_")) Run(root);
        }
        static string Group(Transform t)
        {
            for (int i = 0; t && i < 3; i++, t = t.parent) { var mt = GROUP.Match(t.name); if (mt.Success) return mt.Value.StartsWith("_") ? "postes" : "arcond"; }
            return null;
        }
        static void Run(GameObject root)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cells = new Dictionary<(string g, int x, int z, int layer, ShadowCastingMode sh), (Cell cell, Dictionary<Material, List<CombineInstance>> byMat)>();
            int src = 0, unread = 0; var unreadNames = new HashSet<string>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // r43b: NÃO pula renderizador desligado — o FBDistanceCull já desliga os postes longe antes daqui (tour: 1.751 chamadas dos
                // fios ainda assim); a junção fica no lugar deles (o Unity corta por câmera) e o 'enabled' dos originais deixa de importar
                if (!r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
                var g = Group(r.transform); if (g == null) continue;
                var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null; if (!mesh) continue;
                if (!mesh.isReadable) { unread++; if (unreadNames.Count < 4) unreadNames.Add(g + ":" + r.name); continue; }
                var c = r.bounds.center; var key = (g, Mathf.FloorToInt(c.x / CELL), Mathf.FloorToInt(c.z / CELL), r.gameObject.layer, r.shadowCastingMode);
                if (!cells.TryGetValue(key, out var e)) { e = (new Cell(), new Dictionary<Material, List<CombineInstance>>()); cells[key] = e; }
                var mats = r.sharedMaterials; var M = r.transform.localToWorldMatrix;
                for (int sm = 0; sm < mesh.subMeshCount && sm < mats.Length; sm++)
                {
                    if (!mats[sm] || mesh.GetIndexCount(sm) == 0) continue;
                    if (!e.byMat.TryGetValue(mats[sm], out var L)) e.byMat[mats[sm]] = L = new List<CombineInstance>();
                    L.Add(new CombineInstance { mesh = mesh, subMeshIndex = sm, transform = M });
                }
                e.cell.src.Add(r); src++;
                var pole = r.GetComponentInParent<FBPole>(); if (pole) poleCell[pole] = e.cell;
            }
            int made = 0;
            foreach (var kv in cells)
            {
                var (cell, byMat) = kv.Value; if (cell.src.Count < 2) { poleCell.Where(p => p.Value == cell).Select(p => p.Key).ToList().ForEach(p => poleCell.Remove(p)); continue; }
                foreach (var mk in byMat)
                {
                    var mesh = new Mesh { name = $"juncao_{kv.Key.g}_{kv.Key.x}_{kv.Key.z}_{mk.Key.name}" };
                    if (mk.Value.Sum(ci => (long)ci.mesh.vertexCount) > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.CombineMeshes(mk.Value.ToArray(), true, true); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
                    var go = new GameObject(mesh.name) { layer = kv.Key.layer };                       // malha já em coordenada de mundo
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.SetParent(root.transform, true);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mk.Key;
                    mr.shadowCastingMode = kv.Key.sh; mr.receiveShadows = cell.src[0].receiveShadows; mr.lightProbeUsage = LightProbeUsage.Off;
                    cell.merged.Add(go); made++;
                }
                foreach (var r in cell.src) r.forceRenderingOff = true;
            }
            Sources += src; Merged += made; Unreadable += unread;
            Debug.Log($"[FreeBala][juncao] {root.name}: {src} peças -> {made} malhas por célula de {CELL:0} m ({cells.Count} células · não legíveis {unread}: {string.Join(", ", unreadNames)}) em {sw.ElapsedMilliseconds} ms");
        }
        /// <summary>Poste atingido: a célula dele volta às peças separadas (a junção sai de cena).</summary>
        public static void Break(FBPole p)
        {
            if (!p || !poleCell.TryGetValue(p, out var c) || c.broken) return;
            c.broken = true; foreach (var g in c.merged) if (g) Object.Destroy(g);
            foreach (var r in c.src) if (r) r.forceRenderingOff = false;
        }
    }
}
