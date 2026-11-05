# TASK72 — Accelerated Batch 3: implementation and acceptance

Ngày 31/10/2026, DEV1, branch `feature/BE1-database-integration`.
**Final: 4 COMPLETE_VERIFIED_V1 UCs.** Developer đã duyệt có điều kiện checklist contract
sau báo cáo hai UC ban đầu; tài liệu này mô tả kết quả cuối, thay thế classification
BLOCKED của UC039/040 trong báo cáo ban đầu. UC016 vẫn PARTIAL, UC006 không thay đổi.

## A. Selected UCs và trọng số

| UC | Exact SRS title | Điểm | Final classification |
|---|---|---:|---|
| UC038 | View Room Turnover History | 2 | COMPLETE_VERIFIED_V1 |
| UC039 | Create Room Inspection Checklist | 2 | COMPLETE_VERIFIED_V1 |
| UC040 | Update Room Inspection Checklist | 3 | COMPLETE_VERIFIED_V1 |
| UC048 | View Room Inspection History | 2 | COMPLETE_VERIFIED_V1 |

Đối chiếu AGENTS, SRS §4.11/5.1/5.2/5.10/Permission Matrix, GBR003/005/007/008/015/
016/056/057, SRS_CHANGE_NOTES, delivery plan, Task69/70/71 và entities/DbContext/RoomType/
Room/Structure code. Không chọn UC006 hoặc UC đã complete; không có bằng chứng mới giải
quyết UC016. Approval và proposal: [contract decision](TASK72_CHECKLIST_CONTRACT_PROPOSAL.md).

## B. Implementation và ASSUMED_V1

Controllers chỉ HTTP, DTO không expose EF entities, async/CancellationToken/AsNoTracking.
Mọi thao tác dùng SQL session/current actor/permission/branch membership; không role-name
bypass. Tái sử dụng SERIALIZABLE, retained User-first lock, branch lock, catalog applock
và final SQL session/permission gate của RoomTypeCreationService. Multi-table writes và
audit trong cùng transaction. Module schema gate chỉ kiểm tra, không startup migration.

UC038: count/page/date/result/newest cycles, assignee history, start/end, latest consistent
inspection result, rework count, recorded cancellation reason; full detail có progress/
blocker, versioned inspections/results/defects/reworks/attachments. UC048: branch/room/date/
result/inspector/historical-housekeeper/search, newest stable paging, full historical
criteria (including inactive/unanswered), defects, rework tasks, related inspections.
Không đổi historical version sang checklist hiện hành; cross-room/cross-version/location
mismatch bị exclude. POST-01: read không đổi business state hoặc tạo audit giả.

UC039: create multi-RoomType same-branch checklist/version1 Draft với text/group/mandatory/
pass-condition; publish effective version với audit. UC040: read current versions + actual
inspection-use flag, edit Draft hoặc tạo next Draft, publish atomically retire previous,
explicit confirmed retirement. Published/used criteria và version content/identity immutable;
assigned inspections giữ FK cũ. Strong If-Match dựa **actual SQL rowversion** + aggregate/
audit revision; mỗi API aggregate write tăng root rowversion trong cùng transaction.

Approval cho phép disposable migration/mapping/index/FK/consumer/deployment integration.
SRS có `group` và GBR005 có `row_version`, nên migration thêm nullable criterion_group
nvarchar(100) và generated root rowversion; không dùng Description giả lập group hoặc
state hash thay rowversion. Legacy ownership/content giữ nguyên, không business backfill.
Controlled partial read projections thay vì sửa generated entities. Sáu SQL triggers bảo
vệ scope, applicability, immutable content/version, new inspection binding và default
RoomType selection. Trigger mappings tắt bare OUTPUT; UC017 dùng SCOPE_IDENTITY trên cùng
connection/transaction. Legacy generic checklist UC017 vẫn hoạt động; UC018 nhận scoped
default chỉ nếu cùng branch/applicable/effective Published/active criteria. Không tự đổi
RoomType defaults khi publish/retire, không tự mở rộng applicability.

ASSUMED_V1 khi SRS không định nghĩa contract chi tiết:

- UC048 existing INSPECTION_VIEW; UC038 CLEANING_VIEW + INSPECTION_VIEW cùng branch vì
  payload kết hợp cleaning và inspection. OM/HK_SUPERVISOR canonical có hai branch grants.
  Không suy diễn OWN thành BRANCH. Checklist dùng existing CHECKLIST_MANAGE/BRANCH:
  **migration không cấp HK_SUPERVISOR quyền tự động**. Harness explicitly tạo/gỡ grant chỉ
  trên trial; production activation cần SQL grant/assignment/membership tường minh.
