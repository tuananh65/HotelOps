using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.Services.Authentication;
using Microsoft.EntityFrameworkCore;
namespace HotelOps.Api.Services.HotelStructure;
public sealed partial class RoomTypeCreationService
{
    public static IReadOnlyList<string> RoomLifecycleReleaseGates {get;}=Array.AsReadOnly(new[]{
        "CLEANING_REWORK_INSPECTION_WRITERS","INCIDENT_ROOMBLOCK_ASSET_WRITERS","STATUS_ENGINE_MAPPING_HISTORY_AUDIT","SQL_RACE_ROLLBACK_ACCEPTANCE"});
    /// <summary>Direct service calls remain fail-closed. No configurable/injected clear evaluator.</summary>
    public async Task<RoomTypeBatchResult> ChangeRoomLifecycleCandidateAsync(SessionReference ticket,long sessionId,long branchId,long roomId,
        bool reactivate,string expected,RoomLifecycleRequest request,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);ArgumentNullException.ThrowIfNull(request);
        _=request.ValidatedReason();Batch2Tag(expected);
        if(branchId<=0||roomId<=0)throw new ValidationException("Positive scoped room identity required.");
        ct.ThrowIfCancellationRequested();
        if(sessionId<=0||!current.TryGetCurrentUser(out var actor)||actor.UserId!=ticket.UserId||actor.SessionId!=sessionId||actor.IsRestricted||ticket.IsRestricted)return new(RoomTypeBatchStatus.SessionInvalid);
        // Retain BOTH operations closed even with no known references. No SQL before release.
        if(RoomLifecycleReleaseGates.Count!=0)return new(RoomTypeBatchStatus.Unavailable);
        return await BatchAsync(ticket,sessionId,branchId,async(_,token)=>{
            // Reuse existing User-first/Branch/catalog transaction and server authority only.
            var room=await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r=>r.RoomId==roomId&&r.BranchId==branchId,token);
            if(room==null)return new(RoomTypeBatchStatus.NotFound);
            // Complete dependency census/final rereads and status engine are deliberately unresolved.
            // Do not invent a predicate, copy a caller's clear result or persist an unapproved SQL status.
            return new(RoomTypeBatchStatus.Unavailable);
        },ct);
    }
}
