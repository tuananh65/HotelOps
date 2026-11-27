using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Security;

public static class CleaningContinuationAccess
{
    internal const string BoundWorkKey="HotelOps.CleaningContinuationWork";
    public static bool IsContinuationRoute(string method,string path)
    {
        var p=path.TrimEnd('/').ToLowerInvariant().Split('/',StringSplitOptions.RemoveEmptyEntries);
        if(!method.Equals("POST",StringComparison.OrdinalIgnoreCase)||p.Length<3||p[0]!="api"||p[1]!="v2")return false;
        return p[2]=="work"&&p.Length==6&&long.TryParse(p[3],out _)&&p[4]=="actions"&&p[5] is "complete" or "pause" or "resume" or "request-handover"
            ||p[2]=="evidence"&&(p.Length==3||p.Length==5&&long.TryParse(p[3],out _)&&p[4]=="invalidate");
    }

    public static async Task<bool> AllowsRequest(HotelOpsCompactDbContext db,Actor actor,TimeProvider clock,long branch,CancellationToken ct)
    {
        var request=actor.Request;
        if(request==null)return false;
        if(request.HttpContext.Items.TryGetValue(BoundWorkKey,out var bound)&&bound is long boundWork)
            return await CanFinishExisting(db,actor,clock,boundWork,branch,ct);
        if(!IsContinuationRoute(request.Method,request.Path.Value??""))return false;
        var p=request.Path.Value!.TrimEnd('/').Split('/',StringSplitOptions.RemoveEmptyEntries);
        long? workId=null;
        if(p[2].Equals("work",StringComparison.OrdinalIgnoreCase)){
            workId=long.Parse(p[3]);
            if(p[^1]=="request-handover")return await CanRequestHandover(db,actor,clock,workId.Value,branch,ct);
        }
        else if(p.Length==5)
            workId=await db.Attachments.AsNoTracking().Where(a=>a.AttachmentId==long.Parse(p[3])&&a.BranchId==branch&&a.InvalidatedAt==null).Select(a=>a.WorkId).SingleOrDefaultAsync(ct);
        else if(request.HasFormContentType)
        {
            var form=await request.ReadFormAsync(ct);
            if(long.TryParse(form["workId"],out var id)&&string.IsNullOrEmpty(form["documentId"])&&string.IsNullOrEmpty(form["assetId"]))workId=id;
        }
        return workId is long key&&await CanFinishExisting(db,actor,clock,key,branch,ct,allowSubmitted:p[2].Equals("work",StringComparison.OrdinalIgnoreCase)&&p[^1].Equals("complete",StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<bool> CanFinishExisting(HotelOpsCompactDbContext db,Actor actor,TimeProvider clock,long workId,long branchId,CancellationToken ct,bool allowSubmitted=false)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        var access=new Access(db,actor,clock);
        if(!await access.Allows("CLEANING_EXECUTE",branchId,owns:true,ct:ct)||!await access.HasRole("HK_STAFF",branchId,ct))return false;
        var w=await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w=>w.WorkId==workId&&w.BranchId==branchId,ct);
        if(w==null||w.Kind is not ("CLEANING" or "REWORK")||!(w.Status is "IN_PROGRESS" or "ON_HOLD"||allowSubmitted&&w.Status is "PENDING_INSPECTION" or "PENDING_REINSPECTION"))return false;
        var assignment=await db.WorkAssignments.AsNoTracking().SingleOrDefaultAsync(a=>a.WorkId==workId&&a.BranchId==branchId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"&&a.AcceptedAt!=null,ct);
        if(assignment==null)return false;
        var started=await db.WorkEvents.AsNoTracking().Where(e=>e.WorkId==workId&&e.BranchId==branchId&&e.EventType=="START"&&e.ActorUserId==actor.Id&&e.OccurredAt>=assignment.AssignedAt&&e.OccurredAt>=assignment.AcceptedAt&&e.OccurredAt<=now)
            .OrderByDescending(e=>e.WorkEventId).Select(e=>(DateTime?)e.OccurredAt).FirstOrDefaultAsync(ct);
        if(started==null)return false;
        var consumptionWorkId=w.Kind=="REWORK"?w.ParentWorkId:w.WorkId;
        if(w.Kind=="REWORK"&&!await db.WorkItems.AsNoTracking().AnyAsync(p=>p.WorkId==consumptionWorkId&&p.BranchId==branchId&&p.Kind=="CLEANING"&&p.RoomId==w.RoomId&&p.TurnoverNo==w.TurnoverNo,ct))return false;
        var hasConsumption=await (from ledger in db.StaffSupplyLedgers.AsNoTracking()
            join balance in db.StaffSupplyBalances on ledger.BalanceId equals balance.BalanceId
            join allocation in db.StaffSupplyAllocations on balance.AllocationId equals allocation.AllocationId
            where ledger.WorkId==consumptionWorkId&&ledger.Kind=="USE"&&ledger.Quantity>0&&ledger.OccurredAt<=now
                &&allocation.BranchId==branchId
            select ledger.LedgerId).AnyAsync(ct);
        if(!hasConsumption){
            var saved=await db.AuditLogs.AsNoTracking().Where(a=>a.BranchId==branchId&&a.EntityType=="WorkItem"
                &&a.EntityKey==consumptionWorkId.ToString()&&(a.Action=="SUPPLIES_CONSUMED_AT_ACCEPTANCE"||a.Action=="SUPPLIES_CONFIRMED"))
                .OrderByDescending(a=>a.AuditId).Select(a=>a.AfterJson).FirstOrDefaultAsync(ct);
            if(saved==null)return false;
            using var snapshot=System.Text.Json.JsonDocument.Parse(saved);
            var root=snapshot.RootElement;
            if(!root.TryGetProperty("result",out var result)&&!root.TryGetProperty("Result",out result))return false;
            var confirmed=System.Text.Json.JsonSerializer.Deserialize<HotelOps.Api.Compact.Contracts.RoomSupplyAssessmentDto>(result.GetRawText());
            if(confirmed==null||confirmed.WorkId!=consumptionWorkId||confirmed.RoomId!=w.RoomId||!confirmed.Confirmed
                ||confirmed.Lines.Count==0||confirmed.Lines.Any(l=>l.RequiredQuantity!=0))return false;
        }
        var timezone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branchId&&b.IsActive).Select(b=>b.Timezone).SingleAsync(ct);
        var shifts=await (from day in db.WorkdayAssignments.AsNoTracking()
            join shift in db.WorkdayShifts on day.ShiftId equals shift.ShiftId
            join role in db.Roles on shift.RoleId equals role.RoleId
            where day.UserId==actor.Id&&shift.BranchId==branchId&&shift.IsActive&&role.Code=="HK_STAFF"&&role.IsActive
                &&day.StartedAt!=null&&day.StartedAt<=started&&(day.EndedAt==null||day.EndedAt>=started)
                &&day.Status=="WORKING"&&day.EndedAt==null
                &&db.Departments.Any(d=>d.DepartmentId==shift.DepartmentId&&d.BranchId==branchId&&d.IsActive)
                &&db.Memberships.Any(m=>m.UserId==actor.Id&&m.BranchId==branchId&&m.Kind=="EMPLOYMENT"&&m.DepartmentId==shift.DepartmentId&&m.ValidFrom<=now&&(m.ValidTo==null||m.ValidTo>now))
            select new {day.WorkDate,shift.StartMinute,shift.EndMinute}).ToArrayAsync(ct);
        return shifts.Any(s=>ShiftCommandAccess.Covers(s.WorkDate,s.StartMinute,s.EndMinute,timezone,started.Value)
            &&!ShiftCommandAccess.Covers(s.WorkDate,s.StartMinute,s.EndMinute,timezone,now));
    }

    public static bool IsBeforeAcceptance(WorkItem work,WorkAssignment? assignment,bool hasStarted) =>
        work.Kind is "CLEANING" or "REWORK" && work.Status is "ASSIGNED" or "PENDING_ACCEPTANCE"
        &&assignment is {AssignmentStatus:"ACTIVE",AcceptedAt:null}&&work.ActualStartAt==null&&!hasStarted;

    public static Task<bool> HasPendingHandover(HotelOpsCompactDbContext db,long workId,CancellationToken ct) =>
        db.WorkEvents.AsNoTracking().AnyAsync(e=>e.WorkId==workId&&e.EventType=="SUPERVISOR_HANDOVER_REQUESTED"
            &&db.WorkAssignments.Any(a=>a.WorkId==workId&&a.AssignmentStatus=="ACTIVE"&&a.AssigneeUserId==e.ActorUserId&&a.AssignedAt<=e.OccurredAt)
            &&!db.WorkEvents.Any(d=>d.WorkId==workId&&d.RelatedEventId==e.WorkEventId&&d.EventType=="SUPERVISOR_HANDOVER_ACCEPTED"),ct);

    public static async Task<bool> CanRequestHandover(HotelOpsCompactDbContext db,Actor actor,TimeProvider clock,long workId,long branch,CancellationToken ct)
    {
        var access=new Access(db,actor,clock);
        if(!await access.HasRole("HK_STAFF",branch,ct)||!await access.Allows("CLEANING_EXECUTE",branch,true,ct:ct))return false;
        var work=await db.WorkItems.AsNoTracking().SingleOrDefaultAsync(w=>w.WorkId==workId&&w.BranchId==branch,ct);
        var assignment=await db.WorkAssignments.AsNoTracking().SingleOrDefaultAsync(a=>a.WorkId==workId&&a.BranchId==branch&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE",ct);
        return work!=null&&IsBeforeAcceptance(work,assignment,await db.WorkIntervals.AnyAsync(t=>t.WorkId==workId,ct)
                ||await db.WorkEvents.AnyAsync(e=>e.WorkId==workId&&e.EventType=="START",ct))
            &&!await db.WorkEvents.AnyAsync(e=>e.WorkId==workId&&e.EventType=="SUPERVISOR_HANDOVER_REQUESTED"
                &&e.ActorUserId==assignment!.AssigneeUserId&&e.OccurredAt>=assignment.AssignedAt,ct);
    }
}
