using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.WorkTime;

public sealed record ShiftAssignmentNotice(long EmployeeId, long ShiftId, long AssignmentId);
/// <summary>Notification owner must stage durably in THIS context/transaction, never send or commit here.</summary>
public interface IShiftPlanNotificationParticipant
{
    Task<int> StageAsync(HotelOpsDbContext context, Guid transactionId, long actorId, IReadOnlyList<ShiftAssignmentNotice> notices, CancellationToken ct);
}
public sealed record ShiftPlanSaved(int CreatedShifts, int CreatedAssignments, int NotificationCount);
public sealed record ShiftPlanSaveResult(int Status, ShiftPlanSaved? Value = null);
/// <summary>New plans only; no update/revoke/version invented. Release remains immutable pre-SQL.</summary>
public sealed class ShiftPlanSaveService(HotelOpsDbContext db, ICurrentUserAccessor current, IPermissionDataSource paths,
 ISqlSessionService sessions, TimeProvider clock, IShiftPlanNotificationParticipant? notifications = null)
{
    public static IReadOnlyList<string> PendingReleaseGates { get; } = Array.AsReadOnly(new[] { "ROSTER_USER_SCOPE_WRITER_COORDINATION", "NOTIFICATION_TRANSACTION_PARTICIPANT_ACCEPTANCE", "SQL_OVERLAP_RACE_AUDIT_ROLLBACK_HTTPS" });
    public async Task<ShiftPlanSaveResult> SaveAsync(SessionReference ticket, long sid, long branch, long department, ShiftPreviewRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid || sid <= 0) return new(401);
        if (actor.IsRestricted || ticket.IsRestricted) return new(403);
        if (branch <= 0 || department <= 0 || request?.Drafts == null || request.Drafts.Count is < 1 or > 500) return new(400);
        var snapshot = request.Drafts.ToArray(); if (snapshot.Any(d => d == null || d.EmployeeId <= 0 || d.TemplateId <= 0 || d.ShiftDate == default)) return new(400);
        if (PendingReleaseGates.Count != 0) return new(503);
        return await ExecuteAsync(ticket, sid, actor.UserId, branch, department, new(snapshot), ct);
    }
    private async Task<ShiftPlanSaveResult> ExecuteAsync(SessionReference ticket, long sid, long actor, long branch, long department, ShiftPreviewRequest request, CancellationToken ct)
    {
        if (notifications == null) return new(503); // Missing durable participant cannot be treated as delivered.
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Owned clean shift transaction required.");
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var global = await new GlobalAdminSerialization(db, clock).AcquireAsync(ct: ct); _ = await global.ReadSnapshotAsync(ct);
        await new UserEligibilityLock(db).LockAsync(request.Drafts.Select(d => d.EmployeeId).Append(actor).Distinct().Order().ToArray(), ct);
        _ = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch}").AsNoTracking().ToArrayAsync(ct);
        _ = await db.Departments.FromSqlInterpolated($"SELECT * FROM dbo.departments WITH(UPDLOCK,HOLDLOCK) WHERE department_id={department}").AsNoTracking().ToArrayAsync(ct);
        foreach (var id in request.Drafts.Select(d => d.TemplateId).Distinct().Order())
            _ = await db.ShiftTemplates.FromSqlInterpolated($"SELECT * FROM dbo.shift_templates WITH(HOLDLOCK) WHERE shift_template_id={id}").AsNoTracking().ToArrayAsync(ct);
        // Retain candidate shift range before planning. Other writers must participate; immutable gate enforces this debt.
        _ = await db.WorkShifts.FromSqlInterpolated($"SELECT * FROM dbo.work_shifts WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch} AND department_id={department}").AsNoTracking().ToArrayAsync(ct);
        var admission = new Session(sessions, ticket, sid);
        var core = new SecureShiftPlanPreviewService(current, paths, new SqlShiftPreviewScopeData(db, clock), new SqlShiftPlanPreviewReader(db), admission, clock);
        var preview = await core.PreviewAsync(branch, department, request, ct);
        if (preview.Status != 200) return new(preview.Status);
        if (preview.Value!.Rows.Any(r => r.Issues.Contains(HotelOps.Api.DTOs.WorkTime.ShiftPlanIssue.DataUnavailable) || r.Issues.Contains(HotelOps.Api.DTOs.WorkTime.ShiftPlanIssue.TimezoneUnavailable))) return new(503);
        if (preview.Value.Rows.Any(r => r.Issues.Count != 0 || r.StartUtc == null || r.EndUtc == null)) return new(409);
        var now = clock.GetUtcNow().UtcDateTime;
        var entries = request.Drafts.Select((d, i) => (Draft: d, Row: preview.Value.Rows[i])).ToArray();
        if (entries.Any(x => x.Row.StartUtc!.Value.UtcDateTime <= now)) return new(409); // V1 creates future assignments only.
                                                                                         // Insert-only V1: reject existing template/date plans. Never infer update authority or a fake ETag.
        foreach (var group in entries.GroupBy(x => (x.Draft.TemplateId, x.Draft.ShiftDate)))
            if (await db.WorkShifts.AnyAsync(s => s.BranchId == branch && s.DepartmentId == department && s.ShiftTemplateId == group.Key.TemplateId && s.ShiftDate == group.Key.ShiftDate, ct)) return new(409);
        var created = new List<ShiftAssignmentNotice>(); var shiftCount = 0;
        try
        {
            foreach (var group in entries.GroupBy(x => (x.Draft.TemplateId, x.Draft.ShiftDate)))
            {
                var first = group.First(); var shift = new WorkShift
                {
                    BranchId = branch,
                    DepartmentId = department,
                    ShiftTemplateId = group.Key.TemplateId,
                    ShiftDate = group.Key.ShiftDate,
                    ScheduledStartAt = first.Row.StartUtc!.Value.UtcDateTime,
                    ScheduledEndAt = first.Row.EndUtc!.Value.UtcDateTime,
                    RequiredStaffCount = group.Count(),
                    Status = "PLANNED",
                    CreatedBy = actor,
                    CreatedAt = now
                };
                db.Add(shift); await db.SaveChangesAsync(ct); shiftCount++;
                foreach (var row in group)
                {
                    var assignment = new ShiftAssignment { ShiftId = shift.ShiftId, UserId = row.Draft.EmployeeId, AssignmentStatus = "ASSIGNED", AssignedBy = actor, AssignedAt = now };
                    db.Add(assignment); await db.SaveChangesAsync(ct); created.Add(new(assignment.UserId, shift.ShiftId, assignment.ShiftAssignmentId));
                }
                db.Add(new AuditLog
                {
                    ActorUserId = actor,
                    BranchId = branch,
                    EntityType = "work_shifts",
                    EntityId = shift.ShiftId.ToString(CultureInfo.InvariantCulture),
                    Action = "CREATE",
                    OccurredAt = now,
                    NewValues = JsonSerializer.Serialize(new { shift.ShiftId, branch, department, templateId = shift.ShiftTemplateId, date = shift.ShiftDate, employees = group.Select(x => x.Draft.EmployeeId).ToArray() }),
                    ChangedFields = "[\"shift\",\"assignments\"]"
                });
                await db.SaveChangesAsync(ct);
            }
            db.ChangeTracker.Clear(); // Session admission requires an untracked context; writes are already flushed.
            var staged = await notifications.StageAsync(db, tx.TransactionId, actor, created.AsReadOnly(), ct);
            if (staged != created.Count || db.Database.CurrentTransaction?.TransactionId != tx.TransactionId) throw new InvalidOperationException("Notification staging incomplete.");
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var final = await core.AuthorizeAsync(branch, department, request, ct); if (final != 200) return new(final);
            // Final global employee conflict reread excludes only the assignments written by THIS transaction.
            var ownIds = created.Select(x => x.AssignmentId).ToArray();
            foreach (var entry in entries)
            {
                var a = entry.Row.StartUtc!.Value.UtcDateTime; var b = entry.Row.EndUtc!.Value.UtcDateTime;
                if (await db.ShiftAssignments.AsNoTracking().AnyAsync(x => x.UserId == entry.Draft.EmployeeId && !ownIds.Contains(x.ShiftAssignmentId) && x.AssignmentStatus != "CANCELLED"
                 && ((x.Shift.ScheduledStartAt < b && x.Shift.ScheduledEndAt > a) || x.Shift.ScheduledEndAt <= x.Shift.ScheduledStartAt), ct)) return new(409);
            }
            await tx.CommitAsync(ct); return new(201, new(shiftCount, created.Count, staged));
        }
        finally { db.ChangeTracker.Clear(); } // Every failed/early path disposes the uncommitted transaction.
    }
    private sealed class Session(ISqlSessionService sessions, SessionReference ticket, long sid) : IShiftPreviewSessionAdmission
    { public async Task<bool> ValidateAsync(CancellationToken ct) { var v = await sessions.ValidateSessionAsync(ticket, ct); return v.IsValid && v.Session?.SessionId == sid && v.Session.UserId == ticket.UserId && !v.Session.IsRestricted; } }
}