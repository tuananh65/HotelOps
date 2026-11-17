using System.Text.Json;
using HotelOps.Api.Data;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Rooms;
internal static class RoomReadinessHistory
{
    internal sealed record Snapshot(EffectiveRoomState State, string PhysicalStatus);
    internal static async Task<Snapshot> CaptureAsync(HotelOpsDbContext db, long room, CancellationToken ct, DateTime? eventTime = null)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Room readiness snapshot requires the source transaction.");
        var physical = await db.Rooms.FromSqlInterpolated($"SELECT * FROM dbo.rooms WITH(UPDLOCK,HOLDLOCK) WHERE room_id={room}")
            .AsNoTracking().Select(r => r.OperationalStatus).SingleAsync(ct);
        var now = eventTime ?? DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct), DateTimeKind.Utc);
        var state = (await new EffectiveRoomReadiness(db).ReadAsync([room], now, ct))[room];
        return new(state, physical);
    }

    internal static async Task FinishAsync(HotelOpsDbContext db, Snapshot before, string sourceType,
        long sourceId, long actor, DateTime occurredAt, string? reason, CancellationToken ct)
    {
        var room = before.State.RoomId;
        var physical = await db.Rooms.AsNoTracking().Where(r => r.RoomId == room).Select(r => r.OperationalStatus).SingleAsync(ct);
        var after = (await new EffectiveRoomReadiness(db).ReadAsync([room], occurredAt, ct))[room];
        await RecordAsync(db, before.State, after, before.PhysicalStatus, physical, sourceType, sourceId, actor, occurredAt, reason, ct);
    }

    // Internal participant: source owns authorization, room/block locks and commit.
    internal static async Task RecordAsync(HotelOpsDbContext db, EffectiveRoomState before,
        EffectiveRoomState after, string physicalBefore, string physicalAfter, string sourceType,
        long sourceId, long actor, DateTime occurredAt, string? reason, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable
            || before.RoomId <= 0 || before.RoomId != after.RoomId || sourceId <= 0 || actor <= 0
            || occurredAt.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Readiness history requires the source's serializable transaction and attribution.");
        if (before.Readiness == after.Readiness && physicalBefore == physicalAfter) return;
        // Missing storage rolls back the transition. Never put readiness into physical codes.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF ISNULL(COL_LENGTH('dbo.room_status_history','effective_from'),0)<>40
                OR ISNULL(COL_LENGTH('dbo.room_status_history','effective_to'),0)<>40
                THROW 51990,'ROOM_READINESS_HISTORY_REQUIRED',1;
            INSERT dbo.room_status_history(room_id,from_status,to_status,trigger_type,trigger_id,changed_by,changed_at,reason,effective_from,effective_to)
            VALUES({before.RoomId},{physicalBefore},{physicalAfter},{sourceType},{sourceId},{actor},{occurredAt},{reason},{before.Readiness},{after.Readiness});
            """, ct);
    }

    internal sealed class Row
    {
        public long Id {get;set;}
        public string? EffectiveFrom {get;set;}
        public string? EffectiveTo {get;set;}
    }
    internal static async Task<Dictionary<long,Row>> ReadAsync(HotelOpsDbContext db,long room,long[] historyIds,CancellationToken ct)
    {
        if(historyIds.Length==0) return [];
        var installed=await db.Database.SqlQuery<int>($"SELECT CASE WHEN COL_LENGTH('dbo.room_status_history','effective_from')=40 AND COL_LENGTH('dbo.room_status_history','effective_to')=40 THEN 1 ELSE 0 END AS Value").SingleAsync(ct);
        if(installed!=1) return [];
        var ids=JsonSerializer.Serialize(historyIds);
        var rows=await db.Database.SqlQuery<Row>($"""
            SELECT history_id AS Id,effective_from AS EffectiveFrom,effective_to AS EffectiveTo
            FROM dbo.room_status_history WHERE room_id={room} AND history_id IN(SELECT CONVERT(bigint,value) FROM OPENJSON({ids}))
            """).ToArrayAsync(ct);
        return rows.ToDictionary(r=>r.Id);
    }
}
