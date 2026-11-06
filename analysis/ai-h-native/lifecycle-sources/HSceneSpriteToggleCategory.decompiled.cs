using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HSceneSpriteToggleCategory : MonoBehaviour
{
	public List<Toggle> lstToggle;

	public int GetToggleNum()
	{
		return lstToggle.Count;
	}

	public void SetEnable(bool _enable, int _array = -1)
	{
		if (_array < 0)
		{
			for (int i = 0; i < lstToggle.Count; i++)
			{
				if (lstToggle[i].interactable != _enable)
				{
					lstToggle[i].interactable = _enable;
				}
			}
		}
		else if (lstToggle.Count > _array && lstToggle[_array].interactable != _enable)
		{
			lstToggle[_array].interactable = _enable;
		}
	}

	public bool GetEnable(int _array)
	{
		if (lstToggle.Count > _array)
		{
			return lstToggle[_array].interactable;
		}
		return false;
	}

	public int GetAllEnable()
	{
		int num = 0;
		for (int i = 0; i < lstToggle.Count; i++)
		{
			if (lstToggle[i].interactable)
			{
				num++;
			}
		}
		return (num == lstToggle.Count) ? 1 : ((num != 0) ? 2 : 0);
	}

	public void SetActive(bool _active, int _array = -1)
	{
		if (_array < 0)
		{
			for (int i = 0; i < lstToggle.Count; i++)
			{
				if (lstToggle[i].isActiveAndEnabled != _active)
				{
					lstToggle[i].gameObject.SetActive(_active);
				}
			}
		}
		else if (lstToggle.Count > _array && lstToggle[_array].isActiveAndEnabled != _active)
		{
			lstToggle[_array].gameObject.SetActive(_active);
		}
	}

	public bool GetActive(int _array)
	{
		if (lstToggle.Count > _array)
		{
			return lstToggle[_array].isActiveAndEnabled;
		}
		return false;
	}

	public void SetCheck(bool _check, int _array = -1)
	{
		if (_array < 0)
		{
			for (int i = 0; i < lstToggle.Count; i++)
			{
				lstToggle[i].isOn = _check;
			}
		}
		else if (lstToggle.Count > _array)
		{
			lstToggle[_array].isOn = _check;
		}
	}
}
