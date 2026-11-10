using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HotelOps.Api.Services.Notifications;

namespace HotelOps.Api.Services.HotelStructure;

// Candidate for existing attachments.storage_key; no schema/new digest authority. The digest
// in this identity detects replacement; it does NOT attest WORM, ACL, retention or deployment.
internal sealed record InspectionEvidenceVersion(string Key,string Digest,long Length)
{
    internal static InspectionEvidenceVersion New(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <1 or >10*1024*1024)throw new ArgumentException("Evidence size invalid.");
        var digest=Convert.ToHexString(SHA256.HashData(bytes));
        return new("insp-v1-"+Guid.NewGuid().ToString("N")+"-"+digest+".blob",digest,bytes.Length);
    }
    internal static InspectionEvidenceVersion Parse(string key,long length)
    {
        if(key==null||!Regex.IsMatch(key,@"\Ainsp-v1-[a-f0-9]{32}-[A-F0-9]{64}\.blob\z",RegexOptions.CultureInvariant)
            ||length is <1 or >10*1024*1024)throw new Fe05NotificationUnavailableException("FE05_VERSIONED_EVIDENCE_IDENTITY_REQUIRED");
        return new(key,key.Substring(41,64),length);
    }
}
internal sealed class InspectionEvidenceReadLease: IAsyncDisposable
{
    private readonly FileStream stream;
    internal InspectionEvidenceVersion Version{get;}
    private InspectionEvidenceReadLease(FileStream stream,InspectionEvidenceVersion version){this.stream=stream;Version=version;}
    internal static async Task<InspectionEvidenceReadLease> OpenAsync(string root,InspectionEvidenceVersion version,CancellationToken ct)
    {
        var parsed=InspectionEvidenceVersion.Parse(version.Key,version.Length);
        if(parsed!=version)throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_VERSION_MISMATCH");
        var file=InspectionVersionedEvidenceCandidate.CheckedPath(root,version.Key);
        var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous);
        var lease=new InspectionEvidenceReadLease(stream,version);
        try{await lease.VerifyAsync(ct);return lease;}catch{await lease.DisposeAsync();throw;}
    }
    internal async Task VerifyAsync(CancellationToken ct)
    {
        if(stream.Length!=Version.Length)throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_LENGTH_CHANGED");
        stream.Position=0;var digest=await SHA256.HashDataAsync(stream,ct);
        if(!CryptographicOperations.FixedTimeEquals(digest,Convert.FromHexString(Version.Digest)))
            throw new Fe05NotificationUnavailableException("FE05_EVIDENCE_BYTES_CHANGED");
    }
    public ValueTask DisposeAsync()=>stream.DisposeAsync();
}
internal sealed class InspectionPreparedEvidence: IAsyncDisposable
{
    private readonly string path;private bool retained;
    internal InspectionEvidenceVersion Version{get;}
    internal InspectionPreparedEvidence(string path,InspectionEvidenceVersion version){this.path=path;Version=version;}
    // Only after known successful guarded SQL COMMIT; unknown outcome must retain the orphan,
    // not delete bytes that a possibly committed attachment/proof now references.
    internal void RetainAfterCommitOrUnknownOutcome()=>retained=true;
    public ValueTask DisposeAsync(){if(!retained)File.Delete(path);return ValueTask.CompletedTask;}
}
internal static class InspectionVersionedEvidenceCandidate
{
    internal const bool DurableImmutabilityAccepted=false;
    internal static async Task<InspectionPreparedEvidence> PrepareAsync(string root,ReadOnlyMemory<byte> bytes,CancellationToken ct)
    {
        // Snapshot caller memory before hashing/writing, so caller buffer mutation cannot change
        // bytes between identity creation and persisted file. MIME/auth remain existing caller concerns.
        var snapshot=bytes.ToArray();var version=InspectionEvidenceVersion.New(snapshot);var file=CheckedPath(root,version.Key);
        var created=false;
        try
        {
            await using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous))
            {created=true;await stream.WriteAsync(snapshot,ct);await stream.FlushAsync(ct);}
            return new(file,version);
        }
        catch{if(created)File.Delete(file);throw;}
    }
    internal static string CheckedPath(string root,string key)
    {
        InspectionEvidenceVersion.Parse(key,1);
        var directory=Path.GetFullPath(root);
        // No auto-provisioning permissions, mount, storage root, symlink or shared configuration.
        if(!Directory.Exists(directory))throw new Fe05NotificationUnavailableException("FE05_APPROVED_PRIVATE_STORAGE_ROOT_REQUIRED");
        for(var parent=new DirectoryInfo(directory);parent!=null;parent=parent.Parent)
            if((parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new Fe05NotificationUnavailableException("FE05_STORAGE_REPARSE_POINT_REJECTED");
        var file=Path.Combine(directory,key);
        if(Path.Exists(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)
            throw new Fe05NotificationUnavailableException("FE05_STORAGE_REPARSE_POINT_REJECTED");
        return file;
    }
}
