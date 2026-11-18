# Trang cấu trúc khách sạn — 19/11/2026

## Đã sửa

- Trang `/hotel` có tiêu đề gọn, bộ chọn chi nhánh và các nút cách nhau rõ ràng. Sửa dấu `?` trong tên chi nhánh và nhãn trạng thái tiếng Anh.
- Bộ lọc trạng thái thu gọn, spinner khi tải, thông báo lỗi và trạng thái chưa chọn/trống riêng biệt.
- Danh sách tầng/khu vực/phòng/bộ phận thành các hàng có biểu tượng, tên/mã, trạng thái và nút mở cấp tiếp theo. Giữ phân trang và đường dẫn quay lại từng cấp.
- Chi tiết mục đang xem hiển thị nhãn tiếng Việt, không bày các ID và tên thuộc tính kỹ thuật. Desktop hiển thị cạnh danh sách, màn hình nhỏ xếp xuống dưới.
- Các liên kết quản lý được gom vào nhóm **Quản lý cấu trúc**, hiện khi phản hồi của API là `visibility: management`. API và quyền ghi vẫn được máy chủ kiểm tra; không cấp quyền mới.

## Tệp của lượt sửa trang

- Sửa `frontend/src/hotel/BranchStructureTree.tsx`.
- Thêm `frontend/src/hotel/structure-presentation.tsx`.
- Thêm `frontend/src/hotel/hotel-structure.css`.
- Thêm `frontend/tests/hotel-structure-workspace.test.mjs`.
- Báo cáo này.

Khi kiểm thử gói UI trước khi push, sửa thêm selector trong `frontend/src/hotel/room-directory-layout.css` để bố cục bộ lọc phòng còn hiệu lực khi nằm trong vùng thu gọn. Cập nhật 4 ca trong `frontend/tests/cacao-shell.test.mjs` theo StatusBadge, drawer báo sự cố và thao tác mở bộ lọc P1.

Không sửa schema, C# ứng dụng hoặc cơ chế xác thực trong lượt này. Không sửa component cấu trúc lịch sử chưa được mount.

## Kiểm chứng

- Build frontend: **Passed**, TypeScript và Vite hoàn tất.
- **28/28 kiểm thử Passed**: 10 ca UI trang thật với API fixture và 18 ca API client/đối chiếu ngữ cảnh cấu trúc. Kiểm tra điều hướng theo cấp, phân trang, trạng thái, đổi chi nhánh, phản hồi cũ, dữ liệu trống, lỗi và quản lý/basic.
- Hồi quy gói UI: **104 ca Passed** ở các bộ design-system V2, UI P1, quản lý người dùng, quản lý chi nhánh và API client chi nhánh; **20/20 ca shell Passed** sau khi sửa selector bộ lọc phòng và cập nhật các kiểm thử theo giao diện mới. Lần chạy đầu có 4 ca shell lỗi; kết quả chạy lại đã đạt, không coi lần đầu là Passed.
- Đã xem ảnh 390, 1024, 1440 px với toàn bộ stylesheet ứng dụng và container `cacao-content`; các phần kiểm tra không làm tràn ngang trang.
- Prettier bốn tệp frontend của lượt sửa: Passed.
- Production local cổng 5080: **13/13 smoke checks Passed**, phục vụ asset mới; đăng nhập, kiểm tra ẩn danh, CSRF và SQL health vẫn đạt.
- .NET build/test: Not Run trong lượt UI này vì không sửa C#.
- Bằng chứng local: `artifacts/hotel-structure-ui/` (log, ảnh và công cụ kiểm tra). Không đưa dữ liệu kiểm thử hay bí mật local lên nhánh.

## Giới hạn

Kiểm thử UI sử dụng dữ liệu API mô phỏng; không thử ghi nghiệp vụ thật. Build còn cảnh báo React Router bỏ qua `use client` và bundle JS lớn hơn 500 kB. Không khẳng định toàn bộ test của repository đã đạt; các bộ kiểm thử cũ có giới hạn đã ghi tại báo cáo UI P1/V2.

## Phạm vi đưa lên nhánh

Người dùng đã yêu cầu commit/push lên nhánh hiện tại và chọn **toàn bộ thay đổi UI hiện có**. Gói UI bao gồm các thay đổi frontend từ các lượt trước cùng trang `/hotel` này; giữ nguyên nhánh `integration/dev1-dev2-dev3-unblock-20261114`. Các thay đổi backend cấu hình Production local không thuộc gói UI.
