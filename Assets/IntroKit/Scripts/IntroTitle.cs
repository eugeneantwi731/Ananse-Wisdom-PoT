using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using M = IntroMath;

[System.Serializable] public class TitlePiece { public string name; public float x, y, w, h, gx0, gx1, cx, cy; }
[System.Serializable] public class TitleLayout { public TitlePiece[] pieces; }

// Title + spider, built as UI from the baked images. Positions are in "logo units" (same as the web prototype).
public class IntroTitle
{
    const float S = 0.1025f, THREAD_X = 760f, THREAD_ATTACH = 2116f;
    const float ABD_Y = 2330f, ABD_R = 215f, HEAD_Y = 2700f, HEAD_R = 135f;
    static readonly float[,] LEGS = { { 70, -95, 430, -560, 600, -880, -1 }, { 105, -40, 560, -290, 720, -640, -1 }, { 105, 40, 560, 300, 700, 700, 1 }, { 70, 100, 390, 560, 520, 980, 1 } };

    readonly RectTransform root, spider, thread, body, scaleG, abdomen, eyes;
    readonly Dictionary<string, TitlePiece> data = new Dictionary<string, TitlePiece>();
    readonly Dictionary<string, Image> img = new Dictionary<string, Image>();
    readonly Image[] legs = new Image[32];
    float ft;
    readonly Color cream = M.Hex("#F3E6CF");

    public IntroTitle(RectTransform stage, Sprite capsule, Sprite circle, Sprite diamond)
    {
        root = NewRect("Title", stage); root.anchorMin = root.anchorMax = new Vector2(0, 1); root.pivot = new Vector2(0, 1);
        var layout = JsonUtility.FromJson<TitleLayout>(Resources.Load<TextAsset>("Intro/title_layout").text);
        foreach (var p in layout.pieces)
        {
            data[p.name] = p;
            var im = NewImage(p.name, root, IntroTex.Load("Intro/Title/" + p.name), Color.white);
            var rt = im.rectTransform;
            rt.sizeDelta = new Vector2(p.w, p.h);
            rt.pivot = new Vector2((p.cx - p.x) / p.w, 1 - (p.cy - p.y) / p.h);
            rt.anchoredPosition = new Vector2(p.cx, -p.cy);
            if (p.name.StartsWith("A")) { im.type = Image.Type.Filled; im.fillMethod = Image.FillMethod.Horizontal; im.fillOrigin = 0; }
            img[p.name] = im;
        }

        spider = NewRect("Spider", root);
        thread = NewImage("Thread", spider, IntroTex.ToSprite(IntroTex.Thread()), M.A(cream, 0.9f)).rectTransform; thread.pivot = new Vector2(0.5f, 1);
        body = NewRect("Body", spider);
        scaleG = NewRect("ScaleGroup", body); scaleG.anchoredPosition = new Vector2(0, -(ABD_Y - ABD_R));
        for (int i = 0; i < 32; i++) { legs[i] = NewImage("Leg", scaleG, capsule, cream); legs[i].type = Image.Type.Sliced; }
        var spin = NewImage("Spinneret", scaleG, capsule, cream); spin.type = Image.Type.Sliced; spin.pixelsPerUnitMultiplier = 32f / 90f;
        Place(spin.rectTransform, 0, 2575, 140, 90); spin.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
        abdomen = NewImage("Abdomen", scaleG, circle, cream).rectTransform; Place(abdomen, 0, ABD_Y, ABD_R * 2, ABD_R * 2.16f);
        Place(NewImage("Head", scaleG, circle, cream).rectTransform, 0, HEAD_Y, HEAD_R * 2, HEAD_R * 2);
        Place(NewImage("Diamond", scaleG, diamond, M.A(M.Hex("#EDB649"), 0.9f)).rectTransform, 0, ABD_Y - 20, 140, 180);
        eyes = NewRect("Eyes", scaleG); eyes.anchoredPosition = Loc(0, HEAD_Y + 20);
        var eyeCol = M.Hex("#3D2415");
        float[,] e = { { -52, 20, 34 }, { 52, 20, 34 }, { -20, -50, 16 }, { 20, -50, 16 } };
        for (int i = 0; i < 4; i++) { var r = NewImage("Eye", eyes, circle, eyeCol).rectTransform; r.anchoredPosition = new Vector2(e[i, 0], -(e[i, 1] - 20)); r.sizeDelta = Vector2.one * e[i, 2] * 2; }
        foreach (int side in new[] { -1, 1 })
        {
            Seg(NewImage("Fang", scaleG, capsule, cream), side * 40, HEAD_Y + 115, side * 52.5f, HEAD_Y + 170, 30);
            Seg(NewImage("Fang", scaleG, capsule, cream), side * 52.5f, HEAD_Y + 170, side * 30, HEAD_Y + 215, 30);
        }
    }

