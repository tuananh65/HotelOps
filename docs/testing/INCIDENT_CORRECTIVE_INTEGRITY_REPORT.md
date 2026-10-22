# Incident corrective-integrity — implementation report

Ngày xác minh: 2026-10-21. Phạm vi theo approved locked contract của developer.

## A. Branch / HEAD / worktree

Preflight: `feature/BE2-incident-corrective-integrity`, HEAD `55b56c6`, working tree
và staging sạch. Final HEAD/branch không đổi; `55b56c6` là ancestor của HEAD.
Không stage, commit, push, merge, reset hoặc switch branch.

## B. Production change

Chỉ sửa `src/HotelOps.Api/Services/Incidents/IncidentService.cs`: thêm 10 dòng,
gồm private service invariant và lời gọi trong effective status-change branch.
Không sửa controller, DTO, Maintenance orchestration hoặc DI.

Original bypass: `PATCH /api/incidents/{id}/status` chỉ kiểm Incident transition
matrix rồi ghi Incident status/event/audit. Không xét linked MaintenanceWork.
Vì vậy có thể start Incident nhưng Work vẫn PENDING, resolve khi Work vẫn đang
chạy/ON_HOLD, close/return khi Work vẫn COMPLETED hoặc cancel Incident một phía.
Các Maintenance commands sau đó bị RequireSynchronizedPair chặn.

## C. Effective-status guard behavior

Sau khi begin transaction, load Incident với existing `UPDLOCK,HOLDLOCK`, validate
actor và normalize/validate target status, nếu target khác current status thì
`RequireGenericStatusOwnershipAsync` gọi async `MaintenanceWorks.AnyAsync` theo
IncidentId. Có bất kỳ linked Work nào thì throw ValidationException, trả normal
400 ProblemDetails với thông báo dùng corrective Maintenance workflow.

Không lọc active status, không bỏ qua terminal/legacy links, không tự route PATCH
thành Maintenance command. Query dùng CancellationToken và AsNoTracking.
Linkage check ở trong transaction, sau Incident lock; CreateWorkAsync cũng khóa
Incident trước cardinality/link creation. Giữ Incident → Work/linkage order,
không có precheck ngoài transaction hoặc lock order mới.

## D. Same-status no-op proof

Guard nằm bên trong `incident.Status != normalizedTarget`. Same-status request
giữ hành vi cũ: thành công, không SaveChanges, không mutation/timestamp/event/audit
mới, kể cả có linked Work hoặc pair đã lệch. Actor/status validation hiện hữu
vẫn chạy trước no-op.

Runner chụp fresh-context footprint trước/sau normalized no-op cho unpaired,
paired, terminal và owned mismatched cases. Footprint bao gồm Incident/Work
fields, timestamps, logs, events, audits và global row counts; so sánh bằng nhau.
Năm race cases còn chứng minh generic request chờ paired command rồi thành no-op
hợp lệ, với đúng một bộ paired success history.

## E. Non-corrective transition regression

Khi không linked Work, giữ nguyên sáu transitions:

| Current | Target | Existing note rule |
| --- | --- | --- |
| OPEN | IN_PROGRESS | Không bắt buộc |
| OPEN | CANCELLED | Trimmed note không rỗng |
| IN_PROGRESS | RESOLVED | Không bắt buộc |
| IN_PROGRESS | CANCELLED | Trimmed note không rỗng |
| RESOLVED | CLOSED | Trimmed note không rỗng |
| RESOLVED | IN_PROGRESS | Trimmed reason không rỗng |

Tests thực thi tất cả, kiểm normalize lowercase/whitespace, invalid targets/edges,
required note/reason, 404, resolution timestamp/clear-on-return, closure timestamp/
trimmed note, exact status-event sequence và audit action/actor/branch/before-after.
Create, triage, list/detail không đổi; triage trên owned corrective OPEN vẫn hoạt
động và giữ status. GET Incident/Maintenance smoke PASS.

## F. Corrective paired lifecycle regression

Không thay production Maintenance code. Real API lifecycle PASS:

create → assign → start → hold → resume → create log → submit-result → return →
submit-result lần hai → verify. Assert pair mapping sau từng bước:

| Work | Incident |
| --- | --- |
| PENDING | OPEN |
| IN_PROGRESS / ON_HOLD | IN_PROGRESS |
| COMPLETED | RESOLVED |
| CLOSED | CLOSED |
| CANCELLED | CANCELLED |

Kiểm clear result/review metadata khi return, reviewer/start metadata khi verify,
exact lifecycle Work/Incident audit counts và derived branch/actor. Cancel paired
từ PENDING/IN_PROGRESS/ON_HOLD PASS; COMPLETED không cancel được.

