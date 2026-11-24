using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Services;

public static class CleaningDeadline
{
    public static bool IsOverdue(string kind, string status, DateTime? dueAt, DateTime? completedAt, DateTime now) =>
        kind is "CLEANING" or "REWORK" && dueAt < now && completedAt == null
        && status is "ASSIGNED" or "PENDING_ACCEPTANCE" or "ACCEPTED" or "IN_PROGRESS" or "ON_HOLD";

    public static Guid Key(long assignmentId, DateTime dueAt) => new(SHA256.HashData(
        Encoding.UTF8.GetBytes($"cleaning-overdue:{assignmentId}:{dueAt:O}")).AsSpan(0,16));

    // Caller holds the work transaction. Actor is null: this is a system observation, not a staff action.
    public static async Task LogIfOverdue(HotelOpsCompactDbContext db, WorkItem work,
        WorkAssignment? assignment, DateTime now, CancellationToken ct)
    {
        if (assignment == null || !IsOverdue(work.Kind,work.Status,work.DueAt,work.CompletedAt,now)) return;
        var key = Key(assignment.AssignmentId, work.DueAt!.Value);
        if (db.AuditLogs.Local.Any(x => x.OperationKey == key)
            || await db.AuditLogs.AnyAsync(x => x.OperationKey == key && x.Action == "CLEANING_OVERDUE",ct)) return;
        db.AuditLogs.Add(new AuditLog {
            BranchId=work.BranchId, ActorUserId=null, EntityType="Work", EntityKey=work.WorkId.ToString(),
            Action="CLEANING_OVERDUE", OperationKey=key, OccurredAt=now,
            AfterJson=JsonSerializer.Serialize(new { work.WorkId, work.RoomId, work.DueAt, work.Status,
                assignment.AssignmentId, assignment.AssigneeUserId, assignment.AssignedAt })
        });
    }

    public static async Task Scan(HotelOpsCompactDbContext db, DateTime now, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var rows=await (from w in db.WorkItems.AsNoTracking()
            join a in db.WorkAssignments.AsNoTracking() on w.WorkId equals a.WorkId
            where (w.Kind=="CLEANING"||w.Kind=="REWORK") && w.DueAt<now && w.CompletedAt==null
                && (w.Status=="ASSIGNED"||w.Status=="PENDING_ACCEPTANCE"||w.Status=="ACCEPTED"||w.Status=="IN_PROGRESS"||w.Status=="ON_HOLD")
                && a.AssignmentStatus=="ACTIVE"
            select new { Work=w, Assignment=a }).ToArrayAsync(ct);
        foreach(var row in rows) await LogIfOverdue(db,row.Work,row.Assignment,now,ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
