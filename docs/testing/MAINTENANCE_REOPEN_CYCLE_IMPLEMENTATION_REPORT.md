# Maintenance Reopen + Cycle History V1 — báo cáo triển khai

Ngày kiểm chứng: 2026-10-23. Contract khóa theo chỉ dẫn developer cho task này.

## A. Branch / HEAD / Git cleanliness

Preflight: `feature/BE2-maintenance-reopen-cycle`, HEAD `3688ba9`, worktree/staging
sạch. Không stage/commit/push/merge hoặc thay đổi history/branch. Implementation
chỉ gồm Maintenance cycle, required configuration/mappings, SQL, tests và docs.

## B. SQL verification / backup / patch

Đã verify `@@SERVERNAME / DB_NAME()` = `KIENPC / HotelOpsDB` trước patch. Backup
COPY_ONLY/CHECKSUM và RESTORE VERIFYONLY thành công; backup ngoài Git:

`C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\Backup\HotelOps_ReopenCycle_20261023_3688ba9.bak`

Patch `database/patches/20261023_maintenance_reopen_cycle_contract.sql` additive
đã áp dụng. SELECT target, exact binary HotelOpsDB guard, required SET options,
XACT_ABORT, TRY/CATCH transaction, schema applock, refuse pre-existing table hoặc
log CycleId. Không data rewrite/backfill, reset/reseed, migrations hoặc đổi status
vocabulary. Hostname chỉ là evidence local; reusable patch không guard KIENPC.

## C. Database First scaffold comparison

Full scaffold EF CLI/provider 10.0.8 vào ignored
`src/HotelOps.Api/obj/ReopenCycleScaffold`, default naming/Fluent style;
`HotelOps.Api.Entities`, `HotelOps.Api.Data`, `HotelOpsDbContext`, no-onconfiguring.
Named connection resolve qua process environment; không lưu connection source.

Selective sync: MaintenanceWorkCycle entity, DbSet/full mapping, log CycleId/
index/composite FK, direct navigations trên Work/log/User/Department/ReferenceValue.
Entity hash, toàn cycle mapping block và mọi direct navigation declaration khớp
fresh full scaffold. Không replace unrelated mappings/generated tree.
Output scaffold tạm đã được xóa sau khi verify resolved absolute path thuộc API obj.

Self-fix: index active-cycle chỉ WorkId ban đầu khiến scaffold suy one-to-one.
Đã thay chính index mới thành `(WorkId,FinalizedAt) WHERE FinalizedAt IS NULL`
transactionally, cập nhật patch rồi full scaffold lại. SQL uniqueness vẫn chặn
hai active rows; scaffold sinh đúng collection, được catalog/runtime test chứng
minh. Không invent mapping trái metadata hoặc đổi business invariant.

Pre-existing drift: parent log có convention-only FK indexes trong EF nhưng SQL
không có (ví dụ IX_maintenance_work_logs_logged_by); một số generated PK/UQ names
khác live auto-generated names. Không tạo unrelated indexes hoặc đồng bộ churn.
Parent index tests scope vào approved cycle index và existing composite UQ;
column/FK/navigation checks vẫn đối chiếu live catalog.

## D. Exact schema/catalog

Cycle table 31 columns; log thêm nullable bigint CycleId, không default/backfill.
Catalog PASS 50 columns (31 cycle + 9 log + 10 usage), SQL types/lengths/precision/
nullability/identity; PK/UQ/index/filter/ordered FK columns; navigations/no-shadow;
FKs NO ACTION/trusted. Three cycle CHECKs enabled/trusted. Không rowversion thêm
vào Work/Incident hoặc CycleId trên usages.

UQs `(WorkId,CycleNo)`, `(CycleId,WorkId)`; filtered unique active `(WorkId,FinalizedAt)`
với NULL second key. Composite log FK bảo đảm cùng Work. Index log-history
`(CycleId,WorkId,LoggedAt,LogId)`; supporting FK/actor indexes trên cycle.
SQL rejects duplicate active, invented legacy cycle number và cross-Work log.
Multiple finalized cycles cùng Work được chứng minh qua cycle2–4 thật.

CHECKs: positive number; legacy1 finalized/CLOSED và unknown opening metadata;
nonlegacy>=2 có actor/time/reason>=10 và PENDING/OPEN starting states; coherent
active/finalized fields; CLOSED/CANCELLED terminal snapshots với metadata phù hợp.
Mirrored source types: title nvarchar(200), notes nvarchar(max), datetime2(7).

