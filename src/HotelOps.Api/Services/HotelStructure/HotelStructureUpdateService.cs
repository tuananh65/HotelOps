using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public enum StructureUpdateStatus { Success, SessionInvalid, Forbidden, NotFound, Inactive, InvalidParent, ParentConflict, DuplicateCode, Stale, Unavailable, DependenciesExist, WriterCoordinationRequired }
public sealed record StructureUpdateResult(StructureUpdateStatus Status, EditableStructureDto? Value = null, string? ETag = null,
    IReadOnlyList<StructureDependencyDto>? Dependencies = null);

public sealed class HotelStructureUpdateService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions, TimeProvider clock)
{
    public Task<StructureUpdateResult> DeactivateAsync(SessionReference ticket, long sessionId, long branchId,
        long id, bool area, string etag, DeactivateStructureRequest request, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        if (!request.Confirmation || string.IsNullOrEmpty(reason) || reason.Length is < 10 or > 1000 || reason.Any(char.IsControl))
            throw new ValidationException("Confirmation and a reason of 10-1000 characters without control characters are required.");
        if (!IsStrongTag(etag)) throw new ValidationException("A single strong structure ETag is required.");
        return Run(ticket, sessionId, branchId, id, area, etag, null, ct, deactivate: true);
    }
    public Task<StructureUpdateResult> ReadAsync(SessionReference ticket, long sessionId, long branchId,
        long id, bool area, CancellationToken ct) => Run(ticket, sessionId, branchId, id, area, null, null, ct);
    public Task<StructureUpdateResult> UpdateFloorAsync(SessionReference ticket, long sessionId, long branchId,
        long id, string etag, UpdateFloorRequest request, CancellationToken ct) =>
        Run(ticket, sessionId, branchId, id, false, etag, Normalize(request.Code, request.Name, request.Description, request.Reason, null, 100), ct);
    public Task<StructureUpdateResult> UpdateAreaAsync(SessionReference ticket, long sessionId, long branchId,
        long id, string etag, UpdateAreaRequest request, CancellationToken ct) =>
        Run(ticket, sessionId, branchId, id, true, etag, Normalize(request.Code, request.Name, request.Description, request.Reason, request.FloorId, 150), ct);

