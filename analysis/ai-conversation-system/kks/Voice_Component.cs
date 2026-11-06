using Manager;
using UnityEngine;

public class Voice_Component : MonoBehaviour
{
	public string bundle { get; private set; }

	public string asset { get; private set; }

	public Transform voiceTrans { get; private set; }

	public void Bind(Voice.Loader loader)
	{
		bundle = loader.bundle;
		asset = loader.asset;
		voiceTrans = loader.voiceTrans;
	}
}
