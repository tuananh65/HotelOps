# Actual post-sync build and offline checks

SOURCE_TREE=14f4595c0edeb1f6349d636ec1a08a22a287f569
FEATURE_PARENT=2140522457e89b974cf2d65c64515ec15d733fa9
INTEGRATION_PARENT=8fd30f2f12e9dae102de2826e0be20e99e6a14ad

Build: Passed. Exact command: dotnet build HotelOps.slnx --nologo. Exit 0, 0 errors, 2 NU1900 warnings; restore completed, NuGet vulnerability service unreachable. Receipt: receipts/02_build.txt.

| Suite | PASS | FAIL | SKIP | Exit |
|---|---:|---:|---:|---:|
| DEV2 NoSql | 92 | 0 | 0 | 0 |
| BoundaryTests (real xUnit discovery) | 67 | 0 | 0 | 0 |
| UserAccountDeactivation | 12 | 0 | 0 | 0 |
| UserRoleRevocation | 54 | 0 | 0 | 0 |
| RoomLifecycleCandidate | 29 | 0 | 0 | 0 |
| DepartmentDeactivationCandidate | 25 | 0 | 0 | 0 |
| StructureDeactivationPreview | 27 | 0 | 0 | 0 |
| WorkdayReadRouting | 44 | 0 | 0 | 0 |
| DepartmentChange | 45 | 0 | 0 | 0 |
| ShiftSchedulePlanning | 24 | 0 | 0 | 0 |
| ShiftPlanPreview | 57 | 0 | 0 | 0 |
| ScheduledWorkday | 43 | 0 | 0 | 0 |
| DI composition probe (successful rerun) | 16 | 0 | 0 | 0 |

Required eight slices: 350 PASS; additional existing regressions: 169 PASS; DI probe: 16 PASS. TOTAL_ACTUAL_CHECKS=535. Final successful runs: 0 FAIL, 0 SKIP. Console-runner counts là assertions/checks; BoundaryTests là xUnit discovered tests. Không cộng assertion đã chạy dở trong probe failures hoặc historical counts.

UserRoleRevocation hiện chạy 54 thay vì lịch sử 42; các checks mới về complete responsibility writers/final reread được giữ nguyên từ integration. Historical 338 không được dùng làm receipt mới.

dotnet test HotelOps.slnx exit 0 nhưng solution chỉ chứa API project, không discover test project/test. Không tính lệnh này là test PASS; BoundaryTests được chạy trực tiếp trên test csproj và discover 67.

Exact commands, cwd, exit code, PASS/FAIL/SKIP, SQL status, output paths và SHA256: receipts/verification_summary.json. Raw console output đã sanitize worktree prefix thành <WORKTREE>, lưu .txt để không bị *.log ignore. Mỗi command có .command.json; manifest chứa SHA256 từng receipt.

## Failures retained

1. DI probe đầu: exit 1, CS9137 do OpenAPI generator interceptor namespace của verification csproj; sửa harness csproj. receipts/DiComposition_first_failure.txt.
2. Rerun mặc định Production: actual cookie environment guard từ chối trước SQL; process session được interrupt exit 1 sau unhandled exception, child exit không capture. receipts/DiComposition_environment_failure.txt.
3. Windows Development: 14 assertions đi qua, logger không ghi được Windows Event Log trong sandbox; exit 1. Chuyển riêng probe sang console logger. receipts/DiComposition_eventlog_failure.txt.
4. Rerun cuối exit 0, 16 PASS. receipts/DiComposition_verified.txt.

Không có source failure DEV2/DEV3; không có source corrective edit. Chỉ harness được sửa; source tree không đổi nên các suite production-source đã chạy vẫn cùng identity. Cảnh báo model EF 20602 (3 indexes) được ghi ở DI receipt; không sửa mapping.

SQL/HTTPS/races/rollback/deadlock/live authorization/database connectivity: NOT_RUN. SQL_ACCEPTANCE_19=NOT_RUN. Tất cả runners được kiểm mode trước khi chạy; chỉ no-argument offline mode. IncidentMaintenance default và --responsibility-coordination đều là SQL trials nên không chạy.
