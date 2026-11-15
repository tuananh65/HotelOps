using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    public async Task<RoomTypeBatchResult> ApproveInspectionAsync(SessionReference ticket, long sid,
        long branch, long inspection, string expected, CancellationToken ct)
    {
        if (inspection <= 0 || !WorkflowStrongTag(expected))
            throw new ValidationException("Inspection ID and strong rowversion If-Match are required.");
        InspectionApproveDomainResult? written = null;
        var response = await BatchAsync(ticket, sid, branch, async (_, token) =>
        {
            var prepared = await PrepareApproveInCallerTransactionAsync(ticket, sid, branch, inspection, expected, token);
            if (prepared.Status != RoomTypeBatchStatus.Success) return new(prepared.Status);
            written = await ApproveDomainInCallerTransactionAsync(ticket, sid, branch, inspection, expected, token);
            var entity = await db.RoomInspections.AsNoTracking().SingleAsync(i => i.InspectionId == inspection, token);
            var dto = await ExecutionDto(entity, await InspectionState(inspection, token), branch, token);
            return new(RoomTypeBatchStatus.Success, dto with
            {
                EffectiveReadiness = written.EffectiveReadiness,
                MessageCode = written.EffectiveReadiness == "BLOCKED" ? "MSG17" : "MSG08",
                Notification = "PENDING_DELIVERY"
            }, written.Revision);
        }, ct, permissionCode: "INSPECTION_PERFORM", preserveCommitErrors: true);
        if (response.Status != RoomTypeBatchStatus.Success || written == null) return response;

        // Business commit has already succeeded. Delivery has its own transaction and durable
        // audit key; never replay approval because delivery failed or its acknowledgement was lost.
        var delivery = written.NotificationRecipient.Status;
        if (written.NotificationRecipient.Cleaner is { } cleaner)
        {
            try
            {
                delivery = await new InspectionPassedNotificationDelivery(db, clock)
                    .DeliverAsync(written.AuditId, cleaner.CleanerUserId, ct);
            }
            catch (Exception error) when (error is System.Data.Common.DbException or DbUpdateException
                or InvalidOperationException or OperationCanceledException)
            { delivery = "PENDING_DELIVERY"; }
            finally { db.ChangeTracker.Clear(); }
        }
        return response with { Value = ((InspectionExecutionDto)response.Value!) with { Notification = delivery } };
    }
}
