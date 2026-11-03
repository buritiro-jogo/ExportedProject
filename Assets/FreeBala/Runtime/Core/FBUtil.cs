// Utilidades comuns. GetOrAdd: o "??" do C# não enxerga o null falso dos componentes do Unity (GetComponent devolve um objeto "morto").
using UnityEngine;

namespace FreeBala
{
    public static class FBUtil
    {
        public static T GetOrAdd<T>(this GameObject g) where T : Component { var c = g.GetComponent<T>(); return c ? c : g.AddComponent<T>(); }
        public static T GetOrAdd<T>(this Component c) where T : Component => c.gameObject.GetOrAdd<T>();
        public static float WrapA(float a) => Mathf.Atan2(Mathf.Sin(a), Mathf.Cos(a));
        public static float Rnd(float a, float b) => a + Random.value * (b - a);
        public static T Pick<T>(System.Collections.Generic.IList<T> l) => l.Count == 0 ? default : l[Random.Range(0, l.Count)];
        // yaw no estilo do web (rad, 0 = +Z, cresce para +X) a partir de um vetor do Unity
        public static float YawOf(Vector3 d) => Mathf.Atan2(d.x, d.z);
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
