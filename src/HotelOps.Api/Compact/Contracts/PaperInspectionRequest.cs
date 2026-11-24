using System.ComponentModel.DataAnnotations;

namespace HotelOps.Api.Compact.Contracts;

public sealed record PaperInspectionRequest(
    [Required] string RowVersion, Guid OperationKey,
    [Required, RegularExpression("^(PASS|FAIL)$")] string Result,
    [StringLength(2000)] string? Note, bool ResolveFindings = false,
    string? InspectionRowVersion = null);
