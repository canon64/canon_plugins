using System;
using Cysharp.Threading.Tasks;
using Manager;
using UnityEngine;

public abstract class OpenData : MonoBehaviour
{
	[Serializable]
	public class CameraData
	{
		public Vector3 position;

		public Quaternion rotation;

		public static Quaternion ToTargetRotation(Vector3 myPos, Vector3 targetPos)
		{
			return Quaternion.LookRotation((targetPos - myPos).normalized);
		}
	}

	[Serializable]
	public class Data
	{
		public MonoBehaviour scene;

		public Vector3 position;

		public Quaternion rotation;

		public CameraData camera;
	}

	public string assetBundleName;

	public string levelName;

	public bool isLoad = true;

	public bool isAsync = true;

	public bool isFade = true;

	public bool isOverlap;

	public bool isLoadingImageDraw = true;

	public string manifestFileName;

	public Action onLoad;

	public abstract Data data { get; set; }

	public Func<UniTask> onFadeIn { get; set; }

	public Func<UniTask> onFadeOut { get; set; }

	public FadeCanvas.Fade fadeType { get; set; }

	protected virtual void Start()
	{
		Scene.Data data = new Scene.Data
		{
			bundleName = assetBundleName,
			levelName = levelName,
			isAdd = !isLoad,
			manifestName = manifestFileName,
			onLoad = onLoad,
			onFadeIn = onFadeIn,
			onFadeOut = onFadeOut
		};
		if (isFade)
		{
			data.isFade = isFade;
		}
		else
		{
			data.fadeType = fadeType;
		}
		if (!isAsync)
		{
			Scene.LoadReserve(data, isLoadingImageDraw);
		}
		else
		{
			Scene.LoadReserveAsyncForget(data, isLoadingImageDraw);
		}
	}
}
