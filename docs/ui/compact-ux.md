# Quy ước UX giao diện HotelOps compact

## Quyết định 26/11/2026 — ca tự có hiệu lực

Quyết định này thay thế yêu cầu bấm bắt đầu ca trong các ghi chú lịch sử bên dưới. Sau khi quản lý phân ca, nhân viên đủ điều kiện tự có ca `WORKING` trong khung giờ được phân theo múi giờ chi nhánh. Không còn nút bắt đầu ca ở lịch hoặc chi tiết. Quyền thao tác vẫn phụ thuộc vai trò, chi nhánh, bộ phận, tài khoản và giờ ca; không mở quyền ngoài giờ.

Hệ thống đồng bộ trước các API đã xác thực và mỗi 30 giây qua tác vụ nền. Ghi audit `AUTO_START` với tác nhân hệ thống; thời điểm bắt đầu là lúc hệ thống xác nhận đủ điều kiện, không ghi lùi giờ qua thời gian ngừng dịch vụ hoặc tài khoản không hợp lệ. Hết giờ tự kết thúc; ca đã kết thúc/hủy không tự mở lại. Đã loại bỏ API bắt đầu ca thủ công; lịch chỉ còn kết thúc/hủy theo quyền hiện có.

Quyết định BF-03 ngày 27/11/2026: khi ca buồng phòng tự có hiệu lực, cấp đủ bộ vật tư đầu ca theo cấu hình. Nếu thiếu bất kỳ thành phần nào, không cấp bộ dở dang; ghi nhận thiếu và thông báo để yêu cầu bổ sung qua UC-153. Không tự xuất thêm vật tư tiêu hao từ kho khi nhận từng phòng.

## Quyết định 27/11/2026 — tự động tiêu hao và hoàn trả vật tư

CLEANING/REWORK không còn checklist thực hiện hoặc điều kiện hoàn thành dựa trên checklist. Dữ liệu lịch sử được giữ. Nhân viên gửi hoàn thành với ít nhất một ảnh PNG/JPEG hợp lệ, tối đa 10 MB mỗi ảnh; có thể xem trước, thay hoặc bỏ ảnh chưa gửi và nhập ghi chú. Giám sát tiếp tục xem ảnh và quyết định đạt/không đạt theo luồng kiểm tra/làm lại hiện có.

Quyết định mới nhất: khi nhận CLEANING (hoặc START đồng thời nhận việc), hệ thống trừ đủ vật tư đang giữ theo định mức mỗi khách nhân sức chứa tối đa của loại phòng trong cùng transaction. Không tự xuất thêm từ kho. Thiếu vật tư thì không nhận việc, không ghi dở dang; hiển thị định mức, khả dụng, thiếu và lối yêu cầu bổ sung UC-153 trong ca. Hoàn thành, gửi lại và REWORK không trừ thêm bộ vật tư. Giữ lịch sử tiêu hao hợp lệ, không cộng trừ hồi tố.

Bổ sung giữa ca: nhân viên chủ động gửi yêu cầu đến thủ kho. Thủ kho xác nhận số thực cấp (có thể cấp một phần, để 0 cho mặt hàng chưa giao); thao tác xác nhận đồng thời xuất kho và tăng số dư nhân viên trong một transaction. Không còn bước quản lý vận hành duyệt cho loại yêu cầu này. Yêu cầu, tiếp nhận xử lý và ghi chú không làm tăng vật tư. Hiển thị lượng đề nghị, đã cấp và chưa cấp; phần chưa cấp có thể được yêu cầu tiếp trong ca. Yêu cầu cũ còn mở được chuyển sang kho, giữ nguyên lịch sử đã duyệt. Các yêu cầu bổ sung hàng cho kho vẫn giữ luồng duyệt hiện hành.

Hết giờ hoặc kết thúc ca, tự hoàn trả toàn bộ vật tư còn giữ, dừng phần yêu cầu bổ sung chưa cấp và đóng phân bổ; chạy lại không hoàn trả lần hai. Không có bước kiểm đếm thủ công, đối soát riêng hoặc xác nhận nhận trả của thủ kho.

Công việc vệ sinh đã bắt đầu trong ca, có tiêu hao hợp lệ và được backend cho phép vẫn có thể tiếp tục/gửi ảnh hoàn thành sau ca mà không trừ thêm vật tư. UI chỉ mở ngoại lệ theo capabilities.canContinueCleaning của công việc, giữ nguyên quyền và phạm vi chi nhánh; nhận/bắt đầu việc mới và cấp bổ sung vẫn yêu cầu ca đang hoạt động. Nút tạm dừng tiếp tục ẩn theo UX đã chốt.

Mỗi nhân viên chỉ có một bộ đếm công việc đang chạy. Báo sự cố, kiểm tra của giám sát và làm lại giữ quyền và quy trình hiện có.

## Quyết định popup và báo sự cố — 24/11/2026, cập nhật sau cùng

- Thông báo không cần xác nhận nằm phía trên, có nút × để đóng; thay thế quy ước popup góc dưới/Thu gọn trước đây. Nhiều thông báo xếp riêng theo dialog, không chồng nhau.
- Hộp xác nhận thao tác ở giữa màn hình, Xác nhận màu xanh và Hủy màu đỏ. Màu nút xác nhận này thay thế ngoại lệ màu xác nhận hủy/chuyển phòng trước đây; màu nút mở thao tác giữ nguyên. Hủy hoặc đóng không gửi API; khóa thao tác khi đang xử lý.
- Cảnh báo vượt giờ phân ca dùng hộp xác nhận. Hủy giữ lựa chọn nhưng chưa cho lưu; đổi ngày/nhân viên hoặc phiên số giờ phải xác nhận lại.
- Form tạo báo sự cố bỏ cả Mức ảnh hưởng và Ghi chú theo trả lời trực tiếp của người dùng. Tự chọn mức ảnh hưởng hoạt động đã cấu hình; API không bắt buộc ghi chú kể cả cấu hình cũ RequireNote=true. OM không còn checkbox bắt buộc ghi chú. Các ghi chú xử lý/kiểm tra ở bước sau giữ quy tắc hiện hành.


Xóa ca cấu hình (24/11/2026): nút Xóa ca màu đỏ mở dialog xác nhận, dùng cơ chế ngừng sử dụng hiện có (IsActive=false), giữ lịch sử. Mặc định chỉ hiện ca đang dùng; bật Hiện ca đã xóa / ngừng sử dụng để xem lại. Backend giữ SHIFT_MANAGE, branch, row version, audit và chặn PLANNED/WORKING trước khi ngừng ca. Không xóa phân công hoặc chấm công.

Kho chi nhánh (24/11/2026): hiển thị tên kho hoạt động ở đầu trang; bảng STT, vật tư, tồn cơ sở, đơn vị, tối thiểu, thao tác. Không lặp tên kho/mã vật tư/biến động cuối trong bảng. Tồn dưới tối thiểu tô đỏ kèm chữ để không phụ thuộc riêng màu. OM có ROOM_SUPPLY_CONFIG sửa tối thiểu trong dialog ngay tại tồn kho, kiểm tra version và đồng bộ cấu hình mà giữ phiên bản ca đã mở. Nút xem kho chi nhánh khác chỉ cho chọn chi nhánh có INVENTORY_VIEW trong hồ sơ, không tự mở rộng quyền. OM có trang Lịch sử yêu cầu vật tư riêng với phân loại chứng từ và tìm/lọc. Kho cũ inactive giữ lịch sử, không hiện trong tồn kho hoạt động.

Áp dụng khi bổ sung/sửa trang tại `frontend/src/compact`. Duy trì lựa chọn đã được người dùng chấp nhận; không dùng thay đổi giao diện để thay đổi quyền, phạm vi chi nhánh hoặc trạng thái nghiệp vụ.

## Tiếp nhận tài liệu mới

