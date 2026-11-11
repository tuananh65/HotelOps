// The original guarded SQL probe depends on IntegrationSqlGuard absent from this
// selected integration base. Do not import Notification/trial infrastructure to run it.
// Owner must compose a source-bound registered runtime adapter before execution.
internal static class SqlPlacementProbe
{
    internal static Task Run(string fixtureFile,string leaseFile,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(Path.GetFileName(leaseFile)!="SQL_TRIAL_EXCLUSIVE.lock"||!File.Exists(leaseFile))
            throw new InvalidOperationException("Approved central exclusive lease required before configuration/SQL.");
        throw new InvalidOperationException("REGISTERED_RUNTIME_ADAPTER_AND_OWNED_FIXTURE_APPROVAL_REQUIRED");
    }
}
