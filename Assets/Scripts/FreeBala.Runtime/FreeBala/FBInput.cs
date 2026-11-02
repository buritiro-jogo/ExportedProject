using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeBala
{
public static class FBInput
{
	public static float Sens = 0.5f;

	public static float AdsSens = 0.5f;

	public static bool InvertY;

	public static bool AimToggle;

	private static float r3Down = -1f;

	private static bool aimTog;

	private static float lastPauseFrame = -1f;

	public static Vector2? TestMove;

	public static bool TestSprint;

	public static bool Touch => FBTouch.Active;

	private static Keyboard K => Keyboard.current;

	private static Mouse Ms => Mouse.current;

	private static Gamepad Gp => Gamepad.current;

	public static bool Locked => Cursor.lockState == CursorLockMode.Locked;

	private static bool Down(Key k)
	{
		if (K != null)
		{
			return K[k].wasPressedThisFrame;
		}
		return false;
	}

	private static bool Held(Key k)
	{
		if (K != null)
		{
			return K[k].isPressed;
		}
		return false;
	}

	private static bool Up(Key k)
	{
		if (K != null)
		{
			return K[k].wasReleasedThisFrame;
		}
		return false;
	}

	public static bool PausePressed()
	{
		if ((float)Time.frameCount == lastPauseFrame)
		{
			return false;
		}
		int num;
		if (!Down(Key.Escape))
		{
			if (Gp != null)
			{
				num = (Gp.startButton.wasPressedThisFrame ? 1 : 0);
				if (num != 0)
				{
					goto IL_0037;
				}
			}
			else
			{
				num = 0;
			}
			goto IL_0042;
		}
		num = 1;
		goto IL_0037;
		IL_0042:
		return (byte)num != 0;
		IL_0037:
		lastPauseFrame = Time.frameCount;
		goto IL_0042;
	}

	public static bool AnyKeyDown(Key k)
	{
		return Down(k);
	}

	public static FBInputState Read()
	{
		FBInputState fBInputState = default(FBInputState);
		fBInputState.slot = -1;
		FBInputState result = fBInputState;
		bool flag = FBMatch.Paused || FBShop.Open || FBBag.Open || FBMultimidia.Open || FBOficina.on;
		if (K != null && !flag)
		{
			result.move = new Vector2((Held(Key.D) ? 1 : 0) - (Held(Key.A) ? 1 : 0), (Held(Key.W) ? 1 : 0) - (Held(Key.S) ? 1 : 0));
			// fisica 31/10: no modo SEGURAR SHIFT o Shift so vale enquanto segurado (sprintHeld); o teclado nao trava mais o autoRun (L3 e toque continuam alternando)
			result.sprintToggle = !FBSession.holdRun && (Down(Key.LeftShift) || Down(Key.RightShift)) && !Held(Key.LeftMeta) && !Held(Key.RightMeta);
			result.sprintHeld = Held(Key.LeftShift) || Held(Key.RightShift);
			result.jump = Down(Key.Space);
			result.jumpHeld = Held(Key.Space);
			result.crouchToggle = Down(Key.C) || Down(Key.LeftCtrl);
			result.prone = Down(Key.Z);
			result.reload = Down(Key.R);
			result.heal = Down(Key.H);
			result.pickup = Down(Key.X);
			result.interact = Down(Key.F);
			result.interactHeld = Held(Key.F);
			result.bag = Down(Key.I);
			result.buy = Down(Key.B);
			result.buyReleased = Up(Key.B);
			result.inspect = Down(Key.T);
			result.toggleView = Down(Key.V);
			result.leanL = Held(Key.Q);
			result.leanR = Held(Key.E);
			result.mark = Down(Key.Y);
			result.gps = Down(Key.G);
			result.multimidia = Down(Key.K);
			result.arsenal = Down(Key.N);
			result.scoreboard = Held(Key.Tab);
			result.chat = Down(Key.Enter) || Down(Key.NumpadEnter);
			result.freeLook = Held(Key.LeftAlt) || Held(Key.RightAlt);
			result.freeLookPressed = Down(Key.LeftAlt) || Down(Key.RightAlt);
			result.mic = Down(Key.M);
			if (Down(Key.Digit1))
			{
				result.slot = 0;
			}
			if (Down(Key.Digit2))
			{
				result.slot = 1;
			}
			if (Down(Key.Digit3))
			{
				result.slot = 2;
			}
			if (Down(Key.Digit4))
			{
				result.slot = 3;
			}
			if (Down(Key.Digit5))
			{
				result.slot = 4;
			}
		}
		if (Ms != null && !flag)
		{
			if (Locked)
			{
				float num = 0.0022f * (0.4f + Sens * 1.2f) * 57.29578f;
				Vector2 vector = Ms.delta.ReadValue();
				result.look = new Vector2(vector.x, vector.y * (float)((!InvertY) ? 1 : (-1))) * num;
				result.fire = Ms.leftButton.isPressed;
				result.firePressed = Ms.leftButton.wasPressedThisFrame;
				if (Ms.rightButton.wasPressedThisFrame)
				{
					result.aimPressed = true;
					if (AimToggle)
					{
						aimTog = !aimTog;
					}
				}
				result.aim = (AimToggle ? aimTog : Ms.rightButton.isPressed);
				if (Ms.middleButton.wasPressedThisFrame)
				{
					result.mark = true;
				}
				float y = Ms.scroll.ReadValue().y;
				result.cycle = ((y > 0.1f) ? (-1) : ((y < -0.1f) ? 1 : 0));
			}
			else if (Ms.leftButton.wasPressedThisFrame && !Touch && (bool)FBMatch.I && FBMatch.I.state == "play" && !FBHUD.CardOpen && !FBGarage.Open_)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
		}
		if (Gp != null && !flag)
		{
			Vector2 vector2 = Gp.leftStick.ReadValue();
			float magnitude = vector2.magnitude;
			if (magnitude > 0.22f)
			{
				result.move = vector2 / magnitude * Mathf.Clamp01((magnitude - 0.22f) / 0.78f);
			}
			Vector2 vector3 = Gp.rightStick.ReadValue();
			if (vector3.sqrMagnitude > 0.01f)
			{
				result.look += new Vector2(vector3.x, vector3.y * (float)((!InvertY) ? 1 : (-1))) * (140f + Sens * 220f) * Time.deltaTime * Mathf.Sign(vector3.magnitude) * vector3.magnitude;
			}
			result.fire |= Gp.rightTrigger.isPressed;
			result.firePressed |= Gp.rightTrigger.wasPressedThisFrame;
			result.aim |= Gp.leftTrigger.isPressed;
			result.aimPressed |= Gp.leftTrigger.wasPressedThisFrame;
			result.jump |= Gp.buttonSouth.wasPressedThisFrame;
			result.jumpHeld |= Gp.buttonSouth.isPressed;
			result.crouchToggle |= Gp.buttonEast.wasPressedThisFrame;
			result.reload |= Gp.buttonWest.wasPressedThisFrame;
			if (Gp.buttonNorth.wasPressedThisFrame)
			{
				result.cycle = 1;
			}
			result.sprintToggle |= Gp.leftStickButton.wasPressedThisFrame;
			result.leanL |= Gp.leftShoulder.isPressed;
			result.leanR |= Gp.rightShoulder.isPressed;
			result.interact |= Gp.dpad.up.wasPressedThisFrame;
			result.interactHeld |= Gp.dpad.up.isPressed;
			result.pickup |= Gp.dpad.down.wasPressedThisFrame;
			result.heal |= Gp.dpad.left.wasPressedThisFrame;
			result.buy |= Gp.dpad.right.wasPressedThisFrame;
			result.buyReleased |= Gp.dpad.right.wasReleasedThisFrame;
			if (Gp.rightStickButton.wasPressedThisFrame)
			{
				r3Down = Time.unscaledTime;
			}
			if (Gp.rightStickButton.isPressed && r3Down >= 0f && Time.unscaledTime - r3Down > 0.25f)
			{
				result.freeLook = true;
				if (Time.unscaledTime - r3Down - Time.unscaledDeltaTime <= 0.25f)
				{
					result.freeLookPressed = true;
				}
			}
			if (Gp.rightStickButton.wasReleasedThisFrame)
			{
				if (r3Down >= 0f && Time.unscaledTime - r3Down <= 0.25f)
				{
					result.toggleView = true;
				}
				r3Down = -1f;
			}
			result.scoreboard |= Gp.selectButton.isPressed;
			result.prone |= Gp.buttonEast.wasPressedThisFrame && Gp.buttonEast.isPressed && Gp.leftShoulder.isPressed;
		}
		if (Touch && !flag)
		{
			FBTouch i = FBTouch.I;
			result.move = i.move;
			result.look += i.look * 0.12f;
			result.fire |= i.fire;
			result.firePressed |= i.firePressed;
			result.aim |= i.aim;
			result.jump |= i.jump;
			result.crouchToggle |= i.crouchPressed;
			result.reload |= i.reload;
			result.sprintToggle |= i.sprint;
			if (i.swap)
			{
				result.cycle = 1;
			}
			result.interact |= i.interact;
			result.interactHeld |= i.interactHeld;
			result.pickup |= i.pickup;
			result.heal |= i.heal;
			result.buy |= i.buy;
			result.toggleView |= i.view;
			result.prone |= i.prone;
			result.freeLook |= i.freeLook;
			result.freeLookPressed |= i.freeLookPressed;
			i.EndFrame();
		}
		Vector2? testMove = TestMove;
		if (testMove.HasValue)
		{
			Vector2 valueOrDefault = testMove.GetValueOrDefault();
			result.move = valueOrDefault;
			result.sprintHeld |= TestSprint;
		}
		return result;
	}
}
}
