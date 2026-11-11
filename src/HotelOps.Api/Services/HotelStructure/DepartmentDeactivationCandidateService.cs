using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;
public sealed partial class RoomTypeCreationService
{
    public static IReadOnlyList<string> DepartmentDeactivationReleaseGates {get;}=Array.AsReadOnly(new[]{"ACTIVE_USER_BINDING_WRITERS","WAREHOUSE_TASK_REQUEST_CUSTODY_COVERAGE","CROSS_MODULE_LOCK_ORDER","ATOMIC_AUDIT_SQL_ACCEPTANCE"});
    /// <summary>Candidate only; visible directory accounts/empty diagnostics never certify complete clearance.</summary>
    public async Task<RoomTypeBatchResult> DeactivateDepartmentCandidateAsync(SessionReference ticket,long sessionId,long branchId,long id,string expected,DeactivateStructureRequest request,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);ArgumentNullException.ThrowIfNull(request);
        if(!request.Confirmation)throw new ValidationException("Confirmation required.");
        _=Batch2Reason(request.Reason);Batch2Tag(expected);
        if(branchId<=0||id<=0)throw new ValidationException("Positive branch and department required.");
        ct.ThrowIfCancellationRequested();
        if(sessionId<=0||!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sessionId||actor.IsRestricted||ticket.IsRestricted)return new(RoomTypeBatchStatus.SessionInvalid);
        if(DepartmentDeactivationReleaseGates.Count!=0)return new(RoomTypeBatchStatus.Unavailable);
        return await BatchAsync(ticket,sessionId,branchId,async(_,token)=>{
            var row=await db.Departments.AsNoTracking().SingleOrDefaultAsync(d=>d.DepartmentId==id&&d.BranchId==branchId,token);
            if(row==null)return new(RoomTypeBatchStatus.NotFound);
            if(!row.IsActive)return new(RoomTypeBatchStatus.Inactive);
            if(await DepartmentTag(DepartmentDto(row),token)!=expected)return new(RoomTypeBatchStatus.Stale);
            // No guessed dependency SQL, auto-transfer, delete or successful write before owner acceptance.
            return new(RoomTypeBatchStatus.Unavailable);
        },ct,requireRoomTypeSchema:false);
    }
}
