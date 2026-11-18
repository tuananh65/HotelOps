# KIỂM TRA 6 LUỒNG — LÀM THEO TỪNG BƯỚC

Mở https://localhost:5173/login. Làm từ bước 1 đến bước 35. Mỗi dòng ghi rõ tài khoản, thao tác và kết quả cần thấy. Khi đổi tài khoản, đăng xuất rồi đăng nhập lại hoặc dùng hồ sơ trình duyệt riêng.

## Chuẩn bị một lần

- Chọn hai phòng cùng chi nhánh: **Phòng A = …**, **Phòng B = …**. A đủ điều kiện tạo việc dọn mới; B dùng báo sự cố. Không dùng phòng đang có việc trùng.
- Ghi **ngày chạy = …**, **ca phù hợp = …**. Ca phải được lập trước giờ bắt đầu và cho phép nhân viên bắt đầu tại thời điểm chạy.
- Chọn **xà phòng = …**, **đơn vị = …**, **kho = …**; tồn khả dụng ít nhất 6 đơn vị. Không có xà phòng thì dùng một vật tư buồng phòng có sẵn và giữ cùng mặt hàng suốt bài.
- Chốt **TK-CA = …**: tài khoản có chức năng Lập kế hoạch ca. Kiểm tra `hung.nguyen` trước; nếu không có quyền, chọn tài khoản đã được cấp. TK-CA chỉ là chỗ ghi tên đăng nhập, không phải tài khoản có sẵn.
- Chốt **TK-DUYỆT = …**: tên đăng nhập của người xuất hiện trong danh sách người duyệt hợp lệ khi `lan.dang` tạo yêu cầu. Không mặc định `ha.pham` hoặc `mai.tran` được duyệt. TK-DUYỆT cũng chỉ là chỗ ghi tên đăng nhập.
- Xác nhận `mai.tran` có thể tạo/phân công vệ sinh và được giao kiểm tra; `lan.dang` được dọn và tạo yêu cầu; `minh.le` được phân loại/tiếp nhận/phân công; `thinh.ngo` được thực hiện bảo trì; `tung.vo` được cấp phát. Đây là tài khoản tham khảo từ hướng dẫn hiện có, chưa xác minh lại phiên đăng nhập thật trong lượt viết này.
- Nếu chưa chốt được TK-CA hoặc TK-DUYỆT, hoàn tất chuẩn bị trước. Không cần thử lần lượt nhiều tài khoản khi đang chạy bài.

**Ghi mã sau mỗi lần tạo:** Công việc dọn = … ; Yêu cầu vật tư = … ; Kiểm tra = … ; Làm lại = … ; Sự cố = … ; Công việc bảo trì = …

## Luồng 6 — Mở ca trước khi làm việc

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 1 | TK-CA đã chốt | Mở **Lập kế hoạch ca**. Chọn chi nhánh, ngày và ca. Thêm `lan.dang` và `thinh.ngo` vào đúng bộ phận. | Có hai phân công trong bản nháp. Nếu người kiểm tra cần vào ca, lập thêm ca cho người đó. |
| 2 | TK-CA | Bấm **Xem trước phân công**. Sửa lỗi nếu có. Bấm **Xem lại lưu → Xác nhận lưu kế hoạch**. Tải lại danh sách phân ca. | Lịch đã lưu đúng người, ngày và ca; không còn xung đột. |
| 3 | `lan.dang` | Mở **Bắt đầu / kết thúc ca**, chọn ca vừa được phân. Xem lại và xác nhận bắt đầu. | Ngày làm việc của Lan đang mở. |
| 4 | `thinh.ngo` | Làm giống bước 3 bằng tài khoản Thịnh. | Ngày làm việc của Thịnh đang mở. Chưa kết thúc ca. |

