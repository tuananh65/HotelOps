using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Compact.Controllers;

[ApiController, Authorize, Route("api/v2/documents/staff-issues")]
public sealed class StaffIssuesController(DocumentService service) : ControllerBase
{
    [HttpGet]
    public Task<object> List(long branchId, CancellationToken ct, int page = 1, int pageSize = 25,
        string? search = null, DateTime? from = null, DateTime? to = null)
        => service.StaffIssues(branchId, page, pageSize, search, from, to, ct);

    [HttpGet("documents")]
    public Task<object> Documents(long branchId, CancellationToken ct, int page = 1, int pageSize = 25,
        string? search = null, DateTime? from = null, DateTime? to = null, string sort = "latest", string? status = null)
        => service.StaffIssueDocuments(branchId, page, pageSize, search, from, to, sort, status, ct);

    [HttpGet("documents/{key}")]
    public Task<object> Detail(string key, long branchId, CancellationToken ct)
        => service.StaffIssueDocument(branchId, key, ct);
}
