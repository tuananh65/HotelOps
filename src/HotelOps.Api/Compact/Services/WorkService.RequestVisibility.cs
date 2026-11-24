using HotelOps.Api.Compact.Entities;
namespace HotelOps.Api.Compact.Services;
public sealed partial class WorkService
{
    private IQueryable<WorkItem> VisibleRequests(IQueryable<WorkItem> query) =>
        InternalRequestVisibility.Apply(query, db, actor.Id, Now);
}
