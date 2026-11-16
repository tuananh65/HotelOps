namespace HotelOps.Api.Services.Incidents;

public sealed record IncidentEscalationTiming(bool Eligible,string Classification,decimal? PercentUsed,string? UnavailableReason);
internal static class IncidentEscalationEligibility
{
    internal static IncidentEscalationTiming Calculate(string status,string? severity,DateTime discovered,DateTime? resolution,DateTime now)
    {
        if(status is "CLOSED" or "CANCELLED") return new(false,"CLOSED",null,null);
        var critical=severity=="CRITICAL";
        if(resolution==null || resolution<=discovered || now<discovered)
            return new(critical,critical?"CRITICAL":"UNAVAILABLE",null,"VALID_RESOLUTION_WINDOW_REQUIRED");
        var used=(decimal)(now.Ticks-discovered.Ticks)/(resolution.Value.Ticks-discovered.Ticks)*100m;
        var state=now>resolution ? "OVERDUE" : used>=80m ? "AT_RISK" : "ON_TIME";
        return new(critical || state is "AT_RISK" or "OVERDUE",critical?"CRITICAL":state,used,null);
    }
}
