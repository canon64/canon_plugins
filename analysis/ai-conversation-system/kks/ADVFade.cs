using UnityEngine;
using UnityEngine.UI;

public class ADVFade : MonoBehaviour
{
	public class Fade
	{
		public Image filter;

		public Color initColor;

		public Color color;

		public float time;

		public float timer;

		public bool ignoreTimeScale;

		public bool isFadeInEnd => filter.color.a == 1f;

		public bool isFadeOutEnd => filter.color.a == 0f;

		public bool IsEnd => time == timer;

		public Fade(Image filter, Color color, float time, bool ignoreTimeScale, bool isUpdate = true)
		{
			this.filter = filter;
			initColor = filter.color;
			this.color = color;
			this.time = time;
			this.ignoreTimeScale = ignoreTimeScale;
			timer = 0f;
			if (isUpdate)
			{
				Update();
			}
		}

		public bool Update()
		{
			float num = Time.deltaTime;
			if (ignoreTimeScale)
			{
				num = Time.unscaledDeltaTime;
			}
			timer += num;
			timer = Mathf.Min(timer, time);
			float t = ((time == 0f) ? 1f : Mathf.InverseLerp(0f, time, timer));
			filter.color = Color.Lerp(initColor, color, t);
			return IsEnd;
		}
	}

	public bool isStartRun = true;

	[SerializeField]
	private Image filterFront;

	[SerializeField]
	private Image filterBack;

	private bool isEnd;

	private string frontImageAssetBundleName;

	private string backImageAssetBundleName;

	private Fade front;

	private Fade back;

	private int frontIndex = -1;

	private int backIndex = -1;

	private readonly Color initColor = new Color(1f, 1f, 1f, 0f);

	public Image FilterFront => filterFront;

	public Image FilterBack => filterBack;

	public int FrontIndex => frontIndex;

	public int BackIndex => backIndex;

	public bool IsFadeInEnd
	{
		get
		{
			if (!front.isFadeInEnd)
			{
				return back.isFadeInEnd;
			}
			return true;
		}
	}

	public bool IsEnd => isEnd;

	public void Initialize()
	{
		if (front == null)
		{
			frontIndex = filterFront.rectTransform.GetSiblingIndex();
		}
		if (back == null)
		{
			backIndex = filterBack.rectTransform.GetSiblingIndex();
		}
		filterFront.color = initColor;
		front = new Fade(filterFront, initColor, 0f, ignoreTimeScale: true);
		filterBack.color = initColor;
		back = new Fade(filterBack, initColor, 0f, ignoreTimeScale: true);
	}

	public void SetColor(bool isFront, Color color)
	{
		(isFront ? front.filter : back.filter).color = color;
	}

	public void CrossFadeAlpha(bool isFront, float alpha, float time, bool ignoreTimeScale)
	{
		Color color = (isFront ? front.filter.color : back.filter.color);
		color.a = alpha;
		CrossFadeColor(isFront, color, time, ignoreTimeScale);
	}

	public void CrossFadeColor(bool isFront, Color color, float time, bool ignoreTimeScale)
	{
		if (isFront)
		{
			front = new Fade(front.filter, color, time, ignoreTimeScale);
		}
		else
		{
			back = new Fade(back.filter, color, time, ignoreTimeScale);
		}
		isEnd = false;
	}

	public void LoadSprite(bool isFront, string bundleName, string assetName)
	{
		AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(bundleName, assetName, typeof(Sprite));
		Sprite sprite = assetBundleLoadAssetOperation.GetAsset<Sprite>();
		if (sprite == null)
		{
			Texture2D asset = assetBundleLoadAssetOperation.GetAsset<Texture2D>();
			sprite = Sprite.Create(asset, new Rect(0f, 0f, asset.width, asset.height), Vector2.zero);
		}
		if (isFront)
		{
			front.filter.sprite = sprite;
			if (!frontImageAssetBundleName.IsNullOrEmpty())
			{
				AssetBundleManager.UnloadAssetBundle(frontImageAssetBundleName, isUnloadForceRefCount: false);
			}
			frontImageAssetBundleName = bundleName;
		}
		else
		{
			back.filter.sprite = sprite;
			if (!backImageAssetBundleName.IsNullOrEmpty())
			{
				AssetBundleManager.UnloadAssetBundle(backImageAssetBundleName, isUnloadForceRefCount: false);
			}
			backImageAssetBundleName = bundleName;
		}
	}

	public void ReleaseSprite(bool isFront)
	{
		if (isFront)
		{
			front.filter.sprite = null;
			if (!frontImageAssetBundleName.IsNullOrEmpty())
			{
				AssetBundleManager.UnloadAssetBundle(frontImageAssetBundleName, isUnloadForceRefCount: false);
				frontImageAssetBundleName = null;
			}
		}
		else
		{
			back.filter.sprite = null;
			if (!backImageAssetBundleName.IsNullOrEmpty())
			{
				AssetBundleManager.UnloadAssetBundle(backImageAssetBundleName, isUnloadForceRefCount: false);
				backImageAssetBundleName = null;
			}
		}
	}

	private void Awake()
	{
		Initialize();
	}

	private void Update()
	{
		isEnd = true;
		if (front.Update() && front.isFadeOutEnd)
		{
			filterFront.raycastTarget = false;
		}
		else
		{
			isEnd = false;
			filterFront.raycastTarget = true;
		}
		if (!back.Update() || !back.isFadeOutEnd)
		{
			isEnd = false;
		}
	}

	private void OnDestroy()
	{
		if (AssetBundleManager.initialized)
		{
			ReleaseSprite(isFront: true);
			ReleaseSprite(isFront: false);
		}
	}
}
