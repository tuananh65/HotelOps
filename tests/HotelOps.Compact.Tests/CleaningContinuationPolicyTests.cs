using HotelOps.Api.Compact.Security;
using Xunit;
namespace HotelOps.Compact.Tests;
public class CleaningContinuationPolicyTests
{
    [Theory]
    [InlineData("POST","/api/v2/work/12/actions/complete",true)]
    [InlineData("POST","/api/v2/work/12/actions/pause",true)]
    [InlineData("POST","/api/v2/work/12/actions/resume",true)]
    [InlineData("POST","/api/v2/evidence",true)]
    [InlineData("POST","/api/v2/evidence/12/invalidate",true)]
    [InlineData("POST","/api/v2/work/12/actions/start",false)]
    [InlineData("POST","/api/v2/work/12/actions/accept",false)]
    [InlineData("POST","/api/v2/work/12/actions/progress",false)]
    [InlineData("POST","/api/v2/work",false)]
    [InlineData("POST","/api/v2/work/room-issues",false)]
    [InlineData("POST","/api/v2/work/room-issues/batch",false)]
    [InlineData("POST","/api/v2/supplies/room-assessments",false)]
    [InlineData("POST","/api/v2/rooms/12/actions/complete",false)]
    [InlineData("POST","/api/v2/work/12/actions/complete/extra",false)]
    [InlineData("GET","/api/v2/work/12/actions/complete",false)]
    [InlineData("POST","/api/v2/work/no-id/actions/complete",false)]
    public void ContinuationIsAnExplicitRouteAllowlist(string method,string path,bool expected)=>
        Assert.Equal(expected,CleaningContinuationAccess.IsContinuationRoute(method,path));
}
