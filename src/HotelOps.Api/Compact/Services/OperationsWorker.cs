using System.Security.Cryptography;
using System.Text;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Compact.Services;
public sealed class OperationsWorker(IServiceScopeFactory scopes,ILogger<OperationsWorker> logger,IConfiguration config):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var scheduled=config.GetValue("Compact:WorkerEnabled",true);
        var cleaning=config.GetValue("Compact:CleaningOverdueEnabled",true);
        if(!scheduled&&!cleaning)return;
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        do{try{
            if(cleaning){await using var scope=scopes.CreateAsyncScope();await CleaningDeadline.Scan(scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>(),DateTime.UtcNow,stoppingToken);}
            if(scheduled)await Tick(stoppingToken);
        }catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}catch(Exception e){logger.LogError(e,"Compact scheduled operations failed; transaction rolled back");}}
        while(await timer.WaitForNextTickAsync(stoppingToken));
    }
    public async Task Tick(CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<HotelOpsCompactDbContext>();var notifications=scope.ServiceProvider.GetRequiredService<NotificationService>();var now=DateTime.UtcNow;
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        // Serialize workers across instances using an existing SQL application lock, not another table.
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'Compact.OperationsWorker',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0; IF @r<0 THROW 51400,'Worker is already running.',1;",ct);
        var templates=await db.WorkTemplates.Where(t=>t.IsActive&&t.NextDueAt<=now&&(t.Kind=="OPERATIONAL"||t.Kind=="MAINTENANCE")&&db.Branches.Any(b=>b.BranchId==t.BranchId&&b.IsActive)).OrderBy(t=>t.NextDueAt).Take(50).ToArrayAsync(ct);
        foreach(var t in templates){
            var due=t.NextDueAt!.Value;
            if(!await db.WorkItems.AnyAsync(w=>w.WorkTemplateId==t.WorkTemplateId&&w.OccurrenceAt==due,ct)){
                var w=new WorkItem{BranchId=t.BranchId,Kind=t.Kind,Status=WorkRules.Initial(t.Kind),WorkNo=Guid.NewGuid().ToString("N"),Title=t.Title,Description=t.Instruction,DepartmentId=t.DepartmentId,RoomId=t.RoomId,AssetId=t.AssetId,
                    WorkTemplateId=t.WorkTemplateId,ChecklistTemplateId=t.ChecklistTemplateId,OccurrenceAt=due,DueAt=due,CreatedBy=t.CreatedBy};
                db.WorkItems.Add(w);await db.SaveChangesAsync(ct);
                if(t.ChecklistTemplateId!=null)foreach(var item in await db.ChecklistItems.Where(i=>i.ChecklistTemplateId==t.ChecklistTemplateId).ToArrayAsync(ct))
                    db.WorkChecklistItems.Add(new WorkChecklistItem{BranchId=w.BranchId,WorkId=w.WorkId,ChecklistTemplateId=t.ChecklistTemplateId.Value,ChecklistItemId=item.ChecklistItemId,DescriptionSnapshot=item.Description,RequiredSnapshot=item.IsRequired,WeightSnapshot=item.Weight});
                db.WorkEvents.Add(new WorkEvent{BranchId=w.BranchId,WorkId=w.WorkId,ActorUserId=t.CreatedBy,EventType="SCHEDULE_GENERATED",OperationKey=Guid.NewGuid(),ToStatus=w.Status});
                db.AuditLogs.Add(new AuditLog{BranchId=w.BranchId,ActorUserId=t.CreatedBy,EntityType="Work",EntityKey=w.WorkId.ToString(),Action="SCHEDULE_GENERATED",OperationKey=Guid.NewGuid()});
                await notifications.ForPermission(w.BranchId,WorkRules.Permission(w.Kind,"assign"),"SCHEDULE_GENERATED",w.Title,w.WorkId,null,Key($"schedule:{w.WorkId}"),ct);
            }
            t.NextDueAt=t.Frequency switch{"DAILY"=>due.AddDays(t.FrequencyInterval??1),"WEEKLY"=>due.AddDays(7*(t.FrequencyInterval??1)),"MONTHLY"=>due.AddMonths(t.FrequencyInterval??1),_=>throw new InvalidOperationException("Unsupported recurrence")};
        }
        foreach(var w in await db.WorkItems.Where(w=>w.DueAt<now&&!WorkRules.Terminal.Contains(w.Status)).OrderBy(w=>w.DueAt).Take(500).ToArrayAsync(ct)){
            var key=Key($"overdue:{w.WorkId}:{w.DueAt:O}");
            if(!await db.Notifications.AnyAsync(n=>n.EventKey==key,ct))await notifications.ForPermission(w.BranchId,WorkRules.Permission(w.Kind,"assign"),"WORK_OVERDUE",w.Title,w.WorkId,null,key,ct);
        }
        await scope.ServiceProvider.GetRequiredService<LowStockService>().Reconcile(ct);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    private static Guid Key(string value)=>new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0,16));
}
