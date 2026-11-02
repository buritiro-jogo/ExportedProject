using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBPole : MonoBehaviour
{
	public Vector3 basePos;

	public float height = 6f;

	[NonSerialized]
	public float tilt;

	[NonSerialized]
	public bool falling;

	[NonSerialized]
	public bool down;

	public static readonly List<FBPole> All = new List<FBPole>();

	private Vector3 axis;

	private float w;

	private Quaternion rot0;

	private Vector3 pos0;

	private bool init;

	private Collider[] cols;

	private readonly List<int> lamps = new List<int>();

	private Vector3 lastTop;

	private Vector3 Top => basePos + Quaternion.AngleAxis(tilt, (axis == Vector3.zero) ? Vector3.right : axis) * Vector3.up * height;

	private void OnEnable()
	{
		if (!All.Contains(this))
		{
			All.Add(this);
		}
	}

	private void OnDisable()
	{
		All.Remove(this);
	}

	private void Init()
	{
		if (init)
		{
			return;
		}
		init = true;
		rot0 = base.transform.rotation;
		pos0 = base.transform.position;
		cols = GetComponentsInChildren<Collider>(includeInactive: true);
		Bounds bounds = new Bounds(basePos + Vector3.up * height * 0.5f, new Vector3(1f, height, 1f));
		Renderer[] componentsInChildren = GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			bounds.Encapsulate(renderer.bounds);
		}
		bounds.Expand(0.8f);
		IReadOnlyList<FBStreetLights.Lamp> readOnlyList = FBStreetLights.Lamps;
		for (int j = 0; j < readOnlyList.Count; j++)
		{
			if (bounds.Contains(readOnlyList[j].p))
			{
				lamps.Add(j);
			}
		}
	}

	public void KillLights(float flicker, bool hideGlow)
	{
		Init();
		foreach (int lamp in lamps)
		{
			FBStreetLights.Kill(lamp, flicker * UnityEngine.Random.Range(0.7f, 1.3f), hideGlow);
		}
	}

	public bool Hit(Vector3 dir, float imp, Vector3 at)
	{
		Init();
		FBStaticMerge.Break(this);
		if (down || falling)
		{
			return true;
		}
		dir.y = 0f;
		if (dir.sqrMagnitude < 0.0001f)
		{
			return false;
		}
		dir.Normalize();
		if (axis == Vector3.zero || tilt < 1f)
		{
			axis = Vector3.Cross(Vector3.up, dir).normalized;
		}
		FBFx.Sparks(at, -dir);
		FBAudio.PlayAt("reb_carimpact" + ((imp > 9f) ? 4 : 3), at, Mathf.Min(1f, 0.5f + imp / 20f), 8f);
		if (imp > 7.5f || tilt > 22f)
		{
			falling = true;
			w = Mathf.Max(w, imp * 2.5f);
			KillLights(0.6f, hideGlow: true);
			Collider[] array = cols;
			foreach (Collider collider in array)
			{
				if ((bool)collider)
				{
					collider.enabled = false;
				}
			}
			lastTop = Top;
			return true;
		}
		tilt = Mathf.Min(tilt + Mathf.Lerp(4f, 18f, Mathf.InverseLerp(3.5f, 7.5f, imp)), 24f);
		Apply();
		KillLights(1.2f, hideGlow: false);
		return false;
	}

	private void Apply()
	{
		Quaternion quaternion = Quaternion.AngleAxis(tilt, axis);
		base.transform.SetPositionAndRotation(basePos + quaternion * (pos0 - basePos), quaternion * rot0);
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBPole.Update");

	private void Update()
	{
		if (!FBTempo.On)
		{
			Update__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		Update__fbt();
		FBTempo.Soma(__fbt_Update, t0);
	}

	private void Update__fbt()
	{
		if (falling)
		{
			float deltaTime = Time.deltaTime;
			w += 14.715f / Mathf.Max(2f, height) * Mathf.Sin(Mathf.Max(tilt, 4f) * ((float)Math.PI / 180f)) * 57.29578f * deltaTime;
			float num = Mathf.Min(89f, tilt + w * deltaTime);
			Vector3 top = Top;
			tilt = num;
			Vector3 top2 = Top;
			bool flag = tilt >= 89f;
			Vector3 vector = top2 - top;
			float magnitude = vector.magnitude;
			if (magnitude > 0.001f && Physics.SphereCast(top, 0.15f, vector / magnitude, out var hitInfo, magnitude, FBWorld.SolidMask, QueryTriggerInteraction.Ignore) && !hitInfo.transform.IsChildOf(base.transform))
			{
				flag = true;
				tilt -= w * deltaTime * 0.5f;
			}
			Apply();
			if (flag)
			{
				falling = false;
				down = true;
				Vector3 top3 = Top;
				FBAudio.PlayAt("reb_carimpact2", top3, 0.9f, 10f);
				FBFx.Puff(top3, new Color(0.55f, 0.52f, 0.48f), 1.1f, 1.4f);
				FBFx.Puff(Vector3.Lerp(basePos, top3, 0.5f), new Color(0.55f, 0.52f, 0.48f), 0.9f, 1.2f, 0.5f);
			}
		}
	}
}
}
