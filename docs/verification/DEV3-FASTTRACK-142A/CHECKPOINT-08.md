# FASTTRACK-142A — checkpoint 08: dashboard fullstack có phạm vi

Baseline `13e793abfdfafdccd3e1147b16448a041495c0fe`; canonical `integration/dev1-dev2-dev3-unblock-20261114`. Sprint ACTIVE, đây là tiến độ triển khai, không phải nghiệm thu 144 UC.

## Source và hành vi

Khôi phục chọn lọc DashboardService và DashboardsController từ candidate `4ce3c4703ad621475d4da97ee2d4fc7e4a56276b`; không merge lại cả nhánh. Build đầu tiên thất bại CS0103 do thiếu MaintenanceWorkSources. Khôi phục phần projection truy vấn từ candidate thành helper internal riêng trong Services/Insights; không đăng ký resource authorization reader khác. Không thay EF entities/mappings, schema hay permission catalog.

GET `/api/dashboards/operations` dùng session hiện tại, quyền DASHBOARD_VIEW hiện có, phạm vi branch và department. Các truy vấn trong cùng SERIALIZABLE transaction. GET `/api/dashboards/options` chỉ trả branch/department được phép. DTO không lộ entities; response không cache. Maintenance gắn incident hoặc đúng một preventive run và một target mới suy ra branch; loại record có department mâu thuẫn branch.

Đã nối `/dashboard` vào router canonical và navigation. UI hiển thị snapshot UTC, phạm vi, bộ lọc và refresh chủ động; phân phối trạng thái đang lưu và số lượng theo từng item/unit/warehouse. Lượng decimal truyền dạng chuỗi, không làm tròn qua JS number. Dữ liệu và response muộn bị loại khi authority thay đổi; 401 chỉ invalidate đúng authority gửi request. Lỗi trả thông báo an toàn, không hiện exception SQL, không tự replay.

Đối chiếu SRS UC131–134, GBR161–162: trạng thái READY đang lưu chưa chứng minh effective readiness, nên nhãn ghi rõ Recorded READY và không công bố là KPI readiness đã nghiệm thu. Mẫu số zero hoặc thiếu nguồn trả null/N/A. Room/incident không có department mapping thì giữ N/A cho bộ lọc department. Period áp dụng stock movements; UI ghi rõ các chỉ số khác phản ánh trạng thái hiện tại. Không cộng chung các đơn vị vật tư. Chưa có dropdown theo options, drill-down hoàn chỉnh, SLA/overdue/effort/first-pass/reopened/transfer-loss; chưa đánh dấu UC COMPLETE.

## Kiểm chứng trực tiếp

- `dotnet build HotelOps.slnx`: PASS, 0 warnings/errors (`checkpoint08-build-final.log.txt`).
- `dotnet test HotelOps.slnx`: exit 0, chỉ restore, không discover test; không tính là suite PASS (`checkpoint08-solution-test.log.txt`).
- Existing Insight AuditRedactionTests: 7 PASS, 0 fail/skip (`checkpoint08-insight-regression.log.txt`).
- `npm.cmd run typecheck` và `npm.cmd run build`: PASS (`checkpoint08-typecheck-final.log.txt`, `checkpoint08-frontend-build-final.log.txt`). Vite vẫn cảnh báo use-client và bundle >500 kB (~561 kB); không giấu warning. npm ci không chạy lại vì lockfile/dependencies không đổi; kết quả baseline là lịch sử.
- `--sprint-dashboard`: 25 checks PASS, cleanup=true (`dashboard-https-second.json`). Có login/session/logout thực; không grant trả403; đúng branch đếm hai phòng active, một READY/50%, một open incident và maintenance; không rò foreign/inactive; aggregate chỉ các branch được phép; department-unmapped null; branch rỗng tỷ lệ null; options không lộ foreign; foreign branch/department403; ID0/period đảo/max date400; thu hồi role trả403.
- Browser auth/router: 137 PASS, 0 fail/cancel/skip (`checkpoint08-router-verified.log.txt`), gồm 10 case dashboard mới. Browser dùng fetch fixture; HTTPS gọi API/SQL thực riêng, không phải một luồng browser-to-SQL E2E.

Lượt HTTPS đầu FAIL do fixture chọn SYS_ADMIN không có DASHBOARD_VIEW. Catalog cấp quyền này cho OPERATIONS_MANAGER BRANCH; sửa fixture dùng đúng role, không thêm permission. `dashboard-https-first.json` giữ nguyên failure và cleanup=true. Browser lượt đầu có lỗi vị trí đăng ký test trước hook và assertions fallback cũ `/rooms/status`; dashboard nay là default route, cập nhật assertions `/dashboard`, vẫn giữ kiểm tra chặn returnURL ngoài hệ thống. Hai log trước được giữ là failed/interrupted, không tính PASS.

