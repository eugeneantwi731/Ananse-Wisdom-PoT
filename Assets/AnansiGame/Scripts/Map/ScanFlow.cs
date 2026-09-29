using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vuforia;
using Image = UnityEngine.UI.Image; // Vuforia also has an "Image"

// Village scan flow (added automatically to the village scene, no wiring):
//  - Scan guide: frame + sweeping line + "Point your camera at the village map".
//  - Map found: the village grows up from the paper (a ring ripples out, huts and trees pop up from the middle outwards,
//    pins drop in), birds fly in, then the HUDs (pot bar, Gifts) fade in.
//  - Map lost: the village is hidden at once and the scan guide comes back. Not while a mini-game is open.
public class ScanFlow : MonoBehaviour
{
    // True when the village is up (or when there is no scan flow at all, e.g. a mini-game tested on its own).
    public static bool MapReady => Instance == null || !Instance.enabled || Instance.ready;
    bool ready;
    public static ScanFlow Instance { get; private set; }

    [Tooltip("The Vuforia Image Target of the paper map. Found automatically.")]
    public ObserverBehaviour target;
    [Tooltip("Seconds the map can blink out before the village hides (stops flicker).")]
    public float lostGrace = .25f;
    [Tooltip("Extended tracking counts as 'map found'. Off = the village hides as soon as the paper leaves the camera.")]
    public bool extendedCountsAsFound = false;
    public bool birds = true;
    [Range(0, 12)] public int birdCount = 5;
    public Font font;

    static readonly Color Gold = new Color32(232, 163, 61, 255), Cream = new Color32(246, 235, 217, 255), Ink = new Color32(61, 36, 22, 255),
        Green = new Color32(79, 124, 58, 255), Brown = new Color32(107, 62, 38, 255);

    class Piece { public Transform t; public Vector3 pos, scale; public float delay; public bool ground, pin; public KnowledgePin kp; }
    readonly List<Piece> pieces = new List<Piece>();
    bool tracked, revealed, everFound; float lostAt = -1f, lookT;
    ObserverBehaviour[] observers = new ObserverBehaviour[0];
    Coroutine reveal; BirdFlock flock; float mapSize = 1f; SpriteRenderer ring;

    // guide UI
    ScanGuideUI guide; float foundT = -99f;
    readonly List<CanvasGroup> huds = new List<CanvasGroup>(); float hudAlpha;

    void Awake() { Instance = this; ready = false; }
    void OnDestroy() { if (Instance == this) Instance = null; if (target) target.OnTargetStatusChanged -= OnStatus; }

    void Start()
    {
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (AnansiTestJump.SkipScan) { Debug.Log("ScanFlow: skipped for this test (Test a Section)."); ready = true; enabled = false; return; }
        observers = FindObjectsByType<ObserverBehaviour>();
        // use the target the village actually sits under (a wrong/extra target left the guide up while the village showed)
        if (target == null || VillageScore(target) == 0)
        {
            ObserverBehaviour best = target; int bestScore = 0;
            foreach (var o in observers) { int sc = VillageScore(o); if (sc > bestScore) { bestScore = sc; best = o; } }
            target = best;
        }
        if (target == null) { Debug.LogWarning("ScanFlow: no Vuforia Image Target found, so the village shows straight away."); ready = true; enabled = false; return; }
        guide = gameObject.AddComponent<ScanGuideUI>(); guide.font = font;
        CollectPieces();
        HideVillage();
        lookT = Time.unscaledTime;
        Debug.Log("ScanFlow: watching '" + target.name + "' (" + pieces.Count + " village pieces, " + observers.Length + " Vuforia target(s) in the scene).");
    }
    static int VillageScore(ObserverBehaviour o) => o == null ? 0 : o.GetComponentsInChildren<KnowledgePin>(true).Length * 100 + o.GetComponentsInChildren<Renderer>(true).Length;
    bool IsFound(TargetStatus st) => st.Status == Status.TRACKED || (extendedCountsAsFound && st.Status == Status.EXTENDED_TRACKED);

    // checked every frame instead of waiting for Vuforia's event (the event can be missed if the map is already in view at start)
    void PollTracking()
    {
        bool any = false;
        foreach (var o in observers) if (o && o.isActiveAndEnabled && IsFound(o.TargetStatus)) { any = true; break; }
        if (any == tracked) return;
        tracked = any;
        if (any) { lostAt = -1f; if (!revealed) Reveal(); }
        else lostAt = Time.unscaledTime;
    }

