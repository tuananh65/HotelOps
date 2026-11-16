using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.OperationalTasks;

public sealed record WorkCategoryInput(string Code,string Name,string? Description,long? DepartmentId,bool IsActive,bool Confirmation);
public sealed record WorkCategoryView(long Id,string Code,string Name,string? Description,long? DepartmentId,bool IsActive,long UsageCount,string Version);
public sealed record WorkCategoryDepartment(long Id,string Name);
public sealed record WorkCategoryPage(IReadOnlyList<WorkCategoryView> Items,IReadOnlyList<WorkCategoryDepartment> Departments);

public sealed class WorkCategoryAdministrationService(HotelOpsDbContext db,ISqlSessionService sessions,ICurrentUserAccessor current)
{
    private static InternalRequestReadException Error(int status,string code)=>new(status,code);
    private async Task Gate(SessionReference ticket,long sid,long? branch,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw Error(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)throw Error(401,"SESSION_INVALID");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths=await (from a in db.UserRoles.AsNoTracking()
            join p in db.RolePermissions.AsNoTracking() on a.RoleId equals p.RoleId
            where a.UserId==ticket.UserId && a.Role.RoleCode=="SYS_ADMIN" && p.Permission.PermissionCode=="MASTER_DATA_MANAGE"
                && a.User.AccountStatus=="ACTIVE" && a.User.EmploymentStatus=="ACTIVE" && a.User.DeletedAt==null
            select new PermissionGrantPath(p.Permission.PermissionCode,p.DataScope,a.BranchId,a.IsActive,a.Role.IsActive,a.AssignedAt,a.ExpiresAt,
                a.Branch!=null&&a.Branch.IsActive,db.UserBranchAccesses.Any(b=>b.UserId==ticket.UserId&&b.BranchId==a.BranchId&&b.RevokedAt==null&&b.GrantedAt<=now))).ToArrayAsync(ct);
        if(!paths.Any(p=>PermissionGrantRules.IsEligible(p,now)&&(PermissionGrantRules.EffectiveScope(p)=="GLOBAL"
            ||branch!=null&&p.BranchId==branch&&PermissionGrantRules.EffectiveScope(p)=="BRANCH")))throw Error(403,"ADMIN_MASTER_DATA_MANAGE_REQUIRED");
        if(branch!=null && !await db.UserBranchAccesses.AsNoTracking().AnyAsync(b=>b.UserId==ticket.UserId&&b.BranchId==branch&&b.Branch.IsActive&&b.RevokedAt==null&&b.GrantedAt<=now,ct))
            throw Error(403,"BRANCH_ACCESS_REQUIRED");
    }
    private IQueryable<WorkCategory> Scoped(long? branch)=>db.WorkCategories.AsNoTracking().Where(c=>branch==null?c.DepartmentId==null:c.Department!=null&&c.Department.BranchId==branch);
    private async Task<WorkCategoryView> View(WorkCategory c,CancellationToken ct)
    {
        var id=c.CategoryId.ToString(CultureInfo.InvariantCulture);
        var audit=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="work_categories"&&a.EntityId==id).Select(a=>(long?)a.AuditId).MaxAsync(ct)??0;
        var count=await db.OperationalTasks.AsNoTracking().LongCountAsync(t=>t.CategoryId==c.CategoryId,ct);
        var version='"'+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{c.CategoryId,c.CategoryCode,c.CategoryName,c.Description,c.DepartmentId,c.IsActive,audit}))))+'"';
        return new(c.CategoryId,c.CategoryCode,c.CategoryName,c.Description,c.DepartmentId,c.IsActive,count,version);
    }
    public async Task<WorkCategoryPage> ListAsync(SessionReference ticket,long sid,long? branch,long after,CancellationToken ct)
    {
        if(branch is <=0||after<0)throw Error(400,"INVALID_CATEGORY_FILTER");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await Gate(ticket,sid,branch,ct);
        var rows=await Scoped(branch).Where(c=>c.CategoryId>after).OrderBy(c=>c.CategoryId).Take(100).ToArrayAsync(ct);
        var views=new List<WorkCategoryView>();foreach(var row in rows)views.Add(await View(row,ct));
        var departments=branch==null?[]:await db.Departments.AsNoTracking().Where(d=>d.BranchId==branch&&d.IsActive)
            .OrderBy(d=>d.DepartmentName).Select(d=>new WorkCategoryDepartment(d.DepartmentId,d.DepartmentName)).ToArrayAsync(ct);
        await Gate(ticket,sid,branch,ct);await tx.CommitAsync(ct);return new(views,departments);
    }
    public async Task<WorkCategoryView> SaveAsync(SessionReference ticket,long sid,long? branch,long? id,string? tag,WorkCategoryInput input,CancellationToken ct)
    {
        var code=input.Code?.Trim().ToUpperInvariant();var name=input.Name?.Trim();var description=input.Description?.Trim();
        if(branch is <=0||id is <=0||input.DepartmentId is <=0||string.IsNullOrWhiteSpace(code)||code.Length>40
            ||code.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-')||string.IsNullOrWhiteSpace(name)||name.Length>150||name.Any(char.IsControl)
            ||description?.Length>4000||description?.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')==true)
            throw Error(400,"INVALID_CATEGORY_INPUT");
        if((branch==null)!=(input.DepartmentId==null))throw Error(400,"CATEGORY_SCOPE_MISMATCH");
        if(id!=null&&string.IsNullOrEmpty(tag))throw Error(428,"IF_MATCH_REQUIRED");
        if(!input.IsActive&&!input.Confirmation)throw Error(400,"DEACTIVATION_CONFIRMATION_REQUIRED");
        if(id==null&&!input.IsActive)throw Error(400,"NEW_CATEGORY_MUST_BE_ACTIVE");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await Gate(ticket,sid,branch,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:WorkCategories:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51880,'Category catalog busy.',1;",ct);
        if(input.DepartmentId is {} department&&!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==department&&d.BranchId==branch&&d.IsActive,ct))
            throw Error(400,"ACTIVE_DEPARTMENT_REQUIRED");
        WorkCategoryView? before=null;WorkCategory row;
        if(id!=null)
        {
            row=await Scoped(branch).SingleOrDefaultAsync(c=>c.CategoryId==id,ct)??throw Error(404,"CATEGORY_UNAVAILABLE");
            before=await View(row,ct);if(before.Version!=tag)throw Error(412,"CATEGORY_CHANGED");
            if(row.DepartmentId!=input.DepartmentId)throw Error(409,"CATEGORY_SCOPE_IMMUTABLE");
            if(before.UsageCount>0&&(code!=row.CategoryCode||name!=row.CategoryName||description!=row.Description))throw Error(409,"USED_CATEGORY_MEANING_IMMUTABLE");
            db.Attach(row);
        }
        else
        {
            if(!await TaskPlanningStore.InstalledAsync(db,ct))throw Error(503,"TASK_CATEGORY_PROCESS_STORAGE_REQUIRED");
            row=new WorkCategory{DepartmentId=input.DepartmentId};db.WorkCategories.Add(row);
        }
        if(await db.WorkCategories.AsNoTracking().AnyAsync(c=>c.CategoryId!=row.CategoryId&&c.CategoryCode.ToUpper()==code,ct))throw Error(409,"MSG35");
        row.CategoryCode=code;row.CategoryName=name;row.Description=description;row.IsActive=input.IsActive;
        await db.SaveChangesAsync(ct);
        if(id==null)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.task_category_processes(category_id,process_code) VALUES({row.CategoryId},'OPERATIONAL')",ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,BranchId=branch,EntityType="work_categories",EntityId=row.CategoryId.ToString(CultureInfo.InvariantCulture),
            Action=id==null?"CREATE":"UPDATE",OccurredAt=now,OldValues=before==null?null:JsonSerializer.Serialize(before),
            NewValues=JsonSerializer.Serialize(new{row.CategoryCode,row.CategoryName,row.Description,row.DepartmentId,row.IsActive})});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await Gate(ticket,sid,branch,ct);
        var result=await View(row,ct);await tx.CommitAsync(ct);return result;
    }
}
