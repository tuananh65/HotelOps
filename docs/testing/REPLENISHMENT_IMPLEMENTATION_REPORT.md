# FE-10 Replenishment — báo cáo triển khai

Ngày kiểm chứng: 2026-10-21. Implementation hoàn tất theo locked contract của
developer, trên `feature/BE2-replenishment`, starting HEAD `60a6b91`.

## 1. Preflight, phạm vi và target

Branch, HEAD, `develop` và `origin/develop` đã xác minh tại `60a6b91`. Ban đầu
không có tracked/staged changes; coordination document
`docs/coordination/HotelOps_Dev2_Backend_Sync_RBAC_2026-10-20.md` là untracked
artifact có chủ đích, được giữ nguyên. Không stage/commit/push, đổi nhánh hoặc reset.

SQL local được xác minh bằng `@@SERVERNAME / DB_NAME()` là `KIENPC / HotelOpsDB`.
Đã backup COPY_ONLY/CHECKSUM ngoài Git và RESTORE VERIFYONLY thành công trước patch.
Hostname chỉ là evidence của lần chạy; reusable patch chỉ guard chính xác
`HotelOpsDB` bằng binary collation, đồng thời SELECT server/database cho operator.

Không EF migrations, RBAC migrations, Auth implementation, Notification,
procurement, supplier master, reservation hoặc stock engine thứ hai. Không ghi
stock_movements trực tiếp từ FE-10. Không sửa các module ngoài các dependencies
Inventory được yêu cầu. Không lưu connection string, secrets, backup/dump vào Git.

## 2. SQL patch, Database First và schema

Patch mới: `database/patches/20261021_replenishment_contract.sql`. Đã áp dụng thành
công trên target. Patch additive tạo ba bảng; XACT_ABORT, required SET options,
transaction và schema applock; từ chối nếu đã có một trong ba bảng thay vì overwrite.
Đây không phải installer rerunnable: môi trường đã cài phải compare catalog,
không chạy lại patch mù quáng.

Schema cuối:

| Bảng | Nội dung chính |
| --- | --- |
| `material_replenishments` | Identity PK, unique number varchar(50), warehouse/requester FKs, supplier nvarchar(200) nullable, expected date nullable, bốn statuses, closure metadata CHECK, UTC timestamps, rowversion, composite owner/warehouse UQ. |
| `material_replenishment_lines` | Identity PK, composite owner FK, unique owner/line number, item/base-UOM snapshots, requested/received decimal(18,3), typed source với đúng một FK, coverage bit, note. Filtered unique warehouse/item khi open. |
| `material_replenishment_receipt_lines` | Identity PK, replenishment-line FK, globally unique stock-line FK, positive immutable base credit, linked timestamp/actor. |

Tất cả FK SQL NO ACTION; quantity/source/closure/status CHECKs enabled và trusted.
Supporting indexes có root tracking, source lookups, receipt owner và các FK.
Full metadata test đối chiếu cả 32 columns, SQL types/max lengths/precision/scale/
nullability/identity, PK/UQ/FK target columns theo ordinal, indexes/filter,
navigations, không shadow FK; rowversion là concurrency token, generated
OnAddOrUpdate. `IsRowVersion()` có trong generated Fluent mapping.

EF CLI/provider hiện có là 10.0.8. Đã inspect naming/Fluent style hiện hữu rồi
FULL scaffold vào ignored `src/HotelOps.Api/obj/ReplenishmentScaffold`, dùng
`HotelOps.Api.Entities`, `HotelOps.Api.Data`, `HotelOpsDbContext`, default naming,
`--no-onconfiguring`; không database-names/data-annotations, không overwrite src.
Connection supplied qua local process configuration, không generated OnConfiguring.

