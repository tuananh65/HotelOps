# HOTELOPS — OPERATIONS MANAGER ROLE CONSOLIDATION & BUSINESS WORKFLOW REDESIGN

## 1. Mục tiêu nhiệm vụ

Tiến hành tái cấu trúc hệ thống HotelOps bằng cách hợp nhất hai role hiện tại:

- Operations Manager (OM).
- Department Head (DH).

Thành một role duy nhất:

- Tên tiếng Anh: Operations Manager.
- Tên tiếng Việt: Quản lý vận hành.
- Role code: Ưu tiên giữ role code OM hiện tại trong source và database.

Operations Manager mới chịu trách nhiệm quản lý toàn bộ hoạt động, nhân sự, công việc, tài sản và nguồn lực trong cơ sở được bàn giao.

Loại bỏ Department Head khỏi cơ cấu vận hành hiện tại.

Đây là yêu cầu triển khai thực tế trên toàn bộ hệ thống, bao gồm Frontend, Backend, Database, Authorization, Business Workflow, Notification, Dashboard và Test.

Không được chỉ sửa Sidebar hoặc ẩn các nút trên giao diện.

---

## 2. Nguyên tắc thực hiện

Trước khi sửa code:

1. Kiểm tra toàn bộ source code hiện tại.
2. Xác định role code, permission code và authorization policy thực tế.
3. Kiểm tra database schema, role mappings và dữ liệu hiện có.
4. Kiểm tra toàn bộ references đến Department Head.
5. Kiểm tra các API, services, handlers, routes, components và business workflows liên quan.
6. Kiểm tra SRS, UC specifications và các tài liệu nghiệp vụ hiện tại nếu có trong workspace.
7. Xác định những chức năng có thể tái sử dụng thay vì xây dựng lại.

Các quyết định nghiệp vụ trong prompt này là yêu cầu mới nhất và được ưu tiên nếu có xung đột với thiết kế OM/DH cũ.

Không tự ý thay đổi các nghiệp vụ ngoài phạm vi nhiệm vụ.

Không dừng lại ở bước phân tích. Sau khi rà soát source, chủ động triển khai, chạy kiểm thử, sửa lỗi và hoàn thành nhiệm vụ.

---

## 3. Định nghĩa role Operations Manager mới

Role OM mới chịu trách nhiệm:

- Quản lý toàn bộ hoạt động vận hành trong cơ sở được giao.
- Quản lý công việc vận hành chung.
- Quản lý ca làm việc và bàn giao ca.
- Quản lý hồ sơ tài sản.
- Điều chuyển tài sản nội bộ.
- Kiểm soát tồn kho.
- Phê duyệt yêu cầu cấp phát vật tư.
- Tạo yêu cầu điều chuyển vật tư liên cơ sở.
- Phê duyệt yêu cầu điều chuyển vật tư từ cơ sở khác gửi đến.
- Giám sát tình trạng phòng.
- Giám sát hoạt động Housekeeping.
- Giám sát sự cố và bảo trì.
- Giám sát yêu cầu nội bộ giữa các bộ phận.
- Theo dõi dashboard và báo cáo vận hành.

OM không phải Administrator và không được kế thừa toàn bộ quyền của các role cấp dưới.

---

## 4. Phạm vi phân quyền — Branch Scope

### 4.1. Phạm vi mặc định

OM chỉ có quyền quản lý cơ sở được bàn giao.

Trong cơ sở đó, OM được quản lý tất cả các bộ phận thuộc phạm vi vận hành.

Không tiếp tục giới hạn OM theo một department duy nhất như Department Head cũ.

OM không được truy cập hoặc quản lý trái phép dữ liệu của cơ sở khác, bao gồm:

- Phòng.
- Tình trạng phòng.
- Nhân sự.
- Ca làm việc.
- Công việc vận hành.
- Sự cố.
- Yêu cầu nội bộ.
- Tài sản.
- Chứng từ kho.
- Lịch sử nhập xuất kho.
- Dashboard.
- Báo cáo vận hành.

### 4.2. Ngoại lệ đối với kho liên cơ sở

OM được phép:

- Xem danh sách các cơ sở có kho vật tư.
- Xem danh sách kho của cơ sở khác.
- Tra cứu thông tin vật tư cần thiết để tạo yêu cầu điều chuyển.
- Tạo yêu cầu điều chuyển vật tư từ kho của cơ sở khác về cơ sở mình.
- Xem những yêu cầu điều chuyển mà cơ sở mình là bên gửi hoặc bên nhận.
- Theo dõi trạng thái và lịch sử các yêu cầu liên quan.

Ngoại lệ này không đồng nghĩa với quyền truy cập toàn bộ dữ liệu kho của cơ sở khác.

Chỉ trả về những trường dữ liệu cần thiết để phục vụ điều chuyển.

Nếu cần hiển thị tồn khả dụng của kho cơ sở khác, giới hạn ở thông tin phục vụ lựa chọn vật tư điều chuyển; không mở rộng sang sổ kho, lịch sử giao dịch hoặc chứng từ nội bộ.

Không được:

- Sửa danh mục kho cơ sở khác.
- Điều chỉnh tồn kho cơ sở khác.
- Đảo giao dịch kho cơ sở khác.
- Nhập hoặc xuất kho thay Storekeeper.
- Xem dữ liệu kho không liên quan đến nghiệp vụ điều chuyển.

### 4.3. Backend Authorization

Bắt buộc kiểm tra đồng thời:

- Identity.
- Role.
- Permission.
- Branch Scope.
- Resource ownership.
- Workflow state.
- Quan hệ của người dùng với hồ sơ đang thao tác.

Không tin tưởng branchId, departmentId hoặc userId do Frontend gửi lên nếu chưa xác thực.

Không triển khai cơ chế phân quyền chỉ bằng việc ẩn nút.

---

## 5. FE01 — Tài khoản và xác thực

Giữ các chức năng:

- Đăng nhập.
- Đăng xuất.
- Đổi mật khẩu.
- Xem thông tin tài khoản cá nhân.

Cập nhật role claims, permission claims và session context theo role OM mới.

Kiểm tra khả năng tương thích với các tài khoản Department Head sau migration.

Không tự ý thay đổi mật khẩu tài khoản, chính sách xác thực hoặc thông tin đăng nhập đang được sử dụng.

---

## 6. FE02 — Cơ cấu cơ sở

OM được phép:

