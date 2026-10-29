using UnityEngine;
using UnityEngine.EventSystems;

namespace BestDeal;

/// <summary>标题栏拖动适配；坐标转换遵循原生画布缩放。</summary>
public class HudDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    internal Hud Owner;
    Vector2 _previous;
    RectTransform _parent;
    bool _dragging;

    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button != PointerEventData.InputButton.Left || ScratchInput.isScratching
            || UIDragHandler.isDraggingAnyUI) return;
        _parent = transform.parent.parent as RectTransform;
        _dragging = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent, data.position, data.pressEventCamera, out _previous);
        if (_dragging) UIDragHandler.isDraggingAnyUI = true;
    }

    public void OnDrag(PointerEventData data)
    {
        if (!_dragging || Owner == null) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _parent, data.position, data.pressEventCamera, out var current)) return;
        Owner.Drag(current - _previous);
        _previous = current;
    }

    public void OnEndDrag(PointerEventData data)
    {
        if (_dragging && Owner != null) Owner.RememberPosition();
        ReleaseDrag();
    }

    void OnDisable() => ReleaseDrag();

    void OnApplicationFocus(bool focused)
    {
        if (!focused) ReleaseDrag();
    }

    void ReleaseDrag()
    {
        if (_dragging) UIDragHandler.isDraggingAnyUI = false;
        _dragging = false;
    }
}
