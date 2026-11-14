using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Notifications;

// Explicit infrastructure fault seam; never use this for a source authorization denial.
public sealed class NotificationDeliveryTransientException(string message) : Exception(message);

public static class NotificationDeliveryFailures
{
    public static bool IsDeadlock(Exception error) => error is SqlException { Number: 1205 } ||
        error.InnerException is not null && IsDeadlock(error.InnerException);

    public static bool IsRetryable(Exception error) => !IsDeadlock(error) && (error is
        NotificationDeliveryTransientException or TimeoutException or DbUpdateConcurrencyException ||
        error is SqlException { Number: -2 or 20 or 64 or 233 or 10053 or 10054 or 10060 or 40197 or 40501 or 40613 or 10928 or 10929 } ||
        error.InnerException is not null && IsRetryable(error.InnerException));
}