- Group optional free text≤100; mandatory flag phải được gửi rõ true/false, text không rỗng.
  Checklist code≤40 globally unique theo existing schema; name≤200, group/description/
  pass-condition trim/control validation; 1–200 criteria, 1–100 distinct RoomTypes active
  cùng branch. Limits là V1 input bounds, không invented status/permission values.
- UC040 edit criteria theo SRS; code/name/ownership/applicability không thay qua edit API.
  Applicability immutable sau lần publish đầu. Draft/publication/retirement reasons required;
  effective publish time do server đặt now. Không sửa trực tiếp published/retired content.
- No branch parameter ở history: chỉ active branches có SQL grant. Inactive room vẫn đọc
  lịch sử; inactive branch không mở policy mới. Foreign explicit branch403; missing/foreign
  resource trong authorized branch404. Không SYS_ADMIN history/checklist implicit grant.
- Inspection chronology InspectedAt/ScheduledAt/CreatedAt; cycle ActualStartAt/ScheduledStartAt/
  CreatedAt, ID tie-break; inclusive UTC filter, page default20/max100. Turnover unsupported
  filters400. Reinspection chain dùng actual same-room CleaningTaskId, không invent
  rework→reinspection FK. Cancellation reason chỉ recorded audit cleaning_tasks/CANCEL/
  newValues.reason, thiếu thìnull. Missing historical result không suy diễn PASS/FAIL.
- Attachments dùng exact existing CHECK discriminator INSPECTION/CLEANING_TASK; defect photos
  thuộc inspection source, không invent discriminator defect/rework. Source-authorized
  private local storage `Evidence:PrivateRoot`, default ContentRoot/App_Data/evidence;
  không fetch FileUrl. Kiểm tra relative key/traversal/reparse-point, ≤10MiB, declared size,
  JPG/PNG/PDF magic/MIME. Missing/deleted/unrelated/unreadable file404; attachment/no-store/
  nosniff; không expose StorageKey/FileUrl. Trusted private root do operator quản lý.

## C. Production API endpoints

Tất cả authenticated HTTPS/no-store. Cookie session + SQL authorization; writes dùng CSRF.

| Method | Route | Hành vi |
|---|---|---|
| GET | /api/inspection-history | Scoped filter/page |
| GET | /api/inspection-history/{id} | Exact historical inspection detail |
| GET | /api/inspection-history/{id}/evidence/{attachmentId} | Private source download |
| GET | /api/rooms/{roomId}/turnover-history | Room cycles/filter/page |
| GET | /api/rooms/{roomId}/turnover-history/{id} | Full cycle timeline |
| GET | /api/rooms/{roomId}/turnover-history/{id}/evidence/{attachmentId} | Private source download |
| POST | /api/branches/{branchId}/inspection-checklists | Create version1 Draft,201+ETag |
| GET | /api/branches/{branchId}/inspection-checklists/{id} | Versioned edit detail,200+ETag |
| PUT | /api/branches/{branchId}/inspection-checklists/{id} | Save/new Draft,If-Match |
| POST | /api/branches/{branchId}/inspection-checklists/{id}/publish | Publish/replace,If-Match |
| POST | /api/branches/{branchId}/inspection-checklists/{id}/retire | Confirmed retirement,If-Match |

Checklist errors400 invalid/reference,401 session,403 permission/scope,404 missing/legacy
writer target,409 duplicate/state,412 stale,428 missing If-Match,503 schema/gate/contention.
API DTO definitions are in ChecklistDtos/OperationalHistoryDtos. No EF entity payloads.

## D. Per-UC acceptance

| Acceptance | UC038/048 | UC039 | UC040 |
|---|---|---|---|
| Success/persistence | Real SQL cycles/criteria/assignments/blockers/results/reworks/evidence | Draft1/group/multi-types + effective publish | Draft edit/new version, replace/retire, existing inspection FK retained |
| Unauthorized/forbidden | Anonymous401, no grant403, SYS_ADMIN403 | CSRF-valid anonymous401; denied/admin403; HK before SQL grant403 | Denied PUT/retire403; revoked existing cookie403 |
| Cross-branch/missing | Explicit branch403; hidden/missing resource404 | Foreign branch403; foreign/missing/inactive type400; missing detail404 | Foreign branch403; missing update404; legacy-owned write404 |
| Invalid/business rules | UTC/range/result/page/IDs/unsupported filters400; empty filters echo | Empty/textless/missing mandatory criteria, invalid/duplicate type/code; CSRF400 | Empty criteria, missing/invalid/stale tag; confirmation; inactive criteria/future publication/retired selection |
| Audit/rollback | Reads preserve state/audit | Fault create/publish leaves no partial root/bridge/version/items/audit | Fault save/republish/retire preserves content/status/revision/audit |
| Concurrency | DMV retained grant ordering; actual HTTP cancellation/retry | Concurrent duplicate create201/409; retained revocation; canceled HTTP | Concurrent same-version update200/412; publish200/412; retained revocation/cancellation |
| History compatibility | Exact version, private evidence and group after extension | Multiple checklists for one RoomType permitted | Old version criteria/group unchanged; new inspection selects latest effective Published |

