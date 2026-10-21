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

            // AB Test Override Section: one forced variant per layer, picked from the backend catalog
            _dynamicContainer.AddOption(OptionDefinition.Create(
                "A/B Overrides",
                () => GetAbOverrideDisplay(),
                null,
                "LvlUp A/B"
            ));

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Fetch A/B Tests",
                FetchAbCatalog,
                "LvlUp A/B"
            ));

            AddAbVariantOptions();

            _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                "Clear A/B Overrides",
                ClearAbOverride,
                "LvlUp A/B"
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
            var overrides = global::LvlUp.LvlUpDebugSettings.ForcedAbOverrides;
            if (overrides.Count == 0)
            {
                return "none";
            }

            var parts = new List<string>();
            foreach (var forced in overrides)
            {
                parts.Add(forced.ToString());
            }
            return string.Join(", ", parts);
        }

        /// <summary>
        /// One button per variant in the fetched catalog. Tapping a variant forces it for its
        /// layer (replacing any other variant in that layer); tapping a forced variant clears it.
        /// </summary>
        private void AddAbVariantOptions()
        {
            var remoteConfigService = GetRemoteConfigService(out _);
            var catalog = remoteConfigService?.GetAbDebugCatalog();
            if (catalog == null) return;

            foreach (var layer in catalog)
            {
                if (layer?.tests == null) continue;
                var forced = global::LvlUp.LvlUpDebugSettings.GetForcedAbOverride(layer.key);

                foreach (var test in layer.tests)
                {
                    if (test?.variants == null) continue;
                    foreach (var variant in test.variants)
                    {
                        bool selected = forced != null && forced.testKey == test.key && forced.variantKey == variant.key;
                        string layerKey = layer.key;
                        string testKey = test.key;
                        string variantKey = variant.key;

                        _dynamicContainer.AddOption(OptionDefinition.FromMethod(
                            $"{(selected ? "[x]" : "[ ]")} {layer.key}: {test.key} / {variant.key}",
                            () => ToggleAbVariant(layerKey, testKey, variantKey, selected),
                            "LvlUp A/B"
                        ));
                    }
                }
            }
        }

        private void ToggleAbVariant(string layerKey, string testKey, string variantKey, bool selected)
        {
            if (selected)
            {
                global::LvlUp.LvlUpDebugSettings.ClearForcedAbOverride(layerKey);
                Debug.Log($"[LvlUp] A/B override cleared for layer {layerKey}");
            }
            else
            {
                global::LvlUp.LvlUpDebugSettings.SetForcedAbOverride(layerKey, testKey, variantKey);
                Debug.Log($"[LvlUp] A/B override set: {layerKey} / {testKey} / {variantKey}");
            }
            RefetchConfig();
            RebuildOptions();
        }

        private void FetchAbCatalog()
        {
            var remoteConfigService = GetRemoteConfigService(out var manager);
            if (remoteConfigService == null)
            {
                Debug.LogWarning("[LvlUp] Cannot fetch A/B tests: SDK is not initialized");
                return;
            }

            remoteConfigService.FetchAbDebugCatalogAsync(manager, (success, layers) => RebuildOptions());
        }

        private void ClearAbOverride()
        {
            global::LvlUp.LvlUpDebugSettings.ClearForcedAbOverride();
            Debug.Log("[LvlUp] A/B overrides cleared");
            RefetchConfig();
            RebuildOptions();
        }

        private void RefetchConfig()
        {
            var remoteConfigService = GetRemoteConfigService(out var manager);
            remoteConfigService?.FetchAsync(manager);
        }

        private static global::LvlUp.Services.RemoteConfigService GetRemoteConfigService(out LvlUpManager manager)
        {
            manager = Object.FindObjectOfType<LvlUpManager>();
            return manager != null && manager.IsInitialized() ? manager.GetRemoteConfigService() : null;
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
