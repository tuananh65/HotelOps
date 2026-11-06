using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public enum RoomTypeBatchStatus { Success, SessionInvalid, Forbidden, NotFound, Inactive, Stale, InvalidReference, Duplicate, SchemaRequired, Unavailable, DependenciesExist }
public sealed record RoomTypeBatchResult(RoomTypeBatchStatus Status, object? Value = null, string? ETag = null);

public sealed partial class RoomTypeCreationService
{
    // Retains the UC017 SQL session/grant boundary and lock order for every batch operation.
    private async Task<RoomTypeBatchResult> BatchAsync(SessionReference ticket, long sessionId, long? branchId,
        Func<long, CancellationToken, Task<RoomTypeBatchResult>> operation, CancellationToken ct, bool allowScopedList = false,
        IEnumerable<long>? additionalUsers = null, Func<CancellationToken, Task<bool>>? eligibility = null, bool requireRoomTypeSchema = true,
        string permissionCode = "MASTER_DATA_MANAGE", Func<CancellationToken, Task<PermissionEvaluation>>? featurePermission = null, Func<CancellationToken, Task>? beforeCommit = null)
    {
        if (branchId <= 0) throw new ValidationException("BranchId must be positive.");
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sessionId
            || actor.IsRestricted || ticket.IsRestricted) return new(RoomTypeBatchStatus.SessionInvalid);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null) throw new InvalidOperationException("A clean local SQL context is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync((additionalUsers ?? []).Append(actor.UserId), ct);
            if (await Gate() is { } initial) return new(initial);
            if (eligibility != null && !await eligibility(ct)) return new(RoomTypeBatchStatus.InvalidReference);
            if (branchId is { } branch)
            {
                var parent = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (parent == null) return new(RoomTypeBatchStatus.NotFound);
                if (!parent.IsActive) return new(RoomTypeBatchStatus.Inactive);
            }
            var acquired = await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={CatalogLock},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r AS [Value];").ToArrayAsync(ct);
            if (acquired.Length != 1 || acquired[0] < 0) return new(RoomTypeBatchStatus.Unavailable);
            if (requireRoomTypeSchema && await RoomTypeSchema.ReadAsync(db, ct) != RoomTypeContract.BranchV1) return new(RoomTypeBatchStatus.SchemaRequired);
            var result = await operation(actor.UserId, ct);
            if (result.Status is not (RoomTypeBatchStatus.Success or RoomTypeBatchStatus.DependenciesExist)) return result;
            if (await Gate() is { } final) return new(final);
            if (eligibility != null && !await eligibility(ct)) return new(RoomTypeBatchStatus.InvalidReference);
            if (result.Status == RoomTypeBatchStatus.DependenciesExist) return result;
            if (beforeCommit != null) await beforeCommit(ct);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception error) when (!ct.IsCancellationRequested && SqlNumber(error) is 1205 or 1222 or -2)
        { return new(RoomTypeBatchStatus.Unavailable); }
        catch (Exception error) when (ct.IsCancellationRequested && error is System.Data.Common.DbException or DbUpdateException or SessionStoreUnavailableException)
        { throw new OperationCanceledException("Hotel structure operation cancelled.", error, ct); }
        finally { db.ChangeTracker.Clear(); }

        async Task<RoomTypeBatchStatus?> Gate()
        {
            var valid = await sessions.ValidateSessionAsync(ticket, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted) return RoomTypeBatchStatus.SessionInvalid;
            var access = featurePermission != null ? await featurePermission(ct) : branchId is { } branch
                ? await permissions.EvaluateBranchAsync(permissionCode, branch, ct)
                : await permissions.EvaluateGlobalAsync(permissionCode, ct);
            ct.ThrowIfCancellationRequested();
            if (access.IsGranted) return null;
            if (access.Decision == PermissionDecision.Unavailable) return RoomTypeBatchStatus.Unavailable;
            if (branchId == null && allowScopedList)
                foreach (var candidate in await db.Branches.AsNoTracking().Select(x => x.BranchId).ToArrayAsync(ct))
                {
                    var scoped = await permissions.EvaluateBranchAsync(permissionCode, candidate, ct);
                    if (scoped.Decision == PermissionDecision.Unavailable) return RoomTypeBatchStatus.Unavailable;
                    if (scoped.IsGranted) return null;
                }
            return RoomTypeBatchStatus.Forbidden;
        }
    }

    private static RoomTypeDetailsDto RoomTypeDto(RoomTypeBranchV1 row) => new(row.RoomTypeId, row.BranchId,
        row.TypeCode, row.TypeName, row.Description, row.StandardCleaningMinutes,
        row.DefaultChecklistId, row.StandardCapacity, row.IsActive);

    private async Task<string> RoomTypeTagAsync(RoomTypeDetailsDto value, CancellationToken ct)
    {
        var key = value.Id.ToString(CultureInfo.InvariantCulture);
        var revision = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "room_types" && x.EntityId == key)
            .Select(x => (long?)x.AuditId).MaxAsync(ct) ?? 0;
        var activeRooms = await db.Rooms.AsNoTracking().CountAsync(x => x.RoomTypeId == value.Id && x.IsActive
            && (value.BranchId == null || x.BranchId == value.BranchId), ct);
        return '"' + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { value, revision, activeRooms }, Json))) + '"';
    }

    public Task<RoomTypeBatchResult> ReadRoomTypeAsync(SessionReference ticket, long sessionId, long? branchId, long id, CancellationToken ct)
        => EditRoomTypeAsync(ticket, sessionId, branchId, id, null, null, ct);

    public Task<RoomTypeBatchResult> UpdateRoomTypeAsync(SessionReference ticket, long sessionId, long branchId, long id,
        string expected, UpdateRoomTypeRequest request, CancellationToken ct)
    {
        if (!HotelStructureUpdateService.IsStrongTag(expected)) throw new ValidationException("A single strong ETag is required.");
        var name = request.Name?.Trim(); var description = request.Description?.Trim();
        var code = request.Code?.Trim().ToUpperInvariant();
        if (code != null && (code.Length is < 1 or > 30 || !char.IsAsciiLetterOrDigit(code[0])
            || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')))
            throw new ValidationException("Invalid room type code.");
        if (string.IsNullOrEmpty(name) || name.Length > 150 || name.Any(char.IsControl)
            || description?.Length > 4000 || description?.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') == true
            || request.StandardCleaningMinutes <= 0 || request.DefaultChecklistId <= 0) throw new ValidationException("Invalid room type fields.");
        return EditRoomTypeAsync(ticket, sessionId, branchId, id, expected, request with { Name = name, Description = string.IsNullOrEmpty(description) ? null : description, Code = code }, ct);
    }

    private Task<RoomTypeBatchResult> EditRoomTypeAsync(SessionReference ticket, long sessionId, long? branchId, long id,
        string? expected, UpdateRoomTypeRequest? input, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Id must be positive.");
        return BatchAsync(ticket, sessionId, branchId, async (actor, token) =>
        {
            if (branchId == null && !(await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE", token)).IsGranted)
                return new(RoomTypeBatchStatus.Forbidden);
            var row = await db.BranchRoomTypes.AsNoTracking().SingleOrDefaultAsync(x => x.RoomTypeId == id && x.BranchId == branchId, token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var before = RoomTypeDto(row); var tag = await RoomTypeTagAsync(before, token);
            if (input == null) return new(RoomTypeBatchStatus.Success, new RoomTypeListItem(before,
                await db.Rooms.CountAsync(x => x.RoomTypeId == id && (branchId == null || x.BranchId == branchId) && x.IsActive, token), branchId != null && before.IsActive), tag);
            if (!before.IsActive) return new(RoomTypeBatchStatus.Inactive);
            if (!string.Equals(expected, tag, StringComparison.Ordinal)) return new(RoomTypeBatchStatus.Stale);
            var now = clock.GetUtcNow().UtcDateTime;
            if (!await ChecklistEligible(input.DefaultChecklistId, branchId!.Value, id, token)) return new(RoomTypeBatchStatus.InvalidReference);
            var code = input.Code ?? before.Code;
            if (await db.Database.SqlQuery<int>($"SELECT CONVERT(int,COUNT(*)) AS [Value] FROM dbo.room_types WHERE room_type_id<>{id} AND (branch_id={branchId} OR branch_id IS NULL) AND UPPER(LTRIM(RTRIM(type_code)))={code}").SingleAsync(token) != 0)
                return new(RoomTypeBatchStatus.Duplicate);
            // Ownership and capacity are immutable. No Room or historical checklist FK is changed.
            await db.Database.ExecuteSqlInterpolatedAsync($"/* UC018:Update */ UPDATE dbo.room_types SET type_code={code},type_name={input.Name},description={input.Description},standard_cleaning_minutes={input.StandardCleaningMinutes},default_checklist_id={input.DefaultChecklistId} WHERE room_type_id={id} AND branch_id={branchId}", token);
            var after = before with { Code = code, Name = input.Name, Description = input.Description, StandardCleaningMinutes = input.StandardCleaningMinutes, DefaultChecklistId = input.DefaultChecklistId };
            await BatchAuditAsync(actor, branchId!.Value, "room_types", id, before, after,
                code == before.Code ? ["name", "description", "standardCleaningMinutes", "defaultChecklistId"] : ["code", "name", "description", "standardCleaningMinutes", "defaultChecklistId"], token);
            return new(RoomTypeBatchStatus.Success, after, await RoomTypeTagAsync(after, token));
        }, ct);
    }

    public Task<RoomTypeBatchResult> ListRoomTypesAsync(SessionReference ticket, long sessionId, long? branchId,
        bool? active, string? search, int page, int pageSize, CancellationToken ct, string sort = "code", bool descending = false)
    {
        search = search?.Trim();
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize || search?.Length > 150 || search?.Any(char.IsControl) == true
            || sort is not ("code" or "name" or "status"))
            throw new ValidationException("Invalid list filters or pagination.");
        return BatchAsync(ticket, sessionId, branchId, async (_, token) =>
        {
            var rows = db.BranchRoomTypes.AsNoTracking();
            var visible = new List<long>();
            var global = await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE", token);
            if (global.Decision == PermissionDecision.Unavailable) return new(RoomTypeBatchStatus.Unavailable);
            if (branchId == null && !global.IsGranted)
            {
                foreach (var candidate in await db.Branches.AsNoTracking().Select(x => x.BranchId).ToArrayAsync(token))
                {
                    var access = await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE", candidate, token);
                    if (access.Decision == PermissionDecision.Unavailable) return new(RoomTypeBatchStatus.Unavailable);
                    if (access.IsGranted) visible.Add(candidate);
                }
                rows = rows.Where(x => x.BranchId != null && visible.Contains(x.BranchId.Value));
            }
            if (branchId != null) rows = rows.Where(x => x.BranchId == branchId);
            if (active != null) rows = rows.Where(x => x.IsActive == active);
            if (!string.IsNullOrEmpty(search)) rows = rows.Where(x => x.TypeCode.Contains(search) || x.TypeName.Contains(search));
            var total = await rows.CountAsync(token);
            var ordered = (sort, descending) switch
            {
                ("name", false) => rows.OrderBy(x => x.TypeName), ("name", true) => rows.OrderByDescending(x => x.TypeName),
                ("status", false) => rows.OrderBy(x => x.IsActive), ("status", true) => rows.OrderByDescending(x => x.IsActive),
                (_, true) => rows.OrderByDescending(x => x.TypeCode), _ => rows.OrderBy(x => x.TypeCode)
            };
            var slice = await ordered.ThenBy(x => x.BranchId).ThenBy(x => x.RoomTypeId)
                .Skip((page - 1) * pageSize).Take(pageSize).TagWith("UC142:Page").ToArrayAsync(token);
            var items = new List<RoomTypeListItem>();
            foreach (var row in slice) items.Add(new(RoomTypeDto(row), await db.Rooms.CountAsync(x => x.RoomTypeId == row.RoomTypeId && x.IsActive
                && (row.BranchId == null || x.BranchId == row.BranchId), token), row.BranchId != null && row.IsActive));
            foreach (var branch in visible)
            {
                var access = await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE", branch, token);
                if (!access.IsGranted) return new(access.Decision == PermissionDecision.Unavailable ? RoomTypeBatchStatus.Unavailable : RoomTypeBatchStatus.Forbidden);
            }
            if (branchId == null && global.IsGranted && !(await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE", token)).IsGranted)
                return new(RoomTypeBatchStatus.Forbidden);
            return new(RoomTypeBatchStatus.Success, new RoomTypePage(items, total, page, pageSize, branchId, active, search, sort, descending));
        }, ct, allowScopedList: true);
    }

    private async Task BatchAuditAsync(long actor, long branch, string entity, long id, object? before, object after, string[] fields, CancellationToken ct)
    {
        db.Add(new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = entity, EntityId = id.ToString(CultureInfo.InvariantCulture),
            Action = before == null ? "CREATE" : "UPDATE", OccurredAt = clock.GetUtcNow().UtcDateTime,
            OldValues = before == null ? JsonSerializer.Serialize(new { entity = (object?)null }, Json) : JsonSerializer.Serialize(before, Json),
            NewValues = JsonSerializer.Serialize(after, Json), ChangedFields = JsonSerializer.Serialize(fields, Json) });
        try { await db.SaveChangesAsync(ct); }
        finally { db.ChangeTracker.Clear(); }
    }
}
