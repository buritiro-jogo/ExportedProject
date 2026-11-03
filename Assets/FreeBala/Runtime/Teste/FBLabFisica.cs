// Teste automático feito para medir os bugs relatados (pode apagar depois; fica só no Editor).
// Parte "veiculos": para cada veículo, entra, anda, para, sai e mede quanto o veículo e o jogador se mexem.
// Parte "armas": equipa cada arma em terceira pessoa e mede a posição das mãos e da arma, com foto.
// Parte "grafico": carrega mapa/hora/clima da lista ("vila_jardim:noite:chuva,...") e fotografa pela câmera do jogo.
// Ligado pelo FBLabRunner com: "fisicalab veiculos SaveiroQuadrada,mx_xj6" ou "fisicalab armas" ou "fisicalab tudo".
// Fica fora da pasta Editor porque o Unity não deixa pôr componente de script de editor; o #if tira do jogo final.
// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Scripts/FBTeste/FBLabFisica.cs (lá no Assembly-CSharp, fora de namespace);
// aqui fica no FreeBala.Runtime, dentro do namespace FreeBala.
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FreeBala
{
    public class FBLabFisica : MonoBehaviour
    {
        public string parte = "tudo";
        public string lista = "SaveiroQuadrada,Fusca,mx_xj6,mx_pop110,mx_cg150,pop100,xj6";
        string dir;
        Camera fotoCam;
        FBInputState? entrada;
        bool trocarVista;
        bool apertarE;

        static void Log(string s) => Debug.Log("[FreeBala][fisicalab] " + s);

        IEnumerator Wait(float s)
        {
            for (float w = 0f; w < s; w += Time.deltaTime) yield return null;
        }

        IEnumerator Start()
        {
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../_diag/fisicalab"));
            Directory.CreateDirectory(dir);
            yield return Wait(3f);
            GameData.Load();
            FBSession.Load();
            var keep = (FBSession.map, FBSession.mode, FBSession.view);
            if (parte == "grafico")
            {
                yield return Grafico();
                (FBSession.map, FBSession.mode, FBSession.view) = keep;
                FBSession.Save();
                Log("fim");
                yield break;
            }
            FBSession.map = "vila_jardim";
            FBSession.mode = "classico";
            FBSession.online = false;
            FBSession.view = "tps";
            FBSession.Save();
            var op = SceneManager.LoadSceneAsync("Map_vila_jardim");
            while (op != null && !op.isDone) yield return null;
            for (float w = 0f; w < 30f && !(FBPlayer.I && FBPlayer.I.wu && FBMatch.Player); w += Time.deltaTime) yield return null;
            yield return Wait(4f);
            var me = FBMatch.Player;
            if (!me) { Log("sem jogador"); yield break; }
            FBMatch.God = true;
            foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
            FBPlayer.TestInput = inp =>
            {
                var e = entrada ?? default;
                e.toggleView = trocarVista;
                trocarVista = false;
                // E de verdade: passa pelo FBPlayer.Update igual ao jogador (um quadro só)
                e.interact = apertarE;
                apertarE = false;
                return e;
            };
            fotoCam = new GameObject("FB_FotoCam").AddComponent<Camera>();
            fotoCam.enabled = false;
            fotoCam.nearClipPlane = 0.05f;
            fotoCam.fieldOfView = 50f;
            if (Camera.main) fotoCam.cullingMask = Camera.main.cullingMask;

            // pista plana bem acima do mapa, para medir sem interferência do cenário
            Vector3 p0 = me.Pos + Vector3.up * 300f;
            var pad = new GameObject("FB_PistaTeste").AddComponent<BoxCollider>();
            pad.gameObject.layer = 8;
            pad.size = new Vector3(3000f, 2f, 3000f);
            pad.transform.position = p0 - Vector3.up;
            var vis = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(vis.GetComponent<Collider>());
            vis.transform.position = p0;
            vis.transform.localScale = new Vector3(300f, 1f, 300f);
            Physics.SyncTransforms();

            if (parte == "veiculos" || parte == "tudo")
            {
                int k = 0;
                foreach (var id in lista.Split(','))
                {
                    yield return Veiculo(me, id.Trim(), p0 + new Vector3(18f * k++, 0f, 0f));
                }
            }
            if (parte == "armas" || parte == "tudo")
            {
                yield return Armas(me, p0 + new Vector3(-30f, 0f, 0f));
            }
            if (parte == "saque")
            {
                yield return Saque(me, p0 + new Vector3(-30f, 0f, 0f));
            }
            if (parte == "andar")
            {
                yield return Andar(me, p0 + new Vector3(-60f, 0f, 0f));
            }
            FBPlayer.TestInput = null;
            (FBSession.map, FBSession.mode, FBSession.view) = keep;
            FBSession.Save();
            Log("fim");
        }

        // ---------- veículos ----------
        struct Amostra { public Vector3 pos; public Vector3 eul; }

        static Amostra Pose(FBCar c) => new Amostra { pos = c.transform.position, eul = c.transform.eulerAngles };

        static float Ang(Vector3 a, Vector3 b) => Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(a.x, b.x)), Mathf.Abs(Mathf.DeltaAngle(a.z, b.z)));

        static float RbMax(FBCar c) => c.GetComponentsInChildren<Rigidbody>().Where(r => r && !r.isKinematic).Select(r => r.linearVelocity.magnitude).DefaultIfEmpty(0f).Max();

        static float Penetracao(FBCharacter me, FBCar c)
        {
            var cc = me.GetComponent<CharacterController>();
            if (!cc || !cc.enabled) return 0f;
            float m = 0f;
            foreach (var col in c.cols)
            {
                if (!col || !col.enabled || col.isTrigger) continue;
                if (Physics.ComputePenetration(cc, cc.transform.position, cc.transform.rotation, col, col.transform.position, col.transform.rotation, out _, out var d))
                    m = Mathf.Max(m, d);
            }
            return m;
        }

        IEnumerator Veiculo(FBCharacter me, string id, Vector3 p)
        {
            var car = FBVehicles.Spawn(id, p + Vector3.up * 0.3f, 0f);
            yield return Wait(2f);
            if (!car) { Log($"{id}: não nasceu"); yield break; }
            var cc = me.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            me.transform.position = car.transform.position - car.transform.right * (car.kind == "car" ? 2.2f : 1.3f);
            me.Yaw = car.transform.eulerAngles.y + 90f;
            if (cc) cc.enabled = true;
            yield return Wait(0.6f);
            string tipo = car.mx ? "moto MX" : car.kind;
            var a0 = Pose(car);
            apertarE = true;
            for (float w = 0f; w < 1f && !FBVehicles.CarOf(me); w += Time.deltaTime) yield return null;
            if (FBVehicles.CarOf(me) != car) { Log($"{id} ({tipo}): apertar E não entrou neste veículo (entrou em {(FBVehicles.CarOf(me) ? FBVehicles.CarOf(me).name : "nenhum")})"); if (FBVehicles.CarOf(me)) FBDrive.Exit(me, force: true); yield break; }
            var dp = me.GetComponent<FBDriverPose>();
            float dMax = 0f, aMax = 0f, vMax = 0f, t = 0f;
            var seq = new StringBuilder();
            string ultimo = null;
            bool ccLigado = false;
            for (; t < 6f; t += Time.deltaTime)
            {
                string agora = $"{me.sys ?? "sem clipe"}/{(dp ? dp.trans ?? "-" : "sem pose")}";
                if (agora != ultimo) { seq.Append($" {t:0.00}s {agora};"); ultimo = agora; }
                ccLigado |= cc && cc.enabled;
                var a = Pose(car);
                dMax = Mathf.Max(dMax, (a.pos - a0.pos).magnitude);
                aMax = Mathf.Max(aMax, Ang(a.eul, a0.eul));
                vMax = Mathf.Max(vMax, RbMax(car));
                if (t > 0.3f && (!dp || dp.trans == null)) break;
                yield return null;
            }
            Foto(car, $"{id}_1_sentado", me);
            Log($"{id} ({tipo}) ENTRAR (tecla E): levou {t:0.0} s · veículo andou {dMax:0.00} u · girou {aMax:0.0}° · física máx {vMax:0.00} u/s · colisor do jogador ligado no banco {ccLigado} · fase {dp?.trans ?? "-"} · clipe {me.sys} · sequência:{seq}");

            // anda e para
            entrada = new FBInputState { move = new Vector2(0f, 0.6f) };
            // mede se o quadril do piloto treme para frente e para trás em relação ao veículo (entre 6 e 45 km/h)
            var quadril = me.an ? me.an.GetBoneTransform(HumanBodyBones.Hips) : null;
            var zs = new List<float>();
            int quadros = 0;
            var fimQuadro = new WaitForEndOfFrame();
            for (float w = 0f; w < 2.5f; w += Time.deltaTime)
            {
                // no fim do quadro o veículo e o corpo já estão na mesma posição que foi desenhada
                yield return fimQuadro;
                float vv = Mathf.Abs(car.v);
                if (quadril && vv > 1.7f && vv < 12.5f) { zs.Add(Vector3.Dot(quadril.position - car.transform.position, car.transform.forward)); quadros++; }
            }
            float somaD2 = 0f, maxD = 0f;
            int vaiVolta = 0;
            for (int k = 2; k < zs.Count; k++)
            {
                float d1 = zs[k] - zs[k - 1], d0 = zs[k - 1] - zs[k - 2];
                somaD2 += Mathf.Abs(d1 - d0);
                maxD = Mathf.Max(maxD, Mathf.Abs(d1));
                if (d1 * d0 < 0f && Mathf.Abs(d1) > 0.01f && Mathf.Abs(d0) > 0.01f) vaiVolta++;
            }
            Log($"{id} PILOTANDO 6-45 km/h: {quadros} quadros ({1f / Mathf.Max(0.001f, Time.smoothDeltaTime):0} fps) · quadril vai-e-volta {vaiVolta} vezes · tremida média {(zs.Count > 2 ? somaD2 / (zs.Count - 2) * 100f : 0f):0.00} cm/quadro · maior pulo {maxD * 100f:0.0} cm");
            float vel = Mathf.Abs(car.v);
            entrada = new FBInputState { move = new Vector2(0f, -1f) };
            // solta o freio quando para ou quando a velocidade troca de sinal (senão segurar trás vira ré)
            float sinal = Mathf.Sign(car.v), freou = 0f;
            for (; freou < 5f && Mathf.Abs(car.v) > 0.15f && Mathf.Sign(car.v) == sinal; freou += Time.deltaTime) yield return null;
            entrada = new FBInputState();
            yield return Wait(1.2f);
            Log($"{id} andou: {vel:0.0} u/s antes de frear · freou {freou:0.0} s · parado agora {car.v:+0.00;-0.00} u/s · caiu {(car.mx ? car.mx.crashed.ToString() : "-")}");
            yield return Sair(me, car, id, tipo, "parado");

            // sair andando devagar (só moto): o jogo pula a animação acima de 1,5 u/s
            if (car.kind != "car")
            {
                yield return Wait(1f);
                if (cc) cc.enabled = false;
                me.transform.position = car.transform.position - car.transform.right * 1.3f;
                if (cc) cc.enabled = true;
                yield return Wait(0.4f);
                apertarE = true;
                for (float w = 0f; w < 1f && !FBVehicles.CarOf(me); w += Time.deltaTime) yield return null;
                if (FBVehicles.CarOf(me) == car)
                {
                    for (float w = 0f; w < 5f && dp && dp.trans != null; w += Time.deltaTime) yield return null;
                    dp = me.GetComponent<FBDriverPose>();
                    for (float w = 0f; w < 5f && dp && dp.trans != null; w += Time.deltaTime) yield return null;
                    entrada = new FBInputState { move = new Vector2(0f, 0.35f) };
                    for (float w = 0f; w < 4f && Mathf.Abs(car.v) < 3f; w += Time.deltaTime) yield return null;
                    entrada = new FBInputState();
                    Log($"{id} saindo em movimento a {Mathf.Abs(car.v):0.0} u/s");
                    yield return Sair(me, car, id, tipo, "andando");
                }
            }
            if (FBVehicles.CarOf(me)) FBDrive.Exit(me, force: true);
            yield return Wait(0.5f);
        }

        IEnumerator Sair(FBCharacter me, FBCar car, string id, string tipo, string como)
        {
            var a0 = Pose(car);
            apertarE = true;
            yield return null;
            yield return null;
            var dp = me.GetComponent<FBDriverPose>();
            bool ok = !FBVehicles.CarOf(me) || (dp && dp.trans == "dismount");
            string fase = dp ? dp.trans ?? "-" : "-";
            float dMax = 0f, aMax = 0f, vMax = 0f, penMax = 0f, pyMin = 99f, pyMax = -99f, fim = -1f;
            var sb = new StringBuilder();
            float[] fotos = { 0.3f, 1.0f, 1.8f, 3.0f };
            int fi = 0;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                var a = Pose(car);
                dMax = Mathf.Max(dMax, (a.pos - a0.pos).magnitude);
                aMax = Mathf.Max(aMax, Ang(a.eul, a0.eul));
                vMax = Mathf.Max(vMax, RbMax(car));
                if (!FBVehicles.CarOf(me))
                {
                    if (fim < 0f) fim = t;
                    penMax = Mathf.Max(penMax, Penetracao(me, car));
                    var py = me.Pos.y - car.transform.position.y;
                    pyMin = Mathf.Min(pyMin, py);
                    pyMax = Mathf.Max(pyMax, py);
                }
                if (fi < fotos.Length && t >= fotos[fi])
                {
                    Foto(car, $"{id}_2_sair_{como}_{fotos[fi]:0.0}".Replace(',', '_'), me);
                    var l = car.transform.InverseTransformPoint(me.Pos);
                    sb.Append($" | t={fotos[fi]:0.0}s veículo Δ{(a.pos - a0.pos).magnitude:0.00}u inclinação {Ang(a.eul, a0.eul):0}° jogador local x{l.x:+0.00;-0.00} y{l.y:+0.00;-0.00} z{l.z:+0.00;-0.00}");
                    fi++;
                }
                yield return null;
            }
            var lp = car.transform.InverseTransformPoint(me.Pos);
            var ccc = me.GetComponent<CharacterController>();
            Log($"{id} ({tipo}) SAIR {como}: pedido {(ok ? "aceito" : "RECUSADO")} · fase {fase} · terminou em {(fim < 0 ? "não terminou" : fim.ToString("0.0") + " s")} · veículo andou {dMax:0.00} u · girou {aMax:0.0}° · física máx {vMax:0.00} u/s · jogador dentro do veículo até {penMax:0.00} u · altura do jogador {pyMin:+0.00;-0.00}..{pyMax:+0.00;-0.00} u · fim x{lp.x:+0.00;-0.00} z{lp.z:+0.00;-0.00} · no chão {(ccc ? ccc.isGrounded.ToString() : "-")} · arma visível {(me.weaponGO ? me.weaponGO.activeInHierarchy.ToString() : "sem arma")}{sb}");
            // anda para longe do veículo e confere quando a arma volta para a mão
            if (!FBVehicles.CarOf(me) && me.weapon != null)
            {
                var longe = (me.Pos - car.transform.position).Flat().normalized;
                if (longe.sqrMagnitude < 0.5f) longe = -car.transform.right;
                FBPlayer.I.camYaw = Quaternion.LookRotation(longe).eulerAngles.y;
                bool naMao = me.weaponGO && me.weaponGO.activeInHierarchy;
                entrada = new FBInputState { move = new Vector2(0f, 0.5f) };
                float tVolta = -1f;
                for (float t = 0f; t < 3f; t += Time.deltaTime)
                {
                    if (tVolta < 0f && me.weaponGO && me.weaponGO.activeInHierarchy) tVolta = t;
                    if (t > 1.2f) entrada = new FBInputState();
                    yield return null;
                }
                entrada = new FBInputState();
                Foto(car, $"{id}_3_afastou_{como}", me);
                Log($"{id} ({tipo}) AFASTAR depois de sair {como}: arma na mão logo ao sair {naMao} · voltou para a mão em {(tVolta < 0 ? "não voltou" : tVolta.ToString("0.0") + " s")} · distância do veículo {(me.Pos - car.transform.position).Flat().magnitude:0.00} u");
            }
        }

        // ---------- armas ----------
        IEnumerator Armas(FBCharacter me, Vector3 p)
        {
            var P = FBPlayer.I;
            if (FBVehicles.CarOf(me)) FBDrive.Exit(me, force: true);
            var cc = me.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            me.transform.position = p;
            me.Yaw = 0f;
            if (cc) cc.enabled = true;
            if (P.firstPerson) { trocarVista = true; yield return Wait(0.5f); }
            var an = me.an;
            var mE = an ? an.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            var mD = an ? an.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var peito = an ? an.GetBoneTransform(HumanBodyBones.Chest) : me.transform;
            var ids = GameData.Weapons.Keys.ToList();
            // lista "x" = todas as armas como estão; "ab" = cada arma sem e com o ajuste de empunhadura (FBCharacter.GripRefs);
            // "ab:id1;id2" = só essas armas
            bool ab = lista.StartsWith("ab");
            if (lista.StartsWith("ab:")) ids = lista.Substring(3).Split(';').Where(GameData.Weapons.ContainsKey).ToList();
            var refsJogo = new Dictionary<string, string>(FBCharacter.GripRefs);
            if (ab)
            {
                // teste: cada classe usa uma arma que já fica certa na mão como referência
                FBCharacter.GripRefs = new Dictionary<string, string>
                {
                    { "pistola", "desert_eagle" }, { "fuzil", "ak" }, { "submetralhadora", "ak" },
                    { "escopeta", "ak" }, { "sniper", "ak" }, { "explosivo", "ak" },
                };
            }
            Log($"armas: {ids.Count} · terceira pessoa{(ab ? " · antes x depois do ajuste de empunhadura" : "")}");
            foreach (var id in ids)
            {
                var d = GameData.Weapons[id];
                if (!FBRegistry.I.Weapon(id)) { Log($"arma {id}: SEM MODELO no registro"); continue; }
                string linha = "";
                Quaternion q0 = Quaternion.identity;
                Vector3 p0 = Vector3.zero;
                foreach (bool ajuste in ab ? new[] { false, true } : new[] { FBCharacter.GripAuto })
                {
                    FBCharacter.GripAuto = ajuste;
                    P.wu.Strip();
                    P.wu.Give(id);
                    int s = Array.FindIndex(P.wu.slots, x => x != null && x.id == id);
                    P.wu.Equip(s >= 0 ? s : 0, instant: true);
                    entrada = new FBInputState();
                    yield return Wait(0.3f);
                    var w = me.weaponGO;
                    if (ab && w)
                    {
                        if (!ajuste) { q0 = me.ProxyLocalRot; p0 = me.ProxyLocalPos; }
                        else
                        {
                            float ang = Quaternion.Angle(q0, me.ProxyLocalRot);
                            float cm = (me.ProxyLocalPos - p0).magnitude * (me.an ? me.an.GetBoneTransform(HumanBodyBones.RightHand).lossyScale.x : 1f) * 100f;
                            linha += $" || AJUSTE ref {FBCharacter.GripRefOf(d) ?? "-"}: girou {ang:0.0}° · moveu {cm:0.0} cm";
                            if (ang < 0.5f && cm < 0.5f) { linha += " (sem mudança)"; break; }
                        }
                    }
                    string tag = ab ? (ajuste ? "_depois" : "_antes") : "";
                    yield return Wait(0.9f);
                    string parado = Medir(me, id, mE, mD);
                    Foto(me, $"arma_{id}{tag}_parado", peito);
                    entrada = new FBInputState { aim = true };
                    yield return Wait(1.0f);
                    string mirando = Medir(me, id, mE, mD);
                    Foto(me, $"arma_{id}{tag}_mirando", peito);
                    entrada = new FBInputState();
                    yield return Wait(0.3f);
                    linha += $" || {(ab ? (ajuste ? "DEPOIS " : "ANTES ") : "")}PARADO {parado} || MIRANDO {mirando}";
                }
                Log($"arma {id} ({d.cls}){linha}");
            }
            FBCharacter.GripRefs = refsJogo;
            FBCharacter.GripAuto = true;
        }

        // ---------- sacar a arma (animação de troca) ----------
        // lista "x" = uma arma de cada tipo; ou "id1;id2;...". Cada arma é sacada duas vezes: "antes" (jeito antigo, a mira
        // e a mão esquerda entram logo que a mão pega a arma) e "depois" (FBCharacter.DrawFollowAnim: só no fim da animação).
        IEnumerator Saque(FBCharacter me, Vector3 p)
        {
            var P = FBPlayer.I;
            if (FBVehicles.CarOf(me)) FBDrive.Exit(me, force: true);
            var cc = me.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            me.transform.position = p;
            me.Yaw = 0f;
            if (cc) cc.enabled = true;
            if (P.firstPerson) { trocarVista = true; yield return Wait(0.5f); }
            var an = me.an;
            var peito = an ? an.GetBoneTransform(HumanBodyBones.Chest) : me.transform;
            var pescoco = an ? an.GetBoneTransform(HumanBodyBones.Neck) : me.transform;
            var quadril = an ? an.GetBoneTransform(HumanBodyBones.Hips) : me.transform;
            var ids = lista == "x" ? new List<string> { "ak", "m4", "fal", "rpg_7", "barret_50", "remington_870", "mp5", "uzi", "p90", "g18", "38", "desert_eagle" }
                : lista.Split(';').Where(GameData.Weapons.ContainsKey).ToList();
            float[] fotos = { 0.40f, 0.50f, 0.60f, 0.70f, 0.85f, 1.10f };
            var fimQuadro = new WaitForEndOfFrame();
            Log($"saque: {ids.Count} armas · terceira pessoa · antes x depois");
            foreach (var id in ids)
            {
                var d = GameData.Weapons[id];
                if (!FBRegistry.I.Weapon(id)) { Log($"saque {id}: SEM MODELO"); continue; }
                foreach (var modo in new[] { "antes", "depois" })
                {
                    FBCharacter.DrawFollowAnim = modo == "depois";
                    P.wu.Strip();
                    P.wu.Give(id);
                    entrada = new FBInputState();
                    yield return Wait(0.6f);
                    // guarda e saca de novo com a animação, como na troca de arma
                    int s = Array.FindIndex(P.wu.slots, x => x != null && x.id == id);
                    P.wu.Equip(3, instant: true);
                    yield return Wait(0.4f);
                    P.wu.Equip(s >= 0 ? s : 0, instant: false);
                    float pior = 99f, tPior = -1f, visivelEm = -1f, fimSoma = 0f;
                    int fi = 0, dentro = 0, quadros = 0, fimN = 0;
                    var curva = new StringBuilder();
                    float proxCurva = 0.35f;
                    for (float t = 0f; t < 1.5f; t += Time.deltaTime)
                    {
                        yield return fimQuadro;
                        var w = me.weaponGO;
                        float dist = 99f;
                        if (w && w.activeInHierarchy)
                        {
                            if (visivelEm < 0f) visivelEm = t;
                            dist = DistArmaCorpo(w, quadril.position, pescoco.position);
                            if (t < 1.05f)
                            {
                                quadros++;
                                if (dist < 0.13f * me.ScaleK) dentro++;
                                if (dist < pior) { pior = dist; tPior = t; }
                            }
                            else if (dist < 50f) { fimSoma += dist; fimN++; }
                        }
                        if (t >= proxCurva && t < 1.05f)
                        {
                            curva.Append(dist > 50f ? $" {t:0.00}:-" : $" {t:0.00}:{dist * 100f:0}");
                            proxCurva += 0.05f;
                        }
                        if (fi < fotos.Length && t >= fotos[fi])
                        {
                            var nome = $"saque_{id}_{modo}_{{0}}_{fotos[fi]:0.00}".Replace(',', '_');
                            Foto(me, string.Format(nome, "atras"), peito, "atras");
                            Foto(me, string.Format(nome, "cima"), peito, "cima");
                            fi++;
                        }
                    }
                    Log($"saque {id} {modo} ({d.cls}, {FBCharacter.HolsterSlot(d.cls)}): arma na mão em {(visivelEm < 0 ? "nunca" : visivelEm.ToString("0.00") + " s")} · quadros com a arma DENTRO do corpo até 1,05 s: {dentro} de {quadros} · menor distância arma-corpo {(pior > 50 ? "-" : (pior * 100f).ToString("0") + " cm")} em {tPior:0.00} s · parada no fim {(fimN == 0 ? "-" : (fimSoma / fimN * 100f).ToString("0") + " cm")} · curva cm:{curva}");
                }
            }
            FBCharacter.DrawFollowAnim = true;
        }

        // ---------- arma na mão parada, descansando, andando, correndo e no sprint (poses sem mirar) ----------
        // lista "x" = uma arma de cada tipo; ou "id1;id2;...". Cada arma duas vezes: "antes" (FBRig.TorsoClear desligado, o antigo)
        // e "depois" (ligado). Fotos no fim de cada fase: câmera do jogo, frente e de cima. No fim, saca andando.
        IEnumerator Andar(FBCharacter me, Vector3 p)
        {
            var P = FBPlayer.I;
            if (FBVehicles.CarOf(me)) FBDrive.Exit(me, force: true);
            if (P.firstPerson) { trocarVista = true; yield return Wait(0.5f); }
            var an = me.an;
            var peito = an ? an.GetBoneTransform(HumanBodyBones.Chest) : me.transform;
            var pescoco = an ? an.GetBoneTransform(HumanBodyBones.Neck) : me.transform;
            var quadril = an ? an.GetBoneTransform(HumanBodyBones.Hips) : me.transform;
            var ids = lista == "x" ? new List<string> { "ak", "m4", "remington_870", "barret_50", "rpg_7", "p90" }
                : lista.Split(';').Where(GameData.Weapons.ContainsKey).ToList();
            var andar = new FBInputState { move = new Vector2(0f, 0.5f) };
            var fases = new (string nome, float dur, FBInputState inp)[]
            {
                ("parado", 1.2f, new FBInputState()),
                ("descanso", 3.0f, new FBInputState()),
                ("andando", 1.6f, andar),
                ("correndo", 1.6f, new FBInputState { move = new Vector2(0f, 1f) }),
                ("sprint", 1.6f, new FBInputState { move = new Vector2(0f, 1f), sprintHeld = true }),
                ("parou", 1.2f, new FBInputState()),
                ("mirando_andando", 1.6f, new FBInputState { move = new Vector2(0f, 0.5f), aim = true }),
            };
            var fimQuadro = new WaitForEndOfFrame();
            var cc = me.GetComponent<CharacterController>();
            Log($"andar: {ids.Count} armas · terceira pessoa · antes x depois (arma fora do tronco)");
            foreach (var id in ids)
            {
                var d = GameData.Weapons[id];
                if (!FBRegistry.I.Weapon(id)) { Log($"andar {id}: SEM MODELO"); continue; }
                foreach (var modo in new[] { "antes", "depois" })
                {
                    FBRig.TorsoClear = modo == "depois";
                    entrada = new FBInputState();
                    if (cc) cc.enabled = false;
                    me.transform.position = p;
                    me.Yaw = 0f;
                    if (cc) cc.enabled = true;
                    P.wu.Strip();
                    P.wu.Give(id);
                    int s = Array.FindIndex(P.wu.slots, x => x != null && x.id == id);
                    P.wu.Equip(s >= 0 ? s : 0, instant: true);
                    yield return Wait(0.8f);
                    var linha = new StringBuilder();
                    foreach (var f in fases)
                    {
                        entrada = f.inp;
                        float pior = 0f, piorB = 0f;
                        int dentro = 0, quadros = 0, braco = 0;
                        var rg = me.rig;
                        if (rg)
                        {
                            rg.DbgHandsN = rg.DbgIkN = rg.DbgTorsoN = rg.DbgTorsoHit = 0; rg.DbgTorsoPen = rg.DbgTorsoResid = 0f;
                            rg.DbgPushN = 0; rg.DbgPushReq = rg.DbgPushGot = 0f; rg.DbgPushDir = rg.DbgDeepRel = Vector3.zero;
                            rg.DbgPhaseKept[0] = rg.DbgPhaseKept[1] = 0;
                            rg.DbgCarryAng = 0f; rg.DbgCarryN = 0;
                        }
                        for (float t = 0f; t < f.dur; t += Time.deltaTime)
                        {
                            yield return fimQuadro;
                            var w = me.weaponGO;
                            if (w && w.activeInHierarchy)
                            {
                                float fundo = Fundo(me, w, out float fb);
                                quadros++;
                                if (fundo > 0.02f * me.ScaleK) { dentro++; Conta(ossoFundo.ToString()); }
                                if (fb > 0.02f * me.ScaleK) { braco++; Conta(ossoBraco.ToString()); }
                                pior = Mathf.Max(pior, fundo);
                                piorB = Mathf.Max(piorB, fb);
                            }
                        }
                        Foto(me, $"andar_{id}_{modo}_{f.nome}_atras", peito, "atras");
                        Foto(me, $"andar_{id}_{modo}_{f.nome}_frente", peito);
                        Foto(me, $"andar_{id}_{modo}_{f.nome}_cima", peito, "cima");
                        linha.Append($" | {f.nome}: no tronco/cabeça {dentro}/{quadros} (fundo {pior * 100f:0} cm), no braço {braco} (fundo {piorB * 100f:0} cm) <{Ossos()}>");
                        if (rg) linha.Append($" [ik {rg.DbgIkN}/{rg.DbgHandsN}, tronco {rg.DbgTorsoHit}/{rg.DbgTorsoN}, entra {rg.DbgTorsoPen * 100f:0} cm, sobra {rg.DbgTorsoResid * 100f:0} cm]");
                        if (rg && rg.DbgPushN > 0)
                        {
                            float n = rg.DbgPushN;
                            Vector3 dd = rg.DbgPushDir / n, dp = rg.DbgDeepRel / n * 100f;
                            linha.Append($" {{mão: pedido {rg.DbgPushReq / n * 100f:0} cm, andou {rg.DbgPushGot / n * 100f:0} cm, rumo dir {dd.x:0.00} cima {dd.y:0.00} frente {dd.z:0.00}, ponto fundo vs mão dir {dp.x:0} cima {dp.y:0} frente {dp.z:0} cm, mantidas mão {rg.DbgPhaseKept[0]} giro {rg.DbgPhaseKept[1]}, arma girada em {rg.DbgCarryN} quadros, giro máx {rg.DbgCarryAng:0}°}}");
                        }
                    }
                    // sacar andando (como depois de descer da moto): guarda, anda e saca
                    {
                        entrada = andar;
                        P.wu.Equip(3, instant: true);
                        yield return Wait(0.5f);
                        P.wu.Equip(s >= 0 ? s : 0, instant: false);
                        float[] fotos = { 0.45f, 0.55f, 0.7f, 0.9f, 1.2f };
                        int fi = 0, dentro = 0, quadros = 0;
                        float pior = 0f;
                        for (float t = 0f; t < 1.3f; t += Time.deltaTime)
                        {
                            yield return fimQuadro;
                            var w = me.weaponGO;
                            if (w && w.activeInHierarchy)
                            {
                                float fundo = Fundo(me, w, out _);
                                quadros++;
                                if (fundo > 0.02f * me.ScaleK) dentro++;
                                pior = Mathf.Max(pior, fundo);
                            }
                            if (fi < fotos.Length && t >= fotos[fi])
                            {
                                Foto(me, $"andar_{id}_{modo}_sacar_{fotos[fi]:0.00}_atras".Replace(',', '_'), peito, "atras");
                                fi++;
                            }
                        }
                        linha.Append($" | sacar andando: no tronco/cabeça {dentro}/{quadros} (fundo {pior * 100f:0} cm)");
                    }
                    Log($"andar {id} {modo} ({d.cls}) pontos da arma {pontosN}{linha}");
                }
            }
            FBRig.TorsoClear = true;
            entrada = new FBInputState();
        }

        // o quanto a arma entra no corpo: pontos da malha da arma (até ~1500, no espaço da arma) conferidos contra o corpo
        // medido da malha do personagem (FBCharacter.BodyDepth). Devolve o ponto mais fundo no tronco/cabeça e, em braco,
        // o mais fundo nos braços/pernas (antebraços e mãos não contam: seguram a arma).
        static string pontosId;
        static Vector3[] pontos;
        static string pontosN = "-";
        static HumanBodyBones ossoFundo, ossoBraco;
        static Dictionary<string, int> ossos = new Dictionary<string, int>();
        static void Conta(string o) { ossos.TryGetValue(o, out int n); ossos[o] = n + 1; }
        static string Ossos() { var r = string.Join(", ", ossos.OrderByDescending(x => x.Value).Take(3).Select(x => $"{x.Key} {x.Value}")); ossos.Clear(); return r; }
        static float Fundo(FBCharacter me, GameObject w, out float braco)
        {
            braco = 0f;
            string id = me.weapon != null ? me.weapon.id : "";
            if (pontos == null || pontosId != id)
            {
                pontosId = id;
                var lista = new List<Vector3>();
                int malhas = 0, caixas = 0;
                var w2l = w.transform.worldToLocalMatrix;
                foreach (var r in w.GetComponentsInChildren<Renderer>())
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(r.name, "laser|beam|flash", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
                    Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : (r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null);
                    if (!m) continue;
                    var mat = w2l * r.transform.localToWorldMatrix;
                    if (m.isReadable)
                    {
                        malhas++;
                        var v = m.vertices;
                        int passo = Mathf.Max(1, v.Length / 600);
                        for (int i = 0; i < v.Length; i += passo) lista.Add(mat.MultiplyPoint3x4(v[i]));
                    }
                    else
                    {
                        caixas++;
                        var b = m.bounds;
                        for (int i = 0; i < 125; i++)
                            lista.Add(mat.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(i % 5 / 2f - 1f, i / 5 % 5 / 2f - 1f, i / 25 / 2f - 1f))));
                    }
                }
                pontos = lista.ToArray();
                pontosN = $"{pontos.Length} ({malhas} malhas, {caixas} sem malha legível)";
            }
            float fundo = 0f;
            var t = w.transform;
            for (int i = 0; i < pontos.Length; i++)
            {
                float d = me.BodyDepth(t.TransformPoint(pontos[i]), true, out _, out int parte);
                if (parte == 1) { if (d > braco) { braco = d; ossoBraco = me.DepthBone; } }
                else if (d > fundo) { fundo = d; ossoFundo = me.DepthBone; }
            }
            return fundo;
        }

        // menor distância entre o eixo da arma (do fim da coronha à ponta do cano) e o eixo do tronco (quadril ao pescoço)
        static float DistArmaCorpo(GameObject w, Vector3 q, Vector3 n)
        {
            Vector3 dir;
            if (FBCharacter.GunAxes(w, out var space, out var barrel, out _)) dir = space.TransformDirection(barrel).normalized;
            else dir = w.transform.forward;
            float lo = 1e9f, hi = -1e9f;
            Vector3 c = w.transform.position;
            foreach (var mf in w.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh || !mf.gameObject.activeInHierarchy) continue;
                var b = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var cp = mf.transform.TransformPoint(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z));
                    float pr = Vector3.Dot(cp - c, dir);
                    lo = Mathf.Min(lo, pr); hi = Mathf.Max(hi, pr);
                }
            }
            if (lo > hi) return 99f;
            return SegDist(c + dir * lo, c + dir * hi, q, n);
        }

        static float SegDist(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
            float s, t;
            if (a < 1e-8f && e < 1e-8f) return r.magnitude;
            if (a < 1e-8f) { s = 0f; t = Mathf.Clamp01(f / e); }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e < 1e-8f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else
                {
                    float b = Vector3.Dot(d1, d2), den = a * e - b * b;
                    s = den != 0f ? Mathf.Clamp01((b * f - c * e) / den) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            return ((p1 + d1 * s) - (p2 + d2 * t)).magnitude;
        }

        static string Medir(FBCharacter me, string id, Transform mE, Transform mD)
        {
            var w = me.weaponGO;
            if (!w) return "sem modelo na mão";
            if (!w.activeInHierarchy) return "arma ESCONDIDA";
            var rs = w.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (rs.Length == 0) return "arma sem malha";
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Transform grip = null, muzzle = null;
            foreach (var t in w.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("SOCKET_Grip")) grip = t;
                else if (t.name.StartsWith("SOCKET_Muzzle")) muzzle = t;
            }
            float distE = mE ? Vector3.Distance(b.ClosestPoint(mE.position), mE.position) : -1f;
            float distD = mD ? Vector3.Distance(b.ClosestPoint(mD.position), mD.position) : -1f;
            string cano = "-";
            if (muzzle)
            {
                var dirCano = (muzzle.position - b.center).normalized;
                cano = $"{Vector3.Angle(dirCano, me.transform.forward):0}°";
            }
            return $"tamanho {b.size.magnitude:0.00} u · mão D até a arma {distD:0.00} · mão E até a arma {distE:0.00} · cano x frente {cano} · empunhadura {(grip ? "sim" : "NÃO")} · escala {w.transform.lossyScale.x:0.00}";
        }

        // ---------- gráfico ----------
        IEnumerator Grafico()
        {
            var gdir = Path.Combine(dir, "grafico");
            Directory.CreateDirectory(gdir);
            fotoCam = new GameObject("FB_FotoCam").AddComponent<Camera>();
            fotoCam.enabled = false;
            Object.DontDestroyOnLoad(fotoCam.gameObject);
            foreach (var item in lista.Split(','))
            {
                var p = item.Trim().Split(':');
                string map = p[0], hora = p.Length > 1 ? p[1] : "dia", clima = p.Length > 2 ? p[2] : "limpo";
                var antes = FBSession.WeatherFor(map);
                FBSession.map = map;
                FBSession.mode = "classico";
                FBSession.online = false;
                FBSession.view = "tps";
                FBSession.SetWeather(map, hora, clima);
                var op = SceneManager.LoadSceneAsync("Map_" + map);
                while (op != null && !op.isDone) yield return null;
                for (float w = 0f; w < 40f && !(FBPlayer.I && FBPlayer.I.wu && FBMatch.Player); w += Time.deltaTime) yield return null;
                yield return Wait(8f);
                var me = FBMatch.Player;
                FBSession.SetWeather(map, antes.time, antes.weather);
                if (!me || !Camera.main) { Log($"grafico {map} {hora} {clima}: sem jogador ou sem câmera"); continue; }
                FBMatch.God = true;
                foreach (var b in FBMatch.Bots.ToList()) if (b) b.gameObject.SetActive(false);
                var sb = new StringBuilder();
                for (int k = 0; k < 4; k++)
                {
                    me.Yaw = k * 90f;
                    yield return Wait(1.2f);
                    sb.Append($" | {k * 90}° {Render(Camera.main, Path.Combine(gdir, $"{map}_{hora}_{clima}_{k * 90}.jpg"))}");
                }
                // céu: mesma câmera do jogo, olhando 25° para cima
                var mc = Camera.main;
                fotoCam.CopyFrom(mc);
                fotoCam.enabled = false;
                var dados = fotoCam.GetUniversalAdditionalCameraData();
                var dMain = mc.GetUniversalAdditionalCameraData();
                if (dados && dMain)
                {
                    dados.renderPostProcessing = dMain.renderPostProcessing;
                    dados.antialiasing = dMain.antialiasing;
                }
                fotoCam.transform.SetPositionAndRotation(mc.transform.position, Quaternion.Euler(-25f, mc.transform.eulerAngles.y, 0f));
                sb.Append($" | céu {Render(fotoCam, Path.Combine(gdir, $"{map}_{hora}_{clima}_ceu.jpg"))}");
                Log($"grafico {map} {hora} {clima}{sb}");
            }
        }

        // devolve brilho médio, % de preto e % de rosa (shader com erro) da imagem
        static string Render(Camera cam, string arq)
        {
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                else { var velho = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = velho; }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(arq, tex.EncodeToJPG(85));
                var px = tex.GetPixels32();
                double soma = 0; int preto = 0, rosa = 0;
                foreach (var c in px)
                {
                    float l = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                    soma += l;
                    if (l < 0.02f) preto++;
                    if (c.r > 200 && c.g < 60 && c.b > 200) rosa++;
                }
                Object.Destroy(tex);
                return $"brilho {soma / px.Length:0.00} · preto {100f * preto / px.Length:0}% · rosa {100f * rosa / px.Length:0.0}%";
            }
            catch (Exception e) { return "render falhou: " + e.Message; }
            finally { rt.Release(); Object.Destroy(rt); }
        }

        // ---------- foto ----------
        // vista: null = frente/esquerda (padrão), "atras" = como a câmera do jogo (atrás, ombro direito), "cima" = de cima
        void Foto(Component alvo, string nome, Component olhar, string vista = null)
        {
            var t = alvo.transform;
            bool veiculo = alvo is FBCar;
            var foco = olhar ? olhar.transform.position : t.position;
            if (veiculo)
            {
                foco = t.position + Vector3.up * 0.8f;
                fotoCam.transform.position = t.position - t.right * 4.5f + t.forward * 1.5f + Vector3.up * 2.0f;
            }
            else if (vista == "atras")
            {
                fotoCam.transform.position = t.position - t.forward * 2.0f + t.right * 0.7f + Vector3.up * 1.75f;
            }
            else if (vista == "cima")
            {
                fotoCam.transform.position = foco + Vector3.up * 2.0f + t.forward * 0.35f;
            }
            else
            {
                fotoCam.transform.position = t.position + t.forward * 2.0f - t.right * 1.2f + Vector3.up * 1.6f;
            }
            if (vista == "cima") fotoCam.transform.LookAt(foco, t.forward);
            else fotoCam.transform.LookAt(foco);
            var rt = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGB32);
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(fotoCam, req)) RenderPipeline.SubmitRenderRequest(fotoCam, req);
                else { fotoCam.targetTexture = rt; fotoCam.Render(); fotoCam.targetTexture = null; }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, nome + ".jpg"), tex.EncodeToJPG(80));
                Object.Destroy(tex);
            }
            catch (Exception e) { Log("foto " + nome + " falhou: " + e.Message); }
            finally { rt.Release(); Object.Destroy(rt); }
        }
    }
}
#endif
