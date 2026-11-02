using UnityEngine;

namespace FreeBala
{
[DefaultExecutionOrder(1100)]
public class FBFall : MonoBehaviour
{
	public static int Started;

	public static int Finished;

	public static string Diag;

	private FBCharacter ch;

	private CharacterController cc;

	private string clip;

	private float t;

	private float len;

	private Vector3 vel;

	private Vector3 lastOff;

	private Quaternion R;

	private bool on;

	public static bool Busy(FBCharacter c)
	{
		if ((bool)c && c.TryGetComponent<FBFall>(out var component))
		{
			return component.on;
		}
		return false;
	}

	public static string ClipOf(FBCharacter c)
	{
		if (!c || !c.TryGetComponent<FBFall>(out var component) || !component.on)
		{
			return null;
		}
		return component.clip;
	}

	public static string DirFor(FBCar car, string why)
	{
		if (why == "loop" || ((bool)car && (bool)car.mx && car.mx.wheelieAngle > 45f))
		{
			return "Back";
		}
		if (why == "batida" && (bool)car && Mathf.Abs(car.mx ? (car.mx.speedKmh / 3.6f) : car.v) > 6f)
		{
			return "Fwd";
		}
		if (!car)
		{
			return "Left";
		}
		Transform transform = (car.mx ? car.mx.transform : car.transform);
		Vector3 rhs = Vector3.Cross(Vector3.up, transform.forward.Flat().normalized);
		if (!(Vector3.Dot(transform.up, rhs) > 0f))
		{
			return "Left";
		}
		return "Right";
	}

	public static Vector3 FallPoint(FBCar car, string dir)
	{
		Transform obj = (car.mx ? car.mx.transform : car.transform);
		Vector3 normalized = obj.forward.Flat().normalized;
		Vector3 vector = Vector3.Cross(Vector3.up, normalized);
		Vector3 result = obj.position + dir switch
		{
			"Fwd" => normalized * 1.2f, 
			"Left" => -vector * 0.8f, 
			"Right" => vector * 0.8f, 
			_ => -normalized * 1.1f, 
		};
		car.SetColliders(on: false);
		float? num = FBWorld.GroundY(result.x, result.y + 2f, result.z, 0.3f);
		car.SetColliders(on: true);
		if (num.HasValue)
		{
			float valueOrDefault = num.GetValueOrDefault();
			result.y = valueOrDefault;
		}
		return result;
	}

	public static bool FromBike(FBCharacter c, FBCar car, string why)
	{
		string dir = DirFor(car, why);
		Transform transform = (car.mx ? car.mx.transform : car.transform);
		Vector3 vector = (car.mx ? (transform.forward * car.mx.speedKmh / 3.6f * 1.2f) : (transform.forward * car.v));
		return Begin(c, FallPoint(car, dir), transform.eulerAngles.y, vector, dir);
	}

	public static bool Begin(FBCharacter c, Vector3 at, float yaw, Vector3 vel, string dir)
	{
		if (!c || !c.alive)
		{
			return false;
		}
		string text = "X171_SCR_Stand_Spooked_Fall_" + dir + "_Stand";
		CharacterController component = c.GetComponent<CharacterController>();
		if (!FBRegistry.I || !FBRegistry.I.Clip(text) || !FBAnimPaths.Has(text))
		{
			FBMatch.Teleport(c, at, yaw);
			if ((bool)component)
			{
				component.enabled = true;
			}
			if ((bool)FBRegistry.I && (bool)FBRegistry.I.Clip(FBCombat.GetUpClip))
			{
				c.PlayDance(FBCombat.GetUpClip);
			}
			return false;
		}
		FBMatch.Teleport(c, at, yaw);
		if ((bool)component)
		{
			component.enabled = true;
		}
		c.StopDance(force: true);
		FBFall orAdd = c.gameObject.GetOrAdd<FBFall>();
		orAdd.ch = c;
		orAdd.cc = component;
		orAdd.clip = text;
		orAdd.t = 0f;
		orAdd.len = FBRegistry.I.Clip(text).length;
		orAdd.R = Quaternion.Euler(0f, yaw, 0f);
		orAdd.vel = Vector3.ClampMagnitude(vel.Flat(), 9f);
		orAdd.lastOff = Vector3.zero;
		orAdd.on = true;
		orAdd.enabled = true;
		c.sys = text;
		c.velocity = Vector3.zero;
		Started++;
		Diag = $"{text} · {orAdd.len:0.0} s · {vel.Flat().magnitude * 3.6f / 1.17f:0} km/h";
		return true;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBFall.Update");

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
		if (on && (bool)ch && ch.alive)
		{
			ch.sys = clip;
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBFall.LateUpdate");

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
		if (!on)
		{
			return;
		}
		if (!ch || !ch.alive)
		{
			on = false;
			return;
		}
		t += Time.deltaTime;
		float k = Mathf.Clamp01(t / len);
		Animator an = ch.an;
		Transform transform = (((bool)an && an.isHuman) ? an.GetBoneTransform(HumanBodyBones.Hips) : null);
		float num = (an ? (an.humanScale * base.transform.lossyScale.y) : 1f);
		FBAnimPaths.Offset(clip, k, out var d);
		Vector3 vector = R * new Vector3(d.x, 0f, d.z) * num;
		base.transform.rotation = R;
		Vector3 vector2 = vector - lastOff + vel * Time.deltaTime;
		vel = Vector3.MoveTowards(vel, Vector3.zero, 7f * Time.deltaTime);
		lastOff = vector;
		if ((bool)cc && cc.enabled)
		{
			cc.Move(vector2 + Vector3.down * 3f * Time.deltaTime);
		}
		else
		{
			base.transform.position += vector2;
		}
		if ((bool)transform)
		{
			transform.position -= vector;
		}
		if (t >= len)
		{
			on = false;
			Finished++;
			if (ch.sys == clip)
			{
				ch.sys = null;
			}
			ch.getUpT = 0f;
		}
	}
}
}
