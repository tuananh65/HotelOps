# Quy ước UX giao diện HotelOps compact

Áp dụng khi bổ sung/sửa trang tại `frontend/src/compact`. Duy trì lựa chọn đã được người dùng chấp nhận; không dùng thay đổi giao diện để thay đổi quyền, phạm vi chi nhánh hoặc trạng thái nghiệp vụ.

## Tiếp nhận tài liệu mới

Theo chỉ dẫn ngày 22/11/2026: xem xét tài liệu yêu cầu có chọn lọc, không áp dụng nguyên xi. UX đã chốt trong phiên và tài liệu này tiếp tục được giữ, kể cả khi đề xuất mới khác cách bố trí. Áp dụng tương tự cho các tài liệu tiếp theo. Đối chiếu nghiệp vụ, quyền và mã hiện có; ghi rõ phần áp dụng, phần không áp dụng và phần cần làm rõ. Không tự cấp quyền hoặc thay đổi dữ liệu để đáp ứng một màn hình.

Department Head: nhóm điều hướng riêng chỉ áp dụng khi vai trò hoạt động tại chi nhánh là DEPT_MANAGER; từng menu vẫn phải có quyền tương ứng. Tài khoản đa vai trò giữ các chức năng đã được cấp. Nhân sự phục vụ phân ca chỉ đọc ứng viên theo ca/ngày và lịch tuần, không dùng API `/users`. Phòng/tồn kho là liên kết tham khảo từ Tổng quan. Khả năng duyệt vật tư lấy từ backend, không suy ra chỉ bằng role hoặc MR_APPROVE_L1.

## Bố cục

- Ô nhập một dòng (tìm kiếm, ngày, số, văn bản) và dropdown dùng chung `--control-height` (31px), `--control-padding` và `--control-font-size` với nút bấm trên mọi trang. Giữ chiều rộng responsive theo bố cục; textarea, checkbox, radio và danh sách chọn nhiều dòng giữ kích thước riêng.
- Nút thao tác dùng token `--button-height` (31px, giảm khoảng 30% từ 44px), `--button-padding`, `--button-font-size` và `--button-icon-size`. Áp dụng đồng nhất cho nút tạo/lưu, phân trang, tab, liên kết dạng nút và menu; giữ chữ 12px để dễ đọc. Thẻ nội dung có thể bấm (phòng, tầng, nhân sự trong lịch) vẫn theo bố cục riêng; không thu nhỏ toàn trang hoặc ô nhập liệu bằng transform/zoom.
- Tiêu đề/mô tả thuộc `PageTitle`, hiển thị đúng một lần trong navbar. Không thêm tiêu đề h1 thứ hai trong nội dung.
- Chi nhánh hiện tại và Đổi khách sạn nằm trong `BranchContext` dưới logo sidebar dùng chung cho mọi vai trò; không lặp lại trong nội dung. Sidebar thu gọn vẫn có biểu tượng đổi khách sạn; mobile mở menu để chọn.
- Khoảng cách đầu trang dùng `--page-top-gap` (12px) và `--control-gap` (12px) trong `task-ux.css`. Trang tài khoản có thanh bộ lọc sticky riêng bắt đầu ngay dưới navbar.
- Dùng `PageToolbar` cho bộ lọc/tìm kiếm và `actions` cho các nút tạo. Tránh để nút tạo thành một hàng riêng phía trên bộ lọc. Toolbar danh mục, phòng và lịch có bố cục chuyên biệt đã được kiểm thử.
- Không chèn hàng trống sau khi tiêu đề đã chuyển lên navbar. Không thêm lại nút Làm mới; có Thử lại khi lỗi và tải lại dữ liệu sau khi lưu.
- Nút quay lại bên trái, tìm kiếm bên phải ở danh sách phòng. Khi màn hình hẹp, cho các nhóm xuống hàng; không thu chữ/nút đến mức khó bấm hoặc dùng overflow hidden để che lỗi.
- Bảng/lịch dài cuộn trong khung riêng; tiêu đề cột luôn thấy được dưới navbar/bộ lọc. Toàn bộ trang không được tràn ngang ở 375px.