Chỉ đồng bộ ba generated entities, ba DbSets/mapping blocks và direct navigations
trong bảy entity hiện hữu. Đã compare chính xác text của ba entities/blocks với
fresh scaffold; catalog test kiểm chứng metadata độc lập. Giữ unrelated generated
code/BOM. Không đồng bộ pre-existing drift RolePermission.DataScope, Room và
assignment indexes ngoài delta FE-10. Drift đó không ngăn FE-10 hoạt động an toàn.
Scaffold tạm được xóa sau compare; regeneration document hiện hữu đã đủ quy trình,
nên không thêm hướng dẫn lặp lại riêng FE-10.

Self-fix trong validation: bổ sung sáu FK supporting indexes mà EF convention cần
vào approved patch và áp dụng delta transactional, rồi full scaffold lại. Catalog
test ban đầu yêu cầu alternate-key name theo EF convention; đã sửa để đối chiếu
unique column structure vì scaffold biểu diễn SQL named UQ bằng unique index và
HasPrincipalKey sinh alternate-key convention name. Không đổi generated mapping
để chiều theo test; final metadata test PASS.

## 3. Business contract

NEW → REQUESTED; net >0 và còn thiếu → PARTIALLY_RECEIVED; tất cả lines đủ →
FULLY_RECEIVED. REQUESTED/PARTIAL có remaining dương được close với trimmed reason
ít nhất 10 ký tự → CLOSED. Không edit/delete/reopen/approval/procurement commands.
Requested qty/source/base-UOM snapshot immutable sau create.

LOW_STOCK: warehouse/branch/item/catalog active, đúng threshold warehouse/item,
Inventory ledger OnHand < MinimumQty. AlertEnabled không quyết định eligibility.
DeficitToMinimum chỉ là context; không lấy Maximum/Reorder làm quantity tự động.
Caller chọn positive exact decimal(18,3), không rounding; supplier tối đa 200,
expected date cho null hoặc ngày quá khứ, không tự đặt điều kiện future.

MR_SHORTAGE: chỉ new FE-09 workflow có latest approved cycle, active line,
APPROVED/PARTIALLY_ISSUED, approved > fulfilled, fixed active same-branch source
warehouse. Giữ từng RequestLineId độc lập, kể cả cùng item. FULLY_ISSUED,
REMAINDER_CLOSED và legacy thiếu workflow evidence không được dùng làm source.
Không backfill/repair hoặc reinterpret PURCHASE_ORDER/reference text.

Physical context V1: cộng remaining approved base demand của eligible new workflow
theo warehouse/item rồi trừ observable OnHand, clamp về 0. Chỉ trả candidates khi
physical shortage >0; không coi mọi approved demand là shortage. Aggregate là
read context, không reservation hoặc suggested order. Create rechecks source và
physical context dưới khóa; requested base không vượt remaining converted của
source line. Dùng shared Inventory direct item-specific conversion, không chain
hoặc global fallback; thiếu conversion an toàn thì business validation 400.

Tối đa một active coverage theo warehouse/item, bất kể LOW/MR/source line.
Application shortage lock và filtered SQL unique index cùng bảo vệ invariant.
Line đủ hàng hoặc root CLOSED thì release coverage, giữ lịch sử; không merge
sources. Header không chứa hai lines cùng item vì cùng warehouse sẽ vi phạm rule.

Requested immutable; gross = tổng immutable credits, net = credits từ effective
POSTED RECEIPTs, remaining = requested − net. Received cache/status/coverage được
reconcile với durable links và Inventory state; mismatch fail closed. Planned
replenishment không thay đổi OnHand/Available.

RPL number theo UTC `RPL-yyyyMMdd-NNN`; numeric suffix maximum, ASCII digits,
malformed suffix ignored, 999→1000, tối đa varchar(50), transaction-owned number
lock và SQL uniqueness. Rollback không consume business number; identity gaps
không phải business number và được phép.

## 4. Inventory integration, reversal, locks và audit

