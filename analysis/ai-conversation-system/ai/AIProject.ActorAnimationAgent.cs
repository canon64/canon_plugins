using System;
using BehaviorDesigner.Runtime.Tasks;
using IllusionUtility.GetUtility;
using Manager;
using RootMotion.FinalIK;
using UniRx;
using UniRx.Triggers;
using UnityEngine;

namespace AIProject;

public class ActorAnimationAgent : ActorAnimation
{
	[SerializeField]
	private FullBodyBipedIK _ik;

	private Subject<Unit> _endAction;

	private Subject<Unit> _actionPlay;

	private Subject<Unit> _completeAction;

	private float _currentVelocity;

	public FullBodyBipedIK IK
	{
		get
		{
			return _ik;
		}
		set
		{
			_ik = value;
			if ((bool)_ik)
			{
				IKSolverFullBodyBiped solver = _ik.solver;
				solver.OnPostUpdate = (IKSolver.UpdateDelegate)Delegate.Combine(solver.OnPostUpdate, new IKSolver.UpdateDelegate(AfterFBBIK));
				_ik.enabled = true;
			}
		}
	}

	protected override void Start()
	{
		base.Start();
		if (_ik == null)
		{
			_ik = GetComponentInChildren<FullBodyBipedIK>(includeInactive: true);
			if ((bool)_ik)
			{
				_ik.enabled = true;
				IKSolverFullBodyBiped solver = _ik.solver;
				solver.OnPostUpdate = (IKSolver.UpdateDelegate)Delegate.Combine(solver.OnPostUpdate, new IKSolver.UpdateDelegate(AfterFBBIK));
			}
		}
		if (Animator == null)
		{
			Animator = GetComponentInChildren<Animator>();
		}
		(from _ in Observable.EveryLateUpdate().TakeUntilDestroy(base.gameObject)
			where base.isActiveAndEnabled
			select _).Subscribe(delegate
		{
			OnLateUpdate();
		});
	}

	protected void OnAnimatorMove()
	{
		base.Character.transform.localPosition += Animator.deltaPosition;
		base.Character.transform.localRotation *= Animator.deltaRotation;
	}

	public override Vector3 GetPivotPoint()
	{
		return base.transform.position;
	}

	public IObservable<Unit> OnEndActionAsObservable()
	{
		return _endAction ?? (_endAction = new Subject<Unit>());
	}

	public IObservable<Unit> OnActionPlayAsObservable()
	{
		return _actionPlay ?? (_actionPlay = new Subject<Unit>());
	}

	public IObservable<Unit> OnCompleteActionAsObservable()
	{
		return _completeAction ?? (_completeAction = new Subject<Unit>());
	}

	public TaskStatus OnUpdateActionState()
	{
		if (base.PlayingInAnimation)
		{
			return TaskStatus.Running;
		}
		ActorAnimInfo animInfo = base.AnimInfo;
		if (animInfo.isLoop)
		{
			if (base.PlayingActAnimation)
			{
				return TaskStatus.Running;
			}
			AgentActor agentActor = base.Actor as AgentActor;
			if (!agentActor.Schedule.enabled)
			{
				if (_endAction != null)
				{
					_endAction.OnNext(Unit.Default);
				}
				if (base.PlayingOutAnimation)
				{
					return TaskStatus.Running;
				}
				if (_completeAction != null)
				{
					_completeAction.OnNext(Unit.Default);
				}
				return TaskStatus.Success;
			}
			AnimatorStateInfo currentAnimatorStateInfo = Animator.GetCurrentAnimatorStateInfo(0);
			if (currentAnimatorStateInfo.IsName(animInfo.loopStateName))
			{
				float num = currentAnimatorStateInfo.normalizedTime - animInfo.oldNormalizedTime;
				if (num > 1f)
				{
					animInfo.oldNormalizedTime = currentAnimatorStateInfo.normalizedTime;
					if (UnityEngine.Random.Range(0, animInfo.randomCount) == 0)
					{
						if (_actionPlay != null)
						{
							_actionPlay.OnNext(Unit.Default);
						}
						animInfo.oldNormalizedTime = 0f;
					}
				}
			}
			return TaskStatus.Running;
		}
		if (_endAction != null)
		{
			_endAction.OnNext(Unit.Default);
		}
		if (base.PlayingOutAnimation)
		{
			return TaskStatus.Running;
		}
		if (_completeAction != null)
		{
			_completeAction.OnNext(Unit.Default);
		}
		return TaskStatus.Success;
	}

