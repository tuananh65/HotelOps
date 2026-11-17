using System.Data;
using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HotelOps.Api.Services.Notifications;

// UC082 posting evidence; caller authorization is checked separately by the
// return notification source and current UC090 requester read policy.
internal static class StockReturnPostedSource
{
    internal sealed record Source(long AuditId,long DocumentId,long RequesterId,long WarehouseId,long BranchId,long ActorId,DateTime At,string LinesJson);
    internal sealed record Line(long Id,long Original,long Item,long Uom,decimal Declared,decimal Accepted,decimal Damaged,decimal Factor,decimal Base,string? Reason);

    internal static Source? Parse(AuditLog audit)
    {
        if(audit.AuditId<=0||audit.EntityType!="stock_return_notification_intents"||audit.Action!="CREATE"
            ||audit.OldValues!=null||audit.ActorUserId is not >0||audit.BranchId is not >0
            ||!long.TryParse(audit.EntityId,NumberStyles.None,CultureInfo.InvariantCulture,out var id)||id<=0)return null;
        try
        {
            using var document=JsonDocument.Parse(audit.NewValues??"null");var value=document.RootElement;
            var requester=value.GetProperty("requesterId").GetInt64();var warehouse=value.GetProperty("warehouseId").GetInt64();
            if(value.GetProperty("businessEvent").GetString()!="RETURN_POSTED"||value.GetProperty("documentId").GetInt64()!=id
                ||requester<=0||warehouse<=0)return null;
            var json=value.GetProperty("lines").GetRawText();var lines=Lines(json);
            if(lines==null||!lines.Any(l=>l.Accepted+l.Damaged>0))return null;
            return new(audit.AuditId,id,requester,warehouse,audit.BranchId.Value,audit.ActorUserId.Value,audit.OccurredAt,json);
        }
        catch(Exception e)when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException){return null;}
    }
    private static Line[]? Lines(string json)
    {
        using var document=JsonDocument.Parse(json);
        if(document.RootElement.ValueKind!=JsonValueKind.Array)return null;
        var rows=document.RootElement.EnumerateArray().Select(v=>new Line(v.GetProperty("lineId").GetInt64(),
            v.GetProperty("originalIssueLineId").GetInt64(),v.GetProperty("ItemId").GetInt64(),v.GetProperty("UomId").GetInt64(),
            v.GetProperty("declared").GetDecimal(),v.GetProperty("accepted").GetDecimal(),v.GetProperty("damaged").GetDecimal(),
            v.GetProperty("ConversionFactor").GetDecimal(),v.GetProperty("acceptedBase").GetDecimal(),v.GetProperty("reason").GetString())).ToArray();
        if(rows.Length==0||rows.Select(l=>l.Id).Distinct().Count()!=rows.Length
            ||rows.Any(l=>l.Id<=0||l.Original<=0||l.Item<=0||l.Uom<=0||l.Declared<=0||l.Accepted<0||l.Damaged<0
                ||l.Accepted+l.Damaged>l.Declared||l.Factor<=0||l.Base<0
                ||l.Accepted*l.Factor!=l.Base||decimal.Round(l.Base,3)!=l.Base
                ||(l.Accepted!=l.Declared||l.Damaged>0)&&(l.Reason?.Trim().Length??0)<3))return null;
        return rows;
    }
    internal static async Task<Source?> ReadAsync(HotelOpsDbContext db,long auditId,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel!=IsolationLevel.Serializable)
            throw new InvalidOperationException("Return event evidence requires a serializable snapshot.");
        var audit=await db.AuditLogs.AsNoTracking().SingleOrDefaultAsync(a=>a.AuditId==auditId,ct);
        var source=audit==null?null:Parse(audit);if(source==null)return null;
        if(!await db.StockDocuments.AsNoTracking().AnyAsync(d=>d.DocumentId==source.DocumentId&&d.DocumentType=="RETURN"&&d.Status=="POSTED"
            &&d.CreatedBy==source.RequesterId&&d.PostedBy==source.ActorId&&d.PostedAt==source.At
            &&d.DestinationWarehouseId==source.WarehouseId&&d.DestinationWarehouse!=null&&d.DestinationWarehouse.BranchId==source.BranchId,ct))return null;
        var expected=Lines(source.LinesJson)!;
        var rows=await db.StockDocumentLines.AsNoTracking().Where(l=>l.DocumentId==source.DocumentId)
            .Include(l=>l.StockReturnLineDetailStockDocumentLine).ThenInclude(r=>r!.OriginalIssueLine).ThenInclude(l=>l.Document)
            .Include(l=>l.StockMovements).ToArrayAsync(ct);
        if(rows.Length!=expected.Length)return null;
        foreach(var line in expected)
        {
            var row=rows.SingleOrDefault(r=>r.LineId==line.Id);var detail=row?.StockReturnLineDetailStockDocumentLine;
            if(row==null||detail==null||detail.OriginalIssueLineId!=line.Original||row.ItemId!=line.Item||row.UomId!=line.Uom
                ||row.RequestedQty!=line.Declared||row.ActualQty!=line.Accepted||detail.DamagedQty!=line.Damaged||row.ConversionFactor!=line.Factor
                ||row.BaseQty!=line.Base||detail.Note!=line.Reason||detail.OriginalIssueLine.Document.DocumentType!="ISSUE"
                ||detail.OriginalIssueLine.ItemId!=line.Item)return null;
            // Damaged quantities are recorded but must never inflate usable stock.
            if(line.Base==0&&row.StockMovements.Count!=0||line.Base>0&&(row.StockMovements.Count!=1
                ||!row.StockMovements.Any(m=>m.WarehouseId==source.WarehouseId&&m.ItemId==line.Item&&m.SignedBaseQty==line.Base
                    &&m.CreatedBy==source.ActorId&&m.OccurredAt==source.At)))return null;
        }
        return source;
    }
}
