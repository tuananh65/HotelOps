# Hồ sơ bằng chứng 48 ca E2E — Master §37

Cập nhật 25/11/2026. Phạm vi: Project 2 Compact, database TEST `HotelOpsCompactWorkdayTestV22611250010`. Danh mục có 48 hồ sơ, mỗi hồ sơ đủ 11 trường; không phải tuyên bố 48 ca PASS. Nguồn: [Master §37](OWNER_REQUEST_PART_4.md), [chỉ mục kết quả](E2E_TEST_RESULTS.md), [runbook demo](DEMO_RUNBOOK.md), [quyết định D07/D12](DECISION_AND_BLOCKER_REGISTER.md).

Preconditions/Steps/Expected Result mô tả ca cần tái hiện. Actual Result/Verdict chỉ dựa trên artifact được dẫn, không chứng nhận lượt source mới đang chạy. `tests[n]` là chỉ số JSON từ 0; từng assertion không phải một E2E riêng. API/Database Evidence dẫn kết quả và runner chứa assertion; không thay raw response hoặc SQL snapshot độc lập. Không suy bằng chứng database từ ảnh. Screenshot có thể bị lượt sau ghi đè, phải đọc cùng JSON và runbook.

Không trích token, credentials hoặc fixture private. Chi nhánh A/B là nguồn/đích do fixture tra cứu, không khẳng định ID hoặc tên chi nhánh thật. Chỉ chạy lại trên TEST với backup hợp lệ. Hồ sơ này không chạy API/SQL/browser hay sửa dữ liệu. Session TEST được phép không phải API mock. SMTP sink thuộc D07; fallback thống kê không phải external AI runtime PASS.

Các khoảng trống ghi tại Actual Result, Verdict hoặc trường evidence. Không tự thêm mọi loại concurrency hay mọi thao tác UI làm điều kiện mới của Master. Các lượt đang kiểm lại chưa được nâng PASS từ artifact cũ.

Bổ sung ngày 25/11: [quantity-blockers-twelve-pass.json](../../artifacts/restructure-v2/quantity-blockers-twelve-pass.json) đạt 12/12, gồm chặn báo/bổ sung vượt thiếu, giữ người tạo gốc và người duyệt riêng, cấp một phần rồi chỉ requeue phần thiếu (tests[3–7]) và nhiều blocker (E2E-37 bên dưới). [phone-resource-reports-live.json](../../artifacts/restructure-v2/phone-resource-reports-live.json) kiểm API/Edge thật cho số điện thoại và báo cáo kho/tài sản OM; không dùng probe này thay toàn suite role/configuration. [incident-snapshot-identifier-failure.json](../../artifacts/restructure-v2/incident-snapshot-identifier-failure.json) là toàn lượt **7 PASS/1 FAIL**: tests[3–6] đã kiểm SOURCE legacy/paging/search/recovery giữ creator-history và sửa xong chỉ một HKS PASS đưa Ready; tests[7] lỗi SQL identifier của snapshot quantity. Không ghi suite legacy hoàn toàn PASS hoặc suy ra đã xử lý phòng104/incident653 local.

## E2E-01 — Gộp DH vào OM hoặc xác minh đã gộp

- **Preconditions:** Role và membership TEST đọc được.
- **Actor:** Administrator/OM.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Role DH, OM và quyền lập kế hoạch.
- **Steps:** Đối chiếu role, membership và quyền trước/sau gộp.
- **Expected Result:** OM giữ chức năng DH đã được chấp thuận, không đổi role ngoài phạm vi.
- **Actual Result:** Catalog/API/UI xác nhận OM hoạt động, không có DH hoạt động.
- **API Evidence:** [6/6 roster API/Edge](../../artifacts/restructure-v2/recovered-roster-runtime-results.json).
- **Database Evidence:** [Audit chỉ đọc](../../artifacts/restructure-v2/role-recovery-current.json), provenance và số đếm credential; không xuất hash.
- **UI Evidence:** [Roster runtime](../../artifacts/restructure-v2/recovered-roster-runtime-results.json), [chi nhánh413](../../artifacts/restructure-v2/recovered-roster-branch-413.png), [chi nhánh414](../../artifacts/restructure-v2/recovered-roster-branch-414.png), [OM](../../artifacts/restructure-v2/om-consolidated-roster.png).
- **Verdict:** PASS xác minh gộp OM/DH trên TEST.

## E2E-02 — Giữ/khôi phục Maintenance Staff

- **Preconditions:** Có audit recovery và actor kỹ thuật TEST riêng.
- **Actor:** Administrator, Maintenance Staff.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Role kỹ thuật và assignment TEST.
- **Steps:** Đối chiếu recovery; dùng staff nhận việc được giao.
- **Expected Result:** Giữ hoặc khôi phục Maintenance Staff và quyền nhận việc kỹ thuật.
- **Actual Result:** 10 người gốc phục hồi role đúng chi nhánh, tất cả hiển thị vai trò trên UI, account/role/membership không đổi sau đọc; credentials10/10 bằng nguồn.
- **API Evidence:** [6/6 roster API/Edge](../../artifacts/restructure-v2/recovered-roster-runtime-results.json).
- **Database Evidence:** [Audit chỉ đọc](../../artifacts/restructure-v2/role-recovery-current.json), provenance và số đếm credential; không xuất hash.
- **UI Evidence:** [Roster runtime](../../artifacts/restructure-v2/recovered-roster-runtime-results.json), [chi nhánh413](../../artifacts/restructure-v2/recovered-roster-branch-413.png), [chi nhánh414](../../artifacts/restructure-v2/recovered-roster-branch-414.png), [OM](../../artifacts/restructure-v2/om-consolidated-roster.png).
- **Verdict:** PASS giữ/khôi phục đội TEST; không kiểm password-login của tài khoản phục hồi hoặc rollout local.

## E2E-03 — Maintenance Supervisor quản lý đội kỹ thuật

