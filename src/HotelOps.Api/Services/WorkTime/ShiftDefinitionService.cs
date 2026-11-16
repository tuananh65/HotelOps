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
namespace HotelOps.Api.Services.WorkTime;
public sealed record ShiftDefinitionInput(string Code,string Name,TimeOnly Start,TimeOnly End,bool CrossesMidnight);
public sealed record ShiftDefinitionView(long Id,string Code,string Name,TimeOnly Start,TimeOnly End,bool CrossesMidnight,int BreakMinutes,bool IsActive,string Version);
public sealed class ShiftDefinitionService(HotelOpsDbContext db,ShiftRosterService authority)
{
    private async Task<ShiftDefinitionView> View(ShiftTemplate row,CancellationToken ct)
    {
        var key=row.ShiftTemplateId.ToString(CultureInfo.InvariantCulture);
        var audit=await db.AuditLogs.AsNoTracking().Where(a=>a.EntityType=="shift_templates"&&a.EntityId==key).Select(a=>(long?)a.AuditId).MaxAsync(ct)??0;
        var version='"'+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{row.ShiftTemplateId,row.BranchId,row.ShiftCode,row.ShiftName,row.StartTime,row.EndTime,row.CrossesMidnight,row.BreakMinutes,row.IsActive,audit}))))+'"';
        return new(row.ShiftTemplateId,row.ShiftCode,row.ShiftName,row.StartTime,row.EndTime,row.CrossesMidnight,row.BreakMinutes,row.IsActive,version);
    }
    public async Task<IReadOnlyList<ShiftDefinitionView>> ListAsync(SessionReference ticket,long sid,long branch,long department,long after,CancellationToken ct)
    {
        if(branch<=0||department<=0||after<0)throw new InternalRequestReadException(400,"INVALID_SHIFT_FILTER");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await authority.Gate(ticket,sid,branch,department,ct);
        var rows=await db.ShiftTemplates.AsNoTracking().Where(t=>t.BranchId==branch&&t.ShiftTemplateId>after).OrderBy(t=>t.ShiftTemplateId).Take(100).ToArrayAsync(ct);
        var result=new List<ShiftDefinitionView>();foreach(var row in rows)result.Add(await View(row,ct));
        await authority.Gate(ticket,sid,branch,department,ct);await tx.CommitAsync(ct);return result;
    }
    public async Task<ShiftDefinitionView> SaveAsync(SessionReference ticket,long sid,long branch,long department,long? id,string? tag,ShiftDefinitionInput input,CancellationToken ct)
    {
        var code=input.Code?.Trim().ToUpperInvariant();var name=input.Name?.Trim();
        if(branch<=0||department<=0||id is <=0||string.IsNullOrWhiteSpace(code)||code.Length>30||code.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-')
            ||string.IsNullOrWhiteSpace(name)||name.Length>100||name.Any(char.IsControl))throw new InternalRequestReadException(400,"INVALID_SHIFT_DEFINITION");
        try{_ = ShiftSchedulePlanner.Plan(new DateOnly(2000,1,1),input.Start,input.End,input.CrossesMidnight,TimeZoneInfo.Utc);}
        catch(ArgumentException){throw new InternalRequestReadException(400,"INVALID_SHIFT_TIME");}
        if(id!=null&&string.IsNullOrEmpty(tag))throw new InternalRequestReadException(428,"IF_MATCH_REQUIRED");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await authority.Gate(ticket,sid,branch,department,ct);
        var resource="HotelOps:ShiftDefinitions:"+branch.ToString(CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000; IF @r<0 THROW 51884,'Shift definitions busy.',1;",ct);
        ShiftTemplate row;ShiftDefinitionView? before=null;long[] departments=[];
        if(id is {} value)
        {
            row=await db.ShiftTemplates.FromSqlInterpolated($"SELECT * FROM dbo.shift_templates WITH(UPDLOCK,HOLDLOCK) WHERE shift_template_id={value} AND branch_id={branch}").SingleOrDefaultAsync(ct)
                ??throw new InternalRequestReadException(404,"SHIFT_DEFINITION_UNAVAILABLE");
            before=await View(row,ct);if(before.Version!=tag)throw new InternalRequestReadException(412,"SHIFT_DEFINITION_CHANGED");
            // A branch-wide definition can serve several departments. Confirm every affected scope.
            departments=await db.WorkShifts.AsNoTracking().Where(s=>s.ShiftTemplateId==value).Select(s=>s.DepartmentId).Distinct().OrderBy(d=>d).ToArrayAsync(ct);
            foreach(var dependent in departments)await authority.Gate(ticket,sid,branch,dependent,ct);
        }
        else{row=new ShiftTemplate{BranchId=branch,IsActive=true};db.ShiftTemplates.Add(row);}
        if(await db.ShiftTemplates.AsNoTracking().AnyAsync(t=>t.BranchId==branch&&t.ShiftTemplateId!=row.ShiftTemplateId&&t.ShiftCode.ToUpper()==code,ct))throw new InternalRequestReadException(409,"MSG35");
        row.ShiftCode=code;row.ShiftName=name;row.StartTime=input.Start;row.EndTime=input.End;row.CrossesMidnight=input.CrossesMidnight;
        await db.SaveChangesAsync(ct);
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        db.AuditLogs.Add(new AuditLog{ActorUserId=ticket.UserId,BranchId=branch,EntityType="shift_templates",EntityId=row.ShiftTemplateId.ToString(CultureInfo.InvariantCulture),Action=id==null?"CREATE":"UPDATE",OccurredAt=now,
            OldValues=before==null?null:JsonSerializer.Serialize(before),NewValues=JsonSerializer.Serialize(new{row.ShiftCode,row.ShiftName,row.StartTime,row.EndTime,row.CrossesMidnight,row.BreakMinutes,row.IsActive,existingRosterTimesPreserved=true})});
        await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();await authority.Gate(ticket,sid,branch,department,ct);
        foreach(var dependent in departments)await authority.Gate(ticket,sid,branch,dependent,ct);
        var result=await View(row,ct);await tx.CommitAsync(ct);return result;
    }
}
