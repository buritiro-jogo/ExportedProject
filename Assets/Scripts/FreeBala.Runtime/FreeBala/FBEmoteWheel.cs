using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBEmoteWheel
{
	private static GameObject go;

	private static List<string> ids;

	private static int sel = -1;

	private static Text[] labels;

	public static bool IsOpen
	{
		get
		{
			if ((bool)go)
			{
				return go.activeSelf;
			}
			return false;
		}
	}

	public static void Open()
	{
		FBCharacter player = FBMatch.Player;
		if (!player || !player.alive || player.special || ((bool)FBMatch.I && FBMatch.I.state != "play"))
		{
			return;
		}
		ids = FBEmotes.Equipped();
		if (ids.Count == 0)
		{
			return;
		}
		if ((bool)go)
		{
			UnityEngine.Object.Destroy(go);
		}
		Canvas canvas = FBUI.Canvas("RodaEmotes", 25);
		go = canvas.gameObject;
		labels = new Text[ids.Count];
		FBUI.Panel(canvas.transform, "anel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 520f), new Color(0f, 0f, 0f, 0.45f)).sprite = FBUI.CircleSprite;
		for (int i = 0; i < ids.Count; i++)
		{
			float f = (float)i / (float)ids.Count * (float)Math.PI * 2f;
			labels[i] = FBUI.Label(canvas.transform, FBEmotes.NameOf(ids[i]).ToUpper(), 14, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Sin(f), Mathf.Cos(f)) * 190f - new Vector2(0f, 46f), new Vector2(170f, 30f), TextAnchor.MiddleCenter, title: true);
			Texture2D texture2D = FBIcons.Emote(ids[i], FBSession.skin, 128);
			if ((bool)texture2D)
			{
				GameObject gameObject = new GameObject("icone", typeof(RectTransform), typeof(RawImage));
				RectTransform obj = (RectTransform)gameObject.transform;
				obj.SetParent(canvas.transform, worldPositionStays: false);
				Vector2 anchorMin = (obj.anchorMax = new Vector2(0.5f, 0.5f));
				obj.anchorMin = anchorMin;
				obj.anchoredPosition = new Vector2(Mathf.Sin(f), Mathf.Cos(f)) * 190f + new Vector2(0f, 8f);
				obj.sizeDelta = new Vector2(96f, 96f);
				RawImage component = gameObject.GetComponent<RawImage>();
				component.texture = texture2D;
				component.raycastTarget = false;
			}
		}
		sel = -1;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		go.AddComponent<FBEmoteWheelTicker>();
	}

	public static void Tick()
	{
		if (!IsOpen)
		{
			return;
		}
		Vector2 vector = Mouse.current.position.ReadValue() - new Vector2((float)Screen.width / 2f, (float)Screen.height / 2f);
		if (vector.magnitude > 60f)
		{
			float num = Mathf.Atan2(vector.x, vector.y);
			if (num < 0f)
			{
				num += (float)Math.PI * 2f;
			}
			sel = Mathf.RoundToInt(num / ((float)Math.PI * 2f) * (float)ids.Count) % ids.Count;
		}
		else
		{
			sel = -1;
		}
		for (int i = 0; i < labels.Length; i++)
		{
			labels[i].color = ((i == sel) ? FBUI.Gold : Color.white);
		}
	}

	public static void Close(bool apply)
	{
		if (!IsOpen)
		{
			return;
		}
		if (apply && sel >= 0)
		{
			FBCharacter player = FBMatch.Player;
			if ((bool)player && player.PlayDance(ids[sel]))
			{
				FBPlayer.I?.ForceThirdPerson();
				FBNet.I?.SendEvent("dance:" + ids[sel]);
			}
		}
		UnityEngine.Object.Destroy(go);
		go = null;
		if (!FBInput.Touch && (bool)FBMatch.I && FBMatch.I.state == "play")
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}
}
}
