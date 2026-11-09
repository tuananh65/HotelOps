using System.Security.Cryptography;
using System.Text.Json;

namespace HotelOps.Api.Services.Notifications;

// Exact bc0110e9 FS020 shapes, internal backend contracts (never HTTP model binding).
public sealed record CleaningAssignmentEvent(Guid OperationId, Guid PublicationId, long ActorId,long BranchId,
    long TaskId,long TaskCycleId,long RoomId,long AssignmentId,long AssigneeUserId,DateTime AssignedAtUtc,
    string SourceRevision,DateTime DueAtUtc);
public sealed record CleaningAssignmentReceipt(long Id,long AssignmentId,long BranchId,long AssigneeUserId,
    Guid OperationId,Guid PublicationId,string WorkflowState,string Notification,string SourceETag,
    string Contract="PROVISIONAL_NOT_ACCEPTED");
public sealed record CleaningAssignmentCommand(long ActorId,long BranchId,long TaskId,long AssigneeId,
    Guid OperationId,string RequestFingerprint);
public sealed class CleaningAssignmentState
{
    public long AssignmentId{get;set;}public long TaskId{get;set;}public long RoomId{get;set;}public long BranchId{get;set;}
    public long ActorId{get;set;}public long AssigneeId{get;set;}public DateTime AssignedAt{get;set;}public DateTime? EndedAt{get;set;}
    public string AssignmentStatus{get;set;}="";public string WorkflowState{get;set;}="";public string TaskStatus{get;set;}="";
    public DateTime? DueAt{get;set;}public long CurrentCycleId{get;set;}public byte[] Revision{get;set;}=[];
    public int ActiveAssignments{get;set;}public bool LocationActive{get;set;}
}
public sealed record CleaningAssignmentGraph(long ActorId,Guid OperationId,string Fingerprint,int FingerprintVersion,
    bool Completed,string ResultJson,Guid PublicationId,string EventType,int Version,long AssignmentId,
    byte[] SourceRevision,string PayloadJson,bool HasAudit,IReadOnlyList<long> Recipients,int OperationPublications);
