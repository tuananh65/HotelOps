# UC123 approved Sent/Received contract: follow-up

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=915aa417bbbe7cf27a3ba2e1e7f538ecea9cb227
NEW_COMPLETE_VERIFIED=0
SHARED_DB_MUTATION=NO

The approved direction behavior is already implemented in checkpoint 107: Sent uses RequesterId; Received uses the unique current handler or an eligible DEPT_MANAGER with IR_PROCESS and explicit current receiving-department mapping. Both are applied to the independently IR_VIEW-authorized query before counting and pagination. No new authority is inferred from a user's home department.

This follow-up adds regression cases proving that both directions preserve the read-scope intersection (including inactive branches and revoked read access) and that future, replaced and ended handler assignments do not qualify. The tests exercise query composition in memory; they do not substitute for SQL authorization verification.

The existing unfinished UC114 service caused CS4007 during the first build: SequenceEqual selected a span overload whose receiver crossed an await. Awaiting the current recipient array first repairs compilation while preserving the exact recipient-scope comparison. Other pre-existing UC114 work remains uncommitted and unfinished; no intake endpoint or completed intake workflow is claimed.

Direct execution in this follow-up:

- Initial source build failed with CS4007; repaired.
- Restore/build initially could not read local NuGet.Config under the sandbox; the authorized retry succeeded.
- `dotnet build HotelOps.slnx`: PASS, zero warnings/errors.
- `dotnet test HotelOps.slnx --no-restore`: exit 0, no test project discovered; not counted as test coverage.
- Actual regression project, filter InternalRequest or IncidentReadAccess: 67 passed, zero failed/skipped.
- Frontend typecheck and production build: PASS. Existing React Router use-client and bundle-size warnings remain.
- Frontend history suites: 28 passed. The newly added focused browser direction test then passed separately (1 passed): accessible Sent/Received selection sends GET with page=1, and a denied refresh removes private history. These browser tests mock HTTP responses; they are not a new HTTPS-to-SQL trial.
- No SQL mutation or new HTTPS-to-SQL trial was run in this follow-up. Checkpoint 107 trial3 is historical evidence for cross-branch exclusion, mapping/grant revocation, routing, handler replacement and browser direction selection; its separate cleanup receipt confirms clone removal.

UC123 remains PARTIAL because linked material-request visibility is unresolved. No new UC is marked COMPLETE_VERIFIED. Highest-impact next work remains UC114 intake completion and FE13 lifecycle completion; material visibility must use its own approved target authorization.

Files changed in this follow-up: InternalRequestIntakeService.cs (compile fix within existing untracked work), InternalRequestDirectionTests.cs, frontend/tests/internal-request-history-browser.test.mjs, and this receipt. Earlier intake policy/access/SQL/evidence work was preserved. No commit or push was performed in this follow-up.
