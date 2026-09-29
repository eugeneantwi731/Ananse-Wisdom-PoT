using System;
using System.Collections.Generic;
using UnityEngine;

public enum Speaker { Farmer, Ananse }

// Which Ananse picture to show for a line. WithPot is the default.
public enum AnansePose { WithPot, NoPot }

[Serializable]
public class CropInfo
{
    [Tooltip("Must match the image name: crop_<id>.png")] public string id;
    public string displayName;
    public CropInfo() { }
    public CropInfo(string i, string n) { id = i; displayName = n; }
}

[Serializable]
public class FoodLevel
{
    [Tooltip("Must match the image name: food_<id>.png")] public string foodId;
    public string displayName;
    [Tooltip("Crop ids needed for this food (one slot each).")] public string[] crops;
    [TextArea(2, 4)] public string lessonLine;
    [TextArea(2, 4)] public string fact;
    public FoodLevel() { }
    public FoodLevel(string id, string name, string[] c, string line, string f) { foodId = id; displayName = name; crops = c; lessonLine = line; fact = f; }
}

[Serializable]
public class DialogueLine
{
    public Speaker speaker;
    [TextArea(2, 4)] public string text;
    [Tooltip("Only used when Ananse speaks.")] public AnansePose anansePose = AnansePose.WithPot;
    public DialogueLine() { }
    public DialogueLine(Speaker s, string t, AnansePose p = AnansePose.WithPot) { speaker = s; text = t; anansePose = p; }
}

public static class FarmerGameData
{
    const Speaker F = Speaker.Farmer, A = Speaker.Ananse;

    public static List<CropInfo> DefaultCrops() => new List<CropInfo>
    {
        new CropInfo("yam", "Yam"), new CropInfo("cassava", "Cassava"), new CropInfo("plantain", "Plantain"), new CropInfo("maize", "Maize"),
        new CropInfo("beans", "Beans"), new CropInfo("rice", "Rice"), new CropInfo("cocoa", "Cocoa"), new CropInfo("palmoil", "Palm Oil"),
    };

    public static List<FoodLevel> DefaultLevels() => new List<FoodLevel>
    {
        new FoodLevel("kelewele", "Kelewele", new[] { "plantain" },
            "Kelewele. Ripe plantain, spiced with ginger and pepper, then fried.",
            "Ripe plantain is cut, spiced with ginger and pepper, then fried. A favorite evening street snack."),
        new FoodLevel("chocolate", "Chocolate", new[] { "cocoa" },
            "Chocolate. It all starts with cocoa, and Ghana grows some of the best cocoa in the world.",
            "Ghana is one of the world's largest cocoa producers. The beans are fermented and sun-dried before they become chocolate."),
        new FoodLevel("waakye", "Waakye", new[] { "rice", "beans" },
            "Waakye. Rice and beans, cooked together in one pot.",
            "Rice and beans are cooked together. The red-brown color comes from dried sorghum leaves."),
        new FoodLevel("fufu", "Fufu", new[] { "cassava", "plantain" },
            "Fufu. We boil cassava and plantain, then pound them together.",
            "Cassava and plantain are boiled, then pounded in a mortar and eaten with soup."),
        new FoodLevel("banku", "Banku", new[] { "maize", "cassava" },
            "Banku. Corn dough and cassava dough, stirred over the fire.",
            "Fermented corn dough and cassava dough are stirred over the fire, often served with pepper and grilled tilapia."),
        new FoodLevel("ampesi", "Ampesi", new[] { "yam", "plantain" },
            "Yam and plantain ampesi. Boiled, and eaten with kontomire stew.",
            "Yam and plantain are boiled and eaten with kontomire (cocoyam leaf) stew."),
        new FoodLevel("gobe", "Gobe", new[] { "beans", "palmoil", "plantain", "cassava" },
            "Gobe. Beans in palm oil, with gari from cassava and fried plantain.",
            "Beans are cooked in palm oil and served with gari (roasted grated cassava) and fried plantain."),
    };

    public static List<DialogueLine> Intro() => new List<DialogueLine>
    {
        new DialogueLine(F, "Ah, Kwaku Ananse! You have walked a long way. Welcome to my farm."),
        new DialogueLine(F, "Everything we eat in Ghana begins in the soil. Yam, cassava, plantain, maize, beans, rice, cocoa, palm nut."),
        new DialogueLine(A, "The farmer's knowledge... it will look good in my pot."),
        new DialogueLine(F, "Sit with me. I will show you what each crop becomes."),
    };

    public static List<DialogueLine> PreGame() => new List<DialogueLine>
    {
        new DialogueLine(F, "Now show me what you learned. Put the right crops under each food."),
        new DialogueLine(A, "Careful. Every mistake cracks my pot. If it breaks, I must beg Nana Nyame for another."),
    };

    public static List<DialogueLine> Win() => new List<DialogueLine>
    {
        new DialogueLine(F, "You know my farm now, Ananse. Go well."),
        new DialogueLine(A, "The pot is full, and this time it will not break. Now, the carver."),
    };

    public static List<string> Wrong() => new List<string>
    {
        "Hmm... that is not right. Think, Ananse.",
        "No, no! My pot is cracking...",
        "Careful... what did the old man say?",
    };
}
