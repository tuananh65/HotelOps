using System.Data.Common;
using System.Text.Json;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.HotelStructure;

// Internal prepared payloads, never HTTP DTOs or evidence of commit. Only the
// reviewed owner SQL module + DEV2 operation participant may persist/issue them.
internal sealed record PreparedInspectionReworkAuthority(ReworkAuthoritySnapshot Snapshot,
    ReworkAuthorityReceipt Receipt, InspectionAuthorityAudit Audit);
internal sealed record ReworkAuthorityReceipt(string Contract, string Origin, int Version, string Issuer,
    int IssuerVersion, long ActorUserId, Guid OperationId, long WriterTransactionId, long ReworkId,
    long InspectionId, long CleaningTaskId, long RoomId, long BranchId, long VersionId,
    string InspectionETag, string SourceDigest, string EvidenceDigest, string Conclusion);
internal sealed record InspectionAuthorityAudit(Guid OperationId, string AuthorityContract, string SourceDigest);
internal sealed record InspectionConclusionReceipt(string Contract, string Issuer, int IssuerVersion,
    long InspectionId, long CleaningTaskId, long RoomId, long BranchId, string InspectionETag, string Conclusion);

public sealed partial class RoomTypeCreationService
{
    internal const string Fe04AuthoritySourceCommit = "d37e99f42eaaf3089eb510c6298296a0f0743faa";

    // Caller already performed persisted Rework creation/assignee eligibility.
    // This method re-reads facts; it neither creates a reservation nor inserts a proof.
    internal async Task<PreparedInspectionReworkAuthority> PrepareReworkAuthorityInCallerTransactionAsync(
        SessionReference ticket, long sid, DbConnection connection, DbTransaction transaction,
        long branch, long inspection, long rework, string expectedETag, Guid operation,
        IReadOnlyList<long> selectedDefects, string? privateEvidenceRoot, CancellationToken ct)
    {
        RequireInspectionAuthorityTransaction(connection, transaction);
        if (operation == Guid.Empty || rework <= 0 || selectedDefects.Count is < 1 or > 200
            || selectedDefects.Any(id => id <= 0) || selectedDefects.Distinct().Count() != selectedDefects.Count)
            throw new ArgumentException("REWORK_AUTHORITY_COMMAND_INVALID");
        var source = await PrepareReworkInCallerTransactionAsync(ticket, sid, branch, inspection, expectedETag, ct);
        if (source.Status != RoomTypeBatchStatus.Success)
            throw new InvalidOperationException("REWORK_AUTHORITY_SOURCE_" + source.Status);
        var selected = await db.Database.SqlQuery<long>($"SELECT defect_id AS Value FROM dbo.cleaning_rework_defects WITH(UPDLOCK,HOLDLOCK) WHERE rework_id={rework}").ToArrayAsync(ct);
        if (!selected.Order().SequenceEqual(selectedDefects.Order()))
            throw new InvalidOperationException("REWORK_AUTHORITY_SELECTION_MISMATCH");
        if (source.Value is not DTOs.Housekeeping.ReworkSourcePreview currentSource
            || selected.Any(id => !currentSource.DefectIds.Contains(id)))
            throw new InvalidOperationException("REWORK_AUTHORITY_DEFECT_SOURCE_INVALID");
        // Use the FE04 implementation directly, including private-file content hashes.
        var snapshot = await BuildReworkAuthoritySnapshotAsync(connection, transaction, rework, privateEvidenceRoot, ct)
            ?? throw new InvalidOperationException("REWORK_AUTHORITY_EVIDENCE_UNAVAILABLE");
        var b = snapshot.Binding;
        if (b.BranchId != branch || b.InspectionId != inspection || b.ReworkId != rework
            || WorkflowTag(b.InspectionRevision) != expectedETag)
            throw new InvalidOperationException("REWORK_AUTHORITY_BINDING_MISMATCH");
        if (await AuthorizeRejectLoadAsync(ticket, sid, branch, ct) is { } denied)
            throw new InvalidOperationException("REWORK_AUTHORITY_FINAL_" + denied);
        var writerTransaction = await db.Database.SqlQuery<long>($"SELECT CURRENT_TRANSACTION_ID() AS Value").SingleAsync(ct);
        return PrepareAuthorityPayload(snapshot, ticket.UserId, operation, writerTransaction);
    }

