using HotelOps.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HotelOps.Checklist.CollectionTests;

public sealed class InspectionAssignmentContextTests
{
    [Theory]
    [InlineData("?roomId=")]
    [InlineData("?roomId=0")]
    [InlineData("?roomId=-1")]
    [InlineData("?roomId=1.5")]
    [InlineData("?roomId=1e2")]
    [InlineData("?roomId=01")]
    [InlineData("?roomId=9007199254740992")]
    [InlineData("?roomId=3&roomId=4")]
    public async Task Invalid_room_query_is_rejected_before_service_dispatch(string query)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.QueryString = new QueryString(query);
        // Null service proves invalid context never reaches the database/service.
        var controller = new InspectionAssignmentCandidateController(null!)
        { ControllerContext = new ControllerContext { HttpContext = http } };
        var result = Assert.IsType<ObjectResult>(await controller.Queue(1, CancellationToken.None));
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_ROOM_CONTEXT", Assert.IsType<ProblemDetails>(result.Value).Title);
    }

    [Fact]
    public async Task Valid_direct_open_without_session_is_unauthorized()
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.QueryString = new QueryString("?roomId=3");
        var controller = new InspectionAssignmentCandidateController(null!)
        { ControllerContext = new ControllerContext { HttpContext = http } };
        var result = Assert.IsType<ObjectResult>(await controller.Queue(1, CancellationToken.None, 3));
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public void Production_dispatch_remains_disabled()
        => Assert.True(typeof(InspectionAssignmentCandidateController).IsDefined(typeof(NonControllerAttribute), false));
}