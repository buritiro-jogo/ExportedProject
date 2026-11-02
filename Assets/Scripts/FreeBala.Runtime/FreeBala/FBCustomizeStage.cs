using System;
using UnityEngine;

namespace FreeBala
{
public class FBCustomizeStage : MonoBehaviour
{
	public enum Frame
	{
		Full,
		Torso,
		Face,
		Legs,
		Feet,
		Bust
	}

	public Camera cam;

	public GameObject model;

	public FBCharacter ch;

	public FBWardrobe wardrobe;

	public string id;

	public Rect view = new Rect(0.3f, 0.08f, 0.4f, 0.8f);

	public Frame frame;

	public float yaw = 180f;

	public float yawVel;

	public float zoom = 1f;

	private float shownYaw = 180f;

	private float shownVel;

	private float resetT;

	public const float ZoomMin = 0.7f;

	public const float ZoomMax = 2.6f;

	private float idleT;

	private float poseT;

	private float hHead = 1.6f;

	private float hHips = 0.95f;

	private float hKnee = 0.5f;

	private float measureT;

	private bool dragging;

	private Vector3 camVel;

	private Quaternion camRot;

	private Light key;

	private Light rim;

	private Light rim2;

	private Light fill;

	private float keyI;

	private float rimI;

	private float rim2I;

	private float fillI;

	private float fillR;

	private Vector3 fillP;

	private CanvasGroup floorGlow;

	[NonSerialized]
	public float pan;

	[NonSerialized]
	public Vector3 center;

	public static FBCustomizeStage Create(Camera cam)
	{
		FBCustomizeStage fBCustomizeStage = new GameObject("FB_Vitrine").AddComponent<FBCustomizeStage>();
		fBCustomizeStage.cam = cam;
		fBCustomizeStage.camRot = (cam ? cam.transform.rotation : Quaternion.identity);
		fBCustomizeStage.Grab(ref fBCustomizeStage.key, "LuzChave", ref fBCustomizeStage.keyI);
		fBCustomizeStage.Grab(ref fBCustomizeStage.rim, "Recorte", ref fBCustomizeStage.rimI);
		fBCustomizeStage.Grab(ref fBCustomizeStage.rim2, "Recorte2", ref fBCustomizeStage.rim2I);
		fBCustomizeStage.Grab(ref fBCustomizeStage.fill, "Preenchimento", ref fBCustomizeStage.fillI);
		if ((bool)fBCustomizeStage.fill)
		{
			fBCustomizeStage.fillP = fBCustomizeStage.fill.transform.position;
			fBCustomizeStage.fillR = fBCustomizeStage.fill.range;
		}
		GameObject gameObject = GameObject.Find("LobbyFundo/luz_chao");
		if ((bool)gameObject)
		{
			fBCustomizeStage.floorGlow = FBCustomizeFx.Group(gameObject.transform);
		}
		return fBCustomizeStage;
	}

	private void Grab(ref Light l, string n, ref float i)
	{
		GameObject gameObject = GameObject.Find(n);
		l = (gameObject ? gameObject.GetComponent<Light>() : null);
		if ((bool)l)
		{
			i = l.intensity;
		}
	}

	private void OnDestroy()
	{
		if ((bool)model)
		{
			UnityEngine.Object.Destroy(model);
		}
		if ((bool)key)
		{
			key.intensity = keyI;
		}
		if ((bool)rim)
		{
			rim.intensity = rimI;
		}
		if ((bool)rim2)
		{
			rim2.intensity = rim2I;
		}
		if ((bool)fill)
		{
			fill.intensity = fillI;
			fill.range = fillR;
			fill.transform.position = fillP;
		}
		if ((bool)floorGlow)
		{
			floorGlow.alpha = 1f;
		}
	}