    void OnStatus(ObserverBehaviour ob, TargetStatus st)
    {
        bool found = st.Status == Status.TRACKED || (extendedCountsAsFound && st.Status == Status.EXTENDED_TRACKED);
        if (found == tracked) return;
        tracked = found;
        if (found) { lostAt = -1f; if (!revealed) Reveal(); }
        else lostAt = Time.unscaledTime;
    }

    void Update()
    {
        bool gameOpen = AnansiRuntime.AnyGameOpen;
        PollTracking();
        if (!tracked && revealed && lostAt >= 0 && Time.unscaledTime - lostAt > lostGrace && !gameOpen) Lost();

        // guide
        bool foundBeat = revealed && Time.unscaledTime - foundT < .8f; // "Map found!" for a moment
        bool showGuide = (!revealed || foundBeat) && !gameOpen;
        guide.Tick(showGuide, revealed ? ScanGuideUI.Mode.Found : everFound ? ScanGuideUI.Mode.Lost : ScanGuideUI.Mode.First, Time.unscaledTime - lookT);
        if (!showGuide || revealed) lookT = Time.unscaledTime;

        // HUDs only once the village is up
        if (huds.Count == 0 || Time.frameCount % 30 == 0) CollectHuds();
        bool hudOn = ready || gameOpen; // never hide the pot while a game is using it
        hudAlpha = Mathf.MoveTowards(hudAlpha, hudOn ? 1f : 0f, Time.unscaledDeltaTime / (hudOn ? .4f : .15f));
        foreach (var g in huds) if (g) { g.alpha = hudAlpha; g.blocksRaycasts = g.interactable = hudAlpha > .9f; }
    }

