using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Both suites modify authorization fixtures in the same disposable SQL database.
// Serialize them so rollback fixtures cannot race the transfer workflow.
[CollectionDefinition("Transfer access database", DisableParallelization = true)]
public sealed class TransferAccessDatabaseCollection { }
