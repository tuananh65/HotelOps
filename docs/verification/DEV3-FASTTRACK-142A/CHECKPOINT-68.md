# FASTTRACK-142A checkpoint68 — verification cho task liên kết Internal Request

**UC hoàn thành mới: 0.** Tổng vẫn **14 COMPLETE_VERIFIED / 130 PARTIAL / 144 UC**. UC105 tiến thêm một workflow đầy đủ nhưng giữ PARTIAL vì requester của các nguồn legacy MAINTENANCE/INSPECTION/CLEANING chưa có contract đã xác minh. Không suy đoán từ inspector hay người đang được phân công.

Đã triển khai requester resolution theo source Internal Request được UC102 nêu rõ. API kiểm tra cùng branch và các liên kết department/location/incident; chặn requester gốc, người tạo task, submitter và performers tự xác minh. Source được đọc lại trong transaction verification; thay requester sau khi mở form vẫn bị kiểm tra, kể cả khi ETag task chưa đổi. Audit và event lưu requester chính thức, source identity và người tạo task riêng biệt, bảo toàn snapshot cũ.

**Workflow hoàn chỉnh đã kiểm chứng:** task liên kết Internal Request → Department Head Return → cùng executor Start/Submit lại → Department Head Accept → Closed → assignee mở notification và tải bằng chứng riêng tư. Không sửa lifecycle của Internal Request. Không thay API/DTO, frontend hoặc schema.

[Runtime trực tiếp](checkpoint68-linked-trial1.json) PASS: **102 records**, gồm 34 submission assertions, 34 verification assertions, 13 requester assertions và **42 browser stages**. Source sai branch/department hoặc không xác định trả503; requester/creator tự xác minh trả403; không ghi decision/state khi bị từ chối. [Worker thật](checkpoint68-verification-worker.json) PASS, một delivery, source không đổi. Chi tiết nằm trong [acceptance ledger UC105](UC105_ACCEPTANCE.md).

## Validation và cleanup

- [Backend build](checkpoint68-backend-build.log.txt), [probe build](checkpoint68-probe-build.log.txt): PASS, 0 warning/error.
- [Backend regression với assembly mới](checkpoint68-backend-tests-final.log.txt): **588 PASS, 11 SKIP, 0 FAIL**. [Solution test](checkpoint68-solution-test.log.txt) exit0 nhưng không có test project trong solution; không tính coverage từ exit code đó.
- [Focused frontend FE11](checkpoint68-task-tests.log.txt): **44 PASS**. Không chạy lại npm ci, typecheck, frontend build hoặc Auth/Router trong C68 vì không đổi frontend/lockfile; C67 có bằng chứng tương ứng, không coi đó là kết quả thực thi mới.
- Runtime vẫn có EF warnings 20602 (RoomType index) và 10102 (Take không OrderBy) đã tồn tại; không báo runtime không có cảnh báo.
- C68 chỉ tái sử dụng các capability disposable đã có từ C66. Không thêm bảng, migration, generated entity hay physical status.
- [SQL cleanup](checkpoint68-cleanup-final.txt), [files/hosts](checkpoint68-files-host-cleanup.json): clone và backup đã xóa, source phục hồi, không còn private files/listener. Shared HotelOpsDB không bị mutation.

Runtime không còn blocker cho manual/Internal Request verification trong trial này. Các nguồn legacy chưa chứng minh requester vẫn fail-closed; đây là gap cụ thể, không chặn các workflow khác.

## Ba việc coding tiếp theo

1. UC104 Assign/Reassign: manager phân công lại task sau Decline, đóng interval của assignee cũ trong transaction và thông báo đúng scope.
2. UC102 Create Task/criteria/series: thay synthetic preconditions bằng public workflow theo GBR123–126.
3. UC106 Reopen/cycle và các nguồn legacy UC105: bảo toàn history và segregation; sau đó tiếp tục UC006/010 và FE13.

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=68
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_REMOTE_SHA=72263b266fada65663ffbe9c910a4d8abde7b2be
NEWLY_COMPLETED_UCS=NONE
IMPLEMENTED_FUNCTIONALITY=Internal Request requester provenance; linked task Return/Accept/Closed; immutable requester history and actor separation
SQL_HTTPS_STATUS=PASS on owned clone; cleanup complete; shared database untouched
SPRINT_STATUS=ACTIVE; deadline unchanged 2026-11-16 17:50 UTC

Danh sách changed paths được lưu cùng receipt. Git delivery receipt ghi commit và remote SHA sau khi push bình thường.
