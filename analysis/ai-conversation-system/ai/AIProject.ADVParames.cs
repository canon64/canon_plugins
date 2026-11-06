using System.Collections.Generic;
using ADV;

namespace AIProject;

internal class ADVParames : SceneParameter
{
	public ADVParames(IData data)
		: base(data)
	{
	}

	public override void Init()
	{
		bool? flag = base.data.pack?.isCommandListVisibleEnabled;
		if (!flag.HasValue || flag.Value)
		{
			MapUIContainer.CommandList.Visibled = false;
		}
		base.data.pack?.CommandListVisibleEnabledDefault();
		List<Program.Transfer> transferList = base.data.pack?.Create() ?? Program.Transfer.NewList();
		SceneParameter.advScene.Scenario.openData.Set(base.data.openData);
		SceneParameter.advScene.Scenario.transferList = transferList;
		SceneParameter.advScene.Scenario.SetPackage(base.data.pack);
		SceneParameter.advScene.Scenario.captions.CanvasGroupON();
	}

	public override void Release()
	{
		base.data.pack?.Receive(SceneParameter.advScene.Scenario);
		SceneParameter.advScene.Scenario.SetPackage(null);
		MapUIContainer.SetActiveChoiceUI(active: false);
		Singleton<MapUIContainer>.Instance.CloseADV();
	}
}
