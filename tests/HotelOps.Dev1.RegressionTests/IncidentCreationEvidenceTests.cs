using HotelOps.Api.Data;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Services.Incidents;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class IncidentCreationEvidenceTests
{
    [Fact]
    public async Task StagingUsesPrivateIncidentAttachmentAndRollbackRemovesOwnedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var bytes = "%PDF-synthetic"u8.ToArray();
        string? path = null;
        try
        {
            using (var files = new EvidenceFileLifetime())
            {
                await IncidentEvidenceWrite.StageFilesAsync(db, 17, 42, new DateTime(2026, 11, 16),
                    [new EvidenceUpload("report.pdf", "application/pdf", Convert.ToBase64String(bytes))], root, files, default);
                var row = Assert.Single(db.Attachments.Local);
                Assert.Equal("INCIDENT", row.EntityType);
                Assert.Equal(17, row.EntityId);
                Assert.Equal(42, row.UploadedBy);
                Assert.Null(row.FileUrl);
                Assert.Equal("report.pdf", row.OriginalFileName);
                path = Assert.Single(files.Files);
                Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            }
            Assert.False(File.Exists(path));
        }
        finally { if (path != null) File.Delete(path); if (Directory.Exists(root)) Directory.Delete(root); }
    }
}
