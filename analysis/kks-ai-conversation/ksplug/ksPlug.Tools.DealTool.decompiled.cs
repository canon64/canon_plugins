using System;
using System.Collections;
using System.Collections.Generic;
using ActionGame;
using Manager;
using SaveData;
using UnityEngine;
using ksPlug.Character;

namespace ksPlug.Tools;

public class DealTool
{
	private static bool addBreastPusher;

	private static bool addBellyCtrl;

	private static bool enableAhegao;

	private static int faceShdwFade;

	private static int bodyShdwFade;

	public static ChaControl LatestCreatedCha;

	public static void UnlockAllEcchiPoses()
	{
		Dictionary<int, HashSet<int>> dictionary = new Dictionary<int, HashSet<int>>();
		for (int i = 0; i < 8; i++)
		{
			HashSet<int> hashSet = new HashSet<int>();
			for (int j = 0; j < 50; j++)
			{
				hashSet.Add(j);
			}
			dictionary[i] = hashSet;
		}
		Game.globalData.playHList = dictionary;
	}

	public static List<Heroine> ContextualSaveListForComparison(ChaCore chaCore)
	{
		List<Heroine> list = new List<Heroine>();
		if (Core.dealer.InH)
		{
			list = new List<Heroine> { chaCore.HSaveData };
		}
		if (Core.dealer.Ctx == Dealer.Context.Esthetic)
		{
			list = new List<Heroine> { chaCore.HSaveData };
		}
		if (Core.dealer.Ctx == Dealer.Context.Event)
		{
			if (Object.op_Implicit((Object)(object)Singleton<ActionScene>.Instance))
			{
				HScene instance = Singleton<HScene>.Instance;
				if ((Object)(object)instance != (Object)null && Singleton<ActionScene>.Instance.isPenetration)
				{
					list = instance.GetFieldValue<List<Heroine>>("heroineList");
				}
			}
			if (list.Count == 0 && Core.dealer.eventInfo != null)
			{
				list = Core.dealer.eventInfo.saves;
			}
		}
		return list;
	}

	public static void DealerCycleChange()
	{
		foreach (KeyValuePair<Heroine, Statistic.Daily> item in Statistic.DailyDic)
		{
			if (!item.Value.isRiskyDay)
			{
				item.Value.bellyBuldge = 0f;
			}
			if (item.Value.inAhegao && Settings.AhegaoCount == Settings.AhegCount.EcchiSession && Settings.AhegaoStay == Settings.AhegPersist.DaySection)
			{
				item.Value.inAhegao = false;
			}
		}
	}

	public static void ParseInteractiveOptions()
	{
		if (faceShdwFade != Settings.FaceShdwFade)
		{
			ChaCore.RefreshFacesShadow();
			faceShdwFade = Settings.FaceShdwFade;
		}
		if (bodyShdwFade != Settings.BodyShdwFade)
		{
			ChaCore.RefreshBodiesShadow();
			bodyShdwFade = Settings.BodyShdwFade;
		}
	}

	public static void ParseRefreshableOptions()
	{
		if (!addBreastPusher && Settings.AddBreastPusher)
		{
			BreastPusher.AddOnAllNpcs();
		}
		if (!addBellyCtrl && Settings.AddBellyCtrl)
		{
			BellyCtrl.AddOnAllNpcs();
		}
		if (!enableAhegao && Settings.EnableAhegaoG)
		{
			ResetDailiesAhegao();
		}
		ConformOptionGhosts();
	}

	public static void ResetDailiesAhegao()
	{
		foreach (Statistic.Daily item in Statistic.AllDailies())
		{
			item.inAhegao = false;
		}
	}

	public static void ConformOptionGhosts()
	{
		addBreastPusher = Settings.AddBreastPusher;
		addBellyCtrl = Settings.AddBellyCtrl;
		enableAhegao = Settings.EnableAhegaoG;
	}

	public static void CorrectNamesAndTweakRelations()
	{
		string text = "Arisa Onoe";
		List<Heroine> list = new List<Heroine>();
		List<Heroine> list2 = new List<Heroine>();
		foreach (Heroine heroine in Game.saveData.heroineList)
		{
			list.Add(heroine);
			if (ChaCore.Name(((CharaData)heroine).parameter) == text)
			{
				list2.Add(heroine);
			}
		}
		if (list2.Count > 1)
		{
			((CharaData)list2[0]).parameter.firstname = "Miyami";
			((CharaData)list2[0]).parameter.lastname = "Takatsu";
			Console.WriteLine("!!! found double name and correct it");
		}
		List<Statistic.ExpTweak> list3 = new List<Statistic.ExpTweak>
		{
			Statistic.ExpTweak.FirstLove,
			Statistic.ExpTweak.Tied,
			Statistic.ExpTweak.JustFriend,
			Statistic.ExpTweak.CloseFriend,
			Statistic.ExpTweak.Flirt
		};
		List<string> list4 = new List<string>();
		foreach (Heroine item in list)
		{
			if (item.fixCharaID == 0)
			{
				Statistic.ExpTweak expTweak = Helper.RandomInList(list3);
				Statistic.TweakNpcRelation(item, expTweak);
				list4.Add("\"" + ChaCore.Name(((CharaData)item).parameter) + "\" set to \"" + expTweak.ToString() + "\"");
			}
		}
		Core.dealer.Msgn.ReceiveStringList(list4);
	}

	public static IEnumerator IE_GetNews(Heroine her)
	{
		_ = Game.Player.whreLiveSelection;
		Game.Player.whreLiveSelection = ((CharaData)her).schoolClass;
		ChaCore.DealToolAskNextCreatedCha = true;
		yield return (object)new WaitForSeconds(Time.deltaTime);
		Singleton<Cycle>.Instance.SetWithHeroine(her, 0);
		Singleton<Cycle>.Instance.Change((Type)5);
		yield return (object)new WaitUntil((Func<bool>)(() => (Object)(object)LatestCreatedCha != (Object)null && LatestCreatedCha.GetActiveTop() && ((ChaInfo)LatestCreatedCha).loadEnd));
		yield return (object)new WaitForSeconds(Time.deltaTime);
		((ChaFile)((ChaInfo)LatestCreatedCha).chaFile).status.coordinateType = 2;
		LatestCreatedCha.ChangeCoordinateTypeAndReload(Clothes.GetCoordTyp(2), true);
		LatestCreatedCha = null;
		yield return (object)new WaitForSeconds(Time.deltaTime * 2f);
		if (Core.dealer.eventInfo.saves[0] != null)
		{
			Core.dealer.eventInfo.saves[0].coordinates = 2;
		}
		yield return (object)new WaitUntil((Func<bool>)(() => Core.dealer.Ctx == Dealer.Context.H && (Object)(object)Core.dealer.HCtrl != (Object)null && (Object)(object)Core.dealer.HCtrl.MainGirl != (Object)null && ((ChaInfo)Core.dealer.HCtrl.MainGirl.Cha).loadEnd && Core.dealer.HCtrl.MainGirl.Cha.GetActiveTop()));
		Core.dealer.MainGirlCha.ChangeCoordinateTypeAndReload(Clothes.GetCoordTyp(2), true);
	}
}
