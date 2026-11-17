using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed record RoomDepartmentInput(long[] DepartmentIds, string Reason);
public sealed record RoomDepartmentChoice(long Id, string Name, bool IsActive);
public sealed record RoomDepartmentView(long RoomId, long BranchId, long[] DepartmentIds, RoomDepartmentChoice[] Options);
public sealed partial class RoomTypeCreationService
{
    private sealed class MappingRow
    {
        public long DepartmentId { get; set; }
        public bool IsActive { get; set; }
        public byte[] Version { get; set; } = [];
    }
    public Task<RoomTypeBatchResult> RoomDepartmentsAsync(SessionReference ticket, long sid, long branch, long room,
        string? expected, RoomDepartmentInput? input, CancellationToken ct)
    {
        if (branch <= 0 || room <= 0) throw new ValidationException("Scoped room identity required.");
        var reason = input == null ? null : Batch2Reason(input.Reason);
        if (input != null && (input.DepartmentIds == null || input.DepartmentIds.Length > 200
            || input.DepartmentIds.Any(d => d <= 0) || input.DepartmentIds.Distinct().Count() != input.DepartmentIds.Length))
            throw new ValidationException("Distinct valid departments required (maximum 200).");
        if (input != null) Batch2Tag(expected);
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            if (!await RoomDepartmentSchema.InstalledAsync(db, token)) return new(RoomTypeBatchStatus.SchemaRequired);
            if (!await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room} AND branch_id={branch}")
                .AsNoTracking().AnyAsync(token)) return new(RoomTypeBatchStatus.NotFound);
            var options = await db.Departments.AsNoTracking().Where(d => d.BranchId == branch)
                .OrderBy(d => d.DepartmentId).Select(d => new RoomDepartmentChoice(d.DepartmentId, d.DepartmentName, d.IsActive)).ToArrayAsync(token);
            async Task<MappingRow[]> Rows() => await db.Database.SqlQuery<MappingRow>($"""
                SELECT department_id AS DepartmentId,is_active AS IsActive,CONVERT(varbinary(8),row_version) AS Version
                FROM dbo.room_department_mappings WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}
                """).OrderBy(m => m.DepartmentId).ToArrayAsync(token);
            string Tag(MappingRow[] rows) => '"' + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { room, rows }, Json))) + '"';
            var rows = await Rows();
            var before = rows.Where(m => m.IsActive).Select(m => m.DepartmentId).ToArray();
            if (input == null) return new(RoomTypeBatchStatus.Success, new RoomDepartmentView(room, branch, before, options), Tag(rows));
            if (expected != Tag(rows)) return new(RoomTypeBatchStatus.Stale);
            if (input.DepartmentIds.Any(id => !options.Any(o => o.Id == id && o.IsActive))) return new(RoomTypeBatchStatus.InvalidReference);
            var after = input.DepartmentIds.Order().ToArray();
            if (!before.SequenceEqual(after))
            {
                var audit = new AuditLog { ActorUserId = actor, BranchId = branch, EntityType = "ROOM_DEPARTMENTS",
                    EntityId = room.ToString(System.Globalization.CultureInfo.InvariantCulture), Action = "UPDATE",
                    OccurredAt = clock.GetUtcNow().UtcDateTime, OldValues = JsonSerializer.Serialize(before, Json),
                    NewValues = JsonSerializer.Serialize(new { departmentIds = after, reason }, Json), ChangedFields = "[\"departmentIds\"]" };
                db.Add(audit); await db.SaveChangesAsync(token); db.Entry(audit).State = EntityState.Detached;
                foreach (var old in rows)
                {
                    var active = after.Contains(old.DepartmentId);
                    if (active != old.IsActive) await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.room_department_mappings SET is_active={active},audit_id={audit.AuditId} WHERE room_id={room} AND department_id={old.DepartmentId}", token);
                }
                foreach (var id in after.Where(id => rows.All(r => r.DepartmentId != id)))
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.room_department_mappings(room_id,department_id,is_active,audit_id) VALUES({room},{id},1,{audit.AuditId})", token);
            }
            return new(RoomTypeBatchStatus.Success, new RoomDepartmentView(room, branch, after, options), Tag(await Rows()));
        }, ct, requireRoomTypeSchema: false, featurePermission: token => RoomTypeAdministrationAsync(branch, token));
    }
}
