using System;
using UnityEngine;
using UnityEngine.UI;

// Reusable loading screen for every mini-game (weaving, farmer, carver...).
//   var ls = LoadingScreen.Show(parentRect, "The Kente Loom", hints, font);
//   ls.SetProgress(0.5f, "Winding the threads…");
//   ls.SetReady(() => { ls.Hide(); StartGame(); });
public class LoadingScreen : MonoBehaviour
{
    public float hintSeconds = 4.2f;
    static readonly Color Cream = new Color32(246, 235, 217, 255), Ink = new Color32(61, 36, 22, 255), Brown = new Color32(107, 62, 38, 255),
        Green = new Color32(79, 124, 58, 255), Gold = new Color32(232, 163, 61, 255), GoldDark = new Color32(183, 122, 34, 255),
        Hint = new Color32(154, 116, 86, 255), Box = new Color32(239, 224, 198, 255), Track = new Color32(42, 26, 16, 255);

    RectTransform fill, stripes; Text status, pct, hintText; CanvasGroup hintGroup; GameObject beginBtn;
    string[] hints; float t0, shown, target; Action onBegin;

    public static LoadingScreen Show(RectTransform parent, string title, string[] hints, Font font = null)
    {
        var go = new GameObject("LoadingScreen", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var ls = go.AddComponent<LoadingScreen>();
        ls.Build(title, hints, font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        return ls;
    }

    public void SetProgress(float p, string label = null) { target = Mathf.Clamp01(p); if (label != null) status.text = label; }
    public void SetReady(Action begin, string label = "Ready") { onBegin = begin; target = 1f; status.text = label; }
    public void Hide() { Destroy(gameObject); }

    void Build(string title, string[] h, Font font)
    {
        hints = h != null && h.Length > 0 ? h : new[] { "Hints appear here while the game loads." };
        t0 = Time.unscaledTime;
        var r = (RectTransform)transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var dim = UI.Img("Dim", r, null, new Color(.07f, .04f, .02f, .72f)); UI.Stretch(dim.rectTransform, -4000); // reaches every screen edge, not just the game's box
        dim.raycastTarget = true;
        var card = UI.Panel("Card", r, Cream, 36, Ink); UI.Center(card.rectTransform, 0, 0, 760, 470);
        UI.KenteBand(card.rectTransform, 36);
        var tt = UI.Label(card.transform, title, 44, Brown, TextAnchor.MiddleCenter, font); UI.TL(tt.rectTransform, 0, 60, 760, 60);
        // pill bar like the prototype: dark ink rim, dark track, rounded green fill with moving diagonal stripes
        var track = UI.Panel("Track", card.transform, Ink, 18, null); UI.TL(track.rectTransform, 44, 150, 672, 36);
        var inner = UI.Panel("Inner", track.transform, Track, 14, null); UI.Stretch(inner.rectTransform, 4);
        inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var f = UI.Panel("Fill", inner.transform, Green, 14, null); fill = f.rectTransform;
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
        f.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var st = UI.Img("Stripes", fill, Stripes(), new Color(1, 1, 1, .2f)); st.type = Image.Type.Tiled; stripes = st.rectTransform;
        stripes.anchorMin = Vector2.zero; stripes.anchorMax = new Vector2(1, 1); stripes.offsetMin = new Vector2(-64, 0); stripes.offsetMax = Vector2.zero;
        status = UI.Label(card.transform, "Loading…", 16, Hint, TextAnchor.MiddleLeft, font); UI.TL(status.rectTransform, 48, 190, 400, 26);
        pct = UI.Label(card.transform, "0%", 16, Hint, TextAnchor.MiddleRight, font); UI.TL(pct.rectTransform, 512, 190, 200, 26);
        var hb = UI.Panel("HintBox", card.transform, Box, 22, null); UI.TL(hb.rectTransform, 44, 232, 672, 110);
        hintGroup = hb.gameObject.AddComponent<CanvasGroup>();
        var hl = UI.Label(hb.transform, "HINT", 14, Green, TextAnchor.UpperLeft, font); UI.TL(hl.rectTransform, 24, 16, 300, 20);
        hintText = UI.Label(hb.transform, hints[0], 22, Ink, TextAnchor.UpperLeft, font); UI.TL(hintText.rectTransform, 24, 40, 624, 64);
        var b = UI.Btn(card.transform, "Begin", Gold, Ink, GoldDark, () => onBegin?.Invoke(), 26, font, Ink);
        UI.TL((RectTransform)b.transform, 220, 368, 320, 64); beginBtn = b.gameObject; beginBtn.SetActive(false);
    }

    static Sprite stripeS;
    static Sprite Stripes()
    {
        if (stripeS != null && stripeS.texture != null) return stripeS;
        const int S = 32;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontUnloadUnusedAsset };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float d = Mathf.Repeat(x - y, S); float a = Mathf.Clamp01(Mathf.Min(d, 14f - d) + .5f); if (d > 14f) a = 0f;
            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        t.SetPixels32(px); t.Apply();
        return stripeS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
    }

    void Update()
    {
        float e = Time.unscaledTime - t0;
        shown = Mathf.MoveTowards(shown, Mathf.Min(target, e / 1.8f), Time.unscaledDeltaTime * 1.5f);
        fill.anchorMax = new Vector2(shown, 1);
        float sx = -64f + (e * 40f) % 32f; stripes.offsetMin = new Vector2(sx, 0); stripes.offsetMax = new Vector2(sx + 64f, 0); // stripes drift right
        pct.text = Mathf.RoundToInt(shown * 100) + "%";
        int i = (int)(e / hintSeconds) % hints.Length; float ph = (e % hintSeconds) / hintSeconds;
        if (hintText.text != hints[i]) hintText.text = hints[i];
        hintGroup.alpha = ph < .08f ? ph / .08f : ph > .92f ? (1 - ph) / .08f : 1f;
        bool ready = onBegin != null && shown >= .999f;
        if (beginBtn.activeSelf != ready) beginBtn.SetActive(ready);
    }
}
