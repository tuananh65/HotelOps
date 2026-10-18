using HotelOps.Api.Data;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService : IMaintenanceWorkService
{
    private readonly HotelOpsDbContext _dbContext;

    public MaintenanceWorkService(HotelOpsDbContext dbContext)
    {
        _dbContext = dbContext;
    }
}
