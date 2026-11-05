# B-TASK-04 — UC107 implementation blocker và integration handoff

Ngày: 2026-11-05 (Asia/Saigon). Kết quả: BLOCKED_PRECONDITIONS; chưa triển khai production source.

## Worktree và source ownership

- Absolute path: D:\SEP490\Projects\HotelOps-Codex-B.
- Branch: ai/codex-b. HEAD: 4a187320643d0c82f232c6b9dd5a667b8401c280.
- Git worktree registry xác nhận đúng path/branch/HEAD này.
- Trước tác vụ: chỉ có untracked codex-b-smoke.txt và scripts/verification/CodexB/; không có tracked changes.
- B giữ nguyên smoke file và B-TASK-01/03. Không đọc worktree A/DEV2, không truy cập SQL, không dùng Task82–86, không sửa shared source, không thực hiện Git mutation.
- UC107 thuộc DEV1, FE-11 + FE-12; reservation riêng cho B chưa có bằng chứng.

## Eligibility và contract evidence

Đã tìm trong worktree B, kể cả hidden files ngoài .git và build outputs, theo UC107/decision/contract/ledger/coordination. Không tìm thấy UC107 contract decision mới của Codex A. Không coi yêu cầu triển khai có điều kiện là bằng chứng rằng contract đã được phê duyệt.

| Preconditions | Evidence hiện có | Kết luận |
|---|---|---|
| Exact ledger, chưa credited, không historical COMPLETE_VERIFIED | B-TASK-03/BTASK03_UC_ELIGIBILITY_DECISION.md báo snapshot Task80: UC107 NOT_IMPLEMENTED, weight 2, credited 0; CSV hash AAAD6E638D32F1949D28184F9D2AAF4939477664CD1FF4DB452EAF20D9E19C8F | Chỉ bằng chứng gián tiếp từ báo cáo cũ; chưa có exact ledger mới để xác nhận độc lập |
| Reserved for B | Không có artifact reservation | Chưa xác nhận |
| Approved state/concurrency | B-TASK-03 ghi BLOCKED; OperationalTask không RowVersion; DbContext map Status default TODO, không có concurrency token trong mapping task | Thiếu approved mapping; không tự dùng UpdatedAt thay rowversion |
| Approved ownership/authorization | Resolver folder hiện chỉ có CleaningTaskResourceResolver và CleaningTaskAccessReader; TASK_EXECUTE/OWN xuất hiện trong delegation allowlist | Thiếu approved task resolver/integration path; không coi OWN là BRANCH |
| Approved durable publisher | Không tìm thấy publisher/outbox contract hay implementation tương ứng trong source services/security/Program | Thiếu transaction/retry/deduplication integration path |
| Approved shared DI plan | Không có UC107 DI decision | Thiếu |
| Explicit implementation authorization YES | Không có contract decision chứa xác nhận này | Chưa xác nhận |

B-TASK-03 từng ghi SQL CHECK chỉ hỗ trợ TODO/IN_PROGRESS/BLOCKED/COMPLETED/CANCELLED. Đây là evidence lịch sử, không phải kiểm chứng SQL mới trong B-TASK-04. Không truy cập database để xác nhận lại.

## SRS acceptance matrix

Đã đọc SRS §11.6, GBR-005/010/129, state transition rows và SRS_CHANGE_NOTES.md. Department Head quản lý trong branch; không tự thu hẹp về một department hoặc mở cross-branch.

| Acceptance requirement | Implementation | Runtime validation |
|---|---|---|
| Server session, active account/permission, executor identity | BLOCKED | Not Run |
| Assignee ownership, branch/resource scope, wrong assignee denial | BLOCKED | Not Run |
| Assigned → Accepted, optimistic concurrency | BLOCKED | Not Run |
| Decline reason, Assigned → Pending Assignment, remove assignment | BLOCKED | Not Run |
| Atomic status/history/audit và rollback | BLOCKED | Not Run |
| Durable Department Head notification, read scope, retry/deduplication | BLOCKED | Not Run |
| Stale version, two concurrent requests, duplicate retry | BLOCKED | Not Run |
| Revoked/expired session, inactive account, missing permission, cross-branch | BLOCKED | Not Run |
| Related task assignment regressions | BLOCKED | Not Run |

## Implementation và source changelog

Không tạo endpoint/service/DTO hoặc placeholder publisher khi chưa có approved contracts. Không sửa Program.cs, DbContext, entities, SQL, RBAC, Authentication, Notification Publisher hay shared services. Không có production source để integrate.

Exact created files của tác vụ này:

- scripts/verification/CodexB/B-TASK-04/BTASK04_REPORT.md
- scripts/verification/CodexB/B-TASK-04/BTASK04_VALIDATION.json

Không có file sửa hoặc xóa. Generated build outputs không thuộc integration package.

## Codex A integration handoff

Blocker cần cung cấp: approved UC107 decision và exact ledger/reservation trong B hoặc artifact ở vị trí được phép đọc ngoài worktree A/DEV2. Decision phải định nghĩa state storage, concurrency token/precondition và duplicate retry semantics; active assignment/unassignment/history/reason validation; PermissionService task resource resolver; durable event persistence, transaction participation, recipient read scope, deduplication key và retry ownership; shared DI integration; explicit implementation authorization YES.

Exact DI proposal: chưa thể đưa ra registration C# hợp lệ khi chưa có approved service/interface/publisher types. Không tạo tên interface giả hoặc no-op để lấp dependency. Codex A sở hữu shared DI/resolver/publisher integration theo giới hạn tác vụ; B chỉ triển khai feature sau khi contract và dependencies được cung cấp hợp lệ.

Main integration regression checklist sau khi unblock: build/test integrated source; route/DTO/status/validation; server actor/session/account/permission recheck; OWN và branch isolation; concurrent accept/decline và reassignment; stale version/duplicate retry; audit failure rollback; durable intent atomicity; publisher failure sau commit giữ business operation; notification retry/deduplication/read scope; related task assignment regression. HTTPS/SQL chỉ chạy trên trial riêng được phê duyệt, không dùng HotelOpsDB hoặc Task82–86.

Expected UC weight: 2 theo snapshot lịch sử; points claimed: 0. IMPLEMENTED_IN_B: NO. INTEGRATED_MAIN: NO (không xác minh runtime chính). COMPLETED_VERIFIED: NO. Không cập nhật official ledger.

Validation thực thi được ghi chính xác trong BTASK04_VALIDATION.json. Build baseline không chứng minh UC107 hoạt động.
