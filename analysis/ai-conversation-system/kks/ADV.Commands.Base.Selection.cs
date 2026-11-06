using System;
using System.Linq;
using System.Text.RegularExpressions;
using Illusion.Game;
using Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ADV.Commands.Base;

public class Selection : CommandBase
{
	private int id = -1;

	private string pattern;

	private bool isKeep;

	public override string[] ArgsLabel => new string[3] { "Visible", "Pattern", "isKeep" };

	public override string[] ArgsDefault => new string[3]
	{
		bool.TrueString,
		string.Empty,
		string.Empty
	};

	public override void ConvertBeforeArgsProc()
	{
		base.ConvertBeforeArgsProc();
		args.SafeProc(1, delegate(string s)
		{
			pattern = s;
		});
	}

	public override void Do()
	{
		base.Do();
		base.scenario.ChoicesInit();
		bool num = bool.Parse(args[0]);
		bool.TryParse(args.SafeGet(2) ?? string.Empty, out isKeep);
		if (!num)
		{
			base.scenario.textController.Clear();
		}
		else if (!base.scenario.textController.MessageWindow.text.IsNullOrEmpty())
		{
			(base.scenario as MainScenario).SafeProc(delegate(MainScenario main)
			{
				main.BackLog.Logs.SafeProc(main.BackLog.Logs.Count - 1, delegate(BackLog.Data log)
				{
					if (!log.voiceList.IsNullOrEmpty())
					{
						base.scenario.currentCharaData.CreateVoiceList();
						base.scenario.currentCharaData.voiceList.AddRange(log.voiceList);
					}
				});
			});
		}
		RectTransform rectTransform = base.scenario.Choices.GetChild(0) as RectTransform;
		foreach (var item in base.scenario.choiceList.Select((TextScenario.ChoiceData s, int index) => new { s, index }))
		{
			GameObject gameObject = UnityEngine.Object.Instantiate(rectTransform.gameObject, base.scenario.Choices);
			gameObject.SetActive(value: true);
			gameObject.name = "Choice" + (item.index + 1);
			gameObject.GetComponentInChildren<UnityEngine.UI.Text>().text = item.s.text;
			Button bt = gameObject.GetComponent<Button>();
			bt.onClick.RemoveAllListeners();
			bt.onClick.AddListener(delegate
			{
				id = ButtonProc(bt.name);
			});
			gameObject.SetActive(value: true);
		}
		base.scenario.isSkip = base.scenario.isSkip && Manager.Config.TextData.ChoicesSkip;
		base.scenario.isAuto = base.scenario.isAuto && Manager.Config.TextData.ChoicesAuto;
		base.scenario.isChoice = true;
		base.scenario.Choices.gameObject.SetActive(value: true);
		static int ButtonProc(string btName)
		{
			Utils.Sound.Play(SystemSE.sel);
			int result = 0;
			try
			{
				int.TryParse(Regex.Replace(btName, "[^0-9]", string.Empty), out result);
			}
			catch (Exception)
			{
			}
			return result - 1;
		}
	}

	public override bool Process()
	{
		base.Process();
		return id >= 0;
	}

	public override void Result(bool processEnd)
	{
		base.Result(processEnd);
		if (!processEnd)
		{
			return;
		}
		if (id >= 0)
		{
			string jump = base.scenario.choiceList[id].jump;
			string selectTitle = string.Empty;
			(base.scenario as MainScenario).SafeProc(delegate(MainScenario main)
			{
				main.BackLog.Add(new BackLog.Data(base.localLine, new Text.Data(new string[2]
				{
					selectTitle,
					base.scenario.choiceList[id].text
				}), null));
			});
			foreach (Transform choice in base.scenario.Choices)
			{
				if (choice.gameObject.activeSelf)
				{
					UnityEngine.Object.Destroy(choice.gameObject);
				}
			}
			base.scenario.ChoicesInit();
			if (!pattern.IsNullOrEmpty())
			{
				base.scenario.Vars[pattern] = new ValData(id);
			}
			base.scenario.SearchTagJumpOrOpenFile(jump, base.localLine);
		}
		if (!isKeep)
		{
			base.scenario.choiceList.Clear();
		}
	}
}
