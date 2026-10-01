# iOS release checklist

Reviewed on 2026-10-01. [PR #15](https://github.com/Kouen-Park/BirdieBuddy/pull/15) passed all four CI jobs, including 27 iOS tests. This is an automated baseline, not a completed TestFlight/device release gate. See [current-status.md](current-status.md).

## Project and signing

- Open `ios/BirdieBuddyApp.xcodeproj` in full Xcode with an installed simulator runtime. CI uses Xcode 26.5; the latest local build used Xcode 27.0. The deployment target remains iOS 17.
- Put the Apple Developer Team ID in `ios/Signing.xcconfig` (`DEVELOPMENT_TEAM = …`). Both app build configurations read that file, so it is the only place to set it. Simulator builds and CI work with it empty.
- Confirm or replace the bundle identifier `com.birdiebuddy.mobile` before the first App Store Connect record is created.
- Verify the Release target's `API_BASE_URL` build setting points to the intended HTTPS origin. Both configurations currently default to `https://birdiebuddy.onrender.com`; separate staging/production build configurations are not implemented. A scheme environment override applies only to Xcode-launched processes and is insufficient for an archived build or force-quit/relaunch testing.
- The app target is iPhone-only (`TARGETED_DEVICE_FAMILY = 1`). Declaring iPad would require an iPad layout pass and iPad screenshots in App Store Connect.
- Debug uses `BirdieBuddyApp/Info-Debug.plist` with the localhost networking exception; Release uses `BirdieBuddyApp/Info.plist` without ATS exceptions. Debug HTTP is allowed only for localhost, not plain HTTP LAN addresses. Physical iPhones need a reachable HTTPS API. Add new shared keys to both files.
- Before submission, align `MARKETING_VERSION`/`CURRENT_PROJECT_VERSION` with `CFBundleShortVersionString`/`CFBundleVersion` in both property lists. All currently contain `1.0`/`1`, and the property-list values are literal, so changing build settings alone does not update the submitted bundle version.

## Automated gates

- GitHub Actions `ios-tests` succeeds with Xcode 26.5 on an iOS Simulator.
- The iOS test script dynamically selects the newest installed iPhone Simulator; do not pin CI to a model that may disappear from future Xcode images.
- No connected iPhone is required for automated tests. The script leaves its simulator booted; shut down the selected device after local tests or interruption, as described in [testing.md](testing.md#native-ios-tests).
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
- `ScorecardLayoutTests` covers the scorecard at every Dynamic Type size plus narrow counters and large-text round rows. Recheck all current screens, including the new navigation logo, scrolling resume banners, login, practice empty state, and long Korean labels; these tests do not judge safe areas, keyboard avoidance, VoiceOver focus order, or physical tap targets.
- Switch the device language to Korean and confirm every screen reads correctly; untranslated runtime error text is a known gap.
- Validate Account → Diagnostics when iOS delivers a MetricKit payload on a later launch. Delivery is delayed and not guaranteed on the very next launch. Verify manual sharing/deletion and retention of the latest ten reports; there is no automatic upload.
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

## Feature and contract gaps before release

- Resolve and verify the [anonymous reset/verification CSRF gap](mobile-api.md#anonymous-account-flow-gap). Native screens and URL handlers exist, but their unauthenticated `/api/auth` writes currently omit required browser CSRF state.
- Decide whether native draft abandonment, completed-hole editing, practice completion/results, and guided drill steps belong in the first release; these are implemented on the web but absent from native UI.
- Complete runtime error/dynamic-label localization and a full current-screen visual pass. The [design audit](../design/ios-audit/README.md) documents source-level fixes and the remaining capture scope.
- Verify the flat app icon and matching wordmark at installed/navigation sizes. Icon Composer layers exist, but layered rendering and appearance variants have not been integrated.

## App Store Connect

- App name: Birdie Buddy.
- Category: Sports.
- Supply support, privacy-policy, and terms URLs before external TestFlight.
- Complete App Privacy answers from the deployed product behavior: account identifiers, golf-round data, practice data, and product telemetry are associated with the user's account; the app does not track across companies' apps or websites.
- Review the existing `BirdieBuddyApp/PrivacyInfo.xcprivacy` against actual release behavior. MetricKit reports currently stay on-device until manually shared; adding automatic diagnostic uploads would change the data-flow review.
- Add reviewer credentials for a disposable verified test account and explain the offline scorecard flow in review notes.
- Upload screenshots from supported iPhone display sizes after the final accessibility pass.

## Operational release gate

- The production API runs on an always-on instance. A plan that sleeps when idle is disqualifying: the first request of a round would time out on the tee.
- Production SMTP verification/reset delivery succeeds.
- Database backup restore rehearsal succeeds.
- Readiness health check and external monitoring are active.
- Migration SQL is reviewed before deployment.
- Confirm how native sign-in/sync/conflict observations will be collected during TestFlight. The native app currently does not emit the web client's resume and hole-timing product events; existing beta timing metrics do not cover native usage.
