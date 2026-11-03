// ACÚSTICA DO PAREDÃO (som que sai das caixas, com profundidade): em vez de um AudioSource 3D com filtro "no fone", cada paredão tem
//  · FONTE ÚNICA (FBAcTap): um AudioSource 2D toca a faixa (decodifica uma vez só); o filtro copia o som para um anel de 2,7 s (já com o
//    caráter de paredão: grave reforçado, saturação no volume alto, medidores para LED/VU) e silencia a própria saída;
//  · VOZES (FBAcVoice): cada caminho do som é uma voz que lê o anel com o ATRASO real do caminho (distância/343 m/s; o atraso muda
//    suave = Doppler de verdade), ganho pela distância, filtro passa-baixa (ar, prédio na frente, atrás do cone das caixas, porta-malas)
//    e posição no espaço da cabeça (ILD + ITD + sombra da cabeça no ouvido de longe + atrás mais abafado): coluna da esquerda e da
//    direita do paredão (estéreo largo de perto, um ponto de longe) e ECOS nas fachadas (fontes-imagem achadas por raio: o eco chega
//    da parede, atrasado);
//  · REVERB DA RUA (FBAcReverb): rede de 8 atrasos (FDN, RT60 ~1,7 s, agudos morrem antes) alimentada pelo som que chega — de longe o
//    reverberado domina o direto (sensação de distância), de perto quase seco.
// Sincronia: anel indexado pelo relógio do mixer (AudioSettings.dspTime·taxa), igual para a fonte e as vozes no mesmo bloco.
// WebGL não tem OnAudioFilterRead: lá o FBParedao usa o AudioSource 3D simples.
using System;
using System.Threading;
using UnityEngine;

namespace FreeBala
{
    public class FBAcTap : MonoBehaviour
    {
        public const int N = 1 << 17, MASK = N - 1;
        public readonly float[] L = new float[N], R = new float[N];
        public volatile float drive, bass, level;
        public long end = -1, begin = long.MaxValue;                     // [begin, end): quadros válidos (relógio do mixer)
        int sr; double b0, b1, b2, a1, a2; readonly double[] z1 = new double[2], z2 = new double[2], lp = new double[2];
        void Awake() { sr = AudioSettings.outputSampleRate; Shelf(90, 5.5); }
        void Shelf(double f, double dB)
        {
            double A = Math.Pow(10, dB / 40), w = 2 * Math.PI * f / sr, cs = Math.Cos(w), sn = Math.Sin(w), al = sn / 2 * Math.Sqrt(2), sq = 2 * Math.Sqrt(A) * al, a0 = (A + 1) + (A - 1) * cs + sq;
            b0 = A * ((A + 1) - (A - 1) * cs + sq) / a0; b1 = 2 * A * ((A - 1) - (A + 1) * cs) / a0; b2 = A * ((A + 1) - (A - 1) * cs - sq) / a0; a1 = -2 * ((A - 1) + (A + 1) * cs) / a0; a2 = ((A + 1) + (A - 1) * cs - sq) / a0;
        }
        void OnAudioFilterRead(float[] data, int ch)
        {
            int n = data.Length / Math.Max(1, ch); long F = (long)Math.Round(AudioSettings.dspTime * sr); long e = Interlocked.Read(ref end);
            if (e < 0 || F > e + 2 || F < e - n) begin = F;                     // voltou de pausa / pulo: o que está antes não vale
            float k = drive * 36; double eb = 0, el = 0, c = 1 - Math.Exp(-2 * Math.PI * 110 / sr);
            for (int i = 0; i < n; i++)
            {
                int o = i * ch; double x0 = data[o], x1 = ch > 1 ? data[o + 1] : x0;
                for (int j = 0; j < 2; j++)
                {
                    double x = j == 0 ? x0 : x1, y = b0 * x + z1[j]; z1[j] = b1 * x - a1 * y + z2[j]; z2[j] = b2 * x - a2 * y;
                    if (k > .4f) y = (1 + k) * y / (1 + k * Math.Abs(y));
                    lp[j] += (y - lp[j]) * c; eb += lp[j] * lp[j]; el += y * y;
                    if (j == 0) L[(F + i) & MASK] = (float)y; else R[(F + i) & MASK] = (float)y;
                }
                for (int j = 0; j < ch; j++) data[o + j] = 0;
            }
            Interlocked.Exchange(ref end, F + n);
            bass = Mathf.Clamp01((float)Math.Sqrt(eb / (2 * Math.Max(1, n))) * 3.2f); level = Mathf.Clamp01((float)Math.Sqrt(el / (2 * Math.Max(1, n))) * 2.6f);
        }
        // limites do trecho válido, lidos uma vez por bloco (não por amostra)
        public void Bounds(out long b, out long e) { e = Interlocked.Read(ref end); b = begin; }
        // lê do anel (interpolado); fora do trecho válido = silêncio
        public float Read(double pos, int chan) { Bounds(out var b, out var e); return Read(pos, chan, b, e); }
        public float Read(double pos, int chan, long begin, long e)
        {
            long p0 = (long)Math.Floor(pos);
            if (p0 < begin || p0 + 1 >= e || p0 <= e - N + 2048) return 0;
            float f = (float)(pos - p0); int a = (int)(p0 & MASK), b = (int)((p0 + 1) & MASK);
            float l = L[a] + (L[b] - L[a]) * f, r = R[a] + (R[b] - R[a]) * f;
            return chan == 0 ? l : chan == 1 ? r : (l + r) * .5f;
        }
        public static AudioClip Silence { get { if (!sil) { int sr = AudioSettings.outputSampleRate; sil = AudioClip.Create("fb_silencio", sr / 2, 1, sr, false); sil.SetData(new float[sr / 2], 0); } return sil; } }
        static AudioClip sil;
    }

