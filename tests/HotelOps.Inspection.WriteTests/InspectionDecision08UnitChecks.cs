using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
internal static class InspectionDecision08UnitChecks
{
    internal static async Task RunAsync()
    {
        var count=0;void Check(string title,bool value){if(!value)throw new Exception(title);count++;Console.WriteLine("PASS "+title);}
        var roster=new InspectionRecipientRoster(1,2,3,4,5,6,7,8,9,"0000000000000001","0000000000000002","0000000000000003",Fe05NotificationKind.InspectionFailed);
        Check("Roster includes all independent users in ascending identity",roster.Users.SequenceEqual(new long[]{1,6,7,8,9}));
        Check("Dual role ID deduplicates after both identities supplied",(roster with{OriginalCleanerId=8}).Users.SequenceEqual(new long[]{1,6,7,8}));
        Check("Supervisor change invalidates retained participant tuple",!roster.SameParticipants(roster with{SupervisorId=10}));
        Check("Original cleaner change invalidates retained participant tuple",!roster.SameParticipants(roster with{OriginalCleanerId=10}));
        Check("Binding revision change invalidates retained participant tuple",!roster.SameParticipants(roster with{SupervisorRevision="0000000000000004"}));
        Check("Cycle revision change invalidates retained participant tuple",!roster.SameParticipants(roster with{CycleRevision="0000000000000004"}));
        Check("Only own root revision may be excluded from participant equality",roster.SameParticipants(roster with{InspectionRevision="0000000000000004"}));
        Check("Branch mismatch invalidates retained tuple",!roster.SameParticipants(roster with{BranchId=20}));
        var key=Guid.NewGuid();var fingerprint=new string('A',64);var request=new Fe05OperationRequest(key,2,3,fingerprint,[Fe05NotificationKind.InspectionFailed]);
        Dictionary<string,object> receipt=new(){["contract"]="FE05_INSPECTION_CONCLUSION_V1",["issuer"]="FE05.UC044.UC045",["issuerVersion"]=1,
            ["actorUserId"]=1L,["operationId"]=key,["requestFingerprint"]=fingerprint,["branchId"]=2L,["inspectionId"]=3L,
            ["cleaningTaskId"]=4L,["roomId"]=5L,["writerTransactionId"]=10L,["conclusion"]="FAILED"};
        void Verify(Dictionary<string,object> value){using var doc=JsonDocument.Parse(JsonSerializer.Serialize(value));InspectionOwnerReceipt.Require(doc.RootElement,request,1,roster);}
        Verify(receipt);Check("Typed receipt shape and identity validator accepts consistent fixture (not SQL authority)",true);
        foreach(var change in new Dictionary<string,object>{{"actorUserId",99L},{"branchId",99L},{"inspectionId",99L},{"cleaningTaskId",99L},{"roomId",99L},
            {"operationId",Guid.NewGuid()},{"requestFingerprint",new string('B',64)},{"issuer","TEST_ONLY"},{"issuerVersion",2},{"writerTransactionId",0L},{"conclusion","PASSED"}})
        {
            var invalid=new Dictionary<string,object>(receipt){[change.Key]=change.Value};
            try{Verify(invalid);throw new Exception("Forged fixture accepted: "+change.Key);}catch(NotificationConflictException){Check("Typed receipt rejects "+change.Key,true);}
        }
        try{await new MissingInspectionOriginalCleanerSource().ReadAsync(3,4,5,2,"0000000000000002",default);throw new Exception("Missing original source accepted");}
        catch(Fe05NotificationUnavailableException){Check("No original-cleaner heuristic/default authority",true);}
        var services=new ServiceCollection();services.AddInspectionConclusionComposition(new ConfigurationBuilder().Build());
        Check("Production default source coordination remains unaccepted",services.Last(x=>x.ServiceType==typeof(IFe05SourceCoordination)).ImplementationType==typeof(UnacceptedFe05Coordination));
        services.AddInspectionDecision08OwnerCandidate();
        Check("Concrete owner coordination is explicit opt-in",services.Last(x=>x.ServiceType==typeof(IFe05SourceCoordination)).ImplementationType==typeof(InspectionOwnerSourceCoordination));
        Check("Public Reject dependency gate remains closed",RoomTypeCreationService.RejectWriteDependencies.Length>0);
        Check("Public Rework dependency gate remains closed",RoomTypeCreationService.ReworkWriteDependencies.Length>0);
        services.AddLogging();services.AddSingleton<HotelOps.Api.Security.Authorization.ICurrentUserAccessor,NoActor>();
        services.AddScoped<HotelOps.Api.Security.Authorization.IPermissionDataSource,HotelOps.Api.Security.Authorization.EfPermissionDataSource>();
        services.AddScoped<HotelOps.Api.Security.Authorization.IPermissionResourceResolver,HotelOps.Api.Security.Authorization.UnresolvedPermissionResourceResolver>();
        services.AddScoped<HotelOps.Api.Security.Authorization.IPermissionService,HotelOps.Api.Security.Authorization.PermissionService>();
        services.AddScoped<HotelOps.Api.Services.Authentication.ISessionRevocationService,HotelOps.Api.Services.Authentication.SessionRevocationService>();
        services.AddScoped<HotelOps.Api.Services.Authentication.ISqlSessionService,HotelOps.Api.Services.Authentication.SqlSessionService>();
        services.AddScoped<RoomTypeCreationService>();
        services.AddDbContext<HotelOps.Api.Data.HotelOpsDbContext>((sp,o)=>o.UseSqlServer("Server=invalid;Database=unopened;Integrated Security=true").AddInterceptors(sp.GetRequiredService<NotificationCommitGuard>()));
        using var provider=services.BuildServiceProvider(new ServiceProviderOptions{ValidateOnBuild=true,ValidateScopes=true});using var scope=provider.CreateScope();
        Check("Opt-in coordination/recipient dependency graph resolves",scope.ServiceProvider.GetRequiredService<RoomTypeCreationService>()!=null&&scope.ServiceProvider.GetRequiredService<IFe05MandatoryRecipientAuthority>()!=null);
        Check("N01 sees exact same scoped prelock instance",ReferenceEquals(scope.ServiceProvider.GetRequiredService<InspectionUsersPrelock>(),scope.ServiceProvider.GetRequiredService<IFe05RecipientPrelockAuthority>()));
        Check("Candidate composition opens no SQL connection",scope.ServiceProvider.GetRequiredService<HotelOps.Api.Data.HotelOpsDbContext>().Database.GetDbConnection().State==System.Data.ConnectionState.Closed);
        Console.WriteLine($"{count}/{count} PASS structural/unit checks; no source-read grant or persisted authority acceptance");
    }
    private sealed class NoActor:HotelOps.Api.Security.Authorization.ICurrentUserAccessor{public bool TryGetCurrentUser(out HotelOps.Api.Security.Authorization.CurrentUserInfo user){user=default;return false;}}
}
