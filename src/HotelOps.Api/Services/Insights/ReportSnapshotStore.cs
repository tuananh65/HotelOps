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
        var json = JsonSerializer.SerializeToUtf8Bytes(report);
        if (json.Length > MaximumBytes) throw new InsightException(422, "SNAPSHOT_TOO_LARGE", "Narrow the report filters.");
        lock (gate)
        {
            foreach (var key in entries.Where(e => e.Value.Expires <= clock.GetUtcNow()).Select(e => e.Key).ToArray()) entries.Remove(key);
            while (entries.Count > 0 && (entries.Count >= 20 || entries.Values.Sum(e => (long)e.Json.Length) + json.Length > MaximumBytes))
                entries.Remove(entries.MinBy(e => e.Value.Expires).Key);
            entries[report.SnapshotId] = new(userId, clock.GetUtcNow().AddMinutes(15), json);
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
