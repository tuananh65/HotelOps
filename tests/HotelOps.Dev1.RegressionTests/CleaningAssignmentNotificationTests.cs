using System.Text.Json;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningAssignmentNotificationTests
{
    [Fact]
    public void Persisted_assignment_occurrence_preserves_full_operation_publication_and_result()
    {
        var s = Snapshot();
        var content = CleaningAssignmentNotificationRules.Resolve(s, staging: true);
        Assert.Equal(s.AssignmentId, content.OccurrenceId);
        Assert.Equal(s.OperationId, content.Facts.OperationId);
        Assert.Equal(s.PublicationId, content.Facts.PublicationId);
        Assert.Equal("/cleaning/tasks/23?branchId=31&assignmentId=47", content.ActionPath);
        Assert.Equal(CleaningAssignmentNotificationRules.ReadPolicy, content.ReadPolicy);
        Assert.Equal(content, CleaningAssignmentNotificationRules.Resolve(s, staging: true));
        CleaningAssignmentNotificationRules.RequireCommand(s, s.ActorId, s.OperationId, s.Fingerprint.ToArray());
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("reassign")]
    [InlineData("ended")]
    [InlineData("cycle")]
    [InlineData("room")]
    [InlineData("recipient")]
    [InlineData("branch")]
    [InlineData("actor")]
    [InlineData("audit")]
    [InlineData("intent")]
    [InlineData("incomplete")]
    [InlineData("version")]
    [InlineData("fingerprint-version")]
    [InlineData("publication")]
    [InlineData("operation")]
    [InlineData("stored-revision")]
    [InlineData("payload-cycle")]
    [InlineData("result")]
    public void Invalidated_or_inconsistent_source_is_rejected_for_staging_and_read(string change)
    {
        var s = Snapshot();
        switch (change)
        {
            case "cancel": s.WorkflowState = "CANCELLED"; break;
            case "reassign": s.AssignmentStatus = "REASSIGNED"; break;
            case "ended": s.EndedAt = s.AssignedAt.AddMinutes(1); break;
            case "cycle": s.CurrentCycleId++; break;
            case "room": s.RoomActive = false; break;
            case "recipient": s.RecipientId++; break;
            case "branch": s.BranchId++; break;
            case "actor": s.ActorId++; break;
            case "audit": s.HasAudit = false; break;
            case "intent": s.HasIntent = false; break;
            case "incomplete": s.Completed = false; break;
            case "version": s.SchemaVersion++; break;
            case "fingerprint-version": s.FingerprintVersion++; break;
            case "publication": s.PublicationId = Guid.NewGuid(); break;
            case "operation": s.OperationId = Guid.NewGuid(); break;
            case "stored-revision": s.SourceRevision = new byte[8]; break;
            case "payload-cycle": s.PayloadJson = s.PayloadJson.Replace("\"taskCycleId\":23", "\"taskCycleId\":24"); break;
            case "result": s.ResultJson = s.ResultJson.Replace("PENDING_DELIVERY", "DELIVERED"); break;
        }
        Assert.Throws<InvalidOperationException>(() => CleaningAssignmentNotificationRules.Resolve(s, true));
        Assert.Throws<InvalidOperationException>(() => CleaningAssignmentNotificationRules.Resolve(s, false));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("due")]
    [InlineData("workflow")]
    public void New_staging_requires_exact_revision_but_read_does_not_require_execution_state(string change)
    {
        var s = Snapshot();
        if (change == "revision") s.CurrentRevision = new byte[8];
        if (change == "due") s.DueAt = s.DueAt!.Value.AddMinutes(1);
        if (change == "workflow") s.WorkflowState = "IN_PROGRESS";
        Assert.Throws<InvalidOperationException>(() => CleaningAssignmentNotificationRules.Resolve(s, true));
        Assert.Equal(s.AssignmentId, CleaningAssignmentNotificationRules.Resolve(s, false).OccurrenceId);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("operation")]
    [InlineData("fingerprint")]
    public void Command_replay_identity_cannot_be_retargeted(string change)
    {
        var s = Snapshot();
        Assert.Throws<InvalidOperationException>(() => CleaningAssignmentNotificationRules.RequireCommand(s,
            change == "actor" ? s.ActorId + 1 : s.ActorId,
            change == "operation" ? Guid.NewGuid() : s.OperationId,
            change == "fingerprint" ? new byte[32] : s.Fingerprint));
    }

    internal static CleaningAssignmentNotificationSnapshot Snapshot()
    {
        var at = new DateTime(2026, 11, 8, 1, 0, 0, DateTimeKind.Utc);
        var operation = Guid.Parse("00000000-0000-0000-0000-000000000123");
        var publication = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var tag = "\"000000000000001A\"";
        var facts = new CandidateAssignmentEvent(operation, publication, 7, 31, 23, 23, 37, 47, 53, at, tag, at.AddHours(2));
        var result = new CandidateAssignmentResult(23, 47, 31, 53, operation, publication, "ASSIGNED", "PENDING_DELIVERY", tag);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return new() { AssignmentId = 47, TaskId = 23, RoomId = 37, BranchId = 31, ActorId = 7, RecipientId = 53,
            AssignedAt = at, AssignmentStatus = "ACTIVE", WorkflowState = "ASSIGNED", DueAt = at.AddHours(2),
            CurrentCycleId = 23, CurrentRevision = Convert.FromHexString("000000000000001A"),
            OperationId = operation, PublicationId = publication, Fingerprint = Enumerable.Repeat((byte)3, 32).ToArray(),
            FingerprintVersion = 1, Completed = true, ResultJson = JsonSerializer.Serialize(result, json),
            PayloadJson = JsonSerializer.Serialize(facts, json), SourceRevision = Convert.FromHexString("000000000000001A"),
            EventType = CleaningAssignmentNotificationRules.EventType, SchemaVersion = 1, HasAudit = true, HasIntent = true, RoomActive = true };
    }
}
