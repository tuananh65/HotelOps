using HotelOps.Api.Compact.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    public async Task<object> Personal(long branchId,CancellationToken ct,bool maintenance=false)
    {
        if(maintenance)await access.Require("MAINTENANCE_VIEW",branchId,true,ct);
        var kinds=new List<string>();
        foreach(var kind in maintenance?new[]{"MAINTENANCE"}:new[]{"CLEANING","REWORK","OPERATIONAL"})
            if(await access.Allows(WorkRules.Permission(kind,"read"),branchId,true,ct:ct))kinds.Add(kind);
        var attendance=await access.Allows("ATTENDANCE_RECORD",branchId,true,ct:ct);
        Output.Require(kinds.Count>0||attendance,"PERMISSION_DENIED",403);
        var zone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==branchId).Select(b=>b.Timezone).SingleAsync(ct);
        var now=clock.GetUtcNow().UtcDateTime;
        var today=WorkdayService.LocalDate(now,zone);
        var timezone=TimeZoneInfo.FindSystemTimeZoneById(zone);
        var start=TimeZoneInfo.ConvertTimeToUtc(today.ToDateTime(TimeOnly.MinValue),timezone);
        var end=TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1).ToDateTime(TimeOnly.MinValue),timezone);
        // Finished assignments remain personal history; reassigned work is not another employee's achievement.
        var own=db.WorkItems.AsNoTracking().Where(w=>w.BranchId==branchId&&kinds.Contains(w.Kind)
            &&db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&(a.AssignmentStatus=="ACTIVE"||(a.AssignmentStatus=="COMPLETED"&&(!maintenance||w.Status=="CLOSED"||w.Status=="COMPLETED")))));
        var active=own.Where(w=>!WorkRules.Terminal.Contains(w.Status));
        var priority=await (from w in active join room in db.Rooms on w.RoomId equals room.RoomId into rooms
            from room in rooms.DefaultIfEmpty()
            orderby w.DueAt==null,w.DueAt,w.WorkId
            select new{w.WorkId,w.Kind,w.Title,w.Status,w.DueAt,room=room==null?null:room.Code})
            .Take(10).ToArrayAsync(ct);
        object? shifts=null;
        if(attendance)shifts=await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where a.UserId==actor.Id&&s.BranchId==branchId&&(a.WorkDate==today||a.Status=="WORKING"||(a.WorkDate>today&&a.WorkDate<=today.AddDays(7)&&a.Status=="PLANNED"))
            orderby a.WorkDate,s.StartMinute
            select new{a.AssignmentId,a.WorkDate,a.Status,a.StartedAt,a.EndedAt,s.Name,s.StartMinute,s.EndMinute}).ToArrayAsync(ct);
        return new{branchId,today,timezone=zone,metrics=new{
            assignedToday=await own.CountAsync(w=>db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignedAt>=start&&a.AssignedAt<end&&(a.AssignmentStatus=="ACTIVE"||a.AssignmentStatus=="COMPLETED")),ct),
            awaitingAcceptance=await active.CountAsync(w=>db.WorkAssignments.Any(a=>a.WorkId==w.WorkId&&a.AssigneeUserId==actor.Id&&a.AssignmentStatus=="ACTIVE"&&a.AcceptedAt==null),ct),
            inProgress=await active.CountAsync(w=>w.Status=="IN_PROGRESS",ct),
            completedToday=await own.CountAsync(w=>w.CompletedAt>=start&&w.CompletedAt<end,ct),
            pendingInspection=await active.CountAsync(w=>w.Status=="PENDING_INSPECTION"||w.Status=="PENDING_REINSPECTION",ct),
            rework=await active.CountAsync(w=>w.Kind=="REWORK",ct),
            pendingReview=maintenance?(int?)await own.CountAsync(w=>w.Status=="COMPLETED",ct):null,
            accepted=maintenance?(int?)await own.CountAsync(w=>w.Status=="CLOSED"&&w.VerifiedAt!=null,ct):null,
            maintenanceRework=maintenance?(int?)await active.CountAsync(w=>db.WorkEvents.Any(e=>e.WorkId==w.WorkId&&e.EventType=="REOPEN"),ct):null,
            reportedIncidents=maintenance&&await access.Allows("INCIDENT_VIEW",branchId,true,ct:ct)?(int?)await db.WorkItems.CountAsync(w=>w.BranchId==branchId&&w.Kind=="INCIDENT"&&w.CreatedBy==actor.Id,ct):null,
            overdue=await active.CountAsync(w=>w.DueAt<now&&w.Status!="PENDING_INSPECTION"&&w.Status!="PENDING_REINSPECTION",ct),
            unreadNotifications=await notifications.UnreadCount(branchId,ct)},priority,shifts};
    }
}
