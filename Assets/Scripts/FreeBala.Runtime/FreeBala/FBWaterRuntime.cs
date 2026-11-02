using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeBala
{
public class FBWaterRuntime : MonoBehaviour
{
	public string mapId;

	private FBWater.State st;

	private bool done;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		SceneManager.sceneLoaded -= OnLoaded;
		SceneManager.sceneLoaded += OnLoaded;
		OnLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
	}

	private static void OnLoaded(Scene sc, LoadSceneMode mode)
	{
		if (sc.name.StartsWith("Map_") && !Object.FindAnyObjectByType<FBWaterRuntime>())
		{
			GameObject obj = new GameObject("FB_Agua");
			SceneManager.MoveGameObjectToScene(obj, sc);
			obj.AddComponent<FBWaterRuntime>().mapId = sc.name.Substring(4);
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBWaterRuntime.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		if (!done)
		{
			FBMatch i = FBMatch.I;
			if (!i || i.Level == null)
			{
				return;
			}
			done = true;
			st = FBWater.Setup(i.Level);
		}
		FBWater.Tick(st, Time.deltaTime);
	}

	private void OnDestroy()
	{
		if (st == null)
		{
			return;
		}
		if ((bool)st.drops)
		{
			MeshFilter component = st.drops.GetComponent<MeshFilter>();
			if ((bool)component && (bool)component.sharedMesh)
			{
				Object.Destroy(component.sharedMesh);
			}
			Object.Destroy(st.drops);
		}
		FBWater.Stop();
	}
}
}
