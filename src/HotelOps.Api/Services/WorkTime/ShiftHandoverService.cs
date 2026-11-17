using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed record ShiftHandoverReceiver(long UserId,string Name,long AssignmentId,DateTime Start,DateTime End);
public sealed record ShiftHandoverPreview(long WorkdayId,long BranchId,long DepartmentId,string Version,ShiftHandoverItem[] Items,ShiftHandoverReceiver[] Receivers);
public sealed record ShiftHandoverSubmit(Guid RequestKey,long ReceiverAssignmentId,long? PreviousId,string? GeneralNote,ShiftHandoverItemNote[] Notes);
public sealed record ShiftHandoverItemNote(string Kind,long Id,string? Note);
public sealed record ShiftHandoverDecision(bool Acknowledge,string? Reason);
public sealed record ShiftHandoverView(long Id,long WorkdayId,long BranchId,long DepartmentId,long SenderId,long ReceiverId,long? PreviousId,
    string State,string Version,DateTime SubmittedAt,DateTime? DecisionAt,string? Reason,string? GeneralNote,ShiftHandoverItem[] Items,int RestrictedItems,bool CanDecide);
public sealed record ShiftHandoverSummary(long Id,long WorkdayId,long SenderId,long ReceiverId,string State,DateTime SubmittedAt);

