// Dados da vegetação de um mapa (gerados na importação pelo FBVegBake; lidos pelo FBVegetation). Arquivo próprio: ScriptableObject.
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
    public class FBVegData : ScriptableObject
    {
        [System.Serializable] public class Kind { public string name; public Mesh mesh; public Material mat; public bool shadows; }
        // célula de ~24 m: instâncias de UM tipo, ordenadas pelo sorteio fixo (hash da posição) -> a densidade do nível é um prefixo do array
        [System.Serializable] public class Cell { public Vector3 center; public float radius; public int kind; public Matrix4x4[] m; }
        public Kind[] kinds; public List<Cell> cells = new();
        public List<Vector3> openSpots = new(), edgeSpots = new();   // pontos para os prints (-fbgfx): meio do gramado / beira de calçada
        public int total;
        // tapete de grama (FBGrassCarpet): máscara do gramado (R8, 0..1 com borda suave) e altura do chão (RHalf, metros acima de grassY0)
        // numa grade de grassRect = (x0, z0, largura, profundidade) no mundo; material FreeBala/GrassCarpet salvo como asset (leva o shader ao build)
        public Texture2D grassMask, grassHeight; public Vector4 grassRect; public float grassY0; public Material grassMat;
    }
}
