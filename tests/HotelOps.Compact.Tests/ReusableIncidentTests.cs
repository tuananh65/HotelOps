using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class ReusableIncidentTests
{
    [Theory]
    [InlineData(2,1,0)]
    [InlineData(2,3,0)]
    [InlineData(2,2,2)]
    [InlineData(0,0,0)]
    public void ReplacementRejectsPartialExcessAndDuplicateIssues(decimal reported,decimal actual,decimal issued)
        =>Assert.Equal("REUSABLE_FULL_REPLACEMENT_REQUIRED",Assert.Throws<ApiException>(()=>ReusableIncidentRules.RequireFullIssue(reported,actual,issued)).Code);

    [Fact]
    public void FullReplacementIsAllowedOnce()=>ReusableIncidentRules.RequireFullIssue(2,2,0);

    [Fact]
    public void MissingNeverCreatesPhysicalReturn()
    {
        ReusableIncidentRules.RequireReturn("MISSING",2,0,0);
        Assert.Throws<ApiException>(()=>ReusableIncidentRules.RequireReturn("MISSING",2,0,1));
    }

    [Fact]
    public void DamagedReturnsUseActualCumulativeReceipts()
    {
        ReusableIncidentRules.RequireReturn("BROKEN",3,0,1);
        ReusableIncidentRules.RequireReturn("BROKEN",3,1,2);
        Assert.Throws<ApiException>(()=>ReusableIncidentRules.RequireReturn("BROKEN",3,1,3));
        Assert.Throws<ApiException>(()=>ReusableIncidentRules.RequireReturn("BROKEN",3,0,-1));
        Assert.Throws<ApiException>(()=>ReusableIncidentRules.RequireReturn("BROKEN",3,0,0.00001m));
    }
}
