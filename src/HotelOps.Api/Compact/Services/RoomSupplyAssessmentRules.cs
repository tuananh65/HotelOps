using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Infrastructure;

namespace HotelOps.Api.Compact.Services;

public static class RoomSupplyAssessmentRules
{
    public static decimal Validate(decimal target,AssessRoomSupplyLine input)
    {
        var quantities=new decimal?[]{input.CurrentQuantity,input.UsableQuantity,input.UnusableQuantity,input.ExpiredQuantity,input.ReplenishedQuantity};
        Output.Require(quantities.All(q=>q is >=0 and <=999999999&&decimal.Round(q.Value,4)==q.Value),"ASSESSMENT_QUANTITIES_REQUIRED",400);
        Output.Require(input.CurrentQuantity==input.UsableQuantity+input.UnusableQuantity+input.ExpiredQuantity,"ASSESSMENT_COUNTS_MISMATCH",400);
        var missing=Math.Max(0,target-input.UsableQuantity);
        Output.Require(input.ReplenishedQuantity<=missing,"REFILL_EXCEEDS_MISSING_QUANTITY",400);
        return missing;
    }
}
