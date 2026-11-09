// PRESET DE AMBIENTE (ScriptableObject, rodada 64). Tudo que é ARTÍSTICO fica aqui, em curvas editáveis; o que é FÍSICO (posição do sol,
// cor do sol pela atmosfera, cor do céu e da luz ambiente) sai do FBSolar/FBAtmosphere. As curvas são pela ELEVAÇÃO DO SOL em graus
// (−20 = noite fechada … 90 = sol a pino): assim o mesmo preset serve para qualquer data, mapa e velocidade do ciclo. As poucas coisas que
// dependem do RELÓGIO (janelas acesas de madrugada) usam curvas por hora.
// Asset padrão: Resources/Env/BuritiroEnv_Padrao.asset (FreeBala > Ambiente > Criar presets). Sem o asset, Padrao() monta o mesmo em código.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    [CreateAssetMenu(menuName = "BURITIRO/Preset de ambiente", fileName = "BuritiroEnv")]
    public class FBEnvPreset : ScriptableObject
    {
        [Header("Lugar e data (Boa Vista)")]
        public float latitude = FBSolar.BoaVistaLat;
        public float longitude = FBSolar.BoaVistaLon;
        public float utcOffset = FBSolar.BoaVistaUtc;
        [Tooltip("Data usada pelo sol e pela lua. 0 = data do computador")] public int year = 2026, month = 2, day = 27;   // 27/02/2026: estiagem; 17:30 sol a 11° (golden hour), 18:00 a 3,6° (pôr do sol), 18:30 a −4° (blue hour), lua 86 % alta às 20:00

        [Header("Atmosfera")]
        [Tooltip("Mie: 1 = céu limpo de referência; estiagem/queimada em Boa Vista ≈ 1,6–2,5")] public float turbidity = 1.5f;
        [Range(.6f, .95f)] public float mieG = .8f;
        [Tooltip("Albedo médio do chão (laterita/asfalto/grama) para a luz rebatida")] public float groundAlbedo = .22f;

        [Header("Adaptação (exposição automática da 'câmera')")]
        [Tooltip("Expoente da adaptação: 0 = sem adaptação (pôr do sol fica escuro como na física), 1 = tudo com o mesmo brilho")] [Range(0, 1)] public float adaptation = .78f;
        [Tooltip("Ganho máximo da adaptação (limita o quanto a noite é 'clareada')")] public float maxAdaptation = 220f;
        [Tooltip("Tempo (s) da adaptação suave quando o horário pula")] public float adaptationSeconds = 1.6f;

        [Header("Curvas por elevação do sol (graus)")]
        public AnimationCurve sunIntensity = Flat(1);          // multiplica a força física do sol
        public AnimationCurve skyAmbient = Flat(1);            // luz ambiente e neblina RELATIVAS ao dia aprovado do mapa (1 = meio-dia); a cor vem do céu físico
        public AnimationCurve exposure = Flat(0);              // pós-exposição (EV)
        public AnimationCurve contrast = Flat(8);
        public AnimationCurve saturation = Flat(6);
        public AnimationCurve whiteBalance = Flat(0);          // temperatura do balanço de branco (URP, −100…100: + = mais quente)
        public AnimationCurve tint = Flat(0);
        public AnimationCurve bloom = Flat(.3f);
        public AnimationCurve shadowStrength = Flat(.92f);
        public AnimationCurve fogDensity = Flat(1);            // multiplica a distância de neblina do mapa (maior = mais neblina)
        public AnimationCurve volumetric = Flat(1);            // multiplica a densidade da névoa volumétrica (raios de sol/postes)
        public AnimationCurve night = Flat(0);                 // 0 dia … 1 noite: postes, faróis, janelas, bots (o "n" do FBWeather)
        public AnimationCurve skyBrightness = Flat(1);         // multiplica o céu físico (desenho do skybox)

        [Header("Noite")]
        [Tooltip("Piso de luz ambiente da noite (visibilidade do combate) — azul do céu urbano")] public Color nightAmbientSky = new(.24f, .29f, .42f);
        public Color nightAmbientEquator = new(.17f, .19f, .27f), nightAmbientGround = new(.07f, .07f, .09f);
        [Range(0, 1)] public float nightAmbientIntensity = .4f;
        [Tooltip("Força da lua cheia no alto (luz direcional)")] public float moonIntensity = .16f;
        public Color moonColor = new(.62f, .72f, 1f);
        [Tooltip("Poluição luminosa (0 = sertão, 1 = centro da cidade): apaga estrelas e alaranja o horizonte")] [Range(0, 1)] public float lightPollution = .55f;

        [Header("Curvas por hora do relógio (0–24)")]
        public AnimationCurve windowsOn = Flat(1);             // fração das janelas acesas (madrugada: quase todas apagadas)

        [Serializable] public class MapOverride
        {
            public string mapId;
            [Tooltip("Gira o norte do mapa (graus) se ele não segue o padrão norte = −Z")] public float northYaw;
            public float exposureBias, fogMul = 1, sunMul = 1, ambientMul = 1, volumetricMul = 1, lightPollution = -1;
            [Tooltip("Hora fixa do mapa (−1 = a do menu)")] public float fixedHour = -1;
        }
        [Header("Ajustes por mapa")] public List<MapOverride> maps = new();

        public MapOverride For(string mapId) { foreach (var m in maps) if (m != null && m.mapId == mapId) return m; return null; }

        static AnimationCurve Flat(float v) => new(new Keyframe(-90, v), new Keyframe(90, v));
        static AnimationCurve K(params float[] kv)
        {
            var c = new AnimationCurve(); for (int i = 0; i + 1 < kv.Length; i += 2) c.AddKey(new Keyframe(kv[i], kv[i + 1]));
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0); return c;
        }

        // valores iniciais calibrados (rodada 64): dia igual ao aprovado (contraste 8, saturação 6, sombra .9), golden hour quente sem estourar,
        // blue hour fria, noite escura com piso de luz para o combate.
        public static FBEnvPreset Padrao()
        {
            var p = CreateInstance<FBEnvPreset>(); p.name = "BuritiroEnv_Padrao"; p.Fill(); return p;
        }
        public void Fill()
        {
            sunIntensity = K(-2, 0, 0, .55f, 4, .8f, 12, .95f, 30, 1, 90, 1);
            skyAmbient = K(-90, 0, -13, .03f, -9, .08f, -6, .17f, -3, .3f, 0, .48f, 4, .62f, 10, .78f, 20, .9f, 40, .97f, 90, 1);   // r64b: luz ambiente RELATIVA ao dia aprovado
            exposure = K(-18, -.35f, -8, -.15f, -3, 0, 3, .08f, 10, .05f, 25, 0, 90, 0);
            contrast = K(-18, 12, -6, 10, 0, 10, 6, 10, 15, 9, 30, 8, 90, 8);
            saturation = K(-18, -22, -8, -12, -3, -2, 2, 5, 8, 6, 20, 6, 90, 6);
            whiteBalance = K(-18, -8, -8, -12, -4, -8, 0, 2, 4, 4, 10, 3, 20, 1, 35, 0, 90, 0);   // r64b: o sol físico já é laranja (17:30 = 1/.67/.34)
            tint = K(-18, 0, -6, 4, -2, 6, 2, 3, 10, 0, 90, 0);
            bloom = K(-18, .85f, -6, .7f, -1, .5f, 3, .5f, 10, .38f, 25, .3f, 90, .3f);
            shadowStrength = K(-6, .55f, -1, .6f, 2, .8f, 8, .9f, 90, .92f);
            fogDensity = K(-18, 1.25f, -6, 1.15f, 0, 1.35f, 6, 1.2f, 20, 1, 90, .95f);
            volumetric = K(-18, 3.4f, -6, 2.6f, 0, 2.6f, 4, 2.2f, 12, 1.4f, 30, 1, 90, .9f);
            night = K(8, 0, 3, .08f, 0, .3f, -2, .5f, -5, .75f, -9, .93f, -13, 1, -90, 1);
            skyBrightness = Flat(1);
            windowsOn = K(0, .45f, 1.5f, .18f, 4, .08f, 5.5f, .25f, 7, 0, 17, 0, 18.5f, .85f, 21, 1, 23, .75f, 24, .45f);
            maps = new List<MapOverride>();
        }
    }
}