`IInventoryReceiptPosting` dùng cùng scoped DbContext/connection/transaction của
caller. Primitive yêu cầu transaction, dùng extracted PostCore của Inventory,
không commit; trả authoritative actual/base quantities. Public Inventory Post
vẫn tự quản transaction và giữ contract cũ. `IInventoryQuantityContext` chia sẻ
conversion/ledger observation, không tạo phép quy đổi hoặc stock cache riêng.

Coordinated route khóa receipt/root, gọi Inventory primitive rồi ghi links,
cache/status/coverage/audits trong cùng transaction. Accepted usable BaseQty mới
credit; rejected/damaged không tính. Accepted zero có thể POST Inventory nhưng
không tạo positive receipt link. Một stock line chỉ link một replenishment line
toàn hệ thống; credit toàn line, không split/truncate/client quantity, vượt
remaining phải rollback toàn bộ coordinated post.

Adoption yêu cầu RECEIPT POSTED, đúng destination warehouse/item/base identity,
actual×factor=base, positive usable base và đúng một matching positive movement.
Missing legacy receipt detail được phép chỉ khi document/line/movement chứng minh
deterministic lineage; thiếu hoặc ambiguous movement thì fail closed. Reversed
receipt không adopt. Không suy đoán correlation bằng thứ tự lines/reference text.

Inventory-owned whole-document reversal tìm linked FE-10 owners, reconcile,
recompute net/cache/status/coverage và audit cùng transaction. Gross/link snapshots
không sửa. FULLY có thể correction về PARTIAL/REQUESTED; CLOSED luôn CLOSED nhưng
net/remaining được sửa đúng. Nếu reopening coverage đụng owner khác, reject toàn
reversal, original receipt/stock/FE-10 giữ nguyên. Proven reversal lineage dùng
typed ReversalOfDocumentId, đúng một POSTED REVERSAL và offset ledger theo
warehouse/item; không giả định one-to-one reversal line correlation.

Final lock order: Inventory Catalog → document IDs ascending → MR source owners
ascending khi create → FE-10 owners ascending → shortage warehouse/item sorted →
warehouses → stock pairs → number. Khóa applock transaction-owned và owner rows
UPDLOCK/HOLDLOCK. Catalog hiện serialize writers liên quan; đây là safety boundary
của baseline, không tuyên bố throughput tối ưu. Detail/history/shortage reads dùng
short Catalog-owned transaction cho consistent multi-query state khi local RCSI/
SNAPSHOT tắt. Không đổi shared isolation configuration.

Base-UOM không được đổi sau FE-10 history; warehouse branch history guard mở rộng
FE-10, deactivation chặn open roots theo Inventory policy. Không redesign master APIs.

Audit chỉ dùng allowed CREATE/UPDATE. JSON business events: REPLENISHMENT_CREATED,
RECEIPT_LINKED, REPLENISHMENT_PARTIALLY_RECEIVED, REPLENISHMENT_FULLY_RECEIVED,
REMAINDER_CLOSED, RECEIPT_REVERSED_REPLENISHMENT_RECALCULATED. Có actor, UTC time,
derived branch, root/warehouse/source/item/base identities, before/after requested,
gross/net/remaining/status/coverage; receipt-link extra chứa document/line/credit,
close state chứa reason; reversal event tham chiếu original receipt, Inventory
audit giữ reversal reason. History gộp FE-10 và Inventory audits, ordered UTC/ID.
Không success audit cho validation/stale/duplicate/rollback; fault tests kiểm chứng.

## 5. API và bàn giao DEV1 Authorization

Đúng mười routes trong bảng dưới. Create trả 201; reads/actions 200, missing owner
404; business ValidationException → 400 ProblemDetails. Unexpected DB/lock timeout/
deadlock/connection errors không đổi thành validation 400; cancellation propagate.
Controller chỉ HTTP/DTO; services async/CancellationToken, read-only AsNoTracking.
List có branch/warehouse/status/item/source/supplier/expected/created/keyword
filters, default page 1/size20, max100. SQL order ExpectedOn null-last, date ASC,
root ID ASC. Detail có raw status/Base64 rowversion và receipt correction history.

