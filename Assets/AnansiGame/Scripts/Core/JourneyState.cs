using UnityEngine;

// Everything the village map needs to know about progress. All saved in PlayerPrefs, so the journey continues after the app closes.
public static class JourneyState
{
    public const string Home = "home", Nana = "nana", Farmer = "farmer", Carver = "carver", Drummer = "drummer", Dancer = "dancer";
    public static readonly string[] Chain = { Farmer, Carver, Drummer, Dancer };

    const string K_EVERPOT = "Journey_EverPot", K_NANA = "Journey_NanaVisits", K_DONE = "Journey_Done_", K_GIVEN = "Journey_Given_";

    public static bool TutorialWoven => PlayerPrefs.GetInt(KenteWeavingGame.TutorialKey, 0) == 1;
    public static bool EverHadPot { get => PlayerPrefs.GetInt(K_EVERPOT, 0) == 1; set { PlayerPrefs.SetInt(K_EVERPOT, value ? 1 : 0); PlayerPrefs.Save(); } }
    public static int NanaVisits { get => PlayerPrefs.GetInt(K_NANA, 0); set { PlayerPrefs.SetInt(K_NANA, value); PlayerPrefs.Save(); } }
    public static bool HasPot => LifeManager.Instance != null && LifeManager.Instance.HasPot;

    public static bool Done(string id) => id == Farmer ? PlayerPrefs.GetInt("FarmerComplete", 0) == 1 : PlayerPrefs.GetInt(K_DONE + id, 0) == 1;
    public static void MarkDone(string id) { PlayerPrefs.SetInt(id == Farmer ? "FarmerComplete" : K_DONE + id, 1); SetGiven(id, false); PlayerPrefs.Save(); }
    public static bool JourneyComplete => Done(Dancer);

    // Item the character needs before they will teach (drag it from the Bag onto their pin).
    public static string NeededItem(string id) => AnansiRuntime.TestMode ? null : id == Carver ? Bag.Food : id == Drummer ? Bag.Drum : id == Dancer ? Bag.Rhythm : null;
    public static bool Given(string id) => PlayerPrefs.GetInt(K_GIVEN + id, 0) == 1;
    public static void SetGiven(string id, bool v) { PlayerPrefs.SetInt(K_GIVEN + id, v ? 1 : 0); PlayerPrefs.Save(); }
    public static bool NeedsPot(string id) => System.Array.IndexOf(Chain, id) >= 0;

    // Locked pins are shown grey with a padlock (JourneyPin.showLockedPins). Turn that off to hide them instead.
    public static bool IsVisible(string id) => true;

    // The next teacher to visit (gets a soft pulse on the map). Null when there is nothing to do right now.
    public static string NextPin
    {
        get
        {
            if (!HasPot) return null;
            foreach (var id in Chain) if (IsUnlocked(id) && !Done(id)) return id;
            return null;
        }
    }

    // Anansi's thought when he taps a locked pin.
    public static string LockedThought(string id)
    {
        switch (id)
        {
            case Nana: return "Nana Nyame is waiting for a gift. I should weave something beautiful first.";
            case Farmer: return "The Farmer will not share his secrets yet. I need a pot from Nana Nyame first.";
            case Carver: return "The Carver will not teach me yet. I should learn from the Farmer first.";
            case Drummer: return "The Drummer will not teach me yet. I should learn from the Carver first.";
            case Dancer: return "The Dancer will not teach me yet. I should learn from the Drummer first.";
        }
        return "I cannot go there yet.";
    }

    // Is the pin open to visit? (Locked pins need the step before them.)
    public static bool IsUnlocked(string id)
    {
        if (AnansiRuntime.TestMode) return true;
        switch (id)
        {
            case Home: return true;
            case Nana: return TutorialWoven;
            case Farmer: return EverHadPot;
            case Carver: return Done(Farmer);
            case Drummer: return Done(Carver);
            case Dancer: return Done(Drummer);
        }
        return false;
    }

    // For the menu: show "Continue your journey" when this is true.
    public static bool HasSave => TutorialWoven;

    // "New game" on the menu. Clears the pot, knowledge, Bag and every step.
    public static void NewGame()
    {
        foreach (var k in new[] { K_EVERPOT, K_NANA, "FarmerComplete", "Farmer_FailedBefore", KenteWeavingGame.TutorialKey }) PlayerPrefs.DeleteKey(k);
        NanaNyameOffering.ClearPending();
        foreach (var id in new[] { Farmer, Carver, Drummer, Dancer }) { PlayerPrefs.DeleteKey(K_DONE + id); PlayerPrefs.DeleteKey(K_GIVEN + id); }
        LifeManager.ClearSave(); Bag.ClearAll(); PlayerPrefs.Save();
    }
}
