# iOS release checklist

## Project and signing

- Open `ios/BirdieBuddyApp.xcodeproj` in Xcode 16 or later.
- Select an Apple Developer Team for the `BirdieBuddyApp` target.
- Confirm or replace the bundle identifier `com.birdiebuddy.mobile` before the first App Store Connect record is created.
- Verify the Release target's `API_BASE_URL` build setting points to the intended production HTTPS origin. Release configuration rejects HTTP and localhost endpoints; use a scheme environment override only for deliberate staging builds.
- Increment `MARKETING_VERSION` and `CURRENT_PROJECT_VERSION` for each submitted build.

## Automated gates

- GitHub Actions `ios-tests` succeeds with Xcode 26.5 on an iOS Simulator.
- The iOS test script dynamically selects the newest installed iPhone Simulator; do not pin CI to a model that may disappear from future Xcode images.
- ASP.NET Core build, .NET tests, browser tests, PostgreSQL integration tests, and browser E2E remain green.
- Release configuration archives with no signing, privacy-manifest, or asset-catalog warnings.

## Physical iPhone gates

- Complete an 18-hole round with Wi-Fi disabled and intermittent cellular service.
- Kill and relaunch during a draft; verify the outbox remains user- and round-scoped.
- Let the access token expire during offline entry; verify the draft survives and synchronizes after reauthentication.
- Create a server conflict and verify both “Use server value” and “Keep my value”.
- Switch accounts and confirm another user's draft is never displayed or synchronized.
- Check iPhone SE-sized layout, safe areas, keyboard avoidance, Dynamic Type, and VoiceOver.
- Test background/foreground transitions, low-power mode, and Wi-Fi/cellular switching.

Record each physical-device run with the following fields before internal TestFlight:

| Field | Required evidence |
|---|---|
| Build | Commit SHA, marketing version, build number, API environment |
| Device | Model, iOS version, free storage, low-power mode state |
| Round | Course/tee, 9 or 18 holes, start and completion time |
| Connectivity | Offline start, Wi-Fi/cellular transition, reconnect result |
| Lifecycle | Background duration, force-quit hole, restored pending count |
| Accessibility | iPhone SE-sized layout, largest Dynamic Type, VoiceOver labels, keyboard avoidance |
| Recovery | Reauthentication result, conflict choice tested, final sync status |
| Outcome | Pass/fail, screenshots or screen recording, issue link, tester/date |

## App Store Connect

- App name: Birdie Buddy.
- Category: Sports.
- Supply support, privacy-policy, and terms URLs before external TestFlight.
- Complete App Privacy answers from the deployed product behavior: account identifiers, golf-round data, practice data, and product telemetry are associated with the user's account; the app does not track across companies' apps or websites.
- Add reviewer credentials for a disposable verified test account and explain the offline scorecard flow in review notes.
- Upload screenshots from supported iPhone display sizes after the final accessibility pass.

## Operational release gate

- Production SMTP verification/reset delivery succeeds.
- Database backup restore rehearsal succeeds.
- Readiness health check and external monitoring are active.
- Migration SQL is reviewed before deployment.
- Monitor sign-in failures, round completion, sync failures, and conflict rates during TestFlight.