Quyết định 24/11/2026: trạng thái phòng BLOCKED hiển thị là “Bảo trì”, giữ nguyên mã và quy tắc nghiệp vụ. Nút Hủy đặt phòng (và xác nhận hủy) màu đỏ; Chuyển phòng (và xác nhận chuyển) màu vàng, chữ tương phản. Các alert/notice dùng popup ở góc dưới, tự mở khi nội dung thay đổi, có nút Thu gọn/Mở ra. Giữ nút thử lại, nội dung lỗi và xác nhận vượt giờ; popup trong dialog vẫn thuộc form gốc. Nhiều thông báo xếp riêng trong từng dialog, có cuộn dọc khi thiếu chiều cao, không che nút đóng drawer hoặc tràn ngang trên điện thoại.

Lễ tân (23/11/2026): Công việc & yêu cầu chỉ còn yêu cầu nội bộ, bỏ tab sự cố và nút đổi phạm vi cá nhân. Nhân viên xem yêu cầu do mình tạo hoặc được giao/đề nghị bàn giao cho mình; giữ lịch sử đã hoàn thành/hủy. Backend áp dụng cùng giới hạn cho danh sách, chi tiết, related, báo cáo và tổng quan; quyền BRANCH không tự cấp quyền điều phối. Người có thẩm quyền bộ phận chỉ xem thêm đúng bộ phận, quản lý vẫn cần grant và scope hợp lệ. Không đổi grants. Lễ tân gộp Đang dọn dẹp vào Cần dọn dẹp ở tab, số đếm và nhãn phòng; lifecycle dọn phòng vẫn giữ nguyên. Đặt/hủy đặt/nhận/trả/chuyển phòng chỉ thực hiện khi đã bắt đầu ca cùng chi nhánh, còn trong giờ ca và chưa kết thúc. UI lấy canOperate từ API, cập nhật định kỳ; API kiểm tra lại trong transaction khi ghi.

Quyết định 23/11/2026: checklist dùng bản giấy do người dùng tự quyết định; đề xuất OM cấu hình/gán checklist theo hạng phòng đã hủy. Giám sát ghi Đạt/Không đạt và ghi chú ngay trên công việc chờ kiểm tra, không tạo phiếu thủ công. Không đạt bắt buộc ghi chú, chuyển luồng làm lại hiện có; Đạt kiểm tra độc lập, version, chu kỳ phòng và các lỗi cũ. Lịch sử điện tử cũ được giữ. Thay đổi trigger cho bằng chứng kiểm tra giấy phải áp dụng bằng script được duyệt trước khi dùng Production.

Nhân viên buồng phòng không nhập lý do khi nhận/bắt đầu/gửi hoàn thành, không cập nhật phần trăm tiến độ. Bàn giao chỉ trước lần bắt đầu đầu tiên, kể cả công việc hiện đang tạm dừng cũng không được bàn giao. Workspace buồng phòng ẩn mã kỹ thuật. Tab Đã làm theo ngày lấy ngày gửi hoàn thành theo múi giờ chi nhánh; không dùng ngày tạo và không coi chờ kiểm tra là đã nghiệm thu.

Theo chỉ dẫn ngày 22/11/2026: xem xét tài liệu yêu cầu có chọn lọc, không áp dụng nguyên xi. UX đã chốt trong phiên và tài liệu này tiếp tục được giữ, kể cả khi đề xuất mới khác cách bố trí. Áp dụng tương tự cho các tài liệu tiếp theo. Đối chiếu nghiệp vụ, quyền và mã hiện có; ghi rõ phần áp dụng, phần không áp dụng và phần cần làm rõ. Không tự cấp quyền hoặc thay đổi dữ liệu để đáp ứng một màn hình.

Department Head: nhóm điều hướng riêng chỉ áp dụng khi vai trò hoạt động tại chi nhánh là DEPT_MANAGER; từng menu vẫn phải có quyền tương ứng. Tài khoản đa vai trò giữ các chức năng đã được cấp. Nhân sự phục vụ phân ca chỉ đọc ứng viên theo ca/ngày và lịch tuần, không dùng API `/users`. Phòng/tồn kho là liên kết tham khảo từ Tổng quan. Khả năng duyệt vật tư lấy từ backend, không suy ra chỉ bằng role hoặc MR_APPROVE_L1.

## Bố cục

Lịch cá nhân (23/11/2026): chọn ngày, Ngày trước/Ngày sau/Hôm nay, chỉ hiện ca chưa hủy trong ngày đã chọn; tiêu đề Công việc. Ca đang hoạt động vẫn có nút kết thúc khi xem ngày khác. Lịch quản lý giữ điều hướng tuần. HK Staff không có danh sách phòng riêng; xem phòng và checklist trong công việc được giao. Công việc khác giữ riêng OPERATIONAL, lọc assignedToMe, mô tả rõ là việc vận hành ngoài dọn phòng; không nhập chung lifecycle với CLEANING/REWORK và không thay đổi grants.

Giám sát buồng phòng (23/11/2026): sidebar gộp Phân công dọn phòng, Kiểm tra phòng, Xử lý làm lại vào Công việc với ba tab giữ lifecycle và quyền riêng; liên kết cũ vẫn mở đúng nội dung. Lịch làm việc có mục riêng, dùng Workday OWN hiện có để xem và bắt đầu/kết thúc ca cá nhân. Mọi dashboard có ATTENDANCE_RECORD hiển thị bảng Ca làm của tôi cho tuần/tháng hiện tại, theo chi nhánh đang chọn và múi giờ chi nhánh. Ca đã làm là ca ENDED có đủ giờ vào/ra; giờ đã làm tính từ chênh lệch thực tế, không dùng độ dài ca dự kiến hay timer công việc. Ca WORKING hiển thị riêng, không cộng giờ chưa kết thúc. Không tự thêm ATTENDANCE_RECORD cho vai trò thiếu quyền. Lễ tân có liên kết Tổng quan để xem bảng này mà không cần DASHBOARD_VIEW.

Phân ca Department Head (23/11/2026): chọn Bộ phận trước rồi mới chọn Đối tượng; danh sách đối tượng lấy từ nhân sự/role đang hoạt động của bộ phận tại chi nhánh, đổi bộ phận phải bỏ đối tượng cũ. Backend kiểm tra cùng quan hệ khi lưu cấu hình ca. Phân công có phạm vi ngày, tuần (thứ Hai–Chủ nhật, kể cả qua tháng/năm) hoặc tháng. Cộng giờ ca đã phân chưa hủy, gồm ca đã kết thúc, trên mọi ca của nhân viên; vượt 8 giờ/ngày hoặc 40 giờ/tuần phải hiển thị tổng giờ và xác nhận rõ trước khi lưu. Xác nhận gắn với dữ liệu đã xem, lịch thay đổi phải xác nhận lại; vẫn kiểm tra trùng ca, scope, sức chứa, RowVersion và ghi audit. Đây là ngưỡng cảnh báo theo yêu cầu vận hành, không phải giờ timer công việc. Logo đăng nhập căn giữa thẻ, nền ảnh có lớp tối 55%.

Quyết định bổ sung 23/11/2026 về lễ tân: Sẵn sàng → Đặt phòng (RESERVED, Đã đặt) → Khách đến nhận phòng (OCCUPIED). Hủy đặt hoặc chuyển đặt khi khách chưa ở trả phòng nguồn về Sẵn sàng, không tăng lượt vệ sinh; phòng đích của chuyển đặt vẫn Đã đặt. Chuyển khách đã ở/trả phòng chuyển nguồn sang Cần dọn và tăng lượt vệ sinh. Không có ô lý do khi đặt/hủy đặt/nhận/trả; chuyển phòng và block vẫn giữ lý do. Các hành động mới dùng quyền Reception hiện có, không thêm grants. Đã đặt được hiển thị riêng; không tính vào phòng sẵn sàng để đón khách khác.

