# FE05-FAST-005 — IMPLEMENTED_PENDING_VERIFICATION

**FE04_DEPENDENCY_STATUS: NOT_READY. Successful authoritative public mutation: NOT_RUN.**

## Source và implementation

Worktree mới `D:\SEP490\Projects\HotelOps-DEV1-FE05-FAST005`, branch
`feature/dev1-fe05-authority-wiring`, parent FAST004
`a5048a72670df18050992ed4ff7895cc07b6ee1e`. Fetch/pin R01 remote khớp exact
`857fe0ded7902bc67fe13fec9b0e029917c63f7d`.
Không merge/cherry-pick toàn branch. Ba dependency files R01 được import nguyên nội dung:
`ReworkAuthorityContract.cs`, `ReworkAuthorityReader.cs`, `InspectionRoomProjectionAdapter.cs`.
`dependency-pin.csv` ghi Git blob của từng file. Không sửa FE04 worktree hoặc definitions của owner.
Không tích hợp toàn bộ UC035/036 consumer changes/frontend từ R01 vào candidate này.

`InspectionAuthorityIntegration.cs` là phần FE05 mới:

- `PrepareReworkAuthorityInCallerTransactionAsync`: kiểm tra exact scoped connection/physical
  transaction, SERIALIZABLE/clean tracker/no ambient transaction; real session/branch permission
  qua existing preflight; selected-defect set phải khớp persisted Rework selection.
- Gọi trực tiếp `BuildReworkAuthoritySnapshotAsync` của R01, không tính lại canonical digest:
  persisted identity/version/rowversion/current cycle, selected results/defects, bridge associations,
  metadata và private-file byte hashes. Selected defects còn phải thuộc valid defects của chính Failed source; kiểm tra binding với command rồi recheck authorization.
- Chuẩn bị typed receipt đúng `FE05_REWORK_AUTHORITY_V1`, issuer `FE05.UC045.UC046` v1,
  origin `FE05_COMMITTED_V1`, actual `CURRENT_TRANSACTION_ID()`, quoted uppercase 16-hex ETag,
  uppercase 64-hex source/evidence digests, actor/operation/Rework/source identities và FAILED.
  Audit payload có operationId/authorityContract/sourceDigest đúng R01 consumer.
- `PrepareFailedConclusionReceiptInCallerTransactionAsync`: payload projection riêng
  `FE05_INSPECTION_CONCLUSION_V1`, issuer `FE05.UC044.UC045` v1, persisted FAILED binding
  và assigned-inspector check. Không dùng Rework receipt thay cho conclusion receipt.
- `ProjectFailedConclusionInCallerTransactionAsync`: chuyển chính connection/transaction tới
  FE04 adapter `ApplyAsync`, recheck authorization sau adapter; không commit. FE04 tự kiểm tra
  persisted durable receipt/module trust; không truyền boolean để tự xác nhận authority.

Prepared payloads chỉ là internal data, không phải HTTP DTO, durable receipt, issued proof
hoặc bằng chứng đã commit. Source preparation không ghi operation reservation/proof/audit.
Public Reject/Rework gates và rollback throws FAST004 giữ nguyên byte-for-byte. Chưa có
production coordinator ghép các participants. Không flags mới, notification/outbox/ledger mới,
SQL writer/module mới hoặc Ready/Reinspection lifecycle mới. UC044 preflight giữ nguyên.

## Verification thực tế

| Check | Result / giới hạn |
|---|---|
| Backend build | PASS, 0 warning/error |
| New payload/guard unit checks | 14/14 PASS; không phải SQL authority verification |
| New isolated SQL cases | 11/11 unique cases PASS qua các focused lượt; real credential/session/permission services; rollback từng ca |
| Concurrent case sau bổ sung second-connection identity guard | PASS ở lượt focused riêng; không đếm thành case mới |
| `dotnet test HotelOps.slnx --no-build --no-restore --nologo` | exit 0; solution chỉ API, không tính test credit |
| Persisted real proof/consumer acceptance/positive projection | NOT_RUN; chưa có trusted issuer module + FS016/R01 verified substrate |
| DEV2 staging, durable replay, authoritative public commit | NOT_RUN/BLOCKED; chưa có exact N01 package |
| Historical suites / public HTTP E2E | NOT_RERUN / NOT_RUN task này |

SQL trial mới: `HotelOps_Canonical_20261109_024026_abf4acd5_Fresh`, local
`DESKTOP-JI8U50T\SQLEXPRESS`, SQL 16.0.1000.6, owner
`8ff6bf6a-149e-4984-bad0-fb34c3a57673`. Instance/DB/owner được guard trước writes;
contender connection được guard riêng. Ban đầu 0 users, toàn bộ fixtures synthetic.
Không dùng shared connection string, không reset/drop bất kỳ trial cũ nào.
Trial có existing housekeeping+D4+FS013 substrate; không dựng schema giả để vượt R01 gates.

