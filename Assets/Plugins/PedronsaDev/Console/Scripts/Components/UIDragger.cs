using UnityEngine;
using UnityEngine.EventSystems;

namespace PedronsaDev.Console
{
	public class UIDragger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IDragHandler
	{
		[SerializeField] private Canvas m_Canvas;
		[SerializeField] private RectTransform m_DragRectTransform;
		[SerializeField] private bool m_SetCustomCursor;
		[SerializeField] private Texture2D m_HoverCursor;

		private Vector2 m_HoverCursorHotspot;

		private void OnDisable()
		{
			if (m_SetCustomCursor)
			{
				Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
			}
		}

		private void Start()
		{
			m_HoverCursorHotspot = new Vector2(m_HoverCursor.width / 2, m_HoverCursor.height / 2);
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (m_SetCustomCursor)
			{
				Cursor.SetCursor(m_HoverCursor, m_HoverCursorHotspot, CursorMode.Auto);
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (m_SetCustomCursor)
			{
				Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
			}
		}

		public void OnDrag(PointerEventData eventData)
		{
			m_DragRectTransform.anchoredPosition += eventData.delta / m_Canvas.scaleFactor;
		}
	}
}