Existing business guards PASS: wrong actor không start/hold/resume/log/submit;
assignee không verify hoặc return own result; minimum reason/note, required
resolution note, active corrective cardinality và create/assignment planned-time
guards. Không reinterpret các guards này thành final Auth/RBAC.

## G. Forbidden bypass cases

Tám required cases đều trả 400 với corrective-workflow message:

| Work / Incident ban đầu | Generic target bị chặn |
| --- | --- |
| PENDING / OPEN | IN_PROGRESS, CANCELLED |
| IN_PROGRESS / IN_PROGRESS | RESOLVED, CANCELLED |
| ON_HOLD / IN_PROGRESS | RESOLVED, CANCELLED |
| COMPLETED / RESOLVED | CLOSED, IN_PROGRESS |

Mỗi rejection đối chiếu fresh footprint: không đổi Incident/Work/timestamps,
không thêm log/event/success audit. Tổng runner có 13 rejected HTTP bypass cases:
8 required, 2 owned mismatch, 2 terminal-linked và 1 bổ sung cho any-link evidence.

Unknown-status limitation được kiểm chứng, không bỏ qua: live SQL có
`CK_maintenance_works_status` chỉ cho phép sáu statuses. Test thử unknown trên
owned fixture phải nhận SQL 547 đúng constraint, footprint không đổi. Không tắt
CHECK hoặc đổi schema để tạo impossible stored value. Test-only interceptor
capture production any-link query xác nhận không có status predicate; terminal
và mismatched records được thử trực tiếp. Không tự sửa bất kỳ pair nào.

## H. Concurrency evidence

12 cases PASS: generic cạnh tranh với create/start/submit-result/verify/return/
cancel, mỗi family thử cả hai thứ tự lấy Incident lock.

Mỗi case có independent DbContexts/connections và explicit SPIDs. Test-only
command interceptor giữ worker thứ nhất sau khi real Incident UPDLOCK/HOLDLOCK
query đã execute, transaction vẫn giữ lock. Worker thứ hai chạy production method;
third connection kiểm `sys.dm_exec_requests` thấy chính SPID thứ hai bị block bởi
SPID thứ nhất, `wait_type LIKE 'LCK_M_%'`, trước khi cho worker đầu tiếp tục.
Không dùng Task.WhenAll hoặc timing delay đơn độc làm overlap proof.

Outcomes được kiểm chính xác:

- Generic trước create: Incident unpaired chuyển IN_PROGRESS; create bị state guard
  chặn. Create trước generic: pair PENDING/OPEN được tạo; generic bị guard mới chặn.
- Generic trước paired command: generic bị chặn; paired command thành công.
- Paired command trước generic cùng target: paired command thành công; generic
  thành no-op. Không thêm duplicate success events/audits.

Không có inconsistent committed pair trong race fixtures. Đây là serialization
qua current persisted state, không phải stale-rowversion contract. Không thêm
rowversion hoặc ExpectedRowVersion.

## I. Rollback / cancellation evidence

11 cases PASS với test-only EF interceptors:

1. Generic status/event/audit: lỗi sau actual SQL SaveChanges.
2. Generic: lỗi immediately before commit.
3–4. Corrective create: lỗi sau business save và audit save.
5. Paired submit-result/status/event/audits: lỗi sau SQL save.
6. Paired submit-result: lỗi before commit.
7–8. Technical log: lỗi sau log save và history/audit save.
9. Paired verify/closure/review/audits: lỗi sau SQL save.
10–11. Cancellation khi generic và paired worker thực sự đang chờ Incident lock.

Cancellation được kích hoạt sau khi SQL DMV xác nhận actual worker lock wait,
không chỉ dựa timer. OperationCanceledException phải propagate. Fresh contexts
đối chiếu toàn bộ footprint trước/sau; không partial business/history state.
Status/event/audits cùng một SaveChanges được kiểm như một real flush, không giả
vờ là nhiều SQL boundaries riêng. Không production fault switches.

## J. Existing runner regressions

Đã build và chạy riêng ba console runners hiện hữu, không sửa tests của chúng:

| Runner | HTTP requests | Assertions | Kết quả |
| --- | ---: | ---: | --- |
| Inventory | 246 | 510 | PASS; success=true, owned cleanup committed |
| Material Requests | 179 | 832 | PASS; 11 overlapping races, 10 rollback faults, cleanup completed |
| Replenishment | 273 | 1193 | PASS; 14 SQL races, 21 rollback cases, cleanup và legacy comparison PASS |

