# FASTTRACK-142A — checkpoint 07: UC047 persistence

Baseline `a50640602dd238e7d0ac300acf581eeb9a75b268`; canonical branch `integration/dev1-dev2-dev3-unblock-20261114`. Checkpoint trước được phân loại PROGRESS: source đã push, runtime/test evidence đã thực thi. Sprint vẫn ACTIVE.

## Source và chức năng

Đối chiếu candidate `c235e23e5f52428554490cab2fc93c65164533ff` với `d8d9195`: có hai file source/schema bổ sung chưa nằm trong canonical. Khôi phục chọn lọc `ReinspectionComponentPersistence.cs` và `scripts/database/fe05-fast014/01_ReinspectionCandidate.sql`; không merge cả nhánh hoặc copy lại source đã có.

Component thực hiện INSERT binding chính xác inspection/rework/cycle/version, khởi tạo kết quả từng defect đã chọn, UPDATE Fixed/Not Fixed, ghi audit trong transaction của caller và trả revision inspection/binding/rework. Mỗi lần thay đổi kết quả tăng aggregate revision qua trigger. Original checklist version và assigned version được lưu riêng. Không chọn rework gần nhất, không backfill và không tự kết luận cycle khi lưu kết quả.

Điều chỉnh dependency `InspectionConclusionSql` không tồn tại trong canonical bằng kiểm tra SQL Server, caller SERIALIZABLE transaction, không ambient transaction, ChangeTracker sạch. Guard runtime được giới hạn vào đúng instance/database GUID/run marker của sprint và schema marker cùng ba trigger đang enabled. Không nhận broad database name pattern. Đây vẫn là internal component, không có controller/DI/action công khai hoặc authoritative receipt; không mở các release gate conclusion/notification/projection.

SRS UC047 yêu cầu mỗi defect có Fixed/Not Fixed, notes/photos, assigned inspector, audit cùng transaction và kết luận Passed/Failed kèm room projection/notification. Checkpoint này triển khai storage Fixed/Not Fixed; notes/photos và kết luận đầy đủ còn thiếu. Khác biệt giữa hai checklist version được bảo toàn trong component; chưa tự quyết định chính sách UC041 chọn phiên bản cho nghiệp vụ.

## Kiểm chứng trực tiếp

| Lệnh | Kết quả | Evidence |
|---|---|---|
| `dotnet build HotelOps.slnx` | PASS; 0 warnings/errors | `checkpoint07-build-final.log.txt` |
| `dotnet test HotelOps.slnx` | Exit 0; không discover test; không tính suite acceptance | `checkpoint07-solution-test.log.txt` |
| `dotnet run --project tests/HotelOps.Inspection.LoaderTests` | 64 PASS, 0 FAIL; doubles, không SQL | `checkpoint07-loader.log.txt` |
| `dotnet run --project tests/HotelOps.Inspection.DomainTests` | 45 PASS, 0 FAIL; in-memory | `checkpoint07-domain.log.txt` |
| `dotnet run --project scripts/verification/CanonicalDatabase/ModelProbe -p:BuildProjectReferences=false -- --sprint-reinspection docs/verification/DEV3-FASTTRACK-142A/reinspection-verified.json` | 18 SQL cases PASS; từng case rollback schema/data | `reinspection-verified.json` |

18 cases: Fixed, Not Fixed, separate original/assigned version, missing remediation, inspector/cleaner conflict, duplicate binding, stale binding/inspection/rework, missing/foreign/duplicate defect decision, revoked permission/session, wrong branch, changed cycle revision, disabled trigger, injected audit failure. Dùng credential/session/permission implementation thực và role HK_SUPERVISOR hiện có trong branch tổng hợp; không dùng fake permission grant. Trial gọi internal component qua reflection, không gọi HTTP endpoint. Không có browser/HTTPS/concurrency acceptance mới ở checkpoint này.

Lần đầu fixture thiếu `requested_at` bắt buộc; rollback thành công, sửa fixture. Lần mở rộng thử thay version của inspection đã tạo bị trigger immutability từ chối; giữ nguyên trigger và tạo inspection mới với version khác ngay từ đầu. Giữ `reinspection-first.json`, `reinspection-second.json` (14 cases PASS), `reinspection-expanded.json` (fixture failure) và `reinspection-verified.json` (18 cases PASS). Không biến lỗi fixture thành PASS hoặc sửa schema để lách trigger.