## E. Configuration / reopen window

Typed MaintenanceOptions.ReopenWindowDays, repository appsettings default30;
positive integer, startup ValidateOnStart. Không service magic constant, DB setting
hoặc unlimited fallback. Window dùng Incident.ClosedAt UTC và inclusive boundary
`now <= ClosedAt.AddDays(configuredDays)`; null/expired fail closed 400. Không thay
bằng ReviewedAt/ResolvedAt/current time. Date overflow fail closed.

Standard TimeProvider.System qua DI; test-only FixedClock chứng minh đúng boundary
và +1 tick expiry, cùng custom one-day option. Ba child hosts với config0/-1/text
phải fail startup. Không production fault/test switches.

API ContentRoot lấy application directory để file appsettings được copy cạnh DLL
được load khi runners/deployment gọi từ thư mục khác; environment override giữ
precedence. Lượt startup đầu phát hiện thiếu setting khi CWD là repo root; đã sửa
host và runner fail-fast child-exit diagnostics. Không fixture business được tạo
ở lượt startup lỗi. Lượt rebuild khi runner cũ còn giữ DLL gặp file-lock error;
đã kết thúc đúng owned process, rebuild thành công rồi chạy lại.

## F. Lifecycle/state contract

Chỉ CLOSED Work + CLOSED Incident synchronized, corrective/nonhybrid, ownership
không ambiguous, reliable closure/window và token hợp lệ mới reopen. Target
PENDING/OPEN. Không tạo Work mới, không WorkNo/IncidentNo allocation.

Reset assignee, planned dates, actual start, Work result/review và Incident
resolved/closed/closure note. Giữ IDs/numbers/created/reported/context/classification.
Reason trimmed>=10. Không fake technical log cho reopen.
Managed verify finalize CLOSED snapshot; managed cancel finalize CANCELLED.
Submit-result/return-for-rework không finalize/open cycle. Existing state/assignee/
self-verification guards giữ nguyên. CANCELLED không reopen.

## G. Legacy lazy capture

Work chưa từng reopen vẫn cycle-less, kể cả Work tạo mới qua API cũ. First reopen
tạo legacy finalized cycle1 rồi active cycle2 cùng transaction. Capture đúng
Work/Incident terminal fields, null evidence giữ null. Recorded/FinalizedAt là
thời gian capture thật; historical OpenedAt/By, EndedAt/By không được bịa từ actor
reopen hoặc timestamps. Không reconstruct cycles từ logs/audits.
Subsequent reopen tạo next CycleNo, không sửa finalized history. Snapshot immutable
được enforce trong service paths; không tuyên bố SQL CHECK tự chặn privileged DBA
UPDATE. State/gap/snapshot inconsistency fail closed, không repair.

## H. Cycle token compatibility

ExpectedCycleId nằm trên shared actor DTO. Cycle-less null/omitted tiếp tục hợp lệ.
Managed mọi mutation phải đúng current active CycleId: assign/start/hold/resume/
plain/material log/submit/verify/return/cancel. Guard dưới Incident/Work locks,
trước business mutation. Reopen first yêu cầu null và không có cycles; subsequent
yêu cầu latest finalized ID. Không dùng ClosedAt làm client token hoặc thêm RV.
Delayed previous-cycle command bị reject kể cả Work lại IN_PROGRESS.
Client cũ muốn thao tác reopened Work phải gửi token mới; đây là intentional
compatibility boundary, không tuyên bố blind legacy writes vẫn dùng được cho nó.

## I. Logs / material association

Server lấy CycleId từ locked cycle context trong centralized AddWorkLog; client
không chọn log cycle. Automatic/plain/material logs đều có đúng active FK.
Old null logs không UPDATE; cycle1 log/usage counts là logical legacy grouping,
không gán physical FK hoặc suy timestamp. Usage -> log -> cycle, không duplicate
ownership field. MaterialIssueId luôn null cho logs mới.
Inventory capacity code không đổi: C cộng usages mọi cycles, effective usable+
damaged returns cùng shared cap. Reopen không stock write/reservation/ISSUE.
Real tests giữ old usage/log, dùng thêm trong cycle2, RETURN phần còn lại, và
reject over-cap hoặc old material token sau cycle3. Legacy RETURN regression giữ.

