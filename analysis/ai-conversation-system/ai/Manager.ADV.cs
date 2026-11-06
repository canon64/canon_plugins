using AIProject;
using AIProject.CaptionScript;
using Cinemachine;
using IllusionUtility.GetUtility;
using UniRx;
using UnityEngine;
using UnityEx;

namespace Manager;

public class ADV : Singleton<ADV>
{
	public AgentActor TargetCharacter { get; set; }

	public MerchantActor TargetMerchant { get; set; }

	public Captions Captions { get; set; }

	public AssetBundleInfo AssetBundleInfo { get; set; }

	public bool UsedBGM { get; set; }

	protected override void Awake()
	{
		if (CheckInstance())
		{
			Object.DontDestroyOnLoad(base.gameObject);
		}
	}

	public static void ChangeADVCamera(Actor actor)
	{
		PlayerActor player = Singleton<Map>.Instance.Player;
		Vector3 position = player.Position;
		Vector3 pB = actor.Position;
		position.y = (pB.y = 0f);
		Vector3 forward = Vector3.Normalize(pB - position);
		if (Mathf.Approximately(forward.magnitude, 0f))
		{
			forward = Vector3.back;
		}
		Quaternion rotation = Quaternion.LookRotation(forward);
		GameObject gameObject = actor.transform.FindLoop("cf_J_Mune00");
		Transform target = gameObject.transform;
		Transform cameraTarget = player.CameraTarget.transform;
		Vector3 offset = Singleton<Resources>.Instance.LocomotionProfile.CommunicationOffset;
		Observable.EveryLateUpdate().Take(1).Subscribe(delegate
		{
			cameraTarget.position = new Vector3(pB.x, target.position.y, pB.z);
			if (player.CameraControl.VanishControl != null)
			{
				player.CameraControl.VanishControl.LookAtPosition = cameraTarget.position;
			}
			player.CameraControl.ADVCamera.LookAt = cameraTarget;
			player.CameraControl.ADVCamera.transform.position = cameraTarget.position + rotation * offset;
			player.CameraControl.Mode = CameraMode.ADV;
		});
	}

	public static void ChangeADVCameraDiagonal(Actor actor)
	{
		Observable.EveryLateUpdate().Take(1).Subscribe(delegate
		{
			PlayerActor player = Singleton<Map>.Instance.Player;
			string communicationDiagonalTargetName = Singleton<Resources>.Instance.LocomotionProfile.CommunicationDiagonalTargetName;
			GameObject gameObject = player.transform.FindLoop(communicationDiagonalTargetName);
			Transform transform = gameObject.transform;
			GameObject gameObject2 = actor.transform.FindLoop("cf_J_Mune00");
			Transform transform2 = gameObject2.transform;
			Vector3 vector = Vector3.Normalize(transform.position - transform2.position);
			if (Mathf.Approximately(vector.magnitude, 0f))
			{
				vector = Vector3.back;
			}
			Transform transform3 = player.CameraTarget.transform;
			transform3.position = transform2.position;
			if (player.CameraControl.VanishControl != null)
			{
				player.CameraControl.VanishControl.LookAtPosition = transform3.position;
			}
			player.CameraControl.ADVCamera.LookAt = transform3;
			player.CameraControl.ADVCamera.transform.position = transform2.position + vector * 8f;
			player.CameraControl.Mode = CameraMode.ADV;
		});
	}

	public static void ChangeADVFixedAngleCamera(Actor actor, int attitudeID)
	{
		PlayerActor player = Singleton<Map>.Instance.Player;
		Observable.EveryLateUpdate().Take(1).Subscribe(delegate
		{
			GameObject gameObject = actor.ChaControl.animBody.transform.FindLoop("cf_J_Mune00");
			if (gameObject != null)
			{
				Transform aDVNotStandRoot = player.CameraControl.ADVNotStandRoot;
				aDVNotStandRoot.position = gameObject.transform.position;
				aDVNotStandRoot.rotation = actor.Rotation;
				if (player.CameraControl.VanishControl != null)
				{
					player.CameraControl.VanishControl.LookAtPosition = gameObject.transform.position;
				}
			}
			if (Singleton<Resources>.Instance.Action.ComCameraList.TryGetValue(attitudeID, out var value))
			{
				string text = CommonLib.LoadAsset<TextAsset>(value.AssetBundle, value.AssetFile, clone: false, string.Empty)?.text;
				if (text.IsNullOrEmpty())
				{
					GlobalMethod.DebugLog("cameraファイルが読み込めません", 1);
				}
				else
				{
					GlobalMethod.GetListString(text, out var data);
					Vector3 localPosition = default(Vector3);
					localPosition.x = ((!float.TryParse(data[0][0], out var result)) ? 0f : result);
					localPosition.y = ((!float.TryParse(data[1][0], out result)) ? 0f : result);
					localPosition.z = ((!float.TryParse(data[2][0], out result)) ? 0f : result);
					Quaternion localRotation = default(Quaternion);
					localRotation.x = ((!float.TryParse(data[3][0], out result)) ? 0f : result);
					localRotation.y = ((!float.TryParse(data[4][0], out result)) ? 0f : result);
					localRotation.z = ((!float.TryParse(data[5][0], out result)) ? 0f : result);
					localRotation.w = ((!float.TryParse(data[6][0], out result)) ? 0f : result);
					CinemachineVirtualCameraBase aDVNotStandCamera = player.CameraControl.ADVNotStandCamera;
					aDVNotStandCamera.transform.localPosition = localPosition;
					aDVNotStandCamera.transform.localRotation = localRotation;
					CinemachineVirtualCamera cinemachineVirtualCamera = aDVNotStandCamera as CinemachineVirtualCamera;
					if (cinemachineVirtualCamera != null && float.TryParse(data[7][0], out var result2))
					{
						cinemachineVirtualCamera.m_Lens.FieldOfView = result2;
					}
					player.CameraControl.Mode = CameraMode.ADVExceptStand;
				}
			}
		}, delegate
		{
		}, delegate
		{
		});
	}
}
