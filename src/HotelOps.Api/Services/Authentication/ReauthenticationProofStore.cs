namespace HotelOps.Api.Services.Authentication;

/// <summary>Process-local, fail-closed server state. Never a client token or a session activity timestamp.</summary>
public sealed class ReauthenticationProofStore(TimeProvider clock)
{
    public static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(5);
    private const int Capacity = 10_000;
    private readonly object _sync = new();
    private readonly Dictionary<Key, Proof> _proofs = new();

    internal void Forget(SessionReference reference, long sessionId)
    {
        lock (_sync) _proofs.Remove(new(reference.UserId, sessionId, reference.KeyHash));
    }

    internal void Publish(SessionReference reference, long sessionId, byte[] version, DateTime verifiedAt, DateTime expiresAt)
    {
        var key = new Key(reference.UserId, sessionId, reference.KeyHash);
        lock (_sync)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var expired in _proofs.Where(p => p.Value.ExpiresAt <= now).Select(p => p.Key).ToArray())
                _proofs.Remove(expired);
            if (expiresAt <= now) return;
            if (_proofs.TryGetValue(key, out var prior) && prior.VerifiedAt > verifiedAt) return;
            if (!_proofs.ContainsKey(key) && _proofs.Count >= Capacity)
                _proofs.Remove(_proofs.MinBy(p => p.Value.ExpiresAt).Key);
            _proofs[key] = new(version.ToArray(), verifiedAt, expiresAt);
        }
    }

    internal bool Matches(SessionReference reference, long sessionId, byte[] currentVersion)
    {
        var key = new Key(reference.UserId, sessionId, reference.KeyHash);
        lock (_sync)
        {
            if (!_proofs.TryGetValue(key, out var proof)) return false;
            var now = clock.GetUtcNow().UtcDateTime;
            if (now < proof.VerifiedAt || now >= proof.ExpiresAt || now - proof.VerifiedAt >= MaximumAge
                || !proof.Version.AsSpan().SequenceEqual(currentVersion))
            {
                _proofs.Remove(key); return false;
            }
            return true;
        }
    }

    private sealed record Key(long UserId, long SessionId, string SessionKeyHash);
    private sealed record Proof(byte[] Version, DateTime VerifiedAt, DateTime ExpiresAt);
}
