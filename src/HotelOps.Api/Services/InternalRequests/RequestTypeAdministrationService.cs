using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.OperationalTasks;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.InternalRequests;

public sealed record RequestTypeInput(string Code,string Label,string? Description,bool? MaterialNeed,bool IsActive,bool Confirmation);
public sealed record RequestTypeView(long Id,string Code,string Label,string? Description,bool? MaterialNeed,bool IsActive,bool MeaningLocked,string Version);
public sealed class RequestTypeAdministrationService(HotelOpsDbContext db,WorkCategoryAdministrationService authority)
{
    private async Task Begin(SessionReference ticket,long sid,CancellationToken ct)
    {
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await authority.Gate(ticket,sid,null,ct);
        if(!await InternalRequestIntakePolicy.InstalledAsync(db,ct))throw new InternalRequestReadException(503,"IR_INTAKE_SCHEMA_REQUIRED");
    }
    private async Task<RequestTypeView> View(InternalRequestType row,CancellationToken ct)
    {
        var policy=await InternalRequestIntakePolicy.Types(db).SingleOrDefaultAsync(p=>p.TypeId==row.RequestTypeId,ct);
        var used=await db.InternalRequests.AsNoTracking().AnyAsync(r=>r.RequestTypeId==row.RequestTypeId,ct);
        var key=row.RequestTypeId.ToString(CultureInfo.InvariantCulture);
        var audit=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="internal_request_types"&&a.EntityId==key).Select(a=>(long?)a.AuditId).MaxAsync(ct)??0;
        var version='"'+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{row.RequestTypeId,row.TypeCode,row.TypeName,row.Description,row.IsActive,
            row.DefaultDepartmentId,row.DefaultPriorityId,row.SlaMinutes,policy,audit}))))+'"';
        return new(row.RequestTypeId,row.TypeCode,row.TypeName,row.Description,policy?.MaterialNeed,row.IsActive,used,version);
    }
    public async Task<IReadOnlyList<RequestTypeView>> ListAsync(SessionReference ticket,long sid,long after,CancellationToken ct)
    {
        if(after<0)throw new InternalRequestReadException(400,"INVALID_CURSOR");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,ct);
        var rows=await db.InternalRequestTypes.AsNoTracking().Where(r=>r.RequestTypeId>after).OrderBy(r=>r.RequestTypeId).Take(100).ToArrayAsync(ct);
        var result=new List<RequestTypeView>();foreach(var row in rows)result.Add(await View(row,ct));
        await authority.Gate(ticket,sid,null,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<RequestTypeView> SaveAsync(SessionReference ticket,long sid,long? id,string? tag,RequestTypeInput input,CancellationToken ct)
    {
        var code=input.Code?.Trim().ToUpperInvariant();var label=input.Label?.Trim();var description=input.Description?.Trim();
        if(id is <=0||string.IsNullOrWhiteSpace(code)||code.Length>40||code.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-')
            ||string.IsNullOrWhiteSpace(label)||label.Length>150||label.Any(char.IsControl)||description?.Length>4000
            ||description?.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')==true)
            throw new InternalRequestReadException(400,"INVALID_REQUEST_TYPE");
        if(id!=null&&string.IsNullOrEmpty(tag))throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        if(!input.IsActive&&!input.Confirmation)throw new InternalRequestReadException(400,"DEACTIVATION_CONFIRMATION_REQUIRED");
        if(id==null&&(!input.IsActive||input.MaterialNeed==null))throw new InternalRequestReadException(400,"ACTIVE_CLASSIFIED_TYPE_REQUIRED");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await Begin(ticket,sid,ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:RequestTypeAdministration:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51883,'Request type catalog busy.',1;",ct);
        InternalRequestType row;RequestTypeView? before=null;
        if(id is {} value)
        {
            row=await db.InternalRequestTypes.FromSqlInterpolated($"SELECT * FROM dbo.internal_request_types WITH(UPDLOCK,HOLDLOCK) WHERE request_type_id={value}").SingleOrDefaultAsync(ct)??throw new InternalRequestReadException(404,"REQUEST_TYPE_UNAVAILABLE");
            before=await View(row,ct);if(before.Version!=tag)throw new InternalRequestReadException(412,"REQUEST_TYPE_CHANGED");
            if(before.MeaningLocked&&(code!=row.TypeCode||label!=row.TypeName||description!=row.Description||input.MaterialNeed!=before.MaterialNeed))throw new InternalRequestReadException(409,"USED_REQUEST_TYPE_IMMUTABLE");
            if(input.MaterialNeed==null&&input.IsActive)throw new InternalRequestReadException(400,"REQUEST_TYPE_CLASSIFICATION_REQUIRED");
        }
        else {row=new InternalRequestType();db.InternalRequestTypes.Add(row);}
        if(await db.InternalRequestTypes.AnyAsync(r=>r.RequestTypeId!=row.RequestTypeId&&r.TypeCode.ToUpper()==code,ct))throw new InternalRequestReadException(409,"MSG35");
        row.TypeCode=code;row.TypeName=label;row.Description=description;row.IsActive=input.IsActive;
        await db.SaveChangesAsync(ct);
        if(input.MaterialNeed is {} material)
        {
            if(await InternalRequestIntakePolicy.Types(db).AnyAsync(p=>p.TypeId==row.RequestTypeId,ct))
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.internal_request_type_policy SET is_material_need={material} WHERE request_type_id={row.RequestTypeId}",ct);
            else await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_type_policy(request_type_id,is_material_need) VALUES({row.RequestTypeId},{material})",ct);
        }
        else if(before?.MaterialNeed!=null)throw new InternalRequestReadException(409,"CLASSIFICATION_REMOVAL_NOT_ALLOWED");
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,EntityType="internal_request_types",EntityId=row.RequestTypeId.ToString(CultureInfo.InvariantCulture),Action=id==null?"CREATE":"UPDATE",OccurredAt=now,
            OldValues=before==null?null:JsonSerializer.Serialize(before),NewValues=JsonSerializer.Serialize(new{row.TypeCode,row.TypeName,row.Description,row.IsActive,input.MaterialNeed,row.DefaultDepartmentId,row.DefaultPriorityId,row.SlaMinutes})});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await authority.Gate(ticket,sid,null,ct);var result=await View(row,ct);await tx.CommitAsync(ct);return result;
    }
}
