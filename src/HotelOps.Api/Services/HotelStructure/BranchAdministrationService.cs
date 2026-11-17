using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

public sealed record BranchAdministrationResult(int Status, object? Value = null, string? Code = null, string? ETag = null);

/// <summary>UC012 SQL-rowversion boundary; deactivation requires installed writer coordination and current dependency proof.</summary>
public sealed class BranchAdministrationService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions)
{
    public Task<BranchAdministrationResult> OptionsAsync(SessionReference ticket, long sid, CancellationToken ct)
        => Execute(ticket, sid, async _ => new(200, new {
            Timezone = await SystemTimezone(ct),
            PageSize = await ManagementPageSize(ct),
            Branches = await db.Branches.AsNoTracking().OrderBy(b=>b.BranchCode)
                .Select(b=>new { Id=b.BranchId, Code=b.BranchCode, Name=b.BranchName, b.IsActive }).ToArrayAsync(ct)
        }), ct);

    private async Task<int> ManagementPageSize(CancellationToken ct)
    {
        var values=await db.SystemSettings.AsNoTracking().Where(s=>s.BranchId==null&&s.SettingKey=="SYS-PAR-11")
            .Select(s=>s.SettingValue).ToArrayAsync(ct);
        if(values.Length==0)return 20;
        if(values.Length!=1||!int.TryParse(values[0],out var size)||size is <1 or >100)
            throw new ValidationException("Invalid SYS-PAR-11 configuration.");
        return size;
    }

