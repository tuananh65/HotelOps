# Bàn giao Workday — 23/11/2026

Phạm vi: `plans/261123-ux-role-refinements/workday.md`, Project 2 Compact, FE-12 / UC-138. Triển khai trên nhánh hiện có; không chỉnh các tệp tích hợp do parent sở hữu.

## Đã triển khai

- Chọn nhiều nhân viên bằng checkbox; chọn tất cả khi đủ sức chứa. Danh sách chỉ gồm người đủ điều kiện trên **toàn bộ ngày đã chọn**, không lấy API `/users`.
- Checkbox áp dụng trong tháng, chọn thứ trong tuần và bỏ từng ngày. Mặc định gồm mọi thứ; tháng hiện tại bắt đầu từ hôm nay theo ngày chi nhánh trả về. Có xem trước số người × số ngày và sức chứa còn lại từng ngày.
- Phân công batch một giao dịch: kiểm tra quyền chi nhánh, active, membership/role đúng thời gian ca, quyền thực thi, version, sức chứa từng ngày, trùng lịch kể cả chi nhánh khác; chỉ thêm dòng sau khi mọi lựa chọn hợp lệ. Giữ cùng application lock và `Atomic` với phân công đơn/start/end/cancel. Mỗi phân công có event và audit trong transaction.
- Batch giới hạn 1–100 người và 1–31 ngày trong cùng tháng, từ hôm nay đến 366 ngày sau. Không chấp nhận danh sách trùng. Operation key ổn định khi retry; dùng key con xác định cho từng event vì bảng history yêu cầu key duy nhất. Replay batch đã commit bị từ chối, không phải endpoint replay trả lại receipt.
- Mốc lễ hiển thị trên lịch tuần và danh sách ngày. Có bỏ ngày có mốc lễ hoặc xác nhận vẫn phân ca. Backend kiểm tra xác nhận này. Không tạo trạng thái nghỉ phép/nghỉ bù, không tự áp dụng lịch nghỉ khối hành chính cho khách sạn.
- Màn hình cấu hình ca dùng quyền `SHIFT_MANAGE` hiện có: tạo/sửa/ngừng ca, chọn bộ phận đang hoạt động trong chi nhánh và vai trò đang có quyền `ATTENDANCE_RECORD`. Không sửa hoặc cấp role/grant. Dùng tên, giờ, sức chứa và trạng thái đúng schema; không invent mã ca.
- Ca đã có phân công giữ nguyên department/role/time để bảo toàn lịch sử; còn PLANNED/WORKING không được ngừng; không giảm sức chứa thấp hơn số người đã phân trong bất kỳ ngày nào. Kiểm tra duplicate definition, version và audit.
- Giữ bộ lọc từng đối tượng, lịch OWN và start/end/cancel cũ. Giao diện không cho lưu khi đang tải, lỗi dữ liệu, vượt sức chứa hoặc thiếu xác nhận mốc lễ. Khi lỗi ghi, tải lại nguồn cho lần mở tiếp theo.

## Tệp tạo/sửa

Sửa:

- `frontend/src/compact/workday.tsx`
- `frontend/tests/compact-workday-filter.mjs`
- `src/HotelOps.Api/Compact/Services/WorkdayService.cs` — chuyển sang partial, lifecycle cũ giữ nguyên.
- `src/HotelOps.Api/Compact/Controllers/WorkdayController.cs`

Tạo:

- `frontend/src/compact/workday-assignment.tsx`
- `frontend/src/compact/workday-settings.tsx`
- `frontend/src/compact/workday-scheduling.ts`
- `frontend/src/compact/workday.css`
- `frontend/tests/compact-workday-scheduling.mjs`
- `src/HotelOps.Api/Compact/Contracts/WorkdayRequests.cs`
- `src/HotelOps.Api/Compact/Services/WorkdayService.Scheduling.cs`
- `src/HotelOps.Api/Compact/Services/WorkdayCalendar.cs`
- `scripts/database/compact-v2/test-workday-scheduling-pure.ps1`
- `scripts/database/compact-v2/WorkdaySchedulingChecks.cs`
- `plans/reports/implementation-261123-0943-workday-refinements.md`
- `plans/journals/2026-11-23-workday-multi-staff-scheduling-and-configuration.md` — nhật ký cục bộ bằng `ak-journal`; AgentWiki publish skipped.

Không sửa schema/entities/grants/DI, `app.tsx`, `api.ts`, `ui.tsx`, CSS global, `Common.cs` hay `OperationsControllers.cs`.

## Tích hợp cho parent

1. `Workday` đã có nút **Cấu hình ca**, nên luồng sử dụng được ngay trong màn hình lịch. Nếu cần menu riêng theo thiết kế parent, import default `WorkdaySettings` từ `./workday-settings`, mount `<WorkdaySettings />` và gate bằng `can("SHIFT_MANAGE")`. Component tự kiểm tra quyền và chi nhánh. Không thêm grant. Không cần đăng ký service mới vì dùng partial `WorkdayService` đã đăng ký.
2. Các endpoint mới dưới `/api/v2/workday`:
   - `GET candidates/batch?shiftId=...&dates=2026-11-23&dates=2026-11-24` trả `{users, slots:[{workDate,remaining}]}`.
   - `POST assignments/batch` nhận `{shiftId,userIds,workDates,rowVersion,operationKey,holidaysAcknowledged}`; trả `{count,assignments}` qua DTO.
   - `GET calendar?branchId=...&year=2027` trả `{markers:[{date,name,kind,source}],notice}`; nhận SHIFT_MANAGE hoặc ATTENDANCE_RECORD OWN đúng read scope cũ.
   - `GET configuration?branchId=...` trả `{departments,roles,shifts}`; shifts có `hasHistory`.
   - `POST shifts`, `PUT shifts/{id}` nhận `{branchId,departmentId,roleId,name,startMinute,endMinute,capacity,isActive,rowVersion,operationKey}`.
