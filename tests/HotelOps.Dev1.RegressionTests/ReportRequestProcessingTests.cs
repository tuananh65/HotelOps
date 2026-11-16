using HotelOps.Api.Services.Insights;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReportRequestProcessingTests
{
    private static readonly DateTime Origin = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static ReportRequestResponse.Event Event(long id, string type, int minute, string old, string next, bool assigned = false) =>
        new(id, type, old, next, Origin.AddMinutes(minute), assigned);
    private static ReportRequestResponse.Event[] Cycles => [
        Event(1, "ASSIGN", 1, "ACCEPTED", "IN_PROGRESS", true),
        Event(2, "REASSIGN", 5, "IN_PROGRESS", "IN_PROGRESS", true),
        Event(3, "COMPLETE", 11, "IN_PROGRESS", "CLOSED"),
        Event(4, "REOPEN", 31, "CLOSED", "IN_PROGRESS"),
        Event(5, "COMPLETE", 36, "IN_PROGRESS", "CLOSED")];

    [Fact]
    public void ReassignDoesNotResetAndClosedGapIsExcluded()
    {
        var result = ReportRequestProcessing.Read("CLOSED", Origin, Origin.AddMinutes(36), Origin.AddMinutes(36), Origin.AddHours(1), Cycles);
        Assert.Equal(900m, result.CompletionSeconds);
        Assert.Equal(2, result.CompletedCycles);
        Assert.Equal(1, result.ReopenCount);
    }

    [Fact]
    public void SingleCycleAndImmediateCompletionRemainMeasurable()
    {
        var events = new[] { Cycles[0], Cycles[2] with { At = Cycles[0].At } };
        var result = ReportRequestProcessing.Read("CLOSED", Origin, events[1].At, events[1].At, Origin.AddHours(1), events);
        Assert.Equal(0m, result.CompletionSeconds);
        Assert.Equal(1, result.CompletedCycles);
        Assert.Equal(0, result.ReopenCount);
    }

    [Fact]
    public void OpenReopenedCycleDoesNotBecomeACompletedRequest()
    {
        var result = ReportRequestProcessing.Read("IN_PROGRESS", Origin, null, null, Origin.AddHours(1), Cycles[..4]);
        Assert.Null(result.CompletionSeconds);
        Assert.Equal(1, result.CompletedCycles);
        Assert.Equal(1, result.ReopenCount);
        Assert.Equal("OPEN_PROCESSING_CYCLE", result.CompletionEvidence);
    }

    [Theory]
    [InlineData("no-history")]
    [InlineData("unproven-assignment")]
    [InlineData("unproven-reassignment")]
    [InlineData("missing-start")]
    [InlineData("missing-complete")]
    [InlineData("wrong-reopen-state")]
    [InlineData("future")]
    [InlineData("closure-mismatch")]
    [InlineData("duplicate")]
    public void IncompleteOrContradictoryHistoryReturnsNoDuration(string scenario)
    {
        var events = Cycles;
        var now = Origin.AddHours(1);
        var closed = Origin.AddMinutes(36);
        switch (scenario)
        {
            case "no-history": events = []; break;
            case "unproven-assignment": events[0] = events[0] with { AssignmentVerified = false }; break;
            case "unproven-reassignment": events[1] = events[1] with { AssignmentVerified = false }; break;
            case "missing-start": events = events[1..]; break;
            case "missing-complete": events = [events[0], events[3], events[4]]; break;
            case "wrong-reopen-state": events[3] = events[3] with { OldStatus = "ACCEPTED" }; break;
            case "future": now = Origin.AddMinutes(35); break;
            case "closure-mismatch": closed = closed.AddSeconds(1); break;
            case "duplicate": events = [.. events, events[4]]; break;
        }
        var result = ReportRequestProcessing.Read("CLOSED", Origin, closed, closed, now, events);
        Assert.Null(result.CompletionSeconds);
        Assert.Null(result.ReopenCount);
    }
}
