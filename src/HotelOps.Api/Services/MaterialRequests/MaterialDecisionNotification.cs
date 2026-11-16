using System.Data;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Notifications;
using HotelOps.Api.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.MaterialRequests;

internal sealed partial class MaterialApprovalNotificationDelivery
{
    internal enum NoticeType { Decision, Remainder, Fulfillment, ReplenishmentReceipt }
    internal const string DecisionKind = "MATERIAL_REQUEST_DECIDED";
    internal const string DecisionSource = "MATERIAL_APPROVAL_DECISION";
    internal const string RemainderKind = "MATERIAL_REMAINDER_CLOSED";
    internal const string RemainderSource = "MATERIAL_REMAINDER_CLOSURE";
    internal const string FulfillmentKind = "MATERIAL_READY_FOR_PICKUP";
    internal const string FulfillmentSource = "MATERIAL_ISSUE_FULFILLMENT";
    private static (string Source, string Kind) Identity(NoticeType type) => type switch
    {
        NoticeType.Decision => (DecisionSource, DecisionKind),
        NoticeType.Remainder => (RemainderSource, RemainderKind),
        NoticeType.Fulfillment => (FulfillmentSource, FulfillmentKind),
        NoticeType.ReplenishmentReceipt => (ReplenishmentReceiptSource, ReplenishmentReceiptKind),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    private sealed record NoticeEvent
    {
        public long Id { get; init; }
        public long RequestId { get; init; }
        public long RequesterId { get; init; }
        public long BranchId { get; init; }
        public long ActorId { get; init; }
        public DateTime At { get; init; }
    }

    // The committed decision receipt is durable intent, including earlier cycles.
    private static IQueryable<MaterialRequestApproval> Decisions(HotelOpsDbContext db) => db.MaterialRequestApprovals.AsNoTracking()
        .Where(d => d.ApprovalCycle != null && d.ApprovalCycle.RequestId == d.RequestId
            && d.ApprovalCycle.AssignedApproverId == d.ApproverId && d.ApproverId != d.Request.RequesterId
            && d.ApprovalCycle.ResolvedAt == d.DecidedAt
            && (d.Decision == "RETURNED" || d.Decision == "APPROVED" || d.Decision == "REJECTED"));

    private static IQueryable<NoticeEvent> Events(HotelOpsDbContext db, NoticeType type) => type == NoticeType.ReplenishmentReceipt
        ? ReplenishmentReceiptEvents(db)
        : type == NoticeType.Fulfillment
        ? db.MaterialFulfillments.AsNoTracking().Where(f => f.Status == "ISSUED" && f.FulfilledAt != null
            && f.StockDocument != null && f.StockDocument.Status == "POSTED" && f.StockDocument.DocumentType == "ISSUE"
            && f.StockDocument.ReferenceType == "MATERIAL_FULFILLMENT" && f.StockDocument.ReferenceId == f.FulfillmentId
            && f.StockDocument.SourceWarehouseId == f.WarehouseId && f.WarehouseId == f.Request.SourceWarehouseId
            && f.StockDocument.PostedAt == f.FulfilledAt && f.StockDocument.PostedBy == f.FulfilledBy
            && f.MaterialFulfillmentLines.Any())
            .Select(f => new NoticeEvent { Id = f.FulfillmentId, RequestId = f.RequestId, RequesterId = f.Request.RequesterId,
                BranchId = f.Request.BranchId, ActorId = f.FulfilledBy, At = f.FulfilledAt!.Value })
        : type == NoticeType.Remainder
        ? db.MaterialRequests.AsNoTracking().Where(r => r.Status == "REMAINDER_CLOSED"
            && r.RemainderClosedBy != null && r.RemainderClosedBy != r.RequesterId && r.RemainderClosedAt != null
            && r.RemainderCloseReason != null && r.RemainderCloseReason.Trim().Length >= 10
            && r.MaterialRequestLines.Any(l => l.IsActive && l.FulfilledQty > 0)
            && r.MaterialRequestLines.Any(l => l.IsActive && l.ApprovedQty > l.FulfilledQty)
            && db.MaterialRequestApprovalCycles.Any(c => c.RequestId == r.RequestId && c.Status == "APPROVED"
                && c.AssignedApproverId == r.RemainderClosedBy
                && !db.MaterialRequestApprovalCycles.Any(later => later.RequestId == r.RequestId && later.RevisionNo > c.RevisionNo)))
            .Select(r => new NoticeEvent { Id = r.RequestId, RequestId = r.RequestId, RequesterId = r.RequesterId,
                BranchId = r.BranchId, ActorId = r.RemainderClosedBy!.Value, At = r.RemainderClosedAt!.Value })
        : Decisions(db).Select(d => new NoticeEvent { Id = d.ApprovalId, RequestId = d.RequestId, RequesterId = d.Request.RequesterId,
            BranchId = d.Request.BranchId, ActorId = d.ApproverId, At = d.DecidedAt });

    internal async Task<long[]> PendingDecisionsAsync(long after, CancellationToken ct, NoticeType type = NoticeType.Decision)
    {
        if (!await InstalledAsync(ct)) return [];
        if (type == NoticeType.ReplenishmentReceipt && !await ReplenishmentReceiptsInstalledAsync(db, ct)) return [];
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var cutoff = NotificationRetention.At(now, retention.Value).Cutoff;
        var (source, kind) = Identity(type);
        return await Events(db, type).Where(d => d.Id > after && d.At >= cutoff && d.At <= now
            && !db.NotificationRecipients.Any(r => r.Notification.SourceType == source
                && r.Notification.SourceId == d.Id && r.Notification.NotificationType == kind
                && r.UserId == d.RequesterId && r.DeliveryChannel == "IN_APP" && r.DeliveryStatus == "DELIVERED"))
            .OrderBy(d => d.Id).Select(d => d.Id).Take(100).ToArrayAsync(ct);
    }

    internal static async Task<long?> DecisionTargetAsync(HotelOpsDbContext db, long decisionId, long user, CancellationToken ct, NoticeType type = NoticeType.Decision)
    {
        var locked = await new UserEligibilityLock(db).LockAsync([user], ct);
        if (!locked.TryGetValue(user, out var account) || account is not { DeletedAt: null, AccountStatus: "ACTIVE", EmploymentStatus: "ACTIVE" }) return null;
        var decision = await Events(db, type).SingleOrDefaultAsync(d => d.Id == decisionId, ct);
        if (decision == null || decision.RequesterId != user) return null;
        if (type == NoticeType.Fulfillment)
        {
            var request = await db.MaterialRequests.AsNoTracking().Include(r => r.MaterialRequestLines).SingleAsync(r => r.RequestId == decision.RequestId, ct);
            var batches = await db.MaterialFulfillments.AsNoTracking().Include(f => f.MaterialFulfillmentLines)
                .Include(f => f.StockDocument).ThenInclude(d => d!.StockDocumentLines).Where(f => f.RequestId == decision.RequestId).ToArrayAsync(ct);
            try { MaterialRequestService.ValidateFulfillments(request, batches); }
            catch (System.ComponentModel.DataAnnotations.ValidationException) { return null; }
        }
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        var scope = await MaterialRequestReadAccess.ReadAsync(db, user, decision.BranchId, now, ct);
        return await scope.Apply(db.MaterialRequests.AsNoTracking()).AnyAsync(r => r.RequestId == decision.RequestId, ct)
            && await MaterialReadProvenance.ValidAsync(db, decision.RequestId, decision.BranchId, ct) ? decision.RequestId : null;
    }

    internal async Task<string> DeliverDecisionAsync(long decisionId, CancellationToken ct, NoticeType type = NoticeType.Decision)
    {
        if (db.Database.CurrentTransaction != null || db.ChangeTracker.Entries().Any()
            || System.Transactions.Transaction.Current != null || db.Database.CreateExecutionStrategy().RetriesOnFailure)
            throw new InvalidOperationException("Independent clean delivery context required.");
        if (!await InstalledAsync(ct)) return "SCHEMA_REQUIRED";
        var (source, kind) = Identity(type);
        var hint = await Events(db, type).SingleOrDefaultAsync(d => d.Id == decisionId, ct);
        if (hint == null) return "SOURCE_UNAVAILABLE";
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await DecisionTargetAsync(db, decisionId, hint.RequesterId, ct, type) != hint.RequestId) return "RECIPIENT_READ_DENIED";
        var current = await Events(db, type).SingleAsync(d => d.Id == decisionId, ct);
        if (current != hint) return "SOURCE_CHANGED";
        var now = await db.Database.SqlQuery<DateTime>($"SELECT SYSUTCDATETIME() AS Value").SingleAsync(ct);
        if (!NotificationRetention.At(now, retention.Value).Contains(current.At)) return "EVENT_OUTSIDE_RETENTION";
        var notices = await db.Notifications.FromSqlInterpolated($"SELECT * FROM dbo.notifications WITH(UPDLOCK,HOLDLOCK) WHERE source_type={source} AND source_id={decisionId}")
            .AsNoTracking().ToArrayAsync(ct);
        if (notices.Length > 1 || notices.Any(n => n.NotificationType != kind || n.CreatedBy != current.ActorId || n.CreatedAt != current.At))
            throw new InvalidOperationException("Material decision notification provenance conflict.");
        var notice = notices.SingleOrDefault();
        if (notice == null)
        {
            string? fulfillmentMessage = null;
            if (type == NoticeType.Fulfillment)
            {
                // Batch quantities are historical; remaining quantities are explicitly
                // labelled with delivery time, since subsequent issues may already exist.
                var lines = await db.MaterialRequestLines.AsNoTracking()
                    .Where(l => l.RequestId == current.RequestId && l.IsActive)
                    .OrderBy(l => l.LineNo)
                    .Select(l => new { l.LineNo, l.Item.ItemName, l.Uom.UomName, l.ApprovedQty, l.FulfilledQty,
                        Issued = db.MaterialFulfillmentLines.Where(f => f.FulfillmentId == decisionId && f.RequestLineId == l.LineId)
                            .Sum(f => (decimal?)f.IssuedQty) ?? 0m }).ToArrayAsync(ct);
                fulfillmentMessage = "Material issue posted. Remaining quantities as of delivery (UTC "
                    + now.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + "): "
                    + string.Join("; ", lines.Select(l => FormattableString.Invariant(
                        $"Line {l.LineNo} {l.ItemName}: issued in this batch {l.Issued} {l.UomName}; remaining {(l.ApprovedQty.HasValue ? (l.ApprovedQty.Value - l.FulfilledQty).ToString(System.Globalization.CultureInfo.InvariantCulture) : "N/A")} {l.UomName}")));
            }
            notice = new Notification { NotificationType = kind, SourceType = source, SourceId = decisionId,
                Title = type == NoticeType.ReplenishmentReceipt ? "Replenishment receipt recorded"
                    : type == NoticeType.Fulfillment ? "Materials ready for pickup"
                    : type == NoticeType.Remainder ? "Material request remainder closed" : "Material request review updated",
                Message = type == NoticeType.ReplenishmentReceipt ? "A posted warehouse receipt was linked to replenishment for your material request. Open your request to review its current fulfillment status. This notice does not reserve or issue stock."
                    : type == NoticeType.Fulfillment ? fulfillmentMessage!
                    : type == NoticeType.Remainder ? "The unissued remainder of your material request was closed. Open the request to review its history."
                    : "A review decision was recorded for your material request. Open it to see the decision and current status.",
                Priority = "NORMAL", CreatedBy = current.ActorId, CreatedAt = current.At };
            db.Notifications.Add(notice);
            await db.SaveChangesAsync(ct);
        }
        var receipts = await db.NotificationRecipients.AsNoTracking().Where(r => r.NotificationId == notice.NotificationId).ToArrayAsync(ct);
        if (receipts.Length > 1 || receipts.Any(r => r.UserId != hint.RequesterId || r.DeliveryChannel != "IN_APP"
            || r.DeliveryStatus != "DELIVERED" || r.DeliveredAt == null)) throw new InvalidOperationException("Material decision receipt conflict.");
        if (receipts.Length == 0)
        {
            db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notice.NotificationId, UserId = hint.RequesterId,
                DeliveryChannel = "IN_APP", DeliveryStatus = "DELIVERED", DeliveredAt = now });
            await db.SaveChangesAsync(ct);
        }
        if (await DecisionTargetAsync(db, decisionId, hint.RequesterId, ct, type) != hint.RequestId) return "RECIPIENT_READ_DENIED";
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return "DELIVERED";
    }
}
