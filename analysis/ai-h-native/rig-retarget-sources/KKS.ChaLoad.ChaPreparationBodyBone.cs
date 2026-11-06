using System;
using UnityEngine;

namespace ChaLoad;

public class ChaPreparationBodyBone : MonoBehaviour
{
	[Serializable]
	public class BoneInfo
	{
		public string name = "";

		public GameObject gameObject;
	}

	[SerializeField]
	private Animator _animator;

	[SerializeField]
	private DynamicBone_Ver02[] _dynamicBones;

	[SerializeField]
	private GameObject _objBoneRoot;

	[SerializeField]
	private BoneInfo[] _boneInfos;

	[SerializeField]
	private NeckLookControllerVer2 _neckLookController;

	public Animator Animator => _animator;

	public DynamicBone_Ver02[] DynamicBones => _dynamicBones;

	public GameObject ObjBoneRoot => _objBoneRoot;

	public BoneInfo[] BoneInfos => _boneInfos;

	public NeckLookControllerVer2 NeckLookController => _neckLookController;
}