11 SQL cases: exact persisted receipt fields/transaction ID; wrong selected set; stale D4
revision; missing cycle; revoked session; permission denial; wrong physical connection;
conclusion payload và projection fail-closed thiếu schema; linked evidence đọc/hash file bytes
thật, missing file bị chặn và byte changes đổi cả evidence/source digest; concurrent Reject
và Rework contender bị 1222 khi nguồn đang locked. Ca bổ sung có positive control và thử đổi persisted selection sang defect của Inspection khác; existing SQL trigger từ chối 51731 trước khi có thể tạo graph không hợp lệ. Lượt focused rerun PASS và rollback/persistence check PASS.

Tất cả case rollback explicit và kiểm tra root/task/cycle snapshot, results/defects/attachments,
session/audit counts và không còn Rework. Không có proof hoặc successful authoritative commit.
Concurrent test là lock-contention/rollback verification; không tuyên bố one committed winner,
deadlock-free, same-key replay hoặc unknown-commit recovery. Trên database local, fixture/admin
connection không chứng minh least-privileged FE04 consumer có thể chấp nhận proof.

Sau khi 10/10 SQL đã PASS, bổ sung identity guard cho contender có lỗi harness CS0136
(tên biến trùng catch variable); đã sửa tên biến và chạy lại đúng case concurrency.
Lượt đầu của ca bổ sung dừng ở ownership trigger 51731 vì fixture không thể tạo cross-cycle link. Đã sửa assertion để kiểm tra chính rejection hiện hữu, không disable trigger hoặc fabricate graph. Không sửa production code để né lỗi SQL hoặc bypass authority gate.

## Exact remaining blockers

1. **DEV2 N01:** chưa được cung cấp exact source commit/package/signatures. Fetch lần cuối
   chưa thấy commit mang N01/Inspection staging được nhận diện. Cần caller-transaction stage,
   recipient/event facts, existing operation reservation completion/result JSON, fingerprint,
   replay/ambiguous commit verification và before-commit guard cho UC045/046. Không dùng
   assignment/rework-submitted event khác thay cho InspectionFailed/ReworkRequested.
2. **FE05 trusted SQL issuer:** R01 yêu cầu reviewed `WITH EXECUTE AS OWNER` domain module,
   SQL authorization/source checks, root/task/Rework writes và mandatory staging/proof/receipt
   trong cùng transaction. Module này **chưa triển khai**, không chỉ là deployment registration
   còn thiếu. Phần C# payload preparation mới không thay thế module. Cần N01 contract để hoàn
   thiện atomic SQL/staging boundary, sau đó pin module object ID/UTF-16 definition SHA256/source commit.
3. **FE04 SQL verification/deployment trust:** R01 report chưa SQL-verified. Cần approved
   disposable FS016+R01 substrate và least-privileged API/consumer identity, registry activation
   bởi owner, VIEW DEFINITION và DENY direct proof/registry DML. Candidate này không tự cấp trust.
   Cần kiểm thử persisted proof/receipt/audit/module hash, different-transaction read, projection
   với effective blocks, rollback sau từng participant và incompatible/missing sources.
4. **Operation identity:** UC045 và UC046 có hai receipt contracts/issuers khác nhau và cần
   operation binding riêng theo approved replay contract. Existing UC046 HTTP action trong
   `HousekeepingWorkflowController.cs` chưa truyền Idempotency-Key; không tự sinh key hoặc
   sửa transport trước khi N01 contract xác định binding/replay semantics.
5. **Evidence/concurrency:** R01 cần immutable private storage; SQL locks không khóa file bytes
   sau khi đọc. Giữ điều kiện này trước activation. Reader/child-first evidence/lifecycle lock
   ordering cần owner kiểm chứng cùng participants; contention test không giải quyết mọi deadlock.

Root/task/Rework/proof/audit/notification/projection atomic boundary đầy đủ chưa được wire.
Khi N01 và verified FE04 SQL substrate sẵn sàng: bind existing ledger, implement reviewed issuer,
stage conclusion receipt trước projection; stage Rework receipt/audit/proof theo R01 cùng transaction;
verify before commit; test rollback, persisted/different-transaction consumer read, concurrency,
replay và real HTTPS. Chỉ sau đó xem xét bỏ fail-closed production gates.

## Progress / integrity

Ước tính task FAST005 **65%**; UC044 **45%** (không đổi), UC045 **70%**, UC046 **70%**.
Đây là ước tính implementation, không phải UC ACCEPTED hoặc successful production-path commit.
Exact files/ownership/hash ở source-manifest; receipts đã sanitize, không có private runtime,
password, cert/key, connection string chứa secret hoặc database files. Không sửa SQL/schema,
Program.cs, shared auth/router/EF mappings, notification flags hoặc logic FE04/DEV2/DEV3.
Chỉ commit/push feature branch riêng sau scope checks; không force-push/merge/deploy.
