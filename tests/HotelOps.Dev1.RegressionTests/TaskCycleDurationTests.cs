using HotelOps.Api.Services.Insights;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskCycleDurationTests
{
    private static readonly DateTime Epoch = new(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
    private static DateTime At(int minutes) => Epoch.AddMinutes(minutes);

    [Fact]
    public void Single_cycle_excludes_verification_wait()
    {
        Assert.Equal(600m, TaskCycleDuration.Seconds(At(30), [new(1, 1, At(10), At(30))], [], [new(1, At(0), At(10), true)]));
    }

    [Fact]
    public void Return_and_pause_gaps_remain_elapsed_time_not_effort()
    {
        // First Submit was returned; only the final accepted Submit ends this cycle.
        Assert.Equal(2400m, TaskCycleDuration.Seconds(At(90), [new(2, 3, At(40), At(90))], [],
            [new(1, At(0), At(10), true), new(2, At(20), At(25), false), new(3, At(35), At(40), false)]));
    }

    [Fact]
    public void Multiple_reopens_sum_cycles_and_exclude_closed_gaps()
    {
        Assert.Equal(1800m, TaskCycleDuration.Seconds(At(130),
            [new(1, 1, At(10), At(20)), new(2, 2, At(60), At(70)), new(3, 3, At(120), At(130))],
            [new(1, 1, At(40)), new(2, 2, At(100))],
            [new(1, At(0), At(10), true), new(2, At(50), At(60), true), new(3, At(110), At(120), true)]));
    }

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(20, true)]
    public void Resubmission_intervals_must_not_overlap(int restartMinute, bool valid)
    {
        var seconds = TaskCycleDuration.Seconds(At(90), [new(2, 2, At(40), At(90))], [],
            [new(1, At(0), At(10), true), new(2, At(restartMinute), At(40), false)]);
        if (valid) Assert.Equal(2400m, seconds);
        else Assert.Null(seconds);
    }

    [Fact]
    public void Overlap_in_an_earlier_closed_cycle_invalidates_the_whole_task()
    {
        Assert.Null(TaskCycleDuration.Seconds(At(90),
            [new(1, 2, At(20), At(30)), new(2, 3, At(70), At(90))],
            [new(1, 2, At(40))],
            [new(1, At(0), At(10), true), new(2, At(5), At(20), false), new(3, At(50), At(70), true)]));
    }

    [Theory]
    [InlineData("missing-start")]
    [InlineData("pending")]
    [InlineData("missing-cycle")]
    [InlineData("wrong-cutoff")]
    [InlineData("wrong-completion")]
    [InlineData("future-submit")]
    public void Incomplete_or_inconsistent_timing_is_never_guessed(string defect)
    {
        TaskCycleDuration.Acceptance[] accepted = [new(1, 1, At(10), At(20)), new(2, 2, At(60), At(70))];
        TaskCycleDuration.Reopen[] reopened = [new(1, 1, At(40))];
        TaskCycleDuration.Interval[] intervals = [new(1, At(0), At(10), true), new(2, At(50), At(60), true)];
        var completed = At(70);
        if (defect == "missing-start") intervals[1] = intervals[1] with { HasStartReceipt = false };
        if (defect == "pending") accepted = [accepted[0]];
        if (defect == "missing-cycle") reopened = [];
        if (defect == "wrong-cutoff") reopened[0] = reopened[0] with { IntervalCutoff = 2 };
        if (defect == "wrong-completion") completed = At(71);
        if (defect == "future-submit") accepted[1] = accepted[1] with { SubmittedAt = At(80) };
        Assert.Null(TaskCycleDuration.Seconds(completed, accepted, reopened, intervals));
    }
}
