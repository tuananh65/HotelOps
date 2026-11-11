# Host Git Bash continuation
Chạy trên host có quyền ghi Git common directory và mạng, trong đúng worktree. Đây là các bước còn lại, chưa được thực thi. Không chạy cả tài liệu như script; dừng khi lệnh lỗi hoặc source ownership chưa rõ.
Các báo cáo fallback hiện là untracked; bảo toàn và cập nhật chúng bằng evidence mới trước commit. Không dùng git clean/reset/stash.

## 1. Verify and fetch
```bash
cd /c/Users/Admin/Documents/GitHub/HotelOps_DEV2_PR25_FINAL_COORD_001
pwd
git rev-parse --show-toplevel
git branch --show-current
git rev-parse HEAD
git status --porcelain
git worktree list --porcelain
git rev-parse --git-common-dir
git sparse-checkout list
git remote -v
git fetch origin
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers refs/heads/integration/fullstack-base
git rev-parse origin/feature/dev2-dev3-master001-responsibility-readers
git rev-parse origin/integration/fullstack-base
git rev-list --left-right --count HEAD...origin/feature/dev2-dev3-master001-responsibility-readers
git status
```
Feature local/remote phải khớp pin đã chấp thuận; chỉ các evidence files fallback được untracked. Không có merge/rebase đang chạy. Nếu integration tiến lên, ghi SHA và inspect relevant delta mới.
```bash
FEATURE_BEFORE=$(git rev-parse HEAD)
INTEGRATION_SYNC=$(git rev-parse origin/integration/fullstack-base)
git diff --stat 8fd30f2f12e9dae102de2826e0be20e99e6a14ad "$INTEGRATION_SYNC"
git merge --no-ff --no-commit origin/integration/fullstack-base
git diff --name-only --diff-filter=U
git diff --cached --stat
```
Giải quyết từng conflict theo ownership; không chọn toàn file ours/theirs. Review DI và tất cả guards trong 03. Khi index không còn conflict:
```bash
git write-tree
git diff --check
git diff --cached --check
```
Ghi exact combined tree trước test. Hash source và ghi mọi exit code/output vào receipts.

## 2. Verify combined source
Đọc từng runner Program.cs và project references trước khi chạy; chỉ no-argument no-SQL mode. Nếu sparse thiếu tracked path, chỉ thêm path cần thiết trong worktree này. Không truyền database/connection string.
```bash
dotnet build HotelOps.slnx --nologo
dotnet run --project tests/HotelOps.Dev3Unblock.NoSql/HotelOps.Dev3Unblock.NoSql.csproj --no-launch-profile
dotnet test tests/HotelOps.Dev1.RegressionTests/HotelOps.Dev1.RegressionTests.csproj --filter "FullyQualifiedName~BoundaryTests" --nologo
dotnet run --project scripts/verification/UserAccountDeactivation/UserAccountDeactivation.Verification.csproj --no-launch-profile
dotnet run --project scripts/verification/UserRoleRevocation/UserRoleRevocation.Verification.csproj --no-launch-profile
dotnet run --project scripts/verification/RoomLifecycleCandidate/RoomLifecycleCandidate.Verification.csproj --no-launch-profile
dotnet run --project scripts/verification/DepartmentDeactivationCandidate/DepartmentDeactivationCandidate.Verification.csproj --no-launch-profile
dotnet run --project scripts/verification/StructureDeactivationPreview/StructureDeactivationPreview.Verification.csproj --no-launch-profile
dotnet run --project scripts/verification/WorkdayReadRouting/WorkdayReadRouting.Verification.csproj --no-launch-profile
```
Chạy thêm focused regressions liên quan; kiểm tra solution test projects trước dotnet test HotelOps.slnx để không chạy SQL trái phép. Ghi số discovery thực tế; không tự gán 338. Mọi failure phải lưu và xử lý theo ownership.

## 3. Evidence, commit and publish
Cập nhật cả 7 báo cáo, manifest, sanitized logs bằng kết quả thực. Review secrets/path ownership. Chỉ sau khi validation thành công:
```bash
git diff --name-status "$INTEGRATION_SYNC"...HEAD
git diff --cached --name-status
git add -- docs/integration-notes/PR25-FINAL-COORD-001
git diff --cached --check
git commit -m "merge(dev2): reconcile PR25 with latest fullstack integration"
git rev-list --parents -n 1 HEAD
git log -1 --format=fuller
git status --short
```
Verify parent 1=$FEATURE_BEFORE, parent 2=$INTEGRATION_SYNC. Nếu report cần final SHA, dùng normal follow-up commit, không amend/circular hash.
Recheck remote ngay trước push; nếu feature hoặc integration drift, dừng để reconcile/reverify, không force.
```bash
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers refs/heads/integration/fullstack-base
git merge-base --is-ancestor origin/feature/dev2-dev3-master001-responsibility-readers HEAD
git push origin feature/dev2-dev3-master001-responsibility-readers
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers
git rev-parse HEAD
```
Xác nhận remote=local. Dùng GitHub connector để refetch PR metadata/changed paths/mergeability, update existing PR25 và gửi đúng một targeted DEV3 request đã điền pins/receipts. Không tạo PR khác; không merge PR25/51. Chỉ xin PL merge decision sau DEV3 APPROVED_SOURCE_ONLY.