Ca WORKING tự kết thúc tại giờ hết ca theo múi giờ chi nhánh; tác vụ nền chạy mỗi 30 giây và Workday kiểm tra lại khi tải. Quyết định 25/11/2026: ca PLANNED chưa bắt đầu được tự đóng khi đến giờ kết thúc + 1 giờ, lưu CANCELLED cùng audit AUTO_CANCEL_EXPIRED; không tạo StartedAt/EndedAt giả. API đánh dấu expiredWithoutStart để lịch cá nhân và lịch quản lý vẫn hiển thị “Đã kết thúc · chưa vào ca”, không tính vào sức chứa và không cho bắt đầu/hủy lại. Phân công hủy thủ công vẫn ẩn khỏi lịch. Hệ thống ghi audit AUTO_END (không giả danh nhân viên); lịch sử cá nhân hiển thị sự kiện này. Chỉ cho bắt đầu trong khoảng giờ của ca; giao diện cá nhân cập nhật mỗi 30 giây khi không mở form/chi tiết.

Quyết định bổ sung ngày 23/11/2026: logo login dùng PNG nền trong suốt; focus ô nhập mảnh, không che chữ và vẫn rõ khi dùng bàn phím. Hồ sơ cá nhân mở từ tên tài khoản trên navbar cho mọi vai trò. Cấu trúc khách sạn dùng cây chi nhánh/tầng/khu/phòng, có form liên hệ chi nhánh. Reception gom chờ dọn/chờ kiểm tra/cần làm lại thành nhãn Cần dọn dẹp ở bảng phòng, tách Đang dọn dẹp; giữ thông tin block, không đổi lifecycle backend. Không hiện tạo sự cố cho Reception. Phân trang dùng chung hiển thị khoảng bản ghi/tổng.

Admin tạo tài khoản không nhập mã nhân viên, chọn chi nhánh ngay trong form; mật khẩu 8–128 ký tự có checklist tiêu chí. Các danh mục có mã được sinh phía server khi tạo. Phân ca Department Head hỗ trợ nhiều người và ngày trong tháng; không mặc định coi ngày lễ là ca nghỉ. OM xem tài sản theo phòng, một thanh Tốt/Hỏng/Khác và thống kê công việc theo ngày/tuần/tháng, mặc định ngày. Mọi nút cấu hình tiếp tục theo permission thực tế.

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

- HK Staff chỉ có HK_STAFF tại chi nhánh dùng 6 nhóm cá nhân (đã bỏ danh sách phòng riêng); không dùng role ID để cấp quyền. Dashboard gọi `insights/personal`, không yêu cầu DASHBOARD_VIEW. Việc dọn/làm lại và công việc khác dùng `assignedToMe=true` để giữ lịch sử đã hoàn thành, không bật xem toàn nhánh. Tìm kiếm/sắp xếp/lọc ngày chạy trước phân trang. Khung chi tiết dùng drawer, các form tiến độ/hoàn thành/ghi chú/minh chứng nằm trong drawer; tạo yêu cầu/sự cố/vật tư dùng modal. IR dùng capability của backend để hiện thao tác điều phối; vật tư chỉ người tạo được sửa/gửi/hủy. Kết quả Inspection là thông tin chỉ đọc trong Cleaning/Rework; không cấp INSPECTION_VIEW/PERFORM cho HK Staff. Lịch cá nhân tiếp tục dùng Workday OWN; chỉ người được giao được ghi checklist/tiến độ và minh chứng công việc.
- Giám sát: mặc định mở Phòng cần tạo việc, gồm phòng đang cần dọn nhưng chưa có công việc housekeeping đang mở; không liệt kê phòng có khách, inactive hoặc đang bị chặn. Tìm phòng trước phân trang 25 dòng. Chọn phòng mở modal tạo yêu cầu, điền sẵn thông tin phòng. Theo quyết định ngày 23/11/2026, chọn nhân viên dọn phòng thay bộ phận; bắt buộc nhân viên HK_STAFF có CLEANING_EXECUTE, tài khoản hoạt động và thuộc chi nhánh. Người tạo có CLEANING_ASSIGN và nhân viên nhận đều phải có ca WORKING đã bắt đầu, chưa kết thúc, đúng ngày và trong giờ ca theo múi giờ chi nhánh. API kiểm tra lại khi lưu; tạo và phân công cùng transaction, không mở thêm bước phân công. Bộ phận lấy từ ca của người nhận. Phân công lại và bàn giao Cleaning cũng kiểm tra ca của người nhận. Phòng đã có việc không xuất hiện lại trong danh sách tạo; nhóm công việc cũ giữ riêng. Lựa chọn phòng được kiểm tra lại bằng version và điều kiện thực tế trong transaction, không dùng bằng chứng vệ sinh cũ sau checkout.
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

## Housekeeping Supervisor

- Giữ 11 grants thực tế; bảy nhóm điều hướng theo permission. Role chỉ chọn workspace trình bày, backend vẫn kiểm tra permission/branch/assignment. Lịch cá nhân dùng màn hình chung và ATTENDANCE_RECORD OWN; không thêm SHIFT_MANAGE/USER_VIEW.
- Tổng quan HK lấy snapshot, workload, chờ kiểm tra và ưu tiên từ API riêng. Phân biệt phòng có khách, mức sẵn sàng vệ sinh và chặn vận hành. KPI thiếu quyền được bỏ qua, không hiển thị số 0 giả.
- Phòng giữ grid 5 × 3 và drawer, bổ sung bảng/tầng/vệ sinh/có khách/chặn; liên kết công việc theo turnover hiện tại. Không tạo check-in/out hay room release/unblock cho giám sát.
- Phân công giữ danh sách phòng cần tạo việc, các hàng đợi và modal giao nhân viên. Phiếu kiểm tra nhận đúng công việc gốc từ liên kết, không giới hạn ở 100 công việc đầu. Chi tiết và lịch sử dùng drawer; hành động dùng modal, bộ lọc mặc định đóng.
- Tái kiểm tra chỉ mở sau lần làm lại được gửi sau kết luận không đạt gần nhất. Tái kiểm tra không đạt cho phép tạo lượt làm lại tiếp theo, giữ toàn bộ phiếu cũ và chặn công việc thực hiện song song. Không phân công lại công việc đã gửi kiểm tra/kết luận. Resolve finding cần cả INSPECTION_PERFORM và DEFECT_RESOLVE, đúng root/branch/turnover và người xác minh độc lập.
- Mẫu checklist đã công bố bị khóa bởi trigger hiện hành. Màn hình cho xem và tạo phiên bản mới cùng mã/loại, không sửa lịch sử hoặc giả lập ngừng mẫu. Khả năng kích hoạt/ngừng mẫu đã công bố còn bị chặn bởi contract database.
- Nhân viên/ca đọc từ ứng viên CLEANING_ASSIGN cùng Workday thực tế; trạng thái tài khoản cạnh tên, ca dựa trên giờ vào/ra, không suy diễn ACTIVE là đang làm. Danh sách ứng viên giữ giới hạn 200 của contract hiện hữu.

## Owner

