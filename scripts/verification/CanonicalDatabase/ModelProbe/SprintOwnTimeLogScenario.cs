using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintOwnTimeLogScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db,HttpClient http,long actor,long branch,long department,List<object> checks,CancellationToken ct,string username,string password)
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(new{ownTimeLogCheck=name});}
        var role=await db.Roles.SingleAsync(r=>r.RoleCode=="DEPT_MANAGER",ct);
        var permission=await db.Permissions.SingleAsync(p=>p.PermissionCode=="ATTENDANCE_RECORD",ct);
        var existing=await db.RolePermissions.AsNoTracking().SingleOrDefaultAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct);
        if(existing==null){db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope="OWN",GrantedAt=DateTime.UtcNow});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();}
        var now=DateTime.UtcNow;var date=DateOnly.FromDateTime(now.AddHours(7));
        var day=new Workday{UserId=actor,BranchId=branch,DepartmentId=department,WorkDate=date,IsUnscheduled=true,UnscheduledReason="Synthetic own time log",Status="WORKING",StartedAt=now.AddMinutes(-30)};
        var task=new OperationalTask{TaskCode="D3TIME"+Guid.NewGuid().ToString("N")[..8],BranchId=branch,DepartmentId=department,Title="Synthetic time log task",Status="IN_PROGRESS",CreatedBy=actor,CreatedAt=now.AddMinutes(-30)};
        db.AddRange(day,task);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        WorkInterval Interval(int start,int? end)=>new(){WorkdayId=day.WorkdayId,UserId=actor,BranchId=branch,WorkItemKind="TASK",WorkItemId=task.TaskId,OpenedBy=actor,
            OpenedAt=now.AddMinutes(start),EffectiveStartedAt=now.AddMinutes(start),ClosedBy=end.HasValue?actor:null,ClosedAt=end.HasValue?now.AddMinutes(end.Value):null,EffectiveEndedAt=end.HasValue?now.AddMinutes(end.Value):null};
        var empty=Interval(-25,-25);
        db.AddRange(Interval(-30,-20),empty,Interval(-10,null));await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var code="D3TIMEOTHER"+Guid.NewGuid().ToString("N")[..8];
        var other=new User{Username=code,EmployeeCode=code,FullName="Synthetic other time owner",Email=code+"@example.invalid",AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",DepartmentId=department,CreatedAt=now};
        db.Users.Add(other);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var otherDay=new Workday{UserId=other.UserId,BranchId=branch,DepartmentId=department,WorkDate=date,IsUnscheduled=true,UnscheduledReason="Synthetic unrelated time",Status="ENDED",StartedAt=now.AddMinutes(-30),EndedAt=now.AddMinutes(-20)};
        db.Workdays.Add(otherDay);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var otherInterval=Interval(-30,-20);otherInterval.UserId=other.UserId;otherInterval.WorkdayId=otherDay.WorkdayId;
        db.WorkIntervals.Add(otherInterval);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        var path=$"/api/branches/{branch}/workdays/own-time-log?date={date:yyyy-MM-dd}&pageSize=1";
        using(var response=await http.GetAsync(path,ct))
        {
            Check((int)response.StatusCode==200,"own log HTTPS read allowed with current own attendance grant");
            var body=await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Check(body.GetProperty("total").GetInt32()==3&&body.GetProperty("items").GetArrayLength()==1,"daily summary spans all pages");
            Check(body.GetProperty("workClosedSeconds").GetInt64()==600&&body.GetProperty("workProvisionalSeconds").GetInt64()>=600
                &&body.GetProperty("workdayProvisionalSeconds").GetInt64()>=1800,"pause excluded and workday total separate from task effort");
        }
        using(var response=await http.GetAsync(path+"&page=2",ct))Check((int)response.StatusCode==200,"second own interval page available");
        using(var response=await http.GetAsync(path+$"&userId={other.UserId}",ct))
        {
            Check((int)response.StatusCode==200,"query cannot replace authenticated time owner");
            var body=await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            Check(body.GetProperty("total").GetInt32()==3&&body.GetProperty("items")[0].GetProperty("intervalId").GetInt64()!=otherInterval.IntervalId,"other employee intervals excluded despite same branch");
        }
        using(var response=await http.GetAsync(path+"&page=0",ct))Check((int)response.StatusCode==400,"invalid paging rejected");
        var foreign=await db.Branches.Where(b=>b.BranchId!=branch).Select(b=>b.BranchId).FirstAsync(ct);
        using(var response=await http.GetAsync($"/api/branches/{foreign}/workdays/own-time-log?date={date:yyyy-MM-dd}",ct))Check((int)response.StatusCode==403,"foreign branch own log denied");
        var grant=await db.RolePermissions.AsNoTracking().SingleAsync(g=>g.RoleId==role.RoleId&&g.PermissionId==permission.PermissionId,ct);
        await db.RolePermissions.Where(g=>g.RolePermissionId==grant.RolePermissionId).ExecuteDeleteAsync(ct);
        using(var response=await http.GetAsync(path,ct))Check((int)response.StatusCode==403,"revoked attendance grant denies own time log");
        db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=permission.PermissionId,DataScope=grant.DataScope,GrantedAt=grant.GrantedAt});await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.WorkIntervals.Where(i=>i.IntervalId==empty.IntervalId).ExecuteUpdateAsync(u=>u.SetProperty(i=>i.EffectiveEndedAt,now.AddMinutes(-15)),ct);
        using(var denied=await http.GetAsync(path,ct))Check((int)denied.StatusCode==409,"real overlap is denied even with a formerly zero-duration event");
        await db.WorkIntervals.Where(i=>i.IntervalId==empty.IntervalId).ExecuteUpdateAsync(u=>u.SetProperty(i=>i.EffectiveEndedAt,now.AddMinutes(-25)),ct);
        using(var allowed=await http.GetAsync(path,ct))Check((int)allowed.StatusCode==200,"zero-duration event within closed work is allowed without inflating effort");
        var start=new ProcessStartInfo("node"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add("frontend/scripts/sprintOwnTimeLogBrowser.mjs");
        using var browser=Process.Start(start)??throw new Exception("Own time log browser unavailable");
        var output=browser.StandardOutput.ReadToEndAsync(ct);var errors=browser.StandardError.ReadToEndAsync(ct);
        try{await browser.StandardInput.WriteAsync(JsonSerializer.Serialize(new{username,password,branch,foreign,intervalCount=3,date=date.ToString("yyyy-MM-dd")}));browser.StandardInput.Close();await browser.WaitForExitAsync(ct);
            var proof=JsonSerializer.Deserialize<JsonElement>(await output);_=await errors;checks.Add(new{ownTimeLogBrowser=proof});if(browser.ExitCode!=0||!proof.GetProperty("success").GetBoolean())throw new Exception("Own time log browser failed at "+proof.GetProperty("stage").GetString());
        }finally{if(!browser.HasExited)browser.Kill(entireProcessTree:true);}
    }
}