## Luồng 1 — Xem phòng và chuyển sang công việc

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 5 | `le.truong` | Mở **Trạng thái phòng**, chọn chi nhánh, tìm Phòng A rồi Phòng B. Ghi trạng thái và lý do phòng chưa sẵn sàng. | Đúng hai phòng đã chọn, không nhầm chi nhánh. |
| 6 | `le.truong` → `mai.tran` | Tại Phòng A, xem **Hành động tiếp theo**. Nếu có quyền, mở **Tạo công việc vệ sinh**. Nếu không, đổi sang Mai và mở đúng Phòng A để tạo việc ở bước 7. | Đi tới nghiệp vụ dọn đúng phòng. Không tự đổi phòng thành sẵn sàng. |

## Luồng 2 — Tạo, giao và bắt đầu dọn phòng

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 7 | `mai.tran` | Mở **Công việc buồng phòng → Tạo công việc** hoặc dùng liên kết từ Phòng A. Chọn Phòng A, tải cấu hình, chọn loại vệ sinh, ưu tiên và hạn hợp lệ. Nhập hướng dẫn: **Lau gương, vệ sinh nhà tắm, bổ sung xà phòng để đón khách chiều**. Lưu và ghi mã việc. | Có công việc mới đúng Phòng A. Nếu đã có việc đang mở, không tạo trùng. |
| 8 | `mai.tran` | Mở **Phân công vệ sinh**, chọn việc vừa tạo và `lan.dang`. Bấm **Kiểm tra bản nháp → Lưu phân công**. | Hồ sơ đã lưu ghi Lan là người thực hiện. |
| 9 | `lan.dang` | Mở **Công việc buồng phòng**, chọn việc được giao ở Phòng A. Bấm bắt đầu và xác nhận nếu được yêu cầu. | Công việc đang thực hiện, đã bắt đầu ghi thời gian. |
| 10 | `lan.dang` | Giữ mã công việc, chuyển sang **Tạo yêu cầu vật tư** để làm bước 11. Chưa hoàn tất việc dọn. | Chuẩn bị xin 6 đơn vị xà phòng. |

## Luồng 5 — Xin, duyệt và cấp vật tư

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 11 | `lan.dang` | Mở **Tạo yêu cầu vật tư**. Chọn đúng chi nhánh, bộ phận, kho, TK-DUYỆT. Chọn xà phòng, đơn vị đã chuẩn bị, số lượng **6**. Mục đích: **Phục vụ dọn Phòng A, công việc …**. Lưu nháp, ghi mã phiếu. | Phiếu nháp có 6 đơn vị và đúng người duyệt. Chỉ coi đã liên kết công việc khi hồ sơ thực sự có liên kết. |
| 12 | `lan.dang` | Mở phiếu vừa tạo, kiểm tra thông tin rồi **Gửi duyệt**; xác nhận khi được hỏi. | Phiếu đang chờ duyệt. |
| 13 | TK-DUYỆT đã chốt | Mở **Theo dõi yêu cầu vật tư**, tìm đúng mã phiếu. Xem xét, duyệt **6** đơn vị và xác nhận. | Đã duyệt 6, đã cấp 0. Chưa trừ tồn vì mới duyệt. |
| 14 | `tung.vo` | Mở cùng phiếu đã duyệt. Kiểm tra tồn tại đúng kho. Chọn cấp một phần, nhập **4**, nhập tên người nhận **Đặng Thị Lan**, xem lại và xác nhận cấp. | Đã cấp 4, còn được cấp 2; có chứng từ cấp phát. |
| 15 | `tung.vo` | Tải lại cùng phiếu, cấp tiếp **2** đơn vị và xác nhận. Không tạo phiếu mới. | Tổng đã cấp 6, còn lại 0; có hai đợt cấp. Nếu không có giao dịch khác, tồn giảm tổng cộng 6 đơn vị cơ sở. |

