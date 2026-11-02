using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FreeBala
{
public class FBCarCull : MonoBehaviour
{
	private class St
	{
		public Renderer[] rs;

		public ShadowCastingMode[] sh;

		public float t;

		public bool hid;

		public bool noSh;

		public FBCharacter drv;

		public bool drvHid;
	}

	private static FBCarCull I;

	private readonly Dictionary<FBCar, St> st = new Dictionary<FBCar, St>();

	private float tick;

	public static float HideDist => FBQuality.Mobile ? 150 : 260;

	public static float ShadowDist => FBQuality.Mobile ? 25 : 45;

	public static int Hidden { get; private set; }

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		if (!I)
		{
			GameObject obj = new GameObject("FBCarCull");
			Object.DontDestroyOnLoad(obj);
			I = obj.AddComponent<FBCarCull>();
		}
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBCarCull.Update");

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
		if ((tick += Time.unscaledDeltaTime) < 0.2f)
		{
			return;
		}
		tick = 0f;
		Camera main = Camera.main;
		FBCharacter player = FBMatch.Player;
		if (!main && !player)
		{
			return;
		}
		Vector3 b = (main ? main.transform.position : player.Pos);
		FBCar cur = FBDrive.Cur;
		int num = 0;
		foreach (FBCar item in FBVehicles.list)
		{
			if (!item)
			{
				continue;
			}
			if (!st.TryGetValue(item, out var value) || Time.time - value.t > 3f)
			{
				if (value != null)
				{
					Apply(value, hide: false, noSh: false);
					HideChar(value.drv, off: false);
				}
				value = Snap(item);
				st[item] = value;
			}
			float num2 = Vector3.Distance(item.transform.position, b);
			if ((bool)player)
			{
				num2 = Mathf.Min(num2, Vector3.Distance(item.transform.position, player.Pos));
			}
			bool num3 = item == cur;
			bool flag = !num3 && (value.hid ? (num2 > HideDist * 0.9f) : (num2 > HideDist));
			bool flag2 = !num3 && (value.noSh ? (num2 > ShadowDist * 0.9f) : (num2 > ShadowDist));
			if (flag != value.hid || flag2 != value.noSh)
			{
				Apply(value, flag, flag2);
			}
			FBCharacter fBCharacter = (((bool)item.driver && !item.driver.isPlayer) ? item.driver : null);
			if (fBCharacter != value.drv || ((bool)fBCharacter && value.drvHid != value.hid))
			{
				if ((bool)value.drv && value.drv != fBCharacter)
				{
					HideChar(value.drv, off: false);
				}
				value.drv = fBCharacter;
				value.drvHid = value.hid;
				if ((bool)fBCharacter)
				{
					HideChar(fBCharacter, value.hid);
				}
			}
			if (value.hid)
			{
				num++;
			}
		}
		Hidden = num;
		if (st.Count <= FBVehicles.list.Count + 8)
		{
			return;
		}
		foreach (FBCar item2 in new List<FBCar>(st.Keys))
		{
			if (!item2)
			{
				st.Remove(item2);
			}
		}
	}

	private static void HideChar(FBCharacter c, bool off)
	{
		if (!c)
		{
			return;
		}
		Renderer[] componentsInChildren = c.GetComponentsInChildren<Renderer>(includeInactive: true);
		foreach (Renderer renderer in componentsInChildren)
		{
			if ((bool)renderer && !(renderer is ParticleSystemRenderer))
			{
				renderer.forceRenderingOff = off;
			}
		}
	}

	private static St Snap(FBCar car)
	{
		Renderer[] componentsInChildren = car.GetComponentsInChildren<Renderer>(includeInactive: true);
		ShadowCastingMode[] array = new ShadowCastingMode[componentsInChildren.Length];
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			array[i] = componentsInChildren[i].shadowCastingMode;
		}
		return new St
		{
			rs = componentsInChildren,
			sh = array,
			t = Time.time
		};
	}

	private static void Apply(St s, bool hide, bool noSh)
	{
		s.hid = hide;
		s.noSh = noSh;
		for (int i = 0; i < s.rs.Length; i++)
		{
			Renderer renderer = s.rs[i];
			if ((bool)renderer && !(renderer is ParticleSystemRenderer))
			{
				renderer.forceRenderingOff = hide;
				renderer.shadowCastingMode = ((!noSh || s.sh[i] != ShadowCastingMode.On) ? s.sh[i] : ShadowCastingMode.Off);
			}
		}
	}
}
}
