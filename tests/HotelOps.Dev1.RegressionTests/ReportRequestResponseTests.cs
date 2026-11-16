using HotelOps.Api.Services.Insights;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class ReportRequestResponseTests
{
    private static readonly DateTime Origin = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static ReportRequestResponse.Event Decision(string type = "ACCEPT") =>
        new(123, type, "PENDING_ACCEPTANCE", type == "ACCEPT" ? "ACCEPTED" : "REJECTED", Origin.AddSeconds(90.125));

    [Theory]
    [InlineData("ACCEPT")]
    [InlineData("REJECT")]
    public void ResponseUsesSubmittedTimeAndPreservesEventProvenance(string type)
    {
        var result = ReportRequestResponse.Read(Origin, Origin.AddSeconds(30), Origin.AddHours(1), [Decision(type)]);
        Assert.Equal(60.125m, result.ResponseSeconds);
        Assert.Equal(123, result.ResponseEventId);
        Assert.Equal(Decision(type).At, result.ResponseAt);
        Assert.Equal("RECORDED_DECISION", result.ResponseEvidence);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("reopen")]
    [InlineData("closed-transition")]
    [InlineData("wrong-transition")]
    [InlineData("missing-submitted")]
    [InlineData("before-created")]
    [InlineData("before-submitted")]
    [InlineData("future-decision")]
    public void MissingOrInconsistentEvidenceDoesNotInventZeroDuration(string scenario)
    {
        DateTime? submitted = Origin;
        var now = Origin.AddHours(1);
        ReportRequestResponse.Event[] events = [Decision()];
        switch (scenario)
        {
            case "missing": events = []; break;
            case "duplicate": events = [Decision(), Decision("REJECT")]; break;
            case "reopen": events = [Decision(), new(124, "REOPEN", "CLOSED", "IN_PROGRESS", Origin.AddMinutes(2))]; break;
            case "closed-transition": events = [Decision(), new(124, "LEGACY", "CLOSED", "IN_PROGRESS", Origin.AddMinutes(2))]; break;
            case "wrong-transition": events = [Decision() with { NewStatus = "CLOSED" }]; break;
            case "missing-submitted": submitted = null; break;
            case "before-created": submitted = Origin.AddTicks(-1); break;
            case "before-submitted": submitted = Origin.AddMinutes(2); break;
            case "future-decision": now = Origin.AddSeconds(90); break;
        }
        var result = ReportRequestResponse.Read(Origin, submitted, now, events);
        Assert.Null(result.ResponseSeconds);
        Assert.Null(result.ResponseAt);
        Assert.Null(result.ResponseEventId);
        Assert.NotEqual("RECORDED_DECISION", result.ResponseEvidence);
    }

    [Fact]
    public void RealImmediateDecisionCanBeZero()
    {
        var result = ReportRequestResponse.Read(Origin, Origin, Origin, [Decision() with { At = Origin }]);
        Assert.Equal(0m, result.ResponseSeconds);
        Assert.Equal("RECORDED_DECISION", result.ResponseEvidence);
    }
}