	public bool Show(string skinId, string look)
	{
		GameObject gameObject = (FBRegistry.I ? FBRegistry.I.Char(skinId) : null);
		if (!gameObject)
		{
			return false;
		}
		if ((bool)model)
		{
			UnityEngine.Object.Destroy(model);
		}
		model = UnityEngine.Object.Instantiate(gameObject, Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
		model.name = "Previa";
		id = skinId;
		ch = model.GetComponent<FBCharacter>();
		if ((bool)ch)
		{
			ch.relaxed = true;
			ch.isPlayer = false;
			ch.skinId = skinId;
		}
		if ((bool)ch && GameData.Weapons.TryGetValue(FBSession.primary, out var value))
		{
			ch.SetWeapon(value, FBRegistry.I.Weapon(value.id));
		}
		Animator component = model.GetComponent<Animator>();
		if ((bool)component)
		{
			component.cullingMode = AnimatorCullingMode.AlwaysAnimate;
		}
		wardrobe = FBWardrobe.For(model, skinId);
		if ((bool)wardrobe && wardrobe.Customizable)
		{
			wardrobe.Apply(look);
		}
		else if ((bool)wardrobe && !wardrobe.HasSheet)
		{
			UnityEngine.Object.Destroy(wardrobe);
			wardrobe = null;
		}
		model.AddComponent<FBLobbyDirector>().cam = cam;
		FBCustomizeFx.Pop(model.transform, 0.86f, 0.35f, 0f, model.transform.localScale);
		measureT = 0.25f;
		poseT = 0f;
		return true;
	}

	private void Measure()
	{
		if (!model)
		{
			return;
		}
		Animator an = model.GetComponent<Animator>();
		float y0 = model.transform.position.y;
		float s = model.transform.localScale.y;
		Renderer[] componentsInChildren = model.GetComponentsInChildren<Renderer>();
		float num = 1.75f;
		if (componentsInChildren.Length != 0)
		{
			Bounds bounds = componentsInChildren[0].bounds;
			Renderer[] array = componentsInChildren;
			foreach (Renderer renderer in array)
			{
				bounds.Encapsulate(renderer.bounds);
			}
			num = (bounds.max.y - y0) / Mathf.Max(0.01f, s);
		}
		hHead = B(HumanBodyBones.Head, num * 0.9f);
		hHips = B(HumanBodyBones.Hips, num * 0.53f);
		hKnee = B(HumanBodyBones.LeftLowerLeg, num * 0.28f);
		if (hHead < 0.5f || hHead > 3f)
		{
			hHead = 1.6f;
		}
		float B(HumanBodyBones b, float d)
		{
			Transform transform = (((bool)an && an.isHuman) ? an.GetBoneTransform(b) : null);
			if (!transform)
			{
				return d;
			}
			return (transform.position.y - y0) / Mathf.Max(0.01f, s);
		}
	}

	public void Drag(float dxPixels)
	{
		float num = dxPixels / (float)Mathf.Max(1, Screen.width) * 540f;
		yaw += num;
		yawVel = Mathf.Lerp(yawVel, num / Mathf.Max(Time.unscaledDeltaTime, 0.008f), 0.5f);
		idleT = 0f;
		dragging = true;
	}

	public void EndDrag()
	{
		dragging = false;
	}

	public void Zoom(float steps)
	{
		zoom = Mathf.Clamp(zoom * Mathf.Pow(1.15f, steps), 0.7f, 2.6f);
		idleT = 0f;
	}

	public void SetFrame(Frame f)
	{
		if (frame != f)
		{
			frame = f;
			zoom = 1f;
			pan = 0f;
		}
	}

	public void Pan(float dyPixels)
	{
		float num = Mathf.Max(0.3f, hHead * 0.45f);
		pan = Mathf.Clamp(pan - dyPixels / (float)Mathf.Max(1, Screen.height) * 2.2f / zoom, 0f - num, num);
		idleT = 0f;
	}

	public void Pinch(float ratio)
	{
		if (ratio > 0f)
		{
			zoom = Mathf.Clamp(zoom * ratio, 0.7f, 2.6f);
		}
		dragging = false;
		idleT = 0f;
	}

	public void ResetView()
	{
		zoom = 1f;
		pan = 0f;
		yawVel = 0f;
		resetT = 0.3f;
	}

	public void PlayConfirm()
	{
		if (!ch)
		{
			return;
		}
		yawVel = 0f;
		idleT = 99f;
		string text = FBEmotes.ConfirmPose(ch.IsFemale);
		if (text != null)
		{
			FBLobbyDirector component = model.GetComponent<FBLobbyDirector>();
			if ((bool)component)
			{
				component.home = false;
			}
			if (ch.PlayDance(text))
			{
				poseT = Mathf.Min(FBRegistry.I.Clip(text).length, 6f);
			}
		}
	}

	public void PlayDance()
	{
		string text = (ch ? FBEmotes.RandomDance(ch.IsFemale) : null);
		if (text != null && (bool)ch)
		{
			FBLobbyDirector component = model.GetComponent<FBLobbyDirector>();
			if ((bool)component)
			{
				component.home = false;
			}
			if (ch.PlayDance(text))
			{
				poseT = 8f;
			}
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBCustomizeStage.Update");

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
		float unscaledDeltaTime = Time.unscaledDeltaTime;
		if (measureT > 0f && (measureT -= unscaledDeltaTime) <= 0f)
		{
			Measure();
		}
		if (poseT > 0f && (poseT -= unscaledDeltaTime) <= 0f && (bool)ch)
		{
			ch.StopDance(force: true);
			FBLobbyDirector fBLobbyDirector = (model ? model.GetComponent<FBLobbyDirector>() : null);
			if ((bool)fBLobbyDirector)
			{
				fBLobbyDirector.home = true;
			}
		}
		if (!dragging)
		{
			yaw += yawVel * unscaledDeltaTime;
			yawVel *= Mathf.Exp(-3.5f * unscaledDeltaTime);
			if (Mathf.Abs(yawVel) < 2f)
			{
				yawVel = 0f;
			}
			if ((idleT += unscaledDeltaTime) > 3.5f && yawVel == 0f)
			{
				yaw = Mathf.LerpAngle(yaw, 180f, 1f - Mathf.Exp(-1.2f * unscaledDeltaTime));
			}
		}
		if (resetT > 0f)
		{
			resetT -= unscaledDeltaTime;
			yaw = Mathf.LerpAngle(yaw, 180f, 1f - Mathf.Exp(-14f * unscaledDeltaTime));
			if (resetT <= 0f)
			{
				yaw = 180f;
			}
		}
		shownYaw = Mathf.SmoothDampAngle(shownYaw, yaw, ref shownVel, 0.1f, 1f / 0f, unscaledDeltaTime);
		if ((bool)model)
		{
			model.transform.rotation = Quaternion.Euler(0f, shownYaw, 0f);
		}
	}

	private void Bounds(out float lo, out float hi, out float halfW, out float pitch)
	{
		switch (frame)
		{
		case Frame.Face:
			lo = hHead - 0.3f;
			hi = hHead + 0.26f;
			halfW = 0.24f;
			pitch = 2f;
			break;
		case Frame.Bust:
			lo = hHips + (hHead - hHips) * 0.35f;
			hi = hHead + 0.28f;
			halfW = 0.36f;
			pitch = 3f;
			break;
		case Frame.Torso:
			lo = hHips - 0.12f;
			hi = hHead + 0.22f;
			halfW = 0.42f;
			pitch = 4f;
			break;
		case Frame.Legs:
			lo = -0.02f;
			hi = hHips + 0.18f;
			halfW = 0.3f;
			pitch = 9f;
			break;
		case Frame.Feet:
			lo = -0.05f;
			hi = hKnee + 0.1f;
			halfW = 0.25f;
			pitch = 16f;
			break;
		default:
			lo = -0.04f;
			hi = hHead + 0.3f;
			halfW = 0.5f;
			pitch = 3f;
			break;
		}
	}

	public void Target(out Vector3 pos, out Quaternion rot, out float dist)
	{
		Bounds(out var lo, out var hi, out var halfW, out var pitch);
		float num = (model ? model.transform.localScale.y : 1f);
		lo *= num;
		hi *= num;
		halfW *= num;
		Vector3 vector = (center = new Vector3(0f, (lo + hi) / 2f + pan * num, 0f));
		float num2 = (hi - lo) / 2f;
		float num3 = (cam ? cam.fieldOfView : 30f);
		float num4 = (cam ? cam.aspect : 1.7777778f);
		float num5 = Mathf.Tan(num3 * 0.5f * ((float)Math.PI / 180f));
		float num6 = num5 * num4;
		dist = Mathf.Max(num2 / (num5 * Mathf.Max(0.1f, view.height)), halfW / (num6 * Mathf.Max(0.1f, view.width))) / zoom;
		dist = Mathf.Clamp(dist, 0.55f, 12f);
		rot = Quaternion.Euler(pitch, 0f, 0f);
		Vector3 vector2 = rot * Vector3.forward;
		Vector3 vector3 = rot * Vector3.right;
		Vector3 vector4 = rot * Vector3.up;
		float num7 = (view.center.x - 0.5f) * 2f;
		float num8 = (view.center.y - 0.5f) * 2f;
		pos = vector - vector2 * dist - vector3 * (num7 * dist * num6) - vector4 * (num8 * dist * num5);
		if (pos.y < 0.15f)
		{
			pos.y = 0.15f;
		}
	}

	private static readonly int __fbt_LateUpdate = FBTempo.Id("FBCustomizeStage.LateUpdate");

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
		if ((bool)cam)
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			Target(out var pos, out var rot, out var dist);
			cam.transform.position = Vector3.SmoothDamp(cam.transform.position, pos, ref camVel, 0.22f, 1f / 0f, unscaledDeltaTime);
			camRot = Quaternion.Slerp(cam.transform.rotation, rot, 1f - Mathf.Exp(-9f * unscaledDeltaTime));
			cam.transform.rotation = camRot;
			float t = Mathf.InverseLerp(6f, 1f, dist);
			if ((bool)fill)
			{
				fill.transform.position = cam.transform.position + cam.transform.right * 0.7f + cam.transform.up * 0.5f;
				fill.range = dist + 4f;
				fill.intensity = fillI * Mathf.Lerp(Mathf.Clamp(dist * dist / 5f, 0.6f, 6f), 1f, 0.3f);
			}
			if ((bool)key)
			{
				key.intensity = keyI * Mathf.Lerp(1f, 1.18f, t);
			}
			if ((bool)rim)
			{
				rim.intensity = rimI * Mathf.Lerp(1f, 0.75f, t);
			}
			if ((bool)rim2)
			{
				rim2.intensity = rim2I * Mathf.Lerp(1f, 0.75f, t);
			}
			if ((bool)floorGlow)
			{
				floorGlow.alpha = Mathf.MoveTowards(floorGlow.alpha, (frame == Frame.Full && zoom < 1.3f) ? 1 : 0, unscaledDeltaTime * 3f);
			}
		}
	}

	public void Snap()
	{
		shownYaw = yaw;
		if ((bool)model)
		{
			model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
		}
		if ((bool)cam)
		{
			Target(out var pos, out var rot, out var _);
			cam.transform.SetPositionAndRotation(pos, rot);
			camVel = Vector3.zero;
		}
	}
}
}
