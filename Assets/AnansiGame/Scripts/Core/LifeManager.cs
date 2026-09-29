using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The "brain" of the life system. It saves itself, so every scene picks up the same pot.
//   LifeManager.Instance.GivePot(lives);     -> Nana Nyame hands over a pot with 3-5 segments (from the kente size)
//   LifeManager.Instance.Fail();             -> lose a pot segment
//   LifeManager.Instance.GainLife();         -> get a segment back
//   LifeManager.Instance.AddKnowledge(slot); -> 0 Farmer, 1 Carver, 2 Drummer, 3 Dancer
//   LifeManager.ClearSave();                 -> new journey
public class LifeManager : MonoBehaviour
{
    // One shared pot for the whole game. Found on demand, so scripts that start before it still get the right one.
    static LifeManager instance;
    public static LifeManager Instance { get { if (instance == null) instance = FindAnyObjectByType<LifeManager>(); return instance; } private set { instance = value; } }

    [Header("Rules")]
    [Tooltip("Largest pot Nana Nyame can give (a 5-strip kente). Each pot's own size is set by GivePot(lives).")]
    [Range(3, 10)] public int maxLives = 5;
    [Tooltip("1 = a pot breaks on the first hit. 2 = it cracks first, breaks on the second hit.")]
    [Range(1, 2)] public int hitsPerPot = 1;
    [Tooltip("After a hit, further hits are ignored for this long (seconds).")]
    public float invulnerabilitySeconds = 0.9f;

    [Header("Testing")]
    [Tooltip("F = fail, G = gain, R = reset, P = give pot, K = add next knowledge, N = clear save. Turn off for release.")]
    public bool debugKeys = true;

    [Header("Hook up your own stuff (optional)")]
    public UnityEvent onDamaged = new UnityEvent();
    [Tooltip("Waits this long after the last segment breaks so the shatter can finish.")]
    public float gameOverDelay = 1.4f;
    [Tooltip("Return offered items to the Bag and go back to the map here.")]
    public UnityEvent onGameOver = new UnityEvent();
    public UnityEvent onPotGiven = new UnityEvent();

    public int Lives { get; private set; }
    public int Capacity { get; private set; } // segments in the current pot (3-5)
    public bool HasPot { get; private set; }
    public bool EverHadPot { get; private set; } // the pot HUD stays on screen (empty) after a break; hidden only on a fresh start
    public int KnowledgeMask { get; private set; }
    public bool IsGameOver => Lives <= 0;
    public bool IsInvulnerable => Time.time < invulnUntil;
    public bool HasKnowledge(int slot) => (KnowledgeMask & (1 << slot)) != 0;

    public event Action<int> PotCracked, PotBroken, PotRestored, PotMended, KnowledgeAdded;
    public event Action FullLifeBonus, LivesReset, PotGiven, PotUpgraded;
    public bool IsFull => HasPot && Lives >= maxLives;

    const string K_HAS = "pot_has", K_LIVES = "pot_lives", K_KNOW = "pot_knowledge", K_CAP = "pot_capacity", K_EVER = "pot_ever";

    bool[] cracked;
    float invulnUntil;

    void Awake()
    {
        if (instance != null && instance != this) { Debug.LogWarning("LifeManager: a second pot was found on '" + name + "' and removed. Keep only one (Tools > Anansi Game > Check Project)."); Destroy(this); return; }
        Instance = this;
        cracked = new bool[maxLives];
        HasPot = PlayerPrefs.GetInt(K_HAS, 0) == 1;
        Capacity = Mathf.Clamp(PlayerPrefs.GetInt(K_CAP, 3), 1, maxLives);
        Lives = HasPot ? Mathf.Clamp(PlayerPrefs.GetInt(K_LIVES, Capacity), 1, Capacity) : Capacity;
        KnowledgeMask = PlayerPrefs.GetInt(K_KNOW, 0);
        EverHadPot = PlayerPrefs.GetInt(K_EVER, HasPot || KnowledgeMask != 0 ? 1 : 0) == 1;
    }

    void OnDestroy() { if (instance == this) instance = null; }
    void OnApplicationPause(bool paused) { if (paused) Save(); }