- Xem cơ cấu cơ sở.
- Xem danh sách bộ phận.
- Xem danh sách phòng.
- Xem tầng và khu vực.
- Xem thông tin nhân sự phục vụ phân công công việc.
- Thực hiện nghiệp vụ quản lý phòng mà OM đã được cấp quyền hợp lệ, bao gồm ngừng sử dụng phòng nếu có trong thiết kế hiện tại.

Không cấp quyền quản trị hệ thống cho OM.

Không tự động cấp quyền tạo tài khoản, sửa role hoặc quản lý permission.

Không được biến OM thành Administrator.

---

## 7. FE03, FE04, FE05 — Giám sát hoạt động phòng

OM được phép xem:

- Tình trạng phòng.
- Số phòng sẵn sàng.
- Số phòng đang có khách.
- Số phòng chờ dọn.
- Số phòng đang dọn.
- Số phòng chờ kiểm tra.
- Số phòng bị khóa hoặc ngừng sử dụng.
- Tiến độ dọn phòng.
- Kết quả kiểm tra chất lượng phòng.
- Lịch sử liên quan nếu được cấp quyền đọc.
- Khối lượng công việc Housekeeping.

OM không được:

- Trực tiếp nhận hoặc trả phòng thay Receptionist.
- Trực tiếp cập nhật trạng thái phòng theo nghiệp vụ của Receptionist.
- Tạo và phân công cleaning task thay HK Supervisor.
- Thực hiện cleaning task thay HK Staff.
- Kiểm tra chất lượng dọn phòng thay HK Supervisor.
- Xác nhận hoặc từ chối inspection.
- Thực hiện rework thay nhân viên Housekeeping.

Giữ nguyên trách nhiệm chuyên môn của Receptionist, HK Supervisor và HK Staff.

Các màn hình giám sát phải ở chế độ read-only, ngoại trừ những nghiệp vụ quản lý phòng riêng đã được cấp quyền hợp lệ cho OM.

---

## 8. FE06 — Sự cố và bảo trì

### Quyết định nghiệp vụ bắt buộc

OM mới không có chức năng báo cáo sự cố.

Loại bỏ khỏi OM:

- UC049 — Report Incident.
- UC051 — Attach Incident Evidence khi tạo báo cáo sự cố.
- Quyền tạo sự cố.
- Quyền chỉnh sửa báo cáo sự cố.
- Quyền phân công kỹ thuật viên.
- Quyền tiếp nhận công việc sửa chữa.
- Quyền cập nhật tiến độ sửa chữa.
- Quyền xác nhận hoàn thành sửa chữa.
- Quyền đóng sự cố.
- Các quyền thay đổi trạng thái sự cố khác.

OM chỉ được phép:

- Xem danh sách sự cố trong cơ sở.
- Xem chi tiết sự cố.
- Xem người báo cáo.
- Xem người phụ trách.
- Xem mức độ ưu tiên.
- Xem tiến độ xử lý.
- Xem SLA và deadline.
- Xem sự cố quá hạn.
- Xem lịch sử xử lý.
- Xem dashboard sự cố và bảo trì.

Các role cấp dưới có quyền báo cáo sự cố tiếp tục thực hiện theo nghiệp vụ và phạm vi đã được cấp.

Maintenance Lead chịu trách nhiệm điều phối nghiệp vụ bảo trì.

Maintenance Staff chịu trách nhiệm thực hiện công việc kỹ thuật.

Frontend phải loại bỏ nút tạo sự cố và toàn bộ action ghi dữ liệu sự cố đối với OM.

Backend phải từ chối các request ghi dữ liệu FE06 từ OM.

---

## 9. FE07 — Quản lý tài sản

OM mới được kế thừa các năng lực quản lý tài sản của OM và DH cũ.

Các chức năng cần hỗ trợ:

- UC065 — Tạo hồ sơ tài sản.
- UC066 — Cập nhật thông tin tài sản.
- UC067 — Gán tài sản vào vị trí.
- UC068 — Điều chuyển tài sản.
- UC069 — Ghi nhận bàn giao tài sản theo quy tắc được áp dụng.
- UC070 — Cập nhật tình trạng tài sản.
- UC071 — Tra cứu hồ sơ tài sản.
- UC073 — Thống kê tài sản theo vị trí.
- UC074 — Ngừng sử dụng tài sản.

Đối chiếu tên và mã UC với tài liệu/source thực tế trước khi triển khai.

### 9.1. Điều chuyển tài sản

OM được quyền trực tiếp điều chuyển tài sản trong cơ sở mình phụ trách:

- Giữa các phòng.
- Giữa các tầng.
- Giữa các khu vực.
- Giữa các bộ phận.
- Giữa các vị trí tài sản hợp lệ.

Không yêu cầu Department Head phê duyệt điều chuyển.

Không yêu cầu Owner phê duyệt điều chuyển nội bộ thông thường.

Không cấp quyền điều chuyển tài sản sang cơ sở khác trong phạm vi nhiệm vụ này.

### 9.2. Xác nhận bàn giao tài sản

OM được trực tiếp thực hiện nghiệp vụ điều chuyển.

Ưu tiên giữ quy tắc xác nhận bàn giao hợp lệ trong source hiện tại nếu vẫn có thể vận hành sau khi loại bỏ DH.

Không để workflow bị mắc kẹt vì Department Head không còn tồn tại.

Nếu workflow hiện tại bắt buộc DH xác nhận, triển khai phương án tương thích để OM có thể xác nhận bàn giao nội bộ, ghi nhận rõ đây là policy triển khai đề xuất và giữ đầy đủ audit.

Không được mặc nhiên mở rộng quy tắc này sang bàn giao tài sản liên cơ sở.

### 9.3. Audit

Mọi thay đổi tài sản phải lưu được:

- Mã tài sản.
- Vị trí cũ.
- Vị trí mới.
- Bộ phận cũ và mới nếu có.
- Người thực hiện.
- Người xác nhận nếu có.
- Thời điểm.
- Lý do điều chuyển.
- Trạng thái trước và sau.

Không xóa hoặc ghi đè lịch sử điều chuyển.

---

## 10. FE08 — Quản lý và kiểm soát kho

### 10.1. Quyền của OM

OM được phép:

- Xem tổng quan kho trong cơ sở.
- Xem danh sách vật tư.
- Xem tồn kho.
- Xem sổ kho.
- Xem lịch sử nhập xuất.
- Xem kết quả kiểm kê.
- Xem chênh lệch kiểm kê.
- Điều chỉnh tồn kho theo nghiệp vụ hợp lệ.
- Đảo giao dịch kho theo nghiệp vụ hợp lệ.
- Giám sát điều chuyển vật tư.
- Tạo và phê duyệt yêu cầu điều chuyển liên cơ sở theo quy định bên dưới.

