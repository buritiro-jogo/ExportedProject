// Liga o Frame Timing Stats (CPU principal, render thread e GPU separados) para o teste de desempenho do .exe
// (Assets/Scripts/FBTeste/FBTesteExe.cs). Pode apagar junto com os scripts de teste.
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
