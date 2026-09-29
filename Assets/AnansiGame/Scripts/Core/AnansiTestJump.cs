using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Runtime half of Tools > Anansi Game > Test a Section. The editor window saves a section id, presses Play,
// and this opens the game straight at that part (once; a normal Play afterwards is untouched).
public static class AnansiTestJump
{
    public const string Key = "Anansi_TestJump", SkipScanKey = "Anansi_TestSkipScan";
    public static bool SkipScan { get; private set; }
    public static string Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Current = PlayerPrefs.GetString(Key, "");
        SkipScan = Current != "" && PlayerPrefs.GetInt(SkipScanKey, 1) == 1;
        if (Current == "") return;
        PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save();
        Debug.Log("Anansi Game: testing section '" + Current + "'" + (SkipScan ? " (map scan skipped)." : "."));
        var go = new GameObject("AnansiTestJump") { hideFlags = HideFlags.HideInHierarchy };
        go.AddComponent<Runner>().StartCoroutine(Run(Current));
    }
    class Runner : MonoBehaviour { }

    static IEnumerator Run(string id)
    {
        yield return null; yield return null;
        float wait = 0f; while (!ScanFlow.MapReady && wait < 60f) { wait += Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForSecondsRealtime(.4f);
        string group = id, part = "";
        int c = id.IndexOf(':'); if (c > 0) { group = id.Substring(0, c); part = id.Substring(c + 1); }
        switch (group)
        {
            case "kente":
            {
                var g = Find<KenteWeavingGame>("KenteWeavingGame"); if (g == null) break;
                yield return null; g.OpenForTest(part); break;
            }
            case "farmer":
            {
                var g = Find<FarmerGameController>("FarmerGame"); if (g == null) break;
                yield return null; g.OpenForTest(part); break;
            }
            case "pot":
                if (part == "break") yield return BreakPot();
                if (part == "fx") yield return CelebrationLoop();
                break;
        }
    }

    static T Find<T>(string makeName) where T : MonoBehaviour
    {
        var g = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (g == null) { g = new GameObject(makeName).AddComponent<T>(); }
        if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
        var k = g as KenteWeavingGame; if (k) k.openOnStart = false;
        var f = g as FarmerGameController; if (f) { f.openOnStart = false; f.showTestButton = false; }
        return g;
    }

    // Knocks one bar off the pot every 2.5 s so every crack and break can be watched.
    static IEnumerator BreakPot()
    {
        var lm = LifeManager.Instance; if (lm == null) { Debug.LogWarning("Test: no LifeManager in this scene."); yield break; }
        if (!lm.HasPot) lm.GivePot(5);
        yield return new WaitForSecondsRealtime(1.5f);
        while (lm.HasPot && lm.Lives > 0) { lm.Fail(); yield return new WaitForSecondsRealtime(2.5f); }
        Debug.Log("Test: the pot is empty. Stop Play to finish.");
    }

    // The shared pot celebration on its own, repeating (the same effect every game uses).
    static IEnumerator CelebrationLoop()
    {
        var cv = new GameObject("PotCelebrationTest", typeof(RectTransform)).AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = 900;
        var sc = cv.gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = .5f;
        var dim = new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>(); dim.transform.SetParent(cv.transform, false);
        var dr = dim.rectTransform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero; dim.color = new Color(.1f, .06f, .03f, .55f);
        var fx = PotCelebration.Create((RectTransform)cv.transform, new Color32(150, 205, 110, 255));
        if (fx == null) { Debug.LogWarning("Test: Resources/PotGift is missing, so the 3D pot can't show."); yield break; }
        fx.Rect.sizeDelta = new Vector2(650, 650);
        while (true) { fx.Play(); yield return new WaitForSecondsRealtime(PotCelebration.SettledTime + 1.5f); }
    }
}
