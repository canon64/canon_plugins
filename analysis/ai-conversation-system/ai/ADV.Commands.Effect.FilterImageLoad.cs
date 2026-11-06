using Illusion.Game;
using UnityEngine.UI;

namespace ADV.Commands.Effect;

public class FilterImageLoad : CommandBase
{
	public override string[] ArgsLabel => new string[3] { "Bundle", "Asset", "Manifest" };

	public override string[] ArgsDefault => new string[3]
	{
		string.Empty,
		string.Empty,
		string.Empty
	};

	public override void Do()
	{
		base.Do();
		int num = 0;
		string bundleName = null;
		if (args.SafeProc(num++, delegate(string s)
		{
			bundleName = s;
		}))
		{
			string assetName = null;
			args.SafeProc(num++, delegate(string s)
			{
				assetName = s;
			});
			string manifest = null;
			args.SafeProc(num++, delegate(string s)
			{
				manifest = s;
			});
			string assetBundleName = bundleName;
			string assetName2 = assetName;
			Image filterImage = base.scenario.FilterImage;
			bool isTexSize = false;
			string manifest2 = manifest;
			Illusion.Game.Utils.Bundle.LoadSprite(assetBundleName, assetName2, filterImage, isTexSize, null, manifest2);
		}
		base.scenario.FilterImage.enabled = true;
	}
}
