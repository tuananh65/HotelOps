using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Globalization;
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

public enum StructureCreationStatus { Success, SessionInvalid, Forbidden, InvalidBranch, InvalidFloor, DuplicateCode, Unavailable }
public sealed record StructureCreationResult(StructureCreationStatus Status, object? Value = null);

public sealed class HotelStructureCreationService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions, TimeProvider clock)
{
    public Task<StructureCreationResult> CreateFloorAsync(SessionReference ticket, long sessionId, long branchId,
        CreateFloorRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (code, name, description) = Normalize(request.Code, request.Name, request.Description, 100);
        return CreateAsync(ticket, sessionId, branchId, code, name, description, request.FloorNumber, null, false, ct);
    }

    public Task<StructureCreationResult> CreateAreaAsync(SessionReference ticket, long sessionId, long branchId,
        CreateAreaRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FloorId <= 0) throw new ValidationException("FloorId must be a positive Int64 or null.");
        var (code, name, description) = Normalize(request.Code, request.Name, request.Description, 150);
        return CreateAsync(ticket, sessionId, branchId, code, name, description, null, request.FloorId, true, ct);
    }

    private async Task<StructureCreationResult> CreateAsync(SessionReference ticket, long sessionId, long branchId,
        string code, string name, string? description, int? floorNumber, long? floorId, bool area, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (branchId <= 0) throw new ValidationException("BranchId must be a positive Int64.");
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sessionId
            || actor.IsRestricted || ticket.IsRestricted) return new(StructureCreationStatus.SessionInvalid);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null
            || System.Transactions.Transaction.Current != null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("Structure creation requires a clean local SQL Server context and its own transaction.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // The only User participant is the authenticated actor. Retain it BEFORE session,
            // grant or feature locks; final checks only reread locks already retained here.
            await new UserEligibilityLock(db).LockAsync([actor.UserId], ct);
            if (await Gate() is { } initial) return new(initial);

            // One retained parent update lock serializes every Floor/Area producer in this
            // branch, including absent/cross-table codes; existing unique indexes back it up.
            var branch = await db.Branches.FromSqlInterpolated(
                    $"SELECT * FROM dbo.branches WITH (UPDLOCK,HOLDLOCK) WHERE branch_id={branchId}")
                .AsNoTracking().TagWith("UC014:BranchLock").SingleOrDefaultAsync(ct);
            if (branch is null || !branch.IsActive) return new(StructureCreationStatus.InvalidBranch);
            if (floorId is { } parent)
            {
                var floor = await db.Floors.FromSqlInterpolated(
                        $"SELECT * FROM dbo.floors WITH (HOLDLOCK) WHERE floor_id={parent}")
                    .AsNoTracking().TagWith("UC014:ParentFloor").SingleOrDefaultAsync(ct);
                if (floor is null || !floor.IsActive || floor.BranchId != branchId)
                    return new(StructureCreationStatus.InvalidFloor);
            }
            // GBR003/034: case-insensitive, trimmed branch namespace. Checking both types
            // also preserves the SRS's unified floor/area-code rule without a schema change.
            if (await db.Floors.AsNoTracking().AnyAsync(x => x.BranchId == branchId && x.FloorCode.Trim().ToUpper() == code, ct)
                || await db.Areas.AsNoTracking().AnyAsync(x => x.BranchId == branchId && x.AreaCode.Trim().ToUpper() == code, ct))
                return new(StructureCreationStatus.DuplicateCode);

            object row = area
                ? new Area { BranchId = branchId, FloorId = floorId, AreaCode = code, AreaName = name, Description = description, IsActive = true }
                : new Floor { BranchId = branchId, FloorCode = code, FloorName = name, FloorNumber = floorNumber, Description = description, IsActive = true };
            db.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException error) when (SqlNumber(error) is 2601 or 2627)
            { return new(StructureCreationStatus.DuplicateCode); }
            finally { db.Entry(row).State = EntityState.Detached; }

            object value = row is Area ar
                ? new CreatedAreaDto(ar.AreaId, branchId, code, name, floorId, description, true)
                : new CreatedFloorDto(((Floor)row).FloorId, branchId, code, name, floorNumber, description, true);
            var id = row is Area added ? added.AreaId : ((Floor)row).FloorId;
            var audit = new AuditLog
            {
                ActorUserId = actor.UserId, BranchId = branchId, EntityType = area ? "areas" : "floors",
                EntityId = id.ToString(CultureInfo.InvariantCulture), Action = "CREATE", OccurredAt = clock.GetUtcNow().UtcDateTime,
                // The canonical audit CHECK uses ISJSON without SCALAR: wrap absence
                // in an object, as existing account-creation audits do.
                OldValues = JsonSerializer.Serialize(new { entity = (object?)null }),
                NewValues = JsonSerializer.Serialize(value, value.GetType(), AuditJson),
                ChangedFields = JsonSerializer.Serialize(area
                    ? new[] { "branchId", "code", "name", "floorId", "description", "isActive" }
                    : new[] { "branchId", "code", "name", "floorNumber", "description", "isActive" })
            };
            db.Add(audit);
            try { await db.SaveChangesAsync(ct); }
            finally { db.Entry(audit).State = EntityState.Detached; }
            if (await Gate() is { } final) return new(final); // expiry while waiting/writing rolls back both inserts
            await tx.CommitAsync(ct);
            return new(StructureCreationStatus.Success, value);
        }
        catch (Exception error) when (ct.IsCancellationRequested && error is DbException or DbUpdateException or SessionStoreUnavailableException)
        { throw new OperationCanceledException("Structure creation cancelled.", error, ct); }
        catch (Exception error) when (!ct.IsCancellationRequested && SqlNumber(error) is 1205 or 1222 or -2)
        {
            // No blind replay of a mutation. Dispose rolls back; caller may retry a deadlock
            // or lock timeout as a NEW request. A commit/connection failure never reports success.
            return new(StructureCreationStatus.Unavailable);
        }

        async Task<StructureCreationStatus?> Gate()
        {
            var valid = await sessions.ValidateSessionAsync(ticket, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted)
                return StructureCreationStatus.SessionInvalid;
            // Global authority is checked first so an authorized global manager can receive
            // parent validation errors. Scoped authority cannot reveal an unavailable branch.
            var access = await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE", ct);
            if (!access.IsGranted && access.Decision != PermissionDecision.Unavailable)
                access = await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE", branchId, ct);
            return access.IsGranted ? null : access.Decision == PermissionDecision.Unavailable
                ? StructureCreationStatus.Unavailable : StructureCreationStatus.Forbidden;
        }
    }

    private static readonly JsonSerializerOptions AuditJson = new(JsonSerializerDefaults.Web);

    private static int? SqlNumber(Exception error)
    {
        for (Exception? currentError = error; currentError != null; currentError = currentError.InnerException)
            if (currentError is SqlException sql) return sql.Number;
        return null;
    }

    private static (string Code, string Name, string? Description) Normalize(string? code, string? name, string? description, int nameLimit)
    {
        code = code?.Trim().ToUpperInvariant(); name = name?.Trim(); description = description?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length > 30 || !char.IsAsciiLetterOrDigit(code[0])
            || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
            throw new ValidationException("Code must contain 1–30 ASCII letters/digits, underscores or hyphens and start with a letter/digit.");
        if (string.IsNullOrEmpty(name) || name.Length > nameLimit || name.Any(char.IsControl))
            throw new ValidationException($"Name is required, limited to {nameLimit} characters and cannot contain control characters.");
        if (description?.Length > 4000 || description?.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') == true)
            throw new ValidationException("Description is limited to 4000 characters and cannot contain other control characters.");
        return (code, name, string.IsNullOrEmpty(description) ? null : description);
    }
}
