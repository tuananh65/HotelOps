using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private static string RequestTypePrefix(long branch)=>$"IR_TYPE_{branch}_";
    private sealed record RequestTypeSettings(string? Description=null,long? DefaultDepartmentId=null,
        long[]? SenderRoleIds=null,long[]? RecipientDepartmentIds=null,long[]? RecipientUserIds=null,long[]? RecipientRoleIds=null);
    private static RequestTypeSettings RequestSettings(ReferenceValue row)=>
        string.IsNullOrWhiteSpace(row.SettingsJson)?new():JsonSerializer.Deserialize<RequestTypeSettings>(row.SettingsJson)??new();
    public async Task<object> InternalRequestTypes(long branch,CancellationToken ct)
    {
        await access.Require("IR_VIEW",branch,true,ct:ct);
        var head=await access.HasRole("OPERATION_MANAGER",branch,ct)&&!await IsReadOnlyWorkActor("CONFIGURATION",branch,ct);
        var prefix=RequestTypePrefix(branch);
        var rows=await db.ReferenceValues.AsNoTracking().Where(r=>r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix)).OrderBy(r=>r.Name).ThenBy(r=>r.ReferenceId).ToArrayAsync(ct);
        var roles=await db.Roles.AsNoTracking().Where(r=>r.IsActive&&r.Code!="OWNER"&&r.Code!="ADMIN"&&r.Code!="ADMINISTRATOR"
            &&db.RolePermissions.Any(g=>g.RoleId==r.RoleId&&db.Permissions.Any(p=>p.PermissionId==g.PermissionId&&p.IsActive&&p.Code=="IR_CREATE")))
            .Select(r=>new{r.RoleId,r.Name,r.Code}).ToArrayAsync(ct);
        var actorRoles=await db.UserRoles.AsNoTracking().Where(r=>r.UserId==actor.Id&&(r.BranchId==branch||r.BranchId==null)&&r.RevokedAt==null&&r.AssignedAt<=Now&&(r.ExpiresAt==null||r.ExpiresAt>Now)).Select(r=>r.RoleId).ToArrayAsync(ct);
        var recipients=await CandidateRows(branch,"INTERNAL_REQUEST",ct);
        var recipientRoles=await RequestRecipientRoles().Select(r=>new{r.RoleId,r.Name,r.Code}).ToArrayAsync(ct);
        var candidateIds=recipients.Select(u=>u.UserId).ToArray();
        var recipientMemberships=await db.UserRoles.AsNoTracking().Where(r=>candidateIds.Contains(r.UserId)&&(r.BranchId==branch||r.BranchId==null)&&r.RevokedAt==null&&r.AssignedAt<=Now&&(r.ExpiresAt==null||r.ExpiresAt>Now))
            .Select(r=>new{r.UserId,r.RoleId}).ToArrayAsync(ct);
        return new{configured=rows.Length>0,roles,recipientRoles,recipients=recipients.Select(u=>new{u.UserId,u.FullName,u.EmployeeCode,roleIds=recipientMemberships.Where(r=>r.UserId==u.UserId).Select(r=>r.RoleId).ToArray()}),items=rows.Where(r=>head||(r.IsActive&&(RequestSettings(r).SenderRoleIds==null||RequestSettings(r).SenderRoleIds!.Intersect(actorRoles).Any()))).Select(r=>new{
            r.ReferenceId,r.Name,r.IsActive,description=RequestSettings(r).Description,defaultDepartmentId=RequestSettings(r).DefaultDepartmentId,
            senderRoleIds=RequestSettings(r).SenderRoleIds,recipientDepartmentIds=RequestSettings(r).RecipientDepartmentIds,recipientUserIds=RequestSettings(r).RecipientUserIds,recipientRoleIds=RequestSettings(r).RecipientRoleIds,
            rowVersion=Convert.ToBase64String(r.RowVersion)})};
    }
    public Task<object> SaveInternalRequestType(long branch,long? id,InternalRequestTypeRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await access.RequireRolePermission("OPERATION_MANAGER","IR_VIEW",branch,ct);
        Output.Require(!await IsReadOnlyWorkActor("CONFIGURATION",branch,ct),"WORK_ROLE_READ_ONLY",403);
        var prefix=RequestTypePrefix(branch);
        var row=id==null?null:await db.ReferenceValues.SingleOrDefaultAsync(r=>r.ReferenceId==id&&r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix),ct)
            ??throw new ApiException(404,"REQUEST_TYPE_NOT_FOUND");
        if(row!=null){Output.Require(request.RowVersion!=null,"STALE_ROW_VERSION");Output.Version(row.RowVersion,request.RowVersion!);}
        else Output.Require(request.RowVersion==null,"STALE_ROW_VERSION");
        var name=request.Name.Trim();Output.Require(name.Length>0,"NAME_REQUIRED",400);
        var senders=request.SenderRoleIds?.Distinct().ToArray();var departments=request.RecipientDepartmentIds?.Distinct().ToArray();var users=request.RecipientUserIds?.Distinct().ToArray();
        var receiverRoles=request.RecipientRoleIds?.Distinct().ToArray();
        if(receiverRoles!=null){
            Output.Require(receiverRoles.Length>0,"REQUEST_RECIPIENT_REQUIRED",400);
            Output.Require(await RequestRecipientRoles().CountAsync(r=>receiverRoles.Contains(r.RoleId),ct)==receiverRoles.Length,"REQUEST_RECIPIENT_ROLE_INVALID",400);
            Output.Require((departments?.Length??0)==0&&(users?.Length??0)==0&&request.DefaultDepartmentId==null,"REQUEST_RECIPIENT_NOT_ALLOWED",400);
            departments=null;users=null;
        }
        Output.Require(senders==null||senders.Length>0,"REQUEST_SENDER_REQUIRED",400);
        if(senders!=null)Output.Require(await db.Roles.CountAsync(r=>senders.Contains(r.RoleId)&&r.IsActive&&r.Code!="OWNER"&&r.Code!="ADMIN"&&r.Code!="ADMINISTRATOR"
            &&db.RolePermissions.Any(g=>g.RoleId==r.RoleId&&db.Permissions.Any(p=>p.PermissionId==g.PermissionId&&p.IsActive&&p.Code=="IR_CREATE")),ct)==senders.Length,"REQUEST_SENDER_INVALID",400);
        if(departments!=null||users!=null){
            Output.Require((departments?.Length??0)+(users?.Length??0)>0,"REQUEST_RECIPIENT_REQUIRED",400);
            Output.Require(departments==null||await db.Departments.CountAsync(d=>departments.Contains(d.DepartmentId)&&d.BranchId==branch&&d.IsActive,ct)==departments.Length,"REQUEST_DEPARTMENT_UNAVAILABLE",400);
            var eligible=(await CandidateRows(branch,"INTERNAL_REQUEST",ct)).Select(u=>u.UserId).ToArray();
            Output.Require(users==null||users.All(eligible.Contains),"REQUEST_RECIPIENT_UNAVAILABLE",400);
        }
        Output.Require(!await db.ReferenceValues.AnyAsync(r=>r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix)&&r.ReferenceId!=id&&r.Name==name,ct),"DUPLICATE_NAME",409);
        var retainInactiveDepartment=!request.IsActive&&row!=null&&request.DefaultDepartmentId==RequestSettings(row).DefaultDepartmentId;
        if(request.DefaultDepartmentId!=null)Output.Require(await db.Departments.AnyAsync(d=>d.DepartmentId==request.DefaultDepartmentId&&d.BranchId==branch&&(d.IsActive||retainInactiveDepartment),ct),"REQUEST_DEPARTMENT_UNAVAILABLE",400);
        if(row==null){row=new ReferenceValue{Kind="WORK_CATEGORY",Code=prefix+Guid.NewGuid().ToString("N")};db.ReferenceValues.Add(row);}
        row.Name=name;row.IsActive=request.IsActive;row.SettingsJson=JsonSerializer.Serialize(new RequestTypeSettings(request.Description?.Trim(),request.DefaultDepartmentId,senders,departments,users,receiverRoles));
        await db.SaveChangesAsync(ct);audit.Add("InternalRequestType",row.ReferenceId,"CONFIGURE",branch,after:new{row.Name,row.IsActive,request.Description,request.DefaultDepartmentId,senders,departments,users,receiverRoles});
        return (object)new{row.ReferenceId,row.Name,row.IsActive,rowVersion=Convert.ToBase64String(row.RowVersion)};
    },ct);
    private async Task ApplyInternalRequestType(WorkItem work,CancellationToken ct,long? recipientUserId=null)
    {
        var prefix=RequestTypePrefix(work.BranchId);
        if(work.CategoryId==null){Output.Require(!await db.ReferenceValues.AnyAsync(r=>r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix),ct),"REQUEST_TYPE_REQUIRED",400);return;}
        var row=await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(r=>r.ReferenceId==work.CategoryId&&r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix)&&r.IsActive,ct)
            ??throw new ApiException(400,"REQUEST_TYPE_UNAVAILABLE");
        var settings=RequestSettings(row);
        if(settings.SenderRoleIds!=null){
            var roleIds=settings.SenderRoleIds;
            Output.Require(await db.UserRoles.AnyAsync(r=>r.UserId==actor.Id&&roleIds.Contains(r.RoleId)&&(r.BranchId==work.BranchId||r.BranchId==null)&&r.RevokedAt==null&&r.AssignedAt<=Now&&(r.ExpiresAt==null||r.ExpiresAt>Now)&&db.Roles.Any(role=>role.RoleId==r.RoleId&&role.IsActive),ct),"REQUEST_SENDER_NOT_ALLOWED",403);
        }
        if(recipientUserId!=null){
            var allowedRoles=RequestRecipientRoles();
            var roleRecipient=settings.RecipientRoleIds!=null&&await db.UserRoles.AnyAsync(r=>r.UserId==recipientUserId&&settings.RecipientRoleIds.Contains(r.RoleId)&&(r.BranchId==work.BranchId||r.BranchId==null)&&r.RevokedAt==null&&r.AssignedAt<=Now&&(r.ExpiresAt==null||r.ExpiresAt>Now)&&allowedRoles.Any(role=>role.RoleId==r.RoleId),ct);
            Output.Require(work.DepartmentId==null&&(roleRecipient||settings.RecipientRoleIds==null&&settings.RecipientUserIds?.Contains(recipientUserId.Value)==true),"REQUEST_RECIPIENT_NOT_ALLOWED",403);
            Output.Require((await CandidateRows(work.BranchId,"INTERNAL_REQUEST",ct)).Any(u=>u.UserId==recipientUserId),"REQUEST_RECIPIENT_UNAVAILABLE",409);
        }else{
            Output.Require(settings.RecipientRoleIds==null,"REQUEST_RECIPIENT_REQUIRED",400);
            work.DepartmentId??=settings.DefaultDepartmentId;
            if(settings.RecipientDepartmentIds!=null||settings.RecipientUserIds!=null)
                Output.Require(work.DepartmentId!=null&&settings.RecipientDepartmentIds?.Contains(work.DepartmentId.Value)==true,"REQUEST_RECIPIENT_NOT_ALLOWED",403);
        }
        if(work.DepartmentId!=null)Output.Require(await db.Departments.AnyAsync(d=>d.DepartmentId==work.DepartmentId&&d.BranchId==work.BranchId&&d.IsActive,ct),"REQUEST_DEPARTMENT_UNAVAILABLE",400);
    }
    private IQueryable<Role> RequestRecipientRoles()=>db.Roles.AsNoTracking().Where(r=>r.IsActive&&r.Code!="OWNER"&&r.Code!="ADMIN"&&r.Code!="ADMINISTRATOR"&&r.Code!="OPERATION_MANAGER"
        &&db.RolePermissions.Any(g=>g.RoleId==r.RoleId&&db.Permissions.Any(p=>p.PermissionId==g.PermissionId&&p.IsActive&&p.Code=="IR_PROCESS")));
}
