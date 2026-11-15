using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Services.HotelStructure;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class LinkedTaskCreationTests
{
    private static CreateOperationalTaskRequest Input() => new(Guid.NewGuid(), 2, 3, "Task", null, "Outcome",
        [new ProgressDefinition("Checked", true)], 4, null, null, 20, null, null, null);

    [Fact]
    public void ManualCreationSerializationRetainsHistoricalIdempotencyShape()
    {
        var v = RoomTypeCreationService.NormalizeTaskCreation(Input());
        var previous = new { v.RequestKey, v.DepartmentId, v.CategoryId, v.Title, v.Description, v.RequestedResult, v.Criteria,
            v.PriorityId, v.PlannedStartAt, v.DueAt, v.SlaMinutes, v.RoomId, v.AreaId, v.InternalRequestId };
        Assert.Equal(JsonSerializer.Serialize(previous), JsonSerializer.Serialize(v));
    }

    [Fact]
    public void LinkedCreationRequiresCanonicalSourceVersionAndExplicitMandatoryMetadata()
    {
        var input = Input();
        Assert.Throws<ValidationException>(() => RoomTypeCreationService.NormalizeTaskCreation(input with { InternalRequestId = 7 }));
        Assert.Throws<ValidationException>(() => RoomTypeCreationService.NormalizeTaskCreation(input with { RequestLink = new("AAAAAAAAAAA=", true) }));
        foreach (var version in new[] { "", "AAAAAAAAAAA", "AAAAAAAAAAB=", " AAAAAAAAAAA=", "\"AAAAAAAAAAA=\"" })
            Assert.Throws<ValidationException>(() => RoomTypeCreationService.NormalizeTaskCreation(input with { InternalRequestId = 7, RequestLink = new(version, true) }));
        foreach (var required in new[] { true, false })
            Assert.Equal(required, RoomTypeCreationService.NormalizeTaskCreation(input with { InternalRequestId = 7, RequestLink = new("AAAAAAAAAAA=", required) }).RequestLink!.Mandatory);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<TaskRequestLink>("{\"version\":\"AAAAAAAAAAA=\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
