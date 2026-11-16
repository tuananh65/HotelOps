# UC118 linked Incident transport repair

2026-11-16. UC118 remains PARTIAL_IMPLEMENTATION; no new completed or QA-verified UC.

The Incident creation form now binds the source Internal Request ID, branch and loaded version, requires an explicit completion requirement, and forwards the requestLink DTO to the existing transactional IncidentRequestCreation participant. Incomplete source context fails locally rather than creating an unlinked Incident. The transport preserves mandatory=false and does not retry a stale write. A linked creation URL whose action is unavailable displays that limitation and a return link instead of silently presenting only the list.

No new creation link is advertised by the source Incident panel: A004A-021 still has no accepted production contract in Dev2ActionContracts. Existing action capability admission, session, CSRF and service authorization remain unchanged. This source repair does not enable that action or establish UC049/UC118 end-to-end availability.

Direct evidence: frontend TypeScript and Vite build PASS; three focused transport checks PASS (source binding/CSRF, invalid binding rejection, stale-write no retry). Existing Vite React Router directive and large-chunk warnings remain; the test loader emits a Node module.register deprecation warning. Backend unchanged; backend build and runtime API/SQL checks not run in this batch. No database writes, schema changes or grants.

Next: finish UC049 lifecycle mapping, evidence/duplicate-review/notification requirements and trusted target admission, then expose the source-to-Incident creation link. Keep UC120 legacy mandatory classification separate and pending its business decision. Source-complete count remains 41/144, with 27 historical QA-verified UCs; these totals are not new runtime verification.
