namespace HotelOps.Api.Compact.Services;

public static class ExpiryWarningPolicy
{
    public static int Days(IConfiguration? configuration,long branchId)
    {
        var value=configuration?[$"Inventory:Branches:{branchId}:ExpiryWarningDays"]
            ??configuration?["Inventory:ExpiryWarningDays"];
        return int.TryParse(value,out var days)&&days is >=1 and <=365?days:30;
    }
}
