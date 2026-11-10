# [HotelOps FE05] FAST012 — PARTIALLY_VERIFIED / PENDING_APPROVALS

Ngày: 2026-11-10. Worktree: HotelOps-DEV1-FE05-FAST012. Branch: feature/dev1-fe05-approve-reinspection. Parent: 53531b71e41b1796cc2c05b6e91114307c41cc46. Không sửa worktree FAST011 hoặc INSP; không merge, deploy, shared SQL, activation hay force-push.

## Source thực tế

- InspectionApproveDomainWriter.cs: thêm internal UC044 domain step, chỉ nhận request identity/ETag và session; reuse trusted preflight/session/branch/assigned inspector/SOD/current-cycle/checklist validation. SQL conditional rowversion update chuyển Inspection PASSED/COMPLETED/PASS và task COMPLETED; audit cùng physical SERIALIZABLE caller transaction. Không commit, không Room READY/projection thay thế, không endpoint. Result luôn AuthoritativeCommitAccepted=false, không phải durable receipt.
- InspectionTrustedIssuer.cs: chỉ thêm canonical UC044 request identity bằng encoder SHA256 hiện hữu; không thay đổi Reject/Rework hoặc SQL EXECUTE AS OWNER module/hash/registration.
- ReinspectionDecisionValidator.cs: domain-only UC047 Fixed/Not Fixed cho exact persisted defect set; đủ mọi defect, không duplicate/foreign, hai revision phải khớp. Có thể hoàn tất decision với AllFixed=false; incomplete không được giả thành PASS. Không lưu enum/status mới hoặc cấp authorization/provenance.
- ReinspectionSourceCandidate.cs: internal caller-transaction reader dùng existing authorization/persisted reader, exact assigned inspector, task/cycle, root/Rework ETags, trạng thái Pending Reinspection, SOD và remediation. Giữ cả assigned và original checklist version, không chọn latest hoặc thay version Retired. Không đọc private evidence bytes. Schema check fail-closed trước source query. Cùng task KHÔNG được coi là persisted reinspection/Rework binding: AuthoritativeBindingAccepted luôn false, không endpoint/UI activation.
- ApproveReinspectionChecks.cs / ApproveWriterSqlChecks.cs / test Program.cs: focused domain/gate, real session/SQL rollback checks và runnable UC047 reader cases khi schema tồn tại. Không sửa shared auth/router, generated entity, DbContext, D4, R05, Users prelock, notification hoặc unknown-commit recovery.

## Verification thực tế

| Check | Kết quả | Giới hạn |
|---|---|---|
| Focused domain/gate console checks | 33/33 PASS | UC047 defect completeness/revisions, UC044 identity, gates; không SQL acceptance |
| UC044 actual SQL component | 10/10 PASS | Real credential/session/permission, branch/SOD/cycle, retired version, stale/repeat rejection, domain/task/audit và explicit rollback |
| UC047 missing-schema negative SQL | 1/1 PASS | Real authorized session; fail-closed và rollback trước fixture/source mutation |
| UC047 positive reader + stale Rework/wrong branch/cycle/incomplete remediation | NOT_RUN (5 ca) | Owned FAST010 lane thiếu remediation_note/remediated_by/remediated_at; không cài schema để ép PASS |
| Concurrent authoritative Approve | NOT_RUN | Chưa có composed approved writer/runtime; repeat-with-original-tag test không được gọi concurrency test |
| UC044 durable replay/result/notification/projection full commit | NOT_RUN | Domain component result không phải committed issuer receipt |
| UC045/046 successful authoritative mutation, N01 14-case, proof/projection/different-transaction consumer | NOT_RUN | Existing mandatory gates chưa accepted |

SQL component sử dụng lại database synthetic local đã được cấp phép: HotelOps_Canonical_20261110_031050_3a074f28_Fresh, server DESKTOP-JI8U50T\SQLEXPRESS, owner UUID 2f2502b8-87cc-4185-a56a-2fcae4000c78. Existing harness xác minh server/database/owner trước mutation. Tất cả business/session/fixture writes trong các ca mới đều explicit rollback; so sánh Inspection/task/cycle/results/defects/attachments/audit/session và không còn Rework/new Inspection. Không tạo database/lane mới, không cài DDL hoặc kết nối shared DB. Đây KHÔNG phải N01 SQL acceptance lane.

Run UC047 đầu tiên FAIL do invalid column trên fixture remediation. Giữ receipt lỗi riêng; sửa source reader và harness để kiểm tra schema, chạy lại đúng phần bị ảnh hưởng và nhận missing-schema negative PASS. Không che lỗi ban đầu, không gọi positive SQL PASS. Build lần đầu cũng phát hiện helper trùng tên; đã bỏ helper duplicate và dùng helper hiện hữu.

## UC044: blocker cụ thể

