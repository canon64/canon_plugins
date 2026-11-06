using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace AIProject;

public class CameraConfig : MonoBehaviour
{
	[SerializeField]
	private PostProcessProfile _ppProfile;

	[SerializeField]
	private PostProcessLayer _ppLayer;

	[SerializeField]
	private EnviroSky _enviroSky;

	public PostProcessProfile PPProfile
	{
		[CompilerGenerated]
		get
		{
			return _ppProfile;
		}
	}

	public PostProcessLayer PPLayer
	{
		[CompilerGenerated]
		get
		{
			return _ppLayer;
		}
	}

	public EnviroSky EnviroSky
	{
		[CompilerGenerated]
		get
		{
			return _enviroSky;
		}
	}

	private void Reset()
	{
	}
}
