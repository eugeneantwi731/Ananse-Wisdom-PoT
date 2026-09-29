using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// "Anansi weaves kente" mini-game. Put this on an empty GameObject — it builds everything by itself:
// loading screen, dialogue, size picker, the 3D loom (from Resources/KenteGame/kente_strip_loom), threads, cloth, sewing and end screen.
// Open it from any button: drag the button into "Open Button", or call KenteWeavingGame.OpenGame().
public partial class KenteWeavingGame : MonoBehaviour
{
    public enum Mode { Auto, Tutorial, Free, Advanced }

    [Header("1. Opening the game")]
    public Button openButton;
    [Tooltip("Open the game when you press Play (handy for testing).")]
    public bool openOnStart = false;
    [Tooltip("Auto = tutorial the first time, then the Basic / Free weave / Advanced choice.")]
    public Mode mode = Mode.Auto;

    [Header("2. Art (optional overrides)")]
    [Tooltip("Leave empty to use Resources/KenteGame/kente_strip_loom")]
    public GameObject loomModel;
    public Font font;
    [Tooltip("The game detects which way up the cloth texture goes. Tick only if the woven rows still appear upside down.")]
    public bool flipClothVertically = false;
    [Range(0f, 1f)] public float cameraDim = 0.35f;
    [Tooltip("A see-through dark brown column behind the loom, top to bottom of the screen.")]
    public bool loomBackdrop = true;
    [Range(0f, 1f)] public float loomBackdropOpacity = 0.55f;

    [Header("3. Events")]
    [Tooltip("Fires when a kente is finished: (strips, rows, pot lives earned).")]
    public UnityEvent<int, int, int> onKenteFinished = new UnityEvent<int, int, int>();
    [Tooltip("Fires when the player taps 'Back to village'.")]
    public UnityEvent onBackToVillage = new UnityEvent();
    [Tooltip("Fires instead of opening when the Bag already holds 3 kente. Show Anansi's 'three kente already' thought here.")]
    public UnityEvent onBagFull = new UnityEvent();
    [Tooltip("Shown in portrait. The loom is laid out for landscape.")]
    public string rotateMessage = "Turn your phone sideways to use the loom";
    public UnityEvent onClosed = new UnityEvent();
    public UnityEvent<string> onImageSaved = new UnityEvent<string>();

    public const string TutorialKey = "KenteTutorialDone", BarKey = "KenteBar";
    public bool IsOpen => canvasGO != null && canvasGO.activeSelf;

    // ---------- constants (same layout numbers as the HTML prototype, 1280x720 stage) ----------
    const float SW = 1280, SH = 720, LX = 440, RX = 840, BASE = 560, ROW_H = 44, WIN_TOP = 128, TRAVEL = .84f;
    const int VISIBLE_ROWS = 8, TEX_W = 400, TEX_H = 432;
    static readonly Color Cream = new Color32(246, 235, 217, 255), Ink = new Color32(61, 36, 22, 255), Brown = new Color32(107, 62, 38, 255),
        Gold = new Color32(232, 163, 61, 255), GoldDark = new Color32(183, 122, 34, 255), Green = new Color32(79, 124, 58, 255), GreenDark = new Color32(52, 86, 31, 255),
        Red = new Color32(176, 58, 46, 255), CardBg = new Color32(239, 224, 198, 255), CardOn = new Color32(255, 246, 230, 255), Tan = new Color32(205, 176, 138, 255),
        Hint = new Color32(154, 116, 86, 255), Empty = new Color32(234, 220, 195, 255);

    enum St { Closed, Loading, Dialogue, Setup, Weave, Sew, End }
    St st = St.Closed;
    bool free, assetsReady;
    List<KenteColor> colors; Dictionary<string, KenteColor> byId;

    // ---------- UI ----------
    GameObject canvasGO; RectTransform safe, stage; Image scrim; Transform quitBtnT;
    RectTransform dialogueScr, setupScr, weaveScr, sewScr, endScr;
    Image dlgAnanse; Text dlgText, dlgHintTxt; GameObject skipBtn;
    Text setupTitle, setupSub, valN, valR, shapeLbl, infoLbl; RectTransform previewBox; readonly List<Image> previewCols = new List<Image>(); GameObject presetsRow;
    readonly List<Image> presetBgs = new List<Image>(); Image[] stepBtns = new Image[4];
    Text pillText, clothCaption, clothSub, hintText, nextName; Image nextSpool; GameObject nextBox, undoBtn; CanvasGroup undoGroup;
    RectTransform clothBox; readonly List<List<Image>> clothCells = new List<List<Image>>(); readonly List<Image> clothColOutline = new List<Image>();
    readonly List<Image> spoolCards = new List<Image>(); readonly List<RectTransform> spoolRects = new List<RectTransform>(); readonly List<GameObject> spoolRings = new List<GameObject>();
    RawImage loomView; RectTransform loomRect; Image ananseSmall;
    Image coachRing; RectTransform coachTip; Text coachStep, coachText; Image coachDot;
    Text snapText; Image snapA, snapB; RectTransform stripDoneBadge;
    Text sewTitle; RectTransform sewRow;
    Text endTitle, endBody, saveLbl, againLbl; RectTransform endIcon; GameObject toVillageBtn;
    LoadingScreen loading;

    // ---------- 3D loom ----------
    GameObject loomGO; Camera loomCam; RenderTexture rt;
    Transform shuttle, batten, heddles, treadles; Quaternion trR0 = Quaternion.identity; float hdFlip = -1f; Vector3 shP0, btP0, hdP0; float shCx0, shCy0, btCy0;
    Vector3 clothOrigin, clothRight, clothUp, clothNormal; float clothW, clothH, unitK = 1f;
    Material bobbinMat, weftMat; GameObject weft; Texture2D clothTex; Color32[] clothPx; string clothKey = "";
    Sprite ananseS, spoolFallback; readonly Dictionary<string, Sprite> spoolS = new Dictionary<string, Sprite>();

    // ---------- game state ----------
    string[] dlgLines; int dlgI; float dlgT0; bool dlgDone; Action dlgEnd;
    int pickN = 4, pickR = 8, freeStrips = 4, freeRowsN = 8;
    readonly List<List<string>> done = new List<List<string>>();
    List<string> freeRows = new List<string>();
    int strip, rows; bool sideLeft = true; float shx = LX;
    // Unity can turn an empty string field into "" (e.g. after a script reload in Play mode). "" must mean "no thread", never a colour key.
    [System.NonSerialized] string selRaw; string sel { get => string.IsNullOrEmpty(selRaw) ? null : selRaw; set => selRaw = value; }
    bool dragging; float dragOff; bool returning; float retT, retFrom;
    float beatT = -99, stripDoneT = -1, stripStartT, shakeT = -99, snapT = -99, actT, scroll, hintT; float snapA0, snapB0, snapY;
    string hint = ""; bool hintBad; readonly HashSet<string> seen = new HashSet<string>();
    float sewT, endT, savedT = -99; string saveMsg = "";
    float Now => Time.unscaledTime;

    // =====================================================================
    void Awake()
    {
        colors = KenteData.Colors(); byId = new Dictionary<string, KenteColor>(); foreach (var c in colors) byId[c.id] = c;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ananseS = LoadSprite("KenteGame/ananse");
        foreach (var c in colors) spoolS[c.id] = LoadSprite("KenteGame/spool_" + c.id);
        BuildUI();
        EnsureEventSystem();
    }

    void Start()
    {
        if (openButton != null) openButton.onClick.AddListener(OpenGame);
        bool inJourney = FindAnyObjectByType<JourneyPin>(FindObjectsInactive.Include) != null && !AnansiRuntime.TestMode;
        if (openOnStart && inJourney) Debug.Log("KenteWeavingGame: 'Open On Start' is ignored on the village map. The Weaver pin opens the game.");
        if (openOnStart && !inJourney) OpenGame();
    }

    public void OpenGame()
    {
        if (IsOpen) return;
        if (KenteBag.IsFull && !AnansiRuntime.TestMode && testJump == null && PlayerPrefs.GetInt(TutorialKey, 0) == 0)
        {
            if (onBagFull.GetPersistentEventCount() == 0) JourneyDialogue.Think("I already have three kente in my Gifts. Nana Nyame might like one.");
            onBagFull.Invoke(); return;
        }
        canvasGO.SetActive(true);
        if (!assetsReady) { StartCoroutine(Preload()); return; }
        StartFlow();
    }

    // How far the stage's bottom edge sits above the real screen bottom (stage units). Used to push character art down to the screen edge.
    float StageGapBelow()
    {
        if (stage == null) return 0f;
        var cv = canvasGO != null ? canvasGO.GetComponent<Canvas>() : null;
        Camera cam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
        var c = new Vector3[4]; stage.GetWorldCorners(c);
        float px = RectTransformUtility.WorldToScreenPoint(cam, c[0]).y;
        float scale = Mathf.Abs(RectTransformUtility.WorldToScreenPoint(cam, c[1]).y - px) / SH;
        return scale > 0f ? px / scale : 0f; // < 0 when the stage runs below the screen (wide screens)
    }
    // Anansi's feet sit ON the real screen bottom; he only bobs upward from there, and a small sink hides the edge during the bob.
    float AnanseBaseY(float h) => -(SH - h) - StageGapBelow() - 8f;

