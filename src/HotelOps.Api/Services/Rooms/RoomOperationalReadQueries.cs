using HotelOps.Api.DTOs.Rooms;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Rooms;

public static class RoomOperationalReadQueries
{
    // Caller must supply the already authorized and filtered room query.
    public static IQueryable<RoomStatusCountDto> StatusCounts(IQueryable<Room> scopedRooms) =>
        scopedRooms.GroupBy(r => r.OperationalStatus)
            .OrderBy(g => g.Key)
            .Select(g => new RoomStatusCountDto(g.Key, g.LongCount()));

    public static IQueryable<RoomUnreleasedBlockDto> UnreleasedBlocks(
        IQueryable<RoomBlock> blocks, long authorizedRoomId) =>
        blocks.Where(b => b.RoomId == authorizedRoomId && b.ReleasedAt == null)
            .OrderByDescending(b => b.BlockedAt).ThenByDescending(b => b.BlockId)
            .Select(b => new RoomUnreleasedBlockDto(b.BlockId, b.Reason, b.BlockedAt, b.ExpectedReleaseAt));
}
