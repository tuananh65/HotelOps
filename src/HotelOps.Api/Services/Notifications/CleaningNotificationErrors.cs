namespace HotelOps.Api.Services.Notifications;
public enum CleaningNotificationFailure { Conflict,Forbidden,Unavailable,Cancelled,Deadlock,IntegrityFailure }
public static class CleaningNotificationErrors
{
    // Consumer rolls back first, then maps to its existing HTTP/business result convention.
    // Deadlock is distinct and must fail SQL acceptance, never be counted as successful recovery.
    public static CleaningNotificationFailure Classify(Exception error)=>error switch {
        OperationCanceledException=>CleaningNotificationFailure.Cancelled,
        _ when NotificationDeliveryFailures.IsDeadlock(error)=>CleaningNotificationFailure.Deadlock,
        UnauthorizedAccessException=>CleaningNotificationFailure.Forbidden,
        CleaningSourceStaleException or NotificationConflictException=>CleaningNotificationFailure.Conflict,
        NotificationContractUnavailableException=>CleaningNotificationFailure.Unavailable,
        _ when NotificationDeliveryFailures.IsRetryable(error)=>CleaningNotificationFailure.Unavailable,
        _=>CleaningNotificationFailure.IntegrityFailure};
}
