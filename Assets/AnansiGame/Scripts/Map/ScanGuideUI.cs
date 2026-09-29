using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The "find the map" screen shown over the camera (used by ScanFlow). Portrait + landscape, safe area aware.
//  - frame centred on the screen, shaped like the printed map, with gold corner brackets
//  - a faint picture of YOUR map inside the frame (Resources/ScanGuide/map_preview) so the player knows what to line up
//  - a thin, soft scan line moving down
//  - a quiet instruction card at the bottom: small map thumbnail + title + one line of help. No rim, no button look.
//  - "found" beat: corners snap in and turn green, "Map found!", then it fades
public class ScanGuideUI : MonoBehaviour
{
    public enum Mode { First, Lost, Found }
    public Font font;
    [Tooltip("Width ÷ height of the printed map.")]
    public float mapAspect = 1.415f;
    [Range(0f, 1f)] public float ghostOpacity = .3f;

    static readonly Color Gold = new Color32(232, 163, 61, 255), TitleGold = new Color32(246, 201, 122, 255), Cream = new Color32(240, 226, 202, 255),
        DeepBrown = new Color32(34, 16, 7, 255), FoundGreen = new Color32(140, 196, 104, 255);

    Canvas canvas; CanvasGroup group; RectTransform root, frame, sweep, ghost, scanMask, card, thumb, textBox;
    Image topFade, bottomFade, sweepImg, ghostImg, thumbImg, thumbShine; Text title, body; CanvasGroup bodyGroup;
    readonly Image[] corners = new Image[4], cornerShadows = new Image[4];
    string shownBody = "", wantBody = ""; float bodySwapT = -9f; Mode lastMode = (Mode)(-1); float modeT;
    Sprite mapSprite;

    void Awake() { Build(); }

