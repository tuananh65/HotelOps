# DEV3-NEXT-007 — UC138 dated shift planning

Status: IMPLEMENTED_SOURCE_ONLY (internal business slice, not a complete UC).
Branch: feature/dev3-uc138-shift-planning.
Base: fdd46ac89e645f40470e9b772fb0224cf41b226e.

## Ledger and selection

scripts/verification/HANDOVER_CODEX_A_20261105/FULL_UC_LEDGER.md records UC138 NOT_IMPLEMENTED; UC023 was already credited COMPLETE and PR36 receives no additional UC completion credit. UC004 multi-role/scope alternatives, UC007 transfer/removal and UC010 revoke lack approved narrow semantics; UC006 release acceptance is explicitly locked in UserAccountDeactivationReleaseAcceptance. No gate was removed. Selected a concrete internal UC138 planning operation from SRS 02_use_case_specifications.md UC138 steps 3-4 and GBR141, compatible with the existing ShiftTemplate fields and overlap-reader contract.

## Implemented

ShiftSchedulePlanner materializes wall-clock start/end on a roster date using the explicitly supplied verified branch timezone. The existing CrossesMidnight field chooses the next end date. Returns an immutable dated UTC interval, retaining the roster date separately. Rejects nonpositive intervals, date overflow, nonexistent/ambiguous local DST times and silent UTC clamping. It does not guess a branch, timezone, offset, break deduction, maximum-duration policy, role or permission.

ShiftAssignmentConflictReader gains a dated-template overload. It rejects invalid employee/branch/template, inactive and cross-branch templates before querying, plans the UTC range, then delegates to the existing global employee overlap reader. Existing raw-range query, cancelled-state handling, exclusion ID and half-open overlap semantics are unchanged. Template metadata must be server-trusted and re-read under writer locks; passing a template never confers authority.

Schema inspection: Entities/ShiftTemplate.cs and HotelOpsDbContext ShiftTemplate mapping (start_time, end_time, crosses_midnight); no entity, column or mapping changes. BreakMinutes is not deducted: the full shift range is reserved for conflict checks, not payroll calculation.

## Verification actually executed

- dotnet build HotelOps.slnx: PASS, exit 0, zero warnings/errors.
- dotnet run --project scripts/verification/ShiftSchedulePlanning/ShiftSchedulePlanning.Verification.csproj: 24/24 PASS, exit 0. Covers normal/overnight UTC planning, leap/year rollover, timezone bounds, DST start/end gaps and ambiguity, invalid durations, adjacent/overlapping ranges, template branch/active eligibility and cancellation before SQL.
- Initial verification harness had CS4014 (unawaited cancellation assertion); fixed by explicit discard. Final run has no compiler warning.
- dotnet test HotelOps.slnx --no-build: exit 0, no test cases reported; not counted as acceptance.
- Exact staged allowlist, whitespace and secret-pattern scan checked before commit.

No SQL executed. The tests do not claim successful database overlap reads, locking, concurrent assignment or transaction acceptance. Frontend unchanged; no frontend build/test claimed for this backend-only slice.

## Remaining implementation and policy boundaries

No public API, DI, roster writer, template persistence, assignment creation/cancellation, audit/outbox writer or frontend management screen is introduced. Those remain necessary for UC138. Before exposing management, DEV3/TL/security must finalize the management HTTP/version/authorization contract; writes must retain User-first locks, source rechecks, audit/outbox atomicity and an approved notification enqueue contract. Existing Department Head clarification remains branch-scoped; no single-department or role-name authorization was invented. This planner cannot satisfy those release gates.

No shared policy, Program.cs, EF entities/schema, migrations or DEV1/DEV2-owned writer changed. WorkdaysController remains NonController; scheduled mutation dispatch OFF and candidate unmounted. No shared SQL, live API, deployment or merge. UC008 Update stays version-blocked. Official UC ledger not changed; no COMPLETE_VERIFIED claim.

## Source SHA-256
src/HotelOps.Api/Services/WorkTime/ShiftSchedulePlanner.cs: AF4E3C12234EB61434850C59505EA52AB2CD997F3EE866112278AD5FF3F11FC9
src/HotelOps.Api/Services/WorkTime/ShiftAssignmentConflictReader.cs: B53928FE1503E257A6CB5C6962A8E592EDFD80C7633B26F0553439ED9941ED02
scripts/verification/ShiftSchedulePlanning/Program.cs: 32B5A60060EA7281846735870035702AAA67B78B0E2858CF773505E802D81ABA
scripts/verification/ShiftSchedulePlanning/ShiftSchedulePlanning.Verification.csproj: D5DB2CEC2BFB132082C19FC51669858F454CCB2AFD6D9FE87A6790D8AA96E8F0

## DEV3-NEXT-008 synchronization

Merged integration ec945e9070855baf2cebf3ecddd8f2229020d33a without conflict. Same five functional files. Backend build exit 0, zero warnings/errors; focused planner checks 24/24 PASS, exit 0. Scoped dotnet format whitespace for planner and conflict reader --verify-no-changes --no-restore exit 0. Planner requires caller-verified timezone; reader overload validates active/same-branch template and delegates global employee overlap. No SQL/live, route activation or policy change.