    public static bool IsStrongTag(string? tag) => tag is { Length: 66 } && tag[0] == '"' && tag[^1] == '"'
        && tag.AsSpan(1, 64).ToString().All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    private async Task<StructureUpdateResult> Run(SessionReference ticket, long sessionId, long branchId,
        long id, bool area, string? expected, Input? input, CancellationToken ct, bool deactivate = false)
    {
        if (branchId <= 0 || id <= 0) throw new ValidationException("Branch and record IDs must be positive.");
        if (input != null && !IsStrongTag(expected)) throw new ValidationException("A single strong structure ETag is required.");
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sessionId
            || actor.IsRestricted || ticket.IsRestricted) return new(StructureUpdateStatus.SessionInvalid);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null) throw new InvalidOperationException("Structure update requires its own clean SQL context/transaction.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([actor.UserId], ct);
            if (await Gate() is { } initial) return new(initial);
            // Same retained branch lock as UC014: shared code namespace and all location writers.
            var branch = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH (UPDLOCK,HOLDLOCK) WHERE branch_id={branchId}")
                .AsNoTracking().TagWith("UC015:BranchLock").SingleOrDefaultAsync(ct);
            if (branch == null) return new(StructureUpdateStatus.NotFound);
            if (!branch.IsActive) return new(StructureUpdateStatus.Inactive);
            Floor? floor = null; Area? location = null;
            // Floor parents precede Area locks. Branch U serializes UC014/015 within this branch.
            if (area && input?.FloorId is { } parent)
            {
                var valid = await db.Floors.FromSqlInterpolated($"SELECT * FROM dbo.floors WITH (HOLDLOCK) WHERE floor_id={parent}")
                    .AsNoTracking().TagWith("UC015:ParentFloor").SingleOrDefaultAsync(ct);
                if (valid == null || valid.BranchId != branchId || !valid.IsActive) return new(StructureUpdateStatus.InvalidParent);
            }
            if (area) location = await db.Areas.FromSqlInterpolated($"SELECT * FROM dbo.areas WITH (UPDLOCK,HOLDLOCK) WHERE area_id={id} AND branch_id={branchId}")
                .Where(x => x.FloorId == null || x.Floor!.BranchId == branchId)
                .AsNoTracking().TagWith("UC015:TargetArea").SingleOrDefaultAsync(ct);
            else floor = await db.Floors.FromSqlInterpolated($"SELECT * FROM dbo.floors WITH (UPDLOCK,HOLDLOCK) WHERE floor_id={id} AND branch_id={branchId}")
                .AsNoTracking().TagWith("UC015:TargetFloor").SingleOrDefaultAsync(ct);
            if (floor == null && location == null) return new(StructureUpdateStatus.NotFound);
            var before = area
                ? new EditableStructureDto("area", id, branchId, location!.AreaCode, location.AreaName, location.Description, location.FloorId, null, location.IsActive)
                : new EditableStructureDto("floor", id, branchId, floor!.FloorCode, floor.FloorName, floor.Description, null, floor.FloorNumber, floor.IsActive);
            if ((input != null || deactivate) && !before.IsActive) return new(StructureUpdateStatus.Inactive);
            var entity = area ? "areas" : "floors"; var key = id.ToString(CultureInfo.InvariantCulture);
            // Existing immutable audit identity is the revision anchor. Full-row hash also
            // detects current-state changes from legacy writers. No invented rowversion column.
            var revision = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == entity && x.EntityId == key)
                .Select(x => (long?)x.AuditId).MaxAsync(ct) ?? 0;
            var tag = Tag(before, revision);
            if (deactivate)
            {
                if (!string.Equals(tag, expected, StringComparison.Ordinal)) return new(StructureUpdateStatus.Stale);
                var dependencies = await StructureDeactivationDependencies.ReadAsync(db, branchId, id, area, ct);
                if (await Gate() is { } finalCheck) return new(finalCheck);
                // No mutation is permitted yet: Incident create does not require an active location,
                // and Asset Move releases its location eligibility read before committing the FK.
                // Serializable reader locks alone cannot repair those uncoordinated writers.
                return new(dependencies.Count > 0 ? StructureUpdateStatus.DependenciesExist
                    : StructureUpdateStatus.WriterCoordinationRequired, ETag: tag, Dependencies: dependencies);
            }
            if (input == null)
            {
                if (await Gate() is { } lastRead) return new(lastRead);
                await tx.CommitAsync(ct); return new(StructureUpdateStatus.Success, before, tag);
            }
            if (!string.Equals(tag, expected, StringComparison.Ordinal)) return new(StructureUpdateStatus.Stale);
            if (await db.Floors.AsNoTracking().AnyAsync(x => x.BranchId == branchId && (area || x.FloorId != id) && x.FloorCode.Trim().ToUpper() == input.Code, ct)
                || await db.Areas.AsNoTracking().AnyAsync(x => x.BranchId == branchId && (!area || x.AreaId != id) && x.AreaCode.Trim().ToUpper() == input.Code, ct))
                return new(StructureUpdateStatus.DuplicateCode);
            if (area && input.FloorId is { } newFloor && await db.Rooms.AsNoTracking().AnyAsync(x => x.AreaId == id
                && (x.BranchId != branchId || x.FloorId != newFloor), ct)) return new(StructureUpdateStatus.ParentConflict);
            // No related assignment/FK is rewritten. Preserve FloorNumber, IDs and active flag.
            var after = before with { Code = input.Code, Name = input.Name, Description = input.Description, FloorId = area ? input.FloorId : null };
            var changed = new List<string>();
            if (before.Code != after.Code) changed.Add("code"); if (before.Name != after.Name) changed.Add("name");
            if (before.Description != after.Description) changed.Add("description"); if (before.FloorId != after.FloorId) changed.Add("floorId");
            // Even a no-op SAVE is audited, so every accepted PUT consumes its ETag (ABA-safe).
            if (area) await db.Areas.Where(x => x.AreaId == id && x.BranchId == branchId).TagWith("UC015:UpdateArea")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AreaCode, after.Code).SetProperty(x => x.AreaName, after.Name)
                    .SetProperty(x => x.Description, after.Description).SetProperty(x => x.FloorId, after.FloorId), ct);
            else await db.Floors.Where(x => x.FloorId == id && x.BranchId == branchId).TagWith("UC015:UpdateFloor")
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.FloorCode, after.Code).SetProperty(x => x.FloorName, after.Name)
                    .SetProperty(x => x.Description, after.Description), ct);
            var audit = new AuditLog { ActorUserId = actor.UserId, BranchId = branchId, EntityType = entity, EntityId = key,
                Action = "UPDATE", OccurredAt = clock.GetUtcNow().UtcDateTime,
                OldValues = JsonSerializer.Serialize(before, Json), NewValues = JsonSerializer.Serialize(new { value = after, reason = input.Reason }, Json),
                ChangedFields = JsonSerializer.Serialize(changed, Json) };
            db.Add(audit); try { await db.SaveChangesAsync(ct); } finally { db.Entry(audit).State = EntityState.Detached; }
            if (await Gate() is { } final) return new(final);
            await tx.CommitAsync(ct); return new(StructureUpdateStatus.Success, after, Tag(after, audit.AuditId));
        }
        catch (Exception e) when (ct.IsCancellationRequested && e is DbException or DbUpdateException or SessionStoreUnavailableException)
        { throw new OperationCanceledException("Structure update cancelled.", e, ct); }
        catch (Exception e) when (!ct.IsCancellationRequested && SqlNumber(e) is 1205 or 1222 or -2) { return new(StructureUpdateStatus.Unavailable); }
        catch (DbUpdateException e) when (SqlNumber(e) is 2601 or 2627) { return new(StructureUpdateStatus.DuplicateCode); }

        async Task<StructureUpdateStatus?> Gate()
        {
            var valid = await sessions.ValidateSessionAsync(ticket, ct); ct.ThrowIfCancellationRequested();
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted) return StructureUpdateStatus.SessionInvalid;
            var access = await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE", ct);
            if (!access.IsGranted && access.Decision != PermissionDecision.Unavailable)
                access = await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE", branchId, ct);
            ct.ThrowIfCancellationRequested();
            return access.IsGranted ? null : access.Decision == PermissionDecision.Unavailable ? StructureUpdateStatus.Unavailable : StructureUpdateStatus.Forbidden;
        }
    }
    private sealed record Input(string Code, string Name, string? Description, string Reason, long? FloorId);
    private static Input Normalize(string? code, string? name, string? description, string? reason, long? floorId, int max)
    {
        code = code?.Trim().ToUpperInvariant(); name = name?.Trim(); description = description?.Trim(); reason = reason?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 30 || !char.IsAsciiLetterOrDigit(code[0]) || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')) throw new ValidationException("Invalid structure code (1-30 ASCII letters/digits/_/-).");
        if (string.IsNullOrEmpty(name) || name.Length > max || name.Any(char.IsControl)) throw new ValidationException($"Name is required (maximum {max}, no control characters).");
        if (description?.Length > 4000 || description?.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') == true) throw new ValidationException("Invalid description (maximum 4000).");
        if (string.IsNullOrEmpty(reason) || reason.Length > 1000 || reason.Any(char.IsControl)) throw new ValidationException("A change reason is required (maximum 1000, no control characters).");
        if (floorId <= 0) throw new ValidationException("Parent FloorId must be positive or null.");
        return new(code, name, string.IsNullOrEmpty(description) ? null : description, reason, floorId);
    }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Tag(EditableStructureDto value, long revision) => '"' + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { contract = "UC015-V1", value, revision }, Json))) + '"';
    private static int? SqlNumber(Exception error)
    { for (Exception? e = error; e != null; e = e.InnerException) if (e is SqlException sql) return sql.Number; return null; }
}
