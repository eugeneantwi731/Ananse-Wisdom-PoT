#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Fills in the iPhone camera message if it is empty (iOS will not show the camera prompt without it),
// and adds Tools > Anansi Game > Test: camera denied, to see the "Open Settings" card in the Editor.
[InitializeOnLoad]
public static class AnansiIntroTools
{
    const string CamText = "Anansi uses your camera to find the village map and bring the village to life.";
    const string Item = "Tools/Anansi Game/Test: camera denied";

    static AnansiIntroTools()
    {
        EditorApplication.delayCall += () =>
        {
            if (string.IsNullOrEmpty(PlayerSettings.iOS.cameraUsageDescription)) { PlayerSettings.iOS.cameraUsageDescription = CamText; Debug.Log("Anansi Game: iPhone camera message set (Player Settings > iOS > Camera Usage Description)."); }
        };
    }

    [MenuItem(Item, false, 21)]
    static void Toggle()
    {
        bool on = PlayerPrefs.GetInt(CameraGate.SimKey, 0) == 0;
        PlayerPrefs.SetInt(CameraGate.SimKey, on ? 1 : 0);
        if (on) PlayerPrefs.SetInt(CameraGate.AskedKey, 1); // act as if the player already said no
        else PlayerPrefs.DeleteKey(CameraGate.AskedKey);
        PlayerPrefs.Save();
        Debug.Log("Anansi Game: camera denied test is " + (on ? "ON. Tapping Begin shows the Open Settings card. Turn this off (even while playing) and the game continues." : "OFF."));
    }
    [MenuItem(Item, true)] static bool ToggleCheck() { Menu.SetChecked(Item, PlayerPrefs.GetInt(CameraGate.SimKey, 0) == 1); return true; }
}
#endif