Inventory bảo vệ receipt/return/transfer/stocktake/adjustment/reversal/ISSUE,
locking/numbering/rollback. FE-09 bảo vệ approval/fulfillment/rowversion/ISSUE và
legacy fail-closed. FE-10 giữ toàn bộ acceptance baseline. GET smoke Incident và
Maintenance PASS; không tuyên bố full write regression của các module ngoài scope.

## K. Legacy / live-data preservation

Target được verify `KIENPC / HotelOpsDB` trước fixture writes. Chỉ dùng unique
`ICITEST_` prefix và owned Incident IDs. Cleanup trong finally xác minh ownership,
xóa đúng owned Work logs/works/Incident events/audits/incidents. Không reset/reseed.

Ordered JSON toàn bộ rows trước fixtures và sau cleanup phải khớp cho 15 bảng:
incidents, maintenance_works, maintenance_work_logs, incident_events, audit_logs;
stock_documents/lines/movements; material_requests/lines/fulfillments; ba bảng
material_replenishments/lines/receipt_lines; assets. Snapshots chỉ giữ trong memory,
không xuất dữ liệu nhạy cảm vào logs/source. Final comparison PASS.

Known live mismatch work 21 PENDING / Incident 21 IN_PROGRESS được giữ nguyên,
không repair hoặc tạo corrective history. New runner chỉ đọc các module khác;
regression runners hiện hữu dùng/dọn fixtures riêng theo contract đã có.

## L. Files changed / new

Modified:

- `src/HotelOps.Api/Services/Incidents/IncidentService.cs`.

New:

- `tests/HotelOps.IncidentMaintenance.IntegrationTests/HotelOps.IncidentMaintenance.IntegrationTests.csproj`.
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/Program.cs`.
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/ConcurrencyRollback.cs`.
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/Persistence.cs` — footprint,
  legacy comparison và owned cleanup, tách để runner dễ review.
- `tests/HotelOps.IncidentMaintenance.IntegrationTests/README.md`.
- `docs/testing/INCIDENT_CORRECTIVE_INTEGRITY_REPORT.md`.

Không migrations, DB/schema patch, scaffold, generated Entities/HotelOpsDbContext
changes, requirements edits, Auth/RBAC hoặc unrelated module modifications.

## M. Deferred Auth / RBAC

ActorUserId vẫn là temporary integration identity; không thay authenticated
identity, permission checks, role codes, branch/department scopes hoặc policies.
Assignee/verifier guards hiện hữu chỉ là business guards. Không hard-code role
hoặc branch IDs. Không thêm full cycle/SoD/reopen product rules từ draft SRS.

## N. Build / test totals

- `dotnet build HotelOps.slnx`: PASS, 0 errors. Final idle build với `--no-restore`
  PASS, 0 errors, chỉ NU1900 feed warning.
- New runner project build: PASS, 0 errors. Final acceptance **156 HTTP requests,
  351 assertions, 13 forbidden cases, 12 ordered SQL races, 11 rollback cases**.
  Cleanup và 15-table legacy comparison đều nằm trong final PASS.
- Ba existing runner results xem J. Không tìm thấy dedicated Incident/Maintenance
  tests khác trong tracked baseline.
- `dotnet test HotelOps.slnx --no-restore`: exit 0; solution không discover console
  runners. Business testing evidence là bốn runners đã thực thi riêng, không suy
  ra từ lệnh dotnet test.

Known warnings giữ nguyên: NU1900 khi vulnerability feed không truy cập được;
NU1902/NU1903 cho transitive System.Security.Cryptography.Pkcs 9.0.4 trong builds
Inventory/Replenishment. Không upgrade dependencies ngoài scope. Một build khi
runner còn giữ API DLL có MSB3026 copy retries, sau đó thành công; idle final build
không còn copy warnings. Loopback runner có HTTPS redirect-port warning hiện hữu;
không đổi production HTTPS configuration. Không unresolved build/test failures.

## O. Git diff / status verification

`git diff --check` sạch. Tracked diff chỉ IncidentService, 10 added lines.
Status đầy đủ chỉ production file và sáu new test/doc artifacts liệt kê ở L.
Staging trống; không secrets/connection strings, temp files, generated code,
migrations hoặc SQL patches. Không stage/commit/push. Branch và HEAD không đổi.

## P. Final verdict

Approved effective-status guard hoàn tất; no-op và non-corrective behavior giữ
nguyên. Paired workflow, SQL ordering, rollback/cancellation và ba required
regressions PASS. Known live mismatch vẫn cần tác vụ xử lý dữ liệu riêng nếu
developer muốn; fix này chỉ ngăn generic endpoint tạo thêm drift.

READY TO COMMIT: YES
