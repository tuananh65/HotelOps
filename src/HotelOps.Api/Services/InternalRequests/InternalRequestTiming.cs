namespace HotelOps.Api.Services.InternalRequests;

// Approved UC122: the official creation timestamp is the origin, even after a deadline change.
// Classification is informational; it never grants escalation authority.
public static class InternalRequestTiming
{
    public static string Classify(string status, DateTime createdAt, DateTime? dueAt, DateTime now)
    {
        if (status is "CLOSED" or "REJECTED" or "CANCELLED") return "TERMINAL";
        if (dueAt is not {} due || due <= createdAt) return "INVALID_WINDOW";
        if (now > due) return "OVERDUE";
        // Decimal avoids overflow across the entire datetime2 range and preserves 100ns precision.
        return (decimal)(now.Ticks - createdAt.Ticks) * 5 >= (decimal)(due.Ticks - createdAt.Ticks) * 4
            ? "AT_RISK" : "ON_TIME";
    }
}
