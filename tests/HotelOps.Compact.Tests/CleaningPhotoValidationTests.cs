using System.Reflection;
using HotelOps.Api.Compact.Infrastructure;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public class CleaningPhotoValidationTests
{
    private static readonly MethodInfo Validate=typeof(EvidenceService).GetMethod("ValidateCleaningImage",BindingFlags.Static|BindingFlags.NonPublic)!;
    private static readonly MethodInfo Contains=typeof(EvidenceService).GetMethod("ReportContainsPhoto",BindingFlags.Static|BindingFlags.NonPublic)!;

    [Fact]
    public void ValidPngCanBeUsedAsCompletionEvidence() => Validate.Invoke(null,[Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNomLAAAANEAbF61zYUAAAAAElFTkSuQmCC"),"image/png"]);

    [Fact]
    public void ValidJpegCanBeUsedAsCompletionEvidence() => Validate.Invoke(null,[Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwCxRRRXYch//9k="),"image/jpeg"]);

    [Theory]
    [InlineData("iVBORw0KGgo=","image/png")]
    [InlineData("/9j/2Q==","image/jpeg")]
    [InlineData("JVBERi0xLjQ=","application/pdf")]
    [InlineData("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAAF0lEQVRpbnZhbGlkIHpsaWIgcGl4ZWwgZGF0YUjewn4AAAAASUVORK5CYII=","image/png")]
    public void HeaderOnlyPdfAndCorruptPixelPayloadAreRejected(string encoded,string mime)
    {
        var error=Assert.Throws<TargetInvocationException>(()=>Validate.Invoke(null,[Convert.FromBase64String(encoded),mime]));
        Assert.IsType<ApiException>(error.InnerException);
    }

    [Fact]
    public void MimeMustMatchActualImage()
    {
        var error=Assert.Throws<TargetInvocationException>(()=>Validate.Invoke(null,[Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGNomLAAAANEAbF61zYUAAAAAElFTkSuQmCC"),"image/jpeg"]));
        Assert.IsType<ApiException>(error.InnerException);
    }

    [Theory]
    [InlineData("{\"attachmentIds\":[10,20]}",10,true)]
    [InlineData("{\"attachmentIds\":[10,20]}",1,false)]
    [InlineData("{\"workId\":10}",10,false)]
    public void SubmittedPhotoProtectionUsesExactReportIds(string json,long id,bool expected) =>
        Assert.Equal(expected,(bool)Contains.Invoke(null,[json,id])!);
}
