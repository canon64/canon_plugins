using System;
using System.Collections.Generic;
using IllusionUtility.GetUtility;
using Manager;
using UniRx;
using UnityEngine;
using UnityEx;

public class HParticleCtrl
{
	public class ParticleInfo
	{
		public string assetPath;

		public string file;

		public string manifest;

		public int numParent;

		public string nameParent;

		public Vector3 pos;

		public Vector3 rot;

		public ParticleSystem particle;

		public UnityEx.ValueTuple<GameObject, Transform> particleCache;

		public Transform Parent;
	}

	public Transform particlePlace;

	private List<ParticleInfo> lstParticle = new List<ParticleInfo>();

	private GameObject objParent;

	private Manager.Resources.HSceneTables HSceneTables;

	private IDisposable disposable;

	private bool bUpdatePos;

	public void Init()
	{
		HSceneTables = Singleton<Manager.Resources>.Instance.HSceneTable;
		lstParticle = new List<ParticleInfo>();
		for (int i = 0; i < HSceneTables.lstHParticleCtrl.Count; i++)
		{
			lstParticle.Add(new ParticleInfo
			{
				assetPath = HSceneTables.lstHParticleCtrl[i].assetPath,
				file = HSceneTables.lstHParticleCtrl[i].file,
				manifest = HSceneTables.lstHParticleCtrl[i].manifest,
				numParent = HSceneTables.lstHParticleCtrl[i].numParent,
				nameParent = HSceneTables.lstHParticleCtrl[i].nameParent,
				pos = HSceneTables.lstHParticleCtrl[i].pos,
				rot = HSceneTables.lstHParticleCtrl[i].rot
			});
			GameObject gameObject = CommonLib.LoadAsset<GameObject>(lstParticle[i].assetPath, lstParticle[i].file, clone: true, lstParticle[i].manifest);
			Singleton<HSceneManager>.Instance.hashUseAssetBundle.Add(lstParticle[i].assetPath);
			lstParticle[i].particle = gameObject.GetComponent<ParticleSystem>();
			lstParticle[i].particleCache.Item1 = gameObject;
			lstParticle[i].particleCache.Item1.SetActive(value: false);
			lstParticle[i].particleCache.Item2 = gameObject.transform;
			lstParticle[i].particleCache.Item2.SetParent(particlePlace, worldPositionStays: false);
			lstParticle[i].particleCache.Item2.localPosition = Vector3.zero;
			lstParticle[i].particleCache.Item2.localRotation = Quaternion.identity;
			lstParticle[i].particleCache.Item2.localScale = Vector3.one;
		}
		if (disposable != null)
		{
			disposable.Dispose();
			disposable = null;
		}
		disposable = Observable.EveryEndOfFrame().Subscribe(delegate
		{
			UpdatePosition();
		});
	}

	private void UpdatePosition()
	{
		if (!bUpdatePos)
		{
			return;
		}
		for (int i = 0; i < lstParticle.Count; i++)
		{
			ParticleInfo particleInfo = lstParticle[i];
			if (particleInfo != null && !(particleInfo.Parent == null) && !(particleInfo.particleCache.Item2 == null))
			{
				particleInfo.particleCache.Item2.SetParent(particleInfo.Parent, worldPositionStays: false);
				particleInfo.particleCache.Item2.localPosition = particleInfo.pos;
				particleInfo.particleCache.Item2.localRotation = Quaternion.Euler(particleInfo.rot);
				if (particlePlace != null)
				{
					particleInfo.particleCache.Item2.SetParent(particlePlace);
				}
				else
				{
					particleInfo.particleCache.Item2.parent = null;
				}
				particleInfo.particleCache.Item2.localScale = Vector3.one;
			}
		}
	}

	public void ParticleLoad(GameObject _objBody, int _sex)
	{
		if (_objBody == null)
		{
			return;
		}
		for (int i = 0; i < lstParticle.Count; i++)
		{
			if (lstParticle[i].numParent != _sex)
			{
				continue;
			}
			objParent = _objBody.transform.FindLoop(lstParticle[i].nameParent);
			if (!(objParent == null))
			{
				lstParticle[i].particleCache.Item1.SetActive(value: true);
				lstParticle[i].particle.Stop();
				lstParticle[i].Parent = objParent.transform;
				lstParticle[i].particleCache.Item2.SetParent(objParent.transform, worldPositionStays: false);
				lstParticle[i].particleCache.Item2.localPosition = lstParticle[i].pos;
				lstParticle[i].particleCache.Item2.localRotation = Quaternion.Euler(lstParticle[i].rot);
				if (particlePlace != null)
				{
					lstParticle[i].particleCache.Item2.SetParent(particlePlace);
				}
				else
				{
					lstParticle[i].particleCache.Item2.parent = null;
				}
				lstParticle[i].particleCache.Item2.localScale = Vector3.one;
			}
		}
		bUpdatePos = true;
	}

	public bool ReleaseObject(int _sex)
	{
		for (int i = 0; i < lstParticle.Count; i++)
		{
			if (lstParticle[i] != null && lstParticle[i].numParent == _sex)
			{
				UnityEngine.Object.Destroy(lstParticle[i].particleCache.Item1);
				lstParticle[i].particle = null;
				lstParticle[i].particleCache.Item1 = null;
				lstParticle[i].particleCache.Item2 = null;
			}
		}
		return true;
	}

	public bool ReleaseObject()
	{
		for (int i = 0; i < lstParticle.Count; i++)
		{
			if (lstParticle[i] != null && !(lstParticle[i].particle == null))
			{
				UnityEngine.Object.Destroy(lstParticle[i].particleCache.Item1);
				lstParticle[i].particle = null;
				lstParticle[i].particleCache.Item1 = null;
				lstParticle[i].particleCache.Item2 = null;
			}
		}
		bUpdatePos = false;
		if (disposable != null)
		{
			disposable.Dispose();
			disposable = null;
		}
		return true;
	}

	public bool RePlaceObject()
	{
		for (int i = 0; i < lstParticle.Count; i++)
		{
			if (lstParticle[i] != null && !(lstParticle[i].particle == null) && lstParticle[i].particleCache.Item2.parent != particlePlace)
			{
				lstParticle[i].particleCache.Item1.SetActive(value: false);
				lstParticle[i].particleCache.Item2.SetParent(particlePlace, worldPositionStays: false);
				lstParticle[i].particleCache.Item2.localPosition = Vector3.zero;
				lstParticle[i].particleCache.Item2.localRotation = Quaternion.identity;
				lstParticle[i].particleCache.Item2.localScale = Vector3.one;
			}
		}
		return true;
	}

	public bool Play(int _particle)
	{
		if (lstParticle.Count <= _particle)
		{
			return false;
		}
		if (lstParticle[_particle].particle == null)
		{
			return false;
		}
		lstParticle[_particle].particle.Simulate(0f);
		lstParticle[_particle].particle.Play();
		return true;
	}

	public bool IsPlaying(int _particle)
	{
		if (lstParticle.Count <= _particle)
		{
			return false;
		}
		if (lstParticle[_particle].particle == null)
		{
			return false;
		}
		return lstParticle[_particle].particle.isPlaying;
	}
}
