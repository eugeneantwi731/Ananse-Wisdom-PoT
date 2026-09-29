using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;
using TMPro;

// Pin root: needs a Collider. Children: Icon (SpriteRenderer), Label (SpriteRenderer, Sliced) > Text (TextMeshPro).
[RequireComponent(typeof(BoxCollider))]
public class KnowledgePin : MonoBehaviour
{
    public string pinId = "weaver";
    public string displayName = "Weaver";
    public TMP_Text label;                // drag the Text child here
    public SpriteRenderer labelBackground; // drag the Label child here
    public Transform icon;                 // drag the Icon child here (only this floats)
    Vector3 iconStart;
    public UnityEvent<string> onTapped;
    [Header("Tap")]
    [Tooltip("Leave empty if this pin has its own Button component. Otherwise drag a button here to click it after the animation.")]
    public Button openButton;
    [Tooltip("Length of the tap animation before the game opens")]
    public float tapDuration = 0.5f;
    [Tooltip("How high the icon jumps on tap")]
    public float tapJump = 0.25f;
    bool animating;
    Vector3 iconBaseScale = Vector3.one, labelBaseScale = Vector3.one;
    Vector3 tapOffset;
    SpriteRenderer iconSR;
    public float labelPadding = 0.9f;     // side padding, as a fraction of pill height
    [Tooltip("Height of the name pill")]
    public float labelHeight = 0.3f;
    [Tooltip("Where the pill sits, measured from the pin's bottom point (negative = below)")]
    public float labelY = -0.15f;
    [Tooltip("Text size relative to the pill height")]
    public float textScale = 0.55f;
    [Tooltip("How far the pin floats up/down (in pin sizes)")]
    public float bobHeight = 0.08f;
    [Tooltip("Speed of the float")]
    public float bobSpeed = 2f;
    [Tooltip("Keep the same size on screen at any distance, like a HUD marker. 0 = off")]
    public float screenSize = 0.08f;
    Vector3 baseScale;
    [Header("Home / Weaver swap")]
    [Tooltip("Shows the pin's own Weaver picture until the tutorial kente is woven, then pops into the Home picture. Leave Home Icon empty to use AnansiGame/Resources/Pins/pin_home.")]
    public bool swapAfterTutorial = true;
    public Sprite weaverIcon, homeIcon;
    public string weaverName = "Weaver", homeName = "Home";
    int shownStage = -1;
    [HideInInspector] public float appear = 1f; // 0..1, set by ScanFlow while the village grows onto the paper
    [HideInInspector] public bool freezeBob; // locked pins stand still (set by JourneyPin)
    float bobAmt = 1f;
    bool IsHomePin => pinId == JourneyState.Home || pinId == "weaver";

    Vector3 startPos;
    Camera cam;

    void Start()
    {
        startPos = transform.localPosition;
        cam = Camera.main;
        baseScale = transform.localScale;
        if (icon) { iconStart = icon.localPosition; iconBaseScale = icon.localScale; iconSR = icon.GetComponent<SpriteRenderer>(); }
        FitLabel();
        if (labelBackground) labelBaseScale = labelBackground.transform.localScale;
        if (!openButton) openButton = GetComponent<Button>();
        if (IsHomePin && swapAfterTutorial)
        {
            if (!weaverIcon && iconSR) weaverIcon = iconSR.sprite; // the Weaver picture already on the pin (Assets/Icons/pin_weaver)
            if (!homeIcon) homeIcon = Resources.Load<Sprite>("Pins/pin_home");
            homeIcon = MatchSize(homeIcon, weaverIcon);
            ApplyHomeStage(false);
        }
    }

    // Rebuild 'pic' with the same pixels-per-unit and pivot as 'like', so a swapped picture keeps the exact size and position.
    public static Sprite MatchSize(Sprite pic, Sprite like)
    {
        if (!pic || !like) return pic;
        var lp = new Vector2(like.pivot.x / like.rect.width, like.pivot.y / like.rect.height);
        float ppu = like.pixelsPerUnit * pic.rect.height / like.rect.height; // same height on screen even if the images differ in size
        var s = Sprite.Create(pic.texture, pic.rect, lp, ppu, 0, SpriteMeshType.FullRect);
        s.name = pic.name; return s;
    }

