using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FreeBala
{
public class FBCustomizeStageInput : MonoBehaviour, IPointerDownHandler, IEventSystemHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerClickHandler
{
	public FBCustomizeStage stage;

	private readonly Dictionary<int, Vector2> down = new Dictionary<int, Vector2>();

	public void OnPointerDown(PointerEventData e)
	{
		down[e.pointerId] = e.position;
	}

	public void OnPointerUp(PointerEventData e)
	{
		down.Remove(e.pointerId);
		if (down.Count == 0 && (bool)stage)
		{
			stage.EndDrag();
		}
	}

	public void OnBeginDrag(PointerEventData e)
	{
	}

	public void OnDrag(PointerEventData e)
	{
		if (!stage)
		{
			return;
		}
		if (down.Count >= 2 && down.ContainsKey(e.pointerId))
		{
			int key = -999;
			foreach (int key2 in down.Keys)
			{
				if (key2 != e.pointerId)
				{
					key = key2;
					break;
				}
			}
			Vector2 vector = down[key];
			float magnitude = (down[e.pointerId] - vector).magnitude;
			float magnitude2 = (e.position - vector).magnitude;
			if (magnitude > 8f)
			{
				stage.Pinch(magnitude2 / magnitude);
			}
			stage.Pan(e.delta.y * 0.5f);
			down[e.pointerId] = e.position;
		}
		else
		{
			if (down.ContainsKey(e.pointerId))
			{
				down[e.pointerId] = e.position;
			}
			if (e.button == PointerEventData.InputButton.Left)
			{
				stage.Drag(e.delta.x);
			}
			else
			{
				stage.Pan(e.delta.y);
			}
		}
	}

	public void OnEndDrag(PointerEventData e)
	{
		if ((bool)stage && down.Count < 2)
		{
			stage.EndDrag();
		}
	}

	public void OnScroll(PointerEventData e)
	{
		if ((bool)stage)
		{
			float num = e.scrollDelta.y;
			if (Mathf.Abs(num) > 10f)
			{
				num /= 120f;
			}
			stage.Zoom(Mathf.Clamp(num, -2f, 2f));
		}
	}

	public void OnPointerClick(PointerEventData e)
	{
		if ((bool)stage && e.clickCount == 2 && e.button == PointerEventData.InputButton.Left)
		{
			stage.PlayDance();
		}
	}

	private void OnDisable()
	{
		down.Clear();
	}
}
}
