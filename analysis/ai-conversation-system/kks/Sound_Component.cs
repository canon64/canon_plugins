using Manager;
using UnityEngine;

public class Sound_Component : MonoBehaviour
{
	public string bundle { get; private set; }

	public string asset { get; private set; }

	public void Bind(Manager.Sound.Loader loader)
	{
		bundle = loader.bundle;
		asset = loader.asset;
	}
}
