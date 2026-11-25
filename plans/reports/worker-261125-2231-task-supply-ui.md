# Frontend — vật tư theo công việc (25/11 22:31)

Đã sửa nhãn nhóm A, mô tả định mức theo sức chứa tối đa, bỏ ô cấp đầu ca nhóm A, cập nhật thông báo lỗi và hướng dẫn đối soát/kiểm đếm. Giữ dữ liệu initialQuantity cũ khi sửa; khi tạo nhóm A dùng perGuestQuantity làm giá trị tương thích API cũ. Backend hiện vẫn yêu cầu InitialQuantity > 0. Nhóm B tái sử dụng giữ trường số lượng, đổi nhãn trung tính Định mức đồ dùng; cần backend xác nhận ý nghĩa trước khi thay đổi cấu trúc.

Files: supply-configuration.tsx, supply-api.ts, p0-error-messages.ts, cleaning-supply-reconciliation.tsx, room-supply-assessment.tsx, docs/ui/compact-ux.md.

node --check artifacts/task-supply-ui-edit.mjs: Passed. Typecheck đang chạy, chưa xác nhận kết quả. Không sửa DB, không phát sinh chứng từ. Python không khả dụng nên dùng Node để sửa UTF-8. Các thay đổi có sẵn trong workspace được giữ nguyên.
