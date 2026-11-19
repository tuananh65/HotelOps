# Báo cáo chỉnh giao diện và bộ phận tiếp nhận — 20/11/2026

## Kết quả triển khai

- Ẩn hai loại yêu cầu cần cấp vật tư khỏi biểu mẫu yêu cầu nội bộ. API cũng không trả hai loại này trong danh sách tạo mới; các hồ sơ cũ vẫn giữ nguyên.
- Loại yêu cầu cụ thể dùng bộ phận mặc định đang hoạt động trong chi nhánh từ dữ liệu hiện có. Đổi loại yêu cầu sẽ xóa lựa chọn bộ phận cũ và chọn bộ phận phù hợp. API kiểm tra lại quan hệ này khi gửi.
- Theo xác nhận của người dùng, **Yêu cầu khác** cho chọn mọi bộ phận đang hoạt động trong chi nhánh. Không suy đoán ánh xạ bộ phận giữa các chi nhánh.
- Bỏ các đoạn hướng dẫn dư thừa trong biểu mẫu sự cố; giữ kiểm tra dữ liệu và xác nhận sự cố tương tự.
- Bộ hiển thị chi tiết chung ẩn ID tham chiếu, phiên bản kỹ thuật và cờ nội bộ. Bảng giữ ID ở cột đầu khi có; các cột người dùng, chi nhánh, phòng hiển thị tên/số phòng. Bổ sung tên từ truy vấn đã được giới hạn quyền cho yêu cầu vật tư, lịch sử yêu cầu nội bộ và bằng chứng sự cố.
- Chuẩn hóa thông báo hiển thị, loại tiền tố MSG và mã lỗi thuần kỹ thuật; giữ mã gốc cho xử lý nghiệp vụ. Thông báo tạo sự cố thành công dùng trạng thái thành công.
- Trạng thái có màu riêng; nhóm Ca làm việc, lịch, nhật ký và đề nghị điều chỉnh dùng màu xanh/trắng của frontend.
- Các ô chọn có thể gõ để lọc, hỗ trợ tiếng Việt không dấu và giữ nguyên giá trị đã chọn khi tìm. Ô tìm kiếm có gợi ý từ dữ liệu đã được phép tải, vẫn cho nhập tự do. Không tải danh mục ngoài quyền để tạo gợi ý.

## Tệp thay đổi

Danh sách tệp mã nguồn và kiểm thử: [FILES.md](FILES.md). Phần lớn thay đổi frontend là thay control chọn/tìm kiếm và định dạng nội dung hiển thị, không sửa luồng cấp quyền.

Các tệp chính:

- `frontend/src/components/SearchableSelect.tsx`, `SearchInput.tsx`, `statusColors.ts`, `design-system.tsx`.
- `frontend/src/i18n/userMessage.ts`, `referenceLabel.ts`, `vi.ts`.
- `frontend/src/internalRequests/IntakePage.tsx`, `HistoryPage.tsx`, `historyApi.ts`.
- `frontend/src/dev2/DataView.tsx`, `AuthorizedOperations.tsx`, `OperationsReadFilters.tsx`, các view vật tư/sự cố.
- CSS của design system, internal requests, workday và workTime.
- `src/HotelOps.Api/Services/InternalRequests/InternalRequestIntakeService.cs`, `InternalRequestHistoryQueries.cs`.
- `src/HotelOps.Api/Services/Authentication/HomeNavigationService.cs`.
- `src/HotelOps.Api/Services/Incidents/IncidentEvidenceRead.cs`.
- `src/HotelOps.Api/Services/MaterialRequests/MaterialRequestService.Queries.cs`, `DTOs/MaterialRequests/MaterialRequestContracts.cs`.
- Kiểm thử frontend `ui-consistency`, `staff-workspace-browser`, `time-workspace-browser`; backend `LanAccessReadSqlTests`.

## Kiểm chứng

| Hạng mục | Kết quả | Bằng chứng |
|---|---|---|
| `dotnet build HotelOps.slnx` | **Passed**, 0 warning, 0 error | `backend-build-final.log` |
| `dotnet test HotelOps.slnx` | Lệnh thành công, solution không đưa regression project vào chạy | `solution-tests-final.log` |
| Regression project: intake và đọc quyền/dữ liệu SQL | **Passed 4/4**, không skip | `backend-tests-final.log` |
| Build frontend (TypeScript + Vite) | **Passed** | `frontend-build-final.log` |
| Hồi quy UI: Lan, vật tư, popup, yêu cầu, ca làm việc, nhật ký thời gian | **Passed 104/104** | `frontend-tests-final.log` |
| Kiểm tra control chung cuối cùng | **Passed 3/3**, gồm lựa chọn uncontrolled không tự đổi khi tìm | `presentation-final.log` |
| Kiểm tra trực tiếp 8 trang với tài khoản Lan | Không có alert lỗi, lỗi JavaScript hay mã MSG hiển thị | `live-results.json`, `screenshots/` |
| Màn hình nhỏ 390×844 | Không tràn ngang; popup sự cố tại x=16, y=24, rộng 358, cao 796 | `mobile-results.json` |
| Kiểm tra khoảng trắng diff theo CRLF của Windows | **Passed** | `diff-check-final.log` |

Kiểm tra thực tế xác nhận:

- Giặt là chỉ có bộ phận Giặt là, tự chọn đúng bộ phận.
- Yêu cầu khác có 8 bộ phận đang hoạt động tại chi nhánh của Lan.
- Hai loại cấp vật tư không xuất hiện trong danh sách tạo yêu cầu nội bộ.
- Bằng chứng sự cố hiển thị “Đặng Thị Lan” thay ID người tải.
- Tám trang đã mở: tạo yêu cầu nội bộ, lịch sử yêu cầu nội bộ, sự cố chi tiết, danh sách vật tư, bắt đầu/kết thúc ca, lịch ca, nhật ký thời gian, đề nghị điều chỉnh.

Đã thu hồi phiên kiểm tra tạm và xóa cookie chẩn đoán. Không sửa schema, role, permission hay dữ liệu nghiệp vụ. Không commit, push hoặc deploy.

## Giới hạn và cảnh báo

- Vite còn cảnh báo bundle lớn hơn 500 kB và chỉ thị `use client` của react-router bị bỏ qua. Build vẫn thành công; chưa thực hiện refactor chia bundle ngoài phạm vi.
- Hồ sơ hoặc API cũ không có tên tham chiếu hiển thị “Chưa có tên” hoặc nội dung trống phù hợp, không dựng tên giả và không đưa ID trở lại giao diện. Bản sửa không bổ sung dữ liệu nghiệp vụ lịch sử còn thiếu.
- Loại cụ thể chưa có bộ phận mặc định hợp lệ trong một chi nhánh sẽ báo thiếu cấu hình; không tự cho chọn bộ phận khác để vượt kiểm tra nghiệp vụ.
- Kiểm tra trực tiếp dùng GET và chặn các phương thức ghi. Các thao tác gửi/lưu được kiểm thử bằng fixture; không tạo yêu cầu, xuất vật tư hay bắt đầu/kết thúc ca thật để thử giao diện.
- Chưa chạy toàn bộ test của repository hoặc thử thủ công mọi vai trò. Các kết quả trên chỉ áp dụng cho phạm vi và bộ kiểm thử được ghi rõ.
