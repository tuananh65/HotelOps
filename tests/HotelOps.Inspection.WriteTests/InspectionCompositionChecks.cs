using HotelOps.Api.Data;
using HotelOps.Api.Entities.Notifications;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Security.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
internal static class InspectionCompositionChecks
{
    internal static async Task RunAsync()
    {
        int count=0;
        void Check(string name,bool condition){if(!condition)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
        var services=new ServiceCollection();
        services.AddInspectionConclusionComposition(new ConfigurationBuilder().Build());
        services.AddLogging();services.AddSingleton<ICurrentUserAccessor,NoActor>();
        services.AddScoped<IPermissionDataSource,EfPermissionDataSource>();
        services.AddDbContext<HotelOpsDbContext>((sp,o)=>o.UseSqlServer("Server=invalid;Database=unopened;Integrated Security=true")
            .AddInterceptors(sp.GetRequiredService<NotificationCommitGuard>()));
        using var provider=services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true,ValidateOnBuild=true});
        using var scope=provider.CreateScope();using var other=provider.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
        Check("N01 staging resolves through exact source DI",scope.ServiceProvider.GetRequiredService<Fe05OperationStaging>()!=null);
        Check("One guard singleton across caller scopes",ReferenceEquals(scope.ServiceProvider.GetRequiredService<NotificationCommitGuard>(),other.ServiceProvider.GetRequiredService<NotificationCommitGuard>()));
        Check("No worker registered",!services.Any(x=>x.ServiceType==typeof(IHostedService)));
        Check("No producer profile registered",!services.Any(x=>x.ServiceType==typeof(INotificationEventProfile)));
        Check("Notification mapping uses approved partial hook",db.Model.FindEntityType(typeof(NotificationIntent))?.GetTableName()=="notification_intents_c04");
        Check("FS013 model preserved",db.Model.GetEntityTypes().Any(x=>x.GetTableName()=="notification_operation_reservations"));
        Check("Reject public activation remains closed",RoomTypeCreationService.RejectWriteDependencies.Length>0);
        Check("Rework public activation requires byte immutability",RoomTypeCreationService.ReworkWriteDependencies.Contains("PRIVATE_EVIDENCE_IMMUTABILITY_UNACCEPTED"));
        try{services=new ServiceCollection();services.AddInspectionConclusionComposition(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"NotificationDelivery:Enabled","true"}}).Build());throw new Exception("Worker enabled");}
        catch(InvalidOperationException e)when(e.Message.Contains("not authorized")){count++;Console.WriteLine("PASS Worker cannot activate by configuration");}
        try{await scope.ServiceProvider.GetRequiredService<IFe05TrialExecutionAuthorization>().RequireAsync(default);throw new Exception("Remote lane enabled");}
        catch(Fe05NotificationUnavailableException){count++;Console.WriteLine("PASS Remote trial execution remains closed");}
        Check("DI/model checks open no SQL connection",db.Database.GetDbConnection().State==System.Data.ConnectionState.Closed);
        Console.WriteLine($"{count}/{count} PASS composition/model checks; no SQL or authority acceptance");
    }
    private sealed class NoActor:ICurrentUserAccessor {public bool TryGetCurrentUser(out CurrentUserInfo value){value=default;return false;}}
}
