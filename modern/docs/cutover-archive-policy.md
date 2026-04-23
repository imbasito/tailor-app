# Cutover Archive Policy

Status: Confirmed
Date: 2026-04-18

## Decision
At go-live, the legacy Laravel system will be fully archived.

## Scope
- The new platform becomes the only operational system.
- Legacy runtime endpoints are not kept online in read-only mode.
- Historical legacy artifacts are archived offline for compliance and reference.

## Archive Deliverables
1. Database dump and checksum manifest.
2. Source code snapshot and dependency lockfiles.
3. Environment configuration inventory (sanitized).
4. Audit log export if available.
5. Restoration instructions for emergency forensic access.

## Preconditions Before Archive
1. Migration reconciliation checks pass.
2. UAT sign-off complete.
3. Post-cutover smoke tests green.
4. Rollback package prepared for the cutover window.
