using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Options button for the village (added by itself). Top-right, round, brown + gold like the intro.
// Shows from the moment the camera opens (also on the scan screen), hidden while a mini-game is open
// (the games have their own quit button). Opens a card with: Resume, Sound on/off, Scan help, Return to Home.
// Progress is saved all the time, so Return to Home just goes back to the intro (Continue picks up from there).
public class OptionsMenu : MonoBehaviour
{
    public const string SoundKey = "Anansi_Sound";
    [Tooltip("Intro scene name. Empty = the first scene in the build list.")]
    public string homeScene = "";

    static readonly Color TitleGold = new Color32(246, 201, 122, 255), Gold = new Color32(232, 163, 61, 255), Cream = new Color32(236, 220, 192, 255), Shade = new Color32(30, 14, 6, 255);
    Canvas canvas; RectTransform root, btn, card; CanvasGroup btnGroup; Font font; Text soundLabel; bool open, leaving; float btnAlpha;

    void Start()
    {
        AudioListener.volume = PlayerPrefs.GetInt(SoundKey, 1) == 1 ? 1f : 0f;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        AnansiRuntime.EnsureEventSystem();
        var cgo = new GameObject("OptionsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 700; // above the scan screen
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        root = (RectTransform)cgo.transform;

        var b = Img("OptionsButton", root, Disc(), Color.white); btn = b.rectTransform;
        btn.anchorMin = btn.anchorMax = btn.pivot = new Vector2(1, 1); btn.sizeDelta = new Vector2(104, 104);
        var sh = b.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .45f); sh.effectDistance = new Vector2(0, -6);
        var ic = Img("Icon", btn, MenuIcon(), Color.white); ic.raycastTarget = false; var ir = ic.rectTransform; ir.anchorMin = new Vector2(.26f, .26f); ir.anchorMax = new Vector2(.74f, .74f); ir.offsetMin = ir.offsetMax = Vector2.zero;
        b.gameObject.AddComponent<Button>().onClick.AddListener(Open);
        btnGroup = b.gameObject.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        bool show = !AnansiRuntime.AnyGameOpen && !leaving;
        btnAlpha = Mathf.MoveTowards(btnAlpha, show && !open ? 1f : 0f, Time.unscaledDeltaTime / .2f);
        btnGroup.alpha = btnAlpha; btnGroup.blocksRaycasts = btnGroup.interactable = btnAlpha > .9f;
        if (open && AnansiRuntime.AnyGameOpen) Close();
        // keep clear of the notch
        var sa = Screen.safeArea; float kx = root.rect.width / Mathf.Max(1, Screen.width), ky = root.rect.height / Mathf.Max(1, Screen.height);
        btn.anchoredPosition = new Vector2(-((Screen.width - sa.xMax) * kx + 28), -((Screen.height - sa.yMax) * ky + 28));
    }

