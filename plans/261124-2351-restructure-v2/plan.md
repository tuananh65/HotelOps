# HotelOps Compact — restructuring V2

Nguồn: `docs/restructure-v2/OWNER_REQUEST.md`, được Owner xác nhận là nguồn chính thức; không chờ Note riêng. Quyết định 25/11 tại `DECISION_AND_BLOCKER_REGISTER.md`. HEAD baseline `d109839d9f1300c66f8d71b7d4957bc39dab84fd`, nhánh `feature/project2-role-alignment-20261123`.

## Phạm vi và điều kiện
- Delta fullstack theo 40 CHG, 48 E2E; giữ UX được chấp nhận ngoài thay đổi mới.
- Không reset, không đổi mật khẩu tài khoản cũ, không tạo tồn/hạn dùng giả.
- Audit database chính chỉ đọc. Backup/verify/restore trước migration; chỉ migration database test riêng đã xác minh.
- Không commit/push/deploy trong nhiệm vụ này.
- Phần còn đề xuất chưa có quyết định được ghi blocker, không tự khóa policy.

## Thực hiện
- [x] Phase 0: audit source/database, baseline build/tests, matrix 40 CHG và decision register.
- [x] Phase 1: role correction/recovery có bằng chứng, scope và auth.
- [x] Phase 2: inventory foundation, batch/expiry, reusable, transactions.
- [x] Phase 3: assessment/reservations/checklists, shift allocation/return.
- [x] Phase 4: maintenance staff, cause, replacement, readiness.
- [x] Phase 5: interbranch shipment/receipt/discrepancy.
- [x] Phase 6: account invitation/first-login enforcement.
- [x] Phase 7: persistent notification + authenticated SignalR.
- [x] Phase 8: AI contract/provider/fallback, không tự mutation.
- [x] Phase 9: migration, reconciliation, 48 E2E, 8 demo và independent review.
- [ ] Phase 10: SRS/design/testing documents và receipt có bằng chứng.

## Trạng thái cuối lượt

### Cập nhật 19:47 ngày25/11
- Implementation và nghiệm chứng TEST đã khép các gap47/48:global11,replenishment7,ledger14 nhóm mismatch0;9 role có kiểm đại diện,10 Maintenance Staff giữ credential. Các checklist phase hoàn tất áp dụng cho TEST với fallback/SMTP sink theoD07/D08.
- Source mới nhất buildPASS155 testsPASS/3SKIP,4NU1900; không thay source sản phẩm sau lượt này. Kho43/HK26/transfer15/role20 và audit cuối có artifacts.
- Phase10 còn nội dung/mẫu Word chính thức Report3/4/5 ngoài các register đã cập nhật; không tự nhận nghiệm thu các tài liệu chưa có mẫu. ExternalAI/SMTP,kiểm kê vật lý,localcutover vẫn chưa hoàn tất.
- Không có approval đang chờ, không commit/push/deploy hoặc ghi DB vận hành.

## Lịch sử thực hiện

### Cập nhật 19:35 ngày 25/11
- Các dấu hoàn thành phase ở trên áp dụng cho implementation và kiểm chứng trên TEST; không phải cutover local hoặc provider bên ngoài. Phase1 còn chốt global scope, phase9 còn đối soát cuối và replenishment; phase10 còn hợp nhất receipt.
- Sửa lỗi phiên cũ sau thay quyền trong UserService; build thành công,155 PASS/3 SKIP,4 NU1900. Role/session20/20, kho43/43, housekeeping/assignment26/26, transfer/rejection15/15 đều đã chạy và cleanup.
- Roster10 Maintenance Staff đã kiểm API/Edge; đang kiểm alias DEPT_MANAGER và Owner/Admin. Không đổi credentials người cũ.
- Gói rollout có manifest11 scripts/hash và preflight offline; không phải runner migration. Local49 bảng vẫn giữ nguyên; TEST57. Vite5174 PID19776 còn cần cho browser, fixture5779 được dùng độc quyền. Không có approval đang chờ.


### Cập nhật 18:45 ngày25/11
- Build thành công,4 NU1900; tests155 PASS/3 SKIP.
- Low-stock10/10, quantity/blockers12/12, housekeeping/rework/refill25/25, phone/resource focused PASS.
- Transfer/business realtime15/15 PASS, cleanup sạch. Replacement và legacy đạt riêng trong lượt có lỗi harness phía sau; giữ kết quả toàn lượt.
- Database vận hành chưa thay; Maintenance Staff TEST active10/local inactive0. External AI/SMTP chưa cấu hình. Tiếp tục rà Master còn thiếu.