- **Preconditions:** Có MS, staff và phòng/tài sản TEST cùng chi nhánh.
- **Actor:** Maintenance Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phiếu nội bộ và checklist kỹ thuật.
- **Steps:** MS tạo, phân công; staff nhận và hoàn thành.
- **Expected Result:** MS quản lý đội kỹ thuật; staff giữ vai trò riêng.
- **Actual Result:** MS phân công, Maintenance Staff nhận/thực hiện checklist, MS nghiệm thu qua HTTP/UI đạt. Provenance phục hồi theo dõi riêng E2E02; full scope ở47.
- **API Evidence:** [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[1], tests[6], tests[7]; lượt 2026-11-25T05:26:40.437Z. [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[1], tests[6], tests[7]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [maintenance-created-internal.png](../../artifacts/restructure-v2/maintenance-created-internal.png)
- **Verdict:** PASS quản lý đội bảo trì TEST.

## E2E-04 — Tạo tài khoản và gửi email

- **Preconditions:** Admin TEST có quyền tạo user; SMTP sink local hoạt động.
- **Actor:** Administrator.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** User TEST mới, invitation và outbox local.
- **Steps:** Tạo user trên UI, chờ SMTP sink, gửi lại lời mời và kiểm số user.
- **Expected Result:** Một user được tạo; gửi lại lời mời không tạo user trùng.
- **Actual Result:** UI quản trị tạo → lời mời PENDING → SMTP sink nhận link; UI resend → token cũ revoked, user count không đổi; cleanup hai tài khoản TEST.
- **API Evidence:** [login-recovery-username-nineteen-pass.json](../../artifacts/restructure-v2/login-recovery-username-nineteen-pass.json) — tests[0], tests[3], tests[12], tests[13], tests[14]; lượt 2026-11-25T07:55:19.932Z. [runner/assertions](../../scripts/verification/v2-invitation-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/login-recovery-username-nineteen-pass.json) — tests[0], tests[3], tests[12], tests[13], tests[14]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-invitation-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [invitation-admin-create.png](../../artifacts/restructure-v2/invitation-admin-create.png)
- **Verdict:** PASS với SMTP sink; delivery thật BLOCKED_EXTERNAL_CONFIGURATION

## E2E-05 — Đăng nhập lần đầu bắt buộc đổi mật khẩu

- **Preconditions:** Tài khoản TEST mới chưa thiết lập mật khẩu.
- **Actor:** Người dùng TEST mới.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Invitation một lần và phiên trước setup.
- **Steps:** Đăng nhập lần đầu, mở hai tab, đổi mật khẩu và đăng nhập lại.
- **Expected Result:** Bắt buộc đổi mật khẩu trước workspace; setup thành công.
- **Actual Result:** Mật khẩu tạm buộc đổi ở hai tab, thiết lập từ SMTP token trên mobile, thu hồi tab cũ và đăng nhập mật khẩu mới.
- **API Evidence:** [login-recovery-username-nineteen-pass.json](../../artifacts/restructure-v2/login-recovery-username-nineteen-pass.json) — tests[2], tests[4], tests[5], tests[6], tests[7]; lượt 2026-11-25T07:55:19.932Z. [runner/assertions](../../scripts/verification/v2-invitation-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/login-recovery-username-nineteen-pass.json) — tests[2], tests[4], tests[5], tests[6], tests[7]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-invitation-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [invitation-forced-password-mobile.png](../../artifacts/restructure-v2/invitation-forced-password-mobile.png), [invitation-setup-complete-mobile.png](../../artifacts/restructure-v2/invitation-setup-complete-mobile.png)
- **Verdict:** PASS trong phạm vi tài khoản TEST mới

## E2E-06 — Truy cập API nghiệp vụ trước khi đổi mật khẩu bị từ chối

- **Preconditions:** Tài khoản TEST đang buộc đổi mật khẩu.
- **Actor:** Người dùng TEST mới.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** API nghiệp vụ trước và sau setup.
- **Steps:** Đăng nhập, gọi API trước đổi mật khẩu, setup rồi gọi lại.
- **Expected Result:** API bị từ chối trước đổi mật khẩu và cho phép sau setup hợp lệ.
- **Actual Result:** First login buộc setup; API nghiệp vụ trước đổi mật khẩu bị từ chối, sau setup workspace hoạt động. Không suy rộng thành toàn bộ endpoint/RBAC.
- **API Evidence:** [login-recovery-username-nineteen-pass.json](../../artifacts/restructure-v2/login-recovery-username-nineteen-pass.json) — tests[2], tests[6]; lượt 2026-11-25T07:55:19.932Z. [runner/assertions](../../scripts/verification/v2-invitation-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/login-recovery-username-nineteen-pass.json) — tests[2], tests[6]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-invitation-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [invitation-forced-password-mobile.png](../../artifacts/restructure-v2/invitation-forced-password-mobile.png)
- **Verdict:** PASS với API và browser đã chạy

## E2E-07 — Thu hồi session/token sau đổi quyền hoặc mật khẩu

- **Preconditions:** User TEST có phiên hoạt động.
- **Actor:** Người dùng TEST, Administrator.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phiên cũ, mật khẩu/reset mới; nhánh đổi quyền kiểm riêng.
- **Steps:** Đổi/reset mật khẩu, dùng lại phiên cũ; kiểm riêng nhánh đổi quyền.
- **Expected Result:** Phiên cũ mất hiệu lực khi đổi mật khẩu hoặc quyền.
- **Actual Result:** 20/20 invitation/login/reset/role-change đạt. Admin cấp/thu hồi/cấu hình quyền vô hiệu hai phiên browser; duplicate/stale/FK failure giữ phiên và grants; admin không bị ảnh hưởng.
- **API Evidence:** [20/20 toàn lượt](../../artifacts/restructure-v2/invitation-role-session-twenty-pass.json), [role-change assertions](../../artifacts/restructure-v2/role-change-verified-pass.json).
- **Database Evidence:** Failed FK commit rollback kiểm grants trước/sau bằng API, token hai phiên vẫn200; commit thành công token cũ401. Không gán thành SQL timestamp snapshot độc lập.
- **UI Evidence:** [Hai phiên Edge](../../artifacts/restructure-v2/role-change-verified-pass.json) về login sau grant/revoke/configure, sessionStorage đã xóa.
- **Verdict:** PASS HTTP/browser TEST; hub sau đổi role không thuộc probe này.

## E2E-08 — Start Shift cấp đồ fill

- **Preconditions:** Ca HK đã lên lịch; kho có lô READY đủ định mức.
- **Actor:** HK Staff, Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 10 tiêu hao và 4 reusable.
- **Steps:** Bắt đầu ca qua UI, đọc cấp phát và thử lại.
- **Expected Result:** Cấp fill một lần; bàn giao reusable không giảm sở hữu.
- **Actual Result:** Start Shift qua UI cấp 10 tiêu hao và bàn giao 4 reusable; retry không cấp trùng. Kho thiếu/concurrency là kiểm tra bổ sung.
- **API Evidence:** [housekeeping-completion-reviewed-eighteen-pass.json](../../artifacts/restructure-v2/housekeeping-completion-reviewed-eighteen-pass.json) — tests[1]; lượt 2026-11-25T05:06:20.128Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-completion-reviewed-eighteen-pass.json) — tests[1]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [housekeeping-shift-start.png](../../artifacts/restructure-v2/housekeeping-shift-start.png)
- **Verdict:** PASS phạm vi Start Shift cấp đồ fill

## E2E-09 — Kiểm tra số lượng vật tư hiện có trong phòng

- **Preconditions:** HK có cleaning và reservation theo định mức.
- **Actor:** HK Staff.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Định mức 4, còn dùng 3, thiếu 1.
- **Steps:** Mở assessment phòng, nhập còn dùng và lưu.
- **Expected Result:** Ghi nhận đúng số còn dùng; thiếu 1, giải phóng phần reserve dư.
- **Actual Result:** HK kiểm phòng trên UI: định mức 4, còn dùng 3, thiếu 1; reservation 4→1, phần dư được giải phóng. Negative counts đầy đủ chưa kiểm.
- **API Evidence:** [housekeeping-expiry-settlement-twenty-pass.json](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[2], tests[4]; lượt 2026-11-25T07:49:31.988Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[2], tests[4]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [cleaning-assessment-mobile.png](../../artifacts/restructure-v2/cleaning-assessment-mobile.png)
- **Verdict:** PASS phạm vi assessment đã chạy

## E2E-10 — Chỉ bổ sung đúng số lượng thiếu

- **Preconditions:** Assessment phòng xác nhận thiếu 1.
- **Actor:** HK Staff, HK Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 4 định mức, 3 còn dùng, 1 bổ sung.
- **Steps:** Assessment, hoàn thành checklist/dọn; kiểm ledger và thử lại.
- **Expected Result:** Chỉ tiêu hao đúng 1 bổ sung; không ghi trùng.
- **Actual Result:** Browser assessment 3 còn dùng/1 thiếu và completion tiêu hao đúng 1; exact retry không đổi ledger/chứng từ/history.
- **API Evidence:** [housekeeping-expiry-settlement-twenty-pass.json](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[4], tests[9]; lượt 2026-11-25T07:49:31.988Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[4], tests[9]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [cleaning-assessment-mobile.png](../../artifacts/restructure-v2/cleaning-assessment-mobile.png), [cleaning-completed-browser.png](../../artifacts/restructure-v2/cleaning-completed-browser.png)
- **Verdict:** PASS trong phạm vi fixture TEST

## E2E-11 — Không đủ vật tư thì chặn assignment

- **Preconditions:** HK có lượng khả dụng thấp hơn nhu cầu assignment.
- **Actor:** HK Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phòng TEST cần lượng vượt khả dụng.
- **Steps:** Giao việc; so task/reservation trước và sau lỗi.
- **Expected Result:** Từ chối khi thiếu vật tư; không để task hoặc reservation dở dang.
- **Actual Result:** HKS tạo công việc qua Edge; API trả409 STAFF_SUPPLIES_INSUFFICIENT và UI hiện thông báo thiếu vật tư. Allocation trước/sau bằng nhau, queue không có công việc tạo dở.
- **API Evidence:** [Housekeeping26](../../artifacts/restructure-v2/housekeeping-assignment-scope-twentysix-pass.json) — tests[7], [runner](../../scripts/verification/v2-housekeeping-runtime.mjs), [browser helper](../../scripts/verification/v2-assignment-browser.mjs).
- **Database Evidence:** [Housekeeping26](../../artifacts/restructure-v2/housekeeping-assignment-scope-twentysix-pass.json) — tests[7] đối chiếu allocation và queue qua API; không có raw SQL snapshot riêng cho bước này.
- **UI Evidence:** [Kết quả Edge](../../artifacts/restructure-v2/assignment-insufficient.json), [ảnh thông báo](../../artifacts/restructure-v2/assignment-insufficient.png).
- **Verdict:** PASS chặn phân công thiếu vật tư trên TEST; không suy ra password-login hoặc SignalR.

## E2E-12 — Reservation nhiều phòng

- **Preconditions:** HK có hai phòng TEST dùng chung balance.
- **Actor:** HK Supervisor, HK Staff.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Hai reservation 1 và 4.
- **Steps:** Giao hai phòng, kiểm reserve và hủy một phòng.
- **Expected Result:** Các reservation độc lập; chỉ giải phóng phần của phòng bị hủy.
- **Actual Result:** Tạo công việc phòng thứ hai qua Edge thành công; reserve chung5=1+4, used0. Hủy phòng thứ hai qua API giải phóng4, còn reserve1/used0; UI hiển thị Đã hủy. Không thêm nút hủy vào UX cleaning đã chấp thuận.
- **API Evidence:** [Housekeeping26](../../artifacts/restructure-v2/housekeeping-assignment-scope-twentysix-pass.json) — tests[8], [runner](../../scripts/verification/v2-housekeeping-runtime.mjs), [browser helper](../../scripts/verification/v2-assignment-browser.mjs).
- **Database Evidence:** [Housekeeping26](../../artifacts/restructure-v2/housekeeping-assignment-scope-twentysix-pass.json) — tests[8] kiểm allocation reserved5→1/used0 và assessment phòng gốc vẫn1 qua API; các đối soát SQL toàn lượt nằm trong runner.
- **UI Evidence:** [Tạo phòng thứ hai](../../artifacts/restructure-v2/assignment-second-room.json), [ảnh tạo việc](../../artifacts/restructure-v2/assignment-second-room.png), [trạng thái sau hủy](../../artifacts/restructure-v2/reservation-cancelled.png).
- **Verdict:** PASS nhiều reservation và giải phóng độc lập trên TEST; cancellation thực hiện qua API, không tuyên bố hai assignment đồng thời.

## E2E-13 — Checklist Cleaning chưa đủ thì không cho hoàn thành

- **Preconditions:** Cleaning đang thực hiện và có checklist bắt buộc.
- **Actor:** HK Staff.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Checklist chưa đủ rồi đánh dấu đạt.
- **Steps:** Thử hoàn thành khi thiếu, điền checklist trên UI rồi gửi lại.
- **Expected Result:** Chỉ hoàn thành khi checklist đầy đủ.
- **Actual Result:** Required checklist chặn complete API; browser chọn PASS và gửi hoàn thành thành công, không ghi trùng khi replay.
- **API Evidence:** [housekeeping-expiry-settlement-twenty-pass.json](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[8], tests[9]; lượt 2026-11-25T07:49:31.988Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[8], tests[9]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [cleaning-completed-browser.png](../../artifacts/restructure-v2/cleaning-completed-browser.png)
- **Verdict:** PASS trong phạm vi fixture TEST

## E2E-14 — Dọn phòng bình thường và Inspection PASS

- **Preconditions:** Cleaning bình thường, không có sự cố hoặc blocker khác.
- **Actor:** HK Staff, HK Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phòng TEST và assessment/checklist hợp lệ.
- **Steps:** Hoàn thành dọn; HKS kiểm tra PASS.
- **Expected Result:** Phiếu hoàn thành và phòng Ready sau kiểm tra đạt.
- **Actual Result:** HK hoàn thành dọn, HKS PASS không note/checkbox, phòng Ready. Blocker đồng thời thuộc E2E-37.
- **API Evidence:** [housekeeping-expiry-settlement-twenty-pass.json](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[9]; lượt 2026-11-25T07:49:31.988Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[9]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [cleaning-completed-browser.png](../../artifacts/restructure-v2/cleaning-completed-browser.png), [cleaning-inspected.png](../../artifacts/restructure-v2/cleaning-inspected.png)
- **Verdict:** PASS phạm vi phòng bình thường trên TEST

## E2E-15 — Cleaning Rework không trừ vật tư trùng

- **Preconditions:** Inspection cleaning FAIL; giữ vật tư còn dùng.
- **Actor:** HK Staff, HK Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Cleaning đã tiêu hao1 vật tư; kiểm tra FAIL với lý do `Dust`, dọn lại không bổ sung mới.
- **Steps:** FAIL, làm lại, hoàn thành; so tiêu hao cũ.
- **Expected Result:** Không trừ lại vật tư đã dùng ở vòng trước.
- **Actual Result:** Lượt25/25 đạt: FAIL qua UI, nhân viên thực hiện checklist/dọn lại/gửi hoàn thành; used vẫn1, ledger và assessment không đổi kể cả retry. Phòng DIRTY trước kiểm tra, PASS không note đưa Ready.
- **API Evidence:** [housekeeping-rework-refill-twentyfive-pass.json](../../artifacts/restructure-v2/housekeeping-rework-refill-twentyfive-pass.json), [probe](../../scripts/verification/v2-positive-rework-probe.mjs).
- **Database Evidence:** [positive-rework-evidence.json](../../artifacts/restructure-v2/positive-rework-evidence.json); so snapshot SQL/ledger trước và sau rework/retry.
- **UI Evidence:** [FAIL](../../artifacts/restructure-v2/positive-rework-inspection-failed.png), [hoàn thành dọn lại](../../artifacts/restructure-v2/positive-rework-completed.png); actual Edge/API.
- **Verdict:** PASS phạm vi dọn lại sau tiêu hao thực không trừ trùng; không suy ra nhánh bổ sung mới ngoài ca đã chạy.

## E2E-16 — End Shift hoàn trả đúng vật tư

- **Preconditions:** Ca có vật tư bàn giao còn phải hoàn.
- **Actor:** HK Staff, Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 16 tiêu hao thuộc hai lô và 4 reusable READY.
- **Steps:** Kết ca/đối soát trên UI; kiểm returned và custody.
- **Expected Result:** Hoàn đúng lượng và nguồn; không hoàn hai lần.
- **Actual Result:** End Shift qua UI trả16 tiêu hao về đúng hai lô gốc,4 reusable READY về kho; custody/ledger/retry được đối chiếu. Không gán kết quả này cho mọi nhánh hàng hỏng/bẩn.
- **API Evidence:** [Housekeeping25](../../artifacts/restructure-v2/housekeeping-rejection-twentyfive-pass.json)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-completion-reviewed-eighteen-pass.json) — tests[16]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [Settlement UI](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json)
- **Verdict:** PASS nhánh hoàn trả cuối ca đã chạy trên TEST.

## E2E-17 — Xuất tiêu hao theo FIFO

- **Preconditions:** Item tiêu hao dùng FIFO, có hai lô hợp lệ.
- **Actor:** Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Hai lô nhập 5+5; lô cũ hạn muộn; xuất 3.
- **Steps:** Lập và ghi sổ ISSUE; đọc phân bổ lô và ledger.
- **Expected Result:** Lấy lô nhận cũ trước; còn 2 ở lô cũ và 5 ở lô mới.
- **Actual Result:** Thủ kho ghi sổ xuất FIFO qua popup; lô nhận cũ giảm3 dù hạn muộn hơn, lô mới giữ nguyên.
- **API Evidence:** [Inventory43](../../artifacts/restructure-v2/inventory-fifo-expiry-damage-ui-43-pass.json)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[8]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [FIFO popup](../../artifacts/restructure-v2/fifo-issue-browser.json)
- **Verdict:** PASS FIFO API/SQL/Edge trên TEST.

## E2E-18 — Xuất tiêu hao theo FEFO

- **Preconditions:** Item dùng FEFO, hai lô hợp lệ khác hạn dùng.
- **Actor:** HK Staff, HK Supervisor, Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Lô nhận sau nhưng hết hạn sớm hơn.
- **Steps:** HK yêu cầu, HKS duyệt, SK cấp refill; kiểm allocation.
- **Expected Result:** Cấp lô hết hạn sớm trước, held tăng đúng lượng.
- **Actual Result:** Refill chọn lô mới nhận nhưng hạn sớm hơn; browser HK/HKS/SK tạo, duyệt, cấp và kiểm số dư/history. Transfer FEFO có E2E-40.
- **API Evidence:** [housekeeping-expiry-settlement-twenty-pass.json](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[11], tests[12], tests[13], tests[15]; lượt 2026-11-25T07:49:31.988Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[11], tests[12], tests[13], tests[15]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [refill-hk-desktop.png](../../artifacts/restructure-v2/refill-hk-desktop.png), [refill-store-desktop.png](../../artifacts/restructure-v2/refill-store-desktop.png)
- **Verdict:** PASS phạm vi refill FEFO trên TEST

## E2E-19 — Không cấp lô đã hết hạn

- **Preconditions:** Có lô EXPIRED, QUARANTINED và lô khả dụng.
- **Actor:** Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 7 EXPIRED, 6 QUARANTINED; lượng READY riêng.
- **Steps:** Đọc available, thử xuất vượt lượng hợp lệ; kiểm rollback.
- **Expected Result:** Không cấp lô hết hạn; thiếu lô hợp lệ thì rollback.
- **Actual Result:** Xuất vượt lượng còn hạn bị409 BATCH_AVAILABLE_INSUFFICIENT, lỗi hiển thị rõ; header/movements/allocation giữ nguyên sau rollback.
- **API Evidence:** [Inventory43](../../artifacts/restructure-v2/inventory-fifo-expiry-damage-ui-43-pass.json)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[9], tests[10]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [Expired issue UI](../../artifacts/restructure-v2/expired-issue-denied-browser.json)
- **Verdict:** PASS nhánh chặn xuất hàng hết hạn/không đủ lô hợp lệ; không suy ra mọi đường expiry.

## E2E-20 — Hoàn trả giữ đúng Batch/Expiry

- **Preconditions:** Vật tư held có nguồn gốc từ hai lô.
- **Actor:** HK Staff, Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 16 tiêu hao trả về hai lô và expiry gốc.
- **Steps:** Kết ca/đối soát; so batch allocation và thử lại.
- **Expected Result:** Giữ đúng Batch/Expiry; không tạo lô hoặc hạn dùng giả.
- **Actual Result:** Cuối ca qua UI hoàn 16 tiêu hao về hai lô gốc, giữ hạn dùng; retry không hoàn hai lần. Không gồm hàng trả không đủ điều kiện.
- **API Evidence:** [housekeeping-completion-reviewed-eighteen-pass.json](../../artifacts/restructure-v2/housekeeping-completion-reviewed-eighteen-pass.json) — tests[16]; lượt 2026-11-25T05:06:20.128Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-completion-reviewed-eighteen-pass.json) — tests[16]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [housekeeping-shift-end.png](../../artifacts/restructure-v2/housekeeping-shift-end.png)
- **Verdict:** PASS phạm vi vật tư còn dùng đã kiểm

## E2E-21 — Cấp chăn vào phòng không giảm tổng sở hữu

- **Preconditions:** Reusable READY ở kho TEST và phòng cùng chi nhánh.
- **Actor:** Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Vật tư TEST đại diện đồ vải: sở hữu 10, chuyển 4.
- **Steps:** Chuyển READY sang IN_USE tại phòng qua UI.
- **Expected Result:** Kho giảm 4, phòng tăng 4, tổng sở hữu vẫn 10.
- **Actual Result:** Vật tư REUSABLE TEST đại diện đồ vải: chuyển 4 READY→IN_USE vào phòng qua UI, nguồn giảm/đích tăng 4, sở hữu vẫn 10. Không xác nhận kiểm kê khăn/chăn thực tế.
- **API Evidence:** [inventory-reusable-ui-fortythree-pass.json](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[15]; lượt 2026-11-25T05:34:32.647Z. [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[15]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [reusable-ready-in_use.png](../../artifacts/restructure-v2/reusable-ready-in_use.png)
- **Verdict:** PASS với vật tư TEST tương đương

## E2E-22 — Thu hồi chăn/khăn sang trạng thái cần giặt

- **Preconditions:** Phòng TEST giữ reusable IN_USE.
- **Actor:** Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 4 reusable TEST đang dùng.
- **Steps:** Thu hồi IN_USE sang DIRTY qua UI.
- **Expected Result:** Đồ cần giặt vào DIRTY, giữ lượng và nguồn gốc.
- **Actual Result:** Thu hồi IN_USE→DIRTY qua UI; lượng nguồn/đích và tổng sở hữu đúng, history giữ nguồn gốc.
- **API Evidence:** [inventory-reusable-ui-fortythree-pass.json](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[16]; lượt 2026-11-25T05:34:32.647Z. [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[16]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [reusable-in_use-dirty.png](../../artifacts/restructure-v2/reusable-in_use-dirty.png)
- **Verdict:** PASS với vật tư tái sử dụng TEST

## E2E-23 — Hoàn thành giặt là và chuyển READY

- **Preconditions:** Đã thu hồi reusable DIRTY.
- **Actor:** Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 4 reusable TEST; tổng sở hữu 10.
- **Steps:** Chuyển DIRTY sang LAUNDRY rồi READY qua UI.
- **Expected Result:** Giặt xong trả READY; tổng sở hữu không giảm.
- **Actual Result:** DIRTY→LAUNDRY→READY qua UI, sở hữu vẫn 10; retry custody không di chuyển hai lần. Không suy ra concurrency đã kiểm.
- **API Evidence:** [inventory-reusable-ui-fortythree-pass.json](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[17], tests[18]; lượt 2026-11-25T05:34:32.647Z. [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[17], tests[18]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [reusable-dirty-laundry.png](../../artifacts/restructure-v2/reusable-dirty-laundry.png), [reusable-laundry-ready.png](../../artifacts/restructure-v2/reusable-laundry-ready.png)
- **Verdict:** PASS chu kỳ giặt trên TEST

## E2E-24 — Vật tư hỏng chuyển DAMAGED

- **Preconditions:** Có reusable READY/IN_USE cần đánh dấu hỏng.
- **Actor:** Storekeeper, người phát hiện theo quyền.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 2 reusable trong tổng sở hữu 10.
- **Steps:** Chuyển DAMAGED, xem custody/history và thử lại.
- **Expected Result:** DAMAGED giảm khả dụng, chưa giảm sở hữu trước quyết định hợp lệ.
- **Actual Result:** Thủ kho chuyển READY sang DAMAGED qua UI; lượng khả dụng giảm, tổng sở hữu giữ nguyên, retry không chuyển hai lần.
- **API Evidence:** [Inventory43](../../artifacts/restructure-v2/inventory-fifo-expiry-damage-ui-43-pass.json)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[19], tests[21]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [DAMAGED UI](../../artifacts/restructure-v2/reusable-ready-damaged-browser.json)
- **Verdict:** PASS nhánh vật tư hư hỏng trên TEST.

## E2E-25 — Ghi giảm vật tư không thể phục hồi

- **Preconditions:** Reusable DAMAGED có đề xuất ghi giảm.
- **Actor:** Storekeeper, OM cùng chi nhánh.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Đề xuất ghi giảm 1 trong tổng sở hữu 10.
- **Steps:** SK đề xuất, OM quyết định; so ownership/ledger.
- **Expected Result:** Duyệt giảm đúng 1; từ chối không giảm; thử lại không giảm thêm.
- **Actual Result:** Thủ kho đề nghị qua API, OM cùng chi nhánh duyệt qua UI; khác chi nhánh/tự duyệt bị chặn, từ chối cần lý do; ownership và ledger chỉ giảm một lần sau commit.
- **API Evidence:** [Inventory43](../../artifacts/restructure-v2/inventory-fifo-expiry-damage-ui-43-pass.json)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json) — tests[21], tests[22], tests[23], tests[24], tests[25]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-inventory-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [Workspace assertions](../../scripts/verification/v2-request-browser.mjs) đã chạy trong kiểm cuối cùng của artifact43.
- **Verdict:** PASS API/SQL và UI quyết định ghi giảm; chưa có UI tạo đề nghị hoặc concurrent distinct decisions.

## E2E-26 — Low Stock dựa trên lượng khả dụng

- **Preconditions:** Low-stock reconcile hoạt động trên TEST.
- **Actor:** Storekeeper đúng chi nhánh.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** 100 tiêu hao chỉ 40 usable, ngưỡng 50; reusable 100 có 90 LAUNDRY.
- **Steps:** Reconcile, thay đổi thiếu/phục hồi, gửi/nhận transfer, đọc alert.
- **Expected Result:** Cảnh báo theo usable/READY, cùng episode và đúng scope; incoming chưa khả dụng.
- **Actual Result:** low-stock-ui-ten-pass ghi 10/10 HTTP/SQL/Edge PASS: 100 vật lý/40 usable/ngưỡng 50 báo thiếu 10; reusable 100/LAUNDRY 90 dựa READY 10 báo thiếu 40. Kiểm recipient, cập nhật/đóng/mở episode và incoming chưa khả dụng. Desktop/mobile hiển thị đúng thông báo, không tràn ngang, snapshot đọc UI không đổi; cleanup sạch.
- **API Evidence:** [low-stock-ui-ten-pass.json](../../artifacts/restructure-v2/low-stock-ui-ten-pass.json) — tests[0–9], lượt 2026-11-25T11:22:01.289Z. [runner/assertions](../../scripts/verification/v2-low-stock-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/low-stock-ui-ten-pass.json) và [browser snapshot assertions](../../scripts/verification/v2-low-stock-browser.mjs): so batches/reusable/notifications trước–sau UI, không có write request. Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [consumable browser](../../artifacts/restructure-v2/low-stock-consumable-browser.json), [reusable browser](../../artifacts/restructure-v2/low-stock-reusable-browser.json), [consumable desktop](../../artifacts/restructure-v2/low-stock-consumable-desktop.png), [consumable mobile](../../artifacts/restructure-v2/low-stock-consumable-mobile.png), [reusable desktop](../../artifacts/restructure-v2/low-stock-reusable-desktop.png), [reusable mobile](../../artifacts/restructure-v2/low-stock-reusable-mobile.png).
- **Verdict:** PASS phạm vi cảnh báo thực trên TEST; không suy ra test worker SKIP hoặc SignalR đã chạy trong probe này.

## E2E-27 — HK Staff yêu cầu bổ sung tiêu hao

- **Preconditions:** HK đang ca, có allocation và thiếu tiêu hao.
- **Actor:** HK Staff, HK Supervisor, Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Refill TEST và hai lô FEFO.
- **Steps:** HK tạo, HKS duyệt, SK cấp; kiểm held/history.
- **Expected Result:** Lưu và xử lý đúng vai trò, cấp đúng lượng.
- **Actual Result:** HK tạo yêu cầu → HKS duyệt → SK cấp thực tế; số dư staff và history đúng, retry không trừ hai lần. Browser chạy cả ba vai trò với session TEST; FEFO kiểm ở lượt HTTP.
- **API Evidence:** [housekeeping-expiry-settlement-twenty-pass.json](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[12], tests[13], tests[15]; lượt 2026-11-25T07:49:31.988Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[12], tests[13], tests[15]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [refill-hk-desktop.png](../../artifacts/restructure-v2/refill-hk-desktop.png), [refill-supervisor-desktop.png](../../artifacts/restructure-v2/refill-supervisor-desktop.png), [refill-store-desktop.png](../../artifacts/restructure-v2/refill-store-desktop.png)
- **Verdict:** PASS trong phạm vi tạo/duyệt/cấp; không bao gồm login/SignalR

## E2E-28 — Storekeeper từ chối vì thiếu tồn

- **Preconditions:** Refill được duyệt nhưng tồn hợp lệ không đủ.
- **Actor:** Storekeeper.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Refill thiếu tồn và lý do từ chối TEST.
- **Steps:** Thử cấp thiếu; từ chối không lý do rồi có lý do.
- **Expected Result:** Cấp thiếu rollback; từ chối có lý do giữ history, stock/held không đổi.
- **Actual Result:** Thiếu tồn rollback; từ chối không lý do bị chặn. Thủ kho từ chối qua popup, lưu history/lý do và giữ stock/custody; desktop/mobile đạt.
- **API Evidence:** [25/25 toàn lượt](../../artifacts/restructure-v2/housekeeping-rejection-twentyfive-pass.json).
- **Database Evidence:** Cùng runner so sánh Inspect SQL trước/sau từ chối, inventory/allocation không đổi.
- **UI Evidence:** [Browser assertions](../../artifacts/restructure-v2/refill-rejection-browser.json), [desktop](../../artifacts/restructure-v2/refill-rejection-desktop.png), [mobile](../../artifacts/restructure-v2/refill-rejection-mobile.png).
- **Verdict:** PASS phạm vi TEST đã chạy.

## E2E-29 — HK Staff ghi nhận sự cố phòng

- **Preconditions:** HK được giao cleaning có asset phòng TEST.
- **Actor:** HK Staff.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Đồ vật hỏng TEST; reporter khác người liên quan.
- **Steps:** Chọn loại/đồ vật, gửi báo hỏng và quay lại việc dọn.
- **Expected Result:** Tạo sự cố đúng phòng/người báo; không buộc ghi chú ngoài yêu cầu.
- **Actual Result:** Assigned HK chọn loại rồi mới hiện đồ vật, không ảnh hưởng/note; gửi quay về việc dọn và hoàn thành qua UI. API kiểm reporter/discoverer. Còn cross-branch create.
- **API Evidence:** [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[2], tests[10]; lượt 2026-11-25T05:26:40.437Z. [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[2], tests[10]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [maintenance-hk-reported.png](../../artifacts/restructure-v2/maintenance-hk-reported.png)
- **Verdict:** PASS trong phạm vi HK được giao phòng

## E2E-30 — Reception ghi nhận sự cố phòng

- **Preconditions:** Reception có ca TEST và đồ vật cấu hình.
- **Actor:** Reception.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Trước ca, đang ca, sau ca và chi nhánh khác.
- **Steps:** Thử báo trước ca; Start Shift, báo hỏng, End Shift và thử lại.
- **Expected Result:** Chỉ báo trong ca đúng chi nhánh, không cần cleaning assignment.
- **Actual Result:** Reception PLANNED bị chặn; Start Shift tạo báo hỏng không note/cleaning assignment; End Shift bị chặn, sai branch/thiếu đồ bị chặn. Replay không tạo issue mới.
- **API Evidence:** [role-scope-reviewed-ten-pass.json](../../artifacts/restructure-v2/role-scope-reviewed-ten-pass.json) — tests[1], tests[2], tests[4], tests[5], tests[6]; lượt 2026-11-25T05:45:20.801Z. [runner/assertions](../../scripts/verification/v2-role-scope-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/role-scope-reviewed-ten-pass.json) — tests[1], tests[2], tests[4], tests[5], tests[6]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-role-scope-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** Chưa có UI evidence trực tiếp cho ca này; cần ghi nhận thao tác và kết quả tương ứng.
- **Verdict:** PARTIAL — API/SQL theo trạng thái ca đạt; còn UI Reception báo hỏng.

## E2E-31 — HK Supervisor xác minh nguyên nhân sự cố

- **Preconditions:** Sự cố phòng được báo và có người liên quan.
- **Actor:** HK Supervisor, Maintenance Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Incident nội bộ/ngoài; ý kiến nguyên nhân qua các vòng.
- **Steps:** HKS xác nhận/chọn người liên quan; MS ghi nhận điều tra.
- **Expected Result:** Reporter riêng; nguyên nhân, ý kiến và lịch sử không bị ghi đè.
- **Actual Result:** HKS/MS xác minh theo quyền, người báo/người phát hiện được giữ riêng; HKS chọn người liên quan qua UI. Full cross-branch matrix theo dõi riêng E2E47.
- **API Evidence:** [Maintenance17](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[3], tests[4], tests[11], tests[12]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [Inspection browser](../../artifacts/restructure-v2/maintenance-inspection-browser.json)
- **Verdict:** PASS nhánh xác minh nguyên nhân TEST đã chạy.

## E2E-32 — Maintenance Supervisor tạo và phân công phiếu

- **Preconditions:** Sự cố đã được HKS xác nhận; staff/template hợp lệ.
- **Actor:** Maintenance Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phiếu nội bộ/ngoài TEST.
- **Steps:** Tạo/nhận phiếu, chọn nội bộ và giao staff.
- **Expected Result:** Một child, đúng executor/template; thử lại không nhân đôi.
- **Actual Result:** MS phân công nội bộ/sửa ngoài không note qua UI; retry không đổi child/history; sai assignee/template rollback.
- **API Evidence:** [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[6], tests[14]; lượt 2026-11-25T05:26:40.437Z. [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[6], tests[14]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [maintenance-created-internal.png](../../artifacts/restructure-v2/maintenance-created-internal.png)
- **Verdict:** PASS trong phạm vi TEST; SignalR kiểm riêng

## E2E-33 — Maintenance Staff nhận phiếu, checklist và sửa chữa

- **Preconditions:** Staff có phiếu được giao và checklist kỹ thuật.
- **Actor:** Maintenance Staff.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phiếu bảo trì nội bộ TEST.
- **Steps:** Nhận/start, gắn template, điền checklist và hoàn thành.
- **Expected Result:** Chỉ executor phù hợp thao tác; đủ checklist mới hoàn thành.
- **Actual Result:** Assigned staff nhận/start/gắn template/checklist/complete qua UI; API chặn thiếu checklist và sai executor.
- **API Evidence:** [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[6], tests[7]; lượt 2026-11-25T05:26:40.437Z. [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[6], tests[7]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [maintenance-started-internal.png](../../artifacts/restructure-v2/maintenance-started-internal.png), [maintenance-checklist-internal.png](../../artifacts/restructure-v2/maintenance-checklist-internal.png)
- **Verdict:** PASS trong phạm vi TEST

## E2E-34 — Maintenance Supervisor nghiệm thu

- **Preconditions:** Staff hoàn thành sửa và chờ review.
- **Actor:** Maintenance Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phiếu chờ nghiệm thu kỹ thuật.
- **Steps:** Thử review bằng staff; MS nghiệm thu; xem hàng chờ HKS.
- **Expected Result:** Staff không tự nghiệm thu; MS chuyển kết quả cho HKS.
- **Actual Result:** MS nghiệm thu kỹ thuật qua UI, staff không tự review, early HK inspection bị chặn.
- **API Evidence:** [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[7], tests[15]; lượt 2026-11-25T05:26:40.437Z. [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[7], tests[15]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [maintenance-reviewed-internal.png](../../artifacts/restructure-v2/maintenance-reviewed-internal.png)
- **Verdict:** PASS trong phạm vi TEST

## E2E-35 — Không thể sửa và đề xuất thay mới

- **Preconditions:** Phiếu sửa/tài sản TEST đủ điều kiện đề xuất thay.
- **Actor:** Maintenance Staff, Maintenance Supervisor, OM, HK Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Đề xuất thay tài sản TEST và tài sản thay thế.
- **Steps:** Đề xuất, MS xác nhận, OM duyệt/từ chối, lắp đặt và kiểm tra lại.
- **Expected Result:** Thay thực tế sau duyệt, giữ lịch sử; chỉ hoàn thành khi kiểm đạt.
- **Actual Result:** [Lượt HTTP tích hợp](../../artifacts/restructure-v2/incident-allocation-snapshot-failure.json): đề xuất → MS xác nhận → OM từ chối/duyệt → thay thực tế → checklist mới → nghiệm thu kỹ thuật → HKS PASS/Ready đạt. Toàn runner vẫn24 PASS/1 FAIL tại fixture legacy phía sau; không gọi toàn suite PASS.
- **API Evidence:** [Lượt HTTP tích hợp](../../artifacts/restructure-v2/incident-allocation-snapshot-failure.json): đề xuất → MS xác nhận → OM từ chối/duyệt → thay thực tế → checklist mới → nghiệm thu kỹ thuật → HKS PASS/Ready đạt. Toàn runner vẫn24 PASS/1 FAIL tại fixture legacy phía sau; không gọi toàn suite PASS. [Assertions](../../scripts/verification/v2-replacement-http-probe.mjs).
- **Database Evidence:** Cùng artifact kiểm vị trí tài sản, history/replay và tồn tiêu hao không đổi; [16 service/SQL checks bổ sung](../../artifacts/restructure-v2/replacement-runtime-results.json).
- **UI Evidence:** Browser assertions propose/approve/install và inspection đã PASS trong artifact tích hợp. Ảnh replacement hiện có bị lượt sau ghi đè, không dùng làm ảnh nguyên bản của lượt này.
- **Verdict:** PASS kịch bản TEST; toàn runner24 PASS/1 FAIL được giữ nguyên.

## E2E-36 — HK Supervisor kiểm tra không đạt, yêu cầu sửa lại

- **Preconditions:** Sửa đã qua review kỹ thuật; HKS kiểm chưa đạt.
- **Actor:** HK Supervisor, Maintenance Staff, Maintenance Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Hai vòng repair checklist/review TEST.
- **Steps:** HKS FAIL kèm lý do, staff sửa lại, MS review, HKS PASS.
- **Expected Result:** Trả đúng việc, reset checklist và giữ lịch sử hai vòng.
- **Actual Result:** HK FAIL với lý do ngắn qua UI, trả đúng việc về PENDING, reset checklist và giữ hai vòng lịch sử; sửa lại và PASS đưa Ready.
- **API Evidence:** [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[8], tests[9]; lượt 2026-11-25T05:26:40.437Z. [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) — tests[8], tests[9]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-maintenance-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [maintenance-hks-fail.png](../../artifacts/restructure-v2/maintenance-hks-fail.png), [maintenance-hks-pass.png](../../artifacts/restructure-v2/maintenance-hks-pass.png)
- **Verdict:** PASS trong phạm vi TEST

## E2E-37 — Phòng chỉ Ready khi toàn bộ blocker đã xử lý

- **Preconditions:** Phòng có incident/repair/cleaning và blocker cần đánh giá.
- **Actor:** HK Supervisor, các actor xử lý blocker.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Cùng phòng có hai asset/sự cố độc lập và hai blocker; sau đó phòng OCCUPIED có SAFETY hold riêng.
- **Steps:** Hoàn tất từng blocker, thử Ready trước/sau blocker cuối.
- **Expected Result:** Chỉ Ready khi toàn bộ blocker được xử lý.
- **Actual Result:** QB tests[8–11] đạt: hai blocker cùng turnover; HKS PASS sự cố đầu chỉ đóng blocker tương ứng, phòng vẫn BLOCKED, cleaning chờ kiểm và reserve bị 409. PASS sự cố cuối hoàn tất cleaning và Ready. Khi phòng có khách, repair PASS giữ OCCUPIED và SAFETY hold, không chuyển Ready.
- **API Evidence:** [quantity-blockers-twelve-pass.json](../../artifacts/restructure-v2/quantity-blockers-twelve-pass.json) — tests[8–11], lượt 2026-11-25T11:25:54.666Z, toàn lượt 12 PASS/0 FAIL. [probe/assertions](../../scripts/verification/v2-maintenance-blockers-probe.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/quantity-blockers-twelve-pass.json): API đọc lại room history/blocks, incident/repair/cleaning status, turnover và occupancy sau mỗi commit; reserve bị chặn không đổi phòng. Không có raw SQL snapshot độc lập cho riêng ca này.
- **UI Evidence:** HKS gửi PASS qua Edge: [sự cố đầu](../../artifacts/restructure-v2/blockers-first-hks-pass.png), [sự cố cuối](../../artifacts/restructure-v2/blockers-second-hks-pass.png), [phòng có khách/SAFETY](../../artifacts/restructure-v2/blockers-safety-occupied-hks-pass.png).
- **Verdict:** PASS phạm vi nhiều blocker và occupied/SAFETY trên TEST; không tự đóng sự cố vật lý của phòng local.

## E2E-38 — Tạo yêu cầu điều chuyển DVBV → DVCT

- **Preconditions:** Hai chi nhánh TEST có kho nguồn/đích và item hợp lệ.
- **Actor:** OM đích.
- **Branch:** Hai chi nhánh TEST A (nguồn) và B (đích), tương ứng hướng DVBV tới DVCT; ID do fixture tra cứu.
- **Test Data:** Yêu cầu HTTP 8, UI 3; hướng nguồn/đích tương ứng DVBV tới DVCT.
- **Steps:** OM đích lập/gửi yêu cầu; kiểm nguồn nhận.
- **Expected Result:** Phiếu lưu đúng nguồn/đích và số yêu cầu.
- **Actual Result:** OM đích tạo/gửi yêu cầu 3 qua UI, nguồn nhận đúng; đơn vị cơ sở và gói 2 qua API.
- **API Evidence:** [transfer-eleven-pass.json](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[2], tests[9]; lượt 2026-11-25T04:21:45.875Z. [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[2], tests[9]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [transfer-manager-desktop.png](../../artifacts/restructure-v2/transfer-manager-desktop.png)
- **Verdict:** PASS trong phạm vi TEST hai chi nhánh

## E2E-39 — OM nguồn xác nhận hoặc từ chối

- **Preconditions:** Phiếu điều chuyển gửi tới OM nguồn.
- **Actor:** OM nguồn, OM đích.
- **Branch:** Hai chi nhánh TEST A (nguồn) và B (đích), tương ứng hướng DVBV tới DVCT; ID do fixture tra cứu.
- **Test Data:** UI yêu cầu3, duyệt2; HTTP yêu cầu8. Phiếu riêng1 đơn vị cho nhánh từ chối.
- **Steps:** OM nguồn xác nhận một phần hoặc từ chối qua popup; thử OM đích tự duyệt, từ chối thiếu lý do và replay.
- **Expected Result:** Chỉ OM nguồn quyết định; requested riêng approved; từ chối có lý do và lịch sử, không đổi tồn.
- **Actual Result:** OM nguồn duyệt2/3, OM đích không tự duyệt. Phiếu riêng bị từ chối qua popup, giữ đúng lý do/actor trong lịch sử; thiếu lý do bị chặn, snapshot stock/batch/SQL không đổi; replay409 không thêm lịch sử.
- **API Evidence:** [Transfer15](../../artifacts/restructure-v2/transfer-rejection-fifteen-pass.json) — tests[2–3,10], [probe](../../scripts/verification/v2-transfer-rejection-browser.mjs), [kết quả từ chối](../../artifacts/restructure-v2/transfer-rejection-results.json).
- **Database Evidence:** [Transfer15](../../artifacts/restructure-v2/transfer-rejection-fifteen-pass.json) — probe đối chiếu snapshot source/destination batches và fixture SQL Inspect trước/sau; event REJECT lưu đúng actor/lý do, replay không đổi. Không có raw SQL snapshot riêng.
- **UI Evidence:** [Kết quả browser](../../artifacts/restructure-v2/transfer-rejection-results.json), [desktop](../../artifacts/restructure-v2/transfer-rejection-desktop.png), [mobile](../../artifacts/restructure-v2/transfer-rejection-mobile.png), [xác nhận một phần](../../artifacts/restructure-v2/transfer-manager-desktop.png).
- **Verdict:** PASS cả xác nhận một phần và từ chối có lý do trên TEST; chưa kiểm hai quyết định khác nhau đồng thời.

## E2E-40 — Storekeeper nguồn xác nhận gửi hàng

- **Preconditions:** Yêu cầu được nguồn duyệt, có hai lô FEFO.
- **Actor:** Storekeeper nguồn.
- **Branch:** Hai chi nhánh TEST A (nguồn) và B (đích), tương ứng hướng DVBV tới DVCT; ID do fixture tra cứu.
- **Test Data:** Hai shipment; lô hạn sớm/muộn.
- **Steps:** SK nguồn ghi gửi; thử sai actor, retry và vượt approved.
- **Expected Result:** Gửi đúng approved/FEFO; không ghi sổ trùng.
- **Actual Result:** Hai shipment FEFO, retry không ghi sổ thêm, SK đích không xuất thay nguồn; SK nguồn ghi sổ qua UI.
- **API Evidence:** [transfer-eleven-pass.json](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[3], tests[4], tests[5], tests[9]; lượt 2026-11-25T04:21:45.875Z. [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[3], tests[4], tests[5], tests[9]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [transfer-otherStore-desktop.png](../../artifacts/restructure-v2/transfer-otherStore-desktop.png)
- **Verdict:** PASS trong phạm vi gửi/chống lặp đã kiểm

## E2E-41 — Storekeeper đích nhập số lượng thực nhận

- **Preconditions:** Shipment thực chờ kho đích nhận.
- **Actor:** Storekeeper đích.
- **Branch:** Hai chi nhánh TEST A (nguồn) và B (đích), tương ứng hướng DVBV tới DVCT; ID do fixture tra cứu.
- **Test Data:** Partial receipt, expiry, receivedAt và lô gốc.
- **Steps:** Nhập thực nhận; kiểm received và provenance.
- **Expected Result:** Chỉ tăng thực nhận, giữ lô nguồn và số yêu cầu/gửi gốc.
- **Actual Result:** SK đích nhập lượng thực nhận, giữ lô nguồn/expiry/receivedAt; requested/approved/shipped chỉ đọc trên UI.
- **API Evidence:** [transfer-eleven-pass.json](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[6], tests[7], tests[9]; lượt 2026-11-25T04:21:45.875Z. [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[6], tests[7], tests[9]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [transfer-store-desktop.png](../../artifacts/restructure-v2/transfer-store-desktop.png)
- **Verdict:** PASS trong phạm vi TEST

## E2E-42 — Xử lý chênh lệch Requested/Shipped/Received

- **Preconditions:** Phiếu có Requested/Shipped/Received khác nhau.
- **Actor:** OM và Storekeeper hai chi nhánh.
- **Branch:** Hai chi nhánh TEST A (nguồn) và B (đích), tương ứng hướng DVBV tới DVCT; ID do fixture tra cứu.
- **Test Data:** HTTP thiếu 2/8; UI 3/2/2/1/1.
- **Steps:** Đọc đối soát sau nhận thiếu; kiểm in-transit/ledger.
- **Expected Result:** D12: giữ thiếu chờ đối soát, không tự ghi mất hoặc hoàn kho.
- **Actual Result:** D12 giữ thiếu 2/8, tổng nguồn+đích+đang đi bằng nhập; nhiều shipment/gói quy đổi; mobile đối soát 3/2/2/1/1.
- **API Evidence:** [transfer-eleven-pass.json](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[6], tests[8], tests[9], tests[10]; lượt 2026-11-25T04:21:45.875Z. [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[6], tests[8], tests[9], tests[10]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [transfer-pending-mobile.png](../../artifacts/restructure-v2/transfer-pending-mobile.png)
- **Verdict:** PASS trong phạm vi chờ đối soát, không tự tất toán

## E2E-43 — Không nhận hàng trùng hoặc vượt phạm vi

- **Preconditions:** Shipment TEST còn lượng có thể nhận.
- **Actor:** Storekeeper nguồn/đích.
- **Branch:** Hai chi nhánh TEST A (nguồn) và B (đích), tương ứng hướng DVBV tới DVCT; ID do fixture tra cứu.
- **Test Data:** Retry cùng phiếu, hai receipt cạnh tranh và nhận vượt.
- **Steps:** Gửi receipt đồng thời/replay, thử vượt và sai kho.
- **Expected Result:** Một commit hợp lệ; chặn vượt/sai scope, không nhận đôi.
- **Actual Result:** Cùng request hoặc hai phiếu khác gửi đồng thời chỉ một commit; chặn vượt, sai source/destination; lần nhận tiếp đủ số còn lại.
- **API Evidence:** [transfer-eleven-pass.json](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[5], tests[7], tests[8], tests[10]; lượt 2026-11-25T04:21:45.875Z. [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/transfer-eleven-pass.json) — tests[5], tests[7], tests[8], tests[10]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** Chưa có UI evidence trực tiếp cho ca này; cần ghi nhận thao tác và kết quả tương ứng.
- **Verdict:** PARTIAL theo hồ sơ §37 — HTTP/SQL chống nhận trùng/vượt/sai scope đạt; còn UI trực tiếp.

## E2E-44 — SignalR thông báo realtime đúng người

- **Preconditions:** Hub thật, hai chi nhánh và recipient TEST.
- **Actor:** Người nhận đúng quyền, actor đối chứng khác chi nhánh.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Phiếu điều chuyển thật gửi/duyệt qua HTTP; hub độc lập dùng SQL fixture được ghi rõ.
- **Steps:** Bốn browser nhận event gửi/duyệt, kiểm badge/popup/list, replay, read ownership và positive control cùng stream; hub riêng kiểm reconnect/revoke.
- **Expected Result:** Chỉ người đúng scope nhận notification đã commit.
- **Actual Result:** [15/15 tích hợp](../../artifacts/restructure-v2/transfer-business-realtime-fifteen-pass.json), [bốn Edge contexts](../../artifacts/restructure-v2/business-realtime-verified-pass.json): đề xuất fallback → phiếu HTTP thật → thông báo sau commit → badge/popup/list tự cập nhật. Replay không tạo trùng, read ownership đúng, positive control cùng stream. Cleanup sạch; external AI chưa cấu hình.
- **API Evidence:** [15/15 tích hợp](../../artifacts/restructure-v2/transfer-business-realtime-fifteen-pass.json), [bốn Edge contexts](../../artifacts/restructure-v2/business-realtime-verified-pass.json): đề xuất fallback → phiếu HTTP thật → thông báo sau commit → badge/popup/list tự cập nhật. Replay không tạo trùng, read ownership đúng, positive control cùng stream. Cleanup sạch; external AI chưa cấu hình. [Hub riêng](../../artifacts/restructure-v2/realtime-reviewed-fifteen-pass.json).
- **Database Evidence:** [Business event evidence](../../artifacts/restructure-v2/business-realtime-verified-pass.json): persisted recipient IDs/replay/read ownership; không chèn notification SQL.
- **UI Evidence:** [Bốn Edge contexts thực](../../artifacts/restructure-v2/business-realtime-verified-pass.json), automatic badge/popup/list/status, LongPolling không transport errors.
- **Verdict:** PASS phạm vi TEST đã chạy; không kết luận SLA hoặc external AI.

## E2E-45 — AI dự báo nhu cầu vật tư

- **Preconditions:** Có lịch sử tiêu hao TEST; external provider chưa cấu hình.
- **Actor:** OM.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Tiêu hao 1, cấp 17, hoàn 16; hai dự báo.
- **Steps:** Yêu cầu DEMAND, xem phương pháp/evidence; so ledger.
- **Expected Result:** Dùng tiêu hao thật; fallback có nhãn và không mutation.
- **Actual Result:** DEMAND fallback có nhãn, giữ ledger, chặn branch/horizon sai; dữ liệu 1 tiêu hao/17 cấp/16 hoàn dự báo đúng 2, transfer không tính thành tiêu hao.
- **API Evidence:** [housekeeping-expiry-settlement-twenty-pass.json](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[18]; lượt 2026-11-25T07:49:31.988Z. [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs)
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json) — tests[18]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-housekeeping-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này.
- **UI Evidence:** [advisory-demand-desktop.png](../../artifacts/restructure-v2/advisory-demand-desktop.png)
- **Verdict:** PARTIAL cho AI runtime — fallback STATISTICAL_RULES PASS; external provider BLOCKED_EXTERNAL_CONFIGURATION theo D07.

## E2E-46 — AI đề xuất điều chuyển và phân tích sự cố

- **Preconditions:** Có tồn hai chi nhánh và incident TEST; external provider chưa cấu hình.
- **Actor:** OM, HK Supervisor, Maintenance Supervisor.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** TRANSFER gợi ý dương 2; INCIDENT có dữ kiện.
- **Steps:** Gọi TRANSFER/INCIDENT, xem evidence; so ledger/incident.
- **Expected Result:** Giữ tối thiểu nguồn, trừ incoming; không quy kết người báo hay tự mutation.
- **Actual Result:** TRANSFER đề xuất dương 2 và trừ incoming; INCIDENT HKS/MS mobile, branch/invalid ID guards, không mutation/không quy kết người báo.
- **API Evidence:** [transfer-advisory-fourteen-pass.json](../../artifacts/restructure-v2/transfer-advisory-fourteen-pass.json) — tests[11], tests[13]; lượt 2026-11-25T05:16:31.204Z. [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs); [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) tests[5,13] kiểm INCIDENT.
- **Database Evidence:** [kết quả cùng lượt](../../artifacts/restructure-v2/transfer-advisory-fourteen-pass.json) — tests[11], tests[13]; assertion đối chiếu SQL/ledger/custody theo [runner/assertions](../../scripts/verification/v2-transfer-runtime.mjs). Không có raw SQL snapshot độc lập trong hồ sơ này. [maintenance-report-ui-seventeen-pass.json](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json) tests[5,13]: incident/inventory không mutation.
- **UI Evidence:** [advisory-transfer-positive-desktop.png](../../artifacts/restructure-v2/advisory-transfer-positive-desktop.png), [advisory-incident-supervisor-mobile.png](../../artifacts/restructure-v2/advisory-incident-supervisor-mobile.png)
- **Verdict:** PARTIAL cho AI runtime — TRANSFER/INCIDENT fallback PASS; external provider BLOCKED_EXTERNAL_CONFIGURATION theo D07.

## E2E-47 — RBAC, Branch Scope và Unified Internal Request

- **Preconditions:** Actor TEST có role/membership hai chi nhánh và request hiện có.
- **Actor:** OM, Reception, HK, Storekeeper, kỹ thuật, Administrator theo quyền.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Ordinary HK request, room incident và request mới.
- **Steps:** Đối chiếu read/create/act và scope trên các họ request.
- **Expected Result:** RBAC, branch scope và Unified Request nhất quán.
- **Actual Result:** Đã kiểm đại diện đủ9 role:7 role chi nhánh có16 lần đọc sai chi nhánh trả403, OWNER/SYS_ADMIN có11 kiểm tra GET/POST thực. Các họ ordinary/refill/replenishment/incident/maintenance/write-off/disposal/transfer/replacement có evidence riêng. Replenishment đủ chuỗi UI/API/SQL, nhận1+1 đúng2, replay không nhập trùng.
- **API Evidence:** [Ma trận từng họ](REQUEST_SCOPE_MATRIX.md), [16 denial](../../artifacts/restructure-v2/request-scope-matrix-results.json), [global11](../../artifacts/restructure-v2/global-role-read-scope-results.json), [replenishment7](../../artifacts/restructure-v2/replenishment-scope-results.json), [role scope10](../../artifacts/restructure-v2/role-scope-reviewed-ten-pass.json).
- **Database Evidence:** Các probe đối chiếu snapshot SQL trước/sau denial; global kiểm số work/document/movement của actor, replenishment đối chiếu ledger/batch và request history. [Runner replenishment](../../scripts/verification/v2-replenishment-scope-runtime.mjs), [global runner](../../scripts/verification/v2-global-role-read-scope.mjs).
- **UI Evidence:** [OM approve](../../artifacts/restructure-v2/replenishment-approved-desktop.png), [SK receipt mobile](../../artifacts/restructure-v2/replenishment-received-mobile.png), [OM reject](../../artifacts/restructure-v2/replenishment-rejected-desktop.png), [workspace](../../artifacts/restructure-v2/request-workspace-manager.png).
- **Verdict:** PASS phạm vi9 role và các họ workflow đã nêu trên TEST; không suy thành mọi role×endpoint hoặc các nhánh legacy bypass chưa chạy.

## E2E-48 — Inventory Reconciliation và Regression toàn hệ thống

- **Preconditions:** TEST có ledger, custody, held, asset và transfer.
- **Actor:** OM, Storekeeper và actor regression.
- **Branch:** Chi nhánh TEST của actor/phòng/kho; chi nhánh khác chỉ dùng khi assertion nêu rõ.
- **Test Data:** Receipt/issue/return tổng hợp, custody, in-transit; dữ liệu migration riêng.
- **Steps:** Đối soát ledger/custody/asset/held/in-transit; chạy regression.
- **Expected Result:** Không lệch tồn/sở hữu; regression tương thích toàn hệ thống.
- **Actual Result:** Audit sau cleanup lúc 19:42:35 kiểm 14 nhóm, mọi mismatch=0:184 vị trí reusable,11 nhóm transit,185 số dư kho tiêu hao,55 custody tiêu hao,57 reservation,541 movement đã map,200 tài sản pending và constraint trust. Regression155 PASS/3 SKIP; kho43,housekeeping26,transfer15,replenishment7 checks đạt trên cùng binary mới. Legacy chờ kiểm kê được báo riêng.
- **API Evidence:** [Kho43](../../artifacts/restructure-v2/inventory-fifo-expiry-damage-ui-43-pass.json), [housekeeping26](../../artifacts/restructure-v2/housekeeping-assignment-scope-twentysix-pass.json), [transfer15](../../artifacts/restructure-v2/transfer-rejection-fifteen-pass.json), [replenishment](../../artifacts/restructure-v2/replenishment-scope-results.json), [regression](../../artifacts/restructure-v2/role-session-build-test-summary.json).
- **Database Evidence:** [Audit cuối](../../artifacts/restructure-v2/ledger-reconciliation-final.json), [SQL invariants](../../scripts/verification/v2-ledger-reconciliation.sql), [kettle200](../../artifacts/restructure-v2/kettle-pending-final.json). Số dư reusable lấy opening có provenance + movement vào − ra; transit đối chiếu độc lập chứng từ gửi/thực nhận.
- **UI Evidence:** [FIFO](../../artifacts/restructure-v2/fifo-issue-browser.json), [reusable DAMAGED](../../artifacts/restructure-v2/reusable-ready-damaged-browser.json), [receipt mobile](../../artifacts/restructure-v2/replenishment-received-mobile.png), [transfer](../../artifacts/restructure-v2/transfer-browser-results.json).
- **Verdict:** PASS đối soát sổ và regression trong phạm vi TEST;3 tests SKIP không tính đạt, kiểm kê vật lý/local rollout/provider ngoài không thuộc kết luận này.