- Giữ 12 grants ALL thực tế, role assignment global. Không thêm membership hay quyền ghi để hỗ trợ toàn hệ thống. Giao diện Owner có 7 nhóm được cấp quyền; nhóm audit thứ tám ẩn vì không có AUDIT_VIEW. Mỗi nhóm/tab vẫn kiểm tra grant, không dùng tên role làm authorization backend.
- Bộ chọn phạm vi dưới logo có Tất cả / từng chi nhánh, chỉ đổi truy vấn đọc. Tất cả được gửi bằng branchId vắng mặt, không coi số 0 là ID database. API kiểm tra ALL trên từng permission; ALL của DASHBOARD/REPORT không mở rộng VIEW chỉ có BRANCH. Chọn phạm vi mới tải lại dữ liệu và đưa trang về đầu.
- KPI hiện tại tách khỏi phát sinh/hoàn thành trong khoảng ngày. COMPLETED bảo trì còn chờ nghiệm thu; Incident RESOLVED còn chờ đóng. SLA chưa đóng theo DueAt giữ định nghĩa hiện hành; không cộng Incident vào Maintenance. Mốc thời gian SQL UTC được đổi sang ngày địa phương trước khi gom ngày/tuần/tháng.
- So sánh chi nhánh có cards, bảng, biểu đồ và drawer chỉ đọc. Phòng dùng grid 15 ô, metadata chi nhánh/tầng/loại/sức chứa; occupancy, cleaning, inspection và block được trình bày riêng. Block tương lai không tính là đang hiệu lực. Công việc, tài sản, kho và chứng từ dùng lại workspace/drawer hiện có với phạm vi đọc mở rộng.
- Báo cáo danh sách lọc theo ngày tạo; cột hoàn thành/nghiệm thu là mốc riêng. Phòng/tài sản/tồn/PM là snapshot hiện tại, không giả số dư quá khứ. Chỉ số hoàn thành dashboard lấy CompletedAt trong kỳ độc lập với ngày tạo. Biểu đồ có tên nhánh/đơn vị và phạm vi thời gian; không suy diễn doanh thu/lợi nhuận.
- PM chỉ đọc qua REPORT_VIEW + MAINTENANCE_VIEW, không cần PM_MANAGE. Báo cáo nhân sự chỉ trả tổng hợp theo chi nhánh/bộ phận và các ca thực tế; tỷ lệ có mẫu số, không dùng USER_VIEW/SHIFT_MANAGE/ATTENDANCE_RECORD.
- Chứng từ báo cáo chỉ có link vào domain đã được cấp quyền; không đưa Replenishment vào báo cáo Owner khi thiếu REPLENISHMENT_VIEW. CSV bảng so sánh gồm các chi nhánh trong phạm vi hiện tại; CSV danh sách xuất đúng trang đang hiển thị. UTF-8, escape đúng và vô hiệu hóa formula trong ô văn bản; không xuất dữ liệu khách/credential.
- Lời mời bàn giao cũ không cấp quyền thực thi: Work quyết định bàn giao kiểm tra lại execute grant; Asset acknowledgment cần đường quyền nghiệp vụ tại chi nhánh hoặc quyền quản lý/chuyển tài sản hiện hành. Global ASSET_VIEW thuần giám sát không đủ. Không đổi role_permissions hoặc bypass concurrency/lifecycle.

## Quyết định 23/11 — dọn phòng và khắc phục
- Trang `#staff` của Giám sát đổi thành Lịch sử vệ sinh, thay bảng Nhân viên & Ca trực. Lọc theo ngày gửi hoàn thành tại múi giờ chi nhánh, có Ngày trước/Ngày sau/Hôm nay; hiển thị dọn phòng và làm lại, phòng, người hoàn thành, thời điểm và trạng thái. Bấm tên công việc để mở chi tiết. Lịch làm việc vẫn ở trang riêng. Mobile xếp dòng có nhãn, không cuộn ngang.
- Hạn dọn 10–30 phút tính từ lúc phân công, quá hạn ghi audit hệ thống một lần/lượt giao. Nhân viên bắt đầu trực tiếp và báo hoàn thành; không báo sự cố/tải bằng chứng/bàn giao sau nhận. Mobile dùng hàng công việc có thể chạm, không tràn ngang.
- Sự cố phòng gồm hỏng/thiếu đồ đã gán vào phòng. Head cấu hình và duyệt, bảo trì/kho khắc phục, Head gửi giám sát kiểm tra giấy. Kho tiếp nhận trực tiếp phiếu khắc phục đã được Head duyệt; phiếu thường vẫn duyệt độc lập. Kiểm tra đạt mở lượt Cần dọn dẹp lại, không READY; nhiều sự cố giữ block đến khi tất cả đạt.

## Quyết định 23/11 — cấu hình

- Admin dùng công tắc xanh/đỏ tại danh sách, không checkbox trạng thái trong form chỉnh sửa. Bộ phận và hạng phòng có mô tả/trạng thái. Chỉ Admin ẩn khối chọn chi nhánh dưới logo.
- Trưởng bộ phận có trang Cấu hình nội bộ riêng: tab Yêu cầu nội bộ và Báo cáo phòng. Dùng bảng gọn với công tắc xanh/đỏ, form chỉnh sửa ngắn; không dùng modal chứa nhiều thẻ cấu hình lớn hoặc checkbox phóng to.
- Loại yêu cầu nội bộ có tên, mô tả, bộ phận nhận mặc định và bật/tắt theo chi nhánh. Khi tạo yêu cầu, chọn loại điền bộ phận nhận mặc định, người tạo vẫn có thể đổi bộ phận trong phạm vi hợp lệ. Loại đã tắt không nhận yêu cầu mới; yêu cầu cũ và lịch sử giữ nguyên. Chi nhánh chưa cấu hình loại giữ cách tạo trước đó.
- Cấu hình dùng danh mục WORK_CATEGORY hiện có, version và audit; không thêm schema hay grant. Head cần IR_PROCESS/IR_VIEW theo chi nhánh, báo cáo phòng giữ INCIDENT_TRIAGE. Trên điện thoại bảng xếp thành từng dòng có nhãn và không tràn ngang.

## Quyết định 24/11 — dashboard và nhóm yêu cầu

- OM gộp Cấu hình ca, Cấu hình vật tư và Cấu hình nội bộ vào một menu Cấu hình, route `#configuration`, gồm tab Ca làm việc / Vật tư phòng / Yêu cầu nội bộ / Báo cáo phòng. Tab theo grant hiện có; báo cáo phòng cần cả quyền cấu hình và quyền đọc loại báo cáo. Nút Cấu hình ca ở Lịch làm việc đi thẳng `#configuration?tab=shifts`, có Quay lại lịch. Giữ deep-link `#supply-config`, `#internal-settings` và `?tab=room`; tải lại giữ tab, đổi chi nhánh làm mới dữ liệu/form. Nhóm A/B/C vẫn nằm trong tab Vật tư phòng. Admin và các vai trò khác giữ điều hướng hiện có.

- Cấu hình vật tư OM chia ba tab A (Vật tư cấp theo ca), B (Đồ dùng bền), C (Thiết bị sửa chữa bên ngoài), kèm số cấu hình và hướng dẫn riêng. Form tạo kế thừa nhóm đang mở; form sửa vật tư vẫn cho đổi nhóm. Chỉ nhóm A nhập cấp đầu ca/mỗi khách; vật tư lưu kho nhập tồn tối thiểu, tài sản thuộc phòng không nhập định mức kho. Mỗi chi nhánh có đúng một kho hoạt động: hiển thị kho chung, bỏ dropdown chọn kho ở từng cấu hình; API tự xác định kho và từ chối kho khác/chi nhánh có số kho không hợp lệ. Giữ phiên bản, audit và định mức đã chụp của ca đang mở.

- Các dashboard đặt thẻ số liệu trước bảng Ca làm của tôi, rồi đến thông tin chi tiết. Bảng ca chỉ tải khi có ATTENDANCE_RECORD và giữ nguyên cách tính giờ vào/ra thực tế theo tuần/tháng.
- Màu thẻ theo ý nghĩa chỉ số: xanh lá cho sẵn sàng/hoàn thành, vàng cho chờ xử lý, đỏ cho quá hạn/sự cố, xanh dương cho số liệu chung, tím cho thông báo/yêu cầu. Luôn giữ tên và con số để không phụ thuộc riêng vào màu.
- Trạng thái Đang trong ca có nền xanh và chữ trắng; Đã kết thúc có nền đỏ và chữ trắng trong lịch và thông tin ca. Không thay đổi quy tắc bắt đầu/kết thúc ca.
- Riêng Trưởng bộ phận gộp Công việc, Yêu cầu nội bộ, Yêu cầu vật tư vào menu Yêu cầu, với ba tab tương ứng theo grant TASK_VIEW/IR_VIEW/MR_VIEW. Giữ các URL cũ và phạm vi chi nhánh; các vai trò khác giữ điều hướng hiện có.
# Phân ca sau chuyển vai trò và báo sự cố lễ tân (24/11/2026)

