# DEV3-NEXT-002 — UC139 End response validation

Status: IMPLEMENTED_SOURCE_ONLY; independent review and live acceptance pending.
Branch: feature/dev3-uc139-end-response-validation.
Base: integration/fullstack-base at f219d6fc6fea5ee6ce6a375cf3a4d7347f99db1c.

## Contract and source change

WorkdayResponse in src/HotelOps.Api/DTOs/WorkTime/WorkdayRequests.cs declares EndedAtUtc as nullable DateTimeOffset. Scheduled End must only be confirmed after a valid correlated ENDED DTO and strong ETag. The prior endedAtUtc check used Date.parse, which silently normalizes invalid calendar dates and accepts 24:00 and offsets beyond DateTimeOffset bounds.

Added eight API regressions: five impossible calendar/clock/offset responses and three valid leap-day/fraction/offset responses. Before the fix: 32 PASS / 5 FAIL, exit 1. The five failures incorrectly returned CONFIRMED. The decoder now validates the calendar date, clock ranges and offset bound before the existing ordering check. It preserves response correlation, ETag, safe IDs, state, branch, assignment and sanitization checks. No backend behavior, API activation or business policy changed.

One new offline browser regression verifies that a malformed End response yields Outcome unknown, never confirmed success, locks replay and does not perform a success refresh. Synthetic fixture fetches are isolated from live networking. The existing no-retry, double-submit, branch/session replacement and independent permission tests remain intact.

## Executed verification

- node --test tests/scheduled-workday-api.test.mjs tests/scheduled-workday-browser.test.mjs tests/workday-api.test.mjs: 92/92 PASS, exit 0 (37 scheduled API, 20 browser, 35 read API).
- npm.cmd run typecheck: PASS, exit 0.
- npm.cmd run build: PASS, exit 0; two inherited React Router use-client warnings.
- Scoped Prettier for the three changed source/test files: PASS, exit 0.
- git diff --check and exact allowlist: required before commit.

## Preservation and limitations

Only scheduledContracts.ts, its two test files and this handoff differ from integration. No Auth, Hotel, public route, read panel, writer service, controller, SQL, schema, permission or DEV2-owned files changed. Both WorkdaysController and ScheduledWorkdayCandidateController remain NonController; scheduled mutation dispatch defaults OFF. No production activation, SQL/HTTPS, migration, deployment or merge performed. No dependency on unmerged DEV2 PR #25.

This slice validates malformed End response dates; it is not full UC139 acceptance or exhaustive DateTimeOffset equivalence. Existing Date.parse chronological comparison retains millisecond precision; this change does not claim 100ns ordering verification. Notification, runtime authorization, SQL concurrency/rollback and approved trial acceptance remain pending.

## SHA-256
frontend/src/workday/scheduledContracts.ts: E7DE75AA8B1F54003A79AFF68B5C7F8C229494E7DD7F1E21EEB6BE7A780F71A1
frontend/tests/scheduled-workday-api.test.mjs: A2ABEF515CB2D1BE30E55B98C62B432FED19F78E69798739967B55722B2AD9A8
frontend/tests/scheduled-workday-browser.test.mjs: F2297B925293B37C55E53F5F08283E5B5800EAD2F7637CE0C201907083760F73
