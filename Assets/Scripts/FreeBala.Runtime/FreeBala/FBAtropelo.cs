using UnityEngine;

namespace FreeBala
{
public static class FBAtropelo
{
	public static int Hits;

	public static int GotUp;

	public static string Last;

	private static readonly string[] UP_BACK = new string[3] { "FF_SYS_KO_GetUp", "IE_Downed_GetUp", "FF_SYS_REB_Levantar" };

	private static readonly string[] UP_FRONT = new string[2] { "X171_HM_Levantando", "FF_SYS_KO_GetUp" };

	public static void Hit(FBCharacter a, FBCar car, float v, Vector3 f, bool damage)
	{
		if (!a || !a.alive)
		{
			return;
		}
		float num = Mathf.Abs(v);
		Hits++;
		Last = $"{a.skinId} a {num * 3.6f / 1.2f:0} km/h";
		FBFisica.Sound("atropelamento", a.Pos + Vector3.up * 0.8f, Mathf.Clamp01(0.4f + num / 20f), 9f);
		FBFisica.Sound("corpo", a.Pos, 0.8f, 7f);
		FBAudio.PlayAt("ie_bodyhit", a.Pos, 0.8f, 5f);
		FBNpcVoz.Say(a, "atropelado", 1f, 6f, 4f, 0f);
		bool flag = damage && num * 6.5f >= a.hp;
		if (damage)
		{
			FBCombat.DamageActor(a, num * 6.5f, head: false, car ? car.driver : null, "car");
		}
		if (!a.alive || flag)
		{
			return;
		}
		if (num < 3.2f || a.isPlayer)
		{
			a.velocity += f * num * 0.45f + Vector3.up * 2.2f;
			a.grounded = false;
			return;
		}
		FBRagdoll fBRagdoll = FBRagdoll.Of(a);
		if (!fBRagdoll)
		{
			a.velocity += f * num * 0.6f + Vector3.up * 3f;
			return;
		}
		a.StopDance(force: true);
		Vector3 imp = (f * num * 1.1f + Vector3.up * (1.5f + num * 0.25f)) * 1f;
		fBRagdoll.Go(imp, a.Pos + Vector3.up * 0.55f * a.ScaleK + f * -0.2f);
		a.gameObject.GetOrAdd<FBKnockdown>().Begin(fBRagdoll, Mathf.Lerp(2.5f, 5f, Mathf.Clamp01(num / 15f)));
	}
}
}
