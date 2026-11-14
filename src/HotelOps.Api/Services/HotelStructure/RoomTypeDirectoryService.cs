using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public enum RoomTypeDirectoryStatus { Success, SessionInvalid, Forbidden, NotFound, Inactive, SchemaRequired, Unavailable }
public sealed record RoomTypeDirectoryResult(RoomTypeDirectoryStatus Status, object? Value = null, string? ETag = null);

/// <summary>UC142 read extraction from Task70; no UC017/018/019 writers or schema mutation.</summary>
public sealed class RoomTypeDirectoryService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions)
{
    private const string Permission = "MASTER_DATA_MANAGE";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<RoomTypeDirectoryResult> ListAsync(SessionReference ticket, long sessionId, long? branchId,
        bool? active, string? search, int page, int pageSize, string sort, bool descending, CancellationToken ct)
    {
        search = search?.Trim();
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize
            || search?.Length > 150 || search?.Any(char.IsControl) == true
            || sort is not ("code" or "name" or "status"))
            throw new ValidationException("Invalid list filters or pagination.");
        return ReadAsync(ticket, sessionId, branchId, async token =>
        {
            var rows = Rows();
            var global = await permissions.EvaluateGlobalAsync(Permission, token);
            if (global.Decision == PermissionDecision.Unavailable) return new(RoomTypeDirectoryStatus.Unavailable);
            var visible = new List<long>();
            if (branchId is { } branch) rows = rows.Where(x => x.BranchId == branch);
            else if (!global.IsGranted)
            {
                foreach (var id in await db.Branches.AsNoTracking().Select(x => x.BranchId).ToArrayAsync(token))
                {
                    var access = await permissions.EvaluateBranchAsync(Permission, id, token);
                    if (access.Decision == PermissionDecision.Unavailable) return new(RoomTypeDirectoryStatus.Unavailable);
                    if (access.IsGranted) visible.Add(id);
                }
                rows = rows.Where(x => x.BranchId != null && visible.Contains(x.BranchId.Value));
            }
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
                .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(token);
            var items = new List<RoomTypeListItem>();
            foreach (var row in slice)
                items.Add(new(Details(row), await ActiveRoomsAsync(row.RoomTypeId, row.BranchId, token), row.BranchId != null && row.IsActive));
            foreach (var id in visible)
            {
                var access = await permissions.EvaluateBranchAsync(Permission, id, token);
                if (!access.IsGranted) return new(access.Decision == PermissionDecision.Unavailable
                    ? RoomTypeDirectoryStatus.Unavailable : RoomTypeDirectoryStatus.Forbidden);
            }
            if (branchId == null && global.IsGranted)
            {
                var final = await permissions.EvaluateGlobalAsync(Permission, token);
                if (!final.IsGranted) return new(final.Decision == PermissionDecision.Unavailable
                    ? RoomTypeDirectoryStatus.Unavailable : RoomTypeDirectoryStatus.Forbidden);
            }
            return new(RoomTypeDirectoryStatus.Success,
                new RoomTypePage(items, total, page, pageSize, branchId, active, search, sort, descending));
        }, ct);
    }

    public Task<RoomTypeDirectoryResult> DetailAsync(SessionReference ticket, long sessionId,
        long? branchId, long id, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Id must be positive.");
        return ReadAsync(ticket, sessionId, branchId, async token =>
        {
            if (branchId == null)
            {
                var global = await permissions.EvaluateGlobalAsync(Permission, token);
                if (!global.IsGranted) return new(global.Decision == PermissionDecision.Unavailable
                    ? RoomTypeDirectoryStatus.Unavailable : RoomTypeDirectoryStatus.Forbidden);
            }
            var row = await Rows().SingleOrDefaultAsync(x => x.RoomTypeId == id && x.BranchId == branchId, token);
            if (row == null) return new(RoomTypeDirectoryStatus.NotFound);
            var value = Details(row);
            var key = id.ToString(CultureInfo.InvariantCulture);
            var revision = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "room_types" && x.EntityId == key)
                .Select(x => (long?)x.AuditId).MaxAsync(token) ?? 0;
            var activeRooms = await ActiveRoomsAsync(id, row.BranchId, token);
            var tag = '"' + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { value, revision, activeRooms }, Json))) + '"';
            return new(RoomTypeDirectoryStatus.Success, new RoomTypeListItem(value, activeRooms, branchId != null && value.IsActive), tag);
        }, ct, allowScopedList: false);
    }

    private async Task<RoomTypeDirectoryResult> ReadAsync(SessionReference ticket, long sessionId, long? branchId,
        Func<CancellationToken, Task<RoomTypeDirectoryResult>> read, CancellationToken ct, bool allowScopedList = true)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        if (branchId <= 0) throw new ValidationException("BranchId must be positive.");
        ct.ThrowIfCancellationRequested();
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sessionId
            || actor.IsRestricted || ticket.IsRestricted) return new(RoomTypeDirectoryStatus.SessionInvalid);
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null) throw new InvalidOperationException("A clean local SQL context is required.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var locked = await new UserEligibilityLock(db).LockAsync([actor.UserId], ct);
            if (!locked.TryGetValue(actor.UserId, out var user) || user is null || user.DeletedAt != null
                || user.AccountStatus != "ACTIVE" || user.EmploymentStatus != "ACTIVE") return new(RoomTypeDirectoryStatus.SessionInvalid);
            if (await Gate() is { } denied) return new(denied);
            if (branchId is { } branch)
            {
                var parent = await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={branch}")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (parent == null) return new(RoomTypeDirectoryStatus.NotFound);
                if (!parent.IsActive) return new(RoomTypeDirectoryStatus.Inactive);
            }
            var locks = await db.Database.SqlQuery<int>($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:RoomTypes:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r AS [Value];").ToArrayAsync(ct);
            if (locks.Length != 1 || locks[0] < 0) return new(RoomTypeDirectoryStatus.Unavailable);
            if (await RoomTypeDirectorySchema.ReadAsync(db, ct) != 1) return new(RoomTypeDirectoryStatus.SchemaRequired);
            var result = await read(ct);
            if (result.Status != RoomTypeDirectoryStatus.Success) return result;
            if (await Gate() is { } final) return new(final);
            await tx.CommitAsync(ct);
            return result;
        }
        catch (Exception error) when (!ct.IsCancellationRequested && SqlNumber(error) is 1205 or 1222 or -2 or 207 or 208)
        { return new(RoomTypeDirectoryStatus.Unavailable); }
        catch (Exception error) when (ct.IsCancellationRequested && error is System.Data.Common.DbException or DbUpdateException or SessionStoreUnavailableException)
        { throw new OperationCanceledException("Room type directory cancelled.", error, ct); }

        async Task<RoomTypeDirectoryStatus?> Gate()
        {
            var valid = await sessions.ValidateSessionAsync(ticket, ct);
            if (!valid.IsValid || valid.Session?.SessionId != sessionId || valid.Session.IsRestricted) return RoomTypeDirectoryStatus.SessionInvalid;
            var access = branchId is { } branch ? await permissions.EvaluateBranchAsync(Permission, branch, ct)
                : await permissions.EvaluateGlobalAsync(Permission, ct);
            ct.ThrowIfCancellationRequested();
            if (access.IsGranted) return null;
            if (access.Decision == PermissionDecision.Unavailable) return RoomTypeDirectoryStatus.Unavailable;
            if (branchId == null && allowScopedList)
                foreach (var id in await db.Branches.AsNoTracking().Select(x => x.BranchId).ToArrayAsync(ct))
                {
                    var scoped = await permissions.EvaluateBranchAsync(Permission, id, ct);
                    if (scoped.Decision == PermissionDecision.Unavailable) return RoomTypeDirectoryStatus.Unavailable;
                    if (scoped.IsGranted) return null;
                }
            return RoomTypeDirectoryStatus.Forbidden;
        }
    }

    private IQueryable<RoomTypeDirectoryRow> Rows() => db.Database.SqlQueryRaw<RoomTypeDirectoryRow>("""
        SELECT room_type_id AS RoomTypeId,branch_id AS BranchId,type_code AS TypeCode,type_name AS TypeName,
        description AS Description,standard_cleaning_minutes AS StandardCleaningMinutes,
        default_checklist_id AS DefaultChecklistId,standard_capacity AS StandardCapacity,is_active AS IsActive
        FROM dbo.room_types
        """);
    private Task<int> ActiveRoomsAsync(long id, long? branch, CancellationToken ct) => db.Rooms.AsNoTracking()
        .CountAsync(x => x.RoomTypeId == id && x.IsActive && (branch == null || x.BranchId == branch), ct);
    private static RoomTypeDetailsDto Details(RoomTypeDirectoryRow row) => new(row.RoomTypeId, row.BranchId, row.TypeCode,
        row.TypeName, row.Description, row.StandardCleaningMinutes, row.DefaultChecklistId, row.StandardCapacity, row.IsActive);
    private static int? SqlNumber(Exception error)
    { for (Exception? e = error; e != null; e = e.InnerException) if (e is SqlException sql) return sql.Number; return null; }
    private sealed class RoomTypeDirectoryRow
    {
        public long RoomTypeId { get; set; }
        public long? BranchId { get; set; }
        public string TypeCode { get; set; } = "";
        public string TypeName { get; set; } = "";
        public string? Description { get; set; }
        public int? StandardCleaningMinutes { get; set; }
        public long? DefaultChecklistId { get; set; }
        public int? StandardCapacity { get; set; }
        public bool IsActive { get; set; }
    }
}