## Trạng thái và tương tác

- HK Staff chỉ có HK_STAFF tại chi nhánh dùng 7 nhóm cá nhân; không dùng role ID để cấp quyền. Dashboard gọi `insights/personal`, không yêu cầu DASHBOARD_VIEW. Việc dọn/làm lại và công việc khác dùng `assignedToMe=true` để giữ lịch sử đã hoàn thành, không bật xem toàn nhánh. Tìm kiếm/sắp xếp/lọc ngày chạy trước phân trang. Khung chi tiết dùng drawer, các form tiến độ/hoàn thành/ghi chú/minh chứng nằm trong drawer; tạo yêu cầu/sự cố/vật tư dùng modal. IR dùng capability của backend để hiện thao tác điều phối; vật tư chỉ người tạo được sửa/gửi/hủy. Kết quả Inspection là thông tin chỉ đọc trong Cleaning/Rework; không cấp INSPECTION_VIEW/PERFORM cho HK Staff. Lịch cá nhân tiếp tục dùng Workday OWN; chỉ người được giao được ghi checklist/tiến độ và minh chứng công việc.
- Giữ loading/error/empty rõ ràng qua `State` và `Empty`; lỗi có Thử lại khi có thể tải lại. Nếu chỉ tìm trong dữ liệu trang hiện tại, ghi rõ “Tìm trong trang”. Không có kết quả tìm kiếm phải có thông báo và cách xóa tìm kiếm.
- Phân trang có tổng bản ghi, trang hiện tại/tổng trang, khóa Trước/Tiếp đúng biên. Đổi bộ lọc danh sách đầy đủ phải về trang đầu; đếm và lọc trước khi cắt trang.
- Phòng: 15 ô/trang, desktop 5 cột × tối đa 3 hàng; tablet/mobile giảm số cột. Phòng có khách phải có nhãn Có khách, vẫn giữ cảnh báo vệ sinh/chặn khi cần. Lễ tân không có dashboard.
- Lịch quản lý chỉ xem từng đối tượng; không có Tất cả đối tượng/Lịch toàn chi nhánh. Mặc định chọn nhóm đầu tiên có dữ liệu, giữ lựa chọn khi chuyển tuần; danh sách rỗng hiển thị Chưa có đối tượng. Lịch cá nhân giữ nguyên phạm vi của người dùng.
- Dùng `Dialog` chung; click khoảng trắng bên trong không đóng form. Chỉ click ngoài, Escape hoặc nút Đóng mới yêu cầu đóng; callback vẫn kiểm tra thao tác đang chờ. Khóa cuộn nền khi dialog mở, trả focus về nút mở khi đóng.
- `Catalog.onFinish(saved)` phân biệt lưu thành công và hủy/đóng. Chỉ làm mới danh sách cha khi `saved` đúng để tránh mất focus và tải lại vô ích.
- Chuyển route/chi nhánh bắt đầu ở đầu trang. Skip link chỉ chuyển focus vào main, không thay hash route. Chuyển động tuân theo prefers-reduced-motion.

## Kiểm tra trước khi bàn giao

### Quản lý vận hành

- Profile chỉ có OPERATION_MANAGER tại chi nhánh hiện tại dùng 8 nhóm điều hướng; mỗi nhóm và thao tác vẫn kiểm tra grant thực. Hồ sơ đa vai trò giữ điều hướng chung.
- Buồng phòng và Bảo trì & sự cố dùng lại Work, chỉ liệt kê các loại việc thuộc nhóm. Mặc định xem trong phạm vi quyền, không tự bật mine vì thiếu quyền phân công. Tìm kiếm/lọc chạy trước phân trang trên API, đổi nhóm xóa lọc trạng thái cũ.
- Kho & vật tư, Báo cáo & nhật ký dùng tab trong trang. Không thêm trang quản trị nhân sự, lịch hay quyền chuyên môn cho OM.
- Dashboard chỉ hiển thị thống kê hiện tại có quyền đọc, không giả lập xu hướng theo thời gian. Cần chú ý lấy tối đa 5 việc quá hạn mỗi nhóm.
- Nhật ký chỉ trả metadata, tìm theo hành động/loại/mã đối tượng, 100 dòng mỗi lần. Không giả tổng bản ghi khi API chưa trả tổng.
- Tài sản dùng editor Catalog chung; thay vị trí/tình trạng phải qua action endpoint, không sửa vòng qua catalog. Phòng có khách vẫn xuất hiện trong lọc Đang chặn nếu có block đang hiệu lực.

