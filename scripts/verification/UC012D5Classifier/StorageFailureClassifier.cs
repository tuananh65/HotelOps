using System.Collections.Immutable;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace UC012D5Classifier;

public enum FailureKind { Unexpected, ClassifiedStorage, OperationOwned, Cancelled, OutcomeUnconfirmed }
public sealed record Classification(FailureKind Kind, int? HttpStatus, string PublicCode);

// Pure source candidate. A nonempty profile is configuration input, NOT evidence
// of owner approval. No production caller or installed nonempty profile exists.
public sealed class StorageProfile
{
    public static StorageProfile Disabled { get; } = new("DISABLED", []);
    public string Version { get; }
    public ImmutableHashSet<int> Numbers { get; }

    public StorageProfile(string version, IEnumerable<int> numbers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(numbers);
        Version = version;
        Numbers = numbers.ToImmutableHashSet();
        if (Numbers.Any(n => n <= 0 || n is 1205 or 2601 or 2627))
            throw new ArgumentException("Business conflicts and uncertain-outcome errors cannot be transient profiles.");
    }
}

public static class StorageFailureClassifier
{
    private static readonly Classification Unexpected = new(FailureKind.Unexpected, 500, "INTERNAL_SERVER_ERROR");
    private static readonly Classification OperationOwned = new(FailureKind.OperationOwned, null, "");
    private static readonly Classification Unconfirmed = new(FailureKind.OutcomeUnconfirmed, null, "");

    public static Classification Classify(Exception exception, StorageProfile? profile = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        profile ??= StorageProfile.Disabled;
        // Only transparent EF wrappers are unwrapped. Never traverse arbitrary
        // application exceptions, aggregate branches or parse exception messages.
        for (var depth = 0; depth < 8; depth++)
        {
            if (exception is OperationCanceledException)
                return new(FailureKind.Cancelled, null, "");
            if (exception is DbUpdateConcurrencyException) return OperationOwned;
            if (exception is SqlException sql)
                return ClassifyProviderNumbers(sql.Errors.Cast<SqlError>().Select(e => e.Number).ToArray(), profile);
            if (exception.GetType() != typeof(DbUpdateException) || exception.InnerException is null)
                return Unexpected;
            exception = exception.InnerException;
        }
        return Unexpected;
    }

    // Synthetic numbers exercise the pure decision table only. This method does
    // not authenticate provider provenance, inject faults or prove SQL rollback.
    internal static Classification ClassifyProviderNumbers(IReadOnlyList<int> numbers, StorageProfile profile)
    {
        if (numbers.Count == 0 || numbers.Count > 64) return Unexpected;
        // These remain the operation's responsibility, never a new HTTP mapping.
        if (numbers.All(n => n is 2601 or 2627)) return OperationOwned;
        if (numbers.Any(n => n is 1205 or -2)) return Unconfirmed;
        if (numbers.Any(n => n <= 0) || numbers.Any(n => !profile.Numbers.Contains(n))) return Unexpected;
        return new(FailureKind.ClassifiedStorage, 503, "BRANCH_STORE_UNAVAILABLE");
    }
}
