using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rooms;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Rooms;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class RoomHistoryAttributionTests
{
    [Fact]
    public void History_is_same_room_ordered_and_preserves_nullable_recorded_actor()
    {
        var time = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        var rows = new[] {
            new RoomStatusHistory { HistoryId = 1, RoomId = 7, ChangedAt = time, ChangedBy = 8, ToStatus = "DIRTY" },
            new RoomStatusHistory { HistoryId = 2, RoomId = 7, ChangedAt = time, ChangedBy = null, ToStatus = "READY" },
            new RoomStatusHistory { HistoryId = 3, RoomId = 9, ChangedAt = time.AddDays(1), ChangedBy = 99, ToStatus = "DIRTY" }
        }.AsQueryable();
        var result = RoomOperationalReadQueries.RecordedHistory(rows, 7).ToArray();
        Assert.Equal(new long[] { 2, 1 }, result.Select(r => r.Id));
        Assert.Null(result[0].ChangedByUserId);
        Assert.Equal(8L, result[1].ChangedByUserId);
        Assert.DoesNotContain(result, r => r.ChangedByUserId == 99);
        Assert.Empty(RoomOperationalReadQueries.RecordedHistory(rows, 10));
    }

    [Fact]
    public void Sql_projection_reads_changed_by_only_from_authorized_history_without_user_profile_join()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var query = RoomOperationalReadQueries.RecordedHistory(db.RoomStatusHistories.AsNoTracking(), 7);
        var sql = query.Skip(20).Take(20).ToQueryString();
        Assert.Contains("[changed_by]", sql);
        Assert.Contains("[room_id] =", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("[changed_at] DESC", sql);
        Assert.Contains("[history_id] DESC", sql);
        Assert.DoesNotContain("JOIN", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[users]", sql);
        var latestSql = query.Take(1).ToQueryString();
        Assert.Contains("SELECT TOP(", latestSql);
        Assert.Contains("[changed_at] DESC", latestSql);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(8L)]
    public void Dto_serializes_recorded_actor_without_profile_or_inference(long? actor)
    {
        var value = new RoomStatusEventDto(1, null, "DIRTY", DateTime.UtcNow, null,
            new RoomStatusSourceDto("INIT", null, "NOT_RECORDED"), actor);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var recorded = json.RootElement.GetProperty("changedByUserId");
        if (actor is null) Assert.Equal(JsonValueKind.Null, recorded.ValueKind);
        else Assert.Equal(actor.Value, recorded.GetInt64());
        Assert.False(json.RootElement.TryGetProperty("username", out _));
    }
}
