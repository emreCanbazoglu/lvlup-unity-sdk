# Changelog

All notable changes to the LvlUp Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2026-06-10

### Added
- `LvlUpConfig.logRemoteConfigResult` (and matching Inspector toggle on `LvlUpConfigScriptable`) — when enabled, logs the full Remote Config result (every key/value and A/B assignment, with environment and cache-source) to the console each time configs load. Independent of `enableDebugLogs` so it can be toggled on its own.

### Changed
- SR Debugger integration no longer forces the debug service to be created. Previously accessing `SRDebug.Instance` auto-created the SR Debugger service (via `SRServiceManager.GetService`) even on builds where the host app never enabled it. Registration now waits via the non-creating `SRServiceManager.HasService<IDebugService>()` check and only registers once the host app has created the service, giving up after a 30s timeout.

## [1.3.0] - 2026-06-04

### Added
- A/B test debug override support for Remote Config, including forced layer/test/variant query parameters and a debug catalog fetch endpoint.
- LvlUp Debug Window controls for selecting or manually entering forced A/B cohorts and displaying current A/B assignments.
- SR Debugger controls for changing A/B override keys on device when `lvlup_srdebugger_enabled` is defined.
- `abTests` payload metadata for analytics events and revenue payloads, excluding forced debug overrides from production analytics attribution.

### Fixed
- Forced A/B debug responses are no longer cached as normal Remote Config results.
- Resolved ambiguous debug settings references between the public SDK namespace and the internal utils namespace.

## [1.2.1] - 2026-04-15

### Fixed
- **Unity IAP auto-capture now compiles on both IAP 4.x and 5.x.** Previously the `LvlUp.IAP` asmdef referenced the Purchasing assembly by name (`"Unity.Purchasing"`), which only exists in IAP 5.x — on 4.x the assembly name is `"UnityEngine.Purchasing"` and the reference failed to resolve, breaking the build. Reference is now by asmdef GUID (`60bfecf5cb232594891bc622f40d6bed`), which Unity kept stable across the 4.x → 5.x package rename. The `Product` / `ProductDefinition` / `ProductMetadata` API we rely on is identical between versions, so no code changes were needed alongside the asmdef fix.

### Notes
- Public API unchanged — `LvlUpSDK.Revenue.TrackPurchase(product)` works as before.
- No minimum IAP version constraint is currently set; the integration compiles against any version of `com.unity.purchasing` that ships the core `Product` type. If a future IAP major renames or removes one of those members, we'll add a `versionDefines` clause to gate specifically.

## [1.2.0] - 2026-04-15

### Added
- **Current Level Order Auto-Capture** (for IAP revenue attribution): Enables ARPU-by-level analytics on the dashboard.
  - `LvlUpSDK.Level.SetCurrent(int levelOrder)` — explicitly set the player's current level order. Use this if your game has non-sequential level IDs or tracks progression separately from the `levelId` you pass to `TrackLevelStart`.
  - `LvlUpSDK.Level.GetCurrent()` — returns the currently tracked level order (`int?`).
  - `LvlUpEvents.TrackLevelStart(levelId, ...)` now auto-calls `SetCurrent(levelId)`, so games that use sequential level IDs get the field populated for free.
  - New `currentLevelOrder` field on `RevenueData` — auto-injected for `IN_APP_PURCHASE` events in `RevenueTrackingService.PopulateRevenueContext`. Not injected for `AD_IMPRESSION` events (ad cadence is not player-driven — would add noise, not signal). Callers can still override by setting `RevenueData.currentLevelOrder` explicitly before calling `TrackRevenue`.
- **Objective Progress on Level Fail**: New `TrackLevelFailed` overload accepting `objectiveProgress` — a `float` in [0, 1] representing the share of objectives the player completed before failing (e.g., 0.6 = 60% complete). Normalized so the metric is comparable across levels with different objective counts. Sent as a custom property on the `level_failed` event (consistent with `levelId`, `reason`, `attempts`). Optional — games without an objective system simply don't call this overload.

### Fixed
- **Stale `sdkVersion` reported to backend**: `EventMetadata.sdkVersion` was hardcoded to `"unity 1.0.0"` and never updated through 1.1.0 / 1.1.1 releases, so server-side telemetry could not distinguish SDK versions. Version is now sourced from a single constant (`LvlUp.LvlUpVersion.Current`) that must be bumped alongside `package.json` on every release. Emitted value format unchanged (`"unity <version>"`).

### Backend contract
- Backend agents should reference `specs/003-sdk-level-context/spec.md` in the `lvlup-backend` repo for the new payload fields and storage expectations (direct column vs. `properties` JSON).

## [1.1.1] - 2026-04-13

