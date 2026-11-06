namespace ConfigScene;

public class HSystem : BaseSystem
{
	public bool Visible = true;

	public bool Son = true;

	public bool Cloth = true;

	public bool Accessory = true;

	public bool Shoes = true;

	public int Siru = 1;

	public bool Urine;

	public bool Sio;

	public bool Gloss = true;

	public bool FeelingGauge = true;

	public bool ActionGuide = true;

	public bool MenuIcon = true;

	public bool FinishButton = true;

	public bool InitCamera = true;

	public bool EyeDir0;

	public bool NeckDir0;

	public bool EyeDir1;

	public bool NeckDir1;

	public HSystem(string elementName)
		: base(elementName)
	{
	}

	public override void Init()
	{
		Visible = true;
		Son = true;
		Cloth = true;
		Accessory = true;
		Shoes = true;
		Siru = 1;
		Urine = false;
		Sio = false;
		Gloss = true;
		FeelingGauge = true;
		ActionGuide = true;
		MenuIcon = true;
		FinishButton = true;
		InitCamera = true;
		EyeDir0 = false;
		NeckDir0 = false;
		EyeDir1 = false;
		NeckDir1 = false;
	}
}
