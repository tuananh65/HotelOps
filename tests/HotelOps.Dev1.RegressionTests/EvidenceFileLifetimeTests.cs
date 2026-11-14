using HotelOps.Api.Services.Housekeeping;
using Xunit;

public sealed class EvidenceFileLifetimeTests
{
    [Fact]
    public void Precommit_failure_removes_only_owned_files()
    {
        var owned = Path.GetTempFileName();
        var other = Path.GetTempFileName();
        try
        {
            using (var scope = new EvidenceFileLifetime()) scope.Files.Add(owned);
            Assert.False(File.Exists(owned));
            Assert.True(File.Exists(other));
        }
        finally { File.Delete(owned); File.Delete(other); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Commit_acknowledgment_does_not_control_file_survival(bool loseAcknowledgment)
    {
        var path = Path.GetTempFileName();
        var payload = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(path, payload);
        try
        {
            try
            {
                using var scope = new EvidenceFileLifetime();
                scope.Files.Add(path);
                await scope.BeforeCommitAsync(CancellationToken.None);
                // Simulate success/unknown acknowledgment after the commit boundary.
                // This checks file lifetime, not a physical SQL commit.
                if (loseAcknowledgment) throw new IOException("Synthetic lost commit acknowledgment");
            }
            catch (IOException) when (loseAcknowledgment) { }
            Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Cancellation_before_commit_keeps_rollback_cleanup()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var scope = new EvidenceFileLifetime())
            {
                scope.Files.Add(path);
                await Assert.ThrowsAsync<OperationCanceledException>(() => scope.BeforeCommitAsync(new CancellationToken(true)));
            }
            Assert.False(File.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Cancellation_after_commit_starts_preserves_evidence()
    {
        var path = Path.GetTempFileName();
        try
        {
            using (var scope = new EvidenceFileLifetime())
            {
                scope.Files.Add(path);
                using var cancelled = new CancellationTokenSource();
                await scope.BeforeCommitAsync(cancelled.Token);
                cancelled.Cancel();
            }
            Assert.True(File.Exists(path));
        }
        finally { File.Delete(path); }
    }
}
