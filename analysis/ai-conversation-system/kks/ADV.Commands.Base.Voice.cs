using System;
using System.Collections.Generic;
using Illusion.Extensions;
using Manager;
using UnityEngine;

namespace ADV.Commands.Base;

public class Voice : CommandBase
{
	public class Data : TextScenario.IVoice
	{
		public int no { get; private set; }

		public string bundle { get; set; }

		public string asset { get; private set; }

		public int personality { get; set; }

		public float pitch { get; set; }

		public ChaControl chaCtrl { get; set; }

		public Transform transform { get; set; }

		public Info.Audio.Eco eco { get; set; }

		public bool is2D { get; set; }

		public bool isNotMoveMouth { get; set; }

		public bool usePersonality { get; private set; }

		public bool usePitch { get; private set; }

		public AudioSource audio { get; private set; }

		public void Convert2D()
		{
			transform = null;
		}

		public Data(string[] args, ref int cnt)
		{
			pitch = 1f;
			try
			{
				no = int.Parse(args[cnt++]);
				bundle = args.SafeGet(cnt++);
				asset = args.SafeGet(cnt++);
				usePersonality = args.SafeProc(cnt++, delegate(string s)
				{
					personality = int.Parse(s);
				});
				usePitch = args.SafeProc(cnt++, delegate(string s)
				{
					pitch = float.Parse(s);
				});
			}
			catch (Exception)
			{
			}
		}

		public AudioSource Play()
		{
			Manager.Voice.Loader loader = new Manager.Voice.Loader
			{
				no = personality,
				bundle = bundle,
				asset = asset,
				voiceTrans = transform,
				pitch = pitch
			};
			audio = Manager.Voice.Play(loader);
			if (eco != null)
			{
				AudioEchoFilter audioEchoFilter = audio.gameObject.AddComponent<AudioEchoFilter>();
				audioEchoFilter.delay = eco.delay;
				audioEchoFilter.decayRatio = eco.decayRatio;
				audioEchoFilter.wetMix = eco.wetMix;
				audioEchoFilter.dryMix = eco.dryMix;
			}
			if (chaCtrl != null && transform != null)
			{
				chaCtrl.SetLipSync(isNotMoveMouth ? null : audio);
			}
			if (audio != null && transform != null)
			{
				audio.spatialBlend = 0f;
			}
			return audio;
		}

		public bool Wait()
		{
			return Manager.Voice.IsPlay(personality, transform, isLoopCheck: false);
		}
	}

	public override string[] ArgsLabel => new string[5] { "No", "Bundle", "Asset", "Personality", "Pitch" };

	public override string[] ArgsDefault => new string[1] { int.MaxValue.ToString() };

	public override void Do()
	{
		base.Do();
		TextScenario.CurrentCharaData currentCharaData = base.scenario.currentCharaData;
		currentCharaData.CreateVoiceList();
		List<Data> list = new List<Data>();
		if (args.Length > 1)
		{
			int cnt = 0;
			while (!args.IsNullOrEmpty(cnt))
			{
				Data data = new Data(args, ref cnt);
				CharaData chara = base.scenario.commandController.GetChara(data.no);
				if (chara != null)
				{
					data.transform = chara.voiceTrans;
					data.chaCtrl = chara.chaCtrl;
					if (!data.usePersonality)
					{
						data.personality = chara.voiceNo;
					}
					if (!data.usePitch)
					{
						data.pitch = chara.voicePitch;
					}
				}
				data.is2D = base.scenario.info.audio.is2D;
				data.isNotMoveMouth = base.scenario.info.audio.isNotMoveMouth;
				if (base.scenario.info.audio.eco.use)
				{
					data.eco = base.scenario.info.audio.eco.DeepCopy();
				}
				list.Add(data);
			}
		}
		List<TextScenario.IVoice[]> voiceList = currentCharaData.voiceList;
		TextScenario.IVoice[] item = list.ToArray();
		voiceList.Add(item);
		foreach (Data item2 in list)
		{
			if (item2.bundle.IsNullOrEmpty())
			{
				if (base.scenario.currentCharaData.bundleVoices.TryGetValue(item2.personality, out var value))
				{
					item2.bundle = value;
				}
			}
			else
			{
				base.scenario.currentCharaData.bundleVoices[item2.personality] = item2.bundle;
			}
		}
	}
}
