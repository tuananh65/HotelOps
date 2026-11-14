using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Data;
using HotelOps.Api.Services.Inventory;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService : IMaintenanceWorkService
{
    private readonly IHttpContextAccessor? _authorityHttp;
    private long TrustedActorId => Dev2ServiceAuthority.ActorId(_authorityHttp);

    private readonly HotelOpsDbContext _dbContext;
    private readonly IInventoryIssueCapacity _issueCapacity;

    public MaintenanceWorkService(HotelOpsDbContext dbContext, IInventoryIssueCapacity issueCapacity, IHttpContextAccessor? authorityHttp = null)
    {
        _dbContext = dbContext;
        _issueCapacity = issueCapacity;
        _authorityHttp = authorityHttp;
    }
}