Từ `frontend`, dùng Vite tại `http://127.0.0.1:5186` (hoặc đặt `UX_BASE` cho kiểm thử navbar):

```powershell
npm run build
npm run test:compact:ux
```

Bộ UX dùng Playwright + Edge và chặn API bằng fixtures; không login, gửi email hay ghi database thật. Kiểm tra 16 tuyến trang ở 1440/768/375, các dashboard theo vai trò, phân trang tài khoản/phòng, cấu hình, lịch, sticky header, dialog và bàn phím. Các test chuyên biệt có cấu hình URL riêng trong file; dùng server 5186 cho cả bộ.

Xem ảnh trong `artifacts/navbar-page-titles`, `reception-rooms`, `structure-settings`, `workday-filter`, `dashboard-pagination`. Các tệp `verification.json` ghi bằng chứng. UI test giả lập không chứng minh nghiệp vụ API/database thật; thay đổi endpoint hoặc dữ liệu cần kiểm chứng riêng.
# Storekeeper — CompactDB, 23/11/2026

- Storekeeper có 8 nhóm điều hướng, từng nhóm/nút vẫn kiểm tra grant hiện hành. Dashboard kho dùng `INVENTORY_VIEW`; không bổ sung `DASHBOARD_VIEW`. Lịch cá nhân dùng Workday chung, truy cập từ dashboard.
- Danh mục vật tư mặc định mở mặt hàng, tìm kiếm trên toàn bộ danh mục trước khi phân trang; tên nhóm/đơn vị được giải mã. Chi tiết mặt hàng và biến động kho dùng drawer. Giữ ngoại lệ danh mục mặt hàng dùng chung V10 đang có; kho và định mức luôn theo chi nhánh.
- Tồn vật lý lấy từ ledger; các cặp vật tư/kho có định mức nhưng chưa có giao dịch vẫn hiện tồn 0. Không dựng số lượng giữ chỗ/khả dụng khi schema chưa có. Cảnh báo thấp chỉ tính khi có định mức.
- Chứng từ có tìm kiếm, lọc ẩn mặc định, trạng thái/ngày/sắp xếp và phân trang server 25 bản ghi. Trạng thái chuyển kho tính từ các phiếu nhận được lọc trước phân trang.
- Tạo, sửa dòng, kiểm đếm, duyệt, ghi sổ, đóng và tạo chứng từ liên kết dùng modal; chi tiết/lịch sử dùng drawer. Không mở form dưới bảng. Không báo thành công trước phản hồi backend; lỗi cập nhật số liệu mới và giữ thông báo để xử lý.
- MR đã duyệt → tạo phiếu ISSUE từ dòng nguồn và số lượng còn được cấp → xác nhận ghi sổ. Replenishment → tạo RECEIPT → ghi sổ. Bản nháp không tăng/giảm tồn. Không có nút duyệt MR cho Storekeeper.
- Trả/điều chỉnh/đảo giữ phê duyệt độc lập hiện có. Sau duyệt không sửa dòng. Ghi sổ kiểm tra lại tồn, nguồn, lượng còn lại và version trong transaction. Đảo ISSUE/RECEIPT tính lại trạng thái MR/Replenishment nguồn; không sửa header chứng từ đã post. Không đảo trực tiếp chứng từ có giao dịch phụ thuộc đã post.
- Giữ chặn chuyển liên chi nhánh. Không thay đổi role/grant/schema để vượt điều kiện workflow. Kiểm tra chỉ đọc dữ liệu chính cho thấy DVBV và DVCT mỗi nơi chỉ có một tài khoản có các quyền trả/điều chỉnh/đảo: cần giải quyết nhân sự/phê duyệt bên ngoài thay đổi này trước khi chạy các nghiệp vụ cần hai người trên primary.

