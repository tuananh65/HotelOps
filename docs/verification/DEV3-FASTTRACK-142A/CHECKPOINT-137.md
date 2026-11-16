# FASTTRACK-142A checkpoint137 — incident location validation

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=b46aff9246d7b24847be46695d37fd2ea5192938
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

Found and repaired a concrete UC049/GBR066 violation in the existing CreateIncident command: all three location identifiers could be absent. The command now requires at least one room, area or asset, rejects nonpositive supplied location IDs even when another is valid, and validates source-link input before database work. Existing target existence/branch checks and command admission remain mandatory. GBR003 trimming now applies to persisted incident title/description; required/whitespace and actual 200-character title checks remain enforced. No speculative discovery-time or lifecycle rule was added.

Admission work uncovered a real unresolved contract: UC049 creates Pending Triage, while the actual disposable SQL CHECK permits only OPEN/IN_PROGRESS/RESOLVED/CLOSED/CANCELLED and the service writes OPEN. A direct read of sys.check_constraints confirmed this. Requested the explicit mapping decision; no new admission or lifecycle alias was invented. C136 linked creation remains wired behind the existing target gate. This does not certify UC049 or UC118 completion.

Direct evidence:

- Backend solution build PASS, zero warnings/errors.
- Focused incident validation/link input tests: 23 PASS, including 13 new cases for supported locations, absent/invalid identifiers, whitespace text and persistence length.
- dotnet test HotelOps.slnx succeeds without discovering tests. Explicit regression project: 783 PASS, 14 environment-gated SKIP.
- No database mutation this checkpoint. SQL activity was metadata read only. No new SQL/HTTPS/browser acceptance claim for incident creation; C136 source-participant rollback evidence is historical, not rerun here.
- Full frontend release run started on current unchanged frontend revision in session90352, output artifacts/checkpoint137-frontend-release.log. Handle confirmed running after backend checks. Result PENDING, not PASS. Do not restart it merely because a poll yields no output. Earlier full suite's failure remains recorded in C135.

Product files: IncidentService.cs, new IncidentCreationInput.cs. Tests: IncidentCreationInputTests.cs. The old IncidentMaintenance integration harness creates locationless fixtures and is not a valid acceptance run for the corrected contract; it was not executed or labeled PASS. Target admission/runtime fixture modernization remains required.

New completed UCs: 0, total remains 24/144. Next: apply the Pending Triage mapping decision, finish UC049 target admission with session/CSRF/current actor and permission checks, then the UC118 prefilled create/link UI and real HTTPS transaction tests. Continue polling the existing frontend release process while completing independent work. Goal remains active.
