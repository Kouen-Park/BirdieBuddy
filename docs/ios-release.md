# iOS release checklist

## Project and signing

- Open `ios/BirdieBuddyApp.xcodeproj` in Xcode 16 or later.
- Put the Apple Developer Team ID in `ios/Signing.xcconfig` (`DEVELOPMENT_TEAM = …`). Both app build configurations read that file, so it is the only place to set it. Simulator builds and CI work with it empty.
- Confirm or replace the bundle identifier `com.birdiebuddy.mobile` before the first App Store Connect record is created.
- Verify the Release target's `API_BASE_URL` build setting points to the intended production HTTPS origin. Release configuration rejects HTTP and localhost endpoints; use a scheme environment override only for deliberate staging builds.
- The app target is iPhone-only (`TARGETED_DEVICE_FAMILY = 1`). Declaring iPad would require an iPad layout pass and iPad screenshots in App Store Connect.
- Debug builds use `BirdieBuddyApp/Info-Debug.plist`, which holds the `NSAllowsLocalNetworking` exception for `http://localhost:5000`. Release builds use `BirdieBuddyApp/Info.plist`, which must stay free of App Transport Security exceptions. Add new keys to both files.
- Increment `MARKETING_VERSION` and `CURRENT_PROJECT_VERSION` for each submitted build.

## Automated gates

- GitHub Actions `ios-tests` succeeds with Xcode 26.5 on an iOS Simulator.
- The iOS test script dynamically selects the newest installed iPhone Simulator; do not pin CI to a model that may disappear from future Xcode images.
- `scripts/test-ios.sh` retries `xcodebuild test` once on a runner-class exit status (69, 70, 74) after rebooting the simulator, because GitHub-hosted macOS runners intermittently fail to bring up the XCTest daemon. A build error or failing test (65) is never retried, and a retried run logs a CI warning so the flake stays visible. Set `BIRDIEBUDDY_IOS_TEST_ATTEMPTS=1` to disable the retry when investigating.
- ASP.NET Core build, .NET tests, browser tests, PostgreSQL integration tests, and browser E2E remain green.
- Release configuration archives with no signing, privacy-manifest, or asset-catalog warnings.

## Physical iPhone gates

- Create a brand-new account from the app's Create account screen and play a round on it.
- Complete an 18-hole round with Wi-Fi disabled and intermittent cellular service.
- Kill and relaunch during a draft; verify the outbox remains user- and round-scoped.
- Let the access token expire during offline entry; verify the draft survives and synchronizes after reauthentication.
- Create a server conflict and verify both “Use server value” and “Keep my value”.
- Switch accounts and confirm another user's draft is never displayed or synchronized.
- Check iPhone SE-sized layout, safe areas, keyboard avoidance, Dynamic Type, and VoiceOver.
- Switch the device language to Korean and confirm every screen reads correctly; untranslated runtime error text is a known gap.
- After any crash, confirm the Account screen's Diagnostics section lists a report on the next launch and that it can be shared.
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

- The production API runs on an always-on instance. A plan that sleeps when idle is disqualifying: the first request of a round would time out on the tee.
- Production SMTP verification/reset delivery succeeds.
- Database backup restore rehearsal succeeds.
- Readiness health check and external monitoring are active.
- Migration SQL is reviewed before deployment.
- Monitor sign-in failures, round completion, sync failures, and conflict rates during TestFlight.
