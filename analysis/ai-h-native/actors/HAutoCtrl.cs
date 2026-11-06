using System;
using System.Collections.Generic;
using AIChara;
using Manager;
using UnityEngine;
using UnityEx;

public class HAutoCtrl : MonoBehaviour
{
	public class AutoRandom
	{
		public class AutoRandomDate
		{
			public int mode = -1;

			public int id = -1;
		}

		protected class CCheck
		{
			public AutoRandomDate date;

			public float rate;

			public float minVal;

			public float maxVal;
		}

		private class ListComparer : IComparer<UnityEx.ValueTuple<Guid, int>>
		{
			public int Compare(UnityEx.ValueTuple<Guid, int> a, UnityEx.ValueTuple<Guid, int> b)
			{
				return SortCompare(a.Item1, b.Item1);
			}

			private int SortCompare<T>(T a, T b) where T : IComparable
			{
				return a.CompareTo(b);
			}
		}

		private float allVal;

		private List<CCheck> backup = new List<CCheck>();

		private List<CCheck> checks = new List<CCheck>();

		private List<UnityEx.ValueTuple<Guid, int>> tmpTuple = new List<UnityEx.ValueTuple<Guid, int>>();

		private ListComparer backupSortCompare = new ListComparer();

		public AutoRandom()
		{
			allVal = 0f;
		}

		public bool Add(AutoRandomDate _date, float _rate)
		{
			if (_rate == 0f)
			{
				GlobalMethod.DebugLog("ランダム 追加登録個数が0");
				return false;
			}
			if (checks.Exists((CCheck i) => i.date.mode == _date.mode && i.date.id == _date.id))
			{
				GlobalMethod.DebugLog("ランダム 重複登録");
				return false;
			}
			CCheck cCheck = new CCheck();
			cCheck.date = _date;
			cCheck.rate = _rate;
			backup.Add(cCheck);
			RandomSort();
			allVal = 0f;
			foreach (CCheck check in checks)
			{
				check.minVal = allVal;
				check.maxVal = allVal + check.rate;
				allVal += check.rate;
			}
			return true;
		}

		private void RandomSort()
		{
			tmpTuple.Clear();
			for (int i = 0; i < backup.Count; i++)
			{
				tmpTuple.Add(new UnityEx.ValueTuple<Guid, int>(Guid.NewGuid(), i));
			}
			tmpTuple.Sort(backupSortCompare);
			checks.Clear();
			for (int j = 0; j < tmpTuple.Count; j++)
			{
				checks.Add(backup[tmpTuple[j].Item2]);
			}
		}

		public AutoRandomDate Random()
		{
			if (IsEmpty())
			{
				return new AutoRandomDate();
			}
			float randVal = UnityEngine.Random.Range(0f, allVal);
			CCheck cCheck = checks.Find((CCheck x) => randVal >= x.minVal && randVal <= x.maxVal);
			if (cCheck == null)
			{
				return new AutoRandomDate();
			}
			return cCheck.date;
		}

		public bool IsEmpty()
		{
			return backup.Count == 0;
		}

		public void Clear()
		{
			allVal = 0f;
			checks.Clear();
			backup.Clear();
		}
	}

	[Serializable]
	public class AutoTime
	{
		[DisabledGroup("最小最大")]
		public Vector2 minmax;

		[DisabledGroup("時間まで")]
		public float time;

		[DisabledGroup("経過時間")]
		public float timeDelta;

		public void Reset()
		{
			time = UnityEngine.Random.Range(minmax.x, minmax.y);
			timeDelta = 0f;
		}

		public bool IsTime()
		{
			timeDelta = Mathf.Clamp(timeDelta + Time.deltaTime, 0f, time);
			return timeDelta >= time;
		}
	}

	[Serializable]
	public class AutoLeaveItToYou
	{
		public AutoTime time = new AutoTime();

		[Label("元の変更時間")]
		public Vector2 baseTime = Vector2.zero;

		[Label("おまかせにいく確率")]
		public int rate = 50;
	}

