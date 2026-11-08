using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningCreationTests
{
    private static RoomTypeCreationService Service() => new(null!, null!, null!, null!, TimeProvider.System);
    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public async Task Positive_source_identifiers_required(long room, long type, long priority) => await Assert.ThrowsAsync<ValidationException>(() => Service().CreateCleaningTaskAsync(null!, 0, 1, new(room, type, priority, DateTime.UtcNow.AddHours(1)), default));
    [Fact] public async Task Past_due_never_reaches_persistence() => await Assert.ThrowsAsync<ValidationException>(() => Service().CreateCleaningTaskAsync(null!, 0, 1, new(1, 1, 1, DateTime.UtcNow.AddHours(-1)), default));
    [Theory]
    [InlineData("status")]
    [InlineData("actorId")]
    [InlineData("branchId")]
    public void Creation_contract_rejects_server_owned_fields(string field) => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateCleaningTaskRequest>("{\"RoomId\":1,\"CleaningTypeId\":1,\"PriorityId\":1,\"DueAt\":\"2027-01-01T00:00:00Z\",\"" + field + "\":1}"));
    [Fact] public void Fingerprint_normalizes_reason_and_binds_actor_and_original_tag() { var dto = new AssignCleaningRequest(3, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), " reason "); var a = CleaningAssignmentCommandIdentity.Fingerprint(1, 1, 1, "tag", dto); Assert.Equal(a, CleaningAssignmentCommandIdentity.Fingerprint(1, 1, 1, "tag", dto with { Reason = "reason" })); Assert.NotEqual(a, CleaningAssignmentCommandIdentity.Fingerprint(2, 1, 1, "tag", dto)); Assert.NotEqual(a, CleaningAssignmentCommandIdentity.Fingerprint(1, 1, 1, "newtag", dto)); }
    [Fact] public async Task Assignment_missing_accepted_publisher_cannot_commit() { var result = await Service().AssignCleaningAsync(null!, 0, 1, 1, "\"0000000000000001\"", new(1, DateTime.UtcNow.AddHours(1), "reason"), default); Assert.Equal(RoomTypeBatchStatus.Unavailable, result.Status); }
}
