# KỊCH BẢN TRÌNH DIỄN: CHUẨN BỊ PHÒNG ĐÓN KHÁCH BUỔI CHIỀU

Tài liệu dành cho người trình diễn HotelOps. Ngày soạn: 18/11/2026.

## 1. Câu chuyện và cách sử dụng

Khách sạn cần chuẩn bị hai phòng để đón khách buổi chiều. Phòng A cần dọn và bổ sung xà phòng. Sau lần dọn đầu, người kiểm tra phát hiện gương còn vệt bẩn nên yêu cầu làm lại. Phòng B có vòi nước rò rỉ, cần trưởng bảo trì tiếp nhận, giao kỹ thuật viên xử lý và xác minh kết quả. Cuối buổi, lễ tân kiểm tra lại khả năng phục vụ của hai phòng; nhân viên kết thúc ngày làm việc và đối chiếu thời gian.

Điểm khác với kịch bản trước: dùng hai phòng và hai nhánh công việc song song; có kiểm tra không đạt rồi kiểm tra lại; vật tư được cấp thành hai đợt; có mở và kết thúc ca để đối chiếu thời gian thực tế.

Mỗi bước có ba phần: **Ai làm → Thao tác → Kết quả cần thấy**. Đọc câu dẫn trước mỗi luồng để người xem hiểu mục đích. Đây là kịch bản và kết quả mong đợi, chưa phải biên bản xác nhận đã chạy thành công trên dữ liệu thực tế.

**Thứ tự trình diễn:** Luồng 6 phần mở ca → Luồng 1 → Luồng 2 đến bước nhận việc → Luồng 5 → hoàn tất Luồng 2 → Luồng 3 → Luồng 4 → quay lại Luồng 1 → Luồng 6 phần kết thúc ca. Khoảng 45–60 phút, chưa tính thời gian chuẩn bị dữ liệu.

## 2. Chuẩn bị trước buổi trình diễn

**Địa chỉ:** dùng https://localhost:5173/login cho giao diện phát triển hiện có, hoặc địa chỉ ứng dụng đang được người phụ trách xác nhận. Thử đăng nhập trước buổi diễn.

**Hai phòng mẫu:** gọi là Phòng A và Phòng B. Có thể chọn số 305 và 306 nếu chúng thực sự tồn tại, cùng chi nhánh và phù hợp trạng thái. Nếu không, chọn hai phòng khác và ghi lại bên dưới. Không tạo dữ liệu bằng cách đoán mã phòng.

- Phòng A: chưa sẵn sàng, đủ điều kiện tạo dọn phòng, không có công việc đang mở gây trùng, không có lệnh khóa ngoài kịch bản.
- Phòng B: phòng thử nghiệm có thể báo sự cố vòi nước; dùng tài sản thực tế thuộc phòng nếu danh sách có. Không chọn tài sản của phòng khác.
- Ngày và ca: chọn ngày trình diễn thực tế; chuẩn bị ca trước giờ bắt đầu, không lấy ca cũ hoặc đã hết để thử bắt đầu làm việc.
- Danh mục: có loại vệ sinh, bộ tiêu chí kiểm tra đang hoạt động, nhóm sự cố Nước, mức độ hợp lệ và thời hạn xử lý theo độ ưu tiên đã được cấu hình.
- Vật tư: chọn xà phòng đang có trong danh mục, đúng đơn vị cơ sở. Kiểm tra kho phù hợp có ít nhất 6 đơn vị khả dụng. Nếu không có mặt hàng này, thay bằng một vật tư buồng phòng có thật và sửa toàn bộ ví dụ cho thống nhất.
- Người duyệt: phải có trong danh sách người duyệt mà hệ thống trả về cho phiếu. Ghi tên đăng nhập trước khi diễn; không mặc định giám sát buồng phòng được duyệt.
- Dùng hồ sơ trình duyệt riêng cho mỗi người hoặc đăng xuất khi đổi tài khoản. Hai thẻ cùng hồ sơ dùng chung phiên đăng nhập.
- Thử trước các nút tạo việc, phân công, kiểm tra, phân loại, tiếp nhận, duyệt và cấp phát. Nếu thiếu quyền hoặc danh mục thì ghi bước bị chặn, không giả định đã hoàn thành.

