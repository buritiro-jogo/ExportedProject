// EVENTOS DE RUA DO 171 — ELETRICISTA / CARRO COM ESCADA / CORTE DE ENERGIA (rodada 43, refeito na rodada 44).
// Pedido 28/10: "Corte de energia: implemente completamente a cena de manutenção elétrica, incluindo funcionários, equipamentos, veículos,
// postes, ferramentas e animações. Carro com escada: conclua todos os sistemas do veículo, incluindo escada, movimentação, posicionamento,
// subida, descida, utilização de ferramentas e animações dos trabalhadores."
//
// Base no 171 (BP_EletricistaEscada, pseudocódigo em tools/re/out/171/BP_EletricistaEscada.txt): eletricista no alto da escada encostada no
// poste, clipe EletricistaTrabalhandoPoste, carro de serviço Micto com rack de escada, falas "Sai de perto da escada"… e o FinalizarEvento
// (carro bate na escada / tiro: escada cai pela física, eletricista em ragdoll; 5 s depois foge ou parte para cima).
// O 171 monta a cena PRONTA. Aqui a cena tem o ciclo do trabalho (o que o usuário pediu), com DUAS variantes sorteadas:
//  • "escada"   (carro com escada): os 2 funcionários (r52: categoria eletricista do elenco) tiram a escada do rack do carro, carregam a dois até o
//               poste, erguem e encostam; um sobe (IK de mãos e pés nos degraus, passo diagonal), trabalha, desce; guardam a escada e voltam ao carro;
//  • "energia"  (corte de energia / manutenção): começa como no 171 (eletricista já no alto), o ajudante segura a escada embaixo, as luminárias
//               até 40 m do poste APAGAM (curto + faísca na cruzeta) enquanto ele mexe na rede, faíscas no alicate, e a luz volta no fim.
// Ferramenta: alicate na mão (faísca + estalo elétrico do 171 a cada 4–9 s). Sinalização: cones atrás do carro.
// Diferença documentada: as animações de escada do 171 (LadderSystem RT_LDR_O12_*) têm compressão ACL e o decodificador nativo
// (CUE4Parse-Natives) não está disponível — o subir/descer é por IK procedural (mãos/pés degrau a degrau), não pelos clipes originais.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FreeBala
{
    public static class FBEletricista
    {
        public static bool Active => root; public static string Why;   // motivo do último Start (autoteste)
        public static string State => st; public static FBCharacter Worker => el; public static FBCharacter Helper => aj; public static GameObject Ladder => ladder; public static GameObject Van => van; public static Vector3 Base => ladderBase;
        public static string Variant => variant; public static string ForceVariant; public static int LampsCut => cutLamps.Count; public static int Sparks;
        public static float Cooldown() => Random.Range(220f, 420f);
        static GameObject root, ladder, van; static Transform piv; static FBCharacter el, aj; static string st = "", variant = "escada"; static float stT, talkT, life, sparkT; static int lastTalk = -1;
        static Vector3 ladderBase, poleP, poleTop, toPole, laneDir, rackRear, vanSide; static float ladderLen, workH; static Rigidbody ladderRb; static bool fought;
        static readonly List<int> cutLamps = new();
        const string CLIP = "X171_EletricistaTrabalhandoPoste";
        static readonly string[] IDLE = { "X171_Idle_Male", "NPC_Idle01_M", "NPC_Idle02_M", "Idle" }, SMOKE = { "X171_055_Mocap101_SL_STATIC_B_Smoke_UE41", "X171_fumando_looped" };
        static bool Has(string c) => FBRegistry.I && FBRegistry.I.Clip(c);
        static string Pick(string[] l) => l.FirstOrDefault(Has);
        static void Log(string s) => FBStreetEvents.L("eletricista: " + s);

        static GameObject Part(string lib, string name)
        {
            var pf = Resources.Load<GameObject>("X171/Eventos/" + lib); if (!pf) return null;
            var src = pf.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.parent == pf.transform && t.name.Split('.')[0] == name);
            if (!src) return null; var go = Object.Instantiate(src.gameObject); go.name = "x171_" + name;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }
        static Bounds WorldBounds(GameObject g) { var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }

        // ---- escada com pivô na base: eixo Y local ao longo da escada, Z local = normal da face (aponta para longe do poste)
        static void BuildPivot(GameObject g)
        {
            var pts = new List<Vector3>(); float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) { var m = mf.sharedMesh; if (!m || !m.isReadable) continue; foreach (var v in m.vertices) { var w = mf.transform.TransformPoint(v); pts.Add(w); y0 = Mathf.Min(y0, w.y); y1 = Mathf.Max(y1, w.y); } }
            Vector3 bot, top;
            if (pts.Count == 0) { var b = WorldBounds(g); bot = new Vector3(b.center.x, b.min.y, b.center.z); top = new Vector3(b.center.x, b.max.y, b.center.z); }
            else
            {
                float h = y1 - y0; Vector3 a = Vector3.zero, c = Vector3.zero; int na = 0, nc = 0;
                foreach (var w in pts) { if (w.y > y1 - h * .06f) { c += w; nc++; } else if (w.y < y0 + h * .06f) { a += w; na++; } }
                bot = a / Mathf.Max(1, na); top = c / Mathf.Max(1, nc); bot.y = y0; top.y = y1;
            }
            var axis = (top - bot); ladderLen = axis.magnitude; axis /= Mathf.Max(1e-4f, ladderLen);
            var lean = new Vector3(axis.x, 0, axis.z); var face = lean.sqrMagnitude > 1e-4f ? -lean.normalized : Vector3.forward;
            piv = new GameObject("x171_Escada_pivo").transform; piv.SetPositionAndRotation(bot, Quaternion.LookRotation(face, axis));
            g.transform.SetParent(piv, true);
        }
        /// <summary>Escada de pé encostada (base em 'b', topo em 't'); a face olha para longe do poste.</summary>
        static void Lean(Vector3 b, Vector3 t) { var d = (t - b).normalized; var away = (b - t).Flat(); piv.SetPositionAndRotation(b, Quaternion.LookRotation(away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward, d)); }
        /// <summary>Escada deitada (rack / carregando): base em 'b', eixo na horizontal 'dir', degraus para cima.</summary>
        static void Lay(Vector3 b, Vector3 dir) { piv.SetPositionAndRotation(b, Quaternion.LookRotation(Vector3.up, dir.Flat().normalized)); }
        static Vector3 Along(float s) => piv.position + piv.up * s;

        // ---------------------------------------------------------------- montar
        public static bool Start()
        {
            // r52: só eletricistas do elenco (categoria eletricista) — nunca um civil no lugar; os do 171 saíram e os da AMBAR ENERGIA esperam o Mixamo
            int nEl = FBStreetEvents.FreeIds(FBNpcAnims.ByCls("eletricista")).Count;
            Why = root ? "já ativo" : FBPole.All.Count == 0 ? "sem postes" : !Has(CLIP) ? "sem clipe " + CLIP : !Resources.Load<GameObject>("X171/Eventos/Obra") ? "sem X171/Eventos/Obra" : nEl == 0 ? "sem eletricista livre no elenco" : null;
            if (Why != null) return false;
            var pp = FBStreetEvents.PlayerPos; float near = FBStreetEvents.ForceNear; int nDist = 0, nVis = 0, nLane = 0, nRoad = 0, nGround = 0, nFree = 0, nH = 0, nAll = 0;
            var cand = FBPole.All.Where(p => p && !p.down && !p.falling); cand = near > 0 ? cand.OrderBy(p => Mathf.Abs((p.basePos - pp).Flat().magnitude - near)) : cand.OrderBy(_ => Random.value).Take(80);
            foreach (var pole in cand)
            {
                nAll++; var b = pole.basePos; float d = (b - pp).Flat().magnitude;
                if (near > 0 ? Mathf.Abs(d - near) > 60 : (d < 40 || d > 120)) { nDist++; continue; }
                if (near <= 0 && FBStreetEvents.Visible(b + Vector3.up * 2)) { nVis++; continue; }
                var L = FBRoads.NearestLane(b, 9, out var t, out float lat, null); if (L == null) { nLane++; continue; }
                var lp = FBRoads.At(L, t, out var ld); ld.y = 0; ld.Normalize(); var toRoad = (lp - b).Flat(); if (toRoad.sqrMagnitude < .5f) { nRoad++; continue; } toRoad.Normalize();
                var lb = b + toRoad * 1.2f; if (!FBStreetEvents.Ground(ref lb, b.y, .6f)) { nGround++; continue; } if (!FBStreetEvents.Free(lb, .35f)) { nFree++; continue; }
                if (pole.height < 5.5f) { nH++; continue; }
                var vp0 = lp - ld * 4.6f;                                                                   // vaga do carro de serviço: sem carro estacionado/trânsito em cima
                if (FBVehicles.list.Any(c => c && (c.transform.position - vp0).Flat().magnitude < 5.5f) || FBTransito.List.Any(c => c.car && (c.pos - vp0).Flat().magnitude < 7)) { nFree++; continue; }
                variant = ForceVariant ?? (Random.value < .5f ? "escada" : "energia"); ForceVariant = null;
                if (nEl < 2) variant = "energia";                                                         // carregar a escada pede dois; sozinho ele já está no alto
                Build(pole, lb, lp, ld, toRoad); Why = $"montou no poste a {d:0} m ({variant})"; return true;
            }
            Why = $"{nAll} postes (de {FBPole.All.Count}): distância {nDist} · à vista {nVis} · sem faixa a 9 m {nLane} · em cima da faixa {nRoad} · sem chão {nGround} · ocupado {nFree} · baixo {nH}"; return false;
        }
        static void Build(FBPole pole, Vector3 lb, Vector3 lanePt, Vector3 ld, Vector3 toRoad)
        {
            root = new GameObject("FB_Evento_Eletricista"); poleP = pole.basePos; ladderBase = lb; toPole = -toRoad; laneDir = ld; cutLamps.Clear(); Sparks = 0;
            ladder = Part("Obra", "Escada"); if (!ladder) { Object.Destroy(root); return; }
            BuildPivot(ladder); piv.SetParent(root.transform, true);
            var lc = ladder.AddComponent<BoxCollider>(); var lbnd = WorldBounds(ladder); lc.center = ladder.transform.InverseTransformPoint(lbnd.center); lc.size = ladder.transform.InverseTransformVector(lbnd.size).Abs();
            // ponto de apoio no poste: a escada encosta a ~85 % do comprimento (ângulo de uns 75°)
            float hTop = Mathf.Min(ladderLen * .965f, pole.height - .6f); float run = Mathf.Sqrt(Mathf.Max(.25f, ladderLen * ladderLen - hTop * hTop));
            ladderBase = poleP + toRoad * Mathf.Max(.9f, run); var g0 = ladderBase; if (FBStreetEvents.Ground(ref g0, poleP.y, .8f)) ladderBase = g0;
            poleTop = poleP + Vector3.up * hTop + toRoad * .12f; workH = Mathf.Clamp(hTop - 1.55f, 2.2f, 5.6f);
            // CARRO DE SERVIÇO do 171 (lataria + vidros + rack de escada), junto ao meio-fio ~4 m antes do poste
            van = new GameObject("x171_CarroServico"); van.transform.SetParent(root.transform, false);
            foreach (var n in new[] { "MictoLow_Merged", "VidrosLow_Micto", "EstruturaEscada" }) { var g = Part("CarroServico", n); if (g) g.transform.SetParent(van.transform, true); }
            Bounds rackB = default;
            if (van.transform.childCount > 0)
            {
                var vb = WorldBounds(van); bool alongX = vb.size.x > vb.size.z; float yaw = Mathf.Atan2(ld.x, ld.z) * Mathf.Rad2Deg + (alongX ? -90 : 0);
                van.transform.rotation = Quaternion.Euler(0, yaw, 0); vb = WorldBounds(van);
                var vp = lanePt - ld * 4.6f + toRoad * .2f; var gp = vp; FBStreetEvents.Ground(ref gp, lanePt.y, 1.2f);
                van.transform.position += new Vector3(vp.x - vb.center.x, gp.y - vb.min.y, vp.z - vb.center.z);
                var vb2 = WorldBounds(van); var bc = van.AddComponent<BoxCollider>(); bc.center = van.transform.InverseTransformPoint(vb2.center); bc.size = van.transform.InverseTransformVector(vb2.size).Abs();
                foreach (var t in van.GetComponentsInChildren<Transform>()) t.gameObject.layer = FBWorld.LVehicle;
                var rack = van.transform.Cast<Transform>().FirstOrDefault(t => t.name.Contains("EstruturaEscada")); rackB = rack ? WorldBounds(rack.gameObject) : vb2;
                rackRear = new Vector3(vb2.center.x, rackB.max.y + .04f, vb2.center.z) - ld * (ladderLen * .5f); vanSide = vb2.center - toRoad * 0 + toPole * (vb2.extents.magnitude * .0f);
                vanSide = new Vector3(vb2.center.x, gp.y, vb2.center.z) + (-toRoad) * (Mathf.Min(vb2.size.x, vb2.size.z) * .5f + .7f);   // calçada ao lado do carro
                if (!FBStreetEvents.Ground(ref vanSide, gp.y, 1)) vanSide = ladderBase - ld * 2;
            }
            // cones atrás do carro (sinalização)
            for (int k = 0; k < 3; k++) { var c = Part("Obra", k == 1 ? "SM_ConstructionPylons_01e" : "SM_ConstructionPylons_01d"); if (!c) continue; c.transform.SetParent(root.transform, true); var cp = lanePt - ld * (8.5f + k * 1.4f) + toRoad * (k * .45f); FBStreetEvents.Ground(ref cp, lanePt.y, 1.2f); var cb = WorldBounds(c); c.transform.position += new Vector3(cp.x - cb.center.x, cp.y - cb.min.y, cp.z - cb.center.z); FBFisica.Make(c, "plastico", 2.2f); }
            // FUNCIONÁRIOS (r52: categoria eletricista do elenco): quem sobe = mão de obra; o ajudante (fiscal) segura a escada. Sem segundo, sem ajudante
            var ids = FBStreetEvents.FreeIds(FBNpcAnims.ByCls("eletricista")).OrderBy(i => FBElenco.FuncOf(i) == "mao_de_obra" ? 0 : 1).ToList();
            string id1 = ids.Count > 0 ? ids[0] : null, id2 = ids.Count > 1 ? ids[1] : null;
            el = FBStreetEvents.Spawn(id1, vanSide, Yaw(ld), "eletricista"); aj = FBStreetEvents.Spawn(id2, vanSide + ld * 1.1f, Yaw(-ld), "ajudante");
            foreach (var c in new[] { el, aj }) if (c) { c.hp = c.maxHp = 60; c.gameObject.GetOrAdd<FBStreetActor>().Init(c); c.gameObject.GetOrAdd<FBLadderIK>().Clear(); foreach (var col in c.GetComponentsInChildren<Collider>()) if (!(col is CharacterController)) Physics.IgnoreCollision(col, lc); }
            FBStreetEvents.NoCollide(new[] { el, aj });
            FBDamageEvents.OnDamage -= OnDamage; FBDamageEvents.OnDamage += OnDamage;
            life = 0; fought = false; lastTalk = -1; talkT = 2; sparkT = Random.Range(3f, 6f);
            if (variant == "energia") { Lean(ladderBase, poleTop); PutOnTop(); HelperHold(); CutPower(true); st = "trabalhando"; stT = Random.Range(60f, 110f); }
            else { Lay(rackRear, ld); Idle(el); Idle(aj); st = "preparando"; stT = FBStreetEvents.ForceNear > 0 ? 2 : Random.Range(4f, 8f); }
            Log($"{variant} · poste a {(poleP - FBStreetEvents.PlayerPos).Flat().magnitude:0} m · escada {ladderLen:0.0} · trabalho a {workH:0.0} · {(el ? el.skinId : "-")}+{(aj ? aj.skinId : "-")}");
        }
        static float Yaw(Vector3 f) => Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        static void Idle(FBCharacter c) { if (!c) return; var cl = Pick(IDLE); if (cl != null) { c.PlayDance(cl); if (c.dance != null) c.dance.len = 1e9f; } }
        static FBStreetActor A(FBCharacter c) => c ? c.GetComponent<FBStreetActor>() : null;
        static FBLadderIK IK(FBCharacter c) => c ? c.GetComponent<FBLadderIK>() : null;
        static void Hold(FBCharacter c, bool on) { var cc = c ? c.GetComponent<CharacterController>() : null; if (cc) cc.enabled = !on; var fk = c ? c.GetComponent<FBFootIK>() : null; if (fk) fk.enabled = !on; var a = A(c); if (a) a.enabled = !on; }

        // no alto (variante energia, ou fim da subida): pés nos degraus, mãos pelo clipe de trabalho
        static void PutOnTop() { if (!el) return; Hold(el, true); el.PlayDance(CLIP); var ik = IK(el); ik.Climb(piv, ladderLen, workH, toPole, true); }
        static void HelperHold()
        {   // ajudante segurando a escada embaixo, de frente para o poste, mãos nas laterais a ~1,1 m
            if (!aj) return; var at = ladderBase - toPole * .55f; FBStreetEvents.Ground(ref at, ladderBase.y, .6f); FBMatch.Teleport(aj, at, Yaw(toPole)); Hold(aj, true); Idle(aj);
            IK(aj).HoldRails(piv, 1.15f, toPole);
        }

        // ---------------------------------------------------------------- corte de energia (luminárias até 40 m)
        static void CutPower(bool off)
        {
            if (off)
            {
                cutLamps.Clear(); var L = FBStreetLights.Lamps;
                for (int i = 0; i < L.Count; i++) if ((L[i].p - poleP).Flat().magnitude < 40) { FBStreetLights.Cut(i, true); cutLamps.Add(i); }
                FBFx.Sparks(poleTop + Vector3.up * .5f, Vector3.down); FBFisica.Sound("eletrico", poleTop, .9f, 14);
                Log($"corte de energia: {cutLamps.Count} luminárias apagadas");
            }
            else { foreach (var i in cutLamps) FBStreetLights.Cut(i, false); if (cutLamps.Count > 0) Log($"energia de volta ({cutLamps.Count} luminárias)"); cutLamps.Clear(); }
        }

        static void OnDamage(FBDamage d) { if (el && d.victim == el && (st == "trabalhando" || st == "subindo" || st == "descendo")) Fall((el.Pos - (d.source ? d.source.Pos : el.Pos - el.Fwd)).Flat().normalized, "tiro"); }
        /// <summary>FinalizarEvento do 171: escada com física, eletricista em ragdoll; 5 s depois foge (50 %) ou parte para cima (50 %).</summary>
        public static void Fall(Vector3 push, string why)
        {
            if (st != "trabalhando" && st != "subindo" && st != "descendo") return; st = "caiu"; stT = 5; Log("caiu (" + why + ")"); CutPower(false);
            if (ladder) { piv.SetParent(root.transform, true); ladderRb = piv.gameObject.AddComponent<Rigidbody>(); ladderRb.mass = 18; ladderRb.AddForce((push.sqrMagnitude > .01f ? push : -toPole) * 120 + Vector3.up * 20, ForceMode.Impulse); FBFisica.Watch(piv.gameObject, "metal", 18); }
            if (aj) { IK(aj).Clear(); Hold(aj, false); var a = A(aj); if (a) { a.run = true; a.Go(aj.Pos - toPole * 6, 4f, .5f); } }
            if (el) { IK(el).Clear(); el.StopDance(true); var rd = FBRagdoll.Of(el); rd.Go((push.sqrMagnitude > .01f ? push : -toPole) * 2.5f + Vector3.down, el.Pos + Vector3.up * 1.2f); Say(el, 1); }
        }
        static void Say(FBCharacter c, float vol) { if (!c) return; int k; do k = Random.Range(1, 7); while (k == lastTalk); lastTalk = k; FBAudio.PlayAt("x171_eletricista_" + k, c.Pos + Vector3.up * 1.6f, vol, 16); }

        // ---------------------------------------------------------------- andamento
        static float carryS; static Vector3 carryFrom, carryTo;
        public static void Tick(float dt)
        {
            if (!root) return; life += dt; stT -= dt; var P = FBMatch.Player;
            switch (st)
            {
                case "preparando":   // conversam ao lado do carro; depois vão ao rack
                    if (stT <= 0) { st = "tirando"; stT = 2.2f; foreach (var c in new[] { el, aj }) if (c) { c.StopDance(true); A(c).Go(rackRear + laneDir * (c == el ? ladderLen * .8f : ladderLen * .2f) - (-toPole) * 0 + (vanSide - rackRear).Flat().normalized * .9f, 1.2f, .25f); } }
                    break;
                case "tirando":      // a escada sai do rack para a altura das mãos ao lado do carro
                    {
                        float k = Mathf.SmoothStep(0, 1, 1 - stT / 2.2f); var side = (vanSide - rackRear).Flat().normalized;
                        var hand = new Vector3(rackRear.x, ladderBase.y + 1.0f * (el ? el.ScaleK : 1), rackRear.z) + side * 1.0f;
                        Lay(Vector3.Lerp(rackRear, hand, k), laneDir); CarryHands();
                        if (stT <= 0) { st = "carregando"; carryFrom = piv.position; carryTo = ladderBase - (ladderBase - poleP).Flat().normalized * 0 - laneDir * 0; carryS = 0; }
                    }
                    break;
                case "carregando":   // os dois levam a escada até a base, andando no passo da escada
                    {
                        var dir = (carryTo - carryFrom).Flat(); float L = Mathf.Max(.1f, dir.magnitude); dir /= L; carryS = Mathf.Min(L, carryS + 1.05f * dt);
                        var b = Vector3.Lerp(carryFrom, new Vector3(carryTo.x, carryFrom.y, carryTo.z), carryS / L); var gb = b; FBStreetEvents.Ground(ref gb, carryTo.y, 2); b.y = gb.y + 1.0f * (el ? el.ScaleK : 1);
                        Lay(b - dir * ladderLen * .05f, dir); CarryHands(dir);
                        if (carryS >= L - .02f) { st = "erguendo"; stT = 2.4f; }
                    }
                    break;
                case "erguendo":     // gira em volta da base até encostar no poste; o da frente "anda" a escada para cima
                    {
                        float k = Mathf.SmoothStep(0, 1, 1 - stT / 2.4f); var flat = Quaternion.LookRotation(Vector3.up, (poleTop - ladderBase).Flat().normalized);
                        var up = Quaternion.LookRotation((ladderBase - poleTop).Flat().normalized, (poleTop - ladderBase).normalized);
                        piv.SetPositionAndRotation(Vector3.Lerp(piv.position, ladderBase, Mathf.Min(1, dt * 6)), Quaternion.Slerp(flat, up, k));
                        if (el) { IK(el).Grip(Along(Mathf.Lerp(ladderLen * .55f, 1.6f, k)), piv); A(el).Go(Along(Mathf.Lerp(ladderLen * .5f, .4f, k)).Flat3(ladderBase.y) - toPole * .45f, 1.2f, .1f); }
                        if (aj) { IK(aj).Grip(Along(.35f), piv); }
                        if (stT <= 0) { Lean(ladderBase, poleTop); FBFisica.Sound("metal", poleTop, .7f, 10); st = "subindo"; IK(aj)?.Clear(); HelperHold(); StartClimb(); }
                    }
                    break;
                case "subindo":
                    if (el && IK(el).ClimbDone) { PutOnTop(); st = "trabalhando"; stT = Random.Range(35f, 60f); if (variant == "energia" || Random.value < .5f) CutPower(true); }
                    break;
                case "trabalhando":
                    {
                        if (el && el.dance == null) el.PlayDance(CLIP);
                        if ((sparkT -= dt) <= 0 && el && el.RightHand) { sparkT = Random.Range(4f, 9f); Sparks++; FBFx.Sparks(el.RightHand.position + toPole * .1f, -toPole); FBFisica.Sound("eletrico", el.RightHand.position, .7f, 9); }
                        float dpl = P ? (P.Pos - ladderBase).Flat().magnitude : 99;
                        if (dpl < 7 && (talkT -= dt) <= 0 && el) { Say(el, .95f); talkT = Random.Range(6f, 10f); }
                        if (dpl >= 7) talkT = Mathf.Min(talkT, 1.2f);
                        if (CarHitsLadder(out var push)) { Fall(push, "batida"); break; }
                        if (stT <= 0) { CutPower(false); st = "descendo"; el.StopDance(true); IK(el).Climb(piv, ladderLen, workH, toPole, false, -1); el.PlayDance(CLIP); }
                    }
                    break;
                case "descendo":
                    if (CarHitsLadder(out var push2)) { Fall(push2, "batida"); break; }
                    if (el && IK(el).ClimbDone)
                    {   // no chão: solta, guarda a escada (variante escada) ou fica conversando (energia)
                        IK(el).Clear(); el.StopDance(true); var g = ladderBase - toPole * .6f; FBStreetEvents.Ground(ref g, ladderBase.y, 1); FBMatch.Teleport(el, g, Yaw(-toPole)); Hold(el, false);
                        IK(aj)?.Clear(); Hold(aj, false);
                        if (variant == "escada") { st = "baixando"; stT = 2.2f; } else { st = "pausa"; stT = Random.Range(25f, 45f); Chat(); }
                    }
                    break;
                case "baixando":
                    {
                        float k = Mathf.SmoothStep(0, 1, 1 - stT / 2.2f); var flat = Quaternion.LookRotation(Vector3.up, (vanSide - ladderBase).Flat().normalized); var up = Quaternion.LookRotation((ladderBase - poleTop).Flat().normalized, (poleTop - ladderBase).normalized);
                        piv.SetPositionAndRotation(ladderBase + Vector3.up * k * 1.0f, Quaternion.Slerp(up, flat, k)); CarryHands((vanSide - ladderBase).Flat().normalized);
                        if (stT <= 0) { st = "guardando"; carryFrom = piv.position; carryTo = rackRear; carryS = 0; }
                    }
                    break;
                case "guardando":
                    {
                        var dir = (carryTo - carryFrom).Flat(); float L = Mathf.Max(.1f, dir.magnitude); dir /= L; carryS = Mathf.Min(L, carryS + 1.05f * dt);
                        var b = Vector3.Lerp(carryFrom, new Vector3(carryTo.x, carryFrom.y, carryTo.z), carryS / L);
                        Lay(b, carryS < L - .5f ? dir : laneDir); CarryHands(dir);
                        if (carryS >= L - .02f) { Lay(rackRear, laneDir); FBFisica.Sound("metal", rackRear, .7f, 10); foreach (var c in new[] { el, aj }) IK(c)?.Clear(); st = "pausa"; stT = Random.Range(20f, 40f); Chat(); }
                    }
                    break;
                case "pausa":   // fumam/conversam ao lado do carro; depois recomeça (outra vez no mesmo poste)
                    if (stT <= 0) { if (variant == "escada") { st = "preparando"; stT = 1; } else { st = "subindo"; foreach (var c in new[] { el, aj }) c?.StopDance(true); HelperHold(); StartClimb(); } }
                    break;
                case "caiu":
                    if (stT <= 0 && el)
                    {
                        var rd = el.GetComponent<FBRagdoll>(); var hips = el.Hips ? el.Hips.position : el.Pos; var g = hips; FBStreetEvents.Ground(ref g, hips.y, 3);
                        el.transform.position = new Vector3(hips.x, g.y, hips.z); if (rd) rd.Clear(); Hold(el, false);
                        var a = A(el); a.life = false;
                        if (Random.value < .5f || !P) { st = "fugindo"; a.run = true; var away = (el.Pos - (P ? P.Pos : ladderBase)).Flat().normalized; a.Go(FBNav.RandomPoint(el.Pos + away * 45, 10) ?? el.Pos + away * 40, 5f, 1.5f); Log("fugiu correndo"); }
                        else { st = "brigando"; a.run = true; Log("partiu para cima do jogador"); }
                        stT = 25;
                    }
                    break;
                case "brigando":
                    if (el && P)
                    {
                        var a = A(el); float d = (P.Pos - el.Pos).Flat().magnitude;
                        if (a && d > 1.3f) { a.Go(P.Pos, 4.6f, .9f); if ((talkT -= dt) <= 0) { Say(el, 1); talkT = Random.Range(3f, 5f); } }
                        else if (!fought)
                        {
                            fought = true; a?.Stop(); var push = FBRegistry.I.clips.FirstOrDefault(c => c && c.name.StartsWith("X171_") && c.name.ToLower().Contains("esbarr"));
                            if (push) el.PlayDance(push.name); P.velocity += (P.Pos - el.Pos).Flat().normalized * 4.5f + Vector3.up * 1.5f;
                            FBCombat.DamageActor(P, 5, false, el, "soco", default); stT = 3;
                        }
                        if (fought && stT <= 0) { st = "fugindo"; if (a) { a.run = false; a.Go(FBNav.RandomPoint(el.Pos + el.Fwd * -30, 10) ?? el.Pos - el.Fwd * 25, 1.4f, 1.5f); } }
                    }
                    break;
            }
            bool far = !P || (P.Pos - ladderBase).Flat().magnitude > 160;
            if ((far && life > 30 && !FBStreetEvents.Visible(ladderBase + Vector3.up * 2)) || life > 900) End(false);
        }
        static void StartClimb() { if (!el) return; Hold(el, true); el.StopDance(true); el.PlayDance(CLIP); IK(el).Climb(piv, ladderLen, workH, toPole, false, 1); st = "subindo"; }
        static void Chat() { var sm = Pick(SMOKE); foreach (var c in new[] { el, aj }) if (c) { var g = vanSide + laneDir * (c == el ? -.5f : .5f); FBStreetEvents.Ground(ref g, vanSide.y, 1); A(c)?.Go(g, 1.1f, .2f); var cc = c; A(c).onArrive = () => { cc.Yaw = Yaw((c == el ? laneDir : -laneDir)); if (cc == el && sm != null) cc.PlayDance(sm); else Idle(cc); }; } }
        // mãos dos dois na lateral da escada deitada (frente e trás); andam junto
        static void CarryHands(Vector3? walk = null)
        {
            var dir = piv.up; var side = Vector3.Cross(Vector3.up, dir).normalized;
            if (el) { var p = Along(ladderLen * .78f); IK(el).Grip(p, piv); var a = A(el); a.Go((p - side * .38f).Flat3(ladderBase.y), 1.4f, .08f); a.faceYaw = Yaw(walk ?? dir); }
            if (aj) { var p = Along(ladderLen * .18f); IK(aj).Grip(p, piv); var a = A(aj); a.Go((p - side * .38f).Flat3(ladderBase.y), 1.4f, .08f); a.faceYaw = Yaw(walk ?? dir); }
        }
        static bool CarHitsLadder(out Vector3 push)
        {
            push = Vector3.zero;
            foreach (var car in FBVehicles.list)
            {
                if (!car || Mathf.Abs(car.v) < 2) continue; var cb = car.GetComponentInChildren<Collider>()?.bounds;
                var hit = cb.HasValue ? cb.Value.SqrDistance(ladderBase + Vector3.up * .8f) < .8f : (car.transform.position - ladderBase).Flat().magnitude < 2.2f;
                if (hit) { push = car.transform.forward * Mathf.Sign(car.v); return true; }
            }
            return false;
        }
        public static void End(bool now)
        {
            FBDamageEvents.OnDamage -= OnDamage; CutPower(false);
            foreach (var c in new[] { el, aj }) if (c) FBStreetEvents.Despawn(c); el = aj = null;
            if (root) Object.Destroy(root); root = ladder = van = null; piv = null; st = ""; ladderRb = null;
        }
    }

    // IK de escada (rodada 44): sobe/desce degrau a degrau (mãos e pés alternando em diagonal), segura a escada (ajudante / carregar).
    // Roda depois do Animator; o quadril vai para o lugar certo e mãos/pés para os degraus. Sem clipe do 171 (ACL) — ver FBEletricista.
    [DefaultExecutionOrder(1001)]
    public class FBLadderIK : MonoBehaviour
    {
        FBCharacter c; Transform piv; float len, top, s, dir, rung = .36f; Vector3 toPole; bool climbing, onTop, holdRails; Vector3? gripP; float holdH, w;
        public bool ClimbDone { get; private set; }
        public void Clear() { climbing = onTop = holdRails = false; gripP = null; piv = null; ClimbDone = false; w = 0; }
        public void Climb(Transform p, float length, float workH, Vector3 pole, bool atTop, int direction = 1)
        {
            c = GetComponent<FBCharacter>(); piv = p; len = length; top = workH; toPole = pole.Flat().normalized; climbing = !atTop; onTop = atTop; dir = direction; holdRails = false; gripP = null; ClimbDone = false;
            s = atTop ? top : direction > 0 ? 0 : top; rung = .3f * (c ? c.ScaleK : 1.2f) * 1.0f;
        }
        public void HoldRails(Transform p, float h, Vector3 pole) { c = GetComponent<FBCharacter>(); piv = p; holdH = h; toPole = pole.Flat().normalized; holdRails = true; climbing = onTop = false; gripP = null; }
        public void Grip(Vector3 at, Transform p) { c = GetComponent<FBCharacter>(); piv = p; gripP = at; climbing = onTop = holdRails = false; }
        // [parceiro] v1 (thread de física): tempo do LateUpdate medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_LateUpdate = FBTempo.Id("FBLadderIK.LateUpdate");
        void LateUpdate()
        {
            if (!FBTempo.On) { LateUpdate__fbt(); return; }
            long t0 = FBTempo.T0(); LateUpdate__fbt(); FBTempo.Soma(__fbt_LateUpdate, t0);
        }
        void LateUpdate__fbt()
        {
            if (!c) c = GetComponent<FBCharacter>(); if (!c || !c.an || !c.an.isHuman || !piv) return; var an = c.an;
            w = Mathf.MoveTowards(w, 1, Time.deltaTime / .25f);
            Transform B(HumanBodyBones b) => an.GetBoneTransform(b);
            var up = piv.up; var face = piv.forward;                                                     // face: para longe do poste (lado de quem sobe)
            var side = Vector3.Cross(up, face).normalized; float k = c.ScaleK;
            if (climbing || onTop)
            {
                if (climbing) { s += dir * .55f * k * Time.deltaTime; if (dir > 0 && s >= top) { s = top; climbing = false; ClimbDone = true; } else if (dir < 0 && s <= 0) { s = 0; climbing = false; ClimbDone = true; } }
                float u = s / rung;                                                                      // em degraus
                // corpo: quadril ~0,95 m acima dos pés, 0,32 m para fora da escada, de frente para ela
                var hips = B(HumanBodyBones.Hips); if (!hips) return;
                c.transform.rotation = Quaternion.LookRotation((-face).Flat().normalized, Vector3.up);
                var hp = piv.position + up * (s + .95f * k) + face * (.30f * k); c.transform.position += hp - hips.position;
                Vector3 Rung(float n, float sd, float out_) => piv.position + up * (n * rung) + side * sd * .19f * k + face * out_ * k;
                (float n, float lift) Step(float uu, int off)
                {   // passo diagonal: cada membro fica num degrau par/ímpar e sobe 2 degraus na metade do ciclo
                    float p = (uu + off) / 2f; float n0 = Mathf.Floor(p); float f = p - n0; float st = f < .5f ? 0 : Mathf.SmoothStep(0, 1, (f - .5f) * 2);
                    return (2 * n0 - off + 2 * st, Mathf.Sin(st * Mathf.PI));
                }
                var legs = new[] { (B(HumanBodyBones.LeftUpperLeg), B(HumanBodyBones.LeftLowerLeg), B(HumanBodyBones.LeftFoot), -1f, 0), (B(HumanBodyBones.RightUpperLeg), B(HumanBodyBones.RightLowerLeg), B(HumanBodyBones.RightFoot), 1f, 1) };
                foreach (var (ul, ll, ft, sd, off) in legs)
                {
                    if (!ft) continue; var (n, lift) = onTop ? (Mathf.Round(u) - off, 0f) : Step(u, off);
                    var tg = Rung(n, sd, .05f + lift * .12f); var fr = ft.rotation; FBIK.TwoBonePole(ul, ll, ft, tg, ll.position + face * .5f * k + up * .3f, w); ft.rotation = fr;
                }
                if (!onTop)
                {
                    var arms = new[] { (B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), -1.25f, 1), (B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), 1.25f, 0) };
                    foreach (var (ua, la, hd, sd, off) in arms)
                    {
                        if (!hd) continue; var (n, lift) = Step(u, off); var tg = Rung(n + 5.2f, sd, .02f + lift * .1f);
                        FBIK.TwoBonePole(ua, la, hd, tg, la.position - up * .4f * k + face * .2f * k + side * sd * .3f * k, w);
                    }
                }
                return;
            }
            if (holdRails || gripP != null)
            {   // mãos nas laterais (ajudante: altura holdH na escada em pé; carregar/erguer: no ponto pedido)
                var center = gripP ?? piv.position + up * holdH * c.ScaleK; var sdir = gripP != null ? Vector3.Cross(Vector3.up, up).normalized : side;
                if (sdir.sqrMagnitude < 1e-4f) sdir = side;
                var arms = new[] { (B(HumanBodyBones.LeftUpperArm), B(HumanBodyBones.LeftLowerArm), B(HumanBodyBones.LeftHand), -1f), (B(HumanBodyBones.RightUpperArm), B(HumanBodyBones.RightLowerArm), B(HumanBodyBones.RightHand), 1f) };
                var right = c.transform.right;
                foreach (var (ua, la, hd, sd) in arms)
                {
                    if (!hd) continue; var tg = center + (gripP != null ? right * sd * .2f * k : sdir * sd * .2f * k);
                    if ((tg - ua.position).magnitude > .75f * k) continue;                                // longe demais: não estica o braço
                    FBIK.TwoBonePole(ua, la, hd, tg, la.position - Vector3.up * .3f * k + right * sd * .25f * k, w);
                }
            }
        }
    }
    static class V3Flat { public static Vector3 Flat3(this Vector3 v, float y) => new(v.x, y, v.z); }
}
