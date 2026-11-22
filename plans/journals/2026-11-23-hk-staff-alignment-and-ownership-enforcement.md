---
title: HK Staff alignment and ownership enforcement
date: 2026-11-23
summary: "Dashboard cá nhân, Cleaning/Rework và kiểm tra ownership trên fixture độc lập"
---

# HK Staff alignment and ownership enforcement

Đã triển khai HK Staff Project 2 bằng component Work/Rooms/Documents/Workday hiện có, giữ control 31px, navbar và bộ lọc đóng mặc định. Source audit xác nhận role 4 có 13 grants; chỉ đọc primary. Bổ sung dashboard cá nhân từ actor/assignment/lịch thật; danh sách cá nhân giữ lịch sử hoàn thành; tóm tắt inspection chỉ đọc; form tiến độ/checklist/note/minh chứng và liên kết thông báo.

Rà soát phát hiện IR_PROCESS cho phép hủy yêu cầu không được giao và MR_CREATE cho phép sửa yêu cầu của người khác. Đã thêm guard/capability và kiểm thử cả trường hợp bị từ chối. Minh chứng dùng uploader hiện có, kiểm tra assignment và cho người tạo incident gửi minh chứng bằng INCIDENT_CREATE.

Build đầu bị khóa DLL vì API nằm ngoài sandbox; dừng đúng PID rồi build lại sạch. Fixture ban đầu phát hiện thứ tự kiểm tra quyền/trạng thái và dùng UNIT thay UOM; đã sửa theo schema, không nới quyền hoặc trigger. UI fixtures được cập nhật cho endpoint personal/title mới. Một lượt HMR làm mất execution context khi sửa mã trong lúc test; chạy lại trên source ổn định đã đạt. CLI journal ban đầu dùng ngày UTC và stdin PowerShell mất dấu; đã lưu lại UTF-8 theo ngày Việt Nam bằng công cụ sửa file, không sửa mã skill.

Kiểm chứng: API 26 nhóm trên HotelOpsCompactApiTest60d1620fa10b; .NET 28 passed/2 skipped; HK Staff UI 27 views, DH UI 21, OM UI 24, 5 suite UX chung; FE/BE build passed, C# không cảnh báo. Báo cáo nhiệm vụ ghi kết quả hồi quy API mở rộng riêng. Review inline theo yêu cầu single-agent. API local PID 36268:5768, worker tắt; FE 6424/9324:5186. Không commit/push/deploy hay sửa dữ liệu nghiệp vụ primary.

> Historical work record — not durable authority. Prefer docs/specs/ADRs for current decisions. AgentWiki publish skipped.

Hồi quy API mở rộng cuối: 128 Passed, 0 Failed trên HotelOpsCompactApiTestaae805b66be6, sau khi sửa fixture từ expected 409 sang 403 cho handler chưa được giao và seed PRIORITY chỉ trong fixture thay vì API danh mục cố định. Build frontend cuối exit 0; HK UI cuối xác nhận tầng trong drawer và upload/download minh chứng. Hook cấm đọc output build được tôn trọng; dùng log build để xác nhận. Không còn kiểm tra đang chạy.
