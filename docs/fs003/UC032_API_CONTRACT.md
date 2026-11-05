# UC032 — actual API/security contract

**Source verified; current SQL/HTTP acceptance not verified.** Source priority: developer FS-003 + latest no-DB-write clarification → existing Task74 conditional approval/implementation → SRS UC032/Change Notes → actual code/mappings. Historical credited UC032 is not re-credited.

## Sources

- `docs/requirements/HotelOps_SRS_DRAFT.md`, UC032 at line4520: assigned housekeeper, In Progress, cleaning items/notes/photos, 0–100, audit same transaction, 100% does not complete.
- `scripts/verification/Housekeeping/TASK74_CONTRACT_PROPOSAL.md`, `TASK74_BATCH5_IMPLEMENTATION.md`; `scripts/database/release/progress/README.md` (read only).
- `Controllers/WorkProgressController.cs`, `DTOs/Housekeeping/ProgressDtos.cs`, `DTOs/Housekeeping/InspectionWorkflowDtos.cs`.
- `Services/Housekeeping/WorkProgressService.cs`, `InspectionExecutionService.cs`; `Services/HotelStructure/RoomTypeBatchService.cs`.
- `Data/HotelOpsDbContext.cs`, `HotelOpsDbContext.Checklists.cs`, `Data/ProgressSchema.cs`, `Data/HousekeepingSchema.cs`; generated CleaningTask/Assignment/ProgressLog/Attachment entities.
- `Security/HotelOpsAntiforgery.cs`, `Middleware/ApiCsrfMiddleware.cs`, `Controllers/CsrfController.cs`, cookie/session/access services.

Paths above relative to `src/HotelOps.Api/` unless explicit. WorkProgressController has no NonController; baseline active route inventory includes GET/PUT. Recovery CleaningMonitor activation is unnecessary for known-ID progress read/save.

## Routes and DTO

GET and PUT `/api/branches/{branch:long}/cleaning-tasks/{id:long}/progress`.

GET success200: `WorkProgressDto`, ETag header, no-store. PUT success200: same DTO and new ETag. No EF entities exposed.

```typescript
// Actual camelCase JSON contract, independent of prototype RecordData.
SaveProgressRequest = {
  percent: number; // integer 0..100
  items: { id: number; done: boolean; outcome?: null; note?: string | null }[];
  note: string | null;
  photos?: { name: string; mimeType: string; base64: string }[];
};
WorkProgressDto = {
  id: number; branchId: number; kind: "cleaning"; state: "IN_PROGRESS"; percent: number;
  items: { id: number; label: string; required: boolean; order: number;
           done: boolean | null; outcome: null; note: string | null }[];
  timeline: { id: number; percent: number; status: string | null;
              blockerTypeId: number | null; note: string | null; actorId: number; at: string }[];
  evidence: { id: number; entityType: string; entityId: number; name: string;
              mimeType: string | null; bytes: number | null; uploadedAt: string; downloadPath: string }[];
};
```

Progress request/item DTOs disallow unknown JSON members. Cleaning item IDs phải thuộc frozen snapshot của đúng task; done bắt buộc non-null, outcome phải null/omitted; không gửi inspection PASS/FAIL. Tối đa100 items, unique IDs; backend cho phép subset items, không tự yêu cầu full list. Frontend gửi current list.

Global note trim≤4000; item note trim≤1000; ChecklistText từ chối control chars, kể cả newline. Frontend dùng input một dòng và không echo raw ProblemDetails/body secrets.

## Authorization and state

- HTTPS + protected HotelOps cookie ticket; validated CurrentUserAccessor/session identity, not client actor/role/branch.
- `ProgressAccess` reads SQL CLEANING_EXECUTE grant paths. Current source must resolve task→room→requested branch. `ProgressOwned` additionally requires exactly one ACTIVE assignment, ended_at NULL, assigned_at≤server UTC and assignee=current user. Business owner check applies even to broad grants; no SYS_ADMIN bypass.
- Branch exists/active. Cleaning workflow_status **and** legacy cleaning task status must be IN_PROGRESS; room active. Unknown/unadopted optional schema fails closed.
- UC032 performs **no status transition**: no start/pause/complete/rework/inspection/room-ready/time interval/notification. At100 state remains IN_PROGRESS. Operational-task PAUSED support belongs UC109, not cleaning.
- Missing source may be403 before404 with OWN grants because feature authorization runs before resource existence projection. Frontend must not infer object existence from status alone.

