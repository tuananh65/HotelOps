using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace HotelOps.Api.Services.Notifications;

public sealed record CandidateAssignmentResult(long Id, long AssignmentId, long BranchId, long AssigneeUserId,
    Guid OperationId, Guid PublicationId, string WorkflowState, string Notification, string SourceETag, string Contract = "PROVISIONAL_NOT_ACCEPTED");
public sealed record CandidateAssignmentEvent(Guid OperationId, Guid PublicationId, long ActorId, long BranchId,
    long TaskId, long TaskCycleId, long RoomId, long AssignmentId, long AssigneeUserId, DateTime AssignedAtUtc, string SourceRevision, DateTime DueAtUtc);

// NOT accepted A01. One candidate only, explicit isolated-development opt-in; no hosted dispatcher.
public sealed class ProvisionalCleaningNotification(HotelOpsDbContext db, IConfiguration config, IWebHostEnvironment env)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<bool> AvailableAsync(CancellationToken ct)
    {
        if (!env.IsDevelopment() || config["CleaningAssignment:Candidate"] != "FS009_PROVISIONAL" ||
            db.Database.GetDbConnection().Database != "HotelOps_Canonical_20261106_230000_f090001_Fresh") return false;
        return await db.Database.SqlQuery<int>($"SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('MachineName'))=HOST_NAME() AND CONVERT(nvarchar(128),SERVERPROPERTY('InstanceName'))='SQLEXPRESS' AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS009.Owner' AND CONVERT(nvarchar(100),value)='DEV1_FS009') AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.FS009.ExecutableCandidate' AND CONVERT(nvarchar(100),value)='V1') AND OBJECT_ID('dbo.fs009_verify_assignment_guard','P') IS NOT NULL AND (SELECT COUNT(*) FROM sys.triggers WHERE name IN('TR_fs009_publication_immutable','TR_fs009_operation_immutable','TR_fs009_publication_source') AND is_disabled=0)=3 AND EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications') AND name='UX_fs009_publication_operation' AND is_unique=1 AND is_disabled=0) THEN 1 ELSE 0 END AS [Value]").SingleAsync(ct) == 1;
    }
    public Task<NotificationOperationCandidate?> FindAsync(long actor, Guid key, CancellationToken ct) =>
        db.CandidateOperations.FromSqlInterpolated($"SELECT * FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK) WHERE actor_user_id={actor} AND operation_key={key}").AsNoTracking().SingleOrDefaultAsync(ct);
    public async Task StageAsync(CandidateAssignmentEvent facts, byte[] fingerprint, CandidateAssignmentResult result, CancellationToken ct)
    {
        if (!await AvailableAsync(ct)) throw new InvalidOperationException("CANDIDATE_DISABLED");
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("CALLER_TRANSACTION_REQUIRED");
        db.CandidateOperations.Add(new() { ActorUserId = facts.ActorId, OperationKey = facts.OperationId, Fingerprint = fingerprint, Completed = true, ResultJson = JsonSerializer.Serialize(result, Json), CreatedAt = facts.AssignedAtUtc });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        db.CandidatePublications.Add(new() { PublicationId = facts.PublicationId, ActorUserId = facts.ActorId, OperationKey = facts.OperationId, AssignmentId = facts.AssignmentId, SourceRevision = Convert.FromHexString(facts.SourceRevision.Trim('"')), PayloadJson = JsonSerializer.Serialize(facts, Json), CreatedAt = facts.AssignedAtUtc });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
        db.CandidateIntents.Add(new() { PublicationId = facts.PublicationId, UserId = facts.AssigneeUserId, NextAttemptAt = facts.AssignedAtUtc });
        await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
    }
    public async Task VerifyBeforeCommitAsync(DbTransaction physical, long actor, Guid key, CancellationToken ct)
    {
        if (!ReferenceEquals(db.Database.CurrentTransaction?.GetDbTransaction(), physical) || !ReferenceEquals(physical.Connection, db.Database.GetDbConnection()))
            throw new InvalidOperationException("WRONG_PHYSICAL_TRANSACTION");
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.fs009_verify_assignment_guard @actor={actor},@operation={key}", ct);
    }
    public static CandidateAssignmentResult ReadResult(string json) => JsonSerializer.Deserialize<CandidateAssignmentResult>(json, Json) ?? throw new InvalidOperationException("INVALID_DURABLE_RESULT");
}