// Correção: no Editor, GetComponent devolve um objeto "nulo falso" que o operador ?? não reconhece.
// OrNull() converte esse nulo do Unity em null de verdade, para o ?? funcionar igual ao jogo compilado.
public static class FBUnityNullExt
{
	public static T OrNull<T>(this T o) where T : UnityEngine.Object => o ? o : null;
}