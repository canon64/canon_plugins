using DG.Tweening;
using UnityEngine;

namespace ActionGame.Information;

public static class Mover
{
	public enum Type
	{
		LeftToRight,
		RightToLeft,
		TopToBottom,
		BottomToTop
	}

	public static Tweener SetPivotMove(this GameObject gameObject, Type type, bool isStart, RectTransform mover, float duration)
	{
		if (!Mathf.Approximately(duration, 0f))
		{
			return gameObject.SetMovePivotRelative(type, isStart, mover, duration);
		}
		return gameObject.SetMovePivotAbsolute(type, isStart, mover, duration);
	}

	public static Tweener SetMovePivotRelative(this GameObject gameObject, Type type, bool isStart, RectTransform mover, float duration)
	{
		Tweener t = null;
		switch (type)
		{
		case Type.LeftToRight:
			t = TypeOfMovePivotRelative((!isStart) ? Type.RightToLeft : Type.LeftToRight, mover, duration);
			break;
		case Type.RightToLeft:
			t = TypeOfMovePivotRelative(isStart ? Type.RightToLeft : Type.LeftToRight, mover, duration);
			break;
		case Type.TopToBottom:
			t = TypeOfMovePivotRelative(isStart ? Type.TopToBottom : Type.BottomToTop, mover, duration);
			break;
		case Type.BottomToTop:
			t = TypeOfMovePivotRelative(isStart ? Type.BottomToTop : Type.TopToBottom, mover, duration);
			break;
		}
		return t.SetLink(gameObject);
	}

	public static Tweener SetMovePivotAbsolute(this GameObject gameObject, Type type, bool isStart, RectTransform mover, float duration)
	{
		Tweener t = null;
		switch (type)
		{
		case Type.LeftToRight:
			t = TypeOfMovePivotAbsolute((!isStart) ? Type.RightToLeft : Type.LeftToRight, mover, duration);
			break;
		case Type.RightToLeft:
			t = TypeOfMovePivotAbsolute(isStart ? Type.RightToLeft : Type.LeftToRight, mover, duration);
			break;
		case Type.TopToBottom:
			t = TypeOfMovePivotAbsolute(isStart ? Type.TopToBottom : Type.BottomToTop, mover, duration);
			break;
		case Type.BottomToTop:
			t = TypeOfMovePivotAbsolute(isStart ? Type.BottomToTop : Type.TopToBottom, mover, duration);
			break;
		}
		return t.SetLink(gameObject);
	}

	public static Tweener TypeOfMovePivotRelative(Type type, RectTransform mover, float duration)
	{
		switch (type)
		{
		case Type.LeftToRight:
			duration = Mathf.Lerp(0f, mover.pivot.x, duration);
			break;
		case Type.RightToLeft:
			duration = Mathf.Lerp(0f, 1f - mover.pivot.x, duration);
			break;
		case Type.TopToBottom:
			duration = Mathf.Lerp(0f, mover.pivot.y, duration);
			break;
		case Type.BottomToTop:
			duration = Mathf.Lerp(0f, 1f - mover.pivot.y, duration);
			break;
		}
		return TypeOfMovePivotAbsolute(type, mover, duration);
	}

	public static Tweener TypeOfMovePivotAbsolute(Type type, RectTransform mover, float duration)
	{
		Tweener result = null;
		switch (type)
		{
		case Type.LeftToRight:
			result = mover.DOPivotX(0f, duration);
			break;
		case Type.RightToLeft:
			result = mover.DOPivotX(1f, duration);
			break;
		case Type.TopToBottom:
			result = mover.DOPivotY(0f, duration);
			break;
		case Type.BottomToTop:
			result = mover.DOPivotY(1f, duration);
			break;
		}
		return result;
	}
}