### 10.2. Quyền của Storekeeper

Giữ nguyên trách nhiệm trực tiếp của Storekeeper đối với:

- Quản lý danh mục vật tư.
- Đơn vị tính.
- Định mức tồn kho.
- Lập phiếu nhập kho.
- Xác nhận nhập kho.
- Xuất kho.
- Nhận vật tư hoàn trả.
- Thực hiện điều chuyển kho.
- Xác nhận nhận hàng điều chuyển.
- Kiểm kê.
- Đối chiếu chênh lệch kiểm kê.
- Cấp phát vật tư.
- Tạo yêu cầu bổ sung vật tư.

OM không được tiếp quản các chức năng trực tiếp của Storekeeper.

### 10.3. Kiểm soát giao dịch

Điều chỉnh tồn kho và đảo giao dịch phải:

- Kiểm tra branch scope.
- Có lý do hợp lệ.
- Có audit trail.
- Kiểm tra trạng thái giao dịch.
- Kiểm tra số lượng tồn khả dụng nếu liên quan.
- Bảo toàn lịch sử giao dịch gốc.
- Ngăn xử lý trùng lặp.
- Không tạo tồn kho âm trái quy định.

Không cho phép sửa trực tiếp stock balance để bỏ qua business workflow.

---

## 11. Nghiệp vụ mới — Yêu cầu điều chuyển vật tư liên cơ sở

Đây là nghiệp vụ trọng điểm cần triển khai hoàn chỉnh End-to-End.

### 11.1. Quy tắc nghiệp vụ

Giả sử:

- OM A quản lý cơ sở A.
- OM B quản lý cơ sở B.
- Storekeeper A phụ trách kho tại cơ sở A.
- Storekeeper B phụ trách kho tại cơ sở B.

OM A được tạo yêu cầu lấy vật tư từ cơ sở B về cơ sở A.

Người phê duyệt là OM B.

OM A không được tự phê duyệt yêu cầu do mình gửi.

OM B không được phê duyệt yêu cầu không liên quan đến cơ sở B.

Owner không tham gia phê duyệt nghiệp vụ điều chuyển thông thường.

### 11.2. Quy trình bắt buộc

Bước 1: OM A xem danh sách kho của cơ sở khác.

Bước 2: OM A lựa chọn:

- Cơ sở cung cấp.
- Kho cung cấp.
- Kho nhận thuộc cơ sở mình.
- Vật tư.
- Số lượng.
- Đơn vị tính.
- Lý do yêu cầu.
- Thời hạn mong muốn nếu được hỗ trợ.

Bước 3: OM A gửi yêu cầu điều chuyển.

Bước 4: Hệ thống gửi thông báo cho OM B.

Bước 5: OM B xem xét yêu cầu và kiểm tra tồn khả dụng.

Bước 6: OM B phê duyệt hoặc từ chối.

Nếu từ chối, yêu cầu phải lưu lý do và thông báo lại cho OM A.

Nếu phê duyệt, hệ thống chuyển yêu cầu đến Storekeeper B.

Bước 7: Storekeeper B chuẩn bị vật tư và thực hiện xuất kho theo quy trình điều chuyển hiện có.

Bước 8: Vật tư chuyển sang trạng thái đang vận chuyển.

Bước 9: Storekeeper A kiểm đếm số lượng thực nhận.

Bước 10: Storekeeper A xác nhận nhận vật tư và ghi nhận nhập kho.

Bước 11: Hệ thống hoàn tất giao dịch điều chuyển khi đáp ứng đầy đủ điều kiện.

Bước 12: OM A và OM B theo dõi được kết quả và lịch sử xử lý.

### 11.3. Trạng thái đề xuất

- Pending Approval.
- Approved.
- Rejected.
- Preparing.
- In Transit.
- Received.
- Completed.
- Cancelled.

Ưu tiên tái sử dụng status enum, stock transfer entity và workflow hiện có.

Không tạo hệ thống trạng thái mới trùng lặp nếu source hiện tại đã đáp ứng được nghiệp vụ.

### 11.4. Quy tắc tồn kho

Tạo yêu cầu:

- Không làm giảm tồn kho nguồn.
- Không làm tăng tồn kho đích.

Phê duyệt yêu cầu:

- Không trực tiếp ghi tăng hoặc giảm stock balance.
- Có thể sử dụng cơ chế reservation nếu hệ thống hiện tại hỗ trợ.

Xác nhận xuất kho:

- Giảm tồn kho nguồn đúng số lượng xuất.
- Ghi nhận vật tư đang vận chuyển theo mô hình hiện có.

Xác nhận nhận hàng:

- Tăng tồn kho đích theo số lượng thực nhận hợp lệ.
- Không được nhập kho hai lần.
- Không được tự động cộng số lượng chưa thực nhận.

Ví dụ E2E:

- Kho A có 20 chai dầu gội.
- Kho B có 200 chai dầu gội.
- OM A yêu cầu 50 chai.
- OM B phê duyệt.
- Storekeeper B xuất 50 chai.
- Kho B còn 150 chai.
- Kho A vẫn còn 20 chai trong thời gian vận chuyển.
- Storekeeper A xác nhận nhận đủ 50 chai.
- Kho A có 70 chai.
- Tổng vật tư vẫn là 220 chai nếu không phát sinh nghiệp vụ khác.

### 11.5. Xử lý ngoại lệ

Bắt buộc xử lý hoặc kiểm soát an toàn các trường hợp:

- Không đủ tồn khả dụng.
- Tồn kho thay đổi sau khi phê duyệt.
- Vật tư hoặc kho không còn hoạt động.
- Đơn vị tính không hợp lệ.
- Yêu cầu bị phê duyệt đồng thời.
- Request gửi trùng.
- Xuất kho trùng.
- Nhận hàng trùng.
- Nhận thiếu so với số lượng đã xuất.
- Nhận thừa so với số lượng đã xuất.
- Hủy trước khi xuất kho.
- Cố tình hủy sau khi đã xuất kho.
- Người không thuộc cơ sở cung cấp cố tình phê duyệt.
- Người không thuộc cơ sở nhận cố tình xác nhận nhận hàng.

Không tự động coi nhận thiếu là hoàn thành toàn bộ giao dịch.

Phải giữ được lịch sử và số lượng chênh lệch để xử lý tiếp.

Sử dụng transaction, concurrency control và idempotency phù hợp với kiến trúc hiện có.

---

## 12. FE09 — Yêu cầu cấp phát vật tư nội bộ

Phân biệt rõ:

