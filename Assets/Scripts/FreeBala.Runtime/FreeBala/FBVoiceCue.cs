namespace FreeBala
{
public struct FBVoiceCue
{
	public int key;

	public FBVoiceBank bank;

	public int variation;

	public int prio;

	public float intensity;

	public bool interrupt;

	public int steal;

	public override string ToString()
	{
		return string.Format("{0}:{1}#{2} i={3:0.00}{4}", key, bank, variation, intensity, interrupt ? " int" : "");
	}
}
}
