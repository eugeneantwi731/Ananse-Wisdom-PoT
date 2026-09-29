using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Reusable "knowledge collected" moment for EVERY mini-game (Farmer now; Carver, Drummer, Dancer later):
// the 3D pot turns, a glowing orb dives behind the rim INTO the pot, then the pot leaps, flashes gold,
// rings ripple AROUND it (back half behind the pot, front half in front), a beam and sparks burst out of the mouth.
//
// Use:
//   var fx = PotCelebration.Create(someParentRect, orbColour);   // null if the 3D pot files are missing
//   position/size fx.Rect (keep it square), then either
//   fx.Play(onDone)                     // runs by itself
//   fx.Evaluate(secondsSinceStart)      // or drive it from your own timer
public class PotCelebration : MonoBehaviour
{
    public const float ImpactTime = 2.5f;   // seconds after start when the orb lands inside the pot
    public const float SettledTime = 3.3f;  // good moment to show your title/buttons
    public RectTransform Rect { get; private set; }
    public Color orbColor = new Color32(150, 205, 110, 255);

    const int Layer = 31;
    static int count;
    GameObject stage; Camera cam; RenderTexture rt; Transform spin;
    RawImage pot; Image glow, halo, beam;
    readonly List<Image> trail = new List<Image>(), motes = new List<Image>(), sparks = new List<Image>(), twinkles = new List<Image>();
    Image[] ringBack = new Image[2], ringFront = new Image[2];
    readonly List<Material> mats = new List<Material>(); readonly List<Color> baseCols = new List<Color>();
    static Sprite softS, starS, ringTopS, ringBotS;
    bool playing; float playT; Action onDone; int evalFrame = -10;