- Lịch quản lý mặc định chỉ hiển thị mẫu ca đang dùng. Tùy chọn “Hiện lịch sử ca đã ngừng sử dụng” cho phép xem lại ca cũ; không tạo phân công mới trên ca đã ngừng. Lịch cá nhân vẫn giữ lịch sử.
- Ca đã hết giờ không có nút phân công; API ứng viên và phân công cũng từ chối. Vai trò và membership vẫn phải có hiệu lực suốt ca, không sửa hồi tố quyền để phân vào ca trước thời điểm chuyển vai trò.
- Lễ tân chỉ thấy nút báo sự cố khi đã bắt đầu và còn trong giờ ca lễ tân tại chi nhánh hiện tại. Mở biểu mẫu trực tiếp và gửi API đều kiểm tra lại điều kiện này. Ngoài ca vẫn được xem dữ liệu theo quyền hiện có.


## Điều chỉnh vệ sinh ngày 24/11/2026 (đã xác nhận)

- Nhân viên và giám sát dùng hai tab Công việc hiện tại / Lịch sử vệ sinh; lịch sử ngày/tuần, lọc đóng mặc định, tìm kiếm/trạng thái/sắp xếp và phân trang. HK Staff chỉ đọc lịch sử việc mình hoàn thành ở backend.
- Giám sát không được truy cập trang/API Tình trạng phòng bằng đường quyền HOUSEKEEPING_SUPERVISOR; quyền phân công vệ sinh vẫn cho đọc danh sách phòng cần tạo việc.
- Hàng đợi bắt đầu bằng Tất cả, tách Đã giao khỏi Đang dọn, đặt trên khung chung tìm kiếm và sắp xếp.
- Thời lượng mới chỉ 20/25/30 phút tính từ giao việc. Tạo dọn lại chọn nhân viên và phân công cùng transaction; đóng biểu mẫu giữ trang hiện tại.
- Báo sự cố màu cam chỉ ở bước dọn/kiểm tra đủ điều kiện; API kiểm tra lại work context. Drawer vệ sinh bỏ hủy/minh chứng/ACTIVE, hiển thị người nhận và giờ nhận; báo sự cố cùng hàng với xác nhận hoàn thành.
- Bằng chứng và giới hạn: `plans/reports/implementation-261124-1833-housekeeping-workspace.md`.

## Cấu hình và vệ sinh có sự cố — quyết định bổ sung 24/11/2026

- Loại yêu cầu nội bộ gồm tên, mô tả, danh sách tích vai trò được gửi và danh sách tích người/bộ phận được nhận. Người gửi chọn đúng một nơi nhận trong danh sách được cấu hình; API kiểm tra vai trò, nơi nhận và chi nhánh. Cấu hình cũ chưa có danh sách giữ tương thích.
- Giờ bắt đầu/kết thúc ca dùng cùng bộ chọn 24 giờ; kết thúc cho phép 24:00. API từ chối ca trùng, kể cả hai yêu cầu tạo đồng thời.
- Báo sự cố chỉ hiện các trường phụ thuộc sau khi chọn loại. OM cấu hình tên và bật/tắt hai mức ảnh hưởng nghiệp vụ hiện có (READINESS/SAFETY), cùng yêu cầu ghi chú. Không thay đổi ý nghĩa xử lý của hai mức này.
- Báo sự cố từ công việc quay lại đúng công việc đang làm. Hoàn thành vệ sinh có sự cố giữ phân loại riêng trong lịch sử và vào hàng đợi “Kiểm tra phòng gặp sự cố”, không lẫn kiểm tra vệ sinh thông thường.
- Giám sát xác nhận báo cáo đã liên kết từ công việc: thiếu đồ chuyển kho, hỏng đồ chuyển kỹ thuật. Gửi lại thao tác không tạo yêu cầu khắc phục trùng. Không cho kiểm tra vệ sinh đạt khi còn sự cố chưa xử lý; chỉ xét sẵn sàng sau khắc phục, kiểm tra lại sự cố và kiểm tra vệ sinh cuối cùng.
- Bằng chứng thực tế, phiên bản local và giới hạn kiểm thử: `plans/reports/implementation-261124-1915-internal-config-incident-workflow.md`.

## Vật phẩm trong phòng — bổ sung 24/11/2026

- Đồ dùng bền nhận diện theo cặp ID phòng–ID vật phẩm đã cấp vào phòng; không dùng tên hoặc tồn kho chung để suy ra đồ thuộc phòng. Báo thiếu chỉ chọn đồ dùng bền nhóm B đã được gán.
- Đồ fill nhóm A dùng ID vật phẩm và số lượng bằng sức chứa tối đa phòng nhân định mức mỗi khách. Giữ chỗ khi phân công, trừ vật tư nhân viên khi hoàn thành, gửi lại không trừ lặp. Dọn lại không tự tiêu hao thêm một bộ fill đầy đủ.
- Dữ liệu kiểm thử đã duyệt cho 96 phòng: 8 loại fill, cùng ấm đun/ly uống nước/móc áo/thùng rác mỗi loại 1/phòng; chứng từ ghi rõ dữ liệu kiểm thử. Chi tiết và số lượng kho còn lại: `plans/reports/implementation-261124-1957-room-supplies.md`.

## Hàng đợi và workspace bảo trì — bổ sung 24/11/2026

- “Tất cả” của giám sát buồng phòng gồm công việc hiện tại và phòng cần dọn chưa có việc mở. Tab “Phòng cần dọn” đứng thứ hai; bỏ tab “Chưa phân công”, nhưng công việc chưa giao vẫn nằm trong “Tất cả”. Phòng chưa có việc có nút tạo yêu cầu ngay trong bảng chung; tổng và phân trang tính trên danh sách gộp.
- Menu HK Staff ẩn Công việc khác, Thông báo, Vật tư của tôi, Sự cố phòng và Lịch sử công việc. Vẫn giữ báo sự cố từ công việc đang thực hiện, lịch sử vệ sinh trong trang dọn phòng và các quyền nghiệp vụ đã duyệt.
- Badge Chờ kiểm tra / Kiểm tra phòng gặp sự cố dùng màu vàng, kèm nhãn trạng thái.
- Maintenance Supervisor dùng một trang Công việc bảo trì cho sự cố đã chuyển kỹ thuật và việc bảo trì độc lập. Các lần sửa chữa con được theo dõi trong sự cố gốc, không nhân đôi dòng. Có các nhóm Tất cả hiện tại, Chờ tiếp nhận, Đang xử lý, Chờ kiểm tra, Lịch sử; tìm kiếm và phân trang.
- Tiếp nhận vẫn yêu cầu bắt đầu ca hợp lệ và đúng quyền/chi nhánh/người xử lý. Khi chưa bắt đầu ca, drawer giải thích rõ và dẫn tới Lịch làm việc; không tự ghi nhận chấm công thay nhân viên. URL cũ room-issues/in-progress của giám sát bảo trì mở workspace chung.

## Giao việc chờ nhận và cấp đồ thiếu — bổ sung 24/11/2026

