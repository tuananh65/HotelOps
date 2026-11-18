# Chỉnh giao diện quản lý chi nhánh — 19/11/2026

Đã chỉnh ba màn danh sách, tạo mới và thông tin/chỉnh sửa chi nhánh theo giao diện chung.

- Danh sách có tổng số chi nhánh, số đang hoạt động/ngừng hoạt động từ dữ liệu hiện có; tìm kiếm, bộ lọc thu gọn, xóa bộ lọc, bảng và phân trang rõ ràng.
- Tạo mới chia thành thông tin chung và liên hệ, bổ sung gợi ý nhập, hiển thị múi giờ và bước xem lại trước khi lưu.
- Chi tiết trình bày theo hồ sơ chi nhánh, nhãn và trạng thái rõ; nút chỉnh sửa nổi bật, popup tiếp tục chứa cả form và phần xác nhận.
- Chuẩn hóa biểu tượng, khoảng cách, màu trạng thái và bố cục responsive. Sửa xung đột CSS kế thừa gây sai cột thống kê, thừa khung form và chồng biểu tượng tìm kiếm lên chữ.

## Tệp thay đổi

Trong `frontend/src/branch-maintenance/`:

1. `BranchManagementPage.tsx`
2. `CreateBranchRoute.tsx`
3. `BranchMaintenancePanel.tsx`
4. `BranchMaintenanceRoute.tsx`
5. `branch-ui.tsx`
6. `branch-management.css`

Thêm báo cáo này và công cụ/ảnh kiểm chứng local trong `artifacts/branch-ui-refresh/`. Không sửa API, quyền hay database trong lượt UI này. Không commit hoặc push.

## Kiểm chứng

| Kiểm tra | Kết quả |
|---|---|
| Frontend build, gồm TypeScript | Passed, exit 0 |
| Bộ UI quản lý chi nhánh hiện có | Passed: 15/15 |
| Danh sách/tạo/chi tiết với CSS ứng dụng và container cacao-content | Passed: 9/9 tại 390, 1024, 1440 px |
| Kiểm tra ảnh | Đã xem ảnh danh sách, tạo, chi tiết và popup; bảng trên điện thoại cuộn ngang trong khung |
| Prettier 6 tệp đã sửa | Passed |
| Production local 5080 | Passed: 13/13; HTTP chuyển HTTPS, asset mới, đăng nhập, SQL health, CSRF và truy cập ẩn danh; log `production-smoke.txt` |
| git diff --check trong module | Passed; Git nhắc chuẩn hóa LF/CRLF |
| .NET build/test | Not Run vì không sửa C# |

Log build, browser tests và ảnh ở `artifacts/branch-ui-refresh/`. Kiểm thử UI dùng API fixture, không ghi dữ liệu nghiệp vụ thật. Không chạy toàn bộ npm test. Build vẫn còn cảnh báo React Router bỏ qua `use client` và bundle JS lớn hơn 500 kB.

Các điều kiện ngừng hoạt động và dữ liệu cũ chưa rõ đã ghi trong `BRANCH-MANAGEMENT-FIX-20261119.md` vẫn áp dụng; lượt này chỉ chỉnh giao diện.
