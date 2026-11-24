using HotelOps.Api.Compact.Infrastructure;

namespace HotelOps.Api.Compact.Services;

public sealed partial class InsightService
{
    // A calendar period in the branch timezone. The upper UTC bound is exclusive.
    public sealed record TaskWindow(string Period,string Timezone,DateOnly FromDate,DateOnly ToDate,DateTime FromUtc,DateTime ToUtc)
    {
        public static TaskWindow For(DateTime nowUtc,string timezone,string period)
        {
            Output.Require(period is "day" or "week" or "month","INVALID_TASK_PERIOD",400);
            var zone=TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc,DateTimeKind.Utc),zone));
            var first=period switch {
                "week"=>today.AddDays(-((int)today.DayOfWeek+6)%7),
                "month"=>new DateOnly(today.Year,today.Month,1),
                _=>today
            };
            var end=period switch {"week"=>first.AddDays(7),"month"=>first.AddMonths(1),_=>first.AddDays(1)};
            return new(period,timezone,first,end.AddDays(-1),
                TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue),zone),
                TimeZoneInfo.ConvertTimeToUtc(end.ToDateTime(TimeOnly.MinValue),zone));
        }
    }
}
