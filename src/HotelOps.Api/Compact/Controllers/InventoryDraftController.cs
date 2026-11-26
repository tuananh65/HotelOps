using HotelOps.Api.Compact.Contracts;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelOps.Api.Compact.Controllers;

[ApiController, Authorize, Route("api/v2/documents")]
public sealed class InventoryDraftController(DocumentService service) : ControllerBase
{
    [HttpPut("{id:long}/receipt-draft")]
    public Task<object> Receipt(long id, ReceiptDraftRequest request, CancellationToken ct) => service.UpdateReceiptDraft(id, request, ct);

    [HttpPost("{id:long}/stocktake-items")]
    public Task<object> Stocktake(long id, StocktakeItemsRequest request, CancellationToken ct) => service.AddStocktakeItems(id, request, ct);
}
