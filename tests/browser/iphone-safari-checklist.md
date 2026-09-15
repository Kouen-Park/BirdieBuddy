# iPhone Safari release check

Status: **not executed on iPhone Safari**. This workstation has Command Line Tools but no Xcode/iOS Simulator (`xcrun --find simctl` fails). Desktop viewport checks and mocked UI tests are not a substitute.

Use a physical iPhone or install Xcode plus an iOS simulator runtime, then open Safari. Record device model, iOS version, URL/build, orientation and results. Do not use a production round for destructive tests. A deployment or device-accessible development URL is required; `127.0.0.1` on the phone refers to the phone, not this Mac.

| Check | Expected |
| --- | --- |
| Scroll a long scorecard with address bar expanded and collapsed | Header remains reachable; no unexpected horizontal page scrolling |
| Open navigation halfway down the page, attempt background scroll, close with × or backdrop | Background does not move; original page position is retained |
| Landscape / small viewport, scroll inside navigation | Account/logout/footer accessible without overlap |
| Rotate while navigation is open | No stuck scroll lock or inaccessible controls |
| Open hole editor, focus numeric input, show/hide keyboard | Save/Cancel remain reachable by scrolling; input is not hidden or zoomed unexpectedly |
| Live hole entry at page bottom | Save/next remain tappable and clear of the home indicator |
| Offline edit, refresh while assets remain available, reconnect | Pending changes retained and synced; a missing cached draft shows recovery guidance; full offline boot is not promised |
| Another session changes the same hole | Comparison shown; Cancel preserves input; each resolution behaves as labelled |
| VoiceOver navigation and larger text | Menu controls labelled; hidden drawer skipped; dialog controls reachable; VoiceOver announces `Saved on this device`, `Syncing with server`, `Offline`, `Saved to server`, and conflict/error alerts |
| Reduced Motion enabled | No unnecessary menu or page animation |

For the VoiceOver row, use rotor navigation to visit the live-round status region after an edit, during reconnect, and after a forced conflict. Verify the conflict dialog announces its title, comparison table caption, and all three choice buttons. Record whether each status is announced once and whether focus remains inside the dialog while it is open.

Do not mark this release check passed until these tests have been performed in iPhone Safari. Record device model, iOS version, release commit, each row's pass/fail result, and any failed case with screenshot/video and exact steps.
