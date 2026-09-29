using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Small helpers that build the rounded cream/brown UI in code (used by every Kente screen and the LoadingScreen).
// Positions use the same numbers as the HTML prototype: x,y from the parent's TOP-LEFT, y going down.
public static class UI
{
    static Sprite round, circle, soft, diamonds;
    static readonly Color Ink = new Color32(61, 36, 22, 255);

    public static Sprite Round { get { if (round == null) round = RoundRect(160, 64f, 0f); return round; } }
    static readonly System.Collections.Generic.Dictionary<int, Sprite> rings = new System.Collections.Generic.Dictionary<int, Sprite>();
    // Ring sprite whose line is 'px' thick once sliced at corner radius 'radius'.
    public static Sprite Ring(float px, float radius)
    {
        int key = Mathf.RoundToInt(px * 64f / Mathf.Max(1, radius) * 10f);
        if (!rings.TryGetValue(key, out var sp) || sp == null) { sp = RoundRect(160, 64f, key / 10f); rings[key] = sp; }
        return sp;
    }
    public static Sprite Circle { get { if (circle == null) circle = Tex(128, (x, y) => Mathf.Clamp01(64f - Vector2.Distance(new Vector2(x, y), new Vector2(64, 64))), Vector4.zero); return circle; } }
    public static Sprite Soft { get { if (soft == null) soft = Tex(128, (x, y) => { float a = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) / 64f); return a * a; }, Vector4.zero); return soft; } }

    public static RectTransform Node(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    public static RectTransform TL(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    public static RectTransform Center(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); return r;
    }
    public static RectTransform Stretch(RectTransform r, float inset = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset); return r;
    }
    public static Image Img(string n, Transform parent, Sprite s, Color c)
    {
        var img = Node(n, parent).gameObject.AddComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false; return img;
    }
    public static Image Pic(Transform parent, Sprite s)
    {
        var img = Img("Picture", parent, s, Color.white); img.preserveAspect = true; img.enabled = s != null; return img;
    }
    public static Image Panel(string n, Transform parent, Color c, float radius, Color? border, float borderPx = 4)
    {
        var img = Img(n, parent, Round, c);
        img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 64f / Mathf.Max(1, radius);
        if (border.HasValue)
        {
            var b = Img("Border", img.transform, Ring(borderPx, radius), border.Value);
            b.type = Image.Type.Sliced; b.pixelsPerUnitMultiplier = 64f / Mathf.Max(1, radius);
            Stretch(b.rectTransform);
        }
        return img;
    }
    public static Text Label(Transform parent, string s, int size, Color c, TextAnchor a, Font f, bool bold = true)
    {
        var t = Node("Text", parent).gameObject.AddComponent<Text>();
        t.font = f; t.text = s; t.fontSize = size; t.color = c; t.alignment = a; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; t.supportRichText = true;
        return t;
    }
    public static Button Btn(Transform parent, string label, Color bg, Color fg, Color? shadow, UnityAction onClick, int fontSize, Font f, Color? border = null, float radius = 20)
    {
        var img = Panel("Button " + label, parent, bg, radius, border, 3);
        img.raycastTarget = true;
        var b = img.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = img;
        var cb = b.colors; cb.pressedColor = new Color(.85f, .85f, .85f, 1); b.colors = cb;
        if (onClick != null) b.onClick.AddListener(onClick);
        if (shadow.HasValue) { var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = shadow.Value; sh.effectDistance = new Vector2(0, -5); }
        var t = Label(img.transform, label, fontSize, fg, TextAnchor.MiddleCenter, f); Stretch(t.rectTransform);
        return b;
    }
    // Thin gold line just inside a dialogue name tab (used by every character's dialogue box).
    public static void NameTabLine(RectTransform tab, float radius, float inset)
    {
        var g = Img("GoldLine", tab, Ring(2f, radius - inset), new Color32(232, 163, 61, 230));
        g.type = Image.Type.Sliced; g.pixelsPerUnitMultiplier = 64f / Mathf.Max(1, radius - inset); Stretch(g.rectTransform, inset);
    }
    // Wooden band with gold kente diamonds wrapped over the rounded top of a panel.
    public static void KenteBand(RectTransform panel, float radius, float height = 42)
    {
        var clip = TL(Node("KenteBand", panel), 0, 0, panel.sizeDelta.x, height);
        clip.gameObject.AddComponent<RectMask2D>();
        var wood = Img("Wood", clip, Round, new Color32(168, 98, 47, 255)); wood.type = Image.Type.Sliced; wood.pixelsPerUnitMultiplier = 64f / radius;
        TL(wood.rectTransform, 0, 0, panel.sizeDelta.x, panel.sizeDelta.y);
        var hi = Img("Highlight", clip, null, new Color(1f, .9f, .75f, .3f)); TL(hi.rectTransform, radius, 3, panel.sizeDelta.x - radius * 2, 2);
        var d = Img("Diamonds", clip, Diamonds, Color.white); d.type = Image.Type.Tiled; d.pixelsPerUnitMultiplier = 1f;
        float dw = Mathf.Floor((panel.sizeDelta.x - radius * 1.2f) / 18f) * 18f; // whole diamonds only, centred
        TL(d.rectTransform, (panel.sizeDelta.x - dw) / 2f, (height - 4 - 18) / 2f, dw, 18);
        var line = Img("Edge", clip, null, Ink); TL(line.rectTransform, 0, height - 4, panel.sizeDelta.x, 4);
        var ring = Img("Border", panel, Ring(4, radius), Ink); ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 64f / radius; Stretch(ring.rectTransform);
    }
    static Sprite Diamonds
    {
        get
        {
            if (diamonds != null && diamonds.texture != null) return diamonds;
            int s = 18; var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var gold = new Color32(232, 163, 61, 255);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                { float dd = Mathf.Abs(x + .5f - 9) + Mathf.Abs(y + .5f - 9); t.SetPixel(x, y, dd > 9 ? (Color)gold : new Color(0, 0, 0, 0)); }
            t.Apply();
            diamonds = Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            return diamonds;
        }
    }
    static float Sd(float x, float y, float s, float r)
    {
        float h = s / 2f, qx = Mathf.Abs(x - h) - (h - r), qy = Mathf.Abs(y - h) - (h - r);
        float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
    }
    static Sprite RoundRect(int s, float r, float ringW) => Tex(s, (x, y) => { float d = Sd(x, y, s, r); float a = Mathf.Clamp01(.5f - d); if (ringW > 0) a *= Mathf.Clamp01(.5f + d + ringW); return a; }, new Vector4(r, r, r, r));
    public static Sprite Tex(int s, Func<float, float, float> alpha, Vector4 border)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) px[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(x + .5f, y + .5f)) * 255f));
        t.SetPixels32(px); t.Apply(false, true);
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
    }
    public static void Alpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    public static Color WithA(Color c, float a) { c.a = a; return c; }
    public static float EaseOut(float k) => 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);
}
