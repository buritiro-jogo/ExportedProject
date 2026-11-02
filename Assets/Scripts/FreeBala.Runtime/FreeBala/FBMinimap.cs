using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FreeBala
{
public static class FBMinimap
{
	public static Texture2D Tex;

	public static float Size = 400f;

	private static Vector3 center;

	public static Vector2 UV(Vector3 p)
	{
		return new Vector2((p.x - center.x) / Size + 0.5f, (p.z - center.z) / Size + 0.5f);
	}

	public static void Capture()
	{
		Tex = null;
		List<MeshCollider> list = (from c in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None)
			where c.gameObject.layer == 8
			select c).ToList();
		if (list.Count == 0)
		{
			return;
		}
		Bounds bounds = list[0].bounds;
		foreach (MeshCollider item in list)
		{
			bounds.Encapsulate(item.bounds);
		}
		center = bounds.center;
		Size = Mathf.Min(Mathf.Max(bounds.size.x, bounds.size.z), 900f);
		int num = (FBQuality.Mobile ? 1024 : 2048);
		Light[] array = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
		List<(Light, bool, float, Quaternion, LightShadows)> list2 = array.Select((Light l) => (l: l, enabled: l.enabled, intensity: l.intensity, rotation: l.transform.rotation, shadows: l.shadows)).ToList();
		(AmbientMode, Color, bool, float) tuple = (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.fog, RenderSettings.ambientIntensity);
		Light[] array2 = array;
		foreach (Light light in array2)
		{
			if (light.type == LightType.Directional)
			{
				light.enabled = true;
				light.intensity = 1.1f;
				light.transform.rotation = Quaternion.Euler(70f, -30f, 0f);
				light.shadows = LightShadows.None;
			}
			else
			{
				light.enabled = false;
			}
		}
		RenderSettings.ambientMode = AmbientMode.Flat;
		RenderSettings.ambientLight = new Color(0.62f, 0.64f, 0.66f);
		RenderSettings.fog = false;
		GameObject gameObject = new GameObject("CamRadar");
		Camera camera = gameObject.AddComponent<Camera>();
		camera.orthographic = true;
		camera.orthographicSize = Size / 2f;
		camera.transform.SetPositionAndRotation(new Vector3(center.x, bounds.max.y + 50f, center.z), Quaternion.Euler(90f, 0f, 0f));
		camera.farClipPlane = bounds.size.y + 200f;
		camera.clearFlags = CameraClearFlags.Color;
		camera.backgroundColor = new Color(0.1f, 0.12f, 0.14f);
		camera.cullingMask = 257;
		camera.allowHDR = false;
		UniversalAdditionalCameraData universalAdditionalCameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
		universalAdditionalCameraData.renderPostProcessing = false;
		universalAdditionalCameraData.renderShadows = false;
		RenderTexture renderTexture2 = (camera.targetTexture = new RenderTexture(num, num, 24));
		camera.Render();
		RenderTexture.active = renderTexture2;
		Tex = new Texture2D(num, num, TextureFormat.RGB24, mipChain: true)
		{
			anisoLevel = 4,
			wrapMode = TextureWrapMode.Clamp
		};
		Tex.ReadPixels(new Rect(0f, 0f, num, num), 0, 0);
		RenderTexture.active = null;
		Color32[] pixels = Tex.GetPixels32();
		for (int j = 0; j < pixels.Length; j++)
		{
			Color32 color = pixels[j];
			float num2 = ((float)(int)color.r * 0.3f + (float)(int)color.g * 0.59f + (float)(int)color.b * 0.11f) / 255f;
			Color color2 = Color.Lerp(new Color(num2, num2, num2), new Color((float)(int)color.r / 255f, (float)(int)color.g / 255f, (float)(int)color.b / 255f), 0.8f);
			color2 = (color2 - Color.gray * 0.5f) * 1.18f + Color.gray * 0.5f;
			color2.a = 1f;
			pixels[j] = color2;
		}
		Tex.SetPixels32(pixels);
		Tex.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		camera.targetTexture = null;
		Object.Destroy(renderTexture2);
		Object.Destroy(gameObject);
		foreach (var (light2, enabled, intensity, rotation, shadows) in list2)
		{
			if ((bool)light2)
			{
				light2.enabled = enabled;
				light2.intensity = intensity;
				light2.transform.rotation = rotation;
				light2.shadows = shadows;
			}
		}
		(RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.fog, RenderSettings.ambientIntensity) = tuple;
	}
}
}
