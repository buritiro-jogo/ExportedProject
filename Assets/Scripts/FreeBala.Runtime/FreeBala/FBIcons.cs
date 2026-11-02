using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
public static class FBIcons
{
	private const int LAYER = 30;

	private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

	private static Camera cam;

	private static Light key;

	private static Light fill;

	private static void Rig()
	{
		if (!cam)
		{
			GameObject gameObject = new GameObject("FB_IconStudio");
			Object.DontDestroyOnLoad(gameObject);
			gameObject.transform.position = new Vector3(0f, -8000f, 0f);
			cam = new GameObject("cam").AddComponent<Camera>();
			cam.transform.SetParent(gameObject.transform, worldPositionStays: false);
			cam.enabled = false;
			cam.cullingMask = 1073741824;
			cam.clearFlags = CameraClearFlags.Color;
			cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
			cam.nearClipPlane = 0.01f;
			cam.farClipPlane = 50f;
			cam.allowHDR = false;
			cam.allowMSAA = true;
			UniversalAdditionalCameraData universalAdditionalCameraData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
			universalAdditionalCameraData.renderPostProcessing = false;
			universalAdditionalCameraData.renderShadows = false;
			key = new GameObject("key").AddComponent<Light>();
			key.transform.SetParent(gameObject.transform, worldPositionStays: false);
			key.type = LightType.Directional;
			key.intensity = 1.35f;
			key.color = new Color(1f, 0.96f, 0.9f);
			key.cullingMask = 1073741824;
			key.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
			fill = new GameObject("fill").AddComponent<Light>();
			fill.transform.SetParent(gameObject.transform, worldPositionStays: false);
			fill.type = LightType.Directional;
			fill.intensity = 0.55f;
			fill.color = new Color(0.75f, 0.85f, 1f);
			fill.cullingMask = 1073741824;
			fill.transform.rotation = Quaternion.Euler(-20f, 140f, 0f);
			Light light = key;
			bool enabled = (fill.enabled = false);
			light.enabled = enabled;
		}
	}

	private static Texture2D Shoot(int w, int h)
	{
		RenderTexture temporary = RenderTexture.GetTemporary(w * 2, h * 2, 24, RenderTextureFormat.ARGB32);
		temporary.antiAliasing = 4;
		Light light = key;
		bool enabled = (fill.enabled = true);
		light.enabled = enabled;
		cam.targetTexture = temporary;
		cam.aspect = (float)w / (float)h;
		cam.Render();
		cam.targetTexture = null;
		Light light2 = key;
		enabled = (fill.enabled = false);
		light2.enabled = enabled;
		RenderTexture temporary2 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
		Graphics.Blit(temporary, temporary2);
		RenderTexture active = RenderTexture.active;
		RenderTexture.active = temporary2;
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		texture2D.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
		texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
		RenderTexture.active = active;
		RenderTexture.ReleaseTemporary(temporary);
		RenderTexture.ReleaseTemporary(temporary2);
		return texture2D;
	}

