# BirdieBuddy iOS client

The native client targets SwiftUI and consumes the additive mobile API described in [`../docs/mobile-api.md`](../docs/mobile-api.md). The current slice covers account creation, authentication, password change, email verification, password reset, course browsing, custom course create/edit/delete, draft creation, live hole entry, completion, local outbox sync/conflict review, round history/detail, round date/tee editing and deletion, overview statistics, practice sessions, profile editing, data export, account deletion, on-device crash diagnostics, and sign out. Production release work remains.

## Deep links

The app registers the `birdiebuddy` URL scheme and handles two links:

- `birdiebuddy://verify-email?token=…`
- `birdiebuddy://reset-password?token=…`

Account emails currently point at the web pages (`verify-email.html`,
`reset-password.html`) built from `Application:PublicBaseUrl`. Routing those
emails to the app instead needs Universal Links, which require an
`apple-app-site-association` file served from the API domain.

## Configuration layout

- `ios/Signing.xcconfig` holds `DEVELOPMENT_TEAM` for both app configurations. It is the only place to set the Apple Developer Team ID.
- `BirdieBuddyApp/Info-Debug.plist` is the Debug property list and carries the `NSAllowsLocalNetworking` exception for `http://localhost:5000`. `BirdieBuddyApp/Info.plist` is the Release one and must stay free of App Transport Security exceptions. Add new keys to both.
- The app target is iPhone-only. Adding iPad means committing to an iPad layout and iPad App Store screenshots.
- `BirdieBuddyApp/Localizable.xcstrings` carries English source strings and Korean translations. SwiftUI string literals are localization keys, so adding UI text means adding a catalog entry. Error text produced at runtime as a `String` is not localized yet.

## Visual design

`BirdieBuddyApp/BirdieTheme.swift` mirrors the palette, card treatment, and type roles in `wwwroot/css/styles.css`. The app bundles the same Outfit, Space Grotesk, and DM Mono font files in `BirdieBuddyApp/Fonts/`, along with their licenses. The shared `BirdieBrand` view uses an outlined vector logo from `Assets.xcassets/BirdieLogo.imageset/`; its masters and export script are in [`../design/brand/`](../design/brand/README.md). Login, restored-session loading, and branded navigation share this asset.

The signed-in screens keep native tab navigation and accessibility controls while using the web app's page headings, light canvas, paper cards, dark green actions, and score accents. Add new shared colors to both token sets instead of introducing screen-specific near matches.

The [bird-on-tee icon artwork](../design/app-icon/README.md) is stored as four separate SVG layers for Apple Icon Composer. The app currently bundles a flat export in `BirdieBuddyApp/Assets.xcassets/AppIcon.appiconset/AppIcon-1024.png`. Layered Liquid Glass rendering requires importing the SVG layers into Icon Composer and adding its export to the Xcode project.

## Xcode setup

Open `BirdieBuddyApp.xcodeproj` in Xcode 16 or later. It contains the iOS 17 app target, a shared scheme, and the `BirdieBuddyAppTests` unit-test target. Both Debug and Release builds point `API_BASE_URL` at the deployed HTTPS origin, so an app launched from the device's home screen reaches the same server as one launched from Xcode. A scheme environment variable of the same name overrides it for a local or staging server — but note it only applies to processes Xcode launches, so it is the wrong tool for on-device testing that involves force-quitting and relaunching the app. For local API work, point `API_BASE_URL` at `http://localhost:5000`; Debug accepts `http` only for localhost, and Release refuses `http` and localhost outright.

Run the tests from Xcode or with:

```bash
./scripts/test-ios.sh
```

The script selects an available simulator from the newest installed iOS runtime instead of depending on one hard-coded iPhone model. Set `BIRDIEBUDDY_IOS_SIMULATOR_ID` to a simulator UDID to override the selection.

The app restores its Keychain session at launch. An expired access token is refreshed once, the original request is retried once, and a second authentication failure signs the user out without retrying indefinitely. Network and transient server failures preserve the cached user so offline draft entry remains available. Pending hole writes and conflicts live in SwiftData and synchronize from app lifecycle events, so reopening the live-round screen is not required after connectivity returns.

## Data rules

- Access and refresh tokens are stored in Keychain, never UserDefaults.
- Outbox entries and conflicts are user- and round-scoped in SwiftData; active draft snapshots are cached per user and round in Application Support.
- A local write is acknowledged only after durable local storage succeeds.
- A `409` is a review state, not a retryable network failure.
- Logout preserves the signed-out user's local writes without exposing them to another account; successful account deletion removes that user's local records.
- Retryable network/408/429/5xx failures remain queued, while other 4xx responses become an explicit action-required state.
- The client uses UTC for server timestamps and the device calendar only for presentation.

The native regression suite includes migration and account-isolation coverage plus a mocked end-to-end journey covering course selection, 18 durable hole writes, process-style store recreation, ordered synchronization, acknowledgement, and round completion.
