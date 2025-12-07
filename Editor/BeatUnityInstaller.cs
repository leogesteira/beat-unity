using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;

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
        var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages/manifest.json");
        var json = File.ReadAllText(path, Encoding.UTF8);

        if (json.Contains("\"beat\"")) return;

        var registryBlock =
            "\"scopedRegistries\": [\n" +
            "    {\n" +
            "        \"name\": \"Beat\",\n" +
            "        \"url\": \"https://beat-unity.com/\",\n" +
            "        \"scopes\": [\"beat\"]\n" +
            "    }\n" +
            "],";

        if (json.Contains("\"scopedRegistries\""))
        {
            json = json.Replace("\"scopedRegistries\": [", registryBlock.Split('[')[0] + "[");
        }
        else
        {
            var idx = json.IndexOf("{") + 1;
            json = json.Insert(idx, "\n  " + registryBlock + "\n");
        }

        File.WriteAllText(path, json, Encoding.UTF8);
    }

    static void InstallCorePackage()
    {
        var request = UnityEditor.PackageManager.Client.Add("beat.core");
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
}
