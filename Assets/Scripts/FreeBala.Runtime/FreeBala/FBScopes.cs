using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBScopes
{
	public static bool Scoped;

	private static GameObject ui;

	private static Image lens;

	private static float kick;

	public static float Zoom(WeaponDef w)
	{
		if (w != null)
		{
			return FBWeaponModel.Zoom(FBWeaponModel.Scope(w) ?? (w.IsSniper ? new Dictionary<string, object> { ["fieldOfViewMultiplierAim"] = 0.45 } : null));
		}
		return 1f;
	}

	public static float AdsZoom(WeaponDef w)
	{
		return Zoom(w);
	}

	public static Vector3 AdsOffset(GameObject vm, Camera cam)
	{
		Transform transform = null;
		Transform[] componentsInChildren = vm.GetComponentsInChildren<Transform>();
		foreach (Transform transform2 in componentsInChildren)
		{
			if (transform2.name.Contains("Scope") && transform2.gameObject.activeInHierarchy)
			{
				transform = transform2;
				break;
			}
		}
		Bounds bounds = new Bounds(vm.transform.position, Vector3.zero);
		Renderer[] componentsInChildren2 = vm.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren2)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		Vector3 position = (transform ? transform.position : (bounds.center + Vector3.up * bounds.extents.y * 0.55f));
		Vector3 vector = cam.transform.InverseTransformPoint(position) - vm.transform.localPosition;
		return new Vector3(0f - vector.x, 0f - vector.y, 0.3f);
	}

	public static void Update(Camera cam, WeaponDef w, bool aiming)
	{
		float num = Zoom(w);
		bool flag = aiming && num >= 2f && w != null;
		if (flag && !ui)
		{
			Build();
		}
		if ((bool)ui)
		{
			ui.SetActive(flag);
		}
		Scoped = flag;
		if (flag)
		{
			kick = Mathf.Max(0f, kick - Time.deltaTime * 6f);
			lens.rectTransform.localScale = Vector3.one * (1f + kick * 0.04f);
		}
	}

	public static void Kick()
	{
		kick = 1f;
	}

	private static void Build()
	{
		Canvas canvas = FBUI.Canvas("Luneta", 9);
		ui = canvas.gameObject;
		Object.DontDestroyOnLoad(ui);
		Texture2D texture2D = new Texture2D(512, 512, TextureFormat.RGBA32, mipChain: false);
		for (int i = 0; i < 512; i++)
		{
			for (int j = 0; j < 512; j++)
			{
				float num = (float)(j - 256) / 256f;
				float num2 = (float)(i - 256) / 256f;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				Color color = new Color(0f, 0f, 0f, (num3 > 0.98f) ? 1f : ((num3 > 0.86f) ? Mathf.InverseLerp(0.86f, 0.98f, num3) : 0f));
				if (num3 < 0.86f && ((Mathf.Abs(num) < 0.003f && Mathf.Abs(num2) > 0.02f) || (Mathf.Abs(num2) < 0.003f && Mathf.Abs(num) > 0.02f)))
				{
					color = new Color(0f, 0f, 0f, 0.9f);
				}
				if (num3 < 0.006f)
				{
					color = new Color(1f, 0.15f, 0.1f, 1f);
				}
				texture2D.SetPixel(j, i, color);
			}
		}
		texture2D.Apply();
		lens = FBUI.Panel(canvas.transform, "lente", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 900f), Color.white);
		lens.sprite = Sprite.Create(texture2D, new Rect(0f, 0f, 512f, 512f), Vector2.one * 0.5f);
		(Vector2, Vector2, Vector2)[] array = new(Vector2, Vector2, Vector2)[2]
		{
			(Vector2.zero, new Vector2(0.5f, 1f), new Vector2(-850f, 0f)),
			(new Vector2(0.5f, 0f), Vector2.one, new Vector2(850f, 0f))
		};
		for (int k = 0; k < array.Length; k++)
		{
			Vector2 item = array[k].Item3;
			FBUI.Panel(canvas.transform, "borda", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), item, new Vector2(800f, 0f), Color.black);
		}
	}
}
}
