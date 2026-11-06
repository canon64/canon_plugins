namespace ActionGame.Communication;

public class ChangeValueSelectInfo : ChangeValueAbstractInfo
{
	private int[] aFavor;

	private int[] aLewdness;

	public int this[int _idx, int _kind]
	{
		get
		{
			if (_kind != 0)
			{
				return aLewdness[_idx];
			}
			return aFavor[_idx];
		}
		set
		{
			switch (_kind)
			{
			case 0:
				aFavor[_idx] = value;
				break;
			case 1:
				aLewdness[_idx] = value;
				break;
			}
		}
	}

	public int select { get; set; }

	public int success { get; set; }

	public bool isSuccess => success == select;

	public override int favor => aFavor.SafeGet(select);

	public override int lewdness => aLewdness.SafeGet(select);

	public void Init(int _num)
	{
		aFavor = new int[_num];
		aLewdness = new int[_num];
	}
}
