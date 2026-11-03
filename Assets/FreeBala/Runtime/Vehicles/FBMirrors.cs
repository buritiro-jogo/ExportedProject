// RETROVISORES COM ESPELHO DE VERDADE (pedido 20/10/2026: "o retrovisor tem a mesma textura da placa; que tenha de fato um espelho").
// No REB o vidro dos retrovisores é a região AZUL da cor de vértice da carroceria (shader de veículos do REB) e recebe a RenderTexture
// de UMA câmera (Carro/CarroConfig.CameraRetrovisor, olhando para trás). A exportação tinha jogado essa região no atlas (pedaço qualquer).
// tools/reb/export_mirrors.py -> reb_oficina.json carros[c].espelhos (caixas dos triângulos azuis: esquerdo, direito, interno).
//  · Split: os triângulos do atlas dentro de cada caixa viram um submesh "espelho" com UV pela geometria (espelhada em u);
//  · carro que o JOGADOR dirige: câmera de ré pequena (256×128) numa RenderTexture mostrada em todos os espelhos, como no REB;
//  · demais carros: espelho cromado (metálico, liso: reflete o céu/ambiente).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBMirrors
    {
        static Material chrome, live; static RenderTexture rt; static Camera cam; static FBCar camCar;
        // carros parados: o mapa não tem sonda de reflexo (metal liso saía quase preto) -> "reflexo" fixo de céu claro / horizonte / chão
        public static Material Chrome
        {
            get
            {
                if (chrome) return chrome;
                var t = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "reflexo_espelho" };
                for (int y = 0; y < 64; y++) { float v = y / 63f; var c = v > .45f ? Color.Lerp(new Color(.70f, .76f, .84f), new Color(.86f, .90f, .96f), (v - .45f) / .55f) : Color.Lerp(new Color(.22f, .23f, .25f), new Color(.52f, .54f, .56f), v / .45f); for (int x = 0; x < 4; x++) t.SetPixel(x, y, c); }
                t.Apply(false, true);
                chrome = new Material(FBShaders.Unlit) { name = "veh_espelho" }; chrome.SetTexture("_BaseMap", t); chrome.mainTexture = t; chrome.SetColor("_BaseColor", Color.white); return chrome;
            }
        }
        static List<(Vector3 min, Vector3 max, Vector3 n)> Boxes(string id)
        {
            var l = new List<(Vector3, Vector3, Vector3)>();
            foreach (var o in Json.L(FBReb.Own(id), "espelhos") ?? new List<object>())
            {
                var a = Json.L(o, "min"); var b = Json.L(o, "max"); var n = Json.L(o, "n"); if (a == null || b == null) continue;
                l.Add((new Vector3(Json.Fv(a[0]), Json.Fv(a[1]), Json.Fv(a[2])), new Vector3(Json.Fv(b[0]), Json.Fv(b[1]), Json.Fv(b[2])), n != null ? new Vector3(Json.Fv(n[0]), Json.Fv(n[1]), Json.Fv(n[2])) : Vector3.back));
            }
            return l;
        }
        // separa os triângulos dos espelhos das malhas 'atlas*' do nível de detalhe (raiz = filho do shell, mesmo espaço do REB)
        public static void Split(Transform root, Transform shell, string carId)
        {
            var boxes = Boxes(carId); if (boxes.Count == 0) return; const float E = .012f;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>(); var m = mf.sharedMesh; if (!mr || !m || !m.isReadable) continue;
                var mats = mr.sharedMaterials; int atlasSub = -1;
                for (int k = 0; k < Mathf.Min(mats.Length, m.subMeshCount); k++) if (mats[k] && mats[k].name.StartsWith("atlas")) { atlasSub = k; break; }
                if (atlasSub < 0) continue;
                var P = m.vertices; var M = shell.worldToLocalMatrix * mf.transform.localToWorldMatrix; var I = m.GetTriangles(atlasSub);
                var keep = new List<int>(I.Length); var mir = new List<(int, int, int, int box)>();
                bool In(Vector3 p, (Vector3 min, Vector3 max, Vector3 n) b) => p.x > b.min.x - E && p.x < b.max.x + E && p.y > b.min.y - E && p.y < b.max.y + E && p.z > b.min.z - E && p.z < b.max.z + E;
                for (int i = 0; i + 2 < I.Length; i += 3)
                {
                    Vector3 a = M.MultiplyPoint3x4(P[I[i]]), b = M.MultiplyPoint3x4(P[I[i + 1]]), c = M.MultiplyPoint3x4(P[I[i + 2]]); int bi = -1;
                    for (int k = 0; k < boxes.Count && bi < 0; k++) if (In(a, boxes[k]) && In(b, boxes[k]) && In(c, boxes[k])) bi = k;
                    if (bi < 0) { keep.Add(I[i]); keep.Add(I[i + 1]); keep.Add(I[i + 2]); } else mir.Add((I[i], I[i + 1], I[i + 2], bi));
                }
                if (mir.Count == 0) continue;
                var nm = Object.Instantiate(m); nm.name = m.name.EndsWith("_espelho") ? m.name : m.name + "_espelho"; int sc = m.subMeshCount; nm.subMeshCount = sc + 1;
                for (int k = 0; k < sc; k++) nm.SetTriangles(k == atlasSub ? keep.ToArray() : m.GetTriangles(k), k);
                // vértices próprios com UV planar por espelho (u invertido: imagem de espelho)
                var V = new List<Vector3>(); nm.GetVertices(V); var Nn = new List<Vector3>(); nm.GetNormals(Nn); bool hn = Nn.Count == V.Count;
                var U0 = new List<Vector2>(); nm.GetUVs(0, U0); bool hu = U0.Count == V.Count; var idx = new List<int>();
                var other = new List<Vector2>[3]; for (int ch = 1; ch < 4; ch++) { other[ch - 1] = new List<Vector2>(); nm.GetUVs(ch, other[ch - 1]); }
                foreach (var (a, b, c, bi) in mir)
                {
                    var bx = boxes[bi]; var n = bx.n.sqrMagnitude > .01f ? bx.n.normalized : Vector3.back; var up = Vector3.ProjectOnPlane(Vector3.up, n).normalized; var rgt = Vector3.Cross(up, n);
                    var ctr = (bx.min + bx.max) / 2; float w = Mathf.Max(.02f, Mathf.Abs(Vector3.Dot(bx.max - bx.min, rgt))), h = Mathf.Max(.02f, Mathf.Abs(Vector3.Dot(bx.max - bx.min, up)));
                    foreach (var vi in new[] { a, b, c })
                    {
                        var p = M.MultiplyPoint3x4(P[vi]) - ctr; V.Add(V[vi]); if (hn) Nn.Add(Nn[vi]);
                        if (hu) U0.Add(new Vector2(.5f - Vector3.Dot(p, rgt) / w, .5f + Vector3.Dot(p, up) / h));
                        for (int ch = 0; ch < 3; ch++) if (other[ch].Count == V.Count - 1) other[ch].Add(other[ch][vi]);
                        idx.Add(V.Count - 1);
                    }
                }
                if (V.Count > 65000) nm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                nm.SetVertices(V); if (hn) nm.SetNormals(Nn); if (hu) nm.SetUVs(0, U0); for (int ch = 0; ch < 3; ch++) if (other[ch].Count == V.Count) nm.SetUVs(ch + 1, other[ch]);
                nm.SetTriangles(idx, sc); nm.RecalculateBounds(); mf.sharedMesh = nm;
                var nmats = new Material[sc + 1]; for (int k = 0; k < sc; k++) nmats[k] = k < mats.Length ? mats[k] : mats[^1]; nmats[sc] = Chrome; mr.sharedMaterials = nmats;
            }
        }
        // carro do jogador: câmera de ré numa RenderTexture (no REB uma só para todos os espelhos)
        public static void OnEnter(FBCar car)
        {
            if (!car || car.ie || Boxes(car.id).Count == 0) return;
            if (!rt) { rt = new RenderTexture(256, 128, 16) { name = "retrovisor" }; rt.Create(); }
            if (!live) { live = new Material(FBShaders.Unlit) { name = "veh_espelho_vivo" }; live.SetTexture("_BaseMap", rt); live.mainTexture = rt; live.SetColor("_BaseColor", new Color(.92f, .94f, .96f)); }
            if (!cam)
            {
                var g = new GameObject("CameraRetrovisor"); cam = g.AddComponent<Camera>(); cam.targetTexture = rt; cam.fieldOfView = 55; cam.nearClipPlane = .3f; cam.farClipPlane = 160; cam.depth = -50;
                var d = g.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); d.renderShadows = false; d.renderPostProcessing = false; d.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                cam.cullingMask = Camera.main ? Camera.main.cullingMask & ~(1 << 5) : ~(1 << 5);
            }
            var inner = Boxes(car.id).Find(b => Mathf.Abs((b.min.x + b.max.x) / 2) < .35f); var p = inner.max != Vector3.zero ? (inner.min + inner.max) / 2 : new Vector3(0, car.lbox.max.y * .8f, car.lbox.center.z + car.lbox.extents.z * .3f);
            cam.transform.SetParent(car.shell, false); cam.transform.localPosition = p + Vector3.up * .05f; cam.transform.localRotation = Quaternion.Euler(4, 180, 0); cam.gameObject.SetActive(true);
            Swap(car, live); camCar = car;
        }
        public static void OnExit(FBCar car)
        {
            if (cam) { cam.gameObject.SetActive(false); cam.transform.SetParent(null, false); }
            if (car) Swap(car, Chrome); camCar = null;
        }
        static void Swap(FBCar car, Material to)
        {
            foreach (var r in car.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials; bool ch = false;
                for (int i = 0; i < ms.Length; i++) if (ms[i] && ms[i].name.StartsWith("veh_espelho") && ms[i] != to) { ms[i] = to; ch = true; }
                if (ch) r.sharedMaterials = ms;
            }
        }
    }
}
