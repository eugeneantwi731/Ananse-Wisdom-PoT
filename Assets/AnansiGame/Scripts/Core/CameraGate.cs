using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Asks for the camera before the AR village opens. Used by your IntroSequence (Leave) like this:
//   bool ok = false; yield return CameraGate.Ensure(r => ok = r);
// First time: a friendly card ("Anansi needs your camera") -> the phone's own prompt.
// If the player said no: a card with "Open Settings" (the game needs the camera). Coming back with it on continues by itself.
// Editor test: Tools > Anansi Game > Test: camera denied.
public class CameraGate : MonoBehaviour
{
    public const string AskedKey = "Anansi_CameraAsked", SimKey = "Anansi_SimCameraDenied";
    static readonly Color Brown = new Color32(107, 62, 38, 255), Gold = new Color32(232, 163, 61, 255), GoldDk = new Color32(176, 112, 34, 255),
        Green = new Color32(79, 124, 58, 255), GreenDk = new Color32(52, 86, 36, 255), Cream = new Color32(246, 235, 217, 255), Ink = new Color32(61, 36, 22, 255), Tan = new Color32(200, 170, 130, 255),
        TitleGold = new Color32(246, 201, 122, 255), BodyCream = new Color32(236, 220, 192, 255), Shade = new Color32(30, 14, 6, 255);

    static CameraGate inst;
    RectTransform root, card; Font font; bool waitingSettings; Action<bool> pending;

    public static bool HasCamera
    {
        get
        {
#if UNITY_EDITOR
            if (PlayerPrefs.GetInt(SimKey, 0) == 1) return false;
#endif
            return Application.HasUserAuthorization(UserAuthorization.WebCam);
        }
    }

    public static IEnumerator Ensure(Action<bool> done)
    {
        if (HasCamera) { done?.Invoke(true); yield break; }
        if (inst == null) inst = new GameObject("CameraGate").AddComponent<CameraGate>();
        bool? result = null;
        yield return inst.Run(r => result = r);
        while (result == null) yield return null;
        done?.Invoke(result.Value);
    }

    // A yes/no card in the same style (used by the intro's "New Journey").
    bool swapStyle; // confirm cards: Cancel gets the filled (safe) style, the action gets the outline
    public static void Confirm(string head, string body, string yes, string no, Action<bool> done)
    {
        if (inst == null) inst = new GameObject("CameraGate").AddComponent<CameraGate>();
        inst.swapStyle = true;
        inst.Build();
        inst.ShowCard(head, body, yes, () => { inst.HideCard(); done?.Invoke(true); }, no, () => { inst.HideCard(); done?.Invoke(false); }, true, RestartIcon());
    }

    IEnumerator Run(Action<bool> done)
    {
        swapStyle = false;
        Build();
        if (PlayerPrefs.GetInt(AskedKey, 0) == 0)
        {
            bool? allow = null;
            ShowCard("Anansi needs your camera", "The village lives on your paper map. Your camera lets Anansi see the map and bring the village to life.",
                "Allow camera", () => allow = true, "Not now", () => allow = false, false);
            while (allow == null) yield return null;
            HideCard();
            if (allow == false) { done(false); yield break; }
            PlayerPrefs.SetInt(AskedKey, 1); PlayerPrefs.Save();
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (HasCamera) { done(true); yield break; }
        }
        // said no before (iOS only asks once): send them to Settings
        pending = done; waitingSettings = true;
        ShowCard("The camera is turned off", "Anansi's village only appears through your camera.\nOpen Settings, tap this game and turn on Camera.",
            "Open Settings", OpenSettings, "Back", () => Finish(false), true);
    }

    void Finish(bool ok) { waitingSettings = false; HideCard(); var p = pending; pending = null; p?.Invoke(ok); }
    void OnApplicationFocus(bool focus) { if (focus && waitingSettings && HasCamera) Finish(true); }
#if UNITY_EDITOR
    void Update() { if (waitingSettings && HasCamera) Finish(true); }
#endif