Final checklist suite **282 PASS assertions**, shared history suite **208 PASS** after
extension. Đây là combined assertions với shared bootstrap/security checks, không phải
282 độc lập scenarios cho mỗi checklist UC. Không count failed/intermediate runs.

## E. SQL/HTTPS/deployment evidence

- [task72-checklist-final.local.json](task72-checklist-final.local.json):282 PASS, actual
  root Program.cs/Kestrel/pinned ephemeral HTTPS, real credential login/cookie/SQL session/
  SQL grant, migrations/rollback receipts listed under deployments. Target is guarded
  `HotelOps_Canonical_20261031_073941_c75cc518_Fresh`.
- [task72-checklist-history-regression.local.json](task72-checklist-history-regression.local.json):
  208 PASS on installed checklist module. Original legacy-schema history208 receipt
  `task72-history-final.local.json` retained; not double-counted in final490.
- `obj/task72-checklist-history-canonical.local.log` and canonical evidence for run in
  `obj/task72-checklist-history-run.local.txt`: explicit -Checklists includes UC017,
  Fresh/Upgrade/AuthUpgrade integrity/schema comparison PASS, catalog grants unchanged.
  Earlier successful canonical3 also records package fresh/upgrade comparison.
- Populated legacy upgrade is separately exercised: create legacy checklist/version/item
  before module, verify ownership remainsNULL/content unchanged, rollback empty extension,
  reapply. This is genuine legacy-data upgrade even though the safety-guarded DB name ends
  Fresh. Do not mistake empty canonical Upgrade fixture for populated business data.
- Genuine post-column DDL CREATE_INDEX fault rolls all extension DDL/marker back; retry
  succeeds. Apply replay passes. Disabled trigger Verify/API fail closed; restoring the
  original trigger enables verification. Populated rollback refuses without deleting data.
- Runner pins reviewed server and exact disposable-name regex + marker; transaction/
  applock, source hashes, strict package hash/trigger definitions, trusted FKs/indexes,
  unique unused receipts. No startup DDL or shared/production deployment.

Deployment usage/recovery/mapping contract: [checklist README](../../database/release/checklists/README.md).

## F. Concurrency và atomic rollback

Real DMV waits prove holder→API and API→compliant UserEligibilityLock revoker ordering
for create and update. Revoker cannot remove SQL grant before feature transaction ends;
next existing-cookie request403. SERIALIZABLE source reads + final gate retained. Actual
HTTP canceled during SQL/applock wait: transaction locks disappear before blocker rollback,
no aggregate/audit/revision mutation. History cancellation/retry verified separately.

UC040 ETag includes actual SQL rowversion. A same-content external SQL update changes
rowversion without logical payload/audit change; old If-Match412 proves GBR005 protection.
Concurrent update/publication commit once, stale loser412. Concurrent duplicate create
has one201/one409, one persisted row. SQL triggers refuse published criterion UPDATE/DELETE,
metadata/ownership changes, cross-branch bridge and retired-version new inspection.

Audit failures injected after INSERT/UPDATE prove root/bridge/criteria/version/publication/
previous retirement/audit all rollback together; retries use unchanged revision. Publication
and retirement preserve historical assigned FKs. Existing bindings can progress, new
bindings require applicable effective Published version with active criteria. Catalog
applock serializes with RoomType batch operations; bounded contention can return503.

## G. Shared regression, build, framework tests

| Evidence | Result |
|---|---:|
| task72-checklist-batch-regression.local.json — UC017 deployment + UC018/019/142/020/021/024/025 with checklist module enabled before batch cases | 567 PASS |
| task72-checklist-history-regression.local.json — UC038/048 after module | 208 PASS |
| task72-structure-regression-final.local.json — UC013/014/015, first Task72 phase | 590 PASS |
| task72-rooms-regression-final.local.json — UC023, first Task72 phase | 229 PASS |
| PermissionService/Authorization/ResourceAuthorization/AuthorizationHardening, first phase | 43/61/49/21 PASS |

First phase already ran full targeted regression; continuation reran changed RoomType/
checklist/history paths once. Unchanged structure/room/auth suites retain their first-phase
evidence; no duplicate unrelated full regression. Foundation174 uses documented doubles,
separate from real HTTPS/SQL evidence. Final new history+checklist acceptance490; final
consumer batch567. Do not sum repeated old/new history/batch runs as unique scenarios.