## Maintenance Lead

- Giữ navbar có tiêu đề, chi nhánh dưới logo và controls gọn của toàn hệ thống. Tám nhóm chuyên môn đều kiểm tra grant; lịch cá nhân là liên kết phụ. Không thêm quyền quản trị, kho hay lập ca.
- Công việc và sự cố dùng lại Work với tìm/lọc/sắp xếp/phân trang API. Bảng có thiết bị, ưu tiên và người phụ trách thật; chi tiết bên phải. Phân công, đổi người, triage, escalation và review dùng modal. Escape đóng modal trên cùng, giữ drawer phía dưới.
- Trang nghiệm thu luôn lọc COMPLETED kể cả sau Đặt lại; chỉ có Chấp nhận hoặc Yêu cầu xử lý lại khi đủ quyền/trạng thái. Reopen trở về PENDING theo contract hiện tại, giữ lịch sử và không giả lập trạng thái REWORK. Người từng thực hiện không tự nghiệm thu hoặc yêu cầu làm lại bằng quyền review.
- Từ sự cố đang mở tạo công việc liên kết bằng transaction, key và version. Không tạo thêm khi công việc sửa chữa trước còn mở hoặc chờ nghiệm thu. Maintenance đóng không tự đóng Incident, gỡ room block, đổi tài sản hay chuyển phòng Ready.
- PM có tìm/lọc ẩn/sắp xếp/phân trang, modal tạo/sửa/dừng và drawer lịch sử các lần phát sinh. Giữ mã/loại/chi nhánh, checklist snapshot và lịch sử đã dùng. Ngày giờ SQL được hiểu là UTC trước khi chuyển sang datetime-local, tránh lệch 7 giờ khi chỉ sửa tên.
- Dashboard lấy 12 chỉ số theo branch/domain grants; COMPLETED chờ nghiệm thu vẫn nằm trong tổng bảo trì chưa đóng. Không cộng Incident vào Maintenance. Nhân viên & Ca trực chỉ dùng ứng viên đủ quyền thực thi và dữ liệu ca/timer thật; chưa có ca không được hiển thị là đang trong ca.
- Kế hoạch đến hạn được scheduler hiện có tạo công việc; không chạy bảo trì giả từ UI. Worker local vẫn tắt theo cấu hình đang dùng, không tự bật để phát sinh dữ liệu primary.

## Maintenance Staff

- Bảy nhóm điều hướng cá nhân dùng capability thực tế: tổng quan, bảo trì, sự cố, thiết bị, vật tư/yêu cầu, lịch và thông báo/lịch sử. Giữ navbar, chi nhánh dưới logo, control compact, filter ẩn mặc định, phân trang/sắp xếp server và drawer bên phải. IR và công việc vận hành là tab phụ, không gộp lifecycle với bảo trì.
- Dashboard dùng `insights/personal?maintenance=true`, chỉ tính assignment của người đang đăng nhập. COMPLETED là **Chờ nghiệm thu**; CLOSED có verifiedAt mới tính đã nghiệm thu. Công việc đã đóng được mở lại cần assignment mới; lịch sử cá nhân vẫn giữ lần thực hiện cũ.
- Nhận/start/pause/resume/progress/complete dùng modal và contract Work hiện hữu; minh chứng chỉ ghi khi đang được giao và chưa kết thúc. Hiển thị lý do/kết quả review, lịch sử, ghi chú thực hiện và MR liên kết. Nhãn lịch sử công việc không dùng nhãn bắt đầu/kết thúc ca.
- MR dùng modal tạo/thêm dòng/gửi, drawer chi tiết, key tạo ổn định khi retry và số lượng đề nghị/duyệt/đã cấp/còn thiếu từ API. Không cộng trừ tồn kho khi nhân viên ghi kết quả sửa chữa; không giả lập số lượng đã sử dụng.
- Role thực tế chưa có INCIDENT_CREATE và IR_CREATE: cho xem và thực thi công việc được giao theo INCIDENT_MANAGE/IR_PROCESS, không hiện nút tạo. Điều phối Incident cần INCIDENT_TRIAGE; IR route/assign cần thẩm quyền bộ phận hiện hữu. Không cho nhân viên tự bàn giao/reassign bảo trì hoặc nghiệm thu.
- Decline chưa có transition/API hợp lệ trong CompactDB. Không hiện nút từ chối và không tạo enum/lifecycle mới. Workday vẫn OWN, thiết bị chỉ đọc, inbox chỉ trả thông báo người nhận.

