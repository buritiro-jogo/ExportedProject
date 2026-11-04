// [mirante 03/11/2026] Elevador panorâmico do Mirante Edileusa Lóz (opção C: térreo ↔ varanda a 25 m).
// Funcionamento automático, sem tecla:
//  · quem entra na cabine parada e fica 1,5 s lá dentro faz a porta fechar (0,8 s) e a cabine ir para a outra parada (2,5 m/s ≈ 10 s);
//  · quem pisa na área de chamada de um andar chama a cabine se ela estiver vazia no outro andar;
//  · a cabine é de vidro: quem está fora vê quem sobe. Vidro na camada Gatilho (13): segura o corpo, mas as balas atravessam
//    (o ShotMask não inclui a 13). O piso e o teto ficam na camada Mapa (8).
//  · leva junto todo CharacterController que estiver dentro (subindo, move as pessoas antes da cabine; descendo, depois).
// Som: zumbido do motor gerado em código (3D) e um "plim" na chegada (FBAudio.PlayAt).
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    [DefaultExecutionOrder(-200)]
    public class FBElevador : MonoBehaviour
    {
        public Transform cabine;                 // raiz da cabine (piso na origem, frente de vidro para fora)
        public Transform portaE, portaD;         // folhas da porta da cabine (deslizam no x local)
        public Transform[] portasAndar = new Transform[2];   // folhas duplas de cada andar (pares E/D como filhos), opcional
        public float[] paradas = { 0f, 25f };    // altura do piso da cabine (y local do objeto FBElevador)
        public Vector3 tamanhoCabine = new(2.4f, 2.5f, 1.8f);
        public Vector3 areaChamada = new(2.4f, 2.2f, 2.0f);   // caixa na frente da porta de cada andar (lado +z local da cabine)
        public float velocidade = 2.5f, esperaEntrada = 1.5f, tempoPorta = .8f, aberturaPorta = .55f;

        enum Estado { Parada, Fechando, Andando, Abrindo }
        Estado st = Estado.Parada; int andar, destino; float t, ocupadaT;
        Vector3 pE0, pD0; AudioSource motor; static AudioClip zumbido, plim;
        readonly HashSet<CharacterController> dentro = new(); readonly Collider[] buf = new Collider[16];
        static int MaskChar => 1 << FBWorld.LChar;
        public bool Andando => st == Estado.Andando;
        public float Altura => cabine ? cabine.localPosition.y : 0;

        void Start()
        {
            if (!cabine) cabine = transform.Find("Cabine");
            if (portaE) pE0 = portaE.localPosition; if (portaD) pD0 = portaD.localPosition;
            andar = 0; destino = 0; SetY(paradas[0]); Portas(1); PortaAndar(0, 1); PortaAndar(1, 0);
            motor = gameObject.AddComponent<AudioSource>(); motor.clip = Zumbido(); motor.loop = true; motor.spatialBlend = 1; motor.minDistance = 4; motor.maxDistance = 60; motor.rolloffMode = AudioRolloffMode.Linear; motor.volume = 0; motor.playOnAwake = false;
        }

        void SetY(float y) { var p = cabine.localPosition; p.y = y; cabine.localPosition = p; }
        void Portas(float abre) { if (portaE) portaE.localPosition = pE0 + Vector3.left * aberturaPorta * abre; if (portaD) portaD.localPosition = pD0 + Vector3.right * aberturaPorta * abre; }
        void PortaAndar(int i, float abre)
        {
            if (portasAndar == null || i >= portasAndar.Length || !portasAndar[i] || portasAndar[i].childCount < 2) return;
            var e = portasAndar[i].GetChild(0); var d = portasAndar[i].GetChild(1);
            e.localPosition = new Vector3(-.3f - aberturaPorta * abre, e.localPosition.y, e.localPosition.z); d.localPosition = new Vector3(.3f + aberturaPorta * abre, d.localPosition.y, d.localPosition.z);
        }

        // quem está dentro da cabine (cápsulas do jogador, piloto e bots)
        void Ocupantes()
        {
            dentro.Clear();
            var c = cabine.TransformPoint(new Vector3(0, tamanhoCabine.y * .5f, 0));
            int n = Physics.OverlapBoxNonAlloc(c, tamanhoCabine * .5f - new Vector3(.1f, .1f, .1f), buf, cabine.rotation, MaskChar, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) { var cc = buf[i].GetComponentInParent<CharacterController>(); if (cc) dentro.Add(cc); }
        }
        bool Chamando(int a)
        {
            var c = transform.TransformPoint(new Vector3(0, paradas[a] + areaChamada.y * .5f, tamanhoCabine.z * .5f + areaChamada.z * .5f + .2f));
            return Physics.OverlapBoxNonAlloc(c, areaChamada * .5f, buf, transform.rotation, MaskChar, QueryTriggerInteraction.Ignore) > 0;
        }

        void Update()
        {
            if (!cabine) return; float dt = Time.deltaTime; Ocupantes();
            switch (st)
            {
                case Estado.Parada:
                    ocupadaT = dentro.Count > 0 ? ocupadaT + dt : 0;
                    int outro = 1 - andar;
                    if (ocupadaT >= esperaEntrada) Parte(outro);
                    else if (dentro.Count == 0 && Chamando(outro)) Parte(outro);
                    break;
                case Estado.Fechando:
                    t += dt / tempoPorta; Portas(1 - t); PortaAndar(andar, 1 - t);
                    if (t >= 1) { st = Estado.Andando; motor.volume = .55f * FBSession.master; if (!motor.isPlaying) motor.Play(); }
                    break;
                case Estado.Andando:
                    {
                        float y = Altura, alvo = paradas[destino], dy = Mathf.MoveTowards(y, alvo, velocidade * dt) - y;
                        var delta = transform.TransformVector(new Vector3(0, dy, 0));
                        if (dy > 0) { Leva(delta); SetY(y + dy); } else { SetY(y + dy); Leva(delta); }
                        if (Mathf.Abs(alvo - Altura) < 1e-3f) { andar = destino; st = Estado.Abrindo; t = 0; motor.Stop(); FBAudio.PlayAt(Plim(), cabine.position + Vector3.up * 2.2f, .6f, 5); }
                        break;
                    }
                case Estado.Abrindo:
                    t += dt / tempoPorta; Portas(t); PortaAndar(andar, t);
                    if (t >= 1) { st = Estado.Parada; ocupadaT = -1.5f; }   // quem chegou tem 3 s para sair antes de a cabine voltar
                    break;
            }
        }
        void Parte(int a) { destino = a; st = Estado.Fechando; t = 0; ocupadaT = 0; }
        void Leva(Vector3 delta) { foreach (var cc in dentro) if (cc && cc.enabled) cc.Move(delta); }

        // zumbido do motor (85 Hz + harmônico + ruído leve), 2 s em loop
        static AudioClip Zumbido()
        {
            if (zumbido) return zumbido; int sr = 22050, n = sr * 2; var d = new float[n]; var r = new System.Random(7);
            for (int i = 0; i < n; i++) { float s = i / (float)sr; d[i] = .25f * Mathf.Sin(2 * Mathf.PI * 85 * s) + .1f * Mathf.Sin(2 * Mathf.PI * 170 * s) + .04f * (float)(r.NextDouble() * 2 - 1); }
            zumbido = AudioClip.Create("fb_elevador_motor", n, 1, sr, false); zumbido.SetData(d, 0); return zumbido;
        }

        // "plim" de chegada: 2 notas (mi 5 → dó 5) com decaimento
        static AudioClip Plim()
        {
            if (plim) return plim; int sr = 22050, n = (int)(sr * 1.2f); var d = new float[n];
            for (int i = 0; i < n; i++) { float s = i / (float)sr; float f = s < .35f ? 659.3f : 523.3f, s0 = s < .35f ? s : s - .35f; d[i] = .35f * Mathf.Sin(2 * Mathf.PI * f * s) * Mathf.Exp(-s0 * 5); }
            plim = AudioClip.Create("fb_elevador_plim", n, 1, sr, false); plim.SetData(d, 0); return plim;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = new Color(0, 1, 1, .4f);
            foreach (var y in paradas) { Gizmos.DrawWireCube(new Vector3(0, y + tamanhoCabine.y * .5f, 0), tamanhoCabine); Gizmos.DrawWireCube(new Vector3(0, y + areaChamada.y * .5f, tamanhoCabine.z * .5f + areaChamada.z * .5f + .2f), areaChamada); }
        }
    }
}
