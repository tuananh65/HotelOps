# Transaction and concurrency readiness

## Account atomicity

EF/SQL có thể insert User + UserRole + UserBranchAccess + AuditLog trong cùng một explicit transaction. Nhiều SaveChanges trong cùng transaction không đồng nghĩa nhiều commit. Tuy nhiên orchestration UC004 hiện chưa tồn tại.

TemporaryCredentialService.ApplyAsync:
- Cấm existing/ambient transaction, yêu cầu SQL Server và tracker hoàn toàn trống.
- Consume prepared secret trước begin; tự mở ReadCommitted transaction, lock User UPDLOCK/HOLDLOCK.
- Update hash/temp-expiry/lockout; revoke sessions; thêm audit; SaveChanges/Commit rồi mới trả IssuedTemporaryCredential.
- Chưa kiểm quyền actor/target, delegation hoặc stale-client token; interface yêu cầu caller chịu trách nhiệm.
- InitialProvisioning cần target đã tồn tại, chưa có hash/temp-expiry.

Vì vậy insert User rồi commit để gọi ApplyAsync là **hai transactions**, để lại account/roles không hoàn chỉnh nếu credential fail. Wrap ApplyAsync bằng transaction ngoài cũng bị reject. Không gọi primitive qua context khác rồi coi là distributed atomicity.

SessionRevocationService khác: yêu cầu caller transaction, UTC, SQL locking isolation; từ chối pending changes hoặc tracked User/UserSession. Chỉ SaveChanges xong nhưng User vẫn tracked cũng chưa đủ. Mutation workflow phải thiết kế dedicated context, flush đúng thứ tự rồi detach các entity do workflow sở hữu; không Clear tracker của context shared tùy tiện. Raw conditional updates/no-tracking có thể giữ guard này nguyên.

## Failure boundaries

| Failure | Guarantee hiện có / việc còn cần |
|---|---|
| Validation/hash trước transaction | Chưa có write; prepared secret dùng một lần, không log |
| User/assignment insert trong transaction chưa commit | Có thể rollback hết; identity gap sau rollback là bình thường, không reseed |
| Credential/revoke/audit fail trong ApplyAsync | Source dispose transaction rollback phần primitive; không rollback User đã commit trước đó |
| Full provisioning fail sau extraction proposed | Phải rollback account/assignments/hash/revokes/audit cùng transaction; chưa runtime verified |
| Commit thành công, response mất | DB/HTTP không atomic; không lấy lại plaintext từ DB, cần explicit reset/recovery |
| Commit exception/outcome unknown | Không trả success/secret; không retry hoặc compensate mù, cần reconcile bằng operation identity đã duyệt |
| Audit insert/FK actor lỗi | Fail operation trong cùng transaction, không swallow audit failure |
| Deadlock/timeout/cancellation | Rollback transaction nếu chưa commit; classify, không biến thành authorization allow hay retry secret delivery |

## Các lựa chọn transaction

A. Giữ hai bước commits: ít diff nhưng không đáp ứng007/UC004; không đề xuất.
B. Extract transaction participant, giữ existing wrapper behavior: **đề xuất**, không schema; cần approval shared Authentication.
C. Copy credential SQL vào user admin service: tránh interface diff nhưng tạo hai implementations reset/hash/revoke, dễ drift; không đề xuất.
D. Ambient/distributed transaction: existing guards reject, thêm complexity không cần thiết; không đề xuất.

### Diff proposal có điều kiện — CHƯA ÁP DỤNG

Files dự kiến: TemporaryCredentialService.cs/ITemporaryCredentialService.cs hoặc một internal participant interface; user provisioning service mới trong project hiện tại; tests. Program.cs chỉ nếu DI thật sự cần, xin duyệt riêng.

Conceptual interface addition (tên chưa phải approved API):
```csharp
// Caller owns transaction; never commits or delivers plaintext here.
Task<PendingTemporaryCredential> ApplyInTransactionAsync(
    PreparedTemporaryCredential prepared, long actorUserId,
    CredentialProvisioningOperation operation, CancellationToken cancellationToken);
```

