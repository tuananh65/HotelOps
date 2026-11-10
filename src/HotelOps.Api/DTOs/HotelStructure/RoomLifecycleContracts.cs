using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.HotelStructure;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RoomLifecycleRequest([Required,StringLength(1000,MinimumLength=10)] string Reason,bool Confirmation)
{
    public string ValidatedReason(){var value=Reason?.Trim();if(!Confirmation||string.IsNullOrEmpty(value)||value.Length is <10 or >1000||value.Any(char.IsControl))throw new ValidationException("Confirmation and a reason of10-1000 characters are required.");return value;}
}
// Domain intent only; these are NOT SQL operational_status values or a write authorization.
public enum RoomLifecycleCondition { Inactive, PendingCleaning }
public sealed record RoomLifecyclePlan(long RoomId,long BranchId,bool BeforeActive,bool AfterActive,
    RoomLifecycleCondition RequiredCondition,bool RequiresStatusHistory=true,bool RequiresAtomicAudit=true);
public static class RoomLifecycleRules
{
    public static RoomLifecyclePlan Plan(CreatedRoomDto room,bool reactivate,bool branchActive,bool floorActive,bool typeActive,bool areaEligible)
    {
        if(room.Id<=0||room.BranchId<=0)throw new ValidationException("Invalid room identity.");
        if(room.IsActive==reactivate)throw new ValidationException("Invalid room lifecycle transition.");
        if(reactivate&&(!branchActive||!floorActive||!typeActive||!areaEligible))throw new ValidationException("Active eligible room parents required.");
        return new(room.Id,room.BranchId,room.IsActive,reactivate,reactivate?RoomLifecycleCondition.PendingCleaning:RoomLifecycleCondition.Inactive);
    }
}
