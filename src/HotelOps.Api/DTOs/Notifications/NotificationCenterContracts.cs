using System.ComponentModel.DataAnnotations;
namespace HotelOps.Api.DTOs.Notifications;
public sealed class NotificationQuery
{
    [Range(1,int.MaxValue)] public int Page {get;set;}=1;
    [Range(1,100)] public int PageSize {get;set;}=25;
    [StringLength(60)] public string? Type {get;set;}
    [StringLength(20)] public string? Priority {get;set;}
    [StringLength(200)] public string? Search {get;set;}
    public bool? Unread {get;set;}
    public bool? Acknowledged {get;set;}
    public DateOnly? From {get;set;}
    public DateOnly? To {get;set;}
}
public sealed record NotificationRow(long Id,string Type,string Title,string Message,string Priority,DateTimeOffset CreatedAt,DateTimeOffset? ReadAt,bool RequiresAcknowledgment=false,DateTimeOffset? AcknowledgedAt=null);
public sealed record NotificationPage(IReadOnlyList<NotificationRow> Items,int TotalCount,int UnreadCount,int Page,int PageSize,DateTimeOffset Snapshot,bool AcknowledgmentAvailable=false);
public sealed record ReadNotificationsRequest([Required, MinLength(1), MaxLength(100)] long[] Ids);