    float threadW = 28f;

    // uiScale = screen pixels per stage unit, so the thread always covers ~2.5 real pixels (no shimmer)
    public void SetScreenScale(float uiScale, bool land)
    {
        float sc = S * (land ? 0.85f : 1f);
        threadW = Mathf.Max(28f, 2.6f / Mathf.Max(0.0001f, sc * uiScale));
    }

    public void Layout(bool land)
    {
        float scale = land ? 0.85f : 1f, sc = S * scale, top = land ? 150 : 200;
        float x0 = land ? 1360f - 9562f * sc / 2f : (1080f - 9562f * sc) / 2f;
        root.anchoredPosition = new Vector2(x0, -top);
        root.localScale = Vector3.one * sc;
        ft = -top / sc;
        spider.anchoredPosition = new Vector2(THREAD_X, -ft);
    }

    public void Update(float T)
    {
        // "Anansi": brush wipe + bouncy letters
        float wipe = M.Draw(T, 0, 9000, 0, 1.5f);
        for (int i = 0; i < 6; i++)
        {
            var p = data["A" + i]; var im = img["A" + i];
            float st = 0.05f + i * 0.2f;
            float sc = M.Pop(T, 1.45f, 1, st, 0.7f), dy = M.Pop(T, -420, 0, st, 0.7f);
            im.fillAmount = M.Cl((wipe + 350 - p.x) / p.w);
            im.color = M.A(Color.white, T < 0 ? 0 : 1);
            im.rectTransform.anchoredPosition = new Vector2(p.cx, -(p.cy + dy));
            im.rectTransform.localScale = Vector3.one * sc;
        }
        // "and the Pot of Wisdom"
        float small = M.Enter(T, 0, 1, 1.6f, 0.6f);
        var at = data["andthe"]; img["andthe"].color = M.A(Color.white, small);
        img["andthe"].rectTransform.anchoredPosition = new Vector2(at.cx, -(at.cy + (1 - small) * 180));
        Word("pot", T, 1.6f + 0.35f); Word("of", T, 1.6f + 0.55f); Word("wisdom", T, 1.6f + 0.75f);
        // divider + tagline
        img["divider"].rectTransform.localScale = new Vector3(M.Draw(T, 0, 1, 3.0f, 0.8f), 1, 1);
        float tag = M.Enter(T, 0, 1, 3.35f, 0.9f);
        img["tagline_clean"].color = M.A(Color.white, tag);
        img["tagline_clean"].rectTransform.localScale = new Vector3(1 + (1 - tag) * 0.06f, 1, 1);
        UpdateSpider(T);
    }

    void Word(string n, float T, float st)
    {
        img[n].color = M.A(Color.white, M.Enter(T, 0, 1, st, 0.3f));
        img[n].rectTransform.localScale = Vector3.one * M.Pop(T, 0.55f, 1, st, 0.6f);
    }

