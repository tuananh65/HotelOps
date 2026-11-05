namespace HotelOps.Api.Security.Authorization;

public readonly record struct CurrentUserInfo(long UserId, long SessionId, bool IsRestricted);

public interface ICurrentUserAccessor
{
    bool TryGetCurrentUser(out CurrentUserInfo user);
}