### Kiểm chứng cập nhật 25/11/2026 18:00
- Notification visibility cache theo snapshot/page đã qua review độc lập; giữ nguyên branch/role/ownership và không cache giữa pulse. Build `.artifacts/finish-realtime` thành công, 4 cảnh báo NU1900; chạy toàn solution: 155 PASS, 3 SKIP. Không suy ra tests SKIP hoặc kiểm vulnerability đã đạt.
- Low-stock UI lượt đầu timeout tại Inbox/CanReadTransfer; giữ `low-stock-inbox-timeout-failure.json`. Bản cache mới trả API và hiển thị thông báo đúng trên ảnh, nhưng harness thiếu barrier và lọc trùng thông báo cùng item; giữ `low-stock-render-barrier-failure.json`, sửa chờ response branch và exact heading. Chưa tính UI PASS.
- Legacy fixture đã sửa theo CK_work_items_parent_kind: INCIDENT không có parent, dùng SOURCE tới CLEANING. Không thay constraint/schema; kiểm recovery ParentWorkId không thể tạo hợp lệ dưới schema hiện tại. Đang chạy focused legacy/quantity/multiple-blockers trên TEST.
- Probe phone/OM resources mới đã qua kiểm syntax/review; sửa initialQuantity từ 0 thành 1 theo contract. Chưa runtime. Hồ sơ 48 ca đã sửa mất dấu, 48 x 11 trường và 248 liên kết hợp lệ; không nâng trạng thái nghiệm thu.
- Vite5174 giữ cho browser. Database vận hành và Git remote chưa thay đổi; Maintenance Staff hoạt động trên TEST, local chưa migration. Không có câu hỏi phê duyệt đang chờ.

### Kiểm chứng cập nhật 25/11/2026 16:10
- Low-stock đạt 8/8 HTTP/SQL trên TEST: `low-stock-eight-pass.json`. Khả dụng loại hàng hết hạn/cách ly; reusable chỉ READY, ghi rõ đồ đang giặt; cùng đợt thiếu cập nhật nội dung và giữ trạng thái đọc, hết thiếu đóng rồi tái thiếu tạo episode mới. Gửi/nhận điều chuyển cập nhật cả hai chi nhánh; lượng thiếu nhận vẫn chờ đối soát. Cleanup activeFixtureItems=0.
- Sửa production: verify lô/configure inventory, chuyển trạng thái reusable và post transfer/receipt reconcile cảnh báo trong cùng transaction. Review độc lập không finding mới. Không tự reserve chứng từ approved.
- Build tích hợp mới `lowstock-integrated-build.log` PASS 0 warning/error; `lowstock-integrated-tests.log` 142 PASS/3 SKIP. Không suy ra các tests SKIP đã chạy hoặc NuGet vulnerability scan đã đạt.
- Maintenance các lượt trước có 24 PASS rồi lỗi harness: rowVersion cấu hình, GET checklist chưa tải xong, và capacity12 lớn hơn allocation snapshot10. Giữ artifacts lỗi; sửa helper theo contract, không đổi nghiệp vụ để làm test xanh. Đang chạy lại toàn lượt với replacement/legacy/quantity.
- Review refill continuation xác nhận TEST103 đã READY sau expiry check, peer đủ điều kiện ca mới và cân bằng 35=22 kho+13 dùng. Chưa runtime. Business SignalR helper dùng phiếu thật, bốn actor/browser, heartbeat barrier; chưa runtime.
- Đối soát tiến trình: giữ Vite5174 PID15736; low-stock runner đã dọn TEST5779. Không có phê duyệt đang chờ. Không thay DB vận hành/Git remote.

