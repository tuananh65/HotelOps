using HotelOps.Api.Data;
using HotelOps.Api.Services.Inventory;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService : IMaintenanceWorkService
{
    private readonly HotelOpsDbContext _dbContext;
    private readonly IInventoryIssueCapacity _issueCapacity;

    public MaintenanceWorkService(HotelOpsDbContext dbContext, IInventoryIssueCapacity issueCapacity)
    {
        _dbContext = dbContext;
        _issueCapacity = issueCapacity;
    }
}
