namespace HotelOps.Api.Services.TrialPreparation;

public enum VersionPrecondition { ReadyForAuthorizedWriter, Missing, Malformed, Unavailable, Stale }

public static class StrongVersionPreconditions
{
    // C09 HTTP compatibility prototype. Durable bytes must come from an approved server mapping.
    // No Asset token is generated: the current Asset schema has no RowVersion property.
    public static VersionPrecondition Evaluate(string? ifMatch, ReadOnlySpan<byte> durableVersion)
    {
        if (ifMatch is null) return VersionPrecondition.Missing;
        if (ifMatch.Length < 3 || ifMatch[0] != '"' || ifMatch[^1] != '"' ||
            ifMatch.StartsWith("W/", StringComparison.Ordinal) || ifMatch.Contains(','))
            return VersionPrecondition.Malformed;
        byte[] supplied;
        try { supplied = Convert.FromBase64String(ifMatch[1..^1]); }
        catch (FormatException) { return VersionPrecondition.Malformed; }
        if (supplied.Length == 0 || Convert.ToBase64String(supplied) != ifMatch[1..^1])
            return VersionPrecondition.Malformed;
        if (durableVersion.IsEmpty) return VersionPrecondition.Unavailable;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(supplied, durableVersion)
            ? VersionPrecondition.ReadyForAuthorizedWriter : VersionPrecondition.Stale;
    }
}