**Tài khoản tham khảo từ hướng dẫn hiện có — cần kiểm tra quyền thực tế trước khi dùng:**

- Lễ tân: `le.truong` — theo dõi phòng và chuyển đúng công việc theo menu được cấp.
- Giám sát buồng phòng: `mai.tran` — tạo, phân công dọn phòng và điều phối kiểm tra.
- Nhân viên buồng phòng: `lan.dang` — dọn, yêu cầu vật tư và làm lại công việc được giao.
- Người kiểm tra: dùng `mai.tran` nếu được phân công và hệ thống xác nhận đủ điều kiện; nếu không, chọn người khác từ danh sách hợp lệ. Không để nhân viên tự kiểm tra công việc mình vừa dọn.
- Trưởng bảo trì: `minh.le` — phân loại, tiếp nhận, phân công và xác minh bảo trì.
- Kỹ thuật viên: `thinh.ngo` — thực hiện bảo trì được giao.
- Thủ kho: `tung.vo` — cấp phát phiếu đã duyệt.
- Người lập ca: chọn người có chức năng lập phân công ca; tài khoản tham khảo là `hung.nguyen` hoặc `ha.pham`, chỉ dùng khi hệ thống cho phép.
- Người duyệt vật tư: ghi theo người được chỉ định thực tế trên phiếu; phải khác người yêu cầu và người cấp phát theo điều kiện của hệ thống.

**Phiếu ghi nhanh:** Chi nhánh: … ; Phòng A: … ; Phòng B: … ; Ngày/ca: … ; Người duyệt vật tư: … ; Kho: … ; Vật tư/đơn vị: … ; Tồn khả dụng ban đầu: …

Đặt tiền tố nội dung là **ĐÓN KHÁCH CHIỀU – ngày trình diễn – lần chạy**, ví dụ “ĐÓN KHÁCH CHIỀU – 20/11 – lần 1”. Khi chạy lại, dùng lần chạy mới hoặc tiếp tục hồ sơ chưa xong; không tạo trùng công việc đang mở.

## 3. Luồng 6 — Lập ca và bắt đầu ngày làm việc

**Câu dẫn:** “Trước khi giao việc, tôi bố trí nhân sự vào ca. Thời gian bắt đầu ca và thời gian làm từng công việc được ghi nhận riêng.”

**Bước 6.1 — Người lập ca mở Ca làm việc → Lập phân công ca.** Chọn chi nhánh, bộ phận, ngày và ca thực tế. Thêm phân công cho Lan và Thịnh; bổ sung người kiểm tra hoặc trưởng bảo trì nếu thao tác của họ yêu cầu ngày làm việc đang mở. Dùng định nghĩa ca phù hợp, không sửa giờ máy để vượt điều kiện.

**Cần thấy:** danh sách bản nháp đúng nhân viên, ngày, bộ phận và ca; chưa coi bản nháp là lịch đã lưu.

**Bước 6.2 — Xem trước và lưu.** Bấm Xem trước phân công. Nếu có trùng ca hoặc không đủ điều kiện thì sửa bản nháp. Khi hợp lệ, bấm Xem lại lưu → Xác nhận lưu kế hoạch. Tải danh sách phân ca để kiểm tra.

**Cần thấy:** lịch đã lưu và đúng người; không có ca chồng lấn chưa giải quyết.

**Bước 6.3 — Lan và Thịnh mở ngày làm việc theo ca.** Mỗi người đăng nhập tài khoản của mình, chọn đúng ca, xem lại rồi xác nhận bắt đầu trong thời gian hệ thống cho phép.

**Cần thấy:** ngày làm việc đang mở, giờ bắt đầu do máy chủ ghi nhận. Chụp bằng chứng. Chưa kết thúc ca; chuyển sang Luồng 1.

## 4. Luồng 1 — Theo dõi trạng thái phòng và chuyển việc

**Câu dẫn:** “Lễ tân cần biết phòng nào chưa phục vụ được và chuyển việc cho đúng bộ phận, thay vì tự đánh dấu phòng đã sẵn sàng.”

