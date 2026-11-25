using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.Compact.Contracts;
public sealed record IncidentInvestigationRequest([Required]string RowVersion,Guid OperationKey,
    [Required]string Cause,[Required,StringLength(2000)]string Note,long? RelatedUserId=null);
