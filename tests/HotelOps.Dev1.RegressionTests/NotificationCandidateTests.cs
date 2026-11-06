using HotelOps.Api.Data;
using HotelOps.Api.Models;
using HotelOps.Api.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;
namespace HotelOps.Dev1.RegressionTests;

public sealed class NotificationCandidateTests
{
    [Fact]
    public void Additive_candidate_model_has_actual_keys_nullability_and_rowversions_without_changing_generated_inbox_key()
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=unused.invalid;Database=NoConnection;Integrated Security=true").Options);
        var op = db.Model.FindEntityType(typeof(NotificationOperationCandidate))!;
        Assert.Equal(2, op.FindPrimaryKey()!.Properties.Count); Assert.True(op.FindProperty("RowVersion")!.IsConcurrencyToken);
        var intent = db.Model.FindEntityType(typeof(NotificationIntentCandidate))!;
        Assert.True(intent.FindProperty("InboxNotificationId")!.IsNullable); Assert.True(intent.FindProperty("RowVersion")!.IsConcurrencyToken);
        Assert.Contains(intent.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(CandidateInboxLink) && x.Properties.Count == 3);
        var old = db.Model.FindEntityType(typeof(HotelOps.Api.Entities.NotificationRecipient))!;
        Assert.Single(old.FindPrimaryKey()!.Properties); Assert.Equal("RecipientId", old.FindPrimaryKey()!.Properties[0].Name);
    }
    [Theory]
    [InlineData("Production", "FS009_PROVISIONAL")]
    [InlineData("Development", null)]
    public async Task Candidate_fails_closed_without_development_optin_and_never_contacts_sql(string env, string? flag)
    {
        using var db = new HotelOpsDbContext(new DbContextOptionsBuilder<HotelOpsDbContext>().UseSqlServer("Server=unused.invalid;Database=NoConnection;Integrated Security=true").Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "CleaningAssignment:Candidate", flag } }).Build();
        var candidate = new ProvisionalCleaningNotification(db, config, new Env { EnvironmentName = env }); Assert.False(await candidate.AvailableAsync(default));
    }
    private sealed class Env : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production"; public string ApplicationName { get; set; } = "Tests"; public string ContentRootPath { get; set; } = ""; public string WebRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider(); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
