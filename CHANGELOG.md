# Changelog

All notable changes to the LvlUp Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.15.0] - 2026-10-27

### Added
- **`LvlUpSDK.LiveOps`** tracks live events, passes and purchase offers through the ordinary event queue and `LvlUpEventMonitor.OnTracked`. Typed actions cover availability, impressions, joins, progress, setbacks, finishes, claims, purchases, dismissals, revivals and expiration, with current `levelOrder` attached automatically.
- `LiveOpsRun` pins run identity and variant, with season and offer helpers. Validation normalizes ids and rejects invalid numbers without throwing to the game. Eligible, join, finish and expire are deduplicated across restarts with a PlayerPrefs guard bounded to the most recent 500 entries.
- EditMode coverage for LiveOps payloads, validation, run helpers and persisted deduplication.

## [1.14.0] - 2026-10-26

### Added
- **`LvlUpEventMonitor.OnTracked`**, a debug hook for in-app event viewers. It fires with a `LvlUpTrackedEvent` (kind, name, JSON payload, UTC time) whenever the SDK accepts an event, a revenue item or a session start/end, at track time rather than at batch flush. SDK-tracked events (`app_paused`/`app_resumed`/`app_quit`, economy flushes, ad impressions) are included. Payloads are serialized only while something is subscribed, so there is no cost otherwise. Heartbeats, journey checkpoints, attribution and the session end queued on quit are not reported.

## [1.13.0] - 2026-10-26

### Added
- **`TrackLevelComplete(levelId, score, timeSeconds, winMargin, additionalProperties)`** sends `winMargin`: how much of the level's starting resource was left at the win (remaining ÷ starting, in [0, 1], e.g. balls left ÷ balls at start). It is the win-side counterpart of `objectiveProgress` on `TrackLevelFailed`. The level funnel shows it as **Avg Win Margin** and **Near-miss Wins %** (wins with a margin of 0.1 or less), next to Avg Fail Progress and the new **Near-miss Fails %** (fails with `objectiveProgress` of 0.9 or more).
- Values outside [0, 1] or NaN are dropped with a warning instead of being sent.

### Fixed
- README and API reference no longer document a `TrackLevelComplete(levelId, score, timeSeconds, stars)` overload, which never existed. With the new `float winMargin` overload, copying that example would have sent the star count as the win margin. Pass stars in `additionalProperties`. The API reference now also lists the `objectiveProgress` overload of `TrackLevelFailed`.

## [1.12.3] - 2026-10-24

### Fixed
- **One platform spelling everywhere.** `GetPlatform()` (used for the remote-config context and revenue tracking) returned `Android`/`iOS`, while event metadata sent `android`/`ios`. The backend stored both, so a platform filter matched only part of a game's users (Hidden Valley: 288 vs 147 of 437). The context now sends the same lowercase values as event metadata. The backend now also lowercases on ingest, so games on older SDK versions are covered server-side.

## [1.12.2] - 2026-10-22

### Fixed
- **Float remote config values are correct on every device locale.** `RemoteConfig.GetFloat` (and `LvlUpSDK.Config.TryGetFloat`) parsed with the device culture, so on locales that use `.` as the thousands separator (tr-TR, de-DE, es-ES, it-IT, id-ID, ...) `"0.05"` came back as `5`, with no error or log. Values are now parsed with `CultureInfo.InvariantCulture`.
- `GetInt`, the `0`/`1` fallback in `GetBool`, MAX ad revenue values passed as strings, and `SimpleJson` integer parsing and primitive formatting are culture-invariant too.

### Added
- EditMode coverage that runs `GetFloat`/`GetInt`/`GetBool` under tr-TR, de-DE and en-US.

## [1.12.1] - 2026-10-21

### Fixed
- **Player builds fetch production remote config.** Builds were meant to force `production`, but the environment from `LvlUpConfig.asset` reached `RemoteConfigService` before that guard ran, so a build shipped with `remoteConfigEnvironment = development` fetched development config and the backend assigned no A/B tests. The environment is now resolved once, before the service starts: always `production` in player builds, the asset value in the Editor. `LvlUpDebugSettings.EnvironmentOverride` still applies per fetch and is the supported way to use another environment on a device.
- The "RemoteConfig service ready" and "Remote Config initialized" logs print the environment the service actually fetches, including any override.
- The config cache is keyed by the environment actually fetched. An overridden (e.g. `development`) response is no longer saved as the `production` cache, and a build that cached `development` config before this fix never reads it, since it now loads only the `production` cache.
- Configs loaded from the cache are no longer saved back to it. Re-saving reset the cache file's age, so an offline device could keep using a stale cache past the 5-minute TTL, and the load was reported as `from cache: false`.

