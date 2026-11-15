using System.Text.Json;
using HotelOps.Api.Services.Insights;
using Xunit;

public sealed class AuditRedactionTests
{
    [Fact]
    public void AcknowledgmentAuditExposesTypedFactsAndExplicitBeforeNull()
    {
        const string value="{\"notificationId\":42,\"readAt\":\"2026-11-15T17:00:00Z\",\"acknowledgedAt\":null}";
        Assert.Equal(value,JsonSerializer.Serialize(AuditRedaction.Read(value)));
        Assert.Equal(new[]{"acknowledgedAt","readAt"},AuditRedaction.Fields("[\"readAt\",\"acknowledgedAt\",\"message\"]"));
    }
    [Fact]
    public void NotificationWriterUtcOffsetAndFractionalPrecisionRemainVisible()
    {
        const string value="{\"readAt\":\"2026-11-15T17:00:00.1234567+00:00\",\"acknowledgedAt\":\"2026-11-15T17:00:01+00:00\"}";
        using var actual=JsonDocument.Parse(JsonSerializer.Serialize(AuditRedaction.Read(value)));
        Assert.Equal("2026-11-15T17:00:00.1234567+00:00",actual.RootElement.GetProperty("readAt").GetString());
        Assert.Equal("2026-11-15T17:00:01+00:00",actual.RootElement.GetProperty("acknowledgedAt").GetString());
    }
    [Theory]
    [InlineData("\"SYNTHETIC_SECRET\"")]
    [InlineData("42")]
    [InlineData("\"2026-11-15T17:00:00+07:00\"")]
    [InlineData("\"2026-11-15\"")]
    [InlineData("{\"password\":\"SYNTHETIC_SECRET\"}")]
    public void InvalidAcknowledgmentValuesRemainWithheld(string value)
        => Assert.Equal("{}",JsonSerializer.Serialize(AuditRedaction.Read("{\"acknowledgedAt\":"+value+"}")));
    [Theory]
    [InlineData("\"SYNTHETIC_SECRET\"")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    public void InvalidNotificationIdentifiersRemainWithheld(string value)
        => Assert.Equal("{}",JsonSerializer.Serialize(AuditRedaction.Read("{\"notificationId\":"+value+"}")));
    [Fact]
    public void ExplicitBusinessNullIsDifferentFromUnrecordedOrWithheld()
        => Assert.Equal("{\"status\":null}",JsonSerializer.Serialize(AuditRedaction.Read("{\"status\":null,\"password\":null}")));

    [Fact]
    public void RecordedFieldNamesUseSameAllowlistAndNeverRevealSecretKeys()
        => Assert.Equal(new[]{"isActive","status"}, AuditRedaction.Fields("[\"status\",\"password\",\"token\",\"isActive\",\"STATUS\",{},3]"));

    [Theory]
    [InlineData(null)]
    [InlineData("not-json")]
    [InlineData("{\"status\":true}")]
    public void MissingOrMalformedChangedFieldsRemainUnknown(string? value)
        => Assert.Null(AuditRedaction.Fields(value));

    [Theory]
    [InlineData("{\"status\":\"ACTIVE\",\"password\":\"secret\",\"token\":\"secret\"}")]
    [InlineData("{\"status\":\"ACTIVE\",\"unknown\":{\"branchId\":\"secret\"}}")]
    public void KeepsReviewedScalarAndDropsSecrets(string input)
    {
        var json=JsonSerializer.Serialize(AuditRedaction.Read(input));
        Assert.Equal("{\"status\":\"ACTIVE\"}",json);
    }
    [Theory]
    [InlineData("not-json")]
    [InlineData("[\"secret\"]")]
    [InlineData("\"secret\"")]
    [InlineData("{\"status\":{\"password\":\"secret\"}}")]
    public void MalformedOrNestedValuesNeverEscape(string input)
        => Assert.DoesNotContain("secret",JsonSerializer.Serialize(AuditRedaction.Read(input)));
    [Fact]
    public void LargePayloadIsWithheld()
        => Assert.Equal("{\"withheld\":true}",JsonSerializer.Serialize(AuditRedaction.Read(new string('x',1_000_001))));
}