## Hoàn tất Luồng 2 — Nộp kết quả dọn phòng

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 16 | `lan.dang` | Quay lại công việc dọn Phòng A. Lưu tiến độ, các mục bắt buộc và bằng chứng theo màn hình. | Tiến độ đã lưu. Trong tình huống thử, gương còn một vệt bẩn để kiểm tra ở bước 19. |
| 17 | `lan.dang` | Bấm **Hoàn tất dọn phòng**, xem lại và xác nhận. | Công việc chờ kiểm tra. Chưa kết luận phòng sẵn sàng. |

## Luồng 3 — Kiểm tra, làm lại và kiểm tra lại

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 18 | `mai.tran` | Từ công việc Phòng A, mở phân công kiểm tra. Chọn người kiểm tra là Mai nếu danh sách cho phép, chọn bộ tiêu chí hợp lệ rồi lưu. | Có hồ sơ kiểm tra gắn đúng công việc. Nếu chọn người khác, dùng tài khoản người đó ở các bước kiểm tra sau. |
| 19 | `mai.tran` — người được giao kiểm tra | Mở kiểm tra được giao và bắt đầu. Chọn tiêu chí có thật về gương/nhà tắm, ghi **Không đạt**. Nhập lỗi **Gương nhà tắm còn vệt bẩn ở góc phải**. Ghi các mục còn lại, lưu kết quả và bằng chứng. | Có lỗi gắn với tiêu chí, không chỉ chọn trên màn hình mà chưa lưu. |
| 20 | `mai.tran` | Hoàn tất phần kiểm tra, kết luận không đạt với lý do **Gương còn vệt bẩn, cần lau sạch trước khi phục vụ khách**. Xác nhận. Mở yêu cầu làm lại nếu chưa được tạo, chọn đúng lỗi và giao Lan. | Có yêu cầu làm lại liên kết lần kiểm tra này; không tạo hai yêu cầu trùng. |
| 21 | `lan.dang` | Mở **Xử lý công việc làm lại**, chọn đúng việc. Bắt đầu/tiếp nhận theo nút được phép, ghi **Đã lau sạch gương**, thêm ảnh theo yêu cầu rồi nộp kết quả. | Đã nộp làm lại; chưa phải kiểm tra lại đạt. |
| 22 | `mai.tran` | Mở hồ sơ gốc, phân công kiểm tra lại cho người hợp lệ. Người được giao mở lần kiểm tra lại, kiểm tra lỗi cũ và các mục bắt buộc, lưu kết quả, kết luận đạt. | Có chuỗi: không đạt → làm lại → kiểm tra lại đạt. Lần kiểm tra đầu vẫn còn trong lịch sử. |

## Luồng 4 — Báo sự cố và xử lý bảo trì

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 23 | `lan.dang` | Mở **Sự cố → Báo sự cố**. Chọn Phòng B; tài sản chọn từ phòng nếu có. Nhập tiêu đề **Vòi nước rò rỉ**; mô tả **Sau khi khóa vòi vẫn nhỏ nước**. Xem sự cố tương tự, thêm bằng chứng và lưu. Ghi mã sự cố. | Đúng phòng, chi nhánh và người báo. Không tạo trùng cùng sự việc. |
| 24 | `minh.le` | Mở **Sự cố**, bấm mã vừa tạo. Tại **Phân loại sự cố**, chọn nhóm **Nước**, mức độ **Bình thường**, ảnh hưởng đến phòng **Có**; bấm **Lưu phân loại**. | Phân loại đã lưu. Đây là lựa chọn cho tình huống thử, không áp dụng mọi sự cố rò nước. |
| 25 | `minh.le` | Tại **Tiếp nhận**, chọn độ ưu tiên phù hợp có thời hạn cấu hình. Nhập kế hoạch **Kiểm tra mối nối, siết lại và chạy thử**. Xem lại rồi xác nhận tiếp nhận. | Có ưu tiên, hạn phản hồi/xử lý và kế hoạch. Nếu thiếu cấu hình thời hạn, dừng bước này để chuẩn bị. |
| 26 | `minh.le` | Mở phân công xử lý, chọn `thinh.ngo` từ danh sách hợp lệ và lưu. Ghi mã công việc bảo trì. | Thịnh được giao việc gắn đúng sự cố Phòng B. |
| 27 | `thinh.ngo` | Mở **Công việc bảo trì**, chọn việc vừa được giao, bắt đầu và ghi tiến độ. | Công việc đang xử lý, có thời gian bắt đầu. |
| 28 | `thinh.ngo` | Nộp kết quả: nguyên nhân **Mối nối bị lỏng**; xử lý **Đã siết lại mối nối, chạy thử không còn rò**. Thêm bằng chứng, xem lại và xác nhận nộp. | Chờ xác minh. Bài thử này không thay vật tư; nếu thực tế thay thì phải ghi đúng tiêu hao. |
| 29 | `minh.le` | Mở kết quả, xác minh, nhập **Đã kiểm tra sau chạy thử, không còn rò nước**; chấp nhận và xác nhận đóng khi đủ điều kiện. | Có xác minh độc lập và kết quả đóng đã lưu. Không để Thịnh tự xác minh việc mình làm. |

