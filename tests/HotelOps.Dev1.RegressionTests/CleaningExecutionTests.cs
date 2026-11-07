using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Housekeeping;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningExecutionTests
{
    [Theory]
    [InlineData("ASSIGNED", 1, 1, false, null)]
    [InlineData("ASSIGNED", 2, 1, false, "CURRENT_ASSIGNEE_REQUIRED")]
    [InlineData("IN_PROGRESS", 1, 1, false, "TASK_NOT_ASSIGNED")]
    [InlineData("PENDING_ASSIGNMENT", 1, 1, false, "TASK_NOT_ASSIGNED")]
    [InlineData("COMPLETED", 1, 1, false, "TASK_NOT_ASSIGNED")]
    [InlineData("ON_HOLD", 1, 1, true, "BLOCKER_RESOLUTION_CONTRACT_REQUIRED")]
    [InlineData("ON_HOLD", 1, 1, false, "BLOCKER_RESOLUTION_CONTRACT_REQUIRED")]
    [InlineData("ASSIGNED", 1, 1, true, "NO_OPEN_BLOCKER_TO_RESOLVE")]
    public void Start_requires_current_owner_and_confirmed_transition(string state, long actor, long owner, bool resolve, string? expected)
        => Assert.Equal(expected, CleaningLifecycleRules.StartConflict(state, actor, owner, resolve));
    [Theory]
    [InlineData("ASSIGNED", 1, 2, null)]
    [InlineData("IN_PROGRESS", 1, 2, null)]
    [InlineData("ON_HOLD", 1, 2, null)]
    [InlineData("ASSIGNED", 1, 1, "SAME_ASSIGNEE")]
    [InlineData("PENDING_INSPECTION", 1, 2, "REASSIGN_STATE_INVALID")]
    [InlineData("COMPLETED", 1, 2, "REASSIGN_STATE_INVALID")]
    [InlineData("CANCELLED", 1, 2, "REASSIGN_STATE_INVALID")]
    [InlineData("PENDING_ASSIGNMENT", 1, 2, "REASSIGN_STATE_INVALID")]
    public void Reassign_keeps_SRS_states_and_distinct_new_owner(string state, long previous, long next, string? expected)
        => Assert.Equal(expected, CleaningLifecycleRules.ReassignConflict(state, previous, next));
    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("123456789")]
    [InlineData("0123456789\ninvalid")]
    public void Reassignment_override_reason_requires_ten_characters(string reason)
        => Assert.Throws<ValidationException>(() => CleaningLifecycleRules.Reason(reason));
    [Fact]
    public void Reassignment_identity_binds_action_scope_actor_original_tag_recipient_and_body()
    {
        var input = new ReassignCleaningRequest(5, new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), "  staffing change  ");
        var digest = CleaningLifecycleRules.Fingerprint(1, 2, 3, "oldtag", input);
        Assert.Equal(digest, CleaningLifecycleRules.Fingerprint(1, 2, 3, "oldtag", input with { Reason = "staffing change" }));
        Assert.NotEqual(digest, CleaningLifecycleRules.Fingerprint(2, 2, 3, "oldtag", input));
        Assert.NotEqual(digest, CleaningLifecycleRules.Fingerprint(1, 3, 3, "oldtag", input));
        Assert.NotEqual(digest, CleaningLifecycleRules.Fingerprint(1, 2, 4, "oldtag", input));
        Assert.NotEqual(digest, CleaningLifecycleRules.Fingerprint(1, 2, 3, "newtag", input));
        Assert.NotEqual(digest, CleaningLifecycleRules.Fingerprint(1, 2, 3, "oldtag", input with { AssigneeId = 6 }));
        Assert.NotEqual(digest, CleaningLifecycleRules.Fingerprint(1, 2, 3, "oldtag", input with { DueAt = input.DueAt.AddMinutes(1) }));
    }
    [Theory]
    [InlineData("actorId")]
    [InlineData("actualStartAt")]
    [InlineData("intervalId")]
    public void Start_DTO_rejects_client_owned_identity_or_time(string field)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StartCleaningRequest>("{\"" + field + "\":1}"));
    [Theory]
    [InlineData("oldAssigneeId")]
    [InlineData("status")]
    [InlineData("assignedAt")]
    public void Reassign_DTO_rejects_server_owned_fields(string field)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReassignCleaningRequest>("{\"AssigneeId\":2,\"DueAt\":\"2027-01-01T00:00:00Z\",\"Reason\":\"staffing change\",\"" + field + "\":1}"));
    [Theory]
    [InlineData("")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-key")]
    public void Reassignment_operation_must_be_nonempty_GUID(string key)
        => Assert.Throws<ValidationException>(() => CleaningLifecycleRules.Operation(key));
}
