using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningClosureNotificationSourceTests
{
    private static (CleaningClosureNotificationSource.Proof Proof, CleaningClosureResult Receipt) Fixture(bool complete)
    {
        var receipt = new CleaningClosureResult(10, 20, 30, Guid.NewGuid(), Guid.NewGuid(), 40, 50,
            complete ? "PENDING_INSPECTION" : "ON_HOLD", new DateTime(2026, 11, 16, 1, 0, 0, DateTimeKind.Utc),
            complete ? "PENDING_INSPECTION" : "PENDING_CLEANING", true, "\"0000000000000001\"");
        return (new() { PublicationId = receipt.PublicationId, OperationId = receipt.OperationId,
            ActorId = 60, AssignmentId = 30, EventName = complete ? "CLEANING_TASK_COMPLETED" : "CLEANING_BLOCKER_REPORTED",
            Revision = Convert.FromHexString("0000000000000001"), At = receipt.OccurredAtUtc,
            ResultJson = JsonSerializer.Serialize(receipt, ProvisionalCleaningNotification.Json) }, receipt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AcceptsExactHistoricalReceiptAndRejectsAnotherTaskActorOrEvent(bool complete)
    {
        var (proof, receipt) = Fixture(complete);
        Assert.Equal(receipt, CleaningClosureNotificationSource.Read(proof, proof.EventName, 10, 60));
        Assert.Null(CleaningClosureNotificationSource.Read(proof, proof.EventName, 11, 60));
        Assert.Null(CleaningClosureNotificationSource.Read(proof, proof.EventName, 10, 61));
        Assert.Null(CleaningClosureNotificationSource.Read(proof, "CLEANING_TASK_ASSIGNED", 10, 60));
        Assert.Null(CleaningClosureNotificationSource.Read(proof,
            complete ? "CLEANING_BLOCKER_REPORTED" : "CLEANING_TASK_COMPLETED", 10, 60));
    }

    [Theory]
    [InlineData("publication")]
    [InlineData("operation")]
    [InlineData("assignment")]
    [InlineData("revision")]
    [InlineData("timestamp")]
    [InlineData("state")]
    [InlineData("branch")]
    [InlineData("malformed")]
    public void RejectsUnboundOrMalformedProof(string changed)
    {
        var (proof, receipt) = Fixture(true);
        switch (changed)
        {
            case "publication": proof.PublicationId = Guid.NewGuid(); break;
            case "operation": proof.OperationId = Guid.NewGuid(); break;
            case "assignment": proof.AssignmentId++; break;
            case "revision": proof.Revision[7]++; break;
            case "timestamp": proof.At = proof.At.AddSeconds(1); break;
            case "state": proof.ResultJson = JsonSerializer.Serialize(receipt with { State = "ON_HOLD" }, ProvisionalCleaningNotification.Json); break;
            case "branch": proof.ResultJson = JsonSerializer.Serialize(receipt with { BranchId = 0 }, ProvisionalCleaningNotification.Json); break;
            case "malformed": proof.ResultJson = "{broken"; break;
        }
        Assert.Null(CleaningClosureNotificationSource.Read(proof, proof.EventName, 10, 60));
    }
}
