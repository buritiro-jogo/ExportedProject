using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBMenuReturnTest : MonoBehaviour
{
	private string map = "vila_jardim";

	private string mode = "mata";

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbvoltamenu");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_TesteVoltaMenu");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBMenuReturnTest fBMenuReturnTest = obj.AddComponent<FBMenuReturnTest>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				string[] array = commandLineArgs[num + 1].Split(':');
				fBMenuReturnTest.map = array[0];
				fBMenuReturnTest.mode = ((array.Length > 1) ? array[1] : "mata");
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][voltamenu] " + s);
	}

	private static IEnumerator Wait(float s)
	{
		for (float t = 0f; t < s; t += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Shot(string dir, string name)
	{
		yield return new WaitForEndOfFrame();
		ScreenCapture.CaptureScreenshot(Path.Combine(dir, name));
		yield return null;
		yield return null;
		Log("print " + name);
	}

	private static void State(string when)
	{
		UniversalRenderPipelineAsset universalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
		Camera main = Camera.main;
		UniversalAdditionalCameraData universalAdditionalCameraData = (main ? main.GetUniversalAdditionalCameraData() : null);
		Log(string.Format("{0}: névoa {1} · volumétrica cena {2} · escala {3:0.00} · msaa {4} · AA câmera {5} · fog Unity {6} · LOD_FADE {7}", when, Shader.GetGlobalVector("_FBFogParams"), FBVolumetric.InScene, universalRenderPipelineAsset ? universalRenderPipelineAsset.renderScale : (-1f), universalRenderPipelineAsset ? universalRenderPipelineAsset.msaaSampleCount : (-1), universalAdditionalCameraData ? universalAdditionalCameraData.antialiasing.ToString() : "-", RenderSettings.fog, Shader.IsKeywordEnabled("LOD_FADE_CROSSFADE")));
	}

	private IEnumerator Start()
	{
		string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "autoteste");
		Directory.CreateDirectory(dir);
		while (!FBMenu.I)
		{
			yield return null;
		}
		yield return Wait(4f);
		State("menu na abertura");
		yield return Shot(dir, "volta_menu_1_abertura.png");
		GameData.Load();
		FBSession.Load();
		string keepMap = FBSession.map;
		string keepMode = FBSession.mode;
		FBSession.map = map;
		FBSession.mode = mode;
		FBSession.online = false;
		FBSession.Save();
		AsyncOperation op = SceneManager.LoadSceneAsync("Map_" + map);
		while (op != null && !op.isDone)
		{
			yield return null;
		}
		while (!FBMatch.I || !FBMatch.Player || !(FBMatch.I.state == "play"))
		{
			yield return null;
		}
		yield return Wait(15f);
		State("na partida");
		FBMenu menu0 = FBMenu.I;
		FBMatch.I.ToMenu();
		while (!FBMenu.I || FBMenu.I == menu0)
		{
			yield return null;
		}
		yield return Wait(4f);
		State("menu depois da partida");
		yield return Shot(dir, "volta_menu_2_depois.png");
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		Log("fim");
		Application.Quit(0);
	}
}
}
