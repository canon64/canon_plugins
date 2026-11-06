using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.Serialization.Utilities.Unsafe;

namespace Sirenix.Serialization;

public class BinaryDataReader : BaseDataReader
{
	private static readonly Dictionary<Type, Delegate> PrimitiveFromByteMethods = new Dictionary<Type, Delegate>
	{
		{
			typeof(char),
			(Func<byte[], int, char>)((byte[] b, int i) => (char)ProperBitConverter.ToUInt16(b, i))
		},
		{
			typeof(byte),
			(Func<byte[], int, byte>)((byte[] b, int i) => b[i])
		},
		{
			typeof(sbyte),
			(Func<byte[], int, sbyte>)((byte[] b, int i) => (sbyte)b[i])
		},
		{
			typeof(bool),
			(Func<byte[], int, bool>)((byte[] b, int i) => (b[i] != 0) ? true : false)
		},
		{
			typeof(short),
			new Func<byte[], int, short>(ProperBitConverter.ToInt16)
		},
		{
			typeof(int),
			new Func<byte[], int, int>(ProperBitConverter.ToInt32)
		},
		{
			typeof(long),
			new Func<byte[], int, long>(ProperBitConverter.ToInt64)
		},
		{
			typeof(ushort),
			new Func<byte[], int, ushort>(ProperBitConverter.ToUInt16)
		},
		{
			typeof(uint),
			new Func<byte[], int, uint>(ProperBitConverter.ToUInt32)
		},
		{
			typeof(ulong),
			new Func<byte[], int, ulong>(ProperBitConverter.ToUInt64)
		},
		{
			typeof(decimal),
			new Func<byte[], int, decimal>(ProperBitConverter.ToDecimal)
		},
		{
			typeof(float),
			new Func<byte[], int, float>(ProperBitConverter.ToSingle)
		},
		{
			typeof(double),
			new Func<byte[], int, double>(ProperBitConverter.ToDouble)
		},
		{
			typeof(Guid),
			new Func<byte[], int, Guid>(ProperBitConverter.ToGuid)
		}
	};

	private readonly byte[] buffer = new byte[16];

	private EntryType? peekedEntryType;

	private BinaryEntryType peekedBinaryEntryType;

	private string peekedEntryName;

	private Dictionary<int, Type> types = new Dictionary<int, Type>(16);

	public BinaryDataReader()
		: base(null, null)
	{
	}

	public BinaryDataReader(Stream stream, DeserializationContext context)
		: base(stream, context)
	{
	}

	public override void Dispose()
	{
	}

