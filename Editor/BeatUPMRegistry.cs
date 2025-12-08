using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;  

namespace Beat.UPM.Editor
{

    [InitializeOnLoad]
    public class BeatUPMRegistry
    {
        private static readonly string BeatMarkerPath =
            Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "ProjectSettings/BeatUPM.txt"
            );

        static BeatUPMRegistry()
        {
            // If marker file exists, we've already run for this project.
            if (File.Exists(BeatMarkerPath)) return;

            EditorApplication.update += Run;
        }

        [MenuItem("Tools/Beat/Add UPM Registry")]
        static void Run()
        {
            EditorApplication.update -= Run;

            try
            {
                InstallRegistry();
                InstallCorePackage();

                // Create (or overwrite) the marker file so we don't run again for this project.
                Directory.CreateDirectory(
                    Path.GetDirectoryName(BeatMarkerPath) ?? "ProjectSettings"
                );
                File.WriteAllText(BeatMarkerPath, "Beat Unity registry installed.");

                EditorUtility.DisplayDialog("Beat", "Registry installed.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[Beat UPM] Failed to install registry:\n" + e);
                EditorUtility.DisplayDialog("Beat", "Failed to install registry. Check console for details.", "OK");
            }
        }

        static void InstallRegistry()
        {
            var manifestPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Packages/manifest.json"
            );

            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[Beat UPM] manifest.json not found at " + manifestPath);
                return;
            }

            // Read as UTF‑8, strip BOM if any
            var raw = File.ReadAllText(manifestPath, Encoding.UTF8);
            raw = RemoveBOM(raw);

            // If our registry is already present, do nothing.
            // This is text-based and tolerant of extra fields/spacing.
            if (raw.IndexOf("\"name\"",
                    StringComparison.Ordinal) >= 0 &&
                raw.IndexOf("\"Beat\"",
                    StringComparison.Ordinal) > raw.IndexOf("\"name\"",
                    StringComparison.Ordinal))
            {
                if (raw.Contains("\"name\": \"Beat\"") || raw.Contains("\"name\" : \"Beat\""))
                {
                    Debug.Log("[Beat UPM] Beat registry already present, skipping.");
                    return;
                }
            }

            const string beatRegistryJson =
                "    {\n" +
                "      \"name\": \"Beat\",\n" +
                "      \"url\": \"https://beat-unity.com/\",\n" +
                "      \"scopes\": [\n" +
                "        \"beat\"\n" +
                "      ]\n" +
                "    }";

            string updated;

            // Try to locate an existing top-level "scopedRegistries" array
            var scopedKeyIndex = raw.IndexOf("\"scopedRegistries\"", StringComparison.Ordinal);
            if (scopedKeyIndex >= 0)
            {
                updated = InsertIntoExistingScopedRegistries(raw, scopedKeyIndex, beatRegistryJson);
            }
            else
            {
                updated = AddNewScopedRegistriesBlock(raw, beatRegistryJson);
            }

            if (updated == null)
            {
                Debug.LogError("[Beat UPM] Failed to modify manifest.json due to unexpected structure.");
                return;
            }

            // Write as UTF-8 without BOM
            File.WriteAllText(
                manifestPath,
                updated,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            );

            Debug.Log("[Beat UPM] Beat scoped registry added to manifest.json.");
        }

        static string InsertIntoExistingScopedRegistries(string raw, int scopedKeyIndex, string beatRegistryJson)
        {
            // Find the '[' that starts the array
            var arrayStart = raw.IndexOf('[', scopedKeyIndex);
            if (arrayStart < 0) return null;

            // Find the matching ']' (brace-depth aware for minimal robustness)
            var arrayEnd = FindMatchingBracket(raw, arrayStart, '[', ']');
            if (arrayEnd < 0) return null;

            var arrayContent = raw.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            var trimmed = arrayContent.Trim();

            // If array already contains "Beat" by name, bail out (double safety)
            if (trimmed.Contains("\"name\": \"Beat\"") || trimmed.Contains("\"name\" : \"Beat\""))
                return raw;

            bool isEmpty = string.IsNullOrWhiteSpace(trimmed);

            string newArrayContent;
            if (isEmpty)
            {
                // Just our registry
                // Keep surrounding indentation consistent with manifest style
                newArrayContent = "\n" + beatRegistryJson + "\n  ";
            }
            else
            {
                // Ensure trailing comma on existing content
                // We don't try to parse JSON; we just append after the last '}' before the final ']'.
                // Safest is: trim right, ensure it ends with '}', then add ",\n<entry>"
                var rightTrimmed = arrayContent.TrimEnd();
                if (!rightTrimmed.EndsWith("}", StringComparison.Ordinal))
                {
                    // Unexpected content, but we can still try a simple ", <entry>"
                    newArrayContent = arrayContent + ",\n" + beatRegistryJson + "\n  ";
                }
                else
                {
                    // Insert comma after the existing block
                    // We preserve original left part as-is
                    newArrayContent = arrayContent.TrimEnd() + ",\n" + beatRegistryJson + "\n  ";
                }
            }

            return raw.Substring(0, arrayStart + 1)
                 + newArrayContent
                 + raw.Substring(arrayEnd);
        }

        static string AddNewScopedRegistriesBlock(string raw, string beatRegistryJson)
        {
            // Insert a new "scopedRegistries": [ ... ] near the top of the root object.
            var firstBrace = raw.IndexOf('{');
            if (firstBrace < 0) return null;

            var insertPos = firstBrace + 1;

            // We inject with a trailing comma; the next property will follow.
            // This is how Unity typically formats manifest.json anyway.
            var scopedBlock =
                "\n  \"scopedRegistries\": [\n" +
                beatRegistryJson + "\n" +
                "  ],";

            return raw.Insert(insertPos, scopedBlock);
        }

        static int FindMatchingBracket(string text, int startIndex, char open, char close)
        {
            int depth = 0;
            for (int i = startIndex; i < text.Length; i++)
            {
                var c = text[i];
                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1; // not found / malformed
        }

        static string RemoveBOM(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length > 0 && text[0] == '\uFEFF')
                return text.Substring(1);
            return text;
        }


        private static AddRequest addRequest;
        
        static void InstallCorePackage()
        {
            addRequest = Client.Add("beat.core");
            EditorApplication.update += CheckAddRequest;
        }
    
        static void CheckAddRequest()
        {
            if (!addRequest.IsCompleted) return;
    
            EditorApplication.update -= CheckAddRequest;
    
            if (addRequest.Status == StatusCode.Success)
                Debug.Log("[Beat UPM] Installed beat.core package.");
            else
                Debug.LogError("[Beat UPM] Failed to install beat.core: " + addRequest.Error.message);
        }
    }
}