    static void OpenSettings()
    {
#if UNITY_IOS && !UNITY_EDITOR
        Application.OpenURL("app-settings:");
#elif UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var uriC = new AndroidJavaClass("android.net.Uri"))
            using (var uri = uriC.CallStatic<AndroidJavaObject>("fromParts", "package", act.Call<string>("getPackageName"), null))
            using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                act.Call("startActivity", intent);
        }
        catch (Exception e) { Debug.LogWarning("CameraGate: could not open Settings. " + e.Message); }
#else
        Debug.Log("CameraGate: on the iPhone this opens the game's page in Settings. In the Editor, turn off Tools > Anansi Game > Test: camera denied to continue.");
#endif
    }

    // ---------- UI ----------
    void Build()
    {
        if (root != null) return;
        AnansiRuntime.EnsureEventSystem();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var cgo = new GameObject("CameraGateCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.transform.SetParent(transform, false);
        var cv = cgo.GetComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 950;
        var sc = cgo.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        root = (RectTransform)cgo.transform;
    }

    void ShowCard(string head, string body, string yes, Action onYes, string no, Action onNo, bool warm, Sprite iconSprite = null)
    {
        HideCard();
        bool portrait = Screen.height > Screen.width;
        var dim = Img("CardDim", root, null, new Color(0, 0, 0, .55f)); Stretch(dim.rectTransform); card = dim.rectTransform;
        var box = Img("Card", card, Framed(56, false), Color.white); box.type = Image.Type.Sliced; // same look as your Begin button: brown face, gold rim
        var br = box.rectTransform; br.anchorMin = br.anchorMax = new Vector2(.5f, .5f); br.sizeDelta = portrait ? new Vector2(900, 760) : new Vector2(940, 580);
        var sh0 = box.gameObject.AddComponent<Shadow>(); sh0.effectColor = new Color(0, 0, 0, .45f); sh0.effectDistance = new Vector2(0, -14);
        float top = br.sizeDelta.y / 2f;
        var icon = Img("Icon", br, iconSprite != null ? iconSprite : CameraIcon(), Color.white); Place(icon.rectTransform, 0, top - 95, 110, 110);
        var h = Txt(br, head, 52, TitleGold, FontStyle.Bold); var hs = h.gameObject.AddComponent<Shadow>(); hs.effectColor = Shade; hs.effectDistance = new Vector2(0, -3); Place(h.rectTransform, 0, top - 195, 840, 70);
        var b = Txt(br, body, 34, BodyCream, FontStyle.Normal); Place(b.rectTransform, 0, top - 300, 800, 150);
        if (portrait)
        {
            Place(Btn(br, yes, !swapStyle, onYes, 38), 0, -top + 196, 620, 104);
            Place(Btn(br, no, swapStyle, onNo, 38), 0, -top + 76, 620, 104);
        }
        else
        {
            Place(Btn(br, yes, !swapStyle, onYes, 38), -205, -top + 95, 380, 104);   // action on the left
            Place(Btn(br, no, swapStyle, onNo, 38), 205, -top + 95, 380, 104);     // cancel on the right: same size, gold outline
        }
        StartCoroutine(Pop(br));
    }
    void HideCard() { if (card) Destroy(card.gameObject); card = null; }
    IEnumerator Pop(RectTransform r)
    {
        for (float t = 0; t < .3f; t += Time.unscaledDeltaTime) { if (!r) yield break; float k = t / .3f; r.localScale = Vector3.one * (k < .6f ? Mathf.Lerp(.85f, 1.04f, k / .6f) : Mathf.Lerp(1.04f, 1f, (k - .6f) / .4f)); yield return null; }
        if (r) r.localScale = Vector3.one;
    }

    static RectTransform Node(string n, Transform p) { var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false); return (RectTransform)g.transform; }
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
    static void Place(RectTransform r, float x, float y, float w, float h) { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h); }
    static Image Img(string n, Transform p, Sprite s, Color c) { var i = Node(n, p).gameObject.AddComponent<Image>(); i.sprite = s; i.color = c; return i; }
    Text Txt(Transform p, string s, int size, Color c, FontStyle st)
    {
        var t = Node("Text", p).gameObject.AddComponent<Text>(); t.font = font; t.text = s; t.fontSize = size; t.color = c; t.fontStyle = st; t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false; return t;
    }
    RectTransform Btn(Transform p, string label, bool primary, Action onClick, int size)
    {
        var i = Img(label, p, primary ? OptionsMenu.GoldPill() : Framed(54, true), Color.white); i.type = Image.Type.Sliced;
        if (primary) { var s = i.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, .4f); s.effectDistance = new Vector2(0, -8); }
        i.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        var t = Txt(i.transform, label, size, primary ? (Color)new Color32(61, 30, 12, 255) : Gold, FontStyle.Bold); Stretch(t.rectTransform);
        var ts = t.gameObject.AddComponent<Shadow>();
        if (primary) { ts.effectColor = new Color(1f, .93f, .75f, .45f); ts.effectDistance = new Vector2(0, -2); } // gold button, brown text (like Keep Playing)
        else { ts.effectColor = Shade; ts.effectDistance = new Vector2(0, -3); }
        return i.rectTransform;
    }

    // Rounded panel with a gold rim (light at the top, deeper at the bottom) and a warm brown face that is lighter in the middle.
    // outlineOnly: just the gold rim (for the quiet "Back" / "Not now" button).
    static readonly System.Collections.Generic.Dictionary<int, Sprite> framed = new System.Collections.Generic.Dictionary<int, Sprite>();
    public static Sprite Framed(int R, bool outlineOnly)
    {
        int key = R * 2 + (outlineOnly ? 1 : 0); if (framed.TryGetValue(key, out var cached) && cached) return cached;
        int S = R * 2 + 4; float rim = outlineOnly ? 5f : 7f;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        Color rimTop = new Color32(246, 201, 122, 255), rimBot = new Color32(184, 118, 46, 255), faceMid = new Color32(106, 52, 22, 255), faceEdge = new Color32(52, 24, 10, 255);
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float dx = Mathf.Max(Mathf.Abs(x + .5f - S / 2f) - (S / 2f - R), 0), dy = Mathf.Max(Mathf.Abs(y + .5f - S / 2f) - (S / 2f - R), 0);
            float d = R - Mathf.Sqrt(dx * dx + dy * dy); // distance inside the edge
            float outer = Mathf.Clamp01(d), inner = Mathf.Clamp01(d - rim);
            float v = y / (float)S;
            Color rimC = Color.Lerp(rimBot, rimTop, v);
            Color face = outlineOnly ? new Color(0, 0, 0, 0) : Color.Lerp(faceEdge, faceMid, Mathf.Clamp01(inner * .06f) * (.55f + .45f * v));
            Color c = Color.Lerp(rimC, face, inner); c.a = outlineOnly ? outer * (1f - inner) : outer;
            px[y * S + x] = c;
        }
        t.SetPixels(px); t.Apply();
        var sp = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R + 2, R + 2, R + 2, R + 2));
        framed[key] = sp; return sp;
    }

    static Sprite roundS, camS;
    static Sprite Rounded()
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
    static Sprite restartS;
    static Sprite RestartIcon()
    {
        if (restartS) return restartS;
        var t = Resources.Load<Texture2D>("Intro/UI/restart_icon");
        return restartS = t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100) : CameraIcon();
    }
    static Sprite CameraIcon()
    {
        if (camS) return camS;
        const int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp }; var px = new Color[S * S];
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            var p = new Vector2(x + .5f, y + .5f); Color c = new Color(0, 0, 0, 0);
            float bx = Mathf.Max(Mathf.Abs(p.x - 64) - 40, 0), by = Mathf.Max(Mathf.Abs(p.y - 56) - 26, 0), body = Mathf.Sqrt(bx * bx + by * by) - 10;
            float tx = Mathf.Max(Mathf.Abs(p.x - 64) - 16, 0), ty = Mathf.Max(Mathf.Abs(p.y - 92) - 6, 0), top = Mathf.Sqrt(tx * tx + ty * ty) - 6;
            float shape = Mathf.Min(body, top), lens = Vector2.Distance(p, new Vector2(64, 56));
            c = Mix(c, Ink, 1f - (shape - 5f)); c = Mix(c, Gold, 1f - shape);
            c = Mix(c, Ink, 26f - lens); c = Mix(c, Cream, 19f - lens); c = Mix(c, Brown, 12f - lens);
            px[y * S + x] = c;
        }
        t.SetPixels(px); t.Apply();
        return camS = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100);
    }
    static Color Mix(Color under, Color over, float a) { a = Mathf.Clamp01(a); var c = Color.Lerp(under, over, a); c.a = Mathf.Max(under.a, a); return c; }
}
