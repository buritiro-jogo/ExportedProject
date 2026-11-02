using System;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBGrauHud
{
	private static GameObject root;

	private static Text g1;

	private static Text g2;

	private static RectTransform marker;

	private static GameObject bar;

	public static void Show(FBCar car)
	{
		if (!root)
		{
			Canvas canvas = FBUI.Canvas("Grau", 31);
			root = canvas.gameObject;
			g1 = FBUI.Label(canvas.transform, "", 30, new Vector2(0.5f, 0.83f), new Vector2(0.5f, 0.83f), Vector2.zero, new Vector2(500f, 40f), TextAnchor.MiddleCenter, title: true);
			g2 = FBUI.Label(canvas.transform, "", 15, new Vector2(0.5f, 0.83f), new Vector2(0.5f, 0.83f), new Vector2(0f, -30f), new Vector2(500f, 24f), TextAnchor.MiddleCenter, title: false, new Color(1f, 1f, 1f, 0.85f));
			Image image = FBUI.Panel(canvas.transform, "barra", new Vector2(0.5f, 0.83f), new Vector2(0.5f, 0.83f), new Vector2(0f, -52f), new Vector2(190f, 9f), new Color(0f, 0f, 0f, 0.55f));
			bar = image.gameObject;
			float num = 1.1362092f;
			Image image2 = FBUI.Panel(image.transform, "zona", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Color(0.21f, 0.82f, 0.35f, 0.55f));
			image2.rectTransform.anchorMin = new Vector2((float)Math.PI * 4f / 25f / num, 0f);
			image2.rectTransform.anchorMax = new Vector2(0.89360857f / num, 1f);
			RectTransform rectTransform = image2.rectTransform;
			Vector2 offsetMin = (image2.rectTransform.offsetMax = Vector2.zero);
			rectTransform.offsetMin = offsetMin;
			Image image3 = FBUI.Panel(image.transform, "vermelho", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.94f, 0.24f, 0.24f, 0.6f));
			image3.rectTransform.anchorMin = new Vector2(1.0821041f / num, 0f);
			RectTransform rectTransform2 = image3.rectTransform;
			offsetMin = (image3.rectTransform.offsetMax = Vector2.zero);
			rectTransform2.offsetMin = offsetMin;
			marker = FBUI.Panel(image.transform, "m", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 13f), Color.white).rectTransform;
		}
		FBMoto.Wheelie wh = car.wh;
		bool flag = wh.a > 0.08f;
		(string, string, bool, float)? grauMsg = car.grauMsg;
		(string, string, bool, float)? obj;
		if (grauMsg.HasValue)
		{
			(string, string, bool, float) valueOrDefault = grauMsg.GetValueOrDefault();
			if (Time.time < valueOrDefault.Item4)
			{
				obj = valueOrDefault;
				goto IL_0331;
			}
		}
		obj = null;
		goto IL_0331;
		IL_0331:
		(string, string, bool, float)? tuple = obj;
		if (!flag && !tuple.HasValue)
		{
			root.SetActive(value: false);
			return;
		}
		root.SetActive(value: true);
		bar.SetActive(flag);
		if (flag)
		{
			g1.text = "GRAU " + wh.dist.ToString("0.0").Replace('.', ',') + " m";
			g2.text = ((wh.best > 0f) ? ("RECORDE " + wh.best.ToString("0.0").Replace('.', ',') + " m") : "ESPAÇO + W · FREIE (S) PARA SEGURAR");
			float num2 = 1.1362092f;
			RectTransform rectTransform3 = marker;
			Vector2 offsetMin = (marker.anchorMax = new Vector2(Mathf.Clamp01(wh.a / num2) * 0.98f, 0.5f));
			rectTransform3.anchorMin = offsetMin;
			g1.color = ((wh.a > 0.8237953f) ? new Color(1f, 0.42f, 0.42f) : Color.white);
		}
		else
		{
			(string, string, bool, float) value = tuple.Value;
			g1.text = value.Item1;
			g2.text = value.Item2 ?? "";
			g1.color = (value.Item3 ? new Color(1f, 0.42f, 0.42f) : new Color(1f, 0.82f, 0.29f));
		}
	}

	public static void Hide()
	{
		if ((bool)root)
		{
			root.SetActive(value: false);
		}
	}
}
}