    // 0 = Weaver (before the tutorial kente), 1 = Home (after)
    void ApplyHomeStage(bool animate)
    {
        int stage = JourneyState.TutorialWoven ? 1 : 0;
        if (stage == shownStage) return;
        bool first = shownStage < 0; shownStage = stage;
        if (animate && !first && !animating) StartCoroutine(SwapPop(stage));
        else SetHomeLook(stage);
    }
    void SetHomeLook(int stage)
    {
        var sp = stage == 1 ? homeIcon : weaverIcon;
        if (iconSR && sp) iconSR.sprite = sp;
        displayName = stage == 1 ? homeName : weaverName;
        FitLabel();
    }
    // Shrink away, swap the picture, spring back bigger than before, settle.
    IEnumerator SwapPop(int stage)
    {
        animating = true;
        for (float t = 0; t < 0.18f; t += Time.deltaTime) { if (icon) icon.localScale = iconBaseScale * Mathf.Lerp(1f, 0f, t / 0.18f); yield return null; }
        SetHomeLook(stage);
        if (labelBackground) labelBaseScale = labelBackground.transform.localScale;
        for (float t = 0; t < 0.5f; t += Time.deltaTime)
        {
            float k = t / 0.5f, s = 1f + Mathf.Sin(k * Mathf.PI * 2.2f) * (1f - k) * 0.35f;
            if (icon) icon.localScale = iconBaseScale * Mathf.Min(s, 1.35f) * Mathf.Clamp01(k * 4f);
            yield return null;
        }
        if (icon) icon.localScale = iconBaseScale;
        animating = false;
    }

    // Also runs in the Editor whenever you change Display Name, so you see the fit immediately
    // Resize after the Inspector finishes (resizing inside OnValidate causes a harmless Unity warning)
    void OnValidate()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) FitLabel(); };
