using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Added automatically to each crop card. You don't need to touch this.
public class CropDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public FarmerGameController game;
    public string cropId;
    public CanvasGroup group;
    public Image pic;

    public void OnBeginDrag(PointerEventData e) { if (game) game.BeginDrag(this, e); }
    public void OnDrag(PointerEventData e) { if (game) game.Drag(this, e); }
    public void OnEndDrag(PointerEventData e) { if (game) game.EndDrag(this, e); }
}
