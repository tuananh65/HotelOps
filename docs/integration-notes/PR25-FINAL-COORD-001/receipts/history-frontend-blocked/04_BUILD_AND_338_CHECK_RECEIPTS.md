# Fresh verification — new combined tree

SOURCE_TREE=85bd4cfa8816736231449abeb985a5b573fb79ab
INTEGRATION_PARENT=00bdc481cb224d7117a79c95669abc7016f51ba4

Build: Passed. dotnet build HotelOps.slnx --nologo; exit 0, 0 errors, 2 NU1900 warnings. Restore completed; vulnerability metadata unavailable vì NuGet endpoint không truy cập được. Fresh receipt: receipts/upstream-00bdc481/02_build.txt.

| Suite | PASS | FAIL | SKIP | Exit |
|---|---:|---:|---:|---:|
| DEV2 NoSql | 92 | 0 | 0 | 0 |
| BoundaryTests | 67 | 0 | 0 | 0 |
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
| DI composition probe | 16 | 0 | 0 | 0 |
| SecureShiftPreview | 58 | 0 | 0 | 0 |
| frontend shift-plan-api | 0 | 1 file failure | 0 | 1 |
| frontend secure-shift-plan-browser | 0 | 1 file failure | 0 | 1 |
| frontend shift-plan-preview | 0 | 1 file failure | 0 | 1 |
| frontend shift-plan-preview-browser | 0 | 1 file failure | 0 | 1 |

Backend/DI total=593 PASS, 0 FAIL, 0 SKIP (535 rerun trên tree mới + 58 SecureShiftPreview mới; không tái dùng lịch sử).
Final frontend attempts=0 PASS, 4 failed test files, 0 SKIP; không có nested API/browser assertions được discover/thực thi. ACTUAL_TOTAL_PASS_FAIL_SKIP=593/4/0 cho final executions. Bốn first-attempt frontend file failures cũng giữ riêng, không cộng lại vào final-attempt total.

## Frontend environment failure

Node v26.13.0 có sẵn ở C:/Program Files/nodejs/node.exe, không nằm PATH. Dependencies esbuild/Playwright/React từ node_modules hiện hữu được đọc qua junction, không install hoặc sửa original worktree.
Chỉ export indexed auth/workday source + 4 tests/package/tsconfig sang isolated copies; canonical input hashes ở receipts/upstream-00bdc481/frontend_inputs.json. Không materialize sparse frontend hoặc đổi Git metadata.

Cả 2 vị trí thử (OS temp, sau đó ignored workspace bin scratch) đều lỗi esbuild Cannot read directory ... Access is denied; lỗi resolve imports phát sinh trước assertions. Mỗi node --test exit 1, 1 failed file, pass 0/skip 0. Đây là environment/harness failure, không chứng minh source defect. Không sửa test/assertion/source để né giới hạn.
First receipts: frontend-*_first_failure.txt; second/current: frontend-*.txt. Metadata có exact cwd/command/exit code; không đánh dấu frontend PASS/NOT_RUN sau khi đã chạy và thất bại.

HOST_COMMIT_READY=NO; host cần chạy lại 4 files và cập nhật verdict/receipts. Source validation backend đã Passed nhưng publication readiness chưa đạt.

## Qualifications

New SecureShiftPreview runner được đọc trước execution: dùng Fixture in-memory, Save direct calls bị immutable pre-SQL gate trả 503, connection vẫn Closed; EF chỉ ToQueryString, không thực thi SQL.
DI probe 16 PASS giữ Windows Development/console logger, không production startup validator/listener/SQL. Ba pre-existing EF20602 RoomTypeBranchV1 warnings giữ trong fresh receipt.
dotnet test HotelOps.slnx exit 0 nhưng solution chỉ có API project; zero test discovery, không tính PASS. BoundaryTests chạy trực tiếp csproj, discover 67 thực.

Exact command metadata, source tree, counts và receipt SHA256 ở receipts/upstream-00bdc481/verification_summary.json. Receipt 535 checks trước vẫn ở receipts/ với identity cũ; snapshots ở receipts/history-8fd30f2/. Các DI harness failures của lượt trước vẫn giữ nguyên, không coi là lỗi mới trên tree hiện tại.

Không SQL/HTTPS/live API/mutation/race/rollback/migration; SQL_ACCEPTANCE_19=NOT_RUN. Source fixes=NONE.
