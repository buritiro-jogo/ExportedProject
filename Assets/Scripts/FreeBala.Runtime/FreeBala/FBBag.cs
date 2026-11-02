using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBBag
{
	private static GameObject go;

	public static bool Open
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

	public static void Toggle()
	{
		if (Open)
		{
			go.SetActive(value: false);
			if (!FBInput.Touch)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			return;
		}
		FBCharacter player = FBMatch.Player;
		if (!player || !player.alive)
		{
			return;
		}
		if ((bool)go)
		{
			Object.Destroy(go);
		}
		Canvas canvas = FBUI.Canvas("Mochila", 30);
		go = canvas.gameObject;
		Transform transform = FBUI.Panel(FBUI.Panel(canvas.transform, "fundo", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.7f)).transform, "painel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 440f), FBUI.Dark).transform;
		FBUI.Label(transform, "MOCHILA", 36, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(400f, 50f), TextAnchor.MiddleCenter, title: true, FBUI.Gold);
		FBWeaponUser wu = player.GetComponent<FBWeaponUser>();
		string[] array = new string[3] { "FUZIL", "SMG", "PISTOLA" };
		for (int j = 0; j < 3; j++)
		{
			int i = j;
			WeaponDef weaponDef = wu.slots[j];
			Image image = FBUI.Panel(transform, "slot", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-280 + j * 280, -170f), new Vector2(260f, 140f), (wu.cur == j) ? new Color(0.96f, 0.67f, 0.09f, 0.25f) : new Color(1f, 1f, 1f, 0.06f));
			FBUI.Label(image.transform, array[j], 12, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -16f), new Vector2(-16f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
			FBUI.Label(image.transform, (weaponDef != null) ? weaponDef.name.ToUpper() : "—", 20, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(-16f, 30f), TextAnchor.MiddleLeft, title: true);
			if (weaponDef != null)
			{
				FBUI.Label(image.transform, $"{wu.ammo[j]} / {wu.reserve[j]}", 14, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 18f), new Vector2(-16f, 20f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
				image.gameObject.AddComponent<Button>().onClick.AddListener(delegate
				{
					wu.SwitchTo(i);
					Toggle();
				});
			}
		}
		FBUI.Button(transform, "KIT MÉDICO (" + (FBMed.inf ? "∞" : FBMed.kits.ToString()) + ") · USAR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(380f, 56f), delegate
		{
			Toggle();
			FBPlayer.I?.SendMessage("TryHeal", SendMessageOptions.DontRequireReceiver);
		}, primary: true);
		FBUI.Button(transform, "FECHAR (I)", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(240f, 48f), Toggle);
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}
}
}
