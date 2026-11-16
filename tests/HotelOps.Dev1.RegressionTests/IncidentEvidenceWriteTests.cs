using System.ComponentModel.DataAnnotations;
using HotelOps.Api.Entities;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Incidents;
using HotelOps.Api.Services.InternalRequests;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentEvidenceWriteTests
{
    private static readonly DateTime Now = new(2026, 11, 16);
    private static PermissionGrantPath Grant => new("INCIDENT_EVIDENCE_ADD", "BRANCH", 3, true, true, Now.AddDays(-1), null, true, true);
    [Fact]
    public void IndependentGrantRequiresCurrentEligibleActorAndRelationship()
    {
        Assert.True(IncidentEvidenceWrite.Allows([new("OPERATIONS_MANAGER", Grant)], Now, 3, false, false));
        Assert.True(IncidentEvidenceWrite.Allows([new("HK_STAFF", Grant with { DataScope = "OWN" })], Now, 3, true, false));
        Assert.True(IncidentEvidenceWrite.Allows([new("MAINTENANCE_STAFF", Grant)], Now, 3, false, true));
        Assert.False(IncidentEvidenceWrite.Allows([new("HK_STAFF", Grant)], Now, 3, false, false));
        Assert.False(IncidentEvidenceWrite.Allows([new("OWNER", Grant)], Now, 3, true, true));
        Assert.False(IncidentEvidenceWrite.Allows([new("MAINTENANCE_LEAD", Grant)], Now, 4, true, true));
        foreach (var grant in new[] { Grant with { PermissionCode = "INCIDENT_VIEW" }, Grant with { PermissionCode = "INCIDENT_MANAGE" },
            Grant with { RoleActive = false }, Grant with { AssignmentActive = false }, Grant with { BranchAccessActive = false },
            Grant with { ExpiresAt = Now }, Grant with { AssignedAt = Now.AddDays(1) } })
            Assert.False(IncidentEvidenceWrite.Allows([new("MAINTENANCE_LEAD", grant)], Now, 3, true, true));
    }
    [Fact]
    public void SourceReadDoesNotBroadenListAndRemovesFormerAssignee()
    {
        var grants = new[] { new IncidentReadAccess.Grant("OPERATIONS_MANAGER", Grant with { PermissionCode = "INCIDENT_VIEW" }) };
        Assert.False(IncidentReadAccess.Resolve(4, 3, grants, Now).Lead);
        Assert.True(IncidentReadAccess.ResolveSource(4, 3, grants, Now).Lead);
        var staff = IncidentReadAccess.ResolveSource(4, 3, [new("HK_STAFF", Grant with { PermissionCode = "INCIDENT_VIEW", DataScope = "OWN" })], Now);
        var incident = new Incident { IncidentId = 1, BranchId = 3, Branch = new() { IsActive = true }, ReportedBy = 9 };
        var work = new MaintenanceWork { AssigneeId = 4, Status = "IN_PROGRESS" }; incident.MaintenanceWorks.Add(work);
        Assert.Single(staff.Apply(new[] { incident }.AsQueryable()));
        work.Status = "COMPLETED"; Assert.Empty(staff.Apply(new[] { incident }.AsQueryable()));
        incident.ReportedBy = 4; Assert.Single(staff.Apply(new[] { incident }.AsQueryable()));
    }
    [Fact]
    public void NoteOnlyAndFileOnlyInputsAreSupportedWithoutAcceptingEmptyOrInvalidFiles()
    {
        Assert.Equal("Evidence", IncidentEvidenceWrite.Validate(new(" Evidence ", null)).Note);
        var file = new HotelOps.Api.DTOs.Housekeeping.EvidenceUpload("test.pdf", "application/pdf", Convert.ToBase64String("%PDF-1.7"u8.ToArray()));
        Assert.Single(IncidentEvidenceWrite.Validate(new(null, [file])).Files!);
        Assert.Throws<ValidationException>(() => IncidentEvidenceWrite.Validate(new(" ", [])));
        Assert.Throws<ValidationException>(() => IncidentEvidenceWrite.Validate(new(new string('x', 4001), [])));
        Assert.Throws<ValidationException>(() => IncidentEvidenceWrite.Validate(new(null, Enumerable.Repeat(file, 6).ToArray())));
        Assert.Throws<InternalRequestReadException>(() => IncidentEvidenceWrite.Validate(new(null, [file with { Name = "../test.pdf" }])));
    }
}
