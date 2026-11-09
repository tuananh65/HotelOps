# FE05-FAST-004 — IMPLEMENTED_PENDING_DEPENDENCIES

FE04_DEPENDENCY_STATUS: NOT_READY. Không phát AUTHORITATIVE_INSPECTION_HANDOFF_READY.

## Baseline và thay đổi thực tế

Worktree riêng `D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST004`, branch
`feature/dev1-fe05-conclusion-integration`, parent
`d87d1b9008699d4315e4427317fb273bafdf8d33`.
Fetch thành công; tại thời điểm kiểm tra `origin/develop` vẫn là
`bc0110e97ffcb1e32f907302cf16ee45bcc5bb0c`. Không merge/cherry-pick.
FAST003 và các worktree cũ được giữ nguyên. Manifest kèm theo ghi exact 9 source/test paths,
Added/Modified và SHA256 file trên disk, không bỏ sót untracked source.

- UC044: thêm `ApproveInspectionValidator`; tách nguyên common snapshot validation từ
  Reject vào `InspectionConclusionValidation`, tránh sao chép state/source/revision/criteria rules.
  Theo SRS UC044/GBR-062, mandatory phải PASS; optional chưa trả lời được phép, optional FAIL
  không tự trở thành mandatory FAIL nhưng vẫn phải có defect hợp lệ theo GBR-060.
  Validator không cấp quyền hoặc xác nhận trusted provenance.
- UC044 trusted preflight tái sử dụng chính `RejectReadSession` trong existing partial service:
  real session/branch INSPECTION_PERFORM, assigned inspector, persisted graph/evidence,
  retained referenced version kể cả Retired, SERIALIZABLE caller transaction, explicit cycle.
  Kiểm tra task Pending Inspection, separation-of-duties theo existing assignment writer,
  không xử lý Reinspection như normal approval. Không có Approve endpoint/writer.
- UC045/046: thêm các entry points preparation trong caller transaction, nối public service
  tới preparation; UC046 bổ sung real actor/session/permission recheck ở boundary này.
  `PrepareReworkInCallerTransactionAsync` là read preparation; production caller vẫn là
  BatchAsync, chịu trách nhiệm user/session → branch → catalog locks trước khi gọi. Không
  dùng preparation như standalone authorization cho mutation; nó không tạo assignee proof.
- Sửa lỗi integration có bằng chứng SQL: gate D4 FE05 dùng literal cũ
  `314DBD336CED88E2DCFDCF6762F8126BFA95D349247ACADA7294F83F19B1F7AD`, trong khi exact
  `04_EvidenceInvalidation.sql` tại baseline và marker do installer ghi có SHA256
  `E94CD891B0EDE6AD98384B91B35B6DC395E70E53D3CAD994854F4B7237C61CAC`.
  Chỉ sửa literal gate FE05. Không sửa bất kỳ SQL/trigger/EF mapping D4 nào.
- Public Reject/Rework giữ nguyên nonempty dependency gates và unconditional rollback
  throw sau SQL core nếu một thay đổi tương lai chỉ xóa gate. Không flags mới, không ledger,
  outbox, notification publisher, proof table hoặc Ready projection mới.

## Verification thực tế

| Check | Kết quả | Giới hạn |
|---|---|---|
| `dotnet build HotelOps.slnx --no-restore --nologo -p:UseSharedCompilation=false -m:1` | PASS; 0 warnings/errors | Backend build |
| WriteTests harness build | PASS; 0 warnings/errors | Không phải acceptance |
| LoaderTests console | 36/36 PASS | 19 affected existing + 17 UC044 mới; orchestration doubles/domain |
| DomainTests console | 45/45 PASS | Rerun vì common validation trực tiếp thay đổi |
| SQL `preparation` mode | 11/11 PASS | SQL/session/permission thật; explicit rollback từng ca |
| `dotnet test HotelOps.slnx --no-build --nologo` | exit 0 | Solution không chứa test project; không tính test credit |
| Public HTTP/live E2E | NOT_RUN task này | Không báo SQL preflight là live E2E |
| Durable replay/staging/real-authority/projection commit | NOT_RUN/BLOCKED | Thiếu accepted adapters |
| Concurrent multi-writer stress/deadlock freedom | NOT_RUN | Không suy ra từ SERIALIZABLE/rollback |

Database mới: `HotelOps_Canonical_20261109_020716_cc4522f5_Fresh`, local
`DESKTOP-JI8U50T\SQLEXPRESS`, SQL 16.0.1000.6. Owner UUID và source hashes ở environment receipt.
Instance/DB/owner được guard trước mutations, Windows integrated connection local LPC,
không đọc shared configuration connection string. Ban đầu 0 users; chỉ fixtures synthetic.
Không reset/drop database cũ; trial mới được giữ lại.