    // Tools > Anansi Game > Test a Section: open straight at one part of the weaving.
    string testJump; Mode testPrevMode;
    public void OpenForTest(string section)
    {
        if (IsOpen) CloseGame();
        testPrevMode = mode; testJump = section;
        mode = section == "dialogue" || section == "weave" || section == "last" || section == "sew" ? Mode.Tutorial : Mode.Free;
        OpenGame();
    }
    bool TestStart(string j)
    {
        var ts = KenteData.TutorialStrips();
        switch (j)
        {
            case "free": case "setup": ToSetup(); return true;
            case "free_weave": case "weave": ToSetup(); BeginWeaving(); return true;
            case "basic": free = adv = false; ToSetup(); return true;
            case "advanced": free = false; adv = true; ToSetup(); return true;
            case "adv_weave": free = false; adv = true; ToSetup(); BeginWeaving(); return true;
            case "gallery": ToSetup(); OpenGallery(); return true;
            case "last":
                ToSetup(); BeginWeaving();
                done.Add(new List<string>(ts[0])); done.Add(new List<string>(ts[1])); BuildClothPreview();
                BeginStrip(2); rows = RowsTarget - 1; sideLeft = rows % 2 == 0; shx = sideLeft ? LX : RX;
                SetHint("Test: one row left on the last strip."); return true;
            case "sew":
                ToSetup(); BeginWeaving();
                done.Add(new List<string>(ts[0])); done.Add(new List<string>(ts[1])); BuildClothPreview();
                BeginStrip(2); rows = RowsTarget; FinishStrip(); return true;
        }
        return false; // "dialogue": the normal start
    }

    public void CloseGame()
    {
        StopAllCoroutines(); dragging = false; aDrag = false; running = false; CloseGallery();
        st = St.Closed; canvasGO.SetActive(false);
        if (loomGO != null) loomGO.SetActive(false);
        onClosed.Invoke();
        JourneyPin.RefreshAll(); // Nana Nyame's pin appears after the tutorial kente
    }

    // ---------- loading ----------
    IEnumerator Preload()
    {
        st = St.Loading; ShowOnly(null);
        loading = LoadingScreen.Show(stage, "The Kente Loom", KenteData.LoadingHints(), font);
        if (quitBtnT != null) quitBtnT.SetAsLastSibling(); // close button stays on top of the loading screen
        loading.SetProgress(.1f, "Setting up the loom…");
        GameObject prefab = loomModel;
        if (prefab == null)
        {
            var req = Resources.LoadAsync<GameObject>("KenteGame/kente_strip_loom");
            while (!req.isDone) { loading.SetProgress(.1f + req.progress * .6f, "Setting up the loom…"); yield return null; }
            prefab = req.asset as GameObject;
        }
        loading.SetProgress(.75f, "Winding the threads…");
        yield return null;
        bool standIn = prefab == null;
        if (standIn) { Debug.LogWarning("Kente: the 3D loom (kente_strip_loom.glb) could not load, so a simple stand-in loom is used. Install glTFast: Tools > Anansi Game > Check Project."); prefab = KenteStandInLoom.Build(); }
        BuildLoom(prefab);
        if (standIn) Destroy(prefab);
        loading.SetProgress(.95f, "Almost ready…");
        yield return new WaitForSecondsRealtime(.3f);
        loading.SetReady(() => { loading.Hide(); loading = null; assetsReady = true; StartFlow(); }, "Loom ready");
    }

    void StartFlow()
    {
        bool tutDone = PlayerPrefs.GetInt(TutorialKey, 0) == 1;
        firstTime = mode == Mode.Tutorial || (mode == Mode.Auto && !tutDone); // only the first-ever kente gets Ananse's coaching
        adv = mode == Mode.Advanced; free = !adv && !firstTime;
        done.Clear(); seen.Clear(); A = null; editId = 0;
        if (testJump != null) { var j = testJump; testJump = null; mode = testPrevMode; if (TestStart(j)) return; }
        PlayDialogue(firstTime ? KenteData.Intro() : KenteData.FreeIntro(), ToSetup); // played before: Ananse talks first, then Basic / Free weave / Advanced
    }

    // ---------- dialogue ----------
    void PlayDialogue(string[] lines, Action onEnd)
    {
        st = St.Dialogue; ShowOnly(dialogueScr);
        dlgLines = lines; dlgI = 0; dlgT0 = Now; dlgDone = false; dlgEnd = onEnd;
        skipBtn.SetActive(!firstTime);
    }
    void DlgTap()
    {
        if (st != St.Dialogue || dlgDone) return;
        if (Typed(dlgT0) < dlgLines[dlgI].Length) { dlgT0 = -999; return; }
        if (dlgI < dlgLines.Length - 1) { dlgI++; dlgT0 = Now; return; }
        dlgDone = true; dlgEnd?.Invoke();
    }
    void SkipDlg() { if (st != St.Dialogue || dlgDone) return; dlgDone = true; dlgEnd?.Invoke(); }
    int Typed(float t0) => Mathf.FloorToInt((Now - t0) * 40f);

    // ---------- setup ----------
    void ToSetup()
    {
        st = St.Setup; ShowOnly(setupScr); editId = 0;
        if (adv) { pickN = advStripsN; pickR = advBlocksN; } else if (free) { pickN = freeStrips; pickR = freeRowsN; } else { pickN = 3; pickR = 6; }
        if (firstTime) KenteData.PatternIndex = 0; // the tutorial always weaves the first pattern
        SetupLayout(); RefreshSetup();
    }
    void Step(int dn, int dr)
    {
        if (!free && !adv) return;
        pickN = Mathf.Clamp(pickN + dn, 1, 12); pickR = Mathf.Clamp(pickR + dr, adv ? 2 : 4, 24); RefreshSetup();
    }
    struct Preset { public string label; public int n, r; public Preset(string l, int a, int b) { label = l; n = a; r = b; } }
    static readonly Preset[] Presets = { new Preset("Square", 4, 8), new Preset("Scarf", 2, 20), new Preset("Wide cloth", 10, 12), new Preset("Big square", 8, 16) };

    void BeginWeaving()
    {
        if (adv) { BeginAdv(); return; }
        if (free) { freeStrips = pickN; freeRowsN = pickR; }
        done.Clear(); BuildClothPreview(); BeginStrip(0);
    }

    int NumStrips => free ? freeStrips : 3;
    int RowsTarget => free ? freeRowsN : KenteData.TutorialStrips()[strip].Length;
    string[] Pattern => KenteData.TutorialStrips()[Mathf.Min(strip, 2)];
    string Need => free ? sel : (rows < Pattern.Length ? Pattern[rows] : null);
    List<string> Woven() { if (free) return freeRows; var l = new List<string>(); for (int i = 0; i < rows; i++) l.Add(Pattern[i]); return l; }

    // ---------- weaving ----------
    void BeginStrip(int i)
    {
        st = St.Weave; ShowOnly(weaveScr); if (loomGO != null) loomGO.SetActive(true);
        strip = i; rows = 0; sideLeft = true; shx = LX; if (i == 0) sel = null; pickedThisStrip = false; dragging = false; returning = false; stripDoneT = -1; freeRows = new List<string>();
        scroll = ScrollFor(0); running = false; stripStartT = Now; actT = Now;
        nextBox.SetActive(!free); undoBtn.SetActive(free); ApplyWeaveLayout();
        SetHint(free ? (i == 0 ? "Any colour I like. Pick a thread, then pull the shuttle." : $"Strip {i + 1} goes beside the last one. Watch the cloth grow on the left.")
                     : (i == 0 ? FirstHint() : $"Strip {i + 1} sits beside the last one. The pattern is on the left."));
        UpdateSpools();
    }
    string FirstHint() { var c = byId[Need]; seen.Add(c.id); return $"{c.meaning} I need the {c.name} thread."; }
    void SetHint(string s, bool bad = false) { hint = s; hintBad = bad; hintT = Now; }

    void Pick(string id)
    {
        if (st != St.Weave || dragging || stripDoneT >= 0) return;
        sel = id; actT = Now; pickedThisStrip = true; UpdateSpools();
        var C = byId[id];
        if (adv) { SetHint(seen.Add(id) ? $"{C.meaning} Swipe across the strips you want." : $"{C.name}. Swipe across the strips you want."); return; }
        if (free)
        {
            if (!seen.Contains(id)) { seen.Add(id); SetHint($"{C.meaning} Pull the shuttle."); }
            else SetHint($"{C.name}. Pull the shuttle {(sideLeft ? "to the right" : "to the left")}.");
            return;
        }
        if (id == Need) SetHint($"Good. Now pull the shuttle {(sideLeft ? "to the right" : "to the left")}.");
    }

    public void LoomPointerDown(Vector2 p)
    {
        if (adv && A != null) { if (st == St.Weave && loomCam != null) AdvPointerDown(p); return; }
        if (st != St.Weave || stripDoneT >= 0 || returning || running || loomCam == null) return;
        Vector2 sp = ToPanel(ShuttleWorld(shx, RowCenterY()));
        if (Mathf.Abs(p.x - sp.x) > 90 || Mathf.Abs(p.y - sp.y) > 60) return;
        if (sel == null) { shakeT = Now; SetHint("First a thread. Tap a spool on the right.", true); return; }
        if (!free && Need != null && sel != Need) { shakeT = Now; SetHint($"This row needs {byId[Need].name}. Tap the {byId[Need].name} thread first.", true); return; } // bobbin still holds the last strip's colour
        dragging = true; actT = Now; dragOff = PanelToLoomX(p.x) - shx; classicDownT = Now; classicX0 = shx;
    }
    public void LoomPointerDrag(Vector2 p)
    {
        if (aDrag) { AdvPointerDrag(p); return; }
        if (!dragging) return;
        float start = sideLeft ? LX : RX;
        shx = Mathf.Clamp(PanelToLoomX(p.x) - dragOff, LX, RX);
        float prog = Mathf.Abs(shx - start) / (RX - LX);
        if (!free && sel != Need && prog >= .55f)
        {
            snapA0 = start; snapB0 = shx; snapY = RowCenterY(); snapT = Now; shakeT = Now;
            dragging = false; shx = start;
            SetHint($"Snap! Wrong thread. The pattern asks for {byId[Need].name}.", true);
        }
    }
    public void LoomPointerUp()
    {
        if (aDrag) { AdvPointerUp(); return; }
        if (!dragging) return;
        dragging = false;
        float end = sideLeft ? RX : LX, start = sideLeft ? LX : RX;
        if (Now - classicDownT < .28f && Mathf.Abs(shx - classicX0) < 14) { RunShuttle(start, end, CompleteRow); return; } // a tap sends the shuttle across by itself
        if (Mathf.Abs(shx - end) <= 24) CompleteRow();
        else { returning = true; retT = Now; retFrom = shx; }
    }

