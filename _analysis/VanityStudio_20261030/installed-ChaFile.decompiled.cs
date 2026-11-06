using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Localize.Translate;
using MessagePack;

public class ChaFile
{
	public class OutputData
	{
		public int productNo { get; set; } = -1;

		public int tagIndex { get; set; } = -1;

		public ChaFileAbout about { get; set; }
	}

	public int loadProductNo;

	public Version loadVersion = new Version(ChaFileDefine.ChaFileVersion.ToString());

	public byte[] pngData;

	public byte[] facePngData;

	public ChaFileCustom custom;

	public ChaFileParameter parameter;

	public ChaFileStatus status;

	public ChaFileAbout about;

	private int lastLoadErrorCode;

	public string charaFileName { get; protected set; }

	public ChaFileCoordinate[] coordinate { get; set; }

	public int GetLastErrorCode()
	{
		return lastLoadErrorCode;
	}

	public ChaFile()
	{
		custom = new ChaFileCustom();
		coordinate = new ChaFileCoordinate[Enum.GetNames(typeof(ChaFileDefine.CoordinateType)).Length];
		for (int i = 0; i < coordinate.Length; i++)
		{
			coordinate[i] = new ChaFileCoordinate();
		}
		parameter = new ChaFileParameter();
		status = new ChaFileStatus();
		about = new ChaFileAbout();
		lastLoadErrorCode = 0;
	}

