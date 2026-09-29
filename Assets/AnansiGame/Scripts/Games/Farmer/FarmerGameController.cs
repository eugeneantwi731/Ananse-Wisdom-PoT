using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "Anansi and the Farmer" crop match game.
// Put this on an empty GameObject. It builds the whole overlay by itself,
// and switches between landscape and portrait layouts automatically when the screen rotates.
// Open it from any button: drag the button into "Open Button", or call FarmerGameController.OpenGame().
public class FarmerGameController : MonoBehaviour
{
    [Header("1. Drag your 'Visit the Farmer' button here")]
    public Button openButton;
    [Tooltip("Open the game as soon as you press Play (handy for testing).")]
    public bool openOnStart = false;
    [Tooltip("If Open Button is empty, show a built-in 'Visit the Farmer' button so you can test right away.")]
    public bool showTestButton = true;

    [Header("2. Art (optional)")]
    [Tooltip("Leave empty to use Resources/FarmerGame/farmer.png (waist-up art works best)")]
    public Sprite farmerIllustration;
    [Tooltip("Ananse holding the pot. Leave empty to use Resources/FarmerGame/ananse_pot.png. He stands on the RIGHT, his dialogue on the LEFT.")]
    public Sprite ananseIllustration;
    [Tooltip("Ananse without the pot. Leave empty to use Resources/FarmerGame/ananse.png. Pick it per line with 'Ananse Pose'.")]
    public Sprite ananseNoPotIllustration;
    [Tooltip("How tall the characters are on screen (1080 = full screen height in landscape).")]
    public float characterHeight = 900f;
    [Tooltip("Leave empty for Unity's default font. Drag any .ttf font here to change it.")]
    public Font font;
    [Tooltip("0 = fully see-through (the AR camera shows behind the game). 1 = the solid green/cream background.")]
    [Range(0f, 1f)] public float backgroundOpacity = 0f;
    [Tooltip("Darkens the camera view behind the game so text and cards stay readable. 0 = off.")]
    [Range(0f, 0.8f)] public float cameraDim = 0.3f;
    [Tooltip("Leave empty to use Resources/FarmerGame/ananse_broken.png (shown on Game Over).")]
    public Sprite ananseBrokenPotIllustration;
    [TextArea(2, 4)] public string gameOverLine = "Aaah! My pot! It is broken... The farmer's knowledge is safe in my head, but I need a new pot from Nana Nyame before I try again.";

    [Header("3. Content (edit the text here)")]
    public List<CropInfo> crops = FarmerGameData.DefaultCrops();
    public List<FoodLevel> levels = FarmerGameData.DefaultLevels();
    public List<DialogueLine> introDialogue = FarmerGameData.Intro();
    public List<DialogueLine> preGameDialogue = FarmerGameData.PreGame();
    public List<DialogueLine> winDialogue = FarmerGameData.Win();
    public List<string> wrongMessages = FarmerGameData.Wrong();

    [Header("Events (optional)")]
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();
    [Tooltip("Fires when the player finishes all levels and taps Return to AR.")]
    public UnityEvent onCompleted = new UnityEvent();

    public static bool FarmerComplete => PlayerPrefs.GetInt("FarmerComplete", 0) == 1;
    public bool IsOpen => canvasGO != null && canvasGO.activeSelf;

    static readonly Color Brown = new Color32(107, 62, 38, 255), Gold = new Color32(232, 163, 61, 255), GoldDark = new Color32(183, 122, 34, 255),
        Green = new Color32(79, 124, 58, 255), GreenDark = new Color32(52, 86, 31, 255), Cream = new Color32(246, 235, 217, 255),
        Red = new Color32(176, 58, 46, 255), Ink = new Color32(61, 36, 22, 255), CardShadow = new Color32(224, 201, 164, 255),
        Tan = new Color32(205, 176, 138, 255), BoxLine = new Color32(230, 210, 177, 255), Hint = new Color32(154, 116, 86, 255),
        Dim = new Color32(40, 22, 10, 255);
    const float WRONG_HOLD = 0.7f, LOCK_AFTER = 0.45f, TYPE_SPEED = 40f;

    enum State { Closed, Dialogue, Lesson, Game, Win, Menu }
    const string FailKey = "Farmer_FailedBefore", SeenKey = "Farmer_LessonSeen";
    // Once he has reached the game once, the talk and the lesson get a "Skip to game" button.
    static bool LessonSeen => PlayerPrefs.GetInt(SeenKey, 0) == 1 || FarmerComplete;
    GameObject skipDlgBtn, skipLessonBtn;
    void SkipToGame() { dlg = null; PlayerPrefs.DeleteKey(FailKey); StartRun(0); }
    [Header("After a broken pot")]
    public string returnTitle = "WELCOME BACK";
    [TextArea] public string returnLine = "A new pot from Nana Nyame. This time I will keep it whole.";
    GameObject goTryBtn, goReviewBtn, goLeaveBtn, goLeaveOnlyBtn; Text goTitleText, goLineText; Outline goTitleOutline;
    class DlgBox { public RectTransform rect, tail; public Image tab; public Text speaker, body, hint; }
    class Dlg { public List<DialogueLine> lines; public int i; public float t0; public Action onEnd; public bool done; public DlgBox box; }
    class SlotView { public RectTransform rect; public Image fill, ring, pic; public CanvasGroup wrong; public RectTransform wrongRect; public GameObject remove; public float popT = -99f; }
    class DragState { public CropDrag card; public int phase; public Vector2 pos, grab, from; public float t0; }

    State state = State.Closed;
    Dlg dlg;
    GameObject canvasGO;
    RectTransform root, dialogueScreen, lessonScreen, gameScreen, winScreen, potsHolder;
    GameObject confirmModal, lcModal, gameOverModal;
    Sprite roundS, ringS, circleS, softS, bgS, potS, silhouetteS, farmerS;
    struct Spot { public Vector2 anchor, pos, size; }
    class DualSpot { public RectTransform r; public Spot land, port; }
    readonly List<DualSpot> dual = new List<DualSpot>();
    readonly List<CanvasScaler> scalers = new List<CanvasScaler>();
    bool? isPortrait;
    RectTransform canvasRoot;
    Rect lastSafe;
    Image cameraScrim, goAnanse, checkCircle, checkMark, checkBurst;
    RectTransform checkRoot, goTitle, goBubble;
    CanvasGroup checkGroup, msgGroup, goBubbleGroup;
    readonly List<Image> checkSparks = new List<Image>();
    Sprite ananseBrokenS, checkS, ringCircleS;
    float goT = -99f;
    Image backgroundImage, farmerImage, ananseImage;
    Sprite ananseS, ananseNoPotS;
    bool ananseSpeaking;
    float speakerT = -99f;
    static readonly Color BubbleLine = new Color32(61, 36, 22, 255); // same ink line as the weaving game's dialogue
    readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    LifeManager lives;

    DlgBox dlgMain, dlgWin;
    Image lessonFood; Text lessonFoodName, lessonTitleCounter, lessonText, lessonNextLabel; RectTransform lessonCropRow; CanvasGroup lessonBackGroup;
    int lessonIndex; float lessonT; string lessonRich = "";

    Image foodImage; Text foodName, levelLabel, msgText; RectTransform slotsRow, tray;
    readonly List<SlotView> slots = new List<SlotView>();
    readonly List<CropDrag> cards = new List<CropDrag>();
    string[] filled = new string[0];
    readonly HashSet<string> used = new HashSet<string>();
    int level, hoverSlot = -1;
    bool locked;
    float lockUntil, wrongT = -99f, shakeT = -99f, msgUntil, completeT = -1f, lcT;
    DragState drag;
    RectTransform ghost; Image ghostPic; Text ghostLabel;

    Button checkBtn; CanvasGroup checkBtnGroup, lcContinueGroup; RectTransform lcContinueRect;
    Text lcProgress, lcName, lcFact; Image lcFood; Button lcContinue; readonly List<Image> lcDots = new List<Image>();
    Image winGlow, winGold; Text winTitle; Button winReturn; float winT;

    float Now => Time.unscaledTime;

    // ---------------- setup ----------------
    void Awake()
    {
        // Older versions had the farmer scolding Ananse. The farmer doesn't know, so swap to Ananse's own thoughts.
        if (wrongMessages == null || wrongMessages.Count == 0 || wrongMessages.Contains("Look again, Ananse.")) wrongMessages = FarmerGameData.Wrong();
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MakeSprites();
        potS = LoadSprite("PotLives/pot");
        silhouetteS = LoadSprite("PotLives/silhouette");
        farmerS = farmerIllustration != null ? farmerIllustration : LoadSprite("FarmerGame/farmer");
        ananseS = ananseIllustration != null ? ananseIllustration : LoadSprite("FarmerGame/ananse_pot");
        ananseNoPotS = ananseNoPotIllustration != null ? ananseNoPotIllustration : LoadSprite("FarmerGame/ananse");
        if (ananseS == null) ananseS = ananseNoPotS;
        if (ananseNoPotS == null) ananseNoPotS = ananseS;
        ananseBrokenS = ananseBrokenPotIllustration != null ? ananseBrokenPotIllustration : LoadSprite("FarmerGame/ananse_broken");
        if (ananseBrokenS == null) ananseBrokenS = ananseS;
        BuildUI();
        SetupLives();
        EnsureEventSystem();
    }

    GameObject launcherGO;

    void OnValidate()
    {
        if (backgroundImage != null) backgroundImage.color = new Color(1f, 1f, 1f, backgroundOpacity);
        if (cameraScrim != null) cameraScrim.color = WithA(Dim, cameraDim);
    }