    void Save()
    {
        PlayerPrefs.SetInt(K_HAS, HasPot ? 1 : 0);
        PlayerPrefs.SetInt(K_CAP, Capacity);
        PlayerPrefs.SetInt(K_LIVES, HasPot && Lives > 0 ? Lives : Capacity);
        PlayerPrefs.SetInt(K_KNOW, KnowledgeMask);
        PlayerPrefs.SetInt(K_EVER, EverHadPot ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void ClearSave()
    {
        PlayerPrefs.DeleteKey(K_HAS); PlayerPrefs.DeleteKey(K_LIVES); PlayerPrefs.DeleteKey(K_KNOW); PlayerPrefs.DeleteKey(K_CAP); PlayerPrefs.DeleteKey(K_EVER);
        PlayerPrefs.Save();
    }

    public void GivePot() => GivePot(3);
    public void GivePot(int lives)
    {
        if (HasPot) return;
        StopAllCoroutines();
        HasPot = true; EverHadPot = true;
        Capacity = Mathf.Clamp(lives, 1, maxLives);
        Lives = Capacity;
        Array.Clear(cracked, 0, cracked.Length);
        invulnUntil = 0f;
        Save();
        PotGiven?.Invoke();
        onPotGiven.Invoke();
    }

    // "Try again" / "Review lesson" after a game over: the same pot comes back whole (no kente needed).
    public void RestorePot()
    {
        if (HasPot) return;
        StopAllCoroutines();
        HasPot = true; EverHadPot = true; Lives = Capacity;
        Array.Clear(cracked, 0, cracked.Length);
        invulnUntil = 0f;
        Save();
        PotGiven?.Invoke(); // HUD plays the pop-in; Nana Nyame's onPotGiven is not fired
    }

    // Another kente for a pot that is still whole: its strips are added as bars (also mending lost ones), never above maxLives.
    public void AddBars(int n)
    {
        if (!HasPot) { GivePot(n); return; }
        Capacity = Mathf.Min(maxLives, Capacity + n);
        Lives = Mathf.Min(Capacity, Lives + n);
        Array.Clear(cracked, 0, cracked.Length);
        Save();
        PotUpgraded?.Invoke();
    }

    public void AddKnowledge(int slot)
    {
        if (slot < 0 || slot > 30 || HasKnowledge(slot)) return;
        KnowledgeMask |= 1 << slot;
        Save();
        KnowledgeAdded?.Invoke(slot);
    }

    public void Fail()
    {
        if (!HasPot || IsGameOver || IsInvulnerable) return;
        int i = Lives - 1;
        invulnUntil = Time.time + invulnerabilitySeconds;

        if (hitsPerPot >= 2 && !cracked[i])
        {
            cracked[i] = true;
            PotCracked?.Invoke(i);
            onDamaged.Invoke();
            return;
        }

        cracked[i] = false;
        Lives--;
        if (Lives == 0) HasPot = false; // pot is gone; the player must offer again. Knowledge stays.
        Save();
        PotBroken?.Invoke(i);
        onDamaged.Invoke();
        if (Lives == 0) StartCoroutine(GameOverAfterDelay());
    }

    public void GainLife()
    {
        if (!HasPot || IsGameOver) return;
        if (Lives < Capacity)
        {
            cracked[Lives] = false;
            Lives++;
            Save();
            PotRestored?.Invoke(Lives - 1);
            return;
        }
        for (int j = Capacity - 1; j >= 0; j--)
        {
            if (cracked[j]) { cracked[j] = false; PotMended?.Invoke(j); return; }
        }
        FullLifeBonus?.Invoke();
    }

    public void ResetLives()
    {
        if (!HasPot) return;
        StopAllCoroutines();
        Lives = Capacity;
        Array.Clear(cracked, 0, cracked.Length);
        invulnUntil = 0f;
        Save();
        LivesReset?.Invoke();
    }

    IEnumerator GameOverAfterDelay()
    {
        yield return new WaitForSecondsRealtime(gameOverDelay);
        onGameOver.Invoke();
    }

    void Update()
    {
        if (!debugKeys) return;
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return;
        bool f = kb.fKey.wasPressedThisFrame, g = kb.gKey.wasPressedThisFrame, r = kb.rKey.wasPressedThisFrame,
             p = kb.pKey.wasPressedThisFrame, k = kb.kKey.wasPressedThisFrame, n = kb.nKey.wasPressedThisFrame;
#else
        bool f = Input.GetKeyDown(KeyCode.F), g = Input.GetKeyDown(KeyCode.G), r = Input.GetKeyDown(KeyCode.R),
             p = Input.GetKeyDown(KeyCode.P), k = Input.GetKeyDown(KeyCode.K), n = Input.GetKeyDown(KeyCode.N);
#endif
        if (f) Fail();
        if (g) GainLife();
        if (r) ResetLives();
        if (p) GivePot();
        if (k) for (int s = 0; s < 4; s++) if (!HasKnowledge(s)) { AddKnowledge(s); break; }
        if (n) { ClearSave(); Debug.Log("PotLives: save cleared — reload the scene."); }
    }
}