1. Yêu cầu cấp phát vật tư trong cùng cơ sở.
2. Yêu cầu điều chuyển vật tư giữa các cơ sở.

Không sử dụng chung một authorization rule cho hai nghiệp vụ này.

Đối với cấp phát nội bộ:

- Nhân sự các bộ phận tạo yêu cầu vật tư theo quyền hiện có.
- OM tiếp nhận, xem xét và phê duyệt hoặc từ chối thay cho DH.
- Storekeeper kiểm tra tồn khả dụng.
- Storekeeper thực hiện cấp phát toàn bộ hoặc cấp phát một phần.
- OM theo dõi tiến độ và lịch sử.

Không cấp cho OM quyền trực tiếp cấp phát vật tư.

Quy tắc OM tự tạo và tự duyệt yêu cầu cấp phát nội bộ chưa được chốt mới. Giữ business rule an toàn hiện có, không tự ý mở rộng quyền tự phê duyệt.

Không áp dụng quy tắc tự xác nhận công việc FE11 sang FE09.

---

## 13. FE10 — Bổ sung vật tư

OM được phép:

- Giám sát vật tư dưới định mức.
- Xem các yêu cầu bổ sung vật tư.
- Theo dõi tình trạng bổ sung.
- Kiểm tra nhu cầu vật tư của cơ sở.
- Sử dụng nghiệp vụ điều chuyển liên cơ sở khi cần thiết.

Storekeeper tiếp tục chịu trách nhiệm tạo và theo dõi yêu cầu bổ sung theo thiết kế hiện có.

Không bổ sung module mua hàng, kế toán hoặc quản lý nhà cung cấp ngoài phạm vi nếu hệ thống chưa hỗ trợ.

---

## 14. FE11 — Công việc vận hành

OM mới tiếp quản chức năng quản lý công việc vận hành của DH.

### 14.1. Các chức năng

- UC102 — Tạo công việc vận hành.
- UC103 — Thiết lập mức độ ưu tiên và thời hạn.
- UC104 — Phân công và phân công lại.
- UC105 — Kiểm tra và xác nhận hoàn thành.
- UC106 — Mở lại công việc.
- UC113 — Theo dõi khối lượng công việc và nhật ký thời gian.

Đồng thời bổ sung hoặc cấp lại những permission thực thi cần thiết để OM có thể tự thực hiện công việc của mình.

### 14.2. Tạo công việc

Hồ sơ công việc hỗ trợ các trường phù hợp với source hiện tại:

- Tên công việc.
- Mô tả.
- Cơ sở.
- Bộ phận thực hiện.
- Người được giao.
- Người tạo.
- Mức độ ưu tiên.
- Thời hạn.
- Trạng thái.
- Kết quả thực hiện.
- Bằng chứng nếu nghiệp vụ yêu cầu.
- Lịch sử hoạt động.

### 14.3. Giao công việc cho cấp dưới

OM được phép:

- Giao nhiệm vụ vận hành chung cho nhân sự hợp lệ trong cơ sở.
- Giao việc cho HK Supervisor.
- Giao việc cho Maintenance Lead.
- Giao việc cho Storekeeper.
- Giao việc cho các nhân sự có quyền thực hiện operational task.
- Thay đổi người phụ trách theo business rule.
- Kiểm tra kết quả.
- Xác nhận hoàn thành.
- Mở lại công việc nếu chưa đạt yêu cầu.

Không sử dụng FE11 để bỏ qua quy trình chuyên môn của FE04, FE05, FE06 hoặc FE08.

### 14.4. OM tự thực hiện và tự xác nhận

Đây là quyết định nghiệp vụ đã được phê duyệt.

OM được phép:

- Tạo công việc cho chính mình.
- Tự phân công công việc cho mình.
- Tiếp nhận công việc.
- Bắt đầu thực hiện.
- Cập nhật tiến độ.
- Gửi kết quả thực hiện.
- Tự kiểm tra.
- Tự xác nhận hoàn thành.
- Mở lại công việc của mình nếu trạng thái cho phép.

Không yêu cầu người quản lý thứ hai hoặc Owner xác nhận.

Loại bỏ hoặc điều chỉnh các điều kiện trong FE11 đang cấm OM xác nhận công việc do chính mình thực hiện.

Chỉ áp dụng ngoại lệ này cho Operational Task.

Vẫn phải lưu đầy đủ actor, timestamp, trạng thái trước/sau và lịch sử nghiệp vụ.

---

## 15. FE12 — Ca làm việc và bàn giao

OM được quản lý ca làm việc của nhân sự trong cơ sở được bàn giao.

Các chức năng:

- Xem danh sách ca làm việc.
- Quản lý ngày làm việc.
- Phân công ca.
- Điều chỉnh ca theo điều kiện nghiệp vụ.
- Xem nhân sự đang làm việc.
- Xem lịch sử làm việc.
- Theo dõi khối lượng công việc.
- Quản lý và giám sát bàn giao ca.
- Kiểm tra tình trạng bàn giao chưa hoàn thành.

Tái sử dụng UC138, UC140 và các nghiệp vụ liên quan nếu có trong source.

Không cho phép OM sửa hoặc quản lý ca làm việc của cơ sở khác.

Không xây dựng thêm nghiệp vụ tính lương ngoài phạm vi hệ thống.

---

## 16. FE13 — Yêu cầu nội bộ giữa các bộ phận

### Quyết định nghiệp vụ bắt buộc

Loại bỏ bước phê duyệt trung gian của Department Head.

Các bộ phận được gửi yêu cầu trực tiếp đến bộ phận tiếp nhận.

OM chỉ có quyền xem và giám sát.

### 16.1. Luồng mới

Bộ phận gửi tạo yêu cầu.

Bộ phận nhận trực tiếp tiếp nhận.

Người phụ trách bộ phận nhận phân công người thực hiện.

Nhân viên thực hiện và cập nhật kết quả.

Bộ phận chịu trách nhiệm xác nhận hoàn thành.

Bộ phận gửi kiểm tra kết quả và phản hồi theo business rule.

Không có bước nào phải chờ OM phê duyệt.

### 16.2. Quyền của OM

OM được phép:

- Xem danh sách yêu cầu.
- Xem chi tiết.
- Xem bộ phận gửi.
- Xem bộ phận nhận.
- Xem người phụ trách.
- Xem trạng thái.
- Xem tiến độ.
- Xem thời hạn.
- Xem yêu cầu quá hạn.
- Xem lịch sử xử lý.
- Tìm kiếm và lọc.

OM không được:

