using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.Housekeeping;

namespace HotelOps.Api.Services.HotelStructure;

public sealed partial class RoomTypeCreationService
{
    // Not a production API; this is the service adapter for the NonController candidate.
    internal Task<RoomTypeBatchResult> ReadReinspectionReviewCandidateAsync(SessionReference ticket,long sid,
        long branch,long inspection,long rework,string inspectionTag,string reworkTag,CancellationToken ct)
        => BatchAsync(ticket,sid,branch,async(_,token)=>{
            try {
                var source=await ReadReinspectionCandidateInCallerTransactionAsync(ticket,sid,branch,inspection,rework,inspectionTag,reworkTag,token);
                return new(RoomTypeBatchStatus.Success,new ReinspectionReviewDto(source.InspectionId,source.ReworkId,source.OriginalInspectionId,
                    source.AssignedChecklistVersionId,source.OriginalChecklistVersionId,source.InspectionRevision,source.ReworkRevision,
                    source.Defects.Select(d=>new ReinspectionDefectReviewDto(d.DefectId,d.Note,d.RemediatedBy,d.RemediatedAt)).ToArray(),
                    false,false,ReinspectionSourceCandidate.MissingContracts),source.InspectionRevision);
            }
            catch(RejectInspectionOwnershipException) { return new(RoomTypeBatchStatus.Forbidden); }
            catch(RejectInspectionSourceUnavailable) { return new(RoomTypeBatchStatus.Unavailable); }
            catch(InvalidOperationException e) when(ReinspectionReviewErrors.Status(e.Message) is not null) {
                return new(ReinspectionReviewErrors.Status(e.Message)!.Value,new InspectionMutationBlocked(e.Message,ReinspectionSourceCandidate.MissingContracts));
            }
        },ct,requireRoomTypeSchema:false,permissionCode:"INSPECTION_PERFORM");
}

internal static class ReinspectionReviewErrors
{
    internal static RoomTypeBatchStatus? Status(string code)=>code switch {
        "REINSPECTION_REMEDIATION_SCHEMA_REQUIRED"=>RoomTypeBatchStatus.SchemaRequired,
        "REINSPECTION_SOURCE_FORBIDDEN" or "INSPECTOR_CLEANER_CONFLICT"=>RoomTypeBatchStatus.Forbidden,
        "REINSPECTION_SOURCE_NOT_FOUND"=>RoomTypeBatchStatus.NotFound,
        "REINSPECTION_SOURCE_STALE" or "REINSPECTION_REWORK_STALE"=>RoomTypeBatchStatus.Stale,
        "REINSPECTION_IDENTITY_INVALID"=>RoomTypeBatchStatus.InvalidReference,
        "REINSPECTION_CURRENT_SOURCE_REQUIRED" or "REINSPECTION_REWORK_SOURCE_REQUIRED" or "REINSPECTION_REMEDIATION_REQUIRED"=>RoomTypeBatchStatus.Inactive,
        _=>null
    };
}
