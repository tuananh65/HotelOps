# B-TASK-01 — Integration handoff for Codex A

Branch: ai/codex-b

HEAD: 4a187320643d0c82f232c6b9dd5a667b8401c280

Status: BLOCKED. Exact implementation source files: none. Source diff to integrate: none. Six new documentation artifacts are listed in BTASK01_CHANGELOG.md; no existing artifact was replaced.

Baseline dependencies required before selecting an uncredited independent UC:

1. Current approved DEV1 ledger and Task82–86 receipts available inside B or explicitly supplied; exact missing uncommitted A source cannot be reconstructed from this baseline.
2. Codex A active UC reservation, to prove no overlap.
3. For preferred UC-013: approved read permission and branch scope mapping covering its intended actors. No RBAC edits are authorized here.
4. B-only authorized trial database identity and local configuration procedure, with isolated HTTPS port and output directory. Do not use HotelOpsDB or A trial database.
5. Review required service registration strategy before implementation. No DI/schema changes were made or approved. Do not bypass the Program.cs restriction by hiding registration elsewhere.

Alternatives: UC-113 needs approved interval storage/recording baseline; UC-123 needs approved scoped reads and activated notification publisher plus withdrawal acceptance.

Potential future conflicts: Program.cs DI and shared authorization catalog if scope is separately approved; no current source conflict introduced. No claim about unseen A source.

After future integration, rerun B and combined-runtime acceptance: full selected SRS flows, Cookie/SQL-session auth, permission and branch isolation, filters/empty/detail cases, validation and negative cases, related regression, build and meaningful automated tests. For mutations add concurrency, atomic audit, rollback and publisher failure verification. No previous task results substitute for these checks.

Other worktrees: registry lists main feature/BE1-database-integration at 4a187320, task11 rehearsal at 3688ba99, task12 resolution at d6ef057b, AI-Loop at 8e0b6730, and B at 4a187320. This is metadata only; external file/index states were not inspected or independently verified unchanged.
