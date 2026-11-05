using Microsoft.Data.SqlClient;

namespace HotelOps.Api.Services.Authentication;

public sealed class SessionStoreUnavailableException(Exception innerException)
    : Exception("Session store is unavailable; validity could not be determined.", innerException)
{
    public static bool IsAvailabilityError(int number) => number is
        -2 or 2 or 20 or 53 or 64 or 233 or 4060 or 10928 or 10929
        or 40197 or 40501 or 40613 or 10053 or 10054 or 10060 or 10061 or 11001;

    internal static bool Matches(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is SqlException sql && sql.Errors.Cast<SqlError>().Any(e => IsAvailabilityError(e.Number)))
                return true;
        return false;
    }
}
