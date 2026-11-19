# Khắc phục ca làm việc và đề nghị điều chỉnh giờ — 20/11/2026

Đã triển khai gói được người dùng duyệt, cài dữ liệu mẫu và kiểm chứng bằng SQL/API/trình duyệt local của `lan.dang`.

## Kết quả

- Sửa `CK_rework_workflow_v1` để chấp nhận cặp `ON_HOLD / IN_PROGRESS` mà source hiện sử dụng. Constraint đang bật và trusted.
- Tạm giữ công việc cũ `CLN-20261016-002` và phiếu làm lại số 8. Giữ trạng thái vật lý, giờ bắt đầu và nội dung cũ; không ghi hoàn tất. Có audit trước/sau.
- Ca số 8 của Lan vẫn WORKING, chưa có giờ kết thúc. Đã chạy các guard task/cleaning/rework và `EndWorkdayAsync` thành công trong transaction thử, rồi rollback; xác nhận lại ca, rowversion và audit không đổi. Không gọi HTTP End để đóng ca thật.
- Thêm bốn công việc và bốn khoảng thời gian có nhãn **[DEMO]**, hai ngày làm việc mẫu đã kết thúc. Không ghi kết quả phòng thật. Dữ liệu này nằm trong database local nên được tính vào các tổng tương ứng của Lan.
- Nút **Tạo đề nghị điều chỉnh** mở form trực tiếp trên cả Nhật ký thời gian và Đề nghị điều chỉnh. Chọn ngày, chọn/gõ tìm khoảng đã kết thúc, nhập giờ và lý do trong một popup; popup không vượt viewport 390×844.
- Chuyển các liên kết thao tác trong trang ca/nhật ký/điều chỉnh thành nút đồng bộ. Ca được phân nằm trong card, trạng thái tiếng Việt và thời gian Việt Nam, không hiện branch ID.
- Nhật ký trả tên công việc thay cho ID để nhận diện hoạt động.
- Đã gửi thực tế một đề nghị [DEMO] thêm 30 giây qua form, HTTP 201, đang PENDING/chờ duyệt. Chưa áp dụng thay đổi vào khoảng thời gian.

## Số liệu đối chiếu (giờ Việt Nam)

| Ngày | Hoạt động mẫu | Tổng thời gian công việc | Ca mẫu đã kết thúc |
|---|---|---:|---:|
| 16/11/2026 | Sắp xếp xe dụng cụ | 60 phút | 4 giờ |
| 17/11/2026 | Chuẩn bị đồ vải | 90 phút | 4 giờ |
| 20/11/2026 | Kiểm tra dụng cụ 5 phút; Sắp xếp đồ vải 10 phút | 15 phút | 0 giờ; ca thật còn mở |

Để xem dữ liệu lịch sử, mở bộ lọc Nhật ký thời gian và chọn ngày 16 hoặc 17/11/2026. Tại Đề nghị điều chỉnh có một đề nghị mẫu chờ duyệt, hoặc bấm Tạo đề nghị điều chỉnh để mở form.

## Kiểm chứng

- Backup COPY_ONLY + CHECKSUM, RESTORE VERIFYONLY: Passed; `backup-receipt.json` lưu vị trí backup.
- Repair và demo: thử transaction rollback trước commit. Hai lần thử ban đầu phát hiện audit JSON và thứ tự ghi checklist chưa hợp lệ; đã rollback, sửa và thử lại thành công, không bỏ qua trigger/constraint.
- `dotnet build HotelOps.slnx`: Passed, 0 warnings / 0 errors (`backend-build.log`).
- `dotnet test HotelOps.slnx`: đã chạy; regression project chạy riêng vì ngoài solution. Bộ lọc OwnTimeLog/WorkdayAdmission/WorkdayMembership/CleaningClosureBoundary/TimeCorrection: 54 passed (`backend-tests.log`).
- Frontend `npm run build`: Passed. Vite còn cảnh báo bundle JS >500 kB (`frontend-build.log`).
- Bộ kiểm thử giao diện worktime/scheduled: 66 passed (`frontend-tests.log`). Chạy lại time workspace sau khi thêm hai case popup: 12/12 passed (`popup-tests.log`); số này có trùng với bộ 66, không cộng thành số test độc lập.
- API own-time-log thực tế: ba ngày đều HTTP 200, lần lượt 3600 / 5400 / 900 giây; tên công việc đúng (`live-results.json`).
- Submit form thực tế: HTTP 201 (`correction-created.json`); đề nghị mẫu chờ duyệt, chưa duyệt thử.
- Giao diện desktop/mobile, khung ca và popup: Passed, không có page error/alert trong các trang đã kiểm (`screenshots/`).
- Kiểm tra diff: không có lỗi whitespace; Git có cảnh báo chuyển LF → CRLF theo cấu hình hiện có.
- Phiên kiểm thử tạm đã thu hồi, cookie TEMP đã xóa (`session-cleanup.log`). API Development local đã khởi động lại.

## Các file nguồn thay đổi trong lượt này

Tạo mới:
- `frontend/src/workTime/CreateTimeCorrection.tsx`

Chỉnh sửa:
- `frontend/src/workTime/CorrectionRequestForm.tsx`
- `frontend/src/workTime/MyTimeLogPage.tsx`
- `frontend/src/workTime/TimeCorrectionsPage.tsx`
- `frontend/src/workTime/timeLogApi.ts`
- `frontend/src/workTime/work-time.css`
- `frontend/src/workday/ShiftAssignmentSummary.tsx`
- `frontend/src/workday/WorkdayReadPanel.tsx`
- `frontend/src/workday/WorkdayRoute.tsx`
- `frontend/tests/time-workspace-browser.test.mjs`
- `src/HotelOps.Api/Services/WorkTime/OwnTimeLogReader.cs`

SQL và bằng chứng nằm trong `artifacts/worktime-repair/`: proposal, Query/Backup/Verify scripts, repair-draft/repair-apply, demo/demo-apply, các bản ghi rollback/install/verify, probe C#, browser checks, ảnh và logs. Các thay đổi khác đang có sẵn trong working tree được giữ nguyên.

## Giới hạn còn lại

- Hồ sơ vệ sinh cũ thiếu checklist và hai phiếu làm lại trùng nguồn vẫn được giữ để tra cứu; tạm giữ chỉ giải quyết trạng thái đang chạy chặn kết thúc ca, không tái tạo nguồn nghiệp vụ cũ.
- Chưa kiểm thử bước quản lý duyệt đề nghị mới trong lượt này. Đề nghị DEMO được giữ chờ duyệt để người dùng xem.
- Không đóng ca thật, không sửa quyền, không sửa generated EF entities, không tạo migration, commit, push hay deploy.
