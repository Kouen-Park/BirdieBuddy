# BirdieBuddy iOS client

The native client targets SwiftUI and consumes the additive mobile API described in [`../docs/mobile-api.md`](../docs/mobile-api.md). The current slice covers authentication, course browsing, draft creation, live hole entry, completion, local outbox sync/conflict review, round history/detail, overview statistics, practice sessions, profile editing, password reset request, data export, account deletion, and sign out. Production release work remains.

## Xcode setup

Open `BirdieBuddyApp.xcodeproj` in Xcode 16 or later. It contains the iOS 17 app target, a shared scheme, and the `BirdieBuddyAppTests` unit-test target. Set `API_BASE_URL` in the scheme environment for local, staging and production environments. The source intentionally has no third-party dependency.

Run the tests from Xcode or with:

```bash
xcodebuild test \
  -project ios/BirdieBuddyApp.xcodeproj \
  -scheme BirdieBuddyApp \
  -destination 'platform=iOS Simulator,OS=latest,name=iPhone 16' \
  CODE_SIGNING_ALLOWED=NO
```

## Data rules

- Access and refresh tokens are stored in Keychain, never UserDefaults.
- Drafts and outbox entries are user- and round-scoped.
- A local write is acknowledged only after durable local storage succeeds.
- A `409` is a review state, not a retryable network failure.
- The client uses UTC for server timestamps and the device calendar only for presentation.