11 SQL cases bao gồm PASS/Retired version, mandatory FAIL, D4 stale revision,
missing current-cycle binding, revoked real SQL session, wrong assigned inspector,
foreign branch, denied permission, cleaner/inspector conflict, wrong task phase,
và Reject core → Rework preview trong cùng physical transaction với authority=false.
Mỗi ca thực hiện rollback và so sánh root/task/cycle snapshot, results, defects, attachments,
session/audit count; không tồn tại Rework sau ca test. Credential verification và tạo session
dùng service thật, cùng transaction rollback; không phải cookie/browser test.

Hai lỗi ở lượt đầu được ghi nhận, không che giấu: fixture đổi result khi Pending Conclusion
(51731) và mismatch D4 gate. Fixture được chuyển Inspecting → sửa results → Pending Conclusion
trong transaction; gate được sửa như trên. Chưa có case PASS ở các lượt lỗi. Sau sửa: 11/11 PASS.
Raw private runtime/credentials không đưa vào Git; receipts commit đã sanitize.

## Dependencies và hướng tích hợp theo exact source

| Dependency/owner | Source evidence | Phần chưa thể thực hiện |
|---|---|---|
| DEV2 staging/receipt | `Services/Notifications/ProvisionalCleaningNotification.cs`, `CandidateCommitGuard.cs` tại bc0110e | Candidate assignment-only Stage/Find/Verify, guard thực thi fs009 assignment proof. Không phải adapter InspectionFailed/ReworkRequested/InspectionPassed. Không gọi thay bằng event khác. Cần accepted signatures/event payload/recipient và caller physical transaction verification. |
| FE04 real proof | `Services/Housekeeping/ReworkLifecycleService.cs`, `scripts/database/release/fs016-candidate/01_Rework.sql` tại bc0110e | Digest reader dùng lại được nhưng proof origin CHECK chỉ SYNTHETIC_TEST_ONLY. Cần accepted real receipt/proof writer/reader, source revision/digest binding và replay contract; không sinh synthetic proof để thông qua. |
| FE04 lifecycle/projection | `CleaningLifecycleService.cs`, `CleaningClosureBoundary.cs`, fs013/fs016 candidate scripts tại bc0110e | Cần atomic lifecycle participant với current cycle, effective RoomBlock, readiness event và persisted room-status projection. Không tự map REWORK_REQUIRED vào room status không có trong CHECK hoặc tự cập nhật Ready. |
| UC044 commit | SRS UC044, GBR-059/062 và preparation code mới | Passed + task Completed + FE04 projection + audit + DEV2 staging/receipt cùng transaction chưa có. Preflight Success chỉ là source eligibility, không phải Passed/Ready. |

Lock order reader hiện tại giữ nguyên user/session → branch → catalog → inspection → task → cycle.
Chưa xác nhận tương thích mọi FE04 writer; child-first evidence writer và cycle-first lifecycle
writer vẫn có thể deadlock. Không thêm retry framework. Khi owner giao exact commit/contract:
review hẹp affected files, bind cùng physical transaction; verify durable replay trước stale-source
re-evaluation theo approved contract; stage facts/receipt/real proof/projection/audit rồi verify ngay
trước commit. Test rollback sau từng participant và ambiguous commit/replay trước khi bỏ fail-closed.
Không copy các candidate framework hoặc coi source mới chưa accepted là authority.

## Trạng thái bàn giao

UC044 domain/loader/preflight: IMPLEMENTED, focused VERIFIED; production writer NOT_IMPLEMENTED.
UC045/046 preparation và SQL primitives: có source, preflight/core transaction VERIFIED trong phạm vi
receipts; production mutations BLOCKED. Staging/durable receipt/real proof seams chưa thể bind
concrete adapter khi owner chưa giao contract tương thích. Không tuyên bố các seams đó COMPLETE.

Ước tính kỹ thuật: task FAST004 65%; UC044 45%, UC045 65%, UC046 60%.
Các tỷ lệ là ước tính, không phải ACCEPTED hoặc full-stack verification.
Không sửa FE04/DEV2/DEV3, shared authentication/router/Program.cs, generated entities,
SQL scripts, schema hoặc notification flags. Chỉ commit/push feature branch riêng sau kiểm tra
allowlist/secrets; không merge/deploy. Commit SHA và remote receipt được cung cấp ở báo cáo cuối.
