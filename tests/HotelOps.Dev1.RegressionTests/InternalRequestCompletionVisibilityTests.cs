using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class InternalRequestCompletionVisibilityTests
{
    [Fact]
    public void FilterDoesNotDiscloseUnreadableOrWrongBranchTargetsOrChangeClosureInput()
    {
        InternalRequestCompletionBlocker[] all = [
            new("MATERIAL", 1, "MATERIAL_COMPLETION_UNPROVEN"),
            new("MATERIAL", 2, "MATERIAL_COMPLETION_UNPROVEN"),
            new("MATERIAL", 3, "LINK_BRANCH_MISMATCH")];
        var visible = InternalRequestCompletionVisibility.Filter(all, [1, 3]).ToArray();
        Assert.Equal([all[0]], visible);
        Assert.Equal(3, all.Length);
        Assert.Empty(InternalRequestCompletionVisibility.Filter(all, []));
    }

    [Fact]
    public void UnclassifiedLegacyRequirementStaysExplicitForAnAuthorizedTarget()
    {
        var legacy = new InternalRequestCompletionBlocker("TASK", 7, "LEGACY_COMPLETION_REQUIREMENT_UNCLASSIFIED");
        Assert.Equal(legacy, Assert.Single(InternalRequestCompletionVisibility.Filter([legacy], [7])));
    }
}
