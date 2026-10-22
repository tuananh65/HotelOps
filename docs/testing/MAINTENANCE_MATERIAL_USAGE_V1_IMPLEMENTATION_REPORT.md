# Maintenance Material Usage V1 — báo cáo triển khai

Ngày kiểm chứng: 2026-10-22. Nguồn contract là chỉ dẫn V1 được developer khóa
cho task này; không suy diễn quyền từ Permission Matrix draft.

## A. Branch / HEAD / worktree

Preflight: `feature/BE2-maintenance-completion`, HEAD `ee3cadc`, worktree và
staging ban đầu sạch. Không stage/commit/push/merge hoặc thay đổi branch.

## B. Live SQL patch/application

Target đã xác minh `KIENPC / HotelOpsDB`, Windows Authentication qua process
environment `ConnectionStrings__HotelOpsDb`. Đã backup COPY_ONLY/CHECKSUM ngoài
Git và RESTORE VERIFYONLY trước patch. Patch
`database/patches/20261022_maintenance_material_usage_contract.sql` đã áp dụng
thành công: additive table và parent-log unique key, không rewrite/backfill.
Reusable patch chỉ guard chính xác `HotelOpsDB` bằng binary collation, SELECT
server/database, required SET options, XACT_ABORT, TRY/CATCH transaction và schema
applock. Refuse khi bảng đã tồn tại; không chạy lại mù quáng.

## C. Database First scaffold comparison

Inspect naming/Fluent style hiện hữu rồi FULL scaffold bằng EF CLI/provider
10.0.8 vào ignored `src/HotelOps.Api/obj/MaterialUsageScaffold`. Dùng entity
namespace `HotelOps.Api.Entities`, context namespace `HotelOps.Api.Data`, context
`HotelOpsDbContext`, default naming, `--no-onconfiguring`; không dùng
database-names/data-annotations và không scaffold đè source. Named connection
được resolve từ process configuration; không generated connection/OnConfiguring.

Đồng bộ chọn lọc entity mới, DbSet/mapping, parent-log unique metadata và direct
navigations trong đúng năm entities: MaintenanceWork, MaintenanceWorkLog,
StockDocumentLine, InventoryItem, UnitsOfMeasure (hai UOM relationships).
Entity mới so sánh hash với full scaffold; toàn mapping block và các navigation
declarations khớp chính xác. Unrelated generated code được giữ nguyên, không
đồng bộ drift ngoài approved delta. Không EF migrations.

## D. Schema/mapping verification

SQL catalog runner PASS: 10 columns NOT NULL, identity bigint PK; used/base qty
decimal(18,3), factor decimal(18,6); composite FK parent log/Work và năm direct
FKs; ordered key/FK columns, UQ, indexes/filter, navigation metadata, không shadow
properties trên usage và related entities. SQL FKs NO ACTION, enabled/trusted.
EF scaffold dùng ClientSetNull tương ứng FK SQL không cascade.

SQL CHECKs enabled/trusted: positive qty/factor/base, exact product, base factor1.
Test thực thi chứng minh SQL từ chối nonexact product, base factor khác1, sai
composite log owner và duplicate cùng log/source. Parent UQ
`(log_id,maintenance_work_id)` hỗ trợ composite FK. Capacity index
`(stock_document_line_id,usage_id) INCLUDE(used_base_qty)` được kiểm chứng bằng
catalog; INCLUDE/CHECKs không được suy ra chỉ từ scaffold. Không thêm timestamp,
actor, status, cycle hoặc rowversion. Actor/time lấy từ immutable log.

## E. FE-09 lineage implementation

Trước V1, technical log chưa có authoritative line-level consumption; RETURN
chỉ trừ những RETURN khác nên chưa chia sẻ capacity với Maintenance.

Service chứng minh Work→Incident và MR→cùng Incident/branch; FLine→FHeader→MR
phải hội tụ với FLine→MRLine→MR; FHeader.StockDocumentId và FLine.StockDocumentLineId
phải hội tụ cùng ISSUE. Warehouse của fulfillment/MR/document cùng nhau, branch
khớp Incident. Đúng một owner header và correlation line, document ISSUE/POSTED,
PostedAt có giá trị, reference typed đúng fulfillment; không reversal lineage.
MR thuộc new approved workflow, latest cycle APPROVED có matching approver/
decision/snapshot, active source line; quantities approved/fulfilled/summed issues
khớp. SQL unique owner/line indexes cũng được thử để chứng minh ambiguity bị chặn.

Item/UOM/qty của MRLine/FLine/ISSUE khớp; Requested=Actual=Issued>0;
Actual×persistedFactor=Base; đúng một matching negative movement theo warehouse/
item/base. Explicit unsafe reference →400; unsafe availability candidate bị loại.
Không dùng reference text để đoán linkage hoặc sửa legacy evidence. Historical
inactive item/UOM/category/warehouse vẫn dùng được; tests chỉ deactivate fixtures
sở hữu, bao gồm category riêng, không thay đổi category dùng chung.

