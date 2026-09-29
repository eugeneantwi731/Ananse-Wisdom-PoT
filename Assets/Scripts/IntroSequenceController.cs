using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the Anansi and the Pot of Wisdom intro: title fades in, clouds drift,
/// spider drops in, huts pop up, button pops up and becomes tappable.
///
/// All anchor positions and sizes below were measured directly from the source
/// SVG (Intro_graphics_Ananse_Wisdom_Pot.svg), converted into fractions of the
/// PHONE-SAFE frame (not the full bleed canvas). X can go below 0 or above 1 on
/// purpose for elements (clouds/huts) that are meant to run off the screen edge.
/// Y is measured Unity-style: 0 = bottom of screen, 1 = top.
///
/// Canvas Scaler must be set to "Scale With Screen Size", reference resolution
/// 1170 x 2532, Match = 0.5. The pixel sizes below are in that reference space,
/// so they scale correctly to any real device.
/// </summary>
public class IntroSequenceController : MonoBehaviour
{
    [Header("Drag each Image's RectTransform here")]
    public RectTransform title;
    public RectTransform spider;
    [Tooltip("Assign in this exact order: cloud_1, cloud_2, cloud_3, cloud_4, cloud_5, cloud_6")]
    public RectTransform[] clouds = new RectTransform[6];
    [Tooltip("hut_1.png — sits on the RIGHT, bleeds off the right edge")]
    public RectTransform hutRight;
    [Tooltip("hut_2.png — sits on the LEFT, bleeds off the left edge")]
    public RectTransform hutLeft;
    public RectTransform button;
    public Button beginButton;

    [Header("Timing")]
    public float titleFadeDuration = 1.0f;
    public float spiderDropDuration = 1.2f;
    public float hutPopDuration = 0.4f;
    public float buttonPopDuration = 0.4f;
    public float cloudDriftSpeed = 15f; // reference px/sec

    [Header("Where the button sends the player")]
    public string arSceneName = "ARScan";

    // (anchorX, anchorY, width, height) — measured from the source art, in the
    // 1170x2532 reference resolution described above.
    struct Layout { public float x, y, w, h; public Layout(float x,float y,float w,float h){this.x=x;this.y=y;this.w=w;this.h=h;} }

    static readonly Layout L_Title    = new Layout(0.575f, 0.712f, 839f, 471f);
    static readonly Layout L_Spider   = new Layout(0.136f, 0.728f, 162f, 389f);
    static readonly Layout[] L_Clouds = new Layout[] {
        new Layout(0.085f, 0.900f, 945f, 285f), // cloud_1
        new Layout(0.889f, 0.548f,1126f, 307f), // cloud_2
        new Layout(0.601f, 0.818f, 246f,  63f), // cloud_3
        new Layout(0.887f, 0.942f, 855f, 230f), // cloud_4
        new Layout(0.111f, 0.603f, 409f, 125f), // cloud_5
        new Layout(1.015f, 0.748f, 618f, 148f), // cloud_6 (intentionally off the right edge)
    };
    static readonly Layout L_HutRight = new Layout(0.900f, 0.352f, 724f, 875f);
    static readonly Layout L_HutLeft  = new Layout(0.117f, 0.359f, 713f, 844f);
    static readonly Layout L_Button   = new Layout(0.499f, 0.211f, 993f, 172f);

    CanvasGroup titleCG;

    void Awake()
    {
        Place(title, L_Title);
        Place(spider, L_Spider);
        for (int i = 0; i < clouds.Length && i < L_Clouds.Length; i++)
            Place(clouds[i], L_Clouds[i]);
        Place(hutRight, L_HutRight);
        Place(hutLeft, L_HutLeft);
        Place(button, L_Button);

        // starting states for the elements that animate in
        titleCG = GetOrAddCanvasGroup(title);
        titleCG.alpha = 0f;

        spider.anchoredPosition += new Vector2(0f, 600f); // starts above its resting spot

        hutRight.localScale = Vector3.zero;
        hutLeft.localScale = Vector3.zero;
        button.localScale = Vector3.zero;

        beginButton.interactable = false;
        beginButton.onClick.AddListener(OnBeginJourneyPressed);
    }

    void Place(RectTransform rt, Layout l)
    {
        rt.anchorMin = new Vector2(l.x, l.y);
        rt.anchorMax = new Vector2(l.x, l.y);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(l.w, l.h);
        rt.anchoredPosition = Vector2.zero;
    }

    CanvasGroup GetOrAddCanvasGroup(RectTransform rt)
    {
        var cg = rt.GetComponent<CanvasGroup>();
        if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    void Start()
    {
        StartCoroutine(PlayIntro());
    }

    void Update()
    {
        // continuous background cloud drift, left to right, looping
        foreach (var cloud in clouds)
        {
            if (cloud == null) continue;
            var pos = cloud.anchoredPosition;
            pos.x += cloudDriftSpeed * Time.deltaTime;
            if (pos.x > cloud.rect.width + 200f)
                pos.x = -(cloud.rect.width + 200f);
            cloud.anchoredPosition = pos;
        }
    }

    IEnumerator PlayIntro()
    {
        yield return Fade(titleCG, 0f, 1f, titleFadeDuration);
        yield return DropIn(spider, spiderDropDuration);
        yield return PopIn(hutLeft, hutPopDuration);
        yield return PopIn(hutRight, hutPopDuration);
        yield return PopIn(button, buttonPopDuration);
        beginButton.interactable = true;
    }

    IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    IEnumerator DropIn(RectTransform rt, float duration)
    {
        Vector2 start = rt.anchoredPosition;      // currently elevated (+600)
        Vector2 end = start - new Vector2(0f, 600f); // its real resting spot
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float eased = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
            rt.anchoredPosition = Vector2.Lerp(start, end, eased);
            yield return null;
        }
        rt.anchoredPosition = end;
    }

    IEnumerator PopIn(RectTransform rt, float duration)
    {
        const float overshoot = 1.2f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = t / duration;
            float scale = p < 0.7f
                ? Mathf.Lerp(0f, overshoot, p / 0.7f)
                : Mathf.Lerp(overshoot, 1f, (p - 0.7f) / 0.3f);
            rt.localScale = Vector3.one * scale;
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    public void OnBeginJourneyPressed()
    {
        SceneManager.LoadScene(arSceneName);
    }
}
