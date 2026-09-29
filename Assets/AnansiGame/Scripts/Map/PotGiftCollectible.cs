using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Nana Nyame's pot gift. When he offers a pot, his whole pin is replaced by the 3D pot (Resources/PotGift/pot_clay, pot_dark, pot_band),
// spinning with a soft glow, "+N" and "Tap to collect". Tapping collects it: the pot flies to the pot bar and his picture comes back.
// Lives on the Nana Nyame pin (added by NanaNyameOffering).
public class PotGiftCollectible : MonoBehaviour
{
    public static PotGiftCollectible Current { get; private set; }

    SpriteRenderer icon, glow; Sprite original, potPin; TextMeshPro plus, tap; Transform pot3D;
    [Tooltip("Pot height compared with the pin picture it replaces.")]
    public float potSize = .52f;
    readonly System.Collections.Generic.List<SpriteRenderer> sparks = new System.Collections.Generic.List<SpriteRenderer>();
    static Sprite starS;
    Vector3 iconScale; float t0; bool collecting;
    static Sprite glowS;
    static readonly Color Gold = new Color32(232, 163, 61, 255), Ink = new Color32(61, 36, 22, 255), Cream = new Color32(246, 235, 217, 255);

    public static PotGiftCollectible Show(Component pin, int amount)
    {
        var kp = pin.GetComponent<KnowledgePin>();
        var sr = kp != null && kp.icon != null ? kp.icon.GetComponent<SpriteRenderer>() : null;
        if (sr == null) { Debug.LogWarning("PotGift: the Nana Nyame pin has no Icon picture."); return null; }
        var c = pin.GetComponent<PotGiftCollectible>(); if (c == null) c = pin.gameObject.AddComponent<PotGiftCollectible>();
        c.Begin(sr, amount);
        return c;
    }

    void Begin(SpriteRenderer sr, int amount)
    {
        Current = this; icon = sr; collecting = false; t0 = Time.unscaledTime;
        if (original == null) { original = sr.sprite; iconScale = sr.transform.localScale; }
        var b = original.bounds; // local to the icon
        StopAllCoroutines();
        if (pot3D == null) pot3D = BuildPot(b);
        if (pot3D != null) { HidePin(true); StartCoroutine(PopIn()); } // the pot takes the picture's place
        else { var p = JourneyDialogue.LoadArt("Pins/pin_pot"); potPin = p != null ? KnowledgePin.MatchSize(p, original) : original; StartCoroutine(Flip(potPin)); } // fallback
        if (glow == null)
        {
            if (!glowS) glowS = Radial();
            var g = new GameObject("PotGlow"); g.transform.SetParent(icon.transform, false);
            glow = g.AddComponent<SpriteRenderer>(); glow.sprite = glowS; glow.color = new Color(Gold.r, Gold.g, Gold.b, .75f);
            glow.sortingLayerID = icon.sortingLayerID; glow.sortingOrder = icon.sortingOrder - 1;
            g.transform.localPosition = new Vector3(b.center.x, b.center.y + b.extents.y * .25f, .001f);
        }
        if (plus == null) plus = Label("Plus", Gold, b.size.y * .3f, b.max.y + b.size.y * .22f, b);
        if (tap == null) tap = Label("Tap", Cream, b.size.y * .13f, b.max.y + b.size.y * .04f, b);
        plus.text = "+" + amount; tap.text = "Tap to collect";
        if (sparks.Count == 0 && pot3D != null)
        {
            if (!starS) starS = Star();
            for (int i = 0; i < 8; i++)
            {
                var g = new GameObject("PotSparkle" + i); g.transform.SetParent(icon.transform, false);
                var r = g.AddComponent<SpriteRenderer>(); r.sprite = starS; r.color = new Color(1f, .91f, .64f, 1f);
                r.sortingLayerID = icon.sortingLayerID; r.sortingOrder = icon.sortingOrder + 2;
                sparks.Add(r);
            }
        }
    }