ActorUserId chỉ là temporary integration identity: user tồn tại, chưa deleted,
Account/Employment ACTIVE và có primary-branch hoặc nonrevoked branch relationship.
**Đây không phải authorization.** Không role IDs/names/manager_user_id/department
scope giả định, không coi PrimaryBranchId đơn độc là permission. Coordination xác
định OM/DH là peer branch roles; DH BRANCH scope không suy ra quyền write FE-10.

H1 áp dụng cho mọi route: chưa thể enforce authenticated identity, effective
permission, actual data scope hoặc permission-based SoD cho đến DEV1 merge.
Client filter không phải data-scope enforcement. Future pipeline phải là identity
→ effective permission → data scope → business guard → approved SoD. Bảng chỉ dùng
permission semantics, không invent permission codes hoặc SoD policy chưa duyệt.

| HTTP | Route | Business action | Resource derive Branch | Expected actor | Future permission semantic | Future scope | Business guard | SoD requirement | Chưa enforce |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GET | `/api/replenishments` | List/search | Root.Warehouse.Branch | Storekeeper; oversight nếu được cấp | Read replenishment | Intersect effective branch scope | Paging/filter | Không có SoD read mới | H1 |
| GET | `/api/replenishments/{id}` | Detail | Root.Warehouse.Branch | Storekeeper; granted oversight | Read replenishment detail | Resource branch | Reconciliation | Không có SoD read mới | H1 |
| GET | `/api/replenishments/{id}/history` | Audit history | Root.Warehouse.Branch | Granted history reader | Read replenishment history | Resource branch | Proven links/owner | Không có SoD read mới | H1 |
| GET | `/api/replenishments/{id}/receipts` | Receipt correlation history | Root.Warehouse.Branch | Storekeeper; granted oversight | Read receipt correlation | Resource branch | Proven receipt/reversal evidence | Không có SoD read mới | H1 |
| POST | `/api/replenishments` | Create REQUESTED | Warehouse.Branch; MR source branch consistency | Storekeeper | Create replenishment | Warehouse branch | Active actor, source/quantity, unique coverage | Chưa có FE-10 proposer/approver workflow | H1 |
| POST | `/api/replenishments/{id}/close` | Close remainder | Root.Warehouse.Branch | Storekeeper; OM exception chỉ khi được cấp | Close replenishment remainder | Resource branch | Rowversion/open/positive remaining/reason | Exception grants/SoD cần policy duyệt | H1 |
| POST | `/api/replenishments/{id}/receipts/post` | Inventory POST + correlate | Root.Warehouse.Branch + receipt destination | Storekeeper | Post Inventory receipt AND correlate replenishment | Both resources same permitted branch | Both rowversions, evidence/cap/atomicity | Phải giữ Inventory SoD khi policy hoàn tất | H1 |
| POST | `/api/replenishments/{id}/receipts/link` | Adopt POSTED receipt | Root.Warehouse.Branch + receipt destination | Storekeeper | Correlate posted Inventory receipt | Both resource branches | Rowversion/evidence/global uniqueness/cap | Correlation không bypass Inventory posting permission | H1 |
| GET | `/api/replenishment-shortages/low-stock` | Discover below-minimum | Threshold.Warehouse.Branch | Storekeeper; granted oversight | Read physical low-stock context | Effective warehouse branches | Active catalogs/exact threshold/ledger | Không có SoD read mới | H1 |
| GET | `/api/replenishment-shortages/material-requests` | Discover approved-demand shortage | MR.Branch + SourceWarehouse.Branch | Storekeeper; granted demand reader | Read approved MR shortage context | Effective branch scope | New approved workflow/physical shortage | Không thay approval/fulfillment SoD FE-09 | H1 |

