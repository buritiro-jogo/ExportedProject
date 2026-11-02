using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FreeBala
{
public class FBRig : MonoBehaviour
{
	private class Finger
	{
		public Transform[] b;

		public Quaternion[] rest;

		public bool right;

		public bool index;

		public float sg = 1f;
	}

	private class Sec
	{
		public float s;

		public float cx;

		public float cy;

		public float hx;

		public float hy;
	}

	private FBCharacter ch;

	private Animator an;

	private bool built;

	private Transform[] spine;

	private Transform lArm;

	private Transform lFore;

	private Transform lHand;

	private Transform rArm;

	private Transform rFore;

	private Transform rHand;

	private Transform rMid;

	private GameObject weapon;

	private Transform grip;

	private Transform mag;

	private Transform muzzle;

	private bool pistol;

	private Quaternion lPalm = Quaternion.identity;

	private Quaternion rPalm = Quaternion.identity;

	private bool lPalmOk;

	private bool rPalmOk;

	private float armL;

	private float meta;

	private float aimW;

	private float ikW;

	private float fingW;

	private FBRemote remote;

	private float farCheck;

	private bool far;

	private readonly List<Finger> fingers = new List<Finger>();

	private static readonly float[] K = new float[3] { 0.85f, 1f, 0.8f };

	[NonSerialized]
	public bool bypass;

	[NonSerialized]
	public Vector3 LeftTarget;

	[NonSerialized]
	public Vector3 GripCenter;

	[NonSerialized]
	public Vector3 GripAxis;

	[NonSerialized]
	public float GripRadius;

	[NonSerialized]
	public bool LeftTargetValid;

	private float retreatS;

	private float lowerS;

	private float headW;

	private Transform scopeT;

	private GameObject scopeOf;

	private float armR;

	private string wbId;

	private Vector3 wbC;

	private float wbHalf;

	private float wbThick;

	private Vector3[] wbPts;


	private Renderer wbRend;

	private string wbPtsId;

	public static double ClearMs;

	public static long ClearN;

	private float centerS;

	public static int Mode = 7;

	public static int LiftN;

	public static int CenterN;

	public static float LiftSum;

	public static int DbgN;

	public static int DbgClamp;

	public static int DbgFar;

	public static float DbgRaw;

	public static float DbgWrist;

	public static float DbgClear;

	public static float DbgLeft;

	public static float DbgLower;

	public static float DbgPush;

	public static float DbgTorso;

	// contadores do teste de laboratório (FBLabFisica lê e zera; não mudam o jogo)
	[NonSerialized] public int DbgHandsN, DbgIkN, DbgTorsoN, DbgTorsoHit;
	[NonSerialized] public float DbgTorsoPen, DbgTorsoResid;

	// só para o laboratório: na 1ª passada, quanto a mão devia andar e quanto andou, para onde (frente/direita/cima do
	// personagem) e onde fica o ponto mais fundo em relação à mão; DbgPhaseKept conta as passadas mantidas por jeito
	[NonSerialized] public int DbgPushN;
	[NonSerialized] public float DbgPushReq, DbgPushGot;
	[NonSerialized] public Vector3 DbgPushDir, DbgDeepRel;
	[NonSerialized] public int[] DbgPhaseKept = new int[2];

	// giro extra da arma em volta da mão, no espaço do personagem (ver Carry); DbgCarryAng = maior giro usado
	private Quaternion carry = Quaternion.identity;
	private int carryEixo;
	private string carryId;
	private float lastPen;
	[NonSerialized] public float DbgCarryAng;
	[NonSerialized] public int DbgCarryN;

	// Carry: fundo (m, vezes ScaleK) a partir do qual a arma começa a girar, e abaixo do qual o giro volta a zero.
	// Só para as classes de CarryClasses (arma comprida que o braço não consegue tirar do peito); fuzil, escopeta e
	// submetralhadora o empurrão da mão já resolve, e girar a arma delas só entortava a pose.
	public static float CarryAlto = 0.035f;
	public static float CarryBaixo = 0.02f;
	public static string CarryClasses = "sniper;explosivo";
	private bool carryOk;
	// graus por quadro e giro máximo
	public static float CarryPasso = 2.5f;
	public static float CarryMax = 50f;

	// Correção: com a arma na mão sem mirar (parado, descansando, andando, correndo), partes da arma ficavam dentro do
	// peito ou da cabeça (o tubo do RPG). O empurrão para a frente do ClearBody tem limite de 7 cm e só olha o eixo do cano.
	// Com TorsoClear, pontos da forma da arma são conferidos contra o corpo medido da malha (FBCharacter.BodyDepth): se
	// algum está dentro do tronco ou da cabeça, a mão direita leva a arma para fora pelo lado mais perto (frente ou lado).
	public static bool TorsoClear = true;

	// maior deslocamento da mão, em metros vezes a escala do personagem (ScaleK)
	public static float TorsoMax = 0.3f;

	// distância da câmera (m) até onde os bots também tiram a arma do peito (e só se aparecem na tela); 0 = todos
	public static float TorsoDist = 30f;

	// Coronha no ombro (31/10/2026, Ruan: "o jeito que ele segura o fuzil tá estranho ainda"): o ClearBody e o
	// KeepOutOfTorso tratavam a coronha encostada no ombro como arma dentro do peito e levavam a arma para a frente;
	// o boneco ficava segurando o fuzil longe do corpo, com os braços esticados. Agora os pontos da arma que ficam
	// atrás da mão direita e a menos de OmbroLivre (m vezes ScaleK) do "bolso" do ombro direito não contam.
	// 0 = como era (o FBTesteExe compara os dois).
	public static float OmbroLivre = 0f; // v15: desligado (o teste mostrou só 1-3 cm de ganho e piorou escopeta e RPG)

	// v16, pegada do fuzil (teste "arma" da v15: a pose não mudou; 3 problemas nas fotos e nos números).
	// CabecaFuzil: sem mirar, o clipe deixa o pescoço dobrado e o boneco "olhando a arma" (cano -20°). Com peso > 0,
	// cabeça e pescoço olham para a mira/câmera com esse peso (o mesmo olhar que a pistola já usa). CabecaMira = peso
	// mirando (0 = o clipe manda; o queixo fica em cima da coronha).
	// v17: ligados no jogo (teste "arma" do exe 1033: o olhar para baixo caiu de -36° para -11° e o olhar fora do cano
	// de 13° para 4°, sem a arma entrar mais no corpo). Não vale para o RPG (a cabeça entrava no tubo: 2 -> 69 quadros).
	public static float CabecaFuzil = 0.7f;
	public static float CabecaMira = 0.25f;

	// Patrulha: sem mirar, os clipes de andar/correr levam o fuzil esticado à frente do rosto (cotovelo 161-173°, mão
	// 45-55 cm à frente do peito, coronha solta). Com peso > 0 o cano desce até PatrulhaCano graus abaixo da horizontal
	// (só se estiver acima disso) e a coronha vai para a frente do bolso do ombro direito: a mão direita gira e anda
	// por IK de dois ossos (a arma vai junto), com o cotovelo para baixo e para fora. Não vale para o RPG (vai em cima
	// do ombro). PatrulhaMax = maior deslocamento da mão (m vezes ScaleK).
	// v17: ligada só em movimento (velocidade no chão acima de PatrulhaVel; desliga abaixo de PatrulhaVelFim; o andar
	// normal do jogo é 5 m/s e o analógico pela metade dá 2,5 m/s) e fora da escopeta. No exe 1033: M4 correndo 34 de 75 quadros com a arma dentro do peito -> 0, P90 4 -> 0, cano de +3° para
	// -15°. Parado ela esticava o braço (cotovelo 94° -> 156°) e na escopeta a arma entrava no corpo (3 -> 31 quadros).
	public static float Patrulha = 1f;
	public static float PatrulhaCano = 15f;
	public static float PatrulhaMax = 0.3f;
	public static float PatrulhaVel = 1.5f;
	public static float PatrulhaVelFim = 1.0f;
	private bool patCorre;

	// Desvirar: parado e mirando, o tronco fica muito de lado em relação ao cano. Gira a coluna até Desvirar graus na
	// direção do cano e devolve a mão direita (e a arma) para onde estava, por IK; a coronha volta para o bolso com o
	// mesmo cano (DesvirarBolso = peso desse ajuste). 0 = desligado.
	public static float Desvirar = 0f;
	public static float DesvirarBolso = 1f;

	private float patW;
	private float desvW;
	private bool desvOk;
	private Vector3 desvPos;
	private Quaternion desvRot;
	[NonSerialized] public float DbgTronco, DbgPatrulhaMov;

	private bool Fuzil => !pistol && ch && ch.weapon != null && ch.weapon.cls != "explosivo";

	private bool CarregarPontos()
	{
		if (!weapon)
		{
			return false;
		}
		if (wbPtsId != ch.weapon?.id || wbPts == null)
		{
			wbPtsId = ch.weapon?.id;
			wbPts = WeaponPoints(weapon);
			wbRend = weapon.GetComponentInChildren<Renderer>();
		}
		return wbPts.Length > 0;
	}

	// ponta de trás da arma (soleira da coronha): o ponto da malha mais para trás ao longo do cano
	private Vector3 Soleira(Vector3 bar)
	{
		Matrix4x4 m = weapon.transform.localToWorldMatrix;
		Vector3 melhor = weapon.transform.position;
		float menor = float.MaxValue;
		for (int i = 0; i < wbPts.Length; i++)
		{
			Vector3 p = m.MultiplyPoint3x4(wbPts[i]);
			float d = Vector3.Dot(p, bar);
			if (d < menor)
			{
				menor = d;
				melhor = p;
			}
		}
		return melhor;
	}

	// frente do bolso do ombro direito: o mesmo bolso do Bolso()/FBTesteExe (na profundidade da junta do ombro) levado
	// 7 cm para a frente do peito, para a soleira encostar na roupa e não entrar no corpo
	private Vector3 FrenteBolso()
	{
		float k = ch.ScaleK;
		Vector3 ombro = rArm.position;
		Vector3 peito = (ch.Chest ? ch.Chest.position : ombro);
		ch.TorsoAxes(out var _, out var fw, out var _);
		if (fw.sqrMagnitude < 1E-06f)
		{
			fw = base.transform.forward;
		}
		return ombro + (peito - ombro) * 0.3f + Vector3.down * 0.04f * k + fw.normalized * 0.07f * k;
	}

	// leva a mão direita (e a arma) para que a soleira fique na frente do bolso do ombro, sem mudar o cano
	private void SoleiraNoBolso(float w)
	{
		Vector3 bar = ch.BarrelDir();
		if (w < 0.01f || bar.sqrMagnitude < 1E-06f || !CarregarPontos())
		{
			return;
		}
		float s = S;
		Vector3 delta = FrenteBolso() - Soleira(bar);
		float max = PatrulhaMax * ch.ScaleK;
		if (delta.sqrMagnitude > max * max)
		{
			delta = delta.normalized * max;
		}
		Vector3 alvo = rHand.position + delta;
		Quaternion rotation = rHand.rotation;
		Vector3 antes = rHand.position;
		FBIK.TwoBonePole(rArm, rFore, rHand, alvo, rFore.position + Vector3.down * 0.3f * s + base.transform.right * 0.2f * s, w, w);
		rHand.rotation = rotation;
		DbgPatrulhaMov = Mathf.Max(DbgPatrulhaMov, (rHand.position - antes).magnitude / Mathf.Max(0.01f, ch.ScaleK));
	}

	// peso da patrulha, atualizado em todo quadro no Hands() (mesmo quando o ramo da IK não roda): só no "pronto
	// baixo" de verdade (sem mirar, sem atirar e sem os 3 s de combate quente), com as mãos livres (ikOk); no tiro some
	// na hora, para o cano voltar para a linha do tiro
	private void PatrolPeso(float dt, bool ikOk)
	{
		float vel = ch.alive ? new Vector2(ch.velocity.x, ch.velocity.z).magnitude : 0f;
		patCorre = vel > (patCorre ? PatrulhaVelFim : PatrulhaVel);
		bool on = Patrulha > 0f && Fuzil && ch.weapon.cls != "escopeta" && patCorre && ikOk && !ch.Hot;
		if (ch.shooting)
		{
			patW = 0f;
		}
		patW = Mathf.Lerp(patW, on ? Patrulha : 0f, 1f - Mathf.Exp((0f - dt) * (on ? 6f : 14f)));
	}

	private void Patrol()
	{
		float w = patW * ikW;
		if (w < 0.01f || !rArm || !rFore || !rHand || !weapon)
		{
			return;
		}
		Vector3 bar = ch.BarrelDir();
		if (bar.sqrMagnitude < 1E-06f)
		{
			return;
		}
		// cano para baixo até -PatrulhaCano (gira em volta da mão; a soleira sobe e o passo seguinte a leva ao bolso)
		float pitch = Mathf.Asin(Mathf.Clamp(bar.y, -1f, 1f)) * 57.29578f;
		float desce = pitch + PatrulhaCano;
		Vector3 eixo = Vector3.Cross(Vector3.up, bar);
		if (desce > 0.5f && eixo.sqrMagnitude > 1E-06f)
		{
			rHand.rotation = Quaternion.AngleAxis(desce * w, eixo.normalized) * rHand.rotation;
		}
		SoleiraNoBolso(w);
	}

	// chamado no Aim(), depois do AimSpine: gira a coluna na direção do cano e guarda onde a mão direita estava
	private void DesvirarTronco(float dt)
	{
		desvOk = false;
		// pode = dá para mexer com segurança (some na hora); on = parado/mirando (entra e sai devagar)
		bool pode = Desvirar > 0f && Fuzil && built && !bypass && spine != null && spine.Length > 0 && (bool)rArm && (bool)rFore && (bool)rHand && ch.alive && !ch.Special && !ch.vehiclePose && (bool)ch.weaponGO && ch.weaponGO.activeSelf && !Far;
		bool on = pode && !ch.HandsBusy && aimW > 0.5f && !ch.sprint;
		desvW = (pode ? Mathf.Lerp(desvW, on ? 1 : 0, 1f - Mathf.Exp((0f - dt) * 8f)) : 0f);
		if (!pode || desvW < 0.01f)
		{
			return;
		}
		ch.TorsoAxes(out var _, out var fw, out var _);
		Vector3 bar = ch.BarrelDir();
		Vector3 a = Vector3.ProjectOnPlane(fw, Vector3.up);
		Vector3 b = Vector3.ProjectOnPlane(bar, Vector3.up);
		if (a.sqrMagnitude < 1E-06f || b.sqrMagnitude < 1E-06f)
		{
			return;
		}
		float ang = Vector3.SignedAngle(a, b, Vector3.up);
		DbgTronco = ang;
		float gira = Mathf.Sign(ang) * Mathf.Min(Desvirar, Mathf.Abs(ang)) * desvW;
		if (Mathf.Abs(gira) < 0.5f)
		{
			return;
		}
		desvPos = rHand.position;
		desvRot = rHand.rotation;
		bool esq = (bool)lArm && (bool)lFore && (bool)lHand;
		Vector3 lPos = (esq ? lHand.position : Vector3.zero);
		Quaternion lRot = (esq ? lHand.rotation : Quaternion.identity);
		Quaternion q = Quaternion.AngleAxis(gira / (float)spine.Length, Vector3.up);
		Transform[] array = spine;
		foreach (Transform transform in array)
		{
			transform.rotation = q * transform.rotation;
		}
		// o pescoço (e a cabeça) giraram junto: volta o giro todo nele, para a cabeça continuar olhando pela arma
		Transform nk = (ch.Neck ? ch.Neck : ch.Head);
		if ((bool)nk)
		{
			nk.rotation = Quaternion.AngleAxis(0f - gira, Vector3.up) * nk.rotation;
		}
		// e a mão direita volta para onde estava (a arma é filha dela), no mesmo passo: lean, coice e o resto entram
		// por cima depois, pela hierarquia normal. Polo = cotovelo atual (mantém o plano do braço do clipe).
		FBIK.TwoBonePole(rArm, rFore, rHand, desvPos, rFore.position, 1f);
		rHand.rotation = desvRot;
		// a mão esquerda também volta (na mira o RifleLeftHand a recoloca na arma; no reload ela fica no clipe)
		if (esq)
		{
			FBIK.TwoBonePole(lArm, lFore, lHand, lPos, lFore.position, 1f);
			lHand.rotation = lRot;
		}
		// bolso pesado pelo ikW (do quadro anterior): some e volta junto com a IK das mãos, sem salto
		SoleiraNoBolso(DesvirarBolso * desvW * ikW);
		desvOk = true;
	}

	// bolso do ombro: um pouco para dentro (rumo ao peito) e para baixo da junta do ombro direito
	private bool Bolso(out Vector3 bolso, out Vector3 mao, out Vector3 bar, out float r2)
	{
		bolso = mao = bar = Vector3.zero;
		r2 = 0f;
		if (OmbroLivre <= 0f || !rArm || !rHand || !ch)
		{
			return false;
		}
		float k = ch.ScaleK;
		Vector3 ombro = rArm.position;
		Vector3 peito = (ch.Chest ? ch.Chest.position : ombro);
		bolso = ombro + (peito - ombro) * 0.3f + Vector3.down * 0.04f * k;
		mao = rHand.position;
		bar = ch.BarrelDir();
		r2 = OmbroLivre * k * OmbroLivre * k;
		return bar.sqrMagnitude > 1E-06f;
	}

	private static bool NoBolso(Vector3 p, Vector3 bolso, Vector3 mao, Vector3 bar, float r2)
	{
		return Vector3.Dot(p - mao, bar) <= 0f && (p - bolso).sqrMagnitude < r2;
	}

	public static double TorsoMs;

	public static long TorsoCalls;

	private static readonly Dictionary<string, Sec> sections = new Dictionary<string, Sec>();

	private static readonly Regex SKIP = new Regex("magazine|^mag$|pmag|balas|drum|_mag|Mag_|P_LPSP_WEP_ATT|SM_ATT_|Scope|Laser|Flashlight|Silencer|Grip_|Bipod|holosun|calimator|Render_Camera|lens|glass", RegexOptions.IgnoreCase);

	public bool Far
	{
		get
		{
			if (ch.isPlayer)
			{
				return false;
			}
			if ((farCheck -= Time.deltaTime) <= 0f)
			{
				farCheck = 0.25f + UnityEngine.Random.value * 0.1f;
				Camera main = Camera.main;
				far = (bool)main && (main.transform.position - base.transform.position).sqrMagnitude > FBQuality.AnimCullDistance * FBQuality.AnimCullDistance;
			}
			return far;
		}
	}

	public float AimW => aimW;

	public float IkW => ikW;

	private bool Hd
	{
		get
		{
			if (!ch.isPlayer)
			{
				return !FBQuality.HdOnlyLocal;
			}
			return true;
		}
	}

	private float S => ch.ScaleK * 2.2f / 1.25f;

	public float LowerDeg => lowerS;

	public bool PistolLowReady { get; private set; }

	public float CenterShift { get; private set; }

	public bool HasSection
	{
		get
		{
			if ((bool)ch && ch.weapon != null && sections.TryGetValue(ch.weapon.id, out var value))
			{
				return value != null;
			}
			return false;
		}
	}

	public void Init(FBCharacter c)
	{
		ch = c;
		an = c.an;
		if (built || !an || !an.isHuman)
		{
			return;
		}
		remote = GetComponent<FBRemote>();
		List<Transform> list = new List<Transform>();
		HumanBodyBones[] array = new HumanBodyBones[3]
		{
			HumanBodyBones.Spine,
			HumanBodyBones.Chest,
			HumanBodyBones.UpperChest
		};
		foreach (HumanBodyBones humanBoneId in array)
		{
			Transform boneTransform = an.GetBoneTransform(humanBoneId);
			if ((bool)boneTransform)
			{
				list.Add(boneTransform);
			}
		}
		spine = list.ToArray();
		lArm = B(HumanBodyBones.LeftUpperArm);
		lFore = B(HumanBodyBones.LeftLowerArm);
		lHand = B(HumanBodyBones.LeftHand);
		rArm = B(HumanBodyBones.RightUpperArm);
		rFore = B(HumanBodyBones.RightLowerArm);
		rHand = B(HumanBodyBones.RightHand);
		rMid = B(HumanBodyBones.RightMiddleProximal);
		lPalmOk = FBHand.Frame(an, right: false, out lPalm, out var palm);
		rPalmOk = FBHand.Frame(an, right: true, out rPalm, out palm);
		bool[] array2 = new bool[2] { true, false };
		foreach (bool flag in array2)
		{
			if (!(flag ? rPalmOk : lPalmOk))
			{
				continue;
			}
			Vector3 rhs = (flag ? rHand : lHand).rotation * ((flag ? rPalm : lPalm) * Vector3.up);
			Vector3 a = FingerAxis(flag);
			if (a.sqrMagnitude < 1E-08f)
			{
				continue;
			}
			for (int j = 1; j < 5; j++)
			{
				Transform[] array3 = new Transform[3];
				bool flag2 = true;
				for (int k = 0; k < 3; k++)
				{
					array3[k] = B((HumanBodyBones)((flag ? 39 : 24) + 3 * j + k));
					flag2 &= (bool)array3[k];
				}
				if (flag2)
				{
					Finger finger = new Finger
					{
						b = array3,
						rest = new Quaternion[3],
						right = flag,
						index = (j == 1)
					};
					for (int l = 0; l < 3; l++)
					{
						finger.rest[l] = array3[l].localRotation;
					}
					Vector3 vector = Tip(finger);
					Bend(finger, 0.3f, a);
					Vector3 vector2 = Tip(finger);
					finger.sg = ((Vector3.Dot(vector2 - vector, rhs) > 0f) ? 1 : (-1));
					for (int m = 0; m < 3; m++)
					{
						array3[m].localRotation = finger.rest[m];
					}
					fingers.Add(finger);
				}
			}
		}
		built = true;
		Transform B(HumanBodyBones b)
		{
			return an.GetBoneTransform(b);
		}
	}

	private static Vector3 Tip(Finger f)
	{
		Transform transform = f.b[2];
		return transform.position + (transform.position - f.b[1].position) * 0.8f;
	}

	private Vector3 FingerAxis(bool right)
	{
		Transform boneTransform = an.GetBoneTransform(right ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
		Transform transform = an.GetBoneTransform(right ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal) ?? an.GetBoneTransform(right ? HumanBodyBones.RightRingProximal : HumanBodyBones.LeftRingProximal);
		if (!boneTransform || !transform)
		{
			return Vector3.zero;
		}
		return (boneTransform.position - transform.position).normalized;
	}

	private static void Bend(Finger f, float th, Vector3 A)
	{
		for (int i = 0; i < 3; i++)
		{
			Transform transform = f.b[i];
			transform.localRotation = f.rest[i];
			transform.rotation = Quaternion.AngleAxis(th * K[i] * f.sg * 57.29578f, A) * transform.rotation;
		}
	}

	private void Curl(bool right, float th, float thIndex, float w)
	{
		Vector3 a = FingerAxis(right);
		if (a.sqrMagnitude < 1E-08f)
		{
			return;
		}
		foreach (Finger finger in fingers)
		{
			if (finger.right != right)
			{
				continue;
			}
			float num = (finger.index ? thIndex : th);
			if (!(num < 0f))
			{
				Quaternion localRotation = finger.b[0].localRotation;
				Quaternion localRotation2 = finger.b[1].localRotation;
				Quaternion localRotation3 = finger.b[2].localRotation;
				Bend(finger, num, a);
				if (w < 0.999f)
				{
					finger.b[0].localRotation = Quaternion.Slerp(localRotation, finger.b[0].localRotation, w);
					finger.b[1].localRotation = Quaternion.Slerp(localRotation2, finger.b[1].localRotation, w);
					finger.b[2].localRotation = Quaternion.Slerp(localRotation3, finger.b[2].localRotation, w);
				}
			}
		}
	}

	public void OnWeapon(GameObject w, Transform g, Transform m, Transform mz, bool isPistol)
	{
		if (w != weapon || isPistol != pistol)
		{
			ikW = 0f;
			retreatS = 0f;
			patW = 0f;
			headW = 0f;
		}
		weapon = w;
		grip = g;
		mag = m;
		muzzle = mz;
		pistol = isPistol;
	}

	public void Aim(float dt)
	{
		AimSpine(dt);
		DesvirarTronco(dt);
		HeadLook(dt);
	}

	private void AimSpine(float dt)
	{
		if (!built || !ch || spine == null || spine.Length == 0 || bypass)
		{
			return;
		}
		bool flag = ch.Special || !ch.alive || ch.vehiclePose;
		float num = ((!flag && !ch.sprint && ch.grounded && !ch.IsRelaxedPose && ch.CombatStance) ? 1 : 0);
		aimW = Mathf.Lerp(aimW, num * (ch.HandsBusy ? 0.3f : 1f), 1f - Mathf.Exp((0f - dt) * 8f));
		float num2 = ((!(ch.Hot || !ch.CombatStance || flag)) ? (ch.Resting ? 36 : 20) : 0);
		lowerS = Mathf.Lerp(lowerS, num2, 1f - Mathf.Exp((0f - dt) * (float)((num2 < lowerS) ? 16 : 4)));
		if (aimW < 0.01f || !ch.weaponGO || !ch.weaponGO.activeSelf || Far)
		{
			return;
		}
		Vector3 axis = ((ch.isPlayer && (bool)FBPlayer.I) ? (Quaternion.Euler(0f, FBPlayer.I.camYaw, 0f) * Vector3.right) : base.transform.right);
		float num3 = 0f;
		int num4 = spine.Length;
		for (int i = 0; i < ((!ch.isPlayer) ? 1 : 3); i++)
		{
			if (!(pistol ? TorsoErr(out var dy, out var dp) : AimErr(out dy, out dp)))
			{
				break;
			}
			dy = Mathf.Clamp(dy, -0.87f, 0.87f) * aimW;
			float num5 = Mathf.Clamp(Mathf.Clamp(dp, -0.7f, 0.7f) * aimW, -0.38f - num3, 0.38f - num3);
			num3 += num5;
			Quaternion quaternion = Quaternion.AngleAxis(dy / (float)num4 * 57.29578f, Vector3.up) * Quaternion.AngleAxis((0f - num5) / (float)num4 * 57.29578f, axis);
			Transform[] array = spine;
			foreach (Transform transform in array)
			{
				transform.rotation = quaternion * transform.rotation;
			}
		}
		if (!pistol && AimErr(out var _, out var dp2) && (bool)rArm && (bool)lArm)
		{
			float num6 = Mathf.Clamp(dp2, -0.8f, 0.8f) * aimW;
			if (Mathf.Abs(num6) > 0.01f)
			{
				Quaternion quaternion2 = Quaternion.AngleAxis((0f - num6) * 57.29578f, axis);
				rArm.rotation = quaternion2 * rArm.rotation;
				lArm.rotation = quaternion2 * lArm.rotation;
			}
			if (lowerS > 0.2f)
			{
				Quaternion quaternion3 = Quaternion.AngleAxis(lowerS * aimW, axis);
				rArm.rotation = quaternion3 * rArm.rotation;
				lArm.rotation = quaternion3 * lArm.rotation;
			}
		}
	}

	private void HeadLook(float dt)
	{
		Transform transform = (ch ? ch.Head : null);
		Vector3 fwL = default(Vector3);
		Vector3 upL = default(Vector3);
		// v16: com fuzil também olha (peso CabecaFuzil sem mirar, CabecaMira mirando); pistola continua com peso 1
		// "quer": com CabecaFuzil e CabecaMira em 0, fuzil não olha nada (igual à v15)
		// v17: só arma de fuzil (Fuzil = não pistola e não explosivo); no RPG a cabeça girava para dentro do tubo
		bool quer = pistol || ((CabecaFuzil > 0f || CabecaMira > 0f) && Fuzil);
		bool baseOk = built && !bypass && quer && (bool)transform && (bool)ch.weaponGO && ch.weaponGO.activeSelf && ch.alive && !ch.Special && !ch.vehiclePose && !Far;
		float alvoW = (baseOk ? (pistol ? 1f : (Fuzil ? (ch.aiming ? CabecaMira : CabecaFuzil) : 0f)) : 0f);
		bool flag = baseOk && (alvoW > 0.001f || headW >= 0.01f) && ch.HeadAxes(out fwL, out upL);
		headW = Mathf.Lerp(headW, flag ? alvoW : 0f, 1f - Mathf.Exp((0f - dt) * 8f));
		if (flag && !(headW < 0.01f))
		{
			Vector3 position = transform.position;
			Quaternion b = Quaternion.FromToRotation(toDirection: (ch.hasAim && (ch.aimPoint - position).sqrMagnitude > 1f) ? (ch.aimPoint - position).normalized : ((!remote && !(remote = GetComponent<FBRemote>())) ? base.transform.forward : (Quaternion.Euler(0f - remote.aimPitch, remote.aimYaw, 0f) * Vector3.forward)), fromDirection: transform.TransformDirection(fwL));
			float num = Quaternion.Angle(Quaternion.identity, b);
			if (num > 85f)
			{
				b = Quaternion.Slerp(Quaternion.identity, b, 85f / num);
			}
			Quaternion quaternion = Quaternion.Slerp(Quaternion.identity, b, 0.5f * headW);
			Transform neck = ch.Neck;
			if ((bool)neck)
			{
				neck.rotation = quaternion * neck.rotation;
				transform.rotation = quaternion * transform.rotation;
			}
			else
			{
				transform.rotation = Quaternion.Slerp(Quaternion.identity, b, headW) * transform.rotation;
			}
			Vector3 vector = transform.TransformDirection(fwL);
			Vector3 to = Vector3.ProjectOnPlane(Vector3.up, vector);
			Vector3 from = Vector3.ProjectOnPlane(transform.TransformDirection(upL), vector);
			if (to.sqrMagnitude > 0.0001f && from.sqrMagnitude > 0.0001f)
			{
				transform.rotation = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(from, to, vector), -30f, 30f) * headW, vector) * transform.rotation;
			}
		}
	}

	private void AimWrist()
	{
		if (aimW < 0.01f || !rHand || !ch.weaponGO || !ch.weaponGO.activeSelf)
		{
			return;
		}
		Vector3 vector = ch.BarrelDir();
		Vector3 muzzlePos = ch.MuzzlePos;
		Vector3 vector2 = ((ch.hasAim && (ch.aimPoint - muzzlePos).sqrMagnitude > 1f) ? (ch.aimPoint - muzzlePos).normalized : ((!remote) ? base.transform.forward : (Quaternion.Euler(0f - remote.aimPitch, remote.aimYaw, 0f) * Vector3.forward)));
		if (lowerS > 0.2f)
		{
			Vector3 vector3 = Vector3.Cross(Vector3.up, vector2);
			if (vector3.sqrMagnitude > 0.0001f)
			{
				vector2 = Quaternion.AngleAxis(lowerS * aimW, vector3.normalized) * vector2;
			}
		}
		Quaternion quaternion = Quaternion.FromToRotation(vector, vector2);
		float num = Quaternion.Angle(Quaternion.identity, quaternion);
		if (num > 60f)
		{
			quaternion = Quaternion.Slerp(Quaternion.identity, quaternion, 60f / num);
			DbgClamp++;
		}
		Vector3 vector4 = quaternion * vector;
		Vector3 from = Vector3.ProjectOnPlane(quaternion * TopDir(), vector4);
		Vector3 to = Vector3.ProjectOnPlane(Vector3.up, vector4);
		Quaternion quaternion2 = Quaternion.identity;
		if (from.sqrMagnitude > 1E-06f && to.sqrMagnitude > 1E-06f)
		{
			quaternion2 = Quaternion.AngleAxis(Mathf.Clamp(Vector3.SignedAngle(from, to, vector4), -60f, 60f), vector4);
		}
		rHand.rotation = Quaternion.Slerp(Quaternion.identity, quaternion2 * quaternion, aimW) * rHand.rotation;
	}

	private Vector3 TopDir()
	{
		if (!scopeT || scopeOf != weapon)
		{
			scopeOf = weapon;
			scopeT = null;
			if ((bool)weapon)
			{
				Transform[] componentsInChildren = weapon.GetComponentsInChildren<Transform>(includeInactive: true);
				foreach (Transform transform in componentsInChildren)
				{
					if (transform.name == "SOCKET_Scope")
					{
						scopeT = transform;
						break;
					}
				}
			}
		}
		if ((bool)scopeT)
		{
			return scopeT.position - ch.MuzzlePos;
		}
		if (!FBCharacter.GunAxes(weapon, out var space, out var _, out var top))
		{
			return Node0(weapon).up;
		}
		return space.TransformDirection(top);
	}

	private bool TorsoErr(out float dy, out float dp)
	{
		dy = (dp = 0f);
		ch.TorsoAxes(out var _, out var fw, out var _);
		if (fw.sqrMagnitude < 1E-06f)
		{
			return false;
		}
		float num = Mathf.Atan2(base.transform.forward.x, base.transform.forward.z);
		float num2 = 0f;
		Vector3 vector = (ch.Chest ? ch.Chest.position : base.transform.position);
		if (ch.hasAim)
		{
			Vector3 vector2 = ch.aimPoint - vector;
			if (vector2.sqrMagnitude > 1f)
			{
				num = Mathf.Atan2(vector2.x, vector2.z);
				num2 = Mathf.Atan2(vector2.y, new Vector2(vector2.x, vector2.z).magnitude);
			}
		}
		else if ((bool)remote || (bool)(remote = GetComponent<FBRemote>()))
		{
			num = remote.aimYaw * ((float)Math.PI / 180f);
			num2 = remote.aimPitch * ((float)Math.PI / 180f);
		}
		dy = Mathf.DeltaAngle(Mathf.Atan2(fw.x, fw.z) * 57.29578f, num * 57.29578f) * ((float)Math.PI / 180f);
		dp = num2 - Mathf.Atan2(fw.y, new Vector2(fw.x, fw.z).magnitude);
		return true;
	}

	private bool AimErr(out float dy, out float dp)
	{
		dy = (dp = 0f);
		Vector3 vector = ch.BarrelDir();
		if (vector.sqrMagnitude < 1E-06f)
		{
			return false;
		}
		float num = Mathf.Atan2(base.transform.forward.x, base.transform.forward.z);
		float num2 = 0f;
		if (ch.hasAim)
		{
			Vector3 vector2 = ch.aimPoint - ch.MuzzlePos;
			if (vector2.sqrMagnitude > 1f)
			{
				num = Mathf.Atan2(vector2.x, vector2.z);
				num2 = Mathf.Atan2(vector2.y, new Vector2(vector2.x, vector2.z).magnitude);
			}
		}
		else if ((bool)remote || (bool)(remote = GetComponent<FBRemote>()))
		{
			num = remote.aimYaw * ((float)Math.PI / 180f);
			num2 = remote.aimPitch * ((float)Math.PI / 180f);
		}
		dy = Mathf.DeltaAngle(Mathf.Atan2(vector.x, vector.z) * 57.29578f, num * 57.29578f) * ((float)Math.PI / 180f);
		dp = num2 - Mathf.Atan2(vector.y, new Vector2(vector.x, vector.z).magnitude);
		return true;
	}

	public void Hands(float dt)
	{
		LeftTargetValid = false;
		if (bypass || !built || !ch || !lArm || !lFore || !lHand || !rHand)
		{
			return;
		}
		if (Far)
		{
			DbgFar++;
			return;
		}
		bool flag = ch.Special || !ch.alive || ch.vehiclePose;
		bool handsBusy = ch.HandsBusy;
		bool flag2 = (bool)weapon && weapon.activeSelf;
		bool flag3 = ch.alive && !flag && !handsBusy && !ch.UnarmedLocomotion && flag2 && (!pistol || !ch.sprint);
		ikW = Mathf.Lerp(ikW, flag3 ? 1 : 0, 1f - Mathf.Exp((0f - dt) * 12f));
		DbgHandsN++;
		if (ikW > 0.02f && flag2)
		{
			DbgIkN++;
		}
		fingW = Mathf.Lerp(fingW, (flag2 && ch.alive && !flag && !handsBusy) ? 1 : 0, 1f - Mathf.Exp((0f - dt) * 12f));
		if (armL <= 0f)
		{
			armL = (lFore.position - lArm.position).magnitude + (lHand.position - lFore.position).magnitude;
			Transform boneTransform = an.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
			meta = (boneTransform ? (boneTransform.position - lHand.position).magnitude : (0.09f * S));
		}
		// v16: peso da patrulha em todo quadro (some junto com a IK no reload, saque, troca e reação a tiro)
		PatrolPeso(dt, flag3);
		if (ikW > 0.02f && flag2)
		{
			if (pistol)
			{
				PistolHands(ikW);
			}
			else if (ch.hasAim && aimW > 0.9f)
			{
				DbgN++;
				DbgRaw += ErrNow();
				DbgLower += lowerS;
				AimWrist();
				DbgWrist += ErrNow();
				Patrol();
				ClearBody(ikW);
				DbgClear += ErrNow();
				RifleLeftHand(ikW);
				DbgLeft += ErrNow();
				if (TorsoClear && KeepOutOfTorso(ikW))
				{
					RifleLeftHand(ikW, follow: true);
				}
			}
			else
			{
				AimWrist();
				Patrol();
				ClearBody(ikW);
				RifleLeftHand(ikW);
				if (TorsoClear && KeepOutOfTorso(ikW))
				{
					RifleLeftHand(ikW, follow: true);
				}
			}
		}
		if (fingW > 0.02f && flag2 && Hd)
		{
			Curl(right: true, 1f, 0.45f, fingW);
		}
	}

	private void PistolHands(float w)
	{
		float num = (ch.CombatStance ? (lowerS * aimW) : 40f);
		PistolLowReady = num > 8f;
		if (!rArm || !rFore || !lArm)
		{
			return;
		}
		if (armR <= 0f)
		{
			armR = (rFore.position - rArm.position).magnitude + (rHand.position - rFore.position).magnitude;
		}
		Vector3 vector = (rArm.position + lArm.position) * 0.5f;
		Vector3 vector2 = ((ch.hasAim && (ch.aimPoint - ch.MuzzlePos).sqrMagnitude > 1f) ? (ch.aimPoint - vector).normalized : ((!remote) ? base.transform.forward : (Quaternion.Euler(0f - remote.aimPitch, remote.aimYaw, 0f) * Vector3.forward)));
		Vector3 vector3 = Vector3.ProjectOnPlane(vector2, Vector3.up);
		if (vector3.sqrMagnitude < 0.0001f)
		{
			vector3 = base.transform.forward;
		}
		vector3.Normalize();
		Vector3 normalized = Vector3.Cross(Vector3.up, vector3).normalized;
		if (num > 0.2f)
		{
			vector2 = Quaternion.AngleAxis(num, normalized) * vector2;
		}
		Quaternion quaternion = Quaternion.LookRotation(vector2, (Vector3.ProjectOnPlane(Vector3.up, vector2).sqrMagnitude > 0.0001f) ? Vector3.up : (-vector3));
		Vector3 vector4 = quaternion * Vector3.forward;
		Vector3 vector5 = quaternion * Vector3.up;
		Vector3 vector6 = quaternion * Vector3.right;
		float t = Mathf.Clamp01(1f - num / 40f);
		float num2 = Mathf.Lerp(0.6f, 0.88f, t) * armR;
		float num3 = Mathf.Lerp(0f, 0.12f, t) * armR;
		float num4 = Mathf.Lerp(0.02f, 0.04f, t) * armR;
		Vector3 vector7 = vector + vector4 * num2 + vector5 * num3 + vector6 * num4;
		Vector3 pole = (rArm.position + vector7) * 0.5f + (vector6 * 0.55f - vector5 * 0.45f).normalized * 0.3f * armR;
		FBIK.TwoBonePole(rArm, rFore, rHand, vector7, pole, w);
		PistolWrist(vector4, w);
		AimWrist();
		Vector3 vector8 = rHand.position - vector6 * 0.07f * armR - vector5 * 0.04f * armR - vector4 * 0.02f * armR;
		Vector3 pole2 = (lArm.position + vector8) * 0.5f + (-vector6 * 0.55f - vector5 * 0.45f).normalized * 0.3f * armR;
		FBIK.TwoBonePole(lArm, lFore, lHand, vector8, pole2, w);
		LeftTarget = vector8;
		LeftTargetValid = true;
		if (lPalmOk)
		{
			Vector3 vector9 = ch.BarrelDir();
			Vector3 vector10 = Vector3.Cross(Vector3.up, vector9);
			if (vector10.sqrMagnitude < 0.0001f)
			{
				vector10 = normalized;
			}
			vector10.Normalize();
			Vector3 normalized2 = (vector10 * 0.85f + Vector3.up * 0.25f - vector9 * 0.1f).normalized;
			Vector3 normalized3 = Vector3.ProjectOnPlane(vector9 * 0.45f - Vector3.up * 0.85f + vector10 * 0.25f, normalized2).normalized;
			if (normalized3.sqrMagnitude > 0.5f)
			{
				lHand.rotation = Quaternion.Slerp(lHand.rotation, Quaternion.LookRotation(normalized3, normalized2) * Quaternion.Inverse(lPalm), w);
			}
			if (Hd)
			{
				Curl(right: false, 0.95f, 0.9f, w);
			}
		}
	}

	private void PistolWrist(Vector3 want, float w)
	{
		if ((bool)rHand && (bool)ch.weaponGO && !(w < 0.01f))
		{
			Vector3 vector = ch.BarrelDir();
			Quaternion quaternion = Quaternion.FromToRotation(vector, want);
			Vector3 vector2 = quaternion * vector;
			Vector3 from = Vector3.ProjectOnPlane(quaternion * TopDir(), vector2);
			Vector3 to = Vector3.ProjectOnPlane(Vector3.up, vector2);
			Quaternion quaternion2 = Quaternion.identity;
			if (from.sqrMagnitude > 1E-06f && to.sqrMagnitude > 1E-06f)
			{
				quaternion2 = Quaternion.AngleAxis(Vector3.SignedAngle(from, to, vector2), vector2);
			}
			rHand.rotation = Quaternion.Slerp(Quaternion.identity, quaternion2 * quaternion, w) * rHand.rotation;
		}
	}

	private void ClearBody(float w)
	{
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			ClearBody0(w);
		}
		finally
		{
			ClearMs += stopwatch.Elapsed.TotalMilliseconds;
			ClearN++;
		}
	}

	private void ClearBody0(float w)
	{
		if (!rArm || !rFore || !rHand || !weapon)
		{
			return;
		}
		float s = S;
		if (wbId != ch.weapon?.id)
		{
			wbId = ch.weapon?.id;
			Matrix4x4 worldToLocalMatrix = weapon.transform.worldToLocalMatrix;
			Bounds bounds = default(Bounds);
			bool flag = true;
			Renderer[] componentsInChildren = weapon.GetComponentsInChildren<Renderer>();
			foreach (Renderer renderer in componentsInChildren)
			{
				// Correção: caixa da arma calculada pelos 8 cantos no espaço da arma (antes a medida podia sair
				// negativa e desligava o afastamento da arma do corpo em algumas armas).
				if (!Regex.IsMatch(renderer.name, "laser|beam|flash", RegexOptions.IgnoreCase) && FBCharacter.LocalBoundsOf(renderer, worldToLocalMatrix, out var bounds2))
				{
					if (flag)
					{
						bounds = bounds2;
						flag = false;
					}
					else
					{
						bounds.Encapsulate(bounds2);
					}
				}
			}
			wbC = bounds.center;
			Vector3 normalized = weapon.transform.InverseTransformDirection(ch.BarrelDir()).normalized;
			wbHalf = Mathf.Abs(bounds.extents.x * normalized.x) + Mathf.Abs(bounds.extents.y * normalized.y) + Mathf.Abs(bounds.extents.z * normalized.z);
			wbThick = Mathf.Min(bounds.extents.x, Mathf.Min(bounds.extents.y, bounds.extents.z));
		}
		Vector3 vector = ch.BarrelDir();
		if (!(vector.sqrMagnitude < 1E-06f))
		{
			float x = weapon.transform.lossyScale.x;
			Vector3 vector2 = weapon.transform.TransformPoint(wbC);
			float num = wbHalf * x;
			float pad = wbThick * x + 0.015f * s;
			float a = 0f;
			bool ombroOk = Bolso(out var bolsoC, out var maoC, out var barC, out var r2C);
			bool pegOk = (patW > 0.01f || desvW > 0.01f) && (bool)rArm;
			Vector3 fbC = (pegOk ? FrenteBolso() : Vector3.zero);
			float rk = 0.1f * ch.ScaleK;
			for (int j = -8; j <= 8; j++)
			{
				Vector3 pc = vector2 + vector * (num * (float)j / 8f);
				if (ombroOk && NoBolso(pc, bolsoC, maoC, barC, r2C))
				{
					continue;
				}
				if (pegOk && NoBolso(pc, fbC, rHand.position, vector, rk * rk))
				{
					continue;
				}
				a = Mathf.Max(a, ch.FrontPenetration(pc, pad));
			}
			a = Mathf.Min(a, 0.07f * s);
			DbgPush += a / s;
			if (!(a <= 0.002f * s))
			{
				Vector3 normalized2 = Vector3.ProjectOnPlane(base.transform.forward, Vector3.up).normalized;
				Quaternion rotation = rHand.rotation;
				FBIK.TwoBonePole(rArm, rFore, rHand, rHand.position + normalized2 * a, rFore.position + Vector3.down * 0.3f * s + base.transform.right * 0.2f * s, w);
				rHand.rotation = rotation;
			}
		}
	}

	// depois da mão esquerda: se algum ponto da arma está dentro do tronco ou da cabeça, leva a mão direita (e a arma)
	// para fora pelo lado mais perto. Devolve true se mexeu (aí a mão esquerda é recolocada na arma).
	private bool KeepOutOfTorso(float w)
	{
		if (!rArm || !rFore || !rHand || !weapon)
		{
			return false;
		}
		// longe da câmera ninguém vê a arma encostando no peito: só o jogador e quem está a menos de TorsoDist
		if (!ch.isPlayer && TorsoDist > 0f)
		{
			Camera main = Camera.main;
			if ((bool)main && (main.transform.position - rHand.position).sqrMagnitude > TorsoDist * TorsoDist)
			{
				return false;
			}
		}
		if (wbPtsId != ch.weapon?.id || wbPts == null)
		{
			wbPtsId = ch.weapon?.id;
			wbPts = WeaponPoints(weapon);
			wbRend = weapon.GetComponentInChildren<Renderer>();
		}
		// bot fora da tela também não precisa
		if (!ch.isPlayer && TorsoDist > 0f && (bool)wbRend && !wbRend.isVisible)
		{
			return false;
		}
		Stopwatch stopwatch = Stopwatch.StartNew();
		try
		{
			return KeepOutOfTorso0(w);
		}
		finally
		{
			TorsoMs += stopwatch.Elapsed.TotalMilliseconds;
			TorsoCalls++;
		}
	}

	// Arma comprida sem mirar (Barrett, RPG correndo): a coronha fica atrás da mão, dentro do peito, e o braço não
	// alcança levar a mão para a frente o bastante. Então a arma gira devagar em volta da mão (alguns graus por quadro,
	// num eixo do personagem por vez: em pé, lado, frente), ficando com o giro que diminui o fundo, até ele cair abaixo
	// de CarryAlto; o empurrão da mão resolve o resto. Abaixo de CarryBaixo o giro volta aos poucos para zero, e
	// mirando ele some rápido para não tirar a mira do lugar. Devolve true se a mão girou.
	private bool Carry(bool mira, float k)
	{
		if (carryId != wbPtsId)
		{
			carryId = wbPtsId;
			carry = Quaternion.identity;
			string cls = ch.weapon?.cls;
			carryOk = !string.IsNullOrEmpty(cls) && (";" + CarryClasses + ";").Contains(";" + cls + ";");
		}
		if (!carryOk)
		{
			return false;
		}
		bool zero = carry == Quaternion.identity;
		if (zero && (mira || lastPen < CarryAlto))
		{
			return false;
		}
		if (mira)
		{
			carry = Quaternion.RotateTowards(carry, Quaternion.identity, 400f * Time.deltaTime);
		}
		Quaternion rc = base.transform.rotation;
		Quaternion rc1 = Quaternion.Inverse(rc);
		Quaternion semGiro = rHand.rotation;
		rHand.rotation = rc * carry * rc1 * semGiro;
		if (mira)
		{
			return carry != Quaternion.identity;
		}
		float d0 = Fundo();
		Quaternion novo = carry;
		bool achou = false;
		if (d0 > CarryAlto * k)
		{
			Vector3 eixo = carryEixo == 0 ? Vector3.up : (carryEixo == 1 ? Vector3.right : Vector3.forward);
			for (int sinal = 1; sinal >= -1 && !achou; sinal -= 2)
			{
				Quaternion q = Quaternion.AngleAxis(sinal * CarryPasso, eixo) * carry;
				if (Quaternion.Angle(q, Quaternion.identity) > CarryMax)
				{
					continue;
				}
				rHand.rotation = rc * q * rc1 * semGiro;
				if (Fundo() < d0 - 0.003f * k)
				{
					novo = q;
					achou = true;
				}
			}
			if (!achou)
			{
				carryEixo = (carryEixo + 1) % 3;
			}
		}
		else if (d0 < CarryBaixo * k && !zero)
		{
			Quaternion q = Quaternion.RotateTowards(carry, Quaternion.identity, CarryPasso);
			rHand.rotation = rc * q * rc1 * semGiro;
			if (Fundo() <= CarryBaixo * k)
			{
				novo = q;
				achou = true;
			}
		}
		if (achou)
		{
			carry = novo;
		}
		rHand.rotation = rc * carry * rc1 * semGiro;
		float ang = Quaternion.Angle(carry, Quaternion.identity);
		DbgCarryAng = Mathf.Max(DbgCarryAng, ang);
		if (ang > 0.5f)
		{
			DbgCarryN++;
		}
		return ang > 0.01f;
	}

	// maior fundo de um ponto da arma dentro do tronco ou da cabeça, do jeito que a arma está agora
	private float Fundo()
	{
		Transform hips = ch.Hips;
		Transform neck = ch.Neck;
		bool flag = (bool)hips && (bool)neck;
		float k = ch.ScaleK;
		float r2 = 0.45f * k * 0.45f * k;
		Vector3 a = flag ? hips.position : Vector3.zero;
		Vector3 b = flag ? (neck.position - a) : Vector3.zero;
		float bb = Mathf.Max(b.sqrMagnitude, 1E-06f);
		Matrix4x4 m = weapon.transform.localToWorldMatrix;
		float best = 0f;
		bool ombroOk = Bolso(out var bolsoF, out var maoF, out var barF, out var r2F);
		for (int j = 0; j < wbPts.Length; j++)
		{
			Vector3 p = m.MultiplyPoint3x4(wbPts[j]);
			if (flag && (p - (a + b * Mathf.Clamp(Vector3.Dot(p - a, b) / bb, 0f, 1.35f))).sqrMagnitude > r2)
			{
				continue;
			}
			if (ombroOk && NoBolso(p, bolsoF, maoF, barF, r2F))
			{
				continue;
			}
			float d = ch.BodyDepthFast(p, out var _);
			if (d > best)
			{
				best = d;
			}
		}
		return best;
	}

	private bool KeepOutOfTorso0(float w)
	{
		if (wbPts.Length == 0 || !ch.BodyFastBegin())
		{
			return false;
		}
		float s = S;
		float k = ch.ScaleK;
		Transform t = weapon.transform;
		Transform hips = ch.Hips;
		Transform neck = ch.Neck;
		bool flag = (bool)hips && (bool)neck;
		float num4 = 0.45f * k * 0.45f * k;
		bool result = false;
		DbgTorsoN++;
		if (Carry(ch.hasAim && aimW > 0.5f, k))
		{
			result = true;
		}
		// cada passada mede todos os pontos e mexe a arma; a passada seguinte confere: se não melhorou, desfaz.
		// Primeiro leva a mão (e a arma) pela direção média dos pontos que entraram (pesada pela profundidade);
		// se o braço não alcança, gira a arma em volta da mão (só sem mirar, para não tirar a mira do lugar).
		int num7 = 0;
		bool flag3 = ch.hasAim && aimW > 0.5f;
		bool flag2 = false;
		float num6 = 0f;
		Vector3 vector7 = Vector3.zero;
		Vector3 vector8 = Vector3.zero;
		Vector3 vector9 = Vector3.zero;
		Quaternion rotation = Quaternion.identity;
		Quaternion rotation2 = Quaternion.identity;
		Quaternion rotation3 = Quaternion.identity;
		for (int i = 0; i < 7; i++)
		{
			float num = 0f;
			Vector3 vector = Vector3.zero;
			Vector3 vector5 = Vector3.zero;
			Vector3 vector10 = Vector3.zero;
			Vector3 vector2 = (flag ? hips.position : Vector3.zero);
			Vector3 vector3 = (flag ? (neck.position - vector2) : Vector3.zero);
			float num5 = Mathf.Max(vector3.sqrMagnitude, 1E-06f);
			Matrix4x4 localToWorldMatrix = t.localToWorldMatrix;
			bool ombroOk = Bolso(out var bolsoK, out var maoK, out var barK, out var r2K);
			for (int j = 0; j < wbPts.Length; j++)
			{
				Vector3 vector4 = localToWorldMatrix.MultiplyPoint3x4(wbPts[j]);
				// longe do eixo quadril-pescoço não precisa conferir (a cabeça fica dentro desse raio)
				if (flag && (vector4 - (vector2 + vector3 * Mathf.Clamp(Vector3.Dot(vector4 - vector2, vector3) / num5, 0f, 1.35f))).sqrMagnitude > num4)
				{
					continue;
				}
				// coronha encostada no ombro é o jeito certo de segurar: não conta como dentro do peito
				if (ombroOk && NoBolso(vector4, bolsoK, maoK, barK, r2K))
				{
					continue;
				}
				float num2 = ch.BodyDepthFast(vector4, out var outDir);
				if (num2 > 0f)
				{
					vector5 += outDir * num2;
				}
				if (num2 > num)
				{
					num = num2;
					vector = outDir;
					vector10 = vector4;
				}
			}
			if (i == 0)
			{
				lastPen = num / k;
				DbgTorsoPen = Mathf.Max(DbgTorsoPen, num / k);
				if (num > 0.01f * k)
				{
					DbgTorsoHit++;
				}
			}
			if (flag2)
			{
				flag2 = false;
				if (num > num6 - 0.002f * k)
				{
					// não melhorou: volta ao que era e passa para o próximo jeito
					rArm.rotation = rotation;
					rFore.rotation = rotation2;
					rHand.rotation = rotation3;
					num = num6;
					vector = vector7;
					vector5 = vector8;
					vector10 = vector9;
					num7++;
					if (num7 == 1 && flag3)
					{
						num7 = 2;
					}
					if (num7 >= 2)
					{
						DbgTorsoResid = Mathf.Max(DbgTorsoResid, num / k);
						return result;
					}
				}
				else
				{
					result = true;
					DbgPhaseKept[Mathf.Min(num7, 1)]++;
				}
			}
			if (num <= 0.005f * k)
			{
				return result;
			}
			if (i == 6)
			{
				DbgTorsoResid = Mathf.Max(DbgTorsoResid, num / k);
				return result;
			}
			Vector3 vector6 = ((vector5.sqrMagnitude > 1E-08f) ? vector5.normalized : vector);
			num6 = num;
			vector7 = vector;
			vector8 = vector5;
			vector9 = vector10;
			rotation = rArm.rotation;
			rotation2 = rFore.rotation;
			rotation3 = rHand.rotation;
			if (num7 == 0)
			{
				float num3 = Mathf.Min(num / Mathf.Max(0.5f, Vector3.Dot(vector6, vector)) + 0.02f * k, TorsoMax * k);
				DbgTorso += num3 / k;
				Vector3 position0 = rHand.position;
				FBIK.TwoBonePole(rArm, rFore, rHand, rHand.position + vector6 * num3, rFore.position + Vector3.down * 0.3f * s + base.transform.right * 0.2f * s, w);
				rHand.rotation = rotation3;
				if (i == 0)
				{
					Transform tr = base.transform;
					DbgPushN++;
					DbgPushReq += num3 / k;
					DbgPushGot += Vector3.Dot(rHand.position - position0, vector6) / k;
					DbgPushDir += new Vector3(Vector3.Dot(vector6, tr.right), Vector3.Dot(vector6, tr.up), Vector3.Dot(vector6, tr.forward));
					Vector3 rel = vector10 - position0;
					DbgDeepRel += new Vector3(Vector3.Dot(rel, tr.right), Vector3.Dot(rel, tr.up), Vector3.Dot(rel, tr.forward)) / k;
				}
			}
			else
			{
				Vector3 position = rHand.position;
				Vector3 vector11 = vector10 - position;
				Vector3 axis = Vector3.Cross(vector11, vector6);
				if (axis.sqrMagnitude < 1E-06f * k * k)
				{
					DbgTorsoResid = Mathf.Max(DbgTorsoResid, num / k);
					return result;
				}
				float num8 = Mathf.Min(Mathf.Atan2(num + 0.02f * k, Mathf.Max(vector11.magnitude, 0.05f * k)) * 57.29578f, 30f) * w;
				Quaternion quaternion = Quaternion.AngleAxis(num8, axis);
				Quaternion quaternion2 = Quaternion.AngleAxis(0f - num8, axis);
				rHand.rotation = ((Vector3.Dot(quaternion * vector11 - vector11, vector6) >= Vector3.Dot(quaternion2 * vector11 - vector11, vector6)) ? quaternion : quaternion2) * rotation3;
			}
			flag2 = true;
		}
		return result;
	}

	// pontos que representam a forma da arma (no espaço da arma): os vértices das malhas agrupados numa grade grossa,
	// um ponto por célula ocupada (uns 60 a 150 por arma). Sem malha legível, usa uma grade 3x3x3 de cada caixa.
	private static Vector3[] WeaponPoints(GameObject w)
	{
		List<Vector3> list = new List<Vector3>();
		Matrix4x4 worldToLocalMatrix = w.transform.worldToLocalMatrix;
		Renderer[] componentsInChildren = w.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren)
		{
			if (Regex.IsMatch(renderer.name, "laser|beam|flash", RegexOptions.IgnoreCase))
			{
				continue;
			}
			Mesh mesh = null;
			if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
			{
				mesh = skinnedMeshRenderer.sharedMesh;
			}
			else if (renderer.TryGetComponent<MeshFilter>(out var component))
			{
				mesh = component.sharedMesh;
			}
			if (!mesh)
			{
				continue;
			}
			Matrix4x4 matrix4x = worldToLocalMatrix * renderer.transform.localToWorldMatrix;
			if (mesh.isReadable)
			{
				Vector3[] vertices = mesh.vertices;
				for (int j = 0; j < vertices.Length; j++)
				{
					list.Add(matrix4x.MultiplyPoint3x4(vertices[j]));
				}
				continue;
			}
			Bounds bounds = mesh.bounds;
			for (int k = 0; k < 27; k++)
			{
				list.Add(matrix4x.MultiplyPoint3x4(bounds.center + Vector3.Scale(bounds.extents, new Vector3(k % 3 - 1, k / 3 % 3 - 1, k / 9 - 1))));
			}
		}
		if (list.Count == 0)
		{
			return Array.Empty<Vector3>();
		}
		Bounds bounds2 = new Bounds(list[0], Vector3.zero);
		foreach (Vector3 item in list)
		{
			bounds2.Encapsulate(item);
		}
		float num = Mathf.Max(1E-05f, Mathf.Max(bounds2.size.x, Mathf.Max(bounds2.size.y, bounds2.size.z)) / 20f);
		Dictionary<Vector3Int, Vector4> dictionary = new Dictionary<Vector3Int, Vector4>();
		foreach (Vector3 item2 in list)
		{
			Vector3Int key = Vector3Int.FloorToInt(item2 / num);
			dictionary.TryGetValue(key, out var value);
			dictionary[key] = value + new Vector4(item2.x, item2.y, item2.z, 1f);
		}
		Vector3[] array = new Vector3[dictionary.Count];
		int num2 = 0;
		foreach (Vector4 value2 in dictionary.Values)
		{
			array[num2++] = new Vector3(value2.x, value2.y, value2.z) / value2.w;
		}
		return array;
	}

	private float ErrNow()
	{
		Vector3 from = ch.BarrelDir();
		Vector3 muzzlePos = ch.MuzzlePos;
		if (!ch.hasAim || !((ch.aimPoint - muzzlePos).sqrMagnitude > 1f))
		{
			return 0f;
		}
		return Vector3.Angle(from, ch.aimPoint - muzzlePos);
	}

	[RuntimeInitializeOnLoadMethod]
	private static void ModeArg()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		int num = Array.IndexOf(commandLineArgs, "-fbrigmodo");
		if (num >= 0 && num + 1 < commandLineArgs.Length)
		{
			Mode = int.Parse(commandLineArgs[num + 1]);
		}
	}

	// follow: segunda passada depois do KeepOutOfTorso, só leva a mão esquerda até a arma (sem recuar, centralizar
	// nem girar a arma, que puxariam a arma de volta para dentro do corpo)
	private void RifleLeftHand(float w, bool follow = false)
	{
		float s = S;
		Transform transform = weapon.transform;
		Vector3 bar = ch.BarrelDir();
		Transform transform2 = Node0(weapon);
		Vector3 normalized = transform2.InverseTransformDirection(bar).normalized;
		Vector3 normalized2 = Vector3.ProjectOnPlane(Vector3.up, normalized).normalized;
		Vector3 normalized3 = Vector3.Cross(normalized2, normalized).normalized;
		Vector3 normalized4 = transform2.TransformDirection(normalized2).normalized;
		Vector3 normalized5 = transform2.TransformDirection(normalized3).normalized;
		Vector3 N = (normalized4 * 0.72f + normalized5 * 0.69f).normalized;
		Vector3 F = (Vector3.Cross(N, bar) + bar * 0.25f).normalized;
		Sec sec = Section(transform2, normalized, normalized2, normalized3);
		Vector3 C0;
		float e;
		float num;
		Func<Vector3, bool> func;
		if (sec != null)
		{
			float x = transform2.lossyScale.x;
			float hx = sec.hx * x + 0.003f * s;
			float hy = sec.hy * x + 0.003f * s;
			C0 = transform2.TransformPoint(normalized * sec.s + normalized3 * sec.cx + normalized2 * sec.cy);
			e = 1f / Mathf.Sqrt(0.69f / hx * (0.69f / hx) + 0.72f / hy * (0.72f / hy));
			num = ((((uint)Mode & 2u) != 0) ? (0.2f * s) : (0.05f * s));
			Vector3 c = C0;
			Vector3 ri = normalized5;
			Vector3 up = normalized4;
			func = delegate(Vector3 p)
			{
				Vector3 lhs = p - c;
				float num19 = Vector3.Dot(lhs, ri) / hx;
				float num20 = Vector3.Dot(lhs, up) / hy;
				return num19 * num19 + num20 * num20 <= 1f;
			};
		}
		else
		{
			float num2 = 0.024f * s;
			Vector3 vector = (grip ? grip.position : (muzzle ? (muzzle.position - normalized4 * 0.035f * s) : (rHand.position + bar * 0.35f * s)));
			float num3 = 0.3f * s;
			if ((bool)mag)
			{
				num3 = Mathf.Max(0f, Vector3.Dot(vector - mag.position, bar) - 0.1f * s);
			}
			C0 = vector + normalized4 * num2 - bar * (num3 * 0.35f);
			e = num2;
			num = ((((uint)Mode & 2u) != 0) ? Mathf.Max(num3 * 0.65f, 0.12f * s) : (num3 * 0.65f));
			Vector3 c0 = C0;
			Vector3 b0 = bar;
			float lim = num2 + 0.004f * s;
			func = delegate(Vector3 p)
			{
				Vector3 vector9 = p - c0;
				return (vector9 - b0 * Vector3.Dot(vector9, b0)).magnitude <= lim;
			};
		}
		Vector3 position = lArm.position;
		float num4 = armL * 0.93f;
		Vector3 vector2 = WristAt(0f);
		float num5 = 0f;
		if (!follow && (vector2 - position).magnitude > num4 && (WristAt(num) - position).magnitude > num4)
		{
			float num6 = 0.1f * s;
			num5 = num6;
			if ((WristAt(num) - bar * num6 - position).magnitude <= num4)
			{
				float num7 = 0f;
				float num8 = num6;
				for (int i = 0; i < 8; i++)
				{
					float num9 = (num7 + num8) / 2f;
					if ((WristAt(num) - bar * num9 - position).magnitude <= num4)
					{
						num8 = num9;
					}
					else
					{
						num7 = num9;
					}
				}
				num5 = num8;
			}
		}
		if (!follow)
		{
			retreatS = Mathf.Lerp(retreatS, num5, 1f - Mathf.Exp((0f - Time.deltaTime) * (float)((num5 > retreatS) ? 20 : 6)));
		}
		if (!follow && retreatS > 0.001f * s && (bool)rArm && (bool)rFore)
		{
			float num10 = retreatS;
			Vector3 position2 = rHand.position;
			Quaternion rotation = rHand.rotation;
			FBIK.TwoBonePole(rArm, rFore, rHand, position2 - bar * num10, rFore.position + Vector3.down * 0.3f * s + base.transform.right * 0.2f * s, w);
			rHand.rotation = rotation;
			Vector3 shift2 = rHand.position - position2;
			if (shift2.sqrMagnitude > 1E-08f)
			{
				C0 += shift2;
				Func<Vector3, bool> inn2 = func;
				func = (Vector3 p) => inn2(p - shift2);
			}
			vector2 = WristAt(0f);
		}
		float num11 = 0f;
		Vector3 vector3 = Vector3.zero;
		if (!follow && ((uint)Mode & (true ? 1u : 0u)) != 0 && (vector2 - position).magnitude > num4 && (bool)rArm && (bool)rFore)
		{
			vector3 = Vector3.ProjectOnPlane(position - vector2, bar);
			vector3 -= Vector3.up * Vector3.Dot(vector3, Vector3.up) * 0.7f;
			if (vector3.sqrMagnitude > 1E-08f)
			{
				vector3.Normalize();
				num11 = Mathf.Min(((vector2 - position).magnitude - num4) * 1.15f + 0.01f * s, 0.14f * s);
			}
		}
		if (!follow)
		{
			centerS = Mathf.Lerp(centerS, num11, 1f - Mathf.Exp((0f - Time.deltaTime) * (float)((num11 > centerS) ? 18 : 6)));
		}
		if (!follow && centerS > 0.001f * s && vector3.sqrMagnitude > 0.5f)
		{
			Vector3 position3 = rHand.position;
			Quaternion rotation2 = rHand.rotation;
			CenterN++;
			FBIK.TwoBonePole(rArm, rFore, rHand, position3 + vector3 * centerS, rFore.position + Vector3.down * 0.3f * s + base.transform.right * 0.2f * s, w);
			rHand.rotation = rotation2;
			Vector3 shift = rHand.position - position3;
			if (shift.sqrMagnitude > 1E-08f)
			{
				C0 += shift;
				Func<Vector3, bool> inn = func;
				func = (Vector3 p) => inn(p - shift);
			}
			vector2 = WristAt(0f);
		}
		CenterShift = centerS / Mathf.Max(0.0001f, s);
		if ((vector2 - position).magnitude > num4)
		{
			Vector3 vector4 = WristAt(num);
			if ((vector4 - position).magnitude <= num4)
			{
				float num12 = 0f;
				float num13 = num;
				for (int j = 0; j < 8; j++)
				{
					float num14 = (num12 + num13) / 2f;
					if ((WristAt(num14) - position).magnitude <= num4)
					{
						num13 = num14;
					}
					else
					{
						num12 = num14;
					}
				}
				vector2 = WristAt(num13);
			}
			else
			{
				vector2 = vector4;
				Vector3 vector5 = (rMid ? Vector3.Lerp(rHand.position, rMid.position, 0.5f) : rHand.position);
				Vector3 vector6 = vector2 - vector5;
				Vector3 vector7 = position - vector5;
				Vector3 axis = Vector3.Cross(vector6, vector7);
				if (!follow && axis.sqrMagnitude > 1E-08f)
				{
					axis.Normalize();
					float num15 = 0f;
					float num16 = Mathf.Min((((uint)Mode & 4u) != 0) ? 0.07f : 0.25f, Vector3.Angle(vector6, vector7) * ((float)Math.PI / 180f));
					for (int l = 0; l < 10; l++)
					{
						float num17 = (num15 + num16) / 2f;
						if ((vector5 + Quaternion.AngleAxis(num17 * 57.29578f, axis) * vector6 - position).magnitude <= num4)
						{
							num16 = num17;
						}
						else
						{
							num15 = num17;
						}
					}
					Quaternion quaternion = Quaternion.AngleAxis(num16 * w * 57.29578f, axis);
					LiftN++;
					LiftSum += num16 * 57.29578f;
					transform.SetPositionAndRotation(vector5 + quaternion * (transform.position - vector5), quaternion * transform.rotation);
					vector2 = vector5 + quaternion * vector6;
					bar = quaternion * bar;
					F = quaternion * F;
					N = quaternion * N;
					C0 = vector5 + quaternion * (C0 - vector5);
					Func<Vector3, bool> inner = func;
					Vector3 cq = vector5;
					Quaternion qi = Quaternion.Inverse(quaternion);
					func = (Vector3 p) => inner(cq + qi * (p - cq));
				}
			}
		}
		Vector3 pole = position - base.transform.right * 0.35f * s + Vector3.down * 0.6f * s - base.transform.forward * 0.05f * s;
		FBIK.TwoBonePole(lArm, lFore, lHand, vector2, pole, w);
		LeftTarget = vector2;
		LeftTargetValid = true;
		GripCenter = C0;
		GripAxis = bar;
		GripRadius = e;
		if (lPalmOk)
		{
			Quaternion rotation3 = lHand.rotation;
			Quaternion quaternion2 = Quaternion.Slerp(rotation3, Quaternion.LookRotation(F, N) * Quaternion.Inverse(lPalm), w);
			Vector3 normalized6 = (lHand.position - lFore.position).normalized;
			Quaternion quaternion3 = quaternion2 * Quaternion.Inverse(rotation3);
			Vector3 vector8 = Vector3.Project(new Vector3(quaternion3.x, quaternion3.y, quaternion3.z), normalized6);
			Quaternion quaternion4 = new Quaternion(vector8.x, vector8.y, vector8.z, quaternion3.w);
			float num18 = Mathf.Sqrt(quaternion4.x * quaternion4.x + quaternion4.y * quaternion4.y + quaternion4.z * quaternion4.z + quaternion4.w * quaternion4.w);
			if (num18 > 1E-06f)
			{
				quaternion4 = new Quaternion(quaternion4.x / num18, quaternion4.y / num18, quaternion4.z / num18, quaternion4.w / num18);
				lFore.rotation = Quaternion.Slerp(Quaternion.identity, quaternion4, 0.5f) * lFore.rotation;
			}
			lHand.rotation = quaternion2;
		}
		if (Hd)
		{
			Wrap(func, w);
		}
		Vector3 WristAt(float k)
		{
			return C0 - bar * k - N * (e + 0.016f * s) - F * (0.72f * meta);
		}
	}

	private void Wrap(Func<Vector3, bool> inside, float w)
	{
		Vector3 A = FingerAxis(right: false);
		if (A.sqrMagnitude < 1E-08f)
		{
			return;
		}
		foreach (Finger finger in fingers)
		{
			Finger f = finger;
			if (f.right)
			{
				continue;
			}
			Quaternion localRotation = f.b[0].localRotation;
			Quaternion localRotation2 = f.b[1].localRotation;
			Quaternion localRotation3 = f.b[2].localRotation;
			float num = 0f;
			float num2 = 1.1f;
			for (float num3 = 0.15f; num3 <= 1.5f; num3 += 0.15f)
			{
				if (Touch(num3))
				{
					num2 = num3;
					num = num3 - 0.15f;
					break;
				}
			}
			for (int i = 0; i < 4; i++)
			{
				float num4 = (num + num2) / 2f;
				if (Touch(num4))
				{
					num2 = num4;
				}
				else
				{
					num = num4;
				}
			}
			Bend(f, Mathf.Max(0.8f, Mathf.Min(1.25f, num2)), A);
			if (w < 0.999f)
			{
				f.b[0].localRotation = Quaternion.Slerp(localRotation, f.b[0].localRotation, w);
				f.b[1].localRotation = Quaternion.Slerp(localRotation2, f.b[1].localRotation, w);
				f.b[2].localRotation = Quaternion.Slerp(localRotation3, f.b[2].localRotation, w);
			}
			bool Touch(float th)
			{
				Bend(f, th, A);
				return inside(Tip(f));
			}
		}
	}

	private static Transform Node0(GameObject w)
	{
		Transform transform = w.transform;
		if (transform.childCount != 1 || (bool)transform.GetComponent<Renderer>())
		{
			return transform;
		}
		return transform.GetChild(0);
	}

	private static Vector3 GV(object o)
	{
		if (!(o is List<object> { Count: >=3 } list))
		{
			return Vector3.zero;
		}
		return new Vector3(0f - Json.Fv(list[0]), Json.Fv(list[1]), Json.Fv(list[2]));
	}

	private Sec Section(Transform n0, Vector3 barL, Vector3 upL, Vector3 rightL)
	{
		string text = ch.weapon?.id;
		if (text == null)
		{
			return null;
		}
		if (sections.TryGetValue(text, out var value))
		{
			return value;
		}
		Dictionary<string, object> dictionary = Json.O(Json.O(GameData.TpsGrip, text), "l");
		List<object> list = Json.L(dictionary, "m");
		if (dictionary == null || list == null || list.Count == 0)
		{
			return sections[text] = null;
		}
		Vector3 lhs = Vector3.Lerp(GV(dictionary["w"]), GV(list[0]), 0.6f);
		float num = Vector3.Dot(lhs, barL);
		float num2 = Vector3.Dot(lhs, rightL);
		float num3 = Vector3.Dot(lhs, upL);
		float num4 = 1E+09f;
		float num5 = -1E+09f;
		float num6 = 1E+09f;
		float num7 = -1E+09f;
		int num8 = 0;
		Matrix4x4 worldToLocalMatrix = n0.worldToLocalMatrix;
		MeshFilter[] componentsInChildren = n0.GetComponentsInChildren<MeshFilter>();
		foreach (MeshFilter meshFilter in componentsInChildren)
		{
			Mesh sharedMesh = meshFilter.sharedMesh;
			if (!sharedMesh || !sharedMesh.isReadable)
			{
				continue;
			}
			bool flag = false;
			Transform parent = meshFilter.transform;
			while ((bool)parent && parent != n0.parent)
			{
				if (SKIP.IsMatch(parent.name))
				{
					flag = true;
					break;
				}
				parent = parent.parent;
			}
			Renderer component = meshFilter.GetComponent<Renderer>();
			if (flag || !component || ((bool)component.sharedMaterial && component.sharedMaterial.renderQueue >= 3000))
			{
				continue;
			}
			Matrix4x4 matrix4x = worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
			Vector3[] vertices = sharedMesh.vertices;
			foreach (Vector3 point in vertices)
			{
				Vector3 lhs2 = matrix4x.MultiplyPoint3x4(point);
				float num9 = Vector3.Dot(lhs2, rightL);
				float num10 = Vector3.Dot(lhs2, upL);
				if (!(Mathf.Abs(Vector3.Dot(lhs2, barL) - num) > 0.012f) && !(new Vector2(num9 - num2, num10 - num3).magnitude > 0.09f))
				{
					if (num9 < num4)
					{
						num4 = num9;
					}
					if (num9 > num5)
					{
						num5 = num9;
					}
					if (num10 < num6)
					{
						num6 = num10;
					}
					if (num10 > num7)
					{
						num7 = num10;
					}
					num8++;
				}
			}
		}
		return sections[text] = ((num8 >= 6) ? new Sec
		{
			s = num,
			cx = (num4 + num5) / 2f,
			cy = (num6 + num7) / 2f,
			hx = Mathf.Max(0.01f, (num5 - num4) / 2f),
			hy = Mathf.Max(0.01f, (num7 - num6) / 2f)
		} : null);
	}
}
}