    TextMeshPro Label(string name, Color c, float h, float y, Bounds b)
    {
        var go = new GameObject(name); go.transform.SetParent(icon.transform, false); go.transform.localPosition = new Vector3(b.center.x, y, -.002f);
        var t = go.AddComponent<TextMeshPro>();
        t.color = c; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(b.size.x * 1.6f, h);
        t.enableAutoSizing = true; t.fontSizeMin = .05f; t.fontSizeMax = 60f; t.textWrappingMode = TextWrappingModes.NoWrap;
        t.outlineWidth = .3f; t.outlineColor = Ink;
        var mr = go.GetComponent<MeshRenderer>(); if (mr) { mr.sortingLayerID = icon.sortingLayerID; mr.sortingOrder = icon.sortingOrder + 6; }
        return t;
    }

    // The pot is 3 small files (one material each, no effects): PotGift/pot_clay, pot_dark, pot_band.
    Transform BuildPot(Bounds b)
    {
        var holder = new GameObject("Pot3D").transform; holder.SetParent(transform, false);
        var toon = Shader.Find("KenteGame/Toon");
        if (Shader.GetGlobalVector("_KenteLightDir").sqrMagnitude < .01f) Shader.SetGlobalVector("_KenteLightDir", new Vector4(.35f, .8f, .5f, 0));
        bool any = false;
        foreach (var part in new[] { "pot_clay", "pot_dark", "pot_band" })
        {
            var prefab = Resources.Load<GameObject>("PotGift/" + part);
            if (prefab == null) { Debug.LogWarning("PotGift: Resources/PotGift/" + part + ".obj is missing."); continue; }
            var go = Instantiate(prefab, holder); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
            Color c = part == "pot_dark" ? (Color)new Color32(58, 26, 16, 255) : part == "pot_band" ? Color.white : (Color)new Color32(217, 106, 43, 255);
            var mat = toon != null ? new Material(toon) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c); if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); mat.color = c;
            if (part == "pot_band") { var t = Resources.Load<Texture2D>("PotGift/band"); mat.mainTexture = t; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t); }
            if (mat.HasProperty("_Outline")) mat.SetFloat("_Outline", .012f);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { var arr = new Material[r.sharedMaterials.Length]; for (int i = 0; i < arr.Length; i++) arr[i] = mat; r.sharedMaterials = arr; any = true; }
        }
        if (!any) { Destroy(holder.gameObject); return null; }
        // size in world units: pot height = potSize x the pin picture's height
        holder.localScale = Vector3.one;
        Bounds pb = WorldBounds(holder);
        float picH = icon.transform.TransformVector(new Vector3(0, b.size.y, 0)).magnitude;
        holder.localScale *= picH * potSize / Mathf.Max(1e-6f, pb.size.y);
        holder.position += icon.transform.TransformPoint(b.center) - WorldBounds(holder).center;
        return holder;
    }
    static Bounds WorldBounds(Transform t)
    {
        Bounds nb = new Bounds(); bool first = true;
        foreach (var r in t.GetComponentsInChildren<Renderer>()) { if (first) { nb = r.bounds; first = false; } else nb.Encapsulate(r.bounds); }
        return nb;
    }
    // Hide every pin picture while the pot stands in for it (other scripts may switch them back on, so keep them hidden).
    readonly System.Collections.Generic.List<SpriteRenderer> hidden = new System.Collections.Generic.List<SpriteRenderer>();
    void HidePin(bool hide)
    {
        if (hide)
        {
            hidden.Clear();
            float bottom = original.bounds.min.y;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (r == glow || !r.enabled || sparks.Contains(r)) continue;
                if (IsLabel(r.transform)) continue;                                            // "Nana Nyame" plate stays
                if (r != icon && icon.transform.InverseTransformPoint(r.bounds.center).y < bottom) continue; // anything under the picture stays
                hidden.Add(r);
            }
        }
        foreach (var r in hidden) if (r) r.enabled = !hide;
        if (!hide) hidden.Clear();
    }
    static bool IsLabel(Transform t)
    {
        for (var p = t; p != null && p.GetComponent<PotGiftCollectible>() == null; p = p.parent)
        {
            string n = p.name.ToLowerInvariant();
            if (n.Contains("label") || n.Contains("name") || n.Contains("plate") || n.Contains("caption") || n.Contains("title")) return true;
            if (p.GetComponent<TMP_Text>() != null) return true;
        }
        foreach (Transform c in t) if (c.GetComponent<TMP_Text>() != null) return true;
        return false;
    }
    static Sprite Star()
    {
        const int N = 64; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false); var px = new Color32[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
        {
            float dx = Mathf.Abs(x - N / 2f + .5f) / (N / 2f), dy = Mathf.Abs(y - N / 2f + .5f) / (N / 2f);
            float star = Mathf.Clamp01(1f - (Mathf.Sqrt(dx) + Mathf.Sqrt(dy)) * .95f) * 3f;
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3.2f);
            float a = Mathf.Clamp01(Mathf.Max(star, core));
            px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px); tex.Apply(); tex.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N);
    }
    void LateUpdate() { if (pot3D && !collecting) foreach (var r in hidden) if (r && r.enabled) r.enabled = false; }
    IEnumerator PopIn()
    {
        var baseS = pot3D.localScale;
        for (float t = 0; t < .45f; t += Time.unscaledDeltaTime) { float k = t / .45f, s = k < .6f ? 1.2f * (k / .6f) : Mathf.Lerp(1.2f, 1f, (k - .6f) / .4f); pot3D.localScale = baseS * s; yield return null; }
        pot3D.localScale = baseS;
    }

    void Update()
    {
        if (collecting || icon == null) return;
        float t = Time.unscaledTime - t0;
        if (pot3D) { pot3D.localRotation = Quaternion.Euler(0, t * 70f, 0); }
        if (sparks.Count > 0)
        {
            var bb = original.bounds; float h = bb.size.y;
            for (int i = 0; i < sparks.Count; i++)
            {
                float ph = i * 0.785f, a = ph + t * .5f, rad = h * (.42f + .08f * (i % 3));
                float tw = Mathf.Max(0f, Mathf.Sin(t * 3f + i * 1.7f));
                sparks[i].transform.localPosition = new Vector3(bb.center.x + Mathf.Cos(a) * rad, bb.center.y + Mathf.Sin(a) * rad * .8f, -.002f);
                sparks[i].transform.localScale = Vector3.one * h * (.05f + .06f * (i % 3 == 2 ? 1f : .5f)) * (.3f + .9f * tw);
                sparks[i].transform.localRotation = Quaternion.Euler(0, 0, t * 40f + i * 20f);
                var c = sparks[i].color; c.a = .25f + .75f * tw; sparks[i].color = c;
            }
        }
        if (glow) { glow.transform.localScale = Vector3.one * original.bounds.size.y * 1.35f * (1f + .08f * Mathf.Sin(t * 3.2f)); var c = glow.color; c.a = .55f + .25f * Mathf.Sin(t * 3.2f); glow.color = c; }
        if (plus) plus.transform.localScale = Vector3.one * (1f + .06f * Mathf.Sin(t * 4f));
        if (tap) { var c = tap.color; c.a = .6f + .4f * Mathf.Sin(t * 4f); tap.color = c; }
    }

    // Turn the pin over like a card and show the other picture.
    IEnumerator Flip(Sprite to)
    {
        var tr = icon.transform;
        for (float t = 0; t < .15f; t += Time.unscaledDeltaTime) { tr.localScale = new Vector3(iconScale.x * (1f - t / .15f), iconScale.y, iconScale.z); yield return null; }
        icon.sprite = to;
        for (float t = 0; t < .3f; t += Time.unscaledDeltaTime)
        {
            float k = t / .3f, s = k < .6f ? 1.15f * (k / .6f) : Mathf.Lerp(1.15f, 1f, (k - .6f) / .4f);
            tr.localScale = new Vector3(iconScale.x * s, iconScale.y * (k < .6f ? 1f + .1f * k : 1f), iconScale.z); yield return null;
        }
        tr.localScale = iconScale;
    }

    public void Collect(Action done)
    {
        if (collecting) return;
        collecting = true;
        var cam = Camera.main;
        Vector3 w = glow ? glow.transform.position : icon.transform.position;
        Vector2 from = cam ? (Vector2)cam.WorldToScreenPoint(w) : new Vector2(Screen.width / 2f, Screen.height / 2f);
        PotGiftFlyer.Launch(from, done);
        if (plus) Destroy(plus.gameObject); if (tap) Destroy(tap.gameObject); if (glow) Destroy(glow.gameObject);
        if (pot3D) Destroy(pot3D.gameObject);
        foreach (var sp in sparks) if (sp) Destroy(sp.gameObject); sparks.Clear();
        StopAllCoroutines(); HidePin(false); icon.enabled = true; StartCoroutine(FinishFlip()); // Nana Nyame's picture comes back
    }
    IEnumerator FinishFlip() { icon.sprite = potPin != null ? potPin : original; yield return Flip(original); Current = null; Destroy(this); }

    void OnDestroy() { if (Current == this) Current = null; HidePin(false); if (icon != null && original != null && !collecting) { icon.enabled = true; icon.sprite = original; icon.transform.localScale = iconScale; } }

    static Sprite Radial()
    {
        const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(S / 2f, S / 2f)) / (S / 2f);
            float a = Mathf.Clamp01(1f - d); a *= a;
            px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        t.SetPixels32(px); t.Apply();
        return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), S);
    }
}