    public void Tick(bool show, Mode mode, float searching)
    {
        group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime / (show ? .3f : .25f));
        canvas.enabled = group.alpha > .001f;
        if (!canvas.enabled) return;
        if (mode != lastMode) { lastMode = mode; modeT = Time.unscaledTime; }
        SetText(mode, searching);
        Layout();
        Animate(mode);
    }

    void SetText(Mode mode, float searching)
    {
        title.text = mode == Mode.Found ? "Map found!" : mode == Mode.Lost ? "The village is hiding" : "Find the village map";
        wantBody = mode == Mode.Found ? "Bringing the village to life…"
                 : searching > 10f ? "Step back so the whole map fits in the frame, in good light."
                 : mode == Mode.Lost ? "Point your camera back at your paper map."
                 : "Point your camera at your paper map.";
        if (shownBody == "") { shownBody = wantBody; body.text = wantBody; }
        else if (wantBody != shownBody && bodySwapT < 0) bodySwapT = Time.unscaledTime;
        if (bodySwapT > 0)
        {
            float e = (Time.unscaledTime - bodySwapT) / .4f;
            if (e < .5f) bodyGroup.alpha = 1f - e * 2f;
            else { if (body.text != wantBody) { shownBody = wantBody; body.text = wantBody; } bodyGroup.alpha = Mathf.Clamp01((e - .5f) * 2f); }
            if (e >= 1f) { bodySwapT = -9f; bodyGroup.alpha = 1f; }
        }
    }

    // ---------- layout ----------
    void Layout()
    {
        float W = root.rect.width, H = root.rect.height; if (W <= 0 || H <= 0) return;
        var sa = Screen.safeArea; float kx = W / Mathf.Max(1, Screen.width), ky = H / Mathf.Max(1, Screen.height);
        float sl = sa.xMin * kx, sr = (Screen.width - sa.xMax) * kx, sb = sa.yMin * ky, st = (Screen.height - sa.yMax) * ky;
        bool portrait = H > W;
        float cx = sl + (W - sl - sr) / 2f;

        // card: thumbnail left, text right; everything vertically centred inside
        float pad = portrait ? 30 : 26, thH = portrait ? 104 : 88, thW = thH * mapAspect, gap = portrait ? 28 : 24;
        float cw = Mathf.Min(W - sl - sr - (portrait ? 56 : 120), portrait ? 900 : 860);
        float textW = cw - pad * 2 - thW - gap;
        title.fontSize = portrait ? 42 : 36; body.fontSize = portrait ? 30 : 26;
        float tH = title.cachedTextGenerator.GetPreferredHeight(title.text, title.GetGenerationSettings(new Vector2(textW, 0))) / title.pixelsPerUnit;
        float bH = body.cachedTextGenerator.GetPreferredHeight(wantBody, body.GetGenerationSettings(new Vector2(textW, 0))) / body.pixelsPerUnit;
        float lineGap = portrait ? 8 : 6, textH = tH + lineGap + bH;
        float ch = Mathf.Max(thH, textH) + pad * 2;
        float bottomM = sb + (portrait ? 56 : 28);
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0, 0);
        card.sizeDelta = new Vector2(cw, ch); card.anchoredPosition = new Vector2(cx - cw / 2f, bottomM);
        thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(0, .5f);
        thumb.sizeDelta = new Vector2(thW, thH); thumb.anchoredPosition = new Vector2(pad, 0);
        textBox.anchorMin = textBox.anchorMax = textBox.pivot = new Vector2(0, .5f);
        textBox.sizeDelta = new Vector2(textW, textH); textBox.anchoredPosition = new Vector2(pad + thW + gap, 0);
        var tr = title.rectTransform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0, 1); tr.anchoredPosition = Vector2.zero; tr.sizeDelta = new Vector2(0, tH);
        var br = body.rectTransform; br.anchorMin = new Vector2(0, 0); br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(0, 0); br.anchoredPosition = Vector2.zero; br.sizeDelta = new Vector2(0, bH);

        // frame: centred on the safe screen, as big as fits between the top and the card
        float cy = sb + (H - sb - st) / 2f;
        float cardTop = bottomM + ch + (portrait ? 48 : 24), topM = st + (portrait ? 64 : 28), side = portrait ? 44 : 90;
        float halfH = Mathf.Max(60f, Mathf.Min(cy - cardTop, H - topM - cy));
        float fw = Mathf.Min(W - sl - sr - side * 2, halfH * 2f * mapAspect), fh = fw / mapAspect;
        frame.anchorMin = frame.anchorMax = Vector2.zero; frame.pivot = new Vector2(.5f, .5f);
        frame.sizeDelta = new Vector2(fw, fh); frame.anchoredPosition = new Vector2(cx, cy);
        float len = Mathf.Clamp(Mathf.Min(fw, fh) * .16f, 56, 120);
        for (int i = 0; i < 4; i++) corners[i].rectTransform.sizeDelta = cornerShadows[i].rectTransform.sizeDelta = Vector2.one * len;
        ghost.offsetMin = new Vector2(fw, fh) * .13f; ghost.offsetMax = -new Vector2(fw, fh) * .13f;
        float m = Mathf.Clamp(Mathf.Min(fw, fh) * .045f, 12f, 22f); // small gap: the line nearly reaches the frame edges but never touches them
        scanMask.offsetMin = new Vector2(m, m); scanMask.offsetMax = new Vector2(-m, -m);
        sweep.sizeDelta = new Vector2(fw - m * 2f, Mathf.Max(36f, fh * .16f));
        topFade.rectTransform.sizeDelta = new Vector2(0, H * .22f); bottomFade.rectTransform.sizeDelta = new Vector2(0, bottomM + ch + H * .12f);
    }

    // ---------- animation ----------
    void Animate(Mode mode)
    {
        float t = Time.unscaledTime, found = mode == Mode.Found ? Mathf.Clamp01((t - modeT) / .35f) : 0f;
        float breathe = .5f + .5f * Mathf.Sin(t * 2.2f);
        float push = mode == Mode.Found ? -16f * EaseOut(found) : 4f + 6f * breathe;
        Color cc = mode == Mode.Found ? Color.Lerp(TitleGold, FoundGreen, found) : Color.Lerp(Gold, TitleGold, breathe);
        for (int i = 0; i < 4; i++)
        {
            Vector2 dir = new Vector2(i % 2 == 0 ? -1 : 1, i < 2 ? -1 : 1);
            corners[i].rectTransform.anchoredPosition = dir * push;
            cornerShadows[i].rectTransform.anchoredPosition = dir * push + new Vector2(0, -3);
            corners[i].color = cc;
        }
        // ghost of the map: faint, breathing slightly; gone when found
        ghostImg.color = new Color(1, 1, 1, mode == Mode.Found ? ghostOpacity * (1 - found) : ghostOpacity * (.8f + .2f * breathe));
        // thin scan line: top -> bottom, fades in and out at the ends
        float ph = Mathf.PingPong(t * .42f, 1f), k = EaseInOut(ph), fh = scanMask.rect.height; bool down = Mathf.Repeat(t * .42f, 2f) < 1f;
        // the line travels down, then back up; its glow always trails behind it
        float edge = Mathf.Lerp(fh / 2f, -fh / 2f, k), bh = sweep.rect.height;
        sweep.localScale = new Vector3(1, down ? 1 : -1, 1);
        sweep.anchoredPosition = new Vector2(0, edge + (down ? bh / 2f : -bh / 2f));
        sweepImg.color = new Color(1, 1, 1, mode == Mode.Found ? 0f : .9f);
        // thumbnail shine sweeps across now and then
        float sk = (t % 3.2f) / .9f; var sr = thumbShine.rectTransform; float tw = thumb.rect.width;
        sr.anchoredPosition = new Vector2(Mathf.Lerp(-tw * .6f, tw * 1.1f, Mathf.Clamp01(sk)), 0);
        thumbShine.color = new Color(1, 1, 1, sk <= 1f ? .35f * Mathf.Sin(Mathf.Clamp01(sk) * Mathf.PI) : 0f);
        title.color = mode == Mode.Found ? Color.Lerp(TitleGold, FoundGreen, found) : TitleGold;
    }

    // ---------- build ----------
    void Build()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var tex = Resources.Load<Texture2D>("ScanGuide/map_preview");
        if (tex != null) mapSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100); // aspect stays mapAspect (Unity may resize the texture)
        var st = Resources.Load<Texture2D>("ScanGuide/map_symbol");
        Sprite symbol = st ? Sprite.Create(st, new Rect(0, 0, st.width, st.height), new Vector2(.5f, .5f), 100) : null;

        var cgo = new GameObject("ScanGuideCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 650;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        group = cgo.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;
        root = (RectTransform)cgo.transform;

        topFade = Img("TopFade", root, Fade(true), DeepBrown); Anchor(topFade.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1));
        bottomFade = Img("BottomFade", root, Fade(false), DeepBrown); Anchor(bottomFade.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0));

        frame = Node("Frame", root);
        ghostImg = Img("MapSymbol", frame, symbol, new Color(1, 1, 1, ghostOpacity)); ghost = ghostImg.rectTransform; Stretch(ghost); ghost.gameObject.SetActive(false); // map symbol removed from the scan box
        var mask = Node("ScanMask", frame); Stretch(mask); mask.gameObject.AddComponent<RectMask2D>(); scanMask = mask;
        sweepImg = Img("ScanLine", mask, LineSprite(), Color.white); sweep = sweepImg.rectTransform;
        sweep.anchorMin = sweep.anchorMax = new Vector2(.5f, .5f); sweep.pivot = new Vector2(.5f, .5f);
        sweepImg.type = Image.Type.Sliced;
        var cs = CornerSprite(false); var css = CornerSprite(true);
        for (int i = 0; i < 4; i++)
        {
            Vector2 a = new Vector2(i % 2, i / 2);
            var s = Img("CornerShadow" + i, frame, css, new Color(0, 0, 0, .4f)); var c = Img("Corner" + i, frame, cs, Gold);
            foreach (var im in new[] { s, c })
            {
                var r = im.rectTransform; r.anchorMin = r.anchorMax = a; r.pivot = Vector2.zero; // bend at the pivot, mirrored INTO the frame
                r.localScale = new Vector3(a.x > 0 ? -1 : 1, a.y > 0 ? -1 : 1, 1);
            }
            cornerShadows[i] = s; corners[i] = c;
        }

        // instruction card: soft, see-through, no rim
        var cImg = Img("GuideCard", root, Rounded(), new Color(DeepBrown.r, DeepBrown.g, DeepBrown.b, .78f)); cImg.type = Image.Type.Sliced; card = cImg.rectTransform;
        thumb = Node("MapThumb", card);
        var paper = Img("Paper", thumb, Rounded(), new Color32(246, 235, 217, 255)); paper.type = Image.Type.Sliced; paper.pixelsPerUnitMultiplier = 4f; Stretch(paper.rectTransform); paper.rectTransform.offsetMin = -Vector2.one * 4; paper.rectTransform.offsetMax = Vector2.one * 4;
        var tm = Node("ThumbMask", thumb); Stretch(tm); tm.gameObject.AddComponent<RectMask2D>();
        thumbImg = Img("Map", tm, mapSprite, Color.white); Stretch(thumbImg.rectTransform);
        thumbShine = Img("Shine", tm, ShineSprite(), Color.white); var shr = thumbShine.rectTransform; shr.anchorMin = new Vector2(0, 0); shr.anchorMax = new Vector2(0, 1); shr.pivot = new Vector2(.5f, .5f); shr.sizeDelta = new Vector2(40, 0);
        textBox = Node("Text", card);
        title = Txt(textBox, 36, TitleGold, FontStyle.Bold); var tsh = title.gameObject.AddComponent<Shadow>(); tsh.effectColor = new Color(0, 0, 0, .6f); tsh.effectDistance = new Vector2(0, -2);
        body = Txt(textBox, 26, Cream, FontStyle.Normal);
        bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
    }

    // ---------- helpers ----------
    static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
    static float EaseInOut(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }
    static RectTransform Node(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return (RectTransform)g.transform; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Anchor(RectTransform r, Vector2 min, Vector2 max, Vector2 pivot) { r.anchorMin = min; r.anchorMax = max; r.pivot = pivot; r.anchoredPosition = Vector2.zero; }
    static Image Img(string n, Transform p, Sprite s, Color c) { var i = Node(n, p).gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false; return i; }
    Text Txt(Transform p, int size, Color c, FontStyle st)
    {
        var t = Node("Text", p).gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.color = c; t.fontStyle = st; t.alignment = TextAnchor.UpperLeft;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; t.lineSpacing = 1f; return t;
    }

    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    delegate Color Pixel(float x, float y);
    static Sprite Make(string key, int w, int h, Pixel f, Vector4 border = default, Vector2? pivot = null)
    {
        if (cache.TryGetValue(key, out var s) && s) return s;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x + .5f, y + .5f);
        t.SetPixels(px); t.Apply();
        s = Sprite.Create(t, new Rect(0, 0, w, h), pivot ?? new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        cache[key] = s; return s;
    }
    static float Seg(float x, float y, float ax, float ay, float bx, float by) { float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay; float k = Mathf.Clamp01((px * vx + py * vy) / (vx * vx + vy * vy)); float dx = px - vx * k, dy = py - vy * k; return Mathf.Sqrt(dx * dx + dy * dy); }
    static Color A(float a) => new Color(1, 1, 1, Mathf.Clamp01(a));

    static Sprite CornerSprite(bool shadow) => Make(shadow ? "cornerS" : "corner", 128, 128, (x, y) =>
    {
        const float E = 10f, R = 26f, C = E + R, END = 116f;
        float d = Mathf.Min(Seg(x, y, E, C, E, END), Seg(x, y, C, E, END, E));
        if (x <= C && y <= C) d = Mathf.Min(d, Mathf.Abs(Mathf.Sqrt((x - C) * (x - C) + (y - C) * (y - C)) - R));
        float w = shadow ? 8f : 5.5f;
        return A(shadow ? (w - d) / 5f : w - d);
    }, default, new Vector2(0, 0));
    // thin bright line with a soft glow either side, fading out at the ends
    // the scan sweep: bright line at the bottom edge, soft gold glow trailing above it
    // thin line at the bottom edge, gentle glow trailing above it; both fade out towards the left/right ends
    static Sprite LineSprite() => Make("sweepBand3", 96, 128, (x, y) =>
    {
        float ends = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(x, 96f - x) / 10f)); // short soft tips, so the line reads almost full width
        float line = Mathf.Clamp01(2.2f - y) * .85f, glow = Mathf.Pow(1f - y / 128f, 2.6f) * .22f;
        return new Color(1f, .88f, .6f, Mathf.Max(line, glow) * ends);
    }, new Vector4(30, 0, 30, 0));
    static Sprite Fade(bool top) => Make(top ? "fadeT" : "fadeB", 4, 128, (x, y) => { float v = y / 128f; if (!top) v = 1f - v; return A(Mathf.Pow(v, 1.6f) * .6f); });
    static Sprite ShineSprite() => Make("shine", 32, 8, (x, y) => A(Mathf.Pow(1f - Mathf.Abs(x - 16f) / 16f, 2f)));
    static Sprite Rounded() => Make("round", 96, 96, (x, y) =>
    {
        const float R = 32f; float dx = Mathf.Max(Mathf.Abs(x - 48f) - (48f - R), 0), dy = Mathf.Max(Mathf.Abs(y - 48f) - (48f - R), 0);
        return A(R - Mathf.Sqrt(dx * dx + dy * dy));
    }, new Vector4(34, 34, 34, 34));
}
