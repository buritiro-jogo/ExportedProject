// registra a NavMesh assada no editor quando a cena do mapa carrega
using UnityEngine;
using UnityEngine.AI;

namespace FreeBala
{
    public class FBNavData : MonoBehaviour
    {
        public NavMeshData data; NavMeshDataInstance h;
        void OnEnable() { if (data) h = NavMesh.AddNavMeshData(data); }
        void OnDisable() { if (h.valid) h.Remove(); }
    }

}