### Kiểm chứng cập nhật 25/11/2026 15:05
- Đăng nhập/khôi phục chỉ dùng **Username**, không dùng EmployeeCode; đây là quyết định Owner mới nhất, thay mô tả dual identity ở checkpoint cũ. `login-recovery-username-nineteen-pass.json`: 19/19 HTTP/SMTP sink/Edge PASS, có collision, reset, thu hồi phiên cũ, token một lần/hết hạn; hai tài khoản TEST inactive, API5778/SMTP2526 đã dừng.
- `login-layout-browser.json`: 4/4 viewport PASS sau chạy lại; popup quên mật khẩu tối đa420px, nút Hủy đỏ/Gửi liên kết đặt lại xanh cùng hàng, không tràn ngang ở320px. Đã xem ảnh desktop/mobile.
- `housekeeping-expiry-settlement-twenty-pass.json`: 20/20 PASS. Worker thật kết ca đúng deadline một lần; giữ custody/reservation, hoàn tất việc cũ và đối soát bằng popup; ca mới bị chặn trước đối soát và được phép sau đó. Retry không ghi sổ hai lần; dọn lại không tự chuyển bảo trì.
- Review sự cố phát hiện ba điểm cần sửa: giới hạn bổ sung theo thiếu thực tế, bỏ cắt ngầm200 sự cố bằng phân trang/tìm kiếm, khôi phục liên kết cleaning từ SOURCE/ParentWorkId của incident legacy. Source đã sửa; chưa coi runtime mới là PASS.
- Quantity regression: 137 tests PASS/3 SKIP; build riêng thành công với2 warning NU1900 do feed NuGet không truy cập được. Không bỏ qua warning hoặc dùng build cũ để chứng minh source cuối.
- `incident_runtime_execute` dùng độc quyền fixture housekeeping/inventory và5779 khi source qua review; `incident_quantity_review` đọc độc lập; `uc_report_alignment` đối chiếu tài liệu. Vite5174 PID15736 còn cần cho kiểm UI; dotnet buildhost của VSCode giữ nguyên. Không có listener TEST5778/5779/2526 tại lúc đối soát.
- Không có câu hỏi phê duyệt đang chờ; không thay DB vận hành, mật khẩu cũ hoặc Git remote. Master48 E2E/8 demo và hợp đồng schema phát hành còn thiếu.

### Kiểm chứng cập nhật 25/11/2026 14:02
- Cấu hình/báo cáo đạt 15/15 HTTP và Edge: `configuration-report-reviewed-fifteen-pass.json`; cleanup sạch. Có role-only settings, tạo loại báo cáo phòng, một tiêu đề workspace, bỏ menu vật tư nhân viên, báo cáo mobile và ca AUTO_END đúng một lần.
- Báo cáo đọc sự kiện nghiệp vụ và audit AUTO_END, lọc ngày theo múi giờ chi nhánh; local-day đã đối chiếu với khoảng UTC tương đương. Review độc lập phát hiện thiếu audit AUTO_END, đã sửa và kiểm lại.
- Build backend 0 warning/error, 127 tests PASS/3 SKIP (`ux-delta-final-*`); frontend PASS (`ux-delta-reviewed-frontend.log`), hai warning PURE từ SignalR.
- Giữ lỗi selector mobile tại `configuration-mobile-selector-failure.json/png`. Không đổi UI hoặc tăng timeout để che lỗi; selector kiểm đúng cột dữ liệu mobile và các hành động thực tế.
- Sub-agent `login_finish` hoàn thiện lookup mã nhân viên/username và chuỗi reset qua SMTP sink; `shift_finish` kiểm hết ca với custody/reservation; `incident_finish` sửa popup/người tạo/đường xử lý legacy. Chưa tính các phần này PASS.
- Housekeeping/inventory fixture và cổng5779 do `shift_finish` dùng độc quyền; invitation dùng fixture/cổng riêng. Không chạy maintenance/replacement trùng fixture khi agent chưa trả kết quả.
- Process audit: Vite5174 còn cần cho browser; các API5080/5769/5774/Demo là tiến trình có trước; dotnet22548 MSBuild reuse,29304 VS Code. Không có fixture server mồ côi xác định được.

### Điều chỉnh Owner ngày 25/11/2026, 13:22
- Đối chiếu tệp Master mới: 2.853 dòng không khác nội dung; giữ D01–D12 đã xác nhận.
- Tiếp tục yêu cầu UI/nghiệp vụ theo 14 ảnh, chi tiết tại `docs/restructure-v2/OWNER_UX_DELTA_20261125.md`.
- Ba sub-agent đang xử lý đăng nhập, kết thúc ca, sự cố phòng. Agent chính xử lý cấu hình/báo cáo/điều hướng.
- Bỏ nút phạm vi quyền, gỡ route/menu xem vật tư nhân viên, thêm phone cho ứng viên phân ca và chặn tiêu đề lặp ở workspace yêu cầu. Chưa kiểm chứng build/runtime những thay đổi mới.
- Đã đối soát tiến trình: các dotnet mới 48944/49604/40476 thuộc VS Code/MSBuild; không dừng. Không có server fixture TEST mồ côi được xác định.

