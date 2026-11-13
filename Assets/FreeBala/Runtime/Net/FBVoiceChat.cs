// r69 (13/11/2026, pedido: "corrija a parte de ligar o microfone para se comunicar com os amigos"): CHAT DE VOZ.
// Antes não existia: a entrada "mic" do controle não fazia nada e a tecla M é o mapa. Agora:
//  - CAPS LOCK (ou o botão "MIC" na esquerda da tela, que serve no celular) liga/desliga o microfone. Microfone aberto com porta de
//    ruído: só manda pacote quando há voz (RMS > 0,012, segura 0,35 s depois que a voz cai), então ligado em silêncio não gasta rede.
//  - Captura: Microphone do Unity na taxa que o aparelho aceita → reamostrada para 16 kHz mono → pacotes de 100 ms em IMA ADPCM
//    (4 bits/amostra, 805 bytes → ~1,1 KB em base64; ~11 KB/s só enquanto fala).
//  - Canal: dentro da sala (FBNet.InRoom) vai como evento "vc" da partida; o servidor repassa só para o próprio time no TIMES e no 2 × 2
//    e para a sala toda no rolê. Fora da partida, no squad, vai pelo lobby ("pvc") para os outros do squad.
//  - Reprodução: uma fonte 2D por quem fala, buffer de 150 ms contra variação da rede (máximo 500 ms; acima disso descarta o mais
//    velho), reamostrada 16 kHz → saída do PC dentro do OnAudioFilterRead. Volume = "voz" dos ajustes.
//  - Android pede a permissão de microfone na primeira vez; WebGL não tem microfone (o botão some).
// Sem fone de ouvido o microfone do amigo pode pegar a sua voz saindo da caixa de som (eco): o jogo não faz cancelamento de eco.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FreeBala
{
    public class FBVoiceChat : MonoBehaviour
    {
        public static FBVoiceChat I { get; private set; }
        public const int SR = 16000, PK = 1600;                 // 16 kHz, 100 ms por pacote
        const float GATE = .012f, HOLD = .35f;
        public bool on; public string device = ""; public string erro = "";
        public float level;                                     // nível do próprio microfone (0..1, para a barrinha)
        public bool falando;                                    // porta de ruído aberta agora

        static readonly ConcurrentQueue<(string key, string name, int q, string d)> inbox = new();

        public static FBVoiceChat Ensure()
        {
            if (I) return I; var g = new GameObject("FB_VoiceChat"); DontDestroyOnLoad(g); return g.AddComponent<FBVoiceChat>();
        }
        void Awake() { if (I && I != this) { Destroy(gameObject); return; } I = this; }

        // ------------------------------------------------------------ chegada (podem vir de outra thread: só enfileira)
        public static void OnRoomVoice(int id, string name, int q, string d) { if (!string.IsNullOrEmpty(d)) inbox.Enqueue(("r" + id, name, q, d)); }
        public static void OnLobbyVoice(string from, string name, int q, string d) { if (!string.IsNullOrEmpty(d)) inbox.Enqueue(("p" + from, string.IsNullOrEmpty(name) ? FBLobbyNet.NameOf(from) : name, q, d)); }

        // canal disponível agora
        public static bool InMatchRoom => FBNet.I && FBNet.I.InRoom;
        public static bool InSquad => FBLobbyNet.I && FBLobbyNet.I.status == FBLobbyNet.St.On && FBLobbyNet.I.InParty;
        public static bool Supported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        // ------------------------------------------------------------ ligar / desligar
        public void Toggle() { if (on) Stop(); else StartMic(); if (FBLobbyNet.I) FBLobbyNet.I.version++; }   // version: a tela do squad redesenha o botão
        public void StartMic()
        {
            if (!Supported || on) return; erro = "";
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            { UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone); erro = "PERMITA O MICROFONE E TOQUE DE NOVO"; return; }
#endif
#if UNITY_IOS && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone)) { Application.RequestUserAuthorization(UserAuthorization.Microphone); erro = "PERMITA O MICROFONE E TOQUE DE NOVO"; return; }
#endif
#if !UNITY_WEBGL || UNITY_EDITOR
            var devs = Microphone.devices; if (devs == null || devs.Length == 0) { erro = "NENHUM MICROFONE ENCONTRADO"; return; }
            device = devs[0];
            Microphone.GetDeviceCaps(device, out int mn, out int mx);
            micRate = mn == 0 && mx == 0 ? SR : Mathf.Clamp(SR, mn, mx);
            mic = Microphone.Start(device, true, 1, micRate);
            if (!mic) { erro = "MICROFONE NÃO ABRIU"; return; }
            micRate = mic.frequency; lastPos = 0; ph = 0; inBuf.Clear(); out16.Clear(); hold = 0; seq = 0; enc = default;
            on = true; Debug.Log($"[FreeBala][voz] microfone ligado: {device} {micRate} Hz");
#endif
        }
        public void Stop()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (on && Microphone.IsRecording(device)) Microphone.End(device);
#endif
            on = false; falando = false; level = 0; mic = null;
        }
        void OnDestroy() { Stop(); if (I == this) I = null; }
        void OnApplicationPause(bool p) { if (p && on) Stop(); }

        // ------------------------------------------------------------ captura
        AudioClip mic; int micRate, lastPos; double ph; float hold; int seq; Adpcm.State enc;
        readonly List<float> inBuf = new(), out16 = new();
        float[] chunk = new float[0];

        void Capture()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (!on || !mic) return;
            if (!Microphone.IsRecording(device)) { Stop(); erro = "MICROFONE DESCONECTADO"; return; }
            int pos = Microphone.GetPosition(device); if (pos < 0) return;
            int n = pos - lastPos; if (n < 0) n += mic.samples; if (n < micRate / 50) return;    // junta pelo menos 20 ms
            if (chunk.Length != n) chunk = new float[n];
            mic.GetData(chunk, lastPos);                                                         // clip em loop: o GetData dá a volta no fim
            lastPos = (lastPos + n) % mic.samples;
            for (int i = 0; i < n; i++) inBuf.Add(chunk[i]);
            double step = micRate / (double)SR;
            while (ph + 1 < inBuf.Count) { int i = (int)ph; float f = (float)(ph - i); out16.Add(inBuf[i] + (inBuf[i + 1] - inBuf[i]) * f); ph += step; }
            int drop = Mathf.Min((int)ph, inBuf.Count); if (drop > 0) { inBuf.RemoveRange(0, drop); ph -= drop; }

            while (out16.Count >= PK)
            {
                float s2 = 0; for (int i = 0; i < PK; i++) s2 += out16[i] * out16[i];
                float rms = Mathf.Sqrt(s2 / PK); level = Mathf.Lerp(level, Mathf.Clamp01(rms * 8), .5f);
                if (rms > GATE) hold = HOLD; else hold -= PK / (float)SR;
                falando = hold > 0;
                if (falando)
                {
                    var pcm = new short[PK]; for (int i = 0; i < PK; i++) pcm[i] = (short)Mathf.Clamp(Mathf.RoundToInt(out16[i] * 32767f), -32768, 32767);
                    var d = Convert.ToBase64String(Adpcm.Encode(pcm, ref enc)); seq = (seq + 1) & 0xffff;
                    if (InMatchRoom) FBNet.I.SendMic(seq, d); else if (InSquad) FBLobbyNet.I.SendVoice(seq, d);
                }
                out16.RemoveRange(0, PK);
            }
