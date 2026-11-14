# FASTTRACK-142A — checkpoint 01

Sprint bắt đầu 2026-11-14 17:50 UTC (2026-11-15 giờ địa phương). Deadline 48 giờ: 2026-11-16 17:50 UTC. Đây là checkpoint đang triển khai, không phải nghiệm thu toàn dự án.

## Source và kiểm chứng

- Baseline local/remote sạch: `8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e`, branch `integration/dev1-dev2-dev3-unblock-20261114`. Fetch xác nhận không tiến thêm.
- Đã đọc artifacts 141A và yêu cầu mới. Matrix canonical tại `UC_STATUS_MATRIX.md` có đúng 144 ID duy nhất, bổ sung UC135–137 FE15; bằng chứng 141A được giữ là lịch sử.
- Khôi phục chọn lọc bốn file `ReinspectionDecisionValidator`, `ReinspectionLifecycleCandidate`, `ReinspectionSourceCandidate`, `ReinspectionPersistenceCandidate` từ `d8d9195e13e0a5edb1edc79050ee56f24525209e`. Không merge cả nhánh, không thay entities, mapping hoặc mở write gate.
- Đối chiếu SRS 5.9 UC047: từng defect phải Fixed/Not Fixed; tất cả Fixed mới đề xuất PASSED/COMPLETED; còn lỗi đề xuất FAILED/REWORK_REQUIRED. Lifecycle ở đây chỉ là domain plan, chưa là trạng thái SQL được phép ghi.
- Source adapter dùng existing SERIALIZABLE read session, INSPECTION_PERFORM, assigned inspector, cleaner separation, cycle identity và hai revision. Persistence preparation chỉ tạo plan, không INSERT/UPDATE. Exact binding, result storage, checklist policy, notifications/projection vẫn chưa được chấp nhận.
- Thêm 25 domain checks và hai guard checks cho adapter thật không có transaction. Không mô phỏng các checks này thành concurrency/persistence acceptance.

| Lệnh thực thi mới | Kết quả | Bằng chứng |
|---|---|---|
| `dotnet build HotelOps.slnx` | PASS; 0 warnings/errors | backend-build.log.txt |
| `dotnet run --project tests/HotelOps.Inspection.LoaderTests` | 63 PASS / 0 FAIL; 27 mới | inspection-loader.log.txt |
| `dotnet run --project tests/HotelOps.Inspection.DomainTests` | 45 PASS / 0 FAIL | inspection-domain.log.txt |
| `dotnet test tests/HotelOps.Dev1.RegressionTests --filter 'FullyQualifiedName~Inspection\|FullyQualifiedName~Housekeeping'` | 3 PASS / 0 FAIL | fe05-xunit.log.txt |
| `dotnet test HotelOps.slnx` | exit 0, không discover tests | solution-test.log.txt |
| `sqlcmd -S localhost -E -C -b -d HotelOps_DEV3_142A_20261114_cc78c13f -i docs/verification/DEV3-FASTTRACK-142A/isolated-rollback-check.sql` | synthetic INSERT + ROLLBACK + empty-row cleanup PASS | isolated-rollback-check.log.txt |
| `dotnet dev-certs https --check --trust` | existing trusted localhost certificate, expires 2027-11-01 | trực tiếp tool output; không tạo/sửa certificate |

Frontend không thay đổi; không chạy lại full green suite ở checkpoint source nhỏ này. Không lấy kết quả 141A làm lần chạy mới.

## SQL/HTTPS

Read-only inventory xác nhận default `meita` (SQL Server 16.0.1000.6) và `meita\MSSQLSERVER01`. Instance thứ hai có HotelOpsDB/HotelOps_ImportReview; không thay đổi chúng. Lỗi ODBC sandbox ban đầu được giải quyết bằng execution phù hợp, không phải blocker vật lý cuối cùng.

Runner `scripts/verification/Provision-Dev3SprintTrial.ps1` tạo target mới `HotelOps_DEV3_142A_20261114_cc78c13f` trên default instance, cấm reuse database và tách schema body khỏi lệnh target HotelOpsDB. DDL nằm trong transaction; provisioning để lại empty owned database nếu lỗi. 72 bảng được tạo, không seed tài khoản hoặc dữ liệu thật.

Ownership marker `DEV3.Sprint142A.Run=cc78c13ffc124efcad67b3d804baa84c`. GUID ổn định sau schema commit: `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, xác minh lại trong rollback log. Receipt provisioning gốc ghi GUID zero ngay sau CREATE trước lần sử dụng đầu tiên; giữ nguyên để minh bạch. Runner đã sửa để kiểm tra GUID sau commit cho lần sau. Không dùng GUID zero làm authority.

Transport provisioning: local shared memory, `encrypt_option=FALSE`; không gọi đây là SQL TLS acceptance. HTTPS listener chưa chạy. Startup còn thiếu `users.row_version`; FE05 thiếu `room_cleaning_cycle_state` và `cleaning_rework_defects`. Cần rà soát/apply đúng prerequisite scripts và kiểm tra toàn bộ EF projection trước API trial. Chưa có E2E nghiệp vụ.

Synthetic branch trong rollback check không còn sau transaction; users vẫn rỗng. Database/schema riêng được giữ để tiếp tục sprint, chưa drop. Không có credentials, private certificates hay database files trong Git.

Các lần chạy provision đầu tiên bị execution policy và PowerShell connection-builder property lỗi trước database creation. Process-only script execution permission được dùng; không đổi machine policy. Property assignment sửa sang canonical connection-string keys. Không giấu các lỗi này.

## Trạng thái và phần tiếp theo

`TASK_ID=DEV3-FASTTRACK-142A`; `CHECKPOINT=01_ACTIVE`; `UC_MATRIX_TOTAL=144`; `UC_COMPLETE_VERIFIED=0`; `UC_SOURCE_READY=0`; `UC_PARTIAL=144`; `UC_BLOCKED=0`.

Các số này dùng phân loại bảo thủ: mọi UC còn acceptance/implementation chưa hoàn tất; SOURCE_READY yêu cầu toàn bộ UC, không chỉ một fragment. Không có UC được coi là blocked do DEV1/DEV2. Blocker kỹ thuật cụ thể ghi trong matrix; chưa có bằng chứng hết đường triển khai độc lập.

FE05=PARTIAL/OFFLINE_PASS; UC006/010=baseline offline only, SQL coordination pending; FE11/FE13=PARTIAL, chưa sửa ở checkpoint này; FE14/FE15=candidate recovery pending. BUILD=PASS; SQL=BASE_SCHEMA_AND_ROLLBACK_SMOKE_PASS; HTTPS=TRUSTED_CERT_AVAILABLE/API_NOT_RUN; E2E_VERIFIED_WORKFLOWS=NONE.

Tiếp tục: (1) đối chiếu binding/per-defect storage FE05 và hợp nhất phần conclusion có đủ dependencies; (2) bổ sung schema prerequisite trên đúng trial, kiểm tra EF projection và chạy HTTPS; (3) dùng runtime đó kiểm tra actor/receiver coordination UC006/010, rồi FE11/13 và FE14/15. Không tuyên bố sprint hoàn thành tại checkpoint này.
