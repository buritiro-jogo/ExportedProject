using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public static class FBMarks
{
	private static readonly List<(GameObject g, float t)> marks = new List<(GameObject, float)>();

	public static void Mark(Camera cam)
	{
		if (!cam)
		{
			return;
		}
		bool flag = false;
		if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hitInfo, 300f, FBWorld.ShotMask, QueryTriggerInteraction.Collide))
		{
			return;
		}
		Vector3 point = hitInfo.point;
		FBHitbox component = hitInfo.collider.GetComponent<FBHitbox>();
		flag = (bool)component && FBMatch.Hostile(component.owner, FBMatch.Player);
		if (flag)
		{
			component.owner.tagT = 8f;
		}
		GameObject gameObject = FBMarker.Make(point, flag ? new Color(1f, 0.25f, 0.2f) : FBUI.Gold, 0.8f, 30f).gameObject;
		marks.Add((gameObject, Time.time));
		FBAudio.Synth(flag ? "mark_enemy" : "mark");
		for (int num = marks.Count - 1; num >= 0; num--)
		{
			if (!marks[num].g || Time.time - marks[num].t > 8f || marks.Count > 4)
			{
				if ((bool)marks[num].g)
				{
					Object.Destroy(marks[num].g);
				}
				marks.RemoveAt(num);
			}
		}
	}

	public static void Clear()
	{
		foreach (var mark in marks)
		{
			if ((bool)mark.g)
			{
				Object.Destroy(mark.g);
			}
		}
		marks.Clear();
	}
}
}
