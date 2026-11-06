using System.Collections.Generic;
using AIProject;
using Manager;
using PlaceholderSoftware.WetStuff;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace ConfigScene;

[DisallowMultipleComponent]
public class ConfigEffector : MonoBehaviour
{
	public WetStuff Wetstuff;

	public PostProcessLayer PostProcessLayer;

	private List<Bloom> _bloom = new List<Bloom>();

	private List<AmbientOcclusion> _ao = new List<AmbientOcclusion>();

	private List<ScreenSpaceReflections> _ssr = new List<ScreenSpaceReflections>();

	private List<DepthOfField> _dof = new List<DepthOfField>();

	private List<Vignette> _vignette = new List<Vignette>();

	public bool isInput = true;

	private void Refresh()
	{
		GraphicSystem graphicData = Config.GraphicData;
		foreach (Bloom item in _bloom)
		{
			if (item.active != graphicData.Bloom)
			{
				item.active = graphicData.Bloom;
			}
		}
		foreach (AmbientOcclusion item2 in _ao)
		{
			if (item2.active != graphicData.SSAO)
			{
				item2.active = graphicData.SSAO;
			}
		}
		foreach (ScreenSpaceReflections item3 in _ssr)
		{
			if (item3.active != graphicData.SSR)
			{
				item3.active = graphicData.SSR;
			}
		}
		foreach (DepthOfField item4 in _dof)
		{
			if (item4.active != graphicData.DepthOfField)
			{
				item4.active = graphicData.DepthOfField;
			}
		}
		foreach (Vignette item5 in _vignette)
		{
			if (item5.active != graphicData.Vignette)
			{
				item5.active = graphicData.Vignette;
			}
		}
		if (Singleton<Map>.IsInstance() && Singleton<Map>.Instance.Simulator != null && Singleton<Map>.Instance.Simulator.EnviroSky != null)
		{
			EnviroSky enviroSky = Singleton<Map>.Instance.Simulator.EnviroSky;
			enviroSky.fogSettings.distanceFog = graphicData.Atmospheric;
			enviroSky.fogSettings.heightFog = graphicData.Atmospheric;
			enviroSky.volumeLighting = graphicData.Atmospheric;
			enviroSky.LightShafts.sunLightShafts = graphicData.Atmospheric;
			enviroSky.LightShafts.moonLightShafts = graphicData.Atmospheric;
		}
		if (Wetstuff != null && Wetstuff.enabled != graphicData.Rain)
		{
			Wetstuff.enabled = graphicData.Rain;
		}
	}

	private void Reset()
	{
		PostProcessLayer = GetComponent<PostProcessLayer>();
		Wetstuff = GetComponent<WetStuff>();
	}

	private void Awake()
	{
	}

	private void Start()
	{
		List<PostProcessVolume> list = ListPool<PostProcessVolume>.Get();
		PostProcessManager.instance.GetActiveVolumes(PostProcessLayer, list);
		foreach (PostProcessVolume item in list)
		{
			Bloom setting = item.profile.GetSetting<Bloom>();
			if ((bool)setting)
			{
				_bloom.Add(setting);
			}
			AmbientOcclusion setting2 = item.profile.GetSetting<AmbientOcclusion>();
			if (setting2 != null && setting2.active)
			{
				_ao.Add(setting2);
			}
			ScreenSpaceReflections setting3 = item.profile.GetSetting<ScreenSpaceReflections>();
			if (setting3 != null && setting3.active)
			{
				_ssr.Add(setting3);
			}
			DepthOfField setting4 = item.profile.GetSetting<DepthOfField>();
			if (setting4 != null && setting4.active)
			{
				_dof.Add(setting4);
			}
			Vignette setting5 = item.profile.GetSetting<Vignette>();
			if (setting5 != null && setting5.active)
			{
				_vignette.Add(setting5);
			}
		}
		Refresh();
	}

	private void Update()
	{
		Refresh();
	}
}
