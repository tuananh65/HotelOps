using HotelOps.Api.Services.Incidents;
using Microsoft.Data.SqlClient;
using Xunit;

namespace HotelOps.RoomBlock.ContractTests;

public sealed class RoomBlockTemporalRulesV1Tests
{
    private static readonly DateTime T = new(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc);
    private static RoomBlockIntervalV1 I(int blockedMinutes, int? releasedMinutes = null)
        => new(T.AddMinutes(blockedMinutes),
            releasedMinutes.HasValue ? T.AddMinutes(releasedMinutes.Value) : null);

    [Fact] public void NoRows_Clear() =>
        Assert.Equal(EffectiveRoomBlockState.Clear, RoomBlockTemporalRulesV1.Evaluate([], T).State);

    [Fact] public void ActiveUnreleased_IsEffective() =>
        Assert.False(RoomBlockTemporalRulesV1.Evaluate([I(-5)], T).AllowsFreshStart);

    [Fact] public void PastRelease_IsClear() =>
        Assert.Equal(EffectiveRoomBlockState.Clear, RoomBlockTemporalRulesV1.Evaluate([I(-20, -1)], T).State);

    [Fact] public void ScheduledBlock_NotEffective_ButCapturesActivation() {
        var decision = RoomBlockTemporalRulesV1.Evaluate([I(5)], T);
        Assert.True(decision.AllowsFreshStart);
        Assert.Equal(T.AddMinutes(5), decision.NextScheduledAtUtc);
    }

    [Fact] public void FutureActualRelease_IsUnknown() =>
        Assert.Equal(EffectiveRoomBlockState.Unknown, RoomBlockTemporalRulesV1.Evaluate([I(-2, 1)], T).State);

    [Fact] public void ReleaseBeforeBlocked_IsUnknown() =>
        Assert.Equal(EffectiveRoomBlockState.Unknown, RoomBlockTemporalRulesV1.Evaluate([I(-2, -3)], T).State);

    [Fact] public void SameInstantRelease_IsValidZeroDuration() =>
        Assert.True(RoomBlockTemporalRulesV1.Evaluate([I(-3, -3)], T).AllowsFreshStart);

    [Fact] public void InvalidHistoryOverridesActiveToUnknown() {
        var decision = RoomBlockTemporalRulesV1.Evaluate([I(-5), I(-1, 1)], T);
        Assert.Equal(EffectiveRoomBlockState.Unknown, decision.State);
        Assert.False(decision.AllowsFreshStart);
    }

    [Fact] public void BlockAtDecisionInstant_IsEffective() =>
        Assert.Equal(EffectiveRoomBlockState.Effective, RoomBlockTemporalRulesV1.Evaluate([I(0)], T).State);

    [Fact] public void ReleaseAtDecisionInstant_IsClear() =>
        Assert.Equal(EffectiveRoomBlockState.Clear, RoomBlockTemporalRulesV1.Evaluate([I(-5, 0)], T).State);

    [Fact] public void UnspecifiedTimezone_IsUnknown() =>
        Assert.Equal(EffectiveRoomBlockState.Unknown, RoomBlockTemporalRulesV1.Evaluate(
            [new(DateTime.SpecifyKind(T, DateTimeKind.Unspecified), null)], T).State);

    [Theory]
    [InlineData(EffectiveRoomBlockState.Clear, true)]
    [InlineData(EffectiveRoomBlockState.Effective, false)]
    [InlineData(EffectiveRoomBlockState.Unknown, false)]
    [InlineData((EffectiveRoomBlockState)99, false)]
    public void OnlyClearAllowsProgression(EffectiveRoomBlockState state, bool allowed) =>
        Assert.Equal(allowed, new RoomBlockStartDecisionV1(state, T, null, "TEST").AllowsFreshStart);

    [Fact] public void ScheduledActivation_ReevaluationAtBoundaryDenies()
    {
        RoomBlockIntervalV1[] rows = [I(1)];
        Assert.True(RoomBlockTemporalRulesV1.Evaluate(rows, T).AllowsFreshStart);
        var final = RoomBlockTemporalRulesV1.Evaluate(rows, T.AddMinutes(1));
        Assert.Equal(EffectiveRoomBlockState.Effective, final.State);
        Assert.False(final.AllowsFreshStart);
        // Pure rule coverage only: no assertion about elapsed time between SQL recheck and commit.
    }

    [Fact] public void EarliestScheduledBlockIsReportedAcrossFullHistory()
    {
        var decision = RoomBlockTemporalRulesV1.Evaluate([I(-20, -10), I(10), I(1)], T);
        Assert.True(decision.AllowsFreshStart);
        Assert.Equal(T.AddMinutes(1), decision.NextScheduledAtUtc);
    }

    [Fact] public void InvalidHistoryOverridesScheduledClearRegardlessOfRowOrder()
    {
        foreach (var rows in new[] { new[] { I(5), I(-2, -3) }, new[] { I(-2, -3), I(5) } })
        {
            var decision = RoomBlockTemporalRulesV1.Evaluate(rows, T);
            Assert.Equal(EffectiveRoomBlockState.Unknown, decision.State);
            Assert.False(decision.AllowsFreshStart);
        }
    }

    [Fact] public void MissingSourceAndUnverifiedDecisionTimeFailClosed()
    {
        Assert.False(RoomBlockTemporalRulesV1.Evaluate(null, T).AllowsFreshStart);
        Assert.False(RoomBlockTemporalRulesV1.Evaluate([], DateTime.MinValue).AllowsFreshStart);
        Assert.False(RoomBlockTemporalRulesV1.Evaluate([], DateTime.SpecifyKind(T, DateTimeKind.Unspecified)).AllowsFreshStart);
        Assert.False(RoomBlockTemporalRulesV1.Evaluate([null!], T).AllowsFreshStart);
    }

    // These exercise guards before any SQL connection is opened. They are not
    // transaction identity, schema, lock or SQL concurrency acceptance tests.
    [Fact] public async Task RevalidationWithoutSuccessfulInitialCheckIsDenied()
    {
        await using var connection = new SqlConnection();
        var error = await Assert.ThrowsAsync<RoomBlockStartDeniedException>(() =>
            new SqlRoomBlockStartGateV1().RevalidateBeforeCommitAsync(connection, null!, 1, 1, CancellationToken.None));
        Assert.Equal("ROOM_BLOCK_CHECK_SEQUENCE_INVALID", error.Code);
    }

    [Fact] public async Task CancellationPoisonsGateForSubsequentUse()
    {
        await using var connection = new SqlConnection();
        var gate = new SqlRoomBlockStartGateV1();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            gate.RequireClearForFreshStartAsync(connection, null!, 1, 1, cancellation.Token));
        var error = await Assert.ThrowsAsync<RoomBlockStartDeniedException>(() =>
            gate.RequireClearForFreshStartAsync(connection, null!, 1, 1, CancellationToken.None));
        Assert.Equal("ROOM_BLOCK_CHECK_SEQUENCE_INVALID", error.Code);
    }
}
