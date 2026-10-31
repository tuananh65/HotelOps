using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    private static string Batch2Reason(string? reason)
    {
        var value = reason?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length is < 10 or > 1000 || value.Any(char.IsControl))
            throw new ValidationException("A reason of 10-1000 characters without control characters is required.");
        return value;
    }
    private static void Batch2Tag(string? tag)
    {
        if (!HotelStructureUpdateService.IsStrongTag(tag)) throw new ValidationException("A single strong ETag is required.");
    }
    public Task<RoomTypeBatchResult> DeactivateRoomTypeAsync(SessionReference ticket, long sid, long branch,
        long id, string expected, DeactivateRoomTypeRequest request, CancellationToken ct)
    {
        if (id <= 0 || !request.Confirmation) throw new ValidationException("Positive Id and confirmation are required.");
        var reason = Batch2Reason(request.Reason); Batch2Tag(expected);
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            var row = await db.BranchRoomTypes.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branch && x.RoomTypeId == id, token);
            if (row == null) return new(RoomTypeBatchStatus.NotFound);
            var before = RoomTypeDto(row);
            if (!before.IsActive) return new(RoomTypeBatchStatus.Inactive);
            if (expected != await RoomTypeTagAsync(before, token)) return new(RoomTypeBatchStatus.Stale);
            var rooms = await db.Rooms.AsNoTracking().Where(x => x.RoomTypeId == id && x.IsActive)
                .OrderBy(x => x.RoomId).Select(x => new ActiveRoomReferenceDto(x.RoomId, x.RoomNumber)).ToArrayAsync(token);
            if (rooms.Length != 0) return new(RoomTypeBatchStatus.DependenciesExist, rooms);
            await db.Database.ExecuteSqlInterpolatedAsync($"/* UC019:Update */ UPDATE dbo.room_types SET is_active=0 WHERE room_type_id={id} AND branch_id={branch}", token);
            var after = before with { IsActive = false };
            await BatchAuditAsync(actor, branch, "room_types", id, before, new { record = after, reason }, ["isActive"], token);
            return new(RoomTypeBatchStatus.Success, after, await RoomTypeTagAsync(after, token));
        }, ct);
    }
    public Task<RoomTypeBatchResult> ReadEditableRoomAsync(SessionReference ticket, long sid, long branch, long id, CancellationToken ct)
        => EditRoomAsync(ticket, sid, branch, id, null, null, ct);
    public Task<RoomTypeBatchResult> UpdateRoomInformationAsync(SessionReference ticket, long sid, long branch, long id,
        string expected, UpdateRoomInformationRequest request, CancellationToken ct)
    {
        Batch2Tag(expected); var reason = Batch2Reason(request.Reason);
        var number = request.RoomNumber?.Trim().ToUpperInvariant(); var notes = request.Notes?.Trim();
        if (request.FloorId <= 0 || request.RoomTypeId <= 0 || request.AreaId <= 0 || string.IsNullOrEmpty(number) || number.Length > 30
            || number.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_') || notes?.Length > 4000
            || notes?.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') == true)
            throw new ValidationException("Invalid room fields.");
        return EditRoomAsync(ticket, sid, branch, id, expected, request with { RoomNumber = number, Notes = string.IsNullOrEmpty(notes) ? null : notes, Reason = reason }, ct);
    }
    private Task<RoomTypeBatchResult> EditRoomAsync(SessionReference ticket, long sid, long branch, long id,
        string? expected, UpdateRoomInformationRequest? input, CancellationToken ct)
    {
        if (id <= 0) throw new ValidationException("Id must be positive.");
        return BatchAsync(ticket, sid, branch, async (actor, token) =>
        {
            var room = await db.Rooms.AsNoTracking().Where(x => x.RoomId == id && x.BranchId == branch
                && x.Floor.BranchId == branch && (x.AreaId == null || x.Area != null && x.Area.BranchId == branch
                    && (x.Area.FloorId == null || x.Area.FloorId == x.FloorId)))
                .Where(x => db.BranchRoomTypes.Any(t => t.RoomTypeId == x.RoomTypeId && (t.BranchId == null || t.BranchId == branch)))
                .SingleOrDefaultAsync(token);
            if (room == null) return new(RoomTypeBatchStatus.NotFound);
            var before = new CreatedRoomDto(room.RoomId, room.BranchId, room.FloorId, room.AreaId, room.RoomTypeId,
                room.RoomNumber, room.Description, room.OperationalStatus, room.IsActive);
            var tag = await RoomTag(before, room.UpdatedAt);
            if (input == null) return new(RoomTypeBatchStatus.Success, before, tag);
            if (!room.IsActive) return new(RoomTypeBatchStatus.Inactive);
            if (expected != tag) return new(RoomTypeBatchStatus.Stale);
            var selected = await db.BranchRoomTypes.AsNoTracking().SingleOrDefaultAsync(x => x.RoomTypeId == input.RoomTypeId, token);
            if (selected == null || !selected.IsActive || !(selected.BranchId == branch || selected.BranchId == null && input.RoomTypeId == room.RoomTypeId)
                || !await db.Floors.AsNoTracking().AnyAsync(x => x.FloorId == input.FloorId && x.BranchId == branch && x.IsActive, token)
                || input.AreaId is { } area && !await db.Areas.AsNoTracking().AnyAsync(x => x.AreaId == area && x.BranchId == branch && x.IsActive
                    && (x.FloorId == null || x.FloorId == input.FloorId), token)) return new(RoomTypeBatchStatus.InvalidReference);
            if (await db.Rooms.AsNoTracking().AnyAsync(x => x.RoomId != id && x.BranchId == branch && x.RoomNumber.Trim().ToUpper() == input.RoomNumber, token))
                return new(RoomTypeBatchStatus.Duplicate);
            var now = clock.GetUtcNow().UtcDateTime;
            await db.Database.ExecuteSqlInterpolatedAsync($"/* UC021:Update */ UPDATE dbo.rooms SET floor_id={input.FloorId},area_id={input.AreaId},room_type_id={input.RoomTypeId},room_number={input.RoomNumber},description={input.Notes},updated_at={now} WHERE room_id={id} AND branch_id={branch}", token);
            var after = before with { FloorId = input.FloorId, AreaId = input.AreaId, RoomTypeId = input.RoomTypeId, RoomNumber = input.RoomNumber, Notes = input.Notes };
            await BatchAuditAsync(actor, branch, "rooms", id, before, new { record = after, reason = input.Reason }, ["floorId", "areaId", "roomTypeId", "roomNumber", "notes"], token);
            var stored = await db.Rooms.AsNoTracking().Where(x => x.RoomId == id).Select(x => x.UpdatedAt).SingleAsync(token);
            return new(RoomTypeBatchStatus.Success, after, await RoomTag(after, stored));
            async Task<string> RoomTag(CreatedRoomDto value, DateTime? updated)
            {
                var key = id.ToString(CultureInfo.InvariantCulture);
                var revision = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "rooms" && x.EntityId == key).Select(x => (long?)x.AuditId).MaxAsync(token) ?? 0;
                return '"' + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { value, updated, revision }, Json))) + '"';
            }
        }, ct);
    }
}