    void Start()
    {
        EnsureCamera();
        // On the village map (pins present) the pins open the game: no auto-open, no test button. Test Mode still allows both.
        bool inJourney = FindAnyObjectByType<JourneyPin>(FindObjectsInactive.Include) != null && !AnansiRuntime.TestMode;
        if (openOnStart && inJourney) Debug.Log("FarmerGame: 'Open On Start' is ignored on the village map. The Farmer pin opens the game.");
        if (openButton != null) openButton.onClick.AddListener(OpenGame);
        else if (showTestButton && !inJourney) BuildLauncher();
        if (openOnStart && !inJourney) OpenGame();
    }

    void EnsureCamera()
    {
        if (FindAnyObjectByType<Camera>() != null) return;
        var cam = new GameObject("Main Camera (auto)", typeof(Camera)).GetComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(29, 26, 23, 255);
        cam.transform.position = new Vector3(0, 0, -10);
        Debug.Log("Farmer game: no camera found, so one was added. Your AR camera will replace this later.");
    }

    void BuildLauncher()
    {
        launcherGO = new GameObject("FarmerLauncherCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        launcherGO.transform.SetParent(transform, false);
        var c = launcherGO.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 400;
        var sc = launcherGO.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        scalers.Add(sc);
        ApplyLayout(Screen.height > Screen.width);
        var lr = (RectTransform)launcherGO.transform;
        var b = MakeButton(lr, "Visit the Farmer", Gold, Ink, GoldDark, OpenGame, 44);
        At((RectTransform)b.transform, .5f, 0, 0, 120, 560, 110);
        if (FarmerComplete)
        {
            var done = Label(lr, "Farmer complete  ·  carver unlocked", 28, new Color32(185, 217, 154, 255), TextAnchor.MiddleCenter);
            At(done.rectTransform, .5f, 0, 0, 220, 900, 44);
        }
    }

    void SetupLives()
    {
        // Use the shared pot from the village. Only make one if this scene has none (e.g. testing the farm alone).
        lives = LifeManager.Instance;
        if (lives == null)
        {
            var lm = new GameObject("PotLives (farm test)");
            lives = lm.AddComponent<LifeManager>();
            lives.debugKeys = false; lives.invulnerabilitySeconds = 0f; lives.hitsPerPot = 1;
        }
        var go = new GameObject("FarmerPotHUD");
        go.transform.SetParent(transform, false);
        var hud = go.AddComponent<PotLifeBarHUD>();
        hud.hudParent = potsHolder; hud.flashParent = canvasRoot; hud.hudScale = 0.85f;
        lives.onGameOver.AddListener(() => { if (state == State.Game) ShowGameOver(); });
    }

    void EnsureEventSystem()
    {
        AnansiRuntime.EnsureEventSystem();
    }

    // ---------------- public API ----------------
    public void OpenGame()
    {
        if (IsOpen) return;
        canvasGO.SetActive(true);
        if (launcherGO != null) launcherGO.SetActive(false);
        onOpened.Invoke();
        if (!lives.HasPot && !AnansiRuntime.GiveTestPotIfAllowed(lives)) { CloseGame(); JourneyDialogue.Think(AnansiRuntime.NoPotThought); return; }
        if (PlayerPrefs.GetInt(FailKey, 0) == 1) { ShowReturnMenu(); return; } // failed before: Play again / Review lesson / Leave farm
        PlayDialogue(dialogueScreen, State.Dialogue, dlgMain, introDialogue, ToLesson);
    }

    // Tools > Anansi Game > Test a Section: open straight at one part of the farm.
    public void OpenForTest(string section)
    {
        if (section == "lesson") { PlayerPrefs.DeleteKey(SeenKey); PlayerPrefs.DeleteKey(FailKey); PlayerPrefs.Save(); OpenGame(); return; }
        if (section == "return") { PlayerPrefs.SetInt(FailKey, 1); PlayerPrefs.Save(); OpenGame(); return; }
        if (!lives.HasPot) lives.GivePot(3);
        PlayerPrefs.DeleteKey(FailKey);
        canvasGO.SetActive(true); if (launcherGO != null) launcherGO.SetActive(false); onOpened.Invoke();
        switch (section)
        {
            case "last": StartRun(levels.Count - 1); break;
            case "win": ToWin(); break;
            case "gameover": StartRun(0); ShowGameOver(); break;
            default: StartRun(0); break;
        }
    }

    public void CloseGame()
    {
        StopAllCoroutines();
        EndGhost();
        state = State.Closed; dlg = null;
        confirmModal.SetActive(false);
        canvasGO.SetActive(false);
        if (launcherGO != null) launcherGO.SetActive(true);
        onClosed.Invoke();
    }

    // ---------------- flow ----------------
    void ShowScreen(RectTransform screen, State st)
    {
        StopAllCoroutines();
        EndGhost();
        foreach (var s in new[] { dialogueScreen, lessonScreen, gameScreen, winScreen }) s.gameObject.SetActive(s == screen);
        state = st;
        bool canSkip = LessonSeen;
        if (skipDlgBtn) skipDlgBtn.SetActive(canSkip && st == State.Dialogue);
        if (skipLessonBtn) skipLessonBtn.SetActive(canSkip && st == State.Lesson);
        confirmModal.SetActive(false); lcModal.SetActive(false); gameOverModal.SetActive(false);
        gameScreen.anchoredPosition = Vector2.zero;
    }

    void PlayDialogue(RectTransform screen, State st, DlgBox box, List<DialogueLine> lines, Action onEnd)
    {
        ShowScreen(screen, st);
        if (lines == null || lines.Count == 0) { dlg = null; onEnd?.Invoke(); return; }
        dlg = new Dlg { lines = lines, i = 0, t0 = Now, onEnd = onEnd, box = box };
        ApplySpeaker(box, lines[0]);
    }

    void DlgTap()
    {
        if (dlg == null || dlg.done) return;
        string text = dlg.lines[dlg.i].text ?? "";
        if (Typed(dlg.t0) < text.Length) { dlg.t0 = -9999f; return; }
        if (dlg.i < dlg.lines.Count - 1) { dlg.i++; dlg.t0 = Now; ApplySpeaker(dlg.box, dlg.lines[dlg.i]); return; }
        dlg.done = true;
        dlg.onEnd?.Invoke();
    }

    void ApplySpeaker(DlgBox box, DialogueLine line)
    {
        bool farmer = line.speaker == Speaker.Farmer;
        Color c = farmer ? Green : Brown;
        SetTabName(box, farmer ? "Farmer" : "Kwaku Ananse");
        if (!farmer && ananseImage != null) ananseImage.sprite = line.anansePose == AnansePose.NoPot ? ananseNoPotS : ananseS;
        if (box == dlgMain)
        {
            bool changed = ananseSpeaking == farmer || speakerT < 0;
            ananseSpeaking = !farmer;
            if (changed) speakerT = Now;
            LayoutDialogue();
        }
        box.body.fontStyle = FontStyle.Normal;
        box.body.text = "";
    }

    int Typed(float t0) => Mathf.FloorToInt((Now - t0) * TYPE_SPEED);

    void ToLesson()
    {
        ShowScreen(lessonScreen, State.Lesson);
        dlg = null; lessonIndex = 0;
        ShowLessonPage();
    }

    void ShowLessonPage()
    {
        var L = levels[lessonIndex];
        lessonT = Now;
        lessonRich = HighlightCrops(L.lessonLine ?? "", L.crops);
        SetPic(lessonFood, FoodSprite(L.foodId));
        lessonFoodName.text = L.displayName;
        lessonTitleCounter.text = (lessonIndex + 1) + " / " + levels.Count;
        lessonBackGroup.alpha = lessonIndex > 0 ? 1f : 0.35f;
        lessonNextLabel.text = lessonIndex < levels.Count - 1 ? "Next" : "Start";
        foreach (Transform c in lessonCropRow) Destroy(c.gameObject);
        int n = L.crops.Length; float card = 170f, plus = 56f;
        float x = -(n * card + (n - 1) * plus) / 2f + card / 2f;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) { var p = Legible(Label(lessonCropRow, "+", 56, Brown, TextAnchor.MiddleCenter)); At(p.rectTransform, .5f, .5f, x - card / 2f - plus / 2f, 0, plus, 80); }
            var c = Panel("Crop", lessonCropRow, Cream, 26); c.raycastTarget = false;
            At(c.rectTransform, .5f, .5f, x, 0, card, card); AddShadow(c.gameObject);
            var pic = Pic(c.transform, CropSprite(L.crops[i])); At(pic.rectTransform, .5f, .5f, 0, 16, 140, 100);
            var lbl = Label(c.transform, CropName(L.crops[i]), 26, Ink, TextAnchor.MiddleCenter); At(lbl.rectTransform, .5f, 0, 0, 28, card, 36);
            x += card + plus;
        }
    }

    // Makes the crop names in a lesson line bold + green (the hint for the game).
    string HighlightCrops(string line, string[] cropIds)
    {
        var words = new List<string>();
        foreach (var id in cropIds)
        {
            words.Add(CropName(id));
            if (id == "maize") words.Add("corn");
            if (id == "palmoil") { words.Add("palm oil"); words.Add("palm nut"); }
            if (id == "beans") words.Add("bean");
        }
        words.Sort((a, b) => b.Length.CompareTo(a.Length));
        var parts = new List<string>();
        foreach (var w in words) parts.Add(System.Text.RegularExpressions.Regex.Escape(w));
        if (parts.Count == 0) return line;
        string pattern = @"\b(" + string.Join("|", parts) + @")s?\b";
        return System.Text.RegularExpressions.Regex.Replace(line, pattern,
            m => "<b><color=#3E6A2C>" + m.Value + "</color></b>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    // Typewriter that skips over rich-text tags and closes any left open.
    static string RichTyped(string rich, int visible)
    {
        var sb = new System.Text.StringBuilder();
        var open = new Stack<string>();
        int shown = 0, i = 0;
        while (i < rich.Length && shown < visible)
        {
            if (rich[i] == '<')
            {
                int end = rich.IndexOf('>', i);
                if (end < 0) break;
                string tag = rich.Substring(i, end - i + 1);
                if (tag.StartsWith("</")) { if (open.Count > 0) open.Pop(); }
                else open.Push(tag.StartsWith("<color") ? "</color>" : "</b>");
                sb.Append(tag); i = end + 1; continue;
            }
            sb.Append(rich[i]); i++; shown++;
        }
        while (i < rich.Length && rich[i] == '<' && rich.IndexOf("</", i) == i)
        {
            int end = rich.IndexOf('>', i); if (end < 0) break;
            sb.Append(rich, i, end - i + 1); i = end + 1; if (open.Count > 0) open.Pop();
        }
        while (open.Count > 0) sb.Append(open.Pop());
        return sb.ToString();
    }

    void LessonNext()
    {
        if (lessonIndex < levels.Count - 1) { lessonIndex++; ShowLessonPage(); }
        else PlayDialogue(dialogueScreen, State.Dialogue, dlgMain, preGameDialogue, () => StartRun(0));
    }

    void LessonBack() { if (lessonIndex > 0) { lessonIndex--; ShowLessonPage(); } }

    void StartRun(int lv)
    {
        PlayerPrefs.SetInt(SeenKey, 1); PlayerPrefs.Save();
        if (!lives.HasPot && !AnansiRuntime.GiveTestPotIfAllowed(lives)) { CloseGame(); JourneyDialogue.Think(AnansiRuntime.NoPotThought); return; }
        ShowScreen(gameScreen, State.Game);
        dlg = null;
        LoadLevel(lv);
    }

    void LoadLevel(int i)
    {
        StopAllCoroutines();
        EndGhost();
        SetGameplayVisible(true);
        level = i;
        var L = levels[i];
        levelLabel.text = "Level " + (i + 1) + " / " + levels.Count;
        SetPic(foodImage, FoodSprite(L.foodId));
        foodImage.rectTransform.localScale = Vector3.one;
        foodName.text = L.displayName;
        filled = new string[L.crops.Length];
        used.Clear();

        foreach (var s in slots) Destroy(s.rect.gameObject);
        slots.Clear();
        int n = L.crops.Length;
        for (int k = 0; k < n; k++)
        {
            var s = new SlotView();
            s.rect = At(NewRect("Slot " + (k + 1), slotsRow), .5f, .5f, (k - (n - 1) / 2f) * 176f, 0, 150, 150);
            s.fill = Panel("Fill", s.rect, Cream, 30); Fill(s.fill.rectTransform); s.fill.raycastTarget = false;
            s.ring = Ring(s.rect, Brown, 30);
            s.pic = Pic(s.rect, null); At(s.pic.rectTransform, .5f, .5f, 0, 0, 126, 100); s.pic.enabled = false;
            var w = Panel("Wrong", s.rect, Red, 32); w.raycastTarget = false; Fill(w.rectTransform, -3, -3, -3, -3);
            s.wrongRect = w.rectTransform;
            s.wrong = w.gameObject.AddComponent<CanvasGroup>(); s.wrong.alpha = 0; s.wrong.blocksRaycasts = false;
            var wl = Label(w.transform, "Wrong", 34, Cream, TextAnchor.MiddleCenter); Fill(wl.rectTransform);
            // tap a filled box to take the crop back out
            s.fill.raycastTarget = true; int idx = k;
            var sb = s.fill.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(() => ClearSlot(idx));
            var rm = Panel("Remove", s.rect, Ink, 20); rm.raycastTarget = false; At(rm.rectTransform, 1f, 1f, -14, -14, 40, 40);
            for (int b = 0; b < 2; b++) // drawn cross, exactly centred (a font "×" sits off-centre)
            {
                var bar = Panel("Cross", rm.transform, Cream, 2.5f); bar.raycastTarget = false;
                At(bar.rectTransform, .5f, .5f, 0, 0, 20, 5); bar.rectTransform.localRotation = Quaternion.Euler(0, 0, b == 0 ? 45 : -45);
            }
            s.remove = rm.gameObject; s.remove.SetActive(false);
            s.wrong.transform.SetAsLastSibling();
            slots.Add(s);
        }

        var order = new List<CropDrag>(cards);
        for (int k = order.Count - 1; k > 0; k--) { int j = UnityEngine.Random.Range(0, k + 1); var tmp = order[k]; order[k] = order[j]; order[j] = tmp; }
        for (int k = 0; k < order.Count; k++)
        {
            order[k].transform.SetSiblingIndex(k);
            order[k].group.alpha = 1f; order[k].pic.color = Color.white;
        }

        locked = false; lockUntil = 0; completeT = -1f; wrongT = -99f; msgUntil = 0; hoverSlot = -1;
        lcModal.SetActive(false); gameOverModal.SetActive(false);
        if (checkGroup != null) checkGroup.alpha = 0;
    }

    IEnumerator AfterWrong()
    {
        yield return new WaitForSecondsRealtime(WRONG_HOLD);
        if (state != State.Game) yield break;
        lives.Fail();
        shakeT = Now;
        if (wrongMessages.Count > 0) msgText.text = "<b>Ananse:</b> <i>" + wrongMessages[UnityEngine.Random.Range(0, wrongMessages.Count)] + "</i>";
        msgUntil = Now + 2f;
        if (lives.Lives <= 0) locked = true;
    }

    IEnumerator OpenLevelComplete()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        yield return PlayCheck();
        var L = levels[level];
        lcProgress.text = ("Level " + (level + 1) + " of " + levels.Count + " complete").ToUpper();
        for (int k = 0; k < lcDots.Count; k++) lcDots[k].color = k <= level ? Gold : new Color32(220, 199, 166, 255);
        SetPic(lcFood, FoodSprite(L.foodId));
        lcName.text = L.displayName;
        lcFact.text = L.fact;
        lcT = Now;
        lcContinue.gameObject.SetActive(true); lcContinueGroup.alpha = 0f; lcContinueGroup.interactable = lcContinueGroup.blocksRaycasts = false;
        lcModal.SetActive(true);
        lcModal.transform.SetAsLastSibling();
    }

    void LevelContinue()
    {
        if (level < levels.Count - 1) LoadLevel(level + 1);
        else ToWin();
    }

    void SetGameplayVisible(bool v)
    {
        foreach (Transform c in gameScreen)
            if (c.gameObject != gameOverModal && c.gameObject != lcModal) c.gameObject.SetActive(v);
    }

    void ShowGameOver()
    {
        EndGhost();
        PlayerPrefs.SetInt(FailKey, 1); PlayerPrefs.Save();
        SetGameplayVisible(false);
        SetOverMode(false);
        gameOverModal.SetActive(true);
        gameOverModal.transform.SetAsLastSibling();
        goT = Now;
    }

    // Coming back with a new pot after a game over.
    void ShowReturnMenu()
    {
        ShowScreen(gameScreen, State.Menu);
        dlg = null;
        SetGameplayVisible(false);
        SetOverMode(true);
        gameOverModal.SetActive(true);
        gameOverModal.transform.SetAsLastSibling();
        goT = Now;
    }

    // false = pot just broke (only "Leave farm"), true = back with a new pot (all three choices)
    void SetOverMode(bool back)
    {
        goTitleText.text = back ? returnTitle : "GAME OVER";
        goTitleOutline.effectColor = back ? Green : Red;
        goLineText.text = back ? returnLine : gameOverLine;
        if (goAnanse != null) { var s = back ? (ananseS != null ? ananseS : ananseBrokenS) : ananseBrokenS; goAnanse.sprite = s; goAnanse.enabled = s != null; }
        goTryBtn.SetActive(back); goReviewBtn.SetActive(back); goLeaveBtn.SetActive(back); goLeaveOnlyBtn.SetActive(!back);
    }

    void ToWin()
    {
        PlayDialogue(winScreen, State.Win, dlgWin, winDialogue, null);
        winT = Now;
        winGold.fillAmount = 0; SetAlpha(winTitle, 0); winReturn.gameObject.SetActive(false);
        if (winFx != null) winFx.Evaluate(0f);
    }

    void ReturnToAR()
    {
        PlayerPrefs.SetInt("FarmerComplete", 1); PlayerPrefs.DeleteKey(FailKey); PlayerPrefs.Save();
        lives.AddKnowledge(0);          // Farmer slot (no-op on replays)
        if (!Bag.Add(Bag.Food)) Debug.Log("Bag already holds 3 food.");
        onCompleted.Invoke();
        CloseGame();
        JourneyPin.RefreshAll(); // Carver pin appears
    }

    // Back with a new pot: "Play again" skips the lessons, "Review lesson" goes through them first.
    void TryAgain() { PlayerPrefs.DeleteKey(FailKey); StartRun(0); }
    void ReviewLesson() { PlayerPrefs.DeleteKey(FailKey); ToLesson(); }

    void AskClose() { confirmModal.SetActive(true); confirmModal.transform.SetAsLastSibling(); }

    // ---------------- drag & drop ----------------
    Vector2 ToLocal(Vector2 screen) { RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var p); return p; }
    Vector2 CardHome(CropDrag c) => root.InverseTransformPoint(c.transform.position);

    public void BeginDrag(CropDrag card, PointerEventData e)
    {
        if (state != State.Game || locked || drag != null || Now < lockUntil || confirmModal.activeSelf || used.Contains(card.cropId)) return;
        var home = CardHome(card);
        drag = new DragState { card = card, phase = 0, pos = ToLocal(e.position), grab = Vector2.zero, from = home, t0 = Now };
        ghostPic.sprite = CropSprite(card.cropId); ghostPic.enabled = ghostPic.sprite != null;
        ghostLabel.text = CropName(card.cropId);
        ghost.anchoredPosition = home - root.rect.center;
        ghost.gameObject.SetActive(true);
        ghost.SetAsLastSibling();
        card.group.alpha = 0.5f;
    }

    public void Drag(CropDrag card, PointerEventData e)
    {
        if (drag == null || drag.card != card || drag.phase != 0) return;
        drag.pos = ToLocal(e.position);
        hoverSlot = HitSlot(e.position);
    }

    public void EndDrag(CropDrag card, PointerEventData e)
    {
        if (drag == null || drag.card != card || drag.phase != 0) return;
        hoverSlot = -1;
        int si = HitSlot(e.position);
        if (si < 0 || used.Contains(card.cropId)) { StartReturn(); return; }
        if (filled[si] != null) ReleaseCrop(filled[si]); // dropping on a full box swaps the crop out
        filled[si] = card.cropId; used.Add(card.cropId);
        var s = slots[si]; s.pic.sprite = CropSprite(card.cropId); s.pic.enabled = s.pic.sprite != null; s.popT = Now;
        EndGhost();
        card.group.alpha = 0.4f; card.pic.color = new Color(.55f, .55f, .55f, 1f);
    }

    // Duolingo-style: nothing is judged until the player presses Check.
    float checkReadyT = -9f; bool wasCheckReady;
    void CheckAnswer()
    {
        if (state != State.Game || locked || Now < lockUntil || drag != null || !Array.TrueForAll(filled, f => f != null)) return;
        var L = levels[level];
        if (Array.TrueForAll(filled, f => Array.IndexOf(L.crops, f) >= 0)) { locked = true; completeT = Now; StartCoroutine(OpenLevelComplete()); }
        else
        {
            wrongT = Now; // every box says Wrong, then the pot cracks; the crops stay so he can decide what to take out
            lockUntil = Now + WRONG_HOLD + LOCK_AFTER;
            StartCoroutine(AfterWrong());
        }
    }
    void ClearSlot(int i)
    {
        if (state != State.Game || locked || Now < lockUntil || drag != null || i < 0 || i >= filled.Length || filled[i] == null) return;
        ReleaseCrop(filled[i]); filled[i] = null;
        slots[i].pic.enabled = false; slots[i].popT = Now;
    }
    void ReleaseCrop(string id)
    {
        used.Remove(id);
        foreach (var c in cards) if (c.cropId == id) { c.group.alpha = 1f; c.pic.color = Color.white; }
    }

    void StartReturn() { if (drag == null) return; drag.phase = 2; drag.t0 = Now; drag.from = drag.pos; }

    void EndGhost()
    {
        if (drag != null && !used.Contains(drag.card.cropId)) drag.card.group.alpha = 1f;
        drag = null;
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    int HitSlot(Vector2 screen)
    {
        for (int i = 0; i < slots.Count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(slots[i].rect, screen, null)) return i;
        return -1;
    }

    // ---------------- every frame ----------------
    void Update()
    {
        CheckOrientation();
        if (!IsOpen) return;
        float t = Now;
        if (dlg != null && dlg.box != null)
        {
            string text = dlg.lines[dlg.i].text ?? "";
            int n = Mathf.Min(Typed(dlg.t0), text.Length);
            dlg.box.body.text = text.Substring(0, n);
            SetAlpha(dlg.box.hint, !dlg.done && n >= text.Length ? 1f : 0f);
        }
        if (state == State.Dialogue) AnimateSpeaker();
        if (state == State.Lesson)
        {
            lessonText.text = RichTyped(lessonRich, Typed(lessonT));
        }
        if (state == State.Game) UpdateGame(t);
        if (state == State.Win) UpdateWin(t);
    }

    // ---------- win screen: the shared 3D pot celebration (Core/PotCelebration.cs, reused by every game) ----------
    [Header("Win screen")]
    [Tooltip("Colour of the glowing knowledge orb that flies into the pot when the farm is finished.")]
    public Color winOrbColor = new Color32(150, 205, 110, 255);
    PotCelebration winFx; float winReturnT;
    void UpdateWinFx(float e)
    {
        winFx.Evaluate(e);
        if (e >= PotCelebration.SettledTime - .2f && !winReturn.gameObject.activeSelf) { winReturn.gameObject.SetActive(true); winReturnT = Time.unscaledTime; }
        if (winReturn.gameObject.activeSelf)
        {
            float k = EaseOut((Time.unscaledTime - winReturnT) / .45f);
            SetAlpha(winTitle, k);
            var cgr = winReturn.GetComponent<CanvasGroup>(); if (cgr == null) cgr = winReturn.gameObject.AddComponent<CanvasGroup>(); cgr.alpha = k;
            winReturn.transform.localScale = Vector3.one * Mathf.Lerp(.9f, 1f, k);
        }
    }

    void UpdateGame(float t)
    {
        if (drag != null)
        {
            Vector2 p = drag.pos;
            if (drag.phase == 0) p = Vector2.Lerp(drag.from, drag.pos, EaseOut((t - drag.t0) / 0.08f)); // glides to the finger, then stays under it
            if (drag.phase == 1)
            {
                float e = t - drag.t0;
                p = drag.from + new Vector2(Mathf.Sin(e * 90f) * 14f * Mathf.Clamp01(1f - e / 0.32f), 0);
                if (e >= 0.32f) { drag.phase = 2; drag.t0 = t; drag.from = drag.pos; }
            }
            else if (drag.phase == 2)
            {
                float k = EaseOut((t - drag.t0) / 0.2f);
                p = Vector2.Lerp(drag.from, CardHome(drag.card), k);
                if (k >= 1f) EndGhost();
            }
            if (drag != null) ghost.anchoredPosition = p - root.rect.center;
        }

        float we = t - wrongT;
        bool wrong = we < WRONG_HOLD;
        float wA = wrong ? Mathf.Min(Mathf.Clamp01(we / 0.12f), Mathf.Clamp01((WRONG_HOLD - we) / 0.15f)) : 0f;
        float wS = 0.85f + 0.15f * EaseOut(we / 0.12f);
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            bool f = filled[i] != null, hov = hoverSlot == i, good = completeT >= 0;
            s.ring.color = wrong ? Red : good ? Green : hov ? Gold : f ? Brown : WithA(Brown, .8f);
            s.fill.color = wrong ? WithA(Red, .3f) : good ? (Color)new Color32(226, 240, 214, 255) : hov ? (Color)new Color32(250, 225, 180, 255) : f ? Cream : WithA(Cream, .88f);
            s.remove.SetActive(f && !good && !wrong && !locked);
            s.wrong.alpha = wA;
            s.wrongRect.localScale = Vector3.one * wS;
            float pe = t - s.popT;
            s.rect.localScale = Vector3.one * (pe < 0.25f ? 1f + 0.2f * (1f - EaseOut(pe / 0.25f)) : 1f);
        }

        float ce = completeT >= 0 ? t - completeT : -1f;
        float pulse = ce >= 0 && ce < 0.7f ? Mathf.Sin(ce / 0.7f * Mathf.PI) : 0f;
        foodImage.rectTransform.localScale = Vector3.one * (1f + 0.12f * pulse);

        msgGroup.alpha = t < msgUntil ? 1f : 0f;
        if (gameOverModal.activeSelf)
        {
            float g = t - goT;
            goTitle.localScale = Vector3.one * (1f + 0.6f * (1f - EaseOut(g / 0.4f)));
            if (goAnanse != null) SetAlpha(goAnanse, EaseOut((g - 0.15f) / 0.4f));
            goBubbleGroup.alpha = EaseOut((g - 0.4f) / 0.3f);
            goBubble.localScale = Vector3.one * (0.9f + 0.1f * EaseOut((g - 0.4f) / 0.3f));
        }
        float se = t - shakeT;
        gameScreen.anchoredPosition = new Vector2(se < 0.3f ? Mathf.Sin(se * 90f) * 6f * (1f - se / 0.3f) : 0f, 0);
        if (lcModal.activeSelf)
        {
            float k = EaseOut((t - lcT - 0.7f) / 0.45f); // fades and rises in after the fact is shown
            lcContinueGroup.alpha = k; lcContinueGroup.interactable = lcContinueGroup.blocksRaycasts = k > .8f;
            lcContinueRect.anchoredPosition = new Vector2(0, 90 - 24 * (1f - k)); lcContinueRect.localScale = Vector3.one * (0.94f + 0.06f * k);
        }
        // Check button: only when every box has a crop; hidden while Ananse's message shows
        bool allIn = Array.TrueForAll(filled, x => x != null), canCheck = allIn && !locked && t >= lockUntil && drag == null;
        bool msgOn = t < msgUntil || wrong;
        float cTarget = msgOn || completeT >= 0 ? 0f : canCheck ? 1f : .4f;
        checkBtnGroup.alpha = Mathf.MoveTowards(checkBtnGroup.alpha, cTarget, Time.unscaledDeltaTime * 5f);
        checkBtnGroup.interactable = checkBtnGroup.blocksRaycasts = canCheck && !msgOn;
        float cp = t - checkReadyT; checkBtn.transform.localScale = Vector3.one * (cp < .3f ? 1f + .08f * Mathf.Sin(cp / .3f * Mathf.PI) : 1f);
        if (canCheck && !wasCheckReady) checkReadyT = t; wasCheckReady = canCheck;
    }

    void UpdateWin(float t)
    {
        if (winFx != null) { UpdateWinFx(t - winT); return; }
        float k = EaseOut((t - winT) / 1.5f);
        winGold.fillAmount = k;
        SetAlpha(winGlow, 0.6f * k);
        winGlow.rectTransform.localScale = Vector3.one * (0.8f + 0.3f * k);
        if (k >= 1f && !winReturn.gameObject.activeSelf) { SetAlpha(winTitle, 1f); winReturn.gameObject.SetActive(true); }
    }

    // ---------------- UI building ----------------
    void BuildUI()
    {
        canvasGO = new GameObject("FarmerGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        var sc = canvasGO.GetComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        scalers.Add(sc);
        root = (RectTransform)canvasGO.transform;

        var bg = NewImage("Background", root, bgS); Fill(bg.rectTransform);
        bg.color = new Color(1f, 1f, 1f, backgroundOpacity);
        bg.raycastTarget = backgroundOpacity > 0f;
        cameraScrim = NewImage("CameraDim", root, null); Fill(cameraScrim.rectTransform);
        cameraScrim.color = WithA(Dim, cameraDim); cameraScrim.raycastTarget = false;
        canvasRoot = root;
        root = NewRect("SafeArea", canvasRoot);
        ApplySafeArea();
        backgroundImage = bg;

        BuildDialogueScreen();
        BuildLessonScreen();
        BuildGameScreen();
        BuildWinScreen();
        BuildConfirm();
        BuildGhost();
        ApplyLayout(Screen.height > Screen.width);
        canvasGO.SetActive(false);
    }

    void BuildDialogueScreen()
    {
        dialogueScreen = Fill(NewRect("Dialogue", root));
        var tap = NewImage("TapArea", dialogueScreen, null); Fill(tap.rectTransform); tap.color = Color.clear;
        var tb = tap.gameObject.AddComponent<Button>(); tb.transition = Selectable.Transition.None; tb.onClick.AddListener(DlgTap);

        if (farmerS != null) farmerImage = Pic(dialogueScreen, farmerS);
        if (ananseS != null) ananseImage = Pic(dialogueScreen, ananseS);
        dlgMain = BuildDlgBox(dialogueScreen, .5f, .5f, 1000, 460, true);
        dlgMain.body.fontSize = 42;
        skipDlgBtn = SkipButton(dlgMain.rect, -58);
        LayoutDialogue();
        CloseButton(dialogueScreen);
    }

    void BuildLessonScreen()
    {
        lessonScreen = Fill(NewRect("Lesson", root));
        var title = Legible(Label(lessonScreen, "The Farmer's Lesson", 48, Brown, TextAnchor.MiddleLeft)); At(title.rectTransform, 0, 1, 380, -64, 640, 64);
        lessonTitleCounter = Legible(Label(lessonScreen, "1 / 7", 30, Green, TextAnchor.MiddleLeft)); At(lessonTitleCounter.rectTransform, 0, 1, 380, -118, 640, 40);

        lessonFood = Pic(lessonScreen, null); Dual(At(lessonFood.rectTransform, .28f, .6f, 0, 0, 620, 440), .5f, .74f, 0, 0, 760, 420);
        lessonFoodName = Legible(Label(lessonScreen, "", 64, Brown, TextAnchor.MiddleCenter)); Dual(At(lessonFoodName.rectTransform, .28f, .34f, 0, 0, 800, 80), .5f, .6f, 0, 0, 900, 80);
        lessonCropRow = Dual(At(NewRect("Crops", lessonScreen), .28f, .17f, 0, 0, 900, 180), .5f, .49f, 0, 0, 1000, 180);

        var box = Panel("FarmerBox", lessonScreen, Cream, 40); Dual(At(box.rectTransform, .74f, .46f, 0, 0, 820, 560), .5f, .19f, 0, 0, 880, 560);
        Ring(box.transform, BubbleLine, 30);
        var bb = box.gameObject.AddComponent<Button>(); bb.transition = Selectable.Transition.None; bb.onClick.AddListener(() => lessonT = -9999f);
        NameTab(box.transform, "Farmer", out var lessonTab); lessonTab.rectTransform.sizeDelta = new Vector2(230, 72);
        lessonText = Label(box.transform, "", 38, Ink, TextAnchor.UpperLeft); lessonText.fontStyle = FontStyle.Normal; Place(lessonText.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 170), new Vector2(-48, -56));

        var back = MakeButton(box.transform, "Back", Cream, Brown, null, LessonBack, 40, Tan);
        Place((RectTransform)back.transform, new Vector2(0, 0), new Vector2(.4f, 0), new Vector2(40, 40), new Vector2(-12, 140));
        lessonBackGroup = back.gameObject.AddComponent<CanvasGroup>();
        var next = MakeButton(box.transform, "Next", Gold, Ink, GoldDark, LessonNext, 40);
        Place((RectTransform)next.transform, new Vector2(.4f, 0), new Vector2(1, 0), new Vector2(12, 40), new Vector2(-40, 140));
        lessonNextLabel = next.GetComponentInChildren<Text>();
        skipLessonBtn = SkipButton(box.rectTransform, -58);
        CloseButton(lessonScreen);
    }

    // "Skip to game >" pill just under the bottom-right corner of a dialogue box.
    GameObject SkipButton(RectTransform box, float y)
    {
        var b = KButton(box, "Skip  >>", Brown, Cream, Ink, SkipToGame, 27, Ink, 33);
        var r = At((RectTransform)b.transform, 1, 0, -107, y, 195, 66);
        r.SetAsLastSibling();
        b.gameObject.SetActive(false);
        return b.gameObject;
    }

    void BuildGameScreen()
    {
        gameScreen = Fill(NewRect("Game", root));
        potsHolder = At(NewRect("PotsHolder", gameScreen), 0, 1, 40 + 210, -24 - 60, 420, 120);

        var pill = Panel("LevelPill", gameScreen, Cream, 38); pill.raycastTarget = false; Dual(At(pill.rectTransform, .5f, 1, 0, -80, 300, 76), .5f, 1, 20, -80, 260, 76);
        levelLabel = Label(pill.transform, "Level 1 / 7", 36, Brown, TextAnchor.MiddleCenter); Fill(levelLabel.rectTransform);
        CloseButton(gameScreen);

        foodImage = Pic(gameScreen, null); Dual(At(foodImage.rectTransform, .27f, .63f, 0, 0, 580, 400), .5f, .71f, 0, 0, 760, 460);
        foodName = Legible(Label(gameScreen, "", 60, Brown, TextAnchor.MiddleCenter)); Dual(At(foodName.rectTransform, .27f, .38f, 0, 0, 800, 80), .5f, .575f, 0, 0, 900, 80);
        slotsRow = Dual(At(NewRect("Slots", gameScreen), .27f, .225f, 0, 0, 800, 160), .5f, .49f, 0, 0, 900, 160);
        var msgPill = Panel("WrongMessage", gameScreen, Red, 34); msgPill.raycastTarget = false;
        Dual(At(msgPill.rectTransform, .27f, .085f, 0, 0, 820, 72), .5f, .42f, 0, 0, 980, 72);
        msgGroup = msgPill.gameObject.AddComponent<CanvasGroup>(); msgGroup.alpha = 0; msgGroup.blocksRaycasts = false;
        msgText = Label(msgPill.transform, "", 30, Cream, TextAnchor.MiddleCenter); msgText.fontStyle = FontStyle.Normal;
        msgText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var hl = msgPill.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(34, 34, 0, 0); hl.childAlignment = TextAnchor.MiddleCenter;
        hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
        var fit = msgPill.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        checkBtn = MakeButton(gameScreen, "Check", Gold, Ink, GoldDark, CheckAnswer, 40);
        Dual(At((RectTransform)checkBtn.transform, .27f, .085f, 0, 0, 380, 92), .5f, .42f, 0, 0, 520, 92);
        checkBtnGroup = checkBtn.gameObject.AddComponent<CanvasGroup>(); checkBtnGroup.alpha = .4f;

        var trayImg = Panel("Tray", gameScreen, WithA(Dim, .45f), 40); trayImg.raycastTarget = false;
        tray = Dual(At(trayImg.rectTransform, .73f, .46f, 0, 0, 880, 500), .5f, .2f, 0, 0, 880, 500);
        var grid = tray.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(195, 215); grid.spacing = new Vector2(20, 20);
        grid.padding = new RectOffset(20, 20, 20, 20);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.MiddleCenter;
        foreach (var c in crops)
        {
            var card = Panel("Crop " + c.id, tray, Cream, 28); AddShadow(card.gameObject);
            var d = card.gameObject.AddComponent<CropDrag>();
            d.game = this; d.cropId = c.id;
            d.group = card.gameObject.AddComponent<CanvasGroup>();
            d.pic = Pic(card.transform, CropSprite(c.id)); At(d.pic.rectTransform, .5f, .5f, 0, 22, 170, 140);
            var l = Label(card.transform, c.displayName, 28, Ink, TextAnchor.MiddleCenter); At(l.rectTransform, .5f, 0, 0, 32, 195, 40);
            cards.Add(d);
        }

        BuildCheck();
        BuildLevelComplete();
        BuildGameOver();
    }

    // Big animated check shown over the food when a level is solved.
    void BuildCheck()
    {
        checkRoot = Dual(At(NewRect("CorrectCheck", gameScreen), .27f, .63f, 0, 0, 300, 300), .5f, .71f, 0, 0, 300, 300);
        checkGroup = checkRoot.gameObject.AddComponent<CanvasGroup>(); checkGroup.alpha = 0; checkGroup.blocksRaycasts = false;
        for (int k = 0; k < 10; k++) { var sp = Circle(checkRoot, Gold); At(sp.rectTransform, .5f, .5f, 0, 0, 26, 26); checkSparks.Add(sp); }
        checkBurst = NewImage("Burst", checkRoot, ringCircleS); checkBurst.raycastTarget = false; checkBurst.color = new Color32(255, 214, 110, 255); At(checkBurst.rectTransform, .5f, .5f, 0, 0, 300, 300);
        checkCircle = Circle(checkRoot, Green); At(checkCircle.rectTransform, .5f, .5f, 0, 0, 260, 260);
        var ol = checkCircle.gameObject.AddComponent<Outline>(); ol.effectColor = Cream; ol.effectDistance = new Vector2(5, -5);
        checkMark = NewImage("Tick", checkCircle.transform, checkS); checkMark.raycastTarget = false; checkMark.color = Cream; Fill(checkMark.rectTransform);
        checkMark.type = Image.Type.Filled; checkMark.fillMethod = Image.FillMethod.Horizontal; checkMark.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    IEnumerator PlayCheck()
    {
        const float D = 1.15f;
        checkRoot.SetAsLastSibling();
        checkGroup.alpha = 1f; checkMark.fillAmount = 0f;
        for (float e = 0; e < D; e += Time.unscaledDeltaTime)
        {
            checkCircle.rectTransform.localScale = Vector3.one * (e < .35f ? BackOut(e / .35f) : 1f);
            checkMark.fillAmount = Mathf.Clamp01((e - .2f) / .3f);
            float r = Mathf.Clamp01((e - .22f) / .55f);
            checkBurst.rectTransform.localScale = Vector3.one * (0.9f + r * 0.9f);
            SetAlpha(checkBurst, r > 0f ? (1f - r) * .9f : 0f);
            for (int k = 0; k < checkSparks.Count; k++)
            {
                float a = k / (float)checkSparks.Count * Mathf.PI * 2f;
                checkSparks[k].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (120f + 110f * EaseOut(r));
                checkSparks[k].rectTransform.localScale = Vector3.one * (1f - r);
                SetAlpha(checkSparks[k], r > 0f ? 1f - r : 0f);
            }
            checkGroup.alpha = e > D - .15f ? (D - e) / .15f : 1f;
            yield return null;
        }
        checkGroup.alpha = 0f;
    }

    static float BackOut(float k) { const float c = 1.70158f; k = Mathf.Clamp01(k) - 1f; return 1f + (c + 1f) * k * k * k + c * k * k; }

    // Cream text with a dark outline + drop shadow: readable over any camera image.
    Text Legible(Text t)
    {
        t.color = Cream;
        var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color32(45, 24, 12, 230); o.effectDistance = new Vector2(3, -3);
        var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .45f); sh.effectDistance = new Vector2(0, -5);
        return t;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    void BuildLevelComplete()
    {
        var dim = NewImage("LevelComplete", gameScreen, null); Fill(dim.rectTransform, -600, -600, -600, -600); dim.color = WithA(Dim, .55f);
        lcModal = dim.gameObject;
        var card = Panel("Card", dim.transform, Cream, 44); Dual(At(card.rectTransform, .5f, .5f, 0, 0, 1000, 900), .5f, .5f, 0, 0, 880, 900);
        lcProgress = Label(card.transform, "", 28, Green, TextAnchor.MiddleCenter); At(lcProgress.rectTransform, .5f, 1, 0, -64, 900, 40);
        var dots = At(NewRect("Dots", card.transform), .5f, 1, 0, -112, 400, 20);
        for (int k = 0; k < levels.Count; k++)
        {
            var d = Circle(dots, Gold); At(d.rectTransform, .5f, .5f, (k - (levels.Count - 1) / 2f) * 30f, 0, 18, 18);
            lcDots.Add(d);
        }
        lcFood = Pic(card.transform, null); At(lcFood.rectTransform, .5f, .5f, 0, 140, 700, 300);
        lcName = Label(card.transform, "", 56, Brown, TextAnchor.MiddleCenter); At(lcName.rectTransform, .5f, .5f, 0, -50, 900, 70);
        lcFact = Label(card.transform, "", 32, Ink, TextAnchor.UpperCenter); At(lcFact.rectTransform, .5f, .5f, 0, -170, 860, 160); lcFact.fontStyle = FontStyle.Normal;
        lcContinue = MakeButton(card.transform, "Continue", Gold, Ink, GoldDark, LevelContinue, 42);
        lcContinueRect = At((RectTransform)lcContinue.transform, .5f, 0, 0, 90, 860, 100);
        lcContinueGroup = lcContinue.gameObject.AddComponent<CanvasGroup>();
        lcModal.SetActive(false);
    }

    void BuildGameOver()
    {
        var dim = NewImage("GameOver", gameScreen, null); Fill(dim.rectTransform, -600, -600, -600, -600); dim.color = WithA(Dim, .82f);
        gameOverModal = dim.gameObject;
        var content = Fill(NewRect("Content", dim.transform), 600, 600, 600, 600);
        dim.color = WithA(Dim, .92f);
        if (ananseBrokenS != null)
        {
            goAnanse = Pic(content, ananseBrokenS);
            float h = 800f, w = h * ananseBrokenS.rect.width / ananseBrokenS.rect.height, ph = 700f, pw = w * ph / h;
            Dual(At(goAnanse.rectTransform, 1, 0, -30 - w / 2f, h / 2f, w, h), .5f, .5f, 0, 290, pw, ph);
        }
        var title = Label(content, "GAME OVER", 150, Cream, TextAnchor.MiddleCenter);
        title.resizeTextForBestFit = true; title.resizeTextMinSize = 60; title.resizeTextMaxSize = 150; title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate;
        var tol = title.gameObject.AddComponent<Outline>(); tol.effectColor = Red; tol.effectDistance = new Vector2(6, -6); goTitleOutline = tol; goTitleText = title;
        var tsh = title.gameObject.AddComponent<Shadow>(); tsh.effectColor = new Color(0, 0, 0, .55f); tsh.effectDistance = new Vector2(0, -12);
        goTitle = Dual(At(title.rectTransform, .5f, 1, 0, -150, 1600, 190), .5f, 1, 0, -200, 820, 170);

        var bub = Panel("AnanseBubble", content, Cream, 40); bub.raycastTarget = false;
        goBubble = Dual(At(bub.rectTransform, .5f, .5f, -410, 20, 860, 300), .5f, .5f, 0, -200, 860, 320);
        goBubbleGroup = bub.gameObject.AddComponent<CanvasGroup>();
        Ring(bub.transform, BubbleLine, 30);
        NameTab(bub.transform, "Kwaku Ananse", out var goTab); goTab.rectTransform.sizeDelta = new Vector2(340, 72);
        var goLine = Label(bub.transform, gameOverLine, 40, Ink, TextAnchor.MiddleLeft); goLine.fontStyle = FontStyle.Italic;
        Place(goLine.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 30), new Vector2(-48, -50)); goLineText = goLine;

        var only = MakeButton(content, "Leave farm", Gold, Ink, GoldDark, CloseGame, 40); Dual(At((RectTransform)only.transform, .5f, .5f, -410, -250, 420, 100), .5f, .5f, 0, -440, 720, 100); goLeaveOnlyBtn = only.gameObject;
        var a = MakeButton(content, "Play again", Gold, Ink, GoldDark, TryAgain, 40); Dual(At((RectTransform)a.transform, .5f, .5f, -700, -250, 270, 100), .5f, .5f, 0, -440, 720, 100);
        var b = MakeButton(content, "Review lesson", Color.clear, Cream, null, ReviewLesson, 36, Cream); Dual(At((RectTransform)b.transform, .5f, .5f, -410, -250, 270, 100), .5f, .5f, 0, -560, 720, 100);
        var c = MakeButton(content, "Leave farm", Color.clear, Cream, null, CloseGame, 36, WithA(Cream, .5f)); Dual(At((RectTransform)c.transform, .5f, .5f, -120, -250, 270, 100), .5f, .5f, 0, -680, 720, 100);
        goTryBtn = a.gameObject; goReviewBtn = b.gameObject; goLeaveBtn = c.gameObject;
        gameOverModal.SetActive(false);
    }

    RectTransform LineCard(Transform parent, string who, Color whoC, string text, bool italic, float y)
    {
        var p = Panel("Line", parent, Cream, 30); p.raycastTarget = false; At(p.rectTransform, .5f, .5f, 0, y, 1100, 130);
        var n = Label(p.transform, who, 28, whoC, TextAnchor.UpperLeft); Place(n.rectTransform, Vector2.zero, Vector2.one, new Vector2(36, 0), new Vector2(-36, -20));
        var t = Label(p.transform, text, 32, Ink, TextAnchor.UpperLeft); Place(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(36, 10), new Vector2(-36, -62));
        if (italic) t.fontStyle = FontStyle.BoldAndItalic;
        return p.rectTransform;
    }

    void BuildWinScreen()
    {
        winScreen = Fill(NewRect("Win", root));
        float h = 520f, w = potS != null ? h * potS.rect.width / potS.rect.height : 420f;
        winGlow = NewImage("Glow", winScreen, softS); winGlow.raycastTarget = false; Dual(At(winGlow.rectTransform, .3f, .56f, 0, 0, 900, 900), .5f, .68f, 0, 0, 900, 900);
        winGlow.color = new Color(1f, .83f, .42f, 0f);
        var pot = Pic(winScreen, potS); Dual(At(pot.rectTransform, .3f, .56f, 0, 0, w, h), .5f, .68f, 0, 0, w, h);
        winGold = Pic(winScreen, silhouetteS); Dual(At(winGold.rectTransform, .3f, .56f, 0, 0, w, h), .5f, .68f, 0, 0, w, h);
        winGold.type = Image.Type.Filled; winGold.fillMethod = Image.FillMethod.Vertical; winGold.fillOrigin = (int)Image.OriginVertical.Bottom;
        winGold.color = new Color(1f, .83f, .42f, .88f);
        winFx = PotCelebration.Create(winScreen, winOrbColor);
        if (winFx != null)
        {
            pot.enabled = false; winGold.enabled = false; winGlow.enabled = false;
            float fs = h * 1.25f; Dual(At(winFx.Rect, .3f, .56f, 0, 0, fs, fs), .5f, .68f, 0, 0, fs, fs);
        }
        winTitle = Legible(Label(winScreen, "Farmer's knowledge collected.", 48, Brown, TextAnchor.MiddleCenter)); Dual(At(winTitle.rectTransform, .3f, .12f, 0, 0, 900, 70), .5f, .5f, 0, 0, 1000, 70);
        dlgWin = BuildDlgBox(winScreen, .72f, .55f, 900, 380, false);
        Dual(dlgWin.rect, .5f, .3f, 0, 0, 860, 380);
        winReturn = MakeButton(winScreen, "Return to the village", Green, Cream, GreenDark, ReturnToAR, 42);
        Dual(At((RectTransform)winReturn.transform, .72f, .22f, 0, 0, 900, 100), .5f, .12f, 0, 0, 860, 100);
        CloseButton(winScreen);
    }

    void BuildConfirm()
    {
        var dim = NewImage("ConfirmClose", root, null); Fill(dim.rectTransform, -600, -600, -600, -600); dim.color = WithA(Dim, .6f);
        confirmModal = dim.gameObject;
        var card = Panel("Card", dim.transform, Cream, 40); At(card.rectTransform, .5f, .5f, 0, 0, 760, 380);
        var t = Label(card.transform, "Leave the farm?", 48, Brown, TextAnchor.MiddleLeft); At(t.rectTransform, .5f, 1, 0, -76, 660, 60);
        var b = Label(card.transform, "Your progress will be lost.", 32, Ink, TextAnchor.MiddleLeft); At(b.rectTransform, .5f, 1, 0, -146, 660, 50); b.fontStyle = FontStyle.Normal;
        var no = MakeButton(card.transform, "No", Color.clear, Brown, null, () => confirmModal.SetActive(false), 40, Tan); At((RectTransform)no.transform, .5f, 0, -170, 80, 300, 96);
        var yes = MakeButton(card.transform, "Yes", Red, Cream, null, CloseGame, 40); At((RectTransform)yes.transform, .5f, 0, 170, 80, 300, 96);
        confirmModal.SetActive(false);
    }

    void BuildGhost()
    {
        var g = Panel("DragGhost", root, Cream, 28); g.raycastTarget = false;
        ghost = At(g.rectTransform, .5f, .5f, 0, 0, 195, 215);
        ghost.localScale = Vector3.one * 1.1f;
        var sh = g.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(.24f, .12f, .04f, .35f); sh.effectDistance = new Vector2(0, -14);
        var cg = g.gameObject.AddComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
        ghostPic = Pic(g.transform, null); At(ghostPic.rectTransform, .5f, .5f, 0, 22, 170, 140);
        ghostLabel = Label(g.transform, "", 28, Ink, TextAnchor.MiddleCenter); At(ghostLabel.rectTransform, .5f, 0, 0, 32, 195, 40);
        g.gameObject.SetActive(false);
    }

    // Speech bubble: cream box, dark outline, brown name tab centred on the top edge,
    // and a trail of "thought" circles pointing at whoever is speaking.
    DlgBox BuildDlgBox(Transform parent, float ax, float ay, float w, float h, bool withTail)
    {
        var d = new DlgBox();
        if (withTail)
        {
            // Built before the box so the big circle tucks behind the box edge.
            d.tail = NewRect("Tail", parent);
            d.tail.sizeDelta = Vector2.zero;
            BubbleDot(d.tail, 0, 0, 78);
            BubbleDot(d.tail, -62, 58, 46);
            BubbleDot(d.tail, -100, 104, 28);
        }
        var box = UI.Panel("DialogueBox", parent, Cream, 42, BubbleLine, 5f); box.raycastTarget = true;
        At(box.rectTransform, ax, ay, 0, 0, w, h);
        var sh = box.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, .25f); sh.effectDistance = new Vector2(0, -12);
        var btn = box.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.onClick.AddListener(DlgTap);
        d.rect = box.rectTransform;
        d.speaker = NameTab(box.transform, "Farmer", out d.tab);
        d.body = Label(box.transform, "", 40, Ink, TextAnchor.UpperLeft);
        d.body.fontStyle = FontStyle.Normal; d.body.lineSpacing = 1.1f;
        Place(d.body.rectTransform, Vector2.zero, Vector2.one, new Vector2(64, 70), new Vector2(-64, -66));
        d.hint = Label(box.transform, "Tap to continue  >", 24, Hint, TextAnchor.LowerRight);
        Place(d.hint.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(64, 24), new Vector2(-44, 62));
        return d;
    }

    void BubbleDot(Transform parent, float x, float y, float size)
    {
        var outer = Circle(parent, BubbleLine); At(outer.rectTransform, .5f, .5f, x, y, size, size);
        var inner = Circle(outer.transform, Cream); Fill(inner.rectTransform, 5, 5, 5, 5);
    }

    // Brown name plate centred on the top edge of a box (like a label tab).
    Text NameTab(Transform box, string name, out Image tab)
    {
        tab = UI.Panel("NameTab", box, Brown, 33, BubbleLine, 4f); tab.raycastTarget = false;
        var r = tab.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(.5f, 1); r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(0, 0); r.sizeDelta = new Vector2(300, 66);
        UI.NameTabLine(tab.rectTransform, 33, 6f);
        var t = Label(tab.transform, name, 32, Cream, TextAnchor.MiddleCenter); Fill(t.rectTransform);
        return t;
    }

    void SetTabName(DlgBox box, string name)
    {
        box.speaker.text = name;
        box.tab.rectTransform.sizeDelta = new Vector2(Mathf.Max(220f, box.speaker.preferredWidth + 90f), 66);
    }

    void CloseButton(Transform parent)
    {
        var b = KButton(parent, "X", Red, Cream, new Color32(110, 30, 22, 255), AskClose, 39, new Color32(122, 36, 28, 255), 42);
        At((RectTransform)b.transform, 1, 1, -81, -75, 84, 84);
    }

    // Buttons built exactly like the weaving game's (same helper), scaled from its 1280 stage to this 1920 one.
    Button KButton(Transform parent, string label, Color bg, Color fg, Color shadow, UnityAction onClick, int size, Color border, float radius)
    {
        var b = UI.Btn(parent, label, bg, fg, shadow, onClick, size, font, border, radius);
        var bd = b.transform.Find("Border"); if (bd) bd.GetComponent<Image>().sprite = UI.Ring(4.5f, radius);
        var sh = b.GetComponent<Shadow>(); if (sh) sh.effectDistance = new Vector2(0, -7);
        return b;
    }

    // ---------------- orientation ----------------
    // Records the current (landscape) placement of r, plus where it goes in portrait.
    RectTransform Dual(RectTransform r, float ax, float ay, float x, float y, float w, float h)
    {
        dual.Add(new DualSpot
        {
            r = r,
            land = new Spot { anchor = r.anchorMin, pos = r.anchoredPosition, size = r.sizeDelta },
            port = new Spot { anchor = new Vector2(ax, ay), pos = new Vector2(x, y), size = new Vector2(w, h) }
        });
        return r;
    }

    void ApplyLayout(bool portrait)
    {
        isPortrait = portrait;
        foreach (var sc in scalers) if (sc != null) sc.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
        foreach (var d in dual)
        {
            if (d.r == null) continue;
            var p = portrait ? d.port : d.land;
            d.r.anchorMin = d.r.anchorMax = p.anchor;
            d.r.anchoredPosition = p.pos; d.r.sizeDelta = p.size;
        }
        LayoutDialogue();
    }

    // Only the speaker is on screen. Farmer: bottom-LEFT, bubble to his right.
    // Ananse: bottom-RIGHT, bubble to his left. Characters are cut off by the bottom edge (waist-up).
    void LayoutDialogue()
    {
        if (dlgMain == null) return;
        bool portrait = isPortrait ?? (Screen.height > Screen.width);
        float W = portrait ? 1080f : 1920f;
        float ch = portrait ? characterHeight * 1.05f : characterHeight;
        bool ananseOn = ananseSpeaking && ananseImage != null;
        PlaceCharacter(farmerImage, false, ch, W, portrait);
        PlaceCharacter(ananseImage, true, ch, W, portrait);
        if (farmerImage != null) { farmerImage.gameObject.SetActive(!ananseOn); farmerImage.color = ananseSpeaking ? new Color(.6f, .6f, .6f, 1f) : Color.white; }
        if (ananseImage != null) { ananseImage.gameObject.SetActive(ananseOn); ananseImage.color = Color.white; }

        var r = dlgMain.rect;
        var t = dlgMain.tail;
        if (portrait)
        {
            r.anchorMin = r.anchorMax = new Vector2(.5f, 0); r.pivot = new Vector2(.5f, 0);
            r.sizeDelta = new Vector2(880, 520); r.anchoredPosition = new Vector2(0, 60);
            // circles rise from the top edge towards the character's face
            t.anchorMin = t.anchorMax = new Vector2(.5f, 0);
            t.anchoredPosition = new Vector2(ananseOn ? 260 : -260, 60 + 520 + 10);
            t.localScale = new Vector3(ananseOn ? -1 : 1, 1, 1);
            t.localRotation = Quaternion.Euler(0, 0, ananseOn ? 30 : -30);
        }
        else
        {
            float bw = 1080f, bh = 520f, side = ananseOn ? 0 : 1, sx = ananseOn ? 70 : -70, y = 150;
            r.anchorMin = r.anchorMax = new Vector2(side, 0); r.pivot = new Vector2(side, 0);
            r.sizeDelta = new Vector2(bw, bh); r.anchoredPosition = new Vector2(sx, y);
            // circles sit on the character-side edge, trailing up towards the head
            float edgeX = ananseOn ? sx + bw : sx - bw;
            t.anchorMin = t.anchorMax = new Vector2(side, 0);
            t.anchoredPosition = new Vector2(edgeX + (ananseOn ? 6 : -6), y + bh * .62f);
            t.localScale = new Vector3(ananseOn ? -1 : 1, 1, 1);
            t.localRotation = Quaternion.identity;
        }
        t.SetSiblingIndex(r.GetSiblingIndex() - 1 < 0 ? 0 : r.GetSiblingIndex() - 1);
    }

    // Quick slide-up + fade when the speaker changes.
    void AnimateSpeaker()
    {
        float k = EaseOut((Now - speakerT) / 0.3f);
        var img = ananseSpeaking && ananseImage != null ? ananseImage : farmerImage;
        if (img == null) return;
        bool portrait = isPortrait ?? (Screen.height > Screen.width);
        bool right = img == ananseImage;
        float baseY = portrait ? 580 : 0;
        img.rectTransform.anchoredPosition = new Vector2(right ? -20 : 20, baseY - (1 - k) * 120f);
        var c = img.color; c.a = k; img.color = c;
        float bk = EaseOut((Now - speakerT - 0.08f) / 0.25f);
        dlgMain.rect.localScale = Vector3.one * (0.92f + 0.08f * bk);
        if (dlgMain.tail != null) dlgMain.tail.localScale = new Vector3((right ? -1f : 1f) * bk, bk, 1);
    }

    static void PlaceCharacter(Image img, bool right, float h, float W, bool portrait)
    {
        if (img == null || img.sprite == null) return;
        float w = h * img.sprite.rect.width / img.sprite.rect.height;
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(right ? 1 : 0, 0);
        r.pivot = new Vector2(right ? 1 : 0, 0);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = portrait ? new Vector2(right ? -20 : 20, 580) : new Vector2(right ? -20 : 20, 0);
        if (right) r.localScale = new Vector3(1, 1, 1);
    }

    // Keeps all game UI inside the phone's safe area (away from the notch / Dynamic Island / home bar).
    void ApplySafeArea()
    {
        if (root == null || canvasRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect sa = Screen.safeArea;
        lastSafe = sa;
        root.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        root.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        root.offsetMin = new Vector2(0, 0); root.offsetMax = new Vector2(0, 0);
        root.pivot = new Vector2(.5f, .5f);
    }

    void CheckOrientation()
    {
        if (Screen.safeArea != lastSafe) ApplySafeArea();
        bool p = Screen.height > Screen.width;
        if (isPortrait != p) ApplyLayout(p);
    }

    // ---------------- helpers ----------------
    static RectTransform NewRect(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform At(RectTransform r, float ax, float ay, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(ax, ay); r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static RectTransform Fill(RectTransform r, float l = 0, float b = 0, float rt = 0, float t = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t);
        return r;
    }

    static RectTransform Place(RectTransform r, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = offMin; r.offsetMax = offMax;
        return r;
    }

    static Color WithA(Color c, float a) { c.a = a; return c; }
    static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    static float EaseOut(float k) => 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);

    Image NewImage(string n, Transform parent, Sprite s)
    {
        var img = NewRect(n, parent).gameObject.AddComponent<Image>();
        img.sprite = s;
        return img;
    }

    Image Panel(string n, Transform parent, Color c, float radius)
    {
        var img = NewImage(n, parent, null);
        img.color = c;
        if (radius > 0) { img.sprite = roundS; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 64f / radius; }
        return img;
    }

    Image Ring(Transform parent, Color c, float radius)
    {
        var img = NewImage("Border", parent, ringS);
        img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 64f / radius; img.color = c; img.raycastTarget = false;
        Fill(img.rectTransform);
        return img;
    }

    Image Circle(Transform parent, Color c)
    {
        var img = NewImage("Circle", parent, circleS); img.color = c; img.raycastTarget = false;
        return img;
    }

    Image Pic(Transform parent, Sprite s)
    {
        var img = NewImage("Picture", parent, s); img.preserveAspect = true; img.raycastTarget = false; img.enabled = s != null;
        return img;
    }

    static void SetPic(Image img, Sprite s) { img.sprite = s; img.enabled = s != null; }

    Text Label(Transform parent, string s, int size, Color c, TextAnchor align)
    {
        var t = NewRect("Text", parent).gameObject.AddComponent<Text>();
        t.font = font; t.text = s; t.fontSize = size; t.color = c; t.alignment = align; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false; t.supportRichText = true;
        return t;
    }

    Button MakeButton(Transform parent, string label, Color bg, Color fg, Color? shadow, UnityAction onClick, int fontSize, Color? border = null)
    {
        var img = Panel("Button " + label, parent, bg, 28);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(onClick);
        if (shadow.HasValue) { var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = shadow.Value; sh.effectDistance = new Vector2(0, -7); }
        if (border.HasValue) Ring(img.transform, border.Value, 28);
        var t = Label(img.transform, label, fontSize, fg, TextAnchor.MiddleCenter); Fill(t.rectTransform);
        return b;
    }

    static void AddShadow(GameObject go) { var s = go.AddComponent<Shadow>(); s.effectColor = CardShadow; s.effectDistance = new Vector2(0, -6); }
    static void AddOutline(GameObject go) { var o = go.AddComponent<Outline>(); o.effectColor = BoxLine; o.effectDistance = new Vector2(3, -3); }

    // ---------------- art ----------------
    Sprite LoadSprite(string path)
    {
        if (spriteCache.TryGetValue(path, out var s) && s != null) return s;
        s = Resources.Load<Sprite>(path);
        Texture2D tex = null;
        if (s == null)
        {
            tex = Resources.Load<Texture2D>(path);
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f) : null;
        }
        if (s == null && !path.EndsWith("farmer") && !path.EndsWith("ananse")) Debug.LogWarning("Farmer game: missing image Resources/" + path + ".png");
        spriteCache[path] = s;
        return s;
    }

    Sprite CropSprite(string id) => LoadSprite("FarmerGame/crop_" + id);
    Sprite FoodSprite(string id) => LoadSprite("FarmerGame/food_" + id);
    string CropName(string id) { var c = crops.Find(x => x.id == id); return c != null ? c.displayName : id; }

    void MakeSprites()
    {
        roundS = RoundRect(160, 64f, 0f);
        ringS = RoundRect(160, 64f, 14f);
        circleS = MakeTex(128, (x, y) => Mathf.Clamp01(64f - Vector2.Distance(new Vector2(x, y), new Vector2(64, 64))), Vector4.zero);
        ringCircleS = MakeTex(128, (x, y) => Mathf.Clamp01(1f - Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) - 56f) / 5f), Vector4.zero);
        checkS = MakeTex(256, (x, y) =>
        {
            var p = new Vector2(x / 256f, y / 256f);
            float d = Mathf.Min(SegDist(p, new Vector2(.27f, .52f), new Vector2(.43f, .35f)), SegDist(p, new Vector2(.43f, .35f), new Vector2(.75f, .68f)));
            return Mathf.Clamp01((.065f - d) * 256f + .5f);
        }, Vector4.zero);
        softS = MakeTex(128, (x, y) => { float a = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) / 64f); return a * a; }, Vector4.zero);

        var bt = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color top = new Color32(169, 201, 133, 255), mid = new Color32(214, 224, 173, 255);
        for (int y = 0; y < 256; y++)
        {
            float v = 1f - y / 255f;
            bt.SetPixel(0, y, v < .45f ? Color.Lerp(top, mid, v / .45f) : Color.Lerp(mid, Cream, (v - .45f) / .55f));
        }
        bt.Apply();
        bgS = Sprite.Create(bt, new Rect(0, 0, 1, 256), new Vector2(.5f, .5f), 100f);
    }

    static float SdRound(float x, float y, float s, float r)
    {
        float h = s / 2f, qx = Mathf.Abs(x - h) - (h - r), qy = Mathf.Abs(y - h) - (h - r);
        float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }

    static Sprite RoundRect(int s, float r, float ring)
    {
        return MakeTex(s, (x, y) =>
        {
            float d = SdRound(x, y, s, r);
            float a = Mathf.Clamp01(0.5f - d);
            if (ring > 0) a *= Mathf.Clamp01(0.5f + d + ring);
            return a;
        }, new Vector4(r, r, r, r));
    }

    static Sprite MakeTex(int s, Func<float, float, float> alpha, Vector4 border)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x + .5f, y + .5f)) * 255f));
        t.SetPixels32(px); t.Apply(false, true);
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
}
