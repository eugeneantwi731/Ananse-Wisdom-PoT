#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// Imports the game's pictures as sprites at their true size (no squashing).
public class AnansiImageImporter : AssetPostprocessor
{
    static readonly string[] Dirs = { "/Resources/FarmerGame/", "/Resources/PotLives/", "/Resources/PotLifeBar/", "/Resources/KenteGame/", "/Resources/Bag/", "/Resources/Pins/", "/Resources/Characters/" };
    void OnPreprocessTexture()
    {
        string p = assetPath.Replace('\\', '/');
        if (!p.Contains("/AnansiGame/") || !Dirs.Any(d => p.Contains(d))) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = 2048;
    }
}

// Everything is under Tools > Anansi Game.
public static class AnansiMenu
{
    const string M = "Tools/Anansi Game/";
    const string TestItem = M + "Test Mode (show everything)";

    [MenuItem(M + "Check Project", false, 0)] static void Check() => AnansiDoctor.Open();

    [MenuItem(M + "Fix Image Import", false, 1)]
    public static void FixImages()
    {
        int n = 0;
        foreach (var p in AssetDatabase.GetAllAssetPaths())
            if (p.Contains("/AnansiGame/Resources/") && p.EndsWith(".png")) { AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); n++; }
        Debug.Log("Anansi Game: re-imported " + n + " images.");
    }

    [MenuItem(TestItem, false, 20)]
    static void ToggleTest()
    {
        AnansiRuntime.TestMode = !AnansiRuntime.TestMode;
        Debug.Log("Anansi Game: Test Mode is " + (AnansiRuntime.TestMode ? "ON. Every pin shows and games open without a pot." : "OFF. The real journey rules apply."));
    }
    [MenuItem(TestItem, true)] static bool ToggleTestCheck() { Menu.SetChecked(TestItem, AnansiRuntime.TestMode); return true; }

    // ---------- scene setup ----------
    [MenuItem(M + "Setup/1. Add game systems to this scene", false, 40)]
    public static void AddSystems()
    {
        var scene = SceneManager.GetActiveScene();
        var root = GameObject.Find("Anansi Game Systems");
        if (root == null) { root = new GameObject("Anansi Game Systems"); Undo.RegisterCreatedObjectUndo(root, "Anansi systems"); }
        var lm = UnityEngine.Object.FindAnyObjectByType<LifeManager>(FindObjectsInactive.Include);
        if (lm == null) { var pot = new GameObject("Pot"); pot.transform.SetParent(root.transform, false); lm = pot.AddComponent<LifeManager>(); Undo.RegisterCreatedObjectUndo(pot, "Pot"); }
        if (lm.GetComponent<PotLifeBarHUD>() == null) Undo.AddComponent<PotLifeBarHUD>(lm.gameObject);
        if (UnityEngine.Object.FindAnyObjectByType<BagBarHUD>(FindObjectsInactive.Include) == null) { var b = new GameObject("Bag"); b.transform.SetParent(root.transform, false); b.AddComponent<BagBarHUD>(); Undo.RegisterCreatedObjectUndo(b, "Bag"); }
        if (UnityEngine.Object.FindAnyObjectByType<JourneyDialogue>(FindObjectsInactive.Include) == null) { var d = new GameObject("Dialogue"); d.transform.SetParent(root.transform, false); d.AddComponent<JourneyDialogue>(); Undo.RegisterCreatedObjectUndo(d, "Dialogue"); }
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null) { var es = new GameObject("EventSystem", typeof(EventSystem)); Undo.RegisterCreatedObjectUndo(es, "EventSystem"); }
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Anansi Game: Pot, Bag and Dialogue are in the scene under 'Anansi Game Systems'. Save the scene (Ctrl+S).");
    }

    [MenuItem(M + "Setup/2. Connect my pins", false, 41)]
    public static void ConnectPins()
    {
        var pins = UnityEngine.Object.FindObjectsByType<KnowledgePin>(FindObjectsInactive.Include);
        if (pins.Length == 0) { EditorUtility.DisplayDialog("Anansi Game", "No Knowledge Pins were found in this scene. Open your map scene first.", "OK"); return; }
        var report = new List<string>();
        foreach (var kp in pins)
        {
            var jp = kp.GetComponent<JourneyPin>();
            if (jp == null)
            {
                jp = Undo.AddComponent<JourneyPin>(kp.gameObject);
                jp.pin = Guess(kp.pinId + " " + kp.displayName + " " + kp.name);
                SetCharacterLines(jp);
            }
            if (jp.openButton == null && kp.openButton != null) jp.openButton = kp.openButton;
            if (jp.pin == JourneyPin.PinId.NanaNyame && kp.GetComponent<NanaNyameOffering>() == null) Undo.AddComponent<NanaNyameOffering>(kp.gameObject);
            EditorUtility.SetDirty(jp);
            report.Add(kp.name + "  →  " + jp.pin);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Anansi Game: pins connected", string.Join("\n", report) + "\n\nIf a pin got the wrong type, select it and change 'Pin' on its Journey Pin. Then save the scene.", "OK");
    }

    static JourneyPin.PinId Guess(string s)
    {
        s = s.ToLower();
        if (s.Contains("nana") || s.Contains("nyame")) return JourneyPin.PinId.NanaNyame;
        if (s.Contains("farm")) return JourneyPin.PinId.Farmer;
        if (s.Contains("carv")) return JourneyPin.PinId.Carver;
        if (s.Contains("drum")) return JourneyPin.PinId.Drummer;
        if (s.Contains("danc")) return JourneyPin.PinId.Dancer;
        return JourneyPin.PinId.Home; // weaver / Anansi's home
    }

    static void SetCharacterLines(JourneyPin jp)
    {
        if (jp.pin == JourneyPin.PinId.Drummer)
        {
            jp.characterName = "The Drummer";
            jp.missingItemLine = "A drummer with no drum? Bring me a drum, Ananse, and I will teach you the rhythm.";
            jp.thanksLine = "Ah, a fine drum! Sit down and listen closely.";
        }
        else if (jp.pin == JourneyPin.PinId.Dancer)
        {
            jp.characterName = "The Dancer";
            jp.missingItemLine = "I cannot dance without a rhythm, Ananse. Bring me one.";
            jp.thanksLine = "Yes! Now that is a rhythm. Come, let us dance.";
        }
    }

    // ---------- journey save (use these while NOT in Play mode) ----------
    [MenuItem(M + "Journey/New Game (clear save)", false, 60)]
    static void NewGame() { JourneyState.NewGame(); Debug.Log("Anansi Game: save cleared. Press Play to start the journey from the beginning."); }

    [MenuItem(M + "Journey/Jump: tutorial kente woven", false, 61)]
    static void JumpKente()
    {
        PlayerPrefs.SetInt(KenteWeavingGame.TutorialKey, 1);
        if (KenteBag.Count == 0) KenteBag.Add(3, 6, 3);
        PlayerPrefs.Save(); Debug.Log("Anansi Game: tutorial done and one kente is in the Bag. Nana Nyame's pin will show.");
    }

    [MenuItem(M + "Journey/Jump: pot received", false, 62)]
    static void JumpPot()
    {
        PlayerPrefs.SetInt(KenteWeavingGame.TutorialKey, 1);
        PlayerPrefs.SetInt("pot_has", 1); PlayerPrefs.SetInt("pot_capacity", 3); PlayerPrefs.SetInt("pot_lives", 3);
        JourneyState.EverHadPot = true; if (JourneyState.NanaVisits == 0) JourneyState.NanaVisits = 1;
        PlayerPrefs.Save(); Debug.Log("Anansi Game: Anansi has a 3-segment pot. The Farmer pin will show.");
    }

    [MenuItem(M + "Journey/Jump: Farmer done", false, 63)]
    static void JumpFarmer()
    {
        JumpPot();
        PlayerPrefs.SetInt("FarmerComplete", 1);
        PlayerPrefs.SetInt("pot_knowledge", PlayerPrefs.GetInt("pot_knowledge", 0) | 1);
        if (Bag.Count(Bag.Food) == 0) Bag.Add(Bag.Food);
        PlayerPrefs.Save(); Debug.Log("Anansi Game: Farmer finished, 1 food in the Bag. The Carver pin will show.");
    }

    [MenuItem(M + "Journey/New Game (clear save)", true)]
    [MenuItem(M + "Journey/Jump: tutorial kente woven", true)]
    [MenuItem(M + "Journey/Jump: pot received", true)]
    [MenuItem(M + "Journey/Jump: Farmer done", true)]
    static bool NotPlaying() => !EditorApplication.isPlaying;
}

