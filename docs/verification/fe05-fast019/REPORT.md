# FE05-FAST019 — CONTRACT_PROPOSAL_READY / AUTHORITATIVE_BLOCKED

Ngày 2026-11-11 (Asia/Saigon). Branch `feature/dev1-fe05-contract-blockers`, parent chính xác `940b26b06f4bd19c457cdfea71a81a6d2c59ae19`. Commit bàn giao là commit chứa REPORT/manifest này; hash được trả trong kết quả Git/final, không ghi self-referential hash vào manifest. Worktree riêng `D:/SEP490/Projects/HotelOps-DEV1-FE05/obj/fe05-fast019`. Không thay FAST018 hoặc workspace đang có thay đổi của developer.

## Kết quả thực hiện

- FE05-owned implementation: nullable operation UUID candidate trong DTO/preflight; frontend tạo UUID khi Review và truyền qua body cùng task/cycle/checklist/selected inspector/supervisor. Giữ UUID cho cùng review, chặn empty/malformed UUID, không retry mutation tự động, không giả InspectionId. Client cũ chỉ preflight vẫn tương thích. Candidate controller vẫn NonController và public mutation fail-closed.
- Một tài liệu duy nhất cho DEV2 duyệt contract: [DEV2_G04_HANDOFF.md](DEV2_G04_HANDOFF.md). Bao gồm G01 exact identity/resource binding/fingerprint proposal/sequence/replay/races/reassignment, G05 exact interface/transaction/proof/receipt dependencies, G02/G03 event-specific read policy + allowlist, G04 event/schema/issuer decisions và exact source pins.
- Source-only: pre-CREATE resource binding và canonical UC041 encoding; Assigned/Passed event registration/profile/source-read/issuer/schema; concrete G05 adapter. Không implement mới scheme/ledger, không thay FS013/N01 graph hoặc ClosedInspectionApproveBinding. acceptedConcreteBinding vẫn null, private evidence=false.
- G01 không thể dùng nguyên Reserve hiện tại: nó đòi persisted InspectionId trước CREATE. N01 candidate SQL còn giới hạn event/source CHECK, nên thêm transport/enum không đủ. DEV2/FS013 cần duyệt pre-create tuple và actual root completion binding.
- G05 reuse composition có sẵn: PASS domain → Passed staging → trusted durable completion → original FE04 projection → final Verify → caller commit. Stage không thể đòi completed receipt/READY trước completion/projection. Chưa có approved Passed profile, issuer hoặc concrete accepted binding.

## Files tạo/sửa

Sửa 7 file:

1. `src/HotelOps.Api/DTOs/Housekeeping/InspectionAssignmentCandidateDtos.cs`
2. `src/HotelOps.Api/Services/Housekeeping/InspectionAssignmentDomainComponent.cs` — chỉ guard empty operation UUID trong preflight; domain writer giữ nguyên.
3. `frontend/src/features/inspectionAssignment/client.ts`
4. `frontend/src/features/inspectionAssignment/InspectionAssignmentCandidate.tsx`
5. `frontend/tests/inspection-assignment.test.mjs`
6. `frontend/tests/inspection-assignment.spec.ts`
7. `tests/HotelOps.Inspection.WriteTests/Program.cs` — thêm mode NoSQL trước fixture dispatch.

Tạo `tests/HotelOps.Inspection.WriteTests/InspectionContractBlockerChecks.cs` và toàn bộ tài liệu/logs/pins/manifest trong `docs/verification/fe05-fast019/`. Danh sách đầy đủ và SHA256 từng file nằm trong `manifest.json` (hash physical working-tree bytes, manifest tự loại trừ chính nó; Git có thể normalize CRLF). Không xóa file. Không đổi source DEV2/FE04, shared DI/config/EF mappings/entities, permission records, SQL DDL hoặc UC047.

