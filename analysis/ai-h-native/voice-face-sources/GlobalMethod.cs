using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AIChara;
using AIProject;
using Cinemachine;
using FBSAssist;
using Illusion.Extensions;
using IllusionUtility.GetUtility;
using UnityEngine;

public class GlobalMethod
{
	public class FloatBlend
	{
		private bool blend;

		private float min;

		private float max;

		private TimeProgressCtrl tpc = new TimeProgressCtrl();

		public bool Start(float _min, float _max, float _timeBlend = 0.15f)
		{
			tpc.SetProgressTime(_timeBlend);
			tpc.Start();
			min = _min;
			max = _max;
			blend = true;
			return true;
		}

		public bool Proc(ref float _ans)
		{
			if (!blend)
			{
				return false;
			}
			float num = tpc.Calculate();
			_ans = Mathf.Lerp(min, max, num);
			if (num >= 1f)
			{
				blend = false;
			}
			return true;
		}
	}

	private static ExcelData excelData;

	private static List<string> cell = new List<string>();

	private static List<string> row = new List<string>();

	private static List<ExcelData.Param> excelParams = new List<ExcelData.Param>();

	private static List<string> lstABName = new List<string>();

	private static StringBuilder strNo = new StringBuilder();

	public static void setCameraMoveFlag(VirtualCameraController _ctrl, bool _bPlay)
	{
		if (!(_ctrl == null))
		{
			_ctrl.NoCtrlCondition = () => !_bPlay;
		}
	}

	public static bool IsCameraMoveFlag(VirtualCameraController _ctrl)
	{
		if (_ctrl == null)
		{
			return false;
		}
		VirtualCameraController.NoCtrlFunc noCtrlCondition = _ctrl.NoCtrlCondition;
		bool flag = true;
		if (noCtrlCondition != null)
		{
			flag = noCtrlCondition();
		}
		return !flag;
	}

	public static bool IsCameraActionFlag(CameraControl_Ver2 _ctrl)
	{
		if (_ctrl == null)
		{
			return false;
		}
		return _ctrl.isControlNow;
	}

	public static void setCameraBase(VirtualCameraController _ctrl, Transform _transTarget)
	{
		if (!(_ctrl == null))
		{
			_ctrl.Follow.position = _transTarget.position;
			_ctrl.Follow.rotation = _transTarget.rotation;
		}
	}

	public static void setCameraBase(VirtualCameraController _ctrl, Vector3 _pos, Vector3 _rot)
	{
		if (!(_ctrl == null))
		{
			_ctrl.Follow.position = _pos;
			_ctrl.Follow.rotation = Quaternion.Euler(_rot);
		}
	}

	public static void CameraKeyCtrl(VirtualCameraController _ctrl, ChaControl[] _Females)
	{
		if (_Females == null || _ctrl == null)
		{
			return;
		}
		if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
		{
			if (Input.GetKeyDown(KeyCode.Q))
			{
				GameObject gameObject = _Females[0].objBodyBone.transform.FindLoop("cf_J_Head");
				if (!(gameObject == null))
				{
					_ctrl.TargetPos = _ctrl.Follow.InverseTransformPoint(gameObject.transform.position);
				}
			}
			else if (Input.GetKeyDown(KeyCode.W))
			{
				GameObject gameObject2 = _Females[0].objBodyBone.transform.FindLoop("cf_J_Mune00");
				if (!(gameObject2 == null))
				{
					_ctrl.TargetPos = _ctrl.Follow.InverseTransformPoint(gameObject2.transform.position);
				}
			}
			else if (Input.GetKeyDown(KeyCode.E))
			{
				GameObject gameObject3 = _Females[0].objBodyBone.transform.FindLoop("cf_J_Kokan");
				if (!(gameObject3 == null))
				{
					_ctrl.TargetPos = _ctrl.Follow.InverseTransformPoint(gameObject3.transform.position);
				}
			}
		}
		else
		{
			if (!(_Females[1] != null) || !_Females[1].objBodyBone)
			{
				return;
			}
			if (Input.GetKeyDown(KeyCode.Q))
			{
				GameObject gameObject4 = _Females[1].objBodyBone.transform.FindLoop("cf_J_Head");
				if (!(gameObject4 == null))
				{
					_ctrl.TargetPos = _ctrl.Follow.InverseTransformPoint(gameObject4.transform.position);
				}
			}
			else if (Input.GetKeyDown(KeyCode.W))
			{
				GameObject gameObject5 = _Females[1].objBodyBone.transform.FindLoop("cf_J_Mune00");
				if (!(gameObject5 == null))
				{
					_ctrl.TargetPos = _ctrl.Follow.InverseTransformPoint(gameObject5.transform.position);
				}
			}
			else if (Input.GetKeyDown(KeyCode.E))
			{
				GameObject gameObject6 = _Females[1].objBodyBone.transform.FindLoop("cf_J_Kokan");
				if (!(gameObject6 == null))
				{
					_ctrl.TargetPos = _ctrl.Follow.InverseTransformPoint(gameObject6.transform.position);
				}
			}
		}
	}

