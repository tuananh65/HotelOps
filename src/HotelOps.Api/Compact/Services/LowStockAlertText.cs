using System.Globalization;
using HotelOps.Api.Compact.Entities;

namespace HotelOps.Api.Compact.Services;

public static class LowStockAlertText
{
    private static string Qty(decimal value)=>value.ToString("0.####",CultureInfo.GetCultureInfo("vi-VN"));

    public static string Format(string branch,string warehouse,string item,string unit,bool reusable,
        decimal available,decimal minimum,decimal laundry,decimal incoming,decimal outgoing=0)
    {
        var type=reusable?"Thiếu vật tư sạch sẵn sàng (READY)":"Thiếu tồn khả dụng đủ điều kiện cấp phát";
        var message=$"{branch} · {warehouse} · {item}: {type}; khả dụng {Qty(available)} {unit}; mức tối thiểu {Qty(minimum)} {unit}; thiếu {Qty(Math.Max(0,minimum-available))} {unit}.";
        if(reusable)message+=$" Đang giặt (LAUNDRY): {Qty(laundry)} {unit}.";
        if(incoming>0)message+=$" Đang điều chuyển đến kho: {Qty(incoming)} {unit} (chưa khả dụng).";
        if(outgoing>0)message+=$" Đang điều chuyển đi: {Qty(outgoing)} {unit} (chưa xác nhận nhận đủ).";
        return message;
    }

    public static void Refresh(Notification notification,string message)
    {
        if(notification.NotificationType!="LOW_STOCK"||notification.ExpiresAt!=null)return;
        // Updating an active episode must not redeliver it or erase acknowledgment/read history.
        notification.Message=message;
        notification.Title=message.Length>240?message[..237]+"…":message;
    }
}