## F. Usage quantity/UOM

Chỉ base UOM factor1 hoặc original ISSUE UOM với persisted ISSUE factor. Không
lookup conversion master hiện tại. Thay master factor sau ISSUE không đổi usage.
Positive exact decimal(18,3), factor exact decimal(18,6), reject scale/overflow/
silent rounding. Test valid ISSUE 1000×0.000001=0.001 vẫn từ chối input
0.001 original-UOM (product 0.000000001), nhưng chấp nhận 0.001 base UOM.
Usage snapshots incremental/immutable; một log không có source trùng, nhiều log
có thể dùng cùng source nếu còn capacity. Retry POST là allocation mới.

## G. Shared ISSUE capacity / RETURN

`IInventoryIssueCapacity` dùng cùng scoped InventoryService/DbContext với caller,
require existing transaction và Catalog Exclusive. Primitive không bắt đầu/
commit/rollback transaction, không ghi document/movement/audit, không tự lấy
Catalog muộn sau owner locks.

I = negative ISSUE movement base; C = tổng immutable usage base; R = tổng
POSTED RETURN (usable+damaged)×persisted factor. Remaining=I−C−R. Usage và RETURN
validate projected C+R≤I. Draft không reserve; POST recheck dưới Catalog, exclude
chính RETURN đang post. Reversed RETURN trả lại capacity; terminal Work không
release C. RETURN giữ OriginalIssueLine validation hiện hữu, chỉ thêm consumed
cap, không ép strict FE-09 provenance vào legacy path. ISSUE reversal vẫn không
được hỗ trợ. IInventoryIssuePosting semantics không đổi.

## H. Maintenance API/service

`POST /api/maintenance-works/{id}/logs` nhận optional materialUsages gồm source
line/UOM/usedQty, tạo enriched DTO (201). Omitted/null/empty giữ plain path.
Nonempty dùng internal pre-owner callback sau BeginTransaction, trước Incident.
Existing synchronized-pair/state/current-assignee/actor guards giữ nguyên.
V1 chỉ corrective Work IN_PROGRESS/ON_HOLD; hybrid preventive link fail closed.
Không đổi Incident/Work status; new log MaterialIssueId luôn null. Controller
chỉ HTTP/DTO; validation400 ProblemDetails, missing Work404, infrastructure/
deadlock/cancellation propagate. Không edit/delete/void usage API.

## I. Audit/history

Log CREATE audit giữ convention hiện tại, thêm IDs/base summary khi có usage.
Mỗi allocation một `maintenance_material_usages` CREATE audit, OldValues=null,
snapshot, actor/UTC/branch, Work/log/Incident/MR/request-line/fulfillment/document/
warehouse/item/UOM IDs và before/after capacity. Không đổi AuditLog vocabulary.
Không Work/Incident status audit khi roots không đổi. Detail trả ordered usages
trong mỗi log. Validation/rollback không để success audit hoặc partial history.

## J. Material availability

`GET /api/maintenance-works/{id}/material-availability?page=1&pageSize=20`, max100.
Catalog-owned short transaction và Incident→Work locks bảo đảm multi-query
consistent với plain commands. Chỉ synchronized corrective IN_PROGRESS/ON_HOLD;
eligible remaining>0. DTO có typed lineage/document/line/warehouse/item/base,
allowed UOM/factor và I/C/R/remaining. Stable order PostedAt→DocId→LineNo→LineId.
Unsafe candidates bị loại bằng business ValidationException; infrastructure và
cancellation không bị nuốt. Không reservation/stock effect.

## K. Concurrency evidence

Material branch: Catalog→source ISSUE document IDs ASC→Incident UPDLOCK/HOLDLOCK
→Work UPDLOCK/HOLDLOCK→proof/cap→log/usages/audits→commit. Không warehouse/stock
locks do không stock write. Plain Maintenance giữ Incident→Work, không lấy Catalog
muộn. RETURN chỉ Inventory locks, không Incident/Work locks.

28 ordered SQL races PASS: 12 baseline corrective-integrity và 16 mới gồm
usage/usage; usage/RETURN hai orders; khác lines cùng ISSUE; inverted multi-document
payloads; usage với verify/cancel/plain-log/return-for-rework cả hai orders; ba
Inventory/MR/Replenishment Catalog writers. Independent contexts/connections,
test-only interceptor giữ real SQL row lock; observer chứng minh worker SPID
LCK_M wait trên blocker trước release. Assertions kiểm valid serialized winners,
không dùng Task.WhenAll đơn độc làm chứng cứ. Không deadlock trong các ca này.

## L. Rollback/cancellation

22 cases PASS: 11 baseline và 11 mới: material log save1, usage save2, audit save3,
before commit; RETURN save1/save2/before commit; cancellation chờ Catalog/source
document/Incident/Work. EF test-only interceptors inject sau executed SQL flush;
fresh contexts so root/log/usage/audit/RETURN rowversion/state/detail/movement/
ledger footprint. Test primitive không transaction/missing Catalog bị từ chối,
caller transaction giữ nguyên và không write. Context bị lỗi được dispose;
không reuse tracked rollback state. Không production fault switches.

