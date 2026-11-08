using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using HotelOps.Api.Middleware;
using HotelOps.Api.Security;
using HotelOps.Api.Services.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class CleaningClosureBoundaryTests
{
    private const string Tag = "\"0000000000000001\"";
    private const string Key = "12c44400-7ca4-4da1-8990-8d2fc258f211";
    private static RoomTypeCreationService Service() => new(null!, null!, null!, null!, TimeProvider.System);

    [Theory]
    [InlineData("ON_HOLD", 1, 1, true, "resolved", null)]
    [InlineData("ON_HOLD", 2, 1, true, "resolved", "CURRENT_ASSIGNEE_REQUIRED")]
    [InlineData("ASSIGNED", 1, 1, true, "resolved", "TASK_NOT_ON_HOLD")]
    [InlineData("IN_PROGRESS", 1, 1, true, "resolved", "TASK_NOT_ON_HOLD")]
    [InlineData("PENDING_INSPECTION", 1, 1, true, "resolved", "TASK_NOT_ON_HOLD")]
    [InlineData("ON_HOLD", 1, 1, false, "resolved", "BLOCKER_RESOLUTION_CONFIRMATION_REQUIRED")]
    [InlineData("ON_HOLD", 1, 1, true, null, "BLOCKER_RESOLUTION_NOTE_REQUIRED")]
    [InlineData("ON_HOLD", 1, 1, true, "  ", "BLOCKER_RESOLUTION_NOTE_REQUIRED")]
    public void Resume_requires_owner_state_confirmation_and_note(string state, long actor, long owner, bool confirm, string? note, string? expected)
        => Assert.Equal(expected, CleaningLifecycleRules.ResumeConflict(state, actor, owner, confirm, note));

    [Theory]
    [InlineData("blockers")]
    [InlineData("complete")]
    [InlineData("start")]
    public async Task Unsafe_closure_and_resume_routes_require_CSRF(string route)
    {
        var called = false;
        var middleware = new ApiCsrfMiddleware(_ => { called = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/branches/1/cleaning-tasks/1/" + route;
        context.Request.Method = "POST";
        await middleware.InvokeAsync(context, null!);
        Assert.False(called);
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closure_controller_requires_IfMatch_with_authenticated_ticket(bool complete)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(HotelOpsCookieTicket.SessionIdClaim, "1"),
            new Claim(HotelOpsCookieTicket.CredentialClaim, SessionCredential.Generate()),
            new Claim(HotelOpsCookieTicket.RestrictedClaim, "0"), new Claim(HotelOpsCookieTicket.CreatedClaim, DateTime.UtcNow.Ticks.ToString()),
            new Claim(HotelOpsCookieTicket.VersionClaim, "1") }, HotelOpsCookieTicket.Scheme));
        var controller = new CleaningExecutionController(Service()) { ControllerContext = new() { HttpContext = context } };
        var result = complete ? await controller.Complete(1, 1, new("note"), default)
            : await controller.ReportBlocker(1, 1, new(1, "note"), default);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(428, problem.Status);
        Assert.Equal("IF_MATCH_REQUIRED", problem.Title);
    }

    [Theory]
    [InlineData(0, 1, Tag, Key, "note")]
    [InlineData(1, 0, Tag, Key, "note")]
    [InlineData(1, 1, "*", Key, "note")]
    [InlineData(1, 1, "W/\"0000000000000001\"", Key, "note")]
    [InlineData(1, 1, Tag, "", "note")]
    [InlineData(1, 1, Tag, "00000000-0000-0000-0000-000000000000", "note")]
    [InlineData(1, 1, Tag, Key, " ")]
    [InlineData(1, 1, Tag, Key, "bad\nnote")]
    [InlineData(1, 1, Tag, Key, null)]
    public async Task Closure_rejects_invalid_envelope_before_persistence(long branch, long task, string tag, string key, string? note)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service().ReportCleaningBlockerAsync(null!, 0,
            branch, task, tag, key, new(1, note!), default));
        await Assert.ThrowsAsync<ValidationException>(() => Service().CompleteCleaningAsync(null!, 0,
            branch, task, tag, key, new(note!), default));
    }

    [Fact]
    public async Task Closure_rejects_oversized_note_before_persistence()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Service().ReportCleaningBlockerAsync(null!, 0,
            1, 1, Tag, Key, new(1, new string('x', 501)), default));
        await Assert.ThrowsAsync<ValidationException>(() => Service().CompleteCleaningAsync(null!, 0,
            1, 1, Tag, Key, new(new string('x', 501)), default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Report_requires_positive_reference_before_persistence(long reference)
        => await Assert.ThrowsAsync<ValidationException>(() => Service().ReportCleaningBlockerAsync(null!, 0,
            1, 1, Tag, Key, new(reference, "note"), default));

    [Theory]
    [InlineData("actorId")]
    [InlineData("reportedAt")]
    [InlineData("resolvedAt")]
    [InlineData("state")]
    [InlineData("assignmentId")]
    [InlineData("evidenceIds")]
    [InlineData("incidentId")]
    [InlineData("materialRequestId")]
    public void Closure_rejects_unsupported_or_server_owned_fields(string field)
    {
        var json = "{\"ReasonId\":1,\"Note\":\"note\",\"" + field + "\":1}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ReportCleaningBlockerRequest>(json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CompleteCleaningRequest>(
            "{\"Note\":\"note\",\"" + field + "\":1}"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closure_controller_rejects_insecure_transport_before_service(bool complete)
    {
        var controller = new CleaningExecutionController(Service())
        {
            ControllerContext = new() { HttpContext = new DefaultHttpContext() }
        };
        var result = complete ? await controller.Complete(1, 1, new("note"), default)
            : await controller.ReportBlocker(1, 1, new(1, "note"), default);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(400, problem.Status);
        Assert.Equal("HTTPS_REQUIRED", problem.Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closure_controller_rejects_missing_session_before_service(bool complete)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var controller = new CleaningExecutionController(Service()) { ControllerContext = new() { HttpContext = context } };
        var result = complete ? await controller.Complete(1, 1, new("note"), default)
            : await controller.ReportBlocker(1, 1, new(1, "note"), default);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(401, problem.Status);
        Assert.Equal("SESSION_INVALID", problem.Title);
    }

    [Fact]
    public void Metadata_cannot_advertise_closure_without_contracts()
    {
        var metadata = new CleaningExecutionDto(1, 1, "CL-1", "IN_PROGRESS", 1, 1,
            DateTime.UtcNow, Tag, "STATE_CONFLICT", "PROVISIONAL_NOT_ACCEPTED");
        Assert.Equal("UNAVAILABLE", metadata.ReportBlockerCapability);
        Assert.Equal("UNAVAILABLE", metadata.CompleteCapability);
    }
}
