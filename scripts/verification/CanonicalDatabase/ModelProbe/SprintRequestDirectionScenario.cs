using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintRequestDirectionScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long request,List<object> checks,CancellationToken ct)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(new{requestDirectionCheck=label});}
        var row=await db.InternalRequests.AsNoTracking().SingleAsync(r=>r.InternalRequestId==request,ct);
        var department=await db.Departments.Where(d=>d.BranchId==row.BranchId&&d.DepartmentId!=row.ReceivingDepartmentId).Select(d=>d.DepartmentId).SingleAsync(ct);
        var other=await db.Users.Where(u=>u.UserId!=actor).Select(u=>u.UserId).SingleAsync(ct);
        async Task<long[]> Read(string direction){using var response=await http.GetAsync("/api/internal-requests?direction="+direction,ct);Check((int)response.StatusCode==200,"direction "+direction+" read");var data=await response.Content.ReadFromJsonAsync<JsonElement>(ct);Check(data.GetProperty("totalCount").GetInt32()==data.GetProperty("items").GetArrayLength(),"filtered count agrees with scoped list");return data.GetProperty("items").EnumerateArray().Select(x=>x.GetProperty("id").GetInt64()).ToArray();}
        Check((await Read("SENT")).SequenceEqual(new[]{request})&&(await Read("RECEIVED")).SequenceEqual(new[]{request}),"requester and mapped manager appears once in both; foreign branch excluded");
        using(var invalid=await http.GetAsync("/api/internal-requests?direction=OTHER",ct))Check((int)invalid.StatusCode==400,"invalid direction rejected");
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,department),ct);
        Check((await Read("RECEIVED")).Length==0&&(await Read("SENT")).SequenceEqual(new[]{request}),"routing removes previous receiving manager but preserves Sent");
        var assignment=new InternalRequestAssignment{InternalRequestId=request,AssigneeId=actor,AssignedBy=actor,AssignedAt=DateTime.UtcNow.AddMinutes(-1),Status="ACTIVE"};
        db.InternalRequestAssignments.Add(assignment);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        Check((await Read("RECEIVED")).SequenceEqual(new[]{request}),"current handler receives without department mapping");
        await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.AssigneeId,other),ct);
        Check((await Read("RECEIVED")).Length==0,"former handler removed immediately after assignment change");
        await db.InternalRequests.Where(r=>r.InternalRequestId==request).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,row.ReceivingDepartmentId),ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=SYSUTCDATETIME() WHERE user_id={actor} AND branch_id={row.BranchId} AND department_id={row.ReceivingDepartmentId}",ct);
        Check((await Read("RECEIVED")).Length==0,"revoked mapping removes manager Received");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.department_management_access SET revoked_at=NULL WHERE user_id={actor} AND branch_id={row.BranchId} AND department_id={row.ReceivingDepartmentId}",ct);
        var grant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.Role.RoleCode=="DEPT_MANAGER"&&g.Permission.PermissionCode=="IR_PROCESS",ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteDeleteAsync(ct);
        try
        {
            Check((await Read("RECEIVED")).Length==0,"revoked IR_PROCESS removes manager Received despite IR_VIEW");
            await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.AssigneeId,actor),ct);
            Check((await Read("RECEIVED")).SequenceEqual(new[]{request}),"current handler retains Received with IR_VIEW after manager processing grant revoked");
            Check((await Read("SENT")).SequenceEqual(new[]{request}),"Sent remains requester-based after processing grant revoked");
        }
        finally{db.RolePermissions.Add(new(){RoleId=grant.RoleId,PermissionId=grant.PermissionId,DataScope=grant.DataScope,GrantedAt=grant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();}
        var readGrant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.Role.RoleCode=="DEPT_MANAGER"&&g.Permission.PermissionCode=="IR_VIEW",ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==readGrant.RolePermissionId).ExecuteDeleteAsync(ct);
        try
        {
            foreach(var direction in new[]{"SENT","RECEIVED"})
            {
                using var denied=await http.GetAsync("/api/internal-requests?direction="+direction,ct);
                Check((int)denied.StatusCode==403,"requester, current handler and manager cannot replace IR_VIEW for "+direction);
            }
        }
        finally{db.RolePermissions.Add(new(){RoleId=readGrant.RoleId,PermissionId=readGrant.PermissionId,DataScope=readGrant.DataScope,GrantedAt=readGrant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();}
        Check((await Read("RECEIVED")).SequenceEqual(new[]{request}),"restored current grants and mapping allow Received");
        await db.InternalRequestAssignments.Where(a=>a.AssignmentId==assignment.AssignmentId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.AssigneeId,other),ct);
    }
}
