using System;
using System.Collections;
using Manager;
using ReMotion;
using UniRx;
using UnityEngine;

namespace AIProject.Player;

public abstract class PlayerStateBase : StateBase, IPlayer
{
	protected DateTime _startTime = DateTime.MinValue;

	protected TimeSpan _totalMinute = TimeSpan.MinValue;

	protected float _duration;

	protected float _elapsedTime;

	protected bool _hasAction;

	protected string _loopStateName = string.Empty;

	protected int _randomCount;

	protected float _oldNormalizedTime;

	public Action OnCompleted { get; set; }

	public override void Awake(Actor actor)
	{
		Actor.InputInfo stateInfo = actor.StateInfo;
		stateInfo.move = Vector3.zero;
		actor.StateInfo = stateInfo;
		if (actor is PlayerActor)
		{
			OnAwake(actor as PlayerActor);
		}
	}

	protected abstract void OnAwake(PlayerActor player);

	public override void Release(Actor actor, EventType type)
	{
		PlayerActor playerActor = actor as PlayerActor;
		OnRelease(playerActor);
		if (!(this is Normal) && !(this is Houchi) && !(this is Onbu))
		{
			playerActor.ReleaseCurrentPoint();
			if (playerActor.PlayerController.CommandArea != null)
			{
				playerActor.PlayerController.CommandArea.enabled = true;
			}
			MapUIContainer.SetCommandLabelAcception(CommandLabel.AcceptionState.InvokeAcception);
		}
		actor.ActivateNavMeshAgent();
		actor.IsKinematic = false;
	}

	protected virtual void OnRelease(PlayerActor player)
	{
	}

	public override void AfterUpdate(Actor actor, Actor.InputInfo info)
	{
		if (actor is PlayerActor)
		{
			OnAfterUpdate(actor as PlayerActor, info);
		}
	}

	protected virtual void OnAfterUpdate(PlayerActor player, Actor.InputInfo info)
	{
	}

	public override void Update(Actor actor, ref Actor.InputInfo info)
	{
		if (actor is PlayerActor)
		{
			OnUpdate(actor as PlayerActor, ref info);
		}
	}

	protected abstract void OnUpdate(PlayerActor player, ref Actor.InputInfo info);

	public override void FixedUpdate(Actor actor, Actor.InputInfo info)
	{
		if (actor is PlayerActor)
		{
			OnFixedUpdate(actor as PlayerActor, info);
		}
	}

	protected virtual void OnFixedUpdate(PlayerActor player, Actor.InputInfo info)
	{
	}

	public override void OnAnimatorStateEnter(ActorController control, AnimatorStateInfo stateInfo)
	{
		if (control is PlayerController)
		{
			OnAnimatorStateEnterInternal(control as PlayerController, stateInfo);
		}
	}

	protected virtual void OnAnimatorStateEnterInternal(PlayerController control, AnimatorStateInfo stateInfo)
	{
	}

	public override void OnAnimatorStateExit(ActorController control, AnimatorStateInfo stateInfo)
	{
		if (control is PlayerController)
		{
			OnAnimatorStateExitInternal(control as PlayerController, stateInfo);
		}
	}

	protected virtual void OnAnimatorStateExitInternal(PlayerController control, AnimatorStateInfo stateInfo)
	{
	}

	public override IEnumerator End(Actor actor)
	{
		if (actor is PlayerActor)
		{
			yield return OnEnd(actor as PlayerActor);
		}
	}

	protected virtual IEnumerator OnEnd(PlayerActor player)
	{
		yield break;
	}

	protected IObservable<TimeInterval<float>> FadeOutActionAsObservable(PlayerActor actor, int sex, Transform t, ActionPoint actionPoint)
	{
		if (t != null)
		{
			Vector3 position = actor.Position;
			Quaternion rotation = actor.Rotation;
			actionPoint.TryGetPlayerActionPointInfo(actor.EventKey, out var outInfo);
			if (Singleton<Manager.Resources>.Instance.Animation.PlayerActionAnimTable.TryGetValue(sex, out var value) && value.TryGetValue(outInfo.eventID, out var value2) && value2.TryGetValue(outInfo.poseID, out var value3))
			{
				IConnectableObservable<TimeInterval<float>> connectableObservable = ObservableEasing.Linear(value3.MainStateInfo.OutStateInfo.FadeSecond).FrameTimeInterval().Publish();
				connectableObservable.Connect();
				int directionType = value3.DirectionType;
				switch (directionType)
				{
				case 0:
					if (value3.MainStateInfo.OutStateInfo.EnableFade)
					{
						connectableObservable.Subscribe(delegate(TimeInterval<float> x)
						{
							actor.Position = Vector3.Lerp(position, t.position, x.Value);
							actor.Rotation = Quaternion.Slerp(rotation, t.rotation, x.Value);
						});
					}
					else
					{
						actor.Position = t.position;
						actor.Rotation = t.rotation;
					}
					break;
				case 1:
				{
					Quaternion lookRotation = Quaternion.LookRotation(actionPoint.Position - position);
					if (value3.MainStateInfo.OutStateInfo.EnableFade)
					{
						connectableObservable.Subscribe(delegate(TimeInterval<float> x)
						{
							actor.Rotation = Quaternion.Slerp(rotation, lookRotation, x.Value);
						});
					}
					else
					{
						actor.Rotation = lookRotation;
					}
					break;
				}
				}
				return connectableObservable;
			}
		}
		return Observable.Empty<TimeInterval<float>>();
	}
}