    void CompleteRow()
    {
        if (free) freeRows.Add(sel);
        rows++; sideLeft = !sideLeft; shx = sideLeft ? LX : RX; beatT = Now; actT = Now;
        if (rows >= RowsTarget) { FinishStrip(); return; }
        if (free) SetHint($"Row {rows} done. Same colour, or something new?");
        else
        {
            var c = byId[Need];
            if (!seen.Contains(c.id)) { seen.Add(c.id); SetHint($"{c.meaning} I need the {c.name} thread."); }
            else if (sel == c.id) SetHint($"{c.name} again. Pull the shuttle {(sideLeft ? "to the right" : "to the left")}.");
            else SetHint($"Next thread: {c.name}.");
        }
    }

    void UndoRow()
    {
        if (!free || dragging || stripDoneT >= 0) return;
        if (rows == 0)
        {
            if (strip == 0 || done.Count == 0) return;
            strip--; freeRows = done[done.Count - 1]; done.RemoveAt(done.Count - 1); rows = freeRows.Count;
            SetHint($"Back to strip {strip + 1}. Last row unpicked.");
        }
        else SetHint("Unpicked. Choose again.");
        freeRows.RemoveAt(freeRows.Count - 1); rows--; sideLeft = rows % 2 == 0; shx = sideLeft ? LX : RX;
    }

