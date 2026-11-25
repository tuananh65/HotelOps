# Independent assessment/reservation runtime verification

Scope: `StaffSupplyService` latest assessment/reservation fixes and legacy reusable remaining calculation. Isolated SQL Server database `HotelOpsCompactWorkdayTestV22611250010` only.

## Method

- Executed actual compiled backend service methods against real SQL Server, EF mappings, authorization queries, constraints and triggers.
- Synthetic users, memberships, roles, sessions, room/type, work, consumables, configuration revisions, staff balances and batch custody created inside a serializable transaction for each case. Every transaction rolled back.
- `AssessRoom` and `RoomAssessment` called publicly; internal `Reserve`/`Consume` invoked by reflection exactly as the owning service hooks. This does **not** claim HTTP/controller/end-to-end browser coverage.
- No API process or listener started; port 5781 remains unused. No background process to stop. No real email or AI calls. No live account/password changes.
- Initial issuance documents and shift-start allocation are seeded preconditions, not verified workflows in this harness.

## Results

10 business cases passed; independent fresh-context cleanup check passed: **11 PASS, 0 FAIL**.

1. Reserve full capacity × norm initially; release excess after actual usable stock assessment.
2. Reassignment preserves actual deficit and prior norm even when next employee has a different per-guest snapshot.
3. Unassessed reassignment retains original maximum norm.
4. Recorded partial placement settled once on old employee; exact old batch debit; next employee receives only remaining deficit and zero newly placed quantity.
5. Missing old reserved item on new employee rejects before old reservations change.
6. Partial completion without linked missing report rejects without consuming.
7. Correct linked missing report permits actual-only consumption; releases residual hold; duplicate consume does not debit twice.
8. Unlinked and insufficient report quantities do not authorize incomplete refill.
9. Full actual-deficit refill completes without missing report; no full-room overconsumption.
10. Reclassified legacy reusable amount subtracts from Remaining without fabricating Use/Return.
11. Separate SQL connection sees zero `V2AS-` fixture users after rollback.

## Evidence and reproduction

- `artifacts/restructure-v2/assessment-runtime-results.json` includes individual results and loaded backend assembly SHA-256.
- `artifacts/restructure-v2/assessment-runtime.log` runtime output.
- `artifacts/restructure-v2/assessment-harness-build.log` build output.
- Build: passed, 0 compiler errors. NU1900 warning: NuGet vulnerability feed unavailable because external network unavailable. This is not a clean vulnerability audit.
- `node --check scripts/verification/v2-assessment-runtime.mjs`: passed.

```powershell
dotnet build scripts/verification/v2-assessment-runtime/V2AssessmentRuntime.csproj --no-restore -p:BuildProjectReferences=false
node scripts/verification/v2-assessment-runtime.mjs
```

Build current backend first if testing a newer backend revision. The harness intentionally does not rebuild the shared API project, avoiding concurrent compiler output mutations.

## Files owned

- `scripts/verification/v2-assessment-runtime.mjs`
- `scripts/verification/v2-assessment-runtime/V2AssessmentRuntime.csproj`
- `scripts/verification/v2-assessment-runtime/Program.cs`
- This report and named artifact outputs.

No production source modified. No unresolved business questions. HTTP, browser, initial allocation and actual production migration remain outside this narrow verification.