	[Serializable]
	public class HAutoInfo
	{
		public AutoTime start = new AutoTime();

		public AutoTime reStart = new AutoTime();

		public AutoTime speed = new AutoTime();

		public AutoTime loopChange = new AutoTime();

		public AutoTime motionChange = new AutoTime();

		public AutoTime pull = new AutoTime();

		[DisabledGroup("スピード変更のリープ時間")]
		public float lerpTimeSpeed;

		[DisabledGroup("弱ループ率")]
		public int rateWeakLoop;

		[DisabledGroup("当たりに向かう率")]
		public int rateHit;

		[DisabledGroup("体位変更性癖加算")]
		public float rateAddMotionChange;

		[DisabledGroup("リスタート時の体位変更率")]
		public int rateRestartMotionChange;

		[DisabledGroup("中出しした時に抜く確率")]
		public float rateInsertPull;

		[DisabledGroup("中出しされてない時に抜く確率")]
		public float rateNotInsertPull;
	}

	private HAutoInfo info;

	[DisabledGroup("今のスピード")]
	public float centerSpeed;

	[DisabledGroup("リープ時間")]
	public float timeLerp;

	[DisabledGroup("リープ先")]
	public Vector2 lerp = new Vector2(-1f, -1f);

	[Label("リープアニメーション")]
	public AnimationCurve lerpCurve;

	[DisabledGroup("抜く確率判定")]
	public bool isPulljudge;

	public AutoLeaveItToYou autoLeave;

	public void Load(string _strAssetPath, int _personal, int _attribute = 0)
	{
		lerp = new Vector2(-1f, -1f);
		if (LoadAuto())
		{
			autoLeave.time.minmax = new Vector2(20f, 20f);
			autoLeave.time.Reset();
			autoLeave.rate = 50;
			if (LoadAutoLeaveItToYou() && LoadAutoLeaveItToYouPersonality(_personal) && LoadAutoLeaveItToYouAttribute(_attribute))
			{
			}
		}
	}

	private bool LoadAuto()
	{
		info = Singleton<Manager.Resources>.Instance.HSceneTable.HAutoInfo;
		return info != null;
	}

	private bool LoadAutoLeaveItToYou()
	{
		autoLeave = Singleton<Manager.Resources>.Instance.HSceneTable.HAutoLeaveItToYou;
		return autoLeave != null;
	}

	private bool LoadAutoLeaveItToYouPersonality(int _personal)
	{
		if (!Singleton<Manager.Resources>.Instance.HSceneTable.autoLeavePersonalityRate.TryGetValue(_personal, out var value))
		{
			value = 1f;
		}
		autoLeave.rate = Mathf.CeilToInt((float)autoLeave.rate * value);
		return true;
	}

	private bool LoadAutoLeaveItToYouAttribute(int _attribute)
	{
		if (!Singleton<Manager.Resources>.Instance.HSceneTable.autoLeaveAttributeRate.TryGetValue(_attribute, out var value))
		{
			value = 1f;
		}
		autoLeave.rate = Mathf.CeilToInt((float)autoLeave.rate * value);
		return true;
	}

	public void Reset()
	{
		StartInit();
		ReStartInit();
		SpeedInit();
		LoopMotionInit();
		MotionChangeInit();
		PullInit();
	}

	public void StartInit()
	{
		info.start.Reset();
	}

	public bool IsStart()
	{
		return info.start.IsTime();
	}

	public void ReStartInit()
	{
		info.reStart.Reset();
	}

	public bool IsReStart()
	{
		return info.reStart.IsTime();
	}

	public void SpeedInit()
	{
		info.speed.Reset();
		centerSpeed = 0f;
	}