### Changed
- `LvlUpManager.GetEffectiveRemoteConfigEnvironment()` (and `LvlUpSDK.RemoteConfig.EffectiveEnvironment`) includes `EnvironmentOverride` once the service is initialized.

### Added
- EditMode coverage for remote config environment resolution and cache isolation.

## [1.12.0] - 2026-10-21

### Fixed
- **A/B tests now assign players.** The remote config fetch sends the player's ID as the `userId` query param. The backend assigns A/B variants only when it receives a user ID, so with earlier SDK versions no player was ever assigned and everyone got the default config. Games using auto sessions send the persisted auto user ID on the first fetch, which runs before the session starts. Games that call `StartSession(userId)` themselves get a refetch with that ID if configs were already loaded without it.

### Added
- `RemoteConfigService.SetUserId(userId)` and `ContextUserId`.

## [1.11.0] - 2026-10-21

### Added
- **Force one A/B variant per layer.** The debug A/B override now holds one forced variant per layer, so cohorts in different layers (e.g. `early_ads` and `town_visuals`) can be forced together. `LvlUpDebugSettings.ForcedAbOverrides`, `GetForcedAbOverride(layerKey)` and `ClearForcedAbOverride(layerKey)` were added; `SetForcedAbOverride` now replaces only the variant in that layer. Two or more overrides are sent as the new `forceAbs` query param and need a backend that supports it; a single override still uses the original `forceAbLayerKey`/`forceAbTestKey`/`forceAbVariantKey` params. A saved pre-1.11 override is migrated automatically.
- The Debug Window fetches the A/B test list outside Play mode (using the API key and environment from `LvlUpConfig`), so you can pick cohorts before pressing Play. The list loads when the window opens.

### Changed
- Debug Window A/B section: ticking a forced variant again un-forces it, and in Play mode any change refetches remote config immediately. The Manual Override fields (Layer/Test/Variant key) were removed.
- SRDebugger panel: the A/B key text fields were replaced by a "Fetch A/B Tests" button and one button per variant (category "LvlUp A/B").

### Removed
- `LvlUpDebugSettings.ForceAbLayerKey`, `ForceAbTestKey` and `ForceAbVariantKey`. Use `ForcedAbOverrides` / `GetForcedAbOverride(layerKey)`.
## [1.10.0] - 2026-10-20

### Added
- In-app purchase placement (`iapPlacement`): where in the game a purchase started, for per-source IAP reporting on the dashboard.
- `placement` argument on `LvlUpSDK.Revenue.TrackInAppPurchase`.
- `LvlUpSDK.Revenue.SetPurchasePlacement(placement, productId = null)` and `ClearPurchasePlacement(productId = null)` for `TrackPurchase` auto-capture. The next matching purchase consumes the pending placement; it expires after 10 minutes. A product-specific placement wins over an any-product one, and an explicit argument wins over both. Names are trimmed and limited to 64 characters while preserving casing.
- EditMode coverage for purchase placement resolution.

## [1.9.0] - 2026-10-18

### Added
- Adjust install attribution sent to `analytics/attribution` on initialization and each session, enabled with `lvlup_adjust_enabled` (or the `com.adjust.sdk` UPM package).
- Main-thread callback handling and bounded retries after 10, 30, 60, 120 and 300 seconds when Adjust attribution is not available yet.
- Normalized payload comparison against the last successful locally persisted value; failed sends remain pending for the next session.
- `LvlUpSDK.Attribution.Set(...)` for manual attribution and `LvlUpSDK.Attribution.Refresh()` for an immediate Adjust read, plus EditMode coverage.

## [1.8.0] - 2026-10-15

### Added
- Wallet tracking through `LvlUpSDK.Economy`: resource registration, balance providers, sources, sinks, trades with shared transaction ids, and full-wallet snapshots on the normal event queue.
- Automatic level order and in-level context on flows; provider-backed snapshots on session and level starts, including registered zero balances.
- Optional source coalescing via `LvlUpConfig.economyCoalesceSeconds` (default off), flushed on expiry, resource spends/trades, snapshots, SDK flush, pause and quit.
- Validation and sanitization with logged, contained failures, plus EditMode economy tests.

