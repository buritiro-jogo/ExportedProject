// AMBIENTAÇÃO DAS RUAS COM OS OBJETOS DO 171 (rodada 44 — pedido 28/10: "aproveitar ao máximo os elementos urbanos ... postes, placas,
// cones e barreiras, equipamentos elétricos, centrais de ar-condicionado e condensadoras ... mesas e cadeiras, garrafas, latas de cerveja,
// copos ... distribua naturalmente pelo mapa, evitando repetições excessivas e respeitando as características reais dos locais de Boa Vista").
// Objetos: Resources/X171/Urbano/{Placas,Rua,Comercio,Obra,Lixo,Bar}.glb (tools/x171/props171.py; uso condicionado à autorização da
// Betagames como os outros X171_*; -fbsem171props ou apagar a pasta tira tudo).
// Como distribui: percorre a malha de ruas (FBRoads) a cada ~22 m, dos dois lados, acha o meio-fio e a calçada, e sorteia (semente do mapa:
// igual em todos os clientes) o que cabe ali: lixeira na beira da calçada, placa de trânsito perto das esquinas (pare, proibido estacionar,
// lombada, faixa de pedestre…), ponto de ônibus nos trechos longos, orelhão/caixa do correio, condensadora de ar-condicionado na parede (2–3,5 m),
// lixo junto ao muro (caixas de papelão, pallet, tambor, pneu), cones e cavalete, e "botecos": mesa de plástico com 2–4 cadeiras, cerveja,
// cachaça e latinhas em cima, perto de parede (os NPCs do rolê sentam — FBRole.seats). Nada no asfalto, em porta/passagem estreita ou
// encostado em outro objeto; nada repete o mesmo modelo lado a lado; limite por qualidade (celular ~40 %).
// Física: o que é solto cai/tomba/rola/é empurrado com o som do 171 (FBFisica); poste, placa, orelhão, ponto e condensadora ficam fixos.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBVestirRua
    {
        public static bool On = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbsem171props") < 0;
        public static readonly Dictionary<string, int> Count = new(); public static int Total; public static GameObject Root;
        static readonly Dictionary<string, GameObject> libs = new();
        static readonly string[] LIXEIRA = { "SM_lixeiras1", "SM_lixeiras2", "SM_lixeiras3", "SM_lixeiras4", "SM_Lixeira7", "LixeiraPoste" };
        static readonly string[] PLACA_ESQ = { "PlacasRegulamentacao_Pare", "PlacasRegulamentacao_ProibidoEstacionar", "PlacasAdvertencia_Lombada", "PlacasAdvertencia_FaixaPedestre",
            "PlacasRegulamentacao_Limite40km", "PlacasRegulamentacao_ProibidoUltrapassar", "PlacasRegulamentacao_ProibidoRetornoEsquerda", "PlacasAdvertencia_Semaforo", "PlacasRegulamentacao_PodeEstacionar" };
        static readonly string[] LIXO = { "SM_CaixaPapelao1", "SM_CaixaPapelao2", "SM_CaixaPapelao3", "SM_CaixaPapelao5", "SM_CaixaPapelao8", "SM_CaixaPapelao10", "SM_Pallet_03", "SM_WoodenPallet", "SM_Barrel_01", "SM_Tire_02", "Papelao" };
        static readonly string[] MESA = { "SM_Mesa1", "SM_Table02_003" }, CADEIRA = { "SM_Cadeira1", "SM_CadeiraDeBar2" }, NA_MESA = { "Garrafa_de_Cerveja", "Garrafa_de_Cerveja", "Garrafa_de_Cachaca", "Garrafa3", "SM_CanecaChopp", "BandeijaCopos", "FardoLata" };
        // massa (kg) e material de cada objeto solto
        static readonly Dictionary<string, (string mat, float kg)> FIS = new()
        {
            ["SM_lixeiras1"] = ("plastico", 6), ["SM_lixeiras2"] = ("plastico", 6), ["SM_lixeiras3"] = ("plastico", 7), ["SM_lixeiras4"] = ("plastico", 7), ["SM_Lixeira7"] = ("metal", 12),
            ["SM_CaixaPapelao1"] = ("papelao", 1.5f), ["SM_CaixaPapelao2"] = ("papelao", 1.2f), ["SM_CaixaPapelao3"] = ("papelao", 2), ["SM_CaixaPapelao5"] = ("papelao", 1.8f), ["SM_CaixaPapelao8"] = ("papelao", 1), ["SM_CaixaPapelao10"] = ("papelao", 2.4f), ["Papelao"] = ("papelao", .6f),
            ["SM_Pallet_03"] = ("madeira", 15), ["SM_WoodenPallet"] = ("madeira", 15), ["SM_Barrel_01"] = ("metal_pesado", 22), ["SM_Tire_02"] = ("plastico", 9),
            ["SM_ConedeTransito1"] = ("plastico", 2.2f), ["SM_ConedeTransito2"] = ("plastico", 2.2f), ["SM_BarreiradeTransito1"] = ("plastico", 9), ["Cavalete1"] = ("madeira", 6), ["Cavalete2"] = ("madeira", 6),
            ["SM_Mesa1"] = ("plastico", 4.5f), ["SM_Table02_003"] = ("madeira", 14), ["SM_Cadeira1"] = ("plastico", 2.4f), ["SM_CadeiraDeBar2"] = ("plastico", 2.6f),
            ["Garrafa_de_Cerveja"] = ("vidro", .6f), ["Garrafa_de_Cachaca"] = ("vidro", .9f), ["Garrafa3"] = ("vidro", .6f), ["SM_CanecaChopp"] = ("vidro", .4f), ["BandeijaCopos"] = ("vidro", .8f), ["FardoLata"] = ("lata", 4.8f),
        };

        static GameObject Lib(string g) { if (!libs.TryGetValue(g, out var l)) libs[g] = l = Resources.Load<GameObject>("X171/Urbano/" + g); return l; }
        static GameObject Make(string g, string name, Transform parent)
        {
            var pf = Lib(g); if (!pf) return null; var src = pf.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.parent == pf.transform && t.name.Split('.')[0] == name); if (!src) return null;
            var go = Object.Instantiate(src.gameObject, parent); go.name = "x171_" + name; go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            Count[name] = (Count.TryGetValue(name, out var c) ? c : 0) + 1; Total++; return go;
        }
        static Bounds Bnd(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>(); var b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one * .1f); foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        /// <summary>Põe o objeto com a base em 'p', virado para 'yaw'; colisor de caixa; física se for solto.</summary>
        static GameObject Put(string g, string name, Vector3 p, float yaw, bool fixedObj = false)
        {
            var go = Make(g, name, Root.transform); if (!go) return null; go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation;
            var b = Bnd(go); go.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y + .005f, p.z - b.center.z);
            b = Bnd(go); var bc = go.AddComponent<BoxCollider>(); bc.center = go.transform.InverseTransformPoint(b.center); bc.size = Vector3.Max(go.transform.InverseTransformVector(b.size).Abs3(), Vector3.one * .02f);
            go.layer = fixedObj ? FBWorld.LMap : 0; foreach (Transform t in go.transform) t.gameObject.layer = go.layer;
            if (!fixedObj && FIS.TryGetValue(name, out var f)) FBFisica.Make(go, f.mat, f.kg);
            return go;
        }
        static bool Clear(Vector3 p, float r, float h = 1.4f) => !Physics.CheckBox(p + Vector3.up * (h * .5f + .08f), new Vector3(r, h * .5f, r), Quaternion.identity, FBWorld.SolidMask | 1 | (1 << FBWorld.LChar), QueryTriggerInteraction.Ignore);

        static bool TryBar(Vector3 edge, Vector3 r, float width, System.Func<float> R, System.Func<IList<string>, string> Pk)
        {
            var c = edge + r * Mathf.Min(width * .5f, 1.9f); if (!FBStreetEvents.Ground(ref c, edge.y, .5f) || !Clear(c, 1.0f)) return false;
            var mesa = PutG("Bar", Pk(MESA), c, R() * 90); if (!mesa) return false;
            var top = Bnd(mesa).max.y; int nc = 2 + (int)(R() * 3);
            for (int i = 0; i < nc; i++)
            {
                float a = i * (360f / nc) + R() * 25; var dir = Quaternion.Euler(0, a, 0) * Vector3.forward; var cp = c + dir * .78f; if (!FBStreetEvents.Ground(ref cp, c.y, .4f)) continue;
                if (!PutG("Bar", Pk(CADEIRA), cp, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg)) continue;
                FBRole.seats.Add(new FBSeat { p = cp, yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg, kind = "cadeira", grp = mesa });
            }
            for (int i = 0, nb = 2 + (int)(R() * 3); i < nb; i++) { var q = c + new Vector3(R() - .5f, 0, R() - .5f) * .45f; q.y = top; Put("Bar", Pk(NA_MESA), q, R() * 360); }
            for (int i = 0, nf = 1 + (int)(R() * 3); i < nf; i++) { var q = c + Quaternion.Euler(0, R() * 360, 0) * Vector3.forward * (1.0f + R() * .6f); if (FBStreetEvents.Ground(ref q, c.y, .4f)) PutG("Bar", R() < .7f ? "Garrafa_de_Cerveja" : "FardoLata", q, R() * 360); }
            return true;
        }
        // r45 (pedido: "deixar de espalhar objetos aleatoriamente ... organizar de forma lógica, sem objetos flutuando, menos repetição"):
        // cada peça tem o SEU lugar de rua de verdade, decidido pela malha de faixas — o sorteio só escolhe o modelo e pula vagas:
        //  · placa de trânsito: só nas esquinas, na calçada da DIREITA de quem chega (uma por chegada), de frente para ele;
        //  · lixeira: a cada ~45 m da calçada (fora dos 10 m da esquina), na beira do meio-fio; às vezes um saco/caixa ao lado;
        //  · ponto de ônibus: no meio dos trechos longos (> 90 m), do lado direito, com a placa de ponto 2,2 m antes;
        //  · orelhão / caixa do correio: 9–15 m depois da esquina, 1 a cada ~4 quarteirões;
        //  · boteco de esquina: mesa + cadeiras + bebida junto à parede, 4–12 m da esquina, no máximo um por quarteirão;
        //  · condensadora: nas fachadas (parede), 2–3,5 m, em grupos da mesma parede; lixo (caixas/pallet/tambor) só junto ao muro da esquina;
        //  · cones, cavaletes e barreiras saíram da decoração (ficam na obra e nos eventos);
        //  · nada flutua: a base é medida nos 4 cantos (degrau, meio-fio, rampa) — sobrou vão > 8 cm, a peça não fica;
        //  · menos repetição: o modelo menos usado do grupo tem preferência.
        public static int Floating, Removed;
        static readonly List<GameObject> placed = new();
        static bool Grounded(GameObject go)
        {
            if (!go) return false; var b = Bnd(go); float y0 = b.min.y, gap = 0; int n = 0;
            var cs = new[] { new Vector3(b.min.x, 0, b.min.z), new Vector3(b.max.x, 0, b.min.z), new Vector3(b.min.x, 0, b.max.z), new Vector3(b.max.x, 0, b.max.z) };
            var cols = go.GetComponentsInChildren<Collider>(); foreach (var c in cols) c.enabled = false;
            try
            {
                foreach (var c in cs)
                {
                    var q = Vector3.Lerp(new Vector3(b.center.x, 0, b.center.z), c, .85f);
                    if (Physics.Raycast(new Vector3(q.x, y0 + .5f, q.z), Vector3.down, out var h, 1.5f, FBWorld.SolidMask, QueryTriggerInteraction.Ignore)) { gap = Mathf.Max(gap, y0 - h.point.y); n++; }
                    else gap = 9;
                }
            }
            finally { foreach (var c in cols) if (c) c.enabled = true; }
            return n >= 3 && gap < .08f;
        }
        static GameObject PutG(string g, string name, Vector3 p, float yaw, bool fixedObj = false)
        {
            var go = Put(g, name, p, yaw, fixedObj); if (!go) return null;
            if (!Grounded(go)) { Floating++; Removed++; Count[name]--; Total--; Object.DestroyImmediate(go); return null; }
            placed.Add(go); return go;
        }
        static string Least(IList<string> l, System.Func<float> R) { int m = int.MaxValue; foreach (var n in l) m = Mathf.Min(m, Count.TryGetValue(n, out var c) ? c : 0); var opts = l.Where(n => (Count.TryGetValue(n, out var c) ? c : 0) == m).ToList(); return opts[Mathf.Min(opts.Count - 1, (int)(R() * opts.Count))]; }
        public static void Dress()
        {
            Count.Clear(); Total = 0; Floating = Removed = 0; placed.Clear(); if (Root) Object.Destroy(Root);
            if (!On || !FBRoads.Ready || !Lib("Rua")) { Debug.Log($"[FreeBala][vestir] sem objetos do 171 ({(!On ? "desligado" : !FBRoads.Ready ? "sem malha de ruas" : "sem Resources/X171/Urbano")})"); return; }
            Root = new GameObject("FB_Rua171"); var sw = System.Diagnostics.Stopwatch.StartNew();
            string mapKey = FBSession.map ?? "map"; uint h = 2166136261; foreach (var ch in mapKey) h = (h ^ ch) * 16777619; var rnd = FBVehicles.Seeded(h);
            float R() => rnd(); T Pk<T>(IList<T> l) => l[Mathf.Min(l.Count - 1, (int)(R() * l.Count))];
            int budget = FBQuality.Mobile ? 140 : 380, bars = 0, maxBars = FBQuality.Mobile ? 5 : 12, orel = 0, segN = 0;
            var segs = FBRoads.segs.Where(S => S.kind == 0 && !S.ring && S.len > 12).ToList();
            // calçada à direita do sentido da faixa no ponto s: borda (depois do meio-fio), direção para dentro, parede e largura
            bool Side(FBSeg S, float s, out Vector3 edge, out Vector3 r, out Vector3 tan, out bool wall, out RaycastHit wh, out float width)
            {
                var lp = FBRoads.At(S, s, out tan); tan = tan.Flat().normalized; r = new Vector3(tan.z, 0, -tan.x); edge = default; wall = false; wh = default; width = 0;
                if (tan.sqrMagnitude < .01f) return false;
                float curb = -1; for (float o = .75f; o < 8f; o += .25f) if (!FBRoads.IsRoad(lp + r * o)) { curb = o; break; }
                if (curb < 1) return false;
                edge = lp + r * (curb + .45f); if (!FBStreetEvents.Ground(ref edge, lp.y, .6f) || FBRoads.IsRoad(edge) || !FBNav.InsideNear(edge, 3)) return false;
                wall = Physics.Raycast(edge + Vector3.up * 1.2f, r, out wh, 7f, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore) && Mathf.Abs(wh.normal.y) < .3f; width = wall ? wh.distance : 7;
                return true;
            }
            foreach (var S in segs)
            {
                if (Total >= budget) break; segN++;
                float yawOf(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;
                // 1) placa na chegada da esquina (fim da faixa), de frente para quem vem
                if (S.len > 20 && R() < .7f && Side(S, S.len - 4.5f, out var e1, out var r1, out var t1, out _, out _, out _) && Clear(e1, .25f, 2.2f))
                    PutG("Placas", Least(PLACA_ESQ, R), e1, yawOf(-t1), true);
                // 2) lixeiras a cada ~45 m, fora das esquinas
                for (float s = 18 + R() * 10; s < S.len - 12 && Total < budget; s += 40 + R() * 10)
                {
                    if (!Side(S, s, out var e, out var r, out var t, out var wall, out var wh, out var width) || !Clear(e, .35f)) continue;
                    var n = Least(LIXEIRA, R); var lx = PutG("Rua", n, e, yawOf(-r), n == "LixeiraPoste");
                    if (lx && R() < .3f) { var q = e + t * .7f; FBStreetEvents.Ground(ref q, e.y, .4f); if (Clear(q, .3f)) PutG("Lixo", Pk(new[] { "SM_CaixaPapelao1", "SM_CaixaPapelao3", "Papelao" }), q, R() * 360); }
                }
                // 3) ponto de ônibus no meio dos trechos longos
                if (S.len > 90 && R() < .55f && Side(S, S.len / 2, out var e3, out var r3, out var t3, out _, out _, out var w3) && w3 > 2.6f)
                {
                    var p = e3 + r3 * .8f; if (Clear(p, 1.3f, 2.4f) && PutG("Rua", R() < .5f ? "PontoOnibus" : "SM_PontoOnibus1", p, yawOf(-r3), true) != null) PutG("Placas", "PlacasAuxiliares_PontoOnibus", e3 - t3 * 2.2f, yawOf(-t3), true);
                }
                // 4) orelhão / correio logo depois da esquina (começo da faixa)
                if (S.len > 30 && (segN % 4 == 0) && Side(S, 9 + R() * 6, out var e4, out var r4, out _, out _, out _, out _) && Clear(e4, .5f, 2.2f))
                    if (PutG("Rua", R() < .75f ? Least(new[] { "SM_Orelhao1", "SM_Orelhao2", "SM_Orelhao3" }, R) : "SM_Caixadoscorreios1", e4, yawOf(-r4), true)) orel++;
                // 5) boteco de esquina + lixo do muro da esquina
                if (S.len > 24 && bars < maxBars && R() < .35f && Side(S, S.len - (4 + R() * 8), out var e5, out var r5, out var t5, out var wall5, out var wh5, out var w5) && wall5 && w5 > 2.6f)
                { if (TryBar(e5, r5, w5, R, Pk)) bars++; }
                else if (S.len > 24 && R() < .3f && Side(S, S.len - (6 + R() * 4), out var e6, out var r6, out var t6, out var wall6, out var wh6, out var w6) && wall6 && w6 > 1.8f)
                {
                    var at = wh6.point - r6 * .55f; at.y = e6.y; if (FBStreetEvents.Ground(ref at, e6.y, .6f) && Clear(at, .5f))
                    { int k = 2 + (int)(R() * 2); var used = new HashSet<string>(); for (int i = 0; i < k; i++) { var n = Least(LIXO, R); if (!used.Add(n)) continue; var q = at + t6 * ((i - k / 2f) * .65f); if (FBStreetEvents.Ground(ref q, at.y, .5f)) PutG("Lixo", n, q, R() * 360); } }
                }
                // 6) condensadoras na fachada (2–3 da mesma parede), no meio da quadra
                if (R() < .45f && Side(S, S.len * (.3f + R() * .4f), out var e7, out var r7, out var t7, out var wall7, out _, out _) && wall7)
                    for (int i = 0, k = 1 + (int)(R() * 3); i < k; i++)
                    {
                        float hgt = 2 + R() * 1.5f; var o = e7 + t7 * (i * 2.4f) + Vector3.up * hgt;
                        if (!Physics.Raycast(o, r7, out var w2, 7, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore) || Mathf.Abs(w2.normal.y) > .3f) continue;
                        var ac = Make("Comercio", "ArCondicionado_Exterior", Root.transform);
                        if (ac) { ac.transform.rotation = Quaternion.LookRotation(w2.normal.Flat().normalized, Vector3.up) * ac.transform.rotation; var bb = Bnd(ac); var tgt = w2.point + w2.normal * (bb.extents.magnitude * .25f); ac.transform.position += tgt - bb.center; ac.layer = FBWorld.LMap; }
                    }
            }
            Physics.SyncTransforms();
            Debug.Log($"[FreeBala][vestir] {Total} objetos do 171 em {sw.ElapsedMilliseconds} ms · botecos {bars} · orelhões/correio {orel} · tirados por flutuar {Floating} · " + string.Join(", ", Count.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Take(14).Select(kv => kv.Key + " " + kv.Value)));
        }
    }
}
