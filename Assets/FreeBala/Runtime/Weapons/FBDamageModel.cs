// r51 — DANO DAS ARMAS NO MODELO DO COUNTER-STRIKE (pedido 31/10: "dano base individual, multiplicadores por região do corpo, dano
// máximo na cabeça por categoria, queda pela distância, penetração por material, proteção, cadência/recuo/precisão por arma, HUD = dano
// registrado"). Antes: a "capa" do Free Fire (cabeça 5,5×, membro 0,75×, queda em degrau) com UMA linha da tabela do FF por classe —
// 14 fuzis diferentes davam o mesmo dano (M4A1 do FF), a AK tirava 14 no peito e 80 na cabeça e o colete/capacete era um desconto fixo
// (45 % / ×0,6) igual para pistola e fuzil de batalha.
// Base técnica (CS:GO/CS2, weapons.txt / items_game.txt), igual para todas as armas:
//   · região: cabeça × HeadshotMultiplier (4 no CS), peito × 1, barriga × 1,25, braços × 1, pernas × 0,75;
//   · distância: dano × RangeModifier ^ (d / 500 unidades) — 500 unidades do Source ≈ 12,7 m;
//   · colete (peito/barriga/braços) e capacete (cabeça; perna nunca é protegida): a vida leva dano × ArmorRatio/2 (a "penetração de
//     colete" de 47 a 100 %) e o colete perde metade do resto; colete gasto = sem proteção;
//   · parede: poder de penetração da arma × resistência do material = espessura máxima atravessada, dano perdido pela espessura.
// Os números são os do CS para a arma equivalente (calibre e categoria), com a vida 100. Distâncias em METROS (1 m = 1,43 unidades do
// mundo, a escala dos personagens). No BR o colete/capacete por nível do FF entra na mesma fórmula e o dano escala pela vida (200).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public static class FBDamageModel
    {
        public static bool On = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-fbdanoantigo") < 0;   // -fbdanoantigo = capa do FF (antes)
        public const float UnitsPerMeter = 1.43f, RangeStep = 12.7f;
        public const float CHEST = 1, STOMACH = 1.25f, ARM = 1, LEG = .75f;

        // dmg = dano base por bala · ap = penetração de colete (vida leva ap do dano com colete) · rm = RangeModifier por 12,7 m ·
        // hs = multiplicador da cabeça · pen = poder de penetração em parede · rpm/pellets = cadência/bagos · acc = dispersão relativa
        // (1 = a da classe) · rec = recuo relativo · move = mobilidade (velocidade do CS ÷ 250)
        public class Stat { public float dmg, ap, rm, hs = 4, pen, rpm, acc = 1, rec = 1, move = 1; public int pellets = 1; public string real; }
        static Stat S(string real, float dmg, float ap, float rm, float rpm, float pen, float acc, float rec, float move, float hs = 4, int pellets = 1)
            => new Stat { real = real, dmg = dmg, ap = ap, rm = rm, rpm = rpm, pen = pen, acc = acc, rec = rec, move = move, hs = hs, pellets = pellets };

        public static readonly Dictionary<string, Stat> Table = new()
        {
            // ---- fuzis 7,62×39 (AK-47 do CS: 36 · 77,5 % · 0,98 · 600 rpm) — cabeça mata com capacete (36×4×0,775 = 112)
            ["ak"] = S("AK-47 7,62×39", 36, .775f, .98f, 600, 2, 1, 1.12f, .86f),
            ["gold_aks"] = S("AKS-47 7,62×39", 36, .775f, .98f, 600, 2, 1.05f, 1.12f, .87f),
            ["rpk"] = S("RPK 7,62×39 (metralhadora)", 36, .78f, .97f, 600, 2, 1.25f, 1.05f, .78f, 3.6f),
            // ---- fuzis 5,45×39 (entre a AK e a M4)
            ["aks74"] = S("AKS-74 5,45×39", 32, .76f, .98f, 650, 2, .95f, .95f, .87f),
            ["ak12"] = S("AK-12 5,45×39", 32, .78f, .98f, 650, 2, .9f, .92f, .87f),
            ["ak12_1"] = S("AK-12 5,45×39", 32, .78f, .98f, 650, 2, .9f, .92f, .87f),
            ["brs_74"] = S("BRS-74 5,45×39", 31, .76f, .98f, 650, 2, .95f, .95f, .87f),
            ["ag14w"] = S("AG14W 5,56", 30, .75f, .98f, 620, 2, .95f, .92f, .88f),
            // ---- fuzis 5,56×45 (M4A4 do CS: 33 · 70 % · 0,97 · 666 rpm) — cabeça sem capacete mata (132), com capacete 92 não
            ["m4"] = S("M4A1 5,56×45", 33, .70f, .97f, 666, 2, .9f, .9f, .9f),
            ["m16"] = S("M16A4 5,56×45 (cano longo)", 33, .70f, .98f, 700, 2, .85f, .95f, .88f),
            ["ar15"] = S("AR-15 5,56×45", 32, .70f, .97f, 666, 2, .9f, .9f, .9f),
            ["ar15_new"] = S("AR-15 5,56×45", 32, .70f, .97f, 666, 2, .9f, .9f, .9f),
            ["ar15_prime"] = S("AR-15 5,56×45", 32, .70f, .97f, 666, 2, .88f, .88f, .9f),
            ["ar_15_campeao"] = S("AR-15 5,56×45", 32, .70f, .97f, 666, 2, .88f, .88f, .9f),
            ["ar15_mini"] = S("AR-15 cano curto", 30, .68f, .95f, 700, 2, 1, .95f, .93f),
            ["ar15_compact"] = S("AR-15 compacta", 30, .68f, .95f, 700, 2, 1, .95f, .93f),
            ["lr_300"] = S("LR-300 5,56×45", 32, .70f, .97f, 666, 2, .88f, .9f, .9f),
            ["acr"] = S("ACR 5,56×45", 32, .72f, .97f, 650, 2, .85f, .88f, .9f),
            ["g36"] = S("G36 5,56×45 (FAMAS do CS: 30 · 70 %)", 31, .72f, .97f, 666, 2, .88f, .9f, .9f),
            ["ia2"] = S("IMBEL IA2 5,56×45", 32, .74f, .98f, 620, 2, .9f, .92f, .89f),
            ["galil_ace"] = S("Galil ACE (Galil AR do CS: 30 · 77,5 %)", 30, .775f, .98f, 666, 2, 1.05f, 1, .87f),
            ["sig"] = S("SIG 5,56×45 (SG 553 do CS: 30 · 100 %)", 30, 1f, .98f, 545, 2, .85f, .95f, .84f),
            // ---- subsônico 9×39 (AS Val): pesado, curto alcance, atravessa colete
            ["asval"] = S("AS Val 9×39 subsônico", 33, .90f, .90f, 800, 2, .95f, .85f, .9f),
            // ---- fuzis de batalha 7,62×51 (SCAR-20/G3SG1 do CS: 80 em semi; aqui automáticos → 45, cadência menor)
            ["scar"] = S("SCAR-H 7,62×51", 42, .82f, .98f, 600, 2.5f, .95f, 1.25f, .85f),
            ["fal"] = S("FAL 7,62×51", 45, .82f, .98f, 550, 2.5f, 1, 1.35f, .84f),
            ["fal_prime"] = S("FAL 7,62×51", 45, .82f, .98f, 550, 2.5f, .95f, 1.32f, .84f),
            ["parafal"] = S("ParaFAL 7,62×51", 44, .82f, .97f, 600, 2.5f, 1.05f, 1.35f, .86f),
            ["parafal_1"] = S("ParaFAL 7,62×51", 44, .82f, .97f, 600, 2.5f, 1.05f, 1.35f, .86f),
            ["sa58_para"] = S("SA58 Para 7,62×51", 44, .82f, .97f, 580, 2.5f, 1.05f, 1.35f, .86f),
            ["ar10"] = S("AR-10 7,62×51", 44, .82f, .98f, 560, 2.5f, .95f, 1.28f, .85f),
            ["g3"] = S("G3 7,62×51", 44, .82f, .98f, 550, 2.5f, 1, 1.32f, .84f),
            ["g3_1"] = S("G3 compacta 7,62×51", 40, .80f, .95f, 600, 2.5f, 1.1f, 1.3f, .88f),
            // ---- submetralhadoras (MP5-SD 27·62,5 %·0,85 · UMP-45 35·65 %·0,75 · P90 26·69 %·0,86 · MAC-10 29·57,5 %·0,80)
            ["mp5"] = S("MP5 9×19", 27, .625f, .85f, 800, 1, .85f, .8f, .97f, 3.8f),
            ["ump"] = S("UMP-45 .45 ACP", 35, .65f, .75f, 666, 1, .9f, .85f, .96f, 3.6f),
            ["uzi"] = S("Uzi 9×19 (MAC-10 do CS)", 28, .575f, .80f, 800, 1, 1.1f, .95f, .98f, 3.6f),
            ["p90"] = S("P90 5,7×28", 26, .69f, .86f, 857, 1, .95f, .78f, .96f, 3.6f),
            ["czskorpion"] = S("Skorpion (MP9 do CS: 26 · 60 %)", 26, .60f, .87f, 857, 1, 1, .85f, .98f, 3.6f),
            ["ar15_9mm"] = S("AR-9 9×19", 27, .60f, .85f, 750, 1, .9f, .8f, .96f, 3.8f),
            // ---- pistolas (Glock 30·47 % · P2000/USP 35·50,5 % · Five-SeveN 32·91 % · Desert Eagle 53·93 % · R8 86·93 %)
            ["g18"] = S("Glock 18 9×19 automática", 26, .47f, .85f, 750, 1, 1.15f, 1.1f, .96f),
            ["beretta_gold"] = S("Beretta 92 9×19", 33, .50f, .88f, 400, 1, .9f, 1, .96f),
            ["1911_gold"] = S("M1911 .45 ACP", 36, .51f, .85f, 380, 1, .9f, 1.1f, .96f),
            ["rc425"] = S("RC425 9×19 compacta", 29, .47f, .85f, 420, 1, 1, .95f, .97f),
            ["desert_eagle"] = S("Desert Eagle .50 AE", 53, .932f, .81f, 267, 2, 1.1f, 1.6f, .92f),
            ["desert_eagle_gold"] = S("Desert Eagle .50 AE", 53, .932f, .81f, 267, 2, 1.1f, 1.6f, .92f),
            ["38"] = S("Revólver .38 SPL", 48, .70f, .85f, 240, 1, .95f, 1.45f, .96f),
            ["pistola_308"] = S("Pistola .308", 62, .90f, .90f, 200, 2, 1.2f, 1.9f, .92f),
            // ---- escopetas (Nova 26×9 · 50 % · 0,70 · XM1014 20×6 · 80 %): cabeça ×3 por bago
            ["remington_870"] = S("Remington 870 calibre 12 (Nova do CS)", 26, .50f, .70f, 68, 1, 1, 1, .88f, 3, 9),
            ["m31"] = S("M31 calibre 12 (XM1014 do CS)", 20, .80f, .70f, 171, 1, 1.1f, 1, .88f, 3, 8),
            // ---- precisão (AWP 115·97,5 %·0,99 · SSG 08 88·85 %·0,98)
            ["barret_50"] = S("Barrett .50 BMG (AWP do CS)", 125, .98f, .99f, 48, 3, 1, 1.4f, .78f),
            ["sniper_hunting"] = S("Rifle de caça .308 (SSG 08 do CS)", 88, .85f, .98f, 48, 2.5f, 1, 1.2f, .92f),
        };
        public static Stat Of(WeaponDef w) => w != null && On && Table.TryGetValue(w.id, out var s) ? s : null;

        // carregamento (GameData.Load): cadência, bagos, precisão, recuo e mobilidade da tabela entram no WeaponDef (o resto do jogo —
        // FBWeaponUser, FBRecoil, bots, loja — lê de lá); o dano por distância exportado do web só sobra para quem não está na tabela
        public static void Apply(WeaponDef d)
        {
            var s = Of(d); if (s == null) return;
            d.rpm = s.rpm; d.pellets = s.pellets; d.spread = .25f * s.acc * (s.pellets > 1 ? 2 : 1); d.recoilScale = s.rec; d.move = s.move;
        }

        // dano base com a queda pela distância (metros)
        public static float Base(Stat s, float meters) => s.dmg * Mathf.Pow(s.rm, Mathf.Max(0, meters) / RangeStep);
        public static float Region(Stat s, string part) => part switch { "head" => s.hs, "stomach" => STOMACH, "arm" => ARM, "leg" => LEG, "limb" => LEG, _ => CHEST };
        // dano inteiro (como o CS): o número que sobe no HUD é exatamente a vida que sai — antes 26,6 tirava 26,6 e o HUD mostrava 27
        public static float Whole(float x) => x <= 0 ? 0 : Mathf.Max(1, Mathf.Round(x));
        public static bool Protected(string part, bool helmet, bool vest) => part == "head" ? helmet : part == "leg" || part == "limb" ? false : vest;

        public struct Result { public float raw, health, armorLost; public string part; public bool armored; }
        // um acerto: dano bruto (região × distância) e o que vai para a vida depois do colete/capacete (fórmula do CS)
        public static Result Hit(Stat s, string part, float worldDist, FBCharacter v)
        {
            float m = worldDist / UnitsPerMeter, raw = Base(s, m) * Region(s, part);
            var r = new Result { raw = raw, health = raw, part = part };
            if (v) ApplyArmor(v, ref r, s.ap);
            return r;
        }
        // colete (FBCharacter.armor 0–100) e capacete (FBCharacter.helmet) dos modos com loja; no BR os itens do FF por nível (vest/helmetFF):
        // nível 2 = colete do CS; nível 1 protege menos, nível 3 mais (vida = 1 − redução × (1 − ap) / 0,5), durabilidade gasta pelo absorvido
        public static void ApplyArmor(FBCharacter v, ref Result r, float ap)
        {
            bool br = FBMatch.I && FBMatch.I.FFDamage;
            if (br)
            {
                var item = r.part == "head" ? v.helmetFF : r.part == "leg" || r.part == "limb" ? null : v.vest;
                if (item == null || item.dur <= 0) return;
                float k = Mathf.Clamp01(item.dur / Mathf.Max(1, item.max)), frac = Mathf.Clamp(1 - item.red * k * (1 - ap) / .5f, .2f, 1);
                float hp = r.raw * frac, abs = r.raw - hp; item.dur = Mathf.Max(0, item.dur - abs * 2); r.health = hp; r.armored = abs > 0; return;
            }
            bool vest = v.armor > 0;
            if (!Protected(r.part, v.helmet, vest)) return;
            float toHealth = r.raw * ap, toArmor = (r.raw - toHealth) * .5f;
            if (r.part != "head")
            {
                if (toArmor > v.armor) { toHealth = r.raw - v.armor / .5f; toArmor = v.armor; }
                v.armor = Mathf.Max(0, v.armor - toArmor); r.armorLost = toArmor;
            }
            r.health = Mathf.Max(0, toHealth); r.armored = true;
        }

        // ---- parede: espessura máxima (m) que poder de penetração 1 atravessa, por material (FBImpactFx.Surf); 0 = para a bala
        public static float Resist(FBImpactFx.Surf m) => m switch
        {
            FBImpactFx.Surf.Wood => .14f, FBImpactFx.Surf.Plaster => .10f, FBImpactFx.Surf.Metal => .035f, FBImpactFx.Surf.Glass => .06f,
            FBImpactFx.Surf.Vegetation => .8f, FBImpactFx.Surf.Concrete => .045f, _ => 0,
        };
        // dano que sobra depois de atravessar 'thick' metros de um material que aguenta até 'maxThick': perde 25 % na entrada e até 60 % pela espessura
        public static float AfterWall(float thick, float maxThick) => maxThick <= 0 ? 0 : Mathf.Clamp01(.75f * (1 - .6f * Mathf.Clamp01(thick / maxThick)));
        public static int Penetrations, PenBlocked;                                                  // telemetria do laboratório
    }
}
