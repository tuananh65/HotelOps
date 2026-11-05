using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public Task<RoomTypeBatchResult> CreateRoomAsync(SessionReference ticket, long sessionId, CreateRoomRequest input, CancellationToken ct)
    {
        var number = input.RoomNumber?.Trim().ToUpperInvariant(); var notes = input.Notes?.Trim();
        if (input.FloorId <= 0 || input.RoomTypeId <= 0 || input.AreaId <= 0 || string.IsNullOrEmpty(number) || number.Length > 30
            || number.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_') || notes?.Length > 4000
            || notes?.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t') == true)
            throw new ValidationException("Invalid room fields.");
        return BatchAsync(ticket, sessionId, input.BranchId, async (actor, token) =>
        {
            // RoomType precedes Room, matching UC017's catalog serialization. Parent read
            // locks are retained until the room, initial history and audit have all committed.
            if (!await db.BranchRoomTypes.AsNoTracking().AnyAsync(x => x.RoomTypeId == input.RoomTypeId && x.BranchId == input.BranchId && x.IsActive, token)
                || !await db.Floors.AsNoTracking().AnyAsync(x => x.FloorId == input.FloorId && x.BranchId == input.BranchId && x.IsActive, token)
                || input.AreaId is { } area && !await db.Areas.AsNoTracking().AnyAsync(x => x.AreaId == area && x.BranchId == input.BranchId
                    && x.IsActive && (x.FloorId == null || x.FloorId == input.FloorId), token)) return new(RoomTypeBatchStatus.InvalidReference);
            if (await db.Rooms.AsNoTracking().AnyAsync(x => x.BranchId == input.BranchId && x.RoomNumber.Trim().ToUpper() == number, token))
                return new(RoomTypeBatchStatus.Duplicate);
            // Existing SQL patch's DIRTY represents SRS Pending Cleaning. Never use EF's
            // legacy AVAILABLE default or accept a caller-supplied operational status.
            var room = new Room { BranchId = input.BranchId, FloorId = input.FloorId, AreaId = input.AreaId, RoomTypeId = input.RoomTypeId,
                RoomNumber = number, Description = string.IsNullOrEmpty(notes) ? null : notes, IsActive = true,
                OperationalStatus = "DIRTY", CreatedAt = clock.GetUtcNow().UtcDateTime };
            db.Add(room);
            await db.SaveChangesAsync(token);
            var value = new CreatedRoomDto(room.RoomId, room.BranchId, room.FloorId, room.AreaId, room.RoomTypeId, room.RoomNumber, room.Description, room.OperationalStatus, room.IsActive);
            db.Add(new RoomStatusHistory { RoomId = room.RoomId, FromStatus = null, ToStatus = room.OperationalStatus, TriggerType = "INIT",
                ChangedBy = actor, ChangedAt = clock.GetUtcNow().UtcDateTime });
            await BatchAuditAsync(actor, input.BranchId, "rooms", room.RoomId, null, value,
                ["branchId", "floorId", "areaId", "roomTypeId", "roomNumber", "notes", "operationalStatus", "isActive"], token);
            return new(RoomTypeBatchStatus.Success, value);
        }, ct);
    }
}
