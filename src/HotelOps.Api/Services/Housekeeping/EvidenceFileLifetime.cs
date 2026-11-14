using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("HotelOps.Dev1.RegressionTests")]

namespace HotelOps.Api.Services.Housekeeping;

// Files are registered only after CreateNew succeeds. Before commit they belong to
// the failed invocation; once commit starts they may belong to durable attachments.
// An exception/timeout after that boundary is not proof of rollback. Retain such
// files for reconciliation instead of deleting evidence referenced by committed SQL.
internal sealed class EvidenceFileLifetime : IDisposable
{
    internal List<string> Files { get; } = [];
    private bool commitAttempted;
    private bool disposed;

    internal Task BeforeCommitAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this);
        commitAttempted = true;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (commitAttempted) return;
        foreach (var file in Files) File.Delete(file);
    }
}