## J. API / read models

POST `/api/maintenance-works/{id}/reopen`: actor/reason/nullable ExpectedCycleId;
200 enriched detail, missing Work404, business400 ProblemDetails. Infrastructure/
cancellation propagate, không Auth/RBAC mới.

GET `/api/maintenance-works/{id}/cycles?page=1&pageSize=20`: max100, validate offset,
CycleNo ASC/CycleId ASC; opening + terminal snapshot/legacy marker/log count/usage
count qua logs, không duplicate toàn log rows trong cycle DTO.
Existing detail giữ current fields/flat logs, thêm CurrentCycleId/No (latest cycle,
kể cả finalized), IncidentClosedAt và nullable log CycleId. Detail/history dùng
owner-locked transaction cho consistent multi-query reads; internal detail reuse
caller transaction, không nested transaction hoặc Catalog muộn.

## K. Audit/history

Cycle CREATE legacy có business_event LEGACY_CYCLE_CAPTURED; new-cycle CREATE có
reopen actor/time/reason và previous/new IDs. Managed finalize Cycle UPDATE lưu
before/after snapshot. Reopen Work/Incident UPDATE và IncidentEvent CLOSED->OPEN
có reason. Technical log CREATE thêm CycleId khi có; existing automatic log audit
convention vẫn qua workflow/root history. Không invent Action values. Mọi success
records cùng transaction, no misleading audit/event/cycle sau rollback.

## L. Lock/concurrency proof

Reopen/plain: transaction -> Incident UPDLOCK/HOLDLOCK -> Work UPDLOCK/HOLDLOCK
-> cycle context -> writes/audits -> commit. Material giữ Catalog -> source docs
ASC -> Incident -> Work -> cycle context -> usage/log/audit. Reopen không Catalog.

43 ordered SQL races PASS gồm 28 baseline và 15 mới: reopen/reopen; generic OPEN/
CLOSED hai orders; verify/cancel/plain/material logs hai orders; consistent detail
hai orders. Independent contexts/SPIDs, executed SQL lock barrier và observer
DMV LCK_M wait trên đúng blocker trước release. Material holder/reopen waiter
thể hiện Catalog/source -> Incident; reopen không đảo order. Assertions kiểm
serialized winners/no-op/stale rejection, không dùng Task.WhenAll làm proof duy nhất.

## M. Rollback/fault proof

35 cases PASS gồm22 baseline +13 mới: legacy insert flush1; new cycle + legacy
audit flush2; reset/history flush3; before reopen commit; managed verify/cancel
root/cycle/history flush; plain-log flush1/2; material flush1/2/3; cancellation chờ
Incident/Work. Existing Catalog/document material cancellation tests vẫn PASS.
Fresh contexts compare full cycle scalar snapshots, log CycleId và root/log/usage/
audit/event/RETURN/stock footprints. No partial committed effects. Identity gaps
được phép; rollback không consume logical CycleNo. Failed contexts được dispose.

## N. Preservation / cleanup

Unique ICITEST prefix và exact owned IDs; no reset/reseed/backfill. Cleanup usage
trước log, log trước cycle, cycle trước Work; audit cleanup đúng entity IDs.
Owned preventive-association fixture được xóa trong finally; không toggle shared
schedule/master data. Standalone/drift/ambiguous tests chỉ sửa owned Work/Incident.
Complete ordered JSON17 tables trước/sau khớp, gồm cycles/full log schema/audits và
legacy mismatch. Work21 PENDING / Incident21 IN_PROGRESS không được thay đổi.
Final SQL:59 Works,35 logs,20 preventive runs,0 cycles,0 usages;0 owned ICITEST
Incidents/items. Work/Incident không có rowversion mới. Final acceptance còn kiểm
tra plans/context có giá trị trước reopen, snapshot giữ chúng và classification
Incident không đổi; không chỉ assert các field vốn null.

## O. Build / regression results

`dotnet build HotelOps.slnx` PASS, 0 compile errors. IncidentMaintenance final PASS:
731 HTTP requests,1612 assertions,13 baseline forbidden bypass cases,43 ordered
races,35 rollback/cancellation cases; cleanup/preservation PASS.