**Bước 1.1 — Lễ tân mở Buồng phòng → Trạng thái phòng.** Chọn chi nhánh, tìm Phòng A và Phòng B; mở chi tiết từng phòng. Ghi trạng thái hiện tại, nguyên nhân chưa sẵn sàng và công việc đang liên quan.

**Cần thấy:** số phòng và chi nhánh đúng; phân biệt tình trạng phòng với công việc dọn hoặc bảo trì.

**Bước 1.2 — Chuyển xử lý Phòng A.** Trong hành động tiếp theo, mở Tạo công việc vệ sinh nếu tài khoản có quyền. Nếu không có quyền, chuyển sang giám sát buồng phòng và mở đúng Phòng A để tạo việc ở Luồng 2.

**Cần thấy:** việc mới gắn đúng phòng. “Chuyển việc” trong kịch bản này là đi tới nghiệp vụ tương ứng hoặc bàn giao cho người có quyền, không giả định có nút chuyển việc tự động cho mọi tài khoản.

**Bước 1.3 — Ghi nhận Phòng B cần bảo trì.** Mở hành động Báo sự cố cho phòng này nếu được phép, hoặc để Lan báo ở Luồng 4. Chỉ tạo một báo cáo cho cùng sự việc.

**Cần thấy:** ngữ cảnh phòng được giữ đúng. Không dùng chức năng kích hoạt/ngừng kích hoạt phòng để mô phỏng nhận hoặc trả phòng của khách.

**Bước 1.4 — Để lại màn trạng thái làm điểm đối chiếu.** Chụp hai phòng trước khi xử lý. Cuối buổi quay lại cùng màn hình để so sánh.

## 5. Luồng 2 — Tạo, phân công và thực hiện dọn phòng

**Câu dẫn:** “Giám sát giao công việc dọn Phòng A cho Lan. Việc dọn chỉ hoàn thành phần thực hiện; phòng còn phải qua kiểm tra.”

**Bước 2.1 — Giám sát tạo công việc.** Chọn Phòng A, tải cấu hình phòng, chọn loại vệ sinh phù hợp. Nhập hướng dẫn: “Chuẩn bị phòng đón khách buổi chiều; lau gương, vệ sinh nhà tắm và bổ sung xà phòng theo định mức.” Chọn độ ưu tiên và hạn hoàn thành phù hợp lịch trình; lưu việc mới. Ghi mã công việc: …

**Cần thấy:** công việc thuộc đúng phòng và chi nhánh, đang chờ phân công hoặc đã phân công nếu dùng thao tác lưu kèm phân công.

**Bước 2.2 — Giám sát phân công Lan.** Mở phân công vệ sinh, chọn công việc vừa tạo và Lan trong danh sách hợp lệ. Kiểm tra bản nháp, xem xung đột nếu có, sau đó lưu phân công.

**Cần thấy:** Lan là người thực hiện trên hồ sơ đã tải lại; chưa có nghĩa Lan đã bắt đầu việc.

**Bước 2.3 — Lan mở công việc được giao và bắt đầu.** Kiểm tra ca đang mở và đúng phòng trước khi xác nhận bắt đầu.

**Cần thấy:** công việc đang thực hiện; thời gian thực hiện bắt đầu được ghi nhận.

**Bước 2.4 — Chuyển sang Luồng 5 để xin vật tư.** Lan tạo yêu cầu 6 đơn vị xà phòng từ công việc nếu có liên kết hỗ trợ. Chỉ nói hai hồ sơ đã liên kết sau khi thấy liên kết được lưu. Nếu chưa có liên kết trên giao diện, ghi mã công việc trong mục đích và nói rõ đây là tham chiếu bằng nội dung.

**Bước 2.5 — Sau khi cấp đủ vật tư, Lan hoàn thiện việc dọn.** Ghi tiến độ, hoàn tất các mục bắt buộc và đính kèm bằng chứng theo yêu cầu. Trong tình huống mô phỏng, gương vẫn còn một vệt bẩn để người kiểm tra phát hiện; không bỏ trống dữ liệu bắt buộc để tạo lỗi giả.

**Bước 2.6 — Lan nộp hoàn tất dọn phòng.** Xem lại và xác nhận hoàn tất.

