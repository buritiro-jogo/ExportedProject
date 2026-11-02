namespace FreeBala
{
public interface IKnockable
{
	bool Knocked { get; set; }

	float Hp { get; set; }

	float MaxHp { get; }

	float KnockInvT { get; set; }

	float BleedAcc { get; set; }

	object RevivedBy { get; set; }

	float ReviveP { get; set; }

	bool Civ { get; }
}
}
