using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HotelOps.Api.DTOs.HotelStructure;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeactivateStructureRequest(
    [Required, StringLength(1000, MinimumLength = 10)] string Reason,
    bool Confirmation);

public sealed record StructureDependencyDto(string Kind, long Count, IReadOnlyList<long> Ids);

// Current read snapshot only; never an authorization or mutation-readiness certificate.
public sealed record StructureDeactivationPreviewDto(EditableStructureDto Record,
    IReadOnlyList<StructureDependencyDto> Dependencies, bool WriterCoordinationRequired);
