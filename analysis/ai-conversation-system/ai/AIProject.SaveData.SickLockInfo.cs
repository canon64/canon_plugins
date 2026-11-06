using MessagePack;

namespace AIProject.SaveData;

[MessagePackObject(false)]
public class SickLockInfo
{
	[Key(0)]
	public bool Lock { get; set; }

	[Key(1)]
	public float ElapsedTime { get; set; }
}
