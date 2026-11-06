using System;
using System.Collections.Generic;
using System.Text;
using AIChara;
using AIProject;
using Manager;
using UnityEngine;

public class SiruPasteCtrl : MonoBehaviour
{
	[Serializable]
	public class Timing
	{
		[Label("タイミング")]
		public float normalizeTime;

		public List<int> lstWhere = new List<int>();
	}

	[Serializable]
	public class PasteInfo
	{
		[Label("アニメーション名")]
		public string anim = string.Empty;

		public Timing timing;
	}

	private ExcelData excelData;

	private List<string> row = new List<string>();

	[SerializeField]
	private List<PasteInfo> lstPaste = new List<PasteInfo>();

	[DisabledGroup("女クラス")]
	[SerializeField]
	private ChaControl chaFemale;

	private float oldFrame;

	private int oldHash;

	private PasteInfo p;

	private ChaFileDefine.SiruParts sp;

	private Timing ti;

	private StringBuilder abName;

	private StringBuilder asset;

	private string[] astr;

	public bool Init(ChaControl _female)
	{
		abName = new StringBuilder();
		asset = new StringBuilder();
		Release();
		chaFemale = _female;
		return true;
	}

	public void Release()
	{
		lstPaste.Clear();
	}

	public bool Load(string _assetpath, string _file, int _id)
	{
		lstPaste.Clear();
		if (_file == string.Empty)
		{
			return false;
		}
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_assetpath);
		assetBundleNameListFromPath.Sort();
		asset.Clear();
		asset.Append(_file);
		excelData = null;
		for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
		{
			abName.Clear();
			abName.Append(assetBundleNameListFromPath[i]);
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(abName.ToString(), asset.ToString(), string.Empty))
			{
				continue;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(abName.ToString(), asset.ToString(), clone: false, string.Empty);
			Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(abName.ToString());
			if (excelData == null)
			{
				continue;
			}
			int num = 1;
			while (num < excelData.MaxCell)
			{
				row = excelData.list[num++].list;
				int num2 = 1;
				PasteInfo pasteInfo = new PasteInfo();
				pasteInfo.anim = row.GetElement(num2++);
				pasteInfo.timing = new Timing();
				pasteInfo.timing.normalizeTime = float.Parse(row.GetElement(num2++));
				astr = row.GetElement(num2++).Split(',');
				for (int j = 0; j < astr.Length; j++)
				{
					int result = 0;
					if (!int.TryParse(astr[j], out result))
					{
						result = 0;
					}
					pasteInfo.timing.lstWhere.Add(result);
				}
				lstPaste.Add(pasteInfo);
			}
		}
		oldFrame = 0f;
		oldHash = 0;
		return true;
	}

	public bool Proc(AnimatorStateInfo _ai)
	{
		if (oldHash != _ai.shortNameHash)
		{
			oldFrame = 0f;
		}
		oldHash = _ai.shortNameHash;
		for (int i = 0; i < lstPaste.Count; i++)
		{
			p = lstPaste[i];
			if (!_ai.IsName(p.anim))
			{
				continue;
			}
			float num = _ai.normalizedTime % 1f;
			ti = p.timing;
			if (oldFrame <= ti.normalizeTime && ti.normalizeTime < num)
			{
				for (int j = 0; j < ti.lstWhere.Count; j++)
				{
					sp = (ChaFileDefine.SiruParts)ti.lstWhere[j];
					chaFemale.SetSiruFlag(sp, (byte)Mathf.Clamp(chaFemale.GetSiruFlag(sp) + 1, 0, 2));
				}
			}
		}
		oldFrame = _ai.normalizedTime;
		return true;
	}
}