public enum CleaningSourceDisposition { Current,Stale,Denied }
public sealed class CleaningSourceStaleException : Exception;
public static class CleaningAssignmentProfile
{
    public const string EventType="CLEANING_TASK_ASSIGNED";
    public const short Version=1;
    public const string ReadPolicy="cleaning.assignment.view.v1";
    public static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static string ActionPath(CleaningAssignmentState s)=>$"/cleaning/tasks/{s.TaskId}?branchId={s.BranchId}&assignmentId={s.AssignmentId}";
    public static string PayloadHash(CleaningAssignmentEvent e)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(e,Json)));
    public static void ValidateCommand(CleaningAssignmentCommand c)
    {
        if(c.ActorId<=0||c.BranchId<=0||c.TaskId<=0||c.AssigneeId<=0||c.OperationId==Guid.Empty||
            c.RequestFingerprint.Length!=64||!c.RequestFingerprint.All(Uri.IsHexDigit))throw new ArgumentException("Invalid trusted assignment command.");
    }
    public static CleaningSourceDisposition Current(CleaningAssignmentState s,bool readable)
    {
        if(!s.LocationActive||s.CurrentCycleId!=s.TaskId||s.AssignmentStatus!="ACTIVE"||s.EndedAt is not null||
            s.ActiveAssignments!=1||s.WorkflowState=="CANCELLED")return CleaningSourceDisposition.Stale;
        if(string.IsNullOrWhiteSpace(s.WorkflowState))throw new NotificationContractUnavailableException();
        return readable?CleaningSourceDisposition.Current:CleaningSourceDisposition.Denied;
    }
    public static CleaningAssignmentEvent Build(CleaningAssignmentCommand c,CleaningAssignmentState s,
        CleaningAssignmentReceipt r,DateTime now)
    {
        ValidateCommand(c);
        if(s.AssignmentId<=0||s.TaskId!=c.TaskId||s.BranchId!=c.BranchId||s.ActorId!=c.ActorId||s.AssigneeId!=c.AssigneeId||
            s.RoomId<=0||s.Revision.Length!=8||s.AssignedAt==default||s.AssignedAt>now||s.DueAt is null||
            Current(s,true)!=CleaningSourceDisposition.Current||s.WorkflowState!="ASSIGNED"||s.TaskStatus!="PENDING")
            throw new CleaningSourceStaleException();
        var tag='"'+Convert.ToHexString(s.Revision)+'"';
        if(r.Id!=s.TaskId||r.AssignmentId!=s.AssignmentId||r.BranchId!=s.BranchId||r.AssigneeUserId!=s.AssigneeId||
            r.OperationId!=c.OperationId||r.PublicationId==Guid.Empty||r.WorkflowState!="ASSIGNED"||
            r.Notification!="PENDING_DELIVERY"||r.SourceETag!=tag||r.Contract!="PROVISIONAL_NOT_ACCEPTED")
            throw new NotificationConflictException();
        return new(c.OperationId,r.PublicationId,c.ActorId,s.BranchId,s.TaskId,s.TaskId,s.RoomId,s.AssignmentId,
            s.AssigneeId,DateTime.SpecifyKind(s.AssignedAt,DateTimeKind.Utc),tag,DateTime.SpecifyKind(s.DueAt.Value,DateTimeKind.Utc));
    }
    // Immutable publication proof remains usable for authorized replay after cancellation/reassignment.
    // Current/actionable checks are separate, so replay never rebinds an old assignment.
    public static CleaningAssignmentEvent Verify(CleaningAssignmentGraph g,CleaningAssignmentCommand c)
    {
        ValidateCommand(c);
        var e=JsonSerializer.Deserialize<CleaningAssignmentEvent>(g.PayloadJson,Json)??throw new NotificationConflictException();
        // SQL FOR JSON emits datetime2 without an offset. These two exact contract fields are
        // documented UTC by DEV1; reject local/offset conversions, normalize the native SQL shape.
        if(e.AssignedAtUtc.Kind==DateTimeKind.Local||e.DueAtUtc.Kind==DateTimeKind.Local)throw new NotificationConflictException();
        e=e with {AssignedAtUtc=DateTime.SpecifyKind(e.AssignedAtUtc,DateTimeKind.Utc),DueAtUtc=DateTime.SpecifyKind(e.DueAtUtc,DateTimeKind.Utc)};
        var r=JsonSerializer.Deserialize<CleaningAssignmentReceipt>(g.ResultJson,Json)??throw new NotificationConflictException();
        if(g.ActorId!=c.ActorId||g.OperationId!=c.OperationId||!StringComparer.OrdinalIgnoreCase.Equals(g.Fingerprint,c.RequestFingerprint)||
            g.FingerprintVersion!=1||!g.Completed||g.AssignmentId<=0||g.PublicationId==Guid.Empty||g.EventType!=EventType||g.Version!=Version||
            g.SourceRevision.Length!=8||!g.HasAudit||g.OperationPublications!=1||g.Recipients.Count!=1||g.Recipients[0]!=c.AssigneeId||
            e.ActorId!=c.ActorId||e.OperationId!=c.OperationId||e.PublicationId!=g.PublicationId||e.BranchId!=c.BranchId||
            e.TaskId!=c.TaskId||e.TaskCycleId!=c.TaskId||e.AssignmentId!=g.AssignmentId||e.AssigneeUserId!=c.AssigneeId||e.RoomId<=0||
            e.AssignedAtUtc==default||e.AssignedAtUtc.Kind!=DateTimeKind.Utc||e.DueAtUtc.Kind!=DateTimeKind.Utc||
            e.SourceRevision!='"'+Convert.ToHexString(g.SourceRevision)+'"'||
            r.Id!=e.TaskId||r.AssignmentId!=e.AssignmentId||r.BranchId!=e.BranchId||r.AssigneeUserId!=e.AssigneeUserId||
            r.OperationId!=e.OperationId||r.PublicationId!=e.PublicationId||r.SourceETag!=e.SourceRevision||
            r.WorkflowState!="ASSIGNED"||r.Notification!="PENDING_DELIVERY"||r.Contract!="PROVISIONAL_NOT_ACCEPTED")throw new NotificationConflictException();
        return e;
    }
    public static void VerifySource(CleaningAssignmentEvent e,CleaningAssignmentState s,bool staging)
    {
        if(e.AssignmentId!=s.AssignmentId||e.TaskId!=s.TaskId||e.RoomId!=s.RoomId||e.BranchId!=s.BranchId||
            e.ActorId!=s.ActorId||e.AssigneeUserId!=s.AssigneeId||e.AssignedAtUtc.Ticks!=s.AssignedAt.Ticks)
            throw new NotificationConflictException();
        if(Current(s,true)!=CleaningSourceDisposition.Current)throw new CleaningSourceStaleException();
        if(staging&&(s.WorkflowState!="ASSIGNED"||s.TaskStatus!="PENDING"||s.Revision.Length!=8||
            e.SourceRevision!='"'+Convert.ToHexString(s.Revision)+'"'||s.DueAt?.Ticks!=e.DueAtUtc.Ticks))throw new CleaningSourceStaleException();
    }
}
