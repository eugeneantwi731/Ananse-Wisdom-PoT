using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Put on the Nana Nyame pin, next to JourneyPin (Pin = NanaNyame).
// Drop a kente on him: he talks, then a 3D pot appears above him with "+N" (N = strips, 1-5).
// Tap the pot to collect it: it flies to the pot bar. A whole pot gets the strips added as bars (max 5).
public class NanaNyameOffering : MonoBehaviour
{
    [Tooltip("Leave empty to use Resources/Characters/nana_nyame and nana_nyame_pot.")]
    public Sprite nanaPortrait;
    public Sprite nanaPotArt;
    const string N = "Nana Nyame", A = "Kwaku Ananse";
    const string K_AMOUNT = "PotGift_Amount", K_ADD = "PotGift_Add", K_FIRST = "PotGift_First";

    [TextArea] public string[] firstVisit = {
        "Kwaku Ananse. You climbed all the way up to my sky with a gift?",
        "Kente, woven by your own hands. You have shown me gratitude, and gratitude is rare.",
        "Take this pot. It is no ordinary pot. Keep it whole." };
    [TextArea] public string[] secondVisit = {
        "Ananse again. Your first pot broke, didn't it?",
        "Still, another fine cloth. Here, a new pot. Be more careful this time." };
    [TextArea] public string[] repeatVisit = {
        "Back so soon, little spider? Very well. A cloth for a pot.",
        "Your weaving grows better each time. Take your pot.",
        "The sky has many pots, Ananse. Try not to break this one." };
    [TextArea] public string[] strongerPot = {
        "Another kente? Your gratitude has no end, Ananse.",
        "Let me make your pot stronger." };
    [TextArea] public string fullPotLine = "Your pot is as strong as a pot can be, Ananse. Keep your kente for another day.";
    [TextArea] public string firstPotThought = "Imagine what I could keep in a pot like this... all the knowledge in the village.";
    [TextArea] public string collectThought = "Nana Nyame is holding out a pot for me! I should tap him to take it.";
    [TextArea] public string waitingThought = "Nana Nyame is still holding my pot. I should tap him and take it first.";
    [TextArea] public string alreadyHasPotLine = "Your pot is still whole, Ananse.";
    [TextArea] public string noKenteLine = "Ah, Ananse. It is good to see you.";
    [TextArea] public string dragHintThought = "I should give Nana Nyame my kente. I'll drag it from my Gifts onto him.";
    [TextArea] public string strongerHintThought = "Another kente could make my pot stronger. I'll drag it from my Gifts onto Nana Nyame.";

    public UnityEvent<int> onPotGranted = new UnityEvent<int>();

    void Awake()
    {
        if (dragHintThought != null) dragHintThought = dragHintThought.Replace("from my Bag", "from my Gifts"); // old saved text
        if (!nanaPortrait || nanaPortrait.rect.height < 600) nanaPortrait = JourneyDialogue.LoadArt("Characters/nana_nyame") ?? nanaPortrait; // replaces the old small round portrait
        if (!nanaPotArt) nanaPotArt = JourneyDialogue.LoadArt("Characters/nana_nyame_pot");
        if (!nanaPortrait) Debug.LogWarning("Nana Nyame: Resources/Characters/nana_nyame.png is missing.");
    }
    JourneyDialogue.Line Nana(string text, bool holdingPot = false) => new JourneyDialogue.Line(N, text, holdingPot && nanaPotArt ? nanaPotArt : nanaPortrait) { fullArt = true, artOnRight = true, artCrop = .58f };

    IEnumerator Start()
    {
        yield return null; yield return null; // let the pin place itself first
        if (PlayerPrefs.GetInt(K_AMOUNT, 0) > 0) SpawnGift(); // a pot was left uncollected last time
    }

    List<JourneyDialogue.Line> Lines(string[] src, string speaker)
    {
        // Nana holds out the pot on the last line
        var l = new List<JourneyDialogue.Line>(); for (int i = 0; i < src.Length; i++) l.Add(Nana(src[i], i == src.Length - 1)); return l;
    }
    void Say(string line) => JourneyDialogue.Play(new[] { Nana(line) });
    static bool GiftWaiting => PotGiftCollectible.Current != null || PlayerPrefs.GetInt(K_AMOUNT, 0) > 0;

