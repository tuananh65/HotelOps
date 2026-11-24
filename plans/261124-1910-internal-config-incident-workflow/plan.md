# Cấu hình yêu cầu, ca và hoàn thành vệ sinh có sự cố

- [x] Đọc source, UX và xác định contract hiện hữu; đối chiếu tiến trình local.
- [x] Checkbox vai trò gửi và nơi nhận, kiểm tra API theo cấu hình.
- [x] Bộ chọn giờ 24 giờ thống nhất; kiểm thử trùng ca và cạnh tranh.
- [x] Báo cáo chọn loại trước; OM cấu hình mức ảnh hưởng.
- [x] Quay lại công việc sau báo cáo; phân loại kiểm tra phòng gặp sự cố; giám sát xác nhận và chuyển xử lý.
- [x] Build, test, kiểm chứng API và UI; cập nhật local và báo cáo.

Đã chốt: người gửi chọn một nơi nhận; thiếu đồ chuyển kho, hỏng đồ chuyển kỹ thuật. Không sub-agent, không commit/push. Production API 9944:5769 và FE 7680:5080; Develop API 9220:5774 và FE 48136:5174 đang dùng. API kiểm thử 5775 đã dừng.

Bằng chứng: `plans/reports/implementation-261124-1915-internal-config-incident-workflow.md` và `artifacts/incident-configuration/`. Luồng thực tế TEST: work 764, sự cố 765/766, refill 763; khắc phục cả hai và kiểm tra cuối đưa phòng về READY.
