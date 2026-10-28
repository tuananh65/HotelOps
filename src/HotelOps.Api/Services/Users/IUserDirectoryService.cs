using HotelOps.Api.DTOs.Users;

namespace HotelOps.Api.Services.Users;

public interface IUserDirectoryService
{
    Task<UserDirectoryResponse> ListAsync(UserDirectoryQuery query, CancellationToken cancellationToken);
    Task<UserAccountDetailDto?> GetAsync(long userId, CancellationToken cancellationToken);
}
