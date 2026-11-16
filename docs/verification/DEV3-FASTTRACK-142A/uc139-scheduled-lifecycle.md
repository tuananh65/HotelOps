# UC139 scheduled Start/End source integration

Status: PARTIAL_IMPLEMENTATION, not COMPLETE_VERIFIED. Completion counts unchanged: 52/144 source complete, 27/144 historical QA_VERIFIED.

WorkdaysController now exposes existing POST Start and End actions; duplicate GET actions were removed and Location points to WorkdayReads.Current. HTTPS, cookie authentication, global API CSRF middleware, current active employee/session, ATTENDANCE_RECORD and resource branch/assignment ownership remain enforced. The shared Serializable transaction retains user/time/source locks; Start checks current-date eligible assignment and prior claims, End checks strong row version and absence of open intervals; both persist audit atomically. A final session validation before commit rejects expiry while waiting and rolls back unsuccessful operations.

Canonical /workday/scheduled mounts the existing ScheduledWorkdayPanel after explicit branch selection. It bootstraps CSRF only after review confirmation, binds responses to current session/branch, refreshes authoritative reads and prevents automatic resubmission after uncertain outcomes. /workday links to this workflow. No role/grant/schema change or SQL mutation.

Actual checks: backend build PASS (0 warnings/errors); frontend TypeScript/Vite PASS with existing ignored React Router use-client and large-chunk warnings. Six controller tests PASS (MVC discovery without duplicate GET, HTTP rejection and invalid End ETags). Five focused mocked browser tests PASS (Start/End, interval guard, ambiguous response lock, independent read denial); one new canonical scheduled-route review test PASS. These tests do not prove SQL mutation, rollback or actual HTTP security pipeline behavior.

Remaining: unscheduled Start notification and Pause All continue returning explicit unavailable errors; no bypass. Live disposable HTTPS/SQL Start/End, CSRF, scope, stale-version, race, expiry/rollback and cleanup acceptance remains unexecuted. Do not certify UC139 from this source increment.