    public void Tapped()
    {
        var lm = LifeManager.Instance;
        if (GiftWaiting) { CollectGift(); return; } // his pin shows the pot: tapping takes it
        if (lm != null && lm.IsFull) { Say(fullPotLine); return; }
        if (KenteBag.Count > 0) { JourneyDialogue.Think(lm != null && lm.HasPot ? strongerHintThought : dragHintThought); return; }
        Say(lm != null && lm.HasPot ? alreadyHasPotLine : noKenteLine);
    }

    public bool Offer()
    {
        var lm = LifeManager.Instance;
        if (lm == null) { Debug.LogWarning("NanaNyameOffering: no LifeManager in the scene."); return false; }
        if (GiftWaiting) { JourneyDialogue.Think(waitingThought); return false; }
        if (lm.IsFull) { Say(fullPotLine); return false; }          // 5 full bars: the kente stays in Gifts
        if (!KenteBag.TakeBest(out var cloth)) return false;       // the kente with the most strips
        int bars = KenteBag.Bars(cloth);
        bool add = lm.HasPot;
        int shown = add ? Mathf.Min(lm.maxLives, lm.Lives + bars) - lm.Lives : Mathf.Min(bars, lm.maxLives);

        List<JourneyDialogue.Line> lines;
        bool first = !JourneyState.EverHadPot;
        if (add) lines = Lines(strongerPot, N);
        else
        {
            int visit = JourneyState.NanaVisits;
            if (visit == 0) lines = Lines(firstVisit, N);
            else if (visit == 1) lines = Lines(secondVisit, N);
            else lines = new List<JourneyDialogue.Line> { Nana(repeatVisit[(visit - 2) % repeatVisit.Length], true) };
        }
        JourneyState.NanaVisits = JourneyState.NanaVisits + 1;

        // remember the gift until it is collected (survives closing the app)
        PlayerPrefs.SetInt(K_AMOUNT, bars); PlayerPrefs.SetInt(K_ADD, add ? 1 : 0); PlayerPrefs.SetInt(K_FIRST, first ? 1 : 0);
        PlayerPrefs.SetInt(K_AMOUNT + "_Shown", Mathf.Max(1, shown)); PlayerPrefs.Save();

        JourneyDialogue.Play(lines, () =>
        {
            SpawnGift();
            if (first) JourneyDialogue.Think(collectThought);
        });
        return true;
    }

    void SpawnGift()
    {
        int bars = PlayerPrefs.GetInt(K_AMOUNT, 0); if (bars <= 0) return;
        int shown = PlayerPrefs.GetInt(K_AMOUNT + "_Shown", bars);
        PotGiftCollectible.Show(this, shown); // his pin turns into a pot pin with "+N"
    }

    public void CollectGift()
    {
        var g = PotGiftCollectible.Current;
        if (g != null) g.Collect(Collected);
        else if (PlayerPrefs.GetInt(K_AMOUNT, 0) > 0) Collected();
    }

    void Collected()
    {
        var lm = LifeManager.Instance;
        int bars = PlayerPrefs.GetInt(K_AMOUNT, 0);
        bool add = PlayerPrefs.GetInt(K_ADD, 0) == 1, first = PlayerPrefs.GetInt(K_FIRST, 0) == 1;
        PlayerPrefs.DeleteKey(K_AMOUNT); PlayerPrefs.DeleteKey(K_ADD); PlayerPrefs.DeleteKey(K_FIRST); PlayerPrefs.DeleteKey(K_AMOUNT + "_Shown"); PlayerPrefs.Save();
        if (lm == null || bars <= 0) return;
        if (add && lm.HasPot) lm.AddBars(bars); else lm.GivePot(bars); // HUD: pot pops in / bar grows
        JourneyState.EverHadPot = true;
        onPotGranted.Invoke(bars);
        if (first) JourneyDialogue.Think(firstPotThought, JourneyPin.RefreshAll); // then the Farmer pin appears
        else JourneyPin.RefreshAll();
    }

    public static void ClearPending() { PlayerPrefs.DeleteKey(K_AMOUNT); PlayerPrefs.DeleteKey(K_ADD); PlayerPrefs.DeleteKey(K_FIRST); PlayerPrefs.DeleteKey(K_AMOUNT + "_Shown"); }
}
