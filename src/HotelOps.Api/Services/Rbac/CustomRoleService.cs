using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rbac;
using HotelOps.Api.Entities;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Rbac;
public sealed record CustomRoleResult(int Status,string? Code=null,CustomRoleCreatedDto? Value=null);
/** Inactive HTTP candidate. No assignments/grants and no metadata PATCH until durable version policy. */
public sealed class CustomRoleService(HotelOpsDbContext db,ICurrentUserAccessor current,IPermissionService permissions,TimeProvider clock,IHttpContextAccessor http)
{
 public static Role NewRole(CustomRoleCreateRequest request,DateTime now)=>new(){RoleCode=request.RoleCode,RoleName=request.RoleName,Description=request.Description,IsSystemRole=false,IsActive=true,CreatedAt=now};
 private int Identity(out CurrentUserInfo actor){if(!current.TryGetCurrentUser(out actor)||!HotelOpsCookieTicket.TryRead(http.HttpContext?.User,out var ticket,out var sid)||ticket!.UserId!=actor.UserId||sid!=actor.SessionId)return 401;return actor.IsRestricted||ticket.IsRestricted?403:200;}
 private async Task<CustomRoleResult?> Gate(CurrentUserInfo actor,CancellationToken ct){var status=Identity(out var fresh);if(status!=200||fresh.UserId!=actor.UserId||fresh.SessionId!=actor.SessionId)return new(status==403?403:401,"SESSION_INVALID");if(!await AccountWriterSessionGate.IsValidAsync(db,actor,clock,ct))return new(401,"SESSION_INVALID");var permission=await RoleCatalogAuthority.EvaluateAsync(permissions,ct);return permission.IsGranted?null:new(permission.Decision==PermissionDecision.Unavailable?503:403,"ROLE_AUTHORITY_REQUIRED");}
 public async Task<CustomRoleResult> CreateAsync(CustomRoleCreateRequest input,CancellationToken ct){var request=input.Normalize();ct.ThrowIfCancellationRequested();var identity=Identity(out var actor);if(identity!=200)return new(identity,identity==403?"PASSWORD_CHANGE_REQUIRED":"SESSION_INVALID");if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Clean SQL context required.");
 await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);
 try{var guard=await new GlobalAdminSerialization(db,clock).AcquireAsync(ct:ct);var before=await guard.ReadSnapshotAsync(ct);if(await Gate(actor,ct) is {} denied)return denied;
 // Retain the case-insensitive code lookup through commit, including cooperating concurrent creates.
 var duplicate=await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE role_code COLLATE Latin1_General_100_CI_AS={request.RoleCode}").AsNoTracking().AnyAsync(ct);
 if(duplicate)return new(409,"ROLE_CODE_EXISTS");
 // RoleCode is varchar(60): reject lossy conversion instead of silently changing business input.
 var lossless=await db.Database.SqlQuery<int>($"SELECT CASE WHEN DATALENGTH(CONVERT(varchar(max),{request.RoleCode}))<=60 AND CONVERT(varbinary(max),CONVERT(nvarchar(max),CONVERT(varchar(max),{request.RoleCode})))=CONVERT(varbinary(max),CAST({request.RoleCode} AS nvarchar(max))) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct);if(lossless!=1)return new(400,"ROLE_CODE_STORAGE_INVALID");
 var now=clock.GetUtcNow().UtcDateTime;var role=NewRole(request,now);db.Roles.Add(role);try{await db.SaveChangesAsync(ct);}finally{db.Entry(role).State=EntityState.Detached;}
 var value=new CustomRoleCreatedDto(role.RoleId,role.RoleCode,role.RoleName,role.Description,false,true,0,0);
 var audit=new AuditLog{ActorUserId=actor.UserId,EntityType="roles",EntityId=role.RoleId.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="CREATE",OccurredAt=now,NewValues=JsonSerializer.Serialize(value,new JsonSerializerOptions(JsonSerializerDefaults.Web))};db.AuditLogs.Add(audit);try{await db.SaveChangesAsync(ct);}finally{db.Entry(audit).State=EntityState.Detached;}
 await guard.EnsurePreservedAsync(before,ct);if(await Gate(actor,ct) is {} final)return final;ct.ThrowIfCancellationRequested();await tx.CommitAsync(ct);return new(201,Value:value);
 }catch(GlobalAdminLockException){return new(503,"GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE");}catch(GlobalAdministrationInvariantException){return new(409,"LAST_DURABLE_ADMIN");}catch(DbUpdateException e)when(e.InnerException is SqlException{Number:2601 or 2627}&&e.Entries.Any(x=>x.Entity is Role)){return new(409,"ROLE_CODE_EXISTS");}
 }
 public static CustomRoleResult UpdateBoundary(Role? role)=>role==null?new(404,"ROLE_NOT_FOUND"):role.IsSystemRole?new(409,"SYSTEM_ROLE_PROTECTED"):new(503,"ROLE_VERSION_CONTRACT_REQUIRED");
 public async Task<CustomRoleResult> UpdateAsync(long id,CustomRoleUpdateRequest input,CancellationToken ct){_=input.Normalize();ct.ThrowIfCancellationRequested();if(id<=0)return new(400,"INVALID_ROLE_ID");var identity=Identity(out var actor);if(identity!=200)return new(identity,"SESSION_INVALID");if(db.ChangeTracker.Entries().Any()||db.Database.CurrentTransaction!=null)throw new InvalidOperationException("Clean context required.");await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted,ct);try{var guard=await new GlobalAdminSerialization(db,clock).AcquireAsync(ct:ct);_=await guard.ReadSnapshotAsync(ct);if(await Gate(actor,ct) is {} denied)return denied;var role=await db.Roles.FromSqlInterpolated($"SELECT * FROM dbo.roles WITH(UPDLOCK,HOLDLOCK) WHERE role_id={id}").AsNoTracking().SingleOrDefaultAsync(ct);if(await Gate(actor,ct) is {} final)return final;return UpdateBoundary(role);}catch(GlobalAdminLockException){return new(503,"GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE");}}
}
