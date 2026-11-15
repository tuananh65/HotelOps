using System.Text.Json;
using HotelOps.Api.Services.Insights;
using Xunit;

public sealed class AuditRedactionTests
{
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