    private async Task<string> SystemTimezone(CancellationToken ct)
    {
        var settings = await db.SystemSettings.AsNoTracking().Where(s=>s.BranchId==null && s.SettingKey=="SYS-PAR-10")
            .Select(s=>s.SettingValue).ToArrayAsync(ct);
        if(settings.Length>1)throw new ValidationException("Ambiguous SYS-PAR-10 configuration.");
        var zone=settings.Length==0 ? "Asia/Ho_Chi_Minh" : settings[0];
        if(string.IsNullOrWhiteSpace(zone)||zone.Length>100)throw new ValidationException("Invalid SYS-PAR-10 configuration.");
        try { _=TimeZoneInfo.FindSystemTimeZoneById(zone); }
        catch(Exception e) when(e is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ValidationException("Invalid SYS-PAR-10 configuration."); }
        return zone;
    }
    public Task<BranchAdministrationResult> CreateAsync(SessionReference ticket, long sid, CreateBranchRequest input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        var code = Text(input.Code, 30, true)!.ToUpperInvariant();
        if (!char.IsAsciiLetterOrDigit(code[0]) || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-'))
            throw new ValidationException("Invalid branch code.");
        var fields = Fields(input.Name, input.Address, input.Phone, input.Email);
        return Execute(ticket, sid, async actor =>
        {
            // Feature-only create serialization; never changes the shared RoomTypes lock protocol.
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            command.CommandText = "DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'HotelOps:Branches:Create:v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @r;";
            var acquired = Convert.ToInt32(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
            if (acquired < 0) return new(503, Code: "BRANCH_BUSY");
            if (await db.Branches.AsNoTracking().AnyAsync(x => x.BranchCode.Trim().ToUpper() == code, ct))
                return new(409, Code: "DUPLICATE_BRANCH_CODE");
            var now = await Now(ct);
            var row = new Branch { BranchCode=code, BranchName=fields.Name, Address=fields.Address,
                Phone=fields.Phone, Email=fields.Email, Timezone=await SystemTimezone(ct), IsActive=true, CreatedAt=now };
            db.Branches.Add(row);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException e) when (Number(e) is 2601 or 2627) { return new(409, Code:"DUPLICATE_BRANCH_CODE"); }
            db.Entry(row).State = EntityState.Detached;
            var saved = await Row(row.BranchId, ct) ?? throw new InvalidOperationException("Created branch disappeared.");
            await Audit(actor, saved.Id, "CREATE", null, View(saved), ["code","name","address","phone","email","timezone","isActive"], now, ct);
            return Success(saved, 201);
        }, ct);
    }

    public Task<BranchAdministrationResult> ReadAsync(SessionReference ticket, long sid, long id, CancellationToken ct)
    {
        Id(id);
        return Execute(ticket, sid, async _ => await Row(id, ct) is { } row ? Success(row) : new(404, Code:"BRANCH_NOT_FOUND"), ct);
    }

    public Task<BranchAdministrationResult> UpdateAsync(SessionReference ticket, long sid, long id, byte[] expected,
        UpdateBranchRequest input, CancellationToken ct)
    {
        Id(id); Version(expected); ArgumentNullException.ThrowIfNull(input);
        var fields=Fields(input.Name,input.Address,input.Phone,input.Email);
        var reason=Text(input.Reason,500,true)!;
        if(reason.Length<10) throw new ValidationException("A reason of at least ten characters is required.");
        return Execute(ticket, sid, async actor =>
        {
            var before=await Row(id,ct);
            if(before is null)return new(404,Code:"BRANCH_NOT_FOUND");
            if(!before.Version.AsSpan().SequenceEqual(expected))return new(412,Code:"BRANCH_STALE");
            if(before.Name==fields.Name && before.Address==fields.Address && before.Phone==fields.Phone && before.Email==fields.Email)
                return Success(before); // No-op leaves rowversion, UpdatedAt and audit untouched.
            var now=await Now(ct);
            var changed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.branches SET branch_name={fields.Name},address={fields.Address},phone={fields.Phone},email={fields.Email},updated_at={now} WHERE branch_id={id} AND row_version={expected}",ct);
            if(changed!=1)return new(412,Code:"BRANCH_STALE");
            var after=await Row(id,ct) ?? throw new InvalidOperationException("Updated branch disappeared.");
            var changedFields=new List<string>();
            if(before.Name!=after.Name)changedFields.Add("name");
            if(before.Address!=after.Address)changedFields.Add("address");
            if(before.Phone!=after.Phone)changedFields.Add("phone");
            if(before.Email!=after.Email)changedFields.Add("email");
            await Audit(actor,id,"UPDATE",View(before),new{reason,entity=View(after)},changedFields.ToArray(),now,ct);
            return Success(after);
        },ct);
    }

    public Task<BranchAdministrationResult> DependenciesAsync(SessionReference ticket,long sid,long id,CancellationToken ct)
    {
        Id(id);
        return Execute(ticket,sid,async _ => await Row(id,ct) is not {} row ? new(404,Code:"BRANCH_NOT_FOUND")
            : new(200,await new BranchDeactivationDependencyReader(db).ReadAsync(id,ct),ETag:'"'+Convert.ToBase64String(row.Version)+'"'),ct);
    }

    public Task<BranchAdministrationResult> DeactivateAsync(SessionReference ticket,long sid,long id,byte[] expected,
        DeactivateStructureRequest input,CancellationToken ct)
    {
        Id(id);Version(expected);ArgumentNullException.ThrowIfNull(input);
        var reason=Text(input.Reason,1000,true)!;
        if(!input.Confirmation || reason.Length<10)throw new ValidationException("Confirmation and reason are required.");
        return Execute(ticket,sid,async actor =>
        {
            var row=await Row(id,ct);
            if(row is null)return new(404,Code:"BRANCH_NOT_FOUND");
            if(!row.Version.AsSpan().SequenceEqual(expected))return new(412,Code:"BRANCH_STALE");
            if(!row.IsActive)return Success(row); // Already inactive: no mutation/audit.
            var facts=await new BranchDeactivationDependencyReader(db).ReadAsync(id,ct);
            if(!facts.CanDeactivate)return new(facts.HasConfirmedBlockers?409:503,facts,facts.Coordination);
            var now=await Now(ct);
            if(await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.branches SET is_active=0,updated_at={now} WHERE branch_id={id} AND is_active=1 AND row_version={expected}",ct)!=1)
                return new(412,Code:"BRANCH_STALE");
            var after=await Row(id,ct)??throw new InvalidOperationException("Branch disappeared during deactivation.");
            await Audit(actor,id,"UPDATE",View(row),new{reason,entity=View(after)},["isActive"],now,ct);
            return Success(after);
        },ct);
    }

    private async Task<BranchAdministrationResult> Execute(SessionReference ticket,long sid,
        Func<long,Task<BranchAdministrationResult>> action,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);ct.ThrowIfCancellationRequested();
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sid||actor.IsRestricted||ticket.IsRestricted)
            return new(401,Code:"SESSION_INVALID");
        if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()||System.Transactions.Transaction.Current!=null)
            throw new InvalidOperationException("A clean scoped SQL context is required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            var locked=await new UserEligibilityLock(db).LockAsync([actor.UserId],ct);
            if(!locked.TryGetValue(actor.UserId,out var user)||user is null||user.DeletedAt!=null||user.AccountStatus!="ACTIVE"||user.EmploymentStatus!="ACTIVE")
                return new(401,Code:"SESSION_INVALID");
            if(await Gate() is {} denied)return denied;
            if(!await BranchAdministrationSchema.ReadyAsync(db,ct))return new(503,Code:"BRANCH_ROWVERSION_SCHEMA_REQUIRED");
            var result=await action(actor.UserId);
            if(result.Status is >=200 and <300)
            {
                if(await Gate() is {} revoked)return revoked;
                await tx.CommitAsync(ct);
            }
            return result;
        }
        catch(Exception e) when(!ct.IsCancellationRequested && (e is SqlException or DbUpdateException))
        { return new(503,Code:Number(e)==1205?"BRANCH_DEADLOCK_ROLLED_BACK":"BRANCH_STORE_UNAVAILABLE"); }
        finally {db.ChangeTracker.Clear();}

