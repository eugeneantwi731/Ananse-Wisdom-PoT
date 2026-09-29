#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// Adds the Photos permission text iOS needs before the game can save a kente picture.
public static class AnansiIOSBuild
{
    [PostProcessBuild(100)]
    static void AddPhotoPermission(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument(); plist.ReadFromFile(plistPath);
        var root = plist.root;
        if (!root.values.ContainsKey("NSPhotoLibraryAddUsageDescription"))
            root.SetString("NSPhotoLibraryAddUsageDescription", "Save the kente you weave to your Photos.");
        plist.WriteToFile(plistPath);
    }
}
#endif
