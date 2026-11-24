using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;
public class CleaningDeadlineTests
{
    private static readonly DateTime Now=new(2026,11,23,9,0,0,DateTimeKind.Utc);
    [Theory]
    [InlineData("ASSIGNED",true)] [InlineData("PENDING_ACCEPTANCE",true)]
    [InlineData("IN_PROGRESS",true)] [InlineData("ON_HOLD",true)]
    [InlineData("PENDING_INSPECTION",false)] [InlineData("PENDING_REINSPECTION",false)]
    [InlineData("CANCELLED",false)] [InlineData("COMPLETED",false)]
    public void OnlyUnsubmittedExecutionIsOverdue(string status,bool expected) =>
        Assert.Equal(expected,CleaningDeadline.IsOverdue("CLEANING",status,Now.AddSeconds(-1),null,Now));
    [Fact] public void CompletionBoundaryAndOtherKindsAreExcluded()
    {
        Assert.False(CleaningDeadline.IsOverdue("CLEANING","IN_PROGRESS",Now,null,Now));
        Assert.False(CleaningDeadline.IsOverdue("CLEANING","IN_PROGRESS",null,null,Now));
        Assert.False(CleaningDeadline.IsOverdue("CLEANING","IN_PROGRESS",Now.AddMinutes(-1),Now,Now));
        Assert.False(CleaningDeadline.IsOverdue("MAINTENANCE","IN_PROGRESS",Now.AddMinutes(-1),null,Now));
    }
    [Fact] public void LogIdentityIsPerAssignmentAndDeadline()
    {
        Assert.Equal(CleaningDeadline.Key(1,Now),CleaningDeadline.Key(1,Now));
        Assert.NotEqual(CleaningDeadline.Key(1,Now),CleaningDeadline.Key(2,Now));
        Assert.NotEqual(CleaningDeadline.Key(1,Now),CleaningDeadline.Key(1,Now.AddMinutes(1)));
    }
}
