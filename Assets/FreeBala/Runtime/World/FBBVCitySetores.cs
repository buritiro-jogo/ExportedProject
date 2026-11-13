// BV City: carrega e descarrega a PLANTA por setor de 400 m, conforme a distancia ate a camera.
// Cada setor e um prefab em Resources/BVCity/Setores/<id>, montado pelo Editor (FBBVCityPlanta) a partir da
// planta georreferenciada (tools/bv_city). Corredor (x, y, h) -> Unity (-x, h, +y); 1 unidade = 1 metro.
// Fora do alcance o setor nao existe na memoria: e pedido com Resources.LoadAsync e destruido ao sair.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBBVCitySetores : MonoBehaviour
    {
        [System.Serializable]
        public struct Setor
        {
            public string id;
            public Vector2 min, max;      // quadrado do setor no plano XZ do Unity
            public int triangulos;
        }

        public Setor[] setores = new Setor[0];
        public string pasta = "BVCity/Setores";
        [Tooltip("distancia (m) do quadrado do setor ate a camera para pedir o setor")]
        public float raioEntra = 700f;
        [Tooltip("distancia (m) para destruir o setor; maior que raioEntra para nao ficar ligando e desligando")]
        public float raioSai = 900f;
        public float intervalo = 0.25f;
        [Tooltip("na partida, tudo a menos desta distancia e carregado na hora")]
        public float raioPartida = 300f;
        public Transform alvo;            // se vazio, usa a camera principal

        readonly Dictionary<int, GameObject> vivos = new();
        readonly HashSet<int> pedindo = new();
        float prox; int descargas; bool partiu;

        public static FBBVCitySetores Atual { get; private set; }
        public int Vivos => vivos.Count;
        public int Pedindo => pedindo.Count;
        public bool Vivo(int i) => vivos.TryGetValue(i, out var g) && g;
        public int Cargas { get; private set; }
        public int Descargas { get; private set; }

        void OnEnable() { Atual = this; }
        // desativado no meio de uma carga, a corrotina morre: sem limpar, 'pedindo' ficava cheio e nada mais carregava
        void OnDisable() { if (Atual == this) Atual = null; pedindo.Clear(); }
        void OnValidate() { if (raioSai < raioEntra + 50f) raioSai = raioEntra + 200f; }

        public static float Dist(in Setor s, Vector3 p)
        {
            float dx = Mathf.Max(s.min.x - p.x, 0f, p.x - s.max.x);
            float dz = Mathf.Max(s.min.y - p.z, 0f, p.z - s.max.y);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void Update()
        {
            if (Time.unscaledTime < prox) return;
            prox = Time.unscaledTime + intervalo;
            Vector3 p;
            if (alvo) p = alvo.position;
            else { var c = Camera.main; if (!c) return; p = c.transform.position; }
            // na partida (ou no primeiro quadro com camera) o que esta perto entra de uma vez, sem esperar a fila
            if (!partiu) { partiu = true; CarregaAgora(p, raioPartida); }
            Passo(p);
        }

        /// <summary>Um passo do carregador para a posicao p: destroi o que passou do raio de saida e pede o setor mais
        /// proximo que falta (um por passo). Em Play o pedido e assincrono; fora do Play (teste no Editor) e na hora.</summary>
        public void Passo(Vector3 p)
        {
            int melhor = -1; float dm = float.MaxValue;
            for (int i = 0; i < setores.Length; i++)
            {
                float d = Dist(setores[i], p);
                if (vivos.TryGetValue(i, out var go))
                {
                    if (d > raioSai) { if (go) { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); } vivos.Remove(i); descargas++; Descargas++; }
                }
                else if (d < raioEntra && d < dm && !pedindo.Contains(i)) { dm = d; melhor = i; }
            }
            if (melhor >= 0 && pedindo.Count < 2)
            {
                if (Application.isPlaying) StartCoroutine(Carrega(melhor));
                else
                {
                    var pf = Resources.Load<GameObject>(pasta + "/" + setores[melhor].id);
                    if (pf) { var go = Instantiate(pf, transform); go.name = setores[melhor].id; go.hideFlags = HideFlags.DontSave; vivos[melhor] = go; Cargas++; }
                    else vivos[melhor] = null;
                }
            }
            if (descargas >= 6 && pedindo.Count == 0) { descargas = 0; Resources.UnloadUnusedAssets(); }
        }

        IEnumerator Carrega(int i)
        {
            pedindo.Add(i);
            var req = Resources.LoadAsync<GameObject>(pasta + "/" + setores[i].id);
            yield return req;
            pedindo.Remove(i);
            if (!this || vivos.ContainsKey(i)) yield break;
            var pf = req.asset as GameObject;
            if (!pf) { Debug.LogWarning("[BVCity] setor sem prefab: " + setores[i].id); vivos[i] = null; yield break; }
            var go = Instantiate(pf, transform);
            go.name = setores[i].id;
            vivos[i] = go; Cargas++;
        }

        /// <summary>Carrega na hora (sem esperar) todos os setores a menos de 'raio' de p. Para testes e fotos.</summary>
        public int CarregaAgora(Vector3 p, float raio)
        {
            int n = 0;
            for (int i = 0; i < setores.Length; i++)
            {
                if (vivos.ContainsKey(i) || Dist(setores[i], p) > raio) continue;
                var pf = Resources.Load<GameObject>(pasta + "/" + setores[i].id);
                if (!pf) continue;
                var go = Instantiate(pf, transform);
                go.name = setores[i].id; vivos[i] = go; Cargas++; n++;
            }
            return n;
        }
    }
}
