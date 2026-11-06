using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using AIChara;
using AIProject;
using AIProject.Animal;
using AIProject.Animal.Resources;
using AIProject.Definitions;
using AIProject.MiniGames.Fishing;
using AIProject.Player;
using AIProject.SaveData;
using AIProject.Scene;
using AIProject.UI.Popup;
using Illusion;
using Illusion.Extensions;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using UnityEx;

namespace Manager;

public class Resources : Singleton<Resources>
{
	public class ActionTable
	{
		private static readonly string[] _separators = new string[2] { "/", "／" };

		private static readonly int[] _bustIndexes = new int[7] { 2, 3, 4, 5, 6, 7, 8 };

		private static readonly char[] _splitChars = new char[1] { ',' };

		public Dictionary<int, Dictionary<int, int>> PhaseExp { get; private set; } = new Dictionary<int, Dictionary<int, int>>();


		public Dictionary<int, Dictionary<int, Threshold>> PersonalityMotivation { get; private set; } = new Dictionary<int, Dictionary<int, Threshold>>();


		public Dictionary<int, Dictionary<int, int>> LifestyleTable { get; private set; } = new Dictionary<int, Dictionary<int, int>>();


		public Dictionary<int, Dictionary<int, ObtainItemInfo>> FlavorPickSkillTable { get; private set; } = new Dictionary<int, Dictionary<int, ObtainItemInfo>>();


		public Dictionary<int, Dictionary<int, ObtainItemInfo>> FlavorPickHSkillTable { get; private set; } = new Dictionary<int, Dictionary<int, ObtainItemInfo>>();


		public Dictionary<int, Dictionary<int, ActAnimFlagData>> AgentActionFlagTable { get; private set; } = new Dictionary<int, Dictionary<int, ActAnimFlagData>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, ParameterPacket>>> ActionStatusResultTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, ParameterPacket>>>();


		public Dictionary<int, Dictionary<int, ParameterPacket>> SituationStatusResultTable { get; private set; } = new Dictionary<int, Dictionary<int, ParameterPacket>>();


		public Dictionary<int, Dictionary<int, string>> ActionExpressionTable { get; private set; } = new Dictionary<int, Dictionary<int, string>>();


		public Dictionary<int, Dictionary<int, List<ExpressionKeyframe>>> ActionExpressionKeyframeTable { get; private set; } = new Dictionary<int, Dictionary<int, List<ExpressionKeyframe>>>();


		public Dictionary<string, Dictionary<int, YureCtrl.Info>> ActionYureTable { get; private set; } = new Dictionary<string, Dictionary<int, YureCtrl.Info>>();


		public Dictionary<int, int> AgentLocomotionBreathTable { get; private set; } = new Dictionary<int, int>();


		public Dictionary<int, ABInfoData.Param> ComCameraList { get; private set; } = new Dictionary<int, ABInfoData.Param>();


		public Dictionary<int, Dictionary<int, ByproductInfo>> ByproductList { get; private set; } = new Dictionary<int, Dictionary<int, ByproductInfo>>();


		public void Load(DefinePack definePack)
		{
			LoadPhaseExp(definePack);
			LoadPersonalityMotivation(definePack);
			LoadLifestyleTable(definePack);
			LoadFlavorPickSkillTable(definePack);
			LoadFlavorPickHSkillTable(definePack);
			LoadActionTalkFlags(definePack);
			LoadLocmotionBreathTable(definePack);
			LoadActionExpressionTable(definePack);
			LoadActionExpressionKeyFrameTable(definePack);
			LoadActionBustCtrlTable(definePack);
			LoadActionResultTable(definePack);
			LoadSituationResultTable(definePack);
			LoadComCemra(definePack);
			LoadByproductList(definePack);
		}

		public void Release()
		{
			PhaseExp.Clear();
			PersonalityMotivation.Clear();
			LifestyleTable.Clear();
			FlavorPickSkillTable.Clear();
			FlavorPickHSkillTable.Clear();
			AgentActionFlagTable.Clear();
			ActionStatusResultTable.Clear();
			SituationStatusResultTable.Clear();
			ActionExpressionTable.Clear();
			ActionExpressionKeyframeTable.Clear();
			ActionYureTable.Clear();
			AgentLocomotionBreathTable.Clear();
			ComCameraList.Clear();
			ByproductList.Clear();
		}

		private void LoadPhaseExp(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentPhase);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				PhaseExpData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(PhaseExpData)).GetAllAssets<PhaseExpData>();
				foreach (PhaseExpData phaseExpData in allAssets)
				{
					foreach (PhaseExpData.Param item2 in phaseExpData.param)
					{
						if (!PhaseExp.TryGetValue(item2.Personality, out var value))
						{
							Dictionary<int, int> dictionary = new Dictionary<int, int>();
							PhaseExp[item2.Personality] = dictionary;
							value = dictionary;
						}
						for (int j = 0; j < item2.ExpArray.Count; j++)
						{
							string text = item2.ExpArray[j];
							if (!text.IsNullOrEmpty())
							{
								int result;
								int value2 = (int.TryParse(text, out result) ? result : 0);
								value[j] = value2;
							}
						}
					}
				}
			}
		}

		private void LoadPersonalityMotivation(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentPersonalityMotivation);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ExcelData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ExcelData)).GetAllAssets<ExcelData>();
				foreach (ExcelData excelData in allAssets)
				{
					if (!int.TryParse(excelData.name, out var result))
					{
						continue;
					}
					if (!PersonalityMotivation.TryGetValue(result, out var value))
					{
						Dictionary<int, Threshold> dictionary = new Dictionary<int, Threshold>();
						PersonalityMotivation[result] = dictionary;
						value = dictionary;
					}
					foreach (ExcelData.Param item2 in excelData.list)
					{
						int num = 0;
						if (int.TryParse(item2.list.GetElement(num++), out var result2) && float.TryParse(item2.list.GetElement(num++), out var result3) && float.TryParse(item2.list.GetElement(num++), out var result4))
						{
							value[result2] = new Threshold(result3, result4);
						}
					}
				}
			}
		}

		private void LoadLifestyleTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.LifestyleTable);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ExcelData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ExcelData)).GetAllAssets<ExcelData>();
				foreach (ExcelData excelData in allAssets)
				{
					foreach (ExcelData.Param item2 in excelData.list)
					{
						int num = 0;
						if (!int.TryParse(item2.list.GetElement(num++), out var result))
						{
							continue;
						}
						if (!LifestyleTable.TryGetValue(result, out var value))
						{
							Dictionary<int, int> dictionary = new Dictionary<int, int>();
							LifestyleTable[result] = dictionary;
							value = dictionary;
						}
						num++;
						int num2 = 0;
						while (num < item2.list.Count)
						{
							if (!int.TryParse(item2.list.GetElement(num++), out var result2))
							{
								num2++;
							}
							else
							{
								value[num2++] = result2;
							}
						}
					}
				}
			}
		}

		private void LoadFlavorPickSkillTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.FlavorPickSkillTable);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				SkillObtainData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(SkillObtainData)).GetAllAssets<SkillObtainData>();
				foreach (SkillObtainData skillObtainData in allAssets)
				{
					if (!int.TryParse(skillObtainData.name, out var result))
					{
						continue;
					}
					if (!FlavorPickSkillTable.TryGetValue(result, out var value))
					{
						Dictionary<int, ObtainItemInfo> dictionary = new Dictionary<int, ObtainItemInfo>();
						FlavorPickSkillTable[result] = dictionary;
						value = dictionary;
					}
					foreach (SkillObtainData.Param item2 in skillObtainData.param)
					{
						value[item2.ID] = new ObtainItemInfo
						{
							Name = item2.Name,
							Rate = item2.Rate,
							Info = new ItemInfo
							{
								CategoryID = item2.Category,
								ItemID = item2.ItemID
							}
						};
					}
				}
			}
		}

		private void LoadFlavorPickHSkillTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.FlavorPickHSkillTable);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				SkillObtainData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(SkillObtainData)).GetAllAssets<SkillObtainData>();
				foreach (SkillObtainData skillObtainData in allAssets)
				{
					if (!int.TryParse(skillObtainData.name, out var result))
					{
						continue;
					}
					if (!FlavorPickHSkillTable.TryGetValue(result, out var value))
					{
						Dictionary<int, ObtainItemInfo> dictionary = new Dictionary<int, ObtainItemInfo>();
						FlavorPickHSkillTable[result] = dictionary;
						value = dictionary;
					}
					foreach (SkillObtainData.Param item2 in skillObtainData.param)
					{
						value[item2.ID] = new ObtainItemInfo
						{
							Name = item2.Name,
							Rate = item2.Rate,
							Info = new ItemInfo
							{
								CategoryID = item2.Category,
								ItemID = item2.ItemID
							}
						};
					}
				}
			}
		}

		public void LoadActionTalkFlags(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentCommunicationFlags);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ActionTalkData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ActionTalkData)).GetAllAssets<ActionTalkData>();
				foreach (ActionTalkData actionTalkData in allAssets)
				{
					foreach (ActionTalkData.Param item2 in actionTalkData.param)
					{
						if (!AgentActionFlagTable.TryGetValue(item2.ActionID, out var value))
						{
							Dictionary<int, ActAnimFlagData> dictionary = new Dictionary<int, ActAnimFlagData>();
							AgentActionFlagTable[item2.ActionID] = dictionary;
							value = dictionary;
						}
						value[item2.PoseID] = new ActAnimFlagData
						{
							obstacleRadius = item2.ObstacleRadius,
							useNeckLook = item2.useNeckLook,
							canTalk = item2.CanTalk,
							attitudeID = item2.TalkAttitudeID,
							canHCommand = item2.CanHCommand,
							isBadMood = item2.IsBadMood,
							isSpecial = item2.IsSpecial,
							hPositionID1 = item2.HPositionID,
							hPositionID2 = item2.HPositionSubID
						};
					}
					Singleton<Resources>.Instance.AddLoadAssetBundle(item, string.Empty);
				}
			}
		}

		public void LoadActionResultTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentActionResult);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = CommonLib.LoadAsset<ExcelData>(text, fileNameWithoutExtension, clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(text, string.Empty);
				int num = 1;
				num = 0;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 2;
					string element = param.list.GetElement(num2++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					string element2 = param.list.GetElement(num2++);
					if (!int.TryParse(element2, out var result2))
					{
						continue;
					}
					string element3 = param.list.GetElement(num2++);
					if (!int.TryParse(element3, out var result3))
					{
						continue;
					}
					int result4;
					int num3 = (int.TryParse(param.list.GetElement(num2++), out result4) ? result4 : 0);
					ParameterPacket parameterPacket = new ParameterPacket();
					parameterPacket.Probability = num3;
					ParameterPacket parameterPacket2 = parameterPacket;
					while (num2 < param.list.Count)
					{
						string element4 = param.list.GetElement(num2++);
						string element5 = param.list.GetElement(num2++);
						string element6 = param.list.GetElement(num2++);
						string element7 = param.list.GetElement(num2++);
						if (!element4.IsNullOrEmpty())
						{
							if (!StatusTagTable.TryGetValue(element4, out var value))
							{
								Debug.LogWarning($"タグ読み取りエラー: 値={element4}");
								continue;
							}
							int s = (int.TryParse(element5, out result4) ? result4 : 0);
							int m = (int.TryParse(element6, out result4) ? result4 : 0);
							int l = (int.TryParse(element7, out result4) ? result4 : 0);
							parameterPacket2.Parameters[value] = new TriThreshold(s, m, l);
						}
					}
					if (!ActionStatusResultTable.TryGetValue(result, out var value2))
					{
						Dictionary<int, Dictionary<int, ParameterPacket>> dictionary = new Dictionary<int, Dictionary<int, ParameterPacket>>();
						ActionStatusResultTable[result] = dictionary;
						value2 = dictionary;
					}
					if (!value2.TryGetValue(result2, out var value3))
					{
						Dictionary<int, ParameterPacket> dictionary3 = (value2[result2] = new Dictionary<int, ParameterPacket>());
						value3 = dictionary3;
					}
					value3[result3] = parameterPacket2;
				}
			}
		}

		private void LoadLocmotionBreathTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentLocomotionBreath);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				LocomotionBreathData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(LocomotionBreathData)).GetAllAssets<LocomotionBreathData>();
				foreach (LocomotionBreathData locomotionBreathData in allAssets)
				{
					foreach (LocomotionBreathData.Param item2 in locomotionBreathData.param)
					{
						if (!item2.State.IsNullOrEmpty())
						{
							int key = Animator.StringToHash(item2.State);
							AgentLocomotionBreathTable[key] = item2.VoiceID;
						}
					}
				}
			}
		}

		public void LoadActionExpressionTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActionExpList);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				MotionExpressionData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(MotionExpressionData)).GetAllAssets<MotionExpressionData>();
				foreach (MotionExpressionData motionExpressionData in allAssets)
				{
					int key = int.Parse(motionExpressionData.name.Replace("c", string.Empty));
					if (!ActionExpressionTable.TryGetValue(key, out var value))
					{
						Dictionary<int, string> dictionary = new Dictionary<int, string>();
						ActionExpressionTable[key] = dictionary;
						value = dictionary;
					}
					foreach (MotionExpressionData.Param item2 in motionExpressionData.param)
					{
						if (!item2.State.IsNullOrEmpty())
						{
							int key2 = Animator.StringToHash(item2.State);
							value[key2] = item2.ExpressionName;
						}
					}
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(item, string.Empty);
			}
		}

		public void LoadActionExpressionKeyFrameTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActionExpKeyFrameList);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ExcelData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ExcelData)).GetAllAssets<ExcelData>();
				foreach (ExcelData excelData in allAssets)
				{
					int key = int.Parse(excelData.name.Replace("c", string.Empty));
					if (!ActionExpressionKeyframeTable.TryGetValue(key, out var value))
					{
						Dictionary<int, List<ExpressionKeyframe>> dictionary = new Dictionary<int, List<ExpressionKeyframe>>();
						ActionExpressionKeyframeTable[key] = dictionary;
						value = dictionary;
					}
					foreach (ExcelData.Param item2 in excelData.list)
					{
						int num = 1;
						string element = item2.list.GetElement(num++);
						if (element.IsNullOrEmpty())
						{
							continue;
						}
						int key2 = Animator.StringToHash(element);
						if (!value.TryGetValue(key2, out var value2))
						{
							List<ExpressionKeyframe> list2 = (value[key2] = new List<ExpressionKeyframe>());
							value2 = list2;
						}
						while (num < item2.list.Count)
						{
							string element2 = item2.list.GetElement(num++);
							string element3 = item2.list.GetElement(num++);
							if (float.TryParse(element2, out var result))
							{
								value2.Add(new ExpressionKeyframe
								{
									normalizedTime = result,
									expressionName = element3
								});
							}
						}
					}
				}
			}
		}

		public void LoadActionBustCtrlTable(DefinePack definePack)
		{
			if (definePack.ABDirectories.ActionBustCtrlList.IsNullOrEmpty())
			{
				return;
			}
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActionBustCtrlList);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				MotionBustCtrlData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(MotionBustCtrlData)).GetAllAssets<MotionBustCtrlData>();
				foreach (MotionBustCtrlData motionBustCtrlData in allAssets)
				{
					if (!ActionYureTable.TryGetValue(motionBustCtrlData.name, out var value))
					{
						Dictionary<int, YureCtrl.Info> dictionary = new Dictionary<int, YureCtrl.Info>();
						ActionYureTable[motionBustCtrlData.name] = dictionary;
						value = dictionary;
					}
					foreach (MotionBustCtrlData.Param item2 in motionBustCtrlData.param)
					{
						if (!item2.State.IsNullOrEmpty())
						{
							int key = Animator.StringToHash(item2.State);
							YureCtrl.Info info2 = (value[key] = new YureCtrl.Info());
							YureCtrl.Info info3 = info2;
							int num = 0;
							info3.aIsActive[0] = item2.Parameters.GetElement(num++) == "1";
							info3.aBreastShape[0].MemberInit();
							for (int j = 0; j < _bustIndexes.Length; j++)
							{
								info3.aBreastShape[0].breast[j] = item2.Parameters.GetElement(num++) == "1";
							}
							info3.aBreastShape[0].nip = item2.Parameters.GetElement(num++) == "1";
							info3.aIsActive[1] = item2.Parameters.GetElement(num++) == "1";
							info3.aBreastShape[1].MemberInit();
							for (int k = 0; k < _bustIndexes.Length; k++)
							{
								info3.aBreastShape[1].breast[k] = item2.Parameters.GetElement(num++) == "1";
							}
							info3.aBreastShape[1].nip = item2.Parameters.GetElement(num++) == "1";
							info3.aIsActive[2] = item2.Parameters.GetElement(num++) == "1";
							info3.aIsActive[3] = item2.Parameters.GetElement(num++) == "1";
						}
					}
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(item, string.Empty);
			}
		}

		public void LoadSituationResultTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentSituationResult);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(text, string.Empty);
				int num = 1;
				num = 0;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 1;
					string element = param.list.GetElement(num2++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					string element2 = param.list.GetElement(num2++);
					if (!int.TryParse(element2, out var result2))
					{
						continue;
					}
					int result3;
					int num3 = (int.TryParse(param.list.GetElement(num2++), out result3) ? result3 : 0);
					ParameterPacket parameterPacket = new ParameterPacket();
					parameterPacket.Probability = num3;
					ParameterPacket parameterPacket2 = parameterPacket;
					while (num2 < param.list.Count)
					{
						string element3 = param.list.GetElement(num2++);
						string element4 = param.list.GetElement(num2++);
						string element5 = param.list.GetElement(num2++);
						string element6 = param.list.GetElement(num2++);
						if (!element3.IsNullOrEmpty())
						{
							if (!StatusTagTable.TryGetValue(element3, out var value))
							{
								Debug.LogWarning($"タグ読み取りエラー：値={element3}");
								continue;
							}
							int s = (int.TryParse(element4, out result3) ? result3 : 0);
							int m = (int.TryParse(element5, out result3) ? result3 : 0);
							int l = (int.TryParse(element6, out result3) ? result3 : 0);
							parameterPacket2.Parameters[value] = new TriThreshold(s, m, l);
						}
					}
					if (!SituationStatusResultTable.TryGetValue(result, out var value2))
					{
						Dictionary<int, ParameterPacket> dictionary = new Dictionary<int, ParameterPacket>();
						SituationStatusResultTable[result] = dictionary;
						value2 = dictionary;
					}
					value2[result2] = parameterPacket2;
				}
			}
		}

		private void LoadComCemra(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ComCamera);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ABInfoData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ABInfoData)).GetAllAssets<ABInfoData>();
				foreach (ABInfoData aBInfoData in allAssets)
				{
					foreach (ABInfoData.Param item2 in aBInfoData.param)
					{
						ComCameraList[item2.ID] = item2;
					}
				}
			}
		}

		private void LoadByproductList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActionByproductList);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ActByproductData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ActByproductData)).GetAllAssets<ActByproductData>();
				foreach (ActByproductData actByproductData in allAssets)
				{
					foreach (ActByproductData.Param item2 in actByproductData.param)
					{
						if (!ByproductList.TryGetValue(item2.ActionID, out var value))
						{
							Dictionary<int, ByproductInfo> dictionary = new Dictionary<int, ByproductInfo>();
							ByproductList[item2.ActionID] = dictionary;
							value = dictionary;
						}
						if (item2.ItemList.IsNullOrEmpty())
						{
							continue;
						}
						List<List<int>> list = new List<List<int>>();
						foreach (string item3 in item2.ItemList)
						{
							if (item3.IsNullOrEmpty())
							{
								continue;
							}
							string[] array = item3.Split(_splitChars, StringSplitOptions.RemoveEmptyEntries);
							List<int> list2 = new List<int>();
							string[] array2 = array;
							foreach (string s in array2)
							{
								if (int.TryParse(s, out var result))
								{
									list2.Add(result);
								}
							}
							list.Add(list2);
						}
						value[item2.PoseID] = new ByproductInfo
						{
							ItemList = list
						};
					}
				}
			}
		}
	}

	public class AnimalTables
	{
		private const int AssetBundleNameIndex = 1;

		private const int AssetNameIndex = 2;

		private const int ManifestNameIndex = 3;

		public readonly Dictionary<int, ActionTypes> AnimalActionIDTable = new Dictionary<int, ActionTypes>
		{
			{
				0,
				ActionTypes.None
			},
			{
				1,
				ActionTypes.Rest
			}
		};

		public readonly Dictionary<int, DesireType> AnimalDesireIDTable = new Dictionary<int, DesireType>
		{
			{
				-1,
				DesireType.None
			},
			{
				0,
				DesireType.Sleepiness
			},
			{
				1,
				DesireType.Loneliness
			},
			{
				2,
				DesireType.Action
			}
		};

		public readonly Dictionary<int, ChangeType> ChangeTypeIDTable = new Dictionary<int, ChangeType>
		{
			{
				0,
				ChangeType.Add
			},
			{
				1,
				ChangeType.Sub
			},
			{
				2,
				ChangeType.Cng
			}
		};

		private string[] separators = new string[4] { "/", "／", ",", "、" };

		public Dictionary<int, List<UnityEx.ValueTuple<ItemIDKeyPair, int>>> PetItemInfoTable { get; set; } = new Dictionary<int, List<UnityEx.ValueTuple<ItemIDKeyPair, int>>>();


		public Dictionary<int, UnityEx.ValueTuple<int, List<string>>> PetHomeUIInfoTable { get; private set; } = new Dictionary<int, UnityEx.ValueTuple<int, List<string>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, List<UnityEx.ValueTuple<string, int, int>>>>>> ExpressionTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<string, List<UnityEx.ValueTuple<string, int, int>>>>>>();


		public Dictionary<int, Dictionary<int, UnityEx.ValueTuple<Texture2D, Color[]>>> TextureTable { get; private set; } = new Dictionary<int, Dictionary<int, UnityEx.ValueTuple<Texture2D, Color[]>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, AnimalPlayState>>> CommonAnimeTable { get; set; } = new Dictionary<int, Dictionary<int, Dictionary<int, AnimalPlayState>>>();


		public Dictionary<int, Dictionary<int, AnimalPlayState>> WithAgentAnimeTable { get; set; } = new Dictionary<int, Dictionary<int, AnimalPlayState>>();


		public Dictionary<int, Dictionary<int, RuntimeAnimatorController>> AnimatorTable { get; set; } = new Dictionary<int, Dictionary<int, RuntimeAnimatorController>>();


		protected Dictionary<string, Dictionary<string, RuntimeAnimatorController>> TempAnimatorTable { get; set; } = new Dictionary<string, Dictionary<string, RuntimeAnimatorController>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, AnimalActionInfo>>> ActionInfoTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, AnimalActionInfo>>>();


		public Dictionary<int, Dictionary<int, AnimalModelInfo>> ModelInfoTable { get; set; } = new Dictionary<int, Dictionary<int, AnimalModelInfo>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<int, DesireType>>> DesirePriorityTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<int, DesireType>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, int>>> DesireSpanTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, int>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, System.Tuple<float, float>>>> DesireBorderTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, System.Tuple<float, float>>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, Dictionary<DesireType, float>>>> DesireRateTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, Dictionary<DesireType, float>>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, List<AnimalState>>>> DesireTargetStateTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, List<AnimalState>>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, Dictionary<bool, Dictionary<DesireType, ChangeParamState>>>>> DesireResultTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<DesireType, Dictionary<bool, Dictionary<DesireType, ChangeParamState>>>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, StateCondition>>> StateConditionTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, StateCondition>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, List<ActionTypes>>>> StateTargetActionTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, List<ActionTypes>>>>();


		public Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, LookState>>> LookStateTable { get; set; } = new Dictionary<AnimalTypes, Dictionary<BreedingTypes, Dictionary<AnimalState, LookState>>>();


		public Dictionary<int, Dictionary<int, PlayState>> PlayerCatchAnimalAnimationTable { get; set; } = new Dictionary<int, Dictionary<int, PlayState>>();


		public Dictionary<int, Dictionary<int, PoseKeyPair>> PlayerCatchAnimalPoseTable { get; set; } = new Dictionary<int, Dictionary<int, PoseKeyPair>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> AnimalPointAssetTable { get; set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> AnimalBaseObjInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public void Load(AnimalDefinePack _animalDefinePack)
		{
			LoadInfo(_animalDefinePack);
			LoadAction(_animalDefinePack);
			LoadLook(_animalDefinePack);
			LoadState(_animalDefinePack);
			LoadDesire(_animalDefinePack);
			LoadWithActor(_animalDefinePack);
			LoadPlayerInfo(_animalDefinePack);
			LoadAnimalPoint(_animalDefinePack);
		}

		public void Release()
		{
			PetItemInfoTable.Clear();
			PetHomeUIInfoTable.Clear();
			ExpressionTable.Clear();
			TextureTable.Clear();
			CommonAnimeTable.Clear();
			WithAgentAnimeTable.Clear();
			AnimatorTable.Clear();
			TempAnimatorTable.Clear();
			ActionInfoTable.Clear();
			ModelInfoTable.Clear();
			DesirePriorityTable.Clear();
			DesireSpanTable.Clear();
			DesireBorderTable.Clear();
			DesireRateTable.Clear();
			DesireTargetStateTable.Clear();
			DesireResultTable.Clear();
			StateConditionTable.Clear();
			StateTargetActionTable.Clear();
			LookStateTable.Clear();
			PlayerCatchAnimalAnimationTable.Clear();
			PlayerCatchAnimalPoseTable.Clear();
			AnimalPointAssetTable.Clear();
			AnimalBaseObjInfoTable.Clear();
		}

		private string AssetStr(AssetBundleInfo _info)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}]";
		}

		private string AssetStrWithName(AssetBundleInfo _info)
		{
			return $"Name[{_info.name}] AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}]";
		}

		private RuntimeAnimatorController LoadAnimator(string _assetBundleName, string _assetName, string _manifestName = "")
		{
			RuntimeAnimatorController value = null;
			if (!TempAnimatorTable.ContainsKey(_assetBundleName))
			{
				TempAnimatorTable[_assetBundleName] = new Dictionary<string, RuntimeAnimatorController>();
			}
			if (!TempAnimatorTable[_assetBundleName].TryGetValue(_assetName, out value))
			{
				value = AssetUtility.LoadAsset<RuntimeAnimatorController>(_assetBundleName, _assetName, _manifestName);
				TempAnimatorTable[_assetBundleName][_assetName] = value;
			}
			return value;
		}

		private RuntimeAnimatorController LoadAnimator(AssetBundleInfo _info)
		{
			return LoadAnimator(_info.assetbundle, _info.asset, _info.manifest);
		}

		private string GetDirectory(string _directory, string _animalName)
		{
			return _directory + _animalName + "/";
		}

		private string GetDirectory(string _directory, string _animalName, string _breedingName)
		{
			return $"{_directory}{_animalName}/{_breedingName}/";
		}

		private AssetBundleInfo GetAssetInfo(List<string> _address, ref int _idx, bool _addSummary)
		{
			string name_ = ((!_addSummary) ? string.Empty : (_address.GetElement(_idx++) ?? string.Empty));
			return new AssetBundleInfo(name_, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty);
		}

		private void LoadInfo(AnimalDefinePack _animalDefinePack)
		{
			string animalInfoDirectory = _animalDefinePack.AssetBundleNames.AnimalInfoDirectory;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(animalInfoDirectory);
			if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
						switch (result)
						{
						case 0:
							LoadPetItemInfoList(assetInfo);
							break;
						case 1:
							LoadPetHomeUIInfoList(assetInfo);
							break;
						case 2:
							LoadAnimatorInfo(assetInfo);
							break;
						case 3:
							LoadAnimStateInfo(assetInfo);
							break;
						case 4:
							LoadModelInfo(assetInfo);
							break;
						case 5:
							LoadExpressionList(assetInfo);
							break;
						case 6:
							LoadAnimalTextureList(assetInfo);
							break;
						case 7:
							LoadAnimalBaseObjList(assetInfo);
							break;
						}
					}
				}
			}
		}

		private void LoadPetItemInfoList(AssetBundleInfo _excelAdress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAdress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					LoadPetItemInfo(assetInfo);
				}
			}
		}

		private void LoadPetItemInfo(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var _categoryID))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var _itemID))
				{
					continue;
				}
				string s4 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s4, out var result2))
				{
					continue;
				}
				if (!PetItemInfoTable.TryGetValue(result, out var value) || value == null)
				{
					List<UnityEx.ValueTuple<ItemIDKeyPair, int>> list2 = new List<UnityEx.ValueTuple<ItemIDKeyPair, int>>();
					PetItemInfoTable[result] = list2;
					value = list2;
				}
				if (0 <= result2)
				{
					int num2 = -1;
					UnityEx.ValueTuple<ItemIDKeyPair, int> valueTuple = new UnityEx.ValueTuple<ItemIDKeyPair, int>(new ItemIDKeyPair
					{
						categoryID = _categoryID,
						itemID = _itemID
					}, result2);
					if (0 <= (num2 = value.FindIndex((UnityEx.ValueTuple<ItemIDKeyPair, int> x) => x.Item1.categoryID == _categoryID && x.Item1.itemID == _itemID)))
					{
						value[num2] = valueTuple;
					}
					else
					{
						value.Add(valueTuple);
					}
				}
				else
				{
					value.RemoveAll((UnityEx.ValueTuple<ItemIDKeyPair, int> x) => x.Item1.categoryID == _categoryID && x.Item1.itemID == _itemID);
				}
			}
		}

		private void LoadPetHomeUIInfoList(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 1;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s2, out var result2))
				{
					if (!PetHomeUIInfoTable.TryGetValue(result, out var value))
					{
						value = new UnityEx.ValueTuple<int, List<string>>(result2, new List<string>());
					}
					else
					{
						value.Item2.Clear();
					}
					List<string> item = value.Item2;
					while (num < list.Count)
					{
						item.Add(list.GetElement(num++));
					}
					value.Item2 = item;
					PetHomeUIInfoTable[result] = value;
				}
			}
		}

		private void LoadAnimatorInfo(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 1;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
				RuntimeAnimatorController runtimeAnimatorController = LoadAnimator(assetInfo);
				if (!(runtimeAnimatorController == null))
				{
					if (!AnimatorTable.TryGetValue(result, out var value) || value == null)
					{
						Dictionary<int, RuntimeAnimatorController> dictionary = new Dictionary<int, RuntimeAnimatorController>();
						AnimatorTable[result] = dictionary;
						value = dictionary;
					}
					value[result2] = runtimeAnimatorController;
				}
			}
		}

		private void LoadAnimStateInfo(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					LoadAnimStateInfoElement(assetInfo);
				}
			}
		}

		private void LoadAnimStateInfoElement(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				string text2 = list.GetElement(num++) ?? string.Empty;
				if (!text.IsNullOrEmpty() && !text2.IsNullOrEmpty())
				{
					string text3 = list.GetElement(num++) ?? string.Empty;
					string[] inStateNames = text3.Split(separators, StringSplitOptions.RemoveEmptyEntries);
					if (!bool.TryParse(list.GetElement(num++) ?? string.Empty, out var result4))
					{
						result4 = false;
					}
					if (!float.TryParse(list.GetElement(num++) ?? string.Empty, out var result5))
					{
						result5 = 0f;
					}
					if (!bool.TryParse(list.GetElement(num++) ?? string.Empty, out var result6))
					{
						result6 = false;
					}
					if (!int.TryParse(list.GetElement(num++) ?? string.Empty, out var result7))
					{
						result7 = 0;
					}
					if (!int.TryParse(list.GetElement(num++) ?? string.Empty, out var result8))
					{
						result8 = 0;
					}
					string text4 = list.GetElement(num++) ?? string.Empty;
					string[] outStateNames = text4.Split(separators, StringSplitOptions.RemoveEmptyEntries);
					if (!bool.TryParse(list.GetElement(num++) ?? string.Empty, out var result9))
					{
						result9 = false;
					}
					if (!float.TryParse(list.GetElement(num++) ?? string.Empty, out var result10))
					{
						result10 = 0f;
					}
					if (!int.TryParse(list.GetElement(num++) ?? string.Empty, out var result11))
					{
						result11 = 0;
					}
					AnimalPlayState animalPlayState = new AnimalPlayState(result11, result3, inStateNames, outStateNames);
					AnimalPlayState.PlayStateInfo mainStateInfo = animalPlayState.MainStateInfo;
					mainStateInfo.AssetBundleInfo = new AssetBundleInfo(string.Empty, text, text2, string.Empty);
					mainStateInfo.InFadeEnable = result4;
					mainStateInfo.InFadeSecond = result5;
					mainStateInfo.OutFadeEnable = result9;
					mainStateInfo.OutFadeSecond = result10;
					mainStateInfo.IsLoop = result6;
					mainStateInfo.LoopMin = result7;
					mainStateInfo.LoopMax = result8;
					LoadAnimalAnimator(animalPlayState);
					if (!CommonAnimeTable.TryGetValue(result, out var value))
					{
						value = (CommonAnimeTable[result] = new Dictionary<int, Dictionary<int, AnimalPlayState>>());
					}
					if (!value.TryGetValue(result2, out var value2))
					{
						value2 = (value[result2] = new Dictionary<int, AnimalPlayState>());
					}
					value2[result3] = animalPlayState;
				}
			}
		}

		private void LoadModelInfo(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 1;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(_idx++) ?? string.Empty;
				if (int.TryParse(s2, out var result2))
				{
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
					string value = list.GetElement(_idx++) ?? string.Empty;
					if (!bool.TryParse(value, out var result3))
					{
						result3 = false;
					}
					string text = string.Empty;
					int result4 = -1;
					if (result3)
					{
						text = list.GetElement(_idx++) ?? string.Empty;
						string s3 = list.GetElement(_idx++) ?? string.Empty;
						result4 = ((!int.TryParse(s3, out result4)) ? (-1) : result4);
						result3 = !text.IsNullOrEmpty() && 0 <= result4;
					}
					else
					{
						_idx += 2;
					}
					string value2 = list.GetElement(_idx++) ?? string.Empty;
					if (!bool.TryParse(value2, out var result5))
					{
						result5 = false;
					}
					string text2 = string.Empty;
					int result6 = -1;
					if (result5)
					{
						text2 = list.GetElement(_idx++) ?? string.Empty;
						string s4 = list.GetElement(_idx++) ?? string.Empty;
						result6 = ((!int.TryParse(s4, out result6)) ? (-1) : result6);
						result5 = !text2.IsNullOrEmpty() && 0 <= result6;
					}
					else
					{
						_idx += 2;
					}
					AnimalShapeInfo eyesShapeInfo = new AnimalShapeInfo(result3, text, result4);
					AnimalShapeInfo mouthShapeInfo = new AnimalShapeInfo(result5, text2, result6);
					AnimalModelInfo value3 = new AnimalModelInfo(assetInfo, eyesShapeInfo, mouthShapeInfo);
					if (!ModelInfoTable.TryGetValue(result, out var value4))
					{
						Dictionary<int, AnimalModelInfo> dictionary = new Dictionary<int, AnimalModelInfo>();
						ModelInfoTable[result] = dictionary;
						value4 = dictionary;
					}
					value4[result2] = value3;
				}
			}
		}

		private void LoadExpressionList(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					LoadExpressionTable(assetInfo);
				}
			}
		}

		private void LoadExpressionTable(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string element = list.GetElement(num++);
				if (element.IsNullOrEmpty())
				{
					continue;
				}
				if (!ExpressionTable.TryGetValue(result, out var value) || value == null)
				{
					value = (ExpressionTable[result] = new Dictionary<int, Dictionary<int, Dictionary<string, List<UnityEx.ValueTuple<string, int, int>>>>>());
				}
				if (!value.TryGetValue(result2, out var value2) || value2 == null)
				{
					value2 = (value[result2] = new Dictionary<int, Dictionary<string, List<UnityEx.ValueTuple<string, int, int>>>>());
				}
				if (!value2.TryGetValue(result3, out var value3) || value3 == null)
				{
					value3 = (value2[result3] = new Dictionary<string, List<UnityEx.ValueTuple<string, int, int>>>());
				}
				if (!value3.TryGetValue(element, out var value4) || value4 == null)
				{
					value4 = (value3[element] = new List<UnityEx.ValueTuple<string, int, int>>());
				}
				else
				{
					value4.Clear();
				}
				while (num < list.Count)
				{
					string element2 = list.GetElement(num++);
					string s4 = list.GetElement(num++) ?? string.Empty;
					string s5 = list.GetElement(num++) ?? string.Empty;
					if (!element2.IsNullOrEmpty() && int.TryParse(s4, out var result4) && int.TryParse(s5, out var result5))
					{
						value4.Add(new UnityEx.ValueTuple<string, int, int>(element2, result4, result5));
					}
				}
			}
		}

		private void LoadAnimalTextureList(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				string text2 = list.GetElement(num++) ?? string.Empty;
				string text3 = list.GetElement(num++) ?? string.Empty;
				List<Color> list2 = ListPool<Color>.Get();
				while (num < list.Count)
				{
					string s3 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s3, out var result3))
					{
						result3 = 0f;
					}
					string s4 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s4, out var result4))
					{
						result4 = 0f;
					}
					string s5 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s5, out var result5))
					{
						result5 = 0f;
					}
					list2.Add(new Color(result3 / 255f, result4 / 255f, result5 / 255f, 1f));
				}
				for (int j = 0; j < 4; j++)
				{
					string s6 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s6, out var result6))
					{
						result6 = 0f;
					}
					string s7 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s7, out var result7))
					{
						result7 = 0f;
					}
					string s8 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s8, out var result8))
					{
						result8 = 0f;
					}
					list2.Add(new Color(result6 / 255f, result7 / 255f, result8 / 255f, 1f));
				}
				string assetBundleName = text;
				string assetName = text2;
				string manifestName = ((!text3.IsNullOrEmpty()) ? text3 : null);
				Texture2D texture2D = CommonLib.LoadAsset<Texture2D>(assetBundleName, assetName, clone: false, manifestName);
				if (texture2D != null)
				{
					Singleton<Resources>.Instance.AddLoadAssetBundle(text, text3);
				}
				Color[] array = new Color[list2.Count];
				for (int k = 0; k < array.Length; k++)
				{
					ref Color reference = ref array[k];
					reference = list2[k];
				}
				ListPool<Color>.Release(list2);
				UnityEx.ValueTuple<Texture2D, Color[]> value = new UnityEx.ValueTuple<Texture2D, Color[]>(texture2D, array);
				if (!TextureTable.TryGetValue(result, out var value2) || value2 == null)
				{
					Dictionary<int, UnityEx.ValueTuple<Texture2D, Color[]>> dictionary = new Dictionary<int, UnityEx.ValueTuple<Texture2D, Color[]>>();
					TextureTable[result] = dictionary;
					value2 = dictionary;
				}
				value2[result2] = value;
			}
		}

		private void LoadAnimalBaseObjList(AssetBundleInfo _excelAddress)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_excelAddress);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 1;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string element = list.GetElement(num++);
				string element2 = list.GetElement(num++);
				string element3 = list.GetElement(num++);
				if (!element.IsNullOrEmpty() && !element2.IsNullOrEmpty())
				{
					if (!AnimalBaseObjInfoTable.TryGetValue(result, out var value) || value == null)
					{
						Dictionary<int, AssetBundleInfo> dictionary = new Dictionary<int, AssetBundleInfo>();
						AnimalBaseObjInfoTable[result] = dictionary;
						value = dictionary;
					}
					value[result2] = new AssetBundleInfo(string.Empty, element, element2, element3);
				}
			}
		}

		private void LoadAction(AnimalDefinePack _animalDefinePack)
		{
			string actionInfoListBundleDirectory = _animalDefinePack.AssetBundleNames.ActionInfoListBundleDirectory;
			IReadOnlyList<UnityEx.ValueTuple<string, AnimalTypes>> animalNameList = AIProject.Animal.AnimalData.AnimalNameList;
			for (int i = 0; i < animalNameList.Count; i++)
			{
				string item = animalNameList[i].Item1;
				AnimalTypes item2 = animalNameList[i].Item2;
				IReadOnlyList<UnityEx.ValueTuple<string, BreedingTypes>> breedingNameList = AIProject.Animal.AnimalData.BreedingNameList;
				for (int j = 0; j < breedingNameList.Count; j++)
				{
					string item3 = breedingNameList[j].Item1;
					BreedingTypes item4 = breedingNameList[j].Item2;
					string directory = GetDirectory(actionInfoListBundleDirectory, item, item3);
					List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(directory);
					if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
					{
						continue;
					}
					assetBundleNameListFromPath.Sort();
					for (int k = 0; k < assetBundleNameListFromPath.Count; k++)
					{
						string text = assetBundleNameListFromPath[k];
						string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
						if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
						{
							continue;
						}
						ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
						if (excelData == null)
						{
							continue;
						}
						for (int l = 1; l < excelData.MaxCell; l++)
						{
							List<string> list = excelData.list[l].list;
							if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
							{
								continue;
							}
							int num = 0;
							string s = list.GetElement(num++) ?? string.Empty;
							if (int.TryParse(s, out var result))
							{
								num++;
								AssetBundleInfo sheetABInfo = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
								if (!sheetABInfo.assetbundle.IsNullOrEmpty() && !sheetABInfo.asset.IsNullOrEmpty() && result == 0)
								{
									LoadActionEndInfo(item2, item4, sheetABInfo);
								}
							}
						}
					}
				}
			}
		}

		private void LoadActionEndInfo(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetABInfo)
		{
			if (_sheetABInfo.assetbundle.IsNullOrEmpty() || _sheetABInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetABInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				int num = 0;
				int result = 0;
				int result2 = 0;
				int result3 = 0;
				AnimalState value = AnimalState.None;
				bool result4 = false;
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				string s = list.GetElement(num++) ?? string.Empty;
				string text = list.GetElement(num++) ?? string.Empty;
				num++;
				string value2 = list.GetElement(num++) ?? string.Empty;
				string s2 = list.GetElement(num++) ?? string.Empty;
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out result) && AIProject.Animal.AnimalData.AnimalStateIDTable.TryGetValue(result, out value) && bool.TryParse(value2, out result4) && int.TryParse(s2, out result2) && int.TryParse(s3, out result3))
				{
					if (!ActionInfoTable.ContainsKey(_animalType))
					{
						ActionInfoTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<AnimalState, AnimalActionInfo>>();
					}
					if (!ActionInfoTable[_animalType].ContainsKey(_breedingType))
					{
						ActionInfoTable[_animalType][_breedingType] = new Dictionary<AnimalState, AnimalActionInfo>();
					}
					if (!ActionInfoTable[_animalType][_breedingType].TryGetValue(value, out var value3))
					{
						value3 = new AnimalActionInfo();
					}
					value3.timeInfo = new AnimalActionInfo.TimeInfo(result4, result2, result3);
					ActionInfoTable[_animalType][_breedingType][value] = value3;
				}
			}
		}

		private void LoadAnimalAnimator(AnimalPlayState _playState)
		{
			if (_playState == null)
			{
				return;
			}
			AssetBundleInfo assetBundleInfo = _playState.MainStateInfo.AssetBundleInfo;
			assetBundleInfo.manifest = "abdata";
			_playState.MainStateInfo.Controller = LoadAnimator(assetBundleInfo);
			if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(_playState.SubStateInfos))
			{
				return;
			}
			foreach (AnimalPlayState.PlayStateInfo subStateInfo in _playState.SubStateInfos)
			{
				AssetBundleInfo assetBundleInfo2 = subStateInfo.AssetBundleInfo;
				assetBundleInfo2.manifest = "abdata";
				subStateInfo.Controller = LoadAnimator(assetBundleInfo2);
			}
		}

		private void LoadLook(AnimalDefinePack _animalDefinePack)
		{
			string lookInfoListBundleDirectory = _animalDefinePack.AssetBundleNames.LookInfoListBundleDirectory;
			IReadOnlyList<UnityEx.ValueTuple<string, AnimalTypes>> animalNameList = AIProject.Animal.AnimalData.AnimalNameList;
			for (int i = 0; i < animalNameList.Count; i++)
			{
				string item = animalNameList[i].Item1;
				AnimalTypes item2 = animalNameList[i].Item2;
				string directory = GetDirectory(lookInfoListBundleDirectory, item);
				List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(directory);
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
				{
					continue;
				}
				assetBundleNameListFromPath.Sort();
				for (int j = 0; j < assetBundleNameListFromPath.Count; j++)
				{
					string text = assetBundleNameListFromPath[j];
					string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
					if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
					{
						continue;
					}
					ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
					if (excelData == null)
					{
						continue;
					}
					for (int k = 1; k < excelData.MaxCell; k++)
					{
						List<string> list = excelData.list[k].list;
						if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
						{
							continue;
						}
						int num = 0;
						int result = 0;
						AnimalState value = AnimalState.None;
						string s = list.GetElement(num++) ?? string.Empty;
						string text2 = list.GetElement(num++) ?? string.Empty;
						string s2 = list.GetElement(num++) ?? string.Empty;
						string value2 = list.GetElement(num++) ?? string.Empty;
						string s3 = list.GetElement(num++) ?? string.Empty;
						string value3 = list.GetElement(num++) ?? string.Empty;
						if (!int.TryParse(s, out result) || !AIProject.Animal.AnimalData.AnimalStateIDTable.TryGetValue(result, out value))
						{
							continue;
						}
						int result2 = 0;
						if (int.TryParse(s2, out result2))
						{
							bool result3 = false;
							result3 = bool.TryParse(value2, out result3) && result3;
							if (!LookStateTable.ContainsKey(item2))
							{
								LookStateTable[item2] = new Dictionary<BreedingTypes, Dictionary<AnimalState, LookState>>();
							}
							if (!LookStateTable[item2].ContainsKey(BreedingTypes.Wild))
							{
								LookStateTable[item2][BreedingTypes.Wild] = new Dictionary<AnimalState, LookState>();
							}
							LookStateTable[item2][BreedingTypes.Wild][value] = new LookState(result2, result3);
						}
						int result4 = 0;
						if (int.TryParse(s3, out result4))
						{
							bool result5 = false;
							result5 = bool.TryParse(value3, out result5) && result5;
							if (!LookStateTable.ContainsKey(item2))
							{
								LookStateTable[item2] = new Dictionary<BreedingTypes, Dictionary<AnimalState, LookState>>();
							}
							if (!LookStateTable[item2].ContainsKey(BreedingTypes.Pet))
							{
								LookStateTable[item2][BreedingTypes.Pet] = new Dictionary<AnimalState, LookState>();
							}
							LookStateTable[item2][BreedingTypes.Pet][value] = new LookState(result4, result5);
						}
					}
				}
			}
		}

		private void LoadState(AnimalDefinePack _animalDefinePack)
		{
			string stateInfoListBundleDirectory = _animalDefinePack.AssetBundleNames.StateInfoListBundleDirectory;
			IReadOnlyList<UnityEx.ValueTuple<string, AnimalTypes>> animalNameList = AIProject.Animal.AnimalData.AnimalNameList;
			for (int i = 0; i < animalNameList.Count; i++)
			{
				string item = animalNameList[i].Item1;
				AnimalTypes item2 = animalNameList[i].Item2;
				IReadOnlyList<UnityEx.ValueTuple<string, BreedingTypes>> breedingNameList = AIProject.Animal.AnimalData.BreedingNameList;
				for (int j = 0; j < breedingNameList.Count; j++)
				{
					string item3 = breedingNameList[j].Item1;
					BreedingTypes item4 = breedingNameList[j].Item2;
					string directory = GetDirectory(stateInfoListBundleDirectory, item, item3);
					List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(directory);
					assetBundleNameListFromPath.Sort();
					if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
					{
						continue;
					}
					for (int k = 0; k < assetBundleNameListFromPath.Count; k++)
					{
						string text = assetBundleNameListFromPath[k];
						string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
						if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
						{
							continue;
						}
						ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
						if (excelData == null)
						{
							continue;
						}
						for (int l = 1; l < excelData.MaxCell; l++)
						{
							List<string> list = excelData.list[l].list;
							if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
							{
								continue;
							}
							int num = 0;
							string s = list.GetElement(num++) ?? string.Empty;
							if (!int.TryParse(s, out var result))
							{
								continue;
							}
							num++;
							AssetBundleInfo sheetABInfo = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num));
							if (!sheetABInfo.assetbundle.IsNullOrEmpty() && !sheetABInfo.asset.IsNullOrEmpty())
							{
								switch (result)
								{
								case 0:
									LoadChangeState(item2, item4, sheetABInfo);
									break;
								case 1:
									LoadStateTargetActionType(item2, item4, sheetABInfo);
									break;
								}
							}
						}
					}
				}
			}
		}

		private void LoadChangeState(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetABInfo)
		{
			if (_sheetABInfo.assetbundle.IsNullOrEmpty() || _sheetABInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetABInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					int dx = 0;
					int result = 0;
					AnimalState value = AnimalState.None;
					ConditionType _value = ConditionType.None;
					string s = list.GetElement(dx++) ?? string.Empty;
					string text = list.GetElement(dx++) ?? string.Empty;
					string name = list.GetElement(dx++) ?? string.Empty;
					if (int.TryParse(s, out result) && AIProject.Animal.AnimalData.AnimalStateIDTable.TryGetValue(result, out value) && TryStringToEnum<ConditionType>(name, out _value))
					{
						SetConditionState(_animalType, _breedingType, list, dx, value, _value);
					}
				}
			}
		}

		private void SetConditionState(AnimalTypes _animalType, BreedingTypes _breedingType, List<string> _row, int _dx, AnimalState _animalState, ConditionType _conditionType)
		{
			StateCondition stateCondition = new StateCondition(_conditionType, _animalState);
			switch (_conditionType)
			{
			default:
				return;
			case ConditionType.Forced:
			{
				if (TryStringToEnum<AnimalState>(_row.GetElement(_dx++) ?? string.Empty, out var _value3))
				{
					stateCondition.AddNextState(_value3);
					break;
				}
				return;
			}
			case ConditionType.Proportion:
				while (_dx < _row.Count)
				{
					AnimalState _value2 = AnimalState.None;
					float result = 0f;
					string name2 = _row.GetElement(_dx++) ?? string.Empty;
					string s = _row.GetElement(_dx++) ?? string.Empty;
					if (!TryStringToEnum<AnimalState>(name2, out _value2) || !float.TryParse(s, out result))
					{
						break;
					}
					stateCondition.AddNextState(_value2, result);
				}
				break;
			case ConditionType.Random:
			case ConditionType.NonOverlap:
				while (_dx < _row.Count)
				{
					AnimalState _value = AnimalState.None;
					string name = _row.GetElement(_dx++) ?? string.Empty;
					if (TryStringToEnum<AnimalState>(name, out _value))
					{
						stateCondition.AddNextState(_value);
					}
				}
				break;
			}
			if (stateCondition.Count > 0)
			{
				if (!StateConditionTable.ContainsKey(_animalType))
				{
					StateConditionTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<AnimalState, StateCondition>>();
				}
				if (!StateConditionTable[_animalType].ContainsKey(_breedingType))
				{
					StateConditionTable[_animalType][_breedingType] = new Dictionary<AnimalState, StateCondition>();
				}
				StateConditionTable[_animalType][_breedingType][_animalState] = stateCondition;
			}
		}

		private void LoadStateTargetActionType(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetABInfo)
		{
			if (_sheetABInfo.assetbundle.IsNullOrEmpty() || _sheetABInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetABInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				int result = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				num++;
				string text = list.GetElement(num++) ?? string.Empty;
				string[] array = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
				if (!int.TryParse(s, out result) || !AIProject.Animal.AnimalData.AnimalStateIDTable.TryGetValue(result, out var value))
				{
					continue;
				}
				List<ActionTypes> list2 = new List<ActionTypes>();
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(array))
				{
					string[] array2 = array;
					foreach (string s2 in array2)
					{
						if (int.TryParse(s2, out var result2) && AnimalActionIDTable.TryGetValue(result2, out var value2))
						{
							list2.Add(value2);
						}
					}
				}
				if (!StateTargetActionTable.ContainsKey(_animalType))
				{
					StateTargetActionTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<AnimalState, List<ActionTypes>>>();
				}
				if (!StateTargetActionTable[_animalType].ContainsKey(_breedingType))
				{
					StateTargetActionTable[_animalType][_breedingType] = new Dictionary<AnimalState, List<ActionTypes>>();
				}
				StateTargetActionTable[_animalType][_breedingType][value] = list2;
			}
		}

		private void LoadDesire(AnimalDefinePack _animalDefinePack)
		{
			string desireInfoListBundleDirectory = _animalDefinePack.AssetBundleNames.DesireInfoListBundleDirectory;
			IReadOnlyList<UnityEx.ValueTuple<string, AnimalTypes>> animalNameList = AIProject.Animal.AnimalData.AnimalNameList;
			for (int i = 0; i < animalNameList.Count; i++)
			{
				string item = animalNameList[i].Item1;
				AnimalTypes item2 = animalNameList[i].Item2;
				IReadOnlyList<UnityEx.ValueTuple<string, BreedingTypes>> breedingNameList = AIProject.Animal.AnimalData.BreedingNameList;
				for (int j = 0; j < breedingNameList.Count; j++)
				{
					string item3 = breedingNameList[j].Item1;
					BreedingTypes item4 = breedingNameList[j].Item2;
					string directory = GetDirectory(desireInfoListBundleDirectory, item, item3);
					List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(directory);
					if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
					{
						continue;
					}
					assetBundleNameListFromPath.Sort();
					for (int k = 0; k < assetBundleNameListFromPath.Count; k++)
					{
						string text = assetBundleNameListFromPath[k];
						string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
						if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
						{
							continue;
						}
						ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
						if (excelData == null)
						{
							continue;
						}
						for (int l = 1; l < excelData.MaxCell; l++)
						{
							List<string> list = excelData.list[l].list;
							if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
							{
								continue;
							}
							int num = 0;
							string s = list.GetElement(num++) ?? string.Empty;
							if (!int.TryParse(s, out var result))
							{
								continue;
							}
							num++;
							AssetBundleInfo sheetInfo = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
							if (!sheetInfo.assetbundle.IsNullOrEmpty() && !sheetInfo.asset.IsNullOrEmpty())
							{
								switch (result)
								{
								case 0:
									LoadDesirePriority(item2, item4, sheetInfo);
									break;
								case 1:
									LoadDesireSpan(item2, item4, sheetInfo);
									break;
								case 2:
									LoadDesireBorder(item2, item4, sheetInfo);
									break;
								case 3:
									LoadDesireRate(item2, item4, sheetInfo);
									break;
								case 4:
									LoadDesireTargetState(item2, item4, sheetInfo);
									break;
								case 5:
									LoadDesireResult(item2, item4, sheetInfo);
									break;
								}
							}
						}
					}
				}
			}
		}

		private void LoadDesirePriority(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				int result = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out result) && int.TryParse(s2, out var result2) && AnimalDesireIDTable.TryGetValue(result2, out var value))
				{
					if (!DesirePriorityTable.ContainsKey(_animalType))
					{
						DesirePriorityTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<int, DesireType>>();
					}
					if (!DesirePriorityTable[_animalType].ContainsKey(_breedingType))
					{
						DesirePriorityTable[_animalType][_breedingType] = new Dictionary<int, DesireType>();
					}
					DesirePriorityTable[_animalType][_breedingType][result] = value;
				}
			}
		}

		private void LoadDesireSpan(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				int result = 0;
				int result2 = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				num++;
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out result) && int.TryParse(s2, out result2) && AnimalDesireIDTable.TryGetValue(result, out var value))
				{
					if (!DesireSpanTable.ContainsKey(_animalType))
					{
						DesireSpanTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<DesireType, int>>();
					}
					if (!DesireSpanTable[_animalType].ContainsKey(_breedingType))
					{
						DesireSpanTable[_animalType][_breedingType] = new Dictionary<DesireType, int>();
					}
					DesireSpanTable[_animalType][_breedingType][value] = result2;
				}
			}
		}

		private void LoadDesireBorder(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				float result = 0f;
				float result2 = 0f;
				string s = list.GetElement(num++) ?? string.Empty;
				num++;
				string s2 = list.GetElement(num++) ?? string.Empty;
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out var result3) && float.TryParse(s2, out result) && float.TryParse(s3, out result2) && AnimalDesireIDTable.TryGetValue(result3, out var value))
				{
					if (!DesireBorderTable.ContainsKey(_animalType))
					{
						DesireBorderTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<DesireType, System.Tuple<float, float>>>();
					}
					if (!DesireBorderTable[_animalType].ContainsKey(_breedingType))
					{
						DesireBorderTable[_animalType][_breedingType] = new Dictionary<DesireType, System.Tuple<float, float>>();
					}
					DesireBorderTable[_animalType][_breedingType][value] = new System.Tuple<float, float>(result, result2);
				}
			}
		}

		private void LoadDesireRate(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				int result = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out result) || !AIProject.Animal.AnimalData.AnimalStateIDTable.TryGetValue(result, out var value))
				{
					continue;
				}
				num++;
				int num2 = 0;
				while (num < list.Count)
				{
					if (AnimalDesireIDTable.TryGetValue(num2, out var value2))
					{
						float result2 = 0f;
						string s2 = list.GetElement(num) ?? string.Empty;
						if (float.TryParse(s2, out result2))
						{
							if (!DesireRateTable.ContainsKey(_animalType))
							{
								DesireRateTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<AnimalState, Dictionary<DesireType, float>>>();
							}
							if (!DesireRateTable[_animalType].ContainsKey(_breedingType))
							{
								DesireRateTable[_animalType][_breedingType] = new Dictionary<AnimalState, Dictionary<DesireType, float>>();
							}
							if (!DesireRateTable[_animalType][_breedingType].ContainsKey(value))
							{
								DesireRateTable[_animalType][_breedingType][value] = new Dictionary<DesireType, float>();
							}
							DesireRateTable[_animalType][_breedingType][value][value2] = result2;
						}
					}
					num++;
					num2++;
				}
			}
		}

		private void LoadDesireTargetState(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				int result = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				num++;
				string text = list.GetElement(num++) ?? string.Empty;
				string[] array = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
				if (!int.TryParse(s, out result) || !AnimalDesireIDTable.TryGetValue(result, out var value))
				{
					continue;
				}
				List<AnimalState> list2 = new List<AnimalState>();
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(array))
				{
					for (int j = 0; j < array.Length; j++)
					{
						string s2 = array[j] ?? string.Empty;
						if (int.TryParse(s2, out var result2) && AIProject.Animal.AnimalData.AnimalStateIDTable.TryGetValue(result2, out var value2))
						{
							list2.Add(value2);
						}
					}
				}
				if (!DesireTargetStateTable.ContainsKey(_animalType))
				{
					DesireTargetStateTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<DesireType, List<AnimalState>>>();
				}
				if (!DesireTargetStateTable[_animalType].ContainsKey(_breedingType))
				{
					DesireTargetStateTable[_animalType][_breedingType] = new Dictionary<DesireType, List<AnimalState>>();
				}
				DesireTargetStateTable[_animalType][_breedingType][value] = list2;
			}
		}

		private void LoadDesireResult(AnimalTypes _animalType, BreedingTypes _breedingType, AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			DesireType key = DesireType.None;
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				int result = 0;
				int result2 = 0;
				int result3 = 0;
				float result4 = 0f;
				float result5 = 0f;
				bool result6 = true;
				string s = list.GetElement(num++) ?? string.Empty;
				num++;
				if (int.TryParse(s, out result) && AnimalDesireIDTable.TryGetValue(result, out var value))
				{
					key = value;
				}
				string value2 = list.GetElement(num++) ?? string.Empty;
				string s2 = list.GetElement(num++) ?? string.Empty;
				string s3 = list.GetElement(num++) ?? string.Empty;
				string s4 = list.GetElement(num++) ?? string.Empty;
				string s5 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s2, out result2) && int.TryParse(s3, out result3) && AnimalDesireIDTable.TryGetValue(result2, out var value3) && ChangeTypeIDTable.TryGetValue(result3, out var value4) && bool.TryParse(value2, out result6) && float.TryParse(s4, out result4))
				{
					result5 = ((!float.TryParse(s5, out result5)) ? result4 : result5);
					if (!DesireResultTable.ContainsKey(_animalType))
					{
						DesireResultTable[_animalType] = new Dictionary<BreedingTypes, Dictionary<DesireType, Dictionary<bool, Dictionary<DesireType, ChangeParamState>>>>();
					}
					if (!DesireResultTable[_animalType].ContainsKey(_breedingType))
					{
						DesireResultTable[_animalType][_breedingType] = new Dictionary<DesireType, Dictionary<bool, Dictionary<DesireType, ChangeParamState>>>();
					}
					if (!DesireResultTable[_animalType][_breedingType].ContainsKey(key))
					{
						DesireResultTable[_animalType][_breedingType][key] = new Dictionary<bool, Dictionary<DesireType, ChangeParamState>>();
					}
					if (!DesireResultTable[_animalType][_breedingType][key].ContainsKey(result6))
					{
						DesireResultTable[_animalType][_breedingType][key][result6] = new Dictionary<DesireType, ChangeParamState>();
					}
					DesireResultTable[_animalType][_breedingType][key][result6][value3] = new ChangeParamState(value4, result4, result5);
				}
			}
		}

		private void LoadWithActor(AnimalDefinePack _animalDefinePack)
		{
			string withActorAnimeInfoListBundleDirectory = _animalDefinePack.AssetBundleNames.WithActorAnimeInfoListBundleDirectory;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(withActorAnimeInfoListBundleDirectory);
			if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						num++;
						AssetBundleInfo sheetInfo = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
						if (!sheetInfo.assetbundle.IsNullOrEmpty() && !sheetInfo.asset.IsNullOrEmpty() && result == 0)
						{
							LoadWithAgentAnimationList(sheetInfo, _animalDefinePack);
						}
					}
				}
			}
		}

		private void LoadWithAgentAnimationList(AssetBundleInfo _sheetInfo, AnimalDefinePack _animalDefinePack)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					int num = 1;
					_sheetInfo = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
					if (!_sheetInfo.assetbundle.IsNullOrEmpty() && !_sheetInfo.asset.IsNullOrEmpty())
					{
						LoadWithAgentAnimationState(_sheetInfo, _animalDefinePack);
					}
				}
			}
		}

		private void LoadWithAgentAnimationState(AssetBundleInfo _sheetInfo, AnimalDefinePack _animalDefinePack)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null || excelData.MaxCell == 0)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 2;
				int result = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out result) || result < 0)
				{
					continue;
				}
				int result2 = 0;
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s2, out result2))
				{
					string assetbundle_ = list.GetElement(num++) ?? string.Empty;
					string asset_ = list.GetElement(num++) ?? string.Empty;
					string text = list.GetElement(num++) ?? string.Empty;
					string[] inStateNames = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
					if (!bool.TryParse(list.GetElement(num++) ?? string.Empty, out var result3))
					{
						result3 = false;
					}
					if (!float.TryParse(list.GetElement(num++) ?? string.Empty, out var result4))
					{
						result4 = 0f;
					}
					string text2 = list.GetElement(num++) ?? string.Empty;
					string[] outStateNames = text2.Split(separators, StringSplitOptions.RemoveEmptyEntries);
					if (!bool.TryParse(list.GetElement(num++) ?? string.Empty, out var result5))
					{
						result5 = false;
					}
					if (!float.TryParse(list.GetElement(num++) ?? string.Empty, out var result6))
					{
						result6 = 0f;
					}
					if (!int.TryParse(list.GetElement(num++) ?? string.Empty, out var result7))
					{
						result7 = 0;
					}
					if (!int.TryParse(list.GetElement(num++) ?? string.Empty, out var result8))
					{
						result8 = 0;
					}
					if (!int.TryParse(list.GetElement(num++) ?? string.Empty, out var result9))
					{
						result9 = 0;
					}
					float actionPointDistance = _animalDefinePack.WithActorInfo.ActionPointDistance;
					float result10 = 0f;
					if (!float.TryParse(list.GetElement(num++) ?? string.Empty, out result10))
					{
						result10 = actionPointDistance;
					}
					float result11 = 0f;
					if (!float.TryParse(list.GetElement(num++) ?? string.Empty, out result11))
					{
						result11 = actionPointDistance;
					}
					float result12 = 0f;
					if (!float.TryParse(list.GetElement(num++) ?? string.Empty, out result12))
					{
						result12 = actionPointDistance;
					}
					AnimalPlayState animalPlayState = new AnimalPlayState(result9, result2, inStateNames, outStateNames);
					animalPlayState.FloatList = new float[3] { result10, result11, result12 };
					AnimalPlayState animalPlayState2 = animalPlayState;
					AnimalPlayState.PlayStateInfo mainStateInfo = animalPlayState2.MainStateInfo;
					mainStateInfo.AssetBundleInfo = new AssetBundleInfo(string.Empty, assetbundle_, asset_, string.Empty);
					mainStateInfo.InFadeEnable = result3;
					mainStateInfo.InFadeSecond = result4;
					mainStateInfo.OutFadeEnable = result5;
					mainStateInfo.OutFadeSecond = result6;
					mainStateInfo.IsLoop = 0 < result7 || 0 < result8;
					mainStateInfo.LoopMin = result7;
					mainStateInfo.LoopMax = result8;
					mainStateInfo.Controller = LoadAnimator(mainStateInfo.AssetBundleInfo);
					if (!WithAgentAnimeTable.TryGetValue(result, out var value) || value == null)
					{
						Dictionary<int, AnimalPlayState> dictionary = new Dictionary<int, AnimalPlayState>();
						WithAgentAnimeTable[result] = dictionary;
						value = dictionary;
					}
					value[result2] = animalPlayState2;
				}
			}
		}

		private void LoadPlayerInfo(AnimalDefinePack _definePack)
		{
			string playerInfoListBundleDirectory = _definePack.AssetBundleNames.PlayerInfoListBundleDirectory;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(playerInfoListBundleDirectory);
			if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						num++;
						AssetBundleInfo sheetInfo = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
						if (!sheetInfo.assetbundle.IsNullOrEmpty() && !sheetInfo.asset.IsNullOrEmpty() && result == 0)
						{
							LoadPlayerCatchAnimalPoseTable(sheetInfo);
						}
					}
				}
			}
		}

		private void LoadPlayerCatchAnimalPoseTable(AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					int num = 1;
					AssetBundleInfo sheetInfo = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
					if (!sheetInfo.assetbundle.IsNullOrEmpty() && !sheetInfo.asset.IsNullOrEmpty())
					{
						LoadPlayerCatchAnimalPoseList(sheetInfo);
					}
				}
			}
		}

		private void LoadPlayerCatchAnimalPoseList(AssetBundleInfo _sheetInfo)
		{
			if (_sheetInfo.assetbundle.IsNullOrEmpty() || _sheetInfo.asset.IsNullOrEmpty())
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string s4 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s4, out var result4))
				{
					if (!PlayerCatchAnimalPoseTable.TryGetValue(result, out var value))
					{
						value = (PlayerCatchAnimalPoseTable[result] = new Dictionary<int, PoseKeyPair>());
					}
					value[result2] = new PoseKeyPair
					{
						postureID = result3,
						poseID = result4
					};
				}
			}
		}

		private void LoadAnimalPoint(AnimalDefinePack _define)
		{
			string animalPointPrefabBundleDirectory = _define.AssetBundleNames.AnimalPointPrefabBundleDirectory;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(animalPointPrefabBundleDirectory);
			if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
					if (!(excelData == null))
					{
						int num = 1;
						LoadAnimalPointPrefabInfo(excelData.list.GetElement(num++)?.list, fileNameWithoutExtension);
					}
				}
			}
		}

		private void LoadAnimalPointPrefabInfo(List<string> _pathRow, string _ver)
		{
			if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(_pathRow))
			{
				return;
			}
			int num = 1;
			AssetBundleInfo info = new AssetBundleInfo(string.Empty, _pathRow.GetElement(num++) ?? string.Empty, _pathRow.GetElement(num++) ?? string.Empty, _pathRow.GetElement(num++) ?? string.Empty);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
			if (excelData == null || excelData.MaxCell <= 1)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.Animal.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num2 = 0;
				string name_ = list.GetElement(num2++) ?? string.Empty;
				int result = 0;
				string s = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s, out result))
				{
					continue;
				}
				int result2 = 0;
				string s2 = list.GetElement(num2++) ?? string.Empty;
				if (int.TryParse(s2, out result2))
				{
					string assetbundle_ = list.GetElement(num2++) ?? string.Empty;
					string asset_ = list.GetElement(num2++) ?? string.Empty;
					string manifest_ = list.GetElement(num2++) ?? string.Empty;
					if (!AnimalPointAssetTable.ContainsKey(result))
					{
						AnimalPointAssetTable[result] = new Dictionary<int, AssetBundleInfo>();
					}
					AnimalPointAssetTable[result][result2] = new AssetBundleInfo(name_, assetbundle_, asset_, manifest_);
				}
			}
		}

		public AIProject.Animal.Tuple<string, string, string> GetName(List<string> _row, int _bundleIndex, int _assetIndex, int _manifestIndex)
		{
			return new AIProject.Animal.Tuple<string, string, string>(_row.GetElement(_bundleIndex) ?? string.Empty, _row.GetElement(_assetIndex) ?? string.Empty, _row.GetElement(_manifestIndex) ?? string.Empty);
		}

		public T LoadAsset<T>(List<string> _row, int _bundleIndex, int _assetIndex, int _manifestIndex) where T : UnityEngine.Object
		{
			AIProject.Animal.Tuple<string, string, string> name = GetName(_row, _bundleIndex, _assetIndex, _manifestIndex);
			return AssetUtility.LoadAsset<T>(name.Item1, name.Item2, name.Item3);
		}

		private bool TryStringToEnum<T>(string _name, out T _value) where T : struct
		{
			return Enum.TryParse<T>(_name, ignoreCase: true, out _value);
		}
	}

	public class AnimationTables
	{
		private enum AnimatorLoadMap
		{
			Chara,
			Player,
			Merchant,
			Item
		}

		private enum AgentLoadingMap
		{
			Personal,
			Action,
			Move,
			EventKey,
			Locomotion,
			Common,
			Greet,
			TalkSpeaker,
			TalkListener,
			Animal
		}

		private enum PlayerLoadingMap
		{
			Action,
			Move,
			EventKey,
			Locomotion,
			Common
		}

		public enum CommonLoadMap
		{
			Stand,
			Chair,
			Sit,
			Sleep
		}

		private Dictionary<int, AssetBundleInfo> _playerAnimatorAssetTable = new Dictionary<int, AssetBundleInfo>();

		private Dictionary<int, AssetBundleInfo> _charaAnimatorAssetTable = new Dictionary<int, AssetBundleInfo>();

		private Dictionary<int, AssetBundleInfo> _merchantAnimatorAssetTable = new Dictionary<int, AssetBundleInfo>();

		private Dictionary<int, AssetBundleInfo> _itemAnimatorAssetTable = new Dictionary<int, AssetBundleInfo>();

		private Regex _regex = new Regex("<((?:[\\w/.-]*[,]*)+)>");

		private static readonly string[] _separators = new string[2] { "/", "／" };

		private static readonly char[] _comma = new char[1] { ',' };

		private static readonly string[] _seEventSepa = new string[1] { ">" };

		private static readonly string[] _seEventRemoveStr = new string[2] { "<", ">" };

		public Dictionary<int, Dictionary<int, List<int>>> PersonalActionListTable { get; private set; } = new Dictionary<int, Dictionary<int, List<int>>>();


		public Dictionary<int, Dictionary<int, PlayState>> AgentActionAnimTable { get; private set; } = new Dictionary<int, Dictionary<int, PlayState>>();


		public Dictionary<int, List<AnimeMoveInfo>> AgentMoveInfoTable { get; private set; } = new Dictionary<int, List<AnimeMoveInfo>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> AgentItemEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> AgentAnimalEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>> AgentActSEEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>> AgentActOnceVoiceEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<int>>>> AgentActLoopVoiceEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<int>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>> AgentActParticleEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> AgentChangeClothEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>();


		public Dictionary<string, List<PlayState.ItemInfo>> SurpriseItemList { get; private set; } = new Dictionary<string, List<PlayState.ItemInfo>>();


		public Dictionary<int, PlayState> AgentLocomotionStateTable { get; private set; } = new Dictionary<int, PlayState>();


		public Dictionary<int, PoseKeyPair> TalkSpeakerStateTable { get; private set; } = new Dictionary<int, PoseKeyPair>();


		public Dictionary<int, PoseKeyPair> TalkListenerStateTable { get; private set; } = new Dictionary<int, PoseKeyPair>();


		public Dictionary<int, int> TalkSpeakerRelationTable { get; private set; } = new Dictionary<int, int>();


		public Dictionary<int, int> TalkListenerRelationTable { get; private set; } = new Dictionary<int, int>();


		public Dictionary<int, TriValues> ItemScaleTable { get; private set; } = new Dictionary<int, TriValues>();


		public Dictionary<int, Dictionary<int, PlayState>> WithAnimalStateTable { get; private set; } = new Dictionary<int, Dictionary<int, PlayState>>();


		public Dictionary<int, FootStepInfo[]> AgentFootStepEventKeyTable { get; private set; } = new Dictionary<int, FootStepInfo[]>();


		public Dictionary<int, Dictionary<int, UnityEx.ValueTuple<PoseKeyPair, bool>>> AgentGravurePoseTable { get; private set; } = new Dictionary<int, Dictionary<int, UnityEx.ValueTuple<PoseKeyPair, bool>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, PlayState>>> PlayerActionAnimTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, PlayState>>>();


		public Dictionary<int, Dictionary<int, List<AnimeMoveInfo>>> PlayerMoveInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, List<AnimeMoveInfo>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>> PlayerItemEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>> PlayerActExEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>>> PlayerActSEEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>>> PlayerActParticleEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>>>();


		public Dictionary<int, Dictionary<int, PlayState>> PlayerLocomotionStateTable { get; private set; } = new Dictionary<int, Dictionary<int, PlayState>>();


		public Dictionary<int, Dictionary<int, FootStepInfo[]>> PlayerFootStepEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, FootStepInfo[]>>();


		public Dictionary<int, Dictionary<int, PlayState>> MerchantOnlyActionAnimStateTable { get; private set; } = new Dictionary<int, Dictionary<int, PlayState>>();


		public Dictionary<int, Dictionary<int, PlayState>> MerchantCommonActionAnimStateTable { get; private set; } = new Dictionary<int, Dictionary<int, PlayState>>();


		public Dictionary<int, PlayState> MerchantLocomotionStateTable { get; private set; } = new Dictionary<int, PlayState>();


		public Dictionary<int, List<AnimeMoveInfo>> MerchantMoveInfoTable { get; private set; } = new Dictionary<int, List<AnimeMoveInfo>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> MerchantOnlyItemEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> MerchantCommonItemEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>> MerchantOnlySEEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>> MerchantCommonSEEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>> MerchantOnlyOnceVoiceEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>> MerchantCommonOnceVoiceEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<int>>>> MerchantOnlyLoopVoiceEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<int>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<int>>>> MerchantCommonLoopVoiceEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<int>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>> MerchantOnlyParticleEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>> MerchantCommonParticleEventKeyTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>>();


		public Dictionary<int, PoseKeyPair> MerchantListenerStateTable { get; private set; } = new Dictionary<int, PoseKeyPair>();


		public Dictionary<int, int> MerchantListenerRelationTable { get; private set; } = new Dictionary<int, int>();


		public Dictionary<int, FootStepInfo[]> MerchantFootStepEventKeyTable { get; private set; } = new Dictionary<int, FootStepInfo[]>();


		public RuntimeAnimatorController GetPlayerAnimator(int id, ref AssetBundleInfo outInfo)
		{
			if (!_playerAnimatorAssetTable.TryGetValue(id, out var value))
			{
				return null;
			}
			outInfo = value;
			return AssetUtility.LoadAsset<RuntimeAnimatorController>(value);
		}

		public RuntimeAnimatorController GetCharaAnimator(int id, ref AssetBundleInfo outInfo)
		{
			if (!_charaAnimatorAssetTable.TryGetValue(id, out var value))
			{
				return null;
			}
			outInfo = value;
			return AssetUtility.LoadAsset<RuntimeAnimatorController>(value);
		}

		public RuntimeAnimatorController GetMerchantAnimator(int id, ref AssetBundleInfo outInfo)
		{
			if (!_merchantAnimatorAssetTable.TryGetValue(id, out var value))
			{
				return null;
			}
			outInfo = value;
			return AssetUtility.LoadAsset<RuntimeAnimatorController>(value);
		}

		public RuntimeAnimatorController GetItemAnimator(int id)
		{
			if (!_itemAnimatorAssetTable.TryGetValue(id, out var value))
			{
				return null;
			}
			return AssetUtility.LoadAsset<RuntimeAnimatorController>(value);
		}

		public void SyncLoad(DefinePack definePack)
		{
			if (!(definePack == null))
			{
				Load(definePack, awaitable: false);
			}
		}

		public void Load(DefinePack definePack, bool awaitable = true)
		{
			LoadAnimatorAssetBundles(definePack, awaitable);
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentAnimeInfo);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					string element = param.list.GetElement(0);
					if (int.TryParse(element, out var result))
					{
						switch (result)
						{
						case 0:
							LoadAgentActionPersonalIDs(param.list, i, awaitable);
							break;
						case 1:
							LoadAgentActionStates(param.list, i, awaitable);
							break;
						case 2:
							LoadAgentMoveInfoList(param.list, i, awaitable);
							break;
						case 3:
							LoadAgentEventKeyList(param.list, i, awaitable);
							break;
						case 4:
							LoadAgentLocomotionStateList(param.list, i, awaitable);
							break;
						case 7:
							LoadTalkSpeakerStateList(param.list, i, awaitable);
							break;
						case 8:
							LoadTalkListenerStateList(param.list, i, awaitable);
							break;
						case 9:
							LoadWithAnimalStateList(param.list, i, awaitable);
							break;
						}
					}
				}
			}
			List<string> assetBundleNameListFromPath2 = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.PlayerMaleAnimeInfo);
			assetBundleNameListFromPath2.Sort();
			for (int j = 0; j < assetBundleNameListFromPath2.Count; j++)
			{
				string text2 = assetBundleNameListFromPath2[j];
				string fileNameWithoutExtension2 = System.IO.Path.GetFileNameWithoutExtension(text2);
				ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(text2, fileNameWithoutExtension2, string.Empty);
				if (excelData2 == null)
				{
					continue;
				}
				int num2 = 1;
				while (num2 < excelData2.MaxCell)
				{
					ExcelData.Param param2 = excelData2.list[num2++];
					string s = param2.list[0];
					if (int.TryParse(s, out var result2))
					{
						switch (result2)
						{
						case 0:
							LoadPlayerActionStates(param2.list, j, 0, awaitable);
							break;
						case 1:
							LoadPlayerMoveInfoList(param2.list, j, 0, awaitable);
							break;
						case 2:
							LoadPlayerEventKeyList(param2.list, j, 0, awaitable);
							break;
						case 3:
							LoadPlayerLocomotionStateList(param2.list, j, 0, awaitable);
							break;
						}
					}
				}
			}
			List<string> assetBundleNameListFromPath3 = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.PlayerFemaleAnimeInfo);
			assetBundleNameListFromPath3.Sort();
			for (int k = 0; k < assetBundleNameListFromPath3.Count; k++)
			{
				string text3 = assetBundleNameListFromPath3[k];
				string fileNameWithoutExtension3 = System.IO.Path.GetFileNameWithoutExtension(text3);
				ExcelData excelData3 = AssetUtility.LoadAsset<ExcelData>(text3, fileNameWithoutExtension3, string.Empty);
				if (excelData3 == null)
				{
					continue;
				}
				int num3 = 1;
				while (num3 < excelData3.MaxCell)
				{
					ExcelData.Param param3 = excelData3.list[num3++];
					string element2 = param3.list.GetElement(0);
					if (int.TryParse(element2, out var result3))
					{
						switch (result3)
						{
						case 0:
							LoadPlayerActionStates(param3.list, k, 1, awaitable);
							break;
						case 1:
							LoadPlayerMoveInfoList(param3.list, k, 1, awaitable);
							break;
						case 2:
							LoadPlayerEventKeyList(param3.list, k, 1, awaitable);
							break;
						case 3:
							LoadPlayerLocomotionStateList(param3.list, k, 1, awaitable);
							break;
						}
					}
				}
			}
			List<string> assetBundleNameListFromPath4 = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.MerchantAnimeInfo);
			assetBundleNameListFromPath4.Sort();
			for (int l = 0; l < assetBundleNameListFromPath4.Count; l++)
			{
				string text4 = assetBundleNameListFromPath4[l];
				string fileNameWithoutExtension4 = System.IO.Path.GetFileNameWithoutExtension(text4);
				ExcelData excelData4 = AssetUtility.LoadAsset<ExcelData>(text4, fileNameWithoutExtension4, string.Empty);
				if (excelData4 == null)
				{
					continue;
				}
				int num4 = 1;
				while (num4 < excelData4.MaxCell)
				{
					ExcelData.Param param4 = excelData4.list[num4++];
					string s2 = param4.list.GetElement(0) ?? string.Empty;
					if (int.TryParse(s2, out var result4))
					{
						List<string> list = param4.list;
						switch (result4)
						{
						case 0:
							LoadMerchantOnlyActionAnimState(list, fileNameWithoutExtension4, awaitable);
							break;
						case 1:
							LoadMerchantCommonActionState(list, fileNameWithoutExtension4, awaitable);
							break;
						case 2:
							LoadMerchantLocomotionStateList(list, fileNameWithoutExtension4, awaitable);
							break;
						case 3:
							LoadMerchantMoveInfoList(list, fileNameWithoutExtension4, awaitable);
							break;
						case 4:
							LoadMerchantEventKeyList(list, fileNameWithoutExtension4, awaitable);
							break;
						case 5:
							LoadMerchantListenerStateList(list, fileNameWithoutExtension4, awaitable);
							break;
						}
					}
				}
			}
			LoadItemScaleTable(definePack);
			LoadAgentGravurePoseTable(definePack, awaitable);
			LoadAgentEventKeyTable(definePack, awaitable);
			LoadChangeClothEventKeyTable(definePack, awaitable);
			LoadPlayerEventKeyTable(definePack, awaitable);
			LoadSurpriseItemList(definePack);
			Singleton<Resources>.Instance.LoadMapIK(definePack);
		}

		public void Release()
		{
			_playerAnimatorAssetTable.Clear();
			_charaAnimatorAssetTable.Clear();
			_merchantAnimatorAssetTable.Clear();
			_itemAnimatorAssetTable.Clear();
			PersonalActionListTable.Clear();
			AgentActionAnimTable.Clear();
			AgentMoveInfoTable.Clear();
			AgentItemEventKeyTable.Clear();
			AgentAnimalEventKeyTable.Clear();
			AgentActSEEventKeyTable.Clear();
			AgentActOnceVoiceEventKeyTable.Clear();
			AgentActLoopVoiceEventKeyTable.Clear();
			AgentActParticleEventKeyTable.Clear();
			AgentChangeClothEventKeyTable.Clear();
			SurpriseItemList.Clear();
			AgentLocomotionStateTable.Clear();
			TalkSpeakerStateTable.Clear();
			TalkListenerStateTable.Clear();
			TalkSpeakerRelationTable.Clear();
			TalkListenerRelationTable.Clear();
			ItemScaleTable.Clear();
			WithAnimalStateTable.Clear();
			AgentFootStepEventKeyTable.Clear();
			AgentGravurePoseTable.Clear();
			PlayerActionAnimTable.Clear();
			PlayerMoveInfoTable.Clear();
			PlayerItemEventKeyTable.Clear();
			PlayerActExEventKeyTable.Clear();
			PlayerActSEEventKeyTable.Clear();
			PlayerActParticleEventKeyTable.Clear();
			PlayerLocomotionStateTable.Clear();
			PlayerFootStepEventKeyTable.Clear();
			MerchantOnlyActionAnimStateTable.Clear();
			MerchantCommonActionAnimStateTable.Clear();
			MerchantLocomotionStateTable.Clear();
			MerchantMoveInfoTable.Clear();
			MerchantOnlyItemEventKeyTable.Clear();
			MerchantCommonItemEventKeyTable.Clear();
			MerchantOnlySEEventKeyTable.Clear();
			MerchantCommonSEEventKeyTable.Clear();
			MerchantOnlyOnceVoiceEventKeyTable.Clear();
			MerchantCommonOnceVoiceEventKeyTable.Clear();
			MerchantOnlyLoopVoiceEventKeyTable.Clear();
			MerchantCommonLoopVoiceEventKeyTable.Clear();
			MerchantOnlyParticleEventKeyTable.Clear();
			MerchantCommonParticleEventKeyTable.Clear();
			MerchantListenerStateTable.Clear();
			MerchantListenerRelationTable.Clear();
			MerchantFootStepEventKeyTable.Clear();
			Singleton<Resources>.Instance.MapIKData.Clear();
		}

		private void LoadAnimatorAssetBundles(DefinePack definePack, bool awaitable)
		{
			if (Debug.isDebugBuild)
			{
				Debug.Log("アニメーターリスト");
			}
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActorAnimatorList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					string element = param.list.GetElement(0);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					switch (result)
					{
					case 0:
						if (Debug.isDebugBuild)
						{
							Debug.Log("キャラのアニメーターリスト");
						}
						LoadAnimator(param.list, _charaAnimatorAssetTable);
						break;
					case 1:
						if (Debug.isDebugBuild)
						{
							Debug.Log("プレイヤーのアニメーターリスト");
						}
						LoadAnimator(param.list, _playerAnimatorAssetTable);
						break;
					case 2:
						if (Debug.isDebugBuild)
						{
							Debug.Log("商人のアニメーターリスト");
						}
						LoadAnimator(param.list, _merchantAnimatorAssetTable);
						break;
					case 3:
						if (Debug.isDebugBuild)
						{
							Debug.Log("マップアイテムのアニメーターリスト");
						}
						LoadAnimator(param.list, _itemAnimatorAssetTable);
						break;
					}
				}
			}
		}

		private void LoadAnimator(List<string> row, Dictionary<int, AssetBundleInfo> dictionary)
		{
			int num = 2;
			string element = row.GetElement(num++);
			string element2 = row.GetElement(num++);
			num++;
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(element, element2, string.Empty);
			if (!(excelData != null))
			{
				return;
			}
			int num2 = 1;
			while (num2 < excelData.MaxCell)
			{
				ExcelData.Param param = excelData.list[num2++];
				int num3 = 0;
				if (int.TryParse(param.list.GetElement(num3++), out var result))
				{
					string element3 = param.list.GetElement(num3++);
					string element4 = param.list.GetElement(num3++);
					string element5 = param.list.GetElement(num3++);
					string element6 = param.list.GetElement(num3++);
					dictionary[result] = new AssetBundleInfo
					{
						name = element3,
						assetbundle = element4,
						asset = element5,
						manifest = element6
					};
				}
			}
		}

		private void LoadPlayerActionStates(List<string> row, int id, int sex, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row[num++];
			string assetName = row[num++];
			string manifestName = row[num++];
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (!(excelData != null))
			{
				return;
			}
			if (!PlayerActionAnimTable.TryGetValue(sex, out var value))
			{
				Dictionary<int, Dictionary<int, PlayState>> dictionary = new Dictionary<int, Dictionary<int, PlayState>>();
				PlayerActionAnimTable[sex] = dictionary;
				value = dictionary;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				if (!param.list.IsNullOrEmpty())
				{
					int num2 = 0;
					string element = param.list.GetElement(num2++);
					if (!element.IsNullOrEmpty())
					{
						string assetbundleName2 = param.list[num2++];
						string assetName2 = param.list[num2++];
						string manifestName2 = param.list[num2++];
						ExcelData actionExcel = AssetUtility.LoadAsset<ExcelData>(assetbundleName2, assetName2, manifestName2);
						LoadActionAnimationInfo(actionExcel, value, awaitable);
					}
				}
			}
		}

		private void LoadPlayerMoveInfoList(List<string> row, int id, int sex, bool awaitable)
		{
			int num = 2;
			string element = row.GetElement(num++);
			string element2 = row.GetElement(num++);
			row.GetElement(num++);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(element, element2, string.Empty);
			if (excelData == null)
			{
				return;
			}
			if (!PlayerMoveInfoTable.TryGetValue(sex, out var value))
			{
				Dictionary<int, List<AnimeMoveInfo>> dictionary = new Dictionary<int, List<AnimeMoveInfo>>();
				PlayerMoveInfoTable[sex] = dictionary;
				value = dictionary;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				int num2 = 0;
				string element3 = param.list.GetElement(num2++);
				int key = Animator.StringToHash(element3);
				if (!value.TryGetValue(key, out var value2))
				{
					List<AnimeMoveInfo> list2 = (value[key] = new List<AnimeMoveInfo>());
					value2 = list2;
				}
				while (num2 < param.list.Count)
				{
					string element4 = param.list.GetElement(num2++);
					string element5 = param.list.GetElement(num2++);
					string element6 = param.list.GetElement(num2++);
					if (!element6.IsNullOrEmpty() && float.TryParse(element4, out var result) && float.TryParse(element5, out var result2))
					{
						AnimeMoveInfo animeMoveInfo = default(AnimeMoveInfo);
						animeMoveInfo.start = result;
						animeMoveInfo.end = result2;
						animeMoveInfo.movePoint = element6;
						AnimeMoveInfo item = animeMoveInfo;
						value2.Add(item);
					}
				}
			}
		}

		private void LoadPlayerEventKeyList(List<string> row, int id, int sex, bool awaitable)
		{
			int num = 2;
			string element = row.GetElement(num++);
			string element2 = row.GetElement(num++);
			row.GetElement(num++);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(element, element2, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				int num2 = 1;
				string element3 = param.list.GetElement(num2++);
				string element4 = param.list.GetElement(num2++);
				if (element3.IsNullOrEmpty() || element4.IsNullOrEmpty())
				{
					continue;
				}
				if (element4.Contains("item"))
				{
					if (!PlayerItemEventKeyTable.TryGetValue(sex, out var value))
					{
						Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> dictionary = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>();
						PlayerItemEventKeyTable[sex] = dictionary;
						value = dictionary;
					}
					LoadEventKeyTable(param.list, value, awaitable);
				}
				else if (element4.Contains("actex"))
				{
					if (!PlayerActExEventKeyTable.TryGetValue(sex, out var value2))
					{
						Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> dictionary = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>>();
						PlayerActExEventKeyTable[sex] = dictionary;
						value2 = dictionary;
					}
					LoadEventKeyTable(param.list, value2, awaitable);
				}
			}
		}

		private void LoadPlayerLocomotionStateList(List<string> row, int id, int sex, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row[num++];
			string assetName = row[num++];
			string manifestName = row[num++];
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData != null)
			{
				Dictionary<int, PlayState> dictionary = new Dictionary<int, PlayState>();
				PlayerLocomotionStateTable[sex] = dictionary;
				Dictionary<int, PlayState> dic = dictionary;
				LoadLocomotionStateList(excelData, dic, awaitable);
			}
		}

		private void LoadAgentActionPersonalIDs(List<string> row, int id, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row[num++];
			string assetName = row[num++];
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, string.Empty);
			if (!(excelData != null))
			{
				return;
			}
			foreach (ExcelData.Param item in excelData.list)
			{
				if (item.list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 1;
				string assetbundleName2 = item.list[num2++];
				string assetName2 = item.list[num2++];
				ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(assetbundleName2, assetName2, string.Empty);
				if (excelData2 == null)
				{
					continue;
				}
				foreach (ExcelData.Param item2 in excelData2.list)
				{
					num2 = 1;
					if (!int.TryParse(item2.list.GetElement(num2++), out var result))
					{
						continue;
					}
					if (!PersonalActionListTable.TryGetValue(result, out var value))
					{
						Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
						PersonalActionListTable[result] = dictionary;
						value = dictionary;
					}
					if (!int.TryParse(item2.list.GetElement(num2++), out var result2))
					{
						continue;
					}
					if (!value.TryGetValue(result2, out var value2))
					{
						List<int> list2 = (value[result2] = new List<int>());
						value2 = list2;
					}
					string element = item2.list.GetElement(num2);
					if (element.IsNullOrEmpty())
					{
						continue;
					}
					string[] array = element.Split(_comma, StringSplitOptions.RemoveEmptyEntries);
					string[] array2 = array;
					foreach (string s in array2)
					{
						if (int.TryParse(s, out var result3) && !value2.Contains(result3))
						{
							value2.Add(result3);
						}
					}
				}
			}
		}

		private void LoadAgentActionStates(List<string> row, int id, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row[num++];
			string assetName = row[num++];
			string manifestName = row[num++];
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (!(excelData != null))
			{
				return;
			}
			Dictionary<int, Dictionary<int, PlayState>> agentActionAnimTable = AgentActionAnimTable;
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				if (!param.list.IsNullOrEmpty())
				{
					int num2 = 0;
					string element = param.list.GetElement(num2++);
					if (!element.IsNullOrEmpty())
					{
						string assetbundleName2 = param.list[num2++];
						string assetName2 = param.list[num2++];
						string manifestName2 = param.list[num2++];
						ExcelData actionExcel = AssetUtility.LoadAsset<ExcelData>(assetbundleName2, assetName2, manifestName2);
						LoadActionAnimationInfo(actionExcel, agentActionAnimTable, awaitable);
					}
				}
			}
		}

		private void LoadAgentMoveInfoList(List<string> row, int id, bool awaitablw)
		{
			int num = 2;
			string element = row.GetElement(num++);
			string element2 = row.GetElement(num++);
			row.GetElement(num++);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(element, element2, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				int num2 = 0;
				string element3 = param.list.GetElement(num2++);
				int key = Animator.StringToHash(element3);
				if (!AgentMoveInfoTable.TryGetValue(key, out var value))
				{
					List<AnimeMoveInfo> list = new List<AnimeMoveInfo>();
					AgentMoveInfoTable[key] = list;
					value = list;
				}
				while (num2 < param.list.Count)
				{
					string element4 = param.list.GetElement(num2++);
					string element5 = param.list.GetElement(num2++);
					string element6 = param.list.GetElement(num2++);
					if (!element6.IsNullOrEmpty() && float.TryParse(element4, out var result) && float.TryParse(element5, out var result2))
					{
						AnimeMoveInfo animeMoveInfo = default(AnimeMoveInfo);
						animeMoveInfo.start = result;
						animeMoveInfo.end = result2;
						animeMoveInfo.movePoint = element6;
						AnimeMoveInfo item = animeMoveInfo;
						value.Add(item);
					}
				}
			}
		}

		private void LoadAgentEventKeyList(List<string> row, int id, bool awaitable)
		{
			int num = 2;
			string element = row.GetElement(num++);
			string element2 = row.GetElement(num++);
			row.GetElement(num++);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(element, element2, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				int num2 = 1;
				string element3 = param.list.GetElement(num2++);
				string element4 = param.list.GetElement(num2++);
				if (!element3.IsNullOrEmpty() && !element4.IsNullOrEmpty() && element4.Contains("item"))
				{
					LoadEventKeyTable(param.list, AgentItemEventKeyTable, awaitable);
				}
			}
		}

		private void LoadAgentLocomotionStateList(List<string> row, int id, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row[num++];
			string assetName = row[num++];
			string manifestName = row[num++];
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			LoadLocomotionStateList(excelData, AgentLocomotionStateTable, awaitable);
		}

		private void LoadTalkSpeakerStateList(List<string> row, int id, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row[num++];
			string assetName = row[num++];
			string manifestName = row[num++];
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (!(excelData != null))
			{
				return;
			}
			int num2 = 1;
			while (num2 < excelData.MaxCell)
			{
				ExcelData.Param param = excelData.list[num2++];
				int num3 = 0;
				string element = param.list.GetElement(num3++);
				if (!int.TryParse(element, out var result))
				{
					continue;
				}
				num3++;
				string element2 = param.list.GetElement(num3++);
				if (int.TryParse(element2, out var result2))
				{
					string element3 = param.list.GetElement(num3++);
					if (int.TryParse(element3, out var result3))
					{
						TalkSpeakerStateTable[result] = new PoseKeyPair
						{
							postureID = result2,
							poseID = result3
						};
					}
				}
			}
		}

		private void LoadTalkListenerStateList(List<string> row, int id, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row[num++];
			string assetName = row[num++];
			string manifestName = row[num++];
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (!(excelData != null))
			{
				return;
			}
			int num2 = 1;
			while (num2 < excelData.MaxCell)
			{
				ExcelData.Param param = excelData.list[num2++];
				int num3 = 0;
				string element = param.list.GetElement(num3++);
				if (!int.TryParse(element, out var result))
				{
					continue;
				}
				num3++;
				string element2 = param.list.GetElement(num3++);
				if (int.TryParse(element2, out var result2))
				{
					string element3 = param.list.GetElement(num3++);
					if (int.TryParse(element3, out var result3))
					{
						TalkListenerStateTable[result] = new PoseKeyPair
						{
							postureID = result2,
							poseID = result3
						};
						string element4 = param.list.GetElement(num3++);
						int result4;
						int value = (int.TryParse(element4, out result4) ? result4 : 0);
						TalkListenerRelationTable[result] = value;
					}
				}
			}
		}

		private void LoadAgentGravurePoseTable(DefinePack definePack, bool awaitable)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.GravurePoseInfo);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!text.IsNullOrEmpty() && !fileNameWithoutExtension.IsNullOrEmpty())
				{
					AssetBundleInfo listAsset = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
					LoadGravurePoseList(listAsset, awaitable: false);
				}
			}
		}

		private void LoadGravurePoseList(AssetBundleInfo listAsset, bool awaitable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(listAsset);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num++;
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				string[] array = text.Split(_separationKeywords, StringSplitOptions.RemoveEmptyEntries);
				string value = list.GetElement(num++) ?? string.Empty;
				if (!bool.TryParse(value, out var result4))
				{
					result4 = true;
				}
				List<int> list2 = ListPool<int>.Get();
				if (!array.IsNullOrEmpty())
				{
					string[] array2 = array;
					foreach (string text2 in array2)
					{
						if (!text2.IsNullOrEmpty() && int.TryParse(text2, out var result5))
						{
							list2.Add(result5);
						}
					}
				}
				if (!list2.IsNullOrEmpty())
				{
					PoseKeyPair poseKeyPair = default(PoseKeyPair);
					poseKeyPair.postureID = result2;
					poseKeyPair.poseID = result3;
					PoseKeyPair i2 = poseKeyPair;
					foreach (int item in list2)
					{
						if (!AgentGravurePoseTable.TryGetValue(item, out var value2))
						{
							value2 = (AgentGravurePoseTable[item] = new Dictionary<int, UnityEx.ValueTuple<PoseKeyPair, bool>>());
						}
						value2[result] = new UnityEx.ValueTuple<PoseKeyPair, bool>(i2, result4);
					}
				}
				ListPool<int>.Release(list2);
			}
		}

		private void LoadWithAnimalStateList(List<string> row, int id, bool awaitable)
		{
			int num = 2;
			string assetbundleName = row.GetElement(num++) ?? string.Empty;
			string assetName = row.GetElement(num++) ?? string.Empty;
			string manifestName = row.GetElement(num++) ?? string.Empty;
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (!(excelData == null) && excelData.MaxCell != 0)
			{
				int num2 = 1;
				LoadWithAnimalAnimationList(excelData.list.GetElement(num2++)?.list, awaitable);
				LoadEventKeyTable(excelData.list.GetElement(num2++)?.list, AgentAnimalEventKeyTable, awaitable);
			}
		}

		private void LoadWithAnimalAnimationList(List<string> row, bool awaitable)
		{
			if (row.IsNullOrEmpty())
			{
				return;
			}
			int num = 1;
			string assetbundleName = row.GetElement(num++) ?? string.Empty;
			string assetName = row.GetElement(num++) ?? string.Empty;
			string manifestName = row.GetElement(num++) ?? string.Empty;
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData == null || excelData.MaxCell == 0)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list.GetElement(i)?.list;
				if (!list.IsNullOrEmpty())
				{
					num = 1;
					string assetbundleName2 = list.GetElement(num++) ?? string.Empty;
					string assetName2 = list.GetElement(num++) ?? string.Empty;
					string manifestName2 = list.GetElement(num++) ?? string.Empty;
					ExcelData withAnimalExcel = AssetUtility.LoadAsset<ExcelData>(assetbundleName2, assetName2, manifestName2);
					LoadWithAnimalAnimationState(withAnimalExcel, awaitable);
				}
			}
		}

		private void LoadWithAnimalAnimationState(ExcelData withAnimalExcel, bool awaitable)
		{
			if (withAnimalExcel == null || withAnimalExcel.MaxCell == 0)
			{
				return;
			}
			int num = 1;
			while (num < withAnimalExcel.MaxCell)
			{
				List<string> list = withAnimalExcel.list.GetElement(num++)?.list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 2;
				int result = 0;
				string s = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s, out result))
				{
					continue;
				}
				int result2 = 0;
				string s2 = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s2, out result2))
				{
					continue;
				}
				string assetbundle_ = list.GetElement(num2++) ?? string.Empty;
				string asset_ = list.GetElement(num2++) ?? string.Empty;
				string text = list.GetElement(num2++) ?? string.Empty;
				string[] inStateNames = text.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
				if (!bool.TryParse(list.GetElement(num2++) ?? string.Empty, out var result3))
				{
					result3 = false;
				}
				if (!float.TryParse(list.GetElement(num2++) ?? string.Empty, out var result4))
				{
					result4 = 0f;
				}
				string text2 = list.GetElement(num2++) ?? string.Empty;
				string[] outStateNames = text2.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
				if (!bool.TryParse(list.GetElement(num2++) ?? string.Empty, out var result5))
				{
					result5 = false;
				}
				if (!float.TryParse(list.GetElement(num2++) ?? string.Empty, out var result6))
				{
					result6 = 0f;
				}
				if (!int.TryParse(list.GetElement(num2++) ?? string.Empty, out var result7))
				{
					result7 = 0;
				}
				if (!int.TryParse(list.GetElement(num2++) ?? string.Empty, out var result8))
				{
					result8 = 0;
				}
				if (!int.TryParse(list.GetElement(num2++) ?? string.Empty, out var result9))
				{
					result9 = 0;
				}
				string input = list.GetElement(num2++) ?? string.Empty;
				List<UnityEx.ValueTuple<string, bool, int, bool>> list2 = ListPool<UnityEx.ValueTuple<string, bool, int, bool>>.Get();
				MatchCollection matchCollection = _regex.Matches(input);
				if (0 < matchCollection.Count)
				{
					for (int i = 0; i < matchCollection.Count; i++)
					{
						Match match = matchCollection[i];
						for (int j = 0; j < match.Groups[1].Captures.Count; j++)
						{
							string value = match.Groups[1].Captures[j].Value;
							string[] source = value.Split(_separationKeywords, StringSplitOptions.RemoveEmptyEntries);
							int num3 = 0;
							string i2 = source.GetElement(num3++) ?? string.Empty;
							if (!bool.TryParse(source.GetElement(num3++) ?? string.Empty, out var result10))
							{
								result10 = false;
							}
							if (!int.TryParse(source.GetElement(num3++) ?? string.Empty, out var result11))
							{
								result11 = -1;
							}
							if (!bool.TryParse(source.GetElement(num3++) ?? string.Empty, out var result12))
							{
								result12 = false;
							}
							list2.Add(new UnityEx.ValueTuple<string, bool, int, bool>(i2, result10, result11, result12));
						}
					}
				}
				PlayState playState = new PlayState(result9, inStateNames, outStateNames);
				playState.MainStateInfo.AssetBundleInfo = new AssetBundleInfo(string.Empty, assetbundle_, asset_, string.Empty);
				PlayState.PlayStateInfo mainStateInfo = playState.MainStateInfo;
				mainStateInfo.InStateInfo.EnableFade = result3;
				mainStateInfo.InStateInfo.FadeSecond = result4;
				mainStateInfo.OutStateInfo.EnableFade = result5;
				mainStateInfo.OutStateInfo.FadeSecond = result6;
				mainStateInfo.IsLoop = 0 < result7 || 0 < result8;
				mainStateInfo.LoopMin = result7;
				mainStateInfo.LoopMax = result8;
				playState.ActionInfo = new ActionInfo(hasAction_: false, 0);
				foreach (UnityEx.ValueTuple<string, bool, int, bool> item in list2)
				{
					playState.AddItemInfo(new PlayState.ItemInfo
					{
						parentName = item.Item1,
						fromEquipedItem = item.Item2,
						itemID = item.Item3,
						isSync = item.Item4
					});
				}
				ListPool<UnityEx.ValueTuple<string, bool, int, bool>>.Release(list2);
				if (!WithAnimalStateTable.ContainsKey(result))
				{
					WithAnimalStateTable[result] = new Dictionary<int, PlayState>();
				}
				WithAnimalStateTable[result][result2] = playState;
			}
		}

		private void LoadEventKeyTable(List<string> row, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> eventKeyTable, bool awaitable)
		{
			int num = 1;
			string element = row.GetElement(num++);
			string element2 = row.GetElement(num++);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(element, element2, string.Empty);
			for (int i = 2; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				int num2 = 2;
				string element3 = param.list.GetElement(num2++);
				string element4 = param.list.GetElement(num2++);
				if (!int.TryParse(element3, out var result) || !int.TryParse(element4, out var result2))
				{
					continue;
				}
				string element5 = param.list.GetElement(num2++);
				int key = Animator.StringToHash(element5);
				if (!eventKeyTable.TryGetValue(result, out var value))
				{
					Dictionary<int, Dictionary<int, List<AnimeEventInfo>>> dictionary2 = (eventKeyTable[result] = new Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>());
					value = dictionary2;
				}
				if (!value.TryGetValue(result2, out var value2))
				{
					Dictionary<int, List<AnimeEventInfo>> dictionary4 = (value[result2] = new Dictionary<int, List<AnimeEventInfo>>());
					value2 = dictionary4;
				}
				if (!value2.TryGetValue(key, out var value3))
				{
					List<AnimeEventInfo> list2 = (value2[key] = new List<AnimeEventInfo>());
					value3 = list2;
				}
				string element6 = param.list.GetElement(num2++);
				if (element6.IsNullOrEmpty())
				{
					Debug.LogWarning("エラー：イベントが空");
					continue;
				}
				MatchCollection matchCollection = _regex.Matches(element6);
				if (matchCollection.Count <= 0)
				{
					continue;
				}
				for (int j = 0; j < matchCollection.Count; j++)
				{
					Match match = matchCollection[j];
					for (int k = 0; k < match.Groups[1].Captures.Count; k++)
					{
						string value4 = match.Groups[1].Captures[k].Value;
						string[] source = value4.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
						int num3 = 0;
						if (!float.TryParse(source.GetElement(num3++), out var result3))
						{
							result3 = float.MaxValue;
						}
						if (!int.TryParse(source.GetElement(num3++), out var result4))
						{
							result4 = -1;
						}
						value3.Add(new AnimeEventInfo
						{
							normalizedTime = result3,
							eventID = result4
						});
					}
				}
			}
		}

		private void LoadSEEventKeyTable(string sheetBundleName, string sheetAssetName, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>> table, bool awaitable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundleName, sheetAssetName, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 2; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				if (list.GetElement(0) == "end")
				{
					break;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string element = list.GetElement(num++);
				if (element.IsNullOrEmpty())
				{
					continue;
				}
				int key = Animator.StringToHash(element);
				if (!table.TryGetValue(result, out var value))
				{
					value = (table[result] = new Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>());
				}
				if (!value.TryGetValue(result2, out var value2))
				{
					value2 = (value[result2] = new Dictionary<int, List<AnimeSEEventInfo>>());
				}
				if (!value2.TryGetValue(key, out var value3) || value3 == null)
				{
					value3 = (value2[key] = new List<AnimeSEEventInfo>());
				}
				else if (!value3.IsNullOrEmpty())
				{
					value3.Clear();
				}
				string element2 = list.GetElement(num++);
				if (element2.IsNullOrEmpty())
				{
					continue;
				}
				string[] array = element2.Split(_seEventSepa, StringSplitOptions.RemoveEmptyEntries);
				if (array.IsNullOrEmpty())
				{
					continue;
				}
				foreach (string text in array)
				{
					if (text.IsNullOrEmpty())
					{
						continue;
					}
					string text2 = text.Replace(string.Empty, _seEventRemoveStr);
					if (text2.IsNullOrEmpty())
					{
						continue;
					}
					string[] source = text2.Split(_separators, StringSplitOptions.None);
					int num2 = 0;
					if (!float.TryParse(source.GetElement(num2++) ?? string.Empty, out var result3))
					{
						continue;
					}
					if (result3 < 0f || 1f < result3)
					{
					}
					if (int.TryParse(source.GetElement(num2++) ?? string.Empty, out var result4))
					{
						if (!int.TryParse(source.GetElement(num2++) ?? string.Empty, out var result5))
						{
							result5 = -1;
						}
						string root = source.GetElement(num2++) ?? string.Empty;
						value3.Add(new AnimeSEEventInfo
						{
							NormalizedTime = result3,
							ClipID = result4,
							EventID = result5,
							Root = root
						});
					}
				}
			}
		}

		private void LoadFootStepEventKeyTable(string sheetAssetBundle, string sheetAsset, Dictionary<int, FootStepInfo[]> eventKeyTable, bool awaitable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetAssetBundle, sheetAsset, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 1;
				string name = list.GetElement(num++) ?? string.Empty;
				List<FootStepInfo> list2 = ListPool<FootStepInfo>.Get();
				while (num < list.Count)
				{
					string s = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s, out var result))
					{
						break;
					}
					string s2 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s2, out var result2))
					{
						break;
					}
					string text = list.GetElement(num++) ?? string.Empty;
					string[] array = text.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
					if (array.IsNullOrEmpty())
					{
						break;
					}
					List<float> list3 = ListPool<float>.Get();
					string[] array2 = array;
					foreach (string text2 in array2)
					{
						if (float.TryParse(text2 ?? string.Empty, out var result3))
						{
							list3.Add(result3);
						}
					}
					if (!list3.IsNullOrEmpty())
					{
						list2.Add(new FootStepInfo(result, result2, list3));
					}
					ListPool<float>.Release(list3);
				}
				if (!list2.IsNullOrEmpty())
				{
					int key = Animator.StringToHash(name);
					FootStepInfo[] array3 = new FootStepInfo[list2.Count];
					for (int k = 0; k < list2.Count; k++)
					{
						array3[k] = list2[k];
					}
					eventKeyTable[key] = array3;
				}
				ListPool<FootStepInfo>.Release(list2);
			}
		}

		private void LoadParticleEventKeyTable(string sheetAssetBundleName, string sheetAssetName, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>> eventKeyTable, bool awaitable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetAssetBundleName, sheetAssetName, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 2; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string element = list.GetElement(num++);
				if (element.IsNullOrEmpty())
				{
					continue;
				}
				int key = Animator.StringToHash(element);
				if (!eventKeyTable.TryGetValue(result, out var value))
				{
					value = (eventKeyTable[result] = new Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>());
				}
				if (!value.TryGetValue(result2, out var value2))
				{
					value2 = (value[result2] = new Dictionary<int, List<AnimeParticleEventInfo>>());
				}
				if (!value2.TryGetValue(key, out var value3) || value3 == null)
				{
					value3 = (value2[key] = new List<AnimeParticleEventInfo>());
				}
				else if (!value3.IsNullOrEmpty())
				{
					value3.Clear();
				}
				string element2 = list.GetElement(num++);
				if (element2.IsNullOrEmpty())
				{
					continue;
				}
				MatchCollection matchCollection = _regex.Matches(element2);
				if (0 >= matchCollection.Count)
				{
					continue;
				}
				for (int j = 0; j < matchCollection.Count; j++)
				{
					Match match = matchCollection[j];
					for (int k = 0; k < match.Groups[1].Captures.Count; k++)
					{
						string value4 = match.Groups[1].Captures[k].Value;
						string[] source = value4.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
						int num2 = 0;
						string s3 = source.GetElement(num2++) ?? string.Empty;
						if (!float.TryParse(s3, out var result3))
						{
							continue;
						}
						string s4 = source.GetElement(num2++) ?? string.Empty;
						if (int.TryParse(s4, out var result4))
						{
							string s5 = source.GetElement(num2++) ?? string.Empty;
							if (int.TryParse(s5, out var result5))
							{
								string root = source.GetElement(num2++) ?? string.Empty;
								value3.Add(new AnimeParticleEventInfo
								{
									NormalizedTime = result3,
									ParticleID = result4,
									EventID = result5,
									Root = root
								});
							}
						}
					}
				}
			}
		}

		private void LoadLoopVoiceEventKeyTable(string assetBundleName, string assetName, Dictionary<int, Dictionary<int, Dictionary<int, List<int>>>> table, bool awaitable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetBundleName, assetName, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string element = list.GetElement(num++);
				if (element.IsNullOrEmpty())
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				string[] array = text.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
				List<int> list2 = ListPool<int>.Get();
				if (!array.IsNullOrEmpty())
				{
					string[] array2 = array;
					foreach (string text2 in array2)
					{
						if (!text2.IsNullOrEmpty() && int.TryParse(text2, out var result3))
						{
							list2.Add(result3);
						}
					}
				}
				if (list2.IsNullOrEmpty())
				{
					ListPool<int>.Release(list2);
					continue;
				}
				int key = Animator.StringToHash(element);
				if (!table.TryGetValue(result, out var value))
				{
					value = (table[result] = new Dictionary<int, Dictionary<int, List<int>>>());
				}
				if (!value.TryGetValue(result2, out var value2))
				{
					value2 = (value[result2] = new Dictionary<int, List<int>>());
				}
				if (!value2.TryGetValue(key, out var value3) || value3 == null)
				{
					value3 = (value2[key] = new List<int>());
				}
				else if (!value3.IsNullOrEmpty())
				{
					value3.Clear();
				}
				value3.AddRange(list2);
				ListPool<int>.Release(list2);
			}
		}

		private void LoadEventKeyTable(string sheetAssetBundleName, string sheetAssetName, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>> eventKeyTable, bool awaitable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetAssetBundleName, sheetAssetName, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 2; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				int num = 2;
				string element = param.list.GetElement(num++);
				string element2 = param.list.GetElement(num++);
				if (!int.TryParse(element, out var result) || !int.TryParse(element2, out var result2))
				{
					continue;
				}
				string element3 = param.list.GetElement(num++);
				int key = Animator.StringToHash(element3);
				if (!eventKeyTable.TryGetValue(result, out var value))
				{
					Dictionary<int, Dictionary<int, List<AnimeEventInfo>>> dictionary2 = (eventKeyTable[result] = new Dictionary<int, Dictionary<int, List<AnimeEventInfo>>>());
					value = dictionary2;
				}
				if (!value.TryGetValue(result2, out var value2))
				{
					Dictionary<int, List<AnimeEventInfo>> dictionary4 = (value[result2] = new Dictionary<int, List<AnimeEventInfo>>());
					value2 = dictionary4;
				}
				if (!value2.TryGetValue(key, out var value3) || value3 == null)
				{
					List<AnimeEventInfo> list2 = (value2[key] = new List<AnimeEventInfo>());
					value3 = list2;
				}
				else if (!value3.IsNullOrEmpty())
				{
					value3.Clear();
				}
				string element4 = param.list.GetElement(num++);
				if (element4.IsNullOrEmpty())
				{
					Debug.LogWarning("エラー：イベントが空");
					continue;
				}
				MatchCollection matchCollection = _regex.Matches(element4);
				if (matchCollection.Count <= 0)
				{
					continue;
				}
				for (int j = 0; j < matchCollection.Count; j++)
				{
					Match match = matchCollection[j];
					for (int k = 0; k < match.Groups[1].Captures.Count; k++)
					{
						string value4 = match.Groups[1].Captures[k].Value;
						string[] source = value4.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
						int num2 = 0;
						if (!float.TryParse(source.GetElement(num2++), out var result3))
						{
							result3 = float.MaxValue;
						}
						if (!int.TryParse(source.GetElement(num2++), out var result4))
						{
							result4 = -1;
						}
						value3.Add(new AnimeEventInfo
						{
							normalizedTime = result3,
							eventID = result4
						});
					}
				}
			}
		}

		public void LoadOnceVoiceEventKeyTable(string sheetAssetBundleName, string sheetAssetName, Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>> eventKeyTable, bool awaitable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetAssetBundleName, sheetAssetName, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 2; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				if (text.IsNullOrEmpty())
				{
					continue;
				}
				string text2 = list.GetElement(num++) ?? string.Empty;
				if (text2.IsNullOrEmpty())
				{
					continue;
				}
				int key = Animator.StringToHash(text);
				if (!eventKeyTable.TryGetValue(result, out var value))
				{
					value = (eventKeyTable[result] = new Dictionary<int, Dictionary<int, List<AnimeOnceVoiceEventInfo>>>());
				}
				if (!value.TryGetValue(result2, out var value2))
				{
					value2 = (value[result2] = new Dictionary<int, List<AnimeOnceVoiceEventInfo>>());
				}
				if (!value2.TryGetValue(key, out var value3) || value3 == null)
				{
					value3 = (value2[key] = new List<AnimeOnceVoiceEventInfo>());
				}
				else if (!value3.IsNullOrEmpty())
				{
					value3.Clear();
				}
				MatchCollection matchCollection = _regex.Matches(text2);
				if (0 >= matchCollection.Count)
				{
					continue;
				}
				for (int j = 0; j < matchCollection.Count; j++)
				{
					Match match = matchCollection[j];
					for (int k = 0; k < match.Groups[1].Captures.Count; k++)
					{
						string value4 = match.Groups[1].Captures[k].Value;
						string[] array = value4.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
						int num2 = 0;
						string s3 = array.GetElement(num2++) ?? string.Empty;
						if (!float.TryParse(s3, out var result3))
						{
							result3 = float.MaxValue;
						}
						List<int> list3 = ListPool<int>.Get();
						while (num2 < array.Length)
						{
							string s4 = array.GetElement(num2++) ?? string.Empty;
							if (int.TryParse(s4, out var result4))
							{
								list3.Add(result4);
							}
						}
						AnimeOnceVoiceEventInfo animeOnceVoiceEventInfo = default(AnimeOnceVoiceEventInfo);
						animeOnceVoiceEventInfo.NormalizedTime = result3;
						animeOnceVoiceEventInfo.EventIDs = list3.ToArray();
						AnimeOnceVoiceEventInfo item = animeOnceVoiceEventInfo;
						ListPool<int>.Release(list3);
						value3.Add(item);
					}
				}
			}
		}

		private void LoadLoopVoiceEventKeyTable(string sheetAssetBundleName, string sheetAssetName, Dictionary<int, Dictionary<int, Dictionary<int, int>>> eventKeyTable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetAssetBundleName, sheetAssetName, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 2;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string element = list.GetElement(num++);
				if (element.IsNullOrEmpty())
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s3, out var result3))
				{
					int key = Animator.StringToHash(element);
					if (!eventKeyTable.TryGetValue(result, out var value))
					{
						value = (eventKeyTable[result] = new Dictionary<int, Dictionary<int, int>>());
					}
					if (!value.TryGetValue(result2, out var value2))
					{
						value2 = (value[result2] = new Dictionary<int, int>());
					}
					value2[key] = result3;
				}
			}
		}

		private void LoadAgentEventKeyTable(DefinePack definePack, bool awaitable)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentAnimeInfo);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!text.IsNullOrEmpty() && !fileNameWithoutExtension.IsNullOrEmpty())
				{
					string sheetAsset = $"footstepevent_{fileNameWithoutExtension}";
					LoadFootStepEventKeyTable(text, sheetAsset, AgentFootStepEventKeyTable, awaitable);
					string sheetAssetName = $"event_se_{fileNameWithoutExtension}";
					LoadSEEventKeyTable(text, sheetAssetName, AgentActSEEventKeyTable, awaitable);
					string sheetAssetName2 = $"event_particle_{fileNameWithoutExtension}";
					LoadParticleEventKeyTable(text, sheetAssetName2, AgentActParticleEventKeyTable, awaitable);
					string sheetAssetName3 = $"event_voice_{fileNameWithoutExtension}";
					LoadOnceVoiceEventKeyTable(text, sheetAssetName3, AgentActOnceVoiceEventKeyTable, awaitable);
					string assetName = $"event_voice_loop_{fileNameWithoutExtension}";
					LoadLoopVoiceEventKeyTable(text, assetName, AgentActLoopVoiceEventKeyTable, awaitable);
				}
			}
		}

		private void LoadPlayerEventKeyTable(DefinePack definePack, bool awaitable)
		{
			LoadPlayerEventKeyTable(definePack.ABDirectories.PlayerMaleAnimeInfo, 0, awaitable);
			LoadPlayerEventKeyTable(definePack.ABDirectories.PlayerFemaleAnimeInfo, 1, awaitable);
		}

		private void LoadPlayerEventKeyTable(string directory, int sex, bool awaitable)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(directory);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!text.IsNullOrEmpty() && !fileNameWithoutExtension.IsNullOrEmpty())
				{
					string sheetAsset = $"footstepevent_{fileNameWithoutExtension}";
					if (!PlayerFootStepEventKeyTable.TryGetValue(sex, out var value))
					{
						value = (PlayerFootStepEventKeyTable[sex] = new Dictionary<int, FootStepInfo[]>());
					}
					LoadFootStepEventKeyTable(text, sheetAsset, value, awaitable);
					string sheetAssetName = $"event_se_{fileNameWithoutExtension}";
					if (!PlayerActSEEventKeyTable.TryGetValue(sex, out var value2))
					{
						value2 = (PlayerActSEEventKeyTable[sex] = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeSEEventInfo>>>>());
					}
					LoadSEEventKeyTable(text, sheetAssetName, value2, awaitable);
					string sheetAssetName2 = $"event_particle_{fileNameWithoutExtension}";
					if (!PlayerActParticleEventKeyTable.TryGetValue(sex, out var value3))
					{
						value3 = (PlayerActParticleEventKeyTable[sex] = new Dictionary<int, Dictionary<int, Dictionary<int, List<AnimeParticleEventInfo>>>>());
					}
					LoadParticleEventKeyTable(text, sheetAssetName2, value3, awaitable);
				}
			}
		}

		private void LoadChangeClothEventKeyTable(DefinePack definePack, bool awaitable)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentAnimeInfo);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(item);
				if (!item.IsNullOrEmpty() && !fileNameWithoutExtension.IsNullOrEmpty())
				{
					string sheetAssetName = $"cloth_{fileNameWithoutExtension}";
					LoadEventKeyTable(item, sheetAssetName, AgentChangeClothEventKeyTable, awaitable: false);
				}
			}
		}

		private void LoadSurpriseItemList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.SurpriseItemInfo);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				SurpriseItemData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(SurpriseItemData)).GetAllAssets<SurpriseItemData>();
				foreach (SurpriseItemData surpriseItemData in allAssets)
				{
					foreach (SurpriseItemData.Param item2 in surpriseItemData.param)
					{
						if (!SurpriseItemList.TryGetValue(item2.Animator, out var value))
						{
							List<PlayState.ItemInfo> list = new List<PlayState.ItemInfo>();
							SurpriseItemList[item2.Animator] = list;
							value = list;
						}
						if (item2.ItemList.IsNullOrEmpty())
						{
							continue;
						}
						foreach (string item3 in item2.ItemList)
						{
							if (!item3.IsNullOrEmpty())
							{
								string[] source = item3.Split(_comma, StringSplitOptions.None);
								int num = 0;
								int result;
								int itemID = ((!int.TryParse(source.GetElement(num++), out result)) ? (-1) : result);
								string element = source.GetElement(num++);
								bool result2;
								bool isSync = bool.TryParse(source.GetElement(num++), out result2) && result2;
								value.Add(new PlayState.ItemInfo
								{
									itemID = itemID,
									parentName = element,
									isSync = isSync
								});
							}
						}
					}
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(item, string.Empty);
			}
		}

		private void LoadMerchantActionState(List<string> address, string id, Dictionary<int, Dictionary<int, PlayState>> dic, bool awaitable)
		{
			ExcelData excelData = null;
			int num = 2;
			string assetbundleName = address.GetElement(num++) ?? string.Empty;
			string assetName = address.GetElement(num++) ?? string.Empty;
			string manifestName = address.GetElement(num++) ?? string.Empty;
			excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!list.IsNullOrEmpty())
				{
					int num2 = 0;
					string element = list.GetElement(num2++);
					if (!element.IsNullOrEmpty())
					{
						string assetbundleName2 = list.GetElement(num2++) ?? string.Empty;
						string assetName2 = list.GetElement(num2++) ?? string.Empty;
						string manifestName2 = list.GetElement(num2++) ?? string.Empty;
						ExcelData actionExcel = AssetUtility.LoadAsset<ExcelData>(assetbundleName2, assetName2, manifestName2);
						LoadActionAnimationInfo(actionExcel, dic, awaitable);
					}
				}
			}
		}

		private void LoadMerchantOnlyActionAnimState(List<string> address, string id, bool awaitable)
		{
			LoadMerchantActionState(address, id, MerchantOnlyActionAnimStateTable, awaitable);
		}

		private void LoadMerchantCommonActionState(List<string> address, string id, bool awaitable)
		{
			LoadMerchantActionState(address, id, MerchantCommonActionAnimStateTable, awaitable);
		}

		private void LoadMerchantLocomotionStateList(List<string> address, string id, bool awaitable)
		{
			int num = 2;
			string assetbundleName = address.GetElement(num++) ?? string.Empty;
			string assetName = address.GetElement(num++) ?? string.Empty;
			string manifestName = address.GetElement(num++) ?? string.Empty;
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			LoadLocomotionStateList(excelData, MerchantLocomotionStateTable, awaitable);
		}

		private void LoadMerchantMoveInfoList(List<string> address, string id, bool awaitable)
		{
			int num = 2;
			string text = address.GetElement(num++) ?? string.Empty;
			string text2 = address.GetElement(num++) ?? string.Empty;
			if (!AssetBundleCheck.IsFile(text, text2))
			{
				return;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, text2, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 0;
				string element = list.GetElement(num2++);
				int key = Animator.StringToHash(element);
				if (!MerchantMoveInfoTable.TryGetValue(key, out var value))
				{
					List<AnimeMoveInfo> list2 = new List<AnimeMoveInfo>();
					MerchantMoveInfoTable[key] = list2;
					value = list2;
				}
				while (num2 < list.Count)
				{
					string element2 = list.GetElement(num2++);
					string element3 = list.GetElement(num2++);
					string element4 = list.GetElement(num2++);
					if (!element4.IsNullOrEmpty() && float.TryParse(element2, out var result) && float.TryParse(element3, out var result2))
					{
						AnimeMoveInfo animeMoveInfo = default(AnimeMoveInfo);
						animeMoveInfo.start = result;
						animeMoveInfo.end = result2;
						animeMoveInfo.movePoint = element4;
						AnimeMoveInfo item = animeMoveInfo;
						value.Add(item);
					}
				}
			}
		}

		private void LoadMerchantEventKeyList(List<string> address, string id, bool awaitable)
		{
			ExcelData excelData = null;
			int num = 2;
			string element = address.GetElement(num++);
			string element2 = address.GetElement(num++);
			if (!AssetBundleCheck.IsFile(element, element2))
			{
				return;
			}
			excelData = AssetUtility.LoadAsset<ExcelData>(element, element2, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				string s = list.GetElement(0) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				int num2 = 2;
				string element3 = list.GetElement(num2++);
				string element4 = list.GetElement(num2++);
				if (!element3.IsNullOrEmpty() && !element4.IsNullOrEmpty())
				{
					switch (result)
					{
					case 0:
						LoadEventKeyTable(element3, element4, MerchantOnlyItemEventKeyTable, awaitable);
						break;
					case 1:
						LoadEventKeyTable(element3, element4, MerchantCommonItemEventKeyTable, awaitable);
						break;
					case 2:
						LoadSEEventKeyTable(element3, element4, MerchantOnlySEEventKeyTable, awaitable);
						break;
					case 3:
						LoadSEEventKeyTable(element3, element4, MerchantCommonSEEventKeyTable, awaitable);
						break;
					case 4:
						LoadParticleEventKeyTable(element3, element4, MerchantOnlyParticleEventKeyTable, awaitable);
						break;
					case 5:
						LoadParticleEventKeyTable(element3, element4, MerchantCommonParticleEventKeyTable, awaitable);
						break;
					case 6:
						LoadOnceVoiceEventKeyTable(element3, element4, MerchantOnlyOnceVoiceEventKeyTable, awaitable);
						break;
					case 7:
						LoadOnceVoiceEventKeyTable(element3, element4, MerchantCommonOnceVoiceEventKeyTable, awaitable);
						break;
					case 8:
						LoadLoopVoiceEventKeyTable(element3, element4, MerchantOnlyLoopVoiceEventKeyTable, awaitable);
						break;
					case 9:
						LoadLoopVoiceEventKeyTable(element3, element4, MerchantCommonLoopVoiceEventKeyTable, awaitable);
						break;
					case 10:
						LoadFootStepEventKeyTable(element3, element4, MerchantFootStepEventKeyTable, awaitable);
						break;
					}
				}
			}
		}

		private void LoadMerchantListenerStateList(List<string> address, string id, bool awaitable)
		{
			ExcelData excelData = null;
			int num = 2;
			string assetbundleName = address.GetElement(num++) ?? string.Empty;
			string assetName = address.GetElement(num++) ?? string.Empty;
			string manifestName = address.GetElement(num++) ?? string.Empty;
			excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 0;
				string s = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num2++;
				string s2 = list.GetElement(num2++) ?? string.Empty;
				if (int.TryParse(s2, out var result2))
				{
					string s3 = list.GetElement(num2++) ?? string.Empty;
					if (int.TryParse(s3, out var result3))
					{
						string s4 = list.GetElement(num2++) ?? string.Empty;
						int result4;
						int value = (int.TryParse(s4, out result4) ? result4 : 0);
						MerchantListenerRelationTable[result] = value;
						PoseKeyPair poseKeyPair = default(PoseKeyPair);
						poseKeyPair.postureID = result2;
						poseKeyPair.poseID = result3;
						PoseKeyPair value2 = poseKeyPair;
						MerchantListenerStateTable[result] = value2;
					}
				}
			}
		}

		private void LoadItemScaleTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentAnimeInfo);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string assetName = $"itemscale_{System.IO.Path.GetFileNameWithoutExtension(text)}";
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, assetName, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 1;
					string element = param.list.GetElement(num2++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					string element2 = param.list.GetElement(num2++);
					if (int.TryParse(element2, out var result2))
					{
						string element3 = param.list.GetElement(num2++);
						string element4 = param.list.GetElement(num2++);
						string element5 = param.list.GetElement(num2++);
						if (float.TryParse(element3, out var result3) && float.TryParse(element4, out var result4) && float.TryParse(element5, out var result5))
						{
							TriValues triValues = new TriValues();
							triValues.ScaleType = result2;
							triValues.SThreshold = result3;
							triValues.MThreshold = result4;
							triValues.LThreshold = result5;
							TriValues value = triValues;
							ItemScaleTable[result] = value;
						}
					}
				}
			}
		}

		private void LoadLocomotionStateList(ExcelData excelData, Dictionary<int, PlayState> dic, bool awaitable)
		{
			if (excelData == null)
			{
				return;
			}
			int num = 1;
			while (num < excelData.MaxCell)
			{
				ExcelData.Param param = excelData.list[num++];
				if (param.list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 0;
				string element = param.list.GetElement(num2++);
				if (!int.TryParse(element, out var result))
				{
					Debug.LogWarning($"セルの読み込み失敗: {excelData.name} {num}行目 Type = {element}");
					continue;
				}
				num2++;
				string element2 = param.list.GetElement(num2++);
				string element3 = param.list.GetElement(num2++);
				string element4 = param.list.GetElement(num2++);
				string[] array = element4.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
				bool result2;
				bool enableFade = bool.TryParse(param.list.GetElement(num2++), out result2) && result2;
				float result3;
				float fadeSecond = ((!float.TryParse(param.list.GetElement(num2++), out result3)) ? 0f : result3);
				string element5 = param.list.GetElement(num2++);
				string[] array2 = element5.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
				bool result4;
				bool enableFade2 = bool.TryParse(param.list.GetElement(num2++), out result4) && result4;
				float result5;
				float fadeSecond2 = ((!float.TryParse(param.list.GetElement(num2++), out result5)) ? 0f : result5);
				int result6;
				int num3 = (int.TryParse(param.list.GetElement(num2++), out result6) ? result6 : 0);
				string element6 = param.list.GetElement(num2++);
				int layer_ = (int.TryParse(param.list.GetElement(num2++), out result6) ? result6 : 0);
				PlayState playState = new PlayState();
				playState.Layer = num3;
				playState = (dic[result] = playState);
				PlayState playState3 = playState;
				playState3.MainStateInfo.AssetBundleInfo = new AssetBundleInfo(string.Empty, element2, element3, string.Empty);
				playState3.MainStateInfo.InStateInfo = new PlayState.AnimStateInfo();
				if (!array.IsNullOrEmpty())
				{
					PlayState.Info[] array3 = new PlayState.Info[array.Length];
					playState3.MainStateInfo.InStateInfo.StateInfos = array3;
					PlayState.Info[] array4 = array3;
					for (int i = 0; i < array4.Length; i++)
					{
						ref PlayState.Info reference = ref array4[i];
						reference = new PlayState.Info(array[i], num3);
					}
				}
				playState3.MainStateInfo.OutStateInfo = new PlayState.AnimStateInfo();
				if (!array2.IsNullOrEmpty())
				{
					PlayState.Info[] array3 = new PlayState.Info[array2.Length];
					playState3.MainStateInfo.OutStateInfo.StateInfos = array3;
					PlayState.Info[] array5 = array3;
					for (int j = 0; j < array5.Length; j++)
					{
						ref PlayState.Info reference2 = ref array5[j];
						reference2 = new PlayState.Info(array2[j], num3);
					}
				}
				playState3.MainStateInfo.InStateInfo.EnableFade = enableFade;
				playState3.MainStateInfo.InStateInfo.FadeSecond = fadeSecond;
				playState3.MainStateInfo.OutStateInfo.EnableFade = enableFade2;
				playState3.MainStateInfo.OutStateInfo.FadeSecond = fadeSecond2;
				playState3.MaskStateInfo = new PlayState.Info(element6, layer_);
			}
		}
	}

	public class TriValues
	{
		public int ScaleType { get; set; }

		public float SThreshold { get; set; }

		public float MThreshold { get; set; }

		public float LThreshold { get; set; }
	}

	public class BehaviorTreeTables
	{
		private Dictionary<Desire.ActionType, AgentBehaviorTree> _behaviorTree = new Dictionary<Desire.ActionType, AgentBehaviorTree>();

		private Dictionary<Merchant.ActionType, MerchantBehaviorTree> _merchantBehaviorTree = new Dictionary<Merchant.ActionType, MerchantBehaviorTree>();

		private Dictionary<AIProject.Definitions.Tutorial.ActionType, AgentBehaviorTree> _tutrialBehaviorTree = new Dictionary<AIProject.Definitions.Tutorial.ActionType, AgentBehaviorTree>();

		public AgentBehaviorTree GetBehavior(Desire.ActionType actionType)
		{
			if (!_behaviorTree.TryGetValue(actionType, out var value))
			{
				return null;
			}
			return value;
		}

		public MerchantBehaviorTree GetMerchantBehavior(Merchant.ActionType actionType)
		{
			if (!_merchantBehaviorTree.TryGetValue(actionType, out var value))
			{
				return null;
			}
			return value;
		}

		public AgentBehaviorTree GetTutorialBehavior(AIProject.Definitions.Tutorial.ActionType actionType)
		{
			_tutrialBehaviorTree.TryGetValue(actionType, out var value);
			return value;
		}

		public void Load(DefinePack definePack)
		{
			LoadAgentBehaviorTree(definePack);
			LoadMerchantBehaviorTree(definePack);
			LoadTutorialBehaviorTree(definePack);
		}

		public void Release()
		{
			_behaviorTree.Clear();
			_merchantBehaviorTree.Clear();
			_tutrialBehaviorTree.Clear();
		}

		private void LoadAgentBehaviorTree(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.BehaviorTree);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					ExcelData.Param element = excelData.list.GetElement(j);
					if (element == null || element.list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					if (Enum.TryParse<Desire.ActionType>(element.list[num++], out var result))
					{
						num++;
						string text2 = element.list[num++];
						string text3 = element.list[num++];
						GameObject gameObject = AssetUtility.LoadAsset<GameObject>(text2, text3, string.Empty);
						if (gameObject == null)
						{
							Debug.Log($"見つからない [ モード: {result} アセットバンドル: {text2} アセット: {text3} ]");
						}
						else
						{
							_behaviorTree[result] = gameObject.GetComponent<AgentBehaviorTree>();
						}
					}
				}
			}
		}

		private void LoadMerchantBehaviorTree(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.MerchantBehaviorTree);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (!list.IsNullOrEmpty() && Enum.TryParse<Merchant.ActionType>(list.GetElement(0) ?? string.Empty, out var result))
					{
						int num = 2;
						string assetbundleName = list.GetElement(num++) ?? string.Empty;
						string assetName = list.GetElement(num++) ?? string.Empty;
						string manifestName = list.GetElement(num++) ?? string.Empty;
						GameObject gameObject = AssetUtility.LoadAsset<GameObject>(assetbundleName, assetName, manifestName);
						MerchantBehaviorTree component = gameObject.GetComponent<MerchantBehaviorTree>();
						if (!(component == null))
						{
							_merchantBehaviorTree[result] = component;
						}
					}
				}
			}
		}

		private void LoadTutorialBehaviorTree(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.TutorialBehaviorTree);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string value = list.GetElement(num++) ?? string.Empty;
					if (Enum.TryParse<AIProject.Definitions.Tutorial.ActionType>(value, out var result))
					{
						num++;
						string assetbundleName = list.GetElement(num++) ?? string.Empty;
						string assetName = list.GetElement(num++) ?? string.Empty;
						AgentBehaviorTree agentBehaviorTree = AssetUtility.LoadAsset<GameObject>(assetbundleName, assetName, string.Empty)?.GetComponent<AgentBehaviorTree>();
						if (!(agentBehaviorTree == null))
						{
							_tutrialBehaviorTree[result] = agentBehaviorTree;
						}
					}
				}
			}
		}
	}

	public class FishingTable
	{
		private int mSizeFishCount;

		private string[] separators = new string[3] { "/", "／", "," };

		public Dictionary<int, RuntimeAnimatorController> PlayerAnimatorTable { get; private set; } = new Dictionary<int, RuntimeAnimatorController>();


		public Dictionary<int, PlayState> PlayerAnimStateTable { get; private set; } = new Dictionary<int, PlayState>();


		public Dictionary<string, List<Fishing.Schedule>> AnimEventScheduler { get; private set; } = new Dictionary<string, List<Fishing.Schedule>>();


		public Dictionary<int, FishingRodInfo> RodInfos { get; private set; } = new Dictionary<int, FishingRodInfo>();


		public Dictionary<int, System.Tuple<AssetBundleInfo, RuntimeAnimatorController, string>> FishBodyTable { get; private set; } = new Dictionary<int, System.Tuple<AssetBundleInfo, RuntimeAnimatorController, string>>();


		public Dictionary<int, AssetBundleInfo> EffectTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, FishFoodInfo> FishFoodInfoTable { get; private set; } = new Dictionary<int, FishFoodInfo>();


		public Dictionary<int, Dictionary<int, float>> FishHitBaseRangeTable { get; private set; } = new Dictionary<int, Dictionary<int, float>>();


		public Dictionary<int, FishBaitHitInfo> FishBaitHitInfoTable { get; private set; } = new Dictionary<int, FishBaitHitInfo>();


		public Dictionary<int, Dictionary<int, FishInfo>> FishInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, FishInfo>>();


		public Dictionary<int, Dictionary<int, FishInfo>> FishInfoSizeGroupTable { get; private set; } = new Dictionary<int, Dictionary<int, FishInfo>>();


		public Dictionary<int, System.Tuple<GameObject, RuntimeAnimatorController>> FishModelTable { get; private set; } = new Dictionary<int, System.Tuple<GameObject, RuntimeAnimatorController>>();


		public Dictionary<int, Dictionary<int, int>> FishSizeTable { get; private set; } = new Dictionary<int, Dictionary<int, int>>();


		public float ResultFishReferenceExtent { get; private set; } = 1f;


		public UnityEx.ValueTuple<int, int>[] GetItemIDInSizeTalbe(int _sizeID)
		{
			List<UnityEx.ValueTuple<int, int>> list = ListPool<UnityEx.ValueTuple<int, int>>.Get();
			foreach (KeyValuePair<int, Dictionary<int, int>> item in FishSizeTable)
			{
				foreach (KeyValuePair<int, int> item2 in item.Value)
				{
					if (item2.Value == _sizeID)
					{
						list.Add(new UnityEx.ValueTuple<int, int>(item.Key, item2.Key));
					}
				}
			}
			UnityEx.ValueTuple<int, int>[] array = new UnityEx.ValueTuple<int, int>[list.Count];
			for (int i = 0; i < list.Count; i++)
			{
				ref UnityEx.ValueTuple<int, int> reference = ref array[i];
				reference = list[i];
			}
			ListPool<UnityEx.ValueTuple<int, int>>.Release(list);
			return array;
		}

		public bool TryGetFishSize(int _categoryID, int _itemID, out int _sizeID)
		{
			if (FishSizeTable.TryGetValue(_categoryID, out var value) && value.TryGetValue(_itemID, out var value2))
			{
				_sizeID = value2;
				return true;
			}
			_sizeID = 0;
			return false;
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}]";
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info, string _ver)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}] Ver[{_ver}]";
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info, string _ver, int _row, int _clm)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}] Ver[{_ver}] Row[{_row}] Clm[{_clm}]";
		}

		private AssetBundleInfo GetAssetInfo(List<string> _address, ref int _idx, bool _addSummary)
		{
			string name_ = ((!_addSummary) ? string.Empty : (_address.GetElement(_idx++) ?? string.Empty));
			return new AssetBundleInfo(name_, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty);
		}

		private void LoadExcelData(List<string> _address, ref int _idx, out AssetBundleInfo _info, out ExcelData _data, bool _addSummary)
		{
			string name_ = ((!_addSummary) ? string.Empty : (_address.GetElement(_idx++) ?? string.Empty));
			_info = new AssetBundleInfo(name_, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty);
			_data = AssetUtility.LoadAsset<ExcelData>(_info);
		}

		public void Load(FishingDefinePack _fishingDefinePack)
		{
			mSizeFishCount = 0;
			ResultFishReferenceExtent = 1f;
			string fishingInfoListBundleDirectory = _fishingDefinePack.AssetBundleNames.FishingInfoListBundleDirectory;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(fishingInfoListBundleDirectory);
			if (AIProject.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
						switch (result)
						{
						case 0:
							LoadFishingData(assetInfo, fileNameWithoutExtension);
							break;
						case 1:
							LoadFishingAnimInfo(assetInfo, fileNameWithoutExtension);
							break;
						case 2:
							LoadFishingInfoList(assetInfo, fileNameWithoutExtension);
							break;
						}
					}
				}
			}
			if (0 < mSizeFishCount)
			{
				if (!FishInfoSizeGroupTable.TryGetValue(1, out var value) || value.IsNullOrEmpty())
				{
					return;
				}
				List<int> list2 = ListPool<int>.Get();
				foreach (KeyValuePair<int, FishInfo> item in value)
				{
					if (!list2.Contains(item.Value.ModelID))
					{
						list2.Add(item.Value.ModelID);
					}
				}
				float num = 1f;
				int num2 = 0;
				foreach (int item2 in list2)
				{
					if (FishModelTable.TryGetValue(item2, out var value2) && value2?.Item1 != null)
					{
						Renderer componentInChildren = value2.Item1.GetComponentInChildren<Renderer>(includeInactive: true);
						if (componentInChildren != null)
						{
							num += Mathf.Max(componentInChildren.bounds.extents.y, componentInChildren.bounds.extents.z);
							num2++;
						}
					}
				}
				if (0 < num2)
				{
					ResultFishReferenceExtent = num / (float)list2.Count;
				}
				ListPool<int>.Release(list2);
			}
			else
			{
				if (FishModelTable.IsNullOrEmpty())
				{
					return;
				}
				float num3 = 1f;
				int num4 = 0;
				foreach (KeyValuePair<int, System.Tuple<GameObject, RuntimeAnimatorController>> item3 in FishModelTable)
				{
					GameObject gameObject = item3.Value?.Item1;
					Renderer renderer = ((!(gameObject != null)) ? null : gameObject.GetComponentInChildren<Renderer>(includeInactive: true));
					if (renderer != null)
					{
						num3 += Mathf.Max(renderer.bounds.extents.y, renderer.bounds.extents.z);
						num4++;
					}
				}
				if (0 < num4)
				{
					ResultFishReferenceExtent = num3 / (float)num4;
				}
			}
		}

		public void Release()
		{
			PlayerAnimatorTable.Clear();
			PlayerAnimStateTable.Clear();
			AnimEventScheduler.Clear();
			RodInfos.Clear();
			FishBodyTable.Clear();
			EffectTable.Clear();
			FishFoodInfoTable.Clear();
			FishHitBaseRangeTable.Clear();
			FishBaitHitInfoTable.Clear();
			FishInfoTable.Clear();
			FishInfoSizeGroupTable.Clear();
			FishModelTable.Clear();
			FishSizeTable.Clear();
		}

		private void LoadFishingAnimInfo(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					switch (result)
					{
					case 0:
						LoadPlayerFishingAnimator(assetInfo, _ver);
						break;
					case 1:
						LoadFishingAnimState(assetInfo, _ver);
						break;
					case 2:
						LoadFishingAnimEvent(assetInfo, _ver);
						break;
					}
				}
			}
		}

		private void LoadPlayerFishingAnimator(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(assetInfo);
					if (!(runtimeAnimatorController == null))
					{
						PlayerAnimatorTable[result] = runtimeAnimatorController;
					}
				}
			}
		}

		private void LoadFishingAnimState(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			int index = 0;
			int index2 = 3;
			int index3 = 4;
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					string s = list.GetElement(index) ?? string.Empty;
					string text = list.GetElement(index2) ?? string.Empty;
					string[] array = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
					string s2 = list.GetElement(index3) ?? string.Empty;
					int result = 0;
					int result2 = 0;
					if (!AIProject.CollectionExtensions.IsNullOrEmpty(array) && int.TryParse(s, out result) && int.TryParse(s2, out result2))
					{
						PlayState value = new PlayState(result2, array, null);
						PlayerAnimStateTable[result] = value;
					}
				}
			}
		}

		private void LoadFishingAnimEvent(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			int index = 0;
			int index2 = 1;
			int index3 = 2;
			int index4 = 3;
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				string element = list.GetElement(index);
				string element2 = list.GetElement(index2);
				string element3 = list.GetElement(index3);
				string element4 = list.GetElement(index4);
				int result = 0;
				float result2 = 0f;
				if (!element.IsNullOrEmpty() && int.TryParse(element2, out result) && float.TryParse(element3, out result2))
				{
					Fishing.Schedule item = new Fishing.Schedule(element, result, result2, element4);
					List<Fishing.Schedule> value = null;
					if (!AnimEventScheduler.TryGetValue(element, out value))
					{
						value = (AnimEventScheduler[element] = new List<Fishing.Schedule>());
					}
					value.Add(item);
					AnimEventScheduler[element] = value;
				}
			}
		}

		private void LoadFishingData(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					switch (result)
					{
					case 0:
						LoadFishingParticle(assetInfo, _ver);
						break;
					case 1:
						LoadFishShadow(assetInfo, _ver);
						break;
					case 2:
						LoadFishingRod(assetInfo, _ver);
						break;
					case 3:
						LoadFishModel(assetInfo, _ver);
						break;
					}
				}
			}
		}

		private void LoadFishingParticle(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
				if (!assetInfo.assetbundle.IsNullOrEmpty() && !assetInfo.asset.IsNullOrEmpty())
				{
					string value = list.GetElement(_idx++) ?? string.Empty;
					if (bool.TryParse(value, out var _))
					{
						EffectTable[result] = assetInfo;
					}
				}
			}
		}

		private void LoadFishShadow(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
				if (assetInfo.assetbundle.IsNullOrEmpty() || assetInfo.asset.IsNullOrEmpty())
				{
					continue;
				}
				AssetBundleInfo assetInfo2 = GetAssetInfo(list, ref _idx, _addSummary: false);
				if (!assetInfo2.assetbundle.IsNullOrEmpty() && !assetInfo2.asset.IsNullOrEmpty())
				{
					RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(assetInfo2);
					if (!(runtimeAnimatorController == null))
					{
						string item = list.GetElement(_idx++) ?? string.Empty;
						FishBodyTable[result] = new System.Tuple<AssetBundleInfo, RuntimeAnimatorController, string>(assetInfo, runtimeAnimatorController, item);
					}
				}
			}
		}

		private void LoadFishingRod(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
				GameObject gameObject = CommonLib.LoadAsset<GameObject>(assetInfo.assetbundle, assetInfo.asset, clone: false, assetInfo.manifest);
				if (gameObject == null)
				{
					continue;
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(assetInfo.assetbundle, assetInfo.manifest);
				AssetBundleInfo assetInfo2 = GetAssetInfo(list, ref _idx, _addSummary: false);
				RuntimeAnimatorController runtimeAnimatorController = AssetUtility.LoadAsset<RuntimeAnimatorController>(assetInfo2);
				if (!(runtimeAnimatorController == null))
				{
					string text = list.GetElement(_idx++) ?? string.Empty;
					if (!text.IsNullOrEmpty())
					{
						RodInfos[result] = new FishingRodInfo(gameObject, runtimeAnimatorController, text);
					}
				}
			}
		}

		private void LoadFishModel(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
				GameObject gameObject = CommonLib.LoadAsset<GameObject>(assetInfo.assetbundle, assetInfo.asset, clone: false, assetInfo.manifest);
				if (!(gameObject == null))
				{
					Singleton<Resources>.Instance.AddLoadAssetBundle(assetInfo.assetbundle, assetInfo.manifest);
					AssetBundleInfo assetInfo2 = GetAssetInfo(list, ref _idx, _addSummary: false);
					RuntimeAnimatorController item = null;
					if (!assetInfo2.assetbundle.IsNullOrEmpty() && !assetInfo2.asset.IsNullOrEmpty())
					{
						item = AssetUtility.LoadAsset<RuntimeAnimatorController>(assetInfo2);
					}
					FishModelTable[result] = new System.Tuple<GameObject, RuntimeAnimatorController>(gameObject, item);
				}
			}
		}

		private void LoadFishingInfoList(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					switch (result)
					{
					case 0:
						LoadFishFoodInfo(assetInfo, _ver);
						break;
					case 1:
						LoadFishInfo(assetInfo, _ver);
						break;
					case 2:
						LoadFishHitBaseRange(assetInfo, _ver);
						break;
					case 3:
						LoadFishBaitHitInfoList(assetInfo);
						break;
					}
				}
			}
		}

		private void LoadFishFoodInfo(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					string text = list.GetElement(num++) ?? string.Empty;
					if (!text.IsNullOrEmpty())
					{
						string s2 = list.GetElement(num++) ?? string.Empty;
						string s3 = list.GetElement(num++) ?? string.Empty;
						string s4 = list.GetElement(num++) ?? string.Empty;
						int result2 = (int.TryParse(s2, out result2) ? result2 : 0);
						int result3 = (int.TryParse(s3, out result3) ? result3 : 0);
						int result4 = (int.TryParse(s4, out result4) ? result4 : 0);
						FishFoodInfoTable[result] = new FishFoodInfo(result, text, result2, result3, result4);
					}
				}
			}
		}

		private void LoadFishInfo(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string itemName = list.GetElement(num++) ?? string.Empty;
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string s4 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s4, out var result4))
				{
					continue;
				}
				string s5 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s5, out var result5))
				{
					continue;
				}
				string s6 = list.GetElement(num++) ?? string.Empty;
				string s7 = list.GetElement(num++) ?? string.Empty;
				int result6;
				bool flag = int.TryParse(s6, out result6);
				int result7;
				bool flag2 = int.TryParse(s7, out result7);
				if (flag || flag2)
				{
					if (!flag)
					{
						result6 = result7;
					}
					else if (!flag2)
					{
						result7 = result6;
					}
					else
					{
						int num2 = Mathf.Min(result6, result7);
						int num3 = Mathf.Max(result6, result7);
						result6 = num2;
						result7 = num3;
					}
					string s8 = list.GetElement(num++) ?? string.Empty;
					if (!int.TryParse(s8, out var result8))
					{
						result8 = 0;
					}
					string s9 = list.GetElement(num++) ?? string.Empty;
					if (!float.TryParse(s9, out var result9))
					{
						result9 = 0f;
					}
					FishInfo value = new FishInfo(result, result2, itemName, result3, result4, result5, result6, result7, result8, result9);
					if (!FishInfoSizeGroupTable.TryGetValue(result3, out var value2) || value2 == null)
					{
						Dictionary<int, FishInfo> dictionary = new Dictionary<int, FishInfo>();
						FishInfoSizeGroupTable[result3] = dictionary;
						value2 = dictionary;
					}
					value2[result2] = value;
					if (!FishInfoTable.TryGetValue(result, out var value3) || value3 == null)
					{
						value3 = (FishInfoTable[result] = new Dictionary<int, FishInfo>());
					}
					value3[result2] = value;
					if (result3 == 1)
					{
						mSizeFishCount++;
					}
					if (!FishSizeTable.TryGetValue(result, out var value4))
					{
						Dictionary<int, int> dictionary3 = new Dictionary<int, int>();
						FishSizeTable[result] = dictionary3;
						value4 = dictionary3;
					}
					value4[result2] = result3;
				}
			}
		}

		private void LoadFishHitBaseRange(AssetBundleInfo _info, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s3, out var result3))
				{
					if (!FishHitBaseRangeTable.TryGetValue(result, out var value))
					{
						Dictionary<int, float> dictionary = new Dictionary<int, float>();
						FishHitBaseRangeTable[result] = dictionary;
						value = dictionary;
					}
					value[result2] = result3;
				}
			}
		}

		private void LoadFishBaitHitInfoList(AssetBundleInfo _sheetInfo)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					num++;
					string element = list.GetElement(num++);
					string element2 = list.GetElement(num++);
					string element3 = list.GetElement(num++);
					if (!element.IsNullOrEmpty() && !element2.IsNullOrEmpty())
					{
						AssetBundleInfo sheetInfo = new AssetBundleInfo(string.Empty, element, element2, element3);
						LoadBaitHitInfo(sheetInfo, result);
					}
				}
			}
		}

		private void LoadBaitHitInfo(AssetBundleInfo _sheetInfo, int _baitID)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			if (!FishBaitHitInfoTable.TryGetValue(_baitID, out var value) || value == null)
			{
				value = (FishBaitHitInfoTable[_baitID] = new FishBaitHitInfo());
			}
			else
			{
				value.HitList.Clear();
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				List<UnityEx.ValueTuple<int, int, int>> list2 = ListPool<UnityEx.ValueTuple<int, int, int>>.Get();
				while (num < list.Count)
				{
					string s2 = list.GetElement(num++) ?? string.Empty;
					num++;
					string s3 = list.GetElement(num++) ?? string.Empty;
					string s4 = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s2, out var result2) && int.TryParse(s3, out var result3) && int.TryParse(s4, out var result4))
					{
						list2.Add(new UnityEx.ValueTuple<int, int, int>(result2, result3, result4));
					}
				}
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list2))
				{
					FishBaitHitInfo.HitInfo item = default(FishBaitHitInfo.HitInfo);
					item.Range = result;
					item.FishList = new List<UnityEx.ValueTuple<int, int, int>>(list2);
					value.HitList.Add(item);
				}
				ListPool<UnityEx.ValueTuple<int, int, int>>.Release(list2);
			}
		}

		public bool TryGetBaitHitInfo(int baitID, out UnityEx.ValueTuple<int, int, int> hitInfo)
		{
			if (!FishBaitHitInfoTable.TryGetValue(baitID, out var value) || value == null || AIProject.CollectionExtensions.IsNullOrEmpty(value.HitList))
			{
				hitInfo = new UnityEx.ValueTuple<int, int, int>(0, -1, -1);
				return false;
			}
			return value.GetFishInfo(out hitInfo);
		}
	}

	public class FishBaitHitInfo
	{
		public struct HitInfo
		{
			public int Range { get; set; }

			public List<UnityEx.ValueTuple<int, int, int>> FishList { get; set; }
		}

		public List<HitInfo> HitList { get; private set; } = new List<HitInfo>();


		public bool GetFishInfo(out UnityEx.ValueTuple<int, int, int> _info)
		{
			if (AIProject.CollectionExtensions.IsNullOrEmpty(HitList))
			{
				_info = new UnityEx.ValueTuple<int, int, int>(0, -1, -1);
				return false;
			}
			int num = 0;
			foreach (HitInfo hit in HitList)
			{
				num += hit.Range;
			}
			int num2 = UnityEngine.Random.Range(0, num);
			foreach (HitInfo hit2 in HitList)
			{
				if (num2 < hit2.Range)
				{
					if (AIProject.CollectionExtensions.IsNullOrEmpty(hit2.FishList))
					{
						_info = new UnityEx.ValueTuple<int, int, int>(0, -1, -1);
						return false;
					}
					int num3 = 0;
					foreach (UnityEx.ValueTuple<int, int, int> fish in hit2.FishList)
					{
						num3 += fish.Item1;
					}
					int num4 = UnityEngine.Random.Range(0, num3);
					foreach (UnityEx.ValueTuple<int, int, int> fish2 in hit2.FishList)
					{
						if (num4 < fish2.Item1)
						{
							_info = fish2;
							return true;
						}
						num4 -= fish2.Item1;
					}
				}
				else
				{
					num2 -= hit2.Range;
				}
			}
			_info = new UnityEx.ValueTuple<int, int, int>(0, -1, -1);
			return false;
		}
	}

	public class GameInfoTables
	{
		public class RecipeInfo
		{
			private const string LOAD_PATH = "list/actor/gameitem/recipe/";

			public bool initialized { get; private set; }

			public IReadOnlyDictionary<int, RecipeDataInfo[]> this[int i]
			{
				get
				{
					switch (i)
					{
					case 0:
						return materialTable;
					case 1:
						return equipmentTable;
					case 2:
						return cookTable;
					case 3:
						return petTable;
					case 4:
						return medicineTable;
					default:
						Debug.LogError("RecipeDataInfoTable Not Range:" + i);
						return materialTable;
					}
				}
			}

			public IReadOnlyDictionary<int, RecipeDataInfo[]> materialTable { get; private set; }

			public IReadOnlyDictionary<int, RecipeDataInfo[]> equipmentTable { get; private set; }

			public IReadOnlyDictionary<int, RecipeDataInfo[]> cookTable { get; private set; }

			public IReadOnlyDictionary<int, RecipeDataInfo[]> petTable { get; private set; }

			public IReadOnlyDictionary<int, RecipeDataInfo[]> medicineTable { get; private set; }

			public void Load()
			{
				Load("material", delegate(Dictionary<int, RecipeDataInfo[]> data)
				{
					materialTable = data;
				});
				Load("equipment", delegate(Dictionary<int, RecipeDataInfo[]> data)
				{
					equipmentTable = data;
				});
				Load("cook", delegate(Dictionary<int, RecipeDataInfo[]> data)
				{
					cookTable = data;
				});
				Load("pet", delegate(Dictionary<int, RecipeDataInfo[]> data)
				{
					petTable = data;
				});
				Load("medicine", delegate(Dictionary<int, RecipeDataInfo[]> data)
				{
					medicineTable = data;
				});
				initialized = true;
			}

			private void Load(string name, Action<Dictionary<int, RecipeDataInfo[]>> action)
			{
				List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/gameitem/recipe/" + $"{name}/", subdirCheck: true);
				assetBundleNameListFromPath.Sort();
				List<RecipeData.Param> list = new List<RecipeData.Param>();
				foreach (string item in assetBundleNameListFromPath)
				{
					RecipeData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(RecipeData)).GetAllAssets<RecipeData>();
					foreach (RecipeData recipeData in allAssets)
					{
						list.AddRange(recipeData.param);
					}
					AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
				}
				Dictionary<int, RecipeDataInfo[]> obj = (from item in list
					select new RecipeDataInfo(item) into p
					where p.NeedList.Any()
					select p).ToLookup((RecipeDataInfo v) => v.nameHash, (RecipeDataInfo v) => v).ToDictionary((IGrouping<int, RecipeDataInfo> v) => v.Key, Enumerable.ToArray);
				action(obj);
			}

			public void Release()
			{
				materialTable = null;
				equipmentTable = null;
				cookTable = null;
				petTable = null;
				medicineTable = null;
				initialized = false;
			}
		}

		public class AdvPresentItemInfo
		{
			public int eventItemID { get; }

			public StuffItemInfo itemInfo { get; }

			public AdvPresentItemInfo(int eventItemID, StuffItemInfo itemInfo)
			{
				this.eventItemID = eventItemID;
				this.itemInfo = itemInfo;
			}
		}

		private Dictionary<int, Dictionary<int, ItemTableElement>> _itemPickTables = new Dictionary<int, Dictionary<int, ItemTableElement>>();

		private Dictionary<int, Dictionary<int, ItemTableElement>> _frogItemTable = new Dictionary<int, Dictionary<int, ItemTableElement>>();

		public bool initialized { get; private set; }

		private Dictionary<int, Dictionary<int, StuffItemInfo>> _itemTables { get; } = new Dictionary<int, Dictionary<int, StuffItemInfo>>();


		private IReadOnlyDictionary<int, StuffItemInfo> _itemNameHashTables { get; set; }

		private Dictionary<int, Dictionary<int, StuffItemInfo>> _systemItemTables { get; } = new Dictionary<int, Dictionary<int, StuffItemInfo>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, FoodParameterPacket>>> FoodParameterTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, FoodParameterPacket>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, FoodParameterPacket>>> DrinkParameterTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, FoodParameterPacket>>>();


		public Dictionary<int, Dictionary<int, EquipEventItemInfo>> SearchEquipEventItemTable { get; private set; } = new Dictionary<int, Dictionary<int, EquipEventItemInfo>>();


		public Dictionary<int, Dictionary<int, EquipEventItemInfo>> CommonEquipEventItemTable { get; private set; } = new Dictionary<int, Dictionary<int, EquipEventItemInfo>>();


		public Dictionary<int, Dictionary<int, AccessoryItemInfo>> AccessoryItem { get; private set; } = new Dictionary<int, Dictionary<int, AccessoryItemInfo>>();


		public Dictionary<int, Dictionary<int, RecyclingItemInfo>> RecyclingCreateableItemTable { get; private set; } = new Dictionary<int, Dictionary<int, RecyclingItemInfo>>();


		public List<RecyclingItemInfo> RecyclingCreateableItemList { get; private set; } = new List<RecyclingItemInfo>();


		public Dictionary<int, Dictionary<int, List<VendItemInfo>>> VendItemInfoTable { get; } = new Dictionary<int, Dictionary<int, List<VendItemInfo>>>();


		public IReadOnlyDictionary<int, VendItemInfo> VendItemInfoSpecialTable { get; private set; }

		public RecipeInfo recipe { get; } = new RecipeInfo();


		private IReadOnlyDictionary<int, HarvestDataInfo> HarvestDataInfoTable { get; set; }

		private IReadOnlyDictionary<int, Dictionary<int, AgentAdvPresentInfo.Param>> AgentAdvPresentInfoTable { get; set; }

		private IReadOnlyDictionary<int, Dictionary<int, AgentAdvPresentBirthdayInfo.Param>> AgentAdvPresentBirthdayInfoTable { get; set; }

		private IReadOnlyDictionary<int, Dictionary<int, AgentAdvScroungeInfo.Param>> AgentAdvScroungeInfoTable { get; set; }

		private IReadOnlyDictionary<int, Dictionary<string, AgentAdvEventInfo.Param>> AgentAdvEventInfoTable { get; set; }

		public IReadOnlyDictionary<int, LifeStyleData.Param> AgentLifeStyleInfoTable { get; private set; }

		public int[] GetItemCategories()
		{
			return _itemTables.Keys.ToArray();
		}

		public Dictionary<int, StuffItemInfo> GetItemTable(int category)
		{
			Dictionary<int, StuffItemInfo> value;
			return _itemTables.TryGetValue(category, out value) ? value : null;
		}

		public StuffItemInfo FindItemInfo(int nameHash)
		{
			StuffItemInfo value;
			return _itemNameHashTables.TryGetValue(nameHash, out value) ? value : null;
		}

		public StuffItemInfo GetItem(int category, int id)
		{
			Dictionary<int, StuffItemInfo> itemTable = GetItemTable(category);
			if (itemTable == null)
			{
				return null;
			}
			StuffItemInfo value;
			return itemTable.TryGetValue(id, out value) ? value : null;
		}

		public StuffItemInfo GetItem_System(int category, int id)
		{
			if (!_systemItemTables.TryGetValue(category, out var value))
			{
				return null;
			}
			StuffItemInfo value2;
			return value.TryGetValue(id, out value2) ? value2 : null;
		}

		public bool CanEat(StuffItem item)
		{
			Dictionary<int, Dictionary<int, FoodParameterPacket>> value;
			return (FoodParameterTable.TryGetValue(item.CategoryID, out value) && value.ContainsKey(item.ID)) || (DrinkParameterTable.TryGetValue(item.CategoryID, out value) && value.ContainsKey(item.ID));
		}

		public bool CanEat(StuffItemInfo item)
		{
			Dictionary<int, Dictionary<int, FoodParameterPacket>> value;
			return (FoodParameterTable.TryGetValue(item.CategoryID, out value) && value.ContainsKey(item.ID)) || (DrinkParameterTable.TryGetValue(item.CategoryID, out value) && value.ContainsKey(item.ID));
		}

		public int[] GetAreaKeys()
		{
			return _itemPickTables.Keys.ToArray();
		}

		public Dictionary<int, ItemTableElement> GetItemTableInArea(int pointID)
		{
			if (_itemPickTables == null)
			{
				return null;
			}
			if (!_itemPickTables.TryGetValue(pointID, out var value))
			{
				return null;
			}
			return value;
		}

		public Dictionary<int, ItemTableElement> GetItemTableInFrogPoint(int pointID)
		{
			if (_frogItemTable.IsNullOrEmpty())
			{
				return null;
			}
			Dictionary<int, ItemTableElement> value;
			return (!_frogItemTable.TryGetValue(pointID, out value)) ? null : value;
		}

		public AIProject.SaveData.Environment.PlantInfo GetPlantInfo(int nameHash)
		{
			if (!HarvestDataInfoTable.TryGetValue(nameHash, out var value) || value == null)
			{
				return null;
			}
			StuffItem[] result = (from x in ((IEnumerable<HarvestData.Data>)(((object)value?.Get()) ?? ((object)new HarvestData.Data[0]))).Select(delegate(HarvestData.Data x)
				{
					StuffItemInfo stuffItemInfo = FindItemInfo(x.nameHash);
					int? num = stuffItemInfo?.CategoryID;
					int category = ((!num.HasValue) ? (-1) : num.Value);
					int? num2 = stuffItemInfo?.ID;
					return new StuffItem(category, (!num2.HasValue) ? (-1) : num2.Value, x.stock);
				})
				where x.CategoryID != -1
				select x).ToArray();
			return new AIProject.SaveData.Environment.PlantInfo(value.nameHash, value.Time, result);
		}

		public AdvPresentItemInfo GetAdvPresentInfo(ChaControl chaControl)
		{
			ChaFileControl chaFile = chaControl.chaFile;
			Dictionary<int, int> flavorState = chaFile.gameinfo.flavorState;
			int personality = chaFile.parameter.personality;
			if (!AgentAdvPresentInfoTable.TryGetValue(personality, out var value))
			{
				Debug.LogError(string.Format("{0} not found : personality[{1}]", "GetAdvPresentInfo", personality));
				if (!AgentAdvPresentInfoTable.TryGetValue(0, out value))
				{
					return null;
				}
			}
			KeyValuePair<int, int> keyValuePair = flavorState.OrderByDescending((KeyValuePair<int, int> v) => v.Value).FirstOrDefault();
			if (!value.TryGetValue(keyValuePair.Key, out var value2))
			{
				Debug.LogError($"Parameter not found : personality[{personality}]");
				return null;
			}
			Dictionary<AgentAdvPresentInfo.ItemData, int> targetDict = value2.ItemData.ToDictionary((AgentAdvPresentInfo.ItemData v) => v, (AgentAdvPresentInfo.ItemData v) => 100);
			StuffItemInfo stuffItemInfo = FindItemInfo(Illusion.Utils.ProbabilityCalclator.DetermineFromDict(targetDict).nameHash);
			return (stuffItemInfo != null) ? new AdvPresentItemInfo(value2.ItemID, stuffItemInfo) : null;
		}

		public AdvPresentItemInfo GetAdvPresentBirthdayInfo(ChaControl chaControl)
		{
			ChaFileControl chaFile = chaControl.chaFile;
			int personality = chaFile.parameter.personality;
			if (!AgentAdvPresentBirthdayInfoTable.TryGetValue(personality, out var value))
			{
				Debug.LogError(string.Format("{0} not found : personality[{1}]", "GetAdvPresentBirthdayInfo", personality));
				if (!AgentAdvPresentBirthdayInfoTable.TryGetValue(0, out value))
				{
					return null;
				}
			}
			if (!value.Any())
			{
				Debug.LogError(string.Format("{0} [table empty] : personality[{1}]", "GetAdvPresentBirthdayInfo", personality));
				return null;
			}
			AgentAdvPresentBirthdayInfo.Param param = value.Values.Shuffle().First();
			Dictionary<AgentAdvPresentBirthdayInfo.ItemData, int> targetDict = param.ItemData.ToDictionary((AgentAdvPresentBirthdayInfo.ItemData v) => v, (AgentAdvPresentBirthdayInfo.ItemData v) => 100);
			StuffItemInfo stuffItemInfo = FindItemInfo(Illusion.Utils.ProbabilityCalclator.DetermineFromDict(targetDict).nameHash);
			return (stuffItemInfo != null) ? new AdvPresentItemInfo(param.ItemID, stuffItemInfo) : null;
		}

		public System.Tuple<StuffItemInfo, int> GetAdvScroungeInfo(ChaControl chaControl)
		{
			ChaFileControl chaFile = chaControl.chaFile;
			int personality = chaFile.parameter.personality;
			if (!AgentAdvScroungeInfoTable.TryGetValue(personality, out var value))
			{
				Debug.LogError(string.Format("{0} not found : personality[{1}]", "GetAdvScroungeInfo", personality));
				if (!AgentAdvScroungeInfoTable.TryGetValue(0, out value))
				{
					return null;
				}
			}
			if (!value.Any())
			{
				Debug.LogError(string.Format("{0} [table empty] : personality[{1}]", "GetAdvScroungeInfo", personality));
				return null;
			}
			Dictionary<AgentAdvScroungeInfo.ItemData, int> targetDict = value.Values.Shuffle().First().ItemData.ToDictionary((AgentAdvScroungeInfo.ItemData v) => v, (AgentAdvScroungeInfo.ItemData v) => 100);
			AgentAdvScroungeInfo.ItemData itemData = Illusion.Utils.ProbabilityCalclator.DetermineFromDict(targetDict);
			return Tuple.Create(FindItemInfo(itemData.nameHash), itemData.sum);
		}

		public IReadOnlyDictionary<string, AgentAdvEventInfo.Param> GetAgentAdvEvents(AgentActor agent)
		{
			int personality = agent.ChaControl.fileParam.personality;
			if (!AgentAdvEventInfoTable.TryGetValue(personality, out var value))
			{
				Debug.LogError(string.Format("{0}:{1}[{2}]", "GetAgentAdvEvents", "charaID", personality));
				return new Dictionary<string, AgentAdvEventInfo.Param>();
			}
			return value;
		}

		public void Load(DefinePack definePack)
		{
			LoadItemList();
			LoadItemList_System();
			LoadFoodParameterTable(definePack);
			LoadDrinkParameterTable(definePack);
			LoadItemSearchTable(definePack);
			LoadFrogItemTable(definePack);
			recipe.Load();
			LoadSearchEquipItemList(definePack);
			LoadCommonEquipItemList(definePack);
			LoadAccessoryItem(definePack);
			LoadVendItemList();
			LoadSpecialVendItemList();
			LoadHarvest();
			LoadAgentAdvPresentInfoTable();
			LoadAgentAdvPresentBirthdayInfoTable();
			LoadAgentAdvScroungeInfoTable();
			LoadAgentAdvEventInfoTable();
			LoadAgentLifeStyleInfoTable();
			LoadRecyclingInfo(definePack);
			initialized = true;
		}

		public void Release()
		{
			_itemTables.Clear();
			_systemItemTables.Clear();
			FoodParameterTable.Clear();
			DrinkParameterTable.Clear();
			_itemPickTables.Clear();
			_frogItemTable.Clear();
			SearchEquipEventItemTable.Clear();
			CommonEquipEventItemTable.Clear();
			AccessoryItem.Clear();
			VendItemInfoTable.Clear();
			VendItemInfoSpecialTable = null;
			recipe.Release();
			HarvestDataInfoTable = null;
			AgentAdvPresentInfoTable = null;
			AgentAdvPresentBirthdayInfoTable = null;
			AgentAdvScroungeInfoTable = null;
			AgentAdvEventInfoTable = null;
			RecyclingCreateableItemTable.Clear();
			RecyclingCreateableItemList.Clear();
			initialized = false;
		}

		private void LoadItemList()
		{
			Dictionary<int, System.Tuple<string, Sprite>> categoryIcon = Singleton<Resources>.Instance.itemIconTables.CategoryIcon;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/gameitem/info/item/itemlist/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			Dictionary<int, Dictionary<int, ItemData.Param>> dictionary = new Dictionary<int, Dictionary<int, ItemData.Param>>();
			foreach (string item in assetBundleNameListFromPath)
			{
				ItemData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ItemData)).GetAllAssets<ItemData>();
				foreach (ItemData itemData in allAssets)
				{
					if (!int.TryParse(itemData.name, out var result))
					{
						Debug.LogError($"ItemList Category Name:{itemData.name}");
						continue;
					}
					if (!categoryIcon.ContainsKey(result))
					{
						Debug.LogError($"ItemList not Register Category:{result}");
						continue;
					}
					if (!dictionary.TryGetValue(result, out var value))
					{
						value = (dictionary[result] = new Dictionary<int, ItemData.Param>());
					}
					foreach (ItemData.Param item2 in itemData.param)
					{
						value[item2.ID] = item2;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			foreach (KeyValuePair<int, Dictionary<int, ItemData.Param>> item3 in dictionary)
			{
				int key = item3.Key;
				Dictionary<int, StuffItemInfo> dictionary3 = new Dictionary<int, StuffItemInfo>();
				foreach (KeyValuePair<int, ItemData.Param> item4 in item3.Value)
				{
					StuffItemInfo stuffItemInfo = new StuffItemInfo(key, item4.Value, isNone: false);
					stuffItemInfo.ReactionType = -1;
					stuffItemInfo.IsAvailableHeroine = false;
					stuffItemInfo.EnnuiAddition = new ThresholdInt(0, 1);
					stuffItemInfo.TasteAdditionNormal = new ThresholdInt(0, 1);
					stuffItemInfo.TasteAdditionEnnui = new ThresholdInt(0, 1);
					stuffItemInfo.EquipableState = ItemEquipableState.Impossible;
					StuffItemInfo value2 = stuffItemInfo;
					dictionary3[item4.Key] = value2;
				}
				_itemTables[key] = dictionary3;
			}
			_itemNameHashTables = _itemTables.Values.SelectMany((Dictionary<int, StuffItemInfo> x) => x.Select((KeyValuePair<int, StuffItemInfo> y) => y.Value)).ToDictionary((StuffItemInfo v) => v.nameHash, (StuffItemInfo v) => v);
		}

		private void LoadItemList_System()
		{
			Dictionary<int, System.Tuple<string, Sprite>> categoryIcon = Singleton<Resources>.Instance.itemIconTables.CategoryIcon;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/gameitem/info/item/itemlist_system/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			Dictionary<int, Dictionary<int, ItemData_System.Param>> dictionary = new Dictionary<int, Dictionary<int, ItemData_System.Param>>();
			foreach (string item in assetBundleNameListFromPath)
			{
				ItemData_System[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ItemData_System)).GetAllAssets<ItemData_System>();
				foreach (ItemData_System itemData_System in allAssets)
				{
					if (!int.TryParse(itemData_System.name, out var result))
					{
						Debug.LogError($"ItemList Category Name:{itemData_System.name}");
						continue;
					}
					if (!dictionary.TryGetValue(result, out var value))
					{
						value = (dictionary[result] = new Dictionary<int, ItemData_System.Param>());
					}
					foreach (ItemData_System.Param item2 in itemData_System.param)
					{
						value[item2.ID] = item2;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			foreach (KeyValuePair<int, Dictionary<int, ItemData_System.Param>> item3 in dictionary)
			{
				int key = item3.Key;
				Dictionary<int, StuffItemInfo> dictionary3 = new Dictionary<int, StuffItemInfo>();
				foreach (KeyValuePair<int, ItemData_System.Param> item4 in item3.Value)
				{
					dictionary3[item4.Key] = ItemData_System.Convert(key, item4.Value);
				}
				_systemItemTables[key] = dictionary3;
			}
		}

		private void LoadFoodParameterTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.FoodInfo);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ExcelData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ExcelData)).GetAllAssets<ExcelData>();
				foreach (ExcelData excelData in allAssets)
				{
					foreach (ExcelData.Param item2 in excelData.list)
					{
						int num = 1;
						string element = item2.list.GetElement(num++);
						if (!int.TryParse(element, out var result))
						{
							continue;
						}
						if (!FoodParameterTable.TryGetValue(result, out var value))
						{
							Dictionary<int, Dictionary<int, FoodParameterPacket>> dictionary = new Dictionary<int, Dictionary<int, FoodParameterPacket>>();
							FoodParameterTable[result] = dictionary;
							value = dictionary;
						}
						string element2 = item2.list.GetElement(num++);
						if (!int.TryParse(element2, out var result2))
						{
							continue;
						}
						if (!value.TryGetValue(result2, out var value2))
						{
							Dictionary<int, FoodParameterPacket> dictionary3 = (value[result2] = new Dictionary<int, FoodParameterPacket>());
							value2 = dictionary3;
						}
						string element3 = item2.list.GetElement(num++);
						if (!int.TryParse(element3, out var result3))
						{
							continue;
						}
						int result4;
						int num2 = (int.TryParse(item2.list.GetElement(num++), out result4) ? result4 : 0);
						float result5;
						float minValue = ((!float.TryParse(item2.list.GetElement(num++), out result5)) ? 0f : result5);
						float maxValue = ((!float.TryParse(item2.list.GetElement(num++), out result5)) ? 0f : result5);
						float minValue2 = ((!float.TryParse(item2.list.GetElement(num++), out result5)) ? 0f : result5);
						float maxValue2 = ((!float.TryParse(item2.list.GetElement(num++), out result5)) ? 0f : result5);
						int num3 = (int.TryParse(item2.list.GetElement(num++), out result4) ? result4 : 0);
						FoodParameterPacket foodParameterPacket = new FoodParameterPacket();
						foodParameterPacket.Probability = num2;
						foodParameterPacket.SatiationAscentThreshold = new Threshold(minValue, maxValue);
						foodParameterPacket.SatiationDescentThreshold = new Threshold(minValue2, maxValue2);
						foodParameterPacket.StomachacheRate = num3;
						FoodParameterPacket foodParameterPacket2 = foodParameterPacket;
						while (num < item2.list.Count)
						{
							string element4 = item2.list.GetElement(num++);
							string element5 = item2.list.GetElement(num++);
							string element6 = item2.list.GetElement(num++);
							string element7 = item2.list.GetElement(num++);
							if (!element4.IsNullOrEmpty())
							{
								if (!StatusTagTable.TryGetValue(element4, out var value3))
								{
									Debug.LogWarning($"タグ読み取りエラー: 値={element4}");
									continue;
								}
								int s = (int.TryParse(element5, out result4) ? result4 : 0);
								int m = (int.TryParse(element6, out result4) ? result4 : 0);
								int l = (int.TryParse(element7, out result4) ? result4 : 0);
								foodParameterPacket2.Parameters[value3] = new TriThreshold(s, m, l);
							}
						}
						value2[result3] = foodParameterPacket2;
					}
				}
			}
		}

		private void LoadDrinkParameterTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.DrinkInfo);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				ExcelData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(ExcelData)).GetAllAssets<ExcelData>();
				foreach (ExcelData excelData in allAssets)
				{
					foreach (ExcelData.Param item2 in excelData.list)
					{
						int num = 1;
						string element = item2.list.GetElement(num++);
						if (!int.TryParse(element, out var result))
						{
							continue;
						}
						if (!DrinkParameterTable.TryGetValue(result, out var value))
						{
							Dictionary<int, Dictionary<int, FoodParameterPacket>> dictionary = new Dictionary<int, Dictionary<int, FoodParameterPacket>>();
							DrinkParameterTable[result] = dictionary;
							value = dictionary;
						}
						string element2 = item2.list.GetElement(num++);
						if (!int.TryParse(element2, out var result2))
						{
							continue;
						}
						if (!value.TryGetValue(result2, out var value2))
						{
							Dictionary<int, FoodParameterPacket> dictionary3 = (value[result2] = new Dictionary<int, FoodParameterPacket>());
							value2 = dictionary3;
						}
						string element3 = item2.list.GetElement(num++);
						if (!int.TryParse(element3, out var result3))
						{
							continue;
						}
						int result4;
						int num2 = (int.TryParse(item2.list.GetElement(num++), out result4) ? result4 : 0);
						int num3 = (int.TryParse(item2.list.GetElement(num++), out result4) ? result4 : 0);
						FoodParameterPacket foodParameterPacket = new FoodParameterPacket();
						foodParameterPacket.Probability = num2;
						foodParameterPacket.StomachacheRate = num3;
						FoodParameterPacket foodParameterPacket2 = foodParameterPacket;
						while (num < item2.list.Count)
						{
							string element4 = item2.list.GetElement(num++);
							string element5 = item2.list.GetElement(num++);
							string element6 = item2.list.GetElement(num++);
							string element7 = item2.list.GetElement(num++);
							if (!element4.IsNullOrEmpty())
							{
								if (!StatusTagTable.TryGetValue(element4, out var value3))
								{
									Debug.LogWarning($"タグ読み取りエラー: 値={element4}");
									continue;
								}
								int s = (int.TryParse(element5, out result4) ? result4 : 0);
								int m = (int.TryParse(element6, out result4) ? result4 : 0);
								int l = (int.TryParse(element7, out result4) ? result4 : 0);
								foodParameterPacket2.Parameters[value3] = new TriThreshold(s, m, l);
							}
						}
						value2[result3] = foodParameterPacket2;
					}
				}
			}
		}

		private void LoadItemSearchTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.GatheringTable);
			assetBundleNameListFromPath.Sort();
			foreach (string item11 in assetBundleNameListFromPath)
			{
				GatherItemData[] allAssets = AssetBundleManager.LoadAllAsset(item11, typeof(GatherItemData)).GetAllAssets<GatherItemData>();
				foreach (GatherItemData gatherItemData in allAssets)
				{
					if (!int.TryParse(gatherItemData.name, out var result))
					{
						continue;
					}
					if (!_itemPickTables.TryGetValue(result, out var value))
					{
						Dictionary<int, ItemTableElement> dictionary = new Dictionary<int, ItemTableElement>();
						_itemPickTables[result] = dictionary;
						value = dictionary;
					}
					foreach (GatherItemData.Param item12 in gatherItemData.param)
					{
						ItemTableElement itemTableElement = new ItemTableElement();
						itemTableElement.Rate = item12.Rate;
						itemTableElement = itemTableElement;
						value[item12.ID] = itemTableElement;
						ItemTableElement itemTableElement2 = itemTableElement;
						if (item12.CategoryID_0 > 0)
						{
							string name = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_0, out var value2) && value2.TryGetValue(item12.ItemID_0, out var value3))
							{
								name = value3.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name;
							gatherElement.prob = item12.Probability_0 / 100f;
							gatherElement.categoryID = item12.CategoryID_0;
							gatherElement.itemID = item12.ItemID_0;
							gatherElement.minCount = item12.Min_0;
							gatherElement.maxCount = item12.Max_0;
							ItemTableElement.GatherElement item = gatherElement;
							itemTableElement2.Elements.Add(item);
						}
						if (item12.CategoryID_1 > 0)
						{
							string name2 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_1, out var value4) && value4.TryGetValue(item12.ItemID_1, out var value5))
							{
								name2 = value5.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name2;
							gatherElement.prob = item12.Probability_1 / 100f;
							gatherElement.categoryID = item12.CategoryID_1;
							gatherElement.itemID = item12.ItemID_1;
							gatherElement.minCount = item12.Min_1;
							gatherElement.maxCount = item12.Max_1;
							ItemTableElement.GatherElement item2 = gatherElement;
							itemTableElement2.Elements.Add(item2);
						}
						if (item12.CategoryID_2 > 0)
						{
							string name3 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_2, out var value6) && value6.TryGetValue(item12.ItemID_2, out var value7))
							{
								name3 = value7.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name3;
							gatherElement.prob = item12.Probability_2 / 100f;
							gatherElement.categoryID = item12.CategoryID_2;
							gatherElement.itemID = item12.ItemID_2;
							gatherElement.minCount = item12.Min_2;
							gatherElement.maxCount = item12.Max_2;
							ItemTableElement.GatherElement item3 = gatherElement;
							itemTableElement2.Elements.Add(item3);
						}
						if (item12.CategoryID_3 > 0)
						{
							string name4 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_3, out var value8) && value8.TryGetValue(item12.ItemID_3, out var value9))
							{
								name4 = value9.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name4;
							gatherElement.prob = item12.Probability_3 / 100f;
							gatherElement.categoryID = item12.CategoryID_3;
							gatherElement.itemID = item12.ItemID_3;
							gatherElement.minCount = item12.Min_3;
							gatherElement.maxCount = item12.Max_3;
							ItemTableElement.GatherElement item4 = gatherElement;
							itemTableElement2.Elements.Add(item4);
						}
						if (item12.CategoryID_4 > 0)
						{
							string name5 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_4, out var value10) && value10.TryGetValue(item12.ItemID_4, out var value11))
							{
								name5 = value11.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name5;
							gatherElement.prob = item12.Probability_4 / 100f;
							gatherElement.categoryID = item12.CategoryID_4;
							gatherElement.itemID = item12.ItemID_4;
							gatherElement.minCount = item12.Min_4;
							gatherElement.maxCount = item12.Max_4;
							ItemTableElement.GatherElement item5 = gatherElement;
							itemTableElement2.Elements.Add(item5);
						}
						if (item12.CategoryID_5 > 0)
						{
							string name6 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_5, out var value12) && value12.TryGetValue(item12.ItemID_5, out var value13))
							{
								name6 = value13.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name6;
							gatherElement.prob = item12.Probability_5 / 100f;
							gatherElement.categoryID = item12.CategoryID_5;
							gatherElement.itemID = item12.ItemID_5;
							gatherElement.minCount = item12.Min_5;
							gatherElement.maxCount = item12.Max_5;
							ItemTableElement.GatherElement item6 = gatherElement;
							itemTableElement2.Elements.Add(item6);
						}
						if (item12.CategoryID_6 > 0)
						{
							string name7 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_6, out var value14) && value14.TryGetValue(item12.ItemID_6, out var value15))
							{
								name7 = value15.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name7;
							gatherElement.prob = item12.Probability_6 / 100f;
							gatherElement.categoryID = item12.CategoryID_6;
							gatherElement.itemID = item12.ItemID_6;
							gatherElement.minCount = item12.Min_6;
							gatherElement.maxCount = item12.Max_6;
							ItemTableElement.GatherElement item7 = gatherElement;
							itemTableElement2.Elements.Add(item7);
						}
						if (item12.CategoryID_7 > 0)
						{
							string name8 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_7, out var value16) && value16.TryGetValue(item12.ItemID_7, out var value17))
							{
								name8 = value17.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name8;
							gatherElement.prob = item12.Probability_7 / 100f;
							gatherElement.categoryID = item12.CategoryID_7;
							gatherElement.itemID = item12.ItemID_7;
							gatherElement.minCount = item12.Min_7;
							gatherElement.maxCount = item12.Max_7;
							ItemTableElement.GatherElement item8 = gatherElement;
							itemTableElement2.Elements.Add(item8);
						}
						if (item12.CategoryID_8 > 0)
						{
							string name9 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_8, out var value18) && value18.TryGetValue(item12.ItemID_8, out var value19))
							{
								name9 = value19.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name9;
							gatherElement.prob = item12.Probability_8 / 100f;
							gatherElement.categoryID = item12.CategoryID_8;
							gatherElement.itemID = item12.ItemID_8;
							gatherElement.minCount = item12.Min_8;
							gatherElement.maxCount = item12.Max_8;
							ItemTableElement.GatherElement item9 = gatherElement;
							itemTableElement2.Elements.Add(item9);
						}
						if (item12.CategoryID_9 > 0)
						{
							string name10 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_9, out var value20) && value20.TryGetValue(item12.ItemID_9, out var value21))
							{
								name10 = value21.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name10;
							gatherElement.prob = item12.Probability_9 / 100f;
							gatherElement.categoryID = item12.CategoryID_9;
							gatherElement.itemID = item12.ItemID_9;
							gatherElement.minCount = item12.Min_9;
							gatherElement.maxCount = item12.Max_9;
							ItemTableElement.GatherElement item10 = gatherElement;
							itemTableElement2.Elements.Add(item10);
						}
					}
				}
				AssetBundleManager.UnloadAssetBundle(item11, isUnloadForceRefCount: false);
			}
		}

		private void LoadFrogItemTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.FrogItemTable);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			foreach (string item11 in assetBundleNameListFromPath)
			{
				if (item11.IsNullOrEmpty())
				{
					continue;
				}
				GatherItemData[] allAssets = AssetBundleManager.LoadAllAsset(item11, typeof(GatherItemData)).GetAllAssets<GatherItemData>();
				foreach (GatherItemData gatherItemData in allAssets)
				{
					if (gatherItemData == null)
					{
						continue;
					}
					string s = gatherItemData.name ?? string.Empty;
					if (!int.TryParse(s, out var result))
					{
						continue;
					}
					if (!_frogItemTable.TryGetValue(result, out var value))
					{
						value = (_frogItemTable[result] = new Dictionary<int, ItemTableElement>());
					}
					foreach (GatherItemData.Param item12 in gatherItemData.param)
					{
						ItemTableElement itemTableElement = new ItemTableElement();
						itemTableElement.Rate = item12.Rate;
						itemTableElement = itemTableElement;
						value[item12.ID] = itemTableElement;
						ItemTableElement itemTableElement2 = itemTableElement;
						if (0 < item12.CategoryID_0)
						{
							string name = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_0, out var value2) && value2.TryGetValue(item12.ItemID_0, out var value3))
							{
								name = value3.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name;
							gatherElement.prob = item12.Probability_0 / 100f;
							gatherElement.categoryID = item12.CategoryID_0;
							gatherElement.itemID = item12.ItemID_0;
							gatherElement.minCount = item12.Min_0;
							gatherElement.maxCount = item12.Max_0;
							ItemTableElement.GatherElement item = gatherElement;
							itemTableElement2.Elements.Add(item);
						}
						if (0 < item12.CategoryID_1)
						{
							string name2 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_1, out var value4) && value4.TryGetValue(item12.ItemID_1, out var value5))
							{
								name2 = value5.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name2;
							gatherElement.prob = item12.Probability_1 / 100f;
							gatherElement.categoryID = item12.CategoryID_1;
							gatherElement.itemID = item12.ItemID_1;
							gatherElement.minCount = item12.Min_1;
							gatherElement.maxCount = item12.Max_1;
							ItemTableElement.GatherElement item2 = gatherElement;
							itemTableElement2.Elements.Add(item2);
						}
						if (0 < item12.CategoryID_2)
						{
							string name3 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_2, out var value6) && value6.TryGetValue(item12.ItemID_2, out var value7))
							{
								name3 = value7.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name3;
							gatherElement.prob = item12.Probability_2 / 100f;
							gatherElement.categoryID = item12.CategoryID_2;
							gatherElement.itemID = item12.ItemID_2;
							gatherElement.minCount = item12.Min_2;
							gatherElement.maxCount = item12.Max_2;
							ItemTableElement.GatherElement item3 = gatherElement;
							itemTableElement2.Elements.Add(item3);
						}
						if (0 < item12.CategoryID_3)
						{
							string name4 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_3, out var value8) && value8.TryGetValue(item12.ItemID_3, out var value9))
							{
								name4 = value9.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name4;
							gatherElement.prob = item12.Probability_3 / 100f;
							gatherElement.categoryID = item12.CategoryID_3;
							gatherElement.itemID = item12.ItemID_3;
							gatherElement.minCount = item12.Min_3;
							gatherElement.maxCount = item12.Max_3;
							ItemTableElement.GatherElement item4 = gatherElement;
							itemTableElement2.Elements.Add(item4);
						}
						if (0 < item12.CategoryID_4)
						{
							string name5 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_4, out var value10) && value10.TryGetValue(item12.ItemID_4, out var value11))
							{
								name5 = value11.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name5;
							gatherElement.prob = item12.Probability_4 / 100f;
							gatherElement.categoryID = item12.CategoryID_4;
							gatherElement.itemID = item12.ItemID_4;
							gatherElement.minCount = item12.Min_4;
							gatherElement.maxCount = item12.Max_4;
							ItemTableElement.GatherElement item5 = gatherElement;
							itemTableElement2.Elements.Add(item5);
						}
						if (0 < item12.CategoryID_5)
						{
							string name6 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_5, out var value12) && value12.TryGetValue(item12.ItemID_5, out var value13))
							{
								name6 = value13.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name6;
							gatherElement.prob = item12.Probability_5 / 100f;
							gatherElement.categoryID = item12.CategoryID_5;
							gatherElement.itemID = item12.ItemID_5;
							gatherElement.minCount = item12.Min_5;
							gatherElement.maxCount = item12.Max_5;
							ItemTableElement.GatherElement item6 = gatherElement;
							itemTableElement2.Elements.Add(item6);
						}
						if (0 < item12.CategoryID_6)
						{
							string name7 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_6, out var value14) && value14.TryGetValue(item12.ItemID_6, out var value15))
							{
								name7 = value15.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name7;
							gatherElement.prob = item12.Probability_6 / 100f;
							gatherElement.categoryID = item12.CategoryID_6;
							gatherElement.itemID = item12.ItemID_6;
							gatherElement.minCount = item12.Min_6;
							gatherElement.maxCount = item12.Max_6;
							ItemTableElement.GatherElement item7 = gatherElement;
							itemTableElement2.Elements.Add(item7);
						}
						if (0 < item12.CategoryID_7)
						{
							string name8 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_7, out var value16) && value16.TryGetValue(item12.ItemID_7, out var value17))
							{
								name8 = value17.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name8;
							gatherElement.prob = item12.Probability_7 / 100f;
							gatherElement.categoryID = item12.CategoryID_7;
							gatherElement.itemID = item12.ItemID_7;
							gatherElement.minCount = item12.Min_7;
							gatherElement.maxCount = item12.Max_7;
							ItemTableElement.GatherElement item8 = gatherElement;
							itemTableElement2.Elements.Add(item8);
						}
						if (0 < item12.CategoryID_8)
						{
							string name9 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_8, out var value18) && value18.TryGetValue(item12.ItemID_8, out var value19))
							{
								name9 = value19.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name9;
							gatherElement.prob = item12.Probability_8 / 100f;
							gatherElement.categoryID = item12.CategoryID_8;
							gatherElement.itemID = item12.ItemID_8;
							gatherElement.minCount = item12.Min_8;
							gatherElement.maxCount = item12.Max_8;
							ItemTableElement.GatherElement item9 = gatherElement;
							itemTableElement2.Elements.Add(item9);
						}
						if (0 < item12.CategoryID_9)
						{
							string name10 = string.Empty;
							if (_itemTables.TryGetValue(item12.CategoryID_9, out var value20) && value20.TryGetValue(item12.ItemID_9, out var value21))
							{
								name10 = value21.Name;
							}
							ItemTableElement.GatherElement gatherElement = default(ItemTableElement.GatherElement);
							gatherElement.name = name10;
							gatherElement.prob = item12.Probability_9 / 100f;
							gatherElement.categoryID = item12.CategoryID_9;
							gatherElement.itemID = item12.ItemID_9;
							gatherElement.minCount = item12.Min_9;
							gatherElement.maxCount = item12.Max_9;
							ItemTableElement.GatherElement item10 = gatherElement;
							itemTableElement2.Elements.Add(item10);
						}
					}
				}
				AssetBundleManager.UnloadAssetBundle(item11, isUnloadForceRefCount: false);
			}
		}

		private void TryGet(ref bool success, List<string> row, ref int idx, out float value)
		{
			if (success)
			{
				success &= float.TryParse(row.GetElement(idx++) ?? string.Empty, out value);
				return;
			}
			idx++;
			value = 0f;
		}

		private void TryGet(ref bool success, List<string> row, ref int idx, out int value)
		{
			if (success)
			{
				success &= int.TryParse(row.GetElement(idx++) ?? string.Empty, out value);
				return;
			}
			idx++;
			value = 0;
		}

		private void LoadSearchEquipItemList(DefinePack definePack)
		{
			Dictionary<int, ActionItemInfo> eventItemList = Singleton<Resources>.Instance.Map.EventItemList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.SearchEquipItemObjList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 1;
					if (int.TryParse(param.list.GetElement(num2++), out var result) && int.TryParse(param.list.GetElement(num2++), out var result2) && int.TryParse(param.list.GetElement(num2++), out var result3))
					{
						if (!SearchEquipEventItemTable.TryGetValue(result, out var value))
						{
							Dictionary<int, EquipEventItemInfo> dictionary = new Dictionary<int, EquipEventItemInfo>();
							SearchEquipEventItemTable[result] = dictionary;
							value = dictionary;
						}
						if (eventItemList.TryGetValue(result3, out var value2))
						{
							value[result2] = new EquipEventItemInfo
							{
								EventItemID = result3,
								ActionItemInfo = value2
							};
						}
					}
				}
			}
		}

		private void LoadCommonEquipItemList(DefinePack definePack)
		{
			Dictionary<int, ActionItemInfo> eventItemList = Singleton<Resources>.Instance.Map.EventItemList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.CommonEquipItemObjList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 1;
					if (int.TryParse(param.list.GetElement(num2++), out var result) && int.TryParse(param.list.GetElement(num2++), out var result2) && int.TryParse(param.list.GetElement(num2++), out var result3))
					{
						if (!CommonEquipEventItemTable.TryGetValue(result, out var value))
						{
							Dictionary<int, EquipEventItemInfo> dictionary = new Dictionary<int, EquipEventItemInfo>();
							CommonEquipEventItemTable[result] = dictionary;
							value = dictionary;
						}
						if (eventItemList.TryGetValue(result3, out var value2))
						{
							value[result2] = new EquipEventItemInfo
							{
								EventItemID = result2,
								ActionItemInfo = value2
							};
						}
					}
				}
			}
		}

		private void LoadAccessoryItem(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AccessoryItem);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				GameItemAccessoryData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(GameItemAccessoryData)).GetAllAssets<GameItemAccessoryData>();
				foreach (GameItemAccessoryData gameItemAccessoryData in allAssets)
				{
					if (!int.TryParse(gameItemAccessoryData.name, out var result))
					{
						Debug.LogError($"Failed Load: AccessoryItem Category {gameItemAccessoryData.name}");
						continue;
					}
					if (!AccessoryItem.TryGetValue(result, out var value))
					{
						Dictionary<int, AccessoryItemInfo> dictionary = new Dictionary<int, AccessoryItemInfo>();
						AccessoryItem[result] = dictionary;
						value = dictionary;
					}
					foreach (GameItemAccessoryData.Param item2 in gameItemAccessoryData.param)
					{
						if (!item2.AssetBundle.IsNullOrEmpty() && !item2.Asset.IsNullOrEmpty() && !item2.ParentName.IsNullOrEmpty())
						{
							value[item2.ID] = new AccessoryItemInfo
							{
								Name = item2.Name,
								AssetBundle = item2.AssetBundle,
								Asset = item2.Asset,
								Manifest = item2.Manifest,
								ParentName = item2.ParentName,
								Weight = item2.Weight
							};
						}
					}
				}
			}
		}

		private void LoadVendItemList()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/merchant/vend/normal/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			Dictionary<int, List<VendData.Param>> dictionary = new Dictionary<int, List<VendData.Param>>();
			foreach (string item in assetBundleNameListFromPath)
			{
				VendData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(VendData)).GetAllAssets<VendData>();
				foreach (VendData vendData in allAssets)
				{
					if (!int.TryParse(vendData.name, out var result))
					{
						Debug.LogError($"VendData Stage Name:{vendData.name}");
					}
					else
					{
						dictionary[result] = vendData.param;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			foreach (KeyValuePair<int, List<VendData.Param>> item2 in dictionary)
			{
				Dictionary<int, List<VendItemInfo>> dictionary2 = new Dictionary<int, List<VendItemInfo>>();
				foreach (IGrouping<int, VendData.Param> item3 in from p in item2.Value
					group p by p.Group)
				{
					dictionary2[item3.Key] = (from p in item3.Select(delegate(VendData.Param data)
						{
							StuffItemInfo stuffItemInfo = FindItemInfo(data.nameHash);
							if (stuffItemInfo != null)
							{
								return new VendItemInfo(stuffItemInfo, data);
							}
							Debug.LogError($"VendData Item Name:{data.nameHash}");
							return (VendItemInfo)null;
						})
						where p != null
						select p).ToList();
				}
				VendItemInfoTable[item2.Key] = dictionary2;
			}
		}

		private void LoadSpecialVendItemList()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/merchant/vend/special/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			Dictionary<int, VendItemInfo> dictionary = new Dictionary<int, VendItemInfo>();
			foreach (string item in assetBundleNameListFromPath)
			{
				VendSpecialData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(VendSpecialData)).GetAllAssets<VendSpecialData>();
				foreach (VendSpecialData vendSpecialData in allAssets)
				{
					foreach (VendSpecialData.Param item2 in vendSpecialData.param)
					{
						StuffItemInfo stuffItemInfo = FindItemInfo(item2.nameHash);
						if (stuffItemInfo == null)
						{
							Debug.LogError($"VendSpecialData Item Name:{item2.nameHash}");
						}
						else
						{
							dictionary[item2.ID] = new VendItemInfo(stuffItemInfo, item2);
						}
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			VendItemInfoSpecialTable = dictionary;
		}

		private void LoadHarvest()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/gameitem/harvest/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			List<HarvestData.Param> list = new List<HarvestData.Param>();
			foreach (string item in assetBundleNameListFromPath)
			{
				HarvestData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(HarvestData)).GetAllAssets<HarvestData>();
				foreach (HarvestData harvestData in allAssets)
				{
					list.AddRange(harvestData.param);
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			HarvestDataInfoTable = (from item in list
				select new HarvestDataInfo(item) into p
				where p.table.Any() && FindItemInfo(p.nameHash) != null
				select p).ToDictionary((HarvestDataInfo v) => v.nameHash, (HarvestDataInfo v) => v);
		}

		private void LoadAgentAdvPresentInfoTable()
		{
			Dictionary<int, Dictionary<int, AgentAdvPresentInfo.Param>> dictionary = new Dictionary<int, Dictionary<int, AgentAdvPresentInfo.Param>>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/agent/present/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				AgentAdvPresentInfo[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(AgentAdvPresentInfo)).GetAllAssets<AgentAdvPresentInfo>();
				foreach (AgentAdvPresentInfo agentAdvPresentInfo in allAssets)
				{
					if (!int.TryParse(agentAdvPresentInfo.name, out var result))
					{
						Debug.LogError(string.Format("{0}:{1}", "LoadAgentAdvPresentInfoTable", agentAdvPresentInfo.name));
						continue;
					}
					if (!dictionary.TryGetValue(result, out var value))
					{
						value = (dictionary[result] = new Dictionary<int, AgentAdvPresentInfo.Param>());
					}
					foreach (AgentAdvPresentInfo.Param item2 in agentAdvPresentInfo.param)
					{
						value[item2.ID] = item2;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			foreach (Dictionary<int, AgentAdvPresentInfo.Param> value2 in dictionary.Values)
			{
				int[] array = value2.Keys.ToArray();
				foreach (int key in array)
				{
					AgentAdvPresentInfo.Param param = new AgentAdvPresentInfo.Param(value2[key]);
					foreach (AgentAdvPresentInfo.ItemData item3 in param.ItemData.Where((AgentAdvPresentInfo.ItemData x) => FindItemInfo(x.nameHash) == null))
					{
						param.ItemData.Remove(item3);
					}
					value2[key] = param;
				}
			}
			AgentAdvPresentInfoTable = dictionary;
		}

		private void LoadAgentAdvPresentBirthdayInfoTable()
		{
			Dictionary<int, Dictionary<int, AgentAdvPresentBirthdayInfo.Param>> dictionary = new Dictionary<int, Dictionary<int, AgentAdvPresentBirthdayInfo.Param>>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/agent/present_birthday/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				AgentAdvPresentBirthdayInfo[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(AgentAdvPresentBirthdayInfo)).GetAllAssets<AgentAdvPresentBirthdayInfo>();
				foreach (AgentAdvPresentBirthdayInfo agentAdvPresentBirthdayInfo in allAssets)
				{
					if (!int.TryParse(agentAdvPresentBirthdayInfo.name, out var result))
					{
						Debug.LogError(string.Format("{0}:{1}", "LoadAgentAdvPresentBirthdayInfoTable", agentAdvPresentBirthdayInfo.name));
						continue;
					}
					if (!dictionary.TryGetValue(result, out var value))
					{
						value = (dictionary[result] = new Dictionary<int, AgentAdvPresentBirthdayInfo.Param>());
					}
					foreach (AgentAdvPresentBirthdayInfo.Param item2 in agentAdvPresentBirthdayInfo.param)
					{
						value[item2.ID] = item2;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			foreach (Dictionary<int, AgentAdvPresentBirthdayInfo.Param> value2 in dictionary.Values)
			{
				int[] array = value2.Keys.ToArray();
				foreach (int key in array)
				{
					AgentAdvPresentBirthdayInfo.Param param = new AgentAdvPresentBirthdayInfo.Param(value2[key]);
					foreach (AgentAdvPresentBirthdayInfo.ItemData item3 in param.ItemData.Where((AgentAdvPresentBirthdayInfo.ItemData x) => FindItemInfo(x.nameHash) == null))
					{
						param.ItemData.Remove(item3);
					}
					value2[key] = param;
				}
			}
			AgentAdvPresentBirthdayInfoTable = dictionary;
		}

		private void LoadAgentAdvScroungeInfoTable()
		{
			Dictionary<int, Dictionary<int, AgentAdvScroungeInfo.Param>> dictionary = new Dictionary<int, Dictionary<int, AgentAdvScroungeInfo.Param>>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/agent/scrounge/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				AgentAdvScroungeInfo[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(AgentAdvScroungeInfo)).GetAllAssets<AgentAdvScroungeInfo>();
				foreach (AgentAdvScroungeInfo agentAdvScroungeInfo in allAssets)
				{
					if (!int.TryParse(agentAdvScroungeInfo.name, out var result))
					{
						Debug.LogError(string.Format("{0}:{1}", "LoadAgentAdvScroungeInfoTable", agentAdvScroungeInfo.name));
						continue;
					}
					if (!dictionary.TryGetValue(result, out var value))
					{
						value = (dictionary[result] = new Dictionary<int, AgentAdvScroungeInfo.Param>());
					}
					foreach (AgentAdvScroungeInfo.Param item2 in agentAdvScroungeInfo.param)
					{
						value[item2.ID] = item2;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			foreach (Dictionary<int, AgentAdvScroungeInfo.Param> value2 in dictionary.Values)
			{
				int[] array = value2.Keys.ToArray();
				foreach (int key in array)
				{
					AgentAdvScroungeInfo.Param param = new AgentAdvScroungeInfo.Param(value2[key]);
					foreach (AgentAdvScroungeInfo.ItemData item3 in param.ItemData.Where((AgentAdvScroungeInfo.ItemData x) => FindItemInfo(x.nameHash) == null))
					{
						param.ItemData.Remove(item3);
					}
					value2[key] = param;
				}
			}
			AgentAdvScroungeInfoTable = dictionary;
		}

		private void LoadAgentAdvEventInfoTable()
		{
			Dictionary<int, Dictionary<string, AgentAdvEventInfo.Param>> dictionary = new Dictionary<int, Dictionary<string, AgentAdvEventInfo.Param>>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/agent/event/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				AgentAdvEventInfo[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(AgentAdvEventInfo)).GetAllAssets<AgentAdvEventInfo>();
				foreach (AgentAdvEventInfo agentAdvEventInfo in allAssets)
				{
					if (!int.TryParse(agentAdvEventInfo.name, out var result))
					{
						Debug.LogError(string.Format("{0}:{1}", "LoadAgentAdvEventInfoTable", agentAdvEventInfo.name));
						continue;
					}
					if (!dictionary.TryGetValue(result, out var value))
					{
						value = (dictionary[result] = new Dictionary<string, AgentAdvEventInfo.Param>());
					}
					foreach (AgentAdvEventInfo.Param item2 in agentAdvEventInfo.param)
					{
						value[item2.FileName] = item2;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			AgentAdvEventInfoTable = dictionary;
		}

		private void LoadAgentLifeStyleInfoTable()
		{
			Dictionary<int, LifeStyleData.Param> dictionary = new Dictionary<int, LifeStyleData.Param>();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath("list/actor/agent/lifestyle/", subdirCheck: true);
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				LifeStyleData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(LifeStyleData)).GetAllAssets<LifeStyleData>();
				foreach (LifeStyleData lifeStyleData in allAssets)
				{
					foreach (LifeStyleData.Param item2 in lifeStyleData.param)
					{
						dictionary[item2.ID] = item2;
					}
				}
				AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
			}
			AgentLifeStyleInfoTable = dictionary;
		}

		private void LoadRecyclingInfo(DefinePack definePack)
		{
			if (definePack == null)
			{
				return;
			}
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.RecyclingInfoList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string element = assetBundleNameListFromPath.GetElement(i);
				if (element.IsNullOrEmpty())
				{
					continue;
				}
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(element);
				if (fileNameWithoutExtension.IsNullOrEmpty() || !AssetBundleCheck.IsFile(element, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(element, fileNameWithoutExtension, string.Empty);
				if (excelData == null || excelData.MaxCell <= 0)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetBundleInfo = default(AssetBundleInfo);
						assetBundleInfo.name = list.GetElement(num++);
						assetBundleInfo.assetbundle = list.GetElement(num++);
						assetBundleInfo.asset = list.GetElement(num++);
						assetBundleInfo.manifest = list.GetElement(num++);
						AssetBundleInfo listABInfo = assetBundleInfo;
						if (!listABInfo.asset.IsNullOrEmpty() && !listABInfo.assetbundle.IsNullOrEmpty() && result == 0)
						{
							LoadRecyclingCreateableItemInfoList(listABInfo);
						}
					}
				}
			}
			if (RecyclingCreateableItemTable.IsNullOrEmpty())
			{
				return;
			}
			List<UnityEx.ValueTuple<int, int>> list2 = ListPool<UnityEx.ValueTuple<int, int>>.Get();
			List<int> list3 = ListPool<int>.Get();
			list3.AddRange(RecyclingCreateableItemTable.Keys);
			for (int k = 0; k < list3.Count; k++)
			{
				int key = list3[k];
				if (!RecyclingCreateableItemTable.ContainsKey(key))
				{
					continue;
				}
				Dictionary<int, RecyclingItemInfo> dictionary = RecyclingCreateableItemTable[key];
				if (dictionary.IsNullOrEmpty())
				{
					continue;
				}
				List<int> list4 = ListPool<int>.Get();
				list4.AddRange(dictionary.Keys);
				for (int l = 0; l < list4.Count; l++)
				{
					int key2 = list4[l];
					if (!dictionary.ContainsKey(key2))
					{
						continue;
					}
					RecyclingItemInfo info = dictionary[key2];
					info.ItemInfo = GetItem(info.CategoryID, info.ItemID);
					if (info.ItemInfo == null)
					{
						if (!list2.Exists((UnityEx.ValueTuple<int, int> x) => x.Item1 == info.CategoryID && x.Item2 == info.ItemID))
						{
							list2.Add(new UnityEx.ValueTuple<int, int>(info.CategoryID, info.ItemID));
						}
					}
					else if (info.Adult && !Game.isAdd01 && !list2.Exists((UnityEx.ValueTuple<int, int> x) => x.Item1 == info.CategoryID && x.Item2 == info.ItemID))
					{
						list2.Add(new UnityEx.ValueTuple<int, int>(info.CategoryID, info.ItemID));
					}
					dictionary[key2] = info;
				}
				ListPool<int>.Release(list4);
			}
			ListPool<int>.Release(list3);
			if (!list2.IsNullOrEmpty())
			{
				foreach (UnityEx.ValueTuple<int, int> item in list2)
				{
					if (!RecyclingCreateableItemTable.TryGetValue(item.Item1, out var value))
					{
						continue;
					}
					if (value.IsNullOrEmpty())
					{
						RecyclingCreateableItemTable.Remove(item.Item1);
					}
					else if (value.ContainsKey(item.Item2))
					{
						value.Remove(item.Item2);
						if (value.IsNullOrEmpty())
						{
							RecyclingCreateableItemTable.Remove(item.Item1);
						}
					}
				}
			}
			ListPool<UnityEx.ValueTuple<int, int>>.Release(list2);
			if (RecyclingCreateableItemTable.IsNullOrEmpty())
			{
				return;
			}
			foreach (KeyValuePair<int, Dictionary<int, RecyclingItemInfo>> item2 in RecyclingCreateableItemTable)
			{
				if (item2.Value.IsNullOrEmpty())
				{
					continue;
				}
				foreach (KeyValuePair<int, RecyclingItemInfo> item3 in item2.Value)
				{
					RecyclingCreateableItemList.Add(item3.Value);
				}
			}
			RecyclingCreateableItemList.Sort();
		}

		private void LoadRecyclingCreateableItemInfoList(AssetBundleInfo listABInfo)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(listABInfo);
			if (excelData == null || excelData.MaxCell <= 0)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string value = list.GetElement(num++) ?? string.Empty;
				if (!bool.TryParse(value, out var result4))
				{
					continue;
				}
				string value2 = list.GetElement(num++) ?? string.Empty;
				if (bool.TryParse(value2, out var result5) && !result5 && RecyclingCreateableItemTable.TryGetValue(result, out var value3) && !value3.IsNullOrEmpty() && value3.ContainsKey(result2))
				{
					value3.Remove(result2);
					if (value3.Count <= 0)
					{
						RecyclingCreateableItemTable.Remove(result);
					}
				}
				if (!RecyclingCreateableItemTable.TryGetValue(result, out var value4) || value4 == null)
				{
					Dictionary<int, RecyclingItemInfo> dictionary = new Dictionary<int, RecyclingItemInfo>();
					RecyclingCreateableItemTable[result] = dictionary;
					value4 = dictionary;
				}
				if (!value4.TryGetValue(result2, out var value5))
				{
					RecyclingItemInfo recyclingItemInfo2 = (value4[result2] = default(RecyclingItemInfo));
					value5 = recyclingItemInfo2;
				}
				value5.CategoryID = result;
				value5.ItemID = result2;
				value5.IconID = result3;
				value5.Adult = result4;
				if (value5.ItemNameList == null)
				{
					value5.ItemNameList = new List<string>();
				}
				value5.ItemNameList.Clear();
				while (num < list.Count)
				{
					value5.ItemNameList.Add(list.GetElement(num++) ?? string.Empty);
				}
				value4[result2] = value5;
			}
		}
	}

	public class HSceneTables
	{
		public class HAssetBundle
		{
			public string path;

			public string manifest;

			public HAssetBundle(string _path, string _manifest = "")
			{
				path = _path;
				if (_manifest != string.Empty)
				{
					manifest = _manifest;
				}
				else
				{
					manifest = "abdata";
				}
			}
		}

		private struct HmeshInfo
		{
			public int mapID;

			public string abName;

			public string assetName;

			public string manifest;
		}

		private ExcelData excelData;

		private List<string> row = new List<string>();

		private List<string> pathList = new List<string>();

		private GameObject commonSpace;

		public List<HScene.AnimationListInfo>[] lstAnimInfo = new List<HScene.AnimationListInfo>[6];

		public List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> lstStartAnimInfo = new List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>>();

		public List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>> lstStartAnimInfoM = new List<UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>>();

		public List<StartWaitAnim> startWaitAnims = new List<StartWaitAnim>();

		public List<HScene.EndMotion> lstEndAnimInfo = new List<HScene.EndMotion>();

		public Dictionary<int, List<string>> lstHitObject = new Dictionary<int, List<string>>();

		public Dictionary<string, List<string>> HitObjAtariName = new Dictionary<string, List<string>>();

		public Dictionary<string, List<HitObjectCtrl.CollisionInfo>> DicLstHitObjInfo = new Dictionary<string, List<HitObjectCtrl.CollisionInfo>>();

		public Dictionary<int, Dictionary<int, Dictionary<string, GameObject>>> DicHitObject = new Dictionary<int, Dictionary<int, Dictionary<string, GameObject>>>();

		public List<Dictionary<int, List<HItemCtrl.ListItem>>>[] lstHItemObjInfo = new List<Dictionary<int, List<HItemCtrl.ListItem>>>[6];

		public List<UnityEx.ValueTuple<string, RuntimeAnimatorController>> lstHItemBase = new List<UnityEx.ValueTuple<string, RuntimeAnimatorController>>();

		private RuntimeAnimatorController tmpHItemRuntimeAnimator;

		private List<string> HitemPathList = new List<string>();

		public Dictionary<int, HPointList> hPointLists = new Dictionary<int, HPointList>();

		public Dictionary<int, AutoHPointData> autoHPointDatas;

		public Dictionary<int, HPointList.LoadInfo> hPointListInfos = new Dictionary<int, HPointList.LoadInfo>();

		public Dictionary<int, HPoint.HpointData> loadHPointDatas = new Dictionary<int, HPoint.HpointData>();

		public Dictionary<int, List<UnityEx.ValueTuple<string, string>>> AutoHpointData = new Dictionary<int, List<UnityEx.ValueTuple<string, string>>>();

		public GameObject HPointObj;

		public List<string> HAutoPathList;

		public HAutoCtrl.HAutoInfo HAutoInfo;

		public HAutoCtrl.AutoLeaveItToYou HAutoLeaveItToYou;

		public Dictionary<int, float> autoLeavePersonalityRate = new Dictionary<int, float>();

		public Dictionary<int, float> autoLeaveAttributeRate = new Dictionary<int, float>();

		public string[,] aHsceneBGM;

		public List<HParticleCtrl.ParticleInfo> lstHParticleCtrl = new List<HParticleCtrl.ParticleInfo>();

		public HParticleCtrl hParticle;

		public RuntimeAnimatorController[,] HBaseRuntimeAnimatorControllers = new RuntimeAnimatorController[2, 3];

		private Dictionary<int, Dictionary<int, HmeshInfo>> HmeshDictionary = new Dictionary<int, Dictionary<int, HmeshInfo>>();

		private HmeshInfo hMeshInfo = default(HmeshInfo);

		public Dictionary<int, GameObject> HMeshObjDic = new Dictionary<int, GameObject>();

		public Dictionary<int, Dictionary<int, List<YureCtrl.Info>>> DicDicYure = new Dictionary<int, Dictionary<int, List<YureCtrl.Info>>>();

		public Dictionary<int, Dictionary<int, List<YureCtrlMale.Info>>> DicDicYureMale = new Dictionary<int, Dictionary<int, List<YureCtrlMale.Info>>>();

		public Dictionary<int, List<FeelHit.FeelInfo>> DicLstHitInfo = new Dictionary<int, List<FeelHit.FeelInfo>>();

		public Dictionary<string, List<H_Lookat_dan.MotionLookAtList>> DicLstLookAtDan = new Dictionary<string, List<H_Lookat_dan.MotionLookAtList>>();

		public Dictionary<string, List<CollisionCtrl.CollisionInfo>> DicLstCollisionInfo = new Dictionary<string, List<CollisionCtrl.CollisionInfo>>();

		public Dictionary<string, Dictionary<string, HLayerCtrl.HLayerInfo>> LayerInfos = new Dictionary<string, Dictionary<string, HLayerCtrl.HLayerInfo>>();

		public bool endHLoad;

		public GameObject HSceneSet;

		public GameObject HSceneUISet;

		public Dictionary<int, Dictionary<string, GameObject>> HPointPrefabs = new Dictionary<int, Dictionary<string, GameObject>>();

		public GameObject AutoHPointPrefabs;

		private StringBuilder sbAssetName = new StringBuilder();

		private StringBuilder sbAbName = new StringBuilder();

		private string hscenePrefabPath = "h/scene/";

		private readonly string[] assetNames = new string[6] { "aibu", "houshi", "sonyu", "tokushu", "les", "3P_F2M1" };

		private readonly string[,] strAssetAnimatorBase = new string[2, 3]
		{
			{ "animator/h/male/01/aibu.unity3d", "animator/h/male/01/houshi.unity3d", "animator/h/male/01/sonyu.unity3d" },
			{ "animator/h/female/01/aibu.unity3d", "animator/h/female/01/houshi.unity3d", "animator/h/female/01/sonyu.unity3d" }
		};

		private readonly string[,] racBaseNames = new string[2, 3]
		{
			{ "aia_m_base", "aih_m_base", "ais_m_base" },
			{ "aia_f_base", "aih_f_base", "ais_f_base" }
		};

		public HashSet<HAssetBundle>[] hashUseAssetBundle = new HashSet<HAssetBundle>[2]
		{
			new HashSet<HAssetBundle>(),
			new HashSet<HAssetBundle>()
		};

		public Dictionary<int, ParameterPacket> HBaseParamTable { get; private set; } = new Dictionary<int, ParameterPacket>();


		public Dictionary<int, ParameterPacket> HactionParamTable { get; private set; } = new Dictionary<int, ParameterPacket>();


		public Dictionary<int, Dictionary<int, float>> HSkileParamTable { get; private set; } = new Dictionary<int, Dictionary<int, float>>();


		public static Dictionary<string, int> HTagTable { get; } = new Dictionary<string, int>
		{
			["体温"] = 0,
			["機嫌"] = 1,
			["満腹"] = 2,
			["体調"] = 3,
			["生命"] = 4,
			["やる気"] = 5,
			["H"] = 6,
			["善悪"] = 7,
			["女子力"] = 10,
			["信頼"] = 11,
			["人間性"] = 12,
			["本能"] = 13,
			["変態"] = 14,
			["警戒"] = 15,
			["闇"] = 16,
			["社交"] = 17,
			["トイレ"] = 100,
			["風呂"] = 101,
			["睡眠"] = 102,
			["食事"] = 103,
			["休憩"] = 104,
			["ギフト"] = 105,
			["おねだり"] = 106,
			["寂しい"] = 107,
			["H欲"] = 108,
			["採取"] = 110,
			["遊び"] = 111,
			["料理"] = 112,
			["動物"] = 113,
			["ロケ"] = 114,
			["飲み物"] = 115,
			["ゲージ"] = -1
		};


		public IEnumerator LoadH()
		{
			hashUseAssetBundle[0] = new HashSet<HAssetBundle>();
			commonSpace = GameObject.Find("CommonSpace");
			yield return LoadHmesh();
			yield return LoadAnimationFileName();
			yield return LoadStartAnimationList();
			yield return LoadStartAnimationList(merchant: true);
			yield return LoadStartWaitAnim();
			yield return LoadEndAnimationList();
			LoadHitObject();
			yield return LoadHsceneParticle();
			yield return LoadHItemObjInfo();
			yield return LoadHYure();
			yield return LoadHYureMale();
			yield return LoadHPointInfo();
			yield return LoadAutoHPointPath();
			yield return LoadHItemBaseAnim();
			yield return LoadHAutoInfo();
			yield return LoadAutoLeaveItToYou();
			yield return LoadAutoLeaveItToYouPersonality();
			yield return LoadAutoLeaveItToYouAttribute();
			yield return LoadFeelHit();
			yield return LoadDankonList();
			CollisionLoadExcel();
			HitObjLoadExcel();
			HLayerLoadExcel();
			LoadHParamTable(0);
			LoadHParamTable(1);
			LoadHSkilParm();
			yield return LoadHPointList();
			foreach (HAssetBundle item in hashUseAssetBundle[0])
			{
				AssetBundleManager.UnloadAssetBundle(item.path, isUnloadForceRefCount: false, item.manifest);
			}
			endHLoad = true;
		}

		public void Release()
		{
			for (int i = 0; i < lstAnimInfo.Length; i++)
			{
				lstAnimInfo[i] = null;
			}
			lstStartAnimInfo.Clear();
			lstStartAnimInfoM.Clear();
			startWaitAnims.Clear();
			lstEndAnimInfo.Clear();
			lstHitObject.Clear();
			HitObjAtariName.Clear();
			DicLstHitObjInfo.Clear();
			foreach (KeyValuePair<int, Dictionary<int, Dictionary<string, GameObject>>> item in DicHitObject)
			{
				foreach (KeyValuePair<int, Dictionary<string, GameObject>> item2 in item.Value)
				{
					foreach (KeyValuePair<string, GameObject> item3 in item2.Value)
					{
						UnityEngine.Object.Destroy(item3.Value);
					}
				}
			}
			DicHitObject.Clear();
			for (int j = 0; j < lstHItemObjInfo.Length; j++)
			{
				lstHItemObjInfo[j] = null;
			}
			lstHItemBase.Clear();
			HitemPathList.Clear();
			if (HSceneSet != null)
			{
				HScene componentInChildren = HSceneSet.GetComponentInChildren<HScene>(includeInactive: true);
				if (componentInChildren != null)
				{
					componentInChildren.HParticleSetNull();
				}
			}
			hPointLists.Clear();
			foreach (Dictionary<string, GameObject> value in HPointPrefabs.Values)
			{
				foreach (GameObject value2 in value.Values)
				{
					UnityEngine.Object.Destroy(value2);
				}
				value.Clear();
			}
			HPointPrefabs.Clear();
			UnityEngine.Object.Destroy(AutoHPointPrefabs);
			AutoHPointPrefabs = null;
			autoHPointDatas = null;
			loadHPointDatas.Clear();
			AutoHpointData.Clear();
			if (HPointObj != null)
			{
				UnityEngine.Object.Destroy(HPointObj);
			}
			HPointObj = null;
			HAutoPathList.Clear();
			HAutoInfo = null;
			HAutoLeaveItToYou = null;
			autoLeavePersonalityRate.Clear();
			autoLeaveAttributeRate.Clear();
			aHsceneBGM = null;
			lstHParticleCtrl.Clear();
			hParticle.ReleaseObject();
			hParticle = null;
			for (int k = 0; k < HBaseRuntimeAnimatorControllers.GetLength(0); k++)
			{
				for (int l = 0; l < HBaseRuntimeAnimatorControllers.GetLength(1); l++)
				{
					HBaseRuntimeAnimatorControllers[k, l] = null;
				}
			}
			HmeshDictionary.Clear();
			HMeshObjDic.Clear();
			DicDicYure.Clear();
			DicDicYureMale.Clear();
			DicLstHitInfo.Clear();
			DicLstLookAtDan.Clear();
			DicLstCollisionInfo.Clear();
			LayerInfos.Clear();
			HBaseParamTable.Clear();
			HactionParamTable.Clear();
			HSkileParamTable.Clear();
			endHLoad = false;
		}

		public IEnumerator LoadHObj()
		{
			hashUseAssetBundle[1] = new HashSet<HAssetBundle>();
			yield return LoadHScene();
			yield return LoadHsceneBaseRAC();
			HparticleInit();
			HitObjListInit();
			int mapID = Singleton<Manager.Map>.Instance.MapID;
			ChangeMapHpoint(mapID);
			GC.Collect();
			foreach (HAssetBundle item in hashUseAssetBundle[1])
			{
				AssetBundleManager.UnloadAssetBundle(item.path, isUnloadForceRefCount: false, item.manifest);
			}
			yield return null;
		}

		private IEnumerator LoadAnimationFileName()
		{
			for (int i = 0; i < lstAnimInfo.Length; i++)
			{
				lstAnimInfo[i] = new List<HScene.AnimationListInfo>();
			}
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetAnimationInfoListFolder);
			pathList.Sort();
			sbAbName.Clear();
			excelData = null;
			bool bTokushu2 = false;
			bool bfemaleMulti2 = false;
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				for (int nAssetNameLoopCnt = 0; nAssetNameLoopCnt < assetNames.Length; nAssetNameLoopCnt++)
				{
					sbAssetName.Clear();
					sbAssetName.AppendFormat("{0}_{1}", assetNames[nAssetNameLoopCnt], System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
					bool exist = false;
					if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
					{
						yield return null;
						continue;
					}
					excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
					hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
					if (excelData == null)
					{
						yield return null;
						continue;
					}
					bTokushu2 = nAssetNameLoopCnt > 2;
					bfemaleMulti2 = nAssetNameLoopCnt > 3;
					int excelRowIdx = 1;
					while (excelRowIdx < excelData.MaxCell)
					{
						row = excelData.list[excelRowIdx++].list;
						int index = 0;
						HScene.AnimationListInfo info = new HScene.AnimationListInfo
						{
							nameAnimation = row.GetElement(index++)
						};
						if (!int.TryParse(row.GetElement(index++), out info.id))
						{
							continue;
						}
						if (bTokushu2)
						{
							info.assetpathBaseM = row.GetElement(index++);
							info.assetBaseM = row.GetElement(index++);
						}
						info.assetpathMale = row.GetElement(index++);
						info.fileMale = row.GetElement(index++);
						info.isMaleHitObject = row.GetElement(index++) == "1";
						info.fileMotionNeckMale = row.GetElement(index++);
						if (bTokushu2)
						{
							info.assetpathBaseF = row.GetElement(index++);
							info.assetBaseF = row.GetElement(index++);
						}
						info.assetpathFemale = row.GetElement(index++);
						info.fileFemale = row.GetElement(index++);
						info.isFemaleHitObject = row.GetElement(index++) == "1";
						info.fileMotionNeckFemale = row.GetElement(index++);
						if (nAssetNameLoopCnt == 4)
						{
							info.fileMotionNeckFemalePlayer = row.GetElement(index++);
						}
						if (bfemaleMulti2)
						{
							info.assetpathBaseF2 = row.GetElement(index++);
							info.assetBaseF2 = row.GetElement(index++);
							info.assetpathFemale2 = row.GetElement(index++);
							info.fileFemale2 = row.GetElement(index++);
							info.isFemaleHitObject2 = row.GetElement(index++) == "1";
							info.fileMotionNeckFemale2 = row.GetElement(index++);
						}
						int.TryParse(row.GetElement(index++), out info.ActionCtrl.Item1);
						int.TryParse(row.GetElement(index++), out info.ActionCtrl.Item2);
						string tmpbuf2 = row.GetElement(index++);
						if (tmpbuf2 != string.Empty)
						{
							string[] abuf2 = tmpbuf2.Split(',');
							string[] array = abuf2;
							foreach (string s in array)
							{
								int result = -1;
								if (int.TryParse(s, out result))
								{
									info.nPositons.Add(result);
								}
							}
						}
						tmpbuf2 = row.GetElement(index++);
						if (!tmpbuf2.IsNullOrEmpty())
						{
							string[] abuf2 = tmpbuf2.Split('/');
							string[] array2 = abuf2;
							foreach (string item in array2)
							{
								info.lstOffset.Add(item);
							}
						}
						info.isNeedItem = row.GetElement(index++) == "1";
						int.TryParse(row.GetElement(index++), out info.nDownPtn);
						int.TryParse(row.GetElement(index++), out info.nFaintnessLimit);
						if (!int.TryParse(row.GetElement(index++), out info.nIyaAction))
						{
							info.nIyaAction = 0;
						}
						info.bSleep = row.GetElement(index++) == "1";
						info.bMerchantMotion = row.GetElement(index++) == "1";
						int.TryParse(row.GetElement(index++), out info.nHentai);
						int.TryParse(row.GetElement(index++), out info.nPhase);
						int.TryParse(row.GetElement(index++), out info.nInitiativeFemale);
						int.TryParse(row.GetElement(index++), out info.nFemaleProclivity);
						int.TryParse(row.GetElement(index++), out info.nBackInitiativeID);
						tmpbuf2 = row.GetElement(index++);
						if (tmpbuf2 != string.Empty)
						{
							string[] abuf2 = tmpbuf2.Split(',');
							string[] array3 = abuf2;
							foreach (string text in array3)
							{
								if (text != string.Empty)
								{
									info.lstSystem.Add(int.Parse(text));
								}
							}
						}
						int.TryParse(row.GetElement(index++), out info.nMaleSon);
						int.TryParse(row.GetElement(index++), out info.nFemaleUpperCloths[0]);
						int.TryParse(row.GetElement(index++), out info.nFemaleLowerCloths[0]);
						int.TryParse(row.GetElement(index++), out info.nFemaleUpperCloths[1]);
						int.TryParse(row.GetElement(index++), out info.nFemaleLowerCloths[1]);
						int.TryParse(row.GetElement(index++), out info.nFeelHit);
						info.nameCamera = row.GetElement(index++);
						info.fileSiruPaste = row.GetElement(index++);
						if (nAssetNameLoopCnt == 5)
						{
							info.fileSiruPasteSecond = row.GetElement(index++);
						}
						info.fileSe = row.GetElement(index++);
						string element = row.GetElement(index++);
						if (element != string.Empty)
						{
							int.TryParse(element, out info.nShortBreahtPlay);
						}
						tmpbuf2 = row.GetElement(index++);
						if (tmpbuf2 != string.Empty)
						{
							string[] abuf2 = tmpbuf2.Split(',');
							string[] array4 = abuf2;
							foreach (string text2 in array4)
							{
								if (!(text2 == string.Empty))
								{
									info.hasVoiceCategory.Add(int.Parse(text2));
								}
							}
						}
						element = row.GetElement(index++);
						if (element != string.Empty)
						{
							int.TryParse(element, out info.nPromiscuity);
						}
						tmpbuf2 = row.GetElement(index);
						if (tmpbuf2 != string.Empty)
						{
							string[] abuf2 = tmpbuf2.Split(',');
							if (abuf2.Length == 2)
							{
								int.TryParse(abuf2[0], out info.nAnimListInfoID);
								int.TryParse(abuf2[1], out info.nBreathID);
							}
						}
						int num = CheckAnimationInfoList(lstAnimInfo[nAssetNameLoopCnt], info.id);
						if (num < 0)
						{
							lstAnimInfo[nAssetNameLoopCnt].Add(info);
						}
						else
						{
							lstAnimInfo[nAssetNameLoopCnt][num] = info;
						}
					}
					yield return null;
				}
				yield return null;
			}
		}

		private IEnumerator LoadStartAnimationList(bool merchant = false)
		{
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetStartAnimationListFolder);
			pathList.Sort();
			sbAbName.Clear();
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				sbAssetName.Clear();
				if (!merchant)
				{
					sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
				}
				else
				{
					sbAssetName.Append("merchant");
				}
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 1;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int index = 0;
					int result = -1;
					if (int.TryParse(row.GetElement(index++), out result))
					{
						int result2 = -1;
						int.TryParse(row.GetElement(index++), out result2);
						int result3 = -1;
						int.TryParse(row.GetElement(index++), out result3);
						int result4 = -1;
						int.TryParse(row.GetElement(index), out result4);
						UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion> info = new UnityEx.ValueTuple<HSceneManager.HEvent, int, HScene.StartMotion>((HSceneManager.HEvent)result, result2, new HScene.StartMotion(result3, result4));
						if (!merchant)
						{
							lstStartAnimInfo.Add(info);
						}
						else
						{
							lstStartAnimInfoM.Add(info);
						}
					}
				}
				yield return null;
			}
		}

		private IEnumerator LoadEndAnimationList()
		{
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetEndAnimationInfoFolder);
			pathList.Sort();
			sbAbName.Clear();
			string[] abNames = new string[5];
			string[] assetNames = new string[5];
			string[] stateNames = new string[5];
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				sbAssetName.Clear();
				sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 3;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int num = 0;
					int result = 0;
					int.TryParse(row.GetElement(num++), out result);
					for (int i = 0; i < 5; i++)
					{
						abNames[i] = row.GetElement(num++);
						assetNames[i] = row.GetElement(num++);
						stateNames[i] = row.GetElement(num++);
					}
					HScene.EndMotion info = new HScene.EndMotion(result, abNames, assetNames, stateNames);
					lstEndAnimInfo.Add(info);
				}
				yield return null;
			}
		}

		private IEnumerator LoadHItemObjInfo()
		{
			for (int i = 0; i < lstAnimInfo.Length; i++)
			{
				lstHItemObjInfo[i] = new List<Dictionary<int, List<HItemCtrl.ListItem>>>();
			}
			HitemPathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetHItemObjInfoListFolder);
			HitemPathList.Sort();
			sbAbName.Clear();
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < HitemPathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(HitemPathList[nLoopCnt]);
				for (int nAssetNameLoopCnt = 0; nAssetNameLoopCnt < assetNames.Length; nAssetNameLoopCnt++)
				{
					Dictionary<int, List<HItemCtrl.ListItem>> info = new Dictionary<int, List<HItemCtrl.ListItem>>();
					sbAssetName.Clear();
					sbAssetName.AppendFormat("{00}_{01}", assetNames[nAssetNameLoopCnt], System.IO.Path.GetFileNameWithoutExtension(HitemPathList[nLoopCnt]));
					bool exist = false;
					if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
					{
						yield return null;
						continue;
					}
					excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
					hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
					if (excelData == null)
					{
						yield return null;
						continue;
					}
					int excelRowIdx = 1;
					while (excelRowIdx < excelData.MaxCell)
					{
						row = excelData.list[excelRowIdx++].list;
						int num = 1;
						int result = 0;
						if (!int.TryParse(row[num++], out result))
						{
							continue;
						}
						if (!info.ContainsKey(result))
						{
							info.Add(result, new List<HItemCtrl.ListItem>());
						}
						HItemCtrl.ListItem listItem = new HItemCtrl.ListItem();
						if (!int.TryParse(row.GetElement(num++), out listItem.itemkind))
						{
							listItem.itemkind = -1;
						}
						if (!int.TryParse(row.GetElement(num++), out listItem.itemID))
						{
							listItem.itemID = -1;
						}
						listItem.nameManifest = row.GetElement(num++);
						string element = row.GetElement(num++);
						if (element == null || element == string.Empty)
						{
							break;
						}
						listItem.pathAssetObject = element;
						listItem.nameObject = row.GetElement(num++);
						listItem.pathAssetAnimatorBase = row.GetElement(num++);
						listItem.nameAnimatorBase = row.GetElement(num++);
						listItem.pathAssetAnimator = row.GetElement(num++);
						listItem.nameAnimator = row.GetElement(num++);
						do
						{
							HItemCtrl.ParentInfo parentInfo = new HItemCtrl.ParentInfo();
							int num2 = num;
							element = row.GetElement(num2 + 1);
							if (element == string.Empty)
							{
								break;
							}
							parentInfo.isParentMode = row.GetElement(num++) == "0";
							parentInfo.numToWhomParent = int.Parse(element);
							num++;
							parentInfo.nameParent = row.GetElement(num++);
							parentInfo.nameSelf = row.GetElement(num++);
							parentInfo.isParentScale = row.GetElement(num++) == "1";
							listItem.lstParent.Add(parentInfo);
						}
						while (num < row.Count);
						info[result].Add(listItem);
					}
					lstHItemObjInfo[nAssetNameLoopCnt].Add(info);
				}
			}
			yield return null;
		}

		private IEnumerator LoadHItemBaseAnim()
		{
			sbAbName.Clear();
			List<UnityEx.ValueTuple<string, string>> assetNames = new List<UnityEx.ValueTuple<string, string>>();
			List<Dictionary<int, List<HItemCtrl.ListItem>>>[] array = lstHItemObjInfo;
			UnityEx.ValueTuple<string, string> tmp = default(UnityEx.ValueTuple<string, string>);
			foreach (List<Dictionary<int, List<HItemCtrl.ListItem>>> categoryinfo in array)
			{
				foreach (Dictionary<int, List<HItemCtrl.ListItem>> sheat in categoryinfo)
				{
					foreach (List<HItemCtrl.ListItem> value in sheat.Values)
					{
						foreach (HItemCtrl.ListItem item in value)
						{
							tmp.Item1 = item.pathAssetAnimatorBase;
							tmp.Item2 = item.nameAnimatorBase;
							if (!assetNames.Contains(tmp))
							{
								assetNames.Add(tmp);
							}
						}
					}
					yield return null;
				}
				yield return null;
			}
			foreach (UnityEx.ValueTuple<string, string> asset in assetNames)
			{
				bool exist = false;
				if (GlobalMethod.AssetFileExist(asset.Item1, asset.Item2, string.Empty))
				{
					tmpHItemRuntimeAnimator = CommonLib.LoadAsset<RuntimeAnimatorController>(asset.Item1, asset.Item2, clone: false, string.Empty);
					lstHItemBase.Add(new UnityEx.ValueTuple<string, RuntimeAnimatorController>(asset.Item2, tmpHItemRuntimeAnimator));
					hashUseAssetBundle[0].Add(new HAssetBundle(asset.Item1, string.Empty));
					yield return null;
				}
			}
		}

		private IEnumerator LoadHPointList()
		{
			sbAbName.Clear();
			sbAbName.Append(Singleton<HSceneManager>.Instance.strAssetHpointPrefabListFolder);
			pathList = CommonLib.GetAssetBundleNameListFromPath(sbAbName.ToString());
			pathList.Sort();
			sbAbName.Clear();
			excelData = null;
			StringBuilder[] sbText = new StringBuilder[2]
			{
				new StringBuilder(),
				new StringBuilder()
			};
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				sbAssetName.Clear();
				sbAssetName.AppendFormat("HPointPrefab_{00}", System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 1;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int index = 0;
					int result = 0;
					if (int.TryParse(row[index++], out result))
					{
						HPointList.LoadInfo info = new HPointList.LoadInfo
						{
							Path = row[index++],
							Name = row[index++],
							Manifest = row[index]
						};
						if (!hPointListInfos.ContainsKey(result))
						{
							hPointListInfos.Add(result, info);
						}
						else
						{
							hPointListInfos[result] = info;
						}
					}
				}
				yield return null;
			}
		}

		public void LoadHpointPrefabs(int mapID)
		{
			if (hPointListInfos.Count == 0 || !hPointListInfos.TryGetValue(mapID, out var value))
			{
				return;
			}
			if (!hPointLists.ContainsKey(mapID))
			{
				if (HPointPrefabs == null)
				{
					HPointPrefabs = new Dictionary<int, Dictionary<string, GameObject>>();
				}
				if (!HPointPrefabs.ContainsKey(mapID))
				{
					HPointPrefabs.Add(mapID, new Dictionary<string, GameObject>());
				}
				if (!HPointPrefabs[mapID].ContainsKey(value.Path))
				{
					HPointPrefabs[mapID].Add(value.Name, null);
				}
				HPointPrefabs[mapID][value.Name] = CommonLib.LoadAsset<GameObject>(value.Path, value.Name, clone: true, value.Manifest);
				if (HPointPrefabs[mapID][value.Name] == null)
				{
					return;
				}
				GameObject gameObject = HPointPrefabs[mapID][value.Name];
				Transform parent = ((!(commonSpace == null)) ? commonSpace.transform : GameObject.Find("CommonSpace").transform);
				gameObject.transform.SetParent(parent, worldPositionStays: false);
				gameObject.transform.localPosition = Vector3.zero;
				gameObject.transform.localRotation = Quaternion.identity;
				hPointLists.Add(mapID, null);
				HPointList component = gameObject.GetComponent<HPointList>();
				if (component != null)
				{
					component.Init();
					if (hPointLists[mapID] == null)
					{
						hPointLists[mapID] = component;
					}
					else
					{
						if (hPointLists[mapID].lst == null)
						{
							hPointLists[mapID].lst = new Dictionary<int, List<HPoint>>();
						}
						foreach (KeyValuePair<int, List<HPoint>> item in component.lst)
						{
							if (!hPointLists[mapID].lst.ContainsKey(item.Key))
							{
								hPointLists[mapID].lst.Add(item.Key, item.Value);
							}
							else
							{
								hPointLists[mapID].lst[item.Key] = item.Value;
							}
						}
					}
				}
			}
			AssetBundleManager.UnloadAssetBundle(value.Path, isUnloadForceRefCount: false, value.Manifest);
		}

		private IEnumerator LoadHPointInfo()
		{
			sbAbName.Clear();
			sbAbName.Append(Singleton<HSceneManager>.Instance.strAssetHpointListFolder);
			sbAssetName.Clear();
			pathList = CommonLib.GetAssetBundleNameListFromPath(sbAbName.ToString());
			pathList.Sort();
			excelData = null;
			List<int>[] notList = new List<int>[6];
			UnityEx.ValueTuple<int, int> tmp = default(UnityEx.ValueTuple<int, int>);
			for (int i = 0; i < pathList.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[i]);
				sbAssetName.Clear();
				sbAssetName.Append("Hpoint_");
				sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[i]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 1;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int num = 0;
					int result = -1;
					if (!int.TryParse(row[num++], out result))
					{
						continue;
					}
					num++;
					if (!loadHPointDatas.ContainsKey(result))
					{
						loadHPointDatas.Add(result, new HPoint.HpointData());
					}
					int result2 = 0;
					int result3 = 0;
					int result4 = 0;
					int.TryParse(row[num++], out result2);
					int.TryParse(row[num++], out result3);
					int.TryParse(row[num++], out result4);
					tmp.Item1 = result2;
					tmp.Item2 = result4;
					for (int j = 0; j < 6; j++)
					{
						int num2 = j;
						notList[num2] = new List<int>();
						if (num >= row.Count)
						{
							continue;
						}
						string[] buf = row[num++].Split(',');
						for (int k = 0; k < buf.Length; k++)
						{
							int result5 = -1;
							if (int.TryParse(buf[k], out result5))
							{
								notList[num2].Add(result5);
							}
						}
					}
					if (!loadHPointDatas[result].place.ContainsKey(result3))
					{
						loadHPointDatas[result].place.Add(result3, tmp);
					}
					else
					{
						loadHPointDatas[result].place[result3] = tmp;
					}
					for (int l = 0; l < loadHPointDatas[result].notMotion.Length; l++)
					{
						if (loadHPointDatas[result].notMotion[l].motionID == null || loadHPointDatas[result].notMotion[l].motionID.Count == 0)
						{
							loadHPointDatas[result].notMotion[l].motionID = new List<int>(notList[l]);
						}
						else
						{
							loadHPointDatas[result].notMotion[l].motionID.AddRange(notList[l]);
						}
					}
				}
				GC.Collect();
				AssetBundleManager.UnloadAssetBundle(sbAbName.ToString(), isUnloadForceRefCount: true);
				yield return null;
			}
		}

		public IEnumerator LoadAutoHPointPath()
		{
			sbAbName.Clear();
			sbAbName.Append(Singleton<HSceneManager>.Instance.strAssetHpointListFolder);
			sbAssetName.Clear();
			pathList = CommonLib.GetAssetBundleNameListFromPath(sbAbName.ToString());
			pathList.Sort();
			excelData = null;
			UnityEx.ValueTuple<string, string> tmp = default(UnityEx.ValueTuple<string, string>);
			for (int i = 0; i < pathList.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[i]);
				sbAssetName.Clear();
				sbAssetName.Append("AutoHpoint_");
				sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[i]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 1;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int num = 0;
					tmp.Item1 = row[num++];
					tmp.Item2 = row[num++];
					int result = 0;
					if (row.Count >= num || !int.TryParse(row[num], out result))
					{
						result = 0;
					}
					if (!AutoHpointData.ContainsKey(result))
					{
						AutoHpointData.Add(result, new List<UnityEx.ValueTuple<string, string>>());
					}
					AutoHpointData[result].Add(tmp);
				}
			}
			yield return null;
		}

		public void LoadAutoHPoint(int mapID)
		{
			pathList.Clear();
			pathList = CommonLib.GetAssetBundleNameListFromPath("list/h/hpoint/prefab/");
			autoHPointDatas = new Dictionary<int, AutoHPointData>();
			GameObject gameObject = null;
			for (int i = 0; i < pathList.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[i]);
				bool flag = false;
				if (GlobalMethod.AssetFileExist(sbAbName.ToString(), "Hpointobj", string.Empty))
				{
					if (HPointObj != null)
					{
						UnityEngine.Object.Destroy(HPointObj);
						HPointObj = null;
					}
					gameObject = CommonLib.LoadAsset<GameObject>(sbAbName.ToString(), "Hpointobj", clone: false, string.Empty);
					hashUseAssetBundle[1].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				}
			}
			Transform parent = ((!(commonSpace == null)) ? commonSpace.transform : GameObject.Find("CommonSpace").transform);
			if (HPointObj == null && gameObject != null)
			{
				HPointObj = UnityEngine.Object.Instantiate(gameObject, parent);
				HPointObj.GetComponent<HPoint>().Init();
			}
			if (gameObject == null || !AutoHpointData.ContainsKey(mapID))
			{
				return;
			}
			if (AutoHPointPrefabs == null)
			{
				AutoHPointPrefabs = new GameObject("AutoHpoints");
			}
			GameObject autoHPointPrefabs = AutoHPointPrefabs;
			autoHPointPrefabs.transform.SetParent(parent, worldPositionStays: false);
			autoHPointPrefabs.transform.localPosition = Vector3.zero;
			autoHPointPrefabs.transform.localRotation = Quaternion.identity;
			for (int j = 0; j < AutoHpointData[mapID].Count; j++)
			{
				bool flag2 = false;
				if (!GlobalMethod.AssetFileExist(AutoHpointData[mapID][j].Item1, AutoHpointData[mapID][j].Item2, string.Empty))
				{
					continue;
				}
				AutoHPointData autoHPointData = CommonLib.LoadAsset<AutoHPointData>(AutoHpointData[mapID][j].Item1, AutoHpointData[mapID][j].Item2, clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(AutoHpointData[mapID][j].Item1, string.Empty));
				if (autoHPointData == null)
				{
					continue;
				}
				if (!autoHPointDatas.ContainsKey(mapID))
				{
					autoHPointDatas.Add(mapID, autoHPointData);
				}
				else
				{
					autoHPointDatas[mapID] = autoHPointData;
				}
				if (autoHPointData.Points == null)
				{
					continue;
				}
				foreach (KeyValuePair<string, List<UnityEx.ValueTuple<int, Vector3>>> point in autoHPointData.Points)
				{
					int num = AutoHpointID(point.Key);
					if (num < 0)
					{
						continue;
					}
					foreach (UnityEx.ValueTuple<int, Vector3> item in point.Value)
					{
						GameObject gameObject2 = UnityEngine.Object.Instantiate(gameObject);
						gameObject2.transform.SetParent(autoHPointPrefabs.transform, worldPositionStays: false);
						gameObject2.transform.position = item.Item2;
						gameObject2.transform.rotation = Quaternion.identity;
						gameObject2.GetComponent<HPoint>().id = num;
						if (!hPointLists.ContainsKey(mapID))
						{
							hPointLists.Add(mapID, new GameObject("HPointLists").AddComponent<HPointList>());
							hPointLists[mapID].lst = new Dictionary<int, List<HPoint>>();
						}
						if (!hPointLists[mapID].lst.ContainsKey(item.Item1))
						{
							hPointLists[mapID].lst.Add(item.Item1, new List<HPoint>());
						}
						hPointLists[mapID].lst[item.Item1].Add(gameObject2.GetComponent<HPoint>());
					}
				}
			}
		}

		private void HPointInitData(int mapID)
		{
			foreach (KeyValuePair<int, List<HPoint>> item in hPointLists[mapID].lst)
			{
				foreach (HPoint item2 in item.Value)
				{
					item2.Init();
				}
			}
		}

		private IEnumerator LoadHsceneParticle()
		{
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetHParticleListFolder);
			pathList.Sort();
			sbAbName.Clear();
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				sbAssetName.Clear();
				sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 1;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int index = 1;
					HParticleCtrl.ParticleInfo info = new HParticleCtrl.ParticleInfo
					{
						assetPath = row.GetElement(index++),
						file = row.GetElement(index++),
						manifest = row.GetElement(index++),
						numParent = int.Parse(row.GetElement(index++)),
						nameParent = row.GetElement(index++),
						pos = new Vector3(float.Parse(row.GetElement(index++)), float.Parse(row.GetElement(index++)), float.Parse(row.GetElement(index++))),
						rot = new Vector3(float.Parse(row.GetElement(index++)), float.Parse(row.GetElement(index++)), float.Parse(row.GetElement(index)))
					};
					lstHParticleCtrl.Add(info);
				}
			}
		}

		private IEnumerator LoadHsceneBaseRAC()
		{
			for (int nSexLoopCnt = 0; nSexLoopCnt < HBaseRuntimeAnimatorControllers.GetLength(0); nSexLoopCnt++)
			{
				for (int nCategoryLoopCnt = 0; nCategoryLoopCnt < HBaseRuntimeAnimatorControllers.GetLength(1); nCategoryLoopCnt++)
				{
					bool exist = false;
					if (!GlobalMethod.AssetFileExist(strAssetAnimatorBase[nSexLoopCnt, nCategoryLoopCnt], racBaseNames[nSexLoopCnt, nCategoryLoopCnt], string.Empty))
					{
						yield return null;
						continue;
					}
					HBaseRuntimeAnimatorControllers[nSexLoopCnt, nCategoryLoopCnt] = CommonLib.LoadAsset<RuntimeAnimatorController>(strAssetAnimatorBase[nSexLoopCnt, nCategoryLoopCnt], racBaseNames[nSexLoopCnt, nCategoryLoopCnt], clone: false, string.Empty);
					yield return null;
					AssetBundleManager.UnloadAssetBundle(strAssetAnimatorBase[nSexLoopCnt, nCategoryLoopCnt], isUnloadForceRefCount: false);
				}
			}
		}

		private IEnumerator LoadHAutoInfo()
		{
			sbAssetName.Clear();
			sbAssetName.Append("hAuto");
			excelData = null;
			bool exist = false;
			if (!GlobalMethod.AssetFileExist(Singleton<HSceneManager>.Instance.strAssetHAutoListFolder, sbAssetName.ToString(), string.Empty))
			{
				yield break;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(Singleton<HSceneManager>.Instance.strAssetHAutoListFolder, sbAssetName.ToString(), clone: false, string.Empty);
			hashUseAssetBundle[0].Add(new HAssetBundle(Singleton<HSceneManager>.Instance.strAssetHAutoListFolder, string.Empty));
			if (!(excelData == null))
			{
				HAutoInfo = new HAutoCtrl.HAutoInfo();
				int excelRowIdx = 2;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int c = 0;
					HAutoInfo.start.minmax = new Vector2(float.Parse(row.GetElement(c++)), float.Parse(row.GetElement(c++)));
					HAutoInfo.start.time = UnityEngine.Random.Range(HAutoInfo.start.minmax.x, HAutoInfo.start.minmax.y);
					HAutoInfo.reStart.minmax = new Vector2(float.Parse(row.GetElement(c++)), float.Parse(row.GetElement(c++)));
					HAutoInfo.reStart.time = UnityEngine.Random.Range(HAutoInfo.reStart.minmax.x, HAutoInfo.reStart.minmax.y);
					HAutoInfo.speed.minmax = new Vector2(float.Parse(row.GetElement(c++)), float.Parse(row.GetElement(c++)));
					HAutoInfo.speed.time = UnityEngine.Random.Range(HAutoInfo.speed.minmax.x, HAutoInfo.speed.minmax.y);
					HAutoInfo.lerpTimeSpeed = float.Parse(row.GetElement(c++));
					HAutoInfo.loopChange.minmax = new Vector2(float.Parse(row.GetElement(c++)), float.Parse(row.GetElement(c++)));
					HAutoInfo.loopChange.time = UnityEngine.Random.Range(HAutoInfo.loopChange.minmax.x, HAutoInfo.loopChange.minmax.y);
					HAutoInfo.motionChange.minmax = new Vector2(float.Parse(row.GetElement(c++)), float.Parse(row.GetElement(c++)));
					HAutoInfo.motionChange.time = UnityEngine.Random.Range(HAutoInfo.motionChange.minmax.x, HAutoInfo.motionChange.minmax.y);
					HAutoInfo.rateWeakLoop = int.Parse(row.GetElement(c++));
					HAutoInfo.rateHit = int.Parse(row.GetElement(c++));
					HAutoInfo.rateAddMotionChange = float.Parse(row.GetElement(c++));
					HAutoInfo.rateRestartMotionChange = int.Parse(row.GetElement(c++));
					float time = float.Parse(row.GetElement(c++));
					HAutoInfo.pull.minmax = new Vector2(time, time);
					HAutoInfo.pull.time = UnityEngine.Random.Range(HAutoInfo.motionChange.minmax.x, HAutoInfo.motionChange.minmax.y);
					HAutoInfo.rateInsertPull = float.Parse(row.GetElement(c++));
					HAutoInfo.rateNotInsertPull = float.Parse(row.GetElement(c++));
					yield return null;
				}
				GC.Collect();
			}
		}

		private IEnumerator LoadAutoLeaveItToYou()
		{
			HAutoPathList = new List<string>();
			HAutoPathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetLeaveItToYouFolder);
			HAutoPathList.Sort();
			sbAbName.Clear();
			sbAssetName.Clear();
			sbAssetName.Append("LeaveItToYou");
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < HAutoPathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(HAutoPathList[nLoopCnt]);
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				HAutoLeaveItToYou = new HAutoCtrl.AutoLeaveItToYou();
				int excelRowIdx = 2;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int index = 0;
					HAutoLeaveItToYou.time.minmax = new Vector2(float.Parse(row.GetElement(index++)), float.Parse(row.GetElement(index++)));
					HAutoLeaveItToYou.time.Reset();
					HAutoLeaveItToYou.baseTime = HAutoLeaveItToYou.time.minmax;
					HAutoLeaveItToYou.rate = int.Parse(row.GetElement(index));
				}
				yield return null;
			}
			GC.Collect();
		}

		private IEnumerator LoadAutoLeaveItToYouPersonality()
		{
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < HAutoPathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(HAutoPathList[nLoopCnt]);
				sbAssetName.Clear();
				sbAssetName.AppendFormat("LeaveItToYou_personality_{00}", System.IO.Path.GetFileNameWithoutExtension(HAutoPathList[nLoopCnt]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(sbAbName.ToString());
				if (excelData == null)
				{
					continue;
				}
				int excelRowIdx = 1;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int index = 1;
					int result = 0;
					if (int.TryParse(row.GetElement(index++), out result))
					{
						float result2 = 1f;
						if (!float.TryParse(row.GetElement(index), out result2))
						{
							result2 = 1f;
						}
						if (!autoLeavePersonalityRate.ContainsKey(result))
						{
							autoLeavePersonalityRate.Add(result, result2);
						}
						else
						{
							autoLeavePersonalityRate[result] = result2;
						}
					}
				}
				yield return null;
			}
		}

		private IEnumerator LoadAutoLeaveItToYouAttribute()
		{
			sbAbName.Clear();
			sbAssetName.Clear();
			sbAssetName.Append("LeaveItToYou_attribute");
			excelData = null;
			for (int i = 0; i < HAutoPathList.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(HAutoPathList[i]);
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(sbAbName.ToString());
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				while (num < excelData.MaxCell)
				{
					row = excelData.list[num++].list;
					int index = 1;
					if (int.TryParse(row.GetElement(index++), out var result))
					{
						float result2 = 1f;
						if (!float.TryParse(row.GetElement(index), out result2))
						{
							result2 = 1f;
						}
						if (!autoLeaveAttributeRate.ContainsKey(result))
						{
							autoLeaveAttributeRate.Add(result, result2);
						}
						else
						{
							autoLeaveAttributeRate[result] = result2;
						}
					}
				}
			}
			yield return null;
		}

		private IEnumerator LoadHmesh()
		{
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.HmeshListFolder);
			pathList.Sort();
			sbAbName.Clear();
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				sbAssetName.Clear();
				sbAssetName.Append("hmesh_");
				sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 1;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					int c = 0;
					int mapID = 0;
					if (!int.TryParse(row[c++], out mapID))
					{
						yield return null;
						continue;
					}
					hMeshInfo.mapID = mapID;
					hMeshInfo.abName = row[c++];
					hMeshInfo.assetName = row[c++];
					hMeshInfo.manifest = row[c++];
					if (!HmeshDictionary.ContainsKey(nLoopCnt))
					{
						HmeshDictionary.Add(nLoopCnt, new Dictionary<int, HmeshInfo>());
					}
					if (HmeshDictionary[nLoopCnt].ContainsKey(mapID))
					{
						HmeshDictionary[nLoopCnt].Add(mapID, hMeshInfo);
					}
					else
					{
						HmeshDictionary[nLoopCnt][mapID] = hMeshInfo;
					}
				}
				yield return null;
			}
		}

		private IEnumerator LoadStartWaitAnim()
		{
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetStartWaitAnimListFolder);
			pathList.Sort();
			sbAbName.Clear();
			excelData = null;
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				sbAssetName.Clear();
				sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 2;
				while (excelRowIdx < excelData.MaxCell)
				{
					row = excelData.list[excelRowIdx++].list;
					StartWaitAnim info = new StartWaitAnim();
					int c = 0;
					int ID = -1;
					if (!int.TryParse(row[c++], out ID))
					{
						yield return null;
						continue;
					}
					info.ID = ID;
					if (row.Count != c)
					{
						info.CameraFile = row[c++];
						int result = 0;
						if (row.Count == c || !int.TryParse(row[c++], out result))
						{
							result = 0;
						}
						info.VisibleMode = result;
						info.Player[0].abName = row[c++];
						info.Player[0].assetName = row[c++];
						info.Player[0].State = row[c++];
						info.Player[1].abName = row[c++];
						info.Player[1].assetName = row[c++];
						info.Player[1].State = row[c++];
						info.Agent[0].abName = row[c++];
						info.Agent[0].assetName = row[c++];
						info.Agent[0].State = row[c++];
						if (row.Count > c)
						{
							info.Agent[1].abName = row[c++];
							info.Agent[1].assetName = row[c++];
							info.Agent[1].State = row[c++];
						}
					}
					else
					{
						info.Player[0].abName = string.Empty;
						info.Player[0].assetName = string.Empty;
						info.Player[0].State = string.Empty;
						info.Player[1].abName = string.Empty;
						info.Player[1].assetName = string.Empty;
						info.Player[1].State = string.Empty;
						info.Agent[0].abName = string.Empty;
						info.Agent[0].assetName = string.Empty;
						info.Agent[0].State = string.Empty;
						info.Agent[1].abName = string.Empty;
						info.Agent[1].assetName = string.Empty;
						info.Agent[1].State = string.Empty;
						info.CameraFile = string.Empty;
						info.VisibleMode = 0;
					}
					startWaitAnims.Add(info);
				}
				yield return null;
			}
		}

		public IEnumerator LoadHYure()
		{
			int[] idxBust = new int[7] { 2, 3, 4, 5, 6, 7, 8 };
			pathList.Clear();
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetYureListFolder);
			pathList.Sort();
			excelData = null;
			for (int i = 0; i < 6; i++)
			{
				int key = i;
				Dictionary<int, List<YureCtrl.Info>> dictionary = new Dictionary<int, List<YureCtrl.Info>>();
				for (int j = 0; j < pathList.Count; j++)
				{
					sbAbName.Clear();
					sbAssetName.Clear();
					sbAbName.Append(pathList[j]);
					switch (i)
					{
					case 0:
						sbAssetName.AppendFormat("aia_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 1:
						sbAssetName.AppendFormat("aih_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 2:
						sbAssetName.AppendFormat("ais_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 3:
						sbAssetName.AppendFormat("ait_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 4:
						sbAssetName.AppendFormat("ail_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 5:
						sbAssetName.AppendFormat("ai3p_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					}
					if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
					{
						continue;
					}
					excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
					hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
					if (excelData == null)
					{
						continue;
					}
					int num = 1;
					while (num < excelData.MaxCell)
					{
						row = excelData.list[num++].list;
						int index = 0;
						int result = -1;
						if (int.TryParse(row.GetElement(index++), out result))
						{
							YureCtrl.Info info = new YureCtrl.Info();
							int result2 = 0;
							if (i > 3 && !int.TryParse(row.GetElement(index++), out result2))
							{
								result2 = 0;
							}
							info.nFemale = result2;
							info.nameAnimation = row.GetElement(index++);
							info.aIsActive[0] = row.GetElement(index++) == "1";
							info.aBreastShape[0].MemberInit();
							for (int k = 0; k < idxBust.Length; k++)
							{
								info.aBreastShape[0].breast[k] = row.GetElement(index++) == "1";
							}
							info.aBreastShape[0].nip = row.GetElement(index++) == "1";
							info.aIsActive[1] = row.GetElement(index++) == "1";
							info.aBreastShape[1].MemberInit();
							for (int l = 0; l < idxBust.Length; l++)
							{
								info.aBreastShape[1].breast[l] = row.GetElement(index++) == "1";
							}
							info.aBreastShape[1].nip = row.GetElement(index++) == "1";
							info.aIsActive[2] = row.GetElement(index++) == "1";
							info.aIsActive[3] = row.GetElement(index) == "1";
							if (!dictionary.ContainsKey(result))
							{
								dictionary.Add(result, new List<YureCtrl.Info>());
							}
							dictionary[result].Add(info);
						}
					}
				}
				if (!DicDicYure.ContainsKey(key))
				{
					DicDicYure.Add(key, dictionary);
				}
				DicDicYure[key] = dictionary;
			}
			yield return null;
		}

		public IEnumerator LoadHYureMale()
		{
			int[] idxBust = new int[7] { 2, 3, 4, 5, 6, 7, 8 };
			pathList.Clear();
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetYureListFolder);
			pathList.Sort();
			excelData = null;
			for (int i = 0; i < 6; i++)
			{
				int key = i;
				Dictionary<int, List<YureCtrlMale.Info>> dictionary = new Dictionary<int, List<YureCtrlMale.Info>>();
				for (int j = 0; j < pathList.Count; j++)
				{
					if (i == 4)
					{
						continue;
					}
					sbAbName.Clear();
					sbAssetName.Clear();
					sbAbName.Append(pathList[j]);
					switch (i)
					{
					case 0:
						sbAssetName.AppendFormat("aia_m_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 1:
						sbAssetName.AppendFormat("aih_m_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 2:
						sbAssetName.AppendFormat("ais_m_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 3:
						sbAssetName.AppendFormat("ait_m_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					case 5:
						sbAssetName.AppendFormat("ai3p_m_{0:00}", System.IO.Path.GetFileNameWithoutExtension(pathList[j]));
						break;
					}
					if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
					{
						continue;
					}
					excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
					hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
					if (excelData == null)
					{
						continue;
					}
					int num = 1;
					while (num < excelData.MaxCell)
					{
						row = excelData.list[num++].list;
						int index = 0;
						int result = -1;
						if (int.TryParse(row.GetElement(index++), out result))
						{
							YureCtrlMale.Info info = new YureCtrlMale.Info
							{
								nMale = 0,
								nameAnimation = row.GetElement(index++)
							};
							info.aIsActive[0] = row.GetElement(index++) == "1";
							info.aBreastShape[0].MemberInit();
							for (int k = 0; k < idxBust.Length; k++)
							{
								info.aBreastShape[0].breast[k] = row.GetElement(index++) == "1";
							}
							info.aBreastShape[0].nip = row.GetElement(index++) == "1";
							info.aIsActive[1] = row.GetElement(index++) == "1";
							info.aBreastShape[1].MemberInit();
							for (int l = 0; l < idxBust.Length; l++)
							{
								info.aBreastShape[1].breast[l] = row.GetElement(index++) == "1";
							}
							info.aBreastShape[1].nip = row.GetElement(index++) == "1";
							info.aIsActive[2] = row.GetElement(index++) == "1";
							info.aIsActive[3] = row.GetElement(index) == "1";
							if (!dictionary.ContainsKey(result))
							{
								dictionary.Add(result, new List<YureCtrlMale.Info>());
							}
							dictionary[result].Add(info);
						}
					}
				}
				if (!DicDicYure.ContainsKey(key))
				{
					DicDicYureMale.Add(key, dictionary);
				}
				DicDicYureMale[key] = dictionary;
			}
			yield return null;
		}

		private IEnumerator LoadHScene()
		{
			LoadHSceneSet();
			yield return null;
			LoadHSceneUISet();
			yield return null;
		}

		private IEnumerator LoadFeelHit()
		{
			List<string> pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetFeelHitListFolder);
			pathList.Sort();
			int[] c = new int[2] { 1, 2 };
			string abName = string.Empty;
			excelData = null;
			List<string>[] rowFeel = new List<string>[2]
			{
				new List<string>(),
				new List<string>()
			};
			string[][] Initloop = new string[3][];
			FeelHit.FeelHitInfo hitInfo = default(FeelHit.FeelHitInfo);
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				abName = pathList[nLoopCnt];
				sbAssetName.Clear();
				sbAssetName.Append("FeelHit_");
				sbAssetName.Append(System.IO.Path.GetFileNameWithoutExtension(pathList[nLoopCnt]));
				bool exist = false;
				if (!GlobalMethod.AssetFileExist(abName, sbAssetName.ToString(), string.Empty))
				{
					yield return null;
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(abName, sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(abName, string.Empty));
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				int excelRowIdx = 2;
				while (excelRowIdx < excelData.MaxCell)
				{
					for (int i = 0; i < rowFeel.Length; i++)
					{
						rowFeel[i] = excelData.list[excelRowIdx++].list;
					}
					c[0] = 1;
					c[1] = 2;
					int personality = -1;
					if (!int.TryParse(rowFeel[0].GetElement(c[0]++), out personality))
					{
						continue;
					}
					if (!DicLstHitInfo.ContainsKey(personality))
					{
						DicLstHitInfo.Add(personality, new List<FeelHit.FeelInfo>());
					}
					FeelHit.FeelInfo info = new FeelHit.FeelInfo();
					int nX = rowFeel[0].Count;
					for (int x = 2; x < nX; x += 3)
					{
						for (int j = 0; j < 3; j++)
						{
							Initloop[j] = rowFeel[0].GetElement(c[0]++).Split('/');
							hitInfo.area = new Vector2(float.Parse(Initloop[j][0]), float.Parse(Initloop[j][1]));
							hitInfo.rate = float.Parse(rowFeel[1].GetElement(c[1]++));
							info.lstHitArea.Add(hitInfo);
						}
						DicLstHitInfo[personality].Add(info);
						yield return null;
					}
				}
			}
		}

		private IEnumerator LoadDankonList()
		{
			List<string> pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetDankonListFolder);
			pathList.Sort();
			sbAbName.Clear();
			sbAssetName.Clear();
			excelData = null;
			H_Lookat_dan.ShapeInfo info = default(H_Lookat_dan.ShapeInfo);
			for (int nLoopCnt = 0; nLoopCnt < pathList.Count; nLoopCnt++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[nLoopCnt]);
				for (int nCategory = 0; nCategory < lstAnimInfo.Length; nCategory++)
				{
					for (int i = 0; i < lstAnimInfo[nCategory].Count; i++)
					{
						if (lstAnimInfo[nCategory][i].fileMale.IsNullOrEmpty())
						{
							continue;
						}
						sbAssetName.Clear();
						sbAssetName.Append(lstAnimInfo[nCategory][i].fileMale);
						sbAssetName.Replace("_m_", "_");
						if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
						{
							continue;
						}
						excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
						hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
						if (excelData == null)
						{
							continue;
						}
						if (!DicLstLookAtDan.ContainsKey(sbAssetName.ToString()))
						{
							DicLstLookAtDan.Add(sbAssetName.ToString(), new List<H_Lookat_dan.MotionLookAtList>());
						}
						int num = 3;
						while (num < excelData.MaxCell)
						{
							row = excelData.list[num++].list;
							H_Lookat_dan.MotionLookAtList look = new H_Lookat_dan.MotionLookAtList();
							int num2 = 0;
							look.strMotion = row.GetElement(num2++);
							int result = 0;
							if (int.TryParse(row.GetElement(num2++), out result))
							{
								look.numFemale = result;
							}
							look.strLookAtNull = row.GetElement(num2++);
							look.bTopStick = row.GetElement(num2++) == "1";
							look.bManual = row.GetElement(num2++) == "1";
							int num3 = 0;
							for (int j = num2; j < row.Count; j += 10)
							{
								int num4 = 0;
								int result2 = 0;
								if (!int.TryParse(row.GetElement(j + num4++), out result2))
								{
									break;
								}
								info.shape = result2;
								info.minPos = new Vector3(float.Parse(row.GetElement(j + num4++)), float.Parse(row.GetElement(j + num4++)), float.Parse(row.GetElement(j + num4++)));
								info.middlePos = new Vector3(float.Parse(row.GetElement(j + num4++)), float.Parse(row.GetElement(j + num4++)), float.Parse(row.GetElement(j + num4++)));
								info.maxPos = new Vector3(float.Parse(row.GetElement(j + num4++)), float.Parse(row.GetElement(j + num4++)), float.Parse(row.GetElement(j + num4)));
								info.bUse = true;
								look.lstShape[num3++] = info;
							}
							if (DicLstLookAtDan[sbAssetName.ToString()] == null)
							{
								DicLstLookAtDan[sbAssetName.ToString()] = new List<H_Lookat_dan.MotionLookAtList>();
							}
							DicLstLookAtDan[sbAssetName.ToString()].Add(look);
						}
					}
					yield return null;
				}
			}
		}

		public void LoadHParamTable(int mode)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetParam);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string text2 = string.Empty;
				switch (mode)
				{
				case 0:
					text2 = "base_" + System.IO.Path.GetFileNameWithoutExtension(text);
					break;
				case 1:
					text2 = "hresult_" + System.IO.Path.GetFileNameWithoutExtension(text);
					break;
				}
				bool flag = false;
				if (!GlobalMethod.AssetFileExist(text, text2, string.Empty))
				{
					continue;
				}
				ExcelData excelData = CommonLib.LoadAsset<ExcelData>(text, text2, clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(text, string.Empty);
				int num = 1;
				num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 1;
					string element = param.list.GetElement(num2++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					ParameterPacket parameterPacket = new ParameterPacket();
					while (num2 < param.list.Count)
					{
						string element2 = param.list.GetElement(num2++);
						string element3 = param.list.GetElement(num2++);
						string element4 = param.list.GetElement(num2++);
						string element5 = param.list.GetElement(num2++);
						if (!element2.IsNullOrEmpty())
						{
							if (!HTagTable.TryGetValue(element2, out var value))
							{
								Debug.LogWarning($"タグ読み取りエラー: 値={element2}");
								continue;
							}
							int result2;
							int s = (int.TryParse(element3, out result2) ? result2 : 0);
							int m = (int.TryParse(element4, out result2) ? result2 : 0);
							int l = (int.TryParse(element5, out result2) ? result2 : 0);
							parameterPacket.Parameters[value] = new TriThreshold(s, m, l);
						}
					}
					switch (mode)
					{
					case 0:
						HBaseParamTable[result] = parameterPacket;
						break;
					case 1:
						HactionParamTable[result] = parameterPacket;
						break;
					}
				}
			}
		}

		public void LoadHSkilParm()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetParam);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string text2 = "hskil_" + System.IO.Path.GetFileNameWithoutExtension(text);
				bool flag = false;
				if (!GlobalMethod.AssetFileExist(text, text2, string.Empty))
				{
					continue;
				}
				ExcelData excelData = CommonLib.LoadAsset<ExcelData>(text, text2, clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(text, string.Empty);
				int num = 1;
				num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 0;
					string element = param.list.GetElement(num2++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					num2++;
					Dictionary<int, float> dictionary = new Dictionary<int, float>();
					while (num2 < param.list.Count)
					{
						string element2 = param.list.GetElement(num2++);
						string element3 = param.list.GetElement(num2++);
						if (element2.IsNullOrEmpty())
						{
							continue;
						}
						if (!HTagTable.TryGetValue(element2, out var value))
						{
							Debug.LogWarning($"タグ読み取りエラー: 値={element2}");
							continue;
						}
						if (!float.TryParse(element3, out var result2))
						{
							result2 = 0f;
						}
						if (!dictionary.ContainsKey(value))
						{
							dictionary.Add(value, 0f);
						}
						dictionary[value] = result2;
					}
					HSkileParamTable[result] = dictionary;
				}
			}
		}

		private void LoadHSceneSet()
		{
			if (HSceneSet != null)
			{
				return;
			}
			sbAbName.Clear();
			StringBuilder stringBuilder = new StringBuilder();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(hscenePrefabPath);
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				stringBuilder.Clear();
				stringBuilder.AppendFormat("add{0:00}", System.IO.Path.GetFileNameWithoutExtension(assetBundleNameListFromPath[i]));
				if (GlobalMethod.AssetFileExist(assetBundleNameListFromPath[i], "HSceneSet", stringBuilder.ToString()))
				{
					sbAbName.Clear();
					sbAbName.Append(assetBundleNameListFromPath[i]);
				}
			}
			stringBuilder.Clear();
			stringBuilder.AppendFormat("add{0:00}", System.IO.Path.GetFileNameWithoutExtension(sbAbName.ToString()));
			HSceneSet = CommonLib.LoadAsset<GameObject>(sbAbName.ToString(), "HSceneSet", clone: true, stringBuilder.ToString());
			hashUseAssetBundle[1].Add(new HAssetBundle(sbAbName.ToString(), stringBuilder.ToString()));
			Transform parent = ((!(commonSpace == null)) ? commonSpace.transform : GameObject.Find("CommonSpace").transform);
			HSceneSet.transform.SetParent(parent, worldPositionStays: false);
			HSceneSet.transform.localPosition = Vector3.zero;
			HSceneSet.transform.localRotation = Quaternion.identity;
			HSceneSet.GetComponentInChildren<HSceneFlagCtrl>().MapHVoiceInit();
		}

		private void LoadHSceneUISet()
		{
			if (HSceneUISet != null)
			{
				return;
			}
			sbAbName.Clear();
			StringBuilder stringBuilder = new StringBuilder();
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(hscenePrefabPath);
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				stringBuilder.Clear();
				stringBuilder.AppendFormat("add{0:00}", System.IO.Path.GetFileNameWithoutExtension(assetBundleNameListFromPath[i]));
				if (GlobalMethod.AssetFileExist(assetBundleNameListFromPath[i], "HSceneUISet", stringBuilder.ToString()))
				{
					sbAbName.Clear();
					sbAbName.Append(assetBundleNameListFromPath[i]);
				}
			}
			stringBuilder.Clear();
			stringBuilder.AppendFormat("add{0:00}", System.IO.Path.GetFileNameWithoutExtension(sbAbName.ToString()));
			HSceneUISet = CommonLib.LoadAsset<GameObject>(sbAbName.ToString(), "HSceneUISet", clone: true, stringBuilder.ToString());
			hashUseAssetBundle[1].Add(new HAssetBundle(sbAbName.ToString(), stringBuilder.ToString()));
			Transform parent = ((!(commonSpace == null)) ? commonSpace.transform : GameObject.Find("CommonSpace").transform);
			HSceneUISet.transform.SetParent(parent, worldPositionStays: false);
			HSceneUISet.transform.localPosition = Vector3.zero;
			HSceneUISet.transform.localRotation = Quaternion.identity;
		}

		private bool GetIntMember(string[,] _str, int _y, ref int _line, ref int _member)
		{
			int length = _str.GetLength(1);
			if (length <= _line)
			{
				return false;
			}
			string text = _str[_y, _line++];
			if (!text.IsNullOrEmpty())
			{
				_member = int.Parse(text);
			}
			return true;
		}

		public void SetHmesh(Transform parent)
		{
			HMeshObjDic.Clear();
			GameObject mesh;
			foreach (KeyValuePair<int, Dictionary<int, HmeshInfo>> item in HmeshDictionary)
			{
				int mapID = Singleton<Manager.Map>.Instance.MapID;
				if (!item.Value.TryGetValue(mapID, out var value))
				{
					continue;
				}
				mesh = CommonLib.LoadAsset<GameObject>(value.abName, value.assetName, clone: true, value.manifest);
				hashUseAssetBundle[0].Add(new HAssetBundle(value.abName, value.manifest));
				mesh.transform.SetParent(parent, worldPositionStays: false);
				mesh.transform.localPosition = Vector3.zero;
				mesh.transform.localRotation = Quaternion.identity;
				HMeshObjDic.Add(mapID, mesh);
				Observable.NextFrame().Subscribe(delegate
				{
					HMeshData component = mesh.GetComponent<HMeshData>();
					if (!(component == null))
					{
						component.SetColliderAreaMap();
					}
				});
			}
		}

		private void LoadHitObject()
		{
			pathList = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetHitObjListFolder);
			pathList.Sort();
			sbAbName.Clear();
			sbAssetName.Clear();
			sbAssetName.Append("base");
			excelData = null;
			List<string> list = new List<string>();
			for (int i = 0; i < pathList.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(pathList[i]);
				bool flag = false;
				if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
				{
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
				hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				while (num < excelData.MaxCell)
				{
					row = excelData.list[num++].list;
					int num2 = 0;
					list = new List<string>();
					int result = -1;
					if (int.TryParse(row.GetElement(num2++), out result))
					{
						while (num2 < row.Count)
						{
							list.Add(row.GetElement(num2++));
						}
						lstHitObject.Add(result, list);
					}
				}
			}
		}

		private void CollisionLoadExcel()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetCollisionListFolder);
			assetBundleNameListFromPath.Sort();
			CollisionCtrl.CollisionInfo info = default(CollisionCtrl.CollisionInfo);
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(assetBundleNameListFromPath[i]);
				for (int j = 0; j < lstAnimInfo.Length; j++)
				{
					for (int k = 0; k < lstAnimInfo[j].Count; k++)
					{
						CollisionLoadExcel(lstAnimInfo[j][k], 0, ref info);
						CollisionLoadExcel(lstAnimInfo[j][k], 1, ref info);
						CollisionLoadExcel(lstAnimInfo[j][k], 2, ref info);
					}
				}
			}
		}

		private void CollisionLoadExcel(HScene.AnimationListInfo ainfo, int kind, ref CollisionCtrl.CollisionInfo info)
		{
			switch (kind)
			{
			case 0:
				if (ainfo.fileMale.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ainfo.fileMale);
				break;
			case 1:
				if (ainfo.fileFemale.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ainfo.fileFemale);
				break;
			case 2:
				if (ainfo.fileFemale2.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ainfo.fileFemale2);
				break;
			}
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
			{
				return;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
			hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
			if (excelData == null)
			{
				return;
			}
			if (!DicLstCollisionInfo.ContainsKey(sbAssetName.ToString()))
			{
				DicLstCollisionInfo.Add(sbAssetName.ToString(), new List<CollisionCtrl.CollisionInfo>());
			}
			int num = 1;
			while (num < excelData.MaxCell)
			{
				row = excelData.list[num++].list;
				int num2 = 0;
				info.nameAnimation = row.GetElement(num2++);
				info.lstIsActive = new List<bool>();
				while (num2 < row.Count)
				{
					info.lstIsActive.Add(row.GetElement(num2++) == "1");
				}
				if (DicLstCollisionInfo[sbAssetName.ToString()] == null)
				{
					DicLstCollisionInfo[sbAssetName.ToString()] = new List<CollisionCtrl.CollisionInfo>();
				}
				DicLstCollisionInfo[sbAssetName.ToString()].Add(info);
			}
		}

		private void HitObjLoadExcel()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetHitObjListFolder);
			assetBundleNameListFromPath.Sort();
			HitObjectCtrl.CollisionInfo info = default(HitObjectCtrl.CollisionInfo);
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(assetBundleNameListFromPath[i]);
				for (int j = 0; j < lstAnimInfo.Length; j++)
				{
					for (int k = 0; k < lstAnimInfo[j].Count; k++)
					{
						HitObjLoadExcel(lstAnimInfo[j][k], 0, ref info);
						HitObjLoadExcel(lstAnimInfo[j][k], 1, ref info);
						HitObjLoadExcel(lstAnimInfo[j][k], 2, ref info);
					}
				}
			}
		}

		private void HitObjLoadExcel(HScene.AnimationListInfo ai, int kind, ref HitObjectCtrl.CollisionInfo info)
		{
			switch (kind)
			{
			case 0:
				if (ai.fileMale.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ai.fileMale);
				break;
			case 1:
				if (ai.fileFemale.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ai.fileFemale);
				break;
			case 2:
				if (ai.fileFemale2.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ai.fileFemale2);
				break;
			}
			excelData = null;
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
			{
				return;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
			hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
			if (excelData == null)
			{
				return;
			}
			int num = 0;
			int num2 = 0;
			num2 = 1;
			if (!HitObjAtariName.ContainsKey(sbAssetName.ToString()))
			{
				HitObjAtariName.Add(sbAssetName.ToString(), new List<string>());
			}
			row = excelData.list[num++].list;
			for (int i = 1; i < row.Count; i++)
			{
				int index = i;
				HitObjAtariName[sbAssetName.ToString()].Add(row.GetElement(index));
			}
			if (!DicLstHitObjInfo.ContainsKey(sbAssetName.ToString()))
			{
				DicLstHitObjInfo.Add(sbAssetName.ToString(), new List<HitObjectCtrl.CollisionInfo>());
			}
			while (num < excelData.MaxCell)
			{
				num2 = 0;
				row = excelData.list[num++].list;
				info.nameAnimation = row.GetElement(num2++);
				info.lstIsActive = new List<bool>();
				for (int j = 1; j < row.Count; j++)
				{
					info.lstIsActive.Add(row.GetElement(num2++) == "1");
				}
				DicLstHitObjInfo[sbAssetName.ToString()].Add(info);
			}
		}

		private void HLayerLoadExcel()
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(Singleton<HSceneManager>.Instance.strAssetLayerCtrlListFolder);
			assetBundleNameListFromPath.Sort();
			excelData = null;
			HLayerCtrl.HLayerInfo info = default(HLayerCtrl.HLayerInfo);
			StringBuilder stateName = new StringBuilder();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				sbAbName.Clear();
				sbAbName.Append(assetBundleNameListFromPath[i]);
				for (int j = 0; j < lstAnimInfo.Length; j++)
				{
					for (int k = 0; k < lstAnimInfo[j].Count; k++)
					{
						HLayerLoadExcel(lstAnimInfo[j][k], 0, stateName, ref info);
						HLayerLoadExcel(lstAnimInfo[j][k], 1, stateName, ref info);
						HLayerLoadExcel(lstAnimInfo[j][k], 2, stateName, ref info);
					}
				}
			}
		}

		private void HLayerLoadExcel(HScene.AnimationListInfo ai, int kind, StringBuilder stateName, ref HLayerCtrl.HLayerInfo info)
		{
			switch (kind)
			{
			case 0:
				if (ai.fileMale.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ai.fileMale);
				break;
			case 1:
				if (ai.fileFemale.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ai.fileFemale);
				break;
			case 2:
				if (ai.fileFemale2.IsNullOrEmpty())
				{
					return;
				}
				sbAssetName.Clear();
				sbAssetName.Append(ai.fileFemale2);
				break;
			}
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), sbAssetName.ToString(), string.Empty))
			{
				return;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(sbAbName.ToString(), sbAssetName.ToString(), clone: false, string.Empty);
			hashUseAssetBundle[0].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
			if (excelData == null)
			{
				return;
			}
			if (!LayerInfos.ContainsKey(sbAssetName.ToString()))
			{
				LayerInfos.Add(sbAssetName.ToString(), new Dictionary<string, HLayerCtrl.HLayerInfo>());
			}
			int num = 1;
			while (num < excelData.MaxCell)
			{
				row = excelData.list[num++].list;
				int num2 = 0;
				stateName.Clear();
				stateName.Append(row[num2++]);
				if (!(stateName.ToString() == string.Empty))
				{
					if (!LayerInfos[sbAssetName.ToString()].ContainsKey(stateName.ToString()))
					{
						LayerInfos[sbAssetName.ToString()].Add(stateName.ToString(), default(HLayerCtrl.HLayerInfo));
					}
					int result = 0;
					float result2 = 0f;
					if (int.TryParse(row[num2++], out result))
					{
						float.TryParse(row[num2++], out result2);
					}
					else
					{
						result = 0;
					}
					info.LayerID = result;
					info.weight = result2;
					LayerInfos[sbAssetName.ToString()][stateName.ToString()] = info;
				}
			}
		}

		private int AutoHpointID(string tag)
		{
			int result = -1;
			foreach (KeyValuePair<int, HPoint.HpointData> loadHPointData in loadHPointDatas)
			{
				if (HSceneManager.HmeshTag[tag] != -1)
				{
					if (loadHPointData.Value.place.Count == 1 && loadHPointData.Value.place.TryGetValue(0, out var value) && value.Item1 == HSceneManager.HmeshTag[tag])
					{
						result = loadHPointData.Key;
						break;
					}
				}
				else
				{
					if (loadHPointData.Value.place.Count != 2)
					{
						continue;
					}
					bool flag = true;
					foreach (KeyValuePair<int, UnityEx.ValueTuple<int, int>> item in loadHPointData.Value.place)
					{
						if (item.Value.Item1 != 0 && item.Value.Item1 != 1)
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						result = loadHPointData.Key;
						break;
					}
				}
			}
			return result;
		}

		private void HparticleInit()
		{
			HScene componentInChildren = HSceneSet.GetComponentInChildren<HScene>(includeInactive: true);
			hParticle = new HParticleCtrl();
			hParticle.particlePlace = componentInChildren.hParticlePlace;
			hParticle.Init();
		}

		private void HitObjListInit()
		{
			HScene componentInChildren = HSceneSet.GetComponentInChildren<HScene>(includeInactive: true);
			for (int i = 0; i < 2; i++)
			{
				componentInChildren.ctrlHitObjectFemales[i] = new HitObjectCtrl();
				componentInChildren.ctrlHitObjectFemales[i].Place = componentInChildren.hitobjPlace;
				if (!DicHitObject.ContainsKey(1))
				{
					DicHitObject.Add(1, new Dictionary<int, Dictionary<string, GameObject>>());
				}
				if (!DicHitObject[1].ContainsKey(i))
				{
					DicHitObject[1].Add(i, new Dictionary<string, GameObject>());
				}
				int count = lstHitObject[1].Count;
				for (int j = 0; j < count; j += 3)
				{
					sbAbName.Clear();
					sbAbName.Append(lstHitObject[1][j + 1]);
					bool flag = false;
					if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), lstHitObject[1][j + 2], string.Empty))
					{
						continue;
					}
					GameObject gameObject = CommonLib.LoadAsset<GameObject>(sbAbName.ToString(), lstHitObject[1][j + 2], clone: true, string.Empty);
					hashUseAssetBundle[1].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
					if (!(gameObject == null))
					{
						gameObject.SetActive(value: false);
						gameObject.transform.SetParent(componentInChildren.hitobjPlace, worldPositionStays: false);
						if (!DicHitObject[1][i].ContainsKey(lstHitObject[1][j + 2]))
						{
							DicHitObject[1][i].Add(lstHitObject[1][j + 2], gameObject);
						}
						else
						{
							DicHitObject[1][i][lstHitObject[1][j + 2]] = gameObject;
						}
					}
				}
			}
			for (int k = 0; k < 2; k++)
			{
				componentInChildren.ctrlHitObjectMales[k] = new HitObjectCtrl();
				componentInChildren.ctrlHitObjectMales[k].Place = componentInChildren.hitobjPlace;
				if (!DicHitObject.ContainsKey(0))
				{
					DicHitObject.Add(0, new Dictionary<int, Dictionary<string, GameObject>>());
				}
				if (!DicHitObject[0].ContainsKey(k))
				{
					DicHitObject[0].Add(k, new Dictionary<string, GameObject>());
				}
				int count2 = lstHitObject[0].Count;
				for (int l = 0; l < count2; l += 3)
				{
					sbAbName.Clear();
					sbAbName.Append(lstHitObject[0][l + 1]);
					bool flag2 = false;
					if (!GlobalMethod.AssetFileExist(sbAbName.ToString(), lstHitObject[0][l + 2], string.Empty))
					{
						continue;
					}
					GameObject gameObject2 = CommonLib.LoadAsset<GameObject>(sbAbName.ToString(), lstHitObject[0][l + 2], clone: true, string.Empty);
					hashUseAssetBundle[1].Add(new HAssetBundle(sbAbName.ToString(), string.Empty));
					if (!(gameObject2 == null))
					{
						gameObject2.SetActive(value: false);
						gameObject2.transform.SetParent(componentInChildren.hitobjPlace, worldPositionStays: false);
						if (!DicHitObject[0][k].ContainsKey(lstHitObject[0][l + 2]))
						{
							DicHitObject[0][k].Add(lstHitObject[0][l + 2], gameObject2);
						}
						else
						{
							DicHitObject[0][k][lstHitObject[0][l + 2]] = gameObject2;
						}
					}
				}
			}
		}

		public void ChangeMapHpoint(int mapID)
		{
			if (HPointPrefabs != null && HPointPrefabs.Count > 0)
			{
				foreach (KeyValuePair<int, Dictionary<string, GameObject>> hPointPrefab in HPointPrefabs)
				{
					if (hPointPrefab.Key == mapID)
					{
						continue;
					}
					foreach (GameObject value in hPointPrefab.Value.Values)
					{
						UnityEngine.Object.Destroy(value);
					}
					hPointPrefab.Value.Clear();
				}
			}
			if (AutoHPointPrefabs != null)
			{
				HPoint[] componentsInChildren = AutoHPointPrefabs.GetComponentsInChildren<HPoint>(includeInactive: true);
				foreach (HPoint hPoint in componentsInChildren)
				{
					if (!(hPoint == null) && !(hPoint.gameObject == null))
					{
						UnityEngine.Object.Destroy(hPoint.gameObject);
					}
				}
			}
			if (hPointLists != null && hPointLists.Count > 0)
			{
				List<int> list = new List<int>();
				foreach (KeyValuePair<int, HPointList> hPointList in hPointLists)
				{
					if (hPointList.Key == mapID)
					{
						continue;
					}
					foreach (KeyValuePair<int, List<HPoint>> item in hPointList.Value.lst)
					{
						foreach (HPoint item2 in item.Value)
						{
							if (!(item2 == null) && !(item2.gameObject == null))
							{
								UnityEngine.Object.Destroy(item2.gameObject);
							}
						}
						item.Value.Clear();
					}
					list.Add(hPointList.Key);
				}
				for (int j = 0; j < list.Count; j++)
				{
					hPointLists.Remove(list[j]);
				}
			}
			LoadHpointPrefabs(mapID);
			LoadAutoHPoint(mapID);
			HPointInitData(mapID);
		}

		private int CheckAnimationInfoList(List<HScene.AnimationListInfo> list, int id)
		{
			for (int i = 0; i < list.Count; i++)
			{
				int num = i;
				if (list[num].id == id)
				{
					return num;
				}
			}
			return -1;
		}
	}

	public class ItemIconTables
	{
		public enum IconCategory
		{
			System,
			Menu,
			Category,
			Item
		}

		public Dictionary<int, System.Tuple<string, Sprite>> CategoryIcon = new Dictionary<int, System.Tuple<string, Sprite>>();

		public Dictionary<int, System.Tuple<string, Sprite>> ItemIcon = new Dictionary<int, System.Tuple<string, Sprite>>();

		public Dictionary<int, System.Tuple<string, Sprite>> MenuIcon = new Dictionary<int, System.Tuple<string, Sprite>>();

		public Dictionary<int, System.Tuple<string, Sprite>> SystemIcon = new Dictionary<int, System.Tuple<string, Sprite>>();

		public Dictionary<int, int> MiniMapIcon = new Dictionary<int, int>();

		public Dictionary<int, string> MiniMapIconName = new Dictionary<int, string>();

		public Dictionary<int, string> BaseName = new Dictionary<int, string>();

		private readonly Dictionary<IconCategory, string> BundleNameListPath = new Dictionary<IconCategory, string>
		{
			{
				IconCategory.System,
				"system"
			},
			{
				IconCategory.Menu,
				"menu"
			},
			{
				IconCategory.Category,
				"category"
			},
			{
				IconCategory.Item,
				"item"
			}
		};

		private Dictionary<int, System.Tuple<string, Sprite>> this[IconCategory index] => index switch
		{
			IconCategory.System => SystemIcon, 
			IconCategory.Menu => MenuIcon, 
			IconCategory.Category => CategoryIcon, 
			IconCategory.Item => ItemIcon, 
			_ => null, 
		};

		public Dictionary<int, Sprite> InputIconTable { get; private set; } = new Dictionary<int, Sprite>();


		public Dictionary<int, Sprite> ActionIconTable { get; private set; } = new Dictionary<int, Sprite>();


		public Dictionary<int, Sprite> ActorIconTable { get; private set; } = new Dictionary<int, Sprite>();


		public Dictionary<int, Dictionary<int, int>> EquipmentIconTable { get; private set; } = new Dictionary<int, Dictionary<int, int>>();


		public Dictionary<int, Dictionary<int, Sprite>> StatusIconTable { get; private set; } = new Dictionary<int, Dictionary<int, Sprite>>();


		public Dictionary<int, Sprite> SickIconTable { get; private set; } = new Dictionary<int, Sprite>();


		public Dictionary<int, Sprite> WeatherIconTable { get; private set; } = new Dictionary<int, Sprite>();


		public IEnumerator LoadIcon(IconCategory iconCategory)
		{
			List<string> pathList = CommonLib.GetAssetBundleNameListFromPath($"list/icon/{BundleNameListPath[iconCategory]}/");
			pathList.Sort();
			List<string> unloadFiles = new List<string>();
			List<System.Tuple<string, string>> unloadSprites = new List<System.Tuple<string, string>>();
			foreach (string file in pathList)
			{
				GameIconData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(GameIconData)).GetAllAssets<GameIconData>();
				foreach (GameIconData gameIconData in allAssets)
				{
					foreach (GameIconData.Param item in gameIconData.param)
					{
						Sprite sprite = LoadSpriteAsset(item.Bundle, item.Asset, item.Manifest);
						if (!(sprite == null))
						{
							unloadSprites.Add(new System.Tuple<string, string>(item.Bundle, item.Manifest));
							int iD = item.ID;
							System.Tuple<string, Sprite> value = new System.Tuple<string, Sprite>(item.Name, sprite);
							switch (iconCategory)
							{
							case IconCategory.System:
								SystemIcon[iD] = value;
								break;
							case IconCategory.Menu:
								MenuIcon[iD] = value;
								break;
							case IconCategory.Category:
								CategoryIcon[iD] = value;
								break;
							case IconCategory.Item:
								ItemIcon[iD] = value;
								break;
							}
						}
					}
				}
				unloadFiles.Add(file);
				yield return null;
			}
			foreach (System.Tuple<string, string> item2 in unloadSprites)
			{
				AssetBundleManager.UnloadAssetBundle(item2.Item1, isUnloadForceRefCount: false, item2.Item2);
			}
			foreach (string item3 in unloadFiles)
			{
				AssetBundleManager.UnloadAssetBundle(item3, isUnloadForceRefCount: false);
			}
		}

		public static void SetIcon(IconCategory category, int iconID, Image icon, bool imageName = true)
		{
			Dictionary<int, System.Tuple<string, Sprite>> dictionary = Singleton<Resources>.Instance.itemIconTables[category];
			if (!dictionary.ContainsKey(iconID))
			{
				return;
			}
			icon.sprite = dictionary[iconID].Item2;
			if (imageName)
			{
				Text componentInChildren = icon.GetComponentInChildren<Text>();
				if (!(componentInChildren == null))
				{
					componentInChildren.text = dictionary[iconID].Item1;
					componentInChildren.enabled = icon.sprite == null;
					icon.enabled = !componentInChildren.enabled;
				}
			}
		}

		public void Load(DefinePack definePack)
		{
			LoadInputIcon(definePack);
			LoadActionIcon(definePack);
			LoadActorIcon(definePack);
			LoadWeatherIcon(definePack);
			LoadEquipItemIcon(definePack);
			LoadStatusIcon(definePack);
			LoadSickIcon(definePack);
		}

		public void Release()
		{
			CategoryIcon.Clear();
			ItemIcon.Clear();
			MenuIcon.Clear();
			SystemIcon.Clear();
			MiniMapIcon.Clear();
			MiniMapIconName.Clear();
			BaseName.Clear();
			InputIconTable.Clear();
			ActionIconTable.Clear();
			ActorIconTable.Clear();
			EquipmentIconTable.Clear();
			StatusIconTable.Clear();
			SickIconTable.Clear();
			WeatherIconTable.Clear();
		}

		private void LoadInputIcon(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.InputIconList);
			assetBundleNameListFromPath.Sort();
			LoadIcon(assetBundleNameListFromPath, InputIconTable);
		}

		private void LoadActionIcon(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActionIconList);
			assetBundleNameListFromPath.Sort();
			LoadIcon(assetBundleNameListFromPath, ActionIconTable);
		}

		private void LoadActorIcon(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActorIconList);
			assetBundleNameListFromPath.Sort();
			LoadIcon(assetBundleNameListFromPath, ActorIconTable);
		}

		private void LoadWeatherIcon(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.WeatherIconList);
			assetBundleNameListFromPath.Sort();
			LoadIcon(assetBundleNameListFromPath, WeatherIconTable);
		}

		public IEnumerator LoadMinimapActionIconList()
		{
			List<string> pathList = CommonLib.GetAssetBundleNameListFromPath("list/icon/minimap/");
			pathList.Sort();
			yield return LoadMinimapActionIconList(pathList, MiniMapIcon);
		}

		public IEnumerator LoadMinimapActionIconNameList(DefinePack definePack)
		{
			List<string> pathList = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.MinimapIconNameList);
			pathList.Sort();
			yield return LoadMinimapActionIconNameList(pathList, MiniMapIconName, 0);
			yield return LoadMinimapActionIconNameList(pathList, BaseName, 1);
		}

		private void LoadIcon(List<string> pathList, Dictionary<int, Sprite> table)
		{
			foreach (string path in pathList)
			{
				GameIconData[] allAssets = AssetBundleManager.LoadAllAsset(path, typeof(GameIconData)).GetAllAssets<GameIconData>();
				foreach (GameIconData gameIconData in allAssets)
				{
					foreach (GameIconData.Param item in gameIconData.param)
					{
						Sprite sprite = LoadSpriteAsset(item.Bundle, item.Asset, item.Manifest);
						if (!(sprite == null))
						{
							Singleton<Resources>.Instance.AddLoadAssetBundle(item.Bundle, item.Manifest);
							table[item.ID] = sprite;
						}
					}
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(path, string.Empty);
			}
		}

		private IEnumerator LoadIconAsync(List<string> pathList, Dictionary<int, Sprite> table)
		{
			foreach (string file in pathList)
			{
				GameIconData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(GameIconData)).GetAllAssets<GameIconData>();
				foreach (GameIconData gameIconData in allAssets)
				{
					foreach (GameIconData.Param item in gameIconData.param)
					{
						Sprite sprite = LoadSpriteAsset(item.Bundle, item.Asset, item.Manifest);
						if (!(sprite == null))
						{
							Singleton<Resources>.Instance.AddLoadAssetBundle(item.Bundle, item.Manifest);
							table[item.ID] = sprite;
						}
					}
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(file, string.Empty);
				yield return null;
			}
		}

		private void LoadEquipItemIcon(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.EquipItemIconList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					ExcelData.Param param = excelData.list[j];
					int num = 1;
					if (int.TryParse(param.list.GetElement(num++), out var result) && int.TryParse(param.list.GetElement(num++), out var result2) && int.TryParse(param.list.GetElement(num++), out var result3))
					{
						if (!EquipmentIconTable.TryGetValue(result, out var value))
						{
							Dictionary<int, int> dictionary = new Dictionary<int, int>();
							EquipmentIconTable[result] = dictionary;
							value = dictionary;
						}
						value[result2] = result3;
					}
				}
			}
		}

		private void LoadStatusIcon(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.StatusIconList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					ExcelData.Param param = excelData.list[j];
					int num = 2;
					if (int.TryParse(param.list.GetElement(num++), out var result) && int.TryParse(param.list.GetElement(num++), out var result2))
					{
						string element = param.list.GetElement(num++);
						string element2 = param.list.GetElement(num++);
						string element3 = param.list.GetElement(num++);
						if (!StatusIconTable.TryGetValue(result, out var value))
						{
							Dictionary<int, Sprite> dictionary = new Dictionary<int, Sprite>();
							StatusIconTable[result] = dictionary;
							value = dictionary;
						}
						Sprite value2 = LoadSpriteAsset(element, element2, element3);
						value[result2] = value2;
						Singleton<Resources>.Instance.AddLoadAssetBundle(element, element3);
					}
				}
			}
		}

		private void LoadSickIcon(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.SickIconList);
			assetBundleNameListFromPath.Sort();
			LoadIcon(assetBundleNameListFromPath, SickIconTable);
		}

		public static Sprite LoadSpriteAsset(string assetBundleName, string assetName, string manifestName)
		{
			manifestName = ((!manifestName.IsNullOrEmpty()) ? manifestName : null);
			if (AssetBundleCheck.IsSimulation)
			{
				manifestName = string.Empty;
			}
			if (!AssetBundleCheck.IsFile(assetBundleName, assetName))
			{
				string message = $"読み込みエラー\r\nassetBundleName：{assetBundleName}\tassetName：{assetName}";
				Debug.LogWarning(message);
				return null;
			}
			AssetBundleLoadAssetOperation assetBundleLoadAssetOperation = AssetBundleManager.LoadAsset(assetBundleName, assetName, typeof(Sprite), (!manifestName.IsNullOrEmpty()) ? manifestName : null);
			Sprite sprite = assetBundleLoadAssetOperation.GetAsset<Sprite>();
			if (sprite == null)
			{
				Texture2D asset = assetBundleLoadAssetOperation.GetAsset<Texture2D>();
				if (asset == null)
				{
					return null;
				}
				sprite = Sprite.Create(asset, new Rect(0f, 0f, asset.width, asset.height), Vector2.zero);
			}
			return sprite;
		}

		public IEnumerator LoadMinimapActionIconList(List<string> pathList, Dictionary<int, int> table)
		{
			foreach (string file in pathList)
			{
				MinimapIconIDData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(MinimapIconIDData)).GetAllAssets<MinimapIconIDData>();
				foreach (MinimapIconIDData minimapIconIDData in allAssets)
				{
					foreach (MinimapIconIDData.Param item in minimapIconIDData.param)
					{
						if (item.ActionID != -1)
						{
							table[item.ActionID] = item.ID;
						}
					}
				}
				Singleton<Resources>.Instance.AddLoadAssetBundle(file, string.Empty);
				yield return null;
			}
		}

		public IEnumerator LoadMinimapActionIconNameList(List<string> pathList, Dictionary<int, string> table, int mode)
		{
			string asset = string.Empty;
			foreach (string file in pathList)
			{
				switch (mode)
				{
				case 0:
					asset = $"Names_{System.IO.Path.GetFileNameWithoutExtension(file):00}";
					break;
				case 1:
					asset = $"BaseNames_{System.IO.Path.GetFileNameWithoutExtension(file):00}";
					break;
				}
				ExcelData excelData = CommonLib.LoadAsset<ExcelData>(file, asset, clone: false, string.Empty);
				if (excelData == null)
				{
					yield return null;
					continue;
				}
				foreach (ExcelData.Param item in excelData.list)
				{
					if (!int.TryParse(item.list[0], out var result))
					{
						continue;
					}
					switch (mode)
					{
					case 0:
						if (item.list.Count >= 3)
						{
							table.Add(result, item.list[2]);
						}
						break;
					case 1:
						table.Add(result, item.list[1]);
						break;
					}
				}
			}
			yield return null;
		}
	}

	public class MapTables
	{
		public class VisibleObjectInfo
		{
			public string nameCollider;

			public string VanishObjName;
		}

		public Dictionary<int, Dictionary<int, MinimapNavimesh.AreaGroupInfo>> AreaGroupTable = new Dictionary<int, Dictionary<int, MinimapNavimesh.AreaGroupInfo>>();

		private static readonly string[] _separators = new string[1] { "," };

		public Dictionary<int, AssetBundleInfo> MapList { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> ChunkList { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> SEMeshList { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> CameraColliderList { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, Dictionary<int, string>> MapGroupNameList { get; set; } = new Dictionary<int, Dictionary<int, string>>();


		public Dictionary<int, Dictionary<int, List<int>>> MapGroupHiddenAreaList { get; set; } = new Dictionary<int, Dictionary<int, List<int>>>();


		public Dictionary<int, Dictionary<int, List<int>>> AgentHiddenAreaList { get; set; } = new Dictionary<int, Dictionary<int, List<int>>>();


		public Dictionary<int, AssetBundleInfo> PlantItemList { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public List<ItemInfo> PlantIvyFilterList { get; private set; } = new List<ItemInfo>();


		public Dictionary<int, ActionItemInfo> EventItemList { get; private set; } = new Dictionary<int, ActionItemInfo>();


		public Dictionary<int, Dictionary<int, int>> FoodEventItemList { get; private set; } = new Dictionary<int, Dictionary<int, int>>();


		public Dictionary<int, Dictionary<int, int>> FoodDateEventItemList { get; private set; } = new Dictionary<int, Dictionary<int, int>>();


		public Dictionary<int, AssetBundleInfo> NavMeshSourceList { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> BasePointGroupTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> DevicePointGroupTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> HarvestPointGroupTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> ShipPointGroupTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, AssetBundleInfo> ActionPointGroupTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> MerchantPointGroupTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> EventPointGroupTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> StoryPointGroupTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> LightSwitchPointGroupTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public Dictionary<int, Dictionary<int, List<ActionPointInfo>>> PlayerActionPointInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, List<ActionPointInfo>>>();


		public Dictionary<int, Dictionary<int, List<ActionPointInfo>>> AgentActionPointInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, List<ActionPointInfo>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<DateActionPointInfo>>>> PlayerDateActionPointInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<DateActionPointInfo>>>>();


		public Dictionary<int, Dictionary<int, List<DateActionPointInfo>>> AgentDateActionPointInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, List<DateActionPointInfo>>>();


		public Dictionary<int, Dictionary<int, List<ActionPointInfo>>> MerchantActionPointInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, List<ActionPointInfo>>>();


		public Dictionary<int, Dictionary<int, List<MerchantPointInfo>>> MerchantPointInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, List<MerchantPointInfo>>>();


		public Dictionary<int, AssetBundleInfo> EventParticleTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, Dictionary<bool, string[]>> AreaOpenStateObjectNameTable { get; private set; } = new Dictionary<int, Dictionary<bool, string[]>>();


		public Dictionary<int, List<string>> EventPointCommandLabelTextTable { get; private set; } = new Dictionary<int, List<string>>();


		public Dictionary<int, UnityEx.ValueTuple<int, List<string>>> EventDialogInfoTable { get; private set; } = new Dictionary<int, UnityEx.ValueTuple<int, List<string>>>();


		public Dictionary<int, string> AreaOpenIDTable { get; private set; } = new Dictionary<int, string>();


		public Dictionary<int, int[]> AreaOpenStateMapAreaLinkerTable { get; private set; } = new Dictionary<int, int[]>();


		public Dictionary<int, Dictionary<int, Dictionary<bool, Dictionary<int, List<UnityEx.ValueTuple<string, float>>>>>> TimeRelationObjectStateTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<bool, Dictionary<int, List<UnityEx.ValueTuple<string, float>>>>>>();


		public Dictionary<int, string> TimeRelationObjectIDTable { get; private set; } = new Dictionary<int, string>();


		public Dictionary<int, Dictionary<int, ActionCameraData>> ActionCameraDataTable { get; private set; } = new Dictionary<int, Dictionary<int, ActionCameraData>>();


		public Dictionary<int, List<List<VisibleObjectInfo>>> VanishList { get; private set; } = new Dictionary<int, List<List<VisibleObjectInfo>>>();


		public Dictionary<int, Dictionary<int, List<int>>> VanishHousingAreaGroup { get; private set; } = new Dictionary<int, Dictionary<int, List<int>>>();


		public Dictionary<int, Dictionary<int, Dictionary<int, List<UnityEx.ValueTuple<int, int>>>>> TempRangeTable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, List<UnityEx.ValueTuple<int, int>>>>>();


		public Dictionary<int, MiniMapControler.MinimapInfo> MinimapInfoTable { get; private set; } = new Dictionary<int, MiniMapControler.MinimapInfo>();


		public void Load(DefinePack definePack)
		{
			LoadMapList(definePack);
			LoadChunkList(definePack);
			LoadSEMeshList(definePack);
			LoadCameraColliderList(definePack);
			LoadMapGroupList(definePack);
			LoadMapHiddenAreaList(definePack);
			LoadAgentVanishAreaList(definePack);
			LoadPlantItemList(definePack);
			LoadIvyFilterList(definePack);
			LoadNavMeshSourceTable(definePack);
			LoadEventItemList(definePack);
			LoadFoodEventItemList(definePack);
			LoadEventParticleList(definePack);
			LoadBasePointListTable(definePack);
			LoadDevicePointListTable(definePack);
			LoadFarmPointListTable(definePack);
			LoadShipPointListTable(definePack);
			LoadActionPointListTable(definePack);
			LoadLightSwitchPointListTable(definePack);
			LoadPlayerActionPointInfo(definePack);
			LoadAgentActionPointInfo(definePack);
			LoadPlayerDateActionPointInfo(definePack);
			LoadAgentDateActionPointInfo(definePack);
			LoadMerchantPoint(definePack);
			LoadEventPointListTable(definePack);
			LoadStoryPointListTable(definePack);
			LoadAreaOpenStateList(definePack);
			LoadTimeRelationInfoList(definePack);
			LoadAreaGroup(definePack);
			LoadActionCameraData(definePack);
			LoadVanish(definePack);
			LoadVanishHousingGroup(definePack);
			LoadEnviroInfoList(definePack);
			LoadMiniMapInfo();
		}

		public void Release()
		{
			MapList.Clear();
			ChunkList.Clear();
			SEMeshList.Clear();
			CameraColliderList.Clear();
			MapGroupNameList.Clear();
			MapGroupHiddenAreaList.Clear();
			AgentHiddenAreaList.Clear();
			PlantItemList.Clear();
			PlantIvyFilterList.Clear();
			EventItemList.Clear();
			FoodEventItemList.Clear();
			FoodDateEventItemList.Clear();
			NavMeshSourceList.Clear();
			BasePointGroupTable.Clear();
			DevicePointGroupTable.Clear();
			HarvestPointGroupTable.Clear();
			ShipPointGroupTable.Clear();
			ActionPointGroupTable.Clear();
			MerchantPointGroupTable.Clear();
			EventPointGroupTable.Clear();
			StoryPointGroupTable.Clear();
			LightSwitchPointGroupTable.Clear();
			PlayerActionPointInfoTable.Clear();
			AgentActionPointInfoTable.Clear();
			PlayerDateActionPointInfoTable.Clear();
			AgentDateActionPointInfoTable.Clear();
			MerchantActionPointInfoTable.Clear();
			MerchantPointInfoTable.Clear();
			EventParticleTable.Clear();
			AreaOpenStateObjectNameTable.Clear();
			EventPointCommandLabelTextTable.Clear();
			EventDialogInfoTable.Clear();
			AreaOpenIDTable.Clear();
			AreaOpenStateMapAreaLinkerTable.Clear();
			AreaGroupTable.Clear();
			TimeRelationObjectStateTable.Clear();
			TimeRelationObjectIDTable.Clear();
			ActionCameraDataTable.Clear();
			VanishList.Clear();
			TempRangeTable.Clear();
			MinimapInfoTable.Clear();
		}

		private void LoadMapList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.MapList);
			assetBundleNameListFromPath.Sort();
			if (assetBundleNameListFromPath.Count == 0)
			{
				return;
			}
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 0;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 0;
					if (int.TryParse(param.list.GetElement(num2++), out var result))
					{
						string element = param.list.GetElement(num2++);
						string element2 = param.list.GetElement(num2++);
						string element3 = param.list.GetElement(num2++);
						string element4 = param.list.GetElement(num2++);
						AssetBundleInfo value = new AssetBundleInfo(element, element2, element3, element4);
						MapList[result] = value;
					}
				}
			}
		}

		private void LoadChunkList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ChunkList);
			assetBundleNameListFromPath.Sort();
			if (assetBundleNameListFromPath.Count == 0)
			{
				return;
			}
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 0;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 0;
					if (int.TryParse(param.list.GetElement(num2++), out var result))
					{
						string element = param.list.GetElement(num2++);
						string element2 = param.list.GetElement(num2++);
						string element3 = param.list.GetElement(num2++);
						AssetBundleInfo value = new AssetBundleInfo(string.Empty, element, element2, element3);
						ChunkList[result] = value;
					}
				}
			}
		}

		private void LoadSEMeshList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ChunkList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, $"semesh_{fileNameWithoutExtension}", string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (!list.IsNullOrEmpty())
					{
						int num = 0;
						if (int.TryParse(list.GetElement(num++) ?? string.Empty, out var result))
						{
							string assetbundle_ = list.GetElement(num++) ?? string.Empty;
							string asset_ = list.GetElement(num++) ?? string.Empty;
							string manifest_ = list.GetElement(num++) ?? string.Empty;
							SEMeshList[result] = new AssetBundleInfo(string.Empty, assetbundle_, asset_, manifest_);
						}
					}
				}
			}
		}

		private void LoadCameraColliderList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ChunkList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = CommonLib.LoadAsset<ExcelData>(text, $"cam_col_{fileNameWithoutExtension}", clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (!list.IsNullOrEmpty())
					{
						int num = 0;
						if (int.TryParse(list.GetElement(num++), out var result))
						{
							num++;
							string element = list.GetElement(num++);
							string element2 = list.GetElement(num++);
							string element3 = list.GetElement(num++);
							CameraColliderList[result] = new AssetBundleInfo(string.Empty, element, element2, element3);
						}
					}
				}
			}
		}

		private void LoadMapGroupList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ChunkList);
			assetBundleNameListFromPath.Sort();
			List<string> list = ListPool<string>.Get();
			foreach (string item in assetBundleNameListFromPath)
			{
				MapHiddenGroupData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(MapHiddenGroupData)).GetAllAssets<MapHiddenGroupData>();
				foreach (MapHiddenGroupData mapHiddenGroupData in allAssets)
				{
					foreach (MapHiddenGroupData.Param item2 in mapHiddenGroupData.param)
					{
						if (!MapGroupNameList.TryGetValue(item2.MapID, out var value))
						{
							Dictionary<int, string> dictionary = new Dictionary<int, string>();
							MapGroupNameList[item2.MapID] = dictionary;
							value = dictionary;
						}
						value[item2.ID] = item2.GroupName;
					}
				}
			}
			foreach (string item3 in list)
			{
				AssetBundleManager.UnloadAssetBundle(item3, isUnloadForceRefCount: false);
			}
			ListPool<string>.Release(list);
		}

		private void LoadMapHiddenAreaList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ChunkList);
			assetBundleNameListFromPath.Sort();
			List<string> list = ListPool<string>.Get();
			foreach (string item in assetBundleNameListFromPath)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(item);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(item, $"area_vanish_{fileNameWithoutExtension}", string.Empty);
				if (excelData == null)
				{
					continue;
				}
				foreach (ExcelData.Param item2 in excelData.list)
				{
					int num = 1;
					string element = item2.list.GetElement(num++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					string element2 = item2.list.GetElement(num++);
					if (!int.TryParse(element2, out var result2))
					{
						continue;
					}
					string[] array = item2.list.GetElement(num++).Split(_separators, StringSplitOptions.RemoveEmptyEntries);
					if (array.IsNullOrEmpty())
					{
						continue;
					}
					if (!MapGroupHiddenAreaList.TryGetValue(result, out var value))
					{
						Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
						MapGroupHiddenAreaList[result] = dictionary;
						value = dictionary;
					}
					if (!value.TryGetValue(result2, out var value2))
					{
						List<int> list3 = (value[result2] = new List<int>());
						value2 = list3;
					}
					string[] array2 = array;
					foreach (string s in array2)
					{
						if (int.TryParse(s, out var result3) && !value2.Contains(result3))
						{
							value2.Add(result3);
						}
					}
				}
			}
			foreach (string item3 in list)
			{
				AssetBundleManager.UnloadAssetBundle(item3, isUnloadForceRefCount: false);
			}
			ListPool<string>.Release(list);
		}

		public void LoadAgentVanishAreaList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActorVanishList);
			assetBundleNameListFromPath.Sort();
			List<string> list = ListPool<string>.Get();
			foreach (string item in assetBundleNameListFromPath)
			{
				AgentHiddenAreaData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(AgentHiddenAreaData)).GetAllAssets<AgentHiddenAreaData>();
				foreach (AgentHiddenAreaData agentHiddenAreaData in allAssets)
				{
					foreach (AgentHiddenAreaData.Param item2 in agentHiddenAreaData.param)
					{
						if (!AgentHiddenAreaList.TryGetValue(item2.MapID, out var value))
						{
							Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
							AgentHiddenAreaList[item2.MapID] = dictionary;
							value = dictionary;
						}
						if (!value.TryGetValue(item2.AreaID, out var value2))
						{
							List<int> list2 = new List<int>();
							value[item2.AreaID] = list2;
							value2 = list2;
						}
						string[] array = item2.HiddenAreaIDMulti.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
						if (array.IsNullOrEmpty())
						{
							continue;
						}
						string[] array2 = array;
						foreach (string s in array2)
						{
							if (int.TryParse(s, out var result) && !value2.Contains(result))
							{
								value2.Add(result);
							}
						}
					}
				}
			}
			foreach (string item3 in list)
			{
				AssetBundleManager.UnloadAssetBundle(item3, isUnloadForceRefCount: false);
			}
			ListPool<string>.Release(list);
		}

		private void LoadPlantItemList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.PlantItemList);
			assetBundleNameListFromPath.Sort();
			if (assetBundleNameListFromPath.Count == 0)
			{
				return;
			}
			foreach (string item in assetBundleNameListFromPath)
			{
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(item);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(item, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 0;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 0;
					if (int.TryParse(param.list.GetElement(num2++), out var result))
					{
						string element = param.list.GetElement(num2++);
						string element2 = param.list.GetElement(num2++);
						string element3 = param.list.GetElement(num2++);
						string element4 = param.list.GetElement(num2++);
						PlantItemList[result] = new AssetBundleInfo
						{
							name = element,
							assetbundle = element2,
							asset = element3,
							manifest = element4
						};
					}
				}
			}
		}

		private void LoadIvyFilterList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.IvyFilterList);
			assetBundleNameListFromPath.Sort();
			if (assetBundleNameListFromPath.Count == 0)
			{
				return;
			}
			List<string> toRelease = ListPool<string>.Get();
			foreach (string item in assetBundleNameListFromPath)
			{
				PlantIvyFilterData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(PlantIvyFilterData)).GetAllAssets<PlantIvyFilterData>();
				foreach (PlantIvyFilterData plantIvyFilterData in allAssets)
				{
					foreach (PlantIvyFilterData.Param param in plantIvyFilterData.param)
					{
						if (!PlantIvyFilterList.Exists((ItemInfo x) => x.CategoryID == param.CategoryID && x.ItemID == param.ItemID))
						{
							PlantIvyFilterList.Add(new ItemInfo
							{
								CategoryID = param.CategoryID,
								ItemID = param.ItemID,
								ObjID = param.ObjID
							});
						}
					}
				}
			}
			ListPool<string>.Release(toRelease);
		}

		private void LoadEventItemList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.EventItemList);
			assetBundleNameListFromPath.Sort();
			if (assetBundleNameListFromPath.Count == 0)
			{
				return;
			}
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 0;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 0;
					if (int.TryParse(param.list.GetElement(num2++), out var result))
					{
						string element = param.list.GetElement(num2++);
						string element2 = param.list.GetElement(num2++);
						string element3 = param.list.GetElement(num2++);
						string element4 = param.list.GetElement(num2++);
						bool result2;
						bool existsAnimation = bool.TryParse(param.list.GetElement(num2++), out result2) && result2;
						string element5 = param.list.GetElement(num2++);
						string element6 = param.list.GetElement(num2++);
						EventItemList[result] = new ActionItemInfo
						{
							assetbundleInfo = new AssetBundleInfo(element, element2, element3, element4),
							existsAnimation = existsAnimation,
							animeAssetBundle = new AssetBundleInfo
							{
								assetbundle = element5,
								asset = element6
							}
						};
					}
				}
			}
		}

		private void LoadFoodEventItemList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.EventItemList);
			assetBundleNameListFromPath.Sort();
			if (assetBundleNameListFromPath.Count == 0)
			{
				return;
			}
			foreach (string item in assetBundleNameListFromPath)
			{
				FoodEventItemData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(FoodEventItemData)).GetAllAssets<FoodEventItemData>();
				foreach (FoodEventItemData foodEventItemData in allAssets)
				{
					foreach (FoodEventItemData.Param item2 in foodEventItemData.param)
					{
						if (!FoodEventItemList.TryGetValue(item2.CategoryID, out var value))
						{
							Dictionary<int, int> dictionary = new Dictionary<int, int>();
							FoodEventItemList[item2.CategoryID] = dictionary;
							value = dictionary;
						}
						value[item2.ItemID] = item2.EventItemID;
						if (!FoodDateEventItemList.TryGetValue(item2.CategoryID, out var value2))
						{
							Dictionary<int, int> dictionary = new Dictionary<int, int>();
							FoodDateEventItemList[item2.CategoryID] = dictionary;
							value2 = dictionary;
						}
						if (int.TryParse(item2.DateEventItemID, out var result))
						{
							value2[item2.ItemID] = result;
						}
					}
				}
			}
		}

		private void LoadNavMeshSourceTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.MapList);
			assetBundleNameListFromPath.Sort();
			foreach (string file in assetBundleNameListFromPath)
			{
				PointPrefabData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(PointPrefabData)).GetAllAssets<PointPrefabData>();
				PointPrefabData[] array = allAssets;
				foreach (PointPrefabData pointPrefabData in array)
				{
					foreach (PointPrefabData.Param item in pointPrefabData.param)
					{
						if (item != null)
						{
							NavMeshSourceList[item.MapID] = new AssetBundleInfo(item.Name, item.AssetBundle, item.Asset, item.Manifest);
						}
					}
				}
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file, string.Empty));
				}
			}
		}

		private void LoadActionPointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActionPointPrefabList);
			assetBundleNameListFromPath.Sort();
			foreach (string file in assetBundleNameListFromPath)
			{
				PointPrefabData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(PointPrefabData)).GetAllAssets<PointPrefabData>();
				PointPrefabData[] array = allAssets;
				foreach (PointPrefabData pointPrefabData in array)
				{
					foreach (PointPrefabData.Param item in pointPrefabData.param)
					{
						if (item != null)
						{
							ActionPointGroupTable[item.MapID] = new AssetBundleInfo(item.Name, item.AssetBundle, item.Asset, item.Manifest);
						}
					}
				}
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file, string.Empty));
				}
			}
		}

		private void LoadBasePointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.BasePointPrefabList);
			assetBundleNameListFromPath.Sort();
			foreach (string file in assetBundleNameListFromPath)
			{
				PointPrefabData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(PointPrefabData)).GetAllAssets<PointPrefabData>();
				PointPrefabData[] array = allAssets;
				foreach (PointPrefabData pointPrefabData in array)
				{
					foreach (PointPrefabData.Param item in pointPrefabData.param)
					{
						if (item != null)
						{
							BasePointGroupTable[item.MapID] = new AssetBundleInfo(item.Name, item.AssetBundle, item.Asset, item.Manifest);
						}
					}
				}
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file, string.Empty));
				}
			}
		}

		private void LoadDevicePointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.DevicePointPrefabList);
			assetBundleNameListFromPath.Sort();
			foreach (string file in assetBundleNameListFromPath)
			{
				PointPrefabData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(PointPrefabData)).GetAllAssets<PointPrefabData>();
				PointPrefabData[] array = allAssets;
				foreach (PointPrefabData pointPrefabData in array)
				{
					foreach (PointPrefabData.Param item in pointPrefabData.param)
					{
						if (item != null)
						{
							DevicePointGroupTable[item.MapID] = new AssetBundleInfo(item.Name, item.AssetBundle, item.Asset, item.Manifest);
						}
					}
				}
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file, string.Empty));
				}
			}
		}

		private void LoadFarmPointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.FarmPointPrefabList);
			assetBundleNameListFromPath.Sort();
			foreach (string file in assetBundleNameListFromPath)
			{
				PointPrefabData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(PointPrefabData)).GetAllAssets<PointPrefabData>();
				PointPrefabData[] array = allAssets;
				foreach (PointPrefabData pointPrefabData in array)
				{
					foreach (PointPrefabData.Param item in pointPrefabData.param)
					{
						if (item != null)
						{
							HarvestPointGroupTable[item.MapID] = new AssetBundleInfo(item.Name, item.AssetBundle, item.Asset, item.Manifest);
						}
					}
				}
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file, string.Empty));
				}
			}
		}

		private void LoadShipPointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ShipPointPrefabList);
			assetBundleNameListFromPath.Sort();
			foreach (string file in assetBundleNameListFromPath)
			{
				PointPrefabData[] allAssets = AssetBundleManager.LoadAllAsset(file, typeof(PointPrefabData)).GetAllAssets<PointPrefabData>();
				PointPrefabData[] array = allAssets;
				foreach (PointPrefabData pointPrefabData in array)
				{
					foreach (PointPrefabData.Param item in pointPrefabData.param)
					{
						if (item != null)
						{
							ShipPointGroupTable[item.MapID] = new AssetBundleInfo(item.Name, item.AssetBundle, item.Asset, item.Manifest);
						}
					}
				}
				if (!MapScene.AssetBundlePaths.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == file))
				{
					MapScene.AssetBundlePaths.Add(new UnityEx.ValueTuple<string, string>(file, string.Empty));
				}
			}
		}

		private void LoadLightSwitchPointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.LightSwitchPointList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			foreach (string item in assetBundleNameListFromPath)
			{
				if (item.IsNullOrEmpty())
				{
					continue;
				}
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(item);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(item, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int i = 1; i < excelData.MaxCell; i++)
				{
					List<string> list = excelData.list[i].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = new AssetBundleInfo(list.GetElement(num++), list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
						if (result == 0)
						{
							LoadLightSwitchPointPrefabInfo(assetInfo);
						}
					}
				}
			}
		}

		private void LoadLightSwitchPointPrefabInfo(AssetBundleInfo assetInfo)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				AssetBundleInfo value = new AssetBundleInfo(string.Empty, list.GetElement(num++), list.GetElement(num++), list.GetElement(num++));
				if (!value.assetbundle.IsNullOrEmpty() && !value.asset.IsNullOrEmpty() && !value.manifest.IsNullOrEmpty())
				{
					if (!LightSwitchPointGroupTable.TryGetValue(result, out var value2) || value2 == null)
					{
						Dictionary<int, AssetBundleInfo> dictionary = new Dictionary<int, AssetBundleInfo>();
						LightSwitchPointGroupTable[result] = dictionary;
						value2 = dictionary;
					}
					value2[result2] = value;
				}
			}
		}

		private void LoadPlayerActionPointInfo(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.PlayerActionPointList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					ExcelData.Param param = excelData.list[j];
					int num = 0;
					string element = param.list.GetElement(num++);
					if (int.TryParse(element, out var result))
					{
						string element2 = param.list.GetElement(num++);
						string element3 = param.list.GetElement(num++);
						string element4 = param.list.GetElement(num++);
						Debug.Log($"{element3} 読み込み");
						ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(element2, element3, element4);
						if (!PlayerActionPointInfoTable.TryGetValue(result, out var value))
						{
							Dictionary<int, List<ActionPointInfo>> dictionary = new Dictionary<int, List<ActionPointInfo>>();
							PlayerActionPointInfoTable[result] = dictionary;
							value = dictionary;
						}
						LoadActionPointInfoListSheet(excelData2, value);
					}
				}
			}
		}

		private void LoadAgentActionPointInfo(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentActionPointList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					ExcelData.Param param = excelData.list[j];
					int num = 0;
					string element = param.list.GetElement(num++);
					if (int.TryParse(element, out var result))
					{
						string element2 = param.list.GetElement(num++);
						string element3 = param.list.GetElement(num++);
						string element4 = param.list.GetElement(num++);
						Debug.Log($"{element3} 読み込み");
						ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(element2, element3, element4);
						if (!AgentActionPointInfoTable.TryGetValue(result, out var value))
						{
							Dictionary<int, List<ActionPointInfo>> dictionary = new Dictionary<int, List<ActionPointInfo>>();
							AgentActionPointInfoTable[result] = dictionary;
							value = dictionary;
						}
						LoadActionPointInfoListSheet(excelData2, value);
					}
				}
			}
		}

		private void LoadActionPointInfoListSheet(ExcelData excelData, Dictionary<int, List<ActionPointInfo>> table)
		{
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				ExcelData.Param param = excelData.list[i];
				int num = 0;
				if (!int.TryParse(param.list.GetElement(num++), out var result))
				{
					continue;
				}
				string element = param.list.GetElement(num++);
				string element2 = param.list.GetElement(num++);
				if (!int.TryParse(element2, out var result2))
				{
					continue;
				}
				string element3 = param.list.GetElement(num++);
				if (!int.TryParse(element3, out var result3))
				{
					continue;
				}
				string element4 = param.list.GetElement(num++);
				if (!int.TryParse(element4, out var result4))
				{
				}
				string element5 = param.list.GetElement(num++);
				if (!bool.TryParse(element5, out var result5))
				{
					continue;
				}
				string element6 = param.list.GetElement(num++);
				if (!int.TryParse(element6, out var result6))
				{
					result6 = -1;
				}
				string element7 = param.list.GetElement(num++);
				if (int.TryParse(element7, out var result7))
				{
					string element8 = param.list.GetElement(num++);
					string element9 = param.list.GetElement(num++);
					string element10 = param.list.GetElement(num++);
					string element11 = param.list.GetElement(num++);
					if (!int.TryParse(element11, out var result8))
					{
						result8 = -1;
					}
					string element12 = param.list.GetElement(num++);
					if (!int.TryParse(element12, out var result9))
					{
						result9 = -1;
					}
					string element13 = param.list.GetElement(num++);
					if (!int.TryParse(element13, out var result10))
					{
						result10 = -1;
					}
					AIProject.EventType eventTypeMask = AIProject.Definitions.Action.EventTypeTable[result2];
					if (!table.TryGetValue(result, out var value))
					{
						List<ActionPointInfo> list2 = (table[result] = new List<ActionPointInfo>());
						value = list2;
					}
					value.Add(new ActionPointInfo
					{
						actionName = element,
						pointID = result,
						eventID = result2,
						eventTypeMask = eventTypeMask,
						poseID = result3,
						datePoseID = result4,
						isTalkable = result5,
						iconID = result6,
						cameraID = result7,
						baseNullName = element8,
						recoveryNullName = element9,
						labelNullName = element10,
						searchAreaID = result8,
						gradeValue = result9,
						doorOpenType = result10
					});
				}
			}
		}

		private void LoadPlayerDateActionPointInfo(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.PlayerActionPointList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, $"date_{fileNameWithoutExtension}", string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					ExcelData.Param param = excelData.list[j];
					int num = 0;
					string element = param.list.GetElement(num++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					string element2 = param.list.GetElement(num++);
					if (!int.TryParse(element2, out var result2))
					{
						continue;
					}
					string element3 = param.list.GetElement(num++);
					string element4 = param.list.GetElement(num++);
					string element5 = param.list.GetElement(num++);
					Debug.Log($"{element4} 読み込み");
					ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(element3, element4, element5);
					if (!PlayerDateActionPointInfoTable.TryGetValue(result, out var value))
					{
						Dictionary<int, Dictionary<int, List<DateActionPointInfo>>> dictionary = new Dictionary<int, Dictionary<int, List<DateActionPointInfo>>>();
						PlayerDateActionPointInfoTable[result] = dictionary;
						value = dictionary;
					}
					if (!value.TryGetValue(result2, out var value2))
					{
						Dictionary<int, List<DateActionPointInfo>> dictionary3 = (value[result2] = new Dictionary<int, List<DateActionPointInfo>>());
						value2 = dictionary3;
					}
					for (int k = 1; k < excelData2.MaxCell; k++)
					{
						ExcelData.Param param2 = excelData2.list[k];
						int num2 = 0;
						if (!int.TryParse(param2.list.GetElement(num2++), out var result3))
						{
							continue;
						}
						string element6 = param2.list.GetElement(num2++);
						string element7 = param2.list.GetElement(num2++);
						if (!int.TryParse(element7, out var result4))
						{
							continue;
						}
						string element8 = param2.list.GetElement(num2++);
						if (!int.TryParse(element8, out var result5))
						{
							continue;
						}
						string element9 = param2.list.GetElement(num2++);
						if (int.TryParse(element9, out var result6))
						{
							string element10 = param2.list.GetElement(num2++);
							if (!bool.TryParse(element10, out var result7))
							{
							}
							string element11 = param2.list.GetElement(num2++);
							if (!int.TryParse(element11, out var result8))
							{
								result8 = -1;
							}
							string element12 = param2.list.GetElement(num2++);
							if (!int.TryParse(element12, out var result9))
							{
								result9 = -1;
							}
							string element13 = param2.list.GetElement(num2++);
							string element14 = param2.list.GetElement(num2++);
							string element15 = param2.list.GetElement(num2++);
							string element16 = param2.list.GetElement(num2++);
							string element17 = param2.list.GetElement(num2++);
							string element18 = param2.list.GetElement(num2++);
							if (!int.TryParse(element18, out var result10))
							{
								result10 = -1;
							}
							string element19 = param2.list.GetElement(num2++);
							if (!int.TryParse(element19, out var result11))
							{
								result11 = -1;
							}
							string element20 = param2.list.GetElement(num2++);
							if (!int.TryParse(element20, out var result12))
							{
								result12 = -1;
							}
							AIProject.EventType eventTypeMask = AIProject.Definitions.Action.EventTypeTable[result4];
							if (!value2.TryGetValue(result3, out var value3))
							{
								List<DateActionPointInfo> list2 = (value2[result3] = new List<DateActionPointInfo>());
								value3 = list2;
							}
							value3.Add(new DateActionPointInfo
							{
								actionName = element6,
								pointID = result3,
								eventID = result4,
								eventTypeMask = eventTypeMask,
								poseIDA = result5,
								poseIDB = result6,
								isTalkable = result7,
								iconID = result8,
								cameraID = result9,
								baseNullNameA = element13,
								baseNullNameB = element14,
								recoveryNullNameA = element15,
								recoveryNullNameB = element16,
								labelNullName = element17,
								searchAreaID = result10,
								gradeValue = result11,
								doorOpenType = result12
							});
						}
					}
				}
			}
		}

		private void LoadAgentDateActionPointInfo(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AgentActionPointList);
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, $"date_{fileNameWithoutExtension}", string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					ExcelData.Param param = excelData.list[j];
					int num = 0;
					string element = param.list.GetElement(num++);
					if (!int.TryParse(element, out var result))
					{
						continue;
					}
					string element2 = param.list.GetElement(num++);
					string element3 = param.list.GetElement(num++);
					string element4 = param.list.GetElement(num++);
					Debug.Log($"{element3} 読み込み");
					ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(element2, element3, element4);
					if (!AgentDateActionPointInfoTable.TryGetValue(result, out var value))
					{
						Dictionary<int, List<DateActionPointInfo>> dictionary = new Dictionary<int, List<DateActionPointInfo>>();
						AgentDateActionPointInfoTable[result] = dictionary;
						value = dictionary;
					}
					for (int k = 1; k < excelData2.MaxCell; k++)
					{
						ExcelData.Param param2 = excelData2.list[k];
						int num2 = 0;
						if (!int.TryParse(param2.list.GetElement(num2++), out var result2))
						{
							continue;
						}
						string element5 = param2.list.GetElement(num2++);
						string element6 = param2.list.GetElement(num2++);
						if (!int.TryParse(element6, out var result3))
						{
							continue;
						}
						string element7 = param2.list.GetElement(num2++);
						if (!int.TryParse(element7, out var result4))
						{
							continue;
						}
						string element8 = param2.list.GetElement(num2++);
						if (int.TryParse(element8, out var result5))
						{
							string element9 = param2.list.GetElement(num2++);
							if (!bool.TryParse(element9, out var result6))
							{
								result6 = false;
							}
							string element10 = param2.list.GetElement(num2++);
							if (!int.TryParse(element10, out var result7))
							{
								result7 = -1;
							}
							string element11 = param2.list.GetElement(num2++);
							if (!int.TryParse(element11, out var result8))
							{
								result8 = 1;
							}
							string element12 = param2.list.GetElement(num2++);
							string element13 = param2.list.GetElement(num2++);
							string element14 = param2.list.GetElement(num2++);
							string element15 = param2.list.GetElement(num2++);
							string element16 = param2.list.GetElement(num2++);
							string element17 = param2.list.GetElement(num2++);
							if (!int.TryParse(element17, out var result9))
							{
								result9 = -1;
							}
							string element18 = param2.list.GetElement(num2++);
							if (!int.TryParse(element18, out var result10))
							{
								result10 = -1;
							}
							string element19 = param2.list.GetElement(num2++);
							if (!int.TryParse(element19, out var result11))
							{
								result11 = -1;
							}
							AIProject.EventType eventTypeMask = AIProject.Definitions.Action.EventTypeTable[result3];
							if (!value.TryGetValue(result2, out var value2))
							{
								List<DateActionPointInfo> list2 = (value[result2] = new List<DateActionPointInfo>());
								value2 = list2;
							}
							value2.Add(new DateActionPointInfo
							{
								actionName = element5,
								pointID = result2,
								eventID = result3,
								eventTypeMask = eventTypeMask,
								poseIDA = result4,
								poseIDB = result5,
								isTalkable = result6,
								iconID = result7,
								cameraID = result8,
								baseNullNameA = element12,
								baseNullNameB = element13,
								recoveryNullNameA = element14,
								recoveryNullNameB = element15,
								labelNullName = element16,
								searchAreaID = result9,
								gradeValue = result10,
								doorOpenType = result11
							});
						}
					}
				}
			}
		}

		private void LoadMerchantPoint(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.MerchantActionPointList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				ExcelData excelData = null;
				string text = null;
				string text2 = assetBundleNameListFromPath[i];
				string text3 = (text = System.IO.Path.GetFileNameWithoutExtension(text2));
				if (!AssetBundleCheck.IsFile(text2, text3))
				{
					continue;
				}
				excelData = AssetUtility.LoadAsset<ExcelData>(text2, text3, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					string s = list.GetElement(0) ?? string.Empty;
					if (!int.TryParse(s, out var result))
					{
						continue;
					}
					int num = 2;
					string element = list.GetElement(num++);
					string element2 = list.GetElement(num++);
					if (!element.IsNullOrEmpty() && !element2.IsNullOrEmpty())
					{
						switch (result)
						{
						case 0:
							LoadMerchantPointPrefabInfo(list, text);
							break;
						case 1:
							LoadMerchantPointInfo(list, text);
							break;
						case 2:
							LoadMerchantActionPointInfo(list, text);
							break;
						}
					}
				}
			}
		}

		private void LoadMerchantPointPrefabInfo(List<string> address, string id)
		{
			ExcelData excelData = null;
			int num = 2;
			string assetbundleName = address.GetElement(num++) ?? string.Empty;
			string assetName = address.GetElement(num++) ?? string.Empty;
			string manifestName = address.GetElement(num++) ?? string.Empty;
			excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 0;
				string name_ = list.GetElement(num2++) ?? string.Empty;
				string s = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string text = list.GetElement(num2++) ?? string.Empty;
				string text2 = list.GetElement(num2++) ?? string.Empty;
				string text3 = list.GetElement(num2++) ?? string.Empty;
				if (!text.IsNullOrEmpty() && !text2.IsNullOrEmpty() && !text3.IsNullOrEmpty())
				{
					if (!MerchantPointGroupTable.ContainsKey(result))
					{
						MerchantPointGroupTable[result] = new Dictionary<int, AssetBundleInfo>();
					}
					MerchantPointGroupTable[result][result2] = new AssetBundleInfo(name_, text, text2, text3);
				}
			}
		}

		private void LoadMerchantPointInfo(List<string> address, string id)
		{
			ExcelData excelData = null;
			int num = 2;
			string assetbundleName = address.GetElement(num++) ?? string.Empty;
			string assetName = address.GetElement(num++) ?? string.Empty;
			string manifestName = address.GetElement(num++) ?? string.Empty;
			excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 0;
				string s = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string text = list.GetElement(num2++) ?? string.Empty;
				string text2 = list.GetElement(num2++) ?? string.Empty;
				if (text.IsNullOrEmpty() || text2.IsNullOrEmpty())
				{
					continue;
				}
				ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(text, text2, string.Empty);
				if (!(excelData2 == null))
				{
					if (!MerchantPointInfoTable.TryGetValue(result, out var value))
					{
						Dictionary<int, List<MerchantPointInfo>> dictionary = new Dictionary<int, List<MerchantPointInfo>>();
						MerchantPointInfoTable[result] = dictionary;
						value = dictionary;
					}
					LoadMerchantPointInfoListSheet(list, id, value);
				}
			}
		}

		private void LoadMerchantPointInfoListSheet(List<string> address, string id, Dictionary<int, List<MerchantPointInfo>> dic)
		{
			ExcelData excelData = null;
			int num = 1;
			string assetbundleName = address.GetElement(num++) ?? string.Empty;
			string assetName = address.GetElement(num++) ?? string.Empty;
			string manifestName = address.GetElement(num++) ?? string.Empty;
			excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 0;
				string s = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string actionName = list.GetElement(num2++) ?? string.Empty;
				string s2 = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string value = list.GetElement(num2++) ?? string.Empty;
				if (!bool.TryParse(value, out var result4))
				{
					continue;
				}
				string value2 = list.GetElement(num2++) ?? string.Empty;
				if (bool.TryParse(value2, out var result5))
				{
					string baseNullName = list.GetElement(num2++) ?? string.Empty;
					string recoveryNullName = list.GetElement(num2++) ?? string.Empty;
					Merchant.EventType eventTypeMask = Merchant.EventTypeTable[result2];
					if (!dic.TryGetValue(result, out var value3))
					{
						List<MerchantPointInfo> list3 = (dic[result] = new List<MerchantPointInfo>());
						value3 = list3;
					}
					value3.Add(new MerchantPointInfo
					{
						actionName = actionName,
						pointID = result,
						eventID = result2,
						eventTypeMask = eventTypeMask,
						poseID = result3,
						isTalkable = result4,
						isLooking = result5,
						baseNullName = baseNullName,
						recoveryNullName = recoveryNullName
					});
				}
			}
		}

		private void LoadMerchantActionPointInfo(List<string> address, string id)
		{
			ExcelData excelData = null;
			int num = 2;
			string assetbundleName = address.GetElement(num++) ?? string.Empty;
			string assetName = address.GetElement(num++) ?? string.Empty;
			string manifestName = address.GetElement(num++) ?? string.Empty;
			excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, assetName, manifestName);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num2 = 0;
				string s = list.GetElement(num2++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string assetbundleName2 = list.GetElement(num2++) ?? string.Empty;
				string assetName2 = list.GetElement(num2++) ?? string.Empty;
				string manifestName2 = list.GetElement(num2++) ?? string.Empty;
				ExcelData excelData2 = AssetUtility.LoadAsset<ExcelData>(assetbundleName2, assetName2, manifestName2);
				if (!(excelData2 == null))
				{
					if (!MerchantActionPointInfoTable.TryGetValue(result, out var value))
					{
						Dictionary<int, List<ActionPointInfo>> dictionary = new Dictionary<int, List<ActionPointInfo>>();
						MerchantActionPointInfoTable[result] = dictionary;
						value = dictionary;
					}
					LoadActionPointInfoListSheet(excelData2, value);
				}
			}
		}

		private void LoadEventPointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.EventPointList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (!int.TryParse(s, out var result))
					{
						continue;
					}
					num++;
					string text2 = list.GetElement(num++) ?? string.Empty;
					string text3 = list.GetElement(num++) ?? string.Empty;
					if (!text2.IsNullOrEmpty() && !text3.IsNullOrEmpty())
					{
						switch (result)
						{
						case 0:
							LoadEventPointPrefabList(text2, text3);
							break;
						case 1:
							LoadEventPointCommandText(text2, text3);
							break;
						case 2:
							LoadEventDialogInfo(text2, text3);
							break;
						}
					}
				}
			}
		}

		private void LoadEventPointPrefabList(string sheetBundle, string sheetAsset)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundle, sheetAsset, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string name_ = list.GetElement(num++) ?? string.Empty;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				string text2 = list.GetElement(num++) ?? string.Empty;
				string manifest_ = list.GetElement(num++) ?? string.Empty;
				if (!text.IsNullOrEmpty() && !text2.IsNullOrEmpty())
				{
					if (!EventPointGroupTable.TryGetValue(result, out var value))
					{
						value = (EventPointGroupTable[result] = new Dictionary<int, AssetBundleInfo>());
					}
					value[result2] = new AssetBundleInfo(name_, text, text2, manifest_);
				}
			}
		}

		private void LoadEventPointCommandText(string sheetBundle, string sheetAsset)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundle, sheetAsset, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					if (!EventPointCommandLabelTextTable.TryGetValue(result, out var value) || value != null)
					{
						value = (EventPointCommandLabelTextTable[result] = new List<string>());
					}
					else
					{
						value.Clear();
					}
					while (num < list.Count)
					{
						value.Add(list.GetElement(num++) ?? string.Empty);
					}
				}
			}
		}

		private void LoadEventDialogInfo(string sheetBundle, string sheetAsset)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundle, sheetAsset, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num++;
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				if (!EventDialogInfoTable.TryGetValue(result, out var value))
				{
					value = (EventDialogInfoTable[result] = new UnityEx.ValueTuple<int, List<string>>(result2, new List<string>()));
				}
				else
				{
					if (value.Item2 == null)
					{
						value.Item2 = new List<string>();
					}
					else
					{
						value.Item2.Clear();
					}
					value.Item1 = result2;
				}
				while (num < list.Count)
				{
					value.Item2.Add(list.GetElement(num++));
				}
				EventDialogInfoTable[result] = value;
			}
		}

		private void LoadStoryPointListTable(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.StoryPointList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						num++;
						string element = list.GetElement(num++);
						string element2 = list.GetElement(num++);
						if (!element.IsNullOrEmpty() && !element2.IsNullOrEmpty() && result == 0)
						{
							LoadStoryPointPrefabList(element, element2);
						}
					}
				}
			}
		}

		private void LoadStoryPointPrefabList(string sheetBundle, string sheetAsset)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundle, sheetAsset, string.Empty);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string element = list.GetElement(num++);
				string element2 = list.GetElement(num++);
				string element3 = list.GetElement(num++);
				if (!element.IsNullOrEmpty() && !element2.IsNullOrEmpty())
				{
					if (!StoryPointGroupTable.TryGetValue(result, out var value))
					{
						value = (StoryPointGroupTable[result] = new Dictionary<int, AssetBundleInfo>());
					}
					value[result2] = new AssetBundleInfo(string.Empty, element, element2, element3);
				}
			}
		}

		private void LoadEventParticleList(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.EventParticleList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (!list.IsNullOrEmpty())
					{
						int num = 0;
						string s = list.GetElement(num++) ?? string.Empty;
						if (int.TryParse(s, out var result))
						{
							string name = list.GetElement(num++) ?? string.Empty;
							string assetbundle = list.GetElement(num++) ?? string.Empty;
							string asset = list.GetElement(num++) ?? string.Empty;
							string manifest = list.GetElement(num++) ?? string.Empty;
							EventParticleTable[result] = new AssetBundleInfo
							{
								name = name,
								assetbundle = assetbundle,
								asset = asset,
								manifest = manifest
							};
						}
					}
				}
			}
		}

		private void LoadAreaOpenStateList(DefinePack definePack)
		{
			string areaOpenStateList = definePack.ABDirectories.AreaOpenStateList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(areaOpenStateList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleInfo info = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (!int.TryParse(s, out var result))
					{
						continue;
					}
					num++;
					string text2 = list.GetElement(num++) ?? string.Empty;
					string text3 = list.GetElement(num++) ?? string.Empty;
					string sheetManifest = list.GetElement(num++) ?? string.Empty;
					if (!text2.IsNullOrEmpty() && !text3.IsNullOrEmpty())
					{
						switch (result)
						{
						case 0:
							LoadAreaOpenIDList(text2, text3, sheetManifest);
							break;
						case 1:
							LoadAreaOpenStateObjectName(text2, text3, sheetManifest);
							break;
						case 2:
							LoadAreaOpenStateMapAreaLinkerList(text2, text3, sheetManifest);
							break;
						}
					}
				}
			}
		}

		private void LoadAreaOpenIDList(string sheetBundle, string sheetAsset, string sheetManifest)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundle, sheetAsset, sheetManifest);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!list.IsNullOrEmpty())
				{
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						string value = list.GetElement(num++) ?? string.Empty;
						AreaOpenIDTable[result] = value;
					}
				}
			}
		}

		private void LoadAreaOpenStateObjectName(string sheetBundle, string sheetAsset, string sheetManifest)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundle, sheetAsset, sheetManifest);
			if (excelData == null)
			{
				return;
			}
			int key = 0;
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int j = 0;
				string s = list.GetElement(j++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					key = result;
				}
				string value = list.GetElement(j++) ?? string.Empty;
				if (!bool.TryParse(value, out var result2))
				{
					continue;
				}
				List<string> list2 = ListPool<string>.Get();
				for (; j < list.Count; j++)
				{
					string element = list.GetElement(j);
					if (!element.IsNullOrEmpty())
					{
						list2.Add(element);
					}
				}
				string[] array = new string[list2.Count];
				for (int k = 0; k < array.Length; k++)
				{
					array[k] = list2[k];
				}
				ListPool<string>.Release(list2);
				if (!AreaOpenStateObjectNameTable.TryGetValue(key, out var value2))
				{
					value2 = (AreaOpenStateObjectNameTable[key] = new Dictionary<bool, string[]>());
				}
				value2[result2] = array;
			}
		}

		private void LoadAreaOpenStateMapAreaLinkerList(string sheetBundle, string sheetAsset, string sheetManifest)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundle, sheetAsset, sheetManifest);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num++;
				string text = list.GetElement(num++) ?? string.Empty;
				string[] array = text.Split(_separationKeywords, StringSplitOptions.RemoveEmptyEntries);
				List<int> list2 = ListPool<int>.Get();
				if (!array.IsNullOrEmpty())
				{
					string[] array2 = array;
					foreach (string text2 in array2)
					{
						if (!text2.IsNullOrEmpty() && int.TryParse(text2, out var result2))
						{
							list2.Add(result2);
						}
					}
				}
				int[] array3 = new int[list2.Count];
				if (!list2.IsNullOrEmpty())
				{
					for (int k = 0; k < array3.Length; k++)
					{
						array3[k] = list2.GetElement(k);
					}
				}
				AreaOpenStateMapAreaLinkerTable[result] = array3;
				ListPool<int>.Release(list2);
			}
		}

		private void LoadTimeRelationInfoList(DefinePack definePack)
		{
			string timeRelationInfoList = definePack.ABDirectories.TimeRelationInfoList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(timeRelationInfoList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleInfo info = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (!int.TryParse(s, out var result))
					{
						continue;
					}
					num++;
					string text2 = list.GetElement(num++) ?? string.Empty;
					string text3 = list.GetElement(num++) ?? string.Empty;
					string text4 = list.GetElement(num++) ?? string.Empty;
					if (!text2.IsNullOrEmpty() && !text3.IsNullOrEmpty())
					{
						switch (result)
						{
						case 0:
							LoadTimeRelationObjectIDList(text2, text3, text4);
							break;
						case 1:
							LoadTimeRelationObjectNameList(text2, text3, text4);
							break;
						}
					}
				}
			}
		}

		private void LoadTimeRelationObjectIDList(string sheetBundleName, string sheetAssetName, string sheetManifestName)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetBundleName, sheetAssetName, sheetManifestName);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!list.IsNullOrEmpty())
				{
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						TimeRelationObjectIDTable[result] = list.GetElement(num++) ?? string.Empty;
					}
				}
			}
		}

		private void LoadTimeRelationObjectNameList(string sheetBundleName, string sheetAssetName, string manifestName)
		{
			AssetBundleInfo info = new AssetBundleInfo(string.Empty, sheetBundleName, sheetAssetName, manifestName);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int j = 0;
				string s = list.GetElement(j++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(j++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string s3 = list.GetElement(j++) ?? string.Empty;
				int result3;
				bool flag = int.TryParse(s3, out result3);
				if (!flag)
				{
					result3 = 0;
				}
				if (!TimeRelationObjectStateTable.TryGetValue(result, out var value))
				{
					value = (TimeRelationObjectStateTable[result] = new Dictionary<int, Dictionary<bool, Dictionary<int, List<UnityEx.ValueTuple<string, float>>>>>());
				}
				if (!value.TryGetValue(result2, out var value2))
				{
					value2 = (value[result2] = new Dictionary<bool, Dictionary<int, List<UnityEx.ValueTuple<string, float>>>>());
				}
				if (!value2.TryGetValue(flag, out var value3))
				{
					value3 = (value2[flag] = new Dictionary<int, List<UnityEx.ValueTuple<string, float>>>());
				}
				if (!value3.TryGetValue(result3, out var value4) || value4 == null)
				{
					value4 = (value3[result3] = new List<UnityEx.ValueTuple<string, float>>());
				}
				else
				{
					value4.Clear();
				}
				if (result2 != 0)
				{
					if (result2 != 1)
					{
						continue;
					}
					for (; j < list.Count; j++)
					{
						string element = list.GetElement(j);
						if (!element.IsNullOrEmpty())
						{
							string[] source = element.Split(Resources._separators, StringSplitOptions.RemoveEmptyEntries);
							string element2 = source.GetElement(0);
							string element3 = source.GetElement(1);
							if (!element2.IsNullOrEmpty() && !element3.IsNullOrEmpty() && float.TryParse(element3, out var result4))
							{
								value4.Add(new UnityEx.ValueTuple<string, float>(element2, result4));
							}
						}
					}
					continue;
				}
				for (; j < list.Count; j++)
				{
					string element4 = list.GetElement(j);
					if (!element4.IsNullOrEmpty())
					{
						value4.Add(new UnityEx.ValueTuple<string, float>(element4, 0f));
					}
				}
			}
		}

		private void LoadAreaGroup(DefinePack definePack)
		{
			ExcelData excelData = null;
			List<string> list = new List<string>();
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = new StringBuilder();
			List<string> list2 = new List<string>();
			int num = 1;
			int num2 = 0;
			List<int>[] array = new List<int>[2];
			list = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.AreaGroupList);
			list.Sort();
			excelData = null;
			for (int i = 0; i < list.Count; i++)
			{
				stringBuilder.Clear();
				stringBuilder.Append(list[i]);
				stringBuilder2.Clear();
				stringBuilder2.AppendFormat("map{0}", System.IO.Path.GetFileNameWithoutExtension(list[i]));
				excelData = CommonLib.LoadAsset<ExcelData>(stringBuilder.ToString(), stringBuilder2.ToString(), clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				num = 1;
				while (num < excelData.MaxCell)
				{
					list2 = excelData.list[num++].list;
					num2 = 0;
					int result = 0;
					if (!int.TryParse(list2[num2++], out result))
					{
						continue;
					}
					int result2 = 0;
					if (!int.TryParse(list2[num2++], out result2))
					{
						continue;
					}
					string[] array2 = list2[num2++].Split(',');
					array[0] = new List<int>();
					string[] array3 = array2;
					foreach (string text in array3)
					{
						if (text != string.Empty)
						{
							array[0].Add(int.Parse(text));
						}
					}
					if (list2.Count == num2)
					{
						array[1] = new List<int>();
						array[1].Add(-1);
					}
					else
					{
						string text2 = list2[num2];
						if (text2 != string.Empty)
						{
							array2 = text2.Split(',');
							array[1] = new List<int>();
							string[] array4 = array2;
							foreach (string text3 in array4)
							{
								if (text3 != string.Empty)
								{
									array[1].Add(int.Parse(text3));
								}
							}
						}
						else
						{
							array[1].Add(-1);
						}
					}
					if (!AreaGroupTable.ContainsKey(result))
					{
						AreaGroupTable.Add(result, new Dictionary<int, MinimapNavimesh.AreaGroupInfo>());
					}
					MinimapNavimesh.AreaGroupInfo areaGroupInfo = new MinimapNavimesh.AreaGroupInfo();
					areaGroupInfo.areaID = array[0];
					areaGroupInfo.OpenStateID = array[1];
					if (!AreaGroupTable[result].ContainsKey(result2))
					{
						AreaGroupTable[result].Add(result2, areaGroupInfo);
					}
					else
					{
						AreaGroupTable[result][result2] = areaGroupInfo;
					}
				}
			}
		}

		private void LoadActionCameraData(DefinePack definePack)
		{
			ExcelData excelData = null;
			List<string> list = new List<string>();
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = new StringBuilder();
			StringBuilder stringBuilder3 = new StringBuilder();
			StringBuilder stringBuilder4 = new StringBuilder();
			List<string> list2 = new List<string>();
			int num = 1;
			int num2 = 1;
			list = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.ActionCameraData);
			list.Sort();
			excelData = null;
			for (int i = 0; i < list.Count; i++)
			{
				stringBuilder.Clear();
				stringBuilder.Append(list[i]);
				stringBuilder2.Clear();
				stringBuilder2.Append(System.IO.Path.GetFileNameWithoutExtension(list[i]));
				if (!GlobalMethod.AssetFileExist(stringBuilder.ToString(), stringBuilder2.ToString(), string.Empty))
				{
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(stringBuilder.ToString(), stringBuilder2.ToString(), clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				num = 1;
				while (num < excelData.MaxCell)
				{
					list2 = excelData.list[num++].list;
					num2 = 1;
					stringBuilder3.Clear();
					stringBuilder4.Clear();
					if (!(list2[num2] == string.Empty))
					{
						stringBuilder3.Append(list2[num2++]);
						if (!(list2[num2] == string.Empty))
						{
							stringBuilder4.Append(list2[num2++]);
							LoadActionCameraData(stringBuilder3.ToString(), stringBuilder4.ToString(), stringBuilder2.ToString());
						}
					}
				}
			}
		}

		private void LoadActionCameraData(string abName, string asset, string SheetNumber)
		{
			ExcelData excelData = null;
			List<string> list = new List<string>();
			int num = 2;
			int num2 = 2;
			int result = -1;
			int result2 = -1;
			Vector3 zero = Vector3.zero;
			Vector3 zero2 = Vector3.zero;
			if (!GlobalMethod.AssetFileExist(abName, asset, string.Empty))
			{
				return;
			}
			excelData = CommonLib.LoadAsset<ExcelData>(abName, asset, clone: false, string.Empty);
			if (excelData == null)
			{
				return;
			}
			while (num < excelData.MaxCell)
			{
				ActionCameraData value = default(ActionCameraData);
				list = excelData.list[num++].list;
				num2 = 2;
				if (list.Count < 8)
				{
				}
				if (int.TryParse(list[num2++], out result) && int.TryParse(list[num2++], out result2))
				{
					if (!float.TryParse(list[num2++], out zero.x))
					{
						zero.x = 0f;
					}
					if (!float.TryParse(list[num2++], out zero.y))
					{
						zero.y = 0f;
					}
					if (!float.TryParse(list[num2++], out zero.z))
					{
						zero.z = 0f;
					}
					if (!float.TryParse(list[num2++], out zero2.x))
					{
						zero2.x = 0f;
					}
					if (!float.TryParse(list[num2++], out zero2.y))
					{
						zero2.y = 0f;
					}
					if (!float.TryParse(list[num2++], out zero2.z))
					{
						zero2.z = 0f;
					}
					value.freePos = zero;
					value.nonMovePos = zero;
					value.nonMoveRot = zero2;
					if (!ActionCameraDataTable.ContainsKey(result))
					{
						ActionCameraDataTable.Add(result, new Dictionary<int, ActionCameraData>());
					}
					if (!ActionCameraDataTable[result].ContainsKey(result2))
					{
						ActionCameraDataTable[result].Add(result2, default(ActionCameraData));
					}
					ActionCameraDataTable[result][result2] = value;
				}
			}
		}

		private void LoadVanish(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.VanishCameraList);
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				if (!GlobalMethod.AssetFileExist(text, "map_col_name", string.Empty))
				{
					continue;
				}
				ExcelData excelData = CommonLib.LoadAsset<ExcelData>(text, "map_col_name", clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 0;
				int maxCell = excelData.MaxCell;
				while (num < maxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					string abName = param.list[0];
					int result = 0;
					if (param.list.Count < 2 || !int.TryParse(param.list[1], out result))
					{
						result = 0;
					}
					LoadVanish(text, abName, result);
				}
			}
		}

		private void LoadVanish(string assetbundle, string abName, int mapID)
		{
			if (!GlobalMethod.AssetFileExist(assetbundle, abName, string.Empty))
			{
				return;
			}
			ExcelData excelData = CommonLib.LoadAsset<ExcelData>(assetbundle, abName, clone: false, string.Empty);
			if (excelData == null)
			{
				return;
			}
			List<VisibleObjectInfo> list = new List<VisibleObjectInfo>();
			int maxCell = excelData.MaxCell;
			int num = 2;
			while (num < maxCell)
			{
				ExcelData.Param param = excelData.list[num++];
				if (!param.list[0].IsNullOrEmpty())
				{
					VisibleObjectInfo visibleObjectInfo = new VisibleObjectInfo();
					string nameCollider = param.list[0];
					string vanishObjName = param.list[1];
					visibleObjectInfo.nameCollider = nameCollider;
					visibleObjectInfo.VanishObjName = vanishObjName;
					list.Add(visibleObjectInfo);
				}
			}
			if (!VanishList.ContainsKey(mapID))
			{
				VanishList.Add(mapID, new List<List<VisibleObjectInfo>>());
			}
			VanishList[mapID].Add(new List<VisibleObjectInfo>(list));
		}

		private void LoadVanishHousingGroup(DefinePack definePack)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.VanishCameraList);
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(assetBundleNameListFromPath[i]);
				if (!GlobalMethod.AssetFileExist(text, fileNameWithoutExtension, string.Empty))
				{
					continue;
				}
				ExcelData excelData = CommonLib.LoadAsset<ExcelData>(text, fileNameWithoutExtension, clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				int num = 1;
				int maxCell = excelData.MaxCell;
				while (num < maxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 0;
					int result = 0;
					if (int.TryParse(param.list[num2++], out result))
					{
						string assetbundle = param.list[num2++];
						string abName = param.list[num2++];
						LoadVanishHousingGroup(assetbundle, abName, result);
					}
				}
			}
		}

		private void LoadVanishHousingGroup(string assetbundle, string abName, int mapID)
		{
			if (!GlobalMethod.AssetFileExist(assetbundle, abName, string.Empty))
			{
				return;
			}
			ExcelData excelData = CommonLib.LoadAsset<ExcelData>(assetbundle, abName, clone: false, string.Empty);
			if (excelData == null)
			{
				return;
			}
			if (!VanishHousingAreaGroup.ContainsKey(mapID))
			{
				VanishHousingAreaGroup.Add(mapID, new Dictionary<int, List<int>>());
			}
			int maxCell = excelData.MaxCell;
			int num = 1;
			int result = 0;
			while (num < maxCell)
			{
				ExcelData.Param param = excelData.list[num++];
				int index = 0;
				List<int> list = new List<int>();
				if (param.list[index].IsNullOrEmpty() || !int.TryParse(param.list[index++], out result))
				{
					continue;
				}
				string text = param.list[index++];
				if (!text.IsNullOrEmpty())
				{
					string[] array = text.Split(',');
					string[] array2 = array;
					foreach (string s in array2)
					{
						int result2 = 0;
						if (int.TryParse(s, out result2))
						{
							list.Add(result2);
						}
					}
				}
				if (!VanishHousingAreaGroup[mapID].ContainsKey(result))
				{
					VanishHousingAreaGroup[mapID].Add(result, new List<int>());
				}
				if (list != null && list.Count > 0)
				{
					if (VanishHousingAreaGroup[mapID][result].Count != 0)
					{
						VanishHousingAreaGroup[mapID][result].Clear();
					}
					VanishHousingAreaGroup[mapID][result].AddRange(list);
				}
			}
		}

		private void LoadEnviroInfoList(DefinePack definePack)
		{
			if (definePack == null)
			{
				return;
			}
			string enviroInfoList = definePack.ABDirectories.EnviroInfoList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(enviroInfoList);
			if (assetBundleNameListFromPath.IsNullOrEmpty())
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleInfo info = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (list.IsNullOrEmpty())
					{
						continue;
					}
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (!int.TryParse(s, out var result))
					{
						continue;
					}
					string name_ = list.GetElement(num++) ?? string.Empty;
					string text2 = list.GetElement(num++) ?? string.Empty;
					string text3 = list.GetElement(num++) ?? string.Empty;
					string manifest_ = list.GetElement(num++) ?? string.Empty;
					if (!text2.IsNullOrEmpty() && !text3.IsNullOrEmpty())
					{
						AssetBundleInfo sheetInfo = new AssetBundleInfo(name_, text2, text3, manifest_);
						if (result == 0)
						{
							LoadEnvTempRangeList(sheetInfo);
						}
					}
				}
			}
		}

		private void LoadEnvTempRangeList(AssetBundleInfo sheetInfo)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					string text = list.GetElement(num++) ?? string.Empty;
					string text2 = list.GetElement(num++) ?? string.Empty;
					string manifest_ = list.GetElement(num++) ?? string.Empty;
					if (!text.IsNullOrEmpty() && !text2.IsNullOrEmpty())
					{
						LoadEnvTempRangeTable(new AssetBundleInfo(string.Empty, text, text2, manifest_), result);
					}
				}
			}
		}

		private void LoadEnvTempRangeTable(AssetBundleInfo sheetInfo, int mapID)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (list.IsNullOrEmpty())
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				if (!TempRangeTable.TryGetValue(mapID, out var value) || value == null)
				{
					Dictionary<int, Dictionary<int, List<UnityEx.ValueTuple<int, int>>>> dictionary = new Dictionary<int, Dictionary<int, List<UnityEx.ValueTuple<int, int>>>>();
					TempRangeTable[mapID] = dictionary;
					value = dictionary;
				}
				if (!value.TryGetValue(result, out var value2) || value2 == null)
				{
					Dictionary<int, List<UnityEx.ValueTuple<int, int>>> dictionary3 = (value[result] = new Dictionary<int, List<UnityEx.ValueTuple<int, int>>>());
					value2 = dictionary3;
				}
				if (!value2.TryGetValue(result2, out var value3) || value3 == null)
				{
					List<UnityEx.ValueTuple<int, int>> list3 = (value2[result2] = new List<UnityEx.ValueTuple<int, int>>());
					value3 = list3;
				}
				else
				{
					value3.Clear();
				}
				while (num < list.Count)
				{
					string s3 = list.GetElement(num++) ?? string.Empty;
					string s4 = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s3, out var result3) && int.TryParse(s4, out var result4))
					{
						value3.Add(new UnityEx.ValueTuple<int, int>(result3, result4));
					}
				}
			}
		}

		private void LoadMiniMapInfo()
		{
			ExcelData excelData = null;
			List<string> list = new List<string>();
			StringBuilder stringBuilder = new StringBuilder();
			StringBuilder stringBuilder2 = new StringBuilder();
			List<string> list2 = new List<string>();
			int num = 1;
			int num2 = 0;
			list = CommonLib.GetAssetBundleNameListFromPath(MiniMapControler.RoadPath);
			list.Sort();
			MiniMapControler.MinimapInfo minimapInfo = null;
			excelData = null;
			for (int i = 0; i < list.Count; i++)
			{
				stringBuilder.Clear();
				stringBuilder.Append(list[i]);
				stringBuilder2.Clear();
				stringBuilder2.Append(System.IO.Path.GetFileNameWithoutExtension(list[i]));
				if (!GlobalMethod.AssetFileExist(stringBuilder.ToString(), stringBuilder2.ToString(), string.Empty))
				{
					continue;
				}
				excelData = CommonLib.LoadAsset<ExcelData>(stringBuilder.ToString(), stringBuilder2.ToString(), clone: false, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				num = 1;
				while (num < excelData.MaxCell)
				{
					list2 = excelData.list[num++].list;
					num2 = 0;
					if (list2[num2] == string.Empty)
					{
						continue;
					}
					int result = -1;
					if (int.TryParse(list2[num2++], out result))
					{
						minimapInfo = new MiniMapControler.MinimapInfo();
						minimapInfo.assetPath = list2[num2++];
						minimapInfo.asset = list2[num2++];
						minimapInfo.manifest = list2[num2++];
						if (!MinimapInfoTable.ContainsKey(result))
						{
							MinimapInfoTable.Add(result, minimapInfo);
						}
						else
						{
							MinimapInfoTable[result] = minimapInfo;
						}
					}
				}
			}
		}
	}

	public class PopupInfoTable
	{
		private Dictionary<int, string[]> warningTable = new Dictionary<int, string[]>();

		private Dictionary<int, RequestInfo> requestTable = new Dictionary<int, RequestInfo>();

		private Dictionary<int, System.Tuple<string[], string[]>> requestTextTable = new Dictionary<int, System.Tuple<string[], string[]>>();

		private Dictionary<int, string[]> storySupportTable = new Dictionary<int, string[]>();

		private Dictionary<int, Sprite[]> tutorialImageTable = new Dictionary<int, Sprite[]>();

		private Dictionary<int, List<AssetBundleInfo>> tutorialImageABInfo = new Dictionary<int, List<AssetBundleInfo>>();

		private Dictionary<int, UnityEx.ValueTuple<string, GameObject[]>> tutorialPrefabTable = new Dictionary<int, UnityEx.ValueTuple<string, GameObject[]>>();

		private Dictionary<int, UnityEx.ValueTuple<string, List<AssetBundleInfo>>> tutorialPrefabABInfo = new Dictionary<int, UnityEx.ValueTuple<string, List<AssetBundleInfo>>>();

		private Dictionary<int, TutorialUIFilterInfo> tutorialFilterTable = new Dictionary<int, TutorialUIFilterInfo>();

		public ReadOnlyDictionary<int, string[]> WarningTable { get; private set; }

		public ReadOnlyDictionary<int, RequestInfo> RequestTable { get; private set; }

		public ReadOnlyDictionary<int, string[]> StorySupportTable { get; private set; }

		public ReadOnlyDictionary<int, Sprite[]> TutorialImageTable { get; private set; }

		public ReadOnlyDictionary<int, UnityEx.ValueTuple<string, GameObject[]>> TutorialPrefabTable { get; private set; }

		public ReadOnlyDictionary<int, TutorialUIFilterInfo> TutorialFilterTable { get; private set; }

		public Dictionary<int, int> RequestFlavorAdditionBorderTable { get; private set; } = new Dictionary<int, int>();


		public PopupInfoTable()
		{
			WarningTable = new ReadOnlyDictionary<int, string[]>(warningTable);
			RequestTable = new ReadOnlyDictionary<int, RequestInfo>(requestTable);
			StorySupportTable = new ReadOnlyDictionary<int, string[]>(storySupportTable);
			TutorialImageTable = new ReadOnlyDictionary<int, Sprite[]>(tutorialImageTable);
			TutorialPrefabTable = new ReadOnlyDictionary<int, UnityEx.ValueTuple<string, GameObject[]>>(tutorialPrefabTable);
			TutorialFilterTable = new ReadOnlyDictionary<int, TutorialUIFilterInfo>(tutorialFilterTable);
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}]";
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info, int _row, int _clm)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}] Row[{_row}] Clm[{_clm}]";
		}

		private AssetBundleInfo GetAssetInfo(List<string> _address, ref int _idx, bool _addSummary)
		{
			string name_ = ((!_addSummary) ? string.Empty : (_address.GetElement(_idx++) ?? string.Empty));
			return new AssetBundleInfo(name_, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty);
		}

		public void Load(DefinePack _definePack)
		{
			RequestFlavorAdditionBorderTable.Clear();
			string popupInfoList = _definePack.ABDirectories.PopupInfoList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(popupInfoList);
			if (AIProject.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			tutorialImageABInfo.Clear();
			tutorialPrefabABInfo.Clear();
			tutorialFilterTable.Clear();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(text, fileNameWithoutExtension, string.Empty);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
						switch (result)
						{
						case 0:
							LoadWarningInfo(assetInfo);
							break;
						case 1:
							LoadRequestInfo(assetInfo);
							break;
						case 2:
							LoadRequestText(assetInfo);
							break;
						case 3:
							LoadStorySupportInfo(assetInfo);
							break;
						case 4:
							LoadTutorialUIPrefab(assetInfo);
							break;
						case 5:
							LoadTutorialUIFilterInfo(assetInfo);
							break;
						}
					}
				}
			}
			List<UnityEx.ValueTuple<string, string>> toRelease = ListPool<UnityEx.ValueTuple<string, string>>.Get();
			foreach (KeyValuePair<int, List<AssetBundleInfo>> item in tutorialImageABInfo)
			{
				if (AIProject.CollectionExtensions.IsNullOrEmpty(item.Value) || (tutorialFilterTable.TryGetValue(item.Key, out var value) && !Game.isAdd01 && value.Adult))
				{
					continue;
				}
				List<Sprite> list2 = ListPool<Sprite>.Get();
				foreach (AssetBundleInfo item2 in item.Value)
				{
					Sprite sprite = ItemIconTables.LoadSpriteAsset(item2.assetbundle, item2.asset, item2.manifest);
					if (!(sprite == null))
					{
						Singleton<Resources>.Instance.AddLoadAssetBundle(item2.assetbundle, item2.manifest);
						list2.Add(sprite);
					}
				}
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list2))
				{
					Sprite[] array = new Sprite[list2.Count];
					for (int k = 0; k < array.Length; k++)
					{
						array[k] = list2[k];
					}
					tutorialImageTable[item.Key] = array;
				}
				ListPool<Sprite>.Release(list2);
			}
			ListPool<UnityEx.ValueTuple<string, string>>.Release(toRelease);
			foreach (KeyValuePair<int, UnityEx.ValueTuple<string, List<AssetBundleInfo>>> item3 in tutorialPrefabABInfo)
			{
				if (AIProject.CollectionExtensions.IsNullOrEmpty(item3.Value.Item2) || (tutorialFilterTable.TryGetValue(item3.Key, out var value2) && !Game.isAdd01 && value2.Adult))
				{
					continue;
				}
				List<GameObject> list3 = ListPool<GameObject>.Get();
				foreach (AssetBundleInfo item4 in item3.Value.Item2)
				{
					GameObject gameObject = CommonLib.LoadAsset<GameObject>(item4.assetbundle, item4.asset, clone: false, item4.manifest);
					if (!(gameObject == null))
					{
						Singleton<Resources>.Instance.AddLoadAssetBundle(item4.assetbundle, item4.manifest);
						list3.Add(gameObject);
					}
				}
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list3))
				{
					GameObject[] array2 = new GameObject[list3.Count];
					for (int l = 0; l < array2.Length; l++)
					{
						array2[l] = list3[l];
					}
					tutorialPrefabTable[item3.Key] = new UnityEx.ValueTuple<string, GameObject[]>(item3.Value.Item1, array2);
				}
				ListPool<GameObject>.Release(list3);
			}
			foreach (KeyValuePair<int, RequestInfo> item5 in requestTable)
			{
				if (!requestTextTable.TryGetValue(item5.Key, out var value3))
				{
					item5.Value.SetText(new string[2]
					{
						string.Empty,
						string.Empty
					}, new string[2]
					{
						string.Empty,
						string.Empty
					});
				}
				else
				{
					item5.Value.SetText(value3.Item1, value3.Item2);
				}
			}
			if (RequestFlavorAdditionBorderTable.IsNullOrEmpty())
			{
				return;
			}
			List<UnityEx.ValueTuple<int, int>> list4 = ListPool<UnityEx.ValueTuple<int, int>>.Get();
			foreach (KeyValuePair<int, int> item6 in RequestFlavorAdditionBorderTable)
			{
				list4.Add(new UnityEx.ValueTuple<int, int>(item6.Key, item6.Value));
			}
			RequestFlavorAdditionBorderTable.Clear();
			list4.Sort((UnityEx.ValueTuple<int, int> x1, UnityEx.ValueTuple<int, int> x2) => x1.Item1 - x2.Item1);
			foreach (UnityEx.ValueTuple<int, int> item7 in list4)
			{
				RequestFlavorAdditionBorderTable[item7.Item1] = item7.Item2;
			}
			ListPool<UnityEx.ValueTuple<int, int>>.Release(list4);
		}

		public void Release()
		{
			warningTable.Clear();
			requestTable.Clear();
			requestTextTable.Clear();
			storySupportTable.Clear();
			tutorialImageTable.Clear();
			tutorialImageABInfo.Clear();
			tutorialPrefabTable.Clear();
			tutorialPrefabABInfo.Clear();
			tutorialFilterTable.Clear();
			RequestFlavorAdditionBorderTable.Clear();
		}

		private void LoadWarningInfo(AssetBundleInfo _address)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						num++;
						string text = list.GetElement(num++) ?? string.Empty;
						string text2 = list.GetElement(num++) ?? string.Empty;
						warningTable[result] = new string[2] { text, text2 };
					}
				}
			}
		}

		private void LoadRequestInfo(AssetBundleInfo _address)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				switch (result2)
				{
				case 0:
				{
					List<System.Tuple<int, int, int>> list2 = ListPool<System.Tuple<int, int, int>>.Get();
					while (num < list.Count)
					{
						string s4 = list.GetElement(num++) ?? string.Empty;
						if (!int.TryParse(s4, out var result4))
						{
							break;
						}
						string s5 = list.GetElement(num++) ?? string.Empty;
						if (!int.TryParse(s5, out var result5))
						{
							break;
						}
						string s6 = list.GetElement(num++) ?? string.Empty;
						if (!int.TryParse(s6, out var result6))
						{
							break;
						}
						list2.Add(new System.Tuple<int, int, int>(result4, result5, result6));
					}
					if (AIProject.CollectionExtensions.IsNullOrEmpty(list2))
					{
						ListPool<System.Tuple<int, int, int>>.Release(list2);
						break;
					}
					System.Tuple<int, int, int>[] array2 = new System.Tuple<int, int, int>[list2.Count];
					for (int j = 0; j < list2.Count; j++)
					{
						array2[j] = list2[j];
					}
					requestTable[result] = new RequestInfo(result2, array2);
					ListPool<System.Tuple<int, int, int>>.Release(list2);
					break;
				}
				case 1:
					requestTable[result] = new RequestInfo(result2, null);
					break;
				case 2:
				{
					string s3 = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s3, out var result3))
					{
						System.Tuple<int, int, int>[] array = new System.Tuple<int, int, int>[1]
						{
							new System.Tuple<int, int, int>(result3, 0, 0)
						};
						requestTable[result] = new RequestInfo(result2, array);
						RequestFlavorAdditionBorderTable[result] = array[0].Item1;
					}
					break;
				}
				}
			}
		}

		private void LoadRequestText(AssetBundleInfo _address)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s2, out var result2))
				{
					switch (result2)
					{
					case 0:
					case 2:
					{
						string str5 = list.GetElement(num++) ?? string.Empty;
						num++;
						string str6 = list.GetElement(num++) ?? string.Empty;
						SetNewLine(ref str5);
						SetNewLine(ref str6);
						requestTextTable[result] = new System.Tuple<string[], string[]>(new string[2] { str5, str6 }, new string[2]);
						break;
					}
					case 1:
					{
						string str = list.GetElement(num++) ?? string.Empty;
						string str2 = list.GetElement(num++) ?? string.Empty;
						string str3 = list.GetElement(num++) ?? string.Empty;
						string str4 = list.GetElement(num++) ?? string.Empty;
						SetNewLine(ref str);
						SetNewLine(ref str2);
						SetNewLine(ref str3);
						SetNewLine(ref str4);
						requestTextTable[result] = new System.Tuple<string[], string[]>(new string[2] { str, str3 }, new string[2] { str2, str4 });
						break;
					}
					}
				}
			}
		}

		private void LoadStorySupportInfo(AssetBundleInfo _address)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					int num = 0;
					string s = list.GetElement(num++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						num += 2;
						string text = list.GetElement(num++) ?? string.Empty;
						string text2 = list.GetElement(num++) ?? string.Empty;
						storySupportTable[result] = new string[2] { text, text2 };
					}
				}
			}
		}

		private void LoadTutorialImage(AssetBundleInfo _address)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num++;
				List<UnityEx.ValueTuple<string, string, string>> list2 = ListPool<UnityEx.ValueTuple<string, string, string>>.Get();
				while (num < list.Count)
				{
					string element = list.GetElement(num++);
					string element2 = list.GetElement(num++);
					string element3 = list.GetElement(num++);
					if (!element.IsNullOrEmpty() && !element2.IsNullOrEmpty())
					{
						list2.Add(new UnityEx.ValueTuple<string, string, string>(element, element2, element3));
					}
				}
				if (!tutorialImageABInfo.TryGetValue(result, out var value) || value == null)
				{
					value = (tutorialImageABInfo[result] = new List<AssetBundleInfo>());
				}
				else
				{
					value.Clear();
				}
				foreach (UnityEx.ValueTuple<string, string, string> item in list2)
				{
					value.Add(new AssetBundleInfo(string.Empty, item.Item1, item.Item2, item.Item3));
				}
				ListPool<UnityEx.ValueTuple<string, string, string>>.Release(list2);
			}
		}

		private void LoadTutorialUIPrefab(AssetBundleInfo _address)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				List<UnityEx.ValueTuple<string, string, string>> list2 = ListPool<UnityEx.ValueTuple<string, string, string>>.Get();
				while (num < list.Count)
				{
					string element = list.GetElement(num++);
					string element2 = list.GetElement(num++);
					string element3 = list.GetElement(num++);
					if (!element.IsNullOrEmpty() && !element2.IsNullOrEmpty())
					{
						list2.Add(new UnityEx.ValueTuple<string, string, string>(element, element2, element3));
					}
				}
				if (!tutorialPrefabABInfo.TryGetValue(result, out var value))
				{
					UnityEx.ValueTuple<string, List<AssetBundleInfo>> valueTuple = new UnityEx.ValueTuple<string, List<AssetBundleInfo>>(text, new List<AssetBundleInfo>());
					tutorialPrefabABInfo[result] = valueTuple;
					value = valueTuple;
				}
				else if (value.Item2 == null)
				{
					value.Item1 = text;
					value.Item2 = new List<AssetBundleInfo>();
				}
				else
				{
					value.Item1 = text;
					value.Item2.Clear();
				}
				foreach (UnityEx.ValueTuple<string, string, string> item in list2)
				{
					value.Item2.Add(new AssetBundleInfo(string.Empty, item.Item1, item.Item2, item.Item3));
				}
				ListPool<UnityEx.ValueTuple<string, string, string>>.Release(list2);
				tutorialPrefabABInfo[result] = value;
			}
		}

		private void LoadTutorialUIFilterInfo(AssetBundleInfo _address)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					num++;
					string value = list.GetElement(num) ?? string.Empty;
					if (bool.TryParse(value, out var result2))
					{
						tutorialFilterTable[result] = new TutorialUIFilterInfo
						{
							Success = true,
							Adult = result2
						};
					}
				}
			}
		}

		private void SetNewLine(ref string str)
		{
			if (!str.IsNullOrEmpty())
			{
				str = str.Replace("\\n", "\n");
			}
		}
	}

	public class TutorialUIInfo
	{
		public class ElemInfo
		{
			public List<UnityEx.ValueTuple<AssetBundleInfo, Sprite>> spriteInfoList = new List<UnityEx.ValueTuple<AssetBundleInfo, Sprite>>();

			public string subTitle = string.Empty;

			public string flavorText = string.Empty;
		}

		public int groupID = -1;

		public int layoutType;

		public string title = string.Empty;

		public List<ElemInfo> elemList = new List<ElemInfo>();
	}

	public struct TutorialUIFilterInfo
	{
		public bool Success;

		public bool Adult;
	}

	public class SoundTable
	{
		private string[] separators = new string[2] { "/", "／" };

		private string[] timeSeparators = new string[2] { ":", "：" };

		private Regex regex = new Regex("<\\d{1,2}:[0-9]{1,2}\\/[0-9]{1,2}:[0-9]{1,2}>");

		public Dictionary<int, Dictionary<int, Dictionary<int, UnityEx.ValueTuple<int[], int[], int[]>>>> DefaultFootStepSETable { get; private set; } = new Dictionary<int, Dictionary<int, Dictionary<int, UnityEx.ValueTuple<int[], int[], int[]>>>>();


		public Dictionary<int, AssetBundleInfo> MapBGMABTable { get; private set; } = new Dictionary<int, AssetBundleInfo>();


		public Dictionary<int, Dictionary<int, SoundPlayer.MapBGMInfo>> MapBGMInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, SoundPlayer.MapBGMInfo>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> EnviroSEPrefabInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public Dictionary<int, Dictionary<int, int[]>> EnviroSEAdjacentInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, int[]>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> ReverbPrefabInfoTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public Dictionary<int, Dictionary<int, AssetBundleInfo>> ActorActionVoiceDataTable { get; private set; } = new Dictionary<int, Dictionary<int, AssetBundleInfo>>();


		public bool TryGetMapActionVoiceInfo(int personalID, int voiceID, out AssetBundleInfo info)
		{
			if (ActorActionVoiceDataTable.TryGetValue(personalID, out var value))
			{
				return value.TryGetValue(voiceID, out info);
			}
			info = default(AssetBundleInfo);
			return false;
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}]";
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info, string _ver)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}] Ver[{_ver}]";
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info, string _ver, int _row)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}] Ver[{_ver}] 行[{_row}]";
		}

		private string LogAssetBundleInfo(AssetBundleInfo _info, string _ver, int _row, int _clm)
		{
			return $"AssetBundleName[{_info.assetbundle}] AssetName[{_info.asset}] ManifestName[{_info.manifest}] Ver[{_ver}] 行[{_row}] 列[{_clm}]";
		}

		private AssetBundleInfo GetAssetInfo(List<string> _address, ref int _idx, bool _addSummary)
		{
			string name_ = ((!_addSummary) ? string.Empty : (_address.GetElement(_idx++) ?? string.Empty));
			return new AssetBundleInfo(name_, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty, _address.GetElement(_idx++) ?? string.Empty);
		}

		public void Load(DefinePack _definePack)
		{
			LoadMapBGMInfo(_definePack);
			LoadDefaultFootStep(_definePack.ABDirectories.DefaultMaleFootStepSEInfoList, 0);
			LoadDefaultFootStep(_definePack.ABDirectories.DefaultFemaleFootStepSEInfoList, 1);
			LoadEnviroInfo(_definePack);
			LoadPersonalVoiceInfo(_definePack.ABDirectories.MapActionVoiceInfoList, ActorActionVoiceDataTable);
		}

		public void Release()
		{
			DefaultFootStepSETable.Clear();
			MapBGMABTable.Clear();
			MapBGMInfoTable.Clear();
			EnviroSEPrefabInfoTable.Clear();
			EnviroSEAdjacentInfoTable.Clear();
			ReverbPrefabInfoTable.Clear();
			ActorActionVoiceDataTable.Clear();
		}

		private void LoadMapBGMInfo(DefinePack _definePack)
		{
			string mapBGMInfoList = _definePack.ABDirectories.MapBGMInfoList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(mapBGMInfoList);
			if (AIProject.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleInfo info = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
						switch (result)
						{
						case 0:
							LoadMapBGMAssetInfo(assetInfo, fileNameWithoutExtension);
							break;
						case 1:
							LoadMapBGMChunkInfo(assetInfo, fileNameWithoutExtension);
							break;
						}
					}
				}
			}
		}

		private void LoadMapBGMAssetInfo(AssetBundleInfo _address, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						MapBGMABTable[result] = GetAssetInfo(list, ref _idx, _addSummary: true);
					}
				}
			}
		}

		private void LoadMapBGMChunkInfo(AssetBundleInfo _address, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
						LoadMapBGMAreaInfoInChunk(assetInfo, _ver, result);
					}
				}
			}
		}

		private void LoadMapBGMAreaInfoInChunk(AssetBundleInfo _address, string _ver, int _chunkID)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_address);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num++;
				string s2 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s2, out var result2))
				{
					continue;
				}
				string text = list.GetElement(num++) ?? string.Empty;
				if (text.IsNullOrEmpty())
				{
					continue;
				}
				SoundPlayer.MapBGMInfo.Period[] array = LoadPeriod(text);
				if (AIProject.CollectionExtensions.IsNullOrEmpty(array))
				{
					continue;
				}
				string s3 = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s3, out var result3))
				{
					continue;
				}
				string text2 = list.GetElement(num++) ?? string.Empty;
				if (text2.IsNullOrEmpty())
				{
					continue;
				}
				SoundPlayer.MapBGMInfo.Period[] array2 = LoadPeriod(text2);
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(array2))
				{
					if (!MapBGMInfoTable.TryGetValue(_chunkID, out var value))
					{
						Dictionary<int, SoundPlayer.MapBGMInfo> dictionary = new Dictionary<int, SoundPlayer.MapBGMInfo>();
						MapBGMInfoTable[_chunkID] = dictionary;
						value = dictionary;
					}
					value[result] = new SoundPlayer.MapBGMInfo(result2, array, result3, array2);
				}
			}
		}

		private SoundPlayer.MapBGMInfo.Period[] LoadPeriod(string _timePeriodStr)
		{
			MatchCollection matchCollection = regex.Matches(_timePeriodStr);
			if (0 < matchCollection.Count)
			{
				List<DateTime> list = ListPool<DateTime>.Get();
				for (int i = 0; i < matchCollection.Count; i++)
				{
					List<DateTime> list2 = ListPool<DateTime>.Get();
					Match match = matchCollection[i];
					string text = match.Value.Replace(string.Empty, "<", ">");
					string[] array = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
					if (AIProject.CollectionExtensions.IsNullOrEmpty(array) || array.Length != 2)
					{
						continue;
					}
					for (int j = 0; j < array.Length; j++)
					{
						string text2 = array[j] ?? string.Empty;
						string[] array2 = text2.Split(timeSeparators, StringSplitOptions.RemoveEmptyEntries);
						if (AIProject.CollectionExtensions.IsNullOrEmpty(array2) || array2.Length != 2)
						{
							break;
						}
						int num = 0;
						if (!int.TryParse(array2.GetElement(num++) ?? string.Empty, out var result) || !int.TryParse(array2.GetElement(num++) ?? string.Empty, out var result2))
						{
							break;
						}
						list2.Add(new DateTime(1, 1, 1, result, result2, (j != 0) ? 59 : 0));
					}
					if (list2.Count == 2)
					{
						list.AddRange(list2);
					}
					ListPool<DateTime>.Release(list2);
				}
				SoundPlayer.MapBGMInfo.Period[] array3 = null;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list) && 0 < list.Count && list.Count % 2 == 0)
				{
					array3 = new SoundPlayer.MapBGMInfo.Period[list.Count / 2];
					for (int k = 0; k < list.Count; k += 2)
					{
						array3[k / 2].Start = list[k];
						array3[k / 2].End = list[k + 1];
					}
				}
				ListPool<DateTime>.Release(list);
				return array3;
			}
			return null;
		}

		private void LoadDefaultFootStep(string _directory, int _sexID)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_directory);
			if (AIProject.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				AssetBundleInfo info = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (!int.TryParse(s, out var result))
					{
						continue;
					}
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					if (result == 0)
					{
						if (!DefaultFootStepSETable.TryGetValue(_sexID, out var value))
						{
							value = (DefaultFootStepSETable[_sexID] = new Dictionary<int, Dictionary<int, UnityEx.ValueTuple<int[], int[], int[]>>>());
						}
						LoadDefaultFootStepList(assetInfo, fileNameWithoutExtension, value);
					}
				}
			}
		}

		private void LoadDefaultFootStepList(AssetBundleInfo _sheetInfo, string _ver, Dictionary<int, Dictionary<int, UnityEx.ValueTuple<int[], int[], int[]>>> _chunkTable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
					if (!_chunkTable.TryGetValue(result, out var value))
					{
						value = (_chunkTable[result] = new Dictionary<int, UnityEx.ValueTuple<int[], int[], int[]>>());
					}
					LoadDefaultFootStepTable(assetInfo, _ver, value);
				}
			}
		}

		private void LoadDefaultFootStepTable(AssetBundleInfo _sheetInfo, string _ver, Dictionary<int, UnityEx.ValueTuple<int[], int[], int[]>> _areaTable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num++;
				string text = list.GetElement(num++) ?? string.Empty;
				string[] array = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
				string text2 = list.GetElement(num++) ?? string.Empty;
				string[] array2 = text2.Split(separators, StringSplitOptions.RemoveEmptyEntries);
				string text3 = list.GetElement(num++) ?? string.Empty;
				string[] array3 = text3.Split(separators, StringSplitOptions.RemoveEmptyEntries);
				if (AIProject.CollectionExtensions.IsNullOrEmpty(array))
				{
					continue;
				}
				List<int> list2 = ListPool<int>.Get();
				List<int> list3 = ListPool<int>.Get();
				List<int> list4 = ListPool<int>.Get();
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(array))
				{
					string[] array4 = array;
					foreach (string s2 in array4)
					{
						if (int.TryParse(s2, out var result2))
						{
							list2.Add(result2);
						}
					}
				}
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(array2))
				{
					string[] array5 = array2;
					foreach (string s3 in array5)
					{
						if (int.TryParse(s3, out var result3))
						{
							list3.Add(result3);
						}
					}
				}
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(array3))
				{
					string[] array6 = array3;
					foreach (string s4 in array6)
					{
						if (int.TryParse(s4, out var result4))
						{
							list4.Add(result4);
						}
					}
				}
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list2))
				{
					int[] array7 = new int[list2.Count];
					int[] array8 = new int[list3.Count];
					int[] array9 = new int[list4.Count];
					for (int m = 0; m < array7.Length; m++)
					{
						array7[m] = list2[m];
					}
					for (int n = 0; n < array8.Length; n++)
					{
						array8[n] = list3[n];
					}
					for (int num2 = 0; num2 < array9.Length; num2++)
					{
						array9[num2] = list4[num2];
					}
					_areaTable[result] = new UnityEx.ValueTuple<int[], int[], int[]>(array7, array8, array9);
				}
				ListPool<int>.Release(list2);
				ListPool<int>.Release(list3);
				ListPool<int>.Release(list4);
			}
		}

		private void LoadEnviroInfo(DefinePack _definePack)
		{
			string enviroSEInfoList = _definePack.ABDirectories.EnviroSEInfoList;
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(enviroSEInfoList);
			if (AIProject.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleInfo info = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
						switch (result)
						{
						case 0:
							LoadEnviroPrefabList(assetInfo, fileNameWithoutExtension);
							break;
						case 1:
							LoadEnviroAdjacentList(assetInfo, fileNameWithoutExtension);
							break;
						case 2:
							LoadReverbInfo(assetInfo, fileNameWithoutExtension);
							break;
						}
					}
				}
			}
		}

		private void LoadEnviroPrefabList(AssetBundleInfo _sheetInfo, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
						LoadEnviroPrefabInfoTable(assetInfo, _ver, result);
					}
				}
			}
		}

		private void LoadEnviroPrefabInfoTable(AssetBundleInfo _sheetInfo, string _ver, int _chunkID)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
				if (!assetInfo.asset.IsNullOrEmpty() && !assetInfo.assetbundle.IsNullOrEmpty() && !assetInfo.manifest.IsNullOrEmpty())
				{
					if (!EnviroSEPrefabInfoTable.TryGetValue(_chunkID, out var value))
					{
						Dictionary<int, AssetBundleInfo> dictionary = new Dictionary<int, AssetBundleInfo>();
						EnviroSEPrefabInfoTable[_chunkID] = dictionary;
						value = dictionary;
					}
					value[result] = assetInfo;
				}
			}
		}

		private void LoadEnviroAdjacentList(AssetBundleInfo _sheetInfo, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
						LoadEnviroAdjacentInfoTable(assetInfo, _ver, result);
					}
				}
			}
		}

		private void LoadEnviroAdjacentInfoTable(AssetBundleInfo _sheetInfo, string _ver, int _chunkID)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int num = 0;
				string s = list.GetElement(num++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				num++;
				string text = list.GetElement(num++) ?? string.Empty;
				string[] array = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);
				if (AIProject.CollectionExtensions.IsNullOrEmpty(array))
				{
				}
				List<int> list2 = ListPool<int>.Get();
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(array))
				{
					string[] array2 = array;
					foreach (string s2 in array2)
					{
						if (int.TryParse(s2, out var result2))
						{
							list2.Add(result2);
						}
					}
				}
				int[] array3 = new int[list2.Count];
				for (int k = 0; k < array3.Length; k++)
				{
					array3[k] = list2[k];
				}
				if (!EnviroSEAdjacentInfoTable.TryGetValue(_chunkID, out var value))
				{
					value = (EnviroSEAdjacentInfoTable[_chunkID] = new Dictionary<int, int[]>());
				}
				value[result] = array3;
				ListPool<int>.Release(list2);
			}
		}

		private void LoadReverbInfo(AssetBundleInfo _sheetInfo, string _ver)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (!AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: false);
						LoadReverbPrefabTable(assetInfo, _ver, result);
					}
				}
			}
		}

		private void LoadReverbPrefabTable(AssetBundleInfo _sheetInfo, string _ver, int _chunkID)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (!int.TryParse(s, out var result))
				{
					continue;
				}
				AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
				if (!assetInfo.assetbundle.IsNullOrEmpty() && !assetInfo.asset.IsNullOrEmpty())
				{
					if (!ReverbPrefabInfoTable.TryGetValue(_chunkID, out var value))
					{
						value = (ReverbPrefabInfoTable[_chunkID] = new Dictionary<int, AssetBundleInfo>());
					}
					value[result] = assetInfo;
				}
			}
		}

		private void LoadPersonalVoiceInfo(string _directory, Dictionary<int, Dictionary<int, AssetBundleInfo>> _setTable)
		{
			List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_directory);
			if (AIProject.CollectionExtensions.IsNullOrEmpty(assetBundleNameListFromPath))
			{
				return;
			}
			assetBundleNameListFromPath.Sort();
			for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
			{
				string text = assetBundleNameListFromPath[i];
				string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(text);
				if (!AssetBundleCheck.IsFile(text, fileNameWithoutExtension))
				{
					continue;
				}
				AssetBundleInfo info = new AssetBundleInfo(string.Empty, text, fileNameWithoutExtension, string.Empty);
				ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(info);
				if (excelData == null)
				{
					continue;
				}
				for (int j = 1; j < excelData.MaxCell; j++)
				{
					List<string> list = excelData.list[j].list;
					if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
					{
						continue;
					}
					int _idx = 0;
					string s = list.GetElement(_idx++) ?? string.Empty;
					if (int.TryParse(s, out var result))
					{
						AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
						if (!_setTable.TryGetValue(result, out var value))
						{
							value = (_setTable[result] = new Dictionary<int, AssetBundleInfo>());
						}
						LoadPersonalVoiceInfo(assetInfo, value);
					}
				}
			}
		}

		private void LoadPersonalVoiceInfo(AssetBundleInfo _sheetInfo, Dictionary<int, AssetBundleInfo> _setTable)
		{
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(_sheetInfo);
			if (excelData == null)
			{
				return;
			}
			for (int i = 1; i < excelData.MaxCell; i++)
			{
				List<string> list = excelData.list[i].list;
				if (AIProject.CollectionExtensions.IsNullOrEmpty(list))
				{
					continue;
				}
				int _idx = 0;
				string s = list.GetElement(_idx++) ?? string.Empty;
				if (int.TryParse(s, out var result))
				{
					AssetBundleInfo assetInfo = GetAssetInfo(list, ref _idx, _addSummary: true);
					if (!assetInfo.assetbundle.IsNullOrEmpty() && !assetInfo.asset.IsNullOrEmpty())
					{
						_setTable[result] = assetInfo;
					}
				}
			}
		}
	}

	private static readonly string[] _separators = new string[2] { "/", "／" };

	private static readonly char[] _separationKeywords = new char[3] { ',', '<', '>' };

	private static Regex _regex = new Regex("<((?:[\\w/.]*[,]*)+)>");

	public readonly Dictionary<string, MotionIKData> MapIKData = new Dictionary<string, MotionIKData>();

	private static readonly Dictionary<string, int> DicMapIKFrameID = new Dictionary<string, int>
	{
		{ "f_t_arm_L", 0 },
		{ "f_t_elbo_L", 1 },
		{ "f_t_arm_R", 2 },
		{ "f_t_elbo_R", 3 },
		{ "f_t_leg_L", 4 },
		{ "f_t_knee_L", 5 },
		{ "f_t_leg_R", 6 },
		{ "f_t_knee_R", 7 }
	};

	[SerializeField]
	private DefinePack _definePack;

	[SerializeField]
	private SoundPack _soundPack;

	[SerializeField]
	private LocomotionProfile _locomotionProfile;

	[SerializeField]
	private PlayerProfile _playerProfile;

	[SerializeField]
	private AgentProfile _agentProfile;

	[SerializeField]
	private StatusProfile _statusProfile;

	[SerializeField]
	private CommonDefine _commonDefine;

	[SerializeField]
	private MerchantProfile _merchantProfile;

	[SerializeField]
	private FishingDefinePack _fishingDefinePack;

	[SerializeField]
	private AnimalDefinePack _animalDefinePack;

	private List<UnityEx.ValueTuple<string, string>> _loadedAssetBundles = new List<UnityEx.ValueTuple<string, string>>();

	private readonly string _mainManifestName = "abdata";

	private Dictionary<int, Rarelity> _recognizableShapeFilterTable = new Dictionary<int, Rarelity>();

	private Dictionary<int, Dictionary<int, Dictionary<int, float>>> _desireAddRateMultiTable = new Dictionary<int, Dictionary<int, Dictionary<int, float>>>();

	private Dictionary<int, UnityEx.ValueTuple<int, int>> _desireBorderTable = new Dictionary<int, UnityEx.ValueTuple<int, int>>();

	private IConnectableObservable<Unit> _loadMapResourceStream;

	public static Dictionary<string, int> StatusTagTable { get; } = new Dictionary<string, int>
	{
		["体温"] = 0,
		["機嫌"] = 1,
		["満腹"] = 2,
		["体調"] = 3,
		["生命"] = 4,
		["やる気"] = 5,
		["H"] = 6,
		["善悪"] = 7,
		["女子力"] = 10,
		["信頼"] = 11,
		["人間性"] = 12,
		["本能"] = 13,
		["変態"] = 14,
		["警戒"] = 15,
		["闇"] = 16,
		["社交"] = 17,
		["トイレ"] = 100,
		["風呂"] = 101,
		["睡眠"] = 102,
		["食事"] = 103,
		["休憩"] = 104,
		["ギフト"] = 105,
		["おねだり"] = 106,
		["寂しい"] = 107,
		["H欲"] = 108,
		["採取"] = 110,
		["遊び"] = 111,
		["料理"] = 112,
		["動物"] = 113,
		["ロケ"] = 114,
		["飲み物"] = 115
	};


	public DefinePack DefinePack
	{
		[CompilerGenerated]
		get
		{
			return _definePack;
		}
	}

	public SoundPack SoundPack
	{
		[CompilerGenerated]
		get
		{
			return _soundPack;
		}
	}

	public LocomotionProfile LocomotionProfile
	{
		[CompilerGenerated]
		get
		{
			return _locomotionProfile;
		}
	}

	public PlayerProfile PlayerProfile
	{
		[CompilerGenerated]
		get
		{
			return _playerProfile;
		}
	}

	public AgentProfile AgentProfile
	{
		[CompilerGenerated]
		get
		{
			return _agentProfile;
		}
	}

	public StatusProfile StatusProfile
	{
		[CompilerGenerated]
		get
		{
			return _statusProfile;
		}
	}

	public CommonDefine CommonDefine
	{
		[CompilerGenerated]
		get
		{
			return _commonDefine;
		}
	}

	public MerchantProfile MerchantProfile
	{
		[CompilerGenerated]
		get
		{
			return _merchantProfile;
		}
	}

	public FishingDefinePack FishingDefinePack
	{
		[CompilerGenerated]
		get
		{
			return _fishingDefinePack;
		}
	}

	public AnimalDefinePack AnimalDefinePack
	{
		[CompilerGenerated]
		get
		{
			return _animalDefinePack;
		}
	}

	public ChaFileCoordinate BathDefaultCoord { get; private set; }

	public bool LoadAssetBundle { get; set; }

	public AnimationTables Animation { get; private set; } = new AnimationTables();


	public ActionTable Action { get; private set; } = new ActionTable();


	public BehaviorTreeTables BehaviorTree { get; private set; } = new BehaviorTreeTables();


	public HSceneTables HSceneTable { get; private set; } = new HSceneTables();


	public ItemIconTables itemIconTables { get; private set; } = new ItemIconTables();


	public Dictionary<int, int> DefaultRequiredExpTablePrimal { get; private set; } = new Dictionary<int, int>();


	public FishingTable Fishing { get; private set; } = new FishingTable();


	public AnimalTables AnimalTable { get; private set; } = new AnimalTables();


	public PopupInfoTable PopupInfo { get; private set; } = new PopupInfoTable();


	public SoundTable Sound { get; private set; } = new SoundTable();


	public Dictionary<int, Dictionary<int, List<Vector3>>> WaypointDataList { get; private set; } = new Dictionary<int, Dictionary<int, List<Vector3>>>();


	public Dictionary<int, string> FeatureNameTable { get; private set; }

	public Dictionary<int, UnityEx.ValueTuple<int, int>> FeatureParameterTable { get; private set; }

	public GameInfoTables GameInfo { get; private set; } = new GameInfoTables();


	public MapTables Map { get; private set; } = new MapTables();


	public IObservable<Unit> LoadMapResourceStream
	{
		[CompilerGenerated]
		get
		{
			return _loadMapResourceStream;
		}
	}

	private static void LoadActionAnimationInfo(ExcelData actionExcel, Dictionary<int, Dictionary<int, PlayState>> dic, bool awaitable)
	{
		if (actionExcel == null)
		{
			return;
		}
		for (int i = 1; i < actionExcel.MaxCell; i++)
		{
			ExcelData.Param param = actionExcel.list[i];
			if (param.list.IsNullOrEmpty())
			{
				continue;
			}
			int num = 2;
			string element = param.list.GetElement(num++);
			string element2 = param.list.GetElement(num++);
			if (element.IsNullOrEmpty() || element2.IsNullOrEmpty())
			{
				continue;
			}
			if (!int.TryParse(element, out var result) || !int.TryParse(element2, out var result2))
			{
				Debug.LogWarning($"セルの読み込み失敗: {actionExcel.name} {i}行目 Type = {element} Pose = {element2}");
				continue;
			}
			int key = result;
			string element3 = param.list.GetElement(num++);
			if (!int.TryParse(element3, out var result3))
			{
				result3 = 0;
			}
			string element4 = param.list.GetElement(num++);
			string element5 = param.list.GetElement(num++);
			string element6 = param.list.GetElement(num++);
			string[] array = element6.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
			bool result4;
			bool isLoop = bool.TryParse(param.list.GetElement(num++), out result4) && result4;
			int result5;
			int loopMin = (int.TryParse(param.list.GetElement(num++), out result5) ? result5 : 0);
			int result6;
			int loopMax = (int.TryParse(param.list.GetElement(num++), out result6) ? result6 : 0);
			bool result7;
			bool enableFade = bool.TryParse(param.list.GetElement(num++), out result7) && result7;
			float result8;
			float fadeSecond = ((!float.TryParse(param.list.GetElement(num++), out result8)) ? 0f : result8);
			string[] array2 = param.list.GetElement(num++)?.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
			float result9;
			float fadeOutTime = ((!float.TryParse(param.list.GetElement(num++), out result9)) ? 1f : result9);
			bool result10;
			bool enableFade2 = bool.TryParse(param.list.GetElement(num++), out result10) && result10;
			float result11;
			float fadeSecond2 = ((!float.TryParse(param.list.GetElement(num++), out result11)) ? 0f : result11);
			int result12;
			int randCount = (int.TryParse(param.list.GetElement(num++), out result12) ? result12 : 0);
			int result13;
			int num2 = (int.TryParse(param.list.GetElement(num++), out result13) ? result13 : 0);
			bool result14;
			bool endEnableBlend = bool.TryParse(param.list.GetElement(num++), out result14) && result14;
			float result15;
			float endBlendRate = ((!float.TryParse(param.list.GetElement(num++), out result15)) ? 0f : result15);
			if (!dic.TryGetValue(key, out var value))
			{
				value = (dic[key] = new Dictionary<int, PlayState>());
			}
			string element7 = param.list.GetElement(num++);
			List<UnityEx.ValueTuple<string, bool, int, bool>> list = ListPool<UnityEx.ValueTuple<string, bool, int, bool>>.Get();
			MatchCollection matchCollection = _regex.Matches(element7 ?? string.Empty);
			if (matchCollection.Count > 0)
			{
				for (int j = 0; j < matchCollection.Count; j++)
				{
					Match match = matchCollection[j];
					for (int k = 0; k < match.Groups[1].Captures.Count; k++)
					{
						string value2 = match.Groups[1].Captures[k].Value;
						string[] source = value2.Split(_separationKeywords, StringSplitOptions.None);
						int num3 = 0;
						string element8 = source.GetElement(num3++);
						if (!bool.TryParse(source.GetElement(num3++), out var result16))
						{
							result16 = false;
						}
						if (!int.TryParse(source.GetElement(num3++), out var result17))
						{
							result17 = -1;
						}
						if (!bool.TryParse(source.GetElement(num3++), out var result18))
						{
							result18 = false;
						}
						list.Add(new UnityEx.ValueTuple<string, bool, int, bool>(element8, result16, result17, result18));
					}
				}
			}
			List<PlayState.PlayStateInfo> list2 = ListPool<PlayState.PlayStateInfo>.Get();
			while (num < param.list.Count)
			{
				string element9 = param.list.GetElement(num++);
				if (element9.IsNullOrEmpty())
				{
					continue;
				}
				string element10 = param.list.GetElement(num++);
				string[] array3 = param.list.GetElement(num++)?.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
				bool result19;
				bool isLoop2 = bool.TryParse(param.list.GetElement(num++), out result19) && result19;
				int result20;
				int loopMin2 = (int.TryParse(param.list.GetElement(num++), out result20) ? result20 : 0);
				int result21;
				int loopMax2 = (int.TryParse(param.list.GetElement(num++), out result21) ? result21 : 0);
				bool result22;
				bool enableFade3 = bool.TryParse(param.list.GetElement(num++), out result22) && result22;
				float result23;
				float fadeSecond3 = ((!float.TryParse(param.list.GetElement(num++), out result23)) ? 0f : result23);
				string[] array4 = param.list.GetElement(num++)?.Split(_separators, StringSplitOptions.RemoveEmptyEntries);
				float result24;
				float fadeOutTime2 = ((!float.TryParse(param.list.GetElement(num++), out result24)) ? 1f : result24);
				bool result25;
				bool enableFade4 = bool.TryParse(param.list.GetElement(num++), out result25);
				float result26;
				float fadeSecond4 = ((!float.TryParse(param.list.GetElement(num++), out result26)) ? 0f : result26);
				PlayState.PlayStateInfo playStateInfo = new PlayState.PlayStateInfo();
				playStateInfo.AssetBundleInfo = new AssetBundleInfo(string.Empty, element9, element10, string.Empty);
				playStateInfo.FadeOutTime = fadeOutTime2;
				PlayState.PlayStateInfo playStateInfo2 = playStateInfo;
				playStateInfo2.InStateInfo = new PlayState.AnimStateInfo();
				if (!array3.IsNullOrEmpty())
				{
					PlayState.Info[] array5 = new PlayState.Info[array3.Length];
					playStateInfo2.InStateInfo.StateInfos = array5;
					PlayState.Info[] array6 = array5;
					for (int l = 0; l < array6.Length; l++)
					{
						ref PlayState.Info reference = ref array6[l];
						reference = new PlayState.Info(array3[l], num2);
					}
				}
				playStateInfo2.OutStateInfo = new PlayState.AnimStateInfo();
				if (!array4.IsNullOrEmpty())
				{
					PlayState.Info[] array5 = new PlayState.Info[array4.Length];
					playStateInfo2.OutStateInfo.StateInfos = array5;
					PlayState.Info[] array7 = array5;
					for (int m = 0; m < array7.Length; m++)
					{
						ref PlayState.Info reference2 = ref array7[m];
						reference2 = new PlayState.Info(array4[m], num2);
					}
				}
				playStateInfo2.FadeOutTime = fadeOutTime2;
				playStateInfo2.InStateInfo.EnableFade = enableFade3;
				playStateInfo2.InStateInfo.FadeSecond = fadeSecond3;
				playStateInfo2.OutStateInfo.EnableFade = enableFade4;
				playStateInfo2.OutStateInfo.FadeSecond = fadeSecond4;
				playStateInfo2.IsLoop = isLoop2;
				playStateInfo2.LoopMin = loopMin2;
				playStateInfo2.LoopMax = loopMax2;
				list2.Add(playStateInfo2);
			}
			PlayState playState = new PlayState();
			playState.Layer = num2;
			playState.DirectionType = result3;
			playState.EndEnableBlend = endEnableBlend;
			playState.EndBlendRate = endBlendRate;
			playState = (value[result2] = playState);
			PlayState playState3 = playState;
			playState3.MainStateInfo.AssetBundleInfo = new AssetBundleInfo(string.Empty, element4, element5, string.Empty);
			playState3.MainStateInfo.InStateInfo = new PlayState.AnimStateInfo();
			if (!array.IsNullOrEmpty())
			{
				PlayState.Info[] array5 = new PlayState.Info[array.Length];
				playState3.MainStateInfo.InStateInfo.StateInfos = array5;
				PlayState.Info[] array8 = array5;
				for (int n = 0; n < array8.Length; n++)
				{
					ref PlayState.Info reference3 = ref array8[n];
					reference3 = new PlayState.Info(array[n], num2);
				}
			}
			playState3.MainStateInfo.OutStateInfo = new PlayState.AnimStateInfo();
			if (!array2.IsNullOrEmpty())
			{
				PlayState.Info[] array5 = new PlayState.Info[array2.Length];
				playState3.MainStateInfo.OutStateInfo.StateInfos = array5;
				PlayState.Info[] array9 = array5;
				for (int num4 = 0; num4 < array9.Length; num4++)
				{
					ref PlayState.Info reference4 = ref array9[num4];
					reference4 = new PlayState.Info(array2[num4], num2);
				}
			}
			playState3.MainStateInfo.FadeOutTime = fadeOutTime;
			playState3.MainStateInfo.InStateInfo.EnableFade = enableFade;
			playState3.MainStateInfo.InStateInfo.FadeSecond = fadeSecond;
			playState3.MainStateInfo.OutStateInfo.EnableFade = enableFade2;
			playState3.MainStateInfo.OutStateInfo.FadeSecond = fadeSecond2;
			playState3.MainStateInfo.IsLoop = isLoop;
			playState3.MainStateInfo.LoopMin = loopMin;
			playState3.MainStateInfo.LoopMax = loopMax;
			foreach (PlayState.PlayStateInfo item in list2)
			{
				playState3.SubStateInfos.Add(item);
			}
			foreach (UnityEx.ValueTuple<string, bool, int, bool> item2 in list)
			{
				playState3.AddItemInfo(new PlayState.ItemInfo
				{
					parentName = item2.Item1,
					fromEquipedItem = item2.Item2,
					itemID = item2.Item3,
					isSync = item2.Item4
				});
			}
			playState3.ActionInfo = new ActionInfo(!list2.IsNullOrEmpty(), randCount);
			ListPool<UnityEx.ValueTuple<string, bool, int, bool>>.Release(list);
			ListPool<PlayState.PlayStateInfo>.Release(list2);
		}
	}

	public void LoadMapIK(DefinePack definePack)
	{
		List<UnityEx.ValueTuple<string, string>> list = new List<UnityEx.ValueTuple<string, string>>();
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(definePack.ABDirectories.MapIKList);
		assetBundleNameListFromPath.Sort();
		for (int i = 0; i < assetBundleNameListFromPath.Count; i++)
		{
			int num = 1;
			string assetbundleName = assetBundleNameListFromPath[i];
			string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(assetBundleNameListFromPath[i]);
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(assetbundleName, fileNameWithoutExtension, string.Empty);
			if (!(excelData == null))
			{
				num = 1;
				while (num < excelData.MaxCell)
				{
					ExcelData.Param param = excelData.list[num++];
					int num2 = 0;
					list.Add(new UnityEx.ValueTuple<string, string>(param.list[num2++], param.list[num2++]));
				}
			}
		}
		LoadMapIK(list);
	}

	public void LoadMapIK(List<UnityEx.ValueTuple<string, string>> AnimatorList)
	{
		int num = ChaFileDefine.cf_bodyshapename.Length;
		float result = 0f;
		string text = "f_t_";
		int num2 = 1;
		MotionIKData.BlendWeightInfo item = default(MotionIKData.BlendWeightInfo);
		foreach (UnityEx.ValueTuple<string, string> Animator in AnimatorList)
		{
			bool flag = false;
			if (!GlobalMethod.AssetFileExist(Animator.Item1, Animator.Item2, string.Empty))
			{
				continue;
			}
			ExcelData excelData = AssetUtility.LoadAsset<ExcelData>(Animator.Item1, Animator.Item2, string.Empty);
			if (excelData == null)
			{
				continue;
			}
			MapIKData.Add(Animator.Item2, new MotionIKData());
			List<MotionIKData.State> list = new List<MotionIKData.State>();
			int num3 = 0;
			num2 = 1;
			bool flag2 = false;
			int index = -1;
			int num4 = 0;
			float num5 = -1f;
			float num6 = -1f;
			while (num2 < excelData.MaxCell)
			{
				ExcelData.Param param = excelData.list[num2++];
				int index2 = 0;
				if (param.list == null || param.list.Count <= 0)
				{
					if (num3 == 8)
					{
						num3 = 0;
					}
					continue;
				}
				if (param.list[index2] == string.Empty)
				{
					if (CheckMapIKBlankLine(param) && num3 == 8)
					{
						num3 = 0;
					}
					continue;
				}
				bool flag3 = 0 == string.Compare(text, 0, param.list[index2], 0, text.Length);
				if (num2 == 1 || (num3 == 0 && !flag3))
				{
					index = list.Count;
					list.Add(new MotionIKData.State());
					flag2 = false;
					list[index].name = param.list[index2];
					list[index].frames = new MotionIKData.Frame[8];
					continue;
				}
				index2 = 1;
				if (!flag2)
				{
					if (num3 % 2 == 0)
					{
						list[index].parts[num3 / 2].param2.target = param.list[index2++];
					}
					else
					{
						list[index].parts[num3 / 2].param3.chein = param.list[index2++];
					}
					list[index].frames[num3].editNo = 0;
					list[index].frames[num3].shapes = new MotionIKData.Shape[num];
					for (int i = 0; i < num; i++)
					{
						int shapeNo = i;
						list[index].frames[num3].shapes[i].shapeNo = shapeNo;
					}
				}
				else
				{
					index2++;
				}
				int result2 = 0;
				if (!int.TryParse(param.list[index2++], out result2))
				{
					if (flag2)
					{
						continue;
					}
					if (num3 % 2 == 0)
					{
						if (!float.TryParse(param.list[15], out result))
						{
							result = 1f;
						}
						list[index].parts[num3 / 2].param2.weightPos = result;
						if (!float.TryParse(param.list[16], out result))
						{
							result = 1f;
						}
						list[index].parts[num3 / 2].param2.weightAng = result;
					}
					else
					{
						if (!float.TryParse(param.list[15], out result))
						{
							result = 1f;
						}
						list[index].parts[num3 / 2].param3.weight = result;
					}
					num4 = 0;
					num5 = -1f;
					num6 = -1f;
					index2 = 17;
					if (num3 % 2 == 0)
					{
						if (!int.TryParse(param.list[index2++], out num4))
						{
							num4 = 0;
						}
						item.pattern = num4;
						if (!float.TryParse(param.list[index2++], out num5))
						{
							num5 = 0f;
						}
						num5 = (item.StartKey = Mathf.Clamp01(num5));
						if (!float.TryParse(param.list[index2++], out num6))
						{
							num6 = 1f;
						}
						num6 = (item.EndKey = Mathf.Clamp01(num6));
						item.shape = default(MotionIKData.Shape);
						list[index].parts[num3 / 2].param2.blendInfos[0].Add(item);
						if (!int.TryParse(param.list[index2++], out num4))
						{
							num4 = 0;
						}
						item.pattern = num4;
						if (!float.TryParse(param.list[index2++], out num5))
						{
							num5 = 0f;
						}
						num5 = Mathf.Clamp01(num5);
						item.StartKey = num5;
						if (!float.TryParse(param.list[index2++], out num6))
						{
							num6 = 1f;
						}
						num6 = Mathf.Clamp01(num6);
						item.EndKey = num6;
						list[index].parts[num3 / 2].param2.blendInfos[1].Add(item);
					}
					else
					{
						if (!int.TryParse(param.list[index2++], out num4))
						{
							num4 = 0;
						}
						item.pattern = num4;
						if (!float.TryParse(param.list[index2++], out num5))
						{
							num5 = 0f;
						}
						num5 = Mathf.Clamp01(num5);
						item.StartKey = num5;
						if (!float.TryParse(param.list[index2++], out num6))
						{
							num6 = 1f;
						}
						num6 = Mathf.Clamp01(num6);
						item.EndKey = num6;
						item.shape = default(MotionIKData.Shape);
						list[index].parts[num3 / 2].param3.blendInfos.Add(item);
						index2 += 3;
					}
					list[index].frames[num3].frameNo = num3++;
					flag2 = num3 == 8;
					continue;
				}
				float[,,] array = new float[2, 2, 3];
				if (!float.TryParse(param.list[index2++], out array[0, 0, 0]))
				{
					array[0, 0, 0] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[0, 0, 1]))
				{
					array[0, 0, 1] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[0, 0, 2]))
				{
					array[0, 0, 2] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[0, 1, 0]))
				{
					array[0, 1, 0] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[0, 1, 1]))
				{
					array[0, 1, 1] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[0, 1, 2]))
				{
					array[0, 1, 2] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[1, 0, 0]))
				{
					array[1, 0, 0] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[1, 0, 1]))
				{
					array[1, 0, 1] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[1, 0, 2]))
				{
					array[1, 0, 2] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[1, 1, 0]))
				{
					array[1, 1, 0] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[1, 1, 1]))
				{
					array[1, 1, 1] = 0f;
				}
				if (!float.TryParse(param.list[index2++], out array[1, 1, 2]))
				{
					array[1, 1, 2] = 0f;
				}
				if (!flag2)
				{
					if (num3 % 2 == 0)
					{
						if (!float.TryParse(param.list[index2++], out result))
						{
							result = 1f;
						}
						list[index].parts[num3 / 2].param2.weightPos = result;
						if (!float.TryParse(param.list[index2++], out result))
						{
							result = 1f;
						}
						list[index].parts[num3 / 2].param2.weightAng = result;
					}
					else
					{
						if (!float.TryParse(param.list[index2++], out result))
						{
							result = 1f;
						}
						list[index].parts[num3 / 2].param3.weight = result;
						index2++;
					}
				}
				else
				{
					index2 += 2;
				}
				num4 = 0;
				num5 = -1f;
				num6 = -1f;
				int num7 = 0;
				num7 = (flag2 ? DicMapIKFrameID[param.list[0]] : num3);
				if (num7 % 2 == 0)
				{
					if (!int.TryParse(param.list[index2++], out num4))
					{
						num4 = 0;
					}
					item.pattern = num4;
					if (!float.TryParse(param.list[index2++], out num5))
					{
						num5 = 0f;
					}
					num5 = (item.StartKey = Mathf.Clamp01(num5));
					if (!float.TryParse(param.list[index2++], out num6))
					{
						num6 = 1f;
					}
					num6 = (item.EndKey = Mathf.Clamp01(num6));
					item.shape = default(MotionIKData.Shape);
					item.shape.shapeNo = result2;
					item.shape.small.pos.x = array[0, 0, 0];
					item.shape.small.pos.y = array[0, 0, 1];
					item.shape.small.pos.z = array[0, 0, 2];
					item.shape.large.pos.x = array[1, 0, 0];
					item.shape.large.pos.y = array[1, 0, 1];
					item.shape.large.pos.z = array[1, 0, 2];
					list[index].parts[num7 / 2].param2.blendInfos[0].Add(item);
					if (!int.TryParse(param.list[index2++], out num4))
					{
						num4 = 0;
					}
					item.pattern = num4;
					if (!float.TryParse(param.list[index2++], out num5))
					{
						num5 = 0f;
					}
					num5 = Mathf.Clamp01(num5);
					item.StartKey = num5;
					if (!float.TryParse(param.list[index2++], out num6))
					{
						num6 = 1f;
					}
					num6 = Mathf.Clamp01(num6);
					item.EndKey = num6;
					item.shape = default(MotionIKData.Shape);
					item.shape.shapeNo = result2;
					item.shape.small.ang.x = array[0, 1, 0];
					item.shape.small.ang.y = array[0, 1, 1];
					item.shape.small.ang.z = array[0, 1, 2];
					item.shape.large.ang.x = array[1, 1, 0];
					item.shape.large.ang.y = array[1, 1, 1];
					item.shape.large.ang.z = array[1, 1, 2];
					list[index].parts[num7 / 2].param2.blendInfos[1].Add(item);
				}
				else
				{
					if (!int.TryParse(param.list[index2++], out num4))
					{
						num4 = 0;
					}
					item.pattern = num4;
					if (!float.TryParse(param.list[index2++], out num5))
					{
						num5 = 0f;
					}
					num5 = Mathf.Clamp01(num5);
					item.StartKey = num5;
					if (!float.TryParse(param.list[index2++], out num6))
					{
						num6 = 1f;
					}
					num6 = Mathf.Clamp01(num6);
					item.EndKey = num6;
					item.shape = default(MotionIKData.Shape);
					item.shape.shapeNo = result2;
					item.shape.small.pos.x = array[0, 0, 0];
					item.shape.small.pos.y = array[0, 0, 1];
					item.shape.small.pos.z = array[0, 0, 2];
					item.shape.small.ang.x = array[0, 1, 0];
					item.shape.small.ang.y = array[0, 1, 1];
					item.shape.small.ang.z = array[0, 1, 2];
					item.shape.large.pos.x = array[1, 0, 0];
					item.shape.large.pos.y = array[1, 0, 1];
					item.shape.large.pos.z = array[1, 0, 2];
					item.shape.large.ang.x = array[1, 1, 0];
					item.shape.large.ang.y = array[1, 1, 1];
					item.shape.large.ang.z = array[1, 1, 2];
					list[index].parts[num7 / 2].param3.blendInfos.Add(item);
					index2 += 3;
				}
				if (!flag2)
				{
					list[index].frames[num3].frameNo = num3++;
					flag2 = num3 == 8;
				}
			}
			MapIKData[Animator.Item2].states = new MotionIKData.State[list.Count];
			for (int j = 0; j < list.Count; j++)
			{
				MapIKData[Animator.Item2].states[j] = list[j];
			}
		}
	}

	private bool CheckMapIKBlankLine(ExcelData.Param param)
	{
		for (int i = 1; i < param.list.Count; i++)
		{
			if (param.list[i] != string.Empty)
			{
				return false;
			}
		}
		return true;
	}

	public void BeginLoadAssetBundle()
	{
		_loadedAssetBundles.Clear();
	}

	public void AddLoadAssetBundle(string assetBundleName, string manifestName)
	{
		if (manifestName.IsNullOrEmpty())
		{
			manifestName = _mainManifestName;
		}
		if (!_loadedAssetBundles.Exists((UnityEx.ValueTuple<string, string> x) => x.Item1 == assetBundleName && x.Item2 == manifestName))
		{
			_loadedAssetBundles.Add(new UnityEx.ValueTuple<string, string>(assetBundleName, manifestName));
		}
	}

	public void EndLoadAssetBundle(bool forceRemove = false)
	{
		foreach (UnityEx.ValueTuple<string, string> loadedAssetBundle in _loadedAssetBundles)
		{
			AssetBundleManager.UnloadAssetBundle(loadedAssetBundle.Item1, isUnloadForceRefCount: true, null, forceRemove);
		}
		UnityEngine.Resources.UnloadUnusedAssets();
		GC.Collect();
		_loadedAssetBundles.Clear();
	}

	public bool IsRecognizable(int lv, Rarelity rarelity)
	{
		Rarelity source = _recognizableShapeFilterTable[lv];
		return source.Contains(rarelity);
	}

	public Dictionary<int, float> GetDesireAddRateTable(int personalID, AIProject.TimeZone zone)
	{
		int key = AIProject.Definitions.Environment.TimeZoneIDTable[zone];
		if (!_desireAddRateMultiTable.TryGetValue(personalID, out var value))
		{
			return null;
		}
		if (!value.TryGetValue(key, out var value2))
		{
			Debug.Log($"<color=yellow>存在しないテーブルキー</color>: <color=red>{zone}</color>");
			return null;
		}
		return value2;
	}

	public UnityEx.ValueTuple<int, int> GetDesireBorder(int key)
	{
		if (!_desireBorderTable.TryGetValue(key, out var value))
		{
			return default(UnityEx.ValueTuple<int, int>);
		}
		return value;
	}

	protected override void Awake()
	{
		if (CheckInstance())
		{
		}
	}

	public void PreSetup()
	{
	}

	public void SetupMap()
	{
		_loadMapResourceStream = Observable.FromCoroutine(() => LoadMapResources()).PublishLast();
		_loadMapResourceStream.Connect();
	}

	public IEnumerator LoadMapResources()
	{
		BathDefaultCoord = new ChaFileCoordinate();
		TextAsset ta = CommonLib.LoadAsset<TextAsset>(_definePack.ABPaths.ActorPrefab, "bath_coord_def", clone: false, string.Empty);
		if (ta != null)
		{
			BathDefaultCoord.LoadFile(ta);
		}
		Animation.Load(_definePack);
		Action.Load(_definePack);
		BehaviorTree.Load(_definePack);
		Fishing.Load(_fishingDefinePack);
		AnimalTable.Load(_animalDefinePack);
		PopupInfo.Load(_definePack);
		Map.Load(_definePack);
		Sound.Load(_definePack);
		yield return itemIconTables.LoadIcon(ItemIconTables.IconCategory.System);
		yield return itemIconTables.LoadIcon(ItemIconTables.IconCategory.Menu);
		yield return itemIconTables.LoadIcon(ItemIconTables.IconCategory.Category);
		yield return itemIconTables.LoadIcon(ItemIconTables.IconCategory.Item);
		yield return itemIconTables.LoadMinimapActionIconList();
		yield return itemIconTables.LoadMinimapActionIconNameList(_definePack);
		GameInfo.Load(_definePack);
		itemIconTables.Load(_definePack);
		LoadWaypointData();
		LoadDesireInfo();
		yield return HSceneTable.LoadH();
		GC.Collect();
		Debug.Log("Complete: Manager.Resources.Load Process");
	}

	public void ReleaseMapResources()
	{
		Animation.Release();
		Action.Release();
		BehaviorTree.Release();
		Fishing.Release();
		AnimalTable.Release();
		PopupInfo.Release();
		Map.Release();
		Sound.Release();
		itemIconTables.Release();
		GameInfo.Release();
		WaypointDataList.Clear();
		_desireAddRateMultiTable.Clear();
		_desireBorderTable.Clear();
		HSceneTable.Release();
		GC.Collect();
		Debug.Log("Complete: Manager.Resources.Release Process");
	}

	private void LoadWaypointData()
	{
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_definePack.ABDirectories.WaypointList);
		assetBundleNameListFromPath.Sort();
		foreach (string item in assetBundleNameListFromPath)
		{
			NavMeshWayPointData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(NavMeshWayPointData)).GetAllAssets<NavMeshWayPointData>();
			foreach (NavMeshWayPointData navMeshWayPointData in allAssets)
			{
				if (!WaypointDataList.TryGetValue(navMeshWayPointData.MapID, out var value))
				{
					Dictionary<int, List<Vector3>> dictionary = new Dictionary<int, List<Vector3>>();
					WaypointDataList[navMeshWayPointData.MapID] = dictionary;
					value = dictionary;
				}
				if (!value.TryGetValue(navMeshWayPointData.AreaID, out var value2))
				{
					List<Vector3> list = new List<Vector3>();
					value[navMeshWayPointData.AreaID] = list;
					value2 = list;
				}
				value2.AddRange(navMeshWayPointData.Points);
			}
		}
	}

	private IEnumerator LoadExperience()
	{
		yield return null;
	}

	private void LoadDesireInfo()
	{
		List<string> assetBundleNameListFromPath = CommonLib.GetAssetBundleNameListFromPath(_definePack.ABDirectories.AgentDesire);
		assetBundleNameListFromPath.Sort();
		foreach (string item in assetBundleNameListFromPath)
		{
			DesireRateData[] allAssets = AssetBundleManager.LoadAllAsset(item, typeof(DesireRateData)).GetAllAssets<DesireRateData>();
			foreach (DesireRateData desireRateData in allAssets)
			{
				foreach (DesireRateData.Param item2 in desireRateData.param)
				{
					if (!_desireAddRateMultiTable.TryGetValue(item2.PersonalID, out var value))
					{
						Dictionary<int, Dictionary<int, float>> dictionary = new Dictionary<int, Dictionary<int, float>>();
						_desireAddRateMultiTable[item2.PersonalID] = dictionary;
						value = dictionary;
					}
					if (!value.TryGetValue(0, out var value2))
					{
						Dictionary<int, float> dictionary3 = (value[0] = new Dictionary<int, float>());
						value2 = dictionary3;
					}
					if (!value.TryGetValue(1, out var value3))
					{
						Dictionary<int, float> dictionary3 = (value[1] = new Dictionary<int, float>());
						value3 = dictionary3;
					}
					if (!value.TryGetValue(2, out var value4))
					{
						Dictionary<int, float> dictionary3 = (value[2] = new Dictionary<int, float>());
						value4 = dictionary3;
					}
					value2[item2.ID] = item2.Morning;
					value3[item2.ID] = item2.Day;
					value4[item2.ID] = item2.Night;
				}
			}
			DesireBorderData[] allAssets2 = AssetBundleManager.LoadAllAsset(item, typeof(DesireBorderData)).GetAllAssets<DesireBorderData>();
			foreach (DesireBorderData desireBorderData in allAssets2)
			{
				foreach (DesireBorderData.Param item3 in desireBorderData.param)
				{
					_desireBorderTable[item3.ID] = new UnityEx.ValueTuple<int, int>(item3.Border, item3.Limit);
				}
			}
			AssetBundleManager.UnloadAssetBundle(item, isUnloadForceRefCount: false);
		}
	}
}
