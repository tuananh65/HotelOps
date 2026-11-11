# UC139 disposable trial package — candidate, not approved

Base: 70e1d4ee2c4b63b14543f90c1e315bec39585d80. Production WorkdaysController, WorkdayService, SqlWorkTimeTransactionParticipant, DbContext and Program.cs are unchanged. No frontend flags/routes change. No SQL, HTTPS listener, certificate provisioning or deployment was executed for preparation.

## Runnable deliverables

From repository root (Windows Development); use absolute receipt/artifact/output paths with dotnet run:

```powershell
dotnet build scripts/verification/ScheduledWorkdayTrialHost/ScheduledWorkdayTrialHost.csproj
dotnet build scripts/verification/ScheduledWorkdayAcceptance/ScheduledWorkdayAcceptance.csproj
dotnet run --no-build --project scripts/verification/ScheduledWorkdayTrialHost -- --self-test
node scripts/verification/ScheduledWorkdayAcceptance/test-guards.mjs
# Offline only; output directory must not already exist:
dotnet run --no-build --project scripts/verification/ScheduledWorkdayTrialHost -- --emit-package <new-output-directory>
# Fill receipt.template.json OUTSIDE Git, using the actual owner-approved artifact hashes/IDs:
dotnet run --no-build --project scripts/verification/ScheduledWorkdayAcceptance -- --prepare <receipt.json> <owner-supplied-receipt-sha256>
```

A reviewable generated copy is under scripts/verification/ScheduledWorkdayTrialHost/candidate/. Five SQL files plus package.json:
1. install-candidate: creates ONLY three existing EF WorkTime tables/indexes/FKs in one transaction, refuses any existing WorkTime table; generated from actual model, not invented columns.
2. verify-read-only: full mapped column/type/nullability/length/rowversion, PK/FK/ordered-index/filter checks.
3. rollback-empty-candidate: requires this lease's installation marker, refuses ANY WorkTime rows, drops only the three candidate tables; no fixture/history deletion.
4. fixtures-candidate: creates exactly three fresh ASSIGNED ShiftAssignments against THREE existing approved nonoverlapping today's PLANNED shifts. It validates active actor/primary branch/department, existing attendance grant and membership, denied branch, active template/creator, absence of prior assignments/workdays/live sessions. No user/grant/password/shift creation or updates. Outputs actual assignment IDs to copy into receipt, paired with shiftId/raceShiftId/rollbackShiftId.
5. verify-fixtures-read-only: same prerequisites plus exact three installed assignments and no live session/consumed claims.

## ENV approval and resource prerequisites (still required)

Approve these CANDIDATE artifacts before SQL application; package.json never grants approval. Runtime owner must independently prove disposable server/database/GUID (HotelOps_UC139_Trial_*; never HotelOpsDB), exclusive bounded lease <=8h, and provision database extended property HotelOps.UC139.Trial equal to LeaseId only after isolation verification. Default/old database names alone are not ownership proof. Record approved base-schema/deployment receipts; WorkTime is not the full Auth/HotelOps schema.

SQLCMD scripts require explicit, separately reviewed values: TrialServer, TrialDatabase, TrialDatabaseGuid, LeaseId, LeaseStartsUtc, LeaseEndsUtc; fixture scripts additionally ActorId, BranchId, DeniedBranchId, ShiftId, RaceShiftId, RollbackShiftId. All identifiers/lease strings are owner-controlled SQLCMD literals, not untrusted application input. Use SQLCMD error-stop (-b); do not echo credentials or run scripts from this task automatically. Installer needs a separately authorized DDL identity; host/runner reject sysadmin and db_owner.

Owner must provision a dedicated active unrestricted identity with a supported hashed password and existing approved ATTENDANCE_RECORD permission path, active branch/department/membership, and three approved planned shifts/templates/creator. Package intentionally does not invent seed identity/grants, reset passwords or create policy. Existing historical users/sessions must not be silently reactivated. A second active branch without actor membership supplies the denied fixture. Approval of fixture SQL is separate from business shift-plan release.

Set receipt's schemaArtifact/fixtureArtifact to files01/04, their hashes, trialPackageManifest/package SHA and actual shift/assignment identities. --prepare binds the entire five-file closure and opens no network. The existing negative --execute guard probe uses NOT_APPROVED expired synthetic data and is refused before any network; no acceptance execution took place.

## Trial-only HTTPS host (not started)

Only after explicit runtime approval: configure UC139_TRIAL_SQL (Encrypt Mandatory/Strict, certificate verification, no trust/hostname override), UC139_TRIAL_USERNAME and the runner-only UC139_TRIAL_PASSWORD outside Git. Provide an existing valid trusted development HTTPS certificate, or process-scoped UC139_TRIAL_HTTPS_PFX/UC139_TRIAL_HTTPS_PFX_PASSWORD outside Git; inherited Kestrel endpoints are discarded; no trust bypass/callback is installed. Then set process-scoped UC139_TRIAL_HOST_ENABLED=APPROVED and invoke host --serve <receipt> <owner-sha>. Default invocation exits disabled. It binds loopback HTTPS at exact receipt origin only, validates DB GUID/owner marker/restricted principal/WorkTime+Auth schema/fresh fixtures before listening, and stops at lease expiry.

Exactly eight actions: Auth login/session/logout, CSRF GET, Workday current/own-shifts GET, scheduled Start/End POST. Only receipt fixture assignments/branch can be mutated; rollback-only assignment, unscheduled and Pause All are refused. No ChangePassword/unrelated controller or production WorkdaysController is discovered. Real cookie/session/permission/CSRF middleware is reused; secure trial cookies and encrypted DPAPI key ring have separate names/purpose outside Git. Only approved login identifier and actor are admitted; all business authorization still runs server-side. No production Program.cs/DI mutation.

Only a fresh approved receipt/lease permits existing runner --execute. It consumes two fixture claims and audit history; do not replay. Dispose the trial database under owner policy after acceptance, not via destructive cleanup here. Unknown outcomes/deadlocks/failures are never PASS.

## Evidence boundary

Focused offline host checks exercise config/TLS/lease/HTTP admission, artifact generation/tamper/no-overwrite, actual MVC discovery/DI/options and negative controller dispatch. Runner guards exercise --prepare and refusal paths. They do NOT establish SQL syntax/provider execution, physical locking/races, live authentication/certificates, audit atomicity or rollback. SQL/HTTPS acceptance remains NOT_RUN and whole UC139 remains PARTIAL. Notification handoff is one page in NOTIFICATION_HANDOFF.md; no fake producer was added.

Final offline receipts: trial host 44 PASS; runner guards 9 PASS; existing ScheduledWorkday verification 51 PASS. All project/solution builds exit0, zero warnings/errors. Solution dotnet test exit0 discovers no test projects; no test-case credit from that command. Default host refusal and deterministic candidate file identity were checked separately. SQL/HTTPS execution NOT_RUN.

A final runner compile succeeded but the .NET CLI exit logger raised ObjectDisposedException (exit1). Sequential rerun with process-scoped DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true / MSBuildEnableWorkloadResolver=false and --disable-build-servers completed build exit0 (0 warnings/errors), then all9 runner guards PASS. No global setting or source workaround was applied.
