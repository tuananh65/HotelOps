using System.Text.RegularExpressions;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Security;
using HotelOps.Api.Compact.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
namespace HotelOps.Compact.Tests;
public sealed class WorkerDatabaseFactAttribute:FactAttribute
{
    public WorkerDatabaseFactAttribute(){if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOTELOPS_COMPACT_WORKER_TEST")))Skip="Run Test-Worker.ps1 against an isolated API fixture database.";}
}
public sealed class WorkerIntegrationTests
{
    [WorkerDatabaseFact]
    public async Task RecurrenceAndLowStockAreCommittedOnceAcrossRepeatedTicks()
    {
        var connection=Environment.GetEnvironmentVariable("HOTELOPS_COMPACT_WORKER_TEST")!;
        Assert.Matches(new Regex("^HotelOpsCompactApiTest[a-f0-9]{12}$"),new SqlConnectionStringBuilder(connection).InitialCatalog);
        var config=new ConfigurationBuilder().Build();var services=new ServiceCollection();
        services.AddDbContext<HotelOpsCompactDbContext>(o=>o.UseSqlServer(connection));services.AddHttpContextAccessor();
        services.AddSingleton<TimeProvider>(TimeProvider.System);services.AddScoped<Actor>();services.AddScoped<Access>();services.AddScoped<Atomic>();services.AddScoped<NotificationService>();
        await using var provider=services.BuildServiceProvider();
        var worker=new OperationsWorker(provider.GetRequiredService<IServiceScopeFactory>(),NullLogger<OperationsWorker>.Instance,config);
        long templateId,policyId;
        await using(var scope=provider.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();
            var user=await db.Users.SingleAsync(u=>u.Username=="qa_admin");var branch=await db.Branches.SingleAsync(b=>b.Code=="QA");
            var item=await db.InventoryItems.SingleAsync(i=>i.Code=="SOAP");var warehouse=await db.Warehouses.SingleAsync(w=>w.Code=="MAIN");
            var template=new WorkTemplate{BranchId=branch.BranchId,Code=Guid.NewGuid().ToString("N"),Title="Worker integration verification",Kind="OPERATIONAL",Frequency="DAILY",FrequencyInterval=1,NextDueAt=DateTime.UtcNow.AddMinutes(-1),CreatedBy=user.UserId,IsActive=true};
            db.WorkTemplates.Add(template);
            var policy=await db.InventoryPolicies.SingleOrDefaultAsync(p=>p.WarehouseId==warehouse.WarehouseId&&p.ItemId==item.ItemId);
            if(policy==null){policy=new InventoryPolicy{BranchId=branch.BranchId,WarehouseId=warehouse.WarehouseId,ItemId=item.ItemId};db.InventoryPolicies.Add(policy);}
            policy.MinimumQty=100;policy.TargetQty=100;policy.LowStockEpisode=null;policy.AlertedAt=null;
            await db.SaveChangesAsync();templateId=template.WorkTemplateId;policyId=policy.InventoryPolicyId;
        }
        await worker.Tick(CancellationToken.None);
        Guid episode;int alerts;
        await using(var scope=provider.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();
            Assert.Equal(1,await db.WorkItems.CountAsync(w=>w.WorkTemplateId==templateId));
            Assert.True((await db.WorkTemplates.SingleAsync(t=>t.WorkTemplateId==templateId)).NextDueAt>DateTime.UtcNow);
            var policy=await db.InventoryPolicies.SingleAsync(p=>p.InventoryPolicyId==policyId);
            Assert.NotNull(policy.LowStockEpisode);Assert.NotNull(policy.AlertedAt);episode=policy.LowStockEpisode!.Value;
            alerts=await db.Notifications.CountAsync(n=>n.EventKey==episode);Assert.True(alerts>0);
        }
        await worker.Tick(CancellationToken.None);
        await using(var scope=provider.CreateAsyncScope()){
            var db=scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();
            Assert.Equal(1,await db.WorkItems.CountAsync(w=>w.WorkTemplateId==templateId));
            Assert.Equal(alerts,await db.Notifications.CountAsync(n=>n.EventKey==episode));
            Assert.Equal(episode,(await db.InventoryPolicies.SingleAsync(p=>p.InventoryPolicyId==policyId)).LowStockEpisode);
        }
    }
}
