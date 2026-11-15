using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestProgressInput([property: System.Text.Json.Serialization.JsonRequired] int Percent, string? Note, string? Result, DateTimeOffset? Deadline, string? DeadlineReason);
public sealed record InternalRequestProgressResult(long RequestId, long EventId, string Status, string Version);
public sealed record InternalRequestProgressView(long RequestId, string Status, string Version, int? Percent,
    string? Note, string? Result, DateTime? Deadline, bool CanUpdate, bool CanChangeDeadline);
internal sealed class InternalRequestProgressRow
{
    public int Percent { get; set; }
    public string? Note { get; set; }
    public string? Result { get; set; }
    public long EventId { get; set; }
}

// GBR149 progress and result updates; closure remains a separate operation.
public sealed class InternalRequestProgressService(HotelOpsDbContext db, InternalRequestProgressAuthority authority,
    ISqlSessionService sessions, ICurrentUserAccessor current)
{
    internal static InternalRequestProgressInput Validate(InternalRequestProgressInput input)
    {
        static string? Text(string? text)
        {
            text = text?.Trim();
            if (text?.Length > 4000 || text?.Any(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') == true)
                throw new ValidationException("Progress text must contain at most 4000 characters and no unsupported controls.");
            return string.IsNullOrEmpty(text) ? null : text;
        }
        if (input.Percent is < 0 or > 100) throw new ValidationException("Progress must be between 0 and 100.");
        return input with { Note = Text(input.Note), Result = Text(input.Result), DeadlineReason = Text(input.DeadlineReason) };
    }

    private async Task SessionAsync(SessionReference ticket, long sid, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401, "SESSION_INVALID");
        var session = await sessions.ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted)
            throw new InternalRequestReadException(401, "SESSION_INVALID");
    }

    internal static async Task<bool> InstalledAsync(HotelOpsDbContext db, CancellationToken ct) =>
        await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
              AND name='HotelOps.InternalRequestProgress.V1' AND CONVERT(nvarchar(20),value)='1')
              AND COL_LENGTH('dbo.internal_request_progress','progress_percent')=4
              AND COL_LENGTH('dbo.internal_request_progress','note')=8000
              AND COL_LENGTH('dbo.internal_request_progress','result_note')=8000
              AND (SELECT COUNT(*) FROM sys.foreign_keys WHERE name IN('FK_ir_progress_request','FK_ir_progress_event',
                'FK_ir_progress_notice_event','FK_ir_progress_notice_requester','FK_ir_progress_notice_notification')
                AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)=5
              AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.internal_request_progress')
                AND name='CK_ir_progress_percent' AND is_disabled=0 AND is_not_trusted=0)
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct) == 1;

    internal static Task<InternalRequestProgressRow?> SnapshotAsync(HotelOpsDbContext db, long id, CancellationToken ct) =>
        db.Database.SqlQuery<InternalRequestProgressRow>($"SELECT progress_percent AS [Percent],note AS Note,result_note AS Result,event_id AS EventId FROM dbo.internal_request_progress WHERE internal_request_id={id}")
            .SingleOrDefaultAsync(ct);

    public async Task<InternalRequestProgressView> ReadAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Request identifier required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
        await SessionAsync(ticket, sid, ct);
        if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct))
            throw new InternalRequestReadException(503, "IR_PROGRESS_SCHEMA_REQUIRED");
        // Acquire request lock before authority/snapshot reads, matching mutation lock order.
        var access = await authority.ResolveAsync(ticket.UserId, id, ct);
        var now = DateTime.UtcNow;
        var scope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, now, ct);
        var request = await scope.Apply(db.InternalRequests.AsNoTracking(), now).SingleOrDefaultAsync(r => r.InternalRequestId == id, ct);
        if (request == null) throw new InternalRequestReadException(404, "IR_NOT_FOUND");
        var snapshot = await SnapshotAsync(db, id, ct);
        var version = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
        await SessionAsync(ticket, sid, ct);
        var finalNow = DateTime.UtcNow;
        var finalScope = await new InternalRequestReadAccess(db).ReadAsync(ticket.UserId, finalNow, ct);
        if (!await finalScope.Apply(db.InternalRequests.AsNoTracking(), finalNow).AnyAsync(r => r.InternalRequestId == id, ct))
            throw new InternalRequestReadException(403, "IR_SCOPE_CHANGED");
        access = await authority.ResolveAsync(ticket.UserId, id, ct);
        await tx.CommitAsync(ct);
        return new(id, request.Status, Convert.ToBase64String(version), snapshot?.Percent, snapshot?.Note, snapshot?.Result,
            request.DueAt is {} due ? DateTime.SpecifyKind(due, DateTimeKind.Utc) : null, access.CanUpdate, access.CanChangeDeadline);
    }

    public async Task<InternalRequestProgressResult> UpdateAsync(SessionReference ticket, long sid, long id, byte[] version,
        InternalRequestProgressInput input, CancellationToken ct)
    {
        input = Validate(input);
        if (id <= 0 || version.Length != 8) throw new ValidationException("Loaded request version required.");
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Progress requires a clean context.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([ticket.UserId], ct);
            await SessionAsync(ticket, sid, ct);
            if (!await InternalRequestDecisionStore.InstalledAsync(db, ct) || !await InstalledAsync(db, ct))
                throw new InternalRequestReadException(503, "IR_PROGRESS_SCHEMA_REQUIRED");
            var access = await authority.ResolveAsync(ticket.UserId, id, ct);
            if (!access.CanUpdate) throw new InternalRequestReadException(403, "IR_PROGRESS_REQUIRED");
            var request = await db.InternalRequests.AsNoTracking().SingleAsync(r => r.InternalRequestId == id, ct);
            if (!(await InternalRequestDecisionStore.RevisionAsync(db, id, ct)).SequenceEqual(version))
                throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
            var deadline = input.Deadline?.UtcDateTime ?? request.DueAt;
            if (deadline != request.DueAt)
            {
                if (!access.CanChangeDeadline) throw new InternalRequestReadException(403, "IR_DEADLINE_AUTHORITY_REQUIRED");
                if (input.DeadlineReason == null) throw new ValidationException("Deadline change requires a reason.");
                // Existing request contract has no planned-start field. Do not reinterpret
                // RequiredAt or submission time as a planned start, or forbid overdue dates.
            }
            var previous = await SnapshotAsync(db, id, ct);
            var changed = await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_requests SET due_at={deadline},updated_at={now} WHERE internal_request_id={id} AND status='IN_PROGRESS' AND row_version={version}", ct);
            if (changed != 1) throw new InternalRequestReadException(412, "IR_VERSION_CHANGED");
            var entry = new InternalRequestEvent { InternalRequestId = id, EventType = "PROGRESS", OldStatus = "IN_PROGRESS", NewStatus = "IN_PROGRESS",
                EventNote = input.Note, ActorId = ticket.UserId, OccurredAt = now };
            db.InternalRequestEvents.Add(entry);
            db.AuditLogs.Add(new AuditLog { ActorUserId = ticket.UserId, BranchId = request.BranchId, EntityType = "internal_requests",
                EntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE", OccurredAt = now,
                OldValues = JsonSerializer.Serialize(new { progress = previous, deadline = request.DueAt }),
                NewValues = JsonSerializer.Serialize(new { operation = "PROGRESS", input.Percent, input.Note, input.Result, deadline, input.DeadlineReason }),
                ChangedFields = "[\"progress\",\"due_at\"]" });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            if (previous == null)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_progress(internal_request_id,progress_percent,note,result_note,event_id) VALUES({id},{input.Percent},{input.Note},{input.Result},{entry.EventId})", ct);
            else
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_progress SET progress_percent={input.Percent},note={input.Note},result_note={input.Result},event_id={entry.EventId} WHERE internal_request_id={id}", ct);
            if (input.Percent == 100 && previous?.Percent != 100)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_progress_outbox(event_id,requester_id,created_at) VALUES({entry.EventId},{request.RequesterId},{now})", ct);
            await SessionAsync(ticket, sid, ct);
            var finalAccess = await authority.ResolveAsync(ticket.UserId, id, ct);
            if (!finalAccess.CanUpdate || deadline != request.DueAt && !finalAccess.CanChangeDeadline)
                throw new InternalRequestReadException(403, "IR_AUTHORITY_CHANGED");
            var finalVersion = await InternalRequestDecisionStore.RevisionAsync(db, id, ct);
            await tx.CommitAsync(ct);
            return new(id, entry.EventId, "IN_PROGRESS", Convert.ToBase64String(finalVersion));
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