	public override EntryType PeekEntry(out string name)
	{
		if (peekedEntryType.HasValue)
		{
			name = peekedEntryName;
			return peekedEntryType.Value;
		}
		int num = Stream.ReadByte();
		if (num < 0)
		{
			name = null;
			peekedEntryName = null;
			peekedEntryType = EntryType.EndOfStream;
			peekedBinaryEntryType = BinaryEntryType.EndOfStream;
			return EntryType.EndOfStream;
		}
		peekedBinaryEntryType = (BinaryEntryType)num;
		switch (peekedBinaryEntryType)
		{
		case BinaryEntryType.NamedStartOfReferenceNode:
		case BinaryEntryType.NamedStartOfStructNode:
			name = ReadStringValue();
			peekedEntryType = EntryType.StartOfNode;
			break;
		case BinaryEntryType.UnnamedStartOfReferenceNode:
		case BinaryEntryType.UnnamedStartOfStructNode:
			name = null;
			peekedEntryType = EntryType.StartOfNode;
			break;
		case BinaryEntryType.EndOfNode:
			name = null;
			peekedEntryType = EntryType.EndOfNode;
			break;
		case BinaryEntryType.StartOfArray:
			name = null;
			peekedEntryType = EntryType.StartOfArray;
			break;
		case BinaryEntryType.EndOfArray:
			name = null;
			peekedEntryType = EntryType.EndOfArray;
			break;
		case BinaryEntryType.PrimitiveArray:
			name = null;
			peekedEntryType = EntryType.PrimitiveArray;
			break;
		case BinaryEntryType.NamedInternalReference:
			name = ReadStringValue();
			peekedEntryType = EntryType.InternalReference;
			break;
		case BinaryEntryType.UnnamedInternalReference:
			name = null;
			peekedEntryType = EntryType.InternalReference;
			break;
		case BinaryEntryType.NamedExternalReferenceByIndex:
			name = ReadStringValue();
			peekedEntryType = EntryType.ExternalReferenceByIndex;
			break;
		case BinaryEntryType.UnnamedExternalReferenceByIndex:
			name = null;
			peekedEntryType = EntryType.ExternalReferenceByIndex;
			break;
		case BinaryEntryType.NamedExternalReferenceByGuid:
			name = ReadStringValue();
			peekedEntryType = EntryType.ExternalReferenceByGuid;
			break;
		case BinaryEntryType.UnnamedExternalReferenceByGuid:
			name = null;
			peekedEntryType = EntryType.ExternalReferenceByGuid;
			break;
		case BinaryEntryType.NamedExternalReferenceByString:
			name = ReadStringValue();
			peekedEntryType = EntryType.ExternalReferenceByString;
			break;
		case BinaryEntryType.UnnamedExternalReferenceByString:
			name = null;
			peekedEntryType = EntryType.ExternalReferenceByString;
			break;
		case BinaryEntryType.NamedSByte:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedSByte:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedByte:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedByte:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedShort:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedShort:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedUShort:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedUShort:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedInt:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedInt:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedUInt:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedUInt:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedLong:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedLong:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedULong:
			name = ReadStringValue();
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.UnnamedULong:
			name = null;
			peekedEntryType = EntryType.Integer;
			break;
		case BinaryEntryType.NamedFloat:
			name = ReadStringValue();
			peekedEntryType = EntryType.FloatingPoint;
			break;
		case BinaryEntryType.UnnamedFloat:
			name = null;
			peekedEntryType = EntryType.FloatingPoint;
			break;
		case BinaryEntryType.NamedDouble:
			name = ReadStringValue();
			peekedEntryType = EntryType.FloatingPoint;
			break;
		case BinaryEntryType.UnnamedDouble:
			name = null;
			peekedEntryType = EntryType.FloatingPoint;
			break;
		case BinaryEntryType.NamedDecimal:
			name = ReadStringValue();
			peekedEntryType = EntryType.FloatingPoint;
			break;
		case BinaryEntryType.UnnamedDecimal:
			name = null;
			peekedEntryType = EntryType.FloatingPoint;
			break;
		case BinaryEntryType.NamedChar:
			name = ReadStringValue();
			peekedEntryType = EntryType.String;
			break;
		case BinaryEntryType.UnnamedChar:
			name = null;
			peekedEntryType = EntryType.String;
			break;
		case BinaryEntryType.NamedString:
			name = ReadStringValue();
			peekedEntryType = EntryType.String;
			break;
		case BinaryEntryType.UnnamedString:
			name = null;
			peekedEntryType = EntryType.String;
			break;
		case BinaryEntryType.NamedGuid:
			name = ReadStringValue();
			peekedEntryType = EntryType.Guid;
			break;
		case BinaryEntryType.UnnamedGuid:
			name = null;
			peekedEntryType = EntryType.Guid;
			break;
		case BinaryEntryType.NamedBoolean:
			name = ReadStringValue();
			peekedEntryType = EntryType.Boolean;
			break;
		case BinaryEntryType.UnnamedBoolean:
			name = null;
			peekedEntryType = EntryType.Boolean;
			break;
		case BinaryEntryType.NamedNull:
			name = ReadStringValue();
			peekedEntryType = EntryType.Null;
			break;
		case BinaryEntryType.UnnamedNull:
			name = null;
			peekedEntryType = EntryType.Null;
			break;
		case BinaryEntryType.EndOfStream:
			name = null;
			peekedEntryType = EntryType.EndOfStream;
			break;
		case BinaryEntryType.TypeName:
		case BinaryEntryType.TypeID:
			peekedBinaryEntryType = BinaryEntryType.Invalid;
			peekedEntryType = EntryType.Invalid;
			throw new InvalidOperationException("Invalid binary data stream: BinaryEntryType.TypeName and BinaryEntryType.TypeID must never be peeked by the binary reader.");
		default:
			name = null;
			peekedBinaryEntryType = BinaryEntryType.Invalid;
			peekedEntryType = EntryType.Invalid;
			throw new InvalidOperationException("Invalid binary data stream: could not parse peeked BinaryEntryType byte '" + num + "' into a known entry type.");
		}
		peekedEntryName = name;
		if (!peekedEntryType.HasValue)
		{
			peekedEntryType = EntryType.Invalid;
		}
		return peekedEntryType.Value;
	}

