# Validation before frontend redesign

Branch: `feature/frontend-redesign`, created from the existing integration checkout with explicit user approval.

## Inventory

- Reference commit: `9d220a94509e650464406a9b83099d24cb568220` (`feature/react-frontend`).
- Confirmed 80 unique screen IDs in the reference manifest.
- Recorded 163 static route declarations from current TSX sources. This includes nested, wildcard and potentially unused declarations; it is not a count of reachable screens or a certification of feature parity.
- No application source or backend code changed during this preparation.

## Frontend build

`npm.cmd run build`: passed on the integration baseline.

Warnings: React Router's `use client` directives were ignored by the bundle step; the generated JavaScript chunk is about 1,593.61 kB (405.19 kB gzip), above Vite's 500 kB warning threshold. These warnings predate the redesign.

## Automated tests

`npm.cmd test`: started, then stopped after repeated failures and approximately 32-second timeouts in the user branch membership flow. The suite did not complete; do not report it as passed or as a complete test run.

Observed failures include invalid GET ETag handling, wrong-target DTO rejection, GET failure snapshot hiding, and profile-only admission / confirmed POST. Other activation and route-guard cases passed before these failures. Root cause is not established. No application source had been changed when the failures occurred; do not fix unrelated business/authentication logic as part of visual work without assessing scope.

## Preview verification

Rendered the three revised static previews in headless Microsoft Edge through Playwright at 1440 x 900. Inspected screenshots and corrected viewport overflow. These are desktop composition previews; they do not verify application behavior or mobile layouts.

No real API or SQL Server verification was performed. No C# source changed, so the .NET build/test commands were not run in this preparation.
