namespace FreeBala
{
public struct FBVoiceRule
{
	public int prio;

	public float cd;

	public float vol;

	public float pitch;

	public float refD;

	public float npcChance;

	public FBVoiceRule(int prio, float cd, float vol, float pitch = 1f, float refD = 5f, float npcChance = 1f)
	{
		this.prio = prio;
		this.cd = cd;
		this.vol = vol;
		this.pitch = pitch;
		this.refD = refD;
		this.npcChance = npcChance;
	}

	public static FBVoiceRule Default(FBVoiceBank b)
	{
		return b switch
		{
			FBVoiceBank.Morte => new FBVoiceRule(100, 0f, 1f, 1f, 7f), 
			FBVoiceBank.GasMorte => new FBVoiceRule(100, 0f, 0.95f, 1f, 6f), 
			FBVoiceBank.Caido => new FBVoiceRule(80, 2f, 0.95f, 1f, 6f), 
			FBVoiceBank.Explosao => new FBVoiceRule(70, 1.2f, 1f, 0.97f, 7f), 
			FBVoiceBank.Atropelo => new FBVoiceRule(70, 1.2f, 1f, 1f, 7f), 
			FBVoiceBank.QuedaGrave => new FBVoiceRule(60, 1f, 0.95f, 0.98f, 6f), 
			FBVoiceBank.Cabeca => new FBVoiceRule(55, 0.6f, 0.95f, 1.04f, 6f), 
			FBVoiceBank.DorGrave => new FBVoiceRule(50, 0.7f, 0.95f, 1f, 6f), 
			FBVoiceBank.Fogo => new FBVoiceRule(50, 1.4f, 0.9f, 1f, 6f), 
			FBVoiceBank.DorMedia => new FBVoiceRule(40, 0.6f, 0.85f, 1f, 5f, 0.8f), 
			FBVoiceBank.Queda => new FBVoiceRule(35, 0.8f, 0.7f, 1f, 4f), 
			FBVoiceBank.Membro => new FBVoiceRule(30, 0.6f, 0.75f, 1.02f, 5f, 0.7f), 
			FBVoiceBank.DorLeve => new FBVoiceRule(30, 0.55f, 0.75f, 1.02f, 5f, 0.6f), 
			FBVoiceBank.PoucaVida => new FBVoiceRule(25, 2.5f, 0.7f, 0.98f, 4f), 
			FBVoiceBank.Ambiente => new FBVoiceRule(25, 1.2f, 0.7f, 1f, 4f, 0.7f), 
			FBVoiceBank.GasFraca => new FBVoiceRule(20, 2.5f, 0.6f, 0.97f, 3.5f), 
			FBVoiceBank.GasTosse => new FBVoiceRule(18, 1.6f, 0.8f, 1f, 4.5f), 
			FBVoiceBank.Sangrando => new FBVoiceRule(15, 5f, 0.55f, 0.96f, 3.5f, 0.6f), 
			FBVoiceBank.GasIrrita => new FBVoiceRule(12, 3f, 0.6f, 1f, 3.5f), 
			FBVoiceBank.GasEsforco => new FBVoiceRule(12, 3f, 0.55f, 1f, 3.5f), 
			FBVoiceBank.GasResp => new FBVoiceRule(10, 1.4f, 0.55f, 1f, 3f), 
			_ => new FBVoiceRule(10, 1f, 0.6f, 1f, 4f), 
		};
	}
}
}
