using HotelOps.Api.DTOs.Assets;

namespace HotelOps.Api.Services.Assets;

public interface IAssetService
{
    Task<AssetHandoverDto?> CreateHandoverAsync(long assetId, CreateAssetHandoverRequest request, CancellationToken cancellationToken);
    Task<AssetPagedResponse<AssetHandoverDto>?> GetHandoversAsync(long assetId, AssetPageRequest request, CancellationToken cancellationToken);
    Task<AssetHandoverDto?> GetHandoverAsync(long handoverId, CancellationToken cancellationToken);
    Task<AssetHandoverDto?> AcceptHandoverAsync(long handoverId, AcceptAssetHandoverRequest request, CancellationToken cancellationToken);
    Task<AssetHandoverDto?> RejectHandoverAsync(long handoverId, RejectAssetHandoverRequest request, CancellationToken cancellationToken);
    Task<AssetDetailDto?> RetireAssetAsync(long assetId, RetireAssetRequest request, CancellationToken cancellationToken);
    Task<AssetPagedResponse<AssetMaintenanceHistoryDto>?> GetMaintenanceHistoryAsync(long assetId, AssetMaintenanceHistoryRequest request, CancellationToken cancellationToken);
    Task<AssetLocationStatisticsResponse> GetStatisticsAsync(AssetStatisticsRequest request, CancellationToken cancellationToken);
    Task<AssetPagedResponse<AssetListDto>> GetAssetsAsync(AssetQueryRequest request, CancellationToken cancellationToken);
    Task<AssetDetailDto?> GetAssetAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetTypeLookupDto>> GetAssetTypesAsync(CancellationToken cancellationToken);
    Task<AssetPagedResponse<AssetConditionHistoryDto>?> GetConditionHistoryAsync(long id, AssetPageRequest request, CancellationToken cancellationToken);
    Task<AssetPagedResponse<AssetLocationHistoryDto>?> GetLocationHistoryAsync(long id, AssetPageRequest request, CancellationToken cancellationToken);
    Task<AssetDetailDto> CreateAssetAsync(CreateAssetRequest request, CancellationToken cancellationToken);
    Task<AssetDetailDto?> UpdateAssetAsync(long id, UpdateAssetRequest request, CancellationToken cancellationToken);
    Task<AssetDetailDto?> AssignLocationAsync(long id, AssignAssetLocationRequest request, CancellationToken cancellationToken);
    Task<AssetDetailDto?> TransferLocationAsync(long id, TransferAssetLocationRequest request, CancellationToken cancellationToken);
    Task<AssetDetailDto?> ChangeConditionAsync(long id, ChangeAssetConditionRequest request, CancellationToken cancellationToken);
}