- Không giao thêm dọn mới hoặc dọn lại cho nhân viên đang có assignment ACTIVE chưa AcceptedAt. Kiểm tra tại API trong transaction và loại khỏi danh sách ứng viên; sau khi nhận, nhân viên đủ điều kiện xuất hiện trở lại. Hai yêu cầu giao đồng thời không tạo hai việc chờ nhận.
- HK Staff hiển thị cả ASSIGNED và PENDING_ACCEPTANCE là Chờ nhận. Bỏ nút tạm dừng trong chi tiết vệ sinh và bỏ hướng dẫn tạm dừng; form báo sự cố có khoảng cách riêng giữa ghi chú và nút gửi. Form tạo từ phòng đã chọn không có Chọn phòng khác.
- Giám sát buồng phòng ẩn menu Sự cố & Thông báo, Vật tư nhân viên, Lịch sử vệ sinh; lịch sử vẫn trong Công việc. Thủ kho ẩn Vật tư nhân viên và Sự cố phòng; quyền nghiệp vụ/deep-link vẫn giữ.
- Công việc vệ sinh chỉ chờ kho/kỹ thuật xử lý được ẩn khỏi hàng đợi hiện tại, giữ dữ liệu và lịch sử. Nếu còn sự cố cần giám sát xác nhận hoặc kiểm tra thì vẫn hiện. Sau cấp đủ/sửa xong, việc quay lại hàng đợi kiểm tra.
- Ghi sổ cấp đủ phiếu thiếu đồ tự chuyển sự cố và các yêu cầu con sang kiểm tra trong cùng transaction. Cấp một phần không chuyển. Giám sát kiểm tra bằng nút Kiểm tra sau khắc phục ngay trong drawer Công việc; chưa kiểm tra đạt không tự giải phóng phòng.
- Phiếu cũ đã xuất đủ nhưng chưa ghi bước bàn giao vẫn xuất hiện để giám sát đang trong ca Tiếp nhận kiểm tra phòng. API đối soát lượng cấp thực, hoàn/đảo chứng từ và phiếu mới nhất; chưa cấp đủ thì từ chối. Tiếp nhận không thay cho kết luận kiểm tra.
- Danh mục vật tư dùng toàn chiều rộng khi không có danh mục phụ. Trong workspace Yêu cầu vật tư, APPROVED được trình bày là Chờ duyệt màu vàng theo quyết định người dùng; trạng thái lưu và bằng chứng duyệt không bị viết lại. Địa điểm cấp hiển thị chi nhánh/tầng/phòng từ ReceivingRoomId trong chi tiết và form cấp phát.

## Kiểm tra sau khắc phục và dọn lại — 24/11/2026

- Tiếp nhận giám sát sửa chữa đồng thời bắt đầu việc bảo trì trong transaction, không hỏi ghi chú ở bước nhận. Drawer chuyển sang Kết thúc sửa chữa; số đếm workspace tải lại sau thao tác. Việc cũ chưa bắt đầu vẫn có thao tác bắt đầu tương thích.
- Vệ sinh có sự cố đã khắc phục/đã cấp đủ hiện trong Chờ kiểm tra với nhãn Chờ kiểm tra - sc màu cam. Dọn lại đã gửi kiểm tra hiện cùng tab, nhãn Chờ kiểm tra - dl màu nâu. Đây là phân loại hiển thị, không thêm trạng thái database.
- PASS sự cố cuối cùng của lần vệ sinh hiện tại đồng thời ghi kết quả kiểm tra vệ sinh, kết thúc dọn lại nếu đủ điều kiện và đánh giá READY. Không yêu cầu người dùng kiểm tra lần nữa. Giữ kiểm tra độc lập, chu kỳ phòng, việc dọn lại thực sự đã gửi, sự cố khác và các chặn an toàn/chiếm dụng.
- PASS không cần ghi chú hay checkbox xác nhận lỗi. Các finding được giải quyết bằng bằng chứng PASS và lần dọn lại đã hoàn tất; hệ thống ghi lý do chuẩn nếu không nhập ghi chú. FAIL cần ghi chú không rỗng, tối đa 2.000 ký tự, không có mức tối thiểu 10.
- Form báo sự cố bỏ bộ chọn Mức ảnh hưởng; ưu tiên READINESS đang bật, nếu cấu hình chỉ bật mức khác thì dùng mức đó. Giữ mô tả Ghi chú và cấu hình OM. Panel dùng tên Sự cố đã phát hiện.
- Audit phòng 104 (room 416) cho thấy vệ sinh 652/rework 663 đã COMPLETED, inspection 670 PASSED, nhưng incident 653 OPEN làm readiness BLOCKED. Quá hạn dọn chỉ ghi audit/thông báo, không tạo incident hoặc đổi phòng thành bảo trì. Không tự đóng sự cố chưa được xác minh để ép READY.


## Quyết định 25/11 — cấp vật tư khi nhận công việc (đã được thay thế ngày 27/11)

- Bắt đầu ca không cấp vật tư đầu ca. Khi nhân viên nhận công việc dọn phòng, cấp phần cần thêm để đủ định mức theo sức chứa khách tối đa của phòng, tính đến vật tư khả dụng đang giữ để tránh cấp trùng.
- Cấu hình nhóm A hiển thị định mức mỗi khách và tồn tối thiểu; bỏ trường cấp đầu ca khỏi biểu mẫu. Trường `initialQuantity` cũ được bảo toàn trong hợp đồng API để tương thích dữ liệu, không phải chỉ dẫn cấp phát mới.
- Kiểm đếm vật tư trong phòng vẫn giải phóng phần giữ chỗ dư; tiêu hao chỉ ghi nhận số thực tế bổ sung. Lịch sử cấp phát cũ không bị đổi tên hoặc sửa số lượng.

## Note 25_11 — cập nhật 26/11

- Quyết định mới thay thế luồng bảo trì cũ ở trên: Trưởng đội phân công; Maintenance Staff trong ca ghi kết quả sửa được/không sửa được. Sửa được chuyển HK kiểm tra, không thêm bước trưởng đội nghiệm thu. Không sửa được giữ sự cố, trưởng đội đề nghị thay thế và OM duyệt; lần thay thế là công việc liên kết mới, không ghi đè lần sửa thất bại.
- Kiểm đếm vật tư dùng accordion đóng mặc định, nhập số có kiểm tổng và giới hạn theo phần thiếu; giữ dữ liệu nhập khi API lỗi. Chỉ bỏ checklist khi công việc vệ sinh không gắn mẫu; bảo trì vẫn cần checklist.
- Vật tư tiêu hao bổ sung đi qua OM duyệt rồi kho cấp. OM không xử lý yêu cầu nội bộ thông thường, không còn trang công việc OPERATIONAL.
- Checklist cấu hình theo quyền; Owner xem tổng hợp; nhân sự OM theo chi nhánh; cây tài sản giữ cả tài sản chưa bố trí. Thông báo thụ động tự đóng 5 giây, xác nhận không tự đóng.
- Thao tác nghiệp vụ cần ca thực, đúng vai trò/quyền/chi nhánh và thời gian. Giao diện khóa lệnh, API kiểm lại độc lập; không khóa đọc dữ liệu hoặc quản trị OM/Owner/Admin.

## Quyết định 27/11 — ca buồng phòng, vật tư và sự cố

Các quy tắc dưới đây thay thế phần cấp vật tư, refill và kết thúc ca cũ khi có mâu thuẫn.

Luồng báo thiếu mới phân biệt đồ thay thế đã được đặt/thu tại công việc và tài sản đăng ký trong phòng. Giám sát độc lập xác minh rồi mới ghi giảm đồ cũ không thu hồi được, không trừ thêm đồ sạch. Khi báo cáo còn chờ, phần đồ bẩn liên quan chưa được trả/giặt; bộ ca sau chờ xử lý phần cũ. Công việc lịch sử chưa có chứng cứ đặt/thu không được tự tạo số đồ bẩn để báo thiếu. Báo mất/hỏng cũ chưa được xử lý đủ chặn tự đặt bộ mới vào phòng, giữ nguyên vật tư nhân viên.

Tài sản thiếu được chuyển OM sau xác minh. Duyệt mua chưa đóng báo cáo. OM ghi nhận tài sản thay thế đã đăng ký và bố trí thật, đúng loại/phòng/chi nhánh, đủ điều kiện sử dụng; tài sản mất phải được cập nhật rời phòng. Giám sát kiểm tra đạt mới đóng và đánh giá lại readiness; không đạt thì OM bổ sung lại. Chặn SAFETY vẫn cần xử lý riêng theo quy tắc hiện hữu.

