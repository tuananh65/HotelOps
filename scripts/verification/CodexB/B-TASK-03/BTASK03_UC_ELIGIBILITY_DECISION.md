# B-TASK-03 — Quyết định eligibility

Kết quả: BLOCKED. Không chọn UC để triển khai khi chưa đáp ứng contract/schema/authorization đầy đủ.

Đã đọc trực tiếp các artifact được cho phép trong worktree A tại scripts/verification/Coordination/CODEX_B_UC013_READONLY_HANDOFF_20261105_4/. Không đọc/copy application source A. Toàn bộ 13 file được liệt kê trong EVIDENCE_MANIFEST.json khớp SHA-256. Manifest HEAD và B HEAD cùng 4a187320643d0c82f232c6b9dd5a667b8401c280; đây không chứng nhận uncommitted source hai worktree giống nhau.

CSV SHA-256: AAAD6E638D32F1949D28184F9D2AAF4939477664CD1FF4DB452EAF20D9E19C8F. Export đủ 75 UC; snapshot nguồn scripts/verification/Task80/UC_LEDGER_TASK80.json, không phải live SQL. Tổng baseline được handoff ghi 26/75, 60/256. Không cập nhật ledger.

| UC | Status ledger | Weight | Credited | Quyết định |
|---|---|---|---|---|
| UC013 | COMPLETE_VERIFIED_V1 | 2 | 2 | Loại; Task64 |
| UC023 | COMPLETE_VERIFIED_V1 | 2 | 2 | Loại; Task67, task67-accepted.local.json |
| UC024 | COMPLETE_VERIFIED_V1 | 2 | 2 | Loại; Task71, task71-complete.local.json |
| UC048 | COMPLETE_VERIFIED_V1 | 2 | 2 | Loại; row ghi HTTPS/SQL verified, exact receipt path còn PENDING_EVIDENCE; không được coi thiếu receipt là chưa credited |
| UC015 | COMPLETE_VERIFIED_V1 | 2 | 2 | Loại; Task65, task65-accepted-final2.local.json |

Đã lọc toàn bộ 49 row credited=0, không historical COMPLETE_VERIFIED trong export; không suy từ source B thiếu API rằng UC là mới. Các UC bị user loại trừ/reserved không được chọn.

Fallback kiểm tra trực tiếp:

- UC107, weight2, NOT_IMPLEMENTED/credited0: SRS §11.6 yêu cầu accept/decline, status Assigned/Accepted/Pending Assignment, optimistic concurrency/audit và notification khi decline. Release SQL CK_operational_tasks_status chỉ có TODO/IN_PROGRESS/BLOCKED/COMPLETED/CANCELLED; OperationalTask không rowversion. TASK_EXECUTE grants là OWN; resolver B chỉ hỗ trợ cleaning_task/CLEANING_EXECUTE. Cần approved status+concurrency mapping, task ownership resolver và publisher; không thể chỉ thêm DI.
- UC103, weight2, NOT_IMPLEMENTED/credited0: SRS §11.2 có notification, expected duration/deadline history, cancel và stop recurrence series; B thiếu series/interval foundation và publisher. Không chọn planning-only subset.
- UC117, weight2, NOT_IMPLEMENTED/credited0: SRS §13.4 progress/deadline change, reason, notification/audit; B thiếu publisher và IR workflow/concurrency baseline. Không bỏ luồng đổi deadline để coi UC hoàn thành.

Nhóm còn lại theo exact ledger evidence và B source:

- UC004/006/007/010: partial user administration, thiếu full-role/transfer/revoke/deactivate contracts và responsibility integration. B chỉ có allowlist/add/activation patterns; không mở rộng approved allowlist hoặc shared auth policy.
- UC008/009: fixed canonical role catalog và permission editor/anti-escalation contract chưa được duyệt.
- UC016/022/026: retained DEV2 writer/reference coordination chưa resolved; không sửa DEV2.
- UC027/028/030/031/033/034/035/036/047: room lifecycle, work intervals/current-cycle coordination chưa có baseline tương ứng trong B.
- UC102/104/105/106/108/110/111/112/113/140: thiếu recurring series, criteria, interval, verification/correction hoặc acknowledged-handover contracts/models.
- UC114/115/116/118/119/120/121/122/123: notification, linked-work, receiving/handler policies hoặc handover dependencies; UC123 có withdrawal mutation, không chỉ read list.

UC012/013/029/037/038/041/044/045/046/138/139/142 bị loại theo user scope/credit/reservation. Snapshot ghi Codex A current task COORD-FINAL-01, DEV2 live roster PENDING_EVIDENCE; không tuyên bố có fresh reservation riêng cho fallback.

Không có UC đáp ứng tất cả điều kiện chọn. Không viết source với invented status/permission hoặc placeholder registration. Đề xuất nhiệm vụ sau: chốt riêng UC107 status/concurrency/ownership/publisher rồi reserve cho B; nếu ưu tiên recovery UC023 thì phải giao scope recovery, không cộng 2 điểm lần nữa.