	public static void saveCamera(VirtualCameraController _ctrl, string _strAssetPath, string _strfile)
	{
		if (!(_ctrl == null))
		{
			_ctrl.CameraDataSave(_strAssetPath, _strfile);
		}
	}

	public static void saveCamera(CinemachineVirtualCamera _ctrl, string _strAssetPath, string _strfile)
	{
		if (_ctrl == null)
		{
			return;
		}
		Vector3 localPosition = _ctrl.LookAt.localPosition;
		Vector3 vector = _ctrl.transform.localPosition - _ctrl.LookAt.localPosition;
		FileData fileData = new FileData(string.Empty);
		string text = fileData.Create(_strAssetPath) + _strfile + ".txt";
		Debug.Log(text);
		using StreamWriter streamWriter = new StreamWriter(text, append: false, Encoding.GetEncoding("UTF-8"));
		streamWriter.Write(localPosition.x);
		streamWriter.Write('\n');
		streamWriter.Write(localPosition.y);
		streamWriter.Write('\n');
		streamWriter.Write(localPosition.z);
		streamWriter.Write('\n');
		streamWriter.Write(vector.x);
		streamWriter.Write('\n');
		streamWriter.Write(vector.y);
		streamWriter.Write('\n');
		streamWriter.Write(vector.z);
		streamWriter.Write('\n');
		streamWriter.Write(_ctrl.m_Lens.FieldOfView);
		streamWriter.Write('\n');
	}

	public static void loadCamera(VirtualCameraController _ctrl, string _assetbundleFolder, string _strfile, bool _isDirect = false)
	{
		if (!(_ctrl == null))
		{
			_ctrl.CameraDataLoad(_assetbundleFolder, _strfile, _isDirect);
		}
	}

	public static void loadCamera(CinemachineVirtualCamera _ctrl, string _assetbundleFolder, string _strfile, bool _isDirect = false)
	{
		if (_ctrl == null)
		{
			return;
		}
		string text = string.Empty;
		if (!_isDirect)
		{
			text = LoadAllListText(_assetbundleFolder, _strfile);
		}
		else
		{
			TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(_assetbundleFolder, _strfile, clone: false, string.Empty);
			AssetBundleManager.UnloadAssetBundle(_assetbundleFolder, isUnloadForceRefCount: true);
			if ((bool)textAsset)
			{
				text = textAsset.text;
			}
		}
		if (text == string.Empty)
		{
			DebugLog("cameraファイル読み込めません", 1);
			return;
		}
		GetListString(text, out var data);
		Vector3 vector = default(Vector3);
		vector.x = float.Parse(data[0][0]);
		vector.y = float.Parse(data[1][0]);
		vector.z = float.Parse(data[2][0]);
		Vector3 vector2 = default(Vector3);
		vector2.x = float.Parse(data[3][0]);
		vector2.y = float.Parse(data[4][0]);
		vector2.z = float.Parse(data[5][0]);
		_ctrl.LookAt.localPosition = vector;
		_ctrl.transform.localPosition = vector2 + vector;
		float result = 0f;
		if (float.TryParse(data[6][0], out result))
		{
			_ctrl.m_Lens.FieldOfView = result;
		}
	}

	public static void loadResetCamera(VirtualCameraController _ctrl, string _assetbundleFolder, string _strfile, bool _isDirect = false)
	{
		if (!(_ctrl == null))
		{
			_ctrl.CameraResetDataLoad(_assetbundleFolder, _strfile, _isDirect);
		}
	}

	public static void DebugLog(string _str, int _state = 0)
	{
	}

	public static void SetAllClothState(ChaControl _female, bool _isUpper, int _state, bool _isForce = false)
	{
		if (_female == null)
		{
			return;
		}
		if (_state < 0)
		{
			_state = 0;
		}
		List<int> list = new List<int>();
		if (_isUpper)
		{
			list.Add(0);
			list.Add(2);
		}
		else
		{
			list.Add(1);
			list.Add(3);
			list.Add(5);
		}
		foreach (int item in list)
		{
			if (_female.IsClothesStateKind(item) && (_female.fileStatus.clothesState[item] < _state || _isForce))
			{
				_female.SetClothesState(item, (byte)_state);
			}
		}
	}

