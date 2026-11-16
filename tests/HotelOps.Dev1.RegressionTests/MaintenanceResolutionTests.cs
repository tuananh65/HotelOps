using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Services.Maintenance;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceResolutionTests
{
    [Fact]
    public void Evidence_selection_rejects_invalid_or_duplicate_ids_and_normalizes_order()
    {
        Assert.Empty(MaintenanceResolution.EvidenceIds(null));
        Assert.Equal(new long[] { 3, 8 }, MaintenanceResolution.EvidenceIds([8, 3]));
        foreach (var ids in new long[][] { [0], [-1], [3, 3] })
            Assert.Throws<ValidationException>(() => MaintenanceResolution.Note(new()
            { Diagnosis = "Worn seal", ActionTaken = "Replaced seal", ResultAttachmentIds = ids }));
    }

    [Theory]
    [InlineData("", "Replaced seal")]
    [InlineData(" \r\n", "Replaced seal")]
    [InlineData("Worn seal", "")]
    [InlineData("Worn seal", "\t ")]
    public void Legacy_note_cannot_substitute_for_required_result_fields(string diagnosis, string action)
        => Assert.Throws<ValidationException>(() => MaintenanceResolution.Note(new()
        { Diagnosis = diagnosis, ActionTaken = action, ResolutionNote = "Historical free text" }));

    [Fact]
    public void Result_preserves_distinct_normalized_fields_and_optional_notes()
    {
        var result = MaintenanceResolution.Note(new SubmitMaintenanceResultRequest
        { Diagnosis = " Worn seal ", ActionTaken = " Replaced seal\nChecked pressure ", ResolutionNote = " No leakage " });
        Assert.Equal("Diagnosis/root cause:\nWorn seal\n\nAction taken:\nReplaced seal\nChecked pressure\n\nAdditional result notes:\nNo leakage", result);
        Assert.DoesNotContain("Additional result notes", MaintenanceResolution.Note(new()
        { Diagnosis = "Worn seal", ActionTaken = "Replaced seal", ResolutionNote = " \t " }));
    }
}
