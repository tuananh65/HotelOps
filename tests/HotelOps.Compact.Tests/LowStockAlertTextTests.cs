using HotelOps.Api.Compact.Entities;
using HotelOps.Api.Compact.Services;
using Xunit;

namespace HotelOps.Compact.Tests;

public sealed class LowStockAlertTextTests
{
    [Fact]
    public void ReadyShortageKeepsLaundryAndIncomingSeparateFromAvailability()
    {
        var text=LowStockAlertText.Format("Bùi Viện","Kho A","Khăn tắm","Cái",true,10,50,70,20,5);
        Assert.Contains("Bùi Viện · Kho A · Khăn tắm",text);
        Assert.Contains("Thiếu vật tư sạch sẵn sàng (READY)",text);
        Assert.Contains("khả dụng 10 Cái; mức tối thiểu 50 Cái; thiếu 40 Cái",text);
        Assert.Contains("Đang giặt (LAUNDRY): 70 Cái",text);
        Assert.Contains("Đang điều chuyển đến kho: 20 Cái (chưa khả dụng)",text);
        Assert.Contains("Đang điều chuyển đi: 5 Cái (chưa xác nhận nhận đủ)",text);
    }

    [Fact]
    public void ConsumableShortageUsesBaseUnitPrecisionAndDoesNotInventLaundry()
    {
        var text=LowStockAlertText.Format("B","W","Dầu gội","Lít",false,1.25m,2,0,0);
        Assert.Contains("Thiếu tồn khả dụng đủ điều kiện cấp phát",text);
        Assert.Contains("khả dụng 1,25 Lít; mức tối thiểu 2 Lít; thiếu 0,75 Lít",text);
        Assert.DoesNotContain("LAUNDRY",text);Assert.DoesNotContain("điều chuyển",text);
    }

    [Fact]
    public void RefreshPreservesIdentityReadAndDeliveryHistoryAcrossRepeatedUpdates()
    {
        var now=DateTime.UtcNow;
        var notification=new Notification{NotificationId=7,EventKey=Guid.NewGuid(),NotificationType="LOW_STOCK",Message="old",Title="old",ReadAt=now,DeliveredAt=now,CreatedAt=now,AvailableAt=now,AttemptCount=3};
        var key=notification.EventKey;
        var text=LowStockAlertText.Format(new string('B',200),"Kho","Khăn","Cái",true,2,50,4,1);
        LowStockAlertText.Refresh(notification,text);LowStockAlertText.Refresh(notification,text);
        Assert.Equal(text,notification.Message);Assert.True(notification.Title.Length<=240);
        Assert.Equal(7,notification.NotificationId);Assert.Equal(key,notification.EventKey);
        Assert.Equal(now,notification.ReadAt);Assert.Equal(now,notification.DeliveredAt);
        Assert.Equal(now,notification.CreatedAt);Assert.Equal(now,notification.AvailableAt);
        Assert.Equal(3,notification.AttemptCount);Assert.Null(notification.ExpiresAt);
    }

    [Theory]
    [InlineData("LOW_STOCK",true)]
    [InlineData("SUPPLY_POSTED",false)]
    public void RefreshDoesNotRewriteClosedEpisodesOrOtherNotificationTypes(string type,bool closed)
    {
        var notification=new Notification{NotificationType=type,Title="historical",Message="historical",ExpiresAt=closed?DateTime.UtcNow:null};
        LowStockAlertText.Refresh(notification,"current");
        Assert.Equal("historical",notification.Message);Assert.Equal("historical",notification.Title);
    }
}