- Ca tự có hiệu lực theo phân ca. HK Staff không phải bấm bắt đầu và không tự bị đóng ca khi hết giờ. Sau giờ chỉ tiếp tục công việc đã bắt đầu hợp lệ, không nhận/bắt đầu việc mới hoặc refill thường. Không xin duyệt overtime; hệ thống gửi thông báo OM và giám sát khi hết giờ mà còn công việc.
- Nhân viên kết thúc ca thủ công sau khi hoàn thành hoặc được giám sát tiếp nhận bàn giao; lưu giờ kết thúc thực tế. Đề nghị bàn giao có lý do, giám sát tiếp nhận rồi phân lại hoặc để chờ, không giả hoàn thành phòng.
- Bộ đầu ca gồm tiêu hao và reusable sạch theo cấu hình snapshot: đủ toàn bộ mới xuất, thiếu thì không xuất bộ dở dang. Yêu cầu bổ sung do kho xác nhận: xuất đủ chính yêu cầu hoặc từ chối. Các yêu cầu refill không phải quyền tự lấy hàng; từng yêu cầu cần kho xác nhận. Chỉ nhận phòng khi đủ vật tư cần thiết.
- Tiêu hao được ghi một lần khi nhận việc theo sức chứa tối đa và định mức. Reusable chuyển đồ sạch vào phòng và thu đồ cũ thực có về nhân viên. Đồ mất/hỏng đã báo sự cố không được tự biến thành đồ bẩn thu hồi.
- Sự cố reusable chuyển kho cấp đủ đồ khắc phục hoặc từ chối; cấp lại sau giờ được gắn với công việc đã bắt đầu, không mở refill thường. Kho ghi nhận lượng hỏng thực nhận; mất đồ không có hoàn trả giả. Giám sát kiểm tra lại; phòng còn chặn đến khi đủ điều kiện readiness. Vệ sinh hoàn thành và việc khắc phục của kho có vòng đời riêng.
- Sau kết thúc ca hợp lệ, hệ thống lên lịch hoàn trả sau 5 phút, không cộng kho trước hạn. Tác vụ đồng bộ thực hiện khi đến hạn hoặc lần chạy tiếp theo nếu dịch vụ bị ngắt. Đồ sạch về khả dụng, bẩn/hỏng giữ trạng thái riêng. Bộ ca tiếp theo chờ phần cũ trả xong. Thời gian chờ 5 phút là quy tắc ghi nhận tự động đã được chọn, không phải bằng chứng kiểm đếm vật lý.
- Drawer vật tư hiển thị lượng tiêu hao và số dư reusable sạch/bẩn/hỏng, thời hạn trả. Lịch sử cũ giữ nguyên; không viết lại phiếu partial thành full. Mất vật tư vẫn cần xử lý sai lệch sổ sách theo thẩm quyền, không tự ghi giảm tài sản.

### Điều chỉnh màn hình công việc ngày 27/11, 12:22

Chi tiết CLEANING/REWORK bỏ bảng định mức/tiêu hao và bảng luân chuyển reusable. Dữ liệu vật tư và hạch toán backend vẫn giữ. Khi đang thực hiện và có quyền hoàn thành, nút **Thêm ảnh bằng chứng** mở cùng biểu mẫu báo cáo ảnh hoàn thành: chọn nhiều PNG/JPEG, xem trước, thay/xóa trước khi gửi, tối đa 10 MB mỗi ảnh. Chọn ảnh chưa ghi hoàn thành; chỉ xác nhận gửi báo cáo mới tải ảnh và chuyển bước kiểm tra. Lịch sử ảnh vẫn hiển thị; ngoài giờ giữ điều kiện tiếp tục công việc hiện hữu.

### Điều chỉnh vận hành ngày 27/11, 12:57

- CLEANING/REWORK chỉ có **Đề nghị bàn giao** qua giám sát, trước khi nhận hoặc bắt đầu. Đã nhận việc không được bàn giao. Đang chờ giám sát tiếp nhận thì khóa nhận/bắt đầu ở cả UI và API. Người được phân công lại có quyền đề nghị trước nhận theo cùng quy tắc; không giới hạn công việc chỉ được bàn giao một lần suốt vòng đời.
- Màn lịch, biểu tượng lịch tuần và liên kết lịch cá nhân không còn trong giao diện nhân viên. OM giữ quản lý phân ca. HK Staff dùng **Kết thúc ca** trên thanh đầu trang; vẫn kiểm tra việc đang mở, ghi giờ thực tế và hoàn trả sau 5 phút.
- Yêu cầu vật tư dùng danh sách chọn gọn và ô số lượng theo từng dòng. Thủ kho tiếp nhận rồi cấp đủ/từ chối trong hộp giữa màn hình; giữ kiểm tra quyền, ca và đủ tồn kho. Chi tiết sự cố mở quy trình tại trang yêu cầu.
- Trưởng đội bảo trì mặc định xem **Cần tôi xử lý**, có các hàng đợi và nhãn trạng thái màu kèm chữ. Bỏ cấu hình checklist của vai trò này; các vai trò có quyền độc lập khác vẫn giữ quyền của mình. Không thay bước phân công kỹ thuật viên, khắc phục và kiểm tra phòng.
- Toast thông tin xanh dương, thành công xanh lá, cảnh báo vàng và lỗi đỏ. Thông báo có tin mới là thông tin, không phải lỗi.

### Điều chỉnh yêu cầu và báo thiếu ngày 27/11, 13:32

- Thủ kho mở chứng từ yêu cầu vật tư tại **Yêu cầu nội bộ → Yêu cầu vật tư**. Bỏ mục điều hướng trùng; URL `#fulfillment` và liên kết chứng từ cũ giữ ID khi chuyển sang tab. Yêu cầu bổ sung giữa ca vẫn được xử lý trong Hộp yêu cầu, vì có vòng đời người nhận xử lý riêng.
- Các hàng đợi dùng nhãn **Tất cả**, **Đã gửi**, **Cần xử lý**, **Đang xử lý**, **Hoàn thành**, **Từ chối**. Đã gửi lọc theo người tạo, không phải trạng thái; một yêu cầu do mình gửi rồi hoàn thành có thể xuất hiện ở cả hai hàng đợi. Hoàn thành chỉ gồm yêu cầu đã kết thúc xử lý trong quyền xem.
- **Nhận xử lý** nhận trách nhiệm, không đổi tồn kho. **Xác nhận cấp phát** ghi xuất đủ yêu cầu và cộng vật tư đang giữ của nhân viên. Phần giải thích hiện ngay trong chi tiết để tránh hiểu hai nút là một thao tác.
- Thông báo dùng **Chi tiết**, mở đúng loại công việc, chứng từ hoặc đề xuất thay tài sản. Chi tiết công việc chia hai hàng: báo sự cố/ảnh bằng chứng ở hàng hỗ trợ, thực hiện/hoàn thành ở hàng quyết định. Màu kèm nhãn chữ; không dùng màu thay cho ý nghĩa hành động.
- Báo thiếu có hai nhóm chọn nhiều dòng: **Vật tư thay thế** và **Tài sản trong phòng**. Số lượng nguyên dương; tài sản riêng lẻ có số lượng một. Gửi theo lô nguyên tử, giữ nội dung và khóa gửi khi kết quả mạng chưa rõ để không tạo báo cáo trùng.
- Với vật tư thay thế, đồ sạch được ghi nhận một lần khi nhận công việc theo định mức sức chứa. Báo thiếu không xuất thêm đồ sạch. Giám sát độc lập xác minh trước khi giảm số đồ cũ không thu hồi được, truy theo lần thu đồ của công việc. Không lấy số dư của công việc/ca khác; phần đồ cũ đang chờ xác minh không được giặt hoặc tự trả thành hàng thực có.
- Dữ liệu cũ chưa ghi nhận thu đồ không tự được tạo thành đồ bẩn. Biểu mẫu giải thích khi không có số lượng đủ căn cứ báo thiếu; không suy đoán tồn phòng hoặc viết lại lịch sử để làm xuất hiện số liệu.
- Tài sản thiếu được giám sát xác minh rồi chuyển OM xử lý mua bổ sung. Duyệt mua không tự ghi nhận đã nhận tài sản hoặc đưa phòng về sẵn sàng. Việc ghi nhận tài sản thực tế và kiểm tra độc lập phải được hoàn tất trước khi đóng chặn sự cố.
## Bảo trì và lịch sử cấp vật tư

