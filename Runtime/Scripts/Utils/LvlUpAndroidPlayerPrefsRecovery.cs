using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LvlUp.Utils
{
    /// <summary>
    /// Android-only guard to recover from oversized PlayerPrefs files before SDK initialization.
    /// </summary>
    internal static class LvlUpAndroidPlayerPrefsRecovery
    {
        private const long PLAYER_PREFS_SIZE_THRESHOLD_BYTES = 1536 * 1024; // 1.5 MB

        private static readonly HashSet<string> ExactKeysToRemove = new HashSet<string>
        {
            "LvlUp_OfflineEventCount",
            "LvlUp_OfflineRevenueCount",
            "LvlUp_PendingSessionStartCount",
            "LvlUp_PendingSessionEndCount"
        };

        private static readonly string[] KeyPrefixesToRemove =
        {
            "LvlUp_OfflineEvents_",
            "LvlUp_OfflineRevenue_",
            "LvlUp_PendingSessionStarts_",
            "LvlUp_PendingSessionEnds_"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RecoverOversizedLvlUpPlayerPrefs()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (!IsPlayerPrefsOversized())
                    return;

                int removedCount = RemoveLvlUpKeysFromAndroidSharedPrefs();
                if (removedCount > 0)
                {
                    Debug.LogWarning($"[LvlUp] Oversized PlayerPrefs detected. Cleared {removedCount} LvlUp offline key(s) for startup recovery.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] PlayerPrefs recovery check failed: {ex.Message}");
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool IsPlayerPrefsOversized()
        {
            foreach (var candidate in GetPlayerPrefsXmlCandidates())
            {
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    var fileInfo = new FileInfo(candidate);
                    if (fileInfo.Length >= PLAYER_PREFS_SIZE_THRESHOLD_BYTES)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore candidate read failures and continue with the next path.
                }
            }

            return false;
        }

        private static IEnumerable<string> GetPlayerPrefsXmlCandidates()
        {
            string packageName = Application.identifier;
            if (string.IsNullOrEmpty(packageName))
                yield break;

            string prefsDir = Path.Combine("/data/data", packageName, "shared_prefs");
            yield return Path.Combine(prefsDir, $"{packageName}.v2.playerprefs.xml");
            yield return Path.Combine(prefsDir, $"{packageName}.playerprefs.xml");
        }

        private static int RemoveLvlUpKeysFromAndroidSharedPrefs()
        {
            int removedCount = 0;
            removedCount += RemoveLvlUpKeysFromSharedPrefs($"{Application.identifier}.v2.playerprefs");
            removedCount += RemoveLvlUpKeysFromSharedPrefs($"{Application.identifier}.playerprefs");
            return removedCount;
        }

        private static int RemoveLvlUpKeysFromSharedPrefs(string prefsName)
        {
            int removedCount = 0;
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var sharedPrefs = currentActivity.Call<AndroidJavaObject>("getSharedPreferences", prefsName, 0))
            using (var allMap = sharedPrefs.Call<AndroidJavaObject>("getAll"))
            using (var keySet = allMap.Call<AndroidJavaObject>("keySet"))
            using (var iterator = keySet.Call<AndroidJavaObject>("iterator"))
            using (var editor = sharedPrefs.Call<AndroidJavaObject>("edit"))
            {
                while (iterator.Call<bool>("hasNext"))
                {
                    string key = iterator.Call<string>("next");
                    if (!ShouldRemoveKey(key))
                        continue;

                    editor.Call<AndroidJavaObject>("remove", key);
                    removedCount++;
                }

                editor.Call<bool>("commit");
            }

            return removedCount;
        }

        private static bool ShouldRemoveKey(string key)
        {
            if (ExactKeysToRemove.Contains(key))
                return true;

            foreach (string prefix in KeyPrefixesToRemove)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
#endif
    }
}