	protected override void LoadMatchTargetInfo(string stateName)
	{
		int key = Animator.StringToHash(stateName);
		if (!Singleton<Manager.Resources>.Instance.Animation.AgentMoveInfoTable.TryGetValue(key, out var value))
		{
			return;
		}
		foreach (AnimeMoveInfo item in value)
		{
			GameObject gameObject = base.Actor.CurrentPoint.transform.FindLoop(item.movePoint);
			ProceduralTargetParameter proceduralTargetParameter = new ProceduralTargetParameter();
			proceduralTargetParameter.Start = item.start;
			proceduralTargetParameter.End = item.end;
			ProceduralTargetParameter proceduralTargetParameter2 = proceduralTargetParameter;
			if (gameObject != null)
			{
				proceduralTargetParameter2.Target = gameObject.transform;
			}
			base.Targets.Add(proceduralTargetParameter2);
		}
	}

	protected override void LoadStateLocomotionVoice(int stateHashName)
	{
		AgentActor agentActor = base.Actor as AgentActor;
		if (agentActor == null)
		{
			return;
		}
		Manager.Resources.ActionTable action = Singleton<Manager.Resources>.Instance.Action;
		if (action.AgentLocomotionBreathTable.TryGetValue(stateHashName, out var value) && (_loopActionVoice.Item1 != value || !(_loopActionVoice.Item2 != null)))
		{
			int personality = base.Actor.ChaControl.fileParam.personality;
			if (Singleton<Manager.Resources>.Instance.Sound.TryGetMapActionVoiceInfo(personality, value, out var info))
			{
				Transform transform = base.Actor.Locomotor.transform;
				Voice instance = Singleton<Voice>.Instance;
				int no = personality;
				string assetbundle = info.assetbundle;
				string asset = info.asset;
				Transform voiceTrans = transform;
				Transform transform2 = instance.OnecePlayChara(no, assetbundle, asset, 1f, 0f, 0f, isAsync: true, voiceTrans, Voice.Type.PCM, -1, isPlayEndDelete: false);
				base.Actor.ChaControl.SetVoiceTransform(transform2);
				_loopActionVoice.Item1 = value;
				_loopActionVoice.Item2 = transform2.GetComponent<AudioSource>();
				_loopActionVoice.Item3 = transform;
				_loopActionVoice.Item4 = personality;
			}
		}
	}

	protected override void LoadStateExpression(int stateHashName)
	{
		Manager.Resources.ActionTable action = Singleton<Manager.Resources>.Instance.Action;
		base.ExpressionKeyframeList.Clear();
		int personality = base.Actor.ChaControl.fileParam.personality;
		if (action.ActionExpressionKeyframeTable.TryGetValue(personality, out var value) && value.TryGetValue(stateHashName, out var value2))
		{
			foreach (ExpressionKeyframe item in value2)
			{
				base.ExpressionKeyframeList.Add(new ExpressionKeyframeEvent
				{
					NormalizedTime = item.normalizedTime,
					Name = item.expressionName
				});
			}
			return;
		}
		if (Singleton<Manager.Resources>.Instance.Action.ActionExpressionTable.TryGetValue(personality, out var value3) && value3.TryGetValue(stateHashName, out var value4))
		{
			Singleton<Game>.Instance.GetExpression(personality, value4)?.Change(base.Actor.ChaControl);
		}
	}

	protected override void UpdateExpressionEvent(float normalizedTime, bool isLoop)
	{
		if (isLoop)
		{
			foreach (ExpressionKeyframeEvent expressionKeyframe in base.ExpressionKeyframeList)
			{
				expressionKeyframe.Used = false;
			}
		}
		else
		{
			foreach (ExpressionKeyframeEvent expressionKeyframe2 in base.ExpressionKeyframeList)
			{
				if (expressionKeyframe2.NormalizedTime > normalizedTime && expressionKeyframe2.Used)
				{
					expressionKeyframe2.Used = false;
				}
			}
		}
		int personality = base.Actor.ChaControl.fileParam.personality;
		foreach (ExpressionKeyframeEvent expressionKeyframe3 in base.ExpressionKeyframeList)
		{
			if (expressionKeyframe3.NormalizedTime < normalizedTime && !expressionKeyframe3.Used)
			{
				expressionKeyframe3.Used = true;
				Singleton<Game>.Instance.GetExpression(personality, expressionKeyframe3.Name)?.Change(base.Actor.ChaControl);
			}
		}
	}

