using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Rooms;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Rooms;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class RoomOperationalReadTests
{
    [Fact]
    public void Counts_respect_branch_and_filters_and_cover_more_than_one_page()
    {
        var rooms = Enumerable.Range(1, 25).Select(i => new Room { RoomId = i, BranchId = 1, FloorId = 2, OperationalStatus = "DIRTY" })
            .Concat([new Room { BranchId = 2, FloorId = 2, OperationalStatus = "READY" },
                new Room { BranchId = 1, FloorId = 3, OperationalStatus = "READY" }]).AsQueryable();
        var counts = RoomOperationalReadQueries.StatusCounts(rooms.Where(r => r.BranchId == 1 && r.FloorId == 2)).ToArray();
        Assert.Equal(new RoomStatusCountDto("DIRTY", 25), Assert.Single(counts));
        Assert.Empty(RoomOperationalReadQueries.StatusCounts(rooms.Where(r => r.BranchId == 3)));
    }

    [Fact]
    public void Blocks_exclude_other_rooms_and_released_records_without_inventing_expiry()
    {
        var now = DateTime.UtcNow;
        var blocks = new[] {
            new RoomBlock { BlockId = 1, RoomId = 7, Reason = "old estimate", BlockedAt = now.AddDays(-2), ExpectedReleaseAt = now.AddDays(-1) },
            new RoomBlock { BlockId = 2, RoomId = 7, Reason = "released", ReleasedAt = now },
            new RoomBlock { BlockId = 3, RoomId = 8, Reason = "other room" }
        }.AsQueryable();
        var block = Assert.Single(RoomOperationalReadQueries.UnreleasedBlocks(blocks, 7));
        Assert.Equal(1, block.Id);
        Assert.Equal("old estimate", block.Reason);
    }

    [Fact]
    public void Read_queries_translate_with_SQL_Server_without_connecting()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);
        var counts = RoomOperationalReadQueries.StatusCounts(db.Rooms.AsNoTracking().Where(r => r.BranchId == 7)).ToQueryString();
        Assert.Contains("COUNT_BIG", counts);
        Assert.Contains("GROUP BY", counts);
        Assert.Contains("branch_id", counts);
        var blocks = RoomOperationalReadQueries.UnreleasedBlocks(db.RoomBlocks.AsNoTracking(), 9).ToQueryString();
        Assert.Contains("[released_at] IS NULL", blocks);
        Assert.Contains("[room_id] =", blocks);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(2, false, false)]
    [InlineData(1, true, false)]
    [InlineData(1, false, true)]
    public async Task Invalid_actor_cannot_reach_database(long userId, bool actorRestricted, bool ticketRestricted)
    {
        var current = new TestActor(userId, actorRestricted);
        var service = new RoomDirectoryService(null!, current, null!, null!);
        var ticket = SessionReference.FromProtectedTicket(1, new string('A', 43), ticketRestricted, DateTime.UtcNow);
        var result = await service.ListAsync(ticket, new RoomDirectoryQuery { BranchId = 1 }, default);
        Assert.Equal(RoomDirectoryStatus.SessionInvalid, result.Status);
        Assert.Null(result.Directory);
        Assert.Null(result.Detail);
    }

    private sealed class TestActor(long userId, bool restricted) : ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user)
        {
            user = new(userId, 1, restricted);
            return userId != 0;
        }
    }
    [Theory]
    [InlineData("Pending Cleaning")]
    [InlineData("BLOCKED")]
    [InlineData("INACTIVE")]
    public void Derived_readiness_is_not_a_stored_operational_filter(string status)
    {
        var query = new RoomDirectoryQuery { BranchId = 1, OperationalStatusCode = status };
        Assert.False(Validator.TryValidateObject(query, new ValidationContext(query), [], true));
    }
}