### Kiểm chứng cập nhật 25/11/2026 12:52
- Role-scope sau review/counsel đạt 10/10 (`role-scope-reviewed-ten-pass.json`), cleanup sạch. Reception trước/trong/sau ca và sai branch; OM đọc request thường nhưng create/actions bị chặn đúng mã lỗi. Room report OM bị chặn permission gate IR_CREATE, chưa đi tới guard dự phòng. Replay dùng room version mới xác nhận OPERATION_ALREADY_APPLIED.
- HK fixture thêm runId; role runner kiểm Inspect/Cleanup từ lượt khác bị chặn. Role/maintenance/housekeeping runners nhận diện metadata thuộc lượt hiện tại nếu Setup lỗi giữa chừng để finally vẫn cleanup; không dọn metadata của lượt khác. Chưa fault injection toàn bộ setup.
- Replacement HTTP/UI lượt đầu 9 PASS/1 FAIL vì selector OM khớp cả queue ngoài lẫn panel trong dialog. Đã giữ `replacement-nested-panel-failure.json`, counsel xác nhận scope dialog/heading parent; không dùng first(). Review bổ sung kiểm reset outcome/note/checkedAt/checkedBy, hai vị trí tài sản và thu hẹp claim stock thành consumable. Đang chạy lại toàn luồng đến technical/HKS inspection.
- Impact matrix đã bỏ nhận xét audit lỗi thời và đối chiếu lại 40 CHG; AI/UI/E2E/final report cập nhật đúng artifact. Lệnh ghi tài liệu qua PowerShell pipe làm mất dấu đã được sửa; không ảnh hưởng source sản phẩm.

### Kiểm chứng cập nhật 25/11/2026 12:44
- Reusable UI đạt toàn lượt 43/43: `inventory-reusable-ui-fortythree-pass.json`; READY→IN_USE→DIRTY→LAUNDRY→READY, nguồn trừ 4/đích cộng 4, tổng sở hữu giữ 10. Lỗi selector tên chung ở lượt trước được giữ; tên TEST có prefix riêng, không nới selector bằng first(). Cleanup activeFixtureItems=0.
- Maintenance UI mở rộng đạt 17/17: `maintenance-report-ui-seventeen-pass.json`. HK báo hỏng/quay lại việc, HKS xác nhận, MS phân công nội bộ/ngoài, staff nhận/start/checklist/complete, technical review và HKS FAIL/PASS. INCIDENT advisory HKS/MS mobile không mutation.
- Role-scope lượt đầu 9/9; review yêu cầu siết replay với room version mới, mã lỗi OM và cleanup khi setup lỗi một phần. Đã thêm runId cho housekeeping fixture; các runner hiện có đều truyền runId. Lượt siết đầu lỗi kỳ vọng OM_REQUEST_READ_ONLY: API chặn sớm bằng IR_CREATE/PERMISSION_DENIED đúng cấu hình. Giữ artifact lỗi, không đổi quyền để làm test xanh; đang chạy lại sau counsel.
- Không có server TEST mồ côi; Vite5174 dùng tiếp để kiểm UI. Các server người dùng 5080/5769/5774 và workspace Demo giữ nguyên. Không sửa DB vận hành/credentials cũ/Git remote. Replacement HTTP/UI và acceptance tổng thể còn thiếu.

### Kiểm chứng cập nhật 25/11/2026 12:17
- Invitation UI quản trị tạo/resend cùng first-login/setup đạt 15/15, 4 thư SMTP sink: `invitation-admin-ui-fifteen-pass.json`. Review độc lập không finding; cleanup activeAccounts=0. Giữ lỗi fixture tìm row trước GET refresh, đã thêm barrier kiểm user mới.
- Transfer/advisory đạt 14/14: `transfer-advisory-fourteen-pass.json`. Nguồn 3, minimum 1, đề xuất 2; incoming commitment đủ thì đề xuất 0. UI và ledger không mutation khi xem phân tích. Fixture đã dọn.
- Đang kiểm INCIDENT advisory trên hai UI giám sát và scope/không mutation. Không gọi AI/SMTP bên ngoài, không sửa DB vận hành.

