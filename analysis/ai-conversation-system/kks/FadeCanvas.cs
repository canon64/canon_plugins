using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ReMotion;
using UniRx;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class FadeCanvas : MonoBehaviour
{
	[Flags]
	public enum Fade
	{
		None = 0,
		In = 1,
		Out = 2,
		InOut = 3
	}

	public enum HitTiming
	{
		Default,
		Reverse
	}

	[SerializeField]
	private HitTiming _hitTiming;

	[SerializeField]
	private CanvasGroup _canvasGroup;

	[SerializeField]
	private float _time = 1f;

	[SerializeField]
	private bool _useRaycast = true;

	[SerializeField]
	private bool _isSkip;

	public bool isEnd => _cancellation == null;

	public bool isFading => _isFading.Value;

	public CanvasGroup canvasGroup => _canvasGroup;

	public float time
	{
		set
		{
			_time = value;
		}
	}

	private float initTime { get; set; }

	public bool isSkip
	{
		set
		{
			_isSkip = value;
		}
	}

	public bool visible
	{
		set
		{
			StartFade(value ? Fade.In : Fade.Out);
		}
	}

	public bool visibleForce
	{
		set
		{
			Force(value ? Fade.In : Fade.Out);
		}
	}

	private Action<Fade> _onStart { get; set; }

	private Action<Fade> _onComplete { get; set; }

	protected BoolReactiveProperty _isFading { get; } = new BoolReactiveProperty(initialValue: false);


	private CancellationTokenSource _cancellation { get; set; }

	public event Action<Fade> onStart
	{
		add
		{
			_onStart = (Action<Fade>)Delegate.Combine(_onStart, value);
		}
		remove
		{
			_onStart = (Action<Fade>)Delegate.Remove(_onStart, value);
		}
	}

	public event Action<Fade> onComplete
	{
		add
		{
			_onComplete = (Action<Fade>)Delegate.Combine(_onComplete, value);
		}
		remove
		{
			_onComplete = (Action<Fade>)Delegate.Remove(_onComplete, value);
		}
	}

	public void DefaultTime()
	{
		_time = initTime;
	}

	public void Force(Fade fade)
	{
		StartAysnc(fade, 0f, ignoreTimeScale: false, throwOnError: false).Forget();
	}

	public void StartFade(Fade fade, bool throwOnError = false)
	{
		StartAysnc(fade, _time, ignoreTimeScale: true, throwOnError).Forget();
	}

	public async UniTask StartFadeAysnc(Fade fade, bool throwOnError = false)
	{
		await StartAysnc(fade, _time, ignoreTimeScale: true, throwOnError);
	}

	public void StartInToOut(float duration, Func<UniTask> func, bool ignoreTimeScale = true, bool throwOnError = false)
	{
		StartInToOutAysnc(duration, func, ignoreTimeScale, throwOnError).Forget();
	}

	public async UniTask StartInToOutAysnc(float duration, Func<UniTask> func, bool ignoreTimeScale = true, bool throwOnError = false)
	{
		_ = 2;
		try
		{
			await StartAysnc(Fade.In, duration, ignoreTimeScale, throwOnError: true);
			if (func != null)
			{
				await func();
			}
			await StartAysnc(Fade.Out, duration, ignoreTimeScale, throwOnError: true);
		}
		catch (OperationCanceledException ex)
		{
			if (throwOnError)
			{
				_cancellation?.Dispose();
				_cancellation = null;
				throw new OperationCanceledException("FadeIn-Out:" + ex.Message);
			}
		}
	}

	public void Cancel()
	{
		_cancellation?.Cancel();
	}

	protected async UniTask StartAysnc(Fade fade, float duration, bool ignoreTimeScale, bool throwOnError)
	{
		_cancellation?.Cancel();
		_cancellation = _cancellation ?? new CancellationTokenSource();
		switch (fade)
		{
		case Fade.In:
		{
			_onStart?.Invoke(fade);
			float endAlpha = 1f;
			_isFading.Value = true;
			try
			{
				float alpha = _canvasGroup.alpha;
				if (duration > 0f)
				{
					await ObservableEasing.Linear(Mathf.Lerp(0.01f, duration, endAlpha - alpha), ignoreTimeScale).FrameTimeInterval(ignoreTimeScale).Do(delegate(TimeInterval<float> x)
					{
						_canvasGroup.alpha = Mathf.Lerp(alpha, endAlpha, x.Value);
					})
						.ToUniTask(useFirstValue: false, CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, this.GetCancellationTokenOnDestroy()).Token);
				}
				else
				{
					_canvasGroup.alpha = endAlpha;
				}
			}
			catch (OperationCanceledException ex3)
			{
				_cancellation?.Dispose();
				_cancellation = null;
				if (_isSkip)
				{
					_canvasGroup.alpha = endAlpha;
				}
				if (throwOnError)
				{
					throw new OperationCanceledException("FadeIn:" + ex3.Message);
				}
			}
			_onComplete?.Invoke(fade);
			break;
		}
		case Fade.Out:
		{
			_onStart?.Invoke(fade);
			float endAlpha2 = 0f;
			try
			{
				float alpha2 = _canvasGroup.alpha;
				if (duration > 0f)
				{
					await ObservableEasing.Linear(Mathf.Lerp(0.01f, duration, alpha2), ignoreTimeScale).FrameTimeInterval(ignoreTimeScale).Do(delegate(TimeInterval<float> x)
					{
						_canvasGroup.alpha = Mathf.Lerp(alpha2, endAlpha2, x.Value);
					})
						.ToUniTask(useFirstValue: false, CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token, this.GetCancellationTokenOnDestroy()).Token);
				}
				else
				{
					_canvasGroup.alpha = endAlpha2;
				}
			}
			catch (OperationCanceledException ex2)
			{
				_cancellation?.Dispose();
				_cancellation = null;
				if (_isSkip)
				{
					_canvasGroup.alpha = endAlpha2;
				}
				if (throwOnError)
				{
					throw new OperationCanceledException("FadeOut:" + ex2.Message);
				}
			}
			_isFading.Value = false;
			_onComplete?.Invoke(fade);
			break;
		}
		case Fade.InOut:
			try
			{
				await StartInToOutAysnc(duration, null, ignoreTimeScale, throwOnError: true);
			}
			catch (OperationCanceledException ex)
			{
				if (throwOnError)
				{
					_cancellation?.Dispose();
					_cancellation = null;
					throw ex;
				}
			}
			break;
		}
		_cancellation?.Dispose();
		_cancellation = null;
	}

	protected virtual void Awake()
	{
		initTime = _time;
		if (!_useRaycast)
		{
			_canvasGroup.blocksRaycasts = false;
		}
		switch (_hitTiming)
		{
		case HitTiming.Default:
			_onStart = (Action<Fade>)Delegate.Combine(_onStart, (Action<Fade>)delegate(Fade fade)
			{
				if (fade == Fade.In)
				{
					SetRaycast(isOn: true);
				}
			});
			_onComplete = (Action<Fade>)Delegate.Combine(_onComplete, (Action<Fade>)delegate(Fade fade)
			{
				if (fade == Fade.Out)
				{
					SetRaycast(isOn: false);
				}
			});
			break;
		case HitTiming.Reverse:
			_onComplete = (Action<Fade>)Delegate.Combine(_onComplete, (Action<Fade>)delegate(Fade fade)
			{
				if (fade == Fade.In)
				{
					SetRaycast(isOn: true);
				}
			});
			_onStart = (Action<Fade>)Delegate.Combine(_onStart, (Action<Fade>)delegate(Fade fade)
			{
				if (fade == Fade.Out)
				{
					SetRaycast(isOn: false);
				}
			});
			break;
		}
		void SetRaycast(bool isOn)
		{
			if (_useRaycast && !(_canvasGroup == null))
			{
				_canvasGroup.blocksRaycasts = isOn;
			}
		}
	}

	protected virtual void Reset()
	{
		_canvasGroup = GetComponent<CanvasGroup>();
	}
}
