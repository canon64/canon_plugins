namespace AIProject.Definitions;

public static class Path
{
	public const string ProductName = "";

	public const string DefaultManifestName = "abdata";

	public const string SceneCommonBundleDirectory = "scene/common/";

	public const string SceneCommonBundleName = "scene/common/00.unity3d";

	public static string SaveDataDirectory { get; } = UserData.Path + "save/";

	public static string CharaFileDirectory { get; } = UserData.Path + "chara/";

	public static string GlobalSaveDataFileName { get; } = "global.ila";

	public static string WorldSaveDataFileName { get; } = "save.ila";

	public static string GlobalSaveDataFile { get; } = SaveDataDirectory + GlobalSaveDataFileName;

	public static string WorldSaveDataFile { get; } = SaveDataDirectory + WorldSaveDataFileName;
}
