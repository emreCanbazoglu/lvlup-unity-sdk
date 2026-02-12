using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace LvlUp.Editor
{
    /// <summary>
    /// Ensures ATS exception is present for ip-api HTTP endpoint used by GeoLocationService.
    /// </summary>
    public static class LvlUpiOSPostBuild
    {
        private const string InfoPlistFileName = "Info.plist";
        private const string AtsKey = "NSAppTransportSecurity";
        private const string ExceptionDomainsKey = "NSExceptionDomains";
        private const string GeoDomain = "ip-api.com";
        private const string IncludesSubdomainsKey = "NSIncludesSubdomains";
        private const string AllowInsecureHttpLoadsKey = "NSExceptionAllowsInsecureHTTPLoads";

        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string projectPath)
        {
            if (target != BuildTarget.iOS)
                return;

            string plistPath = Path.Combine(projectPath, InfoPlistFileName);
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[LvlUp] iOS post-build skipped: {InfoPlistFileName} not found at {plistPath}");
                return;
            }

            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            PlistElementDict root = plist.root;
            PlistElementDict ats = GetOrCreateDict(root, AtsKey);
            PlistElementDict exceptionDomains = GetOrCreateDict(ats, ExceptionDomainsKey);
            PlistElementDict geoDomain = GetOrCreateDict(exceptionDomains, GeoDomain);

            geoDomain.SetBoolean(IncludesSubdomainsKey, true);
            geoDomain.SetBoolean(AllowInsecureHttpLoadsKey, true);

            File.WriteAllText(plistPath, plist.WriteToString());
            Debug.Log("[LvlUp] Added ATS exception for ip-api.com (GeoLocationService HTTP endpoint).");
        }

        private static PlistElementDict GetOrCreateDict(PlistElementDict root, string key)
        {
            if (!root.values.TryGetValue(key, out PlistElement element))
                return root.CreateDict(key);

            PlistElementDict dict = element.AsDict();
            return dict ?? root.CreateDict(key);
        }
    }
}
