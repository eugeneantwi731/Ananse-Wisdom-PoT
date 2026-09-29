using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Anansi Game > Test a Section…  One click sets up the save for that moment, opens the Village scene and presses Play.
public class AnansiSectionTester : EditorWindow
{
    [MenuItem("Tools/Anansi Game/Test a Section…", false, 10)]
    static void Open() { var w = GetWindow<AnansiSectionTester>("Test a Section"); w.minSize = new Vector2(330, 420); }

    struct Sec { public string group, label, id; public Action prep; public Sec(string g, string l, string i, Action p) { group = g; label = l; id = i; prep = p; } }

    static readonly Sec[] Sections =
    {
        new Sec("Village map", "Fresh start (new journey)", "map", Fresh),
        new Sec("Village map", "Tutorial kente in Gifts (drag it to Nana Nyame)", "map", KenteWoven),
        new Sec("Village map", "Pot waiting on Nana Nyame's pin (tap to collect)", "map", PotWaiting),
        new Sec("Village map", "Pot collected (Farmer unlocked)", "map", () => HasPot(3)),
        new Sec("Village map", "Farmer completed (Carver unlocked)", "map", FarmerDone),

        new Sec("Kente weaving", "Tutorial: from Anansi's dialogue", "kente:dialogue", Fresh),
        new Sec("Kente weaving", "Tutorial: straight to weaving", "kente:weave", Fresh),
        new Sec("Kente weaving", "Tutorial: last row of the last strip", "kente:last", Fresh),
        new Sec("Kente weaving", "Tutorial: sewing + finish screen", "kente:sew", Fresh),
        new Sec("Kente weaving", "Free weaving: size picker", "kente:free", () => HasPot(3)),
        new Sec("Kente weaving", "Free weaving: straight to weaving", "kente:free_weave", () => HasPot(3)),

        new Sec("Farmer", "Lesson from the start", "farmer:lesson", () => HasPot(3)),
        new Sec("Farmer", "Gameplay, level 1 (skip lessons)", "farmer:game", () => HasPot(3)),
        new Sec("Farmer", "Last level", "farmer:last", () => HasPot(3)),
        new Sec("Farmer", "Win screen (pot celebration)", "farmer:win", () => HasPot(3)),
        new Sec("Farmer", "Game over (pot broken)", "farmer:gameover", () => HasPot(3)),
        new Sec("Farmer", "Coming back after a broken pot", "farmer:return", () => HasPot(3)),

        new Sec("Pot", "Break a 5-bar pot, one bar at a time", "pot:break", () => HasPot(5)),
        new Sec("Pot", "Pot celebration effect on its own (loops)", "pot:fx", () => HasPot(3)),
    };

    Vector2 scroll;
    void OnGUI()
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("Click a section: your save is set to that moment, the Village scene opens and Play starts. Your real progress is replaced, so use Journey > New Game afterwards if needed.", MessageType.Info);
        bool skip = EditorPrefs.GetBool("Anansi_TestSkipScan", true);
        bool nskip = EditorGUILayout.ToggleLeft("Skip the map scan (village shows straight away)", skip);
        if (nskip != skip) EditorPrefs.SetBool("Anansi_TestSkipScan", nskip);
        if (EditorApplication.isPlaying) EditorGUILayout.HelpBox("Stop Play first.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            string last = null;
            foreach (var s in Sections)
            {
                if (s.group != last) { EditorGUILayout.Space(8); EditorGUILayout.LabelField(s.group, EditorStyles.boldLabel); last = s.group; }
                if (GUILayout.Button(s.label, GUILayout.Height(26))) Launch(s);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    static void Launch(Sec s)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!OpenVillage()) return;
        s.prep();
        PlayerPrefs.SetString(AnansiTestJump.Key, s.id);
        PlayerPrefs.SetInt(AnansiTestJump.SkipScanKey, EditorPrefs.GetBool("Anansi_TestSkipScan", true) ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log("Anansi Game: testing \"" + s.group + " > " + s.label + "\".");
        EditorApplication.isPlaying = true;
    }

    // The village scene is the one with the pins. Uses the open scene if it has them, otherwise opens "Village".
    static bool OpenVillage()
    {
        if (UnityEngine.Object.FindAnyObjectByType<JourneyPin>(FindObjectsInactive.Include) != null) return true;
        var guid = AssetDatabase.FindAssets("t:Scene Village").FirstOrDefault(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == "Village")
                   ?? AssetDatabase.FindAssets("t:Scene Village").FirstOrDefault();
        if (guid == null) { EditorUtility.DisplayDialog("Test a Section", "Couldn't find the Village scene. Open it yourself, then click the section again.", "OK"); return false; }
        EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(guid));
        return true;
    }

    // ---------- save states ----------
    static void Fresh() { JourneyState.NewGame(); }
    static void KenteWoven()
    {
        Fresh();
        PlayerPrefs.SetInt(KenteWeavingGame.TutorialKey, 1);
        KenteBag.Add(3, 6, 3);
    }
    static void PotWaiting()
    {
        Fresh();
        PlayerPrefs.SetInt(KenteWeavingGame.TutorialKey, 1);
        JourneyState.NanaVisits = 1;
        PlayerPrefs.SetInt("PotGift_Amount", 3); PlayerPrefs.SetInt("PotGift_Amount_Shown", 3);
        PlayerPrefs.SetInt("PotGift_Add", 0); PlayerPrefs.SetInt("PotGift_First", 1);
    }
    static void HasPot(int bars)
    {
        Fresh();
        PlayerPrefs.SetInt(KenteWeavingGame.TutorialKey, 1);
        PlayerPrefs.SetInt("pot_has", 1); PlayerPrefs.SetInt("pot_ever", 1);
        PlayerPrefs.SetInt("pot_capacity", bars); PlayerPrefs.SetInt("pot_lives", bars);
        JourneyState.EverHadPot = true; JourneyState.NanaVisits = 1;
    }
    static void FarmerDone()
    {
        HasPot(3);
        PlayerPrefs.SetInt("FarmerComplete", 1);
        PlayerPrefs.SetInt("pot_knowledge", 1);
        if (Bag.Count(Bag.Food) == 0) Bag.Add(Bag.Food);
    }
}
