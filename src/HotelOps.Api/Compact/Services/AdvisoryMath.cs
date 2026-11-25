namespace HotelOps.Api.Compact.Services;
public static class AdvisoryMath
{
    // Scale the numerator before division so a repeating daily rate cannot inflate an exact horizon total.
    public static decimal Forecast(decimal used,int observedDays,int horizon)=>observedDays<=0?0:decimal.Ceiling(Math.Max(0,used)*horizon*10000/observedDays)/10000;
    public static decimal Replenishment(decimal demand,decimal minimum,decimal available,decimal expiring)=>Math.Max(0,Math.Max(demand,minimum)-Math.Max(0,available-expiring));
    public static decimal Transfer(decimal needed,decimal available,decimal minimum,decimal demand,decimal pending)=>Math.Max(0,Math.Min(needed,available-Math.Max(minimum,demand)-pending));
}
