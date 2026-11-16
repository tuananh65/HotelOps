using System.Text.Json;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningCancellationNotificationTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("recipient", false)]
    [InlineData("actor", false)]
    [InlineData("assignment", false)]
    [InlineData("publication", false)]
    [InlineData("operation", false)]
    [InlineData("revision", false)]
    [InlineData("time", false)]
    [InlineData("state", false)]
    [InlineData("unassigned", false)]
    [InlineData("json", false)]
    public void CancellationSourceRequiresExactRecipientAndImmutableReceipt(string change, bool expected)
    {
        var operation=Guid.NewGuid(); var publication=Guid.NewGuid(); var at=DateTime.UtcNow;
        var receipt=new CleaningCancellationResult(1,2,3,4,5,operation,publication,6,at,"\"0000000000000001\"");
        var proof=new CleaningCancellationNotificationSource.Proof { Actor=7,Assignment=4,Operation=operation,
            Publication=publication,Revision=Convert.FromHexString("0000000000000001"),At=at };
        if(change=="actor") proof.Actor=0;
        if(change=="assignment") proof.Assignment=9;
        if(change=="publication") proof.Publication=Guid.NewGuid();
        if(change=="operation") proof.Operation=Guid.NewGuid();
        if(change=="revision") proof.Revision=new byte[8];
        if(change=="time") proof.At=at.AddTicks(1);
        if(change=="state") receipt=receipt with { State="ASSIGNED" };
        if(change=="unassigned") receipt=receipt with { AssignmentId=null,AssigneeId=null,PublicationId=null };
        proof.Result=change=="json" ? "invalid" : JsonSerializer.Serialize(receipt,ProvisionalCleaningNotification.Json);
        Assert.Equal(expected,CleaningCancellationNotificationSource.Read(proof,change=="recipient" ? 9 : 5)!=null);
    }
}
