using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LvlUp.Utils
{
    /// <summary>
    /// iOS-only guard to recover from oversized PlayerPrefs files before SDK initialization.
    /// </summary>
    internal static class LvlUpIosPlayerPrefsRecovery
    {
        private const long PLAYER_PREFS_SIZE_THRESHOLD_BYTES = 1536 * 1024; // 1.5 MB
        private const int MAX_DECLARED_COUNT_CLEANUP = 200000;
        private const int MAX_FALLBACK_INDEX_SCAN = 20000;

        private static readonly (string countKey, string prefix)[] QueueKeyGroups =
        {
            ("LvlUp_OfflineEventCount", "LvlUp_OfflineEvents_"),
            ("LvlUp_OfflineRevenueCount", "LvlUp_OfflineRevenue_"),
            ("LvlUp_PendingSessionStartCount", "LvlUp_PendingSessionStarts_"),
            ("LvlUp_PendingSessionEndCount", "LvlUp_PendingSessionEnds_")
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RecoverOversizedLvlUpPlayerPrefs()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                if (!IsPlayerPrefsOversized())
                    return;

                int removedCount = RemoveLvlUpKeysFromPlayerPrefs();
                if (removedCount > 0)
                {
                    PlayerPrefs.Save();
                    Debug.LogWarning($"[LvlUp] Oversized iOS PlayerPrefs detected. Cleared {removedCount} LvlUp offline key(s) for startup recovery.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LvlUp] iOS PlayerPrefs recovery check failed: {ex.Message}");
            }
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private static bool IsPlayerPrefsOversized()
        {
            foreach (var candidate in GetPlayerPrefsPlistCandidates())
            {
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    var fileInfo = new FileInfo(candidate);
                    if (fileInfo.Length >= PLAYER_PREFS_SIZE_THRESHOLD_BYTES)
                        return true;
                }
                catch
                {
                    // Ignore candidate read failures and continue.
                }
            }

            return false;
        }

        private static IEnumerable<string> GetPlayerPrefsPlistCandidates()
        {
            string packageName = Application.identifier;
            if (string.IsNullOrEmpty(packageName))
                yield break;

            string plistFileName = $"{packageName}.plist";

            string candidateFromPersistent = Path.GetFullPath(Path.Combine(
                Application.persistentDataPath,
                "..",
                "Library",
                "Preferences",
                plistFileName));
            yield return candidateFromPersistent;

            string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            if (!string.IsNullOrEmpty(personal))
            {
                string candidateFromPersonal = Path.GetFullPath(Path.Combine(
                    personal,
                    "..",
                    "Library",
                    "Preferences",
                    plistFileName));
                yield return candidateFromPersonal;
            }
        }

        private static int RemoveLvlUpKeysFromPlayerPrefs()
        {
            int removedCount = 0;

            foreach (var group in QueueKeyGroups)
            {
                int declaredCount = Mathf.Clamp(PlayerPrefs.GetInt(group.countKey, 0), 0, MAX_DECLARED_COUNT_CLEANUP);
                removedCount += DeleteIndexedKeys(group.prefix, 0, declaredCount);
                removedCount += DeleteIndexedKeys(group.prefix, declaredCount, MAX_FALLBACK_INDEX_SCAN);
                removedCount += DeleteKeyIfExists(group.countKey);
            }

            return removedCount;
        }

        private static int DeleteIndexedKeys(string prefix, int startIndex, int count)
        {
            int removedCount = 0;
            int endIndex = startIndex + count;
            for (int i = startIndex; i < endIndex; i++)
            {
                string key = $"{prefix}{i}";
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                    removedCount++;
                }
            }

            return removedCount;
        }

        private static int DeleteKeyIfExists(string key)
        {
            if (!PlayerPrefs.HasKey(key))
                return 0;

            PlayerPrefs.DeleteKey(key);
            return 1;
        }
#endif
    }
}
