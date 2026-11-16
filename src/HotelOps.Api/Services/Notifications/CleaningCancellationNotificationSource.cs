using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

internal static class CleaningCancellationNotificationSource
{
    internal sealed class Proof
    {
        public long Actor { get; set; }
        public long Assignment { get; set; }
        public Guid Operation { get; set; }
        public Guid Publication { get; set; }
        public byte[] Revision { get; set; } = [];
        public DateTime At { get; set; }
        public string Result { get; set; } = "";
    }

    internal static CleaningCancellationResult? Read(Proof proof, long recipient)
    {
        if (recipient <= 0 || proof.Actor <= 0 || proof.Assignment <= 0 || proof.Operation == Guid.Empty
            || proof.Publication == Guid.Empty || proof.Revision is not { Length: 8 }) return null;
        try
        {
            var r = JsonSerializer.Deserialize<CleaningCancellationResult>(proof.Result, ProvisionalCleaningNotification.Json);
            return r != null && r.Id > 0 && r.BranchId > 0
                && CleaningCancellationRules.ValidReceipt(r, r.BranchId, r.Id, proof.Operation)
                && r.AssigneeId == recipient && r.AssignmentId == proof.Assignment && r.PublicationId == proof.Publication
                && r.OccurredAtUtc == proof.At && r.SourceETag == '"' + Convert.ToHexString(proof.Revision) + '"'
                ? r : null;
        }
        catch (JsonException) { return null; }
    }

    internal static async Task<(long Actor, CleaningCancellationResult Receipt)?> LoadAsync(
        HotelOpsDbContext db, Guid publication, long recipient, CancellationToken ct)
    {
        var proof = await db.Database.SqlQuery<Proof>($"""
            SELECT p.actor_user_id AS Actor,p.assignment_id AS Assignment,p.operation_key AS Operation,
              p.publication_id AS Publication,p.source_revision AS Revision,p.created_at AS At,o.result_json AS Result
            FROM dbo.notification_publications p
            JOIN dbo.notification_operation_reservations o ON o.actor_user_id=p.actor_user_id AND o.operation_key=p.operation_key
            WHERE p.publication_id={publication} AND p.event_type='CLEANING_TASK_CANCELLED' AND p.schema_version=1
              AND o.completed=1 AND o.fingerprint_version=1 AND DATALENGTH(o.fingerprint)=32
              AND EXISTS(SELECT 1 FROM dbo.notification_delivery_intents i WHERE i.publication_id=p.publication_id
                AND i.user_id={recipient} AND i.delivery_channel='IN_APP')
            """).SingleOrDefaultAsync(ct);
        var receipt = proof == null ? null : Read(proof, recipient);
        return receipt != null && await ProvisionalCleaningNotification.VerifyCancellationSourceAsync(db, proof!.Actor, receipt, ct)
            ? (proof!.Actor, receipt) : null;
    }
}
