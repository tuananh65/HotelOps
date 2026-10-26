using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Users;
using Microsoft.EntityFrameworkCore;

namespace HotelOps.Api.Services.Users;

public sealed class UserDirectoryService(HotelOpsDbContext db, TimeProvider clock) : IUserDirectoryService
{
    public async Task<UserDirectoryResponse> ListAsync(UserDirectoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        Validator.ValidateObject(query, new ValidationContext(query), validateAllProperties: true);
        if (query.BranchId is { } branchId && query.DepartmentId is { } departmentId
            && !await db.Departments.AsNoTracking().AnyAsync(department => department.DepartmentId == departmentId
                && department.BranchId == branchId, cancellationToken))
            throw new ValidationException("Department must belong to the selected branch.");

        // Authentication/Global USER_VIEW authorization precedes this service at the endpoint.
        // One instant gives consistent assignment time predicates in count and page queries.
        var now = clock.GetUtcNow().UtcDateTime;
        var users = UserDirectoryQueries.Filter(db.Users, query, now);
        var total = await users.TagWith("UC141:Count").CountAsync(cancellationToken);
        var items = await UserDirectoryQueries.Page(users, query, now)
            .TagWith("UC141:Page").ToListAsync(cancellationToken);
        return new UserDirectoryResponse(items, query.Page, query.PageSize, total);
    }
}