	protected bool SaveFile(string path)
	{
		string directoryName = Path.GetDirectoryName(path);
		if (!Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		charaFileName = Path.GetFileName(path);
		using FileStream st = new FileStream(path, FileMode.Create, FileAccess.Write);
		return SaveFile(st, savePng: true);
	}

	protected bool SaveFile(Stream st, bool savePng)
	{
		using BinaryWriter bw = new BinaryWriter(st);
		return SaveFile(bw, savePng);
	}

	protected bool SaveFile(BinaryWriter bw, bool savePng)
	{
		if (savePng && pngData != null)
		{
			bw.Write(pngData);
		}
		bw.Write(100);
		bw.Write("【KoiKatuCharaSun】");
		bw.Write(ChaFileDefine.ChaFileVersion.ToString());
		int num = 0;
		if (facePngData != null)
		{
			num = facePngData.Length;
		}
		bw.Write(num);
		if (num != 0)
		{
			bw.Write(facePngData);
		}
		byte[] customBytes = GetCustomBytes();
		byte[] coordinateBytes = GetCoordinateBytes();
		byte[] parameterBytes = GetParameterBytes();
		byte[] statusBytes = GetStatusBytes();
		if (Localize.Translate.Manager.initialized)
		{
			about.language = Localize.Translate.Manager.Language;
		}
		byte[] aboutBytes = GetAboutBytes();
		int num2 = 4;
		long num3 = 0L;
		num2++;
		string[] array = new string[5]
		{
			ChaFileCustom.BlockName,
			ChaFileCoordinate.BlockName,
			ChaFileParameter.BlockName,
			ChaFileStatus.BlockName,
			ChaFileAbout.BlockName
		};
		string[] array2 = new string[5]
		{
			ChaFileDefine.ChaFileCustomVersion.ToString(),
			ChaFileDefine.ChaFileCoordinateVersion.ToString(),
			ChaFileDefine.ChaFileParameterVersion.ToString(),
			ChaFileDefine.ChaFileStatusVersion.ToString(),
			ChaFileDefine.ChaFileAboutVersion.ToString()
		};
		long[] array3 = new long[num2];
		int num4 = 0;
		array3[num4++] = ((customBytes != null) ? customBytes.Length : 0);
		array3[num4++] = ((coordinateBytes != null) ? coordinateBytes.Length : 0);
		array3[num4++] = ((parameterBytes != null) ? parameterBytes.Length : 0);
		array3[num4++] = ((statusBytes != null) ? statusBytes.Length : 0);
		array3[num4++] = ((aboutBytes != null) ? aboutBytes.Length : 0);
		long[] array4 = new long[num2];
		long num5 = (array4[0] = num3);
		for (int i = 1; i < num2; i++)
		{
			num5 = (array4[i] = num5 + array3[i - 1]);
		}
		BlockHeader blockHeader = new BlockHeader();
		for (int j = 0; j < num2; j++)
		{
			BlockHeader.Info item = new BlockHeader.Info
			{
				name = array[j],
				version = array2[j],
				size = array3[j],
				pos = array4[j]
			};
			blockHeader.lstInfo.Add(item);
		}
		byte[] array5 = MessagePackSerializer.Serialize(blockHeader);
		bw.Write(array5.Length);
		bw.Write(array5);
		long num6 = 0L;
		long[] array6 = array3;
		foreach (long num7 in array6)
		{
			num6 += num7;
		}
		bw.Write(num6);
		bw.Write(customBytes);
		bw.Write(coordinateBytes);
		bw.Write(parameterBytes);
		bw.Write(statusBytes);
		bw.Write(aboutBytes);
		return true;
	}

	public static OutputData CheckData(string path)
	{
		OutputData outputData = new OutputData();
		if (!File.Exists(path))
		{
			return outputData;
		}
		using FileStream input = new FileStream(path, FileMode.Open, FileAccess.Read);
		using BinaryReader binaryReader = new BinaryReader(input);
		long pngSize = PngFile.GetPngSize(binaryReader);
		if (pngSize != 0L)
		{
			binaryReader.BaseStream.Seek(pngSize, SeekOrigin.Current);
			if (binaryReader.BaseStream.Length - binaryReader.BaseStream.Position == 0L)
			{
				return outputData;
			}
		}
		try
		{
			outputData.productNo = binaryReader.ReadInt32();
			if (outputData.productNo != 100)
			{
				return outputData;
			}
			string text = binaryReader.ReadString();
			if (text == "【KoiKatuCharaSun】")
			{
				outputData.tagIndex = 0;
			}
			string[] koikatsuCharaFileMarkCheck = ChaFileDefine.KoikatsuCharaFileMarkCheck;
			for (int i = 0; i < koikatsuCharaFileMarkCheck.Length; i++)
			{
				if (text == koikatsuCharaFileMarkCheck[i])
				{
					outputData.tagIndex = i + 1;
					break;
				}
			}
			if (outputData.tagIndex == -1)
			{
				return outputData;
			}
			Version value = new Version(binaryReader.ReadString());
			if (0 > ChaFileDefine.ChaFileVersion.CompareTo(value))
			{
				return outputData;
			}
			int num = binaryReader.ReadInt32();
			if (num != 0)
			{
				binaryReader.ReadBytes(num);
			}
			int count = binaryReader.ReadInt32();
			BlockHeader blockHeader = MessagePackSerializer.Deserialize<BlockHeader>(binaryReader.ReadBytes(count));
			binaryReader.ReadInt64();
			long position = binaryReader.BaseStream.Position;
			BlockHeader.Info info = blockHeader.SearchInfo(ChaFileAbout.BlockName);
			if (info != null)
			{
				Version value2 = new Version(info.version);
				if (0 <= ChaFileDefine.ChaFileAboutVersion.CompareTo(value2))
				{
					binaryReader.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] bytes = binaryReader.ReadBytes((int)info.size);
					outputData.about = MessagePackSerializer.Deserialize<ChaFileAbout>(bytes);
					outputData.about.ComplementWithVersion();
				}
			}
			return outputData;
		}
		catch (EndOfStreamException)
		{
			return outputData;
		}
	}

	protected bool LoadFile(string path, bool noLoadPNG = false, bool noLoadStatus = true)
	{
		if (!File.Exists(path))
		{
			lastLoadErrorCode = -6;
			return false;
		}
		charaFileName = Path.GetFileName(path);
		using FileStream st = new FileStream(path, FileMode.Open, FileAccess.Read);
		return LoadFile(st, noLoadPNG, noLoadStatus);
	}

