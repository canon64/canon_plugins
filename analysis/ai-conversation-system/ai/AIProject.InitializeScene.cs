using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Manager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIProject;

public class InitializeScene : BaseLoader
{
	private XElement _xml;

	private int _width = 1280;

	private int _height = 720;

	private int _quality = 2;

	private bool _full;

	[SerializeField]
	[SceneEnum]
	private int _loadLevel = 1;

	private const string SetupFilePath = "UserData/setup.xml";

	[SerializeField]
	private Color _fadeColor = Color.black;

	private void Start()
	{
		if (SystemInfo.graphicsShaderLevel < 30)
		{
			MonoBehaviour.print("shaders Non support");
		}
		if (File.Exists("UserData/setup.xml"))
		{
			try
			{
				_xml = XElement.Load("UserData/setup.xml");
				if (_xml != null)
				{
					IEnumerable<XElement> enumerable = _xml.Elements();
					foreach (XElement item in enumerable)
					{
						switch (item.Name.ToString())
						{
						case "Width":
							_width = int.Parse(item.Value);
							break;
						case "Height":
							_height = int.Parse(item.Value);
							break;
						case "FullScreen":
							_full = bool.Parse(item.Value);
							break;
						case "Quality":
							_quality = int.Parse(item.Value);
							break;
						}
					}
					Screen.SetResolution(_width, _height, _full);
					switch (_quality)
					{
					case 0:
						QualitySettings.SetQualityLevel(0);
						break;
					case 1:
						QualitySettings.SetQualityLevel(2);
						break;
					case 2:
						QualitySettings.SetQualityLevel(4);
						break;
					}
				}
			}
			catch (XmlException exception)
			{
				Debug.LogException(exception, base.gameObject);
				File.Delete("UserData/setup.xml");
			}
		}
		Singleton<Manager.Scene>.Instance.SetFadeColor(_fadeColor);
		string scenePathByBuildIndex = SceneUtility.GetScenePathByBuildIndex(_loadLevel);
		int num = scenePathByBuildIndex.LastIndexOf("/");
		int num2 = scenePathByBuildIndex.LastIndexOf(".");
		string text = scenePathByBuildIndex.Substring(num + 1, num2 - num - 1);
		Manager.Scene.Data data = new Manager.Scene.Data();
		data.levelName = text;
		data.isFade = true;
		Manager.Scene.Data data2 = data;
		Singleton<Manager.Scene>.Instance.LoadReserve(data2, isLoadingImageDraw: false);
		Debug.Log("First Scene: " + text);
	}
}
