// [parceiro 02/11] copiado de parceiro_ExportedProject/Assets/Editor/FBTestePerfBuild.cs (ferramenta de Editor do parceiro; caminhos adaptados ao nosso projeto).
// Liga o Frame Timing Stats (CPU principal, render thread e GPU separados) para o teste de desempenho do .exe
// (Assets/FreeBala/Runtime/Teste/FBTesteExe.cs; no parceiro era Assets/Scripts/FBTeste/FBTesteExe.cs). Pode apagar junto com os scripts de teste.
// [unificação 02/11] roda sozinho ao abrir o Editor ([InitializeOnLoad]) e liga PlayerSettings.enableFrameTimingStats
// no ProjectSettings (no nosso projeto estava 0).
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public class FBTestePerfBuild : IPreprocessBuildWithReport
{
	static FBTestePerfBuild()
	{
		Liga();
	}

	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		Liga();
	}

	static void Liga()
	{
		if (PlayerSettings.enableFrameTimingStats) return;
		PlayerSettings.enableFrameTimingStats = true;
		Debug.Log("[FreeBala][perf] Frame Timing Stats ligado (CPU principal, render thread e GPU no teste do .exe)");
	}
}