// The 2D pot flying into the pot bar (own overlay canvas, removes itself).
public class PotGiftFlyer : MonoBehaviour
{
    RectTransform r; Vector2 a, b, ctrl; float h0, h1, t0; Action done; bool fired;
    const float Dur = .75f;

    public static void Launch(Vector2 from, Action onDone)
    {
        var hud = PotLifeBarHUD.Map;
        Vector2 to = hud != null ? hud.PotScreenPoint : new Vector2(Screen.height * .12f, Screen.height * .88f);
        float endH = hud != null ? Mathf.Max(20f, hud.PotScreenHeight) : Screen.height * .12f;
        var cgo = new GameObject("PotGiftFlyer", typeof(RectTransform), typeof(Canvas));
        var canvas = cgo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 700;
        var ig = new GameObject("Pot", typeof(RectTransform), typeof(Image)); ig.transform.SetParent(cgo.transform, false);
        var img = ig.GetComponent<Image>(); img.raycastTarget = false; img.preserveAspect = true;
        img.sprite = JourneyDialogue.LoadArt("PotLifeBar/pot_3");
        cgo.AddComponent<PotGiftFlyer>().Begin((RectTransform)ig.transform, from, to, Screen.height * .13f, endH, onDone);
    }

    public void Begin(RectTransform rect, Vector2 from, Vector2 to, float startH, float endH, Action onDone)
    {
        r = rect; a = from; b = to; h0 = startH; h1 = endH; done = onDone; t0 = Time.unscaledTime;
        ctrl = new Vector2(Mathf.Lerp(a.x, b.x, .35f), Mathf.Max(a.y, b.y) + Screen.height * .12f);
        Place(0);
    }
    void Update()
    {
        float k = Mathf.Clamp01((Time.unscaledTime - t0) / Dur);
        Place(k);
        if (k >= 1f && !fired) { fired = true; done?.Invoke(); Destroy(gameObject, .05f); }
    }
    void Place(float k)
    {
        float e = k * k * (3f - 2f * k);
        Vector2 p = (1 - e) * (1 - e) * a + 2 * (1 - e) * e * ctrl + e * e * b;
        r.position = p;
        float h = Mathf.Lerp(h0, h1, e) * (1f + .12f * Mathf.Sin(k * Mathf.PI));
        r.sizeDelta = new Vector2(h, h);
    }
}
