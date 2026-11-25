using System.Runtime.CompilerServices;
using HotelOps.Api.Compact.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HotelOps.Api.Compact.Realtime;

[Authorize]
public sealed class NotificationHub(NotificationRealtimeReader reader, TimeProvider clock) : Hub
{
    public async IAsyncEnumerable<NotificationPulse> Subscribe(long? branchId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        lock (Context.Items)
        {
            if (Context.Items.ContainsKey("notification-stream")) throw new HubException("SUBSCRIPTION_ALREADY_ACTIVE");
            Context.Items["notification-stream"] = true;
        }
        try
        {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Context.ConnectionAborted);
        var ct = linked.Token;
        string? previous = null;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
        do
        {
            var pulse = await reader.Read(Context.User!, branchId, ct);
            if (pulse == null) { Context.Abort(); yield break; }
            if (pulse.EventId != previous)
            {
                previous = pulse.EventId;
                yield return pulse;
            }
        } while (await timer.WaitForNextTickAsync(ct));
        }
        finally { lock (Context.Items) Context.Items.Remove("notification-stream"); }
    }
}