3. Các request ghi mới giữ controller thin, `[Authorize]`, HTTP 400/403/404/409 qua middleware hiện có. Local error mapper ở `workday-scheduling.ts` nên không cần sửa `api.ts`.
4. Fixture UI khác nếu mở Workday cần stub `workday/calendar` (ít nhất `{markers:[],notice:"..."}`); fixture phân công chuyển sang `workday/candidates/batch` và `workday/assignments/batch`. Test filter trong phạm vi này đã cập nhật.
5. Parent chạy full build/test và API integration trên database **fixture đã backup**, theo quy định repo. Delegated scope cấm full builds/primary writes nên chưa thực hiện các bước này tại đây.

## Kiểm chứng

- Frontend typecheck: **Passed** (`npm run typecheck`).
- Formatting: kiểm tra phát hiện một lỗi định dạng trong `workday-settings.tsx`; đã chạy Prettier sửa. Các tệp Workday còn lại không báo lỗi định dạng.
- Biên dịch C# phạm vi Workday: **Passed** bằng Roslyn trên toàn bộ service partial/controller/DTO hiện tại, tham chiếu build outputs hiện có; không chạy full build. Lần đầu phát hiện endpoint lặp và nullable warning; đã sửa, lần chạy lại không còn warning mới. Script chỉ bỏ CS0436 dự kiến do source WorkdayService thay thế type cũ trong assembly tham chiếu.
- Logic thuần: **Passed**, 15 checks: ngày rỗng/trùng/quá số lượng, quá khứ, biên 366 ngày, cùng tháng, sắp xếp, múi giờ Việt Nam, Culture Day, Tết 2027, ngày liền kề Quốc khánh và thông báo năm chưa xác minh.
- Browser fixture: **Passed** `node tests/compact-workday-filter.mjs`: bộ lọc, giữ nhóm khi đổi tuần, gửi đúng ca, lịch cá nhân/rỗng và responsive.
- Browser fixture: **Passed** `node tests/compact-workday-scheduling.mjs`: chọn nhiều người, tháng, weekdays, xác nhận/bỏ mốc lễ, xử lý 409 giữ form, key khi retry, cấu hình tạo/sửa, khóa history, 24:00, OWN và 1440/768/375px. Lần đầu lỗi selector test cho label chứa select; đã sửa selector theo role, chạy lại thành công. Có xem ảnh và sửa checkbox từ cột sang hàng.
- Bằng chứng: `artifacts/workday-scheduling/verification.json`, `monthly-1440.png`, `monthly-768.png`, `monthly-375.png`; `artifacts/workday-filter/verification.json`; output compiler/pure test trong `artifacts/workday-validation`.
- Full build `dotnet build HotelOps.slnx`: **Not Run**, theo giới hạn nhiệm vụ.
- Full test `dotnet test HotelOps.slnx`: **Not Run**, theo giới hạn nhiệm vụ.
- SQL/API thật: **Not Run**. Đã kiểm tra source, mapping và route; fixture browser không chứng minh SQL translation, lock concurrency, rollback hoặc authorization thực tế. Không có primary writes, migration, backup/restore, process shutdown, commit/push/deploy.

## Nguồn mốc lịch và giới hạn

Đã truy cập nguồn chính thức ngày 23/11/2026. Công cụ web trả 404; fallback đọc HTTP trực tiếp cổng Chính phủ thành công:

- [Lịch Tết, lễ 2027](https://xaydungchinhsach.chinhphu.vn/lich-nghi-tet-nghi-le-2027-119261115134738791.htm): dùng mốc dương lịch và Giỗ Tổ 16/4/2027; 27/7 là mốc REVIEW để đối chiếu lịch đơn vị.
- [Tết và Quốc khánh 2027](https://xaydungchinhsach.chinhphu.vn/de-xuat-2-phuong-an-nghi-tet-nguyen-dan-2027-tet-dinh-mui-11926080513033257.htm): mùng 1 Tết 6/2/2027, khoảng các lựa chọn Tết đánh dấu REVIEW; 1/9 và 3/9 là lựa chọn cần đối chiếu, không khẳng định cả hai đều nghỉ.
- [Ngày Văn hóa Việt Nam 24/11](https://xaydungchinhsach.chinhphu.vn/ngay-24-11-hang-nam-la-ngay-van-hoa-viet-nam-nguoi-lao-dong-duoc-nghi-huong-nguyen-luong-119260113152642414.htm): đánh dấu từ năm 2026, không áp dụng ngược lịch sử.

Mốc âm lịch đã xác minh cho **2027**; năm khác hiển thị rõ dữ liệu chưa đầy đủ. Không suy diễn lịch âm bằng lịch Trung Quốc hoặc cộng ngày cố định. Chưa có lưu cấu hình ngày nghỉ/nghỉ bù theo chi nhánh vì schema không chứa thực thể này và nhiệm vụ cấm đổi schema. Người lập lịch chủ động chọn/bỏ ngày; vẫn cần đối chiếu lịch nghỉ được đơn vị chấp thuận.

Không còn câu hỏi bắt buộc để dùng phần triển khai. Các bước còn lại thuộc tích hợp parent: điều hướng nếu muốn menu riêng, full validation và SQL fixture integration.
