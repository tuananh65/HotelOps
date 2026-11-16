# UC138/139 canonical workday read integration

Status: PARTIAL_IMPLEMENTATION. No new completed UC; source-complete remains 52/144, historical QA_VERIFIED 27/144.

Canonical AuthRouteHost now enables /workday through SharedAuthHost, with a Workday navigation link. WorkdayRoute requires a confirmed unrestricted session and resets branch selection on session replacement. Branch edits discard prior data; requests retain abort/response-owner protection. The existing APIs independently require SHIFT_VIEW (own-shifts) and ATTENDANCE_RECORD (current workday), with server session and branch/resource authorization. No write endpoint, grant, generated entity, schema or database was changed. Removed the stale unconditional message that the read backend awaits activation.

Actual validation: frontend TypeScript/Vite build PASS (existing React Router use-client directives ignored and >500 kB bundle warnings). 14 workday-router browser tests PASS, including three new canonical-route cases for allowed reads and independent 403 denial. Browser tests use mocked HTTP, not HTTPS/SQL acceptance. Backend unchanged.

Remaining lifecycle work: scheduled/unscheduled Start and End release admission, pause-all participants, assignment notifications and live authenticated SQL acceptance. WorkdaysController write gates remain unchanged. This read integration does not certify UC138/139 business completion.
