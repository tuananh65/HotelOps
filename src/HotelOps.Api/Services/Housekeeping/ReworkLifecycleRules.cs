using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;

namespace HotelOps.Api.Services.Housekeeping;

public static class ReworkLifecycleRules
{
    // UC035 and UC036. Reinspection alone owns the final COMPLETED/FAILED conclusion.
    public static string? Next(string? state, string action) => (state, action) switch
    {
        ("PENDING_ACCEPTANCE", "accept") => "ACCEPTED",
        ("ACCEPTED", "start") => "IN_PROGRESS",
        ("IN_PROGRESS", "complete") => "PENDING_REINSPECTION",
        _ => null
    };

    public static void ValidateRemediations(IReadOnlyList<long> expected, IReadOnlyList<ReworkRemediation>? input)
    {
        if (expected.Count is < 1 or > 200 || input == null || input.Count != expected.Count
            || input.Any(x => x == null) || input.Select(x => x.DefectId).Distinct().Count() != input.Count
            || !expected.Order().SequenceEqual(input.Select(x => x.DefectId).Order()))
            throw new ValidationException("Submit exactly one remediation for every linked defect.");
        if (input.Sum(x => x.Photos?.Count ?? 0) > 5 || input.Any(x => x.Photos?.Any(p => p == null) == true))
            throw new ValidationException("At most five evidence files are allowed.");
        foreach (var row in input) CleaningLifecycleRules.Reason(row.Note, 1);
    }
}