	public override void LoadEventKeyTable(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.AgentItemEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			base.ItemEventKeyTable = value2;
		}
		if (Singleton<Manager.Resources>.Instance.Animation.AgentChangeClothEventKeyTable.TryGetValue(eventID, out value) && value.TryGetValue(poseID, out var value3))
		{
			base.ClothEventKeyTable = value3;
		}
		LoadSEEventKeyTable(eventID, poseID);
		LoadParticleEventKeyTable(eventID, poseID);
		LoadOnceVoiceEventKeyTable(eventID, poseID);
		LoadLoopVoiceEventKeyTable(eventID, poseID);
	}

	public override void LoadAnimalEventKeyTable(int animalTypeID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.AgentAnimalEventKeyTable.TryGetValue(animalTypeID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			base.ItemEventKeyTable = value2;
		}
	}

	public override void LoadSEEventKeyTable(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.AgentActSEEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			base.SEEventKeyTable = value2;
		}
		else
		{
			base.SEEventKeyTable = null;
		}
	}

	public override void LoadParticleEventKeyTable(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.AgentActParticleEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			base.ParticleEventKeyTable = value2;
		}
		else
		{
			base.ParticleEventKeyTable = null;
		}
	}

	public override void LoadOnceVoiceEventKeyTable(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.AgentActOnceVoiceEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			base.OnceVoiceEventKeyTable = value2;
		}
		else
		{
			base.OnceVoiceEventKeyTable = null;
		}
	}

	public override void LoadLoopVoiceEventKeyTable(int eventID, int poseID)
	{
		if (Singleton<Manager.Resources>.Instance.Animation.AgentActLoopVoiceEventKeyTable.TryGetValue(eventID, out var value) && value.TryGetValue(poseID, out var value2))
		{
			base.LoopVoiceEventKeyTable = value2;
		}
		else
		{
			base.LoopVoiceEventKeyTable = null;
		}
	}

	protected override void PlayEventOnceVoice(int voiceID)
	{
		AgentActor agentActor = base.Actor as AgentActor;
		if (agentActor == null)
		{
			return;
		}
		int personality = agentActor.ChaControl.fileParam.personality;
		if (!Singleton<Manager.Resources>.Instance.Sound.TryGetMapActionVoiceInfo(personality, voiceID, out var info))
		{
			return;
		}
		Transform transform = base.Actor.Locomotor.transform;
		Voice instance = Singleton<Voice>.Instance;
		int no = personality;
		string assetbundle = info.assetbundle;
		string asset = info.asset;
		Transform voiceTrans = transform;
		Transform transform2 = instance.OnecePlayChara(no, assetbundle, asset, 1f, 0f, 0f, isAsync: true, voiceTrans);
		base.Actor.ChaControl.SetVoiceTransform(transform2);
		base.OnceActionVoice = transform2.GetComponent<AudioSource>();
		if (!(base.OnceActionVoice != null))
		{
			return;
		}
		base.OnceActionVoice.OnDestroyAsObservable().TakeUntilDestroy(base.gameObject).Subscribe(delegate
		{
			if (!PlayEventLoopVoice())
			{
				StopLoopActionVoice();
			}
		});
	}

	protected override bool PlayEventLoopVoice()
	{
		if (!base.PlayEventLoopVoice())
		{
			return false;
		}
		AgentActor agentActor = base.Actor as AgentActor;
		if (agentActor == null)
		{
			return false;
		}
		int num = base.StateLoopVoiceEvents[UnityEngine.Random.Range(0, base.StateLoopVoiceEvents.Count)];
		if (_loopActionVoice.Item1 == num && _loopActionVoice.Item2 != null)
		{
			return false;
		}
		int personality = agentActor.ChaControl.fileParam.personality;
		if (!Singleton<Manager.Resources>.Instance.Sound.TryGetMapActionVoiceInfo(personality, num, out var info))
		{
			return false;
		}
		Transform transform = base.Actor.Locomotor.transform;
		Voice instance = Singleton<Voice>.Instance;
		int no = personality;
		string assetbundle = info.assetbundle;
		string asset = info.asset;
		Transform voiceTrans = transform;
		Transform transform2 = instance.OnecePlayChara(no, assetbundle, asset, 1f, 0f, 0f, isAsync: true, voiceTrans, Voice.Type.PCM, -1, isPlayEndDelete: false);
		base.Actor.ChaControl.SetVoiceTransform(transform2);
		_loopActionVoice.Item1 = num;
		_loopActionVoice.Item2 = transform2.GetComponent<AudioSource>();
		_loopActionVoice.Item3 = transform;
		_loopActionVoice.Item4 = personality;
		return _loopActionVoice.Item2 != null;
	}

	protected override void LoadFootStepEventKeyTable()
	{
		_footStepEventKeyTable = Singleton<Manager.Resources>.Instance.Animation.AgentFootStepEventKeyTable;
	}

	public void UpdateState(ActorLocomotion.AnimationState state)
	{
		if (Mathf.Approximately(Time.deltaTime, 0f) || Animator == null)
		{
			return;
		}
		base.IsLocomotionState = Animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");
		DefinePack definePack = Singleton<Manager.Resources>.Instance.DefinePack;
		string directionParameterName = definePack.AnimatorParameter.DirectionParameterName;
		string forwardMove = definePack.AnimatorParameter.ForwardMove;
		string heightParameterName = definePack.AnimatorParameter.HeightParameterName;
		float num = 0f;
		if (state.setMediumOnWalk)
		{
			if (state.moveDirection.z < state.medVelocity || Mathf.Approximately(state.moveDirection.z, state.medVelocity))
			{
				float b = Mathf.InverseLerp(0f, state.maxVelocity, state.medVelocity);
				float value = Mathf.InverseLerp(0f, state.maxVelocity, state.moveDirection.z);
				float t = Mathf.InverseLerp(0f, b, value);
				num = Mathf.Lerp(0f, 0.5f, t);
			}
			else
			{
				num = Mathf.InverseLerp(0f, state.maxVelocity, state.moveDirection.z);
			}
		}
		else
		{
			num = Mathf.InverseLerp(0f, state.maxVelocity, state.moveDirection.z);
		}
		if (!base.Actor.IsSlave)
		{
			float @float = Animator.GetFloat(forwardMove);
			float value2 = Mathf.Lerp(@float, num, Time.deltaTime * Singleton<Manager.Resources>.Instance.LocomotionProfile.LerpSpeed);
			Animator.SetFloat(forwardMove, value2);
		}
		float shapeBodyValue = base.Actor.ChaControl.GetShapeBodyValue(0);
		Animator.SetFloat(heightParameterName, shapeBodyValue);
	}

	private void AfterFBBIK()
	{
	}

	private void OnLateUpdate()
	{
		Follow();
		if (!(Vector3.Angle(base.transform.up, Vector3.up) <= 0.01f))
		{
		}
	}

	private void RotateEffector(IKEffector effector, Quaternion rotation, float mlp)
	{
		Vector3 vector = effector.bone.position - base.transform.position;
		Vector3 vector2 = rotation * vector;
		Vector3 vector3 = vector2 - vector;
		effector.positionOffset += vector3 * mlp;
	}

	public void ForceChangeAnime()
	{
		if ((bool)Animator && Animator.runtimeAnimatorController != null)
		{
			Animator.SetFloat(Singleton<Manager.Resources>.Instance.DefinePack.AnimatorParameter.ForwardMove, 0f);
		}
	}

	public ActorAnimationAgent CloneComponent(GameObject target)
	{
		ActorAnimationAgent actorAnimationAgent = target.AddComponent<ActorAnimationAgent>();
		actorAnimationAgent._character = _character;
		actorAnimationAgent.Animator = Animator;
		actorAnimationAgent.EnabledPoser = base.EnabledPoser;
		actorAnimationAgent.ArmAnimator = base.ArmAnimator;
		actorAnimationAgent.Poser = base.Poser;
		actorAnimationAgent.IsLocomotionState = base.IsLocomotionState;
		actorAnimationAgent._ik = _ik;
		return actorAnimationAgent;
	}
}