    internal static PreparedInspectionReworkAuthority PrepareAuthorityPayload(ReworkAuthoritySnapshot snapshot,
        long actor, Guid operation, long writerTransaction)
    {
        var b = snapshot.Binding;
        if (actor <= 0 || operation == Guid.Empty || writerTransaction <= 0 || !snapshot.CurrentFailedSource
            || b.ReworkId <= 0 || b.InspectionId <= 0 || b.CleaningTaskId <= 0 || b.RoomId <= 0 || b.BranchId <= 0 || b.VersionId <= 0
            || b.InspectionRevision.Length != 8 || snapshot.SourceDigest.Length != 32 || snapshot.EvidenceDigest.Length != 32
            || snapshot.CurrentCycleId != b.CleaningTaskId)
            throw new InvalidOperationException("REWORK_AUTHORITY_CURRENT_PERSISTED_SNAPSHOT_REQUIRED");
        var receipt = new ReworkAuthorityReceipt(ReworkAuthorityContract.ReceiptContract, ReworkAuthorityContract.Origin,
            ReworkAuthorityContract.Version, ReworkAuthorityContract.Issuer, ReworkAuthorityContract.Version,
            actor, operation, writerTransaction, b.ReworkId, b.InspectionId, b.CleaningTaskId, b.RoomId,
            b.BranchId, b.VersionId, WorkflowTag(b.InspectionRevision), Convert.ToHexString(snapshot.SourceDigest),
            Convert.ToHexString(snapshot.EvidenceDigest), "FAILED");
        return new(snapshot, receipt, new(operation, ReworkAuthorityContract.ReceiptContract, receipt.SourceDigest));
    }

    internal async Task<InspectionConclusionReceipt> PrepareFailedConclusionReceiptInCallerTransactionAsync(
        SessionReference ticket, long sid, DbConnection connection, DbTransaction transaction,
        long branch, long inspection, string expectedETag, CancellationToken ct)
    {
        RequireInspectionAuthorityTransaction(connection, transaction);
        var source = await PrepareReworkInCallerTransactionAsync(ticket, sid, branch, inspection, expectedETag, ct);
        if (source.Status != RoomTypeBatchStatus.Success || source.Value is not DTOs.Housekeeping.ReworkSourcePreview value)
            throw new InvalidOperationException("INSPECTION_CONCLUSION_SOURCE_" + source.Status);
        // R01 projection additionally requires the concluding actor to be the assigned inspector.
        if (!await db.RoomInspections.AsNoTracking().AnyAsync(i => i.InspectionId == inspection && i.InspectorUserId == ticket.UserId, ct))
            throw new InvalidOperationException("INSPECTION_CONCLUSION_ASSIGNED_INSPECTOR_REQUIRED");
        return new("FE05_INSPECTION_CONCLUSION_V1", "FE05.UC044.UC045", 1,
            inspection, value.CleaningTaskId, value.RoomId, branch, value.ETag, "FAILED");
    }

    // Caller must have staged the actual durable conclusion receipt through DEV2.
    // The real FE04 adapter checks that persisted receipt and pinned issuer itself.
    // No boolean supplied by FE05 can assert staging/module trust on its behalf.
    internal async Task<InspectionRoomProjection> ProjectFailedConclusionInCallerTransactionAsync(
        SessionReference ticket, long sid, DbConnection connection, DbTransaction transaction,
        long branch, long inspection, string expectedETag, Guid operation, CancellationToken ct)
    {
        await PrepareFailedConclusionReceiptInCallerTransactionAsync(ticket, sid, connection, transaction,
            branch, inspection, expectedETag, ct);
        var result = await new InspectionRoomProjectionAdapter(db).ApplyAsync(connection, transaction,
            ticket.UserId, branch, inspection, Convert.FromHexString(expectedETag.Trim('"')), operation, ct);
        if (await AuthorizeRejectLoadAsync(ticket, sid, branch, ct) is { } denied)
            throw new InvalidOperationException("INSPECTION_PROJECTION_FINAL_" + denied);
        return result;
    }

    private void RequireInspectionAuthorityTransaction(DbConnection connection, DbTransaction transaction)
    {
        InspectionConclusionSql.RequireTransaction(db);
        if (!ReferenceEquals(connection, db.Database.GetDbConnection())
            || !ReferenceEquals(transaction, db.Database.CurrentTransaction!.GetDbTransaction())
            || !ReferenceEquals(transaction.Connection, connection) || System.Transactions.Transaction.Current != null)
            throw new InvalidOperationException("INSPECTION_AUTHORITY_WRONG_PHYSICAL_TRANSACTION");
    }
}