### Kiểm chứng cập nhật 25/11/2026 12:10
- Housekeeping đạt 18/18 sau review: `housekeeping-completion-reviewed-eighteen-pass.json`. Browser thực hiện Start/End Shift, checklist và gửi hoàn thành; retry so cả snapshot ledger/chứng từ và lịch sử. Cleanup xác nhận sessions revoked, synthetic branch inactive.
- Dự báo từ tiêu hao thực 1, cấp 17, hoàn 16 cho kết quả 2 trong 14 ngày, không còn 2.0001; API chặn HK và không thay ledger. Giữ artifact lỗi làm tròn trước sửa. Build 0 warnings/errors, tests 122 PASS/3 SKIP.
- Đang mở rộng invitation UI quản trị tạo tài khoản/gửi lại, chỉ SMTP sink localhost. Không có server TEST mồ côi trước lượt này; giữ các server người dùng hiện có. Master 48 E2E/8 demos vẫn chưa hoàn tất.

### Kiểm chứng cập nhật 25/11/2026 12:02
- Lô/lịch sử đã đạt toàn lượt 13/13: `transfer-batch-pages-thirteen-pass.json`; tìm kiếm, paging biên/invalid/branch, array compatibility và modal lịch sử mobile. Giữ hai lỗi fixture mã lô/lookup cũ; không đổi backend vì hai lỗi đó.
- Audit TEST chỉ đọc: 117 nhóm kho tiêu hao, 24 số dư staff, 26 reservation, 41 nhóm kho reusable, 314 movement có phân rã đều không chênh lệch. 13 write-off có người duyệt độc lập; 234 FK/116 CHECK trusted. Còn 12 lô legacy/1152 đơn vị và 8 vị trí reusable/20 đơn vị chờ kiểm kê; không tự xác nhận dữ liệu thực.
- Probe forecast sau housekeeping tìm thấy lỗi thực decimal: 1/7×14 làm tròn lên 2.0001. Đã nhân tử số trước chia, thêm hai test hồi quy; build 0 warning/error, tests 122 PASS/3 SKIP. Lượt HTTP/UI đang chạy lại với start/end shift và checklist/complete qua browser; chưa công bố PASS.

### Kiểm chứng cập nhật 25/11/2026 11:51
- Invitation sau review đạt 15/15 HTTP/SMTP sink/browser: `invitation-reviewed-fifteen-pass.json`; first-login hai tab, token mobile, tab cũ nhận 401 về login. Không gửi email thật.
- SignalR sau sửa barrier harness đạt 15/15 HTTP/hub và 5/5 browser: `realtime-reviewed-fifteen-pass.json`, `realtime-reviewed-browser-five-pass.json`; kiểm trực tiếp ID không phát sau rollback/revoke. Fixture cleanup đạt. Chưa toàn chuỗi business UI trigger/SLA.
- Transfer/advisory đạt 12/12 (`transfer-advisory-twelve-pass.json`), UI mobile có nhãn số lượng, DEMAND/TRANSFER fallback có nhãn trung thực và không đổi ledger; dataset dương/incident UI còn thiếu.
- Đã bổ sung tìm kiếm/phân trang API lô/lịch sử và modal lịch sử lô. Không đổi schema; bỏ page giữ array cũ. Independent review không có finding chặn. Backend build 0 warning/error, 120 tests PASS/3 SKIP; frontend build PASS, hai PURE warnings SignalR.
- Runtime lượt đầu 12 PASS/1 FAIL ở browser vì mã lô TEST dùng lại từ các lượt cũ, đúng là có 8 kết quả tìm kiếm. Giữ `batch-browser-search-failure.json`; đã đổi mã lô fixture theo run prefix và đang chạy lại, chưa báo PASS phần này.
- Process TEST đã dọn sau mỗi lượt; không thay database vận hành/credential cũ/Git remote. Master vẫn chưa đủ 48 E2E/8 demos.

### Kiểm chứng cập nhật 25/11/2026 11:23
- Transfer mở rộng đạt 11/11: `transfer-eleven-pass.json`. UI bốn actor 3 yêu cầu/2 duyệt/2 gửi/1 nhận/1 chờ; mobile không tràn ngang. Đơn vị gói 2 và hai phiếu nhận tranh nhau kiểm thành công, không nhận vượt; lần nhận cuối giữ tổng 10 đơn vị cơ sở.
- Review lineage đã áp dụng cho tất cả lô nhận sau shipment thứ hai; kiểm originBatchId/expiry/receivedAt. RunId khác bị chặn Inspect/Cleanup/Stop, cleanup cuối đạt.
- Giữ các lỗi harness cũ (kỳ vọng retry 200, lô QUARANTINED, sai tên mã lỗi), không tính PASS. Không có lỗi backend mới trong probe này.
- Đang chạy invitation bằng binary hiện hành/57 bảng, SMTP sink localhost và browser first-login/setup/multi-tab. Chưa công bố kết quả trước khi hoàn tất.

