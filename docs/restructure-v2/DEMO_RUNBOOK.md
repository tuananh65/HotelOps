# Tám demo nghiệp vụ Master V2

Cập nhật 25/11/2026 19:19 (Asia/Saigon). Nguồn: [Master §37–38](OWNER_REQUEST_PART_4.md), [quyết định Owner](DECISION_AND_BLOCKER_REGISTER.md), [E2E evidence index](E2E_TEST_RESULTS.md). Runbook là hướng dẫn tái hiện và mức bằng chứng hiện có, không phải biên bản nghiệm thu tám demo.

## Phạm vi và cách chạy

Master yêu cầu API và dữ liệu thật trên TEST được phép. Session TEST được fixture cấp không làm API thành mock. Không bắt buộc password-login hoặc SignalR trong mọi demo; DEMO-07 kiểm login, DEMO-08 phải có thông báo từ thao tác nghiệp vụ thật. §37 vẫn yêu cầu hồ sơ test có API/DB/UI evidence. Không gộp số assertion thành số E2E.

- Chỉ dùng `HotelOpsCompactWorkdayTestV22611250010`. Không chạy trên database vận hành, không reset hay sửa mật khẩu tài khoản cũ.
- Fixture tạo dữ liệu tổng hợp, lưu ledger/audit, thu hồi session và ngừng hoạt động dữ liệu fixture khi cleanup. Không công bố token trong thư mục `*-private`.
- Windows có .NET 10, Node, SQL Server 2022, quyền SQL Windows authentication và Edge. Dependency đã cài theo repository. Các runner cùng dùng fixture/API 5779 nên chạy tuần tự; invitation dùng 5778/SMTP sink 2526. Không dừng tiến trình của người khác để lấy cổng.
- Chọn binary đã build thành công hiện tại; ví dụ dưới dùng thư mục build đã sửa thu hồi phiên khi đổi quyền. Sau thay đổi source phải build lại, không dùng bản cũ để kết luận PASS.

Tại repository root, chuẩn bị binary:

```powershell
dotnet build HotelOps.slnx --artifacts-path .artifacts/role-session-fix
$env:HOTELOPS_V2_TEST_BUILD = (Resolve-Path .artifacts/role-session-fix/bin/HotelOps.Api/debug).Path
```

Frontend trong terminal riêng nếu chưa có Vite phù hợp:

```powershell
npm --prefix frontend run dev -- --port 5174 --strictPort
```