- Tạo yêu cầu nội bộ.
- Phê duyệt yêu cầu.
- Từ chối yêu cầu.
- Tiếp nhận thay bộ phận nhận.
- Phân công người xử lý.
- Cập nhật tiến độ.
- Thay đổi trạng thái.
- Bàn giao yêu cầu.
- Xác nhận hoàn thành.
- Mở lại yêu cầu.
- Thực hiện escalation bằng thao tác thay đổi hồ sơ.

### 16.3. Phân bổ lại trách nhiệm DH cũ

Rà soát UC114–UC122 và toàn bộ writer hiện có.

Chuyển những trách nhiệm cần thiết sang người phụ trách bộ phận tiếp nhận, ví dụ:

- HK Supervisor đối với Housekeeping.
- Maintenance Lead đối với Maintenance.
- Storekeeper đối với kho.
- Người phụ trách hợp lệ của bộ phận khác theo cơ cấu hiện tại.

Không cấp tất cả quyền FE13 cho mọi nhân viên.

Chỉ cấp quyền tương ứng với vai trò của họ trong từng yêu cầu.

UC116 Accept/Reject nếu cần giữ lại thì sử dụng như xác nhận tiếp nhận hoặc từ chối tiếp nhận của bộ phận nhận, không phải bước phê duyệt trung gian của quản lý.

Các yêu cầu quá hạn được hệ thống cảnh báo cho người phụ trách và OM có thẩm quyền xem.

Không để workflow phụ thuộc vào một role DH đã bị loại bỏ.

---

## 17. FE14 — Dashboard và thông báo

### 17.1. Dashboard OM mới

Thiết kế Dashboard tổng hợp theo cơ sở được giao.

Các nhóm dữ liệu:

**Tổng quan phòng**

- Tổng số phòng.
- Phòng sẵn sàng.
- Phòng đang có khách.
- Phòng chờ dọn.
- Phòng đang dọn.
- Phòng chờ kiểm tra.
- Phòng bị khóa hoặc ngừng sử dụng.

**Công việc vận hành**

- Tổng số công việc.
- Chờ phân công.
- Đang thực hiện.
- Chờ xác nhận.
- Hoàn thành.
- Quá hạn.

**Sự cố và bảo trì**

- Sự cố đang mở.
- Sự cố đang xử lý.
- Sự cố quá hạn.
- Sự cố đã hoàn thành.

**Tài sản và kho**

- Tổng tài sản.
- Tài sản đang sử dụng.
- Tài sản ngừng sử dụng.
- Vật tư dưới định mức.
- Chênh lệch kiểm kê.
- Yêu cầu cấp phát chờ duyệt.
- Yêu cầu điều chuyển liên cơ sở chờ duyệt.
- Vật tư đang vận chuyển.

**Yêu cầu nội bộ**

- Chờ tiếp nhận.
- Đang xử lý.
- Hoàn thành.
- Quá hạn.

Chỉ sử dụng dữ liệu thực tế từ API.

Không hardcode KPI hoặc sử dụng mock data trong production.

### 17.2. Công việc cần xử lý — Action Center

Không tạo một module hoặc Sidebar độc lập cho Action Center.

Tích hợp khu vực Công việc cần xử lý vào Dashboard OM.

Hiển thị:

- Yêu cầu cấp phát vật tư chờ OM duyệt.
- Yêu cầu điều chuyển từ cơ sở khác chờ OM duyệt.
- Công việc vận hành chờ phân công.
- Công việc vận hành chờ xác nhận.
- Công việc vận hành quá hạn.
- Chênh lệch kiểm kê cần xử lý.
- Tài sản cần xử lý bàn giao.
- Ca làm việc cần kiểm tra bàn giao.

Mỗi mục phải điều hướng đến hồ sơ nghiệp vụ gốc.

Không tạo dữ liệu công việc trùng lặp.

Không tạo bản sao trạng thái riêng cho Action Center.

Sự cố và yêu cầu nội bộ quá hạn chỉ hiển thị dưới dạng cảnh báo giám sát.

### 17.3. Notifications

Cập nhật toàn bộ notification recipient resolver đang phụ thuộc Department Head.

Bảo đảm thông báo đúng người và đúng cơ sở.

Bổ sung thông báo cho quy trình điều chuyển liên cơ sở:

- Yêu cầu mới gửi đến OM cung cấp.
- Yêu cầu được phê duyệt.
- Yêu cầu bị từ chối.
- Storekeeper nhận nhiệm vụ xuất kho.
- Vật tư đã xuất.
- Storekeeper nhận nhiệm vụ tiếp nhận.
- Vật tư đã nhận.
- Điều chuyển hoàn thành.
- Điều chuyển phát sinh chênh lệch.

Không gửi thông báo cho toàn bộ OM trên hệ thống.

Chỉ gửi cho những người thuộc phạm vi và có liên quan trực tiếp.

Không tạo thông báo trùng khi request được retry.

---

## 18. FE15 — Báo cáo vận hành

OM được phép xem, tạo và xuất các báo cáo được hệ thống hỗ trợ trong phạm vi cơ sở mình.

Bao gồm:

- Báo cáo tình trạng phòng.
- Báo cáo hoạt động Housekeeping.
- Báo cáo sự cố và bảo trì.
- Báo cáo công việc vận hành.
- Báo cáo tài sản.
- Báo cáo tồn kho.
- Báo cáo giao dịch kho.
- Báo cáo yêu cầu cấp phát vật tư.
- Báo cáo điều chuyển vật tư có liên quan đến cơ sở.
- Báo cáo ca làm việc.
- Báo cáo yêu cầu nội bộ.

Chỉ triển khai những loại báo cáo có dữ liệu và nghiệp vụ được hỗ trợ.

Không mở rộng quyền xem báo cáo của cơ sở khác.

Owner giữ quyền giám sát cấp cao theo thiết kế hiện tại.

---

## 19. Redesign Sidebar của Operations Manager

Thiết kế lại Sidebar theo cấu trúc:

### TỔNG QUAN

- Dashboard vận hành.

### QUẢN LÝ VẬN HÀNH

- Tình trạng phòng.
- Cơ cấu cơ sở.
- Công việc vận hành.
- Ca làm việc.

### TÀI SẢN VÀ VẬT TƯ

- Quản lý tài sản.
- Kiểm soát kho.
- Yêu cầu cấp phát vật tư.
- Điều chuyển vật tư.

### GIÁM SÁT

- Sự cố và bảo trì.
- Yêu cầu nội bộ.

### BÁO CÁO VÀ THÔNG BÁO

- Báo cáo vận hành.
- Thông báo.

### CÁ NHÂN

- Tài khoản cá nhân.

