using System;
using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBLobbyDirector : MonoBehaviour
{
	private static readonly (string a, float w)[] LONG = new(string, float)[7]
	{
		("inspect", 4f),
		("check", 3f),
		("holster", 2.4f),
		("reload", 1.6f),
		("look", 2.2f),
		("cam", 1.8f),
		("emote", 1.1f)
	};

	private static readonly (string a, float w)[] PISTOL = new(string, float)[5]
	{
		("check", 3.2f),
		("holster", 3f),
		("look", 2.4f),
		("cam", 2.2f),
		("emote", 1.2f)
	};

	private static readonly (string a, float w)[] BARE = new(string, float)[3]
	{
		("look", 3f),
		("cam", 2.5f),
		("emote", 2.2f)
	};

	public bool home = true;

	public Camera cam;

	private FBCharacter ch;

	private Animator an;

	private Transform head;

	private Transform neck;

	private float t;

	private float k;

	private float len;

	private float at;

	private string act;

	private string last;

	private bool drew;

	private void Start()
	{
		ch = GetComponent<FBCharacter>();
		an = GetComponent<Animator>();
		t = 2.5f + UnityEngine.Random.value * 3f;
		if ((bool)an && an.isHuman)
		{
			head = an.GetBoneTransform(HumanBodyBones.Head);
			neck = an.GetBoneTransform(HumanBodyBones.Neck);
		}
		if (!cam)
		{
			cam = Camera.main;
		}
		if ((bool)ch && ch.weapon != null)
		{
			ch.SetOverride("Rifle Idle", "ANIM-Rifle_Idle");
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBLobbyDirector.Update");

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
		if (!ch)
		{
			return;
		}
		float deltaTime = Time.deltaTime;
		if (act != null)
		{
			k += deltaTime;
			if (act == "holster" && !drew && k >= at)
			{
				drew = true;
				ch.DrawAnim();
			}
			if (k >= len)
			{
				last = act;
				act = null;
				t = 4.5f + UnityEngine.Random.value * 4.5f;
			}
		}
		else
		{
			if ((t -= deltaTime) > 0f || !home)
			{
				return;
			}
			(string a, float w)[] obj = ((ch.weapon == null) ? BARE : (ch.weapon.IsPistol ? PISTOL : LONG));
			List<(string, float)> list = new List<(string, float)>();
			float num = 0f;
			(string, float)[] array = obj;
			for (int i = 0; i < array.Length; i++)
			{
				(string, float) item = array[i];
				if (item.Item1 != last)
				{
					list.Add(item);
					num += item.Item2;
				}
			}
			float num2 = UnityEngine.Random.value * num;
			string text = list[0].Item1;
			foreach (var item2 in list)
			{
				num2 -= item2.Item2;
				if (num2 <= 0f)
				{
					(text, _) = item2;
					break;
				}
			}
			if (text == "emote")
			{
				string text2 = FBEmotes.RandomShort(ch.IsFemale);
				if (text2 != null && ch.PlayEmote(text2))
				{
					last = "emote";
					t = 6f + UnityEngine.Random.value * 4f;
					return;
				}
				text = "look";
			}
			act = text;
			k = 0f;
			drew = false;
			if (act == "inspect" || act == "reload")
			{
				ch.Trigger(act);
				len = ((act == "inspect") ? 3.2f : 2.6f);
			}
			else if (act == "holster")
			{
				float num3 = ch.HolsterAnim();
				if (num3 <= 0f)
				{
					act = null;
					t = 1f;
				}
				else
				{
					at = num3 + 0.35f;
					len = at + 1.2f;
				}
			}
			else
			{
				len = ((act == "check") ? 2.8f : ((act == "cam") ? 2.4f : 2.6f));
			}
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBLobbyDirector.LateUpdate");

	private void LateUpdate()
	{
		if (!FBTempo.On)
		{
			LateUpdate__fbt();
			return;
		}
		long t0 = FBTempo.T0();
		LateUpdate__fbt();
		FBTempo.Soma(__fbt_LateUpdate, t0);
	}

	private void LateUpdate__fbt()
	{
		if (act == null || !head)
		{
			return;
		}
		float num = k / len;
		float num2 = Mathf.Sin(Mathf.Min(1f, num) * (float)Math.PI);
		if (act == "look")
		{
			float num3 = Mathf.Sin(num * (float)Math.PI * 2f) * num2 * 0.75f * 57.29578f;
			if ((bool)neck)
			{
				neck.Rotate(Vector3.up, num3 * 0.45f, Space.World);
			}
			head.Rotate(Vector3.up, num3 * 0.55f, Space.World);
			head.Rotate(base.transform.right, (0f - num2) * 0.08f * 57.29578f, Space.World);
		}
		else if (act == "check" && (bool)ch.weaponGO)
		{
			Turn(ch.weaponGO.transform.position, 0.55f * num2);
		}
		else if (act == "cam" && (bool)cam)
		{
			Turn(cam.transform.position, 0.7f * num2);
		}
	}

	private void Turn(Vector3 target, float w)
	{
		Vector3 forward = base.transform.forward;
		forward.y = 0f;
		Vector3 normalized = (target - head.position).normalized;
		Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(forward.normalized, normalized), w);
		head.rotation = quaternion * head.rotation;
	}
}
}
