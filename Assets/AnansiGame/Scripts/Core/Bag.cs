using UnityEngine;

// The four Bag slots: kente, food, drum, rhythm. Each holds up to 3.
// Kente keeps its own list (size + lives per cloth) in KenteBag; the others are simple counts.
public static class Bag
{
    public const string Kente = "kente", Food = "food", Drum = "drum", Rhythm = "rhythm";
    public const int Max = 3;
    static string Key(string id) => "Bag_" + id;

    public static int Count(string id) => id == Kente ? KenteBag.Count : PlayerPrefs.GetInt(Key(id), 0);
    public static bool IsFull(string id) => Count(id) >= Max;

    // Returns false when that slot is already full. (Kente: use KenteBag.Add so its size is stored.)
    public static bool Add(string id)
    {
        if (id == Kente) { Debug.LogWarning("Bag: use KenteBag.Add for kente."); return false; }
        int n = Count(id); if (n >= Max) return false;
        PlayerPrefs.SetInt(Key(id), n + 1); PlayerPrefs.Save(); return true;
    }

    // Use one item (e.g. food given to the Carver). Returns false if there is none.
    public static bool Take(string id)
    {
        if (id == Kente) return KenteBag.TakeBest(out _);
        int n = Count(id); if (n <= 0) return false;
        PlayerPrefs.SetInt(Key(id), n - 1); PlayerPrefs.Save(); return true;
    }

    // Offered item comes back when the player fails the game it was used for.
    public static void Return(string id) { if (id != Kente) { int n = Count(id); PlayerPrefs.SetInt(Key(id), Mathf.Min(Max, n + 1)); PlayerPrefs.Save(); } }

    public static void ClearAll()
    {
        foreach (var id in new[] { Food, Drum, Rhythm }) PlayerPrefs.DeleteKey(Key(id));
        KenteBag.Clear();
    }
}