    // ---------- village pieces ----------
    void CollectPieces()
    {
        pieces.Clear();
        var list = new List<Transform>();
        foreach (Transform c in target.transform) if (Usable(c)) list.Add(c);
        // a village made of a few big groups: open them up so the huts and trees can pop one by one
        for (int pass = 0; pass < 4 && list.Count < 8; pass++)
        {
            Transform best = null; int most = 1;
            foreach (var t in list) { int n = 0; foreach (Transform c in t) if (Usable(c)) n++; if (n > most) { most = n; best = t; } }
            if (best == null) break;
            if (best.GetComponent<Renderer>() != null) break; // it draws something itself, keep it whole
            list.Remove(best); foreach (Transform c in best) if (Usable(c)) list.Add(c);
        }
        // size of the map + the ground piece (largest flat thing)
        Bounds all = new Bounds(target.transform.position, Vector3.zero); bool any = false; Piece ground = null; float bestArea = 0f;
        foreach (var t in list)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            if (!any) { all = b; any = true; } else all.Encapsulate(b);
            var p = new Piece { t = t, pos = t.localPosition, scale = t.localScale };
            pieces.Add(p);
            float area = b.size.x * b.size.z;
            if (b.size.y < Mathf.Max(b.size.x, b.size.z) * .08f && area > bestArea) { bestArea = area; ground = p; }
        }
        var ib = target as ImageTargetBehaviour;
        mapSize = ib != null ? Mathf.Max(ib.GetSize().x, ib.GetSize().y) : (any ? Mathf.Max(all.size.x, all.size.z) : 1f);
        if (mapSize <= 0f) mapSize = 1f;
        if (ground != null && pieces.Count > 1) ground.ground = true;
        // pins
        foreach (var kp in target.GetComponentsInChildren<KnowledgePin>(true)) pieces.Add(new Piece { t = kp.transform, pos = kp.transform.localPosition, scale = kp.transform.localScale, pin = true, kp = kp });
        // order: from the middle outwards
        Vector3 c0 = target.transform.position; float far = 1e-4f;
        foreach (var p in pieces) far = Mathf.Max(far, Flat(p.t.position - c0).magnitude);
        int pinI = 0;
        foreach (var p in pieces)
        {
            if (p.ground) p.delay = 0f;
            else if (p.pin) p.delay = 1.55f + (pinI++) * .09f;
            else p.delay = .3f + Flat(p.t.position - c0).magnitude / far * 1.0f + Random.Range(0f, .08f);
        }
    }
    bool Usable(Transform t) => t.GetComponent<KnowledgePin>() == null && t.GetComponentInChildren<KnowledgePin>(true) == null && t.GetComponent<BirdFlock>() == null && t.GetComponentInChildren<Renderer>(true) != null && t.GetComponent<RectTransform>() == null;
    Vector3 Flat(Vector3 v) { var up = target.transform.up; return v - up * Vector3.Dot(v, up); }

    void HideVillage()
    {
        foreach (var p in pieces) { if (p.t && !p.pin) p.t.localScale = Vector3.zero; if (p.kp) p.kp.appear = 0f; }
        if (flock) flock.Hide();
    }

    // ---------- found / lost ----------
    void Reveal()
    {
        revealed = true; everFound = true; foundT = Time.unscaledTime;
        if (reveal != null) StopCoroutine(reveal);
        reveal = StartCoroutine(RevealAnim());
    }
    void Lost()
    {
        revealed = false; ready = false; lostAt = -1f;
        if (reveal != null) StopCoroutine(reveal);
        foreach (var p in pieces) if (p.t) { p.t.localPosition = p.pos; if (!p.pin) p.t.localScale = Vector3.zero; if (p.kp) p.kp.appear = 0f; }
        if (flock) flock.Hide();
        if (ring) ring.enabled = false;
        lookT = Time.unscaledTime;
    }

    IEnumerator RevealAnim()
    {
        // snap the guide frame closed for a "got it" beat
        StartCoroutine(RingRipple());
        float t0 = Time.unscaledTime, end = 0f;
        foreach (var p in pieces) end = Mathf.Max(end, p.delay + (p.ground ? .6f : p.pin ? .55f : .5f));
        Vector3 up = target.transform.up;
        while (Time.unscaledTime - t0 < end)
        {
            float t = Time.unscaledTime - t0;
            foreach (var p in pieces)
            {
                if (!p.t) continue;
                float k = Mathf.Clamp01((t - p.delay) / (p.ground ? .6f : p.pin ? .55f : .5f));
                if (p.ground) { float e = 1f - Mathf.Pow(1f - k, 3f); p.t.localScale = new Vector3(p.scale.x * Mathf.Lerp(.15f, 1f, e), p.scale.y * (k > 0 ? 1f : 0f), p.scale.z * Mathf.Lerp(.15f, 1f, e)); }
                else if (p.pin)
                {
                    // drop from above with a bounce
                    float drop = mapSize * .35f, y = k <= 0 ? drop : k < .6f ? drop * (1f - (k / .6f) * (k / .6f)) : drop * .08f * Mathf.Sin((k - .6f) / .4f * Mathf.PI);
                    p.t.localPosition = p.pos + p.t.parent.InverseTransformVector(up * y);
                    if (p.kp) p.kp.appear = k <= 0 ? 0f : Mathf.Min(1f, k / .25f);
                }
                else
                {
                    float s = k <= 0 ? 0f : k < .65f ? 1.15f * (1f - Mathf.Pow(1f - k / .65f, 3f)) : Mathf.Lerp(1.15f, 1f, (k - .65f) / .35f);
                    float rise = (1f - Mathf.Clamp01(k / .65f)) * mapSize * .04f;
                    p.t.localScale = p.scale * s;
                    p.t.localPosition = p.pos - p.t.parent.InverseTransformVector(up * rise);
                }
            }
            yield return null;
        }
        foreach (var p in pieces) if (p.t) { p.t.localPosition = p.pos; if (!p.pin) p.t.localScale = p.scale; if (p.kp) p.kp.appear = 1f; }
        if (birds && birdCount > 0)
        {
            if (flock == null) { flock = new GameObject("Birds").AddComponent<BirdFlock>(); flock.transform.SetParent(target.transform, false); }
            flock.Show(mapSize, birdCount);
        }
        ready = true;
        reveal = null;
        JourneyPin.RefreshAll(); // unlocks, badges and flights that waited for the map now play
    }

    IEnumerator RingRipple()
    {
        if (ring == null)
        {
            var g = new GameObject("RevealRing"); g.transform.SetParent(target.transform, false);
            g.transform.localRotation = Quaternion.Euler(90, 0, 0); g.transform.localPosition = Vector3.up * .002f * mapSize;
            ring = g.AddComponent<SpriteRenderer>(); ring.sprite = RingSprite(); ring.sortingOrder = 50;
        }
        ring.enabled = true;
        for (float t = 0; t < .9f; t += Time.unscaledDeltaTime)
        {
            float k = t / .9f; ring.transform.localScale = Vector3.one * mapSize * 1.3f * (1f - Mathf.Pow(1f - k, 3f));
            ring.color = new Color(Gold.r, Gold.g, Gold.b, 1f - k); yield return null;
        }
        ring.enabled = false;
    }

    // ---------- HUDs ----------
    void CollectHuds()
    {
        huds.RemoveAll(g => g == null);
        foreach (var h in FindObjectsByType<PotLifeBarHUD>()) AddHud(h.transform);
        foreach (var h in FindObjectsByType<BagBarHUD>()) AddHud(h.transform);
    }
    void AddHud(Transform t)
    {
        if (t.GetComponentInParent<FarmerGameController>() || t.GetComponentInParent<KenteWeavingGame>()) return; // HUDs inside mini-games stay as they are
        foreach (var cv in t.GetComponentsInChildren<Canvas>(true))
        {
            if (cv.transform.parent != null && cv.transform.parent.GetComponentInParent<Canvas>() != null) continue; // root canvases only
            var g = cv.GetComponent<CanvasGroup>(); if (g == null) g = cv.gameObject.AddComponent<CanvasGroup>();
            if (!huds.Contains(g)) { huds.Add(g); g.alpha = hudAlpha; }
        }
    }

    // ---------- helpers ----------
    static RectTransform Node(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return (RectTransform)g.transform; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Place(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }
    static Image NewImg(string n, Transform p, Sprite s, Color c) { var i = Node(n, p).gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false; return i; }
    Text Txt(Transform p, string s, int size, Color c, FontStyle st)
    {
        var t = Node("Text", p).gameObject.AddComponent<Text>(); t.font = font; t.text = s; t.fontSize = size; t.color = c; t.fontStyle = st;
        t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; return t;
    }
    static Sprite cornerS, lineS, roundS, ringS;
    static Sprite CornerSprite()
    {
        if (cornerS) return cornerS;
        const int S = 120, W = 14; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            // L-shape in the bottom-left, rounded outer corner
            float a = 0f;
            bool inL = (x < W && y < S - 6) || (y < W && x < S - 6);
            if (inL) a = 1f;
            if (x < 24 && y < 24) { float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(24, 24)); a = Mathf.Clamp01(24f - d) * Mathf.Clamp01(d - (24f - W) + 1f); }
            px[y * S + x] = new Color(1, 1, 1, a);
        }
        t.SetPixels(px); t.Apply();
        return cornerS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0, 0), 100);
    }
    static Sprite LineSprite()
    {
        if (lineS) return lineS;
        const int W = 4, H = 32; var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[W * H];
        for (int y = 0; y < H; y++) { float d = Mathf.Abs(y + .5f - H / 2f) / (H / 2f); for (int x = 0; x < W; x++) px[y * W + x] = new Color(1, 1, 1, Mathf.Pow(1f - d, 2.2f)); }
        t.SetPixels(px); t.Apply();
        return lineS = Sprite.Create(t, new Rect(0, 0, W, H), new Vector2(.5f, .5f), 100);
    }
    static Sprite RoundSprite()
    {
        if (roundS) return roundS;
        const int S = 96, R = 40; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x + .5f - S / 2f) - (S / 2f - R), 0), dy = Mathf.Max(Mathf.Abs(y + .5f - S / 2f) - (S / 2f - R), 0);
            px[y * S + x] = new Color(1, 1, 1, Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy)));
        }
        t.SetPixels(px); t.Apply();
        return roundS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
    }
    static Sprite RingSprite()
    {
        if (ringS) return ringS;
        const int S = 256; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
            float a = Mathf.Clamp01(1f - Mathf.Abs(d - .88f) / .1f); a = a * a;
            px[y * S + x] = new Color(1, 1, 1, a);
        }
        t.SetPixels(px); t.Apply();
        return ringS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), S);
    }
}