**Cần thấy:** công việc chuyển sang chờ kiểm tra. Không tuyên bố phòng đã sẵn sàng chỉ vì nhân viên bấm hoàn tất. Chuyển sang Luồng 3.

## 6. Luồng 5 — Yêu cầu, duyệt và cấp phát vật tư hai đợt

**Câu dẫn:** “Lan cần 6 đơn vị xà phòng. Người duyệt quyết định số lượng; thủ kho cấp 4 trước và 2 sau để minh họa theo dõi phần còn lại.”

**Bước 5.1 — Lan tạo phiếu.** Mở Yêu cầu vật tư → Tạo yêu cầu, chọn đúng chi nhánh, bộ phận và kho. Mục đích: “Bổ sung xà phòng phục vụ dọn Phòng A và xe đẩy buồng phòng, mã công việc …”. Chọn mặt hàng và đơn vị có thật, số lượng 6; chọn người duyệt từ danh sách hợp lệ. Lưu nháp và ghi mã phiếu: …

**Cần thấy:** người yêu cầu là Lan; số lượng 6; kho và người duyệt đúng. Phiếu nháp chưa được cấp phát.

**Bước 5.2 — Lan gửi duyệt.** Kiểm tra dòng vật tư, xem lại và xác nhận gửi duyệt.

**Cần thấy:** phiếu chờ người duyệt đã chỉ định; người yêu cầu không tự duyệt phiếu của mình.

**Bước 5.3 — Người duyệt mở đúng phiếu.** Đối chiếu mục đích và nhu cầu, phê duyệt 6 đơn vị, ghi nhận xét “Đồng ý phục vụ chuẩn bị phòng đón khách buổi chiều”; hoàn tất xác nhận.

**Cần thấy:** phiếu được duyệt; số lượng đã duyệt là 6, đã cấp là 0. Phê duyệt chưa làm giảm tồn kho.

**Bước 5.4 — Thủ kho cấp đợt một.** Mở phiếu đã duyệt, kiểm tra tồn khả dụng tại đúng kho. Chọn cấp một phần, nhập 4, điền tên người thực nhận và xác nhận cấp phát. Kịch bản chọn cấp hai đợt để minh họa; không nói thiếu hàng nếu kho thực tế vẫn đủ.

**Cần thấy:** đã cấp 4, còn được cấp 2; có chứng từ cấp phát. Nếu không có biến động khác, tồn kho giảm 4 đơn vị cơ sở so với trước lần cấp.

**Bước 5.5 — Thủ kho cấp đợt hai trên cùng phiếu.** Tải lại phiếu, kiểm tra còn lại 2, cấp đúng 2 và xác nhận. Không tạo phiếu mới cho phần còn lại.

**Cần thấy:** yêu cầu 6, duyệt 6, cấp tổng cộng 6, còn lại 0. Hai đợt cấp có lịch sử riêng; tổng tồn giảm 6 nếu không có biến động khác. Không thực hiện thêm một lần xuất kho nếu chứng từ đã được ghi nhận khi cấp phát.

**Bước 5.6 — Lan đối chiếu kết quả.** Mở lại phiếu xem lịch sử, sau đó quay về công việc vệ sinh để tiếp tục Bước 2.5. Tên người nhận trên phiếu không đồng nghĩa có thêm thao tác xác nhận nhận hàng riêng.

## 7. Luồng 3 — Kiểm tra không đạt, làm lại và kiểm tra lại

**Câu dẫn:** “Hoàn tất dọn phòng chưa bảo đảm chất lượng. Người kiểm tra phát hiện lỗi, yêu cầu làm lại đúng lỗi đó, rồi kiểm tra lại trước khi chấp nhận.”

**Bước 3.1 — Giám sát phân công kiểm tra.** Mở công việc dọn Phòng A đang chờ kiểm tra. Chọn người kiểm tra đủ điều kiện và bộ tiêu chí hiện có; lưu phân công. Ghi mã lần kiểm tra: …

**Cần thấy:** đúng Phòng A, đúng công việc nguồn và đúng người được giao.