Ngay trước **mỗi** runner có mutation, tạo backup TEST; fixture kiểm độ mới (15 phút), CHECKSUM và VERIFYONLY. Nếu lệnh thất bại, chưa chạy runner:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/database/compact-v2/Backup-Workday.ps1 -Database HotelOpsCompactWorkdayTestV22611250010
```

Runner tự tạo/khởi động/cleanup fixture. Kiểm exit code, tất cả `tests[].status`, `cleanup` và log; giữ riêng artifact thất bại trước chạy lại vì tên `*-runtime-results.json` có thể bị ghi đè. Không tuyên bố PASS khi runner đang chạy hoặc còn FAIL. Lệnh dưới là lệnh tái hiện, không có nghĩa lượt mới đã được thực thi trong phiên chỉnh tài liệu này.

## DEMO-01 — Dọn phòng bình thường

**Chuỗi Master:** Start Shift → Cấp đồ → Phân công → Nhận phiếu → Kiểm tra vật tư trong phòng → Bổ sung đúng phần thiếu → Checklist → Inspection PASS → Ready.

**Actor/dữ liệu:** OM cấu hình, SK có lô READY, HKS phân công, HK bắt đầu ca; phòng fixture định mức 4, còn dùng 3, thiếu 1. API/branch/room IDs do fixture tra cứu, không dùng phòng khách đang vận hành.

```powershell
node scripts/verification/v2-housekeeping-runtime.mjs
```

**Quan sát:** cấp 10 tiêu hao; assignment reserve 4; HK nhận/start/assessment còn 3 giải phóng 3; checklist bắt buộc; completion tiêu hao đúng 1; HKS PASS không note đưa Ready. Đối chiếu allocation, reservation, ledger, history trước/sau retry.

**Evidence:** [25 checks + UI/rework/refill/rejection](../../artifacts/restructure-v2/housekeeping-rejection-twentyfive-pass.json), [20 checks + expiry/settlement](../../artifacts/restructure-v2/housekeeping-expiry-settlement-twenty-pass.json); script [cleaning browser](../../scripts/verification/v2-cleaning-browser.mjs). E2E-08–14. Chuỗi đã có API thật/DB assertions và UI liên quan; không thiếu password-login/SignalR cho acceptance demo này.

## DEMO-02 — Thiếu đồ tiêu hao

**Chuỗi Master:** HK Staff tạo yêu cầu → HK Supervisor xác nhận → Storekeeper xuất theo FEFO → Staff nhận đồ → Tiếp tục công việc.

**Actor/dữ liệu:** HK đang ca với allocation; hai lô, lô nhận sau hết hạn sớm hơn; HKS xác nhận refill, SK cấp.

```powershell
$env:HOTELOPS_REFILL_CONTINUATION_PROBE = '1'
$env:HOTELOPS_REFILL_REJECTION_BROWSER = '1'
$env:HOTELOPS_REWORK_POSITIVE_PROBE = '1'
node scripts/verification/v2-housekeeping-runtime.mjs
Remove-Item Env:HOTELOPS_REFILL_CONTINUATION_PROBE, Env:HOTELOPS_REFILL_REJECTION_BROWSER, Env:HOTELOPS_REWORK_POSITIVE_PROBE
```

**Quan sát:** HK tạo trên UI, HKS duyệt, SK cấp; kiểm lô FEFO, held tăng đúng, history giữ người xử lý, exact retry không cấp đôi. Sau đó HK dùng phần bổ sung để hoàn thành công việc được giao, kiểm used/reserved/held và room state.

**Evidence:** [25 checks toàn lượt](../../artifacts/restructure-v2/housekeeping-rejection-twentyfive-pass.json), [refill UI](../../artifacts/restructure-v2/refill-browser-results.json). E2E-18/27/28. Bật `HOTELOPS_REFILL_CONTINUATION_PROBE=1` để tái hiện phần tiếp tục: nhân viên có10, không được giao phòng cần12; HK tạo/HKS duyệt/SK cấp2 qua browser, sau đó nhận việc và bổ sung thực12, checklist/inspection PASS. Đối soát35 nhập =22 kho+13 đã dùng; retry không ghi đôi. Không tự thêm một transition xác nhận nhận hàng ngoài nghiệp vụ hiện hành.

## DEMO-03 — Thay chăn và khăn

**Chuỗi Master:** Cấp đồ sạch → Thu hồi đồ cũ → Chuyển Laundry → Hoàn thành giặt → Ready Inventory. Tổng sở hữu không đổi.

**Actor/dữ liệu:** SK và vị trí phòng/kho cùng chi nhánh; REUSABLE TEST tương đương đồ vải, tổng sở hữu 10, chuyển 4 mỗi bước. Không phải xác nhận tồn khăn/chăn thực tế.

```powershell
$env:HOTELOPS_UI_PROBE = '1'
node scripts/verification/v2-inventory-runtime.mjs
Remove-Item Env:HOTELOPS_UI_PROBE
```

**Quan sát:** UI READY→IN_USE tại phòng→DIRTY→LAUNDRY→READY; mỗi bước nguồn giảm/đích tăng 4, tổng sở hữu vẫn 10. History/custody phải giữ nguồn, retry không di chuyển thêm. Phiếu nguồn/approval một số ca inventory là setup fixture, không chứng minh luồng phê duyệt đó.

**Evidence:** [43 checks với reusable UI](../../artifacts/restructure-v2/inventory-fifo-expiry-damage-ui-43-pass.json), [browser helper](../../scripts/verification/v2-reusable-browser.mjs). E2E-21–23. Chu kỳ trạng thái API/UI và sở hữu đã đạt trên vật tư TEST đại diện; chưa nghiệm thu dữ liệu vật lý thật.

## DEMO-04 — Sửa chữa thiết bị

**Chuỗi Master:** Báo hỏng → HK Supervisor kiểm tra → Maintenance Supervisor phân công → Maintenance Staff sửa → Nghiệm thu kỹ thuật → HK Supervisor kiểm tra lại → Ready nếu đủ điều kiện.

**Actor/dữ liệu:** HK được giao phòng, HKS, MS, Maintenance Staff riêng biệt; asset và checklist fixture. Người báo và người liên quan được giữ riêng.

```powershell
node scripts/verification/v2-maintenance-runtime.mjs
```

**Quan sát:** HK báo hỏng quay lại việc dọn; HKS xác nhận; MS giao staff nội bộ; staff nhận/checklist/start/complete; MS nghiệm thu; HKS PASS không note đóng incident/repair/cleaning và Ready. Nhánh FAIL có lý do ngắn trả đúng việc, reset checklist, lưu cả hai vòng; staff không tự nghiệm thu.

**Evidence:** [17 checks report/UI](../../artifacts/restructure-v2/maintenance-report-ui-seventeen-pass.json), [workflow browser](../../scripts/verification/v2-maintenance-workflow-browser.mjs). E2E-29/31–34/36. Chuỗi trước đã đạt API/UI. [Quantity/blockers12](../../artifacts/restructure-v2/quantity-blockers-twelve-pass.json) đã đạt nhiều sự cố và occupied/SAFETY hold: PASS một sự cố không mở Ready khi còn blocker, PASS cuối hoàn tất cleaning. Nhánh legacy SOURCE đã đạt trong lượt7 PASS/1 FAIL tại snapshot quantity phía sau; không gán toàn lượt FAIL thành PASS.

## DEMO-05 — Không thể sửa, đề xuất thay mới

**Chuỗi Master:** Báo hỏng → Kiểm tra kỹ thuật → Không thể sửa → Đề xuất thay mới → OM xử lý theo quyền → Thay thực tế → Kiểm tra lại.

**Actor/dữ liệu:** cùng actor DEMO-04 và OM cùng chi nhánh; tài sản hỏng/tài sản thay có vị trí hợp lệ, không giả tạo tồn vật tư.

```powershell
$env:HOTELOPS_REPLACEMENT_PROBE = '1'
node scripts/verification/v2-maintenance-runtime.mjs
Remove-Item Env:HOTELOPS_REPLACEMENT_PROBE
```

**Quan sát:** staff đề xuất sau kiểm tra, MS xác nhận, OM từ chối có lý do hoặc duyệt; pending chặn complete; chọn asset sai vị trí bị chặn; install thật di chuyển hai tài sản, giữ stock, reset checklist; kỹ thuật nghiệm thu rồi HKS kiểm tra.

**Evidence:** [16 service/SQL checks](../../artifacts/restructure-v2/replacement-runtime-results.json), [HTTP probe](../../scripts/verification/v2-replacement-http-probe.mjs), [workflow browser](../../scripts/verification/v2-maintenance-workflow-browser.mjs). E2E-35. **Đã kiểm chứng:** [Lượt HTTP tích hợp](../../artifacts/restructure-v2/incident-allocation-snapshot-failure.json): đề xuất → MS xác nhận → OM từ chối/duyệt → thay thực tế → checklist mới → nghiệm thu kỹ thuật → HKS PASS/Ready đạt. Toàn runner vẫn24 PASS/1 FAIL tại fixture legacy phía sau; không gọi toàn suite PASS. Ảnh replacement bị lượt sau ghi đè, không gán là ảnh nguyên bản.

## DEMO-06 — Điều chuyển giữa hai chi nhánh

**Chuỗi Master:** Kho thiếu → Yêu cầu điều chuyển → OM nguồn xác nhận → Xuất hàng → Kho đích nhập thực nhận → Xử lý chênh lệch → Hoàn thành.

**Actor/dữ liệu:** OM nguồn/đích, SK nguồn/đích; hai branch TEST tương ứng hướng điều chuyển, lô có expiry và đơn vị quy đổi. Giữ vai trò nguồn/đích trong mọi bước, không hardcode IDs DVBV/DVCT.

```powershell
node scripts/verification/v2-transfer-runtime.mjs
```

**Quan sát:** OM đích yêu cầu, OM nguồn duyệt; SK nguồn gửi FEFO, SK đích nhập lượng thực nhận; requested/approved/shipped giữ nguyên, không sửa phiếu gốc để che thiếu. D12: nếu còn thiếu, kết quả đúng là **chờ đối soát**, không tự ghi mất/hoàn kho. Chỉ kết thúc đủ hàng khi tổng thực nhận đủ và không còn chênh lệch cần xử lý.

**Evidence:** [11 transfer checks](../../artifacts/restructure-v2/transfer-eleven-pass.json), [14 advisory/transfer checks](../../artifacts/restructure-v2/transfer-advisory-fourteen-pass.json), [reusable nhận 2 rồi 1](../../artifacts/restructure-v2/inventory-reusable-ui-fortythree-pass.json). E2E-38–43. API thực đã kiểm nhận đủ phần còn lại và UI bốn actor đã kiểm thiếu chờ đối soát. **Đã nối chuỗi trên TEST:** [Transfer15](../../artifacts/restructure-v2/transfer-business-realtime-fifteen-pass.json) tạo phiếu từ đề xuất thiếu thực của fixture, gửi2 rồi nhận1+1; [UI cùng phiếu](../../artifacts/restructure-v2/transfer-completion-browser-results.json) hiển thị2/2/2/2/0 và hoàn thành trên desktop/mobile. Nhánh thiếu D12 vẫn chờ đối soát. UI từ chối của OM nguồn đang kiểm riêng, chưa nâng PASS tại checkpoint này.

## DEMO-07 — Tạo tài khoản

**Chuỗi Master:** Administrator tạo User → Email Invitation → First Login → Đổi mật khẩu bắt buộc → Truy cập workspace.

**Actor/dữ liệu:** admin và tài khoản TEST mới có email vào local sink; Username, không EmployeeCode. Không gửi đến nhân viên thật.

```powershell
$env:HOTELOPS_ROLE_REVOCATION_PROBE = '1'
node scripts/verification/v2-invitation-runtime.mjs
Remove-Item Env:HOTELOPS_ROLE_REVOCATION_PROBE
```

**Quan sát:** admin tạo/resend trên UI; chỉ một user/outbox hợp lệ; sink nhận link; first login buộc đổi, API nghiệp vụ bị chặn trước đổi; setup token một lần; phiên cũ bị thu hồi; login mới vào workspace. Cleanup tài khoản TEST inactive, giữ audit.

**Evidence:** [20 invitation/recovery/role-session checks](../../artifacts/restructure-v2/invitation-role-session-twenty-pass.json), [admin create](../../artifacts/restructure-v2/invitation-admin-create.json), [browser](../../artifacts/restructure-v2/invitation-browser-results.json). E2E-04–07. Chuỗi với SMTP sink đã đạt theo D07; [đổi quyền](../../artifacts/restructure-v2/role-change-verified-pass.json) cũng thu hồi hai phiên, rejected mutation giữ phiên. Delivery tới email thật **BLOCKED_EXTERNAL_CONFIGURATION**; không phải lý do chặn demo local đã được phép.

## DEMO-08 — AI và SignalR

**Chuỗi Master:** AI đề xuất bổ sung/điều chuyển → OM xem xét → Tạo phiếu thật → SignalR thông báo cho đúng người xử lý → Theo dõi trạng thái realtime.

**Actor/dữ liệu:** OM xem advice, bốn OM/SK nguồn/đích đều là recipient hợp lệ. Probe hub độc lập kiểm actor ngoài scope; không gọi một trong bốn recipient điều chuyển là outsider. Fallback phải ghi rõ `STATISTICAL_RULES`, không đổi tên thành AI provider runtime.

Chuỗi tích hợp và probe hub riêng, chạy tuần tự với backup riêng như trên:

```powershell
$env:HOTELOPS_BUSINESS_REALTIME_PROBE = '1'
node scripts/verification/v2-transfer-runtime.mjs
Remove-Item Env:HOTELOPS_BUSINESS_REALTIME_PROBE
node scripts/verification/v2-realtime-runtime.mjs
```

**Evidence:** [advice 14 checks](../../artifacts/restructure-v2/transfer-advisory-fourteen-pass.json), [hub 15 checks](../../artifacts/restructure-v2/realtime-reviewed-fifteen-pass.json), [browser realtime 5 checks](../../artifacts/restructure-v2/realtime-reviewed-browser-five-pass.json). E2E-44–46.

**Đã kiểm chứng:** [15/15 tích hợp](../../artifacts/restructure-v2/transfer-business-realtime-fifteen-pass.json), [bốn Edge contexts](../../artifacts/restructure-v2/business-realtime-verified-pass.json): đề xuất fallback → phiếu HTTP thật → thông báo sau commit → badge/popup/list tự cập nhật. Replay không tạo trùng, read ownership đúng, positive control cùng stream. Cleanup sạch; external AI chưa cấu hình. Chạy với `HOTELOPS_BUSINESS_REALTIME_PROBE=1`. External AI vẫn **BLOCKED_EXTERNAL_CONFIGURATION**.

## Hồ sơ bàn giao demo

Mỗi lượt lưu run time, build/source, TEST DB, branch/actor đã khử bí mật, dữ liệu trước/sau, steps, expected/actual, API status, DB/ledger assertions, screenshot và cleanup. E2E cần đủ 11 trường §37; runbook chỉ dẫn nguồn, không tự lấp evidence chưa có. Giữ log thất bại và giới hạn phiên bản. Không cộng service checks, browser checks và assertion count thành 48 E2E PASS.

## Điều kiện ngoài phạm vi demo TEST

[Recovered roster6/6](../../artifacts/restructure-v2/recovered-roster-runtime-results.json) xác minh10 Maintenance Staff phục hồi đúng chi nhánh và hiển thị trên UI; OM hoạt động,không DH hoạt động. Credential10/10 khớp nguồn theo audit; không kiểm login bằng mật khẩu của người phục hồi. Database local chưa migration/cutover; không xem demo TEST là đã triển khai local/Linux/EC2. External AI/SMTP thật và kiểm kê vật lý vẫn giữ trạng thái đã nêu,không tạo dữ liệu giả để đóng nghiệm thu.
