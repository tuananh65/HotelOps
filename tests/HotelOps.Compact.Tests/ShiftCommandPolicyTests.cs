using HotelOps.Api.Compact.Security;
using Xunit;

namespace HotelOps.Compact.Tests;

public class ShiftCommandPolicyTests
{
    [Theory]
    [InlineData("GET","/api/v2/work/12",false)]
    [InlineData("POST","/api/v2/work/12/actions/complete",true)]
    [InlineData("PUT","/api/v2/work/12/checklist/2",true)]
    [InlineData("POST","/api/v2/documents/12/actions/post",true)]
    [InlineData("POST","/api/v2/auth/login",false)]
    [InlineData("POST","/api/v2/notifications/12/read",false)]
    [InlineData("POST","/api/v2/workday/assignments/12/end",false)]
    [InlineData("PUT","/api/v2/work/room-issue-types",false)]
    [InlineData("get","/api/v2/work/12",false)]
    [InlineData("PUT","/API/v2/inventory-lifecycle/batches/12",true)]
    [InlineData("POST","/api/v2/reusable-inventory/positions/12/move",true)]
    [InlineData("POST","/api/v2/evidence/12/invalidate",true)]
    [InlineData("POST","/api/v2/work/internal-request-types",false)]
    [InlineData("PUT","/api/v2/work/internal-request-types/12",false)]
    [InlineData("POST","/api/v2/work/12/actions/custom-types",true)]
    [InlineData("PUT","/api/v2/documents/12/receipt-draft",true)]
    [InlineData("POST","/api/v2/documents/12/stocktake-items",true)]
    public void OnlyBusinessWritesRequireShift(string method,string path,bool expected) =>
        Assert.Equal(expected,ShiftCommandAccess.IsBusinessCommand(method,path));

    [Theory]
    [InlineData("2026-11-25T14:59:59Z",false)]
    [InlineData("2026-11-25T15:00:00Z",true)]
    [InlineData("2026-11-25T18:00:00Z",true)]
    [InlineData("2026-11-25T23:00:00Z",false)]
    public void OvernightShiftUsesAssignmentDateAndBranchTimezone(string timestamp,bool expected) =>
        Assert.Equal(expected,ShiftCommandAccess.Covers(new DateOnly(2026,11,25),22*60,6*60,"SE Asia Standard Time",DateTime.Parse(timestamp).ToUniversalTime()));
}
