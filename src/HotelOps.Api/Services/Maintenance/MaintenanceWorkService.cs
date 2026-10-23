using HotelOps.Api.Data;
using HotelOps.Api.Services.Inventory;
using Microsoft.Extensions.Options;

namespace HotelOps.Api.Services.Maintenance;

public sealed partial class MaintenanceWorkService : IMaintenanceWorkService
{
    private readonly HotelOpsDbContext _dbContext;
    private readonly IInventoryIssueCapacity _issueCapacity;
    private readonly MaintenanceOptions _options;
    private readonly TimeProvider _clock;
    private long? _logCycleId;

    public MaintenanceWorkService(HotelOpsDbContext dbContext, IInventoryIssueCapacity issueCapacity,
        IOptions<MaintenanceOptions> options, TimeProvider clock)
    {
        _dbContext = dbContext;
        _issueCapacity = issueCapacity;
        _options = options.Value;
        if (_options.ReopenWindowDays <= 0) throw new OptionsValidationException(nameof(MaintenanceOptions), typeof(MaintenanceOptions), ["ReopenWindowDays must be positive."]);
        _clock = clock;
    }
}
