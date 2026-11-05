namespace HotelOps.Api.Services.Users;

/// <summary>
/// Release evidence, independent of any target's business data. No configuration,
/// injected evaluator, caller flag or empty responsibility result can approve release.
/// Closing a gate requires a reviewed source change backed by integration evidence.
/// </summary>
internal static class UserAccountDeactivationReleaseAcceptance
{
    internal static IReadOnlyList<string> PendingGates { get; } = Array.AsReadOnly(new[]
    {
        "DEV1_WRITER_COORDINATION", "DEV2_INTEGRATED_SQL_ACCEPTANCE",
        "GLOBAL_USERS_FEATURE_LOCK_ORDER_ACCEPTANCE"
    });

    internal static void EnsureAccepted()
    {
        if (PendingGates.Count != 0) throw new DeactivationReleaseNotAcceptedException();
    }
}

internal sealed class DeactivationReleaseNotAcceptedException()
    : InvalidOperationException("Deactivation release acceptance has not passed.");
