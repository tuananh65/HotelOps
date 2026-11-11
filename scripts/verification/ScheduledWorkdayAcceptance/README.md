# UC139 scheduled SQL + HTTPS acceptance (NOT_RUN)

This executable does not provision databases, migrations, fixtures, trust certificates, activate controllers or enable frontend flags. The default invocation refuses execution. A receipt is a reviewed owner/PL authorization artifact, not a self-issued declaration. Its SHA must be supplied out-of-band by the approving owner; a hash alone is not a signature.

Runtime owner must supply:
- Exact isolated server/database/GUID; database must start `HotelOps_UC139_Trial_`. Never primary/shared HotelOpsDB. Ownership/isolation proof and exclusive bounded lease.
- Approved WorkTime schema artifact + SHA and actual deployment receipt. Current legacy 72-table import alone is insufficient. Workdays/intervals, SQL 8-byte rowversion and unique one-working/one-open indexes required.
- Accepted fixture artifact + SHA: unrestricted user with ATTENDANCE_RECORD in the approved branch; a second existing denied branch; three distinct, today's active PLANNED shift assignments with active template/department and branch membership. No current Working day or open interval, and no prior workday claims. Third assignment is used only in rolled-back fault injection.
- Restricted SQL/application identity (not sysadmin/db_owner), metadata visibility for DB GUID/index precheck, required DML permissions only in the leased trial. Valid SQL certificate chain/hostname and HTTPS certificate trust. No bypass.
- Approved isolated trial backend exposing scheduled candidate actions with actual cookie/session/CSRF middleware. Production WorkdaysController remains NonController. This runner DOES NOT enable it. Live trial activation requires separate approval.
- Explicit audit-fault injection permission. The in-process EF interceptor throws during actual AuditLog SaveChanges after Workday INSERT; transaction is rolled back and a separate context checks no workday/audit fragment.

Configure locally (never commit values): `UC139_TRIAL_SQL`, `UC139_TRIAL_USERNAME`, `UC139_TRIAL_PASSWORD`. SQL Encrypt Mandatory/Strict and TrustServerCertificate=false are enforced. No exceptions, responses, tokens or credentials are printed. No automatic retry or replay. Timeout/cancellation/unknown outcome stops execution. Deadlock is a failed run, never PASS.

```powershell
dotnet build scripts/verification/ScheduledWorkdayAcceptance/ScheduledWorkdayAcceptance.csproj
# Fill an owner-approved receipt outside Git; obtain SHA from that owner.
dotnet run --no-build --project scripts/verification/ScheduledWorkdayAcceptance -- --prepare <receipt.json> <approved-sha256>
# ONLY after separate resource/lease/trial mutation/fault approval:
dotnet run --no-build --project scripts/verification/ScheduledWorkdayAcceptance -- --execute <receipt.json> <approved-sha256>
```

Prepare validates artifacts/configuration offline; it opens no SQL/HTTPS connection. Execute verifies identity/restricted principal/schema, fresh fixtures, actual audit-fault rollback, real login/session/CSRF, denied branch 403, Start 201, duplicate rejection, stale End 412, invalid If-Match, End 200, post-End current 204, rejected restart, two concurrent Starts with one winner, audit pairs, logout/session 401. It intentionally consumes two fixture assignments and leaves their audit history. A rerun with consumed fixtures refuses before writes. No DELETE/cleanup or database reset is performed. Owner disposes the leased database separately.

NOT covered automatically: Pause All/source-writer races and out-of-schedule notifications (external contracts missing), frontend browser against this runtime, unrelated business data, imported historical sessions, HTTP fault-injection 500-vs-503 policy, failure of every final commit boundary. Do not call whole UC139 COMPLETE_VERIFIED based solely on this runner.
## Trial preparation package (035R)

Use the separate ScheduledWorkdayTrialHost project and its checked-in candidate SQL package. See docs/fullstack-integration/DEV3-FASTTRACK-035R/HANDOFF.md for exact preparation and runtime-owner steps. Receipt now also binds trialPackageManifest/trialPackageSha256 and ShiftId/RaceShiftId/RollbackShiftId; --prepare checks closure/hash/loopback identity offline. --execute additionally verifies owner marker, full mapped WorkTime metadata, existing Auth startup prerequisites and exact fresh shift/assignment identity before any acceptance write. The host uses dedicated cookies/keys and defaults disabled; no production controller/Program.cs changes. Candidate SQL is NOT_APPROVED and must never be applied automatically.