**Bước 3.2 — Người kiểm tra bắt đầu và lưu kết quả.** Kiểm tra các tiêu chí được giao. Chọn một tiêu chí có thật về gương hoặc nhà tắm, đánh dấu không đạt; các tiêu chí còn lại ghi đúng kết quả mô phỏng. Mô tả lỗi: “Gương nhà tắm còn vệt bẩn ở góc phải, cần lau lại và chụp ảnh sau xử lý.” Bổ sung bằng chứng nếu bắt buộc, rồi lưu kết quả và lỗi.

**Cần thấy:** lỗi gắn với đúng tiêu chí; kết quả đã lưu, không chỉ đang chọn trên màn hình.

**Bước 3.3 — Kết luận không đạt và yêu cầu làm lại.** Hoàn tất phần kiểm tra, nhập lý do từ chối rõ ràng và xác nhận. Mở phần yêu cầu làm lại, chọn lỗi đã ghi, lưu yêu cầu nếu bước kết luận chưa tự tạo. Chỉ giữ một yêu cầu làm lại cho cùng lần này.

**Cần thấy:** kiểm tra không đạt, có yêu cầu làm lại liên kết hồ sơ gốc; phòng chưa được chấp nhận chỉ vì đã kiểm tra xong.

**Bước 3.4 — Phân công và thực hiện làm lại.** Giám sát giao việc làm lại cho Lan. Lan mở đúng việc làm lại, tiếp nhận/bắt đầu theo nút được phép, lau lại gương, nhập kết quả “Đã lau sạch vệt bẩn, kiểm tra dưới ánh sáng và chụp ảnh sau xử lý”, rồi nộp kết quả.

**Cần thấy:** công việc làm lại có người thực hiện, kết quả và bằng chứng. Nộp kết quả chưa phải kiểm tra lại đạt.

**Bước 3.5 — Phân công kiểm tra lại.** Người có quyền mở hồ sơ gốc và phân công kiểm tra lại. Người được giao kiểm tra lại các lỗi trước đó và mọi mục bắt buộc của lần kiểm tra lại; lưu kết quả rồi kết luận đạt khi đủ điều kiện.

**Cần thấy:** lịch sử thể hiện kiểm tra lần đầu không đạt → làm lại → kiểm tra lại đạt. Hồ sơ lần đầu vẫn được giữ.

**Bước 3.6 — Lễ tân tải lại Phòng A.** Xem trạng thái sẵn sàng thực tế. Nếu không còn điều kiện cản trở, phòng được ghi nhận sẵn sàng; nếu còn lệnh khóa hoặc vấn đề khác, nêu rõ nguyên nhân thay vì kết luận sai.

## 8. Luồng 4 — Báo vòi nước rò rỉ và xử lý bảo trì

**Câu dẫn:** “Phòng B còn vướng sự cố kỹ thuật. Nhân viên chỉ báo hiện tượng; trưởng bảo trì đánh giá và giao việc; kết quả được kiểm tra độc lập.”

**Bước 4.1 — Lan báo sự cố.** Mở Sự cố → Báo sự cố. Chi nhánh lấy theo người dùng; tìm và chọn số Phòng B, chọn vòi nước trong danh sách tài sản nếu có. Tiêu đề: “Vòi nước phòng B rò rỉ sau khi khóa”. Mô tả: “Nước tiếp tục nhỏ tại vòi sau khi khóa; cần kiểm tra trước khi phục vụ khách.” Chọn thời điểm phát hiện thực tế, thêm bằng chứng, xem sự cố tương tự trước khi xác nhận tạo. Ghi mã sự cố: …

**Cần thấy:** báo cáo thuộc đúng phòng và người báo; chưa có người xử lý không đồng nghĩa báo cáo thất bại.

**Bước 4.2 — Minh phân loại.** Chọn nhóm Nước. Trong tình huống mô phỏng, trưởng bảo trì đánh giá mức độ Bình thường và xác nhận Có ảnh hưởng đến khả năng sẵn sàng của phòng; bấm Lưu phân loại. Đây là lựa chọn của tình huống, không phải quy tắc áp dụng mọi sự cố rò nước.

