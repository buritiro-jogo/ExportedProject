using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace FreeBala
{
public static class FBGarage
{
	private static GameObject ui;

	private static List<string> ids;

	private static int idx;

	private static FBCar prev;

	private static (Vector3 p, float yaw)? anchor;

	public static bool Open_ => ui;

	public static void Open()
	{
		if (!FBRole.spot.HasValue || !FBMatch.Player)
		{
			return;
		}
		ids = (from k in GameData.Vehicles.Keys
			where (bool)FBRegistry.I.Vehicle(k) && (Json.L(GameData.Vehicles[k], "wheels")?.Count ?? 0) > 0 && !k.ToLowerInvariant().Contains("viatura")
			orderby Json.S(GameData.Vehicles[k], "name") ?? k
			select k).ToList();
		Text nm;
		if (ids.Count != 0)
		{
			string @string = PlayerPrefs.GetString("fb_role_car", "S10_2017");
			idx = Mathf.Max(0, ids.IndexOf(@string));
			anchor = FreeAnchor(FBRole.spot.Value.p, FBRole.spot.Value.yaw);
			Canvas canvas = FBUI.Canvas("Garagem", 34);
			ui = canvas.gameObject;
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			Transform transform = FBUI.Panel(canvas.transform, "barra", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(720f, 130f), FBUI.Dark).transform;
			nm = FBUI.Label(transform, "", 26, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(520f, 36f), TextAnchor.MiddleCenter, title: true, FBUI.Gold);
			FBUI.Label(transform, "GARAGEM · ESCOLHA O CARRO DO ROLÊ", 12, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(520f, 18f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
			FBUI.Button(transform, "◀", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(50f, -10f), new Vector2(70f, 56f), delegate
			{
				idx = (idx - 1 + ids.Count) % ids.Count;
				Show();
			});
			FBUI.Button(transform, "▶", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-50f, -10f), new Vector2(70f, 56f), delegate
			{
				idx = (idx + 1) % ids.Count;
				Show();
			});
			FBUI.Button(transform, "PEGAR", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(240f, 50f), Pick, primary: true);
			Transform transform2 = FBUI.Panel(canvas.transform, "pecas", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 186f), new Vector2(520f, 50f), FBUI.Dark).transform;
			FBUI.Button(transform2, "PORTAS (U)", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(92f, 0f), new Vector2(166f, 40f), delegate
			{
				FBDoors.Toggle(prev, 15);
			});
			FBUI.Button(transform2, "CAPÔ (J)", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(166f, 40f), delegate
			{
				FBDoors.Toggle(prev, 32);
			});
			FBUI.Button(transform2, "MALA (P)", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-92f, 0f), new Vector2(166f, 40f), delegate
			{
				FBDoors.Toggle(prev, 16);
			});
			Show();
		}
		void Show()
		{
			nm.text = (Json.S(GameData.Vehicles[ids[idx]], "name") ?? ids[idx]).Replace('_', ' ').ToUpperInvariant();
			Preview(ids[idx]);
		}
	}

	private static void Preview(string id)
	{
		FBCharacter player = FBMatch.Player;
		if ((bool)prev)
		{
			if ((bool)player && FBVehicles.CarOf(player) == prev)
			{
				FBDrive.ReleaseSeat(player);
			}
			FBVehicles.list.Remove(prev);
			UnityEngine.Object.Destroy(prev.gameObject);
		}
		(Vector3, float) value = FBRole.spot.Value;
		object o = GameData.Vehicles[id];
		List<object> list = Json.L(o, "paint");
		uint value2 = ((list != null) ? (((uint)(Json.Fv(list[0]) * 255f) << 16) | ((uint)(Json.Fv(list[1]) * 255f) << 8) | (uint)(Json.Fv(list[2]) * 255f)) : 13224909u);
		List<string> list2 = (from x in Json.L(Json.O(o, "som"), "fit") ?? new List<object>()
			select x as string into x
			where FBRegistry.I.Som(x)
			select x).ToList();
		(Vector3, float) tuple = anchor ?? (value.Item1, value.Item2);
		prev = FBVehicles.Make(id, tuple.Item1, tuple.Item2, civil: true, () => 0.5f, true, value2, list2.Count > 0, list2.FirstOrDefault((string m) => m.Contains("Grave") || m.Contains("Trio")) ?? list2.FirstOrDefault());
		if ((bool)prev)
		{
			prev.syncId = "role:" + id;
			FBTune.Apply(prev, FBTune.Load(id));
		}
		if ((bool)prev)
		{
			Vector3 vector = prev.transform.TransformPoint(prev.lbox.center);
			Vector3 vector2 = tuple.Item1 - vector;
			vector2.y = 0f;
			prev.transform.position += vector2;
			Physics.SyncTransforms();
			FBRole.spot = (tuple.Item1, tuple.Item2);
			if ((bool)player && player.alive)
			{
				FBVehicles.fp = false;
				FBDrive.Enter(player, prev);
			}
		}
	}

	private static (Vector3 p, float yaw) FreeAnchor(Vector3 p0, float yaw)
	{
		Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
		Vector3 ext = new Vector3(1.35f, 0.6f, 3.4f);
		if (Free(p0))
		{
			return (p: p0, yaw: yaw);
		}
		for (int i = 0; i < 64; i++)
		{
			float f = (float)i * 137.5f * ((float)Math.PI / 180f);
			float num = 2.5f + (float)i * 0.35f;
			Vector3 vector = p0 + new Vector3(Mathf.Cos(f) * num, 0f, Mathf.Sin(f) * num);
			float? num2 = FBWorld.GroundY(vector.x, p0.y + 1.5f, vector.z, 0.3f);
			if (!num2.HasValue)
			{
				continue;
			}
			float valueOrDefault = num2.GetValueOrDefault();
			if (!(Mathf.Abs(valueOrDefault - p0.y) > 1.2f))
			{
				vector.y = valueOrDefault;
				if (Free(vector))
				{
					return (p: vector, yaw: yaw);
				}
			}
		}
		return (p: p0, yaw: yaw);
		bool Free(Vector3 q)
		{
			Collider[] array = Physics.OverlapBox(q + Vector3.up * 0.95f, ext, rot, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
			foreach (Collider collider in array)
			{
				if (!collider.GetComponentInParent<FBCar>() && !collider.GetComponentInParent<FBCharacter>())
				{
					return false;
				}
			}
			return true;
		}
	}

	private static bool Blocked(FBCar c, Vector3 p, Quaternion r)
	{
		Bounds lbox = c.lbox;
		Vector3 halfExtents = Vector3.Scale(lbox.extents, Vector3.one * c.scale) * 0.9f;
		halfExtents.y = Mathf.Max(0.1f, halfExtents.y - 0.35f);
		Collider[] array = Physics.OverlapBox(p + r * (lbox.center * c.scale) + Vector3.up * 0.3f, halfExtents, r, FBWorld.SolidMask, QueryTriggerInteraction.Ignore);
		for (int i = 0; i < array.Length; i++)
		{
			if (!array[i].transform.IsChildOf(c.transform))
			{
				return true;
			}
		}
		return false;
	}

	public static bool Camera(Camera cam, float dt)
	{
		if (!ui || !prev || !cam)
		{
			return false;
		}
		if (!FBInput.Touch && (Cursor.lockState != 0 || !Cursor.visible))
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
		Bounds lbox = prev.lbox;
		float num = Mathf.Max(lbox.size.x, lbox.size.z) * prev.scale * 0.52f + lbox.size.y * prev.scale * 0.45f;
		Vector3 vector = prev.transform.TransformPoint(lbox.center);
		Vector3 b = vector + prev.transform.rotation * (Quaternion.Euler(0f, 36f, 0f) * new Vector3(0f, num * 0.32f, num * 1.9f));
		cam.transform.position = Vector3.Lerp(cam.transform.position, b, 1f - Mathf.Exp((0f - dt) * 6f));
		cam.transform.rotation = Quaternion.LookRotation(vector - Vector3.up * lbox.size.y * prev.scale * 0.06f - cam.transform.position);
		cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 40f, 1f - Mathf.Exp((0f - dt) * 6f));
		return true;
	}

	public static void AutoPick()
	{
		if ((bool)ui)
		{
			Pick();
		}
	}

	private static void Pick()
	{
		if ((bool)prev)
		{
			PlayerPrefs.SetString("fb_role_car", prev.id);
			FBRole.roleCar = prev;
			FBCar fBCar = prev;
			prev = null;
			UnityEngine.Object.Destroy(ui);
			ui = null;
			if (!FBInput.Touch)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
			FBVehicles.fp = false;
			if (FBVehicles.CarOf(FBMatch.Player) != fBCar)
			{
				FBDrive.Enter(FBMatch.Player, fBCar);
			}
			if ((bool)FBPlayer.I)
			{
				FBPlayer.I.camYaw = fBCar.transform.eulerAngles.y;
				FBPlayer.I.camPitch = -7f;
			}
			FBHUD.I?.Center("BOM ROLÊ! · G = GPS · K = SOM · N = LIGA O PAREDÃO", 3.5f);
		}
	}
}
}