// Tools > Anansi Game > Check Project: finds the usual reasons everything goes dead, with a fix button for each.
public class AnansiDoctor : EditorWindow
{
    enum Lvl { Ok, Warn, Error }
    class Item { public Lvl lvl; public string text, fixLabel; public Action fix; }
    readonly List<Item> items = new List<Item>();
    Vector2 scroll; Action pending;

    static readonly string[] OurScripts = {
        "AnansiRuntime", "LifeManager", "PotLifeBarHUD", "DamageOnTouch", "Bag", "KenteBag", "JourneyState",
        "KnowledgePin", "JourneyPin", "JourneyDialogue", "NanaNyameOffering", "BagBarHUD",
        "FarmerGameController", "FarmerGameData", "CropDrag", "KenteWeavingGame", "KenteData", "KenteUI", "KenteStandInLoom", "LoadingScreen" };
    static readonly string[] OldOnlyScripts = { "PotLivesHUD", "FarmerGameImageImporter", "KenteImageImporter" };
    static readonly string[] OldFolders = { "AnansiGame_Scripts", "AnansiJourney_DropIn" };

    public static void Open() { var w = GetWindow<AnansiDoctor>("Anansi Check"); w.minSize = new Vector2(520, 420); w.Run(); }

    void Add(Lvl l, string t, string fixLabel = null, Action fix = null) => items.Add(new Item { lvl = l, text = t, fixLabel = fixLabel, fix = fix });

