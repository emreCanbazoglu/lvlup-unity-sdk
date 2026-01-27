using UnityEngine;
using UnityEditor;

namespace LvlUp.Editor
{
    /// <summary>
    /// Menu items for LvlUp configuration management
    /// </summary>
    public class LvlUpMenu
    {
        private const string CONFIG_FOLDER = "Assets/lvlup-unity-sdk/Resources";
        private const string CONFIG_NAME = "LvlUpConfig";
        private const string CONFIG_PATH = CONFIG_FOLDER + "/" + CONFIG_NAME + ".asset";

        [MenuItem("Assets/LvlUp/Create Configuration")]
        public static void CreateLvlUpConfig()
        {
            // Create Resources folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(CONFIG_FOLDER))
            {
                string parentFolder = "Assets/lvlup-unity-sdk";
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "lvlup-unity-sdk");
                }
                AssetDatabase.CreateFolder(parentFolder, "Resources");
            }

            // Create or get existing config
            LvlUpConfigScriptable config = AssetDatabase.LoadAssetAtPath<LvlUpConfigScriptable>(CONFIG_PATH);
            
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<LvlUpConfigScriptable>();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Success", $"LvlUpConfig created at {CONFIG_PATH}", "OK");
                Selection.activeObject = config;
            }
            else
            {
                EditorUtility.DisplayDialog("Info", $"LvlUpConfig already exists at {CONFIG_PATH}", "OK");
                Selection.activeObject = config;
            }
        }

        [MenuItem("Window/LvlUp/Open Configuration")]
        public static void OpenLvlUpConfig()
        {
            LvlUpConfigScriptable config = AssetDatabase.LoadAssetAtPath<LvlUpConfigScriptable>(CONFIG_PATH);
            
            if (config == null)
            {
                EditorUtility.DisplayDialog("Not Found", "LvlUpConfig not found. Please create one first using Assets/LvlUp/Create Configuration", "OK");
                return;
            }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}