### Fixed
- Event queue persistence now preserves dictionary properties with the existing Newtonsoft serializer, including nested wallet snapshots on pause/quit. Existing persisted JSON remains readable.

## [1.7.0] - 2026-10-14

### Added
- `LvlUpSDK.Ads.SetPlacement` and `ClearPlacement`, with public `LvlUpAdType`, for game-defined ad usage names across MAX and manual impression tracking.
- Thread-safe placement state: one-shot interstitial/rewarded/app-open names expire after 120 seconds; banner/MRec names persist until changed or cleared. Custom names are trimmed and limited to 64 characters while preserving casing.
- Placement precedence: explicit manual placement, then custom placement, then MAX placement. Known format aliases are matched case-insensitively; each impression consumes pending state once.
- MAX interstitial and rewarded display-failure callbacks clear pending placements.

### Fixed
- MAX ad impressions now report the serving network (`adInfo.NetworkName`, e.g. "AppLovin", "Google AdMob") as `adNetworkName` instead of the hard-coded "MAX", so network breakdowns work. Falls back to "MAX" when MAX gives no network name.

## [1.6.0] - 2026-08-12

### Added
- Server-side developer-device detection. `LvlUpSDK.SetDeviceAdvertisingId(string)` forwards this device's advertising ID (IDFA/GAID) to the backend as the `X-Device-Ad-Id` request header on the remote config fetch — never in the URL. The backend checks it against a private dev-device list and injects a grant only into that device's response; the list is never sent to clients. `LvlUpSDK.IsDeveloperDevice` reflects whether the backend recognized this device as a developer/test device, so builds can gate dev-only features — e.g. the in-app debugger via `SRDebug.Init()` — on it instead of matching a broadcast `dev_devices` config locally. Provide the ad ID before `Initialize` so the first fetch includes it; setting it later re-fetches configs.
- `LvlUpHttpClient.Get<T>` now accepts an optional `extraHeaders` parameter (backward compatible) so per-request headers can be attached.

## [1.5.1] - 2026-06-10

### Added
- `LvlUpSDK.Config.WaitUntilReadyAsync(float timeoutSeconds = 15f)` — awaitable variant of `WaitUntilReady`. Returns a standard `Task<bool>` (true once configs load, false on timeout) so it can be awaited directly or adapted (e.g. `.AsUniTask()`), without adding any UniTask dependency to the SDK. The callback-based `WaitUntilReady` remains as the underlying primitive.

## [1.5.0] - 2026-06-10

### Added
- `LvlUpSDK.Config.WaitUntilReady(Action<bool> onReady, float timeoutSeconds = 15f)` — waits until remote config has actually loaded (`IsReady`) and invokes the callback with `true`, or `false` if the timeout elapses first (e.g. offline with no valid cache). Bounded so callers never block indefinitely; on timeout, reads fall back to defaults. Coroutine-based and uses unscaled real time, so it is unaffected by `Time.timeScale`.

### Changed
- `LvlUpSDK.Config.IsReady` now reflects whether configs have **actually loaded** (from server or cache) rather than merely whether the service object was set up. Previously it was an alias for `RemoteConfigService.IsInitialized`, which becomes true immediately on construction — before any fetch — so `IsReady` could report ready while no config values were available. Backed by a new `RemoteConfigService.HasLoadedConfigs` flag set in `ProcessConfigs` and reset by `ClearCache`. The `TryGet*` accessors (which already gate on `IsReady`) now correctly withhold values until configs load.

## [1.4.1] - 2026-06-10

### Fixed
- **Initialization callback was never invoked.** `_Initialize` started the init coroutine without forwarding the `onComplete` callback (`StartCoroutine(InitializeAsync())`), so the success/failure callback passed to `LvlUpSDK.Initialize` never fired in the normal path. The callback is now forwarded, so completion-gated patterns (e.g. `MMLvlUpManager.WaitUntilInitialized`) work as intended.
- **Remote config not ready when initialization completed.** The remote config fetch was started fire-and-forget during init, so `onComplete` (and any wait built on it) could resolve before configs were loaded — callers reading config values immediately got defaults. Init now awaits the config fetch (bounded by its existing internal timeout) before signalling completion. As a side effect, the auto-started session's A/B attribution is now populated from the config fetch rather than starting empty.

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
