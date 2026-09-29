using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

// Pot + segmented life bar + knowledge slots. Empty until Nana Nyame gives the pot (LifeManager.GivePot),
// then the pot pops in, the bar loads and the knowledge slots pop up. The pot cracks with each hit and
// shatters when the bar is empty, then the HUD fades away until the pot is given again.
// Put it on the same GameObject as LifeManager (Hits Per Pot = 1). It builds everything by itself.
public class PotLifeBarHUD : MonoBehaviour
{
    Canvas ownCanvas; float anansiHideT; // ownCanvas is only set when this HUD made its own canvas (the village map one)
    [Header("Placement")]
    [Tooltip("Leave empty for a stand-alone HUD in the top-left corner.")]
    public RectTransform hudParent;
    [Tooltip("Where the red damage flash goes when Hud Parent is set.")]
    public RectTransform flashParent;
    public Vector2 margin = new Vector2(32f, 28f);
    [Range(0.4f, 2f)] public float hudScale = 1f;

    [Header("Look")]
    [Tooltip("Green -> gold -> red as health drops. Off = always gold.")]
    public bool healthColours = true;
    public bool lastLifeWobble = true;
    public bool screenFlash = true;

    [Header("Knowledge slots")]
    [Tooltip("Symbols in order: Farmer, Carver, Drummer, Dancer (sym_*.png).")]
    public Sprite[] knowledgeIcons = new Sprite[4];
    public float slotSize = 56f, slotGap = 23.3f;

    static readonly Color Green = new Color32(79, 124, 58, 255), Gold = new Color32(232, 163, 61, 255), Red = new Color32(176, 58, 46, 255),
        Cream = new Color32(246, 235, 217, 255), Ink = new Color32(61, 36, 22, 255), Track = new Color32(58, 37, 23, 255), Dust = new Color32(216, 195, 174, 255);
    const float PW = 605f, PH = 640f, CX = 302.5f, CY = 358.4f, G = 4200f, WIND = 0.22f;
    const float POT_W = 104f, POT_H = 110f, U = POT_H / PH;
    // layout: the pot is drawn bigger (POT_SCALE) so its base lines up with the bottom of the knowledge circles; bar + circles move right by SHIFT
    const float POT_TOP = 4f, SHIFT = 44f, PAD = 14f;
    // bar: starts under the pot and is cut straight at CLIP_X, just inside the pot's outline at the bar's top edge,
    // so the pot always covers the cut (and the hidden part never shows when the pot breaks). Right end stays at BAR_R.
    const float BAR_X = 104f, BAR_R = 447f, CLIP_X = 114f;
    [Tooltip("Fresh start (no pot yet): how visible the dashed empty pot is on the background.")]
    [Range(0, 1)] public float emptySlotAlpha = .6f;
    float PotScale => (98f + slotSize - POT_TOP) / POT_H;
    [Header("Background")]
    public bool showBackground = true;
    public Color backgroundColor = new Color32(61, 36, 22, 150);
    bool restBroken;

    class Piece { public RectTransform r; public Image img; public Vector2 home; public Vector3 vel; }

    LifeManager lives;
    RectTransform root, potBox, body, fill, trail;
    CanvasGroup bodyGroup, barGroup;
    Image bodyImg, crack1, crack2, ghost, glow, healGlow, vignette;
    Sprite pot3, pot2, pot1, dotS, vignetteS;
    readonly List<Piece> chips = new List<Piece>(), shards = new List<Piece>(), dust = new List<Piece>(), puff = new List<Piece>();
    readonly bool[] chipOff = new bool[2];
    readonly float[] chipT = new float[2];
    readonly bool[] chipBack = new bool[2];

    int shown = 3, max = 3;

    // Damage spread over any pot size: 0 = whole, 1 = last bar left (always the most cracked). Every hit moves it forward.
    //   crack 1 grows over the first half, chip 1 falls at the halfway point,
    //   crack 2 grows over the second half, chip 2 falls on the last bar, then it shatters at 0.
    float Damage(int s) { if (s <= 0) return 1f; int hits = max - 1; if (hits <= 0) return 0f; return Mathf.Clamp01((max - s) / (float)hits); }
    float Crack1At(int s) => Mathf.Clamp01(Damage(s) / .5f);
    float Crack2At(int s) => Mathf.Clamp01((Damage(s) - .5f) / .5f);
    bool ChipOffAt(int i, int s) => s <= 0 || Damage(s) >= (i == 0 ? .5f : 1f) - 1e-4f;
    float hitShakeT = -99f;
    CanvasGroup rootGroup;
    RectTransform frameR;
    readonly List<RectTransform> slots = new List<RectTransform>();
    readonly List<Image> slotIcons = new List<Image>();
    float[] slotPopT = new float[0];
    float appearT = -99f;
    bool visible;
    bool heal;
    float changeT = -99f, fillFrom = 1f, trailFrom = 1f, flashT = -99f, bounceT = -99f, shatterT = -1f, puffT = -99f;
    int puffChip;
    Vector2 rootBase;

