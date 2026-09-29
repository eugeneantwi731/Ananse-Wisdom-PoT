using UnityEngine;
using UnityEngine.EventSystems;

// Small "press in" feel for the round buttons. Added automatically by IntroSequence.
public class IntroPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float pressedScale = 0.92f;
    float goal = 1f;
    public float Value { get; private set; } = 1f;

    public void OnPointerDown(PointerEventData e) { goal = pressedScale; }
    public void OnPointerUp(PointerEventData e) { goal = 1f; }
    public void OnPointerExit(PointerEventData e) { goal = 1f; }

    void Update() { Value = Mathf.MoveTowards(Value, goal, Time.unscaledDeltaTime * 2.5f); }
}