**Cần thấy:** nhóm, mức độ và ảnh hưởng đã lưu; lưu phân loại không tự sinh thời hạn hoặc tự khóa phòng.

**Bước 4.3 — Minh xem xét khóa phòng nếu cần.** Nếu phòng chưa thể phục vụ an toàn và tài khoản có thao tác hợp lệ, tạo lệnh khóa cho Phòng B với lý do tương ứng. Nếu không đủ điều kiện khóa, chỉ trình bày bước đang bị chặn, không khẳng định phòng đã được khóa.

**Cần thấy:** lệnh khóa xuất hiện trong chi tiết phòng khi thực sự lưu thành công.

**Bước 4.4 — Minh tiếp nhận.** Chọn độ ưu tiên phù hợp, xem thời hạn phản hồi và xử lý từ cấu hình; nhập kế hoạch “Kiểm tra đầu vòi và mối nối; khắc phục rò, chạy thử và kiểm tra lại”. Xem lại rồi xác nhận tiếp nhận. Nếu không có thời hạn cấu hình, dừng để chuẩn bị danh mục trước buổi diễn, không tự nhập hạn giả để vượt bước.

**Cần thấy:** có độ ưu tiên, thời hạn xử lý và kế hoạch; hồ sơ đủ điều kiện phân công.

**Bước 4.5 — Minh giao Thịnh xử lý.** Chọn Thịnh từ danh sách kỹ thuật viên hợp lệ; ghi hướng dẫn và lưu. Thịnh mở công việc được giao khi ngày làm việc đang mở, bắt đầu việc, ghi tiến độ.

**Cần thấy:** đúng người, đúng phòng và đúng liên kết sự cố; ghi nhận khoảng thời gian thực hiện.

**Bước 4.6 — Thịnh nộp kết quả.** Tình huống mô phỏng: mối nối lỏng, siết lại và chạy thử không còn rò, không phát sinh thay vật tư. Ghi nguyên nhân, cách xử lý và bằng chứng; xem lại rồi xác nhận nộp. Nếu thực tế cần vật tư thì dùng phiếu liên kết phù hợp, không ghi không tiêu hao để bỏ qua yêu cầu.

**Cần thấy:** công việc chờ xác minh, không tự đóng ngay khi kỹ thuật viên nộp.

**Bước 4.7 — Minh xác minh và đóng.** Đối chiếu kết quả, ghi nhận xét “Đã kiểm tra vòi và mối nối sau chạy thử, không còn rò”, rồi xác nhận chấp nhận/đóng khi đủ điều kiện. Kiểm tra lệnh khóa liên quan; thực hiện thao tác giải phóng được hệ thống cho phép nếu còn hiệu lực. Không tự bỏ các lệnh khóa do sự cố khác.

**Cần thấy:** kết quả được người khác xác minh, hồ sơ có lịch sử đầy đủ. Lễ tân tải lại Phòng B để xem còn điều kiện nào cản trở phục vụ; không suy ra mọi phòng đều sẵn sàng chỉ từ việc đóng bảo trì.

## 9. Luồng 6 — Kết thúc ca và xem thời gian

**Câu dẫn:** “Sau khi công việc đã được xử lý, tôi đối chiếu thời gian thực hiện và kết thúc ngày làm việc. Đây là dữ liệu vận hành, không phải bảng lương.”

**Bước 6.4 — Lan và Thịnh kiểm tra công việc đang chạy.** Mỗi người mở ngày làm việc của mình. Nếu còn công việc đang thực hiện, đọc hướng dẫn tạm dừng hoặc kết thúc phù hợp; không ép kết thúc bằng cách sửa thời gian máy.

**Bước 6.5 — Kết thúc ngày làm việc.** Xem lại ảnh hưởng và xác nhận kết thúc. Nếu giao diện yêu cầu tạm dừng toàn bộ công việc trước khi kết thúc thì thực hiện và kiểm tra kết quả.

**Cần thấy:** giờ kết thúc đã được lưu, không còn khoảng thực hiện đang mở ngoài dự kiến. Nếu kết thúc sớm được hệ thống cho phép thì nói rõ đây là kết thúc sớm, không giả lập đủ tám giờ.

