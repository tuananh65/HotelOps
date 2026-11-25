# Báo cáo triển khai và kiểm chứng V2

Cập nhật 25/11/2026. Nhánh feature/project2-role-alignment-20261123, baseline d109839d. Phạm vi thực thi theo D08 là database TEST; chưa commit/push/deploy hoặc nâng cấp database vận hành. Kết quả mới nhất và giới hạn nằm tại [trạng thái](STATUS_20261125.md), [48 hồ sơ](E2E_CASE_EVIDENCE.md) và [tám kịch bản demo](DEMO_RUNBOOK.md).

## Chức năng đã triển khai và kiểm chứng

- Login chỉ dùng tên đăng nhập; toggle mật khẩu, icon/placeholder và popup quên mật khẩu gọn. Khôi phục qua token một lần, SMTP sink và thu hồi phiên cũ; layout bốn viewport.
- Tự kết thúc ca theo deadline; giữ custody/reservation để đối soát, hoàn vật tư rồi mới nhận ca tiếp. Dọn lại không tự chuyển phòng sang bảo trì và không trừ vật tư hai lần.
- Assessment xác định thiếu thực tế, reservation nhiều phòng, chặn phân công thiếu vật tư, refill rồi tiếp tục công việc. FIFO/FEFO, hết hạn, trạng thái reusable, giặt là, ghi giảm và cảnh báo tồn thấp đã kiểm API/SQL/Edge.
- Giữ đội bảo trì: TEST phục hồi10 Maintenance Staff theo provenance, credentials không đổi. MS giao việc, staff thực hiện checklist, MS nghiệm thu kỹ thuật và HKS kiểm phòng. Thay tài sản, nguyên nhân/người liên quan, nhiều blocker và khôi phục luồng sự cố cũ có evidence riêng.
- Cấu hình yêu cầu theo role; tạo loại báo cáo phòng; bỏ nút phạm vi quyền, menu/trang xem vật tư nhân viên và tiêu đề lặp. Báo cáo OM có sự kiện ca/sự cố/kho/tài sản, số điện thoại trong danh sách nhân sự.
- Điều chuyển có lượng đề nghị/duyệt/gửi/thực nhận; thiếu giữ chờ đối soát. Từ chối bắt buộc lý do và lưu người xử lý/lịch sử. Không tự ghi mất hoặc hoàn kho.
- SignalR từ phiếu nghiệp vụ thật đã chạy trên bốn browser. AI fallback có nhãn, chỉ đề xuất và không tự ghi dữ liệu.
- Sửa lỗi đã tái hiện: admin thay role nhưng phiên cũ còn hiệu lực. UserService thu hồi phiên và đổi quyền trong cùng transaction; lỗi duplicate/stale/FK rollback không đăng xuất người dùng.

## Kết quả thực chạy

| Nhóm | Kết quả | Artifact trong artifacts/restructure-v2 |
|---|---|---|
| Backend build | PASS,0 lỗi,4 NU1900 | role-session-build-test-summary.json |
| Automated tests |155 PASS,0 FAIL,3 SKIP | role-session-build-test-summary.json |
| Frontend build |PASS;2 cảnh báo annotation PURE của SignalR |incident-requeue-frontend-build.log |
| Tài khoản/reset/đổi role |20/20 HTTP/SMTP sink/Edge |invitation-role-session-twenty-pass.json |
| Layout login/recovery |4/4 viewport |login-layout-browser.json |
| Housekeeping/assignment/scope |26/26 HTTP/SQL/Edge |housekeeping-assignment-scope-twentysix-pass.json |
| Kho/FIFO/expiry/DAMAGED/reusable |43/43 HTTP/SQL/Edge |inventory-fifo-expiry-damage-ui-43-pass.json |
| Điều chuyển/từ chối |15/15 HTTP/SQL/Edge |transfer-rejection-fifteen-pass.json |
| Điều chuyển/business realtime |15/15,4 browser contexts |transfer-business-realtime-fifteen-pass.json |
| Tồn thấp |10/10 HTTP/SQL/Edge |low-stock-ui-ten-pass.json |
| Số lượng/nhiều blocker |12/12 HTTP/SQL/Edge |quantity-blockers-twelve-pass.json |
| Cấu hình/báo cáo |15/15; phone/resource focused PASS |configuration-report-reviewed-fifteen-pass.json;phone-resource-focused-pass.json |
| Bảo trì |17/17 HTTP/SQL/Edge |maintenance-report-ui-seventeen-pass.json |
| Phục hồi đội bảo trì |6/6 API/Edge |recovered-roster-runtime-results.json |

