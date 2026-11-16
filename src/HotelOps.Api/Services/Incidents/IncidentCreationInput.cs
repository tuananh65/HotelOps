using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Incidents;

namespace HotelOps.Api.Services.Incidents;

internal static class IncidentCreationInput
{
    internal static void Validate(CreateIncidentRequest input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        if (input.BranchId <= 0 || input.ReportedBy <= 0)
            throw new ValidationException("Branch and reporter are required.");
        // GBR-066: an incident must be locatable. The database-backed target
        // checks still establish branch ownership; an ID alone is not proof.
        if (input.RoomId is null && input.AreaId is null && input.AssetId is null)
            throw new ValidationException("Select at least one room, area or asset.");
        if (input.RoomId is <= 0 || input.AreaId is <= 0 || input.AssetId is <= 0)
            throw new ValidationException("Room, area and asset identifiers must be positive when supplied.");
        IncidentRequestCreation.Validate(input);
    }
}