    void Open()
    {
        if (card) Close();
        if (open && card) return; open = true;
        bool portrait = Screen.height > Screen.width;
        var dim = Img("Dim", root, null, new Color(0, 0, 0, .55f)); Stretch(dim.rectTransform); card = dim.rectTransform;
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close); // tap outside = resume
        var box = Img("Card", card, CameraGate.Framed(56, false), Color.white); box.type = Image.Type.Sliced;
        var br = box.rectTransform; br.anchorMin = br.anchorMax = new Vector2(.5f, .5f); br.sizeDelta = portrait ? new Vector2(760, 900) : new Vector2(700, 840);
        box.gameObject.AddComponent<Button>(); // swallow taps on the card itself
        var s0 = box.gameObject.AddComponent<Shadow>(); s0.effectColor = new Color(0, 0, 0, .45f); s0.effectDistance = new Vector2(0, -14);
        float top = br.sizeDelta.y / 2f, w = br.sizeDelta.x - 120;
        var h = Txt(br, "Options", 54, TitleGold, FontStyle.Bold); Place(h.rectTransform, 0, top - 90, w, 70);
        h.gameObject.AddComponent<Shadow>().effectColor = Shade;
        float y = top - 210, step = portrait ? 132 : 122;
        Place(Btn(br, "Keep Playing", true, Close), 0, y, w, 104); y -= step;
        var snd = Btn(br, SoundText(), false, ToggleSound); soundLabel = snd.GetComponentInChildren<Text>(); Place(snd, 0, y, w, 104); y -= step;
        Place(Btn(br, "How to Play", false, () => Info("How to Play", HelpText)), 0, y, w, 104); y -= step;
        Place(Btn(br, "About this Game", false, () => Info("About this Game", AboutText)), 0, y, w, 104); y -= step;
        Place(Btn(br, "Return to Home", false, GoHome), 0, y, w, 104);
        StartCoroutine(Pop(br));
    }
    void Close() { open = false; if (card) Destroy(card.gameObject); card = null; }
    string SoundText() => PlayerPrefs.GetInt(SoundKey, 1) == 1 ? "Sound: On" : "Sound: Off";
    void ToggleSound()
    {
        bool on = PlayerPrefs.GetInt(SoundKey, 1) == 0;
        PlayerPrefs.SetInt(SoundKey, on ? 1 : 0); PlayerPrefs.Save();
        AudioListener.volume = on ? 1f : 0f; if (soundLabel) soundLabel.text = SoundText();
    }
    void GoHome()
    {
        if (leaving) return;
        Close(); open = true; // keep the button hidden while asking
        CameraGate.Confirm("Return to Home?", "Your journey is saved. Tap Continue Your Journey on the home screen to carry on.", "Go Home", "Cancel",
            yes => { open = false; if (yes) { leaving = true; StartCoroutine(Home()); } else Open(); });
    }

    const string HelpText =
        "<b>Scan the map</b>  Lay your paper map flat in a bright room and hold your phone above it until the village appears.\n\n" +
        "<b>Visit the teachers</b>  Tap a pin to visit. Grey pins with a padlock open once you finish the one before.\n\n" +
        "<b>Your pot</b>  The bar at the top left is your pot. Mistakes crack it. If it breaks, bring Nana Nyame a kente for a new one.\n\n" +
        "<b>Gifts</b>  Weave kente at Anansi's home, then drag it from Gifts onto Nana Nyame's pin.";
    const string AboutText =
        "<b>Anansi and the Pot of Wisdom</b>\n\n" +
        "Kwaku Ananse travels through a village that grows out of your paper map. He learns from the Farmer, the Carver, the Drummer and the Dancer, weaves kente, and keeps the knowledge he gathers in his pot.\n\n" +
        "Knowledge grows when it is shared.";

    void Info(string head, string text)
    {
        Close(); open = true;
        bool portrait = Screen.height > Screen.width;
        var dim = Img("Dim", root, null, new Color(0, 0, 0, .55f)); Stretch(dim.rectTransform); card = dim.rectTransform;
        var box = Img("Card", card, CameraGate.Framed(56, false), Color.white); box.type = Image.Type.Sliced;
        var br = box.rectTransform; br.anchorMin = br.anchorMax = new Vector2(.5f, .5f); br.sizeDelta = portrait ? new Vector2(900, 1180) : new Vector2(1180, 860);
        box.gameObject.AddComponent<Button>();
        var s0 = box.gameObject.AddComponent<Shadow>(); s0.effectColor = new Color(0, 0, 0, .45f); s0.effectDistance = new Vector2(0, -14);
        float top = br.sizeDelta.y / 2f, w = br.sizeDelta.x - 140;
        var h = Txt(br, head, 52, TitleGold, FontStyle.Bold); Place(h.rectTransform, 0, top - 86, w, 70); h.gameObject.AddComponent<Shadow>().effectColor = Shade;
        var b = Txt(br, text, portrait ? 32 : 30, Cream, FontStyle.Normal); b.supportRichText = true; b.alignment = TextAnchor.UpperLeft; b.lineSpacing = 1.1f;
        Place(b.rectTransform, 0, -10, w, br.sizeDelta.y - 330);
        Place(Btn(br, "Back", true, Open), 0, -top + 90, 360, 104);
        StartCoroutine(Pop(br));
    }

    IEnumerator Home()
    {
        var f = Img("Fade", root, null, new Color(.047f, .024f, .016f, 0)); Stretch(f.rectTransform);
        for (float t = 0; t < .45f; t += Time.unscaledDeltaTime) { f.color = new Color(.047f, .024f, .016f, t / .45f); yield return null; }
        PlayerPrefs.Save();
        IntroSequence.SkipToMenu = true;
        if (!string.IsNullOrEmpty(homeScene)) SceneManager.LoadScene(homeScene); else SceneManager.LoadScene(0);
    }

    // ---------- helpers ----------
    IEnumerator Pop(RectTransform r)
    {
        for (float t = 0; t < .28f; t += Time.unscaledDeltaTime) { if (!r) yield break; float k = t / .28f; r.localScale = Vector3.one * (k < .6f ? Mathf.Lerp(.86f, 1.03f, k / .6f) : Mathf.Lerp(1.03f, 1f, (k - .6f) / .4f)); yield return null; }
        if (r) r.localScale = Vector3.one;
    }
    static RectTransform Node(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return (RectTransform)g.transform; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Place(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }
    static Image Img(string n, Transform p, Sprite s, Color c) { var i = Node(n, p).gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; return i; }
    Text Txt(Transform p, string s, int size, Color c, FontStyle st)
    {
        var t = Node("Text", p).gameObject.AddComponent<Text>(); t.font = font; t.text = s; t.fontSize = size; t.color = c; t.fontStyle = st; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; return t;
    }
    RectTransform Btn(Transform p, string label, bool primary, System.Action onClick)
    {
        var i = Img(label, p, primary ? GoldPill() : CameraGate.Framed(54, true), Color.white); i.type = Image.Type.Sliced;
        if (primary) { var s = i.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .4f); s.effectDistance = new Vector2(0, -8); }
        i.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
        var t = Txt(i.transform, label, 38, primary ? (Color)new Color32(61, 30, 12, 255) : Gold, FontStyle.Bold); Stretch(t.rectTransform);
        if (primary) { var ts = t.gameObject.AddComponent<Shadow>(); ts.effectColor = new Color(1f, .93f, .75f, .45f); ts.effectDistance = new Vector2(0, -2); }
        else t.gameObject.AddComponent<Shadow>().effectColor = Shade;
        return i.rectTransform;
    }
    static Sprite discS, menuS, goldS;
    // Filled gold pill (brown text on it): the main button in the Options card.
    public static Sprite GoldPill()
    {
        if (goldS) return goldS;
        const int R = 54, S = R * 2 + 4; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        Color top = new Color32(250, 214, 146, 255), bot = new Color32(218, 150, 62, 255), rim = new Color32(150, 92, 34, 255);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x + .5f - S / 2f) - (S / 2f - 1 - R), 0), dy = Mathf.Max(Mathf.Abs(y + .5f - S / 2f) - (S / 2f - 1 - R), 0);
            float d = R - Mathf.Sqrt(dx * dx + dy * dy);
            Color face = Color.Lerp(bot, top, y / (float)S);
            if (y > S * .55f) face = Color.Lerp(face, Color.white, .08f); // soft top sheen
            Color c = Color.Lerp(rim, face, Mathf.Clamp01(d - 3f)); c.a = Mathf.Clamp01(d); px[y * S + x] = c;
        }
        t.SetPixels(px); t.Apply();
        return goldS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R + 2, R + 2, R + 2, R + 2));
    }
    static Sprite Disc()
    {
        if (discS) return discS;
        const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float d = Mathf.Sqrt((x + .5f - 64) * (x + .5f - 64) + (y + .5f - 64) * (y + .5f - 64)), v = y / (float)S;
            Color rim = Color.Lerp(new Color32(184, 118, 46, 255), new Color32(246, 201, 122, 255), v), face = Color.Lerp(new Color32(46, 21, 9, 255), new Color32(100, 49, 20, 255), v);
            Color c = Color.Lerp(rim, face, Mathf.Clamp01(57f - d)); c.a = Mathf.Clamp01(63f - d); px[y * S + x] = c;
        }
        t.SetPixels(px); t.Apply(); return discS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100);
    }
    static Sprite MenuIcon()
    {
        if (menuS) return menuS;
        const int S = 96; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float a = 0, sh = 0;
            foreach (float ly in new[] { 22f, 48f, 74f })
            {
                float dx = Mathf.Max(Mathf.Abs(x + .5f - 48) - 34, 0);
                a = Mathf.Max(a, Mathf.Clamp01(6.5f - Mathf.Sqrt(dx * dx + (y + .5f - ly) * (y + .5f - ly))));
                sh = Mathf.Max(sh, Mathf.Clamp01(6.5f - Mathf.Sqrt(dx * dx + (y + .5f - ly + 3) * (y + .5f - ly + 3))));
            }
            Color g = Color.Lerp(new Color32(237, 185, 104, 255), new Color32(251, 220, 160, 255), y / (float)S);
            Color c = Color.Lerp(new Color(Shade.r, Shade.g, Shade.b, sh), g, a); c.a = Mathf.Max(a, sh); px[y * S + x] = c;
        }
        t.SetPixels(px); t.Apply(); return menuS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100);
    }
}
