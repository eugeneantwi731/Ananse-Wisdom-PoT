using System.Collections.Generic;
using UnityEngine;

// The kente slot of the Bag. Each cloth keeps its own size and the pot lives it is worth.
// Saved in PlayerPrefs "KenteBar" as "strips x rows : lives;..." (same format the weaving game already used).
public static class KenteBag
{
    public const string Key = "KenteBar";
    public const int Max = 3;

    public struct Cloth { public int strips, rows, lives; }

    public static List<Cloth> All()
    {
        var list = new List<Cloth>();
        foreach (var e in PlayerPrefs.GetString(Key, "").Split(';'))
        {
            var p = e.Split('x', ':');
            if (p.Length == 3 && int.TryParse(p[0], out var s) && int.TryParse(p[1], out var r) && int.TryParse(p[2], out var l))
                list.Add(new Cloth { strips = s, rows = r, lives = l });
        }
        return list;
    }
    static void Write(List<Cloth> list)
    {
        var parts = new List<string>();
        foreach (var c in list) parts.Add(c.strips + "x" + c.rows + ":" + c.lives);
        PlayerPrefs.SetString(Key, string.Join(";", parts)); PlayerPrefs.Save();
    }

    public static int Count => All().Count;
    public static bool IsFull => Count >= Max;

    // Returns false if the Bag already holds 3 (the cloth is not stored).
    public static bool Add(int strips, int rows, int lives)
    {
        var list = All(); if (list.Count >= Max) return false;
        list.Add(new Cloth { strips = strips, rows = rows, lives = lives }); Write(list); return true;
    }

    // Removes the cloth with the most strips (the most pot bars) and returns it. False if the Bag has no kente.
    public static bool TakeBest(out Cloth best)
    {
        var list = All(); best = default;
        if (list.Count == 0) return false;
        int bi = 0; for (int i = 1; i < list.Count; i++) if (list[i].strips > list[bi].strips || (list[i].strips == list[bi].strips && list[i].rows > list[bi].rows)) bi = i;
        best = list[bi]; list.RemoveAt(bi); Write(list); return true;
    }

    public static int Bars(Cloth c) => Mathf.Clamp(c.strips, 1, 5);

    public static void Clear() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
}
