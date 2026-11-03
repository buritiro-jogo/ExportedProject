// Janelas do mapa medidas na IMPORTAÇÃO (FBGlassSplit): centro, normal para fora, largura e altura de cada painel de vidro.
// Em jogo as malhas do mapa podem não ser legíveis; FBWindowLights usa estes dados para acender as janelas à noite.
using UnityEngine;

namespace FreeBala
{
    public class FBWindowData : MonoBehaviour
    {
        public Vector3[] center = new Vector3[0], normal = new Vector3[0]; public Vector2[] size = new Vector2[0];
    }
}
