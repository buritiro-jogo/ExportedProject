using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBGps
{
	private static GameObject big;

	public static bool BigOn => big;

	public static void ToggleBig()
	{
		if ((bool)big)
		{
			Object.Destroy(big);
			big = null;
			if (!FBInput.Touch)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			return;
		}
		Canvas canvas = FBUI.Canvas("GPS", 28);
		big = canvas.gameObject;
		Image image = FBUI.Panel(canvas.transform, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.8f));
		if ((bool)FBMinimap.Tex)
		{
			Image image2 = FBUI.Panel(image.transform, "mapa", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 620f), Color.white);
			image2.sprite = Sprite.Create(FBMinimap.Tex, new Rect(0f, 0f, FBMinimap.Tex.width, FBMinimap.Tex.height), Vector2.one * 0.5f);
			FBCharacter player = FBMatch.Player;
			if ((bool)player)
			{
				Vector2 vector = FBMinimap.UV(player.Pos);
				Image image3 = FBUI.Panel(image2.transform, "eu", Vector2.zero, Vector2.zero, new Vector2(vector.x * 760f, vector.y * 760f), new Vector2(16f, 16f), FBUI.Gold);
				image3.sprite = FBUI.ArrowSprite;
				image3.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (0f - FBPlayer.CamYawRad) * 57.29578f);
			}
		}
		FBUI.Label(image.transform, "GPS · G FECHA", 18, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(400f, 30f), TextAnchor.MiddleCenter, title: true, FBUI.Gold);
	}
}
}
