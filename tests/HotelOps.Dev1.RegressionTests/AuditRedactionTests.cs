using System.Text.Json;
using HotelOps.Api.Services.Insights;
using Xunit;

public sealed class AuditRedactionTests
{
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
