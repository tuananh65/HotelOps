# Kiểm thử HotelposTest theo SRS — 20/10/2026

**Authentication có hoạt động với SQL Server thật nhưng CHƯA đáp ứng đầy đủ SRS. Không đề nghị coi bản này là release/main-ready.** Chưa sửa source ứng dụng hoặc schema.

## A. Feature Inventory

- **3 nghiệp vụ Backend public có logic thật:** Login, Logout, Change Password.
- **4 action route hỗ trợ/template:** session, csrf, database health, WeatherForecast. Tổng **7 endpoint action**; WeatherForecast không phải nghiệp vụ SRS.
- **4 khả năng FE–BE đã thử chạy:** Login, Logout, Change Password, Profile/restore-session. Forgot Password là local instruction theo SRS, không cần API gửi email/reset token.
- Internal temporary provision/reset service được thử trực tiếp; UC-004/005, User Management/RBAC chưa hoàn chỉnh.
- Đã lập inventory toàn bộ **80 screen**, gồm trạng thái IMPLEMENTED/PARTIALLY IMPLEMENTED/NOT IMPLEMENTED; mock helper files được ghi MOCK ONLY và không dùng thay SQL.

[A-FEATURE-INVENTORY.md](A-FEATURE-INVENTORY.md) · [SRS-FE01-FLOWS.md](SRS-FE01-FLOWS.md)

## B. Test Case Report

**114 ca mới đã chạy:** 93 ca HTTP/UI/SQL và 21 ca service/helper có SQL snapshots. **94 PASS, 20 FAIL.** Thêm **3 BLOCKED và 7 NOT TESTED** trong kế hoạch 124 ca; không tính các ca chưa chạy là PASS.

Bộ Node tests cũ chạy riêng: **37 tests, 26 PASS, 11 FAIL** vì fixture hợp đồng auth cũ. Không trộn số đó vào 114 ca mới. Có lượt 87 ca trước hiệu chỉnh công cụ null-array/timing; lưu attempt-1 làm lịch sử, không cộng lặp vào số testcase cuối. Các lần setup bridge lỗi không được coi test ứng dụng.

[B-TEST-CASES.md](B-TEST-CASES.md) · [all-test-cases.json](all-test-cases.json) · [final-summary.json](final-summary.json)

## C. Coverage

[C-COVERAGE-MATRIX.md](C-COVERAGE-MATRIX.md) đối chiếu UC, GBR-001/002/003/007/008/019–024, MSG01/02/03/08/11–15, SYS-PAR-01–06. Không tuyên bố 100%.

Core credentials, five-failure lock30m, generic credential errors, SQL session check/sliding, normal logout, complexity/hash/revoke-all/temporary promotion và internal 24h/one-time handoff đã thử. Còn thiếu effective grants, permission matrix/scope backend, một số exception/message/audit; không chạy đủ 10 actor, real-time 30m/12h, full boundary/concurrency/fault-injection/production. Các ca API-only không tự được gán UI PASS; layer coverage ghi từng case.

## D. Defects

[D-DEFECTS.md](D-DEFECTS.md): **12 nhóm phát hiện, 3 High / 5 Medium / 4 Low**. Không có Critical được chứng minh.

1. Effective roles/grants/scope chưa có; QA được gán SYS_ADMIN nhưng navigation rỗng.
2. Logout với cookie của session expired/revoked trả **400 CSRF_VALIDATION_FAILED**, vi phạm exception success UC-002.
3. Audit Logout/Session không có actor_user_id.
4. Inactive + đúng password bị từ chối nhưng failure counter không tăng theo UC exception.
5. MSG12 thiếu unlock time; MSG13/MSG14/MSG08 sai câu; MSG03 thiếu field reasons/policy items; MSG01 thiếu dialog.
6. Restricted lifetime 15m khác SYS-PAR-06 30m không có ngoại lệ trong DOCX.
7. Profile email rỗng là supplemental integration gap; không tự gán thành vi phạm UC-011.

## E. Bằng chứng và bảo toàn

- Source chuẩn tại hotelposTest được build lại: backend-build.log **0 warning/0 error**; frontend-build.log thành công, vẫn cảnh báo bundle JS >500kB.
- QA dùng port **7039/5185**, không thay config/runtime 7029/5175 đang phục vụ người dùng. Health endpoint trực tiếp/proxy xác nhận HotelOpsDB cũ.
- Mỗi ca chạy có before/after SQL và response/layer evidence trong JSON. Test expired/lock deadline dùng chỉnh mốc thời gian của QA-only row để mô phỏng, không giả vờ đã chờ đủ30m.
- Chỉ tạo user QA riêng. 22 tài khoản QA phát sinh qua setup/attempt/final/service đều INACTIVE; **0 session QA chưa thu hồi**. Audit và dữ liệu QA giữ lại để truy vết, không xóa records hoặc nạp seed. Xem database-final.json.
- Fingerprint credential/account-status/lock/counter của các user đã có trước lượt cuối không đổi: existing-user-preservation.json. Tài khoản hung không dùng làm test gây khóa, không reset credential; không cố khôi phục password QA thành password thật.
- source-verification.json: **248 file source/test đã kiểm tra, 0 thay đổi**. C# project service-probe là harness kiểm thử trong qa folder, không thêm application layer/project vào HotelOps.slnx. Không chỉnh controllers, mappings, entities, frontend/test gốc hoặc config nghiệp vụ.
- Không commit/push/merge Git; snapshot hiện không có metadata Git.
- Log/report không lưu password plaintext, hash đăng nhập hoặc raw cookie/session credential. CSRF response đã redacted trong test evidence; SQL chỉ xuất fingerprint của hash, không xuất hash gốc. Test fixture password chỉ ở memory.

## Kết luận trả lời yêu cầu

1. Backend có **3 nghiệp vụ public thật**, cùng 4 technical/template routes; temporary provision/reset mới ở service nội bộ.
2. **4 khả năng nối FE–BE** được chạy (login, logout, change password, profile/session), đều có giới hạn SRS nêu trên.
3. **114 ca mới distinct đã chạy**, thêm bộ37 regression cũ báo riêng.
4. Suite mới: **94 PASS / 20 FAIL / 3 BLOCKED / 7 NOT TESTED**, tổng kế hoạch124; 10 ca sau là planned chưa chạy.
5. **GBR/MSG chưa đáp ứng đầy đủ**; xem matrix và defect reproduction thay vì kết luận từ build.
6. Authentication **có đọc/ghi SQL thật và enforce core security phía Backend**, nhưng chưa tuân thủ đầy đủ SRS.
7. Có lỗi logic/traceability/authorization incomplete/exception handling; chưa có bằng chứng credential disclosure/bypass trong ca đã thử, không suy rộng thành an toàn toàn diện.
8. Có thể tiếp tục phát triển/tích hợp trên feature/draft workflow kèm lỗi đã biết; **chưa đủ căn cứ merge lên main như bản đạt SRS**. Cần người dùng duyệt sửa, test lại các defect và hoàn thiện permission/coverage còn thiếu. Không sửa source khi chưa được đồng ý.
