using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class KenteColor
{
    public string id, name; public Color color; [TextArea] public string meaning;
    public KenteColor(string i, string n, string hex, string m) { id = i; name = n; ColorUtility.TryParseHtmlString(hex, out color); meaning = m; }
}

public static class KenteData
{
    public static List<KenteColor> Colors() => new List<KenteColor>
    {
        new KenteColor("gold", "Gold", "#E8A33D", "Gold is for royalty and wealth. Fit for the Sky God."),
        new KenteColor("green", "Green", "#4F7C3A", "Green is growth, and a good harvest."),
        new KenteColor("red", "Red", "#B03A2E", "Red is strength, and the sacrifices of our people."),
        new KenteColor("black", "Black", "#2b1d14", "Black is maturity, and the ancestors watching over us."),
        new KenteColor("blue", "Blue", "#2f5d8a", "Blue is peace and harmony."),
        new KenteColor("white", "White", "#F6EBD9", "White is purity, and joy after a victory."),
    };

    // Tutorial patterns: 3 strips x 6 rows each. Written TOP row first (as they look), stored bottom row first (the order you weave).
    public struct KentePattern { public string name, tag; public string[][] strips; }
    static KentePattern P(string name, string tag, string a, string b, string c)
    {
        string[] S(string s) { var r = s.Split(' '); System.Array.Reverse(r); return r; }
        return new KentePattern { name = name, tag = tag, strips = new[] { S(a), S(b), S(c) } };
    }
    public static readonly KentePattern[] Patterns =
    {
        P("Sika Checker", "Royal",    "gold black red green black gold",  "black gold green red gold black", "gold black red green black gold"),
        P("Ananse Steps", "Diagonal", "gold red black gold red black",    "red black gold red black gold",   "black gold red black gold red"),
        P("Nsu River", "Cool",        "blue white gold gold white blue",  "gold black red red black gold",   "blue white gold gold white blue"),
        P("Harvest", "Warm",          "white red gold gold red white",    "red gold black black gold red",   "white red gold gold red white"),
        P("Emerald Crown", "Green",   "green gold black black gold green","gold red gold gold red gold",     "green gold black black gold green"),
    };
    const string K_PATTERN = "Kente_TutorialPattern";
    public static int PatternIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(K_PATTERN, 0), 0, Patterns.Length - 1);
        set { PlayerPrefs.SetInt(K_PATTERN, Mathf.Clamp(value, 0, Patterns.Length - 1)); PlayerPrefs.Save(); }
    }
    public static string[][] TutorialStrips() => Patterns[PatternIndex].strips;

    public static string[] Intro() => new[]
    {
        "Nana Nyame, the Sky God, has been good to me. I must thank him with a gift.",
        "What does a king deserve? Kente. And I will weave it with my own hands.",
        "Every colour in kente speaks. I choose the thread the pattern asks for, then pull the shuttle across.",
    };
    public static string[] Outro() => new[] { "Three strips, sewn edge to edge. Kente, woven by my own hands.", "Now, to Nana Nyame. I hope it pleases him." };
    public static string[] FreeIntro() => new[] { "Back at my loom. The threads are waiting.", "An old pattern, my own colours, or the fine thin threads of the masters? Let me choose." };
    public static string[] FreeOutro() => new[] { "Every colour chosen by me. Another kente for my hut." };

    public static string[] LoadingHints() => new[]
    {
        "Tap a thread to load the shuttle, then drag the shuttle all the way across the loom.",
        "Every kente colour speaks. Gold is royalty, green is growth, blue is peace.",
        "Made a mistake? Undo takes back the last row, even into the strip before.",
        "Kente is woven in narrow strips, then sewn edge to edge into one cloth.",
        "Finished cloths wait in your Gifts. You can keep three kente at a time.",
        "Save your finished cloth as a picture from the last screen.",
        "Old weavers say the Sky God is kind to a generous cloth…",
        "In Advanced, one swipe lays one thin thread. Hold still at the end of a swipe and the shuttle keeps weaving.",
        "Every finished cloth waits in My Kente. Open one to keep weaving it.",
        "In the tutorial the pattern card shows each colour. After that, the design is yours.",
    };

    // Pot bars Nana Nyame grants for a kente (secret): one bar per strip, 1 to 5 (6+ strips still 5).
    public static int LifeReward(int strips, int rows) => Mathf.Clamp(strips, 1, 5);
}
