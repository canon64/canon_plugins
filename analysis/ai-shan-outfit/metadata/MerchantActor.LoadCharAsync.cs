	protected override IEnumerator LoadCharAsync(string fileName)
	{
		ChaFileControl chaFile = null;
		string assetName = "ill_Default_Merchant";
		Dictionary<int, ListInfoBase> dict = Singleton<Character>.Instance.chaListCtrl.GetCategoryInfo(ChaListDefine.CategoryNo.cha_sample_f);
		foreach (KeyValuePair<int, ListInfoBase> item in dict)
		{
			if (item.Value.GetInfo(ChaListDefine.KeyType.MainData) == assetName)
			{
				chaFile = new ChaFileControl();
				chaFile.LoadFromAssetBundle(item.Value.GetInfo(ChaListDefine.KeyType.MainAB), assetName);
				break;
			}
		}
		base.ChaControl = Singleton<Character>.Instance.CreateChara(1, base.gameObject, 0, chaFile);
		base.ChaControl.Load();
		yield return null;
		base.ChaControl.ChangeLookEyesPtn(3);
		base.ChaControl.ChangeLookNeckPtn(3);
		Controller.InitializeFaceLight(base.ChaControl.gameObject);
		yield return null;
	}