## Owner

- Giữ 12 grants ALL thực tế, role assignment global. Không thêm membership hay quyền ghi để hỗ trợ toàn hệ thống. Giao diện Owner có 7 nhóm được cấp quyền; nhóm audit thứ tám ẩn vì không có AUDIT_VIEW. Mỗi nhóm/tab vẫn kiểm tra grant, không dùng tên role làm authorization backend.
- Bộ chọn phạm vi dưới logo có Tất cả / từng chi nhánh, chỉ đổi truy vấn đọc. Tất cả được gửi bằng branchId vắng mặt, không coi số 0 là ID database. API kiểm tra ALL trên từng permission; ALL của DASHBOARD/REPORT không mở rộng VIEW chỉ có BRANCH. Chọn phạm vi mới tải lại dữ liệu và đưa trang về đầu.
- KPI hiện tại tách khỏi phát sinh/hoàn thành trong khoảng ngày. COMPLETED bảo trì còn chờ nghiệm thu; Incident RESOLVED còn chờ đóng. SLA chưa đóng theo DueAt giữ định nghĩa hiện hành; không cộng Incident vào Maintenance. Mốc thời gian SQL UTC được đổi sang ngày địa phương trước khi gom ngày/tuần/tháng.
- So sánh chi nhánh có cards, bảng, biểu đồ và drawer chỉ đọc. Phòng dùng grid 15 ô, metadata chi nhánh/tầng/loại/sức chứa; occupancy, cleaning, inspection và block được trình bày riêng. Block tương lai không tính là đang hiệu lực. Công việc, tài sản, kho và chứng từ dùng lại workspace/drawer hiện có với phạm vi đọc mở rộng.
- Báo cáo danh sách lọc theo ngày tạo; cột hoàn thành/nghiệm thu là mốc riêng. Phòng/tài sản/tồn/PM là snapshot hiện tại, không giả số dư quá khứ. Chỉ số hoàn thành dashboard lấy CompletedAt trong kỳ độc lập với ngày tạo. Biểu đồ có tên nhánh/đơn vị và phạm vi thời gian; không suy diễn doanh thu/lợi nhuận.
- PM chỉ đọc qua REPORT_VIEW + MAINTENANCE_VIEW, không cần PM_MANAGE. Báo cáo nhân sự chỉ trả tổng hợp theo chi nhánh/bộ phận và các ca thực tế; tỷ lệ có mẫu số, không dùng USER_VIEW/SHIFT_MANAGE/ATTENDANCE_RECORD.
- Chứng từ báo cáo chỉ có link vào domain đã được cấp quyền; không đưa Replenishment vào báo cáo Owner khi thiếu REPLENISHMENT_VIEW. CSV bảng so sánh gồm các chi nhánh trong phạm vi hiện tại; CSV danh sách xuất đúng trang đang hiển thị. UTF-8, escape đúng và vô hiệu hóa formula trong ô văn bản; không xuất dữ liệu khách/credential.
- Lời mời bàn giao cũ không cấp quyền thực thi: Work quyết định bàn giao kiểm tra lại execute grant; Asset acknowledgment cần đường quyền nghiệp vụ tại chi nhánh hoặc quyền quản lý/chuyển tài sản hiện hành. Global ASSET_VIEW thuần giám sát không đủ. Không đổi role_permissions hoặc bypass concurrency/lifecycle.
