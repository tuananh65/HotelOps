# Build and verification receipts
SOURCE=2140522457e89b974cf2d65c64515ec15d733fa9
COMBINED_TREE=NOT_CREATED
Build: Not Run. Tests: Not Run. Không có số PASS mới; không có exit code build/test.
Không chạy suite trên source cũ để thay thế chứng cứ hậu đồng bộ.
338 là tổng lịch sử; không phải kết quả phiên này.
Restore/warnings/errors: NOT_OBSERVED.
SQL execution: NOT_RUN (mọi suite). API/DB runtime: NOT_RUN.

| Command | Status | Exit | PASS/FAIL/SKIP |
|---|---|---|---|
| dotnet build HotelOps.slnx --nologo | NOT_RUN | N/A | N/A |
| dotnet test HotelOps.slnx | NOT_RUN | N/A | N/A |
| dotnet run --project tests/HotelOps.Dev3Unblock.NoSql/HotelOps.Dev3Unblock.NoSql.csproj --no-launch-profile | NOT_RUN | N/A | N/A |
| dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --filter "FullyQualifiedName~BoundaryTests" --nologo | NOT_RUN | N/A | N/A |
| dotnet run --project scripts/verification/UserAccountDeactivation/UserAccountDeactivation.Verification.csproj --no-launch-profile | NOT_RUN | N/A | N/A |
| dotnet run --project scripts/verification/UserRoleRevocation/UserRoleRevocation.Verification.csproj --no-launch-profile | NOT_RUN | N/A | N/A |
| dotnet run --project scripts/verification/RoomLifecycleCandidate/RoomLifecycleCandidate.Verification.csproj --no-launch-profile | NOT_RUN | N/A | N/A |
| dotnet run --project scripts/verification/DepartmentDeactivationCandidate/DepartmentDeactivationCandidate.Verification.csproj --no-launch-profile | NOT_RUN | N/A | N/A |
| dotnet run --project scripts/verification/StructureDeactivationPreview/StructureDeactivationPreview.Verification.csproj --no-launch-profile | NOT_RUN | N/A | N/A |
| dotnet run --project scripts/verification/WorkdayReadRouting/WorkdayReadRouting.Verification.csproj --no-launch-profile | NOT_RUN | N/A | N/A |

Additional focused regressions: NOT_RUN. Inspect no-argument runners before execution; never supply SQL arguments.
git diff --check and git diff --check origin/integration/fullstack-base...HEAD: exit 0 before reports.
Exact infrastructure failures: receipts/01_environment_failures.txt; SHA256 in 05_EXACT_DIFF_AND_HASHES.json.
