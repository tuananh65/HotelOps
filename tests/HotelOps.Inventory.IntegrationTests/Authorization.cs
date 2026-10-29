using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HotelOps.Api.DTOs.Inventory;
using Microsoft.EntityFrameworkCore;

sealed partial class InventorySuite
{
    readonly InventoryAuthenticatedClient setupHttp = Account("SETUP"), omHttp = Account("OM"), approverHttp = Account("APPROVER"), deniedHttp = Account("DENIED"), foreignHttp = Account("FOREIGN");
    readonly ConditionalWeakTable<InventoryCommand, ActorChoice> commandActors = new();
    sealed record ActorChoice(long Id);
    static InventoryAuthenticatedClient Account(string name) => new("HOTEL_OPS_INV_" + name)
    { BaseAddress = new("https://localhost:5188"), Timeout = TimeSpan.FromSeconds(60) };
    readonly InventoryAuthenticatedClient? reversalPosterHttp = OptionalReversalPoster();
    static InventoryAuthenticatedClient? OptionalReversalPoster()
    {
        var username = Environment.GetEnvironmentVariable("HOTEL_OPS_INV_REVERSE_POSTER_USERNAME");
        var password = Environment.GetEnvironmentVariable("HOTEL_OPS_INV_REVERSE_POSTER_PASSWORD");
        return string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password) ? null : Account("REVERSE_POSTER");
    }
    InventoryAuthenticatedClient[] Clients() => reversalPosterHttp is null
        ? [http, setupHttp, omHttp, approverHttp, deniedHttp, foreignHttp]
        : [http, setupHttp, omHttp, approverHttp, deniedHttp, foreignHttp, reversalPosterHttp];
    InventoryCommand SelectActor(InventoryCommand command, long? who)
    {
        // Test-only routing to a real authenticated session; never serialized.
        if (who is long id) commandActors.Add(command, new(id));
        return command;
    }
    async Task LoginIdentities()
    {
        foreach (var client in Clients()) await client.LoginAsync();
        Check(Clients().Select(x => x.UserId).Distinct().Count() == Clients().Length, "Supply distinct real fixture accounts.");
        if (reversalPosterHttp is null)
            Console.WriteLine("PREREQUISITE: independent HTTP STOCK_REVERSE poster not supplied. Positive reversal posting uses trusted service calls only; HTTP reversal acceptance remains unverified.");
        actor = http.UserId; other = approverHttp.UserId;
    }
    HttpClient ClientFor(string method, string path, object? body)
    {
        if (body is InventoryCommand command && commandActors.TryGetValue(command, out var choice)) return Clients().Single(x => x.UserId == choice.Id);
        if (method == "GET") return http;
        if (path.Contains("/approve-adjustment", StringComparison.Ordinal)) return approverHttp;
        if (path.StartsWith("inventory/adjustments/", StringComparison.Ordinal) && path.EndsWith("/post", StringComparison.Ordinal)) return approverHttp;
        if (path.StartsWith("inventory/reversals/", StringComparison.Ordinal) && path.EndsWith("/post", StringComparison.Ordinal))
            return reversalPosterHttp ?? throw new InvalidOperationException("Independent STOCK_REVERSE HTTP poster fixture is required.");
        if (path == "inventory/adjustments" || path.EndsWith("/reverse", StringComparison.Ordinal)) return omHttp;
        if (body is InventoryItemWrite or InventoryUnitWrite or InventoryConversionWrite or InventoryWarehouseWrite or InventoryThresholdWrite) return setupHttp;
        return http;
    }
    async Task<int> As(InventoryAuthenticatedClient client, string method, string path, object? input = null)
    {
        using var message = new HttpRequestMessage(new(method), "/api/inventory/" + path);
        if (input is not null) message.Content = JsonContent.Create(input);
        using var response = await client.SendAsync(message);
        if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength != 0)
        {
            using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (data.RootElement.TryGetProperty("documentId", out var document)) documents.Add(document.GetInt64());
            if (data.RootElement.TryGetProperty("stocktakeId", out var take)) stocktakes.Add(take.GetInt64());
            if (data.RootElement.TryGetProperty("adjustmentDocumentId", out var adjustment) && adjustment.ValueKind == JsonValueKind.Number) documents.Add(adjustment.GetInt64());
        }
        return (int)response.StatusCode;
    }
    async Task AuthorizationCases()
    {
        using var anonymous = new HttpClient(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = false }) { BaseAddress = http.BaseAddress };
        using var response = await anonymous.GetAsync("/api/inventory/items");
        Check((int)response.StatusCode == 401, "Anonymous Inventory GET must be 401.");
        using var bootstrap = await anonymous.GetAsync("/api/auth/csrf"); bootstrap.EnsureSuccessStatusCode();
        var token = await bootstrap.Content.ReadFromJsonAsync<JsonElement>();
        using var write = new HttpRequestMessage(HttpMethod.Post, "/api/inventory/units") { Content = JsonContent.Create(new InventoryUnitWrite(prefix + "ANON", prefix)) };
        write.Headers.Add(token.GetProperty("headerName").GetString()!, token.GetProperty("csrfToken").GetString()!);
        using var anonymousWrite = await anonymous.SendAsync(write);
        Check((int)anonymousWrite.StatusCode == 401, "Anonymous write with genuine CSRF must be 401.");
        Check(await As(deniedHttp, "GET", "warehouses") == 403, "No permission must deny reads.");
        Check(await As(http, "POST", "units", new InventoryUnitWrite(prefix + "DENY", prefix)) == 403, "Operations grant must not authorize global master mutations.");
        Check(await As(foreignHttp, "GET", $"warehouses/{source.WarehouseId}") == 403, "Foreign warehouse read must be denied.");
        Check(await As(foreignHttp, "PUT", $"warehouses/{source.WarehouseId}/items/{item.ItemId}/threshold", new InventoryThresholdWrite(1)) == 403, "Foreign master branch mutation denied.");
        Check(await As(deniedHttp, "POST", "adjustments", new InventoryAdjustment(source.WarehouseId, DateOnly.FromDateTime(DateTime.UtcNow), prefix, [new(item.ItemId, 1)])) == 403, "Missing adjustment permission denied.");
        var spoof = JsonSerializer.SerializeToNode(Draft(source.WarehouseId, 1))!.AsObject(); spoof["actorUserId"] = omHttp.UserId;
        var receipt = await Send<InventoryDocumentDto>("POST", "inventory/receipts", spoof, 201);
        await using var db = Db();
        Check(await db.StockDocuments.Where(x => x.DocumentId == receipt.DocumentId).Select(x => x.CreatedBy).SingleAsync() == actor, "Body spoofing cannot change persisted caller.");
        Check(await As(foreignHttp, "GET", $"documents/{receipt.DocumentId}") == 403 && await As(foreignHttp, "GET", $"documents/{receipt.DocumentId}/history") == 403, "Foreign document/history denied.");
        var foreignPage = await foreignHttp.GetFromJsonAsync<InventoryPaged<InventoryWarehouseDto>>("/api/inventory/warehouses?pageSize=1");
        Check(foreignPage!.Items.All(x => x.BranchId != branch), "Warehouse scope must apply before paging.");
        Check(await As(foreignHttp, "POST", "receipts", Draft(source.WarehouseId, 1)) == 403, "Foreign receipt scope denied.");
        Check(await As(foreignHttp, "POST", "returns", Draft(source.WarehouseId, 1)) == 403, "Foreign return scope denied.");
        var foreignBranch = await db.Users.Where(x => x.UserId == foreignHttp.UserId).Select(x => x.PrimaryBranchId).SingleAsync() ?? throw new Exception("FOREIGN needs a separate primary branch.");
        var foreignDepartment = await db.Departments.Where(x => x.BranchId == foreignBranch && x.IsActive).Select(x => x.DepartmentId).FirstAsync();
        var cross = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(foreignBranch, foreignDepartment, prefix + "AUTHX", prefix), 201);
        var authWarehouse = await Send<InventoryWarehouseDto>("POST", "inventory/warehouses", new InventoryWarehouseWrite(branch, department, prefix + "AUTHS", prefix), 201);
        var seeded = await Send<InventoryDocumentDto>("POST", "inventory/receipts", Draft(authWarehouse.WarehouseId, 2), 201);
        seeded = await PostReceipt(seeded, 2);
        Check(await db.StockDocuments.Where(x => x.DocumentId == seeded.DocumentId).Select(x => x.PostedBy).SingleAsync() == actor,
            "Receipt poster must be authenticated storekeeper.");
        Check(await db.StockMovements.Where(x => x.DocumentLine.DocumentId == seeded.DocumentId).AllAsync(x => x.CreatedBy == actor), "Movement actor must be authenticated poster.");
        Check(await db.AuditLogs.AnyAsync(x => x.EntityType == "units_of_measure" && x.EntityId == unit.UomId.ToString() && x.ActorUserId == setupHttp.UserId), "Master audit must identify SETUP.");
        var transferBody = Draft(cross.WarehouseId, 1) with { SourceWarehouseId = authWarehouse.WarehouseId };
        Check(await As(foreignHttp, "POST", "transfers", transferBody) == 403, "Destination-only access cannot create a transfer from another branch.");
        var transfer = await Send<InventoryDocumentDto>("POST", "inventory/transfers", transferBody, 201);
        Check(await As(foreignHttp, "POST", $"transfers/{transfer.DocumentId}/dispatch", new InventoryCommand(transfer.RowVersion)) == 403, "Dispatch requires source-side access.");
        Check(await As(omHttp, "POST", $"transfers/{transfer.DocumentId}/dispatch", new InventoryCommand(transfer.RowVersion)) == 200, "Source-only transfer permission permits dispatch.");
        transfer = await Get(transfer.DocumentId);
        var receive = new InventoryReceive(transfer.RowVersion, [new(transfer.Lines[0].LineId, 1, 0, 0)]);
        Check(await As(omHttp, "POST", $"transfers/{transfer.DocumentId}/receipts", receive) == 403, "Source-only permission cannot receive at destination.");
        Check(await As(foreignHttp, "POST", $"transfers/{transfer.DocumentId}/receipts", receive) == 200, "Destination permission permits receiving.");
        Check(await As(deniedHttp, "POST", $"documents/{seeded.DocumentId}/reverse", new InventoryCommand(seeded.RowVersion, prefix)) == 403, "Reverse permission is mandatory.");
        Check(await As(foreignHttp, "GET", $"warehouses/{authWarehouse.WarehouseId}/items/{item.ItemId}/ledger?from={DateTime.UtcNow.AddDays(-1):O}&to={DateTime.UtcNow:O}") == 403, "Ledger scope is server derived.");
        var take = await Take(authWarehouse.WarehouseId);
        Check(await As(approverHttp, "PUT", $"stocktakes/{take.StocktakeId}/counts", new InventoryCounts(take.RowVersion, [new(take.Lines[0].StocktakeLineId, 0, prefix)])) == 200, "Independent approval fixture may count with a legitimate perform grant.");
        take = await Send<InventoryStocktakeDto>("GET", $"inventory/stocktakes/{take.StocktakeId}");
        Check(await As(approverHttp, "POST", $"stocktakes/{take.StocktakeId}/submit", new InventoryCommand(take.RowVersion)) == 200, "Submit uses real performer.");
        take = await Send<InventoryStocktakeDto>("GET", $"inventory/stocktakes/{take.StocktakeId}");
        Check(await As(approverHttp, "POST", $"stocktakes/{take.StocktakeId}/reconcile", new InventoryCommand(take.RowVersion)) == 200, "Reconcile uses perform permission.");
        take = await Send<InventoryStocktakeDto>("GET", $"inventory/stocktakes/{take.StocktakeId}");
        Check(await As(approverHttp, "POST", $"stocktakes/{take.StocktakeId}/approve-adjustment", new InventoryCommand(take.RowVersion, prefix)) == 400, "Having both permissions cannot bypass counter/approver SoD.");
        Check(await As(omHttp, "POST", $"stocktakes/{take.StocktakeId}/approve-adjustment", new InventoryCommand(take.RowVersion, prefix)) == 200, "Independent branch-authorized approver succeeds.");
        Check(await db.Stocktakes.Where(x => x.StocktakeId == take.StocktakeId).Select(x => x.ApprovedBy).SingleAsync() == omHttp.UserId, "Persisted approval actor must match real session.");
        Check(await As(foreignHttp, "GET", $"stocktakes/{take.StocktakeId}") == 403, "Foreign stocktake read denied.");
        Check(await As(deniedHttp, "POST", "stocktakes", new InventoryStocktakeCreate(source.WarehouseId, prefix)) == 403, "Stocktake perform permission is mandatory.");
    }
}
