# Hướng dẫn sử dụng HotelOps — 6 workflow chính

Cập nhật: 18/11/2026. Áp dụng giao diện Cacao hiện tại của nhánh `feature/front-end-new-cacao`.

Bộ dữ liệu `W6_20261118` đã được chuẩn bị trên HotelOpsDB sau backup và nâng cấp schema. Xem [ID phòng/công việc/ca, kết quả kiểm chứng và các bước còn bị chặn](6-workflows-test-data.md). Dữ liệu đầu vào không đồng nghĩa đã nghiệm thu đủ 6 workflow.

Tài liệu này hướng dẫn 6 workflow đã thống nhất trong cuộc trao đổi, không đồng nhất với danh sách BF-01 đến BF-06 trong bản SRS cũ. Các đường dẫn và nhãn thao tác được đối chiếu mã nguồn; chưa chạy nghiệm thu toàn bộ 6 workflow với database vừa nhập. Có màn hình không đồng nghĩa database đã đáp ứng mọi điều kiện để hoàn tất quy trình.

## Bắt đầu

1. Mở [HotelOps](https://localhost:7443/login), đăng nhập bằng tài khoản test phù hợp.
2. Nếu dùng mật khẩu tạm, đổi mật khẩu khi được yêu cầu rồi mới thực hiện nghiệp vụ. Không chép mật khẩu vào tài liệu test hoặc ảnh chụp.
3. Vào Home, dùng danh mục chức năng hoặc thanh điều hướng hai tầng. Home không còn biểu đồ; nút cố định bên phải chỉ ẩn/hiện khu vực cá nhân.
4. Chọn đúng chi nhánh. Nhóm tài khoản bên dưới là các tài khoản đã chọn để test; quyền thực tế vẫn do máy chủ xác định theo role, chi nhánh, người được giao và trạng thái công việc.
5. Nếu test nhiều vai trò, đăng xuất trước khi đổi tài khoản hoặc dùng các profile trình duyệt riêng. Hai tab trong cùng profile dùng chung phiên, không phải hai người dùng độc lập.

| Người tham gia | Tài khoản test | Công việc thường dùng để kiểm tra |
|---|---|---|
| Lễ tân | `le.truong` | Tra cứu phòng và phối hợp vận hành theo quyền được cấp |
| Quản lý vận hành | `hung.nguyen` | Theo dõi vận hành; thử các thao tác quản lý được hệ thống cho phép |
| Giám sát buồng phòng | `mai.tran` | Tạo/phân công vệ sinh, kiểm tra phòng theo quyền |
| Nhân viên buồng phòng | `lan.dang` | Thực hiện công việc vệ sinh hoặc làm lại được giao |
| Trưởng bảo trì | `minh.le` | Tiếp nhận, phân công, kiểm tra kết quả bảo trì theo quyền |
| Nhân viên bảo trì | `thinh.ngo` | Thực hiện công việc bảo trì được giao |
| Quản lý bộ phận | `ha.pham` | Yêu cầu vật tư, kế hoạch ca hoặc phê duyệt nếu được cấp |
| Thủ kho | `tung.vo` | Kiểm tra tồn và cấp phát vật tư theo quyền |

Tên vai trò không bảo đảm mọi nút đều được cấp. Không dùng `sysadmin` thay thế toàn bộ vai trò nghiệp vụ. Role `STAFF` trong database đang bị vô hiệu hóa. Không tự gán thêm quyền để vượt một bước bị từ chối.

### Chuẩn bị dữ liệu test

- Dùng một phòng, một công việc và một yêu cầu vật tư có thể nhận diện được; ghi lại ID và chi nhánh sau mỗi lần tạo.
- Không lấy số phòng làm `Room ID`, không lấy tên đăng nhập làm `User ID`.
- Tra cứu chi nhánh tại `/hotel`, phòng tại `/room-directory`, tài khoản tại `/users` nếu được cấp quyền.
- Trường ghi rõ UTC/ISO 8601 cần thời gian UTC. Ví dụ 14:00 giờ Việt Nam ngày 20/11/2026 là `2026-11-20T07:00:00Z`. Đây chỉ là ví dụ định dạng, không phải hạn mặc định.
- Chỉ chọn nhân viên, loại vệ sinh, checklist, vật tư và đơn vị được máy chủ trả về. Không nhập ID đoán mò.

## 1. Lễ tân theo dõi trạng thái phòng và chuyển việc

**Mục tiêu:** biết phòng đang ở tình trạng nào, vì sao chưa sẵn sàng và chuyển đúng công việc tiếp theo.

**Người thực hiện:** `le.truong`; phối hợp `mai.tran`, `minh.le` hoặc `hung.nguyen` khi quyền yêu cầu.

**Màn hình:** `/room-directory`, `/rooms/status`, `/rooms/turnover`.

**Giới hạn quan trọng:** chưa xác nhận có màn check-in/check-out hoặc nút đổi trạng thái phòng trực tiếp dành cho Reception. Không dùng màn kích hoạt/ngừng kích hoạt phòng tại `/structure/rooms/:id/lifecycle` để thay cho khách nhận/trả phòng. Quyền Reception và các chuyển trạng thái liên quan vẫn có phần chưa chốt trong yêu cầu.

| Bước | Thao tác | Kết quả cần kiểm tra |
|---|---|---|
| 1 | Mở `/room-directory`. Nhập `Branch ID`, hoặc chọn `All branches` nếu cần phạm vi máy chủ cho phép. Giữ `Action = Browse`, `Page = 1`; thêm từ khóa, tầng, khu vực hoặc loại phòng nếu cần. Bấm `Load from server`. | Có danh sách, thông báo không có phòng phù hợp hoặc lỗi cụ thể; không coi màn chưa tải là danh sách rỗng. |
| 2 | Mở chi tiết hoặc lịch sử từ dòng phòng. Ghi lại ID phòng và chi nhánh. | Đúng phòng cần xử lý, không nhầm phòng cùng số ở chi nhánh khác. |
| 3 | Mở `/rooms/status`, chọn phạm vi và bấm `Lọc phòng`; mở chi tiết phòng. | Xem trạng thái đã ghi nhận, trạng thái sẵn sàng hiệu lực, chu kỳ vệ sinh và các block. |
| 4 | Trong `Hành động tiếp theo`, dùng `Tạo công việc vệ sinh`, phân công kiểm tra hoặc `Report an incident for this room` khi được phép. | Chuyển đến đúng workflow với ngữ cảnh phòng. Nếu nút bị khóa, đọc lý do hoặc nhờ người có quyền xử lý. |
| 5 | Sau khi công việc liên quan hoàn tất, bấm `Refresh room status`, đối chiếu lịch sử tại `/rooms/turnover` khi được cấp. | Trạng thái mới do máy chủ trả về; không tự suy ra phòng Ready từ việc bấm hoàn tất vệ sinh. |

**Đạt khi:** tìm đúng phòng, giải thích được yếu tố đang chặn phòng và mở được bước xử lý phù hợp. **Chưa đánh dấu đạt:** thao tác check-in/check-out chưa có bằng chứng giao diện thực hiện.

## 2. Tạo, phân công và thực hiện dọn phòng

**Luồng:** giám sát tạo công việc → phân công → nhân viên thực hiện → hoàn tất → chuyển kiểm tra.

**Người thực hiện:** `mai.tran` → `lan.dang`.

**Màn hình:** `/cleaning/tasks`, `/cleaning/tasks/new`, `/cleaning/assignment`, chi tiết `/cleaning/tasks/:id`.

**Điều kiện:** phòng thuộc phạm vi được phép, cấu hình vệ sinh có dữ liệu, công việc đủ điều kiện phân công; nhân viên đủ điều kiện và có workday đang làm việc khi thao tác yêu cầu. Nếu báo thiếu workday, thực hiện workflow 6 trước.

1. **Giám sát tạo việc:** từ chi tiết phòng chọn `Tạo công việc vệ sinh`, hoặc vào màn tạo công việc. Nhập/chọn chi nhánh và phòng, bấm `Tải cấu hình phòng`.
2. Kiểm tra có công việc hiện hữu hay không. Nếu đã có, mở công việc đó; không tạo trùng để bỏ qua trạng thái đang chờ.
3. Chọn `Loại vệ sinh`, `Ưu tiên`, nhập `Hạn UTC (ISO 8601)` và `Hướng dẫn`. Bấm `Tạo công việc` hoặc `Save and Assign`.
4. **Phân công:** tại `/cleaning/assignment`, tải phương án cho đúng chi nhánh/công việc. Chọn `Nhân viên (bản nháp)`, hạn UTC và lý do. Bấm `Kiểm tra bản nháp`; khi đủ điều kiện, bấm `Lưu phân công`. Kiểm tra kết quả lưu, không xem bản nháp là phân công thành công.
5. **Nhân viên nhận việc:** đăng nhập `lan.dang`, tìm đúng công việc được giao và mở liên kết thực hiện. Trang execution có dạng `/cleaning/tasks/{taskId}?branchId={branchId}&execution=1`; ưu tiên mở từ danh sách để giữ đúng ngữ cảnh.
6. Bắt đầu công việc bằng thao tác được trang cung cấp; thực hiện các hạng mục và lưu tiến độ/bằng chứng theo yêu cầu.
7. Nếu gặp trở ngại, dùng `Báo cáo trở ngại`. Nếu tạo sự cố hoặc yêu cầu vật tư từ luồng này, hoàn tất lưu/liên kết nguồn rồi quay lại công việc. Không đánh dấu trở ngại đã hết trước khi nguồn xử lý được xác nhận.
8. Khi có thể làm tiếp, kiểm tra blocker, ghi xác nhận nếu được yêu cầu và dùng thao tác tiếp tục. Trường hợp kết thúc ca có đường `Resume after end of shift` khi phù hợp.
9. Hoàn thành các mục bắt buộc, bấm `Hoàn tất dọn phòng` và xác nhận theo màn hình. Ghi lại kết quả, mở lại công việc và chuyển cho người kiểm tra.

**Đạt khi:** nhân viên được giao và tiến độ được lưu, không còn điều kiện bắt buộc chưa xử lý, kết quả hoàn tất được máy chủ xác nhận.

**Lưu ý:** công việc chuyển sang chờ kiểm tra không có nghĩa phòng đã Ready. Theo làm rõ UC034/GBR-044, phòng chưa từng Passed inspection vẫn có thể có effective readiness Pending Cleaning sau lần Complete đầu tiên. Block hoặc trạng thái inactive còn có thể được ưu tiên hơn.

## 3. Kiểm tra phòng, yêu cầu làm lại và kiểm tra lại

**Luồng đạt:** phân công kiểm tra → thực hiện checklist → lưu kết quả → kết luận đạt → đọc lại trạng thái phòng.

**Luồng không đạt:** ghi lỗi → kết luận không đạt/yêu cầu làm lại → phân công làm lại → nhân viên nộp kết quả → kiểm tra lại.

**Người thực hiện:** giám sát/người kiểm tra được giao, thử với `mai.tran` khi đủ quyền; người làm lại là nhân viên được phân công, chẳng hạn `lan.dang`.

**Màn hình:** `/inspections/assignment`, `/inspections/cleaning-tasks/:taskId/assign`, `/inspections/:branch/:id/execution`, `/inspection-history`.

1. Sau workflow 2, mở phòng và chọn phân công kiểm tra; hoặc vào `/inspections/assignment` với chi nhánh/phòng phù hợp.
2. Mở công việc vệ sinh chờ kiểm tra, chọn người kiểm tra và các dữ liệu mà màn phân công yêu cầu. Lưu và kiểm tra người được giao. Không dùng ID công việc vệ sinh thay ID inspection.
3. Người kiểm tra mở liên kết execution của inspection được giao, bấm `Start Inspection` khi thao tác khả dụng.
4. Với từng tiêu chí trong `Assigned checklist criteria`, ghi Pass/Fail, ghi chú và bằng chứng khi yêu cầu; bấm `Save results`. Không chỉ chọn trên màn hình rồi chuyển trang.
5. Nếu có tiêu chí không đạt, ghi `Defects`, gắn đúng tiêu chí, nhập mô tả và các trường bắt buộc; bấm `Record defect` hoặc lưu sửa lỗi.
6. Bấm `Finish → PENDING_CONCLUSION` để đưa kết quả đã lưu sang bước kết luận khi đủ điều kiện. Nếu đạt: dùng `Approve inspection`. Nếu không đạt: ghi lý do từ chối (ít nhất 10 ký tự), bảo đảm đã ghi lỗi tương ứng, xác nhận yêu cầu làm lại và dùng `Reject inspection`. Kiểm tra kết quả được lưu.
7. Khi cần làm lại, dùng phần `Request cleaning rework`; kiểm tra phạm vi lỗi, người nhận và dữ liệu bắt buộc. Mở đường phân công làm lại từ hồ sơ: `/inspections/reworks/:id/assign`.
8. Nhân viên thực hiện công việc được giao tại `/cleaning/reworks/:id` hoặc liên kết hiện có từ hàng đợi `/rework`; ghi kết quả và dùng `Submit rework` khi đủ điều kiện. Không coi nộp làm lại là kiểm tra đạt.
9. Người có quyền phân công kiểm tra lại mở hồ sơ gốc và liên kết `/reinspections/:branch/:id/original`, sau đó thực hiện qua `/reinspections/:branch/:id/execution`. Đối chiếu các lỗi của lần trước.
10. Mở `/inspection-history` và tải lại `/rooms/status` để kiểm tra kết luận mới và các blocker còn tồn tại.

**Đạt khi:** có kết quả checklist đã lưu, kết luận inspection và lịch sử đối chiếu được. Với nhánh không đạt, phải có chuỗi lỗi → làm lại → kiểm tra lại; không chỉ đổi nhãn trạng thái.

**Nếu bị chặn:** kiểm tra đúng người được giao, checklist/tiêu chí bắt buộc, bằng chứng và quyền đọc. Không tạo một inspection khác để né công việc đang mở.

## 4. Báo sự cố và xử lý bảo trì

**Luồng:** người báo → tiếp nhận/phân loại → phân công kỹ thuật → thực hiện → nộp kết quả → xác minh/đóng.

**Người thực hiện:** người được quyền báo; `minh.le` để tiếp nhận/phân công/xác minh khi được phép; `thinh.ngo` thực hiện bảo trì được giao.

**Màn hình:** `/operations/incidents`, `/operations/incidents/:id`, `/operations/maintenance`, `/operations/maintenance/:id`.

1. Từ chi tiết phòng dùng `Report an incident for this room`, hoặc mở danh sách sự cố và chọn `Create Incident` trong danh sách thao tác.
2. Kiểm tra vị trí/chi nhánh, tiêu đề, mô tả, phân loại và các trường bắt buộc. Đọc các sự cố tương tự trước khi tạo để tránh báo trùng. Lưu rồi ghi lại Incident ID.
3. Người tiếp nhận mở đúng sự cố, dùng thao tác triage/phân loại phù hợp; xác định mức độ và ảnh hưởng. Không tự bỏ chặn phòng nếu còn điều kiện khác.
4. Mở công việc bảo trì liên quan bằng `Browse related maintenance works` hoặc màn `/operations/maintenance`. Tạo/phân công theo các thao tác máy chủ cho phép; chọn kỹ thuật viên từ danh sách hợp lệ.
5. Kỹ thuật viên mở công việc được giao, kiểm tra workday và dùng thao tác bắt đầu/tiếp tục được cung cấp. Nếu chưa được giao hoặc bị từ chối, quay lại trưởng bảo trì để kiểm tra phân công.
6. Trong `Record progress and materials`, ghi tiến độ, ghi chú và vật tư thực dùng. Với vật tư xuất kho, chọn đúng dòng cấp phát và đơn vị, không chỉ nhập con số tùy ý. Dùng `Review progress` rồi `Confirm progress` khi hiển thị.
7. Khi xử lý xong, điền `Diagnosis / root cause`, `Action taken`, ghi chú và bằng chứng tại `Submit result for verification`. Bấm `Review submission` → `Confirm submission`.
8. Người có quyền xác minh kiểm tra kết quả và vật tư. Nếu đạt, tại `Accept and close` nhập `Verification note`, bấm `Review closure` → `Confirm closure`. Nếu không đạt, dùng thao tác trả lại kèm lý do và để kỹ thuật viên sửa/nộp lại.
9. Tải lại hồ sơ sự cố, công việc bảo trì và trạng thái phòng. Kiểm tra lịch sử và những block còn hiệu lực.

**Đạt khi:** có kết quả kỹ thuật được xác minh và các trạng thái cuối được tải lại từ máy chủ. Nộp kết quả không đồng nghĩa đã đóng; đóng bảo trì không bảo đảm mọi blocker của phòng đều hết.

## 5. Yêu cầu, duyệt và cấp phát vật tư

**Luồng:** người yêu cầu → người duyệt được chỉ định → thủ kho → người nhận; nếu thiếu thì theo dõi phần còn lại.

**Người thực hiện:** thử người yêu cầu `ha.pham` nếu được quyền; người duyệt phải là người được chỉ định cho chính phiếu đó; thủ kho `tung.vo`. Không mặc định người quản lý bất kỳ đều được duyệt.

**Màn hình:** `/operations/material-requests`, `/operations/material-requests/:id`, `/inventory/stock`. Phần bổ sung/hoàn trả chỉ thực hiện nếu nghiệp vụ phát sinh và có quyền.

1. Mở danh sách yêu cầu vật tư, chọn thao tác tạo yêu cầu. Chọn chi nhánh, phòng ban, người duyệt, mục đích/hạn cần và danh sách vật tư theo các trường hiện có.
2. Với từng dòng, chọn vật tư, đơn vị và số lượng; kiểm tra lý do cần dùng. Nếu xuất phát từ việc dọn phòng/bảo trì/sự cố, dùng liên kết từ công việc để giữ ngữ cảnh, rồi kiểm tra liên kết đã được lưu.
3. Lưu yêu cầu bằng thao tác `Create` và ghi Request ID. Mở lại chi tiết để kiểm tra người yêu cầu, người duyệt và từng dòng vật tư. Dùng `Submit` để gửi duyệt khi màn hình cho phép; tạo phiếu không đồng nghĩa đã gửi duyệt. Khi nhóm thao tác yêu cầu xem xét và xác nhận, hoàn tất cả hai bước.
4. Đăng nhập người duyệt được chỉ định, mở đúng Request ID, xem mục đích và số lượng. Chọn thao tác review/approve/reject được cung cấp; ghi số lượng phê duyệt hoặc lý do từ chối theo yêu cầu. Đọc kết quả lưu và lịch sử.
5. Đăng nhập `tung.vo`, mở yêu cầu đã được duyệt. Dùng `View available stock`, chọn `Issuing warehouse`, bấm `Refresh issuing warehouse availability` nếu cần.
6. Chọn cấp toàn bộ phần còn lại hoặc `Enter partial quantities` khi không đủ. Nhập số lượng từng dòng ở chế độ cấp một phần; số 0 bỏ qua dòng đó. Nhập `Receiver name` và xác nhận cấp phát theo màn hình.
7. Mở lại phiếu: đối chiếu số lượng yêu cầu, đã duyệt, đã cấp và còn lại. Kiểm tra chứng từ cấp phát/tồn kho nếu có quyền. Không cấp lại chỉ vì chưa thấy thông báo ngay lập tức.
8. Nếu còn thiếu, dùng liên kết `Check shortage and create replenishment for the remainder` khi xuất hiện; ghi lại yêu cầu bổ sung và tiếp tục theo dõi phần chưa cấp. Không coi cấp một phần là cấp đủ.
9. Nếu trả vật tư chưa dùng, mở luồng trả từ chứng từ xuất (`/inventory/issues/:issueId/return`), thực hiện yêu cầu/duyệt/nhập trả theo quyền. Kiểm tra tồn kho sau khi nghiệp vụ trả được ghi nhận.

**Đạt khi:** theo dõi được phiếu từ yêu cầu đến quyết định duyệt và cấp phát, các số lượng đối chiếu được, phần thiếu hoặc hoàn trả không bị bỏ qua.

**Nếu bị chặn:** kiểm tra người duyệt hiện tại, trạng thái phiếu, kho trong chi nhánh, tồn khả dụng, số lượng còn được cấp và liên kết công việc. Không sửa trực tiếp số tồn trong database để tiếp tục test.

## 6. Lập ca và ghi nhận thời gian làm việc

**Luồng:** người có quyền lập kế hoạch → nhân viên bắt đầu workday → thực hiện công việc → kết thúc workday → xem thời gian.

**Người thực hiện:** người có quyền quản lý ca, thử `ha.pham`/`hung.nguyen` theo quyền thực tế; nhân viên `lan.dang` hoặc `thinh.ngo` thực hiện workday của chính mình.

**Màn hình:** `/workday/shift-plans`, `/workday/scheduled`, `/workday`, `/my-time-log`; `/time-corrections` khi được phép xử lý điều chỉnh.

1. Người quản lý mở `/workday/shift-plans`, tải định nghĩa ca và danh sách phân ca hiện có. Chọn đúng chi nhánh/phòng ban, nhân viên, ngày và ca từ dữ liệu được phép.
2. Thêm phương án bằng `Add draft`, bấm `Preview assignments`. Đọc các lỗi hoặc xung đột trước khi lưu.
3. Nếu hợp lệ, bấm `Review save` → `Confirm save plan`. Tải lại roster để kiểm tra phân công đã có; preview chưa phải lưu.
4. Nhân viên đăng nhập, mở `/workday/scheduled`, nhập `Branch ID` và bấm `View workday`. Nếu đang ở `/workday`, dùng liên kết `Start or end scheduled workday` để chuyển sang màn thao tác. Chọn ca được phân và đọc thời gian, chi nhánh, phòng ban.
5. Bấm `Start scheduled workday`, kiểm tra phần Review rồi `Confirm scheduled start`. Xác nhận workday đã bắt đầu; giờ thực tế do máy chủ ghi.
6. Nếu phải bắt đầu ngoài kế hoạch và màn hình cho phép, dùng `Start without a shift`, chọn Department, ghi Reason → `Review unscheduled start` → `Confirm unscheduled start`. Đây không phải cách bỏ qua lỗi của phân ca hiện tại.
7. Quay lại công việc được giao, bắt đầu/tạm dừng/tiếp tục ở chính màn công việc. Việc bắt đầu workday không tự hoàn thành hay tự phân công task.
8. Cuối ca, bấm `End workday`. Nếu có khoảng thời gian công việc đang mở, màn hình có thể hiển thị `Pause All and end workday`. Đọc tác động rồi bấm `Confirm workday end`.
9. Tải lại workday, kiểm tra trạng thái kết thúc và các khoảng đang chạy. Nếu thao tác tạm dừng thất bại, không mặc định workday đã kết thúc.
10. Mở `/my-time-log`, đối chiếu công việc và thời gian thực tế. Nếu có sai lệch, dùng luồng điều chỉnh được cấp tại `/time-corrections` hoặc báo người quản lý; không sửa dữ liệu gốc trực tiếp.

**Đạt khi:** phân ca lưu được, nhân viên bắt đầu/kết thúc đúng workday, các khoảng thời gian gắn đúng công việc và không còn khoảng chạy ngoài dự kiến. Dữ liệu này phục vụ vận hành, không được diễn giải thành bảng lương/chấm công sinh trắc học.

## Xử lý tình huống chung

| Hiện tượng | Cách xử lý |
|---|---|
| Không có menu hoặc nút bị khóa | Kiểm tra tài khoản, chi nhánh, phân công và lý do tại màn hình. Có thể chưa được cấp quyền hoặc chưa đúng trạng thái; không mặc định là lỗi CSS. |
| Chưa đăng nhập/phiên hết hạn | Đăng nhập hoặc xác minh phiên, tải lại dữ liệu. Không gửi lại thao tác ghi cũ một cách tự động. |
| 403 / Access denied | Ghi lại tài khoản, màn hình, ID đối tượng và chi nhánh để kiểm tra quyền; không thử ID khác nhằm vượt phạm vi. |
| 409 / 412 / stale | Dữ liệu có thể đã đổi. Tải lại, kiểm tra trạng thái/phiên bản mới rồi lập lại thao tác nếu vẫn phù hợp. |
| Outcome unknown / mất mạng sau khi bấm lưu | Kiểm tra chi tiết và lịch sử trước khi bấm lại, tránh tạo/cấp phát/trả kho trùng. |
| 503 / schema prerequisite / dữ liệu chưa khả dụng | Ghi lại thông báo và bước đang làm. Database nhập có thể chưa chứa điều kiện cho module mới; đánh dấu Blocked, không tự thêm bảng/quyền/trạng thái. |
| Không có dữ liệu | Kiểm tra bộ lọc, trang, chi nhánh và đã bấm tải hay chưa; phân biệt với lỗi tải dữ liệu. |
| Không bắt đầu được task | Kiểm tra đúng người được giao, workday đang làm việc, trạng thái hiện tại và blocker còn hiệu lực. |

## Kịch bản chạy thử đề xuất

1. Đổi mật khẩu tạm cho các tài khoản sẽ dùng và xác nhận menu đúng từng người.
2. Thực hiện workflow 6 để chuẩn bị ca/workday cho nhân viên.
3. Thực hiện workflow 1 để chọn phòng, ghi lại Room ID và chi nhánh.
4. Chạy workflow 2 → 3, thử một lượt đạt và một lượt không đạt cần làm lại trên các công việc phù hợp.
5. Chạy workflow 4 với một sự cố test riêng; nếu cần vật tư, nối sang workflow 5.
6. Kết thúc workday, đối chiếu lịch sử phòng, kiểm tra, sự cố, vật tư và time log.

| Workflow | Tài khoản | Chi nhánh / ID đối tượng | Bước cuối đã chạy | Kết quả thực tế | Đánh giá |
|---|---|---|---|---|---|
| 1. Phòng | | | | | Chưa chạy / Đạt / Lỗi / Blocked |
| 2. Vệ sinh | | | | | Chưa chạy / Đạt / Lỗi / Blocked |
| 3. Kiểm tra/làm lại | | | | | Chưa chạy / Đạt / Lỗi / Blocked |
| 4. Sự cố/bảo trì | | | | | Chưa chạy / Đạt / Lỗi / Blocked |
| 5. Vật tư | | | | | Chưa chạy / Đạt / Lỗi / Blocked |
| 6. Ca/thời gian | | | | | Chưa chạy / Đạt / Lỗi / Blocked |

Khi báo lỗi, gửi: workflow và bước, username (không kèm mật khẩu), đường dẫn màn hình, chi nhánh/ID đối tượng, thao tác, thông báo lỗi và kết quả mong đợi. Nếu chụp màn hình, tránh lộ mật khẩu/token.

## Nguồn đối chiếu và phạm vi kiểm chứng

- [SRS gốc](../requirements/HotelOps_SRS_DRAFT.md): tham khảo nghiệp vụ, không coi toàn bộ ma trận quyền là đã duyệt.
- [Các thay đổi và làm rõ yêu cầu](../requirements/SRS_CHANGE_NOTES.md): Reception, Department Head và UC034/GBR-044.
- [Route chính](../../frontend/src/shared-host/SharedAuthHost.tsx), [route vận hành](../../frontend/src/shared-host/OperationsRoutes.tsx).
- [Trạng thái phòng](../../frontend/src/screens/rooms/RoomStatusBoardScreen.tsx), [hành động tiếp theo](../../frontend/src/screens/rooms/RoomWorkflowActions.tsx).
- [Tạo vệ sinh](../../frontend/src/screens/housekeeping/CreateCleaningTaskScreen.tsx), [phân công](../../frontend/src/screens/housekeeping/CleaningAssignmentScreen.tsx), [thực hiện](../../frontend/src/screens/housekeeping/CleaningExecutionPanel.tsx).
- [Thực hiện kiểm tra](../../frontend/src/features/inspectionExecution/InspectionWorkspace.tsx), [yêu cầu làm lại](../../frontend/src/features/inspectionExecution/RequestReworkForm.tsx).
- [Workspace sự cố/vật tư](../../frontend/src/dev2/Workspace.tsx), [danh mục thao tác](../../frontend/src/dev2/catalog.generated.ts), [cấp vật tư](../../frontend/src/dev2/MaterialIssueFields.tsx).
- [Kế hoạch ca](../../frontend/src/workday/ShiftPlanPreviewPanel.tsx), [workday có ca](../../frontend/src/workday/ScheduledWorkdayPanel.tsx).

Kiểm chứng của lượt viết tài liệu: đối chiếu route, tên nút và logic giao diện với mã nguồn; kiểm tra liên kết tệp nội bộ. Không sửa ứng dụng, không đổi database/quyền, không chạy build/test ứng dụng cho thay đổi tài liệu. Chưa xác nhận các workflow chạy trọn vẹn trên database local; các lỗi schema/tác vụ nền đã ghi nhận trước đó vẫn cần kiểm tra trong lần test thực tế.