- Tách core lock/update/revoke/audit ra participant dùng đúng scoped context/current transaction; không bỏ guards.
- ApplyAsync cũ vẫn refuse ambient/current transaction, tự begin, gọi core, commit rồi phát IssuedTemporaryCredential như hiện nay.
- Participant trả internal nonserializable pending material; chỉ outer orchestration sau confirmed commit được one-time handoff. Không dùng IssuedTemporaryCredential trước commit hoặc expose pending secret từ service DTO.
- Authorization/stale state/identity verification tại orchestrator không phải chỉ tin actorUserId argument.
- Không đổi hash algorithm,24h validity, session lock ordering, forced-change/revoke behavior.
- Proposal cần compile/API compatibility và rollback/concurrency tests trước accept; chưa sửa source trong Task24.

## Concurrency: bốn vấn đề khác nhau

1. DB write serialization: user UPDLOCK/HOLDLOCK hiện có cho auth writers.
2. Stale client/lost update: GBR005 cần loaded version. User không rowversion/EF concurrency token; auth SQL locks không phát hiện form cũ.
3. Multi-user invariants: hai admin cùng remove admin role cần common invariant lock, không chỉ target locks.
4. Cross-module open-work race: check count rồi concurrent assign vẫn có thể orphan responsibilities.

### Lựa chọn không đổi schema

Đề xuất **chỉ cho profile slice sau business approval**: opaque server-authenticated baseline token gắn target+operation+canonical original fullName/email/phone và security-relevant status/deleted state; compare dưới target lock, conditional UPDATE/affected=1; audit same transaction. Token không chứa hash/session secret, không cho client tự sửa baseline/target. Signing key/version/expiry semantics phải thiết kế được duyệt, không tự thêm infrastructure.

Đơn giản hơn: client gửi original values để compare; phát hiện stale trung thực nhưng không chứng minh client đã đọc snapshot, không thay authorization. Không đề xuất coi đây là trustworthy version.

Giới hạn quan trọng: value/snapshot token có **ABA** (A→B→A), không chứng minh mọi intervening mutation; không tương đương monotonic row_version GBR005. Profile token không bảo vệ role/membership edits. Cần approved amendment cho slice hoặc schema/aggregate version quyết định riêng. Không dùng UpdatedAt như version vì null/precision và mọi writers không cập nhật thống nhất.

### Schema option, chưa được phép

SQL rowversion trên User + DB-first regen/token DTO giúp stale updates trên User, nhưng role/membership writers vẫn phải bump/check aggregate User version; User rowversion đơn lẻ không phát hiện UserRole/RolePermission thay đổi. Cần approved schema/model/API changes và migration/release plan riêng. Task24 không tạo EF migration.

### Lock protocol đề xuất cho mutations sau này

- Revalidate actor quyền và target scope trong transaction, bảo vệ grant dependencies cần thiết; pre-request permission check chỉ là gate đầu.
- Mutations admin invariant có common transaction-owned SQL application lock hoặc equivalent serializable invariant protection; acquire theo một order chung trước deterministic user locks, rồi sessions. Không dùng in-process lock cho nhiều API instances.
- Chốt protocol với writers liên quan trước áp dụng; extra actor/user/global locks có thể tạo deadlock với user-first auth writers nếu order không được review.
- Open-work predicate cần cùng ownership lock protocol với assignment writers DEV1/DEV2 hoặc isolation/range locks bao phủ đúng predicates; một read-only guard chưa bảo đảm chống race.
- Recheck expiry/time-sensitive grants gần commit; scheduled expiry vẫn có thể làm mất last admin sau commit, cần định nghĩa continuous invariant.
- Không retry bừa prepared credential/unknown commit. Race tests thực SQL bắt buộc; source analysis chỉ chứng minh design gaps.
