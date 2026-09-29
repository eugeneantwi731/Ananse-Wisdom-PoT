using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Simple speech box for the village map: portrait, name tab, typed text, tap to continue.
// Anansi's thoughts (isThought) show in italics with no name tab, so the other characters never "hear" them.
public class JourneyDialogue : MonoBehaviour
{
    [Serializable] public class Line
    {
        public string speaker; [TextArea] public string text; public Sprite portrait; public bool isThought;
        [Tooltip("Show the portrait as a big illustration at the bottom corner (cut off at the screen edge), with the box beside it.")]
        public bool fullArt; public bool artOnRight = true;
        [Tooltip("Which top part of the picture shows above the screen edge (1 = all of it, 0.58 = crown to lap for a full figure).")]
        public float artCrop = 1f;
        public Line(string s, string t, Sprite p = null, bool thought = false) { speaker = s; text = t; portrait = p; isThought = thought; }
    }

    public static JourneyDialogue Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.root.activeSelf;
    public Font font;
    public Sprite anansePortrait;

    GameObject root; Text nameText, bodyText, hint; Image portrait, tab, art; RectTransform boxR, tabR, bodyR; Sprite artShown; bool artRight; float artInT = -9f, artSwapT = -9f;
    Queue<Line> queue; Action onEnd; string full; float t0; bool typing;
    const float ArtVisibleH = 760f, ArtShownPart = .58f; // how tall the illustration shows, and which top part of it (0.58 = crown to lap)
    static readonly Color Cream = new Color32(246, 235, 217, 255), Ink = new Color32(61, 36, 22, 255), Gold = new Color32(232, 163, 61, 255), Dark = new Color32(28, 14, 6, 235),
        Peg = new Color32(107, 62, 38, 255), HintC = new Color32(150, 110, 80, 255);
    float artCrop = 1f; readonly List<Image> dots = new List<Image>();
    // Loads a picture from Resources as a Sprite, even if Unity imported it as a plain texture.
    public static Sprite LoadArt(string path)
    {
        var s = Resources.Load<Sprite>(path); if (s != null) return s;
        var t = Resources.Load<Texture2D>(path); if (t == null) return null;
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f);
    }
    Image tail; static Sprite tailS;
    // speech-bubble tail: cream triangle with the dark rim on its two outer sides (the side on the bubble stays open)
    static Sprite TailSprite()
    {
        if (tailS) return tailS;
        const int S = 64; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S]; Vector2 A = new Vector2(0, 58), B = new Vector2(0, 14), C = new Vector2(60, 6); // points right-down
        Color32 cream = new Color32(246, 235, 217, 255), ink = new Color32(61, 36, 22, 255);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            var p = new Vector2(x + .5f, y + .5f);
            float d1 = Side(A, C, p), d2 = Side(C, B, p), d0 = x + .5f; // distances inside the edges AC, CB and the open edge x=0
            if (d1 < 0 || d2 < 0) continue;
            px[y * S + x] = (Mathf.Min(d1, d2) < 5f) ? ink : cream;
        }
        t.SetPixels32(px); t.Apply();
        tailS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0, .5f), 100);
        return tailS;
    }
    static float Side(Vector2 a, Vector2 b, Vector2 p) { var n = new Vector2(a.y - b.y, b.x - a.x).normalized; return Vector2.Dot(p - a, n); }

    static Sprite roundS;
    // rounded 9-slice sprite (radius 28) made in code, same look as the game's dialogue bubbles
    static Sprite Round()
    {
        if (roundS) return roundS;
        const int S = 64, Rr = 28; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float cx = Mathf.Clamp(x + .5f, Rr, S - Rr), cy = Mathf.Clamp(y + .5f, Rr, S - Rr);
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
            px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(Rr - d + .5f) * 255));
        }
        t.SetPixels32(px); t.Apply();
        roundS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(Rr, Rr, Rr, Rr));
        return roundS;
    }
    Image RoundImg(string n, Transform p, Color c, float ppuMul = 1f) { var i = Img(n, p, c); i.sprite = Round(); i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = ppuMul; return i; }

    public static void Play(IEnumerable<Line> lines, Action done = null)
    {
        if (Instance == null) new GameObject("JourneyDialogue").AddComponent<JourneyDialogue>();
        Instance.Begin(lines, done);
    }
    public static void Think(string text, Action done = null) => Play(new[] { new Line("Kwaku Ananse", text, null, true) }, done);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (anansePortrait == null) anansePortrait = LoadArt("KenteGame/ananse");
        AnansiRuntime.EnsureEventSystem();
        Build(); root.SetActive(false);
    }

    void Build()
    {
        root = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var cv = root.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 700;
        var sc = root.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        var catcher = Img("TapCatcher", root.transform, new Color(0, 0, 0, .25f)); Stretch(catcher.rectTransform);
        catcher.gameObject.AddComponent<Button>().onClick.AddListener(Next);
        art = Img("CharacterArt", root.transform, Color.white); art.preserveAspect = true; art.raycastTarget = false; art.enabled = false;
        // cream bubble with a dark rim, brown name peg on top (matches the Farmer and weaving dialogue)
        var box = RoundImg("Box", root.transform, Ink); boxR = box.rectTransform; box.raycastTarget = false;
        var br = box.rectTransform; br.anchorMin = br.anchorMax = new Vector2(.5f, 0); br.pivot = new Vector2(.5f, 0); br.sizeDelta = new Vector2(1100, 260); br.anchoredPosition = new Vector2(0, 40);
        var sh = box.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, .28f); sh.effectDistance = new Vector2(0, -10);
        var fill = RoundImg("Fill", box.transform, Cream); fill.raycastTarget = false; Stretch(fill.rectTransform); fill.rectTransform.offsetMin = Vector2.one * 5; fill.rectTransform.offsetMax = -Vector2.one * 5;
        portrait = Img("Portrait", box.transform, Color.white); portrait.preserveAspect = true; portrait.raycastTarget = false;
        var pr = portrait.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0, 0); pr.pivot = new Vector2(0, 0); pr.sizeDelta = new Vector2(300, 380); pr.anchoredPosition = new Vector2(-40, 0);
        tab = RoundImg("NameTab", box.transform, Ink, 1.6f); tab.raycastTarget = false;
        var tr = tab.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(.5f, 1); tr.pivot = new Vector2(.5f, .5f); tr.sizeDelta = new Vector2(320, 62); tr.anchoredPosition = new Vector2(0, 4); tabR = tr;
        var peg = RoundImg("Peg", tr, Peg, 1.6f); peg.raycastTarget = false; Stretch(peg.rectTransform); peg.rectTransform.offsetMin = Vector2.one * 4; peg.rectTransform.offsetMax = -Vector2.one * 4;
        nameText = Txt(tr, 30, Cream, TextAnchor.MiddleCenter); nameText.fontStyle = FontStyle.Bold; Stretch(nameText.rectTransform);
        tail = Img("Tail", box.transform, Color.white); tail.sprite = TailSprite(); tail.raycastTarget = false; tail.gameObject.SetActive(false);
        bodyText = Txt(box.transform, 38, Ink, TextAnchor.UpperLeft); bodyText.lineSpacing = 1.05f;
        var b = bodyText.rectTransform; b.anchorMin = Vector2.zero; b.anchorMax = Vector2.one; b.offsetMin = new Vector2(290, 50); b.offsetMax = new Vector2(-44, -52); bodyR = b;
        hint = Txt(box.transform, 24, HintC, TextAnchor.LowerRight); hint.text = "Tap to continue  >"; hint.fontStyle = FontStyle.Bold;
        var h = hint.rectTransform; h.anchorMin = Vector2.zero; h.anchorMax = Vector2.one; h.offsetMin = new Vector2(0, 18); h.offsetMax = new Vector2(-30, 0);
        // thought dots (Anansi's thoughts only), trailing from the bubble toward him
        float[][] dd = { new[] { -20f, 250f, 26f }, new[] { -46f, 286f, 17f }, new[] { -62f, 312f, 11f } };
        foreach (var d in dd)
        {
            var o = RoundImg("ThoughtDot", box.transform, Ink, 28f / (d[2] * .5f)); o.raycastTarget = false;
            var orr = o.rectTransform; orr.anchorMin = orr.anchorMax = new Vector2(0, 0); orr.pivot = new Vector2(.5f, .5f); orr.sizeDelta = Vector2.one * d[2]; orr.anchoredPosition = new Vector2(d[0], d[1]);
            var inner = RoundImg("In", orr, Cream, 28f / (d[2] * .5f - 3f)); Stretch(inner.rectTransform); inner.rectTransform.offsetMin = Vector2.one * 3; inner.rectTransform.offsetMax = -Vector2.one * 3;
            dots.Add(o);
        }
    }

    void Begin(IEnumerable<Line> lines, Action done)
    {
        queue = new Queue<Line>(lines); onEnd = done; root.SetActive(true); ShowNext();
    }
    void ShowNext()
    {
        if (queue.Count == 0) { artShown = null; art.enabled = false; root.SetActive(false); var e = onEnd; onEnd = null; e?.Invoke(); return; }
        var l = queue.Dequeue();
        var p = l.portrait != null ? l.portrait : (l.speaker == "Kwaku Ananse" ? anansePortrait : null);
        // Anansi (no picture given) shows big on the left, facing into the scene
        bool autoAnanse = l.portrait == null && l.speaker == "Kwaku Ananse" && anansePortrait != null;
        bool big = (l.fullArt && p != null) || autoAnanse;
        bool right = autoAnanse ? false : l.artOnRight;
        artCrop = autoAnanse ? 1f : Mathf.Clamp(l.artCrop, .2f, 1f);
        foreach (var d in dots) d.gameObject.SetActive(l.isThought);
        tail.gameObject.SetActive(big && !l.isThought);
        portrait.sprite = big ? null : p; portrait.enabled = !big && p != null;
        LayoutArt(big ? p : null, right);
        bodyText.fontStyle = l.isThought ? FontStyle.BoldAndItalic : FontStyle.Bold;
        tab.gameObject.SetActive(!l.isThought); nameText.text = l.speaker;
        full = l.isThought ? "(" + l.text + ")" : l.text; t0 = Time.unscaledTime; typing = true; bodyText.text = "";
    }
    // Big character illustration in the bottom corner; the box moves beside it.
    void LayoutArt(Sprite s, bool right)
    {
        float now = Time.unscaledTime;
        if (s == null) { art.enabled = false; artShown = null; boxR.sizeDelta = new Vector2(1100, 260); boxR.anchoredPosition = new Vector2(0, 40); bodyR.offsetMin = new Vector2(portrait.enabled ? 290 : 44, 50); return; }
        if (artShown == null || right != artRight) artInT = now; else if (s != artShown) artSwapT = now;
        artShown = s; artRight = right; art.sprite = s; art.enabled = true;
        float h = ArtVisibleH / artCrop, w = h * s.rect.width / s.rect.height;
        var r = art.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(right ? 1 : 0, 0);
        r.sizeDelta = new Vector2(w, h);
        float boxW = 900, edge = 40, inner = w - 40; // the box stops just before the character
        float cx = right ? -(960 - (1920 - inner - edge)) - boxW / 2f - edge : (inner + edge) - 960 + boxW / 2f + edge;
        boxR.sizeDelta = new Vector2(boxW, 260); boxR.anchoredPosition = new Vector2(cx, 40);
        bodyR.offsetMin = new Vector2(44, 50);
        // tail on the side facing the character, low on the bubble
        var tr = tail.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(right ? 1 : 0, 0); tr.pivot = new Vector2(0, .5f);
        tr.sizeDelta = new Vector2(64, 64); tr.anchoredPosition = new Vector2(right ? -7 : 7, 80); tr.localScale = new Vector3(right ? 1 : -1, 1, 1);
        PlaceArt(now);
    }
    void PlaceArt(float now)
    {
        if (!art.enabled) return;
        var r = art.rectTransform; float h = r.sizeDelta.y;
        float k = Mathf.Clamp01((now - artInT) / .35f), e = 1f - (1f - k) * (1f - k) * (1f - k);
        float slide = (1f - e) * (r.sizeDelta.x * .6f) * (artRight ? 1 : -1);
        r.anchoredPosition = new Vector2((artRight ? -20 : 20) + slide, -(h - ArtVisibleH) - 6); // flat cut edge sits just below the screen edge
        var c = art.color; c.a = e; art.color = c;
        float q = (now - artSwapT) / .3f; float pop = q < 1f ? 1f + .04f * Mathf.Sin(q * Mathf.PI) : 1f;
        r.localScale = Vector3.one * pop;
    }

    void Next() { if (typing) { typing = false; bodyText.text = full; } else ShowNext(); }
    void Update()
    {
        if (root != null && root.activeSelf) PlaceArt(Time.unscaledTime);
        if (!typing || root == null || !root.activeSelf) return;
        int n = Mathf.Min(full.Length, (int)((Time.unscaledTime - t0) * 45f));
        bodyText.text = full.Substring(0, n); if (n >= full.Length) typing = false;
        hint.enabled = !typing;
    }

    Image Img(string n, Transform p, Color c) { var g = new GameObject(n, typeof(RectTransform), typeof(Image)); g.transform.SetParent(p, false); var i = g.GetComponent<Image>(); i.color = c; return i; }
    Text Txt(Transform p, int size, Color c, TextAnchor a) { var g = new GameObject("Text", typeof(RectTransform), typeof(Text)); g.transform.SetParent(p, false); var t = g.GetComponent<Text>(); t.font = font; t.fontSize = size; t.color = c; t.alignment = a; t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; return t; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
}
