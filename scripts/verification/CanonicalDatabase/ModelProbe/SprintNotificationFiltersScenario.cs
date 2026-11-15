using System.Globalization;
using System.Text.Json;
using HotelOps.Api.Data;
using HotelOps.Api.Entities;
using Microsoft.EntityFrameworkCore;

internal static class SprintNotificationFiltersScenario
{
    internal static async Task RunAsync(HotelOpsDbContext db, HttpClient http, long actor, string prefix,
        string username, string password, List<object> checks, CancellationToken ct)
    {
        var titlePrefix = prefix + "FILTER";
        var day = DateTime.UtcNow.Date.AddDays(-2);
        var date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var rows = Enumerable.Range(0, 28).Select(i => new Notification
        {
            NotificationType = i < 26 ? "TASK_ASSIGNED" : "TASK_PLANNED",
            Title = titlePrefix + i.ToString("D2", CultureInfo.InvariantCulture),
            Message = "Synthetic pagination and UTC boundary fixture",
            Priority = "NORMAL",
            CreatedAt = i < 26 ? day : i == 26 ? day.AddTicks(-10) : day.AddDays(1),
            CreatedBy = actor,
        }).ToArray();
        var before = await db.NotificationRecipients.AsNoTracking().OrderBy(r => r.RecipientId)
            .Select(r => new { r.RecipientId, r.ReadAt }).ToArrayAsync(ct);
        try
        {
            db.Notifications.AddRange(rows);
            await db.SaveChangesAsync(ct);
            db.NotificationRecipients.AddRange(rows.Select(n => new NotificationRecipient
            {
                NotificationId = n.NotificationId, UserId = actor, DeliveryChannel = "IN_APP",
                DeliveryStatus = "DELIVERED", DeliveredAt = DateTime.UtcNow,
            }));
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            var expected = rows.Take(26).Select(n => n.NotificationId).OrderDescending().ToArray();
            var filter = $"search={titlePrefix}&type=TASK_ASSIGNED&priority=NORMAL&from={date}&to={date}&unread=true";
            async Task<JsonElement> Page(string query)
            {
                using var response = await http.GetAsync("/api/notifications?" + query, ct);
                if (!response.IsSuccessStatusCode) throw new Exception("Notification filter HTTP failure");
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                return json.RootElement.Clone();
            }
            static long[] Ids(JsonElement page) => page.GetProperty("items").EnumerateArray()
                .Select(n => n.GetProperty("id").GetInt64()).ToArray();
            var first = await Page(filter);
            var second = await Page(filter + "&page=2");
            if (first.GetProperty("totalCount").GetInt32() != 26 || first.GetProperty("pageSize").GetInt32() != 25
                || !Ids(first).Concat(Ids(second)).SequenceEqual(expected))
                throw new Exception("Pagination lost/duplicated rows or violated newest/id ordering");
            var period = await Page($"search={titlePrefix}&from={date}&to={date}");
            if (period.GetProperty("totalCount").GetInt32() != 26)
                throw new Exception("UTC date bounds are not inclusive-start/exclusive-next-day");
            var otherType = await Page($"search={titlePrefix}&type=TASK_PLANNED");
            if (otherType.GetProperty("totalCount").GetInt32() != 2
                || !Ids(otherType).SequenceEqual(new[] { rows[27].NotificationId, rows[26].NotificationId }))
                throw new Exception("Type filtering or chronological sorting failed");
            var empty = await Page($"search={titlePrefix}&priority=NO_MATCH");
            if (empty.GetProperty("totalCount").GetInt32() != 0 || Ids(empty).Length != 0)
                throw new Exception("Priority filter ignored");
            checks.Add(new { notificationPagination25 = true, stableTieOrdering = true, utcDateBounds = true,
                typePrioritySearchFilters = true });
            var browser = await SprintNotificationBrowser.RunAsync(username, password, 0, "", "", ct,
                new { prefix = titlePrefix, date, expected });
            checks.Add(new { filterBrowser = browser });
            if (!browser.GetProperty("success").GetBoolean())
                throw new Exception("Filter browser failed at " + browser.GetProperty("failureStage").GetString());
            if (await db.NotificationRecipients.CountAsync(r => expected.Contains(r.NotificationId) && r.ReadAt != null, ct) != 26
                || await db.NotificationRecipients.AnyAsync(r => (r.NotificationId == rows[26].NotificationId
                    || r.NotificationId == rows[27].NotificationId) && r.ReadAt != null, ct))
                throw new Exception("Filtered browser read scope incorrect in SQL");
            var originalIds = before.Select(r => r.RecipientId).ToArray();
            var after = await db.NotificationRecipients.AsNoTracking().Where(r => originalIds.Contains(r.RecipientId))
                .OrderBy(r => r.RecipientId).Select(r => new { r.RecipientId, r.ReadAt }).ToArrayAsync(ct);
            if (!before.SequenceEqual(after)) throw new Exception("Filter workflow changed unrelated read markers");
            checks.Add(new { filteredReadPersisted = 26, unrelatedReadMarkersUnchanged = true });
        }
        finally
        {
            db.ChangeTracker.Clear();
            await using var cleanup = await db.Database.BeginTransactionAsync(CancellationToken.None);
            var owned = db.Notifications.Where(n => n.CreatedBy == actor && n.Title.StartsWith(titlePrefix));
            await db.NotificationRecipients.Where(r => owned.Select(n => n.NotificationId).Contains(r.NotificationId))
                .ExecuteDeleteAsync(CancellationToken.None);
            await owned.ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.CommitAsync(CancellationToken.None);
        }
    }
}
