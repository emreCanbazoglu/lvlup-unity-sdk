namespace LvlUp
{
    /// <summary>
    /// SDK version constant. Single source of truth for the value reported to the
    /// backend via EventMetadata.sdkVersion.
    ///
    /// MAINTAINERS: bump Current in lockstep with package.json and CHANGELOG.md on
    /// every release. This is intentionally a manual step — Unity's runtime has no
    /// reliable way to read package.json at runtime (UnityEditor.PackageManager is
    /// editor-only).
    /// </summary>
    public static class LvlUpVersion
    {
        /// <summary>
        /// Current SDK version. Must match the "version" field in package.json.
        /// </summary>
        public const string Current = "1.5.0";
    }
}
