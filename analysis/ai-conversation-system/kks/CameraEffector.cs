using UnityEngine;
using UnityStandardAssets.ImageEffects;

public class CameraEffector : MonoBehaviour
{
	[SerializeField]
	private GlobalFog _fog;

	[SerializeField]
	private AmplifyColorEffect _amplifyColor;

	[SerializeField]
	private AmplifyOcclusionEffect _amplifyOcclus;

	[SerializeField]
	private BloomAndFlares _bloom;

	[SerializeField]
	private SunShafts _sunShafts;

	[SerializeField]
	private VignetteAndChromaticAberration _vignette;

	[SerializeField]
	private DepthOfField _dof;

	[SerializeField]
	private Blur _blur;

	[SerializeField]
	private CrossFade _crossFade;

	[SerializeField]
	private SepiaTone _sepia;

	[SerializeField]
	private Transform _dofTarget;

	private CameraEffectorConfig _config;

	public bool useFog
	{
		get
		{
			if (!(_fog == null))
			{
				return _fog.enabled;
			}
			return false;
		}
		set
		{
			if (!(_fog == null))
			{
				_fog.enabled = value;
			}
		}
	}

	public bool useAmplifyColor
	{
		get
		{
			if (!(_amplifyColor == null))
			{
				return _amplifyColor.enabled;
			}
			return false;
		}
		set
		{
			if (!(_amplifyColor == null))
			{
				_amplifyColor.enabled = value;
			}
		}
	}

	public bool useAmplifyOcclus
	{
		get
		{
			if (!(_amplifyOcclus == null))
			{
				return _amplifyOcclus.enabled;
			}
			return false;
		}
		set
		{
			if (!(_amplifyOcclus == null))
			{
				_amplifyOcclus.enabled = value;
			}
		}
	}

	public bool useBloom
	{
		get
		{
			if (!(_bloom == null))
			{
				return _bloom.enabled;
			}
			return false;
		}
		set
		{
			if (!(_bloom == null))
			{
				_bloom.enabled = value;
			}
		}
	}

	public bool useSunShafts
	{
		get
		{
			if (!(_sunShafts == null))
			{
				return _sunShafts.enabled;
			}
			return false;
		}
		set
		{
			if (!(_sunShafts == null))
			{
				_sunShafts.enabled = value;
			}
		}
	}

	public bool useVignette
	{
		get
		{
			if (!(_vignette == null))
			{
				return _vignette.enabled;
			}
			return false;
		}
		set
		{
			if (!(_vignette == null))
			{
				_vignette.enabled = value;
			}
		}
	}

	public bool useDOF
	{
		get
		{
			if (!(_dof == null))
			{
				return _dof.enabled;
			}
			return false;
		}
		set
		{
			if (!(_dof == null))
			{
				_dof.enabled = value;
			}
		}
	}

	public bool useBlur
	{
		get
		{
			if (!(_blur == null))
			{
				return _blur.enabled;
			}
			return false;
		}
		set
		{
			if (!(_blur == null))
			{
				_blur.enabled = value;
			}
		}
	}

	public bool useCrossFade
	{
		get
		{
			if (!(_crossFade == null))
			{
				return _crossFade.enabled;
			}
			return false;
		}
		set
		{
			if (!(_crossFade == null))
			{
				_crossFade.enabled = value;
			}
		}
	}

	public bool useSepia
	{
		get
		{
			if (!(_sepia == null))
			{
				return _sepia.enabled;
			}
			return false;
		}
		set
		{
			if (!(_sepia == null))
			{
				_sepia.enabled = value;
			}
		}
	}

	public GlobalFog fog => _fog;

	public AmplifyColorEffect amplifyColor => _amplifyColor;

	public AmplifyOcclusionEffect amplifyOcclus => _amplifyOcclus;

	public BloomAndFlares bloom => _bloom;

	public SunShafts sunShafts => _sunShafts;

	public VignetteAndChromaticAberration vignette => _vignette;

	public DepthOfField dof => _dof;

	public Blur blur => _blur;

	public CrossFade crossFade => _crossFade;

	public SepiaTone sepia => _sepia;

	public CameraEffectorConfig config => _config;

	public Transform dofTarget => _dofTarget;

	public void SetDOFTarget(Transform target)
	{
		if (!(_dof == null))
		{
			_dof.focalTransform = target;
		}
	}

	public void SetDOFTargetCamera()
	{
		if (!(_dofTarget == null))
		{
			SetDOFTarget(_dofTarget);
		}
	}

	private void Awake()
	{
		_config = GetComponent<CameraEffectorConfig>();
	}

	[ContextMenu("Setup")]
	private void Setup()
	{
		_fog = GetComponent<GlobalFog>();
		_amplifyColor = GetComponent<AmplifyColorEffect>();
		_amplifyOcclus = GetComponent<AmplifyOcclusionEffect>();
		_bloom = GetComponent<BloomAndFlares>();
		_sunShafts = GetComponent<SunShafts>();
		_vignette = GetComponent<VignetteAndChromaticAberration>();
		_dof = GetComponent<DepthOfField>();
		_blur = GetComponent<Blur>();
		_crossFade = GetComponent<CrossFade>();
		_sepia = GetComponent<SepiaTone>();
	}
}