## Verification thực chạy

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| `dotnet build HotelOps.slnx` | PASS, 0 errors, 2 NU1900 warnings | Vulnerability feed NuGet không truy cập được |
| `dotnet test HotelOps.slnx --no-restore` | PASS command, exit 0 | Solution chỉ có API, không có assertion/test project; không tính đây là feature tests |
| `dotnet run --project tests/HotelOps.Inspection.WriteTests -- fast019-unit` | PASS 17/17 | Contract/refusal/cancellation/closed policy; không SQL |
| `node --test tests/inspection-assignment.test.mjs` | PASS 18/18 | Typed transport, same-key explicit request/lost response, no auto retry; controlled fetch, không durable replay |
| `dotnet run --project tests/HotelOps.Notification.N01.NoSql --` | PASS 145 checks | Existing mapping/graph/replay/interceptor regression, memory graph + stub transaction, 0 SQL connections; không event mới hoặc SQL concurrency acceptance |
| `npm run build` | PASS | TypeScript + shared Vite entry; existing react-router use-client warnings |
| `npx vite build --config vite.fe05.config.ts` | PASS | Separate FE05 development entry |
| `npx playwright test --config playwright.fe05-fast017.config.ts` | PASS 5/5 | Browser UI UUID/503/412/unknown outcome/401/403; intercepted HTTP, không real HTTPS/backend auth acceptance |
| `git diff --check`, owner-source diff review | PASS | Chỉ scoped files; có LF→CRLF Git notices, không whitespace errors |
| New successful HTTPS/API mutation | NOT_RUN | Runtime/public writer chưa approved, không bật để test |
| SQL/database/N01 L1/L2 | NOT_RUN | Không kết nối shared hoặc isolated SQL; runtime chưa approved |
| Authoritative Assigned/Passed replay/concurrency/rollback/FE04 consumer | NOT_RUN | Contract/event/issuer/schema/read acceptance chưa có; exact cases trong handoff §6 |

Lần chạy N01 đầu dùng `--no-restore` bị NETSDK1004 vì worktree mới chưa có assets. Giữ log thất bại `n01-nosql-initial-no-restore.log`; rerun có restore thành công 145 checks. Không sửa source để né lỗi. NU1900 cũng xuất hiện khi build harness; không phải C# compile warning. npm ci báo deprecations lodash.isequal/rimraf/glob/fstream/uuid và blocked install scripts core-js/esbuild; dependency source/lockfile không đổi và builds vẫn PASS. Playwright có FORCE_COLOR/NO_COLOR warnings. PowerShell ghi npm notice vào NativeCommandError wrapper trong raw logs, trong khi tool chạy kết thúc exit 0 và build/test có summary PASS. Browser teardown chậm; đã dừng đúng Vite process của run này sau khi 5 case hoàn tất; runner kết thúc `5 passed (3.4m)`. Không ghi nhận browser này là live API evidence.

## Tiến độ và approvals còn mở

| Hạng mục | Tiến độ thực tế |
|---|---|
| G01 | Ambiguity đã chuyển thành exact source-only proposal; UUID candidate có source/tests. Chưa accepted/unblocked: DEV2/FS013 pre-CREATE binding + canonical encoding/replay/schema; PL supervisor/reassignment contract |
| G02/G03 | Proposed event-specific inspector/PASSED-cleaner policies + allowlist; chưa PL/owner approved; acceptedConcreteBinding=null |
| G04 | Single DEV2 handoff đã chuẩn bị với source blobs/pins. Event/catalog/schema/graph integration decisions còn pending |
| G05 | Exact seam/dependency/ordering/R05/projection proof đã xác định, closed-binding tests PASS. DEV2/FS013 issuer/event/receipt/staging, PL policy và FE04 runtime prerequisites còn pending |
| UC041 | BLOCKED cho authoritative assignment; transport preparation tăng, chưa có notification-complete commit |
| UC044 | BLOCKED cho authoritative Approve; không activation hoặc production binding mới |
| FAST019 | Hoàn tất phần FE05 preparation được phép + handoff/verification. PARTIALLY_COMPLETED ở mức unblock liên owner; không đánh dấu các approval pending là PASS |

`SOURCE_CONTRACT_READY=false`. G06 pending theo baseline, không tự đóng. Required owner approvals là action items trong handoff, không yêu cầu developer duyệt lại công việc chuẩn bị đã được giao. N01 L1/L2, accepted SQL graph và independent FE04 proof consumer vẫn NOT_RUN; candidate/refusal/rollback tests không thay acceptance.

`FE04_DEPENDENCY_STATUS=R05_PINNED_UNCHANGED_SOURCE_REUSED; RUNTIME_PROOF_PROJECTION_NOT_RUN; NOT_READY_FOR_AUTHORITATIVE_HANDOFF`. Không cần viết lại R05; phải giữ exact attribution/current cycle và original FE04 adapter, schema/issuer/proof acceptance. Không shared SQL, merge/deploy/force-push hoặc bật producer/worker.
