# FASTTRACK-142A checkpoint136 — linked Incident creation transaction

TASK_ID=DEV3-FASTTRACK-142A
BASE_HEAD=a19080db74dc93dc2c3b61ed796f86b6069b82f2
NEW_COMPLETE_VERIFIED=[]
SHARED_DB_MUTATION=NO

UC118/SRS13.5 requires Create New to use the target command's own permissions and save the request link and audit in the same transaction. Added optional InternalRequestId + RequestLink (canonical eight-byte Version and explicitly supplied Mandatory) to the existing CreateIncident input. Ordinary incident creation remains compatible. The existing admitted IncidentService command now prepares the source binding before asset/number allocation, then writes the explicit incident link and source audit before its existing final authorization/commit.

The internal transaction participant requires Serializable isolation, the installed request/version/link capabilities, current IR_VIEW, exact receiving-department DEPT_MANAGER/IR_PROCESS mapping, matching branch, Accepted/In Progress and the loaded request version. It retains User -> request -> target lock order. The final source check and existing target command revalidation protect commit. Source status is preserved; successful linking advances its row_version. The existing incident-link table is reused. No generated entity, new migration, permission relaxation or shared schema mutation.

The target command admission is still gated by existing Dev2 bindings for the old pinned demo environment. This change does not manufacture a grant or remove that gate. No new public creation endpoint or UI bypass was added. Therefore this checkpoint establishes the wired transaction participant, not a completed Create New Incident UI/HTTPS workflow. A valid independently verified target admission and prefilled UI remain required before UC118 can be completed.

Direct evidence:

- checkpoint136-sql.txt: one opt-in real SQL test PASS. Exact server/database/GUID/run and empty-fixture guards precede mutation. Existing incident-link installer executes twice inside the outer transaction. The test checks foreign branch, stale version, revoked mapping, revoked role, Closed state, persisted mandatory link/audit, unchanged request lifecycle and version advance. Savepoint rollback removes the synthetic incident, link and audit and restores the source version. Outer rollback and subsequent empty users/branches/requests assertions PASS.
- SQL scope is the actual source transaction participant with a synthetic newly inserted incident. It does not execute the public IncidentService authorization pipeline, browser, concurrent writers or injected commit failure; those remain outstanding.
- checkpoint136-unit.txt: 10 PASS, covering paired source/link input, canonical version, explicit Mandatory JSON and inability to enter IncidentService without its target command grant.
- Backend solution build PASS, zero warnings/errors. Solution test succeeds without test discovery. Explicit regression project: 770 PASS, 14 environment-gated SKIP, including the separately executed SQL test's default skip.
- Dev3 NoSQL harness: 101 PASS, zero SQL connections; existing writer admission/lock-order regression. This is not SQL concurrency evidence.
- Frontend unchanged; frontend builds/tests and npm ci not rerun here. Earlier full frontend failure remains recorded in C135, not relabeled green.

Changed files: CreateIncidentRequest.cs, IncidentService.cs, IncidentsController.cs, new IncidentRequestCreation.cs; new IncidentRequestCreationTests.cs and IncidentRequestCreationSqlTests.cs; receipt/matrix/evidence. Controller only maps source HTTP failures. Business logic stays in the existing single backend project.

New completed UCs: 0; total remains 24/144 COMPLETE_VERIFIED. No complete business workflow is newly certified in this checkpoint. Next highest-impact implementation: establish the real target Incident creation admission according to UC049's actors/permissions and current policy architecture, then prefilled canonical UI -> atomic create/link -> independently authorized detail; verify concurrent requests and failure rollback through HTTPS. UC122 reroute/handler and pending handover contract remains pending separately; the sprint stays active.