## CSRF

GET `/api/auth/csrf` returns `{csrfToken, headerName:"X-CSRF-TOKEN"}` bound to current cookie/session identity. Unsafe `/api` methods require that header and valid antiforgery cookie; validation may return400 before authorization. Cookie is __Host-..., Secure/HttpOnly/SameSite=Lax. Refresh after identity replacement/login/change-password.

DEV1 client bootstraps a fresh token for each user-initiated Save, keeps it only in memory for the call, includes cookies and sends the exact named header. It does not implement login, store session secrets, create fake identity, modify shared security or cache token across identities. Invalid bootstrap prevents PUT. A session change between bootstrap and PUT fails400/401; no blind retry.

## Strong ETag/concurrency

`WorkflowTag` uses **uppercase HEX**, not Base64: quote +16 HEX chars+quote for8 SQL rowversion bytes. Example shape `"0000000000000001"` is a fixture, never a usable shared revision. `WorkflowStrongTag` rejects W/, *, lists, lowercase hex, wrong length/Base64.

GET returns the authoritative tag; client echoes it byte-for-byte as If-Match. Missing header428; malformed400; valid old tag412. No UpdatedAt/date/client-generated version. `cleaning_progress_versions.row_version` queried under UPDLOCK/HOLDLOCK; source/assignment/item/evidence guards advance revision. On save parent revision is updated and response re-reads actual version. Frontend requires valid ETag and branch/task DTO before allowing edit;412 preserves draft but disables Save until explicit reload (reload discards draft).

## Actual responses

| Status | Actual contract |
|---|---|
| 200 | Read/save success, WorkProgressDto + ETag |
| 400 | HTTPS_REQUIRED; CSRF_VALIDATION_FAILED before action; binding/unknown members; VALIDATION_FAILED for invalid tag, percent, items, notes/files |
| 401 | Anonymous/invalid SQL session/cookie path; service-level SessionInvalid if reached |
| 403 | Missing grant/scope/current ownership, including some masked missing sources; restricted-session middleware can return PASSWORD_CHANGE_REQUIRED before action |
| 404 | NotFound source after gates, missing authorized evidence |
| 409 | Inactive branch/task/room or phase; Duplicate in initialization (not normal progress save) |
| 412 | Stale strong If-Match |
| 428 | IF_MATCH_REQUIRED |
| 413 | Request body >72MiB via existing filter/server limits |
| 503 | SchemaRequired, Unavailable/timeouts/locks; auth service unavailable |
| 500 | Other programming/provider/schema errors can remain generic500 in middleware; do not falsely map every SQL exception to503 |

PUT401/403 requires CSRF/session setup to reach that gate; malformedCSRF may yield400 first. No claim of live status verification in FS-003.

## Transaction and schema effects

Same HotelOpsDbContext, local SERIALIZABLE transaction owned by BatchAsync. User locks → initial session/grant gate → active Branch lock → feature catalog applock → source/owner/phase/revision checks → raw cleaning_task_items updates → CleaningProgressLog INSERT → revision advance → optional private Attachment + progress_evidence_links → before/after audit → final session/grant gate → one commit. No independent inner commit. Failure disposal rolls back SQL; files owned by failed invocation are deleted; commit ambiguity cannot be safely retried automatically.

ProgressSchema checks marker HotelOps.Progress.V1, six additive tables, actual8-byte rowversions, seven enabled triggers and nine trusted FKs. Housekeeping optional schema is also required. Generated entities do not contain all opt-in metadata; unmapped projections handle it. Existing partial mapping disables SQL OUTPUT where optional triggers require it. No schema/mapping change or package installation in FS-003.

File contract:≤5, decoded1..10MiB each, allowed JPG/PNG/PDF MIME+magic+extension, name≤200 with no path/control chars; private storage server-generated name, typed same-source linkage. Client prevalidates; server remains authoritative. API evidence URLs reauthorize source/owner. UI initial slice supports upload and shows counts; private download/gallery/timeline detail UI remains follow-up.

**Notification dependency:** UC032 save is independent (SRS lists GBR001/002/005/007/008/016/050, no GBR010 producer). Do not add no-op publisher or claim notification acceptance. UC029/034/041/044/045/046 remain separate lifecycle/notification dependencies.
