using HotelOps.Api.Services.Users;

namespace HotelOps.Api.Controllers.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class UserAccessReviewAttribute(bool whenIncluded = false) : Attribute
{
    internal bool WhenIncluded { get; } = whenIncluded;
    private static readonly object ScopeKey = new();
    internal static void Store(HttpContext context, AuthorizedAccessReviewScope scope) => context.Items[ScopeKey] = scope;
    internal static AuthorizedAccessReviewScope Get(HttpContext context) =>
        context.Items[ScopeKey] as AuthorizedAccessReviewScope
        ?? throw new InvalidOperationException("Access review authorization boundary was not executed.");
}
