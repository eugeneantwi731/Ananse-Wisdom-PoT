using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

// Saves a picture where the player can actually find it:
//   iPhone/iPad -> the Photos app (asks permission the first time)
//   Android     -> Gallery, album "Anansi Kente"
//   Editor / PC / Mac -> Pictures/Anansi Kente (the folder opens in the Editor)
// A backup copy always goes to Application.persistentDataPath/Kente too.
public static class KenteGallery
{
    public const string Album = "Anansi Kente";
    static Action<bool, string> iosDone;

    public static void Save(byte[] png, string fileName, Action<bool, string> done)
    {
        try
        {
            string backupDir = Path.Combine(Application.persistentDataPath, "Kente"); Directory.CreateDirectory(backupDir);
            string backup = Path.Combine(backupDir, fileName); File.WriteAllBytes(backup, png);
#if UNITY_IOS && !UNITY_EDITOR
            iosDone = done; EnsureReceiver();
            _AnansiSaveToPhotos(backup);
#elif UNITY_ANDROID && !UNITY_EDITOR
            SaveAndroid(png, fileName);
            done?.Invoke(true, "Saved to your Gallery");
#else
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Album);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName); File.WriteAllBytes(path, png);
            Debug.Log("Kente picture saved: " + path);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(path);
#endif
            done?.Invoke(true, "Saved to Pictures/" + Album);
#endif
        }
        catch (Exception e)
        {
            Debug.LogError("Kente picture could not be saved: " + e.Message);
            done?.Invoke(false, "Couldn't save. Try again");
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void _AnansiSaveToPhotos(string path);
#endif

    // iOS reports back through UnitySendMessage("AnansiGallery", "OnSaved", "ok" | error text)
    class Receiver : MonoBehaviour
    {
        public void OnSaved(string result)
        {
            bool ok = result == "ok";
            if (!ok) Debug.LogWarning("Photos: " + result);
            var d = iosDone; iosDone = null;
            d?.Invoke(ok, ok ? "Saved to Photos" : "Allow Photos access in Settings");
        }
    }
    static void EnsureReceiver()
    {
        if (GameObject.Find("AnansiGallery") != null) return;
        var go = new GameObject("AnansiGallery"); UnityEngine.Object.DontDestroyOnLoad(go); go.AddComponent<Receiver>();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static void SaveAndroid(byte[] png, string fileName)
    {
        using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
        using (var resolver = act.Call<AndroidJavaObject>("getContentResolver"))
        using (var values = new AndroidJavaObject("android.content.ContentValues"))
        using (var ver = new AndroidJavaClass("android.os.Build$VERSION"))
        using (var media = new AndroidJavaClass("android.provider.MediaStore$Images$Media"))
        {
            values.Call("put", "_display_name", fileName);
            values.Call("put", "mime_type", "image/png");
            if (ver.GetStatic<int>("SDK_INT") >= 29) values.Call("put", "relative_path", "Pictures/" + Album);
            using (var uri = media.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            using (var item = resolver.Call<AndroidJavaObject>("insert", uri, values))
            using (var os = resolver.Call<AndroidJavaObject>("openOutputStream", item))
            {
                os.Call("write", (sbyte[])(Array)png);
                os.Call("close");
            }
        }
    }
#endif
}
