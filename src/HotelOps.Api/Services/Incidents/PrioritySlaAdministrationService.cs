using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.Incidents;

public sealed record PrioritySlaInput(string Label,int ResponseMinutes,int ResolutionMinutes);
public sealed record PrioritySlaView(long Id,string Code,string Label,int? ResponseMinutes,int? ResolutionMinutes,bool Configured,string Version);
public sealed class PrioritySlaAdministrationService(HotelOpsDbContext db,WorkCategoryAdministrationService authority)
{
    private static readonly string[] Codes=["LOW","NORMAL","HIGH","CRITICAL"];
    private sealed class Target
    {
        public int ResponseMinutes {get;set;}
        public int ResolutionMinutes {get;set;}
        public bool IsActive {get;set;}
        public string Version {get;set;}="";
    }
    private Task<Target?> TargetAsync(long id,CancellationToken ct)=>db.Database.SqlQuery<Target>($"""
        SELECT response_minutes AS ResponseMinutes,resolution_minutes AS ResolutionMinutes,is_active AS IsActive,
            CONVERT(varchar(16),row_version,2) AS Version
        FROM dbo.incident_priority_sla_targets WITH(UPDLOCK,HOLDLOCK) WHERE priority_id={id}
        """).SingleOrDefaultAsync(ct);
    private static PrioritySlaView View(ReferenceValue row,Target? target)
    {
        var version='"'+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{
            row.ReferenceId,row.GroupCode,row.ValueCode,row.DisplayName,row.IsActive,target}))))+'"';
        return new(row.ReferenceId,row.ValueCode,row.DisplayName,target?.ResponseMinutes,target?.ResolutionMinutes,
            row.IsActive&&target is {IsActive:true},version);
    }
    private async Task Begin(SessionReference ticket,long sid,CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        // Reference values and SLA configuration are shared: a branch grant cannot mutate them.
        await authority.Gate(ticket,sid,null,ct);
        if(!await IncidentTriageStore.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"INCIDENT_SLA_STORAGE_UNAVAILABLE");
    }
    public async Task<IReadOnlyList<PrioritySlaView>> ListAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,ct);
        var ids=await db.ReferenceValues.AsNoTracking().Where(r=>r.GroupCode=="PRIORITY"&&Codes.Contains(r.ValueCode)).OrderBy(r=>r.SortOrder).ThenBy(r=>r.ReferenceId).Select(r=>r.ReferenceId).ToArrayAsync(ct);
        var result=new List<PrioritySlaView>();
        foreach(var id in ids){var target=await TargetAsync(id,ct);var row=await db.ReferenceValues.AsNoTracking().SingleAsync(r=>r.ReferenceId==id,ct);result.Add(View(row,target));}
        await authority.Gate(ticket,sid,null,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<PrioritySlaView> SaveAsync(SessionReference ticket,long sid,long id,string? tag,PrioritySlaInput input,CancellationToken ct)
    {
        var label=input.Label?.Trim();
        if(id<=0||string.IsNullOrWhiteSpace(label)||label.Length>150||label.Any(char.IsControl)||input.ResponseMinutes<=0||input.ResolutionMinutes<=0)
            throw new InternalRequestReadException(400,"INVALID_PRIORITY_SLA");
        if(string.IsNullOrEmpty(tag))throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,ct);
        var target=await TargetAsync(id,ct);
        var row=await db.ReferenceValues.FromSqlInterpolated($"SELECT * FROM dbo.reference_values WITH(UPDLOCK,HOLDLOCK) WHERE reference_id={id}").SingleOrDefaultAsync(ct);
        if(row==null||row.GroupCode!="PRIORITY"||!Codes.Contains(row.ValueCode)||!row.IsActive)throw new InternalRequestReadException(409,"FIXED_ACTIVE_PRIORITY_REQUIRED");
        var before=View(row,target);if(before.Version!=tag)throw new InternalRequestReadException(412,"PRIORITY_SLA_CHANGED");
        row.DisplayName=label;await db.SaveChangesAsync(ct);
        if(target==null)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.incident_priority_sla_targets(priority_id,response_minutes,resolution_minutes,is_active) VALUES({id},{input.ResponseMinutes},{input.ResolutionMinutes},1)",ct);
        else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.incident_priority_sla_targets SET response_minutes={input.ResponseMinutes},resolution_minutes={input.ResolutionMinutes},is_active=1 WHERE priority_id={id}",ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var after=View(row,await TargetAsync(id,ct));
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,EntityType="reference_values",EntityId=id.ToString(CultureInfo.InvariantCulture),Action="UPDATE",OccurredAt=now,
            OldValues=JsonSerializer.Serialize(before),NewValues=JsonSerializer.Serialize(new{operation="PRIORITY_SLA_CONFIGURE",priority=after}),ChangedFields="[\"display_name\",\"sla_targets\"]"});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await authority.Gate(ticket,sid,null,ct);await tx.CommitAsync(ct);return after;
    }
}
