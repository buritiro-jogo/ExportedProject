// SOM DO HELICÓPTERO (retorno do usuário 20/10: "áudio extremamente baixo ou inexistente"). Antes: o laço ie_heli_loop tocava pelo
// FBAudio.SetLoop a volume 0,2–0,26 com queda de fonte comum (ref. 30–40 m, máx. 400 m) — o heli voa a 50–130 m de altura/raio,
// então chegava a ~6 % do volume. Agora a fonte vive NO helicóptero:
//  · volume cheio (o laço já é normalizado), queda lenta de fonte grande (ref. 45 m, fator 0,32, alcance 1,4 km: ouve-se chegando);
//  · ar: passa-baixa pela distância (22 kHz perto -> ~0,9 kHz a 700 m: de longe só o "tum-tum" grave das pás);
//  · Doppler moderado (passa rápido), fonte larga (spread), prioridade alta (não é cortado por tiros/passos), rotação = tom (rpm).
// RODADA 38 (usuário: "volume excessivamente alto"): o ajuste acima passou do ponto. Medido (RMS de curto prazo, p90): laço -10,7 dB;
// a volume 0,9 com queda de referência 45 m / fator 0,32 chegava a -14 dB a 100 m e -18 dB a 200 m — acima de um tiro distante
// (shot_far -21 dB) e ~22 dB acima do ambiente da cidade (-39 dB). Agora MIX 0,45 e queda ref. 30 m / fator 0,78 / 1 km:
// ~-18 dB passando a 30 m, ~-27 dB a 100 m, ~-34 dB a 250 m — sempre abaixo dos tiros e acima da cama de ambiente.
using UnityEngine;

namespace FreeBala
{
    public class FBHeliSound : MonoBehaviour
    {
        AudioSource rotor; AudioLowPassFilter lp; public float vol = .9f, rpm = 1; const float MIX = .45f;
        public static FBHeliSound Attach(GameObject g, float vol)
        {
            // [parceiro] OrNull: no Editor o GetComponent devolve um "nulo falso" que o ?? não reconhece
            if (!g) return null; var h = g.GetComponent<FBHeliSound>().OrNull() ?? g.AddComponent<FBHeliSound>(); h.vol = vol; return h;
        }
        void Start()
        {
            var c = FBAudio.Clip("ie_heli_loop"); if (!c) { enabled = false; return; }
            rotor = gameObject.AddComponent<AudioSource>(); rotor.clip = c; rotor.loop = true; rotor.playOnAwake = false; rotor.spatialBlend = 1; rotor.dopplerLevel = .5f; rotor.spread = 70; rotor.priority = 16;
            rotor.rolloffMode = AudioRolloffMode.Custom; rotor.maxDistance = 1000; rotor.SetCustomCurve(AudioSourceCurveType.CustomRolloff, FBAudio.RolloffCurve(30, .78f, 1000));
            rotor.outputAudioMixerGroup = FBAudio.Group(false); rotor.volume = 0; rotor.time = Random.value * c.length * .9f; rotor.Play();
            lp = gameObject.AddComponent<AudioLowPassFilter>(); lp.cutoffFrequency = 22000; lp.lowpassResonanceQ = 1;
        }
        // [parceiro] v1 (thread de física): tempo do Update medido pelo FBTempo (desligado no jogo: só um teste de bool; o FBTesteExe liga)
        static readonly int __fbt_Update = FBTempo.Id("FBHeliSound.Update");
        void Update()
        {
            if (!FBTempo.On) { Update__fbt(); return; }
            long t0 = FBTempo.T0(); Update__fbt(); FBTempo.Soma(__fbt_Update, t0);
        }
        void Update__fbt()
        {
            if (!rotor) return; var ear = FBAudio.Ear; float d = ear ? Vector3.Distance(ear.position, transform.position) : 120;
            float target = Mathf.Clamp01(vol * MIX * FBAudio.VehVol);
            rotor.volume = Mathf.MoveTowards(rotor.volume, target, Time.deltaTime * 1.5f);
            rotor.pitch = Mathf.Lerp(.68f, 1f, Mathf.Clamp01(rpm));
            if (lp) lp.cutoffFrequency = 22000f * Mathf.Pow(900f / 22000f, Mathf.Clamp01(Mathf.InverseLerp(40, 700, d)));
            if (!rotor.isPlaying && target > 0) rotor.Play();
        }
        void OnDisable() { if (rotor) rotor.Pause(); }
        void OnEnable() { if (rotor) rotor.UnPause(); }
    }
}