## Quay lại Luồng 1 — Kiểm tra kết quả phòng

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 30 | `le.truong` | Mở **Trạng thái phòng**, tải lại Phòng A; xem lịch sử kiểm tra. | Kiểm tra lại đã đạt. Phòng sẵn sàng nếu không còn điều kiện cản trở khác. |
| 31 | `le.truong` | Tải lại Phòng B, xem sự cố và các lệnh khóa còn hiệu lực. | Bảo trì đã được xác minh. Nếu còn khóa, nhờ người có quyền xử lý đúng lý do; không tự suy ra phòng sẵn sàng từ việc đóng sự cố. |

## Kết thúc Luồng 6 — Đóng ca và xem thời gian

| Bước | Tài khoản | Thao tác thực hiện | Kết quả cần thấy |
|---|---|---|---|
| 32 | `lan.dang` | Mở **Bắt đầu / kết thúc ca**, kiểm tra không còn việc đang chạy. Xem lại và xác nhận kết thúc; nếu yêu cầu tạm dừng việc, xử lý theo màn hình. | Ngày làm việc đã kết thúc, có giờ kết thúc. |
| 33 | `lan.dang` | Mở **Nhật ký thời gian của tôi**, tìm việc dọn và làm lại vừa thực hiện. | Có khoảng thời gian gắn đúng công việc của Lan. |
| 34 | `thinh.ngo` | Làm như bước 32 để kết thúc ngày làm việc của Thịnh. | Có giờ kết thúc; không còn khoảng công việc đang mở ngoài dự kiến. |
| 35 | `thinh.ngo` | Mở **Nhật ký thời gian của tôi**, tìm công việc bảo trì vừa làm. | Có khoảng thời gian đúng công việc của Thịnh. Thời gian ghi nhận theo thực tế, không giả lập đủ một ca. |

## Cách đánh dấu kết quả

Sau mỗi dòng, đánh dấu **Đạt / Lỗi / Bị chặn**. Nếu lỗi, ghi: **Bước … / tài khoản … / mã hồ sơ … / thông báo …** và chụp màn hình. Nếu mất mạng sau khi lưu, mở lại hồ sơ kiểm tra trước khi bấm lần nữa.

Không có nút hoặc người được chọn: kiểm tra quyền, chi nhánh, phân công và trạng thái; không bỏ qua bước bằng tài khoản quản trị. Nếu giao diện dùng tên nút hơi khác, chọn thao tác cùng nghĩa trên đúng hồ sơ.

Đây là bảng hướng dẫn thực hiện, chưa phải kết quả chạy thử thành công. Tài liệu không tạo dữ liệu, đổi quyền hay đặt mật khẩu. Hai chỗ TK-CA và TK-DUYỆT phải được điền trước khi dùng.
