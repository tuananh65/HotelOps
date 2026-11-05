# Task85-B — changelog

New production: TurnoverHistoryController.cs; HotelStructure/TurnoverHistoryService.cs,
TurnoverHistoryBoundary.cs, TurnoverHistoryFiles.cs, TurnoverHistorySchema.cs.
Existing production modified: **Program.cs only**, one explicitly approved scoped registration.
Existing DTOs, Task82/84 source, PhaseA evidence, schema/EF/RBAC/authentication and DEV2 files untouched.

New PhaseB artifacts: requested5 reports/validation, pre/postflight manifests, exact DI patch and
approval receipt, controller proposal, selective extraction script, DI and HTTPS verification
source/projects, SQL preparation proposal/hash review, logs/receipts and isolated build outputs.
No source deleted. No Git mutation/protected-database access. Exactly one separately approved
trial created/retained; no pre-existing trial reset. No UC points added.

Failures retained: first proposal compile CS0117 (removed unused InvalidReference mapping);
second harness DI failure (added harness-only IWebHostEnvironment/IConfiguration); final26 checks pass.
First shell generation of HTTPS draft failed because PowerShell Host is read-only and line-ending
search mismatched; regenerated draft with task-specific variables and compiled successfully before
any SQL execution. No production/environment change was made to work around these harness issues.

Live harness failures: SYS_ADMIN operational permission assumption (38 checks before failure),
fixture role SaveChanges omitted (29), wrong Task82 timelinePage object assertion (119).
Final corrected run146 checks PASS. Corrected verification code only, production unchanged.
Continuations preserve business fixtures and use separate owned identities; retained trial has
25users/19role assignments/3rooms/3cycles after one final oracle identity; catalog12/62/140 unchanged.
Additional26 field-oracle HTTPS/SQL checks PASS. Auxiliary failed generation/
build logs retained where present. No failed execution relabeled PASS.
