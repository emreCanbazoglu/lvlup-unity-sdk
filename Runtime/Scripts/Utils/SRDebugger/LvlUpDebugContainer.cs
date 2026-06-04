#if lvlup_srdebugger_enabled
using System;
using System.Collections.Generic;
using SRDebugger;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace LvlUp.Utils
{
    /// <summary>
    /// Simple SR Debugger option container for LvlUp mobile debugging
    /// Allows changing remote config environment and platform in mobile builds
    /// </summary>
    public class LvlUpDebugContainer : IOptionContainer
    {
        private DynamicOptionContainer _dynamicContainer;

        public LvlUpDebugContainer()
        {
            _dynamicContainer = new DynamicOptionContainer();
            RebuildOptions();
        }

        private void RebuildOptions()
        {
            // Clear existing options
            var options = new List<OptionDefinition>(_dynamicContainer.Options);
            foreach (var option in options)
            {
                _dynamicContainer.RemoveOption(option);
            }

            // Environment Override Section
            _dynamicContainer.AddOption(OptionDefinition.Create(
                "Environment",
                () => GetEnvironmentDisplay(),
                null,
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Set Production",
                () => SetEnvironment("production"),
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Set Staging",
                () => SetEnvironment("staging"),
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Set Development",
                () => SetEnvironment("development"),
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Clear Override",
                () => SetEnvironment(null),
                "LvlUp"
            ));

            // Platform Override Section
            _dynamicContainer.AddOption(OptionDefinition.Create(
                "Platform",
                () => GetPlatformDisplay(),
                null,
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Set iOS",
                () => SetPlatform("ios"),
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Set Android",
                () => SetPlatform("android"),
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Set WebGL",
                () => SetPlatform("webgl"),
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Clear Override",
                () => SetPlatform(null),
                "LvlUp"
            ));

            // AB Test Override Section
            _dynamicContainer.AddOption(OptionDefinition.Create(
                "A/B Override",
                () => GetAbOverrideDisplay(),
                null,
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.Create(
                "A/B Layer Key",
                () => global::LvlUp.LvlUpDebugSettings.ForceAbLayerKey ?? "",
                value => global::LvlUp.LvlUpDebugSettings.ForceAbLayerKey = value,
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.Create(
                "A/B Test Key",
                () => global::LvlUp.LvlUpDebugSettings.ForceAbTestKey ?? "",
                value => global::LvlUp.LvlUpDebugSettings.ForceAbTestKey = value,
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.Create(
                "A/B Variant Key",
                () => global::LvlUp.LvlUpDebugSettings.ForceAbVariantKey ?? "",
                value => global::LvlUp.LvlUpDebugSettings.ForceAbVariantKey = value,
                "LvlUp"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Clear A/B Override",
                ClearAbOverride,
                "LvlUp"
            ));

            // SDK Status Section
            var manager = Object.FindObjectOfType<LvlUpManager>();
            if (manager != null && manager.IsInitialized())
            {
                _dynamicContainer.AddOption(OptionDefinition.Create(
                    "Status",
                    () => "✓ Initialized",
                    null,
                    "LvlUp"
                ));

                _dynamicContainer.AddOption(OptionDefinition.Create(
                    "User ID",
                    () => manager.GetCurrentUserId() ?? "N/A",
                    null,
                    "LvlUp"
                ));

                _dynamicContainer.AddOption(OptionDefinition.Create(
                    "Session ID",
                    () => (manager.GetCurrentSession()?.sessionId ?? "N/A"),
                    null,
                    "LvlUp"
                ));
            }
            else
            {
                _dynamicContainer.AddOption(OptionDefinition.Create(
                    "Status",
                    () => "✗ Not Initialized",
                    null,
                    "LvlUp"
                ));
            }
        }

        private string GetEnvironmentDisplay()
        {
            var current = global::LvlUp.LvlUpDebugSettings.EnvironmentOverride;
            if (string.IsNullOrEmpty(current))
            {
                return "production (default)";
            }
            return current;
        }

        private string GetPlatformDisplay()
        {
            var current = global::LvlUp.LvlUpDebugSettings.PlatformOverride;
            if (string.IsNullOrEmpty(current))
            {
                return RuntimePlatform.ToString() + " (default)";
            }
            return current;
        }

        private RuntimePlatform RuntimePlatform
        {
            get
            {
#if UNITY_EDITOR
                return EditorUserBuildSettings.activeBuildTarget switch
                {
                    BuildTarget.iOS => RuntimePlatform.IPhonePlayer,
                    BuildTarget.Android => RuntimePlatform.Android,
                    BuildTarget.WebGL => RuntimePlatform.WebGLPlayer,
                    BuildTarget.StandaloneWindows => RuntimePlatform.WindowsPlayer,
                    BuildTarget.StandaloneWindows64 => RuntimePlatform.WindowsPlayer,
                    BuildTarget.StandaloneOSX => RuntimePlatform.OSXPlayer,
                    BuildTarget.StandaloneLinux64 => RuntimePlatform.LinuxPlayer,
                    _ => Application.platform
                };
#else
                return Application.platform;
#endif
            }
        }

        private void SetEnvironment(string environment)
        {
            global::LvlUp.LvlUpDebugSettings.EnvironmentOverride = environment;
            Debug.Log($"[LvlUp] Environment override set to: {environment ?? "default"}");
            RebuildOptions();
        }

        private void SetPlatform(string platform)
        {
            global::LvlUp.LvlUpDebugSettings.PlatformOverride = platform;
            Debug.Log($"[LvlUp] Platform override set to: {platform ?? "default"}");
            RebuildOptions();
        }

        private string GetAbOverrideDisplay()
        {
            if (!global::LvlUp.LvlUpDebugSettings.HasForcedAbOverride)
            {
                return "none";
            }

            var layer = global::LvlUp.LvlUpDebugSettings.ForceAbLayerKey;
            var test = global::LvlUp.LvlUpDebugSettings.ForceAbTestKey;
            var variant = global::LvlUp.LvlUpDebugSettings.ForceAbVariantKey;
            return string.IsNullOrEmpty(layer) ? $"{test} / {variant}" : $"{layer} / {test} / {variant}";
        }

        private void ClearAbOverride()
        {
            global::LvlUp.LvlUpDebugSettings.ClearForcedAbOverride();
            Debug.Log("[LvlUp] A/B override cleared");
            RebuildOptions();
        }

        // Delegate to DynamicOptionContainer
        IEnumerable<OptionDefinition> IOptionContainer.GetOptions()
        {
            return ((IOptionContainer)_dynamicContainer).GetOptions();
        }

        public bool IsDynamic => true;

        public event Action<OptionDefinition> OptionAdded
        {
            add { _dynamicContainer.OptionAdded += value; }
            remove { _dynamicContainer.OptionAdded -= value; }
        }

        public event Action<OptionDefinition> OptionRemoved
        {
            add { _dynamicContainer.OptionRemoved += value; }
            remove { _dynamicContainer.OptionRemoved -= value; }
        }
    }
}
#endif
