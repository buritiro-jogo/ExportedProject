using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBCustomizeThumbs : MonoBehaviour
{
	public const int Version = 2;

	public const int Layer = 30;

	private static readonly Dictionary<string, Texture2D> mem = new Dictionary<string, Texture2D>();

	private readonly List<(string id, Action<Texture2D> cb)> queue = new List<(string, Action<Texture2D>)>();

	private readonly Dictionary<string, List<Action<Texture2D>>> waiting = new Dictionary<string, List<Action<Texture2D>>>();

	private Camera tcam;

	private Light tlight;

	private Light tfill;

	private RenderTexture rt;

	private GameObject inst;

	private string cur;

	private int frames;

	private int rendered;

	private bool diskOk = true;

	private static readonly Vector3 Spot = new Vector3(1000f, 0f, 0f);

	public static int W
	{
		get
		{
			if (!Application.isMobilePlatform)
			{
				return 224;
			}
			return 160;
		}
	}

	public static int H => W * 5 / 4;

	public int Pending => queue.Count + ((cur != null) ? 1 : 0);

	private static string Dir => Path.Combine(Application.persistentDataPath, "fb_thumbs", "v" + 2);

	public static bool Cached(string id)
	{
		if (mem.ContainsKey(id))
		{
			return mem[id];
		}
		return false;
	}

	public static Texture2D Get(string id)
	{
		if (!mem.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public static FBCustomizeThumbs Create(Transform parent)
	{
		GameObject gameObject = new GameObject("FB_Miniaturas");
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		FBCustomizeThumbs fBCustomizeThumbs = gameObject.AddComponent<FBCustomizeThumbs>();
		GameObject gameObject2 = new GameObject("cam_miniatura");
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		fBCustomizeThumbs.tcam = gameObject2.AddComponent<Camera>();
		fBCustomizeThumbs.tcam.enabled = false;
		fBCustomizeThumbs.tcam.cullingMask = 1073741824;
		fBCustomizeThumbs.tcam.clearFlags = CameraClearFlags.Color;
		fBCustomizeThumbs.tcam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		fBCustomizeThumbs.tcam.fieldOfView = 24f;
		fBCustomizeThumbs.tcam.nearClipPlane = 0.05f;
		fBCustomizeThumbs.tcam.farClipPlane = 20f;
		fBCustomizeThumbs.tcam.allowHDR = false;
		fBCustomizeThumbs.tcam.allowMSAA = true;
		GameObject gameObject3 = new GameObject("luz_miniatura");
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		fBCustomizeThumbs.tlight = gameObject3.AddComponent<Light>();
		fBCustomizeThumbs.tlight.type = LightType.Point;
		fBCustomizeThumbs.tlight.cullingMask = 1073741824;
		fBCustomizeThumbs.tlight.range = 6f;
		fBCustomizeThumbs.tlight.intensity = 5.5f;
		fBCustomizeThumbs.tlight.color = new Color(1f, 0.94f, 0.84f);
		fBCustomizeThumbs.tlight.shadows = LightShadows.None;
		fBCustomizeThumbs.tlight.enabled = false;
		GameObject gameObject4 = new GameObject("luz_fill");
		gameObject4.transform.SetParent(gameObject.transform, worldPositionStays: false);
		fBCustomizeThumbs.tfill = gameObject4.AddComponent<Light>();
		fBCustomizeThumbs.tfill.type = LightType.Point;
		fBCustomizeThumbs.tfill.cullingMask = 1073741824;
		fBCustomizeThumbs.tfill.range = 6f;
		fBCustomizeThumbs.tfill.intensity = 3.5f;
		fBCustomizeThumbs.tfill.color = new Color(0.82f, 0.9f, 1f);
		fBCustomizeThumbs.tfill.shadows = LightShadows.None;
		fBCustomizeThumbs.tfill.enabled = false;
		fBCustomizeThumbs.rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
		{
			antiAliasing = 1,
			name = "rt_miniatura"
		};
		if ((bool)Camera.main)
		{
			Camera.main.cullingMask &= -1073741825;
		}
		return fBCustomizeThumbs;
	}

	private void OnDestroy()
	{
		if ((bool)inst)
		{
			UnityEngine.Object.Destroy(inst);
		}
		if ((bool)rt)
		{
			rt.Release();
			UnityEngine.Object.Destroy(rt);
		}
	}

	public void Request(string id, Action<Texture2D> cb)
	{
		List<Action<Texture2D>> value2;
		if (mem.TryGetValue(id, out var value) && (bool)value)
		{
			cb?.Invoke(value);
		}
		else if (waiting.TryGetValue(id, out value2))
		{
			value2.Add(cb);
			queue.RemoveAll(((string id, Action<Texture2D> cb) q) => q.id == id);
			queue.Add((id, null));
		}
		else if (TryDisk(id, out value))
		{
			cb?.Invoke(value);
		}
		else
		{
			waiting[id] = new List<Action<Texture2D>> { cb };
			queue.Add((id, null));
		}
	}

	private bool TryDisk(string id, out Texture2D t)
	{
		t = null;
		if (!diskOk)
		{
			return false;
		}
		try
		{
			string path = Path.Combine(Dir, id + ".png");
			if (!File.Exists(path))
			{
				return false;
			}
			t = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
			{
				name = "mini_" + id,
				wrapMode = TextureWrapMode.Clamp
			};
			if (!t.LoadImage(File.ReadAllBytes(path), markNonReadable: true))
			{
				UnityEngine.Object.Destroy(t);
				t = null;
				return false;
			}
			mem[id] = t;
			return true;
		}
		catch
		{
			diskOk = false;
			return false;
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBCustomizeThumbs.Update");

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
		if (cur == null)
		{
			if (queue.Count == 0)
			{
				return;
			}
			List<(string id, Action<Texture2D> cb)> list = queue;
			(string, Action<Texture2D>) tuple = list[list.Count - 1];
			queue.RemoveAt(queue.Count - 1);
			cur = tuple.Item1;
			frames = 0;
			GameObject gameObject = Resources.Load<GameObject>("FBPrefabs/Chars/" + cur);
			if (!gameObject)
			{
				Done(null);
				return;
			}
			inst = UnityEngine.Object.Instantiate(gameObject, Spot, Quaternion.Euler(0f, 192f, 0f));
			inst.name = "Miniatura_" + cur;
			FBCharacter component = inst.GetComponent<FBCharacter>();
			if ((bool)component)
			{
				component.relaxed = true;
				component.isPlayer = false;
				component.skinId = cur;
			}
			Animator component2 = inst.GetComponent<Animator>();
			if ((bool)component2)
			{
				component2.cullingMode = AnimatorCullingMode.AlwaysAnimate;
			}
			FBWardrobe component3 = inst.GetComponent<FBWardrobe>();
			if ((bool)component3)
			{
				component3.Apply(FBCustomizeData.LookFor(cur));
			}
			Transform[] componentsInChildren = inst.GetComponentsInChildren<Transform>(includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].gameObject.layer = 30;
			}
			Renderer[] componentsInChildren2 = inst.GetComponentsInChildren<Renderer>(includeInactive: true);
			foreach (Renderer obj in componentsInChildren2)
			{
				obj.shadowCastingMode = ShadowCastingMode.Off;
				obj.receiveShadows = false;
			}
		}
		else if (++frames >= 4)
		{
			Texture2D tex = null;
			try
			{
				tex = Render();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[FreeBala] miniatura " + cur + ": " + ex.Message);
			}
			Done(tex);
		}
	}

	private Texture2D Render()
	{
		Animator component = inst.GetComponent<Animator>();
		Transform transform = (((bool)component && component.isHuman) ? component.GetBoneTransform(HumanBodyBones.Head) : null);
		float y = Spot.y;
		float num;
		if ((bool)transform)
		{
			num = transform.position.y;
		}
		else
		{
			Renderer[] componentsInChildren = inst.GetComponentsInChildren<Renderer>();
			Bounds bounds = ((componentsInChildren.Length != 0) ? componentsInChildren[0].bounds : new Bounds(Spot + Vector3.up, Vector3.one));
			Renderer[] array = componentsInChildren;
			foreach (Renderer renderer in array)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			num = bounds.max.y - bounds.size.y * 0.08f;
		}
		float num2 = Mathf.Max(0.3f, (num - y) / 1.55f);
		float num3 = num - 0.62f * num2;
		float num4 = num + 0.24f * num2;
		float num5 = (num4 - num3) / 2f / Mathf.Tan(tcam.fieldOfView * 0.5f * ((float)Math.PI / 180f));
		Vector3 vector = new Vector3(Spot.x, (num3 + num4) / 2f, Spot.z);
		Quaternion quaternion = Quaternion.Euler(4f, 0f, 0f);
		tcam.transform.SetPositionAndRotation(vector - quaternion * Vector3.forward * num5, quaternion);
		tcam.aspect = (float)W / (float)H;
		tlight.transform.position = tcam.transform.position + new Vector3(-0.5f, 0.6f, 0.3f);
		tlight.range = num5 + 4f;
		tlight.enabled = true;
		tfill.transform.position = tcam.transform.position + new Vector3(0.6f, 0.2f, 0.4f);
		tfill.range = num5 + 4f;
		tfill.enabled = true;
		tcam.targetTexture = rt;
		RenderPipeline.StandardRequest data = new RenderPipeline.StandardRequest
		{
			destination = rt
		};
		if (RenderPipeline.SupportsRenderRequest(tcam, data))
		{
			RenderPipeline.SubmitRenderRequest(tcam, data);
		}
		else
		{
			tcam.Render();
		}
		tlight.enabled = false;
		tfill.enabled = false;
		RenderTexture active = RenderTexture.active;
		RenderTexture.active = rt;
		Texture2D texture2D = new Texture2D(W, H, TextureFormat.RGBA32, mipChain: false)
		{
			name = "mini_" + cur,
			wrapMode = TextureWrapMode.Clamp
		};
		texture2D.ReadPixels(new Rect(0f, 0f, W, H), 0, 0);
		RenderTexture.active = active;
		byte[] array2 = null;
		try
		{
			array2 = texture2D.EncodeToPNG();
		}
		catch
		{
		}
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		if (array2 != null && diskOk)
		{
			try
			{
				Directory.CreateDirectory(Dir);
				File.WriteAllBytes(Path.Combine(Dir, cur + ".png"), array2);
			}
			catch
			{
				diskOk = false;
			}
		}
		return texture2D;
	}

	private void Done(Texture2D tex)
	{
		if ((bool)inst)
		{
			UnityEngine.Object.Destroy(inst);
		}
		inst = null;
		if ((bool)tex)
		{
			mem[cur] = tex;
		}
		if (waiting.TryGetValue(cur, out var value))
		{
			waiting.Remove(cur);
			foreach (Action<Texture2D> item in value)
			{
				try
				{
					item?.Invoke(tex);
				}
				catch (Exception exception)
				{
					Debug.LogException(exception);
				}
			}
		}
		cur = null;
		if (++rendered % 10 == 0)
		{
			Resources.UnloadUnusedAssets();
		}
	}

	public static void Invalidate(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return;
		}
		mem.Remove(id);
		try
		{
			string path = Path.Combine(Dir, id + ".png");
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch
		{
		}
	}

	public static void ClearDisk()
	{
		try
		{
			string path = Path.Combine(Application.persistentDataPath, "fb_thumbs");
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch
		{
		}
		mem.Clear();
	}
}
}
