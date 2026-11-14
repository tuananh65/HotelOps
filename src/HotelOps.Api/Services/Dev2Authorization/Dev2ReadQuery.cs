using System.Linq.Expressions;

namespace HotelOps.Api.Services.Dev2Authorization;

/// <summary>Apply a required server-built visibility predicate before count/page/group/projection. Not a grant factory.</summary>
public static class Dev2ReadQuery
{
    public static IQueryable<T> Restrict<T>(IQueryable<T> query, Expression<Func<T, bool>>? acceptedVisibility)
    {
        if (acceptedVisibility is null) throw new Dev2AccessDenied(403);
        return query.Where(acceptedVisibility);
    }
}
