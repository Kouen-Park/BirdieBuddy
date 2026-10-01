# BirdieBuddy iOS client

The native client is an iPhone-only, portrait SwiftUI app targeting iOS 17+. It consumes the additive [mobile API](../docs/mobile-api.md). Reviewed on 2026-10-01 after [PR #15](https://github.com/Kouen-Park/BirdieBuddy/pull/15); see [current-status.md](../docs/current-status.md) for verified results and release gaps.

The five tabs are Courses, Rounds, Stats, Practice, and Account. Implemented screens cover registration/sign-in, course search and custom create/name-location edit/delete, drafts and live scoring, completion, durable sync/conflict review, paged history, round date/tee editing and deletion, filtered statistics and Swift Charts with data tables, practice creation/history, profile/password changes, export/deletion, and local diagnostics.

Native draft abandonment, completed-hole editing, guided drill steps, and practice completion/result entry are not exposed in the UI yet. Email verification/reset screens and URL handling exist, but their anonymous `/api/auth` writes currently omit the server-required CSRF token; this contract gap needs resolution before release. Production and physical-device verification remain.

## Deep links

The app registers the `birdiebuddy` URL scheme and handles two links:

- `birdiebuddy://verify-email?token=…`
- `birdiebuddy://reset-password?token=…`

Account emails currently point at the web pages (`verify-email.html`,
`reset-password.html`) built from `Application:PublicBaseUrl`. Routing those
emails to the app instead needs Universal Links, which require an
`apple-app-site-association` file served from the API domain.

These custom-scheme handlers are implemented; successful anonymous verification/reset still depends on resolving the CSRF compatibility gap described in [the mobile contract](../docs/mobile-api.md#anonymous-account-flow-gap).

## Configuration layout

- `ios/Signing.xcconfig` holds `DEVELOPMENT_TEAM` for both app configurations. It is the only place to set the Apple Developer Team ID.
- `BirdieBuddyApp/Info-Debug.plist` is the Debug property list and carries the `NSAllowsLocalNetworking` exception for localhost HTTP. `BirdieBuddyApp/Info.plist` is the Release one and must stay free of App Transport Security exceptions. Add new shared keys to both.
- The app target is iPhone-only. Adding iPad means committing to an iPad layout and iPad App Store screenshots.
- `BirdieBuddyApp/Localizable.xcstrings` carries English source strings and Korean translations. SwiftUI string literals are localization keys, so adding UI text means adding a catalog entry. Error text produced at runtime as a `String` is not localized yet.
- The project settings and both property lists currently use version `1.0`, build `1`. `CFBundleShortVersionString` and `CFBundleVersion` are literal values in the property lists; updating only `MARKETING_VERSION`/`CURRENT_PROJECT_VERSION` does not update the bundled versions. Keep them aligned before submission.

## Visual design

`BirdieBuddyApp/BirdieTheme.swift` mirrors the palette, card treatment, and type roles in `wwwroot/css/styles.css`. The app bundles the same Outfit, Space Grotesk, and DM Mono font files in `BirdieBuddyApp/Fonts/`, along with their licenses. The shared `BirdieBrand` view uses an outlined vector logo from `Assets.xcassets/BirdieLogo.imageset/`; its masters and export script are in [`../design/brand/`](../design/brand/README.md). Login, restored-session loading, and branded navigation share this asset.

The signed-in screens keep native tab navigation and accessibility controls while using the web app's page headings, light canvas, paper cards, dark green actions, and score accents. Add new shared colors to both token sets instead of introducing screen-specific near matches.

The app explicitly selects the light color scheme; dark mode is not implemented. Shared `danger` and `warning` colors provide readable status text, while bright `flag`/`sun` accents remain decorative. Counters and round-history scores stack at accessibility text sizes, and resume banners scroll with their pages. [The design audit](../design/ios-audit/README.md) records these changes; a full current-screen and physical-device pass is still needed.

The [bird-on-tee icon artwork](../design/app-icon/README.md) is stored as four separate SVG layers for Apple Icon Composer. The app currently bundles a flat export in `BirdieBuddyApp/Assets.xcassets/AppIcon.appiconset/AppIcon-1024.png`. Layered Liquid Glass rendering requires importing the SVG layers into Icon Composer and adding its export to the Xcode project.

## Xcode setup

Open `BirdieBuddyApp.xcodeproj` in full Xcode with an installed simulator runtime. CI uses Xcode 26.5; the latest local simulator build used Xcode 27.0. The project contains the iOS 17 app target, a shared scheme, and the `BirdieBuddyAppTests` unit-test target. Both Debug and Release point `API_BASE_URL` at `https://birdiebuddy.onrender.com`. A scheme environment variable overrides it only for Xcode-launched processes. For force-quit/relaunch testing, set the target's `API_BASE_URL` build setting so the endpoint is bundled.

For a local simulator, start the backend from the repository root with `dotnet run --launch-profile http` and set the app endpoint to `http://localhost:5205`. To use port 5000 instead, explicitly run `dotnet run --no-launch-profile --urls http://localhost:5000`. Debug accepts HTTP only for localhost; Release requires non-local HTTPS. A physical iPhone must use a reachable HTTPS origin; its localhost is not this Mac, and a plain HTTP LAN address is rejected by the client configuration.

Run the tests from Xcode or with:

```bash
./scripts/test-ios.sh
```

Run that command from the repository root. The script selects an available simulator from the newest installed iOS runtime. Set `BIRDIEBUDDY_IOS_SIMULATOR_ID` to a simulator UDID to override selection. No phone connection is required. If the shell selects Command Line Tools, set `DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer` for the command. Shut down the selected simulator after testing; the script does not do this automatically. See [native test instructions](../docs/testing.md#native-ios-tests).

[PR #15's native CI job](https://github.com/Kouen-Park/BirdieBuddy/actions/runs/36714842015/job/109885117758) passed 27 tests, including four Dynamic Type layout tests. The latest local build succeeded, but local XCTest/app launch stalled; that simulator was shut down. Neither result is a complete native screen/VoiceOver review.

The app restores its Keychain session at launch. An expired access token is refreshed once, the original request is retried once, and a second authentication failure signs the user out without retrying indefinitely. Network and transient server failures preserve the cached user so offline draft entry remains available. Pending hole writes and conflicts live in SwiftData and synchronize from app lifecycle events, so reopening the live-round screen is not required after connectivity returns.

## Data rules

- Access and refresh tokens are stored in Keychain, never UserDefaults.
- Outbox entries and conflicts are user- and round-scoped in SwiftData; active draft snapshots are cached per user and round in Application Support.
- A local write is acknowledged only after durable local storage succeeds.
- A `409` is a review state, not a retryable network failure.
- Logout preserves the signed-out user's local writes without exposing them to another account; successful account deletion removes that user's local records.
- Retryable network/408/429/5xx failures remain queued, while other 4xx responses become an explicit action-required state.
- Server timestamps decode as UTC instants. Round dates use the API's `yyyy-MM-dd` date-only format and must not be converted as UTC timestamps.
- **Save hole**, **Next hole**, and **Finish round** enqueue the current controls durably before sync. Unsubmitted control changes are not guaranteed to survive force-quit; new draft creation and final completion require the API.

The native regression suite includes migration and account-isolation coverage plus a mocked end-to-end journey covering course selection, 18 durable hole writes, process-style store recreation, ordered synchronization, acknowledgement, and round completion.

## Diagnostics

MetricKit crash, hang, CPU, and disk-write payloads are kept in Application Support. The latest ten reports appear in Account → Diagnostics for sharing or deletion. Reports arrive when iOS delivers a payload on a later launch, not necessarily immediately after a crash or on the very next launch. Nothing uploads automatically. Native product events for resume/timing are not implemented; the backend beta timing metrics currently depend on the web client.