#endif
        }

        // ------------------------------------------------------------ reprodução
        public class Peer : MonoBehaviour
        {
            public string key, nome; public float lastT;
            readonly float[] ring = new float[SR];               // 1 s
            int w, r, count; bool primed; double frac; float stepOut; readonly object lk = new();
            public AudioSource src;
            public void Init(string k, string n)
            {
                key = k; nome = n; stepOut = SR / (float)AudioSettings.outputSampleRate;
                src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0; src.loop = true; src.playOnAwake = false; src.priority = 0;
                var one = new float[4096]; for (int i = 0; i < one.Length; i++) one[i] = 1;
                var c = AudioClip.Create("voz_um", one.Length, 1, AudioSettings.outputSampleRate, false); c.SetData(one, 0); src.clip = c;
                src.volume = FBSession.voice; src.Play();
            }
            public void Push(short[] pcm, int n)
            {
                lock (lk)
                {
                    for (int i = 0; i < n; i++) { ring[w] = pcm[i] / 32768f; w = (w + 1) % ring.Length; if (count < ring.Length) count++; else r = (r + 1) % ring.Length; }
                    int max = SR / 2; if (count > max) { int cut = count - SR / 5; r = (r + cut) % ring.Length; count -= cut; }   // atrasou demais: volta a 200 ms
                }
                lastT = Time.unscaledTime;
            }
            public bool Ativo => Time.unscaledTime - lastT < .3f;
            void OnAudioFilterRead(float[] data, int ch)
            {
                lock (lk)
                {
                    if (!primed && count >= SR * 15 / 100) primed = true;
                    for (int i = 0; i < data.Length; i += ch)
                    {
                        float v = 0;
                        if (primed && count > 1)
                        {
                            float a = ring[r], b = ring[(r + 1) % ring.Length]; v = a + (b - a) * (float)frac;
                            frac += stepOut; while (frac >= 1 && count > 1) { frac -= 1; r = (r + 1) % ring.Length; count--; }
                        }
                        else primed = false;
                        for (int c = 0; c < ch; c++) data[i + c] *= v;
                    }
                }
            }
        }
        readonly Dictionary<string, Peer> peers = new();

        void Receive()
        {
            int guard = 0;
            while (guard++ < 64 && inbox.TryDequeue(out var m))
            {
                short[] pcm; try { pcm = Adpcm.Decode(Convert.FromBase64String(m.d)); } catch { continue; }
                if (pcm == null || pcm.Length == 0) continue;
                if (!peers.TryGetValue(m.key, out var p) || !p)
                {
                    var g = new GameObject("voz_" + m.key); g.transform.SetParent(transform, false); p = g.AddComponent<Peer>(); p.Init(m.key, m.name); peers[m.key] = p;
                }
                if (!string.IsNullOrEmpty(m.name)) p.nome = m.name;
                p.Push(pcm, pcm.Length);
            }
            List<string> velhos = null;
            foreach (var kv in peers)
            {
                if (!kv.Value) { (velhos ??= new()).Add(kv.Key); continue; }
                kv.Value.src.volume = FBSession.voice;
                if (Time.unscaledTime - kv.Value.lastT > 30) { Destroy(kv.Value.gameObject); (velhos ??= new()).Add(kv.Key); }
            }
            if (velhos != null) foreach (var k in velhos) peers.Remove(k);
        }

        // ------------------------------------------------------------ teclas e tela
        static bool Digitando()
        {
            var es = EventSystem.current; var go = es ? es.currentSelectedGameObject : null;
            return go && go.TryGetComponent<InputField>(out var f) && f.isFocused;
        }
        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (Supported && kb != null && kb.capsLockKey.wasPressedThisFrame && !Digitando() && (on || InMatchRoom || InSquad)) Toggle();
            Capture(); Receive(); Ui();
        }

        Canvas cv; RectTransform pill; Image pillBg, dot; Text pillT, talkT; float uiT;
        void Ui()
        {
            // na partida: pílula clicável na esquerda (serve no celular); no menu o botão fica na própria tela do SQUAD
            bool partida = FBMatch.I, show = Supported && partida && (on || InMatchRoom);
            if (!cv)
            {
                cv = FBUI.Canvas("FB_VozUI", 900); DontDestroyOnLoad(cv.gameObject);
                pill = FBUI.Rect(cv.transform, "mic", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18 + 110, 40), new Vector2(220, 40));
                pillBg = pill.gameObject.AddComponent<Image>(); FBKit.Pill(pillBg);
                var b = pill.gameObject.AddComponent<Button>(); b.targetGraphic = pillBg; b.transition = Selectable.Transition.None; b.onClick.AddListener(() => { FBAudio.Synth("hover"); Toggle(); });
                var d = FBUI.Rect(pill, "ponto", new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(14, 14)); dot = d.gameObject.AddComponent<Image>(); dot.sprite = FBUI.CircleSprite; dot.raycastTarget = false;
                pillT = FBUI.Label(pill, "", 14, Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-40, 0), TextAnchor.MiddleLeft, true);
                talkT = FBUI.Label(cv.transform, "", 15, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18 + 150, -10), new Vector2(300, 120), TextAnchor.UpperLeft, true);
                talkT.rectTransform.pivot = new Vector2(.5f, 1);
                var sh = talkT.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .8f);
            }
            cv.enabled = show || AlguemFalando();
            if (!cv.enabled || Time.unscaledTime - uiT < .1f) return; uiT = Time.unscaledTime;
            pill.gameObject.SetActive(show);
            var tr = talkT.rectTransform;
            if (partida) { tr.anchorMin = tr.anchorMax = new Vector2(0, .5f); tr.anchoredPosition = new Vector2(18 + 150, -10); talkT.alignment = TextAnchor.UpperLeft; }
            else { tr.anchorMin = tr.anchorMax = new Vector2(.5f, 1); tr.anchoredPosition = new Vector2(0, -8); talkT.alignment = TextAnchor.UpperCenter; }
            string tecla = Application.isMobilePlatform ? "TOQUE" : "CAPS";
            pillT.text = !string.IsNullOrEmpty(erro) ? erro : on ? (falando ? "FALANDO…" : "MIC LIGADO") + $" · {tecla}" : $"MIC DESLIGADO · {tecla}";
            pillBg.color = on ? new Color(.06f, .42f, .2f, .85f) : new Color(.02f, .06f, .13f, .8f);
            dot.color = on ? Color.Lerp(new Color(.45f, .95f, .55f), Color.white, falando ? level : 0) : new Color(.86f, .12f, .06f);
            var sb = new System.Text.StringBuilder();
            foreach (var p in peers.Values) if (p && p.Ativo) sb.Append("» ").Append(p.nome).Append('\n');
            talkT.text = sb.ToString();
        }
        bool AlguemFalando() { foreach (var p in peers.Values) if (p && p.Ativo) return true; return false; }
    }

    // IMA ADPCM 4 bits (mono). Cabeçalho de 5 bytes: preditor (int16 LE), índice do passo (byte), número de amostras (uint16 LE);
    // o estado do codificador continua de um pacote para o outro, e o cabeçalho deixa cada pacote decodificável sozinho (pacote perdido
    // não estraga os seguintes).
    public static class Adpcm
    {
        public struct State { public int pred, idx; }
        static readonly int[] IDX = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        static readonly int[] STEP = { 7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767 };
        public static byte[] Encode(short[] pcm, ref State st)
        {
            int n = pcm.Length; var o = new byte[5 + (n + 1) / 2];
            o[0] = (byte)(st.pred & 0xff); o[1] = (byte)((st.pred >> 8) & 0xff); o[2] = (byte)st.idx; o[3] = (byte)(n & 0xff); o[4] = (byte)((n >> 8) & 0xff);
            int pred = st.pred, idx = st.idx;
            for (int i = 0; i < n; i++)
            {
                int diff = pcm[i] - pred, code = 0; if (diff < 0) { code = 8; diff = -diff; }
                int step = STEP[idx], vd = step >> 3;
                if (diff >= step) { code |= 4; diff -= step; vd += step; } step >>= 1;
                if (diff >= step) { code |= 2; diff -= step; vd += step; } step >>= 1;
                if (diff >= step) { code |= 1; vd += step; }
                pred += (code & 8) != 0 ? -vd : vd; pred = Mathf.Clamp(pred, -32768, 32767);
                idx = Mathf.Clamp(idx + IDX[code], 0, 88);
                if ((i & 1) == 0) o[5 + i / 2] = (byte)code; else o[5 + i / 2] |= (byte)(code << 4);
            }
            st.pred = pred; st.idx = idx; return o;
        }
        public static short[] Decode(byte[] b)
        {
            if (b == null || b.Length < 5) return null;
            int pred = (short)(b[0] | (b[1] << 8)), idx = Mathf.Clamp(b[2], 0, 88), n = b[3] | (b[4] << 8);
            n = Mathf.Min(n, (b.Length - 5) * 2); var o = new short[n];
            for (int i = 0; i < n; i++)
            {
                int code = (i & 1) == 0 ? b[5 + i / 2] & 0xf : b[5 + i / 2] >> 4;
                int step = STEP[idx], vd = step >> 3;
                if ((code & 4) != 0) vd += step; if ((code & 2) != 0) vd += step >> 1; if ((code & 1) != 0) vd += step >> 2;
                pred += (code & 8) != 0 ? -vd : vd; pred = Mathf.Clamp(pred, -32768, 32767);
                idx = Mathf.Clamp(idx + IDX[code], 0, 88); o[i] = (short)pred;
            }
            return o;
        }
    }
}
