using HotelOps.Api.DTOs.Maintenance;

namespace HotelOps.Api.Services.Maintenance;

public interface IMaintenanceWorkService
{
    Task<MaintenanceWorkDetailDto?> CancelWorkAsync(long id, CancelMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> VerifyWorkAsync(long id, VerifyMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> ReturnWorkAsync(long id, ReturnMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> SubmitResultAsync(long id, SubmitMaintenanceResultRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> HoldWorkAsync(long id, HoldMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> ResumeWorkAsync(long id, ResumeMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkLogDto?> CreateLogAsync(long id, CreateMaintenanceWorkLogRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> StartWorkAsync(long id, StartMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> AssignWorkAsync(long id, AssignMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> CreateWorkAsync(long incidentId, CreateMaintenanceWorkRequest request, CancellationToken cancellationToken);
    Task<MaintenancePagedResponse> GetWorksAsync(MaintenanceQueryRequest request, CancellationToken cancellationToken);
    Task<MaintenanceWorkDetailDto?> GetWorkAsync(long id, CancellationToken cancellationToken);
}