Không cộng các nhóm trên thành số ca E2E. Replacement full chain đạt trong lượt24 PASS/1 FAIL do fixture legacy phía sau; legacy SOURCE đạt trong lượt7 PASS/1 FAIL do snapshot quantity phía sau. Giữ nguyên artifact lỗi và giới hạn ở hồ sơ E2E, không gọi toàn runner PASS. Ba tests SKIP chưa được tính đạt; NuGet vulnerability feed không truy cập được, không xác nhận quét vulnerability thành công.

## Điều kiện còn lại ngoài kiểm chứng TEST

- E2E47/48 đã có evidence cuối:9 role đại diện, replenishment7 kiểm tra, global11 và14 nhóm đối soát đều không lệch. Xem index để biết phạm vi, không suy mọi endpoint hoặc toàn bộ điều kiện bên ngoài PASS từ build.
- Database vận hành vẫn49 bảng, TEST V2 có57 bảng dù cùng property3.0.0. [Gói rollout](LOCAL_ROLLOUT_READINESS.md) có11 scripts/hash và preflight chỉ đọc; chưa có rehearsal/cutover local hoặc restore Linux đã kiểm chứng. Không chuyển dữ liệu synthetic TEST vào vận hành.
-200 hồ sơ ấm đun chờ kiểm kê; giữ103 trên sổ kho và97 lượt cấp phòng. Legacy batches/reusable chờ đối soát theo quyết định Owner; không tự tạo tồn, hạn dùng hoặc tình trạng vật lý.
- Incident653/phòng104 local vẫn cần người có quyền xác minh/xử lý. Đường khôi phục đã kiểm trên TEST; không tự đóng sự cố thật.
- External AI và SMTP thật: BLOCKED_EXTERNAL_CONFIGURATION theo D07. SMTP sink và fallback không được gọi là external runtime PASS.
- [UC mapping](UC_CHANGE_MAPPING.md) và [Report3/4/5 alignment](REPORT_3_4_5_ALIGNMENT.md) ghi nguồn và phần thiếu. Chưa có mẫu chính thức Report4/5 trong workspace; không giả mạo nghiệm thu Word/diagram.

## Files và cấu hình

Danh sách implementation theo trách nhiệm: [API](API_CHANGE_REGISTER.md), [frontend](FRONTEND_CHANGE_REGISTER.md), [schema](DATABASE_SCHEMA_DELTA.md). Lượt chốt này sửa UserService.cs và bổ sung scripts/verification cùng evidence; working tree còn nhiều thay đổi có trước, không quy toàn bộ diff thành thay đổi mới.

AI: AI__Provider,AI__Model,AI__ApiKey,AI__Enabled. Email: Email__SmtpHost,Email__SmtpPort,Email__Username,Email__Password,Email__FromAddress,Email__Enabled,Email__PublicBaseUrl,Email__UseTls,Email__AllowLiveDelivery. Lưu secret trong .NET User Secrets/secret store; xem [thiết kế AI](AI_INTEGRATION_DESIGN.md) và [tài khoản/email](ACCOUNT_INVITATION_SECURITY.md) để kích hoạt. Không commit giá trị hoặc gửi email thật khi chưa được phép.
