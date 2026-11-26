using System.Data;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Data;
using HotelOps.Api.Compact.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Compact.Infrastructure;

public sealed class ApiException(int status, string code, string? detail = null) : Exception(detail ?? code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed class Actor(IHttpContextAccessor accessor)
{
    public bool IsBusinessCommand => accessor.HttpContext is { } context &&
        HotelOps.Api.Compact.Security.ShiftCommandAccess.IsBusinessCommand(context.Request.Method,context.Request.Path.Value??"");
    public long Id => long.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),out var id)
        ? id : throw new ApiException(401,"AUTHENTICATION_REQUIRED");
    public long SessionId => long.TryParse(accessor.HttpContext?.User.FindFirstValue("session_id"),out var id)
        ? id : throw new ApiException(401,"AUTHENTICATION_REQUIRED");
}
public static class Output
{
    private static readonly HashSet<string> Secret = ["PasswordHash","SecurityStamp","TokenHash","StorageKey","ProtectedToken","PasswordFingerprint"];
    public static ResourceDto Record<T>(T entity) where T:class => new(typeof(T).Name,
        entity.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public)
        .Where(p=>!Secret.Contains(p.Name) && (p.PropertyType.IsValueType || p.PropertyType==typeof(string) || p.PropertyType==typeof(byte[])))
        .ToDictionary(p=>JsonNamingPolicy.CamelCase.ConvertName(p.Name),p=>p.GetValue(entity)));
    public static void Version(byte[] actual,string expected)
    {
        byte[] supplied;
        try { supplied=Convert.FromBase64String(expected ?? ""); }
        catch(FormatException){throw new ApiException(400,"INVALID_ROW_VERSION");}
        if(supplied.Length!=8)throw new ApiException(400,"INVALID_ROW_VERSION");
        if(!actual.AsSpan().SequenceEqual(supplied))throw new ApiException(409,"STALE_ROW_VERSION");
    }
    public static string Reason(string? reason) => reason?.Trim() is { Length: >=10 and <=2000 } valid
        ? valid : throw new ApiException(400,"REASON_REQUIRES_10_TO_2000_CHARACTERS");
    public static Guid Key(Guid key) => key!=Guid.Empty?key:throw new ApiException(400,"OPERATION_KEY_REQUIRED");
    public static void Require(bool valid,string code,int status=409){if(!valid)throw new ApiException(status,code);}
}
public sealed class Atomic(HotelOpsCompactDbContext db)
{
    public async Task<T> Run<T>(Func<Task<T>> action,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction!=null)return await action();
        await using var transaction=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var result=await action();
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
public sealed class Audit(HotelOpsCompactDbContext db,Actor actor,TimeProvider clock)
{
    public void Add(string type,long id,string action,long? branchId=null,object? before=null,object? after=null,Guid? key=null)
    {
        db.AuditLogs.Add(new AuditLog {ActorUserId=actor.Id,BranchId=branchId,EntityType=type,EntityKey=id.ToString(),
            Action=action,OperationKey=key??Guid.NewGuid(),BeforeJson=before==null?null:JsonSerializer.Serialize(before),
            AfterJson=after==null?null:JsonSerializer.Serialize(after),OccurredAt=clock.GetUtcNow().UtcDateTime});
    }
}
public sealed class ErrorMiddleware(RequestDelegate next,ILogger<ErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try {await next(context);}
        catch(OperationCanceledException) when(context.RequestAborted.IsCancellationRequested) { }
        catch(Exception error)
        {
            if(context.Response.HasStarted)throw;
            // EF can wrap a deadlock in InvalidOperationException before it reaches this boundary.
            // Classify the actual SQL error without retrying any business transaction here.
            SqlException? sql=null;
            for(Exception? cause=error;cause!=null;cause=cause.InnerException)
                if(cause is SqlException databaseError){sql=databaseError;break;}
            var (status,code)=error switch {
                ApiException a=>(a.Status,a.Code),
                DbUpdateConcurrencyException=>(409,"STALE_ROW_VERSION"),
                _ when sql?.Number==1205=>(409,"TRANSACTION_RETRY_REQUIRED"),
                _ when sql?.Number is 2601 or 2627=>(409,"DUPLICATE_RECORD"),
                _ when sql?.Number is 2628 or 8152 or 515 or 8115=>(400,"INVALID_FIELD_VALUE"),
                _ when sql?.Number==547 || sql?.Number is >=51000 and <52000=>(409,"DATABASE_INVARIANT_VIOLATION"),
                _=>(500,"INTERNAL_ERROR")};
            if(status==500)logger.LogError(error,"Compact API failure {TraceId}",context.TraceIdentifier);
            context.Response.StatusCode=status;
            await context.Response.WriteAsJsonAsync(new {type=$"urn:hotelops:error:{code}",title=code,status,
                traceId=context.TraceIdentifier},context.RequestAborted);
        }
    }
}
