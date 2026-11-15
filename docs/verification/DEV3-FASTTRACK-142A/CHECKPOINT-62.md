# Checkpoint 62 — open initial-inspection reassignment

`TASK_ID=DEV3-FASTTRACK-142A`; ACTIVE, original deadline unchanged. Continuation of C61, not a new audit.

**Newly completed UCs: 0. Totals remain 10 COMPLETE_VERIFIED / 134 PARTIAL / 144.** UC041 gains a verified initial-inspection reassignment workflow but still lacks equivalent reinspection transfer and full exception acceptance.

## Implemented and directly verified

The existing assignment page now offers Change inspector when an open initial inspection exists. The supervisor selects an eligible replacement, reviews the change, and confirms it. A separate options read supplies current inspector/cycle ETags. CSRF, session generation, source validation, uncertain-outcome locking and explicit refresh remain enforced. The writer changes only the assigned inspector, retaining checklist version, deadline, workflow and entered results. Terminal/rework paths remain closed to this command.

A serializable protected writer records the new inspector, previous inspector, reason, operation receipt and audit/notification intent together. Same-operation replay returns the committed result once, after current authorization; conflicting or stale requests do not mutate. Notification events use their audit ID, so a second assignment does not reuse the first event's delivery receipt. Current scope and assigned-inspector identity are checked again at delivery and source opening. See decision62.

`checkpoint62-reassignment-final.json` is direct browser/HTTPS/disposable SQL evidence:

- Existing initial assignment and execution: 15 grouped service checks, board → queue → assignment, canonical inbox → checklist execution → Pending Conclusion.
- New reassignment: 12 grouped SQL checks including stale inspection/cycle, wrong branch, revoked role, inactive inspector, cleaner conflict, forced audit rollback, concurrent replay and old-inspector denial. ASSIGNED and INSPECTING retain the assigned retired checklist version and deadline.
- Real UI round trip in ASSIGNED and handoff in PENDING_CONCLUSION; missing CSRF400, missing ETag428, stale412, ineligible inspector400, unchanged source on denial.
- Previous inspector gets403 on execution. A distinct real user logs in, opens the new assignment notification and reads Pending Conclusion. SQL still has one inspection, its saved PASS result and three execution audits.
- Notification proof tampering, disabled writer, revoked recipient branch/role, forced delivery rollback, concurrent deduplication, source reauthorization and stored-URL rejection execute within the probe. These use actual committed SQL receipts, not mocked responses.
- Identity/schema validation, seed/cleanup rollback rehearsal and independent final cleanup pass. No shared database mutation.

## Validation and observed failures

Backend build PASS, zero warnings/errors. Actual regression project: 565 PASS, 11 SKIP, zero FAIL. `dotnet test HotelOps.slnx --no-restore` exits0 but contains no test project; it is not test coverage. Frontend typecheck and production build PASS (698.19kB bundle). Focused inspection client/route tests: 57 PASS. Auth/Router and canonical shared-host tests: 284 PASS, zero FAIL/SKIP; completed log retained. npm ci and the full frontend suite were not rerun; lockfile is unchanged.

The first backend build failed on a missing Data namespace import in the new service; fixed before runtime testing. The first composed handoff trial and diagnostic rerun timed out selecting the new inspector field: the wrapped label's accessible name included select-option text. The API returned eligible options correctly. Giving the select an explicit associated label fixed the accessibility issue; the final composed trial passed. Both failed trials cleaned up successfully and are not counted as PASS.

Warnings remain visible: runtime EF unmapped RoomTypeBranchV1 indexes and unordered row-limiting; Vite React Router use-client and >500kB bundle notices. No dependency downgrade or security bypass.

## Runtime boundaries and next work

All mutations used synthetic fixtures on verified `meita / HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, ownership marker `cc78c13ffc124efcad67b3d804baa84c`. The temporary writer reuses existing columns/operation storage and is removed after the trial. No persistent business schema, EF entity, migration or permission-catalog change. Production/shared capability is not provisioned; the feature fails closed there. Hosted notification scheduling is not claimed.

Independent `checkpoint62-cleanup-final.txt` confirms zero users/branches/inspections/results/audits/operations/notifications, absent initial/reassignment writers and issuer registry, no trial principals/fault constraints, and original guards intact. Private evidence roots are empty and owned ports7142/7143 have no listeners.

Next highest-impact work: complete reassignment for the exact submitted-rework/reinspection binding without weakening UC047 authority; then finish UC041's acceptance ledger and UC048 history. Continue FE11/13 and UC006/010 lifecycle priorities afterward. These are implementation gaps, not approval blockers.

## Git delivery

Initial local/remote baseline `abfc4b20e68e57d6b76183af81b121316fe48577`, branch `integration/dev1-dev2-dev3-unblock-20261114`. Authorized normal commit/push follows final branch, changed-path, remote and validation checks. Changes are confined to UC041 service/DTO/controller, assignment form/client, notification source/delivery, targeted tests, disposable probe and this evidence/decision log. Existing dirty work was preserved (initial working tree was clean).