### Kiểm chứng cập nhật 25/11/2026 11:15
- Consumable transfer đạt 8/8 HTTP/SQL: hai shipment FEFO, nhận một phần, concurrent duplicate chỉ một commit, chặn vượt/sai kho. Nhập 10 = nguồn 2 + đích 6 + đang chuyển 2 theo D12. `transfer-eight-pass.json`.
- Lượt đầu kỳ vọng retry 200 là lỗi harness; API có hợp đồng 409 cho RowVersion cũ và bảo vệ không ghi sổ hai lần. Giữ artifact thất bại; không đổi backend để chiều theo assertion sai.
- Review độc lập yêu cầu kiểm nguồn gốc/hạn dùng/receivedAt sau lần nhận thứ hai và cleanup khi Setup thất bại từng phần. Đã thêm runId vào fixture, xác minh quyền sở hữu trước Start/Inspect/Cleanup; đang chạy lại cùng browser bốn actor.
- Housekeeping 17 checks sau review đã xác minh cả ledger/chứng từ/history, UI HK bắt đầu/assessment và HKS PASS không note: `housekeeping-ui-ledger-seventeen-pass.json`.
- UI transfer lượt đầu bị chặn đúng vì lô nhập còn QUARANTINED; bổ sung bước xác minh READY bằng API Thủ kho. Chưa tính browser PASS trước khi lượt hoàn tất. Không chạm database vận hành.

### Kiểm chứng cập nhật 25/11/2026 10:52
- Housekeeping/refill tăng từ 10 lên 17 checks, toàn lượt PASS: `housekeeping-reservation-seventeen-pass.json`. Có chặn assignment thiếu vật tư và rollback không tạo việc/giữ chỗ; hai phòng cùng reserve và hủy một phòng chỉ giải phóng phần của phòng đó.
- Refill chọn lô hạn sớm hơn, idempotent cấp phát, thiếu tồn rollback, từ chối bắt buộc lý do; End Shift trả 16 về hai lô gốc, tồn cuối 34 = nhập 35 − tiêu hao thực 1. Reusable sở hữu vẫn 30.
- `refill-browser-results.json`: HK tạo → HKS duyệt → SK cấp đủ ba bước trên Edge, persisted balance/history được kiểm lại qua HTTP/SQL. E2E-27 đạt trong phạm vi này; chưa suy ra DEMO-02 gồm tiếp tục công việc hoặc login/SignalR đã đầy đủ.
- Mọi server TEST của lượt đã dừng/session đã thu hồi; đang tiếp tục coverage UI và cross-branch, không thay operational DB.

### Kiểm chứng cập nhật 25/11/2026 10:45
- Maintenance đạt 15/15 HTTP/SQL, gồm nội bộ và sửa ngoài; HKS FAIL trả lại sửa và reset checklist; PASS không note đóng incident/repair/cleaning, phòng Ready. Artifact `maintenance-fifteen-pass.json`.
- Browser Edge thật đạt chọn người liên quan, giữ reporter/discoverer riêng và nghiệm thu sửa ngoài; kiểm 390px không tràn ngang. Review độc lập không phát hiện lỗi trong dropdown/harness. Không suy ra toàn chuỗi UI/login/SignalR đã đạt.
- Build parent-checklist đạt 0 warning/error; tests 120 PASS/3 SKIP. Frontend incident-person build đạt, có hai PURE annotation warnings từ SignalR.
- Lượt đầu browser sai selector exact label đã giữ artifact; sửa bộ chọn theo accessible name và chạy lại đạt. Lượt queue key và build path sai là lỗi harness, không tính PASS.
- Đang mở rộng housekeeping/refill HKS→SK→FEFO, insufficient rollback và trả về lô gốc; chưa ghi PASS trước kết quả.

