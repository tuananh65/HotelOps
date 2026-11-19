using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Opt-in only: writer tests require the disposable clone configured by the test runner.
public sealed class HousekeepingWriterSqlFactAttribute : FactAttribute
{
    public HousekeepingWriterSqlFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TRANSFER_REQUEST_TEST_CONNECTION")))
            Skip = "Requires disposable clone, never the working database.";
    }
}