	protected bool LoadFile(Stream st, bool noLoadPNG = false, bool noLoadStatus = true)
	{
		using BinaryReader br = new BinaryReader(st);
		return LoadFile(br, noLoadPNG, noLoadStatus);
	}

	protected bool LoadFile(BinaryReader br, bool noLoadPNG = false, bool noLoadStatus = true)
	{
		long pngSize = PngFile.GetPngSize(br);
		if (pngSize != 0L)
		{
			if (noLoadPNG)
			{
				br.BaseStream.Seek(pngSize, SeekOrigin.Current);
			}
			else
			{
				pngData = br.ReadBytes((int)pngSize);
			}
			if (br.BaseStream.Length - br.BaseStream.Position == 0L)
			{
				lastLoadErrorCode = -5;
				return false;
			}
		}
		try
		{
			loadProductNo = br.ReadInt32();
			if (loadProductNo > 100)
			{
				lastLoadErrorCode = -3;
				return false;
			}
			if (br.ReadString() != "【KoiKatuCharaSun】")
			{
				lastLoadErrorCode = -1;
				return false;
			}
			loadVersion = new Version(br.ReadString());
			if (0 > ChaFileDefine.ChaFileVersion.CompareTo(loadVersion))
			{
				lastLoadErrorCode = -2;
				return false;
			}
			int num = br.ReadInt32();
			if (num != 0)
			{
				facePngData = br.ReadBytes(num);
			}
			int count = br.ReadInt32();
			BlockHeader blockHeader = MessagePackSerializer.Deserialize<BlockHeader>(br.ReadBytes(count));
			long num2 = br.ReadInt64();
			long position = br.BaseStream.Position;
			BlockHeader.Info info = blockHeader.SearchInfo(ChaFileCustom.BlockName);
			if (info != null)
			{
				Version version = new Version(info.version);
				if (0 > ChaFileDefine.ChaFileCustomVersion.CompareTo(version))
				{
					lastLoadErrorCode = -2;
				}
				else
				{
					br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] data = br.ReadBytes((int)info.size);
					SetCustomBytes(data, version);
				}
			}
			info = blockHeader.SearchInfo(ChaFileCoordinate.BlockName);
			if (info != null)
			{
				Version version2 = new Version(info.version);
				if (0 > ChaFileDefine.ChaFileCoordinateVersion.CompareTo(version2))
				{
					lastLoadErrorCode = -2;
				}
				else
				{
					br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] data2 = br.ReadBytes((int)info.size);
					SetCoordinateBytes(data2, version2);
				}
			}
			info = blockHeader.SearchInfo(ChaFileParameter.BlockName);
			if (info != null)
			{
				Version value = new Version(info.version);
				if (0 > ChaFileDefine.ChaFileParameterVersion.CompareTo(value))
				{
					lastLoadErrorCode = -2;
				}
				else
				{
					br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] parameterBytes = br.ReadBytes((int)info.size);
					SetParameterBytes(parameterBytes);
				}
			}
			if (!noLoadStatus)
			{
				info = blockHeader.SearchInfo(ChaFileStatus.BlockName);
				if (info != null)
				{
					Version value2 = new Version(info.version);
					if (0 > ChaFileDefine.ChaFileStatusVersion.CompareTo(value2))
					{
						lastLoadErrorCode = -2;
					}
					else
					{
						br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
						byte[] statusBytes = br.ReadBytes((int)info.size);
						SetStatusBytes(statusBytes);
					}
				}
			}
			info = blockHeader.SearchInfo(ChaFileAbout.BlockName);
			if (info != null)
			{
				Version value3 = new Version(info.version);
				if (0 > ChaFileDefine.ChaFileAboutVersion.CompareTo(value3))
				{
					lastLoadErrorCode = -2;
				}
				else
				{
					br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] aboutBytes = br.ReadBytes((int)info.size);
					SetAboutBytes(aboutBytes);
				}
			}
			br.BaseStream.Seek(position + num2, SeekOrigin.Begin);
		}
		catch (EndOfStreamException)
		{
			lastLoadErrorCode = -999;
			return false;
		}
		lastLoadErrorCode = 0;
		return true;
	}

	protected bool LoadFileKoikatsu(BinaryReader br, bool noLoadPNG = false, bool noLoadStatus = true)
	{
		long pngSize = PngFile.GetPngSize(br);
		if (pngSize != 0L)
		{
			if (noLoadPNG)
			{
				br.BaseStream.Seek(pngSize, SeekOrigin.Current);
			}
			else
			{
				pngData = br.ReadBytes((int)pngSize);
			}
			if (br.BaseStream.Length - br.BaseStream.Position == 0L)
			{
				lastLoadErrorCode = -5;
				return false;
			}
		}
		try
		{
			loadProductNo = br.ReadInt32();
			if (loadProductNo > 100)
			{
				lastLoadErrorCode = -3;
				return false;
			}
			string value = br.ReadString();
			if (!ChaFileDefine.KoikatsuCharaFileMarkCheck.Contains(value))
			{
				lastLoadErrorCode = -1;
				return false;
			}
			loadVersion = new Version(br.ReadString());
			if (0 > ChaFileDefine.ChaFileVersion.CompareTo(loadVersion))
			{
				lastLoadErrorCode = -2;
				return false;
			}
			int num = br.ReadInt32();
			if (num != 0)
			{
				facePngData = br.ReadBytes(num);
			}
			int count = br.ReadInt32();
			BlockHeader blockHeader = MessagePackSerializer.Deserialize<BlockHeader>(br.ReadBytes(count));
			long num2 = br.ReadInt64();
			long position = br.BaseStream.Position;
			BlockHeader.Info info = blockHeader.SearchInfo(ChaFileCustom.BlockName);
			if (info != null)
			{
				Version version = new Version(info.version);
				if (0 > ChaFileDefine.ChaFileCustomVersion.CompareTo(version))
				{
					lastLoadErrorCode = -2;
				}
				else
				{
					br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] data = br.ReadBytes((int)info.size);
					SetCustomBytes(data, version);
				}
			}
			info = blockHeader.SearchInfo(ChaFileCoordinate.BlockName);
			if (info != null)
			{
				Version version2 = new Version(info.version);
				if (0 > ChaFileDefine.ChaFileCoordinateVersion.CompareTo(version2))
				{
					lastLoadErrorCode = -2;
				}
				else
				{
					br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] data2 = br.ReadBytes((int)info.size);
					SetCoordinateBytes(data2, version2);
				}
			}
			info = blockHeader.SearchInfo(ChaFileParameter.BlockName);
			if (info != null)
			{
				Version value2 = new Version(info.version);
				if (0 > ChaFileDefine.ChaFileParameterVersion.CompareTo(value2))
				{
					lastLoadErrorCode = -2;
				}
				else
				{
					br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
					byte[] parameterBytes = br.ReadBytes((int)info.size);
					SetParameterBytes(parameterBytes);
				}
			}
			if (!noLoadStatus)
			{
				info = blockHeader.SearchInfo(ChaFileStatus.BlockName);
				if (info != null)
				{
					Version value3 = new Version(info.version);
					if (0 > ChaFileDefine.ChaFileStatusVersion.CompareTo(value3))
					{
						lastLoadErrorCode = -2;
					}
					else
					{
						br.BaseStream.Seek(position + info.pos, SeekOrigin.Begin);
						byte[] statusBytes = br.ReadBytes((int)info.size);
						SetStatusBytes(statusBytes);
					}
				}
			}
			br.BaseStream.Seek(position + num2, SeekOrigin.Begin);
		}
		catch (EndOfStreamException)
		{
			lastLoadErrorCode = -999;
			return false;
		}
		lastLoadErrorCode = 0;
		return true;
	}

	public bool AssignCoordinate(ChaFileDefine.CoordinateType type, ChaFileCoordinate srcCoorde)
	{
		if (srcCoorde == null)
		{
			return false;
		}
		byte[] data = srcCoorde.SaveBytes();
		coordinate[(int)type].LoadBytes(data, srcCoorde.loadVersion);
		return true;
	}

	public byte[] GetCustomBytes()
	{
		return GetCustomBytes(custom);
	}

	public static byte[] GetCustomBytes(ChaFileCustom _custom)
	{
		return _custom.SaveBytes();
	}

	public byte[] GetCoordinateBytes()
	{
		return GetCoordinateBytes(coordinate);
	}

	public static byte[] GetCoordinateBytes(ChaFileCoordinate[] _coordinate)
	{
		if (_coordinate.Length == 0)
		{
			return null;
		}
		List<byte[]> list = new List<byte[]>();
		foreach (ChaFileCoordinate chaFileCoordinate in _coordinate)
		{
			list.Add(chaFileCoordinate.SaveBytes());
		}
		return MessagePackSerializer.Serialize(list);
	}

	public byte[] GetParameterBytes()
	{
		return GetParameterBytes(parameter);
	}

	public static byte[] GetParameterBytes(ChaFileParameter _parameter)
	{
		return MessagePackSerializer.Serialize(_parameter);
	}

	public byte[] GetStatusBytes()
	{
		return GetStatusBytes(status);
	}

	public static byte[] GetStatusBytes(ChaFileStatus _status)
	{
		return MessagePackSerializer.Serialize(_status);
	}

	public byte[] GetAboutBytes()
	{
		return GetAboutBytes(about);
	}

	public static byte[] GetAboutBytes(ChaFileAbout _about)
	{
		return MessagePackSerializer.Serialize(_about);
	}

	public void SetCustomBytes(byte[] data, Version ver)
	{
		custom.LoadBytes(data, ver);
	}

	public void SetCoordinateBytes(byte[] data, Version ver)
	{
		List<byte[]> list = MessagePackSerializer.Deserialize<List<byte[]>>(data);
		for (int i = 0; i < coordinate.Length && list.Count > i; i++)
		{
			coordinate[i].LoadBytes(list[i], ver);
		}
	}

	public void SetParameterBytes(byte[] data)
	{
		ChaFileParameter chaFileParameter = MessagePackSerializer.Deserialize<ChaFileParameter>(data);
		chaFileParameter.ComplementWithVersion();
		parameter.Copy(chaFileParameter);
	}

	public void SetStatusBytes(byte[] data)
	{
		ChaFileStatus chaFileStatus = MessagePackSerializer.Deserialize<ChaFileStatus>(data);
		chaFileStatus.ComplementWithVersion();
		status.Copy(chaFileStatus);
	}

	public void SetAboutBytes(byte[] data)
	{
		ChaFileAbout chaFileAbout = MessagePackSerializer.Deserialize<ChaFileAbout>(data);
		chaFileAbout.ComplementWithVersion();
		about.Copy(chaFileAbout);
	}

	public static void CopyChaFile(ChaFile dst, ChaFile src)
	{
		dst.CopyAll(src);
	}

	public void CopyAll(ChaFile _chafile)
	{
		CopyCustom(_chafile.custom);
		CopyCoordinate(_chafile.coordinate);
		CopyParameter(_chafile.parameter);
		CopyStatus(_chafile.status);
		CopyAbout(_chafile.about);
	}

	public void CopyCustom(ChaFileCustom _custom)
	{
		custom.Copy(_custom);
	}

	public void CopyCoordinate(ChaFileCoordinate[] _coordinate)
	{
		byte[] coordinateBytes = GetCoordinateBytes(_coordinate);
		SetCoordinateBytes(coordinateBytes, ChaFileDefine.ChaFileCoordinateVersion);
	}

	public void CopyParameter(ChaFileParameter _parameter)
	{
		parameter.Copy(_parameter);
	}

	public void CopyStatus(ChaFileStatus _status)
	{
		status.Copy(_status);
	}

	public void CopyAbout(ChaFileAbout _about)
	{
		about.Copy(_about);
	}
}
