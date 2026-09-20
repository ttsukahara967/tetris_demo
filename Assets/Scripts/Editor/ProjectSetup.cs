using System.IO;
using UnityEditor;

namespace Tetris.EditorTools
{
    /// <summary>
    /// The local Nakama / AI servers use http, so allow http requests in UnityWebRequest.
    /// (With the default "Not allowed" setting, connections are refused.)
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string SettingsPath = "ProjectSettings/ProjectSettings.asset";

        static ProjectSetup()
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            // Only the in-memory value may have changed without reaching disk, so decide by looking at the file.
            if (!File.ReadAllText(SettingsPath).Contains("insecureHttpOption: 2"))
                AssetDatabase.SaveAssets();
        }
    }
}
