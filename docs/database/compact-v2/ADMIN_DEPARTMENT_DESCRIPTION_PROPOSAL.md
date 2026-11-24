# Mô tả bộ phận cho trang Admin

Entity Department và mapping hiện có chưa có mô tả; RoomType đã có description. Để lưu được mô tả do Admin chỉnh sửa, đề xuất thêm duy nhất `dbo.departments.description nvarchar(2000) NULL`.

Không điền mô tả giả cho dữ liệu cũ. Đồng bộ thuộc tính `Description` và mapping Database First; CatalogService nhận/trả trường này qua DTO danh mục hiện có, kiểm tra độ dài 2000 và giữ kiểm tra quyền/row version/audit. Không migration EF, không đổi grants/trạng thái hoặc bảng khác.

Script: `scripts/database/compact-v2/add-department-description.sql`, idempotent, kiểm tra schema đích. Sao lưu mới và VERIFYONLY trước áp dụng. Kiểm thử trên fixture riêng, .NET build/test và browser trước khi cập nhật Production. Tác động chung BE1/BE2: Department thêm một trường tùy chọn; không thay ý nghĩa các trường hoặc quan hệ hiện có. API mới chỉ chạy sau khi database có cột; bản API cũ vẫn chạy được.

Các đổi giao diện công tắc/ẩn trạng thái trong form/ẩn chọn chi nhánh Admin được thực hiện độc lập trong lúc chờ duyệt schema.
