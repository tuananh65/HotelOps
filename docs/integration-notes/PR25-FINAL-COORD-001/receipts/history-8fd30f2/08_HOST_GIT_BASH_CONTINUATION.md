# Host Git Bash — commit prepared, verified merge

Fetch và merge đã hoàn tất trên host. Không chạy lại merge. Source validation Passed tại tree 14f4595c0edeb1f6349d636ec1a08a22a287f569; build exit 0, 535 checks PASS. Không có production source corrections.

## 1. Verify exact prepared state

```bash
cd /c/Users/Admin/Documents/GitHub/HotelOps_DEV2_PR25_FINAL_COORD_001
git branch --show-current
git rev-parse HEAD
git rev-parse MERGE_HEAD
git ls-files -u
git write-tree
git diff --quiet
git diff --cached --check
git diff --cached --name-status MERGE_HEAD
git status --short
```

Expected branch feature/dev2-dev3-master001-responsibility-readers; HEAD 2140522457e89b974cf2d65c64515ec15d733fa9; MERGE_HEAD 8fd30f2f12e9dae102de2826e0be20e99e6a14ad; write-tree 14f4595c0edeb1f6349d636ec1a08a22a287f569; 0 conflicts; exactly 24 functional paths. git write-tree chỉ dành host. Dừng nếu source drift.

Read 04 và 07, kiểm receipt hashes bằng manifest. Evidence .txt được giữ để tránh *.log ignore. bin/obj/.log không stage. Không sửa mappings để xử lý warning ngoài scope.

## 2. Stage evidence and commit

```bash
git add -- docs/integration-notes/PR25-FINAL-COORD-001
git diff --cached --check
git diff --cached --name-status MERGE_HEAD
git commit -m "merge(dev2): reconcile PR25 with latest fullstack integration"
git rev-list --parents -n 1 HEAD
git log -1 --format=fuller
git status --short
```

Commit phải có đúng 2 parents, thứ tự original feature 2140522 rồi integration 8fd30f2. Sau evidence staging full Git tree sẽ khác tested pre-evidence tree; production source phải giữ nguyên. PR delta gồm 24 functional paths + exact evidence paths trong manifest; không báo tổng chỉ 24.

Điền merge SHA/final HEAD vào PR draft 09 và review request 06. Nếu cần commit evidence follow-up, dùng normal commit riêng, không amend, không tạo circular self-hash.

## 3. Check remote drift, publish and hand off

```bash
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers refs/heads/integration/fullstack-base
git merge-base --is-ancestor 2140522457e89b974cf2d65c64515ec15d733fa9 HEAD
git push origin feature/dev2-dev3-master001-responsibility-readers
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers
git rev-parse HEAD
```

Trước push, remote feature phải vẫn là expected original SHA và remote integration vẫn là synced parent. Nếu drift, dừng để xem delta và xác minh lại; không force. Chỉ push sau khi ancestry/parents/status đều đúng, rồi xác nhận local=remote.

Dùng GitHub connector cập nhật existing PR25 bằng 09_PR25_DESCRIPTION.md, refetch exact changed paths/mergeability, gửi đúng một targeted request 06 đã điền final SHA. Không gửi khi chưa publish. DEV3 review PENDING; chỉ xin đúng một PL merge decision sau APPROVED_SOURCE_ONLY. Không merge PR25/51, không SQL/runtime activation.
