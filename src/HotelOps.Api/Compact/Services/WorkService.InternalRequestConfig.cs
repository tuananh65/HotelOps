using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private static string RequestTypePrefix(long branch)=>$"IR_TYPE_{branch}_";
    private sealed record RequestTypeSettings(string? Description=null,long? DefaultDepartmentId=null);
    private static RequestTypeSettings RequestSettings(ReferenceValue row)=>
        string.IsNullOrWhiteSpace(row.SettingsJson)?new():JsonSerializer.Deserialize<RequestTypeSettings>(row.SettingsJson)??new();
    public async Task<object> InternalRequestTypes(long branch,CancellationToken ct)
    {
        await access.Require("IR_VIEW",branch,true,ct:ct);
        var head=await access.HasRole("OPERATION_MANAGER",branch,ct)&&!await IsReadOnlyWorkActor("CONFIGURATION",branch,ct);
        var prefix=RequestTypePrefix(branch);
        var rows=await db.ReferenceValues.AsNoTracking().Where(r=>r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix)).OrderBy(r=>r.Name).ThenBy(r=>r.ReferenceId).ToArrayAsync(ct);
        return new{configured=rows.Length>0,items=rows.Where(r=>head||r.IsActive).Select(r=>new{r.ReferenceId,r.Name,r.IsActive,description=RequestSettings(r).Description,defaultDepartmentId=RequestSettings(r).DefaultDepartmentId,rowVersion=Convert.ToBase64String(r.RowVersion)})};
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
        Output.Require(!await db.ReferenceValues.AnyAsync(r=>r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix)&&r.ReferenceId!=id&&r.Name==name,ct),"DUPLICATE_NAME",409);
        var retainInactiveDepartment=!request.IsActive&&row!=null&&request.DefaultDepartmentId==RequestSettings(row).DefaultDepartmentId;
        if(request.DefaultDepartmentId!=null)Output.Require(await db.Departments.AnyAsync(d=>d.DepartmentId==request.DefaultDepartmentId&&d.BranchId==branch&&(d.IsActive||retainInactiveDepartment),ct),"REQUEST_DEPARTMENT_UNAVAILABLE",400);
        if(row==null){row=new ReferenceValue{Kind="WORK_CATEGORY",Code=prefix+Guid.NewGuid().ToString("N")};db.ReferenceValues.Add(row);}
        row.Name=name;row.IsActive=request.IsActive;row.SettingsJson=JsonSerializer.Serialize(new RequestTypeSettings(request.Description?.Trim(),request.DefaultDepartmentId));
        await db.SaveChangesAsync(ct);audit.Add("InternalRequestType",row.ReferenceId,"CONFIGURE",branch,after:new{row.Name,row.IsActive,request.Description,request.DefaultDepartmentId});
        return (object)new{row.ReferenceId,row.Name,row.IsActive,rowVersion=Convert.ToBase64String(row.RowVersion)};
    },ct);
    private async Task ApplyInternalRequestType(WorkItem work,CancellationToken ct)
    {
        var prefix=RequestTypePrefix(work.BranchId);
        if(work.CategoryId==null){Output.Require(!await db.ReferenceValues.AnyAsync(r=>r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix),ct),"REQUEST_TYPE_REQUIRED",400);return;}
        var row=await db.ReferenceValues.AsNoTracking().SingleOrDefaultAsync(r=>r.ReferenceId==work.CategoryId&&r.Kind=="WORK_CATEGORY"&&r.Code.StartsWith(prefix)&&r.IsActive,ct)
            ??throw new ApiException(400,"REQUEST_TYPE_UNAVAILABLE");
        work.DepartmentId??=RequestSettings(row).DefaultDepartmentId;
        if(work.DepartmentId!=null)Output.Require(await db.Departments.AnyAsync(d=>d.DepartmentId==work.DepartmentId&&d.BranchId==work.BranchId&&d.IsActive,ct),"REQUEST_DEPARTMENT_UNAVAILABLE",400);
    }
}
