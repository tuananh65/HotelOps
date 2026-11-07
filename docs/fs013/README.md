# FS-013 — FE-04 Cleaning Lifecycle development checkpoint

## 1. Task status

Ưu tiên FE-04 UC028–UC038 theo chỉ đạo mới của Project Leader. FS-013 Dashboard cũ được hoãn; chưa bắt đầu FE-14/FE-15. Đây là checkpoint phát triển, chưa phải nghiệm thu toàn bộ module.

Preflight: nhánh `feature/dev1-room-ops`, HEAD và remote cùng `2662a1c7cff12edb30bf48515c25ba3a901016a4`, working tree sạch trước FS-013. Source/evidence FS-001–FS-012 được giữ nguyên. Hai archive cũ được kiểm tra SHA256 trong `FROZEN_ARCHIVE_HASHES.json`; không tạo ZIP mới.

## 2. UC đã triển khai

- UC029: giữ implementation/SQL write boundary từ FS-012, thêm opt-in riêng cho trial FS-013; không đổi thành accepted shared Notification.
- UC030: thêm GET execution metadata và POST reassign; kiểm tra quyền branch, current ETag, assignee/shift, lý do ≥10 ký tự, deadline UTC. Kết thúc assignment cũ bằng status `REASSIGNED` đã tồn tại trong schema; đóng interval cũ khi đang làm, assignment mới, task/readiness, audit và hai notification intent cùng transaction. Durable replay theo actor/key/fingerprint; retry/recovery không tạo mutation mới khi commit outcome chưa rõ.
- UC031: thêm POST start và React flow cho ASSIGNED/current assignee; Working workday, tối đa một open interval, actual start lần đầu, item snapshot, readiness/effective block và audit trong transaction. **Resume chưa hoàn tất**: chưa có persisted blocker resolution contract; trả lỗi có kiểm soát thay vì giả định đã đóng blocker.
- Đã đối chiếu readiness UC033–UC038, thứ tự và acceptance gaps tại `UC033_UC038_READINESS.md`.

## 3. Backend/frontend/SQL

Backend giữ single-project, controller chỉ HTTP/DTO; không sửa generated entity, DbContext mapping, migration, role grants dùng chung hoặc global DI. Callback tùy chọn trong Batch chỉ được cleaning lifecycle mới dùng để giữ thứ tự user → WorkTime → branch/catalog locks.

Frontend có execution panel, Start, Reassign, metadata/candidate phân trang từ server, CSRF + If-Match, giữ nguyên command/key/tag khi retry và navigation từ task/monitor. Không dùng prototype data thay cho API.

SQL chỉ áp dụng lên disposable `HotelOps_Canonical_20261106_f130001_Fresh`, owner `DEV1_FS013`; không ghi database trial cũ/shared/production. Script 11 dựng Workday/WorkInterval theo model hiện có trong trial và bổ sung reassign modules/intents. Đây chưa phải accepted WorkTime deployment SQL. Chi tiết tác động/các giới hạn nằm trong `INTEGRATION_NOTES.md`.

## 4. Unit Tests và regression thực tế

| Kiểm tra | Kết quả | Evidence |
| --- | --- | --- |
| `dotnet build HotelOps.slnx` sau sửa C# | PASS, 0 warning/0 error | BACKEND_BUILD_SNAPSHOT.log |
| `dotnet test HotelOps.slnx` | Exit 0; solution chỉ chứa API, không dùng số này làm số test | SOLUTION_TEST_FINAL.log |
| xUnit project thật | 179 PASS / 0 FAIL / 0 SKIP; 54 source methods | XUNIT_CONTRACT_FIX.log, evidence/fs013.trx, UNIT_TEST_INVENTORY.json |
| Node unit/contracts | 50 PASS, source methods/commands/evidence từng ca | NODE_TESTS.log, NODE_TEST_INVENTORY.json |
| Browser regression cũ, API interception | 47 PASS | INTERCEPTION.log |
| Browser Start/Reassign mới, API interception | 7 PASS, gồm phân trang/retry | UI_INTERCEPTION_PAGING.log |
| SQL Notification proposal component | 6 PASS, rollback | NOTIFICATION_SQL_RESULTS.json |
| SQL Reassign component | 6 PASS, rollback | REASSIGNMENT_SQL_RESULTS.json |
| .NET/SQL commit/cancellation component | 4 PASS; fresh-context retry exactly once; gate doubles | REASSIGNMENT_COMMIT_PROOF_FINAL.json |
| Frontend production build | PASS, có warning bundler | FRONTEND_BUILD_PAGING.log |
| Snapshot EF/ADO parity | 3 PASS, đầy đủ field/value/array order | SNAPSHOT_PARITY.json |

Inventory ghi từng execution, method, input, source hash, command, PASS/FAIL/SKIP và TRX evidence. Có 42 ca mới so với baseline 137; tổng regression không tự quy đổi thành coverage hay tỷ lệ hoàn thành FE-04. Các TRX trước sửa giữ riêng trong `evidence/`.

## 5. Live E2E

Trạng thái cuối được cập nhật trong `LIVE_VERIFICATION_STATUS.json` và receipt từng suite. Dùng HTTPS với certificate có sẵn, cookie/CSRF thật, API + SQL trial thật; không bỏ qua TLS. Workday được chuẩn bị bằng participant foundation như component fixture, **không xác minh public Workday HTTP/UC139**.