Storekeeper role alone không chứng minh permission; OM không mặc nhiên mọi quyền;
DH BRANCH scope không cho phép FE-10 writes. Reversal tiếp tục là Inventory route,
cần DEV1 bảo vệ Inventory permission/scope/SoD và các affected FE-10 resources.

## 6. Kiểm thử và kết quả

Runner và lệnh tái lập: `tests/HotelOps.Replenishment.IntegrationTests/README.md`.
Tests thật trên SQL/API, không mock stock hoặc sửa tests regression có sẵn.

| Validation | Kết quả |
| --- | --- |
| FE-10 acceptance | PASS: 273 HTTP requests, 1.193 assertions tại acceptance output, 14 SQL overlap races, 21 rollback cases. Cleanup và legacy JSON comparison chạy thêm trong finally, PASS. |
| Schema | PASS: 32 columns; type/length/precision/nullability/identity/rowversion; PK/UQ/FK/index/filter/navigation/no-shadow; constraints trusted/enabled. |
| Inventory regression | PASS: 246 HTTP requests, 510 assertions, success=true; fixture cleanup committed. |
| Material Request regression | PASS: 179 HTTP requests, 832 assertions, 11 overlapping races, 10 rollback faults; fixture cleanup completed. |
| Incident/Maintenance/Asset | PASS startup + GET smoke ở cả FE-10/Inventory runners. Không tuyên bố full write regression ba modules này. |
| `dotnet build HotelOps.slnx` | PASS, 0 compile errors, NU1900 feed warnings. |
| `dotnet test HotelOps.slnx --no-restore` | Exit 0; solution hiện không discover console integration runners. Business test evidence là ba runners thực thi riêng bên trên. |
| FE-10 test project build | PASS, 0 errors/0 warnings trước final acceptance. |

Functional tests bao phủ low-stock/no-threshold/not-below-min, MR shortage và
terminal exclusion, legacy reject, independent duplicate item RequestLineIds,
cả bốn kiểu duplicate coverage, exact decimal bounds, supplier/date null/max/past,
numbering/malformed/999→1000/rollback, malformed/stale versions, các close guards,
coordinated posting/partial/multi-line/multiple/full, accepted-only/zero,
wrong item/warehouse/overcredit/global unique, safe missing-detail adoption và
unsafe/reversed rejection, toàn bộ reversal corrections/collision rollback,
list/detail/history/filters/paging/null-last, audit content/actions, cache mismatch
fail closed, master guards, primitive missing-tx refusal/caller rollback.

Mười bốn race families đều PASS: LOW/LOW, LOW/MR, MR/MR, create/stock-above-min,
MR create/FE-09 fulfillment, same receipt twice, two receipts/remaining, link/close,
coordinated post/link, reversal/link, reversal/new coverage, RPL allocation,
stale commands và FE-10/Inventory writer. Mỗi case dùng separate DbContexts và
third-connection Catalog blocker; `sys.dm_tran_locks` chứng minh cả hai sessions
WAIT trước release. Assertions kiểm winners hợp lệ; không dùng Task.WhenAll làm
bằng chứng duy nhất. Cases independent writers có thể cùng thành công hợp lệ.

21 rollback cases gồm number allocation, ba create SaveChanges boundaries, chín
semantic posting stages, posting before-commit, ba reversal Save boundaries,
reversal before-commit, cancellation chờ Catalog/owner/shortage. State/detail/
movement dùng chung atomic flush; cache/status/coverage/FE-10 audit cũng chung
flush, test nhãn từng effect và kiểm toàn bộ footprint; không tuyên bố các nhãn
là chín SQL flush riêng biệt. Fresh contexts đối chiếu headers/lines/links/cache/
status/coverage/rowversion/timestamps/audits/docs/details/movements/ledger. Không
production fault switches. Number rollback không để lại root/line/audit và number
kế tiếp được reuse. Reversal faults không để partial receipt/offset/FE-10 state.

