using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Add next to KnowledgePin on each village pin. It decides whether the pin is shown, locked or ready,
// and what happens on tap: open the game, or a short line explaining what is missing.
[RequireComponent(typeof(KnowledgePin))]
public class JourneyPin : MonoBehaviour
{
    public enum PinId { Home, NanaNyame, Farmer, Carver, Drummer, Dancer }
    public PinId pin = PinId.Farmer;

    [Header("Open the game")]
    [Tooltip("The game's open button (e.g. the Farmer's launcher button). Clicked when the pin is ready.")]
    public Button openButton;
    public UnityEvent onOpen = new UnityEvent();

    [Header("Character (Carver, Drummer, Dancer)")]
    public string characterName = "The Carver";
    public Sprite characterPortrait;
    [TextArea] public string missingItemLine = "I would love to help you, Ananse, but I cannot work on an empty stomach.";
    [TextArea] public string thanksLine = "Ah, food! Thank you, my friend. Come, sit with me.";

    [Header("Anansi's thoughts")]
    [TextArea] public string noPotThought = "My pot is broken. I need a new one from Nana Nyame before I visit anyone.";
    [TextArea] public string bagFullThought = "I already have three kente in my Gifts. Nana Nyame might like one.";

    public Color lockedTint = new Color(.55f, .55f, .55f, 1f);

    [Header("Locked pins")]
    [Tooltip("On: locked pins show grey with a padlock. Off: they stay hidden until unlocked.")]
    public bool showLockedPins = true;
    [Tooltip("Colour of a locked pin (ash grey).")]
    public Color ashColor = new Color(.50f, .49f, .48f, 1f);
    [Tooltip("How see-through a locked pin is (1 = solid).")]
    [Range(.2f, 1f)] public float ashOpacity = .7f;
    Color padlockTint => Color.white; // colour now comes from the ash shader
    [Tooltip("The next teacher to visit pulses softly.")]
    public bool pulseNextPin = true;

    static readonly List<JourneyPin> all = new List<JourneyPin>();
    public static void RefreshAll() { foreach (var p in all) if (p) p.Refresh(true); }

    KnowledgePin kp; SpriteRenderer iconSR; Renderer[] rends; TMP_Text[] texts; Collider col; bool shown; Coroutine fade;
    string Id => pin == PinId.Home ? JourneyState.Home : pin == PinId.NanaNyame ? JourneyState.Nana : pin.ToString().ToLower();

