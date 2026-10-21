using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace LvlUp
{
    /// <summary>
    /// Debug settings for LvlUp SDK - used for testing
    /// Persists across Editor and builds using PlayerPrefs
    /// </summary>
    public static class LvlUpDebugSettings
    {
        private const string PLATFORM_OVERRIDE_KEY = "LvlUp.Debug.PlatformOverride";
        private const string ENVIRONMENT_OVERRIDE_KEY = "LvlUp.Debug.EnvironmentOverride";
        private const string FORCED_AB_OVERRIDES_KEY = "LvlUp.Debug.ForcedAbOverrides";
        // Pre-1.11 single-override keys; migrated into FORCED_AB_OVERRIDES_KEY on first read.
        private const string LEGACY_FORCE_AB_LAYER_KEY = "LvlUp.Debug.ForceAbLayerKey";
        private const string LEGACY_FORCE_AB_TEST_KEY = "LvlUp.Debug.ForceAbTestKey";
        private const string LEGACY_FORCE_AB_VARIANT_KEY = "LvlUp.Debug.ForceAbVariantKey";

        #region Platform Override

        /// <summary>
        /// Override the platform reported by the SDK
        /// Valid values: null, "ios", "android", "webgl", "windows", "macos", "linux"
        /// Persists in Editor and builds via PlayerPrefs
        /// </summary>
        public static string PlatformOverride
        {
            get
            {
                return PlayerPrefs.GetString(PLATFORM_OVERRIDE_KEY, null);
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(PLATFORM_OVERRIDE_KEY, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(PLATFORM_OVERRIDE_KEY);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Check if platform override is active
        /// </summary>
        public static bool HasPlatformOverride => !string.IsNullOrEmpty(PlatformOverride);

        /// <summary>
        /// Clear platform override
        /// </summary>
        public static void ClearPlatformOverride()
        {
            PlayerPrefs.DeleteKey(PLATFORM_OVERRIDE_KEY);
            PlayerPrefs.Save();
        }

        #endregion

        #region Environment Override

        /// <summary>
        /// Override the remote config environment
        /// Valid values: null, "production", "staging", "development"
        /// Persists in Editor and builds via PlayerPrefs
        /// </summary>
        public static string EnvironmentOverride
        {
            get
            {
                return PlayerPrefs.GetString(ENVIRONMENT_OVERRIDE_KEY, null);
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    PlayerPrefs.SetString(ENVIRONMENT_OVERRIDE_KEY, value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(ENVIRONMENT_OVERRIDE_KEY);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Check if environment override is active
        /// </summary>
        public static bool HasEnvironmentOverride => !string.IsNullOrEmpty(EnvironmentOverride);

        /// <summary>
        /// Clear environment override
        /// </summary>
        public static void ClearEnvironmentOverride()
        {
            PlayerPrefs.DeleteKey(ENVIRONMENT_OVERRIDE_KEY);
            PlayerPrefs.Save();
        }

        #endregion

        #region AB Test Override

        /// <summary>
        /// Forced A/B cohorts, at most one per layer (a real user is also assigned at most one
        /// test per layer). Persists in Editor and builds via PlayerPrefs. Forced cohorts are
        /// excluded from analytics.
        /// </summary>
        public static IReadOnlyList<ForcedAbOverride> ForcedAbOverrides => LoadForcedAbOverrides();

        public static bool HasForcedAbOverride => LoadForcedAbOverrides().Count > 0;

        /// <summary>
        /// Get the forced cohort for a layer, or null if that layer is not forced.
        /// </summary>
        public static ForcedAbOverride GetForcedAbOverride(string layerKey)
        {
            return LoadForcedAbOverrides().Find(o => o.layerKey == layerKey);
        }

        /// <summary>
        /// Force a variant. Replaces any existing forced variant in the same layer.
        /// </summary>
        public static void SetForcedAbOverride(string layerKey, string testKey, string variantKey)
        {
            if (string.IsNullOrEmpty(layerKey) || string.IsNullOrEmpty(testKey) || string.IsNullOrEmpty(variantKey))
            {
                Debug.LogWarning("[LvlUp] SetForcedAbOverride requires layerKey, testKey and variantKey");
                return;
            }

            var overrides = LoadForcedAbOverrides();
            overrides.RemoveAll(o => o.layerKey == layerKey);
            overrides.Add(new ForcedAbOverride { layerKey = layerKey, testKey = testKey, variantKey = variantKey });
            SaveForcedAbOverrides(overrides);
        }

        /// <summary>
        /// Clear the forced variant for one layer.
        /// </summary>
        public static void ClearForcedAbOverride(string layerKey)
        {
            var overrides = LoadForcedAbOverrides();
            if (overrides.RemoveAll(o => o.layerKey == layerKey) > 0)
            {
                SaveForcedAbOverrides(overrides);
            }
        }

        /// <summary>
        /// Clear all forced variants.
        /// </summary>
        public static void ClearForcedAbOverride()
        {
            PlayerPrefs.DeleteKey(FORCED_AB_OVERRIDES_KEY);
            DeleteLegacyAbKeys();
            PlayerPrefs.Save();
        }

        private static List<ForcedAbOverride> LoadForcedAbOverrides()
        {
            MigrateLegacyAbOverride();

            var json = PlayerPrefs.GetString(FORCED_AB_OVERRIDES_KEY, "");
            if (string.IsNullOrEmpty(json)) return new List<ForcedAbOverride>();

            try
            {
                var overrides = JsonConvert.DeserializeObject<List<ForcedAbOverride>>(json) ?? new List<ForcedAbOverride>();
                overrides.RemoveAll(o => o == null || string.IsNullOrEmpty(o.layerKey) ||
                    string.IsNullOrEmpty(o.testKey) || string.IsNullOrEmpty(o.variantKey));
                return overrides;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LvlUp] Ignoring unreadable forced A/B overrides: {e.Message}");
                return new List<ForcedAbOverride>();
            }
        }

        private static void SaveForcedAbOverrides(List<ForcedAbOverride> overrides)
        {
            if (overrides.Count == 0)
            {
                PlayerPrefs.DeleteKey(FORCED_AB_OVERRIDES_KEY);
            }
            else
            {
                PlayerPrefs.SetString(FORCED_AB_OVERRIDES_KEY, JsonConvert.SerializeObject(overrides));
            }
            PlayerPrefs.Save();
        }

        private static void MigrateLegacyAbOverride()
        {
            var testKey = PlayerPrefs.GetString(LEGACY_FORCE_AB_TEST_KEY, "");
            var variantKey = PlayerPrefs.GetString(LEGACY_FORCE_AB_VARIANT_KEY, "");
            if (string.IsNullOrEmpty(testKey) && string.IsNullOrEmpty(variantKey)) return;

            var layerKey = PlayerPrefs.GetString(LEGACY_FORCE_AB_LAYER_KEY, "");
            DeleteLegacyAbKeys();

            // Layer-less legacy overrides (typed by hand) are dropped: without a layer they could
            // collide with a new pick in the same layer.
            if (!string.IsNullOrEmpty(layerKey) && !string.IsNullOrEmpty(testKey) && !string.IsNullOrEmpty(variantKey) &&
                !PlayerPrefs.HasKey(FORCED_AB_OVERRIDES_KEY))
            {
                var migrated = new List<ForcedAbOverride>
                {
                    new ForcedAbOverride { layerKey = layerKey, testKey = testKey, variantKey = variantKey }
                };
                PlayerPrefs.SetString(FORCED_AB_OVERRIDES_KEY, JsonConvert.SerializeObject(migrated));
            }
            PlayerPrefs.Save();
        }

        private static void DeleteLegacyAbKeys()
        {
            PlayerPrefs.DeleteKey(LEGACY_FORCE_AB_LAYER_KEY);
            PlayerPrefs.DeleteKey(LEGACY_FORCE_AB_TEST_KEY);
            PlayerPrefs.DeleteKey(LEGACY_FORCE_AB_VARIANT_KEY);
        }

        #endregion

        /// <summary>
        /// Clear all debug overrides
        /// </summary>
        public static void ClearAll()
        {
            ClearPlatformOverride();
            ClearEnvironmentOverride();
            ClearForcedAbOverride();
        }
    }

    /// <summary>
    /// A forced A/B cohort: one variant of one test, pinned for a layer.
    /// </summary>
    [Serializable]
    public class ForcedAbOverride
    {
        public string layerKey;
        public string testKey;
        public string variantKey;

        public override string ToString()
        {
            return $"{layerKey} / {testKey} / {variantKey}";
        }
    }
}
