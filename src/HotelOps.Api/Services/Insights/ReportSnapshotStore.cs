using System.Text.Json;
using HotelOps.Api.DTOs.Insights;

namespace HotelOps.Api.Services.Insights;

// Ephemeral preview cache, not authoritative history. No disk persistence and no
// client-supplied rows. Export must reauthorize even after a successful preview.
public sealed class ReportSnapshotStore(TimeProvider clock)
{
    private sealed record Entry(long UserId, DateTimeOffset Expires, byte[] Json);
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private const int MaximumBytes = 32 * 1024 * 1024;

    public void Put(long userId, ReportResult report)
    {
        if (userId <= 0 || !Guid.TryParseExact(report.SnapshotId, "N", out _))
            throw new ArgumentException("A valid owner and server snapshot ID are required.");
        var json = JsonSerializer.SerializeToUtf8Bytes(report);
        if (json.Length > MaximumBytes) throw new InsightException(422, "SNAPSHOT_TOO_LARGE", "Narrow the report filters.");
        lock (gate)
        {
            var now = clock.GetUtcNow();
            foreach (var key in entries.Where(e => e.Value.Expires <= now).Select(e => e.Key).ToArray()) entries.Remove(key);
            if (entries.ContainsKey(report.SnapshotId))
                throw new InvalidOperationException("A snapshot ID cannot be replaced.");
            while (entries.Count > 0 && (entries.Count >= 20 || entries.Values.Sum(e => (long)e.Json.Length) + json.Length > MaximumBytes))
                entries.Remove(entries.MinBy(e => e.Value.Expires).Key);
            entries[report.SnapshotId] = new(userId, now.AddMinutes(15), json);
        }
    }

    public ReportResult Get(long userId, string id)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.UserId != userId || entry.Expires <= clock.GetUtcNow())
                throw new InsightException(404, "SNAPSHOT_UNAVAILABLE", "Preview expired or unavailable. Generate a new preview.");
            return JsonSerializer.Deserialize<ReportResult>(entry.Json)!;
        }
    }
}