## M. Existing regressions

IncidentMaintenance final PASS: 456 HTTP requests, 917 assertions, 13 baseline
forbidden bypass cases, 28 ordered races, 22 rollback/cancellation cases.
Các runner chạy tuần tự theo yêu cầu và đều PASS:

| Runner | HTTP requests | Assertions | Concurrency/rollback evidence |
| --- | ---: | ---: | --- |
| IncidentMaintenance | 456 | 917 | 28 ordered SQL races, 22 rollback/cancellation cases |
| Inventory | 246 | 510 | Existing race/fault families PASS, success=true, cleanup committed |
| MaterialRequests | 179 | 832 | 11 overlapping races, 10 rollback faults, cleanup completed |
| Replenishment | 273 | 1193 | 14 overlapping races, 21 rollback cases, cleanup committed |

Các regression runner cũ không bị sửa/weaken để thích ứng V1. Các GET smoke
DEV2 hiện hữu giữ nguyên; không tuyên bố full write regression cho Asset hay
các module DEV1 ngoài scope.

## N. Legacy/live preservation

Unique ICITEST prefix, exact owned IDs, cleanup finally theo FK order; không
reset/reseed/backfill/repair. Complete ordered JSON 16 bảng trước/sau khớp,
bao gồm rowversions và audit history. Existing Work21 PENDING / Incident21
IN_PROGRESS không được sửa. Tests mismatch chỉ dùng owned fixtures.
Final SQL SELECT: usage=0, ICITEST Incidents/items/warehouses/categories=0;
12 stock documents, 14 stock lines, 15 movements, 13 MRs, 14 MR lines.
FE-10 regression cũng PASS ordered legacy JSON comparison sau cleanup.

Self-fix validation: hai lượt đầu sót conversion/threshold CREATE audits vì JSON
chỉ có IDs, không prefix. Đã xác định chính xác bốn audit fixture, xóa riêng chúng,
và sửa cleanup lấy owned conversion/threshold IDs trước xóa masters. Hai lượt
acceptance sau cleanup đều khớp snapshot; không xóa historical orphan audits có
sẵn. Failure diagnostics chỉ in table index/key, không dump snapshot/secrets.

## O. Files changed/new

- SQL patch nêu ở B; `docs/database/DATABASE_SCHEMA.md`; báo cáo này.
- Controller MaintenanceWorks; Maintenance DTO CreateWorkLog/WorkLog và mới
  `MaintenanceMaterialUsageContracts.cs`.
- IMaintenanceWorkService, MaintenanceWorkService constructor, Commands/Logs/
  Queries; mới `MaintenanceWorkService.Materials.cs`.
- Inventory Documents RETURN cap; mới `IInventoryIssueCapacity.cs`,
  `InventoryService.IssueCapacity.cs`; scoped alias trong Program.cs.
- Generated new MaintenanceMaterialUsage; context; đúng năm direct entities ở C.
- IncidentMaintenance test Program/Persistence/ConcurrencyRollback/README;
  mới MaterialUsage/MaterialUsageConcurrencyRollback/SchemaMetadata.cs.

## P. Deferred scope

Cycle/reopen/preventive, usage correction/void/reversal và Auth/RBAC không triển
khai. ActorUserId/current-assignee là business integration identity, không phải
authenticated permission/data-scope enforcement. Không hard-code role/branch IDs
hoặc invent permission codes; DEV1 integration deferred theo coordination.

## Q. Build/test totals

`dotnet build HotelOps.slnx` PASS, 0 compile errors. NU1900 vulnerability-feed
warning còn tồn tại. Integration runner còn NU1902/NU1903 từ package
System.Security.Cryptography.Pkcs 9.0.4 có sẵn; không upgrade dependency ngoài scope.
Loopback host có warning HTTPS redirect port nếu không cấu hình local HTTPS.
Business test evidence là console runners chạy thật; `dotnet test` không discover
những runners này. Bốn runner tổng cộng 1154 HTTP requests và 3452 assertions
được console report, chưa tính thêm cleanup/preservation assertions ở finally của
một số runner. Không claim dotnet test đã thực thi những business assertions đó.

## R. Git / temporary output

Scaffold compare PASS; output tạm đã xóa sau khi kiểm tra resolved absolute path
thuộc API obj. Branch/HEAD giữ nguyên, staging rỗng; diff --check sạch.
Status chỉ có intended source/schema/tests/docs; không temp scaffold, secrets,
backup/dump, generated connection strings hoặc migration trong Git changes.

## S. Verdict

Implementation, build, cả bốn SQL/API runners và repository checks PASS.
Các dependency/feed warnings ở Q vẫn còn; không có compile/test failure chưa xử lý.

READY TO COMMIT: YES