Yêu cầu:

- Không hiển thị menu dành riêng cho Department Head.
- Không hiển thị menu tạo sự cố cho OM.
- Không hiển thị menu trực tiếp dọn phòng.
- Không hiển thị menu kiểm tra chất lượng dọn phòng dưới dạng thao tác nghiệp vụ.
- Không hiển thị menu nhập xuất kho trực tiếp.
- Không tạo menu Công việc cần xử lý độc lập.
- Không tạo Sidebar quá nhiều menu con không cần thiết.

Các chức năng thao tác như tạo, sửa, phân công, xác nhận, điều chuyển và phê duyệt phải được đặt trong trang nghiệp vụ tương ứng.

---

## 20. Yêu cầu thiết kế giao diện

Giữ Design System và phong cách giao diện hiện tại của HotelOps.

Không redesign toàn bộ giao diện của các role khác nếu không cần thiết.

Áp dụng các quy tắc:

### Bảng dữ liệu

- Header rõ ràng.
- Các cột dữ liệu nhất quán.
- Badge trạng thái thống nhất với design system.
- Phân trang.
- Tìm kiếm.
- Empty state.
- Loading state.
- Error state.
- Action menu theo permission.
- Responsive layout phù hợp.

### Bộ lọc

- Mặc định ẩn khi mở trang.
- Có nút Bộ lọc để mở hoặc đóng.
- Có chức năng xóa bộ lọc.
- Không chiếm diện tích khi không sử dụng.

### Popup và Drawer

- Tạo mới, cập nhật, phân công, điều chuyển, phê duyệt và xác nhận sử dụng Modal hoặc Popup phù hợp.
- Xem chi tiết hồ sơ có thể dùng Drawer.
- Không mở form thao tác nối dài bên dưới bảng dữ liệu.
- Có validation.
- Có loading state khi submit.
- Chống double submit.
- Có thông báo thành công hoặc thất bại.
- Sau thao tác thành công, làm mới dữ liệu liên quan.

### Icon

Ưu tiên sử dụng Lucide Icons từ thư viện hiện tại.

https://lucide.dev/icons/

Không sử dụng emoji thay cho icon nghiệp vụ.

### Trang chỉ đọc

Đối với Sự cố và bảo trì, Yêu cầu nội bộ và các trang giám sát:

- Chỉ hiển thị action xem chi tiết.
- Không hiển thị nút tạo mới.
- Không hiển thị nút sửa.
- Không hiển thị nút xóa.
- Không hiển thị nút phê duyệt.
- Không hiển thị nút phân công.
- Không hiển thị action thay đổi trạng thái.

---

## 21. Giao diện điều chuyển vật tư

Trang Điều chuyển vật tư cần tối thiểu ba tab:

### Yêu cầu đã gửi

Hiển thị yêu cầu do cơ sở hiện tại gửi đến cơ sở khác.

Actions:

- Tạo yêu cầu.
- Xem chi tiết.
- Theo dõi tiến độ.
- Hủy nếu còn đủ điều kiện.

Không có nút tự phê duyệt.

### Yêu cầu nhận được

Hiển thị yêu cầu do cơ sở khác gửi đến cơ sở hiện tại.

Actions:

- Xem chi tiết.
- Kiểm tra tồn khả dụng.
- Phê duyệt.
- Từ chối.
- Theo dõi tiến độ xử lý.

Chỉ hiển thị action phê duyệt hoặc từ chối khi trạng thái hợp lệ.

### Lịch sử điều chuyển

Hiển thị các yêu cầu và giao dịch đã hoàn tất hoặc kết thúc có liên quan đến cơ sở hiện tại.

Các cột đề xuất:

- Mã yêu cầu.
- Cơ sở gửi yêu cầu.
- Cơ sở cung cấp.
- Kho nguồn.
- Kho nhận.
- Vật tư.
- Số lượng yêu cầu.
- Số lượng đã duyệt nếu có.
- Số lượng đã xuất.
- Số lượng thực nhận.
- Người yêu cầu.
- Người phê duyệt.
- Ngày tạo.
- Trạng thái.
- Actions.

Popup tạo yêu cầu phải giới hạn kho nhận trong cơ sở của OM hiện tại.

Kho nguồn phải thuộc cơ sở khác và được phép phục vụ điều chuyển.

---

## 22. Migration Department Head

Rà soát toàn bộ references đến Department Head trong:

- Database roles.
- User role mappings.
- Permission mappings.
- Authentication claims.
- Authorization policies.
- Frontend constants.
- Routes.
- Sidebar.
- API controllers.
- Services.
- Workflow handlers.
- Notification resolvers.
- Dashboard queries.
- Report queries.
- Seed data.
- Test fixtures.
- Documentation.

Tạo migration an toàn để chuyển đổi tài khoản và quyền.

Yêu cầu:

1. Giữ nguyên tài khoản và user ID.
2. Giữ nguyên thông tin đăng nhập.
3. Bảo toàn dữ liệu lịch sử.
4. Bảo toàn audit trail.
5. Bảo toàn foreign keys hợp lệ.
6. Không tạo tài khoản trùng.
7. Không tạo role mapping trùng.
8. Không mất các công việc đang được giao.
9. Không làm mất yêu cầu đang chờ xử lý.
10. Có phương án rollback.

Không tự động chuyển mọi tài khoản DH thành OM có toàn quyền trên toàn hệ thống.

Chỉ chuyển sang OM trong cơ sở được bàn giao và theo mapping tài khoản hợp lệ.

Nếu một cơ sở có nhiều DH cũ nhưng chưa có quyết định rõ tài khoản nào trở thành OM, không tự ý nâng quyền quản lý toàn cơ sở cho tất cả. Giữ tài khoản và dữ liệu, báo cáo mapping cần xác định, cung cấp migration có kiểm soát.

Đối với lịch sử nghiệp vụ, giữ nguyên thông tin role tại thời điểm sự kiện xảy ra nếu có.

Không xóa cứng role DH khi còn references lịch sử hoặc dữ liệu phụ thuộc.

Có thể giữ role code DH ở trạng thái legacy/inactive trong giai đoạn chuyển đổi.

Không cho phép gán DH mới sau khi migration hoàn tất.

Không chạy migration phá hủy dữ liệu hoặc tác động database production ngoài môi trường đã được cấp quyền.

---

## 23. Bảo toàn quyền của các role khác

Không được làm hỏng các role:

- Administrator.
- Owner.
- Receptionist.
- HK Supervisor.
- HK Staff.
- Maintenance Lead.
- Maintenance Staff.
- Storekeeper.

