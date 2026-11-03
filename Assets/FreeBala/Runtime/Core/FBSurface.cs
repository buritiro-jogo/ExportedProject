// Marca de piso explícita (o importador põe nos colisores do mapa quando o nome do material indica).
// Fica em arquivo próprio: componente salvo nas cenas precisa estar num .cs com o mesmo nome da classe, senão o jogo compilado
// não acha o script e lê a cena errado ("level corrupted").
using UnityEngine;

namespace FreeBala
{
    public class FBSurface : MonoBehaviour { public int kind; }
}
