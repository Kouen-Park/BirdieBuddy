# Birdie Buddy beta operations

This runbook is for the small private beta. Never run restore commands against the production database.

## Release gate

1. Require the GitHub checks `build-and-test`, `postgres-integration`, and `browser-e2e` for `master` in repository branch protection.
2. Confirm the Render deploy is live and run `./scripts/check-deployment.sh https://birdiebuddy.onrender.com`.
3. Download the `postgres-migration-sql` CI artifact and review schema changes before a migration deploy.
4. Record the release commit, Render deploy time, database backup/restore point, and the smoke-check result.
5. The session-version migration intentionally invalidates cookies issued before the credential-session hardening. Ask testers to sign in again after that migration rather than treating the first sign-in redirect as data loss.
6. Before running more than one app instance, set `DataProtection__KeyRingPath` to a persistent shared volume so instances can decrypt each other's authentication cookies.
7. If `browser-e2e` fails, download its `playwright-report` artifact and review the trace, screenshot and application output before retrying. Do not waive the check solely because the fixture project passed.
8. Complete the physical iPhone Safari gate below after the candidate commit is fixed. Record a new result whenever scoring, storage, service-worker or authentication behavior changes.

## Email verification rollout

1. Set `Application__PublicBaseUrl`, `Email__From`, and all `Email__Smtp__*` values in Render secrets.
2. Run `./scripts/check-smtp.sh SMTP_HOST 587` to validate TLS connectivity without sending credentials or email.
3. Keep `Authentication__RequireVerifiedEmail=false`, create a disposable test account, and confirm that verification and password-reset messages arrive and their links return to the deployed HTTPS origin.
4. Set `Authentication__RequireVerifiedEmail=true`, deploy, check `/health/ready`, and verify that a new unverified account cannot sign in while existing migrated accounts still can.
5. Roll back the flag to `false` if delivery is delayed or links are invalid. Do not remove the grandfathering migration.

## Backup restore rehearsal

Create an empty local database whose name starts with `birdiebuddy_restore_test_`. The rehearsal script refuses any non-local target or a target with another name.

```bash
export BIRDIEBUDDY_BACKUP_SOURCE_URL='postgresql://READ_ONLY_USER:...@SOURCE/...'
export BIRDIEBUDDY_RESTORE_TARGET_URL='postgresql://postgres:...@127.0.0.1/birdiebuddy_restore_test_YYYYMMDD'
./scripts/rehearse-backup-restore.sh
```

Use a read-only source credential when Render permits it. The target is erased and replaced by the dump. Record dump duration, restore duration, row counts, migration result, and who performed the rehearsal; then discard the temporary local database through the normal PostgreSQL administration process.

## Monitoring and beta measures

- Configure an OTLP-compatible provider with `OTEL_EXPORTER_OTLP_ENDPOINT` and secret `OTEL_EXPORTER_OTLP_HEADERS`. Alert on sustained HTTP 5xx responses and elevated API latency; never include reset/verification tokens or member emails in telemetry.
- Use authenticated `GET /api/admin/operations` with `X-Admin-Key` for the current instance's route request count, 5xx rate, average latency, and maximum latency.
- Use authenticated `GET /api/admin/operations/beta` for the last 30 days. It reports round completion rate, resumed-round completion rate, and median hole input time.
- Treat these beta targets as release signals: median hole input at most 10 seconds, completed rounds at least 99% of terminal rounds, and resumed drafts completed at least 95% of the time. Small samples must be shown with their counts.

After deployment, verify both `/health/live` and `/health/ready`. A live-but-not-ready instance usually indicates a database connection or migration problem and should not receive beta traffic. Use a returned `X-Trace-Id` to correlate a tester-visible API error with structured application telemetry; do not ask testers for credentials or raw cookies.

## Five-to-ten person beta

Recruit only people who agree to use a test-stage product. Give each tester the same short task: sign in, start a 9-hole round, enter three holes, close and reopen the page, finish the round, edit one completed hole, and review Statistics and Practice. Ask them not to enter sensitive free text.

For each session, record only consented operational observations: device/iOS version, whether the round completed, approximate hole-entry time, whether the draft resumed, and any error trace ID. Do not record passwords, exact location, or raw browsing history. Review the aggregate metrics after at least five completed sessions before changing the scoring flow.

## Physical iPhone Safari gate

Follow `tests/browser/iphone-safari-checklist.md` on at least one physical iPhone. Record device model, iOS version, orientation, release commit, and pass/fail evidence. Chromium viewport results are useful regression checks but do not certify Safari or VoiceOver.
