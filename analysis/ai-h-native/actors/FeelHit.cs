using System.Collections.Generic;
using Manager;
using UnityEngine;

public class FeelHit
{
	public struct FeelHitInfo
	{
		public Vector2 area;

		public float rate;
	}

	public class FeelInfo
	{
		public List<Vector2> lstHit = new List<Vector2>();

		public List<FeelHitInfo> lstHitArea = new List<FeelHitInfo>();

		public List<float> lstChangeTime = new List<float>();

		public List<float> lstChangeDeltaTime = new List<float>();

		private Vector2 tmpv;

		public void CreateHit()
		{
			foreach (FeelHitInfo item in lstHitArea)
			{
				tmpv = Vector2.zero;
				tmpv.x = Random.Range(5, 95 - (int)item.rate);
				tmpv.y = tmpv.x + item.rate;
				lstHit.Add(tmpv);
			}
		}

		public bool ChangeHit(int _state)
		{
			if (_state >= lstChangeTime.Count || _state >= lstChangeDeltaTime.Count || _state >= lstHitArea.Count || _state >= lstHit.Count)
			{
				return false;
			}
			lstChangeDeltaTime[_state] += Time.deltaTime;
			if (lstChangeDeltaTime[_state] >= lstChangeTime[_state])
			{
				lstChangeTime[_state] = Random.Range(lstHitArea[_state].area.x, lstHitArea[_state].area.y);
				lstChangeDeltaTime[_state] = 0f;
				Vector2 value = lstHit[_state];
				value.x = Random.Range(5, 95 - (int)lstHitArea[_state].rate);
				value.y = value.x + lstHitArea[_state].rate;
				lstHit[_state] = value;
			}
			return true;
		}

		public void InitTime()
		{
			lstChangeTime.Clear();
			lstChangeDeltaTime.Clear();
			foreach (FeelHitInfo item2 in lstHitArea)
			{
				float item = Random.Range(item2.area.x, item2.area.y);
				lstChangeTime.Add(item);
				lstChangeDeltaTime.Add(0f);
			}
		}

		public Vector2 Get(int _state)
		{
			if (_state >= lstHit.Count)
			{
				return new Vector2(1.1f, 1.1f);
			}
			return lstHit[_state] * 0.01f;
		}
	}

	private List<FeelInfo> lstHitInfo = new List<FeelInfo>();

	public void FeelHitInit(int _personality = 0)
	{
		int num = _personality;
		if (num == 90)
		{
			num = 5;
		}
		if (!Singleton<Manager.Resources>.Instance.HSceneTable.DicLstHitInfo.TryGetValue(num, out lstHitInfo))
		{
			lstHitInfo = new List<FeelInfo>();
		}
		if (lstHitInfo.Count == 0)
		{
			return;
		}
		foreach (FeelInfo item in lstHitInfo)
		{
			item.CreateHit();
			item.InitTime();
		}
	}

	public bool isHit(int _state, int _loop, float _power)
	{
		Vector2 hitArea = GetHitArea(_state, _loop);
		return GlobalMethod.RangeOn(_power, hitArea.x, hitArea.y);
	}

	public Vector2 GetHitArea(int _state, int _loop)
	{
		if (_state >= lstHitInfo.Count)
		{
			return new Vector2(1.1f, 1.1f);
		}
		return lstHitInfo[_state].Get(_loop);
	}

	public bool ChangeHit(int _state, int _loop)
	{
		return lstHitInfo[_state].ChangeHit(_loop);
	}

	public void InitTime()
	{
		for (int i = 0; i < lstHitInfo.Count; i++)
		{
			lstHitInfo[i].InitTime();
		}
	}
}
