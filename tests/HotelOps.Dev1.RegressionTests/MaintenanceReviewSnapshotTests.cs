using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.DTOs.Maintenance;
using HotelOps.Api.Entities;
using HotelOps.Api.Services.Maintenance;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class MaintenanceReviewSnapshotTests
{
    [Fact]
    public void Review_requires_explicit_material_and_evidence_snapshots_not_legacy_note()
    {
        var at = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var values = new Dictionary<string,object?> { ["status"]="COMPLETED",["notification_kind"]="MAINTENANCE_SUBMITTED",
            ["notification_version"]=1,["maintenance_work_id"]=7,["incident_id"]=8,["assignee_id"]=9,["resolved_at"]=at,
            ["submitted_diagnosis"]="Worn seal",["submitted_action_taken"]="Replaced seal",["submitted_materials"]=Array.Empty<object>(),
            ["submitted_evidence"]=new[] {new {AttachmentId=12}} };
        var audit = new AuditLog {AuditId=1,EntityType="maintenance_works",EntityId="7",Action="UPDATE",BranchId=2,ActorUserId=9,
            OccurredAt=at,OldValues="{\"status\":\"IN_PROGRESS\"}"};
        MaintenanceReviewDto? Read(){audit.NewValues=JsonSerializer.Serialize(values);return MaintenanceReviewSnapshot.Parse(audit);}
        var review=Assert.IsType<MaintenanceReviewDto>(Read());
        Assert.Equal(1,review.SubmissionAuditId);Assert.Equal("Worn seal",review.Diagnosis);
        Assert.Equal(new long[]{12},review.ResultAttachmentIds);
        values["submitted_evidence"]=new[] {new {AttachmentId=12},new {AttachmentId=12}};Assert.Null(Read());
        values["submitted_evidence"]=Array.Empty<object>();Assert.NotNull(Read());
        values.Remove("submitted_materials");Assert.Null(Read());
    }

    [Fact]
    public void Verify_requires_the_reviewed_submission_identity()
    {
        var request = new VerifyMaintenanceWorkRequest { ActorUserId=10,Note="Verified repair result" };
        Assert.Throws<ValidationException>(()=>Validator.ValidateObject(request,new ValidationContext(request),true));
        request = new VerifyMaintenanceWorkRequest { ActorUserId=10,Note="Verified repair result",SubmissionAuditId=7 };
        Validator.ValidateObject(request,new ValidationContext(request),true);
    }
}
