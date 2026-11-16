using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.InternalRequests;
using HotelOps.Api.Services.Maintenance;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.PreventiveMaintenance;

public sealed record PreventiveCheckItem(string Key, string Description, bool Required);
public sealed record PreventiveTargetOption(long Id,string Kind,string Label);
public sealed record PreventiveScheduleInput(string Title, string Instruction, string Frequency, int Interval,
    DateTime FirstDueAt, long? AssetId, long? RoomId, long? AreaId, long? DefaultAssigneeId, PreventiveCheckItem[] CheckItems);
public sealed class PreventiveScheduleView
{
    public long ScheduleId { get; set; }
    public string ScheduleCode { get; set; } = "";
    public string? Title { get; set; }
    public string? Instruction { get; set; }
    public string Frequency { get; set; } = "";
    public int Interval { get; set; }
    public DateTime NextDueAt { get; set; }
    public long? AssetId { get; set; }
    public long? RoomId { get; set; }
    public long? AreaId { get; set; }
    public long? DefaultAssigneeId { get; set; }
    public bool IsActive { get; set; }
    public string Version { get; set; } = "";
    public PreventiveCheckItem[]? CheckItems { get; set; }
}

public sealed class PreventiveScheduleService(HotelOpsDbContext db, ISqlSessionService sessions, ICurrentUserAccessor current)
{
    private async Task AuthorizeAsync(SessionReference ticket, long sid, long branch, CancellationToken ct)
    {
        if (!current.TryGetCurrentUser(out var actor) || actor.UserId != ticket.UserId || actor.SessionId != sid
            || actor.IsRestricted || ticket.IsRestricted) throw new InternalRequestReadException(401,"SESSION_INVALID");
        var session = await sessions.ValidateSessionAsync(ticket, ct);
        if (!session.IsValid || session.Session?.SessionId != sid || session.Session.IsRestricted)
            throw new InternalRequestReadException(401,"SESSION_INVALID");
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var paths = await (from role in db.UserRoles.AsNoTracking()
            join grant in db.RolePermissions.AsNoTracking() on role.RoleId equals grant.RoleId
            where role.UserId == ticket.UserId && role.BranchId == branch && role.Role.RoleCode == "MAINTENANCE_LEAD"
                && grant.Permission.PermissionCode == "PM_MANAGE" && role.User.AccountStatus == "ACTIVE"
                && role.User.EmploymentStatus == "ACTIVE" && role.User.DeletedAt == null
            select new PermissionGrantPath(grant.Permission.PermissionCode, grant.DataScope, role.BranchId,
                role.IsActive, role.Role.IsActive, role.AssignedAt, role.ExpiresAt, role.Branch != null && role.Branch.IsActive,
                db.UserBranchAccesses.Any(b => b.UserId == ticket.UserId && b.BranchId == branch && b.RevokedAt == null && b.GrantedAt <= now)))
            .ToArrayAsync(ct);
        if (!paths.Any(p => PermissionGrantRules.IsEligible(p,now) && PermissionGrantRules.EffectiveScope(p) is "BRANCH" or "GLOBAL"))
            throw new InternalRequestReadException(403,"SCOPED_PM_MANAGE_REQUIRED");
        if (!await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0
            AND name='HotelOps.PreventiveSchedule.V1' AND CONVERT(nvarchar(10),value)='1')
            AND (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.preventive_maintenance_schedules')
            AND name IN('title','check_items_json','default_assignee_id','row_version'))=4
            AND EXISTS(SELECT 1 FROM sys.foreign_keys WHERE parent_object_id=OBJECT_ID('dbo.preventive_maintenance_schedules')
              AND name='FK_preventive_default_assignee' AND is_disabled=0 AND is_not_trusted=0 AND delete_referential_action=0)
            AND EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.preventive_maintenance_schedules')
              AND name='CK_preventive_definition' AND is_disabled=0 AND is_not_trusted=0)
            AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.preventive_maintenance_schedules')
              AND name IN('UX_pm_asset_title','UX_pm_room_title','UX_pm_area_title') AND is_unique=1 AND is_disabled=0 AND has_filter=1)=3
            THEN 1 ELSE 0 END AS Value
            """).AnyAsync(v => v == 1,ct)) throw new InternalRequestReadException(503,"PREVENTIVE_SCHEDULE_STORAGE_UNAVAILABLE");
    }

    private static PreventiveScheduleInput Normalize(PreventiveScheduleInput input)
    {
        var title = input.Title?.Trim(); var instruction = input.Instruction?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 200 || string.IsNullOrEmpty(instruction) || instruction.Length > 16000
            || input.Interval <= 0 || input.Frequency is not ("DAYS" or "WEEKS" or "MONTHS")
            || input.FirstDueAt.Kind != DateTimeKind.Utc
            || new[]{input.AssetId,input.RoomId,input.AreaId}.Count(x => x != null) != 1
            || new[]{input.AssetId,input.RoomId,input.AreaId,input.DefaultAssigneeId}.Any(x => x is <= 0))
            throw new ValidationException("Provide a title, description, one target, positive interval and a UTC due date.");
        if (input.CheckItems == null || input.CheckItems.Length > 100)
            throw new ValidationException("Provide a checklist with at most 100 items.");
        var items = input.CheckItems.Select(x => x == null ? throw new ValidationException("Invalid check item.")
            : new PreventiveCheckItem(x.Key?.Trim() ?? "",x.Description?.Trim() ?? "",x.Required)).ToArray();
        if (items.Any(x => x.Key.Length is < 1 or > 64 || x.Key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-'))
            || x.Description.Length is < 1 or > 1000) || items.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new ValidationException("Check items require distinct stable keys and descriptions.");
        _ = NextDue(input.FirstDueAt,input.Frequency,input.Interval);
        return input with { Title=title,Instruction=instruction,CheckItems=items };
    }

    internal static DateTime NextDue(DateTime scheduledDue, string frequency, int interval)
    {
        if (interval <= 0) throw new ValidationException("Interval must be positive.");
        try { return frequency switch { "DAYS" => scheduledDue.AddDays(interval), "WEEKS" => scheduledDue.AddDays(checked((long)interval*7)),
            "MONTHS" => scheduledDue.AddMonths(interval), _ => throw new ValidationException("Invalid recurrence unit.") }; }
        catch (ArgumentOutOfRangeException) { throw new ValidationException("Next scheduled date is outside the supported range."); }
    }

    private async Task TargetAsync(long branch,long? asset,long? room,long? area,CancellationToken ct,bool requireActive=true)
    {
        if (new[]{asset,room,area}.Count(x=>x!=null)!=1) throw new InternalRequestReadException(409,"SCHEDULE_TARGET_AMBIGUOUS");
        var valid = asset != null ? await db.Assets.AsNoTracking().AnyAsync(a=>a.AssetId==asset && a.BranchId==branch && (!requireActive || a.LifecycleStatus!="RETIRED"),ct)
            : room != null ? await db.Rooms.AsNoTracking().AnyAsync(r=>r.RoomId==room && r.BranchId==branch && (!requireActive || r.IsActive),ct)
            : await db.Areas.AsNoTracking().AnyAsync(a=>a.AreaId==area && a.BranchId==branch && (!requireActive || a.IsActive),ct);
        if (!valid) throw new InternalRequestReadException(404,"SCHEDULE_TARGET_UNAVAILABLE");
    }

    private sealed class Metadata
    {
        public string? Title { get; set; }
        public string? Checklist { get; set; }
        public long? Assignee { get; set; }
        public string Version { get; set; } = "";
    }
    private Task<Metadata> MetadataAsync(long id,CancellationToken ct) => db.Database.SqlQuery<Metadata>($"""
        SELECT title AS Title,check_items_json AS Checklist,default_assignee_id AS Assignee,
        CONVERT(varchar(16),row_version,2) AS Version FROM dbo.preventive_maintenance_schedules WHERE schedule_id={id}
        """).SingleAsync(ct);
    private async Task<PreventiveMaintenanceSchedule> LockAsync(long id,CancellationToken ct) =>
        await db.PreventiveMaintenanceSchedules.FromSqlInterpolated($"SELECT * FROM dbo.preventive_maintenance_schedules WITH(UPDLOCK,HOLDLOCK) WHERE schedule_id={id}")
            .AsNoTracking().SingleOrDefaultAsync(ct) ?? throw new InternalRequestReadException(404,"SCHEDULE_UNAVAILABLE");
    private async Task<PreventiveScheduleView> ViewAsync(PreventiveMaintenanceSchedule row,CancellationToken ct)
    {
        var meta=await MetadataAsync(row.ScheduleId,ct);
        PreventiveCheckItem[]? checklist=null;
        if(meta.Checklist!=null)
        {
            try { checklist=JsonSerializer.Deserialize<PreventiveCheckItem[]>(meta.Checklist); }
            catch(JsonException) { throw new InternalRequestReadException(409,"SCHEDULE_CHECKLIST_INVALID"); }
        }
        return new(){ScheduleId=row.ScheduleId,ScheduleCode=row.ScheduleCode,Title=meta.Title,Instruction=row.Instruction,
            Frequency=row.Frequency,Interval=row.FrequencyInterval,NextDueAt=DateTime.SpecifyKind(row.NextDueAt,DateTimeKind.Utc),
            AssetId=row.AssetId,RoomId=row.RoomId,AreaId=row.AreaId,DefaultAssigneeId=meta.Assignee,IsActive=row.IsActive,
            Version='"'+meta.Version+'"',CheckItems=checklist};
    }
    public async Task<PreventiveTechnicianPage> TechniciansAsync(SessionReference ticket,long sid,long branch,long afterId,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        var result=await PreventiveTechnicians.ReadAsync(db,branch,afterId,ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        await tx.CommitAsync(ct);return result;
    }
    public async Task<IReadOnlyList<PreventiveTargetOption>> TargetsAsync(SessionReference ticket,long sid,long branch,string kind,long afterId,CancellationToken ct)
    {
        if(afterId<0 || kind is not ("ASSET" or "ROOM" or "AREA"))throw new ValidationException("Invalid target page.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        PreventiveTargetOption[] result;
        if(kind=="ASSET")result=await db.Assets.AsNoTracking()
            .Where(a=>a.BranchId==branch && a.AssetId>afterId && a.LifecycleStatus!="RETIRED")
            .OrderBy(a=>a.AssetId).Take(100).Select(a=>new PreventiveTargetOption(a.AssetId,"ASSET",a.AssetCode+" - "+a.AssetName)).ToArrayAsync(ct);
        else if(kind=="ROOM")result=await db.Rooms.AsNoTracking()
            .Where(r=>r.BranchId==branch && r.RoomId>afterId && r.IsActive)
            .OrderBy(r=>r.RoomId).Take(100).Select(r=>new PreventiveTargetOption(r.RoomId,"ROOM",r.RoomNumber)).ToArrayAsync(ct);
        else result=await db.Areas.AsNoTracking()
            .Where(a=>a.BranchId==branch && a.AreaId>afterId && a.IsActive)
            .OrderBy(a=>a.AreaId).Take(100).Select(a=>new PreventiveTargetOption(a.AreaId,"AREA",a.AreaCode+" - "+a.AreaName)).ToArrayAsync(ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        await tx.CommitAsync(ct);return result;
    }

    public async Task<IReadOnlyList<PreventiveScheduleView>> ListAsync(SessionReference ticket,long sid,long branch,long afterId,int limit,CancellationToken ct)
    {
        if(afterId<0 || limit is <1 or >100)throw new ValidationException("Invalid schedule page.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        var rows=await db.PreventiveMaintenanceSchedules.AsNoTracking()
            .Where(s=>s.ScheduleId>afterId &&
                ((s.AssetId!=null && s.RoomId==null && s.AreaId==null && db.Assets.Any(a=>a.AssetId==s.AssetId && a.BranchId==branch)) ||
                 (s.RoomId!=null && s.AssetId==null && s.AreaId==null && db.Rooms.Any(r=>r.RoomId==s.RoomId && r.BranchId==branch)) ||
                 (s.AreaId!=null && s.AssetId==null && s.RoomId==null && db.Areas.Any(a=>a.AreaId==s.AreaId && a.BranchId==branch))))
            .OrderBy(s=>s.ScheduleId).Take(limit).ToArrayAsync(ct);
        var result=new List<PreventiveScheduleView>(rows.Length);
        foreach(var row in rows)result.Add(await ViewAsync(row,ct));
        await AuthorizeAsync(ticket,sid,branch,ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<PreventiveScheduleView> ReadAsync(SessionReference ticket,long sid,long branch,long id,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        var row=await LockAsync(id,ct);
        await TargetAsync(branch,row.AssetId,row.RoomId,row.AreaId,ct,requireActive:false);
        var result=await ViewAsync(row,ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        await tx.CommitAsync(ct);return result;
    }

    public async Task<PreventiveScheduleView> SaveAsync(SessionReference ticket,long sid,long branch,long? id,string? tag,
        PreventiveScheduleInput input,CancellationToken ct)
    {
        input=Normalize(input);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync(input.DefaultAssigneeId is {} user ? [ticket.UserId,user] : [ticket.UserId],ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        await TargetAsync(branch,input.AssetId,input.RoomId,input.AreaId,ct);
        if(input.DefaultAssigneeId is {} assignee && !await PreventiveTechnicians.EligibleAsync(db,assignee,branch,ct))
            throw new ValidationException("Default assignee requires current maintenance read and execution authority in this branch.");
        var resource=$"HotelOps:PreventiveTarget:{branch}:{input.AssetId}:{input.RoomId}:{input.AreaId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource={resource},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=15000;
            IF @r<0 THROW 51846,'Preventive target lock unavailable.',1;
            """,ct);
        PreventiveScheduleView? before=null;
        PreventiveMaintenanceSchedule row;
        if(id is {} key)
        {
            row=await LockAsync(key,ct);
            await TargetAsync(branch,row.AssetId,row.RoomId,row.AreaId,ct,requireActive:false);
            before=await ViewAsync(row,ct);
            if(tag!=before.Version)throw new InternalRequestReadException(412,"SCHEDULE_CHANGED");
            if(!row.IsActive || before.Title==null || before.CheckItems==null)throw new InternalRequestReadException(409,"ACTIVE_SCHEDULE_DEFINITION_REQUIRED");
            if(input.FirstDueAt!=before.NextDueAt) throw new ValidationException("Editing a definition cannot reset the current scheduled due date.");
            if(row.AssetId!=input.AssetId || row.RoomId!=input.RoomId || row.AreaId!=input.AreaId || before.Title!=input.Title)
                throw new ValidationException("Schedule target and title cannot change during an edit.");
        }
        else
        {
            row=new(){ScheduleCode="PM-"+Guid.NewGuid().ToString("N"),AssetId=input.AssetId,RoomId=input.RoomId,AreaId=input.AreaId,
                Frequency=input.Frequency,FrequencyInterval=input.Interval,Instruction=input.Instruction,NextDueAt=input.FirstDueAt,
                IsActive=true,CreatedBy=ticket.UserId};
        }
        if(await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS Value FROM dbo.preventive_maintenance_schedules WITH(UPDLOCK,HOLDLOCK)
            WHERE is_active=1 AND title={input.Title} AND schedule_id<>{id ?? 0}
            AND (asset_id={input.AssetId} OR room_id={input.RoomId} OR area_id={input.AreaId})
            """).SingleAsync(ct)>0)throw new InternalRequestReadException(409,"DUPLICATE_ACTIVE_SCHEDULE");
        if(id==null){db.PreventiveMaintenanceSchedules.Add(row);await db.SaveChangesAsync(ct);}
        var checklist=JsonSerializer.Serialize(input.CheckItems);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.preventive_maintenance_schedules SET title={input.Title},instruction={input.Instruction},
            frequency={input.Frequency},frequency_interval={input.Interval},check_items_json={checklist},default_assignee_id={input.DefaultAssigneeId}
            WHERE schedule_id={row.ScheduleId}
            """,ct);
        row.Instruction=input.Instruction;row.Frequency=input.Frequency;row.FrequencyInterval=input.Interval;
        var after=await ViewAsync(row,ct);
        await AuditAsync(ticket.UserId,branch,row.ScheduleId,id==null?"CREATE":"UPDATE",before,after,ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        await tx.CommitAsync(ct);return after;
    }

    public async Task<PreventiveScheduleView> DeactivateAsync(SessionReference ticket,long sid,long branch,long id,string tag,string reason,CancellationToken ct)
    {
        reason=reason?.Trim() ?? "";
        if(reason.Length is <10 or >4000)throw new ValidationException("Deactivation reason must contain 10 to 4000 characters.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await new UserEligibilityLock(db).LockAsync([ticket.UserId],ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        var row=await LockAsync(id,ct);await TargetAsync(branch,row.AssetId,row.RoomId,row.AreaId,ct,requireActive:false);
        var before=await ViewAsync(row,ct);
        if(before.Version!=tag)throw new InternalRequestReadException(412,"SCHEDULE_CHANGED");
        if(!row.IsActive)throw new InternalRequestReadException(409,"SCHEDULE_INACTIVE");
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.preventive_maintenance_schedules SET is_active=0 WHERE schedule_id={id}",ct);
        row.IsActive=false;var after=await ViewAsync(row,ct);
        await AuditAsync(ticket.UserId,branch,id,"UPDATE",before,new{schedule=after,reason},ct);
        await AuthorizeAsync(ticket,sid,branch,ct);
        await tx.CommitAsync(ct);return after;
    }
    private async Task AuditAsync(long actor,long branch,long id,string action,object? before,object after,CancellationToken ct)
    {
        var now=await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        db.AuditLogs.Add(new(){EntityType="preventive_maintenance_schedules",EntityId=id.ToString(CultureInfo.InvariantCulture),
            Action=action,ActorUserId=actor,BranchId=branch,OccurredAt=now,OldValues=before==null?null:JsonSerializer.Serialize(before),NewValues=JsonSerializer.Serialize(after)});
        await db.SaveChangesAsync(ct);
    }
}