Cleaning lifecycle: **3/3 PASS**, `LIFECYCLE_LIVE_ATTEMPT4.log/json`. Bao gồm No-Workday/one-open-interval không mutation, current-owner/ETag, effective block, giữ actual start, đóng interval cũ, replay sau lifecycle tiến tiếp, đổi payload cùng key bị 409, rollback mandatory staging, hai recipient + retry/dedup, receipt tối thiểu cho owner cũ/source read 403, và concurrent Start/Reassign 200/412 với một assignment/interval nhất quán.

Regression UC028/029/032: **12 case identities đạt PASS sau targeted retests**, không phải một lượt clean 12/12. Lượt đầu 9 PASS/3 FAIL; retest 1 có 1 PASS/2 FAIL; retest 2 có 2 PASS/0 FAIL. Các failure là snapshot/persistence helper timeout 60 giây. Chỉ tối ưu ba snapshot read-only của harness sang ADO.NET sau exact parity 3/3 với EF; giữ nguyên assertions, timeout và backend nghiệp vụ. Xem `REGRESSION_AGGREGATE.json` và `SNAPSHOT_HARNESS.md`.

Default-off: **1/1 PASS**, cả Assign/Reassign trả 503, capability UNAVAILABLE, UI không cho lưu và snapshot không đổi (`DEFAULT_OFF_RESULTS.json`). Postflight không còn fault constraints, user fixture inactive, role bị disable hoặc branch access bị revoke; ba process owned đã dừng. Không reset/drop trial.

Lần live lifecycle đầu có 3 FAIL: Reassign IN_PROGRESS gặp tracker/session-gate 500; hai ca sau bị ảnh hưởng bởi interval còn mở. Receipt `LIFECYCLE_LIVE_ATTEMPT1.log/json` được giữ. Báo cáo JSON cũ bị worker restart ghi đè một phần; log đầy đủ giữ cả ba failure. Harness đã sửa cách ghi receipt xuyên worker. Chạy lại dùng Reassign API để đóng interval còn lại và EndWorkday participant để kết thúc fixture day theo luồng forward; không reset/xóa lịch sử.

## 6. Integration conflicts

Notification vẫn `PROVISIONAL_NOT_ACCEPTED`, default OFF, giới hạn đúng trial/owner; A01/A02 chưa được coi là accepted shared implementation. FS-012 source findings được carry forward, không audit vòng lặp lại. Workday public controller còn inactive, deployment DDL dùng chung chưa reconcile. Legacy room status/history và Reception/Operation Manager transition permissions chưa được tự suy diễn. UC033 blocker resolution và supervisor notification contract còn thiếu.

## 7. FE-04 completion

Module chưa hoàn tất; không tự chấm 80–90%, TESTER_READY hoặc TESTER_ACCEPTED. UC031 Resume, UC033–036, cancellation UC037 và full turnover/history acceptance UC038 còn critical gaps. Chưa tạo handover ZIP hay gửi yêu cầu Tester.

## 8. Git commit/push

Đã push các source checkpoints `9eaf2c7`, `5213d81`, `b77f872`, `336500e` lên `feature/dev1-room-ops`. Evidence cuối được commit/push riêng sau validation; source identity ghi tại `SOURCE_IDENTITY.json`. Không force-push, tạo PR hoặc merge develop/main.

## 9. Merge readiness / lỗi và warning

**NOT_READY_FOR_MERGE.** Chờ acceptance các UC còn lại, shared SQL/Notification/WorkTime contracts, security critical paths và review/Project Leader approval.

Đã sửa lỗi SQL syntax lúc cài script 11, status assignment gây unique-index 2601, selector accessibility, tracker hygiene và ETag sai định dạng gây live 500. Guard mới kiểm tra durable response identity/state/tag trước commit/HTTP; thêm 10 unit cases cho contract này. Command provisional cũ có tag lỗi được giữ nguyên, không sửa ledger ngược. Sáu lần readiness ban đầu thất bại do kết nối/startup .NET; sau đó component .NET và HTTP đã kết nối được. Thử shared-memory transport không đạt đã được bỏ khỏi source cuối. Một lượt rebuild harness trùng gateway gây hai MSB3026 retry warning; lượt build sạch sau đó đạt 0 warning/0 error. Không che các receipt ban đầu.

Warning còn ghi nhận: NuGet vulnerability feed NU1900 trong các lượt xUnit; React Router `use client`/chunk >500KB ở frontend build; ba EF mapping/index warnings RoomTypeBranchV1 lúc startup (có từ baseline, không sửa unrelated mapping), và warning bounded Take/First thiếu OrderBy ở query mới (cardinality check vẫn từ chối assignment không duy nhất). Browser runner có NO_COLOR/FORCE_COLOR và PowerShell stderr wrapper diagnostics; exit code và kết quả từng suite được ghi riêng. Fault injection commit là component interceptor, không phải network disconnect thật. Chưa chứng minh full production least-privilege hay arbitrary privileged direct-DML resilience.

## 10. Recommended next task

Tiếp tục UC033 blocker persistence + supervisor notification contract và transaction; sau đó hoàn thiện Resume UC031, UC034 completion, rồi UC035/036 rework và acceptance UC037/038. Reconcile Workday deployment/public entry và room status/history trước shared acceptance. Chỉ sau FE-04 review, được PL phê duyệt và merge thành công mới chuyển FE-14/FE-15.

Danh sách đầy đủ file tạo/sửa nằm trong `CHANGED_FILES.md`; source identity được ghi trong `SOURCE_IDENTITY.json`.
