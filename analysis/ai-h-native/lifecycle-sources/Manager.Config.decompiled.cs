using System.Diagnostics;
using ConfigScene;
using Illusion.Elements.Xml;
using UnityEngine;

namespace Manager;

public sealed class Config : Singleton<Config>
{
	private const string UserPath = "config";

	private const string FileName = "system.xml";

	private const string RootName = "System";

	private Control xmlCtrl;

	public static bool initialized { get; private set; }

	public static ActionSystem ActData { get; private set; }

	public static GraphicSystem GraphicData { get; private set; }

	public static SoundSystem SoundData { get; private set; }

	public static GameConfigSystem GameData { get; private set; }

	public static HSystem HData { get; private set; }

	public void Reset()
	{
		if (xmlCtrl != null)
		{
			xmlCtrl.Init();
		}
	}

	public void Load()
	{
		xmlCtrl.Read();
	}

	public void Save()
	{
		xmlCtrl.Write();
	}

	protected override void Awake()
	{
		if (CheckInstance())
		{
			Object.DontDestroyOnLoad(base.gameObject);
		}
	}

	private void Start()
	{
		ActData = new ActionSystem("Action");
		GraphicData = new GraphicSystem("Graphic");
		SoundData = new SoundSystem("Sound");
		GameData = new GameConfigSystem("Game");
		HData = new HSystem("H");
		xmlCtrl = new Control("config", "system.xml", "System", ActData, GraphicData, SoundData, GameData, HData);
		Load();
		initialized = true;
	}

	[Conditional("__DEBUG_PROC__")]
	private void DBLog(object o)
	{
		UnityEngine.Debug.Log(o, this);
	}
}
