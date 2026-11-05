# Task86 — acceptance matrix

Không dùng tổng checks để thay điều kiện nghiệm thu từng luồng. Kết quả SQL/HTTPS cập nhật theo receipts ở cuối Task86; không tính framework 0 cases là PASS.

| Nhóm | Bằng chứng / điều kiện | Trạng thái ban đầu |
|---|---|---|
| Solution build | build-sql-ready.log, current production source | PASS, 0 errors; NU1900 |
| DI/HTTP input/ETag/SQL translation | DI_BOUNDARY_RECEIPT.json và DI_INTEGRATED_RECEIPT.json | 39 checks PASS mỗi lần; không cộng hai lần thành 78 |
| Security regression | postdi-Authorization/ResourceAuthorization/PermissionService/CurrentUserAccessor.log | 93+49+43+14 = 199 PASS; doubles/core/cookie middleware, không production SQL sessions |
| UC037 component | UC037_RECEIPT.json | 14 PASS, isolated timeline boundaries |
| UC038 component | UC038_RECEIPT.json | 26 PASS, DI/input/EF translation |
| UC142 component | UC142_RECEIPT.json | 19 PASS, DI/static/input |
| Framework | framework.log | Exit 0, no discovered cases; NOT AVAILABLE |
| Fresh schema / rowversion | sql-prepare-approved.log | PASS new trial only; prerequisite guards, transactional DDL rollback, forward twice |
| Create + update live | HTTPS_SQL_RECEIPT.json | Create/read/update/validation/no-op/stale cases PASS trong 83 checks; không coi toàn harness đầu PASS |
| SQL audit rollback/no partial commit | trial CHECK constraint + exact branches/audit fingerprint before/after | PASS; SQL_ORACLE_RECEIPT.json thêm 13 metadata/audit checks PASS |
| Concurrent duplicate / update | Hai actor, status set 201/409 và 200/412; persisted count | PASS, không retry/deadlock được tính thành công |
| Revoked/expired/inactive/restricted | Real Cookie credential login + SQL fixture state | PASS theo receipt đầu và HTTPS_RESUME2_RECEIPT.json |
| Cross-branch/global grant | branch-bound SYS_ADMIN assignment không mượn global permission | PASS denial; BRANCH/OWN grants cho MASTER_DATA_MANAGE không có trong catalog nên không tự tạo để test |
| Revoke during observed lock wait | HttpsResume2Program.cs: sys.dm_exec_requests thấy request bị block tại actor lock, revoke role/session rồi commit/release | PASS, cả 403/401 và no branch/audit mutation; không phải test sau khi đã giữ business Branch lock |
| Active rooms block | BR001 trial room + fail-closed endpoint | PASS 409; empty branch vẫn 503, không mutation |
| Stock/open task/incident/request/multiple blocker SQL fixtures | Facts reader đã có source nhưng v1 harness chưa dựng đủ fixtures | NOT RUN |
| Successful deactivate + audit | Chưa có safe mutation | BLOCKED_EXTERNAL / DEACTIVATION_CONCURRENCY_BLOCKED |
| Deactivate/create deterministic races | DEV2 writers chưa có retained active-branch guard được chứng minh | BLOCKED_EXTERNAL, NOT RUN |
| Terminal-state/PM complete scope | Reader luôn báo ScopeResolutionComplete=false | BLOCKED_EXTERNAL |

Trạng thái cuối UC012: PARTIAL kể cả Create/Update live PASS. Acceptance bắt buộc cho toàn bộ UC chưa đủ; earned points=0.

## Execution history không xóa lỗi

| Receipt | PASS | FAIL | Kết luận |
|---|---:|---:|---|
| HTTPS_SQL_RECEIPT.json | 83 | 1 | Lỗi kỳ vọng harness: dùng SYS_ADMIN cho UC038 nhưng catalog không cấp CLEANING_VIEW/INSPECTION_VIEW; actual 403 đúng authorization |
| HTTPS_RESUME_RECEIPT.json | 37 | 1 | UC037/038 allowed supervisor và cross-branch PASS; runner đọc @@SPID smallint bằng GetInt32 gây InvalidCast trước ca lock-wait |
| HTTPS_RESUME2_RECEIPT.json | 51 | 0 | Runner cast CONVERT(int,@@SPID), giữ cùng fixture set, không reset; đóng regression, expiry/inactive/revocation và observed lock-wait |

Không cộng các lượt trùng thành số unique acceptance checks. Hai receipt lỗi giữ nguyên; không tuyên bố toàn Task86/full UC PASS. UC142 HTTPS list/detail nằm trong receipt đầu; UC037/UC038 allowed/denied regression trong lượt bổ sung cuối. SQL oracle là SELECT-only persisted-data verification, không thay HTTPS.