    float Now => Time.unscaledTime;

    void Start()
    {
        lives = LifeManager.Instance; // always the one shared pot
        if (lives == null) lives = GetComponent<LifeManager>(); // a HUD can sit in any mini-game and show the shared pot
        if (lives == null) { Debug.LogWarning("PotLifeBarHUD: no LifeManager found."); enabled = false; return; }
        max = lives.Capacity;
        LoadArt();
        Build();
        shown = lives.Lives;
        lives.PotBroken += _ => OnHit();
        lives.PotRestored += _ => OnHeal();
        lives.FullLifeBonus += () => bounceT = Now;
        lives.LivesReset += ResetView;
        lives.PotGiven += OnPotGiven;
        lives.PotUpgraded += OnUpgraded;
        if (hudParent == null) Map = this;
        lives.KnowledgeAdded += OnKnowledge;
        ResetView();
        visible = lives.HasPot || lives.EverHadPot;
        rootGroup.alpha = visible ? 1f : 0f;
        for (int i = 0; i < slotIcons.Count; i++) Alpha(slotIcons[i], lives.HasKnowledge(i) ? 1f : 0f);
    }

    // ---------- art ----------
    Sprite Load(string n)
    {
        var s = Resources.Load<Sprite>("PotLifeBar/" + n);
        if (s != null) return s;
        var t = Resources.Load<Texture2D>("PotLifeBar/" + n);
        if (t == null) { Debug.LogError("PotLifeBar: missing Resources/PotLifeBar/" + n + ".png"); return null; }
        return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f);
    }

    List<string[]> Table(string n)
    {
        var rows = new List<string[]>();
        var txt = Resources.Load<TextAsset>("PotLifeBar/" + n);
        if (txt == null) { Debug.LogError("PotLifeBar: missing Resources/PotLifeBar/" + n + ".txt"); return rows; }
        foreach (var line in txt.text.Split('\n')) { var p = line.Trim().Split(','); if (p.Length >= 5) rows.Add(p); }
        return rows;
    }

    static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

    void LoadArt()
    {
        pot3 = Load("pot_3"); pot2 = Load("pot_2"); pot1 = Load("pot_1");
        dotS = MakeTex(64, (u, v) => Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f));
        vignetteS = MakeTex(128, (u, v) =>
        {
            float e = 1f - Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v)) * 2f;
            float a = Mathf.Clamp01((e - 0.6f) / 0.4f); return a * a;
        });
    }

    Sprite MakeTex(int s, System.Func<float, float, float> alpha)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[s * s];
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) px[y * s + x] = new Color(1, 1, 1, alpha((x + .5f) / s, (y + .5f) / s));
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100f);
    }

    // ---------- building ----------
    RectTransform NewRect(string n, Transform parent)
    {
        var go = new GameObject(n, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Stretch a rounded bar picture to any length without squashing its round ends.
    static void SetSliced(Image img, Sprite s, float shownHeight)
    {
        if (s == null) return;
        float cap = s.rect.height / 2f;
        if (s.border.sqrMagnitude < .01f) s = Sprite.Create(s.texture, s.rect, new Vector2(.5f, .5f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect, new Vector4(cap, 0, cap, 0));
        img.sprite = s; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = (s.rect.height / shownHeight) * (100f / s.pixelsPerUnit);
    }

    static Sprite roundedS;
    static Sprite RoundedSprite()
    {
        if (roundedS) return roundedS;
        const int S = 64, R = 24;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float cx = Mathf.Clamp(x + .5f, R, S - R), cy = Mathf.Clamp(y + .5f, R, S - R);
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
            px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(R - d + .5f) * 255));
        }
        t.SetPixels32(px); t.Apply();
        roundedS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
        return roundedS;
    }

    Image NewImage(string n, Transform parent, Sprite s, Color c)
    {
        var img = NewRect(n, parent).gameObject.AddComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false;
        return img;
    }

    // Top-left based placement, y measured downwards (same numbers as the HTML prototype).
    static RectTransform TL(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Alpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
    Transform dividerParent; readonly List<GameObject> dividers = new List<GameObject>();
    // one divider between each segment; rebuilt whenever a pot of a different size is given
    void BuildDividers()
    {
        foreach (var g in dividers) Destroy(g); dividers.Clear();
        for (int i = 1; i < max; i++)
        {
            var d = NewImage("Divider", dividerParent, null, Ink).rectTransform;
            d.anchorMin = new Vector2(i / (float)max, 0); d.anchorMax = new Vector2(i / (float)max, 1); d.sizeDelta = new Vector2(4, 0); d.anchoredPosition = Vector2.zero;
            dividers.Add(d.gameObject);
        }
    }
    static float EaseOut(float k) => 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);
    static Color WithA(Color c, float a) { c.a = a; return c; }

    void Build()
    {
        Transform parent, flashP;
        if (hudParent != null) { parent = hudParent; flashP = flashParent; rootBase = Vector2.zero; }
        else
        {
            var canvasGO = new GameObject("PotLifeBarCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100; ownCanvas = canvas;
            var sc = canvasGO.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            parent = flashP = canvasGO.transform; rootBase = new Vector2(margin.x, -margin.y);
        }
        if (flashP != null)
        {
            vignette = NewImage("DamageFlash", flashP, vignetteS, WithA(new Color(.84f, .24f, .12f), 0f));
            Stretch(vignette.rectTransform);
        }

        root = NewRect("PotLifeBar", parent);
        TL(root, 0, 0, 420 + SHIFT, 98 + slotSize); root.anchoredPosition = rootBase; root.localScale = Vector3.one * hudScale;
        rootGroup = root.gameObject.AddComponent<CanvasGroup>(); rootGroup.blocksRaycasts = false; rootGroup.interactable = false;

        // rounded see-through brown panel behind the pot, bar and circles
        if (showBackground)
        {
            var bgp = NewImage("Background", root, RoundedSprite(), backgroundColor); bgp.type = Image.Type.Sliced;
            TL(bgp.rectTransform, -PAD, POT_TOP - PAD, 403 + SHIFT + PAD * 2, 98 + slotSize - POT_TOP + PAD * 2);
        }

        // bar (behind the pot)
        var clip = TL(NewRect("BarClip", root), CLIP_X, 0, BAR_R + 80 - CLIP_X, 98 + slotSize);
        clip.gameObject.AddComponent<RectMask2D>();
        float barW = BAR_R - BAR_X;
        glow = NewImage("LowGlow", clip, Load("bar_glow"), WithA(Color.white, 0f));
        TL(glow.rectTransform, BAR_X - 22 - CLIP_X, 63 - 55, barW + 44, 110);
        var frame = NewImage("BarFrame", clip, null, Color.white); SetSliced(frame, Load("bar_frame"), 62f);
        TL(frame.rectTransform, BAR_X - CLIP_X, 35, barW, 62); frameR = frame.rectTransform; frameR.pivot = new Vector2(0, .5f); frameR.anchoredPosition += new Vector2(0, -31);
        barGroup = frame.gameObject.AddComponent<CanvasGroup>();
        var track = NewImage("Track", frame.transform, null, Track); SetSliced(track, Load("bar_track"), 36f);
        TL(track.rectTransform, 11, 10, barW - 22, 36);
        track.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        var tr = NewImage("Trail", track.transform, null, Cream); trail = tr.rectTransform;
        var fi = NewImage("Fill", track.transform, null, Green); fill = fi.rectTransform;
        foreach (var r in new[] { trail, fill }) { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 1); r.offsetMin = r.offsetMax = Vector2.zero; r.pivot = new Vector2(0, .5f); }
        var gloss = NewImage("Gloss", fill, null, WithA(Color.white, .28f));
        gloss.rectTransform.anchorMin = new Vector2(0, .58f); gloss.rectTransform.anchorMax = Vector2.one; gloss.rectTransform.offsetMin = gloss.rectTransform.offsetMax = Vector2.zero;
        healGlow = NewImage("HealGlow", fill, null, WithA(new Color32(255, 244, 214, 255), 0f)); Stretch(healGlow.rectTransform);
        dividerParent = track.transform; BuildDividers();

        // knowledge slots (under the bar)
        var disc = MakeTex(64, (u, v) => Mathf.Clamp01((0.5f - Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f))) * 40f));
        for (int i = 0; i < knowledgeIcons.Length && i < SymNames.Length; i++) { var sym = LoadQuiet("sym_" + SymNames[i]); if (sym != null) knowledgeIcons[i] = sym; } // new symbol art
        slotPopT = new float[knowledgeIcons.Length];
        for (int i = 0; i < knowledgeIcons.Length; i++)
        {
            var ring = NewImage("KnowledgeSlot" + i, root, disc, Cream);
            var sr = ring.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0, 1); sr.pivot = new Vector2(.5f, .5f);
            sr.sizeDelta = Vector2.one * slotSize; sr.anchoredPosition = new Vector2(98 + SHIFT + slotSize / 2f + i * (slotSize + slotGap), -(98 + slotSize / 2f)); // under the bar track, spread to its width
            var bg = NewImage("Well", sr, disc, Track); Stretch(bg.rectTransform); bg.rectTransform.offsetMin = Vector2.one * 3; bg.rectTransform.offsetMax = -Vector2.one * 3;
            var ic = NewImage("Symbol", sr, knowledgeIcons[i], Color.white); ic.preserveAspect = true; Stretch(ic.rectTransform); ic.enabled = knowledgeIcons[i] != null; // no white square if a picture is missing
            ic.rectTransform.offsetMin = Vector2.one * 8; ic.rectTransform.offsetMax = -Vector2.one * 8;
            slots.Add(sr); slotIcons.Add(ic); slotPopT[i] = -99f;
        }

        // pot (in front)
        var holder = TL(NewRect("PotHolder", root), 0, POT_TOP, POT_W, POT_H);
        holder.pivot = new Vector2(0, 1); holder.anchoredPosition = new Vector2(0, -POT_TOP); holder.localScale = Vector3.one * PotScale;
        potBox = TL(NewRect("Pot", holder), 0, 0, POT_W, POT_H);
        potBox.pivot = new Vector2(.5f, .5f); potBox.anchoredPosition = new Vector2(POT_W / 2f, -POT_H / 2f); // scale from the centre
        ghost = NewImage("EmptySlot", potBox, Load("ghost"), WithA(Color.white, 0f)); Stretch(ghost.rectTransform);
        bodyImg = NewImage("Body", potBox, pot3, Color.white);
        body = bodyImg.rectTransform; body.anchorMin = body.anchorMax = body.pivot = new Vector2(.5f, 0); body.sizeDelta = new Vector2(POT_W, POT_H); body.anchoredPosition = Vector2.zero;
        bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
        crack1 = NewImage("Cracks1", body, Load("crack_1"), WithA(Color.white, 0f)); Stretch(crack1.rectTransform);
        crack2 = NewImage("Cracks2", body, Load("crack_2"), WithA(Color.white, 0f)); Stretch(crack2.rectTransform);
        foreach (var c in new[] { crack1, crack2 }) { c.type = Image.Type.Filled; c.fillMethod = Image.FillMethod.Vertical; c.fillOrigin = (int)Image.OriginVertical.Top; c.fillAmount = 0f; } // cracks grow down from the rim

        foreach (var row in Table("chips")) chips.Add(MakePiece(row));
        foreach (var row in Table("shards")) shards.Add(MakePiece(row));
        for (int k = 0; k < 12; k++) dust.Add(MakeDot());
        for (int k = 0; k < 5; k++) puff.Add(MakeDot());
    }

    Piece MakePiece(string[] row)
    {
        var img = NewImage(row[0], potBox, Load(row[0]), Color.white);
        var r = img.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = new Vector2(F(row[3]), F(row[4])) * U;
        var p = new Piece { r = r, img = img, home = new Vector2(F(row[1]), -F(row[2])) * U };
        r.anchoredPosition = p.home;
        // pot-space centre -> fling direction away from the pot centre
        Vector2 c = new Vector2(F(row[1]) + PW / 2f, F(row[2]) + PH / 2f);
        p.vel = Fling(c, 700f);
        img.gameObject.SetActive(false);
        return p;
    }

    Piece MakeDot()
    {
        var img = NewImage("Dust", potBox, dotS, WithA(Dust, 0f));
        var r = img.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        img.gameObject.SetActive(false);
        return new Piece { r = r, img = img };
    }

    static Vector3 Fling(Vector2 c, float speed)
    {
        Vector2 d = (c - new Vector2(CX, CY)).normalized;
        float sp = speed * Random.Range(0.7f, 1.3f);
        return new Vector3(d.x * sp + Random.Range(-100f, 100f), d.y * sp * .5f - 800f - Random.Range(0f, 400f), Random.Range(-350f, 350f));
    }

    // ---------- events ----------
    // intro timeline (seconds after GivePot)
    const float IN_POT = .45f, IN_BAR0 = .25f, IN_BAR = .35f, IN_FILL0 = .6f, IN_FILL = .8f, IN_SLOT0 = 1.35f, IN_SLOT_STEP = .12f, IN_SLOT = .4f;
    static float EaseBack(float k) { k = Mathf.Clamp01(k); const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(k - 1f, 3f) + c1 * Mathf.Pow(k - 1f, 2f); }
    float IntroFill(float t) => EaseOut((t - appearT - IN_FILL0) / IN_FILL);
    bool InIntro(float t) => t - appearT < IN_FILL0 + IN_FILL;

    float FillAt(float t) => InIntro(t) ? IntroFill(t) : Mathf.Lerp(fillFrom, shown / (float)max, EaseOut((t - changeT) / (heal ? .5f : .18f)));
    float TrailAt(float t)
    {
        if (heal || InIntro(t)) return FillAt(t);
        float e = t - changeT;
        return e < .38f ? trailFrom : Mathf.Lerp(trailFrom, shown / (float)max, EaseOut((e - .38f) / .42f));
    }

    void OnPotGiven()
    {
        max = lives.Capacity; BuildDividers();
        ResetView();
        visible = true; appearT = Now; rootGroup.alpha = 1f;
        for (int i = 0; i < slotPopT.Length; i++) slotPopT[i] = appearT + IN_SLOT0 + i * IN_SLOT_STEP;
    }

    // The village pot bar (the Nana Nyame pot gift flies here).
    public static PotLifeBarHUD Map { get; private set; }
    public Vector2 PotScreenPoint => RectTransformUtility.WorldToScreenPoint(null, potBox.position);
    public float PotScreenHeight { get { var c = new Vector3[4]; potBox.GetWorldCorners(c); return Mathf.Abs(c[1].y - c[0].y); } }
    void OnDestroy() { if (Map == this) Map = null; }

    void OnUpgraded()
    {
        float t = Now, f0 = FillAt(t);
        max = lives.Capacity; BuildDividers();
        ResetView();
        fillFrom = f0; changeT = t; heal = true; bounceT = t;
        if (!visible) { visible = true; appearT = Now; rootGroup.alpha = 1f; }
    }

    static readonly string[] SymNames = { "farmer", "carver", "drummer", "dancer" };
    static readonly Color[] OrbColors = { new Color32(112, 200, 132, 255), new Color32(229, 161, 75, 255), new Color32(232, 143, 203, 255), new Color32(28, 197, 228, 255) };
    Sprite LoadQuiet(string n) { var s = Resources.Load<Sprite>("PotLifeBar/" + n); if (s) return s; var t = Resources.Load<Texture2D>("PotLifeBar/" + n); return t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100f) : null; }

    readonly List<int> pendingKnowledge = new List<int>();
    void OnKnowledge(int slot)
    {
        if (slot < 0 || slot >= slotIcons.Count) return;
        if (hudParent == null && ownCanvas != null) { if (!pendingKnowledge.Contains(slot)) pendingKnowledge.Add(slot); return; } // village bar: an orb brings it in
        slotPopT[slot] = Now;
        Alpha(slotIcons[slot], 1f);
    }

    // ---------- knowledge orb: a glowing ball in the symbol's colour flies from the teacher's pin into its circle ----------
    class Orb { public int slot; public RectTransform core, halo, burst; public Image coreImg, haloImg, burstImg; public readonly List<Image> trail = new List<Image>(); public readonly List<Vector2> path = new List<Vector2>(); public Vector2 from; public float t0; public bool landed; }
    Orb orb; static Sprite orbS, ringS;
    void UpdateOrbs()
    {
        if (ownCanvas == null) return;
        if (orb == null && pendingKnowledge.Count > 0 && ownCanvas.enabled && visible && !JourneyDialogue.IsOpen && ScanFlow.MapReady && !AnansiRuntime.AnyGameOpen) { StartOrb(pendingKnowledge[0]); pendingKnowledge.RemoveAt(0); }
        if (orb == null) return;
        var o = orb; float t = Now - o.t0; var col = OrbColors[Mathf.Clamp(o.slot, 0, OrbColors.Length - 1)];
        Vector2 to = CanvasPoint(RectTransformUtility.WorldToScreenPoint(null, slots[o.slot].position));
        Vector2 p;
        if (t < .5f) { float k = t / .35f; float s = k < 1 ? EaseBack(k) : 1f; p = o.from + new Vector2(0, 50f * Mathf.Min(1, t / .35f)); o.core.localScale = o.halo.localScale = Vector3.one * s; } // rises out of the pin
        else
        {
            float q = Mathf.Clamp01((t - .5f) / .7f), e = q * q * (3 - 2 * q);
            Vector2 st = o.from + new Vector2(0, 50f), ctrl = new Vector2(Mathf.Lerp(st.x, to.x, .6f), Mathf.Max(st.y, to.y) + 140f);
            p = (1 - e) * (1 - e) * st + 2 * (1 - e) * e * ctrl + e * e * to;
            float sc = Mathf.Lerp(1f, .55f, e); o.core.localScale = o.halo.localScale = Vector3.one * sc;
            if (q >= 1f && !o.landed)
            {
                o.landed = true; slotPopT[o.slot] = Now; Alpha(slotIcons[o.slot], 1f); // the symbol appears in its circle
                o.burst.anchoredPosition = to; o.burst.gameObject.SetActive(true); o.t0 = Now - 1.2f; // keep time running for the burst
            }
        }
        if (!o.landed)
        {
            o.core.anchoredPosition = o.halo.anchoredPosition = p;
            float pulse = 1f + .12f * Mathf.Sin(t * 14f); o.halo.sizeDelta = Vector2.one * 150f * pulse;
            o.path.Insert(0, p); if (o.path.Count > 40) o.path.RemoveAt(o.path.Count - 1);
            for (int i = 0; i < o.trail.Count; i++)
            {
                int idx = Mathf.Min(o.path.Count - 1, (i + 1) * 4);
                o.trail[i].rectTransform.anchoredPosition = o.path[idx];
                var c = col; c.a = .6f * (1f - i / (float)o.trail.Count) * Mathf.Clamp01(t * 3f); o.trail[i].color = c;
                o.trail[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(34f, 10f, i / (float)o.trail.Count);
            }
        }
        else
        {
            float k = (Now - o.t0 - 1.2f) / .45f;
            o.core.gameObject.SetActive(false); o.halo.gameObject.SetActive(false); foreach (var tr in o.trail) tr.gameObject.SetActive(false);
            o.burst.localScale = Vector3.one * (1f + 1.2f * (1f - (1f - k) * (1f - k))); var bc = col; bc.a = Mathf.Clamp01(1f - k); o.burstImg.color = bc;
            if (k >= 1f) { Destroy(o.core.parent.gameObject); orb = null; }
        }
    }
    void StartOrb(int slot)
    {
        if (!orbS) orbS = MakeTex(96, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f; return Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f); });
        if (!ringS) ringS = MakeTex(96, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2f; return Mathf.Clamp01(1f - Mathf.Abs(d - .85f) / .12f); });
        var col = OrbColors[Mathf.Clamp(slot, 0, OrbColors.Length - 1)];
        var holder = NewRect("KnowledgeOrb", ownCanvas.transform); Stretch(holder);
        var o = new Orb { slot = slot, t0 = Now };
        for (int i = 0; i < 7; i++) { var d = NewImage("Trail", holder, orbS, WithA(col, 0f)); d.rectTransform.anchorMin = d.rectTransform.anchorMax = new Vector2(.5f, .5f); o.trail.Add(d); }
        o.haloImg = NewImage("Halo", holder, orbS, WithA(col, .8f)); o.halo = o.haloImg.rectTransform;
        o.coreImg = NewImage("Core", holder, orbS, Color.Lerp(col, Color.white, .65f)); o.core = o.coreImg.rectTransform; o.core.sizeDelta = Vector2.one * 70f;
        o.burstImg = NewImage("Burst", holder, ringS, col); o.burst = o.burstImg.rectTransform; o.burst.sizeDelta = Vector2.one * slotSize * 1.3f * hudScale; o.burst.gameObject.SetActive(false);
        foreach (var r in new[] { o.halo, o.core, o.burst }) r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        o.from = CanvasPoint(PinScreenPoint(slot));
        o.halo.anchoredPosition = o.core.anchoredPosition = o.from; o.halo.localScale = o.core.localScale = Vector3.zero;
        orb = o;
    }
    Vector2 PinScreenPoint(int slot)
    {
        var id = slot == 0 ? JourneyPin.PinId.Farmer : slot == 1 ? JourneyPin.PinId.Carver : slot == 2 ? JourneyPin.PinId.Drummer : JourneyPin.PinId.Dancer;
        Camera cam = Camera.main; if (cam == null) foreach (var c in Camera.allCameras) if (c.enabled) { cam = c; break; }
        foreach (var p in FindObjectsByType<JourneyPin>(FindObjectsInactive.Include))
            if (p.pin == id && cam != null)
            {
                var kp = p.GetComponent<KnowledgePin>(); Vector3 w = kp != null && kp.icon != null ? kp.icon.position : p.transform.position;
                Vector3 sp = cam.WorldToScreenPoint(w);
                if (sp.z > 0) return new Vector2(Mathf.Clamp(sp.x, 40, Screen.width - 40), Mathf.Clamp(sp.y, 40, Screen.height - 40));
            }
        return new Vector2(Screen.width * .5f, Screen.height * .45f);
    }
    Vector2 CanvasPoint(Vector2 screen) { RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)ownCanvas.transform, screen, null, out var p); return p; }

    void OnHit()
    {
        float t = Now;
        trailFrom = Mathf.Max(TrailAt(t), FillAt(t)); fillFrom = FillAt(t);
        shown = lives.Lives; changeT = t; heal = false; flashT = t;
        hitShakeT = t;
        for (int ci = 0; ci < chips.Count && ci < chipOff.Length && shown > 0; ci++)
        {
            if (chipOff[ci] || !ChipOffAt(ci, shown)) continue;
            chipOff[ci] = true; chipBack[ci] = false; chipT[ci] = t;
            var c = chips[ci];
            c.vel = Fling(new Vector2(c.home.x / U + PW / 2f, -c.home.y / U + PH / 2f), 700f);
            puffChip = ci; puffT = t;
        }
        if (shown > 0 && puffT != t) { puffChip = -1; puffT = t; } // a hit with no chip still puffs dust
        if (shown <= 0)
        {
            shatterT = t;
            foreach (var s in shards) s.vel = Fling(new Vector2(s.home.x / U + PW / 2f, -s.home.y / U + PH / 2f), 900f);
            foreach (var d in dust)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), sp = Random.Range(300f, 800f);
                d.home = new Vector2(Random.Range(-100f, 100f), Random.Range(-130f, 130f));
                d.vel = new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp - 200f, Random.Range(14f, 36f));
            }
        }
    }

    void OnHeal()
    {
        float t = Now;
        fillFrom = FillAt(t); shown = lives.Lives; changeT = t; heal = true;
        for (int ci = 0; ci < chips.Count && ci < chipOff.Length; ci++)
            if (chipOff[ci] && !ChipOffAt(ci, shown)) { chipBack[ci] = true; chipT[ci] = t; }
    }

    void ResetView()
    {
        restBroken = !lives.HasPot; // loaded with a broken pot: show the empty slot and an empty bar
        shown = restBroken ? 0 : lives.Lives; heal = false; changeT = -99f; fillFrom = trailFrom = shown / (float)max; shatterT = -1f; flashT = -99f;
        for (int i = 0; i < 2; i++) { chipOff[i] = shown > 0 && ChipOffAt(i, shown); chipBack[i] = false; }
        crack1.fillAmount = Crack1At(shown); crack2.fillAmount = Crack2At(shown);
        Alpha(crack1, crack1.fillAmount > 0 ? 1 : 0); Alpha(crack2, crack2.fillAmount > 0 ? 1 : 0);
    }

    // ---------- every frame ----------
    void LateUpdate()
    {
        if (root == null) return;
        if (ownCanvas != null && Time.unscaledTime >= anansiHideT) { anansiHideT = Time.unscaledTime + .25f; ownCanvas.enabled = !AnansiRuntime.AnyGameOpen; } // a mini-game shows its own pot
        UpdateOrbs();
        float t = Now, e = t - changeT;
        if (!visible)
        {
            // fresh start: just the background and a faint dashed pot, waiting for Nana Nyame's gift
            rootGroup.alpha = 1f; barGroup.alpha = 0f; Alpha(glow, 0f);
            foreach (var sl in slots) sl.localScale = Vector3.zero;
            body.gameObject.SetActive(false); potBox.localScale = Vector3.one; Alpha(ghost, emptySlotAlpha);
            return;
        }
        float ae = t - appearT;
        potBox.localScale = Vector3.one * (ae < IN_POT ? EaseBack(ae / IN_POT) : 1f);
        float bk = ae < IN_BAR0 + IN_BAR ? EaseOut((ae - IN_BAR0) / IN_BAR) : 1f;
        frameR.localScale = new Vector3(bk, 1f, 1f);
        for (int i = 0; i < slots.Count; i++)
        {
            float pk = (t - slotPopT[i]) / IN_SLOT;
            slots[i].localScale = Vector3.one * (pk < 0 ? (ae < IN_SLOT0 + 1f ? 0f : 1f) : pk < 1 ? EaseBack(pk) : 1f);
        }
        bool shattering = shatterT >= 0f || restBroken;
        float se = shatterT >= 0f ? t - shatterT : 99f;

        // pot body
        float tx = 0, rot = 0, sx = 1, sy = 1;
        if (!heal && e < .32f && !shattering) { float k = e / .32f; tx = Mathf.Sin(e * 120f) * 16f * (1 - k); sy = 1 - .08f * Mathf.Sin(k * Mathf.PI); sx = 1 + .05f * Mathf.Sin(k * Mathf.PI); }
        if (heal && e < .4f) { float s = 1 + .1f * Mathf.Sin(e / .4f * Mathf.PI); sx = sy = s; }
        float be = t - bounceT; if (be < .35f) { float k = be / .35f; sy = 1 + .1f * Mathf.Sin(k * Mathf.PI) * (1 - k); sx = 1 - .05f * Mathf.Sin(k * Mathf.PI) * (1 - k); }
        float hs = t - hitShakeT; if (hs < .32f && !shattering) rot += Mathf.Sin(hs * 55f) * 6f * (1 - hs / .32f); // every hit shakes the pot
        if (shown == 1 && lastLifeWobble && !shattering) { float c = (t % 1.6f) / 1.6f; if (c < .4f) rot = Mathf.Sin(c / .4f * Mathf.PI * 3f) * 7f * (1 - c / .4f); }

        bool bodyOn = true;
        if (shattering)
        {
            if (se < WIND) { float k = se / WIND; tx = Mathf.Sin(se * 160f) * 20f * k; sy = 1 - .09f * k; sx = 1 + .05f * k; }
            else bodyOn = false;
        }
        body.gameObject.SetActive(bodyOn);
        body.anchoredPosition = new Vector2(tx * U, 0);
        body.localScale = new Vector3(sx, sy, 1);
        body.localRotation = Quaternion.Euler(0, 0, -rot);
        bool flicker = lives.IsInvulnerable && shown > 0 && Mathf.FloorToInt(t / .08f) % 2 == 1;
        bodyGroup.alpha = flicker ? .4f : 1f;
        barGroup.alpha = flicker ? .55f : 1f;

        // which holes are showing
        for (int i = 0; i < 2; i++) if (chipBack[i] && t - chipT[i] >= .45f) { chipBack[i] = false; chipOff[i] = false; }
        bodyImg.sprite = chipOff.Length > 1 && chipOff[1] ? pot1 : chipOff[0] ? pot2 : pot3;

        // cracks fade toward their target
        int cs = shown <= 0 ? 1 : shown; // while shattering keep the last-bar cracks
        crack1.fillAmount = Mathf.MoveTowards(crack1.fillAmount, Crack1At(cs), Time.unscaledDeltaTime / .35f);
        crack2.fillAmount = Mathf.MoveTowards(crack2.fillAmount, Crack2At(cs), Time.unscaledDeltaTime / .35f);
        Alpha(crack1, crack1.fillAmount > .001f ? 1f : 0f); Alpha(crack2, crack2.fillAmount > .001f ? 1f : 0f);

        // flying chips
        for (int i = 0; i < chips.Count && i < 2; i++)
        {
            var c = chips[i];
            float tt, a;
            bool on;
            if (chipBack[i]) { float k = EaseOut((t - chipT[i]) / .45f); tt = (1 - k) * .45f; a = Mathf.Clamp01(k * 3f); on = true; }
            else if (chipOff[i]) { tt = t - chipT[i]; on = tt < 1.1f; a = Mathf.Clamp01(1f - (tt - .6f) / .5f); }
            else { tt = 0; a = 0; on = false; }
            c.r.gameObject.SetActive(on);
            if (on) Place(c, tt, a);
        }
        float pe = t - puffT;
        for (int k = 0; k < puff.Count; k++)
        {
            bool on = pe < .5f && puffChip < chips.Count;
            puff[k].r.gameObject.SetActive(on);
            if (!on) continue;
            float q = pe / .5f;
            Vector2 c = (puffChip >= 0 ? chips[puffChip].home : new Vector2(0, -PH * .1f * U)) + new Vector2(Mathf.Cos(k * 1.3f) * 120f * q, -(Mathf.Sin(k * 1.3f) * 90f * q - 40f * q)) * U;
            puff[k].r.anchoredPosition = c; puff[k].r.sizeDelta = Vector2.one * (18f + 30f * q) * 2f * U;
            Alpha(puff[k].img, (1 - q) * .5f);
        }

        // shatter
        bool shardsOn = shattering && se >= WIND;
        float st = shardsOn ? se - WIND : 0f;
        foreach (var s in shards) { s.r.gameObject.SetActive(shardsOn && st < 1.2f); if (shardsOn) Place(s, st, Mathf.Clamp01(1f - (st - .55f) / .55f)); }
        foreach (var d in dust)
        {
            bool on = shardsOn && st < .7f;
            d.r.gameObject.SetActive(on);
            if (!on) continue;
            Vector2 p = d.home + new Vector2(d.vel.x * st, d.vel.y * st + 600f * st * st);
            d.r.anchoredPosition = new Vector2(p.x, -p.y) * U;
            d.r.sizeDelta = Vector2.one * d.vel.z * 2f * (1 + st * 2f) * U;
            Alpha(d.img, (1 - st / .7f) * .45f);
        }
        Alpha(ghost, shardsOn ? EaseOut(st / .2f) : 0f);
        // after the shatter the HUD stays: empty dashed slot + empty bar, until a pot is given again
        rootGroup.alpha = 1f;

        // bar
        float f = Mathf.Clamp01(FillAt(t)), tr = Mathf.Clamp01(TrailAt(t));
        fill.anchorMax = new Vector2(f, 1); trail.anchorMax = new Vector2(tr, 1);
        var fillImg = fill.GetComponent<Image>();
        fillImg.color = !healthColours ? Gold : (InIntro(t) || shown >= max) ? Green : shown == 1 ? Red : Gold;
        Alpha(healGlow, heal && e < .6f ? .8f * (1 - e / .6f) : 0f);
        Alpha(glow, shown == 1 ? .35f + .35f * Mathf.Sin(t / .22f) : 0f);

        // whole HUD shake + screen flash
        root.anchoredPosition = rootBase + new Vector2(!heal && e < .3f ? Mathf.Sin(e * 90f) * 6f * (1 - e / .3f) : 0f, 0);
        if (vignette != null) { float fe = t - flashT; Alpha(vignette, screenFlash && fe < .45f ? .7f * (1 - fe / .45f) : 0f); }
    }

    void Place(Piece p, float tt, float a)
    {
        Vector2 off = new Vector2(p.vel.x * tt, p.vel.y * tt + .5f * G * tt * tt);
        p.r.anchoredPosition = p.home + new Vector2(off.x, -off.y) * U;
        p.r.localRotation = Quaternion.Euler(0, 0, -p.vel.z * tt);
        Alpha(p.img, a);
    }
}
