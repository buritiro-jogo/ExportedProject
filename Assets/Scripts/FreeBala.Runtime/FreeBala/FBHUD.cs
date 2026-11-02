using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FreeBala
{
public class FBHUD : MonoBehaviour
{
	private class CardFx : MonoBehaviour
	{
		public Action<float> tick;

		private float t;

		private static readonly int __fbt_Update = FBTempo.Id("FBHUD.CardFx.Update");

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
			t += Time.unscaledDeltaTime;
			tick?.Invoke(t);
		}
	}

	private static FBHUD inst;

	public static bool CardOpen;

	private Transform root;

	private Canvas canvas;

	private Text hpT;

	private Text armorT;

	private Text ammoT;

	private Text resT;

	private Text wT;

	private Text eyebrowT;

	private Text leftT;

	private Text centerMatchT;

	private Text rightT;

	private Text boxT;

	private Text centerT;

	private Text toastA;

	private Text toastB;

	private Text toastC;

	private Text bannerA;

	private Text bannerB;

	private Text wipeA;

	private Text wipeB;

	private Text countT;

	private Text killcamT;

	private Text cashT;

	private Text cashPlus;

	private Text promptK;

	private Text promptT;

	private Text promptS;

	private Text zoneT;

	private Text reviveT;

	private Text medT;

	private Text runT;

	private Text boardT;

	private Image hpBar;

	private Image armorBar;

	private Image vignette;

	private Image hitMark;

	private Image barA;

	private Image barB;

	private Image toastBar;

	private Image reviveBar;

	private Image medBar;

	private Image zoneArrow;

	private Image dmgArrow;

	private Image crossDot;

	private RectTransform[] cross = new RectTransform[4];

	private Image[] pips = new Image[7];

	private RectTransform toastRT;

	private RectTransform bannerRT;

	private RectTransform wipeRT;

	private RectTransform boxRT;

	private RectTransform promptRT;

	private RectTransform zoneRT;

	private RectTransform reviveRT;

	private RectTransform medRT;

	private RectTransform mini;

	private RectTransform miniPlayer;

	private RectTransform dmgRT;

	private RectTransform boardRT;

	private Image miniBg;

	private readonly List<(Text t, float ttl)> feed = new List<(Text, float)>();

	private readonly List<(Text t, Vector3 p, float t0)> floats = new List<(Text, Vector3, float)>();

	private readonly List<Image> miniDots = new List<Image>();

	private readonly List<Text> tags = new List<Text>();

	private float hitT;

	private float dmgT;

	private float centerTTL;

	private float toastT;

	private float bannerT;

	private float wipeT;

	private float countTT;

	private float killcamTT;

	private float cashPlusT;

	private float mtoastT;

	private float dmgDirT;

	private float scopeKick;

	private Text mtoastT2;

	private GameObject card;

	private GameObject pausePanel;

	private Image hpLag;

	private Image ring;

	private Text fmT;

	private Text medKey;

	private Text ringT;

	private Text ringS;

	private RawImage wSil;

	private RawImage medIc;

	private RawImage miniMap;

	private RectTransform magRT;

	private RectTransform ringRT;

	private RectTransform scA;

	private RectTransform scMid;

	private RectTransform scB;

	private RectTransform squadRT;

	private RectTransform miniClip;

	private RectTransform miniN;

	private RectTransform cone;

	private Text squadCt;

	private Image lineA;

	private Image lineB;

	private readonly List<Image> magPips = new List<Image>();

	private readonly List<Image> miniRings = new List<Image>();

	private readonly List<(RectTransform rt, Image em, Text num, Text nm, Image bar, Text dt, CanvasGroup cg)> sqRows = new List<(RectTransform, Image, Text, Text, Image, Text, CanvasGroup)>();

	private readonly List<(RectTransform rt, Image em, Text num, Text nm, RectTransform barBg, Image bar)> tagW = new List<(RectTransform, Image, Text, Text, RectTransform, Image)>();

	private readonly List<Image> miniArrows = new List<Image>();

	private Text blockX;

	private float hpLagV = 100f;

	private int magN;

	private string wId;

	private Text nT;

	private Image zoneRing;

	private Image zoneNext;

	private static readonly Dictionary<FBCharacter, int> squadNo = new Dictionary<FBCharacter, int>();

	private static FBMatch squadMatch;

	private static readonly Color[] SQUAD_COL = new Color[8]
	{
		Hex(16098851u),
		Hex(3842303u),
		Hex(15232949u),
		Hex(10189823u),
		Hex(3523162u),
		Hex(2541274u),
		Hex(16740419u),
		Hex(12634675u)
	};

	private static readonly Vector2 C = new Vector2(0.5f, 0.5f);

	private const float K = 1.25f;

	private const float MS = 2.225f;

	private static readonly Color HW = new Color(1f, 1f, 1f, 0.92f);

	private static readonly Color HM = new Color(1f, 1f, 1f, 0.5f);

	private static readonly Color HL = new Color(1f, 1f, 1f, 0.14f);

	private static readonly Color HPC = new Color(0.9254902f, 0.9098039f, 44f / 51f);

	private static Sprite disc;

	private static Sprite ringSp;

	private static Sprite glow;

	private static Sprite arrowS;

	private static Sprite grad;

	private static Sprite shapeF;

	private static Sprite shapeO;

	private static Sprite vigS;

	private readonly List<RectTransform> compass = new List<RectTransform>();

	private float killT;

	private float hitBig = 1f;

	private static Sprite hurtS;

	private static Sprite sideGrad;

	private static Sprite topGrad;

	private static readonly string[] DICAS = new string[8] { "Agachado (C) você erra menos e aparece menos atrás de cobertura.", "Recarregue atrás de uma parede — o inimigo ouve a troca de pente.", "No Tático, ponto disputado não anda: elimine quem está dentro.", "Kit médico (H) cura até em pé; agachado você fica menor.", "Mire na cabeça: headshot vale mais dinheiro e passe.", "Troque de posição depois de abater alguém — o time dele vem atrás do barulho.", "Aliado caído? Limpe a área antes de reanimar.", "Pistola saca mais rápido que trocar o pente do fuzil no meio do tiroteio." };

	private FBCharacter tagFocus;

	private float tagFocusT = -9f;

	public static FBHUD I
	{
		get
		{
			if (!inst)
			{
				return null;
			}
			return inst;
		}
		set
		{
			inst = value;
		}
	}

	private static Sprite DiscSprite
	{
		get
		{
			if (!disc)
			{
				return disc = Gen(128, (float x, float y) => (1f - Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f) * 64f);
			}
			return disc;
		}
	}

	private static Sprite RingSprite
	{
		get
		{
			if (!ringSp)
			{
				return ringSp = Gen(256, delegate(float x, float y)
				{
					float num = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
					return 1f - Mathf.Abs(num - 0.985f) * 128f / 1.6f;
				});
			}
			return ringSp;
		}
	}

	private static Sprite GlowSprite
	{
		get
		{
			if (!glow)
			{
				return glow = Gen(128, delegate(float x, float y)
				{
					float num = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f)) * 2f;
					return (!(num > 1f)) ? (0.28f * (1f - num)) : 0f;
				});
			}
			return glow;
		}
	}

	private static Sprite ArrowSpr
	{
		get
		{
			if (!arrowS)
			{
				return arrowS = Gen(64, delegate(float x, float y)
				{
					float px = (x - 0.5f) * 14f;
					float py = (0.5f - y) * 16f;
					return (InTri(px, py, 0f, -8f, 6f, 6f, 0f, 3f) || InTri(px, py, 0f, -8f, 0f, 3f, -6f, 6f)) ? 1 : 0;
				});
			}
			return arrowS;
		}
	}

	private static Sprite GradSprite
	{
		get
		{
			if (!grad)
			{
				return grad = Gen(64, (float x, float y) => Mathf.Lerp(0.62f, 0.18f, x), 1);
			}
			return grad;
		}
	}

	private static Sprite VignetteSprite
	{
		get
		{
			if (!vigS)
			{
				return vigS = Gen(128, (float x, float y) => Mathf.Clamp01((Mathf.Max(Mathf.Abs(x - 0.5f), Mathf.Abs(y - 0.5f)) * 2f - 0.7f) / 0.3f) * 0.7f);
			}
			return vigS;
		}
	}

	private static Sprite HurtEdge
	{
		get
		{
			if (!hurtS)
			{
				return hurtS = Gen(256, delegate(float x, float y)
				{
					float num = (x - 0.5f) * 2f;
					float num2 = (y - 0.5f) * 2f;
					return Mathf.Pow(Mathf.Clamp01((Mathf.Sqrt(num * num * 0.8f + num2 * num2 * 1.1f) - 0.55f) / 0.75f), 1.6f);
				});
			}
			return hurtS;
		}
	}

	private static Sprite SideGrad
	{
		get
		{
			if (!sideGrad)
			{
				return sideGrad = Gen(128, (float x, float y) => Mathf.Pow(1f - x, 1.6f) * 0.92f, 1);
			}
			return sideGrad;
		}
	}

	private static Sprite TopGrad
	{
		get
		{
			if (!topGrad)
			{
				return topGrad = Gen(1, (float x, float y) => Mathf.Pow(y, 1.4f), 128);
			}
			return topGrad;
		}
	}

	private static Color Hex(uint c, float a = 1f)
	{
		return new Color((float)((c >> 16) & 0xFFu) / 255f, (float)((c >> 8) & 0xFFu) / 255f, (float)(c & 0xFFu) / 255f, a);
	}

	private void Awake()
	{
		I = this;
		canvas = FBUI.Canvas("HUD", 10);
		root = canvas.transform;
		CardOpen = false;
		FBDamageIndicators.Ensure(root);
		vignette = Img("dano", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.6f, 0f, 0f, 0f));
		vignette.sprite = HurtEdge;
		vignette.raycastTarget = false;
		for (int i = 0; i < 4; i++)
		{
			cross[i] = Img("mira" + i, C, C, Vector2.zero, (i < 2) ? new Vector2(2f, 11f) : new Vector2(11f, 2f), Color.white).rectTransform;
		}
		crossDot = Img("ponto", C, C, Vector2.zero, new Vector2(3f, 3f), Color.white);
		hitMark = Img("acerto", C, C, Vector2.zero, new Vector2(20f, 20f), new Color(1f, 1f, 1f, 0f));
		hitMark.sprite = FBUI.HitSprite;
		hitMark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
		hpT = T(root, "100", 28f, Vector2.zero, new Vector2(0f, 0.5f), 28f, 40f, 60f, 32f, TextAnchor.MiddleLeft, title: true, HPC);
		Image image = Box(root, "vidaBarra", Vector2.zero, new Vector2(0f, 0.5f), 88f, 40f, 150f, 4f, HL);
		hpLag = Fill(image.transform, new Color(1f, 1f, 1f, 0.35f));
		hpBar = Fill(image.transform, HPC);
		Image image2 = Box(root, "colete", Vector2.zero, new Vector2(0f, 0.5f), 88f, 34f, 150f, 2f, new Color(1f, 1f, 1f, 0.08f));
		armorBar = Fill(image2.transform, new Color(0.4f, 0.7f, 1f, 0.9f));
		armorT = T(root, "", 9f, Vector2.zero, new Vector2(0f, 0.5f), 88f, 48f, 220f, 12f, TextAnchor.MiddleLeft, title: true, HM);
		medRT = R(root, "kit", Vector2.zero, new Vector2(0f, 0.5f), 28f, 73f, 90f, 18f);
		medIc = Raw(medRT, "ic", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 1f, 0f, 16f, 16f, Resources.Load<Texture2D>("UI/Hud/ic_med"), HW);
		medT = T(medRT, "2", 15f, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 24f, 0f, 20f, 16f, TextAnchor.MiddleLeft, title: true, HW);
		Image image3 = Box(medRT, "tecla", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 46f, 0f, 16f, 16f, new Color(1f, 1f, 1f, 0.04f));
		image3.gameObject.AddComponent<Outline>().effectColor = HL;
		medKey = T(image3.transform, "H", 9f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, 16f, 16f, TextAnchor.MiddleCenter, title: true, HM);
		medBar = Fill(Box(medRT, "mbg", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 0f, -12f, 1f, 1f, Color.clear).transform, Color.clear);
		ringRT = R(root, "anelKit", new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f), 0f, 0f, 54f, 54f);
		Image image4 = ringRT.gameObject.AddComponent<Image>();
		image4.sprite = DiscSprite;
		image4.color = new Color(1f, 1f, 1f, 0.14f);
		image4.raycastTarget = false;
		ring = Box(ringRT, "p", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, 54f, 54f, new Color(1f, 1f, 1f, 0.95f));
		ring.sprite = DiscSprite;
		ring.type = Image.Type.Filled;
		ring.fillMethod = Image.FillMethod.Radial360;
		ring.fillOrigin = 2;
		ring.fillClockwise = true;
		Box(ringRT, "miolo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, 46f, 46f, new Color(0.03f, 0.05f, 0.055f, 0.86f)).sprite = DiscSprite;
		Raw(ringRT, "ic", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 13f, 11f, 11f, Resources.Load<Texture2D>("UI/Hud/ic_med"), HW);
		ringT = T(ringRT, "3.0", 15f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, -3f, 54f, 18f, TextAnchor.MiddleCenter, title: true, Color.white);
		T(ringRT, "USANDO KIT MÉDICO", 11f, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), 0f, -6f, 300f, 14f, TextAnchor.MiddleCenter, title: true, Color.white);
		ringS = T(ringRT, "ANDE OU AGACHE · CORRER OU ATIRAR CANCELA", 9f, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), 0f, -23f, 400f, 12f, TextAnchor.MiddleCenter, title: true, HM);
		ringRT.gameObject.SetActive(value: false);
		Vector2 anchor = new Vector2(1f, 0f);
		ammoT = T(root, "", 32f, anchor, new Vector2(1f, 0.5f), -72f, 47f, 160f, 34f, TextAnchor.MiddleRight, title: true, HW);
		resT = T(root, "", 15f, anchor, new Vector2(1f, 0.5f), -28f, 43f, 90f, 16f, TextAnchor.MiddleRight, title: true, HM);
		wT = T(root, "", 11f, anchor, new Vector2(1f, 0.5f), -29f, 84.5f, 220f, 13f, TextAnchor.MiddleRight, title: true, HM);
		fmT = T(root, "", 9f, anchor, new Vector2(1f, 0.5f), -29f, 72f, 100f, 10f, TextAnchor.MiddleRight, title: false, HM);
		wSil = Raw(root, "silhueta", anchor, new Vector2(1f, 0.5f), -80f, 79f, 106f, 28f, null, new Color(1f, 1f, 1f, 0.8f));
		wSil.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
		magRT = R(root, "pente", anchor, new Vector2(1f, 0.5f), -28f, 25.5f, 150f, 3f);
		runT = T(root, "", 10f, anchor, new Vector2(1f, 0.5f), -28f, 104f, 300f, 12f, TextAnchor.MiddleRight, title: true, FBUI.Gold);
		Vector2 TC = new Vector2(0.5f, 1f);
		Sc("placarA", out scA);
		Sc("placarMeio", out scMid);
		Sc("placarB", out scB);
		lineA = Box(scA, "fio", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0f, 0f, 54f, 2f, new Color(0.3529412f, 32f / 51f, 1f, 0.85f));
		lineA.rectTransform.anchorMin = Vector2.zero;
		lineA.rectTransform.anchorMax = new Vector2(1f, 0f);
		lineA.rectTransform.sizeDelta = new Vector2(0f, 2.5f);
		lineB = Box(scB, "fio", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0f, 0f, 54f, 2f, new Color(1f, 19f / 51f, 1f / 3f, 0.85f));
		lineB.rectTransform.anchorMin = Vector2.zero;
		lineB.rectTransform.anchorMax = new Vector2(1f, 0f);
		lineB.rectTransform.sizeDelta = new Vector2(0f, 2.5f);
		leftT = T(scA, "", 20f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, 120f, 26f, TextAnchor.MiddleCenter, title: true, Color.white);
		rightT = T(scB, "", 20f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, 120f, 26f, TextAnchor.MiddleCenter, title: true, Color.white);
		eyebrowT = T(scMid, "", 8f, TC, new Vector2(0.5f, 1f), 0f, -3f, 300f, 10f, TextAnchor.MiddleCenter, title: false, HM);
		centerMatchT = T(scMid, "", 17f, TC, new Vector2(0.5f, 1f), 0f, -12f, 300f, 22f, TextAnchor.MiddleCenter, title: true, Color.white);
		barA = Fill(Box(scMid, "ba", new Vector2(0f, 0f), new Vector2(0f, 0f), 0f, 0f, 1f, 2f, new Color(1f, 1f, 1f, 0.08f)).transform, new Color(0.18f, 0.48f, 1f));
		barB = Fill(Box(scMid, "bb", new Vector2(1f, 0f), new Vector2(1f, 0f), 0f, 0f, 1f, 2f, new Color(1f, 1f, 1f, 0.08f)).transform, new Color(0.95f, 0.23f, 0.27f));
		barB.fillOrigin = 1;
		for (int j = 0; j < 7; j++)
		{
			pips[j] = Box(scMid, "pip" + j, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0f, 4f, 10f, 5f, new Color(1f, 1f, 1f, 0.16f));
		}
		boxRT = Box(root, "objetivo", TC, new Vector2(0.5f, 1f), 0f, -62f, 200f, 16f, new Color(0f, 0f, 0f, 0.4f)).rectTransform;
		boxT = T(boxRT, "", 10f, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, 900f, 14f, TextAnchor.MiddleCenter, title: false, new Color(1f, 1f, 1f, 0.75f));
		boxRT.gameObject.SetActive(value: false);
		squadRT = R(root, "esquadrao", new Vector2(0f, 1f), new Vector2(0f, 1f), 218f, -16f, 200f, 260f);
		T(squadRT, "ESQUADRÃO", 10f, new Vector2(0f, 1f), new Vector2(0f, 1f), 2f, 0f, 120f, 12f, TextAnchor.MiddleLeft, title: true, new Color(1f, 1f, 1f, 0.7f));
		squadCt = T(squadRT, "", 10f, new Vector2(1f, 1f), new Vector2(1f, 1f), -2f, 0f, 160f, 12f, TextAnchor.MiddleRight, title: true, new Color(1f, 1f, 1f, 0.7f));
		squadCt.supportRichText = true;
		squadRT.gameObject.SetActive(value: false);
		centerT = Lbl(root, "", 30, C, C, new Vector2(0f, 170f), new Vector2(1100f, 50f), TextAnchor.MiddleCenter, title: true, FBUI.Gold);
		mtoastT2 = Lbl(root, "", 26, C, C, new Vector2(0f, 220f), new Vector2(1200f, 44f), TextAnchor.MiddleCenter, title: true, FBUI.Gold);
		toastRT = Img("toast", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-190f, 120f), new Vector2(340f, 84f), new Color(0f, 0f, 0f, 0.6f)).rectTransform;
		toastBar = FBUI.Panel(toastRT, "barra", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f), new Vector2(6f, 0f), FBUI.Gold);
		toastA = Lbl(toastRT, "", 13, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -14f), new Vector2(-20f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Gold);
		toastB = Lbl(toastRT, "", 24, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(10f, 2f), new Vector2(-20f, 30f), TextAnchor.MiddleLeft, title: true);
		toastC = Lbl(toastRT, "", 12, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 14f), new Vector2(-20f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		toastRT.gameObject.SetActive(value: false);
		bannerRT = FBUI.Rect(root, "banner", C, C, new Vector2(0f, -170f), new Vector2(700f, 80f));
		bannerA = Lbl(bannerRT, "", 34, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(700f, 44f), TextAnchor.MiddleCenter, title: true);
		bannerB = Lbl(bannerRT, "", 15, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(700f, 22f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
		bannerRT.gameObject.SetActive(value: false);
		wipeRT = FBUI.Rect(root, "wipe", C, C, new Vector2(0f, 90f), new Vector2(900f, 90f));
		wipeA = Lbl(wipeRT, "", 40, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 50f), TextAnchor.MiddleCenter, title: true);
		wipeB = Lbl(wipeRT, "", 15, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(900f, 22f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
		wipeRT.gameObject.SetActive(value: false);
		countT = Lbl(root, "", 120, C, C, new Vector2(0f, 60f), new Vector2(900f, 160f), TextAnchor.MiddleCenter, title: true, Color.white);
		killcamT = Lbl(root, "", 22, C, C, new Vector2(0f, -60f), new Vector2(900f, 40f), TextAnchor.MiddleCenter, title: true, FBUI.Red);
		cashT = Lbl(root, "", 26, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -30f), new Vector2(260f, 34f), TextAnchor.MiddleLeft, title: true, new Color(0.4f, 0.95f, 0.5f));
		cashPlus = Lbl(root, "", 16, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, -58f), new Vector2(300f, 22f), TextAnchor.MiddleLeft, title: false, new Color(0.4f, 0.95f, 0.5f));
		promptRT = Img("prompt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 200f), new Vector2(420f, 56f), new Color(0f, 0f, 0f, 0.55f)).rectTransform;
		Image image5 = FBUI.Panel(promptRT, "k", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(36f, 36f), FBUI.Gold);
		promptK = Lbl(image5.transform, "X", 20, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter, title: true, Color.black);
		promptT = Lbl(promptRT, "", 18, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(40f, 8f), new Vector2(-90f, 24f), TextAnchor.MiddleLeft, title: true);
		promptS = Lbl(promptRT, "", 12, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(40f, -12f), new Vector2(-90f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut);
		promptRT.gameObject.SetActive(value: false);
		reviveRT = Img("reviver", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 270f), new Vector2(420f, 40f), new Color(0f, 0f, 0f, 0.55f)).rectTransform;
		reviveT = Lbl(reviveRT, "", 16, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero, TextAnchor.MiddleCenter, title: true);
		reviveBar = Fill(FBUI.Panel(reviveRT, "rb", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f), new Vector2(-10f, 4f), new Color(1f, 1f, 1f, 0.1f)).transform, new Color(0.3f, 0.9f, 0.4f));
		reviveRT.gameObject.SetActive(value: false);
		zoneRT = FBUI.Rect(root, "zona", C, C, new Vector2(0f, 120f), new Vector2(400f, 90f));
		zoneArrow = FBUI.Panel(zoneRT, "seta", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(34f, 34f), new Color(0.35f, 0.75f, 1f));
		zoneArrow.sprite = FBUI.ArrowSprite;
		zoneT = Lbl(zoneRT, "", 15, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(400f, 22f), TextAnchor.MiddleCenter, title: true, new Color(0.35f, 0.75f, 1f));
		zoneRT.gameObject.SetActive(value: false);
		dmgRT = FBUI.Rect(root, "dirDano", C, C, Vector2.zero, new Vector2(10f, 10f));
		dmgArrow = FBUI.Panel(dmgRT, "seta", C, C, new Vector2(0f, 140f), new Vector2(60f, 24f), new Color(1f, 0.2f, 0.15f, 0f));
		dmgArrow.sprite = FBUI.ArrowSprite;
		mini = R(root, "minimapa", new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), 113f, -111f, 190f, 190f);
		Box(mini, "sombra", C, C, 2f, -2f, 194f, 194f, new Color(0f, 0f, 0f, 0.4f)).sprite = MiniShape(outline: false);
		Image image6 = Box(mini, "recorte", C, C, 0f, 0f, 190f, 190f, new Color(0.05f, 0.06f, 0.07f, 0.92f));
		image6.sprite = MiniShape(outline: false);
		image6.gameObject.AddComponent<Mask>().showMaskGraphic = true;
		miniClip = image6.rectTransform;
		miniMap = Raw(miniClip, "mapa", C, C, 0f, 0f, 10f, 10f, null, new Color(1f, 1f, 1f, 0.92f));
		miniMap.gameObject.SetActive(value: false);
		int[] array = new int[2] { 15, 30 };
		for (int k = 0; k < array.Length; k++)
		{
			int num = array[k];
			Image image7 = Box(miniClip, "anel" + num, C, C, 0f, 0f, (float)(num * 2) * 2.225f, (float)(num * 2) * 2.225f, new Color(1f, 1f, 1f, 0.07f));
			image7.sprite = RingSprite;
			miniRings.Add(image7);
		}
		Image image8 = Box(miniClip, "cone", C, C, 0f, 0f, 209f, 209f, new Color(1f, 1f, 1f, 0.9f));
		image8.sprite = GlowSprite;
		image8.type = Image.Type.Filled;
		image8.fillMethod = Image.FillMethod.Radial360;
		image8.fillOrigin = 2;
		image8.fillClockwise = true;
		cone = image8.rectTransform;
		zoneRing = Box(miniClip, "zona", C, C, 0f, 0f, 10f, 10f, new Color(16f / 51f, 44f / 51f, 1f, 0.95f));
		zoneRing.sprite = RingSprite;
		zoneRing.gameObject.SetActive(value: false);
		zoneNext = Box(miniClip, "zonaProx", C, C, 0f, 0f, 10f, 10f, new Color(1f, 1f, 1f, 0.8f));
		zoneNext.sprite = RingSprite;
		zoneNext.gameObject.SetActive(value: false);
		Box(miniClip, "vinheta", C, C, 0f, 0f, 178f, 178f, new Color(0.02f, 0.03f, 0.04f, 1f)).sprite = VignetteSprite;
		miniBg = image6;
		Box(mini, "moldura", C, C, 0f, 0f, 190f, 190f, new Color(1f, 1f, 1f, 0.22f)).sprite = MiniShape(outline: true);
		(float, float, float, float)[] array2 = new(float, float, float, float)[4]
		{
			(6f, 6f, 2f, 22f),
			(6f, 6f, 22f, 2f),
			(184f, 184f, 2f, 22f),
			(184f, 184f, 22f, 2f)
		};
		for (int k = 0; k < array2.Length; k++)
		{
			var (num2, num3, w, h) = array2[k];
			Box(mini, "canto", new Vector2(0f, 0f), new Vector2((!(num2 < 95f)) ? 1 : 0, (!(num3 < 95f)) ? 1 : 0), num2 - (float)((num2 < 95f) ? 1 : (-1)), num3 - (float)((num3 < 95f) ? 1 : (-1)), w, h, new Color(1f, 1f, 1f, 0.6f));
		}
		miniPlayer = Box(mini, "eu", C, C, 0f, 0f, 13.2f, 15.4f, Color.white).rectTransform;
		Image component = miniPlayer.GetComponent<Image>();
		component.sprite = ArrowSpr;
		Outline outline = component.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
		outline.effectDistance = new Vector2(1.4f, -1.4f);
		Image image9 = Box(mini, "N", C, C, 0f, 80f, 18f, 18f, new Color(0f, 0f, 0f, 0.75f));
		image9.sprite = DiscSprite;
		miniN = image9.rectTransform;
		nT = T(miniN, "N", 11f, C, C, 0f, 0f, 18f, 18f, TextAnchor.MiddleCenter, title: true, new Color(1f, 0.78f, 0.25f));
		string[] array3 = new string[3] { "L", "S", "O" };
		foreach (string s in array3)
		{
			Text text = T(mini, s, 9f, C, C, 0f, 0f, 14f, 14f, TextAnchor.MiddleCenter, title: true, new Color(1f, 1f, 1f, 0.6f));
			compass.Add(text.rectTransform);
		}
		Box(mini, "escala", C, C, 0f, -103f, 44.5f, 2f, new Color(1f, 1f, 1f, 0.55f));
		T(mini, "20 m", 8f, C, C, 0f, -110f, 40f, 10f, TextAnchor.MiddleCenter, title: false, new Color(1f, 1f, 1f, 0.5f));
		boardRT = Img("placarTab", C, C, Vector2.zero, new Vector2(620f, 430f), new Color(0.03f, 0.04f, 0.05f, 0.9f)).rectTransform;
		boardT = Lbl(boardRT, "", 16, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-40f, -40f), TextAnchor.UpperLeft);
		boardT.supportRichText = true;
		boardRT.gameObject.SetActive(value: false);
		for (int l = 0; l < 5; l++)
		{
			Text text2 = Lbl(root, "", 15, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-200f, -140 - l * 22), new Vector2(380f, 22f), TextAnchor.MiddleRight);
			text2.supportRichText = true;
			feed.Add((text2, 0f));
		}
		FBMinimap.Capture();
		FBHudWeapons.Attach(root);
		FBKillFeed.Attach(root);
		hpT.gameObject.SetActive(value: false);
		hpBar.transform.parent.gameObject.SetActive(value: false);
		armorBar.transform.parent.gameObject.SetActive(value: false);
		armorT.gameObject.SetActive(value: false);
		Image Sc(string n, out RectTransform rt)
		{
			Image image10 = Box(root, n, TC, new Vector2(0.5f, 1f), 0f, -10f, 54f, 51f, new Color(0f, 0f, 0f, 0.45f));
			rt = image10.rectTransform;
			return image10;
		}
	}

	private RectTransform R(Transform parent, string n, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h)
	{
		RectTransform rectTransform = FBUI.Rect(parent, n, anchor, anchor, new Vector2(x * 1.25f, y * 1.25f), new Vector2(w * 1.25f, h * 1.25f));
		rectTransform.pivot = pivot;
		rectTransform.anchoredPosition = new Vector2(x * 1.25f, y * 1.25f);
		return rectTransform;
	}

	private Image Box(Transform parent, string n, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, Color c)
	{
		Image image = R(parent, n, anchor, pivot, x, y, w, h).gameObject.AddComponent<Image>();
		image.color = c;
		image.raycastTarget = false;
		return image;
	}

	private RawImage Raw(Transform parent, string n, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, Texture tex, Color c)
	{
		RawImage rawImage = R(parent, n, anchor, pivot, x, y, w, h).gameObject.AddComponent<RawImage>();
		rawImage.texture = tex;
		rawImage.color = c;
		rawImage.raycastTarget = false;
		return rawImage;
	}

	private Text T(Transform parent, string s, float px, Vector2 anchor, Vector2 pivot, float x, float y, float w, float h, TextAnchor al, bool title, Color c)
	{
		RectTransform rectTransform = R(parent, "txt", anchor, pivot, x, y, w, h);
		Text text = rectTransform.gameObject.AddComponent<Text>();
		text.text = s;
		text.font = (title ? FBUI.Title : FBUI.Font);
		text.fontSize = Mathf.RoundToInt(px * 1.25f);
		text.alignment = al;
		text.color = c;
		text.raycastTarget = false;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		Shadow shadow = rectTransform.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
		shadow.effectDistance = new Vector2(0f, -1.5f);
		return text;
	}

	private static Sprite Gen(int n, Func<float, float, float> a, int h = 0)
	{
		h = ((h > 0) ? h : n);
		Texture2D texture2D = new Texture2D(n, h, TextureFormat.RGBA32, mipChain: false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};
		Color[] array = new Color[n * h];
		for (int i = 0; i < h; i++)
		{
			for (int j = 0; j < n; j++)
			{
				array[i * n + j] = new Color(1f, 1f, 1f, Mathf.Clamp01(a(((float)j + 0.5f) / (float)n, ((float)i + 0.5f) / (float)h)));
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		return Sprite.Create(texture2D, new Rect(0f, 0f, n, h), Vector2.one * 0.5f);
	}

	private static bool InTri(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
	{
		float num = (px - bx) * (ay - by) - (ax - bx) * (py - by);
		float num2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
		float num3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
		bool flag = num < 0f || num2 < 0f || num3 < 0f;
		bool flag2 = num > 0f || num2 > 0f || num3 > 0f;
		return !(flag && flag2);
	}

	private static Sprite MiniShape(bool outline)
	{
		if (!outline && (bool)shapeF)
		{
			return shapeF;
		}
		if (outline && (bool)shapeO)
		{
			return shapeO;
		}
		Sprite result = Gen(380, delegate(float u, float v)
		{
			float num = Inside(u * 190f, (1f - v) * 190f);
			return (!outline) ? Mathf.Clamp01(num * 2f + 0.5f) : (1f - Mathf.Abs(num - 0.5f) * 2f);
		});
		if (outline)
		{
			shapeO = result;
		}
		else
		{
			shapeF = result;
		}
		return result;
		static float Inside(float x, float y)
		{
			return Mathf.Min(Mathf.Min(Mathf.Min(Mathf.Min(x - 6f, 184f - x), Mathf.Min(y - 6f, 184f - y)), (x - 6f + (y - 6f) - 14f) / 1.4142f), (184f - x + (184f - y) - 14f) / 1.4142f);
		}
	}

	private Image Img(string n, Vector2 a0, Vector2 a1, Vector2 p, Vector2 s, Color c)
	{
		Image image = FBUI.Panel(root, n, a0, a1, p, s, c);
		image.raycastTarget = false;
		return image;
	}

	private Text Lbl(Transform parent, string t, int size, Vector2 a0, Vector2 a1, Vector2 p, Vector2 s, TextAnchor al, bool title = false, Color? c = null)
	{
		Text text = FBUI.Label(parent, t, size, a0, a1, p, s, al, title, c);
		Shadow shadow = text.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
		shadow.effectDistance = new Vector2(1f, -1f);
		return text;
	}

	private static Image Fill(Transform parent, Color c)
	{
		Image image = FBUI.Panel(parent, "fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, c);
		image.sprite = FBUI.WhiteSprite;
		image.type = Image.Type.Filled;
		image.fillMethod = Image.FillMethod.Horizontal;
		image.raycastTarget = false;
		return image;
	}

	public void Feed(string s, bool me)
	{
		FBKillFeed.Message(s.Replace(" ✖ ", "  ✖  "), me);
	}

	public void Hit(bool head)
	{
		if (!(killT > 0f))
		{
			hitT = 0.2f;
			hitBig = 1f;
			hitMark.color = (head ? new Color(1f, 0.82f, 0.2f) : Color.white);
			FBAudio.Play2D("hitmark", 0.5f, 0f);
		}
	}

	public void KillMark(bool knock)
	{
		killT = 0.5f;
		hitT = 0.5f;
		hitBig = 1.8f;
		hitMark.color = (knock ? new Color(1f, 0.55f, 0.1f) : new Color(1f, 0.16f, 0.12f));
		FBAudio.Play2D("hitmark", 0.7f, 0f, knock ? 1.1f : 0.85f);
	}

	public void FloatDamage(Vector3 p, float dmg, bool head)
	{
		Text item = Lbl(root, Mathf.RoundToInt(dmg).ToString(), head ? 17 : 13, C, C, Vector2.zero, new Vector2(80f, 22f), TextAnchor.MiddleCenter, title: true, head ? new Color(1f, 0.3f, 0.25f) : Color.white);
		floats.Add((item, p + new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), 0f, UnityEngine.Random.Range(-0.2f, 0.2f)), Time.time));
	}

	public void DamageDir(Vector3 from)
	{
		FBDamageIndicators.Hit(null, from, 20f);
	}

	public void Damaged(float k = 0.55f)
	{
		dmgT = Mathf.Max(dmgT, k);
	}

	public void Damaged()
	{
		Damaged(0.55f);
	}

	public void Center(string s, float ttl = 2.5f)
	{
		centerT.text = s;
		centerTTL = ttl;
	}

	public void MatchToast(string s, Color c, float ttl)
	{
		mtoastT2.text = s;
		mtoastT2.color = c;
		mtoastT = ttl;
	}

	public void Toast(string a, string b, string c, Color col)
	{
		toastA.text = a;
		toastB.text = b;
		toastC.text = c;
		toastA.color = col;
		toastBar.color = col;
		toastT = 3f;
		toastRT.gameObject.SetActive(value: true);
	}

	public void SetBox(string s, bool on)
	{
		boxT.text = s;
		boxRT.gameObject.SetActive(on && !string.IsNullOrEmpty(s));
		boxRT.sizeDelta = new Vector2(boxT.preferredWidth + 25f, 20f);
	}

	public void SetMatch(string eyebrow, string left, string center, string right, float a, float b, Color[] pp)
	{
		eyebrowT.text = eyebrow;
		leftT.text = left;
		centerMatchT.text = center;
		rightT.text = right;
		float num = Mathf.Max(40f, leftT.preferredWidth / 1.25f + 20f);
		float num2 = Mathf.Max(40f, rightT.preferredWidth / 1.25f + 20f);
		float num3 = Mathf.Max(120f, Mathf.Max(eyebrowT.preferredWidth, centerMatchT.preferredWidth) / 1.25f) + 24f;
		float num4 = num + num3 + num2 + 4f;
		scA.sizeDelta = new Vector2(num * 1.25f, 63.75f);
		scMid.sizeDelta = new Vector2(num3 * 1.25f, 63.75f);
		scB.sizeDelta = new Vector2(num2 * 1.25f, 63.75f);
		scA.anchoredPosition = new Vector2(((0f - num4) / 2f + num / 2f) * 1.25f, -12.5f);
		scMid.anchoredPosition = new Vector2(((0f - num4) / 2f + num + 2f + num3 / 2f) * 1.25f, -12.5f);
		scB.anchoredPosition = new Vector2((num4 / 2f - num2 / 2f) * 1.25f, -12.5f);
		RectTransform obj = (RectTransform)barA.transform.parent;
		RectTransform rectTransform = (RectTransform)barB.transform.parent;
		Vector2 sizeDelta = (rectTransform.sizeDelta = new Vector2(num3 / 2f * 1.25f, 2.5f));
		obj.sizeDelta = sizeDelta;
		obj.gameObject.SetActive(a >= 0f);
		rectTransform.gameObject.SetActive(b >= 0f);
		if (a >= 0f)
		{
			barA.fillAmount = Mathf.Clamp01(a);
		}
		if (b >= 0f)
		{
			barB.fillAmount = Mathf.Clamp01(b);
		}
		int num5 = ((pp != null) ? pp.Length : 0);
		for (int i = 0; i < 7; i++)
		{
			pips[i].gameObject.SetActive(i < num5);
			if (i < num5)
			{
				pips[i].color = pp[i];
				pips[i].rectTransform.anchoredPosition = new Vector2(((float)i - (float)(num5 - 1) / 2f) * 14f * 1.25f, 5f);
			}
		}
	}

	public void Countdown(string s)
	{
		countT.text = s;
		countTT = 1f;
		countT.color = ((s.Length > 2) ? new Color(0.3f, 0.9f, 0.45f) : Color.white);
	}

	public void KillBanner(int n, string a, string b, Color c)
	{
		bannerA.text = a;
		bannerA.color = c;
		bannerB.text = b;
		bannerT = 2.2f;
		bannerRT.gameObject.SetActive(value: true);
	}

	public void WipeBanner(string a, string b, Color c)
	{
		wipeA.text = a;
		wipeA.color = c;
		wipeB.text = b;
		wipeT = 3f;
		wipeRT.gameObject.SetActive(value: true);
	}

	public void Killcam(string by, bool on)
	{
		killcamT.text = (on ? ("ABATIDO POR " + by) : "");
		killcamTT = (on ? 3.5f : 0f);
	}

	public void SetCash(int v, string plus, int armor, bool helmet)
	{
		if ((bool)this && (bool)cashT)
		{
			cashT.text = ((v < 0) ? "" : $"$ {v}");
			if (!string.IsNullOrEmpty(plus))
			{
				cashPlus.text = plus;
				cashPlusT = 2f;
			}
		}
	}

	public void BuyHint(bool on)
	{
		if (on)
		{
			Center("B = LOJA", 2f);
		}
	}

	public void Prompt(string key, string text, string sub = null)
	{
		promptRT.gameObject.SetActive(text != null);
		if (text != null)
		{
			promptK.text = key;
			promptT.text = text;
			promptS.text = sub ?? "";
		}
	}

	public void ZoneWarning(bool on, float rel, float dmg, float dist)
	{
		if ((bool)this && (bool)zoneRT)
		{
			zoneRT.gameObject.SetActive(on);
			if (on)
			{
				zoneArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (0f - rel) * 57.29578f);
				zoneT.text = $"FORA DA ZONA · −{Mathf.RoundToInt(dmg)}/s · {Mathf.RoundToInt(dist)} m";
			}
		}
	}

	public void SetRun(bool on)
	{
		runT.text = (on ? "» CORRIDA AUTOMÁTICA" : "");
	}

	public void SetMed(int kits, bool inf, float prog, bool healing)
	{
		medT.text = (inf ? "∞" : kits.ToString());
		medRT.GetOrAdd<CanvasGroup>().alpha = ((!inf && kits <= 0) ? 0.35f : 1f);
		ringRT.gameObject.SetActive(prog > 0f);
		if (prog > 0f)
		{
			ring.fillAmount = prog;
			ringT.text = Mathf.Max(0f, (1f - prog) * 3f).ToString("0.0", CultureInfo.InvariantCulture);
		}
		hpBar.color = (healing ? new Color(0.61f, 0.9f, 0.69f) : hpBar.color);
	}

	public void Revive(string text, float p)
	{
		reviveRT.gameObject.SetActive(text != null);
		if (text != null)
		{
			reviveT.text = text;
			reviveBar.fillAmount = p;
		}
	}

	public void ScopeKick()
	{
		scopeKick = 1f;
	}

	public void CloseCards()
	{
		if ((bool)card)
		{
			UnityEngine.Object.Destroy(card);
		}
		card = null;
		CardOpen = false;
		if ((bool)FBMatch.I && FBMatch.I.state == "play" && !FBMatch.Paused && !FBInput.Touch)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	private GameObject Card(string eyebrow, string title, Color tc, string body)
	{
		CloseCards();
		CardOpen = true;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		Image image = FBUI.Panel(root, "cartao", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
		card = image.gameObject;
		Transform obj = FBUI.Panel(image.transform, "painel", C, C, Vector2.zero, new Vector2(560f, 330f), FBUI.Dark).transform;
		FBUI.Label(obj, eyebrow, 14, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(680f, 22f), TextAnchor.MiddleCenter, title: false, FBUI.Mut);
		FBUI.Label(obj, title, 64, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(680f, 80f), TextAnchor.MiddleCenter, title: true, tc);
		FBUI.Label(obj, body, 18, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(640f, 140f)).supportRichText = true;
		return obj.gameObject;
	}

	private static string Ptbr(float v, string f)
	{
		return v.ToString(f, CultureInfo.GetCultureInfo("pt-BR"));
	}

	private Text Tx(Transform p, string s, int size, Vector2 a, Vector2 pos, Vector2 box, TextAnchor al, bool title, Color c, bool shadow = true)
	{
		Text text = FBUI.Label(p, s, size, a, a, pos, box, al, title, c);
		text.supportRichText = true;
		text.horizontalOverflow = HorizontalWrapMode.Overflow;
		if (shadow)
		{
			Shadow shadow2 = text.gameObject.AddComponent<Shadow>();
			shadow2.effectColor = new Color(0f, 0f, 0f, 0.8f);
			shadow2.effectDistance = new Vector2(2f, -2f);
		}
		return text;
	}

	private static float Ease(float t)
	{
		t = Mathf.Clamp01(t);
		return 1f - Mathf.Pow(1f - t, 3f);
	}

	private static float EaseBack(float t)
	{
		t = Mathf.Clamp01(t);
		float num = 1.7f;
		return 1f + (num + 1f) * Mathf.Pow(t - 1f, 3f) + num * Mathf.Pow(t - 1f, 2f);
	}

	private RectTransform Tile(Transform p, string label, Vector2 a, Vector2 pos, Vector2 size, Color accent, out Text value)
	{
		RectTransform rectTransform = FBUI.Panel(p, "tile_" + label, a, a, pos, size, new Color(0.06f, 0.07f, 0.09f, 0.82f)).rectTransform;
		FBUI.Panel(rectTransform, "acento", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(4f, 0f), accent);
		Tx(rectTransform, label, 13, new Vector2(0f, 1f), new Vector2(86f, -16f), new Vector2(160f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut, shadow: false);
		value = Tx(rectTransform, "0", 38, new Vector2(0f, 0f), new Vector2(96f, 30f), new Vector2(180f, 44f), TextAnchor.MiddleLeft, title: true, Color.white);
		return rectTransform;
	}

	private GameObject OpenScreen(string name, out CardFx fx)
	{
		CloseCards();
		CardOpen = true;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
		Image image = FBUI.Panel(root, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
		card = image.gameObject;
		image.raycastTarget = true;
		fx = card.AddComponent<CardFx>();
		return card;
	}

	public void DeathCard(bool auto, Action respawn, Action menu)
	{
		DeathScreen(auto, respawn, menu, null, null, head: false);
	}

	public void DeathScreen(bool auto, Action respawn, Action menu, FBCharacter by, string why, bool head)
	{
		CardFx fx;
		Transform transform = OpenScreen("tela_abate", out fx).transform;
		Image bgIm = transform.GetComponent<Image>();
		FBUI.Panel(transform, "lado", new Vector2(0f, 0f), new Vector2(0.62f, 1f), Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.03f, 1f)).sprite = SideGrad;
		FBUI.Panel(transform, "vinheta", new Vector2(0f, 0.72f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.55f, 0.03f, 0.03f, 0.55f)).sprite = TopGrad;
		Image slash = FBUI.Panel(transform, "faixa", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(430f, 100f), new Vector2(860f, 6f), FBUI.Red);
		slash.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);
		RectTransform col = FBUI.Rect(transform, "coluna", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(380f, 0f), new Vector2(620f, 600f));
		Text title = Tx(col, "ELIMINADO", 104, new Vector2(0f, 0.5f), new Vector2(310f, 150f), new Vector2(620f, 120f), TextAnchor.MiddleLeft, title: true, new Color(1f, 0.27f, 0.22f));
		string text = (by ? by.Name.ToUpper() : ((why == "zone") ? "A ZONA" : ((why == "fall") ? "UMA QUEDA" : "—")));
		Tx(col, ((bool)by && by != FBMatch.Player) ? ("<color=#9aa3ad>POR</color> " + text) : ((why == "zone") ? "<color=#9aa3ad>PEGO PELA</color> ZONA" : ((why == "fall") ? "<color=#9aa3ad>MORTE POR</color> QUEDA" : "VOCÊ CAIU")), 40, new Vector2(0f, 0.5f), new Vector2(310f, 66f), new Vector2(620f, 50f), TextAnchor.MiddleLeft, title: true, Color.white);
		string text2 = (((bool)by && by.weapon != null) ? by.weapon.name.ToUpper() : (by ? "MÃOS" : ""));
		float v = (((bool)by && (bool)FBMatch.Player) ? (by.Pos - FBMatch.Player.Pos).magnitude : 0f);
		Tx(col, by ? (text2 + "   ·   " + Ptbr(v, "0") + " m" + (head ? "   ·   <color=#ff5a4e>HEADSHOT</color>" : "")) : "", 20, new Vector2(0f, 0.5f), new Vector2(310f, 26f), new Vector2(620f, 26f), TextAnchor.MiddleLeft, title: false, new Color(0.85f, 0.87f, 0.9f));
		Image hpFill = null;
		if ((bool)by && by.alive && by != FBMatch.Player)
		{
			Tx(col, "VIDA QUE SOBROU DELE", 13, new Vector2(0f, 0.5f), new Vector2(310f, -8f), new Vector2(620f, 16f), TextAnchor.MiddleLeft, title: false, FBUI.Mut, shadow: false);
			Image image = FBUI.Panel(col, "vida", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -28f), new Vector2(320f, 10f), new Color(1f, 1f, 1f, 0.12f));
			hpFill = FBUI.Panel(image.transform, "fill", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, new Color(0.35f, 0.9f, 0.45f));
			hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
			hpFill.rectTransform.sizeDelta = new Vector2(0f, 0f);
			Tx(image.transform, Ptbr(Mathf.Max(1f, by.hp), "0") + " HP", 14, new Vector2(1f, 0.5f), new Vector2(50f, 0f), new Vector2(80f, 18f), TextAnchor.MiddleLeft, title: true, Color.white, shadow: false);
		}
		FBMatch.StatsT stats = FBMatch.Stats;
		float num2 = (float)stats.kills / Mathf.Max(1f, stats.deaths);
		List<(RectTransform rt, Text v, float val, string fmt)> tiles = new List<(RectTransform, Text, float, string)>();
		string[] array = new string[4] { "ABATES", "MORTES", "K/D", "HEADSHOTS" };
		float[] array2 = new float[4] { stats.kills, stats.deaths, num2, stats.heads };
		string[] array3 = new string[4] { "0", "0", "0.00", "0" };
		for (int i = 0; i < 4; i++)
		{
			Text value;
			RectTransform item = Tile(col, array[i], new Vector2(0f, 0.5f), new Vector2(76 + i * 158, -112f), new Vector2(150f, 84f), (i == 1) ? FBUI.Red : FBUI.Gold, out value);
			tiles.Add((item, value, array2[i], array3[i]));
		}
		Text tip = Tx(transform, "DICA  ·  " + DICAS[UnityEngine.Random.Range(0, DICAS.Length)], 17, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 24f), TextAnchor.MiddleCenter, title: false, new Color(0.85f, 0.86f, 0.88f));
		RectTransform rectTransform = FBUI.Rect(transform, "direita", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-250f, -20f), new Vector2(380f, 460f));
		FBUI.Panel(rectTransform, "anel_fundo", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(210f, 210f), new Color(1f, 1f, 1f, 0.1f)).sprite = RingSprite;
		Image ringIm = FBUI.Panel(rectTransform, "anel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(210f, 210f), FBUI.Gold);
		ringIm.sprite = RingSprite;
		ringIm.type = Image.Type.Filled;
		ringIm.fillMethod = Image.FillMethod.Radial360;
		ringIm.fillOrigin = 2;
		ringIm.fillClockwise = false;
		Text num = Tx(rectTransform, auto ? "" : "✓", 76, new Vector2(0.5f, 0.5f), new Vector2(0f, 78f), new Vector2(200f, 90f), TextAnchor.MiddleCenter, title: true, Color.white);
		Tx(rectTransform, auto ? "RENASCENDO" : "PRONTO PARA VOLTAR", 15, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(200f, 20f), TextAnchor.MiddleCenter, title: false, FBUI.Mut, shadow: false);
		bool done = false;
		Action go = delegate
		{
			if (!done)
			{
				done = true;
				CloseCards();
				respawn();
			}
		};
		FBUI.Button(rectTransform, "RENASCER  [ESPAÇO]", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -98f), new Vector2(330f, 62f), go, primary: true);
		FBUI.Button(rectTransform, "MENU PRINCIPAL", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -168f), new Vector2(330f, 48f), delegate
		{
			menu();
		});
		CanvasGroup rightCg = rectTransform.gameObject.AddComponent<CanvasGroup>();
		CanvasGroup colCg = col.gameObject.AddComponent<CanvasGroup>();
		float total = ((auto && (bool)FBMatch.I) ? Mathf.Max(0.1f, FBMatch.I.RespawnLeft) : 0f);
		fx.tick = delegate(float t)
		{
			bgIm.color = new Color(0f, 0f, 0f, 0.5f * Ease(t / 0.5f));
			float num3 = Ease((t - 0.05f) / 0.55f);
			col.anchoredPosition = new Vector2(Mathf.Lerp(-300f, 380f, num3), 0f);
			colCg.alpha = num3;
			title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, Ease(t / 0.35f));
			slash.rectTransform.sizeDelta = new Vector2(860f * Ease((t - 0.1f) / 0.5f), 6f);
			rightCg.alpha = Ease((t - 0.35f) / 0.4f);
			CanvasGroup canvasGroup = rightCg;
			bool interactable = (rightCg.blocksRaycasts = t > 0.6f);
			canvasGroup.interactable = interactable;
			for (int j = 0; j < tiles.Count; j++)
			{
				float num4 = Ease((t - 0.45f - (float)j * 0.08f) / 0.5f);
				tiles[j].rt.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, EaseBack(num4));
				tiles[j].v.text = Ptbr(tiles[j].val * num4, tiles[j].fmt);
			}
			if ((bool)hpFill && (bool)by)
			{
				hpFill.rectTransform.sizeDelta = new Vector2(320f * Mathf.Clamp01(by.hp / Mathf.Max(1f, by.maxHp)) * Ease((t - 0.6f) / 0.6f), 0f);
			}
			if (auto && (bool)FBMatch.I)
			{
				float num5 = Mathf.Max(0f, FBMatch.I.RespawnLeft);
				ringIm.fillAmount = ((total > 0f) ? (num5 / total) : 0f);
				num.text = Mathf.CeilToInt(num5).ToString();
				num.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Pow(1f - num5 % 1f, 6f));
			}
			else
			{
				ringIm.fillAmount = Ease((t - 0.4f) / 0.6f);
			}
			tip.color = new Color(0.85f, 0.86f, 0.88f, Ease((t - 0.9f) / 0.5f));
			Keyboard current = Keyboard.current;
			if (t > 0.6f && current != null && (current.spaceKey.wasPressedThisFrame || current.enterKey.wasPressedThisFrame))
			{
				go();
			}
		};
	}

	public void VictoryCard(bool win, string modeName, string mapName, int a, int b, int place, int total, int reward, Action again, Action menu)
	{
		CardFx fx;
		Transform transform = OpenScreen("tela_fim", out fx).transform;
		Image bgIm = transform.GetComponent<Image>();
		FBMatch.StatsT stats = FBMatch.Stats;
		Color acc = (win ? FBUI.Gold : FBUI.Red);
		int team = FBSession.team;
		FBUI.Panel(transform, "faixa_topo", new Vector2(0f, 0.58f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(acc.r * 0.55f, acc.g * 0.4f, acc.b * 0.3f, 0.75f)).sprite = TopGrad;
		Image line = FBUI.Panel(transform, "linha", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(0f, 4f), acc);
		string s = ((place <= 0) ? (win ? "VITÓRIA" : "DERROTA") : (win ? "BOOYAH!" : $"#{place}"));
		Text title = Tx(transform, s, 128, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1000f, 150f), TextAnchor.MiddleCenter, title: true, acc);
		Text eyebrow = Tx(transform, (modeName + " · " + mapName).ToUpper(), 18, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1000f, 24f), TextAnchor.MiddleCenter, title: false, new Color(0.9f, 0.9f, 0.92f));
		RectTransform rectTransform = FBUI.Rect(transform, "placar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -282f), new Vector2(900f, 70f));
		CanvasGroup scoreCg = rectTransform.gameObject.AddComponent<CanvasGroup>();
		Text sa = null;
		Text sb = null;
		Image ba = null;
		Image bb = null;
		if (place > 0)
		{
			Tx(rectTransform, $"<b>#{place}</b> <size=22><color=#9aa3ad>DE {total}</color></size>", 52, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 70f), TextAnchor.MiddleCenter, title: true, Color.white);
		}
		else
		{
			Color c = new Color(0.3f, 0.6f, 1f);
			Color c2 = new Color(1f, 0.36f, 0.33f);
			Tx(rectTransform, FBMatch.TEAM_SHORT[team] + " <size=16><color=#9aa3ad>(VOCÊ)</color></size>", 22, new Vector2(0.5f, 0.5f), new Vector2(-370f, 18f), new Vector2(300f, 28f), TextAnchor.MiddleRight, title: true, c);
			Tx(rectTransform, FBMatch.TEAM_SHORT[1 - team], 22, new Vector2(0.5f, 0.5f), new Vector2(370f, 18f), new Vector2(300f, 28f), TextAnchor.MiddleLeft, title: true, c2);
			sa = Tx(rectTransform, "0", 54, new Vector2(0.5f, 0.5f), new Vector2(-122f, 4f), new Vector2(160f, 64f), TextAnchor.MiddleRight, title: true, Color.white);
			Tx(rectTransform, "×", 30, new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(40f, 40f), TextAnchor.MiddleCenter, title: true, FBUI.Mut);
			sb = Tx(rectTransform, "0", 54, new Vector2(0.5f, 0.5f), new Vector2(122f, 4f), new Vector2(160f, 64f), TextAnchor.MiddleLeft, title: true, Color.white);
			Image image = FBUI.Panel(rectTransform, "barra", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(700f, 8f), new Color(1f, 1f, 1f, 0.08f));
			ba = FBUI.Panel(image.transform, "a", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, c);
			ba.rectTransform.pivot = new Vector2(0f, 0.5f);
			bb = FBUI.Panel(image.transform, "b", new Vector2(1f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, c2);
			bb.rectTransform.pivot = new Vector2(1f, 0.5f);
		}
		RectTransform left = FBUI.Rect(transform, "desempenho", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300f, -110f), new Vector2(520f, 330f));
		CanvasGroup leftCg = left.gameObject.AddComponent<CanvasGroup>();
		Tx(left, "SEU DESEMPENHO", 18, new Vector2(0f, 1f), new Vector2(260f, -12f), new Vector2(520f, 24f), TextAnchor.MiddleLeft, title: true, acc);
		int num = Mathf.RoundToInt(100f * (float)stats.hits / (float)Mathf.Max(1, stats.shots));
		float num2 = (float)stats.kills / Mathf.Max(1f, stats.deaths);
		string[] array = new string[6] { "ABATES", "MORTES", "K/D", "HEADSHOTS", "PRECISÃO %", "TIROS" };
		float[] array2 = new float[6] { stats.kills, stats.deaths, num2, stats.heads, num, stats.shots };
		string[] array3 = new string[6] { "0", "0", "0.00", "0", "0", "0" };
		List<(RectTransform rt, Text v, float val, string fmt)> tiles = new List<(RectTransform, Text, float, string)>();
		for (int i = 0; i < 6; i++)
		{
			Text value;
			RectTransform item = Tile(left, array[i], new Vector2(0f, 1f), new Vector2(85 + i % 3 * 176, -84 - i / 3 * 100), new Vector2(168f, 90f), (i == 1) ? FBUI.Red : acc, out value);
			tiles.Add((item, value, array2[i], array3[i]));
		}
		RectTransform right = FBUI.Rect(transform, "destaques", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300f, -110f), new Vector2(520f, 330f));
		CanvasGroup rightCg = right.gameObject.AddComponent<CanvasGroup>();
		Tx(right, "DESTAQUES DA PARTIDA", 18, new Vector2(0f, 1f), new Vector2(260f, -12f), new Vector2(520f, 24f), TextAnchor.MiddleLeft, title: true, acc);
		List<(string, int, int, int)> list = (from kv in FBMatch.Tally
			select (name: kv.Key, team: kv.Value.team, k: kv.Value.k, d: kv.Value.d) into x
			orderby x.k descending, x.d
			select x).Take(5).ToList();
		if (list.Count > 0)
		{
			(string, int, int, int) tuple = list[0];
			Image image2 = FBUI.Panel(right, "mvp", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, -78f), new Vector2(520f, 76f), new Color(acc.r * 0.3f, acc.g * 0.25f, acc.b * 0.15f, 0.85f));
			Tx(image2.transform, "MVP", 15, new Vector2(0f, 0.5f), new Vector2(46f, 14f), new Vector2(80f, 20f), TextAnchor.MiddleCenter, title: true, acc, shadow: false);
			Tx(image2.transform, "★", 34, new Vector2(0f, 0.5f), new Vector2(46f, -10f), new Vector2(60f, 40f), TextAnchor.MiddleCenter, title: true, acc, shadow: false);
			Tx(image2.transform, tuple.Item1.ToUpper(), 30, new Vector2(0f, 0.5f), new Vector2(270f, 8f), new Vector2(340f, 36f), TextAnchor.MiddleLeft, title: true, Color.white);
			Tx(image2.transform, string.Format("{0} ABATES · {1} MORTES · {2}", tuple.Item3, tuple.Item4, (tuple.Item2 == team) ? "SEU TIME" : "TIME INIMIGO"), 14, new Vector2(0f, 0.5f), new Vector2(270f, -20f), new Vector2(340f, 18f), TextAnchor.MiddleLeft, title: false, FBUI.Mut, shadow: false);
			for (int j = 1; j < list.Count; j++)
			{
				(string, int, int, int) tuple2 = list[j];
				int num3 = -130 - (j - 1) * 40;
				Image image3 = FBUI.Panel(right, "linha" + j, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(260f, num3), new Vector2(520f, 34f), new Color(1f, 1f, 1f, (j % 2 == 0) ? 0.04f : 0.07f));
				Tx(image3.transform, $"{j + 1}", 16, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(30f, 20f), TextAnchor.MiddleCenter, title: true, FBUI.Mut, shadow: false);
				Tx(image3.transform, tuple2.Item1.ToUpper(), 17, new Vector2(0f, 0.5f), new Vector2(190f, 0f), new Vector2(280f, 22f), TextAnchor.MiddleLeft, title: true, (tuple2.Item2 == team) ? new Color(0.55f, 0.78f, 1f) : new Color(1f, 0.6f, 0.58f), shadow: false);
				Tx(image3.transform, $"{tuple2.Item3} / {tuple2.Item4}", 17, new Vector2(1f, 0.5f), new Vector2(-60f, 0f), new Vector2(100f, 22f), TextAnchor.MiddleRight, title: true, Color.white, shadow: false);
			}
		}
		RectTransform rectTransform2 = FBUI.Rect(transform, "rodape", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(1100f, 120f));
		CanvasGroup footCg = rectTransform2.gameObject.AddComponent<CanvasGroup>();
		Text money = Tx(rectTransform2, "", 30, new Vector2(0.5f, 0.5f), new Vector2(-380f, 20f), new Vector2(320f, 40f), TextAnchor.MiddleLeft, title: true, FBUI.Gold);
		Tx(rectTransform2, $"PASSE <b>+{(win ? 500 : 200)} XP</b>   ·   CRÉDITOS <b>${FBSession.money}</b>", 16, new Vector2(0.5f, 0.5f), new Vector2(-380f, -14f), new Vector2(320f, 22f), TextAnchor.MiddleLeft, title: false, new Color(0.85f, 0.86f, 0.88f), shadow: false);
		bool done = false;
		Action go = delegate
		{
			if (!done)
			{
				done = true;
				again();
			}
		};
		FBUI.Button(rectTransform2, "REVANCHE  [R]", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170f, 10f), new Vector2(300f, 66f), go, primary: true);
		FBUI.Button(rectTransform2, "MENU PRINCIPAL", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(440f, 10f), new Vector2(220f, 52f), delegate
		{
			menu();
		});
		fx.tick = delegate(float t)
		{
			bgIm.color = new Color(0.01f, 0.01f, 0.02f, 0.78f * Ease(t / 0.6f));
			float t2 = EaseBack(t / 0.55f);
			title.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, t2);
			title.color = new Color(acc.r, acc.g, acc.b, Ease(t / 0.3f));
			line.rectTransform.sizeDelta = new Vector2(900f * Ease((t - 0.2f) / 0.6f), 4f);
			eyebrow.color = new Color(0.9f, 0.9f, 0.92f, Ease((t - 0.3f) / 0.4f));
			scoreCg.alpha = Ease((t - 0.45f) / 0.4f);
			float num4 = Ease((t - 0.55f) / 1.1f);
			if ((bool)sa)
			{
				sa.text = Mathf.RoundToInt((float)a * num4).ToString();
				sb.text = Mathf.RoundToInt((float)b * num4).ToString();
				float num5 = Mathf.Max(1, a + b);
				ba.rectTransform.sizeDelta = new Vector2((float)(700 * a) / num5 * num4, 0f);
				bb.rectTransform.sizeDelta = new Vector2((float)(700 * b) / num5 * num4, 0f);
			}
			float num6 = Ease((t - 0.7f) / 0.5f);
			leftCg.alpha = num6;
			left.anchoredPosition = new Vector2(Mathf.Lerp(-380f, -300f, num6), -110f);
			float num7 = Ease((t - 0.85f) / 0.5f);
			rightCg.alpha = num7;
			right.anchoredPosition = new Vector2(Mathf.Lerp(380f, 300f, num7), -110f);
			for (int k = 0; k < tiles.Count; k++)
			{
				float num8 = Ease((t - 0.9f - (float)k * 0.07f) / 0.7f);
				tiles[k].v.text = Ptbr(tiles[k].val * num8, tiles[k].fmt);
				tiles[k].rt.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, EaseBack(num8));
			}
			float alpha = Ease((t - 1.2f) / 0.5f);
			footCg.alpha = alpha;
			CanvasGroup canvasGroup = footCg;
			bool interactable = (footCg.blocksRaycasts = t > 1.3f);
			canvasGroup.interactable = interactable;
			money.text = $"+${Mathf.RoundToInt((float)reward * Ease((t - 1.3f) / 1.2f))}";
			Keyboard current = Keyboard.current;
			if (t > 1.3f && current != null && current.rKey.wasPressedThisFrame)
			{
				go();
			}
		};
	}

	public void Pause(bool on, Action menu)
	{
		if (!on)
		{
			if ((bool)pausePanel)
			{
				UnityEngine.Object.Destroy(pausePanel);
			}
			pausePanel = null;
			return;
		}
		Image bg = FBUI.Panel(root, "pausa", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
		pausePanel = bg.gameObject;
		FBUI.Label(bg.transform, "PAUSADO", 60, C, C, new Vector2(0f, 160f), new Vector2(600f, 80f), TextAnchor.MiddleCenter, title: true, FBUI.Gold);
		FBUI.Button(bg.transform, "CONTINUAR", C, C, new Vector2(0f, 50f), new Vector2(320f, 56f), delegate
		{
			FBMatch.Paused = false;
			Pause(on: false, null);
			if (!FBInput.Touch)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
			}
		}, primary: true);
		FBUI.Button(bg.transform, "AJUSTES", C, C, new Vector2(0f, -16f), new Vector2(320f, 50f), delegate
		{
			FBSettings.Open(bg.transform);
		});
		FBUI.Button(bg.transform, "MENU PRINCIPAL", C, C, new Vector2(0f, -76f), new Vector2(320f, 50f), delegate
		{
			FBMatch.Paused = false;
			menu();
		});
	}

	private static readonly int __fbt_Update = FBTempo.Id("FBHUD.Update");

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
		float deltaTime = Time.deltaTime;
		FBPlayer i = FBPlayer.I;
		Camera main = Camera.main;
		canvas.enabled = !FBMatch.I || FBMatch.I.mode == null || !FBMatch.I.mode.role || FBMatch.Paused || CardOpen;
		if ((bool)i)
		{
			FBCharacter ch = i.ch;
			WeaponDef cur = i.wu.Cur;
			float num = Mathf.Clamp01(ch.hp / ch.maxHp);
			hpBar.fillAmount = num;
			hpLagV = Mathf.Max(num, Mathf.MoveTowards((hpLagV > 1f) ? 1f : hpLagV, num, deltaTime * 0.6f));
			hpLag.fillAmount = hpLagV;
			hpBar.color = (FBMed.Healing ? new Color(0.61f, 0.9f, 0.69f) : (ch.knocked ? new Color(1f, 0.45f, 0.2f) : ((ch.hp < 30f) ? new Color(1f, 0.29f, 0.23f) : HPC)));
			hpT.text = Mathf.CeilToInt(Mathf.Max(0f, ch.hp)).ToString();
			float value = ((ch.vest != null) ? (ch.vest.dur / ch.vest.max) : (ch.armor / 100f));
			armorBar.fillAmount = Mathf.Clamp01(value);
			armorT.text = ((ch.vest != null) ? FBLoot.ArmorLabel(ch.vest.key) : "") + ((ch.helmetFF != null) ? (" · " + FBLoot.ArmorLabel(ch.helmetFF.key)) : "");
			Weapon(i, cur);
			bool flag = cur != null && ch.alive && !ch.sprint && !FBScopes.Scoped && !i.Driving && !CardOpen && !i.FreeLooking && (!i.firstPerson || !ch.aiming);
			if (cur != null)
			{
				i.wu.recoil.Spread(cur, new FBRecoil.State
				{
					aiming = ch.aiming,
					crouch = ch.crouch,
					moving = (ch.velocity.Flat().sqrMagnitude > 0.25f),
					air = !ch.grounded,
					zoom = 1f,
					grip = 1f
				});
			}
			float num2 = (i.CrossGapPx + 4.5f) * 1.25f;
			bool flag2 = i.AimBlocked && flag;
			Color color = (flag2 ? Hex(16726832u) : Color.white);
			for (int j = 0; j < 4; j++)
			{
				cross[j].GetComponent<Image>().color = color;
				cross[j].sizeDelta = ((j < 2) ? new Vector2(2.5f, 11.25f) : new Vector2(11.25f, 2.5f));
			}
			if (!blockX)
			{
				blockX = T(root, "✕", 14f, C, C, 0f, 0f, 20f, 20f, TextAnchor.MiddleCenter, title: true, Hex(16726832u));
			}
			blockX.gameObject.SetActive(flag2);
			crossDot.color = color;
			for (int k = 0; k < 4; k++)
			{
				cross[k].gameObject.SetActive(flag);
				cross[k].anchoredPosition = k switch
				{
					2 => new Vector2(num2, 0f), 
					1 => new Vector2(0f, 0f - num2), 
					0 => new Vector2(0f, num2), 
					_ => new Vector2(0f - num2, 0f), 
				};
			}
			crossDot.gameObject.SetActive(flag || (cur == null && ch.alive && !i.firstPerson));
			Minimap(i, main);
		}
		hitT -= deltaTime;
		killT -= deltaTime;
		Color color2 = hitMark.color;
		color2.a = Mathf.Clamp01(hitT * 6f);
		hitMark.color = color2;
		hitMark.rectTransform.localScale = Vector3.one * hitBig * (1f + Mathf.Max(0f, hitT) * 2f);
		dmgT = Mathf.Max(0f, dmgT - deltaTime * 0.9f);
		FBCharacter player = FBMatch.Player;
		float b = (((bool)player && player.alive && player.hp < player.maxHp * 0.3f) ? (Mathf.Lerp(0.7f, 0.38f, player.hp / (player.maxHp * 0.3f)) * (0.72f + 0.28f * Mathf.Sin(Time.time * 4.2f))) : 0f);
		vignette.color = new Color(0.55f, 0f, 0.02f, Mathf.Clamp01(Mathf.Max(Mathf.Clamp01(dmgT) * 0.85f, b)));
		dmgDirT -= deltaTime;
		dmgArrow.color = new Color(1f, 0.2f, 0.15f, Mathf.Clamp01(dmgDirT));
		centerTTL -= deltaTime;
		if (centerTTL <= 0f)
		{
			centerT.text = "";
		}
		mtoastT -= deltaTime;
		if (mtoastT <= 0f)
		{
			mtoastT2.text = "";
		}
		toastT -= deltaTime;
		if (toastT <= 0f && toastRT.gameObject.activeSelf)
		{
			toastRT.gameObject.SetActive(value: false);
		}
		bannerT -= deltaTime;
		if (bannerT <= 0f && bannerRT.gameObject.activeSelf)
		{
			bannerRT.gameObject.SetActive(value: false);
		}
		wipeT -= deltaTime;
		if (wipeT <= 0f && wipeRT.gameObject.activeSelf)
		{
			wipeRT.gameObject.SetActive(value: false);
		}
		countTT -= deltaTime;
		countT.color = new Color(countT.color.r, countT.color.g, countT.color.b, Mathf.Clamp01(countTT * 2f));
		countT.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, countTT - 0.6f));
		killcamTT -= deltaTime;
		if (killcamTT <= 0f)
		{
			killcamT.text = "";
		}
		cashPlusT -= deltaTime;
		if (cashPlusT <= 0f)
		{
			cashPlus.text = "";
		}
		for (int l = 0; l < feed.Count; l++)
		{
			(Text t, float ttl) tuple = feed[l];
			Text item = tuple.t;
			float item2 = tuple.ttl;
			item2 -= deltaTime;
			feed[l] = (item, item2);
			Color color3 = item.color;
			color3.a = Mathf.Clamp01(item2);
			item.color = color3;
		}
		for (int num3 = floats.Count - 1; num3 >= 0; num3--)
		{
			(Text t, Vector3 p, float t0) tuple2 = floats[num3];
			Text item3 = tuple2.t;
			Vector3 item4 = tuple2.p;
			float item5 = tuple2.t0;
			float num4 = (Time.time - item5) / 0.9f;
			if (num4 >= 1f || !main)
			{
				UnityEngine.Object.Destroy(item3.gameObject);
				floats.RemoveAt(num3);
			}
			else
			{
				Vector3 vector = main.WorldToScreenPoint(item4);
				item3.gameObject.SetActive(vector.z > 0f);
				float num5 = (float)Screen.height / 1080f;
				if (vector.z > 0f)
				{
					item3.rectTransform.position = vector + new Vector3(46f + num4 * 10f, 34f + num4 * 38f, 0f) * num5;
				}
				item3.rectTransform.localScale = Vector3.one * ((num4 < 0.12f) ? Mathf.Lerp(1.35f, 1f, num4 / 0.12f) : 1f);
				Color color4 = item3.color;
				color4.a = 1f - num4 * num4;
				item3.color = color4;
			}
		}
		boardRT.gameObject.SetActive(FBInput.Read().scoreboard && (bool)FBMatch.I && !CardOpen);
		if (boardRT.gameObject.activeSelf)
		{
			Board();
		}
		Squad();
		Tags(main);
	}

	private void Board()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<size=22><b>" + FBMatch.I.MapName + " · " + FBMatch.I.mode?.name + "</b></size>\n\n");
		int[] array = ((!FBMatch.FFA) ? new int[2] { 0, 1 } : new int[1] { -1 });
		foreach (int t in array)
		{
			if (t >= 0)
			{
				stringBuilder.Append("<color=" + ((t == 0) ? "#8cc8ff" : "#ff8a80") + "><b>" + FBMatch.TEAM_NAME[t] + "</b></color>\n");
			}
			foreach (FBCharacter item in from a in FBMatch.Actors()
				where t < 0 || a.team == t
				orderby (!a.isPlayer) ? a.kills : FBMatch.Stats.kills descending
				select a)
			{
				stringBuilder.Append(string.Format("{0}{1}{2}{3}   <b>{4}</b> / {5}\n", item.isPlayer ? "<color=#f5aa17>" : "", item.alive ? "" : "✖ ", item.Name, item.isPlayer ? "</color>" : "", item.isPlayer ? FBMatch.Stats.kills : item.kills, item.isPlayer ? FBMatch.Stats.deaths : item.deaths));
			}
			stringBuilder.Append('\n');
		}
		boardT.text = stringBuilder.ToString();
	}

	private void Weapon(FBPlayer pl, WeaponDef w)
	{
		FBCharacter ch = pl.ch;
		wSil.enabled = false;
		Text text = wT;
		Text text2 = fmT;
		Text text3 = resT;
		string text5 = (ammoT.text = "");
		string text7 = (text3.text = text5);
		string text9 = (text2.text = text7);
		text.text = text9;
		magRT.gameObject.SetActive(value: false);
		medRT.gameObject.SetActive(value: false);
	}

	private static (int n, Color col) Slot(FBCharacter a)
	{
		if (squadMatch != FBMatch.I)
		{
			squadMatch = FBMatch.I;
			squadNo.Clear();
		}
		if ((bool)a && a.isPlayer && !squadNo.ContainsKey(a))
		{
			squadNo[a] = 1;
		}
		if (!squadNo.ContainsKey(a))
		{
			squadNo[a] = squadNo.Count + 1 + ((!squadNo.Values.Contains(1)) ? 1 : 0);
		}
		int num = squadNo[a];
		return (n: num, col: SQUAD_COL[(num - 1) % SQUAD_COL.Length]);
	}

	private static bool Ally(FBCharacter a)
	{
		if (!FBMatch.FFA && (bool)a && a.team == FBSession.team && !a.isPlayer)
		{
			return !FBMatch.Civs.Contains(a);
		}
		return false;
	}

	private void Squad()
	{
		FBCharacter player = FBMatch.Player;
		if (!player || !FBMatch.I)
		{
			squadRT.gameObject.SetActive(value: false);
			return;
		}
		List<FBCharacter> list = FBMatch.Actors().Where(Ally).ToList();
		if (list.Count == 0)
		{
			squadRT.gameObject.SetActive(value: false);
			return;
		}
		Slot(player);
		list.Insert(0, player);
		list = list.OrderBy((FBCharacter a) => Slot(a).n).Take(10).ToList();
		squadRT.gameObject.SetActive(value: true);
		int num = list.Count((FBCharacter a) => a.alive);
		squadCt.text = $"<color=#ffffff>{num}</color>/{list.Count} VIVOS" + ((num < list.Count) ? $" · <color=#ff6a5a>{list.Count - num} ✖</color>" : "");
		while (sqRows.Count < list.Count)
		{
			int count = sqRows.Count;
			Image image = Box(squadRT, "r", new Vector2(0f, 1f), new Vector2(0f, 1f), 0f, -(17 + count * 29), 200f, 26f, Color.white);
			image.sprite = GradSprite;
			image.color = new Color(0f, 0f, 0f, 1f);
			Image image2 = Box(image.transform, "n", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 3f, 0f, 18f, 18f, Color.white);
			Text item = T(image2.transform, "", 12f, C, C, 0f, 0f, 18f, 18f, TextAnchor.MiddleCenter, title: true, Color.white);
			Text text = T(image.transform, "", 12f, new Vector2(0f, 1f), new Vector2(0f, 1f), 27f, -3f, 133f, 14f, TextAnchor.MiddleLeft, title: true, Color.white);
			text.horizontalOverflow = HorizontalWrapMode.Wrap;
			Image item2 = Fill(Box(image.transform, "vida", new Vector2(0f, 1f), new Vector2(0f, 1f), 27f, -19f, 133f, 3f, new Color(1f, 1f, 1f, 0.18f)).transform, Hex(15921906u));
			Text item3 = T(image.transform, "", 10f, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), -6f, 0f, 28f, 12f, TextAnchor.MiddleRight, title: true, new Color(1f, 1f, 1f, 0.75f));
			sqRows.Add((image.rectTransform, image2, item, text, item2, item3, image.gameObject.AddComponent<CanvasGroup>()));
		}
		for (int i = 0; i < sqRows.Count; i++)
		{
			(RectTransform, Image, Text, Text, Image, Text, CanvasGroup) tuple = sqRows[i];
			bool flag = i < list.Count;
			tuple.Item1.gameObject.SetActive(flag);
			if (flag)
			{
				FBCharacter fBCharacter = list[i];
				(int, Color) tuple2 = Slot(fBCharacter);
				tuple.Item2.color = (fBCharacter.alive ? tuple2.Item2 : new Color(0.33f, 0.33f, 0.33f));
				tuple.Item3.text = tuple2.Item1.ToString();
				tuple.Item4.text = ((!fBCharacter.isPlayer) ? fBCharacter.Name : ((FBSession.online || FBSession.nameChosen) ? FBSession.name : "Você"));
				tuple.Item4.color = ((!fBCharacter.alive) ? new Color(0.73f, 0.73f, 0.73f) : (fBCharacter.isPlayer ? Hex(16766314u) : Color.white));
				tuple.Item7.alpha = (fBCharacter.alive ? 1f : 0.5f);
				tuple.Item5.fillAmount = (fBCharacter.alive ? Mathf.Clamp01(fBCharacter.hp / Mathf.Max(1f, fBCharacter.maxHp)) : 0f);
				tuple.Item5.color = ((fBCharacter.alive && fBCharacter.hp < 30f) ? Hex(16730682u) : Hex(15921906u));
				tuple.Item6.text = (fBCharacter.isPlayer ? "" : ((!fBCharacter.alive) ? "✖" : (Mathf.RoundToInt(Vector3.Distance(fBCharacter.Pos, player.Pos)) + "m")));
			}
		}
	}

	private void Tags(Camera cam)
	{
		int num = 0;
		if (!cam || !FBMatch.I)
		{
			foreach (var item3 in tagW)
			{
				item3.rt.gameObject.SetActive(value: false);
			}
			return;
		}
		Vector2 size = ((RectTransform)canvas.transform).rect.size;
		Ray r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
		FBCharacter fBCharacter = null;
		float num2 = 1E+09f;
		foreach (FBCharacter item4 in FBMatch.Actors())
		{
			if (item4.isPlayer || !item4.alive || !item4.Head || !Ally(item4))
			{
				continue;
			}
			Vector3 vector = item4.Pos + Vector3.up * 0.15f * item4.ScaleK;
			Vector3 position = item4.Head.position;
			float num3 = Vector3.Dot((vector + position) * 0.5f - r.origin, r.direction);
			if (num3 < 0.5f || num3 > 250f)
			{
				continue;
			}
			float tr;
			float num4 = SegRayDist(vector, position, r, out tr);
			float num5 = (0.42f + tr * 0.006f) * item4.ScaleK;
			if (!(num4 > num5) && !(tr >= num2))
			{
				Vector3 vector2 = (vector + position) * 0.5f - r.origin;
				if (!FBWorld.RayHit(r.origin, vector2.normalized, vector2.magnitude - 0.6f * item4.ScaleK))
				{
					fBCharacter = item4;
					num2 = tr;
				}
			}
		}
		if ((bool)fBCharacter)
		{
			tagFocus = fBCharacter;
			tagFocusT = Time.unscaledTime;
		}
		foreach (FBCharacter item5 in FBMatch.Actors())
		{
			if (item5.isPlayer || !item5.alive || !item5.Head)
			{
				continue;
			}
			bool flag = Ally(item5);
			if (!flag)
			{
				continue;
			}
			bool flag2 = item5 == tagFocus && Time.unscaledTime - tagFocusT < 0.5f;
			bool knocked = item5.knocked;
			if (!flag2 && !knocked)
			{
				continue;
			}
			Vector3 vector3 = item5.Head.position + Vector3.up * 0.55f * item5.ScaleK;
			float num6 = Vector3.Distance(vector3, cam.transform.position);
			Vector3 vector4 = cam.WorldToViewportPoint(vector3);
			float num7 = vector4.x * 2f - 1f;
			float num8 = vector4.y * 2f - 1f;
			bool flag3 = vector4.z < 0f;
			bool flag4 = flag3 || Mathf.Abs(num7) > 0.96f || Mathf.Abs(num8) > 0.94f;
			if (flag4 && !knocked)
			{
				continue;
			}
			if (flag4)
			{
				if (flag3)
				{
					num7 = 0f - num7;
					num8 = 0f - num8;
					if (Mathf.Abs(num7) < 0.05f && Mathf.Abs(num8) < 0.05f)
					{
						num8 = -1f;
					}
				}
				float num9 = Mathf.Max(Mathf.Abs(num7) / 0.94f, Mathf.Abs(num8) / 0.86f, 0.001f);
				num7 /= num9;
				num8 /= num9;
			}
			if (num >= tagW.Count)
			{
				RectTransform rectTransform = R(root, "cracha", C, new Vector2(0.5f, 0f), 0f, 0f, 160f, 22f);
				Image image = Box(rectTransform, "n", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0f, 0f, 15f, 15f, Color.white);
				Text item = T(image.transform, "", 10f, C, C, 0f, 0f, 15f, 15f, TextAnchor.MiddleCenter, title: true, Color.white);
				Text text = T(rectTransform, "", 11f, new Vector2(0.5f, 1f), new Vector2(0f, 1f), 0f, 0f, 200f, 15f, TextAnchor.MiddleLeft, title: true, Color.white);
				text.supportRichText = true;
				Image image2 = Box(rectTransform, "vida", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0f, 0f, 64f, 4f, new Color(0f, 0f, 0f, 0.55f));
				Image item2 = Fill(image2.transform, Color.white);
				tagW.Add((rectTransform, image, item, text, image2.rectTransform, item2));
			}
			(RectTransform, Image, Text, Text, RectTransform, Image) tuple = tagW[num++];
			tuple.Item1.gameObject.SetActive(value: true);
			tuple.Item1.anchoredPosition = new Vector2(num7 * size.x / 2f, num8 * size.y / 2f - (flag4 ? 13.75f : 0f));
			(int, Color) tuple2 = (flag ? Slot(item5) : (0, Color.clear));
			int num10 = Mathf.RoundToInt(num6);
			tuple.Item2.gameObject.SetActive(flag);
			tuple.Item2.color = tuple2.Item2;
			tuple.Item3.text = tuple2.Item1.ToString();
			tuple.Item4.fontSize = Mathf.RoundToInt((float)(flag4 ? 10 : ((num6 > 40f) ? 10 : 11)) * 1.25f);
			tuple.Item4.text = ((!flag2) ? $"CAÍDO · {num10}m" : (flag4 ? $"{num10}m" : (item5.Name + (knocked ? " · CAÍDO" : "") + ((flag && num6 > 20f) ? $" <size={Mathf.RoundToInt(12.5f)}><color=#cfe6ffbf>{num10}m</color></size>" : ""))));
			tuple.Item4.color = ((!flag2 || knocked) ? Hex(16747136u) : (flag ? Hex(13625087u) : Hex(16747136u)));
			float preferredWidth = tuple.Item4.preferredWidth;
			float num11 = (flag ? 23.75f : 0f);
			float num12 = preferredWidth + num11;
			tuple.Item2.rectTransform.anchoredPosition = new Vector2((0f - num12) / 2f + 9.375f, 0f);
			tuple.Item4.rectTransform.anchoredPosition = new Vector2((0f - num12) / 2f + num11, 0f);
			tuple.Item5.gameObject.SetActive(!flag4 && flag2);
			tuple.Item6.fillAmount = Mathf.Clamp01(item5.hp / 100f);
			tuple.Item6.color = ((item5.hp < 30f) ? Hex(16730682u) : (flag ? tuple2.Item2 : Hex(16730682u)));
			CanvasGroup orAdd = tuple.Item1.GetOrAdd<CanvasGroup>();
			float num13 = (flag2 ? Mathf.Clamp01(1f - (Time.unscaledTime - tagFocusT - 0.35f) / 0.15f) : 1f);
			orAdd.alpha = (flag4 ? 0.9f : 1f) * num13;
			if (!flag4)
			{
				tuple.Item2.rectTransform.anchoredPosition += new Vector2(0f, 0f);
			}
		}
		for (int i = num; i < tagW.Count; i++)
		{
			tagW[i].rt.gameObject.SetActive(value: false);
		}
	}

	private static float SegRayDist(Vector3 a, Vector3 b, Ray r, out float tr)
	{
		Vector3 vector = b - a;
		Vector3 direction = r.direction;
		Vector3 rhs = a - r.origin;
		float num = Vector3.Dot(vector, vector);
		float num2 = Vector3.Dot(vector, direction);
		float num3 = Vector3.Dot(direction, direction);
		float num4 = Vector3.Dot(vector, rhs);
		float num5 = Vector3.Dot(direction, rhs);
		float num6 = num * num3 - num2 * num2;
		float num7 = ((num6 > 1E-06f) ? Mathf.Clamp01((num2 * num5 - num3 * num4) / num6) : 0f);
		tr = Mathf.Max(0f, (num5 + num2 * num7) / num3);
		num7 = ((num > 1E-06f) ? Mathf.Clamp01(Vector3.Dot(r.origin + direction * tr - a, vector) / num) : 0f);
		return Vector3.Distance(a + vector * num7, r.origin + direction * tr);
	}

	private void Minimap(FBPlayer pl, Camera cam)
	{
		float camYaw = pl.camYaw;
		float sc = 2.78125f;
		Vector3 me = pl.ch.Pos;
		Vector2 c2 = Vector2.zero;
		Quaternion rot = Quaternion.Euler(0f, 0f, camYaw);
		if ((bool)FBMinimap.Tex)
		{
			if (!miniMap.gameObject.activeSelf)
			{
				miniMap.gameObject.SetActive(value: true);
				miniMap.texture = FBMinimap.Tex;
				miniMap.transform.SetAsFirstSibling();
			}
			Vector2 vector = FBMinimap.UV(me);
			miniMap.rectTransform.sizeDelta = Vector2.one * FBMinimap.Size * sc;
			miniMap.rectTransform.localRotation = rot;
			miniMap.rectTransform.anchoredPosition = c2 + (Vector2)(rot * new Vector2((0.5f - vector.x) * FBMinimap.Size * sc, (0.5f - vector.y) * FBMinimap.Size * sc));
		}
		if ((bool)cam)
		{
			float num = Mathf.Atan(Mathf.Tan(cam.fieldOfView * 0.5f * ((float)Math.PI / 180f)) * cam.aspect) * 57.29578f;
			cone.GetComponent<Image>().fillAmount = num * 2f / 360f;
			cone.localRotation = Quaternion.Euler(0f, 0f, num);
		}
		int i = 0;
		int num2 = 0;
		FBZone.Z z = FBMatch.I?.mode?.Zone;
		zoneRing.gameObject.SetActive(z != null);
		if (z != null)
		{
			zoneRing.rectTransform.anchoredPosition = P(z.c);
			zoneRing.rectTransform.sizeDelta = Vector2.one * Mathf.Max(0.2f, z.r * 2f * sc);
		}
		List<Vector4> list = FBMatch.I?.mode?.Points;
		if (list != null)
		{
			foreach (Vector4 item in list)
			{
				Dot(P(item), (item.w < 0.5f) ? Hex(14211288u) : ((item.w < 1.5f) ? Hex(3046143u) : ((item.w < 2.5f) ? Hex(15874629u) : Hex(16099863u))), 10f, diamond: true);
			}
		}
		foreach (FBCharacter item2 in FBMatch.Actors())
		{
			if (item2.isPlayer || !item2.alive)
			{
				continue;
			}
			if (Ally(item2))
			{
				if (num2 >= miniArrows.Count)
				{
					Image image = Box(miniClip, "aliado", C, C, 0f, 0f, 10.2f, 11.9f, Hex(4037631u));
					image.sprite = ArrowSpr;
					Outline outline = image.gameObject.AddComponent<Outline>();
					outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
					outline.effectDistance = new Vector2(1.2f, -1.2f);
					miniArrows.Add(image);
				}
				Image image2 = miniArrows[num2++];
				image2.gameObject.SetActive(value: true);
				image2.rectTransform.anchoredPosition = Edge(P(item2.Pos));
				image2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, camYaw - item2.Yaw);
			}
			else if (!FBMatch.Civs.Contains(item2) && !(item2.lastShot >= 3f))
			{
				Vector2 v2 = P(item2.Pos);
				bool flag = Mathf.Abs(v2.x) > 101.25f || Mathf.Abs(v2.y) > 101.25f;
				Dot(Edge(v2, 7f), new Color(1f, 0.23f, 0.19f, 1f - item2.lastShot / 3f), flag ? 8 : 10);
			}
		}
		for (int j = i; j < miniDots.Count; j++)
		{
			miniDots[j].gameObject.SetActive(value: false);
		}
		for (int k = num2; k < miniArrows.Count; k++)
		{
			miniArrows[k].gameObject.SetActive(value: false);
		}
		Vector2 normalized = (P(me + Vector3.back * 500f) - c2).normalized;
		float lim2 = 98.75f;
		miniN.anchoredPosition = OnSquare(normalized);
		for (int l = 0; l < compass.Count; l++)
		{
			compass[l].anchoredPosition = OnSquare(Quaternion.Euler(0f, 0f, -90 * (l + 1)) * normalized);
		}
		Image Dot(Vector2 v, Color c, float size, bool diamond = false)
		{
			if (i >= miniDots.Count)
			{
				Image image3 = Box(miniClip, "p", C, C, 0f, 0f, 8f, 8f, Color.white);
				Outline outline2 = image3.gameObject.AddComponent<Outline>();
				outline2.effectColor = new Color(0f, 0f, 0f, 0.85f);
				outline2.effectDistance = new Vector2(1.2f, -1.2f);
				miniDots.Add(image3);
			}
			Image image4 = miniDots[i++];
			image4.gameObject.SetActive(value: true);
			image4.sprite = (diamond ? null : DiscSprite);
			image4.rectTransform.anchoredPosition = v;
			image4.color = c;
			image4.rectTransform.sizeDelta = Vector2.one * size * 1.25f;
			image4.rectTransform.localRotation = Quaternion.Euler(0f, 0f, diamond ? 45 : 0);
			return image4;
		}
		static Vector2 Edge(Vector2 v, float m = 9f)
		{
			float num3 = (89f - m) * 1.25f;
			return new Vector2(Mathf.Clamp(v.x, 0f - num3, num3), Mathf.Clamp(v.y, 0f - num3, num3));
		}
		Vector2 OnSquare(Vector2 d)
		{
			return d / Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y), 0.0001f) * lim2;
		}
		Vector2 P(Vector3 p)
		{
			Vector3 vector2 = (p - me) * sc;
			return c2 + (Vector2)(rot * new Vector2(vector2.x, vector2.z));
		}
	}
}
}
