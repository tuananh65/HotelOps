# Kết quả kiểm tra

Ngày kiểm tra: 16/10/2026. Kiểm tra front-end trên Linux, Node.js 24.19.0; bản build Vite 6.4.3. Browser test dùng Chromium 153 qua Playwright. Không có dịch vụ ASP.NET Core/SQL Server vận hành được cung cấp để gọi tích hợp thật.

| Mức kiểm tra | Lệnh hoặc cách kiểm tra | Kết quả |
|---|---|---|
| Source / kiểu | `npm run typecheck` | Đạt |
| Định dạng theo cấu hình project | `npm run lint` | Đạt; script lint của project là Prettier check, không khẳng định đã chạy ESLint |
| Unit / HTTP contract | `npm test` | 34 tests đạt, 0 fail |
| Build production | `npm run build` | Đạt, tạo dist; JS khoảng 607 kB, gzip khoảng 163 kB |
| Kiểm kê source | `node scripts/verify-screens.mjs` | 80 file màn hình, 80 route import, 144 UC, 103 operation contract riêng |
| UI bằng HTTP fixtures | `npm run test:e2e` | 9 tests đạt; một test đi qua lần lượt cả 80 route; các test còn lại kiểm tra login/permission/error/empty/validation/double submit/unknown result và desktop/mobile |
| Diff | `git diff --check` | Không có lỗi whitespace |
| API thật | Gọi controller ASP.NET Core đang chạy | Chưa thực hiện; không được coi là đã kết nối thành công |
| CORS/cookie thực tế | Request cross-origin tới server thật | Chưa thể kiểm tra |
| Transaction và dữ liệu SQL | Ghi nhận stock ledger, audit, room status, concurrency trong EF/SQL | Chưa thể kiểm tra; không có code back-end được thực thi hoặc sửa |

Unit/contract tests kiểm tra serialization query, JSON/multipart/204, ProblemDetails, message code SRS, 401/403/404/409/412/422/5xx, timeout/mất mạng, HTTP 202, rollback đã xác nhận, bỏ response của phiên cũ và không fallback mock. Chúng kiểm tra tách trạng thái/nhãn, safe BIGINT, UTC+7, tách số lượng thao tác với số lượng tích lũy, scope theo từng grant, 103 đường dispatch riêng, phân trang và gửi context source/room.

Các tests dùng fixture để xác nhận cách front-end gọi API và phản ứng với kết quả. Chúng **không chứng minh** 103 nghiệp vụ đã được back-end triển khai, mọi alternative flow của 144 UC đã được nghiệm thu hoặc transaction SQL đúng. Bản fixture mô tả hợp đồng đề xuất; hai GET sự cố sử dụng field/ID kiểu số đúng với DTO C# đã đọc. Không có chế độ tự sinh dữ liệu test được thêm vào ứng dụng khi chạy bình thường.

Browser kiểm tra form lỗi giữ nguyên dữ liệu, chỉ báo lưu sau response thành công, hai lần submit chỉ phát một POST, lỗi mạng khi ghi khóa nút và hiện Reload. Trạng thái Pending Inspection lấy từ server được giữ khi render board. Đã xem ảnh desktop/mobile sau chạy: bố cục danh sách/phòng và responsive được giữ; không có tràn ngang ở hai kích thước đã kiểm tra.

Build có hai cảnh báo không chặn: directive `use client` của React Router được Vite bỏ qua và chunk JS lớn hơn 500 kB. Không thay đổi package hoặc thực hiện refactor chia bundle ngoài phạm vi tích hợp. Phần đặc tả UC dài được giữ trong tài liệu đối chiếu, không đưa nguyên văn toàn bộ vào bundle.
