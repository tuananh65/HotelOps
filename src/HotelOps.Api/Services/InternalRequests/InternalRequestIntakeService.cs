using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.InternalRequests;

public sealed record InternalRequestIntakeInput(Guid SubmissionId,long BranchId,long TypeId,long DepartmentId,string Subject,string Description,
    long PriorityId,DateTimeOffset DesiredDeadline,long? RoomId=null,long? AreaId=null,IReadOnlyList<EvidenceUpload>? Files=null);
public sealed record InternalRequestIntakeTypeOption(long Id,string Name,bool MaterialNeed);
public sealed record InternalRequestIntakeOptions(InternalRequestFilterValue[] Branches,InternalRequestIntakeTypeOption[] Types,
    InternalRequestFilterValue[] Departments,InternalRequestFilterValue[] Priorities,InternalRequestFilterValue[] Rooms,InternalRequestFilterValue[] Areas);
public sealed record InternalRequestIntakeResult(long RequestId,string Number,string Status,string Version,long EventId,string Message);

public sealed class InternalRequestIntakeService(HotelOpsDbContext db,ISqlSessionService sessions,ICurrentUserAccessor current)
{
    internal static InternalRequestIntakeInput Validate(InternalRequestIntakeInput input,DateTime now)
    {
        var subject=input.Subject?.Trim();var description=input.Description?.Trim();
        if(input.SubmissionId==Guid.Empty||input.BranchId<=0||input.TypeId<=0||input.DepartmentId<=0||input.PriorityId<=0
            ||string.IsNullOrWhiteSpace(subject)||subject.Length>200||subject.Any(char.IsControl)
            ||string.IsNullOrWhiteSpace(description)||description.Length>4000||description.Any(c=>char.IsControl(c)&&c!='\r'&&c!='\n'&&c!='\t')
            ||input.DesiredDeadline.UtcDateTime<=now||input.RoomId is <=0||input.AreaId is <=0||input.RoomId!=null&&input.AreaId!=null
            ||input.Files?.Count>5||input.Files?.Any(f=>f==null)==true)
            throw new ValidationException("Complete required fields, select one location and a future desired deadline.");
        foreach(var file in input.Files??[])InternalRequestEvidence.Validate(file);
        return input with{Subject=subject,Description=description};
    }
    private async Task SessionAsync(SessionReference ticket,long sid,CancellationToken ct)
    {
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var valid=await sessions.ValidateSessionAsync(ticket,ct);
        if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.IsRestricted)throw new InternalRequestReadException(401,"SESSION_INVALID");
    }
    internal static Task<bool> InstalledAsync(HotelOpsDbContext db,CancellationToken ct)=>InternalRequestIntakePolicy.InstalledAsync(db,ct);
    private async Task SchemaAsync(CancellationToken ct)
    {
        if(!await InstalledAsync(db,ct)||!await InternalRequestDecisionStore.InstalledAsync(db,ct)
            ||!await new InternalRequestProcessAuthority(db).InstalledAsync(ct)||!await InternalRequestEvidence.InstalledAsync(db,ct))
            throw new InternalRequestReadException(503,"IR_INTAKE_SCHEMA_REQUIRED");
    }
    public async Task<InternalRequestIntakeOptions> OptionsAsync(SessionReference ticket,long sid,long? branch,CancellationToken ct)
    {
        if(branch is <=0)throw new ValidationException("Invalid branch.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);await SessionAsync(ticket,sid,ct);await SchemaAsync(ct);
        var access=new InternalRequestIntakeAccess(db);var branches=new List<InternalRequestFilterValue>();
        foreach(var item in await db.Branches.AsNoTracking().Where(b=>b.IsActive).OrderBy(b=>b.BranchName).Select(b=>new InternalRequestFilterValue(b.BranchId,b.BranchName)).ToArrayAsync(ct))
            if(await access.AllowsAsync(ticket.UserId,item.Id,ct))branches.Add(item);
        if(branches.Count==0||branch!=null&&!branches.Any(b=>b.Id==branch))throw new InternalRequestReadException(403,"IR_CREATE_REQUIRED");
        var types=await (from type in db.InternalRequestTypes.AsNoTracking() join policy in InternalRequestIntakePolicy.Types(db) on type.RequestTypeId equals policy.TypeId
            where type.IsActive orderby type.TypeName,type.RequestTypeId select new InternalRequestIntakeTypeOption(type.RequestTypeId,type.TypeName,policy.MaterialNeed)).ToArrayAsync(ct);
        var result=new InternalRequestIntakeOptions(branches.ToArray(),types,
            await db.Departments.AsNoTracking().Where(d=>branch!=null&&d.BranchId==branch&&d.IsActive).OrderBy(d=>d.DepartmentName).Select(d=>new InternalRequestFilterValue(d.DepartmentId,d.DepartmentName)).ToArrayAsync(ct),
            await db.ReferenceValues.AsNoTracking().Where(r=>r.GroupCode=="PRIORITY"&&r.IsActive).OrderBy(r=>r.SortOrder).ThenBy(r=>r.ReferenceId).Select(r=>new InternalRequestFilterValue(r.ReferenceId,r.DisplayName)).ToArrayAsync(ct),
            await db.Rooms.AsNoTracking().Where(r=>branch!=null&&r.BranchId==branch&&r.IsActive).OrderBy(r=>r.RoomNumber).Select(r=>new InternalRequestFilterValue(r.RoomId,r.RoomNumber)).ToArrayAsync(ct),
            await db.Areas.AsNoTracking().Where(a=>branch!=null&&a.BranchId==branch&&a.IsActive).OrderBy(a=>a.AreaName).Select(a=>new InternalRequestFilterValue(a.AreaId,a.AreaName)).ToArrayAsync(ct));
        await SessionAsync(ticket,sid,ct);
        foreach(var b in branches)if(!await access.AllowsAsync(ticket.UserId,b.Id,ct))throw new InternalRequestReadException(403,"IR_SCOPE_CHANGED");
        await tx.CommitAsync(ct);return result;
    }
    private Task<long[]> Candidates(long branch,long department,CancellationToken ct)=>db.Database.SqlQuery<long>($"SELECT user_id AS Value FROM dbo.department_management_access WHERE branch_id={branch} AND department_id={department} AND revoked_at IS NULL AND granted_at<=SYSUTCDATETIME()").OrderBy(v=>v).ToArrayAsync(ct);

    public async Task<InternalRequestIntakeResult> CreateAsync(SessionReference ticket,long sid,InternalRequestIntakeInput input,string root,CancellationToken ct)
    {
        input=Validate(input,DateTime.UtcNow);
        if(db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any())throw new InvalidOperationException("Clean intake context required.");
        await SchemaAsync(ct);var candidates=await Candidates(input.BranchId,input.DepartmentId,ct);
        using var files=new EvidenceFileLifetime();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync(candidates.Append(ticket.UserId),ct);await SessionAsync(ticket,sid,ct);
            var access=new InternalRequestIntakeAccess(db);
            if(!await access.AllowsAsync(ticket.UserId,input.BranchId,ct))throw new InternalRequestReadException(403,"IR_CREATE_REQUIRED");
            var number="IR-"+input.SubmissionId.ToString("N");
            // Durable uniqueness prevents duplicate creation after uncertain outcomes. Never replay a mutation automatically.
            if(await db.InternalRequests.FromSqlInterpolated($"SELECT * FROM dbo.internal_requests WITH(UPDLOCK,HOLDLOCK) WHERE request_no={number}").AsNoTracking().AnyAsync(ct))
                throw new InternalRequestReadException(409,"IR_SUBMISSION_ALREADY_USED");
            var policy=await (from type in db.InternalRequestTypes.AsNoTracking() join rule in InternalRequestIntakePolicy.Types(db) on type.RequestTypeId equals rule.TypeId
                where type.RequestTypeId==input.TypeId&&type.IsActive select rule).SingleOrDefaultAsync(ct);
            if(policy==null)throw new InternalRequestReadException(400,"IR_TYPE_UNCLASSIFIED");
            if(policy.MaterialNeed)throw new InternalRequestReadException(400,"MATERIAL_REQUEST_REQUIRED");
            if(!await db.Departments.AsNoTracking().AnyAsync(d=>d.DepartmentId==input.DepartmentId&&d.BranchId==input.BranchId&&d.IsActive,ct)
                ||!await db.ReferenceValues.AsNoTracking().AnyAsync(r=>r.ReferenceId==input.PriorityId&&r.GroupCode=="PRIORITY"&&r.IsActive,ct)
                ||input.RoomId!=null&&!await db.Rooms.AsNoTracking().AnyAsync(r=>r.RoomId==input.RoomId&&r.BranchId==input.BranchId&&r.IsActive,ct)
                ||input.AreaId!=null&&!await db.Areas.AsNoTracking().AnyAsync(a=>a.AreaId==input.AreaId&&a.BranchId==input.BranchId&&a.IsActive,ct))
                throw new ValidationException("Select current references in the request branch.");
            var currentCandidates=await Candidates(input.BranchId,input.DepartmentId,ct);
            if(!candidates.SequenceEqual(currentCandidates))throw new InternalRequestReadException(409,"IR_RECIPIENT_SCOPE_CHANGED");
            var recipients=new List<long>();var managers=new InternalRequestProcessAuthority(db);
            foreach(var recipient in candidates)if(await managers.AllowsDepartmentAsync(recipient,input.BranchId,input.DepartmentId,ct))recipients.Add(recipient);
            var now=DateTime.SpecifyKind(await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct),DateTimeKind.Utc);
            Validate(input,now);
            var home=await db.Users.AsNoTracking().Where(u=>u.UserId==ticket.UserId&&u.Department!=null&&u.Department.BranchId==input.BranchId).Select(u=>u.DepartmentId).SingleOrDefaultAsync(ct);
            var request=new InternalRequest{RequestNo=number,BranchId=input.BranchId,RequestTypeId=input.TypeId,RequesterId=ticket.UserId,RequesterDepartmentId=home,
                ReceivingDepartmentId=input.DepartmentId,Subject=input.Subject,Description=input.Description,PriorityId=input.PriorityId,
                RequiredAt=input.DesiredDeadline.UtcDateTime,RoomId=input.RoomId,AreaId=input.AreaId,Status="PENDING_ACCEPTANCE",CreatedAt=now,SubmittedAt=now};
            db.InternalRequests.Add(request);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            var entry=new InternalRequestEvent{InternalRequestId=request.InternalRequestId,EventType="CREATE",NewStatus=request.Status,ActorId=ticket.UserId,OccurredAt=now};
            db.InternalRequestEvents.Add(entry);await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            foreach(var file in input.Files??[])await InternalRequestEvidence.StoreAsync(db,entry.EventId,ticket.UserId,now,file,root,files.Files,ct,"INTERNAL_REQUEST_INTAKE");
            db.AuditLogs.Add(new(){ActorUserId=ticket.UserId,BranchId=input.BranchId,EntityType="internal_requests",EntityId=request.InternalRequestId.ToString(System.Globalization.CultureInfo.InvariantCulture),Action="CREATE",OccurredAt=now,
                NewValues=JsonSerializer.Serialize(new{operation="CREATE",request.RequestNo,request.Status,request.RequestTypeId,request.ReceivingDepartmentId,request.RequiredAt,request.PriorityId}),ChangedFields="[\"request\"]"});
            await db.SaveChangesAsync(ct);db.ChangeTracker.Clear();
            foreach(var recipient in recipients)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.internal_request_intake_outbox(event_id,recipient_id,branch_id,department_id,created_at) VALUES({entry.EventId},{recipient},{input.BranchId},{input.DepartmentId},{now})",ct);
            await SessionAsync(ticket,sid,ct);if(!await access.AllowsAsync(ticket.UserId,input.BranchId,ct))throw new InternalRequestReadException(403,"IR_SCOPE_CHANGED");
            var version=await InternalRequestDecisionStore.RevisionAsync(db,request.InternalRequestId,ct);
            await files.BeforeCommitAsync(ct);await tx.CommitAsync(ct);
            return new(request.InternalRequestId,number,request.Status,Convert.ToBase64String(version),entry.EventId,"MSG09");
        }
        finally{db.ChangeTracker.Clear();}
    }
}
