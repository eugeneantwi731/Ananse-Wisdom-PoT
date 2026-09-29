using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The Bag bar on the village map: 4 fixed slots (Kente, Food, Drum, Rhythm), each with a small count (up to 3).
// Drag an item from a slot and let go on a pin: JourneyPin.AcceptItem decides if it is taken, otherwise it flies back.
// Put this on any GameObject in the map scene. Icons load from Resources/Bag/item_kente, item_food, item_drum, item_rhythm.
public class BagBarHUD : MonoBehaviour
{
    public Font font;
    [Tooltip("Size of one slot on a 1920x1080 screen.")]
    public float slotSize = 118f;
    [Tooltip("Show the drag tip until the first kente is given to Nana Nyame.")]
    public bool showFirstDragTip = true;
    public string firstDragTip = "Drag your kente onto Nana Nyame";
    [Tooltip("Title on the tab at the top of the bar.")]
    public string title = "Gifts";
    [Tooltip("Show the Bag bar from the start of the journey (empty slots), not only after the first kente.")]
    public bool showFromStart = true;
    public enum EmptyStyle { DashedOutline, FadedIcon }
    [Tooltip("Empty slot: a dashed outline in the item's shape, or a very faint picture.")]
    public EmptyStyle emptyStyle = EmptyStyle.DashedOutline;
    [Tooltip("FadedIcon only: how visible the picture is.")]
    [Range(0, 1)] public float emptyIconAlpha = .08f;

    static readonly string[] Ids = { Bag.Kente, Bag.Food, Bag.Drum, Bag.Rhythm };
    static readonly Color Panel = new Color32(61, 36, 22, 190), PanelLine = new Color32(246, 235, 217, 60), Cream = new Color32(246, 235, 217, 255),
        Ink = new Color32(61, 36, 22, 255), Gold = new Color32(232, 163, 61, 255), SlotBg = new Color32(35, 20, 11, 150),
        Peg = new Color32(107, 62, 38, 255), PegLine = new Color32(61, 36, 22, 255);
    static readonly Dictionary<string, Sprite> rounded = new Dictionary<string, Sprite>();

