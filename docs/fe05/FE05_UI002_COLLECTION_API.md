# FE05-UI002 - Checklist collection read API

## Identity

Backend baseline: `6aee2a0e692a7814f41c81224709cddfbe1e2ec4`.
Worktree: `HotelOps-DEV1-FE05-COLLECTION`.
Branch: `feature/dev1-fe05-checklist-collection`.
Consumer reference: `feature/dev1-fe05-checklist-ui` (unchanged).

## Existing contract evidence

- `Controllers/InspectionChecklistsController.cs` had POST collection and GET detail only. No equivalent collection route was found in backend controllers.
- `Services/HotelStructure/ChecklistService.cs`: `ReadChecklistAsync` / `ChangeChecklist` already uses `BatchAsync(... permissionCode: "CHECKLIST_MANAGE")`; only `ChecklistScopes` matching checklist ID and exact branch are readable.
- `Services/HotelStructure/RoomTypeBatchService.cs`: `BatchAsync` validates current identity/restriction, opens a Serializable SQL transaction, locks user, checks persisted session/branch permission, validates/locks branch, takes existing catalog lock, checks RoomType schema, executes operation, then rechecks session/permission before returning success. All reused unchanged. No role grants are added.
- `Data/HotelOpsDbContext.Checklists.cs`, `Entities/ChecklistV1.cs`: nullable legacy branch ownership is preserved. Unowned legacy records never enter this branch collection.
- `Entities/InspectionChecklistVersion.cs`, existing `ChecklistDetails`: versions are stored separately; historical version IDs/status/usage must remain intact. The new query does not mutate any entity or infer a synthetic aggregate status.
- Existing list convention: `ListRoomTypesAsync` validates search/pagination and returns items,total,page,pageSize plus filters. The new DTO follows this envelope with checklist-specific version summaries.
- SRS UC039/040 supports opening checklists, Draft/version lifecycle and preservation of inspection history. No new role/permission assumptions are introduced from the pending matrix.

## Frontend integration contract

`GET /api/branches/{branchId:long}/inspection-checklists`

HTTPS and an existing real HotelOps cookie session are required. Uses the SAME `CHECKLIST_MANAGE` branch permission as detail reads. GET requires no new mutation CSRF contract. No request body or If-Match is needed. Response is no-store; the collection has no aggregate ETag. Fetch detail before editing to obtain its current strong ETag.

| Query | Default / validation | Meaning |
| --- | --- | --- |
| page | 1; positive; overflow guarded | 1-based page |
| pageSize | 20; 1..100 | Maximum checklist roots per page |
| search | optional; trimmed; max 200; no control characters | Code/name Contains, using existing DB collation |
| isActive | optional boolean | Root active flag; omitted includes both |
| status | optional DRAFT/PUBLISHED/RETIRED | ANY stored version matches, including history |
| roomTypeId | optional positive long | Existing applicability bridge contains type |

Stable order: code then checklist ID ascending. Count is after all filters and before pagination. Empty and out-of-range pages return 200 with an empty items array; total still reflects the filtered count. No global branch fallback, visibility inferred from room types, or legacy backfill occurs. IDs remain backend long values; consumers should retain existing safe ID handling.

Example envelope (abbreviated fixture, not a live receipt):

```json
{
  "items": [{
    "id": 42, "branchId": 7, "code": "CL1", "name": "Room checklist", "active": true,
    "roomTypeIds": [11],
    "versions": [{ "id": 81, "number": 1, "status": "PUBLISHED", "effectiveAt": null,
      "publishedBy": null, "usedByInspection": true }]
  }],
  "total": 1, "page": 1, "pageSize": 20, "branchId": 7,
  "isActive": null, "search": null, "status": null, "roomTypeId": null
}
```

All stored versions are returned in number/ID order, even when filtering by one status. The response intentionally omits criteria/content and does not carry an edit authority or ETag. Do not reuse the UI001 detail DTO validator for this summary response. Opening a list item should call the existing detail GET; historical content remains read-only there.

Errors reuse existing ProblemDetails mapping: 400 invalid query/HTTPS, 401 missing/invalid/restricted session, 403 missing branch permission, 404 authorized missing branch, 409 inactive branch, 503 schema/dependency/permission-store/lock unavailable. Invalid model binding is handled by existing ApiController behavior. No new permission or error framework.

## Read/concurrency behavior

All reads use the existing DbContext, async EF, CancellationToken and AsNoTracking. Count and split page queries execute within the same existing Serializable transaction and catalog-lock boundary. Split queries avoid a RoomType-by-version cartesian result. Parent paging has a deterministic unique tiebreaker. No criteria, inspection, historical version, SQL script, mapping, schema, notification or D4 changes occur. Reads acquire the existing locks; high contention can still return existing Unavailable behavior. This task does not claim deadlock freedom or introduce retries.

## Verification and limitations

Actual receipts (2026-11-08):

- `dotnet build HotelOps.slnx --ignore-failed-sources -p:NuGetAudit=false`: PASS, 0 warnings, 0 errors (1m25.87s). NuGet advisory audit was disabled for this cache-based verification; this is not a security audit receipt.
- `dotnet test tests/HotelOps.Checklist.CollectionTests/HotelOps.Checklist.CollectionTests.csproj --no-restore --logger "console;verbosity=normal"`: 28/28 PASS (33.57s). Test project compilation emitted no warnings/errors.
- `dotnet test HotelOps.slnx --no-build --no-restore`: exit 0; the baseline solution has no test projects, so this command contributes no additional assertions.

- Focused xUnit tests exercise query composition using in-memory fixtures, SQL Server translation via ToQueryString (no execution), HTTPS/cookie guards, and existing denied/unavailable/expired-session gates.
- Authorization gate tests use a test-only in-memory ADO connection that can answer ONLY the existing user-lock SELECT. It has no server address, cannot execute a collection query, and rejects mutations. Permission/session test doubles are not SQL grant verification.
- Positive list/history/filter/page assertions are fixture-level query tests, not database integration receipts.
- SQL query execution, real grant/session revocation races, live HTTP hosting/model binding and live frontend E2E: NOT_RUN. No isolated backend runtime/database is configured for this task. No shared SQL is executed.
- Historical checklist assertions are not rerun: no existing write/detail logic changed. The solution contains only the API project, so focused test project must also be invoked explicitly.

Frontend UI001 source is deliberately unchanged. A subsequent authorized frontend task can consume this collection instead of its session-loaded list; shared real authentication/HTTPS transport and RoomType selector permission dependencies remain.

No commit, push, merge, schema deployment or UC039/040 acceptance is performed by this delivery.