Inventory regression bảo vệ public receipt, return, transfer, stocktake,
adjustment, reversal, ISSUE primitive, locking, numbering, cancellation/rollback.
FE-09 regression bảo vệ approval, fulfillment, IInventoryIssuePosting, versions,
races/rollback và legacy fail-closed; không đổi FE-09 service/contract.

## 7. Cleanup, Git và giới hạn còn lại

Fixtures dùng unique prefix và exact owned IDs. Cleanup trong finally chỉ xóa
owned replenishments/MRs/Inventory effects/audits/master fixtures; không reseed.
Full ordered JSON của stock_documents, stock_document_lines, stock_movements,
material_requests, material_request_lines, incidents, maintenance_works, assets
trước fixtures và sau cleanup khớp byte-for-byte, gồm rowversions. Final SELECT:
12 stock documents, 14 stock lines, 15 movements, 13 MRs, 14 MR lines; cả ba bảng
FE-10 có 0 rows; không còn RPLTEST items/warehouses.

Changed/new files theo nhóm (paths từ repository root):

- `database/patches/20261021_replenishment_contract.sql`.
- `src/HotelOps.Api/Controllers/ReplenishmentsController.cs` và
  `DTOs/Replenishments/ReplenishmentContracts.cs`.
- `Services/Replenishments/ReplenishmentService.cs`,
  `ReplenishmentService.Queries.cs`, `ReplenishmentRules.cs`.
- `Services/Inventory/IInventoryReceiptPosting.cs`,
  `InventoryService.ReceiptIntegration.cs`; required integrations trong
  `InventoryService.Documents.cs`, `InventoryService.Adjustments.cs`,
  `InventoryService.Masters.cs`; scoped DI trong `Program.cs`.
- `Data/HotelOpsDbContext.cs`; new entities `MaterialReplenishment.cs`,
  `MaterialReplenishmentLine.cs`, `MaterialReplenishmentReceiptLine.cs`;
  direct navigations trong `InventoryItem.cs`, `MaterialRequestLine.cs`,
  `StockDocumentLine.cs`, `StockThreshold.cs`, `UnitsOfMeasure.cs`, `User.cs`,
  `Warehouse.cs`.
- `tests/HotelOps.Replenishment.IntegrationTests/`: csproj, `Program.cs`,
  `Additional.cs`, `ContractEvidence.cs`, `Concurrency.cs`, `Rollback.cs`,
  `SchemaMetadata.cs`, `README.md`.
- `docs/database/DATABASE_SCHEMA.md`, báo cáo này; coordination document ban đầu
  được giữ nguyên làm intended artifact, không phải file do FE-10 viết lại.

Final review kiểm tra status đầy đủ, diff/stat/name-status, diff --check và cached
name-status: chỉ intended scope, không staged files; không scaffold artifacts,
secrets, backup/dump hoặc migration. Không stage/commit/push.

Known warnings: NU1900 khi không lấy được vulnerability feed; Inventory runner
giữ NU1902/NU1903 của System.Security.Cryptography.Pkcs 9.0.4 có sẵn. Không upgrade
dependency ngoài scope. Loopback host có HTTPS redirect-port warning; không đổi
production HTTPS configuration. Không có unresolved compile/test failures.

Deferred cross-module gaps theo coordination, chỉ document:

1. Generic Incident status endpoint có thể bypass corrective-work orchestration;
   evidence local work 21 PENDING / incident 21 IN_PROGRESS không được sửa trong FE-10.
2. Inventory adjustment/reversal proposer-vs-poster SoD chưa hoàn thiện.
3. FE-09 final routing employee→DH / DH→OM / OM→DH chưa triển khai đầy đủ.
4. FE-09 hiện yêu cầu explicit approver, chưa pending-unassigned.
5. Maintenance consumption accounting/cycle model chưa hoàn thiện.
6. Auth/RBAC còn deferred; API chưa có effective permission/data-scope enforcement.

Các deferred gaps này không được coi là đã kiểm chứng hoặc đã sửa bởi FE-10.
