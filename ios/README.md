# BirdieBuddy iOS client

The native client targets SwiftUI and consumes the additive mobile API described in [`../docs/mobile-api.md`](../docs/mobile-api.md). The current slice covers authentication, course browsing, draft creation, live hole entry, completion, local outbox sync/conflict review, round history/detail, overview statistics, practice sessions, profile editing, password reset request, data export, account deletion, and sign out. Production release work remains.

## Xcode setup

Open `BirdieBuddyApp.xcodeproj` in Xcode 16 or later. It contains the iOS 17 app target, a shared scheme, and the `BirdieBuddyAppTests` unit-test target. Debug builds default to `http://localhost:5000`; Release builds use the target's production HTTPS `API_BASE_URL` build setting. Override it with a scheme environment variable for another local or staging server. Release configuration refuses HTTP and localhost endpoints. The source intentionally has no third-party dependency.

Run the tests from Xcode or with:

```bash
./scripts/test-ios.sh
```

The script selects an available simulator from the newest installed iOS runtime instead of depending on one hard-coded iPhone model. Set `BIRDIEBUDDY_IOS_SIMULATOR_ID` to a simulator UDID to override the selection.

The app restores its Keychain session at launch. An expired access token is refreshed once, the original request is retried once, and a second authentication failure signs the user out without retrying indefinitely. Network and transient server failures preserve the cached user so offline draft entry remains available. Pending hole writes and conflicts live in SwiftData and synchronize from app lifecycle events, so reopening the live-round screen is not required after connectivity returns.

## Data rules

- Access and refresh tokens are stored in Keychain, never UserDefaults.
- Drafts, outbox entries, and conflicts are user- and round-scoped in SwiftData.
- A local write is acknowledged only after durable local storage succeeds.
- A `409` is a review state, not a retryable network failure.
- Logout preserves the signed-out user's local writes without exposing them to another account; successful account deletion removes that user's local records.
- Retryable network/408/429/5xx failures remain queued, while other 4xx responses become an explicit action-required state.
- The client uses UTC for server timestamps and the device calendar only for presentation.

The native regression suite includes migration and account-isolation coverage plus a mocked end-to-end journey covering course selection, 18 durable hole writes, process-style store recreation, ordered synchronization, acknowledgement, and round completion.