        async Task<BranchAdministrationResult?> Gate()
        {
            var valid=await sessions.ValidateSessionAsync(ticket,ct);
            if(!valid.IsValid||valid.Session?.SessionId!=sid||valid.Session.UserId!=actor.UserId||valid.Session.IsRestricted)
                return new(401,Code:"SESSION_INVALID");
            // Historical UC012 binding: effective GLOBAL MASTER_DATA_MANAGE, never a role-name bypass.
            var now=await Now(ct);
            if(!await db.UserRoles.AsNoTracking().AnyAsync(r=>r.UserId==actor.UserId && r.IsActive && r.Role.IsActive
                && r.Role.RoleCode=="SYS_ADMIN" && r.BranchId==null && r.AssignedAt<=now && (r.ExpiresAt==null||r.ExpiresAt>now),ct))
                return new(403,Code:"BRANCH_ADMINISTRATOR_REQUIRED");
            var access=await permissions.EvaluateGlobalAsync("MASTER_DATA_MANAGE",ct);
            return access.IsGranted?null:new(access.Decision==PermissionDecision.Unavailable?503:403,Code:"BRANCH_FORBIDDEN");
        }
    }

    private Task<BranchRow?> Row(long id,CancellationToken ct)=>BranchRows().SingleOrDefaultAsync(x=>x.Id==id,ct);
    private IQueryable<BranchRow> BranchRows()=>db.Database.SqlQueryRaw<BranchRow>("""
        SELECT branch_id AS Id,branch_code AS Code,branch_name AS Name,address AS Address,phone AS Phone,
        email AS Email,timezone AS Timezone,is_active AS IsActive,CONVERT(varbinary(8),row_version) AS Version
        FROM dbo.branches WITH(UPDLOCK,HOLDLOCK)
        """);
    private Task<DateTime> Now(CancellationToken ct)=>db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS [Value]").SingleAsync(ct);
    private async Task Audit(long actor,long branch,string action,object? before,object after,string[] fields,DateTime now,CancellationToken ct)
    {
        var audit=new AuditLog{ActorUserId=actor,BranchId=branch,EntityType="branches",EntityId=branch.ToString(CultureInfo.InvariantCulture),Action=action,
            OccurredAt=now,OldValues=JsonSerializer.Serialize(before is null?new{entity=(object?)null}:before),NewValues=JsonSerializer.Serialize(after),ChangedFields=JsonSerializer.Serialize(fields)};
        db.AuditLogs.Add(audit);
        try { await db.SaveChangesAsync(ct); }
        finally { db.Entry(audit).State=EntityState.Detached; } // Final SQL-session recheck requires an untracked context.
    }
    private static EditableBranchDto View(BranchRow r)=>new(r.Id,r.Code,r.Name,r.Address,r.Phone,r.Email,r.Timezone,r.IsActive);
    private static BranchAdministrationResult Success(BranchRow r,int status=200)=>new(status,View(r),ETag:'"'+Convert.ToBase64String(r.Version)+'"');
    private static void Id(long id){if(id<=0)throw new ValidationException("Positive BranchId required.");}
    private static void Version(byte[] value){if(value is not {Length:8})throw new ValidationException("SQL rowversion must be eight bytes.");}
    private static string? Text(string? value,int max,bool required)
    {var text=value?.Trim();if(string.IsNullOrEmpty(text)){if(required)throw new ValidationException("Required field is empty.");return null;}
        if(text.Length>max||text.Any(char.IsControl))throw new ValidationException("Invalid branch field.");return text;}
    private static (string Name,string Address,string? Phone,string? Email) Fields(string? name,string? address,string? phone,string? email)
    {
        var n=Text(name,200,true)!;var a=Text(address,4000,true)!;var p=Text(phone,20,false);var e=Text(email,150,false);
        if(p?.Any(c=>!char.IsAsciiDigit(c)&&!"+()- .".Contains(c))==true || e!=null&&(!new EmailAddressAttribute().IsValid(e)||e.Any(c=>c>127)))
            throw new ValidationException("Invalid contact information.");return(n,a,p,e);
    }
    private static int? Number(Exception e){for(Exception? p=e;p!=null;p=p.InnerException)if(p is SqlException s)return s.Number;return null;}
    private sealed class BranchRow
    {
        public long Id{get;set;} public string Code{get;set;}="";public string Name{get;set;}="";
        public string? Address{get;set;}public string? Phone{get;set;}public string? Email{get;set;}public string? Timezone{get;set;}
        public bool IsActive{get;set;}public byte[] Version{get;set;}=[];
    }
}
