namespace ActionGame.Communication;

public abstract class ChangeValueAbstractInfo
{
	public abstract int favor { get; }

	public abstract int lewdness { get; }

	public int topic { get; set; }
}
