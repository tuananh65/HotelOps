using HotelOps.Api.Data;
using HotelOps.Api.Services.InternalRequests;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

internal static class InternalRequestHandoverResponsibilities
{
    internal static async Task<UserResponsibilityAssessment> ReadAsync(HotelOpsDbContext db,long target,CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(target);
        if(db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Handover review requires the owner transaction and retained user locks.");
        if(!await InternalRequestHandoverStore.InstalledAsync(db,ct))
            return new([], ["INTERNAL_REQUEST_HANDOVER_STORAGE_REQUIRED"]);
        // Retain ranges even when the owner uses ReadCommitted. A receiver is responsible
        // before becoming the handler; assignment-only queries cannot discover that obligation.
        var rows=await db.Database.SqlQuery<PendingHandover>($"""
            SELECT h.handover_id AS Id,h.receiver_id AS Receiver,h.created_at AS Created,
              h.request_id AS RequestId,h.branch_id AS Branch,h.department_id AS Department,
              a.assignee_id AS Sender,a.internal_request_id AS SenderRequest,a.status AS SenderStatus,
              a.assigned_at AS Assigned,a.ended_at AS Ended,
              r.branch_id AS CurrentBranch,r.receiving_department_id AS CurrentDepartment,r.status AS RequestStatus,
              e.internal_request_id AS EventRequest,e.actor_id AS EventActor,e.event_type AS EventType,e.occurred_at AS EventTime,
              (SELECT COUNT(*) FROM dbo.internal_request_assignments x WITH(HOLDLOCK)
                WHERE x.internal_request_id=h.request_id AND x.status='ACTIVE' AND x.ended_at IS NULL) AS ActiveHandlers
            FROM dbo.internal_request_handovers h WITH(HOLDLOCK)
            LEFT JOIN dbo.internal_request_assignments a WITH(HOLDLOCK) ON a.assignment_id=h.sender_assignment_id
            LEFT JOIN dbo.internal_requests r WITH(HOLDLOCK) ON r.internal_request_id=h.request_id
            LEFT JOIN dbo.internal_request_events e WITH(HOLDLOCK) ON e.event_id=h.handover_id
            WHERE h.state='PENDING_ACKNOWLEDGMENT' AND (h.receiver_id={target} OR a.assignee_id={target})
            """).ToArrayAsync(ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var blockers=new SortedSet<string>(StringComparer.Ordinal);
        var unresolved=new SortedSet<string>(StringComparer.Ordinal);
        foreach(var row in rows)
        {
            if(row.Receiver==target)blockers.Add("INTERNAL_REQUEST_HANDOVER_ACKNOWLEDGMENT");
            if(row.Sender==target)blockers.Add("INTERNAL_REQUEST_HANDOVER_SENDER");
            if(row.Sender==null||row.Sender==row.Receiver||row.SenderRequest!=row.RequestId
                ||row.SenderStatus!="ACTIVE"||row.Ended!=null||row.Assigned==null||row.Assigned>row.Created
                ||row.Created>now||row.CurrentBranch!=row.Branch||row.CurrentDepartment!=row.Department
                ||row.RequestStatus!="IN_PROGRESS"||row.ActiveHandlers!=1
                ||row.EventRequest!=row.RequestId||row.EventActor!=row.Sender
                ||row.EventType!="HANDOVER_SUBMIT"||row.EventTime!=row.Created)
                unresolved.Add("INTERNAL_REQUEST_HANDOVER_DATA_INCONSISTENCY");
        }
        return new(blockers.ToArray(),unresolved.ToArray());
    }

    private sealed class PendingHandover
    {
        public long Id{get;set;} public long RequestId{get;set;} public long Branch{get;set;} public long Department{get;set;}
        public long Receiver{get;set;} public DateTime Created{get;set;} public long? Sender{get;set;}
        public long? SenderRequest{get;set;} public string? SenderStatus{get;set;} public DateTime? Assigned{get;set;} public DateTime? Ended{get;set;}
        public long? CurrentBranch{get;set;} public long? CurrentDepartment{get;set;} public string? RequestStatus{get;set;}
        public long? EventRequest{get;set;} public long? EventActor{get;set;} public string? EventType{get;set;} public DateTime? EventTime{get;set;}
        public int ActiveHandlers{get;set;}
    }
}
