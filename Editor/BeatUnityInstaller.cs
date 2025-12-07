using System.IO;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Linq;

[InitializeOnLoad]
public class BeatUnityInstaller
{
    static BeatUnityInstaller()
    {
        if (EditorPrefs.GetBool("beat_unity_installed")) return;
        EditorApplication.update += Run;
    }

    static void Run()
    {
        EditorApplication.update -= Run;
        InstallRegistry();
        InstallCorePackage();
        EditorPrefs.SetBool("beat_unity_installed", true);
        EditorUtility.DisplayDialog("Beat Unity", "Registry installed.", "OK");
        TryDeleteSelf();
    }

    static void InstallRegistry()
    {
        var manifestPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages/manifest.json");

        // Read without BOM
        var raw = File.ReadAllText(manifestPath);
        raw = RemoveBOM(raw);

        var manifest = JsonUtility.FromJson<ManifestWrapper>(raw);

        if (manifest.scopedRegistries == null)
            manifest.scopedRegistries = new List<ScopedRegistry>();

        // Already installed?
        if (manifest.scopedRegistries.Any(r => r.name == "Beat Unity"))
            return;

        manifest.scopedRegistries.Add(new ScopedRegistry
        {
            name = "Beat Unity",
            url = "https://beat-unity.com/",
            scopes = new[] { "beat" }
        });

        // Serialize back (Unity's JsonUtility cannot handle lists at root, wrap class required)
        var json = JsonUtility.ToJson(manifest, true);

        // Write as UTF-8 *without* BOM
        File.WriteAllText(manifestPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    static string RemoveBOM(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // Remove UTF-8 BOM if present
        if (text.Length > 0 && text[0] == '\uFEFF')
            return text.Substring(1);

        return text;
    }

    static void InstallCorePackage()
    {
        UnityEditor.PackageManager.Client.Add("beat.core");
    }

    static void TryDeleteSelf()
    {
        var scriptPath = AssetDatabase.FindAssets("BeatUnityInstaller")
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(scriptPath)) return;

        File.Delete(scriptPath);

        var meta = scriptPath + ".meta";
        if (File.Exists(meta)) File.Delete(meta);

        AssetDatabase.Refresh();
    }

    [System.Serializable]
    public class ManifestWrapper
    {
        public List<ScopedRegistry> scopedRegistries;
        public Dictionary<string, string> dependencies;
    }

    [System.Serializable]
    public class ScopedRegistry
    {
        public string name;
        public string url;
        public string[] scopes;
    }
}