### Fixed
- **Revenue data loss on app quit**: Revenue queue is now persisted to disk in `OnApplicationQuit`, matching the existing `OnApplicationPause` behavior. Previously, queued IAP revenue was silently lost if the app was killed before the flush coroutine completed.
- **IAP tracking without receipt**: `TrackPurchase()` now skips products that have no receipt (`hasReceipt == false`), preventing pending or failed purchases from being counted as revenue.
- **Zero-revenue IAP tracking**: `TrackPurchase()` now skips products with `localizedPrice <= 0`, which can occur when store metadata hasn't loaded yet.
- **Null currency defaulting silently to USD**: `TrackPurchase()` now logs a warning when `isoCurrencyCode` is null before defaulting to USD, making currency misattribution easier to diagnose.
- **Transaction dedup eviction clearing all entries**: The deduplication set now evicts the oldest half instead of clearing entirely, preventing a brief window where duplicate transactions could slip through.

## [1.1.0] - 2026-04-11

### Added
- **Unity IAP Auto-Capture**: Automatically track in-app purchases from Unity's IAP package
  - `LvlUpSDK.Revenue.TrackPurchase(product)` — one-line IAP tracking in `ProcessPurchase()`
  - Auto-extracts productId, price, currency, transactionId, store, and product type from Unity's `Product` object
  - Conditionally compiled via `lvlup_iap_enabled` define (auto-set when `com.unity.purchasing` is installed)
  - `autoTrackIAP` config flag (default: `true`) to enable/disable
  - Client-side transaction deduplication by `transactionId` — safe to use alongside manual `TrackInAppPurchase()` calls
- **Level Funnel Tracking**: A/B test and track different level design iterations
  - `levelFunnel` field in `LvlUpConfig` for setting global funnel name (e.g., "live_v1", "test_hard")
  - `levelFunnelVersion` field in `LvlUpConfig` for tracking design version numbers
  - `SetLevelFunnel(string, int)` method for setting funnel configuration after initialization (recommended for A/B tests)
  - `GetLevelFunnel()` method for retrieving current funnel configuration
  - Automatic addition of funnel data to all level events (`level_start`, `level_complete`, `level_failed`)
  - `levelFunnel` and `levelFunnelVersion` fields in `EventMetadata` base class
  - Support for filtering and comparing funnel performance in analytics dashboard

### Changed
- Enhanced level event tracking to automatically include funnel information when configured
- Updated `LvlUpManager.TrackEvent` to detect level events and add funnel data automatically
- Updated API documentation with both static and dynamic configuration approaches

### Documentation
- Added level funnel configuration section to API_REFERENCE.md with complete A/B test example
- Added dynamic configuration approach to QUICKSTART.md (recommended for Remote Config/A/B tests)
- Documented use cases for A/B testing level designs
- Added example showing how to fetch funnel assignment from backend before setting configuration

## [1.0.0] - 2026-01-05

### Added
- Initial release of LvlUp Unity SDK
- Core analytics tracking functionality
  - Session management with automatic lifecycle tracking
  - Event tracking (single and batch)
  - User metadata collection
- Player Journey features
  - Checkpoint creation and management
  - Checkpoint progress tracking
  - Journey analytics
- AI Integration
  - AI chat assistant for in-game help
  - AI-powered analytics insights
  - Context-aware recommendations
- Offline support
  - Automatic event queuing when offline
  - Configurable batch sizes and flush intervals
  - Persistent queue option
- Configuration system
  - Flexible SDK configuration
  - Debug logging options
  - Customizable batch and flush settings
- Unity lifecycle integration
  - Automatic session management
  - App pause/resume tracking
  - Scene change tracking (optional)
- Error handling and retry logic
- Comprehensive documentation
  - Quick start guide
  - Integration guide with examples
  - API reference
  - Best practices
- Example scripts
  - Basic integration example
  - Player journey example
  - AI integration example
- Unity Package Manager support

### Features
- ✅ Session tracking with device info
- ✅ Custom event tracking with properties
- ✅ Batch event sending for efficiency
- ✅ Player checkpoint system
- ✅ AI chat integration
- ✅ AI insights and recommendations
- ✅ Offline event queuing
- ✅ Automatic session lifecycle
- ✅ Configurable SDK behavior
- ✅ Debug logging
- ✅ Unity 2019.4+ support
- ✅ Cross-platform support (iOS, Android, PC, WebGL)

### Supported Platforms
- iOS
- Android
- Windows
- macOS
- Linux
- WebGL

### Requirements
- Unity 2019.4 or later
- .NET Standard 2.0 or higher

## [Unreleased]

### Planned Features
- Remote configuration support
- A/B testing integration
- Advanced analytics dashboards
- Push notification integration
- User segmentation
- Cohort analysis helpers
- Custom event validation
- Event schema definitions
- Local analytics for offline games
- Analytics data export
- GDPR compliance helpers
- User opt-out functionality

---

## Version History

### Version Numbering
- **Major** (X.0.0): Breaking changes, major new features
- **Minor** (0.X.0): New features, non-breaking changes
- **Patch** (0.0.X): Bug fixes, minor improvements

### Migration Guides
When upgrading between major versions, refer to the migration guide in the documentation.

---

## Contributing
See [CONTRIBUTING.md](CONTRIBUTING.md) for information on how to contribute to this project.

## Support
For questions, issues, or feature requests:
- GitHub Issues: https://github.com/yourusername/lvlup-unity-sdk/issues
- Email: support@lvlup.com
- Discord: https://discord.gg/lvlup