### Kiểm chứng cập nhật 25/11/2026 10:25
- Housekeeping sau review đạt lại 10/10: `housekeeping-reviewed-ten-pass.json`; kiểm đúng DIRTY trước inspection, reusable không thay đổi khi lặp start/end.
- Sửa gate điều tra nguyên nhân bằng RequireRolePermission HKS/IR_PROCESS hoặc MS/INCIDENT_MANAGE; build 0 warning/error và 120 tests PASS/3 SKIP. API maintenance đã qua cả hai loại xác minh, giữ người báo/người phát hiện riêng.
- Lượt tiếp theo phát hiện lỗi thật khi tạo sửa chữa có checklist: FK_work_checklist_items_work_template. Scalar-only EF không sắp UPDATE parent template trước INSERT snapshot. Đã thêm SaveChanges trước snapshot trong transaction hiện có; đang build và review độc lập. Không đổi mapping/generated entities/schema.
- Maintenance runner mở rộng kiểm rollback đầu vào sai, attach checklist sau, thiếu checklist, peer/OM/HK denied, kỹ thuật không tự nghiệm thu, HK FAIL quay lại sửa rồi PASS. Chưa công bố PASS trước khi chạy xong.

### Kiểm chứng cập nhật 25/11/2026 10:18
- D12 chênh lệch điều chuyển đã chốt; không chờ thêm phê duyệt. Đã ghi journal cục bộ, không publish.
- Housekeeping mở rộng 10/10 HTTP/SQL đạt; review phát hiện assertion dùng sai MAINTENANCE thay OUT_OF_SERVICE và thiếu so số dư reusable sau retry. Đã siết kiểm DIRTY thực tế và snapshot reusable; đang chạy lại. Worker quá hạn vẫn tắt trong fixture nên chưa chứng minh đường quá hạn.
- Review cleanup: thêm runId vào metadata server và kiểm Stop đúng lượt. Maintenance probe đã xác minh Stop từ runId khác không dừng server. Setup/cleanup vẫn giữ lịch sử TEST và thu hồi sessions.
- Maintenance probe mới: baseline riêng cho hai nhánh sửa nội bộ/sửa ngoài. Hai lỗi fixture thiếu cấu hình và lô cách ly đã lưu, sửa setup bằng API hợp lệ. Lượt ba tìm thấy lỗi thực: HK Supervisor điều tra nguyên nhân bị 403 vì service yêu cầu INCIDENT_MANAGE, trong khi role chỉ có IR_PROCESS/INCIDENT_VIEW. Đang review sửa gate theo quyền xử lý HK đã được duyệt; không cấp INCIDENT_MANAGE toàn cục.
- Database vận hành và Git remote giữ nguyên. Full 48 E2E/8 demo chưa hoàn tất.

### Kiểm chứng cập nhật 25/11/2026 09:58
- Core Housekeeping 7/7 HTTP/SQL PASS: cấp đầu ca 10 consumable + 4 reusable, reserve 4→assess thiếu 1→consume 1, required checklist, inspection PASS không note→Ready, end ca trả 9 đúng lô và 4 reusable giữ ownership. Artifact `housekeeping-seven-pass.json`; chưa browser/login hoặc tất cả alternative flows.
- Frontend sau sửa TS2322 build PASS; backend 120 PASS/3 SKIP. Lượt đang chạy mở rộng rework, idempotency và negative actor; không cộng trước kết quả.
- Các lỗi harness tên cột/rowVersion đã sửa theo mapping/contract và giữ artifact thất bại riêng. Cleanup bảo vệ fixture thuộc lượt khác theo review; tất cả lượt đã xong dọn session, giữ ledger và vô hiệu chi nhánh test.

### Kiểm chứng cập nhật 25/11/2026 09:50
- D12 đã chốt: thiếu điều chuyển giữ chờ đối soát; không tự ghi mất/hoàn kho. UI hiển thị requested/approved/shipped/received/pending theo chứng từ gốc.
- Inventory full 43/43 PASS (HTTP/SQL + OM duyệt ghi giảm qua browser); artifact `inventory-transfer-ui-43-pass.json`. Browser-only sau review PASS; không cộng lượt focused thành full suite.
- Review request/transfer: đã bỏ giới hạn 200 đề nghị/500 lịch sử tái sử dụng, thêm phân trang, refresh theo notification-change, chặn nút duyệt thay tài sản cho người đề xuất hoặc việc terminal. Replacement 16/16 service/SQL rollback PASS.
- Đã có đủ 20 tên tài liệu yêu cầu, nhưng nội dung register vẫn ghi PARTIAL, chưa hoàn tất 48 E2E/8 demo/UC-Report đối chiếu.
- Chuỗi HTTP Housekeeping mới tìm ra `NON_FILL_SUPPLY_CANNOT_AUTO_ISSUE` khi cấu hình reusable nhóm B đầu ca. Đã sửa API/UI và migration `v2-reusable-shift-configuration.sql` trên TEST: giữ 57 bảng, 116 CHECK, 38 trigger; không thay dữ liệu cũ.
- Backend reusable-shift build 0 warnings/0 errors; test 120 PASS/3 SKIP. Frontend lượt đầu TS2322 đã sửa, đang kiểm chứng lại. Harness replacement có NU1900 do feed vulnerability không truy cập được.
- Đang chạy `v2-housekeeping-runtime.mjs` với fixture chi nhánh/user/catalog riêng; chưa có kết quả cuối. Lượt trước dùng binary cũ đã được giữ `housekeeping-old-build-failure.json`; session/branch được dọn. Harness sửa quyền sở hữu cleanup theo review, không cleanup fixture khác nếu Setup bị từ chối.
- Database chính, credential người dùng cũ và Git remote không đổi. Không đánh dấu phase hoàn tất từ build hoặc service test riêng lẻ.

