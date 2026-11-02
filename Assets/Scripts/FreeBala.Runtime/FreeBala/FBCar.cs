using System.Collections.Generic;
using UnityEngine;

namespace FreeBala
{
public class FBCar : MonoBehaviour
{
	private static int seq;

	public readonly int uid = ++seq;

	public string id;

	public string syncId;

	public string soundMod;

	public string kind = "car";

	public Transform soundWallNode;

	public FBCharacter driver;

	public int remoteDriver = -1;

	public bool ie;

	public bool heavy;

	public bool grau;

	public bool armored;

	public bool wallOn;

	public Dictionary<string, object> def;

	public Dictionary<string, object> meta;

	public readonly List<FBWheel> wheels = new List<FBWheel>();

	public readonly List<(Transform obj, Quaternion bas)> steerNodes = new List<(Transform, Quaternion)>();

	public Vector3? steerAxis;

	public float wheelbase = 2.6f;

	public float track = 1.4f;

	public float scale = 1f;

	public float gripSide;

	public float footSide = 0.22f;

	public Vector3 seat = new Vector3(-0.38f, 0.55f, 0.1f);

	public Vector3? feet;

	public Vector3? grip;

	public Vector3[] rigHands;

	public Vector3[] rigPoles;

	public Vector3[] rigFeet;

	public Vector3? rigWheel;

	public Vector3? rigOut;

	public Transform volante;

	public Quaternion volBase;

	public Bounds lbox;

	public Bounds box;

	public readonly List<Collider> cols = new List<Collider>();

	public string engineProf;

	public float v;

	public float steer;

	public float yaw;

	public float pitch;

	public float roll;

	public float vy;

	public float rpm;

	public float spin;

	public float rspin;

	public float crashT;

	public float lean;

	public float stIn;

	public float launchT;

	public float prevThr;

	public float gas;

	public float sp;

	public float revRpm;

	public float vPrev;

	public float gasEst;

	public int gear = 1;

	public bool air;

	public bool burn;

	public Vector3 shift;

	public FBMoto.Wheelie wh = FBMoto.NewWheelie();

	public FBMoto.Engine eng = FBMoto.NewEngine();

	public FBCarSound.Gearbox gb;

	public float steerN;

	public float steerAng;

	public float trick;

	public float wheelie;

	public FBDrive.Rider rider;

	public (string txt, string sub, bool bad, float until)? grauMsg;

	public FBDrive.VolSetup vol;

	public bool volTried;

	public float hornT;

	public bool aiLights;

	public bool lBrake;

	public bool lRev;

	public int lBlink;

	public bool lHeadOff;

	public Color? headTint;

	public int netLights = -1;

	public Transform shell;

	public Transform wheelTpl;

	public float shellY0;

	public float zb;

	public float wbL;

	public Vector3 seat0;

	public Vector3[] rigHands0;

	public Vector3[] rigPoles0;

	public Vector3[] rigFeet0;

	public Vector3? rigWheel0;

	public string wid;

	public string curWid;

	public GameObject wsrc;

	public Color paint = Color.white;

	public Dictionary<string, Material> ownMats;

	public bool turbo;

	public string tracao;

	public float riderHoldT;

	public float hbG = 1f;

	public float vl;

	public float yr;

	public float slipF;

	public float slipR;

	public float beta;

	public float lim;

	public float bPit;

	public float bPitV;

	public float bRol;

	public float bRolV;

	public float bHv;

	public float bHvV;

	public float bodyHv;

	public Quaternion bodyQ = Quaternion.identity;

	public Quaternion shellR0 = Quaternion.identity;

	public Vector3 shellP0;

	public bool bodyOn;

	public string horn;

	public FBMxBike mx;

	public float Speed => v;

	public void SetColliders(bool on)
	{
		foreach (Collider col in cols)
		{
			if ((bool)col)
			{
				col.enabled = on;
			}
		}
	}

	public Vector3 L2W(Vector3 local)
	{
		return base.transform.TransformPoint(bodyOn ? (FBCarPhys.Pivot + bodyQ * (local - FBCarPhys.Pivot) + Vector3.up * bodyHv) : local);
	}

	public void Horn()
	{
		if (!(hornT > Time.time))
		{
			hornT = Time.time + 1.2f;
			FBCarSound.Honk(this);
		}
	}
}
}