    class Slot { public string id; public RectTransform rect; public Image icon, ring, dashes; public GameObject badge; public Text count; public int last = -1, pending; public float popT = -9; }
    class Fly { public Slot slot; public RectTransform rect, glow; public Image img, glowImg; public float t0; public Vector2 from; }
    class Burst { public RectTransform rect; public Image img; public float t0; }
    readonly List<Burst> bursts = new List<Burst>();
    static Sprite softS, ringS;
    static Sprite Radial(bool ring)
    {
        const int S = 96; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
            float a = ring ? Mathf.Clamp01(1f - Mathf.Abs(d - .86f) / .1f) : Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        t.SetPixels32(px); t.Apply(); return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100);
    }
    readonly List<Fly> flies = new List<Fly>();

    readonly List<Slot> slots = new List<Slot>();
    RectTransform root, bar, ghost, tip; Image ghostImg; Text tipText; CanvasGroup barGroup; Canvas canvas;
    Slot dragging; Vector2 flyFrom, flyTo; float flyT = -9; float Now => Time.unscaledTime;

    void Awake()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cgo = new GameObject("BagBarCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        root = (RectTransform)cgo.transform;
        AnansiRuntime.EnsureEventSystem();
        Build();
    }

    void Build()
    {
        float gap = 14, pad = 20, w = Ids.Length * slotSize + (Ids.Length - 1) * gap + pad * 2, h = slotSize + pad * 2;
        var b = Img("BagBar", root, Panel); bar = b.rectTransform; Round(b, 30, 0);
        var bl = Img("BarLine", bar, PanelLine); Round(bl, 30, 2); Stretch(bl.rectTransform, 0); bl.raycastTarget = false;
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, 0); bar.pivot = new Vector2(.5f, 0); bar.sizeDelta = new Vector2(w, h); bar.anchoredPosition = new Vector2(0, 24); bar.sizeDelta += new Vector2(0, 14);
        barGroup = b.gameObject.AddComponent<CanvasGroup>();
        // name peg: brown pill with a dark rim, centred on the top edge (same style as the dialogue name tag)
        var tab = Img("TitlePeg", bar, Peg); Round(tab, 26, 0); tab.raycastTarget = false;
        var tr = tab.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(.5f, 1); tr.pivot = new Vector2(.5f, .5f); tr.sizeDelta = new Vector2(170, 52); tr.anchoredPosition = Vector2.zero;
        var rim = Img("Rim", tr, PegLine); Round(rim, 26, 3.5f); Stretch(rim.rectTransform, 0); rim.raycastTarget = false;
        var tt = Txt(tr, 28, Cream); tt.text = title; tt.fontStyle = FontStyle.Bold; Stretch(tt.rectTransform, 0);
        for (int i = 0; i < Ids.Length; i++)
        {
            var s = new Slot { id = Ids[i] };
            var bg = Img("Slot_" + s.id, bar, SlotBg); Round(bg, 20, 0); s.rect = bg.rectTransform;
            s.rect.anchorMin = s.rect.anchorMax = new Vector2(0, .5f); s.rect.pivot = new Vector2(.5f, .5f);
            s.rect.sizeDelta = Vector2.one * slotSize; s.rect.anchoredPosition = new Vector2(pad + slotSize / 2 + i * (slotSize + gap), -7);
            s.ring = Img("Ring", s.rect, Gold); Round(s.ring, 24, 4); Stretch(s.ring.rectTransform, -4); s.ring.transform.SetAsFirstSibling(); s.ring.enabled = false;
            s.icon = Img("Icon", s.rect, Color.white); s.icon.preserveAspect = true; Stretch(s.icon.rectTransform, 12);
            s.icon.sprite = Resources.Load<Sprite>("Bag/item_" + s.id); s.icon.raycastTarget = false;
            s.dashes = Img("EmptyOutline", s.rect, new Color32(237, 182, 73, 190)); s.dashes.preserveAspect = true; s.dashes.raycastTarget = false; Stretch(s.dashes.rectTransform, 12);
            s.dashes.sprite = DashedOutline(s.icon.sprite);
            var bd = Img("Badge", s.rect, Gold); Round(bd, 10, 0); s.badge = bd.gameObject; var br = bd.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(1, 0); br.pivot = new Vector2(1, 0); br.sizeDelta = new Vector2(34, 34); br.anchoredPosition = new Vector2(-6, 6); // inside the box, bottom-right
            s.count = Txt(br, 24, Ink); s.count.fontStyle = FontStyle.Bold; Stretch(s.count.rectTransform, 0);
            var drag = bg.gameObject.AddComponent<BagSlotDrag>(); drag.hud = this; drag.index = i;
            slots.Add(s);
        }
        ghostImg = Img("DragGhost", root, Color.white); ghostImg.preserveAspect = true; ghostImg.raycastTarget = false;
        ghost = ghostImg.rectTransform; ghost.sizeDelta = Vector2.one * slotSize * 1.1f; ghost.gameObject.SetActive(false);
        var tp = Img("DragTip", root, Cream); tip = tp.rectTransform; tp.raycastTarget = false;
        tip.anchorMin = tip.anchorMax = new Vector2(.5f, 0); tip.pivot = new Vector2(.5f, 0); tip.sizeDelta = new Vector2(560, 64); tip.anchoredPosition = new Vector2(0, h + 92);
        tipText = Txt(tip, 30, Ink); tipText.text = firstDragTip; tipText.fontStyle = FontStyle.Bold; Stretch(tipText.rectTransform, 0);
    }

    void Update()
    {
        bool showBar = showFromStart || JourneyState.TutorialWoven;
        bool onMap = canvas.enabled && ScanFlow.MapReady; // gifts fly in only once the village is showing
        barGroup.alpha = Mathf.MoveTowards(barGroup.alpha, showBar ? 1 : 0, Time.unscaledDeltaTime * 3);
        barGroup.blocksRaycasts = showBar;
        foreach (var s in slots)
        {
            int n = Bag.Count(s.id);
            // new item while the map is showing: fly it in like a collected coin
            if (onMap)
            {
                if (s.last >= 0 && n > s.last) for (int k2 = 0; k2 < n - s.last; k2++) StartFly(s, k2 * .18f);
                else if (n < s.last) s.pending = 0;
                s.last = n;
            }
            n = Mathf.Max(0, n - s.pending);
            bool held = dragging == s;
            bool empty = n == 0 || (held && n == 1);
            bool outline = emptyStyle == EmptyStyle.DashedOutline && s.dashes.sprite != null;
            var c = s.icon.color; c.a = empty ? (outline ? 0f : emptyIconAlpha) : 1f; s.icon.color = c;
            s.dashes.enabled = empty && outline;
            s.badge.SetActive(n > 1 || (n == 1 && held)); s.count.text = (held ? n - 1 : n).ToString();
            float pe = (Now - s.popT) / .35f; float k = pe < 1 ? 1 + .25f * Mathf.Sin(pe * Mathf.PI) : 1;
            s.rect.localScale = Vector3.one * k;
        }
        UpdateFlies();
        // first-time tip: pulse the kente slot until the first offering
        bool tipOn = showFirstDragTip && showBar && JourneyState.NanaVisits == 0 && KenteBag.Count > 0 && dragging == null && !JourneyDialogue.IsOpen;
        tip.gameObject.SetActive(tipOn); slots[0].ring.enabled = tipOn;
        if (tipOn) { var rc = slots[0].ring.color; rc.a = .5f + .5f * Mathf.Sin(Now * 5); slots[0].ring.color = rc; }
        // failed drop: fly back
        if (flyT > 0)
        {
            float q = Mathf.Clamp01((Now - flyT) / .25f), e = 1 - Mathf.Pow(1 - q, 3);
            ghost.anchoredPosition = Vector2.Lerp(flyFrom, flyTo, e);
            if (q >= 1) { flyT = -9; ghost.gameObject.SetActive(false); dragging = null; }
        }
    }

    // Coin-style collect: the item pops up in the middle of the screen, then arcs down into its slot.
    void StartFly(Slot s, float delay)
    {
        s.pending++;
        if (!softS) softS = Radial(false); if (!ringS) ringS = Radial(true);
        var gl = Img("CollectGlow_" + s.id, root, new Color(Gold.r, Gold.g, Gold.b, 0f)); gl.sprite = softS; gl.raycastTarget = false;
        var gr = gl.rectTransform; gr.sizeDelta = Vector2.one * slotSize * 2.6f; gr.anchorMin = gr.anchorMax = new Vector2(.5f, .5f);
        var img = Img("Collect_" + s.id, root, Color.white); img.sprite = s.icon.sprite; img.preserveAspect = true; img.raycastTarget = false;
        var r = img.rectTransform; r.sizeDelta = Vector2.one * slotSize * 1.6f; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.anchoredPosition = SourcePoint(s.id); r.localScale = Vector3.zero; gr.anchoredPosition = r.anchoredPosition;
        flies.Add(new Fly { slot = s, rect = r, img = img, glow = gr, glowImg = gl, t0 = Now + delay, from = r.anchoredPosition });
    }
    // Where a new item comes from: the pin of the place that made it (kente from the Weaver/Home hut, food from the Farmer...).
    // Falls back to the middle of the screen if that pin is hidden or off screen.
    Vector2 SourcePoint(string id)
    {
        var from = id == Bag.Kente ? JourneyPin.PinId.Home : id == Bag.Food ? JourneyPin.PinId.Farmer : id == Bag.Drum ? JourneyPin.PinId.Carver : JourneyPin.PinId.Drummer;
        Camera cam = Camera.main;
        if (cam == null) foreach (var c in Camera.allCameras) if (c.enabled) { cam = c; break; }
        JourneyPin pin = null;
        foreach (var p in FindObjectsByType<JourneyPin>(FindObjectsInactive.Include)) if (p.pin == from) { pin = p; break; }
        if (cam != null && pin != null)
        {
            var kp = pin.GetComponent<KnowledgePin>();
            Vector3 w = kp != null && kp.icon != null ? kp.icon.position : pin.transform.position;
            Vector3 sp = cam.WorldToScreenPoint(w);
            if (sp.z > 0) return Local(new Vector2(Mathf.Clamp(sp.x, 40, Screen.width - 40), Mathf.Clamp(sp.y, 40, Screen.height - 40)));
        }
        Debug.LogWarning("Gifts: couldn't find the " + from + " pin (camera: " + (cam ? cam.name : "none") + "), so the " + id + " flew in from the middle.");
        return new Vector2(0, 60);
    }

    static float EaseBack(float k) { k = Mathf.Clamp01(k); const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f); }
    void UpdateFlies()
    {
        for (int i = bursts.Count - 1; i >= 0; i--) // gold ring spreading out of the slot on landing
        {
            var b = bursts[i]; float k = (Now - b.t0) / .4f;
            b.rect.localScale = Vector3.one * (1f + .7f * (1f - (1f - k) * (1f - k)));
            var c = b.img.color; c.a = Mathf.Clamp01(1f - k); b.img.color = c;
            if (k >= 1f) { Destroy(b.rect.gameObject); bursts.RemoveAt(i); }
        }
        for (int i = flies.Count - 1; i >= 0; i--)
        {
            var f = flies[i]; float t = Now - f.t0;
            if (t < 0) continue;
            // 1) rises out of the pin with a soft glow and a springy pop (no spinning)
            if (t < .55f)
            {
                float k = t / .45f, pop = k < 1 ? EaseBack(k) : 1f;
                f.rect.localScale = Vector3.one * pop;
                f.rect.anchoredPosition = f.from + new Vector2(0, 40f * Mathf.Min(1f, t / .45f) + Mathf.Sin(t * 9f) * 3f);
                f.glow.anchoredPosition = f.rect.anchoredPosition; f.glow.localScale = Vector3.one * (.6f + .4f * pop);
                var gc = f.glowImg.color; gc.a = .55f * Mathf.Min(1f, k); f.glowImg.color = gc;
                continue;
            }
            // 2) glides along an arc into its slot, shrinking to slot size, slight stretch along the motion
            float q = Mathf.Clamp01((t - .55f) / .55f), e = q * q * (3 - 2 * q);
            Vector2 start = f.from + new Vector2(0, 40f);
            Vector2 to = Local(RectTransformUtility.WorldToScreenPoint(null, f.slot.rect.position));
            Vector2 ctrl = new Vector2(Mathf.Lerp(start.x, to.x, .5f), Mathf.Max(start.y, to.y) + 160);
            Vector2 p = (1 - e) * (1 - e) * start + 2 * (1 - e) * e * ctrl + e * e * to;
            float sc = Mathf.Lerp(1f, .62f, e), stretch = Mathf.Sin(q * Mathf.PI) * .08f;
            f.rect.anchoredPosition = p; f.rect.localScale = new Vector3(sc * (1 - stretch), sc * (1 + stretch), 1);
            f.rect.localRotation = Quaternion.identity;
            f.glow.anchoredPosition = p; f.glow.localScale = Vector3.one * sc;
            var g2 = f.glowImg.color; g2.a = .55f * (1f - e); f.glowImg.color = g2;
            if (q >= 1)
            {
                f.slot.pending = Mathf.Max(0, f.slot.pending - 1); f.slot.popT = Now;
                var bi = Img("LandBurst", root, Gold); bi.sprite = ringS; bi.raycastTarget = false;
                var br = bi.rectTransform; br.anchorMin = br.anchorMax = new Vector2(.5f, .5f); br.sizeDelta = Vector2.one * slotSize * 1.1f; br.anchoredPosition = to;
                bursts.Add(new Burst { rect = br, img = bi, t0 = Now });
                Destroy(f.glow.gameObject); Destroy(f.rect.gameObject); flies.RemoveAt(i);
            }
        }
    }

    // Dashed outline that follows the item picture's shape (made in code from the picture, no extra image needed).
    static Sprite DashedOutline(Sprite src)
    {
        if (src == null) return null;
        const int S = 160, Line = 3, DashLen = 9, GapLen = 6; const float Pad = 8;
        // copy the picture through the GPU so it doesn't need Read/Write enabled
        var rt = RenderTexture.GetTemporary(S, S, 0, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active; RenderTexture.active = rt; GL.Clear(true, true, new Color(0, 0, 0, 0));
        var r = src.textureRect; var tex = src.texture;
        float aspect = r.width / r.height, dw = S - Pad * 2, dh = dw; if (aspect > 1) dh = dw / aspect; else dw = dh * aspect;
        GL.PushMatrix(); GL.LoadPixelMatrix(0, S, S, 0);
        Graphics.DrawTexture(new Rect((S - dw) / 2, (S - dh) / 2, dw, dh), tex, new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height), 0, 0, 0, 0);
        GL.PopMatrix();
        var read = new Texture2D(S, S, TextureFormat.RGBA32, false); read.ReadPixels(new Rect(0, 0, S, S), 0, 0); read.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
        var src32 = read.GetPixels32(); Object.Destroy(read);
        bool In(int x, int y) => x >= 0 && y >= 0 && x < S && y < S && src32[y * S + x].a > 110;
        var outPx = new Color32[S * S]; float cx = S / 2f, cy = S / 2f;
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            if (In(x, y)) continue;
            bool near = false; // just outside the shape, within Line pixels
            for (int dy = -Line; dy <= Line && !near; dy++) for (int dx = -Line; dx <= Line; dx++) if (dx * dx + dy * dy <= Line * Line && In(x + dx, y + dy)) { near = true; break; }
            if (!near) continue;
            float ang = Mathf.Atan2(y - cy, x - cx) + Mathf.PI, arc = ang * Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            if (arc % (DashLen + GapLen) < DashLen) outPx[y * S + x] = new Color32(255, 255, 255, 255);
        }
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        t.SetPixels32(outPx); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100);
    }

    // Rounded-corner look for an Image (sliced, so the corners stay round at any size). ring > 0 = outline only, that many px thick.
    static void Round(Image img, float radius, float ring)
    {
        string key = radius + "_" + ring;
        if (!rounded.TryGetValue(key, out var sp) || sp == null)
        {
            int S = Mathf.CeilToInt(radius * 2 + 4); float r = radius;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float qx = Mathf.Abs(x + .5f - S / 2f) - (S / 2f - r), qy = Mathf.Abs(y + .5f - S / 2f) - (S / 2f - r);
                float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                float al = Mathf.Clamp01(.5f - d); if (ring > 0) al *= Mathf.Clamp01(.5f + d + ring);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(al * 255));
            }
            t.SetPixels32(px); t.Apply();
            float bd = radius + 1;
            sp = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(bd, bd, bd, bd));
            rounded[key] = sp;
        }
        img.sprite = sp; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1;
    }

    Vector2 Local(Vector2 screen) { RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var p); return p; }

    internal void BeginDrag(int i, PointerEventData e)
    {
        var s = slots[i];
        if (dragging != null || JourneyDialogue.IsOpen || Bag.Count(s.id) <= 0) return;
        dragging = s; flyT = -9;
        ghostImg.sprite = s.icon.sprite; ghost.gameObject.SetActive(true); ghost.SetAsLastSibling(); ghost.anchoredPosition = Local(e.position);
    }
    internal void Drag(PointerEventData e) { if (dragging != null && flyT < 0) ghost.anchoredPosition = Local(e.position); }
    internal void EndDrag(PointerEventData e)
    {
        if (dragging == null || flyT > 0) return;
        var pin = JourneyPin.At(e.position);
        bool ok = pin != null && pin.AcceptItem(dragging.id);
        if (ok) { ghost.gameObject.SetActive(false); dragging = null; return; }
        flyFrom = ghost.anchoredPosition; flyTo = Local(RectTransformUtility.WorldToScreenPoint(null, dragging.rect.position)); flyT = Now;
    }

    Image Img(string n, Transform p, Color c) { var g = new GameObject(n, typeof(RectTransform), typeof(Image)); g.transform.SetParent(p, false); var i = g.GetComponent<Image>(); i.color = c; return i; }
    Text Txt(Transform p, int size, Color c) { var g = new GameObject("Text", typeof(RectTransform), typeof(Text)); g.transform.SetParent(p, false); var t = g.GetComponent<Text>(); t.font = font; t.fontSize = size; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; return t; }
    static void Stretch(RectTransform r, float inset) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.one * inset; r.offsetMax = -Vector2.one * inset; }
    float anansiHideT;
    void LateUpdate()
    {
        if (Time.unscaledTime < anansiHideT) return; anansiHideT = Time.unscaledTime + .25f;
        if (canvas != null) canvas.enabled = !AnansiRuntime.AnyGameOpen; // a mini-game has its own pot and UI
    }
}
public class BagSlotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public BagBarHUD hud; public int index;
    public void OnBeginDrag(PointerEventData e) => hud.BeginDrag(index, e);
    public void OnDrag(PointerEventData e) => hud.Drag(e);
    public void OnEndDrag(PointerEventData e) => hud.EndDrag(e);
}
