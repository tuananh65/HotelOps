const lines = `Accept|Tiếp nhận
Accept and start|Tiếp nhận và bắt đầu
Acknowledge|Xác nhận
Acknowledge handover|Xác nhận bàn giao
Activate|Kích hoạt
Add conversion|Thêm quy đổi
Add evidence|Thêm minh chứng
Approve adjustment|Duyệt điều chỉnh
Assign handler|Phân công người xử lý
Assign location|Gán vị trí
Assign roles|Gán vai trò
Assign scheduled work|Phân công bảo trì định kỳ
Assign scope|Gán chi nhánh / bộ phận
Block room|Tạm khóa phòng
Classify|Phân loại
Close remainder|Đóng phần còn lại
Close unissued remainder|Đóng phần chưa xuất
Complete scheduled maintenance|Hoàn thành bảo trì định kỳ
Configure permissions|Cấu hình quyền minh họa
Deactivate|Ngừng hoạt động
Decline|Từ chối
Dispatch transfer|Xuất chuyển kho
Escalate|Báo cáo lên cấp trên
Hand over|Bàn giao
Issue remaining|Xuất phần còn lại
Link existing work|Liên kết công việc có sẵn
Link to work|Liên kết công việc
Mark as read|Đánh dấu đã đọc
Partial issue|Xuất một phần
Pause|Tạm dừng
Publish|Xuất bản
Put on hold|Tạm hoãn
Reactivate|Kích hoạt lại
Reconcile|Đối soát
Record counts|Ghi nhận kiểm kê
Remove block|Gỡ tạm khóa
Reopen|Mở lại
Report blocker|Báo cáo trở ngại
Report discovered problem|Báo cáo vấn đề phát hiện
Request information|Yêu cầu thông tin
Reset password|Đặt lại mật khẩu mô phỏng
Resume|Tiếp tục
Retire|Ngừng sử dụng
Return result|Trả kết quả
Return unused materials|Hoàn trả vật tư chưa dùng
Reverse|Đảo giao dịch
Route|Chuyển bộ phận
Start / resume|Bắt đầu / tiếp tục
Start scheduled maintenance|Bắt đầu bảo trì định kỳ
Stock minimum|Định mức tồn tối thiểu
Stop future repetitions|Dừng các lần lặp tiếp theo
Submit counts|Gửi số liệu kiểm kê
Submit for inspection|Gửi để kiểm tra
Submit resolution|Gửi kết quả xử lý
Submit result|Gửi kết quả
Transfer location|Chuyển vị trí
Triage|Đánh giá ban đầu
Update condition|Cập nhật tình trạng
Update progress|Cập nhật tiến độ
Verify|Xác minh
Verify and close|Xác minh và đóng
Withdraw|Rút yêu cầu
Create record|Tạo bản ghi
Save changes|Lưu thay đổi
Record saved.|Đã lưu bản ghi.
Action completed.|Đã hoàn thành thao tác.
A pending handover prevents retirement.|Vui lòng hoàn tất bàn giao trước khi ngừng sử dụng.
Action is no longer available in the current status.|Thao tác không còn phù hợp với trạng thái hiện tại.
All mandatory criteria must pass.|Tất cả tiêu chí bắt buộc phải đạt.
An open escalation already exists.|Đã có báo cáo lên cấp trên đang mở.
An open inspection already exists.|Đã có kiểm tra đang mở.
An open replenishment already covers this shortage.|Đã có đề nghị bổ sung cho lượng thiếu này.
An open stocktake already exists for this warehouse.|Kho này đã có đợt kiểm kê đang mở.
Another active Maintenance Lead is required to verify.|Cần trưởng nhóm bảo trì khác đang hoạt động để xác minh.
Approve at least one positive quantity; otherwise Reject with reason.|Duyệt ít nhất một số lượng dương, hoặc từ chối kèm lý do.
Approved quantity must be between zero and requested quantity.|Số lượng duyệt phải từ 0 đến số lượng yêu cầu.
At least one positive received quantity is required.|Cần ít nhất một dòng có số lượng thực nhận lớn hơn 0.
Base unit cannot change after a stock movement.|Không thể đổi đơn vị cơ sở sau khi phát sinh giao dịch.
Cannot add items while posting.|Không thể thêm vật tư khi ghi sổ.
Cannot remove last Administrator.|Phải giữ lại một vai trò quản trị viên trong ví dụ mô phỏng.
Check every defect.|Vui lòng kiểm tra từng lỗi.
Choose a different employee.|Vui lòng chọn nhân viên khác.
Choose a monthly day between 1 and 31.|Chọn ngày trong tháng từ 1 đến 31.
Choose at least one weekday for weekly recurrence.|Chọn ít nhất một ngày trong tuần để lặp lại.
Complete every mandatory cleaning item and resolve blockers first.|Hoàn tất các tiêu chí vệ sinh bắt buộc và xử lý trở ngại trước.
Complete mandatory linked work and pending handovers first.|Hoàn tất công việc liên quan bắt buộc và bàn giao còn chờ trước.
Correction already processed.|Yêu cầu điều chỉnh đã được xử lý.
Correction creates overlapping intervals.|Điều chỉnh tạo khoảng thời gian trùng nhau.
Counted quantities must be non-negative.|Số lượng kiểm kê không được âm.
Deadline must not precede planned start.|Hạn hoàn thành không được trước thời điểm bắt đầu dự kiến.
Default assignee must be a maintenance technician.|Người thực hiện mặc định phải là kỹ thuật viên bảo trì.
Dependent return documents prevent reversal.|Cần xử lý phiếu hoàn trả liên quan trước khi đảo giao dịch.
Each item may appear only once in a document.|Mỗi vật tư chỉ được xuất hiện một lần trong chứng từ.
Employee has an overlapping shift assignment.|Nhân viên đã có ca làm việc trùng thời gian.
Employee is not eligible for this assignment.|Nhân viên chưa phù hợp với phân công này.
Enter a positive issue quantity.|Nhập số lượng xuất lớn hơn 0.
Every criterion requires Pass/Fail and every Fail needs a defect description.|Mỗi tiêu chí cần kết quả đạt hoặc không đạt; kết quả không đạt cần mô tả lỗi.
Every defect requires a remediation note.|Mỗi lỗi cần ghi chú khắc phục.
Every mandatory check item requires a result.|Mỗi tiêu chí bắt buộc cần có kết quả.
Every mandatory completion criterion requires a result.|Mỗi tiêu chí hoàn thành bắt buộc cần có kết quả.
Every material line requires an item and positive quantity.|Mỗi dòng cần vật tư và số lượng lớn hơn 0.
Insufficient stock: operation would create a negative balance.|Không đủ tồn kho: thao tác sẽ làm số dư âm.
Issue quantity exceeds approved remainder.|Số lượng xuất vượt phần còn lại đã duyệt.
Minimum stock must be non-negative.|Tồn tối thiểu không được âm.
No active workday.|Chưa có ngày làm việc đang mở.
No eligible Department Head approver.|Chưa có trưởng bộ phận phù hợp để duyệt.
No posted issue to return.|Chưa có phiếu xuất đã ghi sổ để hoàn trả.
Only critical, at-risk or overdue records may be escalated.|Chỉ báo cáo lên cấp trên đối với bản ghi nghiêm trọng, có rủi ro hoặc quá hạn.
Open reinspection already exists.|Đã có kiểm tra lại đang mở.
Parent chain creates a cycle.|Quan hệ vị trí cha tạo vòng lặp.
Pause or complete open work before ending your workday.|Tạm dừng hoặc hoàn thành công việc đang mở trước khi kết thúc ngày làm việc.
Quantity discrepancy requires a 10-character reason.|Chênh lệch số lượng cần lý do ít nhất 10 ký tự.
Reassign open work before moving scope.|Phân công lại công việc đang mở trước khi đổi chi nhánh hoặc bộ phận.
Reassign open work before removing roles.|Phân công lại công việc đang mở trước khi bỏ vai trò.
Receipt/loss quantities exceed dispatched remainder.|Số lượng nhận và thất thoát vượt số lượng chuyển còn lại.
Record changed. Reload before saving.|Bản ghi đã thay đổi. Vui lòng tải lại trước khi lưu.
Record defects before rejecting.|Ghi nhận lỗi trước khi kết luận không đạt.
Replenishment requires a low-stock item or approved material shortage.|Đề nghị bổ sung cần vật tư dưới định mức hoặc yêu cầu đã duyệt còn thiếu.
Requires a pending cleaning cycle and published checklist.|Cần chu kỳ vệ sinh chờ kiểm tra và bộ tiêu chí đã xuất bản.
Requires failed inspection without open rework.|Cần kiểm tra không đạt và chưa có công việc khắc phục đang mở.
Resolution deadline must follow response deadline.|Hạn xử lý phải sau hạn phản hồi.
Return exceeds issued quantity.|Số lượng hoàn trả vượt số lượng đã xuất.
Return exceeds original issue.|Số lượng hoàn trả vượt phiếu xuất gốc.
Return must reference a posted issue.|Phiếu hoàn trả phải tham chiếu phiếu xuất đã ghi sổ.
Rework must be submitted first.|Cần gửi kết quả khắc phục trước.
Rework must be submitted for reinspection.|Cần gửi kết quả khắc phục để kiểm tra lại.
Room has an effective block.|Phòng đang bị tạm khóa.
Select a maintenance technician in the same branch.|Chọn kỹ thuật viên bảo trì cùng chi nhánh.
Select a room, area or asset.|Chọn phòng, khu vực hoặc tài sản.
Select an active inventory item.|Chọn vật tư đang hoạt động.
Select an existing request and work record.|Chọn yêu cầu và công việc đang tồn tại.
Select an open work record in the same branch and your scope.|Chọn công việc đang mở cùng chi nhánh.
Select one asset, room or area for maintenance.|Chọn một tài sản, phòng hoặc khu vực cần bảo trì.
Select your own interval, valid start/end and a 10-character reason.|Chọn khoảng làm việc, thời gian bắt đầu/kết thúc hợp lệ và lý do ít nhất 10 ký tự.
Set category and severity before triage.|Chọn loại và mức độ nghiêm trọng trước khi đánh giá ban đầu.
Shift employees must be active in the selected branch and department.|Nhân viên trong ca phải đang hoạt động tại chi nhánh và bộ phận đã chọn.
Shift end must follow start.|Thời điểm kết thúc ca phải sau thời điểm bắt đầu.
Source and destination warehouses must differ.|Kho nguồn và kho đích phải khác nhau.
Starting without a shift assignment requires a 10-character reason.|Bắt đầu khi chưa được phân ca cần lý do ít nhất 10 ký tự.
System role identity cannot be renamed.|Không thể đổi tên định danh vai trò hệ thống.
System roles cannot be deactivated.|Vai trò hệ thống cần giữ hoạt động trong ví dụ mô phỏng.
The reopen window has expired.|Đã hết thời hạn mở lại.
This incident already blocks the room.|Sự cố này đã tạm khóa phòng.
This room already has an open cleaning task.|Phòng này đã có công việc vệ sinh đang mở.
This task has no active recurring series.|Công việc chưa có chuỗi lặp đang hoạt động.
Transfer losses require a 10-character reason.|Thất thoát khi chuyển kho cần lý do ít nhất 10 ký tự.
Unit has no valid conversion to the item base unit.|Đơn vị chưa có quy đổi hợp lệ về đơn vị cơ sở.
Use a unique additional unit with positive factor.|Chọn đơn vị bổ sung chưa có với hệ số dương.
Warehouse still has stock.|Kho vẫn còn tồn vật tư.
Work is already linked.|Công việc đã được liên kết.
Workday already started.|Ngày làm việc đã bắt đầu.
Wrong document type.|Loại chứng từ chưa phù hợp.`;
export const workflowsVi = Object.fromEntries(
  lines.split("\n").map((line) => line.split("|")),
);
