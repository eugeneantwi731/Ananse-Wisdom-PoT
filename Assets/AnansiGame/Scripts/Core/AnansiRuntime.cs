using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Shared helpers used by every Anansi script:
//  - one EventSystem that works with the old AND the new Input System (so buttons never go dead)
//  - taps for pins, for both Input Systems
//  - Test Mode (Tools > Anansi Game > Test Mode): every pin shown, games open without a pot
//  - opening a game from a pin with no wiring
public static class AnansiRuntime
{
    public const string NoPotThought = "My pot is broken. I need a new one from Nana Nyame before I visit anyone.";
    const string TestKey = "Anansi_TestMode";

    public static bool TestMode
    {
        get => PlayerPrefs.GetInt(TestKey, 0) == 1;
        set { PlayerPrefs.SetInt(TestKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Prepare();
    }
    static void OnSceneLoaded(Scene s, LoadSceneMode m) => Prepare();
    static void Prepare()
    {
        foreach (var es in UnityEngine.Object.FindObjectsByType<EventSystem>()) FixModule(es);
        // village scene: scan guide + village reveal + birds, added by itself
        if (UnityEngine.Object.FindAnyObjectByType<JourneyPin>() != null && UnityEngine.Object.FindAnyObjectByType<ScanFlow>() == null)
            new GameObject("ScanFlow").AddComponent<ScanFlow>();
        if (UnityEngine.Object.FindAnyObjectByType<JourneyPin>() != null && UnityEngine.Object.FindAnyObjectByType<OptionsMenu>() == null)
            new GameObject("OptionsMenu").AddComponent<OptionsMenu>();
        if (!TestMode) return;
        Debug.Log("Anansi Game: TEST MODE is on (Tools > Anansi Game > Test Mode). All pins are shown and games open without a pot.");
        var lm = LifeManager.Instance;
        if (lm != null && !lm.HasPot) lm.GivePot(3);
    }

    // ---------- UI input ----------
    public static void EnsureEventSystem()
    {
        var es = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        FixModule(es);
    }
    static void FixModule(EventSystem es)
    {
        if (es == null) return;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        // New Input System only: the old StandaloneInputModule throws errors every frame and kills every button.
        var old = es.GetComponent<StandaloneInputModule>();
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null) es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#elif ENABLE_INPUT_SYSTEM
        if (es.GetComponent<BaseInputModule>() == null) es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        if (es.GetComponent<BaseInputModule>() == null) es.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    // True on the frame the screen is tapped (or the mouse clicked in the Editor).
    public static bool TryGetTap(out Vector2 pos)
    {
#if ENABLE_INPUT_SYSTEM
        var ts = UnityEngine.InputSystem.Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) { pos = ts.primaryTouch.position.ReadValue(); return true; }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) { pos = mouse.position.ReadValue(); return true; }
#else
        if (Input.touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Began) { pos = Input.GetTouch(0).position; return true; }
        if (Input.GetMouseButtonDown(0)) { pos = Input.mousePosition; return true; }
#endif
        pos = default;
        return false;
    }

    // ---------- pot ----------
    // Mini-games need a pot. When a game is tested on its own (no village pins in the scene) or Test Mode is on,
    // give a 3-segment test pot instead of closing the game.
    public static bool GiveTestPotIfAllowed(LifeManager lm)
    {
        if (lm == null) return false;
        if (lm.HasPot) return true;
        bool village = UnityEngine.Object.FindAnyObjectByType<JourneyPin>() != null;
        if (village && !TestMode) return false;
        Debug.Log("Anansi Game: no pot yet, so a 3-segment test pot was given (" + (TestMode ? "Test Mode" : "no village pins in this scene") + ").");
        lm.GivePot(3);
        return lm.HasPot;
    }

    // ---------- opening games ----------
    public static bool AnyGameOpen
    {
        get
        {
            foreach (var f in UnityEngine.Object.FindObjectsByType<FarmerGameController>()) if (f.IsOpen) return true;
            foreach (var k in UnityEngine.Object.FindObjectsByType<KenteWeavingGame>()) if (k.IsOpen) return true;
            return false;
        }
    }

    public static void OpenGameFor(JourneyPin.PinId pin)
    {
        switch (pin)
        {
            case JourneyPin.PinId.Home: OpenKente(); break;
            case JourneyPin.PinId.Farmer: OpenFarmer(); break;
            case JourneyPin.PinId.NanaNyame: break;
            default: JourneyDialogue.Think("This lesson is still being prepared. I will come back soon."); break;
        }
    }

    // For a Knowledge Pin without a Journey Pin: guesses the game from the pin's id.
    public static void OpenGameById(string id)
    {
        id = (id ?? "").ToLower();
        if (id.Contains("farm")) OpenFarmer();
        else if (id.Contains("weav") || id.Contains("home") || id.Contains("kente") || id.Contains("anan")) OpenKente();
        else JourneyDialogue.Think("This lesson is still being prepared. I will come back soon.");
    }

    public static void OpenKente()
    {
        var g = UnityEngine.Object.FindAnyObjectByType<KenteWeavingGame>(FindObjectsInactive.Include);
        bool fresh = g == null;
        if (fresh) { g = new GameObject("KenteWeavingGame").AddComponent<KenteWeavingGame>(); g.openOnStart = false; }
        if (!g.gameObject.activeSelf) { g.gameObject.SetActive(true); fresh = true; }
        if (fresh) Later(g.OpenGame); else g.OpenGame();
    }

    public static void OpenFarmer()
    {
        var g = UnityEngine.Object.FindAnyObjectByType<FarmerGameController>(FindObjectsInactive.Include);
        bool fresh = g == null;
        if (fresh) { g = new GameObject("FarmerGame").AddComponent<FarmerGameController>(); g.openOnStart = false; g.showTestButton = false; }
        if (!g.gameObject.activeSelf) { g.gameObject.SetActive(true); fresh = true; }
        if (fresh) Later(g.OpenGame); else g.OpenGame();
    }

    static AnansiRuntimeRunner runner;
    static void Later(Action a)
    {
        if (runner == null)
        {
            var go = new GameObject("AnansiRuntime") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<AnansiRuntimeRunner>();
        }
        runner.StartCoroutine(NextFrame(a));
    }
    static IEnumerator NextFrame(Action a) { yield return null; a(); } // lets a new game finish Awake/Start first
}

public class AnansiRuntimeRunner : MonoBehaviour { }