    void FinishStrip()
    {
        stripDoneT = Now; SetHint("Beautiful. Tight and even.");
        StartCoroutine(AfterStrip());
    }
    IEnumerator AfterStrip()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        done.Add(new List<string>(Woven()));
        if (strip + 1 < NumStrips) { BeginStrip(strip + 1); yield break; }
        if (loomGO != null) loomGO.SetActive(false);
        st = St.Sew; ShowOnly(sewScr); sewT = Now; BuildSewRow();
        yield return new WaitForSecondsRealtime(2.8f);
        PlayDialogue(free ? KenteData.FreeOutro() : KenteData.Outro(), ToEnd);
    }

    void ToEnd()
    {
        st = St.End; ShowOnly(endScr); endT = Now;
        int n = done.Count, r = 0; foreach (var s in done) r = Mathf.Max(r, s.Count);
        int lives = KenteData.LifeReward(n, r);
        if (firstTime) PlayerPrefs.SetInt(TutorialKey, 1);
        bool stored = KenteBag.Add(n, r, lives);
        onKenteFinished.Invoke(n, r, lives);
        GalleryAddCurrent(); // every finished cloth also waits in My Kente
        endTitle.text = firstTime ? "Kente complete" : "Your kente";
        endBody.text = !stored ? "Your Gifts already hold three kente, so this one waits in My Kente."
            : firstTime ? "Added to your Gifts. In the village, drag it to Nana Nyame to offer your gift."
            : KenteBag.IsFull ? "Added to your Gifts. You now have three kente." : "Added to your Gifts. A copy waits in My Kente.";
        againLbl.text = firstTime ? "Weave freely" : "Weave another";
        BuildEndIcon(); EndExtras();
    }
    void WeaveFree()
    {
        done.Clear(); A = null;
        if (firstTime) { firstTime = false; free = true; adv = false; PlayDialogue(KenteData.FreeIntro(), ToSetup); return; }
        ToSetup(); // "Weave another" in the same mode
    }
    void BackToVillage() { onBackToVillage.Invoke(); CloseGame(); }

    // ---------- snapshot ----------
    void SaveImage() => SaveClothPng(adv && A != null ? A.strips : Expand(done));
    static void Put(Color32[] px, int W, int H, int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }
    static Color32 Mul(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
    static Color32 Lighten(Color32 c, float k) => new Color32((byte)(c.r + (255 - c.r) * k), (byte)(c.g + (255 - c.g) * k), (byte)(c.b + (255 - c.b) * k), 255);

    // =====================================================================
    // 3D loom
    void BuildLoom(GameObject prefab)
    {
        loomGO = Instantiate(prefab); loomGO.name = "KenteLoom3D";
        loomGO.transform.position = new Vector3(5000, 0, 0);
        var toon = Shader.Find("KenteGame/Toon"); var clothSh = Shader.Find("KenteGame/Cloth");
        if (toon == null || clothSh == null) Debug.LogError("Kente: shaders missing (Resources/KenteGame/Shaders).");
        clothTex = new Texture2D(TEX_W, TEX_H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        clothPx = new Color32[TEX_W * TEX_H];
        Renderer clothR = null;
        foreach (var r in loomGO.GetComponentsInChildren<Renderer>(true))
        {
            if (IsFold(r.transform)) continue; // fold_bottom / fold_top are placement guides for the wound cloth
            var mats = r.sharedMaterials; var nm = new Material[mats.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var src = mats[i]; string mname = src != null ? src.name.ToLower() : "";
                if (r.name.ToLower().Contains("cloth") || mname.Contains("cloth")) { nm[i] = new Material(clothSh) { mainTexture = clothTex }; clothR = r; continue; }
                var m = new Material(toon);
                m.SetColor("_Color", SrcColor(src)); var tx = SrcTex(src); if (tx != null) m.SetTexture("_MainTex", tx);
                m.SetFloat("_Outline", mname.Contains("warp") ? 0f : .028f);
                if (mname.Contains("bobbin")) bobbinMat = m;
                nm[i] = m;
            }
            r.sharedMaterials = nm;
        }
        shuttle = Find(loomGO.transform, "shuttle"); batten = Find(loomGO.transform, "batten");
        heddles = Find(loomGO.transform, "heddles"); treadles = Find(loomGO.transform, "treadles");
        if (clothR == null) { var ct = Find(loomGO.transform, "cloth"); if (ct != null) clothR = ct.GetComponentInChildren<Renderer>(); }
        if (clothR == null) { Debug.LogError("Kente: no 'cloth' object in the loom model."); return; }

        // cloth frame
        var cb = clothR.bounds; Vector3 e = cb.extents;
        Bounds all = cb; foreach (var r in loomGO.GetComponentsInChildren<Renderer>()) all.Encapsulate(r.bounds);
        var mf = clothR.GetComponent<MeshFilter>();
        bool readable = mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable && mf.sharedMesh.normals.Length > 0;
        clothNormal = readable ? clothR.transform.TransformDirection(mf.sharedMesh.normals[0]) : (e.z < e.x ? Vector3.forward : Vector3.right);
        clothNormal.y = 0; if (clothNormal.sqrMagnitude < .01f) clothNormal = Vector3.forward; clothNormal.Normalize();
        if (Vector3.Dot(clothNormal, cb.center - all.center) < 0) clothNormal = -clothNormal; // point toward the viewer side
        clothUp = Vector3.up; clothRight = Vector3.Cross(clothUp, -clothNormal).normalized; // camera's right when looking at the cloth
        clothW = 2 * (Mathf.Abs(e.x * clothRight.x) + Mathf.Abs(e.y * clothRight.y) + Mathf.Abs(e.z * clothRight.z));
        // stage px -> world: the model's y = 0 is the fell line (BASE). The loom with fold objects has a longer cloth mesh, so map by model units.
        hasFolds = Find(loomGO.transform, "fold_bottom") != null || Find(loomGO.transform, "fold_top") != null;
        float y0 = hasFolds ? loomGO.transform.position.y : cb.min.y;
        stageU = hasFolds ? 4.3f / (BASE - WIN_TOP) * loomGO.transform.lossyScale.y : 2 * e.y / (BASE - WIN_TOP);
        clothH = (BASE - WIN_TOP) * stageU; unitK = clothH / 4.3f;
        clothOrigin = cb.center - clothRight * clothW / 2; clothOrigin.y = y0;
        texBot = BASE - (cb.min.y - y0) / stageU; texH = Mathf.Clamp(Mathf.RoundToInt((cb.max.y - cb.min.y) / stageU), 16, 2048);
        if (texH != TEX_H) { clothTex.Reinitialize(TEX_W, texH); clothPx = new Color32[TEX_W * texH]; }
        // which way up the cloth texture goes: read the mesh if we can, otherwise the fold model is known to be flipped
        bool autoFlip = hasFolds;
        if (readable) { var msh = mf.sharedMesh; var vs = msh.vertices; var uvs = msh.uv; if (uvs.Length == vs.Length && vs.Length > 0) { int hi = 0; for (int i = 1; i < vs.Length; i++) if (vs[i].y > vs[hi].y) hi = i; autoFlip = uvs[hi].y < .5f; } }
        flipCloth = autoFlip ^ flipClothVertically;

        // camera: straight-on, framing the whole loom
        var camGO = new GameObject("KenteLoomCamera"); camGO.transform.SetParent(loomGO.transform.parent, false);
        loomCam = camGO.AddComponent<Camera>(); loomCam.fieldOfView = 24; loomCam.nearClipPlane = .05f; loomCam.farClipPlane = 200;
        loomCam.clearFlags = CameraClearFlags.SolidColor; loomCam.backgroundColor = new Color(0, 0, 0, 0); loomCam.allowHDR = false; loomCam.allowMSAA = true;
        rt = new RenderTexture(1200, 1160, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        loomCam.targetTexture = rt; loomView.texture = rt;
        float halfFov = loomCam.fieldOfView * .5f * Mathf.Deg2Rad, aspect = 600f / 580f;
        float distH = (all.size.y * .5f) / Mathf.Tan(halfFov), distW = (clothW * 1.5f * .5f) / (Mathf.Tan(halfFov) * aspect);
        float dist = Mathf.Max(distH, distW) * 1.04f + all.extents.magnitude * .2f;
        Vector3 look = new Vector3(cb.center.x, all.center.y, cb.center.z);
        camGO.transform.position = look + clothNormal * dist; camGO.transform.LookAt(look, Vector3.up);
        Shader.SetGlobalVector("_KenteLightDir", (clothNormal * 1.0f + Vector3.up * .7f - clothRight * .4f).normalized);

        // moving parts: remember their start positions in cloth coordinates
        if (shuttle != null) { var c = BoundsOf(shuttle).center; shP0 = shuttle.position; shCx0 = Vector3.Dot(c - clothOrigin, clothRight); shCy0 = Vector3.Dot(c - clothOrigin, clothUp); }
        if (batten != null) { var c = BoundsOf(batten).center; btP0 = batten.position; btCy0 = Vector3.Dot(c - clothOrigin, clothUp); }
        if (heddles != null) hdP0 = heddles.position;
        if (treadles != null) trR0 = treadles.localRotation;
        BuildRollers();
        weft = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(weft.GetComponent<Collider>()); weft.name = "Weft";
        weft.transform.SetParent(loomGO.transform, true);
        weftMat = new Material(toon); weftMat.SetFloat("_Outline", 0f); weft.GetComponent<Renderer>().sharedMaterial = weftMat; weft.SetActive(false);
        loomGO.SetActive(false);
    }
    static Transform Find(Transform t, string n)
    {
        if (t.name.Equals(n, StringComparison.OrdinalIgnoreCase)) return t;
        foreach (Transform c in t) { var f = Find(c, n); if (f != null) return f; }
        return null;
    }
    static Bounds BoundsOf(Transform t) { var rs = t.GetComponentsInChildren<Renderer>(); var b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero); foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
    static Color SrcColor(Material m)
    {
        if (m == null) return Color.white;
        foreach (var p in new[] { "_BaseColor", "baseColorFactor", "_Color" }) if (m.HasProperty(p)) return m.GetColor(p);
        return Color.white;
    }
    static Texture SrcTex(Material m)
    {
        if (m == null) return null;
        foreach (var p in new[] { "_BaseMap", "baseColorTexture", "_MainTex" }) if (m.HasProperty(p) && m.GetTexture(p) != null) return m.GetTexture(p);
        return null;
    }

    // loom-space (prototype px) -> cloth coordinates -> world
    float CX(float lx) => (lx - LX) / (RX - LX) * clothW;
    float CY(float ly) => (BASE - ly) / (BASE - WIN_TOP) * clothH;
    float ShuttleCX(float lx) => clothW / 2 + (CX(lx) - clothW / 2) * TRAVEL;
    float ActiveTop => BASE - (rows + 1) * ROW_H + scroll;
    float RowCenterY() => ActiveTop + ROW_H / 2;
    Vector3 ClothWorld(float cx, float cy, float lift) => clothOrigin + clothRight * cx + clothUp * cy + clothNormal * lift;
    Vector3 ShuttleWorld(float lx, float ly) => ClothWorld(ShuttleCX(lx), CY(ly), .3f * unitK);
    Vector2 ToPanel(Vector3 w) { var v = loomCam.WorldToViewportPoint(w); return new Vector2(v.x * 600f, v.y * 580f); }
    float PanelToLoomX(float px) => PanelToLoomXAt(px, RowCenterY());
    float PanelToLoomXAt(float px, float y)
    {
        float l = ToPanel(ShuttleWorld(LX, y)).x, r = ToPanel(ShuttleWorld(RX, y)).x;
        return LX + (px - l) / Mathf.Max(1f, r - l) * (RX - LX);
    }

    void UpdateLoom(float t)
    {
        if (loomGO == null || !loomGO.activeSelf) return;
        float rowC, top, weftFrom; int flipN; bool showWeft, canPull;
        if (adv && A != null)
        {
            DrawClothAdv(t); rowC = advShY; top = advShY - ROW_H / 2; flipN = A.passes;
            showWeft = sel != null && ((aDrag && !aLift && !aAuto && aMoved) || running); weftFrom = running ? runFrom : AdvEdge(aE0);
            canPull = sel != null && !aDrag && !running && !aRet;
        }
        else
        {
            var woven = Woven(); DrawCloth(woven); rowC = RowCenterY(); top = ActiveTop; flipN = woven.Count;
            showWeft = (dragging || running) && sel != null; weftFrom = sideLeft ? LX : RX;
            canPull = sel != null && sel == Need && !dragging && !running && stripDoneT < 0 && !returning;
        }
        float be = t - beatT, beat = be < .24f ? Mathf.Sin(be / .24f * Mathf.PI) * (adv ? 14f : 26f) : 0f;
        float rowY = CY(rowC);
        float se = t - shakeT, shake = se < .3f ? Mathf.Sin(se * 100f) * 8f * (1 - se / .3f) : 0f;
        if (shuttle != null) shuttle.position = shP0 + clothRight * (ShuttleCX(shx) + CX(LX + shake) - CX(LX) - shCx0) + clothUp * (rowY - shCy0);
        if (batten != null) batten.position = btP0 + clothUp * (CY(top) + .18f * unitK - beat / (BASE - WIN_TOP) * clothH - btCy0);
        float flip = flipN % 2 == 1 ? 1 : -1;
        hdFlip = Mathf.MoveTowards(hdFlip, flip, Time.unscaledDeltaTime * 8f); // heddles glide to the new shed instead of jumping
        if (heddles != null) heddles.position = hdP0 + clothUp * .05f * unitK * hdFlip;
        if (treadles != null) treadles.localRotation = trR0 * Quaternion.Euler((be < .24f ? 4.6f * Mathf.Sin(be / .24f * Mathf.PI) : 0f) * flip, 0, 0); // foot press on each beat
        if (bobbinMat != null)
        {
            Color bc = sel != null ? byId[sel].color : new Color32(216, 199, 173, 255);
            bobbinMat.SetColor("_Color", bc);
            bobbinMat.SetColor("_Emission", bc * (canPull ? (.5f + .5f * Mathf.Sin(t / .18f)) * .35f : 0f));
        }
        weft.SetActive(showWeft);
        if (showWeft)
        {
            float a = CX(weftFrom), b = ShuttleCX(shx);
            weft.transform.position = ClothWorld((a + b) / 2, rowY, .1f * unitK);
            weft.transform.rotation = Quaternion.LookRotation(clothNormal, clothUp);
            weft.transform.localScale = Vector3.one; // reset before sizing in world units
            var ls = loomGO.transform.lossyScale;
            weft.transform.localScale = new Vector3(Mathf.Max(.001f, Mathf.Abs(b - a)) / ls.x, (adv ? .05f : .09f) * unitK / ls.y, .06f * unitK / ls.z);
            weftMat.SetColor("_Color", byId[sel].color);
        }
        UpdateRollers();
    }

    void DrawCloth(List<string> woven)
    {
        string key = string.Join(",", woven) + "|" + Mathf.RoundToInt(scroll);
        if (key == clothKey) return; clothKey = key;
        var clear = new Color32(0, 0, 0, 0); var warpA = new Color32(246, 236, 217, 255); var warpB = new Color32(160, 130, 90, 230);
        for (int y = 0; y < texH; y++) for (int x = 0; x < TEX_W; x++) { int m = (x - 3) % 11; clothPx[y * TEX_W + x] = m >= 0 && m < 4 ? warpA : m == 4 ? warpB : clear; }
        for (int i = 0; i < woven.Count; i++)
        {
            float top = BASE - (i + 1) * ROW_H + scroll; // loom-space
            int y0 = Mathf.RoundToInt(texBot - (top + ROW_H)), y1 = Mathf.RoundToInt(texBot - top); // texture y (up) of row bottom/top
            Color32 c = byId[woven[i]].color, light = Lighten(c, .22f), dark = Mul(c, .8f), line = Mul(c, .86f);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(texH, y1); y++)
                for (int x = 0; x < TEX_W; x++)
                {
                    int m = x % 11, ry = y - y0; Color32 col = c;
                    if (m < 2) col = light; else if (m >= 5 && m < 7) col = dark;
                    if (ry % 7 == 0) col = line; if (ry < 2) col = Mul(c, .8f);
                    clothPx[TexRow(y) * TEX_W + x] = col;
                }
        }
        clothTex.SetPixels32(clothPx); clothTex.Apply(false);
    }

    // =====================================================================
    void Update()
    {
        if (!IsOpen) return;
        float t = Now;
        FitStage();
        UI.Alpha(scrim, cameraDim);
        if (st == St.Dialogue) UpdateDialogue(t);
        if (st == St.Weave) UpdateWeave(t);
        if (st == St.Setup) UpdateSetupTabs(t);
        if (GalleryIsOpen) UpdateGallery(t);
        if (st == St.Sew) UpdateSew(t);
        if (st == St.End) { float k = Mathf.Clamp01((t - endT) / .4f); endIcon.localScale = Vector3.one * (k < 1 ? .4f + .75f * UI.EaseOut(k) - .15f * Mathf.Sin(k * Mathf.PI) : 1f); saveLbl.text = t - savedT < 3f ? saveMsg : "Save as image"; }
    }

    GameObject rotateGO;
    void BuildRotatePrompt(RectTransform root)
    {
        rotateGO = new GameObject("RotatePrompt", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)rotateGO.transform; rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        rotateGO.GetComponent<Image>().color = new Color32(28, 14, 6, 235);
        var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var tr = (RectTransform)tgo.transform; tr.SetParent(rt, false);
        tr.anchorMin = new Vector2(.1f, .3f); tr.anchorMax = new Vector2(.9f, .7f); tr.offsetMin = tr.offsetMax = Vector2.zero;
        var tx = tgo.GetComponent<Text>(); tx.text = "\u21BB\n" + rotateMessage; tx.alignment = TextAnchor.MiddleCenter;
        tx.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 20; tx.resizeTextMaxSize = 64; tx.color = Cream;
        rotateGO.SetActive(false);
    }

    void FitStage()
    {
        var sa = Screen.safeArea;
        safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        var r = safe.rect; float s = Mathf.Min(r.width / SW, r.height / SH) * .98f;
        stage.localScale = new Vector3(s, s, 1);
        bool portrait = Screen.height > Screen.width;
        if (rotateGO != null && rotateGO.activeSelf != portrait) { rotateGO.SetActive(portrait); stage.gameObject.SetActive(!portrait); }
    }

    void UpdateDialogue(float t)
    {
        string line = dlgLines[dlgI]; int n = Mathf.Min(Typed(dlgT0), line.Length);
        dlgText.text = line.Substring(0, n);
        UI.Alpha(dlgHintTxt, !dlgDone && n >= line.Length ? 1 : 0);
        float ci = UI.EaseOut((t - dlgT0) / .32f);
        dlgAnanse.rectTransform.anchoredPosition = new Vector2(dlgAnanse.rectTransform.anchoredPosition.x, AnanseBaseY(dlgAnanse.rectTransform.sizeDelta.y));
    }

    void UpdateWeave(float t)
    {
        UpdateRun(t);
        if (adv && A != null) { UpdateWeaveAdv(t); return; }
        if (returning) { float k = UI.EaseOut((t - retT) / .18f); shx = Mathf.Lerp(retFrom, sideLeft ? LX : RX, k); if (k >= 1) returning = false; }
        float target = ScrollFor(rows); scroll += (target - scroll) * .18f; if (Mathf.Abs(target - scroll) < .5f) scroll = target;
        int tgt = RowsTarget;
        pillText.text = $"Strip {strip + 1} of {NumStrips}   <color=#4F7C3A>Row {Mathf.Min(rows + 1, tgt)} of {tgt}</color>";
        clothCaption.text = $"Strip {strip + 1} of {NumStrips}";
        clothSub.text = strip > 0 ? "Joins to the right of the last strip" : "Weave from the bottom up";
        hintText.text = hint; hintText.color = hintBad ? Red : Ink;
        if (!free && Need != null) { nextName.text = byId[Need].name; nextSpool.sprite = spoolS[Need]; nextSpool.enabled = nextSpool.sprite != null; }
        undoGroup.alpha = (rows > 0 || strip > 0) && stripDoneT < 0 ? 1 : .4f;
        UpdateClothPreview(t);
        float se = t - shakeT; weaveScr.anchoredPosition = new Vector2(se < .3f ? Mathf.Sin(se * 90f) * 8f * (1 - se / .3f) : 0, 0);
        float sde = stripDoneT >= 0 ? t - stripDoneT : -1; stripDoneBadge.gameObject.SetActive(sde >= 0); if (sde >= 0) stripDoneBadge.localScale = Vector3.one * (.6f + .4f * UI.EaseOut(sde / .3f));
        float ae = t - hintT; ananseSmall.rectTransform.anchoredPosition = new Vector2(150, AnanseBaseY(ananseSmall.rectTransform.sizeDelta.y) + (ae < .5f ? 7f * Mathf.Abs(Mathf.Sin(ae / .5f * Mathf.PI * 2)) : 0));
        UpdateLoom(t);
        UpdateSnap(t);
        UpdateCoach(t);
    }

    void UpdateSnap(float t)
    {
        float e = t - snapT; bool on = e < .7f && loomCam != null;
        snapText.gameObject.SetActive(on); snapA.gameObject.SetActive(on && e < .65f); snapB.gameObject.SetActive(on && e < .65f);
        if (!on) return;
        float k = e / .65f, drop = 140f * k * k;
        Vector2 a = LoomToStage(Mathf.Min(snapA0, snapB0), snapY), b = LoomToStage(Mathf.Max(snapA0, snapB0), snapY), mid = (a + b) / 2;
        Place(snapA.rectTransform, a.x, a.y - 6 - drop, mid.x - a.x, 12, 40 * k); Place(snapB.rectTransform, mid.x, mid.y - 6 - drop, b.x - mid.x, 12, -40 * k);
        UI.Alpha(snapA, 1 - k); UI.Alpha(snapB, 1 - k);
        var c = byId.ContainsKey(sel ?? "") ? byId[sel].color : Red; snapA.color = UI.WithA(c, 1 - k); snapB.color = UI.WithA(c, 1 - k);
        UI.TL(snapText.rectTransform, mid.x - 80, mid.y - 60, 160, 50);
    }
    void Place(RectTransform r, float x, float y, float w, float h, float rot) { UI.TL(r, x, y, Mathf.Max(1, w), h); r.localRotation = Quaternion.Euler(0, 0, -rot); }
    Vector2 LoomToStage(float lx, float ly)
    {
        var p = ToPanel(ShuttleWorld(lx, ly)); var r = RectIn(loomRect, weaveScr);
        return new Vector2(r.x + p.x * r.width / 600f, r.y + (580f - p.y) * r.height / 580f);
    }
    // Where a UI element really is, in weaveScr top-left coordinates (follows lifts, scaling and layout changes).
    static Rect RectIn(RectTransform target, RectTransform space)
    {
        var c = new Vector3[4]; target.GetWorldCorners(c);
        Vector2 a = space.InverseTransformPoint(c[0]), b = space.InverseTransformPoint(c[2]);
        var sr = space.rect;
        return new Rect(Mathf.Min(a.x, b.x) - sr.xMin, sr.yMax - Mathf.Max(a.y, b.y), Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
    }
    Rect Frame(Image ring, RectTransform target, float pad)
    {
        var r = RectIn(target, weaveScr);
        UI.TL(ring.rectTransform, r.x - pad, r.y - pad, r.width + pad * 2, r.height + pad * 2);
        return r;
    }

    void UpdateCoach(float t)
    {
        bool show = false, ring = false, dot = false, box = false;
        if (firstTime && !free && !adv && stripDoneT < 0 && !dragging && !returning && !running && loomCam != null)
        {
            bool idle = t - actT > 5f, early = strip == 0 && rows < 3;
            if (strip > 0 && rows == 0 && t - stripStartT < 4.5f && !pickedThisStrip)
            { show = box = true; var cr = Frame(coachBox, clothBox, 5f + 1.5f * Mathf.Sin(t / .18f)); Tip(cr.xMax + 22, cr.y - 30, "STRIP " + (strip + 1), "This strip is sewn right beside the last one. Watch the cloth grow here."); }
            else if ((early || idle) && Need != null)
            {
                if (sel != Need)
                {
                    int i = colors.FindIndex(c => c.id == Need); if (i < 0 || i >= spoolRects.Count) i = 0;
                    var card = RectIn(spoolRects[i], weaveScr); float x = card.x, y = card.y;
                    show = ring = dot = true;
                    Tip(x - 290, y + 30, "STEP 1 · PICK", rows == 0 && strip == 0 ? $"Tap the {byId[Need].name} thread to load the shuttle." : $"The lit row on the pattern shows the next colour. Tap {byId[Need].name}.");
                    Frame(coachRing, spoolRects[i], 7 + 2 * Mathf.Sin(t / .18f));
                    float r = (t % .9f) / .9f, d = 44 + 20 * r; UI.TL(coachDot.rectTransform, card.center.x - d / 2, card.center.y - d / 2, d, d); UI.Alpha(coachDot, 1 - .5f * r);
                }
                else
                {
                    Vector2 cp = LoomToStage(640, ActiveTop);
                    show = dot = true;
                    Tip(cp.x - 130, Mathf.Max(60, cp.y - 132), "STEP 2 · WEAVE", $"Drag the shuttle all the way {(sideLeft ? "to the right" : "to the left")}.");
                    float k = (t % 1.6f) / 1.6f, e = UI.EaseOut(Mathf.Min(1, k / .8f));
                    Vector2 p = LoomToStage(Mathf.Lerp(sideLeft ? LX : RX, sideLeft ? RX : LX, e), RowCenterY());
                    UI.TL(coachDot.rectTransform, p.x - 22, p.y - 22, 44, 44); UI.Alpha(coachDot, k > .85f ? 0 : 1);
                }
            }
        }
        coachTip.gameObject.SetActive(show); coachRing.gameObject.SetActive(ring); coachDot.gameObject.SetActive(dot); coachBox.gameObject.SetActive(box);
        if (show) coachTip.anchoredPosition += new Vector2(0, Mathf.Round(3 * Mathf.Sin(t / .3f)));
    }
    // Tip card grows to fit its text (it used to be a fixed 110px box, so long tips spilled out of it) and sits on whole pixels.
    void Tip(float x, float y, string step, string text)
    {
        coachStep.text = step; coachText.text = text;
        const float W = 270, pad = 18, textW = W - pad * 2;
        var gs = coachText.GetGenerationSettings(new Vector2(textW, 0)); gs.scaleFactor = 1f;
        float th = coachText.cachedTextGeneratorForLayout.GetPreferredHeight(text, gs);
        float h = Mathf.Ceil(pad + 18 + th + pad);
        UI.TL(coachTip, Mathf.Round(x), Mathf.Round(y), W, h);
        UI.TL(coachStep.rectTransform, pad, pad - 4, textW, 16);
        UI.TL(coachText.rectTransform, pad, pad + 16, textW, Mathf.Ceil(th) + 4);
    }
    bool pickedThisStrip; Image coachBox;

    // The strips slide in from both sides (staggered), meet edge to edge, give a small "stitched" bump, then glow gold.
    readonly List<RectTransform> sewCols = new List<RectTransform>(); Image sewGlow; float sewW;
    void UpdateSew(float t)
    {
        float e = t - sewT, glow = Mathf.Clamp01((e - 1.6f) / .5f);
        int n = sewCols.Count; float total = 0f; foreach (var c in sewCols) total += c.sizeDelta.x;
        for (int i = 0; i < n; i++)
        {
            float k = UI.EaseOut((e - .25f - i * .12f) / 1.1f);
            float x = -total / 2f; for (int q = 0; q < i; q++) x += sewCols[q].sizeDelta.x;
            float mid = (i - (n - 1) / 2f), gap = 70f * (1f - k), from = mid * 160f * (1f - k);
            float bump = e > 1.4f && e < 1.7f ? Mathf.Sin((e - 1.4f) / .3f * Mathf.PI) * 6f : 0f;
            sewCols[i].anchoredPosition = new Vector2(x + mid * gap + from, Mathf.Sin(k * Mathf.PI) * 14f * (i % 2 == 0 ? 1 : -1) + bump);
            sewCols[i].localRotation = Quaternion.Euler(0, 0, (1f - k) * (i % 2 == 0 ? 4f : -4f));
        }
        if (sewGlow != null)
        {
            sewGlow.rectTransform.sizeDelta = new Vector2(total + 60f, sewW + 60f);
            var c = sewGlow.color; c.a = .55f * glow * (.85f + .15f * Mathf.Sin(e * 4f)); sewGlow.color = c;
        }
        sewTitle.text = glow > 0 ? "Kente, fit for a king" : "Sewing the strips together";
    }

    // ---------- cloth preview (left panel) ----------
    void BuildClothPreview()
    {
        foreach (Transform c in clothBox) Destroy(c.gameObject);
        clothCells.Clear(); clothColOutline.Clear();
        int N = NumStrips, Rw = free ? freeRowsN : 6;
        float u = 196f / Mathf.Max(2 * N, Rw), w = u * 2 * N, h = u * Rw, gap = N > 8 ? 1 : 3;
        UI.TL(clothBox, 18 + (196 - w) / 2 - 4, 86 + (196 - h) / 2 - 4, w + 8, h + 8);
        float cw = (w - gap * (N - 1)) / N;
        for (int si = 0; si < N; si++)
        {
            var outline = UI.Img("Current", clothBox, null, Gold); UI.TL(outline.rectTransform, 4 + si * (cw + gap) - 3, 1, cw + 6, h + 6); outline.enabled = false; clothColOutline.Add(outline);
            var list = new List<Image>();
            for (int ri = 0; ri < Rw; ri++) { var im = UI.Img("Cell", clothBox, null, Empty); UI.TL(im.rectTransform, 4 + si * (cw + gap), 4 + h - (ri + 1) * u, cw, u + .5f); list.Add(im); }
            clothCells.Add(list);
        }
    }
    void UpdateClothPreview(float t)
    {
        var woven = Woven(); var tut = KenteData.TutorialStrips();
        for (int si = 0; si < clothCells.Count; si++)
        {
            bool cur = si == strip, past = si < strip;
            clothColOutline[si].enabled = cur;
            for (int ri = 0; ri < clothCells[si].Count; ri++)
            {
                Color c = Empty; string id = null; float wash = 0;
                if (past && si < done.Count && ri < done[si].Count) id = done[si][ri];
                else if (cur) { if (ri < woven.Count) id = woven[ri]; else if (!free) { id = tut[si][ri]; wash = .55f; } }
                else if (!free && si < 3) { id = tut[si][ri]; wash = .78f; }
                if (id != null) c = Color.Lerp(byId[id].color, Cream, wash);
                if (cur && ri == rows && stripDoneT < 0) { c = id != null ? byId[id].color : Empty; c = Color.Lerp(c, Color.white, .15f + .15f * Mathf.Sin(t / .16f)); }
                clothCells[si][ri].color = c;
            }
        }
    }

    void UpdateSpools()
    {
        for (int i = 0; i < spoolCards.Count; i++)
        {
            bool on = sel == colors[i].id;
            spoolCards[i].color = on ? CardOn : CardBg;
            spoolRects[i].anchoredPosition = new Vector2(spoolRects[i].anchoredPosition.x, -(84 + (i / 2) * 134) + (on ? 4 : 0));
            spoolRings[i].SetActive(on);
        }
    }

    void BuildSewRow()
    {
        foreach (Transform c in sewRow) Destroy(c.gameObject);
        sewCols.Clear();
        var hl = sewRow.GetComponent<HorizontalLayoutGroup>(); if (hl != null) hl.enabled = false; // strips are moved by hand in UpdateSew
        int n = done.Count, r = 0; foreach (var s in done) r = Mathf.Max(r, s.Count);
        float u = 380f / Mathf.Max(2 * n, r); sewW = u * r;
        sewGlow = UI.Panel("Glow", sewRow, new Color(1f, .8f, .4f, 0f), 28, null); sewGlow.raycastTarget = false;
        var gr = sewGlow.rectTransform; gr.anchorMin = gr.anchorMax = gr.pivot = new Vector2(.5f, .5f); gr.anchoredPosition = Vector2.zero;
        foreach (var s in done)
        {
            var col = UI.Node("Strip", sewRow); col.anchorMin = col.anchorMax = new Vector2(.5f, .5f); col.pivot = new Vector2(0f, .5f); col.sizeDelta = new Vector2(u * 2, u * r);
            sewCols.Add(col);
            var sh = UI.Img("Shadow", col, null, new Color(0, 0, 0, .25f)); UI.TL(sh.rectTransform, 0, 10, u * 2, u * r);
            for (int ri = 0; ri < s.Count; ri++) { var im = UI.Img("Row", col, null, byId[s[ri]].color); UI.TL(im.rectTransform, 0, u * r - (ri + 1) * u, u * 2, u + .5f); }
        }
    }
    void BuildEndIcon()
    {
        foreach (Transform c in endIcon) if (c.name == "Mini") Destroy(c.gameObject);
        int n = done.Count, r = 0; foreach (var s in done) r = Mathf.Max(r, s.Count);
        float u = 96f / Mathf.Max(2 * n, r);
        var mini = UI.Node("Mini", endIcon); UI.Center(mini, 0, 0, u * 2 * n, u * r); mini.localRotation = Quaternion.Euler(0, 0, 8);
        for (int si = 0; si < n; si++) for (int ri = 0; ri < done[si].Count; ri++) { var im = UI.Img("c", mini, null, byId[done[si][ri]].color); UI.TL(im.rectTransform, si * u * 2, u * r - (ri + 1) * u, u * 2, u + .5f); }
    }

    // =====================================================================
    // UI construction
    void ShowOnly(RectTransform scr)
    {
        foreach (var s in new[] { dialogueScr, setupScr, weaveScr, sewScr, endScr }) s.gameObject.SetActive(s == scr);
    }

    void BuildUI()
    {
        canvasGO = new GameObject("KenteGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 600;
        var root = (RectTransform)canvasGO.transform;
        scrim = UI.Img("CameraDim", root, null, new Color(.07f, .04f, .02f, cameraDim)); UI.Stretch(scrim.rectTransform);
        safe = UI.Stretch(UI.Node("SafeArea", root));
        stage = UI.Center(UI.Node("Stage", safe), 0, 0, SW, SH);

        BuildDialogue(); BuildSetup(); BuildWeave(); BuildSew(); BuildEnd(); BuildGallery();
        BuildRotatePrompt(root);
        quitBtnT = null;
        var quit = UI.Btn(stage, "X", Red, Cream, (Color)new Color32(110, 30, 22, 255), CloseGame, 26, font, (Color)new Color32(122, 36, 28, 255), 28);
        UI.TL((RectTransform)quit.transform, SW - 26 - 56, 22, 56, 56); quitBtnT = quit.transform;
        ShowOnly(null);
        canvasGO.SetActive(false);
    }

    RectTransform MakeScreen(string n) { var r = UI.TL(UI.Node(n, stage), 0, 0, SW, SH); return r; }

    void BuildDialogue()
    {
        dialogueScr = MakeScreen("Dialogue");
        var tap = UI.Img("Tap", dialogueScr, null, Color.clear); UI.Stretch(tap.rectTransform); tap.raycastTarget = true;
        var tb = tap.gameObject.AddComponent<Button>(); tb.transition = Selectable.Transition.None; tb.onClick.AddListener(DlgTap);
        float ah = 560, aw = ananseS != null ? ah * ananseS.rect.width / ananseS.rect.height : 400;
        dlgAnanse = UI.Pic(dialogueScr, ananseS); UI.TL(dlgAnanse.rectTransform, 30, SH - ah, aw, ah);
        var bub = UI.Panel("Bubble", dialogueScr, Cream, 28, Ink, 3.4f); UI.TL(bub.rectTransform, 520, SH - 78 - 240, 700, 240);
        var sh = bub.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .25f); sh.effectDistance = new Vector2(0, -8);
        foreach (var (x, y, d) in new[] { (-34f, -8f, 26f), (-64f, -40f, 18f), (-86f, -66f, 12f) }) { var c = UI.Panel("Trail", bub.transform, Cream, d / 2, Ink, 4); UI.TL(c.rectTransform, x, y, d, d); }
        var tag = UI.Panel("Name", bub.transform, Brown, 22, Ink, 2.7f); UI.TL(tag.rectTransform, 350 - 110, -22, 220, 44);
        UI.NameTabLine(tag.rectTransform, 22, 4f);
        var tn = UI.Label(tag.transform, "Kwaku Ananse", 22, Cream, TextAnchor.MiddleCenter, font); UI.Stretch(tn.rectTransform);
        dlgText = UI.Label(bub.transform, "", 28, Ink, TextAnchor.UpperLeft, font); dlgText.fontStyle = FontStyle.BoldAndItalic; UI.TL(dlgText.rectTransform, 40, 46, 620, 150);
        dlgHintTxt = UI.Label(bub.transform, "Tap to continue  >", 16, Hint, TextAnchor.LowerRight, font); UI.TL(dlgHintTxt.rectTransform, 400, 200, 270, 26);
        var sk = UI.Btn(dialogueScr, "Skip  >>", Brown, Cream, Ink, SkipDlg, 18, font, Ink, 22); UI.TL((RectTransform)sk.transform, SW - 60 - 130, SH - 14 - 44, 130, 44); skipBtn = sk.gameObject;
    }

    void BuildSetup()
    {
        setupScr = MakeScreen("Setup");
        var card = UI.Panel("Card", setupScr, Cream, 36, Ink); UI.Center(card.rectTransform, 0, 0, 900, 560); UI.KenteBand(card.rectTransform, 36);
        setupTitle = UI.Label(card.transform, "", 38, Brown, TextAnchor.MiddleCenter, font); UI.TL(setupTitle.rectTransform, 0, 58, 900, 50);
        setupSub = UI.Label(card.transform, "", 19, Ink, TextAnchor.MiddleCenter, font); UI.TL(setupSub.rectTransform, 60, 100, 780, 44);
        int firstContent = card.transform.childCount; // everything below is moved down to make room for the mode tabs
        var stepG = UI.TL(UI.Node("Steppers", card.transform), 0, 0, 900, 560); steppersGO = stepG.gameObject;
        Stepper(stepG, 90, 176, "Strips", "1 to 12 strips, sewn side by side", 0, out valN);
        Stepper(stepG, 90, 290, "Rows per strip", "4 to 24 rows, woven bottom to top", 2, out valR);
        BuildPatternPicker(card.transform);
        presetsRow = UI.TL(UI.Node("Presets", card.transform), 90, 404, 400, 40).gameObject;
        float px = 0;
        for (int i = 0; i < Presets.Length; i++)
        {
            int k = i; var p = Presets[i]; float w = 26 + p.label.Length * 9;
            var b = UI.Btn(presetsRow.transform, p.label, CardBg, Ink, null, () => { if (!free) return; pickN = Presets[k].n; pickR = Presets[k].r; RefreshSetup(); }, 15, font, Ink, 19);
            UI.TL((RectTransform)b.transform, px, 0, w, 38); presetBgs.Add(b.GetComponent<Image>()); px += w + 8;
        }
        var pv = UI.Panel("PreviewBg", card.transform, CardBg, 20, null); UI.TL(pv.rectTransform, 560, 170, 260, 260);
        var box = UI.Img("Cloth", pv.transform, null, Ink); previewBox = UI.Center(box.rectTransform, 0, 0, 100, 100);
        shapeLbl = UI.Label(card.transform, "", 17, Brown, TextAnchor.MiddleCenter, font); UI.TL(shapeLbl.rectTransform, 540, 436, 300, 24);
        infoLbl = UI.Label(card.transform, "", 14, Hint, TextAnchor.MiddleCenter, font); UI.TL(infoLbl.rectTransform, 540, 460, 300, 20);
        var hintL = UI.Label(card.transform, "Old weavers say the Sky God is kind to a generous cloth…", 13, Hint, TextAnchor.MiddleCenter, font, false);
        hintL.fontStyle = FontStyle.Italic; UI.TL(hintL.rectTransform, 520, 480, 340, 20); UI.Alpha(hintL, .85f); setupHint = hintL;
        var start = UI.Btn(card.transform, "Start weaving", Gold, Ink, GoldDark, BeginWeaving, 24, font, Ink); UI.TL((RectTransform)start.transform, 240, 580 - 102, 420, 60);
        // move Start below the content row
        UI.TL((RectTransform)start.transform, 90, 470, 360, 60);
        BuildSetupExtras(card.rectTransform, firstContent);
    }
    // Tutorial only: five kente designs to choose from (3 strips x 6 rows each).
    GameObject steppersGO, patternsGO; readonly List<Image> patternBgs = new List<Image>(); readonly List<GameObject> patternRings = new List<GameObject>();
    void BuildPatternPicker(Transform card)
    {
        var root = UI.TL(UI.Node("Patterns", card), 70, 164, 440, 300); patternsGO = root.gameObject;
        var t = UI.Label(root, "Pick a pattern", 20, Brown, TextAnchor.MiddleLeft, font); UI.TL(t.rectTransform, 20, 0, 300, 26);
        const float cw = 132, ch = 126, gap = 10;
        for (int k = 0; k < KenteData.Patterns.Length; k++)
        {
            int idx = k; var pat = KenteData.Patterns[k];
            int col = k % 3, row = k / 3;
            float x = 20 + col * (cw + gap) + (row == 1 ? (cw + gap) / 2f : 0f), y = 34 + row * (ch + gap); // 3 on top, 2 centred below
            var chip = UI.Panel("Pattern " + pat.name, root, CardBg, 18, null); chip.raycastTarget = true; UI.TL(chip.rectTransform, x, y, cw, ch);
            var b = chip.gameObject.AddComponent<Button>(); b.targetGraphic = chip;
            b.onClick.AddListener(() => { if (free || adv || firstTime) return; KenteData.PatternIndex = idx; RefreshSetup(); });
            var ring = UI.Panel("SelRing", chip.transform, new Color(0, 0, 0, 0), 22, Gold, 4); ring.raycastTarget = false; UI.TL(ring.rectTransform, -5, -5, cw + 10, ch + 10);
            float cellW = 18, cellH = 11, gw = cellW * 3, gh = cellH * 6;
            var frame = UI.Img("Mini", chip.transform, null, Ink); UI.TL(frame.rectTransform, (cw - gw) / 2f - 3, 12, gw + 6, gh + 6); frame.raycastTarget = false;
            for (int si = 0; si < 3; si++) for (int ri = 0; ri < 6; ri++)
                { var c = UI.Img("c", frame.transform, null, byId[pat.strips[si][ri]].color); UI.TL(c.rectTransform, 3 + si * cellW, 3 + (5 - ri) * cellH, cellW, cellH + .5f); }
            var nm = UI.Label(chip.transform, pat.name, 15, Ink, TextAnchor.MiddleCenter, font); UI.TL(nm.rectTransform, 0, 88, cw, 20);
            var tg = UI.Label(chip.transform, pat.tag.ToUpper(), 10, Hint, TextAnchor.MiddleCenter, font); UI.TL(tg.rectTransform, 0, 106, cw, 14);
            patternBgs.Add(chip); patternRings.Add(ring.gameObject);
        }
    }

    void Stepper(Transform parent, float x, float y, string label, string sub, int idx, out Text val)
    {
        var l = UI.Label(parent, label, 20, Brown, TextAnchor.MiddleLeft, font); UI.TL(l.rectTransform, x, y, 300, 26); stepLbl[idx] = l;
        var minus = UI.Btn(parent, "−", CardBg, Ink, Tan, () => Step(idx == 0 ? -1 : 0, idx == 2 ? -1 : 0), 30, font, Ink, 18);
        UI.TL((RectTransform)minus.transform, x, y + 30, 56, 56); stepBtns[idx] = minus.GetComponent<Image>();
        val = UI.Label(parent, "0", 40, Ink, TextAnchor.MiddleCenter, font); UI.TL(val.rectTransform, x + 60, y + 30, 90, 56);
        var plus = UI.Btn(parent, "+", CardBg, Ink, Tan, () => Step(idx == 0 ? 1 : 0, idx == 2 ? 1 : 0), 30, font, Ink, 18);
        UI.TL((RectTransform)plus.transform, x + 154, y + 30, 56, 56); stepBtns[idx + 1] = plus.GetComponent<Image>();
        var s = UI.Label(parent, sub, 13, Hint, TextAnchor.MiddleLeft, font); UI.TL(s.rectTransform, x, y + 90, 360, 18); stepSub[idx] = s;
    }

    void BuildWeave()
    {
        weaveScr = MakeScreen("Weave");
        // 3D loom view
        if (loomBackdrop)
        {
            // see-through dark brown column behind the loom, running from the very top of the screen to the very bottom
            var bd = UI.Img("LoomBackdrop", weaveScr, null, new Color32(40, 22, 10, (byte)(255 * loomBackdropOpacity))); bd.raycastTarget = false;
            var br = bd.rectTransform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, .5f);
            br.offsetMin = new Vector2(340, -4000); br.offsetMax = new Vector2(940, 4000);
        }
        loomView = UI.Node("LoomView", weaveScr).gameObject.AddComponent<RawImage>(); loomRect = UI.TL(loomView.rectTransform, 340, 40, 600, 580);
        loomView.raycastTarget = true; loomView.gameObject.AddComponent<KenteLoomInput>().game = this;
        // top pill
        var pill = UI.Panel("Pill", weaveScr, Cream, 26, Ink); UI.TL(pill.rectTransform, 640 - 190, 22, 380, 52);
        pillText = UI.Label(pill.transform, "", 22, Brown, TextAnchor.MiddleCenter, font); UI.Stretch(pillText.rectTransform);
        // left panel: cloth preview
        var lp = UI.Panel("ClothPanel", weaveScr, Cream, 30, Ink); UI.TL(lp.rectTransform, 60, 96, 240, 456); UI.KenteBand(lp.rectTransform, 30); clothPanelGO = lp.gameObject;
        var lt = UI.Label(lp.transform, "Pattern", 26, Brown, TextAnchor.MiddleCenter, font); UI.TL(lt.rectTransform, 0, 46, 240, 34);
        var cbx = UI.Img("ClothBox", lp.transform, null, Ink); clothBox = cbx.rectTransform;
        clothCaption = UI.Label(lp.transform, "", 15, Brown, TextAnchor.MiddleCenter, font); UI.TL(clothCaption.rectTransform, 0, 300, 240, 20);
        clothSub = UI.Label(lp.transform, "", 13, Hint, TextAnchor.MiddleCenter, font); UI.TL(clothSub.rectTransform, 0, 322, 240, 18);
        var nb = UI.Panel("NextThread", lp.transform, CardBg, 18, null); UI.TL(nb.rectTransform, 18, 456 - 18 - 72, 204, 72); nextBox = nb.gameObject;
        nextSpool = UI.Pic(nb.transform, null); UI.TL(nextSpool.rectTransform, 14, 17, 64, 38);
        var nl = UI.Label(nb.transform, "NEXT THREAD", 12, Hint, TextAnchor.UpperLeft, font); UI.TL(nl.rectTransform, 88, 14, 120, 16);
        nextName = UI.Label(nb.transform, "", 22, Ink, TextAnchor.UpperLeft, font); UI.TL(nextName.rectTransform, 88, 32, 120, 28);
        var ub = UI.Btn(lp.transform, "Undo row", CardBg, Brown, Tan, UndoRow, 20, font, Ink, 18); UI.TL((RectTransform)ub.transform, 18, 456 - 18 - 56, 204, 56);
        undoBtn = ub.gameObject; undoGroup = undoBtn.AddComponent<CanvasGroup>();
        // right panel: threads
        var rp = UI.Panel("Threads", weaveScr, Cream, 30, Ink); UI.TL(rp.rectTransform, 980, 96, 240, 500); UI.KenteBand(rp.rectTransform, 30);
        var rt2 = UI.Label(rp.transform, "Threads", 26, Brown, TextAnchor.MiddleCenter, font); UI.TL(rt2.rectTransform, 0, 46, 240, 34);
        for (int i = 0; i < colors.Count; i++)
        {
            var c = colors[i]; int col = i % 2, row = i / 2; string id = c.id;
            var card = UI.Panel("Spool " + c.id, rp.transform, CardBg, 20, null); card.raycastTarget = true;
            var rr = UI.TL(card.rectTransform, 14 + col * 106, 84 + row * 134, 98, 124);
            var b = card.gameObject.AddComponent<Button>(); b.targetGraphic = card; b.onClick.AddListener(() => Pick(id));
            var selRing = UI.Panel("SelRing", card.transform, new Color(0, 0, 0, 0), 24, Gold, 5); selRing.raycastTarget = false; UI.TL(selRing.rectTransform, -6, -6, 110, 136); selRing.gameObject.SetActive(false);
            var pic = UI.Pic(card.transform, spoolS[id]); UI.TL(pic.rectTransform, 4, 20, 90, 56);
            var nm = UI.Label(card.transform, c.name, 18, Ink, TextAnchor.MiddleCenter, font); UI.TL(nm.rectTransform, 0, 86, 98, 24);
            spoolCards.Add(card); spoolRects.Add(rr); spoolRings.Add(selRing.gameObject);
        }
        // Ananse + hint bar
        float ah = 164, aw = ananseS != null ? ah * ananseS.rect.width / ananseS.rect.height : 120;
        ananseSmall = UI.Pic(weaveScr, ananseS); UI.TL(ananseSmall.rectTransform, 150, 556, aw, ah);
        var hb = UI.Panel("HintBar", weaveScr, Cream, 26, Ink); UI.TL(hb.rectTransform, 340, 622, 600, 80);
        foreach (var (x, y, d) in new[] { (-26f, 26f, 18f), (-44f, 14f, 12f) }) { var c = UI.Panel("Trail", hb.transform, Cream, d / 2, Ink, 3); UI.TL(c.rectTransform, x, y, d, d); }
        var tag = UI.Panel("Name", hb.transform, Brown, 16, Ink, 3); UI.TL(tag.rectTransform, 24, -18, 150, 34);
        var tn = UI.Label(tag.transform, "Kwaku Ananse", 17, Cream, TextAnchor.MiddleCenter, font); UI.Stretch(tn.rectTransform);
        hintText = UI.Label(hb.transform, "", 19, Ink, TextAnchor.MiddleLeft, font); hintText.fontStyle = FontStyle.BoldAndItalic; UI.TL(hintText.rectTransform, 24, 18, 556, 56);
        // coach marks
        coachRing = UI.Panel("CoachRing", weaveScr, new Color(0, 0, 0, 0), 24, Gold, 5); coachRing.raycastTarget = false;
        coachBox = UI.Panel("CoachBox", weaveScr, new Color(0, 0, 0, 0), 4, Gold, 4); coachBox.raycastTarget = false; coachBox.gameObject.SetActive(false); // square corners, like the pattern
        coachDot = UI.Img("CoachDot", weaveScr, UI.Circle, new Color(1, .96f, .9f, .9f));
        coachTip = UI.Panel("CoachTip", weaveScr, Ink, 20, null).rectTransform;
        coachStep = UI.Label(coachTip, "", 12, Gold, TextAnchor.UpperLeft, font); UI.TL(coachStep.rectTransform, 18, 12, 230, 16);
        coachText = UI.Label(coachTip, "", 19, Cream, TextAnchor.UpperLeft, font); UI.TL(coachText.rectTransform, 18, 30, 226, 76);
        // snap + strip done
        snapA = UI.Img("SnapA", weaveScr, UI.Round, Red); snapB = UI.Img("SnapB", weaveScr, UI.Round, Red);
        snapText = UI.Label(weaveScr, "SNAP!", 34, Red, TextAnchor.MiddleCenter, font); snapText.gameObject.AddComponent<Outline>().effectColor = Cream;
        var sd = UI.Panel("StripDone", weaveScr, Green, 34, Ink); stripDoneBadge = UI.Center(sd.rectTransform, 0, 30, 380, 76);
        var sdt = UI.Label(sd.transform, "Strip finished!", 38, Cream, TextAnchor.MiddleCenter, font); UI.Stretch(sdt.rectTransform);
        BuildAdvWeaveUI();
    }

    void BuildSew()
    {
        sewScr = MakeScreen("Sew");
        var pill = UI.Panel("Title", sewScr, Cream, 30, Ink); UI.Center(pill.rectTransform, 0, 250, 520, 60);
        sewTitle = UI.Label(pill.transform, "", 30, Brown, TextAnchor.MiddleCenter, font); UI.Stretch(sewTitle.rectTransform);
        sewRow = UI.Center(UI.Node("Strips", sewScr), 0, -30, 1000, 420);
        var h = sewRow.gameObject.AddComponent<HorizontalLayoutGroup>(); h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = h.childControlHeight = false; h.childForceExpandWidth = h.childForceExpandHeight = false;
    }

    void BuildEnd()
    {
        endScr = MakeScreen("End");
        var dim = UI.Img("Dim", endScr, null, new Color(.07f, .04f, .02f, .7f)); UI.Stretch(dim.rectTransform, -4000);
        var card = UI.Panel("Card", endScr, Cream, 36, Ink); UI.Center(card.rectTransform, 0, 0, 560, 560);
        var ic = UI.Panel("Icon", card.transform, Brown, 30, Ink); endIcon = UI.TL(ic.rectTransform, 205, 40, 150, 150);
        endTitle = UI.Label(card.transform, "", 40, Brown, TextAnchor.MiddleCenter, font); UI.TL(endTitle.rectTransform, 0, 206, 560, 50);
        endBody = UI.Label(card.transform, "", 19, Ink, TextAnchor.UpperCenter, font); UI.TL(endBody.rectTransform, 40, 262, 480, 60);
        var again = UI.Btn(card.transform, "Weave again", Color.clear, Brown, null, WeaveFree, 22, font, Tan); UI.TL((RectTransform)again.transform, 40, 340, 200, 64);
        againLbl = again.GetComponentInChildren<Text>();
        var back = UI.Btn(card.transform, "Back to village", Gold, Ink, GoldDark, BackToVillage, 22, font, Ink); UI.TL((RectTransform)back.transform, 254, 340, 266, 64); toVillageBtn = back.gameObject;
        var save = UI.Btn(card.transform, "Save as image", CardBg, Ink, Tan, SaveImage, 20, font, Ink); UI.TL((RectTransform)save.transform, 40, 424, 480, 54);
        saveLbl = save.GetComponentInChildren<Text>();
        BuildEndExtras(card.rectTransform);
    }

    // ---------- helpers ----------
    Sprite LoadSprite(string path)
    {
        var s = Resources.Load<Sprite>(path); if (s != null) return s;
        var t = Resources.Load<Texture2D>(path); if (t == null) { Debug.LogWarning("Kente: missing image Resources/" + path); return null; }
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f);
    }
    void EnsureEventSystem()
    {
        AnansiRuntime.EnsureEventSystem();
    }
    void OnDestroy() { if (rt != null) rt.Release(); }
}

// Sends touches on the 3D loom picture to the game (added automatically).
public class KenteLoomInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public KenteWeavingGame game;
    Vector2 Local(PointerEventData e)
    {
        var r = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out var p);
        return new Vector2(p.x - r.rect.xMin, p.y - r.rect.yMin); // 0..600, 0..580 from bottom-left
    }
    public void OnPointerDown(PointerEventData e) => game.LoomPointerDown(Local(e));
    public void OnDrag(PointerEventData e) => game.LoomPointerDrag(Local(e));
    public void OnPointerUp(PointerEventData e) => game.LoomPointerUp();
}