## SQL/HTTPS safety và giới hạn

Exact instance `meita`, database `HotelOps_DEV3_142A_20261114_cc78c13f`, GUID `1D5A9B53-7D19-435A-96AB-9FDFF447B6E0`, marker `cc78c13ffc124efcad67b3d804baa84c` được outer harness kiểm tra trước mutation. Dữ liệu synthetic duy nhất; fixture HTTP cần commit để API đọc, sau đó cleanup chính xác trong transaction. Outer probe dọn users/sessions/audits. Không tác động HotelOpsDB/ImportReview. API localhost HTTPS dùng trust chain mặc định, không bỏ TLS validation; SQL TrustServerCertificate không được coi là chứng minh SQL certificate. API trial được dừng sau kiểm chứng.

Positive inventory movement/threshold/request/replenishment và preventive-run SQL fixtures chưa chạy; các truy vấn đã thực thi với tập rỗng. Maintenance corrective branch có fixture thật. Runtime model warnings RoomTypeBranchV1 là tồn tại trước đó. Không suy ra production readiness hoặc full E2E từ kết quả này.

## Receipt

TASK_ID=DEV3-FASTTRACK-142A
CHECKPOINT=08_ACTIVE
ACTUAL_BRANCH=integration/dev1-dev2-dev3-unblock-20261114
INITIAL_SHA=8d2f1e0375c8654cdfaffa2ebc8c9c2e99a3292e
SOURCE_IMPLEMENTED=SCOPED_DASHBOARD_API_AND_CANONICAL_UI
CHANGED_PATHS=src/HotelOps.Api/{Program.cs,Controllers/DashboardsController.cs,Services/Insights/DashboardService.cs,Services/Insights/MaintenanceWorkSources.cs}; frontend/src/{auth/AuthRouteHost.tsx,shared-host/SharedAuthHost.tsx,insights/DashboardPage.tsx,insights/dashboardApi.ts}; frontend/tests/shared-auth-host.test.mjs; scripts/verification/CanonicalDatabase/ModelProbe/{Program.cs,SprintAuthProbe.cs,SprintDashboardScenario.cs}; docs/verification/DEV3-FASTTRACK-142A/{CHECKPOINT-08.md,UC_STATUS_MATRIX.md,checkpoint08*,dashboard-https*.json}
TESTS_EXECUTED=137_AUTH_ROUTER_PASS;7_INSIGHT_REDACTION_PASS;25_HTTPS_SQL_CHECKS_PASS
BUILD_STATUS=BACKEND_TYPECHECK_FRONTEND_PASS
FE05_STATUS=PARTIAL_INTERNAL_COMPONENTS;UNCHANGED_THIS_CHECKPOINT
UC006_UC010_STATUS=PARTIAL;ACTUAL_DEACTIVATION_REVOKE_WRITERS_PENDING
FE11_STATUS=PARTIAL
FE13_STATUS=PARTIAL
FE14_STATUS=DASHBOARD_PARTIAL_SCOPED_READS_IMPLEMENTED;NOTIFICATIONS_AND_KPIS_PENDING
FE15_STATUS=AUDIT_READ_IMPLEMENTED_PREVIOUSLY;REPORTS_PENDING
ISOLATED_SQL_STATUS=25_HTTPS_CHECKS_PASS;FIXTURE_CLEANUP_TRUE
HTTPS_API_STATUS=TRUSTED_LOCALHOST_AUTH_AND_DASHBOARD_READS_VERIFIED
E2E_VERIFIED_WORKFLOWS=NO_NEW_BROWSER_TO_SQL_E2E;HTTP_SQL_AND_MOCK_BROWSER_SEPARATE
UC_MATRIX_TOTAL=144
UC_COMPLETE_VERIFIED=0
UC_SOURCE_READY=0
UC_PARTIAL=144
UC_BLOCKED=0
CRITICAL_BLOCKERS=FE05_PUBLIC_CONCLUSION_NOTIFICATION_PROJECTION;UC006_010_WRITERS;FE11_13_WORKFLOWS;MISSING_KPI_CONTRACTS;BROWSER_SQL_E2E
NEXT_IMPLEMENTATION=Positive inventory/PM dashboard fixtures and scoped dropdowns; confirmed FE11/FE13 workflows; FE05 lifecycle composition

CURRENT_SHA/COMMIT_SHAS/PUSH_STATUS được ghi ở receipt giao hàng sau commit để tránh self-reference.
