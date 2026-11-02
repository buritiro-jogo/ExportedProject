namespace FreeBala
{
public struct FBDamage
{
	public FBCharacter victim;

	public FBCharacter source;

	public FBDamageType type;

	public float amount;

	public float applied;

	public float hpBefore;

	public float hpAfter;

	public bool head;

	public bool killed;

	public bool knocked;

	public FBHitInfo hit;

	public string why;

	public bool Local
	{
		get
		{
			if ((bool)victim)
			{
				return !victim.isRemote;
			}
			return false;
		}
	}
}
}
