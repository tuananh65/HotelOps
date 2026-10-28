# Exact diff proposal — KHÔNG ÁP DỤNG TRONG TASK25

## Preferred CAS slice (chỉ nếu approved amendment GBR005)

### File allowlist khi task implementation được duyệt

| File | Exact intent |
|---|---|
| NEW DTOs/Users/UserProfilePatchRequest.cs | Presence-aware parsing/validation3 fields+expectedProfile; strict unknown/duplicate keys |
| NEW DTOs/Users/UserProfileSnapshotDto.cs | {Id,FullName,Email,Phone}; no version claim |
| NEW Controllers/UserProfileController.cs | [ApiController],[Route("api/users")],[Authorize(Global:USER_MANAGE)],[ResponseCache(NoStore=true)];[HttpPatch("{userId}")];existing CSRF applies |
| NEW Services/Users/IUserProfileService.cs | UpdateAsync(long targetId,UserProfilePatchRequest,CancellationToken), typed outcomes; không alter directory interface |
| NEW Services/Users/UserProfileService.cs | Same scoped DbContext explicit transaction, locked baseline comparison, whitelist update/audit |
| Program.cs | Chính xác1 scoped registration IUserProfileService→UserProfileService; shared DI approval bắt buộc; không reorder/config/middleware changes |
| NEW verification/UserProfileUpdate/ | Parser/domain/SQL fault-injection+HTTP tests trên trial mới; no seed edits |

Paths relative src/HotelOps.Api trừ verification. Interface proposal:
```csharp
Task<UserProfileUpdateResult> UpdateAsync(long userId,
    UserProfilePatchRequest request, CancellationToken cancellationToken);
```
Outcome enum candidate:Updated,Unchanged,NotFound,Forbidden,ProfileChanged,EmailInUse,StateChanged,ValidationFailed; không raw exceptions vào DTO. Input DTO không dùng nullable properties đơn giản để biểu diễn omitted vs clear; validate JsonElement/Object hoặc local presence-aware converter, không thay global JSON options.

### Transaction algorithm chính xác

1. Framework validates cookie/session/CSRF và Global USER_MANAGE trước target read. Bind/validate body/ID; derive actor từ CurrentUserAccessor, không body.
2. Begin explicit SQL ReadCommitted transaction trên scoped context. Recheck Global USER_MANAGE bằng foundation trong transaction; không generic admin role check.
3. Read **chỉ** user_id,full_name,email,phone,account_status,deleted_at với UPDLOCK,HOLDLOCK theo userId (typed scalar read model, parameterized SQL; không SELECT* User/credential). Missing/deleted→NotFound, dispose rollback.
4. Compare expectedProfile exact Ordinal/null-aware với locked current profile. Mismatch→ProfileChanged; không query duplicate trước check này.
5. Apply only present normalized fields. Same values→Unchanged; không mutation/audit; dispose transaction.
6. Precheck email collision nếu email thực sự đổi, trên toàn users trừ target. Parameterized query; bất kỳ conflict trả generic EmailInUse, không personnel details.
7. Conditional parameterized UPDATE chỉ full_name/email/phone/updated_at WHERE user_id=@id AND deleted_at IS NULL AND account_status=@lockedStatus. Target UPDLOCK giữ tới commit, ngăn mọi SQL writer cần X lock thay row giữa read/write. Require affected=1; không SaveChanges stale full User; không overwrite auth columns.
8. Create AuditLog {ActorUserId=verifiedActorId,EntityType="users",EntityId=invariantId,Action="UPDATE",OccurredAt=utcNow,OldValues,NewValues,ChangedFields}. Old/New chỉ actual changed keys trong3 profile fields, ChangedFields deterministic order; BranchId=current primary branch nếu lựa chọn metadata đó được duyệt, không client input. Minimal proposal BranchId=null để không invent authority.
9. Save audit trong cùng transaction. Commit thành công mới return snapshot200. Audit failure rollback profile. Exception/cancel→dispose/rollback nếu chưa commit. Unknown commit→generic error/reconcile, không tự retry patch/audit.

Permission recheck là minimum nhưng không khóa toàn actor role/grant dependencies; nếu yêu cầu revoke-vs-commit serialization tuyệt đối, cần shared writer lock protocol chưa được giao. Không tự mở rộng task sang sửa UserRole/ROLE_MANAGE writers.

Statement DI dự kiến duy nhất, chưa áp dụng:
```csharp
builder.Services.AddScoped<HotelOps.Api.Services.Users.IUserProfileService,
    HotelOps.Api.Services.Users.UserProfileService>();
```

### CAS guarantee / limitation

SQL lock ngăn write interleaving lúc transaction thực thi; expectedProfile phát hiện stale3-field values. Không monotonic version, không phát hiện ABA/A→B→A, không stale role/membership/credential aggregate. Body có thể giả expectedProfile; nó không được dùng như permission proof. Không label là full row_version GBR005. Cần business amendment cho profile slice trước implementation.

### Duplicate error translation

Source unique email constraint/index:UQ__users__AB6E61641B43B5D5. DB numbers2601/2627 có thể là bất kỳ unique index, **không tự tất cả EMAIL_IN_USE**.

Recommended implementation: tại phase profile-only UPDATE users (không insert user/username/employeeCode), nếu2601/2627 thì rollback transaction trước, phân loại constraint/index đúng email nếu có reliable diagnostic; nếu không thì dùng fresh safe existence SELECT sau rollback để xác nhận email collision. Audit INSERT unique failure ở phase audit không bị catch như email conflict. Không return SqlException.Message hoặc parse để lộ PII. Nếu không xác minh đúng collision→generic failure. Precheck race được DB backstop và409 sau rollback, không “check-then-write” là đủ. Metadata/driver diagnostic classifier cần tests thực SQL; không đoán constraint của database khác.

## Alternative strict rowversion — chỉ sau schema approval

Nếu không chấp nhận CAS limitation, candidate release SQL (text proposal, chưa executable file):
```sql
ALTER TABLE dbo.users ADD row_version rowversion NOT NULL;
```
Cần verify column không có/conflicting kiểu trước release, chỉ isolated trial trước; không tự chạy trên HotelOpsDB, không EF migration. DB-first User bổ sung byte[] RowVersion; mapping:
```csharp
entity.Property(e => e.RowVersion).IsRowVersion().HasColumnName("row_version");
```
PATCH dùng expectedVersion (Base64 đúng8 bytes), không expectedProfile; conditional UPDATE theo token,0 rows sau existence check→409 PROFILE_CHANGED. OUTPUT inserted.row_version rồi audit+commit trước response. New version lấy trực tiếp UPDATE hoặc refresh projection, không trả stale token. SQL UPDATE bất kỳ users columns làm token đổi, kể cả auth lockout/login; có thể tạo profile conflicts do auth activity, cần chấp nhận rõ.

Read-token path cũng phải được duyệt: bổ sung version vào detail DTO/SELECT sẽ thay contract Task22; hoặc manager-only read snapshot endpoint riêng (tăng scope). Không publish expectedVersion API mà client không có approved cách đọc token. Shared DB-first mappings/SQL release/regeneration docs phải review với DEV2. User rowversion không version hóa RolePermission/UserRole aggregate, ngoài profile scope.

Không tự thêm column/release script/DTO/mapping trong Task25. Hai alternatives loại trừ nhau trong first implementation; cần chọn trước code, không ship cả hai một cách mơ hồ.