Frontend không thay đổi: npm ci/typecheck/build/Auth-Router không chạy lại. Bằng chứng checkpoint06 (179 router PASS, frontend build/typecheck PASS, 22 HTTPS checks) là lịch sử, không cộng vào kết quả mới.

## SQL safety và giới hạn acceptance

Instance `meita`, database `HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, run marker `cc78c13ffc124efcad67b3d804baa84c`. Fixture kiểm tra exact identity và empty inventory trước mutation. Harness pin SHA256 của SQL sau chuẩn hóa BOM/CRLF: `B31B633A9E5832F79DDB01D2EDB26DBBB29DC3CBE15B591C995A0FD301B704B7`.

Script candidate gốc giữ historical target guard. Harness không thực thi wrapper đó: sau guard exact GUID của sprint, chỉ dùng phần DDL được pin giữa BEGIN/COMMIT trong **transaction đang sở hữu**, không gọi COMMIT. Mỗi case rollback cả DDL, property/trigger, remediation columns và synthetic rows. Không cài schema tồn tại lâu dài. `checkpoint07-cleanup.log.txt`: users/branches/audits/sessions = 0; hai component tables và remediation column = NULL.

Không mutation shared HotelOpsDB/ImportReview, không sửa generated EF entities/mappings/migration, không đổi permission catalog. Quyền Windows SQL hiện có dùng cho disposable component trial; không phải restricted-principal/N01 L1/L2 acceptance. Schema/trigger integrity toàn diện, production deployment, immutable evidence bytes và durable notification graph vẫn cần thực hiện trước release.

## Receipt

`TASK_ID=DEV3-FASTTRACK-142A`; `CHECKPOINT=07_ACTIVE`; `ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114`; `INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e`. CURRENT_SHA/COMMIT_SHAS/PUSH_STATUS ghi trong receipt sau delivery để tránh self-reference.

`SOURCE_IMPLEMENTED=UC047_INTERNAL_EXACT_BINDING_AND_PER_DEFECT_PERSISTENCE`.

`CHANGED_PATHS=src/HotelOps.Api/Services/Housekeeping/ReinspectionComponentPersistence.cs; scripts/database/fe05-fast014/01_ReinspectionCandidate.sql; scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintReinspectionProbe.cs,SprintReinspectionFixture.sql}; docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-07.md,UC_STATUS_MATRIX.md,checkpoint07*,reinspection*.json}`.

`TESTS_EXECUTED=18_SQL_PASS;64_LOADER_PASS;45_DOMAIN_PASS`; `BUILD_STATUS=BACKEND_PASS_0_WARNINGS;FRONTEND_NOT_RERUN`.

`FE05_STATUS=PARTIAL;REINSPECTION_STORAGE_COMPONENT_SQL_VERIFIED`; `UC006_UC010_STATUS=PRIMITIVE_COORDINATION_SUPPORTED_GRANT_PREVIOUSLY_VERIFIED;DEACTIVATION_REVOKE_PENDING`; `FE11_STATUS=PARTIAL`; `FE13_STATUS=PARTIAL`; `FE14_STATUS=PARTIAL`; `FE15_STATUS=AUDIT_READ_IMPLEMENTED;FULL_ACCEPTANCE_PENDING`.

`ISOLATED_SQL_STATUS=18_CASES_SCHEMA_AND_DATA_ROLLBACK_VERIFIED`; `HTTPS_API_STATUS=NOT_RUN_THIS_CHECKPOINT`; `E2E_VERIFIED_WORKFLOWS=NONE_NEW;INTERNAL_SQL_COMPONENT_ONLY`.

`UC_MATRIX_TOTAL=144`; `UC_COMPLETE_VERIFIED=0`; `UC_SOURCE_READY=0`; `UC_PARTIAL=144`; `UC_BLOCKED=0`.

`CRITICAL_BLOCKERS=PUBLIC_FE05_BINDING_AND_CONCLUSION;NOTES_PHOTOS;FAILED_REWORK_STATE_CONTRACT;NOTIFICATION_PROJECTION_REPLAY;ACTUAL_UC006_010_WRITERS;FE11_13_WORKFLOWS;FE14_REPORTS;BROWSER_SQL_E2E`.

`NEXT_IMPLEMENTATION=Complete FE05 lifecycle/notification/projection composition using confirmed SRS; integrate actual UC006/010 competing writers; implement confirmed FE11/13 and FE14/report workflows`. Không chờ DEV1/DEV2, không tuyên bố UC047 COMPLETE từ component tests.
