# Hoàn tất cache thông báo và probe realtime

- Sửa `src/HotelOps.Api/Compact/Services/NotificationService.cs`: cache chứng từ, chứng từ nguồn, quyền và vai trò trong một snapshot/page; inbox tái sử dụng cache cho mọi row. Không cache giữa các pulse.
- Sửa duy nhất helper đọc trong `src/HotelOps.Api/Compact/Services/DocumentService.Transfers.cs`: callback tùy chọn để tái sử dụng kiểm tra đã cache; giữ chính sách quyền/vai trò cùng chi nhánh và chỉ nhận nguồn TRANSFER.
- Thêm `tests/HotelOps.Compact.Tests/TransferNotificationVisibilityTests.cs`: 13 trường hợp quyền người tham gia, vai trò khác chi nhánh, nguồn phiếu nhận sai loại/không tồn tại, loại chứng từ không được hưởng ngoại lệ.
- Sửa `scripts/verification/v2-business-realtime-probe.mjs`: thay yêu cầu heartbeat bằng thao tác read thật trên cùng stream; giữ kiểm tra không phát thêm snapshot sau stale retry; read kiểm tra quyền sở hữu, unread decrement, badge; lưu phase/counters/transportErrors. Đếm badge đã điều chỉnh vì đối chứng đọc thông báo submit trước approval.

Validation:
- `node --check scripts/verification/v2-business-realtime-probe.mjs`: Passed.
- `dotnet build HotelOps.slnx --artifacts-path .artifacts/finish-realtime`: Passed, 0 errors, 4 NU1900 warnings (không truy cập được NuGet vulnerability endpoint), elapsed 6m23s.
- Test được chạy riêng bằng `dotnet test HotelOps.slnx --artifacts-path .artifacts/finish-realtime --no-build --no-restore --filter FullyQualifiedName~TransferNotificationVisibilityTests`; xem báo cáo cuối trong phản hồi agent.
- Không chạy server/browser/DB; agent chính sở hữu runtime 5779 và cần chạy lại probe thực tế. Không claim delivery end-to-end đã xác minh sau sửa.
- Không dùng git, không publish/deploy.

Câu hỏi còn mở: không có; runtime verification còn thuộc agent chính.
`nKết quả test cuối: Passed 13/13, Failed 0, Skipped 0; duration 2s.
