using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Rooms;
internal static class RoomReadinessHistory
{
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