#endif
    }

    [ContextMenu("Fit Label")]
    public void FitLabel()
    {
        if (!label || !labelBackground || !labelBackground.sprite) return;

        // Text: one line, centered, sized from the pill height
        label.text = displayName;
        label.enableAutoSizing = false;
        label.fontSize = labelHeight * textScale * 10f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        Vector2 textSize = label.GetPreferredValues(displayName, 100f, 100f);

        // Pill: width follows the name length
        labelBackground.drawMode = SpriteDrawMode.Sliced;
        float w = Mathf.Max(textSize.x + labelHeight * labelPadding, labelHeight * 1.6f);
        labelBackground.size = new Vector2(w, labelHeight);

        // Place pill under the pin, correcting for whatever pivot the pill image has
        var sp = labelBackground.sprite;
        Vector2 pivot01 = new Vector2(sp.pivot.x / sp.rect.width, sp.pivot.y / sp.rect.height);
        Vector2 centerOffset = new Vector2((0.5f - pivot01.x) * w, (0.5f - pivot01.y) * labelHeight);
        labelBackground.transform.localScale = Vector3.one;
        labelBackground.transform.localRotation = Quaternion.identity;
        // Find the true bottom of the pin image, whatever its pivot is
        float iconBottom = 0f;
        if (icon)
        {
            var isr = icon.GetComponent<SpriteRenderer>();
            float iy = Application.isPlaying ? iconStart.y : icon.localPosition.y;
            if (isr && isr.sprite) iconBottom = iy + isr.sprite.bounds.min.y * icon.localScale.y;
        }
        labelBackground.transform.localPosition = new Vector3(-centerOffset.x, iconBottom + labelY - labelHeight * 0.5f - centerOffset.y, 0f);

        // Text sits exactly in the middle of the pill, slightly in front
        label.transform.localScale = Vector3.one;
        label.transform.localRotation = Quaternion.identity;
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(textSize.x + 0.05f, labelHeight);
        label.transform.localPosition = new Vector3(centerOffset.x, centerOffset.y, -0.001f);

        label.GetComponent<Renderer>().sortingOrder = labelBackground.sortingOrder + 1;

        // Collider covers pin + label
        var col = GetComponent<BoxCollider>();
        if (col) { col.center = new Vector3(0, 0.55f + labelY * 0.5f, 0); col.size = new Vector3(Mathf.Max(1f, w), 1.35f - labelY + labelHeight, 0.1f); }
    }

    void Update()
    {
        if (IsHomePin && swapAfterTutorial && Application.isPlaying) ApplyHomeStage(true);
        // Only the Icon floats up and down; the label stays still.
        if (icon)
        {
            float phase = (startPos.x + startPos.z) * 3f; // offsets each pin so they don't move in sync
            bobAmt = Mathf.MoveTowards(bobAmt, freezeBob ? 0f : 1f, Time.deltaTime * 2f);
            icon.localPosition = iconStart + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight * bobAmt + tapOffset;
        }

        // Face camera on the Y axis only (turns left/right, stays upright)
        if (cam)
        {
            Vector3 dir = transform.position - cam.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir);
            if (screenSize > 0f)
                transform.localScale = baseScale * Vector3.Distance(cam.transform.position, transform.position) * screenSize * appear;
        }
        if ((!cam || screenSize <= 0f) && Application.isPlaying && baseScale != Vector3.zero) transform.localScale = baseScale * appear;

        if (AnansiRuntime.TryGetTap(out var tapPos)) TryTap(tapPos); // works with the old and the new Input System
    }

    void TryTap(Vector2 screenPos)
    {
        if (!cam) cam = Camera.main; // the AR camera can appear after Start
        if (animating || !cam) return;
        if (AnansiRuntime.AnyGameOpen || JourneyDialogue.IsOpen) return; // taps inside a mini-game never reach the village pins
        if (Physics.Raycast(cam.ScreenPointToRay(screenPos), out var hit) && hit.transform == transform)
            StartCoroutine(TapAnimation());
    }

    // Squash -> pop up with a jump and flash -> land -> then open
    IEnumerator TapAnimation()
    {
        animating = true;
        Color baseColor = iconSR ? iconSR.color : Color.white;
        float t = 0f;
        while (t < tapDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / tapDuration);

            // Scale: quick squash (0-15%), springy overshoot, settle
            float sx, sy;
            if (p < 0.15f) { float k = p / 0.15f; sx = Mathf.Lerp(1f, 1.2f, k); sy = Mathf.Lerp(1f, 0.75f, k); }
            else
            {
                float k = (p - 0.15f) / 0.85f;
                float spring = Mathf.Sin(k * Mathf.PI * 2.5f) * (1f - k); // wobble that fades out
                sx = 1f - spring * 0.2f;
                sy = 1f + spring * 0.3f;
            }
            if (icon) icon.localScale = new Vector3(iconBaseScale.x * sx, iconBaseScale.y * sy, iconBaseScale.z);

            // Jump: up and back down after the squash
            float j = p < 0.15f ? 0f : Mathf.Sin((p - 0.15f) / 0.85f * Mathf.PI);
            tapOffset = Vector3.up * j * tapJump;

            // Flash brighter at the top of the jump
            if (iconSR) iconSR.color = Color.Lerp(baseColor, Color.white * 1.6f, j * 0.6f);

            // Label gives a small pulse
            if (labelBackground) labelBackground.transform.localScale = labelBaseScale * (1f + Mathf.Sin(p * Mathf.PI) * 0.1f);

            yield return null;
        }
        if (icon) icon.localScale = iconBaseScale;
        if (iconSR) iconSR.color = baseColor;
        if (labelBackground) labelBackground.transform.localScale = labelBaseScale;
        tapOffset = Vector3.zero;

        onTapped?.Invoke(pinId);
        if (!GetComponent<JourneyPin>()) { if (openButton) openButton.onClick.Invoke(); else AnansiRuntime.OpenGameById(pinId); } // JourneyPin decides if the game may open
        animating = false;
    }
}