    public static PotCelebration Create(RectTransform parent, Color orb)
    {
        var go = new GameObject("PotCelebration", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var fx = go.AddComponent<PotCelebration>(); fx.orbColor = orb; fx.Rect = (RectTransform)go.transform;
        fx.Rect.anchorMin = fx.Rect.anchorMax = fx.Rect.pivot = new Vector2(.5f, .5f); fx.Rect.sizeDelta = new Vector2(600, 600);
        if (!fx.Build()) { Destroy(go); return null; }
        return fx;
    }

    public void Play(Action done = null) { playing = true; playT = Time.unscaledTime; onDone = done; Evaluate(0f); }
    public void Stop() { playing = false; }

    void Update()
    {
        if (!playing) return;
        float e = Time.unscaledTime - playT;
        Evaluate(e);
        if (e >= SettledTime && onDone != null) { var d = onDone; onDone = null; d(); }
    }
    void LateUpdate() { if (cam) cam.enabled = isActiveAndEnabled && Time.frameCount - evalFrame < 3; }

    // ---------------- build ----------------
    bool Build()
    {
        MakeSprites();
        var toon = Shader.Find("KenteGame/Toon");
        if (Shader.GetGlobalVector("_KenteLightDir").sqrMagnitude < .01f) Shader.SetGlobalVector("_KenteLightDir", new Vector4(.35f, .8f, .5f, 0));
        stage = new GameObject("PotCelebrationStage"); stage.transform.position = new Vector3(200f * count++, -5000, 0);
        spin = new GameObject("Spin").transform; spin.SetParent(stage.transform, false);
        var parts = new GameObject("Parts").transform; parts.SetParent(spin, false);
        bool any = false;
        foreach (var part in new[] { "pot_clay", "pot_dark", "pot_band" })
        {
            var prefab = Resources.Load<GameObject>("PotGift/" + part); if (prefab == null) continue;
            var g = Instantiate(prefab, parts); g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
            Color c = part == "pot_dark" ? (Color)new Color32(58, 26, 16, 255) : part == "pot_band" ? Color.white : (Color)new Color32(217, 106, 43, 255);
            var mat = toon != null ? new Material(toon) : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            SetCol(mat, c);
            if (part == "pot_band") { var tx = Resources.Load<Texture2D>("PotGift/band"); mat.mainTexture = tx; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tx); }
            if (mat.HasProperty("_Outline")) mat.SetFloat("_Outline", .012f);
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                var arr = new Material[r.sharedMaterials.Length]; for (int i = 0; i < arr.Length; i++) arr[i] = mat; r.sharedMaterials = arr;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; any = true;
            }
            mats.Add(mat); baseCols.Add(c);
        }
        if (!any) { Destroy(stage); return false; }
        foreach (var tr in stage.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = Layer;
        Bounds b = BoundsOf(parts); parts.position -= b.center - spin.position;
        b = BoundsOf(parts); float size = Mathf.Max(b.size.x, b.size.y);
        var cg = new GameObject("PotCamera"); cg.transform.SetParent(stage.transform, false); cg.layer = Layer;
        cam = cg.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = 1 << Layer; cam.fieldOfView = 24f;
        float dist = size * .5f / Mathf.Tan(12f * Mathf.Deg2Rad) * 1.3f;
        cam.nearClipPlane = dist * .05f; cam.farClipPlane = dist * 4f;
        cg.transform.position = spin.position + new Vector3(0, size * .3f, -dist); cg.transform.LookAt(spin.position);
        rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.enabled = false;
        foreach (var c in Camera.allCameras) if (c != cam) c.cullingMask &= ~(1 << Layer);

        // draw order: glow, orb (behind pot), back halves of rings, POT, front halves of rings, beam, sparks, stars
        glow = Img("Glow", Rect, softS, new Color(1f, .83f, .42f, 0f));
        var orbLayer = Node("OrbLayer", Rect);
        for (int q = 0; q < 12; q++) trail.Add(Img("Trail", orbLayer, softS, Clear(orbColor)));
        halo = Img("OrbHalo", orbLayer, softS, WithA(orbColor, .7f));
        for (int q = 0; q < 5; q++) motes.Add(Img("Mote", halo.rectTransform, softS, new Color(.9f, 1f, .8f, .95f)));
        var mid = Img("OrbMid", halo.rectTransform, softS, WithA(Color.Lerp(orbColor, Color.white, .35f), .95f)); Size(mid.rectTransform, .38f);
        var core = Img("OrbCore", halo.rectTransform, softS, new Color(1f, 1f, .95f, 1f)); Size(core.rectTransform, .18f);
        for (int i = 0; i < 2; i++) { ringBack[i] = Img("RingBack", Rect, ringTopS, Clear(Color.white)); ringBack[i].rectTransform.pivot = new Vector2(.5f, 0f); }
        pot = Node("Pot3D", Rect).gameObject.AddComponent<RawImage>(); pot.texture = rt; pot.raycastTarget = false; Stretch(pot.rectTransform);
        for (int i = 0; i < 2; i++) { ringFront[i] = Img("RingFront", Rect, ringBotS, Clear(Color.white)); ringFront[i].rectTransform.pivot = new Vector2(.5f, 1f); }
        beam = Img("Beam", Rect, softS, new Color(1f, .9f, .55f, 0f)); beam.rectTransform.pivot = new Vector2(.5f, .1f);
        for (int q = 0; q < 16; q++) sparks.Add(Img("Spark", Rect, q % 4 == 0 ? starS : softS, new Color(1f, .9f, .6f, 0f)));
        for (int q = 0; q < 6; q++) twinkles.Add(Img("Twinkle", Rect, starS, new Color(1f, .93f, .7f, 0f)));
        Evaluate(0f);
        return true;
    }

