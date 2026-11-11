using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;

internal static class InspectionContractBlockerChecks
{
    internal static async Task RunAsync()
    {
        var count = 0;
        void Check(string name, bool ok) { if (!ok) throw new Exception(name); count++; Console.WriteLine("PASS " + name); }
        var key = Guid.Parse("20261111-0000-4000-8000-000000000019");
        const string tag = "\"0000000000000001\"";
        var input = new InspectionAssignmentCandidateRequest(5,6,new DateTime(2030,1,1,0,0,0,DateTimeKind.Utc),"assign",tag,4,true,key);
        Check("G01 candidate transports UUID before Inspection CREATE", input.OperationId == key);
        Check("G01 old preflight DTO remains compatible, without identity authority", (input with {OperationId=null}).OperationId is null);
        foreach (var resource in new long[] { 0, -1 })
        {
            var denied = false;
            try { Fe05OperationIdentity.Fingerprint(new(key,1,resource,new string('A',64),[Fe05NotificationKind.InspectionFailed])); }
            catch (Fe05NotificationUnavailableException) { denied = true; }
            Check("G01 N01 refuses missing/fake Inspection resource " + resource, denied);
        }
        // Unknown kinds are not an Assigned/Passed registration. No enum or graph mutation.
        foreach (var kind in new[] { (Fe05NotificationKind)0, (Fe05NotificationKind)3, (Fe05NotificationKind)4 })
        {
            var denied = false;
            try { Fe05OperationIdentity.Fingerprint(new(key,1,2,new string('A',64),[kind])); }
            catch (Fe05NotificationUnavailableException) { denied = true; }
            Check("N01 refuses unregistered event kind " + (int)kind, denied);
        }
        var op = InspectionIssuerOperation.Approve(key,1,2,3,tag);
        IInspectionApproveAcceptedBinding binding = new ClosedInspectionApproveBinding();
        var calls = new Func<Task>[] {
            () => binding.RequireAcceptedAsync(null!,op,default),
            async () => { await binding.ReserveOrReadCompletedAsync(null!,op,default); },
            () => binding.StageRequiredAsync(null!,op,tag,default),
            async () => { await binding.CompleteWithTrustedSqlReceiptAsync(null!,op,tag,default); },
            () => binding.VerifyCompletedAsync(null!,op,"{}",default)
        };
        for (var i=0;i<calls.Length;i++)
        {
            var denied = false;
            try { await calls[i](); }
            catch (Fe05NotificationUnavailableException e) { denied = e.Message=="UC044_PASSED_STAGING_AND_DURABLE_BINDING_UNACCEPTED"; }
            Check("G05 closed binding denies step " + i + " without SQL/receipt fallback", denied);
        }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var cancellationObserved=false;
        try { await binding.RequireAcceptedAsync(null!,op,cancelled.Token); }
        catch(OperationCanceledException) { cancellationObserved=true; }
        Check("G05 closed binding observes cancellation", cancellationObserved);
        foreach(var role in Enum.GetValues<InspectionRecipientRelationship>())
        {
            var policy=InspectionRecipientPermissionContract.For(role);
            var denied=false;
            try { policy.RequireBusinessAcceptance(); }
            catch(Fe05NotificationUnavailableException) { denied=true; }
            Check("G02/G03 existing read acceptance stays closed " + role,denied && policy.AcceptedConcreteBinding is null && !policy.PrivateEvidenceAllowed);
        }
        Check("UC041 candidate controller remains undispatched",typeof(HotelOps.Api.Controllers.InspectionAssignmentCandidateController)
            .IsDefined(typeof(Microsoft.AspNetCore.Mvc.NonControllerAttribute),false));
        Console.WriteLine($"{count}/{count} PASS FAST019 contract/refusal checks; authoritative replay/concurrency/SQL NOT_RUN");
    }
}