    // uma voz = um caminho do som (direto de uma coluna, ou um eco), com atraso, ganho, passa-baixa e posição binaural
    public class FBAcVoice : MonoBehaviour
    {
        public FBAcTap tap; public int chan = 2;
        public volatile float gain, cutoff = 20000, delay = .12f, pan, behind, width = .78f;   // alvos (thread principal)
        float g0, c0 = 20000, p0, bh0; double D0 = -1; int sr; double y1, y2, x1, x2; float farL; readonly float[] itd = new float[128]; int iw; float fadeIn = 1;
        void Awake() { sr = AudioSettings.outputSampleRate; }
        public static FBAcVoice Make(Transform parent, FBAcTap tap, int chan, string name)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); var s = g.AddComponent<AudioSource>();
            s.clip = FBAcTap.Silence; s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; s.priority = 2; s.volume = 1; s.dopplerLevel = 0; s.outputAudioMixerGroup = FBAudio.Group(false);
            var v = g.AddComponent<FBAcVoice>(); v.tap = tap; v.chan = chan; s.Play(); return v;
        }
        void OnAudioFilterRead(float[] data, int ch)
        {
            int n = data.Length / Math.Max(1, ch); var T = tap;
            if (!T || ch < 1) { Array.Clear(data, 0, data.Length); return; }
            long F = (long)Math.Round(AudioSettings.dspTime * sr);
            double Dt = Math.Max(256, delay * sr);
            if (D0 < 0 || Math.Abs(Dt - D0) > .25 * sr) { D0 = Dt; fadeIn = 0; }                 // pulo grande: entra sem estalo
            double D1 = D0 + Math.Max(-.08 * n, Math.Min(.08 * n, Dt - D0));                      // Doppler limitado a 8 %
            float g1 = gain, p1 = Mathf.Clamp(pan, -1, 1), bh1 = Mathf.Clamp01(behind), c1 = Mathf.Lerp(c0, Mathf.Clamp(cutoff * (1 - .45f * bh1), 120, Mathf.Min(20000, sr * .45f)), .35f);   // abaixo de Nyquist (celular a 22/24 kHz)
            // passa-baixa de 2ª ordem (RBJ, Q 0,707) com coeficientes do bloco
            double w0 = 2 * Math.PI * c1 / sr, cs = Math.Cos(w0), al = Math.Sin(w0) / (2 * .7071), a0 = 1 + al;
            double fb0 = (1 - cs) / 2 / a0, fb1 = (1 - cs) / a0, fa1 = -2 * cs / a0, fa2 = (1 - al) / a0;
            float wd = width; double farC = 1 - Math.Exp(-2 * Math.PI * Mathf.Lerp(18000, 2600, Mathf.Abs(p1)) / sr); T.Bounds(out var tb, out var te);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 1f) / n; double D = D0 + (D1 - D0) * t;
                float x = T.Read(F + i - D, chan, tb, te) * (g0 + (g1 - g0) * t);
                if (fadeIn < 1) { x *= fadeIn; fadeIn = Mathf.Min(1, fadeIn + 1f / 256); }
                double y = fb0 * x + fb1 * x1 + fb0 * x2 - fa1 * y1 - fa2 * y2; x2 = x1; x1 = x; y2 = y1; y1 = y;
                float s = (float)y, p = p0 + (p1 - p0) * t;
                // cabeça: ganho por lado, atraso interaural no ouvido de longe (até 0,66 ms) e sombra (passa-baixa) nele
                float gR = Mathf.Sqrt(.5f * (1 + wd * p)), gL = Mathf.Sqrt(.5f * (1 - wd * p));
                itd[iw & 127] = s; float dly = Mathf.Abs(p) * .00066f * sr; int di = (int)dly; float df = dly - di;
                float late = itd[(iw - di) & 127] * (1 - df) + itd[(iw - di - 1) & 127] * df; iw++;
                farL += (float)((late - farL) * farC);
                float near = s, far = farL;
                float outL = p >= 0 ? far * gL : near * gL, outR = p >= 0 ? near * gR : far * gR;
                if (outL > .8f || outL < -.8f) outL = Mathf.Sign(outL) * (.8f + .2f * (float)Math.Tanh((Mathf.Abs(outL) - .8f) * 5));   // limitador suave (sem estalo de clip)
                if (outR > .8f || outR < -.8f) outR = Mathf.Sign(outR) * (.8f + .2f * (float)Math.Tanh((Mathf.Abs(outR) - .8f) * 5));
                int o = i * ch; data[o] = outL; if (ch > 1) data[o + 1] = outR; for (int j = 2; j < ch; j++) data[o + j] = 0;
            }
            D0 = D1; g0 = g1; c0 = c1; p0 = p1; bh0 = bh1;
        }
    }

    // reverb da rua: FDN de 8 linhas (Hadamard), amortecimento de agudos, saída estéreo decorrelacionada
    public class FBAcReverb : MonoBehaviour
    {
        public FBAcTap tap; public volatile float send, delay = .14f, cutoff = 6000, rt60 = 1.7f;
        static readonly int[] LEN = { 1427, 1601, 1867, 2053, 2251, 2399, 2617, 2797 };
        readonly float[][] buf = new float[8][]; readonly int[] pos = new int[8]; readonly float[] damp = new float[8]; float[] gfb = new float[8];
        int sr; double D0 = -1; float s0, c0 = 6000, lpIn; float rtNow = -1; float scale = 1;
        void Awake()
        {
            sr = AudioSettings.outputSampleRate; scale = sr / 48000f;
            for (int i = 0; i < 8; i++) buf[i] = new float[Mathf.Max(64, Mathf.RoundToInt(LEN[i] * scale))];
        }
        public static FBAcReverb Make(Transform parent, FBAcTap tap)
        {
            var g = new GameObject("paredao_reverb"); g.transform.SetParent(parent, false); var s = g.AddComponent<AudioSource>();
            s.clip = FBAcTap.Silence; s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; s.priority = 3; s.volume = 1; s.outputAudioMixerGroup = FBAudio.Group(false);
            var r = g.AddComponent<FBAcReverb>(); r.tap = tap; s.Play(); return r;
        }
        void OnAudioFilterRead(float[] data, int ch)
        {
            int n = data.Length / Math.Max(1, ch); var T = tap; if (!T) { Array.Clear(data, 0, data.Length); return; }
            if (rtNow != rt60) { rtNow = rt60; for (int i = 0; i < 8; i++) gfb[i] = Mathf.Pow(10, -3f * buf[i].Length / (Mathf.Max(.2f, rtNow) * sr)); }
            long F = (long)Math.Round(AudioSettings.dspTime * sr); double Dt = Math.Max(256, delay * sr);
            if (D0 < 0 || Math.Abs(Dt - D0) > .25 * sr) D0 = Dt; double D1 = D0 + Math.Max(-.08 * n, Math.Min(.08 * n, Dt - D0));
            float s1 = send, c1 = Mathf.Lerp(c0, Mathf.Clamp(cutoff, 200, 12000), .3f); float kin = 1 - Mathf.Exp(-2 * Mathf.PI * c1 / sr), kd = 1 - Mathf.Exp(-2 * Mathf.PI * 3600 / sr);
            Span<float> v = stackalloc float[8]; T.Bounds(out var tb, out var te);
            for (int i = 0; i < n; i++)
            {
                float t = (i + 1f) / n; float x = T.Read(F + i - (D0 + (D1 - D0) * t), 2, tb, te) * (s0 + (s1 - s0) * t);
                lpIn += (x - lpIn) * kin; x = lpIn;
                for (int k = 0; k < 8; k++) { var b = buf[k]; v[k] = b[pos[k]]; }
                // Hadamard 8x8 (rápida), normalizada
                for (int h = 1; h < 8; h <<= 1) for (int a = 0; a < 8; a += h << 1) for (int b2 = a; b2 < a + h; b2++) { float p = v[b2], q = v[b2 + h]; v[b2] = p + q; v[b2 + h] = p - q; }
                for (int k = 0; k < 8; k++)
                {
                    float y = v[k] * .35355f * gfb[k]; damp[k] += (y - damp[k]) * kd;                 // agudos morrem antes
                    var b = buf[k]; b[pos[k]] = damp[k] + x * ((k & 1) == 0 ? .5f : -.5f); if (++pos[k] >= b.Length) pos[k] = 0;
                }
                float oL = (v[0] - v[2] + v[4] - v[6]) * .25f, oR = (v[1] - v[3] + v[5] - v[7]) * .25f;
                int o = i * ch; data[o] = oL; if (ch > 1) data[o + 1] = oR; for (int j = 2; j < ch; j++) data[o + j] = 0;
            }
            D0 = D1; s0 = s1; c0 = c1;
        }
    }
}
