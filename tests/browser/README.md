# Local browser smoke checks

Run `node tests/browser/smoke-server.cjs`. This loopback-only server serves the real static frontend with **in-memory mock APIs**, not PostgreSQL or real accounts. Stop it with Ctrl+C. Restarting resets server data, but browser drafts persist; use a fresh browser profile for a clean repeat, or resolve existing test drafts explicitly. Never clear real user drafts to reset a fixture.

## Live conflict resolution

Open `http://127.0.0.1:4173/live-round.html?id=3`.
The first save of each hole simulates another device saving a score of 8 before the request arrives. Subsequent saves enforce the expected snapshot.

1. On hole 10, increase Score from 3 to 4 and select Retry sync.
2. Confirm the status reports a conflict and the header shows **1 pending**.
3. Select Review save conflict. Confirm Score shows device **4**, server **8**.
4. Cancel. Reopen the comparison and confirm the values are unchanged.
5. Choose Use server record. Confirm score **8**, **0 pending**, and persistence after reload.
6. Select Save & next. On hole 11, increase Score to **5** and Retry sync.
7. Compare device **5** with server **8**. Choose Keep my input & retry.
8. Confirm **0 pending**, server-saved status, and score **5** after reload.

These interactions were manually verified in the in-app browser. They do not constitute automated browser CI, a mobile accessibility audit, or PostgreSQL end-to-end verification.

## Other fixtures

- `/live-round.html?id=1`: ordinary back-nine autosave (no injected conflict).
- `/round-details.html?id=2`: completed-hole editing.

Run queue and shared-view regression tests with `node --test tests/browser/*.test.cjs`.