**Bước 6.6 — Mở Thời gian làm việc của tôi.** Lan xem các khoảng dọn phòng và làm lại; Thịnh xem khoảng bảo trì. Đối chiếu công việc, thời điểm bắt đầu/kết thúc và thời gian nghỉ/tạm dừng nếu có.

**Cần thấy:** thời gian gắn đúng người, đúng công việc. Không yêu cầu tổng thời gian công việc bằng toàn bộ độ dài ca vì có thời gian chờ hoặc nghỉ. Không tự cộng hai khoảng làm việc chồng nhau thành thời gian hợp lệ.

## 10. Kết thúc trình diễn và bằng chứng cần lưu

**Lời kết mẫu:** “Từ trạng thái phòng, hệ thống dẫn tới công việc đúng bộ phận. Việc dọn được kiểm tra, lỗi được làm lại và kiểm tra lại. Sự cố có người xử lý và người xác minh riêng. Vật tư đi qua yêu cầu, duyệt, cấp phát và đối chiếu tồn. Toàn bộ công việc được đặt trong ca và có thời gian thực hiện.”

1. Luồng phòng: ảnh trạng thái Phòng A và B trước/sau, nguyên nhân chưa sẵn sàng nếu còn.
2. Luồng dọn: mã việc, người tạo, người được giao và kết quả hoàn tất.
3. Luồng kiểm tra: kết luận không đạt, lỗi, mã làm lại và kết quả kiểm tra lại đạt.
4. Luồng bảo trì: báo cáo, phân loại, tiếp nhận, người được giao, kết quả nộp và xác minh.
5. Luồng vật tư: cùng một phiếu có yêu cầu 6, duyệt 6, cấp 4 rồi 2, còn lại 0; chứng từ và tồn đối chiếu.
6. Luồng ca: lịch đã lưu, ngày làm việc đã mở/kết thúc và các khoảng thời gian theo công việc.

**Phiếu kết quả:** Với từng luồng ghi “Chưa chạy / Đạt / Lỗi / Bị chặn”, mã hồ sơ, bước cuối đã thực hiện và ảnh bằng chứng. Không dùng màn hình nhập liệu để chứng minh dữ liệu đã lưu.

## 11. Nếu gặp vướng mắc khi trình diễn

- Không có nút: kiểm tra tài khoản, chi nhánh, người được phân công và trạng thái hồ sơ; không chuyển sang quản trị viên để làm thay mọi vai trò.
- Không có người duyệt hoặc danh mục: ghi bước bị chặn và hoàn thiện cấu hình được phép trước khi chạy tiếp.
- Thiếu tồn: chỉ cấp số lượng thực tế khả dụng và giữ phần còn lại; không sửa số tồn trực tiếp.
- Sự cố chưa có thời hạn: kiểm tra đã phân loại và tiếp nhận chưa; phân loại không tự đặt thời hạn.
- Không bắt đầu được việc: kiểm tra ngày làm việc đang mở và có công việc khác đang chạy không.
- Mạng lỗi sau khi lưu: đọc lại hồ sơ và lịch sử trước khi bấm lần nữa, đặc biệt với cấp phát vật tư.
- Quyền đang kiểm tra lại: đợi phục hồi hoặc bấm Thử lại; không tải lại toàn trang ngay khi đang có nội dung chưa lưu.

## 12. Phạm vi đối chiếu tài liệu

Kịch bản được đối chiếu với hướng dẫn sáu luồng hiện có, yêu cầu nghiệp vụ, ghi chú thay đổi và các màn phân loại, trạng thái phòng, lập ca trong mã nguồn. Quyền và dữ liệu mẫu phải được xác nhận lại trước buổi trình diễn. Không khẳng định các tài khoản hoặc phòng ví dụ đã đủ điều kiện trên cơ sở dữ liệu hiện tại.

Lượt này chỉ tạo tài liệu mới; không tạo phòng, phiếu, ca, phân quyền hoặc sửa dữ liệu. Chưa chạy thử xuyên suốt sáu luồng. Tài liệu giữ nguyên tên đăng nhập và địa chỉ để người trình diễn sử dụng; toàn bộ diễn giải nghiệp vụ bằng tiếng Việt.