    // ---------------- animation ----------------
    public void Evaluate(float e)
    {
        evalFrame = Time.frameCount;
        e -= .3f; // let the screen settle first
        const float formT = .6f, flyT = 1.0f, reachT = 1.95f, hitT = ImpactTime - .3f;
        float s = Rect.rect.width; if (s < 1f) s = 600f;
        float appear = EaseOut(e / .5f), since = e - hitT;
        Vector2 mouth = new Vector2(0, s * .3f), inside = new Vector2(0, s * .02f), start = new Vector2(-s * .6f, s * 1.0f), ctrl = new Vector2(s * .25f, s * 1.35f);

        // pot: pops in, then leaps + squashes + shakes when the orb lands inside
        float sy = 1f, sx = 1f, hop = 0f, shake = 0f;
        if (since > 0f && since < .7f)
        {
            float k = since / .7f, w = Mathf.Sin(k * Mathf.PI * 2.4f) * Mathf.Exp(-k * 3.2f);
            sy = 1f - .18f * w; sx = 1f + .12f * w;
            hop = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(since / .35f) * Mathf.PI)) * s * .07f;
            shake = Mathf.Sin(since * 70f) * s * .012f * Mathf.Exp(-since * 6f);
        }
        float rise = appear < 1f ? Mathf.Lerp(.6f, 1f, appear) + Mathf.Sin(appear * Mathf.PI) * .08f : 1f;
        var pr = pot.rectTransform;
        pr.localScale = new Vector3(rise * sx, rise * sy, 1f);
        pr.anchoredPosition = new Vector2(shake, hop + (1f - appear) * -s * .1f);
        pot.color = new Color(1, 1, 1, Mathf.Clamp01(e / .3f));
        if (spin) spin.localRotation = Quaternion.Euler(0, e * 30f + (since > 0f ? 220f * (1f - Mathf.Exp(-since * 3f)) : 0f), 0);

        // pot glow
        float flash = since < 0f ? 0f : Mathf.Exp(-since * 2f);
        float pre = e < formT ? 0f : Mathf.Clamp01((e - formT) / (hitT - formT)) * .22f;
        float settle = since < 0f ? 0f : Mathf.Clamp01(since / .6f) * (.36f + .12f * Mathf.Sin(since * 3.6f));
        float g = since < 0f ? pre : Mathf.Max(flash, settle);
        Color hot = new Color(1f, .9f, .55f);
        for (int q = 0; q < mats.Count; q++) { var c = Color.Lerp(baseCols[q], hot, Mathf.Clamp01(g)) * (1f + g * .7f); c.a = 1f; SetCol(mats[q], c); }
        glow.color = new Color(1f, .83f, .42f, Mathf.Clamp01(.15f * appear + g));
        Size(glow.rectTransform, 1.5f); glow.rectTransform.localScale = Vector3.one * (.75f + .15f * appear + .7f * g);
        { float bb = since < 0f ? 0f : Mathf.Exp(-since * 2.6f); beam.color = new Color(1f, .9f, .55f, .8f * bb); beam.rectTransform.anchoredPosition = mouth;
          beam.rectTransform.sizeDelta = new Vector2(s * .34f * (1f - .4f * (1f - bb)), s * 1.3f * (.4f + .8f * (1f - bb * bb))); }

        // orb (drawn behind the pot, so it drops over the rim and disappears inside)
        bool orbOn = e > formT && e < hitT;
        halo.gameObject.SetActive(orbOn);
        float fly = Mathf.Clamp01((e - flyT) / (reachT - flyT)), fk = fly * fly;
        Vector2 Bez(float k) => (1 - k) * (1 - k) * start + 2 * (1 - k) * k * ctrl + k * k * mouth;
        if (orbOn)
        {
            float form = EaseOut((e - formT) / .35f), sink = Mathf.Clamp01((e - reachT) / (hitT - reachT));
            var hr = halo.rectTransform; Size(hr, .62f);
            hr.anchoredPosition = e < reachT ? Bez(fk) : Vector2.Lerp(mouth, inside, sink * sink);
            float pulse = 1f + .2f * Mathf.Sin(e * 24f) + .08f * Mathf.Sin(e * 57f);
            float pop = form < 1f ? form + Mathf.Sin(form * Mathf.PI) * .35f : 1f;
            hr.localScale = Vector3.one * pop * Mathf.Lerp(1f, .6f, fk) * pulse;
            halo.color = WithA(orbColor, .7f * form);
            for (int q = 0; q < motes.Count; q++)
            {
                float a = e * (7f + q) + q * 1.3f, r = s * (.12f + .03f * (q % 2));
                Size(motes[q].rectTransform, .08f);
                motes[q].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * .7f);
            }
        }
        for (int q = 0; q < trail.Count; q++)
        {
            var tr = trail[q]; float k = fk - (q + 1) * .04f;
            bool on = e > flyT && e < reachT + .12f && k > 0f;
            tr.enabled = on; if (!on) continue;
            float d = s * .2f * (1f - q / 14f); tr.rectTransform.sizeDelta = new Vector2(d, d);
            tr.rectTransform.anchoredPosition = Bez(k) + new Vector2(Mathf.Sin(e * 30f + q) * s * .01f, 0);
            tr.color = WithA(orbColor, .6f * (1f - q / (float)trail.Count) * Mathf.Clamp01((e - flyT) / .15f));
        }

        // rings lying AROUND the pot: one at the belly, one at the mouth; each starts at the pot's own width
        Ring(0, since - .02f, .8f, new Vector2(0, -s * .02f), s * .62f, s * 1.9f, .85f, s);
        Ring(1, since, .55f, mouth, s * .34f, s * 1.3f, 1f, s);

        // sparks: fountain out of the mouth
        for (int q = 0; q < sparks.Count; q++)
        {
            var sp = sparks[q];
            if (since < 0f || since > 1.1f) { sp.color = Clear(sp.color); continue; }
            float h1 = Mathf.Repeat(Mathf.Sin(q * 12.9898f) * 43758.55f, 1f), h2 = Mathf.Repeat(Mathf.Sin(q * 78.233f) * 12543.1f, 1f);
            float vx = (h1 - .5f) * s * 1.6f, vy = s * (1.2f + .9f * h2), gr = s * 3f, tt = since;
            float d = s * (q % 4 == 0 ? .09f : .035f); sp.rectTransform.sizeDelta = new Vector2(d, d);
            sp.rectTransform.anchoredPosition = mouth + new Vector2(vx * tt, vy * tt - gr * tt * tt);
            sp.rectTransform.localRotation = Quaternion.Euler(0, 0, since * 180f * (h1 - .5f));
            sp.color = WithA(sp.color, Mathf.Clamp01(1f - since / 1.1f) * Mathf.Clamp01(since / .06f));
        }
        // twinkles around the glowing pot
        for (int q = 0; q < twinkles.Count; q++)
        {
            var st = twinkles[q]; float tt = since - .35f - q * .17f;
            if (tt < 0f) { st.color = Clear(st.color); continue; }
            float ang = q / (float)twinkles.Count * Mathf.PI * 2f + .5f, rad = s * (.36f + .05f * (q % 3));
            float d = s * (.07f + .03f * (q % 2)); st.rectTransform.sizeDelta = new Vector2(d, d);
            st.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang) * rad * 1.05f, Mathf.Sin(ang) * rad * .95f + s * .02f);
            float tw = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(tt * 2.6f + q)), 3f);
            st.rectTransform.localScale = Vector3.one * (.5f + .7f * tw);
            st.rectTransform.localRotation = Quaternion.Euler(0, 0, tt * 40f);
            st.color = WithA(st.color, Mathf.Clamp01(tt / .25f) * (.25f + .75f * tw));
        }
    }

    // One flattened ring: top half drawn behind the pot, bottom half in front, so it wraps around it.
    void Ring(int i, float t, float dur, Vector2 at, float w0, float w1, float alpha, float s)
    {
        var bk = ringBack[i]; var fr = ringFront[i];
        if (t < 0f || t > dur) { bk.color = Clear(bk.color); fr.color = Clear(fr.color); return; }
        float k = t / dur, ek = 1f - Mathf.Pow(1f - k, 3f);
        float w = Mathf.Lerp(w0, w1, ek), h = w * .32f; // perspective: seen from slightly above
        var col = new Color(1f, .88f, .5f, alpha * (1f - k) * Mathf.Clamp01(t / .05f));
        foreach (var im in new[] { bk, fr }) { im.rectTransform.anchoredPosition = at; im.rectTransform.sizeDelta = new Vector2(w, h * .5f); im.color = col; }
    }

    void OnDestroy()
    {
        if (stage) Destroy(stage);
        if (rt) { rt.Release(); Destroy(rt); }
        foreach (var m in mats) if (m) Destroy(m);
    }

    // ---------------- helpers ----------------
    static void MakeSprites()
    {
        if (softS != null) return;
        softS = Tex(128, 128, (x, y) => { float d = Vector2.Distance(new Vector2(x, y), new Vector2(64, 64)) / 64f; return Mathf.Pow(Mathf.Clamp01(1f - d), 2f); }, null);
        starS = Tex(128, 128, (x, y) =>
        {
            float px = Mathf.Abs(x - 64f) / 64f, py = Mathf.Abs(y - 64f) / 64f, len = Mathf.Sqrt(px * px + py * py);
            return Mathf.Clamp01((1f - len) * (.03f / (px * py + .03f))) * Mathf.Clamp01((1f - len) * 3f);
        }, null);
        // ring: a soft gold band around an ellipse-friendly circle, split into top/bottom halves
        Texture2D rtx = null;
        Tex(256, 256, (x, y) =>
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(128, 128));
            return Mathf.Clamp01(1f - Mathf.Abs(d - 116f) / 9f) * .9f + Mathf.Clamp01(1f - Mathf.Abs(d - 116f) / 3f) * .1f;
        }, t => rtx = t);
        ringTopS = Sprite.Create(rtx, new Rect(0, 128, 256, 128), new Vector2(.5f, 0f), 100f);
        ringBotS = Sprite.Create(rtx, new Rect(0, 0, 256, 128), new Vector2(.5f, 1f), 100f);
    }
    static Sprite Tex(int w, int h, Func<float, float, float> a, Action<Texture2D> got)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a(x + .5f, y + .5f)) * 255));
        t.SetPixels32(px); t.Apply();
        got?.Invoke(t);
        return Sprite.Create(t, new Rect(0, 0, w, h), new Vector2(.5f, .5f), 100f);
    }
    static RectTransform Node(string n, Transform p)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); Stretch(r); return r;
    }
    static Image Img(string n, Transform p, Sprite s, Color c)
    {
        var go = new GameObject(n, typeof(RectTransform)); go.transform.SetParent(p, false);
        var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        var i = go.AddComponent<Image>(); i.sprite = s; i.color = c; i.raycastTarget = false; return i;
    }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    void Size(RectTransform r, float k) { float s = Rect.rect.width; if (s < 1f) s = 600f; r.sizeDelta = new Vector2(s * k, s * k); }
    static Bounds BoundsOf(Transform t)
    {
        Bounds nb = new Bounds(t.position, Vector3.zero); bool first = true;
        foreach (var r in t.GetComponentsInChildren<Renderer>()) { if (first) { nb = r.bounds; first = false; } else nb.Encapsulate(r.bounds); }
        return nb;
    }
    static void SetCol(Material m, Color c) { if (m.HasProperty("_Color")) m.SetColor("_Color", c); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); m.color = c; }
    static Color WithA(Color c, float a) { c.a = a; return c; }
    static Color Clear(Color c) { c.a = 0f; return c; }
    static float EaseOut(float k) { k = Mathf.Clamp01(k); return 1f - (1f - k) * (1f - k) * (1f - k); }
}
