# Bàn giao Operations — triển khai và kiểm chứng phần được giao

Phạm vi theo `plans/261123-ux-role-refinements/operations.md`. Không sửa file tích hợp chung, grant, schema hoặc dữ liệu primary. Không chạy full build và không dừng tiến trình.

## Phần parent cần tích hợp

1. `OperationsControllers.cs`, trong `AssetsController`, thêm:

```csharp
[HttpGet]public Task<object> List(long branchId,CancellationToken ct)=>service.List(branchId,ct);
```

2. Thay endpoint Dashboard để chuyển query param vào service:

```csharp
[HttpGet("dashboard")]public Task<object> Dashboard(long branchId,CancellationToken ct,string? taskPeriod=null)=>service.Dashboard(branchId,ct,taskPeriod);
```

OM frontend luôn gửi `taskPeriod=day` mặc định; có `week` và `month`. Không truyền tham số giữ hành vi cũ cho vai trò khác. Giá trị khác trả 400 `INVALID_TASK_PERIOD`; thêm nhãn API tiếng Việt “Khoảng thống kê không hợp lệ. Chọn ngày, tuần hoặc tháng.” vào `api.ts` nếu cần. `taskWindow` trả `period`, `timezone`, `fromDate`, `toDate` (ngày cuối bao gồm), `fromUtc`, `toUtc` (cận trên loại trừ). Các số liệu `work` lọc `CreatedAt`; các chỉ số phòng/kho/cần chú ý vẫn snapshot hiện tại.

3. `app.tsx`: lazy import `OperationsSettings` từ `./operations-settings`; cho OM thuần thêm mục `settings` tên “Cấu hình vận hành”, hiện khi `can("ROOM_VIEW", false) || can("INVENTORY_VIEW", false)`. Nhánh render `active === "settings"` dùng `<OperationsSettings />` cho OM, giữ Catalog cũ cho vai trò khác. Thêm `settings` vào guard/route OM tương ứng. Component chỉ dùng danh mục theo branch: tầng/khu vực, phòng, loại phòng, bộ phận, kho, định mức. Catalog tự kiểm tra quyền ghi hiện tại; không thêm quyền mới.

4. `work.tsx` thuộc parent: kiểm tra bố cục OM trên ba nhóm `housekeeping`, `technical`, `work` ở 1440/768/375px. Bộ lọc/tab/thao tác phải wrap, bảng cuộn riêng; tên công việc cần đủ chiều rộng và ngắt dòng, cột thao tác không kéo tràn viewport. Không đổi lifecycle/grants. Tôi không sửa file này theo ownership.

5. Test OM cũ `frontend/tests/compact-operations-manager.mjs`: fixture mới cần GET `assets` trả array có `roomId`, `roomCode`, `locationName`, `isActive`; test đếm hàng dùng `tbody tr:not(.asset-group-heading)` (25/1). Dashboard fixture cần `taskWindow` theo query period. Khi thêm cấu hình, số menu đủ quyền tăng từ 8 lên 9. Tài khoản ROOM_VIEW-only có thêm cấu hình read-only nếu parent chọn hiện mục này.

## Phần đã triển khai

- `assets.tsx`: OM nhóm phòng, phân trang 25 tài sản (không tính tiêu đề nhóm), tìm cả phòng/vị trí trước phân trang; lọc trạng thái tiếng Việt, thanh tình trạng tổng hợp trên toàn bộ kết quả active. Tài sản ngoài phòng và ngừng sử dụng không biến mất. Giữ Owner và các vai trò khác dùng nguồn dữ liệu cũ. Sửa nguồn lịch sử từ `events` thành `history` đúng API hiện hữu.
- `AssetService.List`: ASSET_VIEW theo branch, AsNoTracking, CancellationToken, DTO projection và tên phòng/vị trí cùng branch; không xuất entity.
- `insights.tsx`, `operations-metrics.tsx`: bộ chọn kỳ OM, một thanh phân đoạn tài sản thay nhiều thanh riêng; không trình bày thống kê toàn thời gian như số liệu kỳ khi controller chưa nối.
- `InsightService.cs`, `InsightService.Operations.cs`: khoảng lịch địa phương quy đổi UTC trước truy vấn; tuần bắt đầu thứ Hai, tháng theo lịch; không lấy kỳ theo múi giờ máy client.
- `asset-condition-bar.tsx`, `asset-presentation.ts`, `operations.css`, `operations-settings.tsx`: component và styles riêng; GOOD=Tốt, FAULTY=Hỏng, còn lại=Khác, tổng và số 0 thật.
- Test: `frontend/tests/compact-operations-refinements.test.mjs`, `frontend/tests/compact-operations-refinements.mjs`, `tests/HotelOps.Compact.Tests/OperationsWindowTests.cs`.

## Kiểm chứng

- Frontend typecheck: **Passed** (`npm run typecheck`); lượt cuối `npx tsc --noEmit --pretty false` sau thay đổi lịch sử cũng exit 0, không có diagnostic được báo.
- Unit frontend: **Passed 2/2**, `node --test tests/compact-operations-refinements.test.mjs`.
- C# tính khoảng lịch: **Passed 6 checks** bằng Roslyn/.NET 10 biên dịch trực tiếp `InsightService.Operations.cs` với runner tại `artifacts/operations-refinements/WindowRunner.cs`. Runner có shim tương đương `Output.Require`/`ApiException`, không kiểm chứng toàn bộ service hoặc truy vấn EF. Kiểm tra ngày/tuần/tháng UTC+7, tuần qua năm, DST 23 giờ và invalid period 400. Không build API hay kết nối SQL. Bộ xUnit tương ứng đã thêm nhưng chưa chạy qua project test.
- Browser fixture: **Passed**, `node tests/compact-operations-refinements.mjs`, Edge headless, toàn bộ API mock và chỉ cho phép GET. Kiểm tra mặc định day, week/month gửi query/cập nhật số lượng thật theo fixture; một thanh 7/3/2; nhóm phòng, phân trang 25 tài sản, tìm phòng trước phân trang, lọc Hỏng, số đếm 0/5/0, empty/reset, không có nút thêm khi thiếu grant; không tràn trang ở 1440/768/375px. Không có pageerror. Đã xem ảnh 375px, bảng cuộn trong khung riêng.
- Bằng chứng: `artifacts/operations-refinements/verification.json`, `assets-375.png`. Hai lượt browser đầu thất bại do selector trùng tên vùng thống kê và timeout selector bộ lọc trong lúc trang reload; đã chuyển selector sang combobox cụ thể và chạy lại trên source ổn định, kết quả Passed.
- Prettier các file frontend thay đổi: **Passed**; file browser test được format sau lần chạy cuối (chỉ whitespace).
- Full solution build: **Not Run**, tuân theo lệnh không full builds trong assignment. `dotnet test HotelOps.slnx`: **Not Run**; parent cần chạy sau tích hợp. Không khẳng định backend build hoặc toàn bộ test suite đã qua.
- API thật/database: **Not Run**. Đã đối chiếu Asset/Room/Location/WorkItem/Branch và mappings; không thay đổi schema/data. Route assets mới và query taskPeriod cần parent nối trước khi kiểm chứng runtime. Component cấu hình chưa có route nên chưa kiểm thử browser; parent cần kiểm tra sau khi nối app.

Không còn lỗi được phát hiện trong kiểm chứng đã hoàn tất; các khoảng trống kiểm chứng và nối shared files nêu trên vẫn còn. Không commit/push/deploy. Journal: `plans/journals/2026-11-23-operations-assets-and-calendar-statistics-refinements.md`.