Authoritative Approve composition CHƯA hoàn tất. Existing Fe05NotificationKind chỉ có InspectionFailed/CleaningReworkRequested; không invent passed event, recipient graph hay durable UC044 acceptance. Cần accepted Passed staging/result/replay binding và existing FE04 projection schema/registered receipt issuer trước guarded commit. Original adapter đã có Passed branch, nhưng READINESS_OVERLAY_V1 marker/READY schema acceptance vẫn cần; không tự chuyển READY hoặc bỏ Blocked precedence. Kiểm tra SQL adapter còn điều kiện RequiredCriteriaPassed loại bất kỳ FAIL, trong khi SRS UC044 và validator chỉ yêu cầu mandatory PASS (optional FAIL hợp lệ kèm defect): không âm thầm sửa FE04-owned adapter/policy.

## UC047: blocker chính xác

Repo SRS docs/requirements/srs_sections/02_use_case_specifications.md section 5.9 (UC047, dòng 4079–4140): mỗi defect Fixed/Not Fixed, remediation notes/photos; all fixed => Rework Completed; not fixed => Rework Failed và UC046 round tiếp theo. Không nhận được bản UC v3 khác để tự resolve mismatch.

Existing UC041 HousekeepingAssignmentsService tạo Inspection với cleaning_task_id và version được chọn; RoomInspection không có exact ReworkId FK. cleaning_rework_defects có remediation, chưa có kết quả reinspector Fixed/Not Fixed. release/fs016-candidate/01_Rework.sql CK không có FAILED Rework workflow/status. Chưa có accepted checklist-version policy cho lần reinspection. Vì vậy chưa thể persist hay mở UC047 API/UI đúng SRS mà không invent binding/schema/lifecycle. Candidate reader không biến explicit client IDs hoặc cùng task thành authority. Positive reader SQL cần remediation schema được cài qua lane/owner đã phê duyệt, không sửa shared schema trong task này.

## UC045/046 owner driver và source reuse

Giữ nguyên FAST011 Reserve/Stage/Verify, canonical identities, trusted issuer, R03/R05 proof/snapshot/projection, retained Users-first roster và recovery. OWNER_14_CASE_DRIVER.md liệt kê exact owner calls và assertions O01–O14. Đây mới là binding/scenario plan; executable authoritative fixture/fault-injection binding vẫn BLOCKED, không tuyên bố driver READY hoặc 14-case PASS. Cần accepted lane/schema/real receipt/issuer/recipient/storage để hoàn thiện và chạy đúng owner fixtures, không thay bằng permissive test double hoặc local unapproved database. Không giao task mới cho DEV khác.

## Permission proposal nguyên văn

PERMISSION_PROPOSAL.md được copy BYTE-FOR-BYTE từ FAST011, không sửa proposed rule, grant semantics hoặc approvals. Ba candidate: responsible supervisor/INSPECTION_PERFORM/exact persisted responsibility; original cleaner/CLEANING_VIEW/exact R05 attribution; assignee/CLEANING_VIEW/exact Rework/current cycle. Các summary field allowlist và OWN semantics giữ nguyên trong tài liệu nguyên văn. acceptedConcreteBinding=null cho cả ba. Không cấp broad grant, INSPECTION_VIEW hoặc private evidence access.

## Evidence storage

Reuse version/content-digest/private-path/create-only/read-lease candidate FAST011 và các receipts trước đó; không chạy lại unchanged historical file tests. Durability acceptance vẫn false. Digest reread hoặc D4 rowversion không chứng minh WORM/ACL, stable namespace, retention/no-replace-delete guarantee sau release lease hay crash/unknown-commit recovery across SQL/filesystem. Chưa activate upload/proof candidate, không promote legacy GUID files. Cần approved namespace/runtime identity/retention/ACL contract; không bypass.

## Progress và FE04

Ước tính kỹ thuật, không phải acceptance: FAST012 65%; UC041 70% (unchanged), UC044 65% (domain writer + SQL rollback component), UC045 80%, UC046 80% (unchanged), UC047 25% (domain/source candidate, chưa có authorized persisted reinspection writer/API/UI). Phần còn lại gồm FE05 executable owner-fixture binding và accepted lifecycle/Passed-staging/storage/permission/runtime/schema decisions; không gọi tất cả là việc của owner khác.

FE04_DEPENDENCY_STATUS: R03/R05_PINNED_R05_INTEGRATED_VERIFIED_ISOLATED / NOT_READY_FOR_AUTHORITATIVE_HANDOFF. Pins giữ nguyên: R05 95f7422e284bc81bcfcf9f460dace52022de5cc7; DEV2 d60022b487b5b59e84d6551a0ee5162c9fd32164. Không tích hợp lại hoặc yêu cầu làm lại R05/UC029. Không phát AUTHORITATIVE_INSPECTION_HANDOFF_READY hoặc UC ACCEPTED.

Build/commit receipts và exact source hashes xem source-manifest.csv cùng receipts/. Không đưa credentials, runtime.private.json, connection strings có secrets, database files hoặc raw TestResults vào commit.
