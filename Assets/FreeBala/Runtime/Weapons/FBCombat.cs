// Combate (porte do main.js: bulletFrom / damageActor / knockdown / impact): uma bala = raio pela física do Unity contra o mapa,
// vidros, água e as hitboxes dos ossos. r51: o dano é o modelo do Counter-Strike (FBDamageModel: dano base por arma, região do corpo,
// queda pela distância, colete/capacete pela penetração da arma, parede atravessável) em todos os modos; -fbdanoantigo volta para a
// "capa" do Free Fire (FBBrCore). Knockdown/revive/sangramento nos modos de equipe (FBMechanics).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBCombat
    {
        public static readonly float[][] DIFF = { new[] { .55f, .75f, 1.4f }, new[] { 1f, 1f, 1f }, new[] { 1.55f, 1.35f, .7f } };   // acc, dmg, react
        public static float DiffAcc => DIFF[Mathf.Clamp(FBSession.diff, 0, 2)][0];
        public static float DiffDmg => DIFF[Mathf.Clamp(FBSession.diff, 0, 2)][1];
        public static float DiffReact => DIFF[Mathf.Clamp(FBSession.diff, 0, 2)][2];
        static float Now => Time.time;
        static readonly Collider[] pbBuf = new Collider[16];   // [parceiro] fisica 31/10: buffer do teste de ponto-a-queima-roupa (origem do raio dentro do hitbox do alvo)

        public struct Shot { public Vector3 end; public FBCharacter hit; public bool head; public string part; public float dist; public Vector3 normal; public Collider col; }

        // uma bala: o mais perto entre mapa, hitbox e alvo de treino; vidro e água no caminho reagem.
        // r52 dry = só a trajetória (indicador de impacto da mira): a MESMA escolha de alvo/parede/penetração, sem dano, som, FX nem contadores
        public static Shot BulletFrom(Vector3 o, Vector3 dir, Vector3 muzzlePos, FBCharacter shooter, WeaponDef weapon, float near = 0, bool tracer = true, float maxD = 300, int pellets = 1, bool dry = false)
        {
            var r = new Shot { end = o + dir * maxD, dist = maxD };
            if (!dry) { ShotN++; LastO = o; LastDir = dir; if (weapon != null) FBBotBrain.Noise(muzzlePos); }   // r42: o "tiro ouvido" nunca era publicado (bots/eventos de rua ficavam surdos)
            var hits = Physics.RaycastAll(o + dir * near, dir, maxD - near, FBWorld.ShotMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            RaycastHit? wall = null; var st = FBDamageModel.Of(weapon);
            // r51: penetração do CS — madeira/reboco/chapa fina/vidro/folhagem (FBImpactFx.Surf) com espessura até poder × resistência do
            // material: a bala segue com o dano reduzido (até 2 superfícies); concreto grosso, terra e o resto param. A espessura é medida
            // pelo raio de volta no MESMO colisor (sem face de saída dentro do limite = grosso demais).
            float dmgK = 1, skipTo = -1; int pens = 0;
            foreach (var h in hits)
            {
                if (h.distance < skipTo) continue;
                var hb = h.collider.GetComponent<FBHitbox>();
                if (hb)
                {
                    var t = hb.owner; if (!t || t == shooter || !t.alive) continue;
                    if (!t.civ && !FBMatch.Hostile(t, shooter)) continue;                        // aliado: a bala atravessa
                    string hpart = hb.PartAt(h.point); var hh = h;
                    if (FBDamageModel.On && hpart == "chest" && hb.AboveNeck(h.point))
                    {   // r51d: a cápsula do peito (peito → pescoço, raio 0,28) tem a ponta redonda 0,28 ACIMA do pescoço, cobrindo a metade de baixo da
                        // cabeça e mais à frente que a esfera dela: no exe 23 de 54 tiros no centro da cabeça contavam peito (×1 em vez de ×4). Acima do
                        // pescoço só vale a cabeça: se o raio segue até a esfera da cabeça do mesmo alvo, é cabeça; senão passou ao lado do pescoço.
                        int hix = -1; for (int k = 0; k < hits.Length; k++) { var o2 = hits[k].distance > h.distance ? hits[k].collider.GetComponent<FBHitbox>() : null; if (o2 && o2.owner == t && o2.head) { hix = k; break; } }
                        if (hix < 0) continue; hh = hits[hix]; hpart = "head";
                    }
                    else if (!FBDamageModel.On && !hb.head && FBHitbox.Torso(hpart))
                    {   // [parceiro] fisica 31/10: a meia-esfera de cima da capsula do tronco (centrada no pescoço) fica na frente do terço de baixo
                        // da esfera da cabeça (queixo/mandíbula) e o raio pegava "body" primeiro. Se o raio entra na cabeça do MESMO dono logo
                        // depois (até 12 cm), vale cabeça. Só para o tronco: braço na frente do rosto continua sendo membro.
                        // (encurtar a cápsula abriria um vão de 2-3 cm no pescoço, por onde a bala atravessaria)
                        // União: com o modelo do CS (FBDamageModel.On) vale o r51d acima (ponto acima do pescoço, medido no exe); esta regra
                        // cobre o dano antigo (-fbdanoantigo), que não tinha correção nenhuma.
                        float lim = h.distance + .12f * t.ScaleK;
                        for (int k = 0; k < hits.Length && hits[k].distance <= lim; k++)
                        {
                            if (hits[k].distance <= h.distance) continue; var o2 = hits[k].collider.GetComponent<FBHitbox>();
                            if (o2 && o2.owner == t && o2.head) { hh = hits[k]; hpart = "head"; break; }
                        }
                    }
                    r.hit = t; r.head = hb.head || hpart == "head"; r.part = hpart; r.dist = hh.distance + near; r.end = hh.point; r.normal = hh.normal; r.col = hh.collider; break;
                }
                var tg = h.collider.GetComponentInParent<FBTarget>(); if (tg && shooter && shooter.isPlayer) { r.dist = h.distance + near; r.end = h.point; r.col = h.collider; if (dry) return r; tg.Hit(h.point, r.dist); FBHUD.I?.Hit(tg.IsHead(h.point)); FBAudio.Play2D("hitmark", .55f); if (tracer) FBFx.Tracer(muzzlePos, r.end); return r; }
                if (h.collider.gameObject.layer == FBWorld.LGlass) { var g = h.collider.GetComponent<FBGlass>(); if (g && !dry) g.Break(h.point, dir); continue; }
                if (h.collider.isTrigger) continue;
                // r51: colisor físico preso a QUALQUER personagem (objeto na mão, peça do corpo) não para a bala — o corpo são as hitboxes. Antes só
                // os do atirador eram ignorados: no exe, tiros no braço (cápsula fina) batiam antes num colisor do alvo e perdiam até 65 % do dano
                if (h.collider.GetComponentInParent<FBCharacter>()) continue;
                if (st != null && pens < 2 && st.pen > 0 && Penetrate(h, dir, st, out float thick, out float maxT))
                {
                    pens++; dmgK *= FBDamageModel.AfterWall(thick, maxT); skipTo = h.distance + thick * FBDamageModel.UnitsPerMeter;
                    if (!dry)
                    {
                        FBDamageModel.Penetrations++; Impact(h, dir); float pd = weapon.Damage(h.distance + near, false); FBDestructible.TryHit(h.collider, pd, h.point, dir);
                        var pc = h.collider.GetComponentInParent<FBCar>(); if (pc) FBCarDamage.OnShot(pc, h.point, dir, pd);
                    }
                    if (dmgK > .05f) continue;
                }
                wall = h; r.dist = h.distance + near; r.end = h.point; r.normal = h.normal; r.col = h.collider; break;
            }
            // [parceiro] fisica 31/10: origem do raio dentro do hitbox do alvo (personagens encostados): o Raycast não reporta collider que
            // contém a origem. Antes do 'dry' para o indicador de impacto escolher o mesmo alvo.
            var o0 = o + dir * near; int nPb = Physics.OverlapSphereNonAlloc(o0, .02f, pbBuf, 1 << FBWorld.LHitbox, QueryTriggerInteraction.Collide);
            FBHitbox pb = null;
            for (int j = 0; j < nPb; j++)
            {
                var hb = pbBuf[j].GetComponent<FBHitbox>(); if (!hb || !hb.owner || hb.owner == shooter || !hb.owner.alive) continue;
                if ((hb.owner.civ || FBMatch.Hostile(hb.owner, shooter)) && (!pb || hb.head)) pb = hb;   // cabeça tem prioridade
            }
            if (pb && r.hit != pb.owner)
            {
                string pp = pb.head ? "head" : pb.PartAt(o0);
                r.hit = pb.owner; r.head = pb.head || pp == "head"; r.part = pp; r.dist = near; r.end = o0; r.normal = -dir; r.col = pb.GetComponent<Collider>();
                wall = null; dmgK = 1;                                                              // nada entre a boca e o alvo
            }
            if (dry) return r;
            if (FBWorld.WaterHit(o, dir, r.dist, out var wp)) FBFx.Splash(wp, 1);
            Flyby(o, r.end, shooter, r.hit);
            FBStreetPoleFx.Ray(o, dir, r.dist);                                                    // luminária de poste no caminho da bala
            if (tracer) FBFx.Tracer(muzzlePos, r.end);
            if (r.hit)
            {
                var t = r.hit; float dealt; float ap = 0; bool armorDone = false;
                bool botK = shooter && !shooter.isPlayer && !shooter.isRemote;
                if (st != null)
                {   // r51: modelo do CS — região × queda pela distância × colete/capacete (no dono do personagem se ele for remoto)
                    string part = r.part ?? (r.head ? "head" : "chest");
                    var res = FBDamageModel.Hit(st, part, r.dist, t.isRemote ? null : t);
                    float hpK = FBMatch.I && FBMatch.I.FFDamage ? Mathf.Max(1, t.maxHp) / 100f : 1;    // BR: vida 200 → mesmo número de tiros
                    dealt = FBDamageModel.Whole(res.health * dmgK * hpK * (botK ? DiffDmg : 1)); armorDone = !t.isRemote; ap = st.ap; r.head = part == "head"; r.part = part;
                    LastHit = new HitLog { weapon = weapon.id, part = part, meters = r.dist / FBDamageModel.UnitsPerMeter, raw = res.raw, health = dealt, armored = res.armored, wallK = dmgK, victim = t.Name };
                }
                else if (weapon != null && !t.civ && FBSession.mode != "treino")
                {
                    bool br = FBMatch.I && FBMatch.I.FFDamage;
                    var res = FBBrCore.Hit(FBBrCore.FFWeapon(weapon.id, weapon.cls), r.part ?? (r.head ? "head" : "body"), br ? r.dist : r.dist / 1.2f, t.vest, t.helmetFF, Time.time - t.poseT);
                    dealt = res.dmg * (br ? 1 : 100 / FBBrCore.HP) * (botK ? DiffDmg : 1);
                    r.head = res.part == "head";
                }
                else dealt = weapon != null ? weapon.Damage(r.dist, r.head) / Mathf.Max(1, pellets) * Random.Range(.97f, 1.03f) : 20;
                if (shooter && shooter.isPlayer) FBMatch.Stats.hits++;
                DamageActor(t, dealt, r.head, shooter, null, new FBHitInfo { valid = true, point = r.end, normal = r.normal, dir = dir, part = r.head ? "head" : r.part ?? "body", weaponId = weapon?.id, dist = r.dist, armorDone = armorDone, ap = ap });
            }
            else if (wall is RaycastHit w)
            {
                Impact(w, dir); float wd = weapon != null ? weapon.Damage(r.dist, false) * dmgK : 20; FBDestructible.TryHit(w.collider, wd, w.point, dir);   // objeto pequeno: perde vida e quebra
                var hc = w.collider.GetComponentInParent<FBCar>(); if (hc) FBCarDamage.OnShot(hc, w.point, dir, wd);                    // carro: vidro / farol / vida
                FBStreetPoleFx.Shot(w.collider, w.point, dir); FBFisica.Shot(w.collider, w.point, dir);                                   // r44: objeto solto leva o tiro                                                                         // luminária de poste: apaga com o tiro
            }
            LastEnd = r.end; Last = r; return r;
        }
        public static int ShotN; public static Vector3 LastO, LastDir, LastEnd; public static Shot Last;                    // r51: último tiro (teste da mira no exe)
        // r51: espessura do obstáculo no ponto (m) e o máximo que esta arma atravessa nesse material; false = para a bala
        static bool Penetrate(RaycastHit h, Vector3 dir, FBDamageModel.Stat st, out float thick, out float maxT)
        {
            thick = 0; maxT = FBDamageModel.Resist(FBImpactFx.Classify(h.collider, h.triangleIndex)) * st.pen; if (maxT <= 0) return false;
            if (h.collider.GetComponentInParent<FBCharacter>() || h.collider is TerrainCollider) return false;
            float L = (maxT + .02f) * FBDamageModel.UnitsPerMeter;
            if (!h.collider.Raycast(new Ray(h.point + dir * L, -dir), out var back, L)) { FBDamageModel.PenBlocked++; return false; }
            thick = Mathf.Max(.005f, (L - back.distance) / FBDamageModel.UnitsPerMeter); return thick <= maxT;
        }
        // último acerto calculado pelo modelo (laboratório -fbdanolab)
        public struct HitLog { public string weapon, part, victim; public float meters, raw, health, wallK; public bool armored; }
        public static HitLog LastHit;

        // zunido/estalo de bala passando perto do jogador (rodada 37): antes só os bots tocavam, em 2D (sem direção) e por sorteio. Agora toda
        // bala que passa a menos de 2,6 m da cabeça toca no PONTO DE PASSAGEM (3D): perto (< 1 m) = estalo supersônico, senão zunido
        static float lastFlyby;
        static void Flyby(Vector3 a, Vector3 b, FBCharacter shooter, FBCharacter hit)
        {
            var me = FBMatch.Player; if (!me || !me.alive || shooter == me || hit == me || !shooter) return;
            if (Time.time - lastFlyby < .07f) return;
            var head = me.Head ? me.Head.position : me.Pos + Vector3.up * 1.6f * me.ScaleK; var ab = b - a; float L = ab.magnitude; if (L < .5f) return;
            float t = Mathf.Clamp(Vector3.Dot(head - a, ab / L), 0, L); if (t < 2 || t > L - .3f) return;           // a bala tem de PASSAR (não parar antes)
            var c = a + ab / L * t; float d = (c - head).magnitude; if (d > 2.6f) return;
            lastFlyby = Time.time; string snd = d < 1 ? "crack" : "flyby" + Random.Range(1, 4);
            FBAudio.PlayAt(snd, c, Mathf.Lerp(.85f, .35f, d / 2.6f), 1.2f, .1f, 0, 1.8f);
        }
        public static void Impact(RaycastHit h, Vector3 dir)
        {
            var n = h.normal; if (Vector3.Dot(n, dir) > 0) n = -n;
            FBImpactFx.Bullet(h, dir);   // impacto por material (buraco alongado, poeira/faísca/lasca, ricochete)
            // som pelo material atingido (171: concreto, terra, areia, metal/ricochete, madeira, vidro, folhas, água); sem ele, os genéricos
            var sk = "impact_" + FBImpactFx.Classify(h.collider, h.triangleIndex).ToString().ToLowerInvariant();
            FBAudio.PlayAt(FBAudio.Has(sk) ? sk : FBUtil.Pick(new[] { "impact1", "impact2", "impact3", "impact4" }), h.point, .7f, 3);
        }

        // ---- dano (damageActor)
        public static void DamageActor(FBCharacter a, float dmg, bool head, FBCharacter src, string why = null, FBHitInfo hit = default)
        {
            if (a) { a.lastWhy = why; a.lastHitHead = head; if (hit.valid) a.lastHit = hit; a.lastHitFrom = src ? src.Pos : (Vector3?)null; }   // r42: a morte usa (FBDeath)
            if (!a || !a.alive || (a.special && why != "takedown")) return;
            var type = FBDamageEvents.TypeOf(why, hit);
            if (a.isRemote)
            {
                FBNet.I?.SendHit(a, dmg, head, hit.ap, hit.part); if (src && src.isPlayer) { FBHUD.I?.Hit(head); a.Trigger("hit"); }
                // o dono do personagem aplica o dano; aqui só o efeito visual local do acerto (sangue no ponto real). Voz vem pela rede.
                FBDamageEvents.Raise(new FBDamage { victim = a, source = src, type = type, amount = dmg, applied = Mathf.Min(dmg, a.hp), hpBefore = a.hp, hpAfter = a.hp, head = head, hit = hit, why = why });
                return;
            }
            bool env = why == "zone" || why == "fall" || why == "bleed";
            if (a.isPlayer && FBKillstreak2.gunner != null && !env && why != "gunner") { FBKillstreak2.Hit(src); return; }   // atirador do killstreak: as balas acertam o veículo
            if (!env && a.protUntil > Now) return;                                       // proteção de spawn (CF SpawnProtectionShield)
            if (a.knocked && why != "bleed" && FBMechanics.KnockInvulnerable(a, Now)) return;
            if (a.isPlayer && FBMatch.God) dmg = 0;
            if (!env && why != "takedown" && !(FBMatch.I && FBMatch.I.FFDamage) && !hit.armorDone)   // r51: o modelo do CS já descontou o colete (FBDamageModel)
            {
                if (head && a.helmet) dmg *= .6f;                                              // capacete: headshot tira bem menos
                else if (!head && a.armor > 0) { float ab = Mathf.Min(a.armor, dmg * .45f); a.armor -= ab; dmg -= ab; }
            }
            float before = Mathf.Max(0, a.hp), applied = Mathf.Min(before, Mathf.Max(0, dmg));
            var ip = DamagePoint(a, head);
            a.hp -= dmg;
            if (applied > 0 && src && src.isPlayer && !env) { FBHUD.I?.FloatDamage(ip, applied, head); FBAudio.Synth(head ? "hit_head" : "hit_body"); }
            if (!env)
            {
                if (!hit.valid) { var bd = src ? (ip - (src.Pos + Vector3.up * 1.4f * src.ScaleK)).normalized : new Vector3(Random.Range(-1f, 1f), .3f, Random.Range(-1f, 1f)).normalized; hit = new FBHitInfo { valid = false, point = ip, normal = -bd, dir = bd, part = head ? "head" : "body" }; }
                if (src && src.isPlayer) { FBHUD.I?.Hit(head); if (!FBMatch.FFA) a.tagT = 4; }
                a.Trigger("hit");
            }
            if (!a.isPlayer && !a.isRemote && !a.knocked && src && src != a && src.team != a.team) a.GetComponent<FBBot>()?.OnHit(src);
            if (a.isPlayer && src && src != a) FBDamageIndicators.Hit(src, src.Pos, applied);
            if (!env && applied > 0) FBCombatStats.Damage(src, a, head);                       // telemetria do autoteste (-fbr37)   // arco por atacante, segue a câmera
            // sangue, impacto, voz de dor: assinantes do barramento (FBBloodFx, FBDamageVoice) — só efeito, não mexem no dano
            bool knockNow = a.hp <= 0 && !a.civ && !a.knocked && why != "bleed" && FBMatch.KnockOn && FBMechanics.CanKnock(a, FBSession.mode, FBMatch.HasReviver(a));
            FBDamageEvents.Raise(new FBDamage { victim = a, source = src, type = type, amount = dmg, applied = applied, hpBefore = before, hpAfter = a.hp, head = head, killed = a.hp <= 0 && !knockNow, knocked = knockNow, hit = hit, why = why });
            if (a.isPlayer)
            {   // efeito proporcional ao dano (rodada 40): fração da vida perdida neste golpe -> vinheta e tranco da câmera
                float frac = Mathf.Clamp01(applied / Mathf.Max(1, a.maxHp)), k = why == "zone" ? .3f : why == "bleed" ? .12f : Mathf.Lerp(.35f, 1f, Mathf.Clamp01(frac * 3.5f)) + (head ? .15f : 0);
                FBHUD.I?.Damaged(k); if (why != "zone" && why != "bleed") FBPlayer.I?.Trauma(Mathf.Lerp(.15f, .6f, Mathf.Clamp01(frac * 3)));
            }
            if (src && src.isPlayer && src != a && !a.civ && a.hp <= 0) FBHUD.I?.KillMark(knockNow);   // confirmação de abate/derrubada
            if (a.civ)
            {
                if (a.hp > 0) { a.GetComponent<FBCivilian>()?.OnHit(); return; }
                a.Die(src); if (src && src.isPlayer) { FBSession.money = Mathf.Max(0, FBSession.money - 150); FBHUD.I?.Toast("CIVIL ATINGIDO", a.Name.ToUpper(), "−$150 · NÃO ATIRE EM INOCENTES", new Color(.86f, .17f, .15f)); }
                FBMatch.Feed($"{(src ? src.Name : "?")} ✖ {a.Name} (civil)", src && src.isPlayer); return;
            }
            if (a.hp <= 0)
            {
                // IE knockdown: nos modos de equipe quem zera a vida cai (se houver aliado de pé para reanimar)
                if (!a.knocked && why != "bleed" && FBMatch.KnockOn && FBMechanics.CanKnock(a, FBSession.mode, FBMatch.HasReviver(a))) { Knock(a, src, head); return; }
                if (a.knocked) { if (why == "bleed" && a.knockedBy) src = a.knockedBy; EndKnock(a); }
                if (src && src != a) src.kills++;
                a.Die(src);
                FBMatch.I?.OnKill(a, src, head, why);
                return;
            }
            if (!a.isPlayer && src && !a.knocked) a.GetComponent<FBBot>()?.Engage(src);
        }

        static Vector3 DamagePoint(FBCharacter a, bool head) { var h = a.Head ? a.Head.position : a.Pos + Vector3.up * 1.6f * a.ScaleK; if (!head) h.y -= .45f * a.ScaleK; return h; }

        // ---- knockdown / revive (IE: bl_PlayerHealthManager + bl_ReviveDetector)
        public static void Knock(FBCharacter a, FBCharacter src, bool head)
        {
            a.koAt = Now; a.koFall = new Vector2(a.velocity.x, a.velocity.z).magnitude > 3.5f ? "FF_SYS_KO_FallRun" : "FF_SYS_KO_Fall";
            FBMechanics.KnockDown(a, Now); a.knockedBy = src && src != a ? src : null;
            a.velocity = new Vector3(0, a.velocity.y, 0); a.StopDance(true); a.StopWaitPose(); a.SetWeaponVisible(false);
            a.prone = true; a.crouch = true; a.sprint = a.shooting = a.aiming = false;
            if (a.isPlayer) { FBPlayer.I?.OnKnocked(); FBAudio.Play2D("ie_knocked", .6f); FBHUD.I?.Damaged(.6f); FBHUD.I?.Center("VOCÊ FOI DERRUBADO", 2); }
            else
            {
                a.GetComponent<FBBot>()?.OnKnocked(); FBAudio.PlayAt("ie_knocked", a.Pos, .7f, 6);
                if (src && src.isPlayer) FBHUD.I?.Toast("INIMIGO DERRUBADO", a.Name.ToUpper(), "FINALIZE ANTES QUE REANIMEM", new Color(1, .3f, .25f));
            }
            FBMatch.Feed($"{(src ? src.Name : "?")} ✖ {a.Name} (derrubado)", (src && src.isPlayer) || a.isPlayer);
        }
        public static void EndKnock(FBCharacter a) { a.knocked = false; a.prone = false; a.crouch = false; a.sys = null; a.revivedBy = null; a.reviveP = 0; }
        public static void CompleteRevive(FBCharacter a, FBCharacter by)
        {
            FBMechanics.ReviveDone(a); EndKnock(a); var gu = FBRegistry.I ? FBRegistry.I.Clip(GetUpClip) : null; a.getUpT = gu ? gu.length - .05f : 0;
            a.SetWeaponVisible(a.weapon != null); a.combat = a.weapon != null;
            if (a.isPlayer) { FBHUD.I?.Center("REANIMADO POR " + by.Name.ToUpper(), 1.6f); FBAudio.Synth("heal"); }
            else { a.GetComponent<FBBot>()?.OnRevived(); if (by.isPlayer) { FBAudio.Synth("heal"); FBHUD.I?.Toast("ALIADO REANIMADO", a.Name.ToUpper(), "VOLTOU COM " + FBMechanics.KNOCK.reviveHealth + " DE VIDA", new Color(.93f, .91f, .86f)); } }
            FBMatch.Feed($"{by.Name} reanimou {a.Name}", by.isPlayer || a.isPlayer);
        }
        // sangramento: 4 de dano a cada 3 s; parado enquanto alguém reanima; sem aliado de pé -> morre
        public static void KnockTick(FBCharacter a, float dt)
        {
            if (a.revivedBy != null && Now - a.reviveTick > .25f) { a.revivedBy = null; a.reviveP = 0; }
            if (a.revivedBy != null) return;
            float d = FBMechanics.BleedStep(a, dt); if (d <= 0) return;
            DamageActor(a, FBMatch.HasReviver(a) ? d : a.hp + 1, false, a.knockedBy, "bleed");
        }
        // clipe do caído (FF): queda nos primeiros 0,8 s, depois de joelhos parado ou andando de joelhos
        public static string GetUpClip => FBRegistry.I && FBRegistry.I.Clip(FBAnimProfile.DownedGetUp) ? FBAnimProfile.DownedGetUp : "FF_SYS_KO_GetUp";   // levantar depois de reanimado
        public static string KoPose(FBCharacter a, bool moving)
        {
            bool H(string c) => FBRegistry.I && FBRegistry.I.Clip(c);
            float t = Now - a.koAt;
            if (t < .8f && H(a.koFall)) return a.koFall;
            if (a.revivedBy != null && Now - a.reviveTick < .35f && H(FBAnimProfile.DownedTreated)) return FBAnimProfile.DownedTreated;   // sendo atendido: deitado de costas (par do IE)
            if (moving && H("FF_SYS_KO_Move")) return "FF_SYS_KO_Move";
            if (moving && H("FF_SYS_KO_Crawl")) return "FF_SYS_KO_Crawl";
            return H("FF_SYS_KO_Idle") ? "FF_SYS_KO_Idle" : moving && H("FF_SYS_Crawl") ? "FF_SYS_Crawl" : "FF_SYS_Prone";
        }
    }
}
