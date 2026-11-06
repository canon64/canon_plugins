using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class AnimationKeyInfo
{
	public class AnmKeyInfo
	{
		public int no;

		public Vector3 pos;

		public Vector3 rot;

		public Vector3 scl;

		public void Set(int _no, Vector3 _pos, Vector3 _rot, Vector3 _scl)
		{
			no = _no;
			pos = _pos;
			rot = _rot;
			scl = _scl;
		}

		public string GetInfoStr()
		{
			StringBuilder stringBuilder = new StringBuilder(128);
			stringBuilder.Append(no.ToString()).Append("\t");
			stringBuilder.Append(pos.ToString("f7")).Append("\t");
			stringBuilder.Append(rot.ToString("f7")).Append("\t");
			stringBuilder.Append(scl.ToString("f7"));
			return stringBuilder.ToString();
		}
	}

	private Dictionary<string, List<AnmKeyInfo>> dictInfo = new Dictionary<string, List<AnmKeyInfo>>();

	public int GetKeyCount()
	{
		if (dictInfo == null)
		{
			return 0;
		}
		if (dictInfo.Count == 0)
		{
			return 0;
		}
		return dictInfo.Values.ToList()[0].Count;
	}

	public bool CreateInfo(int start, int end, GameObject obj, string[] usename)
	{
		if (null == obj)
		{
			return false;
		}
		Animator component = obj.GetComponent<Animator>();
		if (null == component)
		{
			return false;
		}
		dictInfo.Clear();
		float num = 1f / (float)(end - start);
		int num2 = end - start + 1;
		for (int i = 0; i < num2; i++)
		{
			float normalizedTime = 0f + num * (float)i;
			component.Play(component.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, normalizedTime);
			component.Update(0f);
			CreateInfoLoop(i, obj.transform, usename);
		}
		return true;
	}

	private void CreateInfoLoop(int no, Transform tf, string[] usename)
	{
		if (null == tf)
		{
			return;
		}
		List<AnmKeyInfo> value = null;
		bool flag = false;
		if (usename != null && -1 == Array.IndexOf(usename, tf.name))
		{
			flag = true;
		}
		if (!flag)
		{
			if (!dictInfo.TryGetValue(tf.name, out value))
			{
				value = new List<AnmKeyInfo>();
				dictInfo[tf.name] = value;
			}
			AnmKeyInfo anmKeyInfo = new AnmKeyInfo();
			anmKeyInfo.Set(no, tf.localPosition, tf.localEulerAngles, tf.localScale);
			value.Add(anmKeyInfo);
		}
		for (int i = 0; i < tf.childCount; i++)
		{
			Transform child = tf.GetChild(i);
			CreateInfoLoop(no, child, usename);
		}
	}

	public bool GetInfo(string name, float rate, ref Vector3 value, byte type)
	{
		List<AnmKeyInfo> value2 = null;
		if (!dictInfo.TryGetValue(name, out value2))
		{
			return false;
		}
		if (type == 0)
		{
			if (0f == rate)
			{
				value = value2[0].pos;
			}
			else if (1f == rate)
			{
				value = value2[value2.Count - 1].pos;
			}
			else
			{
				float num = (float)(value2.Count - 1) * rate;
				int num2 = Mathf.FloorToInt(num);
				float t = num - (float)num2;
				value = Vector3.Lerp(value2[num2].pos, value2[num2 + 1].pos, t);
			}
		}
		else if (1 == type)
		{
			if (0f == rate)
			{
				value = value2[0].rot;
			}
			else if (1f == rate)
			{
				value = value2[value2.Count - 1].rot;
			}
			else
			{
				float num3 = (float)(value2.Count - 1) * rate;
				int num4 = Mathf.FloorToInt(num3);
				float t2 = num3 - (float)num4;
				value.x = Mathf.LerpAngle(value2[num4].rot.x, value2[num4 + 1].rot.x, t2);
				value.y = Mathf.LerpAngle(value2[num4].rot.y, value2[num4 + 1].rot.y, t2);
				value.z = Mathf.LerpAngle(value2[num4].rot.z, value2[num4 + 1].rot.z, t2);
			}
		}
		else if (0f == rate)
		{
			value = value2[0].scl;
		}
		else if (1f == rate)
		{
			value = value2[value2.Count - 1].scl;
		}
		else
		{
			float num5 = (float)(value2.Count - 1) * rate;
			int num6 = Mathf.FloorToInt(num5);
			float t3 = num5 - (float)num6;
			value = Vector3.Lerp(value2[num6].scl, value2[num6 + 1].scl, t3);
		}
		return true;
	}

	public bool GetInfo(string name, int key, ref Vector3 value, byte type)
	{
		List<AnmKeyInfo> value2 = null;
		if (!dictInfo.TryGetValue(name, out value2))
		{
			return false;
		}
		if (value2.Count <= key)
		{
			return false;
		}
		if (type == 0)
		{
			value = value2[key].pos;
		}
		else if (1 == type)
		{
			value = value2[key].rot;
		}
		else
		{
			value = value2[key].scl;
		}
		return true;
	}

	public bool GetInfo(string name, float rate, ref Vector3 pos, ref Vector3 rot, ref Vector3 scale, bool[] flag)
	{
		if (3 != flag.Length)
		{
			return false;
		}
		List<AnmKeyInfo> value = null;
		if (!dictInfo.TryGetValue(name, out value))
		{
			return false;
		}
		if (flag[0])
		{
			if (0f == rate)
			{
				pos = value[0].pos;
			}
			else if (1f == rate)
			{
				pos = value[value.Count - 1].pos;
			}
			else
			{
				float num = (float)(value.Count - 1) * rate;
				int num2 = Mathf.FloorToInt(num);
				float t = num - (float)num2;
				pos = Vector3.Lerp(value[num2].pos, value[num2 + 1].pos, t);
			}
		}
		if (flag[1])
		{
			if (0f == rate)
			{
				rot = value[0].rot;
			}
			else if (1f == rate)
			{
				rot = value[value.Count - 1].rot;
			}
			else
			{
				float num3 = (float)(value.Count - 1) * rate;
				int num4 = Mathf.FloorToInt(num3);
				float t2 = num3 - (float)num4;
				rot.x = Mathf.LerpAngle(value[num4].rot.x, value[num4 + 1].rot.x, t2);
				rot.y = Mathf.LerpAngle(value[num4].rot.y, value[num4 + 1].rot.y, t2);
				rot.z = Mathf.LerpAngle(value[num4].rot.z, value[num4 + 1].rot.z, t2);
			}
		}
		if (flag[2])
		{
			if (0f == rate)
			{
				scale = value[0].scl;
			}
			else if (1f == rate)
			{
				scale = value[value.Count - 1].scl;
			}
			else
			{
				float num5 = (float)(value.Count - 1) * rate;
				int num6 = Mathf.FloorToInt(num5);
				float t3 = num5 - (float)num6;
				scale = Vector3.Lerp(value[num6].scl, value[num6 + 1].scl, t3);
			}
		}
		return true;
	}

	public bool GetInfo(string name, int key, ref Vector3 pos, ref Vector3 rot, ref Vector3 scale, bool[] flag)
	{
		if (3 != flag.Length)
		{
			return false;
		}
		List<AnmKeyInfo> value = null;
		if (!dictInfo.TryGetValue(name, out value))
		{
			return false;
		}
		if (value.Count <= key)
		{
			return false;
		}
		if (flag[0])
		{
			pos = value[key].pos;
		}
		if (flag[1])
		{
			rot = value[key].rot;
		}
		if (flag[2])
		{
			scale = value[key].scl;
		}
		return true;
	}

	public void SaveInfo(string filepath)
	{
		using FileStream output = new FileStream(filepath, FileMode.Create, FileAccess.Write);
		using BinaryWriter binaryWriter = new BinaryWriter(output);
		int count = dictInfo.Count;
		binaryWriter.Write(count);
		foreach (KeyValuePair<string, List<AnmKeyInfo>> item in dictInfo)
		{
			binaryWriter.Write(item.Key);
			binaryWriter.Write(item.Value.Count);
			for (int i = 0; i < item.Value.Count; i++)
			{
				binaryWriter.Write(item.Value[i].no);
				binaryWriter.Write(item.Value[i].pos.x);
				binaryWriter.Write(item.Value[i].pos.y);
				binaryWriter.Write(item.Value[i].pos.z);
				binaryWriter.Write(item.Value[i].rot.x);
				binaryWriter.Write(item.Value[i].rot.y);
				binaryWriter.Write(item.Value[i].rot.z);
				binaryWriter.Write(item.Value[i].scl.x);
				binaryWriter.Write(item.Value[i].scl.y);
				binaryWriter.Write(item.Value[i].scl.z);
			}
		}
	}

	public void LoadInfo(string filePath)
	{
		using FileStream st = new FileStream(filePath, FileMode.Open, FileAccess.Read);
		LoadInfo(st);
	}

	public void LoadInfo(string manifest, string assetBundleName, string assetName, Action<string, string> funcAssetBundleEntry = null)
	{
		if (AssetBundleCheck.IsSimulation)
		{
			manifest = "";
		}
		if (!AssetBundleCheck.IsFile(assetBundleName, assetName))
		{
			_ = "読み込みエラー\r\nassetBundleName：" + assetBundleName + "\tassetName：" + assetName;
			return;
		}
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(assetBundleName, assetName, typeof(TextAsset), manifest);
		if (assetBundleLoadAssetOperation == null)
		{
			_ = "読み込みエラー\r\nassetName：" + assetName;
			return;
		}
		TextAsset asset = assetBundleLoadAssetOperation.GetAsset<TextAsset>();
		if (!(null == asset))
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				memoryStream.Write(asset.bytes, 0, asset.bytes.Length);
				memoryStream.Seek(0L, SeekOrigin.Begin);
				LoadInfo(memoryStream);
			}
			if (funcAssetBundleEntry == null)
			{
				AssetBundleManager.UnloadAssetBundle(assetBundleName, isUnloadForceRefCount: true);
			}
			else
			{
				funcAssetBundleEntry(assetBundleName, "");
			}
		}
	}

	public void LoadInfo(Stream st)
	{
		using BinaryReader binaryReader = new BinaryReader(st);
		int num = binaryReader.ReadInt32();
		dictInfo.Clear();
		for (int i = 0; i < num; i++)
		{
			List<AnmKeyInfo> list = new List<AnmKeyInfo>();
			string key = binaryReader.ReadString();
			dictInfo[key] = list;
			int num2 = binaryReader.ReadInt32();
			for (int j = 0; j < num2; j++)
			{
				AnmKeyInfo anmKeyInfo = new AnmKeyInfo();
				anmKeyInfo.no = binaryReader.ReadInt32();
				anmKeyInfo.pos.x = binaryReader.ReadSingle();
				anmKeyInfo.pos.y = binaryReader.ReadSingle();
				anmKeyInfo.pos.z = binaryReader.ReadSingle();
				anmKeyInfo.rot.x = binaryReader.ReadSingle();
				anmKeyInfo.rot.y = binaryReader.ReadSingle();
				anmKeyInfo.rot.z = binaryReader.ReadSingle();
				anmKeyInfo.scl.x = binaryReader.ReadSingle();
				anmKeyInfo.scl.y = binaryReader.ReadSingle();
				anmKeyInfo.scl.z = binaryReader.ReadSingle();
				list.Add(anmKeyInfo);
			}
		}
	}

	public void OutputText(string outputPath)
	{
		StringBuilder stringBuilder = new StringBuilder(2048);
		stringBuilder.Length = 0;
		foreach (KeyValuePair<string, List<AnmKeyInfo>> item in dictInfo)
		{
			for (int i = 0; i < item.Value.Count; i++)
			{
				stringBuilder.Append(item.Key).Append("\t");
				stringBuilder.Append(item.Value[i].GetInfoStr());
				stringBuilder.Append("\n");
			}
		}
		using FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
		using StreamWriter streamWriter = new StreamWriter(stream, Encoding.UTF8);
		streamWriter.Write(stringBuilder.ToString());
		streamWriter.Write("\n");
	}
}
