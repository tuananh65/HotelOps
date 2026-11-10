using HotelOps.Api.DTOs.Rooms;
using HotelOps.Api.Entities;

namespace HotelOps.Api.Services.Rooms;

public static class RoomOperationalReadQueries
{
    // Only recorded attribution: no user-profile lookup, role inference or current-user fallback.
    // Caller must authorize the room before querying these same-room history facts.
    public static IQueryable<RoomRecordedStatusRow> RecordedHistory(
        IQueryable<RoomStatusHistory> events, long authorizedRoomId) =>
        events.Where(e => e.RoomId == authorizedRoomId)
            .OrderByDescending(e => e.ChangedAt).ThenByDescending(e => e.HistoryId)
            .Select(e => new RoomRecordedStatusRow(e.HistoryId, e.FromStatus, e.ToStatus,
                e.ChangedAt, e.Reason, e.TriggerType, e.TriggerId, e.ChangedBy));

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

public sealed record RoomRecordedStatusRow(long Id, string? From, string To, DateTime At,
    string? Reason, string? Type, long? SourceId, long? ChangedByUserId);
