# B-TASK-03 — Codex A integration handoff

B branch ai/codex-b; HEAD 4a187320643d0c82f232c6b9dd5a667b8401c280. Status BLOCKED. Source diff to integrate: none. No implementation ownership transferred.

Exact changed files: six BTASK03 files listed in BTASK03_CHANGELOG.md; local build-output is generated and must not be integrated. No production source conflicts introduced.

Authoritative export excludes UC023/024/048/015 (each weight2/credited2, COMPLETE_VERIFIED_V1); UC013 likewise excluded. Do not allocate these as new-credit work. Receipt absence for UC048 does not negate credited ledger row.

Closest investigated uncredited candidate UC107 has weight2/credited0/NOT_IMPLEMENTED. To allocate implementation, supply approved SQL task-state/concurrency mapping (current CHECK lacks ASSIGNED/ACCEPTED/PENDING_ASSIGNMENT and EF has no task rowversion), task TASK_EXECUTE:OWN resolver registration and activated transactional notification publisher for decline. No existing permission alternative or DI-only change resolves all blockers. Exact feature DI line cannot be approved usefully before these interfaces are settled; no dummy service was created.

Other options: UC103 also needs series/duration/deadline-history/notification; UC117 needs IR progress/deadline workflow/concurrency and notification. See eligibility decision for remaining groups. Preserve user-owned A/DEV2 reserved scopes.

Required future verification after actual implementation/integration: B-source build, focused tests; fresh approved B SQL trial and isolated port; authenticated Cookie/SQL Sessions, permission/OWN and branch isolation, invalid/stale requests, concurrent accept/decline, atomic audit/notification/rollback, revoke/eligibility during wait, full SRS alternative flows and affected regressions. Re-run on integrated main runtime; current baseline build is not acceptance.

B-TASK-04 proposal: exact contract/ownership approval and implementation of UC107 after shared dependencies land in an approved baseline; alternatively explicitly authorize recovery work separately from new UC credit. Do not auto-merge, change ledger or restore A source into B.
