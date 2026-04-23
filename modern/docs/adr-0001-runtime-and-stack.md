# ADR 0001: Runtime and Stack Baseline

Status: Accepted
Date: 2026-04-18

## Context
The modernization target is .NET 10 MAUI Blazor Hybrid with local-first architecture and background synchronization to PostgreSQL.

Current developer environment in this workspace has .NET 8 SDK installed. .NET 10 SDK is not currently available on this machine.

## Decision
Implementation starts immediately on .NET 8 project templates to avoid delay. Project structure, module boundaries, APIs, and local-first architecture are being built to remain forward-compatible with a planned framework upgrade.

## Consequences
- Development begins now with working code and tests.
- Upgrade task to .NET 10 is required before production readiness.
- CI should enforce SDK pinning once .NET 10 is available.

## Follow-up
1. Add `global.json` pinned to .NET 10 SDK after installation.
2. Upgrade all projects from net8.0 to net10.0.
3. Run full regression tests and MAUI packaging checks after upgrade.
