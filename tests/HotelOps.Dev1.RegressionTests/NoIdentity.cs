using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
namespace HotelOps.Dev1.RegressionTests;
internal sealed class NoIdentity : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user=default; return false; }
}
