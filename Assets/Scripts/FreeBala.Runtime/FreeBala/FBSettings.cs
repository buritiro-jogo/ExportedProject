using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBSettings
{
	private static void Credits(Transform parent)
	{
		TextAsset textAsset = Resources.Load<TextAsset>("creditos");
		Image bg = FBUI.Panel(parent, "creditos", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.03f, 0.04f, 0.96f));
		FBUI.Label(bg.transform, textAsset ? textAsset.text : "", 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(900f, 560f), TextAnchor.UpperLeft, title: false, FBKit.Txt);
		FBUI.Button(bg.transform, "VOLTAR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(240f, 48f), delegate
		{
			UnityEngine.Object.Destroy(bg.gameObject);
		}, primary: true);
	}

	public static void Open(Transform parent)
	{
		Image bg = FBUI.Panel(parent, "ajustes", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.8f));
		Transform transform = FBUI.Panel(bg.transform, "painel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 600f), FBKit.Bg).transform;
		transform.gameObject.AddComponent<Outline>().effectColor = FBKit.Line;
		_ = (RectTransform)transform;
		RectTransform rectTransform = FBUI.Rect(transform, "o", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
		rectTransform.pivot = new Vector2(0f, 1f);
		Transform o = rectTransform.transform;
		FBKit.T(o, "AJUSTES", 24, 32f, 22f, 400f, 34f, TextAnchor.MiddleLeft, title: true, Color.white);
		FBKit.Box(o, "fio", 32f, 66f, 916f, 1f, FBKit.Line, border: false);
		float[] colX = new float[2] { 32f, 506f };
		float[] colY = new float[2] { 112f, 112f };
		FBKit.Section(o, "CONTROLES", colX[0], 84f, 442f);
		FBKit.Section(o, "ÁUDIO E VÍDEO", colX[1], 84f, 442f);
		Slider(0, "SENSIBILIDADE", FBSession.sens, delegate(float v)
		{
			FBSession.sens = v;
		});
		Slider(0, "SENSIB. MIRANDO", FBSession.adsSens, delegate(float v)
		{
			FBSession.adsSens = v;
		});
		Slider(0, "CAMPO DE VISÃO", FBSession.fov, delegate(float v)
		{
			FBSession.fov = v;
		});
		Choice(0, "TIPO DE MIRA", new string[3] { "PADRÃO (ORIGINAL)", "FREE FIRE", "MANUAL" }, (FBSession.aimAssist == "ff") ? 1 : ((FBSession.aimAssist == "manual") ? 2 : 0), delegate(int i)
		{
			FBSession.aimAssist = (new string[3] { "assist", "ff", "manual" })[i];
		});
		Choice(0, "CORRIDA", new string[2] { "AUTOMÁTICA (SHIFT)", "SEGURAR SHIFT" }, FBSession.holdRun ? 1 : 0, delegate(int i)
		{
			FBSession.holdRun = i == 1;
		});
		Choice(0, "INVERTER Y", new string[2] { "NÃO", "SIM" }, FBSession.invertY ? 1 : 0, delegate(int i)
		{
			FBSession.invertY = i == 1;
		});
		Choice(0, "CÂMERA LIVRE", new string[2] { "SEGURAR ALT / OLHAR", "ALTERNAR ALT / OLHAR" }, FBSession.freeLookToggle ? 1 : 0, delegate(int i)
		{
			FBSession.freeLookToggle = i == 1;
		});
		Slider(1, "VOLUME GERAL", FBSession.master, delegate(float v)
		{
			FBSession.master = v;
		});
		Slider(1, "MÚSICA", FBSession.music, delegate(float v)
		{
			FBSession.music = v;
		});
		Choice(1, "QUALIDADE", new string[1] { "AUTO" }.Concat(FBQuality.Names).ToArray(), FBQuality.Choice + 1, delegate(int i)
		{
			FBQuality.Set(i - 1);
		});
		Choice(1, "SANGUE", new string[2] { "LIGADO", "DESLIGADO" }, (!FBSession.blood) ? 1 : 0, delegate(int i)
		{
			FBSession.blood = i == 0;
		});
		FBKit.Box(o, "fio2", 32f, 520f, 916f, 1f, FBKit.Line, border: false);
		FBKit.TextTile(o, "CRÉDITOS", 32f, 536f, 180f, 44f, sel: false, delegate
		{
			Credits(bg.transform);
		}, 13);
		FBKit.Primary(o, "VOLTAR", 728f, 536f, 220f, 44f, delegate
		{
			UnityEngine.Object.Destroy(bg.gameObject);
		});
		void Choice(int col, string label, string[] opts, int cur, Action<int> set)
		{
			float num = colX[col];
			float num2 = colY[col];
			colY[col] += 52f;
			FBKit.T(o, label, 13, num, num2, 200f, 36f, TextAnchor.MiddleLeft, title: true, FBKit.Txt);
			int c = cur;
			Text t = null;
			Button button = FBKit.Tile(o, "op", num + 200f, num2 + 2f, 242f, 32f, sel: false, null);
			FBKit.T(button.transform, "‹", 14, 8f, 0f, 20f, 32f, TextAnchor.MiddleLeft, title: true, FBKit.Dim);
			FBKit.T(button.transform, "›", 14, 214f, 0f, 20f, 32f, TextAnchor.MiddleRight, title: true, FBKit.Gold);
			t = FBKit.T(button.transform, opts[c], 12, 24f, 0f, 194f, 32f, TextAnchor.MiddleCenter, title: true, Color.white);
			button.onClick.AddListener(delegate
			{
				c = (c + 1) % opts.Length;
				t.text = opts[c];
				set(c);
				FBSession.Save();
			});
		}
		void Slider(int col, string label, float v, Action<float> set)
		{
			float num3 = colX[col];
			float num4 = colY[col];
			colY[col] += 52f;
			FBKit.T(o, label, 13, num3, num4, 200f, 36f, TextAnchor.MiddleLeft, title: true, FBKit.Txt);
			Text val = FBKit.T(o, Mathf.RoundToInt(v * 100f) + "%", 12, num3 + 442f - 50f, num4, 50f, 36f, TextAnchor.MiddleRight, title: true, FBKit.Dim);
			Slider component = new GameObject("s", typeof(RectTransform), typeof(Slider)).GetComponent<Slider>();
			RectTransform obj = (RectTransform)component.transform;
			obj.SetParent(o, worldPositionStays: false);
			Vector2 anchorMin = (obj.anchorMax = new Vector2(0f, 1f));
			obj.anchorMin = anchorMin;
			obj.pivot = new Vector2(0f, 0.5f);
			obj.anchoredPosition = new Vector2(num3 + 200f, 0f - num4 - 18f);
			obj.sizeDelta = new Vector2(182f, 6f);
			FBUI.Panel(obj, "bg", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.12f));
			Image image = FBUI.Panel(FBUI.Rect(obj, "fa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), "f", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, FBKit.Gold);
			Image image2 = FBUI.Panel(FBUI.Rect(obj, "ha", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16f, 0f)), "h", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(16f, 16f), Color.white);
			image2.sprite = FBUI.CircleSprite;
			component.fillRect = image.rectTransform;
			component.handleRect = image2.rectTransform;
			component.targetGraphic = image2;
			component.value = v;
			component.onValueChanged.AddListener(delegate(float x2)
			{
				set(x2);
				val.text = Mathf.RoundToInt(x2 * 100f) + "%";
				FBSession.Save();
			});
		}
	}
}
}
