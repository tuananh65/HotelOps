using System.Net.Http.Json;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal static class SprintTaskRequesterScenario
{
    internal static async Task<(long RequestId,long RequesterId)> RunAsync(HotelOpsDbContext db,HttpClient manager,long actor,long verifier,
        long branch,long foreign,long department,long task,List<object> checks,CancellationToken ct)
    {
        void Check(bool pass,string name){if(!pass)throw new Exception(name);checks.Add(new{requesterCheck=name});}
        var root=$"/api/branches/{branch}/operational-tasks/{task}/verification";
        async Task<(JsonElement Body,string Tag)> Review()
        {
            using var response=await manager.GetAsync(root,ct);response.EnsureSuccessStatusCode();
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));return(json.RootElement.Clone(),response.Headers.ETag!.ToString());
        }
        async Task<int> Return(string tag,long submission)
        {
            var token=await manager.GetFromJsonAsync<JsonElement>("/api/auth/csrf",ct);
            using var request=new HttpRequestMessage(HttpMethod.Post,root){Content=JsonContent.Create(new{submissionId=submission,decision="RETURN",note="Requester source security trial"})};
            request.Headers.Add("X-CSRF-TOKEN",token.GetProperty("csrfToken").GetString());request.Headers.Add("If-Match",tag);
            using var response=await manager.SendAsync(request,ct);return(int)response.StatusCode;
        }
        var suffix=Guid.NewGuid().ToString("N")[..8];
        var requester=new User{EmployeeCode="D3-REQ-"+suffix,Username="task_requester_"+suffix,Email=suffix+"@example.invalid",FullName="Synthetic original requester",
            AccountStatus="ACTIVE",EmploymentStatus="ACTIVE",CreatedAt=DateTime.UtcNow};
        requester.PasswordHash=new PasswordHasher<User>().HashPassword(requester,Guid.NewGuid().ToString("N")+"aA1!");
        db.Users.Add(requester);
        var type=new InternalRequestType{TypeCode="D3-TASK-"+suffix,TypeName="Synthetic operational request",IsActive=true};db.InternalRequestTypes.Add(type);
        await db.SaveChangesAsync(ct);
        var source=new InternalRequest{RequestNo="D3-TASK-"+suffix,BranchId=branch,RequestTypeId=type.RequestTypeId,RequesterId=requester.UserId,
            RequesterDepartmentId=department,ReceivingDepartmentId=department,Subject="Synthetic task source",Description="Original request context",Status="IN_PROGRESS",CreatedAt=DateTime.UtcNow};
        db.InternalRequests.Add(source);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
        await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.SourceType,"INTERNAL_REQUEST").SetProperty(t=>t.SourceId,source.InternalRequestId),ct);
        var review=await Review();var submission=review.Body.GetProperty("submissionId").GetInt64();
        Check(review.Body.GetProperty("canDecide").GetBoolean(),"valid same-branch Internal Request resolves a distinct official requester");
        var baseline=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_verification_events WHERE submission_id={submission}").SingleAsync(ct);
        // Change the SOURCE between review and mutation, without changing the task's
        // rowversion: the server must read current requester authority, not the UI hint.
        await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RequesterId,verifier),ct);
        try{
            var self=await Review();Check(self.Tag==review.Tag&&!self.Body.GetProperty("canDecide").GetBoolean()&&self.Body.GetProperty("denialCode").GetString()=="MSG22",
                "linked requester is denied even though the operational task creator differs");
            Check(await Return(review.Tag,submission)==403,"source requester changed after review is reauthorized at mutation");
        }finally{await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RequesterId,requester.UserId),CancellationToken.None);}
        await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.CreatedBy,verifier),ct);
        try{var creator=await Review();Check(!creator.Body.GetProperty("canDecide").GetBoolean()&&await Return(creator.Tag,submission)==403,"task creator cannot verify even when linked requester is another user");}
        finally{await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.CreatedBy,actor),CancellationToken.None);}
        await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.BranchId,foreign),ct);
        try{var denied=await Review();Check(!denied.Body.GetProperty("canDecide").GetBoolean()&&denied.Body.GetProperty("denialCode").GetString()=="SOURCE_PROVENANCE_REQUIRED"&&await Return(denied.Tag,submission)==503,"foreign-branch source cannot confer verification authority");}
        finally{await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.BranchId,branch),CancellationToken.None);}
        var foreignDepartment=await db.Departments.Where(d=>d.BranchId==foreign).Select(d=>d.DepartmentId).FirstAsync(ct);
        await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,foreignDepartment),ct);
        try{var denied=await Review();Check(await Return(denied.Tag,submission)==503,"source receiving department inconsistent with branch fails closed");}
        finally{await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.ReceivingDepartmentId,department),CancellationToken.None);}
        await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RequesterDepartmentId,foreignDepartment),ct);
        try{var denied=await Review();Check(await Return(denied.Tag,submission)==503,"source requester department inconsistent with branch fails closed");}
        finally{await db.InternalRequests.Where(r=>r.InternalRequestId==source.InternalRequestId).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RequesterDepartmentId,department),CancellationToken.None);}
        foreach(var kind in new[]{"MAINTENANCE","INSPECTION","CLEANING","MANUAL"})
        {
            await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.SourceType,kind),ct);
            try{var denied=await Review();Check(!denied.Body.GetProperty("canDecide").GetBoolean()&&await Return(denied.Tag,submission)==503,kind+" linked provenance remains explicitly unsupported");}
            finally{await db.OperationalTasks.Where(t=>t.TaskId==task).ExecuteUpdateAsync(s=>s.SetProperty(t=>t.SourceType,"INTERNAL_REQUEST"),CancellationToken.None);}
        }
        Check(await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.task_verification_events WHERE submission_id={submission}").SingleAsync(ct)==baseline
            &&(await Review()).Body.GetProperty("state").GetString()=="PENDING_VERIFICATION","source denials create no verification or state transition");
        Check((await Review()).Body.GetProperty("canDecide").GetBoolean(),"verified source restored for real browser Return/resubmit/Accept");
        return(source.InternalRequestId,requester.UserId);
    }
}
