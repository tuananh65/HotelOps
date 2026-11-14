using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Notifications;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningNotificationConsumerCompositionTests
{
    private static ServiceProvider Provider(string environment="Production",string? candidate=null)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ConnectionStrings:HotelOpsDb"]="Data Source=not-a-runtime-target;Initial Catalog=NoSqlComposition;Integrated Security=True;Encrypt=True",
            ["CleaningAssignment:Candidate"]=candidate}).Build();
        var services=new ServiceCollection();
        services.AddHotelOpsFe04CleaningNotificationConsumer(config);
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IWebHostEnvironment>(new TestEnvironment{EnvironmentName=environment});
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor,CurrentUserAccessor>();
        services.AddScoped<IPermissionDataSource,EfPermissionDataSource>();
        services.AddScoped<IPermissionResourceResolver,UnresolvedPermissionResourceResolver>();
        services.AddScoped<IPermissionService,PermissionService>();
        services.AddScoped<ISessionRevocationService,SessionRevocationService>();
        services.AddScoped<ISqlSessionService,SqlSessionService>();
        services.AddScoped<ProvisionalCleaningNotification>();
        services.AddScoped<RoomTypeCreationService>();
        return services.BuildServiceProvider(new ServiceProviderOptions{ValidateScopes=true,ValidateOnBuild=true});
    }

    [Fact]
    public async Task Production_registration_resolves_one_shared_guard_and_preserves_both_DEV1_guards()
    {
        await using var provider=Provider();
        using var first=provider.CreateScope();using var second=provider.CreateScope();
        var guard=provider.GetRequiredService<NotificationCommitGuard>();
        Assert.Same(guard,first.ServiceProvider.GetRequiredService<NotificationCommitGuard>());
        Assert.Same(guard,second.ServiceProvider.GetRequiredService<NotificationCommitGuard>());
        Assert.Single(provider.GetServices<NotificationCommitGuard>());
        var db=first.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
        var interceptors=db.GetService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>().Single().Interceptors!.ToArray();
        Assert.Single(interceptors.OfType<NotificationCommitGuard>());Assert.Contains(guard,interceptors);
        Assert.Contains(CandidateCommitGuard.Instance,interceptors);Assert.Contains(CandidateCommandGuard.Instance,interceptors);
        Assert.IsType<CleaningNotificationParticipant>(first.ServiceProvider.GetRequiredService<CleaningNotificationParticipant>());
        Assert.IsType<NotificationTransactionParticipant>(first.ServiceProvider.GetRequiredService<NotificationTransactionParticipant>());
        Assert.Single(first.ServiceProvider.GetServices<INotificationPublisher>());
        Assert.Empty(provider.GetServices<IHostedService>());
        Assert.Empty(first.ServiceProvider.GetServices<INotificationEventProfile>());
        Assert.IsType<UnacceptedFe05Source>(first.ServiceProvider.GetRequiredService<IFe05NotificationSource>());
        Assert.IsType<UnacceptedFe05Coordination>(first.ServiceProvider.GetRequiredService<IFe05SourceCoordination>());
        Assert.Equal(ConnectionState.Closed,db.Database.GetDbConnection().State);
    }

    [Theory]
    [InlineData("Production",null)]
    [InlineData("Production","FS013_PROVISIONAL")]
    [InlineData("Development",null)]
    [InlineData("Development","FS013_PROVISIONAL")]
    public async Task Actual_UC029_consumer_stays_closed_without_runtime_prerequisites(string environment,string? candidate)
    {
        await using var provider=Provider(environment,candidate);using var scope=provider.CreateScope();
        var service=scope.ServiceProvider.GetRequiredService<RoomTypeCreationService>();
        var ticket=SessionReference.FromProtectedTicket(3,SessionCredential.Generate(),false,DateTime.UtcNow);
        var result=await service.AssignCleaningCandidateAsync(ticket,1,7,17,"\"0000000000000001\"",
            Guid.NewGuid().ToString("D"),new AssignCleaningRequest(31,DateTime.UtcNow.AddHours(1),"Composition gate check"),default);
        Assert.Equal(RoomTypeBatchStatus.Unavailable,result.Status);
        var db=scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>();
        Assert.Equal(ConnectionState.Closed,db.Database.GetDbConnection().State);
        Assert.Empty(db.ChangeTracker.Entries());Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Producer_inbox_and_delivery_activation_fail_before_SQL()
    {
        await using var provider=Provider();using var scope=provider.CreateScope();
        var activation=scope.ServiceProvider.GetRequiredService<ICleaningNotificationActivation>();
        foreach(var capability in Enum.GetValues<CleaningNotificationCapability>())
            await Assert.ThrowsAsync<NotificationContractUnavailableException>(()=>activation.RequireAsync(capability,default));
        var runtime=scope.ServiceProvider.GetRequiredService<ICleaningNotificationRuntime>();
        await Assert.ThrowsAsync<NotificationContractUnavailableException>(()=>runtime.RunContainedBatchAsync(default));
        await Assert.ThrowsAsync<NotificationContractUnavailableException>(()=>runtime.ListAsync(31,1,20,default));
        Assert.Equal(ConnectionState.Closed,scope.ServiceProvider.GetRequiredService<HotelOpsDbContext>().Database.GetDbConnection().State);
    }

    [Fact]
    public void Owner_receipt_adaptation_preserves_all_fields_and_existing_replay_JSON()
    {
        var owner=new CandidateAssignmentResult(17,23,7,31,Guid.NewGuid(),Guid.NewGuid(),"ASSIGNED","PENDING_DELIVERY","\"0000000000000001\"");
        var adapted=CleaningAssignmentNotificationReceipt.Adapt(owner);
        var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Equal(JsonSerializer.Serialize(owner,json),JsonSerializer.Serialize(adapted,json));
        Assert.Equal(owner,ProvisionalCleaningNotification.ReadResult(JsonSerializer.Serialize(adapted,json)));
        Assert.NotEqual(adapted,CleaningAssignmentNotificationReceipt.Adapt(owner with{PublicationId=Guid.NewGuid()}));
        Assert.NotEqual(adapted,CleaningAssignmentNotificationReceipt.Adapt(owner with{AssignmentId=24}));
        Assert.NotEqual(adapted,CleaningAssignmentNotificationReceipt.Adapt(owner with{OperationId=Guid.NewGuid()}));
    }

    private sealed class TestEnvironment:IWebHostEnvironment
    {
        public string EnvironmentName{get;set;}="Production";
        public string ApplicationName{get;set;}="HotelOps.Api";
        public string ContentRootPath{get;set;}=AppContext.BaseDirectory;
        public string WebRootPath{get;set;}=AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
        public IFileProvider WebRootFileProvider{get;set;}=new NullFileProvider();
    }
}
