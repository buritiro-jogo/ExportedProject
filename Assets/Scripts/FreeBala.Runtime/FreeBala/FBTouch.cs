using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace FreeBala
{
public class FBTouch : MonoBehaviour
{
	private class Btn
	{
		public string name;

		public Vector2 pos;

		public float r;

		public RectTransform rt;
	}

	public static FBTouch I;

	public Vector2 move;

	public Vector2 look;

	public bool fire;

	public bool firePressed;

	public bool aim;

	public bool jump;

	public bool crouchPressed;

	public bool reload;

	public bool swap;

	public bool sprint;

	public bool interact;

	public bool interactHeld;

	public bool pickup;

	public bool heal;

	public bool buy;

	public bool view;

	public bool prone;

	public bool freeLook;

	public bool freeLookPressed;

	private bool on;

	private int moveId = -1;

	private int lookId = -1;

	private Vector2 moveOrigin;

	private Vector2 lastLook;

	private RectTransform stickBase;

	private RectTransform stickKnob;

	private Btn[] btns;

	private Canvas cv;

	public static bool Active
	{
		get
		{
			if ((bool)I)
			{
				return I.on;
			}
			return false;
		}
	}

	private void Awake()
	{
		I = this;
		on = Application.isMobilePlatform || (Touchscreen.current != null && SystemInfo.deviceType == DeviceType.Handheld);
		if (on)
		{
			EnhancedTouchSupport.Enable();
			cv = new GameObject("Toque").AddComponent<Canvas>();
			cv.renderMode = RenderMode.ScreenSpaceOverlay;
			cv.sortingOrder = 50;
			CanvasScaler canvasScaler = cv.gameObject.AddComponent<CanvasScaler>();
			canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			canvasScaler.referenceResolution = new Vector2(1600f, 900f);
			canvasScaler.matchWidthOrHeight = 1f;
			cv.transform.SetParent(base.transform, worldPositionStays: false);
			stickBase = Circle("Base", new Vector2(220f, 220f), new Color(1f, 1f, 1f, 0.12f)).rectTransform;
			stickKnob = Circle("Pino", new Vector2(90f, 90f), new Color(1f, 1f, 1f, 0.35f)).rectTransform;
			stickBase.gameObject.SetActive(value: false);
			stickKnob.gameObject.SetActive(value: false);
			btns = new Btn[13]
			{
				new Btn
				{
					name = "ATIRAR",
					pos = new Vector2(-170f, 230f),
					r = 85f
				},
				new Btn
				{
					name = "MIRA",
					pos = new Vector2(-330f, 150f),
					r = 60f
				},
				new Btn
				{
					name = "PULO",
					pos = new Vector2(-150f, 80f),
					r = 55f
				},
				new Btn
				{
					name = "AGACHAR",
					pos = new Vector2(-290f, 60f),
					r = 50f
				},
				new Btn
				{
					name = "DEITAR",
					pos = new Vector2(-420f, 60f),
					r = 42f
				},
				new Btn
				{
					name = "RECARREGAR",
					pos = new Vector2(-400f, 300f),
					r = 45f
				},
				new Btn
				{
					name = "TROCAR",
					pos = new Vector2(-150f, 420f),
					r = 45f
				},
				new Btn
				{
					name = "CURAR",
					pos = new Vector2(-520f, 160f),
					r = 42f
				},
				new Btn
				{
					name = "PEGAR",
					pos = new Vector2(-520f, 290f),
					r = 42f
				},
				new Btn
				{
					name = "AÇÃO",
					pos = new Vector2(-290f, 420f),
					r = 45f
				},
				new Btn
				{
					name = "LOJA",
					pos = new Vector2(-430f, 430f),
					r = 40f
				},
				new Btn
				{
					name = "CÂMERA",
					pos = new Vector2(-90f, 540f),
					r = 38f
				},
				new Btn
				{
					name = "OLHAR",
					pos = new Vector2(-220f, 560f),
					r = 44f
				}
			};
			Btn[] array = btns;
			foreach (Btn btn in array)
			{
				Image image = Circle(btn.name, Vector2.one * btn.r * 2f, new Color(0f, 0f, 0f, 0.35f));
				btn.rt = image.rectTransform;
				RectTransform rt = btn.rt;
				Vector2 anchorMin = (btn.rt.anchorMax = new Vector2(1f, 0f));
				rt.anchorMin = anchorMin;
				btn.rt.anchoredPosition = btn.pos;
				Text component = new GameObject("t", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
				component.transform.SetParent(btn.rt, worldPositionStays: false);
				component.text = btn.name;
				component.alignment = TextAnchor.MiddleCenter;
				component.font = FBUI.Font;
				component.fontSize = ((btn.r > 60f) ? 20 : 15);
				component.color = Color.white;
				((RectTransform)component.transform).sizeDelta = btn.rt.sizeDelta;
			}
		}
		Image Circle(string n, Vector2 size, Color c)
		{
			GameObject obj = new GameObject(n, typeof(RectTransform), typeof(Image));
			obj.transform.SetParent(cv.transform, worldPositionStays: false);
			Image component2 = obj.GetComponent<Image>();
			component2.sprite = FBUI.CircleSprite;
			component2.color = c;
			component2.raycastTarget = false;
			RectTransform obj2 = (RectTransform)obj.transform;
			obj2.sizeDelta = size;
			Vector2 anchorMin2 = (obj2.anchorMax = Vector2.zero);
			obj2.anchorMin = anchorMin2;
			return component2;
		}
	}

	public void Show(bool v)
	{
		if ((bool)cv)
		{
			cv.enabled = v;
		}
	}

	private Vector2 ToCanvas(Vector2 screen)
	{
		return screen * (900f / (float)Screen.height);
	}

	private bool InBtn(Btn b, Vector2 screen)
	{
		Vector2 vector = ToCanvas(screen);
		Vector2 vector2 = new Vector2((float)Screen.width * (900f / (float)Screen.height) + b.pos.x, b.pos.y);
		return (vector - vector2).magnitude < b.r * 1.1f;
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBTouch.Update");

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
		if (!on)
		{
			return;
		}
		look = Vector2.zero;
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		foreach (Touch activeTouch in Touch.activeTouches)
		{
			int touchId = activeTouch.touchId;
			Vector2 screenPosition = activeTouch.screenPosition;
			Btn btn = null;
			Btn[] array = btns;
			foreach (Btn btn2 in array)
			{
				if (InBtn(btn2, screenPosition))
				{
					btn = btn2;
				}
			}
			bool flag5 = activeTouch.phase == TouchPhase.Began;
			bool flag6 = activeTouch.phase == TouchPhase.Ended || activeTouch.phase == TouchPhase.Canceled;
			if (btn != null && touchId != moveId && touchId != lookId)
			{
				if (btn.name == "ATIRAR")
				{
					flag2 = true;
					if (flag5)
					{
						firePressed = true;
					}
					look += activeTouch.delta;
				}
				else if (btn.name == "MIRA")
				{
					flag = true;
				}
				else if (btn.name == "AÇÃO")
				{
					flag3 = true;
					if (flag5)
					{
						interact = true;
					}
				}
				else if (btn.name == "OLHAR")
				{
					flag4 = true;
					if (flag5)
					{
						freeLookPressed = true;
					}
					look += activeTouch.delta;
				}
				else if (flag5)
				{
					switch (btn.name)
					{
					case "PULO":
						jump = true;
						break;
					case "AGACHAR":
						crouchPressed = true;
						break;
					case "DEITAR":
						prone = true;
						break;
					case "RECARREGAR":
						reload = true;
						break;
					case "TROCAR":
						swap = true;
						break;
					case "CURAR":
						heal = true;
						break;
					case "PEGAR":
						pickup = true;
						break;
					case "LOJA":
						buy = true;
						break;
					case "CÂMERA":
						view = true;
						break;
					}
				}
				continue;
			}
			if (flag5)
			{
				if (screenPosition.x < (float)Screen.width * 0.45f && moveId < 0)
				{
					moveId = touchId;
					moveOrigin = screenPosition;
					stickBase.gameObject.SetActive(value: true);
					stickKnob.gameObject.SetActive(value: true);
					stickBase.anchoredPosition = ToCanvas(screenPosition);
				}
				else if (lookId < 0)
				{
					lookId = touchId;
					lastLook = screenPosition;
				}
			}
			if (touchId == moveId)
			{
				Vector2 vector = (screenPosition - moveOrigin) / ((float)Screen.height * 0.09f);
				Vector2 vector2 = (move = Vector2.ClampMagnitude(vector, 1f));
				stickKnob.anchoredPosition = ToCanvas(moveOrigin) + vector2 * 70f;
				sprint = vector.magnitude > 1.35f && vector.y > 0.8f;
				if (flag6)
				{
					moveId = -1;
					move = Vector2.zero;
					sprint = false;
					stickBase.gameObject.SetActive(value: false);
					stickKnob.gameObject.SetActive(value: false);
				}
			}
			else if (touchId == lookId)
			{
				look += screenPosition - lastLook;
				lastLook = screenPosition;
				if (flag6)
				{
					lookId = -1;
				}
			}
		}
		fire = flag2;
		aim = flag;
		interactHeld = flag3;
		freeLook = flag4;
	}

	public void EndFrame()
	{
		firePressed = (jump = (reload = (swap = (crouchPressed = (interact = (pickup = (heal = (buy = (view = (prone = (freeLookPressed = false)))))))))));
	}
}
}