- Trong **Nhập / Xuất / Chuyển kho**, chọn loại chứng từ **Cấp vật tư** trong bảng chung để xem bộ đầu ca, bổ sung và vật tư tái sử dụng đã cấp thực tế. Không còn danh sách lịch sử riêng phía trên. Chi tiết có người nhận, số lượng, đơn vị, kho và thời điểm; dùng chung tìm kiếm, lọc ngày và phân trang. Nhóm theo chứng từ nguồn hoặc mã thao tác khi chưa có chứng từ, người nhận và kho; giữ đầy đủ dòng lịch sử. Đây là màn đọc, không tạo thêm giao dịch xuất.
- Trưởng đội chỉ phân công kỹ thuật viên đang trong đúng ca bảo trì, cùng chi nhánh và có quyền thực hiện. Danh sách ưu tiên người không có công việc đang chạy, sau đó ít việc mở hơn; API kiểm tra lại điều kiện khi gửi phân công.
- Nhân viên bảo trì được đề nghị bàn giao trước khi nhận việc. Đang chờ bàn giao thì không nhận hoặc bắt đầu; đã nhận thì không còn nút đề nghị bàn giao. Trưởng đội tiếp nhận để phân lại. Bấm **Nhận việc** không hỏi lý do và đồng thời bắt đầu thực hiện; không có bước Bắt đầu riêng. Dữ liệu đã nhận từ phiên bản cũ còn chờ bắt đầu có thao tác tiếp tục.
- Khi thực hiện bảo trì, chỉ có một nút **Báo cáo kết quả**. Popup chọn **Sửa thành công** (ảnh bắt buộc, không yêu cầu lý do) hoặc **Không thể sửa chữa — Cần mua mới** (lý do và ảnh bắt buộc). Xác nhận gửi báo cáo sẽ hoàn thành lần sửa chữa. Không yêu cầu checklist trong luồng này; lịch sử cũ được giữ. Sửa thành công được ghi nhận trực tiếp, không chờ trưởng đội duyệt hoàn thành. Kiểm tra phòng độc lập vẫn giữ nguyên, không tự chuyển phòng về sẵn sàng.
- Không sửa được và cần thay mới: nhân viên gửi ảnh/kết luận; trưởng đội xác nhận đề xuất thay mới và chuyển OM xử lý mua. Phê duyệt mua không thay thế bước nhận/lắp tài sản thực tế.
- OM đồng ý thay mới không nhập lý do; lý do chỉ bắt buộc khi từ chối. Sau duyệt, mở **Tài sản**, bấm **Thay thế** ngay trên dòng đồ vật và xác nhận. Không chọn hay tạo thêm tài sản khác; giữ hồ sơ và vị trí hiện tại, chuyển tình trạng về Tốt. Hệ thống ghi lịch sử thay, thông báo sự cố đã xử lý và tạo yêu cầu giám sát buồng phòng kiểm tra lại. Chưa tự giải phóng phòng trước kiểm tra. Nhân viên bảo trì vẫn đọc được kết quả công việc mình đã thực hiện sau khi kết thúc phân công; danh sách và ảnh không hiển thị mã kỹ thuật tự sinh.
- Loại bỏ hỗ trợ phân tích sự cố và thao tác xác minh nguyên nhân. Giữ dữ liệu lịch sử đã ghi; không xóa lịch sử khi bỏ tính năng.

## Báo hỏng và thiếu đồ trong phòng

Loại hỏng đồ chỉ nhận tài sản thuộc phòng, không nhận khăn/ga hoặc vật tư thay thế. Vật tư thay thế chỉ được chọn trong loại mất/thiếu đồ; giám sát xác minh trước khi ghi giảm đồ cũ. Giao diện và API cùng kiểm tra quy tắc này, kể cả gửi nhiều dòng. Báo cáo sai được hủy có lưu lịch sử; hủy một báo cáo không giải phóng các sự cố tài sản khác còn đang xử lý trong phòng.

## Kho và cấu hình yêu cầu nội bộ

Màn phân công bảo trì hiển thị tên công việc, vị trí/phòng hiện có và người nhận; công việc chưa gắn phòng phải nói rõ, không tự đoán phòng. Danh sách nhân viên vẫn lấy người trong ca, ưu tiên người rảnh. Trước khi xác nhận báo cáo bảo trì, không hiển thị các khối lịch sử/kết quả/ảnh rỗng; chỉ gửi ảnh trong popup Báo cáo kết quả. Sau xác nhận, mục **Kết quả xử lý** đọc trạng thái và sự kiện báo cáo đã lưu, hiển thị sửa thành công hoặc không thể sửa chữa/cần mua mới, thời điểm, ảnh và lịch sử. Không dùng lịch sử nghiệm thu cũ thay cho kết quả báo cáo.

- Thủ kho không còn menu Danh mục vật tư; URL trực tiếp không mở màn này. Các bộ chọn vật tư phục vụ nghiệp vụ kho vẫn sử dụng danh mục hoạt động. OM giữ chức năng quản lý theo quyền hiện có.
- Bảng tồn và cảnh báo hiện tại chỉ dùng vật tư hoạt động. Vật tư tạm ngừng vẫn giữ chứng từ và sổ biến động để tra cứu lịch sử. Tồn sổ có thể khác lượng đủ điều kiện cấp; giao diện giải thích khi chưa phân loại, chưa xác định chính sách hạn, chưa đối soát lô hoặc hàng bị hạn chế/giữ chỗ.
- OM vào **Cấu hình → Yêu cầu nội bộ** để cấu hình loại yêu cầu thông thường: vai trò được gửi, bộ phận được gửi (nếu giới hạn), nơi nhận theo vai trò hoặc bộ phận/người cụ thể, hướng dẫn nội dung và mô tả bắt buộc. Vai trò và bộ phận gửi được kiểm tra đồng thời; API kiểm lại membership hiện hành, chi nhánh, người nhận và nội dung. Cấu hình cũ giữ mặc định mô tả không bắt buộc.
- Form tạo hiển thị loại yêu cầu, phạm vi gửi, nơi nhận và nội dung cần có. Yêu cầu vật tư có nút riêng; bổ sung hàng cho kho dùng đúng tiêu đề và nêu OM duyệt trước khi kho ghi nhận hàng thực nhận.
- Luồng bổ sung vật tư giữa ca và bổ sung hàng cho kho có mô tả riêng trong cấu hình OM. Đây là tuyến nghiệp vụ cố định, không bị thay đổi bằng cấu hình loại yêu cầu thông thường; giữ nguyên kiểm tra ca, quyền và cấp đủ hoặc từ chối.

## Kho chi nhánh khác và nhập hàng sau phê duyệt

- OM mở kho chi nhánh khác bằng bảng toàn màn hình có STT, vật tư, tồn cơ sở/có thể cấp, đơn vị, tối thiểu, tìm kiếm, lọc và phân trang. Chỉ xem và đề nghị điều chuyển; không có sửa định mức, biến động chi tiết hoặc thao tác ghi kho ngoài chi nhánh. Hai liên kết duyệt vật tư trên màn tồn kho đã bỏ.
- OM không cần phân ca. Đề nghị điều chuyển kiểm tra quyền tại chi nhánh nhận; duyệt/từ chối kiểm tra chi nhánh cấp. Không mở quyền quản lý chi nhánh khác, không miễn điều kiện ca cho thủ kho.
- Nhập hàng đã duyệt hỗ trợ đồng thời hàng ASSET, REUSABLE và CONSUMABLE. ASSET ghi sổ số lượng, không tạo lô tiêu hao hoặc tự sinh hồ sơ tài sản/số sê-ri. Vật tư tiêu hao chưa có thông tin lô/hạn vẫn theo quy tắc cách ly hiện hữu. Nhập từng phần, giới hạn số còn được nhận và chống gửi trùng vẫn giữ.
Màn Tài sản của OM dựng cây từ toàn bộ danh mục tầng/phòng trong chi nhánh, không chỉ các vị trí đã có tài sản. Tầng/phòng chưa có tài sản vẫn hiển thị với số lượng 0 và thông báo trống. Khi tìm kiếm/lọc tình trạng, chỉ hiển thị nhánh có tài sản phù hợp; số liệu tài sản không bao gồm các vị trí trống.

