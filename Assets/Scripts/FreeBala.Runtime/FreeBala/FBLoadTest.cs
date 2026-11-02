using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FreeBala
{
public class FBLoadTest : MonoBehaviour
{
	private string map = "vila_jardim";

	private string mode = "role";

	private int times = 2;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbcarrega");
		if (num >= 0)
		{
			GameObject obj = new GameObject("FB_TesteCarrega");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			FBLoadTest fBLoadTest = obj.AddComponent<FBLoadTest>();
			if (num + 1 < commandLineArgs.Length && !commandLineArgs[num + 1].StartsWith("-"))
			{
				string[] array = commandLineArgs[num + 1].Split(':');
				fBLoadTest.map = array[0];
				fBLoadTest.mode = ((array.Length > 1) ? array[1] : "mata");
			}
			if (num + 2 < commandLineArgs.Length && int.TryParse(commandLineArgs[num + 2], out var result))
			{
				fBLoadTest.times = Mathf.Clamp(result, 1, 6);
			}
		}
	}

	private static void Log(string s)
	{
		Debug.Log("[FreeBala][carrega-teste] " + s);
	}

	private static IEnumerator Wait(float s)
	{
		for (float t = 0f; t < s; t += Time.unscaledDeltaTime)
		{
			yield return null;
		}
	}

	private IEnumerator Start()
	{
		while (!FBMenu.I)
		{
			yield return null;
		}
		yield return Wait(3f);
		GameData.Load();
		FBSession.Load();
		string keepMap = FBSession.map;
		string keepMode = FBSession.mode;
		FBSession.map = map;
		FBSession.mode = mode;
		FBSession.online = false;
		FBSession.Save();
		StringBuilder res = new StringBuilder();
		for (int i = 1; i <= times; i++)
		{
			Stopwatch sw = Stopwatch.StartNew();
			long mem0 = GC.GetTotalMemory(forceFullCollection: false);
			FBLoading.Load("Map_" + map, map, mode);
			while (!FBLoading.Busy)
			{
				yield return null;
			}
			while (FBLoading.Busy)
			{
				yield return null;
			}
			float num = (float)sw.Elapsed.TotalSeconds;
			Log($"{i}ª entrada {map}:{mode}: {num:0.0} s do clique até jogar (fases somam {FBLoading.LastTotal:0.0} s) · memória gerenciada {(float)(GC.GetTotalMemory(forceFullCollection: false) - mem0) / 1048576f:+0;-0} MB");
			res.Append($"{i}ª {num:0.0} s · ");
			yield return Wait(3f);
			if (i < times)
			{
				FBMenu menu0 = FBMenu.I;
				FBMatch.I.ToMenu();
				while (!FBMenu.I || FBMenu.I == menu0)
				{
					yield return null;
				}
				yield return Wait(3f);
			}
		}
		Log("fim · " + res);
		FBSession.map = keepMap;
		FBSession.mode = keepMode;
		FBSession.Save();
		yield return Wait(1f);
		Application.Quit(0);
	}
}
}