	public bool AddSpeed(float _wheel, int _loop)
	{
		if (lerp.x >= 0f && lerp.y >= 0f)
		{
			return false;
		}
		bool result = false;
		centerSpeed += _wheel;
		switch (_loop)
		{
		case 0:
			if (centerSpeed > 1f)
			{
				result = true;
			}
			break;
		case 1:
			if (centerSpeed < 1f)
			{
				result = true;
			}
			break;
		}
		if (_wheel != 0f)
		{
			info.speed.Reset();
		}
		if (_loop != 2)
		{
			centerSpeed = Mathf.Clamp(centerSpeed, 0f, 2f);
		}
		else
		{
			centerSpeed = Mathf.Clamp(centerSpeed, 0f, 1f);
		}
		return result;
	}

	public void LoopMotionInit()
	{
		info.loopChange.Reset();
	}

	public bool ChangeLoopMotion(bool _loop)
	{
		if (lerp.x >= 0f && lerp.y >= 0f)
		{
			return false;
		}
		if (!info.loopChange.IsTime())
		{
			return false;
		}
		info.loopChange.Reset();
		ShuffleRand shuffleRand = new ShuffleRand();
		shuffleRand.Init(100);
		return IsChangeLoop(_loop, info.rateWeakLoop < shuffleRand.Get());
	}

	private bool IsChangeLoop(bool _loop, bool _changeLoop)
	{
		if (_loop == _changeLoop)
		{
			return false;
		}
		centerSpeed = 1f;
		return true;
	}

	public bool ChangeSpeed(bool _loop, Vector2 _hit)
	{
		if (lerp.x >= 0f && lerp.y >= 0f)
		{
			return false;
		}
		if (!info.speed.IsTime())
		{
			return false;
		}
		info.speed.Reset();
		timeLerp = 0f;
		ShuffleRand shuffleRand = new ShuffleRand();
		if (_hit.x >= 0f)
		{
			shuffleRand.Init(100);
			if (info.rateHit > shuffleRand.Get())
			{
				lerp.x = (_loop ? (centerSpeed - 1f) : centerSpeed);
				lerp.y = UnityEngine.Random.Range(_hit.x, _hit.y);
				return false;
			}
		}
		shuffleRand.Init(100);
		int num;
		do
		{
			num = shuffleRand.Get();
		}
		while (GlobalMethod.RangeOn(num, (int)(_hit.x * 100f), (int)(_hit.y * 100f)));
		lerp.x = (_loop ? (centerSpeed - 1f) : centerSpeed);
		lerp.y = (float)num * 0.01f;
		return false;
	}

	public float GetSpeed(bool _loop)
	{
		if (lerp.x < 0f && lerp.y < 0f)
		{
			return _loop ? (centerSpeed - 1f) : centerSpeed;
		}
		timeLerp = Mathf.Clamp(timeLerp + Time.deltaTime, 0f, info.lerpTimeSpeed);
		centerSpeed = Mathf.Lerp(lerp.x, lerp.y, lerpCurve.Evaluate(Mathf.InverseLerp(0f, info.lerpTimeSpeed, timeLerp)));
		if (_loop)
		{
			centerSpeed += 1f;
		}
		if (timeLerp >= info.lerpTimeSpeed)
		{
			lerp = new Vector2(-1f, -1f);
			timeLerp = 0f;
		}
		return _loop ? (centerSpeed - 1f) : centerSpeed;
	}

	public void SetSpeed(float _speed)
	{
		centerSpeed = _speed;
	}

	public void MotionChangeInit()
	{
		info.motionChange.Reset();
	}

	public bool IsChangeActionAtLoop()
	{
		if (lerp.x >= 0f && lerp.y >= 0f)
		{
			return false;
		}
		if (!info.motionChange.IsTime())
		{
			return false;
		}
		info.motionChange.Reset();
		return true;
	}

	public bool IsChangeActionAtRestart()
	{
		ShuffleRand shuffleRand = new ShuffleRand();
		shuffleRand.Init(100);
		return shuffleRand.Get() < info.rateRestartMotionChange;
	}