### Kiểm chứng cập nhật 25/11/2026 03:42
- Database chính không đổi; delta chỉ trên TEST, 57 bảng. Không commit/push/deploy; chưa đánh dấu toàn bộ phase hoàn tất.
- Backend build gần nhất: 0 warnings/0 errors. Unit tests lần trước thay đổi expiry: 120 passed/3 skipped; frontend compact: 5 passed. Build frontend passed trước chỉnh nhãn pending cuối cùng.
- D10: Thủ kho đề nghị, OM cùng chi nhánh duyệt; mixed adjustment không bypass. Kế hoạch lô được kiểm lại trước duyệt, không nhận partial approval; post chỉ giảm đúng lô. Focused HTTP/SQL 4 PASS trước bổ sung kiểm snapshot stale.
- Inventory full run: 35 PASS. Ba ca mới từng đặt nhầm sau return trong harness đã được sửa; không cộng các lượt focused thành full suite. Cần full rerun sau expiry warnings.
- SignalR optimized: lượt 1 đạt 8/15 rồi một assertion fail; lượt 2 đạt 15/15. Giữ cả hai bằng chứng. Recovery-only 5/5. Chưa browser badge/toast; không tuyên bố SLA.
- Invitation SMTP sink: 15 PASS. Assessment 11 PASS và replacement 15 PASS là service/SQL, không phải HTTP/UI E2E. Harness có NU1900 do feed không truy cập được.
- D11: Owner chọn hồ sơ chờ kiểm kê. Đã tạo/đối soát 200 hồ sơ ấm đun inactive, 200 audit mappings, vị trí/serial chưa xác minh để null; 99 stock movements và số dư 103 giữ nguyên. Chưa phải chuyển đổi tồn hoàn tất.
- Đã viết 10 tài liệu thiết kế; còn impact/API/UI/UC/schema/reconciliation/E2E/final report và acceptance 48 ca/8 demo. Kế hoạch chưa hoàn tất.
- Các server kiểm thử inventory/realtime đã dọn; giữ nguyên tiến trình người dùng. Chỉ build đang chạy thuộc phiên hiện tại.

### Ghi nhận ban đầu (lịch sử)
Phases 1/3/4/6/7 đang triển khai. Baseline build passed, tests 101 passed/3 skipped, frontend 9 passed. SQL audit thành công ngoài sandbox; đã backup CHECKSUM, VERIFYONLY, restore database TEST riêng HotelOpsCompactWorkdayTestV22611250010. TEST đã phục hồi 10 nhân viên bảo trì theo audit, thêm assessment và bảng invitation. Database chính chưa đổi.

- Assessment/reservation thực tế, checklist thực thi, giao kỹ thuật nội bộ/dịch vụ ngoài và lịch sử nguyên nhân: implementation cần runtime verification.
- SignalR: 10 kiểm tra runtime đã passed; reconnect/session revocation/UI chưa hoàn tất.
- Invitation token/outbox SMTP: implementation backend, chưa test delivery; đang bổ sung UI.
- Owner xác nhận khăn 213/218 tái sử dụng và ấm 222 tài sản. Foundation batch/reusable, chuyển loại có đối soát, transfer discrepancy và AI còn chưa hoàn tất.
- Các phase chưa đánh dấu hoàn thành vì chưa đủ acceptance evidence. Không báo đầy đủ 40 CHG/48 E2E đã xong.
