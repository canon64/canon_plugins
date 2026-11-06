using System;
using System.Collections.Generic;
using System.Linq;
using AIChara;
using AIProject;
using Manager;
using UnityEngine;

namespace ADV.Commands.Base;

public class Text : CommandBase
{
	public class Data
	{
		public string name = string.Empty;

		public string text = string.Empty;

		public string colorKey { get; private set; }

		public Data(params string[] args)
		{
			int num = 1;
			int index = num;
			string obj = args.SafeGet(0) ?? string.Empty;
			string text = obj;
			name = obj;
			colorKey = text;
			this.text = args.SafeGet(index) ?? args.SafeGet(num) ?? string.Empty;
		}
	}

	private class Next
	{
		private int currentNo;

		private List<List<Action>> playList = new List<List<Action>>();

		private TextScenario scenario;

		private Action onChange;

		private bool voicePlayEnd;

		public Next(TextScenario scenario)
		{
			Next next = this;
			this.scenario = scenario;
			TextScenario.CurrentCharaData currentCharaData = scenario.currentCharaData;
			List<TextScenario.IMotion[]> motionList = currentCharaData.motionList;
			List<TextScenario.IExpression[]> expressionList = currentCharaData.expressionList;
			int cnt = 0;
			Func<bool> func = () => !motionList.IsNullOrEmpty() && motionList.SafeGet(cnt) != null;
			Func<bool> func2 = () => !expressionList.IsNullOrEmpty() && expressionList.SafeGet(cnt) != null;
			while (func() || func2())
			{
				TextScenario.IMotion[] motion = (func() ? motionList[cnt] : null);
				TextScenario.IExpression[] expression = (func2() ? expressionList[cnt] : null);
				playList.Add(new List<Action>
				{
					delegate
					{
						next.Play(expression);
					},
					delegate
					{
						next.Play(motion);
					}
				});
				cnt++;
			}
			if (!currentCharaData.voiceList.IsNullOrEmpty())
			{
				onChange = delegate
				{
					next.Play();
				};
				scenario.VoicePlay(currentCharaData.voiceList, onChange, delegate
				{
					next.voicePlayEnd = true;
				});
			}
		}

		private bool Play()
		{
			return playList.SafeProc(currentNo++, delegate(List<Action> p)
			{
				p.ForEach(delegate(Action proc)
				{
					proc();
				});
			});
		}

		public bool Process()
		{
			if (scenario.currentCharaData.isSkip)
			{
				return true;
			}
			if (playList.Count <= currentNo && voicePlayEnd)
			{
				return true;
			}
			if (onChange == null)
			{
				bool flag = false;
				List<TextScenario.IMotion[]> motionList = scenario.currentCharaData.motionList;
				if (currentNo == 0)
				{
					flag = true;
				}
				else
				{
					bool flag2 = false;
					if (!motionList.IsNullOrEmpty() && currentNo < motionList.Count)
					{
						flag2 = !motionList[currentNo].IsNullOrEmpty();
					}
					TextScenario.IMotion[] array = ((!flag2) ? null : motionList[currentNo - 1]);
					flag = array.IsNullOrEmpty() || MotionEndCheck(array);
				}
				if (flag && !Play())
				{
					bool lastMotionEnd = true;
					bool flag3 = !motionList.IsNullOrEmpty();
					if (flag3)
					{
						flag3 = motionList.LastOrDefault().SafeProc(delegate(TextScenario.IMotion[] last)
						{
							lastMotionEnd = MotionEndCheck(last);
						});
					}
					if (flag3 && !lastMotionEnd)
					{
						return false;
					}
					return true;
				}
			}
			return false;
		}

		public void Result()
		{
			while (currentNo < playList.Count)
			{
				Play();
			}
		}

		private void Play(TextScenario.IMotion[] motionList)
		{
			if (!motionList.IsNullOrEmpty())
			{
				scenario.CrossFadeStart();
				foreach (TextScenario.IMotion motion in motionList)
				{
					motion.Play(scenario);
				}
			}
		}

		private void Play(TextScenario.IExpression[] expressionList)
		{
			if (!expressionList.IsNullOrEmpty())
			{
				foreach (TextScenario.IExpression expression in expressionList)
				{
					expression.Play(scenario);
				}
			}
		}

		private bool MotionEndCheck(TextScenario.IMotion[] list)
		{
			Func<ChaControl, bool> endCheck = (ChaControl chaCtrl) => chaCtrl.getAnimatorStateInfo(0).normalizedTime >= 1f;
			return list.All((TextScenario.IMotion motion) => endCheck(motion.GetChara(scenario).chaCtrl));
		}
	}

	private Next next;

	public override string[] ArgsLabel => new string[2] { "Name", "Text" };

	public override string[] ArgsDefault => null;

	public override void Convert(string fileName, ref string[] args)
	{
		int index = 1;
		string text = args.SafeGet(index);
		if (text.IsNullOrEmpty())
		{
			Debug.LogWarningFormat("FileName:{0}, Command TEXT text none\nArgs:{1}", fileName, string.Join(",", args));
		}
		else
		{
			args[index++] = string.Join("\n", text.Split(new string[1] { "@br" }, StringSplitOptions.None));
		}
	}

	public override void Do()
	{
		base.Do();
		Data data = new Data(args);
		base.scenario.fontColorKey = data.name;
		if (data.name != string.Empty)
		{
			data.name = base.scenario.ReplaceText(data.name);
		}
		if (data.text != string.Empty)
		{
			data.text = base.scenario.ReplaceText(data.text);
		}
		TextScenario.CurrentCharaData currentCharaData = base.scenario.currentCharaData;
		if (currentCharaData.isSkip || Config.GameData.NextVoiceStop)
		{
			base.scenario.VoicePlay(null, null, null);
		}
		currentCharaData.isSkip = false;
		base.scenario.captionSystem.SetName(data.name);
		base.scenario.captionSystem.SetText(data.text);
		string text = null;
		switch (data.colorKey)
		{
		case "[H]":
			text = MapUIContainer.CharaNameColor(base.scenario.currentChara?.data?.actor);
			break;
		case "[P]":
			text = MapUIContainer.CharaNameColor(base.scenario.player?.actor);
			break;
		case "[M]":
		{
			Actor actor = base.scenario.currentChara?.data?.merchantActor;
			if (actor == null)
			{
				actor = base.scenario.heroineList?.FirstOrDefault((TextScenario.ParamData p) => p.merchantActor != null)?.actor;
			}
			text = MapUIContainer.CharaNameColor(actor);
			break;
		}
		}
		if (text != null)
		{
			data.name = text;
		}
		base.scenario.TextLogCall(data, currentCharaData.voiceList);
		next = new Next(base.scenario);
	}

	public override bool Process()
	{
		base.Process();
		return next.Process();
	}

	public override void Result(bool processEnd)
	{
		base.Result(processEnd);
		next.Result();
	}
}
