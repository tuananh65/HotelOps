using System.Text.Json;
using HotelOps.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

public interface IFe05TrialExecutionAuthorization
{
    // Runtime owner must verify the approved central exclusive lease and owned fixtures.
    // No production implementation or configuration-based approval is registered.
    Task RequireAsync(CancellationToken ct);
}

public sealed record Fe05PublicationPayload(Guid PublicationId, Fe05NotificationCandidate Event);

// Uses ONLY the existing FS013 operation/publication/delivery_intent graph, never C04.
// This SQL implementation is an isolated trial candidate, NOT an accepted production graph.
public sealed class Fe05Fs013CandidateStore(HotelOpsDbContext db, IFe05TrialExecutionAuthorization execution)
    : IFe05OperationStore
{
    private Guid? verifiedTransaction;
    public async Task RequireAvailableAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var tx=Fe05CallerTransaction.Require(db);
        await execution.RequireAsync(ct);
        var connection=db.Database.GetDbConnection();
        if(connection.DataSource!=IntegrationSqlGuard.Server || connection.Database!=IntegrationSqlGuard.Database)
            throw new Fe05NotificationUnavailableException("FE05_ISOLATED_TRIAL_TARGET_REQUIRED");
        if(verifiedTransaction==tx)return;
        var accepted=await db.Database.SqlQuery<int>($"""
            SELECT CASE WHEN CONVERT(nvarchar(128),SERVERPROPERTY('ServerName'))='KIENPC'
              AND DB_NAME()='HotelOps_DEV2_FullStack_Trial' AND ORIGINAL_LOGIN()='HotelOps_DEV2_FullStack_Runtime'
              AND IS_SRVROLEMEMBER('sysadmin')=0 AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER')=0
              AND HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')=0
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.IntegrationTrial'
                AND CONVERT(nvarchar(128),value)='DEV2-INTEGRATION-02-SQL-PROVISIONAL')
              AND EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name='HotelOps.N01.SingleGraphCandidate'
                AND CONVERT(nvarchar(128),value)='IMPLEMENT06-FS016-V1')
              AND COL_LENGTH('dbo.notification_publications','inspection_id')=8
              AND COL_LENGTH('dbo.notification_publications','n01_payload_hash')=32
              AND COL_LENGTH('dbo.notification_operation_reservations','n01_operation_hash')=32
              AND COL_LENGTH('dbo.notification_operation_reservations','n01_receipt_json')=-1
              AND (SELECT COUNT(*) FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID('dbo.notification_publications')
                AND name IN('CK_n01_publication_event','CK_n01_publication_source') AND is_disabled=0 AND is_not_trusted=0)=2
              AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_operation_reservations')
                AND name='PK_notification_operation_v1' AND is_unique=1 AND is_disabled=0)=1
              AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_publications')
                AND name='UX_fs015_occurrence' AND is_unique=1 AND is_disabled=0)=1
              AND (SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.notification_delivery_intents')
                AND name='UQ_delivery_publication_recipient_v1' AND is_unique=1 AND is_disabled=0)=1
              THEN 1 ELSE 0 END AS Value
            """).SingleAsync(ct);
        if(accepted!=1)throw new Fe05NotificationUnavailableException("FE05_SINGLE_FS013_GRAPH_CANDIDATE_SCHEMA_REQUIRED");
        verifiedTransaction=tx;
    }
    public async Task<Fe05StoredOperation?> LockAsync(long actor, Guid operation, CancellationToken ct)
    {
        await RequireAvailableAsync(ct);
        var row=await db.Database.SqlQuery<OperationRow>($"""
            SELECT n01_operation_hash AS Fingerprint,fingerprint AS DomainFingerprint,
              completed AS Completed,result_json AS DomainResultJson,n01_receipt_json AS N01ReceiptJson
            FROM dbo.notification_operation_reservations WITH(UPDLOCK,HOLDLOCK)
            WHERE actor_user_id={actor} AND operation_key={operation}
            """).SingleOrDefaultAsync(ct);
        return row is null ? null : new(row.Fingerprint is null?"":Convert.ToHexString(row.Fingerprint),
            Convert.ToHexString(row.DomainFingerprint),row.Completed,row.DomainResultJson,row.N01ReceiptJson);
    }
    public async Task ReserveAsync(long actor, Guid operation, string fingerprint, string domainFingerprint, CancellationToken ct)
    {
        await RequireAvailableAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_operation_reservations(actor_user_id,operation_key,fingerprint_version,fingerprint,
              completed,result_json,n01_operation_hash,n01_receipt_json)
            VALUES({actor},{operation},1,{Convert.FromHexString(domainFingerprint)},0,NULL,{Convert.FromHexString(fingerprint)},NULL)
            """,ct);
    }
    public async Task WritePublicationAsync(Fe05NotificationCandidate candidate, Guid publication, CancellationToken ct)
    {
        await RequireAvailableAsync(ct);
        if(candidate.Fingerprint!=Fe05NotificationMapping.Fingerprint(candidate) ||
            candidate.EventType is not ("inspection.failed" or "cleaning.rework.requested") || candidate.Version!=1 ||
            candidate.Revision.Length!=18 || candidate.Revision[0]!='"' || candidate.Revision[^1]!='"')
            throw new NotificationConflictException();
        var payload=JsonSerializer.Serialize(new Fe05PublicationPayload(publication,candidate));
        var occurrence=$"N01:{candidate.EventType}:v1:{candidate.InspectionId}:{candidate.Revision.Trim('"')}";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT dbo.notification_publications(publication_id,actor_user_id,operation_key,event_type,schema_version,
              assignment_id,rework_id,inspection_id,source_revision,payload_json,occurrence_key,n01_payload_hash,created_at)
            VALUES({publication},{candidate.ActorId},{candidate.OperationId},{candidate.EventType},1,
              NULL,{candidate.ReworkId},{candidate.InspectionId},{Convert.FromHexString(candidate.Revision.Trim('"'))},
              {payload},{occurrence},{Convert.FromHexString(candidate.Fingerprint)},{candidate.OccurredAt.UtcDateTime})
            """,ct);
        foreach(var recipient in candidate.RecipientIds)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT dbo.notification_delivery_intents(publication_id,user_id,delivery_channel,status,next_attempt_at)
                VALUES({publication},{recipient},'IN_APP','PENDING',{candidate.OccurredAt.UtcDateTime})
                """,ct);
    }
    public async Task CompleteAsync(long actor, Guid operation, string fingerprint, Fe05OperationReceipt receipt, CancellationToken ct)
    {
        await RequireAvailableAsync(ct);
        var affected=await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dbo.notification_operation_reservations SET n01_receipt_json={JsonSerializer.Serialize(receipt)}
            WHERE actor_user_id={actor} AND operation_key={operation} AND completed=0 AND n01_receipt_json IS NULL
              AND n01_operation_hash={Convert.FromHexString(fingerprint)} AND fingerprint={Convert.FromHexString(receipt.RequestFingerprint)}
            """,ct);
        if(affected!=1)throw new NotificationConflictException();
    }
    public async Task VerifyAsync(long actor, Guid operation, string fingerprint, Fe05OperationReceipt receipt, CancellationToken ct)
    {
        var stored=await LockAsync(actor,operation,ct);
        if(stored is null || stored.Fingerprint!=fingerprint || stored.DomainFingerprint!=receipt.RequestFingerprint ||
            stored.N01ReceiptJson!=JsonSerializer.Serialize(receipt))
            throw new NotificationConflictException();
        var rows=await db.Database.SqlQuery<PublicationRow>($"""
            SELECT publication_id AS PublicationId,event_type AS EventType,schema_version AS Version,
              inspection_id AS InspectionId,rework_id AS ReworkId,source_revision AS Revision,payload_json AS Payload,
              n01_payload_hash AS Fingerprint,occurrence_key AS OccurrenceKey
            FROM dbo.notification_publications WITH(HOLDLOCK)
            WHERE actor_user_id={actor} AND operation_key={operation}
            """).ToArrayAsync(ct);
        if(rows.Length!=receipt.Publications.Count)throw new NotificationConflictException();
        foreach(var expected in receipt.Publications)
        {
            var row=rows.SingleOrDefault(r=>r.PublicationId==expected.PublicationId) ?? throw new NotificationConflictException();
            var payload=JsonSerializer.Deserialize<Fe05PublicationPayload>(row.Payload) ?? throw new NotificationConflictException();
            var e=payload.Event;
            if(payload.PublicationId!=row.PublicationId || e.ActorId!=actor || e.OperationId!=operation ||
                e.EventType!=row.EventType || e.EventType!=expected.EventType || e.Version!=row.Version || e.Version!=expected.Version ||
                e.InspectionId!=row.InspectionId || e.ReworkId!=row.ReworkId ||
                e.Revision!='"'+Convert.ToHexString(row.Revision)+'"' ||
                row.OccurrenceKey!=$"N01:{e.EventType}:v1:{e.InspectionId}:{e.Revision.Trim('"')}" ||
                e.Fingerprint!=expected.Fingerprint || e.Fingerprint!=Fe05NotificationMapping.Fingerprint(e) ||
                Convert.ToHexString(row.Fingerprint)!=e.Fingerprint || !e.RecipientIds.SequenceEqual(expected.RecipientIds))
                throw new NotificationConflictException();
            var ids=await db.Database.SqlQuery<long>($"""
                SELECT user_id AS Value FROM dbo.notification_delivery_intents WITH(HOLDLOCK)
                WHERE publication_id={row.PublicationId} AND delivery_channel='IN_APP' ORDER BY user_id
                """).ToArrayAsync(ct);
            var total=await db.Database.SqlQuery<int>($"SELECT COUNT(*) AS Value FROM dbo.notification_delivery_intents WITH(HOLDLOCK) WHERE publication_id={row.PublicationId}").SingleAsync(ct);
            if(total!=ids.Length || !ids.SequenceEqual(expected.RecipientIds))throw new NotificationConflictException();
            // Delivery status may evolve after commit; replay validates immutable publication + fan-out,
            // never resets worker state or repeats the domain mutation.
        }
    }
    private sealed class OperationRow
    {public byte[]? Fingerprint {get;set;}public byte[] DomainFingerprint{get;set;}=[];public bool Completed{get;set;}
     public string? DomainResultJson{get;set;}public string? N01ReceiptJson{get;set;}}
    private sealed class PublicationRow
    {
        public Guid PublicationId{get;set;} public string EventType{get;set;}="";public int Version{get;set;}
        public long? InspectionId{get;set;} public long? ReworkId{get;set;}public byte[] Revision{get;set;}=[];
        public string Payload{get;set;}="";public byte[] Fingerprint{get;set;}=[];public string? OccurrenceKey{get;set;}
    }
}
