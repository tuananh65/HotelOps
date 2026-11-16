using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class TurnoverInspectionProjectionTests
{
    private static readonly DateTime At = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static RoomInspection Row(long id, string status, string? result, int minute) =>
        new() { InspectionId = id, Status = status, Result = result, CreatedAt = At.AddMinutes(minute), InspectedAt = At.AddMinutes(minute) };

    [Fact]
    public void LatestRoundSupersedesFailureAndPendingRoundDoesNotReuseOldPass()
    {
        var rows = new[] { Row(1, "COMPLETED", "FAIL", 0), Row(2, "COMPLETED", "PASS", 1) };
        Assert.Equal("PASS", TurnoverInspectionProjection.Latest(rows.AsQueryable()).Single());
        Assert.Null(TurnoverInspectionProjection.Latest(rows.Append(Row(3, "ASSIGNED", "PASS", 2)).AsQueryable()).Single());
        Assert.Equal("FAIL", TurnoverInspectionProjection.Latest(rows.Append(Row(3, "COMPLETED", "FAIL", 1)).AsQueryable()).Single());
        Assert.Null(TurnoverInspectionProjection.Latest(new[] { Row(1, "COMPLETED", "READY", 0) }.AsQueryable()).Single());
        Assert.Empty(TurnoverInspectionProjection.Latest(Array.Empty<RoomInspection>().AsQueryable()));
    }

    [Fact]
    public void ProjectionTranslatesToBoundedSqlWithExactRoomAndCycle()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=True;TrustServerCertificate=True").Options);
        var sql = TurnoverInspectionProjection.Latest(db.RoomInspections.AsNoTracking()
            .Where(i => i.RoomId == 12 && i.CleaningTaskId == 34)).ToQueryString();
        Assert.Contains("TOP(", sql);
        Assert.Contains("[room_id] = CAST(12 AS bigint)", sql);
        Assert.Contains("[cleaning_task_id] = CAST(34 AS bigint)", sql);
        Assert.Contains("COMPLETED", sql);
        Assert.Contains("ORDER BY", sql);
    }
}
