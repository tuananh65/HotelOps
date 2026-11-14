using HotelOps.Api.Services.Dev2Authorization;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Assets;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Assets;

public sealed partial class AssetService(HotelOpsDbContext dbContext, IHttpContextAccessor? authorityHttp = null) : IAssetService
{
    private readonly IHttpContextAccessor? _authorityHttp = authorityHttp;
    private long TrustedActorId => Dev2ServiceAuthority.ActorId(_authorityHttp);

    private readonly HotelOpsDbContext _dbContext = dbContext;

    private Task<Asset?> LockAssetAsync(long id, CancellationToken cancellationToken) =>
        _dbContext.Assets
            .FromSqlInterpolated($"SELECT * FROM assets WITH (UPDLOCK, HOLDLOCK) WHERE asset_id = {id}")
            .AsTracking().SingleOrDefaultAsync(cancellationToken);

    private async Task<AssetDetailDto> RequireDetailAsync(long id, CancellationToken cancellationToken) =>
        await GetAssetAsync(id, cancellationToken)
        ?? throw new InvalidOperationException("Asset could not be read.");
}
