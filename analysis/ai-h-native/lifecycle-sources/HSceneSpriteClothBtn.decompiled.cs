using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class HSceneSpriteClothBtn : MonoBehaviour
{
	private int state;

	public Button[] buttons;

	private int State
	{
		[CompilerGenerated]
		get
		{
			return state;
		}
	}

	public void SetButton(int State)
	{
		state = State;
		if (State >= buttons.Length)
		{
			state = buttons.Length - 1;
		}
		for (int i = 0; i < buttons.Length; i++)
		{
			if (i != state)
			{
				buttons[i].gameObject.SetActive(value: false);
			}
			else
			{
				buttons[i].gameObject.SetActive(value: true);
			}
		}
	}
}
