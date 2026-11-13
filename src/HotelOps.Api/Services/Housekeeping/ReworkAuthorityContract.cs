using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HotelOps.Api.Services.Housekeeping;

// Trusted backend/persistence contract only. Never model-bind these records from HTTP.
public sealed record ReworkAuthorityBinding(long ReworkId, long InspectionId, long CleaningTaskId,
    long RoomId, long BranchId, long VersionId, byte[] InspectionRevision);
public sealed record ReworkAuthorityProof(ReworkAuthorityBinding Binding, string Origin, int ContractVersion,
    string Issuer, int IssuerVersion, long ActorUserId, Guid OperationId, long WriterTransactionId,
    byte[] SourceDigest, byte[] EvidenceDigest);
public sealed record ReworkAuthorityObservation(ReworkAuthorityBinding Binding, long CurrentCycleId,
    bool CurrentFailedSource, bool AuthorizedWorker, bool TrustedIssuer, bool ReceiptMatches, bool AuditMatches,
    long ReaderTransactionId, byte[] SourceDigest, byte[] EvidenceDigest);
public sealed record ReworkAuthorityEvidence(string Kind, long SourceId, long AttachmentId, string StorageKey,
    string FileName, string? MimeType, long? Size, long UploadedBy, DateTime UploadedAtUtc, byte[] ContentDigest);

public static class ReworkAuthorityContract
{
    public const string Origin = "FE05_COMMITTED_V1";
    public const string Issuer = "FE05.UC045.UC046";
    public const string ReceiptContract = "FE05_REWORK_AUTHORITY_V1";
    public const int Version = 1;

    public static string? Validate(ReworkAuthorityProof proof, ReworkAuthorityObservation observed)
    {
        if (proof.Origin != Origin || proof.ContractVersion != Version || proof.Issuer != Issuer || proof.IssuerVersion != Version)
            return "REWORK_AUTHORITY_ORIGIN_INVALID";
        if (proof.ActorUserId <= 0 || proof.OperationId == Guid.Empty || !observed.TrustedIssuer
            || !observed.ReceiptMatches || !observed.AuditMatches) return "REWORK_ISSUER_RECEIPT_UNTRUSTED";
        if (proof.WriterTransactionId <= 0 || observed.ReaderTransactionId <= 0
            || proof.WriterTransactionId == observed.ReaderTransactionId) return "REWORK_AUTHORITY_NOT_COMMITTED";
        var a = proof.Binding; var b = observed.Binding;
        if (a.ReworkId <= 0 || a.InspectionId <= 0 || a.CleaningTaskId <= 0 || a.RoomId <= 0 || a.BranchId <= 0 || a.VersionId <= 0
            || a.ReworkId != b.ReworkId || a.InspectionId != b.InspectionId || a.CleaningTaskId != b.CleaningTaskId
            || a.RoomId != b.RoomId || a.BranchId != b.BranchId || a.VersionId != b.VersionId
            || a.CleaningTaskId != observed.CurrentCycleId || !observed.CurrentFailedSource)
            return "REWORK_AUTHORITY_SOURCE_NOT_CURRENT";
        if (!observed.AuthorizedWorker) return "REWORK_SOURCE_READ_DENIED";
        if (!Equal(a.InspectionRevision, b.InspectionRevision, 8)
            || !Equal(proof.SourceDigest, observed.SourceDigest, 32) || !Equal(proof.EvidenceDigest, observed.EvidenceDigest, 32))
            return "REWORK_AUTHORITY_STALE";
        return null;
    }

    // Source JSON is emitted by the versioned SQL snapshot in ReworkAuthorityReader, not by a client.
    public static byte[] SourceDigest(string canonicalSourceJson, byte[] legacyDigest, byte[] evidenceDigest)
    {
        if (legacyDigest.Length != 32 || evidenceDigest.Length != 32 || string.IsNullOrWhiteSpace(canonicalSourceJson))
            throw new ArgumentException("Canonical source and SHA256 digests required.");
        return SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            contract = ReceiptContract, version = Version, source = canonicalSourceJson,
            legacyDigest = Convert.ToHexString(legacyDigest), evidenceDigest = Convert.ToHexString(evidenceDigest)
        }));
    }

    public static byte[] EvidenceDigest(IReadOnlyList<ReworkAuthorityEvidence> evidence)
    {
        if (evidence.Count > 200 || evidence.Any(e => e.AttachmentId <= 0 || e.SourceId <= 0
                || e.Kind is not ("DEFECT" or "RESULT") || e.ContentDigest.Length != 32 || e.UploadedAtUtc.Kind != DateTimeKind.Utc)
            || evidence.Select(e => (e.Kind, e.SourceId, e.AttachmentId)).Distinct().Count() != evidence.Count)
            throw new ArgumentException("Bounded, distinct, persisted evidence associations required.");
        return SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(evidence
            .OrderBy(e => e.Kind, StringComparer.Ordinal).ThenBy(e => e.SourceId).ThenBy(e => e.AttachmentId)
            .Select(e => new { e.Kind, e.SourceId, e.AttachmentId, e.StorageKey, e.FileName, e.MimeType, e.Size,
                e.UploadedBy, e.UploadedAtUtc, contentSha256 = Convert.ToHexString(e.ContentDigest) }))));
    }

    private static bool Equal(byte[] a, byte[] b, int length)
        => a.Length == length && b.Length == length && CryptographicOperations.FixedTimeEquals(a, b);
}
