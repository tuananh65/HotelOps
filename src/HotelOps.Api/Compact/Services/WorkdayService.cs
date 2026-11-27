using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public sealed record AssignShiftRequest(long ShiftId,long UserId,DateOnly WorkDate,string RowVersion,Guid OperationKey,string? HoursAcknowledgement=null);
public sealed partial class WorkdayService(HotelOpsCompactDbContext db,Access access,Actor actor,Atomic atomic,Audit audit,TimeProvider clock,ShiftExpiryService expiry,StaffSupplyService supplies)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public static DateOnly LocalDate(DateTimeOffset now,string timezone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(timezone)).DateTime);
    private async Task<DateOnly> Today(long branch,CancellationToken ct) => LocalDate(clock.GetUtcNow(),await db.Branches.Where(b=>b.BranchId==branch).Select(b=>b.Timezone).SingleAsync(ct));
    private async Task<bool> ReadScope(long branch,CancellationToken ct)
    {
        var manages=await access.Allows("SHIFT_MANAGE",branch,ct:ct);
        if(!manages)await access.Require("ATTENDANCE_RECORD",branch,true,ct);
        return manages;
    }
    private async Task RequireShiftNotEnded(WorkdayShift shift, IEnumerable<DateOnly> dates, CancellationToken ct)
    {
        var zone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==shift.BranchId).Select(b=>b.Timezone).SingleAsync(ct);
        var local=TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById(zone));
        Output.Require(dates.All(date=>date.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.EndMinute)>local.DateTime),"SHIFT_ALREADY_ENDED",409);
    }
    public async Task<object> Week(long branch,DateOnly date,CancellationToken ct)
    {
        var manages=await ReadScope(branch,ct);
        Output.Require(date.Year is >=2000 and <=2100,"INVALID_WORKDAY_DATE",400);
        await expiry.Synchronize(ct);
        var start=date.AddDays(-(((int)date.DayOfWeek+6)%7));var end=start.AddDays(7);
        var shifts=await (from s in db.WorkdayShifts.AsNoTracking() join d in db.Departments on s.DepartmentId equals d.DepartmentId
            join r in db.Roles on s.RoleId equals r.RoleId where s.BranchId==branch
            orderby d.Name,s.RoleId,s.StartMinute select new {s.ShiftId,s.DepartmentId,department=d.Name,s.RoleId,role=r.Name,s.Name,s.StartMinute,s.EndMinute,s.Capacity,s.IsActive,s.RowVersion}).ToArrayAsync(ct);
        var assignments=await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            join u in db.Users on a.UserId equals u.UserId where s.BranchId==branch && a.WorkDate>=start && a.WorkDate<end && (manages||a.UserId==actor.Id)
            orderby a.WorkDate,s.StartMinute,u.FullName select new {a.AssignmentId,a.ShiftId,a.UserId,u.FullName,a.WorkDate,a.Status,a.StartedAt,a.EndedAt,a.RowVersion,
                expiredWithoutStart=a.Status=="CANCELLED"&&db.AuditLogs.Any(log=>log.EntityType=="Workday"&&log.EntityKey==a.AssignmentId.ToString()
                    &&log.BranchId==branch&&log.ActorUserId==null&&log.Action==ShiftExpiryService.ExpiredWithoutStartAction)}).ToArrayAsync(ct);
        // Expired attendance remains actionable only for outstanding stock reconciliation.
        var activeRows=await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where a.UserId==actor.Id && s.BranchId==branch && (a.Status=="WORKING" || (a.Status=="ENDED"&&db.StaffSupplyAllocations.Any(x=>x.AssignmentId==a.AssignmentId&&x.Status!="CLOSED")))
            orderby a.WorkDate,a.AssignmentId
            select new {a.AssignmentId,a.ShiftId,s.BranchId,s.Name,a.WorkDate,a.Status,a.StartedAt,a.EndedAt,a.RowVersion}).ToArrayAsync(ct);
        object? active=null;
        var pendingReconciliations=new List<object>();
        foreach(var row in activeRows){
            var endBlockedReason=await supplies.EndBlocker(row.AssignmentId,ct);
            var returnScheduled=await (from allocation in db.StaffSupplyAllocations.AsNoTracking()
                join log in db.AuditLogs on allocation.AllocationId.ToString() equals log.EntityKey
                where allocation.AssignmentId==row.AssignmentId&&log.EntityType=="StaffSupplyAllocation"&&log.Action=="RETURN_SCHEDULED"
                orderby log.AuditId select (DateTime?)log.OccurredAt).FirstOrDefaultAsync(ct);
            var view=new {row.AssignmentId,row.ShiftId,row.BranchId,row.Name,row.WorkDate,row.Status,row.StartedAt,row.EndedAt,row.RowVersion,returnDueAt=returnScheduled?.AddMinutes(5),canEnd=row.Status=="WORKING"&&endBlockedReason==null,endBlockedReason};
            if(row.Status=="WORKING")active=view;else pendingReconciliations.Add(view);
        }
        var zone=await db.Branches.Where(b=>b.BranchId==branch).Select(b=>b.Timezone).SingleAsync(ct);
        var local=TimeZoneInfo.ConvertTime(clock.GetUtcNow(),TimeZoneInfo.FindSystemTimeZoneById(zone));
        return new {weekStart=start,today=DateOnly.FromDateTime(local.DateTime),currentMinute=local.Hour*60+local.Minute,timezone=zone,canManage=manages,shifts,assignments,active,pendingReconciliations};
    }
    public async Task<object> Candidates(long shiftId,DateOnly date,CancellationToken ct)
    {
        var shift=await db.WorkdayShifts.AsNoTracking().SingleOrDefaultAsync(s=>s.ShiftId==shiftId,ct)??throw new ApiException(404,"SHIFT_NOT_FOUND");
        await access.Require("SHIFT_MANAGE",shift.BranchId,ct:ct);
        Output.Require(date.Year is >=2000 and <=2100,"INVALID_WORKDAY_DATE",400);
        Output.Require(shift.IsActive,"SHIFT_INACTIVE");
        await RequireShiftNotEnded(shift,[date],ct);
        var users=await (await EligibleQuery(shift,date,ct)).OrderBy(u=>u.FullName).Select(u=>new {u.UserId,u.EmployeeCode,u.FullName,u.Phone}).ToArrayAsync(ct);
        var busy=await (from a in db.WorkdayAssignments.AsNoTracking() join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId
            where a.WorkDate==date && a.Status!="CANCELLED" && s.StartMinute<shift.EndMinute && s.EndMinute>shift.StartMinute select a.UserId).ToArrayAsync(ct);
        return users.Where(u=>!busy.Contains(u.UserId)).ToArray();
    }
    private async Task<IQueryable<User>> EligibleQuery(WorkdayShift shift,DateOnly date,CancellationToken ct)
    {
        // A future shift needs full coverage. For a shift already in progress,
        // eligibility covers only its remaining time; attendance starts at Now,
        // never retroactively at the planned start (e.g. a role granted mid-shift).
        var zone=await db.Branches.AsNoTracking().Where(b=>b.BranchId==shift.BranchId).Select(b=>b.Timezone).SingleAsync(ct);
        var start=TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.StartMinute),TimeZoneInfo.FindSystemTimeZoneById(zone));
        var end=TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue).AddMinutes(shift.EndMinute),TimeZoneInfo.FindSystemTimeZoneById(zone));
        var now=Now;
        if(start<now)start=now;
        return db.Users.AsNoTracking().Where(u=>u.AccountStatus=="ACTIVE"&&u.EmploymentStatus=="ACTIVE"&&u.DeletedAt==null
            && db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==shift.BranchId&&m.Kind=="BRANCH_ACCESS"&&m.ValidFrom<=start&&(m.ValidTo==null||m.ValidTo>=end))
            && db.Memberships.Any(m=>m.UserId==u.UserId&&m.BranchId==shift.BranchId&&m.DepartmentId==shift.DepartmentId&&m.Kind=="EMPLOYMENT"&&m.ValidFrom<=start&&(m.ValidTo==null||m.ValidTo>=end))
            && db.UserRoles.Any(ur=>ur.UserId==u.UserId&&ur.RoleId==shift.RoleId&&ur.BranchId==shift.BranchId&&ur.AssignedAt<=start&&ur.RevokedAt==null&&(ur.ExpiresAt==null||ur.ExpiresAt>=end))
            && db.Roles.Any(r=>r.RoleId==shift.RoleId&&r.IsActive)
            && db.Departments.Any(d=>d.DepartmentId==shift.DepartmentId&&d.BranchId==shift.BranchId&&d.IsActive));
    }
    private Task Lock(CancellationToken ct) => db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps.Workday',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51410,'Workday lock unavailable.',1;",ct);
    public Task<object> Assign(AssignShiftRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        await Lock(ct);Output.Key(request.OperationKey);
        var shift=await db.WorkdayShifts.SingleOrDefaultAsync(s=>s.ShiftId==request.ShiftId,ct)??throw new ApiException(404,"SHIFT_NOT_FOUND");
        await access.Require("SHIFT_MANAGE",shift.BranchId,ct:ct);Output.Version(shift.RowVersion,request.RowVersion);
        var today=await Today(shift.BranchId,ct);
        Output.Require(request.WorkDate>=today&&request.WorkDate<=today.AddDays(366),"SHIFT_DATE_OUT_OF_RANGE",400);
        await RequireShiftNotEnded(shift,[request.WorkDate],ct);
        Output.Require(shift.IsActive,"SHIFT_INACTIVE");
        Output.Require(await (await EligibleQuery(shift,request.WorkDate,ct)).AnyAsync(u=>u.UserId==request.UserId,ct),"SHIFT_EMPLOYEE_NOT_ELIGIBLE",409);
        await access.EligibleFor(request.UserId,shift.BranchId,"ATTENDANCE_RECORD",ct);
        Output.Require(await db.WorkdayAssignments.CountAsync(a=>a.ShiftId==shift.ShiftId&&a.WorkDate==request.WorkDate&&a.Status!="CANCELLED",ct)<shift.Capacity,"SHIFT_CAPACITY_REACHED");
        Output.Require(!await (from a in db.WorkdayAssignments join s in db.WorkdayShifts on a.ShiftId equals s.ShiftId where a.UserId==request.UserId&&a.WorkDate==request.WorkDate&&a.Status!="CANCELLED"&&s.StartMinute<shift.EndMinute&&s.EndMinute>shift.StartMinute select a.AssignmentId).AnyAsync(ct),"SHIFT_OVERLAP");
        var hours=(await HoursReviews(shift,[request.WorkDate],[request.UserId],ct))[request.UserId];
        ConfirmHours(hours,request.HoursAcknowledgement);
        var row=new WorkdayAssignment{ShiftId=shift.ShiftId,UserId=request.UserId,WorkDate=request.WorkDate,CreatedBy=actor.Id,CreatedAt=Now};
        db.WorkdayAssignments.Add(row);await db.SaveChangesAsync(ct);
        Event(row,"ASSIGN",request.OperationKey,HoursReason(hours, row.WorkDate));audit.Add("Workday",row.AssignmentId,"ASSIGN",shift.BranchId,null,
            hours.Warnings.Length==0?(object)Output.Record(row):new{assignment=Output.Record(row),hoursOverride=hours.Warnings},request.OperationKey);
        return (object)Output.Record(row);
    },ct);
    public Task<object> Action(long id,string command,ActionRequest request,CancellationToken ct)=>atomic.Run(async()=>{
        Output.Require(command is "end" or "cancel","UNKNOWN_SHIFT_ACTION",400);
        await Lock(ct);Output.Key(request.OperationKey);
        // Match expiry lock order before reading attendance rows also read by refill.
        await StaffSupplyService.Lock(db,ct);
        var row=await db.WorkdayAssignments.SingleOrDefaultAsync(a=>a.AssignmentId==id,ct)??throw new ApiException(404,"SHIFT_ASSIGNMENT_NOT_FOUND");
        var shift=await db.WorkdayShifts.SingleAsync(s=>s.ShiftId==row.ShiftId,ct);
        if(command=="cancel")await access.Require("SHIFT_MANAGE",shift.BranchId,ct:ct);
        else {await access.Require("ATTENDANCE_RECORD",shift.BranchId,true,ct);Output.Require(row.UserId==actor.Id,"OWN_SHIFT_REQUIRED",403);}
        var previous=await db.WorkdayEvents.AsNoTracking().SingleOrDefaultAsync(e=>e.AssignmentId==id&&e.ActorUserId==actor.Id&&e.OperationKey==request.OperationKey,ct);
        if(previous!=null){Output.Require(previous.Action==command.ToUpperInvariant(),"OPERATION_KEY_REUSED");return (object)Output.Record(row);}
        Output.Version(row.RowVersion,request.RowVersion);var before=Output.Record(row);
        if(command=="end"){
            Output.Require(row.Status=="WORKING","SHIFT_NOT_WORKING");
            await supplies.End(id,request.OperationKey,ct);
            row.Status="ENDED";row.EndedAt=Now;
        }else if(command=="cancel"){
            Output.Require(row.Status=="PLANNED","ONLY_PLANNED_SHIFT_CAN_CANCEL");Output.Reason(request.Reason);row.Status="CANCELLED";
        }else throw new ApiException(400,"UNKNOWN_SHIFT_ACTION");
        Event(row,command.ToUpperInvariant(),request.OperationKey,request.Reason);
        audit.Add("Workday",id,command.ToUpperInvariant(),shift.BranchId,before,Output.Record(row),request.OperationKey);
        await db.SaveChangesAsync(ct);return (object)Output.Record(row);
    },ct);
    private void Event(WorkdayAssignment row,string action,Guid key,string? reason)=>db.WorkdayEvents.Add(new(){AssignmentId=row.AssignmentId,ActorUserId=actor.Id,Action=action,OperationKey=key,Reason=reason,OccurredAt=Now});
    public async Task<object> History(long id,CancellationToken ct)
    {
        var row=await db.WorkdayAssignments.AsNoTracking().SingleOrDefaultAsync(a=>a.AssignmentId==id,ct)??throw new ApiException(404,"SHIFT_ASSIGNMENT_NOT_FOUND");
        var branch=await db.WorkdayShifts.Where(s=>s.ShiftId==row.ShiftId).Select(s=>s.BranchId).SingleAsync(ct);
        var manages=await ReadScope(branch,ct);Output.Require(manages||row.UserId==actor.Id,"OWN_SHIFT_REQUIRED",403);
        var manual=await (from e in db.WorkdayEvents.AsNoTracking() join u in db.Users on e.ActorUserId equals u.UserId where e.AssignmentId==id
            select new ShiftHistory(e.EventId,e.Action,e.OccurredAt,e.Reason,u.FullName)).ToArrayAsync(ct);
        var key=id.ToString();
        var automatic=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="Workday"&&a.EntityKey==key&&a.BranchId==branch
            &&(a.Action=="AUTO_END"||a.Action==ShiftExpiryService.AutomaticStartAction||a.Action==ShiftExpiryService.ExpiredWithoutStartAction)&&a.ActorUserId==null)
            .Select(a=>new ShiftHistory(-a.AuditId,a.Action,a.OccurredAt,
                a.Action==ShiftExpiryService.ExpiredWithoutStartAction
                    ?"Hệ thống đóng ca chưa bắt đầu vì đã quá giờ kết thúc 1 giờ; không ghi nhận thời gian làm việc."
                    :a.Action==ShiftExpiryService.AutomaticStartAction?"Hệ thống tự kích hoạt ca trong giờ được phân; không cần thao tác bắt đầu ca."
                    :"Hệ thống tự động kết thúc ca khi hết giờ làm việc.","Hệ thống")).ToArrayAsync(ct);
        return manual.Concat(automatic).OrderBy(h=>h.OccurredAt).ToArray();
    }
    private sealed record ShiftHistory(long EventId,string Action,DateTime OccurredAt,string? Reason,string Actor);
}
