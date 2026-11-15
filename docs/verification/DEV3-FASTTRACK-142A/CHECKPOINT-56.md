# FASTTRACK-142A checkpoint 56: assigned inspector notification

New COMPLETE_VERIFIED UCs: **0**. Total remains **8 COMPLETE_VERIFIED / 136 PARTIAL**. This closes UC041's missing reinspection assignment notification delivery/source path; it does not certify initial inspection assignment, reassignment, or all published-checklist compatibility.

## Business change

The registered UC041 writer already committed a REINSPECTION_ASSIGNED intent in its protected assignment receipt/audit, but the notification worker did not discover or deliver it. The worker now discovers that event and uses a fresh serializable delivery transaction. Source verification requires the registered writer digest, immutable binding, exact operation receipt and audit identity, current assigned inspector and matching room/rework/cycle identities. Current branch HK_SUPERVISOR eligibility and INSPECTION_VIEW are checked before delivery and again before source resolution. The assigning actor and assigned recipient are not conflated.

One delivered in-app recipient is persisted per event. Faults roll back the inbox write while retaining the committed assignment intent; retry rechecks authority. The inbox offers Open inspection and accepts only the canonical validated `/reinspections/{branch}/{inspection}/execution` path. Stored ActionUrl is ignored. The existing execution endpoint independently authorizes current results/evidence and mutations. After Passed/Failed, the same original assignment event can open the assigned inspector's terminal view, without treating historical readiness as current room-use authority.

SRS UC040 explicitly says Retired versions cannot be used for new inspections. GBR-056 and UC041 require Published. Therefore the existing pre-assignment guard is retained; no Retired admission or label-based defect remapping is invented. A different published version needs a separate compatibility implementation. Retirement after an already valid assignment remains supported and is exercised here.

## Direct validation

- Backend solution build and ModelProbe build: PASS, **0 compiler warnings/errors**. Actual backend regression: **542 PASS / 11 SKIP / 0 FAIL**. Ten new parser checks cover distinct assigner/recipient, malformed payloads and substituted contracts/states. `dotnet test HotelOps.slnx` succeeds but the solution itself contains no test project.
- Frontend typecheck and production build: PASS. Focused Auth/Router, assignment client, execution route and inbox-source tests: **61 PASS / 0 FAIL**. Full frontend suite and npm ci not rerun; lockfile unchanged.
- `checkpoint56-assignment-notice-pass.json`: PASS for normal and blocked rooms. Actual canonical browser assignment; assignment discovery; malformed/substituted audit and disabled-writer denial; branch revocation; injected recipient-write fault with no leaked notice; concurrent/repeated delivery with exactly one recipient; role loss, undelivered receipt and audit tamper source denial; malicious stored URL ignored. Browser inbox → Assigned execution → reload succeeds. The owned checklist is then retired and a different inspector from the original concludes Passed through the real browser. Inbox → terminal execution → reload succeeds. Per-case cleanup PASS.
- `checkpoint56-assignment-notice-failed.json`: PASS for normal and blocked rooms, including the same assignment delivery/source negatives and inbox while Assigned. After retirement of the already assigned checklist, browser conclusion is Failed and the next Pending Acceptance rework is committed with its assignee inbox/source. The original assigned inspector can then open/reload the Failed terminal inspection from the assignment notification. Per-case cleanup PASS. This does not run that next round all the way to Passed.
- `checkpoint56-cleanup-final.txt`: independent exact server/database/GUID/run check; zero synthetic users/branches/reworks/inspections/checklist versions/items/attachments/intervals/notifications/blocks/incidents and private files. Trial bindings/writers/principals/fault constraints absent; original projection triggers enabled and rework CHECK enabled/trusted. All owned API/probe processes stopped.

The new fixture initially emitted EF1002 for numeric DDL interpolation. It now passes the bound integer through a SQL variable before constructing the owned fault CHECK; rebuild has zero warnings. Existing EF unmapped-index/row-limit warnings, Vite use-client and 676.68 kB chunk warnings, and esbuild import.meta/IIFE test warning remain. No dependency downgrade or security relaxation.

## Runtime and remaining lifecycle gaps

Only the exact owned disposable database is used; no shared database mutation. The new feature uses existing notification tables and the existing protected assignment receipt, with no generated EF entity, migration or production schema change. Current runtime still needs the approved protected workflow installation. Delivery is explicitly invoked in the fixture; hosted scheduler timing is not claimed.

Next highest-impact work: complete the next Failed rework round through Passed; finish UC041 initial assignment/reassignment acceptance; continue FE11/FE13 and UC006/010 lifecycle completion. Whole UC041, UC048 and FE05 remain PARTIAL. The sprint goal stays active.

Canonical branch: `integration/dev1-dev2-dev3-unblock-20261114`. Initial local/remote SHA: `79fa9d52b8e634792b3a02cbc3a3cde285246570`. Product changes: ReinspectionAssignedNotificationSource/Delivery, existing worker dispatcher/source resolver, inbox client/UI. Verification changes: parser tests, client tests, composed probe hook/cleanup and browser acceptance. Decision56 records the SRS boundary and authorization contract. Commit/push follows final runtime cleanup and changed-path review.