	public HScene.StartMotion GetAnimation(List<HScene.AnimationListInfo>[] _listAnim, int _initiative, bool _isFirst = false)
	{
		bool flag = true;
		AutoRandom autoRandom = new AutoRandom();
		for (int i = 0; i < _listAnim.Length; i++)
		{
			for (int j = 0; j < _listAnim[i].Count; j++)
			{
				if (!_listAnim[i][j].nPositons.Contains(Singleton<HSceneFlagCtrl>.Instance.nPlace))
				{
					continue;
				}
				if (Singleton<HSceneManager>.Instance.Player.ChaControl.sex == 0 || (Singleton<HSceneManager>.Instance.Player.ChaControl.sex == 1 && Singleton<HSceneManager>.Instance.bFutanari))
				{
					if (i == 4)
					{
						continue;
					}
					ChaControl[] females = Singleton<HSceneManager>.Instance.HSceneSet.GetComponentInChildren<HScene>().GetFemales();
					if (females[1] == null && i == 5)
					{
						continue;
					}
				}
				else if (i != 4)
				{
					continue;
				}
				if (_initiative == 1)
				{
					if (_listAnim[i][j].nInitiativeFemale != 1 && (!flag || _listAnim[i][j].nInitiativeFemale != 2))
					{
						continue;
					}
				}
				else if (_initiative != 2 || _listAnim[i][j].nInitiativeFemale != 2)
				{
					continue;
				}
				AutoRandom.AutoRandomDate autoRandomDate = new AutoRandom.AutoRandomDate();
				autoRandomDate.mode = i;
				autoRandomDate.id = _listAnim[i][j].id;
				autoRandom.Add(autoRandomDate, 10f + ((!Singleton<HSceneManager>.Instance.HSkil.ContainsKey(_listAnim[i][j].nFemaleProclivity)) ? 0f : info.rateAddMotionChange));
				if (!_isFirst)
				{
					continue;
				}
				break;
			}
		}
		AutoRandom.AutoRandomDate autoRandomDate2 = autoRandom.Random();
		return new HScene.StartMotion(autoRandomDate2.mode, autoRandomDate2.id);
	}

	public void PullInit()
	{
		info.pull.Reset();
		isPulljudge = false;
	}

	public bool IsPull(bool _isInsert)
	{
		if (isPulljudge)
		{
			return false;
		}
		if (!info.pull.IsTime())
		{
			return false;
		}
		info.pull.Reset();
		isPulljudge = true;
		ShuffleRand shuffleRand = new ShuffleRand();
		shuffleRand.Init(100);
		int num = shuffleRand.Get();
		return (!_isInsert) ? ((float)num < info.rateNotInsertPull) : ((float)num < info.rateInsertPull);
	}

	public bool IsAutoAutoLeaveItToYou(ChaControl _female, HScene.AnimationListInfo _ali, bool _isAutoLeaveItToYouButton, bool _isInitiative)
	{
		if (_female == null)
		{
			return false;
		}
		if (_ali == null)
		{
			return false;
		}
		AnimatorStateInfo animatorStateInfo = _female.getAnimatorStateInfo(0);
		if (_isInitiative)
		{
			return false;
		}
		if (!_isAutoLeaveItToYouButton)
		{
			return false;
		}
		if (!animatorStateInfo.IsName("Idle"))
		{
			return false;
		}
		if (_ali.nAnimListInfoID == 3)
		{
			return false;
		}
		if (!autoLeave.time.IsTime())
		{
			return false;
		}
		ShuffleRand shuffleRand = new ShuffleRand();
		shuffleRand.Init(100);
		bool flag = shuffleRand.Get() < autoLeave.rate;
		if (!flag)
		{
			autoLeave.time.minmax.x = Mathf.Max(autoLeave.time.minmax.x - 5f, 0f);
			autoLeave.time.minmax.y = Mathf.Max(autoLeave.time.minmax.y - 5f, 0f);
		}
		else
		{
			autoLeave.time.minmax = autoLeave.baseTime;
		}
		autoLeave.time.Reset();
		return flag;
	}

	public void AutoAutoLeaveItToYouInit()
	{
		autoLeave.time.minmax = autoLeave.baseTime;
		autoLeave.time.Reset();
	}
}
