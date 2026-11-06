using System;
using System.Threading;
using ActionGame.Information;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Illusion.Game;
using Manager;
using TMPro;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace ActionGame;

public class KoikatsuPointUI : SingletonInitializer<KoikatsuPointUI>
{
	[Serializable]
	private class AddPoint
	{
		[SerializeField]
		private FadeCanvas _fadeCanvas;

		[SerializeField]
		private TMP_Text _text;

		[SerializeField]
		private RectTransform _transform;

		[SerializeField]
		private float _up;

		[SerializeField]
		private float _duration;

		[SerializeField]
		private Ease _ease = Ease.Linear;

		public bool visibleForce
		{
			set
			{
				_fadeCanvas.visibleForce = value;
			}
		}

		public bool visible
		{
			set
			{
				_fadeCanvas.visible = value;
			}
		}

		public bool isFading => _fadeCanvas.isFading;

		private Vector3 initPos { get; set; }

		public void Initialize()
		{
			initPos = _transform.anchoredPosition;
		}

		public void Set(int target, int now)
		{
			int num = target - now;
			string arg = string.Empty;
			if (num > 0)
			{
				arg = "+";
				Game.saveData.totalKoikatsuPoint += num;
			}
			_text.text = $"{arg}{num:0}";
		}

		public Tweener Move()
		{
			_transform.anchoredPosition = initPos;
			return _transform.DOAnchorPos(initPos + Vector3.up * _up, _duration).SetEase(_ease).SetUpdate(isIndependentUpdate: true);
		}
	}

	[SerializeField]
	private Mover.Type _type;

	[SerializeField]
	private RectTransform _mover;

	[SerializeField]
	private FadeCanvas _fadeCanvas;

	[SerializeField]
	private TMP_Text _text;

	[SerializeField]
	private float _duration;

	[SerializeField]
	private float _waitTime;

	[SerializeField]
	private AddPoint _addPoint;

	public static bool isActive
	{
		get
		{
			if (SingletonInitializer<KoikatsuPointUI>.initialized)
			{
				if (!SingletonInitializer<KoikatsuPointUI>.instance.processing)
				{
					return SingletonInitializer<KoikatsuPointUI>.instance._fadeCanvas.isFading;
				}
				return true;
			}
			return false;
		}
	}

	public static bool isChanged
	{
		get
		{
			if (SingletonInitializer<KoikatsuPointUI>.initialized)
			{
				return SingletonInitializer<KoikatsuPointUI>.instance.score < Game.Player.koikatsuPoint;
			}
			return false;
		}
	}

	public RectTransform mover => _mover;

	private int score { get; set; }

	private bool processing { get; set; }

	private CancellationTokenSource cancellationTokenSource { get; set; }

	private Tweener tweener { get; set; }

	public static void Clear()
	{
		if (SingletonInitializer<KoikatsuPointUI>.initialized)
		{
			int koikatsuPoint = Game.Player.koikatsuPoint;
			SingletonInitializer<KoikatsuPointUI>.instance.score = koikatsuPoint;
			SingletonInitializer<KoikatsuPointUI>.instance.SetScore(koikatsuPoint);
		}
	}

	public void Cancel()
	{
		Cancel(isCreate: false);
	}

	private CancellationTokenSource Cancel(bool isCreate)
	{
		if (this.cancellationTokenSource != null)
		{
			this.cancellationTokenSource.Cancel();
			this.cancellationTokenSource.Dispose();
		}
		return this.cancellationTokenSource = ((!isCreate) ? null : new CancellationTokenSource());
	}

	private void OnDisable()
	{
		End();
	}

	private void End()
	{
		tweener?.Kill();
		tweener = null;
		Cancel(isCreate: false);
	}

	protected override void Initialize()
	{
		_fadeCanvas.visibleForce = false;
		_addPoint.visibleForce = false;
		_addPoint.Initialize();
		Clear();
		(from _ in this.UpdateAsObservable()
			select Game.Player.koikatsuPoint).DistinctUntilChanged().Subscribe(delegate(int score)
		{
			if (!base.enabled || this.score >= score)
			{
				this.score = score;
				SetScore(score);
				Cancel(isCreate: false);
				_fadeCanvas.visibleForce = false;
				_addPoint.visibleForce = false;
				SetMoveInit();
			}
			else
			{
				Draw(score);
			}
		});
	}

	private void Draw(int score)
	{
		DrawAsync(score, Cancel(isCreate: true).Token).Forget();
	}

	private async UniTaskVoid DrawAsync(int score, CancellationToken token)
	{
		_ = 4;
		try
		{
			processing = true;
			_fadeCanvas.visible = true;
			Tweener tween = (this.tweener = SetMove(isStart: true));
			await tween.WithCancellation(token);
			_addPoint.visible = true;
			await UniTask.WhenAll(_addPoint.Move().WithCancellation(token), Add(score, token).WithCancellation(token));
			await UniTask.Delay(TimeSpan.FromSeconds(_waitTime), ignoreTimeScale: true, PlayerLoopTiming.Update, token);
			_addPoint.visible = false;
			await UniTask.WaitWhile(() => _addPoint.isFading, PlayerLoopTiming.Update, token);
			_fadeCanvas.visible = false;
			tween = (this.tweener = SetMove(isStart: false));
			await tween.WithCancellation(token);
		}
		catch (OperationCanceledException)
		{
			SetScore(this.score);
		}
		finally
		{
			this.tweener = null;
			processing = false;
		}
	}

	private void SetScore(int score)
	{
		_text.text = score.ToString("0");
	}

	private async UniTask Add(int target, CancellationToken token)
	{
		int now = score;
		score = target;
		_addPoint.Set(target, now);
		do
		{
			if (now < target)
			{
				int num = now + 1;
				now = num;
			}
			else if (now > target)
			{
				int num = now - 1;
				now = num;
			}
			SetScore(now);
			Utils.Sound.Play(SystemSE.get_point_add);
			await UniTask.Yield(PlayerLoopTiming.Update, token);
		}
		while (now != target);
		Utils.Sound.Play(SystemSE.get_point);
	}

	private Tweener SetMove(bool isStart)
	{
		return base.gameObject.SetPivotMove(_type, isStart, _mover, _duration).SetUpdate(isIndependentUpdate: true);
	}

	private Tweener SetMoveInit()
	{
		return base.gameObject.SetPivotMove(_type, isStart: false, _mover, 0f).SetUpdate(isIndependentUpdate: true);
	}
}
