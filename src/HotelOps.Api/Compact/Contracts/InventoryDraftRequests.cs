namespace HotelOps.Api.Compact.Contracts;

public sealed record ReceiptDraftRequest(string RowVersion, string? SupplierName, string? Reason);
public sealed record StocktakeItemsRequest(string RowVersion, IReadOnlyList<long> ItemIds);