    void UpdateSpider(float T)
    {
        const float t0 = 3.9f, dur = 2.3f;
        float start = ft - 4800f, pr = M.Draw(T, 0, 1, t0, dur), y = start * (1 - pr), after = T - (t0 + dur);
        if (after > 0) y += 90 * Mathf.Exp(-3.5f * after) * Mathf.Sin(after * 9);
        float sway = Mathf.Sin(T * 1.6f) * 0.25f * (after > 0 ? Mathf.Exp(-0.6f * after) + 0.3f : 1);
        spider.localRotation = Quaternion.Euler(0, 0, -sway);

        float threadEnd = THREAD_ATTACH + y;
        thread.anchoredPosition = new Vector2(0, 50);
        thread.sizeDelta = new Vector2(threadW, Mathf.Max(0, threadEnd - (ft - 50)));
        body.anchoredPosition = new Vector2(0, -(y - ft));

        float land = t0 + dur;
        float falling = T < land ? M.Enter(T, 0, 1, t0, 0.4f) : Mathf.Max(0, 1 - after * 3);
        float tuck = M.Enter(T, 0, 16, t0, 0.5f) - M.Pop(T, 0, 26, land - 0.1f, 0.6f);
        float settle = after > 0 ? Mathf.Exp(-3.5f * after) * Mathf.Cos(after * 9) : 0;
        float waveOn = M.BumpLen(T, land + 0.7f, 1.7f), breathe = 1 + Mathf.Sin(T * 3) * 0.015f;
        float blink = Mathf.Max(M.BumpLen(T, land + 0.35f, 0.18f), Mathf.Max(M.BumpLen(T, land + 2.1f, 0.18f), M.BumpLen(T, t0 + 1.2f, 0.18f)));
        float stretch = 1 + settle * 0.07f;
        scaleG.localScale = new Vector3(1 / Mathf.Sqrt(stretch), stretch, 1);
        abdomen.localScale = Vector3.one * breathe;
        eyes.localScale = new Vector3(1, 1 - blink * 0.9f, 1);

        int n = 0;
        foreach (int side in new[] { 1, -1 })
            for (int i = 0; i < 4; i++)
            {
                float phase = i * 1.4f + (side > 0 ? 0 : Mathf.PI), tk = LEGS[i, 6];
                float hipA = tk * tuck + Mathf.Sin(T * 8 + phase) * 9 * falling + tk * settle * 6;
                float kneeA = tk * tuck * 0.9f + Mathf.Sin(T * 8 + phase + 1) * 12 * falling;
                if (i == 3) { hipA += waveOn * (10 + Mathf.Sin(T * 8 + (side > 0 ? 0 : 0.6f)) * 5); kneeA += waveOn * 6; }
                Vector2 hip = new Vector2(LEGS[i, 0], LEGS[i, 1]);
                Vector2 s1 = Rot(new Vector2(LEGS[i, 2] - LEGS[i, 0], LEGS[i, 3] - LEGS[i, 1]), hipA);
                Vector2 s2 = Rot(new Vector2(LEGS[i, 4] - LEGS[i, 2], LEGS[i, 5] - LEGS[i, 3]), hipA + kneeA);
                Vector2 K = hip + s1, F = K + s2;
                Seg(legs[n++], side * hip.x, HEAD_Y + hip.y, side * K.x, HEAD_Y + K.y, 52);
                Seg(legs[n++], side * K.x, HEAD_Y + K.y, side * F.x, HEAD_Y + F.y, 36);
            }
    }

    // ---------- helpers ----------
    // Logo coords (y down, relative to the spider's scale pivot) -> local UI position
    static Vector2 Loc(float x, float y) { return new Vector2(x, -(y - (ABD_Y - ABD_R))); }
    static Vector2 Rot(Vector2 v, float deg) { float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }

    static void Place(RectTransform r, float x, float y, float w, float h) { r.anchoredPosition = Loc(x, y); r.sizeDelta = new Vector2(w, h); }

    // A round-capped stroke between two logo-space points
    static void Seg(Image im, float x1, float y1, float x2, float y2, float th)
    {
        Vector2 a = Loc(x1, y1), b = Loc(x2, y2), d = b - a;
        float len = d.magnitude;
        var r = im.rectTransform;
        im.pixelsPerUnitMultiplier = 32f / th;
        r.sizeDelta = new Vector2(len + th, th);
        r.pivot = new Vector2((th * 0.5f) / (len + th), 0.5f);
        r.anchoredPosition = a;
        r.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.sizeDelta = Vector2.zero;
        return r;
    }

    public static Image NewImage(string name, Transform parent, Sprite s, Color c)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var im = g.GetComponent<Image>(); im.sprite = s; im.color = c; im.raycastTarget = false;
        return im;
    }
}
