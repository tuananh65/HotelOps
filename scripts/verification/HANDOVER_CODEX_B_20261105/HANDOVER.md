# Codex B final snapshot — 2026-11-05

## Phạm vi và provenance

Worktree D:\SEP490\Projects\HotelOps-Codex-B; branch ban đầu ai/codex-b; baseline HEAD 4a187320643d0c82f232c6b9dd5a667b8401c280. Index và tracked diff ban đầu rỗng. Người dùng đã cho phép tạo branch handoff/codex-b-20261105, selective commit và push, không merge develop/main.

Snapshot bảo toàn 15 file có sẵn: codex-b-smoke.txt, sáu artifact B-TASK-01, sáu artifact B-TASK-03 và hai artifact B-TASK-04. Exact paths, sizes và SHA-256 trong CODEX_B_ARTIFACT_MANIFEST.json. Không có production source/script thực thi mới do B viết trong các tác vụ này. B-TASK-02 không tìm thấy trong tracked/untracked file inventory của B; chỉ có đề xuất nhiệm vụ đó trong B-TASK-01. Không dựng lại artifact thiếu hoặc nhận source baseline là công của B.

BASELINE_FILE_INVENTORY.txt liệt kê toàn bộ 660 tracked paths trong src/scripts/docs/tests/database tại baseline: source, scripts, requirement/permission contracts và evidence cũ được bảo toàn qua Git parent. Không sao chép source worktree A/DEV2. Ignored B-TASK-03/build-output là generated binaries, không phải deliverable; giữ local, không stage. Không reset/clean/xóa worktree hoặc truy cập database.

## Ledger và task outcomes

Các báo cáo cũ được giữ nguyên byte; quyết định người dùng sau B-TASK-04 có ưu tiên: B-TASK-03 và UC107 là BLOCKED_EXTERNAL; B-TASK-04 chưa được phép implementation. Không coi nhãn BLOCKED_PRECONDITIONS trong artifact cũ là authorization.

| Nội dung | Evidence và trạng thái |
|---|---|
| B-TASK-01 | Readiness/selection UC013/113/123; không triển khai, không credit |
| B-TASK-02 | Artifact không có trong worktree B; không xác nhận đã thực hiện |
| B-TASK-03 | Ledger/eligibility analysis, BLOCKED_EXTERNAL; 0 production changes, 0 credit |
| B-TASK-04 / UC107 | BLOCKED_EXTERNAL; người dùng xác nhận Codex A chưa bàn giao contract và KHÔNG cấp implementation authorization |
| UC013/023/024/048/015 | Báo cáo B-TASK-03 ghi COMPLETE_VERIFIED_V1, weight 2 / credited 2 mỗi UC; không được credit lần nữa |
| UC107/103/117 | B-TASK-03 ghi NOT_IMPLEMENTED, weight 2 / credited 0; vẫn thiếu dependencies/approval |
| Tổng lịch sử | Snapshot Task80 được B-TASK-03 ghi 26/75 UC, 60/256 điểm; 49 row credited=0; không phải ledger live hiện tại |

CSV lịch sử được B-TASK-03 ghi SHA-256 AAAD6E638D32F1949D28184F9D2AAF4939477664CD1FF4DB452EAF20D9E19C8F, 13/13 handoff hashes khớp tại thời điểm đó. Exact original ledger/receipts không có trong B; handover này không xác minh lại hoặc cấp credit mới. Các nhóm UC blocked khác được liệt kê nguyên văn tại BTASK03_UC_ELIGIBILITY_DECISION.md. Không sửa official ledger. UC mới hoàn thành bởi B: 0.

## Contract/permission đã xác minh ở mức source

