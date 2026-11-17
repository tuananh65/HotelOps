using System.Data;
using System.Text.Json;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.WorkTime;

public sealed partial class ShiftHandoverService
{
    private async Task<bool> CanDecide(long user,ShiftHandoverRow row,DateTime now,CancellationToken ct)
        => row.State=="PENDING_ACKNOWLEDGMENT"&&row.ReceiverId==user
            &&await ShiftHandoverAccess.ReadAsync(db,user,row,now,ct)
            &&await ShiftHandoverAccess.FeatureAsync(db,user,row.BranchId,"SHIFT_HANDOVER_DECIDE",true,now,ct)
            &&await WorkdayMembershipStore.Employees(db,row.BranchId,row.DepartmentId,now).AnyAsync(u=>u.UserId==user,ct)
            &&await db.ShiftAssignments.AsNoTracking().AnyAsync(a=>a.ShiftAssignmentId==row.ReceiverAssignmentId&&a.UserId==user
                &&a.AssignmentStatus=="ASSIGNED"&&a.AssignedAt<=now&&a.Shift.BranchId==row.BranchId&&a.Shift.DepartmentId==row.DepartmentId
                &&a.Shift.Status!="CANCELLED",ct);

    public async Task<ShiftHandoverView> ReadAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        await Schema(ct);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await Session(ticket,sid,ct);
        var row=await ShiftHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.Id==id,ct);
        var now=await Now(ct);
        if(row==null||!await ShiftHandoverAccess.ReadAsync(db,ticket.UserId,row,now,ct))throw new InternalRequestReadException(404,"HANDOVER_UNAVAILABLE");
        var snapshot=JsonSerializer.Deserialize<ShiftHandoverItem[]>(row.Snapshot,Json)??throw new InternalRequestReadException(503,"HANDOVER_SNAPSHOT_INVALID");
        var visible=new List<ShiftHandoverItem>();
        foreach(var item in snapshot)
            if(await ShiftHandoverSnapshot.CanReadSourceAsync(db,ticket.UserId,row.BranchId,item,now,ct))visible.Add(item);
        var canDecide=await CanDecide(ticket.UserId,row,now,ct);
        await Session(ticket,sid,ct);
        if(!await ShiftHandoverAccess.ReadAsync(db,ticket.UserId,row,await Now(ct),ct))throw new InternalRequestReadException(404,"HANDOVER_UNAVAILABLE");
        foreach(var item in visible)if(!await ShiftHandoverSnapshot.CanReadSourceAsync(db,ticket.UserId,row.BranchId,item,await Now(ct),ct))throw new InternalRequestReadException(403,"HANDOVER_SOURCE_AUTHORITY_CHANGED");
        await tx.CommitAsync(ct);
        return new(row.Id,row.WorkdayId,row.BranchId,row.DepartmentId,row.SenderId,row.ReceiverId,row.PreviousId,row.State,
            '"'+Convert.ToBase64String(row.Version)+'"',DateTime.SpecifyKind(row.SubmittedAt,DateTimeKind.Utc),row.DecisionAt is {} decision?DateTime.SpecifyKind(decision,DateTimeKind.Utc):null,row.Reason,row.GeneralNote,visible.ToArray(),snapshot.Length-visible.Count,canDecide);
    }
    public async Task<ShiftHandoverSummary[]> ListAsync(SessionReference ticket,long sid,long branch,int page,CancellationToken ct)
    {
        if(branch<=0||page<1||page>100000)throw new InternalRequestReadException(400,"INVALID_FILTERS");
        await Schema(ct);await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await Session(ticket,sid,ct);var now=await Now(ct);
        if(!await ShiftHandoverAccess.FeatureAsync(db,ticket.UserId,branch,"SHIFT_HANDOVER_VIEW",true,now,ct))throw new InternalRequestReadException(403,"SHIFT_HANDOVER_VIEW_REQUIRED");
        var departments=await db.Departments.AsNoTracking().Where(d=>d.BranchId==branch&&d.IsActive).Select(d=>d.DepartmentId).ToArrayAsync(ct);
        var managed=new List<long>();
        foreach(var department in departments)if(await ShiftHandoverAccess.HeadAsync(db,ticket.UserId,branch,department,now,ct))managed.Add(department);
        var rows=await ShiftHandoverStore.Rows(db).Where(h=>h.BranchId==branch&&(h.SenderId==ticket.UserId||h.ReceiverId==ticket.UserId||managed.Contains(h.DepartmentId)))
            .OrderByDescending(h=>h.Id).Skip((page-1)*25).Take(25).ToArrayAsync(ct);
        var result=new List<ShiftHandoverSummary>();
        foreach(var row in rows)if(await ShiftHandoverAccess.ReadAsync(db,ticket.UserId,row,await Now(ct),ct))result.Add(new(row.Id,row.WorkdayId,row.SenderId,row.ReceiverId,row.State,DateTime.SpecifyKind(row.SubmittedAt,DateTimeKind.Utc)));
        await Session(ticket,sid,ct);await tx.CommitAsync(ct);return result.ToArray();
    }
    public async Task DecideAsync(SessionReference ticket,long sid,long id,byte[] expected,ShiftHandoverDecision input,CancellationToken ct)
    {
        var reason=input.Acknowledge?null:Note(input.Reason,true);await Schema(ct);
        var hint=await ShiftHandoverStore.Rows(db).SingleOrDefaultAsync(h=>h.Id==id,ct);
        if(hint==null)throw new InternalRequestReadException(404,"HANDOVER_UNAVAILABLE");
        var headHint=await db.Departments.AsNoTracking().Where(d=>d.DepartmentId==hint.DepartmentId&&d.BranchId==hint.BranchId).Select(d=>d.ManagerUserId).SingleOrDefaultAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync(new[]{ticket.UserId,hint.SenderId,hint.ReceiverId}.Concat(headHint is {} h?[h]:Array.Empty<long>()),ct);
        await Session(ticket,sid,ct);
        await db.Database.SqlQuery<long>($"SELECT handover_id AS Value FROM dbo.operational_shift_handovers WITH(UPDLOCK,HOLDLOCK) WHERE handover_id={id}").ToArrayAsync(ct);
        var row=await ShiftHandoverStore.Rows(db).SingleAsync(h=>h.Id==id,ct);var now=await Now(ct);
        if(!row.Version.SequenceEqual(expected))throw new InternalRequestReadException(412,"HANDOVER_VERSION_CHANGED");
        if(!await CanDecide(ticket.UserId,row,now,ct))throw new InternalRequestReadException(403,"HANDOVER_RECEIVER_DECISION_REQUIRED");
        var state=input.Acknowledge?"ACKNOWLEDGED":"REJECTED";
        var audit=await Audit(ticket.UserId,row.BranchId,id,state,new{state=row.State},new{state,reason,receiver=ticket.UserId},now,ct);
        if(await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.operational_shift_handovers SET state={state},decision_at={now},decision_audit_id={audit},rejection_reason={reason}
            WHERE handover_id={id} AND row_version={expected} AND state='PENDING_ACKNOWLEDGMENT' AND receiver_id={ticket.UserId}
            """,ct)!=1)throw new InternalRequestReadException(412,"HANDOVER_VERSION_CHANGED");
        await Intent(audit,id,row.SenderId,"SENDER",now,ct);
        if(input.Acknowledge)
        {
            var head=await db.Departments.AsNoTracking().Where(d=>d.DepartmentId==row.DepartmentId&&d.BranchId==row.BranchId&&d.IsActive).Select(d=>d.ManagerUserId).SingleOrDefaultAsync(ct);
            if(head!=headHint)throw new InternalRequestReadException(409,"DEPARTMENT_HEAD_CHANGED");
            if(head is {} manager)await Intent(audit,id,manager,"HEAD",now,ct);
        }
        await Session(ticket,sid,ct);
        // Revalidate the pre-decision receipt, so an expired grant cannot commit a decision.
        if(!await CanDecide(ticket.UserId,row,await Now(ct),ct))throw new InternalRequestReadException(403,"HANDOVER_AUTHORITY_CHANGED");
        await tx.CommitAsync(ct);
    }
}