public sealed partial class ShiftHandoverService(HotelOpsDbContext db,ISqlSessionService sessions,ICurrentUserAccessor current)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private async Task<DateTime> Now(CancellationToken ct)=>DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
    private async Task Session(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.UserId!=ticket.UserId||valid.Session.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
    }
    private async Task Schema(CancellationToken ct)
    {
        if(!await ShiftHandoverStore.InstalledAsync(db,ct)||!await WorkdayMembershipStore.InstalledAsync(db,ct)
            ||!await new InternalRequestProcessAuthority(db).InstalledAsync(ct))
            throw new InternalRequestReadException(503,"SHIFT_HANDOVER_STORAGE_REQUIRED");
    }
    private async Task<Workday> Working(long user,long day,CancellationToken ct)
    {
        var row=await db.Workdays.FromSqlInterpolated($"SELECT * FROM dbo.workdays WITH(UPDLOCK,HOLDLOCK) WHERE workday_id={day}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if(row==null||row.UserId!=user)throw new InternalRequestReadException(404,"WORKDAY_UNAVAILABLE");
        var now=await Now(ct);
        if(row.Status!="WORKING"||row.EndedAt!=null||row.StartedAt>now)throw new InternalRequestReadException(409,"WORKING_WORKDAY_REQUIRED");
        if(!await WorkdayMembershipStore.Employees(db,row.BranchId,row.DepartmentId,now).AnyAsync(u=>u.UserId==user,ct)
            ||!await ShiftHandoverAccess.FeatureAsync(db,user,row.BranchId,"SHIFT_HANDOVER_SUBMIT",true,now,ct)
            ||!await ShiftHandoverAccess.FeatureAsync(db,user,row.BranchId,"SHIFT_HANDOVER_VIEW",true,now,ct))
            throw new InternalRequestReadException(403,"SHIFT_HANDOVER_SUBMIT_REQUIRED");
        return row;
    }
    private async Task<ShiftHandoverReceiver[]> Receivers(Workday day,DateTime now,CancellationToken ct)
    {
        var anchor=day.StartedAt;
        if(day.ShiftAssignmentId is {} assigned)
        {
            var origin=await db.ShiftAssignments.AsNoTracking().Where(a=>a.ShiftAssignmentId==assigned&&a.UserId==day.UserId
                &&a.Shift.BranchId==day.BranchId&&a.Shift.DepartmentId==day.DepartmentId&&a.AssignmentStatus=="ASSIGNED")
                .Select(a=>(DateTime?)a.Shift.ScheduledStartAt).SingleOrDefaultAsync(ct);
            if(origin==null)throw new InternalRequestReadException(409,"SENDER_SHIFT_CHANGED");
            anchor=origin.Value;
        }
        var next=await db.WorkShifts.AsNoTracking().Where(s=>s.BranchId==day.BranchId&&s.DepartmentId==day.DepartmentId&&s.Status=="PLANNED"
            &&s.ScheduledStartAt>anchor&&s.ScheduledEndAt>now&&s.ScheduledEndAt>s.ScheduledStartAt)
            .Select(s=>(DateTime?)s.ScheduledStartAt).MinAsync(ct);
        if(next==null)return [];
        var candidates=await db.ShiftAssignments.AsNoTracking().Where(a=>a.AssignmentStatus=="ASSIGNED"&&a.AssignedAt<=now&&a.UserId!=day.UserId
            &&a.Shift.BranchId==day.BranchId&&a.Shift.DepartmentId==day.DepartmentId&&a.Shift.Status=="PLANNED"
            &&a.Shift.ScheduledStartAt==next&&a.Shift.ScheduledEndAt>now)
            .Select(a=>new ShiftHandoverReceiver(a.UserId,a.User.FullName,a.ShiftAssignmentId,a.Shift.ScheduledStartAt,a.Shift.ScheduledEndAt)).ToArrayAsync(ct);
        var result=new List<ShiftHandoverReceiver>();
        foreach(var person in candidates)
            if(await WorkdayMembershipStore.Employees(db,day.BranchId,day.DepartmentId,now).AnyAsync(u=>u.UserId==person.UserId,ct)
                &&await ShiftHandoverAccess.FeatureAsync(db,person.UserId,day.BranchId,"SHIFT_HANDOVER_VIEW",true,now,ct)
                &&await ShiftHandoverAccess.FeatureAsync(db,person.UserId,day.BranchId,"SHIFT_HANDOVER_DECIDE",true,now,ct))result.Add(person);
        return result.OrderBy(r=>r.Name,StringComparer.Ordinal).ThenBy(r=>r.AssignmentId)
            .Select(r=>r with{Start=DateTime.SpecifyKind(r.Start,DateTimeKind.Utc),End=DateTime.SpecifyKind(r.End,DateTimeKind.Utc)}).ToArray();
    }
    private static string Tag(Workday day,ShiftHandoverItem[] items)=>'"'+Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{day.WorkdayId,day.RowVersion,items},Json)))+'"';
    public async Task<ShiftHandoverPreview> PreviewAsync(SessionReference ticket,long sid,long day,CancellationToken ct)
    {
        await Schema(ct);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await Session(ticket,sid,ct);
        var workday=await Working(ticket.UserId,day,ct);var now=await Now(ct);
        var items=await ShiftHandoverSnapshot.CaptureAsync(db,ticket.UserId,workday.BranchId,workday.DepartmentId,now,ct);
        var receivers=await Receivers(workday,now,ct);await Session(ticket,sid,ct);await Working(ticket.UserId,day,ct);
        foreach(var item in items)if(!await ShiftHandoverSnapshot.CanReadSourceAsync(db,ticket.UserId,workday.BranchId,item,await Now(ct),ct))throw new InternalRequestReadException(403,"HANDOVER_SOURCE_AUTHORITY_CHANGED");
        await tx.CommitAsync(ct);return new(day,workday.BranchId,workday.DepartmentId,Tag(workday,items),items,receivers);
    }
    private static string? Note(string? value,bool required=false)
    {
        value=value?.Trim();
        if(value?.Length>4000||value?.Any(c=>char.IsControl(c)&&c is not ('\n' or '\r' or '\t'))==true||required&&value?.Length is not >=10)
            throw new ValidationException("Reason must contain at least 10 characters; notes allow at most 4000 characters.");
        return string.IsNullOrEmpty(value)?null:value;
    }
    private async Task<long> Audit(long actor,long branch,long? id,string operation,object before,object after,DateTime now,CancellationToken ct,string? creationKey=null)
    {
        var audit=new AuditLog{ActorUserId=actor,BranchId=branch,EntityType="operational_shift_handovers",EntityId=id?.ToString(CultureInfo.InvariantCulture)??creationKey??throw new InvalidOperationException("Audit identity required."),
            Action=id==null?"CREATE":"UPDATE",OccurredAt=now,OldValues=JsonSerializer.Serialize(before,Json),NewValues=JsonSerializer.Serialize(after,Json),ChangedFields=JsonSerializer.Serialize(new[]{operation})};
        db.AuditLogs.Add(audit);await db.SaveChangesAsync(ct);db.Entry(audit).State=EntityState.Detached;return audit.AuditId;
    }
    private Task Intent(long audit,long handover,long recipient,string kind,DateTime now,CancellationToken ct)=>db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT dbo.operational_shift_handover_outbox(event_audit_id,handover_id,recipient_id,recipient_kind,created_at)
        SELECT {audit},{handover},{recipient},{kind},{now} WHERE NOT EXISTS(SELECT 1 FROM dbo.operational_shift_handover_outbox WHERE event_audit_id={audit} AND recipient_id={recipient})
        """,ct);
    public async Task<long> SubmitAsync(SessionReference ticket,long sid,long day,string expected,ShiftHandoverSubmit input,CancellationToken ct)
    {
        if(input.RequestKey==Guid.Empty||input.ReceiverAssignmentId<=0||input.PreviousId<=0||input.Notes==null||input.Notes.Length>10000||input.Notes.Any(n=>n==null||n.Id<=0||string.IsNullOrWhiteSpace(n.Kind)))
            throw new ValidationException("Valid request key, receiver and notes required.");
        var general=Note(input.GeneralNote);await Schema(ct);
        var receiver=await db.ShiftAssignments.AsNoTracking().Where(a=>a.ShiftAssignmentId==input.ReceiverAssignmentId).Select(a=>(long?)a.UserId).SingleOrDefaultAsync(ct);
        if(receiver==null)throw new InternalRequestReadException(409,"RECEIVER_UNAVAILABLE");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId,receiver.Value],ct);await Session(ticket,sid,ct);
        var dayRow=await Working(ticket.UserId,day,ct);var now=await Now(ct);
        var prior=await ShiftHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.SenderId==ticket.UserId&&h.RequestKey==input.RequestKey,ct);
        if(prior!=null)
        {
            var stored=JsonSerializer.Deserialize<ShiftHandoverItem[]>(prior.Snapshot,Json)??[];
            var notes=input.Notes.Select(n=>new ShiftHandoverItemNote(n.Kind,n.Id,Note(n.Note))).OrderBy(n=>n.Kind).ThenBy(n=>n.Id);
            if(prior.WorkdayId!=day||prior.ReceiverAssignmentId!=input.ReceiverAssignmentId||prior.PreviousId!=input.PreviousId||prior.GeneralNote!=general
                ||!stored.Select(n=>new ShiftHandoverItemNote(n.Kind,n.Id,n.Note)).OrderBy(n=>n.Kind).ThenBy(n=>n.Id).SequenceEqual(notes))
                throw new InternalRequestReadException(409,"HANDOVER_REQUEST_KEY_REUSED");
            await tx.CommitAsync(ct);return prior.Id;
        }
        var options=await Receivers(dayRow,now,ct);
        if(!options.Any(r=>r.AssignmentId==input.ReceiverAssignmentId&&r.UserId==receiver))throw new InternalRequestReadException(409,"NEXT_SHIFT_RECEIVER_REQUIRED");
        if(input.PreviousId is {} previous)
        {
            if(!await ShiftHandoverStore.Rows(db).AnyAsync(h=>h.Id==previous&&h.SenderId==ticket.UserId&&h.WorkdayId==day&&h.State=="REJECTED",ct))
                throw new InternalRequestReadException(409,"REJECTED_HANDOVER_REQUIRED");
        }
        var snapshot=await ShiftHandoverSnapshot.CaptureAsync(db,ticket.UserId,dayRow.BranchId,dayRow.DepartmentId,now,ct);
        if(Tag(dayRow,snapshot)!=expected)throw new InternalRequestReadException(412,"HANDOVER_SNAPSHOT_CHANGED");
        if(input.Notes.Select(n=>(n.Kind,n.Id)).Distinct().Count()!=input.Notes.Length
            ||input.Notes.Length!=snapshot.Length||input.Notes.Any(n=>!snapshot.Any(i=>i.Kind==n.Kind&&i.Id==n.Id)))
            throw new ValidationException("Notes must match the reviewed snapshot items.");
        snapshot=snapshot.Select(i=>i with{Note=Note(input.Notes.Single(n=>n.Kind==i.Kind&&n.Id==i.Id).Note)}).ToArray();
        var json=JsonSerializer.Serialize(snapshot,Json);
        var audit=await Audit(ticket.UserId,dayRow.BranchId,null,"SUBMIT",new{},new{day,receiver=receiver.Value,input.RequestKey,input.PreviousId,state="PENDING_ACKNOWLEDGMENT"},now,ct,input.RequestKey.ToString("D"));
        var ids=await db.Database.SqlQuery<long>($"""
            DECLARE @created TABLE(Value bigint NOT NULL);
            INSERT dbo.operational_shift_handovers(request_key,workday_id,branch_id,department_id,sender_id,receiver_id,receiver_shift_assignment_id,
              previous_handover_id,snapshot_json,general_note,state,submitted_at,submission_audit_id)
            OUTPUT INSERTED.handover_id INTO @created(Value)
            VALUES({input.RequestKey},{day},{dayRow.BranchId},{dayRow.DepartmentId},{ticket.UserId},{receiver.Value},{input.ReceiverAssignmentId},
              {input.PreviousId},{json},{general},'PENDING_ACKNOWLEDGMENT',{now},{audit});
            SELECT Value FROM @created;
            """).ToArrayAsync(ct);
        var id=ids.Single();await Intent(audit,id,receiver.Value,"RECEIVER",now,ct);
        await Session(ticket,sid,ct);await Working(ticket.UserId,day,ct);
        if(!(await Receivers(dayRow,await Now(ct),ct)).Any(r=>r.UserId==receiver&&r.AssignmentId==input.ReceiverAssignmentId))throw new InternalRequestReadException(409,"RECEIVER_CHANGED");
        foreach(var item in snapshot)if(!await ShiftHandoverSnapshot.CanReadSourceAsync(db,ticket.UserId,dayRow.BranchId,item,await Now(ct),ct))throw new InternalRequestReadException(403,"HANDOVER_SOURCE_AUTHORITY_CHANGED");
        await tx.CommitAsync(ct);return id;
    }
}
