// EVENTOS DE RUA DO 171 — rodada 43 (usuário: "Cena do carro de energia: localize e extraia todos os elementos dessa cena do 171, incluindo
// o veículo, personagens, escadas, equipamentos, animações, interações e demais mecânicas ... exatamente como funciona no jogo original").
// Reprodução do BP_EletricistaEscada do 171 (pseudocódigo: tools/re/out/171/BP_EletricistaEscada.txt):
//   · eletricista no alto da escada encostada no poste, clipe EletricistaTrabalhandoPoste (idle tipo 3 do 171) em laço;
//   · carro de serviço (Micto do 171, com o rack de escada no teto) estacionado ao lado, junto ao meio-fio;
//   · jogador perto (< 7 m): falas do eletricista do 171 ("Sai de perto da escada por favor!", "Tá vendo a sinalização não? Se afasta"…);
//   · carro/moto bate na escada (caixa ColisaoCarro do BP) ou tiro nele -> FinalizarEvento: a escada cai pela física, ele cai em ragdoll;
//   · 5 s depois (Delay 5 do BP): 50 % foge correndo ("IA Sair Correndo"), 50 % parte para cima do jogador ("IA Entrar Combat Player":
//     sem corpo a corpo no jogo — avança xingando e dá um empurrão, clipe de esbarrão do 171).
// Objetos/clipes/vozes do 171: Resources/X171/Eventos (CarroServico.glb, Obra.glb), X171_EletricistaTrabalhandoPoste, Audio/171/x171_eletricista_N
// — mesmo uso condicionado à autorização da Betagames (tools/vj_full/x171/LEIAME.md, docs/re/171_SISTEMAS.md).
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    // FBEletricista: ver FBEletricista.cs (rodada 44: carro com escada + corte de energia)
    // ================================================================ OBRA DE RUA (171: BP_ObraDeRua + Child1–3)
    // Os três leiautes do 171 (cm, frame do Unreal: X frente, Y direita; no Unity (−X, Z, Y)/100 e o mesmo yaw), peças do Obra.glb (caçamba,
    // caixa de ferramentas com marmita, garrafa de café e enxada, manilha, cercas, placa da prefeitura, cones, lonas, montes de areia/brita) e
    // os 4 pedreiros do BP: medindo (ObraDeRua_Medindo), vendo a planta (ObraDeRua_VendoPlanos), varrendo (ObraDeRua_Varrendo) e um parado.
    // Som ambiente da obra do 171 (EventosAleatorios.bank). Tiro perto: todo mundo corre.
    public static class FBObra
    {
        public static string Why; public static bool Active => root; public static Vector3 Center => center; public static List<FBCharacter> Workers => npcs;
        public static float Cooldown() => Random.Range(260f, 480f);
        static GameObject root; static Vector3 center; static readonly List<FBCharacter> npcs = new(); static float life; static bool fled;
        static void Log(string s) => FBStreetEvents.L("obra: " + s);
        // (peça, X, Y, yaw) — peça pelo nome no Obra.glb; "*" = pedreiro (com o clipe depois do *)
        static readonly (string m, float x, float y, float yaw)[][] LAYOUTS = {
            new[] { ("Cacamba_grande", 310f, -586f, -90f), ("SM_Crate_01a", 310f, 392f, -20f), ("SM_ConcretePipe_03", -104f, -145f, -10f), ("Cerca_de_obra1", 219f, -24f, -10f),
                    ("Placa_de_prefeitura", -66f, 344f, 125f), ("SM_ConstructionPylons_01d", -141f, -629f, 70f), ("SM_ConstructionPylons_01e", -55f, -620f, 23f),
                    ("SM_ConstructionPylons_01d", -283f, -500f, 93f), ("SM_ConstructionPylons_01e", -259f, -572f, 124f), ("SM_ConstructionPylons_01d", -162f, -567f, 28f),
                    ("SM_Tarp_01", -619f, 213f, -67f), ("SM_Tarp_02", -212f, -490f, -5f), ("SM_Tarp_03", -141f, 62f, -76f), ("Monte_de_areia", -55f, -357f, -3f), ("Monte_de_brita", 146f, -293f, -19f),
                    ("*ObraDeRua_Medindo", -84f, -81f, -105f), ("*ObraDeRua_VendoPlanos", 89f, 171f, -95f), ("*ObraDeRua_Varrendo", 304f, 283f, -135f), ("*", 98f, -20f, 90f) },
            new[] { ("Cacamba_grande", 0f, 187f, 4f), ("SM_Crate_01a", -283f, -121f, -22f), ("Cerca_de_obra1", -397f, -409f, 68f), ("Cerca_de_obra1", -557f, -239f, 31f), ("Cerca_de_obra1", -646f, -46f, 15f),
                    ("Cerca_de_obra1", -103f, -431f, 92f), ("Cerca_de_obra1", 129f, -424f, 88f), ("Cerca_de_obra1", 364f, -418f, 96f), ("Placa_de_prefeitura", -755f, -63f, -145f),
                    ("SM_ConstructionPylons_01d", 473f, -307f, 12f), ("SM_ConstructionPylons_01e", 531f, -259f, -40f), ("SM_ConstructionPylons_01d", 483f, -213f, -94f), ("SM_ConstructionPylons_01d", 551f, -204f, -15f),
                    ("SM_ConstructionPylons_01d", 516f, -149f, -37f), ("SM_ConstructionPylons_01d", 581f, -92f, -35f), ("SM_Tarp_01", 197f, 95f, -170f), ("SM_Tarp_03", -418f, 34f, -172f),
                    ("Monte_de_areia", 248f, -103f, -55f), ("Monte_de_brita", 98f, -47f, -55f),
                    ("*", -457f, 205f, -29f), ("*ObraDeRua_Varrendo", -427f, 11f, 34f), ("*ObraDeRua_Medindo", 115f, -124f, 0f), ("*ObraDeRua_VendoPlanos", 216f, -70f, -197f) },
            new[] { ("Cacamba_grande", 411f, 443f, 2f), ("SM_Crate_01a", -537f, 174f, 14f), ("SM_ConcretePipe_03", -39f, -156f, 99f), ("Cerca_de_obra2", -805f, -348f, -6f), ("Cerca_de_obra2", -769f, -143f, -15f),
                    ("Cerca_de_obra2", -652f, 10f, -63f), ("Cerca_de_obra2", -459f, 52f, -92f), ("Cerca_de_obra2", -253f, 56f, -86f), ("Placa_de_prefeitura", -930f, -160f, 149f),
                    ("SM_ConstructionPylons_01d", 759f, -396f, -68f), ("SM_ConstructionPylons_01d", 651f, -329f, -35f), ("SM_ConstructionPylons_01e", 687f, -400f, 36f), ("SM_ConstructionPylons_01d", 681f, -252f, 8f),
                    ("SM_ConstructionPylons_01d", 638f, -173f, 17f), ("SM_ConstructionPylons_01d", 587f, -94f, 17f), ("SM_ConstructionPylons_01d", 120f, 133f, -15f),
                    ("SM_Tarp_01", 76f, 429f, -170f), ("SM_Tarp_04", 280f, 124f, -104f), ("SM_Tarp_06", 88f, 36f, -170f), ("Monte_de_brita", -431f, 73f, -55f), ("Monte_de_areia", 68f, -474f, -55f),
                    ("Monte_de_brita", 163f, -501f, -55f), ("Monte_de_areia", -477f, 181f, -55f),
                    ("*ObraDeRua_Varrendo", 79f, 125f, -181f), ("*", -635f, -191f, 38f), ("*ObraDeRua_Medindo", -149f, -118f, -14f), ("*ObraDeRua_VendoPlanos", -495f, 57f, -245f) } };
        static readonly string[] EXTRA_CAIXA = { "Marmita_Marmitex_tampada", "Garrafa_de_cafe_5L", "Enxada" };
        static GameObject Piece(string name)
        {
            var pf = Resources.Load<GameObject>("X171/Eventos/Obra"); if (!pf) return null;
            var src = pf.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.parent == pf.transform && t.name.Split('.')[0] == name); if (!src) return null;
            var go = Object.Instantiate(src.gameObject); go.name = "x171_" + name; return go;
        }
        static Bounds Bnds(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>(); var b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.zero); foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        public static bool Start()
        {
            Why = root ? "já ativo" : !FBRegistry.I ? "sem registro" : !Resources.Load<GameObject>("X171/Eventos/Obra") ? "sem X171/Eventos/Obra" : null; if (Why != null) return false;
            var pp = FBStreetEvents.PlayerPos; float near = FBStreetEvents.ForceNear; int nNav = 0, nDist = 0, nVis = 0, nLane = 0, nRoad = 0, nFlat = 0, nBox = 0, nWall = 0, T = near > 0 ? 400 : 60;
            for (int tries = 0; tries < T; tries++)
            {
                var p = FBNav.RandomPoint(pp, near > 0 ? near + 40 : 120); if (p == null) { nNav++; continue; } var c = p.Value; float d = (c - pp).Flat().magnitude;
                if (near > 0 ? Mathf.Abs(d - near) > 45 : (d < 45 || d > 130)) { nDist++; continue; }
                if (near <= 0 && FBStreetEvents.Visible(c + Vector3.up)) { nVis++; continue; }
                var L = FBRoads.NearestLane(c, 16, out var s, out _, null); if (L == null) { nLane++; continue; }
                FBRoads.At(L, s, out var tan); tan.y = 0; if (tan.sqrMagnitude < .01f) { nLane++; continue; } tan.Normalize();
                if (FBRoads.IsRoad(c)) { nRoad++; continue; }                                                       // fora do asfalto (calçada/terreno junto da rua)
                bool flat = true;
                for (int k = 0; k < 8 && flat; k++) { var q = c + Quaternion.Euler(0, k * 45, 0) * Vector3.forward * 5.5f; if (!(FBWorld.GroundY(q.x, c.y + 1.5f, q.z, .3f, 3) is float gy) || Mathf.Abs(gy - c.y) > .5f || FBRoads.IsRoad(q)) flat = false; }
                if (!flat) { nFlat++; continue; }
                if (Physics.CheckBox(c + Vector3.up * 1.4f, new Vector3(5.5f, 1.1f, 5.5f), Quaternion.identity, (FBWorld.SolidMask & ~(1 << FBWorld.LMap)) | (1 << FBWorld.LVehicle), QueryTriggerInteraction.Ignore)) { nBox++; continue; }
                if (Physics.CheckBox(c + Vector3.up * 1.6f, new Vector3(5f, .9f, 5f), Quaternion.identity, 1 << FBWorld.LMap, QueryTriggerInteraction.Ignore)) { nWall++; continue; }   // parede/poste no meio
                Build(c, Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg); Why = $"montou a {d:0} m"; return true;
            }
            Why = $"{T} tentativas: sem ponto {nNav} · distância {nDist} · à vista {nVis} · sem faixa a 16 m {nLane} · no asfalto {nRoad} · não plano/rua a 5,5 m {nFlat} · carro/objeto {nBox} · parede/poste {nWall}"; return false;
        }
        static void Build(Vector3 c, float yaw)
        {
            root = new GameObject("FB_Evento_Obra"); center = c; root.transform.SetPositionAndRotation(c, Quaternion.Euler(0, yaw, 0)); npcs.Clear(); life = 0; fled = false;
            var lay = LAYOUTS[Random.Range(0, LAYOUTS.Length)]; int props = 0;
            foreach (var (m, x, y, yw) in lay)
            {
                var wp = root.transform.TransformPoint(new Vector3(-x / 100f, 0, y / 100f)); float wyaw = yaw + yw;
                if (m.StartsWith("*"))
                {
                    var clip = m.Length > 1 ? "X171_" + m.Substring(1) : null; var g = wp; FBStreetEvents.Ground(ref g, c.y, 1.2f);
                    var id = FBStreetEvents.FreeIds(FBNpcAnims.ByCls("pedreiro")).FirstOrDefault();   // r52: só NPC com função pedreiro (os do 171 saíram); sem ele a obra fica parada, sem civil no lugar
                    var npc = FBStreetEvents.Spawn(id, g, wyaw, "obra"); if (!npc) continue; npcs.Add(npc);
                    var a = npc.GetOrAdd<FBStreetActor>(); a.life = false; a.faceYaw = wyaw;   // [parceiro 01/11] '??' não pega o null falso da Unity (Editor): GetOrAdd testa com bool
                    if (clip != null && FBRegistry.I.Clip(clip)) npc.PlayDance(clip);
                    continue;
                }
                var go = Piece(m); if (!go) continue; go.transform.SetParent(root.transform, true);
                go.transform.rotation = Quaternion.Euler(0, wyaw, 0) * go.transform.rotation;
                var b = Bnds(go); var gp = wp; FBStreetEvents.Ground(ref gp, c.y, 1.2f); go.transform.position += new Vector3(wp.x - b.center.x, gp.y - b.min.y, wp.z - b.center.z);
                if (!m.StartsWith("SM_Tarp")) { var bb = Bnds(go); var col = go.AddComponent<BoxCollider>(); col.center = go.transform.InverseTransformPoint(bb.center); col.size = go.transform.InverseTransformVector(bb.size).Abs(); }
                // r44: o que solta na obra cai/tomba/rola (massa e material reais); caçamba, montes e manilha ficam fixos
                if (m.StartsWith("SM_ConstructionPylons")) FBFisica.Make(go, "plastico", 2.2f); else if (m.StartsWith("Cerca_de_obra")) FBFisica.Make(go, "grade", 14); else if (m == "Placa_de_prefeitura") FBFisica.Make(go, "metal", 9); else if (m == "SM_Crate_01a") FBFisica.Make(go, "madeira", 12);
                if (m == "SM_Crate_01a")
                {
                    float h = Bnds(go).max.y; int k = 0;
                    foreach (var ex in EXTRA_CAIXA) { var e = Piece(ex); if (!e) continue; e.transform.SetParent(go.transform, true); var eb = Bnds(e); var ep = Bnds(go).center + go.transform.right * (k - 1) * .25f; e.transform.position += new Vector3(ep.x - eb.center.x, h - eb.min.y, ep.z - eb.center.z); k++; }
                }
                props++;
            }
            FBStreetEvents.NoCollide(npcs);
            // r45: pedestres desviam da obra — o NavMesh ganha um buraco na área das peças (os pedreiros já estão posicionados)
            { var bb = new Bounds(c, Vector3.zero); bool any = false; foreach (var r in root.GetComponentsInChildren<Renderer>()) { if (r.GetComponentInParent<FBCharacter>()) continue; if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
              if (any) { var ob = new GameObject("obra_desvio"); ob.transform.SetParent(root.transform, false); ob.transform.position = bb.center; ob.transform.rotation = Quaternion.identity; var no = ob.AddComponent<UnityEngine.AI.NavMeshObstacle>(); no.shape = UnityEngine.AI.NavMeshObstacleShape.Box; no.size = bb.size + new Vector3(.6f, 0, .6f); no.carving = true; no.carveOnlyStationary = true; } }
            var amb = FBAudio.Clip("x171_obra_ambiente");
            if (amb) { var src = root.AddComponent<AudioSource>(); src.clip = amb; src.loop = true; src.spatialBlend = 1; src.minDistance = 4; src.maxDistance = 45; src.rolloffMode = AudioRolloffMode.Linear; FBAudio.Track(src, .8f, FBAudio.Cat.Ambient); src.Play(); }
            Log($"montada a {(c - FBStreetEvents.PlayerPos).Flat().magnitude:0} m · {props} peças · {npcs.Count} pedreiros");
        }
        public static void Tick(float dt)
        {
            if (!root) return; life += dt;
            // tiro perto: todos correm (FBBotBrain.Noise publicado pelo FBCombat)
            if (!fled && FBBotBrain.lastNoiseP is Vector3 np && Time.time - FBBotBrain.lastNoiseT < 1 && (np - center).magnitude < 28)
            {
                fled = true; Log("tiro perto: pedreiros correm");
                foreach (var n in npcs)
                {
                    if (!n) continue; n.StopDance(true); var a = n.GetComponent<FBStreetActor>(); if (!a) continue;
                    a.faceYaw = null; a.run = true; var away = (n.Pos - np).Flat().normalized; a.Go(FBNav.RandomPoint(n.Pos + away * 40, 10) ?? n.Pos + away * 35, 5f, 1.5f);
                }
            }
            var P = FBMatch.Player; bool far = !P || (P.Pos - center).Flat().magnitude > 170;
            if ((far && life > 40 && !FBStreetEvents.Visible(center + Vector3.up)) || life > 1200) End(false);
        }
        public static void End(bool now)
        {
            foreach (var n in npcs) if (n) FBStreetEvents.Despawn(n);
            npcs.Clear(); if (root) Object.Destroy(root); root = null;
        }
    }
    static class V3Ext { public static Vector3 Abs(this Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }
}
