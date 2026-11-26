# Note 25_11 — Test report

Cập nhật 26/11/2026. Không cộng các lần chạy lại vào số test. Những kết quả dưới đây đã thu exit code, sau các sửa review nghiệp vụ.

| Kiểm tra | Kết quả | Phạm vi và giới hạn |
|---|---|---|
| Backend Release | PASS, 0 warning, 0 error | `dotnet build HotelOps.slnx -c Release --no-restore -m:1 -p:UseSharedCompilation=false` |
| .NET tests | 197 PASS, 11 SKIP, 0 FAIL | `dotnet test HotelOps.slnx -c Release --no-build --no-restore -m:1`; 6 Note2511 SQL fixtures chạy riêng bên dưới, 5 fixtures môi trường khác chưa chạy |
| Frontend build | PASS | `npm run build`; tsc + Vite. Rollup bỏ hai PURE annotation trong SignalR, không lỗi build |
| Typecheck | PASS | `npm run typecheck`, exit 0 |
| Compact frontend | 11 PASS, 0 FAIL | `npm run test:compact:ci`; test Lead verify cũ được đổi theo nghiệp vụ đã chốt |
| SQL integration | 6 PASS, 0 FAIL, 0 SKIP | `scripts/verification/run-note-25-11-database-tests.ps1`; serial collection, transaction rollback |
| HTTP thật + đối chiếu SQL | 14 PASS | Anonymous401; OM OPERATIONAL/report403; store foreign stock/catalog403; OM catalog hẹp200/fullstock khác403; personnel loại management đúng SQL; workload bằng distinct work; own command-access/unread/inbox200 |
| Browser React components | 8 PASS | Toast dedup/5s/manual, confirmation tồn tại, assessment draft/error/save/reload, attendance3kỳ, calendar7ngày, shift lock/unlock. API mock, không gọi là E2E |
| Browser layout | 5 PASS | Workload thanh ngang đúng số lượng trạng thái; asset tree đóng/cảnh báo/unplaced; stocktake chọn nhiều/search/payload; Owner aggregate và KPI64–80px tại320/1440 |
| Browser shell + API thật | 17 PASS, 5 NOT_RUN | Bảy role navigation/calendar/mobile; workload đúng API; personnel liên hệ; asset tree; OM kho khác/reusable; checklist HK Supervisor được phép/OM bị từ chối; store low-stock đúng available. Năm role không có unread data để kiểm màu badge, không tính là PASS |
| SQL patch | PASS | Clone và DB development, mỗi lần apply có backup CHECKSUM + RESTORE VERIFYONLY;57bảng, legacy pending refill=0 |
| Frontend toàn bộ legacy | FAILED / INTERRUPTED | `npm test` có timeout khoảng30s trong auth/router/activation suites; dừng sau khi ghi nhận. Fixture có expiry cố định2026-11-07, chỉ là nguyên nhân nghi vấn. Source auth/router không thay đổi trong nhiệm vụ; chưa chứng minh toàn bộ suite này đạt |

## SQL scenarios

1. Sáu role: chưa vào ca, đang ca, kết thúc, sai chi nhánh; quyền phải thuộc role của ca, không mượn role khác.
2. Receipt draft/metadata/line, stale version bị từ chối, post/replay không cộng trùng; stocktake nhiều dòng snapshot từ ledger, nhập số đếm không thay đổi tồn.
3. HK refill → OM duyệt → Store cấp; retry không cấp trùng.
4. Staff sửa thành công → HK kiểm tra đúng người nhận; phòng không tự READY; lịch sử Staff vẫn đọc được.
5. FAIL giữ hỏng → Lead đề nghị → OM duyệt → lệnh mới → phân công/nhận/bắt đầu/lắp/hoàn thành PASS; giữ lần FAIL cũ.
6. Source OM duyệt điều chuyển, kho nguồn gửi4, kho đích nhận2, còn2 đang điều chuyển; sai chi nhánh403, retry409, không thêm ledger.

## Cô lập và runtime

SQL fixtures dùng `HotelOpsCompactWorkdayTestNote2511260145`, rollback. HTTP runner tạo session tạm, xóa và dừng API5092 trong finally. Không tạo testdata hoặc reset HotelOpsCompactDB. Development chạy bản mới ở5173/API5081 với DB `HotelOpsCompactWorkdayTestLocalDevV2261125`. Production5080 giữ bản cũ và DB chính thức; cả hai healthcheck57bảng. Không ghi credentials vào báo cáo.

## Lỗi đã xử lý trong vòng kiểm chứng

- Fixture AsyncLocal mất identity qua async factory: fixed accessor chỉ trong test.
- Maintenance fixture thiếu checklist: tạo snapshot/template trong transaction và rollback.
- Notification dedup: assert đúng recipient/root, không yêu cầu tạo thông báo trùng.
- SQL deadlock do chạy fixtures song song: collection tuần tự; DLL bị testhost khóa ở một lượt build: đã đóng testhost và chạy thành công.
- Review sửa quyền lịch sử bảo trì, incident còn đòi Lead CLOSED, OM report/tasks và role-permission của ca; regression cuối đã chạy.
- Screenshot sidebar mobile chụp giữa animation: đợi vị trí thực đạt0 rồi chụp lại.
- Browser thử checklist sai role/selector: đổi sang kiểm quyền thực, OM bị từ chối và HK Supervisor được phép; không nới quyền ứng dụng để làm test đạt.

## Giới hạn

Không có ảnh Word gốc để đối chiếu pixel. Chưa mô phỏng reconnect SignalR khi mất mạng, mọi tổ hợp role×viewport×dữ liệu, hoặc mọi HTTP write đúng thời điểm ca đổi. UI guard hỗ trợ thao tác, backend vẫn là nguồn kiểm quyền. AI/SMTP external không thuộc bằng chứng này; không gửi email thật. Bộ legacy frontend chưa đạt, không tuyên bố toàn bộ repository xanh.
