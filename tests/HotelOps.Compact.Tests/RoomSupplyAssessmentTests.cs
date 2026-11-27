using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using Xunit;
namespace HotelOps.Compact.Tests;
public sealed class RoomSupplyAssessmentTests
{
    [Fact]
    public void RoomAssessmentHasNoWriteRoute()
    {
        var routes=typeof(HotelOps.Api.Compact.Controllers.SuppliesController).GetMethods()
            .SelectMany(method=>method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute),true)
                .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>())
            .Where(route=>route.Template?.StartsWith("room-assessments/")==true).ToArray();
        Assert.NotEmpty(routes);
        Assert.All(routes,route=>Assert.Equal(new[]{"GET"},route.HttpMethods));
    }
    [Fact]
    public void MissingUsableQuantityRemainsNullRatherThanBeingDefaultedToZero()
    {
        var line=JsonSerializer.Deserialize<RoomSupplyAssessmentLine>("{\"itemId\":1}",new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(line);Assert.Equal(1,line.ItemId);Assert.Null(line.UsableQuantity);
    }
    [Fact]
    public void ExplicitZeroIsRetainedAsActualObservation()
    {
        var line=JsonSerializer.Deserialize<RoomSupplyAssessmentLine>("{\"itemId\":1,\"usableQuantity\":0}",new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(line);Assert.Equal(0,line.UsableQuantity);
    }
}
