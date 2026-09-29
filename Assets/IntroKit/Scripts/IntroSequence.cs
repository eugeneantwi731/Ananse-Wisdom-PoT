using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using M = IntroMath;

// Put this on ONE empty GameObject in your Intro scene. It builds and plays everything:
// pot + orbs (3D), title + spider, buttons, pattern strip. Portrait and landscape.
public class IntroSequence : MonoBehaviour
{
    public enum Orient { Auto, Portrait, Landscape }

    [Header("Setup")]
    [Tooltip("Your pot (the .glb imported with glTFast, or an FBX). Drag it from the Project window.")]
    public GameObject potPrefab;
    [Tooltip("Leave empty to use the Main Camera.")]
    public Camera introCamera;
    [Tooltip("Exact name of your Vuforia AR scene (must be in Build Settings).")]
    public string arSceneName = "";

    [Header("Tuning")]
    [Tooltip("Turn this until the Gye Nyame faces you when the pot stops. You can change it while playing.")]
    public float symbolFacingAngle = 100f;
    public float potScale = 1f;
    [Range(0.5f, 2f)] public float speed = 1f;
    [Tooltip("Force a layout while testing in the Editor.")]
    public Orient previewOrientation = Orient.Auto;

    [Header("Buttons")]
    public UnityEvent onHowItWorks;
    public UnityEvent onSettings;
    [Tooltip("Optional: your own images for the round buttons (circle + label, same layout as the originals). Leave empty to use the built-in ones.")]
    public Sprite howItWorksImage;
    public Sprite settingsImage;

    // Timeline (seconds) — same as the web prototype
    public static readonly string[] OrbColors = { "#F0A05E", "#6FCB8D", "#E3BC4F", "#EE6F8F", "#62AEEA", "#A48CF0" };
    public const float ORB_START = 2.4f, ORB_GAP = 0.45f, ORB_FLIGHT = 1.6f;
    public const float LAST_IN = ORB_START + 5 * ORB_GAP + ORB_FLIGHT;
    public const float CAM_MOVE = LAST_IN - 0.15f, TITLE_START = LAST_IN + 0.45f, BUTTON_IN = TITLE_START + 5.8f;

    IntroPotRig rig;
    IntroTitle title;
    CanvasScaler scaler;
    RectTransform canvasRT, stage, beginRT, ringRT, stripRT, help, settings, sparkRoot;
    CanvasGroup beginCG, helpCG, settingsCG;
    Image beginFace, beginMask, glow, shimmer, flash, ring, fadeIn, fadeOut;
    RawImage strip;
    IntroPressScale helpPress, settingsPress;
    Image[] btnSparks = new Image[12];
    Sprite beginP, beginL, maskP, maskL;
    // Played before: "Continue Your Journey" + a "New Journey" button under it (asks first, then clears the save).
    float stageH = 1920f;
    bool hasSave; RectTransform newRT; CanvasGroup newCG; IntroPressScale newPress; bool asking;
    AsyncOperation preload;
    float t, tapTime = -1f;
    int lastLand = -1;
    bool leaving, ready;

    void Awake() { Application.targetFrameRate = 60; }

    void Start()
    {
        var cam = introCamera != null ? introCamera : Camera.main;
        if (cam == null) { Debug.LogError("[Intro] No camera found. Add a Camera tagged MainCamera."); enabled = false; return; }
        rig = new IntroPotRig(cam, potPrefab, potScale);
        BuildUI();
        if (!string.IsNullOrEmpty(arSceneName))
        {
            preload = SceneManager.LoadSceneAsync(arSceneName);
            if (preload != null) preload.allowSceneActivation = false;
        }
        StartCoroutine(BeginAfterWarmup());
    }

    // Wait two frames so the first frame hitch never eats the start of the animation.
    // Coming back from the village (Options > Return to Home): skip straight to the menu, no replay of the intro.
    public static bool SkipToMenu;
    IEnumerator BeginAfterWarmup() { yield return null; yield return null; if (SkipToMenu) { SkipToMenu = false; t = BUTTON_IN + 1.2f; } ready = true; }

    void Update()
    {
        bool land = previewOrientation == Orient.Landscape || (previewOrientation == Orient.Auto && Screen.width > Screen.height);
        float W = land ? 1920 : 1080, H = land ? 1080 : 1920;
        if ((land ? 1 : 0) != lastLand) { lastLand = land ? 1 : 0; Relayout(land, W, H); }
        float uiScale = Mathf.Min(Screen.width / W, Screen.height / H);

        if (ready) t += Mathf.Min(Time.deltaTime, 0.1f) * speed;

        // Light shafts fade out before they reach the title (portrait only)
        float stageTop = (Screen.height - H * uiScale) * 0.5f;
        Shader.SetGlobalFloat("_IntroFadeTop", stageTop + (land ? 120 : 665) * uiScale);
        Shader.SetGlobalFloat("_IntroFadeLen", (land ? 120 : 110) * uiScale);

        title.SetScreenScale(uiScale, land);
        rig.Render(t, land, H, uiScale, symbolFacingAngle);
        title.Update(t - TITLE_START);
        AnimateUI(land);
    }