Các nghiệp vụ chuyên môn hiện tại phải tiếp tục hoạt động.

Đặc biệt:

- Receptionist giữ nghiệp vụ phòng được cấp.
- HK Supervisor giữ quyền điều phối Housekeeping.
- HK Staff giữ quyền thực hiện cleaning task.
- Maintenance Lead giữ quyền điều phối bảo trì.
- Maintenance Staff giữ quyền thực hiện bảo trì.
- Storekeeper giữ quyền trực tiếp thực hiện nghiệp vụ kho.
- Owner giữ quyền giám sát cấp cao.
- Administrator giữ quyền quản trị hệ thống.

Không sử dụng OM làm role thay thế cho mọi chức năng của DH nếu chức năng đó đã được quyết định chuyển cho bộ phận chuyên môn.

---

## 24. Kiểm thử bắt buộc

### TEST 01 — Role Consolidation

- Tài khoản OM đăng nhập thành công.
- Nhận đúng permission.
- Nhận đúng branch scope.
- Không còn Sidebar DH.
- Tài khoản được migration hoạt động theo mapping hợp lệ.
- Không có privilege escalation ngoài phạm vi.

### TEST 02 — Branch Isolation

- OM A xem dữ liệu cơ sở A thành công.
- OM A không xem được phòng của cơ sở B.
- OM A không xem được tài sản cơ sở B.
- OM A không sửa công việc cơ sở B.
- OM A không xem được sổ kho cơ sở B.
- OM A xem được danh sách kho cơ sở B phục vụ điều chuyển.
- Thay đổi branchId thủ công trong API request không vượt được authorization.

### TEST 03 — Incident Read-only

- OM xem danh sách sự cố.
- OM xem chi tiết sự cố.
- OM xem SLA và tiến độ.
- Không xuất hiện nút tạo sự cố.
- Gọi trực tiếp API tạo sự cố bị từ chối.
- Gọi API phân công hoặc thay đổi trạng thái bị từ chối.

### TEST 04 — Internal Request Read-only

- Bộ phận A gửi yêu cầu đến bộ phận B.
- Bộ phận B tiếp nhận trực tiếp.
- Không cần DH hoặc OM duyệt.
- Bộ phận B phân công và xử lý.
- OM xem được tiến độ.
- OM không gọi được API thay đổi yêu cầu.

### TEST 05 — Asset Transfer

- OM tạo hoặc chọn tài sản hợp lệ.
- Điều chuyển sang vị trí khác trong cùng cơ sở.
- Xác nhận theo workflow được triển khai.
- Vị trí tài sản được cập nhật.
- Lịch sử được lưu.
- Không thể điều chuyển sang cơ sở khác trái quyền.

### TEST 06 — Operational Task Assignment

- OM tạo công việc.
- Giao cho nhân viên hợp lệ.
- Nhân viên thực hiện.
- Nhân viên gửi kết quả.
- OM xác nhận hoàn thành.
- Lịch sử hoạt động đầy đủ.

### TEST 07 — OM Self-completion

- OM tạo công việc cho mình.
- Tự phân công.
- Bắt đầu thực hiện.
- Cập nhật kết quả.
- Tự xác nhận hoàn thành.
- Không yêu cầu Owner hoặc người thứ hai.
- Audit ghi nhận đúng identity.
- Ngoại lệ không ảnh hưởng các nghiệp vụ ngoài FE11.

### TEST 08 — Material Request Approval

- Nhân viên tạo yêu cầu cấp phát vật tư.
- OM phê duyệt.
- Storekeeper cấp phát.
- Tồn kho cập nhật đúng.
- OM không trực tiếp cấp phát.
- Không vượt branch scope.

### TEST 09 — Cross-branch Material Transfer

- OM A tạo yêu cầu 50 vật tư từ cơ sở B.
- OM B nhận thông báo.
- OM B phê duyệt.
- Storekeeper B xuất kho.
- Storekeeper A nhận hàng.
- Tồn kho hai bên cập nhật đúng.
- OM A và OM B theo dõi được kết quả.
- OM cơ sở C không truy cập trái phép.

### TEST 10 — Unauthorized Approval

- OM A không tự duyệt yêu cầu gửi đi.
- OM C không duyệt yêu cầu giữa A và B.
- Storekeeper không phê duyệt thay OM.
- OM không xác nhận nhập hoặc xuất kho thay Storekeeper.

### TEST 11 — Inventory Concurrency

- Hai request phê duyệt đồng thời.
- Xuất kho đồng thời.
- Retry request.
- Double submit.
- Nhận hàng trùng.
- Không ghi sổ trùng.
- Không làm âm tồn kho trái quy định.

### TEST 12 — Transfer Exceptions

- Từ chối yêu cầu.
- Không đủ tồn.
- Hủy trước xuất.
- Hủy sau xuất bị chặn hoặc xử lý đúng workflow.
- Nhận thiếu.
- Nhận thừa.
- Vật tư ngừng hoạt động.
- Kho không hợp lệ.

### TEST 13 — Dashboard

- KPI đúng dữ liệu thực tế.
- Action Center đúng hồ sơ chờ xử lý.
- Không hiển thị dữ liệu cơ sở khác.
- Click mở đúng hồ sơ nguồn.
- Không hiển thị action ngoài quyền OM.

### TEST 14 — Regression

Kiểm tra toàn bộ nghiệp vụ của các role khác sau khi loại bỏ DH khỏi cơ cấu vận hành.

### TEST 15 — Migration

- Migration chạy thành công trong môi trường test được phép.
- Không mất dữ liệu.
- Không hỏng foreign keys.
- Không tạo duplicate mapping.
- Không còn workflow đang chờ actor DH không tồn tại.
- Rollback được kiểm tra phù hợp.

---

## 25. Trình tự triển khai

Thực hiện theo thứ tự:

### Phase 1 — Source Audit

- Rà soát architecture.
- Rà soát roles và permissions.
- Xác định các dependencies của DH.
- Lập mapping quyền cũ và mới.
- Xác định các business workflow bị ảnh hưởng.

### Phase 2 — Backend Authorization

- Cập nhật role OM.
- Cập nhật permissions.
- Cập nhật branch scope.
- Loại bỏ quyền không được phép.
- Cập nhật authorization policies.
- Kiểm tra resource ownership.

### Phase 3 — Business Workflow

- FE07 Asset.
- FE08 Inventory.
- Điều chuyển vật tư liên cơ sở.
- FE09 Material Request.
- FE11 Operational Task.
- FE12 Work Time.
- FE13 Internal Request.
- FE06 Read-only.

### Phase 4 — Database Migration

