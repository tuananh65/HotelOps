# Host Git Bash — current upstream drift handoff

HOST_COMMIT_READY=NO. Backend/DI Passed 593 checks trên staged tree mới; frontend bị esbuild sandbox Access is denied trước assertions. Chưa stage/commit/push cho đến khi 4 frontend files được host xác minh và evidence cập nhật.

## 1. Verify current prepared merge

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
```

Expected feature HEAD=2140522457e89b974cf2d65c64515ec15d733fa9; integration=00bdc481cb224d7117a79c95669abc7016f51ba4; tree=85bd4cfa8816736231449abeb985a5b573fb79ab; zero conflicts; 24 functional paths. write-tree chỉ trên host. Không chạy lại merge.

## 2. Resolve frontend verification on host

Indexed source copies hiện tại nằm trong ignored workspace scratch, exact input hashes ở receipts/upstream-00bdc481/frontend_inputs.json. Original node_modules chỉ được đọc, không install/sửa. Chạy trên host có quyền directory access:

```bash
cd /c/Users/Admin/Documents/GitHub/HotelOps_DEV2_PR25_FINAL_COORD_001/bin/PR25-UC138-95d870a47f684d9c9ad4425afdbf4950
"/c/Program Files/nodejs/node.exe" --test tests/shift-plan-api.test.mjs
"/c/Program Files/nodejs/node.exe" --test tests/secure-shift-plan-browser.test.mjs
"/c/Program Files/nodejs/node.exe" --test tests/shift-plan-preview.test.mjs
"/c/Program Files/nodejs/node.exe" --test tests/shift-plan-preview-browser.test.mjs
```

Giữ cả 8 failure receipts cũ. Lưu fresh output, exact exit/counts/cwd; yêu cầu 0 fail và actual discovery, không suy count từ PR63 historical330. Nếu vẫn lỗi hoặc source defect thuộc DEV3, dừng và báo exact defect/owner. Không thay đổi assertions/guards. Sau success, cập nhật reports/PR draft/review request/manifest bằng counts thực và HOST_COMMIT_READY=YES. Refresh helper đọc current command metadata; không tự đánh dấu frontend success.

## 3. Commit only after completed verification

```bash
cd /c/Users/Admin/Documents/GitHub/HotelOps_DEV2_PR25_FINAL_COORD_001
git add -- docs/integration-notes/PR25-FINAL-COORD-001
git diff --cached --check
git diff --cached --name-status MERGE_HEAD
git commit -m "merge(dev2): reconcile PR25 with latest fullstack integration"
git rev-list --parents -n 1 HEAD
git status --short
```

Một normal merge commit, đúng 2 parents theo thứ tự feature 2140522 rồi integration 00bdc481. Full committed tree sẽ khác pre-evidence tested tree; source blobs phải giữ nguyên. PR delta=24 original paths + exact evidence paths ở manifest, không báo tổng chỉ24. Nếu cần follow-up evidence commit, dùng normal commit, không amend/circular hash.

## 4. Recheck drift and publish normally

```bash
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers refs/heads/integration/fullstack-base
git merge-base --is-ancestor 2140522457e89b974cf2d65c64515ec15d733fa9 HEAD
git push origin feature/dev2-dev3-master001-responsibility-readers
git ls-remote origin refs/heads/feature/dev2-dev3-master001-responsibility-readers
git rev-parse HEAD
```

Trước push remote feature phải vẫn expected original SHA, integration phải vẫn00bdc481; drift cần targeted validation, không force. Xác minh local=remote, điền final merge/head SHA vào09 và06, update existing PR25 và current changed paths/mergeability. Gửi một targeted DEV3 request chỉ sau successful publication; xin PL merge decision chỉ sau APPROVED_SOURCE_ONLY. Không self-merge PR25/51 hoặc activate SQL/Workday/notifications.
