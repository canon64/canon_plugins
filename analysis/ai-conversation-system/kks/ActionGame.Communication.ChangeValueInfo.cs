namespace ActionGame.Communication;

public class ChangeValueInfo : ChangeValueAbstractInfo
{
	private int[] array = new int[2];

	public int this[int _idx]
	{
		get
		{
			return array[_idx];
		}
		set
		{
			array[_idx] = value;
		}
	}

	public override int favor => array[0];

	public override int lewdness => array[1];

	public ChangeValueInfo(int _favor = 0, int _lewdness = 0)
	{
		array[0] = _favor;
		array[1] = _lewdness;
	}
}