| Runner | Requests | Assertions | Result |
| --- | ---: | ---: | --- |
| IncidentMaintenance | 731 | 1612 | PASS;43 races,35 rollback cases |
| Inventory | 246 | 510 | PASS;success=true,fixture cleanup committed |
| MaterialRequests | 179 | 832 | PASS;11 overlap races,10 rollback faults |
| Replenishment | 273 | 1193 | PASS;14 overlap races,21 rollback cases;legacy JSON unchanged |

Bốn runner chạy tuần tự theo yêu cầu; sau đó chạy lại IncidentMaintenance với
hai assertions fixture preservation bổ sung, production code không đổi. Tổng
final reported counts1429 requests/4147 assertions, chưa cộng một số finally
assertions ở runners cũ. Không sửa/weaken ba regression runners cũ.
`dotnet test HotelOps.slnx` exit0; solution không discover console
integration runners. Business evidence là runners được thực thi riêng, không
claim dotnet test chạy các assertions đó.

Known warnings: NU1902/NU1903 System.Security.Cryptography.Pkcs9.0.4 hiện xuất hiện
qua transitive graph ở API/integration builds; không thay package versions.
NU1900 có thể xuất hiện khi vulnerability feed unavailable. Loopback HTTPS-port,
existing EF multiple-collection/Distinct-order warnings không được đổi ngoài scope.
Không còn compile/test failure chưa xử lý.

## P. Exact changed files

Các paths dưới đây từ repository root:

- `database/patches/20261023_maintenance_reopen_cycle_contract.sql`
- `docs/database/DATABASE_SCHEMA.md`
- `docs/testing/MAINTENANCE_REOPEN_CYCLE_IMPLEMENTATION_REPORT.md`
- `src/HotelOps.Api/Controllers/MaintenanceWorksController.cs`
- `src/HotelOps.Api/DTOs/Maintenance/MaintenanceActorRequest.cs`
- `src/HotelOps.Api/DTOs/Maintenance/MaintenanceCycleContracts.cs`
- `src/HotelOps.Api/DTOs/Maintenance/MaintenanceWorkDetailDto.cs`
- `src/HotelOps.Api/DTOs/Maintenance/MaintenanceWorkLogDto.cs`
- `src/HotelOps.Api/Data/HotelOpsDbContext.cs`
- `src/HotelOps.Api/Entities/Department.cs`
- `src/HotelOps.Api/Entities/MaintenanceWork.cs`
- `src/HotelOps.Api/Entities/MaintenanceWorkCycle.cs`
- `src/HotelOps.Api/Entities/MaintenanceWorkLog.cs`
- `src/HotelOps.Api/Entities/ReferenceValue.cs`
- `src/HotelOps.Api/Entities/User.cs`
- `src/HotelOps.Api/Program.cs`
- `src/HotelOps.Api/appsettings.json`
- `src/HotelOps.Api/Services/Maintenance/IMaintenanceWorkService.cs`
- `src/HotelOps.Api/Services/Maintenance/MaintenanceOptions.cs`
- `src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.cs`
- `src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Commands.cs`
- `src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Cycles.cs`
- `src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Logs.cs`
- `src/HotelOps.Api/Services/Maintenance/MaintenanceWorkService.Queries.cs`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/ConcurrencyRollback.cs`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/MaterialUsage.cs`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/Persistence.cs`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/Program.cs`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/README.md`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/ReopenCycles.cs`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/ReopenCycleConcurrencyRollback.cs`
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/SchemaMetadata.cs`

## Q. Deferred scope

Preventive functionality, Auth/RBAC/permission policy, usage correction/reversal,
notifications, SLA/evidence subsystem, reconstruction/backfill và CANCELLED reopen
đều deferred. ActorUserId/current assignee không phải authenticated authorization.
Không hardcode role/branch IDs; không thay Inventory capacity hoặc FE09/FE10 source.

## R. Final verdict

Implementation/build/four SQL/API runners và preservation checks PASS. Branch
`feature/BE2-maintenance-reopen-cycle`, HEAD `3688ba9` giữ nguyên.32 intended
changed/new files đúng danh sách P,0 staged files; diff --check sạch. Không temp
scaffold, secrets, backup/dump hoặc migrations trong changed paths; source scan
không có persisted local connection/OnConfiguring. Không stage/commit/push/merge.

READY TO COMMIT: YES