	private static void Layer(GameObject g)
	{
		Transform[] componentsInChildren = g.GetComponentsInChildren<Transform>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].gameObject.layer = 30;
		}
		Renderer[] componentsInChildren2 = g.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer obj in componentsInChildren2)
		{
			obj.shadowCastingMode = ShadowCastingMode.Off;
			obj.receiveShadows = false;
		}
	}

	private static Bounds B(GameObject g)
	{
		Renderer[] componentsInChildren = g.GetComponentsInChildren<Renderer>();
		Bounds result = new Bounds(g.transform.position, Vector3.zero);
		bool flag = true;
		Renderer[] array = componentsInChildren;
		foreach (Renderer renderer in array)
		{
			if (renderer.enabled && renderer.gameObject.activeInHierarchy)
			{
				if (flag)
				{
					result = renderer.bounds;
					flag = false;
				}
				else
				{
					result.Encapsulate(renderer.bounds);
				}
			}
		}
		return result;
	}

	public static Texture2D Weapon(string id, int w = 256, int h = 112)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		string text = "w|" + id + "|" + w;
		if (cache.TryGetValue(text, out var value) && (bool)value)
		{
			return value;
		}
		if (!GameData.Weapons.TryGetValue(id, out var value2))
		{
			return null;
		}
		Rig();
		GameObject gameObject = FBWeaponModel.Make(value2, cam.transform.parent);
		if (!gameObject)
		{
			return null;
		}
		Collider[] componentsInChildren = gameObject.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Object.Destroy(componentsInChildren[i]);
		}
		Layer(gameObject);
		gameObject.transform.localPosition = Vector3.zero;
		gameObject.transform.localRotation = Quaternion.identity;
		gameObject.transform.localScale = Vector3.one;
		Bounds bounds = B(gameObject);
		Transform transform = null;
		Transform[] componentsInChildren2 = gameObject.GetComponentsInChildren<Transform>(includeInactive: true);
		foreach (Transform transform2 in componentsInChildren2)
		{
			if (transform2.name.StartsWith("SOCKET_Muzzle"))
			{
				transform = transform2;
				break;
			}
		}
		Vector3 fromDirection = (transform ? Vector3.ProjectOnPlane(transform.position - bounds.center, Vector3.up).normalized : Vector3.forward);
		if (fromDirection.sqrMagnitude < 0.5f)
		{
			fromDirection = Vector3.forward;
		}
		gameObject.transform.rotation = Quaternion.FromToRotation(fromDirection, Vector3.right) * gameObject.transform.rotation;
		Bounds bounds2 = B(gameObject);
		float num = (float)w / (float)h;
		cam.orthographic = true;
		cam.orthographicSize = Mathf.Max(bounds2.extents.y, bounds2.extents.x / num) * 1.08f;
		cam.transform.position = bounds2.center + new Vector3(0f, 0f, -5f);
		cam.transform.rotation = Quaternion.identity;
		key.transform.rotation = Quaternion.Euler(30f, -25f, 0f);
		Texture2D texture2D = Shoot(w, h);
		Object.DestroyImmediate(gameObject);
		texture2D.name = "icone_" + id;
		Mono(texture2D);
		return cache[text] = texture2D;
	}

	public static Texture2D Emote(string clipId, string charId, int size = 160)
	{
		if (string.IsNullOrEmpty(clipId))
		{
			return null;
		}
		string text = "e|" + clipId + "|" + charId + "|" + size;
		if (cache.TryGetValue(text, out var value) && (bool)value)
		{
			return value;
		}
		AnimationClip animationClip = (FBRegistry.I ? FBRegistry.I.Clip(clipId) : null);
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Char(charId) : null);
		if (!animationClip || !gameObject)
		{
			return null;
		}
		Rig();
		GameObject gameObject2 = Object.Instantiate(gameObject, cam.transform.parent);
		gameObject2.transform.localPosition = Vector3.zero;
		gameObject2.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
		MonoBehaviour[] componentsInChildren = gameObject2.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].enabled = false;
		}
		Collider[] componentsInChildren2 = gameObject2.GetComponentsInChildren<Collider>(includeInactive: true);
		for (int i = 0; i < componentsInChildren2.Length; i++)
		{
			Object.Destroy(componentsInChildren2[i]);
		}
		Layer(gameObject2);
		Animator componentInChildren = gameObject2.GetComponentInChildren<Animator>();
		if ((bool)componentInChildren)
		{
			PlayableGraph graph = PlayableGraph.Create("icone");
			AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "o", componentInChildren);
			AnimationClipPlayable animationClipPlayable = AnimationClipPlayable.Create(graph, animationClip);
			output.SetSourcePlayable(animationClipPlayable);
			animationClipPlayable.SetTime(animationClip.length * 0.4f);
			graph.Evaluate();
			graph.Destroy();
		}
		Bounds bounds = B(gameObject2);
		cam.orthographic = false;
		cam.fieldOfView = 26f;
		float num = bounds.extents.magnitude / Mathf.Tan(0.2268928f) * 1.02f;
		cam.transform.position = bounds.center + new Vector3(0f, bounds.extents.y * 0.05f, 0f - num);
		cam.transform.LookAt(bounds.center);
		key.transform.rotation = Quaternion.Euler(30f, -35f, 0f);
		Texture2D texture2D = Shoot(size, size);
		Object.DestroyImmediate(gameObject2);
		texture2D.name = "emote_" + clipId;
		return cache[text] = texture2D;
	}

	private static void Mono(Texture2D t)
	{
		Color32[] pixels = t.GetPixels32();
		for (int i = 0; i < pixels.Length; i++)
		{
			Color32 color = pixels[i];
			if (color.a < 8)
			{
				pixels[i] = new Color32(255, 255, 255, 0);
				continue;
			}
			byte b = (byte)Mathf.Lerp(205f, 255f, ((float)(int)color.r * 0.3f + (float)(int)color.g * 0.59f + (float)(int)color.b * 0.11f) / 255f);
			pixels[i] = new Color32(b, b, b, (byte)Mathf.Min(255f, (float)(int)color.a * 1.2f));
		}
		t.SetPixels32(pixels);
		t.Apply(updateMipmaps: false, makeNoLongerReadable: false);
	}

	public static void Clear()
	{
		foreach (Texture2D value in cache.Values)
		{
			if ((bool)value)
			{
				Object.Destroy(value);
			}
		}
		cache.Clear();
	}
}
}