    // ---------- UI ----------

    void BuildUI()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }
        var cg = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = cg.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        scaler = cg.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasRT = cg.GetComponent<RectTransform>();

        stage = IntroTitle.NewRect("Stage", canvasRT);

        Sprite capsule = IntroTex.ToSprite(IntroTex.Capsule(), new Vector4(16, 0, 16, 0));
        Sprite circle = IntroTex.ToSprite(IntroTex.Circle());
        title = new IntroTitle(stage, capsule, circle, IntroTex.ToSprite(IntroTex.Diamond()));

        beginP = IntroTex.Load("Intro/UI/begin_portrait"); beginL = IntroTex.Load("Intro/UI/begin_landscape");
        maskP = IntroTex.Load("Intro/UI/begin_mask_portrait"); maskL = IntroTex.Load("Intro/UI/begin_mask_landscape");
        hasSave = JourneyState.HasSave;
        if (hasSave)
        {
            var cp = IntroTex.Load("Intro/UI/continue_portrait"); var cl = IntroTex.Load("Intro/UI/continue_landscape");
            if (cp != null) beginP = cp; if (cl != null) beginL = cl;
        }

        // Round buttons
        help = RoundButton("HowItWorks", howItWorksImage != null ? howItWorksImage : IntroTex.Load("Intro/UI/round_help"), onHowItWorks, out helpCG, out helpPress);
        settings = RoundButton("Settings", settingsImage != null ? settingsImage : IntroTex.Load("Intro/UI/round_settings"), onSettings, out settingsCG, out settingsPress);

        if (hasSave)
        {
            var nj = IntroTitle.NewImage("NewJourney", stage, IntroTex.Load("Intro/UI/new_journey"), Color.white);
            nj.preserveAspect = true; nj.raycastTarget = true; newRT = nj.rectTransform;
            newCG = nj.gameObject.AddComponent<CanvasGroup>(); newPress = nj.gameObject.AddComponent<IntroPressScale>();
            var nb = nj.gameObject.AddComponent<Button>(); nb.transition = Selectable.Transition.None; nb.onClick.AddListener(OnNewJourney);
        }

        // Main button
        beginRT = IntroTitle.NewRect("BeginButton", stage);
        beginCG = beginRT.gameObject.AddComponent<CanvasGroup>();
        glow = IntroTitle.NewImage("Glow", beginRT, IntroTex.ToSprite(IntroTex.PillGlow(), new Vector4(64, 64, 64, 64)), M.A(M.Hex("#F2C14E"), 0));
        glow.type = Image.Type.Sliced; glow.pixelsPerUnitMultiplier = 64f / 100f;
        beginFace = IntroTitle.NewImage("Face", beginRT, beginP, Color.white); beginFace.raycastTarget = true; Stretch(beginFace.rectTransform);
        beginMask = IntroTitle.NewImage("Mask", beginRT, maskP, Color.white); Stretch(beginMask.rectTransform);
        beginMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        shimmer = IntroTitle.NewImage("Shimmer", beginMask.rectTransform, IntroTex.ToSprite(IntroTex.Shimmer()), new Color(1f, 0.9f, 0.67f, 0.155f));
        flash = IntroTitle.NewImage("Flash", beginMask.rectTransform, null, M.A(M.Hex("#FFE7B0"), 0)); Stretch(flash.rectTransform);
        var btn = beginRT.gameObject.AddComponent<Button>(); btn.targetGraphic = beginFace; btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnBegin);

        // Tap ring + sparks
        ringRT = IntroTitle.NewRect("TapRing", stage);
        ring = IntroTitle.NewImage("Ring", ringRT, IntroTex.ToSprite(IntroTex.PillOutline(), new Vector4(68, 68, 68, 68)), M.A(M.Hex("#F7D08C"), 0));
        ring.type = Image.Type.Sliced;
        sparkRoot = IntroTitle.NewRect("TapSparks", stage);
        for (int i = 0; i < 12; i++) { btnSparks[i] = IntroTitle.NewImage("Spark", sparkRoot, circle, M.A(M.Hex(OrbColors[i % 6]), 0)); }

        // Pattern strip along the bottom of the screen
        var sh = IntroTitle.NewImage("StripShadow", canvasRT, IntroTex.ToSprite(IntroTex.VerticalFade()), Color.white).rectTransform;
        stripRT = new GameObject("PatternStrip", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
        stripRT.SetParent(canvasRT, false);
        strip = stripRT.GetComponent<RawImage>(); strip.raycastTarget = false;
        var stripTex = Resources.Load<Texture2D>("Intro/UI/pattern_strip");
        if (stripTex != null) { stripTex.wrapMode = TextureWrapMode.Repeat; strip.texture = stripTex; }
        foreach (var r in new[] { stripRT, sh }) { r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(0.5f, 0); }
        sh.name = "StripShadow"; stripShadow = sh;

        fadeIn = IntroTitle.NewImage("FadeIn", canvasRT, null, M.Hex("#0C0604")); Stretch(fadeIn.rectTransform);
        fadeOut = IntroTitle.NewImage("FadeOut", canvasRT, null, M.A(M.Hex("#0C0604"), 0)); Stretch(fadeOut.rectTransform);
    }
    RectTransform stripShadow;

    RectTransform RoundButton(string name, Sprite sprite, UnityEvent evt, out CanvasGroup cg, out IntroPressScale press)
    {
        var im = IntroTitle.NewImage(name, stage, sprite, Color.white);
        im.preserveAspect = true;
        im.raycastTarget = true;
        cg = im.gameObject.AddComponent<CanvasGroup>();
        press = im.gameObject.AddComponent<IntroPressScale>();
        var b = im.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { if (evt != null) evt.Invoke(); });
        return im.rectTransform;
    }

    void Relayout(bool land, float W, float H)
    {
        scaler.referenceResolution = new Vector2(W, H);
        stage.sizeDelta = new Vector2(W, H); stageH = H;
        title.Layout(land);
        beginFace.sprite = land ? beginL : beginP;
        beginMask.sprite = land ? maskL : maskP;
        float stripH = land ? 64 : 70;
        stripRT.sizeDelta = new Vector2(0, stripH);
        stripShadow.sizeDelta = new Vector2(0, 24); stripShadow.anchoredPosition = new Vector2(0, stripH);
    }

    void AnimateUI(bool land)
    {
        float W = land ? 1920 : 1080;
        float b = t - BUTTON_IN, tp = tapTime >= 0 ? t - tapTime : -1;
        float btnW = land ? 820 : 880, btnLeft = land ? 1360 - btnW / 2 : (W - btnW) / 2, btnTop = hasSave ? (land ? 580 : 1360) : (land ? 610 : 1390);
        float drop = land ? 0f : Mathf.Max(0f, stageH - 1920f) * .6f; // tall phones: move the buttons down into the empty space
        btnTop += drop;
        float cx = btnLeft + btnW / 2, cy = btnTop + 68;

        float press = tp >= 0 ? (tp < 0.1f ? tp / 0.1f : Mathf.Max(0, 1 - (tp - 0.1f) / 0.25f)) : 0;
        float pop = tp >= 0.1f ? M.Bump(tp, 0.1f, 0.5f) : 0;

        // Main button
        beginRT.sizeDelta = new Vector2(btnW + 120, 256);
        StagePos(beginRT, cx, cy + (1 - M.OutBack(b / 0.75f)) * 90);
        beginRT.localScale = Vector3.one * (0.9f + 0.1f * M.OutBack(b / 0.75f)) * (1 - press * 0.07f + pop * 0.05f);
        beginCG.alpha = M.Cl(b / 0.35f);
        bool live = b > 0.3f && tp < 0 && !leaving && !asking;
        beginCG.interactable = beginCG.blocksRaycasts = live;
        float g = b > 0 ? 30 + Mathf.Sin(t * 2.4f) * 14 + pop * 60 : 0;
        glow.rectTransform.sizeDelta = new Vector2(btnW + 64, 136 + 64);
        glow.color = M.A(glow.color, M.Cl(g / 44f) * 0.22f);
        float shimCycle = b > 1 && tp < 0 ? ((b - 1) % 3.4f) / 1.1f : -1;
        float shimX = shimCycle >= 0 && shimCycle <= 1 ? -30 + shimCycle * 150 : -60;
        shimmer.rectTransform.sizeDelta = new Vector2(180, 122);
        shimmer.rectTransform.anchoredPosition = new Vector2(-btnW / 2 + 7 + shimX / 100f * (btnW - 14) + 90, 0);
        flash.color = M.A(flash.color, press * 0.25f + pop * 0.15f);

        // Tap ring + sparks
        float ringP = tp >= 0.08f ? M.Cl((tp - 0.08f) / 0.7f) : 0;
        StagePos(ringRT, cx, cy); ring.rectTransform.sizeDelta = new Vector2(btnW, 136);
        ringRT.localScale = Vector3.one * (1 + M.OutCubic(ringP) * 0.35f);
        ring.color = M.A(ring.color, tp >= 0.08f ? 1 - ringP : 0);
        StagePos(sparkRoot, cx, cy);
        float sp = tp >= 0.08f ? M.OutCubic((tp - 0.08f) / 0.9f) : 0;
        for (int i = 0; i < 12; i++)
        {
            float a = i / 12f * Mathf.PI * 2 + 0.2f, d = sp * (260 + (i % 3) * 70);
            var r = btnSparks[i].rectTransform;
            r.anchoredPosition = new Vector2(Mathf.Cos(a) * d * 1.6f, -Mathf.Sin(a) * d * 0.8f);
            r.sizeDelta = Vector2.one * 18 * (1 - sp * 0.5f);
            btnSparks[i].color = M.A(btnSparks[i].color, tp >= 0.08f && tp < 1.1f ? 1 - sp : 0);
        }

        // New Journey (only after playing before), under the main button
        if (newRT != null)
        {
            float nw = land ? 640 : 700, ny = cy + (land ? 140 : 150);
            newRT.sizeDelta = new Vector2(nw, nw * 280f / 1240f);
            StagePos(newRT, cx, ny + (1 - M.OutBack((b - 0.2f) / 0.6f)) * 50);
            newRT.localScale = Vector3.one * newPress.Value;
            newCG.alpha = M.Cl((b - 0.2f) / 0.4f) * (tp >= 0 ? 1 - M.Cl(tp / 0.3f) : 1);
            newCG.interactable = newCG.blocksRaycasts = live;
        }

        // Round buttons (row centred under the main button)
        float s = (hasSave ? (land ? 98f : 136f) : (land ? 112f : 150f)) / 150f, gap = land ? 150 : 170, rowTop = (hasSave ? (land ? 858 : 1664) : (land ? 772 : 1580)) + drop;
        float over = rowTop + 197f * s - (stageH - (land ? 64f : 70f) - 30f); // labels must stay above the pattern strip
        if (over > 0) rowTop -= over;
        float colA = 196 * s, colB = 150 * s, left = cx - (colA + gap + colB) / 2;
        help.sizeDelta = new Vector2(276, 290); settings.sizeDelta = new Vector2(230, 290);
        StagePos(help, left + colA / 2, rowTop + 105 * s + (1 - M.OutBack((b - 0.35f) / 0.6f)) * 50);
        StagePos(settings, left + colA + gap + colB / 2, rowTop + 105 * s + (1 - M.OutBack((b - 0.5f) / 0.6f)) * 50);
        help.localScale = Vector3.one * s * helpPress.Value;
        settings.localScale = Vector3.one * s * settingsPress.Value;
        helpCG.alpha = M.Cl((b - 0.35f) / 0.4f); settingsCG.alpha = M.Cl((b - 0.5f) / 0.4f);
        helpCG.interactable = helpCG.blocksRaycasts = settingsCG.interactable = settingsCG.blocksRaycasts = live;

        // Strip, fades
        float stripH = land ? 64 : 70, tileW = 1046f * stripH / 70f;
        strip.uvRect = new Rect(0, 0, canvasRT.rect.width / tileW, 1);
        strip.color = M.A(Color.white, M.Cl(t / 1.2f));
        fadeIn.color = M.A(fadeIn.color, 1 - M.Cl(t / 0.5f));
        fadeOut.color = M.A(fadeOut.color, tp >= 0 ? M.InOutCubic((tp - 0.6f) / 0.9f) : 0);

        if (tp > 1.55f && !leaving) StartCoroutine(Leave());
    }

    void OnBegin() { if (tapTime < 0 && !asking) tapTime = t; }

    void OnNewJourney()
    {
        if (tapTime >= 0 || leaving || asking) return;
        asking = true;
        CameraGate.Confirm("Start a new journey?",
            "Your pot, the knowledge you gathered and your gifts will be lost. This can't be undone.",
            "Start over", "Cancel",
            yes =>
            {
                asking = false;
                if (!yes) return;
                JourneyState.NewGame();
                hasSave = false;
                tapTime = t; // same tap + fade as Begin, then camera and the village from the very start
            });
    }

    IEnumerator Leave()
    {
        leaving = true;
        // Camera first (friendly card -> phone prompt -> Settings card if it was refused). Anansi's village needs it.
        bool camOk = false;
        yield return CameraGate.Ensure(ok => camOk = ok);
        if (!camOk) { tapTime = -1; leaving = false; yield break; } // back to the menu, button live again
        if (preload != null) { preload.allowSceneActivation = true; yield break; }
        if (!string.IsNullOrEmpty(arSceneName)) { SceneManager.LoadScene(arSceneName); yield break; }
        Debug.Log("[Intro] No AR scene name set — replaying the intro.");
        yield return null;
        t = 0; tapTime = -1; leaving = false;
    }

    // ---------- helpers ----------
    void StagePos(RectTransform r, float x, float yFromTop)
    {
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -yFromTop);
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero; }
}
