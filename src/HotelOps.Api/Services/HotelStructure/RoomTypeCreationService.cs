using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
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

public enum RoomTypeCreationStatus { Success, SessionInvalid, Forbidden, DuplicateCode, Unavailable, SchemaRequired, InvalidChecklist }
public sealed record RoomTypeCreationResult(RoomTypeCreationStatus Status, CreatedRoomTypeDto? Value = null);

public sealed partial class RoomTypeCreationService(HotelOpsDbContext db, ICurrentUserAccessor current,
    IPermissionService permissions, ISqlSessionService sessions, TimeProvider clock,
    HotelOps.Api.Services.Dependencies.Dev2DependencyReader? departmentDependencies = null)
{
    public const string CatalogLock = "HotelOps:RoomTypes:v1";

    public async Task<RoomTypeCreationResult> CreateAsync(SessionReference ticket, long sessionId,
        CreateRoomTypeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket); ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        if(request.BranchId<=0||request.StandardCleaningMinutes<=0||request.DefaultChecklistId<=0)
            throw new ValidationException("BranchId, StandardCleaningMinutes and DefaultChecklistId must be positive.");
        var code=request.Code?.Trim().ToUpperInvariant(); var name=request.Name?.Trim(); var description=request.Description?.Trim();
        if(string.IsNullOrEmpty(code)||code.Length>30||!char.IsAsciiLetterOrDigit(code[0])
            ||code.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-'))
            throw new ValidationException("Code must be 1-30 ASCII letters/digits/_/-, starting with a letter or digit.");
        if(string.IsNullOrEmpty(name)||name.Length>150||name.Any(char.IsControl))
            throw new ValidationException("Name is required, maximum 150 characters, without control characters.");
        if(description?.Length>4000||description?.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t')==true)
            throw new ValidationException("Description must be at most 4000 characters without other control characters.");
        if(request.StandardCapacity<=0)throw new ValidationException("StandardCapacity must be positive or null.");
        if(!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sessionId
            ||actor.IsRestricted||ticket.IsRestricted)return new(RoomTypeCreationStatus.SessionInvalid);
        if(!db.Database.IsSqlServer()||db.Database.CurrentTransaction!=null||db.ChangeTracker.Entries().Any()
            ||System.Transactions.Transaction.Current!=null)
            throw new InvalidOperationException("Room type creation requires its own clean SQL context/transaction.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            await new UserEligibilityLock(db).LockAsync([actor.UserId],ct);
            if(await Gate() is {} initial)return new(initial);
            var branch=await db.Branches.FromSqlInterpolated($"SELECT * FROM dbo.branches WITH(UPDLOCK,HOLDLOCK) WHERE branch_id={request.BranchId}")
                .AsNoTracking().TagWith("UC017:Branch").SingleOrDefaultAsync(ct);
            if(branch==null||!branch.IsActive)return new(RoomTypeCreationStatus.Forbidden);
            // One compatibility namespace also serializes legacy-code reservations and candidate DDL.
            var acquired=await db.Database.SqlQuery<int>($"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource={CatalogLock},@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000; SELECT @result AS [Value];")
                .ToArrayAsync(ct);
            if(acquired.Length!=1||acquired[0]<0)return new(RoomTypeCreationStatus.Unavailable);
            if(await RoomTypeSchema.ReadAsync(db,ct)!=RoomTypeContract.BranchV1)return new(RoomTypeCreationStatus.SchemaRequired);
            // Generic existing templates only. A checklist bound to another RoomType is not copied
            // or silently reassigned. Validate a real published version with an active criterion.
            var checklist=await db.InspectionChecklists.FromSqlInterpolated($"SELECT * FROM dbo.inspection_checklists WITH(HOLDLOCK) WHERE checklist_id={request.DefaultChecklistId}")
                .AsNoTracking().TagWith("UC017:Checklist").SingleOrDefaultAsync(ct);
            var now=clock.GetUtcNow().UtcDateTime;
            if(checklist==null||!await ChecklistEligible(request.DefaultChecklistId,request.BranchId,null,ct))return new(RoomTypeCreationStatus.InvalidChecklist);
            var duplicate=await db.Database.SqlQuery<int>($"SELECT CONVERT(int,COUNT(*)) AS [Value] FROM dbo.room_types WHERE (branch_id={request.BranchId} OR branch_id IS NULL) AND UPPER(LTRIM(RTRIM(type_code)))={code}").SingleAsync(ct);
            if(duplicate!=0)return new(RoomTypeCreationStatus.DuplicateCode);
            long id;
            // Parameterized INSERT on the scoped EF transaction; BranchRoomTypes supplies
            // the read-only V1 metadata while RoomType retains its legacy projection.
            await using(var insert=db.Database.GetDbConnection().CreateCommand())
            {
                insert.Transaction=tx.GetDbTransaction();insert.CommandTimeout=db.Database.GetCommandTimeout()??30;
                insert.CommandText="/* UC017:Insert */ INSERT dbo.room_types(branch_id,type_code,type_name,description,standard_capacity,is_active,standard_cleaning_minutes,default_checklist_id) VALUES(@branch,@code,@name,@description,@capacity,1,@minutes,@checklist); SELECT CONVERT(bigint,SCOPE_IDENTITY());";
                insert.Parameters.Add(new SqlParameter("@branch",SqlDbType.BigInt){Value=request.BranchId});
                insert.Parameters.Add(new SqlParameter("@code",SqlDbType.VarChar,30){Value=code});
                insert.Parameters.Add(new SqlParameter("@name",SqlDbType.NVarChar,150){Value=name});
                insert.Parameters.Add(new SqlParameter("@description",SqlDbType.NVarChar,-1){Value=string.IsNullOrEmpty(description)?DBNull.Value:description});
                insert.Parameters.Add(new SqlParameter("@capacity",SqlDbType.Int){Value=(object?)request.StandardCapacity??DBNull.Value});
                insert.Parameters.Add(new SqlParameter("@minutes",SqlDbType.Int){Value=request.StandardCleaningMinutes});
                insert.Parameters.Add(new SqlParameter("@checklist",SqlDbType.BigInt){Value=request.DefaultChecklistId});
                try{id=(long)(await insert.ExecuteScalarAsync(ct))!;}
                catch(SqlException error) when(IsDuplicateCode(error)){return new(RoomTypeCreationStatus.DuplicateCode);}
            }
            var value=new CreatedRoomTypeDto(id,request.BranchId,code,name,string.IsNullOrEmpty(description)?null:description,
                request.StandardCleaningMinutes,request.DefaultChecklistId,request.StandardCapacity,true);
            var audit=new AuditLog{ActorUserId=actor.UserId,BranchId=request.BranchId,EntityType="room_types",
                EntityId=id.ToString(CultureInfo.InvariantCulture),Action="CREATE",OccurredAt=clock.GetUtcNow().UtcDateTime,
                OldValues=JsonSerializer.Serialize(new{entity=(object?)null},Json),NewValues=JsonSerializer.Serialize(value,Json),
                ChangedFields=JsonSerializer.Serialize(new[]{"branchId","code","name","description","standardCleaningMinutes","defaultChecklistId","standardCapacity","isActive"},Json)};
            db.Add(audit);try{await db.SaveChangesAsync(ct);}finally{db.Entry(audit).State=EntityState.Detached;}
            if(await Gate() is {} final)return new(final);
            await tx.CommitAsync(ct);return new(RoomTypeCreationStatus.Success,value);
        }
        catch(Exception error) when(ct.IsCancellationRequested&&error is DbException or DbUpdateException or SessionStoreUnavailableException)
        {throw new OperationCanceledException("Room type creation cancelled.",error,ct);}
        catch(Exception error) when(!ct.IsCancellationRequested&&SqlNumber(error) is 1205 or 1222 or -2)
        {return new(RoomTypeCreationStatus.Unavailable);}

        async Task<RoomTypeCreationStatus?> Gate()
        {
            var valid=await sessions.ValidateSessionAsync(ticket,ct);ct.ThrowIfCancellationRequested();
            if(!valid.IsValid||valid.Session?.SessionId!=sessionId||valid.Session.IsRestricted)return RoomTypeCreationStatus.SessionInvalid;
            // Candidate schema creates branch-owned records only, never new global legacy rows.
            var access=await permissions.EvaluateBranchAsync("MASTER_DATA_MANAGE",request.BranchId,ct);ct.ThrowIfCancellationRequested();
            return access.IsGranted?null:access.Decision==PermissionDecision.Unavailable
                ?RoomTypeCreationStatus.Unavailable:RoomTypeCreationStatus.Forbidden;
        }
    }
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    private static bool IsDuplicateCode(SqlException error)=>error.Number is 2601 or 2627
        &&error.Message.Contains("'dbo.room_types'",StringComparison.Ordinal)
        &&error.Message.Contains("UX_room_types_",StringComparison.Ordinal);
    private static int? SqlNumber(Exception error)
    {for(Exception? currentError=error;currentError!=null;currentError=currentError.InnerException)if(currentError is SqlException sql)return sql.Number;return null;}
}
