# DEV1 FS012 — SQL write boundary prototype, frozen Tester handover

Baseline 0ad98b951cf4737842afe3e382e897f7fb4bacc8; branch feature/dev1-room-ops. Authorization của task cho phép tự thực hiện thử nghiệm trên isolated trial; không cần approval từng bước của DEV2/DEV3. Không merge/pull/rebase, không sửa worktree/module của developer khác, không thay shared SQL.

Runtime changes: `Services/Housekeeping/CleaningAssignmentCandidateService.cs` thêm nhánh sau validation để gọi approved SQL module chỉ khi candidate FS012; `Services/Notifications/ProvisionalCleaningNotification.cs` thêm exact FS012 tuple, boundary availability check và parameterized caller-transaction module adapter; `CleaningTaskCreationService.cs` nhận thêm marker FS012-V1. Tests NotificationCandidateTests thêm Production và wrong-database fail-closed cases. Không đổi routes/DTO response hoặc DI/global authentication/mapping/entity được sinh.

SQL mới chỉ tại `scripts/database/release/fs012-candidate/`: copy retargeted additive lifecycle/candidate scripts, thêm `10_SqlWriteBoundary.sql` tạo complete assignment module, deliberately ungranted writer canary, NOLOGIN fs012_app và quyền tường minh. Procedure dùng cùng BatchAsync catalog lock và không sở hữu commit. Boundary chỉ đảm bảo Assignment creation qua module đối với principal có quyền đó; dbo/sysadmin/khác-principal vẫn có thể sửa trực tiếp. UC028/Core UC032 permission compatibility có evidence; prototype không cấp INSERT attachments, không deploy login hay đổi app connection identity.

Shared writer impact: future Reassign/close-assignment paths không thể tiếp tục direct DML nếu chọn principal này; phải review approved mutation path trước rollout. DEV1 WorkTime/deactivation/history/monitor hiện đọc Assignment, không bị sửa. Existing seeded assignments không được backfill. User này không đại diện end-user roles; RBAC/shift policy còn nằm ở service. Không thêm Reception/Department Head permissions.

FS011 ZIP/expanded files/workbooks giữ nguyên; checkpoint4f62d13, ZIP SHA8e661796fe83b7b15c16d84f79547ce72e4cdd6272f7996ce2fc659003141974. Ba runtime files thay đổi phải dùng retest version mới FE04_RETEST_FS012; không coi source FS012 là frozen FS011. Tester DB chỉ đọc schema/seedstatus trong FS012. Chưa có feedback/acceptance.

FS012 installer kiểm18 canonical/module sources bằng LF-normalized hashes để không phụ thuộc autocrlf; trường raw sha256 cũ vẫn dùng làm schema compatibility marker. Plan-only đã PASS trong workspace và fresh Git archive, không tạo database thứ hai. Gói/installer FS011 bất biến, không silently backport thay đổi này vào đợt Tester hiện hành.

A01/A02 vẫn MISSING_EXECUTABLE_SOURCE; mười relevant remote tips không đổi, không lặp discovery toàn lịch sử. Candidate/shared production OFF, UC029 PROVISIONAL_NOT_ACCEPTED; không có accepted BE2 Notification integration hoặc merge readiness. Chi tiết validation/failures/trust boundary ở docs/fs012, code checkpoint ở versioned retest manifest.