    void Awake()
    {
        FixPinFromName();
        if (bagFullThought == "My basket is full of kente. Nana Nyame might like one.") bagFullThought = "I already have three kente in my Gifts. Nana Nyame might like one."; // old saved text
        kp = GetComponent<KnowledgePin>();
        if (kp.openButton != null && openButton == null) openButton = kp.openButton;
        if (kp.onTapped == null) kp.onTapped = new UnityEvent<string>();
        kp.onTapped.AddListener(_ => { if (shown && !JourneyDialogue.IsOpen) Tapped(); }); // fires after the tap animation
        iconSR = kp.icon ? kp.icon.GetComponent<SpriteRenderer>() : null;
        rends = GetComponentsInChildren<Renderer>(true); texts = GetComponentsInChildren<TMP_Text>(true); col = GetComponent<Collider>();
    }
    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }
    void Start() { if (openButton == null) openButton = kp.openButton ? kp.openButton : GetComponent<Button>(); Refresh(false); }

    // If the dropdown was left on the default (Farmer) but the object is clearly another pin, use the name.
    void FixPinFromName()
    {
        string n = gameObject.name.ToLowerInvariant();
        PinId? guess = n.Contains("carv") ? PinId.Carver : n.Contains("drum") ? PinId.Drummer : n.Contains("danc") ? PinId.Dancer
                     : n.Contains("nana") || n.Contains("nyame") ? PinId.NanaNyame : n.Contains("home") || n.Contains("hut") || n.Contains("weav") ? PinId.Home
                     : n.Contains("farm") ? PinId.Farmer : (PinId?)null;
        if (guess.HasValue && guess.Value != pin)
        {
            Debug.LogWarning("JourneyPin on '" + gameObject.name + "' was set to " + pin + ", using " + guess.Value + " from its name. Set the Pin dropdown to fix this message.", this);
            pin = guess.Value;
        }
    }

    // ---------- ash grey look (shader) ----------
    static Shader ashShader; Material ashMat; readonly Dictionary<SpriteRenderer, Material> origMat = new Dictionary<SpriteRenderer, Material>();
    float ash;
    void SetAsh(float v)
    {
        ash = v;
        if (ashShader == null) ashShader = Shader.Find("AnansiGame/SpriteAsh");
        if (ashShader == null) return;
        if (ashMat == null) ashMat = new Material(ashShader);
        ashMat.SetFloat("_Ash", v); ashMat.SetColor("_AshColor", ashColor); ashMat.SetFloat("_AshAlpha", ashOpacity);
        foreach (var r in rends)
        {
            if (!(r is SpriteRenderer sr) || sr == padlock || sr == badge || sr == nextGlow || sr == null) continue;
            if (v > 0.001f) { if (!origMat.ContainsKey(sr)) origMat[sr] = sr.sharedMaterial; sr.sharedMaterial = ashMat; }
            else if (origMat.TryGetValue(sr, out var m)) { sr.sharedMaterial = m; origMat.Remove(sr); }
        }
    }

    bool IsLocked => !AnansiRuntime.TestMode && pin != PinId.Home && !JourneyState.IsUnlocked(Id);
    bool wasLocked; bool lockKnown; float lockAlpha = 1f;

    public void Refresh(bool animate)
    {
        if (animate && !ScanFlow.MapReady) return; // map not showing: ScanFlow calls RefreshAll once the village is back, so the animation is seen
        StartCoroutineSafe(animate);
        bool locked = IsLocked;
        bool vis = showLockedPins ? JourneyState.IsVisible(Id) : JourneyState.IsUnlocked(Id) || AnansiRuntime.TestMode;
        Color tint = locked ? padlockTint : JourneyState.NeedsPot(Id) && !JourneyState.HasPot ? lockedTint : Color.white;
        kp.freezeBob = locked;
        bool unlockNow = lockKnown && wasLocked && !locked && animate && shown && vis;
        wasLocked = locked; lockKnown = true;
        if (unlockNow) { StartCoroutine(UnlockAnim(tint)); return; }
        SetAsh(locked ? 1f : 0f);
        if (locked && vis) ShowPadlock(true); else ShowPadlock(false);
        lockAlpha = locked ? .6f : 1f;
        if (vis != shown || !animate)
        {
            shown = vis; col.enabled = vis;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Fade(vis ? 1f : 0f, animate && vis ? .6f : 0f, tint));
        }
        else SetLook(1f, tint);
    }

    // ---------- padlock (drawn in code) ----------
    SpriteRenderer padlock, nextGlow; Vector3 padScale; static Sprite padS, glowS, sparkS; bool wiggling;
    void ShowPadlock(bool on)
    {
        if (on && padlock == null) BuildPadlock();
        if (padlock == null) return;
        padlock.gameObject.SetActive(on);
        if (on) { padlock.transform.localScale = padScale; padlock.transform.localRotation = Quaternion.identity; padlock.color = Color.white; padlock.transform.localPosition = PadHome(); }
    }
    Vector3 PadHome() { var b = iconSR.sprite.bounds; return new Vector3(b.center.x + b.extents.x * .62f, b.center.y - b.extents.y * .42f, -.012f * b.size.y); }
    void BuildPadlock()
    {
        if (iconSR == null || iconSR.sprite == null) return;
        var go = new GameObject("Padlock"); go.transform.SetParent(iconSR.transform, false);
        padlock = go.AddComponent<SpriteRenderer>(); padlock.sprite = PadlockSprite();
        padlock.sortingLayerID = iconSR.sortingLayerID; padlock.sortingOrder = iconSR.sortingOrder + 6;
        padScale = Vector3.one * iconSR.sprite.bounds.size.y * .36f;
        go.transform.localPosition = PadHome(); go.transform.localScale = padScale; go.SetActive(false);
    }
    IEnumerator Wiggle()
    {
        if (padlock == null || wiggling) yield break; wiggling = true;
        for (float t = 0; t < .45f; t += Time.unscaledDeltaTime) { float k = t / .45f; padlock.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI * 6f) * 18f * (1 - k)); yield return null; }
        padlock.transform.localRotation = Quaternion.identity; wiggling = false;
    }

    // Lock shakes -> pops open and falls away -> colour floods back -> pin bounces with sparkles -> starts floating.
    IEnumerator UnlockAnim(Color tint)
    {
        shown = true; col.enabled = true; foreach (var r in rends) r.enabled = true;
        if (padlock == null) BuildPadlock();
        if (padlock != null)
        {
            padlock.gameObject.SetActive(true); var home = PadHome();
            for (float t = 0; t < .5f; t += Time.unscaledDeltaTime) { float k = t / .5f; padlock.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI * 8f) * 14f * (.4f + k)); yield return null; }
            float h = iconSR.sprite.bounds.size.y;
            for (float t = 0; t < .6f; t += Time.unscaledDeltaTime)
            {
                float k = t / .6f;
                padlock.transform.localPosition = home + new Vector3(h * .25f * k, h * (.5f * k - 1.3f * k * k), 0);
                padlock.transform.localRotation = Quaternion.Euler(0, 0, -160f * k);
                padlock.transform.localScale = padScale * (1f + .2f * Mathf.Sin(Mathf.Min(1, k * 3) * Mathf.PI));
                var c = padlock.color; c.a = 1f - Mathf.Clamp01((k - .5f) / .5f); padlock.color = c;
                SetLook(1f, tint); SetAsh(1f - UIEase(k)); SetTextAlpha(Mathf.Lerp(.6f, 1f, k));
                yield return null;
            }
            padlock.gameObject.SetActive(false);
        }
        lockAlpha = 1f; SetAsh(0f); SetLook(1f, tint);
        StartCoroutine(Sparkles());
        var ic = iconSR.transform; var s0 = ic.localScale;
        for (float t = 0; t < .5f; t += Time.unscaledDeltaTime)
        {
            float k = t / .5f, sc = k < .45f ? 1f + .3f * Mathf.Sin(k / .45f * Mathf.PI * .5f) : Mathf.Lerp(1.3f, 1f, UIEase((k - .45f) / .55f));
            ic.localScale = s0 * sc; yield return null;
        }
        ic.localScale = s0;
        kp.freezeBob = false;
    }
    static float UIEase(float k) { k = Mathf.Clamp01(k); return 1f - Mathf.Pow(1f - k, 3f); }
    IEnumerator Sparkles()
    {
        if (!sparkS) sparkS = GlowSprite(true);
        var b = iconSR.sprite.bounds; var list = new List<SpriteRenderer>();
        for (int i = 0; i < 10; i++)
        {
            var g = new GameObject("UnlockSpark"); g.transform.SetParent(iconSR.transform, false);
            var r = g.AddComponent<SpriteRenderer>(); r.sprite = sparkS; r.color = new Color(1f, .86f, .45f, 1f);
            r.sortingLayerID = iconSR.sortingLayerID; r.sortingOrder = iconSR.sortingOrder + 7; list.Add(r);
        }
        for (float t = 0; t < .7f; t += Time.unscaledDeltaTime)
        {
            float k = t / .7f;
            for (int i = 0; i < list.Count; i++)
            {
                float a = i / (float)list.Count * Mathf.PI * 2f, d = b.extents.y * (.3f + 1.1f * UIEase(k));
                list[i].transform.localPosition = new Vector3(b.center.x + Mathf.Cos(a) * d, b.center.y + Mathf.Sin(a) * d, -.013f * b.size.y);
                list[i].transform.localScale = Vector3.one * b.size.y * .16f * (1f - k * .7f);
                var c = list[i].color; c.a = 1f - k; list[i].color = c;
            }
            yield return null;
        }
        foreach (var r in list) Destroy(r.gameObject);
    }

    // ---------- soft pulse on the next teacher ----------
    void LateUpdate()
    {
        if (shown && IsLocked && (padlock == null || !padlock.gameObject.activeSelf) && iconSR != null && iconSR.sprite != null) { ShowPadlock(true); if (ash < .99f) SetAsh(1f); }
        bool want = pulseNextPin && shown && !IsLocked && JourneyState.NextPin == Id && !JourneyDialogue.IsOpen;
        if (want && nextGlow == null && iconSR != null && iconSR.sprite != null)
        {
            if (!glowS) glowS = GlowSprite(false);
            var g = new GameObject("NextGlow"); g.transform.SetParent(iconSR.transform, false);
            nextGlow = g.AddComponent<SpriteRenderer>(); nextGlow.sprite = glowS;
            nextGlow.sortingLayerID = iconSR.sortingLayerID; nextGlow.sortingOrder = iconSR.sortingOrder - 1;
            var b = iconSR.sprite.bounds; g.transform.localPosition = new Vector3(b.center.x, b.center.y + b.extents.y * .2f, .002f * b.size.y);
        }
        if (nextGlow == null) return;
        nextGlow.gameObject.SetActive(want);
        if (!want) return;
        float p = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3f), h = iconSR.sprite.bounds.size.y;
        nextGlow.transform.localScale = Vector3.one * h * (1.15f + .15f * p);
        nextGlow.color = new Color(.91f, .64f, .24f, .35f + .35f * p);
    }

    static Sprite PadlockSprite()
    {
        if (padS) return padS;
        const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        Color ink = new Color32(61, 36, 22, 255), gold = new Color32(232, 163, 61, 255), goldLt = new Color32(247, 200, 110, 255), goldDk = new Color32(176, 112, 34, 255);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            var p = new Vector2(x + .5f, y + .5f); Color col = new Color(0, 0, 0, 0);
            // shackle: ring centred above the body
            float ring = Mathf.Abs(Vector2.Distance(p, new Vector2(64, 78)) - 24f);
            bool upper = p.y > 70;
            float legs = Mathf.Min(Mathf.Abs(p.x - 40f), Mathf.Abs(p.x - 88f)); bool legZone = p.y > 56 && p.y <= 78;
            float sh = upper ? ring : legZone ? legs : 99f;
            col = Over(col, ink, Mathf.Clamp01(12f - sh));
            col = Over(col, Color.Lerp(goldDk, gold, .5f), Mathf.Clamp01(7f - sh));
            // body: rounded rectangle
            float bx = Mathf.Max(Mathf.Abs(p.x - 64f) - 34f, 0), by = Mathf.Max(Mathf.Abs(p.y - 36f) - 22f, 0), bd = Mathf.Sqrt(bx * bx + by * by);
            col = Over(col, ink, Mathf.Clamp01(12f - bd));
            col = Over(col, Color.Lerp(gold, goldLt, Mathf.Clamp01((p.y - 24f) / 40f)), Mathf.Clamp01(7f - bd));
            // keyhole
            float kh = Mathf.Min(Vector2.Distance(p, new Vector2(64, 42)) - 7f, Mathf.Max(Mathf.Abs(p.x - 64f) - 3.5f, Mathf.Abs(p.y - 31f) - 9f));
            col = Over(col, ink, Mathf.Clamp01(.5f - kh) * Mathf.Clamp01(7f - bd));
            px[y * S + x] = col;
        }
        t.SetPixels(px); t.Apply();
        padS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), S);
        return padS;
    }
    static Sprite GlowSprite(bool star)
    {
        const int S = 96; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float dx = Mathf.Abs(x + .5f - S / 2f) / (S / 2f), dy = Mathf.Abs(y + .5f - S / 2f) / (S / 2f);
            float a = star ? Mathf.Clamp01(Mathf.Max(1f - (Mathf.Sqrt(dx) + Mathf.Sqrt(dy)) * .95f, 0) * 3f + Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3f))
                           : Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 2f);
            px[y * S + x] = new Color(1, 1, 1, a);
        }
        t.SetPixels(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), S);
    }

    // ---------- "completed" badge (Farmer, Carver, Drummer, Dancer only) ----------
    [Header("Completed badge")]
    [Tooltip("Green circle with a white tick on the pin once its lesson is finished.")]
    public bool showDoneBadge = true;
    SpriteRenderer badge; bool badgeShown; Vector3 badgeScale; static Sprite checkS;

    void StartCoroutineSafe(bool animate) { if (isActiveAndEnabled) StartCoroutine(BadgeNextFrame(animate)); }
    IEnumerator BadgeNextFrame(bool animate) { yield return null; UpdateBadge(animate); } // after the pin's own fade has decided if it is shown

    void UpdateBadge(bool animate)
    {
        bool want = showDoneBadge && shown && Array.IndexOf(JourneyState.Chain, Id) >= 0 && JourneyState.Done(Id);
        if (want && badge == null) BuildBadge();
        if (badge == null) return;
        if (want && !badgeShown) { badgeShown = true; badge.gameObject.SetActive(true); if (animate) StartCoroutine(BadgePop()); else badge.transform.localScale = badgeScale; }
        else if (!want && badgeShown) { badgeShown = false; badge.gameObject.SetActive(false); }
    }
    void BuildBadge()
    {
        if (iconSR == null || iconSR.sprite == null) return;
        var go = new GameObject("DoneBadge"); go.transform.SetParent(iconSR.transform, false);
        badge = go.AddComponent<SpriteRenderer>(); badge.sprite = CheckSprite();
        badge.sortingLayerID = iconSR.sortingLayerID; badge.sortingOrder = iconSR.sortingOrder + 5;
        var b = iconSR.sprite.bounds; // top-right of the pin picture, overlapping its edge a little
        badgeScale = Vector3.one * b.size.y * .34f;
        go.transform.localPosition = new Vector3(b.center.x + b.extents.x * .7f, b.center.y + b.extents.y * .66f, -.01f * b.size.y);
        go.transform.localScale = badgeScale; go.SetActive(false);
    }
    IEnumerator BadgePop()
    {
        for (float t = 0; t < .5f; t += Time.unscaledDeltaTime)
        {
            float k = t / .5f, sc = k < .55f ? 1.25f * (1f - Mathf.Pow(1f - k / .55f, 3f)) : Mathf.Lerp(1.25f, 1f, (k - .55f) / .45f);
            badge.transform.localScale = badgeScale * sc; yield return null;
        }
        badge.transform.localScale = badgeScale;
    }
    // Drawn in code: dark rim, cream ring, green disc with a soft top highlight, bold white tick with round ends and a small shadow.
    static Sprite CheckSprite()
    {
        if (checkS) return checkS;
        const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color ink = new Color32(61, 36, 22, 255), cream = new Color32(246, 235, 217, 255), green = new Color32(79, 124, 58, 255), greenLt = new Color32(108, 158, 80, 255), white = Color.white;
        Vector2 c = new Vector2(S / 2f, S / 2f), A = new Vector2(38, 66), B = new Vector2(56, 47), C = new Vector2(91, 83);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            var p = new Vector2(x + .5f, y + .5f); float d = Vector2.Distance(p, c);
            Color col = new Color(0, 0, 0, 0);
            col = Over(col, ink, Mathf.Clamp01(62f - d));
            col = Over(col, cream, Mathf.Clamp01(58f - d));
            Color g = Color.Lerp(green, greenLt, Mathf.Clamp01((p.y - 40f) / 70f));
            col = Over(col, g, Mathf.Clamp01(51f - d));
            float tick = Mathf.Min(Seg(p, A, B), Seg(p, B, C)), sh = Mathf.Min(Seg(p + new Vector2(0, 3), A, B), Seg(p + new Vector2(0, 3), B, C));
            col = Over(col, new Color(0, 0, 0, .25f), Mathf.Clamp01(7.5f - sh) * Mathf.Clamp01(51f - d));
            col = Over(col, white, Mathf.Clamp01(7f - tick));
            px[y * S + x] = col;
        }
        t.SetPixels(px); t.Apply();
        checkS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), S);
        return checkS;
    }
    static float Seg(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude); return Vector2.Distance(p, a + ab * k); }
    static Color Over(Color under, Color over, float a)
    {
        a *= over.a; float outA = a + under.a * (1 - a); if (outA <= 0) return new Color(0, 0, 0, 0);
        var rgb = ((Vector4)over * a + (Vector4)under * under.a * (1 - a)) / outA; return new Color(rgb.x, rgb.y, rgb.z, outA);
    }

    IEnumerator Fade(float to, float dur, Color tint)
    {
        if (to > 0) foreach (var r in rends) r.enabled = true;
        float t = 0;
        while (t < dur) { t += Time.unscaledDeltaTime; SetLook(Mathf.SmoothStep(0, 1, t / dur) * to, tint); yield return null; }
        SetLook(to, tint);
        if (to <= 0) foreach (var r in rends) r.enabled = false;
    }
    void SetLook(float a, Color tint)
    {
        foreach (var r in rends) if (r is SpriteRenderer sr && sr != padlock && sr != badge && sr != nextGlow) { var c = tint; c.a = a; sr.color = c; }
        foreach (var tx in texts) tx.alpha = a * lockAlpha;
        if (padlock) { var c = padlock.color; c.a = a; padlock.color = c; }
    }
    void SetTextAlpha(float a) { foreach (var tx in texts) tx.alpha = a; }

    void Open()
    {
        onOpen.Invoke();
        if (openButton) openButton.onClick.Invoke();
        else if (onOpen.GetPersistentEventCount() == 0) AnansiRuntime.OpenGameFor(pin); // no wiring needed
    }

    void Tapped()
    {
        string id = Id;
        if (IsLocked) { StartCoroutine(Wiggle()); JourneyDialogue.Think(JourneyState.LockedThought(id)); return; }
        if (pin == PinId.Home) { if (KenteBag.IsFull) JourneyDialogue.Think(bagFullThought); else Open(); return; }
        if (pin == PinId.NanaNyame) { var off = GetComponent<NanaNyameOffering>(); if (off) off.Tapped(); return; }
        if (!JourneyState.HasPot) { JourneyDialogue.Think(noPotThought); return; }
        string need = JourneyState.NeededItem(id);
        if (need != null && !JourneyState.Given(id))
        {
            JourneyDialogue.Play(new[] { new JourneyDialogue.Line(characterName, missingItemLine, characterPortrait) });
            return;
        }
        Open();
    }

    // Called when an item from the Bag is dropped on this pin. Returns true if it was accepted.
    public bool AcceptItem(string item)
    {
        if (!shown || IsLocked) return false;
        if (pin == PinId.NanaNyame && item == Bag.Kente) { var off = GetComponent<NanaNyameOffering>(); return off && off.Offer(); }
        string id = Id, need = JourneyState.NeededItem(id);
        if (need == null || item != need || JourneyState.Given(id)) return false;
        if (!JourneyState.HasPot) { JourneyDialogue.Think(noPotThought); return false; }
        if (!Bag.Take(item)) return false;
        JourneyState.SetGiven(id, true);
        JourneyDialogue.Play(new[] { new JourneyDialogue.Line(characterName, thanksLine, characterPortrait) }, Open);
        return true;
    }

    // Hook to the game's "failed / pot broke" event: the offered item goes back to the Bag.
    public void ReturnItem()
    {
        string id = Id, need = JourneyState.NeededItem(id);
        if (need != null && JourneyState.Given(id)) { Bag.Return(need); JourneyState.SetGiven(id, false); }
    }
    // Hook to the game's "completed" event (not needed for the Farmer, it does this itself).
    public void MarkComplete() { JourneyState.MarkDone(Id); RefreshAll(); }

    // For the Bag UI: find the pin under a screen point (where the player let go of a dragged item).
    public static JourneyPin At(Vector2 screenPos, Camera cam = null)
    {
        cam = cam ? cam : Camera.main; if (!cam) return null;
        return Physics.Raycast(cam.ScreenPointToRay(screenPos), out var hit) ? hit.collider.GetComponentInParent<JourneyPin>() : null;
    }
}
