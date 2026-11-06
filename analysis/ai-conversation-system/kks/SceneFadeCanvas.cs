using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class SceneFadeCanvas : FadeCanvas
{
	[SerializeField]
	private Canvas _canvas;

	[SerializeField]
	private Graphic _fadeImage;

	public Canvas canvas => _canvas;

	public Graphic fadeImage => _fadeImage;

	private Color initColor { get; set; } = Color.white;


	public void DefaultColor()
	{
		SetColor(initColor);
	}

	public void SetColor(Color _color)
	{
		if (_fadeImage != null)
		{
			_fadeImage.color = new Color(_color.r, _color.g, _color.b, _fadeImage.color.a);
		}
	}

	protected override void Awake()
	{
		base.Awake();
		if (_fadeImage != null)
		{
			initColor = _fadeImage.color;
		}
		if (_canvas != null)
		{
			base._isFading.Subscribe(delegate(bool isOn)
			{
				_canvas.enabled = isOn;
			});
		}
	}
}