- Role mappings.
- Workflow references.
- Legacy compatibility.
- Migration scripts.
- Rollback scripts.
- Audit preservation.

### Phase 5 — Frontend

- Auth context.
- Route guards.
- Sidebar.
- Dashboard.
- Action Center.
- Các trang nghiệp vụ.
- Popup và Drawer.
- Permission-based actions.
- Loading/error/empty states.

### Phase 6 — Notifications

- Recipient resolution.
- Thông báo điều chuyển.
- Thông báo phê duyệt.
- Cảnh báo quá hạn.
- Loại bỏ dependencies vào DH.

### Phase 7 — Testing & Fixing

- Backend build.
- Backend unit tests.
- Integration tests.
- Frontend typecheck.
- Frontend build.
- Frontend tests.
- E2E tests.
- Authorization negative tests.
- Database migration validation.
- Sửa lỗi phát sinh.

### Phase 8 — Documentation

Cập nhật tài liệu phản ánh đúng source mới.

Không dành phần lớn thời gian chỉ viết tài liệu. Ưu tiên hoàn thành code và kiểm thử trước.

---

## 26. Cập nhật tài liệu dự án

Sau khi triển khai, cập nhật các tài liệu hiện có hoặc tạo tài liệu thay đổi phù hợp:

- Role Permission Matrix.
- Use Case Specifications.
- Business Workflows.
- API Specifications.
- Database Specifications.
- Requirement Traceability Matrix.
- SRS Change Notes.
- Use Case Diagram.
- E2E Test Cases.

Ghi nhận rõ:

- Các UC được kế thừa.
- Các UC bị loại bỏ khỏi OM.
- Các UC chuyển actor.
- Các UC thay đổi business rule.
- Các UC mới của điều chuyển vật tư liên cơ sở.
- Các UC dùng chung với role khác.
- Các chức năng chỉ đọc.
- Các chức năng trực tiếp thực thi.

Không cố định tổng số UC theo số liệu cũ. Thống kê lại sau khi hoàn thành mapping và triển khai.

---

## 27. Điều kiện nghiệm thu

Nhiệm vụ chỉ được coi là hoàn thành khi:

- OM và DH đã được hợp nhất về mô hình vận hành.
- Không còn dependency runtime bắt buộc role DH.
- OM chỉ quản lý cơ sở được giao.
- Ngoại lệ tra cứu kho liên cơ sở hoạt động đúng.
- OM tạo được yêu cầu điều chuyển vật tư.
- OM bên cung cấp phê duyệt được yêu cầu.
- Hai Storekeeper thực hiện xuất và nhận hàng thành công.
- Tồn kho được cập nhật chính xác.
- OM điều chuyển được tài sản trong cơ sở.
- OM tự thực hiện và tự xác nhận được công việc vận hành.
- OM không thể báo cáo sự cố.
- OM chỉ đọc yêu cầu nội bộ.
- Các bộ phận gửi và nhận yêu cầu trực tiếp không cần DH/OM phê duyệt.
- Sidebar đúng thiết kế mới.
- Dashboard sử dụng dữ liệu thực tế.
- Action Center hoạt động.
- Backend authorization chặn các thao tác trái quyền.
- Build và test đạt yêu cầu.
- Không phát sinh regression nghiêm trọng đối với các role khác.

---

## 28. Báo cáo kết quả cuối cùng

Sau khi hoàn thành, cung cấp báo cáo:

### IMPLEMENTATION STATUS

- ROLE_CONSOLIDATION:
- DH_RUNTIME_DEPENDENCY_REMOVED:
- OM_PERMISSION_MATRIX:
- BRANCH_SCOPE:
- CROSS_BRANCH_TRANSFER:
- ASSET_TRANSFER:
- OPERATIONAL_TASK_SELF_COMPLETION:
- INCIDENT_READ_ONLY:
- INTERNAL_REQUEST_READ_ONLY:
- FRONTEND_REDESIGN:
- DASHBOARD_ACTION_CENTER:
- DATABASE_MIGRATION:
- NOTIFICATIONS:
- REGRESSION_TESTS:

### TEST RESULTS

- Backend Build:
- Backend Unit Tests:
- Integration Tests:
- Frontend Typecheck:
- Frontend Build:
- Frontend Tests:
- E2E Tests:
- Authorization Negative Tests:
- Migration Validation:

### UC IMPACT

- Tổng UC của OM trước thay đổi.
- Tổng UC kế thừa từ DH.
- Tổng UC bị loại bỏ.
- Tổng UC chuyển actor.
- Tổng UC thay đổi nghiệp vụ.
- Tổng UC mới.
- Tổng UC của OM sau triển khai.

### SOURCE CHANGES

- Danh sách file đã sửa.
- Danh sách file đã tạo.
- Danh sách API thay đổi.
- Danh sách permission thay đổi.
- Danh sách database migration.
- Những thay đổi quan trọng về business workflow.

### REMAINING ISSUES

Chỉ báo cáo những vấn đề thực sự chưa hoàn thành, chưa được xác minh hoặc cần quyết định nghiệp vụ.

Không tuyên bố PASS cho các bài test chưa chạy.

Không sử dụng số lượng file hoặc số lượng dòng code thay cho mức độ hoàn thành chức năng.

---

## 29. Chỉ thị thực thi cuối cùng

Bắt đầu thực hiện ngay trên workspace HotelOps hiện tại.

Ưu tiên tái sử dụng source code, components, API, services và database schema hiện có.

Không xây dựng lại toàn bộ hệ thống nếu chỉ cần refactor.

Không tạo các implementation song song hoặc duplicate module.

Không sử dụng mock API để che giấu nghiệp vụ Backend chưa hoàn thành.

Không bypass authorization để làm cho UI hoạt động.

Không hardcode dữ liệu demo.

Chủ động giải quyết các lỗi kỹ thuật trong quá trình triển khai.

Thực hiện liên tục qua các phase, không dừng để xin xác nhận sau mỗi phase.

Chỉ yêu cầu quyết định bổ sung khi gặp vấn đề nghiệp vụ thực sự chưa được xác định hoặc thao tác có nguy cơ mất dữ liệu, nâng quyền trái phép hay tác động production.

Các phần độc lập không bị chặn phải tiếp tục được triển khai.

Không commit, push, merge hoặc deploy ngoài phạm vi quyền đã được cấp.

Mục tiêu cuối cùng là hoàn thành một phiên bản Operations Manager mới có thể sử dụng thực tế, với Frontend, Backend, Database, Authorization và các luồng nghiệp vụ End-to-End đồng bộ.