	public override bool EnterArray(out long length)
	{
		PeekEntry();
		if (peekedEntryType == EntryType.StartOfArray)
		{
			PushArray();
			Stream.Read(buffer, 0, 8);
			length = ProperBitConverter.ToInt64(buffer, 0);
			if (length < 0)
			{
				length = 0L;
				MarkEntryContentConsumed();
				base.Context.Config.DebugContext.LogError("Invalid array length: " + length + ".");
				return false;
			}
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		length = 0L;
		return false;
	}

	public override bool EnterNode(out Type type)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedStartOfReferenceNode || peekedBinaryEntryType == BinaryEntryType.UnnamedStartOfReferenceNode)
		{
			type = ReadTypeEntry();
			int id = ReadIntValue();
			PushNode(peekedEntryName, id, type);
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedStartOfStructNode || peekedBinaryEntryType == BinaryEntryType.UnnamedStartOfStructNode)
		{
			type = ReadTypeEntry();
			PushNode(peekedEntryName, -1, type);
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		type = null;
		return false;
	}

	public override bool ExitArray()
	{
		PeekEntry();
		while (peekedBinaryEntryType != BinaryEntryType.EndOfArray && peekedBinaryEntryType != BinaryEntryType.EndOfStream)
		{
			if (peekedEntryType == EntryType.EndOfNode)
			{
				base.Context.Config.DebugContext.LogError("Data layout mismatch; skipping past node boundary when exiting array.");
				MarkEntryContentConsumed();
			}
			SkipEntry();
		}
		if (peekedBinaryEntryType == BinaryEntryType.EndOfArray)
		{
			MarkEntryContentConsumed();
			PopArray();
			return true;
		}
		return false;
	}

	public override bool ExitNode()
	{
		PeekEntry();
		while (peekedBinaryEntryType != BinaryEntryType.EndOfNode && peekedBinaryEntryType != BinaryEntryType.EndOfStream)
		{
			if (peekedEntryType == EntryType.EndOfArray)
			{
				base.Context.Config.DebugContext.LogError("Data layout mismatch; skipping past array boundary when exiting node.");
				MarkEntryContentConsumed();
			}
			SkipEntry();
		}
		if (peekedBinaryEntryType == BinaryEntryType.EndOfNode)
		{
			MarkEntryContentConsumed();
			PopNode(base.CurrentNodeName);
			return true;
		}
		return false;
	}