`dotnet build HotelOps.slnx` **PASS**, verification project build **PASS**. Only NU1900
warnings (unreachable NuGet vulnerability feed); no successful vulnerability scan claimed.
`dotnet test HotelOps.slnx --no-restore`: no test projects discovered, framework tests
**NOT RUN/unavailable**. Solution contains only API; console suites above actually ran.
`git diff --check` plus untracked-source whitespace/hash checks PASS in final validator.

Failures kept, not hidden/counted: early history fixture/discriminator/DTO query translation;
checklist anonymous POST fixture initially lacked CSRF (400 before auth), corrected to real
CSRF bootstrap; initial AuthUpgrade SQL name guard omitted that suffix, corrected with exact
outer regex retained; INSERT trigger exposed UC017 bare OUTPUT issue, fixed with same-scope
identity retrieval; fixture Span/await compile error corrected. Intermediate249 checklist
checks superseded by final282. Existing early trial-marker/Fresh-only guards remained intact.

## H. Blockers and limits

UC039/040 approval blocker resolved by user's conditional authorization. Existing permission
code and BRANCH scope verified; no automatic production HK grant. All implemented schema/
consumer work stays DEV1/approved shared scope, no DEV2 source edits. No required blocker
remains for these four backend V1 flows. UC012/022/026 still need separate lifecycle/DEV2
coordination; UC016 remains PARTIAL; UC006 release gate untouched.

No UI, future inspection assignment orchestration, evidence upload/object-store integration,
shared/production deployment, or arbitrary external writer certification claimed. Private
root is trusted/operator-controlled. Existing RoomType defaults are retained on checklist
retirement; authorized replacement is required before eligible new selection. Legacy
checklists remain readable via existing consumers and cannot be silently claimed by the
new scoped writer. Legacy data is not mass-converted.

## I. Classification and changed files

UC038/039/040/048: **COMPLETE_VERIFIED_V1**, subject to explicit V1 contracts above.
Implementation completed, build passed and actual HTTPS/SQL acceptance passed are distinct
evidence, all satisfied here. No fractional UC credit for migration/test infrastructure.

Created production sources: InspectionChecklistsController, ChecklistDtos, ChecklistService,
ChecklistV1 opt-in projections, HotelOpsDbContext.Checklists partial, ChecklistSchema;
phase1 OperationalHistoryController/DTOs/Service/Files. Modified controlled RoomTypes mapping,
RoomTypeCreationService (eligibility/identity), RoomTypeBatchService (permission/eligibility),
history DTO/service (exact historical group), verification Program. Added Task72ChecklistCases,
Task72HistoryCases, checklist SQL/runner/README; optional canonical switch and report/plan/
proposal updates. Generated entity files, auth core and DEV2 source unchanged.

Final exact created/modified source paths and hashes: `obj/task72-checklist-validation.local.json`.
Original608-file snapshot and continuation652-file snapshot both retained; validator checks
unrelated hashes, backups, HEAD/index and four worktrees. No commit/push/merge/rebase/reset/clean,
no HotelOpsDB access, no shared/production SQL writes.

## J. Updated progress

Task71 **18/75,41/256=16.02%** → final Task72 **22/75 (29.33% UC),50/256=19.53% weighted**.
Gain **4 UC/9 points/+3.52 percentage points**. Remaining **53 UC/206 points**.
Taxonomy22 COMPLETE_VERIFIED(including V1),5 PARTIAL,29 NOT_IMPLEMENTED,19 BLOCKED_EXTERNAL=75.
Only COMPLETE_VERIFIED(_V1) counted; UC016 PARTIAL and denominator75/256 unchanged. Plan §20
updated; intermediate20/75,45/256 snapshot is superseded, not a second credit increment.

## K. Elapsed time

Original recorded preflight `2026-10-31T00:00:35.4104636Z`; original phase20m24s receipt
`obj/task72-validation.local.json` retained. Approved continuation snapshot timestamp
`2026-10-31T00:24:30.5634840Z`. Final end, continuation wall seconds and total Task72
preflight→final wall seconds are recorded exactly in `obj/task72-checklist-validation.local.json`.
Total includes build/SQL/API waits and approval interval, not a claim of uninterrupted
coding time. Initial reading before original preflight is excluded.

## L. Recommended Batch 4

Prioritize reviewed inspection assignment/performance contracts on effective Published
checklists, with actor eligibility/SoD/room-state/task integration settled before coding.
Resolve DEV2 branch/department/room deactivation coordination before UC012/022/026/016.
Task UC107/109 still need confirmed task statuses/notification/time/verification contracts.
Keep UC006 release gate unchanged. Do not deploy the trial module to shared/production
without a separate approved rollout and explicit permission provisioning review.
