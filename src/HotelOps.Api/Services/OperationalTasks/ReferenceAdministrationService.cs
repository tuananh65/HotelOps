using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.OperationalTasks;

public sealed record ReferenceInput(string Code,string Label,string? Description,int Order,bool IsActive,bool Confirmation);
public sealed record ReferenceView(long Id,string Code,string Label,string? Description,int Order,bool IsActive,bool MeaningLocked,string Version);
public sealed class ReferenceAdministrationService(HotelOpsDbContext db,WorkCategoryAdministrationService authority)
{
    private static readonly string[] Groups=["INCIDENT_CATEGORY","CLEANING_TYPE","BLOCKER","TASK_PAUSE_REASON"];
    private static readonly string[] PauseCodes=["WAITING_MATERIAL","WAITING_EQUIPMENT","WAITING_APPROVAL","ACCESS_BLOCKED","SAFETY_ISSUE","OTHER","END_OF_SHIFT"];
    private static void Group(string group){if(!Groups.Contains(group))throw new InternalRequestReadException(400,"UNSUPPORTED_REFERENCE_GROUP");}
    private async Task Begin(SessionReference ticket,long sid,CancellationToken ct)
    {await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await authority.Gate(ticket,sid,null,ct);}
    private async Task<bool> Used(long id,CancellationToken ct)
    {
        // Enumerate real foreign keys, including extension tables absent from generated EF entities.
        // Identifiers originate only from SQL metadata; the value remains a parameter.
        var result=await db.Database.SqlQuery<int>($"""
            DECLARE @sql nvarchar(max);
            SELECT @sql=STRING_AGG(CAST(N' OR EXISTS(SELECT 1 FROM ' AS nvarchar(max))+QUOTENAME(OBJECT_SCHEMA_NAME(f.parent_object_id))+N'.'+
                QUOTENAME(OBJECT_NAME(f.parent_object_id))+N' WITH(HOLDLOCK) WHERE '+QUOTENAME(COL_NAME(f.parent_object_id,f.parent_column_id))+N'=@id)',N'')
            FROM sys.foreign_key_columns f WHERE f.referenced_object_id=OBJECT_ID(N'dbo.reference_values')
                AND COL_NAME(f.referenced_object_id,f.referenced_column_id)=N'reference_id';
            IF @sql IS NULL THROW 51882,'Reference dependency metadata unavailable.',1;
            SET @sql=N'SELECT CASE WHEN 1=0'+@sql+N' THEN 1 ELSE 0 END AS Value';
            EXEC sys.sp_executesql @sql,N'@id bigint',@id={id};
            """).ToListAsync(ct);
        return result.Single()==1;
    }
    private async Task<ReferenceView> View(ReferenceValue r,CancellationToken ct)
    {
        var used=r.GroupCode=="TASK_PAUSE_REASON"||await Used(r.ReferenceId,ct);
        var key=r.ReferenceId.ToString(CultureInfo.InvariantCulture);
        var audit=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="reference_values"&&a.EntityId==key).Select(a=>(long?)a.AuditId).MaxAsync(ct)??0;
        var version='"'+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{r.ReferenceId,r.GroupCode,r.ValueCode,r.DisplayName,r.Description,r.SortOrder,r.IsActive,audit}))))+'"';
        return new(r.ReferenceId,r.ValueCode,r.DisplayName,r.Description,r.SortOrder??0,r.IsActive,used,version);
    }
    public async Task<IReadOnlyList<ReferenceView>> ListAsync(SessionReference ticket,long sid,string group,long after,CancellationToken ct)
    {
        Group(group);if(after<0)throw new InternalRequestReadException(400,"INVALID_CURSOR");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,ct);
        var rows=await db.ReferenceValues.AsNoTracking().Where(r=>r.GroupCode==group&&r.ReferenceId>after).OrderBy(r=>r.ReferenceId).Take(100).ToArrayAsync(ct);
        var items=new List<ReferenceView>();foreach(var row in rows)items.Add(await View(row,ct));
        await authority.Gate(ticket,sid,null,ct);await tx.CommitAsync(ct);return items;
    }
    public async Task<ReferenceView> SaveAsync(SessionReference ticket,long sid,string group,long? id,string? tag,ReferenceInput input,CancellationToken ct)
    {
        Group(group);var code=input.Code?.Trim().ToUpperInvariant();var label=input.Label?.Trim();var description=input.Description?.Trim();
        if(id is <=0||string.IsNullOrWhiteSpace(code)||code.Length>60||code.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-')
            ||string.IsNullOrWhiteSpace(label)||label.Length>150||label.Any(char.IsControl)||description?.Length>4000
            ||description?.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')==true)
            throw new InternalRequestReadException(400,"INVALID_REFERENCE_INPUT");
        if(id!=null&&string.IsNullOrEmpty(tag))throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        if(!input.IsActive&&!input.Confirmation)throw new InternalRequestReadException(400,"DEACTIVATION_CONFIRMATION_REQUIRED");
        if(id==null&&(!input.IsActive||group=="TASK_PAUSE_REASON"))throw new InternalRequestReadException(409,"REFERENCE_CREATION_NOT_ALLOWED");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:ReferenceAdministration:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51881,'Reference catalog busy.',1;",ct);
        ReferenceValue row;ReferenceView? before=null;
        if(id is {} value)
        {
            row=await db.ReferenceValues.FromSqlInterpolated($"SELECT * FROM dbo.reference_values WITH(UPDLOCK,HOLDLOCK) WHERE reference_id={value}").SingleOrDefaultAsync(ct)??throw new InternalRequestReadException(404,"REFERENCE_UNAVAILABLE");
            if(row.GroupCode!=group)throw new InternalRequestReadException(404,"REFERENCE_UNAVAILABLE");
            before=await View(row,ct);if(before.Version!=tag)throw new InternalRequestReadException(412,"REFERENCE_CHANGED");
            if(before.MeaningLocked&&(code!=row.ValueCode||label!=row.DisplayName||description!=row.Description))throw new InternalRequestReadException(409,"REFERENCE_MEANING_IMMUTABLE");
            if(group=="TASK_PAUSE_REASON"&&(!PauseCodes.Contains(code)||code=="END_OF_SHIFT"&&!input.IsActive))throw new InternalRequestReadException(409,"SYSTEM_REASON_REQUIRED");
        }
        else {row=new ReferenceValue{GroupCode=group};db.ReferenceValues.Add(row);}
        if(await db.ReferenceValues.AnyAsync(r=>r.ReferenceId!=row.ReferenceId&&r.GroupCode==group&&r.ValueCode.ToUpper()==code,ct))throw new InternalRequestReadException(409,"MSG35");
        row.ValueCode=code;row.DisplayName=label;row.Description=description;row.SortOrder=input.Order;row.IsActive=input.IsActive;
        await db.SaveChangesAsync(ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,EntityType="reference_values",EntityId=row.ReferenceId.ToString(CultureInfo.InvariantCulture),Action=id==null?"CREATE":"UPDATE",OccurredAt=now,
            OldValues=before==null?null:JsonSerializer.Serialize(before),NewValues=JsonSerializer.Serialize(new{row.GroupCode,row.ValueCode,row.DisplayName,row.Description,row.SortOrder,row.IsActive})});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await authority.Gate(ticket,sid,null,ct);var result=await View(row,ct);await tx.CommitAsync(ct);return result;
    }
}