	public static int ValLoop(int valNow, int valMax)
	{
		return (valMax > 1) ? ((valNow % valMax + valMax) % valMax) : 0;
	}

	public static int ValLoopEX(int valNow, int valMin, int valMax)
	{
		return ValLoop(valNow - valMin, valMax - valMin) + valMin;
	}

	public static void GetListString(string text, out string[][] data)
	{
		string[] array = text.Split('\n');
		int num = array.Length;
		if (num != 0 && array[num - 1].Trim() == string.Empty)
		{
			num--;
		}
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			string[] array2 = array[i].Split('\t');
			num2 = Mathf.Max(num2, array2.Length);
		}
		data = new string[num][];
		for (int j = 0; j < num; j++)
		{
			data[j] = new string[num2];
			string[] array3 = array[j].Split('\t');
			for (int k = 0; k < array3.Length; k++)
			{
				array3[k] = array3[k].Replace("\r", string.Empty).Replace("\n", string.Empty);
				if (k >= num2)
				{
					break;
				}
				data[j][k] = array3[k];
			}
		}
	}

	public static int GetIntTryParse(string _text, int _init = 0)
	{
		int result = 0;
		if (int.TryParse(_text, out result))
		{
			return result;
		}
		return _init;
	}

	public static bool RangeOn<T>(T valNow, T valMin, T valMax) where T : IComparable
	{
		object obj = valMax;
		int result;
		if (valNow.CompareTo(obj) <= 0)
		{
			object obj2 = valMin;
			result = ((valNow.CompareTo(obj2) >= 0) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	public static bool RangeOff<T>(T valNow, T valMin, T valMax) where T : IComparable
	{
		object obj = valMax;
		int result;
		if (valNow.CompareTo(obj) < 0)
		{
			object obj2 = valMin;
			result = ((valNow.CompareTo(obj2) > 0) ? 1 : 0);
		}
		else
		{
			result = 0;
		}
		return (byte)result != 0;
	}

	public static string LoadAllListText(string _assetbundleFolder, string _strLoadFile, List<string> _OmitFolderName = null)
	{
		StringBuilder stringBuilder = new StringBuilder();
		lstABName.Clear();
		lstABName = GetAssetBundleNameListFromPath(_assetbundleFolder);
		lstABName.Sort();
		for (int i = 0; i < lstABName.Count; i++)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(lstABName[i]);
			fileNameWithoutExtension = YS_Assist.GetStringRight(fileNameWithoutExtension, 2);
			if (_OmitFolderName != null && _OmitFolderName.Contains(fileNameWithoutExtension))
			{
				continue;
			}
			string[] allAssetName = AssetBundleCheck.GetAllAssetName(lstABName[i], _WithExtension: false);
			bool flag = false;
			for (int j = 0; j < allAssetName.Length; j++)
			{
				if (allAssetName[j].Compare(_strLoadFile, ignoreCase: true))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				DebugLog("[" + lstABName[i] + "][" + _strLoadFile + "]は見つかりません", 1);
			}
			else
			{
				TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(lstABName[i], _strLoadFile, clone: false, string.Empty);
				AssetBundleManager.UnloadAssetBundle(lstABName[i], isUnloadForceRefCount: true);
				if (!(textAsset == null))
				{
					stringBuilder.Append(textAsset.text);
				}
			}
		}
		return stringBuilder.ToString();
	}

	public static string LoadAllListText(List<string> _lstAssetBundleNames, string _strLoadFile)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < _lstAssetBundleNames.Count; i++)
		{
			string[] allAssetName = AssetBundleCheck.GetAllAssetName(_lstAssetBundleNames[i], _WithExtension: false);
			bool flag = false;
			for (int j = 0; j < allAssetName.Length; j++)
			{
				if (allAssetName[j].Compare(_strLoadFile, ignoreCase: true))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				DebugLog("[" + _lstAssetBundleNames[i] + "][" + _strLoadFile + "]は見つかりません", 1);
			}
			else
			{
				TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(_lstAssetBundleNames[i], _strLoadFile, clone: false, string.Empty);
				AssetBundleManager.UnloadAssetBundle(_lstAssetBundleNames[i], isUnloadForceRefCount: true);
				if (!(textAsset == null))
				{
					stringBuilder.Append(textAsset.text);
				}
			}
		}
		return stringBuilder.ToString();
	}

	public static List<string> LoadAllListTextFromList(string _assetbundleFolder, string _strLoadFile, ref List<string> lst, List<string> _OmitFolderName = null)
	{
		lstABName.Clear();
		lstABName = GetAssetBundleNameListFromPath(_assetbundleFolder);
		lstABName.Sort();
		for (int i = 0; i < lstABName.Count; i++)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(lstABName[i]);
			fileNameWithoutExtension = YS_Assist.GetStringRight(fileNameWithoutExtension, 2);
			if (_OmitFolderName != null && _OmitFolderName.Contains(fileNameWithoutExtension))
			{
				continue;
			}
			bool flag = false;
			if (AssetFileExist(lstABName[i], _strLoadFile, string.Empty))
			{
				TextAsset textAsset = CommonLib.LoadAsset<TextAsset>(lstABName[i], _strLoadFile, clone: false, string.Empty);
				AssetBundleManager.UnloadAssetBundle(lstABName[i], isUnloadForceRefCount: true);
				if (!(textAsset == null))
				{
					lst.Add(textAsset.text);
				}
			}
		}
		return lst;
	}

	public static List<ExcelData.Param> LoadExcelData(string _strAssetPath, string _strFileName, int sCell, int sRow, int eCell, int eRow, bool _isWarning = true)
	{
		bool flag = false;
		if (!AssetFileExist(_strAssetPath, _strFileName, string.Empty))
		{
			if (_isWarning)
			{
				DebugLog("excel : [" + _strAssetPath + "][" + _strFileName + "]は見つかりません", 1);
			}
			return null;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(_strAssetPath, _strFileName, typeof(ExcelData));
		AssetBundleManager.UnloadAssetBundle(_strAssetPath, isUnloadForceRefCount: true);
		if (assetBundleLoadAssetOperation.IsEmpty())
		{
			if (_isWarning)
			{
				DebugLog("excel : [" + _strFileName + "]は[" + _strAssetPath + "]の中に入っていません", 1);
			}
			return null;
		}
		excelData = assetBundleLoadAssetOperation.GetAsset<ExcelData>();
		cell.Clear();
		foreach (ExcelData.Param item in excelData.list)
		{
			cell.Add(item.list[sCell]);
		}
		row.Clear();
		foreach (string item2 in excelData.list[sRow].list)
		{
			row.Add(item2);
		}
		List<string> list = cell;
		List<string> list2 = row;
		ExcelData.Specify specify = new ExcelData.Specify(eCell, eRow);
		ExcelData.Specify specify2 = new ExcelData.Specify(list.Count, list2.Count);
		excelParams.Clear();
		if ((uint)specify.cell > specify2.cell || (uint)specify.row > specify2.row)
		{
			return null;
		}
		if (specify.cell < excelData.list.Count)
		{
			for (int i = specify.cell; i < excelData.list.Count && i <= specify2.cell; i++)
			{
				ExcelData.Param param = new ExcelData.Param();
				if (specify.row < excelData.list[i].list.Count)
				{
					param.list = new List<string>();
					for (int j = specify.row; j < excelData.list[i].list.Count && j <= specify2.row; j++)
					{
						param.list.Add(excelData.list[i].list[j]);
					}
				}
				excelParams.Add(param);
			}
		}
		return excelParams;
	}

	public static List<ExcelData.Param> LoadExcelDataAlFindlFile(string _strAssetPath, string _strFileName, int sCell, int sRow, int eCell, int eRow, List<string> _OmitFolderName = null, bool _isWarning = true)
	{
		lstABName.Clear();
		lstABName = GetAssetBundleNameListFromPath(_strAssetPath);
		lstABName.Sort();
		for (int i = 0; i < lstABName.Count; i++)
		{
			strNo.Clear();
			strNo.Append(Path.GetFileNameWithoutExtension(lstABName[i]));
			strNo.Replace(strNo.ToString(), YS_Assist.GetStringRight(strNo.ToString(), 2));
			if (_OmitFolderName != null && _OmitFolderName.Contains(strNo.ToString()))
			{
				continue;
			}
			string[] allAssetName = AssetBundleCheck.GetAllAssetName(lstABName[i], _WithExtension: false);
			bool flag = false;
			for (int j = 0; j < allAssetName.Length; j++)
			{
				if (allAssetName[j].Compare(_strFileName, ignoreCase: true))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				DebugLog("[" + lstABName[i] + "][" + _strFileName + "]は見つかりません", 1);
			}
			else
			{
				List<ExcelData.Param> list = LoadExcelData(lstABName[i], _strFileName, sCell, sRow, eCell, eRow, _isWarning);
				if (list != null)
				{
					return list;
				}
			}
		}
		return null;
	}

	public static T LoadAllFolderInOneFile<T>(string _findFolder, string _strLoadFile, List<string> _OmitFolderName = null) where T : UnityEngine.Object
	{
		lstABName.Clear();
		lstABName = GetAssetBundleNameListFromPath(_findFolder);
		lstABName.Sort();
		for (int i = 0; i < lstABName.Count; i++)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(lstABName[i]);
			fileNameWithoutExtension = YS_Assist.GetStringRight(fileNameWithoutExtension, 2);
			if (_OmitFolderName == null || !_OmitFolderName.Contains(fileNameWithoutExtension))
			{
				bool flag = false;
				if (AssetFileExist(lstABName[i].ToString(), _strLoadFile, string.Empty))
				{
					T result = CommonLib.LoadAsset<T>(lstABName[i], _strLoadFile, clone: false, string.Empty);
					AssetBundleManager.UnloadAssetBundle(lstABName[i], isUnloadForceRefCount: true);
					return result;
				}
			}
		}
		return (T)null;
	}

	public static List<T> LoadAllFolder<T>(string _findFolder, string _strLoadFile, List<string> _OmitFolderName = null) where T : UnityEngine.Object
	{
		List<T> list = new List<T>();
		lstABName.Clear();
		lstABName = GetAssetBundleNameListFromPath(_findFolder);
		lstABName.Sort();
		for (int i = 0; i < lstABName.Count; i++)
		{
			string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(lstABName[i]);
			fileNameWithoutExtension = YS_Assist.GetStringRight(fileNameWithoutExtension, 2);
			if (_OmitFolderName != null && _OmitFolderName.Contains(fileNameWithoutExtension))
			{
				continue;
			}
			string[] allAssetName = AssetBundleCheck.GetAllAssetName(lstABName[i], _WithExtension: false);
			bool flag = false;
			for (int j = 0; j < allAssetName.Length; j++)
			{
				if (allAssetName[j].Compare(_strLoadFile, ignoreCase: true))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				DebugLog("[" + lstABName[i] + "][" + _strLoadFile + "]は見つかりません", 1);
			}
			else
			{
				T val = CommonLib.LoadAsset<T>(lstABName[i], _strLoadFile, clone: false, string.Empty);
				AssetBundleManager.UnloadAssetBundle(lstABName[i], isUnloadForceRefCount: true);
				if ((bool)val)
				{
					list.Add(val);
				}
			}
		}
		return list;
	}

	public static bool CheckFlagsArray(bool[] flags, int _check = 0)
	{
		if (flags.Length == 0)
		{
			return false;
		}
		bool flag = _check == 0;
		foreach (bool flag2 in flags)
		{
			if ((_check != 0) ? flag2 : (!flag2))
			{
				return !flag;
			}
		}
		return flag;
	}

	public static List<string> GetAssetBundleNameListFromPath(string path, bool subdirCheck = false)
	{
		List<string> result = new List<string>();
		if (!AssetBundleCheck.IsSimulation)
		{
			string path2 = AssetBundleManager.BaseDownloadingURL + path;
			if (subdirCheck)
			{
				List<string> list = new List<string>();
				CommonLib.GetAllFiles(path2, "*.unity3d", list);
				result = list.Select((string s) => s.Replace(AssetBundleManager.BaseDownloadingURL, string.Empty)).ToList();
			}
			else
			{
				if (!Directory.Exists(path2))
				{
					return result;
				}
				result = (from s in Directory.GetFiles(path2, "*.unity3d")
					select s.Replace(AssetBundleManager.BaseDownloadingURL, string.Empty)).ToList();
			}
		}
		return result;
	}

	public static bool StartsWith(string a, string b)
	{
		int length = a.Length;
		int length2 = b.Length;
		int num = 0;
		int num2 = 0;
		while (num < length && num2 < length2 && a[num] == b[num2])
		{
			num++;
			num2++;
		}
		return (num2 == length2 && length >= length2) || (num == length && length2 >= length);
	}

	public static bool AssetFileExist(string path, string targetName, string manifest = "")
	{
		bool result = false;
		if (path.IsNullOrEmpty())
		{
			return result;
		}
		if (!AssetBundleCheck.IsFile(path, targetName))
		{
			return result;
		}
		string[] allAssetName = AssetBundleCheck.GetAllAssetName(path, _WithExtension: false, manifest);
		for (int i = 0; i < allAssetName.Length; i++)
		{
			if (allAssetName[i].Compare(targetName, ignoreCase: true))
			{
				result = true;
				break;
			}
		}
		return result;
	}
}
