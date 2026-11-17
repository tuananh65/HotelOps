# UC068 — source completion, 2026-11-17

SOURCE_COMPLETE / NOT_TESTED. Canonical UC068 and the approved cross-branch custody/source-work decisions are implemented.

- Same-branch location form and cross-branch Submit → receiving Head Accept/Reject, or sender Cancel; current session, role/grants, both-end scope, CSRF and native asset ETag.
- Acceptance alone changes branch, location and custody; movement, handover decision, immutable source-work/schedule provenance and before/after audit share one Serializable transaction. Open work requires explicit confirmation; retries/stale versions cannot transfer twice.
- Existing Incident, Maintenance and preventive occurrence responsibility stays at its original branch. Canonical readers, reports, assignment notifications and responsibility checks preserve that scope; foreign current asset metadata is not disclosed. Existing schedules are not silently adopted by the destination or used to generate work there.
- Handover notification intent is committed in audit, delivered with existing retry/deduplication, and checked again against current full asset access. No grant is created. History remains in the profile under both-end read scope.
- AssetTransferV1.sql is additive, identity guarded and has rollback mode, immutable provenance, trusted no-cascade FKs and location guards. NOT_APPLIED; no SQL mutation or runtime verification. Historical schedules/occurrences lacking reliable origin evidence fail closed before transfer.
- Backend build: PASS, 0 warnings/errors. TypeScript: PASS. Unit/integration/E2E/browser tests: NOT_RUN by instruction. Human QA and guarded development migration activation remain outstanding.
