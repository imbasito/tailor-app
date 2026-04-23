# Cutover Runbook And Rehearsal Checklist

Status: Draft
Date: 2026-04-21
Owners: Engineering Lead, QA Lead, Operations Lead

## Objective
Execute a controlled production cutover from the legacy system to the modern platform with clear go/no-go gates, rollback readiness, and post-cutover validation.

## Scope
- Local-first modern platform is promoted as the production system.
- Legacy Laravel runtime is retired from active operations after acceptance.
- Emergency rollback remains available during the cutover window.

## Roles
- Cutover Commander: Coordinates timeline and checkpoints.
- Release Engineer: Executes deployment, database migrations, and smoke checks.
- QA Lead: Runs functional/UAT and sign-off scripts.
- Operations Lead: Owns backups, monitoring, and rollback package.
- Business Representative: Provides final acceptance confirmation.

## Pre-Cutover Checklist (T-7 To T-1 Days)
1. Freeze scope and confirm release commit SHA.
2. Run non-MAUI gate in CI and capture artifact links.
3. Export legacy database snapshot and checksum manifest.
4. Validate import/reconciliation report against baseline totals.
5. Validate local and central migration scripts in staging.
6. Confirm WhatsApp deep-link flows manually on MAUI and desktop.
7. Confirm dry-run sync worker configuration (enabled=false in production unless dispatcher is ready).
8. Prepare rollback package (last known good app build + DB restore commands).
9. Publish change communication with maintenance window.

## Rehearsal Script (Staging)
1. Restore staging from production-like snapshot.
2. Apply local and central DB migrations.
3. Execute legacy data import endpoint with expected flags.
4. Run reconciliation checks:
- customer count and order count parity
- charged, paid, and balance totals parity
- unresolved migration issues list reviewed
5. Run workflow smoke tests:
- New -> InProgress -> TrialFitting -> Rework -> Ready -> Delivered transitions
- no-op, skip, and backward transitions rejected
6. Run UI smoke tests:
- order wizard happy path
- orders board retryable summary link
- outstanding dues report WhatsApp prefill
7. Run sync checks:
- enqueue duplicate payload twice and verify one queue item only (idempotency)
- mark failed and verify retry due is deferred via next attempt timestamp
- verify diagnostics endpoint values at /api/sync/diagnostics
8. Execute rollback rehearsal:
- restore pre-cutover DB backup
- redeploy rollback package
- rerun basic health and smoke checks

## Cutover Day Timeline
1. T-60m: Start maintenance window, block new writes on legacy system.
2. T-45m: Take final legacy backup and verify checksum.
3. T-35m: Deploy modern API build and apply migrations.
4. T-25m: Execute import and reconciliation scripts.
5. T-15m: Run smoke suite and diagnostics checks.
6. T-10m: Conduct go/no-go review with stakeholders.
7. T-0m: Switch traffic to modern platform.
8. T+30m: Monitor health, error rates, and sync diagnostics.
9. T+120m: Complete hypercare checkpoint and sign-off.

## Go/No-Go Criteria
Go only if all are true:
1. All migration and reconciliation checks pass.
2. Smoke tests pass for API and UI critical paths.
3. Error budget and telemetry remain within agreed thresholds.
4. Rollback package is validated and accessible.
5. Business representative approves final verification.

## Rollback Plan
Trigger rollback if any go/no-go criterion fails during the cutover window.
1. Stop modern write traffic.
2. Restore database from final pre-cutover snapshot.
3. Redeploy rollback application package.
4. Re-enable legacy traffic.
5. Communicate incident and next recovery checkpoint.

## Post-Cutover Verification
1. Validate order creation, payment posting, and status transitions.
2. Validate reports (outstanding dues, daily orders, delivery queue).
3. Verify sync diagnostics trend is stable.
4. Archive cutover logs, evidence, and approval notes.
5. Link final evidence in release notes.
