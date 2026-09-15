# iOS release checklist

## Project and signing

- Open `ios/BirdieBuddyApp.xcodeproj` in Xcode 16 or later.
- Select an Apple Developer Team for the `BirdieBuddyApp` target.
- Confirm or replace the bundle identifier `com.birdiebuddy.mobile` before the first App Store Connect record is created.
- Set `API_BASE_URL` to the production HTTPS origin in the Archive scheme. Never ship the localhost default.
- Increment `MARKETING_VERSION` and `CURRENT_PROJECT_VERSION` for each submitted build.

## Automated gates

- GitHub Actions `ios-tests` succeeds on an iOS Simulator.
- The iOS test script dynamically selects the newest installed iPhone Simulator; do not pin CI to a model that may disappear from future Xcode images.
- ASP.NET Core build, .NET tests, browser tests, PostgreSQL integration tests, and browser E2E remain green.
- Release configuration archives with no signing, privacy-manifest, or asset-catalog warnings.

## Physical iPhone gates

- Complete an 18-hole round with Wi-Fi disabled and intermittent cellular service.
- Kill and relaunch during a draft; verify the outbox remains user- and round-scoped.
- Create a server conflict and verify both “Use server value” and “Keep my value”.
- Switch accounts and confirm another user's draft is never displayed or synchronized.
- Check iPhone SE-sized layout, safe areas, keyboard avoidance, Dynamic Type, and VoiceOver.
- Test background/foreground transitions, low-power mode, and Wi-Fi/cellular switching.

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
