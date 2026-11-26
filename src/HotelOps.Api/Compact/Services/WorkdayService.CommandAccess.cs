using HotelOps.Api.Compact.Security;

namespace HotelOps.Api.Compact.Services;

public sealed partial class WorkdayService
{
    public async Task<object> CommandAccess(long branchId,CancellationToken ct)
    {
        await access.Eligible(actor.Id,branchId,ct);
        var canOperate=await ShiftCommandAccess.CanOperate(db,actor.Id,branchId,Now,ct);
        return new {canOperate,reason=canOperate?null:"ACTIVE_SHIFT_REQUIRED",checkedAt=Now};
    }
}
