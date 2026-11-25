# Review cấp vật tư theo công việc

Đã thêm `scripts/verification/task-supply-contract.mjs`: contract cho adapter gọi API/service thật trên clone có tiền tố `HotelOpsCompactWorkdayTestTaskSupplies`. Kiểm tra start không cấp/không thông báo thiếu đầu ca; assign không cấp; accept theo capacity × perGuest; retry không trừ lần hai; tiêu hao thực bổ sung; shortage rollback toàn bộ. Đây là contract, chưa có adapter thực thi và chưa phải bằng chứng integration PASS.

## Phát hiện cần xử lý

1. `StaffSupplyService.Reusable.cs` trong `AllocateReusableForTask` đang cộng ca khác bằng trạng thái `PAUSED`, nhưng `WorkRules` chuyển CLEANING pause sang `ON_HOLD`. Bỏ sót công việc đang tạm dừng sẽ dùng cùng bộ khăn giữ cho hai công việc, cấp thiếu. Cần dùng ON_HOLD và kiểm thử accepted/paused concurrent rooms.
2. `scripts/verification/p0-supply-contract.mjs` và `p0-runtime-supply-scenarios.mjs` vẫn khẳng định cấp 10 lúc start và thiếu stock thì assign thất bại. Không chạy chúng như acceptance cho policy mới; cần thay adapter/expectation trước khi báo PASS.
3. `Refill` vẫn chứa nhánh legacy SHORT → cấp reusable INITIAL khi đủ InitialTarget. Phải chốt xử lý allocation cũ sao cho không âm thầm cấp full bộ đầu ca qua refill sau cutover.

## SQL review

`p0-supplies.sql` FK `request_work_id` trỏ `work_items` và CHECK ADDITIONAL chỉ yêu cầu không null; trigger provenance kiểm tra branch/allocation/workday WORKING, không khóa kind INTERNAL_REQUEST. Do đó ADDITIONAL liên kết CLEANING không cần schema mới theo source SQL hiện có. Unique `(allocation_id,warehouse_id,request_work_id)` cho ADDITIONAL ngăn cấp hai lần cùng job/kho; cần test retry/reassignment với unique này. Chưa đối chiếu definition trên DB thực tế trong module này.

Không sửa service, không tạo clone, không ghi database chính thức. Build/test C# và API/database integration chưa chạy bởi module này. Node syntax check được chạy riêng, không thay cho integration.