- IPermissionService có EvaluateGlobalAsync, EvaluateBranchAsync và EvaluateResourceAsync. Branch evaluation không chứng minh ownership; resource evaluation cần backend resolver. Đây là contract source baseline, chưa là runtime acceptance mới.
- CleaningTaskResourceResolver gắn cleaning_task với CLEANING_EXECUTE, chỉ IN_PROGRESS, đúng một ACTIVE assignment chưa ended, đúng current user; PermissionService tiếp tục kiểm tra grant/branch. Không tái sử dụng như TASK_EXECUTE resolver khi chưa được duyệt.
- TASK_EXECUTE/OWN có trong baseline delegation allowlist. Không suy ra OWN là BRANCH hoặc authorization UC107 đã được phê duyệt.
- SRS UC107: accept Assigned → Accepted; decline cần lý do, về Pending Assignment và gỡ assignee; audit atomic; durable notification Department Head có retry/deduplication theo GBR-010. OperationalTask baseline không có RowVersion; không tự lấy UpdatedAt làm concurrency token.
- Department Head quản lý trong branch; Reception permissions/room lifecycle vẫn có pending revisions theo SRS_CHANGE_NOTES.md.

## Có thể tái sử dụng và phần chưa được áp dụng

Có thể chuyển tiếp: requirement/source analysis, acceptance matrices, evidence provenance/hashes, danh sách dependency, regression checklist và baseline permission API conventions. Các tài liệu này phục vụ planning/review; không chứng minh endpoint đã chạy.

Không được áp dụng trước approval: đề xuất UC013 permission/DI; UC107 state/concurrency/assignment history, OWN resolver, publisher/outbox, retry/deduplication và DI; UC113 work-interval schema/time recording; UC123 read/withdrawal scope và notification; UC103 series/duration/deadline history; UC117 progress/deadline workflow. Không dùng mock/no-op publisher để đạt acceptance, không sửa RBAC/schema/shared foundation chỉ từ các proposal này.

Blocker UC107 cần Codex A cung cấp đủ: exact eligibility ledger; reservation B; state/concurrency; ownership và resource scope; durable notification transaction/recipient/read permission/retry/deduplication; shared DI integration path; explicit authorization YES. Trial HTTPS/SQL riêng cũng chưa được cấp. Không mở lại readiness audit thay cho các quyết định này.

## Validation và giới hạn

- B-TASK-01: build/tests Not Run.
- B-TASK-03: baseline build Passed, 2 NU1900 warnings do vulnerability feed không truy cập được; tests 0/Not Run.
- B-TASK-04: dotnet build HotelOps.slnx Passed, 0 warnings/errors; tests Not Run, 0 cases; HTTPS/SQL Not Run.
- Snapshot này chỉ thêm documentation/inventory; không sửa C# và không chạy lại build/test/database. Kiểm tra secret candidates và staged diff trước commit; kết quả sẽ được báo trong phiên thực thi. Không có kiểm thử tính năng UC107 thành công để kế thừa.

## Chuyển vai trò DEV1

Theo phân công mới của người dùng: DEV1 phụ trách FE-03 Room Operations, FE-04 Housekeeping, FE-05 Inspection, FE-11 Operational Task Assignment, FE-13 Internal Requests. DEV3 phụ trách FE-01/02/12. DEV2 giữ nguyên. Phân công này ưu tiên hơn ownership lịch sử trong AGENTS.md; snapshot không sửa shared instructions hoặc tự chuyển module source.

DEV1 có thể kế thừa phân tích room lifecycle/current-cycle, assignment và IR blockers; phần time interval/FE-12 cần phối hợp DEV3, notification/FE-14 cần DEV2, authentication/RBAC/structure cần DEV3. Không tự coi reassignment quyền sở hữu là approval cho schema/RBAC/contracts chưa chốt.

Snapshot chỉ hoàn tất khi SHA branch GitHub khớp local commit sau push. Chưa chuẩn bị worktree full-stack trong tác vụ snapshot; đây là bước tiếp theo sau xác minh remote. Worktree registry chỉ chứng minh metadata; không xác nhận file/index của worktree khác vì không truy cập chúng.
