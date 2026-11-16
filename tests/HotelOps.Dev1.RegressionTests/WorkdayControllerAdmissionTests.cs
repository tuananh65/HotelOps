using System.Reflection;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.WorkTime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;
public sealed class WorkdayControllerAdmissionTests
{
    [Fact]
    public void MvcDiscoversWritesAndSeparateReadController()
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(new AssemblyPart(typeof(WorkdaysController).Assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());
        var feature = new ControllerFeature(); manager.PopulateFeature(feature);
        Assert.Contains(typeof(WorkdaysController).GetTypeInfo(), feature.Controllers);
        Assert.Contains(typeof(WorkdayReadsController).GetTypeInfo(), feature.Controllers);
        Assert.DoesNotContain(typeof(WorkdaysController).GetMethods(), m => m.GetCustomAttribute<HttpGetAttribute>() != null);
        Assert.Equal(2, typeof(WorkdaysController).GetMethods().Count(m => m.GetCustomAttribute<HttpPostAttribute>() != null));
    }
    [Fact]
    public async Task PlainHttpStartCannotReachService()
    {
        var controller = Controller();
        var response = Assert.IsType<ObjectResult>(await controller.Start(1, new StartWorkdayRequest { ShiftAssignmentId=2 }, default));
        Assert.Equal(400,response.StatusCode);
        Assert.Equal("HTTPS_REQUIRED",Assert.IsType<ProblemDetails>(response.Value).Title);
    }
    [Theory]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("W/\"AAAAAAAAAAE=\"")]
    [InlineData("\"not-base64!!\"")]
    public async Task InvalidEndVersionCannotReachService(string etag)
    {
        var controller = Controller();controller.Request.Scheme="https";
        controller.Request.Headers.IfMatch=etag;
        var response=Assert.IsType<ObjectResult>(await controller.End(1,2,new EndWorkdayRequest{PauseAll=false},default));
        Assert.Equal(400,response.StatusCode);
        Assert.Equal("INVALID_IF_MATCH",Assert.IsType<ProblemDetails>(response.Value).Title);
    }
    private static WorkdaysController Controller() => new(null!)
    {ControllerContext=new ControllerContext{HttpContext=new DefaultHttpContext()}};
}
