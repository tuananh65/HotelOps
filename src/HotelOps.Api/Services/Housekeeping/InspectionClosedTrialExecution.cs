using HotelOps.Api.Services.Notifications;
namespace HotelOps.Api.Services.HotelStructure;
// Source composition is authorized; DEV2's remote trial lane is not authorized here.
internal sealed class InspectionClosedTrialExecution : IFe05TrialExecutionAuthorization
{
    public Task RequireAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new Fe05NotificationUnavailableException("N01_SQL_LANE_NOT_ACCEPTED");
    }
}
