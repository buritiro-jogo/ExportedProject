using System;
using UnityEngine;

namespace FreeBala
{
public class FBTarget : MonoBehaviour
{
	public int hits;

	public int heads;

	public float headY = 0.78f;

	public Action<FBTarget, bool> onHit;

	public bool IsHead(Vector3 p)
	{
		return base.transform.InverseTransformPoint(p).y > headY;
	}

	public void Hit(Vector3 p, float dist)
	{
		bool flag = IsHead(p);
		hits++;
		if (flag)
		{
			heads++;
		}
		FBMatch.Stats.hits++;
		onHit?.Invoke(this, flag);
	}
}
}
