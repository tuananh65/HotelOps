using HotelOps.Api.Data;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleanerAttributionTests
{
    private static CleanerAttribution Source => new(1,2,3,4,5,6,
        new DateTime(2026,11,9,1,0,0,DateTimeKind.Utc),[0,0,0,0,0,0,0,7],8);

    [Fact]
    public void Single_cleaner_and_repeated_same_user_history_are_unambiguous()
    {
        Assert.True(CleanerAttributionContract.UniqueCleaner(5,[5],[5]));
        Assert.True(CleanerAttributionContract.UniqueCleaner(5,[5,5],[5,5,5]));
        Assert.True(CleanerAttributionContract.Matches(Source,1,2,3,1,Source.CompletionRevision,true,true));
    }

    [Fact]
    public void Reassignment_to_different_cleaner_before_completion_is_not_primary_authority()
    {
        Assert.False(CleanerAttributionContract.UniqueCleaner(9,[5,9],[5,9]));
        Assert.False(CleanerAttributionContract.UniqueCleaner(9,[5,9],[9]));
        Assert.False(CleanerAttributionContract.UniqueCleaner(5,[],[5]));
        Assert.False(CleanerAttributionContract.UniqueCleaner(5,[5],[5,9]));
        Assert.False(CleanerAttributionContract.UniqueCleaner(0,[0],[0]));
    }

    [Fact]
    public void Original_binding_does_not_take_a_rework_assignee_or_assigner_as_input()
    {
        var original = Source;
        // A later rework assignee is deliberately outside the attribution contract.
        Assert.Equal(5, original.CleanerUserId);
        Assert.True(CleanerAttributionContract.Matches(original,1,2,3,1,original.CompletionRevision,true,true));
    }

    [Theory]
    [InlineData("branch")][InlineData("cycle")][InlineData("superseded-task")][InlineData("room")]
    [InlineData("revoked")][InlineData("inactive")][InlineData("source-revision")][InlineData("malformed-revision")]
    [InlineData("missing-source")][InlineData("missing-assignment")][InlineData("missing-progress")][InlineData("transaction")]
    public void Mismatched_or_unavailable_binding_fails_closed(string scenario)
    {
        var p = Source;
        p = scenario switch
        {
            "missing-assignment" => p with { AssignmentId = 0 },
            "missing-progress" => p with { CompletionProgressId = 0 },
            "transaction" => p with { WriterTransactionId = 0 },
            _ => p
        };
        Assert.False(CleanerAttributionContract.Matches(p,scenario == "superseded-task" ? 99 : 1,
            scenario == "room" ? 99 : 2,scenario == "branch" ? 99 : 3,scenario == "cycle" ? 99 : 1,
            scenario == "source-revision" ? [0,0,0,0,0,0,0,9] : scenario == "malformed-revision" ? [] : p.CompletionRevision,
            scenario != "missing-source",scenario is not ("revoked" or "inactive")));
    }

    [Fact]
    public async Task Reader_rejects_missing_caller_transaction_before_any_SQL()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer().Options);
        var service = new RoomTypeCreationService(db,null!,null!,null!,TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadInspectionCleanerAttributionAsync(
            db.Database.GetDbConnection(),null!,1,3,default));
    }
}