    static string OurRoot()
    {
        var g = AssetDatabase.FindAssets("AnansiRuntime t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => p.EndsWith("/AnansiRuntime.cs"));
        if (g == null) return null;
        int i = g.IndexOf("/Scripts/Core/"); return i > 0 ? g.Substring(0, i) : null;
    }

    void Run()
    {
        items.Clear();
        string root = OurRoot();

        if (EditorUtility.scriptCompilationFailed)
            Add(Lvl.Error, "Unity has red script errors, and while they exist no script runs (buttons go dead). Fix the duplicates below first. If errors stay, open Window > General > Console, click the first red line and send it to me.");

        // 1. duplicate / old scripts
        var byName = new Dictionary<string, List<string>>();
        foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".cs")) continue;
            var name = Path.GetFileNameWithoutExtension(path);
            if (!OurScripts.Contains(name) && !OldOnlyScripts.Contains(name)) continue;
            if (!byName.TryGetValue(name, out var list)) byName[name] = list = new List<string>();
            list.Add(path);
        }
        var oldCopies = new List<string>();
        foreach (var kv in byName)
            foreach (var p in kv.Value)
                if (OldOnlyScripts.Contains(kv.Key) || (root != null && !p.StartsWith(root + "/") && kv.Value.Count > 1)) oldCopies.Add(p);
        foreach (var f in AssetDatabase.GetAllAssetPaths())
            if (AssetDatabase.IsValidFolder(f) && OldFolders.Any(o => f.EndsWith("/" + o)) && (root == null || !f.StartsWith(root))) oldCopies.Add(f);
        oldCopies = oldCopies.Distinct().ToList();
        if (oldCopies.Count > 0)
            Add(Lvl.Error, "Old copies of the game scripts are still in the project. Two copies of a script stop everything from compiling:\n  " + string.Join("\n  ", oldCopies.Take(12)) + (oldCopies.Count > 12 ? "\n  …" : ""),
                "Delete old copies", () =>
                {
                    if (!EditorUtility.DisplayDialog("Delete old copies?", "These will be deleted:\n\n" + string.Join("\n", oldCopies.Take(25)), "Delete", "Cancel")) return;
                    var failed = new List<string>(); AssetDatabase.DeleteAssets(oldCopies.ToArray(), failed);
                    AssetDatabase.Refresh();
                });
        else Add(Lvl.Ok, "One copy of every game script" + (root != null ? " (in " + root + ")." : "."));

        // 2. input handling (works either way now; shown for information)
        var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (ps != null && ps.Length > 0)
        {
            var prop = new SerializedObject(ps[0]).FindProperty("activeInputHandler");
            if (prop != null) Add(Lvl.Ok, "Input handling: " + (prop.intValue == 0 ? "Input Manager (Old)" : prop.intValue == 1 ? "Input System Package (New)" : "Both") + ". The game scripts work with any of these.");
        }

        // 3. open scene
        var scene = SceneManager.GetActiveScene();
        var bad = new List<GameObject>(); int missing = 0;
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n > 0) { missing += n; bad.Add(t.gameObject); }
            }
        if (missing > 0)
            Add(Lvl.Warn, "Scene '" + scene.name + "' has " + missing + " 'Missing script' slot(s) on: " + string.Join(", ", bad.Take(8).Select(g => g.name)) + (bad.Count > 8 ? " …" : "") + ". They are leftovers from deleted scripts and do nothing.",
                "Remove them", () =>
                {
                    foreach (var g in bad)
                    {
                        if (g == null) continue;
                        try { Undo.RegisterCompleteObjectUndo(g, "Remove missing scripts"); GameObjectUtility.RemoveMonoBehavioursWithMissingScript(g); }
                        catch (Exception e) { Debug.LogWarning("Could not clean " + g.name + " (it may be inside a prefab: open the prefab and remove it there). " + e.Message); }
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                });
        else Add(Lvl.Ok, "No missing scripts in scene '" + scene.name + "'.");

        int es = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include).Length;
        if (es > 1) Add(Lvl.Warn, "This scene has " + es + " EventSystems. Keep one and delete the others (search 'EventSystem' in the Hierarchy).");

        var pins = UnityEngine.Object.FindObjectsByType<KnowledgePin>(FindObjectsInactive.Include);
        if (pins.Length > 0)
        {
            int lmCount = UnityEngine.Object.FindObjectsByType<LifeManager>(FindObjectsInactive.Include).Length;
            if (lmCount == 0 || UnityEngine.Object.FindAnyObjectByType<BagBarHUD>(FindObjectsInactive.Include) == null)
                Add(Lvl.Warn, "This map scene is missing the pot and/or the Bag bar.", "Add them", AnansiMenu.AddSystems);
            else if (lmCount > 1) Add(Lvl.Warn, "This scene has " + lmCount + " Life Managers. Keep only one.");
            else Add(Lvl.Ok, "Pot and Bag are in the scene.");

            int unlinked = pins.Count(p => p.GetComponent<JourneyPin>() == null);
            if (unlinked > 0) Add(Lvl.Warn, unlinked + " pin(s) have no Journey Pin, so they don't follow the journey rules.", "Connect pins", AnansiMenu.ConnectPins);
            var types = pins.Select(p => p.GetComponent<JourneyPin>()).Where(j => j != null).Select(j => j.pin).Distinct().ToList();
            if (unlinked == 0)
            {
                Add(Lvl.Ok, "Pins: " + string.Join(", ", types));
                if (!types.Contains(JourneyPin.PinId.Home)) Add(Lvl.Warn, "No Home (weaver) pin, so the kente game can't be opened from the map.");
                if (!types.Contains(JourneyPin.PinId.NanaNyame)) Add(Lvl.Warn, "No Nana Nyame pin, so Anansi can never get a pot (turn on Test Mode to test without one).");
            }
        }
        else Add(Lvl.Ok, "No pins in scene '" + scene.name + "'. Open your map scene to check the pins too.");

        // 4. art
        var missingArt = new[] { "FarmerGame/farmer", "FarmerGame/crop_maize", "PotLifeBar/pot_1", "PotLives/pot", "KenteGame/spool_red", "Bag/item_kente" }.Where(p => Resources.Load<Texture2D>(p) == null).ToList();
        if (missingArt.Count > 0) Add(Lvl.Error, "Missing pictures in Resources: " + string.Join(", ", missingArt) + ". Copy the whole AnansiGame folder again.");
        else Add(Lvl.Ok, "All game pictures found.");
        if (Resources.Load<GameObject>("KenteGame/kente_strip_loom") == null)
            Add(Lvl.Warn, "The 3D loom (kente_strip_loom.glb) can't load because Unity needs the free glTFast package to read .glb files. Until it's installed, the weaving game uses a simple block loom.",
                "Install glTFast", () => { UnityEditor.PackageManager.Client.Add("com.unity.cloud.gltfast"); Debug.Log("Anansi Game: installing glTFast… wait for Unity to finish, then click Check again."); });
        else Add(Lvl.Ok, "3D loom loads.");

        // 5. save state
        Add(Lvl.Ok, "Journey save: tutorial " + (JourneyState.TutorialWoven ? "woven" : "not woven") + ", pot " + (PlayerPrefs.GetInt("pot_has", 0) == 1 ? "yes" : "no") + ", Farmer " + (JourneyState.Done(JourneyState.Farmer) ? "done" : "not done") + ", kente in Bag " + KenteBag.Count + ". Test Mode " + (AnansiRuntime.TestMode ? "ON" : "off") + ".");
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Anansi Game: project check", EditorStyles.boldLabel);
        if (GUILayout.Button("Check again", GUILayout.Height(26))) Run();
        EditorGUILayout.Space(4);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var it in items)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            var old = GUI.color;
            GUI.color = it.lvl == Lvl.Ok ? new Color(.55f, .9f, .5f) : it.lvl == Lvl.Warn ? new Color(1f, .8f, .35f) : new Color(1f, .45f, .4f);
            GUILayout.Label(it.lvl == Lvl.Ok ? "OK" : it.lvl == Lvl.Warn ? " ! " : " X ", EditorStyles.boldLabel, GUILayout.Width(28));
            GUI.color = old;
            GUILayout.Label(it.text, EditorStyles.wordWrappedLabel);
            if (it.fix != null && GUILayout.Button(it.fixLabel, GUILayout.Width(130), GUILayout.Height(24))) pending = it.fix;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (pending != null && Event.current.type == EventType.Repaint)
        {
            var a = pending; pending = null;
            EditorApplication.delayCall += () => { a(); Run(); };
        }
    }
}
#endif