	public override bool ReadPrimitiveArray<T>(out T[] array)
	{
		if (!FormatterUtilities.IsPrimitiveArrayType(typeof(T)))
		{
			throw new ArgumentException("Type " + typeof(T).Name + " is not a valid primitive array type.");
		}
		PeekEntry();
		if (peekedEntryType == EntryType.PrimitiveArray)
		{
			Stream.Read(this.buffer, 0, 8);
			int num = ProperBitConverter.ToInt32(this.buffer, 0);
			int num2 = ProperBitConverter.ToInt32(this.buffer, 4);
			int num3 = num * num2;
			if ((object)typeof(T) == typeof(byte))
			{
				byte[] array2 = new byte[num3];
				Stream.Read(array2, 0, num3);
				array = (T[])(object)array2;
				MarkEntryContentConsumed();
				return true;
			}
			using Buffer<byte> buffer = Buffer<byte>.Claim(num3);
			Stream.Read(buffer.Array, 0, num3);
			array = new T[num];
			if (BitConverter.IsLittleEndian)
			{
				UnsafeUtilities.MemoryCopy(buffer.Array, array, num3, 0, 0);
			}
			else
			{
				Func<byte[], int, T> func = (Func<byte[], int, T>)PrimitiveFromByteMethods[typeof(T)];
				byte[] array3 = buffer.Array;
				for (int i = 0; i < num; i++)
				{
					array[i] = func(array3, i * num2);
				}
			}
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		array = null;
		return false;
	}

	public override bool ReadBoolean(out bool value)
	{
		PeekEntry();
		if (peekedEntryType == EntryType.Boolean)
		{
			value = Stream.ReadByte() == 1;
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		value = false;
		return false;
	}

	public override bool ReadSByte(out sbyte value)
	{
		if (ReadInt64(out var value2))
		{
			try
			{
				value = checked((sbyte)value2);
			}
			catch (OverflowException)
			{
				value = 0;
			}
			return true;
		}
		value = 0;
		return false;
	}

	public override bool ReadByte(out byte value)
	{
		if (ReadUInt64(out var value2))
		{
			try
			{
				value = checked((byte)value2);
			}
			catch (OverflowException)
			{
				value = 0;
			}
			return true;
		}
		value = 0;
		return false;
	}

	public override bool ReadInt16(out short value)
	{
		if (ReadInt64(out var value2))
		{
			try
			{
				value = checked((short)value2);
			}
			catch (OverflowException)
			{
				value = 0;
			}
			return true;
		}
		value = 0;
		return false;
	}

	public override bool ReadUInt16(out ushort value)
	{
		if (ReadUInt64(out var value2))
		{
			try
			{
				value = checked((ushort)value2);
			}
			catch (OverflowException)
			{
				value = 0;
			}
			return true;
		}
		value = 0;
		return false;
	}

	public override bool ReadInt32(out int value)
	{
		if (ReadInt64(out var value2))
		{
			try
			{
				value = checked((int)value2);
			}
			catch (OverflowException)
			{
				value = 0;
			}
			return true;
		}
		value = 0;
		return false;
	}

	public override bool ReadUInt32(out uint value)
	{
		if (ReadUInt64(out var value2))
		{
			try
			{
				value = checked((uint)value2);
			}
			catch (OverflowException)
			{
				value = 0u;
			}
			return true;
		}
		value = 0u;
		return false;
	}

	public override bool ReadInt64(out long value)
	{
		PeekEntry();
		if (peekedEntryType == EntryType.Integer)
		{
			try
			{
				switch (peekedBinaryEntryType)
				{
				case BinaryEntryType.NamedSByte:
				case BinaryEntryType.UnnamedSByte:
				case BinaryEntryType.NamedByte:
				case BinaryEntryType.UnnamedByte:
					value = Stream.ReadByte();
					break;
				case BinaryEntryType.NamedShort:
				case BinaryEntryType.UnnamedShort:
					Stream.Read(buffer, 0, 2);
					value = ProperBitConverter.ToInt16(buffer, 0);
					break;
				case BinaryEntryType.NamedUShort:
				case BinaryEntryType.UnnamedUShort:
					Stream.Read(buffer, 0, 2);
					value = ProperBitConverter.ToUInt16(buffer, 0);
					break;
				case BinaryEntryType.NamedInt:
				case BinaryEntryType.UnnamedInt:
					Stream.Read(buffer, 0, 4);
					value = ProperBitConverter.ToInt32(buffer, 0);
					break;
				case BinaryEntryType.NamedUInt:
				case BinaryEntryType.UnnamedUInt:
					Stream.Read(buffer, 0, 4);
					value = ProperBitConverter.ToUInt32(buffer, 0);
					break;
				case BinaryEntryType.NamedLong:
				case BinaryEntryType.UnnamedLong:
					Stream.Read(buffer, 0, 8);
					value = ProperBitConverter.ToInt64(buffer, 0);
					break;
				case BinaryEntryType.NamedULong:
				case BinaryEntryType.UnnamedULong:
					Stream.Read(buffer, 0, 8);
					value = checked((long)ProperBitConverter.ToUInt64(buffer, 0));
					break;
				default:
					throw new InvalidOperationException();
				}
			}
			catch (OverflowException)
			{
				value = 0L;
			}
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		value = 0L;
		return false;
	}

	public override bool ReadUInt64(out ulong value)
	{
		PeekEntry();
		checked
		{
			if (peekedEntryType == EntryType.Integer)
			{
				try
				{
					switch (peekedBinaryEntryType)
					{
					case BinaryEntryType.NamedSByte:
					case BinaryEntryType.UnnamedSByte:
					case BinaryEntryType.NamedByte:
					case BinaryEntryType.UnnamedByte:
						value = (ulong)Stream.ReadByte();
						break;
					case BinaryEntryType.NamedShort:
					case BinaryEntryType.UnnamedShort:
						Stream.Read(buffer, 0, 2);
						value = (ulong)ProperBitConverter.ToInt16(buffer, 0);
						break;
					case BinaryEntryType.NamedUShort:
					case BinaryEntryType.UnnamedUShort:
						Stream.Read(buffer, 0, 2);
						value = ProperBitConverter.ToUInt16(buffer, 0);
						break;
					case BinaryEntryType.NamedInt:
					case BinaryEntryType.UnnamedInt:
						Stream.Read(buffer, 0, 4);
						value = (ulong)ProperBitConverter.ToInt32(buffer, 0);
						break;
					case BinaryEntryType.NamedUInt:
					case BinaryEntryType.UnnamedUInt:
						Stream.Read(buffer, 0, 4);
						value = ProperBitConverter.ToUInt32(buffer, 0);
						break;
					case BinaryEntryType.NamedLong:
					case BinaryEntryType.UnnamedLong:
						Stream.Read(buffer, 0, 8);
						value = (ulong)ProperBitConverter.ToInt64(buffer, 0);
						break;
					case BinaryEntryType.NamedULong:
					case BinaryEntryType.UnnamedULong:
						Stream.Read(buffer, 0, 8);
						value = ProperBitConverter.ToUInt64(buffer, 0);
						break;
					default:
						throw new InvalidOperationException();
					}
				}
				catch (OverflowException)
				{
					value = 0uL;
				}
				MarkEntryContentConsumed();
				return true;
			}
			SkipEntry();
			value = 0uL;
			return false;
		}
	}

	public override bool ReadChar(out char value)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedChar || peekedBinaryEntryType == BinaryEntryType.UnnamedChar)
		{
			Stream.Read(buffer, 0, 2);
			value = (char)ProperBitConverter.ToUInt16(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedString || peekedBinaryEntryType == BinaryEntryType.UnnamedString)
		{
			string text = ReadStringValue();
			if (text.Length > 0)
			{
				value = text[0];
			}
			else
			{
				value = '\0';
			}
			return true;
		}
		SkipEntry();
		value = '\0';
		return false;
	}

	public override bool ReadSingle(out float value)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedFloat || peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
		{
			Stream.Read(buffer, 0, 4);
			value = ProperBitConverter.ToSingle(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedDouble || peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
		{
			Stream.Read(buffer, 0, 8);
			try
			{
				value = (float)ProperBitConverter.ToDouble(buffer, 0);
			}
			catch (OverflowException)
			{
				value = 0f;
			}
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedDecimal || peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
		{
			Stream.Read(buffer, 0, 16);
			try
			{
				value = (float)ProperBitConverter.ToDecimal(buffer, 0);
			}
			catch (OverflowException)
			{
				value = 0f;
			}
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedEntryType == EntryType.Integer)
		{
			ReadInt64(out var value2);
			try
			{
				value = value2;
			}
			catch (OverflowException)
			{
				value = 0f;
			}
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		value = 0f;
		return false;
	}

	public override bool ReadDouble(out double value)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedDouble || peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
		{
			Stream.Read(buffer, 0, 8);
			value = ProperBitConverter.ToDouble(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedFloat || peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
		{
			Stream.Read(buffer, 0, 4);
			value = ProperBitConverter.ToSingle(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedDecimal || peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
		{
			Stream.Read(buffer, 0, 16);
			try
			{
				value = (double)ProperBitConverter.ToDecimal(buffer, 0);
			}
			catch (OverflowException)
			{
				value = 0.0;
			}
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedEntryType == EntryType.Integer)
		{
			ReadInt64(out var value2);
			try
			{
				value = value2;
			}
			catch (OverflowException)
			{
				value = 0.0;
			}
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		value = 0.0;
		return false;
	}

	public override bool ReadDecimal(out decimal value)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedDecimal || peekedBinaryEntryType == BinaryEntryType.UnnamedDecimal)
		{
			Stream.Read(buffer, 0, 16);
			value = ProperBitConverter.ToDecimal(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedDouble || peekedBinaryEntryType == BinaryEntryType.UnnamedDouble)
		{
			Stream.Read(buffer, 0, 8);
			try
			{
				value = (decimal)ProperBitConverter.ToDouble(buffer, 0);
			}
			catch (OverflowException)
			{
				value = default(decimal);
			}
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedBinaryEntryType == BinaryEntryType.NamedFloat || peekedBinaryEntryType == BinaryEntryType.UnnamedFloat)
		{
			Stream.Read(buffer, 0, 4);
			try
			{
				value = (decimal)ProperBitConverter.ToSingle(buffer, 0);
			}
			catch (OverflowException)
			{
				value = default(decimal);
			}
			MarkEntryContentConsumed();
			return true;
		}
		if (peekedEntryType == EntryType.Integer)
		{
			ReadInt64(out var value2);
			try
			{
				value = value2;
			}
			catch (OverflowException)
			{
				value = default(decimal);
			}
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		value = default(decimal);
		return false;
	}

	public override bool ReadExternalReference(out Guid guid)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByGuid || peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByGuid)
		{
			Stream.Read(buffer, 0, 16);
			guid = ProperBitConverter.ToGuid(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		guid = default(Guid);
		return false;
	}

	public override bool ReadGuid(out Guid value)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedGuid || peekedBinaryEntryType == BinaryEntryType.UnnamedGuid)
		{
			Stream.Read(buffer, 0, 16);
			value = ProperBitConverter.ToGuid(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		value = default(Guid);
		return false;
	}

	public override bool ReadExternalReference(out int index)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByIndex || peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByIndex)
		{
			Stream.Read(buffer, 0, 4);
			index = ProperBitConverter.ToInt32(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		index = -1;
		return false;
	}

	public override bool ReadExternalReference(out string id)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedExternalReferenceByString || peekedBinaryEntryType == BinaryEntryType.UnnamedExternalReferenceByString)
		{
			id = ReadStringValue();
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		id = null;
		return false;
	}

	public override bool ReadNull()
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedNull || peekedBinaryEntryType == BinaryEntryType.UnnamedNull)
		{
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		return false;
	}

	public override bool ReadInternalReference(out int id)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedInternalReference || peekedBinaryEntryType == BinaryEntryType.UnnamedInternalReference)
		{
			Stream.Read(buffer, 0, 4);
			id = ProperBitConverter.ToInt32(buffer, 0);
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		id = -1;
		return false;
	}

	public override bool ReadString(out string value)
	{
		PeekEntry();
		if (peekedBinaryEntryType == BinaryEntryType.NamedString || peekedBinaryEntryType == BinaryEntryType.UnnamedString)
		{
			value = ReadStringValue();
			MarkEntryContentConsumed();
			return true;
		}
		SkipEntry();
		value = null;
		return false;
	}

	public override void PrepareNewSerializationSession()
	{
		base.PrepareNewSerializationSession();
		peekedEntryType = null;
		peekedEntryName = null;
		peekedBinaryEntryType = BinaryEntryType.Invalid;
		types.Clear();
	}

	public override string GetDataDump()
	{
		if (!Stream.CanSeek)
		{
			return "Binary data stream cannot seek; cannot dump data.";
		}
		long position = Stream.Position;
		byte[] array = new byte[Stream.Length];
		Stream.Position = 0L;
		Stream.Read(array, 0, array.Length);
		Stream.Position = position;
		return "Binary hex dump: " + ProperBitConverter.BytesToHexString(array);
	}

	private string ReadStringValue()
	{
		int num = Stream.ReadByte();
		if (num < 0)
		{
			return string.Empty;
		}
		switch (num)
		{
		case 0:
		{
			int num4 = ReadIntValue();
			using Buffer<byte> buffer2 = Buffer<byte>.Claim(num4);
			byte[] array2 = buffer2.Array;
			Stream.Read(array2, 0, num4);
			return UnsafeUtilities.StringFromBytes(array2, num4, needs16BitSupport: false);
		}
		case 1:
		{
			int num2 = ReadIntValue();
			int num3 = num2 * 2;
			using Buffer<byte> buffer = Buffer<byte>.Claim(num3);
			byte[] array = buffer.Array;
			Stream.Read(array, 0, num3);
			return UnsafeUtilities.StringFromBytes(array, num2, needs16BitSupport: true);
		}
		default:
			base.Context.Config.DebugContext.LogError("Expected string char size flag, but got value " + num + " at position " + Stream.Position);
			return string.Empty;
		}
	}

	private void SkipStringValue()
	{
		int num = Stream.ReadByte();
		if (num < 0)
		{
			return;
		}
		int num2;
		switch (num)
		{
		case 0:
			num2 = ReadIntValue();
			break;
		case 1:
			num2 = ReadIntValue() * 2;
			break;
		default:
			base.Context.Config.DebugContext.LogError("Expect string char size flag, but got value: " + num);
			return;
		}
		if (Stream.CanSeek)
		{
			Stream.Seek(num2, SeekOrigin.Current);
			return;
		}
		for (int i = 0; i < num2; i++)
		{
			Stream.ReadByte();
		}
	}

	private int ReadIntValue()
	{
		Stream.Read(buffer, 0, 4);
		return ProperBitConverter.ToInt32(buffer, 0);
	}

	private void SkipPeekedEntryContent(bool allowExitArrayAndNode = false)
	{
		if (!peekedEntryType.HasValue || (!allowExitArrayAndNode && (peekedBinaryEntryType == BinaryEntryType.EndOfNode || peekedBinaryEntryType == BinaryEntryType.EndOfArray)))
		{
			return;
		}
		switch (peekedBinaryEntryType)
		{
		case BinaryEntryType.NamedStartOfReferenceNode:
		case BinaryEntryType.UnnamedStartOfReferenceNode:
			ReadTypeEntry();
			ReadIntValue();
			break;
		case BinaryEntryType.NamedStartOfStructNode:
		case BinaryEntryType.UnnamedStartOfStructNode:
			ReadTypeEntry();
			break;
		case BinaryEntryType.StartOfArray:
			if (Stream.CanSeek)
			{
				Stream.Seek(8L, SeekOrigin.Current);
			}
			else
			{
				Stream.Read(buffer, 0, 8);
			}
			break;
		case BinaryEntryType.PrimitiveArray:
		{
			Stream.Read(buffer, 0, 8);
			int num = ProperBitConverter.ToInt32(buffer, 0);
			int num2 = ProperBitConverter.ToInt32(buffer, 4);
			int num3 = num * num2;
			if (Stream.CanSeek)
			{
				Stream.Seek(num3, SeekOrigin.Current);
			}
			else if (num2 <= buffer.Length)
			{
				for (int i = 0; i < num; i++)
				{
					Stream.Read(buffer, 0, num2);
				}
			}
			else
			{
				for (int j = 0; j < num3; j++)
				{
					Stream.ReadByte();
				}
			}
			break;
		}
		case BinaryEntryType.NamedSByte:
		case BinaryEntryType.UnnamedSByte:
		case BinaryEntryType.NamedByte:
		case BinaryEntryType.UnnamedByte:
		case BinaryEntryType.NamedBoolean:
		case BinaryEntryType.UnnamedBoolean:
			Stream.ReadByte();
			break;
		case BinaryEntryType.NamedShort:
		case BinaryEntryType.UnnamedShort:
		case BinaryEntryType.NamedUShort:
		case BinaryEntryType.UnnamedUShort:
		case BinaryEntryType.NamedChar:
		case BinaryEntryType.UnnamedChar:
			if (Stream.CanSeek)
			{
				Stream.Seek(2L, SeekOrigin.Current);
			}
			else
			{
				Stream.Read(buffer, 0, 2);
			}
			break;
		case BinaryEntryType.NamedInternalReference:
		case BinaryEntryType.UnnamedInternalReference:
		case BinaryEntryType.NamedExternalReferenceByIndex:
		case BinaryEntryType.UnnamedExternalReferenceByIndex:
		case BinaryEntryType.NamedInt:
		case BinaryEntryType.UnnamedInt:
		case BinaryEntryType.NamedUInt:
		case BinaryEntryType.UnnamedUInt:
		case BinaryEntryType.NamedFloat:
		case BinaryEntryType.UnnamedFloat:
			if (Stream.CanSeek)
			{
				Stream.Seek(4L, SeekOrigin.Current);
			}
			else
			{
				Stream.Read(buffer, 0, 4);
			}
			break;
		case BinaryEntryType.NamedLong:
		case BinaryEntryType.UnnamedLong:
		case BinaryEntryType.NamedULong:
		case BinaryEntryType.UnnamedULong:
		case BinaryEntryType.NamedDouble:
		case BinaryEntryType.UnnamedDouble:
			if (Stream.CanSeek)
			{
				Stream.Seek(8L, SeekOrigin.Current);
			}
			else
			{
				Stream.Read(buffer, 0, 8);
			}
			break;
		case BinaryEntryType.NamedExternalReferenceByGuid:
		case BinaryEntryType.UnnamedExternalReferenceByGuid:
		case BinaryEntryType.NamedDecimal:
		case BinaryEntryType.UnnamedDecimal:
		case BinaryEntryType.NamedGuid:
		case BinaryEntryType.UnnamedGuid:
			if (Stream.CanSeek)
			{
				Stream.Seek(16L, SeekOrigin.Current);
			}
			else
			{
				Stream.Read(buffer, 0, 16);
			}
			break;
		case BinaryEntryType.NamedString:
		case BinaryEntryType.UnnamedString:
		case BinaryEntryType.NamedExternalReferenceByString:
		case BinaryEntryType.UnnamedExternalReferenceByString:
			SkipStringValue();
			break;
		case BinaryEntryType.TypeName:
			base.Context.Config.DebugContext.LogError("Parsing error in binary data reader: should not be able to peek a TypeID entry.");
			ReadIntValue();
			ReadStringValue();
			break;
		case BinaryEntryType.TypeID:
			base.Context.Config.DebugContext.LogError("Parsing error in binary data reader: should not be able to peek a TypeID entry.");
			ReadIntValue();
			break;
		}
		MarkEntryContentConsumed();
	}

	private Type ReadTypeEntry()
	{
		int num = Stream.ReadByte();
		if (num < 0)
		{
			return null;
		}
		BinaryEntryType binaryEntryType;
		Type value;
		if ((binaryEntryType = (BinaryEntryType)num) == BinaryEntryType.TypeName)
		{
			int key = ReadIntValue();
			string typeName = ReadStringValue();
			value = base.Context.Binder.BindToType(typeName, base.Context.Config.DebugContext);
			types.Add(key, value);
		}
		else
		{
			switch (binaryEntryType)
			{
			case BinaryEntryType.TypeID:
			{
				int num2 = ReadIntValue();
				if (!types.TryGetValue(num2, out value))
				{
					base.Context.Config.DebugContext.LogError("Missing type ID during deserialization: " + num2 + " at node " + base.CurrentNodeName + " and depth " + base.CurrentNodeDepth + " and id " + base.CurrentNodeId);
				}
				break;
			}
			case BinaryEntryType.UnnamedNull:
				value = null;
				break;
			default:
				value = null;
				base.Context.Config.DebugContext.LogError(string.Concat("Expected TypeName, TypeID or UnnamedNull entry flag for reading type data, but instead got the entry flag: ", binaryEntryType, "."));
				break;
			}
		}
		return value;
	}

	private void MarkEntryContentConsumed()
	{
		peekedEntryType = null;
		peekedEntryName = null;
		peekedBinaryEntryType = BinaryEntryType.Invalid;
	}

	protected override EntryType PeekEntry()
	{
		string name;
		return PeekEntry(out name);
	}

	protected override EntryType ReadToNextEntry()
	{
		SkipPeekedEntryContent();
		string name;
		return PeekEntry(out name);
	}
}
